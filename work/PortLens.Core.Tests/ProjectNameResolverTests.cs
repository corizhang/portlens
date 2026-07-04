using PortLens.Services;
using Xunit;

namespace PortLens.Core.Tests;

public class ProjectNameResolverTests : IDisposable
{
    private readonly List<string> _tempPaths = [];

    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public void ResolveProjectName_ReturnsDirectoryName_WhenWorkingDirectoryPresent()
    {
        var actual = ProjectNameResolver.ResolveProjectName("C:\\Projects\\MyApp", "process");

        Assert.Equal("MyApp", actual);
    }

    [Fact]
    public void ResolveProjectName_FallsBackToProcessName_WhenWorkingDirectoryMissing()
    {
        var actual = ProjectNameResolver.ResolveProjectName(null, "process");

        Assert.Equal("process", actual);
    }

    [Fact]
    public void InferWorkingDirectory_ExtractsProjectDirectory_FromNodeModulesPath()
    {
        var projectDir = CreateTempDir();
        var nodeModulesDir = Path.Combine(projectDir, "node_modules", "package");
        Directory.CreateDirectory(nodeModulesDir);
        var commandLine = $"node \"{nodeModulesDir}\\index.js\"";

        var actual = ProjectNameResolver.InferWorkingDirectory(commandLine, null, null);

        Assert.Equal(projectDir, actual);
    }

    [Fact]
    public void InferWorkingDirectory_ExtractsProjectDirectory_FromCsprojPath()
    {
        var projectDir = CreateTempDir();
        var csprojPath = Path.Combine(projectDir, "MyApp.csproj");
        File.WriteAllText(csprojPath, "<Project/>");
        var commandLine = $"dotnet build \"{csprojPath}\"";

        var actual = ProjectNameResolver.InferWorkingDirectory(commandLine, null, null);

        Assert.Equal(projectDir, actual);
    }

    [Fact]
    public void InferWorkingDirectory_ExtractsProjectDirectory_FromDllInBin()
    {
        var projectDir = CreateTempDir();
        var dllPath = Path.Combine(projectDir, "bin", "Debug", "net10.0", "MyApp.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(dllPath)!);
        File.WriteAllText(dllPath, "");
        var commandLine = $"dotnet \"{dllPath}\"";

        var actual = ProjectNameResolver.InferWorkingDirectory(commandLine, null, null);

        Assert.Equal(projectDir, actual);
    }

    [Fact]
    public void InferWorkingDirectory_UsesCwdMarker_WhenCommandLineHasNoPath()
    {
        var currentDir = CreateTempDir();

        var actual = ProjectNameResolver.InferWorkingDirectory("some command", null, currentDir);

        Assert.Equal(currentDir, actual);
    }

    [Fact]
    public void InferWorkingDirectory_UsesExecutableDirectory_AsFallback()
    {
        var exeDir = CreateTempDir();
        var exePath = Path.Combine(exeDir, "app.exe");
        File.WriteAllText(exePath, "");

        var actual = ProjectNameResolver.InferWorkingDirectory("app", exePath, null);

        Assert.Equal(exeDir, actual);
    }

    [Fact]
    public void InferWorkingDirectory_ReturnsNull_ForGoBuildPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var goBuildDir = Path.Combine(localAppData, "go-build", "tmp");
        Directory.CreateDirectory(goBuildDir);
        _tempPaths.Add(goBuildDir);
        var exePath = Path.Combine(goBuildDir, "app.exe");
        File.WriteAllText(exePath, "");

        var actual = ProjectNameResolver.InferWorkingDirectory(null, exePath, null);

        Assert.Null(actual);
    }

    private string CreateTempDir()
    {
        var root = Path.GetPathRoot(Path.GetTempPath()) ?? Path.GetTempPath();
        var path = Path.Combine(root, $"PortLensTests-{Guid.NewGuid()}");
        Directory.CreateDirectory(path);
        _tempPaths.Add(path);
        return path;
    }
}
