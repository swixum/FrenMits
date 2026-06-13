using System;
using Dalamud.Interface.ManagedFontAtlas;

namespace FrenMits;

// Builds crisp Dalamud font handles at arbitrary pixel sizes (SetWindowFontScale
// just stretches the base font and looks soft when large). Rebuilds lazily when
// a requested size changes.
public class FontManager : IDisposable
{
    private IFontHandle? _handle;
    private float _builtSize = -1f;

    public IFontHandle? Get(float sizePx)
    {
        sizePx = Math.Clamp(sizePx, 8f, 200f);
        if (_handle == null || Math.Abs(_builtSize - sizePx) > 0.5f)
        {
            _handle?.Dispose();
            _builtSize = sizePx;
            try
            {
                _handle = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                    e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(sizePx)));
            }
            catch (Exception ex)
            {
                Service.Log.Warning(ex, "FrenMits: failed to build font handle");
                _handle = null;
            }
        }
        return _handle;
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
    }
}
