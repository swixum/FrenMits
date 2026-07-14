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
    public const string NotesVersion = "1.0.0.128";

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
        ImGui.TextColored(ImGuiColors.ParsedGreen, "Timer offset now really shifts your calls");
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
        ("Timer offset actually works now",
            "A fight's Timer offset now genuinely shifts every call: +10 fires everything 10s "
            + "earlier, -2 fires everything 2s later, with resync on or off. Before this fix the "
            + "resync engine snapped the clock right back within seconds, so the offset silently "
            + "did nothing in synced fights like Dancing Mad."),
        ("Check your saved offsets",
            "Because the knob was dead, any offset you set in the past never applied - it will "
            + "now. If a fight's calls suddenly feel early or late, check its Timer offset and "
            + "set it back to 0."),
        ("Timeline fit button fixed",
            "The \"Shift by ...s\" suggestion under Resync now nudges your calls in the correct "
            + "direction (it also wrote to the dead knob before, so it never did anything)."),
    };
}
