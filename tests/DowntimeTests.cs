using Xunit;

namespace FrenMits.Tests;

// The untargetable/targetable windows the board draws. Effective() is cached
// per territory, so the refinement path has to notice when a pull learns a
// better time - otherwise the board would keep drawing the old lull forever.
//
// NOTE: that cache is static. xUnit runs one class at a time within itself but
// classes in parallel, so anything else that calls Effective() belongs in THIS
// class, not a new one.
public class DowntimeTests
{
    private const ushort Fru = Builtin.FruTerritory;
    private const ushort Dmu = Builtin.DmuTerritory;

    [Fact]
    public void WithNothingLearnedTheHardcodedWindowsComeBack()
    {
        var windows = Downtimes.Effective(Dmu, null);
        Assert.Equal(Downtimes.For(Dmu).Count, windows.Count);
        Assert.Equal(Downtimes.For(Dmu)[0].Start, windows[0].Start);
    }

    [Fact]
    public void AnUnknownDutyHasNoWindows()
        => Assert.Empty(Downtimes.Effective(999999, null));

    [Fact]
    public void ALearnedMeasurementRefinesTheWindowItMatches()
    {
        var target = Downtimes.For(Fru).First(w => w.Learn);
        var learned = new Dictionary<string, List<DowntimeWindow>>
        {
            [Fru.ToString()] = new() { new DowntimeWindow { Start = target.Start + 3f, Duration = 41f } },
        };

        var refined = Downtimes.Effective(Fru, learned)
            .First(w => MathF.Abs(w.Start - (target.Start + 3f)) < 0.01f);

        Assert.Equal(41f, refined.Duration);
        // The refinement must not lose the window's own facts.
        Assert.Equal(target.TargetHp, refined.TargetHp);
        Assert.Equal(target.Cutscene, refined.Cutscene);
    }

    [Fact]
    public void RefiningAgainWithNewNumbersIsPickedUp()
    {
        // Straight at the cache: two pulls in a row that measure the same lull
        // differently must both land.
        var target = Downtimes.For(Fru).First(w => w.Learn);
        var learned = new Dictionary<string, List<DowntimeWindow>>
        {
            [Fru.ToString()] = new() { new DowntimeWindow { Start = target.Start, Duration = 30f } },
        };
        Assert.Contains(Downtimes.Effective(Fru, learned), w => MathF.Abs(w.Duration - 30f) < 0.01f);

        learned[Fru.ToString()][0].Duration = 44f;
        Assert.Contains(Downtimes.Effective(Fru, learned), w => MathF.Abs(w.Duration - 44f) < 0.01f);
        Assert.DoesNotContain(Downtimes.Effective(Fru, learned), w => MathF.Abs(w.Duration - 30f) < 0.01f);
    }

    [Fact]
    public void AddingAMeasurementIsPickedUp()
    {
        var learnable = Downtimes.For(Fru).Where(w => w.Learn).Take(2).ToList();
        var learned = new Dictionary<string, List<DowntimeWindow>>
        {
            [Fru.ToString()] = new() { new DowntimeWindow { Start = learnable[0].Start, Duration = 33f } },
        };
        Assert.Contains(Downtimes.Effective(Fru, learned), w => MathF.Abs(w.Duration - 33f) < 0.01f);

        learned[Fru.ToString()].Add(new DowntimeWindow { Start = learnable[1].Start, Duration = 34f });

        var after = Downtimes.Effective(Fru, learned);
        Assert.Contains(after, w => MathF.Abs(w.Duration - 33f) < 0.01f);
        Assert.Contains(after, w => MathF.Abs(w.Duration - 34f) < 0.01f);
    }

    [Fact]
    public void DroppingEveryMeasurementReturnsTheHardcodedTimes()
    {
        var target = Downtimes.For(Fru).First(w => w.Learn);
        var learned = new Dictionary<string, List<DowntimeWindow>>
        {
            [Fru.ToString()] = new() { new DowntimeWindow { Start = target.Start, Duration = 99f } },
        };
        Assert.Contains(Downtimes.Effective(Fru, learned), w => MathF.Abs(w.Duration - 99f) < 0.01f);

        learned[Fru.ToString()].Clear();

        Assert.DoesNotContain(Downtimes.Effective(Fru, learned), w => MathF.Abs(w.Duration - 99f) < 0.01f);
    }

    [Fact]
    public void AMeasurementNowhereNearAWindowIsIgnored()
    {
        var learned = new Dictionary<string, List<DowntimeWindow>>
        {
            [Fru.ToString()] = new() { new DowntimeWindow { Start = 5000f, Duration = 12f } },
        };
        Assert.DoesNotContain(Downtimes.Effective(Fru, learned), w => MathF.Abs(w.Start - 5000f) < 0.01f);
    }

    [Fact]
    public void OneDutysMeasurementsNeverLeakIntoAnother()
    {
        var learned = new Dictionary<string, List<DowntimeWindow>>
        {
            [Fru.ToString()] = new() { new DowntimeWindow { Start = Downtimes.For(Fru)[0].Start, Duration = 77f } },
        };
        Assert.DoesNotContain(Downtimes.Effective(Dmu, learned), w => MathF.Abs(w.Duration - 77f) < 0.01f);
    }

    [Fact]
    public void FixedWindowsAreNeverOverwrittenByAPull()
    {
        // Dancing Mad's windows are log-verified medians, not guesses: Learn is
        // off, so a measured pull must not move them.
        var fixedWindow = Downtimes.For(Dmu).First();
        Assert.False(fixedWindow.Learn);
        var learned = new Dictionary<string, List<DowntimeWindow>>
        {
            [Dmu.ToString()] = new() { new DowntimeWindow { Start = fixedWindow.Start, Duration = 999f } },
        };

        var after = Downtimes.Effective(Dmu, learned).First();

        Assert.Equal(fixedWindow.Duration, after.Duration);
    }
}
