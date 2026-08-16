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
            if (Widgets.RowCheckClick("Turn Calls Back On", "", ref on,
                FontAwesomeIcon.Bell, Theme.Warn))
            { C.AlertsEnabled = on; C.Save(); }
        }

        if (Widgets.RowDoor("Call Display", "", FontAwesomeIcon.Desktop, Theme.Accent))
            _nav = NavKind.CallDisplay;

        if (Widgets.RowDoor("Appearance", "", FontAwesomeIcon.Palette, Theme.Accent))
            _nav = NavKind.Appearance;

        Widgets.ListEnd();
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
        if (Runner is { ControlAvailable: false }) problems.Add("No direction calls");
        if (Runner is { AbilitiesAvailable: false }) problems.Add("No hit calls");
        if (Runner is { LocalVoice.GivenUp: true }) problems.Add("Local voice gave up");
        // Three states, and only one of them is fine. Still shaking hands is worth
        // nothing on the list: it settles on its own in a second or two.
        if (Runner is { ParserReading: false, ParserAsking: false })
            problems.Add(Runner is { ParserConnected: true }
                ? "The parser never answered, head markers are quiet"
                : "No parser, head markers are quiet");
        if (C.TestMode) problems.Add("Test mode is on");

        if (HomeTile("##t1", w, h, FontAwesomeIcon.Bell,
            Theme.V(C.AlertsEnabled ? Theme.Good : Theme.Muted),
            "Calls",
            C.AlertsEnabled ? "Running" : "Off",
            Theme.V(C.AlertsEnabled ? Theme.Good : Theme.Muted),
            fights == 0 ? "Nothing to call yet" : $"{fights} fight{(fights == 1 ? "" : "s")} loaded"))
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
        _callView = 0;
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
            Widgets.RowNote(FightCatalog.PackProblem
                ?? $"All {fight.BuiltIn} of this fight's calls are built in.");
            Widgets.ListEnd();
            return;
        }

        var phase = DrawPhaseTabs(calls);

        // A hundred calls is too many to walk, so the words are searchable.
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##callfilter", "Search these calls", ref _callFilter, 64);
        ImGui.Spacing();

        var here = phase is { } only ? calls.Where(c => c.Phase == only).ToList() : calls;

        var dead = here.Count(c => !LiveCoverage.Covered(c.On));
        if (dead > 0)
        {
            ImGui.TextColored(Theme.V(Theme.Warn),
                $"{dead} of these never fire: nothing on the game side sends what they wait for.");
            ImGui.Spacing();
        }

        var waiting = here.Count(NeedsParser);
        if (waiting > 0)
        {
            ImGui.TextColored(Theme.V(Theme.Warn),
                $"{waiting} of these wait on head markers from a parser, and none are arriving.");
            ImGui.Spacing();
        }

        var loose = here.Count(c => !c.Exact);
        if (loose > 0)
        {
            Widgets.SegmentBegin();
            if (Widgets.Segment($"All {here.Count}", _callView == 0)) _callView = 0;
            if (Widgets.Segment($"Exact {here.Count - loose}", _callView == 1)) _callView = 1;
            if (Widgets.Segment($"Not exact {loose}", _callView == 2)) _callView = 2;
            Widgets.SegmentEnd();

            // An unchecked call ships silent, so this is the way to hear the lot of
            // them in one click rather than ninety, and back again.
            var quiet = here.Count(c => !c.Exact && C.IsCallOn(c.Key, c.ShipsOn));
            if (_callView == 2 && quiet > 0)
            {
                ImGui.SameLine(0, Theme.S(10f));
                if (Widgets.DangerButton($"Turn off all {quiet}"))
                    foreach (var c in here.Where(c => !c.Exact && C.IsCallOn(c.Key, c.ShipsOn)))
                        C.SetCallOn(c.Key, c.ShipsOn, false);
                Tip("Leaves the exact ones alone.");
            }
            else if (_callView == 2 && loose > 0)
            {
                ImGui.SameLine(0, Theme.S(10f));
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, "Turn them all on"))
                    foreach (var c in here.Where(c => !c.Exact))
                        C.SetCallOn(c.Key, c.ShipsOn, true);
            }
            ImGui.Spacing();
        }

        var shown = here
            .Where(c => _callView switch { 1 => c.Exact, 2 => !c.Exact, _ => true })
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

        if (fight.BuiltIn > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Muted),
                $"{fight.BuiltIn} more are built in, not editable here.");
        }

        // Rows that would only have said the mechanic's name. Counted rather than
        // hidden: a fight that lists fewer calls than the strat sheet has mechanics
        // reads like something failed to load.
        var trimmed = Trimmed.Count((ushort)fight.TerritoryId);
        if (trimmed > 0)
        {
            if (fight.BuiltIn == 0) ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Muted),
                $"{trimmed} more only name the mechanic, so they wait until they can say where to go.");
        }
    }

    private int? DrawPhaseTabs(IReadOnlyList<CallEntry> calls)
    {
        var phases = calls.Select(c => c.Phase).Distinct().OrderBy(p => p).ToList();
        // One phase is not phases: a fight where every call sits in the same bucket
        // gets no tabs at all rather than a row with one tab on it.
        if (phases.Count(p => p > 0) < 2) return null;

        int? picked = null;
        if (ImGui.BeginTabBar("##phases", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (ImGui.BeginTabItem("All")) ImGui.EndTabItem();

            foreach (var p in phases.Where(p => p > 0))
            {
                var n = calls.Count(c => c.Phase == p);
                if (ImGui.BeginTabItem($"P{p} ({n})###phase{p}"))
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

        var dead = !LiveCoverage.Covered(call.On);
        var quiet = NeedsParser(call);
        var note = dead ? "Never fires"
            : quiet ? "Needs a parser"
            : !call.Exact && _callView == 0 ? "Not exact" : "";
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
        if (Widgets.RowCheckClick("Lock Position", "", ref locked,
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
        var which = Runner?.LocalVoice is { GivenUp: false, Pack.Ready: true }
            ? "local voice" : "system voice";
        return v.Spoken > 0 ? $"{v.Spoken} read out, {which}" : $"On, {which}";
    }

    private void DrawLocalVoice()
    {
        if (Runner?.LocalVoice is not { } voice) return;

        Widgets.GroupLabel("Local Voice");
        Widgets.ListBegin();

        Widgets.RowNote(voice.Describe());
        Widgets.ListEnd();

        // Three failed starts is broken rather than unlucky, and it falls back
        // silently, so the one state worth colouring is that one.
        if (voice.GivenUp)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Warn), "It kept stopping, so it is not being started again.");
        }

        Widgets.ListBegin();
        if (!voice.Pack.Ready)
            foreach (var piece in voice.Pack.Missing)
                Widgets.RowNote($"{piece.Name}, {AsSize(piece.Bytes)}");
        Widgets.ListEnd();

        if (!voice.Pack.Ready)
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
