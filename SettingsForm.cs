using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PiHoleTray;

class SettingsForm : Form
{
    // ── Layout constants ──────────────────────────────────────────────────────
    private const int FW   = 640;   // form width
    private const int FH   = 608;   // form height (increased for instance bar)
    private const int TH   = 52;    // title bar height
    private const int IH   = 48;    // instance bar height
    private const int BH   = 64;    // button bar height
    private const int Pad  = 24;    // outer padding
    private const int CW   = 287;   // column width (each side)
    private const int CGap = 16;    // gap between columns
    private const int LX   = Pad;            // left column x
    private const int RX   = Pad + CW + CGap; // right column x  (= 327)
    private const int InH  = 36;    // input height

    // ── Colours ───────────────────────────────────────────────────────────────
    private static readonly Color CBg      = Color.White;
    private static readonly Color CBorder  = Color.FromArgb(210, 210, 220);
    private static readonly Color CAccent  = Color.FromArgb(16,  124,  64);
    private static readonly Color CAccentH = Color.FromArgb(10,   96,  50);
    private static readonly Color CInputBd = Color.FromArgb(190, 190, 205);
    private static readonly Color CInputBg = Color.FromArgb(248, 248, 252);
    private static readonly Color CTxt     = Color.FromArgb(20,  20,  24);
    private static readonly Color CTxt2    = Color.FromArgb(80,  80,  96);
    private static readonly Color CTxt3    = Color.FromArgb(140, 140, 158);
    private static readonly Color CRed     = Color.FromArgb(196,  40,  40);
    private static readonly Color COrange  = Color.FromArgb(184, 112,   0);
    private static readonly Color CBtnBg   = Color.FromArgb(234, 234, 242);
    private static readonly Color CBtnH    = Color.FromArgb(216, 216, 228);
    private static readonly Color CGold    = Color.FromArgb(200, 150,   0);

    // ── Instance fonts ────────────────────────────────────────────────────────
    private readonly Font _fTitle   = new("Segoe UI Semibold", 11f);
    private readonly Font _fSection = new("Segoe UI Semibold", 8.5f);
    private readonly Font _fLabel   = new("Segoe UI", 9f);
    private readonly Font _fHint    = new("Segoe UI", 7.5f);
    private readonly Font _fInput   = new("Segoe UI", 10f);
    private readonly Font _fBtn     = new("Segoe UI", 9.5f);

    // ── State ─────────────────────────────────────────────────────────────────
    private AppConfig               _cfg;
    private readonly string         _lang;
    private readonly string         _iconState;
    private readonly Action<AppConfig>? _onSave;

    private List<PiHoleInstance> _instances = [];
    private int _selectedIdx = 0;

    // ── Control references ────────────────────────────────────────────────────
    private ComboBox      _instCombo = null!;
    private Button        _btnStar   = null!;
    private Button        _btnAdd    = null!;
    private Button        _btnDel    = null!;

    // ── Button icon bitmaps ───────────────────────────────────────────────────
    // Star path lives in IconRenderer.StarPath — used via IconRenderer.GetStarBitmap()

    private static readonly string _svgAdd =
        "M144.4,111.66c15.2,0,29.6,0,44.01,0,12.78,0,25.55-.1,38.33.04,8.78.1,15.4,6.41,15.99,14.86.63,8.96-4.9,16.08-13.73,17.5," +
        "-2.15.34-4.36.36-6.54.36-23.73.02-47.45.01-71.18.02h-6.61c-.22,2.52-.59,4.78-.59,7.03-.04,25.17.02,50.34-.06,75.51," +
        "-.03,7.81-5.45,14.25-12.72,15.6-7.96,1.48-15.32-2.23-18.29-9.58-.85-2.11-1.01-4.6-1.02-6.92-.07-25.17-.15-50.34.05-75.51," +
        ".04-4.74-1.25-6.29-6.15-6.24-24.82.23-49.64.17-74.46.07-11.32-.05-18.04-6.11-18.15-15.93-.12-10.2,6.27-16.76,16.65-16.79," +
        "25-.07,50.01-.18,75.01.1,5.46.06,7.21-1.3,7.14-6.96-.31-24.62.27-49.25-.32-73.86-.26-11.04,7.64-17.69,15.76-17.85," +
        "10.06-.2,16.52,6.93,16.53,18.15,0,24.44,0,48.88.02,73.32,0,2.15.2,4.3.34,7.08Z";

    private static readonly string[] _svgDel =
    [
        "M211.83,81.42c-.87,14.73-1.65,29.14-2.57,43.55-1.2,18.78-2.49,37.54-3.74,56.32-.92,13.81-1.67,27.63-2.79,41.41," +
        "-.88,10.8-9.92,18.84-20.69,18.84-35.94.02-71.88.01-107.82,0-10.88,0-19.53-7.5-20.56-18.26-1.12-11.76-1.85-23.56-2.65-35.36," +
        "-1.78-26.37-3.51-52.74-5.21-79.12-.58-8.99-1.04-17.98-1.58-27.38h167.62Z",

        "M127.91,59.7c-27.52,0-55.03.05-82.55-.03-12.15-.03-18.99-10.98-13.36-21.12,2.8-5.04,7.38-6.88,12.85-6.9," +
        "12.28-.05,24.56-.13,36.84.05,3.17.05,5.05-.81,5.69-3.98.12-.6.56-1.12.71-1.72,3.2-12.71,7.93-15.91,21.88-15.6," +
        "13.05.29,26.11.05,39.17.07,10.67.01,14.35,2.83,17.94,12.67,3.8,10.42,1.24,8.32,11.75,8.46,11.04.14,22.08-.08,33.11.08," +
        "6.89.1,11.8,4,13.47,10.12,1.65,6.06-.38,12.54-5.7,15.44-2.84,1.55-6.46,2.33-9.73,2.35-27.36.18-54.72.1-82.08.1Z",
    ];

    private Bitmap _imgStarGold = null!;
    private Bitmap _imgStarGray = null!;
    private Bitmap _imgAdd      = null!;
    private Bitmap _imgDel      = null!;
    private TextBox       _nameTb    = null!;
    private TextBox       _urlTb     = null!;
    private TextBox       _pwTb      = null!;
    private RadioButton   _rbV6      = null!;
    private RadioButton   _rbV5      = null!;
    private NumericUpDown _pollNud   = null!;
    private CheckBox      _autoChk   = null!;
    private ComboBox      _langCombo = null!;
    private Label         _statusLbl = null!;

    private Point _drag;
    private bool  _suppressComboChange = false;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SettingsForm(AppConfig cfg, string iconState, Action<AppConfig>? onSave)
    {
        _cfg       = cfg;
        _lang      = Loc.GetEffectiveLang(cfg.Language);
        _iconState = iconState;
        _onSave    = onSave;

        _instances = cfg.Instances.Select(i => new PiHoleInstance
        {
            Name       = i.Name,
            PiholeUrl  = i.PiholeUrl,
            ApiKey     = i.ApiKey,
            ApiVersion = i.ApiVersion,
            IsDefault  = i.IsDefault,
        }).ToList();
        if (_instances.Count == 0)
            _instances.Add(new PiHoleInstance { Name = "Pi-Hole", IsDefault = true });

        _selectedIdx = Math.Max(0, _instances.FindIndex(i => i.IsDefault));
        Build();
    }

    // ── Form setup ────────────────────────────────────────────────────────────

    private void Build()
    {
        SuspendLayout();
        FormBorderStyle = FormBorderStyle.None;
        BackColor       = CBg;
        TopMost         = true;
        ClientSize      = new Size(FW, FH);
        StartPosition   = FormStartPosition.Manual;
        Font            = _fLabel;
        KeyPreview      = true;
        KeyDown        += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var s = new Panel { Location = new Point(1, 1), Size = new Size(FW-2, FH-2), BackColor = CBg };
        Controls.Add(s);

        BuildTitleBar(s);
        BuildInstanceBar(s);
        BuildContent(s);
        BuildButtonBar(s);

        ResumeLayout();
        PositionNearTaskbar();
    }

    // ── Title bar ─────────────────────────────────────────────────────────────

    private void BuildTitleBar(Panel s)
    {
        var bar = new Panel { Bounds = new Rectangle(0, 0, FW-2, TH), BackColor = CBg };
        s.Controls.Add(bar);
        Draggable(bar);

        try
        {
            var ico = IconRenderer.GetIcon(_iconState, 22);
            var pb  = new PictureBox { Bounds = new Rectangle(Pad, (TH-22)/2, 22, 22),
                                       Image = ico.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom,
                                       BackColor = Color.Transparent };
            bar.Controls.Add(pb);
            Draggable(pb);
        }
        catch { }

        var lbl = new Label { Text = Loc.T("title", _lang), Font = _fTitle, ForeColor = CTxt,
                              AutoSize = true, Location = new Point(Pad+28, (TH-20)/2),
                              BackColor = Color.Transparent };
        bar.Controls.Add(lbl);
        Draggable(lbl);

        var close = MkBtn("", CBg, CTxt3);
        close.Image      = IconRenderer.GetCloseBitmap(CTxt3, 14);
        close.ImageAlign = ContentAlignment.MiddleCenter;
        close.Padding    = Padding.Empty;
        close.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
        close.Bounds = new Rectangle(FW-2-52, 0, 52, TH);
        close.MouseEnter += (_, _) => close.Image = IconRenderer.GetCloseBitmap(Color.White, 14);
        close.MouseLeave += (_, _) => close.Image = IconRenderer.GetCloseBitmap(CTxt3, 14);
        close.Click += (_, _) => Close();
        bar.Controls.Add(close);

        s.Controls.Add(new Panel { Bounds = new Rectangle(0, TH, FW-2, 1), BackColor = CBorder });
    }

    // ── Instance bar ──────────────────────────────────────────────────────────

    private void BuildInstanceBar(Panel s)
    {
        int barY  = TH + 1;
        int btnGap = 5;

        var bar = new Panel { Bounds = new Rectangle(0, barY, FW-2, IH), BackColor = CBg };
        s.Controls.Add(bar);

        // Create combo first — WinForms sets its Height based on font, ignores explicit Height
        _instCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = CInputBg,
            ForeColor     = CTxt,
            Font          = _fInput,
            FlatStyle     = FlatStyle.Flat,
        };
        bar.Controls.Add(_instCombo);

        // Use the natural combo height as the reference for all elements
        int elemH  = _instCombo.Height;
        int elemY  = (IH - elemH) / 2;
        int btnSz  = elemH;          // buttons match combo height → same line

        // Label — same height + vertical alignment
        var lbl = new Label
        {
            Text      = Loc.T("inst_label", _lang),
            Font      = _fLabel,
            ForeColor = CTxt2,
            AutoSize  = false,
            Size      = new Size(70, elemH),
            Location  = new Point(Pad, elemY),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(0, 2, 0, 0),
        };
        bar.Controls.Add(lbl);

        // Render button icons from SVG paths
        int iconSz = Math.Max(12, btnSz - 8);
        _imgStarGold = IconRenderer.GetStarBitmap(Color.FromArgb(249, 178, 51), iconSz);
        _imgStarGray = IconRenderer.GetStarBitmap(Color.FromArgb(160, 160, 175), iconSz);
        _imgAdd      = IconRenderer.RenderSvgBitmap([_svgAdd],  Color.FromArgb(149, 193,  31), iconSz);
        _imgDel      = IconRenderer.RenderSvgBitmap(_svgDel,    Color.FromArgb(227,   6,  19), iconSz);

        // Buttons right-aligned: ★  ＋  🗑
        int rightEdge = FW - 2 - Pad;
        int totalBtns = btnSz * 3 + btnGap * 2;
        int bx = rightEdge - totalBtns;

        _btnStar = MkIconBtn(_imgStarGray, bx, elemY, btnSz);
        _btnStar.Click += (_, _) => SetCurrentAsDefault();
        bar.Controls.Add(_btnStar);

        bx += btnSz + btnGap;
        _btnAdd = MkIconBtn(_imgAdd, bx, elemY, btnSz);
        _btnAdd.Click += (_, _) => AddInstance();
        bar.Controls.Add(_btnAdd);

        bx += btnSz + btnGap;
        _btnDel = MkIconBtn(_imgDel, bx, elemY, btnSz);
        _btnDel.Click += (_, _) => DeleteCurrentInstance();
        bar.Controls.Add(_btnDel);

        // Combo fills remaining space between label and buttons
        int comboX = Pad + lbl.Width + 8;
        int comboW = (rightEdge - totalBtns - btnGap) - comboX;
        _instCombo.Location     = new Point(comboX, elemY);
        _instCombo.Width        = comboW;
        _instCombo.DropDownWidth = comboW;

        RefreshComboItems();

        _instCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressComboChange) return;
            SaveCurrentFields();
            _selectedIdx = _instCombo.SelectedIndex;
            LoadCurrentFields();
            RefreshBarButtons();
        };

        RefreshBarButtons();
        s.Controls.Add(new Panel { Bounds = new Rectangle(0, barY + IH, FW-2, 1), BackColor = CBorder });
    }

    private Button MkIconBtn(Bitmap icon, int x, int y, int sz)
    {
        var b = new Button
        {
            Text      = "",
            Image     = icon,
            ImageAlign = ContentAlignment.MiddleCenter,
            Bounds    = new Rectangle(x, y, sz, sz),
            BackColor = CBtnBg,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Padding                 = Padding.Empty,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = CBtnH;
        return b;
    }

    // ── Instance bar logic ────────────────────────────────────────────────────

    private void RefreshComboItems()
    {
        _suppressComboChange = true;
        try
        {
            _instCombo.Items.Clear();
            foreach (var inst in _instances)
                _instCombo.Items.Add(inst.IsDefault ? $"★  {inst.Name}" : $"    {inst.Name}");
            if (_selectedIdx < _instCombo.Items.Count)
                _instCombo.SelectedIndex = _selectedIdx;
        }
        finally
        {
            _suppressComboChange = false;
        }
    }

    private void RefreshBarButtons()
    {
        bool isDefault = _selectedIdx < _instances.Count && _instances[_selectedIdx].IsDefault;
        bool canStar   = !isDefault && _instances.Count > 1;
        bool canDel    = _instances.Count > 1;

        // ★: gold image when already default, gray when can be set — never disable
        //    (disabled WinForms buttons gray out their image, losing the gold tint)
        _btnStar.Image  = isDefault ? _imgStarGold : _imgStarGray;
        _btnStar.Cursor = canStar ? Cursors.Hand : Cursors.Default;

        // 🗑: only if more than one instance
        _btnDel.Enabled = canDel;
        _btnDel.Cursor  = canDel ? Cursors.Hand : Cursors.Default;
    }

    private void SetCurrentAsDefault()
    {
        if (_selectedIdx >= _instances.Count) return;
        if (_instances[_selectedIdx].IsDefault) return; // already default

        SaveCurrentFields();
        foreach (var i in _instances) i.IsDefault = false;
        _instances[_selectedIdx].IsDefault = true;
        RefreshComboItems();
        RefreshBarButtons();
    }

    private void AddInstance()
    {
        SaveCurrentFields();
        _instances.Add(new PiHoleInstance { Name = "Pi-Hole", PiholeUrl = "http://pi.hole" });
        _selectedIdx = _instances.Count - 1;
        RefreshComboItems();
        LoadCurrentFields();
        RefreshBarButtons();
        _nameTb.Focus();
        _nameTb.SelectAll();
    }

    private void DeleteCurrentInstance()
    {
        if (_instances.Count <= 1) return;

        bool wasDefault = _instances[_selectedIdx].IsDefault;
        _instances.RemoveAt(_selectedIdx);
        if (wasDefault) _instances[0].IsDefault = true;

        _selectedIdx = Math.Min(_selectedIdx, _instances.Count - 1);
        RefreshComboItems();
        LoadCurrentFields();
        RefreshBarButtons();
    }

    private void SaveCurrentFields()
    {
        if (_selectedIdx < 0 || _selectedIdx >= _instances.Count) return;
        var inst = _instances[_selectedIdx];
        inst.Name       = _nameTb.Text.Trim();
        inst.PiholeUrl  = _urlTb.Text.Trim();
        inst.ApiKey     = _pwTb.Text.Trim();
        inst.ApiVersion = _rbV6.Checked ? 6 : 5;
        // Update only this item's label — suppress flag prevents SelectedIndexChanged re-entry
        if (_selectedIdx < _instCombo.Items.Count)
        {
            _suppressComboChange = true;
            _instCombo.Items[_selectedIdx] = inst.IsDefault ? $"★  {inst.Name}" : $"    {inst.Name}";
            _suppressComboChange = false;
        }
    }

    private void LoadCurrentFields()
    {
        if (_selectedIdx < 0 || _selectedIdx >= _instances.Count) return;
        var inst = _instances[_selectedIdx];
        _nameTb.Text  = inst.Name;
        _urlTb.Text   = inst.PiholeUrl;
        _pwTb.Text    = inst.ApiKey;
        _rbV6.Checked = inst.ApiVersion == 6;
        _rbV5.Checked = inst.ApiVersion == 5;
    }

    // ── Content ───────────────────────────────────────────────────────────────

    private void BuildContent(Panel s)
    {
        int cTop = TH + 1 + IH + 1 + 18;

        BuildLeft(s, cTop);
        BuildRight(s, cTop);

        int divH = FH - 2 - TH - 1 - IH - 1 - BH - 1;
        s.Controls.Add(new Panel
        {
            Bounds    = new Rectangle(LX + CW + CGap/2, TH + 1 + IH + 1 + 18, 1, divH),
            BackColor = CBorder,
        });
    }

    private void BuildLeft(Panel s, int y0)
    {
        int y = y0;

        AddSection(s, Loc.T("connection", _lang), LX, ref y);

        AddFieldLabel(s, "Name", LX, ref y);
        _nameTb = AddInput(s, LX, y, CW); y += InH + 20;
        _nameTb.Text = _instances[_selectedIdx].Name;

        AddFieldLabel(s, Loc.T("url", _lang), LX, ref y);
        _urlTb = AddInput(s, LX, y, CW); y += InH + 14;
        _urlTb.Text = _instances[_selectedIdx].PiholeUrl;
        AddHint(s, Loc.T("url_hint", _lang), LX, ref y);

        AddFieldLabel(s, Loc.T("password", _lang), LX, ref y);
        _pwTb = AddInput(s, LX, y, CW, password: true); y += InH + 14;
        _pwTb.Text = _instances[_selectedIdx].ApiKey;
        AddHint(s, Loc.T("pw_hint", _lang), LX, ref y);

        AddFieldLabel(s, Loc.T("version", _lang), LX, ref y);
        _rbV6 = AddRadio(s, Loc.T("v6_label", _lang), LX,       y, _instances[_selectedIdx].ApiVersion == 6);
        _rbV5 = AddRadio(s, Loc.T("v5_label", _lang), LX + 128, y, _instances[_selectedIdx].ApiVersion == 5);
    }

    private void BuildRight(Panel s, int y0)
    {
        int y = y0;

        AddSection(s, Loc.T("options", _lang), RX, ref y);

        AddFieldLabel(s, Loc.T("poll_interval", _lang), RX, ref y);
        _pollNud = new NumericUpDown
        {
            Bounds      = new Rectangle(RX, y, 72, InH),
            Minimum     = 3, Maximum = 120,
            Value       = Math.Clamp(_cfg.PollInterval, 3, 120),
            BackColor   = CInputBg,
            ForeColor   = CTxt,
            Font        = _fInput,
            BorderStyle = BorderStyle.FixedSingle,
        };
        s.Controls.Add(_pollNud);
        s.Controls.Add(new Label { Text = Loc.T("seconds", _lang), Font = _fLabel, ForeColor = CTxt3,
                                   AutoSize = true, Location = new Point(RX+78, y+10),
                                   BackColor = Color.Transparent });
        y += InH + 20;

        _autoChk = new CheckBox
        {
            Text      = Loc.T("autostart", _lang),
            Checked   = _cfg.Autostart,
            AutoSize  = true,
            Font      = _fLabel,
            ForeColor = CTxt,
            Location  = new Point(RX, y),
            BackColor = Color.Transparent,
        };
        s.Controls.Add(_autoChk);
        y += 26 + 24;

        AddFieldLabel(s, Loc.T("language", _lang), RX, ref y);
        _langCombo = new ComboBox
        {
            Bounds        = new Rectangle(RX, y, CW, InH),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = CInputBg,
            ForeColor     = CTxt,
            Font          = _fInput,
            FlatStyle     = FlatStyle.Flat,
        };
        foreach (var name in Loc.Langs.Values)
            _langCombo.Items.Add(name);
        if (Loc.Langs.TryGetValue(Loc.GetEffectiveLang(_cfg.Language), out var cur))
            _langCombo.SelectedItem = cur;
        s.Controls.Add(_langCombo);
        y += InH + 24;

        AddFieldLabel(s, Loc.T("status", _lang), RX, ref y);
        _statusLbl = new Label
        {
            Text      = Loc.T("ready", _lang),
            Font      = _fLabel,
            ForeColor = CTxt3,
            AutoSize  = false,
            Bounds    = new Rectangle(RX, y, CW, 60),
            BackColor = Color.Transparent,
        };
        s.Controls.Add(_statusLbl);
    }

    // ── Button bar ────────────────────────────────────────────────────────────

    private void BuildButtonBar(Panel s)
    {
        int barY = FH - 2 - BH;
        s.Controls.Add(new Panel { Bounds = new Rectangle(0, barY, FW-2, 1), BackColor = CBorder });

        int btnY = barY + (BH - 34) / 2;
        int r    = FW - 2 - Pad;

        var save   = MkBtn(Loc.T("save",            _lang), CAccent, Color.White);
        var cancel = MkBtn(Loc.T("cancel",          _lang), CBtnBg,  CTxt);
        var test   = MkBtn(Loc.T("test_connection", _lang), CBtnBg,  CTxt);

        save.FlatAppearance.MouseOverBackColor   = CAccentH;
        cancel.FlatAppearance.MouseOverBackColor = CBtnH;
        test.FlatAppearance.MouseOverBackColor   = CBtnH;

        int sw = BtnWidth(save);
        int cw = BtnWidth(cancel);
        int tw = BtnWidth(test);

        save.Bounds   = new Rectangle(r - sw,          btnY, sw, 34);
        cancel.Bounds = new Rectangle(r - sw - 8 - cw, btnY, cw, 34);
        test.Bounds   = new Rectangle(Pad,             btnY, tw, 34);

        save.Click   += (_, _) => DoSave();
        cancel.Click += (_, _) => Close();
        test.Click   += async (_, _) => await DoTestAsync();

        s.Controls.Add(save);
        s.Controls.Add(cancel);
        s.Controls.Add(test);
    }

    // ── Control factories ─────────────────────────────────────────────────────

    private TextBox AddInput(Panel parent, int x, int y, int w, bool password = false)
    {
        var wrapper = new Panel { Bounds = new Rectangle(x, y, w, InH), BackColor = CInputBg };
        var tb = new TextBox
        {
            BorderStyle  = BorderStyle.None,
            BackColor    = CInputBg,
            ForeColor    = CTxt,
            Font         = _fInput,
            PasswordChar = password ? '●' : '\0',
        };

        void LayOut() => tb.Bounds = new Rectangle(7, (wrapper.Height - tb.PreferredHeight) / 2,
                                                    Math.Max(1, wrapper.Width - 14), tb.PreferredHeight);
        wrapper.Layout += (_, _) => LayOut();
        LayOut();

        wrapper.Paint += (_, e) =>
        {
            bool f = tb.Focused;
            using var pen = new Pen(f ? CAccent : CInputBd, f ? 2f : 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, wrapper.Width-1, wrapper.Height-1);
        };
        tb.GotFocus  += (_, _) => wrapper.Invalidate();
        tb.LostFocus += (_, _) => wrapper.Invalidate();

        wrapper.Controls.Add(tb);
        parent.Controls.Add(wrapper);
        return tb;
    }

    private void AddSection(Panel p, string text, int x, ref int y)
    {
        p.Controls.Add(new Label { Text = text.ToUpperInvariant(), Font = _fSection,
                                   ForeColor = CAccent, AutoSize = true,
                                   Location = new Point(x, y), BackColor = Color.Transparent });
        y += 18 + 16;
    }

    private void AddFieldLabel(Panel p, string text, int x, ref int y)
    {
        p.Controls.Add(new Label { Text = text, Font = _fLabel, ForeColor = CTxt2,
                                   AutoSize = true, Location = new Point(x, y),
                                   BackColor = Color.Transparent });
        y += 16 + 14;
    }

    private void AddHint(Panel p, string text, int x, ref int y)
    {
        p.Controls.Add(new Label { Text = text, Font = _fHint, ForeColor = CTxt3,
                                   AutoSize = true, Location = new Point(x, y),
                                   BackColor = Color.Transparent });
        y += 14 + 20;
    }

    private RadioButton AddRadio(Panel p, string text, int x, int y, bool chk) =>
        Add(p, new RadioButton { Text = text, Checked = chk, AutoSize = true,
                                 Font = _fLabel, ForeColor = CTxt,
                                 Location = new Point(x, y), BackColor = Color.Transparent });

    private Button MkBtn(string text, Color bg, Color fg)
    {
        var b = new Button
        {
            Text      = text,
            BackColor = bg,
            ForeColor = fg,
            Font      = _fBtn,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = bg;
        return b;
    }

    private int BtnWidth(Button b)
    {
        using var g = CreateGraphics();
        return (int)g.MeasureString(b.Text, b.Font).Width + Pad * 2;
    }

    private static T Add<T>(Panel p, T ctrl) where T : Control { p.Controls.Add(ctrl); return ctrl; }

    // ── Drag support ──────────────────────────────────────────────────────────

    private void Draggable(Control c)
    {
        c.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) _drag = e.Location; };
        c.MouseMove += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            Location = new Point(Location.X + e.X - _drag.X, Location.Y + e.Y - _drag.Y);
        };
    }

    // ── Logic ─────────────────────────────────────────────────────────────────

    private async Task DoTestAsync()
    {
        var url = _urlTb.Text.Trim();
        if (string.IsNullOrEmpty(url)) { SetStatus(Loc.T("enter_url", _lang), COrange); return; }
        SetStatus(Loc.T("testing", _lang), CTxt3);

        var api = new PiHoleApi(url, _pwTb.Text.Trim(), _rbV6.Checked ? 6 : 5);
        var (ok, msg) = await api.TestAsync(_lang);
        api.Dispose();
        SetStatus($"{(ok ? "✓" : "✗")}  {msg.Replace("\n", "  •  ")}", ok ? CAccent : CRed);
    }

    private void DoSave()
    {
        SaveCurrentFields();

        _cfg.Instances    = _instances;
        _cfg.PollInterval = (int)_pollNud.Value;
        _cfg.Autostart    = _autoChk.Checked;
        var langName      = _langCombo.SelectedItem?.ToString() ?? "";
        _cfg.Language     = Loc.Langs.FirstOrDefault(kv => kv.Value == langName).Key ?? "";

        ConfigManager.Save(_cfg);
        ConfigManager.SetAutostart(_cfg.Autostart);
        _onSave?.Invoke(_cfg);
        Close();
    }

    private void SetStatus(string text, Color color)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(text, color)); return; }
        _statusLbl.Text      = text;
        _statusLbl.ForeColor = color;
    }

    // ── Positioning ───────────────────────────────────────────────────────────

    private void PositionNearTaskbar()
    {
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(
            Math.Max(wa.Left + 8, wa.Right  - Width  - 12),
            Math.Max(wa.Top  + 8, wa.Bottom - Height - 12));
    }

    // ── Border ────────────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var shadow = new Pen(Color.FromArgb(55, 0, 0, 0));
        e.Graphics.DrawRectangle(shadow, 0, 0, Width-1, Height-1);
        using var border = new Pen(CBorder);
        e.Graphics.DrawRectangle(border, 1, 1, Width-3, Height-3);
    }

    // ── Dispose fonts ─────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fTitle.Dispose(); _fSection.Dispose(); _fLabel.Dispose();
            _fHint.Dispose();  _fInput.Dispose();   _fBtn.Dispose();
            _imgAdd?.Dispose(); _imgDel?.Dispose();
        }
        base.Dispose(disposing);
    }
}
