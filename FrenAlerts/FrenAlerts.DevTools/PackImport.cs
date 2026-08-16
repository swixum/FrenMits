using FrenAlerts.Engine;

namespace FrenAlerts.DevTools;

public sealed partial class PackImport
{
    private const int Territory = 0, Name = 1, Kind = 2, Id = 3, ByName = 4,
                      Source = 5, Target = 6, Severity = 7, Delay = 8, Hold = 9,
                      Once = 10, Text = 11, Speech = 12, Sure = 14, Roles = 15,
                      Hush = 16, Jobs = 17;

    public int Rows { get; private set; }
    public int Placeholders { get; private set; }
    public int Rewritten { get; private set; }

    // Placeholders the trigger's own name could be turned into a call from.
    public int Named { get; private set; }

    // The ones it could not, listed rather than counted: each is a mechanic with
    // no call in that fight, and the list is the work that is left.
    public List<string> Unnamed { get; } = [];
    public List<string> Untouched { get; } = [];

    public List<string> Numbered { get; } = [];

    public int Collapsed { get; private set; }

    public IEnumerable<CallSpec> Collapse(IEnumerable<CallSpec> specs)
    {
        foreach (var group in specs.GroupBy(s =>
                     (s.Territory, s.On, s.MatchId, s.Text, s.Aim, s.For, s.OnlyMe,
                      s.From, s.Hush, s.Once, s.Reproduced)))
        {
            var kept = group.OrderBy(s => s.Delay).First();
            Collapsed += group.Count() - 1;
            yield return kept;
        }
    }

    public IEnumerable<CallSpec> Number(IEnumerable<CallSpec> specs)
    {
        var all = specs.ToList();
        foreach (var group in all.GroupBy(s => (s.Territory, s.On, s.MatchId)))
        {
            if (group.Key.MatchId == 0) continue;
            var keys = group.Select(s => s.DedupeKey).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (keys.Count < 2) continue;
            if (group.Select(s => (s.Aim, s.For)).Distinct().Count() > 1) continue;

            // Same words, different number, or it is not an occurrence variant.
            if (keys.Select(Digitless).Distinct().Count() != 1) continue;

            // A different delay means a sequence rather than a choice.
            if (group.Select(s => s.Delay).Distinct().Count() > 1) continue;

            var order = keys.Select((k, i) => (k, n: i + 1)).ToDictionary(p => p.k, p => p.n);
            Numbered.Add($"{group.Key.MatchId:X} {group.Key.On}: {string.Join(" then ", keys)}");
            for (var i = 0; i < all.Count; i++)
                if (all[i].Territory == group.Key.Territory && all[i].On == group.Key.On
                    && all[i].MatchId == group.Key.MatchId)
                    all[i] = all[i] with { Occurrence = order[all[i].DedupeKey] };
        }
        return all;
    }

    public IEnumerable<CallSpec> Read(IEnumerable<string> lines, ISet<ushort> territories)
    {
        foreach (var line in lines.Skip(1))
        {
            var c = line.Split('\t');
            if (c.Length <= Jobs) continue;
            if (!ushort.TryParse(c[Territory], out var zone) || !territories.Contains(zone)) continue;

            var kind = MapKind(c[Kind]);
            if (kind == EventKind.Unknown) continue;
            Rows++;

            if (c[ByName].Length > 0 && Hex(c[Id]) == 0)
            {
                Unnamed.Add($"{zone} {c[Name]} (watches for the actor \"{c[ByName]}\" by name)");
                continue;
            }

            var source = c[Text];
            var fullName = c[Name];
            var name = IdTail().Replace(fullName, "");

            var placeholder = string.IsNullOrWhiteSpace(source)
                              || source == Naming.Bare(fullName)
                              || source == Naming.Mechanic(fullName);
            var named = false;
            var text = source;
            if (placeholder)
            {
                Placeholders++;
                if (Naming.TryFor(name, out var built))
                {
                    text = built;
                    named = true;
                    Named++;
                }
                else
                {
                    text = name;
                    Unnamed.Add($"{zone} {name}");
                }
            }
            else
            {
                text = Wording.Rewrite(source);
                if (Wording.StillTheirVoice(source, text)) Untouched.Add(source);
                else Rewritten++;
            }

            // Both paths end here, so a line reads the same way whether its words
            // came from the source or from the trigger's name.
            text = Polish.Finish(text);

            yield return new CallSpec
            {
                Territory = zone,
                // Keeps the "#2" the name carried: two rows on the same mechanic are
                // two triggers, and sharing an id would make them one.
                Id = Slug(fullName),
                Key = Slug(name),
                Phase = PhaseIn(name),
                On = kind,
                MatchId = Hex(c[Id]),
                Text = text,
                // A name-built call reads the same spoken as written, so it needs
                // no separate speech line.
                Speech = placeholder ? "" : Polish.Finish(Wording.Rewrite(c[Speech])),
                // 0 info, 1 warn, 2 danger, in the table's own scale.
                Level = c[Severity] switch
                {
                    "2" => CallLevel.Alarm,
                    "1" => CallLevel.Alert,
                    _ => CallLevel.Info,
                },
                Aim = MapAim(c[Target]),
                OnlyMe = c[Target] == "1",
                Personal = c[Target] == "1",
                From = MapAim(c[Source]),
                Hush = Flt(c[Hush]),
                // Written upstream as a suppress window longer than any fight.
                Once = c[Once] == "1",
                DefaultOn = true,
                Reproduced = c[Sure] == "1",
                Delay = Flt(c[Delay]),
                Hold = Flt(c[Hold]) is var h && h > 0 ? h : 4f,
                For = c[Roles],
                NeedsWording = placeholder && !named,
            };
        }
    }

    private static EventKind MapKind(string code) => code switch
    {
        "1" => EventKind.CastStart,
        "2" => EventKind.AbilityHit,
        "3" => EventKind.StatusGain,
        "4" => EventKind.StatusLose,
        "5" => EventKind.HeadMarker,
        "6" => EventKind.Tether,
        "7" => EventKind.MapEffect,
        "9" => EventKind.ActorControl,
        "10" => EventKind.ActorSpawn,
        _ => EventKind.Unknown,
    };

    private static Aim MapAim(string code) =>
        int.TryParse(code, out var n) && Enum.IsDefined(typeof(Aim), n) ? (Aim)n : Aim.Anyone;

    private static int PhaseIn(string name)
    {
        var found = 0;
        foreach (System.Text.RegularExpressions.Match m in PhaseTag().Matches(name))
        {
            var n = int.Parse(m.Groups[1].Value);
            if (found != 0 && found != n) return 0;
            found = n;
        }
        return found;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\bP(\d)\b")]
    private static partial System.Text.RegularExpressions.Regex PhaseTag();

    // The " #2" the writer appends when one trigger covers several ids.
    [System.Text.RegularExpressions.GeneratedRegex(@"\s*#\d+\s*$")]
    private static partial System.Text.RegularExpressions.Regex IdTail();

    private static uint Hex(string s) =>
        uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0;

    private static float Flt(string s) =>
        float.TryParse(s, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;

    private static string Digitless(string key) =>
        new(key.Where(ch => !char.IsDigit(ch)).ToArray());

    private static string Slug(string name)
    {
        var chars = name.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
