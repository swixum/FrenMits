using System.Reflection;
using Xunit;

namespace FrenMits.Tests;

// Adding an official fight means editing Builtin.cs in eight separate places, and
// the ones that are easy to forget are the ones nothing else notices: a fight whose
// Data class HAS severity grades or phase names, which simply never get served
// because no case was added for it.
//
// BuiltinSheetTests already re-runs a dozen invariants for every entry in
// Builtin.Fights, so the shape of a new fight is covered the moment it lands in
// that array. What's here is the wiring around it: the accessors that a new fight
// silently falls out of.
public class NewFightWiringTests
{
    private static IEnumerable<Type> DataClasses()
        => typeof(Builtin).Assembly.GetTypes()
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed          // static class
                        && t.Name.EndsWith("Data", StringComparison.Ordinal)
                        && !t.Name.Contains("Legacy", StringComparison.Ordinal));

    private static T? CallStatic<T>(Type t, string method) where T : class
    {
        var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
        return m?.Invoke(null, null) as T;
    }

    [Fact]
    public void ADataClassWithGradesIsActuallyServedToTheBoard()
    {
        // Hurt/Buster reach the board only through Builtin.CustomRows. A fight can
        // carry a fully graded timeline and still draw every row as a plain hit,
        // with nothing failing, if that one case is missing.
        foreach (var t in DataClasses())
        {
            var own = CallStatic<List<CustomRow>>(t, "CustomRows");
            if (own is not { Count: > 0 }) continue;

            var served = Builtin.Fights.Any(f => Builtin.CustomRows(f.Territory).Count == own.Count);
            Assert.True(served,
                $"{t.Name}.CustomRows() has {own.Count} graded rows that nothing serves. "
                + "Add a case to Builtin.CustomRows(territory).");
        }
    }

    [Fact]
    public void ADataClassWithPhasesIsActuallyServedToTheBoard()
    {
        // Same shape of mistake for the phase dividers: PhaseStarts() exists on the
        // generated class, but SheetTimeline.PhaseMarks needs to know about it.
        foreach (var t in DataClasses())
        {
            var own = CallStatic<List<(string Name, float Time)>>(t, "PhaseStarts");
            if (own is not { Count: > 0 }) continue;

            var served = Builtin.Fights.Any(f =>
                SheetTimeline.PhaseMarks(Fx.Builtin(f.Territory, Builtin.Slots(f.Territory)[0])).Count == own.Count);
            Assert.True(served,
                $"{t.Name}.PhaseStarts() has {own.Count} phase(s) that nothing serves. "
                + "Add a case to SheetTimeline.PhaseMarks.");
        }
    }

    [Theory]
    [MemberData(nameof(Territories))]
    public void AGradedFightKeepsItsGradesThroughEveryLoadPath(ushort territory)
    {
        // ApplySlot and ResetSlot are the only two ways a built-in's data is
        // refreshed, and every path in the plugin goes through one of them. A fight
        // wired into just one would lose its grades depending on how you got there.
        if (Builtin.CustomRows(territory).Count == 0) return;

        var slot = Builtin.Slots(territory)[0];

        var viaApply = new FightProfile { TerritoryId = territory };
        Builtin.ApplySlot(viaApply, slot);
        Assert.True(viaApply.CustomRows.Count > 0, "ApplySlot dropped the grades");

        var viaReset = new FightProfile { TerritoryId = territory };
        Builtin.ResetSlot(viaReset, slot);
        Assert.True(viaReset.CustomRows.Count > 0, "ResetSlot dropped the grades");

        Assert.Equal(viaApply.CustomRows.Count, viaReset.CustomRows.Count);
    }

    [Theory]
    [MemberData(nameof(Territories))]
    public void ServingGradesNeverWipesAUsersOwnRows(ushort territory)
    {
        // The grades are pushed onto FightProfile.CustomRows, which is also where a
        // user's hand-graded custom sheet keeps its own. Reloading a built-in must
        // never reach across and blank someone else's work.
        var custom = new FightProfile { TerritoryId = 999_999 };
        custom.CustomRows.Add(new CustomRow { Time = 10f, Mechanic = "Mine", Hurt = 3 });
        Builtin.ApplySlot(custom, Builtin.Slots(territory)[0]);
        Assert.Single(custom.CustomRows);
        Assert.Equal("Mine", custom.CustomRows[0].Mechanic);
    }

    [Fact]
    public void EveryFightInTheListAnswersEveryAccessor()
    {
        // A fight added to Fights[] but missed in one of the switches shows up here
        // rather than as an empty board in game.
        foreach (var (territory, name, category, _) in Builtin.Fights)
        {
            Assert.True(Builtin.Has(territory), $"{name}: missing from Has()");
            Assert.False(string.IsNullOrWhiteSpace(Builtin.Name(territory)), $"{name}: missing from Name()");
            Assert.False(string.IsNullOrWhiteSpace(category), $"{name}: no category");
            Assert.NotNull(Builtin.SyncPoints(territory));
            Assert.NotNull(Builtin.BossAnchors(territory));
            Assert.NotNull(Builtin.CustomRows(territory));
            Assert.NotEmpty(Builtin.BuildLines(territory, Builtin.Slots(territory)[0]));
        }
    }

    public static TheoryData<ushort> Territories()
    {
        var d = new TheoryData<ushort>();
        foreach (var (territory, _, _, _) in Builtin.Fights) d.Add(territory);
        return d;
    }
}
