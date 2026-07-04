using System.ComponentModel;
using PortLens.Desktop.Markup;
using PortLens.Desktop.Settings;

namespace PortLens.Desktop.Services;

public sealed class AppSettings : INotifyPropertyChanged
{
    private static readonly Lazy<AppSettings> _instance = new(() => new AppSettings());

    private AppSettings()
    {
    }

    public static AppSettings Instance => _instance.Value;

    private string _chineseFontFamily = "";
    private string _englishFontFamily = "";

    public string ChineseFontFamily
    {
        get => _chineseFontFamily;
        set
        {
            if (_chineseFontFamily == value)
            {
                return;
            }

            _chineseFontFamily = value;
            OnPropertyChanged(nameof(ChineseFontFamily));
            FontFamilyBinding.Instance.Refresh();
        }
    }

    public string EnglishFontFamily
    {
        get => _englishFontFamily;
        set
        {
            if (_englishFontFamily == value)
            {
                return;
            }

            _englishFontFamily = value;
            OnPropertyChanged(nameof(EnglishFontFamily));
            FontFamilyBinding.Instance.Refresh();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Apply(DesktopSettings settings)
    {
        ChineseFontFamily = settings.ChineseFontFamily;
        EnglishFontFamily = settings.EnglishFontFamily;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
