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

    [Fact]
    public void InferWorkingDirectory_RelativeJarFromAncestorDirectory_ReturnsProjectRoot()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"PortLensTests-{Guid.NewGuid()}");
        Directory.CreateDirectory(projectDir);
        _tempPaths.Add(projectDir);
        var jarDir = Path.Combine(projectDir, "mall-backend", "mall-server-web", "target");
        Directory.CreateDirectory(jarDir);
        var jarPath = Path.Combine(jarDir, "mall-server-web-0.0.1-SNAPSHOT.jar");
        File.WriteAllText(jarPath, "fake jar");

        var jdkBin = Path.Combine(projectDir, "jdk", "bin");
        Directory.CreateDirectory(jdkBin);

        var commandLine = $"\"{jdkBin}\\java.exe\" -jar mall-backend/mall-server-web/target/mall-server-web-0.0.1-SNAPSHOT.jar";

        var actual = ProjectNameResolver.InferWorkingDirectory(commandLine, jdkBin, jdkBin);

        Assert.Equal(projectDir, actual);
    }

    [Fact]
    public void InferWorkingDirectory_RelativeJarFromSiblingDirectory_SearchesAncestors()
    {
        var parentDir = Path.Combine(Path.GetTempPath(), $"PortLensTests-{Guid.NewGuid()}");
        Directory.CreateDirectory(parentDir);
        _tempPaths.Add(parentDir);

        var projectDir = Path.Combine(parentDir, "project");
        Directory.CreateDirectory(projectDir);
        var jarDir = Path.Combine(projectDir, "target");
        Directory.CreateDirectory(jarDir);
        var jarPath = Path.Combine(jarDir, "app.jar");
        File.WriteAllText(jarPath, "fake jar");

        var unrelatedDir = Path.Combine(parentDir, "unrelated");
        Directory.CreateDirectory(unrelatedDir);

        var commandLine = "java -jar project/target/app.jar";

        var actual = ProjectNameResolver.InferWorkingDirectory(commandLine, unrelatedDir, unrelatedDir);

        Assert.Equal(parentDir, actual);
    }

    [Fact]
    public void InferWorkingDirectory_MavenSpringBootRunClasspath_ReturnsProjectRoot()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"PortLensTests-{Guid.NewGuid()}");
        Directory.CreateDirectory(projectDir);
        _tempPaths.Add(projectDir);
        var classesDir = Path.Combine(projectDir, "mall-server-web", "target", "classes");
        Directory.CreateDirectory(classesDir);

        var jdkBin = Path.Combine(projectDir, "jdk", "bin");
        Directory.CreateDirectory(jdkBin);
        var repoJar = Path.Combine(projectDir, ".m2", "repository", "org", "example", "lib.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(repoJar)!);
        File.WriteAllText(repoJar, "fake jar");
        var pomPath = Path.Combine(projectDir, "pom.xml");
        File.WriteAllText(pomPath, "<project/>");

        var classpath = $"{classesDir};{repoJar}";
        var commandLine = $"\"{jdkBin}\\java.exe\" -XX:TieredStopAtLevel=1 -cp \"{classpath}\" com.mall.web.MallServerApplication";

        var actual = ProjectNameResolver.InferWorkingDirectory(commandLine, jdkBin, jdkBin);

        Assert.Equal(projectDir, actual);
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
