using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// The mit sheet in pocket form: the calls just fired and the next few coming,
// each with +/- nudges for its per-call offset. Meant to sit open DURING a
// pull, so when a mechanic lands and the call was early or late you fix it on
// the spot (+ = the call fires earlier). Fully clickable in combat: this is a
// tool window, not an overlay.
public class MiniSheetWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public MiniSheetWindow(Plugin plugin) : base("Mit Tuner###fmmini")
    {
        _plugin = plugin;
        Size = new Vector2(320, 240);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(260, 140),
            MaximumSize = new Vector2(700, 800),
        };
        RespectCloseHotkey = false; // Escape mid-fight must not close it
    }

    public override void PreDraw()
    {
        Theme.PushWindow();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6, 4));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(2);
        Theme.PopWindow();
    }

    public override void Draw()
    {
        Theme.PushWidgets();
        try { DrawBody(); }
        finally { Theme.PopWidgets(); }
    }

    private void DrawBody()
    {
        var fight = _plugin.ActiveFight();
        if (fight == null)
        {
            ImGui.TextDisabled("No fight in this zone.");
            return;
        }

        var job = _plugin.ActiveJobAbbreviation();
        var elapsed = _plugin.CueClockFor(fight);
        var running = _plugin.Timer.Running;
        var lines = fight.OrderedLines
            .Where(l => l.Enabled && l.AppliesTo(job))
            .OrderBy(l => l.CueTime)
            .ToList();

        if (lines.Count == 0)
        {
            ImGui.TextDisabled("No calls planned for your job here.");
            return;
        }

        // Live: the last two calls (the ones you just judged) plus the next
        // five. Idle: the plan from the top, for pre-pull tweaking.
        var show = running
            ? lines.Where(l => l.CueTime <= elapsed).TakeLast(2)
                   .Concat(lines.Where(l => l.CueTime > elapsed).Take(5))
                   .ToList()
            : lines.Take(7).ToList();

        if (ImGui.BeginTable("##minitable", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("t", ImGuiTableColumnFlags.WidthFixed, 44);
            ImGui.TableSetupColumn("call", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("nudge", ImGuiTableColumnFlags.WidthFixed, 96);

            for (var i = 0; i < show.Count; i++)
            {
                var line = show[i];
                var rem = line.CueTime - elapsed;
                var past = running && rem <= 0f;

                ImGui.PushID(i);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                if (running)
                    ImGui.TextColored(past ? Dim : Bright, rem <= 0f ? $"{rem:0}s" : $"+{rem:0}s");
                else
                    ImGui.TextColored(Dim, line.TimeText);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                var name = string.IsNullOrWhiteSpace(line.Action)
                    ? line.Mechanic
                    : Icons.DisplayAction(line.ActionFor(job), job);
                ImGui.TextColored(past ? Dim : Bright, name);
                if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(line.Mechanic))
                    ImGui.SetTooltip(line.Mechanic);

                ImGui.TableNextColumn();
                if (ImGui.SmallButton("-")) Nudge(line, -0.5f);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Call 0.5s LATER");
                ImGui.SameLine(0, 3);
                var off = line.OffsetSeconds;
                ImGui.TextColored(off != 0f ? Edited : Dim, off == 0f ? " 0 " : $"{off:+0.#;-0.#}");
                ImGui.SameLine(0, 3);
                if (ImGui.SmallButton("+")) Nudge(line, +0.5f);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Call 0.5s EARLIER");

                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        ImGui.TextDisabled(running ? "+ = earlier. Changes apply instantly." : "+ = earlier. Pull to see live countdowns.");
    }

    // Same semantics as every other offset editor: clamped and saved. A nudge is
    // a HAND-SET offset, so flag it manual - the auto cooldown timer must leave it
    // alone. It is not a new line, so Custom stays off.
    private void Nudge(MitLine line, float delta)
    {
        line.OffsetSeconds = Math.Clamp(line.OffsetSeconds + delta, -30f, 30f);
        line.OffsetManual = true;
        C.Save();
        _plugin.SheetViewWindow.MarkPlanDirty(); // keep the sheet's cooldown cells honest
    }

    private static readonly Vector4 Bright = new(0.93f, 0.91f, 0.90f, 1f);
    private static readonly Vector4 Dim = new(0.55f, 0.53f, 0.52f, 1f);
    private static readonly Vector4 Edited = new(0.96f, 0.62f, 0.36f, 1f);
}
