using Jint;
using Jint.Native;
using FrenAlerts.Engine;
using FrenAlerts.Engine.Scripts;

namespace FrenAlerts.DevTools;

// Fires one of their own casts at one of their own fights and prints what came back.
//
// The registration comparison proves the same fights are there; this proves they still
// speak, which is the half that a rename can break without anything failing to load.
public static class Fire
{
    public static int Run(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: fire <scripts folder> [zone]");
            return 2;
        }

        using var fights = new ScriptFights();
        fights.Load(args[1]);

        if (fights.Problem is { } trouble) Console.Error.WriteLine($"load: {trouble}");

        var zones = args.Length > 2
            ? [ushort.Parse(args[2])]
            : fights.Zones.OrderBy(z => z).ToArray();

        var quiet = 0;

        foreach (var zone in zones)
        {
            fights.StartPull(zone, "Fren Mit", "dps", "SAM");

            var said = new List<ScriptCall>();
            var runner = new ScriptTriggerRunner(fights.Js!) { Say = said.Add };
            runner.Compile(fights.SetsFor(zone));

            if (runner.Problem is { } bad) Console.Error.WriteLine($"zone {zone} compile: {bad}");

            foreach (var set in fights.SetsFor(zone))
            {
                var watched = ACastTheyWatch(fights, set);
                if (watched is not (var id, var source, var name))
                {
                    Console.WriteLine($"zone {zone} set {set}: nothing plain enough to fire");
                    continue;
                }

                said.Clear();
                runner.Process(Cast(id, source), now: 100);

                if (runner.Problem is { } ran) Console.Error.WriteLine($"zone {zone} run: {ran}");

                var heard = said.Count > 0 ? string.Join(" | ", said.Select(c => c.Text)) : "SILENT";
                if (said.Count == 0) quiet++;
                Console.WriteLine($"zone {zone} set {set}: {name} -> {heard}");
            }
        }

        return quiet == 0 ? 0 : 1;
    }

    private static string Cast(uint id, string source) =>
        ScriptLines.Write(
            new GameEvent { Kind = EventKind.CastStart, Time = 0, Id = id, SourceId = 0x40001234, CastTime = 5f },
            _ => source)!;

    private static (uint Id, string Source, string Name)? ACastTheyWatch(ScriptFights fights, int set)
    {
        var found = fights.Js!.Evaluate($$"""
            (function () {
              var ts = triggerSets[{{set}}].triggers;
              for (var i = 0; i < ts.length; i++) {
                var t = ts[i], n = t.netRegex;
                if (t.type !== 'StartsUsing' || !n) continue;
                if (typeof n.id !== 'string' || !/^[0-9A-Fa-f]+$/.test(n.id)) continue;
                if (typeof n.source !== 'string' || /[\\\[\]\(\)\|\?\*\+]/.test(n.source)) continue;
                if (!t.alertText && !t.infoText && !t.alarmText && !t.response) continue;
                if (t.condition) continue;
                return n.id + '' + n.source + '' + t.id;
              }
              return '';
            })()
            """).AsString();

        if (found.Length == 0) return null;

        var parts = found.Split('');
        return (Convert.ToUInt32(parts[0], 16), parts[1], parts[2]);
    }
}
