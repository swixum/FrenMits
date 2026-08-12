using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;

namespace FrenMits.Planning;

// Sheet View: the toolbar and the small dialogs it opens (auto-plan, add row,
// delete sheet, plan snapshots, custom rows).
public partial class SheetViewWindow
{
    // ---- auto-plan mits (custom sheets) -------------------------------------

    private bool _openAutoPlan;
    private static readonly string[] HealerJobs = { "WHM", "AST", "SCH", "SGE" };

    // Generic healer seats (H1, H2, H...) on this sheet: the four healer jobs'
    // kits barely overlap, so these seats cannot be planned honestly by name.
    private List<string> GenericHealerCols()
        => _fight == null ? new List<string>() : _fight.CustomSlots
            .Where(sl => sl.Trim().ToUpperInvariant().StartsWith("H")
                         && !JobPartyKit.ContainsKey(sl.Trim()))
            .ToList();

    // Turn generic healer seats into ALL FOUR healer job columns, the way the
    // official sheets carry WHM/AST/SCH/SGE side by side so any comp finds its
    // column.
    private void ExpandHealerSeats(FightProfile fight)
    {
        var seats = GenericHealerCols();
        if (seats.Count == 0) return;
        var jobs = HealerJobs.Where(j => !fight.CustomSlots.Contains(j, StringComparer.OrdinalIgnoreCase)).ToList();
        if (jobs.Count == 0) return;

        var assign = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var myJob = _plugin.ActiveJobAbbreviation();
        if (myJob != null
            && jobs.Contains(myJob, StringComparer.OrdinalIgnoreCase)
            && seats.Contains(fight.Slot, StringComparer.OrdinalIgnoreCase))
        {
            assign[fight.Slot] = myJob.ToUpperInvariant();
            jobs.RemoveAll(j => string.Equals(j, myJob, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var seat in seats)
        {
            if (assign.ContainsKey(seat) || jobs.Count == 0) continue;
            assign[seat] = jobs[0];
            jobs.RemoveAt(0);
        }

        var lastHealerIdx = -1;
        foreach (var (seat, job) in assign)
        {
            var idx = fight.CustomSlots.FindIndex(sl => string.Equals(sl, seat, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) continue;
            fight.CustomSlots[idx] = job;
            lastHealerIdx = Math.Max(lastHealerIdx, idx);
            if (fight.SavedSlots.TryGetValue(seat, out var moved))
            {
                fight.SavedSlots.Remove(seat);
                fight.SavedSlots[job] = moved;
            }
            if (string.Equals(fight.Slot, seat, StringComparison.OrdinalIgnoreCase)) fight.Slot = job;
        }
        // The healer jobs no seat was left for still get their column, so the
        // sheet covers every healer like the official ones do.
        foreach (var job in jobs)
        {
            if (fight.CustomSlots.Count >= 12) break;
            fight.CustomSlots.Insert(lastHealerIdx >= 0 ? ++lastHealerIdx : fight.CustomSlots.Count, job);
        }
    }

    private void DrawAutoPlanPopup()
    {
        var stay = true;
        if (!ImGui.BeginPopupModal("##autoplan", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;

        PopupHeader("Auto-Plan Mits", 520f);
        if (_fight == null || !_isCustom)
        {
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }
        if (_fight.CustomRows.Count == 0)
        {
            ImGui.TextUnformatted("Want the mits planned for you? Add the mechanics first.");
            ImGui.TextDisabled("Build > Add Row (or Build from Pull / Build from FFLogs) creates the");
            ImGui.TextDisabled("rows; then Build > Auto-Plan Mits fills every column with cooldowns");
            ImGui.TextDisabled("that line up: spaced to their recasts, spread across the party.");
            ImGui.Spacing();
            if (ImGui.Button("Got It", Theme.Sz(110f))) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        var gradedRows = _fight.CustomRows.Count(r => r.Hurt > 0);
        ImGui.TextUnformatted($"Fill the grid with party cooldowns for {_fight.CustomRows.Count} rows?");
        ImGui.PushTextWrapPos(Theme.S(500f));
        ImGui.TextDisabled("Planned the way the official sheets play it: stacked deep on the deadly "
                           + "hits, spread by recast, and your own cells are never touched.");
        ImGui.PopTextWrapPos();
        ImGui.Spacing();

        // A stat row, so the shape of the job lands before any prose.
        Widgets.Chip("Rows", _fight.CustomRows.Count.ToString(), Theme.TextBright);
        ImGui.SameLine(0, Theme.S(8f));
        Widgets.Chip("Graded", gradedRows.ToString(), gradedRows > 0 ? Theme.Good : Theme.Muted);
        ImGui.SameLine(0, Theme.S(8f));
        Widgets.Chip("Columns", _fight.CustomSlots.Count.ToString(), Theme.TextBright);
        ImGui.Spacing();

        // Auto-built rows arrive ungraded, so say how to sharpen the plan.
        if (gradedRows == 0)
        {
            ImGui.PushTextWrapPos(Theme.S(500f));
            ImGui.TextDisabled("No hits are graded yet, so every row plans the same. Build from a kill log "
                               + "to mark the deadly ones, or set a row's grade by hand.");
            ImGui.PopTextWrapPos();
            ImGui.Spacing();
        }

        // The detail is here for whoever wants it, folded away for everyone else.
        if (ImGui.TreeNode("How it plans"))
        {
            ImGui.PushTextWrapPos(Theme.S(500f));
            ImGui.TextDisabled("Deadly hits stack the whole party and healers pair big mits. Hurts takes "
                               + "about half, light gets one press. Long cooldowns are saved for the big hits so "
                               + "they line up, and anything that is back and not owed to a deadly hit goes on "
                               + "the next one, so healer kits never sit unused.");
            ImGui.PopTextWrapPos();
            ImGui.TreePop();
        }
        if (ImGui.TreeNode("Special cases"))
        {
            ImGui.PushTextWrapPos(Theme.S(500f));
            ImGui.TextDisabled("On-damage cooldowns (Liturgy of the Bell, Panhaima, Macrocosmos) are held "
                               + "for multi-hit strings where they tick. Reprisal, Feint and Addle are never "
                               + "doubled on one hit; sources rotate instead. Buster rows get the tanks' own "
                               + "plan: the taker alternates, deadly ones draw an invuln, the rest take Rampart "
                               + "plus a short mit while the co-tank sends Buddy Mit.");
            ImGui.TextDisabled("Columns named for a job (WHM, SGE, MCH...) plan with that job's real kit; "
                               + "other role columns (MT, D3...) get terms that speak as each player's own "
                               + "ability. Recasts are always respected.");
            ImGui.PopTextWrapPos();
            ImGui.TreePop();
        }

        // Healer seats: the four healer jobs' kits barely overlap, so the
        // sheets carry a column per healer JOB.
        var healerCols = GenericHealerCols();
        ImGui.PushTextWrapPos(Theme.S(500f));
        if (healerCols.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Good),
                "Healer seats become WHM, AST, SCH and SGE columns, like the official sheets, so "
                + "every healer job gets its real cooldowns planned. Pick your own column after "
                + "planning, from a column header or the fight page.");
        }
        var noKit = _fight.CustomSlots.Where(sl => PoolFor(sl).Length == 0).ToList();
        if (noKit.Count > 0)
            ImGui.TextColored(Theme.V(Theme.Warn),
                $"No kit for: {string.Join(", ", noKit)}. Rename to a job (WHM) or role (H1, D3) to include them.");
        if (gradedRows == 0)
            ImGui.TextDisabled("Tip: import a kill log and rows get graded by real unmitigated damage, "
                               + "which lets the planner set its own stacking depth.");
        ImGui.PopTextWrapPos();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        if (ImGui.Button("Plan Mits", Theme.Sz(110f)))
        {
            PushUndo("auto-plan mits");
            _plugin.Snapshots.Save(_fight, "before auto-plan");
            // Healer seats become ALL FOUR healer job columns first (the
            // sheets' convention), so every healer's kit is covered.
            ExpandHealerSeats(_fight);
            var n = AutoPlanMits(_fight);
            C.Save();
            _dirty = true;
            var healersNote = healerCols.Count > 0
                ? " Healer columns are per job now; pick yours from its header (right-click) or the fight page."
                : "";
            Flash(n > 0
                ? $"Planned {n} calls. Undo (Ctrl+Z) or Plan > History reverts.{healersNote}"
                : "Nothing to add: every row is already covered.");
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.SameLine();
        if (ImGui.Button("Not Now", Theme.Sz(110f))) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private void PickCustomSlot(string slot)
    {
        if (_fight == null) return;
        _fight.Slot = slot;
        // Any lines the fight already had become this column's plan.
        if (_fight.SavedSlots.TryGetValue(slot, out var saved)) _fight.Lines = saved;
        _fight.SavedSlots[slot] = _fight.Lines;
        C.Save();
        _dirty = true;
    }

    // Move "(you)" to another column of a custom sheet (header right-click).
    private void SwitchCustomSlot(int i)
    {
        if (_fight == null) return;
        CommitPending();
        var slot = _slots[i];
        if (!string.IsNullOrEmpty(_fight.Slot)) _fight.SavedSlots[_fight.Slot] = _fight.Lines;
        _fight.Slot = slot;
        _fight.Lines = _fight.SavedSlots.TryGetValue(slot, out var saved) ? saved : new List<MitLine>();
        _fight.SavedSlots[slot] = _fight.Lines;
        C.Save();
        _dirty = true;
        Flash($"{slot} is your column now; the overlay calls that plan.");
    }

    // The plugin-wide hover delay, so tooltips here match every other window.
    private static bool DelayedHover(ImGuiHoveredFlags flags = ImGuiHoveredFlags.None)
        => Widgets.HoveredDelayed(flags);

    private void DrawToolbar()
    {
        DrawFightPicker();

        // Phase filter, as one segmented control rather than loose buttons.
        DrawPhaseSegments();

        // Text filter across mechanics and mits ("Reprisal" = every Reprisal row).
        ImGui.SameLine(0, Theme.S(10f));
        ImGui.SetNextItemWidth(Theme.S(140f));
        ImGui.InputTextWithHint("##sheetfilter", "filter...", ref _filter, 64);
        if (DelayedHover() && !ImGui.IsItemActive())
            ImGui.SetTooltip("Show only rows whose mechanic or any slot's mit contains this text.");
        if (_filter.Length > 0)
        {
            ImGui.SameLine(0, Theme.S(2f));
            if (ImGui.SmallButton("x##clearfilter")) _filter = "";
        }

        // What the grid shows, all in one menu instead of four loose checkboxes.
        ImGui.SameLine(0, Theme.S(12f));
        DrawViewMenu();

        ImGui.SameLine(0, Theme.S(8f));
        var filtered = _phaseFilter.Length > 0 || _filter.Length > 0 || !_showJobExtra || _clashOnly;
        var shown = _rows.Count(r => !r.Ghost
            && (_phaseFilter.Length == 0 || r.Phase == _phaseFilter)
            && (_showJobExtra || !r.JobExtra) && MatchesFilter(r));
        ImGui.TextDisabled(filtered
            ? $"·  {shown} of {_rows.Count(r => !r.Ghost)} mechanics"
            : $"·  {_rows.Count(r => !r.Ghost)} mechanics, {_slots.Length} slots");

        // Cooldown clashes, and a way to see only those rows.
        if (_clashRowCount > 0 || _clashOnly)
        {
            ImGui.SameLine(0, Theme.S(10f));
            if (Widgets.ChipButton("Clashes", _clashRowCount.ToString(), Theme.Danger, _clashOnly))
            {
                CommitPending();
                _clashOnly = !_clashOnly;
            }
            // Says "every column", since the fight page counts your slot alone
            // and the two numbers are meant to differ.
            if (DelayedHover())
                ImGui.SetTooltip(_clashOnly
                    ? "Showing only rows where a mit repeats before its cooldown is back, across every column.\nClick to show them all."
                    : "Rows where a mit repeats before its cooldown is back, across every column.\nClick to show only those.");
        }

        // The how-to lives here now instead of a permanent footer line.
        ImGui.SameLine(0, Theme.S(8f));
        ImGui.TextDisabled("(?)");
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip(
                "Click a time to re-time a mechanic for every slot; click a cell to edit that slot only.\n"
                + "While editing: Enter moves down, Tab moves right. Ctrl+Z undoes any edit.\n"
                + "Orange text = your edit; red cell = cooldown conflict; amber = above the duty's level sync; dim = deleted.\n"
                + "A faint -> means an earlier press's buff still covers that hit (carry-over).\n"
                + "Drag column edges to resize (double-click to fit) or drag headers to reorder.\n"
                + "Right-click cells, mechanics and column headers; most tools live there.");

        // Right side: Undo | Build (custom sheets) | Plan | Share plan. Measured
        // off the real frame padding, so the block lands right at any scale.
        var tbStyle = ImGui.GetStyle();
        float BtnW(string s) => ImGui.CalcTextSize(s).X + tbStyle.FramePadding.X * 2f;
        var rightW = BtnW("Undo") + BtnW("Plan") + BtnW("Share Plan")
                   + (_isCustom ? BtnW("Build") + tbStyle.ItemSpacing.X : 0f)
                   + tbStyle.ItemSpacing.X * 3f;
        // Off the last item, not the cursor: the cursor is back at the line
        // start by now, which would let this block sit on top of the left side.
        var tbLineEnd = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
        ImGui.SameLine(MathF.Max(tbLineEnd + Theme.S(8f), ImGui.GetContentRegionMax().X - rightW));
        ImGui.BeginDisabled(_undoStack.Count == 0);
        if (ImGui.SmallButton("Undo")) Undo();
        ImGui.EndDisabled();
        if (DelayedHover(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(_undoStack.Count == 0
                ? "Nothing to undo yet. Ctrl+Z also works."
                : $"Undo: {_undoStack[^1].Label} (Ctrl+Z). Restores the plan to how it was before that edit.");

        // Deferred popup opens: OpenPopup can't run inside another popup's
        // scope, so menu items set flags and the popups open out here.
        var openReplace = false;
        var openHistory = false;
        var openAddRow = false;
        var openBuildPull = false;
        var openLog = false;
        // The "no pulls yet" state offers the log route; it opens out here.
        if (_openLogAfterPull) { _openLogAfterPull = false; openLog = true; }
        var openDelete = false;

        if (_isCustom)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Build")) ImGui.OpenPopup("##buildmenu");
            if (DelayedHover())
                ImGui.SetTooltip("Grow this sheet: add rows by hand, from your own pulls, or from an FFLogs kill.");
            if (ImGui.BeginPopup("##buildmenu"))
            {
                if (ImGui.MenuItem("Add Row...")) openAddRow = true;
                if (ImGui.MenuItem("Build from Pull...")) openBuildPull = true;
                if (ImGui.MenuItem("Build from FFLogs...")) openLog = true;
                if (Widgets.HoveredDelayed())
                    ImGui.SetTooltip("Type a fight name to pull its current top kill, or paste a specific\nlog. Its casts become rows + anchors, graded by real damage.");
                ImGui.Separator();
                if (ImGui.MenuItem("Auto-Plan Mits...")) _openAutoPlan = true;
                if (Widgets.HoveredDelayed())
                    ImGui.SetTooltip("Fills the grid with party cooldowns for every row: spaced to each\nrecast, rotated across columns, never overwriting your own cells.");
                ImGui.EndPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Plan")) ImGui.OpenPopup("##planmenu");
        if (DelayedHover())
            ImGui.SetTooltip("Export / import, bulk replace, plan history, and view options.");
        if (ImGui.BeginPopup("##planmenu"))
        {
            // Land any half-typed edit first, so the clipboard never captures
            // a pre-edit grid.
            if (ImGui.MenuItem("Export as Text"))
            {
                CommitPending();
                if (_dirty) Rebuild();
                ExportText();
            }
            if (ImGui.MenuItem("Import Plan Code")) ImportPlan();
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip("Paste a friend's Share plan code from your clipboard.\nTheir slot is replaced; your other slots are kept.");
            if (ImGui.MenuItem("Replace a Mit...")) openReplace = true;
            if (ImGui.MenuItem("Plan History...")) openHistory = true;
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip("Snapshots taken automatically before imports, replaces and\ncolumn pastes; restore any of them.");

            if (!_isCustom && ImGui.MenuItem("Reset All Columns...")) _openResetAll = true;
            if (!_isCustom && Widgets.HoveredDelayed())
                ImGui.SetTooltip("Reload EVERY column from the baked sheet: all slots' edits and\ndeletions go, including added potion, job and tank lines.\nA snapshot is saved first; Plan > History restores it.");
            if (ImGui.MenuItem("Open Fight Page")) _plugin.ConfigWindow.OpenFightPage(_fight!);
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip("Per-line options, anchors and import tools live there.");
            if (ImGui.MenuItem("Open Mit Tuner")) _plugin.MiniSheetWindow.IsOpen = true;
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip("A pocket version for mid-pull use: the calls around now,\neach with +/- nudges for its offset. Also /fm mini.");
            if (_isCustom)
            {
                ImGui.Separator();
                if (ImGui.MenuItem("Delete This Sheet...")) openDelete = true;
            }
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        if (ImGui.SmallButton("Share Plan")) SharePlan();
        ImGui.PopStyleColor(2);
        if (DelayedHover())
            ImGui.SetTooltip("Copy the whole plan as a clipboard code. Friends use Plan > Import plan code\n(or the fight page); it updates their fight in place (their slot's plan).");

        if (openReplace) { _replFind = _filter; ImGui.OpenPopup("##sheetreplace"); }
        DrawReplacePopup();
        if (openHistory)
        {
            _snapList = _plugin.Snapshots.List(_fight!.Id);
            ImGui.OpenPopup("##sheethistory");
        }
        DrawHistoryPopup();
        if (openDelete) ImGui.OpenPopup("##sheetdelete");
        DrawDeleteSheetPopup();
        // Deleting the sheet nulls _fight mid-frame: stop the toolbar here so
        // nothing after it can touch the gone fight this frame.
        if (_fight == null) return;
        // Deferred like the rest: the request can come from the Plan menu or from
        // a cell's right-click menu (a different ID scope), so it rides a flag.
        if (_openResetAll) { _openResetAll = false; ImGui.OpenPopup("##sheetresetall"); }
        DrawResetAllPopup();
        if (_isCustom)
        {
            if (openAddRow) { _rowMech = ""; _rowTime = ""; _rowHurt = 0; ImGui.OpenPopup("##addrow"); }
            DrawAddRowPopup();
            if (openBuildPull) ImGui.OpenPopup("##buildpull");
            DrawBuildFromPullPopup();
            if (openLog) ImGui.OpenPopup("##fflogs");
            DrawFFLogsPopup();
            // Also set right after Create, so a fresh sheet offers the plan.
            if (_openAutoPlan) { _openAutoPlan = false; ImGui.OpenPopup("##autoplan"); }
            DrawAutoPlanPopup();
        }
    }

    private void DrawAddRowPopup()
    {
        // Modal so a stray click outside cannot dismiss the form; the X,
        // Escape, or its own buttons close it.
        var stay = true;
        if (!ImGui.BeginPopupModal("##addrow", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;
        PopupHeader("Add a row", 320f);
        ImGui.SetNextItemWidth(Theme.S(200f));
        ImGui.InputTextWithHint("##armech", "mechanic name", ref _rowMech, 64);
        ImGui.SetNextItemWidth(Theme.S(200f));
        ImGui.InputTextWithHint("##artime", "time (m:ss or seconds)", ref _rowTime, 16);
        ImGui.SetNextItemWidth(Theme.S(200f));
        ImGui.Combo("hits##arhurt", ref _rowHurt, HurtChoices, HurtChoices.Length);
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip("How hard the hit is unmitigated. Auto-Plan stacks mitigation deeper\non harder hits; log imports grade this automatically from real damage.");
        var okRow = _rowMech.Trim().Length > 0 && SheetImport.TryParseTime(_rowTime, out _);
        ImGui.BeginDisabled(!okRow);
        if (ImGui.Button("Add Row", Theme.Sz(110f)))
        {
            SheetImport.TryParseTime(_rowTime, out var t);
            AddCustomRow(_rowMech.Trim(), t, _rowHurt);
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    // Import a friend's plan code from the clipboard, then jump to the fight it
    // touched so the result is on screen immediately.
    private void ImportPlan()
    {
        CommitPending();
        var (fight, _, message) = PlanCodes.Import(_plugin, ImGui.GetClipboardText());
        if (fight != null)
        {
            // Ctrl+Z entries older than the import would also revert the import
            // under a misleading label; the pre-import disk snapshot (History)
            // is the way back instead.
            _undoStack.RemoveAll(s => s.Fight == fight);
            if (Sheetable(fight))
            {
                _fight = fight;
                _phaseFilter = "";
            }
        }
        _dirty = true;
        Flash(message);
    }

    // All + one segment per phase, joined into a single control.
    private void DrawPhaseSegments()
    {
        Widgets.SegmentBegin();
        PhaseButton("All", _phaseFilter.Length == 0);
        foreach (var (name, _) in _phases)
        {
            ImGui.SameLine();
            PhaseButton(name, _phaseFilter == name);
        }
        Widgets.SegmentEnd();
    }

    private void PhaseButton(string name, bool on)
    {
        if (!Widgets.Segment($"{name}###ph{name}", on)) return;
        // Land any open editor BEFORE the filter hides its row, or the edit
        // state would linger unseen (blocking rebuilds) until a later click.
        CommitPending();
        _phaseFilter = name == "All" ? "" : name;
    }

    // Everything that changes what the grid shows, in one place.
    private void DrawViewMenu()
    {
        if (ImGui.SmallButton("View")) ImGui.OpenPopup("##viewmenu");
        if (DelayedHover())
            ImGui.SetTooltip("Which rows and mits the grid shows, and how they're colored.");
        if (!ImGui.BeginPopup("##viewmenu")) return;

        ImGui.MenuItem("Party Mits", "", ref _showPartyMits);
        ImGui.MenuItem("Personal / role mits", "", ref _showPersonalMits);
        ImGui.MenuItem("Job Extras", "", ref _showJobExtra);
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip("Job-specific extras (Mantra, Curing Waltz, ...) that ride into the plan\non their own, at their own time. Untick to hide their rows.");

        var showEmpty = C.ShowEmptyMechanics;
        if (ImGui.MenuItem("Empty Mechanics", "", ref showEmpty))
        {
            C.ShowEmptyMechanics = showEmpty;
            C.Save();
            _dirty = true;
        }
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip("Show mechanics with no mit assigned in any slot (raidwides, autos, ...) as blank reference rows.");

        if (ImGui.MenuItem("Color Mits by Type", "", C.SheetColorByType))
        {
            C.SheetColorByType = !C.SheetColorByType;
            C.Save();
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Reset Column Widths"))
        {
            // Saved, so the reset is still in force after a restart.
            C.SheetWidthReset++;
            C.SaveSettings();
            Flash("Columns back to their standard widths.");
        }
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip("Undo any column dragging. Double-clicking one edge still fits that column.");
        ImGui.EndPopup();
    }

    // Deleting a whole custom sheet: confirmed, snapshotted first, undoable
    // only via History after recreating a sheet in the same duty.
    private void DrawDeleteSheetPopup()
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##sheetdelete", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        ImGui.TextUnformatted($"Delete \"{_fight!.Name}\"?");
        ImGui.TextColored(Theme.V(Theme.Warn), "Every column's plan, rows, notes and learned anchors go with it.");
        ImGui.TextDisabled("A snapshot is saved first. To recover: recreate a sheet in this duty,");
        ImGui.TextDisabled("then History > Find This Duty's Older Snapshots.");
        ImGui.Spacing();

        if (ImGui.Button("Cancel", Theme.Sz(120f))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        if (Widgets.DangerButton("Delete", Theme.Sz(120f)))
        {
            var f = _fight!;
            _plugin.Snapshots.Save(f, "before delete");
            _undoStack.RemoveAll(u => u.Fight == f);
            C.Fights.Remove(f);
            C.Save();
            _fight = null;
            _dirty = true;
            ImGui.CloseCurrentPopup();
            Flash($"\"{f.Name}\" deleted. A snapshot was kept.");
        }
        ImGui.EndPopup();
    }

    // ---- plan snapshots (History) -------------------------------------------

    private List<SnapshotStore.SnapshotInfo> _snapList = new();

    private void DrawHistoryPopup()
    {
        // Modal so a stray click outside cannot dismiss the form; the X,
        // Escape, or its own buttons close it.
        var stay = true;
        if (!ImGui.BeginPopupModal("##sheethistory", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;

        PopupHeader("Plan snapshots (this fight)", 440f);
        if (ImGui.SmallButton("Snapshot Now"))
        {
            _plugin.Snapshots.Save(_fight!, "manual snapshot");
            _snapList = _plugin.Snapshots.List(_fight!.Id);
            Flash("Snapshot saved.");
        }
        ImGui.Separator();

        if (_snapList.Count == 0)
        {
            ImGui.TextUnformatted("No snapshots");
            ImGui.PushTextWrapPos(Theme.S(420f));
            ImGui.TextDisabled("One is saved automatically before every import, replace, column paste "
                               + "and sheet refresh, so there is always a way back.");
            ImGui.PopTextWrapPos();
        }
        foreach (var s in _snapList)
        {
            ImGui.TextUnformatted($"{s.When:MMM d, h:mm tt}");
            ImGui.SameLine(0, Theme.S(8f));
            ImGui.TextDisabled(s.Reason);
            ImGui.SameLine(0, Theme.S(12f));
            if (ImGui.SmallButton($"Restore##{s.File}"))
            {
                CommitPending();
                PushUndo("restore snapshot"); // restoring is itself undoable
                var msg = _plugin.Snapshots.Restore(_fight!, s.File);
                _dirty = true;
                Flash(msg);
                ImGui.CloseCurrentPopup();
            }
        }

        // Recovery for deleted sheets: their snapshots survive under the old
        // fight id; find them by duty and restore into THIS sheet.
        if (_isCustom)
        {
            ImGui.Spacing();
            if (ImGui.SmallButton("Find This Duty's Older Snapshots"))
                _snapList = _plugin.Snapshots.List(_fight!.Id)
                    .Concat(_plugin.Snapshots.ListOrphans(_fight.TerritoryId, _fight.Id))
                    .ToList();
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip("Lists snapshots from sheets you previously deleted in this duty,\nso a deleted sheet can be restored here.");
        }
        ImGui.EndPopup();
    }

    // ---- custom rows ---------------------------------------------------------

    private string _rowMech = "";
    private string _rowTime = "";
    private int _rowHurt;

    // Combo labels for CustomRow.Hurt (index == the stored value).
    private static readonly string[] HurtChoices = { "not graded", "light", "hurts", "deadly" };

    private CustomRow? CustomRowFor(Row row)
        => _fight?.CustomRows.FirstOrDefault(cr =>
            MechEquals(cr.Mechanic, row.Mechanic) && MathF.Abs(cr.Time - row.Time) < 2f);

    private void AddCustomRow(string mech, float time, int hurt = 0)
    {
        if (_fight == null || AbortIfStale()) return;
        if (_rows.Any(r => !r.Ghost && MechEquals(r.Mechanic, mech) && MathF.Abs(r.Time - time) < 2f))
        {
            Flash($"\"{mech}\" already has a row near {TimeText(time)}.");
            return;
        }
        PushUndo($"add \"{mech}\" row");
        _fight.CustomRows.Add(new CustomRow { Time = time, Mechanic = mech, Hurt = hurt });
        C.Save();
        _dirty = true;
        Flash($"\"{mech}\" added at {TimeText(time)}. Click its cells to write mits.");
    }

    // Delete a custom-sheet row: its scaffold entry and every column's lines.
    private void DeleteCustomRow(Row row)
    {
        if (_fight == null || row.Ghost || AbortIfStale()) return;
        PushUndo($"delete \"{row.Mechanic}\" row");
        var processedSlots = new HashSet<int>();
        for (var i = 0; i < _gridCols.Length; i++)
        {
            if (row.Cells[i].Count == 0) continue;
            var slotIdx = _gridToSlot[i];
            if (!processedSlots.Add(slotIdx)) continue;
            EnsureBacked(slotIdx);
            foreach (var l in row.Cells[i].ToList()) _slotLines[slotIdx].Remove(l);
            Resort(slotIdx);
        }
        _fight.CustomRows.RemoveAll(cr =>
            MechEquals(cr.Mechanic, row.Mechanic) && MathF.Abs(cr.Time - row.Time) < 2f);
        C.Save();
        _dirty = true;
        Flash($"\"{row.Mechanic}\" removed. Ctrl+Z brings it back.");
    }
}
