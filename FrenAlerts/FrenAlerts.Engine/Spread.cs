namespace FrenAlerts.Engine;

// Filling a short list from groups that are not the same size.
//
// A search box holds thirty rows and a common word answers hundreds of calls: "in" is on
// 117 of the imported set's alone. Taking them in order hands every row to whichever
// group comes first, and the fight list puts Ultimate at the top, so searching "stack"
// came back as thirty rows of Dancing Mad with every savage behind it invisible.
//
// Lives here rather than beside the search because the window it belongs to does not
// load without Dalamud, and a rule kept in there can only ever be checked by reading it.
public static class Spread
{
    // A few from each group in turn, then the rest of them in order until the list fills.
    //
    // The first pass is what stops one group owning the list. The second is what keeps a
    // narrow query whole: a word that answers ten calls in one fight and nothing anywhere
    // else should show all ten, not three of them and a lot of empty space.
    //
    // Groups keep their order and so do the items in them, because both are somebody's
    // idea of what matters most.
    public static List<T> Fill<T>(IReadOnlyList<IReadOnlyList<T>> groups, int firstPass, int max)
    {
        var found = new List<T>(Math.Min(max, 32));
        if (max <= 0 || groups.Count == 0) return found;

        // Guarded rather than assumed: a first pass of zero would skip straight to the
        // fill and quietly be the old behaviour again, which is the bug this exists for.
        firstPass = Math.Max(1, firstPass);

        foreach (var group in groups)
        {
            var take = Math.Min(firstPass, group.Count);
            for (var i = 0; i < take && found.Count < max; i++) found.Add(group[i]);
            if (found.Count >= max) return found;
        }

        foreach (var group in groups)
            for (var i = firstPass; i < group.Count; i++)
            {
                if (found.Count >= max) return found;
                found.Add(group[i]);
            }

        return found;
    }
}
