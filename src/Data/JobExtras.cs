using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Optional, job-specific mitigation timers derived from FFLogs clears (raalm.com),
// the same way the WHM Asylum line was. Offered as a one-click add on the fight
// page when you're on that job. Lines are job-restricted (only fire for that job)
// and flagged Custom, so a sheet re-bake keeps them.
//
// Every schedule is spaced to the ability's real recast - no call ever asks you to
// press something that's still on cooldown.
public static class JobExtras
{
    public sealed record Extra(string Job, string Action, float Recast, (int Time, string Mechanic)[] Lines);

    private static readonly Dictionary<uint, Extra[]> ByZone = new()
    {
        [Builtin.DmuTerritory] = new[]
        {
            // Bard - Nature's Minne (120s recast). Anchored to sheet v5.0 rows;
            // the last press moved from Fell Forces (3x) to Forsaken (1st Hit)
            // because the re-timed Chaotic Flood (928) leaves Minne on cooldown
            // at 1045.
            new Extra("BRD", "Nature's Minne", 120f, new[]
            {
                (63, "Light of Judgment"),
                (250, "Towers I"),
                (450, "Bowels of Agony (Chaos)"),
                (637, "Thunder III (5th Set)"),
                (793, "Grand Cross"),
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
                (793, "Grand Cross"),
                (928, "Chaotic Flood"),
            }),
            // DNC/MCH/RDM schedules below are OPTIMIZED from raalm.com m-spec
            // top-100 kill logs: every window real players use (>=10% of kills,
            // phase-normalized medians), then the max number of presses that fits
            // the recast, preferring the most-used windows. Times are the logs'
            // actual press moments; % = share of kills pressing there.

            // Dancer - Curing Waltz (60s recast), 10 presses.
            // 68/60/16/77/17/35/68/75/68/84% usage.
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
            // Dismantle / Magick Barrier / Tempera Grassa follow the sheet's
            // "Extras" checkmark column (v5.0 marks 8 rows; the 470s+478s pair is
            // one press, the 10s buff covers both). 7 presses, exactly filling
            // the 120s recast. raalm's top logs diverge in P3/P4 (370/534, and
            // most skip Grand Cross) but the sheet's plan is the plan.
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
        },
    };

    public static IReadOnlyList<Extra> For(uint territory)
        => ByZone.TryGetValue(territory, out var e) ? e : Array.Empty<Extra>();

    public static Extra? For(uint territory, string? job)
        => string.IsNullOrEmpty(job)
            ? null
            : For(territory).FirstOrDefault(e => string.Equals(e.Job, job, StringComparison.OrdinalIgnoreCase));
}
