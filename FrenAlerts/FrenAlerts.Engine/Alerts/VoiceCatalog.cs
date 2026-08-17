namespace FrenAlerts.Engine.Alerts;

// The voices sitting in the voice folder, read from their file names.
//
// Every voice file is named <language><gender>_<person>, and the model library
// reports the same two facts back for each one it loads. Reading them from the name
// means the picker can be built without loading 84MB of model first, and a voice
// dropped in later shows up without anything being edited here.
public static class VoiceCatalog
{
    public readonly record struct Choice(string Name, string Language, bool? Female, string Person)
    {
        public string Label => Female switch
        {
            true => $"{Person} (female)",
            false => $"{Person} (male)",
            _ => Person,
        };
    }

    // The first letter of the file name. Checked against all 156 voices the library
    // ships, which report these same languages back through their own metadata.
    private static readonly Dictionary<char, string> Languages = new()
    {
        ['a'] = "American English",
        ['b'] = "British English",
        ['e'] = "Spanish",
        ['f'] = "French",
        ['h'] = "Hindi",
        ['i'] = "Italian",
        ['j'] = "Japanese",
        ['p'] = "Brazilian Portuguese",
        ['z'] = "Mandarin Chinese",
    };

    // Calls are in English, so those come first, and anything unlabelled sinks to
    // the bottom instead of sorting under whatever letter it starts with.
    private static readonly string[] Order =
    [
        "American English", "British English", "Spanish", "French", "Hindi",
        "Italian", "Japanese", "Brazilian Portuguese", "Mandarin Chinese", Unknown,
    ];

    public const string Unknown = "Other";

    public const string Default = "af_heart";

    // How far down the picker a language sits, and past the end for one Order does not
    // name.
    //
    // These are two lists that have to agree, and the sort read the second with
    // Array.IndexOf, which answers -1 for a miss. So a language added to Languages and
    // forgotten here did not sink, it sorted ahead of American English: a voice nobody
    // could understand at the top of the list, on a plugin whose calls are in English.
    // A miss goes to the bottom now, which is what the comment above already promises
    // for anything unlabelled. The test holds the two lists level so it stays theory.
    public static int Rank(string language)
    {
        var at = Array.IndexOf(Order, language);
        return at < 0 ? Order.Length : at;
    }

    public static IReadOnlyList<Choice> Read(IEnumerable<string> fileNames)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<Choice>();

        foreach (var file in fileNames)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.Length == 0 || !seen.Add(name)) continue;
            found.Add(Parse(name));
        }

        return found
            .OrderBy(c => Rank(c.Language))
            .ThenBy(c => c.Female is null ? 2 : c.Female is true ? 0 : 1)
            .ThenBy(c => c.Person, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Languages in the order they should be offered, for a picker that splits the
    // list in two rather than showing 156 rows at once.
    public static IReadOnlyList<string> LanguagesIn(IEnumerable<Choice> choices) =>
        choices.Select(c => c.Language).Distinct()
            .OrderBy(Rank).ToList();

    // A name that does not follow the convention keeps its own spelling and claims
    // no language and no gender, rather than being filed under a guess.
    private static Choice Parse(string name)
    {
        if (name.Length < 4 || name[2] != '_' || !Languages.TryGetValue(char.ToLowerInvariant(name[0]), out var lang))
            return new Choice(name, Unknown, null, name);

        var gender = char.ToLowerInvariant(name[1]) switch
        {
            'f' => (bool?)true,
            'm' => false,
            _ => null,
        };

        return gender is null
            ? new Choice(name, Unknown, null, name)
            : new Choice(name, lang, gender, Person(name[3..]));
    }

    private static string Person(string tail)
    {
        var words = tail.Replace('_', ' ').Trim();
        return words.Length == 0 ? tail : char.ToUpperInvariant(words[0]) + words[1..];
    }
}
