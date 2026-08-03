using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Time until a tracked mit is off cooldown.
public static class Cooldowns
{
    // Mit names, matched as substrings against a cell.
    private static readonly string[] Names =
    {
        "Reprisal", "Rampart", "Feint", "Addle", "Bloodbath", "Second Wind", "Arm's Length", "Mantra",
        "Holmgang", "Vengeance", "Damnation", "Thrill of Battle", "Shake It Off", "Bloodwhetting",
        "Nascent Flash", "Raw Intuition", "Equilibrium",
        "Sentinel", "Guardian", "Hallowed Ground", "Bulwark", "Sheltron", "Holy Sheltron",
        "Intervention", "Divine Veil", "Passage of Arms", "Rampart",
        "Shadowed Vigil", "Shadow Wall", "Dark Mind", "Living Dead", "The Blackest Night", "Oblation", "Dark Missionary",
        "Camouflage", "Great Nebula", "Nebula", "Superbolide", "Heart of Light", "Heart of Stone", "Heart of Corundum", "Aurora",
        "Sacred Soil", "Expedient", "Fey Illumination", "Seraph", "Recitation", "Whispering Dawn",
        "Consolation", "Excogitation",
        "Temperance", "Plenary Indulgence", "Asylum", "Liturgy of the Bell", "Divine Caress",
        "Collective Unconscious", "Neutral Sect", "Macrocosmos", "Exaltation", "Sun Sign",
        "Kerachole", "Holos", "Panhaima", "Haima", "Physis II", "Krasis", "Zoe", "Philosophia",
        "Magick Barrier", "Addle", "Tactician", "Troubadour", "Shield Samba", "Improvisation", "Dismantle",
        "Nature's Minne", "Curing Waltz",
        "Tempera Grassa", "Seraphism", "Earthly Star", "Celestial Opposition",
    };

    private static Dictionary<string, uint>? _byName;

    private static void EnsureMap()
    {
        if (_byName != null) return;
        var map = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var want = new HashSet<string>(Names, StringComparer.OrdinalIgnoreCase);
            // Names are English, so read the English sheet.
            var sheet = GameSheets.English<Lumina.Excel.Sheets.Action>();
            var recastOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (sheet != null)
                foreach (var row in sheet)
                {
                    var n = row.Name.ExtractText();
                    // Legacy and PvP rows share names with the real action.
                    if (row.ClassJobLevel == 0 || row.IsPvP) continue;
                    if (string.IsNullOrEmpty(n) || !want.Contains(n)) continue;
                    // The fairy's copies share the name but have no recast.
                    if (map.ContainsKey(n) && recastOf[n] >= row.Recast100ms) continue;
                    map[n] = row.RowId;
                    recastOf[n] = row.Recast100ms;
                }
        }
        catch (Exception ex) { Swallowed.Report("cooldown action map", ex); }
        _byName = map;
    }

    // Action text to every id it names, memoized for per-frame callers.
    private static readonly Dictionary<string, List<uint>> _idsByText = new(StringComparer.Ordinal);

    // Seconds until the soonest mit the text names is ready, or null.
    public static float? Remaining(string? actionText)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(actionText)) return null;
            EnsureMap();
            if (_byName == null || _byName.Count == 0) return null;

            if (!_idsByText.TryGetValue(actionText!, out var ids))
            {
                // Same matching the planner uses.
                ids = new List<uint>();
                foreach (var kv in _byName)
                {
                    if (MentionAt(actionText!, kv.Key) >= 0)
                    {
                        ids.Add(kv.Value);
                    }
                }
                _idsByText[actionText!] = ids;
            }
            if (ids.Count == 0) return null;

            // A cell naming several reads as ready when any one of them is.
            float? min = null;
            foreach (var id in ids)
            {
                var r = RecastRemaining(id);
                if (r.HasValue)
                {
                    if (min == null || r.Value < min.Value) min = r.Value;
                }
            }
            return min;
        }
        catch (Exception ex) { Swallowed.Report("cooldown recast read", ex); return null; }
    }

    // ---- static planning data ----

    // Family is a hand-curated shared-cooldown key.
    public readonly record struct PlanMit(string Name, float Recast, int Charges, string Family, int Level, float Duration);

    private static readonly Dictionary<string, string> SharedFamily = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Raw Intuition"] = "war-heal", ["Bloodwhetting"] = "war-heal", ["Nascent Flash"] = "war-heal",
        ["Vengeance"] = "war-mit", ["Damnation"] = "war-mit",
        ["Sentinel"] = "pld-mit", ["Guardian"] = "pld-mit",
        ["Heart of Stone"] = "gnb-heart", ["Heart of Corundum"] = "gnb-heart",
        ["Sheltron"] = "pld-oath", ["Holy Sheltron"] = "pld-oath",
        ["Shadow Wall"] = "drk-wall", ["Shadowed Vigil"] = "drk-wall",
        ["Nebula"] = "gnb-nebula", ["Great Nebula"] = "gnb-nebula",
    };

    // Buff durations, hand-curated from 7.x.
    private static readonly Dictionary<string, float> Durations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Reprisal"] = 15, ["Feint"] = 15, ["Addle"] = 15, ["Dismantle"] = 10,
        ["Rampart"] = 20, ["Thrill of Battle"] = 10, ["Holmgang"] = 10,
        ["Bloodbath"] = 20, ["Arm's Length"] = 6, ["Equilibrium"] = 15,
        ["Bloodwhetting"] = 8, ["Nascent Flash"] = 8, ["Raw Intuition"] = 6,
        ["Shake It Off"] = 30, ["Vengeance"] = 15, ["Damnation"] = 15,
        ["Sentinel"] = 15, ["Guardian"] = 15, ["Divine Veil"] = 30,
        ["Passage of Arms"] = 18, ["Hallowed Ground"] = 10, ["Bulwark"] = 10,
        ["Sheltron"] = 6, ["Holy Sheltron"] = 8, ["Intervention"] = 8,
        ["Shadow Wall"] = 15, ["Shadowed Vigil"] = 15, ["Dark Mind"] = 10, ["Living Dead"] = 10,
        ["The Blackest Night"] = 7, ["Oblation"] = 10, ["Dark Missionary"] = 15,
        ["Camouflage"] = 20, ["Nebula"] = 15, ["Great Nebula"] = 15, ["Superbolide"] = 10,
        ["Heart of Light"] = 15, ["Heart of Stone"] = 7, ["Heart of Corundum"] = 8, ["Aurora"] = 18,
        ["Sacred Soil"] = 15, ["Expedient"] = 20, ["Fey Illumination"] = 20, ["Whispering Dawn"] = 21,
        ["Temperance"] = 20, ["Plenary Indulgence"] = 10, ["Asylum"] = 24,
        ["Liturgy of the Bell"] = 20, ["Divine Caress"] = 10,
        ["Collective Unconscious"] = 10, ["Neutral Sect"] = 20, ["Macrocosmos"] = 15,
        ["Exaltation"] = 8, ["Sun Sign"] = 15,
        ["Consolation"] = 30, ["Excogitation"] = 45,
        ["Kerachole"] = 15, ["Holos"] = 20, ["Panhaima"] = 15, ["Haima"] = 15, ["Physis II"] = 15,
        ["Krasis"] = 10, ["Philosophia"] = 20,
        ["Magick Barrier"] = 10, ["Tactician"] = 15, ["Troubadour"] = 15,
        ["Shield Samba"] = 15, ["Improvisation"] = 15,
        ["Tempera Grassa"] = 10, ["Seraphism"] = 20,
        ["Earthly Star"] = 20, ["Celestial Opposition"] = 15,
        ["Zoe"] = 45, ["Recitation"] = 45,
    };

    // Traits cut some recasts, which the Action sheet's base value misses.
    private static readonly Dictionary<string, float> RecastOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Zoe"] = 90f,
    };

    // Tracked for recast but windowless for now; Mantra and Minne get real windows once tooltips confirm them.
    public static readonly string[] Windowless = { "Seraph", "Second Wind", "Curing Waltz", "Mantra", "Nature's Minne" };

    // Every tracked mit, once each.
    public static readonly string[] Tracked = Names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    // How long this mit's buff runs, 0 if none.
    public static float WindowOf(string name) => Durations.GetValueOrDefault(name);

    // Tracked mits with more than one charge.
    private static readonly string[] Charged = { "Consolation", "Oblation" };

    public static bool HasCharges(string name)
        => Charged.Contains(name, StringComparer.OrdinalIgnoreCase);

    // The longest buff any tracked mit gives.
    public static readonly float LongestWindow = Durations.Count == 0 ? 0f : Durations.Values.Max();

    private static Dictionary<string, PlanMit>? _planByName;

    private static void EnsurePlanMap()
    {
        if (_planByName != null) return;
        EnsureMap();
        var map = new Dictionary<string, PlanMit>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var sheet = GameSheets.English<Lumina.Excel.Sheets.Action>();
            if (sheet != null && _byName != null)
                foreach (var kv in _byName)
                {
                    var row = sheet.GetRowOrDefault(kv.Value);
                    if (row == null) continue;
                    var recast = RecastOverrides.GetValueOrDefault(kv.Key, row.Value.Recast100ms / 10f);
                    if (recast <= 5f) continue; // GCD-ish rows aren't worth validating
                    map[kv.Key] = new PlanMit(kv.Key, recast,
                        Math.Max(1, (int)row.Value.MaxCharges),
                        SharedFamily.GetValueOrDefault(kv.Key, ""),
                        row.Value.ClassJobLevel,
                        Durations.GetValueOrDefault(kv.Key));
                }
        }
        catch (Exception ex) { Swallowed.Report("cooldown plan map", ex); }
        _planByName = map;
    }

    // Each job's kit, for the Suggest a mit menu.
    public static readonly System.Collections.Generic.Dictionary<string, string[]> JobKits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WAR"] = new[] { "Reprisal", "Rampart", "Shake It Off", "Damnation", "Vengeance", "Bloodwhetting", "Raw Intuition", "Thrill of Battle" },
        ["PLD"] = new[] { "Reprisal", "Rampart", "Divine Veil", "Passage of Arms", "Guardian", "Sentinel", "Holy Sheltron", "Sheltron", "Intervention", "Bulwark" },
        ["DRK"] = new[] { "Reprisal", "Rampart", "Dark Missionary", "Shadowed Vigil", "Shadow Wall", "The Blackest Night", "Oblation", "Dark Mind" },
        ["GNB"] = new[] { "Reprisal", "Rampart", "Heart of Light", "Great Nebula", "Nebula", "Heart of Corundum", "Heart of Stone", "Camouflage", "Aurora" },
        ["WHM"] = new[] { "Temperance", "Asylum", "Plenary Indulgence", "Liturgy of the Bell", "Divine Caress" },
        ["SCH"] = new[] { "Sacred Soil", "Expedient", "Seraphism", "Fey Illumination", "Seraph", "Consolation", "Excogitation", "Whispering Dawn", "Recitation" },
        ["AST"] = new[] { "Collective Unconscious", "Neutral Sect", "Macrocosmos", "Earthly Star", "Celestial Opposition", "Exaltation", "Sun Sign" },
        ["SGE"] = new[] { "Kerachole", "Holos", "Panhaima", "Haima", "Physis II", "Krasis", "Zoe", "Philosophia" },
        ["MNK"] = new[] { "Feint" }, ["DRG"] = new[] { "Feint" }, ["NIN"] = new[] { "Feint" },
        ["SAM"] = new[] { "Feint" }, ["RPR"] = new[] { "Feint" }, ["VPR"] = new[] { "Feint" },
        ["BRD"] = new[] { "Troubadour" },
        ["MCH"] = new[] { "Tactician", "Dismantle" },
        ["DNC"] = new[] { "Shield Samba", "Improvisation" },
        ["BLM"] = new[] { "Addle" }, ["SMN"] = new[] { "Addle" },
        ["PCT"] = new[] { "Addle", "Tempera Grassa" },
        ["RDM"] = new[] { "Addle", "Magick Barrier" },
    };

    // Plan data for one mit by exact name.
    public static PlanMit? PlanInfo(string name)
    {
        EnsurePlanMap();
        return _planByName != null && _planByName.TryGetValue(name, out var pm) ? pm : null;
    }

    // The level a duty syncs to, 0 when unknown.
    public static int DutySyncLevel(uint territory)
    {
        try
        {
            var t = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRowOrDefault(territory);
            var cfc = t?.ContentFinderCondition.ValueNullable;
            return cfc?.ClassJobLevelSync ?? 0;
        }
        catch (Exception ex) { Swallowed.Report("duty sync level", ex); return 0; }
    }

    // PlanMits results per action text, memoized.
    private static readonly Dictionary<string, List<PlanMit>> _planMitsByText = new(StringComparer.Ordinal);
    private static readonly PlanMit[] _noPlanMits = Array.Empty<PlanMit>();

    // Cached PlanMits for per-frame callers.
    public static IReadOnlyList<PlanMit> PlanMitsCached(string? actionText)
    {
        if (string.IsNullOrWhiteSpace(actionText)) return _noPlanMits;
        if (_planMitsByText.TryGetValue(actionText!, out var hit)) return hit;
        var list = new List<PlanMit>(PlanMits(actionText));
        // Never memoize a miss from an unbuilt map.
        if (_planByName is { Count: > 0 }) _planMitsByText[actionText!] = list;
        return list;
    }

    // Every tracked mit named in an action text.
    public static IEnumerable<PlanMit> PlanMits(string? actionText)
    {
        if (string.IsNullOrWhiteSpace(actionText)) yield break;
        EnsurePlanMap();
        if (_planByName == null) yield break;
        foreach (var pm in _planByName.Values)
            if (Mentions(actionText!, pm.Name)) yield return pm;
    }

    // Where the text names that mit, or -1.
    private static int IndexIn(string text, string name)
    {
        var idx = text.IndexOf(name, StringComparison.OrdinalIgnoreCase);
        while (idx >= 0)
        {
            var before = idx == 0 ? ' ' : text[idx - 1];
            var end = idx + name.Length;
            var after = end >= text.Length ? ' ' : text[end];
            if (!char.IsLetter(before) && !char.IsLetter(after)) return idx;
            idx = text.IndexOf(name, idx + 1, StringComparison.OrdinalIgnoreCase);
        }
        return -1;
    }

    // The shorthand the sheets are written in.
    private static readonly Dictionary<string, string[]> Shorthand = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Reprisal"] = new[] { "Rep" },
        ["Sacred Soil"] = new[] { "Soil" },
        ["Kerachole"] = new[] { "Kera" },
        ["Expedient"] = new[] { "Exp", "Exped" },
        ["Fey Illumination"] = new[] { "Fey" },
        ["Collective Unconscious"] = new[] { "CU" },
        ["Temperance"] = new[] { "Temp" },
        ["Divine Caress"] = new[] { "Caress" },
        ["Liturgy of the Bell"] = new[] { "Bell" },
        ["Macrocosmos"] = new[] { "Macro" },
        ["Neutral Sect"] = new[] { "Neutral" },
        ["Sun Sign"] = new[] { "Sun" },
        ["Philosophia"] = new[] { "Sophia" },
        // "Concit" claims only Consolation.
        ["Consolation"] = new[] { "Concit" },
    };

    // A cell can say outright it is not a press.
    private static bool CarriedOver(string part)
        => part.Contains("carry over", StringComparison.OrdinalIgnoreCase);

    // Top-level / and + pieces of a cell.
    private static IEnumerable<string> Parts(string action)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < action.Length; i++)
        {
            if (action[i] == '(') depth++;
            else if (action[i] == ')') { if (depth > 0) depth--; }
            else if (depth == 0 && (action[i] == '/' || action[i] == '+'))
            {
                yield return action[start..i];
                start = i + 1;
            }
        }
        yield return action[start..];
    }

    // Does this text call for that mit?
    private static bool Mentions(string text, string name) => MentionAt(text, name) >= 0;

    // Where it first calls for it, or -1.
    private static int MentionAt(string text, string name)
    {
        Shorthand.TryGetValue(name, out var shorts);
        var offset = 0;
        foreach (var part in Parts(text))
        {
            if (!CarriedOver(part))
            {
                var at = IndexIn(part, name);
                if (shorts != null)
                    foreach (var alt in shorts)
                    {
                        var i = IndexIn(part, alt);
                        if (i >= 0 && (at < 0 || i < at)) at = i;
                    }
                if (at >= 0) return offset + at;
            }
            offset += part.Length + 1;   // the separator this part was split on
        }
        return -1;
    }

    // The real name for a cell that is one mit only.
    public static string? Canonical(string text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return null;
        foreach (var name in Tracked)
        {
            if (string.Equals(name, t, StringComparison.OrdinalIgnoreCase)) return name;
            if (!Shorthand.TryGetValue(name, out var shorts)) continue;
            foreach (var s in shorts)
                if (string.Equals(s, t, StringComparison.OrdinalIgnoreCase)) return name;
        }
        return null;
    }

    // Distinct tracked names with a curated duration.
    private static readonly string[] BuffNames = Names
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Where(n => Durations.ContainsKey(n))
        .ToArray();

    // The mits a text names, with buff durations.
    public static IEnumerable<(string Name, float Duration)> BuffsIn(string? actionText)
    {
        if (string.IsNullOrWhiteSpace(actionText)) yield break;
        foreach (var name in BuffNames)
            if (Mentions(actionText!, name)) yield return (name, Durations[name]);
    }

    private static unsafe float? RecastRemaining(uint id)
    {
        var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if (am == null) return null;
        var adjusted = am->GetAdjustedActionId(id);
        var total = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, adjusted);
        if (total <= 0f) return null; // no recast group / not on your current job
        var elapsed = am->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, adjusted);

        // Charge actions: the recast spans all charges.
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
