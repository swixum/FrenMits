using Xunit;

namespace FrenMits.Tests;

// The pre-pull food check. Everything that decides whether to warn is pure, so
// the whole matrix is pinned here rather than discovered in a duty.
public class PrepCheckTests
{
    private static PrepCheck.Buff Up(float remaining) => new(true, remaining);
    private static readonly PrepCheck.Buff None = new(false, 0f);

    [Fact]
    public void NoFoodAtAllIsMissing()
        => Assert.Equal(PrepCheck.Grade.Missing, PrepCheck.GradeOf(None, 240f));

    [Fact]
    public void PlentyOfTimeSaysNothing()
        => Assert.Equal(PrepCheck.Grade.Ok, PrepCheck.GradeOf(Up(1800f), 240f));

    [Theory]
    [InlineData(239f)]   // just inside
    [InlineData(240f)]   // exactly on the threshold: warn, don't split hairs
    [InlineData(1f)]
    public void UnderTheThresholdIsExpiring(float remaining)
        => Assert.Equal(PrepCheck.Grade.Expiring, PrepCheck.GradeOf(Up(remaining), 240f));

    [Fact]
    public void JustOverTheThresholdIsStillFine()
        => Assert.Equal(PrepCheck.Grade.Ok, PrepCheck.GradeOf(Up(241f), 240f));

    [Fact]
    public void APresentBuffWithNoReadableTimerIsNotAWarning()
    {
        // A status that's up but reports no remaining time is one we can't time,
        // not one about to drop. Warning on it would nag for the whole duty.
        Assert.Equal(PrepCheck.Grade.Ok, PrepCheck.GradeOf(Up(0f), 240f));
        Assert.Equal(PrepCheck.Grade.Ok, PrepCheck.GradeOf(Up(-5f), 240f));
    }

    [Theory]
    [InlineData(4f, 240f)]
    [InlineData(1f, 60f)]
    [InlineData(30f, 1800f)]
    [InlineData(0f, 60f)]      // clamped up: a zero threshold would silence the check
    [InlineData(-3f, 60f)]
    [InlineData(9999f, 3600f)] // clamped down: never a permanent warning
    public void TheThresholdIsClampedToSomethingSane(float minutes, float expected)
        => Assert.Equal(expected, PrepCheck.WarnSeconds(minutes));

    [Theory]
    [InlineData(240f, "4:00")]
    [InlineData(221f, "3:41")]
    [InlineData(59.2f, "1:00")]  // ceiling, so it never reads 0:59 with a second left
    [InlineData(0f, "0:00")]
    [InlineData(-10f, "0:00")]   // never negative
    [InlineData(3600f, "60:00")]
    public void TheClockReadsAsMinutesAndSeconds(float seconds, string expected)
        => Assert.Equal(expected, PrepCheck.Clock(seconds));

    [Fact]
    public void TheWarningTextNamesTheProblem()
    {
        Assert.Equal("No food", PrepCheck.FoodLine(None, 240f));
        Assert.Equal("Food 3:41", PrepCheck.FoodLine(Up(221f), 240f));
        // Healthy food produces no line at all, which is what keeps the overlay
        // silent instead of permanently on screen.
        Assert.Equal("", PrepCheck.FoodLine(Up(1800f), 240f));
    }

    // ---- the potion timer --------------------------------------------------
    // It is a mid-fight recast reminder, not a pre-pull one: it must say nothing
    // at all until it has seen a pot actually used.

    private const double Cd = PrepCheck.PotionTimer.CooldownSeconds;

    [Fact]
    public void ItSaysNothingUntilAPotHasBeenUsed()
    {
        // The whole point: standing in front of a boss with a pot ready is not
        // news. Sit there for ten minutes without popping one and it stays quiet.
        var t = new PrepCheck.PotionTimer();
        for (var now = 0.0; now < 600.0; now += 1.0)
            Assert.False(t.Update(false, now), $"fired at {now}s without a pot ever being used");
    }

    [Fact]
    public void UsingAPotStartsTheRecastAndItFiresWhenItIsBack()
    {
        var t = new PrepCheck.PotionTimer();
        Assert.False(t.Update(true, 100.0));            // popped it
        Assert.False(t.Update(false, 130.0));           // Medicated wore off: still silent
        Assert.False(t.Update(false, 100.0 + Cd - 1));  // one second short
        Assert.True(t.Update(false, 100.0 + Cd));       // back up: say so
    }

    [Fact]
    public void TheNoteLeavesOnItsOwn()
    {
        var t = new PrepCheck.PotionTimer();
        t.Update(true, 100.0);
        Assert.True(t.Update(false, 100.0 + Cd));
        Assert.True(t.Update(false, 100.0 + Cd + 4.9));
        Assert.False(t.Update(false, 100.0 + Cd + 5.0));   // 5s elapsed
        Assert.False(t.Update(false, 100.0 + Cd + 400));   // and stays gone
    }

    [Fact]
    public void ASecondPotIsTimedToo()
    {
        var t = new PrepCheck.PotionTimer();
        t.Update(true, 100.0);
        Assert.True(t.Update(false, 100.0 + Cd));          // first one back
        t.Update(false, 100.0 + Cd + 10);                  // note gone

        var second = 100.0 + Cd + 20;
        Assert.False(t.Update(true, second));              // popped the second
        Assert.False(t.Update(false, second + Cd - 1));
        Assert.True(t.Update(false, second + Cd));         // and it's timed as well
    }

    [Fact]
    public void AMissedFrameDoesNotLoseTheAnnouncement()
    {
        // The clock is compared against wall time rather than counted down, so a
        // hitch (or a cutscene) can't skip the moment it comes back.
        var t = new PrepCheck.PotionTimer();
        t.Update(true, 100.0);
        Assert.True(t.Update(false, 100.0 + Cd + 3));   // first look is already late
    }

    [Fact]
    public void LeavingTheDutyForgetsThePot()
    {
        var t = new PrepCheck.PotionTimer();
        t.Update(true, 100.0);
        t.Reset();
        // The pending use is gone, so nothing fires on the old schedule.
        for (var now = 100.0; now < 100.0 + Cd + 60; now += 5.0)
            Assert.False(t.Update(false, now));
    }

    [Fact]
    public void TheRecastAndShowTimeAreTheExpectedNumbers()
    {
        Assert.Equal(270f, PrepCheck.PotionTimer.CooldownSeconds);  // 4m30s
        Assert.Equal(5f, PrepCheck.PotionTimer.ShowSeconds);
    }

    // ---- speech ------------------------------------------------------------
    // The food line sits on screen for as long as the problem lasts, so the only
    // thing standing between it and being spoken every frame is the announcer.

    [Fact]
    public void SpokenPhrasesCarryNoCountdown()
    {
        // A phrase containing "3:41" would differ every second, and the announcer
        // would dutifully speak every one of them.
        Assert.Equal("No food", PrepCheck.SpeechFor(PrepCheck.Grade.Missing));
        Assert.Equal("Food is running out", PrepCheck.SpeechFor(PrepCheck.Grade.Expiring));
        Assert.Equal("", PrepCheck.SpeechFor(PrepCheck.Grade.Ok));
        foreach (var g in new[] { PrepCheck.Grade.Missing, PrepCheck.Grade.Expiring })
            Assert.DoesNotContain(":", PrepCheck.SpeechFor(g));
    }

    [Fact]
    public void EachPhraseIsSpokenOnceNotEveryFrame()
    {
        var a = new PrepCheck.Announcer();
        Assert.Equal("No food", a.Next("No food"));
        for (var i = 0; i < 300; i++) Assert.Null(a.Next("No food"));
    }

    [Fact]
    public void AChangeOfStateIsSpoken()
    {
        var a = new PrepCheck.Announcer();
        Assert.Equal("No food", a.Next("No food"));
        Assert.Equal("Food is running out", a.Next("Food is running out"));
        Assert.Null(a.Next("Food is running out"));
    }

    [Fact]
    public void SilenceIsNeverSpokenButDoesReArm()
    {
        var a = new PrepCheck.Announcer();
        Assert.Equal("No food", a.Next("No food"));
        Assert.Null(a.Next(""));             // problem fixed: nothing said
        Assert.Equal("No food", a.Next("No food"));  // came back: said again
    }

    [Fact]
    public void ResetLetsTheNextPullSpeakAgain()
    {
        var a = new PrepCheck.Announcer();
        Assert.Equal("No food", a.Next("No food"));
        Assert.Null(a.Next("No food"));
        a.Reset();
        Assert.Equal("No food", a.Next("No food"));
    }

    [Theory]
    [InlineData(true, true, false, true)]    // in a duty, out of combat: the one moment it helps
    [InlineData(true, true, true, false)]    // in combat: nothing you can do about it
    [InlineData(true, false, false, false)]  // open world: nagging
    [InlineData(false, true, false, false)]  // switched off
    public void ItOnlyShowsWhereItIsActionable(bool enabled, bool inDuty, bool inCombat, bool expected)
        => Assert.Equal(expected, PrepCheck.ShouldShow(enabled, inDuty, inCombat));

    [Fact]
    public void TheStatusIdsAreTheOnesTheGameUses()
    {
        // Verified against the Status sheet; both rows are unique. If a patch ever
        // moved them the check would silently report "no food" forever.
        Assert.Equal(48u, PrepCheck.WellFedStatus);
        Assert.Equal(49u, PrepCheck.MedicatedStatus);
    }
}
