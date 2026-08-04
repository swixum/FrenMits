using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FrenMits.Meter;

// Drains the parser link and keeps the pull on screen.
public class MeterEngine : IDisposable
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public MeterLink Link { get; }
    public RdpsEngine Engine { get; } = new();
    private readonly MeterDiag _mdiag = new();

    // One line into the pull record, and the session file.
    private void Note(string what)
    {
        if (_plugin.Config.Diagnostics) _plugin.Diag.Note("meter: " + what);
        if (C.MeterDiagFile) _mdiag.Note(_replaying ? "(replay) " + what : what);
    }

    public MeterEncounter? Current { get; private set; }
    public List<MeterEncounter> History { get; } = new();
    // Enough for a duty's bosses, or an evening of prog.
    private const int MaxHistory = 30;

    // Freezes the display; log lines keep flowing.
    public bool Paused { get; set; }

    // The previous update, so the bars glide between parser ticks.
    public MeterEncounter? Previous { get; private set; }
    public DateTime CurrentAt { get; private set; }
    public float LerpSpan { get; private set; } = 1f;

    // The fight being stitched across parser splits.
    private FightCarry? _carry;
    private MeterEncounter? _rawSeg;
    private long _fightStartSec;
    private string _fightTitle = "";
    private bool _sawBoss;
    // Whether the pull in progress has a boss in it yet.
    public bool SawBoss => _sawBoss;
    private float _bossLeft = -1f;
    private int _standing = -1;

    // How a pull finished: party down is a wipe.
    public static PullEnd EndOf(int standing, float bossLeft)
    {
        if (standing == 0) return PullEnd.Wipe;
        if (bossLeft == 0f) return PullEnd.Kill;
        return PullEnd.Unknown;
    }

    // The boss reading to keep, lowest once it drops.
    public static float TrackBoss(float current, float reading, bool inCombat)
    {
        if (reading < 0f) return current;
        if (inCombat) return reading;
        return current < 0f || reading < current ? reading : current;
    }

    // Whether the readout is due fresh numbers.
    public static bool DueToRefresh(double since, float rate)
        => rate <= 0f || since >= rate || since < 0;

    // A pull is over once combat stays off with control back.
    public const double SettleSeconds = 1.5;

    public static bool SettleDue(bool inCombat, bool cutscene, double sinceDrop)
        => !inCombat && !cutscene && sinceDrop > SettleSeconds;

    private DateTime _nextTrim = DateTime.MinValue;

    public MeterEngine(Plugin plugin)
    {
        _plugin = plugin;
        Link = new MeterLink(plugin);
        Engine.IsLimitBreak = IsLimitBreak;
        // English-sheet lookups, so localized logs still match.
        Engine.ResolveStatusIds = StatusIdsOf;
        Engine.ResolveActionIds = ActionIdsOf;
    }

    // One pass per sheet keeps the engine's lookups cheap. Volatile: the warm task publishes these.
    private static volatile Dictionary<string, List<uint>>? _statusIds;
    private static volatile Dictionary<string, List<uint>>? _actionIds;
    private static volatile HashSet<uint>? _lbActions;
    // Separate gates: the cheap limit-break pass must not queue behind the name maps.
    private static readonly object NameGate = new();
    private static readonly object LbGate = new();
    private static readonly List<uint> NoIds = new();

    // The Action sheet alone is tens of thousands of rows, so pay for it off the game's thread.
    // Best effort: the sheets may not be up yet, and the lazy paths below still cover that.
    public static void WarmSheets()
        => System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                // Cheapest and most load-bearing first, so a live pull is right soonest.
                BuildLimitBreaks(wait: true);
                StatusMap(wait: true);
                ActionMap(wait: true);
            }
            catch (Exception ex) { Swallowed.Report("meter sheet warm", ex); }
        });

    // Names are not unique, so each one keeps every row id that carries it.
    private static void Add(Dictionary<string, List<uint>> map, string name, uint id)
        => (map.TryGetValue(name, out var list) ? list : map[name] = new List<uint>()).Add(id);

    // The game's thread never waits on the warm task: a miss just retries next frame.
    private static bool Enter(object gate, bool wait)
    {
        if (wait) { System.Threading.Monitor.Enter(gate); return true; }
        return System.Threading.Monitor.TryEnter(gate);
    }

    private static Dictionary<string, List<uint>>? StatusMap(bool wait = false)
    {
        if (_statusIds != null) return _statusIds;
        if (!Enter(NameGate, wait)) return null;
        try
        {
            if (_statusIds != null) return _statusIds;
            var sheet = GameData.English<Lumina.Excel.Sheets.Status>();
            if (sheet == null) return null;
            var map = new Dictionary<string, List<uint>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in sheet)
            {
                var name = row.Name.ExtractText();
                if (name.Length > 0) Add(map, name, row.RowId);
            }
            return _statusIds = map;
        }
        finally { System.Threading.Monitor.Exit(NameGate); }
    }

    private static Dictionary<string, List<uint>>? ActionMap(bool wait = false)
    {
        if (_actionIds != null) return _actionIds;
        if (!Enter(NameGate, wait)) return null;
        try
        {
            if (_actionIds != null) return _actionIds;
            var sheet = GameData.English<Lumina.Excel.Sheets.Action>();
            if (sheet == null) return null;
            var map = new Dictionary<string, List<uint>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in sheet)
            {
                var name = row.Name.ExtractText();
                if (name.Length > 0) Add(map, name, row.RowId);
            }
            return _actionIds = map;
        }
        finally { System.Threading.Monitor.Exit(NameGate); }
    }

    private static List<uint>? StatusIdsOf(string english)
        => StatusMap() is { } map ? map.TryGetValue(english, out var ids) ? ids : NoIds : null;

    private static List<uint>? ActionIdsOf(string english)
        => ActionMap() is { } map ? map.TryGetValue(english, out var ids) ? ids : NoIds : null;

    // Limit break ids, resolved once the sheets are up. No text to pull out, so this pass is cheap.
    private static HashSet<uint>? BuildLimitBreaks(bool wait = false)
    {
        if (_lbActions != null) return _lbActions;
        if (!Enter(LbGate, wait)) return null;
        try
        {
            if (_lbActions != null) return _lbActions;
            var sheet = GameData.English<Lumina.Excel.Sheets.Action>();
            if (sheet == null) return null;
            var set = new HashSet<uint>();
            foreach (var row in sheet)
                if (row.ActionCategory.RowId == 9)
                    set.Add(row.RowId);
            return _lbActions = set;
        }
        finally { System.Threading.Monitor.Exit(LbGate); }
    }

    // Not ready: retry on a later hit.
    private bool IsLimitBreak(uint actionId) => BuildLimitBreaks()?.Contains(actionId) ?? false;

    public void Update()
    {
        if (!C.MeterEnabled)
        {
            Link.EnsureStopped();
            return;
        }
        Link.EnsureStarted();
        // Ids resolve once the sheets are up, whether or not lines are flowing.
        Engine.PrimeIds();

        var budget = 5000;
        while (budget-- > 0 && Link.TryDequeue(out var msg)) Handle(msg);

        // The world the stitch reads, taken once a frame.
        _inCombat = Plugin.InCombat;
        _cutscene = Plugin.CutsceneActive;

        if (!_replaying) CheckFeedAlive();

        // Seeded from the game's own list, since the log only names allies once.
        if (_inCombat && !_replaying) SeedAllies();

        // A boss on the field makes this fight worth keeping.
        if (_inCombat && _plugin.BossHpFraction >= 0f) _sawBoss = true;
        _bossLeft = TrackBoss(_bossLeft, _plugin.BossHpFraction, _inCombat);
        // The last word on who was up, never through a cutscene.
        if (_inCombat && !_cutscene && _plugin.PlayersStanding >= 0)
            _standing = _plugin.PlayersStanding;
        UpdateKillFreeze();

        // Combat over: close the fight without waiting.
        var inCombat = _inCombat;
        if (inCombat != _wasInCombat && !_replaying)
            Note($"combat {(inCombat ? "on" : "off")} - board {Current?.Seconds ?? 0:0}s");
        if (!inCombat && _wasInCombat) _combatDropAt = DateTime.UtcNow;
        // A cutscene drops combat without ending the pull.
        if (_cutscene) _combatDropAt = DateTime.UtcNow;
        if (inCombat) _cutDone = false;
        _wasInCombat = inCombat;
        var sinceDrop = _combatDropAt == DateTime.MinValue
            ? 0.0 : (DateTime.UtcNow - _combatDropAt).TotalSeconds;
        var settle = SettleDue(inCombat, _cutscene, sinceDrop);

        // A stitched fight that ended in the quiet gap.
        if (!Paused && _carry != null && _rawSeg is not { Active: true } && settle)
        {
            Note($"stitched fight settled in the gap after {sinceDrop:0.0}s quiet");
            if (Current != null)
            {
                Current.Active = false;
                Materialize(Current);
                if (WorthKeeping(Current)) PushHistory(Current);
            }
            EndFight();
        }

        if (DateTime.UtcNow >= _nextTrim)
        {
            _nextTrim = DateTime.UtcNow + TimeSpan.FromMinutes(1);
            Engine.Trim();
        }

        _mdiag.Update();

        if (!_cutDone && !Paused && _rawSeg is { Active: true } && settle)
        {
            _cutDone = true;
            Note($"settle cut after {sinceDrop:0.0}s quiet - board {Current?.Seconds ?? 0:0}s "
               + $"{(Current?.TotalDamage ?? 0) / 1e6:0.0}M");
            CutHere();
        }

        // The active profile follows every tweak by itself.
        if (Configuration.SaveTick != _syncedTick && DateTime.UtcNow >= _nextProfileSync)
        {
            _nextProfileSync = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            var name = C.MeterProfileName;
            if (name.Length > 0 && C.MeterProfiles.TryGetValue(name, out var stored))
            {
                var now = MeterProfile.Export(C);
                if (!string.Equals(stored, now, StringComparison.Ordinal))
                {
                    C.MeterProfiles[name] = now;
                    C.SaveSettings();
                }
            }
            // Set last, so the save above doesn't ask for another.
            _syncedTick = Configuration.SaveTick;
        }
    }

    private DateTime _nextProfileSync = DateTime.MinValue;
    private int _syncedTick = -1;
    private DateTime _combatDropAt = DateTime.MinValue;
    private bool _wasInCombat;
    private bool _cutDone;

    private void Handle(JObject msg)
    {
        if (string.Equals(msg["type"]?.ToString(), "LogLine", StringComparison.Ordinal))
        {
            if (msg["line"] is JArray { Count: > 1 } arr)
            {
                // Most of a raid's lines are types the engine skips, so ask first.
                if (!RdpsEngine.Handles(arr[0]?.ToString() ?? "")) return;
                var line = new string[arr.Count];
                for (var i = 0; i < arr.Count; i++) line[i] = arr[i]?.ToString() ?? "";
                if (line.Length > 3 && line[0] == "01") Note($"zone change - {line[3]}");
                Engine.Process(line);
            }
            return;
        }

        if (Paused) return;
        if (MeterEncounter.Parse(msg) is not { Rows.Count: > 0 } raw) return;
        OnSummary(raw);
    }

    // The world the stitch reads, live or from a recording.
    private bool _inCombat;
    private bool _cutscene;
    private bool _replaying;

    // ---- is the feed even alive? ----

    // A link can die quietly, so both totals must go quiet too.
    public const double StaleAfterSeconds = 15;

    // A fight is on and the numbers stopped being about it.
    public static bool FeedIsStale(bool fightOn, double sinceFresh)
        => fightOn && sinceFresh > StaleAfterSeconds;

    public bool FeedStale { get; private set; }

    // A replay feeds nothing, so the board holds the last pull.
    public bool FeedStaleInReplay { get; private set; }

    private DateTime _feedFreshAt = DateTime.UtcNow;
    private (bool Active, float Seconds, double Damage) _feedSig;
    private double _seenLines;
    private DateTime _nextRelink = DateTime.MinValue;
    private DateTime _nextLineCheck = DateTime.MinValue;

    private void FeedIsFresh() => _feedFreshAt = DateTime.UtcNow;

    private void CheckFeedAlive()
    {
        var now = DateTime.UtcNow;
        // Counting the whole table is not a per-frame job.
        if (now >= _nextLineCheck)
        {
            _nextLineCheck = now + TimeSpan.FromSeconds(1);
            var lines = EngineTotal();
            if (lines > _seenLines) FeedIsFresh();
            _seenLines = lines;
        }

        var replay = Plugin.InDutyPlayback;
        var stale = FeedIsStale(_inCombat || (replay && _plugin.Timer.Running),
                                (now - _feedFreshAt).TotalSeconds);
        FeedStaleInReplay = stale && replay;
        if (stale != FeedStale)
        {
            FeedStale = stale;
            Note(stale
                ? FeedStaleInReplay
                    ? "a replay feeds the parser nothing - the board is still showing the last real pull"
                    : "the parser feed has gone quiet mid-fight - what is on the board is not this pull"
                : "parser feed is live again");
            Service.Log.Warning(stale
                ? "[FrenMits] Meter: no new parser data and no damage counted for "
                  + $"{StaleAfterSeconds:0}s{(replay ? " of playback" : " of combat")}."
                : "[FrenMits] Meter: parser feed recovered.");
        }

        // Ask again, unless a replay is what went quiet.
        if (!stale || replay || now < _nextRelink) return;
        _nextRelink = now + TimeSpan.FromSeconds(30);
        Link.RetryNow();
    }

    private void RecordFeed(MeterEncounter incoming)
    {
        if (!MeterFeed.Recording) return;
        var m = new MeterFeed.Message
        {
            At = MeterFeed.Elapsed,
            Active = incoming.Active, Seconds = incoming.Seconds, Damage = incoming.TotalDamage,
            InCombat = _inCombat, Cutscene = _cutscene, SawBoss = _sawBoss, LogLines = EngineTotal(),
        };
        foreach (var r in incoming.Rows)
            m.Rows.Add((r.Name, r.Job, r.Damage, r.Healed, r.Taken, r.Deaths, r.Shielded));
        MeterFeed.Record(m);
    }

    // Run a recorded feed back through the stitch.
    public string Replay(string path)
    {
        // The replay banks and clears as it goes, so a running pull is off limits.
        if (_inCombat || Current is { Active: true })
            return "a pull is running - replay once it ends";

        List<MeterFeed.Message> feed;
        try { feed = MeterFeed.Load(path); }
        catch (Exception ex) { return $"could not read that recording: {ex.Message}"; }
        if (feed.Count == 0) return "that recording is empty";

        var wasRecording = MeterFeed.Recording;
        var keepCombat = _inCombat;
        var keepCutscene = _cutscene;
        var keepBoss = _sawBoss;
        // The stitch state the next live summary must come back to.
        var keepCut = _cut;
        var keepRawIn = _rawIn;
        var keepRawSeg = _rawSeg;
        var keepCarry = _carry;
        var keepStart = _fightStartSec;
        var keepTitle = _fightTitle;
        MeterFeed.Pause();
        _replaying = true;
        var logLines = 0.0;
        try
        {
            Clear(keepHistory: true);
            // A replay starts from nothing, like the recorded pull.
            _cut = null;
            _rawIn = null;

            foreach (var m in feed)
            {
                _inCombat = m.InCombat;
                _cutscene = m.Cutscene;
                _sawBoss = m.SawBoss;
                logLines = Math.Max(logLines, m.LogLines);
                OnSummary(MeterFeed.ToEncounter(m));
            }
        }
        finally
        {
            // Live state comes back whole even if a summary throws mid-replay.
            _replaying = false;
            _inCombat = keepCombat;
            _cutscene = keepCutscene;
            _sawBoss = keepBoss;
            _cut = keepCut;
            _rawIn = keepRawIn;
            _rawSeg = keepRawSeg;
            _carry = keepCarry;
            _fightStartSec = keepStart;
            _fightTitle = keepTitle;
            _idle.Clear();
            if (wasRecording) MeterFeed.Resume();
        }

        var shown = Current?.TotalDamage ?? 0;
        var drift = logLines > 0 ? shown / logLines : 0;
        return $"{feed.Count} summaries: the board makes it {shown / 1e6:0.0}M, "
             + $"the log lines {logLines / 1e6:0.0}M"
             + (logLines > 0 ? $" ({drift:0.00}x)" : "");
    }

    private void OnSummary(MeterEncounter incoming)
    {
        RecordFeed(incoming);
        // Only a summary saying something new counts as alive.
        var sig = (incoming.Active, incoming.Seconds, incoming.TotalDamage);
        if (sig != _feedSig) { _feedSig = sig; FeedIsFresh(); }
        _rawIn = incoming;
        var raw = incoming;
        var restarted = false;
        if (_cut != null)
        {
            // A new parser encounter retires the cut.
            if (!CutStillHolds(raw.TotalDamage, _cut.Damage))
            {
                Note($"parser restarted (damage {raw.TotalDamage / 1e6:0.0}M "
                   + $"under the cut's {_cut.Damage / 1e6:0.0}M); baseline dropped");
                _cut = null;
                restarted = true;
            }
            else
            {
                raw = Subtract(incoming, _cut);
                // A pull starts when damage does, so the cut slides.
                if (raw.TotalDamage <= 0)
                {
                    _cut = Snapshot(incoming);
                    return;
                }
            }
        }

        if (raw.Active)
        {
            var continuing = SameSegment(restarted, _rawSeg, raw.TotalDamage);
            if (continuing) AccrueIdle(raw.Seconds, raw.TotalDamage);
            if (!continuing)
            {
                // A segment ended without its final update, so bank this.
                if (_rawSeg is { Active: true })
                {
                    if (!restarted)
                        Note($"segment break - parser clock {_rawSeg.Seconds:0}s -> {raw.Seconds:0}s, "
                           + $"damage {_rawSeg.TotalDamage / 1e6:0.0}M -> {raw.TotalDamage / 1e6:0.0}M");
                    if (restarted)
                    {
                        // Nothing is banked yet, so this message is all of it.
                        EndSegment(_rawSeg);
                        _cut = null;
                    }
                    else
                    {
                        // The dying segment's own last summary; raw already belongs to the new pull.
                        EndSegment(_rawSeg);
                        if (_cut != null) raw = Subtract(incoming, _cut);
                    }
                }
                if (_carry == null)
                {
                    _fightStartSec = Math.Max(0, Engine.LatestSec - (long)raw.Seconds);
                    _fightTitle = ""; // a fresh fight names itself from scratch
                    _sawBoss = false;
                    Note($"pull start - parser {raw.Seconds:0}s {raw.TotalDamage / 1e6:0.00}M"
                       + $"{(_cut != null ? " (cut active)" : "")}{(_inCombat ? "" : ", combat off")}");
                }
                // A fresh segment starts its idle clock over.
                ResetIdle(raw.Seconds, raw.TotalDamage);
            }
            SetTitle(raw);
            _rawSeg = raw;
            // Frozen at the kill: already trimmed, so it skips the second pass.
            Publish(Merge(_carry, _killFrozen ?? Trimmed(raw)));
            return;
        }

        SetTitle(raw);

        // The segment's final numbers.
        if (_rawSeg is { Active: true })
        {
            AccrueIdle(raw.Seconds, raw.TotalDamage);
            _rawSeg = raw;
            EndSegment(raw);
            ResetIdle(raw.Seconds, raw.TotalDamage);
        }
        else
            _rawSeg = raw;
    }

    private void EndSegment(MeterEncounter final)
    {
        // Banked on the active clock, and held at the kill if the boss died first.
        final = _killFrozen ?? Trimmed(final);
        var display = Merge(_carry, final);
        // Stitching is the only arithmetic here, and it's opt-in.
        if (C.MeterStitchSegments && _inCombat && _sawBoss)
        {
            // Mid-boss split: stitch and keep reading as one fight.
            _carry ??= new FightCarry { StartSec = _fightStartSec, Title = _fightTitle };
            Fold(_carry, final, Engine.LatestSec);
            // Banked now, so the parser is measured from here.
            if (_rawIn != null) _cut = Snapshot(_rawIn);
            // Printed beside the total, a bad stitch shows itself.
            Note($"banked segment {final.Seconds:0}s {final.TotalDamage / 1e6:0.0}M; "
               + $"fight now {_carry.Seconds:0}s {Total(_carry) / 1e6:0.0}M "
               + $"(log lines say {EngineTotal() / 1e6:0.0}M)");
            display.Active = true;
            Publish(display);
            return;
        }

        display.Active = false;
        Materialize(display);
        Publish(display);
        Note($"pull ended - {display.Seconds:0}s {display.TotalDamage / 1e6:0.0}M"
           + $"{(WorthKeeping(display) ? "" : ", not kept")}");
        if (WorthKeeping(display)) PushHistory(display);
        EndFight();
    }

    // A finished pull carries its own breakdowns.
    private void Materialize(MeterEncounter enc)
    {
        // Already carrying them: banked before, or frozen at the kill.
        if (enc.Dealt.Count > 0 || enc.Deaths.Count > 0) return;
        enc.BossLeft = _bossLeft;
        enc.Boss = _sawBoss;
        enc.Ended = EndOf(_standing, _bossLeft);

        // The parser says "YOU", so resolve it before filing.
        var you = LocalName();
        foreach (var r in enc.Rows)
        {
            // The engine files the limit break under its English constant.
            var who = r.LimitBreak ? RdpsEngine.LimitBreakName
                : you.Length > 0 && string.Equals(r.Name, "YOU", StringComparison.OrdinalIgnoreCase)
                    ? you
                    : r.Display.Length > 0 ? r.Display : r.Name;
            if (Engine.Dealt(who) is { Count: > 0 } d) enc.Dealt[who] = Freeze(d);
            if (Engine.Targets(who) is { Count: > 0 } t) enc.Targets[who] = Freeze(t);
            if (Engine.Taken(who) is { Count: > 0 } k) enc.Taken[who] = Freeze(k);
            if (Engine.Heals(who) is { Count: > 0 } h) enc.Heals[who] = Freeze(h);
            if (Engine.HealTargets(who) is { Count: > 0 } ht) enc.HealTargets[who] = Freeze(ht);
            if (Engine.HealFrom(who) is { Count: > 0 } hf) enc.HealFrom[who] = Freeze(hf);
            if (Engine.Given(who) is { Count: > 0 } g) enc.Given[who] = Freeze(g);
            if (Engine.Received(who) is { Count: > 0 } rc) enc.Received[who] = Freeze(rc);
        }

        enc.Deaths.Clear();
        var start = FightStart(enc);
        foreach (var d in Engine.Deaths()) enc.Deaths.Add(Freeze(d, start));
    }

    // Copies, since the engine keeps tallying into its rows.
    public static List<AbilityStat> Freeze(List<AbilityStat> live)
    {
        var copy = new List<AbilityStat>(live.Count);
        foreach (var a in live)
            copy.Add(new AbilityStat
            {
                Name = a.Name, Hits = a.Hits, Crits = a.Crits,
                Dhs = a.Dhs, Cdhs = a.Cdhs, Damage = a.Damage, Max = a.Max, Over = a.Over,
                Id = a.Id, IsStatus = a.IsStatus,
                Parts = a.Parts is { } parts ? Freeze(parts) : null,
            });
        return copy;
    }

    // The same for a death, in fight time.
    public static DeathRecord Freeze(DeathRecord live, long start)
    {
        var copy = new DeathRecord
        {
            Name = live.Name, Sec = live.Sec, Killer = live.Killer, KillingBlow = live.KillingBlow,
            At = Math.Max(0f, live.Sec - start),
        };
        foreach (var h in live.Lead)
            copy.Lead.Add(new DeathHit { Name = h.Name, Amount = h.Amount, Sec = h.Sec, Heal = h.Heal });
        return copy;
    }

    // Where the pull on screen started, in log seconds.
    private long FightStart(MeterEncounter enc)
        => _carry?.StartSec ?? (_fightStartSec > 0 ? _fightStartSec : Engine.LatestSec - (long)enc.Seconds);

    // A pull with breakdowns is finished, so never read live.
    private static bool Banked(MeterEncounter enc)
        => enc.Dealt.Count > 0 || enc.Taken.Count > 0 || enc.Heals.Count > 0 || enc.Deaths.Count > 0;

    // What a player did, or had done to them, in the pull on screen.
    public List<AbilityStat> Breakdown(MeterEncounter enc, string player, int kind)
    {
        var stored = kind switch
        {
            1 => enc.Targets,
            2 => enc.Taken,
            3 => enc.Heals,
            4 => enc.HealTargets,
            5 => enc.HealFrom,
            6 => enc.Given,
            7 => enc.Received,
            _ => enc.Dealt,
        };
        if (stored.TryGetValue(player, out var saved)) return saved;
        if (Banked(enc)) return new List<AbilityStat>();
        return kind switch
        {
            1 => Engine.Targets(player),
            2 => Engine.Taken(player),
            3 => Engine.Heals(player),
            4 => Engine.HealTargets(player),
            5 => Engine.HealFrom(player),
            6 => Engine.Given(player),
            7 => Engine.Received(player),
            _ => Engine.Dealt(player),
        };
    }

    // Every death in the pull on screen, in the order they happened.
    public List<DeathRecord> Deaths(MeterEncounter enc)
    {
        if (enc.Deaths.Count > 0 || Banked(enc)) return enc.Deaths;
        var start = FightStart(enc);
        var live = new List<DeathRecord>();
        foreach (var d in Engine.Deaths()) live.Add(Freeze(d, start));
        return live;
    }

    // One player's, out of those.
    public List<DeathRecord> Deaths(MeterEncounter enc, string player)
    {
        var list = new List<DeathRecord>();
        foreach (var d in Deaths(enc))
            if (string.Equals(d.Name, player, StringComparison.OrdinalIgnoreCase))
                list.Add(d);
        return list;
    }

    private void EndFight()
    {
        // The parser's encounter can outlive the pull, so the next starts from here.
        if (_rawIn != null) _cut = Snapshot(_rawIn);
        _carry = null;
        _fightStartSec = 0;
        _fightTitle = "";
        _sawBoss = false;
        _bossLeft = -1f;
        _standing = -1;
        _warnedAt = 0;
        _seenLines = 0;   // the engine's table is cleared below, so its count restarts
        _idle.Clear();
        _killFrozen = null;
        // Clear at fight end, or the lagging feed eats the opener.
        Engine.ClearBreakdown();
    }

    // Worth looking back at: a boss, or something long.
    public const float HistoryMinSeconds = 25f;

    public static bool WorthKeeping(bool sawBoss, float seconds)
        => sawBoss || seconds >= HistoryMinSeconds;

    // Judged on the wall clock, so a cutscene can't disqualify a long pull.
    private bool WorthKeeping(MeterEncounter enc)
        => WorthKeeping(_sawBoss, MathF.Max(enc.Seconds, enc.WallSeconds));

    private void PushHistory(MeterEncounter enc)
    {
        if (enc.Rows.Count == 0) return;
        History.Insert(0, enc);
        while (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);
        // The insert shifted every row down one, so held picks follow their pull.
        _plugin.MeterWindow.OnHistoryInserted(History.Count);
        _plugin.MeterHistoryWindow.OnHistoryInserted(History.Count);
    }

    // The parser says "Encounter" until it ends, so name it.
    private void SetTitle(MeterEncounter e)
    {
        if (e.Title.Length > 0 && !e.Title.Equals("Encounter", StringComparison.OrdinalIgnoreCase))
            _fightTitle = e.Title;               // the parser named it: final word
        else if (_plugin.BossHpFraction >= 0f && _plugin.CurrentBossName.Length > 0)
            _fightTitle = _plugin.CurrentBossName;
        else if (_fightTitle.Length == 0 && Engine.CurrentEnemy.Length > 0)
            _fightTitle = Engine.CurrentEnemy;
        e.Title = _fightTitle.Length > 0 ? _fightTitle : "Encounter";
    }

    // How far the parser's total may sit above our own count.
    private const double DriftWarnAbove = 1.10;
    private double _warnedAt;

    // The two totals should track, so the record says when not.
    private void CheckAgainstLogLines(MeterEncounter enc)
    {
        if (_replaying) return;   // the engine holds this session, not the recording
        var counted = EngineTotal();
        if (counted <= 0 || enc.TotalDamage <= counted * DriftWarnAbove) return;
        if (enc.TotalDamage < _warnedAt * 1.05) return;   // once per step, not per frame
        _warnedAt = enc.TotalDamage;
        Note($"DRIFT - showing {enc.TotalDamage / 1e6:0.0}M but the log lines "
           + $"only account for {counted / 1e6:0.0}M");
    }

    private void Publish(MeterEncounter enc)
    {
        // The frozen board keeps its capture-time credit; the engine has moved on.
        if (!ReferenceEquals(enc, _killFrozen)) ApplyRdps(enc);
        if (!enc.Active && !_replaying) NoteAttribution(enc);
        // Banked with the pull, so history keeps its icon.
        if (Engine.LastLimitBreak != 0) enc.LimitBreakAction = Engine.LastLimitBreak;
        if (enc.Active) CheckAgainstLogLines(enc);
        // Glide only within one running fight.
        Previous = enc.Active && Current is { Active: true } cur
                   && cur.Title == enc.Title && enc.Seconds + 0.5f >= cur.Seconds
            ? Current
            : null;
        if (Previous != null)
            LerpSpan = Math.Clamp((float)(DateTime.UtcNow - CurrentAt).TotalSeconds, 0.25f, 1.5f);
        CurrentAt = DateTime.UtcNow;
        Current = enc;
    }

    // ---- segment stitching ----

    public sealed class FightCarry
    {
        public long StartSec;
        public float Seconds;
        // The banked segments on the wall clock, for the on-screen timer.
        public float WallSeconds;
        public string Title = "";
        public Dictionary<string, MeterCombatant> Rows { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    // Bank a segment, capped at the time really elapsed.
    public static void Fold(FightCarry carry, MeterEncounter final, long nowSec = 0)
    {
        foreach (var r in final.Rows)
            carry.Rows[r.Name] = Combine(carry.Rows.GetValueOrDefault(r.Name), r);
        carry.Seconds += final.Seconds;
        carry.WallSeconds += final.WallSeconds > 0f ? final.WallSeconds : final.Seconds;
        if (nowSec > 0 && carry.StartSec > 0)
        {
            var elapsed = Math.Max(0, nowSec - carry.StartSec);
            carry.Seconds = Math.Min(carry.Seconds, elapsed);
            carry.WallSeconds = Math.Min(carry.WallSeconds, elapsed);
        }
        if (final.Title.Length > 0) carry.Title = final.Title;
    }

    // Everything the engine has counted from the log lines this fight.
    private double EngineTotal() => Engine.DealtTotal;

    // What a stitched fight has banked so far, for the pull record.
    public static double Total(FightCarry carry)
    {
        var sum = 0.0;
        foreach (var r in carry.Rows) sum += r.Value.Damage;
        return sum;
    }

    // The banked segments plus the live one, presented as a single fight.
    public static MeterEncounter Merge(FightCarry? carry, MeterEncounter seg)
    {
        if (carry == null || carry.Seconds <= 0f) return seg;
        var secs = Math.Max(1f, carry.Seconds + seg.Seconds);
        // The timer shows the whole fight; the rates below divide by active time only.
        var wall = Math.Max(secs,
            (carry.WallSeconds > 0f ? carry.WallSeconds : carry.Seconds)
            + (seg.WallSeconds > 0f ? seg.WallSeconds : seg.Seconds));
        var e = new MeterEncounter
        {
            Title = seg.Title.Length > 0 ? seg.Title : carry.Title,
            Active = seg.Active,
            Seconds = secs,
            WallSeconds = wall,
            Duration = $"{(int)wall / 60:00}:{(int)wall % 60:00}",
            When = seg.When,
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in seg.Rows)
        {
            seen.Add(r.Name);
            e.Rows.Add(Combine(carry.Rows.GetValueOrDefault(r.Name), r));
        }
        foreach (var kv in carry.Rows)
            if (seen.Add(kv.Key))
                e.Rows.Add(Combine(null, kv.Value));

        foreach (var r in e.Rows)
        {
            r.Dps = r.Damage / secs;
            r.RDps = r.Dps;
            r.Hps = r.Healed / secs;
            e.TotalDamage += r.Damage;
            e.TotalTaken += r.Taken;
            e.TotalDeaths += r.Deaths;
        }
        e.TotalDps = e.TotalDamage / secs;
        e.TotalHps = 0;
        var healed = 0.0;
        foreach (var r in e.Rows) healed += r.Healed;
        foreach (var r in e.Rows)
        {
            e.TotalHps += r.Hps;
            r.DamagePct = e.TotalDamage > 0 ? $"{r.Damage / e.TotalDamage * 100:0}%" : "";
            r.HealedPct = healed > 0 ? $"{r.Healed / healed * 100:0}%" : "";
        }
        return e;
    }

    // ---- cutting the parser's running encounter ----

    // Where the meter last drew a line under the parser's totals.
    public sealed class Baseline
    {
        public float Seconds;
        public double Damage;
        public Dictionary<string, MeterCombatant> Rows { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private Baseline? _cut;
    private MeterEncounter? _rawIn;

    // A cut holds while the parser is on the same encounter.
    public static bool CutStillHolds(double parserDamage, double cutDamage)
        => parserDamage + 1.0 >= cutDamage;

    // The same rule decides continuation, since the clock backs up.
    public static bool SameSegment(bool restarted, MeterEncounter? prev, double damage)
        => !restarted && prev is { Active: true } && damage + 1.0 >= prev.TotalDamage;

    public static Baseline Snapshot(MeterEncounter raw)
    {
        var b = new Baseline { Seconds = raw.Seconds, Damage = raw.TotalDamage };
        foreach (var r in raw.Rows) b.Rows[r.Name] = r;
        return b;
    }

    // Parser time spent in a cutscene or downtime this segment; per-second numbers must not count it.
    private readonly IdleClock _idle = new();

    private void AccrueIdle(float parserSec, double damage)
        => _idle.Accrue(parserSec, damage, _cutscene || (!_replaying && _plugin.DowntimeActive));

    // A new segment also outlives any kill freeze from the last one.
    private void ResetIdle(float parserSec, double damage)
    {
        _idle.Reset(parserSec, damage);
        _killFrozen = null;
    }

    // ---- kill freeze ----

    // The board as it stood when the boss hit zero; hits on the corpse stay off the pull.
    private MeterEncounter? _killFrozen;
    private DateTime _killAt;

    // Combat outliving a dead boss this long reads as a phase, not the kill.
    private const double KillGraceSeconds = 10;

    private void UpdateKillFreeze()
    {
        if (_replaying) return;
        var hp = _plugin.BossHpFraction;
        if (_killFrozen == null)
        {
            if (hp == 0f && _sawBoss && _rawSeg is { Active: true } seg)
            {
                var frozen = Trimmed(seg);
                // Always a copy, so the live segment object stays untouched.
                if (ReferenceEquals(frozen, seg)) frozen = Subtract(seg, new Baseline());
                // Credit and breakdowns land now, before the engine counts corpse hits.
                ApplyRdps(frozen);
                Materialize(frozen);
                _killFrozen = frozen;
                _killAt = DateTime.UtcNow;
                Note($"boss down at {frozen.TotalDamage / 1e6:0.0}M - hits on the corpse stay off this pull");
            }
            return;
        }
        // A boss went live again, or combat carried on: a phase or the next target, not the kill.
        if (hp > 0f || (_inCombat && (DateTime.UtcNow - _killAt).TotalSeconds > KillGraceSeconds))
        {
            Note("fight carries on - counting again");
            _killFrozen = null;
        }
    }

    // The same numbers on the active clock only; a copy, so the stitch math upstream keeps the raw clock.
    private MeterEncounter Trimmed(MeterEncounter enc)
    {
        if (_idle.IdleSec < 0.25f) return enc;
        var e = Subtract(enc, new Baseline { Seconds = _idle.IdleSec });
        // The clock on screen keeps the whole fight; only the per-second math shrinks.
        e.WallSeconds = enc.Seconds;
        e.Duration = enc.Duration.Length > 0
            ? enc.Duration
            : $"{(int)enc.Seconds / 60:00}:{(int)enc.Seconds % 60:00}";
        return e;
    }

    // Close the running fight here and start the next one from zero.
    private void CutHere()
    {
        if (_rawSeg is { Active: true } seg)
        {
            var display = Merge(_carry, _killFrozen ?? Trimmed(seg));
            display.Active = false;
            Materialize(display);
            Publish(display);
            if (WorthKeeping(display)) PushHistory(display);
            EndFight();
        }
        if (_rawIn != null) _cut = Snapshot(_rawIn);
        _rawSeg = null;
    }

    // The parser's totals minus everything before the cut.
    public static MeterEncounter Subtract(MeterEncounter raw, Baseline cut)
    {
        var secs = Math.Max(0f, raw.Seconds - cut.Seconds);
        var div = Math.Max(1f, secs);
        var e = new MeterEncounter
        {
            Title = raw.Title, Active = raw.Active, Seconds = secs, When = raw.When,
            Duration = $"{(int)secs / 60:00}:{(int)secs % 60:00}",
        };

        foreach (var r in raw.Rows)
        {
            var b = cut.Rows.GetValueOrDefault(r.Name);
            var row = new MeterCombatant
            {
                Name = r.Name, Display = r.Display, Job = r.Job, ADps = r.ADps,
                LimitBreak = r.LimitBreak,
                Damage = Math.Max(0, r.Damage - (b?.Damage ?? 0)),
                Healed = Math.Max(0, r.Healed - (b?.Healed ?? 0)),
                Shielded = Math.Max(0, r.Shielded - (b?.Shielded ?? 0)),
                Taken = Math.Max(0, r.Taken - (b?.Taken ?? 0)),
                Deaths = Math.Max(0, r.Deaths - (b?.Deaths ?? 0)),
                // Rates are running averages the parser never breaks down.
                CritPct = r.CritPct, DirectHitPct = r.DirectHitPct,
                CritDirectHitPct = r.CritDirectHitPct, OverhealPct = r.OverhealPct,
                MaxHit = r.MaxHit,
            };
            if (row.Damage <= 0 && row.Healed <= 0 && row.Taken <= 0 && row.Deaths <= 0) continue;
            row.Dps = row.Damage / div;
            row.RDps = row.Dps;
            row.Hps = row.Healed / div;
            e.Rows.Add(row);
            e.TotalDamage += row.Damage;
            e.TotalTaken += row.Taken;
            e.TotalDeaths += row.Deaths;
            e.TotalHps += row.Hps;
        }
        e.TotalDps = e.TotalDamage / div;
        var healed = 0.0;
        foreach (var r in e.Rows) healed += r.Healed;
        foreach (var r in e.Rows)
        {
            r.DamagePct = e.TotalDamage > 0 ? $"{r.Damage / e.TotalDamage * 100:0}%" : "";
            r.HealedPct = healed > 0 ? $"{r.Healed / healed * 100:0}%" : "";
        }
        return e;
    }

    // Two chunks of one player's fight, damage-weighted.
    public static MeterCombatant Combine(MeterCombatant? a, MeterCombatant b)
    {
        a ??= new MeterCombatant();
        var dmg = a.Damage + b.Damage;
        var healed = a.Healed + b.Healed;
        return new MeterCombatant
        {
            Name = b.Name.Length > 0 ? b.Name : a.Name,
            Display = b.Display.Length > 0 ? b.Display : a.Display,
            Job = b.Job.Length > 0 ? b.Job : a.Job,
            LimitBreak = b.LimitBreak || a.LimitBreak,
            Damage = dmg,
            Healed = healed,
            Shielded = a.Shielded + b.Shielded,
            Taken = a.Taken + b.Taken,
            Deaths = a.Deaths + b.Deaths,
            ADps = dmg > 0 ? (a.ADps * a.Damage + b.ADps * b.Damage) / dmg : b.ADps,
            CritPct = dmg > 0 ? (a.CritPct * a.Damage + b.CritPct * b.Damage) / dmg : b.CritPct,
            DirectHitPct = dmg > 0 ? (a.DirectHitPct * a.Damage + b.DirectHitPct * b.Damage) / dmg : b.DirectHitPct,
            CritDirectHitPct = dmg > 0
                ? (a.CritDirectHitPct * a.Damage + b.CritDirectHitPct * b.Damage) / dmg : b.CritDirectHitPct,
            OverhealPct = healed > 0 ? (a.OverhealPct * a.Healed + b.OverhealPct * b.Healed) / healed : b.OverhealPct,
            MaxHit = MaxHitValue(b.MaxHit) >= MaxHitValue(a.MaxHit) ? b.MaxHit : a.MaxHit,
        };
    }

    public static double MaxHitValue(string maxHit)
    {
        var dash = maxHit.LastIndexOf('-');
        return dash >= 0 && double.TryParse(maxHit[(dash + 1)..], out var v) ? v : 0;
    }

    // ---- rDPS ----

    private void ApplyRdps(MeterEncounter enc)
    {
        // The window covers the fight, padded for feed lag; wall time, since log lines span idle too.
        var wall = enc.WallSeconds > 0f ? enc.WallSeconds : enc.Seconds;
        var from = (_carry?.StartSec
                    ?? (enc.Active && _fightStartSec > 0 ? _fightStartSec : Engine.LatestSec - (long)wall)) - 2;
        var totals = Engine.WindowTotals(from);
        var seconds = Math.Max(1f, enc.Seconds);
        var you = LocalName();

        enc.RaidRDps = 0;
        foreach (var row in enc.Rows)
        {
            // The parser reports the local player as "YOU".
            if (string.Equals(row.Name, "YOU", StringComparison.OrdinalIgnoreCase) && you.Length > 0)
                row.Display = you;
            row.RDps = row.Dps;
            if (totals.TryGetValue(row.Display, out var t))
                row.RDps = Math.Max(0, row.Dps + (t.Given - t.Received) / seconds);
            enc.RaidRDps += row.RDps;
        }

        // A replay's engine holds, so only live pulls overlay it.
        if (!_replaying) OverlayEngineFacts(enc, Engine);
    }

    // Whether the engine matched every row, once the pull is over and the counts are final.
    // Asked mid-pull this says nothing: nobody has landed enough hits to have rolled anything yet.
    private void NoteAttribution(MeterEncounter enc)
    {
        if (!C.Diagnostics && !C.MeterDiagFile) return;
        var blind = "";
        foreach (var r in enc.Rows)
        {
            if (r.LimitBreak || r.Damage <= 0) continue;
            var who = r.Display.Length > 0 ? r.Display : r.Name;
            if (Engine.DealtFacts(who).Hits > 0) continue;
            // Both names, since the parser's raw one is what the display is derived from.
            blind += (blind.Length > 0 ? ", " : "")
                   + (string.Equals(who, r.Name, StringComparison.Ordinal) ? who : $"{who} (raw {r.Name})");
        }
        if (blind.Length == 0) { Note("every row matched the log lines"); return; }
        // The engine's own names beside them, so a mismatch reads instead of being guessed at.
        var had = "";
        foreach (var d in Engine.Dealers())
        {
            if (had.Length > 240) { had += ", ..."; break; }
            had += (had.Length > 0 ? ", " : "") + d;
        }
        Note($"unmatched at close - {blind}");
        // Who the plugin thinks you are, since the parser only ever says "YOU".
        Note($"engine had - {(had.Length > 0 ? had : "nothing")} (you = '{LocalName()}')");
    }

    // Engine counts replace the parser's running averages.
    public static void OverlayEngineFacts(MeterEncounter enc, RdpsEngine engine)
    {
        foreach (var row in enc.Rows)
        {
            var who = row.LimitBreak
                ? RdpsEngine.LimitBreakName
                : row.Display.Length > 0 ? row.Display : row.Name;
            var (hits, crits, dhs, cdhs, max, maxName) = engine.DealtFacts(who);
            if (hits > 0 && !row.LimitBreak)
            {
                row.CritPct = crits * 100.0 / hits;
                row.DirectHitPct = dhs * 100.0 / hits;
                row.CritDirectHitPct = cdhs * 100.0 / hits;
            }
            if (max > 0 && maxName.Length > 0) row.MaxHit = $"{maxName}-{(int)max}";
            var (landed, over) = engine.HealFacts(who);
            if (landed + over > 0) row.OverhealPct = over * 100.0 / (landed + over);
        }
    }

    // How often the party list is walked; it is eight entries, but not every frame.
    private DateTime _nextSeed = DateTime.MinValue;

    private void SeedAllies()
    {
        if (DateTime.UtcNow < _nextSeed) return;
        _nextSeed = DateTime.UtcNow.AddSeconds(2);
        try
        {
            foreach (var m in Service.PartyList)
                Engine.NoteAlly(m.EntityId, m.Name.ToString(), m.ClassJob.RowId);
        }
        catch (Exception ex) { Swallowed.Report("seed allies", ex); }
    }

    private string LocalName()
    {
        var name = Plugin.LocalPlayer?.Name.ToString() ?? "";
        return name.Length > 0 ? name : Engine.LocalPlayerName;
    }

    public void Clear(bool keepHistory = false)
    {
        Current = null;
        Previous = null;
        if (!keepHistory) History.Clear();
        _rawSeg = null;
        EndFight();
    }

    // A reset lines off the totals but keeps past pulls.
    public void ResetEncounter()
    {
        Clear(keepHistory: true);
        if (_rawIn != null) _cut = Snapshot(_rawIn);
    }

    // The board and everything behind it.
    public void ClearAll()
    {
        Clear();
        if (_rawIn != null) _cut = Snapshot(_rawIn);
    }

    // The link comes first: a dropped link used to report itself as connected.
    public string StatusText => !C.MeterEnabled ? "off"
        : Link.Status switch
        {
            MeterLink.LinkStatus.Ipc => FeedStale ? Quiet : "connected to the parser (in-process)",
            MeterLink.LinkStatus.Socket => FeedStale ? Quiet : "connected to ACT (WebSocket)",
            MeterLink.LinkStatus.Searching => "searching for a parser...",
            _ => "starting...",
        };

    private const string Quiet = "connected, but the parser has stopped sending - reconnecting";

    // The overlay's empty screen: what is wrong, then what is being done about it.
    public (string Line, string Note) StatusLines
    {
        get
        {
            if (!C.MeterEnabled) return ("Fren Meter is off.", "");
            return Link.Status switch
            {
                MeterLink.LinkStatus.Ipc or MeterLink.LinkStatus.Socket =>
                    FeedStale ? ("Connected, but can't find parser.", RetryNote) : ("", ""),
                MeterLink.LinkStatus.Searching => ("Can't find the parser.", RetryNote),
                _ => ("Starting...", ""),
            };
        }
    }

    // Naming the wait, so a still screen doesn't read as a stuck one.
    private string RetryNote => Link.RetryIn > 0 ? $"Reconnecting in {Link.RetryIn}s" : "Reconnecting...";

    public bool Connected => Link.Status is MeterLink.LinkStatus.Ipc or MeterLink.LinkStatus.Socket;

    // A steady sample pull, for placing and styling.
    private MeterEncounter? _sample;

    public MeterEncounter Sample()
    {
        if (_sample != null) return _sample;
        var e = new MeterEncounter
        {
            Title = "Kefka (sample)", Duration = "04:12", Seconds = 252f, Active = false,
        };
        var rows = new (string Name, string Job, double Dps, double Edge)[]
        {
            ("Riko Snowpetal", "PCT", 21460, 1.06),
            ("Auri Vale", "VPR", 20110, 0.94),
            ("Sable Marsh", "SAM", 19230, 0.97),
            ("Nophica Reed", "MCH", 18040, 1.02),
            ("Tia Windrun", "DRK", 12480, 1.05),
            ("Oren Bluewake", "GNB", 11930, 1.03),
            ("Mira Dawnfall", "WHM", 6540, 1.01),
            ("Lily Farsong", "SGE", 5810, 0.99),
        };
        foreach (var r in rows)
        {
            var c = new MeterCombatant
            {
                Name = r.Name, Display = r.Name, Job = r.Job,
                Dps = r.Dps, ADps = r.Dps * 1.08, RDps = r.Dps * r.Edge, Damage = r.Dps * e.Seconds,
                CritPct = 18 + r.Dps % 13, DirectHitPct = 22 + r.Dps % 21,
                CritDirectHitPct = 4 + r.Dps % 7,
                Hps = r.Job switch { "SGE" => 9840, "WHM" => 8630, _ => r.Dps % 900 },
                Healed = 0, OverhealPct = r.Job switch { "SGE" => 21, "WHM" => 24, _ => 4 },
                Taken = 42000 + r.Dps % 9000, Deaths = r.Job is "VPR" ? 1 : 0,
                MaxHit = $"Big One-{(int)(r.Dps * 6)}",
            };
            c.Healed = c.Hps * e.Seconds;
            // A shield healer's absorbs, already inside their healed.
            c.Shielded = r.Job switch { "SGE" => c.Healed * 0.38, "WHM" => c.Healed * 0.04, _ => 0 };
            e.TotalDps += c.Dps;
            e.RaidRDps += c.RDps;
            e.TotalHps += c.Hps;
            e.TotalTaken += c.Taken;
            e.TotalDeaths += c.Deaths;
            e.Rows.Add(c);
        }
        // The party's shared limit break, in its own row.
        var lb = new MeterCombatant
        {
            Name = "Limit Break", Display = "Limit Break", LimitBreak = true,
            Dps = 4820, ADps = 4820, RDps = 4820, Damage = 4820 * e.Seconds,
            MaxHit = "Dragonsong Dive-1214000",
        };
        e.TotalDps += lb.Dps;
        e.RaidRDps += lb.RDps;
        e.Rows.Add(lb);
        e.LimitBreakAction = SampleLimitBreak();
        e.TotalDamage = e.TotalDps * e.Seconds;
        var healedTotal = 0.0;
        foreach (var c in e.Rows) healedTotal += c.Healed;
        foreach (var c in e.Rows)
        {
            c.DamagePct = $"{c.Damage / e.TotalDamage * 100:0}%";
            c.HealedPct = healedTotal > 0 ? $"{c.Healed / healedTotal * 100:0}%" : "";
        }
        SampleBreakdowns(e);
        return _sample = e;
    }

    // A stand-in limit break, so Test mode has an icon.
    private static uint SampleLimitBreak()
    {
        var sheet = GameData.English<Lumina.Excel.Sheets.Action>();
        if (sheet == null) return 0;
        foreach (var row in sheet)
            if (row.ActionCategory.RowId == 9 && row.Name.ExtractText() == "Braver")
                return row.RowId;
        return 0;
    }

    // A sample breakdown, so the detail view can be styled.
    private static void SampleBreakdowns(MeterEncounter e)
    {
        // Shares of what a player dealt and took, by rank.
        var shares = new[] { 0.24, 0.19, 0.15, 0.12, 0.10, 0.08, 0.07, 0.05 };
        // Real actions, so the sample shows real icons.
        var byJob = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["PCT"] = new[] { "Star Prism", "Comet in Black", "Hammer Stamp", "Rainbow Drip",
                              "Holy in White", "Fire in Red", "Blizzard in Cyan", "Thunder in Magenta" },
            ["VPR"] = new[] { "Reawaken", "Ouroboros", "First Generation", "Uncoiled Fury",
                              "Flanksting Strike", "Hindsting Strike", "Steel Fangs", "Dread Fangs" },
            ["SAM"] = new[] { "Midare Setsugekka", "Ogi Namikiri", "Higanbana", "Shoha",
                              "Gekko", "Kasha", "Yukikaze", "Hakaze" },
            ["MCH"] = new[] { "Full Metal Field", "Chain Saw", "Excavator", "Air Anchor",
                              "Drill", "Heat Blast", "Clean Shot", "Split Shot" },
            ["RDM"] = new[] { "Resolution", "Scorch", "Verholy", "Verflare",
                              "Verthunder III", "Veraero III", "Grand Impact", "Jolt III" },
            ["DRK"] = new[] { "Torcleaver", "Disesteem", "Bloodspiller", "Edge of Shadow",
                              "Souleater", "Salted Earth", "Syphon Strike", "Hard Slash" },
            ["GNB"] = new[] { "Double Down", "Gnashing Fang", "Wicked Talon", "Savage Claw",
                              "Burst Strike", "Solid Barrel", "Brutal Shell", "Keen Edge" },
            ["SGE"] = new[] { "Pneuma", "Phlegma III", "Eukrasian Dosis III", "Dosis III",
                              "Toxikon II", "Dyskrasia II", "Psyche", "Eukrasia" },
            ["WHM"] = new[] { "Glare IV", "Afflatus Misery", "Assize", "Glare III",
                              "Dia", "Holy III", "Afflatus Rapture", "Stone IV" },
        };
        var generic = new[]
        {
            "Opener", "Burst finisher", "Filler", "Combo ender", "Combo starter",
            "Damage over time", "Off-global", "Ranged shot",
        };
        var hurt = new[] { "Cleave", "Raidwide", "Tank buster", "attack" };
        var cures = new[] { "Pneuma", "Eukrasian Prognosis", "Kerachole", "Physis II" };
        var buffs = new[]
        {
            "Divination", "Battle Litany", "Searing Light", "Embolden",
            "Technical Finish", "Radiant Finale", "Battle Voice",
        };

        // One player's share, split across the buffs behind it.
        AbilityStat Trade(string name, double amount, int seed) => new()
        {
            Name = name, Damage = amount,
            Parts = new List<AbilityStat>
            {
                new() { Name = buffs[seed % buffs.Length], Damage = amount * 0.58 },
                new() { Name = buffs[(seed + 3) % buffs.Length], Damage = amount * 0.27 },
                new() { Name = buffs[(seed + 5) % buffs.Length], Damage = amount * 0.15 },
            },
        };

        // A sample breakdown, so the limit break row opens.
        e.Dealt[RdpsEngine.LimitBreakName] = new List<AbilityStat>
        {
            new() { Name = "Braver", Damage = 4820 * e.Seconds, Hits = 1, Max = 4820 * e.Seconds },
        };

        foreach (var r in e.Rows)
        {
            if (r.LimitBreak) continue;
            var who = r.Display.Length > 0 ? r.Display : r.Name;
            var names = byJob.TryGetValue(r.Job, out var jobNames) ? jobNames : generic;
            var dealt = new List<AbilityStat>();
            for (var i = 0; i < shares.Length; i++)
                dealt.Add(new AbilityStat
                {
                    Name = names[i], Damage = r.Damage * shares[i],
                    Hits = 6 + i * 7, Crits = 2 + i * 2, Dhs = 3 + i * 2,
                    Max = r.Damage * shares[i] / (4 + i),
                });
            e.Dealt[who] = dealt;

            e.Targets[who] = new List<AbilityStat>
            {
                new() { Name = "Kefka", Damage = r.Damage * 0.82, Hits = 74, Max = r.Damage * 0.04 },
                new() { Name = "Lingering Spirit", Damage = r.Damage * 0.18, Hits = 12, Max = r.Damage * 0.03 },
            };

            var taken = new List<AbilityStat>();
            for (var i = 0; i < hurt.Length; i++)
                taken.Add(new AbilityStat
                {
                    Name = hurt[i], Damage = r.Taken * (0.42 - i * 0.09),
                    Hits = 2 + i * 6, Max = r.Taken * 0.3,
                });
            e.Taken[who] = taken;

            var heals = new List<AbilityStat>();
            for (var i = 0; i < cures.Length; i++)
                heals.Add(new AbilityStat
                {
                    Name = cures[i], Damage = r.Healed * (0.38 - i * 0.11),
                    Over = r.Healed * (0.05 + i * 0.04), Hits = 4 + i * 9,
                    Max = r.Healed * 0.06,
                });
            if (r.Healed > 0) e.Heals[who] = heals;
        }

        // Healing and buff credit only make sense across the party.
        foreach (var r in e.Rows)
        {
            if (r.LimitBreak) continue;
            var who = r.Display.Length > 0 ? r.Display : r.Name;
            var healed = new List<AbilityStat>();
            var from = new List<AbilityStat>();
            var given = new List<AbilityStat>();
            var got = new List<AbilityStat>();
            var i = 0;
            foreach (var other in e.Rows)
            {
                if (other.LimitBreak) continue;
                var name = other.Display.Length > 0 ? other.Display : other.Name;
                if (string.Equals(name, who, StringComparison.OrdinalIgnoreCase)) continue;
                var share = 0.34 - i * 0.04;
                if (r.Healed > 0)
                    healed.Add(new AbilityStat { Name = name, Damage = r.Healed * share, Over = r.Healed * share * 0.14, Hits = 9 + i * 3 });
                if (other.Healed > 0)
                    from.Add(new AbilityStat { Name = name, Damage = other.Healed * share * 0.4, Hits = 7 + i });
                given.Add(Trade(name, r.Damage * 0.012 * (1.4 - i * 0.12), i));
                got.Add(Trade(name, other.Damage * 0.011 * (1.3 - i * 0.1), i + 2));
                i++;
            }
            if (healed.Count > 0) e.HealTargets[who] = healed;
            if (from.Count > 0) e.HealFrom[who] = from;
            e.Given[who] = given;
            e.Received[who] = got;

            if (r.Deaths > 0)
                e.Deaths.Add(new DeathRecord
                {
                    Name = who, At = 168f, Killer = "Tank buster", KillingBlow = r.Taken * 0.42,
                    Lead =
                    {
                        new DeathHit { Name = "Raidwide", Amount = r.Taken * 0.21, Sec = -6 },
                        new DeathHit { Name = "Cure III", Amount = r.Taken * 0.3, Sec = -4, Heal = true },
                        new DeathHit { Name = "Cleave", Amount = r.Taken * 0.18, Sec = -2 },
                    },
                });
        }
    }

    public void Dispose()
    {
        _mdiag.Flush();
        Link.Dispose();
    }
}
