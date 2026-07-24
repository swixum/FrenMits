using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// The Dancing Mad sheet-update merge engine: re-bakes the built-in from the
// latest baked timeline while KEEPING the custom lines people added and every
// per-line tweak, by diffing against the frozen previous bake (DmuLegacy /
// TankMitsLegacy). Only the config migrations drive this.
public static class DmuRebake
{
    // Re-bake the Dancing Mad built-in from the (updated) sheet while KEEPING the
    // custom lines people added.
    public static int SmartRebake(Configuration config)
    {
        var n = 0;
        foreach (var f in config.Fights)
        {
            if (f.TerritoryId != Builtin.DmuTerritory) continue;

            if (!string.IsNullOrEmpty(f.Slot))
                f.Lines = MergeSlot(f, f.Slot, f.Lines);
            foreach (var key in new List<string>(f.SavedSlots.Keys))
                f.SavedSlots[key] = MergeSlot(f, key, f.SavedSlots[key]);

            f.SyncPoints = Builtin.SyncPoints(f.TerritoryId);
            f.BossAnchors = Builtin.BossAnchors(f.TerritoryId);
            n++;
        }
        if (n > 0) config.Save();
        return n;
    }

    private static List<MitLine> MergeSlot(FightProfile fight, string slot, List<MitLine> existing)
    {
        // The DMU data files stay keyed by their native MT/OT/D1-style labels.
        var native = SlotNames.ToLegacy(slot);
        var oldBaked = DmuLegacy.BuildLines(native);
        // Deleted calls stay deleted through a sheet re-bake too.
        var newBaked = DmuData.BuildLines(native)
            .Where(b => !Builtin.IsDeleted(fight, slot, b)).ToList();

        // Exact match against the previous bake (time + action + mechanic).
        static bool SameBaked(MitLine a, MitLine b)
            => MathF.Abs(a.Time - b.Time) < 0.6f
               && string.Equals(a.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase)
               && string.Equals(a.Mechanic.Trim(), b.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase);

        // Mit parts of a combined call ("Divine Caress + Asylum" -> two parts),
        // for containment checks between bake versions.
        static string[] Parts(string action)
            => action.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        // Every mit named by `a` is also named by `b` (case-insensitive).
        static bool Covers(MitLine b, MitLine a)
            => Parts(a.Action).All(p => Parts(b.Action).Contains(p, StringComparer.OrdinalIgnoreCase));

        // "Shadows a real call": the same spoken action within a few seconds of a
        // current baked line.
        static bool Shadows(MitLine line, List<MitLine> baked)
            => baked.Any(b => MathF.Abs(b.Time - line.Time) < 6f
                              && (string.Equals(b.Action.Trim(), line.Action.Trim(), StringComparison.OrdinalIgnoreCase)
                                  || Covers(b, line)));

        // Keep a line only if it does NOT shadow a baked call (no overlap) AND it is
        // either a user-flagged custom or not a recognised old sheet-baked line.
        var customs = existing
            .Where(l => !Shadows(l, newBaked) && (l.Custom || !oldBaked.Any(b => SameBaked(l, b))))
            .ToList();

        foreach (var c in customs) c.Custom = true; // flag survivors so future updates keep them cleanly

        // Before a replaced sheet-owned line goes, carry its per-line tweaks onto
        // the new baked call it corresponds to, so a sheet update never costs
        // anyone their offsets or settings.
        var donors = existing.Except(customs).ToList();
        var matched = new HashSet<MitLine>();

        static string BaseAction(string a)
        {
            var i = a.IndexOf('(');
            return (i > 0 ? a[..i] : a).Trim();
        }
        static void Carry(MitLine to, MitLine from)
        {
            to.OffsetSeconds = from.OffsetSeconds;
            to.OffsetManual = from.OffsetManual;
            to.CoverUntil = from.CoverUntil;
            to.Enabled = from.Enabled;
            to.LeadOverride = from.LeadOverride;
            to.Tts = from.Tts;
            to.Sound = from.Sound;
            to.Color = from.Color;
            to.IconId = from.IconId;
            if (from.Jobs.Count > 0 && to.Jobs.Count == 0) to.Jobs = new List<string>(from.Jobs);
        }

        foreach (var b in newBaked) // pass 1: identical calls keep their tweaks
        {
            var exact = donors.FirstOrDefault(d => SameBaked(d, b));
            if (exact == null) continue;
            donors.Remove(exact);
            matched.Add(b);
            Carry(b, exact);
        }
        foreach (var b in newBaked) // pass 2: moved / renamed calls
        {
            if (matched.Contains(b)) continue;
            var near = donors
                .Where(d => MathF.Abs(d.Time - b.Time) <= 30f
                            && (string.Equals(d.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase)
                                || string.Equals(BaseAction(d.Action), BaseAction(b.Action), StringComparison.OrdinalIgnoreCase)
                                || Covers(b, d)))
                .OrderBy(d => MathF.Abs(d.Time - b.Time))
                .FirstOrDefault();
            if (near == null) continue;
            donors.Remove(near);
            Carry(b, near);
        }

        var result = new List<MitLine>(newBaked);
        result.AddRange(customs);
        return result.OrderBy(l => l.Time).ToList();
    }

    // The BRD/MNK/PLD job-mitigation anchors that moved when they were re-timed
    // to sheet v5.0 rows (old time/mechanic -> new).
    private static readonly (string Job, string Action, float OldTime, string OldMech, float NewTime, string NewMech)[] ExtraMoves =
    {
        ("BRD", "Nature's Minne", 249, "Towers I", 250, "Towers I"),
        ("BRD", "Nature's Minne", 451, "Bowels of Agony (Chaos)", 450, "Bowels of Agony (Chaos)"),
        ("BRD", "Nature's Minne", 789, "Grand Cross", 793, "Grand Cross"),
        ("BRD", "Nature's Minne", 922, "Chaotic Flood", 928, "Chaotic Flood"),
        ("BRD", "Nature's Minne", 1046, "Fell Forces (3x)", 1062, "Forsaken (1st Hit)"),
        ("MNK", "Mantra", 237, "Forsaken", 236, "Forsaken"),
        ("MNK", "Mantra", 451, "Bowels of Agony (Chaos)", 450, "Bowels of Agony (Chaos)"),
        ("MNK", "Mantra", 544, "The Decisive Battle", 545, "The Decisive Battle"),
        ("MNK", "Mantra", 765, "Inferno/Tsunami", 769, "Inferno/Tsunami"),
        ("MNK", "Mantra", 905, "Ultima Repeater", 911, "Ultima Repeater"),
        ("PLD", "Passage of Arms", 342, "Light of Judgement", 343, "Light of Judgement"),
        ("PLD", "Passage of Arms", 609, "Shocking Impact", 609, "Shocking Impact/Shockwave"),
        ("PLD", "Passage of Arms", 789, "Grand Cross", 793, "Grand Cross"),
        ("PLD", "Passage of Arms", 922, "Chaotic Flood", 928, "Chaotic Flood"),
    };

    // One-time v18 upgrade: bring already-added DMU tank-buster plans and the
    // BRD/MNK/PLD job-mitigation lines up to the sheet v5.0 data.
    public static void UpgradeTankAndExtraLines(Configuration config)
    {
        foreach (var f in config.Fights)
        {
            if (f.TerritoryId != Builtin.DmuTerritory) continue;
            UpgradeSet(f, f.Lines);
            foreach (var key in new List<string>(f.SavedSlots.Keys))
                UpgradeSet(f, f.SavedSlots[key]);
        }
        config.Save();
    }

    private static void UpgradeSet(FightProfile fight, List<MitLine> lines)
    {
        // Job-mitigation extras: re-time in place, keeping every per-line tweak.
        foreach (var l in lines)
            foreach (var m in ExtraMoves)
                if (MathF.Abs(l.Time - m.OldTime) < 0.5f
                    && string.Equals(l.Action, m.Action, StringComparison.OrdinalIgnoreCase)
                    && l.Mechanic == m.OldMech
                    && l.Jobs.Contains(m.Job, StringComparer.OrdinalIgnoreCase))
                {
                    l.Time = m.NewTime;
                    l.Mechanic = m.NewMech;
                    break;
                }

        // Tank-buster plans (the card adds them as "Tank:" lines tagged to one job).
        foreach (var job in new[] { "WAR", "PLD", "DRK", "GNB" })
        {
            var mine = lines.Where(l => l.Mechanic.StartsWith("Tank:", StringComparison.Ordinal)
                                        && l.Jobs.Count == 1
                                        && string.Equals(l.Jobs[0], job, StringComparison.OrdinalIgnoreCase)).ToList();
            if (mine.Count == 0) continue;

            // Find which pairing's old plan these came from by counting exact
            // matches against each old bake, with the remembered dropdown pick
            // breaking ties.
            string? comp = null;
            var matched = new List<MitLine>();
            foreach (var c in TankMits.Comps(Builtin.DmuTerritory))
            {
                if (!TankMits.Jobs(c).Contains(job)) continue;
                var old = TankMitsLegacy.DmuFor(c, job);
                var hits = mine.Where(l => old.Any(e =>
                    MathF.Abs(l.Time - e.Time) < 0.5f
                    && l.Mechanic == $"Tank: {e.Mechanic}"
                    && l.Action == e.Action)).ToList();
                if (hits.Count > matched.Count
                    || (hits.Count == matched.Count && hits.Count > 0 && c == fight.TankPairing))
                {
                    comp = c;
                    matched = hits;
                }
            }
            if (comp == null || matched.Count == 0) continue; // fully hand-edited: hands off

            // Swap the unedited old lines for the new plan; edited lines stay,
            // and win over a new entry landing on the same moment.
            foreach (var l in matched) lines.Remove(l);
            var kept = lines.Where(l => l.Mechanic.StartsWith("Tank:", StringComparison.Ordinal)
                                        && l.Jobs.Count == 1
                                        && string.Equals(l.Jobs[0], job, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var e in TankMits.For(Builtin.DmuTerritory, comp, job))
            {
                if (kept.Any(k => MathF.Abs(k.Time - e.Time) < 1f)) continue;
                var donor = matched.FirstOrDefault(d => MathF.Abs(d.Time - e.Time) < 0.5f);
                lines.Add(new MitLine
                {
                    Time = e.Time,
                    Mechanic = $"Tank: {e.Mechanic}",
                    Action = e.Action,
                    Jobs = new List<string> { job },
                    Custom = true,
                    Enabled = donor?.Enabled ?? true,
                    OffsetSeconds = donor?.OffsetSeconds ?? 0f,
                    OffsetManual = donor?.OffsetManual ?? false,
                    CoverUntil = donor?.CoverUntil ?? 0f,
                    LeadOverride = donor?.LeadOverride ?? 0f,
                    Tts = donor?.Tts ?? "",
                    Sound = donor?.Sound ?? true,
                    Color = donor?.Color ?? 0,
                    IconId = donor?.IconId ?? 0,
                });
            }
        }

        var sorted = lines.OrderBy(l => l.Time).ToList();
        lines.Clear();
        lines.AddRange(sorted);
    }
}
