using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace PiHoleTray;

class PiHoleInstance
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Pi-Hole";

    [JsonPropertyName("pihole_url")]
    public string PiholeUrl { get; set; } = "http://pi.hole";

    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("api_version")]
    public int ApiVersion { get; set; } = 6;

    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; } = false;
}

class AppConfig
{
    [JsonPropertyName("instances")]
    public List<PiHoleInstance> Instances { get; set; } = [];

    [JsonPropertyName("autostart")]
    public bool Autostart { get; set; } = false;

    [JsonPropertyName("poll_interval")]
    public int PollInterval { get; set; } = 10;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    [JsonPropertyName("left_click_action")]
    public string LeftClickAction { get; set; } = "toggle_global";

    [JsonPropertyName("client_ip")]
    public string ClientIp { get; set; } = "";

    // ── Legacy flat fields — only used during migration ───────────────────────
    [JsonPropertyName("pihole_url")]
    public string? LegacyUrl { get; set; }

    [JsonPropertyName("api_key")]
    public string? LegacyApiKey { get; set; }

    [JsonPropertyName("api_version")]
    public int? LegacyApiVersion { get; set; }
}

static class ConfigManager
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PiHoleTray");

    public static readonly string ConfigPath = Path.Combine(AppDataDir, "config.json");
    public static readonly string LogPath    = Path.Combine(AppDataDir, "pihole_tray.log");

    static ConfigManager()
    {
        Directory.CreateDirectory(AppDataDir);
    }

    public static AppConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg  = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                return Migrate(cfg);
            }
            catch { }
        }
        return DefaultConfig();
    }

    private static AppConfig Migrate(AppConfig cfg)
    {
        // Old single-instance config → wrap in list
        if (cfg.Instances.Count == 0 && cfg.LegacyUrl != null)
        {
            cfg.Instances.Add(new PiHoleInstance
            {
                Name       = "Pi-Hole",
                PiholeUrl  = cfg.LegacyUrl,
                ApiKey     = cfg.LegacyApiKey ?? "",
                ApiVersion = cfg.LegacyApiVersion ?? 6,
                IsDefault  = true,
            });
        }

        return cfg;
    }

    private static AppConfig DefaultConfig()
    {
        var cfg = new AppConfig();
        cfg.Instances.Add(new PiHoleInstance
        {
            Name      = "Pi-Hole",
            PiholeUrl = "http://pi.hole",
            IsDefault = true,
        });
        return cfg;
    }

    public static void Save(AppConfig cfg)
    {
        // Clear legacy fields before saving
        cfg.LegacyUrl        = null;
        cfg.LegacyApiKey     = null;
        cfg.LegacyApiVersion = null;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, options));
    }

    public static void SetAutostart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;
            if (enable)
            {
                var exe = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                key.SetValue("PiHoleTray", $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue("PiHoleTray", throwOnMissingValue: false);
            }
        }
        catch { }
    }
}
