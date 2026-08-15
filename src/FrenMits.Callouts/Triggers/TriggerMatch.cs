using System;

namespace FrenMits.Callouts;

// Which actor a condition is about.
public enum ActorScope
{
    Anyone = 0,
    Me,
    NotMe,
    Enemy,
    Player,

    // Somebody else in the party. NotMe also matches the boss, which is the
    // wrong answer for "it landed on a person, and that person was not me".
    OtherPlayer,

    // The cast points at nobody: at its own caster, or at an empty slot in a
    // crowd. A call that would name who was hit has nothing to name.
    Untargeted,
}

// What a trigger waits for. Empty and zero mean "do not care", so a trigger
// says only what it needs to.
public sealed record TriggerMatch
{
    public EventKind Kind { get; init; } = EventKind.CastStart;

    // Ability, status, marker or tether id; zero matches any.
    public uint Id { get; init; }

    // Exact name; empty matches any. Ids are better, names read better.
    public string Name { get; init; } = "";

    // The number riding on the event: status stacks, a marker's shape, a
    // director's command. Null matches any, which is almost always right; a
    // fight that hides an answer in there says which one it wants.
    public uint? Param { get; init; }

    public ActorScope Source { get; init; } = ActorScope.Anyone;

    public ActorScope Target { get; init; } = ActorScope.Anyone;

    public bool Matches(GameEvent e, PlayerContext me)
    {
        if (e.Kind != Kind) return false;
        if (Id != 0 && e.Id != Id) return false;
        if (Param is { } want && e.Extra != want) return false;
        if (Name.Length > 0 && !string.Equals(e.Name, Name, StringComparison.Ordinal)) return false;
        return Fits(Source, e.Source, me) && Fits(Target, e.Target, me);
    }

    private static bool Fits(ActorScope scope, Actor a, PlayerContext me) => scope switch
    {
        ActorScope.Anyone => true,
        ActorScope.Me => me.IsMe(a),
        ActorScope.NotMe => a.Known && !me.IsMe(a),
        ActorScope.Enemy => a.Known && !a.IsPlayer,
        ActorScope.Player => a.IsPlayer,
        ActorScope.OtherPlayer => a.IsPlayer && !me.IsMe(a),
        ActorScope.Untargeted => !a.IsPlayer,
        _ => true,
    };
}
