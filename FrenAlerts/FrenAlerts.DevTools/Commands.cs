using FrenAlerts.Engine;

namespace FrenAlerts.DevTools;

// The commands that turn recorded pulls and the trigger table into shipped data.
public static partial class Program
{
    private static int RoundTrip(string[] args)
    {
        if (args.Length < 2 || !File.Exists(args[1]))
        {
            Console.Error.WriteLine("usage: FrenAlerts.DevTools roundtrip <logfile>");
            return 1;
        }

        var original = new LogReader().Read(File.ReadLines(args[1])).ToArray();
        var text = new StringWriter();
        EventLog.WriteAll(text, original);
        var lines = text.ToString().Split(Environment.NewLine);
        var back = EventLog.ReadAll(lines).ToArray();

        Console.WriteLine($"events out    {original.Length}");
        Console.WriteLine($"events back   {back.Length}");

        var mismatched = 0;
        var worstTime = 0.0;
        var lost = 0;
        for (var i = 0; i < Math.Min(original.Length, back.Length); i++)
        {
            var a = original[i];
            var b = back[i];
            worstTime = Math.Max(worstTime, Math.Abs(a.Time - b.Time));
            if (a.Kind != b.Kind || a.SourceId != b.SourceId || a.TargetId != b.TargetId || a.Id != b.Id)
                mismatched++;
            if (a.Source.Known != b.Source.Known || a.Target.Known != b.Target.Known) lost++;
        }

        Console.WriteLine($"ids or kinds wrong   {mismatched}");
        Console.WriteLine($"position known/unknown flipped   {lost}");
        Console.WriteLine($"worst time drift     {worstTime:F4}s");
        Console.WriteLine($"ordered on read-back {EventOrder.IsOrdered(back)}");

        // Writing what came back has to give the same bytes, or a recording drifts
        // every time it is opened and saved.
        var again = new StringWriter();
        EventLog.WriteAll(again, back);
        Console.WriteLine($"byte identical on rewrite  {again.ToString() == text.ToString()}");

        var ok = original.Length == back.Length && mismatched == 0 && lost == 0;
        Console.WriteLine(ok ? "\nround trip is identity" : "\nROUND TRIP LOST SOMETHING");
        return ok ? 0 : 1;
    }

    // Builds the shipped call pack: import, reword, number the variants, write.
    private static int Pack(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: FrenAlerts.DevTools pack <table> <out.facall>");
            return 1;
        }

        var import = new PackImport();
        // Collapsed before numbering: rows reduced to the same words are one
        // call, and only the ones that still differ need an occurrence to wait for.
        var specs = import.Number(import.Collapse(import.Read(File.ReadLines(args[1]), Scope.Keys.ToHashSet()))).ToList();

        using (var to = new StreamWriter(args[2]))
            CallPack.WriteAll(to, specs);

        var written = CallPack.ReadAll(File.ReadLines(args[2])).ToList();
        Console.WriteLine($"rows read     {import.Rows}");
        Console.WriteLine($"reworded      {import.Rewritten}");
        Console.WriteLine($"named         {import.Named} (built from the trigger's own name)");
        Console.WriteLine($"collapsed     {import.Collapsed} (same words, same event, one call)");
        Console.WriteLine($"held back     {import.Unnamed.Count} (no call this could state)");
        Console.WriteLine($"written       {written.Count}");
        Console.WriteLine($"  on by default {written.Count(s => s.DefaultOn)} (all of them, as in the source)");
        Console.WriteLine($"  not an exact port {written.Count(s => !s.Reproduced)} " +
                          "(the narrowing condition could not be read, or the line only had a mechanic name to say)");
        Console.WriteLine($"  once a pull   {written.Count(s => s.Once)}");
        Console.WriteLine($"  own quiet window {written.Count(s => s.Hush > 0)}");
        Console.WriteLine();

        foreach (var (zone, name) in Scope.OrderBy(p => p.Value))
        {
            var have = written.Count(s => s.Territory == zone);
            var want = specs.Count(s => s.Territory == zone);
            var flag = have == want ? "" : $"  ({want - have} still silent)";
            Console.WriteLine($"  {name,-14}{have,4} of {want,-4}{flag}");
        }

        if (import.Unnamed.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("mechanics with no call, and why: the name is a label or a");
            Console.WriteLine("position we cannot work out, so nothing true could be said.");
            foreach (var u in import.Unnamed.Distinct().OrderBy(u => u)) Console.WriteLine($"  {u}");
        }

        // Read back and compared here rather than trusted, because a pack that
        // writes fine and reads wrong is a fight that goes quiet in game.
        var same = written.Count == specs.Count(s => !s.NeedsWording);
        Console.WriteLine(same ? "\npack round trips" : "\nPACK LOST ROWS ON READ-BACK");
        return same ? 0 : 1;
    }

    // The 14 fights in scope: the Dawntrail ultimates and savage tiers.
    private static readonly Dictionary<ushort, string> Scope = new()
    {
        [1363] = "Dancing Mad", [1238] = "FRU", [1327] = "M12S", [1325] = "M11S",
        [1323] = "M10S", [1321] = "M9S", [1263] = "M8S", [1261] = "M7S",
        [1259] = "M6S", [1257] = "M5S", [1232] = "M4S", [1230] = "M3S",
        [1228] = "M2S", [1226] = "M1S",
    };

    private static int Import(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: FrenAlerts.DevTools import <table>");
            return 1;
        }

        var import = new PackImport();
        var specs = import.Read(File.ReadLines(args[1]), Scope.Keys.ToHashSet()).ToList();

        Console.WriteLine($"rows          {import.Rows}");
        Console.WriteLine($"rewritten     {import.Rewritten}");
        Console.WriteLine($"placeholders  {import.Placeholders}  (loaded but never spoken)");
        Console.WriteLine($"still theirs  {import.Untouched.Count}");
        Console.WriteLine();

        foreach (var (zone, name) in Scope.OrderBy(p => p.Value))
        {
            var mine = specs.Where(s => s.Territory == zone).ToList();
            if (mine.Count == 0) continue;
            var ready = mine.Count(s => !s.NeedsWording);
            Console.WriteLine($"{name,-14}{ready,4} ready  {mine.Count - ready,4} need wording");
        }

        Console.WriteLine();
        Console.WriteLine("Dancing Mad, what it would say:");
        foreach (var s in specs.Where(s => s.Territory == 1363 && !s.NeedsWording).Take(18))
            Console.WriteLine($"  {s.On,-12} {s.MatchId:X4}  {s.Text}");

        if (import.Untouched.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("lines the rewrite could not reach:");
            foreach (var t in import.Untouched.Distinct().Take(15)) Console.WriteLine($"  {t}");
        }
        return 0;
    }
}
