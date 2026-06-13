using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Registry of fights that ship with baked mit timelines + resync anchors.
public static class Builtin
{
    public const ushort DmuTerritory = 1363;
    public const ushort FruTerritory = 1238;

    public static readonly (ushort Territory, string Name)[] Fights =
    {
        (DmuTerritory, "Dancing Mad (Ultimate)"),
        (FruTerritory, "Futures Rewritten (Ultimate)"),
    };

    public static bool Has(uint territory) => territory is DmuTerritory or FruTerritory;

    public static string Name(uint territory) => territory switch
    {
        FruTerritory => "Futures Rewritten (Ultimate)",
        _ => "Dancing Mad (Ultimate)"
    };

    public static string[] Slots(uint territory) => territory == FruTerritory ? FruData.Slots : DmuData.Slots;

    public static List<MitLine> BuildLines(uint territory, string slot) =>
        territory == FruTerritory ? FruData.BuildLines(slot) : DmuData.BuildLines(slot);

    public static List<SyncPoint> SyncPoints(uint territory) =>
        territory == FruTerritory ? FruData.SyncPoints() : DmuData.SyncPoints();

    public static List<BossAnchor> BossAnchors(uint territory) =>
        territory == FruTerritory ? FruData.BossAnchors() : DmuData.BossAnchors();

    // Two baked lines are "the same call" when they share a time + mechanic, so a
    // re-load recognises lines you already have (and may have edited).
    public static bool SameCall(MitLine a, MitLine b)
        => MathF.Abs(a.Time - b.Time) < 0.75f
           && string.Equals(a.Mechanic.Trim(), b.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase);

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

        if (string.IsNullOrEmpty(fight.Slot))
        {
            // First use / migrating an older profile: adopt this slot. Keep any
            // existing lines as-is (don't top up — we can't assume they're this
            // slot's), otherwise bake the slot fresh.
            fight.Slot = slot;
            if (fight.Lines.Count == 0) fight.Lines = BuildLines(fight.TerritoryId, slot);
            else topUp = false;
        }
        else if (!string.Equals(fight.Slot, slot, StringComparison.OrdinalIgnoreCase))
        {
            fight.SavedSlots[fight.Slot] = fight.Lines;   // stash what we're leaving
            fight.Slot = slot;
            fight.Lines = fight.SavedSlots.TryGetValue(slot, out var saved) && saved.Count > 0
                ? saved                                    // your saved edits for this slot
                : BuildLines(fight.TerritoryId, slot);     // or a clean bake
        }
        else if (fight.Lines.Count == 0)
        {
            fight.Lines = BuildLines(fight.TerritoryId, slot);
        }

        var added = 0;
        if (topUp)
        {
            var baked = BuildLines(fight.TerritoryId, slot);
            foreach (var b in baked)
                if (!fight.Lines.Any(l => SameCall(l, b))) { fight.Lines.Add(b); added++; }
        }

        fight.Lines = fight.Lines.OrderBy(l => l.Time).ToList();
        fight.SavedSlots[slot] = fight.Lines;
        fight.SyncPoints = SyncPoints(fight.TerritoryId);
        fight.BossAnchors = BossAnchors(fight.TerritoryId);
        fight.AutoLoaded = true;
        return added;
    }

    // Discard this slot's edits and reload it straight from the baked sheet.
    public static void ResetSlot(FightProfile fight, string slot)
    {
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
