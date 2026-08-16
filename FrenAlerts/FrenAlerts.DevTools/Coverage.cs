using FrenAlerts.Engine;

namespace FrenAlerts.DevTools;

// What a pull sounds like from each side of the parser handover.
//
// The offline replay has always read every line, so it answers what the fight could
// say at its best and never what it actually says in a duty. This runs the same pull
// twice: once with everything, once with only what the client can see on its own,
// and prints the difference. That difference is the reason the feed exists, and it is
// a number rather than an argument.
public static partial class Program
{
    private static int Coverage(string[] args)
    {
        if (args.Length < 2 || !File.Exists(args[1]))
        {
            Console.Error.WriteLine("usage: FrenAlerts.DevTools coverage <logfile> [meId] [mySlot]");
            return 1;
        }

        var me = args.Length > 2 ? Convert.ToUInt32(args[2], 16) : 0;
        var mySlot = args.Length > 3 ? args[3] : "";

        var reader = new LogReader();
        var events = EventOrder.Sorted(reader.Read(File.ReadLines(args[1]))).ToList();
        if (events.Count == 0)
        {
            Console.Error.WriteLine("nothing readable in that file");
            return 1;
        }

        var territory = (ushort)events.FirstOrDefault(e => e.Kind == EventKind.ZoneChange).Id;
        Console.WriteLine($"territory     {territory} {Scope.GetValueOrDefault(territory, "unknown")}");
        Console.WriteLine($"events        {events.Count}");
        Console.WriteLine();

        var withParser = Say(events, territory, me, mySlot, reader);
        var onItsOwn = Say(events.Where(ClientCanSee).ToList(), territory, me, mySlot, reader);

        Console.WriteLine($"{"",-22}{"with a parser",16}{"client alone",16}");
        Row("events reaching it", withParser.Events, onItsOwn.Events);
        Row("calls", withParser.Calls, onItsOwn.Calls);
        Row("personal calls", withParser.Personal, onItsOwn.Personal);
        Console.WriteLine();

        var lost = withParser.Texts
            .Where(p => !onItsOwn.Texts.ContainsKey(p.Key) || onItsOwn.Texts[p.Key] < p.Value)
            .OrderByDescending(p => p.Value - onItsOwn.Texts.GetValueOrDefault(p.Key))
            .ToList();

        Console.WriteLine($"quiet without a parser: {lost.Count} distinct calls");
        foreach (var (text, n) in lost.Take(Rows))
            Console.WriteLine($"  {n - onItsOwn.Texts.GetValueOrDefault(text),4} of {n,4}  {text}");
        if (lost.Count > Rows) Console.WriteLine($"  ... and {lost.Count - Rows} more not shown");

        return 0;
    }

    private static void Row(string label, int a, int b) =>
        Console.WriteLine($"{label,-22}{a,16}{b,16}");

    // What the client raises on its own, which is not the same as what a recording
    // holds.
    //
    // Head markers and tethers used to be the answer here and are not any more: both
    // arrive on a control packet the plugin hooks, with no limit on who they are
    // about. What is left is the statuses, and that one was measured rather than
    // reasoned about: 2,999 of the 37,269 statuses in a Dancing Mad pull sit on the
    // boss and its adds, and the poll walks the party only, so it never sees one.
    private static bool ClientCanSee(GameEvent e) => e.Kind switch
    {
        // Polled off each party member's own status list, ten times a second, so a
        // status on anything that is not a party member is invisible.
        EventKind.StatusGain or EventKind.StatusLose => ActorId.IsPlayer(e.TargetId),

        _ => true,
    };

    private static Heard Say(
        List<GameEvent> events, ushort territory, uint me, string mySlot, LogReader reader)
    {
        var engine = new TriggerEngine(new PlayerContext { MyId = me, MySlot = mySlot });
        engine.Party.Assign(me, mySlot);
        foreach (var (id, name) in reader.Names) engine.Actors.Remember(id, name);

        if (territory == DancingMad.Territory)
        {
            engine.AddRange(DancingMad.Triggers());
            engine.AddRange(DancingMad.AllSequences());
            engine.State.LearnPhases(DancingMad.PhaseChanges());
        }
        engine.AddRange(MarkerCalls.Triggers(territory, engine.Triggers));

        var calls = engine.Replay(events).ToList();

        return new Heard
        {
            Events = events.Count,
            Calls = calls.Count,
            Personal = calls.Count(c => c.Personal),
            Texts = calls.GroupBy(c => c.Text).ToDictionary(g => g.Key, g => g.Count()),
        };
    }

    private readonly record struct Heard
    {
        public int Events { get; init; }
        public int Calls { get; init; }
        public int Personal { get; init; }
        public Dictionary<string, int> Texts { get; init; }
    }
}
