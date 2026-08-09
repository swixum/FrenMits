using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Planning;

// The sheet in pocket form, with nudges for each call.
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
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 8) * Theme.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6, 4) * Theme.Scale);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(2);
        Theme.PopWindow();
    }

    public override void Draw()
    {
        Theme.PushWidgets();
        using var uiFont = Widgets.PushUiFont(_plugin.Fonts, Theme.Scale);
        try { DrawBody(); }
        finally { Theme.PopWidgets(); }
    }

    private void DrawBody()
    {
        var fight = _plugin.ActiveFight();
        if (fight == null)
        {
            ImGui.TextUnformatted("Nothing planned here");
            ImGui.PushTextWrapPos(0f);
            ImGui.TextDisabled("The Mit Tuner follows the fight you are standing in, and this "
                               + "duty has no sheet yet.");
            ImGui.PopTextWrapPos();
            ImGui.Spacing();
            if (Widgets.AccentButton("Make a sheet for this duty"))
                _plugin.SheetViewWindow.Open(null);
            return;
        }

        var job = _plugin.GetActiveJobAbbr(fight);
        var elapsed = _plugin.CueClockFor(fight);
        var running = _plugin.Timer.Live;
        // Filled into a reused buffer, since this runs every frame.
        _rows.Clear();
        foreach (var l in fight.OrderedLines)
            if (l.Enabled && l.AppliesTo(job)) _rows.Add(l);
        StableSortByCueTime(_rows);

        if (_rows.Count == 0)
        {
            ImGui.TextUnformatted("Your column is empty");
            ImGui.PushTextWrapPos(0f);
            ImGui.TextDisabled(string.IsNullOrEmpty(fight.Slot)
                ? "No slot is picked for this fight, so nothing is yours to press yet."
                : $"This sheet has rows, but nothing is assigned to {fight.Slot} yet.");
            ImGui.PopTextWrapPos();
            ImGui.Spacing();
            if (Widgets.AccentButton("Open Sheet View")) _plugin.SheetViewWindow.Open(fight);
            return;
        }

        // Live shows the calls around now, idle the plan from the top.
        int start = 0, end = Math.Min(7, _rows.Count);
        if (running)
        {
            var firstFuture = _rows.Count;
            for (var i = 0; i < _rows.Count; i++)
                if (_rows[i].CueTime > elapsed) { firstFuture = i; break; }
            start = Math.Max(0, firstFuture - 2);
            end = Math.Min(_rows.Count, firstFuture + 5);
        }

        if (ImGui.BeginTable("##minitable", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("t", ImGuiTableColumnFlags.WidthFixed, Theme.S(44f));
            ImGui.TableSetupColumn("call", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("nudge", ImGuiTableColumnFlags.WidthFixed, Theme.S(96f));

            for (var i = start; i < end; i++)
            {
                var line = _rows[i];
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
                if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Call 0.5s LATER");
                ImGui.SameLine(0, Theme.S(3f));
                var off = line.OffsetSeconds;
                ImGui.TextColored(off != 0f ? Edited : Dim, off == 0f ? " 0 " : $"{off:+0.#;-0.#}");
                ImGui.SameLine(0, Theme.S(3f));
                if (ImGui.SmallButton("+")) Nudge(line, +0.5f);
                if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Call 0.5s EARLIER");

                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        ImGui.TextDisabled(running ? "+ = earlier. Changes apply instantly." : "+ = earlier. Pull to see live countdowns.");
    }

    // Scratch for the visible rows, reused frame to frame.
    private readonly List<MitLine> _rows = new();

    // Insertion sort: stable like the OrderBy it replaced, on a handful of rows.
    private static void StableSortByCueTime(List<MitLine> rows)
    {
        for (var i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            var j = i - 1;
            while (j >= 0 && rows[j].CueTime > r.CueTime) { rows[j + 1] = rows[j]; j--; }
            rows[j + 1] = r;
        }
    }

    // A nudge is hand-set, so the solver must leave it alone.
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
