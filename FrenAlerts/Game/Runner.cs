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

    private TriggerEngine _engine;
    private uint _territory;
    private double _lastPartyPoll = -99;

    public Runner(AlertBoard board)
    {
        _board = board;
        _territory = Service.ClientState.TerritoryType;
        _engine = _fights.Build(_territory);
        _sources = new EventSources(OnEvent);
        Voice.Local = LocalVoice;
        Service.Framework.Update += OnFrame;
    }

    public bool Enabled
    {
        get => _sources.Enabled;
        set => _sources.Enabled = value;
    }

    public string Fight => _fights.Fight;

    public int TriggerCount => _engine.Triggers.Count;

    // How many will actually speak, which is the number that matters: a status line
    // reading only the total would call a fight covered while most of it was off.
    public int SpeakingCount => _engine.Triggers.Count(t => t.Enabled);

    // Rebuilds on assignment, because the host sets this in an object initializer,
    // which runs after the constructor has already built the engine for the zone the
    // player is standing in. Without the rebuild, every call somebody had switched
    // off stayed on until they next changed zone.
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

    public int MarkersSeen => _sources.MarkersSeen;

    public int Pulls => _sources.Pulls;

    public bool InPull => _sources.InPull;

    public int PlanCalls => _fights.PlanCalls;

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

        // Recorded rather than acted on, because whether head markers ride on a
        // control category is still open and a probe that fed the engine would be a
        // guess wearing a measurement's clothes.
        if (e.Kind == EventKind.ActorControl)
            Markers.NoteControl(e.Time, _territory, e.Id, (uint)e.Duration, e.TargetId);

        RefreshParty(e.Time);

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
