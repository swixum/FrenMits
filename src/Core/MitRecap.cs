using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Watches every mitigation in a pull, on the boss and on the party, for the recap.
public class MitRecap
{
    private readonly Plugin _plugin;
    private bool _wasRunning;
    private DateTime _lastScan;
    private readonly HashSet<string> _active = new(StringComparer.OrdinalIgnoreCase); // "source|mit" currently up
    private readonly Dictionary<string, Applied> _activeRef = new(StringComparer.OrdinalIgnoreCase); // its open log entry

    // Damage-down debuffs a full party lands on the boss.
    public static readonly string[] StandardRaidMits = { "Reprisal", "Feint", "Addle", "Dismantle" };

    public sealed record Applied(float Time, string Mit, string Source, MitTypes.Kind Kind, bool OnBoss, uint Icon)
    {
        // When it fell off, so grading can ask "was it STILL up at the hit?"
        // rather than only "was it pressed nearby?". Stamped when the status
        // drops off the scan; still-running mits get the pull's end at freeze.
        public float End { get; set; } = -1f;
    }
    public sealed record Active(uint Icon, string Mit, string Source, float Remaining, MitTypes.Kind Kind, bool OnBoss);

    // A death with its story: what killed them, what was running as it landed,
    // how fast they dropped, and the last hits leading in (frozen at death, so
    // a browsed history entry keeps them).
    public readonly record struct Death(float Time, string Name, string Had, float FromPct, float Seconds,
        string KilledBy = "", List<DamageCapture.PlayerHit>? Hits = null);

    // One frozen pull; a short history is kept so wipes stay comparable.
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
        // Plan vs. actual: how many planned presses were graded, how many
        // landed on plan, and the ones that didn't (late or never seen).
        public int PlanTotal;
        public int PlanGood;
        public List<PlanHit> PlanProblems = new();
        // Enemy hits from the packet capture, each carrying the boss debuffs
        // that were up as its damage was calculated.
        public List<DamageCapture.EnemyHit> Hits = new();
    }

    // One planned press that went wrong: never seen, up Delta seconds after the
    // mechanic it was planned for, or (Why) up at the wrong time entirely.
    public readonly record struct PlanHit(float Time, string Mit, string Mechanic, float Delta, bool Missed, uint Icon, string Why = "");

    public List<Applied> Log { get; } = new();

    // Party roster this pull, so coverage ("7/8") can name exactly who was
    // missing a party mit, not just count.
    public List<string> Party { get; } = new();

    // Frozen pulls, newest first; View picks which one the window shows.
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
    // True while a config-page preview is showing: lets the popup appear for
    // placement even when the recap itself is switched off.
    public bool Previewing { get; private set; }
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
    public void Dismiss() { PopupDismissed = true; Previewing = false; }

    public MitRecap(Plugin plugin) => _plugin = plugin;

    public void Update()
    {
        try
        {
            if (!_plugin.Config.RecapEnabled) { _wasRunning = false; return; }
            // Only track inside an actual duty/instance - never in the open world,
            // hunts, cities, etc.
            if (!InDuty()) { _wasRunning = false; return; }

            // A phase cutscene is a freeze, not a pull boundary.
            if (Plugin.CutsceneActive) return;

            var running = _plugin.Timer.Running;
            if (running && !_wasRunning)
            {
                Log.Clear(); _active.Clear(); _activeRef.Clear(); Party.Clear(); _deaths.Clear(); _dead.Clear();
                _jobs.Clear(); _lastMits.Clear(); _hp.Clear(); _liveBoss = "";
                _pullId = Guid.NewGuid();
                _plugin.Damage.Clear();
            }
            else if (!running && _wasRunning && Log.Count > 0) FinalizePull(); // pull ended -> freeze recap
            _wasRunning = running;
            if (!running) return;

            // Mits last seconds - scanning a few times a second is plenty and keeps
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

                    // Death edge: HP hits zero, recorded once per life with the story
                    // attached.
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
                    if (_active.Add(key))
                    {
                        var a = new Applied(elapsed, m.Mit, src, m.Kind, onBoss, m.Icon);
                        Log.Add(a);
                        _activeRef[key] = a;
                    }
                    live.Add(new Active(m.Icon, m.Mit, src, m.Remaining, m.Kind, onBoss));
                    mine?.Add(m.Mit);
                }
            }
            // Dropped mits close their interval (and can log again on re-apply).
            foreach (var k in _active)
                if (!now.Contains(k) && _activeRef.TryGetValue(k, out var gone))
                {
                    gone.End = elapsed;
                    _activeRef.Remove(k);
                }
            _active.RemoveWhere(k => !now.Contains(k));
            _snapLive = live; // keep "what's up" current, so the wipe snapshot has the boss mits
                              // from the last live moment (the boss resets the instant combat ends)
        }
        // Never disturb the tick - but never vanish either: a recurring failure
        // here silently ends recap tracking for the session.
        catch (Exception ex) { Swallowed.Report("mit recap tick", ex); }
    }

    private List<Active> _snapLive = new();

    // Freeze the recap when a pull ends: keep the live snapshot (the boss has
    // reset by now), copy the timeline and run the after-action analysis.
    private void FinalizePull()
    {
        Push(BuildPull(_snapLive));
        PopupDismissed = false;
        Previewing = false;
    }

    private PullRecap BuildPull(List<Active> snapshot)
    {
        // Mits still up when the pull ended never dropped off a scan; their
        // interval runs to the freeze.
        foreach (var a in Log)
            if (a.End < 0f) a.End = MathF.Max(a.Time, _liveElapsed);
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
            Hits = new List<DamageCapture.EnemyHit>(_plugin.Damage.Hits),
        };
        pr.Unused = ComputeUnused(pr);
        // Grade the plan NOW, while this is still the active fight: a browsed
        // history entry keeps its grades after you leave the zone.
        var fight = _plugin.ActiveFight();
        if (fight is { TimelineOnly: false } && fight.TerritoryId == pr.Territory)
            ComputePlanCheck(pr, fight, _plugin.ActiveJobAbbreviation());
        return pr;
    }

    // Newest first; a re-freeze of the same pull upgrades in place instead of
    // duplicating it.
    private void Push(PullRecap p)
    {
        var i = History.FindIndex(h => h.PullId == p.PullId);
        if (i >= 0) History.RemoveAt(i);
        History.Insert(0, p);
        while (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);
        View = 0;
    }

    // How long after its last recorded hit a death still reads as that hit.
    // Past this the killing blow was something the packet capture can't see
    // (a dot tick, a fall), so the story stays quiet rather than blaming the
    // wrong mechanic.
    private const float KillingBlowWindow = 8f;

    // The death story: the killing blow and what was up AS IT LANDED when the
    // packet capture saw it; the frozen last-alive scan otherwise.
    private Death MakeDeath(float t, string name)
    {
        var had = _lastMits.TryGetValue(name, out var lm) && lm.Count > 0
            ? string.Join(", ", lm.Take(4)) : "";
        var killedBy = "";
        List<DamageCapture.PlayerHit>? hits = null;
        if (_plugin.Damage.RecentHits.TryGetValue(name, out var ring) && ring.Count > 0)
        {
            var hit = ring[^1];
            if (t - hit.Time <= KillingBlowWindow)
            {
                killedBy = hit.Action.Length > 0
                    ? (hit.Amount > 0 ? $"{hit.Action} ({hit.Amount:N0})" : hit.Action)
                    : hit.Amount > 0 ? $"{hit.Amount:N0} damage" : "";
                // What the hit was calculated against beats what a scan saw
                // last; an empty read there means genuinely nothing was up.
                had = hit.Mits;
            }
            // The run-in: only hits close enough to be part of the same story.
            hits = ring.Where(h => t - h.Time <= 12f).ToList();
            if (hits.Count == 0) hits = null;
        }
        var from = 0f; var secs = 0f;
        if (_hp.TryGetValue(name, out var hpRing) && hpRing.Count > 0)
        {
            // The most recent healthy-ish moment; failing that, the best HP we
            // saw in the trace window.
            (float T, float Pct)? healthy = null;
            for (var i = hpRing.Count - 1; i >= 0; i--)
                if (hpRing[i].Pct >= 0.7f) { healthy = hpRing[i]; break; }
            var pick = healthy ?? hpRing.OrderByDescending(x => x.Pct).First();
            from = pick.Pct;
            secs = MathF.Max(0.1f, t - pick.T);
        }
        return new Death(t, name, had, from, secs, killedBy, hits);
    }

    // The recognized mit names currently on a player, for the packet capture's
    // at-the-hit read.
    internal static IEnumerable<string> MitNamesOn(IBattleChara chara)
    {
        foreach (var h in MitsOn(chara, onBoss: false)) yield return h.Mit;
    }

    // Follow-up abilities that only exist inside another cooldown's window; a
    // "never used" nag for them would just duplicate the parent's.
    private static readonly HashSet<string> DependentMits = new(StringComparer.OrdinalIgnoreCase)
        { "Divine Caress", "Sun Sign" };

    // Party-facing cooldowns that sat unused all pull.
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
        catch (Exception ex) { Swallowed.Report("recap unused-cooldown analysis", ex); }
        return res.OrderBy(r => r.Item3 == "never used" ? 0 : 1)
            .ThenBy(r => r.Item2, StringComparer.OrdinalIgnoreCase)
            .Take(10).ToList();
    }

    // ---- plan vs. actual ---------------------------------------------------

    // Status names that differ from the action the plan wrote down.
    public static readonly (string StatusPart, string Canon)[] StatusAliases =
    {
        ("Expedience", "Expedient"), ("Desperate Measures", "Expedient"),
        ("Blackest Night", "The Blackest Night"),
        ("Seraphic", "Seraph"),
        // Upgrade pairs, both directions: a synced player's status keeps the
        // old name while the sheet may write the new one, and vice versa.
        ("Damnation", "Vengeance"), ("Vengeance", "Damnation"),
        ("Guardian", "Sentinel"), ("Sentinel", "Guardian"),
        ("Great Nebula", "Nebula"), ("Nebula", "Great Nebula"),
        ("Shadowed Vigil", "Shadow Wall"), ("Shadow Wall", "Shadowed Vigil"),
        ("Bloodwhetting", "Raw Intuition"), ("Raw Intuition", "Bloodwhetting"),
    };

    // Planned actions the recap can never observe (no lasting status).
    public static readonly HashSet<string> DeltaBlind =
        new(StringComparer.OrdinalIgnoreCase) { "Second Wind", "Bloodbath", "Equilibrium" };

    // Every slot's planned lines: the live plan plus each saved slot.
    public static IEnumerable<(string Slot, MitLine Line)> PlannedLines(FightProfile fight, string? myJob)
    {
        foreach (var l in fight.Lines)
            if (l.Enabled && l.AppliesTo(myJob))
                yield return (fight.Slot, l);
        foreach (var kv in fight.SavedSlots)
        {
            if (string.Equals(kv.Key, fight.Slot, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var l in kv.Value)
                if (l.Enabled && l.Jobs.Count == 0 && !l.HasJobGate())
                    yield return (kv.Key, l);
        }
    }

    // The game decides a cast's damage a beat before the hit lands (the cast
    // bar's end), and the sheet's row time is the hit itself. A mit graded "on
    // plan" therefore has to be up HERE, not at the row time. Internal so the
    // timing solver plans by the same physics the recap grades by.
    internal const float SnapshotLead = 0.7f;
    // Poll-scan slack on interval edges: a status is first seen (and last seen)
    // up to a scan late.
    private const float EdgeGrace = 0.6f;

    // Grade the sheet against the pull: each planned press covered its
    // mechanic's snapshot, was late to it, fell off before it, or never went
    // out. Boss damage-downs are graded from the packet capture when it ran:
    // what was really on the attacker as each hit's damage was calculated.
    // Public because the grading rules live under test with no game attached.
    public static void ComputePlanCheck(PullRecap p, FightProfile fight, string? myJob)
    {
        try
        {
            if (p.Log.Count == 0 && p.Hits.Count == 0) return;

            // What the plan expects: observable, comp-possible presses, deduped.
            var planned = new List<(float Time, string Name, string Mechanic)>();
            foreach (var (_, line) in PlannedLines(fight, myJob))
            {
                if (line.Time > p.CaptureElapsed - 1f) continue; // pull ended first
                foreach (var pm in Cooldowns.PlanMits(line.Action))
                {
                    if (DeltaBlind.Contains(pm.Name)) continue;
                    if (!(IsBossMit(pm.Name) || IsPartyMit(pm.Name))) continue; // recap can't see it
                    if (!CompHas(p, pm.Name)) continue; // nobody here plays it tonight
                    if (planned.Any(x => string.Equals(x.Name, pm.Name, StringComparison.OrdinalIgnoreCase)
                                         && MathF.Abs(x.Time - line.Time) < 3f)) continue;
                    planned.Add((line.Time, pm.Name, line.Mechanic.Trim()));
                }
            }
            if (planned.Count == 0) return;
            planned.Sort((a, b) => a.Time.CompareTo(b.Time));

            // Each mit's applications as intervals, matched through the same
            // alias table the log entries were named with.
            var spans = new Dictionary<string, List<(float Start, float End, uint Icon)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in p.Log.OrderBy(a => a.Time))
                foreach (var name in NamesFor(a.Mit).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!spans.TryGetValue(name, out var list)) list = spans[name] = new List<(float, float, uint)>();
                    // Party-wide statuses land one entry per member; one press
                    // is one interval.
                    if (list.Count > 0 && a.Time - list[^1].Start <= 6f)
                    {
                        if (a.End > list[^1].End) list[^1] = (list[^1].Start, a.End, list[^1].Icon);
                        continue;
                    }
                    list.Add((a.Time, a.End, a.Icon));
                }

            foreach (var (t, name, mech) in planned)
            {
                p.PlanTotal++;
                var snap = t - SnapshotLead;
                spans.TryGetValue(name, out var mine);

                // Boss damage-downs: the packet capture is the ground truth for
                // whether the debuff was baked into this mechanic's damage.
                if (IsBossMit(name) && p.Hits.Count > 0
                    && NearestHit(p.Hits, t, mech) is { } hit)
                {
                    if ((hit.DebuffMask & DamageCapture.BitOf(name)) != 0) { p.PlanGood++; continue; }
                    p.PlanProblems.Add(Diagnose(t, snap, name, mech, mine));
                    continue;
                }

                // No capture (or a party mit): grade from the interval. Covering
                // the snapshot is what "on plan" means.
                if (mine != null && mine.Any(s => s.Start - EdgeGrace <= snap && snap <= s.End + EdgeGrace))
                {
                    p.PlanGood++;
                    continue;
                }
                p.PlanProblems.Add(Diagnose(t, snap, name, mech, mine));
            }
            p.PlanProblems.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
        catch { p.PlanTotal = 0; p.PlanGood = 0; p.PlanProblems.Clear(); }
    }

    // Why a planned mit wasn't covering its mechanic's snapshot: late, fell off
    // early, up at some unrelated time, or never used at all.
    private static PlanHit Diagnose(float t, float snap, string name, string mech,
        List<(float Start, float End, uint Icon)>? spans)
    {
        if (spans is { Count: > 0 })
        {
            // Late: it went up shortly after the snapshot (the classic "pressed
            // during the cast" miss).
            var late = spans.Where(s => s.Start > snap && s.Start - t <= 12f)
                .OrderBy(s => s.Start).ToList();
            if (late.Count > 0)
                // A press between the snapshot and the hit still shows as (at
                // least) a second late: the game had already done its math.
                return new PlanHit(t, name, mech, MathF.Max(1f, late[0].Start - t), false, late[0].Icon);
            // Fell off: it WAS up, and expired before the game took the snapshot.
            var early = spans.Where(s => s.End < snap && snap - s.End <= 20f)
                .OrderByDescending(s => s.End).ToList();
            if (early.Count > 0)
                return new PlanHit(t, name, mech, 0f, true, early[0].Icon,
                    $"fell off {snap - early[0].End:0}s before the hit");
            return new PlanHit(t, name, mech, 0f, true, spans[0].Icon, "up, but not for this one");
        }
        return new PlanHit(t, name, mech, 0f, true, IconFor(name));
    }

    // The enemy hit a planned mit was for: same mechanic name if the sheet uses
    // real cast names, else the nearest connecting hit. Null when nothing landed
    // near the row (the mechanic got skipped, or the clock was off): grading
    // then falls back to intervals rather than guessing.
    private static DamageCapture.EnemyHit? NearestHit(List<DamageCapture.EnemyHit> hits, float t, string mech)
    {
        DamageCapture.EnemyHit? best = null;
        var bestAbs = float.MaxValue;
        foreach (var h in hits)
        {
            var d = MathF.Abs(h.Time - t);
            var named = mech.Length > 0 && h.Action.Length > 0 && SheetTimeline.MechEquals(h.Action, mech);
            if (d > (named ? 10f : 5f)) continue;
            if (named) d -= 100f; // a name match outranks any unnamed proximity
            if (d < bestAbs) { bestAbs = d; best = h; }
        }
        return best;
    }

    // Every plan-vocabulary name a logged status can satisfy: itself, any
    // tracked mit its text word-matches, and the pre-rename aliases.
    private static IEnumerable<string> NamesFor(string statusName)
    {
        yield return statusName.Trim();
        foreach (var pm in Cooldowns.PlanMits(statusName)) yield return pm.Name;
        foreach (var (part, canon) in StatusAliases)
            if (statusName.Contains(part, StringComparison.OrdinalIgnoreCase)) yield return canon;
    }

    // Whether anyone in this party plays a job that owns the mit, unknown owners
    // passing since the plan is trusted over an incomplete kit table.
    private static bool CompHas(PullRecap p, string mit)
    {
        var known = false;
        foreach (var (job, kit) in Cooldowns.JobKits)
            foreach (var k in kit)
                if (k.Contains(mit, StringComparison.OrdinalIgnoreCase)
                    || mit.Contains(k, StringComparison.OrdinalIgnoreCase))
                {
                    known = true;
                    if (p.Jobs.Values.Contains(job, StringComparer.OrdinalIgnoreCase)) return true;
                    break;
                }
        return !known;
    }

    private static readonly Dictionary<string, uint> IconCache = new(StringComparer.OrdinalIgnoreCase);
    private static uint IconFor(string mit)
    {
        if (IconCache.TryGetValue(mit, out var i)) return i;
        return IconCache[mit] = SampleIcon(mit);
    }

    // ---- aggregation for the recap window ---------------------------------

    // One use of a mit, with every member the same press covered.
    public sealed record MitEvent(float Time, string Mit, MitTypes.Kind Kind, bool OnBoss, uint Icon, List<string> Covered);

    public List<MitEvent> LastEvents()
    {
        var events = new List<MitEvent>();
        foreach (var a in LastLog.OrderBy(a => a.Time))
        {
            // Party buffs merge across members; everything else merges only with
            // itself.
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

    // Make the popup + window appear now (for placing them) without real data.
    public void ShowTestPopup()
    {
        if (History.Count == 0) LoadSample();
        if (History.Count > 0) History[0].CapturedAt = DateTime.UtcNow;
        PopupDismissed = false;
        Previewing = true;
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

    // Each job's own defensive cooldowns (party buffs - boss damage-downs are
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

    // Fill the recap with a randomized, comp-accurate sample pull.
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
                var d1 = sampleLog[sampleLog.Count / 2].Time + 2f;
                var d2 = sampleLog[^1].Time + 1f;
                pr.Deaths = new List<Death>
                {
                    new(d1, comp.Count > 0 ? comp[0] : "Someone",
                        "Rampart, Sacred Soil, 8% shield", 0.86f, 3.4f, "Mortal Slash (154,201)",
                        new List<DamageCapture.PlayerHit>
                        {
                            new(d1 - 3.1f, "Attack", 38112, "Rampart, Sacred Soil, 22% shield"),
                            new(d1 - 1.4f, "Mortal Slash", 154201, "Rampart, Sacred Soil, 8% shield"),
                        }),
                    new(d2, comp.Count > 1 ? comp[1] : "Someone Else", "", 0.97f, 1.8f, "Ruinous Omen (118,455)",
                        new List<DamageCapture.PlayerHit>
                        {
                            new(d2 - 2.2f, "Attack", 24860, ""),
                            new(d2 - 0.8f, "Ruinous Omen", 118455, ""),
                        }),
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
            // English: `mit` is one of our own names (see GameSheets).
            var sheet = GameSheets.English<Status>();
            if (sheet == null) return 0;
            foreach (var row in sheet)
                if (string.Equals(row.Name.ExtractText(), mit, StringComparison.OrdinalIgnoreCase))
                    return (uint)row.Icon;
        }
        catch { /* ignore */ }
        return 0;
    }

    // Standard raid damage-downs that never landed on the boss this pull
    // (informational - comp-dependent).
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
            sb.AppendLine("Still up at the end:");
            foreach (var m in Snapshot.OrderByDescending(m => m.OnBoss).ThenBy(m => m.Source))
                sb.AppendLine($"  {m.Mit} - {(m.OnBoss ? "on boss" : m.Source)} ({m.Remaining:0}s)");
        }

        if (LastDeaths.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Deaths:");
            foreach (var d in LastDeaths.OrderBy(d => d.Time))
            {
                sb.AppendLine($"  {(int)d.Time / 60}:{(int)d.Time % 60:00}  {d.Name}"
                    + (d.FromPct > 0 ? $"  ({(int)(d.FromPct * 100)}% to dead in {d.Seconds:0.0}s)" : "")
                    + (d.KilledBy.Length > 0 ? $"  killed by {d.KilledBy}" : "")
                    + (d.Had.Length > 0 ? $"  had {d.Had}" : "  nothing up"));
                if (d.Hits is { Count: > 0 })
                    foreach (var h in d.Hits)
                        sb.AppendLine($"      {(int)h.Time / 60}:{(int)h.Time % 60:00}  "
                            + (h.Action.Length > 0 ? h.Action : "hit")
                            + (h.Amount > 0 ? $"  {h.Amount:N0}" : "")
                            + $"  ({(h.Mits.Length > 0 ? "had " + h.Mits : "nothing up")})");
            }
        }

        if (Shown.PlanTotal > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Plan check: {Shown.PlanGood} of {Shown.PlanTotal} planned mits went out on plan.");
            foreach (var h in Shown.PlanProblems)
                sb.AppendLine($"  {(int)h.Time / 60}:{(int)h.Time % 60:00}  {h.Mit}"
                    + (h.Why.Length > 0 ? $" - {h.Why}" : h.Missed ? " - never went out" : $" - {h.Delta:0}s late")
                    + (h.Mechanic.Length > 0 ? $" ({h.Mechanic})" : ""));
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
        var playerMaxHp = Plugin.LocalPlayer?.MaxHp ?? 0u;
        foreach (var o in Service.ObjectTable)
            if (o is IBattleNpc n && Plugin.BossSized(n.MaxHp, playerMaxHp)
                && (boss is null || n.MaxHp > boss.MaxHp))
                boss = n;
        return boss;
    }

    private readonly record struct Hit(uint Icon, string Mit, float Remaining, MitTypes.Kind Kind);

    private static List<Hit> MitsOn(IBattleChara chara, bool onBoss)
    {
        var list = new List<Hit>();
        // English, because every status read here is matched against our own
        // English tables (StandardRaidMits, MitTypes, HealNoise) - see GameSheets.
        var sheet = GameSheets.English<Status>();
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

    // Damage-reduction buffs on players: recognised mit kinds minus the pure heals
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
