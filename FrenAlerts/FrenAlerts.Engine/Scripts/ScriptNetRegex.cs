using Jint;
using System.Text;
using System.Text.RegularExpressions;
using Jint.Native.Object;

namespace FrenAlerts.Engine.Scripts;

// The regex a trigger's `netRegex` block compiles to, ported from theirs.
//
// Their triggers do not match on our events; they match on a parser's line, field
// by field, by position. So the layouts below are not a design decision, they are
// the format their fights were written against: which field of which line carries
// the ability id, which carries the caster, which carries the heading. Every index
// is theirs. Change one and a trigger stops firing with nothing to say why.
//
// The names of the capture groups matter as much as the positions: a trigger's own
// code reads `matches.sourceId` and `matches.count`, so these are the words their
// fights use.
public static class ScriptNetRegex
{
    private readonly record struct Field(int Index, string Name);

    private static readonly Dictionary<string, (string Code, Field[] Fields)> Layouts = new()
    {
        ["StartsUsing"] = ("20",
        [
            new(2, "sourceId"), new(3, "source"), new(4, "id"), new(5, "ability"),
            new(6, "targetId"), new(7, "target"), new(8, "castTime"),
            new(9, "x"), new(10, "y"), new(11, "z"), new(12, "heading"),
        ]),
        ["Ability"] = ("2[12]",
        [
            new(2, "sourceId"), new(3, "source"), new(4, "id"), new(5, "ability"),
            new(6, "targetId"), new(7, "target"),
        ]),
        ["GainsEffect"] = ("26",
        [
            new(2, "effectId"), new(3, "effect"), new(4, "duration"),
            new(5, "sourceId"), new(6, "source"), new(7, "targetId"), new(8, "target"),
            new(9, "count"),
        ]),
        ["LosesEffect"] = ("30",
        [
            new(2, "effectId"), new(3, "effect"),
            new(5, "sourceId"), new(6, "source"), new(7, "targetId"), new(8, "target"),
            new(9, "count"),
        ]),
        ["HeadMarker"] = ("27", [new(2, "targetId"), new(3, "target"), new(6, "id")]),
        ["Tether"] = ("35",
        [
            new(2, "sourceId"), new(3, "source"), new(4, "targetId"), new(5, "target"),
            new(8, "id"),
        ]),
        ["ActorControl"] = ("33",
        [
            new(2, "instance"), new(3, "command"),
            new(4, "data0"), new(5, "data1"), new(6, "data2"), new(7, "data3"),
        ]),
        ["AddedCombatant"] = ("03",
        [
            new(2, "id"), new(3, "name"), new(10, "npcBaseId"), new(17, "x"), new(18, "y"),
        ]),
        ["StartsUsingExtra"] = ("263",
        [
            new(2, "sourceId"), new(3, "id"),
            new(4, "x"), new(5, "y"), new(6, "z"), new(7, "heading"),
        ]),
        ["AbilityExtra"] = ("264",
        [
            new(2, "sourceId"), new(3, "id"), new(4, "globalEffectCounter"), new(5, "dataFlag"),
            new(6, "x"), new(7, "y"), new(8, "z"), new(9, "heading"),
        ]),
        ["ActorControlExtra"] = ("273",
        [
            new(2, "id"), new(3, "category"),
            new(4, "param1"), new(5, "param2"), new(6, "param3"), new(7, "param4"),
        ]),
        ["ActorMove"] = ("270",
        [
            new(2, "id"), new(3, "heading"), new(5, "moveType"),
            new(6, "x"), new(7, "y"), new(8, "z"),
        ]),
        ["ActorSetPos"] = ("271",
        [
            new(2, "id"), new(3, "heading"), new(6, "x"), new(7, "y"), new(8, "z"),
        ]),
        ["NameToggle"] = ("34",
        [
            new(2, "id"), new(3, "name"), new(4, "targetId"), new(5, "targetName"), new(6, "toggle"),
        ]),
        ["SpawnNpcExtra"] = ("272",
        [
            new(2, "id"), new(3, "parentId"), new(4, "tetherId"), new(5, "animationState"),
        ]),
        ["MapEffect"] = ("257",
        [
            new(2, "instance"), new(3, "flags"), new(4, "location"), new(5, "data0"),
        ]),
        // Not a parser line at all: their own code for a boss saying one of its
        // scripted lines, which no log format writes.
        ["NpcYell"] = ("NpcYell", [new(2, "sourceId"), new(3, "npcYellId")]),
    };

    // The memory reads a trigger can ask for by name rather than by position.
    private static readonly string[] PairKeys =
        ["BNpcID", "BNpcNameID", "PosX", "PosY", "PosZ", "Heading", "CurrentHP", "MaxHP"];

    public const string CombatantMemory = "CombatantMemory";

    public static IReadOnlyCollection<string> KnownTypes => Layouts.Keys;

    // Which line codes a trigger of this type could ever match, so a line only ever
    // reaches the triggers that could want it.
    public static string[] LineCodesFor(string type)
    {
        if (type == CombatantMemory) return ["261"];
        if (!Layouts.TryGetValue(type, out var layout)) return [];
        return layout.Code == "2[12]" ? ["21", "22"] : [layout.Code];
    }

    public static Regex? Build(string type, ObjectInstance net)
    {
        if (type == CombatantMemory) return BuildCombatantMemory(net);
        if (!Layouts.TryGetValue(type, out var layout)) return null;

        var last = 0;
        foreach (var field in layout.Fields)
            if (field.Index > last) last = field.Index;

        var byIndex = new Dictionary<int, Field>();
        foreach (var field in layout.Fields) byIndex[field.Index] = field;

        var pattern = new StringBuilder();
        pattern.Append('^').Append(layout.Code);
        for (var i = 1; i <= last; i++)
        {
            pattern.Append("\\|");
            if (byIndex.TryGetValue(i, out var field))
                pattern.Append("(?<").Append(field.Name).Append('>')
                       .Append(Filter(net, field.Name) ?? "[^|]*").Append(')');
            else
                pattern.Append("[^|]*");
        }

        return new Regex(pattern.ToString(),
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    // A memory read is written as a list of key/value pairs in no fixed order, so it
    // is matched by lookahead rather than by counting fields.
    private static Regex? BuildCombatantMemory(ObjectInstance net)
    {
        var pattern = new StringBuilder();
        pattern.Append("^261\\|[^|]*\\|");
        pattern.Append("(?<change>").Append(Filter(net, "change") ?? "[^|]*").Append(")\\|");
        pattern.Append("(?<id>").Append(Filter(net, "id") ?? "[^|]*").Append(")\\|");

        var pairs = net.Get("pair");
        if (pairs.IsArray())
        {
            var array = pairs.AsArray();
            var length = (uint)array.Get("length").AsNumber();
            for (var i = 0u; i < length; i++)
            {
                var item = array.Get(i.ToString());
                if (!item.IsObject()) continue;

                var pair = item.AsObject();
                var key = pair.Get("key");
                if (!key.IsString()) continue;

                pattern.Append("(?=.*\\|").Append(Regex.Escape(key.AsString())).Append("\\|")
                       .Append(Filter(pair, "value") ?? "[^|]*").Append("\\|)");
            }
        }

        foreach (var key in PairKeys)
            pattern.Append("(?:(?=.*\\|").Append(key).Append("\\|(?<pair")
                   .Append(key).Append(">[^|]*)\\|))?");

        pattern.Append(".*");

        return new Regex(pattern.ToString(),
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    // What a trigger asked for on one field: a word, a list of words, or nothing,
    // which matches whatever is there.
    private static string? Filter(ObjectInstance net, string key)
    {
        var value = net.Get(key);
        if (value.IsString()) return Regex.Escape(value.AsString());

        if (!value.IsArray()) return null;

        var array = value.AsArray();
        var length = (uint)array.Get("length").AsNumber();
        var options = new List<string>();
        for (var i = 0u; i < length; i++)
        {
            var item = array.Get(i.ToString());
            if (item.IsString()) options.Add(Regex.Escape(item.AsString()));
        }

        return options.Count > 0 ? "(?:" + string.Join("|", options) + ")" : null;
    }
}
