using Newtonsoft.Json;
using Xunit;

namespace FrenMits.Tests;

// Fight plans moved out of the config file into their own.
//
// This is the one change in the plugin where a mistake costs somebody every plan
// they have ever written, so the whole contract is pinned here: what the config
// stops writing, what it still READS so an existing profile hands its fights
// over, and that a line survives the round trip now that most of its fields are
// no longer written at all.
public class PlanStoreTests
{
    private static Configuration WithFights()
    {
        var c = new Configuration();
        var f = new FightProfile { Name = "Dancing Mad (UMAD)", TerritoryId = Builtin.DmuTerritory };
        f.Lines.Add(new MitLine { Time = 221f, Mechanic = "Ultimate Embrace", Action = "Reprisal" });
        f.SavedSlots["T1"] = f.Lines;
        c.Fights.Add(f);
        return c;
    }

    // ---- what the config file now holds -------------------------------------

    [Fact]
    public void TheConfigStillSerializesAtAll()
    {
        // An ignored Fights sat next to a [JsonProperty("Fights")] LegacyFights is
        // the sort of thing Newtonsoft rejects as a duplicate name. It doesn't -
        // but if that ever changed, every save in the plugin would throw, so this
        // is worth a test of its own.
        var json = JsonConvert.SerializeObject(WithFights());
        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("\"Version\"", json);
    }

    [Fact]
    public void PlansStayOutEvenThroughDalamudsOwnSerializer()
    {
        // SavePluginConfig doesn't use bare JsonConvert - it stamps $type on every
        // object (TypeNameHandling.Objects), which is where the config's "$type":
        // "FrenMits.MitLine" on all 1770 lines came from. That's the serializer
        // that actually runs in the game, so the contract is pinned against it
        // rather than only against the defaults every other test here uses.
        var json = JsonConvert.SerializeObject(WithFights(), Formatting.Indented,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects });
        Assert.DoesNotContain("Ultimate Embrace", json);
        Assert.DoesNotContain("SavedSlots", json);
        Assert.Contains("\"Version\"", json);

        var back = JsonConvert.DeserializeObject<Configuration>(json)!;
        Assert.Empty(back.Fights);
        Assert.Null(back.LegacyFights);
    }

    [Fact]
    public void PlansAreNoLongerWrittenIntoTheConfig()
    {
        var json = JsonConvert.SerializeObject(WithFights());
        Assert.DoesNotContain("Ultimate Embrace", json);
        Assert.DoesNotContain("SavedSlots", json);
        Assert.DoesNotContain("\"Fights\"", json);
    }

    [Fact]
    public void SettingsAreStillWrittenIntoTheConfig()
    {
        var c = WithFights();
        c.PrepCheckWarnMinutes = 7f;
        c.UpcomingBoardPhases = true;
        var back = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(c))!;
        Assert.Equal(7f, back.PrepCheckWarnMinutes);
        Assert.True(back.UpcomingBoardPhases);
    }

    [Fact]
    public void DroppingPlansMakesTheConfigDramaticallySmaller()
    {
        // The whole point. A real profile was 718KB of plans against 6KB of
        // settings, and Save() runs from a hundred places.
        var c = new Configuration();
        foreach (var (t, _, _) in Builtin.Fights)
        {
            var f = Fx.Builtin(t, Builtin.Slots(t)[0]);
            foreach (var s in Builtin.Slots(t)) f.SavedSlots[s] = Builtin.BuildLines(t, s);
            c.Fights.Add(f);
        }
        var settings = JsonConvert.SerializeObject(c).Length;
        var plans = JsonConvert.SerializeObject(c.Fights).Length;
        Assert.True(settings < 20_000, $"the config alone should be tiny, got {settings} bytes");
        Assert.True(plans > settings * 5, $"plans ({plans}) should dwarf settings ({settings})");
    }

    // ---- handing the old ones over ------------------------------------------

    [Fact]
    public void AConfigFromBeforeTheSplitStillYieldsItsFights()
    {
        // Exactly the shape every existing profile on disk has right now. If this
        // regressed, everyone's plans would silently vanish on update.
        const string legacy = """
        {
          "Version": 23,
          "Fights": [
            { "Name": "Dancing Mad (UMAD)", "TerritoryId": 1363,
              "Lines": [ { "Time": 221.0, "Mechanic": "Ultimate Embrace", "Action": "Reprisal" } ] }
          ]
        }
        """;
        var c = JsonConvert.DeserializeObject<Configuration>(legacy)!;
        Assert.NotNull(c.LegacyFights);
        var f = Assert.Single(c.LegacyFights!);
        Assert.Equal("Dancing Mad (UMAD)", f.Name);
        Assert.Equal("Ultimate Embrace", Assert.Single(f.Lines).Mechanic);
        // Fights itself stays empty: the plugin moves them across at load.
        Assert.Empty(c.Fights);
    }

    [Fact]
    public void TheHandoverIsNeverWrittenBack()
    {
        // Once moved, LegacyFights must not reappear in the file - otherwise the
        // config keeps both copies and nothing was saved at all.
        var c = new Configuration { LegacyFights = new List<FightProfile> { new() { Name = "LeftoverFightName" } } };
        Assert.DoesNotContain("LeftoverFightName", JsonConvert.SerializeObject(c));
    }

    [Fact]
    public void AConfigWithNoFightsKeyIsAFreshInstallNotAMigration()
    {
        var c = JsonConvert.DeserializeObject<Configuration>("""{"Version":23}""")!;
        Assert.Null(c.LegacyFights);
    }

    // ---- which copy wins ----------------------------------------------------

    [Fact]
    public void TheFirstLoadAfterTheSplitTakesTheConfigsCopy()
        => Assert.True(PlanStore.PreferConfigCopy(planFileExists: false, legacyCount: 11, configIsNewer: false));

    [Fact]
    public void OnceTheresAPlanFileTheConfigsLeftoverIsIgnored()
        => Assert.False(PlanStore.PreferConfigCopy(planFileExists: true, legacyCount: 11, configIsNewer: false));

    [Fact]
    public void ADowngradeThatEditedPlansIsPickedUpOnTheWayBack()
    {
        // From this version the config never writes fights, so a config that HAS
        // them and was written more recently than the plan file can only be an
        // older build's work - and its copy is the newer one.
        Assert.True(PlanStore.PreferConfigCopy(planFileExists: true, legacyCount: 11, configIsNewer: true));
    }

    [Fact]
    public void AnEmptyLegacyListNeverWinsOverAPlanFile()
    {
        // "I deleted all my fights" must not be mistaken for "there is nothing
        // here yet", in either direction.
        Assert.False(PlanStore.PreferConfigCopy(planFileExists: true, legacyCount: 0, configIsNewer: true));
        Assert.False(PlanStore.PreferConfigCopy(planFileExists: false, legacyCount: 0, configIsNewer: false));
    }

    // ---- the slimmed line ---------------------------------------------------

    [Fact]
    public void ALineAtItsDefaultsWritesAlmostNothing()
    {
        var json = JsonConvert.SerializeObject(new MitLine { Time = 100f, Mechanic = "Raidwide", Action = "Reprisal" });
        foreach (var gone in new[] { "Enabled", "Sound", "LeadOverride", "Color", "IconId",
                                     "OffsetSeconds", "OffsetManual", "CoverUntil", "Jobs", "Custom", "Tts" })
            Assert.DoesNotContain($"\"{gone}\"", json);
    }

    [Fact]
    public void DerivedFieldsAreNeverWritten()
    {
        // Both are get-only, so they could never be read back anyway - they were
        // just riding along in the config, every plan code and every snapshot.
        var json = JsonConvert.SerializeObject(new MitLine { Time = 100f, OffsetSeconds = 3f });
        Assert.DoesNotContain("CueTime", json);
        Assert.DoesNotContain("TimeText", json);
    }

    [Fact]
    public void ADefaultedLineComesBackWithItsDefaults()
    {
        // The trap: Enabled and Sound default to TRUE, so skipping them when true
        // is only safe because the field initializers put them back.
        var line = new MitLine { Time = 100f, Mechanic = "Raidwide", Action = "Reprisal" };
        var back = JsonConvert.DeserializeObject<MitLine>(JsonConvert.SerializeObject(line))!;
        Assert.True(back.Enabled);
        Assert.True(back.Sound);
        Assert.Empty(back.Jobs);
        Assert.Equal(0f, back.LeadOverride);
        Assert.Equal(100f, back.Time);
    }


    [Fact]
    public void EveryNonDefaultValueSurvivesTheRoundTrip()
    {
        var line = new MitLine
        {
            Time = 221f, Mechanic = "Ultimate Embrace", Action = "Reprisal",
            Jobs = new List<string> { "WAR", "DRK" },
            Enabled = false, Custom = true, OffsetSeconds = 2.5f, OffsetManual = true,
            CoverUntil = 240f, LeadOverride = 4f, Tts = "shield the tank",
            Sound = false, Color = 0xFF00FF00, IconId = 12345,
        };
        var back = JsonConvert.DeserializeObject<MitLine>(JsonConvert.SerializeObject(line))!;
        Assert.Equal(line.Time, back.Time);
        Assert.Equal(line.Mechanic, back.Mechanic);
        Assert.Equal(line.Action, back.Action);
        Assert.Equal(line.Jobs, back.Jobs);
        Assert.False(back.Enabled);
        Assert.True(back.Custom);
        Assert.Equal(line.OffsetSeconds, back.OffsetSeconds);
        Assert.True(back.OffsetManual);
        Assert.Equal(line.CoverUntil, back.CoverUntil);
        Assert.Equal(line.LeadOverride, back.LeadOverride);
        Assert.Equal(line.Tts, back.Tts);
        Assert.False(back.Sound);
        Assert.Equal(line.Color, back.Color);
        Assert.Equal(line.IconId, back.IconId);
        // Derived, but must still compute correctly on the far side.
        Assert.Equal(218.5f, back.CueTime, 3);
    }

    [Fact]
    public void AWholePlanSurvivesTheRoundTripIntact()
    {
        foreach (var (t, _, _) in Builtin.Fights)
        {
            var f = Fx.Builtin(t, Builtin.Slots(t)[0]);
            foreach (var s in Builtin.Slots(t)) f.SavedSlots[s] = Builtin.BuildLines(t, s);

            var back = JsonConvert.DeserializeObject<FightProfile>(JsonConvert.SerializeObject(f))!;
            Assert.Equal(f.Name, back.Name);
            Assert.Equal(f.TerritoryId, back.TerritoryId);
            Assert.Equal(f.Lines.Count, back.Lines.Count);
            Assert.Equal(f.SavedSlots.Count, back.SavedSlots.Count);
            for (var i = 0; i < f.Lines.Count; i++)
            {
                Assert.Equal(f.Lines[i].Time, back.Lines[i].Time);
                Assert.Equal(f.Lines[i].Action, back.Lines[i].Action);
                Assert.Equal(f.Lines[i].Enabled, back.Lines[i].Enabled);
                Assert.Equal(f.Lines[i].Sound, back.Lines[i].Sound);
            }
            foreach (var (slot, lines) in f.SavedSlots)
                Assert.Equal(lines.Count, back.SavedSlots[slot].Count);
        }
    }

    [Fact]
    public void APlanCodeStillRoundTripsAfterTheSlimming()
    {
        // Plan codes serialize a whole fight, so the ShouldSerialize rules reach
        // every code anybody shares.
        var f = Fx.Builtin(Builtin.DmuTerritory, "T1");
        var code = PlanCodes.Encode(f);
        var back = PlanCodes.Decode(code);
        Assert.NotNull(back);
        Assert.Equal(f.Lines.Count, back!.Lines.Count);
        Assert.All(back.Lines, l => Assert.True(l.Enabled));
    }
}
