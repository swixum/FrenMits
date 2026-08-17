namespace FrenAlerts.Engine.Scripts;

// One call the imported set can make, as a page can show it.
//
// Their triggers carry the mechanic in the id, because that is how their files are
// written: "DMU P3 Black Hole Order", "UWU Diffractive Laser". So the id is the
// mechanic's name here, and the phase is read back out of it rather than stored
// twice.
public sealed record ScriptShownCall(string Id, bool Speaks, IReadOnlyList<string> Words)
{
    // The fight's own short name, where their id starts with one: "DMU P3 Something"
    // is the third phase of Dancing Mad. Empty where a file names its triggers some
    // other way, which is most of the savage ones.
    public string Phase
    {
        get
        {
            var parts = Id.Split(' ', 3);
            if (parts.Length < 2) return "";

            var second = parts[1];
            return second.Length is 2 and > 0 && second[0] is 'P' or 'p'
                   && char.IsDigit(second[1])
                ? second.ToUpperInvariant()
                : "";
        }
    }

    // What it says, as one line for a list. Several, where their trigger picks between
    // words depending on what happened, which is most of the interesting ones.
    public string Line => Words.Count == 0 ? "" : string.Join("  /  ", Words);
}
