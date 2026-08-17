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

    // The calls that come off the timeline rather than off an event, for the
    // mechanics whose own cast bar is shorter than the warning is worth.
    private TimelineCaller _ahead;

    private TriggerEngine _engine;
    private uint _territory;
    private double _lastPartyPoll = -99;

    // The last seat table and the last replay speed written down, so both are
    // recorded when they change rather than on every frame that agrees with the one
    // before it.
    private string _seats = "";
    private float _speedNoted = -1;

    public Runner(AlertBoard board)
    {
        _board = board;
        _territory = Service.ClientState.TerritoryType;
        _engine = _fights.Build(_territory);
        _engine.Diary = Diary;
        _clock = _timelines.Build(_territory);
        _ahead = new TimelineCaller((ushort)_territory);
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

    // The seat to read the calls as, when the game cannot say. Empty is the normal
    // answer and means work it out from the party.
    //
    // Read on every party poll rather than held, so setting it takes effect within
    // two seconds instead of at the next zone: it is set mid-replay, by somebody who
    // has just heard a call name the wrong person.
    public Func<string>? Seating { get; set; }

    private string Seat => Seating?.Invoke() ?? "";

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

    // Where the recording says it is, and what the calls are timing against. Shown
    // side by side because a stuck clock looks exactly like a quiet fight from the
    // outside: no call counts down, none of them clear, and the board holds whatever
    // arrived first.
    public double ClockSeconds => _sources.Now;

    // What the game says it is simulating at: 1 normal, 0 paused, 4 fast forwarding.
    // Shown because it is the whole input to the replay clock now, so a clock that
    // is not moving and a speed of zero are the same sentence.
    public float ReplaySpeed => Replay.Speed;

    // The clock everything is timed against: the wall normally, the recording's own
    // position in a replay. Public so the screen counts in the same seconds the
    // engine does, or a call would age out while a paused replay sat still.
    public double Now => _sources.Now;

    // Off, and stays off, on this machine only, and never produces a call.
    public MarkerProbe Markers { get; } = new();

    // What the engine saw and what it did about it, when somebody switches it on.
    // Off in every shipped build, and it changes nothing about what is called.
    public Diary Diary { get; } = new();

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

    // ---- the recording ----

    // Starts a fresh one and writes down where it is starting from. Everything in
    // the header is the sort of thing that is obvious while you are sitting there
    // and impossible to recover from the file afterwards.
    public void OpenDiary()
    {
        Diary.On = true;
        Diary.Forget();
        _seats = "";
        _speedNoted = -1;
        _lastPartyPoll = -99;
        NoteTheRun();
    }

    // Called at both ends of a pull, because half of these change during one: a
    // parser can start reading, a timeline can anchor, a replay can be sped up.
    private void NoteTheRun()
    {
        Diary.Note("fight",
            $"{Fight}, territory {_territory}, {SpeakingCount} of {TriggerCount} speaking");
        Diary.Note("replay", InReplay ? $"yes, at {ReplaySpeed:0.##}x" : "no");
        Diary.Note("clock", $"{Now:F1}s");
        Diary.Note("parser",
            ParserReading ? $"reading, {ParserEventsSeen} events, {ParserDropped} dropped"
            : ParserConnected ? "connected, not reading yet"
            : "not there, so statuses come off the party poll only");
        Diary.Note("hooks",
            $"control {(ControlAvailable ? "on" : "off")}, "
            + $"hits {(AbilitiesAvailable ? "on" : "off")}, "
            + $"{MarkersSeen} head markers and {TethersSeen} tethers seen");
        Diary.Note("timeline",
            HasTimeline
                ? $"{TimelineMechanics} mechanics, "
                  + (TimelineRunning
                      ? $"anchored at {TimelineAt:0}s, {TimelineResyncs} resyncs"
                      : "not anchored")
                : "none for this fight");
        Diary.Note("pull", InPull ? $"running, pull {Pulls}" : $"not in one, {Pulls} so far");
    }

    // Where the last section went, so the window can offer the folder without
    // holding its own copy. Kept when the recorder is switched off: the file is
    // still there, and that is the moment somebody wants to open it.
    public string LastRecording { get; private set; } = "";

    // Writes what is held and starts the next section, so there is one per pull.
    public string? WriteDiary()
    {
        if (!Diary.On) return null;

        // A section with a header and no events describes a pull that never
        // happened. Zone changes and reloads reach here too, and writing one of
        // these each time fills the file with blocks holding nothing.
        if (Diary.Events == 0)
        {
            Diary.Forget();
            NoteTheRun();
            return null;
        }

        NoteTheRun();
        var path = DiaryFile.Write(Diary.Render());
        Diary.Forget();
        _seats = "";
        _speedNoted = -1;

        // The next section gets its header now rather than waiting on a pull
        // starting. In a replay the combat flag may never fire, and a section with
        // no header is one with no seats line and no replay line, which are the two
        // facts the whole recording exists to carry.
        NoteTheRun();

        if (path is { Length: > 0 }) LastRecording = path;
        return path;
    }

    public void CloseDiary()
    {
        Diary.On = false;
        Diary.Forget();
    }

    private void OnFrame(Dalamud.Plugin.Services.IFramework framework)
    {
        foreach (var e in _sources.Drain()) OnEvent(e);

        // Written down when it changes, because a fight that ages four seconds per
        // second and a fight whose clock is stuck look identical in a list of call
        // times. This is the line that tells them apart.
        if (Diary.On && InReplay && Math.Abs(ReplaySpeed - _speedNoted) > 0.01f)
        {
            _speedNoted = ReplaySpeed;
            Diary.Note("speed", $"{_speedNoted:0.##}x at clock {Now:F1}s");
        }

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

        // A pull that starts closes the last one's section, so a night of attempts
        // reads as one block each rather than as a single unbroken file. The next
        // section's header is written by WriteDiary itself.
        if (e.Kind == EventKind.CombatStart && Diary.On) WriteDiary();

        // The dropped count belongs to one pull. Nothing cleared it, so a single
        // crowded moment left "Dropped 3" on the status line for the rest of the
        // night, across every pull after it and every zone, describing something
        // that happened hours ago.
        if (e.Kind == EventKind.CombatStart) _board.ResetDropped();

        // Same reason: what the timeline already said belongs to the pull that said
        // it. Without this the second attempt of the night is silent for every
        // mechanic the first one got through.
        if (e.Kind == EventKind.CombatStart) _ahead.Forget();

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

        // What the timeline says is nearly here, before the event's own calls: a
        // warning counted back from a mechanic is always the earlier of the two.
        foreach (var call in _ahead.Due(_clock, e.Time))
        {
            // These are not triggers, so switching one off on the fight page cannot
            // reach them the way it reaches the rest: the page turns a trigger off
            // by not building it, and there is no trigger here to leave out.
            if (Switched?.Invoke(call.Key) is false) continue;
            if (_board.Show(call, e.Time, CallIcon.None)) Voice.Say(call.Spoken);
        }

        foreach (var call in _engine.Feed(e))
        {
            // The debuff or head marker that caused the call, drawn beside it, so a
            // glance says which of the two you got without reading the line.
            // Only what reached the board is spoken, or a fight turned off, or the
            // whole plugin turned off, would go quiet on screen and keep talking.
            if (_board.Show(call, e.Time, CallIcon.For(e, _engine.Player.MyId)))
                Voice.Say(call.Spoken);
        }
    }

    // The fight changes before the engine sees the event, or the first mechanic of
    // the pull is read by the previous fight's triggers.
    private void LeaveFight(uint territory)
    {
        WriteProbe();
        WriteDiary();
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
        WriteDiary();
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
        // Rebuilt engines are new objects, so the recorder has to be handed over or
        // it stops writing the moment somebody changes a strat mid-session.
        _engine.Diary = Diary;
        // A fresh clock rather than a reset one, for the same reason as the engine:
        // a pull can never inherit the last fight's anchors.
        _clock = _timelines.Build(_territory);
        _ahead = new TimelineCaller((ushort)_territory);
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
            var seat = _engine.Party.SlotOf(me.EntityId);

            // Said by hand, when the game cannot say. A replay has no party list, so
            // the object table stands in for it and this player is always first
            // among the eight: read as MT, H1, M1 or R1 and never as the second of
            // the role. Told which seat, the party is re-labelled around it so the
            // other half of every pair lands on the right person too.
            if (Seat is { Length: > 0 } mine && mine != seat)
            {
                _engine.Party.Swap(mine, me.EntityId);
                seat = mine;
            }

            _engine.Player.MySlot = seat;
        }

        // Names come from the client, so a call can say who rather than which id.
        foreach (var (id, _) in members)
        {
            var obj = Service.ObjectTable.SearchByEntityId(id);
            if (obj is not null) _engine.Actors.Remember(id, obj.Name.TextValue);
        }

        if (Diary.On) NoteSeats(members);
    }

    // Who the engine thinks is sitting where, and where that came from.
    //
    // The second half is the point. A party list is in party order, which is the
    // order the seat names mean. The object table is in the order the game happens
    // to hold actors, and the local player is always first in it, so off a stand-in
    // this player is always the first of their role and never the second. A call
    // that says the wrong half of a pair looks like a broken trigger and is not one.
    private void NoteSeats(IReadOnlyList<(uint EntityId, uint JobId)> members)
    {
        var seats = string.Join(" ", members
            .Select(m => (Slot: _engine.Party.SlotOf(m.EntityId), m.EntityId, m.JobId))
            .Where(s => s.Slot.Length > 0)
            .OrderBy(s => s.Slot, StringComparer.Ordinal)
            .Select(s => $"{s.Slot}={s.EntityId:X}/job{s.JobId}"));

        var line =
            $"you are {(_engine.Player.MySlot.Length > 0 ? _engine.Player.MySlot : "unseated")}"
            + $", {members.Count} read from "
            + (Watchers.StandIn()
                ? "the object table, which has no seat order"
                : "the party list")
            + $" | {seats}";

        if (line == _seats) return;
        _seats = line;
        Diary.Note("seats", line);
    }

    public void Dispose()
    {
        // Before the sources go, or a pull that was still running when the plugin
        // was reloaded takes its recording with it.
        WriteDiary();
        Service.Framework.Update -= OnFrame;
        _sources.Dispose();
        Voice.Dispose();
        LocalVoice.Dispose();
        _board.Clear();
    }
}
