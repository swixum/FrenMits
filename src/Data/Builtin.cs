using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Registry of fights that ship with baked mit timelines + resync anchors.
public static class Builtin
{
    public const ushort DmuTerritory = 1363;
    public const ushort FruTerritory = 1238;
    // M12S (AAC Heavyweight M4 Savage), per the cactbot r12s timeline.
    public const ushort M12sTerritory = 1327;
    // The legacy ultimates, timed from Ikuya's sheets against the cactbot
    // timelines (see IkuyaTimelines).
    public const ushort UcobTerritory = 733;
    public const ushort UwuTerritory = 777;
    public const ushort TeaTerritory = 887;
    public const ushort DsrTerritory = 968;
    public const ushort TopTerritory = 1122;

    public static readonly (ushort Territory, string Name, string Category)[] Fights =
    {
        (DmuTerritory, "Dancing Mad (UMAD)", "Ultimate"),
        (FruTerritory, "Futures Rewritten (FRU)", "Ultimate"),
        (UcobTerritory, "Unending Coil of Bahamut (UCOB)", "Ultimate"),
        (UwuTerritory, "Weapon's Refrain (UWU)", "Ultimate"),
        (TeaTerritory, "Epic of Alexander (TEA)", "Ultimate"),
        (DsrTerritory, "Dragonsong's Reprise (DSR)", "Ultimate"),
        (TopTerritory, "The Omega Protocol (TOP)", "Ultimate"),
        (M12sTerritory, "M12S - Lindwurm", "Savage"),
    };

    public static bool Has(uint territory) =>
        territory is DmuTerritory or FruTerritory or M12sTerritory || IkuyaTimelines.Has(territory);

    public static string Name(uint territory) => territory switch
    {
        FruTerritory => "Futures Rewritten (FRU)",
        M12sTerritory => "M12S - Lindwurm",
        UcobTerritory => "Unending Coil of Bahamut (UCOB)",
        UwuTerritory => "Weapon's Refrain (UWU)",
        TeaTerritory => "Epic of Alexander (TEA)",
        DsrTerritory => "Dragonsong's Reprise (DSR)",
        TopTerritory => "The Omega Protocol (TOP)",
        _ => "Dancing Mad (UMAD)"
    };

    public static string Category(uint territory)
    {
        foreach (var f in Fights)
            if (f.Territory == territory) return f.Category;
        return "Other";
    }

    public static string[] Slots(uint territory) => territory switch
    {
        FruTerritory => FruData.Slots,
        M12sTerritory => M12sData.Slots,
        _ when IkuyaTimelines.Has(territory) => IkuyaTimelines.Slots,
        _ => DmuData.Slots,
    };

    // Canonical cross-fight roles for the global role picker. One pick maps to
    // whatever slot code each fight uses for that role (DMU/M12S use MT/OT/D1..,
    // FRU uses T1/T2/M1../R/Caster), so it applies sensibly everywhere.
    public static readonly string[] Roles =
        { "Main Tank", "Off Tank", "WHM", "AST", "SCH", "SGE", "Melee 1", "Melee 2", "Phys Ranged", "Caster" };

    static readonly Dictionary<string, string[]> RoleSlotCodes = new()
    {
        ["Main Tank"] = new[] { "MT", "T1" },
        ["Off Tank"] = new[] { "OT", "T2" },
        ["WHM"] = new[] { "WHM" },
        ["AST"] = new[] { "AST" },
        ["SCH"] = new[] { "SCH" },
        ["SGE"] = new[] { "SGE" },
        ["Melee 1"] = new[] { "D1", "M1" },
        ["Melee 2"] = new[] { "D2", "M2" },
        ["Phys Ranged"] = new[] { "D3", "R" },
        ["Caster"] = new[] { "D4", "Caster" },
    };

    // The slot code a given fight uses for a canonical role, or null if it has none.
    public static string? RoleSlot(uint territory, string role)
    {
        if (string.IsNullOrEmpty(role) || !RoleSlotCodes.TryGetValue(role, out var codes)) return null;
        var slots = Slots(territory);
        foreach (var c in codes)
            if (slots.Contains(c, StringComparer.OrdinalIgnoreCase)) return c;
        return null;
    }

    // Phase start times for the practice phase-jump. Empty for fights with no
    // phase data (the practice UI then doesn't show).
    public static List<(string Name, float Time)> PhaseStarts(uint territory) => territory switch
    {
        _ when IkuyaTimelines.Has(territory) => IkuyaTimelines.PhaseStarts(territory),
        DmuTerritory => DmuData.PhaseStarts(),
        _ => new(),
    };

    // The sheet's per-phase "Notes" footer, shown at the bottom of the Sheet
    // View. Empty for fights whose sheet has no notes.
    public static string PhaseNotes(uint territory, string phase) => territory switch
    {
        DmuTerritory => DmuData.PhaseNotes(phase),
        _ => "",
    };

    // Long display title for a phase key ("P1" -> "Phase 1: Kefka").
    public static string PhaseTitle(uint territory, string phase) => territory switch
    {
        DmuTerritory => DmuData.PhaseTitle(phase),
        _ => phase,
    };

    public static List<MitLine> BuildLines(uint territory, string slot) => territory switch
    {
        FruTerritory => FruData.BuildLines(slot),
        M12sTerritory => M12sData.BuildLines(slot),
        _ when IkuyaTimelines.Has(territory) => IkuyaTimelines.BuildLines(territory, slot),
        _ => DmuData.BuildLines(slot),
    };

    public static List<SyncPoint> SyncPoints(uint territory) => territory switch
    {
        FruTerritory => FruData.SyncPoints(),
        M12sTerritory => M12sData.SyncPoints(),
        _ when IkuyaTimelines.Has(territory) => IkuyaTimelines.SyncPoints(territory),
        _ => DmuData.SyncPoints(),
    };

    public static List<BossAnchor> BossAnchors(uint territory) => territory switch
    {
        FruTerritory => FruData.BossAnchors(),
        M12sTerritory => M12sData.BossAnchors(),
        _ when IkuyaTimelines.Has(territory) => IkuyaTimelines.BossAnchors(territory),
        _ => DmuData.BossAnchors(),
    };

    // Two baked lines are "the same call" when they share a time + mechanic, so a
    // re-load recognizes lines you already have (and may have edited).
    public static bool SameCall(MitLine a, MitLine b)
        => MathF.Abs(a.Time - b.Time) < 0.75f
           && string.Equals(a.Mechanic.Trim(), b.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase);

    // A deletion tombstone suppresses a baked line when the spoken action matches
    // within a wide window (same "a fight never reuses one mit that close"
    // reasoning as the de-overlap in ApplySlot), so a sheet update that re-times
    // or renames the mechanic can't resurrect a deleted call. Lines with no
    // action on either side fall back to the mechanic label.
    public static bool MatchesTombstone(DeletedCall d, string slot, MitLine baked)
        => string.Equals(d.Slot, slot, StringComparison.OrdinalIgnoreCase)
           && MathF.Abs(d.Time - baked.Time) < 6f
           && (!string.IsNullOrWhiteSpace(d.Action) || !string.IsNullOrWhiteSpace(baked.Action)
               ? string.Equals(d.Action.Trim(), baked.Action.Trim(), StringComparison.OrdinalIgnoreCase)
               : string.Equals(d.Mechanic.Trim(), baked.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsDeleted(FightProfile fight, string slot, MitLine baked)
        => fight.DeletedCalls.Any(d => MatchesTombstone(d, slot, baked));

    // Tombstone the ORIGINAL coordinates + flag the line Custom before an edit
    // mutates it, so re-bakes keep the user's version instead of reverting it.
    // Slot-explicit: the fight-page editor passes the active slot, the sheet
    // view passes whichever column is being edited. Call BEFORE mutating.
    public static void PreserveEdit(FightProfile fight, string slot, MitLine line)
    {
        if (line.Custom || !Has(fight.TerritoryId) || string.IsNullOrEmpty(slot)) return;
        fight.DeletedCalls.Add(new DeletedCall
        {
            Slot = slot,
            Time = line.Time,
            Mechanic = line.Mechanic,
            Action = line.Action,
        });
        line.Custom = true;
    }

    // Make `slot` the fight's active slot and load its mits — and ONLY its mits.
    //  - Switching to a different slot stashes the slot you're leaving and swaps in
    //    the target slot's own set (your saved edits for it, or a fresh bake).
    //  - Staying on the same slot just tops up any newly-baked lines (keeps edits).
    // Never mixes one slot's lines into another. Returns how many lines were added.
    public static int ApplySlot(FightProfile fight, string slot)
    {
        if (string.IsNullOrEmpty(slot))
            slot = Slots(fight.TerritoryId).FirstOrDefault() ?? "";

        var topUp = true;

        // A fresh bake never includes calls the user deleted from this slot.
        List<MitLine> Bake(string s)
            => BuildLines(fight.TerritoryId, s).Where(b => !IsDeleted(fight, s, b)).ToList();

        if (string.IsNullOrEmpty(fight.Slot))
        {
            // First use / migrating an older profile: adopt this slot. Keep any
            // existing lines as-is (don't top up — we can't assume they're this
            // slot's), otherwise bake the slot fresh.
            fight.Slot = slot;
            if (fight.Lines.Count == 0) fight.Lines = Bake(slot);
            else topUp = false;
        }
        else if (!string.Equals(fight.Slot, slot, StringComparison.OrdinalIgnoreCase))
        {
            fight.SavedSlots[fight.Slot] = fight.Lines;   // stash what we're leaving
            fight.Slot = slot;
            fight.Lines = fight.SavedSlots.TryGetValue(slot, out var saved) && saved.Count > 0
                ? saved                                    // your saved edits for this slot
                : Bake(slot);                              // or a clean bake
        }
        else if (fight.Lines.Count == 0)
        {
            fight.Lines = Bake(slot);
        }

        var added = 0;
        if (topUp)
        {
            var baked = BuildLines(fight.TerritoryId, slot);
            // The bake minus deleted calls: what this slot is actually entitled to.
            // The de-overlap below also checks against THIS list, so a custom line
            // the user added to replace a deleted call doesn't get swept as a
            // duplicate of the (suppressed) original.
            var live = baked.Where(b => !IsDeleted(fight, slot, b)).ToList();
            foreach (var b in live)
                if (!fight.Lines.Any(l => SameCall(l, b)))
                {
                    fight.Lines.Add(b);
                    added++;
                }

            // Drop stale leftovers from an earlier build. SameCall only matches
            // within 0.75s, so when a baked mechanic's time shifted further than
            // that between versions (e.g. Bowels of Agony 448 -> 451) the top-up
            // above added the new line WITHOUT removing the old one — leaving two
            // copies of the same call a few seconds apart, which speak as doubled
            // audio. Remove any surviving line that no longer matches a current
            // baked call yet shadows one: same spoken mit within a few seconds.
            // (A real fight never reuses one mit that close — its cooldown is far
            // longer — so this only ever clears a redundant duplicate.)
            fight.Lines.RemoveAll(l =>
                !string.IsNullOrWhiteSpace(l.Action)
                && !live.Any(b => SameCall(l, b))
                && live.Any(b => MathF.Abs(b.Time - l.Time) < 6f
                                 && string.Equals(b.Action.Trim(), l.Action.Trim(),
                                                  StringComparison.OrdinalIgnoreCase)));

            // Housekeeping: drop tombstones for calls the sheet itself no longer
            // bakes, so the list can't grow stale forever.
            fight.DeletedCalls.RemoveAll(d =>
                string.Equals(d.Slot, slot, StringComparison.OrdinalIgnoreCase)
                && !baked.Any(b => MatchesTombstone(d, slot, b)));
        }

        fight.Lines = fight.Lines.OrderBy(l => l.Time).ToList();
        fight.SavedSlots[slot] = fight.Lines;
        fight.SyncPoints = SyncPoints(fight.TerritoryId);
        fight.BossAnchors = BossAnchors(fight.TerritoryId);
        fight.AutoLoaded = true;
        return added;
    }

    // Discard this slot's edits and reload it straight from the baked sheet.
    // "Edits" includes deletions: the slot's tombstones are cleared, so every
    // sheet call comes back.
    public static void ResetSlot(FightProfile fight, string slot)
    {
        fight.DeletedCalls.RemoveAll(d => string.Equals(d.Slot, slot, StringComparison.OrdinalIgnoreCase));
        fight.Slot = slot;
        fight.Lines = BuildLines(fight.TerritoryId, slot);
        fight.SavedSlots[slot] = fight.Lines;
        fight.SyncPoints = SyncPoints(fight.TerritoryId);
        fight.BossAnchors = BossAnchors(fight.TerritoryId);
        fight.AutoLoaded = true;
    }

    // Best-guess sheet slot for a job, used for the first auto-load before the
    // user has explicitly picked one. Healers map to their own column; everyone
    // else maps by role, preferring the lower number / first seat.
    public static string DefaultSlotForJob(uint territory, string? jobAbbr)
    {
        var slots = Slots(territory);
        if (slots.Length == 0) return "";

        var info = Jobs.ByAbbreviation(jobAbbr);
        if (info is { } job)
        {
            // Healers (and any role whose own abbreviation is a column) map directly.
            var own = slots.FirstOrDefault(s => string.Equals(s, job.Abbreviation, StringComparison.OrdinalIgnoreCase));
            if (own != null) return own;

            var prefs = job.Role switch
            {
                JobRole.Tank => new[] { "MT", "T1", "OT", "T2" },
                JobRole.Melee => new[] { "D1", "M1", "D2", "M2" },
                JobRole.PhysicalRanged => new[] { "D3", "R" },
                JobRole.Caster => new[] { "D4", "Caster" },
                _ => Array.Empty<string>(),
            };
            foreach (var p in prefs)
            {
                var hit = slots.FirstOrDefault(s => string.Equals(s, p, StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit;
            }
        }
        return slots[0];
    }
}
