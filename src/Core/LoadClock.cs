using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FrenMits;

// Times each phase of plugin load, so a stutter on update can be pinned on one of them.
public sealed class LoadClock
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly List<(string Phase, long Ms)> _marks = new();
    private long _last;

    public void Mark(string phase)
    {
        var now = _watch.ElapsedMilliseconds;
        _marks.Add((phase, now - _last));
        _last = now;
    }

    // "load 214ms (config 61, migrations 3, seeding 12, windows 138)"
    // The total is the marks added up, so no time hides between the last one and here.
    public string Report()
    {
        var total = 0L;
        foreach (var m in _marks) total += m.Ms;
        var sb = new StringBuilder($"load {total}ms (");
        for (var i = 0; i < _marks.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(_marks[i].Phase).Append(' ').Append(_marks[i].Ms);
        }
        return sb.Append(')').ToString();
    }
}
