using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using MaterialDesignThemes.Wpf;
using PortLens.Desktop.Properties;
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
    private System.Windows.Controls.TextBlock? _statusCountText;
    private System.Windows.Controls.TextBlock? _statusSubText;
    private System.Windows.Controls.MenuItem? _pauseResumeItem;
    private System.Windows.Controls.MenuItem? _refreshItem;
    private System.Windows.Controls.MenuItem? _copySummaryItem;
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
            Text = Resources.GetString("TrayIconText"),
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
        if (_contextMenu is null)
        {
            _contextMenu = BuildContextMenu();
        }

        SetForegroundWindow(new WindowInteropHelper(_owner).EnsureHandle());

        var status = _getStatus();
        UpdateContextMenuState(status);

        var cursor = Forms.Cursor.Position;
        _contextMenu.PlacementTarget = _owner;
        _contextMenu.Placement = PlacementMode.AbsolutePoint;
        _contextMenu.HorizontalOffset = cursor.X;
        _contextMenu.VerticalOffset = cursor.Y;
        _contextMenu.IsOpen = true;
        _contextMenu.Focus();
    }

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu
        {
            MinWidth = 230,
            Padding = new Thickness(4),
            Focusable = true,
            StaysOpen = false,
            Background = FindBrush("PortLensBackgroundBrush"),
            BorderBrush = FindBrush("PortLensBorderBrush"),
            Foreground = FindBrush("PortLensGroupTitleBrush"),
            FontSize = 13
        };

        var statusHeader = BuildStatusHeader();
        menu.Items.Add(statusHeader);
        menu.Items.Add(BuildSeparator());
        menu.Items.Add(BuildMenuItem(Resources.GetString("TrayMenuOpenPortLens"), PackIconKind.OpenInApp, (_, _) => _showMainWindow()));
        menu.Items.Add(BuildMenuItem(Resources.GetString("TrayMenuSettings"), PackIconKind.CogOutline, async (_, _) => await _showSettingsAsync()));
        _pauseResumeItem = BuildMenuItem("", PackIconKind.Pause, (_, _) => _toggleScanPaused());
        menu.Items.Add(_pauseResumeItem);
        _refreshItem = BuildMenuItem(Resources.GetString("TrayMenuRefresh"), PackIconKind.Refresh, async (_, _) => await _refreshAsync());
        menu.Items.Add(_refreshItem);
        _copySummaryItem = BuildMenuItem(Resources.GetString("TrayMenuCopyPortSummary"), PackIconKind.ContentCopy, (_, _) => _copyPortSummary());
        menu.Items.Add(_copySummaryItem);
        menu.Items.Add(BuildSeparator());
        menu.Items.Add(BuildMenuItem(Resources.GetString("TrayMenuExit"), PackIconKind.ExitToApp, (_, _) => _exitApplication(), isDestructive: true));

        return menu;
    }

    private void UpdateContextMenuState(TrayStatusSnapshot status)
    {
        if (_statusCountText is not null)
        {
            _statusCountText.Text = status.ShowAllPorts
                ? Resources.GetString("TrayStatusPortsFormat", status.ServiceCount)
                : Resources.GetString("TrayStatusServicesFormat", status.ServiceCount);
        }

        if (_statusSubText is not null)
        {
            _statusSubText.Text = status.IsPaused
                ? Resources.GetString("TrayStatusPaused")
                : status.LastScanAt is { } lastScanAt
                    ? Resources.GetString("TrayStatusLastScanFormat", lastScanAt)
                    : Resources.GetString("TrayStatusNotScanned");
        }

        if (_pauseResumeItem is not null)
        {
            var isPaused = status.IsPaused;
            UpdateMenuItemHeader(_pauseResumeItem, isPaused ? Resources.GetString("TrayMenuResumeScanning") : Resources.GetString("TrayMenuPauseScanning"), isPaused ? PackIconKind.PlayOutline : PackIconKind.Pause);
        }

        if (_refreshItem is not null)
        {
            _refreshItem.IsEnabled = !status.IsPaused;
        }

        if (_copySummaryItem is not null)
        {
            _copySummaryItem.IsEnabled = status.ServiceCount > 0;
        }
    }

    private static void UpdateMenuItemHeader(System.Windows.Controls.MenuItem item, string text, PackIconKind iconKind)
    {
        if (item.Header is not StackPanel panel)
        {
            return;
        }

        foreach (var child in panel.Children)
        {
            if (child is PackIcon icon)
            {
                icon.Kind = iconKind;
            }
            else if (child is System.Windows.Controls.TextBlock textBlock)
            {
                textBlock.Text = text;
            }
        }
    }

    private Border BuildStatusHeader()
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(12, 8, 12, 8)
        };

        _statusCountText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("PortLensGroupTitleBrush")
        };
        panel.Children.Add(_statusCountText);

        _statusSubText = new TextBlock
        {
            FontSize = 12,
            Foreground = FindBrush("PortLensTextBrush"),
            Margin = new Thickness(0, 3, 0, 0)
        };
        panel.Children.Add(_statusSubText);

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
            Background = FindBrush("PortLensBorderBrush")
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
            ? FindBrush("PortLensDangerBrush")
            : FindBrush("PortLensSettingsIconBrush");
        var labelBrush = isDestructive
            ? FindBrush("PortLensDangerBrush")
            : FindBrush("PortLensGroupTitleBrush");

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

    private static System.Windows.Media.Brush FindBrush(string key)
    {
        return System.Windows.Application.Current.TryFindResource(key) as System.Windows.Media.Brush
            ?? new SolidColorBrush(WpfColor.FromRgb(124, 113, 106));
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}

internal sealed record TrayStatusSnapshot(int ServiceCount, DateTime? LastScanAt, bool IsPaused, bool ShowAllPorts);
