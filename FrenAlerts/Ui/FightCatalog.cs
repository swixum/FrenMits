using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FrenAlerts.Engine;

namespace FrenAlerts.Ui;

public sealed record FightEntry(
    string Name, string Full, string Category, uint TerritoryId, int Calls, int BuiltIn);

public sealed record CallEntry(
    string Key, string Text, CallLevel Level, float Hold, int Phase,
    bool ShipsOn, EventKind On, uint MatchId);

public static class FightCatalog
{
    public const string DefaultCategory = "Ultimate";

    // A pack far bigger than the real one is a broken file, not a big raid tier.
    private const int MaxPackLines = 20_000;

    // The sidebar's groups, in the order they are shown.
    private static readonly string[] Order = { "Ultimate", "Savage", "Other" };

    private static readonly Dictionary<uint, (string Name, string Full, string Category)> Known = new()
    {
        [1226] = ("M1S", "", "Savage"),
        [1228] = ("M2S", "", "Savage"),
        [1230] = ("M3S", "", "Savage"),
        [1232] = ("M4S", "", "Savage"),
        [1238] = ("FRU", "Futures Rewritten", "Ultimate"),
        [1257] = ("M5S", "", "Savage"),
        [1259] = ("M6S", "", "Savage"),
        [1261] = ("M7S", "", "Savage"),
        [1263] = ("M8S", "", "Savage"),
        [1321] = ("M9S", "", "Savage"),
        [1323] = ("M10S", "", "Savage"),
        [1325] = ("M11S", "", "Savage"),
        [1327] = ("M12S", "Lindwurm", "Savage"),
        [1363] = ("DMU", "Dancing Mad", "Ultimate"),
    };

    // Swapped whole rather than edited in place, so a draw reading the list never
    // sees it half filled while the pack lands on another thread.
    private static IReadOnlyList<FightEntry> _entries = Array.Empty<FightEntry>();
    private static IReadOnlyDictionary<uint, IReadOnlyList<CallEntry>> _calls =
        new Dictionary<uint, IReadOnlyList<CallEntry>>();
    private static IReadOnlyDictionary<string, string> _shipped =
        new Dictionary<string, string>();
    private static IReadOnlyDictionary<string, string> _keyOf =
        new Dictionary<string, string>();
    private static bool _asked;

    public static IReadOnlyList<CallEntry> CallsIn(uint territory)
    {
        Ensure();
        return _calls.TryGetValue(territory, out var list) ? list : [];
    }

    public static string ShippedText(string key)
    {
        Ensure();
        return _shipped.TryGetValue(key, out var text) ? text : "";
    }

    public static FightEntry? At(uint territory)
    {
        Ensure();
        foreach (var f in _entries) if (f.TerritoryId == territory) return f;
        return null;
    }

    public static string CallOf(string triggerId)
    {
        Ensure();
        return _keyOf.TryGetValue(triggerId, out var key) ? key : "";
    }

    public static IReadOnlyList<FightEntry> All { get { Ensure(); return _entries; } }

    // Only the groups that have something in them, in the order above.
    public static IEnumerable<string> Categories
    {
        get
        {
            Ensure();
            var have = _entries;
            return Order.Where(c => have.Any(f => f.Category == c));
        }
    }

    public static IEnumerable<FightEntry> In(string category)
        => All.Where(f => f.Category == category);

    public static int CountIn(string category)
        => All.Count(f => f.Category == category);

    private static void Ensure()
    {
        if (_asked) return;
        _asked = true;
        _entries = Build([]);
        _ = Task.Run(LoadPack);
    }

    private static void LoadPack()
    {
        try
        {
            _entries = Build(ReadPack().ToList());
        }
        catch (Exception ex)
        {
            PackProblem = "The call pack would not read, so only the built-in fights call.";
            Service.Log.Error(ex, "Fren Alerts: the call pack would not read into the fight list");
        }
    }

    private static IEnumerable<CallSpec> ReadPack()
    {
        var dir = Service.PluginInterface.AssemblyLocation.Directory?.FullName;
        var path = dir is null ? null : Path.Combine(dir, "calls.facall");
        if (path is null || !File.Exists(path))
        {
            PackProblem = "The call pack is missing, so only the built-in fights call.";
            return [];
        }
        return CallPack.ReadAll(File.ReadLines(path).Take(MaxPackLines)).ToList();
    }

    private static IReadOnlyList<FightEntry> Build(IReadOnlyList<CallSpec> pack)
    {
        var modules = new Dictionary<uint, List<Trigger>>();
        Module(modules, DancingMad.Territory, DancingMad.Triggers);
        Module(modules, FuturesRewritten.Territory, FuturesRewritten.Triggers);
        Module(modules, Lindwurm.Territory, Lindwurm.Triggers);

        var calls = new Dictionary<uint, IReadOnlyList<CallEntry>>();
        var shipped = new Dictionary<string, string>();
        var keyOf = new Dictionary<string, string>();
        var list = new List<FightEntry>(Known.Count);
        var territories = modules.Keys.Union(pack.Select(s => (uint)s.Territory));
        foreach (var territory in territories)
        {
            var mine = modules.TryGetValue(territory, out var m) ? m : [];
            var loaded = pack.Count == 0
                ? []
                : Loaded(pack, (ushort)territory, mine, keyOf);

            // Hand written calls belong in the list with the rest of them. They were
            // counted and not shown, so authoring a mechanic took it off the page:
            // Dancing Mad went from 157 rows to 33 and read as an empty fight.
            var built = Written(mine, keyOf);
            var all = built.Concat(loaded).ToList();
            calls[territory] = all;
            foreach (var c in all) shipped[c.Key] = c.Text;

            // Specs that share a key are one call, so the fight's number is the
            // number of lines it can put on screen, not the number of rows a file
            // happens to carry.
            if (all.Count == 0) continue;

            var (name, full, category) = Known.TryGetValue(territory, out var k)
                ? k : ($"Territory {territory}", "", "Other");
            // Nothing is hidden from the page any more, so nothing is counted as
            // hidden either.
            list.Add(new FightEntry(name, full, category, territory, all.Count, 0));
        }
        _calls = calls;
        _shipped = shipped;
        _keyOf = keyOf;

        list.Sort(static (a, b) =>
        {
            var by = Array.IndexOf(Order, a.Category).CompareTo(Array.IndexOf(Order, b.Category));
            return by != 0 ? by : a.TerritoryId.CompareTo(b.TerritoryId);
        });
        return list;
    }

    // The hand written calls for one fight, as list rows.
    //
    // What a trigger says is only known by asking it, so each one is run once
    // against an empty event. A trigger that declines that (the catch-alls answer
    // only for a marker they do not already name) still gets a row, under its own
    // id, because a call you cannot switch off is worse than one that reads plainly.
    private static IReadOnlyList<CallEntry> Written(
        List<Trigger> mine, Dictionary<string, string> keyOf)
    {
        var list = new List<CallEntry>(mine.Count);
        var seen = new HashSet<string>();
        foreach (var t in mine)
        {
            if (!seen.Add(t.Id)) continue;
            keyOf[t.Id] = t.Id;

            string text;
            float hold;
            CallLevel level;
            try
            {
                var sample = t.Make(Blank(t));
                text = sample?.Text ?? Readable(t.Id);
                hold = sample?.Hold ?? 4f;
                level = sample?.Level ?? CallLevel.Info;
            }
            catch
            {
                text = Readable(t.Id);
                hold = 4f;
                level = CallLevel.Info;
            }

            // Written by hand, so it says what it means at the moment it means it.
            list.Add(new CallEntry(t.Id, text, level, hold, t.Phase, t.Enabled, t.On, t.MatchId));
        }
        return list;
    }

    private static TriggerContext Blank(Trigger t) => new(
        new GameEvent { Kind = t.On, Time = 0, Id = t.MatchId },
        new PlayerContext(), new ActorBook(), new PartyContext(), new FightState());

    private static string Readable(string id) => id.Replace('-', ' ');

    // The pack's calls for one fight, as they will actually load: built through
    // the engine's own filter so a spec the module already covers is not listed
    // as something the player can edit, then grouped so one mechanic is one line.
    private static IReadOnlyList<CallEntry> Loaded(
        IReadOnlyList<CallSpec> pack, ushort territory, List<Trigger> modules,
        Dictionary<string, string> keyOf)
    {
        var kept = TriggerPack.Build(pack, territory, modules).Select(t => t.Id).ToHashSet();
        var seen = new HashSet<string>();
        var list = new List<CallEntry>();
        foreach (var s in pack)
        {
            if (s.Territory != territory || !kept.Contains(s.Id)) continue;
            keyOf[s.Id] = s.DedupeKey;
            if (!seen.Add(s.DedupeKey)) continue;
            list.Add(new CallEntry(s.DedupeKey, s.Text, s.Level, s.Hold, s.Phase,
                s.DefaultOn, s.On, s.MatchId));
        }
        return list;
    }

    // One fight that throws leaves itself out rather than taking the list, and
    // the window, down with it.
    private static void Module(Dictionary<uint, List<Trigger>> into, ushort territory,
        Func<IEnumerable<Trigger>> triggers)
    {
        try
        {
            into[territory] = triggers().ToList();
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"territory {territory} would not build into the fight list");
        }
    }

    public static string? PackProblem { get; private set; }
}
