namespace FrenAlerts.Engine;

public sealed class ActorBook
{
    public const int Capacity = 8192;

    private readonly Dictionary<uint, Actor> _byId = new(Capacity);

    public int Count => _byId.Count;

    public int Dropped { get; private set; }

    public Actor? Get(uint id) => _byId.GetValueOrDefault(id);

    public const int NameLimit = 8192;

    private readonly Dictionary<uint, string> _names = new(NameLimit);

    public int NamesKnown => _names.Count;
    public int NamesDropped { get; private set; }

    public void Remember(uint id, string name)
    {
        if (id == 0 || string.IsNullOrWhiteSpace(name)) return;
        if (_names.ContainsKey(id)) return;
        if (_names.Count >= NameLimit) { NamesDropped++; return; }
        _names[id] = name;
    }

    public void ForgetNames()
    {
        _names.Clear();
        NamesDropped = 0;
    }

    public string ShortName(uint id)
    {
        var name = _names.GetValueOrDefault(id) ?? Get(id)?.Name ?? "";
        if (name.Length == 0) return "";
        var space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }

    public void Add(Actor actor)
    {
        if (_byId.ContainsKey(actor.Id)) { _byId[actor.Id] = actor; return; }
        if (_byId.Count >= Capacity) { Dropped++; return; }
        _byId[actor.Id] = actor;
    }

    // Where an actor is, for the calls that turn a spot into a direction. Unknown
    // rather than the middle when nothing has ever carried one for it.
    public Position Where(uint id) => Get(id)?.Where ?? Position.None;

    public uint DataIdOf(uint id) => Get(id)?.DataId ?? 0;

    // Every actor of one kind that has a position, which is what a mechanic made of
    // several identical props is read from.
    public IEnumerable<Actor> OfKind(uint dataId)
    {
        if (dataId == 0) yield break;
        foreach (var a in _byId.Values)
            if (a.DataId == dataId && a.Where.Known) yield return a;
    }

    // Applied to every event, because an id seen mid-pull may never have had a
    // spawn line in this recording at all.
    public void Note(in GameEvent e)
    {
        if (e.SourceId == 0) return;
        var known = Get(e.SourceId);
        var cast = e.Kind is EventKind.CastStart or EventKind.AbilityHit;

        // A spawn names the kind; everything else only ever carries where it is,
        // so a later event cannot overwrite the kind with nothing.
        var placed = e.Kind is EventKind.ActorSpawn or EventKind.ActorMoved;
        var dataId = placed ? e.DataId : 0;

        if (known is null)
        {
            Add(new Actor
            {
                Id = e.SourceId,
                HasCast = cast,
                DataId = dataId,
                Where = e.Source.Known ? e.Source : Position.None,
            });
            return;
        }

        var next = known;
        if (cast && !next.HasCast) next = next with { HasCast = true };
        if (dataId != 0 && next.DataId == 0) next = next with { DataId = dataId };
        if (e.Source.Known) next = next with { Where = e.Source };
        if (!ReferenceEquals(next, known)) _byId[e.SourceId] = next;
    }

    public Actor? Boss()
    {
        Actor? best = null;
        foreach (var a in _byId.Values)
        {
            if (a.IsPlayer || !a.HasCast || a.MaxHp == 0) continue;
            if (best is null || a.MaxHp > best.MaxHp) best = a;
        }
        return best;
    }

    public void Reset()
    {
        _byId.Clear();
        Dropped = 0;
    }
}
