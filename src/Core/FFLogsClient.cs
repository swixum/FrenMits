using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FrenMits;

// Minimal FFLogs v2 (GraphQL) client for Sheet View's "Import log": list a
// report's fights, then pull the enemies' cast events for one fight. Uses the
// standard client-credentials flow with an API client the user creates once at
// fflogs.com/api/clients. Everything is async; callers marshal results back by
// assigning immutable lists that the draw thread reads.
public sealed class FFLogsClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private string? _token;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private string _tokenForId = "";

    public sealed record FightInfo(int Id, string Name, bool Kill, float DurationSec, double StartMs, double EndMs);

    // One enemy cast: game ability id, seconds from the fight start (resolve
    // moment), and whether it had a cast bar (only those can be resync anchors:
    // the sync engine watches cast bars).
    public sealed record LogCast(uint AbilityId, float Time, bool HasCastBar);

    // Pull the report code out of a pasted URL (or accept a bare code).
    public static string? ParseReportCode(string input)
    {
        input = (input ?? "").Trim();
        if (input.Length == 0) return null;
        // (?:a:)? = anonymized report codes keep their prefix.
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

    // The report's boss fights (trash pulls excluded), kills first.
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
        // Stable order: kills first, then pull order within each group.
        return list.OrderByDescending(f => f.Kill).ThenBy(f => f.Id).ToList();
    }

    // Every enemy cast of one fight, fight-relative seconds. "begincast" events
    // mark which abilities have cast bars; "cast" events give the resolve times.
    public async Task<List<LogCast>> GetCastsAsync(string clientId, string secret, string code, FightInfo fight)
    {
        const string q = @"query($code:String!,$fid:Int!,$start:Float!,$end:Float!){reportData{report(code:$code){
            events(fightIDs:[$fid],dataType:Casts,hostilityType:Enemies,startTime:$start,endTime:$end,limit:10000)
            {data nextPageTimestamp}}}}";

        var castBar = new HashSet<uint>();
        var casts = new List<LogCast>();
        var start = fight.StartMs;
        for (var page = 0; page < 6; page++)
        {
            var j = await QueryAsync(clientId, secret, q,
                new { code, fid = fight.Id, start, end = fight.EndMs }).ConfigureAwait(false);
            var ev = j["data"]?["reportData"]?["report"]?["events"];
            if (ev?["data"] is not JArray data) break;

            foreach (var e in data)
            {
                // Read wide then range-check: FFLogs can emit odd/negative ids
                // for special rows, and one of those must not abort the import.
                var rawId = e["abilityGameID"]?.Value<long>() ?? 0;
                if (rawId <= 0 || rawId > uint.MaxValue) continue;
                var ability = (uint)rawId;
                var t = (float)(((e["timestamp"]?.Value<double>() ?? 0) - fight.StartMs) / 1000.0);
                var type = e["type"]?.ToString();
                if (type == "begincast") castBar.Add(ability);
                else if (type == "cast") casts.Add(new LogCast(ability, t, false));
            }

            var next = ev["nextPageTimestamp"];
            if (next == null || next.Type == JTokenType.Null) break;
            start = next.Value<double>();
        }

        // Stamp cast-bar knowledge onto the resolve events.
        for (var i = 0; i < casts.Count; i++)
            if (castBar.Contains(casts[i].AbilityId))
                casts[i] = casts[i] with { HasCastBar = true };
        return casts;
    }

    // Per enemy ability: the hardest single hit it landed on anyone
    // (unmitigatedAmount = what it would do with NO mitigation up) and how many
    // DISTINCT players it ever hit. The pair is what Auto-plan grades rows
    // with: the amount says how hard, the target count separates raidwides
    // (hit everyone; party mits) from busters (hit a tank or two; not a
    // stack-the-party moment).
    public sealed record AbilityDamage(long Worst, int Targets);

    public async Task<Dictionary<uint, AbilityDamage>> GetDamageAsync(string clientId, string secret, string code, FightInfo fight)
    {
        const string q = @"query($code:String!,$fid:Int!,$start:Float!,$end:Float!){reportData{report(code:$code){
            events(fightIDs:[$fid],dataType:DamageTaken,startTime:$start,endTime:$end,limit:10000)
            {data nextPageTimestamp}}}}";

        var worst = new Dictionary<uint, long>();
        var targets = new Dictionary<uint, HashSet<long>>();
        var start = fight.StartMs;
        for (var page = 0; page < 6; page++)
        {
            var j = await QueryAsync(clientId, secret, q,
                new { code, fid = fight.Id, start, end = fight.EndMs }).ConfigureAwait(false);
            var ev = j["data"]?["reportData"]?["report"]?["events"];
            if (ev?["data"] is not JArray data) break;

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
            if (next == null || next.Type == JTokenType.Null) break;
            start = next.Value<double>();
        }
        return worst.ToDictionary(kv => kv.Key,
            kv => new AbilityDamage(kv.Value, targets.TryGetValue(kv.Key, out var t) ? t.Count : 0));
    }
}
