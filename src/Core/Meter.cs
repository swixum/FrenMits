using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FrenMits;

// Drains the parser link, feeds the rDPS engine, and keeps the pull on screen
// plus the history behind it.
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
    private float _bossLeft = -1f;
    private int _standing = -1;

    // How a pull finished: the party down is a wipe, the enemy at zero a kill.
    public static PullEnd EndOf(int standing, float bossLeft)
    {
        if (standing == 0) return PullEnd.Wipe;
        if (bossLeft == 0f) return PullEnd.Kill;
        return PullEnd.Unknown;
    }

    // The boss reading to keep: the newest in combat, the lowest once it drops.
    public static float TrackBoss(float current, float reading, bool inCombat)
    {
        if (reading < 0f) return current;
        if (inCombat) return reading;
        return current < 0f || reading < current ? reading : current;
    }

    // Whether the readout is due a fresh set of numbers, zero meaning every frame.
    public static bool DueToRefresh(double since, float rate)
        => rate <= 0f || since >= rate || since < 0;

    // A pull is only over once combat has stayed off with control handed back.
    public const double SettleSeconds = 1.5;

    public static bool SettleDue(bool inCombat, bool cutscene, double sinceDrop)
        => !inCombat && !cutscene && sinceDrop > SettleSeconds;

    private DateTime _nextTrim = DateTime.MinValue;

    public Meter(Plugin plugin)
    {
        _plugin = plugin;
        Link = new MeterLink(plugin);
        Engine.IsLimitBreak = IsLimitBreak;
        // English-sheet lookups so localized logs still match by id.
        Engine.ResolveStatusIds = StatusIdsOf;
        Engine.ResolveActionIds = ActionIdsOf;
    }

    // One pass per sheet keeps the engine's lookups cheap.
    private static Dictionary<string, List<uint>>? _statusIds;
    private static Dictionary<string, List<uint>>? _actionIds;
    private static readonly List<uint> NoIds = new();

    private static List<uint>? StatusIdsOf(string english)
    {
        if (_statusIds == null)
        {
            var sheet = GameSheets.English<Lumina.Excel.Sheets.Status>();
            if (sheet == null) return null;
            var map = new Dictionary<string, List<uint>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in sheet)
            {
                var name = row.Name.ExtractText();
                if (name.Length == 0) continue;
                (map.TryGetValue(name, out var list) ? list : map[name] = new List<uint>()).Add(row.RowId);
            }
            _statusIds = map;
        }
        return _statusIds.TryGetValue(english, out var ids) ? ids : NoIds;
    }

    private static List<uint>? ActionIdsOf(string english)
    {
        if (_actionIds == null)
        {
            var sheet = GameSheets.English<Lumina.Excel.Sheets.Action>();
            if (sheet == null) return null;
            var map = new Dictionary<string, List<uint>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in sheet)
            {
                var name = row.Name.ExtractText();
                if (name.Length == 0) continue;
                (map.TryGetValue(name, out var list) ? list : map[name] = new List<uint>()).Add(row.RowId);
            }
            _actionIds = map;
        }
        return _actionIds.TryGetValue(english, out var ids) ? ids : NoIds;
    }

    // Limit break action ids from the game sheet, resolved once sheets are up.
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

        // The world the stitch reads, taken once a frame.
        _inCombat = Plugin.InCombat;
        _cutscene = Plugin.CutsceneActive;

        if (!_replaying) CheckFeedAlive();

        // A boss on the field marks this fight as one worth stitching and keeping.
        if (_inCombat && _plugin.BossHpFraction >= 0f) _sawBoss = true;
        _bossLeft = TrackBoss(_bossLeft, _plugin.BossHpFraction, _inCombat);
        // The last word on who was up, never counted through a cutscene.
        if (_inCombat && !_cutscene && _plugin.PlayersStanding >= 0)
            _standing = _plugin.PlayersStanding;

        // Combat over: close the fight here rather than wait out the parser.
        var inCombat = _inCombat;
        if (!inCombat && _wasInCombat) _combatDropAt = DateTime.UtcNow;
        // A cutscene between phases drops combat without ending the pull.
        if (_cutscene) _combatDropAt = DateTime.UtcNow;
        if (inCombat) _cutDone = false;
        _wasInCombat = inCombat;
        var sinceDrop = _combatDropAt == DateTime.MinValue
            ? 0.0 : (DateTime.UtcNow - _combatDropAt).TotalSeconds;
        var settle = SettleDue(inCombat, _cutscene, sinceDrop);

        // A stitched fight that ended in the quiet gap, such as a downtime wipe.
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
            // Set last, so the save just above does not ask for another pass.
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

    // The world the stitch reads, from the game live or from a recording in replay.
    private bool _inCombat;
    private bool _cutscene;
    private bool _replaying;

    // ---- is the feed even alive? -------------------------------------------

    // A link can die without disconnecting and repeat an encounter that ended
    // pulls ago, so both the parser's totals and the counted damage must go quiet.
    public const double StaleAfterSeconds = 15;

    // A fight is on and the numbers have stopped being about it.
    public static bool FeedIsStale(bool fightOn, double sinceFresh)
        => fightOn && sinceFresh > StaleAfterSeconds;

    public bool FeedStale { get; private set; }

    // A replay feeds the parser nothing, so the board sits on the last real pull.
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

        // Ask for the subscription again, unless a replay is what went quiet.
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

    // Run a recorded feed back through the stitch and report what it makes of it.
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
        // A replay starts from nothing, the way the recorded pull did.
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
                // A pull starts when damage does, so until then the cut slides along.
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
                // A segment ended without its final update, so bank what THIS
                // message shows and start the live segment from here.
                if (_rawSeg is { Active: true })
                {
                    if (restarted)
                    {
                        // Nothing of the new encounter is banked, so this message is all of it.
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
        // Stitching is the meter's only arithmetic on the parser's own numbers,
        // and it is off by default.
        if (C.MeterStitchSegments && _inCombat && _sawBoss)
        {
            // Mid-boss split (downtime): stitch, and keep reading as a live fight.
            _carry ??= new FightCarry { StartSec = _fightStartSec, Title = _fightTitle };
            Fold(_carry, final, Engine.LatestSec);
            // Banked now, so the parser has to be measured from here on.
            if (_rawIn != null) _cut = Snapshot(_rawIn);
            // Printed beside the parser's total, a stitch gone wrong shows itself.
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

    // A finished pull carries its own breakdowns, so history never needs the engine.
    private void Materialize(MeterEncounter enc)
    {
        enc.BossLeft = _bossLeft;
        enc.Ended = EndOf(_standing, _bossLeft);

        // The parser calls the local player "YOU", so resolve it before filing.
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

    // Copies, because the engine keeps tallying into its own rows.
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

    // A pull with its own breakdowns is finished, so never read live data for it.
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
        // Clear when a fight ENDS, or the lagging summary feed would eat the opener.
        Engine.ClearBreakdown();
    }

    // Worth looking back at: a boss, or anything too long to have been trash.
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

    // The parser calls every fight "Encounter" until it ends, so name it here.
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

    // How far the parser's total may sit above the plugin's own count of the lines.
    private const double DriftWarnAbove = 1.10;
    private double _warnedAt;

    // The two totals come from separate paths and should track, so the pull
    // record says when they do not.
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
        // Banked with the pull, so a history entry keeps its own icon.
        if (Engine.LastLimitBreak != 0) enc.LimitBreakAction = Engine.LastLimitBreak;
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

    // Bank a segment into the running fight, capped at the time really elapsed.
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

    // ---- cutting the parser's running encounter ----------------------------

    // Where the meter last drew a line under the parser's totals.
    public sealed class Baseline
    {
        public float Seconds;
        public double Damage;
        public Dictionary<string, MeterCombatant> Rows { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private Baseline? _cut;
    private MeterEncounter? _rawIn;

    // A cut holds while the parser is still on the encounter it was taken from,
    // which its damage says and its idle-trimmed clock cannot.
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
                LimitBreak = r.LimitBreak,
                Damage = Math.Max(0, r.Damage - (b?.Damage ?? 0)),
                Healed = Math.Max(0, r.Healed - (b?.Healed ?? 0)),
                Shielded = Math.Max(0, r.Shielded - (b?.Shielded ?? 0)),
                Taken = Math.Max(0, r.Taken - (b?.Taken ?? 0)),
                Deaths = Math.Max(0, r.Deaths - (b?.Deaths ?? 0)),
                // Rates are running averages the parser never breaks down.
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
        var healed = 0.0;
        foreach (var r in e.Rows) healed += r.Healed;
        foreach (var r in e.Rows)
        {
            r.DamagePct = e.TotalDamage > 0 ? $"{r.Damage / e.TotalDamage * 100:0}%" : "";
            r.HealedPct = healed > 0 ? $"{r.Healed / healed * 100:0}%" : "";
        }
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
            LimitBreak = b.LimitBreak || a.LimitBreak,
            Damage = dmg,
            Healed = healed,
            Shielded = a.Shielded + b.Shielded,
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
        // The window covers the whole fight, with a pad for the lagging feed.
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

        // A replay's engine holds this session, so only live pulls overlay it.
        if (!_replaying) OverlayEngineFacts(enc, Engine);
    }

    // Engine per-fight counts replace the parser's running averages.
    public static void OverlayEngineFacts(MeterEncounter enc, RdpsEngine engine)
    {
        foreach (var row in enc.Rows)
        {
            var who = row.LimitBreak
                ? RdpsEngine.LimitBreakName
                : row.Display.Length > 0 ? row.Display : row.Name;
            var (hits, crits, dhs, max, maxName) = engine.DealtFacts(who);
            if (hits > 0 && !row.LimitBreak)
            {
                row.CritPct = crits * 100.0 / hits;
                row.DirectHitPct = dhs * 100.0 / hits;
            }
            if (max > 0 && maxName.Length > 0) row.MaxHit = $"{maxName}-{(int)max}";
            var (landed, over) = engine.HealFacts(who);
            if (landed + over > 0) row.OverhealPct = over * 100.0 / (landed + over);
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

    // A reset draws a line under the parser's totals but keeps past pulls.
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
                Hps = r.Job switch { "SGE" => 9840, "WHM" => 8630, _ => r.Dps % 900 },
                Healed = 0, OverhealPct = r.Job switch { "SGE" => 21, "WHM" => 24, _ => 4 },
                Taken = 42000 + r.Dps % 9000, Deaths = r.Job is "VPR" ? 1 : 0,
                MaxHit = $"Big One-{(int)(r.Dps * 6)}",
            };
            c.Healed = c.Hps * e.Seconds;
            // A shield healer's absorbs, already inside their healed total.
            c.Shielded = r.Job switch { "SGE" => c.Healed * 0.38, "WHM" => c.Healed * 0.04, _ => 0 };
            e.TotalDps += c.Dps;
            e.RaidRDps += c.RDps;
            e.TotalHps += c.Hps;
            e.TotalTaken += c.Taken;
            e.TotalDeaths += c.Deaths;
            e.Rows.Add(c);
        }
        // The party's shared limit break, drawn under everyone in its own row.
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

    // A stand-in limit break for the sample, so Test mode has an icon to show.
    private static uint SampleLimitBreak()
    {
        var sheet = GameSheets.English<Lumina.Excel.Sheets.Action>();
        if (sheet == null) return 0;
        foreach (var row in sheet)
            if (row.ActionCategory.RowId == 9 && row.Name.ExtractText() == "Braver")
                return row.RowId;
        return 0;
    }

    // A breakdown for the sample too, so the detail view can be styled in Test mode.
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

        // A sample breakdown so the limit break row opens in Test mode.
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

    public void Dispose() => Link.Dispose();
}
