using PortLens.Models;

namespace PortLens.Services;

public static class PortScannerFilters
{
    public static bool IsLocalAddress(string address)
    {
        return address is "0.0.0.0" or "127.0.0.1" or "::" or "::1"
            || address.StartsWith("127.", StringComparison.Ordinal)
            || address.StartsWith("[::]", StringComparison.Ordinal);
    }

    public static TcpRow SelectPreferredListener(IEnumerable<TcpRow> rows)
    {
        return rows
            .OrderBy(row => GetAddressPriority(row.LocalAddress))
            .ThenBy(row => row.LocalAddress, StringComparer.Ordinal)
            .First();
    }

    public static int GetAddressPriority(string address)
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

    public static bool IsEnabledDevelopmentService(PortEntry entry, IReadOnlySet<string> enabledFrameworks)
    {
        if (!entry.IsRecognizedDevelopmentService || string.IsNullOrWhiteSpace(entry.Framework))
        {
            return false;
        }

        return enabledFrameworks.Contains(entry.Framework);
    }
}
