using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortLens.Desktop.Services;

namespace PortLens.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _serviceProvider = ServiceRegistration.BuildServiceProvider();
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        FlushLogs();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception, "Dispatcher unhandled exception");
        e.Handled = true;
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex, "AppDomain unhandled exception");
        }

        FlushLogs();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    private void LogException(Exception exception, string message)
    {
        try
        {
            var logger = _serviceProvider?.GetService<ILogger<App>>();
            logger?.LogError(exception, message);
        }
        catch
        {
            // Logging must not throw during shutdown.
        }
    }

    private void FlushLogs()
    {
        try
        {
            var provider = _serviceProvider?.GetService<FileLoggerProvider>();
            provider?.Flush(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Logging must not throw during shutdown.
        }
    }
}
