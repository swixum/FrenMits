using Xunit;

namespace FrenMits.Tests;

// ApplySlot runs on every zone-in and every column switch. It has to top a plan
// up with new sheet rows WITHOUT resurrecting deleted calls, duplicating what's
// already there, or losing anyone's edits.
public class ApplySlotTests
{
    private const ushort Dmu = Builtin.DmuTerritory;

    [Fact]
    public void AFreshFightBakesTheColumn()
    {
        var fight = new FightProfile { TerritoryId = Dmu };
        // The return value counts what the TOP-UP added on top of an existing
        // plan, so a first bake reports 0 while still filling the fight.
        var added = Builtin.ApplySlot(fight, "T1");

        Assert.Equal(0, added);
        Assert.Equal("T1", fight.Slot);
        Assert.True(fight.AutoLoaded);
        Assert.Equal(Builtin.BuildLines(Dmu, "T1").Count, fight.Lines.Count);
    }

    [Fact]
    public void ReapplyingTheSameColumnAddsNothing()
    {
        var fight = new FightProfile { TerritoryId = Dmu };
        Builtin.ApplySlot(fight, "T1");
        var before = fight.Lines.Count;

        Assert.Equal(0, Builtin.ApplySlot(fight, "T1"));
        Assert.Equal(before, fight.Lines.Count);
    }

    [Fact]
    public void SwitchingColumnsStashesTheOneYouLeave()
    {
        var fight = new FightProfile { TerritoryId = Dmu };
        Builtin.ApplySlot(fight, "T1");
        var tankLines = fight.Lines;

        Builtin.ApplySlot(fight, "SCH");

        Assert.Equal("SCH", fight.Slot);
        Assert.Same(tankLines, fight.SavedSlots["T1"]);
        Assert.Same(fight.Lines, fight.SavedSlots["SCH"]);
        Assert.NotSame(tankLines, fight.Lines);
    }

    [Fact]
    public void ComingBackToAColumnKeepsTheEditsYouMade()
    {
        var fight = new FightProfile { TerritoryId = Dmu };
        Builtin.ApplySlot(fight, "T1");
        var mine = Fx.Line(500f, "My own reminder", "Second Wind");
        mine.Custom = true;
        fight.Lines.Add(mine);

        Builtin.ApplySlot(fight, "SCH");
        Builtin.ApplySlot(fight, "T1");

        Assert.Contains(fight.Lines, l => l.Mechanic == "My own reminder");
    }

    [Fact]
    public void ADeletedCallIsNotBakedBackIn()
    {
        var fight = new FightProfile { TerritoryId = Dmu };
        Builtin.ApplySlot(fight, "T1");
        var victim = fight.Lines.First(l => !string.IsNullOrWhiteSpace(l.Action));
        fight.DeletedCalls.Add(new DeletedCall
        {
            Slot = "T1", Time = victim.Time, Mechanic = victim.Mechanic, Action = victim.Action,
        });
        fight.Lines.Remove(victim);
        var after = fight.Lines.Count;

        Builtin.ApplySlot(fight, "T1");

        Assert.Equal(after, fight.Lines.Count);
        Assert.DoesNotContain(fight.Lines, l => Builtin.SameCall(l, victim));
    }

    [Fact]
    public void TombstonesAreMatchedThroughARenameOrRetime()
    {
        // A sheet update that nudges a call or renames its mechanic must not
        // resurrect something the user deleted.
        var baked = Builtin.BuildLines(Dmu, "T1").First(l => !string.IsNullOrWhiteSpace(l.Action));
        var tomb = new DeletedCall
        {
            Slot = "T1", Time = baked.Time + 4f, Mechanic = "Renamed since", Action = baked.Action,
        };
        Assert.True(Builtin.MatchesTombstone(tomb, "T1", baked));
    }

    [Fact]
    public void ATombstoneIsScopedToItsOwnColumn()
    {
        var baked = Builtin.BuildLines(Dmu, "T1").First(l => !string.IsNullOrWhiteSpace(l.Action));
        var tomb = new DeletedCall { Slot = "T1", Time = baked.Time, Mechanic = baked.Mechanic, Action = baked.Action };
        Assert.False(Builtin.MatchesTombstone(tomb, "SCH", baked));
    }

    [Fact]
    public void ResetSlotThrowsAwayEditsAndDeletions()
    {
        var fight = new FightProfile { TerritoryId = Dmu };
        Builtin.ApplySlot(fight, "T1");
        fight.Lines.Add(Fx.Line(500f, "Mine", "Second Wind"));
        fight.DeletedCalls.Add(new DeletedCall { Slot = "T1", Time = 16f, Mechanic = "X", Action = "Reprisal" });

        Builtin.ResetSlot(fight, "T1");

        Assert.DoesNotContain(fight.Lines, l => l.Mechanic == "Mine");
        Assert.DoesNotContain(fight.DeletedCalls, d => string.Equals(d.Slot, "T1", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(Builtin.BuildLines(Dmu, "T1").Count, fight.Lines.Count);
    }

    [Fact]
    public void PreserveEditTombstonesTheOriginalBeforeItChanges()
    {
        var fight = new FightProfile { TerritoryId = Dmu };
        Builtin.ApplySlot(fight, "T1");
        var line = fight.Lines.First(l => !l.Custom);

        Builtin.PreserveEdit(fight, "T1", line);

        Assert.True(line.Custom);
        Assert.Single(fight.DeletedCalls);
        // A second edit of the same line must not pile up another tombstone.
        Builtin.PreserveEdit(fight, "T1", line);
        Assert.Single(fight.DeletedCalls);
    }

    [Fact]
    public void ApplySlotNeverLeavesDuplicateCalls()
    {
        foreach (var (territory, _, _) in Builtin.Fights)
            foreach (var slot in new[] { "T1", "SCH", "M1" })
            {
                var fight = new FightProfile { TerritoryId = territory };
                Builtin.ApplySlot(fight, slot);
                Builtin.ApplySlot(fight, slot);   // a second zone-in

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var l in fight.Lines)
                    Assert.True(seen.Add($"{l.Time:0.###}|{l.Mechanic.Trim()}|{l.Action.Trim()}"),
                        $"{territory}/{slot}: re-applying the column duplicated '{l.Action}' at {l.Time}");
            }
    }

    [Fact]
    public void AnEmptySlotNameFallsBackToTheFirstColumn()
    {
        var fight = new FightProfile { TerritoryId = Dmu };
        Builtin.ApplySlot(fight, "");
        Assert.Equal(SlotNames.Standard[0], fight.Slot);
    }

    [Fact]
    public void SameCallToleratesASmallRetimeButNotABigOne()
    {
        var a = Fx.Line(100f, "Raidwide", "Reprisal");
        Assert.True(Builtin.SameCall(a, Fx.Line(100.5f, "raidwide", "Reprisal")));
        Assert.False(Builtin.SameCall(a, Fx.Line(120f, "Raidwide", "Reprisal")));
        Assert.False(Builtin.SameCall(a, Fx.Line(100f, "Buster", "Reprisal")));
    }
}
