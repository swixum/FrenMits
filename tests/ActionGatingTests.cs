using Xunit;

namespace FrenMits.Tests;

// Which parts of a combined call belong to YOUR job. Every overlay, the board,
// the tuner and the cue engine run this over every line, every frame.
public class ActionGatingTests
{
    [Fact]
    public void AnUngatedCallReadsTheSameForEveryone()
    {
        var line = Fx.Line(10, "Raidwide", "Reprisal");
        Assert.Equal("Reprisal", line.ActionFor("WAR"));
        Assert.Equal("Reprisal", line.ActionFor("SCH"));
        Assert.True(line.AppliesTo("WAR"));
        Assert.True(line.AppliesTo("SCH"));
    }

    [Fact]
    public void AGatedSegmentIsDroppedForEveryoneElse()
    {
        var line = Fx.Line(10, "Raidwide", "Reprisal + Party Mit (GNB/DRK)");
        Assert.Equal("Reprisal", line.ActionFor("WAR"));
        Assert.Equal("Reprisal + Party Mit (GNB/DRK)", line.ActionFor("DRK"));
        Assert.Equal("Reprisal + Party Mit (GNB/DRK)", line.ActionFor("GNB"));
    }

    [Fact]
    public void ACallThatIsEntirelySomeoneElsesDoesNotApply()
    {
        var line = Fx.Line(10, "Raidwide", "Party Mit (GNB/DRK)");
        Assert.Equal("", line.ActionFor("WAR"));
        Assert.False(line.AppliesTo("WAR"));
        Assert.True(line.AppliesTo("DRK"));
    }

    [Fact]
    public void GatesAreCaseInsensitive()
    {
        var line = Fx.Line(10, "Raidwide", "Party Mit (gnb/drk)");
        Assert.True(line.AppliesTo("DRK"));
        Assert.False(line.AppliesTo("WAR"));
    }

    [Fact]
    public void ParenthesesThatArentJobsAreLeftAlone()
    {
        // "(3x)" and "(If Available)" are notes, not gates - dropping those
        // segments would silently delete real calls from everyone's plan.
        foreach (var action in new[] { "Rampart (3x)", "Party Mit (If Available)", "Holmgang (Take Solo)" })
        {
            var line = Fx.Line(10, "Hyperdrive", action);
            Assert.Equal(action, line.ActionFor("WAR"));
            Assert.True(line.AppliesTo("WAR"));
            Assert.False(line.HasJobGate());
        }
    }

    [Fact]
    public void MixedGateAndNoteResolvesPerSegment()
    {
        var line = Fx.Line(10, "Thunder III", "Rampart (3x) + Short Mit (WAR/PLD)");
        Assert.Equal("Rampart (3x)", line.ActionFor("DRK"));
        Assert.Equal("Rampart (3x) + Short Mit (WAR/PLD)", line.ActionFor("PLD"));
    }

    [Fact]
    public void TheJobListGatesTheWholeLine()
    {
        var line = Fx.Line(10, "Towers", "Nature's Minne", "BRD");
        Assert.True(line.AppliesTo("BRD"));
        Assert.True(line.AppliesTo("brd"));
        Assert.False(line.AppliesTo("MCH"));
    }

    [Fact]
    public void NoJobMeansShowEverything()
    {
        // With the job unknown (not logged in, or a job the table doesn't have),
        // nothing is filtered out rather than silently hiding the plan.
        var line = Fx.Line(10, "Raidwide", "Party Mit (GNB/DRK)");
        Assert.True(line.AppliesTo(null));
        Assert.Equal("Party Mit (GNB/DRK)", line.ActionFor(null));
    }

    [Fact]
    public void HasJobGateSpotsOnlyRealGates()
    {
        Assert.True(Fx.Line(0, "", "Party Mit (WAR/PLD)").HasJobGate());
        Assert.True(Fx.Line(0, "", "Reprisal + Short Mit (DRK)").HasJobGate());
        Assert.False(Fx.Line(0, "", "Reprisal").HasJobGate());
        Assert.False(Fx.Line(0, "", "Rampart (3x)").HasJobGate());
        Assert.False(Fx.Line(0, "", "").HasJobGate());
    }

    [Fact]
    public void JobTagForNormalizesTheGateItFinds()
    {
        Assert.Equal("DRK/GNB", MitLine.JobTagFor("Reprisal + Party Mit (GNB/DRK)", "Party Mit"));
        Assert.Equal("", MitLine.JobTagFor("Reprisal + Party Mit (GNB/DRK)", "Reprisal"));
        Assert.Equal("", MitLine.JobTagFor("Rampart (3x)", "Rampart"));
    }

    [Fact]
    public void CueTimeHonorsThePerLineOffset()
    {
        var line = new MitLine { Time = 100f, OffsetSeconds = 2.5f };
        Assert.Equal(97.5f, line.CueTime);
    }

    [Theory]
    [InlineData(0f, "0:00")]
    [InlineData(65f, "1:05")]
    [InlineData(600f, "10:00")]
    [InlineData(-5f, "-0:05")]
    public void TimeTextReadsAsAClock(float time, string expected)
        => Assert.Equal(expected, new MitLine { Time = time }.TimeText);
}
