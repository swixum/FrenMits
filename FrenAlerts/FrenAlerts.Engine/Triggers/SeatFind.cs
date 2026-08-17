using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenAlerts.Engine;

// Matching what somebody typed against the party, for the role list on the Roles page.
//
// Lives here rather than in the window because it is the only part of that list that can
// be wrong rather than ugly: which name Enter takes decides who a call names on the night,
// and a window cannot be tested without a game loaded around it.
public static class SeatFind
{
    // Everybody carrying what was typed, in the order the party is in. Empty typing
    // matches the lot, so the list opens showing the party rather than showing nothing.
    public static List<string> Matching(IReadOnlyList<string> names, string typed)
    {
        var want = (typed ?? "").Trim();
        if (names is null) return [];
        if (want.Length == 0) return [.. names];

        return [.. names.Where(n => n is not null
            && n.Contains(want, StringComparison.OrdinalIgnoreCase))];
    }

    // Whether the party already has somebody by that name, which is what decides if the
    // list offers the typing as a name of its own.
    public static bool Known(IReadOnlyList<string> names, string typed)
    {
        var want = (typed ?? "").Trim();
        return want.Length > 0 && names is not null
            && names.Any(n => string.Equals(n, want, StringComparison.OrdinalIgnoreCase));
    }

    // What Enter takes.
    //
    // The party's own spelling wherever the party has one, because the game spells a
    // name one way and the book matches without case: taking the typed text would write
    // "dee" into a seat the party calls "Dee", and it is the seat that gets read out.
    //
    // An exact name wins over a longer one carrying it. Typing all of "Dee" while the
    // party also holds "Deedee" is somebody naming Dee, not somebody halfway through
    // typing the other one, and Enter has to be able to reach the shorter name at all.
    //
    // Failing both, the typing stands as its own name: that is how somebody who is not
    // online is named, on the night the group is planned rather than the night it runs.
    public static string Taken(IReadOnlyList<string> names, string typed)
    {
        var want = (typed ?? "").Trim();
        if (want.Length == 0 || names is null) return want;

        var exact = names.FirstOrDefault(n =>
            string.Equals(n, want, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var carrying = Matching(names, want);
        return carrying.Count == 1 ? carrying[0] : want;
    }
}
