using System;
using System.Collections.Generic;

namespace FrenMits.Encounters;

// Splitting one planned call that names several actions into one line each.
// Pure text and line work, shared by the plan store and the sheet baker.
public static class LineSplit
{
    private static readonly HashSet<string> JobAbbrs = new(Jobs.Abbreviations, StringComparer.OrdinalIgnoreCase);

    // Splits every combined line in place. True when anything changed.
    public static bool SplitLineList(List<MitLine> lines)
    {
        var dirty = false;
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line.Action)) continue;
            var segments = SplitTopLevel(line.Action);
            if (segments.Count <= 1) continue;

            // Replace this one line with N lines, one per action segment.
            lines.RemoveAt(i);
            for (var s = 0; s < segments.Count; s++)
            {
                var seg = segments[s].Trim();
                if (seg.Length == 0) continue;
                var jobs = ExtractJobGate(ref seg);
                var newLine = new MitLine
                {
                    Time = line.Time,
                    Mechanic = line.Mechanic,
                    Action = seg,
                    Jobs = jobs.Count > 0 ? jobs : new List<string>(line.Jobs),
                    Enabled = line.Enabled,
                    Custom = line.Custom,
                    OffsetSeconds = line.OffsetSeconds,
                    OffsetManual = line.OffsetManual,
                    CoverUntil = line.CoverUntil,
                    LeadOverride = line.LeadOverride,
                    Tts = s == 0 ? line.Tts : "",       // only the first segment inherits TTS
                    Sound = line.Sound,
                    Color = line.Color,
                    IconId = s == 0 ? line.IconId : 0u,  // only the first segment inherits icon
                };
                lines.Insert(i + s, newLine);
            }
            dirty = true;
        }
        return dirty;
    }

    // Splits an action string at top-level '+' characters, respecting
    // parenthesised groups so "(WAR/PLD)" stays intact.
    private static List<string> SplitTopLevel(string action)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < action.Length; i++)
        {
            var c = action[i];
            if (c == '(') depth++;
            else if (c == ')') { if (depth > 0) depth--; }
            else if (depth == 0 && c == '+')
            {
                parts.Add(action[start..i]);
                start = i + 1;
            }
        }
        parts.Add(action[start..]);
        return parts;
    }

    // Pulls a parenthesised job gate like "(WAR/PLD)" out of the segment and
    // strips it. Empty when there is no gate.
    private static List<string> ExtractJobGate(ref string segment)
    {
        var jobs = new List<string>();
        var i = segment.IndexOf('(');
        if (i < 0) return jobs;
        var j = segment.IndexOf(')', i + 1);
        if (j < 0) return jobs;

        var inside = segment.Substring(i + 1, j - i - 1);
        var tokens = inside.Split('/');
        var allJobs = tokens.Length > 0;
        foreach (var t in tokens)
        {
            var tok = t.Trim();
            if (tok.Length == 0 || !JobAbbrs.Contains(tok)) { allJobs = false; break; }
        }
        if (!allJobs) return jobs;

        // Valid job gate found, extract and strip.
        foreach (var t in tokens)
            jobs.Add(t.Trim().ToUpperInvariant());

        segment = (segment[..i] + segment[(j + 1)..]).Trim();
        return jobs;
    }
}
