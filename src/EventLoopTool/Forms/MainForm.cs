using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;
using EventLoopTool.Models;
using EventLoopTool.Data;

namespace EventLoopTool.Forms;

public class MainForm : Form
{
    // ── State ────────────────────────────────────────────────────────────────

    private SqlConnection? _connection;
    private ProLawRepository? _repo;
    private UserSession? _session;
    private List<DisplayLoop> _loops = [];

    // ── UI controls ──────────────────────────────────────────────────────────

    private readonly ToolStrip _toolbar;
    private readonly ToolStripButton _btnRefresh;
    private readonly ToolStripButton _btnPrint;
    private readonly ToolStripButton _btnExport;
    private readonly ToolStripLabel _lblSummary;
    private readonly StatusStrip _statusBar;
    private readonly ToolStripStatusLabel _lblStatus;
    private readonly ToolStripStatusLabel _lblUser;
    private readonly Panel _scrollPanel;
    private readonly FlowLayoutPanel _loopList;
    private readonly Label _lblEmpty;

    // Collapse state: indices of collapsed loops
    private readonly HashSet<int> _collapsed = [];

    // ── Colors (matching the Events Manager web app) ─────────────────────────

    private static readonly Color CardBg = Color.FromArgb(250, 250, 249);
    private static readonly Color CardBorder = Color.FromArgb(232, 229, 222);
    private static readonly Color TextPrimary = Color.FromArgb(42, 37, 32);
    private static readonly Color TextSecondary = Color.FromArgb(107, 101, 96);
    private static readonly Color TextMuted = Color.FromArgb(168, 160, 152);

    private static Color BadgeBg(LoopType t) => t switch
    {
        LoopType.Self => Color.FromArgb(255, 240, 224),
        LoopType.TwoNode => Color.FromArgb(255, 240, 240),
        _ => Color.FromArgb(240, 232, 255),
    };
    private static Color BadgeFg(LoopType t) => t switch
    {
        LoopType.Self => Color.FromArgb(176, 96, 0),
        LoopType.TwoNode => Color.FromArgb(160, 0, 0),
        _ => Color.FromArgb(96, 0, 176),
    };

    // ── Constructor ──────────────────────────────────────────────────────────

    public MainForm()
    {
        Text = "Event Loop Tool";
        Size = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        // Toolbar
        _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        _btnRefresh = new ToolStripButton("\u21bb Refresh") { Enabled = false };
        _btnRefresh.Click += async (_, _) => await LoadDataAsync();
        _btnPrint = new ToolStripButton("\U0001f5a8 Print") { Enabled = false };
        _btnPrint.Click += (_, _) => HandlePrint();
        _btnExport = new ToolStripButton("\U0001f4cb Export CSV") { Enabled = false };
        _btnExport.Click += (_, _) => HandleExportCsv();
        _lblSummary = new ToolStripLabel("") { Alignment = ToolStripItemAlignment.Right, ForeColor = TextSecondary };
        _toolbar.Items.AddRange([_btnRefresh, _btnPrint, _btnExport, _lblSummary]);

        // Status bar
        _statusBar = new StatusStrip();
        _lblStatus = new ToolStripStatusLabel("Not connected") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _lblUser = new ToolStripStatusLabel("") { Alignment = ToolStripItemAlignment.Right };
        _statusBar.Items.AddRange([_lblStatus, _lblUser]);

        // Empty state label
        _lblEmpty = new Label
        {
            Text = "Connecting...",
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 12f),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Visible = true,
        };

        // Loop card container
        _loopList = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 8, 16, 8),
            Visible = false,
        };

        // Scroll panel wrapping the flow layout
        _scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
        };
        _scrollPanel.Controls.Add(_loopList);
        _scrollPanel.Controls.Add(_lblEmpty);

        Controls.Add(_scrollPanel);
        Controls.Add(_toolbar);
        Controls.Add(_statusBar);

        FormClosing += MainForm_FormClosing;
        Shown += async (_, _) => await InitAsync();
    }

    // ── Startup ──────────────────────────────────────────────────────────────

    private async Task InitAsync()
    {
        // Step 1: Connection dialog
        ConnectionProfile profile;
        using (var connectForm = new ConnectForm())
        {
            if (connectForm.ShowDialog(this) != DialogResult.OK) { Close(); return; }
            profile = connectForm.SelectedProfile!;
        }

        // Step 2: Open connection
        _lblEmpty.Text = "Connecting...";
        try
        {
            _connection = new SqlConnection(profile.ConnectionString);
            await Task.Run(() => _connection.Open());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not connect to '{profile.Name}'.\n\n{ex.Message}",
                "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close(); return;
        }

        _repo = new ProLawRepository(_connection);
        _lblStatus.Text = $"Connected: {profile.Name}";

        // Step 3: Authenticate via Windows login
        var windowsLogin = Environment.UserDomainName + "\\" + Environment.UserName;
        var profResult = _repo.LookupProfessional(windowsLogin);

        if (profResult == null)
        {
            MessageBox.Show(
                $"Your Windows login ({windowsLogin}) was not found in the ProLaw " +
                "Professionals table.\n\nContact your ProLaw administrator.",
                "Authentication Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close(); return;
        }

        var (profAtom, securityClassAtom, profName) = profResult.Value;

        // Step 4: Check permissions
        var permissions = _repo.LoadPermissions(securityClassAtom);

        if (!permissions.CanUseApp)
        {
            new AccessDeniedForm(profName, permissions).ShowDialog(this);
            return;
        }

        // Step 5: Register session
        try { _repo.InsertCurrentProfessionals(profAtom); }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not establish ProLaw session.\n\n{ex.Message}",
                "Session Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close(); return;
        }

        _session = new UserSession
        {
            ProfessionalsAtom = profAtom,
            SecurityClassAtom = securityClassAtom,
            ProfName = profName,
            Permissions = permissions,
            Profile = profile,
        };

        Text = $"Event Loop Tool \u2014 {profile.Name}";
        _lblUser.Text = profName;
        _btnRefresh.Enabled = true;

        // Step 6: Load data
        await LoadDataAsync();
    }

    // ── Data loading ─────────────────────────────────────────────────────────

    private async Task LoadDataAsync()
    {
        _btnRefresh.Enabled = false;
        _btnPrint.Enabled = false;
        _btnExport.Enabled = false;
        _lblSummary.Text = "Loading...";
        _lblEmpty.Text = "Loading...";
        _lblEmpty.Visible = true;
        _loopList.Visible = false;

        try
        {
            _loops = await Task.Run(() => _repo!.LoadLoops());
            _collapsed.Clear();
            for (int i = 0; i < _loops.Count; i++) _collapsed.Add(i);
            RebuildCards();
        }
        catch (Exception ex)
        {
            _lblEmpty.Text = $"Error: {ex.Message}";
            _lblEmpty.Visible = true;
            _loopList.Visible = false;
            _lblSummary.Text = "Error";
        }
        finally
        {
            _btnRefresh.Enabled = true;
        }
    }

    // ── Card rendering ───────────────────────────────────────────────────────

    private void RebuildCards()
    {
        _loopList.SuspendLayout();
        _loopList.Controls.Clear();

        var totalEvents = _loops.Sum(l => l.Rows.Count);
        _lblSummary.Text = $"{totalEvents:N0} event{(totalEvents != 1 ? "s" : "")} in {_loops.Count} loop{(_loops.Count != 1 ? "s" : "")}";

        if (_loops.Count == 0)
        {
            _lblEmpty.Text = "No event loops found.";
            _lblEmpty.Visible = true;
            _loopList.Visible = false;
            _loopList.ResumeLayout();
            return;
        }

        _lblEmpty.Visible = false;
        _loopList.Visible = true;

        int cardWidth = _scrollPanel.ClientSize.Width - 52;
        _loopList.Width = _scrollPanel.ClientSize.Width - 4;

        for (int i = 0; i < _loops.Count; i++)
            _loopList.Controls.Add(BuildCard(i, cardWidth));

        // Set flow layout height to accommodate all cards
        int totalHeight = 0;
        foreach (Control c in _loopList.Controls)
            totalHeight += c.Height + c.Margin.Vertical;
        _loopList.Height = totalHeight + _loopList.Padding.Vertical + 20;

        _loopList.ResumeLayout();
        _btnPrint.Enabled = true;
        _btnExport.Enabled = true;
    }

    private Panel BuildCard(int index, int width)
    {
        var loop = _loops[index];
        bool isCollapsed = _collapsed.Contains(index);

        // Header height + optional grid
        int headerHeight = 50;
        int gridHeight = isCollapsed ? 0 : Math.Min(loop.Rows.Count * 28 + 34, 300);

        var card = new Panel
        {
            Width = width,
            Height = headerHeight + gridHeight + (isCollapsed ? 0 : 8),
            BackColor = CardBg,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(0),
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(CardBorder);
            var r = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
            e.Graphics.DrawRectangle(pen, r);
        };

        // Expand/collapse toggle
        var lblToggle = new Label
        {
            Text = isCollapsed ? "\u25b6" : "\u25bc",
            Font = new Font("Segoe UI", 8f),
            ForeColor = TextMuted,
            Location = new Point(12, 16),
            AutoSize = true,
            Cursor = Cursors.Hand,
        };

        // Type badge
        var lblBadge = new Label
        {
            Text = DisplayLoop.TypeLabel(loop.Type),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = BadgeFg(loop.Type),
            BackColor = BadgeBg(loop.Type),
            Location = new Point(32, 10),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
        };

        // Event count
        var lblCount = new Label
        {
            Text = $"{loop.Rows.Count} event{(loop.Rows.Count != 1 ? "s" : "")}",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = TextMuted,
            Location = new Point(32, 30),
            AutoSize = true,
        };

        // Chain label
        var lblChain = new Label
        {
            Text = loop.ChainLabel,
            Font = new Font("Consolas", 10f),
            ForeColor = TextPrimary,
            Location = new Point(lblBadge.PreferredWidth + 50, 14),
            AutoSize = true,
        };

        // Click handler for header area
        void ToggleClick(object? s, EventArgs e)
        {
            if (_collapsed.Contains(index)) _collapsed.Remove(index);
            else _collapsed.Add(index);
            RebuildCards();
        }
        lblToggle.Click += ToggleClick;
        lblBadge.Click += ToggleClick;
        lblCount.Click += ToggleClick;
        lblChain.Click += ToggleClick;
        card.Click += (s, e) =>
        {
            // Only toggle if clicking the header area (not the grid)
            var me = (MouseEventArgs)e;
            if (me.Y <= headerHeight) ToggleClick(s, e);
        };

        card.Controls.AddRange([lblToggle, lblBadge, lblCount, lblChain]);

        // Detail grid
        if (!isCollapsed)
        {
            var grid = BuildGrid(index, loop, width - 24);
            grid.Location = new Point(12, headerHeight);
            card.Controls.Add(grid);
        }

        return card;
    }

    private DataGridView BuildGrid(int loopIndex, DisplayLoop loop, int width)
    {
        var grid = new DataGridView
        {
            Width = width,
            Height = Math.Min(loop.Rows.Count * 28 + 34, 300),
            AutoGenerateColumns = false,
            ReadOnly = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BorderStyle = BorderStyle.None,
            BackgroundColor = CardBg,
            GridColor = CardBorder,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextPrimary,
                BackColor = CardBg,
                SelectionBackColor = Color.FromArgb(230, 240, 255),
                SelectionForeColor = TextPrimary,
                Padding = new Padding(2),
            },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = TextMuted,
                BackColor = CardBg,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
            },
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 28,
            RowTemplate = { Height = 26 },
            ScrollBars = loop.Rows.Count > 10 ? ScrollBars.Vertical : ScrollBars.None,
        };

        grid.AllowUserToResizeColumns = true;
        grid.AllowUserToResizeRows = false;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        grid.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { Name = "EventNo", HeaderText = "Event/Document #", Width = 120, ReadOnly = true },
            new DataGridViewTextBoxColumn { Name = "EventPK", HeaderText = "Events (PK)", Width = 80, ReadOnly = true },
            new DataGridViewTextBoxColumn { Name = "ParentNo", HeaderText = "Parent #", Width = 120, ReadOnly = true },
            new DataGridViewTextBoxColumn { Name = "ParentPK", HeaderText = "EventsParent (PK)", Width = 80, ReadOnly = true },
            new DataGridViewTextBoxColumn { Name = "Matter", HeaderText = "Matter", Width = 100, ReadOnly = true },
            new DataGridViewTextBoxColumn { Name = "EventDate", HeaderText = "Date", Width = 90, ReadOnly = true },
            new DataGridViewTextBoxColumn { Name = "Kind", HeaderText = "Kind", Width = 75, ReadOnly = true },
            new DataGridViewTextBoxColumn { Name = "EventType", HeaderText = "Event Type", Width = 130, ReadOnly = true },
            new DataGridViewTextBoxColumn { Name = "Professional", HeaderText = "Professional", Width = 110, ReadOnly = true },
            new DataGridViewTextBoxColumn { Name = "Note", HeaderText = "Note", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true },
            new DataGridViewButtonColumn { Name = "Action", HeaderText = "", Text = "Clear parent", UseColumnTextForButtonValue = true, Width = 90 },
        ]);

        foreach (var row in loop.Rows)
        {
            int ri = grid.Rows.Add(
                row.EventNo ?? "\u2014",
                row.EventId,
                row.ParentNo ?? (row.ParentId != null ? row.ParentId[..Math.Min(8, row.ParentId.Length)] + "\u2026" : "\u2014"),
                row.ParentId ?? "\u2014",
                row.Matter,
                row.EventDate ?? "\u2014",
                row.Kind,
                row.EventType,
                row.Professional,
                row.Note
            );
            grid.Rows[ri].Tag = row.EventId;
        }

        grid.CellClick += async (_, e) =>
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].Name != "Action") return;

            var eventId = (string)grid.Rows[e.RowIndex].Tag!;
            var eventNo = grid.Rows[e.RowIndex].Cells["EventNo"].Value?.ToString() ?? eventId[..8];

            var result = MessageBox.Show(
                $"Clear EventsParent on event {eventNo}?\n\nThis will sever the loop at this node.",
                "Confirm Repair",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
                await DoRepairAsync(eventId);
        };

        return grid;
    }

    // ── Repair ───────────────────────────────────────────────────────────────

    private async Task DoRepairAsync(string eventId)
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            await Task.Run(() => _repo!.ClearEventParent(eventId));
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Repair failed:\n\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    // ── Print ────────────────────────────────────────────────────────────────

    private void HandlePrint()
    {
        var sb = new StringBuilder();
        var timestamp = DateTime.Now.ToString("g");
        var totalEvents = _loops.Sum(l => l.Rows.Count);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"><title>Event Loops</title></head>");
        sb.AppendLine("<body style=\"font-family:'Segoe UI',Arial,sans-serif;font-size:11px;color:#333;max-width:1100px;margin:0 auto;padding:24px\">");
        sb.AppendLine($"<h2 style=\"margin:0 0 4px;font-size:18px;color:#2A2520\">Event Loops</h2>");
        sb.AppendLine($"<div style=\"font-size:11px;color:#857F78;margin-bottom:16px\">{totalEvents:N0} event{(totalEvents != 1 ? "s" : "")} in {_loops.Count} loop{(_loops.Count != 1 ? "s" : "")} &bull; {Esc(timestamp)} &bull; {Esc(_session?.Profile.Name ?? "")}</div>");

        // Group by type
        var grouped = _loops.GroupBy(l => l.Type).OrderBy(g => g.Key);
        foreach (var group in grouped)
        {
            var groupEvents = group.Sum(l => l.Rows.Count);
            sb.AppendLine($"<h3 style=\"margin:18px 0 6px;font-size:13px;color:#4A4540\">{Esc(DisplayLoop.TypeLabel(group.Key))} &mdash; {group.Count()} loop{(group.Count() != 1 ? "s" : "")}, {groupEvents} event{(groupEvents != 1 ? "s" : "")}</h3>");

            foreach (var loop in group)
            {
                sb.AppendLine($"<div style=\"margin:0 0 4px;font-size:11px;color:#857F78;font-family:Consolas,monospace\">{Esc(loop.ChainLabel)}</div>");
                sb.AppendLine("<table style=\"width:100%;border-collapse:collapse;font-size:11px;margin-bottom:14px\">");
                sb.AppendLine("<thead><tr>");
                foreach (var h in new[] { "Event/Document #", "Events (PK)", "Parent #", "EventsParent (PK)", "Matter", "Date", "Kind", "Event Type", "Professional", "Note" })
                    sb.AppendLine($"<th style=\"text-align:left;padding:4px 8px 6px 0;font-size:10px;font-weight:700;color:#888;text-transform:uppercase;letter-spacing:0.06em;border-bottom:2px solid #ccc;white-space:nowrap\">{h}</th>");
                sb.AppendLine("</tr></thead><tbody>");

                foreach (var row in loop.Rows)
                {
                    sb.AppendLine("<tr style=\"border-bottom:1px solid #ddd\">");
                    var td = "padding:5px 8px 5px 0;vertical-align:top";
                    sb.AppendLine($"<td style=\"{td}\">{Esc(row.EventNo ?? "\u2014")}</td>");
                    sb.AppendLine($"<td style=\"{td};font-family:Consolas,monospace;font-size:10px\">{Esc(row.EventId)}</td>");
                    sb.AppendLine($"<td style=\"{td}\">{Esc(row.ParentNo ?? "\u2014")}</td>");
                    sb.AppendLine($"<td style=\"{td};font-family:Consolas,monospace;font-size:10px\">{Esc(row.ParentId ?? "\u2014")}</td>");
                    sb.AppendLine($"<td style=\"{td}\">{Esc(row.Matter)}</td>");
                    sb.AppendLine($"<td style=\"{td};white-space:nowrap\">{Esc(row.EventDate ?? "\u2014")}</td>");
                    sb.AppendLine($"<td style=\"{td}\">{Esc(row.Kind)}</td>");
                    sb.AppendLine($"<td style=\"{td}\">{Esc(row.EventType)}</td>");
                    sb.AppendLine($"<td style=\"{td}\">{Esc(row.Professional)}</td>");
                    sb.AppendLine($"<td style=\"{td}\">{Esc(row.Note)}</td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</tbody></table>");
            }
        }

        sb.AppendLine("<div style=\"margin-top:24px;font-size:9px;color:#999;border-top:1px solid #ddd;padding-top:6px\">Generated by NextPro Event Loop Tool</div>");
        sb.AppendLine("</body></html>");

        var tempPath = Path.Combine(Path.GetTempPath(), "event-loops-report.html");
        File.WriteAllText(tempPath, sb.ToString());
        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
    }

    // ── Export CSV ────────────────────────────────────────────────────────────

    private void HandleExportCsv()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"event-loops-{DateTime.Now:yyyy-MM-dd}.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var sb = new StringBuilder();
        sb.AppendLine("Loop Type,Chain,Event/Document #,Events (PK),Parent #,EventsParent (PK),Matter,Date,Kind,Event Type,Professional,Note");
        foreach (var loop in _loops)
            foreach (var row in loop.Rows)
            {
                sb.AppendLine(string.Join(",",
                    Csv(DisplayLoop.TypeLabel(loop.Type)),
                    Csv(loop.ChainLabel),
                    Csv(row.EventNo ?? ""),
                    Csv(row.EventId),
                    Csv(row.ParentNo ?? ""),
                    Csv(row.ParentId ?? ""),
                    Csv(row.Matter),
                    Csv(row.EventDate ?? ""),
                    Csv(row.Kind),
                    Csv(row.EventType),
                    Csv(row.Professional),
                    Csv(row.Note)
                ));
            }

        File.WriteAllText(dlg.FileName, sb.ToString());
        _lblStatus.Text = $"Exported to {Path.GetFileName(dlg.FileName)}";
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _repo?.DeleteCurrentProfessionals();
        _connection?.Close();
        _connection?.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Esc(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string Csv(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
