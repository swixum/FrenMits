using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FrenMits.Callouts;

// Baked triggers, grouped by the duty they belong to. Tab separated for the
// same reason the recording is: the library reads its own data with no package
// behind it, so importing it into the plugin stays a one line reference.
public static class TriggerPack
{
    public const string Magic = "fmtrig";
    public const int Version = 5;

    private const char Sep = TextFields.Sep;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(TextWriter w, IReadOnlyDictionary<uint, List<Trigger>> byTerritory)
    {
        w.WriteLine($"{Magic} {Version}");
        foreach (var (territory, triggers) in byTerritory)
            foreach (var t in triggers)
                w.WriteLine(Format(territory, t));
    }

    public static Dictionary<uint, List<Trigger>> Read(TextReader r)
    {
        var header = r.ReadLine() ?? throw new InvalidDataException("Empty trigger pack.");
        var parts = header.Split(' ');
        if (parts.Length != 2 || parts[0] != Magic)
            throw new InvalidDataException($"Not a trigger pack: '{header}'.");
        if (!int.TryParse(parts[1], NumberStyles.Integer, Inv, out var v) || v != Version)
            throw new InvalidDataException($"Trigger pack version {parts[1]}, expected {Version}.");

        var packs = new Dictionary<uint, List<Trigger>>();
        while (r.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var (territory, trigger) = Parse(line);
            if (!packs.TryGetValue(territory, out var list)) packs[territory] = list = new List<Trigger>();
            list.Add(trigger);
        }
        return packs;
    }

    public static string Format(uint territory, Trigger t)
    {
        var sb = new StringBuilder(160);
        sb.Append(territory.ToString(Inv)).Append(Sep);
        sb.Append(TextFields.Escape(t.Key)).Append(Sep);
        sb.Append((int)t.On.Kind).Append(Sep);
        sb.Append(t.On.Id.ToString("X", Inv)).Append(Sep);
        sb.Append(TextFields.Escape(t.On.Name)).Append(Sep);
        sb.Append((int)t.On.Source).Append(Sep);
        sb.Append((int)t.On.Target).Append(Sep);
        sb.Append((int)t.Severity).Append(Sep);
        sb.Append(t.Delay.ToString("R", Inv)).Append(Sep);
        sb.Append(t.Duration.ToString("R", Inv)).Append(Sep);
        sb.Append(t.OncePerPull ? '1' : '0').Append(Sep);
        sb.Append(TextFields.Escape(t.Text)).Append(Sep);
        sb.Append(TextFields.Escape(t.Tts)).Append(Sep);
        sb.Append(TextFields.Escape(t.Where)).Append(Sep);
        sb.Append(t.Enabled ? '1' : '0').Append(Sep);
        sb.Append(TextFields.Escape(t.Roles)).Append(Sep);
        sb.Append(t.Suppress.ToString("R", Inv)).Append(Sep);
        sb.Append(TextFields.Escape(t.Jobs));
        return sb.ToString();
    }

    public static (uint Territory, Trigger Trigger) Parse(string line)
    {
        var f = line.Split(Sep);
        if (f.Length < 16) throw new InvalidDataException($"Short trigger line: '{line}'.");

        var trigger = new Trigger
        {
            Key = TextFields.Unescape(f[1]),
            On = new TriggerMatch
            {
                Kind = (EventKind)int.Parse(f[2], Inv),
                Id = uint.Parse(f[3], NumberStyles.HexNumber, Inv),
                Name = TextFields.Unescape(f[4]),
                Source = (ActorScope)int.Parse(f[5], Inv),
                Target = (ActorScope)int.Parse(f[6], Inv),
            },
            Severity = (CallSeverity)int.Parse(f[7], Inv),
            Delay = float.Parse(f[8], NumberStyles.Float, Inv),
            Duration = float.Parse(f[9], NumberStyles.Float, Inv),
            OncePerPull = f[10] == "1",
            Text = TextFields.Unescape(f[11]),
            Tts = TextFields.Unescape(f[12]),
            Where = TextFields.Unescape(f[13]),
            Enabled = f[14] != "0",
            Roles = TextFields.Unescape(f[15]),
            Suppress = f.Length > 16 ? float.Parse(f[16], NumberStyles.Float, Inv) : 0f,
            Jobs = f.Length > 17 ? TextFields.Unescape(f[17]) : "",
        };

        return (uint.Parse(f[0], NumberStyles.Integer, Inv), trigger);
    }
}
