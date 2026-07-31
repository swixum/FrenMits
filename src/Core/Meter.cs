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
// resets per pack as normal, and only boss fights are kept as history.
public class Meter : IDisposable
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public MeterLink Link { get; }
    public RdpsEngine Engine { get; } = new();

    public MeterEncounter? Current { get; private set; }
    public List<MeterEncounter> History { get; } = new();
    private const int MaxHistory = 10;

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

        // A boss on the field marks this fight as one worth stitching and keeping.
        if (Plugin.InCombat && _plugin.BossHpFraction >= 0f) _sawBoss = true;

        // A stitched fight that ended inside the quiet gap (a wipe during
        // downtime): no further segment is coming, settle it when combat drops.
        if (!Paused && _carry != null && _rawSeg is not { Active: true } && !Plugin.InCombat)
        {
            if (Current != null)
            {
                Current.Active = false;
                Materialize(Current);
                if (_sawBoss) PushHistory(Current);
            }
            EndFight();
        }

        if (DateTime.UtcNow >= _nextTrim)
        {
            _nextTrim = DateTime.UtcNow + TimeSpan.FromMinutes(1);
            Engine.Trim();
        }

        // Combat over: close this fight right away instead of waiting out the
        // parser's idle timeout. That is what makes each trash pack in a
        // dungeon start from zero, and freezes a kill at the killing blow.
        var inCombat = Plugin.InCombat;
        if (!inCombat && _wasInCombat) _combatDropAt = DateTime.UtcNow;
        if (inCombat) _cutDone = false;
        else if (!_cutDone && !Paused && _rawSeg is { Active: true }
                 && _combatDropAt != DateTime.MinValue
                 && (DateTime.UtcNow - _combatDropAt).TotalSeconds > 1.5)
        {
            _cutDone = true;
            CutHere();
        }
        _wasInCombat = inCombat;

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

    private void OnSummary(MeterEncounter incoming)
    {
        _rawIn = incoming;
        var raw = incoming;
        if (_cut != null)
        {
            // The parser starting its own new encounter retires the cut.
            if (raw.Seconds + 0.5f < _cut.Seconds) _cut = null;
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
                if (_rawSeg is { Active: true }) EndSegment(_rawSeg);
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
        if (Plugin.InCombat && _sawBoss)
        {
            // Mid-boss split (downtime): stitch, and keep reading as a live fight.
            _carry ??= new FightCarry { StartSec = _fightStartSec, Title = _fightTitle };
            Fold(_carry, final);
            display.Active = true;
            Publish(display);
            return;
        }

        display.Active = false;
        Materialize(display);
        Publish(display);
        if (_sawBoss) PushHistory(display);
        EndFight();
    }

    // A finished pull carries its own breakdowns, so looking back at it later
    // does not depend on the engine still holding that fight.
    private void Materialize(MeterEncounter enc)
    {
        foreach (var r in enc.Rows)
        {
            var who = r.Display.Length > 0 ? r.Display : r.Name;
            if (Engine.Dealt(who) is { Count: > 0 } d) enc.Dealt[who] = Freeze(d);
            if (Engine.Targets(who) is { Count: > 0 } t) enc.Targets[who] = Freeze(t);
            if (Engine.Taken(who) is { Count: > 0 } k) enc.Taken[who] = Freeze(k);
        }
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
                Dhs = a.Dhs, Damage = a.Damage, Max = a.Max,
            });
        return copy;
    }

    // What a player did, or had done to them, in the pull on screen.
    public List<AbilityStat> Breakdown(MeterEncounter enc, string player, int kind)
    {
        var stored = kind switch { 1 => enc.Targets, 2 => enc.Taken, _ => enc.Dealt };
        if (stored.TryGetValue(player, out var saved)) return saved;
        if (enc.Dealt.Count > 0 || enc.Taken.Count > 0) return new List<AbilityStat>();
        return kind switch { 1 => Engine.Targets(player), 2 => Engine.Taken(player), _ => Engine.Dealt(player) };
    }

    private void EndFight()
    {
        _carry = null;
        _fightStartSec = 0;
        _fightTitle = "";
        _sawBoss = false;
        // Clear when a fight ENDS, not when the next one starts: the summary
        // feed lags the log, so clearing on arrival would eat the opener.
        Engine.ClearBreakdown();
    }

    private void PushHistory(MeterEncounter enc)
    {
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

    private void Publish(MeterEncounter enc)
    {
        ApplyRdps(enc);
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

    // Bank a finished segment's numbers into the running fight.
    public static void Fold(FightCarry carry, MeterEncounter final)
    {
        foreach (var r in final.Rows)
            carry.Rows[r.Name] = Combine(carry.Rows.GetValueOrDefault(r.Name), r);
        carry.Seconds += final.Seconds;
        if (final.Title.Length > 0) carry.Title = final.Title;
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
        public Dictionary<string, MeterCombatant> Rows { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private Baseline? _cut;
    private MeterEncounter? _rawIn;

    public static Baseline Snapshot(MeterEncounter raw)
    {
        var b = new Baseline { Seconds = raw.Seconds };
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
            if (_sawBoss) PushHistory(display);
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
                Name = r.Name, Display = r.Display, Job = r.Job,
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
            CritPct = dmg > 0 ? (a.CritPct * a.Damage + b.CritPct * b.Damage) / dmg : b.CritPct,
            DirectHitPct = dmg > 0 ? (a.DirectHitPct * a.Damage + b.DirectHitPct * b.Damage) / dmg : b.DirectHitPct,
            OverhealPct = healed > 0 ? (a.OverhealPct * a.Healed + b.OverhealPct * b.Healed) / healed : b.OverhealPct,
            MaxHit = MaxHitValue(b.MaxHit) >= MaxHitValue(a.MaxHit) ? b.MaxHit : a.MaxHit,
        };
    }

    private static double MaxHitValue(string maxHit)
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

    public void Clear()
    {
        Current = null;
        Previous = null;
        History.Clear();
        _rawSeg = null;
        EndFight();
    }

    // A mid-combat reset draws a line under the parser's running totals, or
    // they would just repopulate the meter one second later.
    public void ResetEncounter()
    {
        Clear();
        if (_rawIn != null) _cut = Snapshot(_rawIn);
    }

    public string StatusText => !C.MeterEnabled ? "off" : Link.Status switch
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
                Dps = r.Dps, RDps = r.Dps * r.Edge, Damage = r.Dps * e.Seconds,
                CritPct = 18 + r.Dps % 13, DirectHitPct = 22 + r.Dps % 21,
                Hps = r.Job is "SGE" ? 9840 : r.Dps % 900,
                Healed = 0, OverhealPct = r.Job is "SGE" ? 21 : 4,
                Taken = 42000 + r.Dps % 9000, Deaths = r.Job is "VPR" ? 1 : 0,
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
        }
    }

    public void Dispose() => Link.Dispose();
}
