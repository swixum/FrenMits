using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FrenMits.Callouts;

// What an action covers on the ground, straight from the game's own numbers.
// Cast type is the game's value; what each one means is settled by measurement
// against real logs rather than by assumption, so it stays a raw byte here.
// Width is a line's width or a donut's inner radius; Angle is a cone's spread.
//
// Measured says the spread or the hole came from watching real hits rather than
// from the game's own telegraph, because most actions ship without one. It
// changes nothing about how the shape is used and everything about how much a
// diagnostic should trust it.
//
// Reaims says the caster turns while it casts, so the shape only fits the way
// it was facing when the damage landed. Measured on real pulls: one ability
// fits 23% of the time against the facing at cast start and 87% against the
// facing at the hit. Nothing can predict that turn, so a call cannot say which
// way to move for one of these before it happens.
public readonly record struct ActionShape(
    uint ActionId,
    byte CastType,
    float Range,
    float Width,
    float Angle,
    string Name,
    bool Measured = false,
    bool Reaims = false)
{
    public bool Known => CastType != 0;
}

// The shape table, baked from the installed game and loaded with no dependency.
public static class ActionShapes
{
    public const string Magic = "fmgeom";
    public const int Version = 4;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(TextWriter w, IEnumerable<ActionShape> shapes)
    {
        w.WriteLine($"{Magic} {Version}");
        foreach (var s in shapes)
            w.WriteLine(string.Join(TextFields.Sep,
                s.ActionId.ToString("X", Inv),
                s.CastType.ToString(Inv),
                s.Range.ToString("0.###", Inv),
                s.Width.ToString("0.###", Inv),
                s.Angle.ToString("0.###", Inv),
                TextFields.Escape(s.Name),
                s.Measured ? "1" : "0",
                s.Reaims ? "1" : "0"));
    }

    public static Dictionary<uint, ActionShape> Read(TextReader r)
    {
        var header = r.ReadLine() ?? throw new InvalidDataException("Empty shape table.");
        var parts = header.Split(' ');
        if (parts.Length != 2 || parts[0] != Magic)
            throw new InvalidDataException($"Not a shape table: '{header}'.");
        if (!int.TryParse(parts[1], NumberStyles.Integer, Inv, out var v) || v != Version)
            throw new InvalidDataException($"Shape table version {parts[1]}, expected {Version}.");

        var shapes = new Dictionary<uint, ActionShape>();
        while (r.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var f = line.Split(TextFields.Sep);
            if (f.Length < 6) continue;

            var id = uint.Parse(f[0], NumberStyles.HexNumber, Inv);
            shapes[id] = new ActionShape(
                id,
                byte.Parse(f[1], Inv),
                float.Parse(f[2], NumberStyles.Float, Inv),
                float.Parse(f[3], NumberStyles.Float, Inv),
                float.Parse(f[4], NumberStyles.Float, Inv),
                TextFields.Unescape(f[5]),
                f.Length > 6 && f[6] == "1",
                f.Length > 7 && f[7] == "1");
        }
        return shapes;
    }

    public static ActionShape For(this IReadOnlyDictionary<uint, ActionShape> shapes, uint actionId)
        => shapes.TryGetValue(actionId, out var s) ? s : default;
}
