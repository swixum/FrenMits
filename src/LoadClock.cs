using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FrenMits;

// Times each phase of plugin load, so a stutter on update can be pinned on one of them.
public sealed class LoadClock
{
    // A plugin update runs Dispose and the new constructor on the game's thread.
    // One phase past this is a visible hitch; a whole path past the total is what
    // users call a freeze. Crossing either logs a warning naming the phase, so a
    // regression says so itself instead of waiting to be reported.
    public const long PhaseBudgetMs = 50;
    public const long TotalBudgetMs = 250;

    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly List<(string Phase, long Ms)> _marks = new();
    private long _last;

    public void Mark(string phase)
    {
        var now = _watch.ElapsedMilliseconds;
        _marks.Add((phase, now - _last));
        _last = now;
    }

    public long Total
    {
        get { var t = 0L; foreach (var m in _marks) t += m.Ms; return t; }
    }

    public bool OverBudget
    {
        get
        {
            if (Total > TotalBudgetMs) return true;
            foreach (var m in _marks) if (m.Ms > PhaseBudgetMs) return true;
            return false;
        }
    }

    // Every phase past the per-phase budget, worst first, for the warning line.
    public string Slowest()
    {
        var over = new List<(string Phase, long Ms)>();
        foreach (var m in _marks) if (m.Ms > PhaseBudgetMs) over.Add(m);
        over.Sort((a, b) => b.Ms.CompareTo(a.Ms));
        var sb = new StringBuilder();
        for (var i = 0; i < over.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(over[i].Phase).Append(' ').Append(over[i].Ms).Append("ms");
        }
        return sb.ToString();
    }

    // "load 214ms (config 61, migrations 3, seeding 12, windows 138!)"
    // A phase past its budget is flagged, so the line reads on its own.
    // The total is the marks added up, so no time hides between the last one and here.
    public string Report(string label = "load")
    {
        var sb = new StringBuilder($"{label} {Total}ms (");
        for (var i = 0; i < _marks.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(_marks[i].Phase).Append(' ').Append(_marks[i].Ms);
            if (_marks[i].Ms > PhaseBudgetMs) sb.Append('!');
        }
        return sb.Append(')').ToString();
    }
}
