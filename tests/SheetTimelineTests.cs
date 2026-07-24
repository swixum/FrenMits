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
}
