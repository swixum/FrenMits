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

    // A death with its story: what the player still had running just before,
    // and how fast they went from healthy to dead (both from the same 4 Hz
    // status/HP sweep - no game hooks).
    public readonly record struct Death(float Time, string Name, string Had, float FromPct, float Seconds);

    // One frozen pull. The recap keeps a short history of these so the last
    // few wipes stay comparable ("did we fix it?") instead of each wipe
    // overwriting the one before.
    public sealed class PullRecap
    {
        public Guid PullId;
        public List<Applied> Log = new();
        public List<string> Party = new();
        public Dictionary<string, string> Jobs = new(StringComparer.OrdinalIgnoreCase); // name -> job abbr
        public List<Death> Deaths = new();
        public List<Active> Snapshot = new();
        public string BossName = "";
        public float CaptureElapsed;
        public uint Territory;
        public DateTime CapturedAt;
        // Party cooldowns that sat unused: (who, mit, why it counts, icon).
        public List<(string Who, string Mit, string Note, uint Icon)> Unused = new();
    }

    public List<Applied> Log { get; } = new();

    // Party roster this pull, so coverage ("7/8") can name exactly who was
    // missing a party mit, not just count.
    public List<string> Party { get; } = new();

    // Frozen pulls, newest first. View picks which one the window shows.
    public List<PullRecap> History { get; } = new();
    public int View;
    private const int MaxHistory = 6;
    private static readonly PullRecap Empty = new();
    public PullRecap Shown => History.Count > 0 ? History[Math.Clamp(View, 0, History.Count - 1)] : Empty;

    // Facade over the shown pull (keeps the window/popup call sites simple).
    public List<Applied> LastLog => Shown.Log;
    public List<string> LastParty => Shown.Party;
    public List<Active> Snapshot { get; private set; } = new();
    public DateTime CapturedAt => History.Count > 0 ? History[0].CapturedAt : default;
    public uint Territory => Shown.Territory;
    public bool PopupDismissed { get; private set; }
    public List<Death> LastDeaths => Shown.Deaths;
    public string BossName => Shown.BossName;
    public float CaptureElapsed => Shown.CaptureElapsed;

    // Live-pull tracking state.
    private readonly List<Death> _deaths = new();
    private readonly HashSet<string> _dead = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _lastMits = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<(float T, float Pct)>> _hp = new(StringComparer.OrdinalIgnoreCase);
    private string _liveBoss = "";
    private float _liveElapsed;
    private Guid _pullId = Guid.NewGuid();

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
            if (Plugin.CutsceneActive) return;

            var running = _plugin.Timer.Running;
            if (running && !_wasRunning)
            {
                Log.Clear(); _active.Clear(); Party.Clear(); _deaths.Clear(); _dead.Clear();
                _jobs.Clear(); _lastMits.Clear(); _hp.Clear(); _liveBoss = "";
                _pullId = Guid.NewGuid();
            }
            else if (!running && _wasRunning && Log.Count > 0) FinalizePull(); // pull ended -> freeze recap
            _wasRunning = running;
            if (!running) return;

            // Mits last seconds — scanning a few times a second is plenty and keeps
            // the per-tick status sweep cheap.
            if ((DateTime.UtcNow - _lastScan).TotalSeconds < 0.25) return;
            _lastScan = DateTime.UtcNow;

            var fight = _plugin.ActiveFight();
            var elapsed = fight != null ? _plugin.ElapsedFor(fight) : _plugin.Timer.Elapsed;
            _liveElapsed = elapsed;
            var now = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var live = new List<Active>();

            foreach (var (src, onBoss, chara) in Sources())
            {
                if (onBoss) _liveBoss = chara.Name.ToString();
                else
                {
                    if (!Party.Contains(src)) Party.Add(src);
                    if (!_jobs.ContainsKey(src) && chara is IPlayerCharacter pc
                        && Jobs.ByRowId(pc.ClassJob.RowId) is { } ji)
                        _jobs[src] = ji.Abbreviation;

                    // Death edge: HP hits zero, recorded once per life, with the
                    // story attached (what they had up, how fast they dropped).
                    // The dead keep their last-alive HP/mits frozen for that.
                    if (chara.CurrentHp == 0)
                    {
                        if (_dead.Add(src)) _deaths.Add(MakeDeath(elapsed, src));
                        continue;
                    }
                    _dead.Remove(src);

                    // Short HP trace (~12s at 4 Hz) feeding the death story.
                    var pct = chara.MaxHp > 0 ? chara.CurrentHp / (float)chara.MaxHp : 0f;
                    if (!_hp.TryGetValue(src, out var ring)) ring = _hp[src] = new List<(float, float)>();
                    ring.Add((elapsed, pct));
                    if (ring.Count > 48) ring.RemoveAt(0);
                }

                List<string>? mine = null;
                if (!onBoss)
                {
                    if (!_lastMits.TryGetValue(src, out mine)) mine = _lastMits[src] = new List<string>();
                    mine.Clear();
                }
                foreach (var m in MitsOn(chara, onBoss))
                {
                    var key = src + "|" + m.Mit;
                    now.Add(key);
                    if (_active.Add(key)) Log.Add(new Applied(elapsed, m.Mit, src, m.Kind, onBoss, m.Icon));
                    live.Add(new Active(m.Icon, m.Mit, src, m.Remaining, m.Kind, onBoss));
                    mine?.Add(m.Mit);
                }
            }
            _active.RemoveWhere(k => !now.Contains(k)); // dropped -> can log again on re-apply
            _snapLive = live; // keep "what's up" current, so the wipe snapshot has the boss mits
                              // from the last live moment (the boss resets the instant combat ends)
        }
        catch { /* never disturb the tick */ }
    }

    private List<Active> _snapLive = new();

    // Freeze the recap when a pull ends: keep the live snapshot (the boss has
    // reset by now), copy the timeline and run the after-action analysis.
    private void FinalizePull()
    {
        Push(BuildPull(_snapLive));
        PopupDismissed = false;
    }

    private PullRecap BuildPull(List<Active> snapshot)
    {
        var pr = new PullRecap
        {
            PullId = _pullId,
            Log = new List<Applied>(Log),
            Party = new List<string>(Party),
            Jobs = new Dictionary<string, string>(_jobs, StringComparer.OrdinalIgnoreCase),
            Deaths = new List<Death>(_deaths),
            Snapshot = new List<Active>(snapshot),
            BossName = _liveBoss,
            CaptureElapsed = _liveElapsed,
            Territory = Service.ClientState.TerritoryType,
            CapturedAt = DateTime.UtcNow,
        };
        pr.Unused = ComputeUnused(pr);
        return pr;
    }

    // Newest first; a mid-pull "Capture now" of the same pull upgrades in place
    // instead of duplicating it when the wipe freeze lands moments later.
    private void Push(PullRecap p)
    {
        var i = History.FindIndex(h => h.PullId == p.PullId);
        if (i >= 0) History.RemoveAt(i);
        History.Insert(0, p);
        while (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);
        View = 0;
    }

    // The death story from the frozen last-alive state: what was still running
    // on them, and how fast they went from healthy to dead.
    private Death MakeDeath(float t, string name)
    {
        var had = _lastMits.TryGetValue(name, out var lm) && lm.Count > 0
            ? string.Join(", ", lm.Take(4)) : "";
        var from = 0f; var secs = 0f;
        if (_hp.TryGetValue(name, out var ring) && ring.Count > 0)
        {
            // The most recent healthy-ish moment; failing that, the best HP we
            // saw in the trace window.
            (float T, float Pct)? healthy = null;
            for (var i = ring.Count - 1; i >= 0; i--)
                if (ring[i].Pct >= 0.7f) { healthy = ring[i]; break; }
            var pick = healthy ?? ring.OrderByDescending(x => x.Pct).First();
            from = pick.Pct;
            secs = MathF.Max(0.1f, t - pick.T);
        }
        return new Death(t, name, had, from, secs);
    }

    // Follow-up abilities that only exist inside another cooldown's window; a
    // "never used" nag for them would just duplicate the parent's.
    private static readonly HashSet<string> DependentMits = new(StringComparer.OrdinalIgnoreCase)
        { "Divine Caress", "Sun Sign" };

    // Party-facing cooldowns that sat unused all pull (or came back long before
    // the wipe and never went out again). Job-unique names make presence in the
    // log attributable; a job appearing twice in the roster is skipped - the
    // recap can't tell whose press it saw.
    private static List<(string Who, string Mit, string Note, uint Icon)> ComputeUnused(PullRecap p)
    {
        var res = new List<(string, string, string, uint)>();
        try
        {
            var dupJobs = p.Jobs.Values
                .GroupBy(j => j, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, job) in p.Jobs)
            {
                if (dupJobs.Contains(job)) continue;
                if (!Cooldowns.JobKits.TryGetValue(job, out var kit)) continue;
                foreach (var mit in kit)
                {
                    if (DependentMits.Contains(mit)) continue;
                    if (MitTypes.Classify(mit) != MitTypes.Kind.Party) continue;
                    var recast = Cooldowns.PlanInfo(mit)?.Recast ?? 0f;
                    if (recast < 45f) continue; // short rollers are never "wasted"
                    var times = p.Log.Where(a => !a.OnBoss
                            && string.Equals(a.Mit, mit, StringComparison.OrdinalIgnoreCase))
                        .Select(a => a.Time).ToList();
                    if (times.Count == 0)
                    {
                        // Only nag once the pull was long enough to have used it.
                        if (p.CaptureElapsed >= recast * 0.9f)
                            res.Add((name, mit, "never used", SampleIcon(mit)));
                    }
                    else
                    {
                        var idle = p.CaptureElapsed - times.Max() - recast;
                        if (idle >= 20f) res.Add((name, mit, $"was back {(int)idle}s before the end", SampleIcon(mit)));
                    }
                }
            }
        }
        catch { /* analysis is optional garnish */ }
        return res.OrderBy(r => r.Item3 == "never used" ? 0 : 1)
            .ThenBy(r => r.Item2, StringComparer.OrdinalIgnoreCase)
            .Take(10).ToList();
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
            var snap = new List<Active>();
            foreach (var (src, onBoss, chara) in Sources())
            {
                if (onBoss) _liveBoss = chara.Name.ToString();
                foreach (var m in MitsOn(chara, onBoss))
                    snap.Add(new Active(m.Icon, m.Mit, src, m.Remaining, m.Kind, onBoss));
            }
            var f = _plugin.ActiveFight();
            _liveElapsed = f != null ? _plugin.ElapsedFor(f) : _plugin.Timer.Elapsed;
            Push(BuildPull(snap));
            PopupDismissed = false;
        }
        catch { /* ignore */ }
    }

    // Make the popup + window appear now (for placing them) without real data.
    public void ShowTestPopup()
    {
        if (History.Count == 0) LoadSample();
        if (History.Count > 0) History[0].CapturedAt = DateTime.UtcNow;
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
            var sampleLog = log.OrderBy(a => a.Time).ToList();
            var pr = new PullRecap
            {
                PullId = Guid.NewGuid(),
                Log = sampleLog,
                Party = comp.ToList(), // sample roster, so coverage renders too
                BossName = Pick(SampleBosses),
                CaptureElapsed = sampleLog.Count > 0 ? sampleLog[^1].Time + 6 : 0,
                Territory = 0, // sample data: never graded against a plan
                CapturedAt = DateTime.UtcNow,
            };
            // Sample jobs by full name, so the unused-cooldown analysis previews
            // with the real logic.
            foreach (var job in comp)
                if (Jobs.All.FirstOrDefault(j => string.Equals(j.Name, job, StringComparison.OrdinalIgnoreCase)) is { RowId: > 0 } ji)
                    pr.Jobs[job] = ji.Abbreviation;
            pr.Snapshot = sampleLog.OrderBy(_ => rnd.Next()).Take(3 + rnd.Next(3))
                .Select(a => new Active(a.Icon, a.Mit, a.Source, 4 + rnd.Next(18), a.Kind, a.OnBoss))
                .ToList();
            if (sampleLog.Count > 2)
            {
                pr.Deaths = new List<Death>
                {
                    new(sampleLog[sampleLog.Count / 2].Time + 2f, comp.Count > 0 ? comp[0] : "Someone",
                        "Rampart, Sacred Soil", 0.86f, 3.4f),
                    new(sampleLog[^1].Time + 1f, comp.Count > 1 ? comp[1] : "Someone Else", "", 0.97f, 1.8f),
                };
            }
            pr.Unused = ComputeUnused(pr);
            Push(pr);
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

    public bool HasData => History.Count > 0;

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

        if (LastDeaths.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Deaths:");
            foreach (var d in LastDeaths.OrderBy(d => d.Time))
                sb.AppendLine($"  {(int)d.Time / 60}:{(int)d.Time % 60:00}  {d.Name}"
                    + (d.FromPct > 0 ? $"  ({(int)(d.FromPct * 100)}% to dead in {d.Seconds:0.0}s)" : "")
                    + (d.Had.Length > 0 ? $"  had {d.Had}" : "  nothing up"));
        }

        if (Shown.Unused.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Left on the table:");
            foreach (var (who, mit, note, _) in Shown.Unused)
                sb.AppendLine($"  {mit} - {who}: {note}");
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
