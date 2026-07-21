using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FrenMits.Windows;

// Small reusable UI pieces shared across the plugin's windows, so every window
// draws the same section headers, stat chips and accent buttons instead of each
// re-deriving them. All colors route through Theme (named roles + V()).
internal static class Widgets
{
    private const uint CardBorder = 0xFF2F2724; // #24272F soft panel outline

    // Accent-bar section header: a short accent tab, then a muted upper-case
    // label. Matches the config window's section rhythm.
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

    // Small stat pill: grey label, colored value, in a rounded panel. Advances
    // the cursor by its own size so callers can SameLine a row of them.
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

    // Clickable variant of Chip: same pill, but hit-tested, with a hover glow and
    // an "open" state that stays lit while its detail panel is showing. The value
    // color doubles as the accent (a red deaths chip lights red), so it reads as a
    // toggle without extra chrome. Returns true on click; the InvisibleButton is
    // the last item, so callers can IsItemHovered() for a tooltip afterwards.
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

    // Accent-filled button (white label), for the one primary action in a
    // window. Returns true on click, like ImGui.Button.
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
