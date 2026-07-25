using Xunit;

namespace FrenMits.Tests;

// Doomtrain and Enuo, the first extremes to ship, and the first fights where the
// pipeline had to tell two different things apart:
//
//   * an ability the game has no name for is not a mechanic. Doomtrain's
//     unknown_b294 is the boss auto-attack - 104 hits on the two tanks - and the
//     import made it 53 of that sheet's 118 rows, every one a nameless bar with a
//     tank mit spent on it.
//   * a whole stretch of the fight running early is not a broken anchor. Both
//     trials gate phases on the boss's HP, so a faster party arrives sooner and
//     every anchor past that point shares one offset. Those anchors are the fix
//     for it, not the fault, and stripping them would remove the correction.
//
// The rest is covered for free by BuiltinSheetTests and NewFightWiringTests.
public class ExtremeTrialTests
{
    private const uint Doomtrain = 1308;   // Hell on Rails (Extreme)
    private const uint Enuo = 1362;        // the Unmaking (Extreme)

    public static TheoryData<uint> Both() => new() { Doomtrain, Enuo };

    [Fact]
    public void BothAreRegisteredAsExtremes()
    {
        Assert.True(Builtin.Has(Doomtrain));
        Assert.True(Builtin.Has(Enuo));
        Assert.Equal("Doomtrain (EX)", Builtin.Name(Doomtrain));
        Assert.Equal("Enuo (EX)", Builtin.Name(Enuo));
        foreach (var t in new[] { Doomtrain, Enuo })
        {
            Assert.Equal("Extreme", Builtin.Category(t));
            Assert.Equal("Dawntrail", Builtin.Expansion(t));
        }
    }

    [Fact]
    public void NoRowIsAnAbilityTheGameCannotName()
    {
        // The regression Doomtrain exists to guard. A log calls an ability it
        // doesn't know "unknown_<hex>", and for these the game's own Action sheet
        // is blank too - there is no name to put on the bar.
        foreach (var (territory, _, _, _) in Builtin.Fights)
            foreach (var row in Builtin.CustomRows(territory))
                Assert.False(row.Mechanic.StartsWith("unknown", StringComparison.OrdinalIgnoreCase),
                    $"{Builtin.Name(territory)} has a row called \"{row.Mechanic}\" at {row.Time}s");
    }

    [Fact]
    public void DoomtrainKeptTheRealSheetAndDroppedOnlyTheAutoAttacks()
    {
        // 118 rows in, 53 of them the auto-attack, so 65 is the whole fight. The
        // 13 tank mits the planner hung on those autos went with them, leaving
        // 122 of the sheet's 135 presses.
        Assert.Equal(65, DoomtrainData.Timeline.Length);

        var total = 0;
        foreach (var slot in Builtin.Slots(Doomtrain))
            total += Builtin.BuildLines(Doomtrain, slot).Count;
        Assert.True(total >= 122, $"only {total} calls baked; presses are being dropped again");
    }

    [Fact]
    public void EnuoShippedItsWholeSheet()
    {
        // Nothing to strip here - 71 rows and all 152 presses.
        Assert.Equal(71, EnuoData.Timeline.Length);

        var total = 0;
        foreach (var slot in Builtin.Slots(Enuo))
            total += Builtin.BuildLines(Enuo, slot).Count;
        Assert.True(total >= 152, $"only {total} calls baked; presses are being dropped again");
    }

    [Fact]
    public void EnuosSwappingEmptinessIsNeverAnAnchor()
    {
        // Enuo casts Airy or Dense Emptiness first, drawn per pull. The sheet
        // recorded one order, and in the logs the two are a clean 65s apart from
        // where it has them - anchoring either would name the wrong moment.
        // They were invisible until the search window widened past 20s, which is
        // why this one is pinned by id.
        var swapping = new uint[] { 0xC370, 0xC371 };
        Assert.DoesNotContain(EnuoData.Timeline, e => swapping.Contains(e.Sync));
        // the rows themselves stay - the mechanic does happen
        Assert.Contains(EnuoData.Timeline, e => e.Mechanic.Contains("Emptiness", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Both))]
    public void SeverityAndBustersReachTheBoard(uint territory)
    {
        var rows = Builtin.CustomRows(territory);
        Assert.True(rows.Count >= 39, $"only {rows.Count} graded rows");
        Assert.True(rows.Count(r => r.Buster) >= 15, "tank busters went missing");

        var board = SheetTimeline.Build(Fx.Builtin(territory, "T1"));
        Assert.True(board.Count(r => r.Hurt > 0) >= 35, "grades did not reach the board");
        Assert.True(board.Count(r => r.Buster) >= 15, "buster flags did not reach the board");
    }

    [Theory]
    [MemberData(nameof(Both))]
    public void NoCastIsAnchoredTwice(uint territory)
    {
        foreach (var group in Builtin.SyncPoints(territory).GroupBy(sp => sp.Ability))
        {
            var times = group.Select(sp => sp.Time).OrderBy(t => t).ToList();
            for (var i = 1; i < times.Count; i++)
                Assert.True(times[i] - times[i - 1] > 1f,
                    $"0x{group.Key:X} anchored twice at {times[i - 1]} and {times[i]}");
        }
    }

    [Theory]
    [MemberData(nameof(Both))]
    public void RowsAreInTimeOrderAndNoneAreBlank(uint territory)
    {
        // The two Data classes are independent types, so flatten to the parts
        // this actually asserts on.
        var rows = territory == Doomtrain
            ? DoomtrainData.Timeline.Select(e => (e.Time, e.Mechanic, e.Actions.Length)).ToList()
            : EnuoData.Timeline.Select(e => (e.Time, e.Mechanic, e.Actions.Length)).ToList();

        for (var i = 1; i < rows.Count; i++)
            Assert.True(rows[i].Time >= rows[i - 1].Time, $"row {i} is out of order");
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Mechanic)));
        Assert.All(rows, r => Assert.Equal(10, r.Item3));
    }

    [Theory]
    [MemberData(nameof(Both))]
    public void BothHaveTheirUntargetableWindows(uint territory)
    {
        // Two each, and an extreme that lost them would run its whole second half
        // on a clock that never paused.
        Assert.Equal(2, Downtimes.For(territory).Count);
        Assert.All(Downtimes.For(territory), w => Assert.True(w.Duration > 0f));
    }
}
