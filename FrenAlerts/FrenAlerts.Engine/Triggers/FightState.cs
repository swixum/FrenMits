namespace FrenAlerts.Engine;

public sealed class FightState
{
    public const int TrackedIds = 4096;

    public const double BurstWindow = 0.5;

    private readonly Dictionary<(EventKind Kind, uint Id), int> _counts = new(TrackedIds);
    private readonly Dictionary<(EventKind Kind, uint Id), double> _lastSeen = new(TrackedIds);

    // Whether the event just handled opened a new occurrence rather than continuing
    // the one before it.
    public bool NewBurst { get; private set; } = true;

    private readonly Dictionary<(EventKind Kind, uint Id), int> _phaseOf = [];

    public int Phase { get; private set; } = 1;
    public int Dropped { get; private set; }

    // Seconds since the pull started, updated as events arrive, so a trigger can
    // ask how long a mechanic took without holding its own clock.
    public double Now { get; private set; }

    private double _from;
    private bool _running;

    // Keyed on the kind as well as the id, because ids are only unique within one.
    //
    // Dancing Mad's phase 3 is marked by tether 0054, and 0054 is also the status a
    // Warrior's Bloodbath applies. Keyed on the id alone, a Warrior pressing it in
    // phase 1 moved the fight to phase 3 five minutes early, and since phases only
    // go forward that cleared every occurrence count for the rest of the pull.
    public void LearnPhases(IEnumerable<(EventKind Kind, uint Id, int Phase)> pairs)
    {
        var seen = new Dictionary<(EventKind, uint), int>();
        foreach (var (kind, id, phase) in pairs)
        {
            if (id == 0 || phase <= 0) continue;
            var key = (kind, id);
            if (seen.TryGetValue(key, out var already) && already != phase) { seen[key] = -1; continue; }
            seen.TryAdd(key, phase);
        }
        foreach (var (key, phase) in seen)
            if (phase > 0) _phaseOf[key] = phase;
    }

    public int PhasesKnown => _phaseOf.Count;

    // How many separate things a fight may remember across one pull. A fight uses
    // one of these; the bound is here so a mistake cannot turn it into a leak.
    public const int MemorySlots = 16;

    private readonly Dictionary<Type, object> _memory = new(MemorySlots);

    public int Remembering => _memory.Count;

    // Somewhere for a fight to keep what one call has to tell the next: which
    // towers have spawned, whose turn it is, how many of a thing have gone off.
    //
    // Kept here rather than in the fight's own file because this is what a pull
    // ending clears. State that outlives its pull is the bug where the second pull
    // of the night is called with the first pull's answers.
    public T Remember<T>() where T : class, new()
    {
        if (_memory.TryGetValue(typeof(T), out var had)) return (T)had;
        var made = new T();
        // Past the bound it is handed back unbacked rather than stored: the caller
        // still gets a usable object, and nothing grows without limit.
        if (_memory.Count < MemorySlots) _memory[typeof(T)] = made;
        return made;
    }

    public void StartAt(double time)
    {
        _running = true;
        _from = time;
        Now = 0;
    }

    // How many times this event has fired so far, counting the one being handled.
    //
    // Keyed on the kind as well as the id, because a cast and the ability it becomes
    // share one id: counting them together made every other occurrence a phantom, so
    // "the third cast" arrived on the second one.
    public int Count(EventKind kind, uint id) => _counts.GetValueOrDefault((kind, id));

    // Counts restart at each phase, so a fight can say "the second one this phase"
    // without the number carrying over from before the transition.
    public void Note(in GameEvent e)
    {
        if (!_running)
        {
            _running = true;
            _from = e.Time;
        }
        Now = e.Time - _from;

        if (_phaseOf.TryGetValue((e.Kind, e.Id), out var phase) && phase > Phase)
        {
            Phase = phase;
            _counts.Clear();
            _lastSeen.Clear();
            NewBurst = true;
        }

        NewBurst = true;
        if (e.Id == 0) return;

        var key = (e.Kind, e.Id);

        if (_lastSeen.TryGetValue(key, out var seen) && e.Time - seen <= BurstWindow)
        {
            _lastSeen[key] = e.Time;
            NewBurst = false;
            return;
        }

        if (_counts.Count >= TrackedIds && !_counts.ContainsKey(key))
        {
            Dropped++;
            NewBurst = false;
            return;
        }
        _lastSeen[key] = e.Time;
        _counts[key] = _counts.GetValueOrDefault(key) + 1;
    }

    public void Reset()
    {
        _counts.Clear();
        _lastSeen.Clear();
        _memory.Clear();
        Phase = 1;
        Dropped = 0;
        Now = 0;
        _from = 0;
        _running = false;
        NewBurst = true;
    }
}
