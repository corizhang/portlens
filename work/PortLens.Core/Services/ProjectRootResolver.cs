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
            var aggregated = TryAggregateToParent(markerRoot);
            return aggregated?.FullName ?? markerRoot.FullName;
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

    /// <summary>
    /// Computes a subtitle for a project group entry, preferring the path of <paramref name="workingDirectory"/>
    /// relative to <paramref name="rootDirectory"/> so that entries grouped under a shared root remain distinguishable.
    /// </summary>
    public static string ComputeRelativeSubtitle(string? rootDirectory, string? workingDirectory, string fallback)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return fallback;
        }

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return DisplayName(workingDirectory, fallback);
        }

        var root = rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var work = workingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (root.Equals(work, StringComparison.OrdinalIgnoreCase))
        {
            return DisplayName(workingDirectory, fallback);
        }

        if (work.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || work.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            var relative = work[(root.Length + 1)..];
            return string.IsNullOrWhiteSpace(relative) ? DisplayName(workingDirectory, fallback) : relative;
        }

        return DisplayName(workingDirectory, fallback);
    }

    private static DirectoryInfo? TryAggregateToParent(DirectoryInfo markerRoot)
    {
        var parent = markerRoot.Parent;
        if (parent is null)
        {
            return null;
        }

        if (!ChildProjectNames.Contains(markerRoot.Name))
        {
            return null;
        }

        // Keep existing workspace-container semantics (e.g. apps/web stays web).
        if (WorkspaceContainerNames.Contains(parent.Name))
        {
            return null;
        }

        // Aggregate frontend/backend siblings into a shared parent project root.
        if (HasRootMarker(parent))
        {
            return parent;
        }

        return null;
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
