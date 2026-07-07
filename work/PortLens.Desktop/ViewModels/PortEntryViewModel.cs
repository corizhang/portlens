using System.ComponentModel;
using PortLens.Desktop.Properties;
using PortLens.Models;
using PortLens.Services;

namespace PortLens.Desktop.ViewModels;

public sealed class PortEntryViewModel : INotifyPropertyChanged
{
    private PortEntry _entry;
    private bool _isExpanded;

    private string? _cachedProjectRootDirectory;
    private string _cachedProjectGroupKey = "";
    private string _cachedProjectGroupTitle = "";
    private string _cachedProjectGroupSubtitle = "";
    private string _cachedSearchHaystack = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public PortEntryViewModel(PortEntry entry)
    {
        _entry = entry;
        RecalculateDerivedValues();
    }

    public int LocalPort => _entry.LocalPort;
    public int ProcessId => _entry.ProcessId;
    public string ProcessName => _entry.ProcessName;
    public string? ProjectName => _entry.ProjectName;
    public string? Framework => _entry.Framework;
    public string? WorkingDirectory => _entry.WorkingDirectory;
    public string? ExecutablePath => _entry.ExecutablePath;
    public string? ProcessDirectory => !string.IsNullOrWhiteSpace(_entry.ExecutablePath) ? Path.GetDirectoryName(_entry.ExecutablePath) : null;
    public string? ProjectRootDirectory => _cachedProjectRootDirectory;
    public string ProjectGroupKey => _cachedProjectGroupKey;
    public string ProjectGroupTitle => _cachedProjectGroupTitle;
    public string ProjectGroupSubtitle => _cachedProjectGroupSubtitle;
    public string Url => _entry.Url;
    public string PortText => $":{_entry.LocalPort}";
    public string DisplayName => _entry.DisplayName;
    public string FrameworkText => string.IsNullOrWhiteSpace(_entry.Framework) ? _entry.ProcessName : _entry.Framework;
    public string UptimeText => FormatUptime(_entry.Uptime);
    public string CpuText => _entry.CpuPercent.HasValue
        ? Resources.GetString("CpuTextFormat", _entry.CpuPercent.Value)
        : Resources.GetString("CpuTextIdle");
    public string MemoryText => _entry.MemoryBytes.HasValue
        ? Resources.GetString("MemoryTextFormat", _entry.MemoryBytes.Value / 1024 / 1024)
        : "";
    public string AddressText => $"{_entry.Protocol} {_entry.LocalAddress}:{_entry.LocalPort}";
    public string CommandText
    {
        get
        {
            var command = _entry.CommandLine ?? _entry.ProcessName;
            if (command is null)
            {
                return _entry.ProcessName;
            }

            if (command.Length > 300)
            {
                var firstPart = command[..250];
                var lastPart = command[^50..];
                return $"{firstPart} ... {lastPart}";
            }

            return command;
        }
    }

    public string FullCommandText => _entry.CommandLine ?? _entry.ProcessName;

    public string DirectoryText => _entry.WorkingDirectory ?? _entry.ExecutablePath ?? "";

    public PortEntryKey Key => new(_entry.Protocol, _entry.LocalAddress, _entry.LocalPort, _entry.ProcessId);

    public string SearchHaystack => _cachedSearchHaystack;

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
        var oldProcessName = ProcessName;
        var oldProjectName = ProjectName;
        var oldFramework = Framework;
        var oldWorkingDirectory = WorkingDirectory;
        var oldExecutablePath = ExecutablePath;
        var oldProcessDirectory = ProcessDirectory;
        var oldProjectRootDirectory = ProjectRootDirectory;
        var oldProjectGroupKey = ProjectGroupKey;
        var oldProjectGroupTitle = ProjectGroupTitle;
        var oldProjectGroupSubtitle = ProjectGroupSubtitle;
        var oldDisplayName = DisplayName;
        var oldFrameworkText = FrameworkText;
        var oldUptimeText = UptimeText;
        var oldCpuText = CpuText;
        var oldMemoryText = MemoryText;
        var oldAddressText = AddressText;
        var oldCommandText = CommandText;
        var oldDirectoryText = DirectoryText;
        var oldSearchHaystack = SearchHaystack;

        _entry = entry;
        RecalculateDerivedValues();

        RaiseIfChanged(oldProcessName, ProcessName, nameof(ProcessName));
        RaiseIfChanged(oldProjectName, ProjectName, nameof(ProjectName));
        RaiseIfChanged(oldFramework, Framework, nameof(Framework));
        RaiseIfChanged(oldWorkingDirectory, WorkingDirectory, nameof(WorkingDirectory));
        RaiseIfChanged(oldExecutablePath, ExecutablePath, nameof(ExecutablePath));
        RaiseIfChanged(oldProcessDirectory, ProcessDirectory, nameof(ProcessDirectory));
        RaiseIfChanged(oldProjectRootDirectory, ProjectRootDirectory, nameof(ProjectRootDirectory));
        RaiseIfChanged(oldProjectGroupKey, ProjectGroupKey, nameof(ProjectGroupKey));
        RaiseIfChanged(oldProjectGroupTitle, ProjectGroupTitle, nameof(ProjectGroupTitle));
        RaiseIfChanged(oldProjectGroupSubtitle, ProjectGroupSubtitle, nameof(ProjectGroupSubtitle));
        RaiseIfChanged(oldDisplayName, DisplayName, nameof(DisplayName));
        RaiseIfChanged(oldFrameworkText, FrameworkText, nameof(FrameworkText));
        RaiseIfChanged(oldUptimeText, UptimeText, nameof(UptimeText));
        RaiseIfChanged(oldCpuText, CpuText, nameof(CpuText));
        RaiseIfChanged(oldMemoryText, MemoryText, nameof(MemoryText));
        RaiseIfChanged(oldAddressText, AddressText, nameof(AddressText));
        RaiseIfChanged(oldCommandText, CommandText, nameof(CommandText));
        RaiseIfChanged(oldDirectoryText, DirectoryText, nameof(DirectoryText));
        RaiseIfChanged(oldSearchHaystack, SearchHaystack, nameof(SearchHaystack));
    }

    private void RaiseIfChanged(string? oldValue, string? newValue, string propertyName)
    {
        if (!string.Equals(oldValue, newValue))
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void RecalculateDerivedValues()
    {
        _cachedProjectRootDirectory = ProjectRootResolver.Resolve(_entry.WorkingDirectory);
        _cachedProjectGroupKey = _cachedProjectRootDirectory ?? _entry.WorkingDirectory ?? _entry.ProcessName;
        _cachedProjectGroupTitle = ProjectRootResolver.DisplayName(
            _cachedProjectRootDirectory ?? _entry.WorkingDirectory, DisplayName);
        _cachedProjectGroupSubtitle = ProjectRootResolver.ComputeRelativeSubtitle(
            _cachedProjectRootDirectory, _entry.WorkingDirectory, DisplayName);
        _cachedSearchHaystack = string.Join(" ",
            _entry.LocalPort, _entry.ProcessId, _entry.ProcessName, _entry.ProjectName,
            _cachedProjectGroupTitle, _cachedProjectGroupSubtitle,
            _entry.Framework, CommandText, DirectoryText);
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
