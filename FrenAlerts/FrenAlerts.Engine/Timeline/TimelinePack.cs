using System.Globalization;

namespace FrenAlerts.Engine;

// Reads timelines.fatime, the file FrenAlerts/tools/bake_timelines.py writes.
//
// Tab separated and hand readable on purpose, the same as the call pack: a
// timeline that cannot be diffed is a timeline nobody notices drifting.
public static class TimelinePack
{
    public const string Header = "fatime 1";

    public static IReadOnlyDictionary<ushort, Timeline> ReadAll(IEnumerable<string> lines)
    {
        var entries = new Dictionary<ushort, List<TimelineEntry>>();
        var syncs = new Dictionary<ushort, List<TimelineSync>>();
        var first = true;

        foreach (var line in lines)
        {
            if (first)
            {
                first = false;
                if (line.StartsWith("fatime", StringComparison.Ordinal))
                {
                    if (line != Header)
                        throw new InvalidDataException($"timeline pack is {line}, this reads {Header}");
                    continue;
                }
            }
            if (line.Length == 0) continue;

            var f = line.Split('\t');
            if (f.Length < 4) continue;
            if (!ushort.TryParse(f[1], out var territory)) continue;
            if (!float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
                continue;

            switch (f[0])
            {
                case "e":
                    var mechanic = Unescape(f[3]);
                    if (mechanic.Length == 0) continue;
                    Bucket(entries, territory).Add(new TimelineEntry(time, mechanic));
                    break;

                case "s":
                    if (f.Length < 5) continue;
                    if (!uint.TryParse(f[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                            out var ability)) continue;
                    Bucket(syncs, territory).Add(new TimelineSync(time, ability, f[4] == "1"));
                    break;
            }
        }

        var built = new Dictionary<ushort, Timeline>();
        foreach (var territory in entries.Keys.Union(syncs.Keys))
        {
            var mine = entries.GetValueOrDefault(territory) ?? [];
            var anchors = syncs.GetValueOrDefault(territory) ?? [];

            // Sorted here rather than trusted: the runtime walks in list order and
            // a file edited by hand is the whole reason it is readable.
            mine.Sort(static (a, b) => a.Time.CompareTo(b.Time));
            anchors.Sort(static (a, b) => a.Time.CompareTo(b.Time));

            built[territory] = new Timeline
            {
                Territory = territory,
                Entries = mine,
                Syncs = anchors,
            };
        }
        return built;
    }

    private static List<T> Bucket<T>(Dictionary<ushort, List<T>> into, ushort territory)
    {
        if (!into.TryGetValue(territory, out var list)) into[territory] = list = [];
        return list;
    }

    private static string Unescape(string s)
    {
        if (!s.Contains('\\', StringComparison.Ordinal)) return s;
        return s.Replace("\\t", "\t").Replace("\\n", "\n")
                .Replace("\\r", "\r").Replace("\\\\", "\\");
    }
}
