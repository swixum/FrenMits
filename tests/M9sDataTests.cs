using Xunit;

namespace FrenMits.Tests;

// M9S is the fight that exposed what the generator was really doing wrong.
//
// It used to give a row the NEAREST cast id within 3s, which is a fuzzy match
// rather than an identity: every row inside that window inherited the same id.
// Downstream, "two rows share a cast id" was read as "one cast baked twice" and
// one of the two rows was deleted. In M9S that was wrong twelve times over -
// Coffinfiller lands on the same second as Half Moon, Plummet on Gravegrazer,
// Ultrasonic Spread on Blood Lash - and each deletion took a real hit and the
// mits written for it. It cost M10S nine rows and M11S twenty-seven, silently.
//
// Anchors carry the ability's name and so do rows, so they are matched on that
// now, one cast to one row. Nothing is deleted, and what ships is what the sheet
// said. These pin the numbers that proves it.
public class M9sDataTests
{
    private const uint Territory = 1321;

    [Fact]
    public void ItIsRegisteredAsABuiltin()
    {
        Assert.True(Builtin.Has(Territory));
        Assert.Equal("M9S - Vamp Fatale", Builtin.Name(Territory));
        Assert.Contains(Builtin.Fights, f => f.Territory == Territory && f.Category == "Savage");
    }

    [Fact]
    public void TheWholeSheetSurvivedTheBake()
    {
        // 110 rows and 134 presses came out of the in-game sheet. Two of those were
        // a bare "Party Mit" in the caster column, which no caster can press, so
        // 132 is the whole sheet - see PartyMitTermTests.
        Assert.Equal(110, M9sData.Timeline.Length);

        var total = 0;
        foreach (var slot in Builtin.Slots(Territory))
            total += Builtin.BuildLines(Territory, slot).Count;
        Assert.True(total >= 132, $"only {total} calls baked; presses are being dropped again");
    }

    [Fact]
    public void MechanicsThatShareASecondBothSurvive()
    {
        // The regression this fight exists to guard. Half Moon and Coffinfiller
        // land on the same second four times over, and each is its own cast with
        // its own mits - so each keeps its own row, rather than one being read as
        // a copy of the other and deleted.
        var pairs = M9sData.Timeline.Count(e => e.Mechanic == "Half Moon"
            && M9sData.Timeline.Any(o => o.Mechanic == "Coffinfiller" && o.Time == e.Time));
        Assert.True(pairs >= 4, $"only {pairs} same-second pairs left; rows are being merged again");

        foreach (var g in M9sData.Timeline.Where(e => e.Sync != 0).GroupBy(e => e.Time))
            Assert.Equal(g.Count(), g.Select(e => e.Sync).Distinct().Count());
    }

    [Fact]
    public void HalfMoonIsNeverAnAnchor()
    {
        // Half Moon is cast under one of two ability ids, drawn per pull. The sheet
        // recorded whichever its own import drew, so anchoring it would re-base the
        // clock, within the plugin's 8s match window, onto a moment that pull did
        // not have: the logs put those casts up to 5s from where the sheet has them.
        // The rows are real and keep their mits - only the anchor is withheld.
        Assert.All(M9sData.Timeline.Where(e => e.Mechanic == "Half Moon"),
            e => Assert.Equal(0u, e.Sync));
        Assert.True(M9sData.Timeline.Count(e => e.Mechanic == "Half Moon") >= 10,
            "the Half Moon rows themselves went missing");
    }

    [Fact]
    public void SeverityAndBustersReachTheBoard()
    {
        var rows = Builtin.CustomRows(Territory);
        Assert.True(rows.Count >= 75, $"only {rows.Count} graded rows");
        // Vamp Fatale is a buster-heavy fight - a third of its graded rows land
        // on a tank, and Gravegrazer alone is a chain of them.
        Assert.True(rows.Count(r => r.Buster) >= 35, "tank busters went missing");

        var board = SheetTimeline.Build(Fx.Builtin(Territory, "T1"));
        Assert.True(board.Count(r => r.Hurt > 0) >= 70, "grades did not reach the board");
        Assert.True(board.Count(r => r.Buster) >= 35, "buster flags did not reach the board");
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
    public void EveryCellInTheTimelineReachesItsColumn()
    {
        // BuildLines drops a repeat of the same button on the same second, which
        // is right - but it used to key that on the row's CAST, and two mechanics
        // do share a second here (Half Moon with Coffinfiller, four times over),
        // so a column told to press different things for each would have lost one.
        // Nothing in this fight is a genuine repeat, so every cell is a line.
        for (var i = 0; i < M9sData.Slots.Length; i++)
        {
            var cells = M9sData.Timeline.Count(e => !string.IsNullOrWhiteSpace(e.Actions[i]));
            var lines = M9sData.BuildLines(M9sData.Slots[i]).Count;
            Assert.True(lines == cells,
                $"{M9sData.Slots[i]}: {cells} planned presses became {lines} calls");
        }
    }

    [Fact]
    public void RowsAreInTimeOrderAndNoneAreBlank()
    {
        var t = M9sData.Timeline;
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
        Assert.Single(M9sData.PhaseStarts());
        Assert.Empty(Downtimes.For(Territory));
    }
}
