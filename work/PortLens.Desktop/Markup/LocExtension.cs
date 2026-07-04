using System.Windows.Data;
using System.Windows.Markup;
using PortLens.Desktop.Services;

namespace PortLens.Desktop.Markup;

public class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new System.Windows.Data.Binding($"[{Key}]")
        {
            Source = LocalizationManager.Instance,
            Mode = System.Windows.Data.BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
