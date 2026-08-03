using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Registry of fights that ship with baked timelines.
public static class Builtin
{
    public const ushort DmuTerritory = 1363;
    public const ushort FruTerritory = 1238;
    // Dawntrail extremes, from log-built sheets.
    public const ushort DoomtrainTerritory = 1308;
    public const ushort EnuoTerritory = 1362;
    // Zelenia, built from twenty logged kills and Auto-planned.
    public const ushort ZeleniaTerritory = 1271;
    // The first Dawntrail savage tier, twelve logged kills each.
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
    // The legacy ultimates, timed from Ikuya's sheets.
    public const ushort UcobTerritory = 733;
    public const ushort UwuTerritory = 777;
    public const ushort TeaTerritory = 887;
    public const ushort DsrTerritory = 968;
    public const ushort TopTerritory = 1122;
    
    public const float M12sPhase2Offset = 420f;

    // Newest expansion first, release order inside it.
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

    public static bool Has(uint territory) => LoadJsonDef(territory) != null;

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

    // Built-ins all present the one standard column set.
    public static string[] Slots(uint territory) => SlotNames.Standard;

    // Canonical cross-fight roles for the global role picker.
    public static readonly string[] Roles =
        { "Main Tank", "Off Tank", "Healer 1", "Healer 2", "Melee 1", "Melee 2", "Phys Ranged", "Caster" };

    // Healer roles carry a seat-group fallback for bare H1/H2.
    static readonly Dictionary<string, string[]> RoleSlotCodes = new()
    {
        ["Main Tank"] = new[] { "MT", "T1" },
        ["Off Tank"] = new[] { "OT", "T2" },
        ["Healer 1"] = new[] { "H1", "WHM", "AST" },
        ["Healer 2"] = new[] { "H2", "SCH", "SGE" },
        ["Melee 1"] = new[] { "M1", "D1" },
        ["Melee 2"] = new[] { "M2", "D2" },
        ["Phys Ranged"] = new[] { "R1", "D3", "R" },
        ["Caster"] = new[] { "R2", "D4", "Caster" }
    };

    // The slot a fight uses for a role, or null if it has none.
    public static string? RoleSlot(uint territory, string role)
        => RoleSlotIn(Slots(territory), role);

    // Same, against any sheet's own column list.
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

    private static List<(string Name, float Time)> RawPhaseStarts(uint territory)
    {
        var def = LoadJsonDef(territory);
        if (def == null) return new();
        return def.PhaseStarts.Select(p => (p.Name, p.Time)).ToList();
    }

    // Windows where a Tank-kind MT/OT line means priority 1 / priority 2
    // instead of literal enmity. See PriorityPhase and TankPriority.
    public static IReadOnlyList<PriorityPhase> PriorityPhases(uint territory)
        => (IReadOnlyList<PriorityPhase>?)LoadJsonDef(territory)?.PriorityPhases ?? Array.Empty<PriorityPhase>();

    // The sheet's per-phase notes footer, shown in Sheet View.
    public static string PhaseNotes(uint territory, string phase) => territory switch
    {
        DmuTerritory => DmuPhaseNotes(phase),
        _ => "",
    };

    // Long display title for a phase key ("P1" -> "Phase 1: Kefka").
    public static string PhaseTitle(uint territory, string phase) => territory switch
    {
        DmuTerritory => phase switch
        {
            "P1" => "Phase 1: Kefka",
            "P2" => "Phase 2: Forsaken Kefka",
            "P3" => "Phase 3: Chaos & Exdeath",
            "P4" => "Phase 4: Kefka Says",
            "P5" => "Phase 5: Ultima Kefka",
            _ => phase,
        },
        _ => phase,
    };

    private static string DmuPhaseNotes(string phase) => phase switch
    {
        "P1" => "All mechanics require shields!\n"
            + "Mitigation for the first Mystery Magic should carry over till the first Double-Trouble Trap unless there is a different usage timing below. "
            + "Targeted mitigation does not work on Wave Cannon, but does apply to Double-Trouble Trap.\n"
            + "Use mitigation for Light of Judgement late into the castbar so it will cover Hyperdrive.\n"
            + "\n"
            + "1) Use your 90s party mitigation as Kefka re-centers to cast the first Graven Image (WAR/PLD can use after Revolting Ruin III finishes).\n"
            + "2) Use your 30s mitigation for the first Mystery Magic after the Graven Image castbar.\n"
            + "3) You can alternatively use Bell just before the first set of puddles which will provide an immediate heal when the second set of puddles occurs, as the Bell will expire shortly after.\n"
            + "4) If you plan to use Dissipation in your opener, use it before Aetherflow. If you use the first Spreadlo earlier, you will get it back for the Double-Trouble Trap in the second Graven Image and be able to use Seraphism earlier/later.",

        "P2" => "All mechanics require shields!\n"
            + "\n"
            + "1) Provide single target mitigation and GCD shield both tanks in the phase transition for Ultimate Embrace. Also assist tanks with the last Ultimate Embrace.\n"
            + "2) Prepare Spreadlo either on the OT shortly beforehand or the MT during Ultimate Embrace to assist the tanks.\n"
            + "3) Use Holos during the first Ultimate Embrace so it is back for Light of Judgement and provides mitigation to the tanks. Alternatively, you can use Holos for the Wings of Destruction + Ultimate Embrace.\n"
            + "4) Use early to avoid shaking off mitigation if playing WAR.",

        "P3" => "All mechanics require shields!\n"
            + "Targeted mitigation must be on your firewalled target unless the firewall is down. For the most part, most targeted mitigation is mostly filler and does not work on raidwides. It is mainly used for minimizing tank autos and/or busters.\n"
            + "Both tanks will get attacked for moderately high damage throughout the entire phase, ensure you are rolling mitigation and heals on them.\n"
            + "\n"
            + "1) At the beginning of the phase, use 30s mitigation after (when the textbox disappears) Kefka says, \"Oh! What other toys can I throw in here...\" to get tank autos and the raidwide + an additional usage for Stray Flames/Tsunami.\n"
            + "2) There is a very small period where you can cover both hits of Thunder III and the next Stray Flames/Tsunami; if you miss the timing, you can use it next GCD.\n"
            + "3) Use if holding Chaos, otherwise use at the beginning of P4 for autos.\n"
            + "\n"
            + "4) Non-healers should avoid using any healing abilities that may cause their Accretion to pop early such as Second Wind, Curing Waltz or Divine Veil. If both Accretions are activated in a short amount of time, it will cause a wipe.\n"
            + "Healers will need to manage HP burst accordingly to ensure that Accretions are not popped together. The H1 and H2 can throw single target heals at whoever has the Accretion between them.\n"
            + "If playing AST, ensure the vulnerability has expired before popping Macrocosmos. WHM can use Benediction (if not used earlier) to instantly pop the healer Accretion.\n"
            + "\n"
            + "5) If you are holding Exdeath instead of Chaos at the beginning, use Reprisal on both before The Decisive Battle finishes.\n"
            + "6) Use LB3 at the W of Vacuum Wave. Either tank can press it, discuss beforehand.\n"
            + "7) Seraphism can be shifted to P4 if you feel you have sufficient mitigation.\n"
            + "8) Prepare Spreadlo on the tanks, prioritizing WAR > DRK > GNB/PLD.\n"
            + "9) Prepare immediately after Bowels of Agony.",

        "P4" => "All mechanics require shields!\n"
            + "Targeted mitigation (Reprisal, Addle, etc) only works on Ultima Upsurge; the rest is used to assist in mitigating tank auto attacks.\n"
            + "\n"
            + "1) Use at the beginning of the phase for autos.",

        "P5" => "All mechanics require shields!\n"
            + "For Forsaken, use any timed mitigation as late as possible unless otherwise noted.\n"
            + "\n"
            + "1) Use when Kefka brings his staff down to his right side (the sheet links a video example). The subsequent usages should be pressed immediately off cooldown.\n"
            + "2) Healers should monitor the tanks during Maddening Orchestra (especially the Flare tank) and Fell Forces. For WAR/DRK, you will need to have single target burst healing prepared after their invulnerability expires so they can survive the 3rd auto.\n"
            + "3) Use two GCDs after the Stray Apocalypse castbar is completed so it is back for Forsaken.\n"
            + "4) Use during the Celestriad castbar.\n"
            + "5) Use after the third towers in Celestriad resolves.",

        _ => "",
    };

    public static List<MitLine> BuildLines(uint territory, string slot)
    {
        var lines = Bake(territory, slot);
        PlanStore.SplitLineList(lines);
        CoveredRepeats.Strip(lines);
        // In time order, because a data file need not be.
        return lines.OrderBy(l => l.Time).ToList();
    }

    private static readonly Dictionary<(uint Territory, string Slot), List<MitLine>> _bakeCache = new();

    // One shared bake, for readers that never touch the lines.
    public static IReadOnlyList<MitLine> BakedLines(uint territory, string slot)
    {
        var key = (territory, slot);
        lock (_bakeCache)
        {
            if (!_bakeCache.TryGetValue(key, out var lines))
                _bakeCache[key] = lines = BuildLines(territory, slot);
            return lines;
        }
    }

    // BakedLines, run through this player's live priority-phase pick - the
    // "what does the sheet say for me right now" baseline that override
    // detection (LineTable, DefaultLineFor) must diff against. Comparing
    // against the literal per-slot bake instead flags a borrowed priority
    // pick as a user override, since its text differs from this slot's own
    // column - the row shows a false "reset" affordance, and clicking it
    // would overwrite the correct borrowed pick with the wrong-slot default.
    public static List<MitLine> BakedLinesForFight(FightProfile fight, string slot, bool includeDeleted = false)
        => TankPriority.Apply(fight, slot, BakedLines(fight.TerritoryId, slot).ToList(), includeDeleted);

    // Same, but for the Sheet View grid's passive tank column - see
    // TankPriority.ApplyGrid. `slot` must not be fight.Slot.
    public static List<MitLine> BakedLinesForGrid(FightProfile fight, string slot, bool includeDeleted = false)
        => TankPriority.ApplyGrid(fight, fight.Slot, slot, BakedLines(fight.TerritoryId, slot).ToList(), includeDeleted);

    private static readonly Dictionary<uint, HashSet<string>> _hiddenCache = new();

    // Mechanic names the sheet marks as personal timers rather than boss casts
    // (see MechanicAction.Hidden). Keyed by name, not carried on the line: a
    // plan saved before the flag existed has no Hidden of its own, so the sheet
    // stays the one authority and old saves resolve the same as new ones.
    public static bool IsHiddenMechanic(uint territory, string mechanic)
    {
        if (string.IsNullOrWhiteSpace(mechanic)) return false;
        HashSet<string>? names;
        lock (_hiddenCache)
        {
            if (!_hiddenCache.TryGetValue(territory, out names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var d = LoadJsonDef(territory);
                if (d != null)
                    foreach (var a in d.DefaultActions)
                        if (a.Hidden && !string.IsNullOrWhiteSpace(a.Mechanic))
                            names.Add(a.Mechanic.Trim());
                _hiddenCache[territory] = names;
            }
        }
        return names.Count > 0 && names.Contains(mechanic.Trim());
    }

    // The raw sheet behind a built-in, for callers that need the whole thing.
    public static FightDefinition? Definition(uint territory) => LoadJsonDef(territory);

    private static readonly Dictionary<uint, FightDefinition> _jsonCache = new();

    // Parse every sheet off the game's thread, so the first read doesn't pay for it.
    public static void WarmSheets()
        => System.Threading.Tasks.Task.Run(() =>
        {
            try { foreach (var f in Fights) LoadJsonDef(f.Territory); }
            catch (Exception ex) { Swallowed.Report("sheet warm", ex); }
        });

    // Locked: readers can race the first load (worker threads, parallel tests).
    private static FightDefinition? LoadJsonDef(uint territory)
    {
        lock (_jsonCache)
        {
            if (_jsonCache.TryGetValue(territory, out var cached)) return cached;
            var f = Fights.FirstOrDefault(x => x.Territory == territory);
            if (f.Territory == 0) return null;
            var safeName = string.Join("_", f.Name.Split(System.IO.Path.GetInvalidFileNameChars())).Replace(" ", "");
            // Outside Dalamud (the test host) the sheets sit next to this assembly.
            var dir = Service.PluginInterface?.AssemblyLocation.DirectoryName
                      ?? System.IO.Path.GetDirectoryName(typeof(Builtin).Assembly.Location)!;
            var path = System.IO.Path.Combine(dir, "Sheets", $"{safeName}.json");
            if (!System.IO.File.Exists(path)) return null;
            var json = System.IO.File.ReadAllText(path);
            var def = System.Text.Json.JsonSerializer.Deserialize<FightDefinition>(json);
            if (def == null) return null;
            // Sheets aren't guaranteed to be sorted on disk.
            def.Timeline.Sort((a, b) => a.Time.CompareTo(b.Time));
            _jsonCache[territory] = def;
            return def;
        }
    }

    private static List<MitLine> Bake(uint territory, string slot)
    {
        var def = LoadJsonDef(territory);
        if (def == null) return new List<MitLine>();
        var lines = new List<MitLine>();
        foreach (var a in def.DefaultActions)
        {
            // A blank Slot is a job-only entry (e.g. a job-extra timer): it
            // applies to whoever plays that job, regardless of which party
            // position they're viewing, so it isn't filtered by slot here -
            // AppliesTo(jobAbbr) does the actual gating at render time.
            // Canon on both sides, so saved fights carrying old column names still bake.
            if (a.Slot.Length == 0 || string.Equals(SlotNames.Canon(a.Slot), SlotNames.Canon(slot), StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(new MitLine { Time = a.Time, Mechanic = a.Mechanic, Action = a.Action, Jobs = a.Jobs, IsJobExtra = a.Slot.Length == 0 && a.Jobs.Count > 0 });
            }
        }
        
        return lines;
    }

    public static List<SyncPoint> SyncPoints(uint territory)
    {
        var def = LoadJsonDef(territory);
        if (def == null) return new();
        return Dedupe(def.SyncPoints);
    }

    // A sheet can carry two rows for one cast.
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

    // Only overwrites graded rows, so custom sheets survive.
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
            // A copy, so folding and callers never write into the cached sheet.
            var copy = new CustomRow { Time = r.Time, Mechanic = r.Mechanic, Hurt = r.Hurt, Buster = r.Buster, Enrage = r.Enrage };
            at[(r.Time, r.Mechanic)] = copy;
            folded.Add(copy);
        }
        return folded;
    }

    private static List<CustomRow> RawCustomRows(uint territory)
    {
        var def = LoadJsonDef(territory);
        if (def == null) return new();
        return def.CustomRows;
    }

    public static List<BossAnchor> BossAnchors(uint territory)
    {
        var def = LoadJsonDef(territory);
        // A copy, so a fight's own edits never write into the cached sheet.
        return def == null ? new() : new List<BossAnchor>(def.BossAnchors);
    }

    // Two baked lines match when they share a time and mechanic.
    public static bool SameCall(MitLine a, MitLine b)
        => MathF.Abs(a.Time - b.Time) < 0.75f
           && string.Equals(a.Mechanic.Trim(), b.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase);

    // A tombstone suppresses a baked line matching within a window.
    public static bool MatchesTombstone(DeletedCall d, string slot, MitLine baked)
        => string.Equals(d.Slot, slot, StringComparison.OrdinalIgnoreCase)
           && MathF.Abs(d.Time - baked.Time) < 6f
           && (!string.IsNullOrWhiteSpace(d.Action) || !string.IsNullOrWhiteSpace(baked.Action)
               ? string.Equals(d.Action.Trim(), baked.Action.Trim(), StringComparison.OrdinalIgnoreCase)
               : string.Equals(d.Mechanic.Trim(), baked.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsDeleted(FightProfile fight, string slot, MitLine baked)
        => fight.DeletedCalls.Any(d => MatchesTombstone(d, slot, baked));

    // Tombstone the original before an edit mutates the line.
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
        line.Personal = true; // Edits from the Fights view are personal overrides.
    }

    // Reconcile a fight's lines with the baked sheet, optionally adding missing calls.
    public static void UpdateLines(FightProfile fight, string slot, bool topUp = true)
    {
        bool SameCall(MitLine a, MitLine b)
            => MathF.Abs(a.Time - b.Time) < 0.1f && string.Equals(a.Mechanic.Trim(), b.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase);

        // A fresh bake never includes calls deleted from this slot.
        List<MitLine> Bake(string s)
            => BuildLines(fight.TerritoryId, s).Where(b => !IsDeleted(fight, s, b)).ToList();

        if (string.IsNullOrEmpty(fight.Slot))
        {
            // First use or an old profile: adopt this slot, keep lines.
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
            // The bake minus deletions, so what the slot is entitled to.
            var live = baked.Where(b => !IsDeleted(fight, slot, b)).ToList();
            foreach (var b in live)
                if (!fight.Lines.Any(l => SameCall(l, b)))
                {
                    fight.Lines.Add(b);
                    added++;
                }

            // Drop a line shadowing a baked call, since mits don't repeat.
            fight.Lines.RemoveAll(l =>
                !string.IsNullOrWhiteSpace(l.Action)
                && !live.Any(b => SameCall(l, b))
                && live.Any(b => MathF.Abs(b.Time - l.Time) < 6f
                                 && string.Equals(b.Action.Trim(), l.Action.Trim(),
                                                  StringComparison.OrdinalIgnoreCase)));

            // Drop tombstones for calls the sheet no longer bakes.
            fight.DeletedCalls.RemoveAll(d =>
                string.Equals(d.Slot, slot, StringComparison.OrdinalIgnoreCase)
                && !baked.Any(b => MatchesTombstone(d, slot, b)));
        }

        fight.Lines = fight.Lines.OrderBy(l => l.Time).ToList();
    }

    // Make this the active slot and load only its mits.
    public static int ApplySlot(FightProfile fight, string slot)
    {
        if (string.IsNullOrEmpty(slot))
            slot = Slots(fight.TerritoryId).FirstOrDefault() ?? "";

        var topUp = true;

        // A fresh bake never includes calls deleted from this slot, and
        // borrows the other column's Tank-kind lines during a PriorityPhase
        // when the live party's job ranking (or a manual swap) says so.
        List<MitLine> Bake(string s)
            => TankPriority.Apply(fight, s, BuildLines(fight.TerritoryId, s).Where(b => !IsDeleted(fight, s, b)).ToList());

        if (string.IsNullOrEmpty(fight.Slot))
        {
            // First use or an old profile: adopt this slot, keep lines.
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
            // Deleted or not, so a tombstone on a borrowed priority line still
            // sees the call it's suppressing and doesn't prune itself away.
            var baked = TankPriority.Apply(fight, slot, BuildLines(fight.TerritoryId, slot), includeDeleted: true);
            // The bake minus deletions, so what the slot is entitled to.
            var live = Bake(slot);
            foreach (var b in live)
                if (!fight.Lines.Any(l => SameCall(l, b)))
                {
                    fight.Lines.Add(b);
                    added++;
                }

            // Drop a line shadowing a baked call, since mits don't repeat.
            fight.Lines.RemoveAll(l =>
                !string.IsNullOrWhiteSpace(l.Action)
                && !live.Any(b => SameCall(l, b))
                && live.Any(b => MathF.Abs(b.Time - l.Time) < 6f
                                 && string.Equals(b.Action.Trim(), l.Action.Trim(),
                                                  StringComparison.OrdinalIgnoreCase)));

            // Drop tombstones for calls the sheet no longer bakes.
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

    // Discard this slot's edits and reload it from the sheet.
    public static void ResetSlot(FightProfile fight, string slot)
    {
        fight.DeletedCalls.RemoveAll(d => string.Equals(d.Slot, slot, StringComparison.OrdinalIgnoreCase));
        fight.Slot = slot;
        fight.Lines = TankPriority.Apply(fight, slot, BuildLines(fight.TerritoryId, slot));
        fight.SavedSlots[slot] = fight.Lines;
        fight.SyncPoints = SyncPoints(fight.TerritoryId);
        fight.BossAnchors = BossAnchors(fight.TerritoryId);
        ApplyCustomRows(fight);
        fight.AutoLoaded = true;
    }

    // Re-resolve a fight's priority-phase tank lines against the current
    // party/manual-swap state, dropping only the auto-resolved (non-Custom)
    // ones so a re-pick can bring in the correct column; ApplySlot's top-up
    // then re-adds whatever the fresh resolution calls for.
    public static void ReapplyPriority(FightProfile fight)
    {
        if (string.IsNullOrEmpty(fight.Slot)) return;
        var phases = PriorityPhases(fight.TerritoryId);
        if (phases.Count == 0) return;
        fight.Lines.RemoveAll(l => !l.Custom
            && phases.Any(p => l.Time >= p.Start && l.Time < p.End)
            && MitTypes.Classify(l.Action, l.Mechanic) != MitTypes.Kind.Party);
        ApplySlot(fight, fight.Slot);
    }

    // Best-guess slot for a job, for the first auto-load.
    public static string DefaultSlotForJob(uint territory, string? jobAbbr, Configuration? config = null)
    {
        var slots = Slots(territory);
        if (slots.Length == 0) return "";
        var hit = DefaultSlotForJobIn(slots, jobAbbr, config);
        return hit.Length > 0 ? hit : slots[0];
    }

    // Same guess with no fallback, so "" means ask.
    public static string DefaultSlotForJobIn(IReadOnlyList<string> slots, string? jobAbbr, Configuration? config = null)
    {
        if (slots.Count == 0 || Jobs.ByAbbreviation(jobAbbr) is not { } job) return "";

        // Apply specific job preferences first! (e.g. PCT -> D4, RDM -> M2)
        if (config != null && config.JobSlotPreferences.TryGetValue(job.Abbreviation, out var jobPref))
        {
            foreach (var s in slots)
                if (string.Equals(s, jobPref, StringComparison.OrdinalIgnoreCase)) return s;
        }

        // A chosen role preference outranks the built-in seat guess below.
        if (config != null && config.GlobalRolePreferences.TryGetValue(job.Role, out var rolePref))
        {
            foreach (var s in slots)
                if (string.Equals(s, rolePref, StringComparison.OrdinalIgnoreCase)) return s;
        }

        // Healers map to their own column (or their H1/H2 seat group).
        if (job.Role == JobRole.Healer)
        {
            foreach (var kvp in RoleSlotCodes)
            {
                if (kvp.Value.Contains(job.Abbreviation, StringComparer.OrdinalIgnoreCase))
                {
                    var slot = RoleSlotIn(slots, kvp.Key);
                    if (!string.IsNullOrEmpty(slot)) return slot;
                }
            }
        }

        // Any job whose own abbreviation is a column maps directly.
        foreach (var s in slots)
            if (string.Equals(s, job.Abbreviation, StringComparison.OrdinalIgnoreCase)) return s;

        var prefs = job.Role switch
        {
            JobRole.Tank => new[] { "MT", "T1", "OT", "T2" },
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
