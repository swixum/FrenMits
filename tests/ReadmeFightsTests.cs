using System.Text.RegularExpressions;
using Xunit;

namespace FrenMits.Tests;

// The README's fight table is the only place anyone browsing the repo learns what
// ships planned, and it is the easiest thing in the world to forget when adding a
// fight - nothing builds from it and nothing else reads it. So this does.
//
// Adding an official fight now means adding its row here too, or the suite says so
// by name.
public class ReadmeFightsTests
{
    private static string Readme()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "README.md")))
            dir = dir.Parent;
        Assert.True(dir != null, "no README.md above " + AppContext.BaseDirectory);
        return File.ReadAllText(Path.Combine(dir!.FullName, "README.md"));
    }

    [Fact]
    public void EveryOfficialFightIsListed()
    {
        var readme = Readme();
        foreach (var (_, name, _, _) in Builtin.Fights)
            Assert.True(readme.Contains(name, StringComparison.Ordinal),
                $"README.md doesn't mention \"{name}\". Add it to the built-in fights table.");
    }

    [Fact]
    public void EveryExpansionWithAFightHasItsOwnRow()
    {
        var readme = Readme();
        foreach (var expansion in Builtin.Fights.Select(f => f.Expansion).Distinct())
            Assert.True(readme.Contains(expansion, StringComparison.Ordinal),
                $"README.md has no {expansion} row, so its fights are listed under the wrong heading.");
    }

    [Fact]
    public void TheTableListsNothingThatIsntShipping()
    {
        // The other direction: a fight pulled from Builtin.Fights but left in the
        // table advertises a sheet nobody gets. Only rows of the fights table are
        // checked, since the rest of the README names plenty of other things.
        var rows = Regex.Matches(Readme(), @"^\| \*\*(\w+)\*\* \|(.*)$", RegexOptions.Multiline);
        Assert.NotEmpty(rows);
        var shipping = Builtin.Fights.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        foreach (Match row in rows)
            foreach (var cell in row.Groups[2].Value.Split('|'))
                foreach (var listed in cell.Split(',', StringSplitOptions.TrimEntries
                                                      | StringSplitOptions.RemoveEmptyEntries))
                    Assert.True(shipping.Contains(listed),
                        $"README.md lists \"{listed}\", which is not in Builtin.Fights.");
    }

    [Fact]
    public void ExpansionsAreOrderedNewestFirst()
    {
        // The table and the in-game menu both take their order straight from
        // Builtin.Fights, so a fight filed under the wrong expansion, or an
        // expansion split across two runs of the array, shows up crooked in both.
        var order = Builtin.Expansions.ToList();
        var seen = new List<string>();
        foreach (var f in Builtin.Fights)
        {
            Assert.True(order.Contains(f.Expansion),
                $"{f.Name} is filed under \"{f.Expansion}\", which isn't in Builtin.Expansions.");
            if (seen.Count == 0 || seen[^1] != f.Expansion) seen.Add(f.Expansion);
        }
        Assert.Equal(seen.Count, seen.Distinct().Count());        // no expansion appears twice
        Assert.Equal(seen.OrderBy(e => order.IndexOf(e)).ToList(), seen);
    }
}
