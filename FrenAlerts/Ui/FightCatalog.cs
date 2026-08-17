using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FrenAlerts.Engine;

namespace FrenAlerts.Ui;

public sealed record FightEntry(
    string Name, string Full, string Category, uint TerritoryId, int Calls,
    string Expansion = "");

// Text is the one line this call ships with, and is what an edit is measured
// against. OnYou is the other thing it says when it lands on this player, empty
// where the call says the same either way, and is for the row label only: putting
// it in Text would hand the editor two lines to save as one.
public sealed record CallEntry(
    string Key, string Text, CallLevel Level, float Hold, int Phase,
    bool ShipsOn, EventKind On, uint MatchId, bool Sampled = true, string OnYou = "",
    bool FromTimeline = false,
    // Written here rather than imported.
    bool Written = false,
    // Shown on a fight the imported set also covers. Opted into per call, because
    // nearly every call written here answers an ability theirs already answers and
    // listing all of them put forty near-duplicate rows on that page.
    bool Listed = false);

public static class FightCatalog
{
    public const string DefaultCategory = "Ultimate";

    // A pack far bigger than the real one is a broken file, not a big raid tier.
    private const int MaxPackLines = 20_000;

    // The same bound for the timelines, which run a few hundred lines a fight.
    private const int MaxTimelineLines = 20_000;

    // The sidebar's groups, in the order they are shown.
    private static readonly string[] Order = { "Ultimate", "Savage", "Other" };

    // What a territory is called on the page.
    //
    // Shipped is the only list: the page used to keep its own copy of the fourteen
    // fights beside it, and the two disagreed the moment either was edited. A
    // territory it does not name still gets a page rather than being dropped, so a
    // pack carrying a fight nobody listed is visible instead of silently missing.
    private static (string Name, string Full, string Category, string Expansion) NameOf(uint territory) =>
        Shipped.At((ushort)territory) is { } f
            ? (f.Name, f.Full, f.Category, f.Expansion)
            : ($"Territory {territory}", "", "Other", "");

    // Everything one build produces, published as a single reference.
    //
    // These were four fields, assigned one after another, under a comment saying a draw
    // never sees the list half filled while the pack lands on another thread. That was
    // true of each field and false across them.
    //
    // Two builds really do run at once: the pack lands on its own thread and calls Build,
    // and the fight page calls ReadAs on the frame thread, which rebuilds the first time
    // it is asked. Interleaved, the page can take its fight list from one build and its
    // calls from the other, and a fight then reads "157 calls" with nothing under it.
    // That is the symptom this file already carries a comment about, arrived at from a
    // second direction.
    //
    // One reference means the four cannot disagree: a reader has the whole of one build
    // or the whole of the other, and a reference assignment cannot be seen half done.
    private sealed record Catalog(
        IReadOnlyList<FightEntry> Entries,
        IReadOnlyDictionary<uint, IReadOnlyList<CallEntry>> Calls,
        IReadOnlyDictionary<string, string> Shipped,
        IReadOnlyDictionary<string, string> KeyOf);

    private static Catalog _catalog = new(
        Array.Empty<FightEntry>(),
        new Dictionary<uint, IReadOnlyList<CallEntry>>(),
        new Dictionary<string, string>(),
        new Dictionary<string, string>());

    private static IReadOnlyDictionary<uint, int> _mechanics = new Dictionary<uint, int>();

    // Per fight, the second each timeline mechanic lands. Read once with the
    // timelines and used only to order the rows.
    private static IReadOnlyDictionary<uint, MechanicClock> _whenBy =
        new Dictionary<uint, MechanicClock>();

    // Kept so a strat change can re-sample the calls without reading the file again.
    private static List<CallSpec> _pack = [];

    private static string _slot = "";

    private static Func<ushort, string, string>? _strat;
    private static bool _asked;

    public static IReadOnlyList<CallEntry> CallsIn(uint territory)
    {
        Ensure();
        return _catalog.Calls.TryGetValue(territory, out var list) ? list : [];
    }

    public static string ShippedText(string key)
    {
        Ensure();
        return _catalog.Shipped.TryGetValue(key, out var text) ? text : "";
    }

    public static FightEntry? At(uint territory)
    {
        Ensure();
        foreach (var f in _catalog.Entries) if (f.TerritoryId == territory) return f;
        return null;
    }

    public static string CallOf(string triggerId)
    {
        Ensure();
        return _catalog.KeyOf.TryGetValue(triggerId, out var key) ? key : "";
    }

    public static IReadOnlyList<FightEntry> All { get { Ensure(); return _catalog.Entries; } }

    // Only the groups that have something in them, in the order above.
    public static IEnumerable<string> Categories
    {
        get
        {
            Ensure();
            var have = _catalog.Entries;
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
        _catalog = Build([]);
        _ = Task.Run(LoadPack);
    }

    // Who to read the calls as. Handed in by the page rather than reached for, so
    // the engine's own player stays the only thing the calls actually fire against.
    //
    // A rebuild only when one of them really moved: this is called every frame the
    // fight page draws, and re-running every trigger in the game per frame would be
    // a stutter nobody could explain.
    public static void ReadAs(string slot, Func<ushort, string, string> strat)
    {
        var same = string.Equals(slot, _slot, StringComparison.Ordinal) && _strat is not null;
        _slot = slot;
        _strat = strat;
        if (!same) Rebuild();
    }

    // The group changed an answer, so every call that reads one says something else
    // now. Cheap: the pack is already in memory, only the sampling runs again.
    public static void Invalidate() => Rebuild();

    private static void Rebuild()
    {
        if (!_asked) return;
        try
        {
            _catalog = Build(_pack);
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Fren Alerts: the fight list would not rebuild");
        }
    }

    private static void LoadPack()
    {
        // Timelines first, because the build puts the rows in the order they say.
        // Its own try, so a timeline that will not read costs the page its ordering
        // and a line of detail rather than the whole fight list.
        try
        {
            _mechanics = ReadTimelines();
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Fren Alerts: the timelines would not read into the fight list");
        }

        try
        {
            _pack = ReadPack().ToList();
            _catalog = Build(_pack);
        }
        catch (Exception ex)
        {
            PackProblem = "The call pack would not read, so only the built-in fights call.";
            Service.Log.Error(ex, "Fren Alerts: the call pack would not read into the fight list");
        }
    }

    // How many mechanics the shipped timeline lists per fight.
    //
    // A count, never a ratio: one call can cover several timeline entries and the
    // timeline lists things nobody would ever call, so "157 of 405 covered" would be
    // a precise-looking number that is not true. How long the fight is and how much
    // of it has been written are both worth knowing; the arithmetic between them is
    // not.
    private static IReadOnlyDictionary<uint, int> ReadTimelines()
    {
        var dir = Service.PluginInterface.AssemblyLocation.Directory?.FullName;
        var path = dir is null ? null : Path.Combine(dir, "timelines.fatime");
        if (path is null || !File.Exists(path)) return new Dictionary<uint, int>();

        var packs = TimelinePack.ReadAll(File.ReadLines(path).Take(MaxTimelineLines));

        // The same read gives every mechanic its second, which is what puts the
        // call rows in the order the fight happens in.
        _whenBy = packs.ToDictionary(t => (uint)t.Key, t => new MechanicClock(t.Value.Entries));
        return packs.ToDictionary(t => (uint)t.Key, t => t.Value.Entries.Count);
    }

    // The rows in the order the fight puts them, so a reader can follow it down the
    // page instead of meeting every hand written call and then every packed one.
    //
    // Phase first, because a phase is the one ordering the fight itself guarantees
    // and every row carries it. Then the second the mechanic lands, off the shipped
    // timeline. A call whose mechanic is not on the timeline keeps its old place at
    // the end of its own phase: unknown is not zero, and floating it to the top
    // would read as "this happens first".
    private static IReadOnlyList<CallEntry> InFightOrder(
        uint territory, List<CallEntry> all)
    {
        if (!_whenBy.TryGetValue(territory, out var clock) || clock.Count == 0) return all;

        var at = new float[all.Count];
        for (var i = 0; i < all.Count; i++) at[i] = clock.WhenOf(all[i].Key);

        return all
            .Select((c, i) => (Call: c, At: at[i], Was: i))
            .OrderBy(r => r.Call.Phase == 0 ? int.MaxValue : r.Call.Phase)
            .ThenBy(r => r.At)
            .ThenBy(r => r.Was)
            .Select(r => r.Call)
            .ToList();
    }

    public static int MechanicsIn(uint territory)
    {
        Ensure();
        return _mechanics.TryGetValue(territory, out var n) ? n : 0;
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

    private static Catalog Build(IReadOnlyList<CallSpec> pack)
    {
        var modules = new Dictionary<uint, List<Trigger>>();
        Module(modules, DancingMad.Territory, DancingMad.Triggers);
        var sequences = new Dictionary<uint, List<SequenceTrigger>>
        {
            [DancingMad.Territory] = DancingMad.AllSequences().ToList(),
        };
        // Every fight FightLoader builds a module for has to be registered here too,
        // or its hand written calls are neither listed nor switchable: the page only
        // shows what this dictionary knows about. That has already gone wrong once,
        // when authoring a mechanic quietly took it off the page.
        Module(modules, UnendingCoil.Territory, UnendingCoil.Triggers);
        Module(modules, WeaponsRefrain.Territory, WeaponsRefrain.Triggers);
        Module(modules, VampFatale.Territory, VampFatale.Triggers);
        Module(modules, RedHotDeepBlue.Territory, RedHotDeepBlue.Triggers);
        Module(modules, TyrantComet.Territory, TyrantComet.Triggers);
        Module(modules, Lindwurm.Territory, Lindwurm.Triggers);

        var calls = new Dictionary<uint, IReadOnlyList<CallEntry>>();
        var shipped = new Dictionary<string, string>();
        var keyOf = new Dictionary<string, string>();
        var list = new List<FightEntry>(Shipped.Fights.Count);
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
            var built = Written(mine, keyOf, (ushort)territory)
                .Concat(Ordered(sequences.GetValueOrDefault(territory) ?? [], keyOf))
                .Concat(Ahead((ushort)territory, keyOf))
                .ToList();
            var all = InFightOrder(territory, built.Concat(loaded).ToList());
            calls[territory] = all;
            foreach (var c in all) shipped[c.Key] = c.Text;

            // Specs that share a key are one call, so the fight's number is the
            // number of lines it can put on screen, not the number of rows a file
            // happens to carry.
            if (all.Count == 0) continue;

            var (name, full, category, expansion) = NameOf(territory);
            // Nothing is hidden from the page any more, so nothing is counted as
            // hidden either.
            list.Add(new FightEntry(name, full, category, territory, all.Count, expansion));
        }
        // Category, then newest expansion, then release order inside it. Territory ids
        // climb with the patch, so ascending inside one expansion is release order and
        // needs no second list to maintain.
        list.Sort(static (a, b) =>
        {
            var by = Array.IndexOf(Order, a.Category).CompareTo(Array.IndexOf(Order, b.Category));
            if (by != 0) return by;
            by = Shipped.ExpansionRank(a.Expansion).CompareTo(Shipped.ExpansionRank(b.Expansion));
            return by != 0 ? by : a.TerritoryId.CompareTo(b.TerritoryId);
        });

        // Handed back rather than written into the fields, so the only place a build is
        // published is the one line that assigns it.
        return new Catalog(list, calls, shipped, keyOf);
    }

    // The hand written calls for one fight, as list rows.
    //
    // What a trigger says is only known by asking it, so each one is run once
    // against an empty event. A trigger that declines that (the catch-alls answer
    // only for a marker they do not already name) still gets a row, under its own
    // id, because a call you cannot switch off is worse than one that reads plainly.
    private static IReadOnlyList<CallEntry> Written(
        List<Trigger> mine, Dictionary<string, string> keyOf, ushort territory)
    {
        var list = new List<CallEntry>(mine.Count);
        var seen = new HashSet<string>();
        foreach (var t in mine)
        {
            // A claim holds an event so the pack stays quiet on it. There is no call
            // in it, so a row for it reads as a mechanic with its own id for words.
            if (t.Claims) continue;
            if (!seen.Add(t.Id)) continue;
            keyOf[t.Id] = t.Id;

            string text;
            float hold;
            CallLevel level;
            bool sampled;
            var onYou = "";
            try
            {
                var sample = t.Make(Blank(t, territory));
                sampled = !string.IsNullOrWhiteSpace(sample?.Text);
                // The same trigger asked as the call landing on this player. A
                // buster names whoever it hit, so one run only ever hears the half
                // about somebody else: "Tank Cleave" for a call whose other half is
                // "Tank Cleave on YOU". Skipped where the blank already aimed at
                // this player, because there is no second answer to find.
                if (sampled && !t.OnlyMe && t.Aim != Aim.Me)
                {
                    var yours = t.Make(AtMe(t, territory))?.Text;
                    if (!string.IsNullOrWhiteSpace(yours) && yours != sample!.Text) onYou = yours!;
                }
                // Asked, then told, then the id as a last resort. A call that reads
                // the pull cannot answer here, but its author knows what it sounds
                // like, and a player choosing what to switch off needs the words
                // rather than a slug.
                text = sampled ? sample!.Text
                    : t.Says.Length > 0 ? t.Says
                    : Readable(t.Id);
                hold = sample?.Hold ?? 4f;
                level = sample?.Level ?? CallLevel.Info;
            }
            catch
            {
                sampled = false;
                onYou = "";
                text = Readable(t.Id);
                hold = 4f;
                level = CallLevel.Info;
            }

            // Written by hand, so it says what it means at the moment it means it.
            list.Add(new CallEntry(
                t.Id, text, level, hold, t.Phase, t.Enabled, t.On, t.MatchId, sampled, onYou,
                Written: true, Listed: t.Listed));
        }
        return list;
    }

    // Sequences say one thing when two events land in order, and they were left off
    // the page entirely, so the call they make could be neither seen nor switched off.
    private static IReadOnlyList<CallEntry> Ordered(
        List<SequenceTrigger> steps, Dictionary<string, string> keyOf)
    {
        var list = new List<CallEntry>(steps.Count);
        var seen = new HashSet<string>();
        foreach (var q in steps)
        {
            string text;
            CallLevel level;
            string key;
            try
            {
                var sample = q.Make(BlankFor(q.ThenOn, q.ThenId));
                text = sample?.Text ?? Readable(q.Id);
                level = sample?.Level ?? CallLevel.Info;
                key = sample?.Key is { Length: > 0 } k ? k : q.Id;
            }
            catch
            {
                text = Readable(q.Id);
                level = CallLevel.Info;
                key = q.Id;
            }

            // Sixteen portent pairs are one mechanic and one switch, so they group by
            // the call's key rather than each landing its own row on the page.
            keyOf[q.Id] = key;
            if (!seen.Add(key)) continue;

            list.Add(new CallEntry(key, text, level, 4f, q.Phase, true, q.ThenOn, q.ThenId,
                Written: true));
        }
        return list;
    }

    // The calls that come off the timeline rather than off an event.
    //
    // A fourth source of rows, and the page knew nothing about it: UWU's three
    // timeline calls fired in the fight and appeared nowhere on the page, so they
    // could not be found, switched off, or reworded. A call you cannot switch off
    // is the one thing this page must never have.
    //
    // Their words are fixed, so there is nothing to sample: what a timeline call
    // says is written down beside how long before the mechanic it says it.
    private static IReadOnlyList<CallEntry> Ahead(ushort territory, Dictionary<string, string> keyOf)
    {
        var list = new List<CallEntry>();
        var seen = new HashSet<string>();
        foreach (var c in TimelineCaller.Shipped)
        {
            if (c.Territory != territory) continue;
            if (!seen.Add(c.Key)) continue;
            keyOf[c.Key] = c.Key;
            list.Add(new CallEntry(
                c.Key, c.Text, c.Level, 4f, Phase: 0, ShipsOn: true,
                // No event brings one of these, which is the point of them. The kind
                // is what the page reads to warn that a call cannot fire, so a
                // timeline call is marked instead of being given a borrowed one.
                On: EventKind.ZoneChange, MatchId: 0, Sampled: true, OnYou: "",
                FromTimeline: true, Written: true));
        }
        return list;
    }

    private static TriggerContext BlankFor(EventKind kind, uint id) => new(
        new GameEvent { Kind = kind, Time = 0, Id = id },
        Asking(0), new ActorBook(), new PartyContext(), new FightState());

    // Who the page samples a call as.
    //
    // It used to be nobody: no slot, no strat, and an id of zero. Three things went
    // wrong at once. Every role call read its generic branch, so nine mechanics all
    // showed its generic half whoever was reading. Every strat call read
    // its fallback, so picking a strat changed the dropdown and nothing above it.
    // And "is this aimed at me" is an id comparison, so zero equalled zero and every
    // buster in the fight claimed to be on you.
    private static PlayerContext Asking(ushort territory) => new()
    {
        // Any id that is not the blank event's zero. What it is does not matter,
        // only that "aimed at me" stops being accidentally true for everything.
        MyId = Me,
        MySlot = _slot,
        Strat = key => _strat?.Invoke(territory, key) ?? "",
    };

    private const uint Me = 0x10000001;

    // A call that only ever fires on you is sampled as being on you; anything that
    // can land on anybody is sampled as landing on somebody else, which is the form
    // most of the party hears most of the time.
    private static TriggerContext Blank(Trigger t, ushort territory) => new(
        new GameEvent
        {
            Kind = t.On,
            Time = 0,
            Id = t.MatchId,
            TargetId = t.OnlyMe || t.Aim == Aim.Me ? Me : 0,
        },
        Asking(territory), new ActorBook(), new PartyContext(), new FightState());

    // The same event aimed at this player, for the second half of a call that names
    // who it landed on.
    private static TriggerContext AtMe(Trigger t, ushort territory) => new(
        new GameEvent { Kind = t.On, Time = 0, Id = t.MatchId, TargetId = Me },
        Asking(territory), new ActorBook(), new PartyContext(), new FightState());

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
            list.Add(new CallEntry(s.DedupeKey, s.Text,
                s.Level, s.Hold, s.Phase,
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
