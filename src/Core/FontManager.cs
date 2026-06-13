using System;
using System.Collections.Generic;
using Dalamud.Interface.ManagedFontAtlas;

namespace FrenMits;

// Builds crisp Dalamud font handles at specific pixel sizes. Each size is built
// once and cached, so the overlay's call / mechanic / upcoming text are all
// sharp instead of being stretched from one base font (which looks blurry).
public class FontManager : IDisposable
{
    private readonly Dictionary<int, IFontHandle> _handles = new();

    public IFontHandle? Get(float sizePx)
    {
        // Snap to a 2px grid so tiny size changes (and the 0.55x mechanic line)
        // reuse a handle instead of building a new atlas for every fraction.
        var key = (int)MathF.Round(Math.Clamp(sizePx, 8f, 160f) / 2f) * 2;
        if (_handles.TryGetValue(key, out var existing))
            return existing;

        // Guard against runaway growth (e.g. dragging the size slider): once we
        // have a lot of cached sizes, drop them and rebuild what's in use. Safe
        // here because Get() runs before any handle is pushed for this frame.
        if (_handles.Count >= 16)
        {
            foreach (var h in _handles.Values) h.Dispose();
            _handles.Clear();
        }

        try
        {
            var handle = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(key)));
            _handles[key] = handle;
            return handle;
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: failed to build font handle");
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var h in _handles.Values) h.Dispose();
        _handles.Clear();
    }
}
