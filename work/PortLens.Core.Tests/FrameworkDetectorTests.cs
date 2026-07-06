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

    [Fact]
    public void InferFramework_RelativeSpringBootJarWithWorkingDirectory_ReturnsSpring()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"portlens-rel-{Guid.NewGuid()}");
        var jarDir = Path.Combine(tempDir, "mall-backend", "mall-server-web", "target");
        Directory.CreateDirectory(jarDir);
        var jarPath = Path.Combine(jarDir, "mall-server-web-0.0.1-SNAPSHOT.jar");
        CreateFakeSpringBootJarAt(jarPath);

        var entry = new PortEntry
        {
            ProcessName = "java",
            CommandLine = "java -jar mall-backend/mall-server-web/target/mall-server-web-0.0.1-SNAPSHOT.jar",
            WorkingDirectory = tempDir
        };

        var actual = FrameworkDetector.InferFramework(entry);

        Assert.Equal("Spring", actual);
    }

    private static string CreateFakeSpringBootJar()
    {
        var path = Path.Combine(Path.GetTempPath(), $"portlens-spring-{Guid.NewGuid()}.jar");
        CreateFakeSpringBootJarAt(path);
        return path;
    }

    private static void CreateFakeSpringBootJarAt(string path)
    {
        using var stream = File.Create(path);
        using var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create);
        var entry = zip.CreateEntry("META-INF/MANIFEST.MF");
        using var writer = new StreamWriter(entry.Open());
        writer.WriteLine("Main-Class: org.springframework.boot.loader.launch.JarLauncher");
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
