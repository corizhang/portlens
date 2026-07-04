namespace PortLens.Desktop.Services;

using System.Drawing.Text;
using System.Windows;
using System.Windows.Media;

public static class FontService
{
    public static IReadOnlyList<string> GetInstalledFontFamilies()
    {
        using var collection = new InstalledFontCollection();
        return collection.Families
            .Select(f => f.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
