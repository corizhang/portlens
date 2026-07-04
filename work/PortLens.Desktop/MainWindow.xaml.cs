using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using PortLens.Desktop.Dialogs;
using PortLens.Desktop.Properties;
using PortLens.Desktop.Services;
using PortLens.Desktop.Settings;
using PortLens.Desktop.ViewModels;
using PortLens.Services;

namespace PortLens.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _appMetricsTimer = new();
    private readonly DispatcherTimer _settingsSaveTimer = new();
    private readonly DispatcherTimer _updateCheckTimer = new();
    private readonly DesktopSettingsStore _settingsStore = new();
    private readonly TrayIconService _trayIcon;
    private readonly PortEntryActionService _entryActions;
    private readonly UpdateCheckService _updateCheckService;
    private readonly MainWindowViewModel _viewModel;
    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromSeconds(30);
    public SnackbarMessageQueue SnackbarMessageQueue { get; } = new(TimeSpan.FromSeconds(3));
    private DesktopSettings _settings = new();
    private bool _isApplyingSettings;
    private bool _rememberWindowPlacement = true;
    private bool _closeToTray = true;
    private bool _isScanPaused;
    private bool _isExiting;
    private TimeSpan _lastAppCpuTime;
    private DateTimeOffset _lastAppMetricsAt;

    public MainWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _logger = serviceProvider.GetService<ILogger<MainWindow>>();
        _settings = _settingsStore.Load();
        AppSettings.Instance.Apply(_settings);
        ThemeService.ApplyTheme(_settings.Theme);
        LocalizationManager.Instance.ChangeCulture(_settings.Language);

        _viewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
        _viewModel.RefreshIntervalChanged += (_, _) => UpdateScanTimerInterval();
        _viewModel.SnackbarRequested += (_, message) => ShowSnackbar(message);
        DataContext = _viewModel;

        _updateCheckService = serviceProvider.GetRequiredService<UpdateCheckService>();

        ApplyPersistedSettings();

        _entryActions = new PortEntryActionService(
            serviceProvider.GetRequiredService<PortScanner>(),
            message => ShowSnackbar(message),
            () => _viewModel.RefreshAsync());
        _trayIcon = new TrayIconService(
            this,
            ShowMainWindow,
            () => _viewModel.RefreshAsync(),
            ShowSettingsDialogAsync,
            () => _isScanPaused = !_isScanPaused,
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
        _timer.Tick += (_, _) => _ = _viewModel.RefreshAsync();
        _timer.Start();

        UpdateAppMetrics();
        _appMetricsTimer.Interval = TimeSpan.FromSeconds(1);
        _appMetricsTimer.Tick += (_, _) => UpdateAppMetrics();
        _appMetricsTimer.Start();

        Loaded += async (_, _) =>
        {
            await _viewModel.RefreshAsync();
            _ = CheckForUpdatesAsync();
        };

        _updateCheckTimer.Interval = TimeSpan.FromHours(6);
        _updateCheckTimer.Tick += (_, _) => _ = CheckForUpdatesAsync();
        _updateCheckTimer.Start();

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

    public MainWindowViewModel ViewModel => _viewModel;

    private void DialogHost_DialogOpened(object sender, DialogOpenedEventArgs e)
    {
        if (e.Session.Content is FrameworkElement element)
        {
            Dispatcher.BeginInvoke(() =>
            {
                element.Focus();
                Keyboard.Focus(element);
            }, DispatcherPriority.Render);
        }
    }

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
            _viewModel.AppResourceText = Properties.Resources.GetString("AppResourceFormat", cpuText, memoryMb.ToString("0", CultureInfo.CurrentCulture));
        }
        catch
        {
            _viewModel.AppResourceText = Properties.Resources.GetString("AppResourceIdle");
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveSettings();
        if (_isExiting || !_closeToTray)
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
            _ = _viewModel.RefreshAsync();
            return;
        }

        _viewModel.ShowSystemPorts = dialogResult.ShowSystemPorts;
        _viewModel.RefreshIntervalSeconds = dialogResult.RefreshIntervalSeconds;
        _rememberWindowPlacement = dialogResult.RememberWindowPlacement;
        _closeToTray = dialogResult.CloseToTray;
        _viewModel.GroupByProject = dialogResult.GroupByProject;
        _viewModel.ShowAppMetrics = dialogResult.ShowAppMetrics;
        _settings.Theme = dialogResult.Theme;
        _settings.Language = dialogResult.Language;
        AppSettings.Instance.ChineseFontFamily = dialogResult.ChineseFontFamily;
        AppSettings.Instance.EnglishFontFamily = dialogResult.EnglishFontFamily;
        ThemeService.ApplyTheme(dialogResult.Theme);
        LocalizationManager.Instance.ChangeCulture(dialogResult.Language);
        _viewModel.ApplyState(new MainWindowState
        {
            ShowSystemPorts = dialogResult.ShowSystemPorts,
            RefreshIntervalSeconds = dialogResult.RefreshIntervalSeconds,
            GroupByProject = dialogResult.GroupByProject,
            ExcludedPorts = dialogResult.ExcludedPorts,
            EnabledFrameworks = dialogResult.EnabledFrameworks
        });
        SaveSettings();
        _ = _viewModel.RefreshAsync();
    }

    private void ToggleScanPaused()
    {
        _isScanPaused = !_isScanPaused;
        _viewModel.StatusText = _isScanPaused
            ? Properties.Resources.GetString("StatusScanningPaused")
            : Properties.Resources.GetString("StatusScanningResumed");
        if (!_isScanPaused)
        {
            _ = _viewModel.RefreshAsync();
        }
    }

    private void CopyPortSummary()
    {
        var lines = _viewModel.Entries
            .Select(entry => $"{entry.PortText} {entry.DisplayName} | {entry.FrameworkText} | PID {entry.ProcessId} | {entry.Url}")
            .ToArray();

        if (lines.Length == 0)
        {
            ShowSnackbar(Properties.Resources.GetString("SnackbarNoServices"));
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, lines));
            ShowSnackbar(Properties.Resources.GetString("SnackbarCopiedFormat", lines.Length));
        }
        catch (Exception ex)
        {
            ShowSnackbar(Properties.Resources.GetString("SnackbarCopyFailedFormat", ex.Message));
        }
    }

    private TrayStatusSnapshot GetTrayStatus()
    {
        DateTime? lastScanAt = null;
        if (_viewModel.LastScanText.StartsWith("Scan ") &&
            DateTime.TryParseExact(_viewModel.LastScanText[5..], "HH:mm:ss", null, DateTimeStyles.None, out var parsed))
        {
            lastScanAt = parsed;
        }

        return new TrayStatusSnapshot(
            _viewModel.Entries.Count,
            lastScanAt,
            _isScanPaused,
            _viewModel.ShowSystemPorts);
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
            _rememberWindowPlacement = _settings.RememberWindowPlacement;
            _closeToTray = _settings.CloseToTray;
            _viewModel.ShowAppMetrics = _settings.ShowAppMetrics;
            _viewModel.ApplyState(BuildStateFromSettings());

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

    private MainWindowState BuildStateFromSettings()
    {
        return new MainWindowState
        {
            SearchText = _settings.SearchText,
            ShowSystemPorts = _settings.ShowSystemPorts,
            RefreshIntervalSeconds = _settings.RefreshIntervalSeconds,
            GroupByProject = _settings.GroupByProject,
            ExcludedPorts = _settings.ExcludedPorts,
            EnabledFrameworks = _settings.EnabledFrameworks
        };
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

        var state = _viewModel.CaptureState();
        _settings.SearchText = state.SearchText ?? "";
        _settings.ShowSystemPorts = state.ShowSystemPorts;
        _settings.RefreshIntervalSeconds = state.RefreshIntervalSeconds;
        _settings.RememberWindowPlacement = _rememberWindowPlacement;
        _settings.CloseToTray = _closeToTray;
        _settings.GroupByProject = state.GroupByProject;
        _settings.ShowAppMetrics = _viewModel.ShowAppMetrics;
        _settings.Theme = _settings.Theme;
        _settings.Language = _settings.Language;
        _settings.ChineseFontFamily = AppSettings.Instance.ChineseFontFamily;
        _settings.EnglishFontFamily = AppSettings.Instance.EnglishFontFamily;
        _settings.ExcludedPorts = state.ExcludedPorts.ToList();
        _settings.EnabledFrameworks = state.EnabledFrameworks.ToList();
        _settings.IsMaximized = WindowState == WindowState.Maximized;

        if (!_rememberWindowPlacement)
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
            ShowSnackbar(Properties.Resources.GetString("SnackbarSettingsSaveFailed"));
        }
    }

    private void UpdateScanTimerInterval()
    {
        var foregroundInterval = TimeSpan.FromSeconds(_viewModel.RefreshIntervalSeconds);
        if (_isScanPaused)
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

    private void ApplyDefaultSettings()
    {
        _viewModel.ApplyState(new MainWindowState
        {
            SearchText = "",
            ShowSystemPorts = false,
            RefreshIntervalSeconds = 5,
            GroupByProject = true,
            ExcludedPorts = [],
            EnabledFrameworks = DesktopSettings.DefaultEnabledFrameworks
        });
        _rememberWindowPlacement = true;
        _closeToTray = true;
        _viewModel.ShowAppMetrics = true;
        _settings.WindowLeft = null;
        _settings.WindowTop = null;
        _settings.WindowWidth = null;
        _settings.WindowHeight = null;
        _settings.IsMaximized = false;
    }

    private FrameworkElement BuildSettingsDialog()
    {
        return new SettingsDialog(new SettingsDialogState
        {
            ShowSystemPorts = _viewModel.ShowSystemPorts,
            RefreshIntervalSeconds = _viewModel.RefreshIntervalSeconds,
            RememberWindowPlacement = _rememberWindowPlacement,
            CloseToTray = _closeToTray,
            GroupByProject = _viewModel.GroupByProject,
            ShowAppMetrics = _viewModel.ShowAppMetrics,
            Theme = _settings.Theme,
            Language = _settings.Language,
            ChineseFontFamily = _settings.ChineseFontFamily,
            EnglishFontFamily = _settings.EnglishFontFamily,
            ExcludedPorts = _viewModel.CaptureState().ExcludedPorts.ToHashSet(),
            EnabledFrameworks = _viewModel.CaptureState().EnabledFrameworks.ToHashSet(StringComparer.OrdinalIgnoreCase)
        });
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowSettingsDialogAsync();
    }

    private async void StatusBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        await ShowSettingsDialogAsync();
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

        _viewModel.AddToBlacklist(entry.LocalPort);
        SaveSettings();
        ShowSnackbar(Properties.Resources.GetString("SnackbarPortBlacklistedFormat", entry.LocalPort));
    }

    private async void KillProcessTreeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetEntry(sender) is { } entry)
        {
            await _entryActions.KillProcessTreeAsync(entry);
        }
    }

    internal async Task ShowSnackbarAsync(string message)
    {
        await Dispatcher.InvokeAsync(() => ShowSnackbar(message));
    }

    private void ShowSnackbar(string message)
    {
        SnackbarMessageQueue.Enqueue(message);
        _viewModel.StatusText = message;
    }

    private static PortEntryViewModel? GetEntry(object sender)
    {
        return sender is FrameworkElement { Tag: PortEntryViewModel entry } ? entry : null;
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

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var update = await _updateCheckService.CheckAsync();
            if (update?.IsUpdateAvailable != true)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                var message = Properties.Resources.GetString("UpdateAvailableFormat", update.CurrentVersion, update.LatestVersion);
                SnackbarMessageQueue.Enqueue(
                    message,
                    Properties.Resources.GetString("UpdateDownloadButton"),
                    () => Process.Start(new ProcessStartInfo(update.ReleaseUrl) { UseShellExecute = true }));
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Update check failed.");
        }
    }

    private readonly ILogger<MainWindow>? _logger;
}
