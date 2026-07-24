using Xunit;

namespace FrenMits.Tests;

// Integrity of the baked content itself. These walk every shipped fight and every
// column, so a malformed row in a new savage fails here instead of mid-pull.
public class BuiltinSheetTests
{
    public static TheoryData<ushort> Territories()
    {
        var data = new TheoryData<ushort>();
        foreach (var (territory, _, _) in Builtin.Fights) data.Add(territory);
        return data;
    }

    [Theory]
    [MemberData(nameof(Territories))]
    public void EveryShippedFightIsRegisteredBothWays(ushort territory)
    {
        Assert.True(Builtin.Has(territory));
        Assert.NotEmpty(Builtin.Name(territory));
        Assert.NotEqual("Other", Builtin.Category(territory));
    }

    [Theory]
    [MemberData(nameof(Territories))]
    public void EveryFightPresentsTheStandardColumns(ushort territory)
        => Assert.Equal(SlotNames.Standard, Builtin.Slots(territory));

    [Theory]
    [MemberData(nameof(Territories))]
    public void EveryColumnBakesUsableLines(ushort territory)
    {
        var anyLines = false;
        foreach (var slot in Builtin.Slots(territory))
        {
            var lines = Builtin.BuildLines(territory, slot);
            anyLines |= lines.Count > 0;
            foreach (var l in lines)
            {
                Assert.True(float.IsFinite(l.Time), $"{territory}/{slot}: non-finite time");
                Assert.True(l.Time >= 0f, $"{territory}/{slot}: negative time on '{l.Action}'");
                Assert.False(string.IsNullOrWhiteSpace(l.Mechanic) && string.IsNullOrWhiteSpace(l.Action),
                    $"{territory}/{slot}: a row with neither a mechanic nor an action");
                Assert.NotNull(l.Jobs);
            }
        }
        Assert.True(anyLines, $"territory {territory} bakes nothing for any column");
    }

    [Theory]
    [MemberData(nameof(Territories))]
    public void NoColumnBakesTheSameCallTwice(ushort territory)
    {
        foreach (var slot in Builtin.Slots(territory))
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in Builtin.BuildLines(territory, slot))
            {
                var key = $"{l.Time:0.###}|{l.Mechanic.Trim()}|{l.Action.Trim()}|{string.Join('/', l.Jobs)}";
                Assert.True(seen.Add(key), $"{territory}/{slot}: duplicate baked call {key}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Territories))]
    public void ResyncAnchorsAreWellFormed(ushort territory)
    {
        var points = Builtin.SyncPoints(territory);
        foreach (var sp in points)
        {
            Assert.NotEqual(0u, sp.Ability);
            Assert.True(float.IsFinite(sp.Time) && sp.Time >= 0f,
                $"territory {territory}: anchor '{sp.Label}' at {sp.Time}");
        }
    }

    [Fact]
    public void RepeatedAnchorsAlwaysResolveTheSameWay()
    {
        // One cast can bake more than one anchor, because a sheet may carry two rows
        // for the same moment (FRU has "Fulgent Blade 2" and "3" both at 1041, and
        // two Burnished Glory rows at 86). Copies at one coordinate are only safe if
        // the winner is decided, not left to list order: SnapToCast breaks a tie
        // toward a phase anchor and gives it the wider window, so at most one of the
        // copies may be a phase anchor.
        foreach (var (territory, _, _) in Builtin.Fights)
            foreach (var group in Builtin.SyncPoints(territory)
                         .GroupBy(sp => $"{sp.Ability:X}|{sp.Time:0.#}")
                         .Where(g => g.Count() > 1))
            {
                var phases = group.Count(sp => sp.IsPhase);
                Assert.True(phases <= 1,
                    $"territory {territory}: {group.Key} bakes {phases} phase anchors at one coordinate");
            }
    }

    // Boss-presence anchors resolve names through the game's BNpcName sheet, so
    // offline they come back empty. This pins the SHAPE (nothing malformed, and
    // no crash when the sheet isn't there), not the contents.
    [Theory]
    [MemberData(nameof(Territories))]
    public void BossAnchorsAreWellFormed(ushort territory)
    {
        foreach (var ba in Builtin.BossAnchors(territory))
        {
            Assert.NotEqual(0u, ba.NameId);
            Assert.True(float.IsFinite(ba.Time) && ba.Time >= 0f);
        }
    }

    [Theory]
    [MemberData(nameof(Territories))]
    public void EveryRolePicksAColumn(ushort territory)
    {
        foreach (var role in Builtin.Roles)
            Assert.False(string.IsNullOrEmpty(Builtin.RoleSlot(territory, role)),
                $"territory {territory} has no column for role '{role}'");
    }

    [Theory]
    [MemberData(nameof(Territories))]
    public void EveryJobGuessesAColumnThatExists(ushort territory)
    {
        var slots = Builtin.Slots(territory);
        foreach (var job in Jobs.All)
        {
            var slot = Builtin.DefaultSlotForJob(territory, job.Abbreviation);
            Assert.Contains(slot, slots);
        }
    }

    [Fact]
    public void AnUnknownJobGetsNoGuessRatherThanSomeoneElsesSeat()
    {
        // A brand new job (next expansion) must not silently inherit the main
        // tank's calls.
        Assert.Equal("", Builtin.DefaultSlotForJobIn(SlotNames.Standard, "XYZ"));
        Assert.Equal("", Builtin.DefaultSlotForJobIn(SlotNames.Standard, null));
    }

    [Theory]
    [MemberData(nameof(Territories))]
    public void PhaseJumpTargetsAreOrderedAndReachable(ushort territory)
    {
        var phases = Builtin.PhaseStarts(territory);
        for (var i = 1; i < phases.Count; i++)
            Assert.True(phases[i].Time > phases[i - 1].Time,
                $"territory {territory}: phase {i + 1} starts before phase {i}");
        foreach (var p in phases)
        {
            Assert.NotEmpty(p.Name);
            Assert.True(p.Time >= 0f);
        }
    }

    [Theory]
    [MemberData(nameof(Territories))]
    public void ApplySlotLeavesTheFightSortedAndAliased(ushort territory)
    {
        foreach (var slot in Builtin.Slots(territory))
        {
            var fight = new FightProfile { TerritoryId = territory };
            Builtin.ApplySlot(fight, slot);

            Assert.Equal(slot, fight.Slot);
            Assert.Same(fight.Lines, fight.SavedSlots[slot]);
            for (var i = 1; i < fight.Lines.Count; i++)
                Assert.True(fight.Lines[i].Time >= fight.Lines[i - 1].Time,
                    $"{territory}/{slot}: lines came back out of order");
        }
    }

    [Fact]
    public void DowntimeWindowsNeverOverlapOrRunBackwards()
    {
        foreach (var (territory, _, _) in Builtin.Fights)
        {
            var windows = Downtimes.For(territory).OrderBy(w => w.Start).ToList();
            for (var i = 0; i < windows.Count; i++)
            {
                Assert.True(windows[i].Duration > 0f,
                    $"territory {territory}: lull at {windows[i].Start} has no length");
                if (i > 0)
                    Assert.True(windows[i].Start >= windows[i - 1].Start + windows[i - 1].Duration,
                        $"territory {territory}: lull at {windows[i].Start} overlaps the one before it");
            }
        }
    }
}
