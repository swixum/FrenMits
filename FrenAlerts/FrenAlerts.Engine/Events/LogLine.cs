namespace FrenAlerts.Engine;

public static class LogLine
{
    public const string HeadMarkerKind = "27";

    private const int Target = 2;
    private const int Marker = 6;

    private const int Fields = 7;

    // Returns null for every line that is not a head marker, which is nearly all of
    // them: a pull is a hundred thousand lines and a few dozen markers.
    public static GameEvent? Read(IReadOnlyList<string> fields, double now)
    {
        if (fields.Count < Fields) return null;
        if (fields[0] != HeadMarkerKind) return null;

        var id = Hex(fields[Marker]);
        var target = Hex(fields[Target]);
        if (id == 0 || target == 0) return null;

        return new GameEvent
        {
            Kind = EventKind.HeadMarker,
            Time = now,
            Id = id,
            TargetId = target,
        };
    }

    private static uint Hex(string s) =>
        uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0;
}
