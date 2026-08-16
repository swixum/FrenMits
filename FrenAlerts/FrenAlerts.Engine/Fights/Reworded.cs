namespace FrenAlerts.Engine;

// Better words for a pack row, one row at a time.
//
// A hand written call in the fight module replaces every pack row on its event
// and id, which is usually what you want. It is wrong when one id carries two
// rows that say different things: Future's End shares its id with "tank limit
// break", so authoring that id would silence the most important line in the
// fight to fix the wording of the least important one.
//
// So this changes the words and leaves everything else, including which rows
// exist. Only used where the real wording is knowable from the row itself.
public static class Reworded
{
    public static string For(ushort territory, string id, string text) =>
        territory == DancingMad.Territory && DancingMad_.TryGetValue(id, out var better)
            ? better
            : text;

    public static int Count(ushort territory) =>
        territory == DancingMad.Territory ? DancingMad_.Count : 0;

    private static readonly Dictionary<string, string> DancingMad_ = new()
    {
        // Two casts, one each. Which one landed is the call, and the raid says it
        // in one word.
        ["dmu-p2-future-s-end-past-s-end-early-1"] = "future",
        ["dmu-p2-future-s-end-past-s-end-early-2"] = "past",

        // The pair of elements is in the row's own name, and the pair is what you
        // act on. The mechanic's name on its own tells you nothing you did not
        // already know from the cast bar.
        ["dmu-p1-mystery-magic-fire-and-thunder"] = "fire and thunder",
        ["dmu-p1-mystery-magic-ice-and-fire"] = "ice and fire",
        ["dmu-p1-and-p4-mystery-magic-ice-and-thunder"] = "ice and thunder",

        // No commas, no slashes. Brackets only where one spot has two names, so
        // "Right (East)" earns them and "Front Back" does not: those are two spots.
        // "then" is an order.
        ["dmu-p1-gravitational-wave"] = "Right (East)",
        ["dmu-p1-impertinent-will"] = "Left (West)",
        ["dmu-p1-wave-cannon"] = "East West spread",
        ["dmu-p3-longitudinal-implosion"] = "Sides then Front Back",
        ["dmu-p3-latitudinal-implosion"] = "Front Back then Sides",
        ["dmu-p5-forsaken"] = "stack raidwide",
        ["dmu-p5-ultima-repeater"] = "raidwide x4",
    };
}
