using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Consensus potion timings per job, clustered from the top logs the raalm.com /
// Lorrgs "m-spec" site serves. Baked offline; nothing is added to a sheet unless
// the user clicks.
public static class PotionTimings
{
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
    // clustered from the top logs on raalm.com / Lorrgs. Last refreshed: 2026-06-20.
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
    // The standard 2-minute-meta potion plan for a fight of this length: pot
    // the opener, then each 6:00 burst (item recast is 270s; bursts land on the
    // even minutes) while at least half a minute of fight remains.
    public static List<float> GenericWindows(float fightEnd)
    {
        var times = new List<float>();
        for (var t = 0f; t <= fightEnd - 30f || t == 0f; t += 360f) times.Add(t);
        return times;
    }

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
}
