using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// One boss's timeline, learned from your own pulls.
[Serializable]
public class LearnedFight
{
    public uint BossNameId { get; set; }
    public string BossName { get; set; } = "";
    public uint Territory { get; set; }

    // How many pulls fed this, so new data is weighted in.
    public int Pulls { get; set; }
    public DateTime LastSeen { get; set; }

    public List<LearnedCast> Casts { get; set; } = new();
}

[Serializable]
public class LearnedCast
{
    public float Time { get; set; }
    public uint Ability { get; set; }
    public string Name { get; set; } = "";
}

public static class TimelineLearner
{
    // Floor and ceiling for what a pull can teach.
    private const int MinCasts = 4;
    private const int MaxCasts = 220;

    // Two casts closer than this are one mechanic ticking.
    private const float RepeatWindow = 3f;

    // How far two pulls may differ and still mean one moment.
    private const float MatchWindow = 8f;

    // One pull's raw casts turned into timeline rows.
    public static List<LearnedCast> Distill(IEnumerable<(uint Ability, float Time, string Name)> casts)
    {
        var result = new List<LearnedCast>();
        foreach (var (ability, time, name) in casts.OrderBy(c => c.Time))
        {
            if (ability == 0 || time < 0f || !float.IsFinite(time)) continue;
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (string.Equals(name, "attack", StringComparison.OrdinalIgnoreCase)) continue;
            // Same ability within a breath: one mechanic, one row.
            if (result.Count > 0)
            {
                var last = result[^1];
                if (last.Ability == ability && time - last.Time < RepeatWindow) continue;
            }
            result.Add(new LearnedCast { Time = MathF.Round(time, 1), Ability = ability, Name = name.Trim() });
            if (result.Count >= MaxCasts) break;
        }
        return result;
    }

    // Fold a fresh pull into what's already known.
    public static bool Merge(LearnedFight into, List<LearnedCast> fresh)
    {
        if (fresh.Count == 0) return false;

        var changed = false;
        // Capped, or a retuned boss would never be re-learned.
        var weight = MathF.Min(MathF.Max(1, into.Pulls), 8f);
        var searchFrom = 0;

        foreach (var f in fresh)
        {
            var matched = -1;
            for (var i = searchFrom; i < into.Casts.Count; i++)
            {
                var s = into.Casts[i];
                if (s.Ability != f.Ability) continue;
                if (MathF.Abs(s.Time - f.Time) > MatchWindow) continue;
                matched = i;
                break;
            }

            if (matched >= 0)
            {
                var s = into.Casts[matched];
                var blended = MathF.Round((s.Time * weight + f.Time) / (weight + 1f), 1);
                if (MathF.Abs(blended - s.Time) > 0.05f) { s.Time = blended; changed = true; }
                // Names arrive blank if the action sheet wasn't ready.
                if (s.Name.Length == 0 && f.Name.Length > 0) { s.Name = f.Name; changed = true; }
                searchFrom = matched + 1;
            }
            else if (into.Casts.Count == 0 || f.Time > into.Casts[^1].Time + MatchWindow)
            {
                // This pull got further than any before it.
                into.Casts.Add(new LearnedCast { Time = f.Time, Ability = f.Ability, Name = f.Name });
                searchFrom = into.Casts.Count;
                changed = true;
            }
            // A cast the boss only sometimes does stays out.
        }

        if (changed || fresh.Count > 0)
        {
            into.Casts.Sort((a, b) => a.Time.CompareTo(b.Time));
            into.Pulls++;
            into.LastSeen = DateTime.UtcNow;
            changed = true;
        }
        return changed;
    }

    // ---- segmenting a pull ----

    // A pull that produced almost nothing isn't a fight.
    private const float MinEngagementSeconds = 40f;
    private const int MinDistinctAbilities = 3;

    public readonly record struct PullCast(uint Ability, float Time, string Name, uint CasterNameId);

    // The boss's own slice of a capture, rebased to zero.
    public static List<LearnedCast> Segment(IEnumerable<PullCast> casts, uint bossNameId,
        bool requireEngagement = true)
    {
        if (bossNameId == 0) return new List<LearnedCast>();
        var ordered = casts.OrderBy(c => c.Time).ToList();

        var start = -1f;
        foreach (var c in ordered)
            if (c.CasterNameId == bossNameId) { start = c.Time; break; }
        if (start < 0f) return new List<LearnedCast>();   // the boss never cast anything

        var engaged = ordered.Where(c => c.Time >= start - 1f).ToList();
        if (engaged.Count == 0) return new List<LearnedCast>();

        if (requireEngagement)
        {
            // Long and varied enough to be a boss, not trash.
            if (engaged[^1].Time - start < MinEngagementSeconds) return new List<LearnedCast>();
            if (engaged.Select(c => c.Ability).Distinct().Count() < MinDistinctAbilities)
                return new List<LearnedCast>();
        }

        return Distill(engaged.Select(c => (c.Ability, MathF.Max(0f, c.Time - start), c.Name)));
    }

    // Record a finished pull against its boss.
    public static bool LearnPull(Configuration config, uint bossNameId, string bossName, uint territory,
        IEnumerable<PullCast> casts)
    {
        var fresh = Segment(casts, bossNameId);
        if (fresh.Count < MinCasts) return false;
        return Store(config, bossNameId, bossName, territory, fresh);
    }

    // Record a pull already relative to the fight's start.
    public static bool Learn(Configuration config, uint bossNameId, string bossName, uint territory,
        IEnumerable<(uint Ability, float Time, string Name)> casts)
    {
        if (bossNameId == 0) return false;
        var fresh = Distill(casts);
        if (fresh.Count < MinCasts) return false;   // a wipe in the opener teaches nothing
        return Store(config, bossNameId, bossName, territory, fresh);
    }

    private static bool Store(Configuration config, uint bossNameId, string bossName, uint territory,
        List<LearnedCast> fresh)
    {

        var key = bossNameId.ToString();
        if (!config.LearnedFights.TryGetValue(key, out var fight))
        {
            config.LearnedFights[key] = fight = new LearnedFight
            {
                BossNameId = bossNameId,
                BossName = bossName,
                Territory = territory,
            };
        }
        if (fight.BossName.Length == 0 && bossName.Length > 0) fight.BossName = bossName;
        if (fight.Territory == 0) fight.Territory = territory;
        var changed = Merge(fight, fresh);
        Prune(config);
        return changed;
    }

    // ---- first-pull projection ----

    // A cycle must repeat twice and be longer than one mechanic.
    private const int MinCycleCasts = 3;
    private const float MinCycleSeconds = 12f;
    private const float MaxCycleSeconds = 600f;
    private const int ProjectCycles = 3;

    // The repeating tail of a pull, or null when none repeats.
    public static (List<LearnedCast> Cycle, float Period)? FindLoop(List<LearnedCast> casts)
    {
        // Longest cycle first, so A B C doesn't read as C C.
        for (var k = casts.Count / 2; k >= MinCycleCasts; k--)
        {
            var tail = casts.Count - k;
            var prev = tail - k;
            var same = true;
            for (var i = 0; i < k && same; i++)
                same = casts[tail + i].Ability == casts[prev + i].Ability;
            if (!same) continue;

            var period = casts[tail].Time - casts[prev].Time;
            if (period < MinCycleSeconds || period > MaxCycleSeconds) continue;
            // The whole cycle has to have shifted by one period.
            var consistent = true;
            for (var i = 0; i < k && consistent; i++)
                consistent = MathF.Abs((casts[tail + i].Time - casts[prev + i].Time) - period) <= MatchWindow;
            if (!consistent) continue;

            return (casts.GetRange(tail, k), period);
        }
        return null;
    }

    // What the boss is about to do, from the loop it runs.
    public static List<LearnedCast> ProjectLoop(List<LearnedCast> casts)
    {
        var result = new List<LearnedCast>();
        if (FindLoop(casts) is not { } loop) return result;
        for (var c = 1; c <= ProjectCycles; c++)
            foreach (var cast in loop.Cycle)
                result.Add(new LearnedCast
                {
                    Time = MathF.Round(cast.Time + loop.Period * c, 1),
                    Ability = cast.Ability,
                    Name = cast.Name,
                });
        return result;
    }

    // The board for a boss nobody has fought yet.
    public static FightProfile? BuildFromLivePull(uint territory, string bossName, uint bossNameId,
        IEnumerable<PullCast> casts)
    {
        // Segmented like a finished pull, so trash can't fake a loop.
        var seen = Segment(casts, bossNameId, requireEngagement: false);
        var projected = ProjectLoop(seen);
        if (projected.Count == 0) return null;

        var fight = new FightProfile
        {
            TerritoryId = territory,
            Name = bossName.Length > 0 ? bossName : "Learning this fight",
            Category = "Other",
            TimelineOnly = true,
        };
        foreach (var c in projected)
            fight.Lines.Add(new MitLine { Time = c.Time, Mechanic = c.Name, Action = "", Sound = false });
        return fight;
    }

    // The learned timeline for a boss, or null when unknown.
    public static FightProfile? Build(Configuration config, uint bossNameId, uint territory)
    {
        if (bossNameId == 0) return null;
        if (!config.LearnedFights.TryGetValue(bossNameId.ToString(), out var learned)) return null;
        if (learned.Casts.Count < MinCasts) return null;

        var fight = new FightProfile
        {
            TerritoryId = territory,
            Name = learned.BossName.Length > 0 ? learned.BossName : "Learned timeline",
            Category = "Other",
            TimelineOnly = true,
        };
        foreach (var c in learned.Casts)
            fight.Lines.Add(new MitLine { Time = c.Time, Mechanic = c.Name, Action = "", Sound = false });
        // Every cast doubles as its own resync anchor.
        foreach (var c in learned.Casts)
            fight.SyncPoints.Add(new SyncPoint { Ability = c.Ability, Time = c.Time, IsPhase = false, Label = "learned" });
        return fight;
    }

    // Bounded, dropping whatever went unseen longest.
    private const int MaxLearnedFights = 400;

    private static void Prune(Configuration config)
    {
        if (config.LearnedFights.Count <= MaxLearnedFights) return;
        foreach (var stale in config.LearnedFights
                     .OrderBy(kv => kv.Value.LastSeen)
                     .Take(config.LearnedFights.Count - MaxLearnedFights)
                     .Select(kv => kv.Key)
                     .ToList())
            config.LearnedFights.Remove(stale);
    }

    // Drop everything learned for one boss.
    public static bool Forget(Configuration config, uint bossNameId)
        => config.LearnedFights.Remove(bossNameId.ToString());
}
