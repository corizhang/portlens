using PortLens.Models;
using PortLens.Services;
using Xunit;

namespace PortLens.Core.Tests;

public class PortScannerFiltersTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.0.0.2", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("::1", true)]
    [InlineData("::", true)]
    [InlineData("[::]", true)]
    [InlineData("192.168.1.1", false)]
    [InlineData("10.0.0.1", false)]
    public void IsLocalAddress_MatchesExpected(string address, bool expected)
    {
        Assert.Equal(expected, PortScannerFilters.IsLocalAddress(address));
    }

    [Theory]
    [InlineData("127.0.0.1", 0)]
    [InlineData("127.0.0.2", 0)]
    [InlineData("0.0.0.0", 1)]
    [InlineData("::", 2)]
    [InlineData("[::]", 2)]
    [InlineData("192.168.1.1", 3)]
    public void GetAddressPriority_ReturnsExpectedRank(string address, int expected)
    {
        Assert.Equal(expected, PortScannerFilters.GetAddressPriority(address));
    }

    [Fact]
    public void SelectPreferredListener_PrefersLoopbackOverAny()
    {
        var rows = new[]
        {
            new TcpRow("TCP", "0.0.0.0", 8080, "LISTEN", 1),
            new TcpRow("TCP", "127.0.0.1", 8080, "LISTEN", 1)
        };

        var selected = PortScannerFilters.SelectPreferredListener(rows);

        Assert.Equal("127.0.0.1", selected.LocalAddress);
    }

    [Fact]
    public void SelectPreferredListener_PrefersLowerPriorityNumber()
    {
        var rows = new[]
        {
            new TcpRow("TCP", "::", 3000, "LISTEN", 2),
            new TcpRow("TCP", "0.0.0.0", 3000, "LISTEN", 2),
            new TcpRow("TCP", "127.0.0.1", 3000, "LISTEN", 2)
        };

        var selected = PortScannerFilters.SelectPreferredListener(rows);

        Assert.Equal("127.0.0.1", selected.LocalAddress);
    }

    [Fact]
    public void IsEnabledDevelopmentService_ReturnsTrue_WhenFrameworkEnabled()
    {
        var entry = new PortEntry { Framework = "Vite", IsRecognizedDevelopmentService = true };
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Vite" };

        Assert.True(PortScannerFilters.IsEnabledDevelopmentService(entry, enabled));
    }

    [Fact]
    public void IsEnabledDevelopmentService_ReturnsFalse_WhenFrameworkDisabled()
    {
        var entry = new PortEntry { Framework = "Docker", IsRecognizedDevelopmentService = true };
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Vite" };

        Assert.False(PortScannerFilters.IsEnabledDevelopmentService(entry, enabled));
    }

    [Fact]
    public void IsEnabledDevelopmentService_ReturnsFalse_WhenNotRecognized()
    {
        var entry = new PortEntry { Framework = "", IsRecognizedDevelopmentService = false };
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Vite" };

        Assert.False(PortScannerFilters.IsEnabledDevelopmentService(entry, enabled));
    }
}
