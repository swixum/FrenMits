using System.Text;
using System.Text.RegularExpressions;

namespace FrenAlerts.Engine.Scripts;

// Which of a trigger's output strings the trigger can actually say.
//
// Their fights hand one output table to several triggers: dancingmad.js gives the same
// twenty-eight Mystery Magic lines to every Mystery Magic trigger in the fight, and
// each of them speaks four or five of them. Listing the table is therefore listing
// mostly words the mechanic never says. The fight page read as a wall of every word in
// the file: "Bait Puddle", "Get Middle" and "Look At Statue" all sat under a mechanic
// that says none of the three.
//
// Which keys a trigger reaches is written in exactly one place, its own callbacks, and
// Jint hands those back as "[native code]". So it is read off the file text instead:
// find the trigger's own object by its id, then collect every `output.key` and
// `voice.key` in it and in whatever helpers it hands the output to. Only the text is
// needed, so none of it is kept.
//
// Nothing is narrowed on a guess. A trigger this cannot place, and a trigger that picks
// a key by name at run time, both keep their whole table: too many lines is untidy, a
// line somebody cannot find and cannot reword is a bug, and only one of those is worth
// risking. That rule is most of the point, because half of their direction calls read
// `voice[facing]` and narrowing those would hide every compass point in the fight.
public static class ScriptOutputUse
{
    // Far larger than any of theirs, which top out near 200KB. A file past this is not a
    // fight file, and the backwards walk for an enclosing block is quadratic in the
    // worst case.
    private const int MaxFileChars = 4_000_000;

    // How far a trigger's output is followed through their helpers. Their deepest is one
    // hop; this is a guard on a cycle, not a limit anything real runs into.
    private const int MaxHops = 8;

    // How they reach a line: `voice.spread()` in this port, `output.spread!()` in theirs,
    // and `output['spread']` in the handful that name a key outright.
    private static readonly Regex Reaches = new(
        @"(?:output|voice)\s*(?:\.\s*([A-Za-z_$][A-Za-z0-9_$]*)|\[\s*['""]([^'""\r\n]+)['""]\s*\])",
        RegexOptions.Compiled);

    // A key worked out as it fires: `voice[facing]`, `voice[dir2s]`. Nothing static can
    // say which line that comes out as, so a trigger holding one is not narrowed at all.
    private static readonly Regex Dynamic = new(
        @"(?:output|voice)\s*\[\s*(?!['""])",
        RegexOptions.Compiled);

    // Anything called by name, so the output can be followed into their helpers.
    private static readonly Regex Calls = new(
        @"\b([A-Za-z_$][A-Za-z0-9_$]*)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex Declares = new(
        @"\bfunction\s+([A-Za-z_$][A-Za-z0-9_$]*)\s*\(",
        RegexOptions.Compiled);

    // Every trigger this file names, with the output keys its own body reaches.
    //
    // Only the ones that reach something come back. That is what keeps a netRegex's own
    // `id: 'BA94'` out of the answer without having to know what a netRegex is: a match
    // block has no output in it, so it yields nothing and is dropped.
    public static IEnumerable<(string Trigger, IReadOnlyList<string> Keys)> Read(string source)
    {
        if (source.Length is 0 or > MaxFileChars) yield break;

        var code = new bool[source.Length];
        var named = new List<(int At, string Value)>();
        Scan(source, code, named);

        var helpers = Helpers(source, code);

        foreach (var (at, value) in named)
        {
            if (Block(source, code, at) is not { } block) continue;

            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var walked = new HashSet<string>(StringComparer.Ordinal);
            if (Gather(block, helpers, keys, seen, walked, MaxHops) && keys.Count > 0)
                yield return (value, keys);
        }
    }

    // The keys one body reaches, and the bodies it hands the output on to. False where a
    // key is worked out rather than named, which is the caller's signal to narrow
    // nothing.
    private static bool Gather(string body, IReadOnlyDictionary<string, string> helpers,
        List<string> keys, HashSet<string> seen, HashSet<string> walked, int hops)
    {
        if (Dynamic.IsMatch(body)) return false;

        foreach (Match m in Reaches.Matches(body))
        {
            var key = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (key.Length > 0 && seen.Add(key)) keys.Add(key);
        }

        if (hops <= 0) return true;

        // Their triggers routinely hand the whole thing to a function beside them:
        // M9S Half Moon's alert is `m9sHalfMoonAlerts(pull, hit, voice)` and every one of
        // its words is in there. Read as its own block it reaches nothing at all.
        foreach (Match m in Calls.Matches(body))
        {
            var name = m.Groups[1].Value;
            if (!walked.Add(name)) continue;
            if (!helpers.TryGetValue(name, out var inner)) continue;
            if (!Gather(inner, helpers, keys, seen, walked, hops - 1)) return false;
        }

        return true;
    }

    // Every named function in the file, as its body text.
    private static IReadOnlyDictionary<string, string> Helpers(string s, bool[] code)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Declares.Matches(s))
        {
            if (!code[m.Index]) continue;
            if (found.ContainsKey(m.Groups[1].Value)) continue;
            if (BodyAfter(s, code, m.Index + m.Length) is { } body) found[m.Groups[1].Value] = body;
        }
        return found;
    }

    // The next braced block at or after here, which for a function declaration is its
    // body.
    private static string? BodyAfter(string s, bool[] code, int from)
    {
        for (var i = from; i < s.Length; i++)
        {
            if (!code[i] || s[i] != '{') continue;
            return Forward(s, code, i);
        }
        return null;
    }

    // One pass: mark which characters are code rather than string or comment, and note
    // every string literal that is the value of an `id:` property.
    //
    // The mask is what makes the brace walks below safe. Their files are full of braces
    // inside strings, from `'4[0-9A-Fa-f]{7}'` in a match to `'${mech} + ${ice}'` in a
    // line, and a walk that counted those would pair the wrong ones.
    private static void Scan(string s, bool[] code, List<(int At, string Value)> named)
    {
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];

            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n') i++;
                continue;
            }

            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) i++;
                i = Math.Min(s.Length, i + 2);
                continue;
            }

            if (c is '\'' or '"' or '`')
            {
                var start = i;
                var words = new StringBuilder();
                i++;
                while (i < s.Length)
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        // Their ids carry apostrophes: "DMU P2 Future's End/Past's End
                        // (Early)" is written escaped, and a literal compared raw against
                        // the compiled id would miss that one trigger.
                        words.Append(Unescaped(s[i + 1]));
                        i += 2;
                        continue;
                    }
                    if (s[i] == c) { i++; break; }
                    words.Append(s[i]);
                    i++;
                }
                if (IsIdValue(s, code, start)) named.Add((start, words.ToString()));
                continue;
            }

            code[i] = true;
            i++;
        }
    }

    private static char Unescaped(char c) => c switch
    {
        'n' => '\n',
        'r' => '\r',
        't' => '\t',
        _ => c,
    };

    // Whether the literal starting here is what an `id:` property was set to. Anything
    // else in the file is a line, a job name or a match, and none of those name a
    // trigger.
    private static bool IsIdValue(string s, bool[] code, int quote)
    {
        var i = Back(s, code, quote - 1);
        if (i < 0 || s[i] != ':') return false;

        i = Back(s, code, i - 1);
        if (i < 1 || s[i] != 'd' || s[i - 1] != 'i') return false;

        // A whole word, so `uid:` and `mapId:` are not read as `id:`.
        var before = i - 2;
        return before < 0 || !(char.IsLetterOrDigit(s[before]) || s[before] is '_' or '$');
    }

    // The previous character that is code and not whitespace.
    private static int Back(string s, bool[] code, int from)
    {
        while (from >= 0 && (!code[from] || char.IsWhiteSpace(s[from]))) from--;
        return from;
    }

    // The object literal the character at `at` sits inside, as text.
    private static string? Block(string s, bool[] code, int at)
    {
        var depth = 0;
        for (var i = at; i >= 0; i--)
        {
            if (!code[i]) continue;
            if (s[i] == '}') depth++;
            else if (s[i] == '{')
            {
                if (depth == 0) return Forward(s, code, i);
                depth--;
            }
        }
        return null;
    }

    // From an opening brace to the one that closes it.
    private static string? Forward(string s, bool[] code, int open)
    {
        var depth = 0;
        for (var i = open; i < s.Length; i++)
        {
            if (!code[i]) continue;
            if (s[i] == '{') depth++;
            else if (s[i] == '}' && --depth == 0) return s[open..(i + 1)];
        }
        return null;
    }
}
