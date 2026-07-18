using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// One naming standard for sheet columns, everywhere: T1 T2 for tanks, the four
// healer jobs as their own columns (WHM/AST fill the H1 seat, SCH/SGE fill H2),
// M1 M2 for melee, R1 R2 for phys ranged / caster. Sheets that call a column
// something else (MT/OT, D1-D4, FRU's R/Caster) are translated on the way in;
// the baked data files keep their native labels internally.
public static class SlotNames
{
    // The canonical column set (and order) every built-in sheet presents.
    public static readonly string[] Standard =
        { "T1", "T2", "WHM", "AST", "SCH", "SGE", "M1", "M2", "R1", "R2" };

    // Any known alias -> its canonical name. Unknown labels (custom columns,
    // player names) pass through untouched.
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

    // Canonical -> the MT/OT/D1-D4 labels the DMU-style data files are keyed by.
    public static string ToLegacy(string slot) => Canon(slot) switch
    {
        "T1" => "MT", "T2" => "OT",
        "M1" => "D1", "M2" => "D2",
        "R1" => "D3", "R2" => "D4",
        var c => c,
    };

    // Canonical -> FRU's native labels (only its ranged pair differs).
    public static string ToFru(string slot) => Canon(slot) switch
    {
        "R1" => "R", "R2" => "Caster",
        var c => c,
    };

    // Rename a saved fight onto the standard, idempotently: active slot,
    // per-slot stashes, custom columns and deletion tombstones. Returns true
    // when anything actually changed (so the caller knows to save).
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

        foreach (var d in fight.DeletedCalls)
        {
            var c = Canon(d.Slot);
            if (!string.Equals(c, d.Slot, StringComparison.Ordinal)) { d.Slot = c; changed = true; }
        }

        // The active slot's lines must stay aliased into the stash under the
        // (possibly renamed) key, or the next slot switch would lose edits.
        if (changed && !string.IsNullOrEmpty(fight.Slot))
            fight.SavedSlots[fight.Slot] = fight.Lines;

        return changed;
    }
}
