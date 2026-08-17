using Jint;

namespace FrenAlerts.Engine.Scripts;

// What we run, per fight, when nobody has said otherwise.
//
// Their files ship defaults picked for whoever downloads them. These are swix's,
// given 2026-08-16, and they are what a fresh install should call before anybody
// opens a settings window. Anything not named here keeps their default.
//
// Kept as a table rather than as edits inside their fights, so replacing the whole
// folder with a newer copy of theirs cannot quietly take a pick away.
public static class ScriptPicks
{
    // The Dancing Mad picks, by their own option values.
    public static readonly IReadOnlyDictionary<string, string> DancingMad =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Bigbox / Freaky
            ["teleportent"] = "clockwise",
            ["forsaken"] = "kroxy-rinon",
            // Tank LB3
            ["boa"] = "lb3",
            ["blackHole"] = "dsa",
            // Ours rather than theirs: north is wherever Kefka is. Their file offers
            // true north and a clock number only, and the third mode is added by the
            // patch that implements it.
            ["blackHoleTether"] = KefkaNorth,
        };

    public const string KefkaNorth = "kefka";

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ByFight =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["DancingMadUltimate"] = DancingMad,
        };

    // What a fight should run: our picks where we have one, whatever somebody has
    // chosen since where they have, and their default for the rest.
    public static IReadOnlyDictionary<string, string> For(
        string fightId, IReadOnlyDictionary<string, string>? chosen = null)
    {
        var picks = new Dictionary<string, string>(StringComparer.Ordinal);

        if (ByFight.TryGetValue(fightId, out var ours))
            foreach (var (id, value) in ours) picks[id] = value;

        if (chosen is not null)
            foreach (var (id, value) in chosen)
                if (!string.IsNullOrEmpty(value)) picks[id] = value;

        return picks;
    }

    // Applies them to a loaded fight. The strategies are read from the fight itself,
    // so a choice we name that the fight no longer offers is simply not written.
    public static void Apply(
        Jint.Engine js, int setIndex, string fightId,
        IReadOnlyDictionary<string, string>? chosen = null)
    {
        var strategies = ScriptStrategies.Read(js, setIndex);
        ScriptStrategies.Apply(js, strategies, For(fightId, chosen));
    }
}
