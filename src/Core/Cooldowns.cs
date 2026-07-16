using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Looks up how long until one of your mits is off cooldown, so a call can warn
// when the mit won't be ready in time. Action ids are resolved from the game data
// by NAME (no hard-coded ids), and the recast is read through ActionManager.
// Everything is guarded — if any of it isn't available the call simply returns
// null and the overlay behaves exactly as before.
public static class Cooldowns
{
    // Canonical mit action names (must match the Action sheet). Matched as
    // substrings against a line's action text, so "Rampart + 90s" finds "Rampart".
    private static readonly string[] Names =
    {
        "Reprisal", "Rampart", "Feint", "Addle", "Bloodbath", "Second Wind", "Arm's Length",
        "Holmgang", "Vengeance", "Damnation", "Thrill of Battle", "Shake It Off", "Bloodwhetting",
        "Nascent Flash", "Raw Intuition", "Equilibrium",
        "Sentinel", "Guardian", "Hallowed Ground", "Bulwark", "Sheltron", "Holy Sheltron",
        "Intervention", "Divine Veil", "Passage of Arms", "Rampart",
        "Shadow Wall", "Dark Mind", "Living Dead", "The Blackest Night", "Oblation", "Dark Missionary",
        "Camouflage", "Nebula", "Superbolide", "Heart of Light", "Heart of Stone", "Heart of Corundum", "Aurora",
        "Sacred Soil", "Expedient", "Fey Illumination", "Seraph", "Recitation", "Whispering Dawn",
        "Temperance", "Plenary Indulgence", "Asylum", "Liturgy of the Bell", "Divine Caress",
        "Collective Unconscious", "Neutral Sect", "Macrocosmos", "Exaltation", "Sun Sign",
        "Kerachole", "Holos", "Panhaima", "Physis II", "Krasis", "Zoe", "Philosophia",
        "Magick Barrier", "Addle", "Tactician", "Troubadour", "Shield Samba", "Improvisation", "Dismantle",
    };

    private static Dictionary<string, uint>? _byName;

    private static void EnsureMap()
    {
        if (_byName != null) return;
        var map = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var want = new HashSet<string>(Names, StringComparer.OrdinalIgnoreCase);
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet != null)
                foreach (var row in sheet)
                {
                    var n = row.Name.ExtractText();
                    // Legacy and PvP duplicate rows share names with the real
                    // action; the real one has a job level and is not PvP.
                    if (row.ClassJobLevel == 0 || row.IsPvP) continue;
                    if (!string.IsNullOrEmpty(n) && want.Contains(n) && !map.ContainsKey(n))
                        map[n] = row.RowId;
                }
        }
        catch { }
        _byName = map;
    }

    // Seconds until the mit referenced by `actionText` is ready, or null if it
    // isn't a tracked mit / can't be read. 0 = ready now.
    public static float? Remaining(string? actionText)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(actionText)) return null;
            EnsureMap();
            if (_byName == null || _byName.Count == 0) return null;

            uint id = 0;
            foreach (var kv in _byName)
                if (actionText!.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)) { id = kv.Value; break; }
            if (id == 0) return null;

            return RecastRemaining(id);
        }
        catch { return null; }
    }

    // ---- static planning data (from the game sheets, no combat needed) ----

    // Family: hand-curated shared-cooldown family key ("" = its own timer).
    // NOT the Action sheet's CooldownGroup: those numbers are per-actor slots
    // reused across jobs (Temperance and Panhaima collide), so they can never
    // be used to pool timers. Level: when the job learns it, for level-sync
    // warnings. Duration: buff uptime in seconds (0 = unknown).
    public readonly record struct PlanMit(string Name, float Recast, int Charges, string Family, int Level, float Duration);

    private static readonly Dictionary<string, string> SharedFamily = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Raw Intuition"] = "war-heal", ["Bloodwhetting"] = "war-heal", ["Nascent Flash"] = "war-heal",
        ["Vengeance"] = "war-mit", ["Damnation"] = "war-mit",
        ["Sentinel"] = "pld-mit", ["Guardian"] = "pld-mit",
        ["Heart of Stone"] = "gnb-heart", ["Heart of Corundum"] = "gnb-heart",
        ["Sheltron"] = "pld-oath", ["Holy Sheltron"] = "pld-oath",
    };

    // Buff durations, hand-curated (7.x values): the game sheets don't expose
    // status uptime cleanly. 0 / absent = no window math for that mit. Values
    // are the MITIGATION uptime, not trailing regen ticks.
    private static readonly Dictionary<string, float> Durations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Reprisal"] = 15, ["Feint"] = 15, ["Addle"] = 15, ["Dismantle"] = 10,
        ["Rampart"] = 20, ["Thrill of Battle"] = 10, ["Holmgang"] = 10,
        ["Bloodwhetting"] = 8, ["Nascent Flash"] = 8, ["Raw Intuition"] = 6,
        ["Shake It Off"] = 30, ["Vengeance"] = 15, ["Damnation"] = 15,
        ["Sentinel"] = 15, ["Guardian"] = 15, ["Divine Veil"] = 30,
        ["Passage of Arms"] = 18, ["Hallowed Ground"] = 10, ["Bulwark"] = 10,
        ["Sheltron"] = 8, ["Holy Sheltron"] = 8, ["Intervention"] = 8,
        ["Shadow Wall"] = 15, ["Dark Mind"] = 10, ["Living Dead"] = 10,
        ["The Blackest Night"] = 7, ["Oblation"] = 10, ["Dark Missionary"] = 15,
        ["Camouflage"] = 20, ["Nebula"] = 15, ["Superbolide"] = 10,
        ["Heart of Light"] = 15, ["Heart of Stone"] = 7, ["Heart of Corundum"] = 8, ["Aurora"] = 18,
        ["Sacred Soil"] = 15, ["Expedient"] = 20, ["Fey Illumination"] = 20, ["Whispering Dawn"] = 21,
        ["Temperance"] = 20, ["Plenary Indulgence"] = 10, ["Asylum"] = 24,
        ["Liturgy of the Bell"] = 20, ["Divine Caress"] = 10,
        ["Collective Unconscious"] = 18, ["Neutral Sect"] = 20, ["Macrocosmos"] = 15,
        ["Exaltation"] = 8, ["Sun Sign"] = 15,
        ["Kerachole"] = 15, ["Holos"] = 20, ["Panhaima"] = 15, ["Physis II"] = 15,
        ["Krasis"] = 10, ["Philosophia"] = 20,
        ["Magick Barrier"] = 10, ["Tactician"] = 15, ["Troubadour"] = 15,
        ["Shield Samba"] = 15, ["Improvisation"] = 15,
    };

    private static Dictionary<string, PlanMit>? _planByName;

    private static void EnsurePlanMap()
    {
        if (_planByName != null) return;
        EnsureMap();
        var map = new Dictionary<string, PlanMit>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet != null && _byName != null)
                foreach (var kv in _byName)
                {
                    var row = sheet.GetRowOrDefault(kv.Value);
                    if (row == null) continue;
                    var recast = row.Value.Recast100ms / 10f;
                    if (recast <= 5f) continue; // GCD-ish rows aren't worth validating
                    map[kv.Key] = new PlanMit(kv.Key, recast,
                        Math.Max(1, (int)row.Value.MaxCharges),
                        SharedFamily.GetValueOrDefault(kv.Key, ""),
                        row.Value.ClassJobLevel,
                        Durations.GetValueOrDefault(kv.Key));
                }
        }
        catch { }
        _planByName = map;
    }

    // Each job's mitigation kit (tracked names only), for the custom-sheet
    // "Suggest a mit" menu. Upgrade forms first, so the menu can show the
    // highest level-legal member of each shared-cooldown family.
    public static readonly System.Collections.Generic.Dictionary<string, string[]> JobKits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WAR"] = new[] { "Reprisal", "Rampart", "Shake It Off", "Damnation", "Vengeance", "Bloodwhetting", "Raw Intuition", "Thrill of Battle" },
        ["PLD"] = new[] { "Reprisal", "Rampart", "Divine Veil", "Passage of Arms", "Guardian", "Sentinel", "Holy Sheltron", "Sheltron", "Intervention", "Bulwark" },
        ["DRK"] = new[] { "Reprisal", "Rampart", "Dark Missionary", "Shadow Wall", "The Blackest Night", "Oblation", "Dark Mind" },
        ["GNB"] = new[] { "Reprisal", "Rampart", "Heart of Light", "Nebula", "Heart of Corundum", "Heart of Stone", "Camouflage", "Aurora" },
        ["WHM"] = new[] { "Temperance", "Asylum", "Plenary Indulgence", "Liturgy of the Bell", "Divine Caress" },
        ["SCH"] = new[] { "Sacred Soil", "Expedient", "Fey Illumination", "Seraph", "Whispering Dawn", "Recitation" },
        ["AST"] = new[] { "Collective Unconscious", "Neutral Sect", "Macrocosmos", "Exaltation", "Sun Sign" },
        ["SGE"] = new[] { "Kerachole", "Holos", "Panhaima", "Physis II", "Krasis", "Zoe", "Philosophia" },
        ["MNK"] = new[] { "Feint" }, ["DRG"] = new[] { "Feint" }, ["NIN"] = new[] { "Feint" },
        ["SAM"] = new[] { "Feint" }, ["RPR"] = new[] { "Feint" }, ["VPR"] = new[] { "Feint" },
        ["BRD"] = new[] { "Troubadour" },
        ["MCH"] = new[] { "Tactician", "Dismantle" },
        ["DNC"] = new[] { "Shield Samba", "Improvisation" },
        ["BLM"] = new[] { "Addle" }, ["SMN"] = new[] { "Addle" }, ["PCT"] = new[] { "Addle" },
        ["RDM"] = new[] { "Addle", "Magick Barrier" },
    };

    // Static plan data for one tracked mit by exact name, or null.
    public static PlanMit? PlanInfo(string name)
    {
        EnsurePlanMap();
        return _planByName != null && _planByName.TryGetValue(name, out var pm) ? pm : null;
    }

    // The level a duty syncs players to, or 0 when unknown (no warnings then).
    public static int DutySyncLevel(uint territory)
    {
        try
        {
            var t = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRowOrDefault(territory);
            var cfc = t?.ContentFinderCondition.ValueNullable;
            return cfc?.ClassJobLevelSync ?? 0;
        }
        catch { return 0; }
    }

    // Every tracked mit referenced in an action text ("Sacred Soil + Spreadlo"
    // yields Sacred Soil), with its full recast and charge count from the game
    // sheets. Word-boundary matched, so "Seraphism" is not read as "Seraph".
    public static IEnumerable<PlanMit> PlanMits(string? actionText)
    {
        if (string.IsNullOrWhiteSpace(actionText)) yield break;
        EnsurePlanMap();
        if (_planByName == null) yield break;
        foreach (var pm in _planByName.Values)
        {
            // Check every occurrence: "Seraphism + Seraph" must still find the
            // standalone Seraph even though the first occurrence fails the
            // boundary test inside "Seraphism".
            var idx = actionText!.IndexOf(pm.Name, StringComparison.OrdinalIgnoreCase);
            while (idx >= 0)
            {
                var before = idx == 0 ? ' ' : actionText[idx - 1];
                var end = idx + pm.Name.Length;
                var after = end >= actionText.Length ? ' ' : actionText[end];
                if (!char.IsLetter(before) && !char.IsLetter(after))
                {
                    yield return pm;
                    break;
                }
                idx = actionText.IndexOf(pm.Name, idx + 1, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static unsafe float? RecastRemaining(uint id)
    {
        var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if (am == null) return null;
        var adjusted = am->GetAdjustedActionId(id);
        var total = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, adjusted);
        if (total <= 0f) return 0f; // no recast group / not on your current job
        var elapsed = am->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, adjusted);

        // Charge actions (Aurora, Oblation): the recast timer spans ALL charges,
        // so total - elapsed reads "on cooldown" even while a charge sits ready.
        // A charge is available once elapsed covers one per-charge span.
        var maxCharges = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetMaxCharges(adjusted, 0);
        if (maxCharges > 1)
        {
            var perCharge = total / maxCharges;
            if (perCharge > 0f && elapsed >= perCharge) return 0f;      // a charge is up
            return MathF.Max(0f, perCharge - elapsed);                  // time to first charge
        }

        return MathF.Max(0f, total - elapsed);
    }
}
