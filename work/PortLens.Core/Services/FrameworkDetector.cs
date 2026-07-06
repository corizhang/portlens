using System.IO.Compression;
using System.Text.RegularExpressions;
using PortLens.Models;

namespace PortLens.Services;

public static class FrameworkDetector
{
    private static readonly char[] QuoteChars = ['"', '\''];

    public static string InferFramework(PortEntry entry)
    {
        var text = $"{entry.ProcessName} {entry.CommandLine} {entry.WorkingDirectory} {entry.ExecutablePath}".ToLowerInvariant();
        if (ContainsAny(text, "vite", "vite.js", "vite\\bin", "vite/bin")) return "Vite";
        if (ContainsAny(text, "next dev", "next start", "next-server", "\\next\\", "/next/")) return "Next.js";
        if (ContainsAny(text, "nuxt", "nuxi")) return "Nuxt";
        if (ContainsAny(text, "manage.py runserver", "django.core", "daphne", "runserver")) return "Django";
        if (ContainsAny(text, "fastapi", "uvicorn", "hypercorn")) return "FastAPI";
        if (ContainsAny(text, "spring-boot", "springframework", "org.springframework.boot")) return "Spring";
        if (ContainsAny(text, "dotnet", "kestrel", "aspnetcore")) return ".NET";
        if (ContainsAny(text, "go run", "air.toml", "\\air.exe", "/air", "gin-gonic", "fiber", "echo/v4", "go-build", "\\go.exe", "/go ")) return "Go";
        if (ContainsAny(text, "docker-proxy", "com.docker", "docker desktop", "dockerd")) return "Docker";
        if (ContainsAny(text, "wslhost", "wslservice", "wsl.exe", "\\wsl$")) return "WSL";

        if (entry.ProcessName.Contains("java", StringComparison.OrdinalIgnoreCase)
            && entry.CommandLine is { Length: > 0 }
            && IsSpringBootJarCommandLine(entry.CommandLine))
        {
            return "Spring";
        }

        return "";
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.Ordinal));
    }

    private static bool IsSpringBootJarCommandLine(string commandLine)
    {
        foreach (var jarPath in ExtractJarPaths(commandLine))
        {
            if (string.IsNullOrWhiteSpace(jarPath) || !File.Exists(jarPath))
            {
                continue;
            }

            if (JarManifestContainsSpringBootLauncher(jarPath))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> ExtractJarPaths(string commandLine)
    {
        foreach (var token in commandLine.Split([' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = token.Trim().Trim(QuoteChars);
            if (trimmed.Length > 4
                && trimmed.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                && trimmed.Length > 2
                && trimmed[1] == ':')
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
