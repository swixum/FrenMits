namespace FrenAlerts.Engine;

public sealed record CallSpec
{
    public required ushort Territory { get; init; }
    public required string Id { get; init; }

    public string Key { get; init; } = "";

    public string DedupeKey => string.IsNullOrEmpty(Key) ? Id : Key;
    public required EventKind On { get; init; }

    public uint MatchId { get; init; }

    public required string Text { get; init; }
    public string Speech { get; init; } = "";

    public int Phase { get; init; }

    public CallLevel Level { get; init; } = CallLevel.Info;
    public bool OnlyMe { get; init; }
    public bool Personal { get; init; }

    // Which case of the mechanic this line covers.
    public Aim Aim { get; init; } = Aim.Anyone;

    public int Occurrence { get; init; }

    // Seconds to wait after the event before saying it, for calls that are about
    // the resolve rather than the cast.
    public float Delay { get; init; }
    public float Hold { get; init; } = 4f;

    public string For { get; init; } = "";

    public Aim From { get; init; } = Aim.Anyone;

    public float Hush { get; init; }

    public bool Once { get; init; }

    public bool DefaultOn { get; init; } = true;

    public bool Reproduced { get; init; } = true;

    public bool NeedsWording { get; init; }

    // The same as Call.Spoken, and for the same reason: the arrow is drawn on screen
    // and said as a word.
    public string Spoken =>
        Alerts.CallText.Speak(string.IsNullOrEmpty(Speech) ? Text : Speech);
}
