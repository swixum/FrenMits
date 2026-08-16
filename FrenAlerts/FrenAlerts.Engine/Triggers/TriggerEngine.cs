namespace FrenAlerts.Engine;

public sealed class TriggerEngine
{
    private readonly List<Trigger> _triggers = [];
    private readonly List<SequenceTrigger> _sequences = [];

    public TriggerEngine(PlayerContext? player = null, CallScheduler? scheduler = null)
    {
        Player = player ?? new PlayerContext();
        Scheduler = scheduler ?? new CallScheduler();
    }

    public PlayerContext Player { get; }
    public CallScheduler Scheduler { get; }
    public ActorBook Actors { get; } = new();

    public PartyContext Party { get; } = new();

    // What has happened so far this pull, which is what lets one id mean several
    // different things.
    public FightState State { get; } = new();

    public IReadOnlyList<Trigger> Triggers => _triggers;

    public void Add(Trigger trigger) => _triggers.Add(trigger);
    public void AddRange(IEnumerable<Trigger> triggers) => _triggers.AddRange(triggers);
    public void Add(SequenceTrigger sequence) => _sequences.Add(sequence);
    public void AddRange(IEnumerable<SequenceTrigger> sequences) => _sequences.AddRange(sequences);

    public IEnumerable<Call> Feed(in GameEvent e)
    {
        Actors.Note(e);

        // Leaving the fight clears everything, including who was in the party.
        if (e.Kind == EventKind.ZoneChange)
        {
            Reset();
            return [];
        }

        if (e.Kind is EventKind.CombatStart or EventKind.CombatEnd)
        {
            NewPull();
            // Time into the pull is measured from here, not from the first event
            // that happens to arrive afterwards.
            if (e.Kind == EventKind.CombatStart) State.StartAt(e.Time);
            return [];
        }

        State.Note(e);

        var ctx = new TriggerContext(e, Player, Actors, Party, State);
        List<Call>? calls = null;

        foreach (var t in _triggers)
        {
            if (!t.Matches(ctx)) continue;
            var call = t.Make(ctx);
            if (call is null) continue;
            var passed = Scheduler.Offer(call);
            if (passed is null) continue;
            (calls ??= []).Add(passed);
        }

        // Sequences see every event, because they are waiting on one.
        foreach (var s in _sequences)
        {
            var call = s.Step(ctx);
            if (call is null) continue;
            var passed = Scheduler.Offer(call);
            if (passed is null) continue;
            (calls ??= []).Add(passed);
        }

        return calls ?? Enumerable.Empty<Call>();
    }

    public IEnumerable<Call> Replay(IEnumerable<GameEvent> events)
    {
        foreach (var e in events)
            foreach (var call in Feed(e))
                yield return call;
    }

    public void NewPull()
    {
        // Without this the once-a-pull calls have already said themselves and never
        // speak again, and the pull before this one keeps its calls suppressed into
        // this one.
        Scheduler.Reset();
        State.Reset();
        // An armed sequence left over from before would fire at the first matching
        // follow-up in the new pull, with nothing having started it.
        foreach (var s in _sequences) s.Reset();
        Actors.Reset();
    }

    public void Reset()
    {
        NewPull();
        Party.Reset();
        Actors.ForgetNames();
    }
}
