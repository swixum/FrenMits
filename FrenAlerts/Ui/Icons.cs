using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using FrenAlerts.Engine.Alerts;

namespace FrenAlerts.Ui;

internal static class Icons
{
    private const int MaxCached = 256;

    private static readonly Dictionary<uint, uint> StatusIcons = new();

    // A status id to its icon in the game's sheet; 0 when it cannot be resolved,
    // which is remembered too so a missing row is not looked up every frame.
    public static uint ForStatus(uint statusId)
    {
        if (statusId == 0) return 0;
        if (StatusIcons.TryGetValue(statusId, out var hit)) return hit;

        UiServices.Ensure();
        uint icon = 0;
        if (UiServices.Ready)
        {
            try
            {
                if (UiServices.Data.GetExcelSheet<Lumina.Excel.Sheets.Status>()
                        ?.GetRowOrDefault(statusId) is { } row)
                    icon = row.Icon;
            }
            catch (Exception ex) { Service.Log.Warning(ex, $"status {statusId} has no icon"); }
        }

        if (StatusIcons.Count >= MaxCached) StatusIcons.Clear();
        return StatusIcons[statusId] = icon;
    }

    public static bool DrawTo(ImDrawListPtr dl, uint iconId, Vector2 p0, Vector2 size)
    {
        if (iconId == 0) return false;
        UiServices.Ensure();
        if (!UiServices.Ready) return false;
        try
        {
            // Default, not Empty: an empty wrap draws nothing and would report
            // success, leaving a hole where the icon goes while art loads.
            var tex = UiServices.Textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
            if (tex == null) return false;
            dl.AddImage(tex.Handle, p0, p0 + size);
            return true;
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, $"icon {iconId} would not draw");
            return false;
        }
    }

    public static bool Draw(CallIcon icon, ImDrawListPtr dl, Vector2 p0, float size, uint tint, bool shadow)
    {
        switch (icon.Kind)
        {
            case CallIconKind.Status:
                return DrawTo(dl, ForStatus(icon.Id), p0, new Vector2(size, size));

            case CallIconKind.Marker:
                DrawGlyph(dl, FontAwesomeIcon.Crosshairs, p0, size, tint, shadow);
                return true;

            default:
                return false;
        }
    }

    // A font glyph scaled to a box, drawn to the list so it cannot push layout.
    public static void DrawGlyph(ImDrawListPtr dl, FontAwesomeIcon icon, Vector2 p0, float size,
        uint color, bool shadow)
    {
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var glyph = icon.ToIconString();
            var sz = ImGui.CalcTextSize(glyph);
            if (sz.Y <= 0.01f) return;
            var k = size / sz.Y;
            var font = ImGui.GetFont();
            var px = ImGui.GetFontSize() * k;
            var at = p0 + new Vector2((size - sz.X * k) * 0.5f, 0f);
            if (shadow) dl.AddText(font, px, at + new Vector2(1.5f, 1.5f), 0xE0000000, glyph);
            dl.AddText(font, px, at, color, glyph);
        }
    }

    private static Dalamud.Interface.Textures.ISharedImmediateTexture? _logo;
    private static bool _logoLookedUp;

    public static Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? Logo()
    {
        if (!_logoLookedUp)
        {
            _logoLookedUp = true;
            UiServices.Ensure();
            try
            {
                var dir = Service.PluginInterface.AssemblyLocation.Directory?.FullName;
                var path = dir == null ? null : System.IO.Path.Combine(dir, "icon.png");
                if (path != null && System.IO.File.Exists(path) && UiServices.Ready)
                    _logo = UiServices.Textures.GetFromFile(path);
            }
            catch (Exception ex) { Service.Log.Warning(ex, "the plugin icon would not load"); }
        }
        return _logo?.GetWrapOrDefault();
    }

    public static void Forget()
    {
        StatusIcons.Clear();
        _logo = null;
        _logoLookedUp = false;
    }
}
