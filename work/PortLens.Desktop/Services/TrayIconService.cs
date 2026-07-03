using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using MaterialDesignThemes.Wpf;
using WpfColor = System.Windows.Media.Color;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace PortLens.Desktop.Services;

internal sealed class TrayIconService : IDisposable
{
    private readonly Window _owner;
    private readonly Action _showMainWindow;
    private readonly Func<Task> _refreshAsync;
    private readonly Action _exitApplication;
    private readonly Forms.NotifyIcon _notifyIcon;
    private System.Windows.Controls.ContextMenu? _contextMenu;
    private bool _disposed;

    public TrayIconService(Window owner, Action showMainWindow, Func<Task> refreshAsync, Action exitApplication)
    {
        _owner = owner;
        _showMainWindow = showMainWindow;
        _refreshAsync = refreshAsync;
        _exitApplication = exitApplication;
        _notifyIcon = BuildNotifyIcon();
    }

    public bool Visible
    {
        get => _notifyIcon.Visible;
        set => _notifyIcon.Visible = value;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CloseContextMenu();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
    }

    private Forms.NotifyIcon BuildNotifyIcon()
    {
        var icon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "PortLens",
            Visible = true
        };
        icon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                _showMainWindow();
            }
        };
        icon.MouseUp += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Right)
            {
                _owner.Dispatcher.Invoke(ShowContextMenu);
            }
        };
        return icon;
    }

    private void ShowContextMenu()
    {
        CloseContextMenu();
        SetForegroundWindow(new WindowInteropHelper(_owner).EnsureHandle());

        var cursor = Forms.Cursor.Position;
        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = _owner,
            Placement = PlacementMode.AbsolutePoint,
            HorizontalOffset = cursor.X,
            VerticalOffset = cursor.Y,
            MinWidth = 196,
            Padding = new Thickness(4),
            Focusable = true,
            StaysOpen = false,
            Background = new SolidColorBrush(WpfColor.FromRgb(247, 242, 236)),
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(226, 216, 206)),
            Foreground = new SolidColorBrush(WpfColor.FromRgb(45, 38, 34)),
            FontSize = 13
        };

        menu.Items.Add(BuildMenuItem("Open PortLens", PackIconKind.OpenInApp, (_, _) => _showMainWindow()));
        menu.Items.Add(BuildMenuItem("Refresh", PackIconKind.Refresh, async (_, _) => await _refreshAsync()));
        menu.Items.Add(new Separator
        {
            Margin = new Thickness(6, 4, 6, 4),
            Background = new SolidColorBrush(WpfColor.FromRgb(226, 216, 206))
        });
        menu.Items.Add(BuildMenuItem("Exit", PackIconKind.ExitToApp, (_, _) => _exitApplication(), isDestructive: true));

        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_contextMenu, menu))
            {
                _contextMenu = null;
            }
        };
        _contextMenu = menu;
        menu.IsOpen = true;
        menu.Focus();
    }

    private void CloseContextMenu()
    {
        if (_contextMenu is null)
        {
            return;
        }

        _contextMenu.IsOpen = false;
        _contextMenu = null;
    }

    private static System.Windows.Controls.MenuItem BuildMenuItem(string text, PackIconKind iconKind, RoutedEventHandler click, bool isDestructive = false)
    {
        var foreground = isDestructive
            ? new SolidColorBrush(WpfColor.FromRgb(190, 32, 32))
            : new SolidColorBrush(WpfColor.FromRgb(95, 55, 190));
        var labelBrush = isDestructive
            ? new SolidColorBrush(WpfColor.FromRgb(190, 32, 32))
            : new SolidColorBrush(WpfColor.FromRgb(45, 38, 34));

        var header = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(new PackIcon
        {
            Kind = iconKind,
            Width = 17,
            Height = 17,
            Foreground = foreground,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = labelBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var item = new System.Windows.Controls.MenuItem
        {
            Header = header,
            Height = 34,
            Padding = new Thickness(12, 0, 12, 0)
        };
        item.Click += click;
        return item;
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/portlens-icon.ico"));
        return resource is not null
            ? new System.Drawing.Icon(resource.Stream)
            : System.Drawing.SystemIcons.Application;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
