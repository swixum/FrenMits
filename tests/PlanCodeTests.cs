using Xunit;

namespace FrenMits.Tests;

// A share code that decodes wrong hands someone else's friend a broken plan and
// nobody can tell why, so the round trip is pinned here.
public class PlanCodeTests
{
    private static FightProfile SampleFight()
    {
        var fight = new FightProfile
        {
            Name = "Dancing Mad (UMAD)",
            TerritoryId = Builtin.DmuTerritory,
            Category = "Ultimate",
            Slot = "T1",
            TimerOffset = 1.5f,
            TankPairing = "WAR/DRK",
        };
        fight.Lines.Add(Fx.Line(16f, "Revolting Ruin III", "Reprisal"));
        fight.Lines.Add(new MitLine
        {
            Time = 220f,
            Mechanic = "Ultimate Embrace",
            Action = "Holmgang",
            Jobs = new List<string> { "WAR" },
            Custom = true,
            OffsetSeconds = 2.5f,
            OffsetManual = true,
            CoverUntil = 231f,
            LeadOverride = 7f,
            Tts = "big one",
            Sound = false,
            Color = 0xFF00FF00,
            IconId = 12345,
        });
        fight.Notes.Add(new SheetNote { Time = 220f, Mechanic = "Ultimate Embrace", Text = "invuln here" });
        fight.DeletedCalls.Add(new DeletedCall { Slot = "T1", Time = 98f, Mechanic = "X", Action = "Rampart" });
        fight.SyncPoints.Add(new SyncPoint { Ability = 0xC3FD, Time = 25f, IsPhase = true, Label = "P1" });
        fight.BossAnchors.Add(new BossAnchor { NameId = 4242, Time = 0f, Label = "Kefka" });
        fight.CustomSlots.Add("T1");
        fight.CustomRows.Add(new CustomRow { Time = 16f, Mechanic = "Revolting Ruin III", Hurt = 3, Buster = true });
        fight.CustomDowntimes.Add(new DowntimeWindow { Start = 199f, Duration = 10f, TargetHp = 0.15f });
        return fight;
    }

    [Fact]
    public void RoundTripsEveryFieldThatMatters()
    {
        var original = SampleFight();
        var back = PlanCodes.Decode(PlanCodes.Encode(original));

        Assert.NotNull(back);
        Assert.Equal(original.Name, back!.Name);
        Assert.Equal(original.TerritoryId, back.TerritoryId);
        Assert.Equal(original.Category, back.Category);
        Assert.Equal(original.Slot, back.Slot);
        Assert.Equal(original.TimerOffset, back.TimerOffset);
        Assert.Equal(original.TankPairing, back.TankPairing);
        Assert.Equal(original.Lines.Count, back.Lines.Count);
        Assert.Equal(original.Notes.Count, back.Notes.Count);
        Assert.Equal(original.DeletedCalls.Count, back.DeletedCalls.Count);
        Assert.Equal(original.SyncPoints.Count, back.SyncPoints.Count);
        Assert.Equal(original.BossAnchors.Count, back.BossAnchors.Count);
        Assert.Equal(original.CustomSlots, back.CustomSlots);
        Assert.Equal(original.CustomRows.Count, back.CustomRows.Count);
        Assert.Equal(original.CustomDowntimes.Count, back.CustomDowntimes.Count);
    }

    [Fact]
    public void EveryPerLineTweakSurvives()
    {
        var original = SampleFight();
        var back = PlanCodes.Decode(PlanCodes.Encode(original))!;

        var a = original.Lines[1];
        var b = back.Lines[1];
        Assert.Equal(a.Time, b.Time);
        Assert.Equal(a.Mechanic, b.Mechanic);
        Assert.Equal(a.Action, b.Action);
        Assert.Equal(a.Jobs, b.Jobs);
        Assert.Equal(a.Custom, b.Custom);
        Assert.Equal(a.OffsetSeconds, b.OffsetSeconds);
        Assert.Equal(a.OffsetManual, b.OffsetManual);
        Assert.Equal(a.CoverUntil, b.CoverUntil);
        Assert.Equal(a.LeadOverride, b.LeadOverride);
        Assert.Equal(a.Tts, b.Tts);
        Assert.Equal(a.Sound, b.Sound);
        Assert.Equal(a.Color, b.Color);
        Assert.Equal(a.IconId, b.IconId);
    }

    [Fact]
    public void CodeCarriesTheGzipMarkerAndIsPasteable()
    {
        var code = PlanCodes.Encode(SampleFight());
        Assert.StartsWith("FRENMITS2:", code);
        Assert.DoesNotContain('\n', code);
        Assert.True(PlanCodes.LooksLikeCode(code));
        Assert.True(PlanCodes.LooksLikeCode("  " + code + "  "));
    }

    [Fact]
    public void LegacyPlainBase64CodesStillImport()
    {
        // FRENMITS1 codes are still in Discord history; they must keep working.
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(SampleFight());
        var legacy = "FRENMITS1:" + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

        var back = PlanCodes.Decode(legacy);

        Assert.NotNull(back);
        Assert.Equal("Dancing Mad (UMAD)", back!.Name);
        Assert.Equal(2, back.Lines.Count);
    }

    [Fact]
    public void CompressionActuallyShrinksARealPlan()
    {
        var fight = Fx.Builtin(Builtin.DmuTerritory, "T1");
        var raw = Newtonsoft.Json.JsonConvert.SerializeObject(fight).Length;
        Assert.True(PlanCodes.Encode(fight).Length < raw,
            "FRENMITS2 exists to make a full raid plan paste-friendly");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("just some chat text")]
    [InlineData("FRENMITS2:not-base64!!")]
    [InlineData("FRENMITS1:zzzz")]
    public void GarbageDecodesToNullInsteadOfThrowing(string? text)
    {
        Assert.Null(PlanCodes.Decode(text));
    }

    [Fact]
    public void OrderedLinesIsNotSerializedIntoTheCode()
    {
        // The derived sort is JsonIgnore'd so a code doesn't carry every line
        // twice; a regression here would roughly double every share code.
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(SampleFight());
        Assert.DoesNotContain("OrderedLines", json);
    }
}
