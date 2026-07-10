using System.IO.Compression;
using PortLens.Models;

namespace PortLens.Services;

public static class FrameworkDetector
{
    private static readonly char[] QuoteChars = ['"', '\''];

    public static string InferFramework(PortEntry entry)
        => InferFramework(entry, FrameworkRules.Defaults);

    public static string InferFramework(PortEntry entry, IReadOnlyList<FrameworkRule> rules)
    {
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                continue;
            }

            if (MatchesRule(entry, rule))
            {
                return rule.Name;
            }
        }

        // Final fallback for Spring Boot executable JARs.
        if (rules.Any(rule => rule.Name.Equals("Spring", StringComparison.OrdinalIgnoreCase))
            && entry.ProcessName.Contains("java", StringComparison.OrdinalIgnoreCase)
            && entry.CommandLine is { Length: > 0 }
            && IsSpringBootJarCommandLine(entry.CommandLine, entry.WorkingDirectory))
        {
            return "Spring";
        }

        return "";
    }

    private static bool MatchesRule(PortEntry entry, FrameworkRule rule)
        => ContainsAny(entry.ProcessName, rule.ProcessNameKeywords)
           || ContainsAny(entry.CommandLine, rule.CommandLineKeywords)
           || ContainsAny(entry.WorkingDirectory, rule.PathKeywords)
           || ContainsAny(entry.ExecutablePath, rule.PathKeywords);

    private static bool ContainsAny(string? text, IReadOnlyList<string> needles)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var span = text.AsSpan();
        foreach (var needle in needles)
        {
            if (MemoryExtensions.Contains(span, needle.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSpringBootJarCommandLine(string commandLine, string? workingDirectory)
    {
        foreach (var jarPath in ExtractJarPaths(commandLine))
        {
            var absolutePath = ResolveJarPath(jarPath, workingDirectory);
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                continue;
            }

            if (JarManifestContainsSpringBootLauncher(absolutePath))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveJarPath(string jarPath, string? workingDirectory)
    {
        if (Path.IsPathRooted(jarPath))
        {
            return jarPath;
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            var candidate = Path.GetFullPath(Path.Combine(workingDirectory, jarPath));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> ExtractJarPaths(string commandLine)
    {
        foreach (var token in commandLine.Split([' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = token.Trim().Trim(QuoteChars);
            if (trimmed.Length > 4
                && trimmed.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                yield return trimmed;
            }
        }
    }

    private static bool JarManifestContainsSpringBootLauncher(string jarPath)
    {
        try
        {
            using var stream = File.OpenRead(jarPath);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var manifest = zip.GetEntry("META-INF/MANIFEST.MF");
            if (manifest is null)
            {
                return false;
            }

            using var reader = new StreamReader(manifest.Open());
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.StartsWith("Main-Class:", StringComparison.OrdinalIgnoreCase)
                    && line.Contains("org.springframework.boot.loader", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore unreadable or locked JAR files.
        }

        return false;
    }
}
