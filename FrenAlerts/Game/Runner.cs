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

    // Their fights, for the zones they cover. Where one does, it covers the whole
    // zone: our own triggers, our own timeline and our own counted-down calls are
    // all left out, because two sets of calls for one mechanic talk over each other.
    private readonly ScriptFightHost _scripts = new();

    // Triggers somebody wrote themselves. Run everywhere rather than per fight: half
    // of what they are for is the zones no module covers.
    private readonly UserTriggerHost _mine = new();

    // What the tracked cooldowns are doing. Polled here rather than drawn here: the
    // overlay reads the board and works nothing out.
    private readonly Cooldowns _cooldowns = new();

    // Which hand-written calls are on screen, so a trigger set to wait its turn knows
    // its own is still up. Held here because the board is the only thing that knows.
    private readonly HashSet<string> _liveMine = new(StringComparer.Ordinal);

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
        _scripts.Say = OnScriptCall;
        _scripts.TimelineNote = line => Diary.Note("resync", line);
        _scripts.Chosen = id => ScriptStrat?.Invoke(id) ?? "";
        _mine.Say = OnMyCall;
        Voice.Local = LocalVoice;
        Service.Framework.Update += OnFrame;
    }

    // Which way the group runs each of their fights' choices, asked per pull.
    public Func<string, string>? ScriptStrat { get; set; }

    // Their lines in somebody else's words, asked for as a zone loads and whenever the
    // fight page changes one.
    public Func<IEnumerable<FrenAlerts.Engine.Scripts.ScriptCallEdit>>? ScriptWords
    {
        get => _scripts.Reworded;
        set => _scripts.Reworded = value;
    }

    // A line reworded while a fight is loaded. The pull being played picks it up, because
    // the reason anybody rewords a call is that the last pull proved the words wrong.
    public void ScriptWordsChanged() => _scripts.ApplyEdits();

    // What this zone's imported fights offer a choice on, for the page that sets them.
    public IReadOnlyList<FrenAlerts.Engine.Scripts.ScriptStrategy> ScriptStrategiesFor(ushort zone) =>
        _scripts.StrategiesFor(zone);

    // Whether their fight owns this zone, which is what decides whose calls run.
    public bool Scripted => _scripts.Running;

    // Whether the imported set covers a fight, asked about a zone rather than about
    // where the player is standing: the fight page is read between pulls, from a
    // hub, and the answer is the same either way.
    public bool ScriptCovers(ushort zone) => _scripts.Covers(zone);

    // Whether a noise is allowed out at all, asked the same way the board asks whether
    // a call is: the master switch and this zone's mute. Without it a trigger's sound
    // played through both, because it is played before the call is ever offered to the
    // board and the board is where those two questions live.
    public Func<bool>? Audible { get; set; }

    // Every call the imported set can make there, with the words it says.
    public IReadOnlyList<FrenAlerts.Engine.Scripts.ScriptShownCall> ScriptCallsFor(ushort zone) =>
        _scripts.CallsFor(zone);

    public int ScriptTimelineMechanics(ushort zone) => _scripts.TimelineMechanicsFor(zone);

    // What their side is doing, for the status line: matched is whether the feed is
    // reaching them at all, fired is whether their conditions let anything through.
    public int ScriptMatched => _scripts.Matched;

    public int ScriptFired => _scripts.Fired;

    public string ScriptProblem =>
        _scripts.Problem.Length > 0 ? _scripts.Problem : _scripts.TriggerProblem;

    // The triggers somebody wrote themselves, for the page that edits them.
    public UserTriggerHost Mine => _mine;

    // The cooldown tracker, for the page that edits it and the overlay that draws it.
    public Cooldowns Cooldowns => _cooldowns;

    // Whether hand-written triggers run at all. Set from the config on the frame, so
    // switching it takes effect on the next event rather than the next zone.
    public bool MineEnabled { get; set; } = true;

    public bool Enabled
    {
        get => _sources.Enabled;
        set => _sources.Enabled = value;
    }

    public string Fight => Scripted ? _scripts.Fight : _fights.Fight;

    // Which seat this player is in, for the fight page to read its calls as. Empty
    // outside a party, which is the page's cue to show the plain half of a call.
    public string MySlot => _engine.Player.MySlot;

    // Both sides, because both run.
    //
    // This read "theirs if a fight is scripted, ours otherwise", which is not what the
    // plugin does: a scripted fight still has our own triggers loaded and firing beside
    // their file. Dancing Mad went from "230 of 230" to "162 of 162" in the diary the
    // day it became scripted, and nothing had been lost; 68 of them had simply stopped
    // being counted. It is the same reason phase 4 raidwides were heard twice.
    public int TriggerCount => (Scripted ? _scripts.TriggerCount : 0) + _engine.Triggers.Count;

    // How many will actually speak, which is the number that matters: a status line
    // reading only the total would call a fight covered while most of it was off.
    //
    // Theirs are switched one by one too. The note here used to say they were not, and
    // returned the total for them, so a scripted fight could never show a single call
    // switched off: the two numbers were equal whatever anybody had turned off. Their
    // calls go through the same `Switched` as ours does, three lines further down this
    // file, which is what decides it.
    public int SpeakingCount =>
        (Scripted ? _scripts.SpeakingCount(Switched) : 0)
        + _engine.Triggers.Count(t => t.Enabled && Switched?.Invoke(t.Id) is not false);

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

    public void ParserRetry() => _sources.ParserRetry();

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
    public bool HasTimeline => Scripted ? _scripts.HasTimeline : _clock is not null;

    // Whether the clock knows where it is. False until something anchors it, which
    // for a fight written in phase blocks is its first anchor rather than the pull
    // starting: counting down against a clock nobody has placed is worse than saying
    // nothing at all.
    public bool TimelineRunning =>
        Scripted ? _scripts.TimelineRunning : _clock is { Running: true };

    // How many times the clock has corrected itself, and by how much on average.
    // Positive drift means the clock was running ahead of the fight.
    // Branched on Scripted like the two above it. Read off the hand-written clock
    // alone, this reported no corrections at all for every scripted fight, which
    // read as a clock that had given up and was the reason one was diagnosed.
    public int TimelineResyncs => Scripted ? _scripts.TimelineResyncs : _clock?.Resyncs ?? 0;

    public double TimelineDrift => Scripted ? _scripts.TimelineDrift : _clock?.Drift ?? 0d;

    // Seconds into the fight's own timeline, which is not the same as seconds into
    // the pull: a fight written in phase blocks counts from its block base.
    public double TimelineAt => Scripted ? _scripts.TimelineAt(Now) : _clock?.At(Now) ?? 0d;

    // What the fight expects next, soonest first. Empty until the clock is anchored.
    //
    // The last of these to ask the hand-written clock without checking which one was
    // running. Every fight in the plugin is scripted, so the window's "Next" line had
    // nothing to show in any of them and said only that a timeline was running.
    public IEnumerable<Upcoming> Upcoming(int count = 3) =>
        Scripted ? _scripts.Upcoming(Now, count) : _clock?.Next(Now, count) ?? [];

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
        // The count comes from whichever timeline is actually running. Ours is not
        // built at all where their fight owns the zone, and printing its mechanic
        // count beside their clock's anchor read a whole day of Dancing Mad diaries
        // as "312 mechanics" off a file that was never loaded.
        Diary.Note("timeline",
            HasTimeline
                ? $"{(Scripted ? ScriptTimelineMechanics((ushort)_territory) : TimelineMechanics)} "
                  + "mechanics, "
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
        // Read on the first frame rather than in the constructor: it opens eleven
        // files and parses every one of them, and a plugin that does that while the
        // game waits is a plugin that freezes the moment it updates.
        if (!_scripts.Loaded)
        {
            _scripts.Load();
            _mine.Load();
            Reload();
        }

        foreach (var e in _sources.Drain()) OnEvent(e);

        // After the frame's events, so a call whose delay ran out this frame is said
        // with everything that happened before it already in their state.
        _scripts.Tick(_sources.Now);
        if (MineEnabled) _mine.Tick(_sources.Now);
        NoteMyLiveCalls();
        _cooldowns.Poll(_sources.Now, (ushort)_territory);

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

        // A pull of theirs starts and ends with ours, so their counters are rebuilt
        // from their own initData rather than carrying the last attempt's.
        if (Scripted && e.Kind == EventKind.CombatStart)
        {
            var (me, role, job) = WhoAmI();
            _scripts.StartPull(me, role, job);
        }
        if (Scripted && e.Kind == EventKind.CombatEnd) _scripts.EndPull();

        // Theirs reset on both edges too: a follow-up armed on a pull that wiped is
        // a line said into the next attempt for no reason.
        if (e.Kind is EventKind.CombatStart or EventKind.CombatEnd)
        {
            _mine.Reset();
            _liveMine.Clear();
            _cooldowns.Reset();
        }

        // Their triggers read the event as a line, which is the shape they were
        // written against. Fed after the engine has noted the actor, so a call that
        // asks where the caster is standing has an answer.
        _engine.Actors.Note(e);
        // The picture goes in with the event, because a trigger of theirs can wait
        // seconds before it speaks and by then the event is gone.
        _scripts.Feed(e, FromEnemy(e), CallIcon.For(e, _engine.Player.MyId));
        if (MineEnabled) _mine.Feed(e);
        _cooldowns.Feed(e, _engine.Player.MyId);

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

    // Whether the event came from something that is not in the party, which is what
    // their timeline clock is told: a healer's cast must never drag the fight clock
    // to wherever that ability appears in the file.
    private static bool FromEnemy(in GameEvent e) =>
        e.SourceId != 0 && !Watchers.Watching(e.SourceId);

    // Who their fight thinks it is calling for. Their triggers read all three: the
    // name to compare a target against, the role for the half of a mechanic that is
    // yours, and the job for the lines that name it.
    private (string Me, string Role, string Job) WhoAmI()
    {
        // Read off the object table like everything else here, because a replay has
        // no local player and the first slot is this player in both.
        var me = PartySlots.Me;
        var name = me?.Name.TextValue ?? "";
        var job = me?.ClassJob.ValueNullable?.Abbreviation.ExtractText() ?? "";

        // The seat's role first, because that is the one the party was read into and
        // the one a hand-set seat corrects. The job answers where there is no seat:
        // solo, or in a replay before the party has been read.
        var role = Audience.RoleOf(_engine.Player.MySlot);
        if (role.Length == 0)
            role = Engine.UserTriggers.Jobs.Get(job).Role switch
            {
                Engine.UserTriggers.JobRole.Tank => "tank",
                Engine.UserTriggers.JobRole.Healer => "healer",
                Engine.UserTriggers.JobRole.Dps => "dps",
                _ => "",
            };

        return (name, role, job);
    }

    // One of their calls, on the same board and in the same voice as ours.
    //
    // Through the board rather than straight to the voice, so everything that already
    // decides what is said still decides: the master switch, a muted fight, an edited
    // line, and a call switched off by hand.
    private void OnScriptCall(FrenAlerts.Engine.Scripts.ScriptCall call)
    {
        if (Switched?.Invoke(call.TriggerId) is false)
        {
            Diary.Dropped(Now, call.TriggerId,
                new Call { Text = call.Text, Key = call.TriggerId, Time = Now }, "switched off");
            return;
        }

        var shown = new Call
        {
            Text = call.Text,
            Speech = call.Speech,
            // When the cast lands, where the line that fired this was a cast starting,
            // and now for everything else.
            //
            // This was always Now, on the grounds that their triggers do their own
            // waiting. They do, but the board reads this as "when the mechanic is",
            // and a call that means now has no lead to count down: the seconds in
            // brackets never appeared on a single ported call. It stays silent either
            // way, because the number is put on at draw time and never reaches the
            // voice.
            Time = call.Lands > Now ? call.Lands : Now,
            Key = call.TriggerId,
            Hold = (float)call.Seconds,
            Level = call.Level switch
            {
                FrenAlerts.Engine.Scripts.ScriptCallLevel.Alarm => CallLevel.Alarm,
                FrenAlerts.Engine.Scripts.ScriptCallLevel.Alert => CallLevel.Alert,
                _ => CallLevel.Info,
            },
        };

        // Written down either way, because a wrong call and a call that never came
        // look identical afterwards otherwise. Every fight the port owns went through
        // here and left the diary empty: a whole day of Dancing Mad replays recorded
        // the events and not one of the calls made off them.
        if (_board.Show(shown, Now, call.Icon))
        {
            Diary.Fired(Now, call.TriggerId, shown);
            Voice.Say(shown.Spoken);
        }
        else Diary.Dropped(Now, call.TriggerId, shown, "the board took the other one");
    }

    // Their own keys, kept apart from every other call's so a hand-written trigger
    // can never replace a fight's call by picking the same name.
    private const string MineKey = "mine/";

    // How long a counted-down call stays up after it reaches zero. Long enough to
    // read at the moment it means go, short enough not to sit over the next one.
    private const float CountdownHold = 2f;

    // One call from a trigger somebody wrote.
    private void OnMyCall(Engine.UserTriggers.UserCall call)
    {
        var key = MineKey + call.OwnerId;

        // Their clear rule: the mechanic resolved, so the warning about it goes now
        // rather than sitting out its full time while the fight has moved on.
        if (call.ClearsOwner)
        {
            _board.Drop(key);
            _liveMine.Remove(key);
            _mine.NoteLive(call.OwnerId, false);
            return;
        }

        // Their own sound, before the words: it is the thing that makes somebody
        // look, and a beep after the line has been read is a beep for nothing. Held to
        // the same two questions the board asks, so alerts switched off or a muted
        // fight is silent rather than quietly still beeping.
        if (call.SoundPath.Length > 0 && Audible?.Invoke() != false) Sounds.Play(call.SoundPath);

        // Spoken only, which is a real setting in their editor: the words are for the
        // ears and the screen is left alone.
        if (call.Text.Length == 0)
        {
            if (call.Speech.Length > 0) Voice.Say(call.Speech);
            return;
        }

        // A trigger set to count down means the seconds are the wait, not the time
        // on screen: the board counts to the moment and holds it briefly after.
        // Without this their countdown triggers showed a number that never moved.
        var counting = call.Countdown && call.Seconds > 0.5f;

        var look = _mine.LookOf(call.OwnerId);

        var shown = new Call
        {
            Text = call.Text,
            Speech = call.Speech,
            Time = counting ? Now + call.Seconds : Now,
            Key = key,
            Hold = counting ? CountdownHold : call.Seconds,
            Tint = look.Tint,
            Scale = look.Scale,
            At = look.At,
        };

        // Their icon is a game icon by number, which is what their editor asks for,
        // rather than a head marker: drawn as a marker it came out as a crosshair
        // whatever somebody picked.
        if (_board.Show(shown, Now, CallIcon.Sheet(call.IconId))) Voice.Say(shown.Spoken);
    }

    // Which of their calls are still on screen. A trigger set to wait its turn is
    // asking exactly this, and only the board can answer it.
    private void NoteMyLiveCalls()
    {
        var now = _board.Live()
            .Select(s => s.Call.Key)
            .Where(k => k.StartsWith(MineKey, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var key in _liveMine)
            if (!now.Contains(key)) _mine.NoteLive(key[MineKey.Length..], false);

        foreach (var key in now)
            if (!_liveMine.Contains(key)) _mine.NoteLive(key[MineKey.Length..], true);

        _liveMine.Clear();
        foreach (var key in now) _liveMine.Add(key);
    }

    // The fight changes before the engine sees the event, or the first mechanic of
    // the pull is read by the previous fight's triggers.
    private void LeaveFight(uint territory)
    {
        WriteProbe();
        WriteDiary();
        _territory = territory;
        ArenaSeen = 0;
        // Everything a hand-written trigger was waiting on belonged to the zone being
        // left, down to which of its calls were on screen.
        _mine.Reset();
        _liveMine.Clear();
        _cooldowns.Reset();
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
        // Asked first, because it decides whether any of ours is built at all.
        var theirs = _scripts.Covers((ushort)_territory);

        _engine = _fights.Build(_territory, theirs);
        // Rebuilt engines are new objects, so the recorder has to be handed over or
        // it stops writing the moment somebody changes a strat mid-session.
        _engine.Diary = Diary;
        // A fresh clock rather than a reset one, for the same reason as the engine:
        // a pull can never inherit the last fight's anchors.
        //
        // Not built at all where their fight owns the zone: theirs ships its own
        // timeline, and a second one counting down the same mechanics is the double
        // calling this port exists to end.
        _clock = theirs ? null : _timelines.Build(_territory);
        _ahead = new TimelineCaller(theirs ? (ushort)0 : (ushort)_territory);

        // Their side is pointed at the zone after ours is built, because the actor
        // book their arena reads come from belongs to the engine just built.
        if (theirs)
        {
            var (me, role, job) = WhoAmI();
            _scripts.Enter((ushort)_territory, _engine.Actors, me, role, job);
        }
        else _scripts.Leave();

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

        // Read on the party poll rather than per event: a hand-written trigger asks
        // who somebody is on every single one, and this walks the party to answer.
        _mine.Refresh((ushort)_territory, _engine.Player.MyId, _engine.Party);

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
        _scripts.Dispose();
        // What the night watched happen, before the handles go.
        _mine.Remember();
        Voice.Dispose();
        LocalVoice.Dispose();
        _board.Clear();
    }
}
