using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PiHoleTray;

class QueryLogForm : Form
{
    // ── Layout ────────────────────────────────────────────────────────────────
    private const int FW  = 1000;  // form width
    private const int FH  = 600;   // form height
    private const int TH  = 52;    // title bar
    private const int FBH = 50;    // filter bar
    private const int BH  = 60;    // button bar
    private const int Pad = 20;

    // Content top = below title separator + filter bar + filter separator
    private const int CT = TH + 1 + FBH + 1;

    // ── Colours ───────────────────────────────────────────────────────────────
    private static readonly Color CBg      = Color.White;
    private static readonly Color CBorder  = Color.FromArgb(210, 210, 220);
    private static readonly Color CAccent  = Color.FromArgb(16,  124,  64);
    private static readonly Color COrange  = Color.FromArgb(184, 112,   0);
    private static readonly Color CRed     = Color.FromArgb(196,  40,  40);
    private static readonly Color CTxt     = Color.FromArgb(20,   20,  24);
    private static readonly Color CTxt2    = Color.FromArgb(80,   80,  96);
    private static readonly Color CTxt3    = Color.FromArgb(140, 140, 158);
    private static readonly Color CBtnBg   = Color.FromArgb(234, 234, 242);
    private static readonly Color CBtnH    = Color.FromArgb(216, 216, 228);
    private static readonly Color CRowAlt  = Color.FromArgb(248, 248, 252);
    private static readonly Color CRowSel  = Color.FromArgb(218, 242, 226);
    private static readonly Color CAllowed = Color.FromArgb(232, 250, 238);
    private static readonly Color CTemp    = Color.FromArgb(255, 248, 230);
    private static readonly Color CHead    = Color.FromArgb(240, 240, 248);
    private static readonly Color CFilter  = Color.FromArgb(245, 245, 252);

    // ── Fonts ─────────────────────────────────────────────────────────────────
    private readonly Font _fTitle = new("Segoe UI Semibold", 11f);
    private readonly Font _fHead  = new("Segoe UI Semibold", 8.5f);
    private readonly Font _fRow   = new("Segoe UI", 9f);
    private readonly Font _fBtn   = new("Segoe UI", 9.5f);
    private readonly Font _fBold  = new("Segoe UI Semibold", 9f);
    private readonly Font _fLbl   = new("Segoe UI", 8.5f);

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly PiHoleApi           _api;
    private readonly string              _lang;
    private readonly Action<string, int> _scheduleRemoval;

    private List<BlockedQuery> _allQueries = [];

    // ── Controls ──────────────────────────────────────────────────────────────
    private ListView  _list        = null!;
    private Label     _overlay     = null!;
    private ComboBox  _timeCombo   = null!;
    private ComboBox  _clientCombo = null!;
    private Point     _drag;

    // Time-period options: (display, minutes; 0 = no limit)
    private static readonly (string Label, int Minutes)[] TimePeriods =
    {
        ("—",      0),
        ("1 min",  1),
        ("5 min",  5),
        ("10 min", 10),
        ("30 min", 30),
        ("1 h",    60),
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    public QueryLogForm(PiHoleApi api, string lang, Action<string, int> scheduleRemoval)
    {
        _api             = api;
        _lang            = lang;
        _scheduleRemoval = scheduleRemoval;
        Build();
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void Build()
    {
        SuspendLayout();
        FormBorderStyle = FormBorderStyle.None;
        BackColor       = CBg;
        TopMost         = true;
        ClientSize      = new Size(FW, FH);
        StartPosition   = FormStartPosition.Manual;
        Font            = _fRow;
        KeyPreview      = true;
        KeyDown        += (_, e) =>
        {
            if      (e.KeyCode == Keys.F5)     _ = LoadAsync();
            else if (e.KeyCode == Keys.Escape) Close();
        };

        var s = new Panel { Location = new Point(1, 1), Size = new Size(FW-2, FH-2), BackColor = CBg };
        Controls.Add(s);

        BuildTitleBar(s);
        BuildFilterBar(s);
        BuildList(s);
        BuildButtonBar(s);

        ResumeLayout();
        PositionNearTaskbar();
        _ = LoadAsync();
    }

    // ── Title bar ─────────────────────────────────────────────────────────────

    private void BuildTitleBar(Panel s)
    {
        var bar = new Panel { Bounds = new Rectangle(0, 0, FW-2, TH), BackColor = CBg };
        s.Controls.Add(bar);
        Draggable(bar);

        try
        {
            var ico = IconRenderer.GetIcon("enabled", 22);
            var pb  = new PictureBox { Bounds = new Rectangle(Pad, (TH-22)/2, 22, 22),
                                       Image = ico.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom,
                                       BackColor = Color.Transparent };
            bar.Controls.Add(pb);
            Draggable(pb);
        }
        catch { }

        var lbl = new Label { Text = Loc.T("ql_title", _lang), Font = _fTitle, ForeColor = CTxt,
                              AutoSize = true, Location = new Point(Pad+28, (TH-20)/2),
                              BackColor = Color.Transparent };
        bar.Controls.Add(lbl);
        Draggable(lbl);

        var close = MkBtn("  \u2715  ", CBg, CTxt3);
        close.Font = new Font("Segoe UI", 12f);
        close.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
        close.Bounds = new Rectangle(FW-2-52, 0, 52, TH);
        close.MouseEnter += (_, _) => close.ForeColor = Color.White;
        close.MouseLeave += (_, _) => close.ForeColor = CTxt3;
        close.Click += (_, _) => Close();
        bar.Controls.Add(close);

        s.Controls.Add(new Panel { Bounds = new Rectangle(0, TH, FW-2, 1), BackColor = CBorder });
    }

    // ── Filter bar ────────────────────────────────────────────────────────────

    private void BuildFilterBar(Panel s)
    {
        var bar = new Panel { Bounds = new Rectangle(0, TH+1, FW-2, FBH), BackColor = CFilter };
        s.Controls.Add(bar);

        int cy = (FBH - 24) / 2;   // vertical centre for 24px-high controls
        int x  = Pad;

        // ── Time period ── (fixed label width avoids text cut-off in any language)
        bar.Controls.Add(new Label
        {
            Text      = Loc.T("ql_filter_period", _lang) + ":",
            Font      = _fLbl, ForeColor = CTxt2,
            AutoSize  = false, Width = 90, Height = 20,
            Location  = new Point(x, cy + 5),
            BackColor = Color.Transparent,
        });
        x += 94;

        _timeCombo = MkCombo(bar, x, cy, 120);
        _timeCombo.Items.Add(Loc.T("ql_all", _lang));
        foreach (var (label, _) in TimePeriods.Skip(1))
            _timeCombo.Items.Add(label);
        _timeCombo.SelectedIndex = 0;
        _timeCombo.SelectedIndexChanged += (_, _) => ApplyFilter();
        x += 120 + 28;

        // ── Client ──
        bar.Controls.Add(new Label
        {
            Text      = Loc.T("ql_filter_client", _lang) + ":",
            Font      = _fLbl, ForeColor = CTxt2,
            AutoSize  = false, Width = 60, Height = 20,
            Location  = new Point(x, cy + 5),
            BackColor = Color.Transparent,
        });
        x += 64;

        _clientCombo = MkCombo(bar, x, cy, 300);
        _clientCombo.Items.Add(Loc.T("ql_all", _lang));
        _clientCombo.SelectedIndex = 0;
        _clientCombo.SelectedIndexChanged += (_, _) => ApplyFilter();

        // Bottom separator
        s.Controls.Add(new Panel { Bounds = new Rectangle(0, TH+1+FBH, FW-2, 1), BackColor = CBorder });
    }

    private static ComboBox MkCombo(Panel parent, int x, int y, int w)
    {
        var cb = new ComboBox
        {
            Bounds        = new Rectangle(x, y, w, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle     = FlatStyle.Flat,
            BackColor     = Color.White,
            ForeColor     = Color.FromArgb(20, 20, 24),
        };
        parent.Controls.Add(cb);
        return cb;
    }

    // ── List area ─────────────────────────────────────────────────────────────

    private void BuildList(Panel s)
    {
        int listH = FH - 2 - CT - BH;

        // Column widths sum to 958 ≤ FW-2-40(scrollbar+DPI buffer) = 958 — no horizontal scroll
        // Zeit=82, Domain=430, Client=248, Status=198 → 958
        _list = new ListView
        {
            Bounds        = new Rectangle(0, CT, FW-2, listH),
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            MultiSelect   = false,
            HeaderStyle   = ColumnHeaderStyle.Nonclickable,
            BackColor     = CBg,
            ForeColor     = CTxt,
            Font          = _fRow,
            BorderStyle   = BorderStyle.None,
            OwnerDraw     = true,
            Scrollable    = true,
        };

        _list.Columns.Add(Loc.T("ql_col_time",   _lang),  82);
        _list.Columns.Add(Loc.T("ql_col_domain",  _lang), 430);
        _list.Columns.Add(Loc.T("ql_col_client",  _lang), 248);
        _list.Columns.Add(Loc.T("ql_col_status",  _lang), 198);

        _list.DrawColumnHeader += OnDrawHeader;
        _list.DrawItem         += (_, e) => { e.DrawDefault = false; };
        _list.DrawSubItem      += OnDrawSubItem;

        _list.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && _list.SelectedItems.Count > 0)
                ShowContextMenu(e.Location);
        };

        s.Controls.Add(_list);

        // Overlay: shown when loading or no results
        _overlay = new Label
        {
            Text      = Loc.T("ql_loading", _lang),
            Font      = _fRow,
            ForeColor = CTxt3,
            AutoSize  = false,
            Bounds    = new Rectangle(0, CT, FW-2, listH),
            BackColor = CBg,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible   = true,
        };
        s.Controls.Add(_overlay);
        _overlay.BringToFront();
    }

    private void OnDrawHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using var bg  = new SolidBrush(CHead);
        using var sep = new Pen(CBorder);
        e.Graphics.FillRectangle(bg, e.Bounds);
        e.Graphics.DrawLine(sep, e.Bounds.Left, e.Bounds.Bottom-1, e.Bounds.Right, e.Bounds.Bottom-1);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", _fHead,
            Rectangle.Inflate(e.Bounds, -6, 0), CTxt2,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    private void OnDrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item == null || e.SubItem == null) return;

        bool   sel  = e.Item.Selected;
        bool   even = e.ItemIndex % 2 == 0;
        string tag  = e.Item.Tag as string ?? "";

        Color bg = sel ? CRowSel
                 : tag == "perm" ? CAllowed
                 : tag == "temp" ? CTemp
                 : even ? CBg : CRowAlt;

        using var bgBrush = new SolidBrush(bg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        using var sep = new Pen(Color.FromArgb(232, 232, 242));
        e.Graphics.DrawLine(sep, e.Bounds.Left, e.Bounds.Bottom-1, e.Bounds.Right, e.Bounds.Bottom-1);

        Color fg = e.ColumnIndex switch
        {
            3 => tag == "perm" ? CAccent : tag == "temp" ? COrange : CRed,
            0 or 2 => CTxt2,
            _ => CTxt,
        };

        TextRenderer.DrawText(e.Graphics, e.SubItem.Text ?? "", _fRow,
            Rectangle.Inflate(e.Bounds, -5, 0), fg,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private void ShowContextMenu(Point pt)
    {
        var lvi    = _list.SelectedItems[0];
        var domain = lvi.SubItems[1].Text;

        var ctx = new ContextMenuStrip { Font = _fRow };

        var perm = new ToolStripMenuItem(Loc.T("ql_allow_perm", _lang)) { Font = _fBold };
        perm.Click += async (_, _) => await AllowPermAsync(lvi, domain);
        ctx.Items.Add(perm);

        ctx.Items.Add(new ToolStripSeparator());

        var temp = new ToolStripMenuItem(Loc.T("ql_allow_temp", _lang));
        foreach (var (label, min) in new (string, int)[]
        {
            (Loc.T("menu_5min",  _lang),   5),
            (Loc.T("menu_30min", _lang),  30),
            (Loc.T("menu_1h",    _lang),  60),
            (Loc.T("menu_2h",    _lang), 120),
        })
        {
            int m = min;
            var sub = new ToolStripMenuItem(label);
            sub.Click += async (_, _) => await AllowTempAsync(lvi, domain, m);
            temp.DropDownItems.Add(sub);
        }
        ctx.Items.Add(temp);

        ctx.Show(_list, pt);
    }

    // ── Allow actions ─────────────────────────────────────────────────────────

    private async Task AllowPermAsync(ListViewItem lvi, string domain)
    {
        bool ok = await _api.AllowDomainAsync(domain).ConfigureAwait(true);
        lvi.Tag              = ok ? "perm" : "";
        lvi.SubItems[3].Text = ok ? Loc.T("ql_allowed_perm", _lang)
                                  : Loc.T("ql_error", _lang) + "failed";
        _list.Invalidate();
    }

    private async Task AllowTempAsync(ListViewItem lvi, string domain, int minutes)
    {
        bool ok = await _api.AllowDomainAsync(domain).ConfigureAwait(true);
        if (ok) _scheduleRemoval(domain, minutes);
        lvi.Tag              = ok ? "temp" : "";
        lvi.SubItems[3].Text = ok ? Loc.T("ql_allowed_temp", _lang)
                                  : Loc.T("ql_error", _lang) + "failed";
        _list.Invalidate();
    }

    // ── Load + filter ─────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        _overlay.Text    = Loc.T("ql_loading", _lang);
        _overlay.Visible = true;
        _list.Visible    = false;
        _list.Items.Clear();

        try
        {
            _allQueries = await _api.GetBlockedQueriesAsync(500).ConfigureAwait(true);
            RefreshClientCombo();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _overlay.Text    = Loc.T("ql_error", _lang) + ex.Message;
            _overlay.Visible = true;
        }
    }

    private void RefreshClientCombo()
    {
        // Remember current selection
        var current = _clientCombo.SelectedIndex > 0 ? _clientCombo.SelectedItem?.ToString() : null;

        _clientCombo.SelectedIndexChanged -= OnClientComboChanged;
        _clientCombo.Items.Clear();
        _clientCombo.Items.Add(Loc.T("ql_all", _lang));

        var clients = _allQueries
            .Select(q => string.IsNullOrEmpty(q.ClientName) ? q.ClientIp : $"{q.ClientName} ({q.ClientIp})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        foreach (var c in clients) _clientCombo.Items.Add(c);

        // Restore selection if still present
        int idx = current != null ? _clientCombo.Items.IndexOf(current) : -1;
        _clientCombo.SelectedIndex = idx > 0 ? idx : 0;
        _clientCombo.SelectedIndexChanged += OnClientComboChanged;
    }

    private void OnClientComboChanged(object? sender, EventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        string? clientFilter = _clientCombo.SelectedIndex > 0
            ? _clientCombo.SelectedItem?.ToString()
            : null;

        int timeSel = _timeCombo.SelectedIndex;
        int minutes = timeSel > 0 && timeSel - 1 < TimePeriods.Length
            ? TimePeriods[timeSel].Minutes   // index 0 = "All" → skip; index 1..5 map to TimePeriods[1..5]
            : 0;

        // Correct mapping: SelectedIndex 0 = "All" (TimePeriods[0]), 1 = "1 min" (TimePeriods[1]), …
        minutes = timeSel > 0 && timeSel < TimePeriods.Length
            ? TimePeriods[timeSel].Minutes
            : 0;

        DateTime since = minutes > 0 ? DateTime.Now.AddMinutes(-minutes) : DateTime.MinValue;

        var filtered = _allQueries.Where(q =>
            (clientFilter == null || ClientStr(q) == clientFilter) &&
            (since == DateTime.MinValue || q.Time >= since)
        ).ToList();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var q in filtered)
        {
            var lvi = new ListViewItem(q.Time.ToString("HH:mm:ss"));
            lvi.SubItems.Add(q.Domain);
            lvi.SubItems.Add(ClientStr(q));
            lvi.SubItems.Add(q.Status);
            _list.Items.Add(lvi);
        }
        _list.EndUpdate();

        if (_list.Items.Count > 0)
        {
            _overlay.Visible = false;
            _list.Visible    = true;
        }
        else
        {
            _overlay.Text    = Loc.T("ql_none", _lang);
            _overlay.Visible = true;
            _list.Visible    = false;
        }
    }

    private static string ClientStr(BlockedQuery q) =>
        string.IsNullOrEmpty(q.ClientName) ? q.ClientIp : $"{q.ClientName} ({q.ClientIp})";

    // ── Button bar ────────────────────────────────────────────────────────────

    private void BuildButtonBar(Panel s)
    {
        int barY = FH - 2 - BH;
        s.Controls.Add(new Panel { Bounds = new Rectangle(0, barY, FW-2, 1), BackColor = CBorder });

        int btnY = barY + (BH - 34) / 2;

        var refresh = MkBtn(Loc.T("ql_refresh", _lang), CBtnBg, CTxt);
        var close   = MkBtn(Loc.T("cancel",     _lang), CBtnBg, CTxt);
        refresh.FlatAppearance.MouseOverBackColor = CBtnH;
        close.FlatAppearance.MouseOverBackColor   = CBtnH;

        int rw = BtnWidth(refresh);
        int cw = BtnWidth(close);
        refresh.Bounds = new Rectangle(Pad,          btnY, rw, 34);
        close.Bounds   = new Rectangle(FW-2-Pad-cw, btnY, cw, 34);

        refresh.Click += async (_, _) => await LoadAsync();
        close.Click   += (_, _) => Close();

        s.Controls.Add(refresh);
        s.Controls.Add(close);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Button MkBtn(string text, Color bg, Color fg)
    {
        var b = new Button
        {
            Text = text, BackColor = bg, ForeColor = fg, Font = _fBtn,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = bg;
        return b;
    }

    private int BtnWidth(Button b)
    {
        using var g = CreateGraphics();
        return (int)g.MeasureString(b.Text, b.Font).Width + 40;
    }

    private void Draggable(Control c)
    {
        c.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) _drag = e.Location; };
        c.MouseMove += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            Location = new Point(Location.X + e.X - _drag.X, Location.Y + e.Y - _drag.Y);
        };
    }

    private void PositionNearTaskbar()
    {
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(
            Math.Max(wa.Left + 8, wa.Right  - Width  - 12),
            Math.Max(wa.Top  + 8, wa.Bottom - Height - 12));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var shadow = new Pen(Color.FromArgb(55, 0, 0, 0));
        e.Graphics.DrawRectangle(shadow, 0, 0, Width-1, Height-1);
        using var border = new Pen(CBorder);
        e.Graphics.DrawRectangle(border, 1, 1, Width-3, Height-3);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fTitle.Dispose(); _fHead.Dispose(); _fRow.Dispose();
            _fBtn.Dispose();   _fBold.Dispose(); _fLbl.Dispose();
        }
        base.Dispose(disposing);
    }
}
