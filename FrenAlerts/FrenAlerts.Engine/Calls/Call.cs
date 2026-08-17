namespace FrenAlerts.Engine;

public enum CallLevel
{
    Info,   // worth knowing
    Alert,  // act now
    Alarm,  // act now or die
}

public sealed record Call
{
    public required string Text { get; init; }
    public required double Time { get; init; }

    public CallLevel Level { get; init; } = CallLevel.Info;

    public bool Personal { get; init; }

    public required string Key { get; init; }

    // Seconds to keep it on screen.
    public float Hold { get; init; } = 4f;

    public float Hush { get; init; }

    public bool Once { get; init; }

    public string Speech { get; init; } = "";

    // A colour this one call asks for, packed the way the screen wants it, or zero
    // to take the colour of its level like everything else.
    //
    // Only a hand-written trigger sets this: their editor lets somebody colour their
    // own call, and a call that comes out amber when they picked green reads as the
    // setting being ignored.
    public uint Tint { get; init; }

    // How big this one call asks to be, against whatever size the screen is set to.
    // One is the normal answer and means take the setting as it is.
    public float Scale { get; init; } = 1f;

    // Where this one call asks to be drawn, as a fraction of the screen, or null to
    // sit in the stack with everything else.
    //
    // A hand-written trigger can place its own call, and the reason is worth keeping
    // in mind: somebody writes one for a mechanic they need to read while looking at
    // a different corner of the screen. Placed, it is not in the stack at all, so it
    // can never push a fight's call around either.
    public System.Numerics.Vector2? At { get; init; }

    public bool Placed => At is not null;

    public string Spoken => string.IsNullOrEmpty(Speech) ? Text : Speech;

    public override string ToString() =>
        $"{Time,8:F2} [{Level}]{(Personal ? " (me)" : "")} {Text}";
}
