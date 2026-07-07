using System.Globalization;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PortLens.Models;
using PortLens.Services;

namespace PortLens.Benchmarks;

[MemoryDiagnoser]
public class ScanBenchmark
{
    private PortScanner _scanner = null!;
    private PortScanOptions _showAllOptions = null!;
    private PortScanOptions _filteredOptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var inspector = new ProcessInspector(
            new ProcessCommandLineReader(loggerFactory.CreateLogger<ProcessCommandLineReader>()),
            new ProcessCurrentDirectoryReader(loggerFactory.CreateLogger<ProcessCurrentDirectoryReader>()),
            new ProcessTreeReader(loggerFactory.CreateLogger<ProcessTreeReader>()),
            loggerFactory.CreateLogger<ProcessInspector>());
        _scanner = new PortScanner(inspector);

        _showAllOptions = new PortScanOptions
        {
            ShowAll = true,
            ExcludedPorts = new HashSet<int>(),
            EnabledFrameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
        _filteredOptions = new PortScanOptions
        {
            ShowAll = false,
            ExcludedPorts = new HashSet<int>(),
            EnabledFrameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Node.js",
                ".NET",
                "Java",
                "Go",
                "Python",
                "Vite",
                "Next.js"
            }
        };
    }

    [Benchmark]
    public IReadOnlyList<PortEntry> ScanShowAll()
    {
        return _scanner.Scan(_showAllOptions);
    }

    [Benchmark]
    public IReadOnlyList<PortEntry> ScanFiltered()
    {
        return _scanner.Scan(_filteredOptions);
    }
}

[MemoryDiagnoser]
public class FrameworkDetectionBenchmark
{
    private readonly PortEntry _viteEntry = new()
    {
        ProcessName = "node",
        CommandLine = "node node_modules/.bin/vite --host 127.0.0.1",
        WorkingDirectory = "C:\\Projects\\frontend",
        ExecutablePath = "C:\\Program Files\\nodejs\\node.exe"
    };

    private readonly PortEntry _dotnetEntry = new()
    {
        ProcessName = "dotnet",
        CommandLine = "dotnet run --project MyApp --urls http://127.0.0.1:5000",
        WorkingDirectory = "C:\\Projects\\api",
        ExecutablePath = "C:\\Program Files\\dotnet\\dotnet.exe"
    };

    private readonly PortEntry _springEntry = new()
    {
        ProcessName = "java",
        CommandLine = "java -jar target/app-0.0.1-SNAPSHOT.jar",
        WorkingDirectory = "C:\\Projects\\backend",
        ExecutablePath = "C:\\Program Files\\Java\\bin\\java.exe"
    };

    [Benchmark]
    public string InferVite() => FrameworkDetector.InferFramework(_viteEntry);

    [Benchmark]
    public string InferDotNet() => FrameworkDetector.InferFramework(_dotnetEntry);

    [Benchmark]
    public string InferSpring() => FrameworkDetector.InferFramework(_springEntry);
}

[MemoryDiagnoser]
public class ProjectRootResolutionBenchmark
{
    private readonly string _path = Path.GetDirectoryName(typeof(ProjectRootResolutionBenchmark).Assembly.Location)!;

    [Benchmark]
    public string? ResolveRoot() => ProjectRootResolver.Resolve(_path);

    [Benchmark]
    public string ComputeSubtitle() => ProjectRootResolver.ComputeRelativeSubtitle(_path, _path, "fallback");
}

[MemoryDiagnoser]
public class ProcessTreeBenchmark
{
    private readonly ProcessTreeReader _reader = new(NullLoggerFactory.Instance.CreateLogger<ProcessTreeReader>());
    private readonly int _currentProcessId = Environment.ProcessId;

    [Benchmark]
    public int CountDescendants() => _reader.CountDescendants(_currentProcessId);
}
