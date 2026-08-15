using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FrenMits.Callouts;

// One duty's floor: where its middle is, how far the ground reaches, and how
// close to the middle still counts as the middle.
//
// A direction word only means something against a floor. Without one, "north"
// is a guess, so the library says nothing instead. Authored numbers come from a
// floor book; a floor learned from play carries Authored false and is only ever
// a fallback.
public readonly record struct Floor(
    uint Territory,
    string Name,
    float CenterX,
    float CenterY,
    float Radius,
    float BandX,
    float BandY,
    bool Square,
    bool Authored)
{
    // A floor with no reach cannot answer anything.
    public bool Known => Radius > 0f && !float.IsNaN(CenterX) && !float.IsNaN(CenterY);

    public Spot Middle => Known ? new Spot(CenterX, CenterY, 0f) : Spot.Nowhere;

    // Close enough to the middle to be called the middle. Bands are per axis
    // because arenas are not all square, and a long one splits differently
    // across than along.
    public bool IsMiddle(Spot at)
        => Known && at.Known
            && MathF.Abs(at.X - CenterX) <= BandX
            && MathF.Abs(at.Y - CenterY) <= BandY;

    // The named part of the floor a spot sits in, split by the bands: nine
    // areas, the middle and the eight around it. This is what a call means when
    // it says a direction.
    public Way Sector(Spot at)
    {
        if (!Known || !at.Known) return Way.Unknown;

        var east = at.X > CenterX + BandX;
        var west = at.X < CenterX - BandX;
        var south = at.Y > CenterY + BandY;
        var north = at.Y < CenterY - BandY;

        if (east) return north ? Way.NE : south ? Way.SE : Way.E;
        if (west) return north ? Way.NW : south ? Way.SW : Way.W;
        return north ? Way.N : south ? Way.S : Way.Middle;
    }

    // The same question answered by bearing rather than by bands, for a fight
    // whose spots sit on a circle instead of in quadrants.
    public Way Where(Spot at, Ring ring = Ring.Eight)
    {
        if (!Known || !at.Known) return Way.Unknown;
        if (IsMiddle(at)) return Way.Middle;
        return Compass.Of(at, Middle, ring);
    }

    public bool Inside(Spot at)
    {
        if (!Known || !at.Known) return false;
        if (!Square) return Middle.DistanceTo(at) <= Radius;
        return MathF.Abs(at.X - CenterX) <= Radius && MathF.Abs(at.Y - CenterY) <= Radius;
    }

    // How much ground is left between a spot and the edge; negative past it.
    public float Room(Spot at)
    {
        if (!Known || !at.Known) return float.NaN;
        if (!Square) return Radius - Middle.DistanceTo(at);
        return Radius - MathF.Max(MathF.Abs(at.X - CenterX), MathF.Abs(at.Y - CenterY));
    }

    // A spot that far out from the middle in one direction.
    public Spot At(Way w, float distance) => w == Way.Middle ? Middle : w.From(Middle, distance);

    // Where each of the eight sectors sits, at a sensible standing distance.
    public Spot Toward(Way w) => At(w, Radius * 0.65f);
}

// Floors by duty, in the same tab separated shape as the rest of the data files
// so one editor opens all of them.
public static class FloorBook
{
    public const string Magic = "fmfloor";
    public const int Version = 1;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(TextWriter w, IEnumerable<Floor> floors)
    {
        w.WriteLine($"{Magic} {Version}");
        foreach (var f in floors)
            w.WriteLine(string.Join(TextFields.Sep,
                f.Territory.ToString(Inv),
                TextFields.Escape(f.Name),
                f.CenterX.ToString("0.###", Inv),
                f.CenterY.ToString("0.###", Inv),
                f.Radius.ToString("0.###", Inv),
                f.BandX.ToString("0.###", Inv),
                f.BandY.ToString("0.###", Inv),
                f.Square ? "1" : "0"));
    }

    public static Dictionary<uint, Floor> Read(TextReader r)
    {
        var header = r.ReadLine() ?? throw new InvalidDataException("Empty floor book.");
        var parts = header.Split(' ');
        if (parts.Length != 2 || parts[0] != Magic)
            throw new InvalidDataException($"Not a floor book: '{header}'.");
        if (!int.TryParse(parts[1], NumberStyles.Integer, Inv, out var v) || v != Version)
            throw new InvalidDataException($"Floor book version {parts[1]}, expected {Version}.");

        var book = new Dictionary<uint, Floor>();
        while (r.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var f = line.Split(TextFields.Sep);
            if (f.Length < 8) continue;

            var territory = uint.Parse(f[0], NumberStyles.Integer, Inv);
            book[territory] = new Floor(
                territory,
                TextFields.Unescape(f[1]),
                float.Parse(f[2], NumberStyles.Float, Inv),
                float.Parse(f[3], NumberStyles.Float, Inv),
                float.Parse(f[4], NumberStyles.Float, Inv),
                float.Parse(f[5], NumberStyles.Float, Inv),
                float.Parse(f[6], NumberStyles.Float, Inv),
                f[7] == "1",
                Authored: true);
        }
        return book;
    }

    public static Floor For(this IReadOnlyDictionary<uint, Floor> book, uint territory)
        => book.TryGetValue(territory, out var f) ? f : default;
}

// A floor guessed from where people actually stood, for a duty nobody has
// measured yet. Middle is the median rather than the mean, so a stack strat or
// one player parked at a wall cannot drag it, and the reach is a high
// percentile rather than the furthest sample, so it errs small: a floor guessed
// too small refuses a call, a floor guessed too big walks somebody off the edge.
public sealed class FloorEstimate
{
    // Samples held at once. Oldest out first, so late in a pull the estimate
    // describes where the fight is now. Cleared by Reset.
    public const int MaxSamples = 512;

    // Below this there is not enough spread to call anything.
    public const int MinSamples = 64;

    // Which percentile of how far players got counts as the wall. Measured on
    // nine real ultimate pulls: half the samples sit within 6.5 yalms of the
    // middle and the ninetieth is only 12.9, while the floor really reaches 20.
    // Taking the ninetieth left every candidate spot in the inner third of the
    // room and the directing named places nobody could use. High enough to find
    // the wall, short of the handful of samples a knockback throws past it.
    public const float Wall = 0.99f;

    // How many times the typical distance from the middle a sample can be
    // before it is thrown out of the wall measurement. A high percentile finds
    // the wall but does not survive a duty where a tenth of the samples were
    // taken somewhere else entirely, so the scale is set by the median first
    // and anything wildly past it is dropped. Real pulls put the furthest
    // sample at three and a half times the median.
    public const float TrimAt = 5f;

    // How much of the floor around the middle still counts as the middle.
    public const float MiddleShare = 0.25f;

    // What a duty's floor can plausibly measure, in yalms. Measured against
    // real recordings: open world content guesses reaches of five hundred and
    // more, because players walk between hunt marks, and a middle worked out
    // over half a zone would have the library confidently directing people
    // nowhere. An authored floor is trusted as written; a guess has to look
    // like a room.
    public const float MinReach = 8f;
    public const float MaxReach = 80f;

    private readonly float[] _x = new float[MaxSamples];
    private readonly float[] _y = new float[MaxSamples];
    private readonly float[] _sort = new float[MaxSamples];
    private int _next;
    private int _count;
    private int _sinceBake;
    private Floor _baked;

    // Samples taken between rebuilds. Sorting on every question would cost more
    // than the answer is worth.
    public const int RebuildEvery = 32;

    public int Samples => _count;

    public void Note(Spot at)
    {
        if (!at.Known) return;

        _x[_next] = at.X;
        _y[_next] = at.Y;
        _next = (_next + 1) % MaxSamples;
        if (_count < MaxSamples) _count++;
        _sinceBake++;
    }

    public void Reset()
    {
        _next = 0;
        _count = 0;
        _sinceBake = 0;
        _baked = default;
    }

    // The floor as it looks right now, or nothing while there is too little to
    // go on. Rebuilt on a counter rather than per call, since sorting costs more
    // than the answer is worth.
    public Floor Guess(uint territory, string name = "")
    {
        if (_count < MinSamples) return default;
        if (_sinceBake < RebuildEvery && _baked.Known && _baked.Territory == territory) return _baked;

        _sinceBake = 0;
        return _baked = Build(territory, name, _x, _y, _count, _sort);
    }

    // The same shape read off a whole recording rather than a live pull, so a
    // mined floor and a watched one are one piece of arithmetic.
    public static Floor Of(uint territory, IReadOnlyList<Spot> spots, string name = "")
    {
        var count = 0;
        var x = new float[spots.Count];
        var y = new float[spots.Count];
        foreach (var s in spots)
        {
            if (!s.Known) continue;
            x[count] = s.X;
            y[count] = s.Y;
            count++;
        }

        if (count < MinSamples) return default;
        return Build(territory, name, x, y, count, new float[count]);
    }

    private static Floor Build(uint territory, string name, float[] x, float[] y, int count, float[] sort)
    {
        var cx = Percentile(x, count, sort, 0.5f);
        var cy = Percentile(y, count, sort, 0.5f);

        for (var i = 0; i < count; i++)
            sort[i] = MathF.Sqrt((x[i] - cx) * (x[i] - cx) + (y[i] - cy) * (y[i] - cy));
        Array.Sort(sort, 0, count);

        // The wall, measured over what is left after the far strays are cut.
        var typical = Pick(sort, count, 0.5f);
        if (typical <= 0f) return default;

        var kept = count;
        while (kept > 1 && sort[kept - 1] > typical * TrimAt) kept--;

        var radius = Pick(sort, kept, Wall);
        if (radius is < MinReach or > MaxReach) return default;

        return new Floor(
            territory,
            name,
            cx,
            cy,
            radius,
            radius * MiddleShare,
            radius * MiddleShare,
            Square: false,
            Authored: false);
    }

    private static float Percentile(float[] source, int count, float[] sort, float p)
    {
        Array.Copy(source, sort, count);
        Array.Sort(sort, 0, count);
        return Pick(sort, count, p);
    }

    private static float Pick(float[] sort, int count, float p)
        => sort[Math.Clamp((int)MathF.Floor(p * (count - 1) + 0.5f), 0, count - 1)];
}
