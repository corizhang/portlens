namespace PortLensMaterial.Settings;

internal sealed class DesktopSettings
{
    public string SearchText { get; set; } = "";
    public bool ShowSystemPorts { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 5;
    public bool RememberWindowPlacement { get; set; } = true;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool IsMaximized { get; set; }
}
