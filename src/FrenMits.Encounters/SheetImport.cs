using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Encounters;

// Parses text pasted from a mit sheet, tabs or commas.
public static class SheetImport
{
    public static List<string[]> ParseGrid(string raw, out char delimiter)
    {
        // Counted, so one stray tab can't flip a comma paste.
        var tabs = 0; var commas = 0;
        foreach (var ch in raw) { if (ch == '\t') tabs++; else if (ch == ',') commas++; }
        delimiter = tabs >= commas && tabs > 0 ? '\t' : ',';
        var rows = new List<string[]>();
        foreach (var line in raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (line.Length == 0) continue;
            rows.Add(SplitLine(line, delimiter));
        }
        return rows;
    }

    private static string[] SplitLine(string line, char delimiter)
    {
        // Minimal quote-aware split, so quoted cells survive.
        var cells = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                cells.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(c);
        }
        cells.Add(sb.ToString());
        return cells.Select(s => s.Trim()).ToArray();
    }

    // Accepts m:ss, h:mm:ss or plain seconds.
    public static bool TryParseTime(string text, out float seconds)
    {
        seconds = 0f;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();

        var negative = text.StartsWith("-");
        if (negative) text = text[1..];

        if (text.Contains(':'))
        {
            var parts = text.Split(':');
            float total = 0f;
            foreach (var p in parts)
            {
                if (!float.TryParse(p, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v))
                    return false;
                total = total * 60f + v;
            }
            seconds = negative ? -total : total;
            return true;
        }

        // Strip a trailing unit like "s".
        var cleaned = new string(text.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
        if (float.TryParse(cleaned, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var sec))
        {
            seconds = negative ? -sec : sec;
            return true;
        }
        return false;
    }

    // A time typed against the phase it sits in: "P2 1:30" on a multi-phase
    // fight, "B3 1:00" on a field op. A late phase starts hundreds of seconds
    // into the pull, and a field op's bosses run on 1000s blocks, so without
    // this every call past phase one needs its number worked out by hand.
    public static bool TryParseTime(string text, uint territory, out float seconds)
    {
        seconds = 0f;
        if (string.IsNullOrWhiteSpace(text)) return false;
        // Text that opens with a letter resolves as a tag or not at all: the
        // plain parser strips letters, so "P2" alone would read as 2 seconds
        // and put the call at the top of the fight.
        if (!char.IsLetter(text.Trim()[0])) return TryParseTime(text, out seconds);
        if (!SplitPhaseTag(text, out var tag, out var rest)) return false;
        if (!TryPhaseBase(tag, territory, out var start, out _)) return false;
        if (!TryParseTime(rest, out var offset)) return false;
        seconds = start + offset;
        return true;
    }

    // What the time box reads back under a tagged time, empty for a plain one.
    public static string PhaseTimeHint(string text, uint territory)
    {
        if (!SplitPhaseTag(text, out var tag, out var rest)) return "";
        if (!TryPhaseBase(tag, territory, out var start, out var name))
            return $"{tag} is not a phase on this fight";
        if (!TryParseTime(rest, out var offset)) return $"{tag} needs a time after it";
        var at = (int)MathF.Round(Builtin.DisplayTime(territory, start + offset));
        var sign = at < 0 ? "-" : "";
        at = Math.Abs(at);
        return $"{Builtin.PhaseTitle(territory, name)}  ·  lands at {sign}{at / 60}:{at % 60:00}";
    }

    // The line a time box shows about tags, naming a tag this fight has. Empty
    // on a fight with one phase, which has nothing to tag.
    public static string PhaseTagTip(uint territory)
    {
        var phases = Builtin.PhaseStarts(territory);
        if (phases.Count < 2) return "";
        return Builtin.FieldOp(territory)
            ? $"Type B{Math.Min(3, phases.Count)} 1:00 to place it inside a boss."
            : $"Type {phases[1].Name} 1:30 to place it inside a phase.";
    }

    // "P2 1:30" and "P2+1:30" both split; a plain time carries no tag.
    private static bool SplitPhaseTag(string text, out string tag, out string rest)
    {
        tag = rest = "";
        text = text.Trim();
        if (text.Length == 0 || !char.IsLetter(text[0])) return false;
        var end = 0;
        while (end < text.Length && char.IsLetterOrDigit(text[end])) end++;
        tag = text[..end];
        rest = text[end..].TrimStart('+', ' ', '\t');
        return rest.Length > 0;
    }

    // Where a tag's clock starts. A field op shows each boss its own clock off
    // its 1000s block, so the tag adds the block rather than the boss's first
    // cast; everywhere else the phase start is what the sheet counts from.
    private static bool TryPhaseBase(string tag, uint territory, out float start, out string name)
    {
        start = 0f;
        name = "";
        var phases = Builtin.PhaseStarts(territory);
        var fieldOp = Builtin.FieldOp(territory);
        for (var i = 0; i < phases.Count; i++)
        {
            if (!string.Equals(phases[i].Name, tag, StringComparison.OrdinalIgnoreCase)
                && !(fieldOp && string.Equals($"B{i + 1}", tag, StringComparison.OrdinalIgnoreCase)))
                continue;
            start = fieldOp ? MathF.Floor(phases[i].Time / 1000f) * 1000f : phases[i].Time;
            name = phases[i].Name;
            return true;
        }
        return false;
    }

    public class Options
    {
        public int TimeColumn;
        public int MechanicColumn = 1;
        public int ActionColumn = 2;
        public bool FirstRowIsHeader = true;
        public List<string> Jobs = new(); // applied to every imported line; empty = all
    }

    // Builds lines from the grid, skipping untimed rows.
    public static List<MitLine> BuildLines(List<string[]> grid, Options opt)
    {
        var lines = new List<MitLine>();
        for (var r = opt.FirstRowIsHeader ? 1 : 0; r < grid.Count; r++)
        {
            var row = grid[r];
            if (!TryParseTime(Cell(row, opt.TimeColumn), out var time)) continue;

            var action = Cell(row, opt.ActionColumn);
            var mechanic = Cell(row, opt.MechanicColumn);
            if (string.IsNullOrWhiteSpace(action) && string.IsNullOrWhiteSpace(mechanic)) continue;

            lines.Add(new MitLine
            {
                Time = time,
                Mechanic = mechanic,
                Action = action,
                Jobs = new List<string>(opt.Jobs),
                Enabled = true
            });
        }
        return lines;
    }

    private static string Cell(string[] row, int index)
        => index >= 0 && index < row.Length ? row[index] : "";
}
