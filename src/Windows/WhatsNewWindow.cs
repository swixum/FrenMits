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
    public const string NotesVersion = "1.0.0.123";

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
        ImGui.TextColored(ImGuiColors.ParsedGreen, "Full refresh: updated mits");
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
        ("Full reset to the sheet",
            "This update does a one-time full reset of Dancing Mad to the sheet, including any "
            + "timers you added yourself, to clear out stale/overlapping lines. Re-add your own "
            + "and they'll be kept from here on."),
        ("Dancing Mad mits refreshed",
            "The whole DMU timeline was re-synced to the latest Ikuya sheet again - every call "
            + "re-timed to the newest timings, plus the P5 enrage marker."),
        ("Optional job mitigations",
            "On a fight's page, a one-click Add for your job's extra mit: BRD Nature's Minne, "
            + "MNK Mantra, PLD Passage of Arms (and WHM Asylum), pulled from logs and spaced to "
            + "each ability's recast."),
        ("Combat timer",
            "A customizable combat stopwatch overlay (font, color, placement) under Tools."),
        ("Overlay and UI polish",
            "Mits at the same time now stack instead of one hiding the other; green checkboxes "
            + "replace the old pill toggles; the Refresh-from-sheet button is harder to hit by "
            + "accident; and a tidier settings layout."),
    };
}
