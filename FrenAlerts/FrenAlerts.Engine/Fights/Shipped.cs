namespace FrenAlerts.Engine;

// One fight the plugin ships calls for.
public sealed record ShippedFight(
    ushort Territory, string Name, string Full, string Category, string Expansion);

// Which fights ship, in one place.
//
// The pack's import filter, the fight page and the tests all used to carry their
// own copy of this list, so dropping a fight meant finding three of them and the
// build stayed green when one was missed.
public static class Shipped
{
    // Newest first, which is the order the fight list is grouped in. Copied from the
    // one in FrenMits rather than worked out here, because it is the same answer and
    // two lists of expansions would disagree the week a new one lands.
    public static readonly string[] Expansions =
        ["Dawntrail", "Endwalker", "Shadowbringers", "Stormblood"];

    public static readonly IReadOnlyList<ShippedFight> Fights =
    [
        new(733, "UCOB", "The Unending Coil of Bahamut", "Ultimate", "Stormblood"),
        new(777, "UWU", "The Weapon's Refrain", "Ultimate", "Stormblood"),
        new(1363, "DMU", "Dancing Mad", "Ultimate", "Dawntrail"),
        new(1321, "M9S", "Vamp Fatale", "Savage", "Dawntrail"),
        new(1323, "M10S", "Red Hot & Deep Blue", "Savage", "Dawntrail"),
        new(1325, "M11S", "The Tyrant & Comet", "Savage", "Dawntrail"),
        new(1327, "M12S", "Lindwurm", "Savage", "Dawntrail"),
    ];

    // How far down the list an expansion sits, and past the end for one nobody named
    // so a fight added without an expansion sinks rather than floating to the top and
    // reading as the newest thing there is.
    public static int ExpansionRank(string expansion)
    {
        var at = Array.IndexOf(Expansions, expansion);
        return at < 0 ? Expansions.Length : at;
    }

    public static readonly IReadOnlySet<ushort> Territories =
        Fights.Select(f => f.Territory).ToHashSet();

    public static ShippedFight? At(ushort territory) =>
        Fights.FirstOrDefault(f => f.Territory == territory);

    public static string NameOf(ushort territory) => At(territory)?.Name ?? "unknown";
}
