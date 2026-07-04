using System.ComponentModel;
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
    public string CpuText => _entry.CpuPercent.HasValue ? $"{_entry.CpuPercent:0.0}% CPU" : "CPU ...";
    public string MemoryText => _entry.MemoryBytes.HasValue ? $"{_entry.MemoryBytes.Value / 1024 / 1024} MB" : "";
    public string AddressText => $"{_entry.Protocol} {_entry.LocalAddress}:{_entry.LocalPort}";
    public string CommandText => _entry.CommandLine ?? _entry.ProcessName;
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
        _entry = entry;
        RecalculateDerivedValues();
        OnPropertyChanged(nameof(ProcessName));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(Framework));
        OnPropertyChanged(nameof(WorkingDirectory));
        OnPropertyChanged(nameof(ExecutablePath));
        OnPropertyChanged(nameof(ProcessDirectory));
        OnPropertyChanged(nameof(ProjectRootDirectory));
        OnPropertyChanged(nameof(ProjectGroupKey));
        OnPropertyChanged(nameof(ProjectGroupTitle));
        OnPropertyChanged(nameof(ProjectGroupSubtitle));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FrameworkText));
        OnPropertyChanged(nameof(UptimeText));
        OnPropertyChanged(nameof(CpuText));
        OnPropertyChanged(nameof(MemoryText));
        OnPropertyChanged(nameof(AddressText));
        OnPropertyChanged(nameof(CommandText));
        OnPropertyChanged(nameof(DirectoryText));
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
