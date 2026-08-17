namespace FrenAlerts.Engine;

// The second each of a fight's mechanics lands, and which one a call is about.
//
// Only the fight page uses it, to put its rows in the order the fight puts them.
// It lives here rather than beside the page because the page cannot be built
// without the game loaded, and a matcher nothing can run against the real packs
// is a matcher nobody can check.
//
// Names and call ids are flattened to the same shape and matched as whole words.
// A run of characters was the first attempt and it dated
// "r12s-bursting-grotesquerie" off a mechanic called "Burst", which is a
// different mechanic that happens to start the same way.
public sealed class MechanicClock
{
    // "Revolting Ruin III 2" to ["revolting", "ruin", "iii"], so a timeline name
    // and a call id meet in the middle. The trailing count is dropped: it numbers
    // the repetition, not the mechanic.
    public static string Flatten(string name)
    {
        var chars = new char[name.Length];
        var n = 0;
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c)) chars[n++] = char.ToLowerInvariant(c);
            else if (n > 0 && chars[n - 1] != '-') chars[n++] = '-';
        }
        var flat = new string(chars, 0, n).Trim('-');

        var cut = flat.LastIndexOf('-');
        return cut > 0 && flat[(cut + 1)..].All(char.IsDigit) ? flat[..cut] : flat;
    }

    private readonly record struct Mechanic(string Flat, string[] Words, float At);

    private readonly List<Mechanic> _mechanics = [];

    public int Count => _mechanics.Count;

    // The first second each mechanic lands. First rather than every: a mechanic
    // that repeats is one row on the page and belongs where it is first heard.
    public MechanicClock(IEnumerable<TimelineEntry> entries)
    {
        var first = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            var flat = Flatten(e.Mechanic);
            if (flat.Length == 0) continue;
            if (!first.TryGetValue(flat, out var at) || e.Time < at) first[flat] = e.Time;
        }
        foreach (var (flat, at) in first) _mechanics.Add(new Mechanic(flat, flat.Split('-'), at));
    }

    // Which second the call's mechanic lands on, or float.MaxValue if the timeline
    // does not name it. Unknown is not zero: a call nothing dates keeps its own
    // place at the end rather than floating to the top and reading as first.
    //
    // Either way round, because neither side is reliably the longer one. A call id
    // can carry the mechanic and more ("wave-cannon-towers" holds "wave-cannon"),
    // and it can equally be the shorter of the two: the timeline writes "Revolting
    // Ruin III" where the call is just "revolting-ruin".
    //
    // Longest match wins, counted in characters, so a call holding both
    // "wave-cannon" and "wave-cannon-explosion" is dated by the mechanic it is
    // actually about. Counted in words instead, one-word matches all tie and the
    // winner is whichever was read first: "ucu-megaflare-stack-me" dated off
    // "stack" rather than off Megaflare.
    public float WhenOf(string callKey)
    {
        var id = Flatten(callKey);
        if (id.Length == 0) return float.MaxValue;
        var mine = id.Split('-');

        var best = float.MaxValue;
        var longest = 0;

        foreach (var m in _mechanics)
        {
            var hit = m.Flat.Length >= id.Length ? Holds(m.Words, mine) : Holds(mine, m.Words);
            if (!hit) continue;

            var strength = Math.Min(m.Flat.Length, id.Length);
            if (strength <= longest) continue;
            longest = strength;
            best = m.At;
        }
        return best;
    }

    // Whether the shorter run of words appears whole inside the longer one.
    private static bool Holds(string[] outer, string[] inner)
    {
        if (inner.Length > outer.Length) return false;
        for (var at = 0; at + inner.Length <= outer.Length; at++)
        {
            var all = true;
            for (var i = 0; i < inner.Length && all; i++) all = Same(outer[at + i], inner[i]);
            if (all) return true;
        }
        return false;
    }

    // A trailing s is the same word: the timeline writes "Twister" where the call
    // says "twisters", and "Wings of Destruction" where it says "single wing of
    // destruction". Only that, so "burst" stays a different word from "bursting".
    private static bool Same(string a, string b) =>
        a == b
        || (a.Length == b.Length + 1 && a[^1] == 's' && a.StartsWith(b, StringComparison.Ordinal))
        || (b.Length == a.Length + 1 && b[^1] == 's' && b.StartsWith(a, StringComparison.Ordinal));
}
