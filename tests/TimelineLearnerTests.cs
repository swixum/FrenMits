using Xunit;

namespace FrenMits.Tests;

// Timelines learned from the player's own pulls, for the ~150 duties cactbot
// never covered. The danger here isn't crashing, it's silent corruption: a wipe
// in the opener must not erase a timeline learned from a full clear, and repeated
// pulls must converge rather than drift.
public class TimelineLearnerTests
{
    private static (uint, float, string)[] Pull(params (float Time, uint Ability, string Name)[] casts)
        => casts.Select(c => (c.Ability, c.Time, c.Name)).ToArray();

    private static readonly (float, uint, string)[] FullPull =
    {
        (10f, 100u, "Opener"), (25f, 101u, "Raidwide"), (44f, 102u, "Tank Buster"),
        (61f, 103u, "Adds"), (95f, 104u, "Enrage"),
    };

    [Fact]
    public void ADistilledPullKeepsTheMechanicsInOrder()
    {
        var casts = TimelineLearner.Distill(Pull(FullPull));
        Assert.Equal(5, casts.Count);
        Assert.Equal(new[] { "Opener", "Raidwide", "Tank Buster", "Adds", "Enrage" },
            casts.Select(c => c.Name));
        for (var i = 1; i < casts.Count; i++)
            Assert.True(casts[i].Time >= casts[i - 1].Time);
    }

    [Fact]
    public void AutoAttacksAndNamelessAbilitiesAreDropped()
    {
        var casts = TimelineLearner.Distill(Pull(
            (5f, 7u, "attack"), (10f, 100u, "Opener"), (12f, 999u, ""), (20f, 101u, "Raidwide")));
        Assert.Equal(new[] { "Opener", "Raidwide" }, casts.Select(c => c.Name));
    }

    [Fact]
    public void OneMechanicTickingIsOneRow()
    {
        // A cleave that hits four times in two seconds is one thing to show.
        var casts = TimelineLearner.Distill(Pull(
            (10f, 100u, "Cleave"), (10.5f, 100u, "Cleave"), (11f, 100u, "Cleave"), (12f, 100u, "Cleave"),
            (40f, 100u, "Cleave")));
        Assert.Equal(2, casts.Count);   // the burst, then the genuine repeat later
    }

    [Fact]
    public void OutOfOrderCapturesAreSortedNotDropped()
    {
        var casts = TimelineLearner.Distill(Pull(
            (44f, 102u, "Third"), (10f, 100u, "First"), (25f, 101u, "Second")));
        Assert.Equal(new[] { "First", "Second", "Third" }, casts.Select(c => c.Name));
    }

    [Fact]
    public void AFirstPullIsLearnedWhole()
    {
        var config = new Configuration();
        Assert.True(TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(FullPull)));

        var learned = config.LearnedFights["4242"];
        Assert.Equal(5, learned.Casts.Count);
        Assert.Equal(1, learned.Pulls);
        Assert.Equal("Test Boss", learned.BossName);
        Assert.Equal(1000u, learned.Territory);
    }

    [Fact]
    public void AnOpenerWipeTeachesNothing()
    {
        // Two casts in isn't a timeline, it's a bad pull.
        var config = new Configuration();
        Assert.False(TimelineLearner.Learn(config, 4242, "Test Boss", 1000,
            Pull((10f, 100u, "Opener"), (25f, 101u, "Raidwide"))));
        Assert.Empty(config.LearnedFights);
    }

    [Fact]
    public void AWipeNeverErasesWhatALongerPullTaught()
    {
        // The single most important property here. Learn a full clear, then wipe
        // 30 seconds in; the late mechanics must survive.
        var config = new Configuration();
        TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(FullPull));
        TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(
            (10f, 100u, "Opener"), (25f, 101u, "Raidwide"), (30f, 105u, "Died Here"), (31f, 106u, "Wipe")));

        var names = config.LearnedFights["4242"].Casts.Select(c => c.Name).ToList();
        Assert.Contains("Adds", names);
        Assert.Contains("Enrage", names);
    }

    [Fact]
    public void RepeatedPullsConvergeOnTheRealTiming()
    {
        // Same boss, slightly different kill speeds. The stored time should settle
        // between them rather than snapping to whichever pull was last.
        var config = new Configuration();
        TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(FullPull));
        for (var i = 0; i < 6; i++)
            TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(
                (12f, 100u, "Opener"), (27f, 101u, "Raidwide"), (46f, 102u, "Tank Buster"),
                (63f, 103u, "Adds"), (97f, 104u, "Enrage")));

        var opener = config.LearnedFights["4242"].Casts.First(c => c.Ability == 100);
        Assert.InRange(opener.Time, 10f, 12f);
        Assert.True(opener.Time > 11f, $"should have converged toward 12s, sat at {opener.Time}");
    }

    [Fact]
    public void LearningIsMonotone()
    {
        // However many pulls go in, the timeline only ever grows.
        var config = new Configuration();
        TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(FullPull));
        var count = config.LearnedFights["4242"].Casts.Count;
        for (var i = 0; i < 20; i++)
        {
            TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(
                (10f, 100u, "Opener"), (25f, 101u, "Raidwide"), (44f, 102u, "Tank Buster")));
            Assert.True(config.LearnedFights["4242"].Casts.Count >= count);
            count = config.LearnedFights["4242"].Casts.Count;
        }
    }

    [Fact]
    public void APullThatGetsFurtherExtendsTheTimeline()
    {
        var config = new Configuration();
        TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(
            (10f, 100u, "Opener"), (25f, 101u, "Raidwide"), (44f, 102u, "Tank Buster"), (61f, 103u, "Adds")));
        TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(FullPull));

        Assert.Contains(config.LearnedFights["4242"].Casts, c => c.Name == "Enrage");
    }

    [Fact]
    public void EachBossKeepsItsOwnTimeline()
    {
        // What makes a 3-boss dungeon work without any block arithmetic.
        var config = new Configuration();
        TimelineLearner.Learn(config, 1, "First Boss", 1000, Pull(FullPull));
        TimelineLearner.Learn(config, 2, "Second Boss", 1000, Pull(
            (8f, 200u, "Other Opener"), (20f, 201u, "Other Raidwide"),
            (35f, 202u, "Other Buster"), (50f, 203u, "Other Adds")));

        Assert.Equal(2, config.LearnedFights.Count);
        Assert.Equal("First Boss", config.LearnedFights["1"].BossName);
        Assert.Equal("Second Boss", config.LearnedFights["2"].BossName);
        Assert.DoesNotContain(config.LearnedFights["2"].Casts, c => c.Name == "Opener");
    }

    [Fact]
    public void ALearnedBossBuildsASilentTimelineOnlyFight()
    {
        var config = new Configuration();
        TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(FullPull));

        var fight = TimelineLearner.Build(config, 4242, 1000)!;
        Assert.NotNull(fight);
        Assert.True(fight.TimelineOnly);
        Assert.Equal("Test Boss", fight.Name);
        Assert.Equal(1000u, fight.TerritoryId);
        Assert.Equal(5, fight.Lines.Count);
        Assert.All(fight.Lines, l => Assert.False(l.Sound));
        Assert.All(fight.Lines, l => Assert.Equal("", l.Action));
        // Every cast doubles as a resync anchor so the clock self-corrects.
        Assert.Equal(5, fight.SyncPoints.Count);
    }

    [Fact]
    public void AnUnknownBossBuildsNothing()
    {
        var config = new Configuration();
        Assert.Null(TimelineLearner.Build(config, 9999, 1000));
        Assert.Null(TimelineLearner.Build(config, 0, 1000));
    }

    [Fact]
    public void ForgettingABossClearsIt()
    {
        var config = new Configuration();
        TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(FullPull));
        Assert.True(TimelineLearner.Forget(config, 4242));
        Assert.Null(TimelineLearner.Build(config, 4242, 1000));
        Assert.False(TimelineLearner.Forget(config, 4242));
    }

    [Fact]
    public void ALearnedTimelineSurvivesBeingSavedAndReloaded()
    {
        // It lives in the config, so it has to round-trip through Newtonsoft.
        var config = new Configuration();
        TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(FullPull));

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(config);
        var back = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(json)!;

        var fight = TimelineLearner.Build(back, 4242, 1000)!;
        Assert.Equal(5, fight.Lines.Count);
        Assert.Equal("Test Boss", fight.Name);
        Assert.Equal(1, back.LearnedFights["4242"].Pulls);
    }

    [Fact]
    public void AnAbsurdlyLongPullIsCapped()
    {
        // A capture is bounded, so one very long fight can't bloat the config.
        var many = Enumerable.Range(0, 400)
            .Select(i => ((float)(i * 5), (uint)(1000 + i), $"Cast {i}")).ToArray();
        var casts = TimelineLearner.Distill(Pull(many));
        Assert.True(casts.Count <= 220, $"{casts.Count} casts kept");
    }

    // ---- first-pull loop projection ----------------------------------------

    private static List<LearnedCast> Casts(params (float Time, uint Ability)[] c)
        => c.Select(x => new LearnedCast { Time = x.Time, Ability = x.Ability, Name = $"Cast {x.Ability}" }).ToList();

    [Fact]
    public void ABossThatLoopsIsProjectedForward()
    {
        // A B C, A B C on a 60s cycle: the next cycle is predictable.
        var seen = Casts((10, 1), (30, 2), (50, 3), (70, 1), (90, 2), (110, 3));
        var loop = TimelineLearner.FindLoop(seen);

        Assert.NotNull(loop);
        Assert.Equal(3, loop!.Value.Cycle.Count);
        Assert.Equal(60f, loop.Value.Period, 1);

        var next = TimelineLearner.ProjectLoop(seen);
        Assert.Equal(new[] { 130f, 150f, 170f }, next.Take(3).Select(c => c.Time));
        Assert.Equal(new uint[] { 1, 2, 3 }, next.Take(3).Select(c => c.Ability));
    }

    [Fact]
    public void TheLongestCycleWins()
    {
        // Read A B C as a three-cast cycle, not as C repeating.
        var seen = Casts((10, 1), (30, 2), (50, 3), (70, 1), (90, 2), (110, 3));
        Assert.Equal(3, TimelineLearner.FindLoop(seen)!.Value.Cycle.Count);
    }

    [Fact]
    public void ABossThatHasNotRepeatedYetProjectsNothing()
    {
        // The honest answer for the opening of a fight nobody has ever seen.
        Assert.Null(TimelineLearner.FindLoop(Casts((10, 1), (30, 2), (50, 3))));
        Assert.Empty(TimelineLearner.ProjectLoop(Casts((10, 1), (30, 2), (50, 3))));
    }

    [Fact]
    public void AWanderingScheduleIsNotTreatedAsALoop()
    {
        // Same abilities, but the second run is nothing like the first's spacing:
        // predicting off that would put wrong times on the board.
        var seen = Casts((10, 1), (30, 2), (50, 3), (70, 1), (200, 2), (400, 3));
        Assert.Null(TimelineLearner.FindLoop(seen));
    }

    [Fact]
    public void OneMechanicDoubleTappingIsNotALoop()
    {
        var seen = Casts((10, 1), (11, 1), (12, 1), (13, 1));
        Assert.Null(TimelineLearner.FindLoop(seen));
    }

    [Fact]
    public void AFirstPullBoardIsBuiltFromTheLoop()
    {
        var casts = new[]
        {
            new TimelineLearner.PullCast(1, 10f, "Alpha", 500), new TimelineLearner.PullCast(2, 30f, "Beta", 500),
            new TimelineLearner.PullCast(3, 50f, "Gamma", 500), new TimelineLearner.PullCast(1, 70f, "Alpha", 500),
            new TimelineLearner.PullCast(2, 90f, "Beta", 500), new TimelineLearner.PullCast(3, 110f, "Gamma", 500),
        };
        var fight = TimelineLearner.BuildFromLivePull(1000, "Some Boss", 500, casts)!;

        Assert.NotNull(fight);
        Assert.True(fight.TimelineOnly);
        Assert.Equal("Some Boss", fight.Name);
        Assert.NotEmpty(fight.Lines);
        // Everything projected sits in the future, past what has already happened.
        Assert.All(fight.Lines, l => Assert.True(l.Time > 110f));
        Assert.All(fight.Lines, l => Assert.False(l.Sound));
    }

    [Fact]
    public void AFirstPullWithNoLoopBuildsNothing()
    {
        var casts = new[]
        {
            new TimelineLearner.PullCast(1, 10f, "Alpha", 500),
            new TimelineLearner.PullCast(2, 30f, "Beta", 500),
        };
        Assert.Null(TimelineLearner.BuildFromLivePull(1000, "Some Boss", 500, casts));
    }

    // ---- segmenting one captured pull --------------------------------------
    // A dungeon run is ONE capture containing several trash packs and then a
    // boss. Learning it verbatim gets everything wrong: the boss's opener lands
    // minutes in, trash casts pollute the rows, and the whole thing is filed
    // under whichever mob had the most health. Segment() is what makes a single
    // pull produce a correct timeline.

    private const uint Boss = 500;
    private const uint Trash = 900;
    private const uint Add = 901;

    private static TimelineLearner.PullCast P(float time, uint ability, uint caster, string? name = null)
        => new(ability, time, name ?? $"Cast {ability}", caster);

    // Five minutes of trash, then a boss engaged at 300s.
    private static List<TimelineLearner.PullCast> DungeonRun() => new()
    {
        P(12, 800, Trash), P(30, 801, Trash), P(120, 800, Trash), P(200, 802, Trash),
        P(300, 10, Boss), P(320, 11, Boss), P(345, 12, Boss), P(370, 13, Boss), P(400, 10, Boss),
    };

    [Fact]
    public void TheBossEngagementIsRebasedToZero()
    {
        var casts = TimelineLearner.Segment(DungeonRun(), Boss);

        Assert.NotEmpty(casts);
        Assert.Equal(0f, casts[0].Time);              // the boss's first cast IS zero
        Assert.Equal(10u, casts[0].Ability);
        Assert.Equal(100f, casts[^1].Time);           // 400s - 300s
    }

    [Fact]
    public void TrashBeforeTheBossIsNotLearned()
    {
        var casts = TimelineLearner.Segment(DungeonRun(), Boss);
        Assert.DoesNotContain(casts, c => c.Ability is 800 or 801 or 802);
    }

    [Fact]
    public void AddsDuringTheFightAreKept()
    {
        // An add's cast after the boss engages is a mechanic, not noise.
        var run = DungeonRun();
        run.Add(P(360, 700, Add, "Add Cast"));
        var casts = TimelineLearner.Segment(run, Boss);
        Assert.Contains(casts, c => c.Ability == 700);
    }

    [Fact]
    public void ATrashPackNeverBecomesABossTimeline()
    {
        // Short and samey: exactly what a trash pull looks like.
        var trashOnly = new List<TimelineLearner.PullCast>
        {
            P(10, 800, Trash), P(15, 800, Trash), P(22, 801, Trash), P(28, 800, Trash),
        };
        Assert.Empty(TimelineLearner.Segment(trashOnly, Trash));
    }

    [Fact]
    public void ABossThatNeverCastAnythingIsNotLearned()
    {
        Assert.Empty(TimelineLearner.Segment(DungeonRun(), 12345));
        Assert.Empty(TimelineLearner.Segment(DungeonRun(), 0));
    }

    [Fact]
    public void AnInstantWipeOnTheBossIsNotLearned()
    {
        var quick = new List<TimelineLearner.PullCast>
        {
            P(300, 10, Boss), P(305, 11, Boss), P(310, 12, Boss),
        };
        Assert.Empty(TimelineLearner.Segment(quick, Boss));   // under the engagement floor
    }

    [Fact]
    public void OneDungeonRunTeachesTheBossCorrectly()
    {
        // End to end: the capture goes in raw, what comes out starts at zero and
        // contains only the fight.
        var config = new Configuration();
        Assert.True(TimelineLearner.LearnPull(config, Boss, "Dungeon Boss", 1000, DungeonRun()));

        var fight = TimelineLearner.Build(config, Boss, 1000)!;
        Assert.Equal("Dungeon Boss", fight.Name);
        Assert.Equal(0f, fight.Lines.Min(l => l.Time));
        Assert.All(fight.Lines, l => Assert.True(l.Time <= 100f));
        Assert.DoesNotContain(fight.Lines, l => l.Mechanic.Contains("80"));
    }

    [Fact]
    public void ASecondRunKeepsTheSameZeroPoint()
    {
        // The rebase has to be stable, or pull two would fight pull one over where
        // the fight starts and the averaged times would drift toward nonsense.
        var config = new Configuration();
        TimelineLearner.LearnPull(config, Boss, "Dungeon Boss", 1000, DungeonRun());

        // Same fight, but the group cleared trash faster, so it engages at 180s.
        var faster = new List<TimelineLearner.PullCast>
        {
            P(20, 800, Trash), P(90, 801, Trash),
            P(180, 10, Boss), P(200, 11, Boss), P(225, 12, Boss), P(250, 13, Boss), P(280, 10, Boss),
        };
        TimelineLearner.LearnPull(config, Boss, "Dungeon Boss", 1000, faster);

        var learned = config.LearnedFights[Boss.ToString()];
        Assert.Equal(0f, learned.Casts.Min(c => c.Time));
        Assert.Equal(2, learned.Pulls);
        // Two runs of the same fight agree, so nothing should have been appended.
        Assert.Equal(5, learned.Casts.Count);
    }

    [Fact]
    public void TheLivePullProjectionIsAlsoSegmented()
    {
        // Trash looping before the boss must not put a phantom timeline on the
        // board; the boss's own loop must.
        var trashLoop = new List<TimelineLearner.PullCast>
        {
            P(10, 800, Trash), P(30, 801, Trash), P(50, 802, Trash),
            P(70, 800, Trash), P(90, 801, Trash), P(110, 802, Trash),
        };
        Assert.Null(TimelineLearner.BuildFromLivePull(1000, "Boss", Boss, trashLoop));

        var bossLoop = new List<TimelineLearner.PullCast>
        {
            P(10, 800, Trash),
            P(100, 10, Boss), P(120, 11, Boss), P(140, 12, Boss),
            P(160, 10, Boss), P(180, 11, Boss), P(200, 12, Boss),
        };
        var fight = TimelineLearner.BuildFromLivePull(1000, "Boss", Boss, bossLoop)!;
        Assert.NotNull(fight);
        Assert.All(fight.Lines, l => Assert.DoesNotContain("800", l.Mechanic));
    }

    [Fact]
    public void TheAveragingStaysAdaptiveAfterManyPulls()
    {
        // With an uncapped pull count a boss retuned in a patch would never be
        // re-learned: the hundredth pull would move the stored time by 1%.
        var config = new Configuration();
        for (var i = 0; i < 60; i++)
            TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(FullPull));

        var before = config.LearnedFights["4242"].Casts.First(c => c.Ability == 100).Time;
        // The fight gets retuned: the opener now lands 6s later, every pull.
        for (var i = 0; i < 12; i++)
            TimelineLearner.Learn(config, 4242, "Test Boss", 1000, Pull(
                (16f, 100u, "Opener"), (25f, 101u, "Raidwide"), (44f, 102u, "Tank Buster"),
                (61f, 103u, "Adds"), (95f, 104u, "Enrage")));

        var after = config.LearnedFights["4242"].Casts.First(c => c.Ability == 100).Time;
        Assert.True(after > before + 3f,
            $"stuck at {after}s after a dozen pulls of the retuned fight (was {before}s)");
    }

    [Fact]
    public void TheStoreIsBounded()
    {
        // Every boss in every duty with no baked timeline lands here, and the whole
        // config is rewritten on each save.
        var config = new Configuration();
        for (uint boss = 1; boss <= 460; boss++)
            TimelineLearner.Learn(config, boss, $"Boss {boss}", 1000, Pull(FullPull));

        Assert.True(config.LearnedFights.Count <= 400,
            $"{config.LearnedFights.Count} learned fights kept");
        // What survived should be what was seen most recently.
        Assert.True(config.LearnedFights.ContainsKey("460"));
    }
}
