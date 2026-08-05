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

// Sheet View: exporting a plan, and drawing the grid.
public partial class SheetViewWindow
{
    // ---- export ----

    // The whole grid as tab-separated text.
    private void ExportText()
    {
        if (_fight == null) return;
        var sb = new System.Text.StringBuilder();
        sb.Append("Time\tMechanic");
        foreach (var i in _order) sb.Append('\t').Append(_gridCols[i]);
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

    // Tabs or newlines in a cell would split the row.
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

    private static readonly string[] TankSlots = { "MT", "OT", "T" };
    private static readonly string[] HealSlots = { "WHM", "AST", "SCH", "SGE", "H1", "H2", "H" };

    private static Vector4 RoleColor(string slot)
        => TankSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? ImGuiColors.TankBlue
         : HealSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? ImGuiColors.HealerGreen
         : ImGuiColors.DPSRed;

    // The hit types worth naming on a planning row, in the board chip's colors
    // (ABGR); a party hit is the default here and the severity mark already says so.
    private static (string Tag, uint Color, string Tip) TypeTag(CustomRow r)
        => r.Enrage ? ("enrage", 0xFF4646FFu, "The fight's timer running out, not something you mit.")
         : r.Buster ? ("buster", 0xFF4090F0u, "Hits one or two tanks, not the party. Right-click to change.")
         : ("", 0u, "");

    private static readonly Vector4 EditedColor = new(0.96f, 0.62f, 0.36f, 1f);
    private static readonly Vector4 NoteBlue = new(0.42f, 0.66f, 0.96f, 1f);
    private const uint YouCellBg = 0x2233AA33;   // faint green tint (ABGR)
    private const uint WarnCellBg = 0x483040E6;  // translucent red: cooldown conflict
    private const uint LevelCellBg = 0x4820A0E0; // translucent amber: above level sync

    // The game font has no symbol glyphs, so use the icon font.
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

    // Popup header: a dim title plus a right-aligned X.
    private static void PopupHeader(string title, float width)
    {
        ImGui.TextDisabled(title);
        var titleEnd = ImGui.GetItemRectSize().X + 24f;
        ImGui.SameLine(MathF.Max(width - 22f, titleEnd));
        if (IconSmallButton(FontAwesomeIcon.Times, "##closepopup"))
            ImGui.CloseCurrentPopup();
    }

    private bool _editorDrawn; // safety net: an open editor whose row got hidden

    private List<MitLine> GetCellLinesForJob(Row row, int i)
    {
        var cell = row.Cells[i];
        
        // Filter out actions based on the Action Type checkboxes
        if (!_showPartyMits || !_showPersonalMits)
        {
            var filtered = new List<MitLine>();
            foreach (var l in cell)
            {
                var isPartyMit = AbilityBook.PartyMits.Contains(l.Action);
                if (isPartyMit && !_showPartyMits) continue;
                if (!isPartyMit && !_showPersonalMits) continue;
                filtered.Add(l);
            }
            return filtered;
        }

        return cell;
    }

    private void DrawGrid()
    {
        _editorDrawn = false;
        // Hover highlight rides one frame behind the cells.
        _hoverLivePrev = _hoverLive;
        _hoverLive = null;
        // Below the grid: the notes panel plus one footer line.
        var footerH = ImGui.GetTextLineHeightWithSpacing() + 10f + NotesReserve();
        // Resizable: drag a column edge, or double-click to fit.
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY
                  | ImGuiTableFlags.ScrollX | ImGuiTableFlags.SizingFixedFit
                  | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable;
        // Settings save by column index, so the id bakes in the layout.
        var tableId = $"##sheetgrid|{_fight!.Id}|{string.Join(",", _order)}";
        if (!ImGui.BeginTable(tableId, 2 + _gridCols.Length, flags, new Vector2(0, -footerH)))
            return;

        // Pinned columns ride frozen, capped for narrow windows.
        ImGui.TableSetupScrollFreeze(2 + Math.Min(4, _pinnedCount), 1);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 62);
        ImGui.TableSetupColumn("Mechanic", ImGuiTableColumnFlags.WidthFixed, 240);
        foreach (var i in _order)
            ImGui.TableSetupColumn(_gridCols[i], ImGuiTableColumnFlags.WidthFixed, 130);

        // Header row with role colors and a "(you)" tag.
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableNextColumn();
        _headerY = ImGui.GetCursorScreenPos().Y;
        ImGui.TableHeader("Time");
        ImGui.TableNextColumn(); ImGui.TableHeader("Mechanic");
        foreach (var i in _order)
        {
            ImGui.TableNextColumn();
            var slotIdx = _gridToSlot[i];
            ImGui.PushStyleColor(ImGuiCol.Text, RoleColor(_gridCols[i]));
            ImGui.TableHeader(IsYouColumn(i) ? $"{_gridCols[i]} (you)" : _gridCols[i]);
            ImGui.PopStyleColor();
            var headMin = ImGui.GetItemRectMin();
            var headMax = ImGui.GetItemRectMax();
            var pinned = IsPinnedColumn(i);
            if (DelayedHover())
                ImGui.SetTooltip((IsYouColumn(i) ? $"{SlotTip(_gridCols[i])}, your slot." : SlotTip(_gridCols[i]))
                    + (pinned ? "\nPinned. Right-click to unpin." : "\nRight-click to pin."));
            if (ImGui.BeginPopupContextItem($"##colpin{i}"))
            {
                if (_isCustom && !IsActiveSlot(slotIdx) && ImGui.MenuItem("Make this my column"))
                    SwitchCustomSlot(slotIdx);
                if (ImGui.MenuItem(pinned ? "Unpin column" : "Pin column"))
                {
                    if (pinned)
                        C.SheetPinnedSlots.RemoveAll(s => string.Equals(s, _gridCols[i], StringComparison.OrdinalIgnoreCase));
                    else
                        C.SheetPinnedSlots.Add(_gridCols[i]);
                    C.Save();
                    CommitPending();
                    _dirty = true;
                }
                ImGui.Separator();
                if (ImGui.MenuItem($"Copy column ({_gridCols[i]})"))
                {
                    _copyColFight = _fight;
                    _copyColSlot = _slots[slotIdx];
                }
                var canPaste = _copyColFight == _fight && _copyColSlot.Length > 0
                    && !string.Equals(_copyColSlot, _slots[slotIdx], StringComparison.OrdinalIgnoreCase)
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
                // Thumbtack in the corner, so pinned state reads at a glance.
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
            if (!_showJobExtra && row.JobExtra) continue;
            
            if (row.Cells.All(c => c.Count == 0))
            {
                if (!(_isCustom || C.ShowEmptyMechanics) || !_fight.CustomRows.Any(cr => MechEquals(row.Mechanic, cr.Mechanic)))
                    continue;
            }

            if (!MatchesFilter(row)) continue;

            if (row.Phase != lastPhase)
            {
                lastPhase = row.Phase;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, 0xFF221B17);
                ImGui.TableNextColumn();
                // Accent blue, so the separators pop instead of reading dim.
                ImGui.TextColored(NoteBlue, Builtin.PhaseTitle(_fight!.TerritoryId, row.Phase));
                // On this row for a swapped-priority phase, the toggle sits in
                // the MT/OT columns themselves - it swaps both at once, not
                // just the one you happen to be viewing as.
                var priPhase = _isCustom ? null : TankPriority.PhaseAt(_fight!.TerritoryId, row.Time);
                foreach (var i in _order)
                {
                    ImGui.TableNextColumn();
                    if (priPhase != null && TankSlots.Contains(_slots[_gridToSlot[i]], StringComparer.OrdinalIgnoreCase))
                        DrawPriorityPhaseToggle(priPhase, i);
                }
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

        // An editor whose row was hidden can't deactivate normally.
        if (Editing && !_editorDrawn && !_focusPending) CommitPending();
    }

    // A pill naming the phase you're scrolled into.
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
        // Tiny window: don't cover the frozen columns.
        if (p0.X < rectMin.X + 460f) return;

        // Foreground list, since the rows render after this one.
        var dl = ImGui.GetForegroundDrawList();
        dl.PushClipRect(rectMin, rectMax);
        dl.AddRectFilled(p0, p0 + size + pad * 2f, 0xE619130F, 5f);
        dl.AddRect(p0, p0 + size + pad * 2f, 0x2EFFFFFF, 5f);
        dl.AddText(p0 + pad, ImGui.GetColorU32(NoteBlue), _stickyTitle);
        dl.PopClipRect();
    }

    // On a phase whose tank busters follow priority (not literal MT/OT), a
    // small toggle in the MT/OT columns to flip which physical tank each one
    // shows - the grid shows both columns at once, so this exchanges them
    // both, unlike the Fight Editor's toggle which only affects your own seat.
    private void DrawPriorityPhaseToggle(PriorityPhase phase, int colIdx)
    {
        if (_fight == null) return;
        var swapped = TankPriority.IsSwapped(_fight, phase);
        ImGui.PushStyleColor(ImGuiCol.Text, swapped ? 0xFF5C9EF5 : ImGui.GetColorU32(ImGuiCol.TextDisabled));
        var clicked = IconSmallButton(FontAwesomeIcon.Random, $"##priswap{colIdx}");
        ImGui.PopStyleColor();
        if (clicked)
        {
            TankPriority.SetSwapped(_fight, phase, !swapped);
            Builtin.ReapplyPriority(_fight);
            MarkPlanDirty();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(swapped
                ? "Priority swapped for this phase - click to go back to the sheet's default MT/OT."
                : "Tank busters here follow job priority, not MT/OT.\nClick to swap MT and OT for this phase.");
    }

    private void DrawTimeCell(Row row)
    {
        ImGui.TableNextColumn();
        // The first row below the header is the top visible one.
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
            // Enter or click-away commits; Escape just closes.
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
                    ImGui.SetTooltip($"{row.Time:0.#}s. Click to re-time every slot.");
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
                ImGui.SetTooltip("Deleted from your plan. Undo restores it.");
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
            // Custom rows grade the hit here, and Auto-plan reads it.
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
            // Type tag, matching the board's chip: what this row's hit IS.
            if (CustomRowFor(row) is { } tr && TypeTag(tr) is var (tag, tagCol, tagTip) && tag.Length > 0)
            {
                ImGui.SameLine(0, 6);
                ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(tagCol), tag);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(tagTip);
            }
            // The severity grade, right-click to change.
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
                    ImGui.SetTooltip($"Hits {HurtChoices[gr.Hurt]} unmitigated. Right-click to regrade.");
            }
            // Custom rows are all yours, so delete is the only action.
            ImGui.SameLine(0, 6);
            if (IconSmallButton(FontAwesomeIcon.Times, "##delrow")) DeleteCustomRow(row);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Delete this row (every column). Ctrl+Z brings it back.");
            return;
        }

        if (row.JobExtra && !row.Edited)
        {
            // A quiet tag: this row is a job schedule at its own time.
            ImGui.SameLine(0, 6);
            ImGui.TextDisabled("job extra");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A job-specific line at its own time. Nothing is wrong.");

            // A hidden mechanic (a summoner's pet cycle) is the sheet's own
            // timer rather than a call mixed in from the job-extra schedules,
            // so there is nothing here to opt out of - offering delete would
            // only tombstone rows the sheet will keep baking.
            if (_fight is { } f && Builtin.IsHiddenMechanic(f.TerritoryId, row.Mechanic)) return;

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
        var processedSlots = new HashSet<int>();
        for (var i = 0; i < _gridCols.Length; i++)
        {
            if (row.Cells[i].Count == 0) continue;
            var slotIdx = _gridToSlot[i];
            if (!processedSlots.Add(slotIdx)) continue;
            EnsureBacked(slotIdx);
            foreach (var l in row.Cells[i].ToList())
            {
                // Tombstone it, or the auto-mixed extras would just put it
                // right back next time the fight page (or a zone entry) tops
                // the lines up.
                if (JobExtras.IsAutoExtra(l))
                    _fight.DeletedCalls.Add(new DeletedCall
                    {
                        Slot = _slots[slotIdx],
                        Time = l.Time,
                        Mechanic = l.Mechanic,
                        Action = l.Action,
                    });
                _slotLines[slotIdx].Remove(l);
                removed++;
            }
            Resort(slotIdx);
        }
        if (removed == 0) { PopUndo(); return; }
        C.Save();
        _dirty = true;
        Flash($"\"{row.Mechanic}\" job extra removed. Ctrl+Z brings it back.");
    }

    private bool IsYouColumn(int i)
    {
        var col = _gridCols[i];
        if (Jobs.ByAbbreviation(col) != null)
        {
            var activeJob = _fight != null ? _plugin.GetActiveJobAbbr(_fight) : _plugin.ActiveJobAbbreviation();
            return string.Equals(col, activeJob, StringComparison.OrdinalIgnoreCase);
        }
        return IsActiveSlot(_gridToSlot[i]);
    }

    private string FormatLineText(MitLine l, int i)
    {
        var action = l.Action;
        if (l.Jobs.Count > 0 && Jobs.ByAbbreviation(_gridCols[i]) == null)
        {
            return $"{action} ({string.Join("/", l.Jobs)})";
        }
        return action;
    }

    private void DrawSlotCell(Row row, int i)
    {
        ImGui.TableNextColumn();
        var slotIdx = _gridToSlot[i];
        if (IsYouColumn(i)) ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, YouCellBg);

        if (row.Ghost)
        {
            var baked = row.Bake!.Cells[i];
            if (baked.Count > 0)
                ImGui.TextDisabled(string.Join(" · ", baked.Select(l => FormatLineText(l, i))));
            return;
        }

        if (_editCellRow == row && _editCellSlot == i)
        {
            _editorDrawn = true;
            ImGui.SetNextItemWidth(-1);
            if (_focusPending) { ImGui.SetKeyboardFocusHere(); _focusPending = false; }
            ImGui.InputText("##c", ref _cellBuf, 256);
            // A second click hands off to the full editor, so the box stays the
            // fast path: type, Enter or Tab, next cell.
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                CommitPending();
                _cellEditOpening = (row.Time, row.Mechanic, i);
                return;
            }
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

        var cell = GetCellLinesForJob(row, i);
        var carries = row.Carry?[i];

        List<string>? uniqueCarries = null;
        if (carries != null && carries.Count > 0)
        {
            foreach (var cItem in carries)
            {
                if (!cell.Any(l => string.Equals(l.Action.Trim(), cItem.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    uniqueCarries ??= new List<string>();
                    uniqueCarries.Add(cItem);
                }
            }
        }

        var first = cell.Count == 0 ? "" : FormatLineText(cell[0], i);
        var jobOnly = cell.Count > 0 && cell.All(JobExtras.IsAutoExtra);
        var off = cell.Count > 0 && cell.All(l => !l.Enabled);

        // Cooldown conflicts tint red, level problems amber.
        string? warn = null;
        string? lvl = null;
        foreach (var l in cell)
        {
            if (_conflicts.TryGetValue(l, out var w)) warn = warn == null ? w : warn + "\n" + w;
            if (_levelWarns.TryGetValue(l, out var v)) lvl = lvl == null ? v : lvl + "\n" + v;
        }
        if (warn != null) ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, WarnCellBg);
        else if (lvl != null) ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, LevelCellBg);

        // Measure how tall the cell will be, then draw the Selectable first (invisible label)
        // so ImGui registers hover/click beneath our text. We then overdraw colored text on top.
        var lineCount = cell.Count + (uniqueCarries?.Count ?? 0);
        if (lineCount == 0) lineCount = 1;
        var cellWidth = ImGui.GetContentRegionAvail().X;
        var cellHeight = lineCount * ImGui.GetTextLineHeightWithSpacing();

        var startPos = ImGui.GetCursorScreenPos();
        var clicked = ImGui.Selectable($"##c{i}", false, ImGuiSelectableFlags.None, new Vector2(cellWidth, cellHeight));

        // Now overdraw text on top (cursor is already past the selectable region).
        var textPos = startPos;
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeightWithSpacing();

        if (cell.Count > 0)
        {
            foreach (var l in cell)
            {
                var lineText = FormatLineText(l, i) + (off ? "  (off)" : "");
                // Job-extra lines (Mantra, Curing Waltz, ...) are an official
                // merged schedule, not a personal edit, so they don't paint as edited.
                var lineCustom = !_isCustom && !JobExtras.IsAutoExtra(l) && (l.Custom || l.Personal);
                var kindCol = C.SheetColorByType && !lineCustom && !off && l.Action.Length > 0
                    ? MitColors.Color(MitTypes.Classify(l.Action), C) : 0u;
                uint textCol;
                if (lineCustom) textCol = ImGui.ColorConvertFloat4ToU32(EditedColor);
                else if (off) textCol = ImGui.GetColorU32(ImGuiCol.TextDisabled);
                else if (kindCol != 0) textCol = kindCol;
                else textCol = ImGui.GetColorU32(ImGuiCol.Text);
                dl.AddText(textPos, textCol, lineText);
                textPos.Y += lineH;
            }
        }

        if (uniqueCarries != null && uniqueCarries.Count > 0)
        {
            var dimColor = (ImGui.GetColorU32(ImGuiCol.TextDisabled) & 0x00FFFFFF) | 0xB0000000;
            foreach (var cItem in uniqueCarries)
            {
                dl.AddText(textPos, dimColor, $"-> {cItem}");
                textPos.Y += lineH;
            }
        }

        if (cell.Count == 0 && (uniqueCarries == null || uniqueCarries.Count == 0))
        {
            // empty — selectable still covers one line height for click target
        }

        if (clicked && !CommitPending())
        {
            _editCellRow = row;
            _editCellSlot = i;
            _cellBuf = _cellSeed = cell.Count == 0 ? "" : cell[0].Action;
            _focusPending = true;
        }

        if (ImGui.IsItemHovered())
        {
            _hoverRow = row; _hoverLive = row;
            var tipCarryStr = uniqueCarries != null ? string.Join(" · ", uniqueCarries) : null;
            var tip = cell.Count == 0
                ? (tipCarryStr != null
                    ? $"Still covered: {tipCarryStr} from an earlier row is up through this hit.\nClick to add a mit of your own for {_gridCols[i]}."
                    : $"Click to add a mit for {_gridCols[i]} here (that slot only)")
                : cell.Count == 1
                    ? $"{first}\n" + (tipCarryStr != null ? $"Still covered: {tipCarryStr} from an earlier row is up through this hit.\n" : "") + $"Click to edit {_gridCols[i]}'s mit (that slot only). Clear the text to remove it."
                    : $"{string.Join("  ·  ", cell.Select(l => FormatLineText(l, i)))}\n" + (tipCarryStr != null ? $"Still covered: {tipCarryStr} from an earlier row is up through this hit.\n" : "") + "Two lines share this moment; editing changes the first one only. Fine-tune both on the fight page.";
            string? win = null;
            foreach (var l in cell)
                if (_windows.TryGetValue(l, out var w0))
                    win = win == null ? w0 : win + "\n" + w0;
            if (jobOnly) tip = $"Job extra: only fires for {string.Join("/", cell[0].Jobs)}.\n" + tip;
            if (win != null) tip = win + "\n\n" + tip;
            if (off) tip = "Disabled on the fight page (won't be called).\n" + tip;
            if (lvl != null) tip = lvl + "\n\n" + tip;
            if (warn != null) tip = warn + "\n\n" + tip;
            tip += "\nDouble-click for the full editor.";
            // Warnings show immediately; informational tips wait the beat.
            if (warn != null || lvl != null || DelayedHover()) ImGui.SetTooltip(tip);
        }

        // Opened a frame late, so a call typed into the box is in the rows by now.
        if (AtCell(_cellEditOpening, row, i))
        {
            _cellEditOpening = null;
            BindCellEditor(row, i);
            ImGui.OpenPopup($"##celledit{i}");
        }
        if (AtCell(_cellEditAt, row, i)) DrawCellEditor(row, i);

        // Right-click: quick actions + the per-call offset, sheet-side.
        if (ImGui.BeginPopupContextItem($"##cellctx{i}"))
        {
            if (ImGui.IsWindowAppearing()) _offsetUndoArmed = true;
            ImGui.TextDisabled($"{_gridCols[i]}  ·  {row.Mechanic}");
            ImGui.Separator();
            if (cell.Count > 0)
            {
                var line = cell[0];
                var offset = line.OffsetSeconds;
                ImGui.SetNextItemWidth(110f);
                // Clamped, and not flagged Custom, since a nudge isn't a rewrite.
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

                // Multi-hit coverage: stretch this mit over later hits.
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

    // Set by a menu; the modal opens from the toolbar's scope.
    private bool _openResetAll;

    // Cue times bake in at Rebuild, so outside edits must poke this.
    public void MarkPlanDirty() => _dirty = true;

    // Full reset across every column, confirmed and undoable.
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
            if (!string.IsNullOrEmpty(_fight.Slot))
            {
                Builtin.ResetSlot(_fight, _fight.Slot);
            }
            C.Save();
            _dirty = true;
            Flash("Every column reset to the baked sheet. Plan > History (or Ctrl+Z) restores the old plan.");
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndPopup();
    }

    // ---- suggest a mit ----
    private static readonly string[] TankJobs = { "WAR", "PLD", "DRK", "GNB" };
    private static readonly string[] HealJobs = { "WHM", "SCH", "AST", "SGE" };
    private static readonly string[] DpsJobs = { "MNK", "DRG", "NIN", "SAM", "RPR", "VPR", "BRD", "MCH", "DNC", "BLM", "SMN", "RDM", "PCT" };

    private void DrawSuggestMenu(Row row, int i)
    {
        var gridCol = _gridCols[i];
        var slotIdx = _gridToSlot[i];
        var slot = _slots[slotIdx];
        var jobs = TankSlots.Contains(gridCol, StringComparer.OrdinalIgnoreCase) ? TankJobs
                 : HealerJobs.Contains(gridCol, StringComparer.OrdinalIgnoreCase) ? new[] { gridCol.ToUpperInvariant() }
                 : HealSlots.Contains(gridCol, StringComparer.OrdinalIgnoreCase) ? HealJobs
                 : DpsJobs;
        var syncLevel = _fight != null ? CooldownTracker.DutySyncLevel(_fight.TerritoryId) : 0;

        foreach (var job in jobs)
        {
            if (!AbilityBook.JobKits.TryGetValue(job, out var kit)) continue;
            if (!ImGui.BeginMenu(job)) continue;

            var shownFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var any = false;
            foreach (var name in kit)
            {
                if (CooldownTracker.PlanInfo(name) is not { } pm) continue;
                if (syncLevel > 0 && pm.Level > syncLevel) continue; // above the duty's sync
                // One entry per family, upgrades first so the best form wins.
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

    // Is this mit's timer free at t, given the column's plan?
    private bool MitFreeAt(int i, AbilityBook.PlanMit pm, float t)
    {
        var nearby = 0;
        foreach (var l in _slotLines[i])
        {
            if (!l.Enabled || MathF.Abs(l.CueTime - t) >= pm.Recast) continue;
            foreach (var other in CooldownTracker.PlanMits(l.Action))
                if (string.Equals(other.Name, pm.Name, StringComparison.OrdinalIgnoreCase)
                    || (pm.Family.Length > 0 && other.Family == pm.Family))
                {
                    nearby++;
                    break;
                }
        }
        return nearby < pm.Charges;
    }

    // Cell clipboard for right-click copy and paste.
    private string _cellClip = "";
    // Column clipboard: which fight + slot code was copied.
    private FightProfile? _copyColFight;
    private string _copyColSlot = "";

    // Overwrite one column with another slot's plan.
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
            // SamePress, not SameCall: a moment can hold several calls, and
            // tombstoning by row alone would leave the rest to come back.
            foreach (var b in Builtin.BuildLines(_fight.TerritoryId, _slots[dst]))
                if (!target.Any(l => Builtin.SamePress(l, b)))
                    _fight.DeletedCalls.Add(new DeletedCall
                    { Slot = _slots[dst], Time = b.Time, Mechanic = b.Mechanic, Action = b.Action });
        }
        Resort(dst);
        C.Save();
        _dirty = true;
        Flash($"{_slots[src]}'s plan pasted into {_slots[dst]} (that column only). Ctrl+Z undoes it.");
    }

    // ---- the full call editor, shared with the fight page ----

    private bool AtCell((float Time, string Mech, int Slot)? at, Row row, int i)
        => at is { } a && a.Slot == i && MathF.Abs(a.Time - row.Time) < 0.05f
           && MechEquals(a.Mech, row.Mechanic);

    // Bind the editor to this cell: its call, or a draft for an empty one.
    private void BindCellEditor(Row row, int i)
    {
        _cellEditAt = (row.Time, row.Mechanic, i);
        _cellEditUndoArmed = true;
        _cellEditDraft = null;
        if (GetCellLinesForJob(row, i).Count > 0) return;

        var jobs = new List<string>();
        if (!_isCustom && Jobs.ByAbbreviation(_gridCols[i]) != null) jobs.Add(_gridCols[i]);
        _cellEditDraft = new MitLine
        {
            Time = row.Time,
            Mechanic = row.Mechanic,
            Enabled = true,
            Custom = true,
            Personal = true,
            Jobs = jobs,
        };
    }

    private void DrawCellEditor(Row row, int i)
    {
        var id = $"##celledit{i}";
        if (!ImGui.BeginPopup(id))
        {
            if (!ImGui.IsPopupOpen(id)) { _cellEditAt = null; _cellEditDraft = null; }
            return;
        }

        var fight = _fight;
        var cell = GetCellLinesForJob(row, i);
        var line = _cellEditDraft ?? (cell.Count > 0 ? cell[0] : null);
        if (line == null || fight == null)
        {
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            _cellEditAt = null;
            _cellEditDraft = null;
            return;
        }

        var slotIdx = _gridToSlot[i];
        var baked = row.Bake?.Cells[i];
        MitLineEditor.Draw(line, C, new MitLineEditor.Hooks
        {
            Stale = AbortIfStale,
            BeforeEdit = (l, rewrite) =>
            {
                if (_cellEditUndoArmed)
                {
                    PushUndo($"edit {_slots[slotIdx]}'s \"{row.Mechanic}\"");
                    _cellEditUndoArmed = false;
                }
                EnsureBacked(slotIdx);
                // A draft is nobody's sheet call yet, so there is nothing to keep.
                if (rewrite && _cellEditDraft == null) Builtin.PreserveEdit(fight, _slots[slotIdx], l);
            },
            Save = () => { AdoptDraft(row, i, line); C.Save(); _dirty = true; },
            Delete = () =>
            {
                if (_cellEditDraft != null) { _cellEditDraft = null; _cellEditAt = null; }
                else DeleteCellLine(row, i);
            },
            // Offered on a draft too: an empty cell whose sheet call you deleted
            // is exactly where you want the sheet's version back.
            Reset = row.Bake != null ? () => ResetCell(row, i) : null,
            Default = baked is { Count: > 0 } ? baked[0] : null,
            Job = !_isCustom && Jobs.ByAbbreviation(_gridCols[i]) != null ? _gridCols[i] : null,
            Context = $"{TimeText(row.Time)}  ·  {row.Mechanic}  ·  {_gridCols[i]}",
        });
        ImGui.EndPopup();
    }

    // A draft joins the slot once it names an action, so opening the editor on
    // an empty cell and closing it again changes nothing.
    private void AdoptDraft(Row row, int i, MitLine line)
    {
        if (_cellEditDraft != line || string.IsNullOrWhiteSpace(line.Action)) return;
        var slotIdx = _gridToSlot[i];
        EnsureBacked(slotIdx);
        _slotLines[slotIdx].Add(line);
        Resort(slotIdx);
        _cellEditDraft = null;
        // Named as you type, so the text itself would read half-finished.
        Flash($"Added a call for {_slots[slotIdx]} at {row.Mechanic} (that slot only).");
    }

    // Delete this row's line, tombstoned like clearing the text.
    private void DeleteCellLine(Row row, int i)
    {
        if (_fight == null || row.Ghost || AbortIfStale()) return;
        var slotIdx = _gridToSlot[i];
        var cell = GetCellLinesForJob(row, i);
        if (cell.Count == 0) return;
        PushUndo($"delete {_slots[slotIdx]}'s \"{row.Mechanic}\" mit");
        EnsureBacked(slotIdx);
        var line = cell[0];
        if (!line.Custom)
            _fight.DeletedCalls.Add(new DeletedCall
            { Slot = _slots[slotIdx], Time = line.Time, Mechanic = line.Mechanic, Action = line.Action });
        _slotLines[slotIdx].Remove(line);
        Resort(slotIdx);
        C.Save();
        _dirty = true;
        Flash($"{_slots[slotIdx]}'s mit for \"{row.Mechanic}\" removed. The undo button on the row brings the sheet's version back.");
    }

    // Reset one slot's cell to the baked sheet.
    private void ResetCell(Row row, int i)
    {
        if (_fight == null || AbortIfStale()) return;
        var slotIdx = _gridToSlot[i];
        var slot = _slots[slotIdx];
        if (row.Bake == null)
        {
            // No baked pair means the sheet has nothing here, so clear it.
            if (row.Cells[i].Count == 0) { Flash($"{slot} has nothing on this row."); return; }
            PushUndo($"remove {slot}'s \"{row.Mechanic}\"");
            EnsureBacked(slotIdx);
            foreach (var line in row.Cells[i].ToList()) _slotLines[slotIdx].Remove(line);
            Resort(slotIdx);
            C.Save();
            _dirty = true;
            return;
        }

        var candidates = row.Bake.Cells[i];
        var pristine = row.Cells[i].All(l => !l.Custom)
            && row.Cells[i].Count == candidates.Count
            && candidates.All(b => row.Cells[i].Any(l => Builtin.SamePress(l, b)))
            && !_fight.DeletedCalls.Any(d => candidates.Any(b => Builtin.MatchesTombstone(d, slot, b)));

        if (pristine) { Flash($"{slot} is already at the sheet default here."); return; }

        PushUndo($"reset {slot}'s \"{row.Mechanic}\"");
        var changed = 0;
        EnsureBacked(slotIdx);

        // Remove our lines.
        foreach (var l in row.Cells[i].ToList())
        {
            _slotLines[slotIdx].Remove(l);
            changed++;
        }

        _fight.DeletedCalls.RemoveAll(d => candidates.Any(b => Builtin.MatchesTombstone(d, slot, b)));
        foreach (var b in candidates)
        {
            _slotLines[slotIdx].Add(new MitLine
            {
                Time = b.Time,
                Mechanic = b.Mechanic,
                Action = b.Action,
                Enabled = true,
                Jobs = new List<string>(b.Jobs),
            });
            changed++;
        }

        if (changed > 0)
        {
            Resort(slotIdx);
            C.Save();
            _dirty = true;
            Flash($"Reset {slot} for \"{row.Mechanic}\" to the sheet version (that slot only).");
        }
    }

    private static string TimeText(float t) => Fmt.MmssSigned(t);

    private static string SlotTip(string slot)
        => TankSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? "Tank slot"
         : HealSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? "Healer slot"
         : "DPS slot";

    // One quiet line: a flash, else the hovered row's note.
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
