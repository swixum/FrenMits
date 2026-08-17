using FrenAlerts.Engine;
using FrenAlerts.Engine.Scripts;

namespace FrenAlerts.Game;

// Their fights, connected to this game.
//
// Everything a fight of theirs needs is now in the engine: the loader that reads
// their files, the runner that compiles and fires their triggers, the reads their
// calls make of the arena, and their timeline clock. None of it runs on its own.
// This is the part that builds them, points them at the zone the player is standing
// in, and turns what they say into a call on the board.
//
// It is also where a zone belongs to one side or the other. Where their fight covers
// the zone, it covers all of it: our own triggers for that fight are not loaded at
// all, because two sets of calls for one mechanic is worse than either alone.
public sealed class ScriptFightHost : IDisposable
{
    // How often the arena snapshot their calls read is rebuilt. Their position calls
    // ask where things are standing, which moves slowly compared to a frame, and
    // rebuilding it per event would run the whole object table through the script
    // engine hundreds of times a pull.
    private const double PositionPace = 0.5;

    // Names their triggers match on, kept because 208 of their triggers name the
    // caster and looking each one up per event is a walk over the object table.
    // Bounded and dropped at every zone, so it can never outlive the fight it
    // describes.
    private const int MaxNames = 4096;

    private readonly ScriptFights _fights = new();
    private readonly List<ScriptTriggerRunner> _runners = [];
    private readonly Dictionary<uint, string> _names = new();

    private IReadOnlyDictionary<string, ScriptTimeline> _timelines =
        new Dictionary<string, ScriptTimeline>();

    private ScriptWorld? _world;
    private ScriptTimelineRuntime? _clock;

    private ushort _zone;
    private double _lastPositions = -99;

    // Where a finished call goes, left to the runner: what a call means on screen is
    // not this file's question.
    public Action<ScriptCall>? Say { get; set; }

    // Which way the group runs each mechanic that has more than one answer, asked
    // per strategy as a pull starts.
    //
    // Asked rather than held, so a change takes effect on the next pull instead of
    // the next reload: somebody sets these while looking at the fight page between
    // attempts, which is the only time anybody ever changes them.
    public Func<string, string>? Chosen { get; set; }

    // Their lines in somebody else's words, asked for rather than held.
    //
    // Asked for the same reason the strategies are: these are edited on the fight page
    // between pulls, and one copy of the answer cannot go stale.
    public Func<IEnumerable<ScriptCallEdit>>? Reworded { get; set; }

    // Ours written into their override hook.
    //
    // Cleared and re-applied whole rather than patched, because the page can put a line
    // back to default and a patch has no way to say so. Called as a zone loads and again
    // the moment anything is reworded, so a change lands on the pull being played instead
    // of at the next reload: somebody rewords a call because the last pull proved the
    // words were wrong.
    public void ApplyEdits()
    {
        if (_runners.Count == 0) return;

        var edits = Reworded?.Invoke() ?? [];
        foreach (var runner in _runners)
        {
            runner.Overrides.ClearWords();
            ScriptCallEdits.Apply(edits, runner.Overrides);
        }
    }

    // What each fight in a zone offers, read once when their files load rather than
    // per pull: it walks every trigger set and their fields do not change.
    private readonly Dictionary<ushort, List<(int Set, string Fight, IReadOnlyList<ScriptStrategy> Strategies)>>
        _strategies = new();

    // Every choice this zone offers, in the order their fights declare them.
    public IReadOnlyList<ScriptStrategy> StrategiesFor(ushort zone)
    {
        if (!_strategies.TryGetValue(zone, out var fights)) return [];

        var all = new List<ScriptStrategy>();
        foreach (var fight in fights) all.AddRange(fight.Strategies);
        return all;
    }

    // Every call the imported set can make in a zone, with the words it can say.
    //
    // Read without standing in the fight, because the page that lists them is read
    // between pulls: a runner is compiled for that zone's sets and thrown away. It is
    // the same compile the pull does, so what this lists is what would speak.
    //
    // Cached per zone. The list is a few hundred entries and building it walks every
    // trigger through the script engine, which is not a thing to do per frame.
    private readonly Dictionary<ushort, List<ScriptShownCall>> _listed = new();

    public IReadOnlyList<ScriptShownCall> CallsFor(ushort zone) =>
        _listed.TryGetValue(zone, out var listed) ? listed : [];

    // Built once, at load, for every zone their set covers.
    //
    // Two ways of asking what a call says, because their files use both. Most write
    // their lines down as output strings; a third of them hand a response builder a
    // set of lines and let it pick, and those only exist once the builder has run. So
    // the builder is run, which needs a pull's worth of state to run against.
    //
    // Done here rather than when a page is opened, because seeding that state is what
    // starting a pull does: at load there is no pull to disturb, and later there
    // might be.
    private void ReadTheCalls()
    {
        _listed.Clear();

        foreach (var zone in _fights.Zones.ToList())
        {
            var listed = new List<ScriptShownCall>();

            try
            {
                // Their own per-pull state, so a builder that reads the role or the
                // party does not throw on the way to its words.
                _fights.StartPull(zone, "Fren Mit", "dps", "SAM");

                var runner = new ScriptTriggerRunner(_fights.Js!);
                runner.Compile(_fights.SetsFor(zone));

                listed.AddRange(ScriptListing.For(runner, _fights));
            }
            catch (Exception ex)
            {
                Service.Log.Warning(ex, $"Fren Alerts: could not list the imported calls for {zone}");
            }

            _listed[zone] = listed;
        }
    }

    // How many mechanics their timeline lists for a zone, for the page to say so the
    // same way ours does. Read off the files rather than off the running clock, so it
    // answers from anywhere rather than only in the fight.
    public int TimelineMechanicsFor(ushort zone)
    {
        var total = 0;
        foreach (var key in _fights.TimelineKeysFor(zone))
            if (_timelines.TryGetValue(key, out var timeline)) total += timeline.Entries.Count;
        return total;
    }

    // Whether their files have been read yet. Read once, on a frame rather than in a
    // constructor: it opens eleven files and parses every one of them, and a plugin
    // that does that while the game waits is a plugin that freezes on update.
    public bool Loaded { get; private set; }

    // What went wrong reading them, if anything, so a fight that is quiet because a
    // file would not parse says so instead of looking like a fight with no calls.
    public string Problem => _fights.Problem ?? "";

    public int FightsLoaded => _fights.FightsLoaded;

    // Whether their set covers a zone at all, which is what decides who owns it.
    public bool Covers(ushort zone) => Loaded && _fights.Knows(zone);

    // Whether their fight is the one actually running right now.
    public bool Running => _runners.Count > 0;

    public string Fight => _fights.NameOf(_zone);

    public int TriggerCount => _runners.Sum(r => r.Triggers.Count);

    // How many of their triggers matched a line and how many of those said
    // something. Both, because they answer different questions: nothing matched is a
    // feed that is not reaching them, matched but nothing said is their own
    // conditions deciding it was not your problem.
    public int Matched => _runners.Sum(r => r.Matched);

    public int Fired => _runners.Sum(r => r.Fired);

    // The first complaint any of their triggers made, for the status line.
    public string TriggerProblem => _runners.Select(r => r.Problem).FirstOrDefault(p => p is not null) ?? "";

    public bool HasTimeline => _clock is not null;

    public bool TimelineRunning => _clock is { Running: true };

    public double TimelineAt(double now) => _clock?.Fight(now) ?? 0d;

    public string TimelineNext =>
        _clock?.Next(NowForNext) is { } entry ? entry.Name : "";

    // Only used to ask the clock what is next, and the clock answers from where it
    // already is, so the moment asked about does not have to be this frame's.
    private double NowForNext => _lastPositions;

    // Reads their files. Everything after this is indexing what they registered.
    public void Load()
    {
        Loaded = true;

        var dir = Service.PluginInterface.AssemblyLocation.Directory?.FullName;
        if (dir is null) return;

        _fights.Load(Path.Combine(dir, "scripts"));
        _timelines = ScriptTimelines.Load(Path.Combine(dir, "timelines"));

        ReadStrategies();
        ReadTheCalls();

        Service.Log.Information(
            $"Fren Alerts: {_fights.FightsLoaded} scripted fights and {_timelines.Count} timelines read, "
            + $"{_strategies.Values.Sum(f => f.Sum(x => x.Strategies.Count))} strategies offered."
            + (Problem.Length > 0 ? $" Problems: {Problem}" : ""));
    }

    // What each fight offers as a choice, read off their own files. Read here rather
    // than per pull, so the fight page can show them before anybody has zoned in.
    private void ReadStrategies()
    {
        _strategies.Clear();
        if (_fights.Js is not { } js) return;

        foreach (var zone in _fights.Zones)
        {
            var fights = new List<(int, string, IReadOnlyList<ScriptStrategy>)>();
            foreach (var set in _fights.SetsFor(zone))
            {
                try { fights.Add((set, _fights.IdOf(set), ScriptStrategies.Read(js, set))); }
                catch (Exception ex) { Service.Log.Warning($"Fren Alerts: zone {zone} strategies, {ex.Message}"); }
            }
            _strategies[zone] = fights;
        }
    }

    // The answers written where their triggers read them: ours where we have a pick,
    // the group's where they have said, and their own default for the rest.
    //
    // Every pull rather than every zone, because their state is rebuilt every pull
    // and the table goes with it.
    private void ApplyStrategies(ushort zone)
    {
        if (_fights.Js is not { } js || !_strategies.TryGetValue(zone, out var fights)) return;

        var chosen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fight in fights)
            foreach (var strategy in fight.Strategies)
                if (Chosen?.Invoke(strategy.Id) is { Length: > 0 } picked) chosen[strategy.Id] = picked;

        foreach (var fight in fights)
        {
            try { ScriptPicks.Apply(js, fight.Set, fight.Fight, chosen); }
            catch (Exception ex) { Service.Log.Warning($"Fren Alerts: strategies for {fight.Fight}, {ex.Message}"); }
        }
    }

    // Points them at a zone. Everything per-pull is rebuilt here rather than reset,
    // for the same reason our own engine is: a pull can never inherit the last one's
    // counters, and a zone can never inherit the last zone's fight.
    public void Enter(ushort zone, ActorBook actors, string me, string role, string job)
    {
        Leave();
        _zone = zone;
        if (!Covers(zone) || _fights.Js is not { } js) return;

        _fights.StartPull(zone, me, role, job);
        ApplyStrategies(zone);

        _world = new ScriptWorld(actors);
        _world.Bind(js, line => Service.Log.Debug($"Fren Alerts script: {line}"));

        // One runner over every fight the zone has, rather than one each. Both halves
        // of a two-file encounter then share the guard against saying the same call
        // twice, which is the whole point of that guard: the same mechanic is written
        // in both files where the phases overlap.
        var runner = new ScriptTriggerRunner(js) { Say = call => Say?.Invoke(call) };
        // Their prelude asks the host for the reworded lines every time it builds one, so
        // the hook has to be in place before a single trigger compiles. Nothing called
        // this outside the tests, which is why every rewording on the fight page changed
        // what the page said and nothing about what the fight called.
        runner.Bind();
        runner.Compile(_fights.SetsFor(zone));
        _runners.Add(runner);
        ApplyEdits();

        // Their own timelines for this zone, all of them: a fight written in two
        // halves has one file each, and the clock is told about both so the second
        // half is somewhere it can move to rather than a fight it has never heard of.
        var mine = _fights.TimelineKeysFor(zone)
            .Select(k => _timelines.GetValueOrDefault(k))
            .OfType<ScriptTimeline>()
            .ToList();

        if (mine.Count > 0)
        {
            _clock = new ScriptTimelineRuntime(mine) { Speak = SayFromTimeline };
            _clock.SetZone(mine[0]);
        }

        Service.Log.Information(
            $"Fren Alerts: {Fight} loaded from their set, {TriggerCount} triggers"
            + (mine.Count > 0 ? $" and {mine.Sum(t => t.Entries.Count)} timeline entries." : ", no timeline."));
    }

    // Leaving the fight. Their per-pull state stays where it is until the next zone
    // builds a new one; what has to go is anything still waiting to be said.
    public void Leave()
    {
        foreach (var runner in _runners) runner.ClearPending();
        _runners.Clear();
        _clock?.Reset();
        _clock = null;
        _world = null;
        _names.Clear();
        _lastPositions = -99;
    }

    // A pull starting. Their state is rebuilt from their own initData, because half
    // of what their triggers hold is a count of something that happened this pull.
    public void StartPull(string me, string role, string job)
    {
        if (!Running) return;

        _fights.StartPull(_zone, me, role, job);
        ApplyStrategies(_zone);
        foreach (var runner in _runners) runner.ClearPending();
        _clock?.Stop();
        _clock?.Engage();
    }

    public void EndPull()
    {
        foreach (var runner in _runners) runner.ClearPending();
        _clock?.Stop();
    }

    // One event, written out as the line their triggers were written against.
    public void Feed(in GameEvent e, bool fromEnemy, FrenAlerts.Engine.Alerts.CallIcon icon = default)
    {
        if (!Running) return;

        Remember(e);
        Positions(e.Time);

        if (ScriptLines.Write(e, NameOf) is { } line)
            foreach (var runner in _runners) runner.Process(line, e.Time, icon);

        if (_clock is { } clock)
        {
            clock.InCombat = true;
            clock.OnEvent(e, fromEnemy);
        }
    }

    // Anything whose delay ran out, and anything the timeline counted down to.
    public void Tick(double now)
    {
        if (!Running) return;

        foreach (var runner in _runners) runner.Tick(now);
        _clock?.Tick(now);
    }

    // Where everything is standing, rebuilt on a pace and only while their fight is
    // the one running.
    private void Positions(double now)
    {
        if (_world is null || _fights.Js is not { } js) return;
        if (!Paced.Due(now, _lastPositions, PositionPace)) return;

        _lastPositions = now;
        try { _world.Remember(js); }
        catch (Exception ex) { Service.Log.Debug($"Fren Alerts: arena read failed, {ex.Message}"); }
    }

    // A timeline line is a call like any other, so it goes out the same door rather
    // than to a second screen nobody configured.
    private void SayFromTimeline(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        Say?.Invoke(new ScriptCall(
            $"timeline/{_zone}/{line}", line, line, ScriptCallLevel.Info, 4.0));
    }

    // Whoever this event was about, by name, because their triggers match the caster
    // by the name the game shows rather than by id.
    private void Remember(in GameEvent e)
    {
        Learn(e.SourceId);
        Learn(e.TargetId);
    }

    private void Learn(uint id)
    {
        if (id == 0 || _names.ContainsKey(id) || _names.Count >= MaxNames) return;

        var obj = Service.ObjectTable.SearchByEntityId(id);
        // Written down either way. A miss is worth remembering too: an id that is not
        // in the table now will not be next event either, and looking it up again on
        // every one of its lines is the walk this exists to avoid.
        _names[id] = obj?.Name.TextValue ?? "";
    }

    private string NameOf(uint id) => _names.GetValueOrDefault(id, "");

    public void Dispose()
    {
        Leave();
        _fights.Dispose();
    }
}
