using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace PortLens.Desktop.Services;

public sealed class AutoUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AutoUpdateService> _logger;

    public AutoUpdateService(HttpClient httpClient, ILogger<AutoUpdateService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> DownloadMsiAsync(
        UpdateInfo updateInfo,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(updateInfo.MsiDownloadUrl))
        {
            _logger.LogWarning("No MSI download URL available.");
            return null;
        }

        var fileName = $"PortLens-v{updateInfo.LatestVersion}-win-x64.msi";
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);

        try
        {
            using var response = await _httpClient.GetAsync(
                updateInfo.MsiDownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                8192,
                true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;
            while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                totalRead += read;

                if (totalBytes > 0 && progress != null)
                {
                    progress.Report((double)totalRead / totalBytes);
                }
            }

            _logger.LogInformation("Downloaded MSI to {Path}", tempPath);
            return tempPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download MSI from {Url}", updateInfo.MsiDownloadUrl);
            return null;
        }
    }

    public void StartUpdateAndExit(string msiPath)
    {
        var batchPath = Path.Combine(Path.GetTempPath(), $"PortLens-update-{Guid.NewGuid():N}.cmd");
        const string installedExePath = @"C:\Program Files\PortLens\PortLens.exe";

        var batchContent = $$"""
@echo off
timeout /t 2 /nobreak >nul
start /wait "" msiexec /i "{{msiPath}}" /qn /norestart
if exist "{{installedExePath}}" (
    start "" "{{installedExePath}}"
)
del "%~f0"
""";

        File.WriteAllText(batchPath, batchContent);

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batchPath}\"",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            Verb = "runas",
            CreateNoWindow = true
        };

        Process.Start(startInfo);
        _logger.LogInformation("Started update installer. Exiting application.");
    }
}
