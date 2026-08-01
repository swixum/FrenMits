using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FrenMits.Windows;

// Chrome shared by the on-screen HUD windows.
internal static class OverlayChrome
{
    // Locked if the user ticked the lock or a live pull is running.
    public static bool Locked(bool userLock, Configuration c)
        => userLock || (Plugin.InCombat && !c.TestMode);

    // Pushes a crisp Dalamud font handle at the given px size.
    public static IDisposable PushFont(FontManager fonts, float sizePx, string family, bool bold, bool italic)
    {
        // Asking for it is what starts it building.
        var handle = fonts.Get(sizePx, family, bold, italic);
        if (handle is { Available: true }) return handle.Push();

        // Not ready (first draw, or a size never used before).
        if (fonts.Nearest(sizePx, family, bold, italic) is { } near)
        {
            var push = near.Handle.Push();
            var scale = FontManager.Correction(FontManager.SnapPx(sizePx), near.Px);
            if (scale == 1f) return push;
            ImGui.SetWindowFontScale(scale);
            return new PopScaledFont(push);
        }

        // Nothing is built yet, on the very first overlay frame.
        ImGui.SetWindowFontScale(MathF.Max(0.5f, sizePx / 18f));
        return ResetFontScale.Instance;
    }

    private sealed class ResetFontScale : IDisposable
    {
        public static readonly ResetFontScale Instance = new();
        public void Dispose() => ImGui.SetWindowFontScale(1f);
    }

    // Clears the correction AND pops the borrowed handle.
    private sealed class PopScaledFont : IDisposable
    {
        private readonly IDisposable _push;
        public PopScaledFont(IDisposable push) => _push = push;

        public void Dispose()
        {
            ImGui.SetWindowFontScale(1f);
            _push.Dispose();
        }
    }

    // Pin the window's center to the saved work-area fraction.
    public static void ApplyPosition(Vector2 savedFrac, bool locked, ref bool applyPos)
    {
        var vp = ImGui.GetMainViewport();
        var pos = vp.WorkPos + savedFrac * vp.WorkSize;
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y)); // whole pixels = sharp text
        if (locked) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); applyPos = true; }
        else if (applyPos) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); applyPos = false; }
    }

    // The current center when the user has dragged it, else null.
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
