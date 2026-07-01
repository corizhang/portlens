using PortLens.Models;

namespace PortLens.UI;

internal sealed class PortCardControl : UserControl
{
    private PortEntry _entry;
    private readonly Panel _detailsPanel = new();
    private readonly Button _toggleButton = new();
    private readonly Label _portLabel = new();
    private readonly Label _nameLabel = new();
    private readonly Label _metaLabel = new();
    private readonly Label _riskLabel = new();
    private readonly Label _pidValueLabel = new();
    private readonly Label _commandValueLabel = new();
    private readonly Label _directoryValueLabel = new();
    private bool _expanded;

    public event EventHandler<PortEntry>? OpenRequested;
    public event EventHandler<PortEntry>? CopyUrlRequested;
    public event EventHandler<PortEntry>? CopyPidRequested;
    public event EventHandler<PortEntry>? OpenDirectoryRequested;
    public event EventHandler<PortEntry>? KillRequested;

    public PortCardControl(PortEntry entry)
    {
        _entry = entry;
        Height = 92;
        MinimumSize = new Size(520, 92);
        BackColor = Color.Transparent;
        DoubleBuffered = true;

        BuildCard();
        ContextMenuStrip = BuildMenu();
    }

    public void UpdateEntry(PortEntry entry)
    {
        _entry = entry;
        _portLabel.Text = $":{_entry.LocalPort}";
        _nameLabel.Text = _entry.DisplayName;
        _metaLabel.Text = BuildMetaText();
        _riskLabel.Text = _entry.RiskLevel;
        _riskLabel.ForeColor = RiskColor();
        _pidValueLabel.Text = _entry.ProcessId.ToString();
        _commandValueLabel.Text = Shorten(_entry.CommandLine ?? _entry.ProcessName, 86);
        _directoryValueLabel.Text = Shorten(_entry.WorkingDirectory ?? _entry.ExecutablePath ?? "", 86);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var shadow = new SolidBrush(Color.FromArgb(26, 80, 60, 40));
        using var fill = new SolidBrush(Color.FromArgb(253, 250, 244));
        using var border = new Pen(Color.FromArgb(226, 216, 205));
        var rect = new Rectangle(1, 1, Width - 5, Height - 5);
        var shadowRect = new Rectangle(3, 4, Width - 5, Height - 5);
        e.Graphics.FillRoundedRectangle(shadow, shadowRect, 11);
        e.Graphics.FillRoundedRectangle(fill, rect, 11);
        e.Graphics.DrawRoundedRectangle(border, rect, 11);
    }

    private void BuildCard()
    {
        _portLabel.Text = $":{_entry.LocalPort}";
        _portLabel.Font = new Font("Consolas", 13F, FontStyle.Bold);
        _portLabel.ForeColor = Color.FromArgb(70, 126, 202);
        _portLabel.Location = new Point(18, 23);
        _portLabel.Size = new Size(78, 28);

        _nameLabel.Text = _entry.DisplayName;
        _nameLabel.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
        _nameLabel.ForeColor = Color.FromArgb(44, 39, 36);
        _nameLabel.Location = new Point(106, 16);
        _nameLabel.Size = new Size(Width - 310, 26);
        _nameLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

        _metaLabel.Text = BuildMetaText();
        _metaLabel.Font = new Font("Segoe UI", 8.5F);
        _metaLabel.ForeColor = Color.FromArgb(112, 103, 96);
        _metaLabel.Location = new Point(108, 43);
        _metaLabel.Size = new Size(Width - 270, 22);
        _metaLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

        _riskLabel.Text = _entry.RiskLevel;
        _riskLabel.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _riskLabel.ForeColor = RiskColor();
        _riskLabel.Location = new Point(108, 65);
        _riskLabel.Size = new Size(100, 20);

        var openButton = BuildActionButton("Open", Width - 212, 26, 60);
        openButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        openButton.Click += (_, _) => OpenRequested?.Invoke(this, _entry);

        var copyButton = BuildActionButton("Copy", Width - 146, 26, 58);
        copyButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        copyButton.Click += (_, _) => CopyUrlRequested?.Invoke(this, _entry);

        _toggleButton.Text = ">";
        _toggleButton.FlatStyle = FlatStyle.Flat;
        _toggleButton.BackColor = Color.FromArgb(250, 247, 241);
        _toggleButton.ForeColor = Color.FromArgb(101, 93, 86);
        _toggleButton.Location = new Point(Width - 72, 26);
        _toggleButton.Size = new Size(38, 30);
        _toggleButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _toggleButton.FlatAppearance.BorderColor = Color.FromArgb(224, 216, 206);
        _toggleButton.Click += (_, _) => ToggleDetails();

        _detailsPanel.Visible = false;
        _detailsPanel.Location = new Point(108, 90);
        _detailsPanel.Size = new Size(Width - 144, 104);
        _detailsPanel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        _detailsPanel.BackColor = Color.Transparent;
        BuildDetails();

        Controls.Add(_portLabel);
        Controls.Add(_nameLabel);
        Controls.Add(_metaLabel);
        Controls.Add(_riskLabel);
        Controls.Add(openButton);
        Controls.Add(copyButton);
        Controls.Add(_toggleButton);
        Controls.Add(_detailsPanel);
    }

    private void BuildDetails()
    {
        _detailsPanel.Controls.Clear();
        AddDetail("PID", _pidValueLabel, _entry.ProcessId.ToString(), 0);
        AddDetail("Command", _commandValueLabel, Shorten(_entry.CommandLine ?? _entry.ProcessName, 86), 26);
        AddDetail("Directory", _directoryValueLabel, Shorten(_entry.WorkingDirectory ?? _entry.ExecutablePath ?? "", 86), 52);
    }

    private void AddDetail(string key, Label valueLabel, string value, int top)
    {
        var keyLabel = new Label
        {
            Text = key,
            ForeColor = Color.FromArgb(160, 150, 140),
            Location = new Point(0, top),
            Size = new Size(86, 20)
        };
        valueLabel.Text = value;
        valueLabel.ForeColor = Color.FromArgb(78, 70, 64);
        valueLabel.Font = new Font("Segoe UI", 8.5F);
        valueLabel.Location = new Point(92, top);
        valueLabel.Size = new Size(_detailsPanel.Width - 100, 20);
        valueLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        _detailsPanel.Controls.Add(keyLabel);
        _detailsPanel.Controls.Add(valueLabel);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open localhost", null, (_, _) => OpenRequested?.Invoke(this, _entry));
        menu.Items.Add("Copy URL", null, (_, _) => CopyUrlRequested?.Invoke(this, _entry));
        menu.Items.Add("Copy PID", null, (_, _) => CopyPidRequested?.Invoke(this, _entry));
        menu.Items.Add("Open directory", null, (_, _) => OpenDirectoryRequested?.Invoke(this, _entry));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Kill process tree", null, (_, _) => KillRequested?.Invoke(this, _entry));
        return menu;
    }

    private Button BuildActionButton(string text, int left, int top, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 30,
            Location = new Point(left, top),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(76, 68, 62)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(224, 216, 206);
        return button;
    }

    private void ToggleDetails()
    {
        _expanded = !_expanded;
        _detailsPanel.Visible = _expanded;
        Height = _expanded ? 202 : 92;
        _toggleButton.Text = _expanded ? "v" : ">";
        Invalidate();
    }

    private string BuildMetaText()
    {
        var framework = string.IsNullOrWhiteSpace(_entry.Framework) ? _entry.ProcessName : _entry.Framework;
        var cpu = _entry.CpuPercent.HasValue ? $"{_entry.CpuPercent:0.0}% CPU" : "CPU ...";
        var memory = _entry.MemoryBytes.HasValue ? $"{_entry.MemoryBytes.Value / 1024 / 1024} MB" : "";
        var uptime = FormatUptime(_entry.Uptime);
        return string.Join("   ", new[] { framework, uptime, cpu, memory, _entry.LocalAddress }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private Color RiskColor()
    {
        return _entry.RiskLevel == "Medium" ? Color.FromArgb(180, 111, 31) : Color.FromArgb(54, 168, 86);
    }

    private static string FormatUptime(TimeSpan? uptime)
    {
        if (uptime is null)
        {
            return "";
        }

        if (uptime.Value.TotalDays >= 1)
        {
            return $"{(int)uptime.Value.TotalDays}d {uptime.Value.Hours}h";
        }

        if (uptime.Value.TotalHours >= 1)
        {
            return $"{(int)uptime.Value.TotalHours}h {uptime.Value.Minutes}m";
        }

        return $"{uptime.Value.Minutes}m";
    }

    private static string Shorten(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= max)
        {
            return value;
        }

        return value[..Math.Max(0, max - 3)] + "...";
    }
}

internal sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
{
    private const int WM_SETREDRAW = 0x000B;

    public BufferedFlowLayoutPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    public void SetRedraw(bool enabled)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        SendMessage(Handle, WM_SETREDRAW, enabled ? 1 : 0, 0);
        if (enabled)
        {
            Invalidate(true);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = RoundedPath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = RoundedPath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
