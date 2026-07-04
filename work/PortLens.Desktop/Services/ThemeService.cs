using System.Windows;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using MdTheme = MaterialDesignThemes.Wpf.Theme;

namespace PortLens.Desktop.Services;

internal sealed class ThemeService
{
    private const string ThemeDictionaryUriPrefix = "pack://application:,,,/PortLens;component/Themes/PortLensColors.";

    public static void ApplyTheme(PortLens.Desktop.Settings.Theme theme)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var merged = app.Resources.MergedDictionaries;
        ResourceDictionary? existing = null;
        foreach (var dictionary in merged)
        {
            if (dictionary.Source?.OriginalString.StartsWith(ThemeDictionaryUriPrefix, StringComparison.OrdinalIgnoreCase) == true)
            {
                existing = dictionary;
                break;
            }
        }

        if (existing is not null)
        {
            merged.Remove(existing);
        }

        var source = new Uri($"{ThemeDictionaryUriPrefix}{theme}.xaml", UriKind.Absolute);
        merged.Add(new ResourceDictionary { Source = source });

        var bundledTheme = merged.OfType<BundledTheme>().FirstOrDefault();
        if (bundledTheme is not null)
        {
            bundledTheme.BaseTheme = theme == PortLens.Desktop.Settings.Theme.Dark
                ? BaseTheme.Dark
                : BaseTheme.Light;
        }

        try
        {
            var paletteHelper = new PaletteHelper();
            var mdTheme = paletteHelper.GetTheme();
            mdTheme.SetBaseTheme(theme == PortLens.Desktop.Settings.Theme.Dark ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(mdTheme);
        }
        catch
        {
            // Ignore if theme helper is unavailable during early initialization.
        }
    }
}
