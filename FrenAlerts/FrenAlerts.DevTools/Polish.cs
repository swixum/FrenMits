using System.Text.RegularExpressions;

namespace FrenAlerts.DevTools;

public static partial class Polish
{
    [GeneratedRegex(@"\s*\+\s*")]
    private static partial Regex Plus();

    [GeneratedRegex(@"\bYOU\b")]
    private static partial Regex Shouted();

    // Punctuation left stranded where something was removed from in front of it.
    [GeneratedRegex(@"\s+([,:])")]
    private static partial Regex Orphaned();

    [GeneratedRegex(@"[,:]{2,}")]
    private static partial Regex Doubled();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Runs();

    private static readonly HashSet<string> Plain = new(StringComparer.OrdinalIgnoreCase)
    {
        "left", "right", "north", "south", "east", "west", "in", "out", "on", "of",
        "the", "a", "and", "or", "to", "at", "with", "your", "you", "from",
        "stop", "moving", "move", "middle", "sides", "side", "front", "back",
        "under", "over", "behind", "stack", "stacks", "spread", "towers", "tower",
        "bait", "swap", "knockback", "safe", "spot", "spots", "groups", "group",
        "light", "party", "parties", "healer", "healers", "near", "far", "close",
        "cardinals", "intercards", "corners", "buster", "raidwide", "get", "go",
        "dodge", "cleanse", "share", "partner", "melee", "range", "hits", "times",
    };

    public static string Finish(string text)
    {
        var s = Plus().Replace(text, " and ");
        s = Shouted().Replace(s, "you");
        s = Runs().Replace(s, " ");
        s = Orphaned().Replace(s, "$1");
        s = Doubled().Replace(s, ",");
        s = s.Trim(' ', ',', ':', '-', '+');

        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0 && words.All(w => Plain.Contains(w.Trim(',', ':', '.'))))
            return s.ToLowerInvariant();

        return s;
    }
}
