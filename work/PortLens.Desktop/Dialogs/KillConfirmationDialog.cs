using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using PortLens.Desktop.ViewModels;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace PortLens.Desktop.Dialogs;

internal static class KillConfirmationDialog
{
    public static async Task<bool> ShowAsync(PortEntryViewModel entry, int childProcessCount)
    {
        var panel = new StackPanel
        {
            Width = 420,
            Margin = new Thickness(24)
        };

        var header = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 16)
        };
        header.Children.Add(new PackIcon
        {
            Kind = PackIconKind.AlertCircleOutline,
            Width = 30,
            Height = 30,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(179, 38, 30)),
            Margin = new Thickness(0, 0, 12, 0)
        });
        header.Children.Add(new TextBlock
        {
            Text = "Confirm kill",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 27, 24)),
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(header);

        panel.Children.Add(new TextBlock
        {
            Text = childProcessCount > 0
                ? $"Kill PID {entry.ProcessId} ({entry.ProcessName}) and {childProcessCount} child process(es)?"
                : $"Kill PID {entry.ProcessId} ({entry.ProcessName})?",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(72, 63, 58)),
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(new TextBlock
        {
            Text = entry.DisplayName,
            FontSize = 13,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
            Margin = new Thickness(0, 0, 0, 20)
        });

        var actions = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };
        var cancel = new WpfButton
        {
            Content = "Cancel",
            Style = FindStyle("MaterialDesignOutlinedButton"),
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 88,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = false
        };
        var kill = new WpfButton
        {
            Content = "Kill",
            Style = FindStyle("MaterialDesignOutlinedButton"),
            Foreground = new SolidColorBrush(WpfColor.FromRgb(179, 38, 30)),
            MinWidth = 88,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = true
        };
        actions.Children.Add(cancel);
        actions.Children.Add(kill);
        panel.Children.Add(actions);

        var result = await DialogHost.Show(panel, "RootDialog");
        return result is true;
    }

    private static Style FindStyle(string key)
    {
        return (Style)System.Windows.Application.Current.FindResource(key);
    }
}
