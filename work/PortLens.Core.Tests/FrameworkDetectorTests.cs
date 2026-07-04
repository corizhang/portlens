using PortLens.Models;
using PortLens.Services;
using Xunit;

namespace PortLens.Core.Tests;

public class FrameworkDetectorTests
{
    [Theory]
    [InlineData("node", "vite", null, "Vite")]
    [InlineData("node", "next dev", null, "Next.js")]
    [InlineData("node", "nuxt", null, "Nuxt")]
    [InlineData("python", "manage.py runserver", null, "Django")]
    [InlineData("python", "uvicorn main:app", null, "FastAPI")]
    [InlineData("java", "spring-boot", null, "Spring")]
    [InlineData("", "dotnet run", null, ".NET")]
    [InlineData("go", "go run main.go", null, "Go")]
    [InlineData("docker-proxy", "", null, "Docker")]
    [InlineData("wslhost", "", null, "WSL")]
    [InlineData("unknown", "", null, "")]
    public void InferFramework_MatchesExpected(string processName, string? commandLine, string? workingDirectory, string expected)
    {
        var entry = new PortEntry
        {
            ProcessName = processName,
            CommandLine = commandLine,
            WorkingDirectory = workingDirectory
        };

        var actual = FrameworkDetector.InferFramework(entry);

        Assert.Equal(expected, actual);
    }
}
