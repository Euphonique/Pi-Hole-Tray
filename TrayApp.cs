using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PiHoleTray;

class TrayApp : ApplicationContext, IDisposable
{
    private AppConfig    _cfg;
    private string       _lang;
    private PiHoleApi    _api;
    private string?      _status;   // "enabled" | "disabled" | null

    private readonly NotifyIcon        _tray;
    private ContextMenuStrip           _menu;
    private ToolStripMenuItem          _miEnable  = null!;
    private ToolStripMenuItem          _miDisable = null!;

    private System.Threading.Timer? _timer;
    private bool         _polling = false;
    private bool         _disposed = false;
    private SettingsForm?   _settingsWin;
    private QueryLogForm?   _queryLogWin;
    private readonly SynchronizationContext _uiContext;

    // Temp-allow: domains to remove after a scheduled time
    private readonly List<(string Domain, DateTime RemoveAt)> _tempAllows = [];

    // ── Logging ───────────────────────────────────────────────────────────────

    private static readonly StreamWriter _log = OpenLog();

    private static StreamWriter OpenLog()
    {
        try
        {
            var sw = new StreamWriter(ConfigManager.LogPath, append: true) { AutoFlush = true };
            return sw;
        }
        catch
        {
            return StreamWriter.Null;
        }
    }

    private static void Log(string msg) =>
        _log.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO] {msg}");

    // ── Constructor ───────────────────────────────────────────────────────────

    public TrayApp()
    {
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _cfg  = ConfigManager.Load();
        _lang = Loc.GetEffectiveLang(_cfg.Language);
        _api  = new PiHoleApi(_cfg.PiholeUrl, _cfg.ApiKey, _cfg.ApiVersion);

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
            if (e.Button == MouseButtons.Left) Toggle();
        };

        Log($"Start — {_cfg.PiholeUrl} v{_cfg.ApiVersion} lang={_lang}");
        StartPolling();
    }

    // ── Tray menu ─────────────────────────────────────────────────────────────

    private ContextMenuStrip BuildMenu()
    {
        var L = _lang;
        var menu = new ContextMenuStrip();

        _miEnable  = new ToolStripMenuItem(Loc.T("menu_enable",  L));
        _miDisable = new ToolStripMenuItem(Loc.T("menu_disable", L));
        _miDisable.Font = new Font("Segoe UI", 9f, FontStyle.Bold);   // visually primary action

        _miEnable.Click  += async (_, _) => await DoEnableAsync();
        _miDisable.Click += async (_, _) => await DoDisableAsync();

        var timedMenu = new ToolStripMenuItem(Loc.T("menu_timed", L));
        foreach (var (label, sec) in new[]
        {
            (Loc.T("menu_5min",  L),  300),
            (Loc.T("menu_10min", L),  600),
            (Loc.T("menu_30min", L), 1800),
            (Loc.T("menu_1h",    L), 3600),
            (Loc.T("menu_2h",    L), 7200),
            (Loc.T("menu_5h",    L), 18000),
        })
        {
            int s = sec;
            var item = new ToolStripMenuItem(label);
            item.Click += async (_, _) => await DoDisableAsync(s);
            timedMenu.DropDownItems.Add(item);
        }

        // Section 1 — blocking controls + query log
        menu.Items.Add(_miEnable);
        menu.Items.Add(_miDisable);
        menu.Items.Add(timedMenu);
        menu.Items.Add(Loc.T("menu_querylog",  L), null, (_, _) => OpenQueryLog());
        menu.Items.Add(new ToolStripSeparator());
        // Section 2 — navigation
        menu.Items.Add(Loc.T("menu_dashboard", L), null, (_, _) => OpenDashboard());
        menu.Items.Add(Loc.T("menu_settings",  L), null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        // Section 3 — quit
        menu.Items.Add(Loc.T("menu_quit", L),      null, (_, _) => Quit());

        menu.Opening += (_, _) => RefreshMenuVisibility();

        return menu;
    }

    private void RefreshMenuVisibility()
    {
        _miEnable.Visible  = _status != "enabled";
        _miDisable.Visible = _status == "enabled";
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void Toggle() =>
        _ = _status == "enabled" ? DoDisableAsync() : DoEnableAsync();

    private async Task DoEnableAsync()
    {
        if (await _api.EnableAsync()) _status = "enabled";
        UpdateTray();
    }

    private async Task DoDisableAsync(int seconds = 0)
    {
        if (await _api.DisableAsync(seconds)) _status = "disabled";
        UpdateTray();
    }

    private void OpenDashboard()
    {
        try
        {
            var url = _cfg.PiholeUrl.TrimEnd('/') + "/admin";
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
        _settingsWin = new SettingsForm(_cfg, _status ?? "unknown", OnSaved);
        _settingsWin.FormClosed += (_, _) => _settingsWin = null;
        _settingsWin.Show();
    }

    private void OnSaved(AppConfig cfg)
    {
        _cfg  = cfg;
        _lang = Loc.GetEffectiveLang(cfg.Language);
        _api.Dispose();
        _api  = new PiHoleApi(cfg.PiholeUrl, cfg.ApiKey, cfg.ApiVersion);
        _status = null;

        // Rebuild menu so all labels appear in the new language
        var oldMenu = _menu;
        _menu = BuildMenu();
        _tray.ContextMenuStrip = _menu;
        oldMenu.Dispose();

        UpdateTray();
        RestartTimer();
        Log($"Config saved — {cfg.PiholeUrl} v{cfg.ApiVersion} lang={_lang}");
    }

    private void OpenQueryLog()
    {
        if (_queryLogWin != null && !_queryLogWin.IsDisposed)
        {
            _queryLogWin.BringToFront();
            _queryLogWin.Focus();
            return;
        }
        _queryLogWin = new QueryLogForm(_api, _lang, ScheduleTempAllow);
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
        foreach (var domain in expired)
        {
            try
            {
                await _api.RemoveDomainAsync(domain).ConfigureAwait(false);
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
            var s = await _api.GetStatusAsync();
            if (s != null) _status = s;
            UpdateTray();
        }
        catch (Exception ex)
        {
            Log($"Poll error: {ex.Message}");
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
            var icon    = IconRenderer.GetIcon(_status ?? "unknown", 64);
            var tooltip = BuildTooltip();
            _uiContext.Post(_ => ApplyTray(icon, tooltip), null);
        }
        catch { }
    }

    private void ApplyTray(System.Drawing.Icon icon, string tooltip)
    {
        try
        {
            _tray.Icon = icon;
            _tray.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
        }
        catch { }
    }

    private string BuildTooltip()
    {
        var label = _status switch
        {
            "enabled"  => Loc.T("tray_active",   _lang),
            "disabled" => Loc.T("tray_disabled",  _lang),
            _          => Loc.T("tray_noconn",    _lang),
        };
        return $"Pi-Hole: {label}";
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
            _api.Dispose();
        }
        base.Dispose(disposing);
    }
}
