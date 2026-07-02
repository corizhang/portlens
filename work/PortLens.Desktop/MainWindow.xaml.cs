using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using MaterialDesignThemes.Wpf;
using PortLens.Models;
using PortLens.Services;
using PortLens.Desktop.Settings;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace PortLens.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly PortScanner _scanner = new();
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _settingsSaveTimer = new();
    private readonly DesktopSettingsStore _settingsStore = new();
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly ObservableCollection<PortEntryViewModel> _entries = new();
    private readonly Dictionary<string, PortEntryViewModel> _entriesByKey = new(StringComparer.Ordinal);
    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromSeconds(30);
    private DesktopSettings _settings = new();
    private bool _isApplyingSettings;
    private bool _isRefreshing;
    private bool _showSystemPorts;
    private int _refreshIntervalSeconds = 5;
    private bool _rememberWindowPlacement = true;
    private string _searchText = "";
    private string _statusText = "Scanning...";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsStore.Load();
        ApplyPersistedSettings();
        DataContext = this;

        FilteredEntries = CollectionViewSource.GetDefaultView(_entries);
        FilteredEntries.Filter = item => item is PortEntryViewModel entry && Matches(entry);

        _notifyIcon = BuildTrayIcon();

        _settingsSaveTimer.Interval = TimeSpan.FromMilliseconds(600);
        _settingsSaveTimer.Tick += (_, _) =>
        {
            _settingsSaveTimer.Stop();
            SaveSettings();
        };

        UpdateScanTimerInterval();
        _timer.Tick += (_, _) => _ = RefreshPortsAsync();
        _timer.Start();

        Loaded += async (_, _) => await RefreshPortsAsync();
        LocationChanged += (_, _) => ScheduleSettingsSave();
        SizeChanged += (_, _) => ScheduleSettingsSave();
        StateChanged += (_, _) =>
        {
            ScheduleSettingsSave();
            UpdateScanTimerInterval();
        };
        IsVisibleChanged += (_, _) => UpdateScanTimerInterval();
        Closing += MainWindow_Closing;
    }

    public ICollectionView FilteredEntries { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            FilteredEntries.Refresh();
            OnPropertyChanged(nameof(IsEmpty));
            ScheduleSettingsSave();
        }
    }

    public bool ShowSystemPorts
    {
        get => _showSystemPorts;
        set
        {
            if (_showSystemPorts == value)
            {
                return;
            }

            _showSystemPorts = value;
            OnPropertyChanged(nameof(ShowSystemPorts));
            ScheduleSettingsSave();
            _ = RefreshPortsAsync();
        }
    }

    public int RefreshIntervalSeconds
    {
        get => _refreshIntervalSeconds;
        set
        {
            var normalized = NormalizeRefreshInterval(value);
            if (_refreshIntervalSeconds == normalized)
            {
                return;
            }

            _refreshIntervalSeconds = normalized;
            UpdateScanTimerInterval();
            OnPropertyChanged(nameof(RefreshIntervalSeconds));
            ScheduleSettingsSave();
        }
    }

    public bool RememberWindowPlacement
    {
        get => _rememberWindowPlacement;
        set
        {
            if (_rememberWindowPlacement == value)
            {
                return;
            }

            _rememberWindowPlacement = value;
            OnPropertyChanged(nameof(RememberWindowPlacement));
            ScheduleSettingsSave();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool IsEmpty => FilteredEntries.IsEmpty;

    private async Task RefreshPortsAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        StatusText = "Scanning in background...";
        var showAll = ShowSystemPorts;

        try
        {
            var entries = await Task.Run(() => _scanner.Scan(showAll));
            ApplyEntries(entries);
            StatusText = showAll
                ? $"{_entries.Count} local listening ports - {DateTime.Now:HH:mm:ss}"
                : $"{_entries.Count} development services - {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void ApplyEntries(IReadOnlyList<PortEntry> entries)
    {
        var liveKeys = entries.Select(CardKey).ToHashSet(StringComparer.Ordinal);
        foreach (var staleKey in _entriesByKey.Keys.Where(key => !liveKeys.Contains(key)).ToList())
        {
            var stale = _entriesByKey[staleKey];
            _entries.Remove(stale);
            _entriesByKey.Remove(staleKey);
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var key = CardKey(entry);
            if (_entriesByKey.TryGetValue(key, out var existing))
            {
                existing.Update(entry);
                var currentIndex = _entries.IndexOf(existing);
                if (currentIndex >= 0 && currentIndex != index)
                {
                    _entries.Move(currentIndex, index);
                }
            }
            else
            {
                var created = new PortEntryViewModel(entry);
                _entriesByKey[key] = created;
                _entries.Insert(Math.Min(index, _entries.Count), created);
            }
        }

        FilteredEntries.Refresh();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private Forms.NotifyIcon BuildTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open PortLens", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Refresh", null, async (_, _) => await RefreshPortsAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            SaveSettings();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Closing -= MainWindow_Closing;
            Close();
        });

        var icon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "PortLens",
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                ShowMainWindow();
            }
        };
        return icon;
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/portlens-icon.ico"));
        return resource is not null
            ? new System.Drawing.Icon(resource.Stream)
            : System.Drawing.SystemIcons.Application;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveSettings();
        e.Cancel = true;
        Hide();
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        UpdateScanTimerInterval();
        Activate();
    }

    private void ApplyPersistedSettings()
    {
        _isApplyingSettings = true;
        try
        {
            _searchText = _settings.SearchText ?? "";
            _showSystemPorts = _settings.ShowSystemPorts;
            _refreshIntervalSeconds = NormalizeRefreshInterval(_settings.RefreshIntervalSeconds);
            _rememberWindowPlacement = _settings.RememberWindowPlacement;

            if (_settings.RememberWindowPlacement && IsUsableWindowPlacement(_settings))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = _settings.WindowLeft!.Value;
                Top = _settings.WindowTop!.Value;
                Width = Math.Max(MinWidth, _settings.WindowWidth!.Value);
                Height = Math.Max(MinHeight, _settings.WindowHeight!.Value);
            }

            if (_settings.RememberWindowPlacement && _settings.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    private static bool IsUsableWindowPlacement(DesktopSettings settings)
    {
        if (settings.WindowLeft is not { } left ||
            settings.WindowTop is not { } top ||
            settings.WindowWidth is not { } width ||
            settings.WindowHeight is not { } height)
        {
            return false;
        }

        if (!double.IsFinite(left) ||
            !double.IsFinite(top) ||
            !double.IsFinite(width) ||
            !double.IsFinite(height) ||
            width < 320 ||
            height < 240)
        {
            return false;
        }

        var right = left + width;
        var bottom = top + height;
        return right > SystemParameters.VirtualScreenLeft &&
               bottom > SystemParameters.VirtualScreenTop &&
               left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
               top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
    }

    private void ScheduleSettingsSave()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private void SaveSettings()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        _settings.SearchText = SearchText;
        _settings.ShowSystemPorts = ShowSystemPorts;
        _settings.RefreshIntervalSeconds = RefreshIntervalSeconds;
        _settings.RememberWindowPlacement = RememberWindowPlacement;
        _settings.IsMaximized = WindowState == WindowState.Maximized;

        if (!RememberWindowPlacement)
        {
            _settings.WindowLeft = null;
            _settings.WindowTop = null;
            _settings.WindowWidth = null;
            _settings.WindowHeight = null;
            _settings.IsMaximized = false;
        }
        else if (WindowState == WindowState.Normal)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
        }

        try
        {
            _settingsStore.Save(_settings);
        }
        catch
        {
            StatusText = "Settings save failed.";
        }
    }

    private static int NormalizeRefreshInterval(int seconds)
    {
        return seconds switch
        {
            3 or 5 or 10 or 30 => seconds,
            _ => 5
        };
    }

    private void UpdateScanTimerInterval()
    {
        var foregroundInterval = TimeSpan.FromSeconds(RefreshIntervalSeconds);
        _timer.Interval = IsVisible && WindowState != WindowState.Minimized
            ? foregroundInterval
            : Max(foregroundInterval, BackgroundRefreshInterval);
    }

    private static TimeSpan Max(TimeSpan first, TimeSpan second)
    {
        return first >= second ? first : second;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshPortsAsync();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowSettingsDialogAsync();
    }

    private async Task ShowSettingsDialogAsync()
    {
        var result = await DialogHost.Show(BuildSettingsDialog(), "RootDialog");
        if (result is not SettingsDialogResult dialogResult)
        {
            return;
        }

        if (dialogResult.Reset)
        {
            ApplyDefaultSettings();
            SaveSettings();
            _ = RefreshPortsAsync();
            return;
        }

        ShowSystemPorts = dialogResult.ShowSystemPorts;
        RefreshIntervalSeconds = dialogResult.RefreshIntervalSeconds;
        RememberWindowPlacement = dialogResult.RememberWindowPlacement;
        SaveSettings();
    }

    private FrameworkElement BuildSettingsDialog()
    {
        var panel = new StackPanel
        {
            Width = 420,
            Margin = new Thickness(24)
        };

        var header = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 18)
        };
        header.Children.Add(new PackIcon
        {
            Kind = PackIconKind.CogOutline,
            Width = 28,
            Height = 28,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(103, 58, 183)),
            Margin = new Thickness(0, 0, 12, 0)
        });
        header.Children.Add(new TextBlock
        {
            Text = "Settings",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 27, 24)),
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(header);

        var showSystemPorts = new ToggleButton
        {
            IsChecked = ShowSystemPorts,
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignSwitchToggleButton"),
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(BuildSettingRow("System ports", "Include non-development listening ports.", showSystemPorts));

        var refreshInterval = new System.Windows.Controls.ComboBox
        {
            Width = 128,
            SelectedValue = RefreshIntervalSeconds,
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedComboBox")
        };
        foreach (var seconds in new[] { 3, 5, 10, 30 })
        {
            refreshInterval.Items.Add(new ComboBoxItem
            {
                Content = $"{seconds} seconds",
                Tag = seconds
            });
        }

        refreshInterval.SelectedIndex = RefreshIntervalSeconds switch
        {
            3 => 0,
            10 => 2,
            30 => 3,
            _ => 1
        };
        panel.Children.Add(BuildSettingRow("Refresh interval", "How often PortLens scans in the background.", refreshInterval));

        var rememberPlacement = new ToggleButton
        {
            IsChecked = RememberWindowPlacement,
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignSwitchToggleButton"),
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(BuildSettingRow("Window placement", "Restore size and position on launch.", rememberPlacement));

        panel.Children.Add(new TextBlock
        {
            Text = "PortLens - local development port monitor",
            Foreground = new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
            FontSize = 12,
            Margin = new Thickness(0, 16, 0, 18)
        });

        var actions = new Grid();
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var reset = new WpfButton
        {
            Content = "Reset",
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedButton"),
            MinWidth = 86,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(179, 38, 30)),
            Command = DialogHost.CloseDialogCommand
        };
        reset.CommandParameter = new SettingsDialogResult
        {
            Reset = true
        };
        Grid.SetColumn(reset, 0);
        actions.Children.Add(reset);

        var rightActions = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };
        var cancel = new WpfButton
        {
            Content = "Cancel",
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedButton"),
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 86,
            Command = DialogHost.CloseDialogCommand
        };
        var save = new WpfButton
        {
            Content = "Save",
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedButton"),
            MinWidth = 86,
            Command = DialogHost.CloseDialogCommand
        };
        save.Click += (_, _) =>
        {
            var selectedSeconds = refreshInterval.SelectedItem is ComboBoxItem { Tag: int seconds }
                ? seconds
                : 5;
            save.CommandParameter = new SettingsDialogResult
            {
                ShowSystemPorts = showSystemPorts.IsChecked == true,
                RefreshIntervalSeconds = selectedSeconds,
                RememberWindowPlacement = rememberPlacement.IsChecked == true
            };
        };
        rightActions.Children.Add(cancel);
        rightActions.Children.Add(save);
        Grid.SetColumn(rightActions, 1);
        actions.Children.Add(rightActions);
        panel.Children.Add(actions);

        return panel;
    }

    private static Grid BuildSettingRow(string title, string description, UIElement control)
    {
        var row = new Grid
        {
            Margin = new Thickness(0, 0, 0, 14)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel
        {
            Margin = new Thickness(0, 0, 20, 0)
        };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(48, 42, 38))
        });
        text.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var presenter = new ContentControl
        {
            Content = control,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(presenter, 1);
        row.Children.Add(presenter);
        return row;
    }

    private void ApplyDefaultSettings()
    {
        SearchText = "";
        ShowSystemPorts = false;
        RefreshIntervalSeconds = 5;
        RememberWindowPlacement = true;
        _settings.WindowLeft = null;
        _settings.WindowTop = null;
        _settings.WindowWidth = null;
        _settings.WindowHeight = null;
        _settings.IsMaximized = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        UpdateScanTimerInterval();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        UpdateScanTimerInterval();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            Process.Start(new ProcessStartInfo(entry.Url) { UseShellExecute = true });
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            System.Windows.Clipboard.SetText(entry.Url);
        }
    }

    private void CopyPidButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            System.Windows.Clipboard.SetText(entry.ProcessId.ToString());
        }
    }

    private void OpenDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is not { } entry || string.IsNullOrWhiteSpace(entry.WorkingDirectory))
        {
            return;
        }

        if (Directory.Exists(entry.WorkingDirectory))
        {
            Process.Start(new ProcessStartInfo(entry.WorkingDirectory) { UseShellExecute = true });
        }
    }

    private async void KillButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is not { } entry)
        {
            return;
        }

        var confirmed = await ShowKillConfirmationAsync(entry);
        if (!confirmed)
        {
            return;
        }

        try
        {
            _scanner.Kill(entry.ProcessId);
            _ = RefreshPortsAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Kill failed", ex.Message);
        }
    }

    private static async Task<bool> ShowKillConfirmationAsync(PortEntryViewModel entry)
    {
        var panel = new StackPanel
        {
            Width = 420,
            Margin = new Thickness(24)
        };

        var header = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 16)
        };
        header.Children.Add(new PackIcon
        {
            Kind = PackIconKind.AlertCircleOutline,
            Width = 30,
            Height = 30,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(179, 38, 30)),
            Margin = new Thickness(0, 0, 12, 0)
        });
        header.Children.Add(new TextBlock
        {
            Text = "Confirm kill",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 27, 24)),
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(header);

        panel.Children.Add(new TextBlock
        {
            Text = $"Kill PID {entry.ProcessId} ({entry.ProcessName}) and its child processes?",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(72, 63, 58)),
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(new TextBlock
        {
            Text = entry.DisplayName,
            FontSize = 13,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
            Margin = new Thickness(0, 0, 0, 20)
        });

        var actions = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };
        var cancel = new WpfButton
        {
            Content = "Cancel",
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedButton"),
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 88,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = false
        };
        var kill = new WpfButton
        {
            Content = "Kill",
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedButton"),
            Foreground = new SolidColorBrush(WpfColor.FromRgb(179, 38, 30)),
            MinWidth = 88,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = true
        };
        actions.Children.Add(cancel);
        actions.Children.Add(kill);
        panel.Children.Add(actions);

        var result = await DialogHost.Show(panel, "RootDialog");
        return result is true;
    }

    private static async Task ShowErrorDialogAsync(string title, string message)
    {
        var panel = new StackPanel
        {
            Width = 420,
            Margin = new Thickness(24)
        };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 27, 24)),
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(72, 63, 58)),
            Margin = new Thickness(0, 0, 0, 20)
        });
        panel.Children.Add(new WpfButton
        {
            Content = "OK",
            HorizontalAlignment = WpfHorizontalAlignment.Right,
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedButton"),
            MinWidth = 88,
            Command = DialogHost.CloseDialogCommand
        });
        await DialogHost.Show(panel, "RootDialog");
    }

    private static PortEntryViewModel? GetEntry(object sender)
    {
        return sender is FrameworkElement { Tag: PortEntryViewModel entry } ? entry : null;
    }

    private bool Matches(PortEntryViewModel entry)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var haystack = string.Join(" ", entry.LocalPort, entry.ProcessId, entry.ProcessName, entry.ProjectName, entry.Framework, entry.CommandText, entry.DirectoryText);
        return haystack.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private static string CardKey(PortEntry entry)
    {
        return $"{entry.Protocol}:{entry.LocalAddress}:{entry.LocalPort}:{entry.ProcessId}";
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.TextBox)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class PortEntryViewModel : INotifyPropertyChanged
{
    private PortEntry _entry;
    private bool _isExpanded;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PortEntryViewModel(PortEntry entry)
    {
        _entry = entry;
    }

    public int LocalPort => _entry.LocalPort;
    public int ProcessId => _entry.ProcessId;
    public string ProcessName => _entry.ProcessName;
    public string? ProjectName => _entry.ProjectName;
    public string? Framework => _entry.Framework;
    public string? WorkingDirectory => _entry.WorkingDirectory;
    public string Url => _entry.Url;
    public string PortText => $":{_entry.LocalPort}";
    public string DisplayName => _entry.DisplayName;
    public string FrameworkText => string.IsNullOrWhiteSpace(_entry.Framework) ? _entry.ProcessName : _entry.Framework;
    public string UptimeText => FormatUptime(_entry.Uptime);
    public string CpuText => _entry.CpuPercent.HasValue ? $"{_entry.CpuPercent:0.0}% CPU" : "CPU ...";
    public string MemoryText => _entry.MemoryBytes.HasValue ? $"{_entry.MemoryBytes.Value / 1024 / 1024} MB" : "";
    public string AddressText => $"{_entry.Protocol} {_entry.LocalAddress}:{_entry.LocalPort}";
    public string CommandText => _entry.CommandLine ?? _entry.ProcessName;
    public string DirectoryText => _entry.WorkingDirectory ?? _entry.ExecutablePath ?? "";

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged(nameof(IsExpanded));
        }
    }

    public void Update(PortEntry entry)
    {
        _entry = entry;
        OnPropertyChanged(nameof(ProcessName));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(Framework));
        OnPropertyChanged(nameof(WorkingDirectory));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FrameworkText));
        OnPropertyChanged(nameof(UptimeText));
        OnPropertyChanged(nameof(CpuText));
        OnPropertyChanged(nameof(MemoryText));
        OnPropertyChanged(nameof(AddressText));
        OnPropertyChanged(nameof(CommandText));
        OnPropertyChanged(nameof(DirectoryText));
    }

    private static string FormatUptime(TimeSpan? uptime)
    {
        if (uptime is null)
        {
            return "";
        }

        if (uptime.Value.TotalDays >= 1)
        {
            return $"{(int)uptime.Value.TotalDays}d {uptime.Value.Hours}h";
        }

        if (uptime.Value.TotalHours >= 1)
        {
            return $"{(int)uptime.Value.TotalHours}h {uptime.Value.Minutes}m";
        }

        return $"{uptime.Value.Minutes}m";
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class SettingsDialogResult
{
    public bool Reset { get; init; }
    public bool ShowSystemPorts { get; init; }
    public int RefreshIntervalSeconds { get; init; } = 5;
    public bool RememberWindowPlacement { get; init; } = true;
}
