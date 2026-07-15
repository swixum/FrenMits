using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// The Party Mit Recap as its own movable / resizable window, themed to match the
// config UI: what mits were on the boss and the party this pull, when, by whom,
// and which standard raid mits never landed.
public class RecapWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public RecapWindow(Plugin plugin) : base("Party Mit Recap###recapwin")
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
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Copy the recap as text; paste it into Discord or your notes.");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Vec(0xFF81766E), r.CapturedAt == default
            ? "no capture yet"
            : $"captured {(int)(DateTime.UtcNow - r.CapturedAt).TotalSeconds}s ago");

        if (!r.HasData)
        {
            ImGui.Spacing();
            ImGui.TextColored(Vec(0xFF81766E), "Do a pull; the boss's mits, the party's cooldowns and");
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
            ImGui.TextColored(Vec(0xFF81766E), "comp-dependent: no caster = no Addle, no MCH = no Dismantle");
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

        // Full timeline: ONE row per mechanic, its mits inline with coverage
        // counts, so a wipe reads at a glance instead of as a long scroll.
        Header("Applied this pull");
        var events = r.LastEvents();
        var party = r.LastParty;
        var fight = _plugin.ActiveFight();
        if (ImGui.BeginTable("##recapwin", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX,
                new Vector2(0, 0)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 52);
            ImGui.TableSetupColumn("Mechanic", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Mits", ImGuiTableColumnFlags.WidthStretch, 1);
            ImGui.TableHeadersRow();

            var ih = ImGui.GetTextLineHeight();
            var idx = 0;
            while (idx < events.Count)
            {
                // Consecutive events under the same mechanic share one row.
                var mech = MechanicFor(fight, events[idx].Time);
                var group = new List<MitRecap.MitEvent> { events[idx] };
                var next = idx + 1;
                while (next < events.Count
                       && MechanicFor(fight, events[next].Time) == mech
                       && events[next].Time - events[idx].Time < 25f)
                    group.Add(events[next++]);
                idx = next;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(Vec(0xFF81766E), $"{(int)group[0].Time / 60}:{(int)group[0].Time % 60:00}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(mech.Length > 0 ? mech : "-");
                ImGui.TableNextColumn();

                var n = 0;
                foreach (var e in group)
                {
                    // Three mits per visual line, then wrap inside the cell, so
                    // heavy mechanics don't clip off the right edge.
                    if (n > 0 && n % 3 != 0) { ImGui.SameLine(0, 2); ImGui.TextColored(Vec(0xFF81766E), " · "); ImGui.SameLine(0, 2); }
                    n++;
                    if (e.Icon != 0) { Icons.Draw(e.Icon, new Vector2(ih, ih)); ImGui.SameLine(0, 4); }
                    var col = MitTypes.Color(e.Kind, C);
                    ImGui.TextColored(col != 0 ? Vec(col) : Vec(0xFFECE8E6), e.Mit);

                    if (e.OnBoss)
                    {
                        ImGui.SameLine(0, 4);
                        ImGui.TextColored(Vec(0xFF81766E), "(boss)");
                    }
                    else if (e.Kind == MitTypes.Kind.Party && party.Count is > 1 and <= 8)
                    {
                        // Coverage only for party-wide buffs, and only with a sane
                        // 8-man denominator (alliance zones would read "8/24").
                        // Coverage: how many of the party the buff actually hit.
                        ImGui.SameLine(0, 4);
                        var full = e.Covered.Count >= party.Count;
                        ImGui.TextColored(full ? Vec(Theme.Good) : Vec(Theme.Warn),
                            $"{e.Covered.Count}/{party.Count}");
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextColored(Vec(0xFF81766E), $"{e.Mit} coverage:");
                            foreach (var name in party)
                            {
                                // Check/cross via the icon font: the text font
                                // has no glyph for either symbol.
                                var hit = e.Covered.Contains(name);
                                var rowCol = hit ? Vec(Theme.Good) : Vec(0xFF5050E0);
                                using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                                    ImGui.TextColored(rowCol,
                                        (hit ? FontAwesomeIcon.Check : FontAwesomeIcon.Times).ToIconString());
                                ImGui.SameLine(0, 5);
                                ImGui.TextColored(rowCol, name);
                            }
                            ImGui.EndTooltip();
                        }
                    }
                    else if (e.Covered.Count == 1)
                    {
                        ImGui.SameLine(0, 4);
                        ImGui.TextColored(Vec(0xFF81766E), e.Covered[0]);
                    }
                }
            }
            ImGui.EndTable();
        }
    }

    // The plan mechanic nearest this moment (within a window), so recap rows can
    // group under the same names the calls used. Empty when no fight is active
    // or nothing on the plan is close.
    private static string MechanicFor(FightProfile? fight, float time)
    {
        if (fight == null) return "";
        MitLine? best = null;
        var bestDist = 9f; // window: within 9s of a planned call
        foreach (var l in fight.Lines)
        {
            var d = MathF.Abs(l.Time - time);
            if (d < bestDist && !string.IsNullOrWhiteSpace(l.Mechanic)) { best = l; bestDist = d; }
        }
        return best?.Mechanic.Trim() ?? "";
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
