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

    private void DrawTheirCalls(ushort territory)
    {
        if (Runner is not { } runner) return;

        // Only the ones that say something. The rest keep the fight's own state and
        // are not calls, so a list of calls is not where they belong.
        var calls = runner.ScriptCallsFor(territory).Where(c => c.Speaks).ToList();
        if (calls.Count == 0)
        {
            Widgets.ListBegin();
            Widgets.RowNote("No calls for this fight yet.");
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

        // Searched on the mechanic and on every line it can say, so both "towers" and
        // the words a call actually puts on screen find it.
        //
        // Trimmed once, then used for the test, the match and the message alike. They
        // disagreed: the empty test trimmed and the match did not, so a trailing space
        // searched for "towers " and found nothing while the message trimmed it back
        // and said no call here has "towers" in it.
        var find = _theirFilter.Trim();

        var shown = here
            .Where(c => find.Length == 0
                        || c.Mechanic.Contains(find, StringComparison.OrdinalIgnoreCase)
                        || c.Lines.Any(l => l.Text.Contains(find, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (shown.Count == 0)
        {
            Widgets.ListBegin();
            Widgets.RowNote($"No call here has \"{find}\" in it.");
            Widgets.ListEnd();
            return;
        }

        Widgets.ListBegin();
        foreach (var call in shown) DrawTheirCallRow(call);
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
        // The savage fights and Enuo are written through the authoring layer rather than
        // with an output table, so their words are built inside a function as the call
        // fires and there is nothing to read here. Said out loud, because a name with an
        // empty line under it reads as a call whose words went missing.
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

        foreach (var line in call.Lines)
        {
            var mine = TheirEdit(call.Id, line);
            _theirWords.Add(mine?.Text ?? "");
            _theirSpoken.Add(mine?.Tts ?? "");
            // Opened showing the second box where there is already something in it,
            // rather than hiding a line somebody set and then cannot find.
            if (mine is { Tts.Length: > 0 }) _theirTtsShown = true;
        }
    }

    private void Close()
    {
        _theirOpen = "";
        _theirWords.Clear();
        _theirSpoken.Clear();
        _theirTtsShown = false;
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

        for (var i = 0; i < lines; i++)
        {
            var line = call.Lines[i];
            var mine = TheirEdit(call.Id, line);

            if (line.Keys.Count == 0)
            {
                // Read without keys, so their hook has nothing to hang a rewording on.
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

        var tts = _theirTtsShown;
        if (Widgets.RowCheckClick("Different TTS words", "", ref tts,
                id: "theirtts" + call.Id, changed: _theirSpoken.Any(s => s.Length > 0)))
            _theirTtsShown = tts;
        Tip("Off = TTS says what is on screen.");

        if (_theirTtsShown)
            for (var i = 0; i < lines; i++)
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

    // Their line, as the label on the box that replaces it. Elided against the room the
    // box leaves, so a long direction call cannot push the box off the row.
    private static string Label(ScriptShownLine line) =>
        Widgets.Elide(line.Text,
            ImGui.GetContentRegionAvail().X - Theme.S(260f) - Theme.S(52f));

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
