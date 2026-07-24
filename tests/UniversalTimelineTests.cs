using Xunit;

namespace FrenMits.Tests;

// The baked duty timelines pack every encounter of one instance onto a single
// clock, each segment parked far from the last. Every fight starts its own combat
// from zero, so an encounter is only reachable if a PHASE anchor inside it is
// within the resync engine's forward window. Miss that and the board shows
// nothing for that boss, which looks exactly like "this duty isn't supported" -
// so it needs pinning.
public class UniversalTimelineTests
{
    // The gap that separates one encounter from the next, matching the generator
    // (tools/gen_universal_timelines.py). Blocks are NOT simply time/1000: a
    // segment can run long enough to cross the next thousand.
    private const float BlockGap = 150f;

    private static IEnumerable<uint> Territories()
    {
        for (uint t = 1; t < 2000; t++)
            if (UniversalTimelines.Has(t)) yield return t;
    }

    private sealed record Encounter(uint Territory, int Index, float Start, int Events, float? Anchor);

    // Split a baked duty into its encounters and find each one's entry anchor.
    private static List<Encounter> EncountersOf(uint territory)
    {
        var fight = UniversalTimelines.Build(territory)!;
        var times = fight.Lines.Select(l => l.Time).OrderBy(t => t).ToList();

        var starts = new List<float> { times[0] };
        for (var i = 1; i < times.Count; i++)
            if (times[i] - times[i - 1] > BlockGap) starts.Add(times[i]);

        var result = new List<Encounter>();
        for (var i = 0; i < starts.Count; i++)
        {
            var from = starts[i];
            var to = i + 1 < starts.Count ? starts[i + 1] : float.MaxValue;
            float? anchor = null;
            foreach (var sp in fight.SyncPoints.OrderBy(s => s.Time))
                if (sp.IsPhase && sp.Time >= from - 5f && sp.Time < to) { anchor = sp.Time; break; }
            result.Add(new Encounter(territory, i + 1, from,
                times.Count(t => t >= from && t < to), anchor));
        }
        return result;
    }

    private static List<Encounter> All()
        => Territories().SelectMany(EncountersOf).ToList();

    [Fact]
    public void TheBakedResourceLoads()
    {
        var count = Territories().Count();
        Assert.True(count >= 300, $"only {count} duties baked; the embedded resource looks wrong");
    }

    [Fact]
    public void ABakedDutyBuildsASilentTimelineOnlyFight()
    {
        var fight = UniversalTimelines.Build(Territories().First())!;
        Assert.True(fight.TimelineOnly);
        Assert.NotEmpty(fight.Lines);
        // Timeline-only fights never speak and never claim a mit.
        Assert.All(fight.Lines, l => Assert.False(l.Sound));
        Assert.All(fight.Lines, l => Assert.Equal("", l.Action));
    }

    [Fact]
    public void EveryEncounterIsReachableFromAStandingStart()
    {
        // The regression this exists for: with a 2000s forward window only the
        // FIRST boss of any dungeon or alliance raid could be reached, so a few
        // hundred encounters silently showed an empty board.
        var stranded = All()
            .Where(e => e.Start >= 1000f)               // 0-based openers need no jump
            .Where(e => e.Anchor is { } a && a > SyncEngine.TimelineBlockReach)
            .ToList();

        Assert.True(stranded.Count == 0,
            "encounters the clock can never jump to: "
            + string.Join(", ", stranded.Take(10).Select(e => $"terr {e.Territory} #{e.Index} @{e.Anchor}")));
    }

    [Fact]
    public void TheOldWindowReallyDidStrandMostEncounters()
    {
        // Guards the fix itself: if someone "simplifies" the forward window back
        // to the configured default, this is what it costs.
        var stranded = All().Count(e => e.Start >= 1000f && e.Anchor is { } a && a > 2000f);
        Assert.True(stranded > 150,
            $"expected the old 2000s window to strand many encounters, found {stranded}");
    }

    // An encounter with no anchorable cast in it at all can never be entered,
    // however wide the window - that's a hole in cactbot's timeline, not in the
    // engine. Ratchet only: it may shrink when the timelines are regenerated,
    // never grow.
    private const int KnownUnenterable = 2;

    [Fact]
    public void NoMoreEncountersLoseTheirEntryAnchor()
    {
        var blind = All().Where(e => e.Start >= 1000f && e.Anchor is null).ToList();

        Assert.True(blind.Count <= KnownUnenterable,
            $"{blind.Count} encounters have no anchor to enter on (was {KnownUnenterable}): "
            + string.Join(", ", blind.Take(10).Select(e => $"terr {e.Territory} #{e.Index} ({e.Events} events)")));
    }

    [Fact]
    public void MultiBossDutiesActuallyCarryEveryBoss()
    {
        // A spot check with teeth: the alliance raid that started all this, plus a
        // three-boss dungeon. Every encounter must be present AND enterable.
        foreach (var terr in new uint[] { 1368, 1248 })   // Windurst 3rd Walk, Jeuno 1st Walk
        {
            Assert.True(UniversalTimelines.Has(terr), $"territory {terr} isn't baked at all");
            var encounters = EncountersOf(terr);
            Assert.True(encounters.Count >= 3,
                $"terr {terr}: only {encounters.Count} encounters baked");
            Assert.All(encounters, e => Assert.True(e.Anchor is not null || e.Start < 1000f,
                $"terr {terr}: encounter #{e.Index} has no way in"));
        }
    }

    [Fact]
    public void EveryBakedEntryIsUsable()
    {
        foreach (var t in Territories())
        {
            var fight = UniversalTimelines.Build(t)!;
            Assert.NotEmpty(fight.Lines);
            foreach (var l in fight.Lines)
            {
                Assert.True(float.IsFinite(l.Time) && l.Time >= 0f, $"terr {t}: bad time {l.Time}");
                Assert.False(string.IsNullOrWhiteSpace(l.Mechanic), $"terr {t}: a nameless row at {l.Time}");
            }
            foreach (var sp in fight.SyncPoints)
            {
                Assert.NotEqual(0u, sp.Ability);
                Assert.True(float.IsFinite(sp.Time) && sp.Time >= 0f);
            }
        }
    }
}
