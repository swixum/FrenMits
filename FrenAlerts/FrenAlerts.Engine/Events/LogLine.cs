namespace FrenAlerts.Engine;

// One reader for a parser's log lines, used by the live bridge and by the recording
// reader both.
//
// Two readers of the same format is two chances to read a field differently, and the
// difference only shows as a call that fires on a recording and never in the duty. So
// the field meanings live here once, the offline side adds timestamps and names on
// top, and the live side adds a clock.
public static class LogLine
{
    public const string HeadMarkerKind = "27";

    // Enough fields for the shortest line any kind here reads from.
    private const int Fields = 3;

    // Which kind a line carries, or Unknown for the nine tenths of a pull that is
    // movement, health ticks and chat. One switch on a short string, allocating
    // nothing, because it runs on every line the parser sends.
    public static EventKind KindOf(string type) => type switch
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

    public static GameEvent? Read(IReadOnlyList<string> fields, double time)
    {
        if (fields.Count < Fields) return null;
        return Read(KindOf(fields[0]), fields, time);
    }

    // The kind handed in, for the offline reader that has already looked it up and
    // counted the ones it is refusing.
    public static GameEvent? Read(EventKind kind, IReadOnlyList<string> fields, double time)
    {
        if (fields.Count < Fields) return null;

        switch (kind)
        {
            // 01|ts|zoneId|zoneName|hash
            case EventKind.ZoneChange:
                return new GameEvent { Kind = kind, Time = time, Id = Hex(fields, 2) };

            // 03|ts|id|name|job|lvl|owner|world|worldName|..|..|curHp|maxHp|curMp|maxMp|||x|y|elev|heading|hash
            case EventKind.ActorSpawn:
            case EventKind.ActorDespawn:
            {
                var id = Hex(fields, 2);
                if (id == 0) return null;
                return new GameEvent
                {
                    Kind = kind, Time = time,
                    SourceId = id,
                    Id = Dec(fields, 12),             // max health, which names the boss
                    Source = Pos(fields, 17),
                };
            }

            // 20|ts|src|srcName|action|actionName|tgt|tgtName|castTime|x|y|elev|heading|hash
            case EventKind.CastStart:
            {
                var action = Hex(fields, 4);
                if (action == 0) return null;
                return new GameEvent
                {
                    Kind = kind, Time = time,
                    SourceId = Hex(fields, 2), TargetId = Hex(fields, 6), Id = action,
                    CastTime = Flt(fields, 8),
                    Source = Pos(fields, 9),
                };
            }

            // 21/22|ts|src|srcName|action|actionName|tgt|tgtName|flags|...|tgtPos|...|srcPos|hash
            case EventKind.AbilityHit:
            {
                var action = Hex(fields, 4);
                if (action == 0) return null;
                return new GameEvent
                {
                    Kind = kind, Time = time,
                    SourceId = Hex(fields, 2), TargetId = Hex(fields, 6), Id = action,
                    Target = Pos(fields, 30),
                    Source = Pos(fields, 40),
                };
            }

            // 26/30|ts|status|statusName|duration|src|srcName|tgt|tgtName|stacks|curHp|maxHp|hash
            // Source and target settled by a Dance Partner line: the dancer is
            // field 6 and the partner wearing the status is field 8.
            //
            // The duration is read, never assumed. A trap that arrives at 5, 49 and
            // 68 seconds in one pull was shipped as a constant 49 once already, and
            // the number is the whole call.
            case EventKind.StatusGain:
            case EventKind.StatusLose:
            {
                var status = Hex(fields, 2);
                if (status == 0) return null;
                return new GameEvent
                {
                    Kind = kind, Time = time,
                    Id = status, Duration = Flt(fields, 4),
                    SourceId = Hex(fields, 5), TargetId = Hex(fields, 7),
                    // Stacks, which nothing the client polls reports at all.
                    Param = (ushort)Hex(fields, 9),
                };
            }

            // 27|ts|target|targetName|0000|0000|markerId|target|0000|0000|hash
            // Fields 3 and 8 were identical in all 490 markers of the sample pull,
            // so which one is authoritative is untested, not decided.
            case EventKind.HeadMarker:
            {
                var id = Hex(fields, 6);
                var target = Hex(fields, 2);
                if (id == 0 || target == 0) return null;
                return new GameEvent { Kind = kind, Time = time, Id = id, TargetId = target };
            }

            // 35|ts|src|srcName|tgt|tgtName|0000|0000|tetherId|...|hash
            case EventKind.Tether:
            {
                var id = Hex(fields, 8);
                if (id == 0) return null;
                return new GameEvent
                {
                    Kind = kind, Time = time,
                    SourceId = Hex(fields, 2), TargetId = Hex(fields, 4), Id = id,
                };
            }

            // 257|ts|eventId|state|index|||hash
            // Same three the map effect packet carries, in the same places, or the
            // one trigger would fire off a hook and not off a line.
            case EventKind.MapEffect:
                return new GameEvent
                {
                    Kind = kind, Time = time,
                    SourceId = Hex(fields, 2), Id = Hex(fields, 3), TargetId = Hex(fields, 4),
                };

            default:
                return null;
        }
    }

    private static uint Hex(IReadOnlyList<string> f, int i) =>
        i < f.Count && uint.TryParse(f[i], System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0;

    private static uint Dec(IReadOnlyList<string> f, int i) =>
        i < f.Count && uint.TryParse(f[i], out var v) ? v : 0;

    private static float Flt(IReadOnlyList<string> f, int i) =>
        i < f.Count && float.TryParse(f[i], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;

    private static Position Pos(IReadOnlyList<string> f, int i)
    {
        if (i + 3 >= f.Count) return Position.None;
        if (!float.TryParse(f[i], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var x))
            return Position.None;
        return new Position(x, Flt(f, i + 1), Flt(f, i + 2), Flt(f, i + 3));
    }
}
