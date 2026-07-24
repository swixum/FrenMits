using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// Sheet View: exporting a plan, and drawing the grid itself - every cell, the
// phase pill, the footer and the suggest menu.
public partial class SheetViewWindow
{
    // ---- export -------------------------------------------------------------

    // The whole grid as tab-separated text: pastes into Google Sheets / Excel
    // as real columns, and reads fine in Discord.
    private void ExportText()
    {
        if (_fight == null) return;
        var sb = new System.Text.StringBuilder();
        sb.Append("Time\tMechanic");
        foreach (var i in _order) sb.Append('\t').Append(_slots[i]);
        var anyNotes = _fight.Notes.Count > 0;
        if (anyNotes) sb.Append("\tNotes");
        sb.AppendLine();

        var lastPhase = "";
        foreach (var row in _rows)
        {
            if (row.Ghost) continue;
            if (row.Phase != lastPhase)
            {
                lastPhase = row.Phase;
                sb.Append('\t').Append(Builtin.PhaseTitle(_fight.TerritoryId, row.Phase)).AppendLine();
            }
            sb.Append(TimeText(row.Time)).Append('\t').Append(TsvCell(row.Mechanic));
            foreach (var i in _order)
                sb.Append('\t').Append(TsvCell(string.Join(" + ", row.Cells[i].Select(l => l.Action))));
            if (anyNotes) sb.Append('\t').Append(TsvCell(NoteFor(row)?.Text ?? ""));
            sb.AppendLine();
        }

        foreach (var (_, title, text) in _phaseNotes)
        {
            sb.AppendLine();
            sb.AppendLine(title);
            sb.AppendLine(text);
        }

        ImGui.SetClipboardText(sb.ToString());
        Flash("Plan copied as text. Paste into Google Sheets / Excel (lands in columns) or Discord.");
    }

    // Imported plans can carry arbitrary text; tabs/newlines inside a cell
    // would shift or split the TSV row.
    private static string TsvCell(string s)
        => s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

    private bool MatchesFilter(Row row)
    {
        if (_filter.Length == 0) return true;
        if (row.Mechanic.Contains(_filter, StringComparison.OrdinalIgnoreCase)) return true;
        var cells = row.Ghost ? row.Bake!.Cells : row.Cells;
        foreach (var cell in cells)
            foreach (var l in cell)
                if (l.Action.Contains(_filter, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static readonly string[] TankSlots = { "MT", "OT", "T1", "T2", "T" };
    private static readonly string[] HealSlots = { "WHM", "AST", "SCH", "SGE", "H1", "H2", "H" };

    private static Vector4 RoleColor(string slot)
        => TankSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? ImGuiColors.TankBlue
         : HealSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? ImGuiColors.HealerGreen
         : ImGuiColors.DPSRed;

    private static readonly Vector4 EditedColor = new(0.96f, 0.62f, 0.36f, 1f);
    private static readonly Vector4 NoteBlue = new(0.42f, 0.66f, 0.96f, 1f);
    private const uint YouCellBg = 0x2233AA33;   // faint green tint (ABGR)
    private const uint WarnCellBg = 0x483040E6;  // translucent red: cooldown conflict
    private const uint LevelCellBg = 0x4820A0E0; // translucent amber: above level sync

    // The game font has no glyphs for symbols like a star, pen, or undo arrow
    // (they render as an empty box), so every symbol is drawn with the icon font.
    private static void IconText(FontAwesomeIcon icon, Vector4 color)
    {
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            ImGui.TextColored(color, icon.ToIconString());
    }

    private static bool IconSmallButton(FontAwesomeIcon icon, string id)
    {
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            return ImGui.SmallButton(icon.ToIconString() + id);
    }

    // Header row for the Sheet View popups: a dim title plus a right-aligned X,
    // so every menu shows a visible way out (Esc and clicking outside still work).
    private static void PopupHeader(string title, float width)
    {
        ImGui.TextDisabled(title);
        var titleEnd = ImGui.GetItemRectSize().X + 24f;
        ImGui.SameLine(MathF.Max(width - 22f, titleEnd));
        if (IconSmallButton(FontAwesomeIcon.Times, "##closepopup"))
            ImGui.CloseCurrentPopup();
    }

    private bool _editorDrawn; // safety net: an open editor whose row got hidden

    private string? _gridJob; // active job, cached once per frame for cell gating

    private void DrawGrid()
    {
        _editorDrawn = false;
        _gridJob = _plugin.ActiveJobAbbreviation();
        // Hover highlight rides one frame behind: cells set _hoverLive while
        // drawing, and the NEXT frame tints that whole row.
        _hoverLivePrev = _hoverLive;
        _hoverLive = null;
        // Below the grid: the sheet-notes panel plus one footer line (flash
        // message, or the hovered row's note).
        var footerH = ImGui.GetTextLineHeightWithSpacing() + 10f + NotesReserve();
        // Resizable: drag a column edge, or double-click it to auto-fit the
        // column to its content.
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY
                  | ImGuiTableFlags.ScrollX | ImGuiTableFlags.SizingFixedFit
                  | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable;
        // Settings (widths, drag order) are saved by column INDEX, so the ID
        // bakes in the fight + pin layout: a layout change resets them instead
        // of re-attaching them to the wrong slots.
        var tableId = $"##sheetgrid|{_fight!.Id}|{string.Join(",", _order)}";
        if (!ImGui.BeginTable(tableId, 2 + _slots.Length, flags, new Vector2(0, -footerH)))
            return;

        // Pinned columns ride in the frozen area right after Mechanic (capped so
        // the frozen block can't out-grow a narrow window).
        ImGui.TableSetupScrollFreeze(2 + Math.Min(4, _pinnedCount), 1);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 62);
        ImGui.TableSetupColumn("Mechanic", ImGuiTableColumnFlags.WidthFixed, 240);
        foreach (var i in _order)
            ImGui.TableSetupColumn(_slots[i], ImGuiTableColumnFlags.WidthFixed, 130);

        // Header row with role colors + a "(you)" tag on your active slot.
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableNextColumn();
        _headerY = ImGui.GetCursorScreenPos().Y;
        ImGui.TableHeader("Time");
        ImGui.TableNextColumn(); ImGui.TableHeader("Mechanic");
        foreach (var i in _order)
        {
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, RoleColor(_slots[i]));
            ImGui.TableHeader(IsActiveSlot(i) ? $"{_slots[i]} (you)" : _slots[i]);
            ImGui.PopStyleColor();
            var headMin = ImGui.GetItemRectMin();
            var headMax = ImGui.GetItemRectMax();
            var pinned = IsPinnedColumn(i);
            if (DelayedHover())
                ImGui.SetTooltip((IsActiveSlot(i)
                    ? $"{SlotTip(_slots[i])}, your slot. These are the lines your overlay calls."
                    : SlotTip(_slots[i]))
                    + (pinned ? "\nPinned. Right-click to unpin."
                              : "\nRight-click to pin this column next to Mechanic."));
            if (ImGui.BeginPopupContextItem($"##colpin{i}"))
            {
                if (_isCustom && !IsActiveSlot(i) && ImGui.MenuItem("Make this my column"))
                    SwitchCustomSlot(i);
                if (ImGui.MenuItem(pinned ? "Unpin column" : "Pin column"))
                {
                    if (pinned)
                        C.SheetPinnedSlots.RemoveAll(s => string.Equals(s, _slots[i], StringComparison.OrdinalIgnoreCase));
                    else
                        C.SheetPinnedSlots.Add(_slots[i]);
                    C.Save();
                    CommitPending();
                    _dirty = true;
                }
                ImGui.Separator();
                if (ImGui.MenuItem($"Copy column ({_slots[i]})"))
                {
                    _copyColFight = _fight;
                    _copyColSlot = _slots[i];
                }
                var canPaste = _copyColFight == _fight && _copyColSlot.Length > 0
                    && !string.Equals(_copyColSlot, _slots[i], StringComparison.OrdinalIgnoreCase)
                    && _slots.Contains(_copyColSlot, StringComparer.OrdinalIgnoreCase);
                ImGui.BeginDisabled(!canPaste);
                if (ImGui.MenuItem(canPaste ? $"Paste column ({_copyColSlot}'s plan)" : "Paste column"))
                {
                    CommitPending();
                    PasteColumn(i);
                }
                ImGui.EndDisabled();
                ImGui.EndPopup();
            }
            if (pinned)
            {
                // Thumbtack in the header's top-right corner, so pinned state is
                // visible at a glance.
                using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                {
                    var s = FontAwesomeIcon.Thumbtack.ToIconString();
                    var sz = ImGui.CalcTextSize(s);
                    ImGui.GetWindowDrawList().AddText(
                        new Vector2(headMax.X - sz.X - 4f, headMin.Y + (headMax.Y - headMin.Y - sz.Y) * 0.5f),
                        0xCCD0C8C0, s);
                }
            }
        }

        _firstDrawnIdx = -1;
        _stickyRowIdx = -1;
        _stickyTitle = "";
        var lastPhase = "";
        for (var r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            if (_phaseFilter.Length > 0 && row.Phase != _phaseFilter) continue;
            if (!MatchesFilter(row)) continue;

            if (row.Phase != lastPhase)
            {
                lastPhase = row.Phase;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, 0xFF221B17);
                ImGui.TableNextColumn();
                // Accent blue, matching the phase titles in the notes panel,
                // so the separators pop instead of reading as disabled text.
                ImGui.TextColored(NoteBlue, Builtin.PhaseTitle(_fight!.TerritoryId, row.Phase));
                for (var i = 0; i < _slots.Length; i++) ImGui.TableNextColumn();
            }

            if (_firstDrawnIdx < 0) _firstDrawnIdx = r;
            _rowIdxDrawing = r;

            ImGui.PushID(r);
            ImGui.TableNextRow();
            if (row == _hoverLivePrev)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, 0x16FFFFFF); // RowBg1 layers; RowBg0 would replace the alternation

            DrawTimeCell(row);
            DrawMechanicCell(row);
            foreach (var i in _order) DrawSlotCell(row, i);

            ImGui.PopID();
        }

        ImGui.EndTable();
        DrawStickyPhasePill();

        // An editor whose row was hidden this frame (filter change, rebuild race)
        // can never deactivate normally; land it now instead of leaving a zombie
        // edit that silently blocks rebuilds and commits minutes later.
        if (Editing && !_editorDrawn && !_focusPending) CommitPending();
    }

    // A quiet pill in the grid's top-right corner naming the phase you're
    // scrolled into, since the phase separator rows scroll away with the rows.
    private void DrawStickyPhasePill()
    {
        if (_phaseFilter.Length > 0 || _filter.Length > 0) return;
        if (_stickyRowIdx < 0 || _stickyRowIdx <= _firstDrawnIdx || _stickyTitle.Length == 0) return;

        var rectMin = ImGui.GetItemRectMin(); // the table is the last item
        var rectMax = ImGui.GetItemRectMax();
        var size = ImGui.CalcTextSize(_stickyTitle);
        var pad = new Vector2(8f, 3f);
        var headerH = ImGui.GetTextLineHeight() + ImGui.GetStyle().CellPadding.Y * 2f + 4f;
        var p0 = new Vector2(rectMax.X - size.X - pad.X * 2f - 24f, rectMin.Y + headerH + 6f);
        // Tiny window: don't cover the frozen columns (time+mechanic+your slot).
        if (p0.X < rectMin.X + 460f) return;

        // Foreground list: the table's rows live in an inner scrolling child,
        // which renders AFTER the window's own draw list; drawing there would
        // put the pill underneath the cells.
        var dl = ImGui.GetForegroundDrawList();
        dl.PushClipRect(rectMin, rectMax);
        dl.AddRectFilled(p0, p0 + size + pad * 2f, 0xE619130F, 5f);
        dl.AddRect(p0, p0 + size + pad * 2f, 0x2EFFFFFF, 5f);
        dl.AddText(p0 + pad, ImGui.GetColorU32(NoteBlue), _stickyTitle);
        dl.PopClipRect();
    }

    private void DrawTimeCell(Row row)
    {
        ImGui.TableNextColumn();
        // First row that renders below the frozen header = the top visible row;
        // its phase feeds the sticky pill.
        if (_stickyRowIdx < 0 && ImGui.GetCursorScreenPos().Y > _headerY + ImGui.GetTextLineHeight())
        {
            _stickyRowIdx = _rowIdxDrawing;
            _stickyTitle = Builtin.PhaseTitle(_fight!.TerritoryId, row.Phase);
        }
        if (row.Ghost)
        {
            ImGui.TextDisabled(TimeText(row.Time));
            return;
        }

        if (_editTimeRow == row)
        {
            _editorDrawn = true;
            ImGui.SetNextItemWidth(-1);
            if (_focusPending) { ImGui.SetKeyboardFocusHere(); _focusPending = false; }
            ImGui.InputText("##t", ref _timeBuf, 16);
            // Enter/click-away with an edit commits; Escape (ImGui reverts, not
            // "after edit") or leaving untouched just closes.
            if (ImGui.IsItemDeactivated())
            {
                if (ImGui.IsItemDeactivatedAfterEdit()) CommitTime(row);
                _editTimeRow = null;
            }
        }
        else
        {
            if (row.Edited) { ImGui.TextColored(EditedColor, "*"); ImGui.SameLine(0, 3); }
            if (ImGui.Selectable(TimeText(row.Time) + "##time", false) && !CommitPending())
            {
                _editTimeRow = row;
                _timeBuf = _timeSeed = row.Time.ToString("0.##", CultureInfo.InvariantCulture);
                _focusPending = true;
            }
            if (ImGui.IsItemHovered())
            {
                _hoverRow = row; _hoverLive = row;
                if (DelayedHover())
                    ImGui.SetTooltip($"{row.Time:0.#}s. Click to re-time \"{row.Mechanic}\" for EVERY slot at once.");
            }
        }
    }

    private void DrawMechanicCell(Row row)
    {
        ImGui.TableNextColumn();
        if (row.Ghost)
        {
            ImGui.TextDisabled(row.Mechanic);
            ImGui.SameLine(0, 6);
            ImGui.TextColored(EditedColor, "deleted");
            ImGui.SameLine(0, 4);
            if (IconSmallButton(FontAwesomeIcon.Undo, "##reset")) ResetRow(row);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("This mechanic is deleted from your plan. The undo button restores the sheet's version.");
            return;
        }

        ImGui.TextUnformatted(row.Mechanic);
        if (ImGui.IsItemHovered())
        {
            _hoverRow = row; _hoverLive = row;
            if (DelayedHover())
                ImGui.SetTooltip("Right-click to add or edit this mechanic's note.");
        }
        // Right-click the mechanic name = note editor.
        if (ImGui.BeginPopupContextItem("##notectx"))
        {
            if (ImGui.IsWindowAppearing())
            {
                _noteBuf = NoteFor(row)?.Text ?? "";
                _noteUndoArmed = true; // one undo entry per editing session
            }
            ImGui.TextDisabled($"Note: {row.Mechanic}");
            if (ImGui.InputTextMultiline("##notetxt", ref _noteBuf, 1000, new Vector2(360, 84)))
                SaveNote(row, _noteBuf);
            ImGui.TextDisabled("Saved as you type. Clear the text to remove the note.");
            // Custom rows also grade how hard the hit is here; Auto-plan reads it.
            if (_isCustom && CustomRowFor(row) is { } cr)
            {
                ImGui.Separator();
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled("Hits:");
                for (var h = 0; h < HurtChoices.Length; h++)
                {
                    ImGui.SameLine(0, 6);
                    if (ImGui.RadioButton($"{HurtChoices[h]}##hurt{h}", cr.Hurt == h) && cr.Hurt != h)
                    {
                        cr.Hurt = h;
                        C.Save();
                    }
                }
                ImGui.TextDisabled("Auto-plan depth: deadly 3 mits, hurts 2, light 1.");
                var tb = cr.Buster;
                if (ImGui.Checkbox("Tank buster (tanks' own plan, not party mits)", ref tb))
                {
                    cr.Buster = tb;
                    C.Save();
                }
            }
            ImGui.EndPopup();
        }
        if (NoteFor(row) != null)
        {
            ImGui.SameLine(0, 5);
            IconText(FontAwesomeIcon.PencilAlt, NoteBlue);
        }
        if (_isCustom)
        {
            // Buster tag: this row is planned by the tank lane, not party mits.
            if (CustomRowFor(row) is { Buster: true })
            {
                ImGui.SameLine(0, 6);
                ImGui.TextColored(ImGuiColors.TankBlue, "buster");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Hits one tank or two, not the party. Auto-plan gives it the tanks'\nown plan (invuln or Rampart + short mit, Buddy Mit from the co-tank).\nRight-click the mechanic to change.");
            }
            // The severity grade, visible at a glance (right-click to change).
            if (CustomRowFor(row) is { Hurt: > 0 } gr)
            {
                ImGui.SameLine(0, 6);
                var (mark, color) = gr.Hurt switch
                {
                    3 => ("!!!", 0xFF4444E0u),
                    2 => ("!!", 0xFF3BA8F0u),
                    _ => ("!", 0xFF9BA0A6u),
                };
                ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(color), mark);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Hits {HurtChoices[gr.Hurt]} unmitigated. Right-click the mechanic to regrade;\nAuto-plan stacks {gr.Hurt} mit(s) here.");
            }
            // Custom-sheet rows are all yours; the only row action is delete.
            ImGui.SameLine(0, 6);
            if (IconSmallButton(FontAwesomeIcon.Times, "##delrow")) DeleteCustomRow(row);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Delete this row (every column). Ctrl+Z brings it back.");
            return;
        }

        if (row.JobExtra && !row.Edited)
        {
            // A quiet tag, not a warning: this row is a job-specific schedule
            // (Nature's Minne and friends) sitting at its own time on purpose.
            ImGui.SameLine(0, 6);
            ImGui.TextDisabled("job extra");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A job-specific line (like the fight page's Job extras): it only fires "
                    + "for the listed job, and sits at its own time on purpose. Nothing is wrong.");
            ImGui.SameLine(0, 4);
            if (IconSmallButton(FontAwesomeIcon.Times, "##delextra")) DeleteExtraRow(row);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Remove this job extra (every slot). Ctrl+Z brings it back.");
        }
        else if (row.Edited)
        {
            ImGui.SameLine(0, 6);
            ImGui.TextColored(EditedColor, "edited");
            ImGui.SameLine(0, 4);
            if (IconSmallButton(FontAwesomeIcon.Undo, "##reset")) ResetRow(row);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Reset this mechanic to the baked sheet, every slot.");
        }
    }

    // Remove a job-extra row's lines everywhere.
    private void DeleteExtraRow(Row row)
    {
        if (_fight == null || row.Ghost || AbortIfStale()) return;
        PushUndo($"delete \"{row.Mechanic}\" job extra");
        var removed = 0;
        for (var i = 0; i < _slots.Length; i++)
        {
            if (row.Cells[i].Count == 0) continue;
            EnsureBacked(i);
            foreach (var l in row.Cells[i].ToList()) { _slotLines[i].Remove(l); removed++; }
            Resort(i);
        }
        if (removed == 0) { PopUndo(); return; }
        C.Save();
        _dirty = true;
        Flash($"\"{row.Mechanic}\" job extra removed. Ctrl+Z brings it back.");
    }

    private void DrawSlotCell(Row row, int i)
    {
        ImGui.TableNextColumn();
        if (IsActiveSlot(i)) ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, YouCellBg);

        if (row.Ghost)
        {
            var baked = row.Bake!.Cells[i];
            if (baked.Count > 0)
                ImGui.TextDisabled(string.Join(" · ", baked.Select(l => l.Action)));
            return;
        }

        if (_editCellRow == row && _editCellSlot == i)
        {
            _editorDrawn = true;
            ImGui.SetNextItemWidth(-1);
            if (_focusPending) { ImGui.SetKeyboardFocusHere(); _focusPending = false; }
            ImGui.InputText("##c", ref _cellBuf, 256);
            if (ImGui.IsItemDeactivated())
            {
                var enter = ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false);
                var tab = ImGui.IsKeyPressed(ImGuiKey.Tab, false);
                if (ImGui.IsItemDeactivatedAfterEdit()) CommitCell(row, i);
                _editCellRow = null;
                if (enter || tab) QueueNeighborEdit(row, i, tab);
            }
            return;
        }

        var cell = row.Cells[i];
        var first = cell.Count == 0 ? "" : cell[0].Action;
        // Job extras render as normal text (no orange, no *): they're not edits.
        var jobOnly = cell.Count > 0 && cell.All(l => l.Custom && l.Jobs.Count > 0);
        var custom = !_isCustom && !jobOnly && cell.Any(l => l.Custom);
        var off = cell.Count > 0 && cell.All(l => !l.Enabled);
        // Every line here is another job's press (a "(WAR/PLD)" style tag, or a
        // job-tagged extra): dim it, since it will never fire on your current
        // job.
        var foreign = !string.IsNullOrEmpty(_gridJob) && cell.Count > 0
            && cell.All(l => !l.AppliesTo(_gridJob));

        // Cooldown conflicts tint the cell red; level-sync problems amber
        // (red wins when both apply).
        string? warn = null;
        string? lvl = null;
        foreach (var l in cell)
        {
            if (_conflicts.TryGetValue(l, out var w)) warn = warn == null ? w : warn + "\n" + w;
            if (_levelWarns.TryGetValue(l, out var v)) lvl = lvl == null ? v : lvl + "\n" + v;
        }
        if (warn != null) ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, WarnCellBg);
        else if (lvl != null) ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, LevelCellBg);

        // Carry-over ghost: this hit is still inside an earlier press's buff.
        var carry = cell.Count == 0 && row.Carry != null ? row.Carry[i] : null;

        // Merged cells stack their lines instead of hiding behind a "+1".
        var body = cell.Count > 1 ? string.Join("\n", cell.Select(l => l.Action)) : first;
        var label = (custom ? "* " : "") + (body.Length == 0 ? carry ?? " " : body) + (off ? "  (off)" : "");

        // Text color: your edits stay orange, disabled lines dim, carry-over
        // ghosts dimmer still, and with the Colors box ticked the rest is
        // colored by mit type (overlay colors).
        var kindCol = C.SheetColorByType && !custom && !off && first.Length > 0
            ? MitTypes.Color(MitTypes.Classify(first), C) : 0u;
        var pushed = true;
        if (custom) ImGui.PushStyleColor(ImGuiCol.Text, EditedColor);
        else if (carry != null)
            ImGui.PushStyleColor(ImGuiCol.Text,
                (ImGui.GetColorU32(ImGuiCol.TextDisabled) & 0x00FFFFFF) | 0x78000000);
        else if (off || foreign) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        else if (kindCol != 0) ImGui.PushStyleColor(ImGuiCol.Text, kindCol);
        else pushed = false;
        var clicked = ImGui.Selectable($"{label}##c{i}", false);
        if (pushed) ImGui.PopStyleColor();

        if (clicked && !CommitPending())
        {
            _editCellRow = row;
            _editCellSlot = i;
            _cellBuf = _cellSeed = first;
            _focusPending = true;
        }
        if (ImGui.IsItemHovered())
        {
            _hoverRow = row; _hoverLive = row;
            var tip = cell.Count == 0
                ? (carry != null
                    ? $"Still covered: {carry[3..]} from an earlier row is up through this hit.\nClick to add a mit of your own for {_slots[i]}."
                    : $"Click to add a mit for {_slots[i]} here (that slot only)")
                : cell.Count == 1
                    ? $"{first}\nClick to edit {_slots[i]}'s mit (that slot only). Clear the text to remove it."
                    : $"{string.Join("  ·  ", cell.Select(l => l.Action))}\nTwo lines share this moment; "
                      + "editing changes the first one only. Fine-tune both on the fight page.";
            string? win = null;
            foreach (var l in cell)
                if (_windows.TryGetValue(l, out var w0))
                    win = win == null ? w0 : win + "\n" + w0;
            if (jobOnly) tip = $"Job extra: only fires for {string.Join("/", cell[0].Jobs)}.\n" + tip;
            if (foreign) tip = $"Another job's press; it won't fire for you on {_gridJob}.\n" + tip;
            if (win != null) tip = win + "\n\n" + tip;
            if (off) tip = "Disabled on the fight page (won't be called).\n" + tip;
            if (lvl != null) tip = lvl + "\n\n" + tip;
            if (warn != null) tip = warn + "\n\n" + tip;
            // Warnings show immediately; informational tips wait the beat.
            if (warn != null || lvl != null || DelayedHover()) ImGui.SetTooltip(tip);
        }

        // Right-click: quick actions + the per-call offset, sheet-side.
        if (ImGui.BeginPopupContextItem($"##cellctx{i}"))
        {
            if (ImGui.IsWindowAppearing()) _offsetUndoArmed = true;
            ImGui.TextDisabled($"{_slots[i]}  ·  {row.Mechanic}");
            ImGui.Separator();
            if (cell.Count > 0)
            {
                var line = cell[0];
                var offset = line.OffsetSeconds;
                ImGui.SetNextItemWidth(110f);
                // Same semantics as the fight page's ±s column: clamped, and NOT
                // flagged Custom (an offset is a nudge, not a rewrite).
                if (ImGui.InputFloat("call offset (s)", ref offset, 0.5f, 1f, "%.1f") && !AbortIfStale())
                {
                    if (_offsetUndoArmed) { PushUndo($"adjust \"{row.Mechanic}\" offset"); _offsetUndoArmed = false; }
                    EnsureBacked(i);
                    line.OffsetSeconds = Math.Clamp(offset, -30f, 30f);
                    line.OffsetManual = true; // hand-set: the timing solver won't touch it
                    C.Save();
                    _dirty = true; // cooldown math runs on cue times; recompute
                }
                ImGui.TextDisabled("+ fires this one call earlier, - later.");

                // Multi-hit coverage: stretch this mit over later hits; the
                // tooltip then shows the valid press window.
                var coverBase = line.CoverUntil > row.Time ? line.CoverUntil : row.Time;
                var nextRow = _rows.FirstOrDefault(r => !r.Ghost && r.Time > coverBase + 0.5f);
                ImGui.BeginDisabled(nextRow == null);
                if (ImGui.MenuItem(nextRow != null
                        ? $"Cover through {nextRow.Mechanic} ({TimeText(nextRow.Time)})"
                        : "Cover through next hit") && nextRow != null && !AbortIfStale())
                {
                    PushUndo($"extend {row.Mechanic} coverage");
                    EnsureBacked(i);
                    line.CoverUntil = nextRow.Time;
                    line.OffsetManual = true; // hand-set timing: the auto cooldown timer won't touch it
                    C.Save();
                    _dirty = true;
                }
                ImGui.EndDisabled();
                if (line.CoverUntil > row.Time && ImGui.MenuItem($"Clear coverage (through {TimeText(line.CoverUntil)})") && !AbortIfStale())
                {
                    PushUndo($"clear {row.Mechanic} coverage");
                    EnsureBacked(i);
                    line.CoverUntil = 0f;
                    line.OffsetManual = true; // hand-set timing: the auto cooldown timer won't touch it
                    C.Save();
                    _dirty = true;
                }
                if (_windows.TryGetValue(line, out var lineWin))
                {
                    var winFirst = lineWin.Split('\n')[0];
                    ImGui.TextDisabled(winFirst);
                    // One click to move the CALL to the window's start.
                    var m = System.Text.RegularExpressions.Regex.Match(winFirst, "between (\\d+):(\\d+)");
                    if (m.Success)
                    {
                        var winStart = int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value);
                        var shift = MathF.Round(row.Time - winStart);
                        if (shift is > 0f and <= 30f && MathF.Abs(line.OffsetSeconds - shift) >= 0.5f
                            && ImGui.MenuItem($"Call at window start (+{shift:0}s)") && !AbortIfStale())
                        {
                            PushUndo($"offset {row.Mechanic} to window");
                            EnsureBacked(i);
                            line.OffsetSeconds = shift;
                            line.OffsetManual = true; // hand-set: the auto cooldown timer won't touch it
                            C.Save();
                            _dirty = true;
                        }
                    }
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Copy mit")) _cellClip = line.Action;
                if (ImGui.MenuItem("Delete this mit")) DeleteCellLine(row, i);
            }
            if (_isCustom && cell.Count == 0 && ImGui.BeginMenu("Suggest a mit"))
            {
                DrawSuggestMenu(row, i);
                ImGui.EndMenu();
            }
            ImGui.BeginDisabled(_cellClip.Length == 0);
            if (ImGui.MenuItem(_cellClip.Length > 0
                    ? $"Paste mit ({(_cellClip.Length > 24 ? _cellClip[..22] + "..." : _cellClip)})"
                    : "Paste mit"))
                ApplyCellText(row, i, _cellClip);
            ImGui.EndDisabled();
            if (ImGui.MenuItem("Reset this cell to the sheet")) ResetCell(row, i);
            if (!_isCustom && ImGui.MenuItem("Reset all columns...")) _openResetAll = true;
            ImGui.EndPopup();
        }
    }

    // Set by the Plan menu or a cell context menu; the confirm modal opens from
    // the toolbar's ID scope on the next pass.
    private bool _openResetAll;

    // The conflict + press-window math bakes cue times in at Rebuild, so edits
    // from other windows must poke the grid or the red cooldown cells go stale.
    public void MarkPlanDirty() => _dirty = true;

    // Full reset across every column (same as the fight page's Reset all columns):
    // snapshot-first, confirmed, and undoable with Ctrl+Z here in the Sheet View.
    private void DrawResetAllPopup()
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##sheetresetall", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        ImGui.TextUnformatted("Reset every column to the baked sheet?");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "All slots' edits and deletions go, including added potion, job and tank lines.");
        ImGui.TextDisabled("A snapshot is saved first; Plan > History (or Ctrl+Z) restores it.");
        ImGui.Spacing();

        if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF1E40C0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF2046D0);
        if (ImGui.Button("Reset every column", new Vector2(180, 0)) && _fight != null)
        {
            PushUndo("reset every column");
            _plugin.Snapshots.Save(_fight, "before Reset all columns");
            _fight.SavedSlots.Clear();
            _fight.DeletedCalls.Clear();
            if (!string.IsNullOrEmpty(_fight.Slot)) Builtin.ResetSlot(_fight, _fight.Slot);
            C.Save();
            _dirty = true;
            Flash("Every column reset to the baked sheet. Plan > History (or Ctrl+Z) restores the old plan.");
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndPopup();
    }

    // ---- suggest a mit (custom sheets) --------------------------------------
    // Which jobs fit a column, by its slot code's role bucket.
    private static readonly string[] TankJobs = { "WAR", "PLD", "DRK", "GNB" };
    private static readonly string[] HealJobs = { "WHM", "SCH", "AST", "SGE" };
    private static readonly string[] DpsJobs = { "MNK", "DRG", "NIN", "SAM", "RPR", "VPR", "BRD", "MCH", "DNC", "BLM", "SMN", "RDM", "PCT" };

    private void DrawSuggestMenu(Row row, int i)
    {
        var slot = _slots[i];
        var jobs = TankSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? TankJobs
                 : HealSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? HealJobs
                 : DpsJobs;
        var syncLevel = _fight != null ? Cooldowns.DutySyncLevel(_fight.TerritoryId) : 0;

        foreach (var job in jobs)
        {
            if (!Cooldowns.JobKits.TryGetValue(job, out var kit)) continue;
            if (!ImGui.BeginMenu(job)) continue;

            var shownFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var any = false;
            foreach (var name in kit)
            {
                if (Cooldowns.PlanInfo(name) is not { } pm) continue;
                if (syncLevel > 0 && pm.Level > syncLevel) continue; // above the duty's sync
                // One entry per shared-cooldown family: the kit lists the
                // upgrade first, so the highest legal form wins.
                if (pm.Family.Length > 0 && !shownFamilies.Add(pm.Family)) continue;
                var free = MitFreeAt(i, pm, row.Time);
                ImGui.BeginDisabled(!free);
                if (ImGui.MenuItem(free ? name : $"{name} (on cooldown here)"))
                    ApplyCellText(row, i, name);
                ImGui.EndDisabled();
                any = true;
            }
            if (!any) ImGui.TextDisabled("nothing available");
            ImGui.EndMenu();
        }
    }

    // Is this mit's timer free at `t`, given the column's existing plan?
    private bool MitFreeAt(int i, Cooldowns.PlanMit pm, float t)
    {
        var nearby = 0;
        foreach (var l in _slotLines[i])
        {
            if (!l.Enabled || MathF.Abs(l.CueTime - t) >= pm.Recast) continue;
            foreach (var other in Cooldowns.PlanMits(l.Action))
                if (string.Equals(other.Name, pm.Name, StringComparison.OrdinalIgnoreCase)
                    || (pm.Family.Length > 0 && other.Family == pm.Family))
                {
                    nearby++;
                    break;
                }
        }
        return nearby < pm.Charges;
    }

    // Cell clipboard for right-click copy/paste (a mit's action text).
    private string _cellClip = "";
    // Column clipboard: which fight + slot code was copied.
    private FightProfile? _copyColFight;
    private string _copyColSlot = "";

    // Overwrite one column with another slot's plan, like pasting a column in a
    // spreadsheet.
    private void PasteColumn(int dst)
    {
        if (_fight == null || AbortIfStale()) return;
        var src = Array.FindIndex(_slots, s => s.Equals(_copyColSlot, StringComparison.OrdinalIgnoreCase));
        if (src < 0 || src == dst) return;

        PushUndo($"paste {_slots[src]}'s plan into {_slots[dst]}");
        _plugin.Snapshots.Save(_fight, $"before pasting {_slots[src]} into {_slots[dst]}");
        EnsureBacked(dst);
        var target = _slotLines[dst];
        target.Clear();
        foreach (var l in _slotLines[src])
        {
            var copy = Clone(l);
            copy.Custom = true;
            target.Add(copy);
        }
        if (!_isCustom)
        {
            _fight.DeletedCalls.RemoveAll(d => string.Equals(d.Slot, _slots[dst], StringComparison.OrdinalIgnoreCase));
            foreach (var b in Builtin.BuildLines(_fight.TerritoryId, _slots[dst]))
                if (!target.Any(l => Builtin.SameCall(l, b)))
                    _fight.DeletedCalls.Add(new DeletedCall
                    { Slot = _slots[dst], Time = b.Time, Mechanic = b.Mechanic, Action = b.Action });
        }
        Resort(dst);
        C.Save();
        _dirty = true;
        Flash($"{_slots[src]}'s plan pasted into {_slots[dst]} (that column only). Ctrl+Z undoes it.");
    }

    // Delete one slot's line at this row: tombstoned exactly like clearing the
    // cell's text, so sheet updates don't resurrect it.
    private void DeleteCellLine(Row row, int i)
    {
        if (_fight == null || row.Ghost || AbortIfStale()) return;
        var cell = row.Cells[i];
        if (cell.Count == 0) return;
        PushUndo($"delete {_slots[i]}'s \"{row.Mechanic}\" mit");
        EnsureBacked(i);
        var line = cell[0];
        if (!line.Custom)
            _fight.DeletedCalls.Add(new DeletedCall
            { Slot = _slots[i], Time = line.Time, Mechanic = line.Mechanic, Action = line.Action });
        _slotLines[i].Remove(line);
        Resort(i);
        C.Save();
        _dirty = true;
        Flash($"{_slots[i]}'s mit for \"{row.Mechanic}\" removed. The undo button on the row brings the sheet's version back.");
    }

    // Reset ONE slot's cell to the baked sheet (the row's undo button does every
    // slot at once; this is the surgical version).
    private void ResetCell(Row row, int i)
    {
        if (_fight == null || AbortIfStale()) return;
        var slot = _slots[i];
        if (row.Bake == null)
        {
            // Same idea as ResetRow: no baked pair means the sheet has nothing
            // here, so reset clears this cell's lines instead of dead-ending.
            if (row.Cells[i].Count == 0) { Flash($"{slot} has nothing on this row."); return; }
            PushUndo($"remove {slot}'s \"{row.Mechanic}\"");
            EnsureBacked(i);
            foreach (var line in row.Cells[i].ToList()) _slotLines[i].Remove(line);
            Resort(i);
            C.Save();
            _dirty = true;
            Flash($"{slot}'s \"{row.Mechanic}\" removed: this row isn't on the baked sheet. Undo brings it back.");
            return;
        }
        var candidates = row.Bake.Cells[i];
        var pristine = row.Cells[i].All(l => !l.Custom)
            && row.Cells[i].Count == candidates.Count
            && candidates.All(b => row.Cells[i].Any(l => Builtin.SameCall(l, b)))
            && !_fight.DeletedCalls.Any(d => candidates.Any(b => Builtin.MatchesTombstone(d, slot, b)));
        if (pristine)
        {
            Flash($"{slot}'s \"{row.Mechanic}\" already matches the sheet.");
            return;
        }
        PushUndo($"reset {slot}'s \"{row.Mechanic}\"");
        EnsureBacked(i);
        foreach (var line in row.Cells[i].ToList()) _slotLines[i].Remove(line);
        foreach (var b in candidates)
        {
            _fight.DeletedCalls.RemoveAll(d => Builtin.MatchesTombstone(d, slot, b));
            if (!_slotLines[i].Any(l => Builtin.SameCall(l, b)
                    || (MathF.Abs(l.Time - b.Time) < 0.9f
                        && string.Equals(l.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase))))
                _slotLines[i].Add(b);
        }
        Resort(i);
        C.Save();
        _dirty = true;
        Flash($"{slot}'s \"{row.Mechanic}\" reset to the sheet.");
    }

    private static string TimeText(float t) => Fmt.MmssSigned(t);

    private static string SlotTip(string slot)
        => TankSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? "Tank slot"
         : HealSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? "Healer slot"
         : "DPS slot";

    // One quiet line: a flash message when something just happened, otherwise
    // the hovered row's note (Ikuya-footer style, sticky on the last hovered
    // row so it stays readable while the mouse travels down here).
    private void DrawFooter()
    {
        ImGui.Spacing();

        if ((DateTime.Now - _flashAt).TotalSeconds < 4.5 && _flash.Length > 0)
        {
            ImGui.TextColored(ImGuiColors.ParsedGreen, _flash);
            return;
        }

        var note = _hoverRow != null ? NoteFor(_hoverRow) : null;
        if (note == null) return;
        IconText(FontAwesomeIcon.PencilAlt, NoteBlue);
        ImGui.SameLine(0, 6);
        ImGui.TextUnformatted($"{_hoverRow!.Mechanic}:");
        ImGui.SameLine(0, 6);
        var text = note.Text.Replace('\n', ' ');
        ImGui.TextDisabled(text.Length > 220 ? text[..220] + "..." : text);
        if (ImGui.IsItemHovered() && note.Text.Length > 220) ImGui.SetTooltip(note.Text);
    }
}
