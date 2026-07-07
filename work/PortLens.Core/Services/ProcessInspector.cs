using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PortLens.Models;

namespace PortLens.Services;

public sealed class ProcessInspector
{
    private readonly CpuSampler _cpuSampler = new();
    private readonly ConcurrentDictionary<int, CachedProcessInfo> _detailsCache = new();
    private readonly ConcurrentDictionary<int, CachedProcessInfo> _basicDetailsCache = new();
    private readonly IProcessCommandLineReader _commandLineReader;
    private readonly ProcessCurrentDirectoryReader _currentDirectoryReader;
    private readonly IProcessTreeReader _processTreeReader;
    private readonly ILogger<ProcessInspector> _logger;

    public ProcessInspector(
        IProcessCommandLineReader commandLineReader,
        ProcessCurrentDirectoryReader currentDirectoryReader,
        IProcessTreeReader processTreeReader,
        ILogger<ProcessInspector> logger)
    {
        _commandLineReader = commandLineReader;
        _currentDirectoryReader = currentDirectoryReader;
        _processTreeReader = processTreeReader;
        _logger = logger;
    }

    public void PruneCaches(IEnumerable<int> liveProcessIds)
    {
        var live = liveProcessIds.ToHashSet();
        PruneCache(_detailsCache, live);
        PruneCache(_basicDetailsCache, live);
        _cpuSampler.Prune(live);
        _commandLineReader.Prune(live);
        _processTreeReader.Prune(live);
    }

    public IReadOnlyDictionary<int, ProcessSnapshot> CaptureSnapshot(IEnumerable<int> processIds)
    {
        var wanted = processIds.ToHashSet();
        var snapshot = new Dictionary<int, ProcessSnapshot>();
        foreach (var processId in wanted)
        {
            ProcessSnapshot? snap = null;
            try
            {
                using var process = Process.GetProcessById(processId);
                snap = new ProcessSnapshot(
                    process.Id,
                    process.ProcessName,
                    process.StartTime,
                    process.WorkingSet64,
                    process.TotalProcessorTime,
                    process.MainModule?.FileName);
            }
            catch
            {
                // Ignore processes we cannot inspect.
            }

            if (snap is null && _processTreeReader.TryGetProcessName(processId, out var name) && !string.IsNullOrWhiteSpace(name))
            {
                snap = new ProcessSnapshot(processId, name!, default, 0, TimeSpan.Zero, null);
            }

            if (snap.HasValue)
            {
                snapshot[snap.Value.Id] = snap.Value;
            }
        }

        return snapshot;
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

        var commandLines = _commandLineReader.ReadMany(missingIds, cancellationToken);
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

    public void EnrichBasic(PortEntry entry, IReadOnlyDictionary<int, ProcessSnapshot> snapshot, CancellationToken cancellationToken = default)
    {
        try
        {
            if (snapshot.TryGetValue(entry.ProcessId, out var process))
            {
                entry.ProcessName = process.ProcessName;
                entry.StartedAt = process.StartTime;
                entry.Uptime = DateTimeOffset.Now - process.StartTime;
                entry.MemoryBytes = process.WorkingSet64;
                entry.CpuPercent = _cpuSampler.CalculateCpu(entry.ProcessId, process.TotalProcessorTime);
                entry.RiskLevel = entry.CpuPercent.GetValueOrDefault() > 25 || ToMb(entry.MemoryBytes) > 1024 ? "Medium" : "Low";
            }
            else
            {
                entry.ProcessName = entry.ProcessName.Length > 0 ? entry.ProcessName : "Access denied";
            }

            var cached = GetCachedDetails(entry.ProcessId, allowStale: true);
            if (cached is not null)
            {
                ApplyDetails(entry, cached);
                return;
            }

            var basic = GetCachedBasicDetails(entry.ProcessId);
            if (basic is null)
            {
                basic = ReadProcessDetails(entry.ProcessId, snapshot, readExecutablePath: false, cancellationToken: cancellationToken);
                _basicDetailsCache[entry.ProcessId] = basic;
            }

            ApplyDetails(entry, basic);
        }
        catch
        {
            entry.ProcessName = entry.ProcessName.Length > 0 ? entry.ProcessName : "Access denied";
        }
    }

    public void EnrichDetails(PortEntry entry, IReadOnlyDictionary<int, ProcessSnapshot> snapshot, CancellationToken cancellationToken = default)
    {
        EnrichBasic(entry, snapshot, cancellationToken);

        var cached = GetCachedDetails(entry.ProcessId, allowStale: false);
        if (cached is null)
        {
            var basic = GetCachedBasicDetails(entry.ProcessId);
            cached = basic is null
                ? ReadProcessDetails(entry.ProcessId, snapshot, readExecutablePath: true, cancellationToken: cancellationToken)
                : ReadProcessDetails(entry.ProcessId, snapshot, readExecutablePath: true, basic.CommandLine, cancellationToken);
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

    public int CountChildProcesses(int processId)
    {
        return _processTreeReader.CountDescendants(processId);
    }

    public IReadOnlyDictionary<int, int>? GetProcessParentMap(CancellationToken cancellationToken = default)
    {
        return (_processTreeReader as ProcessTreeReader)?.GetParentMap(cancellationToken);
    }

    private static void ApplyDetails(PortEntry entry, CachedProcessInfo cached)
    {
        entry.ExecutablePath = cached.ExecutablePath;
        entry.CommandLine = cached.CommandLine;
        entry.WorkingDirectory = cached.WorkingDirectory;
        entry.Framework = FrameworkDetector.InferFramework(entry);
        entry.ProjectName = ProjectNameResolver.ResolveProjectName(entry.WorkingDirectory, entry.ProcessName);
        entry.IsRecognizedDevelopmentService = !string.IsNullOrWhiteSpace(entry.Framework);
    }

    private CachedProcessInfo ReadProcessDetails(
        int processId,
        IReadOnlyDictionary<int, ProcessSnapshot> snapshot,
        bool readExecutablePath,
        string? cachedCommandLine = null,
        CancellationToken cancellationToken = default)
    {
        var commandLine = cachedCommandLine ?? _commandLineReader.Read(processId, cancellationToken);
        var executablePath = readExecutablePath && snapshot.TryGetValue(processId, out var process)
            ? process.ExecutablePath
            : null;
        var parentMap = GetProcessParentMap(cancellationToken);
        var currentDirectory = _currentDirectoryReader.Read(processId, parentMap);
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

    private T? Safe<T>(Func<T?> action)
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
