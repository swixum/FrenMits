using System.Globalization;
using System.Text;

namespace FrenAlerts.Engine;

public static class EventLog
{
    public const string Header = "fmrec 2";

    // What version 1 wrote, still readable: those recordings are the only proof
    // some of these calls have, and refusing them would throw the evidence away.
    public const string HeaderV1 = "fmrec 1";

    private const int FieldsV1 = 15;

    // Fixed order, and the header carries the version, so an older recording either
    // reads correctly or is refused rather than being silently misread.
    public static string Write(GameEvent e)
    {
        var sb = new StringBuilder(64);
        sb.Append(e.Time.ToString("F3", CultureInfo.InvariantCulture)).Append('\t');
        sb.Append((int)e.Kind).Append('\t');
        sb.Append(e.SourceId.ToString("X")).Append('\t');
        sb.Append(e.TargetId.ToString("X")).Append('\t');
        sb.Append(e.Id.ToString("X")).Append('\t');
        sb.Append(F(e.Duration)).Append('\t');
        sb.Append(F(e.CastTime)).Append('\t');
        Pos(sb, e.Source);
        sb.Append('\t');
        Pos(sb, e.Target);
        // Version 2 adds these three on the end, where a version 1 reader would
        // have ignored them and this one finds them missing without complaining.
        sb.Append('\t').Append(e.DataId.ToString("X"));
        sb.Append('\t').Append(e.Arg1.ToString("X"));
        sb.Append('\t').Append(e.Arg2.ToString("X"));
        sb.Append('\t').Append(e.Param.ToString("X"));
        return sb.ToString();
    }

    public static void WriteAll(TextWriter to, IEnumerable<GameEvent> events)
    {
        to.WriteLine(Header);
        foreach (var e in events) to.WriteLine(Write(e));
    }

    // Null for a line that cannot be read, counted by the caller rather than
    // thrown, so one bad line does not lose the rest of the pull.
    public static GameEvent? Read(string line)
    {
        var f = line.Split('\t');
        if (f.Length < FieldsV1) return null;
        if (!double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var time)) return null;
        if (!int.TryParse(f[1], out var kind)) return null;

        return new GameEvent
        {
            Time = time,
            Kind = (EventKind)kind,
            SourceId = Hex(f[2]),
            TargetId = Hex(f[3]),
            Id = Hex(f[4]),
            Duration = Flt(f[5]),
            CastTime = Flt(f[6]),
            Source = ReadPos(f, 7),
            Target = ReadPos(f, 11),
            DataId = At(f, 15),
            Arg1 = At(f, 16),
            Arg2 = At(f, 17),
            Param = (ushort)At(f, 18),
        };
    }

    // Zero for a version 1 line, which never carried these.
    private static uint At(string[] f, int i) => i < f.Length ? Hex(f[i]) : 0;

    public static IEnumerable<GameEvent> ReadAll(IEnumerable<string> lines)
    {
        var first = true;
        foreach (var line in lines)
        {
            if (first)
            {
                first = false;
                if (line.StartsWith("fmrec", StringComparison.Ordinal))
                {
                    if (line != Header && line != HeaderV1)
                        throw new InvalidDataException($"recording is {line}, this reads {Header}");
                    continue;
                }
            }
            if (line.Length == 0) continue;
            var e = Read(line);
            if (e is not null) yield return e.Value;
        }
    }

    // An unknown position writes as empty fields and reads back as unknown, never
    // as the origin, which is a real spot on every arena.
    private static void Pos(StringBuilder sb, Position p)
    {
        if (!p.Known) { sb.Append("\t\t\t"); return; }
        sb.Append(F(p.X)).Append('\t').Append(F(p.Y)).Append('\t')
          .Append(F(p.Elevation)).Append('\t').Append(F(p.Heading));
    }

    private static Position ReadPos(string[] f, int at) =>
        f[at].Length == 0
            ? Position.None
            : new Position(Flt(f[at]), Flt(f[at + 1]), Flt(f[at + 2]), Flt(f[at + 3]));

    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static uint Hex(string s) =>
        uint.TryParse(s, NumberStyles.HexNumber, null, out var v) ? v : 0;

    private static float Flt(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
}
