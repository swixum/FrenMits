using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FrenMits.Ui;

// Small reusable UI pieces shared across the plugin's windows.
internal static class Widgets
{
    public const uint CardBorder = 0xFF2F2724; // #24272F soft panel outline

    // How long the mouse must settle before any tooltip appears.
    private const double TooltipDelay = 0.35;

    private static Vector2 _tipPos;
    private static double _tipSince;
    private static int _tipFrame;

    // True once the mouse has settled here, so sweeping a page stays quiet. The
    // item rect is identity enough; a frame gap means the delay starts over.
    public static bool HoveredDelayed(ImGuiHoveredFlags flags = ImGuiHoveredFlags.None)
    {
        if (!ImGui.IsItemHovered(flags)) return false;
        var pos = ImGui.GetItemRectMin();
        var now = ImGui.GetTime();
        var frame = ImGui.GetFrameCount();
        if (pos != _tipPos || frame - _tipFrame > 2) { _tipPos = pos; _tipSince = now; }
        _tipFrame = frame;
        return now - _tipSince >= TooltipDelay;
    }

    // The one tooltip call, so every hint in the plugin waits the same beat.
    public static void Tooltip(string text)
    {
        if (HoveredDelayed()) ImGui.SetTooltip(text);
    }

    // The window font at the user's UI scale; null at 1x or while it builds.
    public static IDisposable? PushUiFont(FontManager fonts, float scale)
    {
        if (MathF.Abs(scale - 1f) < 0.02f) return null;
        var handle = fonts.Get(ImGui.GetFontSize() * scale, "Default", false, false);
        return handle is { Available: true } ? handle.Push() : null;
    }

    // The header every plugin window opens with: an accent bar, a title, and a
    // muted detail. Right-aligned actions go after it with SameLine.
    public static void WindowHeader(string title, string detail = "")
    {
        var p = ImGui.GetCursorScreenPos();
        var h = ImGui.GetFrameHeight();
        ImGui.GetWindowDrawList().AddRectFilled(
            p + new Vector2(0, 2), p + new Vector2(3, h - 2), Theme.Accent, 2f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10);
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.V(Theme.Accent), title);
        if (detail.Length == 0) return;
        ImGui.SameLine(0, 10);
        ImGui.TextColored(Theme.V(Theme.Muted), detail);
    }

    // Section header: an accent tab, then a muted label.
    public static void SectionHeader(string text)
    {
        ImGui.Dummy(new Vector2(0, 4));
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var h = ImGui.GetTextLineHeight();
        dl.AddRectFilled(p + new Vector2(0, 1), p + new Vector2(3, h), Theme.Accent, 2f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10);
        ImGui.TextColored(new Vector4(0.62f, 0.66f, 0.72f, 1f), text.ToUpperInvariant());
        ImGui.Spacing();
    }

    // The one checkbox style used across the plugin.
    public static bool GreenCheckbox(string label, ref bool v)
    {
        var on = v; // push and pop must use the same flag
        if (on)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, 0xFF5AC832);        // green (ABGR)
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0xFF6FD647);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, 0xFF5AC832);
            ImGui.PushStyleColor(ImGuiCol.CheckMark, 0xFFFFFFFF);      // white tick
        }
        var changed = ImGui.Checkbox(label, ref v);
        if (on) ImGui.PopStyleColor(4);
        return changed;
    }

    // Packed color from a picker's floats; Theme.V goes the other way.
    public static uint ToColor(Vector4 v) =>
        ((uint)(Math.Clamp(v.W, 0, 1) * 255) << 24) |
        ((uint)(Math.Clamp(v.Z, 0, 1) * 255) << 16) |
        ((uint)(Math.Clamp(v.Y, 0, 1) * 255) << 8) |
        (uint)(Math.Clamp(v.X, 0, 1) * 255);

    // Small stat pill: grey label, colored value.
    public static void Chip(string label, string value, uint valueColor)
    {
        var pad = new Vector2(8, 3);
        var lSz = ImGui.CalcTextSize(label);
        var vSz = ImGui.CalcTextSize(value);
        var size = new Vector2(lSz.X + vSz.X + 5 + pad.X * 2, ImGui.GetTextLineHeight() + pad.Y * 2);
        var p = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + size, Theme.PanelBg, 5f);
        dl.AddRect(p, p + size, CardBorder, 5f);
        dl.AddText(p + pad, Theme.Muted, label);
        dl.AddText(p + pad + new Vector2(lSz.X + 5, 0), valueColor, value);
        ImGui.Dummy(size);
    }

    // Clickable Chip, with a hover glow and a lit open state.
    public static bool ChipButton(string label, string value, uint valueColor, bool open)
    {
        var pad = new Vector2(8, 3);
        var lSz = ImGui.CalcTextSize(label);
        var vSz = ImGui.CalcTextSize(value);
        var size = new Vector2(lSz.X + vSz.X + 5 + pad.X * 2, ImGui.GetTextLineHeight() + pad.Y * 2);
        var p = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton("##chip_" + label, size);
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var lit = open || hovered;
        var bg = open ? (valueColor & 0x00FFFFFFu) | 0x33000000u
               : hovered ? (valueColor & 0x00FFFFFFu) | 0x1A000000u
               : Theme.PanelBg;
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + size, bg, 5f);
        dl.AddRect(p, p + size, lit ? valueColor : CardBorder, 5f, ImDrawFlags.None, lit ? 1.6f : 1f);
        dl.AddText(p + pad, Theme.Muted, label);
        dl.AddText(p + pad + new Vector2(lSz.X + 5, 0), valueColor, value);
        return clicked;
    }

    // One control: drag to adjust, or click to type a value.
    public static bool SliderInput(string label, ref float v, float min, float max, string fmt, float width = 150f)
    {
        ImGui.SetNextItemWidth(width);
        var changed = ImGui.DragFloat(label, ref v, MathF.Max(0.001f, (max - min) / 200f), min, max, fmt, ImGuiSliderFlags.AlwaysClamp);
        if (HoveredDelayed()) ImGui.SetTooltip("Drag to adjust; double-click to type.");
        return changed;
    }

    public static bool SliderInput(string label, ref int v, int min, int max, string fmt = "%d", float width = 150f)
    {
        ImGui.SetNextItemWidth(width);
        var changed = ImGui.DragInt(label, ref v, MathF.Max(0.05f, (max - min) / 200f), min, max, fmt, ImGuiSliderFlags.AlwaysClamp);
        if (HoveredDelayed()) ImGui.SetTooltip("Drag to adjust; double-click to type.");
        return changed;
    }

    // Accent-filled button, for a window's primary action.
    public static bool AccentButton(string label, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentHover);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentText);
        var clicked = ImGui.Button(label, size);
        ImGui.PopStyleColor(4);
        return clicked;
    }
}
