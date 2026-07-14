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
    public const string NotesVersion = "1.0.0.129";

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
        ImGui.TextColored(ImGuiColors.ParsedGreen, "Deleted lines now stay deleted");
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
        ("Deleted lines stay deleted",
            "Deleting a sheet call now sticks. Zoning in, switching slot or role, and sheet "
            + "updates used to quietly re-add it; now the plugin remembers what you deleted."),
        ("Restore when you change your mind",
            "The Lines header shows how many sheet calls you've deleted, with a Restore button "
            + "to bring them all back. \"Reset to sheet\" also brings everything back."),
        ("From v128: Timer offset works now",
            "In case you missed it: a fight's Timer offset now genuinely shifts every call "
            + "(+10 fires everything 10s earlier) and resync can't cancel it. If you set an "
            + "offset long ago, it applies now - set it back to 0 if you didn't mean it."),
    };
}
