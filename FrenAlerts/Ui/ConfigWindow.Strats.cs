using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using FrenAlerts.Engine;
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

        Widgets.GroupLabel("Your Strats");
        Widgets.ListBegin();

        foreach (var run in ScriptStrategies.Runs(strategies))
        {
            if (run.Count < FoldAt) { foreach (var s in run) DrawScriptStrategy(s); continue; }
            DrawFoldedRun(run);
        }

        Widgets.ListEnd();
        ImGui.Spacing();
    }

    // A run shorter than this reads better as rows than as something to open.
    private const int FoldAt = 3;

    // One run open at a time, so opening the gaols cannot leave three lists expanded
    // behind it, and cleared with the fight so it dies with the page that set it.
    private string _openRun = "";

    public void CloseStrategyRuns() => _openRun = "";

    // Twenty numbered boxes are one setting to a group and twenty rows to the page.
    //
    // UWU writes out "Titan Gaol Order 1" through "Titan Gaol Order 20" and every one
    // of them is a text box, so the fight's page opened on a wall of them and the
    // calls below were off the bottom of the window.
    private void DrawFoldedRun(IReadOnlyList<ScriptStrategy> run)
    {
        var name = NameOfRun(run);
        var set = run.Count(s => C.ScriptStratFor(s.Id).Length > 0);
        var open = _openRun == name;

        if (Widgets.RowFold(name, $"{run.Count} in order, first is called first", ref open,
            FontAwesomeIcon.ListOl, set > 0 ? Theme.Accent : Theme.Muted,
            note: set > 0 ? $"{set} of {run.Count} set" : "None set"))
            _openRun = open ? name : "";

        if (_openRun != name) return;

        foreach (var s in run)
        {
            var typed = C.ScriptStratFor(s.Id);
            if (Widgets.RowText(ScriptStrategies.NumberOf(s.Name), ref typed, $"ss{s.Id}",
                width: 190f, changed: Set(s), sub: true, placeholder: "job or full name"))
            {
                C.SetScriptStrat(s.Id, typed, s.Default);
            }
        }
        Widgets.RowNote("A job (WAR, sge) or a full name. Blanks skipped, anyone left out goes last.");
    }

    // "Titan Gaol Order 1" and its nineteen neighbours are "Titan Gaol Order".
    private static string NameOfRun(IReadOnlyList<ScriptStrategy> run) =>
        ScriptStrategies.Prefix(run[0].Name) ?? run[0].Name;

    private void DrawScriptStrategy(ScriptStrategy strategy)
    {
        // Most of theirs are typed into rather than picked from: a name, a job, a
        // direction. Twenty-three of their twenty-eight settings are this kind, so a
        // page that drew only the dropdowns would hide nearly all of them.
        if (strategy.Options.Count == 0)
        {
            var (hint, placeholder, notes) = HelpFor(strategy);
            var typed = C.ScriptStratFor(strategy.Id);
            if (Widgets.RowText(strategy.Name, ref typed, $"ss{strategy.Id}",
                width: 190f, changed: Set(strategy), hint: hint,
                placeholder: placeholder.Length > 0 ? placeholder : strategy.Default))
            {
                C.SetScriptStrat(strategy.Id, typed, strategy.Default);
            }
            Tip(strategy.Default.Length > 0
                ? $"Next pull. Blank = \"{strategy.Default}\"."
                : "Next pull. Blank = the fight's own answer.");

            // Written out rather than left in the tooltip: a box wanting one number
            // out of eight is a box nobody can fill in without being told which eight.
            //
            // A line each, because a row draws its text with no wrapping and no
            // clipping: one sentence long enough to hold the answer and all eight
            // seats runs off the side of the panel instead of being cut off there.
            foreach (var note in notes) Widgets.RowNote(note);
            return;
        }

        var chosen = C.ScriptStratFor(strategy.Id);

        // -1 where the saved answer is none of the options they offer now. Handed to the
        // box as it is, so it draws empty rather than drawing the first option as though
        // somebody had picked it.
        var at = ScriptStrategies.OptionAt(strategy, chosen);
        var lost = at < 0;

        var labels = strategy.Options.Select(o => o.Label).ToArray();

        // No second line: the row said "your answer" or "their default" under every
        // name, which is five lines of bookkeeping on a page whose rows already carry
        // a dot when they have been changed and a tooltip saying what the default is.
        //
        // Its own id, because two fights can offer a choice with the same name and
        // rows sharing an ImGui id move together.
        if (Widgets.RowCombo(strategy.Name, "",
            ref at, labels, width: 190f, changed: Set(strategy), id: $"ss{strategy.Id}"))
        {
            C.SetScriptStrat(strategy.Id, strategy.Options[Math.Clamp(at, 0, labels.Length - 1)].Value,
                strategy.Default);
        }

        // Said in their own words: the option list is theirs, and a call that names a
        // spot only makes sense next to the name of the strat it belongs to.
        Tip($"Next pull. Default is "
            + $"{strategy.Options.FirstOrDefault(o => o.Value == strategy.Default)?.Label ?? strategy.Default}.");

        // An empty box is the honest drawing, and on its own it is not an explanation.
        // Said out loud with the answer that went, because the words are the only thing
        // anybody would recognise it by, and left as a row rather than a tooltip: nobody
        // hovers a box that looks like one nothing has been chosen in yet.
        if (lost)
            Widgets.RowNote(chosen.Length > 0
                ? $"Your answer \"{chosen}\" is not one this fight offers any more. Pick one."
                : "This fight's own default is not one of its options. Pick one.");
    }

    private bool Set(ScriptStrategy strategy) => C.ScriptStratFor(strategy.Id).Length > 0;

    // What a typed setting actually wants, for the ones whose answer is a number.
    //
    // Their files carry a comment explaining these and the kit does not read it, so
    // the box arrived with a name and nothing else. UCOB's is the worst of them: it
    // takes one digit, the digit means a tower, and nothing on screen said which
    // towers there were or where the counting starts.
    //
    // Read out of their own code rather than off a guide. `xyToTurnAmount` runs
    // N=0, E=90, S=180, W=270, the eight towers are sorted by it, and the spot is
    // added to Nael's, so counting up is counting clockwise from Nael.
    private static (string Hint, string Placeholder, IReadOnlyList<string> Notes) HelpFor(
        ScriptStrategy strategy)
        => strategy.Id switch
        {
            // The example pairs each seat with its own number, which is party order
            // against 0-7, so it is generated rather than typed: a ninth seat would
            // otherwise leave somebody reading an example that stops at eight.
            //
            // It led with "Example, one each", which reads as a list to fill in. The
            // box takes one number and it is the reader's own, so that is its own line
            // and it comes first.
            // The hint is the short one of the two. It shares its row with the box,
            // which takes 190 of the page's roughly 540, where a note has the row to
            // itself: every other hint in this window is 43 characters or fewer and
            // the middle of them is 18, so what does not fit in that goes below.
            "heavensfallTowerPosition" => (
                "Clockwise from Nael, 0 is Nael's.",
                "0-7, or disabled",
                (IReadOnlyList<string>)
                [
                    "One number, yours. Not the whole list.",
                    "Example: "
                        + string.Join(", ", Audience.Slots.Select((slot, at) => $"{slot} {at}")),
                ]),
            _ => ("", "", []),
        };
}
