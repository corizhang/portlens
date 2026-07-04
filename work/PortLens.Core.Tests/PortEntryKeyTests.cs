using PortLens.Models;
using Xunit;

namespace PortLens.Core.Tests;

public sealed class PortEntryKeyTests
{
    [Fact]
    public void Equals_SameValues_AreEqual()
    {
        var a = new PortEntryKey("TCP", "127.0.0.1", 5000, 1234);
        var b = new PortEntryKey("TCP", "127.0.0.1", 5000, 1234);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Equals_DifferentValues_AreNotEqual()
    {
        var a = new PortEntryKey("TCP", "127.0.0.1", 5000, 1234);
        var b = new PortEntryKey("TCP", "127.0.0.1", 5000, 5678);

        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void GetHashCode_SameValues_AreSame()
    {
        var a = new PortEntryKey("TCP", "127.0.0.1", 5000, 1234);
        var b = new PortEntryKey("TCP", "127.0.0.1", 5000, 1234);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void HashSet_Contains_DeduplicatesByValue()
    {
        var set = new HashSet<PortEntryKey>
        {
            new("TCP", "127.0.0.1", 5000, 1234),
            new("TCP", "127.0.0.1", 5000, 1234)
        };

        Assert.Single(set);
    }

    [Fact]
    public void Dictionary_UsesValueEquality()
    {
        var dict = new Dictionary<PortEntryKey, string>
        {
            [new PortEntryKey("TCP", "127.0.0.1", 5000, 1234)] = "first"
        };

        Assert.True(dict.TryGetValue(new PortEntryKey("TCP", "127.0.0.1", 5000, 1234), out var value));
        Assert.Equal("first", value);
    }
}
