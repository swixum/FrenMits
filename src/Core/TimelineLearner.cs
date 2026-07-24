using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// One boss's timeline, learned from your own pulls.
//
// The baked timelines (UniversalTimelines) come from cactbot, which covers most
// current content but has nothing at all for a long tail of older duties - about
// 150 of them. Rather than leave those blank forever, the plugin watches the
// bosses' casts while you fight them and builds the timeline itself, so the
// second time you meet a boss the board already knows what it does.
//
// Keyed by the boss's NameId rather than the duty, which is what makes a 3-boss
// dungeon or an alliance raid work without any of the block arithmetic the baked
// timelines need: each boss is simply its own fight, starting from zero.
[Serializable]
public class LearnedFight
{
    public uint BossNameId { get; set; }
    public string BossName { get; set; } = "";
    public uint Territory { get; set; }

    // How many pulls have fed this, so a fresh measurement is weighted against
    // everything already learned instead of overwriting it.
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
    // A boss can't be learned from a pull that barely started, and no fight needs
    // more rows than this on a board.
    private const int MinCasts = 4;
    private const int MaxCasts = 220;

    // Two casts of one ability closer together than this are the same mechanic
    // ticking, not two rows worth showing.
    private const float RepeatWindow = 3f;

    // How far apart two pulls may place the same cast and still be considered the
    // same moment. Generous, because a boss's schedule shifts with kill speed.
    private const float MatchWindow = 8f;

    // Turn one pull's raw enemy casts into timeline rows: drop autos and
    // unnamed abilities, collapse rapid repeats, keep them in time order.
    public static List<LearnedCast> Distill(IEnumerable<(uint Ability, float Time, string Name)> casts)
    {
        var result = new List<LearnedCast>();
        foreach (var (ability, time, name) in casts.OrderBy(c => c.Time))
        {
            if (ability == 0 || time < 0f || !float.IsFinite(time)) continue;
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (string.Equals(name, "attack", StringComparison.OrdinalIgnoreCase)) continue;
            // Same ability again within a breath: one mechanic, one row.
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
    //
    // Deliberately monotone: a cast that has been seen once is never removed, so
    // wiping thirty seconds in can't erase the timeline learned from a much longer
    // pull. Matching casts have their times averaged (weighted by how many pulls
    // are already behind the stored value), and anything the fresh pull saw past
    // the end of what's known gets appended. Returns true if anything changed.
    public static bool Merge(LearnedFight into, List<LearnedCast> fresh)
    {
        if (fresh.Count == 0) return false;

        var changed = false;
        var weight = MathF.Max(1, into.Pulls);
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
                // Names can arrive blank if the action sheet wasn't ready.
                if (s.Name.Length == 0 && f.Name.Length > 0) { s.Name = f.Name; changed = true; }
                searchFrom = matched + 1;
            }
            else if (into.Casts.Count == 0 || f.Time > into.Casts[^1].Time + MatchWindow)
            {
                // Past everything known: this pull got further than any before it.
                into.Casts.Add(new LearnedCast { Time = f.Time, Ability = f.Ability, Name = f.Name });
                searchFrom = into.Casts.Count;
                changed = true;
            }
            // Anything else is a cast the boss does sometimes (a branch, an add
            // phase order): left out rather than allowed to fight with the
            // schedule everyone else's pulls agree on.
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

    // ---- segmenting a pull ------------------------------------------------
    // One captured "pull" is not one fight. In a dungeon the clock starts on the
    // first trash pack, so the boss's opener sits at t=300 with five minutes of
    // trash in front of it. Learning that verbatim produces a timeline that is
    // wrong in every way: wrong start, wrong contents, filed under whichever mob
    // happened to have the most HP.
    //
    // So a pull is cut down to the boss's own engagement first: everything from
    // the boss's first cast onward, rebased so that moment is zero.

    // A boss fight that produced almost nothing isn't a fight worth learning -
    // it's a trash pack, or someone pulling and immediately wiping.
    private const float MinEngagementSeconds = 40f;
    private const int MinDistinctAbilities = 3;

    public readonly record struct PullCast(uint Ability, float Time, string Name, uint CasterNameId);

    // The boss's own slice of a captured pull, rebased to start at zero. Empty
    // when the pull doesn't contain a real boss engagement.
    //
    // Casts from OTHER enemies after the boss engages are kept: adds are part of
    // the fight, and their casts are mechanics worth seeing on the board.
    // `requireEngagement` applies the "this was really a boss" gates. On by
    // default for anything being written to disk; off for the live first-pull
    // read, where the loop detection is itself the evidence and the fight is
    // still in progress.
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
            // Long enough, and varied enough, to be a boss rather than a trash pack.
            if (engaged[^1].Time - start < MinEngagementSeconds) return new List<LearnedCast>();
            if (engaged.Select(c => c.Ability).Distinct().Count() < MinDistinctAbilities)
                return new List<LearnedCast>();
        }

        return Distill(engaged.Select(c => (c.Ability, MathF.Max(0f, c.Time - start), c.Name)));
    }

    // Record a finished pull against its boss, cutting the boss's engagement out
    // of everything else the capture picked up. Returns true when the store
    // changed (the caller saves).
    public static bool LearnPull(Configuration config, uint bossNameId, string bossName, uint territory,
        IEnumerable<PullCast> casts)
    {
        var fresh = Segment(casts, bossNameId);
        if (fresh.Count < MinCasts) return false;
        return Store(config, bossNameId, bossName, territory, fresh);
    }

    // Record a pull whose times are ALREADY relative to the fight's own start.
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
        return Merge(fight, fresh);
    }

    // ---- first-pull projection --------------------------------------------
    // Learning only pays off from the second pull. The first one would show a
    // blank board - except that most bosses, especially in the older duties that
    // have no baked timeline, run their mechanics on a LOOP. Once a cycle has been
    // seen through twice, the rest of the fight is predictable from the pull
    // that's already happening.

    // A cycle has to repeat at least twice to be believed, and be long enough not
    // to be one mechanic double-tapping.
    private const int MinCycleCasts = 3;
    private const float MinCycleSeconds = 12f;
    private const float MaxCycleSeconds = 600f;
    private const int ProjectCycles = 3;

    // The repeating tail of a pull, or null when nothing convincing repeats.
    // Returned as the casts of ONE cycle plus how long the cycle takes.
    public static (List<LearnedCast> Cycle, float Period)? FindLoop(List<LearnedCast> casts)
    {
        // Longest cycle first: a boss looping A B C A B C should be read as a
        // 3-cast cycle, not as the 1-cast "C C" that a shortest-first scan finds.
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
            // The whole cycle has to have shifted by the same period, or the two
            // runs aren't really the same loop.
            var consistent = true;
            for (var i = 0; i < k && consistent; i++)
                consistent = MathF.Abs((casts[tail + i].Time - casts[prev + i].Time) - period) <= MatchWindow;
            if (!consistent) continue;

            return (casts.GetRange(tail, k), period);
        }
        return null;
    }

    // What the boss is about to do, projected from the loop it has been running.
    // Empty when nothing repeats yet, which is the honest answer for the opening
    // of a fight nobody has ever seen.
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

    // The board for a boss nobody has fought yet: whatever its loop says is
    // coming. Rebuilt as the pull goes, so the timeline appears the moment the
    // boss repeats itself rather than only on the next pull.
    public static FightProfile? BuildFromLivePull(uint territory, string bossName, uint bossNameId,
        IEnumerable<PullCast> casts)
    {
        // Segmented the same way a finished pull is, so a dungeon's trash can't
        // put a phantom loop on the board before the boss is even engaged.
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

    // The in-memory timeline-only fight for a boss we've learned, or null when it
    // isn't known yet (or hasn't been seen enough times to be worth showing).
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
        // Every cast doubles as its own resync anchor: the clock starts at zero
        // with the pull, and each recognised cast nudges it back onto the schedule.
        foreach (var c in learned.Casts)
            fight.SyncPoints.Add(new SyncPoint { Ability = c.Ability, Time = c.Time, IsPhase = false, Label = "learned" });
        return fight;
    }

    // Drop everything learned for one boss (the settings page's per-entry clear).
    public static bool Forget(Configuration config, uint bossNameId)
        => config.LearnedFights.Remove(bossNameId.ToString());
}
