using Jint;
using Jint.Native;
using FrenAlerts.Engine.Scripts;

namespace FrenAlerts.DevTools;

// What a fight's page would list, counted rather than eyeballed.
//
// The page shows the calls that speak, with the words off their own output strings. A
// call that speaks but declares no words is a row with a name and nothing under it, so
// the count of those is the honest measure of whether the list is complete.
public static class Listing
{
    public static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: listing <scripts folder> <zone>");
            return 2;
        }

        using var fights = new ScriptFights();
        fights.Load(args[1]);

        var zone = ushort.Parse(args[2]);
        // The same two ways the plugin asks, in the same order.
        fights.StartPull(zone, "Fren Mit", "dps", "SAM");

        var runner = new ScriptTriggerRunner(fights.Js!);
        runner.Compile(fights.SetsFor(zone));

        List<string> Words(string id)
        {
            var said = runner.Says(id).ToList();
            if (said.Count == 0)
                said = runner.Outputs(id).Select(o => o.Shipped).Where(w => w.Length > 0)
                    .Distinct(StringComparer.Ordinal).ToList();
            return said;
        }

        var all = runner.Triggers.Count;
        var speaks = runner.Triggers.Count(t => t.Speaks);
        var worded = runner.Triggers.Count(t => t.Speaks && Words(t.Id).Count > 0);

        Console.WriteLine($"zone {zone}: {all} triggers, {speaks} speak, {worded} of those carry words");
        Console.WriteLine($"listed on the page: {speaks}, of which {speaks - worded} would show no words");

        foreach (var trigger in runner.Triggers.Where(t => t.Speaks && Words(t.Id).Count == 0).Take(12))
            Console.WriteLine($"  no words: {trigger.Id}");

        return 0;
    }
}
