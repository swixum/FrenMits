using System;
using System.Collections.Generic;

namespace FrenMits;

// The cast that ends a fight when the timer runs out.
public static class Enrages
{
    // What an enrage looks like when there's no table entry: a party-wide hit
    // far past anything a fight is balanced around.
    public const long DamageFloor = 1_000_000;

    public readonly record struct Enrage(uint Ability, float Time, string Name);

    private static readonly Dictionary<uint, Enrage[]> Known = new()
    {
        // Vamp Fatale.
        [Builtin.M9sTerritory] = new[] { new Enrage(0xB371, 606f, "Finale Fatale") },
        // Red Hot / Deep Blue casts both halves together.
        [Builtin.M10sTerritory] = new[]
        {
            new Enrage(0xB5FC, 601f, "Over the Falls"),
            new Enrage(0xB5FD, 601f, "Over the Falls"),
        },
        [Builtin.M11sTerritory] = new[] { new Enrage(0xB463, 664f, "Heartbreaker") },
        // the Unmaking.
        [Builtin.EnuoTerritory] = new[] { new Enrage(0xC382, 665f, "Almagest") },
        // Hell on Rails has NO hard enrage cast.
        [Builtin.DoomtrainTerritory] = Array.Empty<Enrage>(),
    };

    public static IReadOnlyList<Enrage> For(uint territory)
        => Known.TryGetValue(territory, out var e) ? e : Array.Empty<Enrage>();

    // Is this cast the fight's enrage?
    public static bool Is(uint territory, uint ability)
    {
        foreach (var e in For(territory))
            if (e.Ability == ability) return true;
        return false;
    }

    // Same question for a fight with no table entry, answered from the log itself:
    // a party-wide hit this far past everything else is the timer, not a mechanic.
    public static bool LooksLikeOne(long unmitigated, int targets)
        => unmitigated >= DamageFloor && targets > 3;

    // Everything the plan should refuse to spend cooldowns on, for a fight the
    // sheet knows by territory.
    public static bool IsEnrageRow(uint territory, CustomRow row)
        => row.Enrage || For(territory).Exists(e => MathF.Abs(e.Time - row.Time) < 3f
                                                    && MechEquals(e.Name, row.Mechanic));

    private static bool Exists(this IReadOnlyList<Enrage> list, Func<Enrage, bool> match)
    {
        foreach (var e in list)
            if (match(e)) return true;
        return false;
    }

    private static bool MechEquals(string a, string b)
        => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
