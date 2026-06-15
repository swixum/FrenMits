using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// The Party Mit Recap as its own movable / resizable window: what mits were on the
// boss and the party this pull, when, by whom, and which standard raid mits never
// landed. Opened by the post-wipe popup or the Party Mit Recap config page.
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

    public override void Draw()
    {
        var r = _plugin.Recap;

        if (ImGui.Button("Capture now")) r.Capture();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Snapshot the mits up right now (use before the boss resets).");
        ImGui.SameLine();
        ImGui.TextDisabled(r.CapturedAt == default
            ? "no capture yet"
            : $"captured {(int)(DateTime.UtcNow - r.CapturedAt).TotalSeconds}s ago");

        if (!r.HasData)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Do a pull — the boss's mits, the party's cooldowns and anything missing show here.");
            return;
        }

        // Missing standard raid mits.
        var missed = r.NotSeen();
        ImGui.Spacing();
        if (missed.Count == 0)
            ImGui.TextColored(ImGuiColors.HealerGreen, "All four standard raid mits landed this pull.");
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, "Never landed: " + string.Join(", ", missed));
            ImGui.TextDisabled("(comp-dependent — no caster = no Addle, no MCH = no Dismantle, etc.)");
        }

        if (ImGui.CollapsingHeader("Up at capture", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (r.Snapshot.Count == 0) ImGui.TextDisabled("No mits were active.");
            else foreach (var m in r.Snapshot.OrderByDescending(m => m.OnBoss).ThenBy(m => m.Source))
            {
                if (m.Icon != 0) { Icons.Draw(m.Icon, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight())); ImGui.SameLine(0, 6); }
                var col = MitTypes.Color(m.Kind, C);
                if (col != 0) ImGui.PushStyleColor(ImGuiCol.Text, col);
                ImGui.TextUnformatted(m.Mit);
                if (col != 0) ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.TextDisabled($"· {m.Source} · {m.Remaining:0}s left");
            }
        }

        if (ImGui.CollapsingHeader("Applied this pull", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.BeginTable("##recapwin", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
                    new Vector2(0, 0)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 55);
                ImGui.TableSetupColumn("Mit", ImGuiTableColumnFlags.WidthStretch, 1);
                ImGui.TableSetupColumn("By / on", ImGuiTableColumnFlags.WidthStretch, 1);
                ImGui.TableHeadersRow();
                foreach (var a in r.LastLog.OrderBy(a => a.Time))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{(int)a.Time / 60}:{(int)a.Time % 60:00}");
                    ImGui.TableNextColumn();
                    var col = MitTypes.Color(a.Kind, C);
                    if (col != 0) ImGui.PushStyleColor(ImGuiCol.Text, col);
                    ImGui.TextUnformatted(a.Mit);
                    if (col != 0) ImGui.PopStyleColor();
                    ImGui.TableNextColumn();
                    if (a.OnBoss) ImGui.TextDisabled("on boss");
                    else ImGui.TextUnformatted(a.Source);
                }
                ImGui.EndTable();
            }
        }
    }
}
