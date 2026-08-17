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
        // Having no parser is not a fault any more. Every kind has a client route now,
        // head markers and tethers included, and LiveCoverage.NeedsAParser is empty to
        // say so. This used to read "No parser, head markers are quiet", which as of
        // this build tells somebody their markers are dead while they are working.
        //
        // A parser that answered and then never opened its channel is still a fault:
        // it is there, it is not feeding, and the statuses it would have improved are
        // silently the poorer ones. Still shaking hands is worth nothing on the list,
        // because it settles on its own in a second or two.
        if (Runner is { ParserConnected: true, ParserReading: false, ParserAsking: false })
            problems.Add("The parser never answered");
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
        Tip(problems.Count == 0 ? "Nothing needs attention." : "Go and fix the first one.");
    }

    private static ConfigWindow.NavKind PageFor(string problem) => problem switch
    {
        "Calls are off" => NavKind.CallDisplay,
        "Test mode is on" => NavKind.CallDisplay,
        "Local voice gave up" => NavKind.Tts,
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
        if (Widgets.RowCheckClick("Every Call On", "", ref all,
            FontAwesomeIcon.Bullhorn, Theme.Accent,
            changed: Changed(nameof(Configuration.AllCallsOn)),
            note: all ? "" : "Only the exact ones"))
        { C.AllCallsOn = all; C.Save(); }
        Tip(all
            ? "Everything calls, exact port or not."
            : "Only the exact ports call. Turn the rest on per fight.");
        Widgets.ListEnd();
        ImGui.Spacing();

        Widgets.ListBegin();
        foreach (var f in fights)
        {
            var off = C.IsMuted(f.TerritoryId);
            var edits = EditedIn(f);
            var note = off ? "Off"
                : edits > 0 ? $"{f.Calls} calls, {edits} edited"
                : $"{f.Calls} call{(f.Calls == 1 ? "" : "s")}";
            if (Widgets.RowDoor(f.Name, "", CategoryIcon(category),
                off ? Theme.Muted : Theme.Accent, note: note,
                noteCol: off ? Theme.Warn : 0u))
                OpenFight(f);
            if (f.Full.Length > 0) Tip(f.Full);
        }
        Widgets.ListEnd();
    }

    // ---- one fight's calls ----

    private void OpenFight(FightEntry f)
    {
        _navFightId = f.TerritoryId;
        _nav = NavKind.Fight;
        _openCall = "";
        _callFilter = "";
    }

    private void OpenCall(CallEntry call)
    {
        _openCall = call.Key;
        // Opened as it is drawn, so the box and the row agree.
        _callWords = CallText.Sentence(C.EditFor(call.Key)?.Text ?? call.Text);
    }

    private int EditedIn(FightEntry f) =>
        FightCatalog.CallsIn(f.TerritoryId).Count(c => C.IsEdited(c.Key));

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

        var calls = FightCatalog.CallsIn(fight.TerritoryId);
        var edited = calls.Count(c => C.IsEdited(c.Key));
        var on = !C.IsMuted(fight.TerritoryId);
        if (PageHead(fight.Name, edited > 0 ? $"{fight.Calls} calls, {edited} edited"
                : $"{fight.Calls} call{(fight.Calls == 1 ? "" : "s")}", on,
                reset: () =>
                {
                    C.ClearEdits(calls.Select(c => c.Key));
                    if (C.MutedTerritories.Remove(fight.TerritoryId)) C.Save();
                    // Back to defaults means the strats too, or the button half
                    // undoes the page and leaves the group's answers behind.
                    foreach (var s in Strategies.For((ushort)fight.TerritoryId))
                        C.SetStrat((ushort)fight.TerritoryId, s.Key, s.Default);
                    _openCall = "";
                },
                icon: CategoryIcon(fight.Category)) is { } master)
        {
            if (master) C.MutedTerritories.Remove(fight.TerritoryId);
            else C.MutedTerritories.Add(fight.TerritoryId);
            C.Save();
        }

        if (!on)
        {
            ImGui.TextColored(Theme.V(Theme.Warn),
                "This fight is off. None of these will call.");
            ImGui.Spacing();
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
        DrawStrats((ushort)fight.TerritoryId);

        var phase = DrawPhaseTabs(calls);

        // A hundred calls is too many to walk, so the words are searchable.
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##callfilter", "Search these calls", ref _callFilter, 64);
        ImGui.Spacing();

        var here = phase is { } only ? calls.Where(c => c.Phase == only).ToList() : calls;

        var dead = here.Count(c => !LiveCoverage.Covered(c.On));
        // Covered on paper, silent in fact. Counted apart from the dead ones because
        // this one comes back on its own the moment a parser starts.
        var hushed = here.Count(QuietNow);
        // Only the ones that are otherwise fine, or a call is counted twice.
        var unproven = here.Count(c => Unproven(c) && !NeedsParser(c) && !QuietNow(c)
                                       && LiveCoverage.Covered(c.On));
        var noArena = here.Count(NoArenaYet);
        DrawCallStateChips(here.Count(NeedsParser), dead, hushed, unproven, noArena);

        var shown = here
            .Where(c => string.IsNullOrWhiteSpace(_callFilter)
                || Wording(c).Contains(_callFilter, StringComparison.OrdinalIgnoreCase)
                || c.Text.Contains(_callFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (shown.Count == 0)
        {
            Widgets.ListBegin();
            Widgets.RowNote(string.IsNullOrWhiteSpace(_callFilter)
                ? "Nothing here."
                : $"No call here has \"{_callFilter.Trim()}\" in it.");
            Widgets.ListEnd();
            return;
        }

        Widgets.ListBegin();
        foreach (var call in shown) DrawCallRow(call);
        Widgets.ListEnd();

        // How long the fight actually is, from the shipped timeline. Two counts side
        // by side and no ratio between them: a call can cover several timeline
        // entries and the timeline lists things nobody would call, so a percentage
        // would read as coverage while measuring nothing.
        var mechanics = FightCatalog.MechanicsIn(fight.TerritoryId);
        if (mechanics > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Muted),
                $"The timeline lists {mechanics} mechanics for this fight.");
        }
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
    private static readonly string[] Seats =
        ["Work it out", "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2"];

    private void DrawSeat()
    {
        var guessed = Runner?.MySlot ?? "";
        var replay = Runner is { InReplay: true };

        // Out of the way until it is needed. A party list answers this correctly, so
        // a row asking about it there is a setting inviting somebody to break their
        // own calls; in a replay, or with nothing read at all, it is the answer.
        if (!replay && C.SeatOverride.Length == 0 && guessed.Length > 0) return;

        Widgets.GroupLabel("Your Seat");
        Widgets.ListBegin();

        var idx = Math.Max(0, Array.IndexOf(Seats, C.SeatOverride));
        if (Widgets.RowCombo("Read the calls as",
                replay ? "A recording has no party list, so this is a guess" : "",
                ref idx, Seats, 190f, changed: C.SeatOverride.Length > 0, id: "seat"))
        {
            C.SeatOverride = idx <= 0 ? "" : Seats[idx];
            C.Save();
            // The rows above are sampled as whoever this is, so they are rebuilt
            // rather than left showing the previous seat's half of every call.
            FightCatalog.Invalidate();
        }

        Widgets.RowNote(C.SeatOverride.Length > 0
            ? $"Reading every call as {C.SeatOverride}."
            : guessed.Length > 0
                ? $"Worked out as {guessed}." + (replay ? " In a recording that is a guess." : "")
                : "No party read yet, so calls show their plain half.");

        Widgets.ListEnd();
        ImGui.Spacing();
    }

    private void DrawStrats(ushort territory)
    {
        var strats = Strategies.For(territory);
        if (strats.Count == 0) return;

        Widgets.GroupLabel("Your Strats");
        Widgets.ListBegin();
        foreach (var s in strats)
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

        // Said once under the list rather than on every row that can be switched off.
        if (strats.Any(s => C.StratFor(territory, s.Key) == "none"))
            Widgets.RowNote("A strat left off stays quiet rather than guessing.");

        Widgets.ListEnd();
        ImGui.Spacing();
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
            if (ImGui.BeginTabItem("All")) ImGui.EndTabItem();

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

        // A timeline call answers no event at all, so every question below about
        // where its event comes from has no answer and would read as a fault.
        var dead = !call.FromTimeline && !LiveCoverage.Covered(call.On);
        var quiet = !call.FromTimeline
                    && (NeedsParser(call) || QuietNow(call) || NoArenaYet(call));
        // Unproven is last and is not a warning: the others say a call cannot speak,
        // this one says nobody has watched it speak yet.
        // Last of all, and the only one that is not about a call being in trouble:
        // it reads off the pull, so there is no one line to show before a pull.
        var note = dead ? "Never fires"
            // Said plainly rather than left blank: it is the answer to why this one
            // lands early when nothing on screen has happened yet.
            : call.FromTimeline ? "Counted down"
            : NeedsParser(call) ? "Needs a parser"
            : QuietNow(call) ? "Quiet this patch"
            : NoArenaYet(call) ? "No arena reads"
            : Unproven(call) ? "Unproven"
            : !call.Sampled ? "Reads the pull" : "";
        var room = ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() * 2f
                   - ImGui.CalcTextSize(note).X - Theme.S(48f);

        Widgets.RowBegin(Widgets.Elide(words, room), "", ImGui.GetFrameHeight(),
            changed: C.IsEdited(call.Key), clickable: true,
            icon: open ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight,
            iconCol: dead || quiet ? Theme.Muted : LevelColor(edit?.Level ?? call.Level),
            id: "call" + call.Key, note: note, noteCol: dead || quiet ? Theme.Warn : 0u);
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
    private bool NeedsParser(CallEntry call) =>
        LiveCoverage.NeedsAParser.ContainsKey(call.On) && Runner is { ParserReading: false };

    // Why some of this fight's calls are not speaking, as one row of chips.
    //
    // These arrived one per pass and were four stacked paragraphs of amber, which
    // pushed the calls themselves off the first screen and made a fight with one dead
    // call look like a fight in trouble. Same four numbers, one line, and the words
    // that explain each one moved onto the hover where they are read once.
    private static void DrawCallStateChips(int needsParser, int dead, int quiet, int unproven,
        int noArena)
    {
        if (needsParser + dead + quiet + unproven + noArena == 0) return;

        var avail = ImGui.GetContentRegionAvail().X;
        var used = 0f;
        // Empty as of this build: every kind has a client route now. Kept because it
        // reads the engine's own list rather than a copy, so if a kind ever goes back
        // to being parser-only the page says so without being edited.
        Chip("Needs a parser", needsParser, Theme.Warn, ref used, avail,
            "These only come from a parser, and none is reading.");
        Chip("Never fires", dead, Theme.Warn, ref used, avail,
            "Nothing on the game side sends what these wait for.");
        Chip("Quiet this patch", quiet, Theme.Warn, ref used, avail,
            "These wait on a hit, and this patch moved where hits are read from.\n"
            + "A running parser would cover them.");
        Chip("No arena reads", noArena, Theme.Warn, ref used, avail,
            "These wait on where the arena's props are, and nothing has arrived\n"
            + "from that source in this zone. Counted, not assumed.");
        // Muted: not a fault, just unwatched.
        Chip("Unproven", unproven, Theme.Muted, ref used, avail,
            "Expected to work and never seen doing it in a pull.\n"
            + "Open one to see what to watch for.");

        ImGui.Spacing();
    }

    // Laid out by measuring first, so a narrow window wraps the row instead of
    // running the last chip off the edge.
    private static void Chip(string label, int count, uint color, ref float used, float avail,
        string tip)
    {
        if (count == 0) return;

        var value = count.ToString();
        var w = Widgets.ChipWidth(label, value) + Theme.S(6f);
        if (used > 0f && used + w > avail) used = 0f;
        else if (used > 0f) ImGui.SameLine(0, Theme.S(6f));
        used += w;

        Widgets.Chip(label, value, color);
        Widgets.Tooltip(tip);
    }

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
    private bool NoArenaYet(CallEntry call) =>
        FromTheArena(call.On)
        && Runner is { ArenaSeen: 0 }
        && _navFightId == Service.ClientState.TerritoryType;

    // Wired, believed to work, never actually seen working in a pull.
    //
    // Not a fault and not a warning: the engine writes down which kinds are still
    // unproven, and a page that showed them as plainly fine would be making a claim
    // nobody has earned. Said quietly, next to what to watch for.
    private static bool Unproven(CallEntry call) =>
        LiveCoverage.UnprovenLive.ContainsKey(call.On);

    // Quiet right now for a reason that is not the call's own: the kind is one this
    // build can emit, but nothing is emitting it this session.
    //
    // Separate from "never fires", which is about the kind having no source at all.
    // A hit call is covered on paper and silent in fact when the address moved and
    // no parser is up, and that gap only shows at the moment it costs a pull.
    private bool QuietNow(CallEntry call) =>
        call.On == EventKind.AbilityHit && Runner is { HitsCovered: false };

    private string? FiresOn(CallEntry call)
    {
        if (_navFightId == 0) return null;
        if (MarkerMeanings.TryFor((ushort)_navFightId, call.On, call.MatchId, out var says))
            return says;

        return call.On switch
        {
            EventKind.HeadMarker => "a head marker",
            EventKind.Tether => "a tether",
            EventKind.StatusGain => "a debuff",
            _ => null,
        };
    }

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
            edit?.Level ?? call.Level);

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
        if (Widgets.RowCombo("Severity", "", ref level, SeverityNames, 120f,
                changed: edit?.Level is not null, sub: true))
            Change(call.Key, e =>
                e.Level = (CallLevel)level == call.Level ? null : (CallLevel)level);

        if (!LiveCoverage.Covered(call.On))
        {
            ImGui.TextColored(Theme.V(Theme.Warn), "This one never fires.");
            ImGui.TextWrapped(LiveCoverage.Explain(call.On));
            ImGui.Spacing();
        }
        else if (NeedsParser(call))
        {
            ImGui.TextColored(Theme.V(Theme.Warn), "Quiet without a parser.");
            ImGui.TextWrapped(LiveCoverage.Explain(call.On));
            ImGui.TextColored(Theme.V(Theme.Muted),
                "Everything else here works without one.");
            ImGui.Spacing();
        }
        else if (QuietNow(call))
        {
            ImGui.TextColored(Theme.V(Theme.Warn), "Quiet this patch.");
            ImGui.TextWrapped(LiveCoverage.Explain(call.On));
            ImGui.TextColored(Theme.V(Theme.Muted),
                "A running parser would cover it until the address is found again.");
            ImGui.Spacing();
        }
        else if (NoArenaYet(call))
        {
            ImGui.TextColored(Theme.V(Theme.Warn), "Nothing is reading the arena here.");
            ImGui.TextWrapped(LiveCoverage.Explain(call.On));
            ImGui.TextColored(Theme.V(Theme.Muted),
                "This call needs to know where a prop is, and no spawn, move or "
                + "targetable flip has reached the engine in this zone.");
            ImGui.Spacing();
        }
        else if (Unproven(call) && LiveCoverage.UnprovenLive.TryGetValue(call.On, out var watch))
        {
            // Muted, not amber: nothing is wrong with this call. It has simply never
            // been watched working, and saying it is fine would be a claim off the
            // back of no pull at all.
            ImGui.TextColored(Theme.V(Theme.Muted), "Not seen working in a pull yet.");
            ImGui.TextWrapped(watch);
            ImGui.Spacing();
        }

        if (FiresOn(call) is { } fires)
        {
            Widgets.RowBegin("Fires on", "", ImGui.CalcTextSize(fires).X, sub: true,
                ctlHeight: ImGui.GetTextLineHeight());
            ImGui.TextColored(Theme.V(Theme.Muted), fires);
            Widgets.RowEnd();
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

        Widgets.GroupLabel("How It Reads");
        Widgets.ListBegin();
        Widgets.RowNote("Wave Cannon: MT N, OT S, H1 NW");
        Widgets.RowNote("Towers: M1 west, M2 east");
        Widgets.ListEnd();
        Tip("A mechanic, then who goes where. Slots are MT OT H1 H2 M1 M2 R1 R2.");
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

        Widgets.GroupLabel("The Line");
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
        Tip("Only for a debuff on you or a marker over your head.");

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

        var shadow = C.TextShadow;
        if (Widgets.RowCheckClick("Shadow", "", ref shadow,
            changed: Changed(nameof(Configuration.TextShadow)))) { C.TextShadow = shadow; C.Save(); }

        var outline = C.TextOutline;
        if (Widgets.RowCheckClick("Outline", "", ref outline,
            changed: Changed(nameof(Configuration.TextOutline)))) { C.TextOutline = outline; C.Save(); }
        Tip("Harder edge than the shadow.");

        var pulse = C.PulseWhenClose;
        if (Widgets.RowCheckClick("Pulse on Go", "", ref pulse,
            changed: Changed(nameof(Configuration.PulseWhenClose)))) { C.PulseWhenClose = pulse; C.Save(); }

        Widgets.ListEnd();

        Widgets.GroupLabel("Severity");
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

    private void DrawCallPreview()
    {
        var h = C.CallFontSizePx * 2.9f + Theme.S(18f);
        var w = ImGui.GetContentRegionAvail().X;
        var p = ImGui.GetCursorScreenPos();

        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + new Vector2(w, h), 0xFF10080B, Theme.S(8f));   // #0B0810
        dl.AddRect(p, p + new Vector2(w, h), Widgets.CardBorder, Theme.S(8f));

        if (ImGui.BeginChild("##callpreview", new Vector2(w, h), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            ImGui.Dummy(new Vector2(1f, Theme.S(6f)));
            _overlay.DrawPreview();
        }
        ImGui.EndChild();
        ImGui.Spacing();
    }

    private void DrawOnePreview(string text, CallLevel level)
    {
        var h = C.CallFontSizePx * 1.6f + Theme.S(14f);
        var w = ImGui.GetContentRegionAvail().X;
        var p = ImGui.GetCursorScreenPos();

        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + new Vector2(w, h), 0xFF10080B, Theme.S(8f));
        dl.AddRect(p, p + new Vector2(w, h), Widgets.CardBorder, Theme.S(8f));

        if (ImGui.BeginChild("##calledit", new Vector2(w, h), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            ImGui.Dummy(new Vector2(1f, Theme.S(5f)));
            _overlay.DrawOne(text, level);
        }
        ImGui.EndChild();
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

        if (voice is { Dropped: > 0 })
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Muted),
                $"{voice.Spoken} read out, {voice.Dropped} skipped for coming too fast.");
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
        return v.Spoken > 0 ? $"{v.Spoken} read out, {which}" : $"On, {which}";
    }

    private void DrawLocalVoice()
    {
        if (Runner?.LocalVoice is not { } voice) return;

        Widgets.GroupLabel("Local Voice");
        Widgets.ListBegin();

        var useLocal = C.UseLocalVoice;
        if (Widgets.RowCheck("Use it", "off reads out with the system voice", ref useLocal,
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

    // Whether a setting has been moved off what it ships as.
    private bool Changed(string prop) => SettingsIndex.IsChanged(C, prop);
}
