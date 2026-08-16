using System.Collections.Concurrent;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

// Every way an event can reach the engine, behind one drain.
//
// Six sources with three different delivery styles: hooks that queue from the game's
// own threads, polls that read the object table on the frame, and a parser that
// pushes from its thread. Keeping them together is what lets the runner ask for a
// frame's events without knowing which of the three any of them came from.
public sealed class EventSources : IDisposable
{
    private readonly LiveEvents _live;
    private readonly ControlEvents _control;
    private readonly AbilityEvents _abilities;
    private readonly MapEffectEvents _mapEffects;
    private readonly CombatEvents _combat = new();
    private readonly ParserBridge _parser;

    // Filled on the parser's thread, emptied on the frame.
    private readonly ConcurrentQueue<GameEvent> _fromParser = new();

    private readonly ReplayClock _replay = new();
    private readonly System.Diagnostics.Stopwatch _wall = System.Diagnostics.Stopwatch.StartNew();

    // Written once a frame and read from the detour threads. Sixty-four bit and
    // aligned, so a read never sees half of one write.
    private double _now;

    public EventSources(Action<GameEvent> onPoll)
    {
        Tick();
        // All share one clock, so a packet and a cast in the same moment carry the
        // same time and the scheduler can compare them.
        _live = new LiveEvents(onPoll, () => Now);
        _control = new ControlEvents(() => Now);
        _abilities = new AbilityEvents(() => Now);
        _mapEffects = new MapEffectEvents(() => Now);
        _parser = new ParserBridge(_fromParser.Enqueue, () => Now);
    }

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

        if (InReplay)
        {
            _now = _replay.Note(Replay.Position);
            if (_replay.Jumped) Scrubbed = true;
            return;
        }

        _replay.Forget();
        _now = _wall.Elapsed.TotalSeconds;
    }

    public bool TakeScrubbed()
    {
        var was = Scrubbed;
        Scrubbed = false;
        return was;
    }

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

    public int MarkersSeen => _parser.Reported;

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

        foreach (var e in _control.Drain()) yield return e;
        foreach (var e in _abilities.Drain()) yield return e;
        foreach (var e in _mapEffects.Drain()) yield return e;
        while (_fromParser.TryDequeue(out var marker)) yield return marker;

        // Last, so a pull that ends this frame does so after the events that ended it.
        if (_combat.Poll(Now) is { } edge) yield return edge;
    }

    // Leaving the instance, which the combat flag has no way to know about.
    public void LeftTheFight() => _combat.Forget();

    public void Dispose()
    {
        _parser.Dispose();
        _mapEffects.Dispose();
        _abilities.Dispose();
        _control.Dispose();
        _live.Dispose();
    }
}
