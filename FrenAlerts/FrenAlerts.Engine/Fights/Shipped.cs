namespace FrenAlerts.Engine;

// One fight the plugin ships calls for.
public sealed record ShippedFight(ushort Territory, string Name, string Full, string Category);

// Which fights ship, in one place.
//
// The pack's import filter, the fight page and the tests all used to carry their
// own copy of this list, so dropping a fight meant finding three of them and the
// build stayed green when one was missed.
public static class Shipped
{
    public static readonly IReadOnlyList<ShippedFight> Fights =
    [
        new(733, "UCOB", "The Unending Coil of Bahamut", "Ultimate"),
        new(777, "UWU", "The Weapon's Refrain", "Ultimate"),
        new(1363, "DMU", "Dancing Mad", "Ultimate"),
        new(1321, "M9S", "Vamp Fatale", "Savage"),
        new(1323, "M10S", "Red Hot & Deep Blue", "Savage"),
        new(1325, "M11S", "The Tyrant & Comet", "Savage"),
        new(1327, "M12S", "Lindwurm", "Savage"),
    ];

    public static readonly IReadOnlySet<ushort> Territories =
        Fights.Select(f => f.Territory).ToHashSet();

    public static ShippedFight? At(ushort territory) =>
        Fights.FirstOrDefault(f => f.Territory == territory);

    public static string NameOf(ushort territory) => At(territory)?.Name ?? "unknown";
}
