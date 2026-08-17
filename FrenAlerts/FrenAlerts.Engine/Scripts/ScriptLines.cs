using System.Globalization;
using System.Text;

namespace FrenAlerts.Engine.Scripts;

// An event written out in the shape their triggers read.
//
// Their fights match a parser's line, field by field by position, and this plugin
// has no parser: casts, statuses, markers and tethers come off the object table and
// the client's own hooks. So the line is written here from what was already seen.
// Nothing is read from a file or a socket; this is the same event the rest of the
// engine gets, spelled the way their regexes expect it.
//
// The field positions are not ours to choose. They are the ones in ScriptNetRegex,
// which are theirs, and the two files have to be read together: a field written one
// place to the left is a trigger that quietly never fires.
public static class ScriptLines
{
    // A line their layouts can match, or null for an event none of their types
    // covers. The timestamp field is left empty, since no trigger reads it and every
    // layout skips over it.
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
                e.Param.ToString(CultureInfo.InvariantCulture)),

            // 30|ts|effectId|effect|_|sourceId|source|targetId|target|count
            EventKind.StatusLose => Join("30", "",
                Hex(e.Id), "", "0.00",
                Id(e.SourceId), name(e.SourceId), Id(e.TargetId), name(e.TargetId),
                e.Param.ToString(CultureInfo.InvariantCulture)),

            // 27|ts|targetId|target|_|_|id
            EventKind.HeadMarker => Join("27", "",
                Id(e.TargetId), name(e.TargetId), "0000", "0000", Marker(e.Id)),

            // 35|ts|sourceId|source|targetId|target|_|_|id
            EventKind.Tether => Join("35", "",
                Id(e.SourceId), name(e.SourceId), Id(e.TargetId), name(e.TargetId),
                "0000", "0000", Marker(e.Id)),

            // 273|ts|id|category|param1|param2|param3|param4
            EventKind.ActorControl => Join("273", "",
                Id(e.SourceId), Hex(e.Id), Hex(e.Arg1), Hex(e.Arg2), "0", "0"),

            // 03|ts|id|name|_|_|_|_|_|_|npcBaseId|_|_|_|_|_|_|x|y
            EventKind.ActorSpawn => Join("03", "",
                Id(e.SourceId), name(e.SourceId), "", "", "", "", "", "",
                Hex(e.DataId), "", "", "", "", "", "",
                Coord(e.Source.X), Coord(e.Source.Y)),

            // 34|ts|id|name|targetId|targetName|toggle
            EventKind.NameToggle => Join("34", "",
                Id(e.SourceId), name(e.SourceId), Id(e.TargetId), name(e.TargetId),
                e.Arg1.ToString(CultureInfo.InvariantCulture)),

            // 257|ts|instance|flags|location|data0. The engine reads the same three
            // the packet carries, in the same places: event id, state, index.
            EventKind.MapEffect => Join("257", "",
                Hex(e.SourceId), Hex(e.Id), Hex(e.TargetId), Hex(e.Arg1)),

            // 270|ts|id|heading|_|moveType|x|y|z
            EventKind.ActorMoved => Join("270", "",
                Id(e.SourceId), Coord(e.Source.Heading), "0", "0",
                Coord(e.Source.X), Coord(e.Source.Y), Coord(e.Source.Elevation)),

            // Their own code, for a line no log format writes.
            EventKind.NpcYell => Join("NpcYell", "", Id(e.SourceId), Hex(e.Id)),

            _ => null,
        };
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

    // An ability, status or yell id: as many hex digits as it takes, which is what
    // their triggers were written against.
    private static string Hex(uint value) => value.ToString("X", CultureInfo.InvariantCulture);

    // Markers and tethers are the exception: their tables write these four wide,
    // zeroes and all, and a trigger asking for `01B5` never sees `1B5`.
    private static string Marker(uint value) => value.ToString("X4", CultureInfo.InvariantCulture);

    private static string Number(float value) =>
        float.IsNaN(value) ? "" : value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Coord(float value) =>
        float.IsNaN(value) ? "" : value.ToString("0.000", CultureInfo.InvariantCulture);
}
