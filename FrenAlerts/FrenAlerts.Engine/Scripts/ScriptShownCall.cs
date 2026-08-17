namespace FrenAlerts.Engine.Scripts;

// One line a call can put on screen, and the output keys that say it.
//
// Keys rather than a key, because their tables reach the same words under several
// names: Mystery Magic composes four lines that all read "${mech} + ${ice}". Those are
// one line to read and one thing to reword, so a rewording goes to every key that says
// it. Empty where the words were read without keys, which is a line that can be shown
// and not reworded.
public sealed record ScriptShownLine(IReadOnlyList<string> Keys, string Text)
{
    // A line their code fills in as it fires: "${mech} + ${ice}", "Knockback from
    // ${players}". Worth knowing because a page cannot show what it will come out as,
    // so it is not the line to lead a row with.
    public bool FillsIn { get; } = Text.Contains("${", StringComparison.Ordinal);
}

// One call the imported set can make, as a page can show it.
//
// Their triggers carry the mechanic in the id, because that is how their files are
// written: "DMU P3 Black Hole Order", "UWU Diffractive Laser". So the mechanic's name
// is read back out of the id, and so is the phase, rather than either being stored
// twice.
public sealed record ScriptShownCall(
    string Id, bool Speaks, IReadOnlyList<ScriptShownLine> Lines, string Name = "")
{
    // The fight's own short name, where their id starts with one: "DMU P3 Something"
    // is the third phase of Dancing Mad. Empty where a file names its triggers some
    // other way, which is most of the savage ones.
    public string Phase { get; } = PhaseOf(Id);

    // The mechanic: the name the fight gave the call, or the id with the fight and the
    // phase taken off the front where it named none.
    //
    // A name wins because the ids are not all written by hand. A fight built through
    // the authoring kit generates one from the fight, the event and the ability code,
    // so an unnamed call reached the page as "AacHeavyweightM1Savage StartsUsing B33E
    // [17]" and every savage fight was a column of those.
    //
    // Taken off as a run rather than as two words, because a mechanic that happens in
    // two phases is named for both: "DMU P1 and P4 Mystery Magic Ice and Thunder" read
    // as "and P4 Mystery Magic Ice and Thunder" on the page, which is how a fight page
    // ends up looking like a parser error.
    public string Mechanic { get; } = Name.Length > 0 ? Name : MechanicOf(Id);

    // The one line to show beside the name when the rest are folded away.
    //
    // The first that does not fill itself in, because a line their code completes at
    // call time reads as code rather than as a call: "Spread" says more about the
    // mechanic than "${mech} + ${ice}" does. Falls back to the first line, so a call
    // whose every line fills in still shows its shape rather than nothing.
    public string Lead { get; } =
        Lines.FirstOrDefault(l => !l.FillsIn)?.Text ?? Lines.FirstOrDefault()?.Text ?? "";

    private static string PhaseOf(string id)
    {
        var parts = id.Split(' ');
        return parts.Length >= 2 && IsPhase(parts[1]) ? parts[1].ToUpperInvariant() : "";
    }

    // "P1", "p4". Their only phase spelling, and the thing that separates a fight tag
    // from the first word of a mechanic's name.
    private static bool IsPhase(string word) =>
        word.Length >= 2 && word[0] is 'P' or 'p' && word[1..].All(char.IsDigit);

    // A fight's own tag: "DMU", "UWU", "M12S". Their files all lead with one, and it is
    // on every row of the page it is drawn on, so it is dropped. A file that names its
    // triggers some other way leads with an ordinary word, which this leaves alone.
    private static bool IsFightTag(string word) =>
        word.Length is >= 2 and <= 6 && !IsPhase(word)
        && word.All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c));

    // What joins two phases in one of their ids: "P1 and P4", "P3 & P4", "P1, P2".
    private static bool JoinsPhases(string word) =>
        word is "and" or "&" or "or" or "+" or "," or "/";

    private static string MechanicOf(string id)
    {
        var words = id.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var at = 0;
        if (at < words.Length && IsFightTag(words[at])) at++;

        // The whole leading run of phases and whatever joins them, so both halves of a
        // two-phase name come off together.
        while (at < words.Length)
        {
            if (IsPhase(words[at])) { at++; continue; }
            if (JoinsPhases(words[at]) && at + 1 < words.Length && IsPhase(words[at + 1]))
            {
                at += 2;
                continue;
            }
            break;
        }

        // An id that is nothing but a tag and a phase keeps its own words, because a
        // row with no name at all is worse than a row named awkwardly.
        return at < words.Length ? string.Join(' ', words[at..]) : id;
    }
}
