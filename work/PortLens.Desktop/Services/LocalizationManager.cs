using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using PortLens.Desktop.Properties;

namespace PortLens.Desktop.Services;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());

    private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("en-US");

    private LocalizationManager()
    {
    }

    public static LocalizationManager Instance => _instance.Value;

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        private set
        {
            if (Equals(_currentCulture, value))
            {
                return;
            }

            _currentCulture = value;
            Resources.Culture = value;
            Thread.CurrentThread.CurrentUICulture = value;
            Thread.CurrentThread.CurrentCulture = value;
            OnPropertyChanged(string.Empty);
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string this[string key] => Resources.GetString(key);

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CultureChanged;

    public void ChangeCulture(string name)
    {
        CurrentCulture = CultureInfo.GetCultureInfo(name);
    }

    public string GetString(string key)
    {
        return Resources.GetString(key);
    }

    public string GetString(string key, params object?[] args)
    {
        return Resources.GetString(key, args);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
