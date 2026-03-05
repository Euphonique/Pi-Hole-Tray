using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PiHoleTray;

class TrayApp : ApplicationContext, IDisposable
{
    private AppConfig _cfg;
    private string    _lang;

    private record InstanceState(PiHoleInstance Cfg, PiHoleApi Api)
    {
        public string? Status { get; set; }
    }
    private List<InstanceState> _instances = [];

    private readonly NotifyIcon _tray;
    private ContextMenuStrip    _menu;

    // Top-level menu items for the default instance
    private ToolStripMenuItem? _miEnable;
    private ToolStripMenuItem? _miDisable;

    private System.Threading.Timer? _timer;
    private bool _polling  = false;
    private bool _disposed = false;
    private SettingsForm? _settingsWin;
    private QueryLogForm? _queryLogWin;
    private readonly SynchronizationContext _uiContext;

    private readonly List<(string Domain, DateTime RemoveAt)> _tempAllows = [];

    // ── Logging ───────────────────────────────────────────────────────────────

    private static readonly StreamWriter _log = OpenLog();

    private static StreamWriter OpenLog()
    {
        try { return new StreamWriter(ConfigManager.LogPath, append: true) { AutoFlush = true }; }
        catch { return StreamWriter.Null; }
    }

    private static void Log(string msg) =>
        _log.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO] {msg}");

    // ── Constructor ───────────────────────────────────────────────────────────

    public TrayApp()
    {
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _cfg  = ConfigManager.Load();
        _lang = Loc.GetEffectiveLang(_cfg.Language);

        RebuildInstances();

        _menu = BuildMenu();
        _tray = new NotifyIcon
        {
            Icon    = IconRenderer.GetIcon("enabled", 64),
            Text    = "Pi-Hole Tray",
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ToggleDefault();
        };

        Log($"Start — {_cfg.Instances.Count} instance(s) lang={_lang}");
        StartPolling();
    }

    private void RebuildInstances()
    {
        foreach (var s in _instances) s.Api.Dispose();
        _instances = _cfg.Instances
            .Select(i => new InstanceState(i, new PiHoleApi(i.PiholeUrl, i.ApiKey, i.ApiVersion)))
            .ToList();
    }

    private InstanceState? DefaultInstance =>
        _instances.FirstOrDefault(s => s.Cfg.IsDefault) ?? _instances.FirstOrDefault();

    // ── Tray menu ─────────────────────────────────────────────────────────────

    private ContextMenuStrip BuildMenu()
    {
        var L    = _lang;
        var menu = new ContextMenuStrip();
        var def  = DefaultInstance;

        if (def != null)
        {
            // Default instance: flat top-level items (same layout as single-instance)
            _miEnable  = new ToolStripMenuItem(Loc.T("menu_enable",  L));
            _miDisable = new ToolStripMenuItem(Loc.T("menu_disable", L));
            _miDisable.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            _miEnable.Click  += async (_, _) => await DoEnableAsync(def);
            _miDisable.Click += async (_, _) => await DoDisableAsync(def);

            var timedMenu = new ToolStripMenuItem(Loc.T("menu_timed", L));
            foreach (var (label, sec) in TimedOptions(L))
            {
                int s    = sec;
                var item = new ToolStripMenuItem(label);
                item.Click += async (_, _) => await DoDisableAsync(def, s);
                timedMenu.DropDownItems.Add(item);
            }

            menu.Items.Add(_miEnable);
            menu.Items.Add(_miDisable);
            menu.Items.Add(timedMenu);
            menu.Items.Add(Loc.T("menu_querylog",  L), null, (_, _) => OpenQueryLog(def));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Loc.T("menu_dashboard", L), null, (_, _) => OpenDashboard(def.Cfg));
        }

        // Additional instances as submenus — only shown if there are more than one
        var others = _instances.Where(s => !s.Cfg.IsDefault).ToList();
        if (others.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            foreach (var state in others)
                menu.Items.Add(BuildInstanceMenuItem(state));
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("menu_settings", L), null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("menu_quit",     L), null, (_, _) => Quit());

        menu.Opening += (_, _) => RefreshMenuItems();
        return menu;
    }

    private ToolStripMenuItem BuildInstanceMenuItem(InstanceState state)
    {
        var L    = _lang;
        var root = new ToolStripMenuItem
        {
            Tag   = state,
            Text  = state.Cfg.Name,
            Image = IconRenderer.GetStatusBitmap(state.Status ?? "unknown", 16),
        };

        var miEnable  = new ToolStripMenuItem(Loc.T("menu_enable",  L));
        var miDisable = new ToolStripMenuItem(Loc.T("menu_disable", L));
        miDisable.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        miEnable.Click  += async (_, _) => await DoEnableAsync(state);
        miDisable.Click += async (_, _) => await DoDisableAsync(state);

        var timedMenu = new ToolStripMenuItem(Loc.T("menu_timed", L));
        foreach (var (label, sec) in TimedOptions(L))
        {
            int s    = sec;
            var item = new ToolStripMenuItem(label);
            item.Click += async (_, _) => await DoDisableAsync(state, s);
            timedMenu.DropDownItems.Add(item);
        }

        var miQueryLog = new ToolStripMenuItem(Loc.T("menu_querylog",  L));
        var miDash     = new ToolStripMenuItem(Loc.T("menu_dashboard", L));
        miQueryLog.Click += (_, _) => OpenQueryLog(state);
        miDash.Click     += (_, _) => OpenDashboard(state.Cfg);

        var miSetDefault = new ToolStripMenuItem(Loc.T("menu_set_default", L))
        {
            Image = IconRenderer.GetStarBitmap(Color.FromArgb(200, 150, 0), 16),
        };
        miSetDefault.Click += (_, _) => SetInstanceAsDefault(state);

        root.DropDownItems.Add(miEnable);
        root.DropDownItems.Add(miDisable);
        root.DropDownItems.Add(timedMenu);
        root.DropDownItems.Add(miQueryLog);
        root.DropDownItems.Add(new ToolStripSeparator());
        root.DropDownItems.Add(miDash);
        root.DropDownItems.Add(new ToolStripSeparator());
        root.DropDownItems.Add(miSetDefault);

        return root;
    }

    private void SetInstanceAsDefault(InstanceState state)
    {
        foreach (var s in _instances) s.Cfg.IsDefault = false;
        state.Cfg.IsDefault = true;
        ConfigManager.Save(_cfg);

        var oldMenu = _menu;
        _menu = BuildMenu();
        _tray.ContextMenuStrip = _menu;
        oldMenu.Dispose();
        UpdateTray();
    }

    private static (string label, int sec)[] TimedOptions(string L) =>
    [
        (Loc.T("menu_5min",  L),   300),
        (Loc.T("menu_10min", L),   600),
        (Loc.T("menu_30min", L),  1800),
        (Loc.T("menu_1h",    L),  3600),
        (Loc.T("menu_2h",    L),  7200),
        (Loc.T("menu_5h",    L), 18000),
    ];

    private void RefreshMenuItems()
    {
        var def = DefaultInstance;

        // Top-level enable/disable visibility
        if (_miEnable  != null) _miEnable.Visible  = def?.Status != "enabled";
        if (_miDisable != null) _miDisable.Visible = def?.Status == "enabled";

        // Update non-default instance submenu labels + icons + enable/disable visibility
        foreach (ToolStripItem item in _menu.Items)
        {
            if (item is not ToolStripMenuItem mi || mi.Tag is not InstanceState state) continue;
            mi.Text  = state.Cfg.Name;
            mi.Image = IconRenderer.GetStatusBitmap(state.Status ?? "unknown", 16);
            if (mi.DropDownItems.Count >= 2)
            {
                mi.DropDownItems[0].Visible = state.Status != "enabled";
                mi.DropDownItems[1].Visible = state.Status == "enabled";
            }
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void ToggleDefault()
    {
        var def = DefaultInstance;
        if (def == null) return;
        _ = def.Status == "enabled" ? DoDisableAsync(def) : DoEnableAsync(def);
    }

    private async Task DoEnableAsync(InstanceState state)
    {
        if (await state.Api.EnableAsync()) state.Status = "enabled";
        UpdateTray();
    }

    private async Task DoDisableAsync(InstanceState state, int seconds = 0)
    {
        if (await state.Api.DisableAsync(seconds)) state.Status = "disabled";
        UpdateTray();
    }

    private void OpenDashboard(PiHoleInstance inst)
    {
        try
        {
            var url = inst.PiholeUrl.TrimEnd('/') + "/admin";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void OpenSettings()
    {
        if (_settingsWin != null && !_settingsWin.IsDisposed)
        {
            _settingsWin.BringToFront();
            _settingsWin.Focus();
            return;
        }
        _settingsWin = new SettingsForm(_cfg, DefaultInstance?.Status ?? "unknown", OnSaved);
        _settingsWin.FormClosed += (_, _) => _settingsWin = null;
        _settingsWin.Show();
    }

    private void OnSaved(AppConfig cfg)
    {
        _cfg  = cfg;
        _lang = Loc.GetEffectiveLang(cfg.Language);

        RebuildInstances();

        var oldMenu = _menu;
        _menu = BuildMenu();
        _tray.ContextMenuStrip = _menu;
        oldMenu.Dispose();

        UpdateTray();
        RestartTimer();
        Log($"Config saved — {cfg.Instances.Count} instance(s) lang={_lang}");
    }

    private void OpenQueryLog(InstanceState state)
    {
        if (_queryLogWin != null && !_queryLogWin.IsDisposed)
        {
            _queryLogWin.BringToFront();
            _queryLogWin.Focus();
            return;
        }
        _queryLogWin = new QueryLogForm(state.Api, _lang, ScheduleTempAllow);
        _queryLogWin.Text = state.Cfg.Name;
        _queryLogWin.FormClosed += (_, _) => _queryLogWin = null;
        _queryLogWin.Show();
    }

    public void ScheduleTempAllow(string domain, int minutes)
    {
        lock (_tempAllows)
        {
            _tempAllows.RemoveAll(x => x.Domain == domain);
            _tempAllows.Add((domain, DateTime.UtcNow.AddMinutes(minutes)));
        }
        Log($"Temp-allow scheduled: {domain} for {minutes} min");
    }

    private async Task CheckTempAllowsAsync()
    {
        List<string> expired;
        lock (_tempAllows)
        {
            expired = _tempAllows.Where(x => DateTime.UtcNow >= x.RemoveAt)
                                  .Select(x => x.Domain).ToList();
            _tempAllows.RemoveAll(x => expired.Contains(x.Domain));
        }
        var def = DefaultInstance;
        if (def == null) return;
        foreach (var domain in expired)
        {
            try
            {
                await def.Api.RemoveDomainAsync(domain).ConfigureAwait(false);
                Log($"Temp-allow expired, removed: {domain}");
            }
            catch { }
        }
    }

    private void Quit()
    {
        _timer?.Dispose();
        _tray.Visible = false;
        Application.Exit();
    }

    // ── Polling ───────────────────────────────────────────────────────────────

    private void StartPolling()
    {
        var interval = TimeSpan.FromSeconds(Math.Max(3, _cfg.PollInterval));
        _timer = new System.Threading.Timer(async _ => await PollAsync(), null, TimeSpan.Zero, interval);
    }

    private void RestartTimer()
    {
        _timer?.Dispose();
        StartPolling();
    }

    private async Task PollAsync()
    {
        if (_polling) return;
        _polling = true;
        try
        {
            await CheckTempAllowsAsync().ConfigureAwait(false);

            var tasks = _instances.Select(async state =>
            {
                try
                {
                    var s = await state.Api.GetStatusAsync().ConfigureAwait(false);
                    if (s != null) state.Status = s;
                }
                catch (Exception ex)
                {
                    Log($"Poll error [{state.Cfg.Name}]: {ex.Message}");
                }
            });
            await Task.WhenAll(tasks).ConfigureAwait(false);

            UpdateTray();
        }
        finally
        {
            _polling = false;
        }
    }

    // ── Tray update ───────────────────────────────────────────────────────────

    private void UpdateTray()
    {
        if (_disposed) return;
        try
        {
            var def     = DefaultInstance;
            var icon    = IconRenderer.GetIcon(def?.Status ?? "unknown", 64);
            var tooltip = BuildTooltip(def);
            _uiContext.Post(_ => ApplyTray(icon, tooltip), null);
        }
        catch { }
    }

    private void ApplyTray(Icon icon, string tooltip)
    {
        try
        {
            _tray.Icon = icon;
            _tray.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
        }
        catch { }
    }

    private string BuildTooltip(InstanceState? def)
    {
        var label = def?.Status switch
        {
            "enabled"  => Loc.T("tray_active",   _lang),
            "disabled" => Loc.T("tray_disabled",  _lang),
            _          => Loc.T("tray_noconn",    _lang),
        };
        return $"{def?.Cfg.Name ?? "Pi-Hole"}: {label}";
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            _timer?.Dispose();
            _tray.Dispose();
            _menu.Dispose();
            foreach (var s in _instances) s.Api.Dispose();
        }
        base.Dispose(disposing);
    }
}
