using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Optional job mit timers, derived from log clears.
public static class JobExtras
{
    // Steps: a sequence extra carries its action per entry.
    public sealed record Extra(string Job, string Action, float Recast, (int Time, string Mechanic)[] Lines,
        (int Time, string Summon)[]? Steps = null);

    private static readonly Dictionary<uint, Extra[]> ByZone = new()
    {
        [Builtin.DmuTerritory] = new[]
        {
            // Bard, anchored to sheet v5.0 rows.
            new Extra("BRD", "Nature's Minne", 120f, new[]
            {
                (63, "Light of Judgment"),
                (250, "Towers I"),
                (450, "Bowels of Agony (Chaos)"),
                (637, "Thunder III (5th Set)"),
                (789, "Grand Cross"),
                (928, "Chaotic Flood"),
                (1062, "Forsaken (1st Hit)"),
            }),
            // Monk - Mantra (90s recast), sheet v5.0 rows.
            new Extra("MNK", "Mantra", 90f, new[]
            {
                (88, "Gravitas II (Part I)"),
                (236, "Forsaken"),
                (450, "Bowels of Agony (Chaos)"),
                (545, "The Decisive Battle"),
                (650, "Black Holes III (6th Tether Set)"),
                (769, "Inferno/Tsunami"),
                (911, "Ultima Repeater"),
            }),
            // Paladin - Passage of Arms (120s recast), sheet v5.0 rows.
            new Extra("PLD", "Passage of Arms", 120f, new[]
            {
                (63, "Light of Judgment"),
                (343, "Light of Judgement"),
                (609, "Shocking Impact/Shockwave"),
                (789, "Grand Cross"),
                (928, "Chaotic Flood"),
            }),
            // DNC, MCH and RDM schedules come from top kill logs.

            // Dancer, ten presses at 60s recast.
            new Extra("DNC", "Curing Waltz", 60f, new[]
            {
                (64, "Light of Judgment"),
                (134, "Light of Judgment"),
                (196, "Mystery Magic"),
                (327, "Towers VIII (Past/Future's End)"),
                (453, "Bowels of Agony (Chaos)"),
                (519, "Cyclone"),
                (681, "Shocking Impact/Shockwave"),
                (781, "Inferno/Tsunami"),
                (928, "Chaotic Flood"),
                (1063, "Forsaken (1st Hit)"),
            }),
            // These three follow the sheet's Extras column.
            new Extra("MCH", "Dismantle", 120f, new[]
            {
                (62, "Light of Judgment"),
                (235, "Forsaken"),
                (469, "Stray Flames/Tsunami"),
                (608, "Shocking Impact/Shockwave"),
                (762, "Grand Cross"),
                (910, "Ultima Repeater"),
                (1061, "Forsaken (1st Hit)"),
            }),
            new Extra("RDM", "Magick Barrier", 120f, new[]
            {
                (62, "Light of Judgment"),
                (235, "Forsaken"),
                (469, "Stray Flames/Tsunami"),
                (608, "Shocking Impact/Shockwave"),
                (762, "Grand Cross"),
                (910, "Ultima Repeater"),
                (1061, "Forsaken (1st Hit)"),
            }),
            new Extra("PCT", "Tempera Grassa", 120f, new[]
            {
                (62, "Light of Judgment"),
                (235, "Forsaken"),
                (469, "Stray Flames/Tsunami"),
                (608, "Shocking Impact/Shockwave"),
                (762, "Grand Cross"),
                (910, "Ultima Repeater"),
                (1061, "Forsaken (1st Hit)"),
            }),
            // Summoner: which primal to summon next.
            new Extra("SMN", "Summon", 0f, Array.Empty<(int, string)>(), new[]
            {
                (19, "Garuda"), (33, "Titan"), (48, "Ifrit"),
                (79, "Garuda"), (91, "Ifrit"), (107, "Titan"),
                (139, "Ifrit"), (155, "Titan"), (168, "Garuda"),
                (209, "Ifrit"), (222, "Garuda"), (231, "Titan"),
                (261, "Garuda"), (273, "Ifrit"), (286, "Titan"),
                (321, "Titan"), (334, "Ifrit"), (347, "Garuda"),
                (448, "Titan"), (461, "Garuda"), (473, "Ifrit"),
                (506, "Ifrit"), (520, "Titan"), (535, "Garuda"),
                (567, "Ifrit"), (580, "Garuda"), (595, "Titan"),
                (627, "Garuda"), (641, "Titan"), (654, "Ifrit"),
                (687, "Ifrit"), (701, "Garuda"), (713, "Titan"),
                (751, "Ifrit"), (764, "Garuda"), (781, "Titan"),
                (811, "Ifrit"), (827, "Garuda"), (839, "Titan"),
                (896, "Garuda"), (904, "Titan"), (911, "Ifrit"),
                (936, "Ifrit"), (952, "Garuda"), (966, "Titan"),
                (996, "Titan"), (1009, "Garuda"), (1021, "Ifrit"),
                (1057, "Garuda"), (1071, "Titan"), (1084, "Ifrit"),
            }),
        },
    };

    public static IReadOnlyList<Extra> For(uint territory)
        => ByZone.TryGetValue(territory, out var e) ? e : Array.Empty<Extra>();

    public static Extra? For(uint territory, string? job)
        => string.IsNullOrEmpty(job)
            ? null
            : For(territory).FirstOrDefault(e => string.Equals(e.Job, job, StringComparison.OrdinalIgnoreCase));

    // Each job's optional extras, for sheets with no baked schedule.
    private static readonly (string Job, string Action, float Recast, int Level)[] Kit =
    {
        ("BRD", "Nature's Minne", 120f, 66),
        ("MNK", "Mantra", 90f, 42),
        ("PLD", "Passage of Arms", 120f, 70),
        ("DNC", "Curing Waltz", 60f, 52),
        ("DNC", "Improvisation", 120f, 80),
        ("MCH", "Dismantle", 120f, 62),
        ("RDM", "Magick Barrier", 120f, 86),
        ("PCT", "Tempera Grassa", 120f, 88),
    };

    // Every universal-kit extra for a custom sheet.
    public static IReadOnlyList<Extra> ForCustomSheet(FightProfile fight, string? job)
    {
        if (string.IsNullOrEmpty(job) || fight.CustomRows.Count == 0) return Array.Empty<Extra>();
        // Never suggest an ability the duty sync locks out.
        var sync = Cooldowns.DutySyncLevel(fight.TerritoryId);
        var result = new List<Extra>();
        foreach (var kit in Kit.Where(k => string.Equals(k.Job, job, StringComparison.OrdinalIgnoreCase)))
        {
            if (sync > 0 && kit.Level > sync) continue;
            if (ComputeExtra(fight, kit) is { } e) result.Add(e);
        }
        return result;
    }

    // Place one kit ability across a custom sheet's rows.
    private static Extra? ComputeExtra(FightProfile fight, (string Job, string Action, float Recast, int Level) kit)
    {
        // Graded sheets place extras where the fight hurts.
        var pool = fight.CustomRows.Any(r => !r.Buster)
            ? fight.CustomRows.Where(r => !r.Buster).ToList()
            : fight.CustomRows;
        var candidates = pool.Any(r => r.Hurt > 0)
            ? pool.Where(r => r.Hurt > 0)
            : pool;
        var picked = new List<(float Time, string Mechanic)>();
        foreach (var row in candidates.OrderByDescending(r => r.Hurt).ThenBy(r => r.Time))
        {
            if (picked.Any(p => MathF.Abs(p.Time - row.Time) < kit.Recast)) continue;
            picked.Add((row.Time, row.Mechanic));
        }
        var lines = picked.OrderBy(p => p.Time)
            .Select(p => ((int)MathF.Round(p.Time), p.Mechanic)).ToArray();
        return lines.Length == 0 ? null : new Extra(kit.Job, kit.Action, kit.Recast, lines);
    }

    // Everything to offer for this job; baked wins a clash.
    public static IReadOnlyList<Extra> AllFor(FightProfile fight, string? job)
    {
        if (string.IsNullOrEmpty(job)) return Array.Empty<Extra>();
        var result = For(fight.TerritoryId)
            .Where(e => string.Equals(e.Job, job, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var e in ForCustomSheet(fight, job))
            if (!result.Any(r => string.Equals(r.Action, e.Action, StringComparison.OrdinalIgnoreCase)))
                result.Add(e);
        return result;
    }
}
