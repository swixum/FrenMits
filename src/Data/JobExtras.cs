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
            // Bard - Nature's Minne (120s recast)
            new Extra("BRD", "Nature's Minne", 120f, new[]
            {
                (63, "Light of Judgment"),
                (249, "Towers I"),
                (451, "Bowels of Agony (Chaos)"),
                (637, "Thunder III (5th Set)"),
                (789, "Grand Cross"),
                (922, "Chaotic Flood"),
                (1046, "Fell Forces (3x)"),
            }),
            // Monk - Mantra (90s recast)
            new Extra("MNK", "Mantra", 90f, new[]
            {
                (88, "Gravitas II (Part I)"),
                (237, "Forsaken"),
                (451, "Bowels of Agony (Chaos)"),
                (544, "The Decisive Battle"),
                (650, "Black Holes III (6th Tether Set)"),
                (765, "Inferno/Tsunami"),
                (905, "Ultima Repeater"),
            }),
            // Paladin - Passage of Arms (120s recast)
            new Extra("PLD", "Passage of Arms", 120f, new[]
            {
                (63, "Light of Judgment"),
                (342, "Light of Judgement"),
                (609, "Shocking Impact"),
                (789, "Grand Cross"),
                (922, "Chaotic Flood"),
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
            // Machinist - Dismantle (120s recast), 6 presses.
            // 95/71/36/61/61/96% usage.
            new Extra("MCH", "Dismantle", 120f, new[]
            {
                (60, "Light of Judgment"),
                (230, "Forsaken"),
                (370, "Wings of Destruction"),
                (534, "Thunder III (2nd Set)"),
                (906, "Ultima Repeater"),
                (1055, "Forsaken (1st Hit)"),
            }),
            // Red Mage - Magick Barrier (120s recast), 7 presses.
            // 72/94/59/43/76/91/97% usage.
            new Extra("RDM", "Magick Barrier", 120f, new[]
            {
                (38, "Mystery Magic"),
                (268, "Towers III (All Things Ending)"),
                (511, "Vacuum Wave"),
                (634, "Thunder III (5th Set)"),
                (771, "Inferno/Tsunami"),
                (904, "Ultima Repeater"),
                (1056, "Forsaken (1st Hit)"),
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
