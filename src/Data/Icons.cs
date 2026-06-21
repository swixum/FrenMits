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

    // The icon a line should display: its pinned icon, else the potion icon for a
    // potion line, else inferred from the action text.
    public static uint For(MitLine line)
    {
        if (line.IconId != 0) return line.IconId;
        if (IsPotion(line)) return PotionIcon(PotionStat(line));
        return ResolveFromText(line.Action);
    }

    // A potion line (from the Potions section): action "Potion", or a "Potion (…)"
    // mechanic. These have no player-action icon, so they get the item icon instead.
    public static bool IsPotion(MitLine line)
        => line.Action.Trim().Equals("Potion", StringComparison.OrdinalIgnoreCase)
           || line.Mechanic.StartsWith("Potion", StringComparison.OrdinalIgnoreCase);

    // The stat baked into a potion line's mechanic, e.g. "Potion (Strength)" -> Strength.
    private static string PotionStat(MitLine line)
    {
        var m = line.Mechanic;
        int i = m.IndexOf('('), j = m.IndexOf(')');
        return i >= 0 && j > i ? m.Substring(i + 1, j - i - 1).Trim() : "";
    }

    // The stat-coloured Gemdraught icon for a line (Strength/Dexterity/Intelligence/
    // Mind). Public so the icon picker can pin the right one.
    public static uint PotionIconFor(MitLine line) => PotionIcon(PotionStat(line));

    // Icon for a stat's Gemdraught, resolved from the Item sheet and cached per stat.
    // Falls back to any Gemdraught, then 0 (e.g. a non-English client) — caller draws none.
    private static readonly Dictionary<string, uint> _potionIconByStat = new(StringComparer.OrdinalIgnoreCase);
    public static uint PotionIcon(string? stat = null)
    {
        stat = (stat ?? "").Trim();
        var key = stat.Length == 0 ? "*" : stat;
        if (_potionIconByStat.TryGetValue(key, out var cached)) return cached;

        uint icon = 0, anyGem = 0;
        try
        {
            var items = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
            if (items != null)
                foreach (var row in items)
                {
                    var name = row.Name.ExtractText();
                    if (!name.Contains("Gemdraught", StringComparison.OrdinalIgnoreCase)) continue;
                    if (anyGem == 0) anyGem = row.Icon;
                    if (stat.Length > 0 && name.Contains(stat, StringComparison.OrdinalIgnoreCase))
                    {
                        icon = row.Icon;
                        break;
                    }
                }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: potion icon lookup failed");
        }

        if (icon == 0) icon = anyGem; // unknown/blank stat -> any Gemdraught
        _potionIconByStat[key] = icon;
        return icon;
    }

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
            ImGui.Image(tex.Handle, size);
        }
        catch
        {
            ImGui.Dummy(size);
        }
    }

    // A clickable icon (image button). Returns true when clicked. Falls back to an
    // empty same-size button for ids with no icon. PushID keeps each unique even
    // when the texture (and thus ImGui's derived id) repeats.
    public static bool Button(uint iconId, Vector2 size, string id)
    {
        ImGui.PushID(id);
        try
        {
            var tex = Service.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            return ImGui.ImageButton(tex.Handle, size);
        }
        catch
        {
            return ImGui.Button("##empty", size + new Vector2(8, 8));
        }
        finally
        {
            ImGui.PopID();
        }
    }
}
