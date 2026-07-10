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
            .Where(row => PortScannerFilters.IsLocalAddress(row.LocalAddress))
            .Where(row => !options.ExcludedPorts.Contains(row.LocalPort))
            .GroupBy(row => new { row.Protocol, row.LocalPort, row.ProcessId })
            .Select(PortScannerFilters.SelectPreferredListener)
            .OrderBy(row => row.LocalPort)
            .ThenBy(row => row.ProcessId)
            .ToList();

        var liveProcessIds = rows.Select(row => row.ProcessId).Distinct().ToArray();
        _inspector.PruneCaches(liveProcessIds);
        _inspector.PreloadProcessDetails(liveProcessIds, cancellationToken);
        var snapshot = _inspector.CaptureSnapshot(liveProcessIds);

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

            _inspector.EnrichBasic(entry, snapshot, options.FrameworkRules, cancellationToken);
            if (!options.ShowAll && !PortScannerFilters.IsEnabledDevelopmentService(entry, options.EnabledFrameworks))
            {
                continue;
            }

            _inspector.EnrichDetails(entry, snapshot, options.FrameworkRules, cancellationToken);
            entries.Add(entry);
        }

        return entries;
    }

    public void Kill(int processId) => _inspector.Kill(processId);

    public int CountChildProcesses(int processId) => _inspector.CountChildProcesses(processId);
}
