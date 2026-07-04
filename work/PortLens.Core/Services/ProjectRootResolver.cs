namespace PortLens.Services;

public static class ProjectRootResolver
{
    private static readonly HashSet<string> ChildProjectNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "api",
        "app",
        "backend",
        "client",
        "frontend",
        "server",
        "web"
    };

    private static readonly HashSet<string> WorkspaceContainerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "apps",
        "packages",
        "services"
    };

    public static string? Resolve(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return directory;
        }

        var current = new DirectoryInfo(directory);
        var markerRoot = FindMarkerRoot(current);
        if (markerRoot is not null)
        {
            return markerRoot.FullName;
        }

        var parent = current.Parent;
        if (parent is null)
        {
            return current.FullName;
        }

        if (ChildProjectNames.Contains(current.Name))
        {
            return parent.FullName;
        }

        if (WorkspaceContainerNames.Contains(parent.Name) && parent.Parent is not null)
        {
            return parent.Parent.FullName;
        }

        return current.FullName;
    }

    public static string DisplayName(string? directory, string fallback)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return fallback;
        }

        var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private static DirectoryInfo? FindMarkerRoot(DirectoryInfo start)
    {
        for (var current = start; current is not null; current = current.Parent)
        {
            if (HasRootMarker(current))
            {
                return current;
            }
        }

        return null;
    }

    private static bool HasRootMarker(DirectoryInfo directory)
    {
        try
        {
            return Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || Directory.Exists(Path.Combine(directory.FullName, ".idea"))
                || Directory.Exists(Path.Combine(directory.FullName, ".vscode"))
                || File.Exists(Path.Combine(directory.FullName, "pnpm-workspace.yaml"))
                || File.Exists(Path.Combine(directory.FullName, "turbo.json"))
                || File.Exists(Path.Combine(directory.FullName, "nx.json"))
                || File.Exists(Path.Combine(directory.FullName, "lerna.json"))
                || File.Exists(Path.Combine(directory.FullName, "docker-compose.yml"))
                || File.Exists(Path.Combine(directory.FullName, "go.mod"))
                || File.Exists(Path.Combine(directory.FullName, "pyproject.toml"))
                || File.Exists(Path.Combine(directory.FullName, "Cargo.toml"))
                || File.Exists(Path.Combine(directory.FullName, "composer.json"))
                || File.Exists(Path.Combine(directory.FullName, "deno.json"))
                || File.Exists(Path.Combine(directory.FullName, "bun.lockb"))
                || Directory.EnumerateFiles(directory.FullName, "*.sln").Any()
                || PackageJsonHasWorkspaces(Path.Combine(directory.FullName, "package.json"));
        }
        catch
        {
            return false;
        }
    }

    private static bool PackageJsonHasWorkspaces(string path)
    {
        try
        {
            return File.Exists(path) && File.ReadAllText(path).Contains("\"workspaces\"", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
