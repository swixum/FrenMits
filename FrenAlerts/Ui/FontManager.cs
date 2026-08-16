using System;
using System.Collections.Generic;
using Dalamud.Interface.ManagedFontAtlas;

namespace FrenAlerts.Ui;

public class FontManager : IDisposable
{
    private sealed class Entry
    {
        public IFontHandle Handle = null!;
        public int Px;
        public long Used;
    }

    private readonly Dictionary<int, Entry> _entries = new();
    private readonly List<(IFontHandle Handle, long Frame)> _retired = new();
    private long _frame;

    // Headroom, since steady state is about four handles.
    private const int MaxHandles = 16;

    // Frames a handle sits retired before disposal.
    private const int RetireFrames = 3;

    // Sizes snap to 2px so a slider nudge reuses a handle.
    public static int SnapPx(float sizePx) => (int)MathF.Round(Math.Clamp(sizePx, 8f, 160f) / 2f) * 2;

    // Once per frame: age the retirement list and dispose.
    public void Tick()
    {
        _frame++;
        if (_retired.Count == 0) return;
        for (var i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < RetireFrames) continue;
            try { _retired[i].Handle.Dispose(); }
            catch (Exception ex) { Service.Log.Warning(ex, "font dispose failed"); }
            _retired.RemoveAt(i);
        }
    }

    public IFontHandle? Get(float sizePx)
    {
        var px = SnapPx(sizePx);
        if (_entries.TryGetValue(px, out var hit))
        {
            hit.Used = _frame;
            return hit.Handle;
        }

        // Full: retire the least recently used one.
        if (_entries.Count >= MaxHandles)
        {
            var oldest = 0;
            var oldestUsed = long.MaxValue;
            foreach (var (k, e) in _entries)
                if (e.Used < oldestUsed) { oldestUsed = e.Used; oldest = k; }
            if (oldest > 0 && _entries.Remove(oldest, out var dead))
                _retired.Add((dead.Handle, _frame));
        }

        try
        {
            var handle = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(px)));
            _entries[px] = new Entry { Handle = handle, Px = px, Used = _frame };
            return handle;
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "failed to build a font handle");
            return null;
        }
    }

    // The closest ready handle, while the exact one builds.
    public (IFontHandle Handle, int Px)? Nearest(float sizePx)
    {
        var want = SnapPx(sizePx);
        IFontHandle? best = null;
        var bestPx = 0;
        var bestGap = int.MaxValue;
        foreach (var e in _entries.Values)
        {
            if (!e.Handle.Available) continue;
            var gap = Math.Abs(e.Px - want);
            if (gap >= bestGap) continue;
            bestGap = gap;
            best = e.Handle;
            bestPx = e.Px;
        }
        return best == null ? null : (best, bestPx);
    }

    // Build the configured sizes before anything draws them.
    public void WarmIfNeeded(Configuration c)
    {
        var stamp = $"{SnapPx(c.CallFontSizePx)}|{SnapPx(c.CallFontSizePx * 0.55f)}|{SnapPx(18f * c.UiScale)}";
        if (string.Equals(stamp, _warmStamp, StringComparison.Ordinal)) return;
        _warmStamp = stamp;

        Get(c.CallFontSizePx);
        Get(c.CallFontSizePx * 0.55f);   // the mechanic subline under the call
        Get(18f * c.UiScale);            // the config window at its own scale
    }

    // The scale to draw at when borrowing another size.
    public static float Correction(int wantPx, int havePx)
    {
        if (havePx <= 0 || wantPx <= 0) return 1f;
        var ratio = wantPx / (float)havePx;
        return MathF.Abs(ratio - 1f) < 0.02f ? 1f : ratio;
    }

    private string _warmStamp = "";

    public void Dispose()
    {
        foreach (var e in _entries.Values)
        {
            try { e.Handle.Dispose(); } catch (Exception ex) { Service.Log.Warning(ex, "font dispose failed"); }
        }
        _entries.Clear();
        foreach (var (h, _) in _retired)
        {
            try { h.Dispose(); } catch (Exception ex) { Service.Log.Warning(ex, "font dispose failed"); }
        }
        _retired.Clear();
    }
}
