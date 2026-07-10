namespace PortLens.Desktop.Settings;

using PortLens.Models;
using PortLens.Services;

internal sealed class DesktopSettings
{
    public const int CurrentVersion = 3;
    public static readonly string[] DefaultEnabledFrameworks = PortLens.Services.FrameworkRules.DefaultNames();

    public int Version { get; set; } = CurrentVersion;
    public string SearchText { get; set; } = "";
    public bool ShowSystemPorts { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 5;
    public bool RememberWindowPlacement { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool GroupByProject { get; set; } = true;
    public bool ShowAppMetrics { get; set; } = true;
    public Theme Theme { get; set; } = Theme.Light;
    public string Language { get; set; } = "en-US";
    public List<int> ExcludedPorts { get; set; } = [];
    public List<string> EnabledFrameworks { get; set; } = [.. DefaultEnabledFrameworks];
    public List<FrameworkRule> FrameworkRules { get; set; } = PortLens.Services.FrameworkRules.CloneDefaults().ToList();
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public string ChineseFontFamily { get; set; } = "";
    public string EnglishFontFamily { get; set; } = "";
    public bool IsMaximized { get; set; }
}
