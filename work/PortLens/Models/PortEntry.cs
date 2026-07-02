namespace PortLens.Models;

internal sealed class PortEntry
{
    public string Protocol { get; init; } = "";
    public string LocalAddress { get; init; } = "";
    public int LocalPort { get; init; }
    public string State { get; init; } = "";
    public int ProcessId { get; init; }
    public string ProcessName { get; set; } = "";
    public string? ExecutablePath { get; set; }
    public string? CommandLine { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? ProjectName { get; set; }
    public string? Framework { get; set; }
    public bool IsRecognizedDevelopmentService { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public TimeSpan? Uptime { get; set; }
    public double? CpuPercent { get; set; }
    public long? MemoryBytes { get; set; }
    public string RiskLevel { get; set; } = "Low";

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(ProjectName) ? ProjectName :
        !string.IsNullOrWhiteSpace(ProcessName) ? ProcessName :
        $"PID {ProcessId}";

    public string Url => $"http://localhost:{LocalPort}";
}
