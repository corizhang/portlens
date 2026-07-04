using System.Globalization;
using System.Windows.Data;
using PortLens.Desktop.Properties;

namespace PortLens.Desktop.Converters;

public sealed class LocalizedStringFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = parameter?.ToString() ?? string.Empty;
        var format = Resources.GetString(key);
        return value is null ? format : string.Format(culture, format, value);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
