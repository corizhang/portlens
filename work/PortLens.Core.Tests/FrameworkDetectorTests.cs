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

    [Fact]
    public void InferFramework_SpringBootJar_ReturnsSpring()
    {
        var jarPath = CreateFakeSpringBootJar();
        var entry = new PortEntry
        {
            ProcessName = "java",
            CommandLine = $"java -jar \"{jarPath}\""
        };

        var actual = FrameworkDetector.InferFramework(entry);

        Assert.Equal("Spring", actual);
    }

    [Fact]
    public void InferFramework_NonSpringBootJar_ReturnsEmpty()
    {
        var jarPath = CreateFakePlainJar();
        var entry = new PortEntry
        {
            ProcessName = "java",
            CommandLine = $"java -jar \"{jarPath}\""
        };

        var actual = FrameworkDetector.InferFramework(entry);

        Assert.Equal(string.Empty, actual);
    }

    private static string CreateFakeSpringBootJar()
    {
        var path = Path.Combine(Path.GetTempPath(), $"portlens-spring-{Guid.NewGuid()}.jar");
        using var stream = File.Create(path);
        using var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create);
        var entry = zip.CreateEntry("META-INF/MANIFEST.MF");
        using var writer = new StreamWriter(entry.Open());
        writer.WriteLine("Main-Class: org.springframework.boot.loader.launch.JarLauncher");
        return path;
    }

    private static string CreateFakePlainJar()
    {
        var path = Path.Combine(Path.GetTempPath(), $"portlens-plain-{Guid.NewGuid()}.jar");
        using var stream = File.Create(path);
        using var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create);
        var entry = zip.CreateEntry("META-INF/MANIFEST.MF");
        using var writer = new StreamWriter(entry.Open());
        writer.WriteLine("Main-Class: com.example.Main");
        return path;
    }
}
