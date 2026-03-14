using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace PiHoleTray;

class PiHoleApi : IDisposable
{
    private readonly HttpClient _client;
    private string _sid = "";
    private bool _authed = false;

    public string BaseUrl  { get; private set; }
    public string Password { get; private set; }
    public int    Version  { get; private set; }

    public PiHoleApi(string url, string password, int version = 6)
    {
        var u = url.TrimEnd('/');
        if (u.EndsWith("/admin")) u = u[..^6];
        BaseUrl  = u;
        Password = password;
        Version  = version;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    // ── V6 helpers ──────────────────────────────────────────────────────────

    private string V6Url(string ep) => $"{BaseUrl}/api/{ep.TrimStart('/')}";

    private async Task LogoutV6Async()
    {
        if (string.IsNullOrEmpty(_sid)) return;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Delete, V6Url("auth"));
            req.Headers.Add("sid", _sid);
            await _client.SendAsync(req).ConfigureAwait(false);
        }
        catch { }
        _sid = "";
        _authed = false;
    }

    private async Task<bool> AuthV6Async()
    {
        try
        {
            // Close existing session before opening a new one
            await LogoutV6Async().ConfigureAwait(false);

            var body    = JsonSerializer.Serialize(new { password = Password });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp    = await _client.PostAsync(V6Url("auth"), content).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) { _authed = false; return false; }

            var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
            var sess = json?["session"];
            if (sess?["valid"]?.GetValue<bool>() == true)
            {
                _sid    = sess["sid"]?.GetValue<string>() ?? "";
                _authed = true;
                return true;
            }
        }
        catch { }
        _authed = false;
        return false;
    }

    private async Task<HttpResponseMessage?> V6Async(HttpMethod method, string ep, object? body = null)
    {
        if (!_authed && !await AuthV6Async().ConfigureAwait(false))
            return null;

        async Task<HttpResponseMessage> Send()
        {
            var req = new HttpRequestMessage(method, V6Url(ep));
            if (!string.IsNullOrEmpty(_sid))
                req.Headers.Add("sid", _sid);
            if (body != null)
                req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            return await _client.SendAsync(req).ConfigureAwait(false);
        }

        try
        {
            var resp = await Send().ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _authed = false;
                if (!await AuthV6Async().ConfigureAwait(false)) return null;
                resp = await Send().ConfigureAwait(false);
            }
            return resp;
        }
        catch { return null; }
    }

    // ── V5 helper ───────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage?> V5Async(string query)
    {
        try
        {
            return await _client.GetAsync($"{BaseUrl}/admin/api.php?{query}&auth={Password}")
                                .ConfigureAwait(false);
        }
        catch { return null; }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task<string?> GetStatusAsync()
    {
        if (Version == 6)
        {
            var r = await V6Async(HttpMethod.Get, "dns/blocking").ConfigureAwait(false);
            if (r?.IsSuccessStatusCode == true)
            {
                var json = JsonNode.Parse(await r.Content.ReadAsStringAsync().ConfigureAwait(false));
                return json?["blocking"]?.GetValue<string>() == "enabled" ? "enabled" : "disabled";
            }
            return null;
        }
        var rv = await V5Async("status").ConfigureAwait(false);
        if (rv?.IsSuccessStatusCode == true)
        {
            var json = JsonNode.Parse(await rv.Content.ReadAsStringAsync().ConfigureAwait(false));
            return json?["status"]?.GetValue<string>();
        }
        return null;
    }

    public async Task<bool> EnableAsync()
    {
        if (Version == 6)
        {
            var r = await V6Async(HttpMethod.Post, "dns/blocking",
                        new { blocking = true, timer = (object?)null }).ConfigureAwait(false);
            return r?.IsSuccessStatusCode == true;
        }
        var rv = await V5Async("enable").ConfigureAwait(false);
        if (rv?.IsSuccessStatusCode == true)
        {
            var json = JsonNode.Parse(await rv.Content.ReadAsStringAsync().ConfigureAwait(false));
            return json?["status"]?.GetValue<string>() == "enabled";
        }
        return false;
    }

    public async Task<bool> DisableAsync(int seconds = 0)
    {
        if (Version == 6)
        {
            object payload = seconds > 0
                ? new { blocking = false, timer = seconds }
                : new { blocking = false };
            var r = await V6Async(HttpMethod.Post, "dns/blocking", payload).ConfigureAwait(false);
            return r?.IsSuccessStatusCode == true;
        }
        var query = seconds > 0 ? $"disable={seconds}" : "disable";
        var rv    = await V5Async(query).ConfigureAwait(false);
        if (rv?.IsSuccessStatusCode == true)
        {
            var json = JsonNode.Parse(await rv.Content.ReadAsStringAsync().ConfigureAwait(false));
            return json?["status"]?.GetValue<string>() == "disabled";
        }
        return false;
    }

    public async Task<(bool ok, string msg)> TestAsync(string lang)
    {
        if (Version == 6)
        {
            try
            {
                var req  = new HttpRequestMessage(HttpMethod.Get, V6Url("dns/blocking"));
                var resp = await _client.SendAsync(req).ConfigureAwait(false);

                if (resp.IsSuccessStatusCode)
                {
                    var json     = JsonNode.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                    var blocking = json?["blocking"]?.GetValue<string>() ?? "?";
                    return (true, $"{Loc.T("connected", lang)}\nBlocking: {blocking}");
                }
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (string.IsNullOrEmpty(Password))
                        return (false, Loc.T("auth_required", lang));
                    _authed = false;
                    if (await AuthV6Async().ConfigureAwait(false))
                    {
                        var s = await GetStatusAsync().ConfigureAwait(false);
                        return (true, $"{Loc.T("connected_auth", lang)}\nBlocking: {s ?? "?"}");
                    }
                    return (false, Loc.T("login_failed", lang));
                }
                return (false, $"HTTP {(int)resp.StatusCode}");
            }
            catch (TaskCanceledException)
            {
                return (false, Loc.T("timeout", lang));
            }
            catch (HttpRequestException)
            {
                return (false, $"{Loc.T("no_connection", lang)}\n{BaseUrl}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        var rv = await V5Async("status").ConfigureAwait(false);
        if (rv?.IsSuccessStatusCode == true)
        {
            var json     = JsonNode.Parse(await rv.Content.ReadAsStringAsync().ConfigureAwait(false));
            var blocking = json?["status"]?.GetValue<string>() ?? "?";
            return (true, $"{Loc.T("connected", lang)} (v5)\nBlocking: {blocking}");
        }
        return (false, Loc.T("conn_failed", lang));
    }

    // ── Query log ────────────────────────────────────────────────────────────

    public async Task<List<BlockedQuery>> GetBlockedQueriesAsync(int count = 200)
    {
        if (Version == 6)
        {
            var r = await V6Async(HttpMethod.Get, $"queries?length={count}&upstream=blocklist")
                            .ConfigureAwait(false);
            if (r?.IsSuccessStatusCode == true)
            {
                var json = JsonNode.Parse(await r.Content.ReadAsStringAsync().ConfigureAwait(false));
                var arr  = json?["queries"]?.AsArray();
                if (arr == null) return [];
                var list = new List<BlockedQuery>(arr.Count);
                foreach (var q in arr)
                {
                    if (q == null) continue;
                    var id     = q["id"]?.GetValue<long>() ?? 0;
                    var ts     = q["time"]?.GetValue<double>() ?? 0;
                    var time   = DateTimeOffset.FromUnixTimeSeconds((long)ts).LocalDateTime;
                    var domain = q["domain"]?.GetValue<string>() ?? "";
                    var status = q["status"]?.GetValue<string>() ?? "BLOCKED";
                    var ip     = q["client"]?["ip"]?.GetValue<string>() ?? "";
                    var name   = q["client"]?["name"]?.GetValue<string>() ?? "";
                    list.Add(new BlockedQuery(id, time, domain, ip, name, status));
                }
                return list;
            }
            return [];
        }

        // v5: getAllQueries, filter blocked status codes
        var rv = await V5Async($"getAllQueries={count}").ConfigureAwait(false);
        if (rv?.IsSuccessStatusCode == true)
        {
            var json = JsonNode.Parse(await rv.Content.ReadAsStringAsync().ConfigureAwait(false));
            var data = json?["data"]?.AsArray();
            if (data == null) return [];
            var list = new List<BlockedQuery>();
            long fakeId = 0;
            foreach (var row in data)
            {
                if (row is not JsonArray cols || cols.Count < 5) continue;
                // v5 row: [timestamp, qtype, domain, client, status_int, ...]
                var code = cols[4]?.GetValue<int>() ?? 0;
                if (code != 2 && code != 4 && code != 5 && code != 6 && code != 7) continue;
                var tsStr = cols[0]?.GetValue<string>() ?? "0";
                var ts    = long.TryParse(tsStr, out var t) ? t : 0;
                var time   = DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime;
                var domain = cols[2]?.GetValue<string>() ?? "";
                var ip     = cols[3]?.GetValue<string>() ?? "";
                var status = code switch { 2 => "GRAVITY", 4 => "REGEX_DENY", 5 => "DENYLIST",
                                           6 => "REGEX_DENY", 7 => "DENYLIST", _ => "BLOCKED" };
                list.Add(new BlockedQuery(fakeId++, time, domain, ip, "", status));
            }
            return list;
        }
        return [];
    }

    // ── Allowlist management ─────────────────────────────────────────────────

    public async Task<bool> AllowDomainAsync(string domain)
    {
        if (Version == 6)
        {
            var r = await V6Async(HttpMethod.Post, "domains/allow/exact",
                        new { domain, comment = "Added from PiHole Tray", enabled = true })
                        .ConfigureAwait(false);
            // 201 = created, 200 = updated, treat both as success; duplicate (4xx) also means it's allowed
            return r != null && ((int)r.StatusCode is >= 200 and < 300 or 400);
        }
        var rv = await V5Async($"list=white&add={Uri.EscapeDataString(domain)}").ConfigureAwait(false);
        return rv?.IsSuccessStatusCode == true;
    }

    public async Task<bool> RemoveDomainAsync(string domain)
    {
        if (Version == 6)
        {
            var r = await V6Async(HttpMethod.Delete,
                        $"domains/allow/exact/{Uri.EscapeDataString(domain)}")
                        .ConfigureAwait(false);
            return r != null && ((int)r.StatusCode is >= 200 and < 300 or 404);
        }
        var rv = await V5Async($"list=white&sub={Uri.EscapeDataString(domain)}").ConfigureAwait(false);
        return rv?.IsSuccessStatusCode == true;
    }

    // ── Per-client group management (v6 only) ─────────────────────────────

    private int? _unblockedGroupId;

    public async Task<int> GetOrCreateUnblockedGroupAsync()
    {
        if (_unblockedGroupId.HasValue) return _unblockedGroupId.Value;
        if (Version != 6) return -1;

        // Check if group already exists
        var r = await V6Async(HttpMethod.Get, "groups").ConfigureAwait(false);
        if (r?.IsSuccessStatusCode == true)
        {
            var json = JsonNode.Parse(await r.Content.ReadAsStringAsync().ConfigureAwait(false));
            var groups = json?["groups"]?.AsArray();
            if (groups != null)
            {
                foreach (var g in groups)
                {
                    if (g?["name"]?.GetValue<string>() == "PiHoleTray_Unblocked")
                    {
                        _unblockedGroupId = g["id"]?.GetValue<int>() ?? -1;
                        return _unblockedGroupId.Value;
                    }
                }
            }
        }

        // Create the group
        var cr = await V6Async(HttpMethod.Post, "groups",
            new { name = "PiHoleTray_Unblocked", comment = "Created by PiHole Tray – no adlists attached", enabled = true })
            .ConfigureAwait(false);
        if (cr != null && (int)cr.StatusCode is >= 200 and < 300)
        {
            var json = JsonNode.Parse(await cr.Content.ReadAsStringAsync().ConfigureAwait(false));
            _unblockedGroupId = json?["group"]?["id"]?.GetValue<int>()
                             ?? json?["id"]?.GetValue<int>()
                             ?? -1;
            return _unblockedGroupId.Value;
        }
        return -1;
    }

    public async Task<bool> EnsureClientAsync(string clientIp)
    {
        if (Version != 6) return false;

        // Check if client exists
        var r = await V6Async(HttpMethod.Get, $"clients/{Uri.EscapeDataString(clientIp)}")
                        .ConfigureAwait(false);
        if (r?.IsSuccessStatusCode == true) return true;

        // Create client
        var cr = await V6Async(HttpMethod.Post, "clients",
            new { client = clientIp, comment = "Added by PiHole Tray" })
            .ConfigureAwait(false);
        return cr != null && (int)cr.StatusCode is >= 200 and < 300 or 409;
    }

    public async Task<bool> IsClientUnblockedAsync(string clientIp)
    {
        if (Version != 6) return false;
        var gid = await GetOrCreateUnblockedGroupAsync().ConfigureAwait(false);
        if (gid < 0) return false;

        var r = await V6Async(HttpMethod.Get, $"clients/{Uri.EscapeDataString(clientIp)}")
                        .ConfigureAwait(false);
        if (r?.IsSuccessStatusCode != true) return false;

        var json = JsonNode.Parse(await r.Content.ReadAsStringAsync().ConfigureAwait(false));
        var groups = json?["client"]?["groups"]?.AsArray();
        if (groups == null) return false;

        // Unblocked = only in unblocked group (not in default group 0)
        var ids = new HashSet<int>();
        foreach (var g in groups) ids.Add(g?.GetValue<int>() ?? -1);
        return ids.Contains(gid) && !ids.Contains(0);
    }

    public async Task<bool> DisableClientAsync(string clientIp)
    {
        if (Version != 6) return false;
        var gid = await GetOrCreateUnblockedGroupAsync().ConfigureAwait(false);
        if (gid < 0) return false;

        await EnsureClientAsync(clientIp).ConfigureAwait(false);

        var r = await V6Async(HttpMethod.Put, $"clients/{Uri.EscapeDataString(clientIp)}",
            new { groups = new[] { gid } }).ConfigureAwait(false);
        return r?.IsSuccessStatusCode == true;
    }

    public async Task<bool> EnableClientAsync(string clientIp)
    {
        if (Version != 6) return false;

        var r = await V6Async(HttpMethod.Put, $"clients/{Uri.EscapeDataString(clientIp)}",
            new { groups = new[] { 0 } }).ConfigureAwait(false);
        return r?.IsSuccessStatusCode == true;
    }

    public void InvalidateAuth()
    {
        _ = LogoutV6Async();
    }

    public void Dispose()
    {
        try { LogoutV6Async().GetAwaiter().GetResult(); } catch { }
        _client.Dispose();
    }
}

record BlockedQuery(long Id, DateTime Time, string Domain, string ClientIp, string ClientName, string Status);
