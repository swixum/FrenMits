using Dalamud.Bindings.ImGui;

namespace FrenMits.Windows;

// A cohesive dark theme for the config window, inspired by clean panel-based
// plugin UIs: near-black backgrounds, soft borders, rounded controls and a single
// blue accent for anything interactive. Colors are packed ABGR (0xAABBGGRR).
internal static class Theme
{
    public const uint Accent = 0xFFF6823B;       // #3B82F6 blue
    public const uint AccentHover = 0xFFFAA560;   // #60A5FA
    public const uint AccentText = 0xFFFFFFFF;
    public const uint PanelBg = 0xFF14110E;       // card background
    public const uint Good = 0xFF4FB45A;          // saved / on (green)
    public const uint Warn = 0xFF3BC0F0;          // amber-ish notice

    // Window-scope colors — must be pushed before ImGui.Begin (in PreDraw).
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

    // Widget-scope colors — fine to push inside Draw().
    private static readonly (ImGuiCol Col, uint Val)[] WidgetColors =
    {
        (ImGuiCol.Text,               0xFFECE8E6),
        (ImGuiCol.TextDisabled,       0xFF81766E),
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

    // Rounded, softer geometry so the window doesn't look like raw ImGui. Window-
    // scope vars (rounding/border/padding of the window itself) are pushed before
    // Begin; the rest with the widgets.
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
