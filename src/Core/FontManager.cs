using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface.ManagedFontAtlas;

namespace FrenMits;

// Builds crisp font handles for the overlay. Each (family, style, size) is built
// once and cached. "Default" uses Dalamud's font; the named families load the
// matching Windows TTF (bold/italic variants where they exist).
public class FontManager : IDisposable
{
    private readonly Dictionary<string, IFontHandle> _handles = new();

    // Selectable families -> (regular, bold, italic, bold-italic) filenames in the
    // Windows Fonts folder. A missing variant falls back to the regular file.
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
        catch { return null; }
    }

    public IFontHandle? Get(float sizePx, string family, bool bold, bool italic)
    {
        // Snap to a 2px grid so tiny size changes reuse a handle.
        var px = (int)MathF.Round(Math.Clamp(sizePx, 8f, 160f) / 2f) * 2;
        var file = string.IsNullOrEmpty(family) || family == "Default" ? null : ResolveFile(family, bold, italic);
        var key = $"{(file ?? "default")}|{px}";

        if (_handles.TryGetValue(key, out var existing))
            return existing;

        // Cap the cache; building runs before any handle is pushed this frame.
        if (_handles.Count >= 24)
        {
            foreach (var h in _handles.Values) h.Dispose();
            _handles.Clear();
        }

        try
        {
            var handle = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk =>
                {
                    if (file == null)
                        tk.AddDalamudDefaultFont(px);
                    else
                        tk.AddFontFromFile(file, new SafeFontConfig { SizePx = px });
                }));
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
