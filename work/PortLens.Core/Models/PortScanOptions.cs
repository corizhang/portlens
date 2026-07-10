namespace PortLens.Models;

public sealed class PortScanOptions
{
    public bool ShowAll { get; init; }
    public IReadOnlySet<int> ExcludedPorts { get; init; } = new HashSet<int>();
    public IReadOnlySet<string> EnabledFrameworks { get; init; } = Services.FrameworkRules.DefaultNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<FrameworkRule> FrameworkRules { get; init; } = Services.FrameworkRules.CloneDefaults();
}
