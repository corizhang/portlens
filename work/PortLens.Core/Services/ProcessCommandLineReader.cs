using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PortLens.Services;

public interface IProcessCommandLineReader
{
    string? Read(int processId, CancellationToken cancellationToken = default);
    IReadOnlyDictionary<int, string?> ReadMany(IReadOnlyCollection<int> processIds, CancellationToken cancellationToken = default);
    void Prune(IEnumerable<int> liveProcessIds);
}

public sealed class ProcessCommandLineReader : IProcessCommandLineReader
{
    private static readonly Regex CommandLineRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly ILogger<ProcessCommandLineReader> _logger;
    private readonly ConcurrentDictionary<int, CacheEntry> _cache = new();

    public ProcessCommandLineReader(ILogger<ProcessCommandLineReader> logger)
    {
        _logger = logger;
    }

    public string? Read(int processId, CancellationToken cancellationToken = default)
    {
        if (TryGetCached(processId, out var cached))
        {
            return cached;
        }

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

            using (cancellationToken.Register(() => Safe(() => process.Kill(entireProcessTree: true))))
            {
                var output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(350))
                {
                    Safe(() => process.Kill(entireProcessTree: true));
                    return null;
                }

                var trimmed = output.Trim();
                var commandLine = string.IsNullOrWhiteSpace(trimmed)
                    ? null
                    : CommandLineRegex.Replace(trimmed, " ");
                StoreCache(processId, commandLine);
                return commandLine;
            }
        });
    }

    public IReadOnlyDictionary<int, string?> ReadMany(IReadOnlyCollection<int> processIds, CancellationToken cancellationToken = default)
    {
        if (processIds.Count == 0)
        {
            return new Dictionary<int, string?>();
        }

        var result = new Dictionary<int, string?>();
        var missing = new List<int>();
        foreach (var processId in processIds)
        {
            if (TryGetCached(processId, out var cached))
            {
                result[processId] = cached;
            }
            else
            {
                missing.Add(processId);
            }
        }

        if (missing.Count == 0)
        {
            return result;
        }

        var fetched = Safe(() =>
        {
            var filter = BuildProcessIdFilter(missing);
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-CimInstance Win32_Process -Filter \\\"{filter}\\\" | Select-Object ProcessId,CommandLine | ConvertTo-Json -Compress\"",
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

            using (cancellationToken.Register(() => Safe(() => process.Kill(entireProcessTree: true))))
            {
                var output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(1200))
                {
                    Safe(() => process.Kill(entireProcessTree: true));
                    return new Dictionary<int, string?>();
                }

                cancellationToken.ThrowIfCancellationRequested();
                return ParseJsonProcessOutput(output, missing);
            }
        }) ?? new Dictionary<int, string?>();

        foreach (var pair in fetched)
        {
            result[pair.Key] = pair.Value;
            StoreCache(pair.Key, pair.Value);
        }

        return result;
    }

    public void Prune(IEnumerable<int> liveProcessIds)
    {
        var live = liveProcessIds.ToHashSet();
        foreach (var key in _cache.Keys.Where(key => !live.Contains(key)).ToList())
        {
            _cache.TryRemove(key, out _);
        }
    }

    private bool TryGetCached(int processId, out string? commandLine)
    {
        if (_cache.TryGetValue(processId, out var entry) && !entry.IsExpired)
        {
            commandLine = entry.CommandLine;
            return true;
        }

        commandLine = null;
        return false;
    }

    private void StoreCache(int processId, string? commandLine)
    {
        _cache[processId] = new CacheEntry(commandLine, DateTimeOffset.UtcNow);
    }

    private static string BuildProcessIdFilter(IEnumerable<int> processIds)
    {
        return string.Join(" OR ", processIds.Select(processId => $"ProcessId={processId}"));
    }

    private static IReadOnlyDictionary<int, string?> ParseJsonProcessOutput(string output, IReadOnlyCollection<int> processIds)
    {
        var wanted = processIds.ToHashSet();
        var result = new Dictionary<int, string?>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return result;
        }

        using var document = JsonDocument.Parse(output);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in document.RootElement.EnumerateArray())
            {
                AddProcess(element, wanted, result);
            }
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            AddProcess(document.RootElement, wanted, result);
        }

        return result;
    }

    private static void AddProcess(JsonElement element, HashSet<int> wanted, Dictionary<int, string?> result)
    {
        if (!element.TryGetProperty("ProcessId", out var pidElement) || !pidElement.TryGetInt32(out var processId) || !wanted.Contains(processId))
        {
            return;
        }

        var commandLine = element.TryGetProperty("CommandLine", out var commandLineElement) && commandLineElement.ValueKind == JsonValueKind.String
            ? CommandLineRegex.Replace(commandLineElement.GetString() ?? "", " ").Trim()
            : null;
        result[processId] = string.IsNullOrWhiteSpace(commandLine) ? null : commandLine;
    }

    private T? Safe<T>(Func<T?> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read process command line.");
            return default;
        }
    }

    private void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read process command line.");
        }
    }

    private sealed class CacheEntry
    {
        public CacheEntry(string? commandLine, DateTimeOffset cachedAt)
        {
            CommandLine = commandLine;
            CachedAt = cachedAt;
        }

        public string? CommandLine { get; }
        public DateTimeOffset CachedAt { get; }
        public bool IsExpired => DateTimeOffset.UtcNow - CachedAt > CacheTtl;
    }
}
