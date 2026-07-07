using PortLens.Services;
using Xunit;

namespace PortLens.Core.Tests;

public sealed class CommandLineNormalizerTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("\t\n", null)]
    public void Normalize_ReturnsNull_ForEmptyOrWhitespace(string? input, string? expected)
    {
        Assert.Equal(expected, CommandLineNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("node app.js", "node app.js")]
    [InlineData("  node   app.js  ", "node app.js")]
    [InlineData("node\tapp.js", "node app.js")]
    [InlineData("node\napp.js", "node app.js")]
    [InlineData("node\r\napp.js", "node app.js")]
    [InlineData("node    app.js", "node app.js")]
    [InlineData("  node   app.js   arg  ", "node app.js arg")]
    [InlineData("\tnode\t\tapp.js\targ\n", "node app.js arg")]
    public void Normalize_CollapsesWhitespace(string input, string expected)
    {
        Assert.Equal(expected, CommandLineNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_Preserves_SingleSpacesBetweenArguments()
    {
        const string input = "dotnet run --project MyApp --verbosity quiet";
        Assert.Equal(input, CommandLineNormalizer.Normalize(input));
    }
}
