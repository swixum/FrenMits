namespace FrenAlerts.Engine.Scripts;

// One of our events, in the shape their triggers read.
//
// Their fights match a log line's fields and their handlers read those same fields
// back off `matches`. There is no log line here: there is the game's own event, off
// hooks and the object table. This is the mapping, and it is the whole seam.
//
// Hex without an 0x and upper case throughout, because that is how a line writes an
// id and how every one of their netRegex entries is written. A lower case id matches
// nothing, silently, which is a fight that loads and runs and never speaks.
public static class ScriptFields
{
    // Their type name for each of our kinds. The ones with no event behind them are
    // left out: nothing here reads memory, so nothing here can answer a memory read.
    public static string? TypeOf(EventKind kind) => kind switch
    {
        EventKind.CastStart => "StartsUsing",
        EventKind.CastCancel => "StartsUsing",
        EventKind.AbilityHit => "Ability",
        EventKind.StatusGain => "GainsEffect",
        EventKind.StatusLose => "LosesEffect",
        EventKind.HeadMarker => "HeadMarker",
        EventKind.Tether => "Tether",
        EventKind.NpcYell => "NpcYell",
        EventKind.ActorControl => "ActorControlExtra",
        EventKind.ActorSpawn => "AddedCombatant",
        EventKind.ActorMoved => "ActorSetPos",
        EventKind.NameToggle => "NameToggle",
        EventKind.MapEffect => "MapEffect",
        _ => null,
    };

    public static bool Covered(EventKind kind) => TypeOf(kind) is not null;

    // How a line writes a number: hex, upper case, no prefix, no padding.
    public static string Hex(uint value) => value.ToString("X");

    // Markers and tethers are the exception their tables write four wide, so a
    // trigger asking for `01B5` never sees `1B5`.
    public static string Marker(uint value) => value.ToString("X4");

    // The fields one event carries, named the way their code reads them back.
    //
    // Everything is a string, including the numbers, because that is what a parsed
    // line hands them and several of their handlers compare against string literals.
    // The positions are the exception: their own files do arithmetic on those.
    public static Dictionary<string, object?> For(
        in GameEvent e, string sourceName = "", string targetName = "")
    {
        var m = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sourceId"] = Id(e.SourceId),
            ["targetId"] = Id(e.TargetId),
            ["source"] = sourceName,
            ["target"] = targetName,
        };

        switch (e.Kind)
        {
            case EventKind.CastStart:
            case EventKind.CastCancel:
                m["id"] = Hex(e.Id);
                m["castTime"] = e.CastTime;
                break;

            case EventKind.AbilityHit:
                m["id"] = Hex(e.Id);
                break;

            // Their effect triggers key on the id and read the duration to tell one
            // debuff from another, which is most of what a fight hides in them.
            case EventKind.StatusGain:
            case EventKind.StatusLose:
                m["effectId"] = Hex(e.Id);
                m["effect"] = "";
                m["duration"] = e.Duration;
                m["count"] = Hex(e.Param);
                break;

            // A marker is aimed at whoever got it, so the actor it is about is the
            // target. Their files read matches.target for exactly that reason.
            case EventKind.HeadMarker:
                m["id"] = Marker(e.Id);
                break;

            case EventKind.Tether:
                m["id"] = Marker(e.Id);
                break;

            case EventKind.NpcYell:
                m["npcYellId"] = Hex(e.Id);
                break;

            // The raw packet: their handlers read the category and the first argument,
            // so both go across whole rather than rounded.
            case EventKind.ActorControl:
                m["id"] = Id(e.SourceId);
                m["category"] = Hex(e.Id);
                m["param1"] = Hex(e.Arg1);
                m["param2"] = Hex(e.Arg2);
                break;

            case EventKind.ActorSpawn:
                m["id"] = Id(e.SourceId);
                m["name"] = sourceName;
                m["npcBaseId"] = Hex(e.DataId);
                break;

            case EventKind.ActorMoved:
                m["id"] = Id(e.SourceId);
                break;

            case EventKind.NameToggle:
                m["id"] = Id(e.SourceId);
                m["name"] = sourceName;
                m["toggle"] = e.Arg1.ToString();
                break;

            case EventKind.MapEffect:
                m["instance"] = Hex(e.SourceId);
                m["flags"] = Hex(e.Id);
                m["location"] = Hex(e.TargetId);
                break;
        }

        // Where it happened, for the direction calls, and only when the event carried
        // one: a zero would read as the middle of the arena and point half their
        // direction calls at nothing.
        if (e.Source.Known)
        {
            m["x"] = e.Source.X;
            m["y"] = e.Source.Y;
            m["heading"] = e.Source.Heading;
        }
        if (e.Target.Known)
        {
            m["targetX"] = e.Target.X;
            m["targetY"] = e.Target.Y;
        }

        return m;
    }

    // An entity id, eight hex digits, the way every line writes one.
    private static string Id(uint id) => id.ToString("X8");
}
