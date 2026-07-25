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
        foreach (var (territory, _, _, _) in Builtin.Fights)
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

    private static List<SheetTimeline.PhaseMark> Phases() => new()
    {
        new(0f, "P1 Fatebreaker"),
        new(215.3f, "P2 Shiva"),
        new(500f, "P3 Gaia"),
    };

    [Fact]
    public void APhaseStartingInTheGapIsNamed()
        => Assert.Equal("P2 Shiva", SheetTimeline.PhaseBetween(Phases(), 210f, 220f));

    [Fact]
    public void NothingDrawsWhenNoPhaseStartsInTheGap()
        => Assert.Equal("", SheetTimeline.PhaseBetween(Phases(), 220f, 300f));

    [Fact]
    public void APhaseOnARowBelongsToThatRow()
    {
        // The row AT 215.3 is P2's first hit, not P1's last, so the divider sits
        // above it - and must not then repeat above the row after.
        Assert.Equal("P2 Shiva", SheetTimeline.PhaseBetween(Phases(), 210f, 215.3f));
        Assert.Equal("", SheetTimeline.PhaseBetween(Phases(), 215.3f, 230f));
    }

    [Fact]
    public void TwoPhasesInOneGapNameTheLater()
    {
        // Whatever their order in the list: the next row belongs to the last
        // phase that started, not the first.
        var jumbled = new List<SheetTimeline.PhaseMark>
        {
            new(500f, "P3 Gaia"),
            new(215.3f, "P2 Shiva"),
        };
        Assert.Equal("P3 Gaia", SheetTimeline.PhaseBetween(jumbled, 100f, 600f));
    }

    [Fact]
    public void NoPhasesIsQuiet()
        => Assert.Equal("", SheetTimeline.PhaseBetween(new List<SheetTimeline.PhaseMark>(), 0f, 9999f));

    // ---- where the names come from -----------------------------------------

    [Theory]
    [InlineData(733u, "MT", 4)]    // UCoB
    [InlineData(777u, "MT", 4)]    // UWU
    [InlineData(887u, "MT", 4)]    // TEA
    [InlineData(968u, "MT", 4)]    // DSR
    [InlineData(1122u, "MT", 4)]   // TOP
    [InlineData(1363u, "T1", 5)]   // DMU
    [InlineData(1238u, "T1", 5)]   // FRU
    [InlineData(1327u, "MT", 2)]   // M12S
    public void EveryBakedFightNamesItsOwnPhases(uint territory, string slot, int least)
    {
        // Every timeline FrenMits ships tags each row with its phase, so the names
        // were already in the plugin - nothing fetched, nothing baked. If a
        // re-generation ever dropped the tags the dividers would go silent with no
        // other sign that anything had changed.
        var marks = SheetTimeline.PhaseMarks(Fx.Builtin(territory, slot));
        Assert.True(marks.Count >= least, $"territory {territory}: expected {least}+ phases, found {marks.Count}");
        Assert.All(marks, m => Assert.False(string.IsNullOrWhiteSpace(m.Label)));
        // Distinct times, or two dividers would stack on one gap.
        Assert.Equal(marks.Count, marks.Select(m => m.Time).Distinct().Count());
        // In time order, so PhaseBetween's "last one wins" means the latest phase.
        for (var i = 1; i < marks.Count; i++)
            Assert.True(marks[i].Time > marks[i - 1].Time, $"territory {territory}: phases out of order");
    }

    [Fact]
    public void FruNamesThePhasesItsAnchorsDeliberatelySkip()
    {
        // The anchors stop at P3 on purpose (Pandora fired P5's opener the instant
        // it spawned), but naming a boundary was never the job they were dropped
        // from, so the marks carry P4 and P5 too.
        var marks = SheetTimeline.PhaseMarks(Fx.Builtin(1238u, "T1"));
        var labels = marks.Select(m => m.Label).ToList();
        Assert.Contains("P4", labels);
        Assert.Contains("P5", labels);
        // P2 and P3 must stay exactly on the anchors' times: that agreement is the
        // only thing establishing the two are measured on the same clock.
        Assert.Equal(215.3f, marks.Single(m => m.Label == "P2").Time, 1);
        Assert.Equal(500.0f, marks.Single(m => m.Label == "P3").Time, 1);
    }

    [Fact]
    public void FruDoesNotFoldUpItsOwnPhaseTags()
    {
        // Three rows in FruData are tagged P4 but sit at 1052-1068, interleaved
        // with P5's rows. Grouping by tag would put P4 AFTER P5; the written-out
        // table exists to avoid exactly that, so P4 must land before P5 and before
        // any row Pandora owns.
        var marks = SheetTimeline.PhaseMarks(Fx.Builtin(1238u, "T1"));
        var p4 = marks.Single(m => m.Label == "P4").Time;
        var p5 = marks.Single(m => m.Label == "P5").Time;
        Assert.True(p4 < p5, $"P4 at {p4}s must precede P5 at {p5}s");
        Assert.True(p4 < 1041f, $"P4 at {p4}s has fallen inside phase five");
    }

    [Fact]
    public void DmuSpellsItsPhasesOut()
    {
        // DMU already carried nicer titles for the practice phase-jump, so the
        // board gets them rather than a bare "P3".
        var labels = SheetTimeline.PhaseMarks(Fx.Builtin(1363u, "T1")).Select(m => m.Label).ToList();
        Assert.Contains("Phase 3: Chaos & Exdeath", labels);
        Assert.DoesNotContain("P3", labels);
    }

    [Fact]
    public void M12sPhaseTwoLandsOnTheClockTheBoardMeasures()
    {
        // P2's rows already carry Phase2Offset. A mark that forgot it would sit
        // 420s early, dropping the divider into the middle of phase one.
        var p2 = SheetTimeline.PhaseMarks(Fx.Builtin(1327u, "MT")).Single(m => m.Label == "P2");
        Assert.True(p2.Time >= M12sData.Phase2Offset,
            $"P2 at {p2.Time}s is before the offset it should already include");
    }

    [Fact]
    public void UnlabelledAnchorsAreStructuralAndNeverDraw()
    {
        var f = new FightProfile { TerritoryId = 9999 };
        f.BossAnchors.Add(new BossAnchor { Time = 100f, Label = "" });
        f.BossAnchors.Add(new BossAnchor { Time = 110f, Label = "   " });
        Assert.Empty(SheetTimeline.PhaseMarks(f));
    }

    [Fact]
    public void AnchorsAreTheFallbackWhenTheTimelineHasNoTags()
    {
        var f = new FightProfile { TerritoryId = 9999 };
        f.BossAnchors.Add(new BossAnchor { Time = 215.3f, Label = " P2 Shiva " });
        var marks = SheetTimeline.PhaseMarks(f);
        Assert.Equal("P2 Shiva", Assert.Single(marks).Label);
    }

    [Fact]
    public void TheTimelinesOwnTagsBeatTheAnchors()
    {
        // A fight with both gets the finer-grained answer: six or seven tagged
        // phases rather than an anchor list's two or three.
        var f = Fx.Builtin(1122u, "MT");
        f.BossAnchors.Add(new BossAnchor { Time = 42f, Label = "Not this one" });
        Assert.DoesNotContain(SheetTimeline.PhaseMarks(f), m => m.Label == "Not this one");
    }

    [Fact]
    public void ADutyTimelineHasNoPhasesToName()
    {
        // Duty timelines pack every encounter of an instance onto one clock in
        // 1000-second blocks, so a phase time from either source lands in the
        // wrong block - and a dungeon's bosses aren't phases anyway.
        var f = Fx.Builtin(1122u, "MT");
        f.TimelineOnly = true;
        f.BossAnchors.Add(new BossAnchor { Time = 215.3f, Label = "P2 Shiva" });
        Assert.Empty(SheetTimeline.PhaseMarks(f));
    }

    [Fact]
    public void ABossWhoseNameDoesNotResolveContributesNoDivider()
    {
        // On the anchor path the label rides on the anchor, and BossNames.Add
        // drops an anchor whole when the boss's name can't be matched in
        // BNpcName - label and all. Off the game (here) nothing resolves, which
        // is also why FruData.BossAnchors() is empty in the test host.
        //
        // Failing that way round is the right way round: no anchor means no
        // divider, the same silence as a fight that never had phase names. The
        // resync those anchors exist for degrades identically.
        var list = new List<BossAnchor>();
        BossNames.Add(list, "Definitely Not A Real Boss", 100f, "P2 Nonsense");
        Assert.Empty(list);
    }
}
