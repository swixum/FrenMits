using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FrenMits.Cues;

// How loud a call feels. The overlay maps these onto its own colors.
public enum AlertLevel { Info = 0, Warn, Danger }

// One call as it ships. Read from the pack and never written back, so a reset
// always has something true to go back to.
//
// The pack is derived work under the Apache License 2.0. See NOTICE.md.
public sealed record BossAlert
{
    public uint Territory { get; init; }

    // Stable name, which is also the config key and the voice clip name.
    public string Key { get; init; } = "";

    public string Text { get; init; } = "";

    public string Tts { get; init; } = "";

    public AlertLevel Level { get; init; }

    // What the engine waits for. The page never shows these; the runner needs
    // every one of them, and they come from the same row.
    public FrenMits.Callouts.TriggerMatch Match { get; init; } = new();

    public string Jobs { get; init; } = "";

    public float Suppress { get; init; }

    public bool OncePerPull { get; init; }

    // "tank", "healer", "dps", or several separated by a comma. Empty is everyone.
    public string Roles { get; init; } = "";

    public bool On { get; init; } = true;

    // Seconds before the thing lands, or 0 for "as it starts".
    public float Lead { get; init; }

    public float Hold { get; init; } = 4f;

    // A key reads "FRU P2 Absolute Zero": a fight code, sometimes a phase, then
    // the mechanic. The list groups by the phase and labels by the mechanic, so
    // both are pulled out of the one string.
    public string Group => Split().Phase;

    public string Mechanic => Split().Mechanic;

    private (string Phase, string Mechanic) Split()
    {
        var rest = Key;

        // A trailing "#2" only says which of several ids this row watches.
        var tag = rest.LastIndexOf(" #", StringComparison.Ordinal);
        if (tag > 0 && rest[(tag + 2)..].All(char.IsDigit)) rest = rest[..tag];

        // A long duty writes its whole name into every key. Whatever they all
        // share is the fight, not the mechanic. Never strip it down to nothing.
        if (Prefix.Length > 0 && rest.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var left = rest[Prefix.Length..].TrimStart();
            if (left.Length > 0) rest = left;
        }

        var parts = rest.Split(' ');
        var at = 0;
        var phase = "Everything else";

        // A key opens with up to two throat-clearing words, in either order:
        // the fight's short name and the phase. The phase is tested first,
        // because "P1" also looks like a fight code and is not one.
        for (var i = 0; i < 2 && at < parts.Length - 1; i++)
        {
            if (phase == "Everything else" && IsPhase(parts[at]))
            {
                // A mechanic spanning two phases files under the first of them.
                phase = "Phase " + parts[at].Split('/')[0].TrimStart('P', 'p');
                at++;
            }
            else if (IsFightCode(parts[at])) at++;
            else break;
        }

        var mechanic = string.Join(' ', parts.Skip(at));
        return (phase, mechanic.Length > 0 ? mechanic : rest);
    }

    private static bool IsFightCode(string word)
        => word.Length is >= 2 and <= 6
            && char.IsUpper(word[0])
            && (word.All(c => char.IsUpper(c) || char.IsDigit(c)) || word.Any(char.IsDigit));

    // "P2", "P2.5", and "P4/P5" for a mechanic that spans two of them.
    private static bool IsPhase(string word)
        => word.Length > 1 && word[0] is 'P' or 'p'
            && word[1..].All(c => char.IsDigit(c) || c is '.' or '/' or 'P' or 'p');

    // The game's own art for whatever this call is about: the debuff for a
    // status trigger, the ability for a cast. Zero when neither applies, which
    // the row draws as an empty slot so the text still lines up.
    public uint Icon => Match.Id == 0 ? 0u : Match.Kind switch
    {
        FrenMits.Callouts.EventKind.StatusGain or FrenMits.Callouts.EventKind.StatusLose
            => FrenMits.Ui.Icons.ByStatusId(Match.Id),
        FrenMits.Callouts.EventKind.CastStart or FrenMits.Callouts.EventKind.Ability
            => FrenMits.Ui.Icons.ByActionId(Match.Id),
        _ => 0u,
    };

    // A row whose only wording is its own name has none: the real call depends
    // on fight state nothing has ported yet, so it ships off and says so. Both
    // halves are needed. Some real calls are honestly named after the mechanic
    // and say so on purpose, and those ship on.
    public bool NamedOnly => !On && Text == Mechanic;

    // Words every key in this duty starts with, which is the fight's own name
    // and belongs to none of the mechanics. Set when the book loads.
    public string Prefix { get; init; } = "";
}

// What the player changed about one call. Only the fields they touched are
// stored, so a call they never opened costs nothing and follows the pack when
// the pack improves.
[Serializable]
public sealed class AlertTweak
{
    public bool? On { get; set; }
    public string? Text { get; set; }
    public string? Tts { get; set; }
    public string? Sound { get; set; }
    public AlertLevel? Level { get; set; }
    public string? Roles { get; set; }

    // Nothing set means nothing to store, so an untouched call leaves no trace.
    public bool Empty => On is null && Text is null && Tts is null
        && Sound is null && Level is null && Roles is null;
}

// Every call the plugin knows about, by duty.
//
// The pack is a tab separated file written by the bake, shipped beside the
// sheets. Only the columns the settings page shows are read here; the engine
// reads the whole thing. The version is checked rather than assumed, so a pack
// that grew a column says so instead of loading half of itself.
public sealed class AlertBook
{
    public const string Magic = "fmtrig";
    public const int Version = 5;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly Dictionary<uint, List<BossAlert>> _byDuty = new();

    public string Problem { get; private set; } = "";

    public int Count { get; private set; }

    public IReadOnlyList<BossAlert> For(uint territory)
        => _byDuty.TryGetValue(territory, out var list) ? list : [];

    public IReadOnlyCollection<uint> Duties => _byDuty.Keys;

    public static AlertBook Load(string path)
    {
        var book = new AlertBook();
        if (!File.Exists(path))
        {
            book.Problem = "No call pack is installed yet.";
            return book;
        }

        try
        {
            using var r = new StreamReader(path);
            var header = (r.ReadLine() ?? "").Split(' ');
            if (header.Length != 2 || header[0] != Magic)
            {
                book.Problem = "That file is not a call pack.";
                return book;
            }
            if (!int.TryParse(header[1], NumberStyles.Integer, Inv, out var v) || v != Version)
            {
                book.Problem = $"Call pack version {header[1]}, expected {Version}.";
                return book;
            }

            while (r.ReadLine() is { } line)
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var f = line.Split('\t');
                if (f.Length < 16) continue;
                if (!uint.TryParse(f[0], NumberStyles.Integer, Inv, out var duty)) continue;

                var alert = new BossAlert
                {
                    Territory = duty,
                    Key = Unescape(f[1]),
                    Match = new FrenMits.Callouts.TriggerMatch
                    {
                        Kind = (FrenMits.Callouts.EventKind)Num(f[2], 0),
                        Id = uint.TryParse(f[3], NumberStyles.HexNumber, Inv, out var aid) ? aid : 0u,
                        Name = Unescape(f[4]),
                        Source = (FrenMits.Callouts.ActorScope)Num(f[5], 0),
                        Target = (FrenMits.Callouts.ActorScope)Num(f[6], 0),
                    },
                    Level = (AlertLevel)Num(f[7], 0),
                    OncePerPull = f[10] == "1",
                    Suppress = f.Length > 16 ? Real(f[16]) : 0f,
                    Jobs = f.Length > 17 ? Unescape(f[17]) : "",
                    Lead = Real(f[8]),
                    Hold = Real(f[9]) is var h && h > 0f ? h : 4f,
                    Text = Unescape(f[11]),
                    Tts = Unescape(f[12]),
                    On = f[14] != "0",
                    Roles = Unescape(f[15]),
                };
                if (alert.Text.Length == 0) continue;

                if (!book._byDuty.TryGetValue(duty, out var list))
                    book._byDuty[duty] = list = new List<BossAlert>();
                list.Add(alert);
                book.Count++;
            }
        }
        catch (Exception e)
        {
            book.Problem = "Could not read the call pack: " + e.Message;
        }

        foreach (var duty in book._byDuty.Keys.ToList())
        {
            var list = book._byDuty[duty];
            var prefix = SharedStart(list);
            if (prefix.Length > 0)
                book._byDuty[duty] = list = list.Select(a => a with { Prefix = prefix }).ToList();
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        }
        return book;
    }

    // The words every key in a duty opens with. One call has no shared start to
    // speak of, and the last word is never taken, so a mechanic always has a
    // name left.
    private static string SharedStart(List<BossAlert> calls)
    {
        if (calls.Count < 2) return "";

        var first = calls[0].Key.Split(' ');
        var shared = first.Length - 1;
        foreach (var a in calls)
        {
            var words = a.Key.Split(' ');
            var same = 0;
            while (same < shared && same < words.Length - 1
                && string.Equals(words[same], first[same], StringComparison.Ordinal))
                same++;
            shared = same;
            if (shared == 0) return "";
        }
        return string.Join(' ', first.Take(shared));
    }

    private static int Num(string s, int fallback)
        => int.TryParse(s, NumberStyles.Integer, Inv, out var n) ? n : fallback;

    private static float Real(string s)
        => float.TryParse(s, NumberStyles.Float, Inv, out var n) ? n : 0f;

    // The bake escapes the two characters that would break a line.
    private static string Unescape(string s)
        => s.IndexOf('\\') < 0 ? s
            : s.Replace("\\t", "\t").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\\", "\\");
}
