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
    public static void Tooltip(string text, ImGuiHoveredFlags flags = ImGuiHoveredFlags.None)
    {
        if (HoveredDelayed(flags)) ImGui.SetTooltip(text);
    }

    // A hint on a control that is currently held: the reason it cannot be
    // touched is exactly when it is worth reading, so hover still counts.
    public static void TooltipWhenHeld(string text) => Tooltip(text, ImGuiHoveredFlags.AllowWhenDisabled);

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
            p + new Vector2(0, 2), p + new Vector2(Theme.S(3f), h - 2), Theme.Accent, 2f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Theme.S(10f));
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.V(Theme.Accent), title);
        if (detail.Length == 0) return;
        ImGui.SameLine(0, 10);
        ImGui.TextColored(Theme.V(Theme.Muted), detail);
    }

    // Text cut to a pixel width, with an ellipsis when it had to be cut. Used
    // where a name shares its line with right-aligned furniture: eliding is the
    // one way a long name cannot end up drawn underneath it.
    public static string Elide(string text, float maxWidth)
    {
        if (text.Length == 0 || maxWidth <= 0f) return text;
        if (ImGui.CalcTextSize(text).X <= maxWidth) return text;
        const string tail = "...";
        var room = maxWidth - ImGui.CalcTextSize(tail).X;
        if (room <= 0f) return tail;
        // Binary search the longest prefix that fits, so the cut is by pixels
        // rather than by a character count that is wrong in every other font.
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (ImGui.CalcTextSize(text[..mid]).X <= room) lo = mid; else hi = mid - 1;
        }
        return lo <= 0 ? tail : text[..lo].TrimEnd() + tail;
    }

    // Section header: an accent tab, then a muted label.
    public static void SectionHeader(string text)
    {
        ImGui.Dummy(new Vector2(0, Theme.S(4f)));
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var h = ImGui.GetTextLineHeight();
        dl.AddRectFilled(p + new Vector2(0, 1), p + new Vector2(Theme.S(3f), h), Theme.Accent, 2f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Theme.S(10f));
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

    // Chip padding tracks the text, like every other framed thing here.
    private static Vector2 ChipPad => new Vector2(8, 3) * Theme.Scale;
    private static float ChipGap => 5f * Theme.Scale;

    // Label, gap, value, padding both sides. A value-only chip drops the gap.
    private static Vector2 ChipSize(Vector2 labelSize, Vector2 valueSize, bool hasLabel)
        => new(labelSize.X + valueSize.X + (hasLabel ? ChipGap : 0f) + ChipPad.X * 2,
               ImGui.GetTextLineHeight() + ChipPad.Y * 2);

    // Small stat pill: grey label, colored value. An empty label gives a chip
    // that is only the number, for where the heading beside it already names it.
    public static void Chip(string label, string value, uint valueColor)
    {
        var pad = ChipPad;
        var hasLabel = label.Length > 0;
        var lSz = ImGui.CalcTextSize(label);
        var vSz = ImGui.CalcTextSize(value);
        var size = ChipSize(lSz, vSz, hasLabel);
        var p = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + size, Theme.PanelBg, 5f);
        dl.AddRect(p, p + size, CardBorder, 5f);
        if (hasLabel) dl.AddText(p + pad, Theme.Muted, label);
        dl.AddText(p + pad + new Vector2(hasLabel ? lSz.X + ChipGap : 0f, 0), valueColor, value);
        ImGui.Dummy(size);
    }

    // Clickable Chip, with a hover glow and a lit open state.
    public static bool ChipButton(string label, string value, uint valueColor, bool open)
    {
        var pad = ChipPad;
        var lSz = ImGui.CalcTextSize(label);
        var vSz = ImGui.CalcTextSize(value);
        var size = ChipSize(lSz, vSz, label.Length > 0);
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
        dl.AddText(p + pad + new Vector2(lSz.X + ChipGap, 0), valueColor, value);
        return clicked;
    }

    // ---- label column ----
    // ImGui draws a control's label after the widget. For a value control that
    // reads backwards, so the label is drawn first instead, right-aligned into a
    // column shared by every row in the same scope. Scoped per tab: one column
    // for a whole window would be as wide as its longest label everywhere.
    // Width is measured as rows draw and applied next frame.

    private static readonly Dictionary<string, (float Cur, float Next)> LabelCols = new();
    private static string _labelScope = "";

    public static void LabelScope(string key) => _labelScope = key;

    public static float LabelColWidth
        => LabelCols.TryGetValue(_labelScope, out var e) ? e.Cur : 0f;

    public static void RowLabel(string text)
    {
        // Everything from ## on is an ImGui id, not something to draw. Widgets
        // strip it themselves; a plain Text call does not.
        var cut = text.IndexOf("##", StringComparison.Ordinal);
        var shown = cut >= 0 ? text[..cut] : text;

        var w = ImGui.CalcTextSize(shown).X;
        var e = LabelCols.TryGetValue(_labelScope, out var v) ? v : default;
        LabelCols[_labelScope] = (e.Cur, MathF.Max(e.Next, w));
        ImGui.AlignTextToFramePadding();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(e.Cur, w) - w);
        ImGui.TextDisabled(shown);
        ImGui.SameLine(0, 8f * Theme.Scale);
    }

    // Once per frame: this frame's widest label per scope sets next frame's column.
    public static void RollLabelCols()
    {
        foreach (var key in LabelCols.Keys.ToList())
            LabelCols[key] = (LabelCols[key].Next, 0f);
    }

    // One control: drag to adjust, or click to type a value.
    public static bool SliderInput(string label, ref float v, float min, float max, string fmt, float width = 150f)
    {
        RowLabel(label);
        ImGui.SetNextItemWidth(width);
        var changed = ImGui.DragFloat("##" + label, ref v, MathF.Max(0.001f, (max - min) / 200f), min, max, fmt, ImGuiSliderFlags.AlwaysClamp);
        if (HoveredDelayed()) ImGui.SetTooltip("Drag to adjust; double-click to type.");
        return changed;
    }

    public static bool SliderInput(string label, ref int v, int min, int max, string fmt = "%d", float width = 150f)
    {
        RowLabel(label);
        ImGui.SetNextItemWidth(width);
        var changed = ImGui.DragInt("##" + label, ref v, MathF.Max(0.05f, (max - min) / 200f), min, max, fmt, ImGuiSliderFlags.AlwaysClamp);
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

    // ---- segmented control ----
    // Small buttons joined into one: square corners, hairline gaps, one outline.
    // Call Begin right where the first segment goes, then Segment per item.

    private static float _segLeft;

    public static void SegmentBegin()
    {
        _segLeft = ImGui.GetCursorScreenPos().X;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1f, ImGui.GetStyle().ItemSpacing.Y));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
    }

    public static bool Segment(string label, bool on) => Segment(label, on, Theme.Accent);

    // A lit color of its own, for a segment that means "changed" rather than
    // "selected": accent says picked, amber says you overrode the auto choice.
    public static bool Segment(string label, bool on, uint onColor)
    {
        if (on)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, onColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Lighten(onColor, 0.22f));
            ImGui.PushStyleColor(ImGuiCol.Text, OnColorText(onColor));
        }
        var clicked = ImGui.SmallButton(label);
        if (on) ImGui.PopStyleColor(3);
        return clicked;
    }

    private static uint Lighten(uint abgr, float t)
    {
        uint Ch(int shift)
        {
            var c = (abgr >> shift) & 0xFF;
            return (uint)(c + (255 - c) * t) & 0xFF;
        }
        return (abgr & 0xFF000000) | (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
    }

    // Black on a light fill, white on a dark one, so a lit segment stays legible.
    private static uint OnColorText(uint abgr)
    {
        var r = abgr & 0xFF;
        var g = (abgr >> 8) & 0xFF;
        var b = (abgr >> 16) & 0xFF;
        return r * 299 + g * 587 + b * 114 > 140_000 ? 0xFF120E0Du : Theme.AccentText;
    }

    public static void SegmentEnd()
    {
        ImGui.PopStyleVar(2);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddRect(new Vector2(_segLeft, min.Y), max, CardBorder, 4f);
    }

    // Filled danger: this button destroys a body of work. One color for all of
    // them, and it follows colorblind mode, which the old literals never did.
    public static void PushDanger()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Danger);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.DangerHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.DangerHover);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentText);
    }

    // Outlined danger: this removes one row, not a body of work.
    public static void PushDangerOutline()
    {
        var rgb = Theme.Danger & 0x00FFFFFFu;
        ImGui.PushStyleColor(ImGuiCol.Button, rgb | 0x28000000u);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, rgb | 0x4C000000u);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, rgb | 0x6E000000u);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Danger);
    }

    public static void PopDanger() => ImGui.PopStyleColor(4);

    public static bool DangerButton(string label, Vector2 size = default)
    {
        PushDanger();
        var clicked = ImGui.Button(label, size);
        PopDanger();
        return clicked;
    }

    public static bool DangerOutlineButton(string label, Vector2 size = default)
    {
        PushDangerOutline();
        var clicked = ImGui.Button(label, size);
        PopDanger();
        return clicked;
    }

}
