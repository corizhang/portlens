using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PortLens.Desktop.Services;

public sealed class UpdateCheckService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateCheckService> _logger;
    private const string LatestReleaseUrl = "https://api.github.com/repos/corizhang/portlens/releases/latest";

    public UpdateCheckService(HttpClient httpClient, ILogger<UpdateCheckService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PortLens-UpdateCheck");
    }

    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(LatestReleaseUrl, cancellationToken);
            if (release?.TagName is null || release.HtmlUrl is null)
            {
                return null;
            }

            var currentVersion = GetCurrentVersion();
            var latestVersion = ParseVersion(release.TagName);
            var isNewer = latestVersion > currentVersion;

            var msiUrl = release.Assets?
                .FirstOrDefault(a => a.Name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true)?
                .BrowserDownloadUrl;

            return new UpdateInfo(
                isNewer,
                release.TagName,
                release.HtmlUrl,
                release.Body ?? string.Empty,
                currentVersion.ToString(3),
                latestVersion.ToString(3),
                msiUrl ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates.");
            return null;
        }
    }

    private static Version GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var versionPart = informational.Split('+')[0];
            if (Version.TryParse(versionPart, out var parsed))
            {
                return new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
            }
        }

        return assembly.GetName().Version ?? new Version(1, 0, 0);
    }

    private static Version ParseVersion(string tag)
    {
        var trimmed = tag.TrimStart('v', 'V');
        if (Version.TryParse(trimmed, out var parsed))
        {
            return parsed;
        }

        return new Version(1, 0, 0);
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public IReadOnlyList<GitHubReleaseAsset>? Assets { get; set; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}

public sealed record UpdateInfo(
    bool IsUpdateAvailable,
    string LatestTag,
    string ReleaseUrl,
    string ReleaseNotes,
    string CurrentVersion,
    string LatestVersion,
    string MsiDownloadUrl);
