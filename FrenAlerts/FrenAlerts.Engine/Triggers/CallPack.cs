using System.Globalization;

namespace FrenAlerts.Engine;

public static class CallPack
{
    public const string Header = "facall 3";

    public static string Write(CallSpec s) => string.Join('\t',
    [
        s.Territory.ToString(CultureInfo.InvariantCulture),
        s.Id,
        s.Key,
        ((int)s.On).ToString(CultureInfo.InvariantCulture),
        s.MatchId.ToString("X"),
        ((int)s.Aim).ToString(CultureInfo.InvariantCulture),
        ((int)s.Level).ToString(CultureInfo.InvariantCulture),
        s.Occurrence.ToString(CultureInfo.InvariantCulture),
        s.Phase.ToString(CultureInfo.InvariantCulture),
        s.Delay.ToString("0.###", CultureInfo.InvariantCulture),
        s.Hold.ToString("0.###", CultureInfo.InvariantCulture),
        s.OnlyMe ? "1" : "0",
        s.Personal ? "1" : "0",
        s.For,
        Escape(s.Text),
        Escape(s.Speech),
        ((int)s.From).ToString(CultureInfo.InvariantCulture),
        s.Hush.ToString("0.###", CultureInfo.InvariantCulture),
        s.Once ? "1" : "0",
        s.DefaultOn ? "1" : "0",
        s.Reproduced ? "1" : "0",
    ]);

    public static void WriteAll(TextWriter to, IEnumerable<CallSpec> specs)
    {
        to.WriteLine(Header);
        foreach (var s in specs.Where(s => !s.NeedsWording)) to.WriteLine(Write(s));
    }

    public static CallSpec? Read(string line)
    {
        var f = line.Split('\t');
        if (f.Length < 21) return null;
        if (!ushort.TryParse(f[0], out var territory)) return null;

        return new CallSpec
        {
            Territory = territory,
            Id = f[1],
            Key = f[2],
            On = (EventKind)Int(f[3]),
            MatchId = uint.TryParse(f[4], NumberStyles.HexNumber, null, out var m) ? m : 0,
            Aim = (Aim)Int(f[5]),
            Level = (CallLevel)Int(f[6]),
            Occurrence = Int(f[7]),
            Phase = Int(f[8]),
            Delay = Flt(f[9]),
            Hold = Flt(f[10]),
            OnlyMe = f[11] == "1",
            Personal = f[12] == "1",
            For = f[13],
            Text = Unescape(f[14]),
            Speech = Unescape(f[15]),
            From = (Aim)Int(f[16]),
            Hush = Flt(f[17]),
            Once = f[18] == "1",
            DefaultOn = f[19] == "1",
            Reproduced = f[20] == "1",
        };
    }

    public static IEnumerable<CallSpec> ReadAll(IEnumerable<string> lines)
    {
        var first = true;
        foreach (var line in lines)
        {
            if (first)
            {
                first = false;
                if (line.StartsWith("facall", StringComparison.Ordinal))
                {
                    if (line != Header)
                        throw new InvalidDataException($"call pack is {line}, this reads {Header}");
                    continue;
                }
            }
            if (line.Length == 0) continue;
            var s = Read(line);
            if (s is not null) yield return s;
        }
    }

    // A call's text is the one field a person writes, so a tab or a newline in it
    // must not silently split the row it lives in.
    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "");

    private static string Unescape(string s) =>
        s.Replace("\\t", "\t").Replace("\\n", "\n").Replace("\\\\", "\\");

    private static int Int(string s) => int.TryParse(s, out var v) ? v : 0;

    private static float Flt(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
}
