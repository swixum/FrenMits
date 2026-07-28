using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Registry of fights that ship with baked mit timelines + resync anchors.
public static class Builtin
{
    public const ushort DmuTerritory = 1363;
    public const ushort FruTerritory = 1238;
    // Dawntrail extremes: Hell on Rails and the Unmaking, from log-built sheets
    // (see the official-fight notes in tools/ (kept local)).
    public const ushort DoomtrainTerritory = 1308;
    public const ushort EnuoTerritory = 1362;
    // Zelenia, built end to end from twenty logged kills and planned by the
    // plugin's own Auto-planner - no in-game sheet went into it.
    public const ushort ZeleniaTerritory = 1271;
    // The AAC Light-heavyweight tier - the first Dawntrail savage tier - built the
    // same way the rest were, from twelve logged kills each.
    public const ushort M1sTerritory = 1226;
    public const ushort M2sTerritory = 1228;
    public const ushort M3sTerritory = 1230;
    public const ushort M4sTerritory = 1232;
    // The AAC Cruiserweight tier, built the same way Zelenia was.
    public const ushort M5sTerritory = 1257;
    public const ushort M6sTerritory = 1259;
    public const ushort M7sTerritory = 1261;
    public const ushort M8sTerritory = 1263;
    // The AAC Heavyweight tier.
    public const ushort M9sTerritory = 1321;
    public const ushort M10sTerritory = 1323;
    public const ushort M11sTerritory = 1325;
    public const ushort M12sTerritory = 1327;
    // The legacy ultimates, timed from Ikuya's sheets against the cactbot
    // timelines (see IkuyaTimelines).
    public const ushort UcobTerritory = 733;
    public const ushort UwuTerritory = 777;
    public const ushort TeaTerritory = 887;
    public const ushort DsrTerritory = 968;
    public const ushort TopTerritory = 1122;

    // Newest expansion first, and in release order inside it: the fight someone
    // is raiding today is the one they came to add.
    public static readonly string[] Expansions =
        { "Dawntrail", "Endwalker", "Shadowbringers", "Stormblood" };

    public static readonly (ushort Territory, string Name, string Category, string Expansion)[] Fights =
    {
        (FruTerritory, "Futures Rewritten (FRU)", "Ultimate", "Dawntrail"),
        (DmuTerritory, "Dancing Mad (UMAD)", "Ultimate", "Dawntrail"),
        (M1sTerritory, "M1S - Black Cat", "Savage", "Dawntrail"),
        (M2sTerritory, "M2S - Honey B. Lovely", "Savage", "Dawntrail"),
        (M3sTerritory, "M3S - Brute Bomber", "Savage", "Dawntrail"),
        (M4sTerritory, "M4S - Wicked Thunder", "Savage", "Dawntrail"),
        (M5sTerritory, "M5S - Dancing Green", "Savage", "Dawntrail"),
        (M6sTerritory, "M6S - Sugar Riot", "Savage", "Dawntrail"),
        (M7sTerritory, "M7S - Brute Abombinator", "Savage", "Dawntrail"),
        (M8sTerritory, "M8S - Howling Blade", "Savage", "Dawntrail"),
        (M9sTerritory, "M9S - Vamp Fatale", "Savage", "Dawntrail"),
        (M10sTerritory, "M10S - Red Hot / Deep Blue", "Savage", "Dawntrail"),
        (M11sTerritory, "M11S - The Tyrant", "Savage", "Dawntrail"),
        (M12sTerritory, "M12S - Lindwurm", "Savage", "Dawntrail"),
        (DoomtrainTerritory, "Doomtrain", "Extreme", "Dawntrail"),
        (EnuoTerritory, "Enuo", "Extreme", "Dawntrail"),
        (ZeleniaTerritory, "Zelenia", "Extreme", "Dawntrail"),
        (DsrTerritory, "Dragonsong's Reprise (DSR)", "Ultimate", "Endwalker"),
        (TopTerritory, "The Omega Protocol (TOP)", "Ultimate", "Endwalker"),
        (TeaTerritory, "Epic of Alexander (TEA)", "Ultimate", "Shadowbringers"),
        (UcobTerritory, "Unending Coil of Bahamut (UCOB)", "Ultimate", "Stormblood"),
        (UwuTerritory, "Weapon's Refrain (UWU)", "Ultimate", "Stormblood"),
    };

    public static bool Has(uint territory) =>
        territory is DmuTerritory or FruTerritory
            or M1sTerritory or M2sTerritory or M3sTerritory or M4sTerritory
            or M5sTerritory or M6sTerritory or M7sTerritory
            or M8sTerritory or M9sTerritory or M10sTerritory or M11sTerritory
            or M12sTerritory or DoomtrainTerritory or EnuoTerritory or ZeleniaTerritory
            || IkuyaTimelines.Has(territory);

    public static string Name(uint territory) => territory switch
    {
        FruTerritory => "Futures Rewritten (FRU)",
        DoomtrainTerritory => "Doomtrain",
        ZeleniaTerritory => "Zelenia",
        EnuoTerritory => "Enuo",
        M1sTerritory => "M1S - Black Cat",
        M2sTerritory => "M2S - Honey B. Lovely",
        M3sTerritory => "M3S - Brute Bomber",
        M4sTerritory => "M4S - Wicked Thunder",
        M5sTerritory => "M5S - Dancing Green",
        M6sTerritory => "M6S - Sugar Riot",
        M7sTerritory => "M7S - Brute Abombinator",
        M8sTerritory => "M8S - Howling Blade",
        M9sTerritory => "M9S - Vamp Fatale",
        M10sTerritory => "M10S - Red Hot / Deep Blue",
        M11sTerritory => "M11S - The Tyrant",
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

    public static string Expansion(uint territory)
    {
        foreach (var f in Fights)
            if (f.Territory == territory) return f.Expansion;
        return "";
    }

    // Every built-in sheet presents the ONE standard column set (SlotNames);
    // the data files' native labels are translated in BuildLines.
    public static string[] Slots(uint territory) => SlotNames.Standard;

    // Canonical cross-fight roles for the global role picker.
    public static readonly string[] Roles =
        { "Main Tank", "Off Tank", "WHM", "AST", "SCH", "SGE", "Melee 1", "Melee 2", "Phys Ranged", "Caster" };

    // Healer roles carry the seat-group fallback (WHM/AST sit H1, SCH/SGE sit
    // H2) for custom sheets that use bare H1/H2 healer columns.
    static readonly Dictionary<string, string[]> RoleSlotCodes = new()
    {
        ["Main Tank"] = new[] { "T1", "MT" },
        ["Off Tank"] = new[] { "T2", "OT" },
        ["WHM"] = new[] { "WHM", "H1" },
        ["AST"] = new[] { "AST", "H1" },
        ["SCH"] = new[] { "SCH", "H2" },
        ["SGE"] = new[] { "SGE", "H2" },
        ["Melee 1"] = new[] { "M1", "D1" },
        ["Melee 2"] = new[] { "M2", "D2" },
        ["Phys Ranged"] = new[] { "R1", "D3", "R" },
        ["Caster"] = new[] { "R2", "D4", "Caster" },
    };

    // The slot code a given fight uses for a canonical role, or null if it has none.
    public static string? RoleSlot(uint territory, string role)
        => RoleSlotIn(Slots(territory), role);

    // Same, resolved against any sheet's column list, returning that sheet's own
    // string.
    public static string? RoleSlotIn(IReadOnlyList<string> slots, string role)
    {
        if (string.IsNullOrEmpty(role) || !RoleSlotCodes.TryGetValue(role, out var codes)) return null;
        foreach (var c in codes)
            foreach (var s in slots)
                if (string.Equals(s, c, StringComparison.OrdinalIgnoreCase)) return s;
        return null;
    }

    // Where each of a fight's phases begins.
    public static List<(string Name, float Time)> PhaseStarts(uint territory)
    {
        var starts = RawPhaseStarts(territory);
        return starts.Count > 1 ? starts : new();
    }

    private static List<(string Name, float Time)> RawPhaseStarts(uint territory) => territory switch
    {
        _ when IkuyaTimelines.Has(territory) => IkuyaTimelines.PhaseStarts(territory),
        DmuTerritory => DmuData.PhaseStarts(),
        FruTerritory => FruData.PhaseStarts(),
        DoomtrainTerritory => DoomtrainData.PhaseStarts(),
        EnuoTerritory => EnuoData.PhaseStarts(),
        ZeleniaTerritory => ZeleniaData.PhaseStarts(),
        M1sTerritory => M1sData.PhaseStarts(),
        M2sTerritory => M2sData.PhaseStarts(),
        M3sTerritory => M3sData.PhaseStarts(),
        M4sTerritory => M4sData.PhaseStarts(),
        M5sTerritory => M5sData.PhaseStarts(),
        M6sTerritory => M6sData.PhaseStarts(),
        M7sTerritory => M7sData.PhaseStarts(),
        // Howling Blade's P1 ends on a cutscene, so it is the one log-built fight with
        // a phase jump.
        M8sTerritory => M8sData.PhaseStarts(),
        M9sTerritory => M9sData.PhaseStarts(),
        M10sTerritory => M10sData.PhaseStarts(),
        M11sTerritory => M11sData.PhaseStarts(),
        M12sTerritory => M12sData.PhaseStarts(),
        _ => new(),
    };

    // The sheet's per-phase "Notes" footer, shown at the bottom of the Sheet
    // View (empty for fights whose sheet has no notes).
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

    // Accepts the standard slot names (or any alias) and translates to each
    // data file's native labels.
    public static List<MitLine> BuildLines(uint territory, string slot)
    {
        var lines = Bake(territory, slot);
        CoveredRepeats.Strip(lines);
        // In time order, because a data file need not be.
        return lines.OrderBy(l => l.Time).ToList();
    }

    private static List<MitLine> Bake(uint territory, string slot) => territory switch
    {
        FruTerritory => FruData.BuildLines(SlotNames.ToFru(slot)),
        DoomtrainTerritory => DoomtrainData.BuildLines(SlotNames.ToLegacy(slot)),
        ZeleniaTerritory => ZeleniaData.BuildLines(SlotNames.ToLegacy(slot)),
        EnuoTerritory => EnuoData.BuildLines(SlotNames.ToLegacy(slot)),
        M1sTerritory => M1sData.BuildLines(SlotNames.ToLegacy(slot)),
        M2sTerritory => M2sData.BuildLines(SlotNames.ToLegacy(slot)),
        M3sTerritory => M3sData.BuildLines(SlotNames.ToLegacy(slot)),
        M4sTerritory => M4sData.BuildLines(SlotNames.ToLegacy(slot)),
        M5sTerritory => M5sData.BuildLines(SlotNames.ToLegacy(slot)),
        M6sTerritory => M6sData.BuildLines(SlotNames.ToLegacy(slot)),
        M7sTerritory => M7sData.BuildLines(SlotNames.ToLegacy(slot)),
        M8sTerritory => M8sData.BuildLines(SlotNames.ToLegacy(slot)),
        M9sTerritory => M9sData.BuildLines(SlotNames.ToLegacy(slot)),
        M10sTerritory => M10sData.BuildLines(SlotNames.ToLegacy(slot)),
        M11sTerritory => M11sData.BuildLines(SlotNames.ToLegacy(slot)),
        M12sTerritory => M12sData.BuildLines(SlotNames.ToLegacy(slot)),
        _ when IkuyaTimelines.Has(territory) => IkuyaTimelines.BuildLines(territory, SlotNames.ToLegacy(slot)),
        _ => DmuData.BuildLines(SlotNames.ToLegacy(slot)),
    };

    public static List<SyncPoint> SyncPoints(uint territory) => Dedupe(territory switch
    {
        FruTerritory => FruData.SyncPoints(),
        DoomtrainTerritory => DoomtrainData.SyncPoints(),
        ZeleniaTerritory => ZeleniaData.SyncPoints(),
        EnuoTerritory => EnuoData.SyncPoints(),
        M1sTerritory => M1sData.SyncPoints(),
        M2sTerritory => M2sData.SyncPoints(),
        M3sTerritory => M3sData.SyncPoints(),
        M4sTerritory => M4sData.SyncPoints(),
        M5sTerritory => M5sData.SyncPoints(),
        M6sTerritory => M6sData.SyncPoints(),
        M7sTerritory => M7sData.SyncPoints(),
        M8sTerritory => M8sData.SyncPoints(),
        M9sTerritory => M9sData.SyncPoints(),
        M10sTerritory => M10sData.SyncPoints(),
        M11sTerritory => M11sData.SyncPoints(),
        M12sTerritory => M12sData.SyncPoints(),
        _ when IkuyaTimelines.Has(territory) => IkuyaTimelines.SyncPoints(territory),
        _ => DmuData.SyncPoints(),
    });

    // A sheet can carry two rows for one cast, which used to bake the anchor twice.
    private static List<SyncPoint> Dedupe(List<SyncPoint> points)
    {
        var byCoord = new Dictionary<(uint, int), int>();
        var result = new List<SyncPoint>(points.Count);
        foreach (var sp in points)
        {
            var key = (sp.Ability, (int)MathF.Round(sp.Time * 10f));
            if (byCoord.TryGetValue(key, out var at))
            {
                if (sp.IsPhase) result[at].IsPhase = true;
                continue;
            }
            byCoord[key] = result.Count;
            result.Add(sp);
        }
        return result;
    }

    // Severity grades and tank-buster flags for a built-in.

    // Only overwrites when the built-in actually HAS graded rows, so a user's own
    // custom sheet (whose CustomRows are theirs) can never be wiped by a reload.
    private static void ApplyCustomRows(FightProfile fight)
    {
        var rows = CustomRows(fight.TerritoryId);
        if (rows.Count > 0) fight.CustomRows = rows;
    }

    // One mechanic, one row.
    public static List<CustomRow> CustomRows(uint territory)
    {
        var folded = new List<CustomRow>();
        var at = new Dictionary<(float, string), CustomRow>();
        foreach (var r in RawCustomRows(territory))
        {
            if (at.TryGetValue((r.Time, r.Mechanic), out var seen))
            {
                seen.Hurt = Math.Max(seen.Hurt, r.Hurt);
                seen.Buster |= r.Buster;
                seen.Enrage |= r.Enrage;
                continue;
            }
            at[(r.Time, r.Mechanic)] = r;
            folded.Add(r);
        }
        return folded;
    }

    private static List<CustomRow> RawCustomRows(uint territory) => territory switch
    {
        FruTerritory => FruData.CustomRows(),
        DmuTerritory => DmuData.CustomRows(),
        _ when IkuyaTimelines.Has(territory) => IkuyaTimelines.CustomRows(territory),
        DoomtrainTerritory => DoomtrainData.CustomRows(),
        ZeleniaTerritory => ZeleniaData.CustomRows(),
        EnuoTerritory => EnuoData.CustomRows(),
        M1sTerritory => M1sData.CustomRows(),
        M2sTerritory => M2sData.CustomRows(),
        M3sTerritory => M3sData.CustomRows(),
        M4sTerritory => M4sData.CustomRows(),
        M5sTerritory => M5sData.CustomRows(),
        M6sTerritory => M6sData.CustomRows(),
        M7sTerritory => M7sData.CustomRows(),
        M8sTerritory => M8sData.CustomRows(),
        M9sTerritory => M9sData.CustomRows(),
        M10sTerritory => M10sData.CustomRows(),
        M11sTerritory => M11sData.CustomRows(),
        M12sTerritory => M12sData.CustomRows(),
        _ => new List<CustomRow>(),
    };

    public static List<BossAnchor> BossAnchors(uint territory) => territory switch
    {
        FruTerritory => FruData.BossAnchors(),
        DoomtrainTerritory => DoomtrainData.BossAnchors(),
        ZeleniaTerritory => ZeleniaData.BossAnchors(),
        EnuoTerritory => EnuoData.BossAnchors(),
        M1sTerritory => M1sData.BossAnchors(),
        M2sTerritory => M2sData.BossAnchors(),
        M3sTerritory => M3sData.BossAnchors(),
        M4sTerritory => M4sData.BossAnchors(),
        M5sTerritory => M5sData.BossAnchors(),
        M6sTerritory => M6sData.BossAnchors(),
        M7sTerritory => M7sData.BossAnchors(),
        M8sTerritory => M8sData.BossAnchors(),
        M9sTerritory => M9sData.BossAnchors(),
        M10sTerritory => M10sData.BossAnchors(),
        M11sTerritory => M11sData.BossAnchors(),
        M12sTerritory => M12sData.BossAnchors(),
        _ when IkuyaTimelines.Has(territory) => IkuyaTimelines.BossAnchors(territory),
        _ => DmuData.BossAnchors(),
    };

    // Two baked lines are "the same call" when they share a time + mechanic, so a
    // re-load recognizes lines you already have (and may have edited).
    public static bool SameCall(MitLine a, MitLine b)
        => MathF.Abs(a.Time - b.Time) < 0.75f
           && string.Equals(a.Mechanic.Trim(), b.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase);

    // A deletion tombstone suppresses a baked line whose action matches within a wide
    // window.
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

    // Make `slot` the fight's active slot and load ONLY its mits, never mixing one
    // slot's lines into another (returns how many lines were added).
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
            // First use or an older profile: adopt this slot, keeping existing lines.
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
            var live = baked.Where(b => !IsDeleted(fight, slot, b)).ToList();
            foreach (var b in live)
                if (!fight.Lines.Any(l => SameCall(l, b)))
                {
                    fight.Lines.Add(b);
                    added++;
                }

            // Drop a surviving line that shadows a current baked call, since a fight
            // never reuses a mit that close.
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
        ApplyCustomRows(fight);
        fight.AutoLoaded = true;
        return added;
    }

    // Discard this slot's edits (including deletions - tombstones are cleared) and
    // reload it straight from the baked sheet.
    public static void ResetSlot(FightProfile fight, string slot)
    {
        fight.DeletedCalls.RemoveAll(d => string.Equals(d.Slot, slot, StringComparison.OrdinalIgnoreCase));
        fight.Slot = slot;
        fight.Lines = BuildLines(fight.TerritoryId, slot);
        fight.SavedSlots[slot] = fight.Lines;
        fight.SyncPoints = SyncPoints(fight.TerritoryId);
        fight.BossAnchors = BossAnchors(fight.TerritoryId);
        ApplyCustomRows(fight);
        fight.AutoLoaded = true;
    }

    // Best-guess sheet slot for a job, used for the first auto-load before the
    // user has explicitly picked one.
    public static string DefaultSlotForJob(uint territory, string? jobAbbr)
    {
        var slots = Slots(territory);
        if (slots.Length == 0) return "";
        var hit = DefaultSlotForJobIn(slots, jobAbbr);
        return hit.Length > 0 ? hit : slots[0];
    }

    // Same guess with no first-column fallback: "" means ask rather than guess.
    public static string DefaultSlotForJobIn(IReadOnlyList<string> slots, string? jobAbbr)
    {
        if (slots.Count == 0 || Jobs.ByAbbreviation(jobAbbr) is not { } job) return "";

        // Healers map to their own column (or their H1/H2 seat group).
        if (job.Role == JobRole.Healer)
            return RoleSlotIn(slots, job.Abbreviation) ?? "";

        // Any job whose own abbreviation is a column maps directly.
        foreach (var s in slots)
            if (string.Equals(s, job.Abbreviation, StringComparison.OrdinalIgnoreCase)) return s;

        var prefs = job.Role switch
        {
            JobRole.Tank => new[] { "T1", "MT", "T2", "OT" },
            JobRole.Melee => new[] { "M1", "D1", "M2", "D2" },
            JobRole.PhysicalRanged => new[] { "R1", "D3", "R" },
            JobRole.Caster => new[] { "R2", "D4", "Caster" },
            _ => Array.Empty<string>(),
        };
        foreach (var p in prefs)
            foreach (var s in slots)
                if (string.Equals(s, p, StringComparison.OrdinalIgnoreCase)) return s;
        return "";
    }
}
