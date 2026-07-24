using Xunit;

namespace FrenMits.Tests;

// SlotNames.NormalizeFight runs over every saved fight on EVERY launch, so a
// mistake here silently rewrites everyone's columns on next start.
public class SlotNameTests
{
    [Theory]
    [InlineData("MT", "T1")]
    [InlineData("OT", "T2")]
    [InlineData("D1", "M1")]
    [InlineData("D2", "M2")]
    [InlineData("D3", "R1")]
    [InlineData("R", "R1")]
    [InlineData("D4", "R2")]
    [InlineData("Caster", "R2")]
    [InlineData("WHM", "WHM")]
    [InlineData("SGE", "SGE")]
    [InlineData("H1", "H1")]
    public void AliasesMapOntoTheStandard(string alias, string expected)
        => Assert.Equal(expected, SlotNames.Canon(alias));

    [Theory]
    [InlineData("mt", "T1")]
    [InlineData("  ot  ", "T2")]
    [InlineData("d3", "R1")]
    public void MatchingIgnoresCaseAndPadding(string alias, string expected)
        => Assert.Equal(expected, SlotNames.Canon(alias));

    [Fact]
    public void UnknownColumnsPassThroughUntouched()
    {
        Assert.Equal("Swix", SlotNames.Canon("Swix"));   // player-named column
        Assert.Equal("", SlotNames.Canon(null));
    }

    [Fact]
    public void CanonIsIdempotent()
    {
        foreach (var s in new[] { "MT", "OT", "D1", "D2", "D3", "D4", "R", "Caster", "H1", "H2", "Swix", "" })
            Assert.Equal(SlotNames.Canon(s), SlotNames.Canon(SlotNames.Canon(s)));
    }

    [Fact]
    public void EveryStandardNameIsAlreadyCanonical()
    {
        foreach (var s in SlotNames.Standard)
            Assert.Equal(s, SlotNames.Canon(s));
    }

    [Fact]
    public void LegacyAndFruRoundTripBackToCanonical()
    {
        foreach (var s in SlotNames.Standard)
        {
            Assert.Equal(s, SlotNames.Canon(SlotNames.ToLegacy(s)));
            Assert.Equal(s, SlotNames.Canon(SlotNames.ToFru(s)));
        }
    }

    [Fact]
    public void NormalizeRenamesSlotStashesAndTombstones()
    {
        var fight = new FightProfile
        {
            Slot = "MT",
            CustomSlots = new List<string> { "MT", "D3" },
            DeletedCalls = new List<DeletedCall> { new() { Slot = "D1", Mechanic = "Trine" } },
        };
        fight.Lines = new List<MitLine> { Fx.Line(10, "Trine", "Reprisal") };
        fight.SavedSlots["MT"] = fight.Lines;
        fight.SavedSlots["OT"] = new List<MitLine> { Fx.Line(12, "Trine", "Rampart") };

        Assert.True(SlotNames.NormalizeFight(fight));

        Assert.Equal("T1", fight.Slot);
        Assert.Equal(new[] { "T1", "R1" }, fight.CustomSlots);
        Assert.Equal("M1", fight.DeletedCalls[0].Slot);
        Assert.True(fight.SavedSlots.ContainsKey("T1"));
        Assert.True(fight.SavedSlots.ContainsKey("T2"));
        Assert.False(fight.SavedSlots.ContainsKey("MT"));
    }

    [Fact]
    public void NormalizeIsIdempotent()
    {
        var fight = new FightProfile { Slot = "MT", CustomSlots = new List<string> { "MT", "D1", "D3" } };
        fight.Lines = new List<MitLine> { Fx.Line(10, "Trine", "Reprisal") };
        fight.SavedSlots["MT"] = fight.Lines;

        Assert.True(SlotNames.NormalizeFight(fight));
        // A second pass must report (and change) nothing, or the plugin would
        // rewrite and re-save the config on every single launch.
        Assert.False(SlotNames.NormalizeFight(fight));
    }

    [Fact]
    public void ColumnCollisionKeepsTheFullerPlan()
    {
        // A sheet with both MT and T1 columns collapses to one; the plan with
        // more lines in it is the one worth keeping.
        var fight = new FightProfile { Slot = "T1", CustomSlots = new List<string> { "MT", "T1" } };
        fight.SavedSlots["MT"] = new List<MitLine> { Fx.Line(10, "A", "Reprisal"), Fx.Line(20, "B", "Rampart") };
        fight.SavedSlots["T1"] = new List<MitLine> { Fx.Line(10, "A", "Reprisal") };
        fight.Lines = fight.SavedSlots["T1"];

        SlotNames.NormalizeFight(fight);

        Assert.Single(fight.CustomSlots);
        Assert.Equal(2, fight.SavedSlots["T1"].Count);
    }

    [Fact]
    public void ActiveLinesStayAliasedIntoTheStash()
    {
        // Lines IS SavedSlots[Slot] by reference; breaking that loses every edit
        // the next time the user switches columns.
        var fight = new FightProfile { Slot = "MT" };
        fight.Lines = new List<MitLine> { Fx.Line(10, "Trine", "Reprisal") };
        fight.SavedSlots["MT"] = fight.Lines;

        SlotNames.NormalizeFight(fight);

        Assert.Same(fight.Lines, fight.SavedSlots[fight.Slot]);
    }
}
