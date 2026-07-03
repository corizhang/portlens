using System.Collections.Concurrent;
using System.Diagnostics;

namespace PortLens.Services;

internal sealed class CpuSampler
{
    private sealed record ProcessSample(DateTimeOffset Timestamp, TimeSpan TotalProcessorTime);

    private readonly ConcurrentDictionary<int, ProcessSample> _lastSamples = new();

    public double? CalculateCpu(Process process)
    {
        var now = DateTimeOffset.UtcNow;
        var total = process.TotalProcessorTime;

        _lastSamples.TryGetValue(process.Id, out var last);
        _lastSamples[process.Id] = new ProcessSample(now, total);

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

    public void Prune(IEnumerable<int> liveProcessIds)
    {
        var live = liveProcessIds.ToHashSet();
        foreach (var key in _lastSamples.Keys.Where(key => !live.Contains(key)).ToList())
        {
            _lastSamples.TryRemove(key, out _);
        }
    }
}
