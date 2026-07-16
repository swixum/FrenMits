using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Watches every mitigation in the fight through a pull — the damage-down debuffs
// that land ON the boss (Reprisal / Feint / Addle / Dismantle) AND the
// damage-reduction buffs party members put on themselves / the party (Rampart,
// Sacred Soil, Kerachole, Reprisal-the-buff, etc.). Logs when each goes up and by
// whom, and snapshots on pull end, so after a wipe you get a full recap and can
// see what was missing. Read-only game state, fully guarded.
public class MitRecap
{
    private readonly Plugin _plugin;
    private bool _wasRunning;
    private DateTime _lastScan;
    private readonly HashSet<string> _active = new(StringComparer.OrdinalIgnoreCase); // "source|mit" currently up

    // Damage-down debuffs a full party lands on the boss.
    public static readonly string[] StandardRaidMits = { "Reprisal", "Feint", "Addle", "Dismantle" };

    public sealed record Applied(float Time, string Mit, string Source, MitTypes.Kind Kind, bool OnBoss, uint Icon);
    public sealed record Active(uint Icon, string Mit, string Source, float Remaining, MitTypes.Kind Kind, bool OnBoss);

    public List<Applied> Log { get; } = new();
    public List<Applied> LastLog { get; private set; } = new();

    // Party roster this pull / at the last freeze, so coverage ("7/8") can name
    // exactly who was missing a party mit, not just count.
    public List<string> Party { get; } = new();
    public List<string> LastParty { get; private set; } = new();
    public List<Active> Snapshot { get; private set; } = new();
    public DateTime CapturedAt { get; private set; }

    // Where the capture happened, so the recap is only graded against the plan
    // of the SAME duty (0 = sample data, never graded).
    public uint Territory { get; private set; }
    public bool PopupDismissed { get; private set; }
    public string BossName { get; private set; } = "";
    public float CaptureElapsed { get; private set; }   // fight time (s) at the capture / wipe

    // Hide the post-wipe popup without clearing the recap data.
    public void Dismiss() => PopupDismissed = true;

    public MitRecap(Plugin plugin) => _plugin = plugin;

    public void Update()
    {
        try
        {
            if (!_plugin.Config.RecapAutoCapture) { _wasRunning = false; return; }
            // Only track inside an actual duty/instance — never in the open world,
            // hunts, cities, etc.
            if (!InDuty()) { _wasRunning = false; return; }

            // A phase cutscene is a FREEZE, not a pull boundary: the timer keeps
            // running through it. Treating it as a boundary used to finalize the
            // recap (and pop the wipe recap mid-fight) at every DMU transition,
            // then clear the log, so a real wipe only showed the last phase.
            if (Plugin.InCutscene) return;

            var running = _plugin.Timer.Running;
            if (running && !_wasRunning) { Log.Clear(); _active.Clear(); Party.Clear(); BossName = ""; }
            else if (!running && _wasRunning && Log.Count > 0) FinalizePull(); // pull ended -> freeze recap
            _wasRunning = running;
            if (!running) return;

            // Mits last seconds — scanning a few times a second is plenty and keeps
            // the per-tick status sweep cheap.
            if ((DateTime.UtcNow - _lastScan).TotalSeconds < 0.25) return;
            _lastScan = DateTime.UtcNow;

            var fight = _plugin.ActiveFight();
            var elapsed = fight != null ? _plugin.ElapsedFor(fight) : _plugin.Timer.Elapsed;
            CaptureElapsed = elapsed;
            var now = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var live = new List<Active>();

            foreach (var (src, onBoss, chara) in Sources())
            {
                if (onBoss) BossName = chara.Name.ToString();
                else if (!Party.Contains(src)) Party.Add(src);
                foreach (var m in MitsOn(chara, onBoss))
                {
                    var key = src + "|" + m.Mit;
                    now.Add(key);
                    if (_active.Add(key)) Log.Add(new Applied(elapsed, m.Mit, src, m.Kind, onBoss, m.Icon));
                    live.Add(new Active(m.Icon, m.Mit, src, m.Remaining, m.Kind, onBoss));
                }
            }
            _active.RemoveWhere(k => !now.Contains(k)); // dropped -> can log again on re-apply
            Snapshot = live; // keep "what's up" current, so the wipe snapshot has the boss mits
                             // from the last live moment (the boss resets the instant combat ends)
        }
        catch { /* never disturb the tick */ }
    }

    // Freeze the recap when a pull ends: keep the live Snapshot (the boss has reset
    // by now) and copy the timeline.
    private void FinalizePull()
    {
        LastLog = new List<Applied>(Log);
        LastParty = new List<string>(Party);
        Territory = Service.ClientState.TerritoryType;
        CapturedAt = DateTime.UtcNow;
        PopupDismissed = false;
    }

    // ---- aggregation for the recap window ---------------------------------

    // One USE of a mit: the same buff seen on several party members within a
    // short window collapses to a single event with the covered members listed,
    // so "Troubadour" reads as one line with 7/8, not seven rows.
    public sealed record MitEvent(float Time, string Mit, MitTypes.Kind Kind, bool OnBoss, uint Icon, List<string> Covered);

    public List<MitEvent> LastEvents()
    {
        var events = new List<MitEvent>();
        foreach (var a in LastLog.OrderBy(a => a.Time))
        {
            // PARTY buffs merge across members (one Troubadour = one event with
            // its coverage). Everything else merges only with ITSELF (the same
            // source re-detected), so both tanks hitting Rampart 2s apart stays
            // two distinct uses instead of a bogus "2/8 coverage".
            var ev = events.FirstOrDefault(e =>
                e.OnBoss == a.OnBoss
                && string.Equals(e.Mit, a.Mit, StringComparison.OrdinalIgnoreCase)
                && a.Time - e.Time < 6f
                && (a.Kind == MitTypes.Kind.Party || a.OnBoss
                    || (e.Covered.Count == 1 && e.Covered[0] == a.Source)));
            if (ev == null)
            {
                ev = new MitEvent(a.Time, a.Mit, a.Kind, a.OnBoss, a.Icon, new List<string>());
                events.Add(ev);
            }
            if (!a.OnBoss && !ev.Covered.Contains(a.Source)) ev.Covered.Add(a.Source);
        }
        return events;
    }

    // Manual capture ("Capture now") — re-scans the current state right now.
    public void Capture()
    {
        try
        {
            LastLog = new List<Applied>(Log);
            LastParty = new List<string>(Party);
            var snap = new List<Active>();
            foreach (var (src, onBoss, chara) in Sources())
            {
                if (onBoss) BossName = chara.Name.ToString();
                foreach (var m in MitsOn(chara, onBoss))
                    snap.Add(new Active(m.Icon, m.Mit, src, m.Remaining, m.Kind, onBoss));
            }
            Snapshot = snap;
            var f = _plugin.ActiveFight();
            CaptureElapsed = f != null ? _plugin.ElapsedFor(f) : _plugin.Timer.Elapsed;
            Territory = Service.ClientState.TerritoryType;
            CapturedAt = DateTime.UtcNow;
            PopupDismissed = false;
        }
        catch { /* ignore */ }
    }

    // Make the popup + window appear now (for placing them) without touching data.
    public void ShowTestPopup()
    {
        CapturedAt = DateTime.UtcNow;
        PopupDismissed = false;
    }

    private static bool InDuty()
        => Service.Condition[ConditionFlag.BoundByDuty]
           || Service.Condition[ConditionFlag.BoundByDuty56]
           || Service.Condition[ConditionFlag.BoundByDuty95];

    // --- Sample data (job-accurate) ---------------------------------------

    private static readonly string[] SampleTanks = { "Paladin", "Warrior", "Dark Knight", "Gunbreaker" };
    private static readonly string[] SampleHealers = { "White Mage", "Scholar", "Astrologian", "Sage" };
    private static readonly string[] SampleMelee = { "Monk", "Dragoon", "Ninja", "Samurai", "Reaper", "Viper" };
    private static readonly string[] SampleRanged = { "Bard", "Machinist", "Dancer" };
    private static readonly string[] SampleCasters = { "Black Mage", "Summoner", "Red Mage", "Pictomancer" };

    private static readonly string[] SampleBosses =
        { "Dragon-king Thordan", "Golden Bahamut", "The Omega Protocol", "Kefka", "Alexander Prime", "Pandora" };

    // Each job's own defensive cooldowns (party buffs — boss damage-downs are
    // handled separately by role).
    private static readonly Dictionary<string, string[]> JobBuffs = new()
    {
        ["Paladin"] = new[] { "Rampart", "Sentinel", "Bulwark", "Holy Sheltron", "Divine Veil", "Passage of Arms", "Intervention" },
        ["Warrior"] = new[] { "Rampart", "Vengeance", "Thrill of Battle", "Bloodwhetting", "Nascent Flash", "Shake It Off" },
        ["Dark Knight"] = new[] { "Rampart", "Shadow Wall", "Dark Mind", "Dark Missionary", "The Blackest Night", "Oblation" },
        ["Gunbreaker"] = new[] { "Rampart", "Camouflage", "Nebula", "Heart of Light", "Heart of Corundum", "Aurora" },
        ["White Mage"] = new[] { "Temperance", "Divine Caress", "Asylum", "Liturgy of the Bell", "Aquaveil" },
        ["Scholar"] = new[] { "Sacred Soil", "Expedient", "Fey Illumination", "Whispering Dawn", "Deployment Tactics" },
        ["Astrologian"] = new[] { "Collective Unconscious", "Neutral Sect", "Sun Sign", "Exaltation", "Macrocosmos" },
        ["Sage"] = new[] { "Kerachole", "Holos", "Panhaima", "Taurochole", "Krasis" },
        ["Monk"] = new[] { "Arm's Length", "Second Wind", "Riddle of Earth" },
        ["Dragoon"] = new[] { "Arm's Length", "Second Wind" },
        ["Ninja"] = new[] { "Arm's Length", "Second Wind", "Shade Shift" },
        ["Samurai"] = new[] { "Arm's Length", "Second Wind", "Third Eye" },
        ["Reaper"] = new[] { "Arm's Length", "Second Wind", "Arcane Crest" },
        ["Viper"] = new[] { "Arm's Length", "Second Wind" },
        ["Bard"] = new[] { "Troubadour", "Nature's Minne", "Second Wind" },
        ["Machinist"] = new[] { "Tactician", "Second Wind" },
        ["Dancer"] = new[] { "Shield Samba", "Improvisation", "Curing Waltz" },
        ["Black Mage"] = new[] { "Manaward", "Addle" },
        ["Summoner"] = new[] { "Addle" },
        ["Red Mage"] = new[] { "Magick Barrier", "Addle" },
        ["Pictomancer"] = new[] { "Tempera Coat", "Addle" },
    };

    // Fill the recap with a randomised, comp-accurate fake pull — every job only
    // emits mits it can actually use — so you can see exactly how it looks in-game
    // (icons, colours, missing mits) without doing a real pull.
    public void LoadSample()
    {
        try
        {
            var rnd = new Random();
            string Pick(string[] pool) => pool[rnd.Next(pool.Length)];

            // A realistic 8-player comp: 2 DISTINCT tanks, 2 DISTINCT healers,
            // 4 DPS - duplicates would make full coverage impossible by name.
            var dps = SampleMelee.Concat(SampleRanged).Concat(SampleCasters).OrderBy(_ => rnd.Next()).Take(4).ToList();
            var comp = SampleTanks.OrderBy(_ => rnd.Next()).Take(2)
                .Concat(SampleHealers.OrderBy(_ => rnd.Next()).Take(2))
                .Concat(dps).ToList();

            // Which boss damage-downs the comp could even provide.
            var canProvide = new List<string>();
            if (comp.Any(j => SampleTanks.Contains(j))) canProvide.Add("Reprisal");
            if (comp.Any(j => SampleMelee.Contains(j))) canProvide.Add("Feint");
            if (comp.Any(j => SampleCasters.Contains(j))) canProvide.Add("Addle");
            if (comp.Contains("Machinist")) canProvide.Add("Dismantle");
            // Land most-but-not-all of what's available, so something shows "missing".
            var landed = canProvide.OrderBy(_ => rnd.Next())
                .Take(Math.Max(1, canProvide.Count - rnd.Next(1, 2))).ToList();

            var seq = new List<(string mit, string src, bool onBoss)>();
            foreach (var b in landed) { seq.Add((b, "Boss", true)); if (rnd.Next(3) == 0) seq.Add((b, "Boss", true)); }
            foreach (var job in comp)
                if (JobBuffs.TryGetValue(job, out var buffs))
                    foreach (var buff in buffs.Where(b => b != "Addle").OrderBy(_ => rnd.Next()).Take(1 + rnd.Next(3)))
                        seq.Add((buff, job, false));

            var log = new List<Applied>();
            var t = 10f + rnd.Next(8);
            foreach (var (mit, src, onBoss) in seq.OrderBy(_ => rnd.Next()))
            {
                t += 6 + rnd.Next(20);
                var kind = MitTypes.Classify(mit);
                log.Add(new Applied(t, mit, src, kind, onBoss, SampleIcon(mit)));
                // Party-wide buffs land on most of the raid: emit an entry per
                // covered member so the coverage readout (7/8 etc.) previews too.
                if (!onBoss && kind == MitTypes.Kind.Party)
                    foreach (var member in comp.Where(m => m != src).OrderBy(_ => rnd.Next())
                                 .Take(comp.Count - 1 - rnd.Next(0, 3)))
                        log.Add(new Applied(t + 0.3f, mit, member, kind, false, SampleIcon(mit)));
            }
            LastLog = log.OrderBy(a => a.Time).ToList();
            LastParty = comp.ToList(); // sample roster, so coverage renders too

            Snapshot = LastLog.OrderBy(_ => rnd.Next()).Take(3 + rnd.Next(3))
                .Select(a => new Active(a.Icon, a.Mit, a.Source, 4 + rnd.Next(18), a.Kind, a.OnBoss))
                .ToList();

            BossName = Pick(SampleBosses);
            CaptureElapsed = LastLog.Count > 0 ? LastLog[^1].Time + 6 : 0;
            Territory = 0; // sample data: never graded against a plan
            CapturedAt = DateTime.UtcNow;
            PopupDismissed = false;
        }
        catch { /* ignore */ }
    }

    private static uint SampleIcon(string mit)
    {
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Status>();
            if (sheet == null) return 0;
            foreach (var row in sheet)
                if (string.Equals(row.Name.ExtractText(), mit, StringComparison.OrdinalIgnoreCase))
                    return (uint)row.Icon;
        }
        catch { /* ignore */ }
        return 0;
    }

    // Standard raid damage-downs that never landed on the boss this pull
    // (informational — comp-dependent).
    public List<string> NotSeen()
        => StandardRaidMits
            .Where(s => !LastLog.Any(a => a.OnBoss && a.Mit.Contains(s, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    public bool HasData => LastLog.Count > 0 || Snapshot.Count > 0;

    // A plain-text recap for the clipboard (paste into Discord / notes).
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.Append("Party Mit Recap");
        if (!string.IsNullOrEmpty(BossName)) sb.Append(" - ").Append(BossName);
        if (CaptureElapsed > 0) sb.Append($"  ({(int)CaptureElapsed / 60}:{(int)CaptureElapsed % 60:00}")
            .Append(LastLog.Count > 0 ? " wipe)" : ")");
        sb.AppendLine();
        var missed = NotSeen();
        sb.AppendLine(missed.Count == 0
            ? "All four standard raid mits landed."
            : "Never landed: " + string.Join(", ", missed));

        if (Snapshot.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Up at capture:");
            foreach (var m in Snapshot.OrderByDescending(m => m.OnBoss).ThenBy(m => m.Source))
                sb.AppendLine($"  {m.Mit} - {(m.OnBoss ? "on boss" : m.Source)} ({m.Remaining:0}s)");
        }

        if (LastLog.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Applied this pull:");
            foreach (var a in LastLog.OrderBy(a => a.Time))
                sb.AppendLine($"  {(int)a.Time / 60}:{(int)a.Time % 60:00}  {a.Mit} - {(a.OnBoss ? "on boss" : a.Source)}");
        }
        return sb.ToString();
    }

    // The things we read statuses off: the boss (debuffs) + every party player (buffs).
    private IEnumerable<(string source, bool onBoss, IBattleChara chara)> Sources()
    {
        var boss = FindBoss();
        if (boss != null) yield return ("Boss", true, boss);

        foreach (var o in Service.ObjectTable)
            if (o is IPlayerCharacter pc && pc.MaxHp > 0)
                yield return (pc.Name.ToString(), false, pc);
    }

    private static IBattleNpc? FindBoss()
    {
        IBattleNpc? boss = null;
        foreach (var o in Service.ObjectTable)
            if (o is IBattleNpc n && n.MaxHp > 1_000_000 && (boss is null || n.MaxHp > boss.MaxHp))
                boss = n;
        return boss;
    }

    private readonly record struct Hit(uint Icon, string Mit, float Remaining, MitTypes.Kind Kind);

    private static List<Hit> MitsOn(IBattleChara chara, bool onBoss)
    {
        var list = new List<Hit>();
        var sheet = Service.DataManager.GetExcelSheet<Status>();
        if (sheet == null) return list;
        foreach (var st in chara.StatusList)
        {
            if (st is null || st.StatusId == 0) continue;
            if (sheet.GetRowOrDefault(st.StatusId) is not { } row) continue;
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (onBoss ? !IsBossMit(name) : !IsPartyMit(name)) continue;
            list.Add(new Hit((uint)row.Icon, name, MathF.Abs(st.RemainingTime), MitTypes.Classify(name)));
        }
        return list;
    }

    private static bool IsBossMit(string name)
    {
        foreach (var s in StandardRaidMits)
            if (name.Contains(s, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // Damage-reduction buffs on players. Recognised mit kinds, minus the pure heals
    // that share keywords (we want mitigation, not healing).
    private static readonly string[] HealNoise = { "medica", "cure", "regen", "benediction", "physis", "asylum" };
    private static bool IsPartyMit(string name)
    {
        if (MitTypes.Classify(name) == MitTypes.Kind.Other) return false;
        var l = name.ToLowerInvariant();
        foreach (var h in HealNoise)
            if (l.Contains(h)) return false;
        return true;
    }
}
