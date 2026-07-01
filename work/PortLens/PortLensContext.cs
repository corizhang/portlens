using PortLens.UI;

namespace PortLens;

internal sealed class PortLensContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly MainForm _mainForm;

    public PortLensContext()
    {
        _mainForm = new MainForm();
        _mainForm.FormClosing += (_, args) =>
        {
            if (args.CloseReason == CloseReason.UserClosing)
            {
                args.Cancel = true;
                _mainForm.Hide();
            }
        };

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PortLens",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                ToggleWindow();
            }
        };

        _mainForm.Show();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _mainForm.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open PortLens", null, (_, _) => ShowWindow());
        menu.Items.Add("Refresh", null, (_, _) => _mainForm.RefreshPorts());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _notifyIcon.Visible = false;
            Application.Exit();
        });
        return menu;
    }

    private void ToggleWindow()
    {
        if (_mainForm.Visible && _mainForm.WindowState != FormWindowState.Minimized)
        {
            _mainForm.Hide();
            return;
        }

        ShowWindow();
    }

    private void ShowWindow()
    {
        _mainForm.Show();
        _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.Activate();
        _mainForm.RefreshPorts();
    }
}
