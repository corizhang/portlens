using PortLens.Models;

namespace PortLens.Services;

public sealed class PortScanner
{
    private readonly ProcessInspector _inspector = new();

    public IReadOnlyList<PortEntry> Scan(bool showAll)
    {
        return Scan(new PortScanOptions { ShowAll = showAll });
    }

    public IReadOnlyList<PortEntry> Scan(PortScanOptions options)
    {
        var rows = NativeTcp.GetTcpListeners()
            .Where(row => IsLocalAddress(row.LocalAddress))
            .Where(row => !options.ExcludedPorts.Contains(row.LocalPort))
            .GroupBy(row => new { row.Protocol, row.LocalAddress, row.LocalPort, row.ProcessId })
            .Select(group => group.First())
            .OrderBy(row => row.LocalPort)
            .ThenBy(row => row.ProcessId)
            .ToList();

        var liveProcessIds = rows.Select(row => row.ProcessId).Distinct().ToArray();
        _inspector.PruneCaches(liveProcessIds);
        _inspector.PreloadProcessDetails(liveProcessIds);

        var entries = new List<PortEntry>();
        foreach (var row in rows)
        {
            var entry = new PortEntry
            {
                Protocol = row.Protocol,
                LocalAddress = row.LocalAddress,
                LocalPort = row.LocalPort,
                State = row.State,
                ProcessId = row.ProcessId
            };

            _inspector.EnrichBasic(entry);
            if (!options.ShowAll && !IsEnabledDevelopmentService(entry, options.EnabledFrameworks))
            {
                continue;
            }

            _inspector.EnrichDetails(entry);
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

    private static bool IsEnabledDevelopmentService(PortEntry entry, IReadOnlySet<string> enabledFrameworks)
    {
        if (!entry.IsRecognizedDevelopmentService || string.IsNullOrWhiteSpace(entry.Framework))
        {
            return false;
        }

        return enabledFrameworks.Contains(entry.Framework);
    }
}
