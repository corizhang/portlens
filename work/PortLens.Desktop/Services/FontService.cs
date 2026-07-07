namespace PortLens.Desktop.Services;

using System.Drawing.Text;
using System.Windows;
using System.Windows.Media;

public static class FontService
{
    private static readonly Lazy<IReadOnlyList<string>> _installedFontFamilies = new(() =>
    {
        using var collection = new InstalledFontCollection();
        return collection.Families
            .Select(f => f.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    });

    public static IReadOnlyList<string> GetInstalledFontFamilies()
    {
        return _installedFontFamilies.Value;
    }

    public static FontFamily ResolveFontFamily(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return SystemFonts.MessageFontFamily;
        }

        try
        {
            return new FontFamily(name);
        }
        catch
        {
            return SystemFonts.MessageFontFamily;
        }
    }
}
