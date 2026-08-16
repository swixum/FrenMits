using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FrenAlerts.Ui;

internal static class Theme
{
    // ---- chrome ----

    // One color for every interactive thing; the config's picker sets it.
    public static uint Accent = DefaultAccent;
    public const uint DefaultAccent = 0xFFF755A8;  // #A855F7 violet
    public static uint AccentHover => Lighten(Accent, 0.28f);
    public const uint AccentText = 0xFFFFFFFF;    // white text on the accent
    public const uint PanelBg = 0xFF191014;       // #141019 card background

    // Text and spacing multiplier for the plugin's own windows.
    public static float Scale = 1f;

    public static float S(float px) => px * Scale;

    public static Vector2 Sz(float w, float h = 0f) => new(S(w), h == 0f ? 0f : S(h));

    // A lighter shade of a packed color, for hover states.
    private static uint Lighten(uint abgr, float t)
    {
        uint Ch(int shift)
        {
            var c = (abgr >> shift) & 0xFF;
            return (uint)(c + (255 - c) * t) & 0xFF;
        }
        return (abgr & 0xFF000000) | (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
    }

    // ---- text roles ----
    public const uint TextBright = 0xFFF2E7EC;    // #ECE7F2 primary text
    public const uint Muted = 0xFF90767E;         // #7E7690 secondary / detail text

    // ---- status roles ----

    // When true, status colors avoid the red and green pairing.
    public static bool Colorblind;

    public static uint Good => Colorblind ? 0xFF739E00 : 0xFF4FB45A;   // #5AB44F green -> #009E73 bluish-green
    public static uint Warn => Colorblind ? 0xFF009FE6 : 0xFF3BC0F0;   // #F0C03B amber -> #E69F00 orange
    public static uint Danger => Colorblind ? 0xFFA779CC : 0xFF5050E0; // #E05050 red   -> #CC79A7 reddish-purple
    public static uint DangerHover => Lighten(Danger, 0.22f);
    public static uint GoodBright => Lighten(Good, 0.3f);              // the just-saved flash

    // Uppercase headings and empty states, a step up from Muted.
    public const uint Heading = 0xFFBB9CA7;                            // #A79CBB
    public const uint NavText = 0xFFDDC6CF;                            // #CFC6DD sidebar rows at rest

    // The one place packed colors become floats.
    public static Vector4 V(uint abgr) => new(
        (abgr & 0xFF) / 255f, ((abgr >> 8) & 0xFF) / 255f, ((abgr >> 16) & 0xFF) / 255f, ((abgr >> 24) & 0xFF) / 255f);

    // Window colors, pushed before Begin.
    private static readonly (ImGuiCol Col, uint Val)[] WindowColors =
    {
        (ImGuiCol.WindowBg,           0xFF140D10),   // #100D14
        (ImGuiCol.PopupBg,            0xFF211519),   // #191521
        (ImGuiCol.Border,             0xFF3C2A32),   // #322A3C
        (ImGuiCol.TitleBg,            0xFF1D1217),   // #17121D
        (ImGuiCol.TitleBgActive,      0xFF2A1A21),   // #211A2A
        (ImGuiCol.TitleBgCollapsed,   0xFF1D1217),
        (ImGuiCol.ScrollbarBg,        0xFF140D10),
    };

    // Widget-scope colors - fine to push inside Draw().
    private static readonly (ImGuiCol Col, uint Val)[] WidgetColors =
    {
        (ImGuiCol.Text,               TextBright),
        (ImGuiCol.TextDisabled,       Muted),
        (ImGuiCol.ChildBg,            0x00000000),
        (ImGuiCol.FrameBg,            0xFF2B1B22),   // #221B2B
        (ImGuiCol.FrameBgHovered,     0xFF39242E),   // #2E2439
        (ImGuiCol.FrameBgActive,      0xFF482E3A),   // #3A2E48
        (ImGuiCol.Button,             0xFF35212A),   // #2A2135
        (ImGuiCol.ButtonHovered,      0xFF472E39),   // #392E47
        (ImGuiCol.ButtonActive,       0xFF5C3B4A),   // #4A3B5C
        (ImGuiCol.Header,             0xFF4A2433),   // #33244A
        (ImGuiCol.HeaderHovered,      0xFF6A3246),   // #46326A
        (ImGuiCol.HeaderActive,       0xFF823F57),   // #573F82
        // Tabs share the header surface, so they read as ours.
        (ImGuiCol.Tab,                0xFF301E25),   // #251E30
        (ImGuiCol.TabHovered,         0xFF6A3246),
        (ImGuiCol.TabActive,          0xFF823F57),
        (ImGuiCol.TabUnfocused,       0xFF2B1B22),
        (ImGuiCol.TabUnfocusedActive, 0xFF5E2F3E),   // #3E2F5E
        (ImGuiCol.Separator,          0xFF3C2A32),
        (ImGuiCol.SeparatorHovered,   0xFF6A3246),
        (ImGuiCol.ScrollbarGrab,      0xFF47323B),   // #3B3247
        (ImGuiCol.ScrollbarGrabHovered, 0xFF60424E), // #4E4260
    };

    // Pushed from the live accent, so the picker moves them all at once.
    private static readonly ImGuiCol[] AccentColors =
    {
        ImGuiCol.CheckMark, ImGuiCol.SliderGrab, ImGuiCol.SeparatorActive, ImGuiCol.ScrollbarGrabActive,
    };

    // Rounded, so the window doesn't look raw.
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

    private static readonly (ImGuiStyleVar Var, Vector2 Val)[] WidgetVarsV =
    {
        (ImGuiStyleVar.FramePadding, new Vector2(9, 5)),
        (ImGuiStyleVar.ItemSpacing, new Vector2(8, 6)),
        (ImGuiStyleVar.ItemInnerSpacing, new Vector2(6, 4)),
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
        foreach (var c in AccentColors) ImGui.PushStyleColor(c, Accent);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, AccentHover);
        // Padding grows with the text, so a scaled window keeps its proportions.
        foreach (var (s, v) in WidgetVarsF) ImGui.PushStyleVar(s, v * Scale);
        foreach (var (s, v) in WidgetVarsV) ImGui.PushStyleVar(s, v * Scale);
    }

    public static void PopWidgets()
    {
        ImGui.PopStyleVar(WidgetVarsF.Length + WidgetVarsV.Length);
        ImGui.PopStyleColor(WidgetColors.Length + AccentColors.Length + 1);
    }
}
