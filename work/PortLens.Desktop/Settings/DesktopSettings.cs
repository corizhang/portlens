namespace PortLens.Desktop.Settings;

internal sealed class DesktopSettings
{
    public static readonly string[] DefaultEnabledFrameworks = ["Vite", "Next.js", "Nuxt", "Django", "FastAPI", "Spring", ".NET", "Docker", "WSL"];

    public string SearchText { get; set; } = "";
    public bool ShowSystemPorts { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 5;
    public bool RememberWindowPlacement { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool GroupByProject { get; set; } = true;
    public List<int> ExcludedPorts { get; set; } = [];
    public List<string> EnabledFrameworks { get; set; } = [.. DefaultEnabledFrameworks];
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool IsMaximized { get; set; }
}
