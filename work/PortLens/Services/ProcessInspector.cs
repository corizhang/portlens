using System.Diagnostics;
using PortLens.Models;

namespace PortLens.Services;

internal sealed class ProcessInspector
{
    private readonly Dictionary<int, ProcessSample> _lastSamples = new();
    private readonly Dictionary<int, CachedProcessInfo> _detailsCache = new();
    private readonly Lock _cacheLock = new();

    public void EnrichBasic(PortEntry entry)
    {
        try
        {
            using var process = Process.GetProcessById(entry.ProcessId);
            entry.ProcessName = process.ProcessName;
            entry.StartedAt = process.StartTime;
            entry.Uptime = DateTimeOffset.Now - process.StartTime;
            entry.MemoryBytes = process.WorkingSet64;
            entry.CpuPercent = CalculateCpu(process);
            entry.RiskLevel = entry.CpuPercent.GetValueOrDefault() > 25 || ToMb(entry.MemoryBytes) > 1024 ? "Medium" : "Low";
        }
        catch
        {
            entry.ProcessName = entry.ProcessName.Length > 0 ? entry.ProcessName : "Access denied";
        }
    }

    public void EnrichDetails(PortEntry entry)
    {
        EnrichBasic(entry);

        var cached = GetCachedDetails(entry.ProcessId);
        if (cached is null)
        {
            var executablePath = Safe(() =>
            {
                using var process = Process.GetProcessById(entry.ProcessId);
                return process.MainModule?.FileName;
            });
            var commandLine = ReadCommandLine(entry.ProcessId);
            var workingDirectory = InferWorkingDirectory(commandLine, executablePath);
            cached = new CachedProcessInfo(DateTimeOffset.UtcNow, executablePath, commandLine, workingDirectory);
            lock (_cacheLock)
            {
                _detailsCache[entry.ProcessId] = cached;
            }
        }

        entry.ExecutablePath = cached.ExecutablePath;
        entry.CommandLine = cached.CommandLine;
        entry.WorkingDirectory = cached.WorkingDirectory;
        entry.Framework = InferFramework(entry);
        entry.ProjectName = InferProjectName(entry);
    }

    public void Kill(int processId)
    {
        using var process = Process.GetProcessById(processId);
        process.Kill(entireProcessTree: true);
    }

    private double? CalculateCpu(Process process)
    {
        var now = DateTimeOffset.UtcNow;
        var total = process.TotalProcessorTime;
        ProcessSample? last;
        lock (_cacheLock)
        {
            _lastSamples.TryGetValue(process.Id, out last);
            _lastSamples[process.Id] = new ProcessSample(now, total);
        }

        if (last is null)
        {
            return null;
        }

        var elapsedMs = (now - last.Timestamp).TotalMilliseconds;
        if (elapsedMs <= 0)
        {
            return null;
        }

        var cpuMs = (total - last.TotalProcessorTime).TotalMilliseconds;
        return Math.Max(0, Math.Round(cpuMs / elapsedMs / Environment.ProcessorCount * 100, 1));
    }

    private static string? ReadCommandLine(int processId)
    {
        return Safe(() =>
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "wmic.exe",
                Arguments = $"process where ProcessId={processId} get CommandLine /value",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(250))
            {
                Safe<object>(() =>
                {
                    process.Kill(entireProcessTree: true);
                    return new object();
                });
                return null;
            }

            var line = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(value => value.StartsWith("CommandLine=", StringComparison.OrdinalIgnoreCase));
            return line?["CommandLine=".Length..].Trim();
        });
    }

    private CachedProcessInfo? GetCachedDetails(int processId)
    {
        lock (_cacheLock)
        {
            if (!_detailsCache.TryGetValue(processId, out var cached))
            {
                return null;
            }

            return DateTimeOffset.UtcNow - cached.CachedAt < TimeSpan.FromMinutes(2)
                ? cached
                : null;
        }
    }

    private static string? InferWorkingDirectory(string? commandLine, string? executablePath)
    {
        if (!string.IsNullOrWhiteSpace(commandLine))
        {
            var lowered = commandLine.ToLowerInvariant();
            var markers = new[] { "--cwd ", "-workingdirectory ", " --project " };
            foreach (var marker in markers)
            {
                var index = lowered.IndexOf(marker, StringComparison.Ordinal);
                if (index >= 0)
                {
                    var tail = commandLine[(index + marker.Length)..].Trim().Trim('"');
                    var candidate = tail.Split('"', ' ').FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return !string.IsNullOrWhiteSpace(executablePath) ? Path.GetDirectoryName(executablePath) : null;
    }

    private static string InferFramework(PortEntry entry)
    {
        var text = $"{entry.ProcessName} {entry.CommandLine} {entry.WorkingDirectory}".ToLowerInvariant();
        if (text.Contains("vite")) return "Vite";
        if (text.Contains("next")) return "Next.js";
        if (text.Contains("react-scripts")) return "React";
        if (text.Contains("vue-cli") || text.Contains("nuxt")) return "Vue/Nuxt";
        if (text.Contains("node") || text.Contains("npm") || text.Contains("pnpm") || text.Contains("yarn")) return "Node";
        if (text.Contains("python") || text.Contains("uvicorn") || text.Contains("fastapi")) return "Python";
        if (text.Contains("dotnet")) return ".NET";
        if (text.Contains("java")) return "Java";
        if (text.Contains("go.exe") || text.Contains("\\go\\")) return "Go";
        return "";
    }

    private static string? InferProjectName(PortEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.WorkingDirectory))
        {
            var name = Path.GetFileName(entry.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return entry.ProcessName;
    }

    private static long ToMb(long? bytes) => bytes.GetValueOrDefault() / 1024 / 1024;

    private static T? Safe<T>(Func<T?> action)
    {
        try
        {
            return action();
        }
        catch
        {
            return default;
        }
    }

    private sealed record ProcessSample(DateTimeOffset Timestamp, TimeSpan TotalProcessorTime);
    private sealed record CachedProcessInfo(DateTimeOffset CachedAt, string? ExecutablePath, string? CommandLine, string? WorkingDirectory);
}
