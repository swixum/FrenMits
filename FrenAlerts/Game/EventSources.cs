using FrenAlerts.Engine;

namespace FrenAlerts.Game;

// Every way an event can reach the engine, behind one drain.
//
// Six sources with three different delivery styles: hooks that queue from the game's
// own threads, polls that read the object table on the frame, and a parser that
// pushes from its thread. Keeping them together is what lets the runner ask for a
// frame's events without knowing which of the three any of them came from.
//
// It is also where the two that overlap are kept apart. A reading parser answers the
// kinds in LiveCoverage.ParserOwned better than the client can, so while one is
// reading the client's own reads of those kinds stand down and come back the moment
// it stops. Both at once would not be a safety net, it would be every call twice.
public sealed class EventSources : IDisposable
{
    private readonly LiveEvents _live;
    private readonly ControlEvents _control;
    private readonly AbilityEvents _abilities;
    private readonly MapEffectEvents _mapEffects;
    private readonly CombatEvents _combat = new();
    private readonly ParserBridge _parser;
    private readonly YellEvents _yells;
    private readonly ArenaEvents _arena;

    // Where the arena's furniture is. Queued rather than emitted straight out, so it
    // passes the same off switch and the same bound as everything else.
    //
    // Sized for a zone's worth of spawns arriving in one poll: entering an instance
    // reports every prop at once, and a bound that a normal zone-in trips would drop
    // the very events the fights need.
    private readonly EventQueue _fromArena = new(max: 2048);

    // Boss lines arrive on the framework thread from the chat service, so they are
    // queued like the hooks' events rather than raised mid-drain. Small: a fight
    // says a handful of lines a pull, not a stream.
    private readonly EventQueue _fromYells = new(max: 64);

    // Filled on the parser's thread, emptied on the frame. Bounded like the hooks'
    // queues are: this used to carry a few dozen markers a pull and now carries every
    // cast, status and hit, so a frame that stops draining is a queue that grows
    // until the night ends.
    private readonly EventQueue _fromParser = new(max: 8192);

    private readonly ReplayClock _replay = new();
    private readonly System.Diagnostics.Stopwatch _wall = System.Diagnostics.Stopwatch.StartNew();

    // Written once a frame and read from the detour threads. Sixty-four bit and
    // aligned, so a read never sees half of one write.
    private double _now;

    // Where the wall was last frame, so a replay can be told how much real time
    // passed and scale it.
    private double _lastWall;

    public EventSources(Action<GameEvent> onPoll)
    {
        Tick();
        // All share one clock, so a packet and a cast in the same moment carry the
        // same time and the scheduler can compare them.
        _live = new LiveEvents(onPoll, () => Now);
        _control = new ControlEvents(() => Now);
        _abilities = new AbilityEvents(() => Now);
        _mapEffects = new MapEffectEvents(() => Now);
        _parser = new ParserBridge(e => _fromParser.Offer(e), () => Now);
        _yells = new YellEvents(e => _fromYells.Offer(e), () => Now);
        _arena = new ArenaEvents(e => _fromArena.Offer(e));
    }

    // Told which boss lines this fight reads as it loads. A fight that names none
    // stops the listener rather than leaving it reading every line in the log.
    public void WatchYells(IReadOnlySet<uint> yellIds) => _yells.Watch(yellIds);

    // How many of the fight's lines the client could name, for the status command.
    public int YellsKnown => _yells.Known;

    public double Now => _now;

    // True when the replay was scrubbed, so the host can start the fight over. Read
    // once and cleared, because it describes a moment rather than a state.
    public bool Scrubbed { get; private set; }

    public bool InReplay { get; private set; }

    // In a replay the clock is the recording's own position rather than the wall.
    // Paused, it stops and nothing ages out. At four times speed, two mechanics two
    // seconds apart stay two seconds apart instead of collapsing into one burst.
    private void Tick()
    {
        InReplay = Replay.InPlayback;

        // The wall keeps running either way, so the step between frames is the same
        // measurement in both branches and a replay only changes what it is worth.
        var wall = _wall.Elapsed.TotalSeconds;
        var step = wall - _lastWall;
        _lastWall = wall;

        if (InReplay)
        {
            _now = _replay.Tick(step, Replay.Speed);
            if (_replay.Jumped) Scrubbed = true;
            return;
        }

        _replay.Forget();
        _now = wall;
    }

    public bool TakeScrubbed()
    {
        var was = Scrubbed;
        Scrubbed = false;
        return was;
    }

    // Off means nothing reaches the engine, not just that the polls stop.
    //
    // It used to mean only the polls, which was nearly the same thing back when the
    // hooks and the parser between them carried a few dozen events a pull. The feed
    // carries the whole fight, so "off" that still let casts, hits and statuses
    // through would be a switch that does not switch anything off.
    public bool Enabled
    {
        get => _live.Enabled;
        set => _live.Enabled = value;
    }

    public bool ControlAvailable => _control.Available;

    public bool AbilitiesAvailable => _abilities.Available;

    public int AbilitiesSeen => _abilities.Reported;

    public int TethersSeen => _live.Tethers.Reported;

    public bool ParserConnected => _parser.Connected;

    // Connected is only half of it: the parser accepts the subscriber first and opens
    // the channel we read from some time after, and it can give up trying. Head
    // markers arrive only once both have happened, so this is what the screen asks.
    public bool ParserReading => _parser.Reading;

    // Still working on it, so the screen can wait rather than call it broken.
    public bool ParserAsking => _parser.Asking;

    public void ParserRetry() => _parser.RetryNow();

    public int MarkersSeen => _parser.MarkersReported;

    // Everything the parser handed over, head markers included, which is the number
    // that says whether the feed is doing anything.
    public int ParserEventsSeen => _parser.Reported;

    public int ParserLinesSeen => _parser.Lines;

    // Non-zero means the frame stopped draining, not that the pull was busy.
    public int ParserDropped => _fromParser.Dropped;

    // True while the client's own reads of the overlapping kinds are standing down.
    public bool ClientReadsStoodDown => _parser.Reading;

    public int Pulls => _combat.Pulls;

    public bool InPull => _combat.InPull;

    // A frame's worth, in the order the sources are asked.
    //
    // Two queues drained one after another can put an ability and a control packet a
    // few milliseconds out of order relative to each other. Everything downstream
    // reasons in tenths of a second at the finest, so sorting every frame would buy
    // nothing.
    public IEnumerable<GameEvent> Drain()
    {
        // First, so everything drained this frame is stamped with one time.
        Tick();
        _parser.Tick(_now);

        // Set on the frame rather than read inside the polls, so one frame's events
        // all come from the same side of the handover.
        // Never in a replay. A parser reads live network traffic, and a recording
        // produces none, so standing the client's own reads down for it would hand
        // the fight to a source that is watching something else entirely: the whole
        // replay goes quiet while the parser reports on the empty room you are
        // actually standing in.
        var fromParser = _parser.Reading && !InReplay;
        _live.Muted = fromParser;

        // The VFX poll was the only tether route there was, and it walks party
        // members only, so a tether between two things that are not in the party was
        // invisible to it. The control packet carries every one with both ends named,
        // so wherever that hook installed, the poll has nothing left to add.
        _live.TethersMuted = _control.Available || fromParser;

        var on = Enabled;

        // The control hook raises three things: the raw packet, which nothing else
        // can see, and head markers and tethers, which a parser also answers. The
        // parser's copies are the measured ones, so while it is reading the hook's
        // are dropped rather than doubled. Off a parser, these are the only route to
        // a head marker there is, which is what makes a bare install work.
        foreach (var e in _control.Drain())
        {
            if (fromParser && LiveCoverage.ParserOwned.Contains(e.Kind)) continue;
            if (on) yield return e;
        }

        // Drained whether it is wanted or not. Standing down is not the same as
        // switching the hook off: leaving it installed means the handover back is
        // instant, and emptying it is what stops a queue filling while nothing reads
        // it. Same reason switched off drains rather than skips.
        foreach (var e in _abilities.Drain())
        {
            if (on && !fromParser) yield return e;
        }

        // Both sources have this one and only the hook's field meanings are proved,
        // so the parser does not forward it and there is nothing to stand down.
        foreach (var e in _mapEffects.Drain())
        {
            if (on) yield return e;
        }

        foreach (var e in _fromParser.Drain())
        {
            if (on) yield return e;
        }

        // No parser copy to stand down for: a yell is not a kind any parser this
        // plugin talks to forwards, so this is the only route there is.
        foreach (var e in _fromYells.Drain())
        {
            if (on) yield return e;
        }

        // Read on the frame, like the other polls. No parser forwards where a prop is
        // standing, so this is the only route to it and nothing stands down.
        //
        // Polled before it is drained so a spawn reaches the engine on the frame it
        // happened: half these calls are about a thing appearing, and a frame's delay
        // is a call that lands after the mechanic it was about.
        _arena.Poll(_now);
        foreach (var e in _fromArena.Drain())
        {
            if (on) yield return e;
        }

        // Last, so a pull that ends this frame does so after the events that ended it.
        // Polled even when off, or the pull it is counting ends while nobody looked
        // and the next one starts mid-fight.
        //
        // A recording is its own pull. The combat flag is read off the client and a
        // replay never sets it: measured across seventeen minutes of a recorded
        // Dancing Mad, it fired exactly zero times. Everything a pull resets was
        // therefore never reset, so occurrence counts, once-a-pull calls and the
        // fight's own clock all carried in from whatever happened before the
        // recording was opened. Opening one is the start, closing it is the end.
        if (_combat.Poll(Now, InReplay) is { } edge && on) yield return edge;
    }

    // What the arena poll is tracking and how much it has said, so the window can
    // report whether props are actually being read rather than assuming they are.
    public int ArenaTracking => _arena.Tracking;

    public int ArenaReported => _arena.Reported;

    public int ArenaDropped => _arena.Dropped;

    // Leaving the instance, which the combat flag has no way to know about.
    //
    // The arena forgets with it: every id it remembers belongs to the zone being
    // left, and holding them would make the next zone's first spawns read as things
    // that had merely moved.
    public void LeftTheFight()
    {
        _combat.Forget();
        _arena.Forget();
    }

    public void Dispose()
    {
        _yells.Dispose();
        _parser.Dispose();
        _mapEffects.Dispose();
        _abilities.Dispose();
        _control.Dispose();
        _live.Dispose();
    }
}
