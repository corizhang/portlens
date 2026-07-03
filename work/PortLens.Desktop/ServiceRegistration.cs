using Microsoft.Extensions.DependencyInjection;
using PortLens.Desktop.Services;
using PortLens.Desktop.ViewModels;
using PortLens.Services;

namespace PortLens.Desktop;

internal static class ServiceRegistration
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<PortScanner>();
        services.AddSingleton<MainWindowViewModel>(serviceProvider =>
        {
            var scanner = serviceProvider.GetRequiredService<PortScanner>();
            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            return new MainWindowViewModel(scanner, message => mainWindow.ShowSnackbarAsync(message));
        });
        services.AddTransient<PortEntryActionService>();
        services.AddTransient<TrayIconService>();

        services.AddSingleton<MainWindow>(serviceProvider => new MainWindow(serviceProvider));

        return services.BuildServiceProvider();
    }
}
