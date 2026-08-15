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
    private double _started;
    private bool _wasFighting;

    // What each actor was doing last frame, so this frame can tell what changed.
    private readonly Dictionary<ulong, uint> _casting = new();
    private readonly Dictionary<ulong, HashSet<uint>> _statuses = new();
    private readonly HashSet<ulong> _seen = new();

    // Drops a repeat, spaces two that land together, and merges a pair that
    // says the same thing. The engine raises calls; this decides what is heard.
    private readonly CallScheduler _scheduler = new();

    // What is on screen right now, newest last. Bounded, because a fight that
    // raised more than this at once has a bug worth seeing rather than hiding.
    public const int MaxOnScreen = 4;

    private readonly List<LiveAlert> _live = new();

    public IReadOnlyList<LiveAlert> Live => _live;

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

        var triggers = TriggersFor(territory);
        if (triggers.Count == 0) { _engine = null; return; }

        if (_config.BossAlertsRecord) StartRecording(territory);

        _engine = new TriggerEngine(triggers, Me());

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
    public void Update(bool inCombat)
    {
        if (!_config.BossAlertsEnabled) return;

        // The clock and the sweep of what is on screen come first, and run
        // wherever you are. A duty with no calls still has to clear a banner
        // asked for from the settings page.
        if (_started <= 0) _started = Now();
        Clock = (float)(Now() - _started);

        for (var i = _live.Count - 1; i >= 0; i--)
            if (_live[i].Until <= Clock) _live.RemoveAt(i);

        var here = Service.ClientState.TerritoryType;
        if (here != _territory) Enter(here);
        if (_engine is null) return;

        if (!inCombat)
        {
            if (_wasFighting) { _engine.Reset(); _casting.Clear(); _statuses.Clear(); _seen.Clear(); }
            _wasFighting = false;
            return;
        }

        _wasFighting = true;
        _engine.Me = Me();
        Sweep(Clock);
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
            if (chara is IPlayerCharacter) WatchStatuses(chara, actor, time);
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

    private void WatchStatuses(IBattleChara chara, Callouts.Actor actor, float time)
    {
        if (_statuses.Count >= MaxParty && !_statuses.ContainsKey(chara.GameObjectId)) return;

        if (!_statuses.TryGetValue(chara.GameObjectId, out var was))
            _statuses[chara.GameObjectId] = was = new HashSet<uint>();

        var now = new HashSet<uint>();
        foreach (var status in chara.StatusList)
        {
            if (status.StatusId == 0) continue;
            now.Add(status.StatusId);
            if (was.Contains(status.StatusId)) continue;

            Raise(new GameEvent
            {
                Kind = EventKind.StatusGain,
                Time = time,
                Id = status.StatusId,
                Value = status.RemainingTime,
                Source = Find(status.SourceObject?.GameObjectId ?? 0),
                Target = actor,
            });
        }

        foreach (var gone in was)
            if (!now.Contains(gone))
                Raise(new GameEvent
                {
                    Kind = EventKind.StatusLose, Time = time, Id = gone, Target = actor,
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
        if (_started <= 0) _started = Now();
        Clock = (float)(Now() - _started);

        _live.Add(new LiveAlert(text, icon, level, Lands: Clock + hold * 0.6f,
            Until: Clock + hold, Personal: personal));
        while (_live.Count > MaxOnScreen) _live.RemoveAt(0);
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
