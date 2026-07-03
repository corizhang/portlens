using System.Text.Json;

namespace PortLens.Desktop.Settings;

internal sealed class DesktopSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public DesktopSettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsPath = Path.Combine(appData, "PortLens", "settings.json");
    }

    public DesktopSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new DesktopSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<DesktopSettings>(json, JsonOptions) ?? new DesktopSettings();
            return Migrate(settings);
        }
        catch
        {
            return new DesktopSettings();
        }
    }

    private static DesktopSettings Migrate(DesktopSettings settings)
    {
        if (settings.Version >= DesktopSettings.CurrentVersion)
        {
            return settings;
        }

        settings.Version = DesktopSettings.CurrentVersion;
        settings.EnabledFrameworks = [.. DesktopSettings.DefaultEnabledFrameworks];
        return settings;
    }

    public void Save(DesktopSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
