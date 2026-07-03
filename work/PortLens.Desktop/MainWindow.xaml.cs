using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using PortLens.Desktop.Dialogs;
using PortLens.Desktop.Services;
using PortLens.Models;
using PortLens.Services;
using PortLens.Desktop.Settings;
using PortLens.Desktop.ViewModels;

namespace PortLens.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly PortScanner _scanner = new();
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _appMetricsTimer = new();
    private readonly DispatcherTimer _settingsSaveTimer = new();
    private readonly DesktopSettingsStore _settingsStore = new();
    private readonly TrayIconService _trayIcon;
    private readonly PortEntryActionService _entryActions;
    private readonly ObservableCollection<PortEntryViewModel> _entries = new();
    private readonly Dictionary<string, PortEntryViewModel> _entriesByKey = new(StringComparer.Ordinal);
    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromSeconds(30);
    public SnackbarMessageQueue SnackbarMessageQueue { get; } = new(TimeSpan.FromSeconds(3));
    private DesktopSettings _settings = new();
    private bool _isApplyingSettings;
    private bool _isRefreshing;
    private bool _showSystemPorts;
    private int _refreshIntervalSeconds = 5;
    private bool _rememberWindowPlacement = true;
    private bool _closeToTray = true;
    private bool _groupByProject = true;
    private bool _showAppMetrics = true;
    private bool _isScanPaused;
    private bool _isExiting;
    private HashSet<int> _excludedPorts = new();
    private HashSet<string> _enabledFrameworks = new(StringComparer.OrdinalIgnoreCase);
    private string _searchText = "";
    private string _statusText = "Scanning...";
    private string _serviceCountText = "0 services";
    private string _lastScanText = "Scan --:--:--";
    private string _appResourceText = "CPU --  Mem --";
    private DateTime? _lastScanAt;
    private TimeSpan _lastAppCpuTime;
    private DateTimeOffset _lastAppMetricsAt;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsStore.Load();
        ApplyPersistedSettings();
        DataContext = this;

        FilteredEntries = CollectionViewSource.GetDefaultView(_entries);
        FilteredEntries.Filter = item => item is PortEntryViewModel entry && Matches(entry);
        ApplyGrouping();

        _entryActions = new PortEntryActionService(_scanner, ShowSnackbar, RefreshPortsAsync);
        _trayIcon = new TrayIconService(
            this,
            ShowMainWindow,
            RefreshPortsAsync,
            ShowSettingsDialogAsync,
            ToggleScanPaused,
            CopyPortSummary,
            GetTrayStatus,
            ExitApplication);

        _settingsSaveTimer.Interval = TimeSpan.FromMilliseconds(600);
        _settingsSaveTimer.Tick += (_, _) =>
        {
            _settingsSaveTimer.Stop();
            SaveSettings();
        };

        UpdateScanTimerInterval();
        _timer.Tick += (_, _) => _ = RefreshPortsAsync();
        _timer.Start();

        UpdateAppMetrics();
        _appMetricsTimer.Interval = TimeSpan.FromSeconds(1);
        _appMetricsTimer.Tick += (_, _) => UpdateAppMetrics();
        _appMetricsTimer.Start();

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

    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            if (_closeToTray == value)
            {
                return;
            }

            _closeToTray = value;
            OnPropertyChanged(nameof(CloseToTray));
            ScheduleSettingsSave();
        }
    }

    public bool GroupByProject
    {
        get => _groupByProject;
        set
        {
            if (_groupByProject == value)
            {
                return;
            }

            _groupByProject = value;
            ApplyGrouping();
            OnPropertyChanged(nameof(GroupByProject));
            ScheduleSettingsSave();
        }
    }

    public bool ShowAppMetrics
    {
        get => _showAppMetrics;
        set
        {
            if (_showAppMetrics == value)
            {
                return;
            }

            _showAppMetrics = value;
            OnPropertyChanged(nameof(ShowAppMetrics));
            ScheduleSettingsSave();
        }
    }

    public bool IsScanPaused
    {
        get => _isScanPaused;
        private set
        {
            if (_isScanPaused == value)
            {
                return;
            }

            _isScanPaused = value;
            UpdateScanTimerInterval();
            OnPropertyChanged(nameof(IsScanPaused));
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

    public string ServiceCountText
    {
        get => _serviceCountText;
        private set
        {
            _serviceCountText = value;
            OnPropertyChanged(nameof(ServiceCountText));
        }
    }

    public string LastScanText
    {
        get => _lastScanText;
        private set
        {
            _lastScanText = value;
            OnPropertyChanged(nameof(LastScanText));
        }
    }

    public string AppResourceText
    {
        get => _appResourceText;
        private set
        {
            _appResourceText = value;
            OnPropertyChanged(nameof(AppResourceText));
        }
    }

    public string AppVersionText { get; } = $"v{GetAppVersion()}";

    public bool IsEmpty => FilteredEntries.IsEmpty;

    private void UpdateAppMetrics()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var now = DateTimeOffset.UtcNow;
            var totalCpu = process.TotalProcessorTime;
            var memoryMb = process.WorkingSet64 / 1024d / 1024d;
            var cpuText = "--";

            if (_lastAppMetricsAt != default)
            {
                var elapsedMs = (now - _lastAppMetricsAt).TotalMilliseconds;
                var cpuMs = (totalCpu - _lastAppCpuTime).TotalMilliseconds;
                if (elapsedMs > 0)
                {
                    var cpuPercent = Math.Max(0, cpuMs / elapsedMs / Environment.ProcessorCount * 100);
                    cpuText = $"{cpuPercent:0.0}%";
                }
            }

            _lastAppMetricsAt = now;
            _lastAppCpuTime = totalCpu;
            AppResourceText = $"CPU {cpuText}  Mem {memoryMb:0} MB";
        }
        catch
        {
            AppResourceText = "CPU --  Mem --";
        }
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var version = !string.IsNullOrWhiteSpace(informationalVersion)
            ? informationalVersion
            : assembly.GetName().Version?.ToString(3);

        return string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Split('+')[0];
    }

    private async Task RefreshPortsAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        if (IsScanPaused)
        {
            StatusText = "Scanning paused.";
            return;
        }

        _isRefreshing = true;
        StatusText = "Scanning in background...";
        var showAll = ShowSystemPorts;

        try
        {
            var options = new PortScanOptions
            {
                ShowAll = showAll,
                ExcludedPorts = _excludedPorts.ToHashSet(),
                EnabledFrameworks = _enabledFrameworks.ToHashSet(StringComparer.OrdinalIgnoreCase)
            };
            var entries = await Task.Run(() => _scanner.Scan(options));
            ApplyEntries(entries);
            _lastScanAt = DateTime.Now;
            ServiceCountText = showAll
                ? $"{_entries.Count} ports"
                : $"{_entries.Count} services";
            LastScanText = $"Scan {_lastScanAt:HH:mm:ss}";
            StatusText = showAll
                ? "Local listening ports"
                : "Development services";
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

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveSettings();
        if (_isExiting || !CloseToTray)
        {
            _trayIcon.Dispose();
            return;
        }

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
        CloseToTray = dialogResult.CloseToTray;
        GroupByProject = dialogResult.GroupByProject;
        ShowAppMetrics = dialogResult.ShowAppMetrics;
        _excludedPorts = NormalizeExcludedPorts(dialogResult.ExcludedPorts);
        _enabledFrameworks = NormalizeEnabledFrameworks(dialogResult.EnabledFrameworks);
        SaveSettings();
        _ = RefreshPortsAsync();
    }

    private void ToggleScanPaused()
    {
        IsScanPaused = !IsScanPaused;
        StatusText = IsScanPaused ? "Scanning paused." : "Scanning resumed.";
        if (!IsScanPaused)
        {
            _ = RefreshPortsAsync();
        }
    }

    private void CopyPortSummary()
    {
        var lines = _entries
            .Select(entry => $"{entry.PortText} {entry.DisplayName} | {entry.FrameworkText} | PID {entry.ProcessId} | {entry.Url}")
            .ToArray();

        if (lines.Length == 0)
        {
            ShowSnackbar("No services to copy.");
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, lines));
            ShowSnackbar($"Copied {lines.Length} port summaries.");
        }
        catch (Exception ex)
        {
            ShowSnackbar($"Copy failed: {ex.Message}");
        }
    }

    private TrayStatusSnapshot GetTrayStatus()
    {
        return new TrayStatusSnapshot(
            _entries.Count,
            _lastScanAt,
            IsScanPaused,
            ShowSystemPorts);
    }

    private void ExitApplication()
    {
        _isExiting = true;
        SaveSettings();
        Close();
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
            _closeToTray = _settings.CloseToTray;
            _groupByProject = _settings.GroupByProject;
            _showAppMetrics = _settings.ShowAppMetrics;
            _excludedPorts = NormalizeExcludedPorts(_settings.ExcludedPorts);
            _enabledFrameworks = NormalizeEnabledFrameworks(_settings.EnabledFrameworks);

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
        _settings.CloseToTray = CloseToTray;
        _settings.GroupByProject = GroupByProject;
        _settings.ShowAppMetrics = ShowAppMetrics;
        _settings.ExcludedPorts = _excludedPorts.Order().ToList();
        _settings.EnabledFrameworks = _enabledFrameworks
            .OrderBy(framework => Array.IndexOf(DesktopSettings.DefaultEnabledFrameworks, framework))
            .ToList();
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
            ShowSnackbar("Settings save failed.");
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
        if (IsScanPaused)
        {
            _timer.Stop();
            return;
        }

        _timer.Interval = IsVisible && WindowState != WindowState.Minimized
            ? foregroundInterval
            : Max(foregroundInterval, BackgroundRefreshInterval);
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private static TimeSpan Max(TimeSpan first, TimeSpan second)
    {
        return first >= second ? first : second;
    }

    private void ApplyGrouping()
    {
        if (FilteredEntries is null)
        {
            return;
        }

        FilteredEntries.GroupDescriptions.Clear();
        if (GroupByProject)
        {
            FilteredEntries.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PortEntryViewModel.ProjectGroupKey)));
        }

        FilteredEntries.Refresh();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshPortsAsync();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowSettingsDialogAsync();
    }

    private async void StatusBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        await ShowSettingsDialogAsync();
    }

    private FrameworkElement BuildSettingsDialog()
    {
        return new SettingsDialogBuilder(
            ShowSystemPorts,
            RefreshIntervalSeconds,
            RememberWindowPlacement,
            CloseToTray,
            GroupByProject,
            ShowAppMetrics,
            _excludedPorts,
            _enabledFrameworks).Build();
    }

    private void ApplyDefaultSettings()
    {
        SearchText = "";
        ShowSystemPorts = false;
        RefreshIntervalSeconds = 5;
        RememberWindowPlacement = true;
        CloseToTray = true;
        GroupByProject = true;
        ShowAppMetrics = true;
        _excludedPorts.Clear();
        _enabledFrameworks = NormalizeEnabledFrameworks(DesktopSettings.DefaultEnabledFrameworks);
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
            _entryActions.OpenUrl(entry);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            _entryActions.CopyUrl(entry);
        }
    }

    private void CopyPidButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            _entryActions.CopyPid(entry);
        }
    }

    private void OpenDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            _entryActions.OpenProjectDirectory(entry);
        }
    }

    private async void KillButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is not { } entry)
        {
            return;
        }

        await _entryActions.KillProcessTreeAsync(entry);
    }

    private void OpenUrlMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            _entryActions.OpenUrl(entry);
        }
    }

    private void CopyUrlMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            _entryActions.CopyUrl(entry);
        }
    }

    private void CopyCommandLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            _entryActions.CopyCommandLine(entry);
        }
    }

    private void OpenProcessDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            _entryActions.OpenProcessDirectory(entry);
        }
    }

    private void OpenProjectDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            _entryActions.OpenProjectDirectory(entry);
        }
    }

    private void OpenTerminalMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            _entryActions.OpenTerminal(entry);
        }
    }

    private void BlacklistPortMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PortEntryViewModel entry })
        {
            return;
        }

        _excludedPorts.Add(entry.LocalPort);
        SaveSettings();
        ShowSnackbar($"Port {entry.LocalPort} added to blacklist.");
        _ = RefreshPortsAsync();
    }

    private async void KillProcessTreeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            await _entryActions.KillProcessTreeAsync(entry);
        }
    }

    private void ShowSnackbar(string message)
    {
        SnackbarMessageQueue.Enqueue(message);
        StatusText = message;
    }

    private static PortEntryViewModel? GetEntry(object sender)
    {
        return sender is FrameworkElement { Tag: PortEntryViewModel entry } ? entry : null;
    }

    private static HashSet<int> NormalizeExcludedPorts(IEnumerable<int>? ports)
    {
        return ports?
            .Where(port => port is > 0 and <= 65535)
            .ToHashSet()
            ?? new HashSet<int>();
    }

    private static HashSet<string> NormalizeEnabledFrameworks(IEnumerable<string>? frameworks)
    {
        var valid = DesktopSettings.DefaultEnabledFrameworks.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var provided = frameworks?.ToArray();
        var normalized = provided?
            .Where(framework => valid.Contains(framework))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalized is null)
        {
            return valid;
        }

        var previousDefault = valid.Where(framework => !framework.Equals("Go", StringComparison.OrdinalIgnoreCase)).ToArray();
        var wasPreviousDefault = normalized.Count == previousDefault.Length &&
            previousDefault.All(framework => normalized.Contains(framework));
        if (wasPreviousDefault)
        {
            normalized.Add("Go");
        }

        return normalized;
    }

    private bool Matches(PortEntryViewModel entry)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var haystack = string.Join(" ", entry.LocalPort, entry.ProcessId, entry.ProcessName, entry.ProjectName, entry.ProjectGroupTitle, entry.ProjectGroupSubtitle, entry.Framework, entry.CommandText, entry.DirectoryText);
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
