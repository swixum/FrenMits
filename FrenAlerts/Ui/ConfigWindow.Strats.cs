using Dalamud.Bindings.ImGui;
using FrenAlerts.Engine.Scripts;

namespace FrenAlerts.Ui;

// Which way the group runs the mechanics an imported fight has more than one answer
// for.
//
// This is the setting that matters most and shows least. Their fights read the answer
// straight out of their own state, so a fight nobody has set is not quiet: it calls
// the strategy whoever wrote the file happened to run, confidently and on time, at a
// group doing something else. That is worse than no call.
//
// Shown on the fight's own page beside our calls, because it is the same question our
// own fights ask there and somebody setting up a night should find both in one place.
public partial class ConfigWindow
{
    // The zone being stood in, where it is one of theirs and we have no page for it.
    // Shown on the home page so the answer is one click away mid-raid.
    private void DrawScriptStrategiesHere()
    {
        if (Runner is not { Scripted: true } runner) return;

        var here = (ushort)Service.ClientState.TerritoryType;
        if (FightCatalog.All.Any(f => f.TerritoryId == here)) return;

        Widgets.GroupLabel(runner.Fight.Length > 0 ? runner.Fight : "This fight");
        Widgets.ListBegin();
        foreach (var strategy in runner.ScriptStrategiesFor(here)) DrawScriptStrategy(strategy);
        Widgets.ListEnd();
        ImGui.Spacing();
    }

    private void DrawScriptStrategies(ushort territory)
    {
        if (Runner?.ScriptStrategiesFor(territory) is not { Count: > 0 } strategies) return;

        Widgets.GroupLabel("How this fight is called");
        Widgets.ListBegin();

        foreach (var strategy in strategies) DrawScriptStrategy(strategy);

        Widgets.ListEnd();
        ImGui.Spacing();
    }

    private void DrawScriptStrategy(ScriptStrategy strategy)
    {
        // Most of theirs are typed into rather than picked from: a name, a job, a
        // direction. Twenty-three of their twenty-eight settings are this kind, so a
        // page that drew only the dropdowns would hide nearly all of them.
        if (strategy.Options.Count == 0)
        {
            var typed = C.ScriptStratFor(strategy.Id);
            if (Widgets.RowText(strategy.Name, ref typed, $"ss{strategy.Id}",
                width: 190f, changed: Set(strategy)))
            {
                C.SetScriptStrat(strategy.Id, typed, strategy.Default);
            }
            Tip(strategy.Default.Length > 0
                ? $"Takes effect on the next pull. Theirs is \"{strategy.Default}\" when this is empty."
                : "Takes effect on the next pull. Left empty, the fight uses its own answer.");
            return;
        }

        var chosen = C.ScriptStratFor(strategy.Id);
        var current = chosen.Length > 0 ? chosen : strategy.Default;

        var at = 0;
        for (var i = 0; i < strategy.Options.Count; i++)
            if (strategy.Options[i].Value == current) at = i;

        var labels = strategy.Options.Select(o => o.Label).ToArray();

        // Its own id, because two fights can offer a choice with the same name and
        // rows sharing an ImGui id move together.
        if (Widgets.RowCombo(strategy.Name, Set(strategy) ? "your answer" : "their default",
            ref at, labels, width: 190f, changed: Set(strategy), id: $"ss{strategy.Id}"))
        {
            C.SetScriptStrat(strategy.Id, strategy.Options[Math.Clamp(at, 0, labels.Length - 1)].Value,
                strategy.Default);
        }

        // Said in their own words: the option list is theirs, and a call that names a
        // spot only makes sense next to the name of the strat it belongs to.
        Tip($"Takes effect on the next pull. Their default is "
            + $"{strategy.Options.FirstOrDefault(o => o.Value == strategy.Default)?.Label ?? strategy.Default}.");
    }

    private bool Set(ScriptStrategy strategy) => C.ScriptStratFor(strategy.Id).Length > 0;
}
