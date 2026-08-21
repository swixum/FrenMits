using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Encounters;

// Re-bakes the DMU built-in, keeping custom lines and tweaks.
public static class DmuRebake
{
    // Re-bake from the updated sheet, keeping added lines.
    public static int SmartRebake(List<FightProfile> fights)
    {
        var n = 0;
        foreach (var f in fights)
        {
            if (f.TerritoryId != Builtin.DmuTerritory) continue;
            // A Custom sheet in the zone is the user's, not ours to rebake.
            if (f.Category == "Custom") continue;

            if (!string.IsNullOrEmpty(f.Slot))
                f.Lines = MergeSlot(f, f.Slot, f.Lines);
            foreach (var key in new List<string>(f.SavedSlots.Keys))
                f.SavedSlots[key] = MergeSlot(f, key, f.SavedSlots[key]);

            f.SyncPoints = Builtin.SyncPoints(f.TerritoryId);
            f.BossAnchors = Builtin.BossAnchors(f.TerritoryId);
            n++;
        }
        return n;
    }

    private static List<MitLine> MergeSlot(FightProfile fight, string slot, List<MitLine> existing)
    {
        // The DMU data files stay keyed by their native labels.
        var native = SlotNames.ToLegacy(slot);
        var oldBaked = DmuLegacy.BuildLines(native);
        // Deleted calls stay deleted through a re-bake. Clones, because the
        // merged fight owns these lines and edits must not reach the cache.
        var newBaked = Builtin.BakedLines(Builtin.DmuTerritory, native)
            .Where(b => !Builtin.IsDeleted(fight, slot, b)).Select(b => b.Clone()).ToList();

        // Exact match against the previous bake.
        static bool SameBaked(MitLine a, MitLine b)
            => MathF.Abs(a.Time - b.Time) < 0.6f
               && string.Equals(a.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase)
               && string.Equals(a.Mechanic.Trim(), b.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase);

        // The same spoken action within a few seconds of a baked line.
        static bool Shadows(MitLine line, List<MitLine> baked)
            => baked.Any(b => MathF.Abs(b.Time - line.Time) < 6f
                              && string.Equals(b.Action.Trim(), line.Action.Trim(), StringComparison.OrdinalIgnoreCase));

        // Keep a line only if it shadows nothing and isn't old bake.
        var customs = existing
            .Where(l => !Shadows(l, newBaked) && (l.Custom || !oldBaked.Any(b => SameBaked(l, b))))
            .ToList();

        foreach (var c in customs) c.Custom = true; // flag survivors so future updates keep them cleanly

        // Carry a replaced line's tweaks onto the new call.
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
            if (from.TankPairs.Count > 0 && to.TankPairs.Count == 0) to.TankPairs = new List<string>(from.TankPairs);
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
                                || string.Equals(BaseAction(d.Action), BaseAction(b.Action), StringComparison.OrdinalIgnoreCase)))
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

    // The job-mitigation anchors that moved in sheet v5.0.
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

    // One-time v18 upgrade onto the sheet v5.0 data.
    public static void UpgradeTankAndExtraLines(List<FightProfile> fights)
    {
        foreach (var f in fights)
        {
            if (f.TerritoryId != Builtin.DmuTerritory) continue;
            UpgradeSet(f, f.Lines);
            foreach (var key in new List<string>(f.SavedSlots.Keys))
                UpgradeSet(f, f.SavedSlots[key]);
        }
    }

    private static void UpgradeSet(FightProfile fight, List<MitLine> lines)
    {
        // Job-mitigation extras: re-time in place, keeping tweaks.
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

        var sorted = lines.OrderBy(l => l.Time).ToList();
        lines.Clear();
        lines.AddRange(sorted);
    }
}
