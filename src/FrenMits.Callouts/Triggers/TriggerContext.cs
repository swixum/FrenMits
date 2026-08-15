using System.Collections.Generic;

namespace FrenMits.Callouts;

// Everything a fight's own code can see when it decides what to say. Passed by
// reference to the trigger's condition and to whatever works out its words, so
// a mechanic that depends on where the boss is standing, what landed earlier in
// the pull, or which strat the group runs can answer for itself.
public readonly record struct TriggerContext(
    GameEvent Event,
    PlayerContext Me,
    FightState State,
    Arena Arena,
    IReadOnlyDictionary<string, string> Options)
{
    public bool Mine => Me.IsMe(Event.Target);

    public Spot MySpot => Arena.Of(Me.Id);

    public Spot SpotOf(uint actorId) => Arena.Of(actorId);

    // Which way something is from the middle of the room.
    public Way WayOf(Spot at, Ring ring = Ring.Eight) => Arena.Floor.Where(at, ring);

    public Way WayOf(uint actorId, Ring ring = Ring.Eight) => WayOf(Arena.Of(actorId), ring);

    // Which side of a facing actor a spot is on, from that actor's own view.
    public Side SideOf(Actor a, Spot at) => Compass.SideOf(a.At, a.Heading, at);

    // How the group runs this mechanic, or the fallback when nobody chose.
    public string Option(string name, string fallback = "")
        => Options.TryGetValue(name, out var v) && v.Length > 0 ? v : fallback;

    public bool Is(string name, string value)
        => string.Equals(Option(name), value, System.StringComparison.OrdinalIgnoreCase);
}

// What a fight's own code decided to say. Null from a trigger means say nothing
// after all, which is a normal answer for a mechanic that only matters to some
// of the party.
public readonly record struct Say(
    string Text,
    Way Direction = Way.Unknown,
    string Where = "",
    CallSeverity? Severity = null,
    string Tts = "",
    float Delay = 0f,
    float Duration = 0f)
{
    // The common case: a mechanic that just names a place.
    public static Say Go(string text, Way direction) => new(text, direction);

    public static Say Words(string text) => new(text);
}
