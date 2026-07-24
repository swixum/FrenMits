using Xunit;

namespace FrenMits.Tests;

// FightProfile.OrderedLines caches its sort behind a fingerprint. If invalidation
// ever misses a case, the overlay silently keeps calling a stale plan - which
// looks like the plugin working, so nothing would flag it in game.
public class OrderedLinesTests
{
    private static FightProfile ThreeLines()
    {
        var f = new FightProfile();
        f.Lines.Add(Fx.Line(30, "C", "Feint"));
        f.Lines.Add(Fx.Line(10, "A", "Reprisal"));
        f.Lines.Add(Fx.Line(20, "B", "Rampart"));
        return f;
    }

    [Fact]
    public void SortsByTime()
    {
        var f = ThreeLines();
        Assert.Equal(new[] { 10f, 20f, 30f }, f.OrderedLines.Select(l => l.Time));
    }

    [Fact]
    public void RepeatedAsksAreStable()
    {
        var f = ThreeLines();
        Assert.Equal(f.OrderedLines.Select(l => l.Mechanic), f.OrderedLines.Select(l => l.Mechanic));
    }

    [Fact]
    public void SortIsStableForLinesSharingATime()
    {
        // Two calls at the same second must keep the order they were baked in, or
        // the board and the overlay could disagree about which comes first.
        var f = new FightProfile();
        f.Lines.Add(Fx.Line(10, "first", "Reprisal"));
        f.Lines.Add(Fx.Line(10, "second", "Feint"));
        f.Lines.Add(Fx.Line(10, "third", "Addle"));
        Assert.Equal(new[] { "first", "second", "third" }, f.OrderedLines.Select(l => l.Mechanic));
    }

    [Fact]
    public void AddingALineIsPickedUp()
    {
        var f = ThreeLines();
        _ = f.OrderedLines;
        f.Lines.Add(Fx.Line(15, "New", "Addle"));
        Assert.Equal(new[] { 10f, 15f, 20f, 30f }, f.OrderedLines.Select(l => l.Time));
    }

    [Fact]
    public void RemovingALineIsPickedUp()
    {
        var f = ThreeLines();
        _ = f.OrderedLines;
        f.Lines.RemoveAt(0);
        Assert.Equal(new[] { 10f, 20f }, f.OrderedLines.Select(l => l.Time));
    }

    [Fact]
    public void RetimingALineIsPickedUp()
    {
        var f = ThreeLines();
        _ = f.OrderedLines;
        f.Lines[0].Time = 5f;    // the 30s line moves to the front
        Assert.Equal(new[] { 5f, 10f, 20f }, f.OrderedLines.Select(l => l.Time));
    }

    [Fact]
    public void EvenAHairlineRetimeIsPickedUp()
    {
        // The editor's nudge buttons move a call by a tenth of a second; a coarse
        // fingerprint would round that away and keep serving the old sort.
        var f = ThreeLines();
        _ = f.OrderedLines;
        f.Lines[1].Time = 10.01f;
        Assert.Equal(10.01f, f.OrderedLines[0].Time);
    }

    [Fact]
    public void SwappingTheWholeListIsPickedUp()
    {
        // Switching sheet columns replaces Lines outright (the alias invariant).
        var f = ThreeLines();
        _ = f.OrderedLines;
        f.Lines = new List<MitLine> { Fx.Line(99, "Z", "Holmgang") };
        Assert.Equal(new[] { 99f }, f.OrderedLines.Select(l => l.Time));
    }

    [Fact]
    public void SwappingToAListWithTheSameShapeIsStillPickedUp()
    {
        var f = ThreeLines();
        _ = f.OrderedLines;
        f.Lines = new List<MitLine>
        {
            Fx.Line(30, "C2", "Feint"), Fx.Line(10, "A2", "Reprisal"), Fx.Line(20, "B2", "Rampart"),
        };
        Assert.Equal(new[] { "A2", "B2", "C2" }, f.OrderedLines.Select(l => l.Mechanic));
    }

    [Fact]
    public void AnEmptyPlanIsFine()
    {
        var f = new FightProfile();
        Assert.Empty(f.OrderedLines);
        f.Lines.Add(Fx.Line(1, "A", "Reprisal"));
        Assert.Single(f.OrderedLines);
    }

    [Fact]
    public void EditingAnActionDoesNotNeedAResort()
    {
        // Not an invalidation case (order can't change), but the caller must still
        // see the edit, because the cache holds the live line objects.
        var f = ThreeLines();
        _ = f.OrderedLines;
        f.Lines[1].Action = "Changed";
        Assert.Equal("Changed", f.OrderedLines[0].Action);
    }
}
