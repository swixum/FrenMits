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
                found.Add(new ScriptStrategy(id, id, value.IsString() ? value.AsString() : "", []));
            }
        }
        catch
        {
            // A fight whose state cannot be built offline has nothing to add here,
            // and its declared choices are already on the list.
        }

        return found;
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

            js.Execute($"__data.{Field}[{Quote(strategy.Id)}] = {Quote(value)};");
        }
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
