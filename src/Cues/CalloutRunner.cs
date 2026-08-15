using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FrenMits.Callouts;

namespace FrenMits.Cues;

// Boss alerts at run time: the game every frame, calls out the other side.
//
// Everything here is read from the object table on the tick the plugin already
// has. No detours, no memory patches, no second process. What that reach costs
// is the events it cannot see: a head marker, a tether and a map effect are not
// on any actor, so those triggers stay quiet until there is a hook for them.
// Four in five calls do not need one.
//
// The engine itself is in FrenMits.Callouts and knows nothing about the game.
// This is the only place the two meet.
public sealed class CalloutRunner : IDisposable
{
    // A duty is eight players and a handful of enemies. The cap is a backstop
    // against an open world zone, not a real limit.
    public const int MaxTracked = 128;

    // Statuses are read for the party only. They are three percent of the calls
    // and the whole object table would be the expensive part of this loop.
    public const int MaxParty = 8;

    private readonly Configuration _config;
    private readonly Audio _audio;
    private readonly Func<AlertBook> _book;

    // A recording of exactly what the plugin saw, for working out afterwards
    // why a call did or did not land. Off unless asked for, written nowhere but
    // this machine, and capped so a long night cannot fill a disk.
    public const int MaxRecorded = 50_000;

    private System.IO.StreamWriter? _record;
    private int _recorded;

    private TriggerEngine? _engine;
    private uint _territory;
    private bool _wasFighting;

    // Fight seconds and the pull boundary. A live pull gets both from the game;
    // a duty recording gets neither, so they are worked out here instead.
    private readonly PullClock _clock = new();
    private double _lastFrame = Now();

    // What each actor was doing last frame, so this frame can tell what changed.
    private readonly Dictionary<ulong, uint> _casting = new();
    private readonly Dictionary<ulong, HashSet<Held>> _statuses = new();

    // Reused per actor per sweep, so a full party costs no allocations.
    private readonly List<Callouts.Held> _hand = new();
    private readonly HashSet<ulong> _seen = new();

    // Drops a repeat, spaces two that land together, and merges a pair that
    // says the same thing. The engine raises calls; this decides what is heard.
    private readonly CallScheduler _scheduler = new();

    // What is on screen right now, newest last. Bounded, because a fight that
    // raised more than this at once has a bug worth seeing rather than hiding.
    public const int MaxOnScreen = 4;

    private readonly List<LiveAlert> _live = new();

    public IReadOnlyList<LiveAlert> Live => _live;

    // A banner asked for from settings outranks every setting that would hide
    // one, because the point of asking is to see it.
    public bool Testing => _testUntil > Clock;

    private float _testUntil;

    // While the Boss Alerts page is on screen, a sample banner stays put so it
    // can be dragged and sized. A banner that leaves after five seconds cannot
    // be placed. The page stamps the frame it drew on; two frames of slack
    // covers the gap between the settings window drawing and the overlay.
    public int PreviewFrame { get; set; } = -100;

    // The duty the page is showing, which is where the sample's art comes from.
    // Standing in a city, the duty you are in has no calls and no icons.
    public uint PreviewDuty { get; set; }

    public bool Placing(int frame) => frame - PreviewFrame <= 2;

    // Real art for the sample, taken from the duty being looked at, so what is
    // placed looks like what a call in that fight will. Worked out once per
    // duty, because it walks the duty's calls to find one that has an icon.
    private uint _sampleIcon;
    private uint _sampleFor = uint.MaxValue;

    public uint SampleIcon(uint territory)
    {
        if (PreviewDuty != 0) territory = PreviewDuty;
        if (territory == _sampleFor) return _sampleIcon;
        _sampleFor = territory;
        _sampleIcon = 0;
        foreach (var a in _book().For(territory))
            if (a.Icon != 0) { _sampleIcon = a.Icon; break; }
        return _sampleIcon;
    }

    // Fight seconds as the overlay counts them, so the countdown and the call
    // that raised it read from one clock.
    public float Clock { get; private set; }

    public CalloutRunner(Configuration config, Audio audio, Func<AlertBook> book)
    {
        _config = config;
        _audio = audio;
        _book = book;
    }

    public bool Running => _engine is not null;

    public int TriggerCount => _engine?.TriggerCount ?? 0;

    // Everything the pack holds for this duty, with the player's own changes
    // laid over it. Built once per duty rather than per frame.
    private List<Trigger> TriggersFor(uint territory)
    {
        var built = new List<Trigger>();
        foreach (var a in _book().For(territory))
        {
            var tweak = _config.BossAlertTweaks.GetValueOrDefault($"{territory}|{a.Key}");
            if (!(tweak?.On ?? a.On)) continue;

            built.Add(new Trigger
            {
                Key = a.Key,
                On = a.Match,
                Text = tweak?.Text ?? a.Text,
                Tts = tweak?.Tts ?? a.Tts,
                Severity = (CallSeverity)(int)(tweak?.Level ?? a.Level),
                Roles = tweak?.Roles ?? a.Roles,
                Jobs = a.Jobs,
                Duration = a.Hold,
                BeforeExpiry = a.Lead,
                Suppress = a.Suppress,
                OncePerPull = a.OncePerPull,
            });
        }
        return built;
    }

    // A duty change is a new fight and a new engine. Leaving one drops it, so
    // an idle zone costs nothing at all.
    public void Enter(uint territory)
    {
        _territory = territory;
        _casting.Clear();
        _statuses.Clear();
        _seen.Clear();

        StopRecording();

        // The pack's rows, and over them whatever this fight has written as
        // code. A written trigger with the same key wins; everything else in
        // the pack keeps working exactly as it did.
        var module = new FightModule { Territory = territory, Triggers = TriggersFor(territory) };
        if (FrenMits.Callouts.Fights.FightBook.For(territory) is { } written)
        {
            // Switched off on the page means off here too. A trigger that only
            // remembers has nothing to switch and always runs, or the calls that
            // read it would go quiet with it.
            var wanted = written.Triggers
                .Where(t => t.About.Length == 0
                            || (_config.BossAlertTweaks.GetValueOrDefault($"{territory}|{t.Key}")?.On ?? true))
                .ToList();
            module = (written with { Triggers = wanted }).Over(module);
        }

        if (module.Count == 0) { _engine = null; return; }

        if (_config.BossAlertsRecord) StartRecording(territory);

        _engine = TriggerEngine.For(module, Me());

        // Shapes and floors are deliberately not handed over, so no call here
        // ever names a direction. The geometry works, but it is measured on one
        // fight: 30 right, 0 wrong, 4 untested, all Dancing Mad. Turning that on
        // for 412 duties on the strength of one is how the guessed cone shipped.
        // When a fight's arena is measured, its floor goes in here and only its
        // calls start pointing.
        _engine.Feed(new GameEvent { Kind = EventKind.Zone, Id = territory });
    }

    public void Reset()
    {
        _live.Clear();
        _engine?.Reset();
        _casting.Clear();
        _statuses.Clear();
        _seen.Clear();
    }

    private static PlayerContext Me()
    {
        var me = Plugin.LocalPlayer;
        if (me is null) return PlayerContext.Unknown;

        var job = me.ClassJob.ValueNullable?.Abbreviation.ExtractText() ?? "";
        return new PlayerContext
        {
            Id = me.EntityId,
            Name = me.Name.TextValue,
            Job = job,
            Role = RoleOf(job),
        };
    }

    private static string RoleOf(string job) => job switch
    {
        "PLD" or "WAR" or "DRK" or "GNB" or "GLA" or "MRD" => "tank",
        "WHM" or "SCH" or "AST" or "SGE" or "CNJ" => "healer",
        "" => "",
        _ => "dps",
    };

    // Called from the plugin's own tick. Everything below runs on the game
    // thread, so it stays a walk over a short list and nothing else.
    //
    // A duty recording is the same fight seen through a different frame: no
    // combat flag ever arrives, and the speed it runs at is the watcher's to
    // set. Both of those are the clock's problem, not this loop's.
    public void Update(bool inCombat, bool playback, float speed)
    {
        // The clock and the sweep of what is on screen come first, and run
        // wherever you are. A duty with no calls still has to clear a banner
        // asked for from the settings page.
        var now = Now();
        _clock.Advance((float)(now - _lastFrame), playback, speed);
        _lastFrame = now;
        Clock = _clock.Time;

        for (var i = _live.Count - 1; i >= 0; i--)
            if (_live[i].Until <= Clock) _live.RemoveAt(i);

        if (!_config.BossAlertsEnabled) return;

        var here = Service.ClientState.TerritoryType;
        if (here != _territory) Enter(here);
        if (_engine is null) return;

        // A chapter skip is a load screen, and everything remembered from
        // before it describes a moment the recording has already left.
        if (playback && Plugin.Loading) { EndPull(); return; }

        // A cutscene empties the arena and puts it back, and every status that
        // comes back reads as freshly applied. None of that is the fight.
        if (Plugin.CutsceneActive) return;

        var fighting = _clock.Fighting(inCombat, playback);

        // With no flag to read, the pull starts when something hostile turns
        // up. Only worth a look while the arena is otherwise quiet, since a
        // running sweep spots its own enemies.
        if (playback && !fighting && SeesEnemy()) fighting = true;

        // A replay that says nothing is the hard one to tell from a replay that
        // is not being watched at all, so each end of it says which.
        if (playback && fighting != _wasFighting)
            Service.Log.Information(fighting
                ? $"[FrenMits] Playback: calls armed in duty {_territory}, {TriggerCount} triggers."
                : $"[FrenMits] Playback: no enemy for {PullClock.QuietSeconds:0}s; calls stood down.");

        if (!fighting) { EndPull(); return; }

        _wasFighting = true;
        _engine.Me = Me();
        Sweep(Clock);
    }

    // ---- what the sweep cannot see ----
    //
    // A head marker and a tether are not on any actor: they arrive as the server
    // telling the client to draw something, and are gone by the next frame. They
    // come in on the recap's actor-control detour, which was already running for
    // deaths, and are handed straight over here.
    //
    // This runs inside packet handling rather than on the tick, so it does the
    // least it can: no object table walk beyond naming the two actors, and
    // nothing at all when there is no fight to tell.

    public void OnMarker(uint actorId, uint markerId, uint pointsAt)
        => FromHook(EventKind.HeadMarker, markerId, actorId, pointsAt);

    public void OnTether(uint actorId, uint tetherId, uint pointsAt)
        => FromHook(EventKind.Tether, tetherId, actorId, pointsAt);

    private void FromHook(EventKind kind, uint id, uint actorId, uint pointsAt)
    {
        if (_engine is null || !_wasFighting) return;

        // The marker's own actor is what a trigger asks about: whether it is on
        // you, on somebody else, or on the boss. One the table has not caught up
        // with still gets raised under its id, so a recording shows the marker
        // rather than a gap where somebody has to guess whether it arrived.
        var on = Find(actorId);
        if (!on.Known) on = new Callouts.Actor(actorId, "", 0, Spot.Nowhere, 0f);

        Raise(new GameEvent
        {
            Kind = kind,
            Time = Clock,
            Id = id,
            Name = on.Name,
            Source = pointsAt != 0 ? Find(pointsAt) : Callouts.Actor.Nobody,
            Target = on,
        });
    }

    // Between pulls: what each actor was doing is about a fight that is over,
    // and a once-per-pull call has to be allowed to happen again.
    private void EndPull()
    {
        if (!_wasFighting) return;
        _engine?.Reset();
        _casting.Clear();
        _statuses.Clear();
        _seen.Clear();
        _clock.Forget();
        _wasFighting = false;
    }

    // A hostile that is up and has hit points, which is the same thing the
    // timeline's own playback watchdog counts.
    private bool SeesEnemy()
    {
        foreach (var obj in Service.ObjectTable)
        {
            try
            {
                if (obj is not IBattleNpc npc) continue;
                if ((byte)npc.BattleNpcKind != 5 || npc.MaxHp == 0 || npc.CurrentHp == 0) continue;
                _clock.SawEnemy();
                return true;
            }
            catch (NullReferenceException) { /* stale actor this frame; next one */ }
        }
        return false;
    }

    private static double Now() => Environment.TickCount64 / 1000.0;

    private void Sweep(float time)
    {
        var live = new HashSet<ulong>();

        foreach (var obj in Service.ObjectTable)
        {
            if (live.Count >= MaxTracked) break;
            if (obj is not IBattleChara chara) continue;
            live.Add(chara.GameObjectId);

            // A pull that is still going keeps feeding its own clock, so the
            // quiet window that ends a replay's pull cannot trip mid-fight.
            if (chara is IBattleNpc npc && (byte)npc.BattleNpcKind == 5
                && npc.MaxHp != 0 && npc.CurrentHp != 0)
                _clock.SawEnemy();

            var actor = Actor(chara);

            // A spawn, which some fights announce nothing else about.
            if (_seen.Add(chara.GameObjectId) && _seen.Count <= MaxTracked)
                Raise(new GameEvent
                {
                    Kind = EventKind.ActorAdd,
                    Time = time,
                    Id = chara.BaseId,
                    Name = actor.Name,
                    Source = actor,
                });

            WatchCast(chara, actor, time);

            // Bosses carry statuses too, and some of them are the whole answer:
            // Dancing Mad hangs its real-or-fake tell on one, as a number in the
            // stack count rather than as a separate id.
            WatchStatuses(chara, actor, time);
        }

        // Anything gone is not casting any more either.
        Forget(_casting, live);
        Forget(_statuses, live);
        _seen.IntersectWith(live);
    }

    private void WatchCast(IBattleChara chara, Callouts.Actor actor, float time)
    {
        var was = _casting.GetValueOrDefault(chara.GameObjectId);
        var now = chara.IsCasting ? chara.CastActionId : 0u;
        if (now == was) return;

        if (now != 0 && _casting.Count < MaxTracked)
        {
            _casting[chara.GameObjectId] = now;
            Raise(new GameEvent
            {
                Kind = EventKind.CastStart,
                Time = time,
                Id = now,
                Name = ActionNames.Of(now),
                // What the engine leads a call by: how long is left to run.
                Value = MathF.Max(0f, chara.TotalCastTime - chara.CurrentCastTime),
                Source = actor,
                Target = Find(chara.TargetObjectId),
            });
        }
        else
        {
            _casting.Remove(chara.GameObjectId);
        }
    }

    // A status and the number riding on it. Two applications of the same id with
    // different stacks are two different things to a fight, so the pair is what
    // is remembered rather than the id alone.
    private readonly record struct Held(uint Id, ushort Param);

    private void WatchStatuses(IBattleChara chara, Callouts.Actor actor, float time)
    {
        if (_statuses.Count >= MaxTracked && !_statuses.ContainsKey(chara.GameObjectId)) return;

        if (!_statuses.TryGetValue(chara.GameObjectId, out var was))
            _statuses[chara.GameObjectId] = was = new HashSet<Held>();

        var now = new HashSet<Held>();

        // The whole hand goes to the engine before any of it is announced, so
        // the first debuff of a burst can already see the rest of them.
        _hand.Clear();
        foreach (var status in chara.StatusList)
            if (status.StatusId != 0)
                _hand.Add(new Callouts.Held(status.StatusId, status.RemainingTime, status.Param));
        _engine?.Statuses.Set((uint)chara.GameObjectId, _hand);

        foreach (var status in chara.StatusList)
        {
            if (status.StatusId == 0) continue;
            var held = new Held(status.StatusId, status.Param);
            now.Add(held);
            if (was.Contains(held)) continue;

            Raise(new GameEvent
            {
                Kind = EventKind.StatusGain,
                Time = time,
                Id = status.StatusId,
                Value = status.RemainingTime,
                // Stacks, and for some fights the only thing that tells two
                // otherwise identical applications apart.
                Extra = status.Param,
                Source = Find(status.SourceObject?.GameObjectId ?? 0),
                Target = actor,
            });
        }

        foreach (var gone in was)
            if (!now.Contains(gone))
                Raise(new GameEvent
                {
                    Kind = EventKind.StatusLose, Time = time, Id = gone.Id,
                    Extra = gone.Param, Target = actor,
                });

        _statuses[chara.GameObjectId] = now;
    }

    private static Callouts.Actor Actor(IBattleChara chara) => new(
        (uint)chara.GameObjectId,
        chara.Name.TextValue,
        chara.BaseId,
        new Spot(chara.Position.X, chara.Position.Z, chara.Position.Y),
        chara.Rotation);

    private static Callouts.Actor Find(ulong id)
    {
        if (id is 0 or 0xE0000000) return Callouts.Actor.Nobody;
        foreach (var obj in Service.ObjectTable)
            if (obj.GameObjectId == id && obj is IBattleChara chara) return Actor(chara);
        return Callouts.Actor.Nobody;
    }

    // One file per duty, named so two pulls in a night do not overwrite.
    private void StartRecording(uint territory)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Service.PluginInterface.GetPluginConfigDirectory(), "callout-records");
            System.IO.Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            _record = new System.IO.StreamWriter(
                System.IO.Path.Combine(dir, $"{territory}-{stamp}.fmrec"));
            _record.WriteLine($"{EventLog.Magic} {EventLog.Version}");
            _recorded = 0;
        }
        catch (Exception ex) { Swallowed.Report("callout recording", ex); _record = null; }
    }

    private void StopRecording()
    {
        if (_record is null) return;
        try { _record.Flush(); _record.Dispose(); }
        catch { /* the file is a diagnostic, never worth an exception */ }
        _record = null;
    }

    private void Recall(GameEvent e, IReadOnlyList<Call> calls)
    {
        if (_record is null || _recorded >= MaxRecorded) return;
        try
        {
            _record.WriteLine(EventLog.Format(e));
            _recorded++;
            for (var i = 0; i < calls.Count; i++)
                _record.WriteLine($"# said\t{calls[i].At:0.00}\t{calls[i].Banner}");
            if (_recorded % 200 == 0) _record.Flush();
        }
        catch { /* same */ }
    }

    private void Raise(GameEvent e)
    {
        var raised = _engine!.Feed(e);
        Recall(e, raised);
        if (raised.Count == 0) return;

        var heard = _scheduler.Apply(raised);
        for (var i = 0; i < heard.Count; i++)
        {
            Show(heard[i], e);
            Speak(heard[i]);
        }
    }

    // The banner, with the game's own art for whatever it is about and the
    // moment the thing actually resolves, so the overlay can count down to it.
    private void Show(Call call, GameEvent e)
    {
        if (!_config.BossAlertsDraw) return;

        var icon = e.Kind switch
        {
            EventKind.StatusGain or EventKind.StatusLose => Icons.ByStatusId(e.Id),
            EventKind.CastStart or EventKind.Ability => Icons.ByActionId(e.Id),
            _ => 0u,
        };

        _live.Add(new LiveAlert(
            call.Banner,
            icon,
            call.Severity,
            Lands: e.Time + MathF.Max(0f, e.Value),
            Until: MathF.Max(e.Time, call.At) + call.Duration,
            Personal: call.Personal));

        while (_live.Count > MaxOnScreen) _live.RemoveAt(0);
    }

    // A banner asked for from the settings page, so a call can be seen and the
    // overlay placed without pulling anything.
    public void ShowTest(string text, uint icon, CallSeverity level, bool personal, float hold = 5f)
    {
        _live.Add(new LiveAlert(text, icon, level, Lands: Clock + hold * 0.6f,
            Until: Clock + hold, Personal: personal));
        while (_live.Count > MaxOnScreen) _live.RemoveAt(0);
        _testUntil = Clock + hold;

        Service.Log.Information(
            $"[FrenMits] alert test: \"{text}\" live={_live.Count} clock={Clock:0.0} "
            + $"until={Clock + hold:0.0} draw={_config.BossAlertsDraw} on={_config.BossAlertsEnabled}");
    }

    private void Speak(Call call)
    {
        if (!_config.BossAlertsSpeak || !_config.AudioEnabled) return;

        var voice = _config.TtsUseEdge
            ? (string.IsNullOrWhiteSpace(_config.TtsCustomVoice)
                ? _config.TtsEdgeVoice : _config.TtsCustomVoice)
            : _config.TtsVoice;
        _audio.Speak(call.Spoken, _config.TtsRate, _config.TtsVolume, _config.TtsUseEdge, voice,
            Audio.AlertChannel);
    }

    // The z axis is height in the game and the second axis on the floor here,
    // which is why Actor swaps them.
    private static void Forget<T>(Dictionary<ulong, T> book, HashSet<ulong> live)
    {
        if (book.Count == 0) return;
        List<ulong>? gone = null;
        foreach (var id in book.Keys)
            if (!live.Contains(id)) (gone ??= new List<ulong>()).Add(id);
        if (gone is null) return;
        foreach (var id in gone) book.Remove(id);
    }

    public void Dispose()
    {
        StopRecording();
        _engine = null;
        _casting.Clear();
        _statuses.Clear();
        _seen.Clear();
    }
}

// One call as the overlay draws it: what it says, the game's art for it, how
// loud it is, when the thing lands and when the banner goes away.
public readonly record struct LiveAlert(
    string Text, uint Icon, FrenMits.Callouts.CallSeverity Level,
    float Lands, float Until, bool Personal);
