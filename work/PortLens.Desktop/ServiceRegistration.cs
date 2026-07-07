using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortLens.Desktop.Services;
using PortLens.Desktop.ViewModels;
using PortLens.Services;

namespace PortLens.Desktop;

internal static class ServiceRegistration
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PortLens",
            "logs",
            $"portlens-{DateTimeOffset.Now:yyyyMMdd}.log");
        var fileLoggerProvider = new FileLoggerProvider(logPath);
        services.AddSingleton(fileLoggerProvider);
        services.AddLogging(builder => builder.AddProvider(fileLoggerProvider));

        services.AddSingleton<IProcessCommandLineReader, ProcessCommandLineReader>();
        services.AddSingleton<ProcessCurrentDirectoryReader>();
        services.AddSingleton<IProcessTreeReader, ProcessTreeReader>();
        services.AddSingleton<ProcessInspector>();
        services.AddSingleton<PortScanner>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>(serviceProvider => new MainWindow(serviceProvider));
        services.AddHttpClient<UpdateCheckService>();
        services.AddHttpClient<AutoUpdateService>();

        return services.BuildServiceProvider();
    }
}
