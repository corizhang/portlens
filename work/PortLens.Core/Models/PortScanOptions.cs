namespace PortLens.Models;

public sealed class PortScanOptions
{
    public static readonly string[] DefaultDevelopmentFrameworks = ["Vite", "Next.js", "Nuxt", "Django", "FastAPI", "Spring", ".NET", "Docker", "WSL"];

    public bool ShowAll { get; init; }
    public IReadOnlySet<int> ExcludedPorts { get; init; } = new HashSet<int>();
    public IReadOnlySet<string> EnabledFrameworks { get; init; } = DefaultDevelopmentFrameworks.ToHashSet(StringComparer.OrdinalIgnoreCase);
}
