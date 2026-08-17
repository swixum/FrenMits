using System.Globalization;
using System.Text.RegularExpressions;

namespace FrenAlerts.Engine.Scripts;

// Their timeline files, read the way they read them.
//
// The `.txt` files that ship beside the fight scripts are the other half of the
// port: the scripts say what a mechanic means, the timeline says when it is due
// and how the clock finds its place again. Both halves come across as written,
// so the field meanings here are their importer's, down to the defaults: a
// window nobody wrote is 2.5 either side, a single-number window is that number
// split in half, and a name starting with `--` is their own bookkeeping and
// never a mechanic.
//
// Separate from the `Timeline*` pair next door, which reads the baked pack. Two
// formats, two readers, no shared field meanings to get crossed.
public sealed class ScriptTimelineEntry
{
    public required float Time { get; init; }

    public required string Name { get; init; }

    // The ability that puts the clock here, and the other ids the same mechanic
    // ships under. A cleave written for four directions is one entry with four.
    public required uint ActionId { get; init; }

    public IReadOnlyList<uint> AltIds { get; init; } = [];

    // Filled by the game side out of the action sheet, which the engine cannot
    // read. Zero everywhere offline, and nothing depends on it.
    public uint IconId { get; set; }

    public float WindowBefore { get; init; } = DefaultWindow;

    public float WindowAfter { get; init; } = DefaultWindow;

    // Where the clock goes when this entry syncs, or below zero for an entry
    // that only says where it is. Zero is a jump their runtime reads as "stop".
    public float Jump { get; init; } = -1f;

    public IReadOnlyList<ScriptTimelineCallout> Callouts { get; init; } = [];

    public const float DefaultWindow = 2.5f;

    // A window this wide is a phase gate rather than a nudge, and their picker
    // prefers the latest of them over the nearest ordinary entry.
    public const float WideWindow = 50f;

    public bool HasJump => Jump >= 0f;

    public bool IsWide => WindowBefore >= WideWindow;

    public bool Matches(uint id) => id != 0 && (id == ActionId || AltIds.Contains(id));

    public bool InWindow(float now) => now >= Time - WindowBefore && now <= Time + WindowAfter;

    public override string ToString() => $"{Time,8:F1} {Name} ({ActionId:X})";
}

// A line spoken ahead of an entry, from the `callout` directive.
public sealed record ScriptTimelineCallout(float Before, string Label);

// One fight's timeline, sorted, keyed by the file it came from.
public sealed class ScriptTimeline
{
    public required string Key { get; init; }

    public required IReadOnlyList<ScriptTimelineEntry> Entries { get; init; }

    public override string ToString() => $"{Key} ({Entries.Count})";
}

public static class ScriptTimelineReader
{
    // Their importer reads ability lines and nothing else: an entry the clock can
    // sync to needs an action id, and their other sync kinds (yells, head markers,
    // combat state, memory reads) carry none. Those lines are left where they are.
    private static readonly Regex TimedLine = new(
        """^\s*([0-9]+(?:\.[0-9]+)?)\s+"([^"]+)"\s+(#?)(?:Ability|StartsUsing)\s*\{[^}]*\bid:\s*(\[[^\]]*\]|"[0-9A-Fa-f]+")""",
        RegexOptions.Compiled);

    private static readonly Regex HexIds = new("[0-9A-Fa-f]+", RegexOptions.Compiled);

    private static readonly Regex WindowDirective = new(
        @"\bwindow\s+([0-9]+(?:\.[0-9]+)?)(?:\s*,\s*([0-9]+(?:\.[0-9]+)?))?", RegexOptions.Compiled);

    // Numbers only, theirs included: a `jump "label"` names a place in the file
    // rather than a second, so their importer reads no jump at all off it and the
    // entry stays an ordinary sync that the clock resyncs against instead.
    private static readonly Regex JumpDirective = new(
        @"\b(?:jump|forcejump)\s+([0-9]+(?:\.[0-9]+)?)", RegexOptions.Compiled);

    // The same directive written as a name, and the line that gives the name a
    // second. Read only when asked for, so the default stays theirs exactly.
    private static readonly Regex JumpToLabel = new(
        @"\b(?:jump|forcejump)\s+""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex LabelLine = new(
        @"^\s*([0-9]+(?:\.[0-9]+)?)\s+label\s+""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex CalloutDirective = new(
        """\bcallout(?:\s+([0-9]+(?:\.[0-9]+)?))?(?:\s+"([^"]*)")?""", RegexOptions.Compiled);

    private const float DefaultCalloutBefore = 3f;

    // Reads one file's text.
    //
    // includeHidden carries their `#` convention: a sync commented out that way is
    // an entry they kept on the display and took off the clock, so the default
    // leaves it out exactly as the running timeline does.
    //
    // followLabels reads one thing more of the file than they do, and swix ruled it
    // on 2026-08-16. A looping phase writes `forcejump "label"`, their importer takes
    // numbers only, and the loop therefore never closes: their clock runs past the
    // end of the loop and finds its way back by resyncing on the next ability. Only
    // the Unending Coil writes them, 23 of them, so this changes that fight and no
    // other. Pass false to read a file exactly as they do.
    public static IReadOnlyList<ScriptTimelineEntry> ReadEntries(
        string text, bool includeHidden = false, bool followLabels = true)
    {
        var entries = new List<ScriptTimelineEntry>();
        var seen = new HashSet<string>();
        var lines = text.Replace("\r", "").Split('\n');
        var labels = followLabels ? ReadLabels(lines) : null;

        foreach (var line in lines)
        {
            var m = TimedLine.Match(line);
            if (!m.Success) continue;
            if (!float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
                continue;

            var name = m.Groups[2].Value.Trim();
            var hidden = m.Groups[3].Value == "#";
            if (name.Length == 0 || name.StartsWith("--") || (hidden && !includeHidden)) continue;

            var ids = ReadIds(m.Groups[4].Value);
            if (ids.Count == 0) continue;

            // Their key: the same ability at the same second twice over is the file
            // saying it two ways, not two mechanics.
            if (!seen.Add($"{ids[0]}@{time:0.0}")) continue;

            var (before, after) = ReadWindow(line);

            entries.Add(new ScriptTimelineEntry
            {
                Time = time,
                Name = name,
                ActionId = ids[0],
                AltIds = ids.Count > 1 ? ids.GetRange(1, ids.Count - 1) : [],
                WindowBefore = before,
                WindowAfter = after,
                Jump = ReadJump(line, labels),
                Callouts = ReadCallouts(line),
            });
        }

        entries.Sort((a, b) => a.Time.CompareTo(b.Time));
        return entries;
    }

    public static ScriptTimeline Read(
        string key, string text, bool includeHidden = false, bool followLabels = true) =>
        new() { Key = key, Entries = ReadEntries(text, includeHidden, followLabels) };

    private static Dictionary<string, float> ReadLabels(IEnumerable<string> lines)
    {
        var labels = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var m = LabelLine.Match(line);
            if (m.Success
                && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var at))
                labels[m.Groups[2].Value] = at;
        }
        return labels;
    }

    private static List<uint> ReadIds(string raw)
    {
        var ids = new List<uint>();
        foreach (Match hex in HexIds.Matches(raw))
        {
            if (!uint.TryParse(hex.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id))
                continue;
            if (id != 0 && !ids.Contains(id)) ids.Add(id);
        }
        return ids;
    }

    private static (float Before, float After) ReadWindow(string line)
    {
        var m = WindowDirective.Match(line);
        if (!m.Success) return (ScriptTimelineEntry.DefaultWindow, ScriptTimelineEntry.DefaultWindow);

        if (m.Groups[2].Success
            && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var before)
            && float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var after))
            return (before, after);

        // One number is the whole window, so each side gets half of it.
        if (float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var whole))
            return (whole / 2f, whole / 2f);

        return (ScriptTimelineEntry.DefaultWindow, ScriptTimelineEntry.DefaultWindow);
    }

    private static float ReadJump(string line, IReadOnlyDictionary<string, float>? labels)
    {
        var m = JumpDirective.Match(line);
        if (m.Success
            && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var to))
            return to;

        if (labels is null) return -1f;

        var named = JumpToLabel.Match(line);
        return named.Success && labels.TryGetValue(named.Groups[1].Value, out var at) ? at : -1f;
    }

    private static IReadOnlyList<ScriptTimelineCallout> ReadCallouts(string line)
    {
        var m = CalloutDirective.Match(line);
        if (!m.Success) return [];

        var before = DefaultCalloutBefore;
        if (m.Groups[1].Success)
            float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out before);

        return [new ScriptTimelineCallout(before, m.Groups[2].Success ? m.Groups[2].Value : "")];
    }
}

// Every timeline in a folder, keyed by file stem so a fight finds its own.
public static class ScriptTimelines
{
    public static IReadOnlyDictionary<string, ScriptTimeline> Load(
        string folder, bool includeHidden = false, bool followLabels = true)
    {
        var byKey = new Dictionary<string, ScriptTimeline>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(folder)) return byKey;

        foreach (var path in Directory.GetFiles(folder, "*.txt"))
        {
            var key = KeyOf(Path.GetFileNameWithoutExtension(path));
            byKey[key] = ScriptTimelineReader.Read(key, File.ReadAllText(path), includeHidden, followLabels);
        }
        return byKey;
    }

    // `lindwurm_a.js` and `lindwurm_a.txt` are the same fight; the punctuation
    // between the two halves of a name is the only thing that ever disagrees.
    public static string KeyOf(string name)
    {
        Span<char> kept = stackalloc char[name.Length];
        var n = 0;
        foreach (var c in name)
            if (char.IsLetterOrDigit(c)) kept[n++] = char.ToLowerInvariant(c);
        return new string(kept[..n]);
    }
}
