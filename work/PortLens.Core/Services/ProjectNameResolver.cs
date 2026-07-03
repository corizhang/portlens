using System.Text.RegularExpressions;

namespace PortLens.Services;

internal static class ProjectNameResolver
{
    private static readonly Regex QuotedWindowsPathRegex = new(@"""([A-Za-z]:\\[^""]+)""", RegexOptions.Compiled);
    private static readonly Regex UnquotedWindowsPathRegex = new(@"[A-Za-z]:\\[^\s""']+", RegexOptions.Compiled);

    public static string? ResolveProjectName(string? workingDirectory, string processName)
    {
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            var name = Path.GetFileName(workingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return processName;
    }

    public static string? InferWorkingDirectory(string? commandLine, string? executablePath, string? currentDirectory)
    {
        var projectDirectory = InferProjectDirectory(commandLine);
        if (!string.IsNullOrWhiteSpace(projectDirectory))
        {
            return projectDirectory;
        }

        if (ExistingDirectoryOrNull(currentDirectory) is { } existingCurrentDirectory)
        {
            return existingCurrentDirectory;
        }

        if (!string.IsNullOrWhiteSpace(commandLine))
        {
            var lowered = commandLine.ToLowerInvariant();
            var markers = new[] { "--cwd ", "-workingdirectory ", " --project " };
            foreach (var marker in markers)
            {
                var index = lowered.IndexOf(marker, StringComparison.Ordinal);
                if (index >= 0)
                {
                    var tail = commandLine[(index + marker.Length)..].Trim().Trim('"');
                    var candidate = tail.Split('"', ' ').FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        if (IsGoBuildPath(executablePath))
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(executablePath) ? Path.GetDirectoryName(executablePath) : null;
    }

    private static string? InferProjectDirectory(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        foreach (var path in ExtractWindowsPaths(commandLine))
        {
            var normalized = path.Trim().Trim('"');
            var lowered = normalized.ToLowerInvariant();

            var nodeModulesIndex = lowered.IndexOf(@"\node_modules\", StringComparison.Ordinal);
            if (nodeModulesIndex > 0)
            {
                return ExistingDirectoryOrNull(normalized[..nodeModulesIndex]);
            }

            if (lowered.EndsWith(@"\manage.py", StringComparison.Ordinal))
            {
                return ExistingDirectoryOrNull(Path.GetDirectoryName(normalized));
            }

            if (lowered.EndsWith(".csproj", StringComparison.Ordinal))
            {
                return ExistingDirectoryOrNull(Path.GetDirectoryName(normalized));
            }

            if (lowered.EndsWith(".dll", StringComparison.Ordinal))
            {
                var binIndex = lowered.LastIndexOf(@"\bin\", StringComparison.Ordinal);
                if (binIndex > 0)
                {
                    return ExistingDirectoryOrNull(normalized[..binIndex]);
                }

                return ExistingDirectoryOrNull(Path.GetDirectoryName(normalized));
            }

            if (lowered.EndsWith(".jar", StringComparison.Ordinal))
            {
                var buildIndex = lowered.LastIndexOf(@"\build\", StringComparison.Ordinal);
                if (buildIndex > 0)
                {
                    return ExistingDirectoryOrNull(normalized[..buildIndex]);
                }

                var targetIndex = lowered.LastIndexOf(@"\target\", StringComparison.Ordinal);
                if (targetIndex > 0)
                {
                    return ExistingDirectoryOrNull(normalized[..targetIndex]);
                }

                return ExistingDirectoryOrNull(Path.GetDirectoryName(normalized));
            }

            if (lowered.EndsWith(".exe", StringComparison.Ordinal) && !IsGoBuildPath(normalized))
            {
                return ExistingDirectoryOrNull(Path.GetDirectoryName(normalized));
            }
        }

        return null;
    }

    private static IEnumerable<string> ExtractWindowsPaths(string text)
    {
        foreach (Match match in QuotedWindowsPathRegex.Matches(text))
        {
            yield return match.Groups[1].Value.TrimEnd(',', ';');
        }

        foreach (Match match in UnquotedWindowsPathRegex.Matches(text))
        {
            yield return match.Value.TrimEnd(',', ';');
        }
    }

    private static string? ExistingDirectoryOrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Directory.Exists(path) ? path : null;
    }

    private static bool IsGoBuildPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            return path.Contains(@"\go-build\", StringComparison.OrdinalIgnoreCase);
        }

        var goBuildRoot = Path.Combine(localAppData, "go-build");
        return path.StartsWith(goBuildRoot, StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\go-build\", StringComparison.OrdinalIgnoreCase);
    }
}
