using Xunit;

namespace FrenMits.Tests;

// M11S is the first fight generated end-to-end by tools/gen_official_fight.py from
// an in-game sheet, so these pin the things that generator quietly got wrong on the
// first pass. Each one cost a real bug:
//
//   * presses were dropped because a mit is pressed BEFORE its mechanic and the
//     attach window was a symmetric 2s
//   * the same ability id landed in SyncPoints twice, which broke replay
//     auto-start - and the repair for it deleted 27 real rows (see M9sDataTests)
//   * severity and buster flags had nowhere to live in the baked shape at all
//
// A regeneration that loses any of it should fail here rather than ship.
public class M11sDataTests
{
    private const uint Territory = 1325;

    [Fact]
    public void ItIsRegisteredAsABuiltin()
    {
        Assert.True(Builtin.Has(Territory));
        Assert.Equal("M11S - The Tyrant", Builtin.Name(Territory));
        Assert.Contains(Builtin.Fights, f => f.Territory == Territory && f.Category == "Savage");
    }

    [Fact]
    public void EverySlotBuildsAPlan()
    {
        // All ten standard columns, every one of them carrying calls - an official
        // fight ships its whole planned sheet, never an empty scaffold.
        foreach (var slot in Builtin.Slots(Territory))
        {
            var lines = Builtin.BuildLines(Territory, slot);
            Assert.True(lines.Count > 0, $"slot {slot} baked no lines");
            Assert.All(lines, l => Assert.False(string.IsNullOrWhiteSpace(l.Action)));
        }
    }

    [Fact]
    public void TheWholeSheetSurvivedTheBake()
    {
        // 129 rows and 202 presses came out of the in-game sheet, and all of them
        // have to be here.
        Assert.Equal(129, M11sData.Timeline.Length);

        var total = 0;
        foreach (var slot in Builtin.Slots(Territory))
            total += Builtin.BuildLines(Territory, slot).Count;
        Assert.True(total >= 202, $"only {total} calls baked; presses are being dropped again");
    }

    [Fact]
    public void SeverityAndBustersReachTheBoard()
    {
        // Hurt/Buster live on FightProfile.CustomRows and nowhere else, so a
        // built-in that doesn't hand them over draws every row as a plain hit.
        var rows = Builtin.CustomRows(Territory);
        Assert.True(rows.Count >= 72, $"only {rows.Count} graded rows");
        Assert.True(rows.Count(r => r.Buster) >= 20, "tank busters went missing");
        Assert.All(rows, r => Assert.True(r.Hurt > 0 || r.Buster));

        var board = SheetTimeline.Build(Fx.Builtin(Territory, "T1"));
        Assert.True(board.Count(r => r.Hurt > 0) >= 60, "grades did not reach the board");
        Assert.True(board.Count(r => r.Buster) >= 20, "buster flags did not reach the board");
    }

    [Fact]
    public void NoCastIsAnchoredTwice()
    {
        // The invariant BuiltinSheetTests enforces across every fight, pinned here
        // too because M11S is the one whose import actually violated it: replay
        // auto-start can only anchor a clock from an ability that appears once.
        foreach (var group in Builtin.SyncPoints(Territory).GroupBy(sp => sp.Ability))
        {
            var times = group.Select(sp => sp.Time).OrderBy(t => t).ToList();
            for (var i = 1; i < times.Count; i++)
                Assert.True(times[i] - times[i - 1] > 1f,
                    $"0x{group.Key:X} anchored twice at {times[i - 1]} and {times[i]}");
        }
    }

    [Fact]
    public void TheGenericNameNeverShadowsASpecificOne()
    {
        // "Assault Evolved" is the cast bar; Sweeping Victory, Sharp Taste and
        // Heavy Weight are what actually lands, each its own cast a second later.
        // Reading the pair as one mechanic baked twice, and deleting one of them,
        // took whichever the tie-break disliked - along with its mits.
        var mechs = M11sData.Timeline.Select(e => e.Mechanic).ToList();
        foreach (var specific in new[] { "Sweeping Victory", "Sharp Taste", "Heavy Weight" })
            Assert.True(mechs.Count(m => m == specific) >= 3,
                $"{specific} only appears {mechs.Count(m => m == specific)} times; "
                + "the generic name is shadowing it again");
    }

    [Fact]
    public void TheRandomisedAssaultVariantsAreNeverAnchors()
    {
        // Which of B418/B419/B41A the boss casts is drawn per pull, so the one the
        // sheet's import saw fires several seconds elsewhere in most kills. The
        // plugin matches a mechanic anchor within 8s either way, so anchoring one
        // does not fail safe - it re-bases the clock onto the wrong moment.
        // B417 opens every pull at the same time and stays an anchor.
        var randomised = new uint[] { 0xB418, 0xB419, 0xB41A };
        Assert.DoesNotContain(M11sData.Timeline, e => randomised.Contains(e.Sync));
        Assert.Contains(M11sData.Timeline, e => e.Sync == 0xB417);
    }

    [Fact]
    public void RowsAreInTimeOrderAndNoneAreBlank()
    {
        var t = M11sData.Timeline;
        for (var i = 1; i < t.Length; i++)
            Assert.True(t[i].Time >= t[i - 1].Time, $"row {i} is out of order");
        Assert.All(t, e => Assert.False(string.IsNullOrWhiteSpace(e.Mechanic)));
        Assert.All(t, e => Assert.Equal(10, e.Actions.Length));
    }

    [Fact]
    public void ThereIsExactlyOnePhase()
    {
        // Not an oversight: across 8 top kills the logs show ZERO untargetable
        // windows, so the fight genuinely never transitions. If that ever changes,
        // this failing is the signal to re-derive the phases.
        Assert.Single(M11sData.PhaseStarts());
        Assert.Empty(Downtimes.For(Territory));
    }
}
