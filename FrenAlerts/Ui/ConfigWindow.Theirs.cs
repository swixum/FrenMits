using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using FrenAlerts.Engine.Scripts;

namespace FrenAlerts.Ui;

// The calls the imported set makes, listed on the fight's own page.
//
// Where the imported set covers a fight it is the only thing calling: ours is not
// loaded there at all. A page that listed our calls anyway was describing a hundred
// and seventy calls that never fire, beside a strategy list nobody's answers reach,
// while the fight ran a hundred and sixty of theirs that appeared nowhere.
//
// So a covered fight shows theirs, per mechanic, in their own words. A row is the
// mechanic and the line it leads with; opening it shows every line the mechanic can say,
// each one a box to say it differently.
//
// It used to be one row per trigger with every line the trigger's output table declared
// joined onto it by slashes, which for Mystery Magic was seventeen lines including three
// the mechanic never says, and it could not be clicked. Every fight of theirs read that
// way, and every fight is one of theirs.
public partial class ConfigWindow
{
    private string _theirFilter = "";

    // Which mechanic is open, and the words being typed into it. Held for the open row
    // only: one is open at a time, and a buffer per row would be a copy of every line in
    // the fight kept for the life of the window.
    private string _theirOpen = "";
    private readonly List<string> _theirWords = [];
    private readonly List<string> _theirSpoken = [];
    private bool _theirTtsShown;
    private bool _theirAllLines;

    // How many of a mechanic's lines an open row shows before the rest are folded away.
    //
    // Every line was drawn as its own box. Tele-Portents in Dancing Mad declares 46 of
    // them, Replication 2 in M12S declares 34, and Path of Light Towers 32; 56 of their
    // calls across the eight fights carry more than ten. With the second box switched on
    // that is 92 boxes under one mechanic.
    //
    // swix reported this exact shape once already, on the gaol order list: "Gaolorder
    // 1-20 should be collapsed, its taking up entire page". The strategies list was
    // folded for it and this was not, which is the same fault left standing on the page
    // next door.
    private const int LinesShown = 6;

    // Theirs, and the handful written here that they have no answer for.
    //
    // Ours go in the same list under the same phase tab rather than in a block of
    // their own underneath: a section at the bottom read as a second page stapled on,
    // and what these are is a few more calls for the same fight.
    private void DrawTheirCalls(ushort territory, IReadOnlyList<CallEntry> mine)
    {
        if (Runner is not { } runner) return;

        // Only the ones that say something. The rest keep the fight's own state and
        // are not calls, so a list of calls is not where they belong.
        var calls = runner.ScriptCallsFor(territory).Where(c => c.Speaks).ToList();
        if (calls.Count == 0 && mine.Count == 0)
        {
            Widgets.ListBegin();
            Widgets.RowNote("No calls for this fight yet.");
            Widgets.ListEnd();
            return;
        }

        var phase = DrawTheirPhaseTabs(calls, mine);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##theirfilter", "Search these calls", ref _theirFilter, 64);
        ImGui.Spacing();

        var here = phase is { } only
            ? calls.Where(c => c.Phase == only).ToList()
            : calls;

        // Their phases read "P5"; ours are numbered, so the tab is matched rather
        // than the two lists being grouped apart.
        var mineHere = phase is { } tab
            ? mine.Where(c => $"P{c.Phase}" == tab).ToList()
            : mine;

        // Searched on the mechanic and on every line it can say, so both "towers" and
        // the words a call actually puts on screen find it.
        //
        // Trimmed once, then used for the test, the match and the message alike. They
        // disagreed: the empty test trimmed and the match did not, so a trailing space
        // searched for "towers " and found nothing while the message trimmed it back
        // and said no call here has "towers" in it.
        var find = _theirFilter.Trim();

        // The call's own rule, which is the one the window's search up top uses.
        //
        // This box had a second copy of it that read the shipped lines and not the words
        // somebody put in their place, so renaming a call made it vanish from the box
        // directly above the row still showing the new name: findable by its old words
        // here and by its new ones in the search, with the row reading the new ones. Our
        // half of this list has always matched on the rewording, which is what made the
        // two halves disagree on the same page.
        var shown = here
            .Where(c => find.Length == 0 || TheirsSay(c, find))
            .ToList();

        var mineShown = mineHere
            .Where(c => find.Length == 0 || MineSays(c, find))
            .ToList();

        if (shown.Count == 0 && mineShown.Count == 0)
        {
            Widgets.ListBegin();
            Widgets.RowNote($"No call here has \"{find}\" in it.");
            Widgets.ListEnd();
            return;
        }

        Widgets.ListBegin();
        foreach (var call in shown) DrawTheirCallRow(call);
        foreach (var call in mineShown) DrawCallRow(call);
        Widgets.ListEnd();
    }

    // One mechanic: its name, the line it leads with, and its own editor underneath.
    private void DrawTheirCallRow(ScriptShownCall call)
    {
        var open = _theirOpen == call.Id;
        var edited = EditedIn(call);

        // The count only where it says something the lead line does not. A mechanic with
        // one line is its line, and "1 line" beside it is noise.
        var note = edited > 0 ? $"{call.Lines.Count} lines, {edited} yours"
            : call.Lines.Count > 1 ? $"{call.Lines.Count} lines"
            : "";

        var room = ImGui.GetContentRegionAvail().X
                   - ImGui.CalcTextSize(note).X - Theme.S(56f);

        Widgets.RowBegin(call.Mechanic, Widgets.Elide(Said(call), room), 0f,
            changed: edited > 0, clickable: true,
            icon: open ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight,
            iconCol: Theme.Accent, id: "their" + call.Id, note: note);
        ImGui.Dummy(System.Numerics.Vector2.Zero);
        var clicked = Widgets.RowClicked;
        Widgets.RowEnd();

        if (clicked)
        {
            if (open) Close();
            else OpenTheirCall(call);
        }

        if (_theirOpen == call.Id) DrawTheirCallEditor(call);
    }

    // What the row leads with: their line, or the words somebody put in its place. The
    // row has to read as what the fight will actually say, or a reworded call is only
    // visible from inside the editor that reworded it.
    private string Said(ScriptShownCall call)
    {
        var lead = call.Lines.FirstOrDefault(l => !l.FillsIn) ?? call.Lines.FirstOrDefault();

        // A call whose words are built inside a function as it fires, so there is nothing
        // to read before it runs. Said out loud, because a name with an empty line under
        // it reads as a call whose words went missing.
        //
        // Eleven of their 413 speaking calls, measured: M9S Coffinfiller and its ability,
        // the four M12S Mortal Slayer tank sides, and five in Enuo. It used to be most of
        // the authored fights, until the kit started declaring written-down text as an
        // output string, which left only the ones that genuinely read the pull.
        if (lead is null) return Unreadable;

        var mine = TheirEdit(call.Id, lead);
        return mine is { Text.Length: > 0 } ? mine.Text : lead.Text;
    }

    private const string Unreadable = "Built live";

    // How many of a mechanic's lines have been given other words.
    private int EditedIn(ScriptShownCall call) =>
        call.Lines.Count(l => TheirEdit(call.Id, l) is { IsDefault: false });

    // A line's rewording, read off its first key: the page shows keys that ship the same
    // words as one line and writes the same words to all of them, so any one of them is
    // the answer for the line.
    private ScriptCallEdit? TheirEdit(string trigger, ScriptShownLine line) =>
        line.Keys.Count == 0 ? null : C.ScriptEditFor(trigger, line.Keys[0]);

    private void OpenTheirCall(ScriptShownCall call)
    {
        _theirOpen = call.Id;
        _theirWords.Clear();
        _theirSpoken.Clear();
        _theirTtsShown = false;
        _theirAllLines = false;

        for (var i = 0; i < call.Lines.Count; i++)
        {
            var mine = TheirEdit(call.Id, call.Lines[i]);
            _theirWords.Add(mine?.Text ?? "");
            _theirSpoken.Add(mine?.Tts ?? "");
            // Opened showing the second box where there is already something in it,
            // rather than hiding a line somebody set and then cannot find.
            if (mine is { Tts.Length: > 0 }) _theirTtsShown = true;
            // And opened unfolded where one of the folded lines is somebody's own. The
            // row counts every reworded line in "3 yours", so folding one away leaves a
            // count nothing on screen accounts for.
            if (i >= LinesShown && mine is { IsDefault: false }) _theirAllLines = true;
        }
    }

    private void Close()
    {
        _theirOpen = "";
        _theirWords.Clear();
        _theirSpoken.Clear();
        _theirTtsShown = false;
        _theirAllLines = false;
    }

    // Every line the mechanic can say, each one a box to say it differently.
    //
    // Their words are the placeholder rather than the contents, so an empty box means
    // theirs and there is no way to end up storing their own line back as an edit of
    // itself. The label is what they ship, so the row reads as "this line, said like
    // this".
    private void DrawTheirCallEditor(ScriptShownCall call)
    {
        if (call.Lines.Count == 0)
        {
            Widgets.RowNote("Built live, nothing to reword.");
            return;
        }

        // Both buffers walk with the lines, so the shortest of the three is the bound: the
        // list is cached per zone and cannot change while a row is open, and a guard that
        // trusted that is a guard that throws on the frame it stops being true.
        var lines = Math.Min(call.Lines.Count, Math.Min(_theirWords.Count, _theirSpoken.Count));
        var upTo = _theirAllLines ? lines : Math.Min(lines, LinesShown);

        for (var i = 0; i < upTo; i++)
        {
            var line = call.Lines[i];
            var mine = TheirEdit(call.Id, line);

            if (line.Keys.Count == 0)
            {
                // Read without keys, so their hook has nothing to hang a rewording on.
                //
                // No shipped fight reaches this any more: all 1988 lines across the eight
                // arrive keyed since the listing started reading the trigger's own table
                // instead of dropping the keys off it. Kept because a line with no key is
                // still a line, and showing it beats showing nothing.
                Widgets.RowBegin(line.Text, "", 0f, sub: true);
                ImGui.Dummy(System.Numerics.Vector2.Zero);
                Widgets.RowEnd();
                continue;
            }

            var words = _theirWords[i];
            if (Widgets.RowText(Label(line), ref words, "theirw" + call.Id + line.Keys[0],
                    width: 260f, changed: mine?.Text.Length > 0, sub: true,
                    placeholder: line.Text, max: 192))
            {
                _theirWords[i] = words;
                C.SetScriptEdit(call.Id, line.Keys, words, _theirSpoken[i]);
                Runner?.ScriptWordsChanged();
            }
            if (line.FillsIn) Tip("Keep the ${...} bits, the fight fills them in.");
        }

        DrawTheirLineFold(call, lines);

        var tts = _theirTtsShown;
        if (Widgets.RowCheckClick("Different TTS words", "", ref tts,
                id: "theirtts" + call.Id, changed: _theirSpoken.Any(s => s.Length > 0)))
            _theirTtsShown = tts;
        Tip("Off = TTS says what is on screen.");

        // The same bound, or switching the second box on rebuilds the wall the fold just
        // took down: these are a box per line as well.
        if (_theirTtsShown)
            for (var i = 0; i < upTo; i++)
            {
                var line = call.Lines[i];
                if (line.Keys.Count == 0) continue;

                var spoken = _theirSpoken[i];
                if (Widgets.RowText("Reads out", ref spoken, "theirs" + call.Id + line.Keys[0],
                        width: 260f, changed: line.Keys.Count > 0 && spoken.Length > 0, sub: true,
                        placeholder: _theirWords[i].Length > 0 ? _theirWords[i] : line.Text,
                        max: 192))
                {
                    _theirSpoken[i] = spoken;
                    C.SetScriptEdit(call.Id, line.Keys, _theirWords[i], spoken);
                    Runner?.ScriptWordsChanged();
                }
            }

        if (EditedIn(call) == 0) return;

        Widgets.RowBegin("", "", IconBtnWidth(FontAwesomeIcon.Undo, "Back to default"), sub: true);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, "Back to default"))
        {
            C.ClearScriptEdits([call.Id]);
            Runner?.ScriptWordsChanged();
            OpenTheirCall(call);
        }
        Widgets.RowEnd();
    }

    // The fold under a mechanic that has more lines than a row should open with.
    //
    // Drawn like the mechanic row above it rather than with the strategy list's fold
    // widget: that one has no indented form, and a flush row in the middle of a run of
    // sub rows reads as the start of a new section rather than as part of this one.
    private void DrawTheirLineFold(ScriptShownCall call, int lines)
    {
        var hidden = lines - LinesShown;
        if (hidden <= 0) return;

        var open = _theirAllLines;

        Widgets.RowBegin(open ? "Fewer lines" : $"{hidden} more line{(hidden == 1 ? "" : "s")}",
            "", 0f, sub: true, clickable: true,
            icon: open ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight,
            iconCol: Theme.Accent, id: "theirmore" + call.Id);
        ImGui.Dummy(System.Numerics.Vector2.Zero);
        var clicked = Widgets.RowClicked;
        Widgets.RowEnd();

        if (clicked) _theirAllLines = !_theirAllLines;
    }

    // Their line, as the label on the box that replaces it. Elided against the room the
    // box leaves, so a long direction call cannot push the box off the row.
    private static string Label(ScriptShownLine line) =>
        Widgets.Elide(line.Text,
            ImGui.GetContentRegionAvail().X - Theme.S(260f) - Theme.S(52f));

    // Their phases, read off the ids. Null is every phase, an empty string is the
    // triggers that carry no phase at all.
    private string? DrawTheirPhaseTabs(
        IReadOnlyList<ScriptShownCall> calls, IReadOnlyList<CallEntry> mine)
    {
        // Ours are numbered and theirs are named "P5", so a phase is counted in the
        // form the tabs use. A tab counting one side of a list it shows both sides of
        // is the same quiet wrong as the heading that counted theirs alone.
        int Ours(string phase) => mine.Count(c => $"P{c.Phase}" == phase);

        var phases = calls.Select(c => c.Phase).Where(p => p.Length > 0)
            .Concat(mine.Select(c => $"P{c.Phase}"))
            .Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();

        // One phase is not phases, same as our own list: a fight whose triggers are
        // not named by phase gets no tab row at all.
        if (phases.Count < 2) return null;

        string? picked = null;

        if (ImGui.BeginTabBar("##theirphases", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (ImGui.BeginTabItem("All", AllTabFlag())) ImGui.EndTabItem();

            foreach (var phase in phases)
            {
                var n = calls.Count(c => c.Phase == phase) + Ours(phase);
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
