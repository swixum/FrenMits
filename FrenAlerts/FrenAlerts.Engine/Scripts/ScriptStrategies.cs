using Jint;
using Jint.Native.Object;

namespace FrenAlerts.Engine.Scripts;

// Which way a fight is called, when their file knows more than one.
//
// Several of their fights ship a list of strategies: which arrow pattern the group
// runs in phase one, which Forsaken order, which black hole order, who takes which
// gaol. Their triggers read the answer straight out of `data.triggerSetConfig`, so a
// host that never fills it in silently runs whatever they defaulted to and calls a
// different strat than the group is running.
//
// That is the worst kind of wrong for a boss mod: every call is confident, on time,
// and points at the wrong tower.
public sealed record ScriptStrategyOption(string Value, string Label);

public sealed record ScriptStrategy(
    string Id, string Name, string Default, IReadOnlyList<ScriptStrategyOption> Options);

public static class ScriptStrategies
{
    // Where their triggers read the answers back.
    public const string Field = "triggerSetConfig";

    // Every choice one of their fights offers, in the order the file lists them.
    public static IReadOnlyList<ScriptStrategy> Read(Jint.Engine js, int setIndex)
    {
        var found = new List<ScriptStrategy>();

        var set = js.Evaluate($"triggerSets[{setIndex}]");
        if (!set.IsObject()) return found;

        var config = set.AsObject().Get("config");
        if (!config.IsArray()) return found;

        var array = config.AsArray();
        var count = (uint)array.Get("length").AsNumber();

        for (var i = 0u; i < count; i++)
        {
            var item = array.Get(i.ToString());
            if (!item.IsObject()) continue;

            var entry = item.AsObject();
            var id = Str(entry, "id");
            if (string.IsNullOrEmpty(id)) continue;

            var options = ReadOptions(entry);
            var fallback = Str(entry, "default") ?? "";

            // Their rule: a choice with no default named takes the first option, so a
            // fight always has an answer rather than an empty string their triggers
            // would compare against and never match.
            if (string.IsNullOrEmpty(fallback) && options.Count > 0) fallback = options[0].Value;

            found.Add(new ScriptStrategy(id, Str(entry, "name") ?? id, fallback, options));
        }

        found.AddRange(Seeded(js, setIndex, found));
        return found;
    }

    // The knobs a fight seeds into its own state without declaring them.
    //
    // The Weapon's Refrain is the case: its file writes out twenty gaol order slots
    // and every gaol call reads them, but the list of them never reaches the fight
    // kit, so nothing offers them and all twenty stay empty forever. A key the fight
    // seeds is a key the fight reads, so it belongs on the list either way.
    private static IEnumerable<ScriptStrategy> Seeded(
        Jint.Engine js, int setIndex, List<ScriptStrategy> declared)
    {
        var found = new List<ScriptStrategy>();

        try
        {
            var state = js.Evaluate($"triggerSets[{setIndex}].initData()");
            if (!state.IsObject()) return found;

            var config = state.AsObject().Get(Field);
            if (!config.IsObject()) return found;

            foreach (var (key, property) in config.AsObject().GetOwnProperties())
            {
                var id = key.ToString();
                if (string.IsNullOrEmpty(id)) continue;
                if (declared.Exists(s => s.Id == id)) continue;

                var value = property.Value;
                found.Add(new ScriptStrategy(id, Readable(id), value.IsString() ? value.AsString() : "", []));
            }
        }
        catch
        {
            // A fight whose state cannot be built offline has nothing to add here,
            // and its declared choices are already on the list.
        }

        return found;
    }

    // A seeded key has no name of its own, and the key is a variable name. UCOB's one
    // setting read "heavensfallTowerPosition" at the top of its page, which is a line
    // of somebody's source code where the fight's most important question should be.
    public static string Readable(string id)
    {
        if (id.Length == 0) return id;

        var words = new System.Text.StringBuilder(id.Length + 8);
        words.Append(char.ToUpperInvariant(id[0]));
        for (var i = 1; i < id.Length; i++)
        {
            // A digit starts a word too, or "gaolOrder20" comes out as "Gaol Order20".
            var breaks = char.IsUpper(id[i]) || (char.IsDigit(id[i]) && !char.IsDigit(id[i - 1]));
            if (breaks && id[i - 1] != ' ') words.Append(' ');
            words.Append(id[i]);
        }
        return words.ToString();
    }

    // ---- the same question, numbered ----

    // The part of a name before its trailing number, or nothing where it has none.
    // "Titan Gaol Order 1" is "Titan Gaol Order"; "Nael Dive Marker" is nothing.
    public static string? Prefix(string name)
    {
        var cut = name.LastIndexOf(' ');
        if (cut <= 0 || cut == name.Length - 1) return null;
        var tail = name[(cut + 1)..];
        return tail.All(char.IsDigit) ? name[..cut] : null;
    }

    // Just the trailing number, for a row whose run already carries the rest of it.
    public static string NumberOf(string name) =>
        Prefix(name) is null ? name : name[(name.LastIndexOf(' ') + 1)..];

    // Neighbouring settings that are one question numbered, in the file's own order.
    //
    // Here rather than in the window because it is an answer about their files, and
    // because the order is load-bearing: the number IS the answer for a gaol order, so
    // a run can never be sorted or regrouped, only folded away. Twenty of these opened
    // UWU's page on a wall of text boxes with the fight's calls below the window.
    public static IReadOnlyList<IReadOnlyList<ScriptStrategy>> Runs(
        IReadOnlyList<ScriptStrategy> strategies)
    {
        var runs = new List<List<ScriptStrategy>>();
        foreach (var s in strategies)
        {
            var prefix = Prefix(s.Name);
            if (prefix is not null && runs.Count > 0 && Prefix(runs[^1][0].Name) == prefix)
            {
                runs[^1].Add(s);
                continue;
            }
            runs.Add([s]);
        }
        return runs;
    }

    private static List<ScriptStrategyOption> ReadOptions(ObjectInstance entry)
    {
        var options = new List<ScriptStrategyOption>();

        var list = entry.Get("options");
        if (!list.IsArray()) return options;

        var array = list.AsArray();
        var count = (uint)array.Get("length").AsNumber();

        for (var i = 0u; i < count; i++)
        {
            var item = array.Get(i.ToString());
            if (!item.IsObject()) continue;

            var option = item.AsObject();
            var value = Str(option, "value");
            if (value is null) continue;

            options.Add(new ScriptStrategyOption(value, Str(option, "label") ?? value));
        }

        return options;
    }

    // Writes the answers where their triggers read them. Anything not chosen takes
    // the fight's own default, so the table is always complete.
    public static void Apply(
        Jint.Engine js, IReadOnlyList<ScriptStrategy> strategies,
        IReadOnlyDictionary<string, string>? chosen = null)
    {
        js.Execute($"__data.{Field} = __data.{Field} || {{}};");

        foreach (var strategy in strategies)
        {
            var value = chosen is not null && chosen.TryGetValue(strategy.Id, out var picked) && picked.Length > 0
                ? picked
                : strategy.Default;

            // An answer their file does not offer falls back to the default rather
            // than being written in: the page drops an unoffered pick from what it
            // draws, so writing one would run a strat nobody can see, and a patch that
            // failed to load is exactly how a pick stops being offered.
            if (strategy.Options.Count > 0 && OptionAt(strategy, value) < 0)
                value = strategy.Default;

            js.Execute($"__data.{Field}[{Quote(strategy.Id)}] = {Quote(value)};");
        }
    }

    // Which of a choice's options an answer is, or -1 for an answer that is none of them.
    //
    // The page used to walk the options looking for a match and leave the index at 0 when
    // it found none, so a saved answer their file no longer offers drew as the first
    // option, selected, with the dot beside it that means somebody chose this. What the
    // fight got was the saved value, which their triggers compare against and none of
    // them match, so the mechanic takes whatever branch their else covers. Confident, on
    // time, and a different strat than the one on screen: the exact failure this whole
    // page exists to prevent, arrived at from the other side.
    //
    // Reachable because their fight files are replaced wholesale with newer copies, which
    // is what the patches folder exists to survive. An option value renamed upstream
    // turns every config that had picked it into this.
    //
    // Their own default answers the same way, so a fight that ships a default outside its
    // own option list is caught here too rather than drawing as option zero.
    public static int OptionAt(ScriptStrategy strategy, string chosen)
    {
        var want = chosen.Length > 0 ? chosen : strategy.Default;

        for (var i = 0; i < strategy.Options.Count; i++)
            if (strategy.Options[i].Value == want) return i;

        return -1;
    }

    // What an imported fight answers for one of its own questions, for the calls of
    // ours that ask the same thing.
    //
    // Their default counts. A row left alone writes nothing to disk and still means
    // the thing it is showing, so reading only what was saved would say "no answer"
    // about the row swix is looking at. Empty means they do not ask this at all, which
    // is the one case where our own answer is the only one there is.
    public static string Answer(
        IReadOnlyList<ScriptStrategy> asked, string key, string chosen)
    {
        if (asked is null || string.IsNullOrWhiteSpace(key)) return "";

        foreach (var one in asked)
        {
            if (one.Id != key) continue;
            return chosen.Length > 0 ? chosen : one.Default;
        }
        return "";
    }

    // What a fight is set to right now, read back out of their own state.
    public static string Current(Jint.Engine js, string id)
    {
        var value = js.Evaluate($"(__data.{Field} || {{}})[{Quote(id)}]");
        return value.IsString() ? value.AsString() : "";
    }

    private static string? Str(ObjectInstance o, string key)
    {
        var value = o.Get(key);
        return value.IsString() ? value.AsString() : null;
    }

    // A label of theirs can hold an apostrophe, which unescaped would end the string
    // and change the program.
    private static string Quote(string s) =>
        "'" + (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'") + "'";
}
