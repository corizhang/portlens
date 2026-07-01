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
            .ThenBy(row => row.ProcessId);

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
            if (!showAll && !LooksLikeDevelopmentService(entry))
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

    private static bool LooksLikeDevelopmentService(PortEntry entry)
    {
        if (entry.LocalPort <= 1024)
        {
            return false;
        }

        if (IsKnownDevelopmentPort(entry.LocalPort))
        {
            return true;
        }

        var name = entry.ProcessName.ToLowerInvariant();
        if (name is "system" or "svchost" or "spoolsv" or "lsass" or "services" or "wininit" or "winlogon")
        {
            return false;
        }

        var devProcessNames = new[]
        {
            "node", "npm", "pnpm", "yarn", "bun", "deno", "python", "pythonw",
            "dotnet", "java", "go", "air", "cargo", "ruby", "rails", "php",
            "uvicorn", "gunicorn", "ollama", "code", "cursor", "webstorm"
        };

        if (devProcessNames.Any(dev => name.Equals(dev, StringComparison.Ordinal) || name.Contains(dev, StringComparison.Ordinal)))
        {
            return true;
        }

        return entry.LocalPort >= 3000 && entry.LocalPort <= 19999;
    }

    private static bool IsKnownDevelopmentPort(int port)
    {
        return port is
            1313 or 1420 or 1900 or 24678 or
            3000 or 3001 or 3002 or 3003 or 3004 or 3005 or
            4200 or 4321 or 5000 or 5001 or 5173 or 5174 or
            6006 or 7000 or 7071 or 7860 or 8000 or 8001 or
            8080 or 8081 or 8888 or 9000 or 9001 or 9229 or
            10000 or 11434;
    }
}
