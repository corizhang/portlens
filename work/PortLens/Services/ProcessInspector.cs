using System.Diagnostics;
using System.Text.RegularExpressions;
using PortLens.Models;

namespace PortLens.Services;

internal sealed class ProcessInspector
{
    private readonly Dictionary<int, ProcessSample> _lastSamples = new();
    private readonly Dictionary<int, CachedProcessInfo> _detailsCache = new();
    private readonly Dictionary<int, CachedProcessInfo> _basicDetailsCache = new();
    private readonly Lock _cacheLock = new();

    public void PreloadProcessDetails(IEnumerable<int> processIds)
    {
        var missingIds = processIds
            .Distinct()
            .Where(processId => GetCachedDetails(processId, allowStale: true) is null && GetCachedBasicDetails(processId) is null)
            .ToArray();
        if (missingIds.Length == 0)
        {
            return;
        }

        var commandLines = ProcessCommandLineReader.ReadMany(missingIds);
        lock (_cacheLock)
        {
            foreach (var processId in missingIds)
            {
                commandLines.TryGetValue(processId, out var commandLine);
                _basicDetailsCache[processId] = new CachedProcessInfo(
                    DateTimeOffset.UtcNow,
                    null,
                    commandLine,
                    InferWorkingDirectory(commandLine, null));
            }
        }
    }

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

            var cached = GetCachedDetails(entry.ProcessId, allowStale: true);
            if (cached is not null)
            {
                ApplyDetails(entry, cached);
                return;
            }

            var basic = GetCachedBasicDetails(entry.ProcessId);
            if (basic is null)
            {
                basic = ReadProcessDetails(entry.ProcessId, readExecutablePath: false);
                lock (_cacheLock)
                {
                    _basicDetailsCache[entry.ProcessId] = basic;
                }
            }

            ApplyDetails(entry, basic);
        }
        catch
        {
            entry.ProcessName = entry.ProcessName.Length > 0 ? entry.ProcessName : "Access denied";
        }
    }

    public void EnrichDetails(PortEntry entry)
    {
        EnrichBasic(entry);

        var cached = GetCachedDetails(entry.ProcessId, allowStale: false);
        if (cached is null)
        {
            cached = ReadProcessDetails(entry.ProcessId, readExecutablePath: true);
            lock (_cacheLock)
            {
                _detailsCache[entry.ProcessId] = cached;
                _basicDetailsCache[entry.ProcessId] = cached;
            }
        }

        ApplyDetails(entry, cached);
    }

    private static void ApplyDetails(PortEntry entry, CachedProcessInfo cached)
    {
        entry.ExecutablePath = cached.ExecutablePath;
        entry.CommandLine = cached.CommandLine;
        entry.WorkingDirectory = cached.WorkingDirectory;
        entry.Framework = InferFramework(entry);
        entry.ProjectName = InferProjectName(entry);
        entry.IsRecognizedDevelopmentService = !string.IsNullOrWhiteSpace(entry.Framework);
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

    private static CachedProcessInfo ReadProcessDetails(int processId, bool readExecutablePath)
    {
        var commandLine = ProcessCommandLineReader.Read(processId);
        var executablePath = Safe(() =>
        {
            using var process = Process.GetProcessById(processId);
            return readExecutablePath ? process.MainModule?.FileName : null;
        });
        var workingDirectory = InferWorkingDirectory(commandLine, executablePath);
        return new CachedProcessInfo(DateTimeOffset.UtcNow, executablePath, commandLine, workingDirectory);
    }

    private CachedProcessInfo? GetCachedDetails(int processId, bool allowStale)
    {
        lock (_cacheLock)
        {
            if (!_detailsCache.TryGetValue(processId, out var cached))
            {
                return null;
            }

            return allowStale || DateTimeOffset.UtcNow - cached.CachedAt < TimeSpan.FromMinutes(2)
                ? cached
                : null;
        }
    }

    private CachedProcessInfo? GetCachedBasicDetails(int processId)
    {
        lock (_cacheLock)
        {
            if (!_basicDetailsCache.TryGetValue(processId, out var cached))
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
        var text = $"{entry.ProcessName} {entry.CommandLine} {entry.WorkingDirectory} {entry.ExecutablePath}".ToLowerInvariant();
        if (ContainsAny(text, "vite", "vite.js", "vite\\bin", "vite/bin")) return "Vite";
        if (ContainsAny(text, "next dev", "next start", "next-server", "\\next\\", "/next/")) return "Next.js";
        if (ContainsAny(text, "nuxt", "nuxi")) return "Nuxt";
        if (ContainsAny(text, "manage.py runserver", "django.core", "daphne", "runserver")) return "Django";
        if (ContainsAny(text, "fastapi", "uvicorn", "hypercorn")) return "FastAPI";
        if (ContainsAny(text, "spring-boot", "springframework", "org.springframework.boot")) return "Spring";
        if (ContainsAny(text, "dotnet", "kestrel", "aspnetcore")) return ".NET";
        if (ContainsAny(text, "docker-proxy", "com.docker", "docker desktop", "dockerd")) return "Docker";
        if (ContainsAny(text, "wslhost", "wslservice", "wsl.exe", "\\wsl$")) return "WSL";
        return "";
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.Ordinal));
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

    private static class ProcessCommandLineReader
    {
        private static readonly Regex CommandLineRegex = new(@"\s+", RegexOptions.Compiled);

        public static string? Read(int processId)
        {
            return Safe(() =>
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"(Get-CimInstance Win32_Process -Filter 'ProcessId={processId}').CommandLine\"",
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
                if (!process.WaitForExit(350))
                {
                    Safe<object>(() =>
                    {
                        process.Kill(entireProcessTree: true);
                        return new object();
                    });
                    return null;
                }

                var trimmed = output.Trim();
                return string.IsNullOrWhiteSpace(trimmed)
                    ? null
                    : CommandLineRegex.Replace(trimmed, " ");
            });
        }

        public static IReadOnlyDictionary<int, string?> ReadMany(IReadOnlyCollection<int> processIds)
        {
            return Safe<IReadOnlyDictionary<int, string?>>(() =>
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-CimInstance Win32_Process | Select-Object ProcessId,CommandLine | ConvertTo-Json -Compress\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    return new Dictionary<int, string?>();
                }

                var output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(1200))
                {
                    Safe<object>(() =>
                    {
                        process.Kill(entireProcessTree: true);
                        return new object();
                    });
                    return new Dictionary<int, string?>();
                }

                return ParseJsonProcessOutput(output, processIds);
            }) ?? new Dictionary<int, string?>();
        }

        private static IReadOnlyDictionary<int, string?> ParseJsonProcessOutput(string output, IReadOnlyCollection<int> processIds)
        {
            var wanted = processIds.ToHashSet();
            var result = new Dictionary<int, string?>();
            if (string.IsNullOrWhiteSpace(output))
            {
                return result;
            }

            using var document = System.Text.Json.JsonDocument.Parse(output);
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    AddProcess(element, wanted, result);
                }
            }
            else if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                AddProcess(document.RootElement, wanted, result);
            }

            return result;
        }

        private static void AddProcess(System.Text.Json.JsonElement element, HashSet<int> wanted, Dictionary<int, string?> result)
        {
            if (!element.TryGetProperty("ProcessId", out var pidElement) || !pidElement.TryGetInt32(out var processId) || !wanted.Contains(processId))
            {
                return;
            }

            var commandLine = element.TryGetProperty("CommandLine", out var commandLineElement) && commandLineElement.ValueKind == System.Text.Json.JsonValueKind.String
                ? CommandLineRegex.Replace(commandLineElement.GetString() ?? "", " ").Trim()
                : null;
            result[processId] = string.IsNullOrWhiteSpace(commandLine) ? null : commandLine;
        }
    }
}
