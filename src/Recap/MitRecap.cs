using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;

namespace FrenMits.Recap;

// Watches every mit in a pull, on the boss and the party.
public class MitRecap
{
    private readonly Plugin _plugin;
    private bool _wasRunning;
    private DateTime _lastScan;
    // "source|mit" currently up, each holding its open log entry.
    private readonly Dictionary<string, Applied> _open = new(StringComparer.OrdinalIgnoreCase);

    // Damage-down debuffs a full party lands on the boss.
    public static readonly string[] StandardRaidMits = { "Reprisal", "Feint", "Addle", "Dismantle" };

    // Window is what the status said it had left when it went up, so an interval
    // whose drop was never seen still ends where it really ended.
    public sealed record Applied(float Time, string Mit, string Source, MitTypes.Kind Kind, bool OnBoss, uint Icon,
        float Window = 0f)
    {
        // When it fell off, so grading can ask if it was still up.
        public float End { get; set; } = -1f;
    }
    public sealed record Active(uint Icon, string Mit, string Source, float Remaining, MitTypes.Kind Kind, bool OnBoss);

    // A death with its story, frozen so history keeps it.
    public readonly record struct Death(float Time, string Name, string Had, float FromPct, float Seconds,
        string KilledBy = "", List<DamageCapture.PlayerHit>? Hits = null);

    // One frozen pull, kept so wipes stay comparable.
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
        // Party cooldowns that sat unused.
        public List<(string Who, string Mit, string Note, uint Icon)> Unused = new();
        // Plan against actual: how many presses landed on plan.
        public int PlanTotal;
        public int PlanGood;
        public List<PlanHit> PlanProblems = new();
        // Enemy hits, each with the boss debuffs that were up.
        public List<DamageCapture.EnemyHit> Hits = new();
    }

    // One planned press that went wrong.
    public readonly record struct PlanHit(float Time, string Mit, string Mechanic, float Delta, bool Missed, uint Icon, string Why = "");

    public List<Applied> Log { get; } = new();

    // Party roster this pull, so coverage can name who missed.
    public List<string> Party { get; } = new();

    // Frozen pulls, newest first.
    public List<PullRecap> History { get; } = new();
    public int View;
    private const int MaxHistory = 6;
    private static readonly PullRecap Empty = new();
    public PullRecap Shown => History.Count > 0 ? History[Math.Clamp(View, 0, History.Count - 1)] : Empty;

    // Facade over the shown pull.
    public List<Applied> LastLog => Shown.Log;
    public List<string> LastParty => Shown.Party;
    public List<Active> Snapshot { get; private set; } = new();
    public DateTime CapturedAt => History.Count > 0 ? History[0].CapturedAt : default;
    public uint Territory => Shown.Territory;
    public bool PopupDismissed { get; private set; }
    // True while a config-page preview is showing.
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
    private uint _liveBossHp;
    private float _liveElapsed;
    private Guid _pullId = Guid.NewGuid();

    // Hide the post-wipe popup without clearing the data.
    public void Dismiss() { PopupDismissed = true; Previewing = false; }

    public MitRecap(Plugin plugin) => _plugin = plugin;

    public void Update()
    {
        try
        {
            if (!_plugin.Config.RecapEnabled) { _wasRunning = false; return; }
            // Only track inside a duty, never in the open world.
            if (!InDuty()) { _wasRunning = false; return; }

            // A phase cutscene is a freeze, not a pull boundary.
            if (Plugin.CutsceneActive) return;

            var running = _plugin.Timer.Running;
            if (running && !_wasRunning)
            {
                Log.Clear(); _open.Clear(); Party.Clear(); _deaths.Clear(); _dead.Clear();
                _jobs.Clear(); _lastMits.Clear(); _hp.Clear(); _liveBoss = ""; _liveBossHp = 0;
                _pullId = Guid.NewGuid();
                _plugin.Damage.Clear();
            }
            else if (!running && _wasRunning && Log.Count > 0) FinalizePull(); // pull ended -> freeze recap
            _wasRunning = running;
            if (!running) return;

            var fight = _plugin.ActiveFight();
            var elapsed = fight != null ? _plugin.ElapsedFor(fight) : _plugin.Timer.Elapsed;
            _liveElapsed = elapsed;

            // Server-stamped first: a mit that came and went between two scans
            // still gets its real window, and a death keeps the instant it happened.
            DrainFeeds();

            // The scan is the reconcile pass now: it catches what was already up
            // when the pull started, and closes anything the feed never saw end.
            if ((DateTime.UtcNow - _lastScan).TotalSeconds < 0.25) return;
            _lastScan = DateTime.UtcNow;
            Reconcile(elapsed);
        }
        // Leave a trail, since a recurring failure would end tracking.
        catch (Exception ex) { Swallowed.Report("mit recap tick", ex); }
    }

    private List<Active> _snapLive = new();

    // ---- interval bookkeeping ----

    // A mit goes up once per holder; a second sighting is the same window.
    private void OpenMit(float t, string mit, string source, MitTypes.Kind kind, bool onBoss, uint icon, float window)
    {
        var key = source + "|" + mit;
        if (_open.ContainsKey(key)) return;
        var a = new Applied(t, mit, source, kind, onBoss, icon, window);
        Log.Add(a);
        _open[key] = a;
    }

    private void CloseMit(float t, string mit, string source)
    {
        if (!_open.Remove(source + "|" + mit, out var gone)) return;
        gone.End = MathF.Max(gone.Time, t);
    }

    // Statuses and deaths the server stamped, taken in the order they happened.
    private void DrainFeeds()
    {
        var status = _plugin.Damage.StatusFeed;
        foreach (var s in status)
        {
            // Only the four damage-downs mean anything on an enemy.
            if (s.OnEnemy && !MitStatusBook.IsBossMit(s.Mit)) continue;
            if (!s.OnEnemy) NoteMember(s.Who);
            if (s.Applied) OpenMit(s.Time, s.Mit, s.Who, s.Kind, s.OnEnemy, s.Icon, s.Duration);
            else CloseMit(s.Time, s.Mit, s.Who);
        }
        status.Clear();

        var deaths = _plugin.Damage.DeathFeed;
        foreach (var d in deaths)
            if (_dead.Add(d.Who)) _deaths.Add(MakeDeath(d.Time, d.Who));
        deaths.Clear();
    }

    private void NoteMember(string name)
    {
        if (!Party.Contains(name)) Party.Add(name);
    }

    // The scan pass: fill in what the feed could not have seen, and close
    // anything that fell off while nobody was listening.
    private void Reconcile(float elapsed)
    {
        var live = new List<Active>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // sources this scan reached
        var up = new HashSet<string>(StringComparer.OrdinalIgnoreCase);     // "source|mit" still up

        foreach (var (src, onBoss, chara) in Sources())
        {
            seen.Add(src);
            if (onBoss)
            {
                // Strictly bigger, so two equal adds cannot swap the name each scan.
                if (chara.MaxHp > _liveBossHp) { _liveBoss = chara.Name.ToString(); _liveBossHp = chara.MaxHp; }
            }
            else
            {
                NoteMember(src);
                if (!_jobs.ContainsKey(src) && chara is IPlayerCharacter pc
                    && Jobs.ByRowId(pc.ClassJob.RowId) is { } ji)
                    _jobs[src] = ji.Abbreviation;

                // Death edge: the feed usually got here first, this is the backstop.
                if (chara.CurrentHp == 0)
                {
                    if (_dead.Add(src)) _deaths.Add(MakeDeath(elapsed, src));
                    continue;
                }
                _dead.Remove(src);

                // HP trace feeding the death story, backing the packet samples.
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
                up.Add(src + "|" + m.Mit);
                OpenMit(elapsed, m.Mit, src, m.Kind, onBoss, m.Icon, m.Remaining);
                live.Add(new Active(m.Icon, m.Mit, src, m.Remaining, m.Kind, onBoss));
                mine?.Add(m.Mit);
            }
        }

        // Only close what this scan could actually have seen: someone who walked
        // out of range still has their cooldowns running.
        foreach (var key in _open.Keys.ToList())
        {
            var bar = key.IndexOf('|');
            if (bar <= 0 || !seen.Contains(key[..bar]) || up.Contains(key)) continue;
            if (_open.Remove(key, out var gone)) gone.End = MathF.Max(gone.Time, elapsed);
        }
        _snapLive = live; // kept current, so the wipe snapshot has the boss mits
    }

    // Freeze on pull end and run the after-action analysis.
    private void FinalizePull()
    {
        Push(BuildPull(_snapLive));
        PopupDismissed = false;
        Previewing = false;
    }

    private PullRecap BuildPull(List<Active> snapshot)
    {
        // An interval nobody saw end runs to the freeze, or to where the status
        // itself said it would run out, whichever came first.
        foreach (var a in Log)
            if (a.End < 0f)
                a.End = a.Window > 0f
                    ? Math.Clamp(a.Time + a.Window, a.Time, MathF.Max(a.Time, _liveElapsed))
                    : MathF.Max(a.Time, _liveElapsed);
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
        // Grade now, while this is still the active fight.
        var fight = _plugin.ActiveFight();
        if (fight is { TimelineOnly: false } && fight.TerritoryId == pr.Territory)
            ComputePlanCheck(pr, fight, _plugin.ActiveJobAbbreviation());
        return pr;
    }

    // Newest first; a re-freeze upgrades in place.
    private void Push(PullRecap p)
    {
        var i = History.FindIndex(h => h.PullId == p.PullId);
        if (i >= 0) History.RemoveAt(i);
        History.Insert(0, p);
        while (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);
        View = 0;
    }

    // How long after a hit a death still reads as that hit.
    private const float KillingBlowWindow = 8f;
    // A hit and the death it caused share one packet batch, so they can stamp
    // the same instant either way round.
    private const float PacketGrace = 0.05f;

    // The death story: the killing blow and what was up for it.
    private Death MakeDeath(float t, string name)
    {
        var had = _lastMits.TryGetValue(name, out var lm) && lm.Count > 0
            ? string.Join(", ", lm.Take(4)) : "";
        var killedBy = "";
        List<DamageCapture.PlayerHit>? hits = null;
        if (_plugin.Damage.RecentHits.TryGetValue(name, out var ring) && ring.Count > 0)
        {
            // The death is stamped by the server and read a frame later, so the
            // ring can already hold hits from after it. Nothing that landed after
            // someone died is part of how they died.
            var over = ring.FindLastIndex(h => h.Time <= t + PacketGrace);
            if (over >= 0 && t - ring[over].Time <= KillingBlowWindow)
            {
                var hit = ring[over];
                var what = hit.Action.Length > 0 ? Fmt.Numerals(hit.Action) : hit.OverTime ? "damage over time" : "";
                killedBy = what.Length > 0
                    ? (hit.Amount > 0 ? $"{what} ({hit.Amount:N0})" : what)
                    : hit.Amount > 0 ? $"{hit.Amount:N0} damage" : "";
                // What the hit was calculated against beats a stale scan.
                had = hit.Mits;
            }
            // The run-in: only hits close enough to be one story.
            hits = ring.Where(h => h.Time <= t + PacketGrace && t - h.Time <= 12f).ToList();
            if (hits.Count == 0) hits = null;
        }

        // Every HP reading there is, so the drop is measured against the packets
        // rather than whichever quarter-second the scan happened to land on.
        var trace = new List<(float T, float Pct)>();
        if (_hp.TryGetValue(name, out var hpRing))
            foreach (var s in hpRing)
                if (s.T <= t) trace.Add(s);
        if (ring != null)
            foreach (var h in ring)
                if (h.MaxHp > 0 && h.Time <= t + PacketGrace) trace.Add((h.Time, h.Hp / (float)h.MaxHp));
        var from = 0f; var secs = 0f;
        if (trace.Count > 0)
        {
            trace.Sort((a, b) => a.T.CompareTo(b.T));
            // The most recent healthy moment, else the best HP seen.
            (float T, float Pct)? healthy = null;
            for (var i = trace.Count - 1; i >= 0; i--)
                if (trace[i].Pct >= 0.7f) { healthy = trace[i]; break; }
            var pick = healthy ?? trace.OrderByDescending(x => x.Pct).First();
            from = pick.Pct;
            secs = MathF.Max(0.1f, t - pick.T);
        }
        return new Death(t, name, had, from, secs, killedBy, hits);
    }

    // The recognized mits on a player, for the at-the-hit read.
    internal static IEnumerable<string> MitNamesOn(IBattleChara chara)
    {
        foreach (var h in MitsOn(chara, onBoss: false)) yield return h.Mit;
    }

    // One button, whatever the level it synced to calls it.
    private static bool SameMit(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        var fa = AbilityBook.SharedFamily.GetValueOrDefault(a, "");
        return fa.Length > 0 && string.Equals(fa, AbilityBook.SharedFamily.GetValueOrDefault(b, ""), StringComparison.Ordinal);
    }

    // Follow-ups that only exist inside another cooldown.
    private static readonly HashSet<string> DependentMits = new(StringComparer.OrdinalIgnoreCase)
        { "Divine Caress", "Sun Sign" };

    // Party-facing cooldowns that sat unused all pull.
    public static List<(string Who, string Mit, string Note, uint Icon)> ComputeUnused(PullRecap p)
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
                if (!AbilityBook.JobKits.TryGetValue(job, out var kit)) continue;
                foreach (var mit in kit)
                {
                    if (DependentMits.Contains(mit)) continue;
                    if (MitStatusBook.KindOf(mit) != MitTypes.Kind.Party) continue;
                    var recast = CooldownTracker.PlanInfo(mit)?.Recast ?? 0f;
                    if (recast < 45f) continue; // short rollers are never "wasted"
                    var times = p.Log.Where(a => !a.OnBoss && SameMit(a.Mit, mit))
                        .Select(a => a.Time).ToList();
                    if (times.Count == 0)
                    {
                        // Only nag once the pull was long enough to use it.
                        if (p.CaptureElapsed >= recast * 0.9f)
                            res.Add((name, mit, "never used", IconFor(mit)));
                    }
                    else
                    {
                        var idle = p.CaptureElapsed - times.Max() - recast;
                        if (idle >= 20f) res.Add((name, mit, $"was back {(int)idle}s before the end", IconFor(mit)));
                    }
                }
            }
        }
        catch (Exception ex) { Swallowed.Report("recap unused-cooldown analysis", ex); }
        return res.OrderBy(r => r.Item3 == "never used" ? 0 : 1)
            .ThenBy(r => r.Item2, StringComparer.OrdinalIgnoreCase)
            .Take(10).ToList();
    }

    // ---- plan against actual ----

    // The log names the button now, so only the upgrade pairs still need a
    // bridge: a synced job presses the old one where the sheet says the new.
    public static readonly (string StatusPart, string Canon)[] StatusAliases =
    {
        ("Damnation", "Vengeance"), ("Vengeance", "Damnation"),
        ("Guardian", "Sentinel"), ("Sentinel", "Guardian"),
        ("Great Nebula", "Nebula"), ("Nebula", "Great Nebula"),
        ("Shadowed Vigil", "Shadow Wall"), ("Shadow Wall", "Shadowed Vigil"),
        ("Bloodwhetting", "Raw Intuition"), ("Raw Intuition", "Bloodwhetting"),
    };

    // Planned actions the recap can never observe.
    public static readonly HashSet<string> DeltaBlind =
        new(StringComparer.OrdinalIgnoreCase) { "Second Wind", "Bloodbath", "Equilibrium" };

    // Every slot's planned lines, live plus saved.
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

    // Damage is decided a beat before the hit, so grade there.
    internal const float SnapshotLead = TimingSolver.SnapshotLead;
    // Slack on both ends of the window: exactly when the server decided a hit
    // is a beat either side of where the plan says the mechanic lands.
    private const float EdgeGrace = 0.6f;

    // Grade the sheet against the pull, press by press.
    public static void ComputePlanCheck(PullRecap p, FightProfile fight, string? myJob)
    {
        try
        {
            if (p.Log.Count == 0 && p.Hits.Count == 0) return;

            // What the plan expects, deduped and comp-possible.
            var planned = new List<(float Time, string Name, string Mechanic)>();
            foreach (var (_, line) in PlannedLines(fight, myJob))
            {
                if (line.Time > p.CaptureElapsed - 1f) continue; // pull ended first
                foreach (var pm in CooldownTracker.PlanMits(line.Action))
                {
                    if (DeltaBlind.Contains(pm.Name)) continue;
                    if (!MitStatusBook.IsTrackedAction(pm.Name)) continue; // recap can't see it
                    if (!CompHas(p, pm.Name)) continue; // nobody here plays it tonight
                    if (planned.Any(x => string.Equals(x.Name, pm.Name, StringComparison.OrdinalIgnoreCase)
                                         && MathF.Abs(x.Time - line.Time) < 3f)) continue;
                    planned.Add((line.Time, pm.Name, line.Mechanic.Trim()));
                }
            }
            if (planned.Count == 0) return;
            planned.Sort((a, b) => a.Time.CompareTo(b.Time));

            // Each mit's applications as intervals, alias-matched. A party press
            // logs one entry per member, so the names are resolved once each
            // rather than thousands of times over.
            var spans = new Dictionary<string, List<(float Start, float End, uint Icon)>>(StringComparer.OrdinalIgnoreCase);
            var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in p.Log.OrderBy(a => a.Time))
            {
                if (!aliases.TryGetValue(a.Mit, out var names))
                    names = aliases[a.Mit] = NamesFor(a.Mit).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                foreach (var name in names)
                {
                    if (!spans.TryGetValue(name, out var list)) list = spans[name] = new List<(float, float, uint)>();
                    // Party statuses land per member, but one press is one interval.
                    if (list.Count > 0 && a.Time - list[^1].Start <= 6f)
                    {
                        if (a.End > list[^1].End) list[^1] = (list[^1].Start, a.End, list[^1].Icon);
                        continue;
                    }
                    list.Add((a.Time, a.End, a.Icon));
                }
            }

            foreach (var (t, name, mech) in planned)
            {
                p.PlanTotal++;
                var snap = t - SnapshotLead;
                spans.TryGetValue(name, out var mine);

                // The interval is stamped when the server applied the status, so
                // it answers the only question that matters: was it up at snapshot.
                if (mine != null && mine.Any(s => s.Start - EdgeGrace <= snap && snap <= s.End + EdgeGrace))
                {
                    p.PlanGood++;
                    continue;
                }

                // Nothing logged at all: a hit that carried the debuff still proves
                // it went out, which covers a pull the status feed never reached.
                if (mine == null && MitStatusBook.IsBossMit(name) && p.Hits.Count > 0
                    && NearestHit(p.Hits, t, mech) is { } hit
                    && (hit.DebuffMask & MitStatusBook.BitOf(name)) != 0)
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

    // Why a planned mit missed its snapshot.
    private static PlanHit Diagnose(float t, float snap, string name, string mech,
        List<(float Start, float End, uint Icon)>? spans)
    {
        if (spans is { Count: > 0 })
        {
            // Late: it went up shortly after the snapshot.
            var late = spans.Where(s => s.Start > snap && s.Start - t <= 12f)
                .OrderBy(s => s.Start).ToList();
            if (late.Count > 0)
                // A press after the snapshot is at least a second late.
                return new PlanHit(t, name, mech, MathF.Max(1f, late[0].Start - t), false, late[0].Icon);
            // Fell off: it was up, and expired before the snapshot.
            var early = spans.Where(s => s.End < snap && snap - s.End <= 20f)
                .OrderByDescending(s => s.End).ToList();
            if (early.Count > 0)
                return new PlanHit(t, name, mech, 0f, true, early[0].Icon,
                    $"fell off {snap - early[0].End:0}s before the hit");
            return new PlanHit(t, name, mech, 0f, true, spans[0].Icon, "up, but not for this one");
        }
        return new PlanHit(t, name, mech, 0f, true, IconFor(name));
    }

    // The enemy hit a planned mit was for, or null when none landed.
    private static DamageCapture.EnemyHit? NearestHit(List<DamageCapture.EnemyHit> hits, float t, string mech)
    {
        DamageCapture.EnemyHit? best = null;
        var bestScore = float.MaxValue;
        foreach (var h in hits)
        {
            var d = MathF.Abs(h.Time - t);
            var named = mech.Length > 0 && h.Action.Length > 0 && SheetTimeline.MechEquals(h.Action, mech);
            // An unnamed hit only stands in for a mechanic if it hit the party;
            // a tank auto lands in the same window and answers for a different caster.
            if (named ? d > 10f : (d > 4f || h.PlayerTargets < 2)) continue;
            var score = named ? d - 100f : d; // a name match outranks any unnamed proximity
            if (score < bestScore) { bestScore = score; best = h; }
        }
        return best;
    }

    // Every planned name a logged mit can satisfy.
    private static IEnumerable<string> NamesFor(string mit)
    {
        var name = mit.Trim();
        yield return name;
        foreach (var pm in CooldownTracker.PlanMitsCached(name)) yield return pm.Name;
        foreach (var (part, canon) in StatusAliases)
            if (name.Contains(part, StringComparison.OrdinalIgnoreCase)) yield return canon;
    }

    // Whether anyone here plays a job that owns the mit.
    private static bool CompHas(PullRecap p, string mit)
    {
        var known = false;
        foreach (var (job, kit) in AbilityBook.JobKits)
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

    // ---- aggregation for the recap window ----

    // One use of a mit, with everyone the press covered.
    public sealed record MitEvent(float Time, string Mit, MitTypes.Kind Kind, bool OnBoss, uint Icon, List<string> Covered);

    public List<MitEvent> LastEvents()
    {
        var events = new List<MitEvent>();
        foreach (var a in LastLog.OrderBy(a => a.Time))
        {
            // Party buffs merge across members, others only with self.
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

    // Show the popup and window now, for placing them.
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

    // ---- sample data ----

    private static readonly string[] SampleTanks = { "Paladin", "Warrior", "Dark Knight", "Gunbreaker" };
    private static readonly string[] SampleHealers = { "White Mage", "Scholar", "Astrologian", "Sage" };
    private static readonly string[] SampleMelee = { "Monk", "Dragoon", "Ninja", "Samurai", "Reaper", "Viper" };
    private static readonly string[] SampleRanged = { "Bard", "Machinist", "Dancer" };
    private static readonly string[] SampleCasters = { "Black Mage", "Summoner", "Red Mage", "Pictomancer" };

    private static readonly string[] SampleBosses =
        { "Dragon-king Thordan", "Golden Bahamut", "The Omega Protocol", "Kefka", "Alexander Prime", "Pandora" };

    // A job's kit, by the full name the sample comps are built from. Reading
    // the one shipped table keeps the preview honest about what the recap sees.
    private static string[] SampleKit(string jobName)
    {
        var abbr = Jobs.All.FirstOrDefault(j => string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase))
            .Abbreviation;
        return abbr != null && AbilityBook.JobKits.TryGetValue(abbr, out var kit)
            ? kit
            : Array.Empty<string>();
    }

    // Fill the recap with a randomized sample pull.
    public void LoadSample()
    {
        try
        {
            var rnd = new Random();
            string Pick(string[] pool) => pool[rnd.Next(pool.Length)];

            // A realistic comp, distinct jobs so coverage is reachable.
            var dps = SampleMelee.Concat(SampleRanged).Concat(SampleCasters).OrderBy(_ => rnd.Next()).Take(4).ToList();
            var comp = SampleTanks.OrderBy(_ => rnd.Next()).Take(2)
                .Concat(SampleHealers.OrderBy(_ => rnd.Next()).Take(2))
                .Concat(dps).ToList();

            // Which boss damage-downs the comp could provide.
            var canProvide = new List<string>();
            if (comp.Any(j => SampleTanks.Contains(j))) canProvide.Add("Reprisal");
            if (comp.Any(j => SampleMelee.Contains(j))) canProvide.Add("Feint");
            if (comp.Any(j => SampleCasters.Contains(j))) canProvide.Add("Addle");
            if (comp.Contains("Machinist")) canProvide.Add("Dismantle");
            // Land most but not all, so something shows missing.
            var landed = canProvide.OrderBy(_ => rnd.Next())
                .Take(Math.Max(1, canProvide.Count - rnd.Next(1, 2))).ToList();

            var seq = new List<(string mit, string src, bool onBoss)>();
            foreach (var b in landed) { seq.Add((b, "Boss", true)); if (rnd.Next(3) == 0) seq.Add((b, "Boss", true)); }
            foreach (var job in comp)
                foreach (var buff in SampleKit(job).Where(b => !canProvide.Contains(b))
                             .OrderBy(_ => rnd.Next()).Take(1 + rnd.Next(3)))
                    seq.Add((buff, job, false));

            var log = new List<Applied>();
            var t = 10f + rnd.Next(8);
            foreach (var (mit, src, onBoss) in seq.OrderBy(_ => rnd.Next()))
            {
                t += 6 + rnd.Next(20);
                var kind = MitStatusBook.KindOf(mit);
                var window = AbilityBook.WindowOf(mit);
                // A sample interval runs its real window, so the preview charts
                // the same shape a pull would.
                log.Add(new Applied(t, mit, src, kind, onBoss, SampleIcon(mit), window)
                    { End = t + (window > 0f ? window : 15f) });
                // Party buffs emit an entry per member, so coverage previews.
                if (!onBoss && kind == MitTypes.Kind.Party)
                    foreach (var member in comp.Where(m => m != src).OrderBy(_ => rnd.Next())
                                 .Take(comp.Count - 1 - rnd.Next(0, 3)))
                        log.Add(new Applied(t + 0.3f, mit, member, kind, false, SampleIcon(mit), window)
                            { End = t + 0.3f + (window > 0f ? window : 15f) });
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
            // Sample jobs by full name, so unused analysis previews.
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
            // English, since mit is one of our own names.
            var sheet = GameData.English<Status>();
            if (sheet == null) return 0;
            foreach (var row in sheet)
                if (string.Equals(row.Name.ExtractText(), mit, StringComparison.OrdinalIgnoreCase))
                    return (uint)row.Icon;
        }
        catch { /* ignore */ }
        return 0;
    }

    // Standard damage-downs that never landed this pull.
    public List<string> NotSeen()
        => StandardRaidMits
            .Where(s => !LastLog.Any(a => a.OnBoss && string.Equals(a.Mit, s, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    public bool HasData => History.Count > 0;

    // A plain-text recap for the clipboard.
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
            : "Never landed: " + string.Join(", ", missed.Select(Fmt.Numerals)));

        if (Snapshot.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Still up at the end:");
            foreach (var m in Snapshot.OrderByDescending(m => m.OnBoss).ThenBy(m => m.Source))
                sb.AppendLine($"  {Fmt.Numerals(m.Mit)} - {(m.OnBoss ? "on boss" : m.Source)} ({m.Remaining:0}s)");
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
                            + (h.Action.Length > 0 ? Fmt.Numerals(h.Action) : h.OverTime ? "damage over time" : "hit")
                            + (h.OverTime ? " (tick)" : "")
                            + (h.Amount > 0 ? $"  {h.Amount:N0}" : "")
                            + $"  ({(h.Mits.Length > 0 ? "had " + h.Mits : "nothing up")})");
            }
        }

        if (Shown.PlanTotal > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Plan check: {Shown.PlanGood} of {Shown.PlanTotal} planned mits went out on plan.");
            foreach (var h in Shown.PlanProblems)
                sb.AppendLine($"  {(int)h.Time / 60}:{(int)h.Time % 60:00}  {Fmt.Numerals(h.Mit)}"
                    + (h.Why.Length > 0 ? $" - {h.Why}" : h.Missed ? " - never went out" : $" - {h.Delta:0}s late")
                    + (h.Mechanic.Length > 0 ? $" ({Fmt.Numerals(h.Mechanic)})" : ""));
        }

        if (Shown.Unused.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Left on the table:");
            foreach (var (who, mit, note, _) in Shown.Unused)
                sb.AppendLine($"  {Fmt.Numerals(mit)} - {who}: {note}");
        }

        if (LastLog.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Applied this pull:");
            foreach (var a in LastLog.OrderBy(a => a.Time))
                sb.AppendLine($"  {(int)a.Time / 60}:{(int)a.Time % 60:00}  {Fmt.Numerals(a.Mit)} - {(a.OnBoss ? "on boss" : a.Source)}");
        }
        return sb.ToString();
    }

    // What we read statuses off: every enemy worth a damage-down, and every player.
    private static IEnumerable<(string source, bool onBoss, IBattleChara chara)> Sources()
    {
        var playerMaxHp = Plugin.LocalPlayer?.MaxHp ?? 0u;
        foreach (var o in Service.ObjectTable)
            // A second target carries its own Reprisal, so it is its own source.
            if (o is IBattleNpc n && (byte)n.BattleNpcKind == 5 && Plugin.BossSized(n.MaxHp, playerMaxHp))
                yield return (n.Name.ToString(), true, n);

        foreach (var o in Service.ObjectTable)
            if (o is IPlayerCharacter pc && pc.MaxHp > 0)
                yield return (pc.Name.ToString(), false, pc);
    }

    private readonly record struct Hit(uint Icon, string Mit, float Remaining, MitTypes.Kind Kind);

    // A mit can hold more than one status; the button is what the recap logs.
    private static List<Hit> MitsOn(IBattleChara chara, bool onBoss)
    {
        var list = new List<Hit>();
        foreach (var st in chara.StatusList)
        {
            if (st is null || st.StatusId == 0) continue;
            if (MitStatusBook.Resolve(st.StatusId) is not { } e) continue;
            if (onBoss && !MitStatusBook.IsBossMit(e.Mit)) continue;
            var remaining = MathF.Abs(st.RemainingTime);
            var at = list.FindIndex(h => string.Equals(h.Mit, e.Mit, StringComparison.OrdinalIgnoreCase));
            if (at >= 0)
            {
                // Two statuses, one button: the window is the longer of them.
                if (remaining > list[at].Remaining) list[at] = list[at] with { Remaining = remaining };
                continue;
            }
            list.Add(new Hit(e.Icon, e.Mit, remaining, e.Kind));
        }
        return list;
    }
}
