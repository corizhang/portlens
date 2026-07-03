using PortLens.Models;

namespace PortLens.Services;

public sealed class PortScanner
{
    private readonly ProcessInspector _inspector;

    public PortScanner(ProcessInspector inspector)
    {
        _inspector = inspector;
    }

    public IReadOnlyList<PortEntry> Scan(bool showAll, CancellationToken cancellationToken = default)
    {
        return Scan(new PortScanOptions { ShowAll = showAll }, cancellationToken);
    }

    public IReadOnlyList<PortEntry> Scan(PortScanOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = NativeTcp.GetTcpListeners()
            .Where(row => IsLocalAddress(row.LocalAddress))
            .Where(row => !options.ExcludedPorts.Contains(row.LocalPort))
            .GroupBy(row => new { row.Protocol, row.LocalPort, row.ProcessId })
            .Select(SelectPreferredListener)
            .OrderBy(row => row.LocalPort)
            .ThenBy(row => row.ProcessId)
            .ToList();

        var liveProcessIds = rows.Select(row => row.ProcessId).Distinct().ToArray();
        _inspector.PruneCaches(liveProcessIds);
        _inspector.PreloadProcessDetails(liveProcessIds, cancellationToken);

        var entries = new List<PortEntry>();
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = new PortEntry
            {
                Protocol = row.Protocol,
                LocalAddress = row.LocalAddress,
                LocalPort = row.LocalPort,
                State = row.State,
                ProcessId = row.ProcessId
            };

            _inspector.EnrichBasic(entry, cancellationToken);
            if (!options.ShowAll && !IsEnabledDevelopmentService(entry, options.EnabledFrameworks))
            {
                continue;
            }

            _inspector.EnrichDetails(entry, cancellationToken);
            entries.Add(entry);
        }

        return entries;
    }

    public void Kill(int processId) => _inspector.Kill(processId);

    public int CountChildProcesses(int processId) => _inspector.CountChildProcesses(processId);

    private static bool IsLocalAddress(string address)
    {
        return address is "0.0.0.0" or "127.0.0.1" or "::" or "::1"
            || address.StartsWith("127.", StringComparison.Ordinal)
            || address.StartsWith("[::]", StringComparison.Ordinal);
    }

    private static TcpRow SelectPreferredListener(IEnumerable<TcpRow> rows)
    {
        return rows
            .OrderBy(row => GetAddressPriority(row.LocalAddress))
            .ThenBy(row => row.LocalAddress, StringComparer.Ordinal)
            .First();
    }

    private static int GetAddressPriority(string address)
    {
        if (address is "127.0.0.1" or "::1" || address.StartsWith("127.", StringComparison.Ordinal))
        {
            return 0;
        }

        if (address is "0.0.0.0")
        {
            return 1;
        }

        if (address is "::" || address.StartsWith("[::]", StringComparison.Ordinal))
        {
            return 2;
        }

        return 3;
    }

    private static bool IsEnabledDevelopmentService(PortEntry entry, IReadOnlySet<string> enabledFrameworks)
    {
        if (!entry.IsRecognizedDevelopmentService || string.IsNullOrWhiteSpace(entry.Framework))
        {
            return false;
        }

        return enabledFrameworks.Contains(entry.Framework);
    }
}
