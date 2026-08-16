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

    public string Spoken => string.IsNullOrEmpty(Speech) ? Text : Speech;

    public override string ToString() =>
        $"{Time,8:F2} [{Level}]{(Personal ? " (me)" : "")} {Text}";
}
