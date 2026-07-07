using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
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

        services.AddHttpClient<UpdateCheckService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }).AddPolicyHandler(CreateRetryPolicy(retryCount: 2));

        services.AddHttpClient<AutoUpdateService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        }).AddPolicyHandler(CreateRetryPolicy(retryCount: 1));

        return services.BuildServiceProvider();
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(int retryCount)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)));
    }
}
