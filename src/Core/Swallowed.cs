using System;
using System.Collections.Generic;

namespace FrenMits;

// Reporting for errors that are deliberately swallowed.
//
// Reading game state from a draw loop has to be defensive: an actor can go stale
// mid-frame, and a sheet can move under us on patch day. Catching that is right.
// Catching it SILENTLY is not - when a game patch changes a sheet's shape, the
// feature that depends on it dies for good and the log says nothing, so it reads
// as "the recap just stopped working" with nothing to go on.
//
// So each site reports through here instead: counted forever, logged at most once
// a minute, and surfaced on the settings page so a bug report can say which part
// is failing and how often.
public static class Swallowed
{
    private sealed class Site
    {
        public int Count;
        public DateTime First = DateTime.UtcNow;
        public DateTime Last;
        public DateTime LastLogged = DateTime.MinValue;
        public string Message = "";
    }

    private static readonly Dictionary<string, Site> _sites = new(StringComparer.Ordinal);
    private static readonly object _gate = new();

    // How long a site stays quiet after logging, so a per-frame failure can't
    // flood the log.
    private static readonly TimeSpan Quiet = TimeSpan.FromMinutes(1);

    public static void Report(string site, Exception ex)
    {
        bool log;
        int count;
        lock (_gate)
        {
            if (!_sites.TryGetValue(site, out var s)) { _sites[site] = s = new Site(); _distinctSites = _sites.Count; }
            s.Count++;
            s.Last = DateTime.UtcNow;
            s.Message = ex.Message;
            count = s.Count;
            log = DateTime.UtcNow - s.LastLogged >= Quiet;
            if (log) s.LastLogged = DateTime.UtcNow;
        }
        if (log)
            Service.Log?.Warning(ex, $"[FrenMits] {site} failed (x{count} this session)");
    }

    public readonly record struct Entry(string Site, int Count, DateTime First, DateTime Last, string Message);

    // Everything that has failed this session, worst first, for the settings page.
    public static List<Entry> All()
    {
        lock (_gate)
        {
            var list = new List<Entry>(_sites.Count);
            foreach (var (site, s) in _sites)
                list.Add(new Entry(site, s.Count, s.First, s.Last, s.Message));
            list.Sort((a, b) => b.Count.CompareTo(a.Count));
            return list;
        }
    }

    // Lock-free so the settings header can ask every frame without paying for a
    // lock, an allocation and a sort just to decide whether to draw a dot.
    private static volatile int _distinctSites;
    public static bool Any => _distinctSites > 0;

    // The worst offender only, so the header's dot costs one short lock rather
    // than a list and a sort.
    public static Entry Worst()
    {
        lock (_gate)
        {
            var best = new Entry("", 0, default, default, "");
            foreach (var (site, s) in _sites)
                if (s.Count > best.Count) best = new Entry(site, s.Count, s.First, s.Last, s.Message);
            return best;
        }
    }

    public static int TotalCount
    {
        get { lock (_gate) { var n = 0; foreach (var s in _sites.Values) n += s.Count; return n; } }
    }

    public static void Clear() { lock (_gate) { _sites.Clear(); _distinctSites = 0; } }
}
