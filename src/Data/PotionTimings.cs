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

    // Fights the site has data for (its boss slug). Others return null.
    public static string? BossSlug(uint territory) => territory switch
    {
        Builtin.DmuTerritory => "dancing-mad",
        _ => null,
    };

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

    // Group nearby cast times (pots are minutes apart) and keep windows that show
    // up in a reasonable share of logs; average each into one time.
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
        return clusters
            .Where(c => c.Count >= minSupport)
            .Select(c => MathF.Round(c.Average()))
            .ToList();
    }
}
