using System.Collections.Concurrent;
using System.Diagnostics;
using PortLens.Models;

namespace PortLens.Services;

internal sealed class ProcessInspector
{
    private readonly CpuSampler _cpuSampler = new();
    private readonly ConcurrentDictionary<int, CachedProcessInfo> _detailsCache = new();
    private readonly ConcurrentDictionary<int, CachedProcessInfo> _basicDetailsCache = new();

    public void PruneCaches(IEnumerable<int> liveProcessIds)
    {
        var live = liveProcessIds.ToHashSet();
        PruneCache(_detailsCache, live);
        PruneCache(_basicDetailsCache, live);
        _cpuSampler.Prune(live);
    }

    public void PreloadProcessDetails(IEnumerable<int> processIds, CancellationToken cancellationToken = default)
    {
        var missingIds = processIds
            .Distinct()
            .Where(processId => GetCachedDetails(processId, allowStale: true) is null && GetCachedBasicDetails(processId) is null)
            .ToArray();
        if (missingIds.Length == 0)
        {
            return;
        }

        var commandLines = ProcessCommandLineReader.ReadMany(missingIds, cancellationToken);
        foreach (var processId in missingIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            commandLines.TryGetValue(processId, out var commandLine);
            _basicDetailsCache[processId] = new CachedProcessInfo(
                DateTimeOffset.UtcNow,
                null,
                commandLine,
                ProjectNameResolver.InferWorkingDirectory(commandLine, null, null));
        }
    }

    public void EnrichBasic(PortEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = Process.GetProcessById(entry.ProcessId);
            entry.ProcessName = process.ProcessName;
            entry.StartedAt = process.StartTime;
            entry.Uptime = DateTimeOffset.Now - process.StartTime;
            entry.MemoryBytes = process.WorkingSet64;
            entry.CpuPercent = _cpuSampler.CalculateCpu(process);
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
                basic = ReadProcessDetails(entry.ProcessId, readExecutablePath: false, cancellationToken: cancellationToken);
                _basicDetailsCache[entry.ProcessId] = basic;
            }

            ApplyDetails(entry, basic);
        }
        catch
        {
            entry.ProcessName = entry.ProcessName.Length > 0 ? entry.ProcessName : "Access denied";
        }
    }

    public void EnrichDetails(PortEntry entry, CancellationToken cancellationToken = default)
    {
        EnrichBasic(entry, cancellationToken);

        var cached = GetCachedDetails(entry.ProcessId, allowStale: false);
        if (cached is null)
        {
            var basic = GetCachedBasicDetails(entry.ProcessId);
            cached = basic is null
                ? ReadProcessDetails(entry.ProcessId, readExecutablePath: true, cancellationToken: cancellationToken)
                : ReadProcessDetails(entry.ProcessId, readExecutablePath: true, basic.CommandLine, cancellationToken);
            _detailsCache[entry.ProcessId] = cached;
            _basicDetailsCache[entry.ProcessId] = cached;
        }

        ApplyDetails(entry, cached);
    }

    public void Kill(int processId)
    {
        using var process = Process.GetProcessById(processId);
        process.Kill(entireProcessTree: true);
    }

    public int CountChildProcesses(int processId) => ProcessTreeReader.CountDescendants(processId);

    private static void ApplyDetails(PortEntry entry, CachedProcessInfo cached)
    {
        entry.ExecutablePath = cached.ExecutablePath;
        entry.CommandLine = cached.CommandLine;
        entry.WorkingDirectory = cached.WorkingDirectory;
        entry.Framework = FrameworkDetector.InferFramework(entry);
        entry.ProjectName = ProjectNameResolver.ResolveProjectName(entry.WorkingDirectory, entry.ProcessName);
        entry.IsRecognizedDevelopmentService = !string.IsNullOrWhiteSpace(entry.Framework);
    }

    private static CachedProcessInfo ReadProcessDetails(int processId, bool readExecutablePath, string? cachedCommandLine = null, CancellationToken cancellationToken = default)
    {
        var commandLine = cachedCommandLine ?? ProcessCommandLineReader.Read(processId, cancellationToken);
        var executablePath = Safe(() =>
        {
            using var process = Process.GetProcessById(processId);
            return readExecutablePath ? process.MainModule?.FileName : null;
        });
        var currentDirectory = ProcessCurrentDirectoryReader.Read(processId);
        var workingDirectory = ProjectNameResolver.InferWorkingDirectory(commandLine, executablePath, currentDirectory);
        return new CachedProcessInfo(DateTimeOffset.UtcNow, executablePath, commandLine, workingDirectory);
    }

    private CachedProcessInfo? GetCachedDetails(int processId, bool allowStale)
    {
        if (!_detailsCache.TryGetValue(processId, out var cached))
        {
            return null;
        }

        return allowStale || DateTimeOffset.UtcNow - cached.CachedAt < TimeSpan.FromMinutes(2)
            ? cached
            : null;
    }

    private CachedProcessInfo? GetCachedBasicDetails(int processId)
    {
        if (!_basicDetailsCache.TryGetValue(processId, out var cached))
        {
            return null;
        }

        return DateTimeOffset.UtcNow - cached.CachedAt < TimeSpan.FromMinutes(2)
            ? cached
            : null;
    }

    private static long ToMb(long? bytes) => bytes.GetValueOrDefault() / 1024 / 1024;

    private static void PruneCache(ConcurrentDictionary<int, CachedProcessInfo> cache, HashSet<int> liveProcessIds)
    {
        foreach (var key in cache.Keys.Where(key => !liveProcessIds.Contains(key)).ToList())
        {
            cache.TryRemove(key, out _);
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

    private sealed record CachedProcessInfo(DateTimeOffset CachedAt, string? ExecutablePath, string? CommandLine, string? WorkingDirectory);
}
