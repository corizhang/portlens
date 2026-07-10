namespace PortLens.Models;

public sealed class FrameworkRule
{
    public string Name { get; set; } = "";
    public List<string> ProcessNameKeywords { get; set; } = [];
    public List<string> CommandLineKeywords { get; set; } = [];
    public List<string> PathKeywords { get; set; } = [];
    public List<int> DefaultPorts { get; set; } = [];

    public FrameworkRule Clone()
    {
        return new FrameworkRule
        {
            Name = Name,
            ProcessNameKeywords = [.. ProcessNameKeywords],
            CommandLineKeywords = [.. CommandLineKeywords],
            PathKeywords = [.. PathKeywords],
            DefaultPorts = [.. DefaultPorts]
        };
    }
}
