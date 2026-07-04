using System.Text.RegularExpressions;
using PortLens.Models;

namespace PortLens.Services;

public static class FrameworkDetector
{
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
        return "";
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.Ordinal));
    }
}
