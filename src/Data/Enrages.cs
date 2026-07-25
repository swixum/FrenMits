using System;
using System.Collections.Generic;

namespace FrenMits;

// The cast that ends a fight when the timer runs out.
//
// It matters here for two reasons, and the second is the one that bites quietly:
//
//   1. It cannot be mitigated. Every hard enrage below lands for a flat ten
//      million, which is not a number any cooldown is scaled against, so a plan
//      that spends a party stack on it has thrown those cooldowns away.
//   2. It wrecks the severity scale. Rows are graded against the HARDEST raidwide
//      in the log (HurtLevel), so a single 10,000,000 hit makes a real 340,000
//      raidwide grade 3% - "light" - and every genuine mechanic in the fight ends
//      up looking harmless.
//
// A sheet built from a KILL never sees one, which is why this went unnoticed:
// every official sheet so far was imported from a kill. Import a wipe that ran to
// the timer and both problems land at once.
//
// The table is what the logs say, read from pulls that actually reached it (the
// ranked logs are all kills, so these came from listing reports by zone instead).
public static class Enrages
{
    // What an enrage looks like when there's no table entry: a party-wide hit far
    // past anything a fight is balanced around. The four measured ones are all
    // exactly ten million; the real mechanics they sit beside top out near half a
    // million, so anywhere in between separates them cleanly.
    public const long DamageFloor = 1_000_000;

    public readonly record struct Enrage(uint Ability, float Time, string Name);

    private static readonly Dictionary<uint, Enrage[]> Known = new()
    {
        // Vamp Fatale. Shares its name with the Finale Fatale at 2:52 and 6:30,
        // which are different casts and are mitigated normally.
        [Builtin.M9sTerritory] = new[] { new Enrage(0xB371, 606f, "Finale Fatale") },
        // Red Hot / Deep Blue casts both halves together.
        [Builtin.M10sTerritory] = new[]
        {
            new Enrage(0xB5FC, 601f, "Over the Falls"),
            new Enrage(0xB5FD, 601f, "Over the Falls"),
        },
        [Builtin.M11sTerritory] = new[] { new Enrage(0xB463, 664f, "Heartbreaker") },
        // the Unmaking. Also shares its name with the Almagest at 4:52 and 7:18.
        [Builtin.EnuoTerritory] = new[] { new Enrage(0xC382, 665f, "Almagest") },
        // Hell on Rails has NO hard enrage cast. Its longest pulls end during the
        // final Derailment Siege chain around 11:00 and then run on with nobody
        // taking damage - the party is already dead. Damage outrunning the healers,
        // not one lethal cast, so there is no id to list and nothing to skip.
        [Builtin.DoomtrainTerritory] = Array.Empty<Enrage>(),
    };

    public static IReadOnlyList<Enrage> For(uint territory)
        => Known.TryGetValue(territory, out var e) ? e : Array.Empty<Enrage>();

    // Is this cast the fight's enrage? Checked by ability id, never by name: an
    // enrage habitually reuses the name of a normal mechanic in the same fight
    // (M9S's Finale Fatale, Enuo's Almagest), and those are mitigated normally.
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
