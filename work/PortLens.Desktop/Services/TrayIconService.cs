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
    private readonly Func<Task> _showSettingsAsync;
    private readonly Action _toggleScanPaused;
    private readonly Action _copyPortSummary;
    private readonly Func<TrayStatusSnapshot> _getStatus;
    private readonly Action _exitApplication;
    private readonly Forms.NotifyIcon _notifyIcon;
    private System.Windows.Controls.ContextMenu? _contextMenu;
    private bool _disposed;

    public TrayIconService(
        Window owner,
        Action showMainWindow,
        Func<Task> refreshAsync,
        Func<Task> showSettingsAsync,
        Action toggleScanPaused,
        Action copyPortSummary,
        Func<TrayStatusSnapshot> getStatus,
        Action exitApplication)
    {
        _owner = owner;
        _showMainWindow = showMainWindow;
        _refreshAsync = refreshAsync;
        _showSettingsAsync = showSettingsAsync;
        _toggleScanPaused = toggleScanPaused;
        _copyPortSummary = copyPortSummary;
        _getStatus = getStatus;
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
            MinWidth = 230,
            Padding = new Thickness(4),
            Focusable = true,
            StaysOpen = false,
            Background = new SolidColorBrush(WpfColor.FromRgb(247, 242, 236)),
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(226, 216, 206)),
            Foreground = new SolidColorBrush(WpfColor.FromRgb(45, 38, 34)),
            FontSize = 13
        };

        var status = _getStatus();
        menu.Items.Add(BuildStatusHeader(status));
        menu.Items.Add(BuildSeparator());
        menu.Items.Add(BuildMenuItem("Open PortLens", PackIconKind.OpenInApp, (_, _) => _showMainWindow()));
        menu.Items.Add(BuildMenuItem("Settings", PackIconKind.CogOutline, async (_, _) => await _showSettingsAsync()));
        menu.Items.Add(BuildMenuItem(
            status.IsPaused ? "Resume scanning" : "Pause scanning",
            status.IsPaused ? PackIconKind.PlayOutline : PackIconKind.Pause,
            (_, _) => _toggleScanPaused()));
        menu.Items.Add(BuildMenuItem("Refresh", PackIconKind.Refresh, async (_, _) => await _refreshAsync(), isEnabled: !status.IsPaused));
        menu.Items.Add(BuildMenuItem("Copy port summary", PackIconKind.ContentCopy, (_, _) => _copyPortSummary(), isEnabled: status.ServiceCount > 0));
        menu.Items.Add(BuildSeparator());
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

    private static Border BuildStatusHeader(TrayStatusSnapshot status)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(12, 8, 12, 8)
        };
        panel.Children.Add(new TextBlock
        {
            Text = status.ShowAllPorts
                ? $"{status.ServiceCount} listening ports"
                : $"{status.ServiceCount} development services",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(45, 38, 34))
        });
        panel.Children.Add(new TextBlock
        {
            Text = status.IsPaused
                ? "Scanning paused"
                : status.LastScanAt is { } lastScanAt
                    ? $"Last scan {lastScanAt:HH:mm:ss}"
                    : "Not scanned yet",
            FontSize = 12,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
            Margin = new Thickness(0, 3, 0, 0)
        });

        return new Border
        {
            Child = panel
        };
    }

    private static Separator BuildSeparator()
    {
        return new Separator
        {
            Margin = new Thickness(6, 4, 6, 4),
            Background = new SolidColorBrush(WpfColor.FromRgb(226, 216, 206))
        };
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

    private static System.Windows.Controls.MenuItem BuildMenuItem(
        string text,
        PackIconKind iconKind,
        RoutedEventHandler click,
        bool isDestructive = false,
        bool isEnabled = true)
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
            Padding = new Thickness(12, 0, 12, 0),
            IsEnabled = isEnabled
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

internal sealed record TrayStatusSnapshot(int ServiceCount, DateTime? LastScanAt, bool IsPaused, bool ShowAllPorts);
