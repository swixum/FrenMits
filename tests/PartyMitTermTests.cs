using Xunit;

namespace FrenMits.Tests;

// "Party Mit" is a generic: the sheet writes it once and it reads as whatever the
// player looking at it actually presses. That only works for a role whose every
// job HAS one, and the caster role is the one that doesn't.
//
// Auto-plan used to hand the caster column "Party Mit" anyway. A Black Mage or
// Summoner got a call with no button behind it, and a Red Mage or Pictomancer got
// told to press Magick Barrier / Tempera Grassa - real abilities, but JobExtras
// with their own schedules, which the planner is not supposed to spend.
public class PartyMitTermTests
{
    private const string Term = "Party Mit";

    [Theory]
    [InlineData("WAR")] [InlineData("PLD")] [InlineData("DRK")] [InlineData("GNB")]
    [InlineData("BRD")] [InlineData("MCH")] [InlineData("DNC")]
    [InlineData("WHM")] [InlineData("AST")] [InlineData("SCH")] [InlineData("SGE")]
    public void EveryJobInARoleThatCarriesTheGenericCanResolveIt(string job)
    {
        // Tanks, phys ranged and healers: all four jobs of each have a party
        // mitigation, so the generic always names a real button.
        Assert.NotEqual(Term, Icons.DisplayAction(Term, job));
    }

    [Theory]
    [InlineData("BLM")] [InlineData("SMN")]
    public void ACasterHasNoPartyMitigationToResolveTo(string job)
    {
        // Nothing to press. This is why the caster pool in Auto-plan is Addle and
        // nothing else - a generic that can't resolve is a call the player stares
        // at during the mechanic.
        Assert.Equal(Term, Icons.DisplayAction(Term, job));
    }

    [Theory]
    [InlineData("MNK")] [InlineData("DRG")] [InlineData("NIN")]
    [InlineData("SAM")] [InlineData("RPR")] [InlineData("VPR")]
    public void AMeleeHasNoPartyMitigationEither(string job)
    {
        // Same reason melee columns plan Feint and nothing else.
        Assert.Equal(Term, Icons.DisplayAction(Term, job));
    }

    // The slot standard's columns and who sits in them.
    private static readonly Dictionary<string, JobRole> SlotRole = new(StringComparer.OrdinalIgnoreCase)
    {
        ["T1"] = JobRole.Tank, ["T2"] = JobRole.Tank,
        ["WHM"] = JobRole.Healer, ["AST"] = JobRole.Healer,
        ["SCH"] = JobRole.Healer, ["SGE"] = JobRole.Healer,
        ["M1"] = JobRole.Melee, ["M2"] = JobRole.Melee,
        ["R1"] = JobRole.PhysicalRanged, ["R2"] = JobRole.Caster,
    };

    [Theory]
    [MemberData(nameof(Territories))]
    public void NoBuiltinTellsAColumnToPressWhatItHasnt(ushort territory)
    {
        // The data side of the same rule. Auto-plan wrote bare "Party Mit" into the
        // caster column of M9S, M10S and M11S - thirteen calls a Black Mage could
        // only stare at - and an official sheet is not editable in game, so nobody
        // could take them out. The bake refuses them now; this is what says so.
        foreach (var slot in Builtin.Slots(territory))
        {
            if (!SlotRole.TryGetValue(slot, out var role)) continue;
            var jobs = Jobs.AbbreviationsForRole(role)
                .Where(j => !string.Equals(j, "BLU", StringComparison.Ordinal))  // not a raid job
                .ToList();

            foreach (var line in Builtin.BuildLines(territory, slot))
                foreach (var part in line.Action.Split('+', StringSplitOptions.TrimEntries
                                                           | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!string.Equals(part, Term, StringComparison.OrdinalIgnoreCase)) continue;
                    var stuck = jobs.Where(j => Icons.DisplayAction(part, j) == part).ToList();
                    Assert.True(stuck.Count == 0,
                        $"{Builtin.Name(territory)} {slot} at {line.Time}s calls \"{part}\", "
                        + $"which {string.Join("/", stuck)} cannot press.");
                }
        }
    }

    public static TheoryData<ushort> Territories()
    {
        var d = new TheoryData<ushort>();
        foreach (var (territory, _, _, _) in Builtin.Fights) d.Add(territory);
        return d;
    }
}
