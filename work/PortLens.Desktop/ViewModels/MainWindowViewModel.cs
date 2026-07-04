using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PortLens.Desktop.Services;
using PortLens.Desktop.Settings;
using PortLens.Models;
using PortLens.Services;

namespace PortLens.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly PortScanner _scanner;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ObservableCollection<PortEntryViewModel> _entries = new();
    private readonly Dictionary<string, PortEntryViewModel> _entriesByKey = new(StringComparer.Ordinal);
    private readonly object _refreshLock = new();
    private readonly DispatcherTimer _searchDebounceTimer;
    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _searchCts;
    private HashSet<string> _matchingKeys = new(StringComparer.Ordinal);

    private bool _showSystemPorts;
    private int _refreshIntervalSeconds = 5;
    private bool _groupByProject = true;
    private string _searchText = "";
    private string _statusText = "Scanning...";
    private string _serviceCountText = "0 services";
    private string _lastScanText = "Scan --:--:--";
    private DateTime? _lastScanAt;
    private bool _isRefreshing;
    private bool _isLoading;
    private bool _showAppMetrics = true;
    private string _appResourceText = "CPU --  Mem --";
    private HashSet<int> _excludedPorts = new();
    private HashSet<string> _enabledFrameworks = new(StringComparer.OrdinalIgnoreCase);

    public MainWindowViewModel(PortScanner scanner, ILogger<MainWindowViewModel> logger)
    {
        _scanner = scanner;
        _logger = logger;

        FilteredEntries = CollectionViewSource.GetDefaultView(_entries);
        FilteredEntries.Filter = item => item is PortEntryViewModel entry && (string.IsNullOrWhiteSpace(SearchText) || _matchingKeys.Contains(entry.Key));
        ApplyGrouping();

        _searchDebounceTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(150),
            IsEnabled = false
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            _ = RefreshSearchFilterAsync();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
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
            _ = RefreshAsync();
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
            OnPropertyChanged(nameof(RefreshIntervalSeconds));
            RefreshIntervalChanged?.Invoke(this, EventArgs.Empty);
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
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public string ServiceCountText
    {
        get => _serviceCountText;
        set
        {
            _serviceCountText = value;
            OnPropertyChanged(nameof(ServiceCountText));
        }
    }

    public string LastScanText
    {
        get => _lastScanText;
        set
        {
            _lastScanText = value;
            OnPropertyChanged(nameof(LastScanText));
        }
    }

    public bool IsEmpty => FilteredEntries.IsEmpty;

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
        }
    }

    public string AppResourceText
    {
        get => _appResourceText;
        set
        {
            _appResourceText = value;
            OnPropertyChanged(nameof(AppResourceText));
        }
    }

    public string AppVersionText { get; } = $"v{GetAppVersion()}";

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(EmptyTitle));
            OnPropertyChanged(nameof(EmptySubtitle));
        }
    }

    public string EmptyTitle => IsLoading ? "Scanning..." : "No development services found";

    public string EmptySubtitle => IsLoading ? "Looking for local listening ports." : "Start a dev server, or enable System ports.";

    public IReadOnlyCollection<PortEntryViewModel> Entries => _entries;

    public event EventHandler? RefreshIntervalChanged;

    public async Task RefreshAsync()
    {
        lock (_refreshLock)
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
        }

        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var cancellationToken = _refreshCts.Token;

        IsLoading = true;
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
            var entries = await Task.Run(() => _scanner.Scan(options, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
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
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed.");
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            lock (_refreshLock)
            {
                _isRefreshing = false;
            }
        }
    }

    public void KillProcess(int processId)
    {
        _scanner.Kill(processId);
    }

    public int CountChildProcesses(int processId)
    {
        return _scanner.CountChildProcesses(processId);
    }

    public void AddToBlacklist(int port)
    {
        _excludedPorts.Add(port);
        _ = RefreshAsync();
    }

    public void ApplyState(MainWindowState state)
    {
        SearchText = state.SearchText ?? "";
        ShowSystemPorts = state.ShowSystemPorts;
        RefreshIntervalSeconds = NormalizeRefreshInterval(state.RefreshIntervalSeconds);
        GroupByProject = state.GroupByProject;
        _excludedPorts = NormalizeExcludedPorts(state.ExcludedPorts);
        _enabledFrameworks = NormalizeEnabledFrameworks(state.EnabledFrameworks);
        ApplyGrouping();
    }

    public MainWindowState CaptureState()
    {
        return new MainWindowState
        {
            SearchText = SearchText,
            ShowSystemPorts = ShowSystemPorts,
            RefreshIntervalSeconds = RefreshIntervalSeconds,
            GroupByProject = GroupByProject,
            ExcludedPorts = _excludedPorts.Order().ToList(),
            EnabledFrameworks = _enabledFrameworks
                .OrderBy(framework => Array.IndexOf(DesktopSettings.DefaultEnabledFrameworks, framework))
                .ToList()
        };
    }

    public event EventHandler<string>? SnackbarRequested;

    public async Task ShowSnackbarAsync(string message)
    {
        await Task.Run(() => SnackbarRequested?.Invoke(this, message));
    }

    private void ApplyEntries(IReadOnlyList<PortEntry> entries)
    {
        var liveKeys = entries.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var staleKey in _entriesByKey.Keys.Where(key => !liveKeys.Contains(key)).ToList())
        {
            var stale = _entriesByKey[staleKey];
            _entries.Remove(stale);
            _entriesByKey.Remove(staleKey);
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var key = entry.Key;
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

        _ = RefreshSearchFilterAsync();
    }

    private void ApplyGrouping()
    {
        FilteredEntries.GroupDescriptions.Clear();
        if (GroupByProject)
        {
            FilteredEntries.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PortEntryViewModel.ProjectGroupKey)));
        }

        FilteredEntries.Refresh();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task RefreshSearchFilterAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var cancellationToken = _searchCts.Token;
        var text = SearchText;
        var entries = _entries.ToList();

        try
        {
            var matchingKeys = await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return entries.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
                }

                return entries
                    .Where(entry => MatchesText(entry, text))
                    .Select(entry => entry.Key)
                    .ToHashSet(StringComparer.Ordinal);
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _matchingKeys = matchingKeys;
            FilteredEntries.Refresh();
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search filter update failed.");
        }
    }

    private static bool MatchesText(PortEntryViewModel entry, string text)
    {
        var haystack = string.Join(" ", entry.LocalPort, entry.ProcessId, entry.ProcessName, entry.ProjectName, entry.ProjectGroupTitle, entry.ProjectGroupSubtitle, entry.Framework, entry.CommandText, entry.DirectoryText);
        return haystack.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static int NormalizeRefreshInterval(int seconds)
    {
        return seconds switch
        {
            3 or 5 or 10 or 30 => seconds,
            _ => 5
        };
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
        var normalized = frameworks?
            .Where(framework => valid.Contains(framework))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return normalized is { Count: > 0 } ? normalized : valid;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
}

public sealed class MainWindowState
{
    public string? SearchText { get; set; }
    public bool ShowSystemPorts { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 5;
    public bool GroupByProject { get; set; } = true;
    public IReadOnlyList<int> ExcludedPorts { get; set; } = [];
    public IReadOnlyList<string> EnabledFrameworks { get; set; } = [];
}
