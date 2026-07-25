using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface.ManagedFontAtlas;

namespace FrenMits;

// Builds crisp font handles for the overlay, each (family, style, size) built
// once and cached.
//
// A handle is NOT ready the moment it's created: NewDelegateFontHandle kicks off
// an atlas build on a background thread and Available stays false until it lands,
// a few frames later. Anything drawn in the meantime used to fall back to
// magnifying the ~12px bitmap atlas, which is where the blocky first frames after
// pressing Test came from - the main call overlay defaults to 40px, so that was a
// 2.2x blow-up of a bitmap.
//
// Three things keep that off the screen now:
//   Warm       - build the sizes actually configured before anything draws them
//   Nearest    - if a handle still isn't ready, borrow the closest one that IS,
//                so the worst case is a real font scaled slightly rather than a
//                bitmap scaled a lot
//   LRU + Tick - evict one handle at a time instead of dumping every one of them
//                (which made every overlay go blocky at once), and hold the
//                disposal for a few frames in case a window is mid-draw with it
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

    // Steady state is about seven handles (five overlays plus the call's two
    // derived sizes), so this is headroom rather than a working set - handles are
    // fonts baked into a shared atlas, and hoarding them costs texture memory.
    //
    // The cap mattering less is the point: it used to be 24 and dumped ALL of them
    // on overflow, which one text-size slider could trigger on its own (the combat
    // timer's runs 12-120px, or 55 handles on the 2px grid) and took every overlay
    // blocky with it. Evicting one at a time means a slider drag now pushes out the
    // sizes it just passed through, not the ones being drawn.
    private const int MaxHandles = 32;

    // Frames a handle sits retired before it's really disposed. Anything mid-draw
    // has long since popped it by then.
    private const int RetireFrames = 3;

    // Selectable families -> (regular, bold, italic, bold-italic) filenames in the
    // Windows Fonts folder.
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

    // For the Display-tab dropdown.
    public static readonly string[] FamilyNames = new[] { "Default" }.Concat(Families.Keys).ToArray();

    // Sizes snap to a 2px grid so a slider nudge reuses a handle instead of
    // building a new one on every notch.
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

    // Once per frame: age the retirement list and actually dispose what's old
    // enough. Kept separate from Get so eviction never disposes a handle that a
    // window pushed earlier in this same frame.
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

        // Full: retire the least recently used ONE, not the whole cache. Dumping
        // everything meant every overlay on screen went blocky together.
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

    // The closest handle that is ACTUALLY ready, for the frames before the exact
    // one finishes building. Same font file only - borrowing a different typeface
    // would be a worse lie than a slight size correction.
    //
    // Returns the handle and the size it was really built at, so the caller can
    // correct the difference. Null when nothing of this font is ready yet.
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

    // Build the sizes that are actually configured, before anything draws them.
    // This is what stops the Test button showing a blocky first frame: by the time
    // an overlay appears its handle has been building for a while already.
    //
    // Re-runs only when a font setting actually changes, so it costs one string
    // compare a frame the rest of the time.
    public void WarmIfNeeded(Configuration c)
    {
        var stamp = $"{c.OverlayFontFamily}|{c.OverlayFontBold}|{c.OverlayFontItalic}|"
                    + $"{c.CombatTimerFontFamily}|{c.CombatTimerFontBold}|{c.CombatTimerFontItalic}|"
                    + $"{SnapPx(c.OverlayFontSizePx)}|{SnapPx(c.UpcomingFontSizePx)}|"
                    + $"{SnapPx(c.MitBarFontSizePx)}|{SnapPx(c.PrepCheckFontSizePx)}|"
                    + $"{SnapPx(c.CombatTimerFontSizePx)}";
        if (string.Equals(stamp, _warmStamp, StringComparison.Ordinal)) return;
        _warmStamp = stamp;

        // Every overlay that pushes a font, at the size it's set to right now.
        Get(c.OverlayFontSizePx, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
        Get(c.UpcomingFontSizePx, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
        Get(c.MitBarFontSizePx, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
        Get(c.PrepCheckFontSizePx, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
        Get(c.CombatTimerFontSizePx, c.CombatTimerFontFamily, c.CombatTimerFontBold, c.CombatTimerFontItalic);
        // The call overlay's secondary text is a fixed fraction of its own size
        // (the mechanic line and the countdown), so those are real configured
        // sizes too. Its radial-ring number scales with the ring's diameter and
        // can't be predicted - Nearest covers that one.
        Get(c.OverlayFontSizePx * 0.5f, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
        Get(c.OverlayFontSizePx * 0.55f, c.OverlayFontFamily, c.OverlayFontBold, c.OverlayFontItalic);
    }

    // The scale to draw at when the handle we WANT isn't ready and we're borrowing
    // one built at another size.
    //
    // Exactly 1 when they're close enough that resampling would cost more than it
    // fixes - a correction under 2% is invisible, and leaving the scale alone keeps
    // glyphs on whole pixels.
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
