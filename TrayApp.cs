using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
        public bool ClientUnblocked { get; set; }
    }
    private List<InstanceState> _instances = [];

    private readonly NotifyIcon _tray;
    private ContextMenuStrip    _menu;

    // Top-level menu items (default instance or all-mode)
    private ToolStripMenuItem? _miEnable;
    private ToolStripMenuItem? _miDisable;
    private ToolStripMenuItem? _miClearDefault;

    // Per-client menu items (v6 only)
    private ToolStripMenuItem? _miEnableClient;
    private ToolStripMenuItem? _miDisableClient;
    private ToolStripMenuItem? _miTimedClient;

    private System.Threading.Timer? _timer;
    private bool _polling  = false;
    private bool _disposed = false;
    private SettingsForm? _settingsWin;
    private QueryLogForm? _queryLogWin;
    private AboutForm?    _aboutWin;
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
            if (e.Button == MouseButtons.Left) HandleLeftClick();
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
        _instances.FirstOrDefault(s => s.Cfg.IsDefault);

    // ── Tray menu ─────────────────────────────────────────────────────────────

    private ContextMenuStrip BuildMenu()
    {
        var L    = _lang;
        var menu = new ContextMenuStrip();
        var def  = DefaultInstance;

        _miEnable       = null;
        _miDisable      = null;
        _miClearDefault = null;
        _miEnableClient  = null;
        _miDisableClient = null;
        _miTimedClient   = null;

        if (def != null)
        {
            // Default instance: flat top-level items
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

            _miClearDefault = new ToolStripMenuItem(Loc.T("menu_clear_default", L))
            {
                Image = IconRenderer.GetStarBitmap(Color.Silver, 16),
            };
            _miClearDefault.Click += (_, _) => ClearDefault();

            menu.Items.Add(_miEnable);
            menu.Items.Add(_miDisable);
            menu.Items.Add(timedMenu);

            // Per-client items (v6 only)
            if (def.Cfg.ApiVersion == 6)
            {
                menu.Items.Add(new ToolStripSeparator());

                _miEnableClient  = new ToolStripMenuItem(Loc.T("menu_enable_client",  L));
                _miDisableClient = new ToolStripMenuItem(Loc.T("menu_disable_client", L));
                _miEnableClient.Click  += async (_, _) => await DoEnableClientAsync(def);
                _miDisableClient.Click += async (_, _) => await DoDisableClientAsync(def);

                _miTimedClient = new ToolStripMenuItem(Loc.T("menu_timed_client", L));
                foreach (var (label, sec) in TimedOptions(L))
                {
                    int s    = sec;
                    var item = new ToolStripMenuItem(label);
                    item.Click += async (_, _) => await DoDisableClientTimedAsync(def, s);
                    _miTimedClient.DropDownItems.Add(item);
                }

                menu.Items.Add(_miEnableClient);
                menu.Items.Add(_miDisableClient);
                menu.Items.Add(_miTimedClient);
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Loc.T("menu_querylog",  L), null, (_, _) => OpenQueryLog(def));
            menu.Items.Add(Loc.T("menu_dashboard", L), null, (_, _) => OpenDashboard(def.Cfg));
            menu.Items.Add(_miClearDefault);
        }
        else if (_instances.Count > 0)
        {
            // No default — top-level items control ALL instances
            _miEnable  = new ToolStripMenuItem(Loc.T("menu_enable_all",  L));
            _miDisable = new ToolStripMenuItem(Loc.T("menu_disable_all", L));
            _miDisable.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _miEnable.Click  += async (_, _) => await DoEnableAllAsync();
            _miDisable.Click += async (_, _) => await DoDisableAllAsync();

            var timedMenu = new ToolStripMenuItem(Loc.T("menu_timed", L));
            foreach (var (label, sec) in TimedOptions(L))
            {
                int s    = sec;
                var item = new ToolStripMenuItem(label);
                item.Click += async (_, _) => await DoDisableAllAsync(s);
                timedMenu.DropDownItems.Add(item);
            }

            menu.Items.Add(_miEnable);
            menu.Items.Add(_miDisable);
            menu.Items.Add(timedMenu);

            // Per-client items if any v6 instance exists
            var firstV6 = _instances.FirstOrDefault(s => s.Cfg.ApiVersion == 6);
            if (firstV6 != null)
            {
                menu.Items.Add(new ToolStripSeparator());

                _miEnableClient  = new ToolStripMenuItem(Loc.T("menu_enable_client",  L));
                _miDisableClient = new ToolStripMenuItem(Loc.T("menu_disable_client", L));
                _miEnableClient.Click  += async (_, _) => await DoEnableClientAsync(firstV6);
                _miDisableClient.Click += async (_, _) => await DoDisableClientAsync(firstV6);

                _miTimedClient = new ToolStripMenuItem(Loc.T("menu_timed_client", L));
                foreach (var (label, sec) in TimedOptions(L))
                {
                    int s    = sec;
                    var item = new ToolStripMenuItem(label);
                    item.Click += async (_, _) => await DoDisableClientTimedAsync(firstV6, s);
                    _miTimedClient.DropDownItems.Add(item);
                }

                menu.Items.Add(_miEnableClient);
                menu.Items.Add(_miDisableClient);
                menu.Items.Add(_miTimedClient);
            }
        }

        // Instance submenus: non-default when a default exists, all instances otherwise
        var others = def != null
            ? _instances.Where(s => !s.Cfg.IsDefault).ToList()
            : _instances.ToList();
        if (others.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            foreach (var state in others)
                menu.Items.Add(BuildInstanceMenuItem(state));
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("menu_settings", L), null, (_, _) => OpenSettings());
        menu.Items.Add(Loc.T("menu_about",    L), null, (_, _) => OpenAbout());
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

        // Per-client items in instance submenu (v6 only)
        if (state.Cfg.ApiVersion == 6)
        {
            root.DropDownItems.Add(new ToolStripSeparator());

            var miEnableC  = new ToolStripMenuItem(Loc.T("menu_enable_client",  L));
            var miDisableC = new ToolStripMenuItem(Loc.T("menu_disable_client", L));
            miEnableC.Click  += async (_, _) => await DoEnableClientAsync(state);
            miDisableC.Click += async (_, _) => await DoDisableClientAsync(state);

            var timedClientMenu = new ToolStripMenuItem(Loc.T("menu_timed_client", L));
            foreach (var (lbl, sc) in TimedOptions(L))
            {
                int sv   = sc;
                var itm  = new ToolStripMenuItem(lbl);
                itm.Click += async (_, _) => await DoDisableClientTimedAsync(state, sv);
                timedClientMenu.DropDownItems.Add(itm);
            }

            root.DropDownItems.Add(miEnableC);
            root.DropDownItems.Add(miDisableC);
            root.DropDownItems.Add(timedClientMenu);
        }

        root.DropDownItems.Add(new ToolStripSeparator());
        root.DropDownItems.Add(miQueryLog);
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

    private void ClearDefault()
    {
        foreach (var s in _instances) s.Cfg.IsDefault = false;
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

        if (def != null)
        {
            // Default mode: show Enable/Disable based on default instance status
            if (_miEnable  != null) _miEnable.Visible  = def.Status != "enabled";
            if (_miDisable != null) _miDisable.Visible = def.Status == "enabled";

            // Per-client: show Enable/Disable based on client unblocked state
            if (_miEnableClient  != null) _miEnableClient.Visible  = def.ClientUnblocked;
            if (_miDisableClient != null) _miDisableClient.Visible = !def.ClientUnblocked;
            if (_miTimedClient   != null) _miTimedClient.Visible   = !def.ClientUnblocked;
        }
        else
        {
            // All mode: show Enable All if not all enabled, Disable All if not all disabled
            bool allEnabled  = _instances.Count > 0 && _instances.All(s => s.Status == "enabled");
            bool allDisabled = _instances.Count > 0 && _instances.All(s => s.Status == "disabled");
            if (_miEnable  != null) _miEnable.Visible  = !allEnabled;
            if (_miDisable != null) _miDisable.Visible = !allDisabled;

            // Per-client visibility based on first v6 instance
            var firstV6 = _instances.FirstOrDefault(s => s.Cfg.ApiVersion == 6);
            if (firstV6 != null)
            {
                if (_miEnableClient  != null) _miEnableClient.Visible  = firstV6.ClientUnblocked;
                if (_miDisableClient != null) _miDisableClient.Visible = !firstV6.ClientUnblocked;
                if (_miTimedClient   != null) _miTimedClient.Visible   = !firstV6.ClientUnblocked;
            }
        }

        // Update non-default instance submenu labels + icons + enable/disable visibility
        foreach (ToolStripItem item in _menu.Items)
        {
            if (item is not ToolStripMenuItem mi || mi.Tag is not InstanceState state) continue;
            mi.Text  = state.Cfg.Name;
            mi.Image = IconRenderer.GetStatusBitmap(state.Status ?? "unknown", 16);
            if (mi.DropDownItems.Count >= 2)
            {
                mi.DropDownItems[0].Visible = state.Status != "enabled";  // Enable
                mi.DropDownItems[1].Visible = state.Status == "enabled";  // Disable
            }
            // Per-client items in submenu (v6): after separator at index 3
            if (state.Cfg.ApiVersion == 6 && mi.DropDownItems.Count >= 7)
            {
                mi.DropDownItems[4].Visible = state.ClientUnblocked;   // Enable client
                mi.DropDownItems[5].Visible = !state.ClientUnblocked;  // Disable client
                mi.DropDownItems[6].Visible = !state.ClientUnblocked;  // Timed client
            }
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void HandleLeftClick()
    {
        switch (_cfg.LeftClickAction)
        {
            case "toggle_client":
                ToggleClient();
                break;
            case "open_dashboard":
                var d = DefaultInstance;
                if (d != null) OpenDashboard(d.Cfg);
                else if (_instances.Count > 0) OpenDashboard(_instances[0].Cfg);
                break;
            case "none":
                break;
            default: // "toggle_global"
                ToggleDefault();
                break;
        }
    }

    private void ToggleDefault()
    {
        var def = DefaultInstance;
        if (def != null)
        {
            _ = def.Status == "enabled" ? DoDisableAsync(def) : DoEnableAsync(def);
        }
        else
        {
            bool anyEnabled = _instances.Any(s => s.Status == "enabled");
            _ = anyEnabled ? DoDisableAllAsync() : DoEnableAllAsync();
        }
    }

    private void ToggleClient()
    {
        var def = DefaultInstance ?? _instances.FirstOrDefault();
        if (def == null || def.Cfg.ApiVersion != 6) return;
        _ = def.ClientUnblocked ? DoEnableClientAsync(def) : DoDisableClientAsync(def);
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

    private async Task DoEnableAllAsync()
    {
        await Task.WhenAll(_instances.Select(async s =>
        {
            if (await s.Api.EnableAsync()) s.Status = "enabled";
        }));
        UpdateTray();
    }

    private async Task DoDisableAllAsync(int seconds = 0)
    {
        await Task.WhenAll(_instances.Select(async s =>
        {
            if (await s.Api.DisableAsync(seconds)) s.Status = "disabled";
        }));
        UpdateTray();
    }

    // ── Per-client actions ──────────────────────────────────────────────────

    private string GetClientIp()
    {
        if (!string.IsNullOrWhiteSpace(_cfg.ClientIp)) return _cfg.ClientIp.Trim();
        return DetectLocalIp();
    }

    private static string DetectLocalIp()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 80);
            return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }
        catch { return "127.0.0.1"; }
    }

    private async Task DoDisableClientAsync(InstanceState state)
    {
        var ip = GetClientIp();
        if (await state.Api.DisableClientAsync(ip)) state.ClientUnblocked = true;
        UpdateTray();
    }

    private async Task DoEnableClientAsync(InstanceState state)
    {
        var ip = GetClientIp();
        if (await state.Api.EnableClientAsync(ip)) state.ClientUnblocked = false;
        UpdateTray();
    }

    private async Task DoDisableClientTimedAsync(InstanceState state, int seconds)
    {
        var ip = GetClientIp();
        if (await state.Api.DisableClientAsync(ip))
        {
            state.ClientUnblocked = true;
            UpdateTray();
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds)).ConfigureAwait(false);
                if (await state.Api.EnableClientAsync(ip).ConfigureAwait(false))
                    state.ClientUnblocked = false;
                UpdateTray();
            });
        }
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

    private void OpenAbout()
    {
        if (_aboutWin != null && !_aboutWin.IsDisposed)
        {
            _aboutWin.BringToFront();
            _aboutWin.Focus();
            return;
        }
        _aboutWin = new AboutForm();
        _aboutWin.FormClosed += (_, _) => _aboutWin = null;
        _aboutWin.Show();
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

            var clientIp = GetClientIp();
            var tasks = _instances.Select(async state =>
            {
                try
                {
                    var s = await state.Api.GetStatusAsync().ConfigureAwait(false);
                    if (s != null) state.Status = s;

                    // Poll per-client status for v6 instances
                    if (state.Cfg.ApiVersion == 6)
                    {
                        state.ClientUnblocked = await state.Api.IsClientUnblockedAsync(clientIp)
                                                               .ConfigureAwait(false);
                    }
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
            var def    = DefaultInstance;
            var status = def != null ? (def.Status ?? "unknown") : AggregateStatus();
            var name   = def?.Cfg.Name ?? "Pi-Hole";

            // Use client-specific icons only when client state differs from global
            var iconState = status;
            var clientInst = def?.Cfg.ApiVersion == 6 ? def
                : _instances.FirstOrDefault(s => s.Cfg.ApiVersion == 6);
            if (clientInst != null && status == "enabled" && clientInst.ClientUnblocked)
                iconState = "client_disabled";
            else if (clientInst != null && status == "enabled"
                     && _cfg.LeftClickAction == "toggle_client")
                iconState = "client_enabled";

            var icon    = IconRenderer.GetIcon(iconState, 64);
            var tooltip = BuildTooltip(name, status, clientInst?.ClientUnblocked == true);
            _uiContext.Post(_ => ApplyTray(icon, tooltip), null);
        }
        catch { }
    }

    private string AggregateStatus()
    {
        if (_instances.Count == 0) return "unknown";
        if (_instances.All(s => s.Status == "enabled"))  return "enabled";
        if (_instances.All(s => s.Status == "disabled")) return "disabled";
        if (_instances.Any(s => s.Status == "enabled"))  return "mixed";
        return "unknown";
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

    private string BuildTooltip(string name, string status, bool clientUnblocked = false)
    {
        var label = status switch
        {
            "enabled"  => Loc.T("tray_active",   _lang),
            "disabled" => Loc.T("tray_disabled",  _lang),
            _          => Loc.T("tray_noconn",    _lang),
        };
        var tip = $"{name}: {label}";
        if (clientUnblocked)
            tip += $" ({Loc.T("tray_client_disabled", _lang)})";
        return tip;
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
