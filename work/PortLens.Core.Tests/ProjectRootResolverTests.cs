using PortLens.Services;
using Xunit;

namespace PortLens.Core.Tests;

public class ProjectRootResolverTests : IDisposable
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
    public void Resolve_ReturnsDirectory_WhenNullOrEmpty()
    {
        Assert.Null(ProjectRootResolver.Resolve(null));
        Assert.Equal("", ProjectRootResolver.Resolve(""));
        Assert.Equal("   ", ProjectRootResolver.Resolve("   "));
    }

    [Fact]
    public void Resolve_FindsGitRoot()
    {
        var root = CreateTempDir();
        var child = Path.Combine(root, "src", "app");
        Directory.CreateDirectory(child);
        Directory.CreateDirectory(Path.Combine(root, ".git"));

        var actual = ProjectRootResolver.Resolve(child);

        Assert.Equal(root, actual);
    }

    [Fact]
    public void Resolve_AggregatesChildProjects_ToSharedParentRoot()
    {
        var root = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        var frontend = Path.Combine(root, "frontend");
        var backend = Path.Combine(root, "backend");
        Directory.CreateDirectory(frontend);
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(Path.Combine(frontend, ".vscode"));
        Directory.CreateDirectory(Path.Combine(backend, ".vscode"));

        Assert.Equal(root, ProjectRootResolver.Resolve(frontend));
        Assert.Equal(root, ProjectRootResolver.Resolve(backend));
    }

    [Fact]
    public void Resolve_KeepsWorkspaceContainerSemantics()
    {
        var root = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        var webDir = Path.Combine(root, "apps", "web");
        Directory.CreateDirectory(webDir);
        Directory.CreateDirectory(Path.Combine(webDir, ".vscode"));

        Assert.Equal(webDir, ProjectRootResolver.Resolve(webDir));
    }

    [Fact]
    public void ComputeRelativeSubtitle_ReturnsRelativePath_WhenUnderRoot()
    {
        var subtitle = ProjectRootResolver.ComputeRelativeSubtitle(
            "C:\\Projects\\MyApp", "C:\\Projects\\MyApp\\frontend", "fallback");

        Assert.Equal("frontend", subtitle);
    }

    [Fact]
    public void ComputeRelativeSubtitle_ReturnsDisplayName_WhenSameAsRoot()
    {
        var subtitle = ProjectRootResolver.ComputeRelativeSubtitle(
            "C:\\Projects\\MyApp", "C:\\Projects\\MyApp", "fallback");

        Assert.Equal("MyApp", subtitle);
    }

    [Fact]
    public void ComputeRelativeSubtitle_ReturnsFallback_WhenWorkingDirectoryMissing()
    {
        Assert.Equal("fallback", ProjectRootResolver.ComputeRelativeSubtitle("C:\\Projects", null, "fallback"));
    }

    [Fact]
    public void Resolve_ReturnsParent_WhenDirectoryIsChildProjectName()
    {
        var root = CreateTempDir();
        var apiDir = Path.Combine(root, "api");
        Directory.CreateDirectory(apiDir);

        var actual = ProjectRootResolver.Resolve(apiDir);

        Assert.Equal(root, actual);
    }

    [Fact]
    public void Resolve_ReturnsWorkspaceRoot_WhenParentIsWorkspaceContainer()
    {
        var root = CreateTempDir();
        var appsDir = Path.Combine(root, "apps");
        var serviceDir = Path.Combine(appsDir, "service");
        Directory.CreateDirectory(serviceDir);

        var actual = ProjectRootResolver.Resolve(serviceDir);

        Assert.Equal(root, actual);
    }

    [Fact]
    public void Resolve_ReturnsCurrentDirectory_WhenNoMarkerOrPattern()
    {
        var dir = CreateTempDir();

        var actual = ProjectRootResolver.Resolve(dir);

        Assert.Equal(dir, actual);
    }

    [Fact]
    public void DisplayName_ReturnsFallback_WhenDirectoryNullOrEmpty()
    {
        Assert.Equal("fallback", ProjectRootResolver.DisplayName(null, "fallback"));
        Assert.Equal("fallback", ProjectRootResolver.DisplayName("", "fallback"));
    }

    [Fact]
    public void DisplayName_ReturnsLastDirectorySegment()
    {
        Assert.Equal("MyProject", ProjectRootResolver.DisplayName("C:\\Projects\\MyProject", "fallback"));
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
