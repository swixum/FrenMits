using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// One naming standard for sheet columns.
public static class SlotNames
{
    // The canonical column set every built-in presents.
    public static readonly string[] Standard =
        { "T1", "T2", "WHM", "AST", "SCH", "SGE", "M1", "M2", "R1", "R2" };

    // Any known alias to its canonical name.
    public static string Canon(string? slot)
    {
        var s = (slot ?? "").Trim();
        return s.ToUpperInvariant() switch
        {
            "MT" or "T1" => "T1",
            "OT" or "T2" => "T2",
            "D1" or "M1" => "M1",
            "D2" or "M2" => "M2",
            "D3" or "R" or "R1" => "R1",
            "D4" or "CASTER" or "R2" => "R2",
            "WHM" => "WHM", "AST" => "AST", "SCH" => "SCH", "SGE" => "SGE",
            "H1" => "H1", "H2" => "H2",
            _ => s,
        };
    }

    // Canonical to the labels the DMU-style files use.
    public static string ToLegacy(string slot) => Canon(slot) switch
    {
        "T1" => "MT", "T2" => "OT",
        "M1" => "D1", "M2" => "D2",
        "R1" => "D3", "R2" => "D4",
        var c => c,
    };

    // Canonical to FRU's native labels.
    public static string ToFru(string slot) => Canon(slot) switch
    {
        "R1" => "R", "R2" => "Caster",
        var c => c,
    };

    // Rename a saved fight onto the standard, idempotently.
    public static bool NormalizeFight(FightProfile fight)
    {
        var changed = false;

        var slot = Canon(fight.Slot);
        if (!string.Equals(slot, fight.Slot, StringComparison.Ordinal)) { fight.Slot = slot; changed = true; }

        if (fight.SavedSlots.Keys.Any(k => !string.Equals(Canon(k), k, StringComparison.Ordinal)))
        {
            var moved = new Dictionary<string, List<MitLine>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, lines) in fight.SavedSlots)
            {
                var ck = Canon(key);
                // A collision (both MT and T1 stashed) keeps the fuller plan.
                if (!moved.TryGetValue(ck, out var have) || lines.Count > have.Count)
                    moved[ck] = lines;
            }
            fight.SavedSlots.Clear();
            foreach (var (key, lines) in moved) fight.SavedSlots[key] = lines;
            changed = true;
        }

        for (var i = 0; i < fight.CustomSlots.Count; i++)
        {
            var c = Canon(fight.CustomSlots[i]);
            if (!string.Equals(c, fight.CustomSlots[i], StringComparison.Ordinal))
            { fight.CustomSlots[i] = c; changed = true; }
        }
        // Two old names can collide, so drop the later one.
        for (var i = fight.CustomSlots.Count - 1; i > 0; i--)
            if (fight.CustomSlots.Take(i).Contains(fight.CustomSlots[i], StringComparer.OrdinalIgnoreCase))
            { fight.CustomSlots.RemoveAt(i); changed = true; }

        foreach (var d in fight.DeletedCalls)
        {
            var c = Canon(d.Slot);
            if (!string.Equals(c, d.Slot, StringComparison.Ordinal)) { d.Slot = c; changed = true; }
        }

        // The active slot stays aliased, or a switch loses edits.
        if (!string.IsNullOrEmpty(fight.Slot))
        {
            // On a rename collision the fuller plan wins.
            if (changed && fight.SavedSlots.TryGetValue(fight.Slot, out var winner)
                && !ReferenceEquals(winner, fight.Lines) && winner.Count > fight.Lines.Count)
                fight.Lines = winner;
            // Re-aliased on EVERY load, not only when a rename happened.
            fight.SavedSlots[fight.Slot] = fight.Lines;
        }

        return changed;
    }
}
