using System.IO.Compression;
using PortLens.Models;

namespace PortLens.Services;

public static class FrameworkDetector
{
    private static readonly char[] QuoteChars = ['"', '\''];

    private static readonly string[] ViteNeedles = ["vite", "vite.js", "vite\\bin", "vite/bin"];
    private static readonly string[] NextNeedles = ["next dev", "next start", "next-server", "\\next\\", "/next/"];
    private static readonly string[] NuxtNeedles = ["nuxt", "nuxi"];
    private static readonly string[] DjangoNeedles = ["manage.py runserver", "django.core", "daphne", "runserver"];
    private static readonly string[] FastApiNeedles = ["fastapi", "uvicorn", "hypercorn"];
    private static readonly string[] SpringNeedles = ["spring-boot", "springframework", "org.springframework.boot"];
    private static readonly string[] DotNetNeedles = ["dotnet", "kestrel", "aspnetcore"];
    private static readonly string[] GoNeedles = ["go run", "air.toml", "\\air.exe", "/air", "gin-gonic", "fiber", "echo/v4", "go-build", "\\go.exe", "/go "];
    private static readonly string[] DockerNeedles = ["docker-proxy", "com.docker", "docker desktop", "dockerd"];
    private static readonly string[] WslNeedles = ["wslhost", "wslservice", "wsl.exe", "\\wsl$"];

    public static string InferFramework(PortEntry entry)
    {
        // Most specific signals first; avoid allocating a combined haystack.
        if (ContainsAny(entry.ProcessName, entry.CommandLine, ViteNeedles)) return "Vite";
        if (ContainsAny(entry.ProcessName, entry.CommandLine, NextNeedles)) return "Next.js";
        if (ContainsAny(entry.ProcessName, entry.CommandLine, NuxtNeedles)) return "Nuxt";
        if (ContainsAny(entry.ProcessName, entry.CommandLine, DjangoNeedles)) return "Django";
        if (ContainsAny(entry.ProcessName, entry.CommandLine, FastApiNeedles)) return "FastAPI";
        if (ContainsAny(entry.ProcessName, entry.CommandLine, SpringNeedles)) return "Spring";
        if (ContainsAny(entry.ProcessName, entry.CommandLine, DotNetNeedles)) return ".NET";
        if (ContainsAny(entry.ProcessName, entry.CommandLine, entry.WorkingDirectory, entry.ExecutablePath, GoNeedles)) return "Go";
        if (ContainsAny(entry.ProcessName, entry.CommandLine, DockerNeedles)) return "Docker";
        if (ContainsAny(entry.ProcessName, entry.CommandLine, WslNeedles)) return "WSL";

        // Final fallback for Spring Boot executable JARs.
        if (entry.ProcessName.Contains("java", StringComparison.OrdinalIgnoreCase)
            && entry.CommandLine is { Length: > 0 }
            && IsSpringBootJarCommandLine(entry.CommandLine, entry.WorkingDirectory))
        {
            return "Spring";
        }

        return "";
    }

    private static bool ContainsAny(string? value, string?[] values, string[] needles)
    {
        foreach (var text in values)
        {
            if (ContainsAny(text, needles))
            {
                return true;
            }
        }

        return ContainsAny(value, needles);
    }

    private static bool ContainsAny(string? value1, string? value2, string[] needles)
        => ContainsAny(value1, needles) || ContainsAny(value2, needles);

    private static bool ContainsAny(string? value1, string? value2, string? value3, string? value4, string[] needles)
        => ContainsAny(value1, needles)
           || ContainsAny(value2, needles)
           || ContainsAny(value3, needles)
           || ContainsAny(value4, needles);

    private static bool ContainsAny(string? text, string[] needles)
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
