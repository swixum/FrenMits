namespace FrenAlerts.Engine;

// Pack rows that never load, named one at a time.
//
// The bar is high and it is not "I could not reword it". A mechanic nobody can
// phrase yet is still a mechanic that kills people, and hearing its name at the
// moment it happens beats hearing nothing. Only rows that would say the same
// thing as a call already firing at the same moment belong here.
//
// Dropping by row rather than by id matters: several ids carry a bare row and a
// real one, and Future's End also carries "tank limit break", which is the single
// most important line in the fight.
public static class Trimmed
{
    public static bool Drops(ushort territory, string id) =>
        territory == DancingMad.Territory && DancingMad_.Contains(id);

    public static int Count(ushort territory) =>
        territory == DancingMad.Territory ? DancingMad_.Count : 0;

    private static readonly HashSet<string> DancingMad_ =
    [
        // The catch-all tether call already says one is on you, and says it once.
        // These five would be a second line at the same moment saying the same thing.
        "dmu-p1-gravitas-and-vitrophyre-tethers-2",
        "dmu-p1-indulgent-will-and-idyllic-will-tethers-early",
        "dmu-p1-pulse-wave-tethers",
        "dmu-p3-black-hole-2-nothingness-2",
        "dmu-p3-black-hole-6-nothingness-10",

        // Their phase 2 tank limit break is gated on a strat option this group does
        // not run. The one that matters is on Vacuum Wave in phase 3, and the fight
        // module calls it there.
        "dmu-p2-path-of-light-tower-8-aaaabbbb-special-1",
        "dmu-p2-path-of-light-tower-8-aaaabbbb-special-2",
    ];
}
