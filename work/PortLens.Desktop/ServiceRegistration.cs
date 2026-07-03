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
        services.AddLogging(builder => builder.AddProvider(new FileLoggerProvider(logPath)));

        services.AddSingleton<ProcessCommandLineReader>();
        services.AddSingleton<ProcessCurrentDirectoryReader>();
        services.AddSingleton<ProcessTreeReader>();
        services.AddSingleton<ProcessInspector>();
        services.AddSingleton<PortScanner>();
        services.AddSingleton<MainWindowViewModel>(serviceProvider =>
        {
            var scanner = serviceProvider.GetRequiredService<PortScanner>();
            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            var logger = serviceProvider.GetRequiredService<ILogger<MainWindowViewModel>>();
            return new MainWindowViewModel(scanner, message => mainWindow.ShowSnackbarAsync(message), logger);
        });
        services.AddTransient<PortEntryActionService>();
        services.AddTransient<TrayIconService>();

        services.AddSingleton<MainWindow>(serviceProvider => new MainWindow(serviceProvider));

        return services.BuildServiceProvider();
    }
}
