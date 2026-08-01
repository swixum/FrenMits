using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface.ManagedFontAtlas;

namespace FrenMits;

// Overlay font handles, one per family, style and size.
public class FontManager : IDisposable
{
    private sealed class Entry
    {
        public IFontHandle Handle = null!;
        public int Px;
        public string File = "";   // "" = Dalamud's own font
        public long Used;
    }

    private readonly Dictionary<string, Entry> _entries = new();
    private readonly List<(IFontHandle Handle, long Frame)> _retired = new();
    private long _frame;

    // Headroom, since steady state is about seven handles.
    private const int MaxHandles = 32;

    // Frames a handle sits retired before disposal.
    private const int RetireFrames = 3;

    // Family to its regular, bold and italic filenames.
    private static readonly Dictionary<string, (string Reg, string? Bold, string? Ital, string? BoldItal)> Families = new()
    {
        ["Segoe UI"]        = ("segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf"),
        ["Arial"]           = ("arial.ttf",   "arialbd.ttf",  "ariali.ttf",   "arialbi.ttf"),
        ["Verdana"]         = ("verdana.ttf", "verdanab.ttf", "verdanai.ttf", "verdanaz.ttf"),
        ["Tahoma"]          = ("tahoma.ttf",  "tahomabd.ttf", null,           null),
        ["Trebuchet MS"]    = ("trebuc.ttf",  "trebucbd.ttf", "trebucit.ttf", "trebucbi.ttf"),
        ["Georgia"]         = ("georgia.ttf", "georgiab.ttf", "georgiai.ttf", "georgiaz.ttf"),
        ["Times New Roman"] = ("times.ttf",   "timesbd.ttf",  "timesi.ttf",   "timesbi.ttf"),
        ["Consolas"]        = ("consola.ttf", "consolab.ttf", "consolai.ttf", "consolaz.ttf"),
        ["Comic Sans MS"]   = ("comic.ttf",   "comicbd.ttf",  "comici.ttf",   "comicz.ttf"),
        ["Impact"]          = ("impact.ttf",  null,           null,           null),
    };

    // For the Display tab's dropdown.
    public static readonly string[] FamilyNames = new[] { "Default" }.Concat(Families.Keys).ToArray();

    // Sizes snap to 2px so a slider nudge reuses a handle.
    public static int SnapPx(float sizePx) => (int)MathF.Round(Math.Clamp(sizePx, 8f, 160f) / 2f) * 2;

    private static string? ResolveFile(string family, bool bold, bool italic)
    {
        if (!Families.TryGetValue(family, out var f)) return null;
        var name = (bold, italic) switch
        {
            (true, true) => f.BoldItal ?? f.Bold ?? f.Reg,
            (true, false) => f.Bold ?? f.Reg,
            (false, true) => f.Ital ?? f.Reg,
            _ => f.Reg,
        };
        try
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var path = Path.Combine(dir, name);
            return File.Exists(path) ? path : null;
        }
        catch (Exception ex) { Swallowed.Report("font lookup", ex); return null; }
    }

    private static string FileFor(string family, bool bold, bool italic)
        => (string.IsNullOrEmpty(family) || family == "Default"
            ? null
            : ResolveFile(family, bold, italic)) ?? "";

    // Once per frame: age the retirement list and dispose.
    public void Tick()
    {
        _frame++;
        if (_retired.Count == 0) return;
        for (var i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < RetireFrames) continue;
            try { _retired[i].Handle.Dispose(); }
            catch (Exception ex) { Swallowed.Report("font dispose", ex); }
            _retired.RemoveAt(i);
        }
    }

    public IFontHandle? Get(float sizePx, string family, bool bold, bool italic)
        => GetByFile(SnapPx(sizePx), FileFor(family, bold, italic));

    private IFontHandle? GetByFile(int px, string file)
    {
        var key = $"{file}|{px}";
        if (_entries.TryGetValue(key, out var hit))
        {
            hit.Used = _frame;
            return hit.Handle;
        }

        // Full: retire the least recently used one.
        if (_entries.Count >= MaxHandles)
        {
            var oldest = "";
            var oldestUsed = long.MaxValue;
            foreach (var (k, e) in _entries)
                if (e.Used < oldestUsed) { oldestUsed = e.Used; oldest = k; }
            if (oldest.Length > 0 && _entries.Remove(oldest, out var dead))
                _retired.Add((dead.Handle, _frame));
        }

        try
        {
            var handle = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk =>
                {
                    if (file.Length == 0)
                        tk.AddDalamudDefaultFont(px);
                    else
                        tk.AddFontFromFile(file, new SafeFontConfig { SizePx = px });
                }));
            _entries[key] = new Entry { Handle = handle, Px = px, File = file, Used = _frame };
            return handle;
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: failed to build font handle");
            return null;
        }
    }

    // The closest ready handle, while the exact one builds.
    public (IFontHandle Handle, int Px)? Nearest(float sizePx, string family, bool bold, bool italic)
    {
        var want = SnapPx(sizePx);
        var file = FileFor(family, bold, italic);
        IFontHandle? best = null;
        var bestPx = 0;
        var bestGap = int.MaxValue;
        foreach (var e in _entries.Values)
        {
            if (e.File != file || !e.Handle.Available) continue;
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
        var stamp = $"{c.OverlayFontFamily}|{c.OverlayFontBold}|{c.OverlayFontItalic}|"
                    + $"{c.CombatTimerFontFamily}|{c.CombatTimerFontBold}|{c.CombatTimerFontItalic}|"
                    + $"{SnapPx(c.OverlayFontSizePx)}|{SnapPx(c.UpcomingFontSizePx)}|"
                    + $"{SnapPx(c.MitBarFontSizePx)}|{SnapPx(c.PrepCheckFontSizePx)}|"
                    + $"{SnapPx(c.CombatTimerFontSizePx)}";
        if (string.Equals(stamp, _warmStamp, StringComparison.Ordinal)) return;
        _warmStamp = stamp;

        // Every overlay that pushes a font, at its current size.
        Get(c.OverlayFontSizePx, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
        Get(c.UpcomingFontSizePx, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
        Get(c.MitBarFontSizePx, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
        Get(c.PrepCheckFontSizePx, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
        Get(c.CombatTimerFontSizePx, c.CombatTimerFontFamily, c.CombatTimerFontBold, c.CombatTimerFontItalic);
        // The secondary text is a fraction of the overlay size.
        Get(c.OverlayFontSizePx * 0.5f, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
        Get(c.OverlayFontSizePx * 0.55f, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
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
            try { e.Handle.Dispose(); } catch (Exception ex) { Swallowed.Report("font dispose", ex); }
        }
        _entries.Clear();
        foreach (var (h, _) in _retired)
        {
            try { h.Dispose(); } catch (Exception ex) { Swallowed.Report("font dispose", ex); }
        }
        _retired.Clear();
    }
}
