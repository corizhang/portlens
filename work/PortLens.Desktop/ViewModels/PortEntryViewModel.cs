using System.ComponentModel;
using PortLens.Models;
using PortLens.Desktop.Services;

namespace PortLens.Desktop.ViewModels;

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
    public string? ExecutablePath => _entry.ExecutablePath;
    public string? ProcessDirectory => !string.IsNullOrWhiteSpace(_entry.ExecutablePath) ? Path.GetDirectoryName(_entry.ExecutablePath) : null;
    public string? ProjectRootDirectory => ProjectRootResolver.Resolve(_entry.WorkingDirectory);
    public string ProjectGroupKey => ProjectRootDirectory ?? _entry.WorkingDirectory ?? _entry.ProcessName;
    public string ProjectGroupTitle => ProjectRootResolver.DisplayName(ProjectRootDirectory ?? _entry.WorkingDirectory, DisplayName);
    public string ProjectGroupSubtitle => ProjectRootDirectory ?? _entry.WorkingDirectory ?? _entry.ProcessName;
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
