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
    public const string NotesVersion = "1.0.0.131";

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
        ImGui.TextColored(ImGuiColors.ParsedGreen, "New: Sheet View — the whole raid plan in one grid");
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
        ("Sheet View (under Sheet View in the sidebar, or /fm sheet)",
            "Every slot's mits in one grid, just like the Google sheet. Rows are the fight's "
            + "mechanics, columns are all ten slots, your slot is starred and tinted."),
        ("Re-time once, move everyone",
            "Click a mechanic's TIME to shift it for every slot at once. Click a cell to change "
            + "one slot's mit. Everything you touch turns orange (yours now, safe from sheet "
            + "updates), and the row's ⟲ puts it back on the sheet."),
        ("Share the fixed plan",
            "Hit Share plan, post the code, and each friend's import updates their own slot in "
            + "place. One person fixes the timings; the whole group syncs up."),
        ("From v130, in case you missed it",
            "Edits and deletes on the fight page now stick through zone-ins and sheet updates, "
            + "and wipe recaps cover the whole pull instead of just the last phase."),
    };
}
