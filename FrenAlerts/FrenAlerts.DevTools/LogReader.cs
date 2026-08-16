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

        var kind = f[0] switch
        {
            "01" => EventKind.ZoneChange,
            "03" => EventKind.ActorSpawn,
            "04" => EventKind.ActorDespawn,
            "20" => EventKind.CastStart,
            "21" or "22" => EventKind.AbilityHit,
            "26" => EventKind.StatusGain,
            "27" => EventKind.HeadMarker,
            "30" => EventKind.StatusLose,
            "35" => EventKind.Tether,
            "257" => EventKind.MapEffect,
            _ => EventKind.Unknown,
        };
        if (kind == EventKind.Unknown) { Skipped++; return null; }

        NoteNames(f[0], f);

        if (!DateTimeOffset.TryParse(f[1], out var stamp)) { Refuse(f[0]); return null; }
        _origin ??= stamp;
        var time = (stamp - _origin.Value).TotalSeconds;

        try
        {
            return kind switch
            {
                // 01|ts|zoneId|zoneName|hash
                EventKind.ZoneChange => new GameEvent { Kind = kind, Time = time, Id = Hex(f, 2) },

                // 03|ts|id|name|job|lvl|owner|world|worldName|..|..|curHp|maxHp|curMp|maxMp|||x|y|elev|heading|hash
                EventKind.ActorSpawn or EventKind.ActorDespawn => new GameEvent
                {
                    Kind = kind, Time = time,
                    SourceId = Hex(f, 2),
                    Id = Dec(f, 12),                  // max health, which names the boss
                    Source = Pos(f, 17),
                },

                // 20|ts|src|srcName|action|actionName|tgt|tgtName|castTime|x|y|elev|heading|hash
                EventKind.CastStart => new GameEvent
                {
                    Kind = kind, Time = time,
                    SourceId = Hex(f, 2), TargetId = Hex(f, 6), Id = Hex(f, 4),
                    CastTime = Flt(f, 8),
                    Source = Pos(f, 9),
                },

                EventKind.AbilityHit => new GameEvent
                {
                    Kind = kind, Time = time,
                    SourceId = Hex(f, 2), TargetId = Hex(f, 6), Id = Hex(f, 4),
                    Target = Pos(f, 30),
                    Source = Pos(f, 40),
                },

                // 26/30|ts|status|statusName|duration|src|srcName|tgt|tgtName|stacks|curHp|maxHp|hash
                // Source and target settled by a Dance Partner line: the dancer is
                // field 6 and the partner wearing the status is field 8.
                EventKind.StatusGain or EventKind.StatusLose => new GameEvent
                {
                    Kind = kind, Time = time,
                    Id = Hex(f, 2), Duration = Flt(f, 4),
                    SourceId = Hex(f, 5), TargetId = Hex(f, 7),
                },

                // 27|ts|target|targetName|0000|0000|markerId|target|0000|0000|hash
                // Fields 3 and 8 were identical in all 490 markers of the sample pull,
                // so which one is authoritative is untested, not decided.
                EventKind.HeadMarker => new GameEvent
                {
                    Kind = kind, Time = time,
                    TargetId = Hex(f, 2), Id = Hex(f, 6),
                },

                // 35|ts|src|srcName|tgt|tgtName|0000|0000|tetherId|...|hash
                EventKind.Tether => new GameEvent
                {
                    Kind = kind, Time = time,
                    SourceId = Hex(f, 2), TargetId = Hex(f, 4), Id = Hex(f, 8),
                },

                // 257|ts|instance|flags|00|||hash
                EventKind.MapEffect => new GameEvent
                {
                    Kind = kind, Time = time,
                    SourceId = Hex(f, 2), Id = Hex(f, 3),
                },

                _ => null,
            };
        }
        catch (IndexOutOfRangeException)
        {
            Refuse(f[0]);
            return null;
        }
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

    private static uint Dec(string[] f, int i) =>
        i < f.Length && uint.TryParse(f[i], out var v) ? v : 0;

    private static float Flt(string[] f, int i) =>
        i < f.Length && float.TryParse(f[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;

    private static Position Pos(string[] f, int i)
    {
        if (i + 3 >= f.Length) return Position.None;
        if (!float.TryParse(f[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x))
            return Position.None;
        return new Position(x, Flt(f, i + 1), Flt(f, i + 2), Flt(f, i + 3));
    }
}
