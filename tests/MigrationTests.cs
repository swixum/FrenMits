using Xunit;

namespace FrenMits.Tests;

// The versioned config chain. Someone who hasn't launched in months runs a dozen
// of these back to back on their real saved plans, and a mistake is unrecoverable
// by the time they notice - so the chain is replayed here against configs shaped
// the way each era's really were.
//
// The pre-v8 fixtures are built from DmuLegacy, the FROZEN previous bake the repo
// keeps for exactly this purpose, so they carry the same lines a user's config
// actually held at that version.
public class MigrationTests
{
    private static FakeMigrationHost Run(Configuration config)
    {
        var host = new FakeMigrationHost(config);
        ConfigMigrations.Run(host);
        return host;
    }

    // Whatever the chain currently ends on, read from the chain itself so adding a
    // v24 doesn't mean editing a magic number in here.
    private static readonly int Latest = LatestVersion();

    private static int LatestVersion()
    {
        var config = new Configuration { Version = 1 };
        ConfigMigrations.Run(new FakeMigrationHost(config));
        return config.Version;
    }

    [Fact]
    public void EveryStartingVersionLandsOnTheSamePlace()
    {
        // No version may get stuck partway (a missing bump would leave a config
        // re-running its migration on every single launch).
        Assert.True(Latest >= 23, $"the chain only reaches v{Latest}; did a migration get dropped?");
        for (var from = 1; from <= Latest; from++)
        {
            var config = Fx.ConfigAt(from, Fx.LegacyDmu());
            Run(config);
            Assert.Equal(Latest, config.Version);
        }
    }

    [Fact]
    public void RunningTheChainTwiceChangesNothingTheSecondTime()
    {
        var config = Fx.ConfigAt(7, Fx.LegacyDmu());
        Run(config);
        var after = Snapshot(config);

        var host = Run(config);

        Assert.Equal(after, Snapshot(config));
        Assert.Equal(0, host.ResetAllCalls);
        Assert.Empty(host.Snapshots);
    }

    [Fact]
    public void AnAlreadyCurrentConfigIsUntouched()
    {
        var config = Fx.ConfigAt(Latest, Fx.LegacyDmu());
        var before = Snapshot(config);

        var host = Run(config);

        Assert.Equal(before, Snapshot(config));
        Assert.Equal(0, host.ResetAllCalls);
    }

    // v5 and v11..v14 are deliberate CLEAN resets of Dancing Mad ("wiping any
    // custom lines too", per v11). So a config old enough to still be owed one of
    // those loses its custom DMU lines by design, and anything newer must keep
    // them. Both halves are pinned, because the boundary is the whole point: it
    // decides whether a returning raider's own notes survive an update.
    [Theory]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(18)]
    [InlineData(22)]
    public void CustomLinesSurviveOnceThePlannedResetsAreBehindYou(int from)
    {
        var config = Fx.ConfigAt(from, Fx.LegacyDmu("MT",
            Fx.Line(505f, "My own reminder", "Second Wind"),
            Fx.Line(742f, "Watch the adds", "Sprint")));

        Run(config);

        var fight = config.Fights[0];
        Assert.Contains(fight.Lines, l => l.Mechanic == "My own reminder");
        Assert.Contains(fight.Lines, l => l.Mechanic == "Watch the adds");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(13)]
    public void TheDeliberateResetsClearCustomDmuLinesAsDesigned(int from)
    {
        var config = Fx.ConfigAt(from, Fx.LegacyDmu("MT", Fx.Line(505f, "My own reminder", "Second Wind")));

        Run(config);

        var fight = config.Fights[0];
        Assert.DoesNotContain(fight.Lines, l => l.Mechanic == "My own reminder");
        Assert.NotEmpty(fight.Lines);   // and the fight is left freshly baked, not empty
    }

    [Fact]
    public void CustomLinesOnOtherFightsAreNeverCaughtByTheDmuResets()
    {
        // The resets are scoped to Dancing Mad; a note on any other fight has to
        // come through every version untouched.
        var other = new FightProfile { TerritoryId = Builtin.TopTerritory, Name = "TOP", Slot = "T1" };
        other.Lines.Add(Fx.Line(505f, "My own reminder", "Second Wind"));
        other.SavedSlots["T1"] = other.Lines;
        var config = Fx.ConfigAt(7, other);

        Run(config);

        Assert.Contains(config.Fights[0].Lines, l => l.Mechanic == "My own reminder");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(16)]
    [InlineData(22)]
    public void TheChainNeverLeavesDuplicateCalls(int from)
    {
        var config = Fx.ConfigAt(from, Fx.LegacyDmu("MT", Fx.Line(505f, "Mine", "Second Wind")));

        Run(config);

        foreach (var f in config.Fights)
            foreach (var (slot, lines) in AllSlots(f))
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var l in lines)
                    Assert.True(seen.Add($"{l.Time:0.###}|{l.Mechanic.Trim()}|{l.Action.Trim()}"),
                        $"from v{from}, slot {slot}: '{l.Action}' at {l.Time} ended up in the plan twice");
            }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(16)]
    public void TheChainNeverLeavesTheSameMitStackedOnItself(int from)
    {
        // Two presses of one mit within a few seconds is the shape the "hardened
        // de-overlap" in v9 was added to clear; it must not come back.
        var config = Fx.ConfigAt(from, Fx.LegacyDmu());

        Run(config);

        foreach (var (slot, lines) in AllSlots(config.Fights[0]))
        {
            var ordered = lines.Where(l => !string.IsNullOrWhiteSpace(l.Action)).OrderBy(l => l.Time).ToList();
            for (var i = 1; i < ordered.Count; i++)
                Assert.False(
                    MathF.Abs(ordered[i].Time - ordered[i - 1].Time) < 6f
                    && string.Equals(ordered[i].Action.Trim(), ordered[i - 1].Action.Trim(),
                        StringComparison.OrdinalIgnoreCase),
                    $"from v{from}, slot {slot}: '{ordered[i].Action}' fires twice around {ordered[i].Time}");
        }
    }

    [Fact]
    public void PlansStaySortedThroughTheChain()
    {
        var config = Fx.ConfigAt(7, Fx.LegacyDmu("MT", Fx.Line(505f, "Mine", "Second Wind")));

        Run(config);

        foreach (var (slot, lines) in AllSlots(config.Fights[0]))
            for (var i = 1; i < lines.Count; i++)
                Assert.True(lines[i].Time >= lines[i - 1].Time, $"slot {slot} came out unsorted");
    }

    [Fact]
    public void TheActiveSlotStaysAliasedIntoItsStash()
    {
        var config = Fx.ConfigAt(7, Fx.LegacyDmu());
        Run(config);

        var fight = config.Fights[0];
        Assert.False(string.IsNullOrEmpty(fight.Slot));
        Assert.True(fight.SavedSlots.ContainsKey(fight.Slot));
    }

    [Fact]
    public void V3GivesEveryFightASidebarCategory()
    {
        var config = Fx.ConfigAt(2,
            new FightProfile { TerritoryId = Builtin.DmuTerritory, Name = "UMAD" },
            new FightProfile { TerritoryId = 12345, Name = "Something custom" });

        Run(config);

        Assert.Equal("Ultimate", config.Fights[0].Category);
        Assert.Equal("Other", config.Fights[1].Category);
    }

    [Fact]
    public void V15ReplacesEmDashesTheGameFontCannotRender()
    {
        var config = Fx.ConfigAt(14, new FightProfile { Name = "M12S — Lindwurm", TerritoryId = 12345 });
        Run(config);
        Assert.Equal("M12S - Lindwurm", config.Fights[0].Name);
        Assert.DoesNotContain('—', config.Fights[0].Name);
    }

    [Fact]
    public void V20MovesTheOldM12sPlaceholderZone()
    {
        var config = Fx.ConfigAt(19, new FightProfile { TerritoryId = 1320, Name = "M12S", Category = "Other" });

        Run(config);

        Assert.Equal(Builtin.M12sTerritory, config.Fights[0].TerritoryId);
        Assert.Equal("Savage", config.Fights[0].Category);
    }

    [Fact]
    public void V21TurnsAutoCooldownTimingOffOnce()
    {
        var config = Fx.ConfigAt(20);
        config.AutoCooldownTiming = true;
        Run(config);
        Assert.False(config.AutoCooldownTiming);
    }

    [Fact]
    public void V22GivesImportedSummonerCallsTheirVoice()
    {
        var summons = new MitLine
        {
            Time = 100, Mechanic = "Adds", Action = "Garuda/Titan/Ifrit",
            Jobs = new List<string> { "SMN" }, Custom = true, Sound = false,
        };
        var fight = new FightProfile { TerritoryId = 12345 };
        fight.Lines.Add(summons);
        var config = Fx.ConfigAt(21, fight);

        Run(config);

        Assert.True(summons.Sound);
        Assert.Equal("Garuda, Titan, Ifrit", summons.Tts);
    }

    [Fact]
    public void V22LeavesOtherPeoplesCallsAlone()
    {
        var muted = new MitLine
        {
            Time = 100, Mechanic = "Adds", Action = "Reprisal",
            Jobs = new List<string> { "WAR" }, Custom = true, Sound = false,
        };
        var fight = new FightProfile { TerritoryId = 12345 };
        fight.Lines.Add(muted);
        var config = Fx.ConfigAt(21, fight);

        Run(config);

        Assert.False(muted.Sound);
        Assert.Equal("", muted.Tts);
    }

    [Fact]
    public void ARebakeSnapshotsTheFightItIsAboutToTouch()
    {
        // v16..v19 stash a restorable copy first; losing that is how a bad rebake
        // becomes permanent.
        var config = Fx.ConfigAt(15, Fx.LegacyDmu());
        var host = Run(config);
        Assert.NotEmpty(host.Snapshots);
    }

    [Fact]
    public void ConfigsWithNoFightsMigrateCleanly()
    {
        var config = Fx.ConfigAt(1);
        Run(config);
        Assert.Equal(Latest, config.Version);
        Assert.Empty(config.Fights);
    }

    [Fact]
    public void ACustomSheetIsNeverTouchedByTheDmuRebakes()
    {
        var custom = new FightProfile { TerritoryId = 4242, Name = "My own fight", Slot = "T1" };
        custom.CustomSlots.Add("T1");
        custom.Lines.Add(Fx.Line(30, "Big one", "Reprisal"));
        custom.SavedSlots["T1"] = custom.Lines;
        var config = Fx.ConfigAt(7, custom);

        Run(config);

        var line = Assert.Single(config.Fights[0].Lines);
        Assert.Equal("Big one", line.Mechanic);
        Assert.Equal("Reprisal", line.Action);
    }

    // Every plan a fight holds: the live one plus each column's stash.
    private static IEnumerable<(string Slot, List<MitLine> Lines)> AllSlots(FightProfile fight)
    {
        yield return ("(active)", fight.Lines);
        foreach (var (slot, lines) in fight.SavedSlots) yield return (slot, lines);
    }

    private static string Snapshot(Configuration config)
        => string.Join("\n", config.Fights.Select(f =>
            $"{f.Name}|{f.TerritoryId}|{f.Slot}|{f.Category}|" +
            string.Join(";", f.Lines.Select(l => $"{l.Time:0.###}:{l.Mechanic}:{l.Action}:{l.Sound}:{l.OffsetSeconds}"))));
}
