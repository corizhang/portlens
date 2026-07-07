using System.Collections.Concurrent;

namespace PortLens.Services;

public static class ProjectRootResolver
{
    private static readonly ConcurrentDictionary<string, CacheEntry> RootCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

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

        var normalized = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (RootCache.TryGetValue(normalized, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Root;
        }

        var result = ResolveUncached(directory);
        RootCache[normalized] = new CacheEntry(result, DateTimeOffset.UtcNow + CacheTtl);
        return result;
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
    /// Computes a subtitle for a project group entry, preferring the path of <paramref name="workingDirectory"/u003e
    /// relative to <paramref name="rootDirectory"/u003e so that entries grouped under a shared root remain distinguishable.
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

    private static string? ResolveUncached(string? directory)
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
            var path = directory.FullName;
            return Directory.Exists(Path.Combine(path, ".git"))
                || PackageJsonHasWorkspaces(Path.Combine(path, "package.json"))
                || Directory.Exists(Path.Combine(path, ".vscode"))
                || Directory.Exists(Path.Combine(path, ".idea"))
                || Directory.EnumerateFiles(path, "*.sln").Any()
                || File.Exists(Path.Combine(path, "pnpm-workspace.yaml"))
                || File.Exists(Path.Combine(path, "turbo.json"))
                || File.Exists(Path.Combine(path, "nx.json"))
                || File.Exists(Path.Combine(path, "lerna.json"))
                || File.Exists(Path.Combine(path, "docker-compose.yml"))
                || File.Exists(Path.Combine(path, "go.mod"))
                || File.Exists(Path.Combine(path, "pyproject.toml"))
                || File.Exists(Path.Combine(path, "Cargo.toml"))
                || File.Exists(Path.Combine(path, "composer.json"))
                || File.Exists(Path.Combine(path, "deno.json"))
                || File.Exists(Path.Combine(path, "bun.lockb"));
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

    private sealed record CacheEntry(string? Root, DateTimeOffset ExpiresAt);
}
