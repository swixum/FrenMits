using FrenAlerts.Engine;

namespace FrenAlerts.DevTools;

public sealed class LogReader
{
    private DateTimeOffset? _origin;

    public Dictionary<string, int> Refused { get; } = [];
    public int Parsed { get; private set; }
    public int Skipped { get; private set; }

    public Dictionary<uint, string> Names { get; } = new(NameLimit);
    private const int NameLimit = 8192;

    public IEnumerable<GameEvent> Read(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var e = ParseLine(line);
            if (e is null) continue;
            Parsed++;
            yield return e.Value;
        }
    }

    public GameEvent? ParseLine(string line)
    {
        var f = line.Split('|');
        if (f.Length < 3) return null;

        // The same table the live bridge reads by, so a recording and a duty cannot
        // disagree about what a line means.
        var kind = LogLine.KindOf(f[0]);
        if (kind == EventKind.Unknown) { Skipped++; return null; }

        NoteNames(f[0], f);

        if (!DateTimeOffset.TryParse(f[1], out var stamp)) { Refuse(f[0]); return null; }
        _origin ??= stamp;
        var time = (stamp - _origin.Value).TotalSeconds;

        // Every field meaning lives in the engine's reader, so this side is the
        // timestamp, the names and the counters and nothing else.
        var e = LogLine.Read(kind, f, time);
        if (e is null) Refuse(f[0]);
        return e;
    }

    private void Refuse(string type) =>
        Refused[type] = Refused.GetValueOrDefault(type) + 1;

    private void NoteNames(string type, string[] f)
    {
        switch (type)
        {
            case "03" or "04" or "27": Pair(f, 2, 3); break;
            case "20" or "21" or "22": Pair(f, 2, 3); Pair(f, 6, 7); break;
            case "26" or "30": Pair(f, 5, 6); Pair(f, 7, 8); break;
            case "35": Pair(f, 2, 3); Pair(f, 4, 5); break;
        }
    }

    private void Pair(string[] f, int idAt, int nameAt)
    {
        if (nameAt >= f.Length) return;
        var name = f[nameAt];
        if (name.Length == 0) return;
        var id = Hex(f, idAt);
        if (id == 0 || Names.ContainsKey(id)) return;
        if (Names.Count >= NameLimit) return;
        Names[id] = name;
    }

    private static uint Hex(string[] f, int i) =>
        i < f.Length && uint.TryParse(f[i], System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0;
}
