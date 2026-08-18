using System.Globalization;
using System.Text;

namespace FrenAlerts.Engine.Scripts;

// An event written out in the shape their triggers read, field for field.
//
// The widths are not ours to choose: every one below is what a real parser writes,
// checked against a recorded raid night, because a field a digit narrow matches
// nothing and a fight then loads, runs and never speaks.
public static class ScriptLines
{
    public static string? Write(GameEvent e, Func<uint, string>? nameOf = null,
        Func<uint, string>? abilityOf = null)
    {
        var name = nameOf ?? (_ => "");
        var ability = abilityOf ?? (_ => "");

        return e.Kind switch
        {
            // 20|ts|sourceId|source|id|ability|targetId|target|castTime|x|y|z|heading
            EventKind.CastStart => Join("20", "",
                Id(e.SourceId), name(e.SourceId), Hex(e.Id), ability(e.Id),
                Id(e.TargetId), name(e.TargetId), Number(e.CastTime),
                Coord(e.Source.X), Coord(e.Source.Y), Coord(e.Source.Elevation),
                Coord(e.Source.Heading)),

            // 21|ts|sourceId|source|id|ability|targetId|target
            EventKind.AbilityHit => Join("21", "",
                Id(e.SourceId), name(e.SourceId), Hex(e.Id), ability(e.Id),
                Id(e.TargetId), name(e.TargetId)),

            // 26|ts|effectId|effect|duration|sourceId|source|targetId|target|count
            EventKind.StatusGain => Join("26", "",
                Hex(e.Id), "", Number(e.Duration),
                Id(e.SourceId), name(e.SourceId), Id(e.TargetId), name(e.TargetId),
                Count(e.Param)),

            // 30|ts|effectId|effect|_|sourceId|source|targetId|target|count
            EventKind.StatusLose => Join("30", "",
                Hex(e.Id), "", "0.00",
                Id(e.SourceId), name(e.SourceId), Id(e.TargetId), name(e.TargetId),
                Count(e.Param)),

            // 27|ts|targetId|target|_|_|id
            EventKind.HeadMarker => Join("27", "",
                Id(e.TargetId), name(e.TargetId), "0000", "0000", Marker(e.Id)),

            // 35|ts|sourceId|source|targetId|target|_|_|id
            EventKind.Tether => Join("35", "",
                Id(e.SourceId), name(e.SourceId), Id(e.TargetId), name(e.TargetId),
                "0000", "0000", Marker(e.Id)),

            // 273|ts|id|category|param1|param2|param3|param4
            EventKind.ActorControl => Join("273", "",
                Id(e.SourceId), Marker(e.Id), Hex(e.Arg1), Hex(e.Arg2), "0", "0"),

            // 03|ts|id|name|_|_|_|_|_|_|npcBaseId|_|_|_|_|_|_|x|y
            EventKind.ActorSpawn => Join("03", "",
                Id(e.SourceId), name(e.SourceId), "", "", "", "", "", "",
                Decimal(e.DataId), "", "", "", "", "", "",
                Coord(e.Source.X), Coord(e.Source.Y)),

            // 34|ts|id|name|targetId|targetName|toggle
            EventKind.NameToggle => Join("34", "",
                Id(e.SourceId), name(e.SourceId), Id(e.TargetId), name(e.TargetId),
                e.Arg1.ToString(CultureInfo.InvariantCulture)),

            // 257|ts|instance|flags|location|data0
            EventKind.MapEffect => Join("257", "",
                Hex(e.SourceId), Hex(e.Id), Hex(e.TargetId), Hex(e.Arg1)),

            // 271|ts|id|heading|_|_|x|y|z, which is the one their position triggers
            // read: nothing anywhere asks for a 270.
            EventKind.ActorMoved => Join("271", "",
                Id(e.SourceId), Coord(e.Source.Heading), "0", "0",
                Coord(e.Source.X), Coord(e.Source.Y), Coord(e.Source.Elevation)),

            // Their own code, for a line no log format writes.
            EventKind.NpcYell => Join("NpcYell", "", Id(e.SourceId), Hex(e.Id)),

            _ => null,
        };
    }

    // A second line the same event answers to, or null where it answers to one.
    //
    // Two of their types are written by the game as a second form of an event we
    // already have, and their fights read whichever form the trigger was written
    // against: Dancing Mad takes four of its six black hole sets off the spawn form
    // and two off the tether, and its trine and crystal directions off the memory
    // form of a spawn.
    public static string? Extra(GameEvent e, Func<uint, string>? nameOf = null) => e.Kind switch
    {
        // 272|ts|id|parentId|tetherId|animationState, the far end first the way both
        // lines write it.
        EventKind.Tether => Join("272", "",
            Id(e.SourceId), Id(e.TargetId), Marker(e.Id), "00"),

        // 261|ts|Add|id|BNpcID|base|PosX|x|PosY|y, which is read by lookahead rather
        // than by position, so every pair carries its own trailing bar.
        EventKind.ActorSpawn => Join("261", "", "Add", Id(e.SourceId),
            "BNpcID", Hex(e.DataId),
            "PosX", Coord(e.Source.X), "PosY", Coord(e.Source.Y), ""),

        _ => null,
    };

    // Every line one event writes, the plain one first.
    public static IEnumerable<string> All(GameEvent e, Func<uint, string>? nameOf = null,
        Func<uint, string>? abilityOf = null)
    {
        if (Write(e, nameOf, abilityOf) is { } line) yield return line;
        if (Extra(e, nameOf) is { } extra) yield return extra;
    }

    // The code a line starts with, which is how a line finds the handful of triggers
    // that could want it instead of all of them.
    public static string? CodeOf(string line)
    {
        var bar = line.IndexOf('|');
        return bar > 0 ? line[..bar] : null;
    }

    private static string Join(string code, params string[] fields)
    {
        var line = new StringBuilder(code);
        foreach (var field in fields) line.Append('|').Append(field);
        return line.ToString();
    }

    // An entity id, eight hex digits, the way every line writes one.
    private static string Id(uint id) => id.ToString("X8", CultureInfo.InvariantCulture);

    // An ability, status or yell id: as many hex digits as it takes.
    private static string Hex(uint value) => value.ToString("X", CultureInfo.InvariantCulture);

    // Markers, tethers and a control category are written four wide, and a trigger
    // asking for `019D` never sees `19D`.
    private static string Marker(uint value) => value.ToString("X4", CultureInfo.InvariantCulture);

    // A status stack count is hex and never narrower than two, which is what the
    // parser writes and what their own `parseInt(count, 16)` expects.
    private static string Count(uint value) => value.ToString("X2", CultureInfo.InvariantCulture);

    // A spawn line is the one place a base id is written in decimal, and their P5
    // towers are matched by the decimal of it.
    private static string Decimal(uint value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Number(float value) =>
        float.IsNaN(value) ? "" : value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Coord(float value) =>
        float.IsNaN(value) ? "" : value.ToString("0.000", CultureInfo.InvariantCulture);
}
