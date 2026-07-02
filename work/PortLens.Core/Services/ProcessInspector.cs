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

    public void PruneCaches(IEnumerable<int> liveProcessIds)
    {
        var live = liveProcessIds.ToHashSet();
        lock (_cacheLock)
        {
            RemoveDeadKeys(_lastSamples, live);
            RemoveDeadKeys(_detailsCache, live);
            RemoveDeadKeys(_basicDetailsCache, live);
        }
    }

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
            var basic = GetCachedBasicDetails(entry.ProcessId);
            cached = basic is null
                ? ReadProcessDetails(entry.ProcessId, readExecutablePath: true)
                : ReadProcessDetails(entry.ProcessId, readExecutablePath: true, basic.CommandLine);
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

    public int CountChildProcesses(int processId)
    {
        return ProcessTreeReader.CountDescendants(processId);
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

    private static CachedProcessInfo ReadProcessDetails(int processId, bool readExecutablePath, string? cachedCommandLine = null)
    {
        var commandLine = cachedCommandLine ?? ProcessCommandLineReader.Read(processId);
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
        var projectDirectory = InferProjectDirectory(commandLine);
        if (!string.IsNullOrWhiteSpace(projectDirectory))
        {
            return projectDirectory;
        }

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

    private static string? InferProjectDirectory(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        foreach (var path in ExtractWindowsPaths(commandLine))
        {
            var normalized = path.Trim().Trim('"');
            var lowered = normalized.ToLowerInvariant();

            var nodeModulesIndex = lowered.IndexOf(@"\node_modules\", StringComparison.Ordinal);
            if (nodeModulesIndex > 0)
            {
                return ExistingDirectoryOrNull(normalized[..nodeModulesIndex]);
            }

            if (lowered.EndsWith(@"\manage.py", StringComparison.Ordinal))
            {
                return ExistingDirectoryOrNull(Path.GetDirectoryName(normalized));
            }

            if (lowered.EndsWith(".csproj", StringComparison.Ordinal))
            {
                return ExistingDirectoryOrNull(Path.GetDirectoryName(normalized));
            }

            if (lowered.EndsWith(".dll", StringComparison.Ordinal))
            {
                var binIndex = lowered.LastIndexOf(@"\bin\", StringComparison.Ordinal);
                if (binIndex > 0)
                {
                    return ExistingDirectoryOrNull(normalized[..binIndex]);
                }

                return ExistingDirectoryOrNull(Path.GetDirectoryName(normalized));
            }

            if (lowered.EndsWith(".jar", StringComparison.Ordinal))
            {
                var buildIndex = lowered.LastIndexOf(@"\build\", StringComparison.Ordinal);
                if (buildIndex > 0)
                {
                    return ExistingDirectoryOrNull(normalized[..buildIndex]);
                }

                var targetIndex = lowered.LastIndexOf(@"\target\", StringComparison.Ordinal);
                if (targetIndex > 0)
                {
                    return ExistingDirectoryOrNull(normalized[..targetIndex]);
                }

                return ExistingDirectoryOrNull(Path.GetDirectoryName(normalized));
            }
        }

        return null;
    }

    private static IEnumerable<string> ExtractWindowsPaths(string text)
    {
        foreach (Match match in QuotedWindowsPathRegex.Matches(text))
        {
            yield return match.Groups[1].Value.TrimEnd(',', ';');
        }

        foreach (Match match in UnquotedWindowsPathRegex.Matches(text))
        {
            yield return match.Value.TrimEnd(',', ';');
        }
    }

    private static string? ExistingDirectoryOrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Directory.Exists(path) ? path : null;
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
        if (ContainsAny(text, "go run", "air.toml", "\\air.exe", "/air", "gin-gonic", "fiber", "echo/v4", "go-build", "\\go.exe", "/go ")) return "Go";
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

    private static void RemoveDeadKeys<TValue>(Dictionary<int, TValue> cache, HashSet<int> liveProcessIds)
    {
        foreach (var processId in cache.Keys.Where(processId => !liveProcessIds.Contains(processId)).ToList())
        {
            cache.Remove(processId);
        }
    }

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
    private static readonly Regex QuotedWindowsPathRegex = new(@"""([A-Za-z]:\\[^""]+)""", RegexOptions.Compiled);
    private static readonly Regex UnquotedWindowsPathRegex = new(@"[A-Za-z]:\\[^\s""']+", RegexOptions.Compiled);

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

    private static class ProcessTreeReader
    {
        public static int CountDescendants(int processId)
        {
            return Safe(() =>
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-CimInstance Win32_Process | Select-Object ProcessId,ParentProcessId | ConvertTo-Json -Compress\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    return 0;
                }

                var output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(1200))
                {
                    Safe<object>(() =>
                    {
                        process.Kill(entireProcessTree: true);
                        return new object();
                    });
                    return 0;
                }

                return CountDescendantsFromJson(output, processId);
            });
        }

        private static int CountDescendantsFromJson(string output, int rootProcessId)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return 0;
            }

            var childrenByParent = new Dictionary<int, List<int>>();
            using var document = System.Text.Json.JsonDocument.Parse(output);
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    AddProcess(element, childrenByParent);
                }
            }
            else if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                AddProcess(document.RootElement, childrenByParent);
            }

            var count = 0;
            var stack = new Stack<int>();
            stack.Push(rootProcessId);
            while (stack.Count > 0)
            {
                var parent = stack.Pop();
                if (!childrenByParent.TryGetValue(parent, out var children))
                {
                    continue;
                }

                foreach (var child in children)
                {
                    count++;
                    stack.Push(child);
                }
            }

            return count;
        }

        private static void AddProcess(System.Text.Json.JsonElement element, Dictionary<int, List<int>> childrenByParent)
        {
            if (!element.TryGetProperty("ProcessId", out var pidElement) ||
                !pidElement.TryGetInt32(out var processId) ||
                !element.TryGetProperty("ParentProcessId", out var parentElement) ||
                !parentElement.TryGetInt32(out var parentProcessId))
            {
                return;
            }

            if (!childrenByParent.TryGetValue(parentProcessId, out var children))
            {
                children = new List<int>();
                childrenByParent[parentProcessId] = children;
            }

            children.Add(processId);
        }
    }
}
