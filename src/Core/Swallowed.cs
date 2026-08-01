using System;
using System.Collections.Generic;

namespace FrenMits;

// Reporting for errors that are deliberately swallowed.
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

    // How long a site stays quiet, so it can't flood the log.
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

    // Everything that failed this session, worst first.
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

    // Lock-free, so the header can ask this every frame.
    private static volatile int _distinctSites;
    public static bool Any => _distinctSites > 0;

    // The worst offender only, so the dot stays cheap.
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
