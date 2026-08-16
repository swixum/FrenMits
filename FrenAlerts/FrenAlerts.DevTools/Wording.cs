using System.Text.RegularExpressions;

namespace FrenAlerts.DevTools;

public static partial class Wording
{
    private sealed record Rule(Regex Pattern, string Replacement, bool LowerCaptures = false);

    // Order matters: the long forms have to run before the short ones, or
    // "Tank Buster on YOU" is half-rewritten by the "Tank Buster" rule first.
    private static readonly Rule[] Rules =
    [
        R(@"^Shared Tank Buster on YOU\b.*$", "share the buster, on you"),
        R(@"^Shared Tank Buster on \{target\}$", "share the buster, {target}"),
        R(@"^Shared Tank Buster$", "share the buster"),
        R(@"^Tank Buster on YOU\b.*$", "buster on you"),
        R(@"^Tank Buster on \{target\}$", "buster on {target}"),
        R(@"^Tank (?:Buster|Cleave)s?$", "buster"),
        R(@"^Avoid Tank Cleaves?$", "out of the cleave"),
        R(@"^Avoid Front \+ Side Cleaves$", "out of the front and side cleaves"),
        R(@"^AoE x6 (?:then )?Big AoE$", "six raidwides, then a heavy one"),
        R(@"^Avoid (?:the )?line AoEs?$", "out of the lines"),
        R(@"^Avoid (.+)$", "out of $1"),
        R(@"^big AoE!?$", "heavy raidwide"),
        R(@"^(?:AoE|Raidwide)!?$", "raidwide"),
        R(@"^Move!?$", "move"),
        R(@"^Stack on YOU\b.*$", "stack on you"),
        R(@"^Stack on \{target\}$", "stack on {target}"),
        R(@"^Spread on YOU\b.*$", "spread, on you"),
        R(@"^Get Out\b.*$", "get out"),
        R(@"^Get In\b.*$", "get in"),
        R(@"^Get Behind\b.*$", "get behind"),
        R(@"^Get Front\b.*$", "get in front"),
        R(@"^Get Under\b.*$", "get under"),
        R(@"^Get Middle\b.*$", "get to the middle"),
        R(@"^Get tethers?$", "take your tether"),
        R(@"^Tank Swap\b.*$", "tank swap"),
        R(@"^Bait cleaves?$", "bait the cleave"),
        R(@"^Drop seeds?$", "drop your seed"),
        R(@"^Cleanse in spotlight$", "cleanse in the spotlight"),
        R(@"^Rotate away from proximity markers$", "rotate away from the markers"),
        R(@"^Spread, Away from front$", "spread, away from the front"),
        R(@"^Go North, big AoE \+ Launch$", "north, heavy raidwide and launch"),
        R(@"^(.+?) Spread/Stack$", "$1, spread or stack"),
        R(@"^(.+?) Stack/Spread$", "$1, stack or spread"),
        // The repeat count is already appended by the time the rules run, so these
        // match what a counted line looks like rather than its bare form.
        R(@"^Stack, (.+)$", "stack, $1", lower: true),
        R(@"^Dodge w/Partner, (.+)$", "dodge with your partner, $1", lower: true),
        R(@"^TANK LB$", "tank limit break"),
        R(@"^Raidwide, (.+)$", "raidwide, $1", lower: true),
        R(@"^E/W Groups, Out of Middle$", "east west groups, out of the middle"),
        R(@"^E/W (.+)$", "east west, $1", lower: true),
        R(@"^N/S (.+)$", "north south, $1", lower: true),
        R(@"^Go N/S \+ Big AoE$", "north or south, heavy raidwide"),
        R(@"^East/West (.+)$", "east or west, $1", lower: true),
        R(@"^North/South (.+)$", "north or south, $1", lower: true),
        R(@"^Front/Back (.+)$", "front or back, $1", lower: true),
        R(@"^(.+) Front/Back$", "$1, front or back", lower: true),
        R(@"^Out Of (.+)$", "out of the $1", lower: true),
        R(@"^Tower Knockback to (.+)$", "knockback to the $1", lower: true),
        R(@"^LoS behind (.+)$", "line of sight behind the $1", lower: true),
        // The mechanic being baited keeps its name; only the instruction is ours.
        R(@"^Bait cleaves? towards (.+)$", "bait the cleave toward $1"),
        R(@"^Bait (.+)$", "bait $1"),
        R(@"^Draw In$", "draw in"),
        R(@"^In Line Debuff$", "in line debuff"),
        R(@"^Look Away(?: From (.+))?$", "look away"),
        R(@"^Look At (.+)$", "look at the $1"),
        R(@"^Knockback\b.*$", "knockback"),
        R(@"^Sides$", "go to the sides"),
        R(@"^Healer Groups?$", "healer groups"),
        R(@"^Light ?parties?$", "light parties"),
        R(@"^Out of Melee$", "out of melee range"),
        R(@"^Under\b.*$", "get under"),
        R(@"^Away From (.+)$", "away from $1"),
        R(@"^Get Towers?$", "towers"),
        R(@"^Stack With Partner$", "stack with your partner"),
        R(@"^Stored:\s*(.+)$", "stored, $1", lower: true),
        R(@"^\((.+) after\)$", "$1 after", lower: true),
        R(@"^(.+?) Stack/Defamation\b.*$", "$1, stack or defamation", lower: true),
        R(@"^(.+?)\s*\+\s*(.+)$", "$1 and $2", lower: true),
    ];

    private static readonly HashSet<string> Irreducible = new(StringComparer.OrdinalIgnoreCase)
    {
        "stack", "spread", "out", "in", "under", "towers", "knockback",
        "sides", "middle", "corners", "front", "behind", "left", "right",
        "north", "south", "east", "west", "move", "bait", "swap",
    };

    private static Rule R(string pattern, string replacement, bool lower = false) =>
        new(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled), replacement, lower);

    private static readonly string[] Words =
        ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"];

    private static string Count(string digits) =>
        int.TryParse(digits, out var n) && n < Words.Length ? Words[n] : digits;

    [GeneratedRegex(@"\s*(?:=>|<=|-->|<--)\s*")]
    private static partial Regex Arrows();

    [GeneratedRegex(@"\s+--\s+")]
    private static partial Regex Dashes();

    [GeneratedRegex(@"\s*\b(?:x(\d+)|(\d+)x)\s*$")]
    private static partial Regex Repeats();

    // The same count in the middle counts the thing after it: "Bait 3x puddles".
    [GeneratedRegex(@"\b(\d+)x\s+")]
    private static partial Regex Each();

    [GeneratedRegex(@"\s*\((?:early|later|optional|enrage sequence|snaking)\)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Aside();

    // Shouting is the source's emphasis, and ours is carried by the call's level.
    [GeneratedRegex(@"[!?]+$")]
    private static partial Regex Emphasis();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Runs();

    public static string Rewrite(string source)
    {
        var text = Arrows().Replace(source, " ").Trim();
        text = Dashes().Replace(text, ", ");
        text = Aside().Replace(text, "");
        text = Repeats().Replace(text, m =>
            $", {Count(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)} hits");
        text = Each().Replace(text, m => $"{Count(m.Groups[1].Value)} ");
        text = Emphasis().Replace(text, "").Trim();
        text = Runs().Replace(text, " ");

        foreach (var rule in Rules)
        {
            if (!rule.Pattern.IsMatch(text)) continue;
            var result = rule.Pattern.Replace(text, rule.Replacement);
            return rule.LowerCaptures ? result.ToLowerInvariant() : result;
        }

        // Slash pairs read as one direction, not two: "Get Right/East" is a single
        // instruction phrased twice, so it becomes a comma the voice can pace.
        var slashed = SlashPair().Match(text);
        if (slashed.Success)
            return $"{slashed.Groups[1].Value.ToLowerInvariant()}, {slashed.Groups[2].Value.ToLowerInvariant()}";

        return text;
    }

    [GeneratedRegex(@"^(?:Get\s+)?([A-Za-z]+)/([A-Za-z]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SlashPair();

    public static bool StillTheirVoice(string source, string rewritten)
    {
        if (!string.Equals(source.Trim(), rewritten, StringComparison.Ordinal)) return false;
        if (Irreducible.Contains(rewritten.Trim())) return false;
        return InstructionWords().IsMatch(rewritten);
    }

    [GeneratedRegex(@"\b(avoid|get|go|move|stack|spread|look|dodge|run|stay|swap|bait|drop|out|in|away|under|behind|front|left|right|north|south|east|west)\b",
                    RegexOptions.IgnoreCase)]
    private static partial Regex InstructionWords();
}
