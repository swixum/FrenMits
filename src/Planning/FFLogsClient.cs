using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FrenMits.Planning;

// Minimal logs client for Sheet View's Import log.
public sealed class FFLogsClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    // Unload drops the pool, so its sockets and timers don't outlive the plugin.
    public static void Shutdown()
    {
        try { Http.CancelPendingRequests(); Http.Dispose(); } catch { /* ignore */ }
    }

    private string? _token;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private string _tokenForId = "";

    public sealed record FightInfo(int Id, string Name, bool Kill, float DurationSec, double StartMs, double EndMs);

    // One enemy cast: ability, time, and whether it had a bar.
    public sealed record LogCast(uint AbilityId, float Time, bool HasCastBar);

    // Pull the report code out of a URL, or take a bare code.
    public static string? ParseReportCode(string input)
    {
        input = (input ?? "").Trim();
        if (input.Length == 0) return null;
        // Anonymous reports carry an a: prefix.
        var m = System.Text.RegularExpressions.Regex.Match(input, @"reports/((?:a:)?[A-Za-z0-9]{8,})");
        if (m.Success) return m.Groups[1].Value;
        return System.Text.RegularExpressions.Regex.IsMatch(input, @"^(?:a:)?[A-Za-z0-9]{8,}$") ? input : null;
    }

    private async Task<string> TokenAsync(string clientId, string secret)
    {
        if (_token != null && DateTime.UtcNow < _tokenExpiry && _tokenForId == clientId)
            return _token;

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.fflogs.com/oauth/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}")));
        req.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });

        var resp = await Http.SendAsync(req).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new Exception("FFLogs rejected the credentials (check client id / secret).");

        var j = JObject.Parse(body);
        _token = j["access_token"]?.ToString() ?? throw new Exception("FFLogs sent no token.");
        var expires = j["expires_in"]?.Value<long>() ?? 3600;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(60, expires - 300));
        _tokenForId = clientId;
        return _token;
    }

    private async Task<JObject> QueryAsync(string clientId, string secret, string query, object variables)
    {
        var token = await TokenAsync(clientId, secret).ConfigureAwait(false);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.fflogs.com/api/v2/client");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(
            Newtonsoft.Json.JsonConvert.SerializeObject(new { query, variables }),
            Encoding.UTF8, "application/json");

        var resp = await Http.SendAsync(req).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"FFLogs API error ({(int)resp.StatusCode}).");
        var j = JObject.Parse(body);
        if (j["errors"] is JArray { Count: > 0 } errs)
            throw new Exception("FFLogs: " + (errs[0]?["message"]?.ToString() ?? "query failed."));
        return j;
    }

    // The report's boss fights, kills first.
    public async Task<List<FightInfo>> GetFightsAsync(string clientId, string secret, string code)
    {
        const string q = @"query($code:String!){reportData{report(code:$code){
            fights{id name kill startTime endTime encounterID}}}}";
        var j = await QueryAsync(clientId, secret, q, new { code }).ConfigureAwait(false);
        var fights = j["data"]?["reportData"]?["report"]?["fights"] as JArray
            ?? throw new Exception("Report not found (is the code right, and the log public?).");

        var list = new List<FightInfo>();
        foreach (var f in fights)
        {
            if ((f["encounterID"]?.Value<int>() ?? 0) == 0) continue; // trash
            var start = f["startTime"]?.Value<double>() ?? 0;
            var end = f["endTime"]?.Value<double>() ?? start;
            list.Add(new FightInfo(
                f["id"]?.Value<int>() ?? 0,
                f["name"]?.ToString() ?? "?",
                f["kill"]?.Value<bool>() ?? false,
                (float)((end - start) / 1000.0),
                start, end));
        }
        // Stable order: kills first, then pull order.
        return list.OrderByDescending(f => f.Kill).ThenBy(f => f.Id).ToList();
    }

    // Every enemy cast of one fight, in fight-relative seconds.
    public async Task<List<LogCast>> GetCastsAsync(string clientId, string secret, string code, FightInfo fight)
    {
        const string q = @"query($code:String!,$fid:Int!,$start:Float!,$end:Float!){reportData{report(code:$code){
            events(fightIDs:[$fid],dataType:Casts,hostilityType:Enemies,startTime:$start,endTime:$end,limit:10000)
            {data nextPageTimestamp}}}}";

        var castBar = new HashSet<uint>();
        var casts = new List<LogCast>();
        var start = fight.StartMs;
        var more = false;
        for (var page = 0; page < 6; page++)
        {
            var j = await QueryAsync(clientId, secret, q,
                new { code, fid = fight.Id, start, end = fight.EndMs }).ConfigureAwait(false);
            var ev = j["data"]?["reportData"]?["report"]?["events"];
            if (ev?["data"] is not JArray data) { more = false; break; }

            foreach (var e in data)
            {
                // Read wide then range-check, since odd ids show up.
                var rawId = e["abilityGameID"]?.Value<long>() ?? 0;
                if (rawId <= 0 || rawId > uint.MaxValue) continue;
                var ability = (uint)rawId;
                var t = (float)(((e["timestamp"]?.Value<double>() ?? 0) - fight.StartMs) / 1000.0);
                var type = e["type"]?.ToString();
                if (type == "begincast") castBar.Add(ability);
                else if (type == "cast") casts.Add(new LogCast(ability, t, false));
            }

            var next = ev["nextPageTimestamp"];
            if (next == null || next.Type == JTokenType.Null) { more = false; break; }
            start = next.Value<double>();
            more = true;
        }
        if (more) Service.Log.Warning("[FrenMits] FFLogs import hit the page cap; a very long fight may be partially imported.");

        // Stamp cast-bar knowledge onto the resolve events.
        for (var i = 0; i < casts.Count; i++)
            if (castBar.Contains(casts[i].AbilityId))
                casts[i] = casts[i] with { HasCastBar = true };
        return casts;
    }

    // The report's ability id to name map.
    public async Task<Dictionary<uint, string>> GetAbilityNamesAsync(string clientId, string secret, string code)
    {
        const string q = @"query($code:String!){reportData{report(code:$code){
            masterData{abilities{gameID name}}}}}";
        var j = await QueryAsync(clientId, secret, q, new { code }).ConfigureAwait(false);
        var abilities = j["data"]?["reportData"]?["report"]?["masterData"]?["abilities"] as JArray;
        var map = new Dictionary<uint, string>();
        if (abilities != null)
            foreach (var a in abilities)
            {
                var rawId = a["gameID"]?.Value<long>() ?? 0;
                if (rawId <= 0 || rawId > uint.MaxValue) continue;
                var name = a["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name)) map[(uint)rawId] = name!.Trim();
            }
        return map;
    }

    public sealed record EncounterRef(int Id, string Name);

    // Resolve a partial fight name to an encounter.
    public async Task<EncounterRef?> FindEncounterAsync(string clientId, string secret, string name)
    {
        var needle = (name ?? "").Trim();
        if (needle.Length == 0) return null;
        const string q = @"{worldData{expansions{zones{encounters{id name}}}}}";
        var j = await QueryAsync(clientId, secret, q, new { }).ConfigureAwait(false);
        EncounterRef? exact = null, contains = null;
        foreach (var exp in j["data"]?["worldData"]?["expansions"] as JArray ?? new JArray())
            foreach (var z in exp["zones"] as JArray ?? new JArray())
                foreach (var e in z["encounters"] as JArray ?? new JArray())
                {
                    var en = e["name"]?.ToString() ?? "";
                    var id = e["id"]?.Value<int>() ?? 0;
                    if (id == 0 || en.Length == 0) continue;
                    if (string.Equals(en, needle, StringComparison.OrdinalIgnoreCase))
                        exact = new EncounterRef(id, en);
                    else if (en.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                             && (contains == null || en.Length < contains.Name.Length))
                        contains = new EncounterRef(id, en);
                }
        return exact ?? contains;
    }

    // The top-ranked kill's report code and fight id.
    public async Task<(string Code, int FightId)?> GetTopKillAsync(string clientId, string secret, int encounterId, string metric)
    {
        // Inlined from an allow-list, never user text.
        var m = metric == "execution" ? "execution" : "speed";
        var q = @"query($e:Int!){worldData{encounter(id:$e){fightRankings(metric:" + m + @",page:1)}}}";
        var j = await QueryAsync(clientId, secret, q, new { e = encounterId }).ConfigureAwait(false);
        var fr = j["data"]?["worldData"]?["encounter"]?["fightRankings"];
        if (fr == null || fr.Type == JTokenType.Null) return null;
        // This field can come back as a JSON string.
        var parsed = fr.Type == JTokenType.String ? JToken.Parse(fr.ToString()) : fr;
        // Index only an object, since a scalar indexer throws.
        if (parsed is not JObject obj || obj["rankings"] is not JArray rankings) return null;
        foreach (var r in rankings)
        {
            var rep = r["report"];
            var repCode = rep?["code"]?.ToString();
            var fid = rep?["fightID"]?.Value<int>() ?? 0;
            if (!string.IsNullOrEmpty(repCode) && fid > 0) return (repCode!, fid);
        }
        return null;
    }

    // Per ability: its hardest unmitigated hit and target count.
    public sealed record AbilityDamage(long Worst, int Targets);

    public async Task<Dictionary<uint, AbilityDamage>> GetDamageAsync(string clientId, string secret, string code, FightInfo fight)
    {
        const string q = @"query($code:String!,$fid:Int!,$start:Float!,$end:Float!){reportData{report(code:$code){
            events(fightIDs:[$fid],dataType:DamageTaken,startTime:$start,endTime:$end,limit:10000)
            {data nextPageTimestamp}}}}";

        var worst = new Dictionary<uint, long>();
        var targets = new Dictionary<uint, HashSet<long>>();
        var start = fight.StartMs;
        var more = false;
        for (var page = 0; page < 6; page++)
        {
            var j = await QueryAsync(clientId, secret, q,
                new { code, fid = fight.Id, start, end = fight.EndMs }).ConfigureAwait(false);
            var ev = j["data"]?["reportData"]?["report"]?["events"];
            if (ev?["data"] is not JArray data) { more = false; break; }

            foreach (var e in data)
            {
                var rawId = e["abilityGameID"]?.Value<long>() ?? 0;
                if (rawId <= 0 || rawId > uint.MaxValue) continue;
                var amt = e["unmitigatedAmount"]?.Value<long>() ?? e["amount"]?.Value<long>() ?? 0;
                if (amt <= 0) continue;
                var id = (uint)rawId;
                if (!worst.TryGetValue(id, out var cur) || amt > cur) worst[id] = amt;
                var tgt = e["targetID"]?.Value<long>() ?? -1;
                if (tgt >= 0) (targets.TryGetValue(id, out var set) ? set : targets[id] = new()).Add(tgt);
            }

            var next = ev["nextPageTimestamp"];
            if (next == null || next.Type == JTokenType.Null) { more = false; break; }
            start = next.Value<double>();
            more = true;
        }
        if (more) Service.Log.Warning("[FrenMits] FFLogs import hit the page cap; a very long fight may be partially imported.");
        return worst.ToDictionary(kv => kv.Key,
            kv => new AbilityDamage(kv.Value, targets.TryGetValue(kv.Key, out var t) ? t.Count : 0));
    }

    // When the log's party hit their defensive buttons.
    public sealed record MitPress(uint AbilityId, float Time);

    private static readonly string[] MitNames =
    {
        "Reprisal", "Feint", "Addle", "Dismantle",
        "Shake It Off", "Divine Veil", "Dark Missionary", "Heart of Light",
        "Temperance", "Liturgy of the Bell", "Asylum", "Plenary Indulgence",
        "Collective Unconscious", "Neutral Sect", "Macrocosmos",
        "Sacred Soil", "Expedient", "Fey Illumination", "Summon Seraph", "Whispering Dawn",
        "Kerachole", "Holos", "Panhaima", "Physis II", "Philosophia",
        "Magick Barrier", "Troubadour", "Tactician", "Shield Samba",
        "Nature's Minne", "Mantra", "Curing Waltz", "Tempera Grassa", "Passage of Arms",
        "Rampart", "Bloodwhetting", "Nascent Flash", "The Blackest Night", "Oblation",
        "Holy Sheltron", "Intervention", "Heart of Corundum", "Aurora",
        "Vengeance", "Damnation", "Sentinel", "Guardian", "Camouflage", "Nebula",
        "Thrill of Battle", "Dark Mind", "Bulwark",
        "Holmgang", "Hallowed Ground", "Living Dead", "Superbolide",
    };

    public async Task<List<MitPress>> GetMitCastsAsync(string clientId, string secret, string code, FightInfo fight)
    {
        const string q = @"query($code:String!,$fid:Int!,$start:Float!,$end:Float!,$filter:String!){reportData{report(code:$code){
            events(fightIDs:[$fid],dataType:Casts,hostilityType:Friendlies,filterExpression:$filter,
                   startTime:$start,endTime:$end,limit:10000)
            {data nextPageTimestamp}}}}";
        var filter = "ability.name IN (" + string.Join(", ", MitNames.Select(n => $"\"{n}\"")) + ")";

        var presses = new List<MitPress>();
        var start = fight.StartMs;
        var more = false;
        for (var page = 0; page < 4; page++)
        {
            var j = await QueryAsync(clientId, secret, q,
                new { code, fid = fight.Id, start, end = fight.EndMs, filter }).ConfigureAwait(false);
            var ev = j["data"]?["reportData"]?["report"]?["events"];
            if (ev?["data"] is not JArray data) { more = false; break; }

            foreach (var e in data)
            {
                if (e["type"]?.ToString() != "cast") continue;
                var rawId = e["abilityGameID"]?.Value<long>() ?? 0;
                if (rawId <= 0 || rawId > uint.MaxValue) continue;
                var t = (float)(((e["timestamp"]?.Value<double>() ?? 0) - fight.StartMs) / 1000.0);
                presses.Add(new MitPress((uint)rawId, t));
            }

            var next = ev["nextPageTimestamp"];
            if (next == null || next.Type == JTokenType.Null) { more = false; break; }
            start = next.Value<double>();
            more = true;
        }
        if (more) Service.Log.Warning("[FrenMits] FFLogs import hit the page cap; a very long fight may be partially imported.");
        return presses;
    }
}
