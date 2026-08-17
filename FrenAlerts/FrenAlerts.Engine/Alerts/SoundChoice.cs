namespace FrenAlerts.Engine.Alerts;

// Which sound a hand-written trigger asked for.
//
// The field is called a path and carries several shapes: a bare number, their own
// se.4, and the <se.4> somebody copies straight out of a macro. Reading it lives here
// rather than beside the code that plays it, because every one of those shapes is a
// rule worth checking and playing a noise is not something a test can hear.
public static class SoundChoice
{
    // What the game has. One is the plain one every macro uses.
    public const int Most = 16;

    public static bool Names(string path) => Number(path) > 0;

    // The effect a trigger asked for, or zero for one that named something else: a
    // file on somebody else's machine, or nothing at all.
    public static int Number(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return 0;

        var text = path.Trim().Trim('<', '>').Trim();
        if (text.StartsWith("se.", StringComparison.OrdinalIgnoreCase)) text = text[3..];
        else if (text.StartsWith("sound", StringComparison.OrdinalIgnoreCase)) text = text[5..].Trim();

        return int.TryParse(text, out var n) && n is >= 1 and <= Most ? n : 0;
    }
}
