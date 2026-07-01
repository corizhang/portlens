using System.Diagnostics;
using PortLens.Models;
using PortLens.Services;

namespace PortLens.UI;

internal sealed class MainForm : Form
{
    private readonly PortScanner _scanner = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly TextBox _searchBox = new();
    private readonly CheckBox _showAllBox = new();
    private readonly Label _statusLabel = new();
    private readonly BufferedFlowLayoutPanel _cardsPanel = new();
    private readonly Panel _emptyPanel = new();
    private readonly Dictionary<string, PortCardControl> _cardsByKey = new();
    private IReadOnlyList<PortEntry> _entries = Array.Empty<PortEntry>();
    private bool _isRefreshing;

    public MainForm()
    {
        Text = "PortLens";
        Width = 760;
        Height = 720;
        MinimumSize = new Size(620, 520);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 240, 232);
        Font = new Font("Segoe UI", 9F);
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        Controls.Add(BuildBody());
        Controls.Add(BuildHeader());

        _refreshTimer.Interval = 5000;
        _refreshTimer.Tick += (_, _) => RefreshPorts();
        _refreshTimer.Start();

        Shown += (_, _) => RefreshPorts();
        Resize += (_, _) => ResizeCards();
    }

    public async void RefreshPorts()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        var showAll = _showAllBox.Checked;
        _statusLabel.Text = "Scanning in background...";

        try
        {
            var entries = await Task.Run(() => _scanner.Scan(showAll));
            if (IsDisposed)
            {
                return;
            }

            _entries = entries;
            RenderEntries();
            _statusLabel.Text = showAll
                ? $"{_entries.Count} local listening ports - {DateTime.Now:HH:mm:ss}"
                : $"{_entries.Count} development services - {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 142,
            Padding = new Padding(22, 18, 22, 12),
            BackColor = Color.FromArgb(250, 247, 241)
        };

        var brand = new Label
        {
            Text = "PORTLENS",
            AutoSize = true,
            ForeColor = Color.FromArgb(172, 92, 98),
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            Location = new Point(25, 18)
        };

        var title = new Label
        {
            Text = "Ports",
            AutoSize = true,
            Font = new Font("Georgia", 31F, FontStyle.Bold),
            ForeColor = Color.FromArgb(36, 31, 30),
            Location = new Point(22, 34)
        };

        _searchBox.PlaceholderText = "Search localhost services...";
        _searchBox.Width = 330;
        _searchBox.Height = 30;
        _searchBox.Location = new Point(190, 58);
        _searchBox.BorderStyle = BorderStyle.FixedSingle;
        _searchBox.TextChanged += (_, _) => RenderEntries();

        _showAllBox.Text = "Show system ports";
        _showAllBox.AutoSize = true;
        _showAllBox.Location = new Point(540, 62);
        _showAllBox.CheckedChanged += (_, _) => RefreshPorts();

        var refreshButton = new Button
        {
            Text = "Refresh",
            Width = 88,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Location = new Point(540, 94)
        };
        refreshButton.FlatAppearance.BorderColor = Color.FromArgb(224, 216, 206);
        refreshButton.Click += (_, _) => RefreshPorts();

        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.FromArgb(116, 107, 101);
        _statusLabel.Location = new Point(26, 108);
        _statusLabel.Text = "Scanning...";

        panel.Controls.Add(brand);
        panel.Controls.Add(title);
        panel.Controls.Add(_searchBox);
        panel.Controls.Add(_showAllBox);
        panel.Controls.Add(refreshButton);
        panel.Controls.Add(_statusLabel);
        return panel;
    }

    private Control BuildBody()
    {
        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 16, 22, 22),
            BackColor = Color.FromArgb(245, 240, 232)
        };

        _cardsPanel.Dock = DockStyle.Fill;
        _cardsPanel.AutoScroll = true;
        _cardsPanel.FlowDirection = FlowDirection.TopDown;
        _cardsPanel.WrapContents = false;
        _cardsPanel.BackColor = Color.Transparent;
        _cardsPanel.Padding = new Padding(0, 0, 8, 0);

        _emptyPanel.Dock = DockStyle.Fill;
        _emptyPanel.BackColor = Color.Transparent;
        _emptyPanel.Visible = false;
        var emptyText = new Label
        {
            Text = "No development services found",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 94, 88),
            Location = new Point(20, 28)
        };
        var emptyHint = new Label
        {
            Text = "Start a local dev server, or enable Show system ports.",
            AutoSize = true,
            ForeColor = Color.FromArgb(132, 124, 116),
            Location = new Point(22, 58)
        };
        _emptyPanel.Controls.Add(emptyText);
        _emptyPanel.Controls.Add(emptyHint);

        body.Controls.Add(_cardsPanel);
        body.Controls.Add(_emptyPanel);
        return body;
    }

    private void RenderEntries()
    {
        var query = _searchBox.Text.Trim();
        var rows = _entries.Where(entry => Matches(entry, query)).ToList();
        var visibleKeys = rows.Select(CardKey).ToHashSet(StringComparer.Ordinal);

        _cardsPanel.SetRedraw(false);
        _cardsPanel.SuspendLayout();
        _emptyPanel.Visible = rows.Count == 0;
        _cardsPanel.Visible = rows.Count > 0;

        foreach (var staleKey in _cardsByKey.Keys.Where(key => !visibleKeys.Contains(key)).ToList())
        {
            var staleCard = _cardsByKey[staleKey];
            _cardsPanel.Controls.Remove(staleCard);
            _cardsByKey.Remove(staleKey);
            staleCard.Dispose();
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var entry = rows[index];
            var key = CardKey(entry);
            if (!_cardsByKey.TryGetValue(key, out var card))
            {
                card = CreateCard(entry);
                _cardsByKey[key] = card;
                _cardsPanel.Controls.Add(card);
            }
            else
            {
                card.UpdateEntry(entry);
            }

            card.Width = Math.Max(520, _cardsPanel.ClientSize.Width - 28);
            _cardsPanel.Controls.SetChildIndex(card, index);
        }

        _cardsPanel.ResumeLayout();
        _cardsPanel.SetRedraw(true);
        _cardsPanel.Invalidate();
    }

    private PortCardControl CreateCard(PortEntry entry)
    {
        var card = new PortCardControl(entry)
        {
            Width = Math.Max(520, _cardsPanel.ClientSize.Width - 28),
            Margin = new Padding(0, 0, 0, 10)
        };
        card.OpenRequested += (_, selected) => OpenUrl(selected);
        card.CopyUrlRequested += (_, selected) => Clipboard.SetText(selected.Url);
        card.CopyPidRequested += (_, selected) => Clipboard.SetText(selected.ProcessId.ToString());
        card.OpenDirectoryRequested += (_, selected) => OpenDirectory(selected);
        card.KillRequested += (_, selected) => KillProcess(selected);
        return card;
    }

    private void ResizeCards()
    {
        foreach (Control control in _cardsPanel.Controls)
        {
            control.Width = Math.Max(520, _cardsPanel.ClientSize.Width - 28);
        }
    }

    private static void OpenUrl(PortEntry entry)
    {
        Process.Start(new ProcessStartInfo(entry.Url) { UseShellExecute = true });
    }

    private static void OpenDirectory(PortEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.WorkingDirectory) && Directory.Exists(entry.WorkingDirectory))
        {
            Process.Start(new ProcessStartInfo(entry.WorkingDirectory) { UseShellExecute = true });
        }
    }

    private void KillProcess(PortEntry entry)
    {
        var confirm = MessageBox.Show(
            $"Kill PID {entry.ProcessId} ({entry.ProcessName}) and its child processes?",
            "Confirm kill",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _scanner.Kill(entry.ProcessId);
            RefreshPorts();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Kill failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool Matches(PortEntry entry, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var haystack = string.Join(" ", entry.LocalPort, entry.ProcessId, entry.ProcessName, entry.ProjectName, entry.Framework, entry.CommandLine, entry.WorkingDirectory);
        return haystack.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string CardKey(PortEntry entry)
    {
        return $"{entry.Protocol}:{entry.LocalAddress}:{entry.LocalPort}:{entry.ProcessId}";
    }
}
