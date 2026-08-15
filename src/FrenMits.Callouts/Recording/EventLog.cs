using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FrenMits.Callouts;

// The one format both producers write: the live recorder and the log importer.
public static class EventLog
{
    public const string Magic = "fmrec";
    public const int Version = 1;

    private const char Sep = TextFields.Sep;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(TextWriter w, IEnumerable<GameEvent> events)
    {
        w.WriteLine($"{Magic} {Version}");
        foreach (var e in events)
            w.WriteLine(Format(e));
    }

    // Streams, so a long pull never has to sit in memory twice.
    public static IEnumerable<GameEvent> Read(TextReader r)
    {
        var header = r.ReadLine() ?? throw new InvalidDataException("Empty recording.");
        var parts = header.Split(' ');
        if (parts.Length != 2 || parts[0] != Magic)
            throw new InvalidDataException($"Not a recording: '{header}'.");
        if (!int.TryParse(parts[1], NumberStyles.Integer, Inv, out var v) || v != Version)
            throw new InvalidDataException($"Recording version {parts[1]}, expected {Version}.");

        while (r.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            yield return Parse(line);
        }
    }

    public static string Format(GameEvent e)
    {
        var sb = new StringBuilder(160);
        sb.Append((int)e.Kind).Append(Sep);
        sb.Append(Num(e.Time)).Append(Sep);
        sb.Append(e.Id.ToString("X", Inv)).Append(Sep);
        sb.Append(TextFields.Escape(e.Name)).Append(Sep);
        sb.Append(Num(e.Value)).Append(Sep);
        sb.Append(e.Extra.ToString("X", Inv)).Append(Sep);
        sb.Append(e.Flags.ToString("X", Inv));
        AppendActor(sb, e.Source);
        AppendActor(sb, e.Target);
        return sb.ToString();
    }

    public static GameEvent Parse(string line)
    {
        var f = line.Split(Sep);
        if (f.Length < 21) throw new InvalidDataException($"Short event line: '{line}'.");
        return new GameEvent
        {
            Kind = (EventKind)int.Parse(f[0], Inv),
            Time = Val(f[1]),
            Id = uint.Parse(f[2], NumberStyles.HexNumber, Inv),
            Name = TextFields.Unescape(f[3]),
            Value = Val(f[4]),
            Extra = uint.Parse(f[5], NumberStyles.HexNumber, Inv),
            Flags = uint.Parse(f[6], NumberStyles.HexNumber, Inv),
            Source = ReadActor(f, 7),
            Target = ReadActor(f, 14),
        };
    }

    private static void AppendActor(StringBuilder sb, Actor a)
    {
        sb.Append(Sep).Append(a.Id.ToString("X", Inv));
        sb.Append(Sep).Append(TextFields.Escape(a.Name));
        sb.Append(Sep).Append(a.NameId.ToString("X", Inv));
        sb.Append(Sep).Append(Num(a.At.X));
        sb.Append(Sep).Append(Num(a.At.Y));
        sb.Append(Sep).Append(Num(a.At.Z));
        sb.Append(Sep).Append(Num(a.Heading));
    }

    private static Actor ReadActor(string[] f, int i) => new(
        uint.Parse(f[i], NumberStyles.HexNumber, Inv),
        TextFields.Unescape(f[i + 1]),
        uint.Parse(f[i + 2], NumberStyles.HexNumber, Inv),
        new Spot(Val(f[i + 3]), Val(f[i + 4]), Val(f[i + 5])),
        Val(f[i + 6]));

    private static string Num(float v) => v.ToString("R", Inv);

    private static float Val(string s) => float.Parse(s, NumberStyles.Float, Inv);

}
