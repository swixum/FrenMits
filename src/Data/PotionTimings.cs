using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FrenMits;

// Pulls consensus potion timings for a job from top logs, via the static JSON the
// raalm.com / Lorrgs "m-spec" site serves. Each stat-potion is a distinct ability
// id; we collect that id's casts across the top reports and cluster them into the
// windows the best players actually pot in. Opt-in only — nothing is added unless
// the user clicks.
public static class PotionTimings
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public sealed record Result(bool Ok, string Message, List<float> Times);

    // Fights the site has potion-cast data for (its boss slug). Others return null.
    // Only the current tier (this ultimate + this savage) is tracked with potion
    // casts on the site; the older ultimates (UCOB/UWU/TEA/DSR/TOP) and FRU expose
    // only rotational abilities in their rankings, so there's nothing to pull.
    public static string? BossSlug(uint territory) => territory switch
    {
        Builtin.DmuTerritory => "dancing-mad",
        Builtin.M12sTerritory => "lindwurm",
        _ => null,
    };

    // Baked consensus windows (seconds from pull) per boss slug, then per job,
    // clustered from the top logs on raalm.com / Lorrgs. Shown straight away so you
    // don't have to fetch; "Refresh from logs" re-pulls the latest live numbers.
    // Last refreshed: 2026-06-20.
    public static readonly Dictionary<string, Dictionary<string, int[]>> Defaults = new()
    {
        ["dancing-mad"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PLD"] = new[] { 468, 761, 1083 },
            ["WAR"] = new[] { 6, 469, 749, 1077 },
            ["DRK"] = new[] { 3, 468, 755, 1086 },
            ["GNB"] = new[] { 455, 753, 1092 },
            ["MNK"] = new[] { 119, 467, 759, 1082 },
            ["DRG"] = new[] { 466, 764, 1082 },
            ["NIN"] = new[] { 3, 465, 753, 1076 },
            ["SAM"] = new[] { 4, 471, 748, 1081 },
            ["RPR"] = new[] { 220, 490, 760, 1084 },
            ["VPR"] = new[] { 211, 481, 751, 1080 },
            ["BRD"] = new[] { 211, 481, 751, 1072 },
            ["MCH"] = new[] { 120, 450, 742, 1064 },
            ["DNC"] = new[] { 215, 485, 757, 1073 },
            ["BLM"] = new[] { 6, 473, 764, 1080 },
            ["SMN"] = new[] { 3, 462, 754, 1092 },
            ["RDM"] = new[] { 211, 481, 755, 1080 },
            ["PCT"] = new[] { 469, 759, 1091 },
            ["WHM"] = new[] { 1, 471, 746, 1082 },
            ["SCH"] = new[] { 470, 749, 1085 },
            ["AST"] = new[] { 472, 757, 1092 },
            ["SGE"] = new[] { 1, 469, 767, 1088 },
        },
        ["lindwurm"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PLD"] = new[] { 5, 357 },
            ["WAR"] = new[] { 7, 357 },
            ["DRK"] = new[] { 2, 358 },
            ["GNB"] = new[] { 7, 354 },
            ["MNK"] = new[] { 3, 361 },
            ["DRG"] = new[] { 3, 360 },
            ["NIN"] = new[] { 3, 361 },
            ["SAM"] = new[] { 4, 359 },
            ["RPR"] = new[] { 3, 364 },
            ["VPR"] = new[] { 6, 364 },
            ["BRD"] = new[] { 4, 360 },
            ["MCH"] = new[] { 362 },
            ["DNC"] = new[] { 2, 362 },
            ["BLM"] = new[] { 8, 349 },
            ["SMN"] = new[] { 4, 364 },
            ["RDM"] = new[] { 3, 362 },
            ["PCT"] = new[] { 1, 360 },
            ["WHM"] = new[] { 1, 362 },
            ["SCH"] = new[] { 1, 357 },
            ["AST"] = new[] { 1, 359 },
            ["SGE"] = new[] { 1, 344 },
        },
    };

    // Baked windows for a fight + job, as floats. Empty if none.
    public static List<float> DefaultsFor(uint territory, string? jobAbbr)
    {
        var slug = BossSlug(territory);
        if (slug == null || jobAbbr == null) return new();
        return Defaults.TryGetValue(slug, out var jobs) && jobs.TryGetValue(jobAbbr, out var t)
            ? t.Select(x => (float)x).ToList()
            : new();
    }

    public static string Stat(string? jobAbbr) => (jobAbbr ?? "").ToUpperInvariant() switch
    {
        "PLD" or "WAR" or "DRK" or "GNB" or "MNK" or "DRG" or "SAM" or "RPR" => "Strength",
        "NIN" or "VPR" or "BRD" or "MCH" or "DNC" => "Dexterity",
        "BLM" or "SMN" or "RDM" or "PCT" => "Intelligence",
        "WHM" or "SCH" or "AST" or "SGE" => "Mind",
        _ => "",
    };

    // The Gemdraught ability id for the job's main stat.
    private static long PotionId(string? jobAbbr) => Stat(jobAbbr) switch
    {
        "Strength" => 34603666,
        "Dexterity" => 34603667,
        "Intelligence" => 34603669,
        "Mind" => 34603670,
        _ => 0,
    };

    private static string? SpecSlug(string? jobAbbr) => (jobAbbr ?? "").ToUpperInvariant() switch
    {
        "PLD" => "paladin", "WAR" => "warrior", "DRK" => "darkknight", "GNB" => "gunbreaker",
        "MNK" => "monk", "DRG" => "dragoon", "NIN" => "ninja", "SAM" => "samurai",
        "RPR" => "reaper", "VPR" => "viper",
        "BRD" => "bard", "MCH" => "machinist", "DNC" => "dancer",
        "BLM" => "blackmage", "SMN" => "summoner", "RDM" => "redmage", "PCT" => "pictomancer",
        "WHM" => "whitemage", "SCH" => "scholar", "AST" => "astrologian", "SGE" => "sage",
        _ => null,
    };

    public static async Task<Result> FetchAsync(uint territory, string? jobAbbr)
    {
        var boss = BossSlug(territory);
        if (boss == null) return new(false, "No top-log potion data for this fight.", new());

        var slug = SpecSlug(jobAbbr);
        var potId = PotionId(jobAbbr);
        if (slug == null || potId == 0) return new(false, "Pick your job first.", new());

        var spec = $"{slug}-{slug}";
        var url = $"https://raalm.com/m-spec/data/spec_ranking_{spec}_{boss}.json";

        try
        {
            var json = await Http.GetStringAsync(url).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var stamps = new List<float>();
            var reports = 0;
            foreach (var report in doc.RootElement.GetProperty("reports").EnumerateArray())
            {
                reports++;
                foreach (var fight in report.GetProperty("fights").EnumerateArray())
                foreach (var player in fight.GetProperty("players").EnumerateArray())
                {
                    if (!player.TryGetProperty("spec_slug", out var ss) || ss.GetString() != spec) continue;
                    foreach (var cast in player.GetProperty("casts").EnumerateArray())
                        if (cast.GetProperty("spell_id").GetInt64() == potId)
                            stamps.Add(cast.GetProperty("ts").GetInt64() / 1000f); // ms -> s from pull
                }
            }

            if (stamps.Count == 0) return new(false, "No potion casts found in the logs.", new());

            var times = Cluster(stamps, reports);
            return times.Count == 0
                ? new(false, "Potion casts were too scattered to agree on windows.", new())
                : new(true, $"Found {times.Count} potion window(s) from {reports} top logs.", times);
        }
        catch (Exception ex)
        {
            return new(false, $"Fetch failed: {ex.Message}", new());
        }
    }

    // Potion recast (Gemdraught / Tincture): 4m30s. No single player can pot two
    // windows closer than this, so windows nearer than it are competing strats, not
    // a real sequence — we keep the better-supported one.
    private const float PotionRecast = 270f;

    // Group nearby cast times (pots are minutes apart) and keep windows that show
    // up in a reasonable share of logs; average each into one time. Then enforce the
    // recast so the result is a feasible sequence for one player.
    private static List<float> Cluster(List<float> stamps, int reports)
    {
        stamps.Sort();
        var clusters = new List<List<float>>();
        foreach (var s in stamps)
        {
            if (clusters.Count == 0 || s - clusters[^1][^1] > 60f) clusters.Add(new List<float>());
            clusters[^1].Add(s);
        }

        var minSupport = Math.Max(2, reports / 3);
        var kept = clusters
            .Where(c => c.Count >= minSupport)
            .Select(c => MathF.Round(c.Average()))
            .OrderBy(t => t)
            .ToList();

        return EnforceRecast(kept);
    }

    // Make a set of windows a feasible sequence for one player. A window within the
    // recast of a pre-pull (<=30s) pot is a competing opener strat (prepull vs an
    // early pot) -> drop it. Any other too-close window is just averaging noise ->
    // push it out so the spacing is at least the recast. Windows are sorted in.
    public static List<float> EnforceRecast(List<float> windows)
    {
        var result = new List<float>();
        foreach (var t in windows)
        {
            if (result.Count == 0) { result.Add(t); continue; }
            var prev = result[^1];
            if (t - prev >= PotionRecast) result.Add(t);
            else if (prev > 30f) result.Add(prev + PotionRecast); // noise: keep, spaced out
            // else prev is a pre-pull pot: drop this competing opener
        }
        return result;
    }
}
