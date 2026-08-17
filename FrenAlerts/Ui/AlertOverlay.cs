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
    private bool _pushedPad;

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

        // Room for the slab, which is drawn past the text rather than laid out.
        //
        // The window auto-resizes to its items, and a call's item is the text box
        // only: the slab behind it reaches PadX and PadY further out on every side,
        // and a window draw list is clipped to its window. At a 55px call that is
        // 27px sideways against ImGui's default 8, so the rounded corners and the
        // border were being cut off.
        //
        // Off the configured size rather than the drawn one, because the fit only
        // ever shrinks a call and this has to be the roomier of the two.
        _pushedPad = C.ShowBackground;
        if (_pushedPad)
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(
                C.CallFontSizePx * CallLook.PadX, C.CallFontSizePx * CallLook.PadY));

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
        if (_pushedPad) ImGui.PopStyleVar();
        if (_pushedBg) ImGui.PopStyleColor();
    }

    // Counted as the stack rather than as the board: a call that named its own place
    // is drawn by the window that places them, and opening this one for it puts an
    // empty background box on screen for as long as that call lasts.
    public override bool DrawConditions()
        => OverlayState.Visible(C.AlertsEnabled, C.TestMode, _board.Stacked().Count,
                                UiServices.GameUiHidden);

    public override void Draw()
    {
        SavePositionIfDragged();

        // Right-click menu, only while the overlay takes the mouse.
        if (ImGui.BeginPopupContextWindow("##facallctx"))
        {
            if (ImGui.MenuItem("Lock position", "", C.OverlayLocked))
            {
                C.OverlayLocked = !C.OverlayLocked;
                C.Save();
            }
            if (ImGui.MenuItem("Center"))
            {
                C.OverlayPosition = new Vector2(0.5f, 0.35f);
                C.Save();
                RequestReposition();
            }
            if (ImGui.MenuItem("Open settings")) OpenSettings?.Invoke();
            ImGui.EndPopup();
        }

        var now = _board.Now;
        // Anything that named its own place is drawn by the window that places them,
        // and must not take a slot here as well: it would be on screen twice, and
        // the second copy would push a fight's call down.
        var live = _board.Stacked();
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
                if (i > 0) Gap(px);
                var s = live[i];
                DrawCall(s.Call.Text, s.Call.Level, s.Icon, s.Remaining(now), s.Counting(now),
                    (float)(now - s.FireAt), px, s.Call.Tint,
                    age: (float)(now - s.At), holds: (float)(s.EndsAt - now));
            }
        }

        if (Placing) DrawPlacementFrame();
    }

    // The space between one call in the stack and the next, in one place so the
    // preview and the game cannot drift apart.
    //
    // This was ImGui.Spacing(), which is four points, and it left the stack running
    // together: the slab behind a call is drawn PadY past the item box top and
    // bottom, so at a 55px call two of them overlap by thirty points before any gap
    // is counted. The engine carries their own gap as CallLook.StackGap and nothing
    // was reading it.
    //
    // Off the settled stack size rather than each call's own, or the gap would
    // breathe while a call pops in and push the one below it around.
    private void Gap(float px)
    {
        var gap = CallLook.StackGap * (px / CallLook.BasePx)
                  + (C.ShowBackground ? 2f * px * CallLook.PadY : 0f);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + gap);
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

    // The call you drag and size, on the game screen where it will actually appear.
    //
    // It carries an icon whenever icons are on, because this is the only place the
    // icon can be judged before a pull: Icon Size is a slider whose effect was
    // invisible until a real mechanic landed, and then it is too late to adjust it.
    // Sized as a stack of one, so it lands at exactly the size a lone call does.
    private void DrawSample()
    {
        var (remaining, counting, sinceGo) = SampleClock();
        var icon = C.ShowCallIcon ? CallIcon.Marker(0) : CallIcon.None;
        var px = OverlayState.FitFontPxFor(C.CallFontSizePx,
            ImGui.GetMainViewport().WorkSize.X * 0.92f,
            [Need("Raidwide", icon, remaining, counting)]);
        DrawCall("Raidwide", CallLevel.Alert, icon, remaining, counting, sinceGo, px);
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
        Gap(px);
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

    // Five seconds counting down, one at go, then round again, with SinceGo riding
    // along so a preview pulses exactly when a real call does.
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

    // One call, drawn the way the plugin the fights came from drew them.
    //
    // Their geometry throughout, in CallLook: the size it lands at and grows from, the
    // ring of sixteen offsets that makes the outline a ring rather than four corners,
    // the rounded slab behind it with its shadow and its own colour on the edge, the
    // icon at 95% of the line with a quarter of a line beside it, and the bar
    // underneath while it counts.
    //
    // The seconds stay in brackets after the words, and there is no bar: theirs runs
    // one under the call with the number beside it, and swix wants the brackets and
    // nothing else moving.
    private void DrawCall(string text, CallLevel level, CallIcon icon, float remaining, bool counting,
        float sinceGo = 0f, float? sharedPx = null, uint tint = 0, float age = 99f,
        float holds = 99f)
    {
        const float pulseWindow = 1.5f;

        // Faded on what is left of its time on screen, not on what is left of the
        // countdown: a counted call is at its most useful as it reaches zero.
        var alpha = CallLook.AlphaAt(age, holds);
        if (!CallLook.WorthDrawing(alpha)) return;

        var baseColor = tint != 0 ? tint : ColorFor(level);
        var pulsing = C.PulseWhenClose
            && (counting ? remaining < pulseWindow : sinceGo < pulseWindow);
        var color = pulsing ? OverlayChrome.Pulse(baseColor) : baseColor;

        var words = CallText.Sentence(text);
        var (line, reserve) = OverlayState.Countdown(words, C.ShowCountdown, counting, remaining);

        var pieces = CallText.Pieces(line);
        var plain = CallText.Plain(line);

        var wanted = sharedPx ?? OverlayState.FitFontPxFor(C.CallFontSizePx,
            ImGui.GetMainViewport().WorkSize.X * 0.92f,
            [ImGui.CalcTextSize(CallText.Plain(reserve)).X / MathF.Max(1f, ImGui.GetFontSize())
             + IconFactor(icon)]);

        // It arrives at 85% and grows into place over a fifth of a second.
        var px = wanted * CallLook.ScaleAt(age);

        using (OverlayChrome.PushFont(_fonts, px))
        {
            var dl = ImGui.GetWindowDrawList();
            var font = ImGui.GetFont();
            var drawn = ImGui.GetFontSize();

            var withIcon = C.ShowCallIcon && icon.Any;
            var iconPx = withIcon ? drawn * CallLook.IconSize * Math.Clamp(C.CallIconScale, 0.4f, 1.6f) : 0f;
            var lead = withIcon ? iconPx + drawn * CallLook.IconGap : 0f;

            var size = ImGui.CalcTextSize(plain);
            var holdW = MathF.Max(size.X, ImGui.CalcTextSize(CallText.Plain(reserve)).X);

            var offset = AlignOffset(ImGui.GetContentRegionAvail().X, lead + holdW);
            if (offset > 0) ImGui.SetCursorPosX(MathF.Round(ImGui.GetCursorPosX() + offset));

            var at = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(lead + holdW, size.Y));

            var textAt = new Vector2(at.X + lead, at.Y);

            if (C.ShowBackground)
            {
                var pad = new Vector2(drawn * CallLook.PadX, drawn * CallLook.PadY);
                var p0 = textAt - pad - new Vector2(lead, 0f);
                var p1 = textAt + new Vector2(size.X, size.Y) + pad;
                var round = drawn * CallLook.Round;

                var drop = new Vector2(0f, CallLook.ShadowDrop);
                dl.AddRectFilled(p0 + drop, p1 + drop,
                    ImGui.ColorConvertFloat4ToU32(CallLook.ShadowColor(alpha)), round);
                dl.AddRectFilledMultiColor(p0, p1,
                    ImGui.ColorConvertFloat4ToU32(CallLook.BackTop(alpha)),
                    ImGui.ColorConvertFloat4ToU32(CallLook.BackTop(alpha)),
                    ImGui.ColorConvertFloat4ToU32(CallLook.BackBottom(alpha)),
                    ImGui.ColorConvertFloat4ToU32(CallLook.BackBottom(alpha)));

                var edge = Theme.V(color);
                edge.W = CallLook.BorderAlpha * alpha;
                dl.AddRect(p0, p1, ImGui.ColorConvertFloat4ToU32(edge), round, ImDrawFlags.None,
                    CallLook.BorderWidth);
            }

            if (withIcon)
                Icons.Draw(icon, dl, new Vector2(at.X, at.Y + (size.Y - iconPx) * 0.5f),
                    iconPx, Faded(0xFFFFFFFF, alpha), false);

            var ring = CallLook.OutlineWidth(drawn);
            var shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, alpha));
            var pen = textAt;

            foreach (var piece in pieces)
            {
                if (piece.Text.Length == 0) continue;

                var ink = piece.Color is { } own
                    ? Faded(Widgets.ToColor(own), alpha)
                    : Faded(color, alpha);

                if (C.TextOutline)
                    foreach (var (x, y) in CallLook.Ring)
                        dl.AddText(font, drawn, pen + new Vector2(x * ring, y * ring), shadow, piece.Text);

                dl.AddText(font, drawn, pen, ink, piece.Text);
                pen.X += ImGui.CalcTextSize(piece.Text).X;
            }

        }
    }

    // The same colour, carrying the fade.
    private static uint Faded(uint abgr, float alpha)
    {
        var v = Theme.V(abgr);
        v.W *= alpha;
        return Widgets.ToColor(v);
    }

    // Horizontal offset for the configured alignment.
    private float AlignOffset(float avail, float contentWidth) => C.CallTextAlign switch
    {
        0 => 0f,
        2 => MathF.Max(0f, avail - contentWidth),
        _ => MathF.Max(0f, (avail - contentWidth) * 0.5f),
    };

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
