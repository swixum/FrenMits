using Xunit;

namespace FrenMits.Tests;

// The board's row list: every mechanic the sheet knows about across ALL columns,
// with rows merged so one mechanic doesn't draw twice.
public class SheetTimelineTests
{
    private static FightProfile CustomFight()
    {
        var f = new FightProfile { TerritoryId = 9999, Name = "Custom" };
        f.CustomSlots.AddRange(new[] { "T1", "T2" });
        f.Slot = "T1";
        f.Lines = new List<MitLine> { Fx.Line(100, "Raidwide", "Reprisal") };
        f.SavedSlots["T1"] = f.Lines;
        f.SavedSlots["T2"] = new List<MitLine> { Fx.Line(100, "Raidwide", "Rampart") };
        return f;
    }

    [Fact]
    public void ColumnsMergeOntoOneRowPerMechanic()
    {
        var rows = SheetTimeline.Build(CustomFight());
        Assert.Single(rows);
        Assert.Equal("Raidwide", rows[0].Mechanic);
    }

    [Fact]
    public void RowsComeBackInTimeOrder()
    {
        var f = CustomFight();
        f.Lines.Add(Fx.Line(20, "Early", "Feint"));
        f.Lines.Add(Fx.Line(300, "Late", "Addle"));

        var rows = SheetTimeline.Build(f);

        for (var i = 1; i < rows.Count; i++)
            Assert.True(rows[i].Time >= rows[i - 1].Time);
    }

    [Fact]
    public void NearbyCopiesOfOneMechanicShareARow()
    {
        // Columns rarely agree to the tenth of a second; within the merge window
        // they are the same hit.
        var f = CustomFight();
        f.SavedSlots["T2"] = new List<MitLine> { Fx.Line(100.9f, "Raidwide", "Rampart") };
        Assert.Single(SheetTimeline.Build(f));
    }

    [Fact]
    public void TheSameMechanicFarApartStaysTwoRows()
    {
        var f = CustomFight();
        f.Lines.Add(Fx.Line(400, "Raidwide", "Feint"));
        Assert.Equal(2, SheetTimeline.Build(f).Count);
    }

    [Fact]
    public void ScaffoldRowsAppearBeforeAnyoneHasPlannedThem()
    {
        // A mechanic exists on a custom sheet as soon as it's added, mits or not.
        var f = new FightProfile { TerritoryId = 9999 };
        f.CustomSlots.Add("T1");
        f.CustomRows.Add(new CustomRow { Time = 50, Mechanic = "Unplanned", Hurt = 3, Buster = true });

        var rows = SheetTimeline.Build(f);

        var row = Assert.Single(rows);
        Assert.Equal("Unplanned", row.Mechanic);
        Assert.Equal(3, row.Hurt);
        Assert.True(row.Buster);
    }

    [Fact]
    public void ScaffoldGradesLandOnTheRowThePlanShares()
    {
        var f = CustomFight();
        f.CustomRows.Add(new CustomRow { Time = 100, Mechanic = "Raidwide", Hurt = 2, Buster = true });

        var row = Assert.Single(SheetTimeline.Build(f));

        Assert.Equal(2, row.Hurt);
        Assert.True(row.Buster);
    }

    [Fact]
    public void ABareTimerGetsANameFromItsOwnAction()
    {
        // A user-added row with no mechanic label would otherwise draw a nameless
        // bar on the board.
        var f = new FightProfile { TerritoryId = 9999 };
        f.Lines.Add(Fx.Line(50, "", "Potion"));

        var row = Assert.Single(SheetTimeline.Build(f));

        Assert.Equal("", row.Mechanic);
        Assert.Equal("Potion", row.Fallback);
    }

    [Fact]
    public void EveryBuiltinBuildsABoardWithoutColliding()
    {
        foreach (var (territory, _, _) in Builtin.Fights)
        {
            var fight = Fx.Builtin(territory, "T1");
            var rows = SheetTimeline.Build(fight);
            Assert.NotEmpty(rows);
            for (var i = 1; i < rows.Count; i++)
                Assert.True(rows[i].Time >= rows[i - 1].Time, $"territory {territory}: board out of order");
        }
    }

    [Fact]
    public void MechEqualsIgnoresCaseAndPadding()
    {
        Assert.True(SheetTimeline.MechEquals("Raidwide", " raidwide "));
        Assert.False(SheetTimeline.MechEquals("Raidwide", "Buster"));
    }

    // ---- phase dividers ----------------------------------------------------

    private static List<BossAnchor> Phases() => new()
    {
        new BossAnchor { Time = 0f, Label = "P1 Fatebreaker" },
        new BossAnchor { Time = 215.3f, Label = "P2 Shiva" },
        new BossAnchor { Time = 500f, Label = "P3 Gaia" },
    };

    [Fact]
    public void APhaseStartingInTheGapIsNamed()
        => Assert.Equal("P2 Shiva", SheetTimeline.PhaseBetween(Phases(), 210f, 220f));

    [Fact]
    public void NothingDrawsWhenNoPhaseStartsInTheGap()
        => Assert.Equal("", SheetTimeline.PhaseBetween(Phases(), 220f, 300f));

    [Fact]
    public void AnAnchorOnARowBelongsToThatRow()
    {
        // The row AT 215.3 is P2's first hit, not P1's last, so the divider sits
        // above it - and must not then repeat above the row after.
        Assert.Equal("P2 Shiva", SheetTimeline.PhaseBetween(Phases(), 210f, 215.3f));
        Assert.Equal("", SheetTimeline.PhaseBetween(Phases(), 215.3f, 230f));
    }

    [Fact]
    public void TwoPhasesInOneGapNameTheLater()
    {
        // Whatever the anchors' order in the list: the next row belongs to the
        // last phase that started, not the first.
        var jumbled = new List<BossAnchor>
        {
            new() { Time = 500f, Label = "P3 Gaia" },
            new() { Time = 215.3f, Label = "P2 Shiva" },
        };
        Assert.Equal("P3 Gaia", SheetTimeline.PhaseBetween(jumbled, 100f, 600f));
    }

    [Fact]
    public void UnlabelledAnchorsAreStructuralAndNeverDraw()
    {
        var anchors = new List<BossAnchor>
        {
            new() { Time = 100f, Label = "" },
            new() { Time = 110f, Label = "   " },
        };
        Assert.Equal("", SheetTimeline.PhaseBetween(anchors, 90f, 200f));
    }

    [Fact]
    public void ALabelledAnchorStillWinsOverALaterBlankOne()
    {
        // The later anchor has no name to draw, so it must not shadow the named
        // one sitting behind it in the same gap.
        var anchors = new List<BossAnchor>
        {
            new() { Time = 215.3f, Label = "P2 Shiva" },
            new() { Time = 240f, Label = "" },
        };
        Assert.Equal("P2 Shiva", SheetTimeline.PhaseBetween(anchors, 200f, 300f));
    }

    [Fact]
    public void NoAnchorsIsQuiet()
        => Assert.Equal("", SheetTimeline.PhaseBetween(new List<BossAnchor>(), 0f, 9999f));

    [Fact]
    public void ABossWhoseNameDoesNotResolveContributesNoDivider()
    {
        // Phase labels ride on boss anchors, and BossNames.Add drops an anchor
        // whole when the boss's name can't be matched in BNpcName - label and
        // all. Off the game (here) nothing resolves, so this is also why
        // FruData.BossAnchors() is empty in the test host and the baked phase
        // names can't be asserted from here.
        //
        // Failing that way round is the right way round: no anchor means no
        // divider, which is the same silence as a fight that never had phase
        // names. The resync those anchors exist for degrades identically.
        var list = new List<BossAnchor>();
        BossNames.Add(list, "Definitely Not A Real Boss", 100f, "P2 Nonsense");
        Assert.Empty(list);
        Assert.Equal("", SheetTimeline.PhaseBetween(list, 0f, 999f));
    }
}
