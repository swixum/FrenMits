using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Parses text pasted straight from a mit-sheet (Google Sheets / Excel copy is
// tab-separated; CSV is comma-separated). The caller maps which columns hold
// the time, the mechanic, and the action so any sheet layout works.
public static class SheetImport
{
    public static List<string[]> ParseGrid(string raw, out char delimiter)
    {
        // Counted, not first-hit: one stray tab inside a comma CSV must not
        // flip the whole paste to tab-separated.
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
        // Minimal quote-aware split so quoted cells containing the delimiter survive.
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

    // Accepts "m:ss", "mm:ss", "h:mm:ss", or plain seconds. Returns false if the
    // cell has no usable time.
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

    public class Options
    {
        public int TimeColumn;
        public int MechanicColumn = 1;
        public int ActionColumn = 2;
        public bool FirstRowIsHeader = true;
        public List<string> Jobs = new(); // applied to every imported line; empty = all
    }

    // Builds lines from the grid. Rows without a parseable time are skipped
    // (these are usually headers / section separators).
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
