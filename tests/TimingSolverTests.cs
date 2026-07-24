using Xunit;

namespace FrenMits.Tests;

// The cooldown-aware offset solver. Its whole job is to press early enough to
// cover a run of hits while keeping the recast ready for the next one, so the
// tests assert those invariants rather than exact numbers.
public class TimingSolverTests
{
    // A fixed mit table, so the solver can be exercised without the game's Action
    // sheet. Names, recasts and durations match the real 7.x values.
    private static readonly Dictionary<string, Cooldowns.PlanMit> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Reprisal"] = new("Reprisal", 60f, 1, "", 22, 15f),
        ["Rampart"] = new("Rampart", 90f, 1, "", 8, 20f),
        ["Feint"] = new("Feint", 90f, 1, "", 22, 15f),
        ["Sacred Soil"] = new("Sacred Soil", 30f, 1, "", 50, 15f),
        ["Holmgang"] = new("Holmgang", 240f, 1, "", 42, 10f),
    };

    private static IEnumerable<Cooldowns.PlanMit> MitsFor(string action)
    {
        foreach (var kv in Table)
            if (action.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                yield return kv.Value;
    }

    private static FightProfile FightWith(params MitLine[] lines)
    {
        var f = new FightProfile { TerritoryId = 9999 };
        f.Lines.AddRange(lines);
        return f;
    }

    private static int Solve(FightProfile f, IReadOnlyList<float> hits, float lead = 5f)
        => TimingSolver.Solve(f, hits, lead, MitsFor);

    [Fact]
    public void NoHitsMeansNoChanges()
        => Assert.Equal(0, Solve(FightWith(Fx.Line(100, "A", "Reprisal")), Array.Empty<float>()));

    [Fact]
    public void AnUntrackedActionIsLeftAlone()
    {
        var line = Fx.Line(100, "A", "Some note to myself");
        Solve(FightWith(line), new[] { 100f });
        Assert.Equal(0f, line.OffsetSeconds);
    }

    [Fact]
    public void APressIsNeverScheduledAfterItsOwnHit()
    {
        // A negative offset would mean "press it once the hit has already landed".
        var lines = new[]
        {
            Fx.Line(100, "A", "Reprisal"), Fx.Line(200, "B", "Reprisal"), Fx.Line(260, "C", "Rampart"),
        };
        var f = FightWith(lines);
        Solve(f, new[] { 100f, 200f, 260f });

        foreach (var l in lines)
            Assert.True(l.OffsetSeconds >= 0f, $"'{l.Action}' was scheduled {l.OffsetSeconds}s after its hit");
    }

    [Fact]
    public void ASolvedPressIsNeverPulledInFrontOfItsCooldown()
    {
        // Where the solver DOES move a press, it may never move it earlier than
        // the recast allows. (A call it can't make work is left alone - see
        // AnImpossiblePressIsLeftOnItsSheetTime.)
        var lines = new[]
        {
            Fx.Line(100, "A", "Reprisal"), Fx.Line(200, "B", "Reprisal"),
            Fx.Line(300, "C", "Reprisal"), Fx.Line(420, "D", "Rampart"),
        };
        var f = FightWith(lines);
        Solve(f, new[] { 100f, 200f, 300f, 420f });

        var readyAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in f.Lines.OrderBy(l => l.Time))
        {
            var press = l.Time - l.OffsetSeconds;
            foreach (var m in MitsFor(l.Action))
            {
                if (l.OffsetSeconds != 0f && readyAt.TryGetValue(m.Name, out var ready))
                    Assert.True(press >= ready - 0.05f,
                        $"'{l.Action}' solved to a press at {press}, but it was only back at {ready}");
                readyAt[m.Name] = press + m.Recast;
            }
        }
    }

    [Fact]
    public void AnImpossiblePressIsLeftOnItsSheetTime()
    {
        // Two Reprisals 40s apart on a 60s recast: the second one simply cannot
        // be ready. The solver declines rather than inventing an offset that
        // would read as "this is handled" - the cooldown-aware call flags it
        // in game instead.
        var first = Fx.Line(100, "A", "Reprisal");
        var second = Fx.Line(140, "B", "Reprisal");
        var f = FightWith(first, second);

        Solve(f, new[] { 100f, 140f });

        Assert.True(first.OffsetSeconds > 0f);
        Assert.Equal(0f, second.OffsetSeconds);
        Assert.Equal(0f, second.CoverUntil);
    }

    [Fact]
    public void ThePressStillCoversTheHitItIsPlannedFor()
    {
        var line = Fx.Line(100, "A", "Reprisal");
        var f = FightWith(line);
        Solve(f, new[] { 100f });

        var press = line.Time - line.OffsetSeconds;
        Assert.True(press <= line.Time, "pressed after the hit");
        Assert.True(press + Table["Reprisal"].Duration >= line.Time, "buff had faded by the hit");
    }

    [Fact]
    public void OnePressIsStretchedAcrossAClusterOfHits()
    {
        // Three hits inside one 15s Reprisal: the press should be pulled back so
        // the buff blankets the whole run, and CoverUntil should name the last one.
        var line = Fx.Line(100, "A", "Reprisal");
        var f = FightWith(line);
        Solve(f, new[] { 100f, 106f, 112f });

        var press = line.Time - line.OffsetSeconds;
        Assert.True(press + Table["Reprisal"].Duration >= 112f - 0.05f,
            "the buff didn't reach the last hit of the run");
        Assert.Equal(112f, line.CoverUntil, 1);
    }

    [Fact]
    public void ASingleHitNeedsNoCoverageWindow()
    {
        var line = Fx.Line(100, "A", "Reprisal");
        Solve(FightWith(line), new[] { 100f });
        Assert.Equal(0f, line.CoverUntil);
    }

    [Fact]
    public void AHandTimedPressIsNeverMoved()
    {
        var manual = new MitLine
        {
            Time = 100, Mechanic = "A", Action = "Reprisal",
            OffsetSeconds = 9f, OffsetManual = true,
        };
        Solve(FightWith(manual), new[] { 100f, 106f, 112f });
        Assert.Equal(9f, manual.OffsetSeconds);
    }

    [Fact]
    public void AHandTimedPressStillBooksItsHitsAndItsRecast()
    {
        // The manual press covers 100 and 104 and puts Reprisal on cooldown until
        // 155, so the solver must not pull the following Reprisal back on top of
        // it. With no legal spot it leaves that one on its sheet time.
        var manual = new MitLine
        {
            Time = 100, Mechanic = "A", Action = "Reprisal", OffsetSeconds = 5f, OffsetManual = true,
        };
        var next = Fx.Line(104, "B", "Reprisal");
        var f = FightWith(manual, next);

        Solve(f, new[] { 100f, 104f });

        Assert.Equal(5f, manual.OffsetSeconds);
        Assert.Equal(0f, next.OffsetSeconds);
    }

    [Fact]
    public void DisabledLinesAreSkipped()
    {
        var off = new MitLine { Time = 100, Mechanic = "A", Action = "Reprisal", Enabled = false };
        Solve(FightWith(off), new[] { 100f });
        Assert.Equal(0f, off.OffsetSeconds);
    }

    [Fact]
    public void SolvingTwiceIsAFixedPoint()
    {
        // The auto-timer re-runs on every slot change and every zone-in; if it
        // drifted a little each pass, a plan would creep out of shape.
        var f = FightWith(Fx.Line(100, "A", "Reprisal"), Fx.Line(200, "B", "Rampart"), Fx.Line(260, "C", "Feint"));
        var hits = new[] { 100f, 106f, 200f, 205f, 260f };

        Solve(f, hits);
        var snapshot = f.Lines.Select(l => (l.OffsetSeconds, l.CoverUntil)).ToList();

        Assert.Equal(0, Solve(f, hits));
        Assert.Equal(snapshot, f.Lines.Select(l => (l.OffsetSeconds, l.CoverUntil)).ToList());
    }

    [Fact]
    public void OffsetsLandOnATenthOfASecond()
    {
        var f = FightWith(Fx.Line(100, "A", "Reprisal"), Fx.Line(103, "B", "Rampart"));
        Solve(f, new[] { 100f, 103f, 109f });
        foreach (var l in f.Lines)
            Assert.Equal(l.OffsetSeconds, MathF.Round(l.OffsetSeconds * 10f) / 10f, 3);
    }

    [Fact]
    public void HitTimesNeedNotArriveSorted()
    {
        var sorted = FightWith(Fx.Line(100, "A", "Reprisal"));
        var jumbled = FightWith(Fx.Line(100, "A", "Reprisal"));

        Solve(sorted, new[] { 100f, 106f, 112f });
        Solve(jumbled, new[] { 112f, 100f, 106f });

        Assert.Equal(sorted.Lines[0].OffsetSeconds, jumbled.Lines[0].OffsetSeconds);
        Assert.Equal(sorted.Lines[0].CoverUntil, jumbled.Lines[0].CoverUntil);
    }
}
