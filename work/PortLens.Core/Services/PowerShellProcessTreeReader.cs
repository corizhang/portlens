using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PortLens.Services;

/// <summary>
/// Fallback process-tree reader that uses PowerShell/CIM. Kept for restricted environments
/// where native system-information queries are unavailable.
/// </summary>
public sealed class PowerShellProcessTreeReader : IProcessTreeReader
{
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromSeconds(60);

    private readonly ILogger _logger;
    private Snapshot? _snapshot;
    private readonly object _snapshotLock = new();

    public PowerShellProcessTreeReader(ILogger logger)
    {
        _logger = logger;
    }

    public int CountDescendants(int processId, CancellationToken cancellationToken = default)
    {
        var childrenByParent = GetSnapshot(cancellationToken);
        if (childrenByParent is null)
        {
            return 0;
        }

        return CountDescendants(childrenByParent, processId);
    }

    internal IReadOnlyDictionary<int, IReadOnlyList<int>>? CaptureSnapshot(CancellationToken cancellationToken = default)
    {
        return GetSnapshot(cancellationToken);
    }

    private static int CountDescendants(IReadOnlyDictionary<int, IReadOnlyList<int>> childrenByParent, int processId)
    {
        var count = 0;
        var stack = new Stack<int>();
        stack.Push(processId);
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

    public void Prune(IEnumerable<int> liveProcessIds)
    {
        lock (_snapshotLock)
        {
            if (_snapshot is null)
            {
                return;
            }

            var live = liveProcessIds.ToHashSet();
            var pruned = new Dictionary<int, IReadOnlyList<int>>();
            foreach (var pair in _snapshot.ChildrenByParent)
            {
                if (live.Contains(pair.Key))
                {
                    var filteredChildren = pair.Value.Where(child => live.Contains(child)).ToList();
                    if (filteredChildren.Count > 0)
                    {
                        pruned[pair.Key] = filteredChildren;
                    }
                }
            }

            _snapshot = new Snapshot(pruned, _snapshot.CachedAt);
        }
    }

    private IReadOnlyDictionary<int, IReadOnlyList<int>>? GetSnapshot(CancellationToken cancellationToken)
    {
        lock (_snapshotLock)
        {
            if (_snapshot is { IsExpired: false })
            {
                return _snapshot.ChildrenByParent;
            }
        }

        var childrenByParent = FetchSnapshot(cancellationToken);
        if (childrenByParent is null)
        {
            return null;
        }

        lock (_snapshotLock)
        {
            _snapshot = new Snapshot(childrenByParent, DateTimeOffset.UtcNow);
            return _snapshot.ChildrenByParent;
        }
    }

    private IReadOnlyDictionary<int, IReadOnlyList<int>>? FetchSnapshot(CancellationToken cancellationToken)
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
                return null;
            }

            using (cancellationToken.Register(() => Safe(() => process.Kill(entireProcessTree: true))))
            {
                var output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(1200))
                {
                    Safe(() => process.Kill(entireProcessTree: true));
                    return null;
                }

                cancellationToken.ThrowIfCancellationRequested();
                return ParseSnapshot(output);
            }
        });
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<int>>? ParseSnapshot(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var childrenByParent = new Dictionary<int, List<int>>();
        using var document = JsonDocument.Parse(output);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in document.RootElement.EnumerateArray())
            {
                AddProcess(element, childrenByParent);
            }
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            AddProcess(document.RootElement, childrenByParent);
        }

        return childrenByParent.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)pair.Value);
    }

    private static void AddProcess(JsonElement element, Dictionary<int, List<int>> childrenByParent)
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

    private T? Safe<T>(Func<T?> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count process descendants via PowerShell fallback.");
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
            _logger.LogWarning(ex, "Failed to count process descendants via PowerShell fallback.");
        }
    }

    private sealed class Snapshot
    {
        public Snapshot(IReadOnlyDictionary<int, IReadOnlyList<int>> childrenByParent, DateTimeOffset cachedAt)
        {
            ChildrenByParent = childrenByParent;
            CachedAt = cachedAt;
        }

        public IReadOnlyDictionary<int, IReadOnlyList<int>> ChildrenByParent { get; }
        public DateTimeOffset CachedAt { get; }
        public bool IsExpired => DateTimeOffset.UtcNow - CachedAt > SnapshotTtl;
    }
}
