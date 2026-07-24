using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FrenMits.Windows;

// Chrome shared by the on-screen HUD windows (center call, next-mits board, mit
// bar, combat timer, recap popup): the lock rule, crisp font pushing,
// center-anchored placement, drag-to-place geometry, and the pulse/shadow
// drawing they all use. One implementation instead of a copy per window.
internal static class OverlayChrome
{
    // Locked for real if the user ticked the lock OR a live pull is running (but
    // not while previewing) - combat always pins a HUD window so it can't be
    // grabbed and "stuck" mid-fight.
    public static bool Locked(bool userLock, Configuration c)
        => userLock || (Plugin.InCombat && !c.TestMode);

    // Pushes a crisp Dalamud font handle at the given px size, falling back to
    // SetWindowFontScale if the handle is not ready yet.
    public static IDisposable PushFont(FontManager fonts, float sizePx, string family, bool bold, bool italic)
    {
        var handle = fonts.Get(sizePx, family, bold, italic);
        if (handle is { Available: true }) return handle.Push();
        ImGui.SetWindowFontScale(MathF.Max(0.5f, sizePx / 18f));
        return ResetFontScale.Instance;
    }

    private sealed class ResetFontScale : IDisposable
    {
        public static readonly ResetFontScale Instance = new();
        public void Dispose() => ImGui.SetWindowFontScale(1f);
    }

    // Pin the window's CENTER to the saved work-area fraction: every frame while
    // locked, or for one frame after RequestReposition asked for a snap-back.
    public static void ApplyPosition(Vector2 savedFrac, bool locked, ref bool applyPos)
    {
        var vp = ImGui.GetMainViewport();
        var pos = vp.WorkPos + savedFrac * vp.WorkSize;
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y)); // whole pixels = sharp text
        if (locked) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); applyPos = true; }
        else if (applyPos) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); applyPos = false; }
    }

    // The window's current CENTER as a work-area fraction when it differs from
    // the saved one (i.e. the user dragged it); null while unmoved. The caller
    // stores the new fraction and saves ONCE when the mouse releases.
    public static Vector2? MovedCenterFrac(Vector2 saved)
    {
        var vp = ImGui.GetMainViewport();
        var cur = ImGui.GetWindowPos();
        var center = new Vector2(cur.X + ImGui.GetWindowWidth() * 0.5f, cur.Y + ImGui.GetWindowHeight() * 0.5f);
        var frac = (center - vp.WorkPos) / vp.WorkSize;
        return (frac - saved).LengthSquared() > 0.0000001f ? frac : null;
    }

    // Brightness oscillation for imminent pulses, preserving alpha.
    public static uint Pulse(uint abgr)
    {
        var t = MathF.Sin((float)ImGui.GetTime() * 12f) * 0.5f + 0.5f;
        var factor = 0.55f + 0.45f * t;
        var a = abgr & 0xFF000000;
        var b = (uint)(((abgr >> 16) & 0xFF) * factor) & 0xFF;
        var g = (uint)(((abgr >> 8) & 0xFF) * factor) & 0xFF;
        var r = (uint)((abgr & 0xFF) * factor) & 0xFF;
        return a | (b << 16) | (g << 8) | r;
    }

    // Draw-list text with the overlay's readability shadow.
    public static void BoardText(ImDrawListPtr dl, Vector2 pos, uint color, string text, bool shadow)
    {
        if (shadow) dl.AddText(pos + new Vector2(1.5f, 1.5f), 0xE0000000, text);
        dl.AddText(pos, color, text);
    }
}
