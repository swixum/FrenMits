using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using FrenAlerts.Engine;
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

    private static readonly Dictionary<uint, uint> ActionIcons = new();

    // An action id to its icon, the same way a status resolves to one. Wanted by the
    // cooldown tracker: somebody types an action id and should not have to go and
    // find the icon number that goes with it.
    public static uint ForAction(uint actionId)
    {
        if (actionId == 0) return 0;
        if (ActionIcons.TryGetValue(actionId, out var hit)) return hit;

        UiServices.Ensure();
        uint icon = 0;
        if (UiServices.Ready)
        {
            try
            {
                if (UiServices.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>()
                        ?.GetRowOrDefault(actionId) is { } row)
                    icon = row.Icon;
            }
            catch (Exception ex) { Service.Log.Warning(ex, $"action {actionId} has no icon"); }
        }

        if (ActionIcons.Count >= MaxCached) ActionIcons.Clear();
        return ActionIcons[actionId] = icon;
    }

    // Tinted, because a call fades and its icon has to fade with it: drawn at full
    // opacity it hangs there for the last fraction of a second on its own, which is
    // the sort of thing that reads as a stuck call.
    //
    // High resolution art, as theirs asks for: the icon is drawn a line high and the
    // low resolution sheet is visibly soft at that size.
    public static bool DrawTo(ImDrawListPtr dl, uint iconId, Vector2 p0, Vector2 size,
        uint tint = 0xFFFFFFFF)
    {
        if (iconId == 0) return false;
        UiServices.Ensure();
        if (!UiServices.Ready) return false;
        try
        {
            // Default, not Empty: an empty wrap draws nothing and would report
            // success, leaving a hole where the icon goes while art loads.
            var tex = UiServices.Textures
                .GetFromGameIcon(new GameIconLookup(iconId, false, true)).GetWrapOrDefault();
            if (tex == null) return false;
            dl.AddImage(tex.Handle, p0, p0 + size, Vector2.Zero, Vector2.One, tint);
            return true;
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, $"icon {iconId} would not draw");
            return false;
        }
    }

    // Whether this icon will actually put something on screen.
    //
    // Asked before the space for it is reserved, because the two were decided
    // separately: the layout reserved a gap for any icon that was not None, and Draw
    // quietly did nothing when the art would not resolve, so a call could open a hole
    // to the left of its words and draw nothing in it.
    //
    // The same cached lookups Draw uses, so the two can never disagree.
    public static bool Has(CallIcon icon) => icon.Kind switch
    {
        CallIconKind.Status => ForStatus(icon.Id) != 0,
        CallIconKind.Sheet => icon.Id != 0,
        _ => false,
    };

    public static bool Draw(CallIcon icon, ImDrawListPtr dl, Vector2 p0, float size, uint tint, bool shadow)
    {
        switch (icon.Kind)
        {
            case CallIconKind.Status:
                return DrawTo(dl, ForStatus(icon.Id), p0, new Vector2(size, size), tint);

            // Drawn as it is: the number already names the art, so nothing has to be
            // looked up and nothing stands in for it.
            case CallIconKind.Sheet:
                return DrawTo(dl, icon.Id, p0, new Vector2(size, size), tint);

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

    private static CallIcon _sample = CallIcon.None;
    private static int _sampleFrom = -1;

    // A real debuff icon for the sample call, taken from a call this install actually
    // makes.
    //
    // Not a number written in here. Every small number is a real row in the sheet and
    // draws a real picture of something unrelated, and judging the icon is the whole
    // reason the sample carries one, so a stand-in would defeat it. A build whose calls
    // watch no debuff shows the sample with no icon instead of a wrong one.
    public static CallIcon Sample()
    {
        // The pack lands on a background thread, so a miss is retried until the number
        // of fights stops changing rather than cached from an empty catalog forever.
        var loaded = FightCatalog.All.Count;
        if (_sampleFrom == loaded) return _sample;
        _sampleFrom = loaded;

        foreach (var fight in FightCatalog.All)
            foreach (var call in FightCatalog.CallsIn(fight.TerritoryId))
            {
                if (call.On != EventKind.StatusGain || call.MatchId == 0) continue;
                if (ForStatus(call.MatchId) == 0) continue;
                return _sample = CallIcon.Status(call.MatchId);
            }

        return _sample = CallIcon.None;
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
        ActionIcons.Clear();
        _sample = CallIcon.None;
        _sampleFrom = -1;
        _logo = null;
        _logoLookedUp = false;
    }
}
