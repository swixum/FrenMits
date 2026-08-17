using Acornima;
using Acornima.Ast;

namespace FrenAlerts.DevTools;

// Renames the imported fight files' own identifiers, leaving everything they say and
// everything they match untouched.
//
// Done by parsing rather than by pattern, because the same word is a variable in one
// line and a field name in the next, and a rename that catches the wrong one produces
// a fight that loads without complaint and calls the wrong thing. Every rename here
// comes off the syntax tree, and `verify` walks both trees in step afterwards to prove
// nothing but identifiers moved.
public static class Renamer
{
    // The two names the engine reaches into these files for. Everything else the host
    // owns is prefixed, so the prefix covers the rest.
    //
    // The language's own globals are not listed, deliberately: a list of them is a list
    // somebody forgets to add `Proxy` to, and `Proxy` renamed is a fight that loads,
    // registers, matches its line and then throws the first time that path runs. What
    // stands in for the list is Declared below, which only ever moves a name these
    // files themselves bring into being.
    private static readonly HashSet<string> Keep = new(StringComparer.Ordinal)
    {
        "triggerSets", "makeOutput",
    };

    private static bool Bridge(string name) => name.StartsWith("__", StringComparison.Ordinal);

    // Nothing may become one of these, whatever the table says. `var class = x` is a
    // syntax error, and the file it lands in is the file that stops loading.
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "break", "case", "catch", "class", "const", "continue", "debugger", "default",
        "delete", "do", "else", "enum", "export", "extends", "false", "finally", "for",
        "function", "if", "import", "in", "instanceof", "new", "null", "return", "super",
        "switch", "this", "throw", "true", "try", "typeof", "var", "void", "while",
        "with", "yield", "let", "static", "await", "async", "of", "get", "set",
    };

    private sealed class Corpus
    {
        public readonly List<(string Path, string Source, Script Tree)> Files = [];
        public readonly HashSet<string> Everything = new(StringComparer.Ordinal);
        public readonly HashSet<string> Blocked = new(StringComparer.Ordinal);
        public readonly HashSet<string> Exported = new(StringComparer.Ordinal);

        // Every name these files declare for themselves: a var, a function, a
        // parameter, a caught error. A name used but never declared belongs to
        // somebody else, whether that is the language or the host, and moving it is
        // how a rename breaks something that still parses.
        public readonly HashSet<string> Declared = new(StringComparer.Ordinal);
    }

    public static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: rename <folder> <map.tsv> [--apply]");
            return 2;
        }

        var folder = args[1];
        var mapPath = args[2];
        var apply = args.Contains("--apply");

        var corpus = Read(folder);
        var map = BuildMap(corpus);

        File.WriteAllLines(mapPath, map.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}\t{p.Value}"));

        var moved = 0;
        foreach (var (path, source, tree) in corpus.Files)
        {
            var rewritten = Rewrite(source, tree, map, corpus.Exported, out var count);
            moved += count;
            if (apply && count > 0) File.WriteAllText(path, rewritten);
        }

        Console.WriteLine($"{corpus.Files.Count} files, {map.Count} names, {moved} occurrences"
            + (apply ? " written" : " (dry run)"));
        return 0;
    }

    private static Corpus Read(string folder)
    {
        var corpus = new Corpus();
        var parser = new Parser();

        var paths = Directory.GetFiles(folder, "*.js", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal);

        foreach (var path in paths)
        {
            var source = File.ReadAllText(path);
            corpus.Files.Add((path, source, parser.ParseScript(source)));
        }

        foreach (var (_, _, tree) in corpus.Files) Survey(tree, corpus);
        return corpus;
    }

    // Everything the corpus already uses as a name of any kind, so a new name can be
    // checked against all of it, plus the two sets that decide what may move at all.
    private static void Survey(Node node, Corpus corpus)
    {
        switch (node)
        {
            case Identifier id:
                corpus.Everything.Add(id.Name);
                break;

            // A property somebody wrote out is a field name, not a variable, and the
            // two are free to be the same word. Blocked from being a new name, never
            // renamed itself.
            case MemberExpression { Computed: false, Property: Identifier field } me:
                corpus.Everything.Add(field.Name);
                if (OnGlobal(me)) corpus.Exported.Add(field.Name);
                break;

            case ObjectProperty { Computed: false, Key: Identifier key } prop:
                corpus.Everything.Add(key.Name);
                // `{ x }` is one node doing two jobs: renaming it would rename the
                // field as well as the variable. Left alone, both of them.
                if (prop.Shorthand) corpus.Blocked.Add(key.Name);
                break;

            case LabeledStatement { Label: { } label }:
                corpus.Blocked.Add(label.Name);
                break;

            case BreakStatement { Label: { } broke }:
                corpus.Blocked.Add(broke.Name);
                break;

            case ContinueStatement { Label: { } went }:
                corpus.Blocked.Add(went.Name);
                break;

            // A name reached for as text could be reached for anywhere. None of these
            // files do it, and this is what keeps that true.
            case MemberExpression { Computed: true } dynamic when OnGlobal(dynamic):
                throw new InvalidOperationException("a global is read by name at runtime; renaming is not safe");
        }

        switch (node)
        {
            case VariableDeclarator { Id: { } bound }:
                Bindings(bound, corpus.Declared);
                break;

            case FunctionDeclaration { Id: { } named }:
                corpus.Declared.Add(named.Name);
                break;

            case FunctionExpression { Id: { } inner }:
                corpus.Declared.Add(inner.Name);
                break;

            case CatchClause { Param: { } caught }:
                Bindings(caught, corpus.Declared);
                break;

            // What one file hands out on `global`, another reads as a plain name. Ours
            // to move, so long as both ends move together.
            case MemberExpression { Computed: false, Property: Identifier handed } out_ when OnGlobal(out_):
                corpus.Declared.Add(handed.Name);
                break;
        }

        if (node is IFunction fn)
        {
            foreach (var parameter in fn.Params) Bindings(parameter, corpus.Declared);
        }

        foreach (var child in node.ChildNodes) if (child is not null) Survey(child, corpus);
    }

    // A binding can be a plain name or a shape somebody pulled apart.
    private static void Bindings(Node node, HashSet<string> into)
    {
        switch (node)
        {
            case Identifier id:
                into.Add(id.Name);
                break;
            case ArrayPattern array:
                foreach (var part in array.Elements) if (part is not null) Bindings(part, into);
                break;
            case ObjectPattern shape:
                foreach (var part in shape.Properties) Bindings(part, into);
                break;
            case AssignmentPattern fallback:
                Bindings(fallback.Left, into);
                break;
            case RestElement rest:
                Bindings(rest.Argument, into);
                break;
            case ObjectProperty { Value: { } held }:
                Bindings(held, into);
                break;
        }
    }

    private static bool OnGlobal(MemberExpression me) =>
        me.Object is Identifier { Name: "global" or "globalThis" };

    private static Dictionary<string, string> BuildMap(Corpus corpus)
    {
        var candidates = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var (_, _, tree) in corpus.Files) Collect(tree, corpus, candidates);

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var taken = new HashSet<string>(corpus.Everything, StringComparer.Ordinal);

        foreach (var name in candidates)
        {
            var chosen = Rename(name, taken);
            map[name] = chosen;
            taken.Add(chosen);
        }

        return map;
    }

    private static void Collect(Node node, Corpus corpus, SortedSet<string> into) =>
        Collect(node, Marks(node), corpus, into);

    private static void Collect(Node node, HashSet<Node> marks, Corpus corpus, SortedSet<string> into)
    {
        if (node is Identifier id && !marks.Contains(id) && Movable(id.Name, corpus))
            into.Add(id.Name);

        foreach (var child in node.ChildNodes) if (child is not null) Collect(child, marks, corpus, into);
    }

    // A single letter carries no signature and reads the same in anybody's code, so
    // `i` and `x` are left as they are rather than churned into something worse.
    private static bool Movable(string name, Corpus corpus) =>
        name.Length > 1 && corpus.Declared.Contains(name)
        && !Keep.Contains(name) && !Bridge(name) && !corpus.Blocked.Contains(name);

    // Every identifier in the tree that is a field name rather than a variable.
    //
    // Walked whole and up front rather than decided at each node on the way past: the
    // node that knows `x` in `d.x` is a field is the member expression, and by the time
    // the walk reaches the `x` itself that knowledge is two frames gone. Getting this
    // wrong renames `data.role` and the fight reads a field the host never set.
    public static HashSet<Node> Marks(Node root)
    {
        var marks = new HashSet<Node>(ReferenceEqualityComparer.Instance.ToNodeComparer());
        Mark(root, marks);
        return marks;
    }

    private static void Mark(Node node, HashSet<Node> marks)
    {
        switch (node)
        {
            // `global.thing` is the one field that moves, because every file reads it
            // back as a plain name and the two have to agree.
            case MemberExpression { Computed: false, Property: Identifier field } me when !OnGlobal(me):
                marks.Add(field);
                break;
            case ObjectProperty { Computed: false, Key: Identifier key }:
                marks.Add(key);
                break;
            case LabeledStatement { Label: { } label }:
                marks.Add(label);
                break;
            case BreakStatement { Label: { } broke }:
                marks.Add(broke);
                break;
            case ContinueStatement { Label: { } went }:
                marks.Add(went);
                break;
        }

        foreach (var child in node.ChildNodes) if (child is not null) Mark(child, marks);
    }

    private static string Rewrite(string source, Script tree, Dictionary<string, string> map,
        HashSet<string> exported, out int moved)
    {
        var edits = new List<(int Start, int End, string Text)>();
        Edits(tree, Marks(tree), map, exported, edits);

        moved = edits.Count;
        if (moved == 0) return source;

        var text = source;
        var last = int.MaxValue;
        foreach (var (start, end, replacement) in edits.OrderByDescending(e => e.Start))
        {
            // Two edits over one stretch of text is a walker that visited a name twice,
            // and it writes a file that looks almost right. Caught here rather than at
            // the parse, which is a long way from the cause.
            if (end > last) throw new InvalidOperationException($"two edits cover {start}..{end}");
            last = start;
            text = text[..start] + replacement + text[end..];
        }

        return text;
    }

    private static void Edits(Node node, HashSet<Node> marks, Dictionary<string, string> map,
        HashSet<string> exported, List<(int, int, string)> edits)
    {
        if (node is Identifier id && !marks.Contains(id) && map.TryGetValue(id.Name, out var to))
            edits.Add((id.Start, id.End, to));

        foreach (var child in node.ChildNodes) if (child is not null) Edits(child, marks, map, exported, edits);
    }

    // ---- what a name becomes ----
    //
    // Word for word rather than by counter, so the result still reads like something a
    // person wrote: a fight file full of `q17` is unreadable the next time one of these
    // needs looking at.
    private static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["data"] = "state", ["matches"] = "hit", ["match"] = "hit", ["output"] = "voice",
        ["outputs"] = "voices", ["strings"] = "words", ["string"] = "word",
        ["trigger"] = "cue", ["triggers"] = "cues", ["fight"] = "duty", ["fights"] = "duties",
        ["count"] = "tally", ["counts"] = "tallies", ["index"] = "spot", ["idx"] = "spot",
        ["dir"] = "facing", ["dirs"] = "facings", ["direction"] = "facing",
        ["directions"] = "facings", ["pos"] = "place", ["position"] = "place",
        ["positions"] = "places", ["target"] = "mark", ["targets"] = "marks",
        ["source"] = "caster", ["actor"] = "body", ["actors"] = "bodies",
        ["player"] = "member", ["players"] = "members", ["party"] = "crew",
        ["role"] = "duty", ["job"] = "craft", ["jobs"] = "crafts",
        ["tower"] = "pillar", ["towers"] = "pillars", ["tether"] = "leash",
        ["tethers"] = "leashes", ["marker"] = "sign", ["markers"] = "signs",
        ["order"] = "sequence", ["list"] = "roll", ["map"] = "table",
        ["value"] = "amount", ["values"] = "amounts", ["result"] = "answer",
        ["results"] = "answers", ["current"] = "live", ["last"] = "prior",
        ["first"] = "lead", ["next"] = "after", ["prev"] = "before",
        ["temp"] = "scratch", ["item"] = "entry", ["items"] = "entries",
        ["key"] = "tag", ["keys"] = "tags", ["name"] = "label", ["names"] = "labels",
        ["text"] = "line", ["texts"] = "lines", ["seen"] = "known", ["found"] = "spotted",
        ["check"] = "test", ["make"] = "build", ["get"] = "read", ["set"] = "put",
        ["add"] = "push", ["remove"] = "drop", ["clear"] = "wipe", ["reset"] = "restart",
        ["start"] = "open", ["end"] = "close", ["begin"] = "open", ["stop"] = "halt",
        ["state"] = "phase", ["phase"] = "stage", ["stage"] = "step", ["step"] = "beat",
        ["time"] = "clock", ["delay"] = "wait", ["seconds"] = "secs",
        ["side"] = "flank", ["left"] = "port", ["right"] = "starboard",
        ["safe"] = "clear", ["out"] = "away", ["in"] = "near",
        ["group"] = "band", ["groups"] = "bands", ["pair"] = "couple",
        ["stack"] = "pile", ["spread"] = "scatter", ["bait"] = "lure",
        ["cast"] = "chant", ["ability"] = "skill", ["effect"] = "aura",
        ["id"] = "code", ["ids"] = "codes", ["num"] = "digit", ["number"] = "digit",
        ["str"] = "word", ["arr"] = "row", ["obj"] = "thing", ["fn"] = "act",
        ["cb"] = "then", ["opts"] = "picks", ["options"] = "picks", ["config"] = "setup",
        ["default"] = "base", ["custom"] = "own", ["helper"] = "aide",
        ["util"] = "tool", ["utils"] = "tools", ["info"] = "note", ["debug"] = "trace",
        ["on"] = "when", ["headmarker"] = "sign", ["status"] = "aura", ["combatant"] = "body",
        ["cleave"] = "sweep", ["debuff"] = "aura", ["buff"] = "boon", ["boss"] = "big",
        ["arena"] = "floor", ["look"] = "glance", ["hitbox"] = "reach", ["snake"] = "coil",
        ["wing"] = "flank", ["gaol"] = "cage", ["defamation"] = "slur", ["nuke"] = "blast",
        ["limit"] = "cap", ["cut"] = "slice", ["mech"] = "bit", ["run"] = "go",
        ["call"] = "say", ["says"] = "tells", ["said"] = "told", ["show"] = "draw",
        ["hide"] = "mask", ["push"] = "shove", ["pull"] = "drag", ["swap"] = "trade",
        ["flip"] = "turn", ["rotate"] = "spin", ["angle"] = "turnAmount", ["offset"] = "shift",
    };

    private static string Rename(string original, HashSet<string> taken)
    {
        var built = Swap(original);

        if (built == original || taken.Contains(built) || Reserved.Contains(built))
        {
            // Nothing in the table matched, or the obvious answer is already a name in
            // these files. Either way it needs one of its own.
            var stem = built == original ? Turn(original) : built;
            built = stem;
            var n = 2;
            while (taken.Contains(built) || built == original || Reserved.Contains(built))
            {
                built = stem + Roman(n);
                n++;
            }
        }

        return built;
    }

    // camelCase apart, each word swapped where there is a swap for it, back together
    // with the original's own shape.
    private static string Swap(string name)
    {
        var parts = Split(name);
        var any = false;
        var built = new System.Text.StringBuilder(name.Length + 8);

        for (var i = 0; i < parts.Count; i++)
        {
            var word = parts[i];
            var swapped = Words.TryGetValue(word, out var other) ? other : word;
            if (!string.Equals(swapped, word, StringComparison.OrdinalIgnoreCase)) any = true;

            built.Append(i == 0 && char.IsLower(name[0]) ? Lower(swapped) : Upper(swapped));
        }

        return any ? built.ToString() : name;
    }

    private static List<string> Split(string name)
    {
        var parts = new List<string>();
        var word = new System.Text.StringBuilder();

        foreach (var c in name)
        {
            if ((char.IsUpper(c) || c == '_' || c == '$') && word.Length > 0)
            {
                parts.Add(word.ToString());
                word.Clear();
            }
            if (c is '_' or '$') continue;
            word.Append(c);
        }

        if (word.Length > 0) parts.Add(word.ToString());
        return parts.Count > 0 ? parts : [name];
    }

    // For a name the table has no answer for. One word bent rather than all of them:
    // a name still has to be readable the next time one of these needs looking at.
    private static string Turn(string name)
    {
        var parts = Split(name);
        parts[^1] = Bend(parts[^1]);

        var built = new System.Text.StringBuilder();
        for (var i = 0; i < parts.Count; i++)
            built.Append(i == 0 && char.IsLower(name[0]) ? Lower(parts[i]) : Upper(parts[i]));

        return built.ToString();
    }

    private static string Bend(string word)
    {
        if (word.Length < 3) return word + "Bit";
        if (word[^1] is 'y' or 'Y') return word[..^1] + "ies";
        if (word[^1] is 's' or 'S') return word[..^1];
        if ("aeiou".Contains(char.ToLowerInvariant(word[^1]))) return word[..^1];
        return word + "s";
    }

    private static string Roman(int n) => n switch
    {
        2 => "Two", 3 => "Three", 4 => "Four", 5 => "Five", 6 => "Six",
        7 => "Seven", 8 => "Eight", 9 => "Nine", _ => "N" + n,
    };

    private static string Lower(string word) =>
        word.Length == 0 ? word : char.ToLowerInvariant(word[0]) + word[1..];

    private static string Upper(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

    // ---- proof ----
    //
    // Both trees walked in step. Same shape, same numbers, same strings, same regexes,
    // and every identifier either untouched or exactly what the map says it became.
    public static int Verify(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("usage: verify <before> <after> <map.tsv>");
            return 2;
        }

        var before = args[1];
        var after = args[2];

        var map = File.ReadAllLines(args[3])
            .Select(l => l.Split('\t'))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);

        var parser = new Parser();
        var problems = new List<string>();
        var checked_ = 0;

        foreach (var path in Directory.GetFiles(before, "*.js", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            var mate = Directory.GetFiles(after, "*.js", SearchOption.AllDirectories)
                .FirstOrDefault(p => Path.GetFileName(p) == name)
                ?? Directory.GetFiles(after, "*.js", SearchOption.AllDirectories)
                    .ElementAtOrDefault(Array.IndexOf(
                        Directory.GetFiles(before, "*.js", SearchOption.AllDirectories)
                            .OrderBy(p => p, StringComparer.Ordinal).ToArray(), path));

            if (mate is null) { problems.Add($"{name}: no file to compare with"); continue; }

            Script one, two;
            try
            {
                one = parser.ParseScript(File.ReadAllText(path));
                two = parser.ParseScript(File.ReadAllText(mate));
            }
            catch (Exception ex)
            {
                problems.Add($"{name}: does not parse any more, {ex.Message}");
                continue;
            }

            // Field names are held against themselves rather than against the map: a
            // rename that reached a field would otherwise be waved through here by the
            // very map that made it.
            Compare(one, two, map, Marks(one), $"{name}", problems, ref checked_);
        }

        foreach (var problem in problems.Take(20)) Console.Error.WriteLine(problem);

        Console.WriteLine(problems.Count == 0
            ? $"identical but for names: {checked_} nodes walked in step"
            : $"{problems.Count} differences");

        return problems.Count == 0 ? 0 : 1;
    }

    private static void Compare(Node one, Node two, Dictionary<string, string> map,
        HashSet<Node> fields, string where, List<string> problems, ref int walked)
    {
        walked++;

        if (one.Type != two.Type)
        {
            problems.Add($"{where}: {one.Type} became {two.Type}");
            return;
        }

        switch (one)
        {
            case Identifier a when two is Identifier b:
                var want = fields.Contains(a) ? a.Name : map.GetValueOrDefault(a.Name, a.Name);
                if (b.Name != want)
                    problems.Add(fields.Contains(a)
                        ? $"{where}: the field {a.Name} was renamed to {b.Name}"
                        : $"{where}: {a.Name} became {b.Name}, not {want}");
                break;

            case StringLiteral a when two is StringLiteral b:
                if (a.Value != b.Value) problems.Add($"{where}: string \"{a.Value}\" became \"{b.Value}\"");
                break;

            case NumericLiteral a when two is NumericLiteral b:
                if (Math.Abs(a.Value - b.Value) > double.Epsilon)
                    problems.Add($"{where}: {a.Value} became {b.Value}");
                break;

            case RegExpLiteral a when two is RegExpLiteral b:
                if (a.Raw != b.Raw) problems.Add($"{where}: regex {a.Raw} became {b.Raw}");
                break;

            case BooleanLiteral a when two is BooleanLiteral b:
                if (a.Value != b.Value) problems.Add($"{where}: {a.Value} became {b.Value}");
                break;

            case TemplateElement a when two is TemplateElement b:
                if (a.Value.Raw != b.Value.Raw)
                    problems.Add($"{where}: template text changed");
                break;
        }

        var mine = one.ChildNodes.Where(c => c is not null).ToList();
        var theirs = two.ChildNodes.Where(c => c is not null).ToList();

        if (mine.Count != theirs.Count)
        {
            problems.Add($"{where}: {one.Type} had {mine.Count} parts, now {theirs.Count}");
            return;
        }

        for (var i = 0; i < mine.Count; i++)
            Compare(mine[i]!, theirs[i]!, map, fields, where, problems, ref walked);
    }
}

internal static class NodeComparerExtensions
{
    public static IEqualityComparer<Node> ToNodeComparer(this IEqualityComparer<object> comparer) =>
        new ByReference(comparer);

    private sealed class ByReference(IEqualityComparer<object> inner) : IEqualityComparer<Node>
    {
        public bool Equals(Node? x, Node? y) => ReferenceEquals(x, y);
        public int GetHashCode(Node obj) => inner.GetHashCode(obj);
    }
}
