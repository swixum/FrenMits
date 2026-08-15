namespace FrenMits.Callouts;

// How loud a call should feel; the host maps these onto its own colors.
public enum CallSeverity
{
    Info = 0,
    Warn,
    Danger,
}

// One thing to tell the player. The host decides how to show or speak it.
public sealed record Call
{
    // Banner text, kept short enough to read mid-pull.
    public string Text { get; init; } = "";

    // Spoken text; empty falls back to Text.
    public string Tts { get; init; } = "";

    // Voice pack lookup; a missing clip falls back to speech.
    public string ClipKey { get; init; } = "";

    public CallSeverity Severity { get; init; } = CallSeverity.Info;

    // Fight time this belongs to, so the host can lead or delay it.
    public float At { get; init; }

    public float Duration { get; init; } = 4f;

    // Aimed at this player rather than the party.
    public bool Personal { get; init; }

    // Where to go when it is a direction on the floor. Spoken as a word and
    // drawn as an abbreviation, out of one place, so the two cannot disagree.
    public Way Direction { get; init; } = Way.Unknown;

    // Where to go when it is not a direction: "on A", "out", "under".
    public string Where { get; init; } = "";

    // What gets said, with where to go on the end when something knows.
    public string Spoken
    {
        get
        {
            var words = Tts.Length > 0 ? Tts : Text;
            var go = Direction != Way.Unknown ? Direction.Name() : Where;
            return go.Length > 0 ? $"{words}, {go}" : words;
        }
    }

    // What gets drawn, same rule, in fewer letters.
    public string Banner
    {
        get
        {
            var go = Direction != Way.Unknown ? Direction.Short() : Where;
            return go.Length > 0 ? $"{Text}, {go}" : Text;
        }
    }
}
