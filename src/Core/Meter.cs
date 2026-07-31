using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FrenMits;

// Fren Meter's brain: drains the parser link, feeds the rDPS engine, and keeps
// the current encounter plus history for the overlay.
//
// The parser splits an encounter whenever the log goes quiet, which happens
// mid-boss in every downtime phase. Fights are stitched back together here:
// while the game still says the party is in combat with a boss, a "new"
// parser encounter is a continuation, its segments summed. Dungeon trash
// resets per pack as normal, and anything worth looking back at is kept.
public class Meter : IDisposable
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public MeterLink Link { get; }
    public RdpsEngine Engine { get; } = new();

    public MeterEncounter? Current { get; private set; }
    public List<MeterEncounter> History { get; } = new();
    // Enough for a whole duty's bosses, or an evening of prog pulls.
    private const int MaxHistory = 30;

    // Freezes the display (log lines keep flowing so rDPS stays honest).
    public bool Paused { get; set; }

    // The prior update of the running pull, so the overlay can glide between
    // the parser's once-a-second ticks instead of jumping.
    public MeterEncounter? Previous { get; private set; }
    public DateTime CurrentAt { get; private set; }
    public float LerpSpan { get; private set; } = 1f;

    // The fight being stitched across parser splits.
    private FightCarry? _carry;
    private MeterEncounter? _rawSeg;
    private long _fightStartSec;
    private string _fightTitle = "";
    private bool _sawBoss;
    private float _bossLeft = -1f;
    private int _standing = -1;

    // How a pull finished. A wipe is the party going down, which is the only
    // thing that says so for certain: enemy health cannot, because a dungeon
    // pack is several enemies and killing one leaves the next at full. A kill
    // is the other way round, the enemy at zero with the party still up.
    // Anything else stays unknown, and the list says nothing at all.
    public static PullEnd EndOf(int standing, float bossLeft)
    {
        if (standing == 0) return PullEnd.Wipe;
        if (bossLeft == 0f) return PullEnd.Kill;
        return PullEnd.Unknown;
    }

    // The boss reading a pull should keep. While the party is in combat the
    // newest one always wins, so a fight that changes boss between phases
    // follows it. Once combat drops only a lower reading counts: that is the
    // killing blow landing, where a higher one is the boss walking back to full
    // after a wipe.
    public static float TrackBoss(float current, float reading, bool inCombat)
    {
        if (reading < 0f) return current;
        if (inCombat) return reading;
        return current < 0f || reading < current ? reading : current;
    }

    // Whether the readout is due for a fresh set of numbers. Values that move
    // every frame are unreadable, so they are taken on a cadence and held
    // still in between; a rate of zero is the old every-frame behavior.
    public static bool DueToRefresh(double since, float rate)
        => rate <= 0f || since >= rate || since < 0;

    // A pull is only over once combat has stayed off for a beat with the game
    // back in the player's hands. The flag alone says nothing: every phase
    // cutscene drops it mid-fight, and filing there would leave one pull spread
    // across several entries in the list.
    public const double SettleSeconds = 1.5;

    public static bool SettleDue(bool inCombat, bool cutscene, double sinceDrop)
        => !inCombat && !cutscene && sinceDrop > SettleSeconds;

    private DateTime _nextTrim = DateTime.MinValue;

    public Meter(Plugin plugin)
    {
        _plugin = plugin;
        Link = new MeterLink(plugin);
        Engine.IsLimitBreak = IsLimitBreak;
    }

    // Limit break action ids from the game sheet (category 9), resolved lazily
    // because sheets are not ready at load.
    private HashSet<uint>? _lbActions;

    private bool IsLimitBreak(uint actionId)
    {
        if (_lbActions == null)
        {
            var sheet = GameSheets.English<Lumina.Excel.Sheets.Action>();
            if (sheet == null) return false; // not ready: retry on a later hit
            var set = new HashSet<uint>();
            foreach (var row in sheet)
                if (row.ActionCategory.RowId == 9)
                    set.Add(row.RowId);
            _lbActions = set;
        }
        return _lbActions.Contains(actionId);
    }

    public void Update()
    {
        if (!C.MeterEnabled)
        {
            Link.EnsureStopped();
            return;
        }
        Link.EnsureStarted();

        var budget = 5000;
        while (budget-- > 0 && Link.TryDequeue(out var msg)) Handle(msg);

        // The world the stitch reads, taken once a frame. A replay hands it the
        // same values the recording was made under, so the decisions it makes
        // are the ones it made in the fight.
        _inCombat = Plugin.InCombat;
        _cutscene = Plugin.CutsceneActive;

        if (!_replaying) CheckFeedAlive();

        // A boss on the field marks this fight as one worth stitching and keeping.
        if (_inCombat && _plugin.BossHpFraction >= 0f) _sawBoss = true;
        _bossLeft = TrackBoss(_bossLeft, _plugin.BossHpFraction, _inCombat);
        // The last word on who was still up, taken while the fight was still on.
        // A cutscene can empty the object table with the party perfectly alive,
        // so nothing is counted through one.
        if (_inCombat && !_cutscene && _plugin.PlayersStanding >= 0)
            _standing = _plugin.PlayersStanding;

        // Combat over: close this fight right away instead of waiting out the
        // parser's idle timeout. That is what makes each trash pack in a
        // dungeon start from zero, and freezes a kill at the killing blow.
        var inCombat = _inCombat;
        if (!inCombat && _wasInCombat) _combatDropAt = DateTime.UtcNow;
        // A cutscene between phases drops combat without ending the pull, and
        // filing one there splits a single fight across several entries in the
        // list. The settle only starts once the game hands control back.
        if (_cutscene) _combatDropAt = DateTime.UtcNow;
        if (inCombat) _cutDone = false;
        _wasInCombat = inCombat;
        var sinceDrop = _combatDropAt == DateTime.MinValue
            ? 0.0 : (DateTime.UtcNow - _combatDropAt).TotalSeconds;
        var settle = SettleDue(inCombat, _cutscene, sinceDrop);

        // A stitched fight that ended inside the quiet gap (a wipe during
        // downtime): no further segment is coming, settle it once combat has
        // really gone.
        if (!Paused && _carry != null && _rawSeg is not { Active: true } && settle)
        {
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

        if (!_cutDone && !Paused && _rawSeg is { Active: true } && settle)
        {
            _cutDone = true;
            CutHere();
        }

        // The active profile follows every tweak by itself; no manual save.
        if (DateTime.UtcNow >= _nextProfileSync)
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
        }
    }

    private DateTime _nextProfileSync = DateTime.MinValue;
    private DateTime _combatDropAt = DateTime.MinValue;
    private bool _wasInCombat;
    private bool _cutDone;

    private void Handle(JObject msg)
    {
        if (string.Equals(msg["type"]?.ToString(), "LogLine", StringComparison.Ordinal))
        {
            if (msg["line"] is JArray arr)
            {
                var line = new string[arr.Count];
                for (var i = 0; i < arr.Count; i++) line[i] = arr[i]?.ToString() ?? "";
                Engine.Process(line);
            }
            return;
        }

        if (Paused) return;
        if (MeterEncounter.Parse(msg) is not { Rows.Count: > 0 } raw) return;
        OnSummary(raw);
    }

    // The world the stitch reads. Live these follow the game; through a replay
    // they are whatever the recording says they were.
    private bool _inCombat;
    private bool _cutscene;
    private bool _replaying;

    // ---- is the feed even alive? -------------------------------------------
    //
    // A parser link can die without disconnecting. The socket stays open, the
    // summaries keep arriving once a second, and every one of them repeats an
    // encounter that ended some pulls ago - the board goes on showing numbers
    // that stopped being about this fight, and nothing says so. A recorded
    // Dancing Mad kill came back as 1111 identical messages.
    //
    // Two things have to be quiet at once for that to be the case: the parser's
    // own totals, and the damage this plugin counts off the log lines itself.
    // A real fight cannot leave both untouched.
    public const double StaleAfterSeconds = 15;

    // A fight is on and the numbers have stopped being about it.
    public static bool FeedIsStale(bool fightOn, double sinceFresh)
        => fightOn && sinceFresh > StaleAfterSeconds;

    public bool FeedStale { get; private set; }

    // A replay is the other way a fight can be underway with nothing arriving:
    // the parser is not fed by a duty recording at all, so the board sits on
    // whatever real pull came last. Worth saying plainly rather than leaving
    // someone reading an old pull's numbers over a replay of a new one.
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
        // Counting the engine's whole table is not a per-frame job.
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
            _plugin.Diag.Note(stale
                ? FeedStaleInReplay
                    ? "meter: a replay feeds the parser nothing - the board is still showing the last real pull"
                    : "meter: the parser feed has gone quiet mid-fight - what is on the board is not this pull"
                : "meter: parser feed is live again");
            Service.Log.Warning(stale
                ? "[FrenMits] Meter: no new parser data and no damage counted for "
                  + $"{StaleAfterSeconds:0}s{(replay ? " of playback" : " of combat")}."
                : "[FrenMits] Meter: parser feed recovered.");
        }

        // Ask for the subscription again: the parser can drop a listener and
        // leave the socket open, and a reconnect is the only way back. A replay
        // is not a broken link, so it is left alone.
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
            m.Rows.Add((r.Name, r.Job, r.Damage, r.Healed, r.Taken, r.Deaths));
        MeterFeed.Record(m);
    }

    // Run a recorded feed back through the real stitch and report what it makes
    // of it, against what the log lines said at the time.
    public string Replay(string path)
    {
        List<MeterFeed.Message> feed;
        try { feed = MeterFeed.Load(path); }
        catch (Exception ex) { return $"could not read that recording: {ex.Message}"; }
        if (feed.Count == 0) return "that recording is empty";

        var wasRecording = MeterFeed.Recording;
        var keepCombat = _inCombat;
        var keepCutscene = _cutscene;
        var keepBoss = _sawBoss;
        MeterFeed.Pause();
        _replaying = true;
        Clear(keepHistory: true);
        // Clear baselines against whatever the parser last said live; a replay
        // starts from nothing, the way the recorded pull did.
        _cut = null;
        _rawIn = null;

        var logLines = 0.0;
        foreach (var m in feed)
        {
            _inCombat = m.InCombat;
            _cutscene = m.Cutscene;
            _sawBoss = m.SawBoss;
            logLines = Math.Max(logLines, m.LogLines);
            OnSummary(MeterFeed.ToEncounter(m));
        }

        var shown = Current?.TotalDamage ?? 0;
        _replaying = false;
        _inCombat = keepCombat;
        _cutscene = keepCutscene;
        _sawBoss = keepBoss;
        if (wasRecording) MeterFeed.Resume();

        var drift = logLines > 0 ? shown / logLines : 0;
        return $"{feed.Count} summaries: the board makes it {shown / 1e6:0.0}M, "
             + $"the log lines {logLines / 1e6:0.0}M"
             + (logLines > 0 ? $" ({drift:0.00}x)" : "");
    }

    private void OnSummary(MeterEncounter incoming)
    {
        RecordFeed(incoming);
        // Only a summary that says something new counts as the feed being alive.
        var sig = (incoming.Active, incoming.Seconds, incoming.TotalDamage);
        if (sig != _feedSig) { _feedSig = sig; FeedIsFresh(); }
        _rawIn = incoming;
        var raw = incoming;
        var restarted = false;
        if (_cut != null)
        {
            // The parser starting its own new encounter retires the cut.
            if (!CutStillHolds(raw.TotalDamage, _cut.Damage))
            {
                _plugin.Diag.Note($"meter: parser restarted (damage {raw.TotalDamage / 1e6:0.0}M "
                                + $"under the cut's {_cut.Damage / 1e6:0.0}M); baseline dropped");
                _cut = null;
                restarted = true;
            }
            else
            {
                raw = Subtract(incoming, _cut);
                // A pull starts when damage does. Until then the cut slides
                // along, so neither the wait after a wipe nor the healing that
                // follows one lands on the next pull's clock.
                if (raw.TotalDamage <= 0)
                {
                    _cut = Snapshot(incoming);
                    return;
                }
            }
        }

        if (raw.Active)
        {
            var continuing = _rawSeg is { Active: true } && raw.Seconds + 0.5f >= _rawSeg.Seconds;
            if (!continuing)
            {
                // A segment ended without its final update: settle it first.
                //
                // Which one gets banked matters. Banking is what moves the
                // baseline, so bank what THIS message shows and start the live
                // segment from here. Banking the previous message instead left
                // the stretch between the two in the bank and on screen at once,
                // and the fight grew by that much at every lull.
                if (_rawSeg is { Active: true })
                {
                    if (restarted)
                    {
                        // Nothing of the parser's new encounter is banked yet, so
                        // the old encounter's leftovers go in and this message
                        // stands as the whole of the new one.
                        EndSegment(_rawSeg);
                        _cut = null;
                    }
                    else
                    {
                        EndSegment(raw);
                        if (_cut != null) raw = Subtract(incoming, _cut);
                    }
                }
                if (_carry == null)
                {
                    _fightStartSec = Math.Max(0, Engine.LatestSec - (long)raw.Seconds);
                    _fightTitle = ""; // a fresh fight names itself from scratch
                    _sawBoss = false;
                }
            }
            SetTitle(raw);
            _rawSeg = raw;
            Publish(Merge(_carry, raw));
            return;
        }

        SetTitle(raw);

        // The segment's final numbers.
        if (_rawSeg is { Active: true })
        {
            _rawSeg = raw;
            EndSegment(raw);
        }
        else
            _rawSeg = raw;
    }

    private void EndSegment(MeterEncounter final)
    {
        var display = Merge(_carry, final);
        if (_inCombat && _sawBoss)
        {
            // Mid-boss split (downtime): stitch, and keep reading as a live fight.
            _carry ??= new FightCarry { StartSec = _fightStartSec, Title = _fightTitle };
            Fold(_carry, final, Engine.LatestSec);
            // This segment is banked now, so the parser has to be measured from
            // here on. Without it, a parser that resumes its old encounter
            // instead of starting a new one hands back totals this fight has
            // already counted, and every split doubles the clock.
            if (_rawIn != null) _cut = Snapshot(_rawIn);
            // The engine's own figure is counted straight off the log lines, one
            // event at a time, so it cannot double count. Printed beside the
            // parser's running total, a stitch that has gone wrong shows itself.
            _plugin.Diag.Note($"meter: banked segment {final.Seconds:0}s {final.TotalDamage / 1e6:0.0}M; "
                            + $"fight now {_carry.Seconds:0}s {Total(_carry) / 1e6:0.0}M "
                            + $"(log lines say {EngineTotal() / 1e6:0.0}M)");
            display.Active = true;
            Publish(display);
            return;
        }

        display.Active = false;
        Materialize(display);
        Publish(display);
        if (WorthKeeping(display)) PushHistory(display);
        EndFight();
    }

    // A finished pull carries its own breakdowns, so looking back at it later
    // does not depend on the engine still holding that fight.
    private void Materialize(MeterEncounter enc)
    {
        enc.BossLeft = _bossLeft;
        enc.Ended = EndOf(_standing, _bossLeft);

        // The parser calls the local player "YOU", and only publishing the pull
        // turns that into their name. Resolve it here as well, or their own
        // breakdown ends up filed under a name nothing later looks it up by.
        var you = LocalName();
        foreach (var r in enc.Rows)
        {
            var who = you.Length > 0 && string.Equals(r.Name, "YOU", StringComparison.OrdinalIgnoreCase)
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

    // Copies, not the engine's own rows: it keeps tallying into those, and a
    // finished pull must never move again.
    public static List<AbilityStat> Freeze(List<AbilityStat> live)
    {
        var copy = new List<AbilityStat>(live.Count);
        foreach (var a in live)
            copy.Add(new AbilityStat
            {
                Name = a.Name, Hits = a.Hits, Crits = a.Crits,
                Dhs = a.Dhs, Damage = a.Damage, Max = a.Max, Over = a.Over,
                Id = a.Id, IsStatus = a.IsStatus,
                Parts = a.Parts is { } parts ? Freeze(parts) : null,
            });
        return copy;
    }

    // The same for a death, with the log's clock turned into fight time.
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

    // A pull that carries its own breakdowns is finished, and the engine has
    // long since moved on: never fall back to live data for it.
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
        // The parser's encounter can outlive the pull: it goes on counting
        // through a wipe and, if it never times out, carries those totals into
        // the next pull as one 20-minute fight that barely moves. Baselining
        // here means the next pull starts from zero whether the parser starts
        // a new encounter or not; if it does, the cut retires itself.
        if (_rawIn != null) _cut = Snapshot(_rawIn);
        _carry = null;
        _fightStartSec = 0;
        _fightTitle = "";
        _sawBoss = false;
        _bossLeft = -1f;
        _standing = -1;
        _warnedAt = 0;
        _seenLines = 0;   // the engine's table is cleared below, so its count restarts
        // Clear when a fight ENDS, not when the next one starts: the summary
        // feed lags the log, so clearing on arrival would eat the opener.
        Engine.ClearBreakdown();
    }

    // Worth looking back at: a boss, or anything that ran long enough that it
    // cannot have been a trash pack. The boss test reads a raid-sized health
    // bar, which a duty boss below the level cap never has, so duration is what
    // keeps those in the list.
    public const float HistoryMinSeconds = 25f;

    public static bool WorthKeeping(bool sawBoss, float seconds)
        => sawBoss || seconds >= HistoryMinSeconds;

    private bool WorthKeeping(MeterEncounter enc) => WorthKeeping(_sawBoss, enc.Seconds);

    private void PushHistory(MeterEncounter enc)
    {
        if (enc.Rows.Count == 0) return;
        History.Insert(0, enc);
        while (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);
    }

    // The parser calls every fight "Encounter" until it ends. Until it gives
    // the real name, the boss actually on the field beats whoever got tagged
    // first, and any target name beats the placeholder.
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

    // How far the parser's running total may sit above the plugin's own count of
    // the same log lines before something has gone wrong with the stitch.
    private const double DriftWarnAbove = 1.10;
    private double _warnedAt;

    // The two totals come from completely separate paths: the parser's summary,
    // and the engine adding up damage events one at a time. They should track.
    // When they don't, the pull record says so rather than the board quietly
    // showing a fight that did half again the damage it really did.
    private void CheckAgainstLogLines(MeterEncounter enc)
    {
        if (_replaying) return;   // the engine holds this session, not the recording
        var counted = EngineTotal();
        if (counted <= 0 || enc.TotalDamage <= counted * DriftWarnAbove) return;
        if (enc.TotalDamage < _warnedAt * 1.05) return;   // once per step, not per frame
        _warnedAt = enc.TotalDamage;
        _plugin.Diag.Note($"meter: DRIFT - showing {enc.TotalDamage / 1e6:0.0}M but the log lines "
                        + $"only account for {counted / 1e6:0.0}M");
    }

    private void Publish(MeterEncounter enc)
    {
        ApplyRdps(enc);
        if (enc.Active) CheckAgainstLogLines(enc);
        // Glide only within the same running fight; anything else snaps.
        Previous = enc.Active && Current is { Active: true } cur
                   && cur.Title == enc.Title && enc.Seconds + 0.5f >= cur.Seconds
            ? Current
            : null;
        if (Previous != null)
            LerpSpan = Math.Clamp((float)(DateTime.UtcNow - CurrentAt).TotalSeconds, 0.25f, 1.5f);
        CurrentAt = DateTime.UtcNow;
        Current = enc;
    }

    // ---- segment stitching -------------------------------------------------

    public sealed class FightCarry
    {
        public long StartSec;
        public float Seconds;
        public string Title = "";
        public Dictionary<string, MeterCombatant> Rows { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    // Bank a finished segment's numbers into the running fight. A stitched
    // fight leaves the gaps between its segments out, so its clock can never
    // pass the time actually elapsed since it started: that ceiling is what
    // stops a miscounted segment from compounding into an hour-long pull.
    public static void Fold(FightCarry carry, MeterEncounter final, long nowSec = 0)
    {
        foreach (var r in final.Rows)
            carry.Rows[r.Name] = Combine(carry.Rows.GetValueOrDefault(r.Name), r);
        carry.Seconds += final.Seconds;
        if (nowSec > 0 && carry.StartSec > 0)
            carry.Seconds = Math.Min(carry.Seconds, Math.Max(0, nowSec - carry.StartSec));
        if (final.Title.Length > 0) carry.Title = final.Title;
    }

    // Everything the engine has counted from the log lines this fight.
    private double EngineTotal()
    {
        var sum = 0.0;
        foreach (var name in Engine.Dealers())
            foreach (var a in Engine.Dealt(name))
                sum += a.Damage;
        return sum;
    }

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
        var e = new MeterEncounter
        {
            Title = seg.Title.Length > 0 ? seg.Title : carry.Title,
            Active = seg.Active,
            Seconds = secs,
            Duration = $"{(int)secs / 60:00}:{(int)secs % 60:00}",
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
        foreach (var r in e.Rows)
        {
            e.TotalHps += r.Hps;
            r.DamagePct = e.TotalDamage > 0 ? $"{r.Damage / e.TotalDamage * 100:0}%" : "";
        }
        return e;
    }

    // ---- cutting the parser's running encounter ----------------------------

    // Where the meter last drew a line under the parser's totals. Everything
    // before it is subtracted out, so a new pull starts from zero without
    // asking the parser to end anything.
    public sealed class Baseline
    {
        public float Seconds;
        public double Damage;
        public Dictionary<string, MeterCombatant> Rows { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private Baseline? _cut;
    private MeterEncounter? _rawIn;

    // A cut only means anything while the parser is still on the encounter it
    // was taken from, and its totals are what say so: damage only ever climbs
    // inside one encounter, so a drop is the parser starting over.
    //
    // The encounter clock cannot answer this. The parser trims idle time off an
    // encounter, so the duration steps BACKWARDS while the same fight carries
    // on - which read as a new encounter, dropped the baseline, and handed back
    // totals already banked. Every lull then re-counted the fight from the top:
    // a Dancing Mad pull came out at five times the damage it really did.
    public static bool CutStillHolds(double parserDamage, double cutDamage)
        => parserDamage + 1.0 >= cutDamage;

    public static Baseline Snapshot(MeterEncounter raw)
    {
        var b = new Baseline { Seconds = raw.Seconds, Damage = raw.TotalDamage };
        foreach (var r in raw.Rows) b.Rows[r.Name] = r;
        return b;
    }

    // Close the running fight here and start the next one from zero.
    private void CutHere()
    {
        if (_rawSeg is { Active: true } seg)
        {
            var display = Merge(_carry, seg);
            display.Active = false;
            Materialize(display);
            Publish(display);
            if (WorthKeeping(display)) PushHistory(display);
            EndFight();
        }
        if (_rawIn != null) _cut = Snapshot(_rawIn);
        _rawSeg = null;
    }

    // The parser's totals with everything before the cut taken back out.
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
                Damage = Math.Max(0, r.Damage - (b?.Damage ?? 0)),
                Healed = Math.Max(0, r.Healed - (b?.Healed ?? 0)),
                Taken = Math.Max(0, r.Taken - (b?.Taken ?? 0)),
                Deaths = Math.Max(0, r.Deaths - (b?.Deaths ?? 0)),
                // Rates are running averages the parser never breaks down, so
                // they carry over as they stand.
                CritPct = r.CritPct, DirectHitPct = r.DirectHitPct, OverhealPct = r.OverhealPct,
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
        foreach (var r in e.Rows)
            r.DamagePct = e.TotalDamage > 0 ? $"{r.Damage / e.TotalDamage * 100:0}%" : "";
        return e;
    }

    // Two chunks of the same player's fight, damage-weighted where it matters.
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
            Damage = dmg,
            Healed = healed,
            Taken = a.Taken + b.Taken,
            Deaths = a.Deaths + b.Deaths,
            ADps = dmg > 0 ? (a.ADps * a.Damage + b.ADps * b.Damage) / dmg : b.ADps,
            CritPct = dmg > 0 ? (a.CritPct * a.Damage + b.CritPct * b.Damage) / dmg : b.CritPct,
            DirectHitPct = dmg > 0 ? (a.DirectHitPct * a.Damage + b.DirectHitPct * b.Damage) / dmg : b.DirectHitPct,
            OverhealPct = healed > 0 ? (a.OverhealPct * a.Healed + b.OverhealPct * b.Healed) / healed : b.OverhealPct,
            MaxHit = MaxHitValue(b.MaxHit) >= MaxHitValue(a.MaxHit) ? b.MaxHit : a.MaxHit,
        };
    }

    public static double MaxHitValue(string maxHit)
    {
        var dash = maxHit.LastIndexOf('-');
        return dash >= 0 && double.TryParse(maxHit[(dash + 1)..], out var v) ? v : 0;
    }

    // ---- rDPS --------------------------------------------------------------

    private void ApplyRdps(MeterEncounter enc)
    {
        // The window reaches back over the whole stitched fight, downtime
        // included; a small pad absorbs the summary feed lagging the lines.
        var from = (_carry?.StartSec
                    ?? (enc.Active && _fightStartSec > 0 ? _fightStartSec : Engine.LatestSec - (long)enc.Seconds)) - 2;
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

    // A mid-combat reset draws a line under the parser's running totals, or
    // they would just repopulate the meter one second later. Past pulls stay:
    // starting the board over is not a reason to lose the bosses behind it.
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

    public string StatusText => !C.MeterEnabled ? "off"
        : FeedStale ? "connected, but the parser has stopped sending - reconnecting"
        : Link.Status switch
        {
            MeterLink.LinkStatus.Ipc => "connected to the parser (in-process)",
            MeterLink.LinkStatus.Socket => "connected to ACT (WebSocket)",
            MeterLink.LinkStatus.Searching => "searching for a parser...",
            _ => "starting...",
        };

    public bool Connected => Link.Status is MeterLink.LinkStatus.Ipc or MeterLink.LinkStatus.Socket;

    // A steady sample pull so the overlay can be placed and styled from Test mode.
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
            ("Ember Halcyon", "RDM", 17110, 1.11),
            ("Tia Windrun", "DRK", 12480, 1.05),
            ("Oren Bluewake", "GNB", 11930, 1.03),
            ("Lily Farsong", "SGE", 5810, 0.99),
        };
        foreach (var r in rows)
        {
            var c = new MeterCombatant
            {
                Name = r.Name, Display = r.Name, Job = r.Job,
                Dps = r.Dps, ADps = r.Dps * 1.08, RDps = r.Dps * r.Edge, Damage = r.Dps * e.Seconds,
                CritPct = 18 + r.Dps % 13, DirectHitPct = 22 + r.Dps % 21,
                Hps = r.Job is "SGE" ? 9840 : r.Dps % 900,
                Healed = 0, OverhealPct = r.Job is "SGE" ? 21 : 4,
                Taken = 42000 + r.Dps % 9000, Deaths = r.Job is "VPR" ? 1 : 0,
                MaxHit = $"Big One-{(int)(r.Dps * 6)}",
            };
            c.Healed = c.Hps * e.Seconds;
            e.TotalDps += c.Dps;
            e.RaidRDps += c.RDps;
            e.TotalHps += c.Hps;
            e.TotalTaken += c.Taken;
            e.TotalDeaths += c.Deaths;
            e.Rows.Add(c);
        }
        e.TotalDamage = e.TotalDps * e.Seconds;
        foreach (var c in e.Rows)
            c.DamagePct = $"{c.Damage / e.TotalDamage * 100:0}%";
        SampleBreakdowns(e);
        return _sample = e;
    }

    // Give the sample pull a breakdown too, so the detail view can be placed
    // and styled from Test mode like everything else.
    private static void SampleBreakdowns(MeterEncounter e)
    {
        // Shares of a player's damage, and of what they took, by rank.
        var shares = new[] { 0.24, 0.19, 0.15, 0.12, 0.10, 0.08, 0.07, 0.05 };
        // Real actions, so the sample shows real icons and colors too.
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

        // One player's share of the trade, split across the buffs behind it.
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

        foreach (var r in e.Rows)
        {
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

        // Healing and buff credit only make sense across the party, so both are
        // shared out from everyone else's rows.
        foreach (var r in e.Rows)
        {
            var who = r.Display.Length > 0 ? r.Display : r.Name;
            var healed = new List<AbilityStat>();
            var from = new List<AbilityStat>();
            var given = new List<AbilityStat>();
            var got = new List<AbilityStat>();
            var i = 0;
            foreach (var other in e.Rows)
            {
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

    public void Dispose() => Link.Dispose();
}
