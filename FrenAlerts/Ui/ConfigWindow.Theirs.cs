using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using FrenAlerts.Engine.Scripts;

namespace FrenAlerts.Ui;

// The calls the imported set makes, listed on the fight's own page.
//
// Where the imported set covers a fight it is the only thing calling: ours is not
// loaded there at all. A page that listed our calls anyway was describing a hundred
// and seventy calls that never fire, beside a strategy list nobody's answers reach,
// while the fight ran a hundred and sixty of theirs that appeared nowhere.
//
// So a covered fight shows theirs, per mechanic, in their own words.
public partial class ConfigWindow
{
    private string _theirFilter = "";

    private void DrawTheirCalls(ushort territory)
    {
        if (Runner is not { } runner) return;

        var calls = runner.ScriptCallsFor(territory);
        if (calls.Count == 0)
        {
            Widgets.ListBegin();
            Widgets.RowNote("The imported set covers this fight, but listed no calls.");
            Widgets.ListEnd();
            return;
        }

        var phase = DrawTheirPhaseTabs(calls);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##theirfilter", "Search these calls", ref _theirFilter, 64);
        ImGui.Spacing();

        var here = phase is { } only
            ? calls.Where(c => c.Phase == only).ToList()
            : calls;

        var quiet = here.Count(c => !c.Speaks);
        if (quiet > 0)
        {
            ImGui.TextColored(Theme.V(Theme.Muted),
                $"{here.Count - quiet} speak, {quiet} keep track without saying anything.");
            ImGui.Spacing();
        }

        var shown = here
            .Where(c => _theirFilter.Trim().Length == 0
                        || c.Id.Contains(_theirFilter, StringComparison.OrdinalIgnoreCase)
                        || c.Line.Contains(_theirFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (shown.Count == 0)
        {
            Widgets.ListBegin();
            Widgets.RowNote($"No call here has \"{_theirFilter.Trim()}\" in it.");
            Widgets.ListEnd();
            return;
        }

        Widgets.ListBegin();
        foreach (var call in shown) DrawTheirCallRow(call);
        Widgets.ListEnd();

        // The same line our own fights carry, off their timeline: two counts side by
        // side and no ratio between them, because a call can cover several entries and
        // a timeline lists things nobody would call.
        var mechanics = runner.ScriptTimelineMechanics(territory);
        if (mechanics > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Muted),
                $"Their timeline lists {mechanics} mechanics for this fight.");
        }
    }

    // Their id is the mechanic's name, so it is the row's name. What it says sits
    // under it, and a call that only keeps track says so rather than reading as a
    // call with its words missing.
    private static void DrawTheirCallRow(ScriptShownCall call)
    {
        var name = call.Id;
        // The fight's own prefix is on every row of the page it is drawn on, so it
        // is dropped: "DMU P3 Black Hole Order" reads as "Black Hole Order" here.
        var parts = name.Split(' ', 3);
        if (parts.Length == 3 && call.Phase.Length > 0) name = parts[2];

        Widgets.RowBegin(name, call.Speaks ? call.Line : "keeps track, says nothing", 0f);
        Widgets.RowEnd();
    }

    // Their phases, read off the ids. Null is every phase, an empty string is the
    // triggers that carry no phase at all.
    private string? DrawTheirPhaseTabs(IReadOnlyList<ScriptShownCall> calls)
    {
        var phases = calls.Select(c => c.Phase).Where(p => p.Length > 0)
            .Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();

        // One phase is not phases, same as our own list: a fight whose triggers are
        // not named by phase gets no tab row at all.
        if (phases.Count < 2) return null;

        string? picked = null;

        if (ImGui.BeginTabBar("##theirphases", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (ImGui.BeginTabItem("All")) ImGui.EndTabItem();

            foreach (var phase in phases)
            {
                var n = calls.Count(c => c.Phase == phase);
                if (ImGui.BeginTabItem($"{phase} ({n})###their{phase}"))
                {
                    picked = phase;
                    ImGui.EndTabItem();
                }
            }

            var loose = calls.Count(c => c.Phase.Length == 0);
            if (loose > 0 && ImGui.BeginTabItem($"Any ({loose})###theirany"))
            {
                picked = "";
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        return picked;
    }
}
