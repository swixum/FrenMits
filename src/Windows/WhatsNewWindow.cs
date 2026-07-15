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
    public const string NotesVersion = "1.0.0.132";

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
        ImGui.TextColored(ImGuiColors.ParsedGreen, "Per-call offsets, sheet notes, and a cleaner recap");
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
        ("Offset a single call",
            "New ±s column in the line table (and in each line's … options): +2 fires just that "
            + "one call 2s earlier. The fight-wide Timer offset now sits at the top of each fight "
            + "instead of hiding in Advanced."),
        ("Party Mit Recap, rebuilt",
            "One line per mechanic, with coverage counts: Troubadour 7/8 means one person missed "
            + "it - hover to see exactly who (green in, red out)."),
        ("Notes on the sheet",
            "Right-click a mechanic in Sheet View to write a note (Ikuya-footer style). Hover any "
            + "✎ row and the note shows at the bottom - no clicks to read. Notes travel with "
            + "shared plan codes."),
        ("Polish",
            "★ marks the official sheets everywhere; the call display no longer wanders as text "
            + "length changes; cooldown-aware calls dim only the ability, not the whole line; a "
            + "one-click \"Use current (job)\" button; and the footer now just tells the truth: "
            + "every edit saves instantly, nothing to lose on exit."),
    };
}
