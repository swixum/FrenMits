using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FrenMits.Windows;

// A cohesive dark theme for the config window, inspired by clean panel-based
// plugin UIs: near-black backgrounds, soft borders, rounded controls and a single
// blue accent for anything interactive.
//
// Colors are packed ABGR (0xAABBGGRR) - the byte order ImGui wants - so the hex
// reads back-to-front from CSS.
internal static class Theme
{
    // --- chrome (fixed regardless of colorblind mode) --------------------
    public const uint Accent = 0xFFF6823B;       // #3B82F6 blue - interactive things
    public const uint AccentHover = 0xFFFAA560;   // #60A5FA
    public const uint AccentText = 0xFFFFFFFF;    // white text on the accent
    public const uint PanelBg = 0xFF14110E;       // #0E1114 card background

    // --- text roles ------------------------------------------------------
    public const uint TextBright = 0xFFECE8E6;    // #E6E8EC primary text
    public const uint Muted = 0xFF81766E;         // #6E7681 secondary / detail text

    // --- status roles (hue carries meaning, so they switch to an
    //     Okabe-Ito colorblind-safe set when the user turns it on) --------

    // When true, status colors avoid the red/green pairing that reads the
    // same under the common forms of color blindness.
    public static bool Colorblind;

    public static uint Good => Colorblind ? 0xFF739E00 : 0xFF4FB45A;   // #5AB44F green -> #009E73 bluish-green
    public static uint Warn => Colorblind ? 0xFF009FE6 : 0xFF3BC0F0;   // #F0C03B amber -> #E69F00 orange
    public static uint Danger => Colorblind ? 0xFFA779CC : 0xFF5050E0; // #E05050 red   -> #CC79A7 reddish-purple

    // ABGR -> ImGui Vector4: the one place packed colors become floats, so no
    // window has to re-derive the conversion.
    public static Vector4 V(uint abgr) => new(
        (abgr & 0xFF) / 255f, ((abgr >> 8) & 0xFF) / 255f, ((abgr >> 16) & 0xFF) / 255f, ((abgr >> 24) & 0xFF) / 255f);

    // Window-scope colors - must be pushed before ImGui.Begin (in PreDraw).
    private static readonly (ImGuiCol Col, uint Val)[] WindowColors =
    {
        (ImGuiCol.WindowBg,           0xFF120E0D),
        (ImGuiCol.PopupBg,            0xFF1B1614),
        (ImGuiCol.Border,             0xFF2F2724),
        (ImGuiCol.TitleBg,            0xFF191311),
        (ImGuiCol.TitleBgActive,      0xFF221A16),
        (ImGuiCol.TitleBgCollapsed,   0xFF191311),
        (ImGuiCol.ScrollbarBg,        0xFF120E0D),
    };

    // Widget-scope colors - fine to push inside Draw().
    private static readonly (ImGuiCol Col, uint Val)[] WidgetColors =
    {
        (ImGuiCol.Text,               TextBright),
        (ImGuiCol.TextDisabled,       Muted),
        (ImGuiCol.ChildBg,            0x00000000),
        (ImGuiCol.FrameBg,            0xFF241D1A),
        (ImGuiCol.FrameBgHovered,     0xFF332723),
        (ImGuiCol.FrameBgActive,      0xFF40312B),
        (ImGuiCol.Button,             0xFF30231F),
        (ImGuiCol.ButtonHovered,      0xFF42312A),
        (ImGuiCol.ButtonActive,       0xFF564034),
        (ImGuiCol.Header,             0xFF34271F),
        (ImGuiCol.HeaderHovered,      0xFF50362A),
        (ImGuiCol.HeaderActive,       0xFF634032),
        // Tabs share the section-header surface family so they read as part of
        // the theme, not raw ImGui blue-gray.
        (ImGuiCol.Tab,                0xFF2A211C),
        (ImGuiCol.TabHovered,         0xFF50362A),
        (ImGuiCol.TabActive,          0xFF634032),
        (ImGuiCol.TabUnfocused,       0xFF241D1A),
        (ImGuiCol.TabUnfocusedActive, 0xFF4A362C),
        (ImGuiCol.CheckMark,          Accent),
        (ImGuiCol.SliderGrab,         Accent),
        (ImGuiCol.SliderGrabActive,   AccentHover),
        (ImGuiCol.Separator,          0xFF2F2724),
        (ImGuiCol.SeparatorHovered,   0xFF50362A),
        (ImGuiCol.SeparatorActive,    Accent),
        (ImGuiCol.ScrollbarGrab,      0xFF382E2A),
        (ImGuiCol.ScrollbarGrabHovered, 0xFF4C3F3A),
        (ImGuiCol.ScrollbarGrabActive,  Accent),
    };

    // Rounded, softer geometry so the window doesn't look like raw ImGui.
    private static readonly (ImGuiStyleVar Var, float Val)[] WindowVarsF =
    {
        (ImGuiStyleVar.WindowRounding, 9f),
        (ImGuiStyleVar.WindowBorderSize, 1f),
        (ImGuiStyleVar.ChildRounding, 8f),
        (ImGuiStyleVar.PopupRounding, 7f),
    };

    private static readonly (ImGuiStyleVar Var, float Val)[] WidgetVarsF =
    {
        (ImGuiStyleVar.FrameRounding, 5f),
        (ImGuiStyleVar.GrabRounding, 4f),
        (ImGuiStyleVar.TabRounding, 5f),
        (ImGuiStyleVar.ScrollbarRounding, 6f),
    };

    private static readonly (ImGuiStyleVar Var, System.Numerics.Vector2 Val)[] WidgetVarsV =
    {
        (ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(9, 5)),
        (ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(8, 6)),
        (ImGuiStyleVar.ItemInnerSpacing, new System.Numerics.Vector2(6, 4)),
    };

    public static void PushWindow()
    {
        foreach (var (c, v) in WindowColors) ImGui.PushStyleColor(c, v);
        foreach (var (s, v) in WindowVarsF) ImGui.PushStyleVar(s, v);
    }

    public static void PopWindow()
    {
        ImGui.PopStyleVar(WindowVarsF.Length);
        ImGui.PopStyleColor(WindowColors.Length);
    }

    public static void PushWidgets()
    {
        foreach (var (c, v) in WidgetColors) ImGui.PushStyleColor(c, v);
        foreach (var (s, v) in WidgetVarsF) ImGui.PushStyleVar(s, v);
        foreach (var (s, v) in WidgetVarsV) ImGui.PushStyleVar(s, v);
    }

    public static void PopWidgets()
    {
        ImGui.PopStyleVar(WidgetVarsF.Length + WidgetVarsV.Length);
        ImGui.PopStyleColor(WidgetColors.Length);
    }
}
