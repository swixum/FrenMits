using Jint;
using Jint.Native;
using FrenAlerts.Engine.Scripts;

namespace FrenAlerts.DevTools;

// Loads two folders of their fight files and holds what each one registered against
// the other.
//
// The tree comparison proves the code is the same shape; this proves the fights are.
// Same sets in the same order, same trigger ids, same regexes, same words, and the
// same field names on every trigger, which is what would break if a rename had caught
// a field rather than a variable.
public static class Compare
{
    private const string Dump = """
        JSON.stringify(triggerSets.map(function (s) {
          return {
            id: s.id,
            zone: String(s.zoneId),
            triggers: (s.triggers || []).map(function (t) {
              return {
                id: t.id,
                fields: Object.keys(t).sort().join(','),
                re: t.netRegex ? String(t.netRegex.source || t.netRegex) : '',
                out: t.outputStrings ? JSON.stringify(t.outputStrings) : '',
                delay: (typeof t.delaySeconds === 'number') ? String(t.delaySeconds)
                     : (t.delaySeconds ? 'fn' : ''),
                sup: (typeof t.suppressSeconds === 'number') ? String(t.suppressSeconds)
                   : (t.suppressSeconds ? 'fn' : '')
              };
            })
          };
        }), null, 1)
        """;

    public static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: compare <before> <after>");
            return 2;
        }

        var one = Load(args[1]);
        var two = Load(args[2]);

        if (one == two)
        {
            Console.WriteLine($"the same fights registered either way: {one.Length} characters of them");
            return 0;
        }

        var lines = one.Split('\n');
        var other = two.Split('\n');
        var shown = 0;

        for (var i = 0; i < Math.Max(lines.Length, other.Length) && shown < 20; i++)
        {
            var a = i < lines.Length ? lines[i] : "";
            var b = i < other.Length ? other[i] : "";
            if (a == b) continue;
            Console.Error.WriteLine($"line {i + 1}:\n  before {a.Trim()}\n  after  {b.Trim()}");
            shown++;
        }

        Console.Error.WriteLine("they registered different fights");
        return 1;
    }

    // Loaded the way the plugin loads them, rather than by a rule of this file's own:
    // a loader that guesses which files are the harness is a second answer to a
    // question the engine already answers, and it was wrong the first time.
    private static string Load(string folder)
    {
        using var fights = new ScriptFights();
        fights.Load(folder);

        if (fights.Problem is { } trouble) Console.Error.WriteLine($"{folder}: {trouble}");

        return fights.Js!.Evaluate(Dump).AsString();
    }
}
