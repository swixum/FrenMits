using FrenAlerts.Engine;
using FrenAlerts.Engine.Alerts;

namespace FrenAlerts.Game;

// Holds the engine together at runtime: events in from the sources, calls out to the
// board and the voice.
//
// Everything it does is coordination. What an event is comes from EventSources, what
// a fight is comes from FightLoader, and what a call means comes from the engine, so
// none of the logic needs a game to be checked.
public sealed class Runner : IDisposable
{
    // The party changes rarely, and reading it is a walk over eight members.
    private const double PartyPollSeconds = 2.0;

    private readonly AlertBoard _board;
    private readonly EventSources _sources;
    private readonly FightLoader _fights = new();
    private readonly TimelineLoader _timelines = new();

    // Where this fight is on its own timeline. Null for a fight with none, which is
    // not a fault: a timeline is what lets a call be counted down to rather than
    // announced as it lands, and a fight without one still calls.
    private TimelineClock? _clock;

    private TriggerEngine _engine;
    private uint _territory;
    private double _lastPartyPoll = -99;

    public Runner(AlertBoard board)
    {
        _board = board;
        _territory = Service.ClientState.TerritoryType;
        _engine = _fights.Build(_territory);
        _clock = _timelines.Build(_territory);
        _sources = new EventSources(OnEvent);
        _sources.WatchYells(FightLoader.YellsFor(_territory));
        Voice.Local = LocalVoice;
        Service.Framework.Update += OnFrame;
    }

    public bool Enabled
    {
        get => _sources.Enabled;
        set => _sources.Enabled = value;
    }

    public string Fight => _fights.Fight;

    // Which seat this player is in, for the fight page to read its calls as. Empty
    // outside a party, which is the page's cue to show the plain half of a call.
    public string MySlot => _engine.Player.MySlot;

    public int TriggerCount => _engine.Triggers.Count;

    // How many will actually speak, which is the number that matters: a status line
    // reading only the total would call a fight covered while most of it was off.
    public int SpeakingCount => _engine.Triggers.Count(t => t.Enabled);

    // Rebuilds on assignment, because the host sets this in an object initializer,
    // which runs after the constructor has already built the engine for the zone the
    // player is standing in. Without the rebuild, every call somebody had switched
    // off stayed on until they next changed zone.
    public Func<ushort, string, string>? Strat
    {
        get => _fights.Strat;
        set
        {
            _fights.Strat = value;
            Reload();
        }
    }

    public Func<string, bool?>? Switched
    {
        get => _fights.Switched;
        set
        {
            _fights.Switched = value;
            Reload();
        }
    }

    public bool ControlAvailable => _sources.ControlAvailable;

    public bool AbilitiesAvailable => _sources.AbilitiesAvailable;

    public int AbilitiesSeen => _sources.AbilitiesSeen;

    public int TethersSeen => _sources.TethersSeen;

    public bool ParserConnected => _sources.ParserConnected;

    // Whether head markers are actually arriving, which is not the same as a parser
    // being there: the handshake can still be running, or can have given up.
    public bool ParserReading => _sources.ParserReading;

    public bool ParserAsking => _sources.ParserAsking;

    public int MarkersSeen => _sources.MarkersSeen;

    // How many events have actually arrived from the arena poll: a prop spawning,
    // moving, or turning targetable.
    //
    // Counted rather than taken on trust. LiveCoverage says these come from
    // ArenaEvents and the fight page believed it, so five fights listed calls as
    // fine that could not fire, because nothing constructs that source yet. A count
    // is the only honest answer to whether something is really arriving.
    public int ArenaSeen { get; private set; }

    // What the arena poll is holding and how much it has had to drop.
    public int ArenaTracking => _sources.ArenaTracking;

    public int ArenaDropped => _sources.ArenaDropped;

    // ---- the fight's own clock ----

    // Whether this fight ships a timeline at all.
    public bool HasTimeline => _clock is not null;

    // Whether the clock knows where it is. False until something anchors it, which
    // for a fight written in phase blocks is its first anchor rather than the pull
    // starting: counting down against a clock nobody has placed is worse than saying
    // nothing at all.
    public bool TimelineRunning => _clock is { Running: true };

    // How many times the clock has corrected itself, and by how much on average.
    // Positive drift means the clock was running ahead of the fight.
    public int TimelineResyncs => _clock?.Resyncs ?? 0;

    public double TimelineDrift => _clock?.Drift ?? 0d;

    // Seconds into the fight's own timeline, which is not the same as seconds into
    // the pull: a fight written in phase blocks counts from its block base.
    public double TimelineAt => _clock?.At(Now) ?? 0d;

    // What the fight expects next, soonest first. Empty until the clock is anchored.
    public IEnumerable<Upcoming> Upcoming(int count = 3) =>
        _clock?.Next(Now, count) ?? [];

    public int TimelineMechanics => _timelines.Mechanics(_territory);

    // Seconds since the pull started, on the fight's own clock. Used to give the
    // timeline time to find itself before anybody is told it has not: a fight written
    // in phase blocks is legitimately unanchored for the first stretch of every pull.
    public double PullSeconds => _engine.State.Now;

    // How many of this fight's boss lines the client could name. Fourteen in the
    // Unending Coil, zero everywhere else, and zero in the Coil means the sheet
    // lookup missed and Nael will call nothing.
    public int YellsKnown => _sources.YellsKnown;

    // How many this fight should have found, so the status line can say "14 of 14"
    // rather than a bare number nobody can judge.
    public int YellsExpected => FightLoader.YellsFor(_territory).Count;

    // Whether the client's own reads of the overlapping kinds are standing down
    // because a parser is answering them instead. Not a fault, but it is the answer
    // to "it behaved differently with the parser open".
    public bool ClientReadsStoodDown => _sources.ClientReadsStoodDown;

    public int ParserEventsSeen => _sources.ParserEventsSeen;

    // Non-zero means the frame stopped draining the feed, which is a real fault:
    // those events are gone, not late.
    public int ParserDropped => _sources.ParserDropped;

    // Whether calls that wait on a hit have a source at all. The hook stands down
    // while a parser reads, so the hook being unavailable only silences them when
    // there is no parser to take over.
    public bool HitsCovered => _sources.AbilitiesAvailable || _sources.ClientReadsStoodDown;

    public int Pulls => _sources.Pulls;

    public bool InPull => _sources.InPull;

    public int PlanCalls => _fights.PlanCalls;

    // Which phase the fight is in, and whether it has a phase table at all. Both,
    // because the number alone cannot say whether it means anything: a fight with
    // no table sits at 1 all pull and would read as a phase 1 that never ends.
    public int Phase => _engine.State.Phase;

    public bool PhasesKnown => _engine.State.PhasesKnown > 0;

    // Whether what is being watched is a recording. Worth surfacing: in a replay the
    // clock is the recording's position rather than the wall, and the party comes
    // from the object table rather than a party list, so "it behaved differently"
    // has an answer.
    public bool InReplay => _sources.InReplay;

    // The clock everything is timed against: the wall normally, the recording's own
    // position in a replay. Public so the screen counts in the same seconds the
    // engine does, or a call would age out while a paused replay sat still.
    public double Now => _sources.Now;

    // Off, and stays off, on this machine only, and never produces a call.
    public MarkerProbe Markers { get; } = new();

    // Its own process, so a failure in there falls back to the system voice rather
    // than crashing the game.
    public NeuralVoice LocalVoice { get; } = new(
        Path.Combine(Service.PluginInterface.ConfigDirectory.FullName, "voice"));

    // Says the call out loud, on its own thread.
    public Voice Voice { get; } = new();

    public string LoadPlan()
    {
        var said = _fights.ReadPlan((ushort)_territory);
        Reload();
        return said;
    }

    private void OnFrame(Dalamud.Plugin.Services.IFramework framework)
    {
        foreach (var e in _sources.Drain()) OnEvent(e);

        // Somebody moved the replay slider. Every count, every once-a-pull and every
        // armed sequence describes a stretch of the fight that no longer leads to
        // here, so the fight starts over rather than carrying on from a place it
        // never passed through.
        if (_sources.TakeScrubbed())
        {
            _engine.Reset();
            _board.Clear();
            _lastPartyPoll = -99;
        }

        Markers.Poll(_sources.Now, _territory);
    }

    private void OnEvent(GameEvent e)
    {
        if (e.Kind == EventKind.ZoneChange && e.Id != _territory) LeaveFight(e.Id);
        if (e.Kind == EventKind.CombatEnd) EndPull();

        // The dropped count belongs to one pull. Nothing cleared it, so a single
        // crowded moment left "Dropped 3" on the status line for the rest of the
        // night, across every pull after it and every zone, describing something
        // that happened hours ago.
        if (e.Kind == EventKind.CombatStart) _board.ResetDropped();

        // Counted for the zone, so leaving a fight forgets it: the question is
        // whether the arena is being read here, not whether it ever was.
        if (e.Kind is EventKind.ActorSpawn or EventKind.ActorMoved or EventKind.NameToggle)
            ArenaSeen++;

        // Recorded rather than acted on, because whether head markers ride on a
        // control category is still open and a probe that fed the engine would be a
        // guess wearing a measurement's clothes.
        if (e.Kind == EventKind.ActorControl)
            Markers.NoteControl(e.Time, _territory, e.Id, (uint)e.Duration, e.TargetId);

        RefreshParty(e.Time);

        // Before the engine, so anything reading what is coming next on this frame
        // sees the anchor this event just landed rather than the one before it.
        // Handles the pull's own start itself, so nothing here has to.
        _clock?.Feed(e);

        foreach (var call in _engine.Feed(e))
        {
            // The debuff or head marker that caused the call, drawn beside it, so a
            // glance says which of the two you got without reading the line.
            _board.Show(call, e.Time, CallIcon.For(e, _engine.Player.MyId));
            // Only what reached the board is spoken, or the ones the scheduler
            // dropped for crowding would be read out anyway.
            Voice.Say(call.Spoken);
        }
    }

    // The fight changes before the engine sees the event, or the first mechanic of
    // the pull is read by the previous fight's triggers.
    private void LeaveFight(uint territory)
    {
        WriteProbe();
        _territory = territory;
        ArenaSeen = 0;
        Reload();
        _sources.LeftTheFight();
        _board.Clear();
    }

    // A wipe leaves whatever was mid-mechanic sitting on screen, and the probe's rows
    // belong to the pull they were recorded in rather than to the night.
    private void EndPull()
    {
        WriteProbe();
        _board.Clear();
    }

    private void WriteProbe()
    {
        if (Markers.Enabled) Markers.Write();
        Markers.Forget();
    }

    private void Reload()
    {
        _engine = _fights.Build(_territory);
        // A fresh clock rather than a reset one, for the same reason as the engine:
        // a pull can never inherit the last fight's anchors.
        _clock = _timelines.Build(_territory);
        // Asked the same question the engine was, in the same breath, so the
        // listener can never be watching a fight the engine is not loaded for.
        _sources.WatchYells(FightLoader.YellsFor(_territory));
        _lastPartyPoll = -99;
    }

    private void RefreshParty(double now)
    {
        if (!Paced.Due(now, _lastPartyPoll, PartyPollSeconds)) return;
        _lastPartyPoll = now;

        var members = PartySlots.Read();
        PartySlots.Fill(_engine.Party, members);

        if (PartySlots.Me is { } me)
        {
            _engine.Player.MyId = me.EntityId;
            _engine.Player.MySlot = _engine.Party.SlotOf(me.EntityId);
        }

        // Names come from the client, so a call can say who rather than which id.
        foreach (var (id, _) in members)
        {
            var obj = Service.ObjectTable.SearchByEntityId(id);
            if (obj is not null) _engine.Actors.Remember(id, obj.Name.TextValue);
        }
    }

    public void Dispose()
    {
        Service.Framework.Update -= OnFrame;
        _sources.Dispose();
        Voice.Dispose();
        LocalVoice.Dispose();
        _board.Clear();
    }
}
