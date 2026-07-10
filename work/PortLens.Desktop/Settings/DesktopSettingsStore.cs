using System.Text.Json;
using PortLens.Models;
using PortLens.Services;

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
        if (settings.FrameworkRules.Count == 0)
        {
            settings.FrameworkRules = FrameworkRules.CloneDefaults().ToList();
        }

        settings.FrameworkRules = NormalizeRules(settings.FrameworkRules).ToList();

        if (settings.EnabledFrameworks.Count == 0)
        {
            settings.EnabledFrameworks = settings.FrameworkRules.Select(rule => rule.Name).ToList();
        }
        else
        {
            var valid = settings.FrameworkRules
                .Select(rule => rule.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            settings.EnabledFrameworks = settings.EnabledFrameworks
                .Where(valid.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (settings.EnabledFrameworks.Count == 0)
            {
                settings.EnabledFrameworks = settings.FrameworkRules.Select(rule => rule.Name).ToList();
            }
        }

        return settings;
    }

    private static IReadOnlyList<FrameworkRule> NormalizeRules(IEnumerable<FrameworkRule> rules)
    {
        var normalized = rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Name))
            .Select(rule =>
            {
                rule.Name = rule.Name.Trim();
                rule.ProcessNameKeywords = NormalizeKeywords(rule.ProcessNameKeywords).ToList();
                rule.CommandLineKeywords = NormalizeKeywords(rule.CommandLineKeywords).ToList();
                rule.PathKeywords = NormalizeKeywords(rule.PathKeywords).ToList();
                rule.DefaultPorts = rule.DefaultPorts
                    .Where(port => port is > 0 and <= 65535)
                    .Distinct()
                    .Order()
                    .ToList();
                return rule;
            })
            .GroupBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        return normalized.Count > 0 ? normalized : FrameworkRules.CloneDefaults();
    }

    private static IEnumerable<string> NormalizeKeywords(IEnumerable<string>? keywords)
        => keywords?
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            ?? [];

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
