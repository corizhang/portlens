namespace PortLens.Desktop.Properties;

using System.Globalization;
using System.Resources;

#pragma warning disable CS8669

internal static class Resources
{
    private static readonly ResourceManager ResourceManager = new(
        "PortLens.Desktop.Properties.Resources",
        typeof(Resources).Assembly);

    public static CultureInfo Culture { get; set; } = CultureInfo.CurrentUICulture;

    public static string GetString(string name)
    {
        return ResourceManager.GetString(name, Culture) ?? name;
    }

    public static string GetString(string name, params object[] args)
    {
        var format = GetString(name);
        return args.Length == 0 ? format : string.Format(Culture, format, args);
    }
}

#pragma warning restore CS8669
