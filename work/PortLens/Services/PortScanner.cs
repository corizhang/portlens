using PortLens.Models;

namespace PortLens.Services;

internal sealed class PortScanner
{
    private readonly ProcessInspector _inspector = new();

    public IReadOnlyList<PortEntry> Scan(bool showAll)
    {
        var rows = NativeTcp.GetTcpListeners()
            .Where(row => IsLocalAddress(row.LocalAddress))
            .GroupBy(row => new { row.Protocol, row.LocalAddress, row.LocalPort, row.ProcessId })
            .Select(group => group.First())
            .OrderBy(row => row.LocalPort)
            .ThenBy(row => row.ProcessId)
            .ToList();

        _inspector.PreloadProcessDetails(rows.Select(row => row.ProcessId));

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
            if (!showAll && !entry.IsRecognizedDevelopmentService)
            {
                continue;
            }

            _inspector.EnrichDetails(entry);
            entries.Add(entry);
        }

        return entries;
    }

    public void Kill(int processId) => _inspector.Kill(processId);

    private static bool IsLocalAddress(string address)
    {
        return address is "0.0.0.0" or "127.0.0.1" or "::" or "::1"
            || address.StartsWith("127.", StringComparison.Ordinal)
            || address.StartsWith("[::]", StringComparison.Ordinal);
    }

}
