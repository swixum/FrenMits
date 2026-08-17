namespace FrenAlerts.Engine;

public sealed class TriggerEngine
{
    private readonly List<Trigger> _triggers = [];
    private readonly List<SequenceTrigger> _sequences = [];

    // Which triggers matched this event and declined it, held until the end of the
    // event so the recording can tell "a collector was busy" from "nothing here
    // could answer". Reused rather than allocated per event, because this is on the
    // path every status and every marker in the fight goes down.
    private readonly List<string> _quiet = [];

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

    // Set to record what this engine saw and what it did about it. Null is the
    // shipped state: nothing is written unless somebody switches it on.
    public Diary? Diary { get; set; }

    public IEnumerable<Call> Feed(in GameEvent e)
    {
        Actors.Note(e);

        // Leaving the fight clears everything, including who was in the party.
        if (e.Kind == EventKind.ZoneChange)
        {
            Diary?.Saw(e, "watched");
            Reset();
            return [];
        }

        if (e.Kind is EventKind.CombatStart or EventKind.CombatEnd)
        {
            Diary?.Saw(e, "watched");
            NewPull();
            // Time into the pull is measured from here, not from the first event
            // that happens to arrive afterwards.
            if (e.Kind == EventKind.CombatStart) State.StartAt(e.Time);
            return [];
        }

        State.Note(e);

        var ctx = new TriggerContext(e, Player, Actors, Party, State);
        List<Call>? calls = null;
        _quiet.Clear();

        // The loud kinds earn a line on arrival. The rest earn one the moment
        // something wants them, which lands immediately above that trigger's own
        // line because nothing is written in between.
        //
        // The burst is said out loud on it, because a repeat inside that window
        // never reaches a trigger at all: it is dropped before anything can want it,
        // and without this it reads identically to a mechanic nobody wrote a call
        // for.
        var arrival = State.NewBurst ? "watched" : "watched, same burst as the one above";

        var written = false;
        if (Diary is { On: true } && WorthALine(e))
        {
            Diary.Saw(e, arrival);
            written = true;
        }

        foreach (var t in _triggers)
        {
            if (!t.Matches(ctx)) continue;

            if (Diary is { On: true } && !written)
            {
                Diary.Saw(e, "wanted");
                written = true;
            }

            var call = t.Make(ctx);
            if (call is null)
            {
                if (Diary is { On: true }) _quiet.Add(t.Id);
                continue;
            }

            var passed = Scheduler.Offer(call, out var why);
            if (passed is null)
            {
                Diary?.Dropped(e.Time, t.Id, call, why);
                continue;
            }

            Diary?.Fired(e.Time, t.Id, passed);
            (calls ??= []).Add(passed);
        }

        // Sequences see every event, because they are waiting on one.
        foreach (var s in _sequences)
        {
            var call = s.Step(ctx);
            if (call is null) continue;

            if (Diary is { On: true } && !written)
            {
                Diary.Saw(e, "wanted");
                written = true;
            }

            var passed = Scheduler.Offer(call, out var why);
            if (passed is null)
            {
                Diary?.Dropped(e.Time, s.Id, call, why);
                continue;
            }

            Diary?.Fired(e.Time, s.Id, passed);
            (calls ??= []).Add(passed);
        }

        // Only where the event produced nothing. A collector staying quiet beside a
        // call that fired is the design working; a mechanic where every trigger
        // that matched declined is the thing being looked for.
        if (calls is null) Diary?.Quiet(e.Time, _quiet);

        return calls ?? Enumerable.Empty<Call>();
    }

    // A cast from somebody in the party is a player pressing a button, and eight
    // players pressing buttons for twenty minutes is the whole file. Everything
    // else in the loud set is rare enough to keep whole.
    private bool WorthALine(in GameEvent e) =>
        Diary.Loud.Contains(e.Kind)
        && (e.Kind != EventKind.CastStart || Party.SlotOf(e.SourceId).Length == 0);

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
