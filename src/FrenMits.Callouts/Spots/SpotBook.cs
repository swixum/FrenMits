using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FrenMits.Callouts;

// Where one slot should be for one mechanic. Positions are relative to the
// arena's middle and normalized to its radius, so a plan drawn on a picture and
// a spot mined from real kills describe the same thing.
public readonly record struct Spotting(string Mechanic, string Slot, float X, float Y, string Where, float Spread)
{
    // How tightly the source agreed. Zero means an authored plan, which is exact.
    public bool Trustworthy => Spread <= 0.35f;
}

// Per-duty spots, from an authored plan or mined from kills. Same file either
// way, so the engine does not care which one a group uses.
public static class SpotBook
{
    public const string Magic = "fmspot";
    public const int Version = 1;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(TextWriter w, IReadOnlyDictionary<uint, List<Spotting>> byTerritory)
    {
        w.WriteLine($"{Magic} {Version}");
        foreach (var (territory, spots) in byTerritory)
            foreach (var s in spots)
                w.WriteLine(string.Join(TextFields.Sep,
                    territory.ToString(Inv),
                    TextFields.Escape(s.Mechanic),
                    TextFields.Escape(s.Slot),
                    s.X.ToString("0.###", Inv),
                    s.Y.ToString("0.###", Inv),
                    TextFields.Escape(s.Where),
                    s.Spread.ToString("0.###", Inv)));
    }

    public static Dictionary<uint, List<Spotting>> Read(TextReader r)
    {
        var header = r.ReadLine() ?? throw new InvalidDataException("Empty spot book.");
        var parts = header.Split(' ');
        if (parts.Length != 2 || parts[0] != Magic)
            throw new InvalidDataException($"Not a spot book: '{header}'.");
        if (!int.TryParse(parts[1], NumberStyles.Integer, Inv, out var v) || v != Version)
            throw new InvalidDataException($"Spot book version {parts[1]}, expected {Version}.");

        var book = new Dictionary<uint, List<Spotting>>();
        while (r.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var f = line.Split(TextFields.Sep);
            if (f.Length < 7) continue;

            var territory = uint.Parse(f[0], NumberStyles.Integer, Inv);
            if (!book.TryGetValue(territory, out var list)) book[territory] = list = new List<Spotting>();
            list.Add(new Spotting(
                TextFields.Unescape(f[1]),
                TextFields.Unescape(f[2]),
                float.Parse(f[3], NumberStyles.Float, Inv),
                float.Parse(f[4], NumberStyles.Float, Inv),
                TextFields.Unescape(f[5]),
                float.Parse(f[6], NumberStyles.Float, Inv)));
        }
        return book;
    }

    // How close to the middle still reads as the middle, in floor radii.
    public const float MiddleBand = 0.08f;

    // The direction of an offset that is already relative to the middle and
    // scaled to the floor's radius. Same compass as everything else, so a spot
    // and a dodge never name the same place two ways.
    public static Way Way(float x, float y, Ring ring = Ring.Eight)
        => MathF.Abs(x) <= MiddleBand && MathF.Abs(y) <= MiddleBand
            ? Callouts.Way.Middle
            : Compass.Of(x, y, ring);

    // The same, written the short way, which is what a stored spot carries.
    public static string Direction(float x, float y) => Way(x, y).Short();

    // The spot for one slot, or nothing when the source did not agree with itself.
    public static Spotting? Find(this IReadOnlyList<Spotting> spots, string mechanic, string slot)
    {
        foreach (var s in spots)
            if (s.Trustworthy
                && string.Equals(s.Mechanic, mechanic, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.Slot, slot, StringComparison.OrdinalIgnoreCase))
                return s;
        return null;
    }
}
