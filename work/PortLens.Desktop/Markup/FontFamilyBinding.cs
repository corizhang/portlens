using System.ComponentModel;
using System.Windows.Media;
using PortLens.Desktop.Services;

namespace PortLens.Desktop.Markup;

public sealed class FontFamilyBinding : INotifyPropertyChanged
{
    private static readonly Lazy<FontFamilyBinding> _instance = new(() => new FontFamilyBinding());

    private FontFamilyBinding()
    {
        LocalizationManager.Instance.CultureChanged += (_, _) => OnPropertyChanged(nameof(Current));
    }

    public static FontFamilyBinding Instance => _instance.Value;

    public System.Windows.Media.FontFamily Current
    {
        get
        {
            var isChinese = string.Equals(LocalizationManager.Instance.CurrentCulture.Name, "zh-Hans", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(LocalizationManager.Instance.CurrentCulture.Name, "zh-CN", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(LocalizationManager.Instance.CurrentCulture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase);
            var familyName = isChinese ? AppSettings.Instance.ChineseFontFamily : AppSettings.Instance.EnglishFontFamily;
            return FontService.ResolveFontFamily(familyName);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Refresh()
    {
        OnPropertyChanged(nameof(Current));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
