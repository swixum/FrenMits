namespace FrenAlerts.Engine.Scripts;

// Where each limit cut number stands, ported from theirs.
//
// The eight numbers walk around the arena from wherever the first one starts, and
// which spot each number gets is a rotation of one fixed list rather than anything
// worked out from an angle. Their three tables below are that list: the order the
// symbols sit in, the spots read clockwise, and the spots read the other way.
//
// A table, not arithmetic, on purpose. Deriving it looks like it would work and
// quietly sends half the group to the wrong side when the rotation wraps.
public static class LimitCut
{
    // The symbols in the order they sit around the arena, starting at 1.
    public static readonly string[] SymbolOrder = ["1", "A", "2", "B", "3", "C", "4", "D"];

    public static readonly string[] Clockwise = ["B3", "2B", "A2", "1A", "D1", "4D", "C4", "3C"];

    public static readonly string[] CounterClockwise = ["3C", "C4", "4D", "D1", "1A", "A2", "2B", "B3"];

    // Their defaults, kept because the two braces in them are the whole format:
    // {start} and {dir} in the header, {n} and {spot} in each line.
    public const string DefaultHeader = "Limit Cut ({start} start, {dir})";

    public const string DefaultLine = "{n} -> {spot}";

    // Which spot each of the eight numbers takes, or nothing when the start symbol is
    // not one of theirs.
    public static string[]? Spots(bool clockwise, string startSymbol)
    {
        var start = Array.IndexOf(SymbolOrder, startSymbol);
        if (start < 0) return null;

        var source = clockwise ? Clockwise : CounterClockwise;
        var spots = new string[8];
        for (var i = 0; i < 8; i++)
        {
            var index = clockwise ? ((i - start) % 8 + 8) % 8 : ((i + start) % 8 + 8) % 8;
            spots[i] = source[index];
        }
        return spots;
    }

    // The whole post, header first, one line per number. Handed to the macro queue,
    // which sends it a line at a time.
    public static string? Post(
        bool clockwise, string startSymbol, string header = DefaultHeader, string line = DefaultLine)
    {
        var spots = Spots(clockwise, startSymbol);
        if (spots is null) return null;

        var lines = new List<string>(9)
        {
            (header ?? "").Replace("{start}", startSymbol).Replace("{dir}", clockwise ? "CW" : "CCW"),
        };

        for (var i = 0; i < 8; i++)
            lines.Add((line ?? "").Replace("{n}", (i + 1).ToString()).Replace("{spot}", spots[i]));

        return string.Join("\n", lines);
    }

    // Which waymark a spot is nearest, which is how a fight works out where the
    // first number started. Their own answer: nearest by plain distance, no ties
    // broken, since two waymarks in the same place is not a thing a group sets up.
    public static string? NearestSymbol(IReadOnlyList<Waymark> waymarks, float x, float z)
    {
        var best = -1;
        var closest = double.MaxValue;

        foreach (var mark in waymarks)
        {
            var dx = mark.X - x;
            var dz = mark.Z - z;
            var distance = dx * dx + dz * dz;
            if (distance >= closest) continue;
            closest = distance;
            best = mark.Index;
        }

        return best is >= 0 and < 8 ? SlotSymbols[best] : null;
    }

    // Their slot order: the four letters first, then the four numbers.
    public static readonly string[] SlotSymbols = ["A", "B", "C", "D", "1", "2", "3", "4"];
}

// One waymark on the floor, in the shape their scripts read: which slot it is and
// where it is. The game side fills these in; nothing here reads memory.
public readonly record struct Waymark(int Index, float X, float Z)
{
    // Their own array shape, which is what a script gets back from the host.
    public double[] AsRow() => [Index, X, Z];
}
