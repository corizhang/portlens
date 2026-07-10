using PortLens.Models;

namespace PortLens.Services;

public static class FrameworkRules
{
    public static readonly IReadOnlyList<FrameworkRule> Defaults =
    [
        new()
        {
            Name = "Vite",
            CommandLineKeywords = ["vite", "vite.js", "vite\\bin", "vite/bin"],
            DefaultPorts = [5173, 5174]
        },
        new()
        {
            Name = "Next.js",
            CommandLineKeywords = ["next dev", "next start", "next-server", "\\next\\", "/next/"],
            DefaultPorts = [3000, 3001]
        },
        new()
        {
            Name = "Nuxt",
            CommandLineKeywords = ["nuxt", "nuxi"],
            DefaultPorts = [3000]
        },
        new()
        {
            Name = "Django",
            CommandLineKeywords = ["manage.py runserver", "django.core", "daphne", "runserver"],
            DefaultPorts = [8000]
        },
        new()
        {
            Name = "FastAPI",
            ProcessNameKeywords = ["uvicorn", "hypercorn"],
            CommandLineKeywords = ["fastapi", "uvicorn", "hypercorn"],
            DefaultPorts = [8000]
        },
        new()
        {
            Name = "Spring",
            CommandLineKeywords = ["spring-boot", "springframework", "org.springframework.boot"],
            DefaultPorts = [8080]
        },
        new()
        {
            Name = ".NET",
            CommandLineKeywords = ["dotnet", "kestrel", "aspnetcore"],
            DefaultPorts = [5000, 5001, 7000, 7001]
        },
        new()
        {
            Name = "Go",
            ProcessNameKeywords = ["go"],
            CommandLineKeywords = ["go run", "air.toml", "\\air.exe", "/air", "gin-gonic", "fiber", "echo/v4", "go-build", "\\go.exe", "/go "],
            PathKeywords = ["go-build"],
            DefaultPorts = [8080]
        },
        new()
        {
            Name = "Docker",
            ProcessNameKeywords = ["docker-proxy"],
            CommandLineKeywords = ["docker-proxy", "com.docker", "docker desktop", "dockerd"]
        },
        new()
        {
            Name = "WSL",
            ProcessNameKeywords = ["wslhost", "wslservice", "wsl.exe"],
            CommandLineKeywords = ["wslhost", "wslservice", "wsl.exe", "\\wsl$"],
            PathKeywords = ["\\wsl$"]
        }
    ];

    public static IReadOnlyList<FrameworkRule> CloneDefaults()
        => Defaults.Select(rule => rule.Clone()).ToList();

    public static string[] DefaultNames()
        => Defaults.Select(rule => rule.Name).ToArray();
}
