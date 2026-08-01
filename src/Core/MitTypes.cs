using System;

namespace FrenMits;

// Sorts a call into party, tank or other, for its color.
public static class MitTypes
{
    public enum Kind { Party, Tank, Personal, Other }

    // Tank cooldowns, invulns and buster prefixes.
    private static readonly string[] TankWords =
    {
        "holmgang", "living dead", "hallowed ground", "superbolide", "rampart",
        "vengeance", "damnation", "bloodwhetting", "nascent flash", "raw intuition",
        "shadow wall", "dark mind", "oblation", "the blackest night", "tbn",
        "sentinel", "guardian", "bulwark", "sheltron", "intervention", "hallowed",
        "blackest night",
        "heart of stone", "heart of corundum", "nebula", "camouflage", "reprisal",
        "provoke", "shirk", "thrill of battle", "equilibrium", "tank:", "invuln",
    };

    // Party-wide raid mitigation and healer party cooldowns.
    private static readonly string[] PartyWords =
    {
        "feint", "addle", "dismantle", "magick barrier", "tactician", "troubadour",
        "shield samba", "improvisation", "divine veil", "passage of arms", "shake it off",
        "heart of light", "dark missionary", "sacred soil", "kerachole", "holos",
        "expedient", "expedience", "desperate measures", "temperance", "divine caress", "neutral sect", "collective",
        "plenary", "fey illumination", "seraph", "panhaima", "philosophia", "zoe",
        "succor", "medica", "deployment", "kerakeia", "liturgy", "macrocosmos",
        "spreadlo", "party mit", "kitchen sink", "sun sign", "seraphism", "barrier",
        "tempera grassa", "earthly star", "celestial opposition",
    };

    private static readonly string[] PersonalWords =
    {
        "second wind", "bloodbath", "personal", "feather", "stem the flow",
    };

    // Memoized, since the answer depends only on the two texts.
    private static readonly System.Collections.Generic.Dictionary<(string Action, string Mech), Kind> _cache = new();

    public static Kind Classify(string? action, string? mechanic = null)
    {
        (string Action, string Mech) key = (action ?? "", mechanic ?? "");
        if (_cache.TryGetValue(key, out var hit)) return hit;
        var kind = ClassifyUncached(key.Action, key.Mech);
        if (_cache.Count > 4096) _cache.Clear(); // free-text mechanic names can't grow it forever
        _cache[key] = kind;
        return kind;
    }

    private static Kind ClassifyUncached(string action, string mechanic)
    {
        var s = (action + " " + mechanic).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(s)) return Kind.Other;
        if (Contains(s, TankWords)) return Kind.Tank;
        if (Contains(s, PartyWords)) return Kind.Party;
        if (Contains(s, PersonalWords)) return Kind.Personal;
        return Kind.Other;
    }

    private static bool Contains(string s, string[] words)
    {
        foreach (var w in words)
            if (s.Contains(w, StringComparison.Ordinal)) return true;
        return false;
    }

    // The configured color for a kind, or 0 for the default.
    public static uint Color(Kind kind, Configuration c) => kind switch
    {
        Kind.Party => c.MitColorParty,
        Kind.Tank => c.MitColorTank,
        Kind.Personal => c.MitColorPersonal,
        _ => 0u,
    };
}
