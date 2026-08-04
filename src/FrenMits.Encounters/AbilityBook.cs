using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Encounters;

// Curated mit data: names, durations, job kits and the shorthand the sheets
// are written in. No game client contact, so it reads the same offline.
public static class AbilityBook
{
    // Mit names, matched as substrings against a cell.
    public static readonly string[] Names =
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

    // Family is a hand-curated shared-cooldown key.
    public readonly record struct PlanMit(string Name, float Recast, int Charges, string Family, int Level, float Duration);

    public static readonly Dictionary<string, string> SharedFamily = new(StringComparer.OrdinalIgnoreCase)
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
    public static readonly Dictionary<string, float> Durations = new(StringComparer.OrdinalIgnoreCase)
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
    public static readonly Dictionary<string, float> RecastOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Zoe"] = 90f,
    };

    // Tracked for recast but windowless for now; Mantra and Minne get real windows once tooltips confirm them.
    public static readonly string[] Windowless = { "Seraph", "Second Wind", "Curing Waltz", "Mantra", "Nature's Minne" };

    public static readonly string[] NoCarryOver =
    {
        "Zoe", "Recitation", "Seraph", "Emergency Tactics", "Pepsis", "Dissipation", "Swiftcast", "Lightspeed"
    };

    public static bool IsNoCarryOver(string name)
        => NoCarryOver.Contains(name, StringComparer.OrdinalIgnoreCase);

    public static readonly HashSet<string> PartyMits = new(StringComparer.OrdinalIgnoreCase)
    {
        "Reprisal", "Feint", "Addle", "Shake It Off", "Divine Veil", "Passage of Arms",
        "Dark Missionary", "Heart of Light", "Sacred Soil", "Expedient", "Fey Illumination",
        "Kerachole", "Panhaima", "Holos", "Trophy", "Collective Unconscious",
        "Celestial Opposition", "Earthly Star", "Macrocosmos"
    };

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

    // ---- cell text matching ----

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
        // The sheets name the buff here, not the button that grants it.
        ["Plenary Indulgence"] = new[] { "Confession" },
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
    public static bool Mentions(string text, string name) => MentionAt(text, name) >= 0;

    // Where it first calls for it, or -1.
    public static int MentionAt(string text, string name)
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
}
