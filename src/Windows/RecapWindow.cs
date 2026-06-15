using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// The Party Mit Recap as its own movable / resizable window, themed to match the
// config UI: what mits were on the boss and the party this pull, when, by whom,
// and which standard raid mits never landed.
public class RecapWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public RecapWindow(Plugin plugin) : base("Party Mit Recap##recapwin")
    {
        _plugin = plugin;
        Size = new Vector2(470, 540);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw()
    {
        Theme.PushWindow();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        Theme.PopWindow();
    }

    public override void Draw()
    {
        Theme.PushWidgets();
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 16f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 7));

        DrawBody();

        ImGui.PopStyleVar(3);
        Theme.PopWidgets();
    }

    private void DrawBody()
    {
        var r = _plugin.Recap;

        if (Button("Capture now")) r.Capture();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Snapshot the mits up right now (before the boss resets).");
        ImGui.SameLine();
        if (Button("Copy")) ImGui.SetClipboardText(r.ToText());
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Copy the recap as text — paste it into Discord or your notes.");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Vec(0xFF81766E), r.CapturedAt == default
            ? "no capture yet"
            : $"captured {(int)(DateTime.UtcNow - r.CapturedAt).TotalSeconds}s ago");

        if (!r.HasData)
        {
            ImGui.Spacing();
            ImGui.TextColored(Vec(0xFF81766E), "Do a pull — the boss's mits, the party's cooldowns and");
            ImGui.TextColored(Vec(0xFF81766E), "anything missing will show here.");
            return;
        }

        // Boss name + fight time of the capture.
        ImGui.Dummy(new Vector2(0, 2));
        ImGui.TextColored(Vec(Theme.Accent), string.IsNullOrEmpty(r.BossName) ? "Last pull" : r.BossName);
        if (r.CaptureElapsed > 0)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(Vec(0xFF81766E), $"·  {(int)r.CaptureElapsed / 60}:{(int)r.CaptureElapsed % 60:00} in");
        }

        // Headline: missing standard raid mits.
        ImGui.Spacing();
        var missed = r.NotSeen();
        if (missed.Count == 0)
        {
            ImGui.TextColored(Vec(Theme.Good), "All four standard raid mits landed this pull.");
        }
        else
        {
            ImGui.TextColored(Vec(Theme.Warn), "Never landed:  " + string.Join("   ", missed));
            ImGui.TextColored(Vec(0xFF81766E), "comp-dependent — no caster = no Addle, no MCH = no Dismantle");
        }

        // What's up at the capture.
        Header("Up at capture");
        if (r.Snapshot.Count == 0) ImGui.TextColored(Vec(0xFF81766E), "Nothing was active.");
        else foreach (var m in r.Snapshot.OrderByDescending(m => m.OnBoss).ThenBy(m => m.Source))
        {
            if (m.Icon != 0) { Icons.Draw(m.Icon, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight())); ImGui.SameLine(0, 6); }
            var col = MitTypes.Color(m.Kind, C);
            ImGui.TextColored(col != 0 ? Vec(col) : Vec(0xFFECE8E6), m.Mit);
            ImGui.SameLine();
            ImGui.TextColored(Vec(0xFF81766E), $"· {m.Source} · {m.Remaining:0}s");
        }

        // Full timeline.
        Header("Applied this pull");
        if (ImGui.BeginTable("##recapwin", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX,
                new Vector2(0, 0)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 52);
            ImGui.TableSetupColumn("Mit", ImGuiTableColumnFlags.WidthStretch, 1);
            ImGui.TableSetupColumn("By / on", ImGuiTableColumnFlags.WidthStretch, 1);
            ImGui.TableHeadersRow();
            var ih = ImGui.GetTextLineHeight();
            foreach (var a in r.LastLog.OrderBy(a => a.Time))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(Vec(0xFF81766E), $"{(int)a.Time / 60}:{(int)a.Time % 60:00}");
                ImGui.TableNextColumn();
                if (a.Icon != 0) { Icons.Draw(a.Icon, new Vector2(ih, ih)); ImGui.SameLine(0, 6); }
                var col = MitTypes.Color(a.Kind, C);
                ImGui.TextColored(col != 0 ? Vec(col) : Vec(0xFFECE8E6), a.Mit);
                ImGui.TableNextColumn();
                ImGui.TextColored(Vec(a.OnBoss ? 0xFF81766E : 0xFFECE8E6u), a.OnBoss ? "on boss" : a.Source);
            }
            ImGui.EndTable();
        }
    }

    // Accent-bar section header, matching the config window's SeparatorText.
    private static void Header(string text)
    {
        ImGui.Dummy(new Vector2(0, 4));
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var h = ImGui.GetTextLineHeight();
        dl.AddRectFilled(p + new Vector2(0, 1), p + new Vector2(3, h), Theme.Accent, 2f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10);
        ImGui.TextColored(new Vector4(0.62f, 0.66f, 0.72f, 1f), text.ToUpperInvariant());
        ImGui.Spacing();
    }

    private static bool Button(string label) => ImGui.Button(label);
    private static Vector4 Vec(uint abgr) => new(
        (abgr & 0xFF) / 255f, ((abgr >> 8) & 0xFF) / 255f, ((abgr >> 16) & 0xFF) / 255f, ((abgr >> 24) & 0xFF) / 255f);
}
