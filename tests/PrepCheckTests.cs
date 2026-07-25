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

    // ---- the optional extras ------------------------------------------------
    // Every one of these is off by default, and the contract is that with all of
    // them off the check behaves EXACTLY as it did before they existed.

    private static PrepCheck.FoodOpts Off(float warn = 240f) => new(warn, false, false, false);

    [Theory]
    [InlineData(1800f)]  // fine
    [InlineData(221f)]   // expiring
    public void WithEveryExtraOffTheVerdictMatchesTheOriginalLine(float remaining)
    {
        var food = Up(remaining);
        Assert.Equal(PrepCheck.FoodLine(food, 240f), PrepCheck.FoodVerdict(food, true, true, Off()).Text);
    }

    [Fact]
    public void WithEveryExtraOffMissingFoodStillReadsTheSame()
        => Assert.Equal(PrepCheck.FoodLine(None, 240f), PrepCheck.FoodVerdict(None, true, true, Off()).Text);

    [Fact]
    public void CrafterFoodIsOnlyFlaggedWhenAskedFor()
    {
        var food = Up(1800f);
        Assert.False(PrepCheck.FoodVerdict(food, isBattleFood: false, isHq: true, Off()).Any);

        var on = new PrepCheck.FoodOpts(240f, WarnWrongFood: true, WarnNq: false, AlwaysShow: false);
        var v = PrepCheck.FoodVerdict(food, isBattleFood: false, isHq: true, on);
        Assert.Equal("Crafter food", v.Text);
        Assert.Equal(PrepCheck.Level.Danger, v.Level);
    }

    [Fact]
    public void CrafterFoodOutranksTheTimer()
    {
        // A dish doing nothing for you is worse news than one about to run out.
        var on = new PrepCheck.FoodOpts(240f, WarnWrongFood: true, WarnNq: true, AlwaysShow: false);
        Assert.Equal("Crafter food", PrepCheck.FoodVerdict(Up(30f), false, false, on).Text);
    }

    [Fact]
    public void NqIsOnlyFlaggedWhenAskedFor()
    {
        var food = Up(1800f);
        Assert.False(PrepCheck.FoodVerdict(food, true, isHq: false, Off()).Any);

        var on = new PrepCheck.FoodOpts(240f, false, WarnNq: true, AlwaysShow: false);
        Assert.Equal("Food is NQ", PrepCheck.FoodVerdict(food, true, isHq: false, on).Text);
        // HQ food says nothing.
        Assert.False(PrepCheck.FoodVerdict(food, true, isHq: true, on).Any);
    }

    [Fact]
    public void ARunningOutTimerOutranksTheNqNote()
    {
        // Both are true at once constantly; the one with a deadline wins.
        var on = new PrepCheck.FoodOpts(240f, false, WarnNq: true, AlwaysShow: false);
        Assert.Equal("Food 3:41", PrepCheck.FoodVerdict(Up(221f), true, false, on).Text);
    }

    [Fact]
    public void TheAlwaysOnTimerIsAReadoutNotAWarning()
    {
        var on = new PrepCheck.FoodOpts(240f, false, false, AlwaysShow: true);
        var v = PrepCheck.FoodVerdict(Up(1471f), true, true, on);
        Assert.Equal("Food 24:31", v.Text);
        Assert.Equal(PrepCheck.Level.Info, v.Level);   // muted, not amber
    }

    [Fact]
    public void TheAlwaysOnTimerNeverInventsATimeItDoesNotHave()
    {
        // A present buff with an unreadable timer would otherwise read "Food 0:00".
        var on = new PrepCheck.FoodOpts(240f, false, false, AlwaysShow: true);
        Assert.False(PrepCheck.FoodVerdict(Up(0f), true, true, on).Any);
    }

    [Fact]
    public void UnknownFoodIsNeverAccused()
    {
        // A failed sheet lookup reports "battle food" and "HQ", so a lookup that
        // breaks on a patch day goes quiet rather than calling everyone's dinner
        // crafter food.
        var on = new PrepCheck.FoodOpts(240f, WarnWrongFood: true, WarnNq: true, AlwaysShow: false);
        Assert.False(PrepCheck.FoodVerdict(Up(1800f), isBattleFood: true, isHq: true, on).Any);
    }

    [Theory]
    [InlineData(false, 4f, 700f, 240f)]   // off: the slider wins
    [InlineData(true, 4f, 700f, 700f)]    // on: the fight's length wins
    [InlineData(true, 4f, 0f, 240f)]      // on, but no sheet: back to the slider
    public void TheThresholdCanComeFromTheFight(bool useFight, float minutes, float fightSeconds, float expected)
        => Assert.Equal(expected, PrepCheck.WarnSecondsFor(useFight, minutes, fightSeconds));

    [Fact]
    public void AFightsLengthIsItsLastMechanic()
    {
        var f = new FightProfile { TerritoryId = 1 };
        f.Lines.Add(Fx.Line(120, "A", "Reprisal"));
        f.Lines.Add(Fx.Line(640, "B", "Rampart"));
        f.CustomRows.Add(new CustomRow { Time = 702, Mechanic = "Enrage" });
        Assert.Equal(702f, PrepCheck.FightSeconds(f));
    }

    [Fact]
    public void ABakedDutyTimelineHasNoMeaningfulLength()
    {
        // Those pack several encounters onto one clock in 1000-second blocks, so
        // the last time is boss three's coordinate, not a fight length.
        var f = new FightProfile { TerritoryId = 1, TimelineOnly = true };
        f.Lines.Add(Fx.Line(3400, "Boss 3 thing", ""));
        Assert.Equal(0f, PrepCheck.FightSeconds(f));
        Assert.Equal(0f, PrepCheck.FightSeconds(null));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(-1, "")]
    [InlineData(1, "  (1 left)")]
    [InlineData(12, "  (12 left)")]
    public void BagCountsOnlyShowWhenThereIsOne(int n, string expected)
        => Assert.Equal(expected, PrepCheck.Count(n));

    [Fact]
    public void ThePotCountdownReadsDownToZero()
    {
        var t = new PrepCheck.PotionTimer();
        Assert.Equal(0f, t.Remaining(50.0));            // nothing timed yet
        t.Update(true, 300f, 100.0);
        Assert.Equal(300f, t.Remaining(100.0));
        Assert.Equal(120f, t.Remaining(280.0));
        Assert.Equal(0f, t.Remaining(400.0));           // never negative
    }

    // ---- the potion timer --------------------------------------------------
    // It is a mid-fight recast reminder, not a pre-pull one: it must say nothing
    // at all until it has seen a pot actually used.

    private const double Cd = PrepCheck.PotionTimer.DefaultCooldownSeconds;

    [Fact]
    public void ItSaysNothingUntilAPotHasBeenUsed()
    {
        // The whole point: standing in front of a boss with a pot ready is not
        // news. Sit there for ten minutes without popping one and it stays quiet.
        var t = new PrepCheck.PotionTimer();
        for (var now = 0.0; now < 600.0; now += 1.0)
            Assert.False(t.Update(false, (float)Cd, now), $"fired at {now}s without a pot ever being used");
    }

    [Fact]
    public void UsingAPotStartsTheRecastAndItFiresWhenItIsBack()
    {
        var t = new PrepCheck.PotionTimer();
        Assert.False(t.Update(true, (float)Cd, 100.0));            // popped it
        Assert.False(t.Update(false, (float)Cd, 130.0));           // Medicated wore off: still silent
        Assert.False(t.Update(false, (float)Cd, 100.0 + Cd - 1));  // one second short
        Assert.True(t.Update(false, (float)Cd, 100.0 + Cd));       // back up: say so
    }

    [Fact]
    public void TheNoteLeavesOnItsOwn()
    {
        var t = new PrepCheck.PotionTimer();
        t.Update(true, (float)Cd, 100.0);
        Assert.True(t.Update(false, (float)Cd, 100.0 + Cd));
        Assert.True(t.Update(false, (float)Cd, 100.0 + Cd + 4.9));
        Assert.False(t.Update(false, (float)Cd, 100.0 + Cd + 5.0));   // 5s elapsed
        Assert.False(t.Update(false, (float)Cd, 100.0 + Cd + 400));   // and stays gone
    }

    [Fact]
    public void ASecondPotIsTimedToo()
    {
        var t = new PrepCheck.PotionTimer();
        t.Update(true, (float)Cd, 100.0);
        Assert.True(t.Update(false, (float)Cd, 100.0 + Cd));          // first one back
        t.Update(false, (float)Cd, 100.0 + Cd + 10);                  // note gone

        var second = 100.0 + Cd + 20;
        Assert.False(t.Update(true, (float)Cd, second));              // popped the second
        Assert.False(t.Update(false, (float)Cd, second + Cd - 1));
        Assert.True(t.Update(false, (float)Cd, second + Cd));         // and it's timed as well
    }

    [Fact]
    public void AMissedFrameDoesNotLoseTheAnnouncement()
    {
        // The clock is compared against wall time rather than counted down, so a
        // hitch (or a cutscene) can't skip the moment it comes back.
        var t = new PrepCheck.PotionTimer();
        t.Update(true, (float)Cd, 100.0);
        Assert.True(t.Update(false, (float)Cd, 100.0 + Cd + 3));   // first look is already late
    }

    [Fact]
    public void LeavingTheDutyForgetsThePot()
    {
        var t = new PrepCheck.PotionTimer();
        t.Update(true, (float)Cd, 100.0);
        t.Reset();
        // The pending use is gone, so nothing fires on the old schedule.
        for (var now = 100.0; now < 100.0 + Cd + 60; now += 5.0)
            Assert.False(t.Update(false, (float)Cd, now));
    }

    [Fact]
    public void AnUnreadableRecastFallsBackToTheStandardFiveMinutes()
    {
        // If the tincture's own row can't be resolved we get 0, and timing the
        // pot off zero would fire the note instantly.
        var t = new PrepCheck.PotionTimer();
        t.Update(true, 0f, 100.0);
        Assert.False(t.Update(false, 0f, 100.0 + 1));
        Assert.False(t.Update(false, 0f, 100.0 + Cd - 1));
        Assert.True(t.Update(false, 0f, 100.0 + Cd));
    }

    [Fact]
    public void APotWithItsOwnRecastIsTimedOnThatNumber()
    {
        // Not every medicine is 300s, so the item's own value wins when we have it.
        var t = new PrepCheck.PotionTimer();
        t.Update(true, 90f, 100.0);
        Assert.False(t.Update(false, 90f, 189.0));
        Assert.True(t.Update(false, 90f, 190.0));
    }

    [Fact]
    public void TheDefaultRecastAndShowTimeAreTheExpectedNumbers()
    {
        // Straight from the Item sheet: every tincture and gemdraught in the game
        // carries Cooldowns = 300, across all grades and expansions.
        Assert.Equal(300f, PrepCheck.PotionTimer.DefaultCooldownSeconds);
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
