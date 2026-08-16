using System.Text.RegularExpressions;

namespace FrenAlerts.DevTools;

public static partial class Naming
{
    [GeneratedRegex(@"^(?:R\d+S|DMU|FRU)\b\s*")]
    private static partial Regex FightTag();

    [GeneratedRegex(@"\b(?:and\s+)?P\d\b\s*")]
    private static partial Regex PhaseTag();

    // Trailing "(Early)", "(Enrage Sequence)", "(Snaking)": the writer's notes to
    // themselves about when the trigger fires, not part of what to say.
    [GeneratedRegex(@"\s*\([^)]*\)")]
    private static partial Regex Aside();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Runs();

    [GeneratedRegex(@"[\s,]*\b(?:Early|Reminder|Followup|Follow-up|Initial|Part)\b\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Timing();

    // Left behind where a counter was removed from the middle of a name:
    // "Black Hole 2, Nothingness 2" loses both digits and would keep the gap.
    [GeneratedRegex(@"\s+([,:])")]
    private static partial Regex Orphaned();

    private static readonly string[] NotACall =
    [
        "collector", "cardinal/intercardinal", "far target", "tether number",
        "tower number", "location", "positions",
    ];

    private static readonly (string Ending, string Say)[] Answers =
    [
        ("light party stacks", "light party stacks"),
        ("light parties", "light parties"),
        ("healer groups", "healer groups"),
        ("safe spot", "safe spot"),
        ("safe spots", "safe spots"),
        ("tank swap", "tank swap"),
        ("tank side", "tanks to the side"),
        ("dodge cleaves", "dodge the cleaves"),
        ("tankbuster", "buster"),
        ("buster", "buster"),
        ("knockback", "knockback"),
        ("towers", "towers"),
        ("tower", "tower"),
        ("stacks", "stack"),
        ("stack", "stack"),
        ("spread", "spread"),
        ("cleanse", "cleanse it"),
        ("enrage", "enrage"),
        ("swap", "tank swap"),
        ("bait", "bait it"),
        ("in", "get in"),
        ("out", "get out"),
    ];

    private static readonly Dictionary<string, string> Better = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DMU P5 Catastrophic Choice In"] = "get in",
        ["DMU P5 Catastrophic Choice Out"] = "get out",
        ["DMU P5 Enrage"] = "enrage",
        ["DMU P3 Thunder III AOE"] = "raidwide",
        ["DMU P3 Thunder III Tank Swap"] = "tank swap",
        ["DMU P3 Thunder III Tankbuster"] = "buster",
        ["DMU P4 Acceleration Bomb Reminder"] = "stop moving",
        ["R12S Doom Cleanse"] = "cleanse the doom",
        ["R12S Avoid Earth Tower (Missing Dooms)"] = "stay out of the tower",
        ["R12S Double Sobat on you"] = "double buster on you",
        ["R12S Shared Grotesquerie"] = "share it",
        ["R9S Headmarker Tankbuster"] = "buster on you",
    };

    public static string Bare(string triggerName)
    {
        var name = IdTail().Replace(triggerName, "");
        name = FightTag().Replace(name.Trim(), "");
        name = PhaseTag().Replace(name, "");
        return Runs().Replace(name, " ").Trim();
    }

    [GeneratedRegex(@"\s*#\d+\s*$")]
    private static partial Regex IdTail();

    public static string Mechanic(string triggerName) =>
        Runs().Replace(Aside().Replace(Bare(triggerName), " "), " ").Trim();

    public static bool TryFor(string triggerName, out string text)
    {
        text = "";
        var name = Aside().Replace(Bare(triggerName), " ");
        name = Runs().Replace(name, " ");
        name = Orphaned().Replace(name, "$1");
        name = Timing().Replace(name, "");
        name = Runs().Replace(name, " ").Trim(' ', ',', '-', ':', '+');

        if (name.Length == 0) return false;

        var lower = name.ToLowerInvariant();
        if (NotACall.Any(lower.Contains)) return false;

        if (Better.TryGetValue(triggerName.Trim(), out var better))
        {
            text = better;
            return true;
        }

        foreach (var (ending, say) in Answers)
        {
            if (!lower.EndsWith(ending, StringComparison.Ordinal)) continue;
            // The word has to stand alone, or "Fearsome Fireball" loses its tail
            // to a rule about the word "all".
            var cut = name.Length - ending.Length;
            if (cut > 0 && name[cut - 1] != ' ') continue;

            var mechanic = name[..cut].Trim(' ', ',', '-');
            text = mechanic.Length == 0 ? say : $"{mechanic}, {say}";
            return true;
        }

        text = name;
        return true;
    }
}
