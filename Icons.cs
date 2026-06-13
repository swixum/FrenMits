using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;

namespace FrenMits;

// Resolves action names to game icon ids (built once from the Action sheet) and
// draws those icons. Lines may pin an explicit icon; otherwise the icon is
// inferred from the action text.
public static class Icons
{
    private static Dictionary<string, uint>? _exact;
    private static List<(string Name, uint Icon)>? _byLength;
    private static readonly Dictionary<string, uint> _textCache = new(StringComparer.OrdinalIgnoreCase);

    private static void EnsureBuilt()
    {
        if (_exact != null) return;
        _exact = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var list = new List<(string, uint)>();
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet != null)
            {
                foreach (var row in sheet)
                {
                    if (!row.IsPlayerAction) continue;
                    var icon = (uint)row.Icon;
                    if (icon == 0) continue;
                    var name = row.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    _exact.TryAdd(name, icon);
                    list.Add((name, icon));
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: action index build failed");
        }

        list.Sort((a, b) => b.Item1.Length - a.Item1.Length);
        _byLength = list;
    }

    // The icon a line should display: its pinned icon, else inferred from text.
    public static uint For(MitLine line)
        => line.IconId != 0 ? line.IconId : ResolveFromText(line.Action);

    public static uint ResolveFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        if (_textCache.TryGetValue(text, out var cached)) return cached;
        EnsureBuilt();

        uint icon = 0;
        if (_exact!.TryGetValue(text.Trim(), out var exact))
        {
            icon = exact;
        }
        else
        {
            foreach (var (name, ic) in _byLength!)
                if (name.Length >= 4 && text.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    icon = ic;
                    break;
                }
        }

        _textCache[text] = icon;
        return icon;
    }

    public static IEnumerable<(string Name, uint Icon)> Search(string query, int max)
    {
        EnsureBuilt();
        if (string.IsNullOrWhiteSpace(query)) yield break;
        var n = 0;
        foreach (var (name, ic) in _byLength!)
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                yield return (name, ic);
                if (++n >= max) yield break;
            }
    }

    public static void Draw(uint iconId, Vector2 size)
    {
        if (iconId == 0) { ImGui.Dummy(size); return; }
        try
        {
            var tex = Service.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            ImGui.Image(tex.ImGuiHandle, size);
        }
        catch
        {
            ImGui.Dummy(size);
        }
    }
}
