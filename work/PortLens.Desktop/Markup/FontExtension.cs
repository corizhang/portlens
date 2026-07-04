using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using PortLens.Desktop.Markup;

using Binding = System.Windows.Data.Binding;

namespace PortLens.Desktop.Markup;

public class FontExtension : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(FontFamilyBinding.Current))
        {
            Source = FontFamilyBinding.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
