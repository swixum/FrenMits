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
    public const string NotesVersion = "1.0.0.130";

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
        ImGui.TextColored(ImGuiColors.ParsedGreen, "Big fix round: edits stick, recaps cover the whole pull");
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
        ("Your edits stick now",
            "Re-timing or renaming a sheet call used to quietly revert the next time you zoned "
            + "in. Edited lines are now yours for good (like deleted ones since last update); "
            + "Restore or Reset to sheet brings the originals back."),
        ("Wipe recap covers the whole pull",
            "Phase cutscenes no longer restart the mit recap and review, so after a wipe you "
            + "see every mit from the whole pull - and the wipe popup can't appear mid-fight "
            + "during a transition anymore."),
        ("Sharing a plan updates, not duplicates",
            "Importing a friend's plan for a fight you already have now updates that fight "
            + "instead of creating a stuck duplicate that never fires."),
        ("Voice and polish fixes",
            "A slow online voice can no longer talk over the next call; charge mits (Aurora, "
            + "Oblation) aren't shown as on cooldown when a charge is ready; a passed call's "
            + "NOW now stays up alongside the next call; plus smaller editor and overlay fixes."),
    };
}
