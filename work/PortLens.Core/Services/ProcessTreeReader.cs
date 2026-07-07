using Microsoft.Extensions.Logging;

namespace PortLens.Services;

public interface IProcessTreeReader
{
    int CountDescendants(int processId, CancellationToken cancellationToken = default);
    void Prune(IEnumerable<int> liveProcessIds);
    bool TryGetProcessName(int processId, out string? processName);
}

/// <summary>
/// Reads the process parent/child graph from a native system-information snapshot,
/// falling back to a PowerShell/CIM reader only when native access is unavailable.
/// </summary>
public sealed class ProcessTreeReader : IProcessTreeReader
{
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromSeconds(60);

    private readonly ILogger<ProcessTreeReader> _logger;
    private readonly PowerShellProcessTreeReader _fallback;
    private Snapshot? _snapshot;
    private readonly object _snapshotLock = new();
    private bool _nativeFailed;

    public ProcessTreeReader(ILogger<ProcessTreeReader> logger)
    {
        _logger = logger;
        _fallback = new PowerShellProcessTreeReader(logger);
    }

    public bool TryGetProcessName(int processId, out string? processName)
    {
        var snapshot = GetSnapshot(CancellationToken.None);
        if (snapshot?.ProcessNames is not null && snapshot.ProcessNames.TryGetValue(processId, out var name))
        {
            processName = name;
            return true;
        }

        processName = null;
        return false;
    }

    public int CountDescendants(int processId, CancellationToken cancellationToken = default)
    {
        var snapshot = GetSnapshot(cancellationToken);
        if (snapshot is null)
        {
            return 0;
        }

        var count = 0;
        var stack = new Stack<int>();
        stack.Push(processId);
        while (stack.Count > 0)
        {
            var parent = stack.Pop();
            if (!snapshot.ChildrenByParent.TryGetValue(parent, out var children))
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

    public IReadOnlyDictionary<int, int>? GetParentMap(CancellationToken cancellationToken = default)
    {
        var snapshot = GetSnapshot(cancellationToken);
        return snapshot?.ParentByChild;
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
            var prunedChildren = new Dictionary<int, IReadOnlyList<int>>();
            foreach (var pair in _snapshot.ChildrenByParent)
            {
                if (live.Contains(pair.Key))
                {
                    var filteredChildren = pair.Value.Where(child => live.Contains(child)).ToList();
                    if (filteredChildren.Count > 0)
                    {
                        prunedChildren[pair.Key] = filteredChildren;
                    }
                }
            }

            var prunedParents = _snapshot.ParentByChild
                .Where(pair => live.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            var prunedNames = _snapshot.ProcessNames
                .Where(pair => live.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            _snapshot = new Snapshot(prunedChildren, prunedParents, prunedNames, _snapshot.CachedAt);
        }
    }

    private Snapshot? GetSnapshot(CancellationToken cancellationToken)
    {
        if (_nativeFailed)
        {
            return GetFallbackSnapshot(cancellationToken);
        }

        lock (_snapshotLock)
        {
            if (_snapshot is { IsExpired: false })
            {
                return _snapshot;
            }
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var native = NativeProcessSnapshot.Capture();
            if (native is not null)
            {
                lock (_snapshotLock)
                {
                    _snapshot = new Snapshot(native.ChildrenByParent, native.ParentByChild, native.ProcessNames, DateTimeOffset.UtcNow);
                    return _snapshot;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Native process snapshot failed; switching to PowerShell fallback.");
            _nativeFailed = true;
        }

        return GetFallbackSnapshot(cancellationToken);
    }

    private Snapshot? GetFallbackSnapshot(CancellationToken cancellationToken)
    {
        var childrenByParent = Safe(() => _fallback.CaptureSnapshot(cancellationToken));
        if (childrenByParent is null)
        {
            return null;
        }

        lock (_snapshotLock)
        {
            _snapshot = new Snapshot(childrenByParent, new Dictionary<int, int>(), new Dictionary<int, string>(), DateTimeOffset.UtcNow);
            return _snapshot;
        }
    }

    private T? Safe<T>(Func<T?> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Process tree snapshot failed.");
            return default;
        }
    }

    private sealed class Snapshot
    {
        public Snapshot(
            IReadOnlyDictionary<int, IReadOnlyList<int>> childrenByParent,
            IReadOnlyDictionary<int, int> parentByChild,
            IReadOnlyDictionary<int, string> processNames,
            DateTimeOffset cachedAt)
        {
            ChildrenByParent = childrenByParent;
            ParentByChild = parentByChild;
            ProcessNames = processNames;
            CachedAt = cachedAt;
        }

        public IReadOnlyDictionary<int, IReadOnlyList<int>> ChildrenByParent { get; }
        public IReadOnlyDictionary<int, int> ParentByChild { get; }
        public IReadOnlyDictionary<int, string> ProcessNames { get; }
        public DateTimeOffset CachedAt { get; }
        public bool IsExpired => DateTimeOffset.UtcNow - CachedAt > SnapshotTtl;
    }
}
