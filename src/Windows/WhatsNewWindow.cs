using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// A one-time "What's New" panel shown after the plugin updates. Dismissing it
// records the version so it won't show again until the next release with notes.
public class WhatsNewWindow : Window
{
    // Bump this (and the Notes below) when there's news to show. The panel pops
    // once per NotesVersion, so routine version bumps don't re-trigger it.
    public const string NotesVersion = "1.0.0.138";

    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public WhatsNewWindow(Plugin plugin)
        : base("Fren Mits: What's New##whatsnew")
    {
        _plugin = plugin;
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 0),
            MaximumSize = new Vector2(480, 900),
        };
    }

    public override void Draw()
    {
        ImGui.TextColored(ImGuiColors.ParsedGreen, "Sheet View grows up: notes, colors, cooldown checks");
        ImGui.TextDisabled($"Fren Mits v{Plugin.PluginVersion}");
        ImGui.Separator();
        ImGui.Spacing();

        foreach (var (head, body) in Notes)
        {
            ImGui.TextColored(new Vector4(0.96f, 0.62f, 0.36f, 1f), head);
            ImGui.PushTextWrapPos(0f);
            ImGui.TextUnformatted(body);
            ImGui.PopTextWrapPos();
            ImGui.Spacing();
        }

        ImGui.Separator();
        if (ImGui.Button("Got it", new Vector2(120, 0)))
            Dismiss();
    }

    public override void OnClose() => Dismiss();

    private void Dismiss()
    {
        C.LastWhatsNew = NotesVersion;
        C.Save();
        IsOpen = false;
    }

    private static readonly (string Head, string Body)[] Notes =
    {
        ("The sheet's phase notes, in game",
            "Sheet View now has a \"Sheet notes\" panel at the bottom with the notes section "
            + "from every phase tab of the official sheet (usage tips, footnotes, healer "
            + "callouts). Filter to a phase and the panel follows; collapse it any time."),
        ("Empty-box symbols fixed",
            "The little empty-box character that showed up around the UI is gone. It appeared "
            + "wherever a symbol wasn't in the game's font; every star, pen, undo arrow, "
            + "status dot and check mark is now drawn with real icons instead."),
        ("One-click role match",
            "The sidebar's Your Role section now has a \"Use current\" button that sets the "
            + "role from the job you're playing, just like the job picker's."),
        ("Sheet View declutter",
            "The fight dropdown is grouped by category with your slot shown per fight, and "
            + "the permanent hint text at the bottom is gone; the how-to now lives in the "
            + "toolbar's (?) hover."),
        ("Sheet View usability",
            "Columns are resizable: drag an edge, or double-click it to fit the text, like "
            + "a spreadsheet. The fight list scrolls instead of running off screen and no "
            + "longer overlaps long names. And an Import button now sits next to Share plan, "
            + "so pasting a friend's code happens right where you'd look for it."),
        ("Sheet View, now a real planner",
            "Mits are colored by type (party / tank / personal, your overlay colors). A cell "
            + "turns red when that mit is planned again before its cooldown can be back. "
            + "A filter box finds every row containing e.g. \"Reprisal\". Your slot's column "
            + "is pinned next to Mechanic, phase tabs show row counts, a corner tag names the "
            + "phase you're scrolled into, and right-clicking a cell offers delete / reset / "
            + "a per-call offset. Disabled lines now show dim with (off)."),
        ("Export and Replace",
            "Export copies the whole grid as spreadsheet-ready text (paste into Google Sheets "
            + "or Discord, phase notes included). Replace renames a mit across the entire "
            + "sheet in one go, like \"all my Vengeance becomes Damnation\"; replacing with "
            + "nothing deletes those calls."),
    };
}
