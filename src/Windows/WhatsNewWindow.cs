using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// A one-time "What's New" panel shown after the plugin updates. Dismissing it
// records the version so it won't show again until the next release with notes.
// One short line per version; only versions newer than the last one dismissed
// are listed, so it reads like a changelog, not an essay.
public class WhatsNewWindow : Window
{
    // Bump this (and the Notes below) when there's news to show. The panel pops
    // once per NotesVersion, so routine version bumps don't re-trigger it.
    public const string NotesVersion = "1.0.0.159";

    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public WhatsNewWindow(Plugin plugin)
        : base("Fren Mits: What's New###whatsnew")
    {
        _plugin = plugin;
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 0),
            MaximumSize = new Vector2(520, 900),
        };
    }

    public override void Draw()
    {
        ImGui.TextColored(ImGuiColors.ParsedGreen, "What's new");
        ImGui.TextDisabled($"Fren Mits v{Plugin.PluginVersion}");
        ImGui.Separator();
        ImGui.Spacing();

        var shown = 0;
        foreach (var (version, text) in Notes)
        {
            if (!IsNewerThan(version, C.LastWhatsNew)) continue;
            if (++shown > 10) break; // fresh installs see the latest ten, not the whole history
            ImGui.TextDisabled(version);
            ImGui.SameLine(88f);
            ImGui.PushTextWrapPos(0f);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
        }
        if (shown == 0) ImGui.TextDisabled("You're all caught up.");

        ImGui.Spacing();
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

    // True when `version` is newer than `seen` ("" or unparseable = show).
    private static bool IsNewerThan(string version, string seen)
    {
        if (!Version.TryParse(seen, out var s)) return true;
        return Version.TryParse(version, out var v) && v > s;
    }

    // Newest first; a few words per release.
    private static readonly (string Version, string Text)[] Notes =
    {
        ("1.0.0.159", "Reliability: a stuck cutscene flag can no longer freeze the timer and hide the overlays; internal errors now show in the header."),
        ("1.0.0.158", "The recap now compares reality to the plan: each mechanic lists planned mits that never landed, and partial coverage."),
        ("1.0.0.157", "Press windows: stretch one mit over several hits (right-click a cell) and the sheet computes when to press it, squeeze warnings included."),
        ("1.0.0.156", "Smarter checks: shared cooldowns (Bloodwhetting family) count as one timer, and amber cells flag mits above the duty's level sync."),
        ("1.0.0.155", "Quieter hovers: the official-sheet tooltip lives on the star, and sweep paths wait a beat."),
        ("1.0.0.154", "Polish: shorter sheet help, aligned control widths, window positions safe across future renames."),
        ("1.0.0.153", "Practice moved onto each fight's page, one Add fight menu, quieter status header, Refresh from sheet moved off Home."),
        ("1.0.0.152", "Sheet cells: Enter moves down, Tab moves right, like a spreadsheet. A Get started card for new installs."),
        ("1.0.0.151", "Drag columns to reorder, row hover highlight, click the server-bar timer, quick jumps between windows."),
        ("1.0.0.150", "Sheet View toolbar folded into Build and Plan menus."),
        ("1.0.0.149", "Tidier UI: fewer sidebar pages, extras folded away, one Preview button."),
        ("1.0.0.148", "Test mode turns itself off when a real pull starts."),
        ("1.0.0.147", "Shorter update notes (this list)."),
        ("1.0.0.146", "Import an FFLogs kill into a custom sheet: rows + anchors from any report link."),
        ("1.0.0.145", "Build from pull: your own wipes become rows + resync anchors automatically."),
        ("1.0.0.144", "Build your own mit sheets for any duty (+ New sheet in the fight list)."),
        ("1.0.0.143", "Job extras (Nature's Minne, ...) labeled instead of looking like duplicates."),
        ("1.0.0.142", "Undo (Ctrl+Z), cell/column copy-paste, and restorable plan snapshots."),
        ("1.0.0.141", "Pin columns by right-click (pin icon); phase rows in accent blue."),
        ("1.0.0.140", "Cleaner phase tabs."),
        ("1.0.0.139", "Mit type colors are now a Colors checkbox, off by default."),
        ("1.0.0.138", "Export the sheet as spreadsheet-ready text; Replace renames a mit everywhere."),
        ("1.0.0.137", "Cooldown warnings (red cells), filter box, per-call offsets on right-click."),
        ("1.0.0.136", "Sheet Import button, resizable columns (double-click an edge), scrolling fight list."),
        ("1.0.0.135", "Sheet footer decluttered; fight dropdown grouped by category."),
        ("1.0.0.134", "One-click \"Use current\" for Your Role."),
        ("1.0.0.133", "The sheet's per-phase notes in game; empty-box symbols fixed everywhere."),
    };
}
