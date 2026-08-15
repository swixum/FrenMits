using System.Collections.Generic;

namespace FrenMits.Callouts.Fights;

// The fights written as code rather than as rows.
//
// A pack row is an id and a sentence, which covers most of a fight and none of
// the parts anybody struggles with. The parts anybody struggles with are the
// ones where the same id means two different things depending on something else
// that happened, and those are written here, one file per fight.
//
// A module here is laid over that fight's pack rows, so anything it does not
// cover keeps working exactly as it did.
public static class FightBook
{
    private static readonly Dictionary<uint, FightModule> Written = Build();

    private static Dictionary<uint, FightModule> Build()
    {
        var book = new Dictionary<uint, FightModule>();
        Add(book, DancingMadP1.Module());
        Add(book, DancingMadP2.Module());
        Add(book, DancingMadP3.Module());
        Add(book, DancingMadEarthquake.Module());
        Add(book, DancingMadKefkaSays.Module());
        return book;
    }

    private static void Add(Dictionary<uint, FightModule> book, FightModule module)
    {
        if (book.TryGetValue(module.Territory, out var already))
            book[module.Territory] = module.Over(already);
        else
            book[module.Territory] = module;
    }

    public static bool Has(uint territory) => Written.ContainsKey(territory);

    public static FightModule? For(uint territory)
        => Written.TryGetValue(territory, out var m) ? m : null;

    public static IReadOnlyCollection<uint> Territories => Written.Keys;
}
