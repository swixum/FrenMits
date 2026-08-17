using FrenAlerts.Engine;

namespace FrenAlerts.DevTools;

// The authoring tools, one command each.
public static partial class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: FrenAlerts.DevTools <logfile>");
            return 1;
        }

        if (args[0] == "rename") return Renamer.Run(args);
        if (args[0] == "compare") return Compare.Run(args);
        if (args[0] == "fire") return Fire.Run(args);
        if (args[0] == "verify") return Renamer.Verify(args);
        if (args[0] == "import") return Import(args);
        if (args[0] == "roundtrip") return RoundTrip(args);
        if (args[0] == "pack") return Pack(args);
        if (args[0] == "coverage") return Coverage(args);

        var path = args[0];
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"no such file: {path}");
            return 1;
        }

        var reader = new LogReader();
        var book = new ActorBook();
        var byKind = new Dictionary<EventKind, int>();
        var events = new List<GameEvent>();

        foreach (var e in reader.Read(File.ReadLines(path)))
        {
            events.Add(e);
            byKind[e.Kind] = byKind.GetValueOrDefault(e.Kind) + 1;
            book.Note(e);
            if (e.Kind == EventKind.ActorSpawn)
                book.Add(new Actor
                {
                    Id = e.SourceId,
                    MaxHp = e.Id,
                    IsPlayer = (e.SourceId >> 28) == 1,
                    HasCast = book.Get(e.SourceId)?.HasCast ?? false,
                });
        }

        Console.WriteLine($"parsed   {reader.Parsed}");
        Console.WriteLine($"skipped  {reader.Skipped}  (line types the engine has no use for)");
        Console.WriteLine($"refused  {reader.Refused.Values.Sum()}");
        foreach (var (type, n) in reader.Refused.OrderByDescending(p => p.Value))
            Console.WriteLine($"  type {type}: {n}");

        Console.WriteLine();
        foreach (var (kind, n) in byKind.OrderByDescending(p => p.Value))
            Console.WriteLine($"{kind,-14}{n,8}");

        Console.WriteLine();
        Console.WriteLine($"span          {events[^1].Time - events[0].Time:F1}s");
        Console.WriteLine($"ordered       {EventOrder.IsOrdered(events)}");
        Console.WriteLine($"worst backstep {EventOrder.WorstBackstep(events):F3}s");
        Console.WriteLine($"actors        {book.Count} (dropped {book.Dropped})");

        var boss = book.Boss();
        Console.WriteLine(boss is null
            ? "boss          none found"
            : $"boss          {boss.Id:X8} at {boss.MaxHp:N0} max health");

        var positioned = events.Count(e => e.Source.Known);
        Console.WriteLine($"with a source position  {positioned} of {events.Count}");

        var me = args.Length > 1 ? Convert.ToUInt32(args[1], 16) : 0;
        var mySlot = args.Length > 2 ? args[2] : "";
        var engine = new TriggerEngine(new PlayerContext { MyId = me, MySlot = mySlot });
        engine.Party.Assign(me, mySlot);
        foreach (var (id, name) in reader.Names) engine.Actors.Remember(id, name);

        // Which fight this is comes from the recording's own zone change, so the
        // tool is not pinned to one territory.
        var territory = (ushort)events.FirstOrDefault(e => e.Kind == EventKind.ZoneChange).Id;
        Console.WriteLine($"territory     {territory} {Scope.GetValueOrDefault(territory, "unknown")}");

        if (territory == DancingMad.Territory)
        {
            engine.AddRange(DancingMad.Triggers());
            engine.AddRange(DancingMad.AllSequences());
        }

        // Same order the plugin uses: fight module, then the named marker and tether
        // calls, then the pack.
        engine.AddRange(MarkerCalls.Triggers(territory, engine.Triggers));

        if (args.Length > 3 && File.Exists(args[3]))
        {
            var pack = new PackImport();
            var specs = pack.Number(pack.Collapse(pack.Read(File.ReadLines(args[3]), Scope.Keys.ToHashSet()))).ToList();
            foreach (var fam in pack.Numbered.Where(f => f.Contains("Cast") || f.Contains("Ability")).Take(6))
                Console.WriteLine($"numbered      {fam}");
            if (territory == DancingMad.Territory)
                engine.State.LearnPhases(DancingMad.PhaseChanges());
            else engine.State.LearnPhases(specs
                .Where(s => s.Territory == territory && s.Phase > 0)
                .Select(s => (s.On, s.MatchId, s.Phase)));
            // The hand-written module is already loaded, so imported rows covering
            // the same event are skipped rather than left to collide with it.
            var imported = TriggerPack.Build(specs, territory, engine.Triggers).ToList();
            engine.AddRange(imported);
            Console.WriteLine($"phase ids     {engine.State.PhasesKnown}");
            Console.WriteLine($"imported      {imported.Count} (after skipping ones the fight module covers)");
        }
        var traced = args.Length > 4 ? Convert.ToUInt32(args[4], 16) : 0u;

        var ordered = EventOrder.Sorted(events).ToList();
        if (traced != 0)
        {
            Console.WriteLine($"\ntracing id {traced:X}:");
            var watching = engine.Triggers.Where(t => t.MatchId == traced).ToList();
            Console.WriteLine($"  {watching.Count} triggers watch it: " +
                              string.Join(", ", watching.Select(t => $"{t.Id}({t.On},enabled={t.Enabled})")));
            var hits = ordered.Where(e => e.Id == traced).ToList();
            Console.WriteLine($"  {hits.Count} events carry it, {hits.Count(h => h.TargetId == me)} aimed at you");
            foreach (var h in hits.Where(h => h.TargetId == me).Take(10))
                Console.WriteLine($"    {h.Time,9:F2} {h.Kind} target {h.TargetId:X8}");
        }

        var calls = engine.Replay(ordered).ToList();

        Console.WriteLine();
        Console.WriteLine($"calls         {calls.Count} ({calls.Count(c => c.Personal)} personal)");
        Console.WriteLine($"suppressed    {engine.Scheduler.Suppressed}");
        Console.WriteLine($"reached phase {engine.State.Phase}");
        foreach (var c in calls.Take(Rows)) Console.WriteLine("  " + c);
        if (calls.Count > Rows) Console.WriteLine($"  ... and {calls.Count - Rows} more not shown");

        Console.WriteLine("\nwhat it said, and how often:");
        foreach (var (text, n) in calls.GroupBy(c => c.Text)
                     .Select(g => (g.Key, g.Count()))
                     .OrderByDescending(p => p.Item2))
            Console.WriteLine($"  {n,4}x  {text}");

        var s = engine.Scheduler;
        Console.WriteLine();
        Console.WriteLine($"dropped as a repeat   {s.DroppedAsRepeat.Values.Sum()}");
        Console.WriteLine($"dropped for crowding  {s.DroppedForCrowding.Values.Sum()}");
        Console.WriteLine($"keys forgotten to the bound  {s.Forgotten}");

        Show("dropped for crowding, and what beat them", s.DroppedForCrowding,
             key => $" lost to {s.LostTo.GetValueOrDefault(key, "?")}");
        Show("dropped as a repeat", s.DroppedAsRepeat, _ => "");
        return 0;
    }

    private const int Rows = 12;

    private static void Show(string title, Dictionary<string, int> counts, Func<string, string> note)
    {
        Console.WriteLine($"\n{title}: {counts.Count} distinct, {counts.Values.Sum()} in total");
        foreach (var (key, n) in counts.OrderByDescending(p => p.Value).Take(Rows))
            Console.WriteLine($"  {n,4}x  {key,-42}{note(key)}");
        if (counts.Count > Rows) Console.WriteLine($"  ... and {counts.Count - Rows} more not shown");
    }

}
