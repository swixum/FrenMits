using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FrenAlerts.Engine;
using FrenAlerts.Engine.Alerts;

namespace FrenAlerts.Ui;

public class AlertOverlay : Window
{
    private readonly Configuration _config;
    private readonly FontManager _fonts;
    private readonly AlertBoard _board;

    private Configuration C => _config;

    private bool _applyPos = true;

    private bool _pushedBg;

    public AlertOverlay(Configuration config, FontManager fonts, AlertBoard board)
        : base("Fren Alerts##facall")
    {
        _config = config;
        _fonts = fonts;
        _board = board;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
        IsOpen = true;
    }

    // Snap back to the saved position next frame.
    public void RequestReposition() => _applyPos = true;

    // The config window's "Open Settings", handed in rather than reached for,
    // so the overlay knows nothing about the rest of the plugin.
    public Action? OpenSettings;

    private bool Placing => C.TestMode;

    // A pull locks it too, whatever the setting says: a call you can drag is a
    // call that eats a click you meant for the boss.
    private bool EffectiveLocked =>
        OverlayState.Locked(C.OverlayLocked, UiServices.InCombat, C.TestMode);

    public override void PreDraw()
    {
        // No title bar ever, or the content jumps when you lock it.
        Flags = ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.AlwaysAutoResize;

        if (!C.ShowBackground)
            Flags |= ImGuiWindowFlags.NoBackground;

        if (EffectiveLocked)
            Flags |= ImGuiWindowFlags.NoResize
                     | ImGuiWindowFlags.NoMove
                     | ImGuiWindowFlags.NoMouseInputs;

        _pushedBg = C.ShowBackground;
        if (_pushedBg)
            ImGui.PushStyleColor(ImGuiCol.WindowBg, C.BackgroundColor);

        var pos = SavedScreenPos();
        if (EffectiveLocked || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            _applyPos = true;   // re-apply the moment a drag ends, or on a reset
        }
        else if (_applyPos)
        {
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            _applyPos = false;
        }
    }

    private static Vector2 Round(Vector2 v) => new(MathF.Round(v.X), MathF.Round(v.Y));

    // Whole pixels, or the text is drawn on a half pixel and blurs.
    private Vector2 SavedScreenPos()
    {
        var vp = ImGui.GetMainViewport();
        return Round(vp.WorkPos + C.OverlayPosition * vp.WorkSize);
    }

    public override void PostDraw()
    {
        if (_pushedBg) ImGui.PopStyleColor();
    }

    public override bool DrawConditions()
        => OverlayState.Visible(C.AlertsEnabled, C.TestMode, _board.Live().Count);

    public override void Draw()
    {
        SavePositionIfDragged();

        // Right-click menu, only while the overlay takes the mouse.
        if (ImGui.BeginPopupContextWindow("##facallctx"))
        {
            if (ImGui.MenuItem("Lock Position", "", C.OverlayLocked))
            {
                C.OverlayLocked = !C.OverlayLocked;
                C.Save();
            }
            if (ImGui.MenuItem("Center It"))
            {
                C.OverlayPosition = new Vector2(0.5f, 0.35f);
                C.Save();
                RequestReposition();
            }
            if (ImGui.MenuItem("Open Settings")) OpenSettings?.Invoke();
            ImGui.EndPopup();
        }

        var now = _board.Now;
        var live = _board.Live();
        if (live.Count == 0)
        {
            if (Placing) DrawSample();
        }
        else
        {
            // Measured before anything is drawn, so every call in the stack lands at
            // the same size rather than each shrinking to its own longest word.
            var needs = new float[live.Count];
            for (var i = 0; i < live.Count; i++)
                needs[i] = Need(live[i].Call.Text, live[i].Icon,
                    live[i].Remaining(now), live[i].Counting(now));

            var px = OverlayState.FitFontPxFor(C.CallFontSizePx,
                ImGui.GetMainViewport().WorkSize.X * 0.92f, needs);

            for (var i = 0; i < live.Count; i++)
            {
                if (i > 0) ImGui.Spacing();
                var s = live[i];
                DrawCall(s.Call.Text, s.Call.Level, s.Icon, s.Remaining(now), s.Counting(now),
                    (float)(now - s.FireAt), px);
            }
        }

        if (Placing) DrawPlacementFrame();
    }

    private void DrawPlacementFrame()
    {
        var dl = ImGui.GetForegroundDrawList();
        const float pad = 6f;
        var p0 = ImGui.GetWindowPos() - new Vector2(pad, pad);
        var p1 = ImGui.GetWindowPos() + ImGui.GetWindowSize() + new Vector2(pad, pad);
        var hot = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        Dashed(dl, p0, p1, hot ? Theme.Accent : (Theme.Accent & 0x00FFFFFFu) | 0xB0000000u);

        var hint = C.OverlayLocked
            ? "drag to place  ·  locked again once test is off"
            : "drag to place  ·  right-click to lock";
        var w = ImGui.CalcTextSize(hint).X;
        var at = new Vector2((p0.X + p1.X - w) * 0.5f, p1.Y + 4f);
        dl.AddText(at + new Vector2(1f, 1f), 0xE0000000, hint);
        dl.AddText(at, Theme.Muted, hint);
    }

    // A dashed rectangle, drawn as segments: ImGui has no dashed stroke, and a
    // solid one reads as part of the call rather than as furniture.
    private static void Dashed(ImDrawListPtr dl, Vector2 p0, Vector2 p1, uint color)
    {
        const float dash = 7f, gap = 5f, thick = 1.6f;
        for (var x = p0.X; x < p1.X; x += dash + gap)
        {
            var xe = MathF.Min(x + dash, p1.X);
            dl.AddLine(new Vector2(x, p0.Y), new Vector2(xe, p0.Y), color, thick);
            dl.AddLine(new Vector2(x, p1.Y), new Vector2(xe, p1.Y), color, thick);
        }
        for (var y = p0.Y; y < p1.Y; y += dash + gap)
        {
            var ye = MathF.Min(y + dash, p1.Y);
            dl.AddLine(new Vector2(p0.X, y), new Vector2(p0.X, ye), color, thick);
            dl.AddLine(new Vector2(p1.X, y), new Vector2(p1.X, ye), color, thick);
        }
    }

    private void DrawSample()
    {
        var (remaining, counting, sinceGo) = SampleClock();
        DrawCall("Raidwide", CallLevel.Alert, CallIcon.None, remaining, counting, sinceGo);
    }

    public void DrawPreview()
    {
        var (remaining, counting, sinceGo) = SampleClock();
        var second = MathF.Max(0f, remaining - 1f);

        // Sized as a pair, the same way the real stack is, or the preview shows two
        // sizes and the game shows one.
        var px = OverlayState.FitFontPxFor(C.CallFontSizePx,
            ImGui.GetMainViewport().WorkSize.X * 0.92f,
            [Need("Raidwide", CallIcon.None, remaining, counting),
             Need("Stack on you", CallIcon.Marker(0), second, counting)]);

        DrawCall("Raidwide", CallLevel.Alert, CallIcon.None, remaining, counting, sinceGo, px);
        ImGui.Spacing();
        // A second call a beat ahead of the first, so the preview shows two at
        // once the way a real burst does, each pulsing at its own moment.
        DrawCall("Stack on you", CallLevel.Alarm, CallIcon.Marker(0),
            second, counting, sinceGo + 1f, px);
    }

    public void DrawOne(string text, CallLevel level)
    {
        var (remaining, counting, sinceGo) = SampleClock();
        DrawCall(text, level, CallIcon.None, remaining, counting, sinceGo);
    }

    // Five seconds counting down, one at go, then round again.
    // Five seconds counting down, one at go, then round again. SinceGo rides along
    // so a preview pulses exactly when a real call does.
    private static (float Remaining, bool Counting, float SinceGo) SampleClock()
    {
        var t = (float)(ImGui.GetTime() % 6d);
        var counting = t < 5f;
        return (counting ? 5f - t : 0f, counting, counting ? 0f : t - 5f);
    }

    private uint ColorFor(CallLevel level) => level switch
    {
        CallLevel.Alarm => C.ColorAlarm,
        CallLevel.Alert => C.ColorAlert,
        _ => C.ColorInfo,
    };

    // How much width one call wants, per pixel of font size, icon included. The one
    // place that measurement is written, so the stack's shared size and the size a
    // lone call picks for itself can never drift apart.
    private float Need(string text, CallIcon icon, float remaining, bool counting)
    {
        var (_, reserve) = OverlayState.Countdown(
            CallText.Sentence(text), C.ShowCountdown, counting, remaining);
        var perPx = ImGui.CalcTextSize(reserve).X / MathF.Max(1f, ImGui.GetFontSize());
        return perPx + IconFactor(icon);
    }

    private float IconFactor(CallIcon icon) =>
        C.ShowCallIcon && icon.Any ? Math.Clamp(C.CallIconScale, 0.4f, 1.6f) + 0.32f : 0f;

    private void DrawCall(string text, CallLevel level, CallIcon icon, float remaining, bool counting,
        float sinceGo = 0f, float? sharedPx = null)
    {
        // Centred on go rather than stopping at it: the flash is worth most at the
        // moment you act, and the switch is called Pulse on Go.
        const float pulseWindow = 1.5f;
        var baseColor = ColorFor(level);
        var pulsing = C.PulseWhenClose
            && (counting ? remaining < pulseWindow : sinceGo < pulseWindow);
        var color = pulsing ? OverlayChrome.Pulse(baseColor) : baseColor;

        var words = CallText.Sentence(text);
        var (line, reserve) = OverlayState.Countdown(words, C.ShowCountdown, counting, remaining);

        // Measured at the window's own font, then scaled: text width is close
        // enough to linear in size for the same face, and this is what lets the
        // size be chosen before the font is pushed rather than after.
        //
        // Against the reserved form, so the fitted size does not step up on the
        // frame the countdown drops off either.
        // The stack's own size when there is one, so four calls read as four calls
        // rather than as four sizes. A lone call still fits itself.
        var px = sharedPx ?? OverlayState.FitFontPxFor(C.CallFontSizePx,
            ImGui.GetMainViewport().WorkSize.X * 0.92f,
            [ImGui.CalcTextSize(reserve).X / MathF.Max(1f, ImGui.GetFontSize()) + IconFactor(icon)]);

        using (OverlayChrome.PushFont(_fonts, px))
        {
            var withIcon = C.ShowCallIcon && icon.Any;
            var lineH = ImGui.GetTextLineHeight();
            var iconH = withIcon ? MathF.Round(lineH * Math.Clamp(C.CallIconScale, 0.4f, 1.6f)) : 0f;
            var gap = withIcon ? MathF.Round(lineH * 0.32f) : 0f;
            var textW = ImGui.CalcTextSize(line).X;
            var holdW = MathF.Max(textW, ImGui.CalcTextSize(reserve).X);

            var offset = AlignOffset(ImGui.GetContentRegionAvail().X, iconH + gap + holdW);
            if (offset > 0) ImGui.SetCursorPosX(MathF.Round(ImGui.GetCursorPosX() + offset));

            var start = ImGui.GetCursorPos();
            if (withIcon)
            {
                var at = ImGui.GetCursorScreenPos();
                var dl = ImGui.GetWindowDrawList();
                // Centered against the words, and drawn to the list so it can
                // never push the text it sits beside.
                // Art that has not resolved leaves its space empty rather than
                // standing something else in: on the frame a debuff lands its
                // texture is often still loading, and a warning glyph blinking in
                // and out at the moment you are reading the call is worse than a
                // gap. The space is already reserved, so the words never move.
                Icons.Draw(icon, dl, new Vector2(at.X, at.Y + (lineH - iconH) * 0.5f),
                    iconH, color, C.TextShadow);
            }

            ImGui.SetCursorPos(new Vector2(start.X + iconH + gap, start.Y));
            Text(line, color);

            // The room the countdown gave up, held open: the window sizes to what it
            // holds, so letting it shrink would drag the words along with the edge.
            if (holdW > textW)
            {
                ImGui.SameLine(0f, 0f);
                ImGui.Dummy(new Vector2(holdW - textW, 1f));
            }
        }
    }

    // Horizontal offset for the configured alignment.
    private float AlignOffset(float avail, float contentWidth) => C.CallTextAlign switch
    {
        0 => 0f,
        2 => MathF.Max(0f, avail - contentWidth),
        _ => MathF.Max(0f, (avail - contentWidth) * 0.5f),
    };

    private void Text(string text, uint color)
    {
        var p = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var size = ImGui.CalcTextSize(text);
        if (C.TextOutline) OverlayChrome.Outline(dl, p, text, size.Y);
        else if (C.TextShadow) dl.AddText(p + new Vector2(1.5f, 1.5f), 0xE0000000, text);

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private void SavePositionIfDragged()
    {
        if (EffectiveLocked) return;
        // Only capture a real drag, or a stray hold saves drift.
        if (!ImGui.IsMouseDragging(ImGuiMouseButton.Left) || !ImGui.IsWindowFocused()) return;
        if (OverlayChrome.MovedCenterFrac(C.OverlayPosition) is not { } frac) return;
        // Kept on screen, or a call dragged off an edge is gone for good with no
        // way to grab it back.
        C.OverlayPosition = new Vector2(Math.Clamp(frac.X, 0.02f, 0.98f),
                                        Math.Clamp(frac.Y, 0.02f, 0.98f));
        C.Save();
    }
}
