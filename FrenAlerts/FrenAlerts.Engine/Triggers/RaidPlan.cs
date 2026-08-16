using System.Text.RegularExpressions;

namespace FrenAlerts.Engine;

// One mechanic from a raid plan, and who does what in it.
public sealed record PlanEntry(string Mechanic, IReadOnlyDictionary<string, string> BySlot);

public static partial class RaidPlan
{
    // The slot standard the sheets use, so a plan written for one party resolves
    // against a different one.
    public static readonly string[] Slots = ["MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2"];

    public const int MaxEntries = 200;

    // "Wave Cannon: MT N, OT S, H1 NW" and the same with dashes or tabs between.
    [GeneratedRegex(@"^\s*(?<mechanic>[^:]{2,60}?)\s*[::]\s*(?<body>.+?)\s*$")]
    private static partial Regex Headed();

    [GeneratedRegex(@"(?<![A-Za-z0-9])(?<slot>MT|OT|H1|H2|M1|M2|R1|R2)(?![A-Za-z0-9])\s*[-:]?\s*(?<what>[^,;/|]+)",
                    RegexOptions.IgnoreCase)]
    private static partial Regex Assignment();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Runs();

    // Lines a plan is full of that are not assignments: headings, timestamps,
    // and the note somebody left about who is out this week.
    [GeneratedRegex(@"^\s*(?:#|//|\*|-{3,}|={3,})")]
    private static partial Regex NotAPlanLine();

    public static IReadOnlyList<PlanEntry> Read(IEnumerable<string> lines)
    {
        var found = new List<PlanEntry>();

        var pending = "";
        var open = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            if (found.Count >= MaxEntries) break;

            var line = Runs().Replace(raw ?? "", " ").Trim();
            if (line.Length == 0 || NotAPlanLine().IsMatch(line))
            {
                Close(found, ref pending, open);
                continue;
            }

            var headed = Headed().Match(line);
            if (headed.Success)
            {
                var body = headed.Groups["body"].Value;
                var slots = SlotsIn(body);

                if (slots.Count > 0)
                {
                    // Everything on one line, which closes anything still open.
                    Close(found, ref pending, open);
                    Add(found, headed.Groups["mechanic"].Value, slots);
                    continue;
                }

                // A slot line belonging to the mechanic above it: "H1: northwest".
                var lead = SlotsIn(line);
                if (lead.Count > 0 && pending.Length > 0)
                {
                    foreach (var (slot, what) in lead) open[slot] = what;
                    continue;
                }

                // A heading with nothing on it yet.
                Close(found, ref pending, open);
                pending = Clean(headed.Groups["mechanic"].Value);
                continue;
            }

            var inline = SlotsIn(line);
            if (inline.Count > 0 && pending.Length > 0)
            {
                foreach (var (slot, what) in inline) open[slot] = what;
                continue;
            }

            // A bare line with no slots on it starts a new mechanic.
            Close(found, ref pending, open);
            if (inline.Count == 0) pending = Clean(line);
        }

        Close(found, ref pending, open);
        return found;
    }

    private static void Close(List<PlanEntry> into, ref string pending, Dictionary<string, string> open)
    {
        if (pending.Length > 0 && open.Count > 0) Add(into, pending, open);
        pending = "";
        open.Clear();
    }

    private static void Add(List<PlanEntry> into, string mechanic, IEnumerable<KeyValuePair<string, string>> slots)
    {
        var name = Clean(mechanic);
        if (name.Length == 0) return;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slot, what) in slots)
            if (what.Length > 0) map[slot.ToUpperInvariant()] = what;

        if (map.Count > 0 && into.Count < MaxEntries) into.Add(new PlanEntry(name, map));
    }

    private static Dictionary<string, string> SlotsIn(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Assignment().Matches(text))
        {
            var what = Clean(m.Groups["what"].Value);
            // A slot listed with nothing after it is a roster, not an assignment.
            if (what.Length == 0) continue;
            map[m.Groups["slot"].Value.ToUpperInvariant()] = what;
        }
        return map;
    }

    private static string Clean(string s) => s.Trim(' ', '\t', '-', ':', '.', '•', '*', '–');

    public static IEnumerable<CallSpec> Apply(
        IReadOnlyList<CallSpec> pack, IReadOnlyList<PlanEntry> plan, ushort territory,
        ICollection<string>? unmatched = null)
    {
        var mine = pack.Where(s => s.Territory == territory).ToList();

        foreach (var entry in plan)
        {
            var target = Best(mine, entry.Mechanic);
            if (target is null)
            {
                unmatched?.Add(entry.Mechanic);
                continue;
            }

            foreach (var (slot, what) in entry.BySlot)
                yield return target with
                {
                    Id = $"{target.Id}-plan-{slot.ToLowerInvariant()}",
                    // Its own key per slot, or the first one said would suppress the
                    // rest as repeats of itself.
                    Key = $"{target.DedupeKey}-plan-{slot.ToLowerInvariant()}",
                    Text = what,
                    Speech = "",
                    For = slot,
                    DefaultOn = true,
                    NeedsWording = false,
                };
        }
    }

    // The call whose wording contains the mechanic's name, longest match winning so
    // "Wave Cannon 2" is not taken by a call that only says "Wave Cannon".
    private static CallSpec? Best(IReadOnlyList<CallSpec> pack, string mechanic)
    {
        var wanted = Normalise(mechanic);
        if (wanted.Length < 3) return null;

        CallSpec? best = null;
        var bestLength = 0;

        foreach (var spec in pack)
        {
            var haystack = Normalise($"{spec.Text} {spec.Key}");
            if (!haystack.Contains(wanted, StringComparison.Ordinal)) continue;
            if (wanted.Length <= bestLength) continue;
            best = spec;
            bestLength = wanted.Length;
        }

        return best;
    }

    private static string Normalise(string s) =>
        new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
