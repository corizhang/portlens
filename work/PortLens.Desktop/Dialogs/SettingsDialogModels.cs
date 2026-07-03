using System.Windows;

namespace PortLens.Desktop.Dialogs;

internal sealed class SettingsDialogResult
{
    public bool Reset { get; init; }
    public bool ShowSystemPorts { get; init; }
    public int RefreshIntervalSeconds { get; init; } = 5;
    public bool RememberWindowPlacement { get; init; } = true;
    public bool CloseToTray { get; init; } = true;
    public bool GroupByProject { get; init; } = true;
    public bool ShowAppMetrics { get; init; } = true;
    public IReadOnlyList<int> ExcludedPorts { get; init; } = [];
    public IReadOnlyList<string> EnabledFrameworks { get; init; } = [];
}

internal sealed class SettingsDialogState
{
    public bool ShowSystemPorts { get; init; }
    public int RefreshIntervalSeconds { get; init; } = 5;
    public bool RememberWindowPlacement { get; init; } = true;
    public bool CloseToTray { get; init; } = true;
    public bool GroupByProject { get; init; } = true;
    public bool ShowAppMetrics { get; init; } = true;
    public IReadOnlySet<int> ExcludedPorts { get; init; } = new HashSet<int>();
    public IReadOnlySet<string> EnabledFrameworks { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
