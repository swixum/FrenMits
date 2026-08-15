using System.Collections.Generic;

namespace FrenMits.Callouts;

// One status as somebody is carrying it right now.
public readonly record struct Held(uint Id, float Remaining, uint Param)
{
    public static readonly Held None = new(0, 0f, 0);

    public bool Present => Id != 0;

    // Some fights hang their answer on how long a debuff runs rather than on
    // which one it is: the same debuff at 20 seconds and at 70 resolves in
    // opposite orders and wants opposite calls.
    public bool Shorter(float than) => Present && Remaining < than;

    public bool Longer(float than) => Present && Remaining >= than;
}

// Who is carrying what, as of the last sweep.
//
// The engine is fed one event at a time, so a trigger only ever sees the one
// thing that just happened. A burst of eight debuffs is eight events, and the
// first of them cannot answer "and what else did I get" without this.
//
// Bounded on both axes and cleared on every pull edge, because it is a mirror
// of live state rather than a history of it.
public sealed class StatusBook
{
    public const int MaxActors = 64;
    public const int MaxPerActor = 60;

    private readonly Dictionary<uint, Dictionary<uint, Held>> _byActor = new();

    public int Actors => _byActor.Count;

    public Held On(uint actorId, uint statusId)
        => actorId != 0
           && _byActor.TryGetValue(actorId, out var held)
           && held.TryGetValue(statusId, out var one)
            ? one : Held.None;

    public bool Any(uint actorId, uint statusId) => On(actorId, statusId).Present;

    // Replaces one actor's whole hand, which is how a sweep reports it. Passing
    // an empty set is how somebody who lost everything is recorded.
    public void Set(uint actorId, IReadOnlyList<Held> held)
    {
        if (actorId == 0) return;
        if (!_byActor.TryGetValue(actorId, out var mine))
        {
            if (_byActor.Count >= MaxActors) return;
            _byActor[actorId] = mine = new Dictionary<uint, Held>();
        }

        mine.Clear();
        for (var i = 0; i < held.Count && i < MaxPerActor; i++) mine[held[i].Id] = held[i];
    }

    public void Forget(uint actorId) => _byActor.Remove(actorId);

    public void Reset() => _byActor.Clear();
}
