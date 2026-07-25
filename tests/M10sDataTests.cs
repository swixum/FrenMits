using Xunit;

namespace FrenMits.Tests;

// M10S is the first fight put through the hardened pipeline, and it immediately
// found a bug M11S had hidden: the generator deleted rows it believed were one
// cast baked twice, and it was wrong about which ones.
//
// M10S hits Alley-Oop Double-Dip at 126s and again at 129s - the second one a tank
// buster - and the merge swallowed the second hit and orphaned the two tank calls
// written for it. The generator's own self-check refused to write the file. Nine
// M10S rows were going that way in all; the full story is in M9sDataTests, which
// is where the root cause finally surfaced.
//
// Everything structural is already covered for free by BuiltinSheetTests and
// NewFightWiringTests, which re-run for every entry in Builtin.Fights. What's here
// is only what those can't know: this fight's own numbers.
public class M10sDataTests
{
    private const uint Territory = 1323;

    [Fact]
    public void ItIsRegisteredAsABuiltin()
    {
        Assert.True(Builtin.Has(Territory));
        Assert.Equal("M10S - Red Hot / Deep Blue", Builtin.Name(Territory));
        Assert.Contains(Builtin.Fights, f => f.Territory == Territory && f.Category == "Savage");
    }

    [Fact]
    public void TheWholeSheetSurvivedTheBake()
    {
        // 81 rows and 129 presses came out of the in-game sheet.
        Assert.Equal(81, M10sData.Timeline.Length);

        var total = 0;
        foreach (var slot in Builtin.Slots(Territory))
            total += Builtin.BuildLines(Territory, slot).Count;
        Assert.True(total >= 129, $"only {total} calls baked; presses are being dropped again");
    }

    [Fact]
    public void SeverityAndBustersReachTheBoard()
    {
        var rows = Builtin.CustomRows(Territory);
        Assert.True(rows.Count >= 67, $"only {rows.Count} graded rows");
        Assert.True(rows.Count(r => r.Buster) >= 15, "tank busters went missing");

        var board = SheetTimeline.Build(Fx.Builtin(Territory, "T1"));
        Assert.True(board.Count(r => r.Hurt > 0) >= 60, "grades did not reach the board");
        Assert.True(board.Count(r => r.Buster) >= 15, "buster flags did not reach the board");
    }

    [Fact]
    public void BothHitsOfADoubleDipSurvive()
    {
        // The regression this fight exists to guard. Two rows named the same thing
        // a few seconds apart are the mechanic happening twice, and the later one
        // is the buster - collapsing them loses a real hit and the mits for it.
        var dips = M10sData.Timeline.Where(e => e.Mechanic == "Alley-Oop Double-Dip").ToList();
        Assert.True(dips.Count >= 4, $"only {dips.Count} Double-Dip rows; they are being merged again");

        var first = dips.Where(e => e.Time is >= 120 and <= 135).OrderBy(e => e.Time).ToList();
        Assert.Equal(2, first.Count);
        Assert.True(first[1].Time - first[0].Time >= 2, "the pair collapsed into one row");
        Assert.Contains(first, e => e.Buster);
        // and the tank calls written for the second one are still on it
        Assert.Contains(first, e => e.Actions.Any(a => a.Contains("Rampart", StringComparison.Ordinal)));
    }

    [Fact]
    public void NoCastIsAnchoredTwice()
    {
        foreach (var group in Builtin.SyncPoints(Territory).GroupBy(sp => sp.Ability))
        {
            var times = group.Select(sp => sp.Time).OrderBy(t => t).ToList();
            for (var i = 1; i < times.Count; i++)
                Assert.True(times[i] - times[i - 1] > 1f,
                    $"0x{group.Key:X} anchored twice at {times[i - 1]} and {times[i]}");
        }
    }

    [Fact]
    public void RowsAreInTimeOrderAndNoneAreBlank()
    {
        var t = M10sData.Timeline;
        for (var i = 1; i < t.Length; i++)
            Assert.True(t[i].Time >= t[i - 1].Time, $"row {i} is out of order");
        Assert.All(t, e => Assert.False(string.IsNullOrWhiteSpace(e.Mechanic)));
        Assert.All(t, e => Assert.Equal(10, e.Actions.Length));
    }

    [Fact]
    public void ThereIsExactlyOnePhase()
    {
        // Eight top kills show zero untargetable windows, so the fight never
        // transitions. If that changes, this failing is the signal to re-derive.
        Assert.Single(M10sData.PhaseStarts());
        Assert.Empty(Downtimes.For(Territory));
    }
}
