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

    // Applied to every event, because an id seen mid-pull may never have had a
    // spawn line in this recording at all.
    public void Note(in GameEvent e)
    {
        if (e.SourceId == 0) return;
        var known = Get(e.SourceId);
        var cast = e.Kind is EventKind.CastStart or EventKind.AbilityHit;
        if (known is null)
        {
            Add(new Actor { Id = e.SourceId, HasCast = cast });
            return;
        }
        if (cast && !known.HasCast) _byId[e.SourceId] = known with { HasCast = true };
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
