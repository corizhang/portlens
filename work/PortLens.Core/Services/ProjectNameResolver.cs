using System.Linq;
using System.Text.RegularExpressions;

namespace PortLens.Services;

public static class ProjectNameResolver
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
        var projectDirectory = InferProjectDirectory(commandLine, currentDirectory);
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

    private static string? InferProjectDirectory(string? commandLine, string? currentDirectory)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        if (TryResolveRelativeJarWorkingDirectory(commandLine, currentDirectory) is { } jarWorkingDirectory)
        {
            return jarWorkingDirectory;
        }

        var javaProjectDirectory = TryResolveJavaProjectDirectory(commandLine);
        if (!string.IsNullOrWhiteSpace(javaProjectDirectory))
        {
            return javaProjectDirectory;
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

    private static string? TryResolveJavaProjectDirectory(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var lowered = commandLine.ToLowerInvariant();
        if (!lowered.Contains("java.exe") || (!lowered.Contains(" -cp ") && !lowered.Contains("\t-cp\t") && !lowered.Contains(" -classpath ") && !lowered.Contains("\t-classpath\t")))
        {
            return null;
        }

        var classpathMarker = lowered.Contains(" -cp ") ? " -cp " : lowered.Contains("\t-cp\t") ? "\t-cp\t" : lowered.Contains(" -classpath ") ? " -classpath " : "\t-classpath\t";
        var start = lowered.IndexOf(classpathMarker, StringComparison.Ordinal) + classpathMarker.Length;
        if (start >= commandLine.Length)
        {
            return null;
        }

        var remainder = commandLine[start..].Trim();
        var classpathValue = remainder.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(classpathValue))
        {
            return null;
        }

        classpathValue = classpathValue.Trim('"', '\'');
        var entries = classpathValue.Split([Path.PathSeparator], StringSplitOptions.RemoveEmptyEntries);

        string? bestDirectory = null;
        var bestScore = int.MinValue;
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var normalized = entry.Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            string? directory = null;
            var entryLowered = normalized.ToLowerInvariant();
            if (entryLowered.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                directory = Path.GetDirectoryName(normalized);
            }
            else if (Directory.Exists(normalized))
            {
                directory = normalized;
            }

            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var score = ScoreProjectRoot(directory);
            if (score > bestScore)
            {
                bestScore = score;
                bestDirectory = directory;
            }
        }

        if (bestDirectory is null)
        {
            return null;
        }

        var info = new DirectoryInfo(bestDirectory);
        for (var current = info; current is not null; current = current.Parent)
        {
            if (HasJavaProjectMarker(current))
            {
                return current.FullName;
            }
        }

        return bestDirectory;
    }

    private static bool HasJavaProjectMarker(DirectoryInfo directory)
    {
        return File.Exists(Path.Combine(directory.FullName, "pom.xml"))
            || File.Exists(Path.Combine(directory.FullName, "build.gradle"))
            || File.Exists(Path.Combine(directory.FullName, "build.gradle.kts"))
            || Directory.Exists(Path.Combine(directory.FullName, ".git"))
            || Directory.Exists(Path.Combine(directory.FullName, ".idea"));
    }

    private static string? TryResolveRelativeJarWorkingDirectory(string? commandLine, string? currentDirectory)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var lowered = commandLine.ToLowerInvariant();
        var jarIndex = lowered.IndexOf("-jar", StringComparison.Ordinal);
        if (jarIndex < 0)
        {
            return null;
        }

        var afterJar = commandLine[(jarIndex + 4)..].Trim();
        var jarPath = afterJar.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(jarPath))
        {
            return null;
        }

        jarPath = jarPath.Trim('"', '\'');
        if (Path.IsPathRooted(jarPath) || !jarPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalizedJarPath = jarPath.Replace('/', Path.DirectorySeparatorChar);
        var jarFile = FindRelativeFile(normalizedJarPath, currentDirectory);
        if (jarFile is null)
        {
            return null;
        }

        var relativeSegments = normalizedJarPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;
        var directory = new FileInfo(jarFile).Directory;
        for (var i = 1; i < relativeSegments && directory is not null; i++)
        {
            directory = directory.Parent;
        }

        return directory?.FullName;
    }

    private static string? FindRelativeFile(string relativePath, string? currentDirectory)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            var candidate = Path.GetFullPath(Path.Combine(currentDirectory, relativePath));
            if (File.Exists(candidate))
            {
                candidates.Add(candidate);
            }

            var dir = new DirectoryInfo(currentDirectory);
            while (dir is not null)
            {
                candidate = Path.GetFullPath(Path.Combine(dir.FullName, relativePath));
                if (File.Exists(candidate))
                {
                    candidates.Add(candidate);
                }

                dir = dir.Parent;
            }
        }

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            var candidate = Path.GetFullPath(Path.Combine(drive.RootDirectory.FullName, relativePath));
            if (File.Exists(candidate))
            {
                candidates.Add(candidate);
            }
        }

        return candidates.Count == 0
            ? null
            : candidates.OrderByDescending(ScoreProjectRoot).First();
    }

    private static int ScoreProjectRoot(string filePath)
    {
        var score = 0;
        var directory = new FileInfo(filePath).Directory;
        while (directory is not null)
        {
            var fullName = directory.FullName;
            if (Directory.Exists(Path.Combine(fullName, ".git"))) score += 100;
            if (Directory.Exists(Path.Combine(fullName, ".idea"))) score += 50;
            if (File.Exists(Path.Combine(fullName, "pom.xml"))) score += 30;
            if (File.Exists(Path.Combine(fullName, "build.gradle")) || File.Exists(Path.Combine(fullName, "build.gradle.kts"))) score += 30;
            if (File.Exists(Path.Combine(fullName, "package.json"))) score += 20;
            directory = directory.Parent;
        }

        var lowered = filePath.ToLowerInvariant();
        if (lowered.Contains(@"\target\") || lowered.Contains(@"\build\")) score += 10;

        return score;
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
