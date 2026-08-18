using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using FrenAlerts.Engine;
using FrenAlerts.Engine.Alerts;

namespace FrenAlerts.Ui;

public partial class ConfigWindow
{
    private void DrawHomePage()
    {
        var muted = Theme.V(Theme.Muted);

        var frameH = ImGui.GetFrameHeight();
        var headStart = ImGui.GetCursorPos();
        if (Icons.Logo() is { } logo)
        {
            var lsz = ImGui.GetFrameHeight();
            ImGui.Image(logo.Handle, new Vector2(lsz, lsz));
            ImGui.SameLine(0, Theme.S(9f));
        }
        var (headH, headEnd) = PageTitle("Fren Alerts");

        ImGui.SetCursorPos(new Vector2(headEnd + Theme.S(12f),
            headStart.Y + (headH - ImGui.GetTextLineHeight()) * 0.5f));
        ImGui.TextColored(muted, $"v{Version}");
        var used = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;

        var ghW = IconBtnWidth(FontAwesomeIcon.ExternalLinkAlt, "GitHub");
        ImGui.SetCursorPos(new Vector2(
            MathF.Max(used + Theme.S(12f), ImGui.GetContentRegionMax().X - ghW),
            headStart.Y + (headH - frameH) * 0.5f));
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, "GitHub"))
            Dalamud.Utility.Util.OpenLink("https://github.com/swixum/FrenMits");
        ImGui.SetCursorPos(new Vector2(headStart.X, headStart.Y + headH));

        ImGui.Spacing();
        DrawHomeTiles();
        ImGui.Spacing();

        // The fight being stood in right now, where it is one of theirs. Here as well
        // as on the fight's own page, because half of their zones have no page of ours
        // at all and the answer would otherwise be unreachable from inside the duty.
        DrawScriptStrategiesHere();

        if (FightCatalog.All.Count == 0)
        {
            Widgets.ListBegin();
            Widgets.RowNote("1. Tick Test, then drag the call where you want it");
            Widgets.RowNote("2. Set the size and colors on Call Display");
            Widgets.RowNote("3. Fights land here as they are built");
            Widgets.ListEnd();
            ImGui.Spacing();
        }

        Widgets.ListBegin();

        var test = C.TestMode;
        if (Widgets.RowCheckClick("Test Mode", "", ref test,
            FontAwesomeIcon.ArrowsAlt, Theme.Accent))
        { C.TestMode = test; C.Save(); }
        Tip("Sample call on screen. Drag it where you want it.");

        if (!C.AlertsEnabled)
        {
            var on = C.AlertsEnabled;
            if (Widgets.RowCheckClick("Turn calls back on", "", ref on,
                FontAwesomeIcon.Bell, Theme.Warn))
            { C.AlertsEnabled = on; C.Save(); }
        }

        if (Widgets.RowDoor("Call Display", "", FontAwesomeIcon.Desktop, Theme.Accent))
            _nav = NavKind.CallDisplay;

        if (Widgets.RowDoor("Appearance", "", FontAwesomeIcon.Palette, Theme.Accent))
            _nav = NavKind.Appearance;

        DrawRecordRow();

        Widgets.ListEnd();
    }

    // Switching the recorder on from the window rather than the chat command.
    //
    // The command came first because the recorder was built to answer one question
    // in one replay. It is meant to be used mid-replay by somebody who is watching
    // the fight, and typing an exact subcommand is the wrong thing to ask for at
    // that moment.
    private void DrawRecordRow()
    {
        // Never on a machine that has not asked for it. The recorder writes a file
        // to disk, and a debug surface belongs to whoever went looking for it, not
        // on the front page of everybody's install.
        if (!C.Diagnostics) return;
        if (Runner is not { } run) return;

        var on = run.Diary.On;
        if (Widgets.RowCheckClick("Record this pull", "", ref on,
            FontAwesomeIcon.FileAlt, Theme.Warn,
            note: run.Diary.On
                ? run.Diary.Full ? "full" : $"{run.Diary.Lines} lines"
                : ""))
        {
            if (on) run.OpenDiary();
            else { run.WriteDiary(); run.CloseDiary(); }

            // The same thing the chat command does, for the same reason. Without it
            // this switch is the one control a reload undoes: switched off here it
            // came back on by itself, and switched on here it stopped by itself
            // mid-replay. The window is the path used during a replay, so it is the
            // one that has to survive one.
            C.KeepRecording = on;
            C.Save();
        }
        Tip("Writes what every call actually did to pulls.log, one section per pull.\n"
            + "Off by default, and it changes nothing about what gets called.\n"
            + "Left on, it comes back on after a reload.");

        // Read off the runner rather than held here, because a pull writes itself
        // out as it ends. Kept locally, this row only ever appeared after somebody
        // switched the recorder off by hand, which through a whole replay is never.
        if (run.LastRecording.Length > 0
            && Widgets.RowDoor("Open the folder", "", FontAwesomeIcon.FolderOpen,
                Theme.Accent, note: Game.DiaryFile.Name))
            OpenConfigFolder();
    }

    private void DrawHomeTiles()
    {
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var w = (ImGui.GetContentRegionAvail().X - gap) * 0.5f;
        var h = ImGui.GetTextLineHeightWithSpacing() * 3f + Theme.S(9f) * 2f;

        var fights = FightCatalog.All.Count;
        var live = _board.Live().Count;

        var problems = new List<string>();
        if (!C.AlertsEnabled) problems.Add("Calls are off");
        if (fights == 0) problems.Add("No fights built yet");
        if (FightCatalog.PackProblem is not null) problems.Add("The call pack did not load");
        if (MutedHere) problems.Add("This fight is off");
        // Only for the fight you are standing in: a strat you have not picked for a
        // fight you are not in is next week's job, not something needing a look.
        if (StratsOff(Service.ClientState.TerritoryType) is > 0 and var unset)
            problems.Add($"{unset} strat{(unset == 1 ? "" : "s")} not set here");
        if (Runner is { ControlAvailable: false }) problems.Add("No direction calls");
        // Covered, not available: a reading parser answers hits and the hook stands
        // down while it does, so the address alone would call them dead mid-pull.
        if (Runner is { HitsCovered: false }) problems.Add("No hit calls");
        if (Runner is { ParserDropped: > 0 } fed)
            problems.Add($"The feed dropped {fed.ParserDropped}");
        // Only where it costs something: a fight with no prop calls does not care
        // that the arena is unread.
        if (ArenaCallsHere() is > 0 and var waiting)
            problems.Add($"{waiting} call{(waiting == 1 ? "" : "s")} here need the arena read");
        // Well into a pull with a timeline that still has not placed itself. Every
        // countdown is missing and nothing else would say so.
        if (Runner is { InPull: true, HasTimeline: true, TimelineRunning: false } late
            && late.PullSeconds > TimelineGrace)
            problems.Add("The timeline has not anchored");
        if (Runner is { LocalVoice.GivenUp: true }) problems.Add("Local voice gave up");
        // The parser is not on this list in any state, because no state of it costs a
        // call. Every kind has a client route now, head markers and tethers included,
        // and LiveCoverage.NeedsAParser is empty to say so.
        //
        // "The parser never answered" was here and could not be got rid of. It is the
        // bridge's give-up state, which is terminal: Tick returns on !Asking, so after
        // MaxAsks nothing asks again, and the one path that clears it wants a line the
        // failed subscribe was what would have sent. It sat on the home page for the
        // rest of the session naming something that was costing nothing, and there is
        // no parser page to send anybody to about it either.
        if (C.TestMode) problems.Add("Test mode is on");
        // Same reason as test mode: not a fault, but it writes to disk and it is
        // meant to be switched off again once the question it was asked is answered.
        if (Runner is { Diary.On: true } rec)
            problems.Add(rec.Diary.Full ? "The recording is full" : "Recording is on");

        if (HomeTile("##t1", w, h, FontAwesomeIcon.Bell,
            Theme.V(C.AlertsEnabled ? Theme.Good : Theme.Muted),
            "Calls",
            C.AlertsEnabled ? "Running" : "Off",
            Theme.V(C.AlertsEnabled ? Theme.Good : Theme.Muted),
            UnwrittenHere() is { } unwritten ? $"No calls for {unwritten} yet"
            : fights == 0 ? "Nothing to call yet"
            : $"{fights} fight{(fights == 1 ? "" : "s")} loaded"))
            _nav = NavKind.CallDisplay;
        ImGui.SameLine();
        if (HomeTile("##t2", w, h, FontAwesomeIcon.Desktop, Theme.V(Theme.Accent),
            "On Screen",
            live > 0 ? $"{live} showing" : C.TestMode ? "Sample call" : "Nothing",
            Theme.V(Theme.TextBright),
            $"Text at {C.CallFontSizePx:0}px"))
            _nav = NavKind.CallDisplay;

        if (HomeTile("##t3", w, h, FontAwesomeIcon.VolumeUp,
            Theme.V(C.VoiceEnabled ? Theme.Good : Theme.Muted),
            "TTS",
            C.VoiceEnabled ? "On" : "Off",
            Theme.V(C.VoiceEnabled ? Theme.Good : Theme.Muted),
            VoiceLine())) _nav = NavKind.Tts;
        ImGui.SameLine();
        if (HomeTile("##t4", w, h,
            problems.Count == 0 ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.ExclamationTriangle,
            Theme.V(problems.Count == 0 ? Theme.Good : Theme.Warn),
            "Needs a Look",
            problems.Count == 0 ? "All Good" : problems[0],
            Theme.V(problems.Count == 0 ? Theme.Good : Theme.Warn),
            problems.Count > 1 ? string.Join(", ", problems.Skip(1)) : ""))
            _nav = problems.Count == 0 ? NavKind.Home : PageFor(problems[0]);
        Tip(problems.Count == 0 ? "Nothing needs attention."
            : NoPageFixes(problems[0]) is { Length: > 0 } why ? why
            : "Go and fix the first one.");
    }

    // What a problem means, where there is no page to go and fix it on.
    //
    // "Go and fix the first one" is a promise the tile has to keep. It could, until the
    // sidebar's Connection block stopped reading out anything but the parser: these
    // three now land on Home with nothing on it about them, and a tooltip telling
    // somebody to go and fix it is the dead tile all over again, one level down.
    //
    // So they say what they are instead. Every one is a hook that did not install,
    // which happens when a game patch moves something, and the answer is a plugin
    // update rather than a setting. Saying that costs nothing and beats sending
    // somebody looking for a switch that was never there.
    private static string NoPageFixes(string problem) => problem switch
    {
        "No direction calls" => "Left, right and compass calls cannot fire. "
            + "A game patch moved something; it needs a plugin update, not a setting.",
        "No hit calls" => "Tank busters and hit counts cannot fire. "
            + "A game patch moved something; it needs a plugin update, not a setting.",
        _ when problem.StartsWith("The feed dropped ", StringComparison.Ordinal) =>
            "The parser sent more than the queue could hold. Calls in that burst were "
            + "missed. Nothing to set; it catches up on its own.",
        _ => "",
    };

    // Where to go to answer a problem, which has to be the page the switch is
    // actually on. Sending somebody to a page that does not hold the thing reads as
    // a dead tile, and that is what it was doing: test mode went to Call Display and
    // the recorder fell through to Fights, while both are switched on the home page
    // itself, in the list directly under these tiles.
    private static ConfigWindow.NavKind PageFor(string problem) => problem switch
    {
        "Calls are off" => NavKind.CallDisplay,
        "Test mode is on" => NavKind.Home,
        "Recording is on" => NavKind.Home,
        "The recording is full" => NavKind.Home,
        "Local voice gave up" => NavKind.Tts,
        // What is feeding the calls. These fell through to Fights, which is a list of
        // fights and says nothing about a feed, so they went to Home when the sidebar's
        // Connection block still read out Hits, Facing and the dropped count.
        //
        // It does not any more: swix asked for those three rows gone, and the block now
        // answers whether the parser is feeding and nothing else. So Home no longer
        // explains any of these either, and none of them has a page anywhere.
        //
        // They stay on Home and stay on the list. Every one of them means a hook that
        // did not install, which is a game patch having moved something and a whole kind
        // of call not firing until the plugin is rebuilt. That is worth saying and there
        // is nothing to click; what changed is the tooltip, which no longer promises a
        // page to go and fix it on. See NoPageFixes.
        "No direction calls" => NavKind.Home,
        "No hit calls" => NavKind.Home,
        _ when problem.StartsWith("The feed dropped ", StringComparison.Ordinal) => NavKind.Home,
        _ => NavKind.Fights,
    };

    // Drawn by hand rather than as a child window, so the whole tile is one
    // click target and can show a hover state.
    private static bool HomeTile(string id, float w, float h, FontAwesomeIcon icon, Vector4 iconCol,
        string label, string line, Vector4 lineCol, string sub)
    {
        var p = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, new Vector2(w, h));
        var hot = ImGui.IsItemHovered();
        if (hot) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + new Vector2(w, h), hot ? 0xFF28171Eu : Theme.PanelBg, Theme.S(8f));
        dl.AddRect(p, p + new Vector2(w, h), hot ? Theme.Accent : Widgets.CardBorder, Theme.S(8f));

        var pad = Theme.S(9f);
        var lineH = ImGui.GetTextLineHeightWithSpacing();
        var room = w - pad * 2f;

        // Status-tinted icon in the corner, echoing the sidebar's.
        float iconW;
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var ic = icon.ToIconString();
            iconW = ImGui.CalcTextSize(ic).X;
            dl.AddText(p + new Vector2(w - pad - iconW, pad), Widgets.ToColor(iconCol), ic);
        }

        dl.AddText(p + new Vector2(pad, pad), Theme.Muted,
            Widgets.Elide(label, room - iconW - Theme.S(6f)));
        dl.AddText(p + new Vector2(pad, pad + lineH), Widgets.ToColor(lineCol), Widgets.Elide(line, room));
        if (sub.Length > 0)
            dl.AddText(p + new Vector2(pad, pad + lineH * 2f), Theme.Muted, Widgets.Elide(sub, room));
        return clicked;
    }

    // ---- fights ----

    private void DrawFightCategoryPage(string category)
    {
        var n = FightCatalog.CountIn(category);
        PageHead(category, $"{n} fight{(n == 1 ? "" : "s")}", false, hasMaster: false,
            reset: () => ResetPage(NavKind.Fights), icon: CategoryIcon(category));

        // A short list because the shipped calls did not load is worth saying, or
        // it reads as a plugin that only knows three fights.
        if (FightCatalog.PackProblem is { } problem)
        {
            ImGui.TextColored(Theme.V(Theme.Warn), problem);
            ImGui.Spacing();
        }

        var fights = FightCatalog.In(category).ToList();
        if (fights.Count == 0)
        {
            Widgets.ListBegin();
            Widgets.RowNote($"No {category.ToLowerInvariant()} fight is built yet.");
            Widgets.ListEnd();
            return;
        }

        Widgets.ListBegin();
        var all = C.AllCallsOn;
        if (Widgets.RowCheckClick("Call everything", "", ref all,
            FontAwesomeIcon.Bullhorn, Theme.Accent,
            changed: Changed(nameof(Configuration.AllCallsOn)),
            note: all ? "" : "Only the exact ones"))
        { C.AllCallsOn = all; C.Save(); }
        Tip(all
            ? "Everything calls."
            : "Only the exact ones. Turn the rest on per fight.");
        Widgets.ListEnd();
        ImGui.Spacing();

        // Under its expansion, newest at the top. The list was one long run in
        // territory order, which reads as one pile the moment a second expansion has
        // fights in it: UCOB and UWU sat above Dancing Mad because their zone numbers
        // are lower, so the oldest fights in the game led the page.
        //
        // The heading stands over every group, including a page whose fights are all
        // from one expansion. Savage is four Dawntrail fights and used to draw as one
        // unlabeled card, so the two pages read as different windows.
        var byExpansion = fights
            .GroupBy(f => f.Expansion)
            .OrderBy(g => Shipped.ExpansionRank(g.Key))
            .ToList();

        // A card per expansion, with the heading above it rather than inside it.
        //
        // One card holding every group read as a list interrupted twice. The heading sat
        // hard against the card's left edge while every row under it was indented past
        // that, and the divider a row draws above itself landed between the heading and
        // the first fight it names. Every other heading on this page stands over its own
        // card, so the fights read like the rest of the window now.
        for (var g = 0; g < byExpansion.Count; g++)
        {
            var group = byExpansion[g];
            ExpansionLabel(group.Key.Length > 0 ? group.Key : "Other");

            Widgets.ListBegin();
            foreach (var f in group)
            {
                var off = C.IsMuted(f.TerritoryId);
                var edits = EditedIn(f);
                var shown = CallsShownIn(f);
                var note = off ? "Off"
                    : edits > 0 ? $"{shown} calls, {edits} edited"
                    : $"{shown} call{(shown == 1 ? "" : "s")}";
                if (Widgets.RowDoor(f.Name, "", CategoryIcon(category),
                    off ? Theme.Muted : Theme.Accent, note: note,
                    noteCol: off ? Theme.Warn : 0u))
                    OpenFight(f);
                if (f.Full.Length > 0) Tip(f.Full);
            }
            Widgets.ListEnd();

            // Between the cards only. A gap under the last one is the page's own bottom
            // edge moving, not spacing anybody asked for.
            if (g < byExpansion.Count - 1) ImGui.Spacing();
        }
    }

    // The expansion over its own card, in the accent and in bold, at the spacing every
    // other heading uses.
    //
    // The accent rather than a color of its own, so the headings follow whatever is set
    // in Preset Accents and the page never carries a hue nobody chose. It is the same
    // color the fight icons under it are drawn in.
    //
    // The weight is painted rather than pushed. The plugin builds the default font at
    // the sizes it needs and there is no bold cut of it to push, so it is the same
    // glyphs drawn twice a fraction apart. Half a pixel reads as bold at this size; a
    // whole one smears.
    private static void ExpansionLabel(string text)
    {
        var words = text.ToUpperInvariant();

        ImGui.Dummy(new Vector2(0, Theme.S(6f)));
        var at = ImGui.GetCursorScreenPos();
        ImGui.TextColored(Theme.V(Theme.Accent), words);
        ImGui.GetWindowDrawList().AddText(at + new Vector2(Theme.S(0.6f), 0f), Theme.Accent, words);
        ImGui.Dummy(new Vector2(0, Theme.S(1f)));
    }

    // ---- one fight's calls ----

    // Opened with what was being looked for, where something was.
    //
    // A fight opened from a search hit arrives filtered to the words that found it. Its
    // list can run to a hundred and sixteen rows, so landing at the top of one with the
    // call opened somewhere below the fold is a page that looks like nothing happened.
    //
    // Safe to filter by because the page's box and the window's search now ask the call
    // the same question, so the row that was picked is always one of the ones left.
    private void OpenFight(FightEntry f, string filter = "")
    {
        _navFightId = f.TerritoryId;
        _nav = NavKind.Fight;
        _openCall = "";
        _callFilter = filter;
        // Theirs too, or the row left open in the last fight is still open, holding the
        // words that were being typed into a mechanic this fight has never heard of.
        Close();
        _theirFilter = filter;
        // Same reason: a run left open in the last fight is a wall of boxes at the top
        // of this one, belonging to a mechanic it has never heard of.
        CloseStrategyRuns();

        // And back to All, or the filter is read through whichever phase tab was left
        // selected on the last fight. The tabs are keyed by the phase's own name, so P5
        // stays picked from one fight to the next, and a hit in P1 would arrive already
        // hidden by a tab nobody touched.
        _backToAllPhases = filter.Length > 0;
    }

    // Set on arriving from a search, read once by whichever tab row draws next.
    private bool _backToAllPhases;

    // The flag for the All tab, taken as it is read so it cannot re-select the tab on
    // every frame and pin somebody out of the rest of them.
    private ImGuiTabItemFlags AllTabFlag()
    {
        if (!_backToAllPhases) return ImGuiTabItemFlags.None;
        _backToAllPhases = false;
        return ImGuiTabItemFlags.SetSelected;
    }

    private void OpenCall(CallEntry call)
    {
        _openCall = call.Key;
        // Opened as it is drawn, so the box and the row agree.
        _callWords = CallText.Sentence(C.EditFor(call.Key)?.Text ?? call.Text);
    }

    // What the fight page will actually list, so the row that opens it agrees with it.
    //
    // A covered fight's page shows the imported set plus the handful opted in here,
    // and the catalog's own total counts the pack as well, which that page has never
    // drawn. Left alone the list said 157 calls over a page headed 116.
    //
    // Both sides of this are cached lookups: their list is built once at load and
    // held per zone, and the catalog's is rebuilt only when the pack or the player's
    // seat changes.
    private bool Covered(FightEntry f) => Runner?.ScriptCovers((ushort)f.TerritoryId) == true;

    // Theirs alone where their fight owns the zone, because ours is not built there:
    // FightLoader.Build hands back an empty engine before it reaches the module. Ours
    // were added to this number when the page started listing them, and both were
    // wrong the same way, so both come out together.
    private int CallsShownIn(FightEntry f) =>
        Covered(f)
            ? Runner?.ScriptCallsFor((ushort)f.TerritoryId).Count(c => c.Speaks) ?? 0
            : f.Calls;

    // The same rule for the reworded number beside it: a row nobody can open, or one
    // that cannot fire, is a row nobody can have reworded.
    private int EditedIn(FightEntry f) =>
        Covered(f)
            ? Runner?.ScriptCallsFor((ushort)f.TerritoryId).Where(c => c.Speaks).Sum(EditedIn) ?? 0
            : FightCatalog.CallsIn(f.TerritoryId).Count(c => C.IsEdited(c.Key));

    // What the three levels are called on screen. The engine's own enum still reads
    // Info/Alert/Alarm and the pack stores it as a number, so this is the one place
    // the words live.
    private static readonly string[] SeverityNames = { "Info", "Warning", "Danger" };

    private static string SeverityName(CallLevel level) => SeverityNames[(int)level];

    private void DrawFightPage()
    {
        // Looked up fresh, so a fight that left the list when the pack reloaded
        // sends the page back rather than drawing from a stale copy.
        if (FightCatalog.All.FirstOrDefault(f => f.TerritoryId == _navFightId) is not { } fight)
        {
            _nav = NavKind.Fights;
            return;
        }

        // Back to the list this fight was opened from.
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ArrowLeft, fight.Category))
        {
            _navCategory = fight.Category;
            _nav = NavKind.Fights;
            return;
        }
        ImGui.Spacing();

        // Where the imported set covers this fight, ours is not loaded at all, so
        // every switch below this line is describing calls that are not running.
        // Said out loud, because a page full of controls that change nothing is the
        // worst kind of quiet.
        // Asked about the zone rather than about where the player is standing: the
        // answer is the same from a hub, and this page is read between pulls.
        //
        // Asked before the heading, because the heading counts the calls the page is
        // about to list. It counted ours either way, so a covered fight was headed "170
        // calls" over a list of a hundred and sixteen of theirs.
        var theirs = Runner?.ScriptCovers((ushort)fight.TerritoryId) == true;

        var calls = FightCatalog.CallsIn(fight.TerritoryId);
        var their = theirs
            ? Runner?.ScriptCallsFor((ushort)fight.TerritoryId).Where(c => c.Speaks).ToList() ?? []
            : [];

        // Only theirs on a fight of theirs, because only theirs can speak there.
        //
        // FightLoader.Build returns an empty engine for a covered zone: not the module,
        // not the pack, not the plan. So the forty hand-written Dancing Mad calls this
        // page used to list beside theirs were forty rows that cannot fire, switchable
        // and rewordable and silent, and the heading counted them.
        //
        // They were added here in the belief that both engines run. Nothing on any
        // screen said otherwise, which is how an evening went into fixing calls that
        // were never going to be heard and then deleting the ones that were.
        var listed = new List<CallEntry>();

        var total = theirs ? their.Count : fight.Calls;
        var edited = theirs
            ? their.Sum(EditedIn)
            : calls.Count(c => C.IsEdited(c.Key));

        var on = !C.IsMuted(fight.TerritoryId);
        if (PageHead(fight.Name, edited > 0 ? $"{total} calls, {edited} reworded"
                : $"{total} call{(total == 1 ? "" : "s")}", on,
                reset: () =>
                {
                    C.ClearEdits(calls.Select(c => c.Key));
                    C.ClearScriptEdits(their.Select(c => c.Id));
                    Runner?.ScriptWordsChanged();
                    if (C.MutedTerritories.Remove(fight.TerritoryId)) C.Save();
                    // Back to defaults means the strats too, or the button half
                    // undoes the page and leaves the group's answers behind.
                    //
                    // Both stores, and asked the same way the block above draws them.
                    // Only ours were put back, and on a fight the imported set covers
                    // every row on that block belongs to the other one: the button
                    // cleared keys that have no row and left every answer on screen
                    // sitting there. Dancing Mad's whole strat list survived its own
                    // reset.
                    foreach (var s in Strategies.For((ushort)fight.TerritoryId))
                        C.SetStrat((ushort)fight.TerritoryId, s.Key, s.Default);
                    C.ClearScriptStrats(
                        (Runner?.ScriptStrategiesFor((ushort)fight.TerritoryId) ?? [])
                        .Select(s => s.Id));
                    _openCall = "";
                    Close();
                },
                icon: CategoryIcon(fight.Category)) is { } master)
        {
            if (master) C.MutedTerritories.Remove(fight.TerritoryId);
            else C.MutedTerritories.Add(fight.TerritoryId);
            C.Save();
        }

        DrawScriptStrategies((ushort)fight.TerritoryId);

        if (!on)
        {
            ImGui.TextColored(Theme.V(Theme.Warn),
                "This fight is off. None of these will call.");
            ImGui.Spacing();
        }

        // Theirs, per mechanic, in their words, and then the ones written here.
        //
        // Ours used to stop at the return below. That was right when the rest of this
        // page was the imported set read back a second time, and wrong the moment a
        // covered fight started answering with calls of its own: both engines run in
        // a fight like Dancing Mad, so a page showing one of them is a page hiding
        // forty calls that fire. Only the hand written ones are added, never the rows
        // read out of the pack, which is what made two lists of one thing before.
        if (theirs)
        {
            DrawSeat();
            // Read as this player before the rows are sampled, the same as below, or
            // ours say the half of a call that lands on somebody else.
            FightCatalog.ReadAs(Runner?.MySlot ?? "", C.StratFor);
            DrawTheirCalls((ushort)fight.TerritoryId, listed);
            return;
        }

        if (calls.Count == 0)
        {
            Widgets.ListBegin();
            // Only reachable in the blink between the pack reloading and the page
            // redrawing, so it says that rather than inventing a reason.
            Widgets.RowNote(FightCatalog.PackProblem ?? "Nothing here yet.");
            Widgets.ListEnd();
            return;
        }

        // Read the calls as this player, so the rows say what this player would
        // hear: their role's half of a call and their group's answer to a strat.
        FightCatalog.ReadAs(Runner?.MySlot ?? "", C.StratFor);

        DrawSeat();

        var phase = DrawPhaseTabs(calls);

        // A hundred calls is too many to walk, so the words are searchable.
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##callfilter", "Search these calls", ref _callFilter, 64);
        ImGui.Spacing();

        var here = phase is { } only ? calls.Where(c => c.Phase == only).ToList() : calls;

        // Trimmed once, then used for the test, the match and the message alike.
        //
        // They disagreed: the empty test ignored whitespace and the match did not, so a
        // trailing space searched for "towers " and found nothing, while the message
        // trimmed it back and said no call here has "towers" in it. A search box that
        // denies something you can see on the row above it is worse than one that finds
        // nothing.
        var find = _callFilter.Trim();

        var shown = here
            .Where(c => find.Length == 0 || MineSays(c, find))
            .ToList();

        if (shown.Count == 0)
        {
            Widgets.ListBegin();
            Widgets.RowNote(find.Length == 0
                ? "Nothing here."
                : $"No call here has \"{find}\" in it.");
            Widgets.ListEnd();
            return;
        }

        Widgets.ListBegin();
        foreach (var call in shown) DrawCallRow(call);
        Widgets.ListEnd();

    }

    // Which answer your group runs, for the mechanics that have more than one.
    //
    // A mechanic with several accepted answers has no single right call: which tower
    // you take, which way the rotation reads, where the healers plant. Picking one in
    // code is choosing a group's strat for them and being wrong for everybody else.
    // Which seat the calls are read as, and a way to say when the game is wrong.
    //
    // It is right on its own everywhere there is a party list. A replay has none, so
    // the eight players in the object table stand in for it and this player is
    // always first among them: read as MT, H1, M1 or R1 and never as the second of
    // the role. Every call that splits a pair then names the other person's job,
    // which is most of what looks broken while watching a recording back.
    // The engine's eight, with "work it out" in front of them. Spelled out here once,
    // which meant a ninth seat would have reached every call in the plugin and not this
    // dropdown.
    private static readonly string[] Seats = ["Work it out", .. Audience.Slots];

    private void DrawSeat()
    {
        var guessed = Runner?.MySlot ?? "";
        var replay = Runner is { InReplay: true };

        // Out of the way until it is needed. A party list answers this correctly, so
        // a row asking about it there is a setting inviting somebody to break their
        // own calls; in a replay, or with nothing read at all, it is the answer.
        if (!replay && C.SeatOverride.Length == 0 && guessed.Length > 0) return;

        Widgets.GroupLabel("Your Role");
        Widgets.ListBegin();

        var idx = Math.Max(0, Array.IndexOf(Seats, C.SeatOverride));
        if (Widgets.RowCombo("Read the calls as",
                replay ? "A recording has no party list, so this is a guess" : "",
                ref idx, Seats, 190f, changed: C.SeatOverride.Length > 0, id: "seat"))
        {
            C.SeatOverride = idx <= 0 ? "" : Seats[idx];
            C.Save();
            // The rows above are sampled as whoever this is, so they are rebuilt
            // rather than left showing the previous role's half of every call.
            FightCatalog.Invalidate();
        }

        Widgets.RowNote(C.SeatOverride.Length > 0
            ? $"Reading every call as {C.SeatOverride}."
            : guessed.Length > 0
                ? $"Worked out as {guessed}." + (replay ? " In a recording that is a guess." : "")
                : "No party read yet. Calls show their plain half.");

        Widgets.ListEnd();
        ImGui.Spacing();
    }

    // One of our own strat rows. Drawn from the block in ConfigWindow.Strats.cs, which
    // is where their questions and ours are put in one list.
    private void DrawStratRow(ushort territory, Strategy s)
    {
        var labels = s.Options.Select(o => o.Label).ToArray();
        var chosen = C.StratFor(territory, s.Key);
        // Falls back to the first option rather than to nothing, so an answer
        // that has since been dropped cannot leave the row blank.
        var idx = Math.Max(0, IndexOf(s.Options, chosen));
        // Keyed on the strat's key, not its label. Labels are fight data and two
        // could read the same; keys are unique per fight and tested to be.
        if (Widgets.RowCombo(s.Name, s.Hint, ref idx, labels, 190f,
                changed: C.StratIsSet(territory, s.Key), id: $"strat-{territory}-{s.Key}")
            && idx >= 0 && idx < s.Options.Count)
        {
            C.SetStrat(territory, s.Key, s.Options[idx].Value);
            // The calls below this list are what the answer changes, so they are
            // re-read now rather than at the next reload.
            FightCatalog.Invalidate();
        }
    }

    private static int IndexOf(IReadOnlyList<StrategyOption> options, string value)
    {
        for (var i = 0; i < options.Count; i++) if (options[i].Value == value) return i;
        return -1;
    }

    // The fight you are standing in when the plugin knows its name and has nothing to
    // say for it yet. Null everywhere else, including in a fight that does have calls.
    //
    // Worth its own line: silence in a fight nobody has written looks exactly like
    // silence from something broken, and only one of those is worth chasing.
    private static string? UnwrittenHere()
    {
        var here = Service.ClientState.TerritoryType;
        if (FightCatalog.At(here) is not null) return null;
        return Shipped.At((ushort)here)?.Name;
    }

    // Calls in the fight you are standing in that wait on the arena being read, while
    // nothing has been read from it. Zero everywhere else, including in a fight that
    // has no prop calls at all.
    private int ArenaCallsHere()
    {
        var here = Service.ClientState.TerritoryType;
        if (Runner is not { ArenaSeen: 0 }) return 0;
        return FightCatalog.CallsIn(here).Count(c => FromTheArena(c.On));
    }

    // Strats still sitting on Off, which is a count of mechanics that will not call.
    private int StratsOff(uint territory)
    {
        var t = (ushort)territory;
        return Strategies.For(t).Count(s => C.StratFor(t, s.Key) == "none");
    }

    // The phase the pull is in, when this page is open on the fight being played and
    // that fight knows its own phases. Null everywhere else, so nothing is marked
    // from a number that was never earned.
    private int? LivePhase =>
        Runner is { InPull: true, PhasesKnown: true } r
        && _navFightId == Service.ClientState.TerritoryType
            ? r.Phase : null;

    private int? DrawPhaseTabs(IReadOnlyList<CallEntry> calls)
    {
        var phases = calls.Select(c => c.Phase).Distinct().OrderBy(p => p).ToList();
        // One phase is not phases: a fight where every call sits in the same bucket
        // gets no tabs at all rather than a row with one tab on it.
        if (phases.Count(p => p > 0) < 2) return null;

        var live = LivePhase;
        int? picked = null;
        if (ImGui.BeginTabBar("##phases", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (ImGui.BeginTabItem("All", AllTabFlag())) ImGui.EndTabItem();

            foreach (var p in phases.Where(p => p > 0))
            {
                var n = calls.Count(c => c.Phase == p);
                var open = ImGui.BeginTabItem($"P{p} ({n})###phase{p}");
                // Read before anything is drawn into the tab, while the tab itself
                // is still the last item.
                if (p == live) MarkLiveTab();
                if (open)
                {
                    picked = p;
                    ImGui.EndTabItem();
                }
            }

            if (phases.Contains(0))
            {
                var n = calls.Count(c => c.Phase == 0);
                if (ImGui.BeginTabItem($"Any ({n})###phaseany"))
                {
                    picked = 0;
                    ImGui.EndTabItem();
                }
                Tip("Not tied to a phase.");
            }
            ImGui.EndTabBar();
        }
        ImGui.Spacing();
        return picked;
    }

    // An accent underline on the tab of the phase the pull is in, so the page says
    // where the fight has got to without stealing the tab you were reading.
    private static void MarkLiveTab()
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var inset = Theme.S(5f);
        ImGui.GetWindowDrawList().AddRectFilled(
            new Vector2(min.X + inset, max.Y - Theme.S(2f)),
            new Vector2(max.X - inset, max.Y), Theme.Accent);
        Tip("The pull is in this phase now.");
    }

    // One call: what it says, whether it is on, and its own editor underneath.
    private void DrawCallRow(CallEntry call)
    {
        var open = _openCall == call.Key;
        var edit = C.EditFor(call.Key);
        // Against how it shipped, not against "on unless turned off": an unchecked
        // call ships silent, and a tick that reads on while the call says nothing is
        // the one thing this page must never do.
        var on = C.IsCallOn(call.Key, call.ShipsOn);
        var words = CallText.Sentence(Wording(call));

        // A call that cannot fire is the one state worth a word on the row. The rest
        // of what the engine knows about a call, which parser it wants and whether it
        // has been watched working, is ours to worry about and not something to read
        // before a pull.
        var dead = !call.FromTimeline && !LiveCoverage.Covered(call.On);
        var note = dead ? "Never fires" : "";
        var room = ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() * 2f
                   - ImGui.CalcTextSize(note).X - Theme.S(48f);

        Widgets.RowBegin(Widgets.Elide(words, room), "", ImGui.GetFrameHeight(),
            changed: C.IsEdited(call.Key), clickable: true,
            icon: open ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight,
            iconCol: dead ? Theme.Muted : LevelColor(edit?.Level ?? call.Level),
            id: "call" + call.Key, note: note, noteCol: dead ? Theme.Warn : 0u);
        var was = on;
        Widgets.GreenCheckbox("##on" + call.Key, ref on);
        var opened = Widgets.RowClicked;
        Widgets.RowEnd();

        if (on != was) C.SetCallOn(call.Key, call.ShipsOn, on);
        if (opened)
        {
            if (open) _openCall = "";
            else OpenCall(call);
        }
        if (_openCall == call.Key) DrawCallEditor(call);
    }

    // Reads the current edit, hands it to the caller to change, and puts it back
    // through the one setter that drops an edit once it is back to default.
    private void Change(string key, Action<CallEdit> change)
    {
        var edit = C.EditFor(key)?.Copy() ?? new CallEdit();
        change(edit);
        C.SetEdit(key, edit);
    }

    // Reading, not connected: a parser that accepted us and never opened its channel
    // leaves these calls just as quiet, and saying they are fine would be a lie the
    // player only finds out about mid-pull.

    // Kinds the arena poll is supposed to supply. Named here rather than asked of
    // LiveCoverage, because what matters is which source a kind comes from and that
    // list only records what the source is called.
    private static bool FromTheArena(EventKind kind) =>
        kind is EventKind.ActorSpawn or EventKind.ActorMoved or EventKind.NameToggle;

    // A call that waits on the arena poll while nothing has arrived from it.
    //
    // Checked by counting what has turned up, not by reading the coverage list. The
    // list says these come from ArenaEvents and is right about the intent, but no
    // event has ever reached the engine from it, and a page that reads the intent
    // reports five fights of calls as fine when they cannot fire.

    private static uint LevelColor(CallLevel level) => level switch
    {
        CallLevel.Alarm => Theme.Danger,
        CallLevel.Alert => Theme.Warn,
        _ => Theme.Accent,
    };

    private void DrawCallEditor(CallEntry call)
    {
        var edit = C.EditFor(call.Key);

        DrawOnePreview(
            CallText.Sentence(string.IsNullOrWhiteSpace(_callWords) ? call.Text : _callWords),
            edit?.Level ?? call.Level,
            CallIcon.Listed(call.On, call.MatchId));

        // The box holds what is being typed; an empty one means no rewording at
        // all, which is what stores as nothing rather than as an empty line.
        if (Widgets.RowText("Call", ref _callWords, call.Key, sub: true,
                changed: edit?.Text is not null))
        {
            var typed = _callWords;
            // Compared against the shipped line as it is drawn, or opening a call
            // and typing nothing would store its own capital letter as an edit.
            Change(call.Key, e => e.Text =
                string.IsNullOrWhiteSpace(typed) || typed == CallText.Sentence(call.Text)
                    ? null : typed);
        }
        if (call.Text.Contains(CallEdits.Target, StringComparison.Ordinal))
            Tip($"Keep {CallEdits.Target} and it fills in the name.");

        var level = (int)(edit?.Level ?? call.Level);
        if (Widgets.RowCombo("Level", "", ref level, SeverityNames, 120f,
                changed: edit?.Level is not null, sub: true))
            Change(call.Key, e =>
                e.Level = (CallLevel)level == call.Level ? null : (CallLevel)level);

        if (!LiveCoverage.Covered(call.On))
        {
            ImGui.TextColored(Theme.V(Theme.Warn), "This one never fires.");
            ImGui.Spacing();
        }

        if (C.IsEdited(call.Key))
        {
            Widgets.RowBegin("", "", IconBtnWidth(FontAwesomeIcon.Undo, "Back to default"),
                sub: true);
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, "Back to default"))
            {
                C.ClearEdit(call.Key);
                OpenCall(call);
            }
            Widgets.RowEnd();
        }
    }

    // ---- raid plan ----

    private string _planSaid = "";

    private void DrawPlanPage()
    {
        PageHead("Raid Plan", Runner is { PlanCalls: > 0 } r
            ? $"{r.PlanCalls} call{(r.PlanCalls == 1 ? "" : "s")} for "
              + (FightCatalog.At(Service.ClientState.TerritoryType)?.Name ?? r.Fight)
            : "", false, hasMaster: false, icon: FontAwesomeIcon.ClipboardList);

        Widgets.ListBegin();
        Widgets.RowNote("Your group's strats, hung on the calls that already exist");
        Widgets.RowNote("Write it in plan.txt, in the config folder");
        Widgets.ListEnd();
        ImGui.Spacing();

        Widgets.GroupLabel("Example");
        Widgets.ListBegin();
        Widgets.RowNote("Wave Cannon: MT N, OT S, H1 NW");
        Widgets.RowNote("Towers: M1 west, M2 east");
        Widgets.ListEnd();
        // Read off the engine's list rather than typed out. This was the fifth copy of
        // the eight and the one nobody would have caught: a sweep for the array form
        // does not find a sentence, and a plan page naming a seat the parser does not
        // know is a line somebody writes and then cannot get to fire.
        Tip($"A mechanic, then who goes where. Slots are {string.Join(' ', Audience.Slots)}, "
            + "and who sits in each is on the Roles page.");
        ImGui.Spacing();

        if (Runner is not { } runner)
        {
            ImGui.TextColored(Theme.V(Theme.Muted), "Not running yet.");
            return;
        }

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Read the plan"))
            _planSaid = runner.LoadPlan();
        Tip("Reads plan.txt again. Delete the file to undo a bad paste.");

        ImGui.SameLine(0, Theme.S(8f));
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.FolderOpen, "Open folder"))
            OpenConfigFolder();
        Tip("Where plan.txt goes.");

        if (_planSaid.Length > 0)
        {
            ImGui.Spacing();
            // Their sentence, not ours: it already names what matched and what did
            // not, and a plan that matched nothing has to say so.
            ImGui.TextWrapped(_planSaid);
        }
    }

    private static void OpenConfigFolder()
    {
        var dir = Service.PluginInterface.ConfigDirectory;
        try
        {
            dir.Create();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir.FullName)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"could not open {dir.FullName}");
        }
    }

    // ---- call display ----

    private void DrawCallDisplayPage()
    {
        if (PageHead("Call Display", "", C.AlertsEnabled,
                reset: () => ResetPage(NavKind.CallDisplay), icon: FontAwesomeIcon.Desktop) is { } alerts)
            C.AlertsEnabled = alerts;

        DrawCallPreview();

        // Placing it is done on the game screen, where it will actually appear;
        // this switch is the way there.
        Widgets.ListBegin();
        var testing = C.TestMode;
        if (Widgets.RowCheckClick("Test Mode", "", ref testing,
            FontAwesomeIcon.ArrowsAlt, Theme.Accent, changed: Changed(nameof(Configuration.TestMode))))
        { C.TestMode = testing; C.Save(); }
        Tip("Sample call on screen. Drag it where you want it.");
        Widgets.ListEnd();

        Widgets.GroupLabel("Text");
        Widgets.ListBegin();

        var size = C.CallFontSizePx;
        if (Widgets.RowDrag("Text Size", "", ref size, 14f, 120f, "%.0f px",
            changed: Changed(nameof(Configuration.CallFontSizePx)))) { C.CallFontSizePx = size; C.Save(); }

        var align = C.CallTextAlign;
        if (Widgets.RowCombo("Alignment", "", ref align,
            new[] { "Left", "Center", "Right" }, 120f,
            Changed(nameof(Configuration.CallTextAlign)))) { C.CallTextAlign = align; C.Save(); }

        var icon = C.ShowCallIcon;
        if (Widgets.RowCheckClick("Icon", "", ref icon, changed: Changed(nameof(Configuration.ShowCallIcon))))
        { C.ShowCallIcon = icon; C.Save(); }
        Tip("The debuff on you. Other calls show none.");

        if (C.ShowCallIcon)
        {
            var iscale = C.CallIconScale;
            if (Widgets.RowDrag("Icon Size", "", ref iscale, 0.4f, 1.6f, "%.2fx",
                changed: Changed(nameof(Configuration.CallIconScale)), sub: true))
            { C.CallIconScale = iscale; C.Save(); }
        }

        var count = C.ShowCountdown;
        if (Widgets.RowCheckClick("Countdown", "", ref count,
            changed: Changed(nameof(Configuration.ShowCountdown)))) { C.ShowCountdown = count; C.Save(); }
        Tip("Seconds left, after the words.");

        var tts = C.VoiceEnabled;
        if (Widgets.RowCheckClick("TTS", "", ref tts,
            changed: Changed(nameof(Configuration.VoiceEnabled)))) { C.VoiceEnabled = tts; C.Save(); }
        Tip("Read the call out loud.");

        var outline = C.TextOutline;
        if (Widgets.RowCheckClick("Outline", "", ref outline,
            changed: Changed(nameof(Configuration.TextOutline)))) { C.TextOutline = outline; C.Save(); }
        Tip("Keeps a call readable over a bright floor.");

        var pulse = C.PulseWhenClose;
        if (Widgets.RowCheckClick("Pulse on Go", "", ref pulse,
            changed: Changed(nameof(Configuration.PulseWhenClose)))) { C.PulseWhenClose = pulse; C.Save(); }

        Widgets.ListEnd();

        Widgets.GroupLabel("Colors");
        Widgets.ListBegin();

        var info = Theme.V(C.ColorInfo);
        if (Widgets.RowColor(SeverityName(CallLevel.Info), "", ref info,
            Changed(nameof(Configuration.ColorInfo)))) { C.ColorInfo = Widgets.ToColor(info); C.Save(); }
        Tip("Worth knowing.");

        var alert = Theme.V(C.ColorAlert);
        if (Widgets.RowColor(SeverityName(CallLevel.Alert), "", ref alert,
            Changed(nameof(Configuration.ColorAlert)))) { C.ColorAlert = Widgets.ToColor(alert); C.Save(); }
        Tip("Act now.");

        var alarm = Theme.V(C.ColorAlarm);
        if (Widgets.RowColor(SeverityName(CallLevel.Alarm), "", ref alarm,
            Changed(nameof(Configuration.ColorAlarm)))) { C.ColorAlarm = Widgets.ToColor(alarm); C.Save(); }
        Tip("Act now or die.");

        Widgets.ListEnd();

        Widgets.GroupLabel("Placement");
        Widgets.ListBegin();

        var back = C.ShowBackground;
        if (Widgets.RowCheckClick("Background", "", ref back,
            changed: Changed(nameof(Configuration.ShowBackground)))) { C.ShowBackground = back; C.Save(); }
        Tip("Box behind the call.");

        if (C.ShowBackground)
        {
            var bcol = Theme.V(C.BackgroundColor);
            if (Widgets.RowColor("Background Color", "", ref bcol,
                Changed(nameof(Configuration.BackgroundColor)), sub: true))
            { C.BackgroundColor = Widgets.ToColor(bcol); C.Save(); }
        }

        var locked = C.OverlayLocked;
        if (Widgets.RowCheckClick("Lock position", "", ref locked,
            changed: Changed(nameof(Configuration.OverlayLocked)))) { C.OverlayLocked = locked; C.Save(); }
        Tip("Locks it in place. A pull locks it anyway, and test mode unlocks it.");

        Widgets.RowBegin("Position", "", Widgets.SmallWidth("Center"),
            ctlHeight: Widgets.SmallHeight, changed: Changed(nameof(Configuration.OverlayPosition)));
        if (ImGui.SmallButton("Center##pos"))
        {
            C.OverlayPosition = new Vector2(0.5f, 0.35f);
            C.Save();
            _overlay.RequestReposition();
        }
        Widgets.Tooltip("Back to the middle of the screen.");
        Widgets.RowEnd();

        Widgets.ListEnd();
    }

    private void DrawCallPreview() =>
        CallBox("##callpreview", 2, () => _overlay.DrawPreview());

    private void DrawOnePreview(string text, CallLevel level, CallIcon icon = default) =>
        CallBox("##calledit", 1, () => _overlay.DrawOne(text, level, icon));

    // A box holding calls drawn exactly as the overlay draws them.
    //
    // Both boxes guessed their own height, at 2.9 and 1.6 times the font size, and
    // both cut the slab off. The slab is drawn past the text box rather than laid out,
    // so no multiple of the font size tracks it: at the default 30px the two-call box
    // was 105 tall against 134 of content, and the gap between two calls grows with
    // the size, so it got worse the bigger the calls were set.
    //
    // Sideways it was the same story from the other end. A child window clips its own
    // draw list and pads by ImGui's default 8, while the slab reaches PadX past the
    // text: fine on centred text, which is the default and is why this went unnoticed,
    // and clipped on every left or right aligned call.
    //
    // Both now come from the overlay, off the same CallLook arithmetic its own window
    // uses in PreDraw, so the preview and the game cannot drift apart.
    private void CallBox(string id, int calls, Action draw)
    {
        // The slab's room, or the box's own, whichever is larger. With the background
        // switched off there is no slab to make room for, and taking the padding
        // straight from it put the words flat against the border: nothing was being
        // cut, and it read exactly like something was.
        var air = Theme.S(8f);
        var pad = Vector2.Max(_overlay.SlabPad(), new Vector2(air, air));
        var h = _overlay.StackContentHeight(calls) + pad.Y * 2f;
        var w = ImGui.GetContentRegionAvail().X;
        var p = ImGui.GetCursorScreenPos();

        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + new Vector2(w, h), 0xFF10080B, Theme.S(8f));   // #0B0810
        dl.AddRect(p, p + new Vector2(w, h), Widgets.CardBorder, Theme.S(8f));

        // The slab's overhang lives in the padding, which is why nothing inside adds a
        // leading spacer any more: that spacer was the old way of buying room at the
        // top and it bought none at the sides.
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, pad);
        if (ImGui.BeginChild(id, new Vector2(w, h), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            draw();
        }
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.Spacing();
    }

    // ---- tts ----

    private void DrawTtsPage()
    {
        var voice = Runner?.Voice;
        if (PageHead("TTS", VoiceLine(), C.VoiceEnabled,
                reset: () => ResetPage(NavKind.Tts), icon: FontAwesomeIcon.VolumeUp,
                noteCol: voice is { Unavailable: true } ? Theme.Warn : 0u) is { } speaking)
        { C.VoiceEnabled = speaking; C.Save(); }

        // A voice this machine does not have is worth saying out loud, since the
        // switch would otherwise look on and do nothing.
        if (voice is { Unavailable: true })
        {
            ImGui.TextColored(Theme.V(Theme.Warn),
                "No voice installed here, so nothing is read out.");
            ImGui.Spacing();
        }

        Widgets.ListBegin();
        var vol = C.VoiceVolume;
        if (Widgets.RowDrag("Volume", "", ref vol, 0f, 1f, "%.2f",
            changed: Changed(nameof(Configuration.VoiceVolume)))) { C.VoiceVolume = vol; C.Save(); }
        Tip("How loud it reads out.");

        var speed = C.VoiceSpeed;
        if (Widgets.RowDrag("Speed", "local voice only", ref speed, 0.5f, 2f, "%.2fx",
            changed: Changed(nameof(Configuration.VoiceSpeed)))) { C.VoiceSpeed = speed; C.Save(); }
        Tip("How fast it talks. 1.00x is how the voice was recorded.");
        Widgets.ListEnd();

        DrawLocalVoice();

        // Lines that never got read out.
        //
        // This branch was here with nothing but a blank line in it: the count was
        // worked out, the condition survived, and whatever used to say it did not. So
        // the one number that means "a call you were meant to hear did not happen" was
        // in the chat command and nowhere anybody looks during a night.
        //
        // Both ways it counts are the voice falling behind, and it does not record
        // which: the queue is bounded and Send drops rather than blocking the frame,
        // and a line still waiting after StaleSeconds is thrown away as too late to be
        // worth saying. Neither is worth alarming about on its own, which is why it
        // reads as a count and not as a fault.
        if (voice is { Dropped: > 0 } behind)
        {
            ImGui.Spacing();
            // Phrased "n of m" so it reads right at one as well as at forty, rather
            // than agreeing with the plural and then saying "1 line were not read out".
            ImGui.TextColored(Theme.V(Theme.Warn),
                $"{behind.Dropped} of {behind.Dropped + behind.Spoken} lines never got "
                + "read out. The voice could not keep up.");
        }
    }

    // What the voice is doing, in the one place both the tile and the page ask.
    private string VoiceLine()
    {
        if (Runner?.Voice is not { } v) return C.VoiceEnabled ? "On" : "Off";
        if (v.Unavailable) return "No voice on this machine";
        if (!C.VoiceEnabled) return "Off";
        // Which voice will do the talking, not which one happens to be up: the
        // local one starts on the first call, so "system voice" before then would
        // be wrong about the very next line.
        var which = Runner?.LocalVoice is { GivenUp: false, Installed: true }
            ? "local voice" : "system voice";
        return $"On, {which}";
    }

    private void DrawLocalVoice()
    {
        if (Runner?.LocalVoice is not { } voice) return;

        Widgets.GroupLabel("Local Voice");
        Widgets.ListBegin();

        var useLocal = C.UseLocalVoice;
        if (Widgets.RowCheck("Use it", "off = system voice", ref useLocal,
            Changed(nameof(Configuration.UseLocalVoice)))) { C.UseLocalVoice = useLocal; C.Save(); }

        DrawVoicePicker(voice);

        Widgets.RowNote(voice.Describe());
        Widgets.ListEnd();

        if (voice.Voices.Count > 0)
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Hear it"))
                Runner?.Voice.Test("Stack on the tank");
            Tip("Reads out a sample line so you can hear the voice.");
            ImGui.Spacing();
        }

        // Three failed starts is broken rather than unlucky, and it falls back
        // silently, so the one state worth colouring is that one.
        if (voice.GivenUp)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Warn), "It kept stopping, so it is not being started again.");

            // And what to do about it, because nothing else on this page can.
            //
            // The give-up is for the session: neither the counter nor the flag is ever
            // put back, so fixing whatever killed it changes nothing until the plugin
            // is loaded again. Every other dead end in this window says how to get out
            // of it, and this one sat there saying only that it had happened.
            //
            // Dropping the voice files in IS noticed on its own, because the pack is
            // rechecked on a timer, so that case is deliberately not mentioned here:
            // this line is only for the one where the voice ran and would not stay up.
            ImGui.TextColored(Theme.V(Theme.Muted),
                "Reload the plugin to try it again. Until then the system voice reads the calls.");

            // What the voice itself said on the way down. It is already captured for
            // the log, and the person reading this page is the one who needs it: a
            // missing runtime and a bad voice file both read as "it kept stopping"
            // otherwise, and neither is fixable without the reason.
            if (voice.WhyItStopped is { Length: > 0 } why)
            {
                ImGui.Spacing();
                ImGui.TextColored(Theme.V(Theme.Muted), "It said:");
                ImGui.TextWrapped(why);
            }
        }

        Widgets.ListBegin();
        if (!voice.Installed)
            foreach (var piece in voice.Pack.Missing)
                Widgets.RowNote($"{piece.Name}, {AsSize(piece.Bytes)}");
        Widgets.ListEnd();

        if (!voice.Installed)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Muted),
                "Drop them in the voice folder beside the config.");
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.FolderOpen, "Open folder"))
                OpenConfigFolder();
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Muted), "Until then it reads out with the system voice.");
        }
    }

    // Rebuilt only when the folder, the language or the voice moves, because these
    // two rows are drawn on every frame the page is open.
    private IReadOnlyList<VoiceCatalog.Choice>? _voiceSource;
    private string[] _voiceLanguages = [];
    private string _voiceLang = "";
    private string _voiceChosen = "";
    private VoiceCatalog.Choice[] _voiceOptions = [];
    private string[] _voiceLabels = [];

    // Two rows rather than one list of everything installed: the language narrows it
    // to a handful, and the second row is then short enough to read.
    private void DrawVoicePicker(Game.NeuralVoice voice)
    {
        var all = voice.Voices;
        if (all.Count == 0) return;

        if (!ReferenceEquals(_voiceSource, all))
        {
            _voiceSource = all;
            _voiceLanguages = VoiceCatalog.LanguagesIn(all).ToArray();
            _voiceLang = "";
        }

        // The language row follows the voice that is set, so a config carrying a
        // British voice opens on British rather than on the top of the list.
        if (_voiceLang.Length == 0 || _voiceChosen != C.LocalVoiceName)
        {
            _voiceChosen = C.LocalVoiceName;
            var known = LanguageOf(all, _voiceChosen);

            // The saved voice is not in the folder: deleted, renamed, or carried in
            // from a config that had the full pack. Falling through to the first
            // language would leave the row showing a voice nobody chose while the
            // saved name went to the engine, so the window would name one voice
            // while another spoke. Snap to one that exists instead.
            if (known is null)
            {
                _voiceChosen = all.Any(v => v.Name == VoiceCatalog.Default)
                    ? VoiceCatalog.Default
                    : all[0].Name;
                C.LocalVoiceName = _voiceChosen;
                C.Save();
                known = LanguageOf(all, _voiceChosen);
            }

            _voiceLang = known ?? _voiceLanguages[0];
            Regroup();
        }

        var lang = Math.Max(0, Array.IndexOf(_voiceLanguages, _voiceLang));
        if (_voiceLanguages.Length > 1 &&
            Widgets.RowCombo("Language", "", ref lang, _voiceLanguages, sub: true))
        {
            _voiceLang = _voiceLanguages[lang];
            Regroup();
            // The voice moves with it, since the one that was set is no longer on
            // the list below.
            Choose(_voiceOptions[0].Name);
        }

        var pick = Math.Max(0, Array.FindIndex(_voiceOptions, v => v.Name == C.LocalVoiceName));
        if (Widgets.RowCombo("Voice", "", ref pick, _voiceLabels, width: 180f,
            changed: Changed(nameof(Configuration.LocalVoiceName)), sub: true))
            Choose(_voiceOptions[pick].Name);

        void Regroup()
        {
            _voiceOptions = all.Where(v => v.Language == _voiceLang).ToArray();
            _voiceLabels = _voiceOptions.Select(v => v.Label).ToArray();
        }

        void Choose(string name)
        {
            C.LocalVoiceName = name;
            _voiceChosen = name;
            C.Save();
        }
    }

    private static string? LanguageOf(IReadOnlyList<VoiceCatalog.Choice> all, string name)
    {
        foreach (var choice in all)
            if (choice.Name == name) return choice.Language;
        return null;
    }

    private static string AsSize(long bytes) =>
        bytes >= 1_048_576 ? $"{bytes / 1_048_576}MB" : $"{Math.Max(1, bytes / 1024)}KB";

    // ---- appearance ----

    private void DrawAppearancePage()
    {
        PageHead("Appearance", "", false, hasMaster: false,
            reset: () => ResetPage(NavKind.Appearance), icon: FontAwesomeIcon.Palette);

        Widgets.ListBegin();

        var accent = Theme.V(C.AccentColor);
        if (Widgets.RowColor("Accent Color", "", ref accent,
            Changed(nameof(Configuration.AccentColor))))
        {
            C.AccentColor = Widgets.ToColor(accent);
            Theme.Accent = C.AccentColor;
            C.Save();
        }

        var scale = C.UiScale;
        if (Widgets.RowDrag("Window Scale", "", ref scale, 0.8f, 1.6f, "%.2fx",
            changed: Changed(nameof(Configuration.UiScale))))
        {
            C.UiScale = scale;
            C.Save();
        }

        var cb = C.ColorblindMode;
        if (Widgets.RowCheckClick("Colorblind Mode", "", ref cb,
            changed: Changed(nameof(Configuration.ColorblindMode))))
        {
            C.ColorblindMode = cb;
            Theme.Colorblind = cb;
            C.Save();
        }

        Widgets.ListEnd();

        Widgets.GroupLabel("Preset Accents");
        DrawAccentSwatches();
    }

    // The palette the window ships with, so a color can be picked without
    // opening the wheel.
    private static readonly (string Name, uint Color)[] Presets =
    {
        ("Violet", 0xFFF755A8),   // #A855F7
        ("Magenta", 0xFFB53AEC),  // #EC3AB5
        ("Cyan", 0xFFE0C822),     // #22C8E0
        ("Lime", 0xFF54D486),     // #86D454
        ("Amber", 0xFF31A6F5),    // #F5A631
        ("Rose", 0xFF6B5CF4),     // #F45C6B
    };

    private void DrawAccentSwatches()
    {
        var sq = ImGui.GetFrameHeight();
        var avail = ImGui.GetContentRegionAvail().X;
        var x = 0f;
        foreach (var (name, color) in Presets)
        {
            var w = sq + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.CalcTextSize(name).X
                    + ImGui.GetStyle().FramePadding.X * 2f;
            if (x > 0f && x + w > avail) { x = 0f; } else if (x > 0f) ImGui.SameLine();
            x += w + ImGui.GetStyle().ItemSpacing.X;

            var on = C.AccentColor == color;
            var p = ImGui.GetCursorScreenPos();
            var size = new Vector2(w, sq);
            var clicked = ImGui.InvisibleButton($"##acc{name}", size);
            var hot = ImGui.IsItemHovered();
            if (hot) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(p, p + size,
                on ? (color & 0x00FFFFFFu) | 0x2A000000u : hot ? 0xFF2B1B22 : Theme.PanelBg, 5f);
            dl.AddRect(p, p + size, on ? color : Widgets.CardBorder, 5f);
            var pad = ImGui.GetStyle().FramePadding.X;
            var chip = sq * 0.55f;
            dl.AddRectFilled(new Vector2(p.X + pad, p.Y + (sq - chip) * 0.5f),
                new Vector2(p.X + pad + chip, p.Y + (sq + chip) * 0.5f), color, 3f);
            dl.AddText(new Vector2(p.X + pad + chip + ImGui.GetStyle().ItemInnerSpacing.X,
                p.Y + (sq - ImGui.GetTextLineHeight()) * 0.5f), Theme.TextBright, name);

            if (!clicked) continue;
            C.AccentColor = color;
            Theme.Accent = color;
            C.Save();
        }
    }

    // ---- parser ----

    // What the parser is doing, and how to give it something to do.
    //
    // There is no source to pick and no address to type, unlike the meter's page in the
    // other plugin: the link here is the parser plugin's own IPC channel and nothing
    // else, so a box for a socket address would be a box that does nothing. What is
    // worth showing is the state, a way back from a link that gave up, and the parser
    // settings that make it send usable lines.
    //
    // Led with the fact that none of it is required. Every kind of event the pack uses
    // reaches the engine off the client on a bare install, and a page of setup steps
    // with no such line reads as a page of things somebody has to do first.
    private void DrawParserPage()
    {
        PageHead("Parser", "", false, hasMaster: false, icon: FontAwesomeIcon.NetworkWired);

        var reading = Runner is { ParserReading: true };
        var connected = Runner is { ParserConnected: true };
        var asking = Runner is { ParserAsking: true };

        // Four states and they are not degrees of the same thing, so each gets its own
        // words and its own color. Off is muted rather than red: no parser is the
        // ordinary way to run this, and a red light over a working install is a bug
        // report waiting to be filed.
        //
        // No counts beside them. A number that climbs is the one thing on a page that
        // pulls the eye every frame, and it answers a question nobody opened this page
        // to ask.
        var (state, color) =
            reading ? ("Connected to the parser", Theme.Good)
            : asking ? ("Looking for a parser", Theme.Warn)
            : connected ? ("Connected, but quiet", Theme.Warn)
            : ("Off", Theme.Muted);

        StatusStrip(color, state);

        if (Runner is { ParserDropped: > 0 } fed)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Warn), $"The feed dropped {fed.ParserDropped}.");
        }

        ImGui.Spacing();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Reconnect"))
            Runner?.ParserRetry();
        Tip("Drops nothing that is working. Picks up a link that gave up.");

        ImGui.Spacing();
        ImGui.PushTextWrapPos(0f);
        ImGui.TextDisabled(
            "Calls work without a parser. Every kind of event the fights use is read "
            + "from the game itself, so this is only ever a second opinion.");
        ImGui.PopTextWrapPos();

        Widgets.SectionHeader("In IINACT");
        ImGui.TextDisabled("Nothing to connect: this links straight to it. On its Parser tab:");
        SetupToggle(1, "Disable Damage Shield Estimates", false, "or shields read zero.");
        SetupToggle(2, "End encounter automatically after leaving combat", true);
        SetupStep(3, "Player name: leave it as YOU.");
        Tip("The parser says YOU and the call fills your name in.");
        ImGui.TextDisabled("Writing out the network log file is for uploading logs, not for this.");

        Widgets.SectionHeader("In ACT");
        for (var i = 0; i < ActSteps.Length; i++) SetupStep(i + 1, ActSteps[i]);
        ImGui.TextDisabled("Lower than that splits a fight at its own downtime.");
    }

    // The link's state, in a strip that takes the state's own color: a bar down the
    // edge, a wash behind it, a dot, and the words.
    //
    // Carried by color rather than by wording, so it reads before it is read. The same
    // three alphas the selected nav row uses, off the one color, which is what keeps a
    // green strip and an amber one looking like the same control.
    //
    // A dot and a few words rather than a label and a value. "Parser: off" is the
    // sidebar's job, where it sits beside nine other rows and has to be short; on a page
    // that is only about the parser, naming it again says nothing.
    private static void StatusStrip(uint color, string text)
    {
        var lineH = ImGui.GetTextLineHeight();
        var pad = Theme.S(10f);
        var rgb = color & 0x00FFFFFFu;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, rgb | 0x1E000000u);
        ImGui.PushStyleColor(ImGuiCol.Border, rgb | 0x66000000u);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Theme.S(6f));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad * 0.7f));

        var h = lineH + pad * 1.4f;
        var at = ImGui.GetCursorScreenPos();

        if (ImGui.BeginChild("##parserstate", new Vector2(0f, h), true))
        {
            var dot = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(lineH * 0.7f, lineH));
            ImGui.GetWindowDrawList().AddCircleFilled(
                new Vector2(dot.X + lineH * 0.35f, dot.Y + lineH * 0.5f), lineH * 0.27f, color);

            ImGui.SameLine(0, Theme.S(6f));
            ImGui.TextColored(Theme.V(color), text);
        }
        ImGui.EndChild();

        // Drawn after the child so it sits over the border rather than under it.
        ImGui.GetWindowDrawList().AddRectFilled(
            new Vector2(at.X, at.Y + 2f), new Vector2(at.X + Theme.S(3f), at.Y + h - 2f),
            color, Theme.S(2f));

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(2);
    }

    // A setting to find and what to leave it on, the state in its own color.
    private static void SetupToggle(int n, string setting, bool on, string why = "")
    {
        ImGui.TextColored(Theme.V(Theme.Accent), $"{n}");
        ImGui.SameLine(0, Theme.S(10f));
        ImGui.TextUnformatted(setting + ":");
        ImGui.SameLine(0, Theme.S(5f));
        ImGui.TextColored(Theme.V(on ? Theme.Good : Theme.Danger), on ? "ON" : "OFF");
        if (why.Length == 0) return;
        ImGui.SameLine(0, Theme.S(5f));
        ImGui.TextDisabled(why);
    }

    // A numbered line, the number in the accent color.
    private static void SetupStep(int n, string text)
    {
        ImGui.TextColored(Theme.V(Theme.Accent), $"{n}");
        ImGui.SameLine(0, Theme.S(10f));
        ImGui.TextUnformatted(text);
    }

    private static readonly string[] ActSteps =
    [
        "Run ACT, with its FFXIV plugin.",
        "Plugins > OverlayPlugin.dll > WSServer > Start.",
        "Options > Main Table/Encounters > Idle Limit: 180.",
    ];

    // Whether a setting has been moved off what it ships as.
    private bool Changed(string prop) => SettingsIndex.IsChanged(C, prop);
}
