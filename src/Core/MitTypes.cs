using System;

namespace FrenMits;

// Classifies a call by the kind of mitigation it is, so the overlay can colour it
// at a glance: party-wide raidbuffs vs tank cooldowns vs personal/other. Matching
// is by keyword on the action text — generous and forgiving, since sheets phrase
// things loosely.
public static class MitTypes
{
    public enum Kind { Party, Tank, Personal, Other }

    // Tank invulns / tank-specific cooldowns and tank-buster prefixes.
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

    // Party-wide raid mitigation (tank/melee/ranged/caster utility + healer party CDs).
    private static readonly string[] PartyWords =
    {
        "feint", "addle", "dismantle", "magick barrier", "tactician", "troubadour",
        "shield samba", "improvisation", "divine veil", "passage of arms", "shake it off",
        "heart of light", "dark missionary", "sacred soil", "kerachole", "holos",
        "expedient", "expedience", "desperate measures", "temperance", "divine caress", "neutral sect", "collective",
        "plenary", "fey illumination", "seraph", "panhaima", "philosophia", "zoe",
        "succor", "medica", "deployment", "kerakeia", "liturgy", "macrocosmos",
        "spreadlo", "party mit", "kitchen sink", "sun sign", "seraphism", "barrier",
        "tempera", "earthly star", "celestial opposition", "asylum", "whispering dawn", "physis",
    };

    private static readonly string[] PersonalWords =
    {
        "second wind", "bloodbath", "personal", "feather", "stem the flow",
    };

    public static Kind Classify(string? action, string? mechanic = null)
    {
        var s = ((action ?? "") + " " + (mechanic ?? "")).ToLowerInvariant();
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

    // The configured colour for a kind, or 0 to fall back to the default overlay
    // colour (for Other, or kinds the user has zeroed out).
    public static uint Color(Kind kind, Configuration c) => kind switch
    {
        Kind.Party => c.MitColorParty,
        Kind.Tank => c.MitColorTank,
        Kind.Personal => c.MitColorPersonal,
        _ => 0u,
    };
}
