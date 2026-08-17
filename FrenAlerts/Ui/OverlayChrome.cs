using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using FrenAlerts.Engine;
using FrenAlerts.Engine.Alerts;

namespace FrenAlerts.Ui;

// Chrome shared by the on-screen call windows.
internal static class OverlayChrome
{
    // Pushes a crisp Dalamud font handle at the given px size.
    public static IDisposable PushFont(FontManager fonts, float sizePx)
    {
        // Asking for it is what starts it building.
        var handle = fonts.Get(sizePx);
        if (handle is { Available: true }) return handle.Push();

        // Not ready (first draw, or a size never used before).
        if (fonts.Nearest(sizePx) is { } near)
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

    // The current center when the user has dragged it, else null.
    public static Vector2? MovedCenterFrac(Vector2 saved)
    {
        var vp = ImGui.GetMainViewport();
        var cur = ImGui.GetWindowPos();
        var center = new Vector2(cur.X + ImGui.GetWindowWidth() * 0.5f, cur.Y + ImGui.GetWindowHeight() * 0.5f);
        var frac = (center - vp.WorkPos) / vp.WorkSize;
        return (frac - saved).LengthSquared() > 0.0000001f ? frac : null;
    }

    // What colour a call is drawn in, for every surface that draws one.
    //
    // Both overlays had their own copy of this switch, and neither knew about
    // Colorblind Mode: the setting reached the window's status dots and stopped there,
    // so somebody who turned it on still got amber against red for Alert against
    // Alarm, which is the pair it exists to separate.
    //
    // Shipped colours only, per CallLook.Safely. Anything picked by hand is drawn as
    // picked, switch or no switch.
    private static readonly Configuration Shipped = new();

    public static uint CallColor(Configuration c, CallLevel level)
    {
        var (chosen, shipped, safe) = level switch
        {
            CallLevel.Alarm => (c.ColorAlarm, Shipped.ColorAlarm, CallLook.SafeAlarm),
            CallLevel.Alert => (c.ColorAlert, Shipped.ColorAlert, CallLook.SafeAlert),
            _ => (c.ColorInfo, Shipped.ColorInfo, CallLook.SafeInfo),
        };

        return c.ColorblindMode ? CallLook.Safely(chosen, shipped, safe) : chosen;
    }

    // The same colour carrying a call's fade, for every surface that draws one.
    public static uint Faded(uint abgr, float alpha)
    {
        var v = Theme.V(abgr);
        v.W *= alpha;
        return Widgets.ToColor(v);
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

    // A call's coloured runs, outlined and then inked, for every surface that draws one.
    //
    // Two passes rather than one per run. Written a run at a time, the black ring of the
    // second run goes down on top of the last glyph of the first: the ring reaches
    // OutlineWidth in every direction, three points at a 55px call, so every colour
    // boundary in a call had a dark bite taken out of the letter before it. "Stack on
    // <red>you</red> now" wears it twice. Laying every outline first means nothing is
    // drawn over a letter once the letter is there.
    //
    // Both windows had this loop written out, and they are already held to drawing a call
    // the same way. One of them was going to get fixed and the other was not.
    public static void DrawPieces(ImDrawListPtr dl, ImFontPtr font, float px, Vector2 pen,
        IReadOnlyList<CallPiece> pieces, uint fallback, float alpha, bool outline)
    {
        if (outline)
        {
            var ring = CallLook.OutlineWidth(px);
            var shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, alpha));
            var at = pen;

            foreach (var piece in pieces)
            {
                if (piece.Text.Length == 0) continue;

                foreach (var (x, y) in CallLook.Ring)
                    dl.AddText(font, px, at + new Vector2(x * ring, y * ring), shadow, piece.Text);

                at.X += ImGui.CalcTextSize(piece.Text).X;
            }
        }

        foreach (var piece in pieces)
        {
            if (piece.Text.Length == 0) continue;

            dl.AddText(font, px, pen, piece.Color is { } own
                ? Faded(Widgets.ToColor(own), alpha)
                : Faded(fallback, alpha), piece.Text);

            pen.X += ImGui.CalcTextSize(piece.Text).X;
        }
    }

    public static void Outline(ImDrawListPtr dl, Vector2 pos, string text, float lineHeight)
    {
        var d = Math.Clamp(lineHeight * 0.055f, 1f, 3f);
        for (var oy = -1; oy <= 1; oy++)
            for (var ox = -1; ox <= 1; ox++)
                if (ox != 0 || oy != 0)
                    dl.AddText(pos + new Vector2(ox * d, oy * d), 0xE6000000, text);
    }

    // The layered countdown fill: a solid base, a gradient body, a crisp edge.
    public static void Fill(ImDrawListPtr dl, Vector2 p0, Vector2 p1, float frac, uint color, float round)
    {
        if (frac <= 0.004f) return;
        var rgb = color & 0x00FFFFFF;
        var edgeX = p0.X + (p1.X - p0.X) * frac;
        var corners = frac >= 0.999f ? ImDrawFlags.RoundCornersAll : ImDrawFlags.RoundCornersLeft;
        dl.AddRectFilled(p0, new Vector2(edgeX, p1.Y), rgb | 0x66000000, round, corners);
        dl.AddRectFilledMultiColor(p0, new Vector2(edgeX, p1.Y),
            rgb | 0x14000000, rgb | 0x7A000000, rgb | 0x7A000000, rgb | 0x14000000);
        if (frac > 0.02f && frac < 0.985f)
            dl.AddRectFilled(new Vector2(edgeX - 1.5f, p0.Y + 1f),
                new Vector2(edgeX + 0.5f, p1.Y - 1f), rgb | 0xF0000000);
    }
}
