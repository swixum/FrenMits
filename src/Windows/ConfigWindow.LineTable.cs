using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// Settings: the per-call line table, where a fight's plan is edited row by row.
public partial class ConfigWindow
{
    private void DrawLineTable(FightProfile fight)
    {
        ImGui.TextUnformatted($"Lines ({fight.Lines.Count})");
        ImGui.SameLine();
        if (ImGui.SmallButton("Add line")) { fight.Lines.Add(new MitLine { Custom = true }); C.Save(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("Sort by time")) SetFightLines(fight, fight.Lines.OrderBy(l => l.Time).ToList());
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Right-click a line's time / mechanic / action cell to copy, paste, duplicate, reorder, or delete it.");

        // Deleted sheet calls are remembered (so updates can't re-add them); show
        // that hidden state and offer the way back.
        var dead = fight.DeletedCalls.Count(d => string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase));
        if (dead > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"· {dead} deleted sheet call{(dead == 1 ? "" : "s")}");
            ImGui.SameLine();
            if (ImGui.SmallButton("Restore"))
            {
                fight.DeletedCalls.RemoveAll(d => string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase));
                var back = Builtin.ApplySlot(fight, fight.Slot);
                C.Save();
                FlashBuiltin($"Restored {back} deleted sheet call{(back == 1 ? "" : "s")}.");
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Bring every deleted sheet call for this slot back from the sheet.");
        }

        // Grow the table to fill what's left, leaving room for the import header
        // underneath, so a freshly loaded sheet isn't cut off.
        var avail = ImGui.GetContentRegionAvail().Y;
        var tableH = MathF.Max(200f, avail - ImGui.GetFrameHeightWithSpacing() - 8f);

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;
        if (!ImGui.BeginTable("##lines", 8, flags, new Vector2(0, tableH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("±s", ImGuiTableColumnFlags.WidthFixed, 44);
        ImGui.TableSetupColumn("Mechanic", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("Jobs", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("##opt", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableHeadersRow();

        MitLine? toDelete = null;
        // Right-click line ops (paste / duplicate / move) mutate the list, so we
        // capture them here and run them after the table loop, never mid-iteration.
        Action? deferred = null;
        for (var i = 0; i < fight.Lines.Count; i++)
        {
            var line = fight.Lines[i];
            ImGui.TableNextRow();
            ImGui.PushID(i);

            ImGui.TableNextColumn();
            // Mit-type colour chip: faint tint on the left cell (party / tank / personal).
            var chip = MitTypes.Color(MitTypes.Classify(line.Action, line.Mechanic), C);
            if (chip != 0)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, (chip & 0x00FFFFFFu) | 0x55000000u, 0);
            var on = line.Enabled;
            if (GreenCheckbox("##on", ref on)) { line.Enabled = on; C.Save(); _plugin.SheetViewWindow.MarkPlanDirty(); }

            ImGui.TableNextColumn();
            // Edit time as m:ss, using a per-edit buffer so partial typing isn't lost.
            var timeBuf = _editTimeLine == line ? _editTimeBuf : line.TimeText;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##time", ref timeBuf, 12)) _editTimeBuf = timeBuf;
            if (ImGui.IsItemActivated()) { _editTimeLine = line; _editTimeBuf = line.TimeText; }
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                // Commit ONLY if the shared buffer still belongs to this line, since
                // clicking straight into an earlier row's time cell activates that
                // cell first in the frame (overwriting the buffer), so committing
                // unconditionally here would write the OTHER row's time into this
                // line.
                if (_editTimeLine == line && SheetImport.TryParseTime(_editTimeBuf, out var sec)
                    && MathF.Abs(sec - line.Time) > 0.001f)
                {
                    PreserveBakedEdit(fight, line);
                    line.Time = sec;
                    C.Save();
                }
                if (_editTimeLine == line) _editTimeLine = null;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Type m:ss (e.g. 2:30) or seconds; right-click to reset");
            if (ImGui.BeginPopupContextItem("##timectx"))
            {
                if (DefaultLineFor(fight, line) is { } def)
                {
                    if (ImGui.MenuItem($"Reset time to default ({(int)def.Time / 60}:{(int)def.Time % 60:00})"))
                    {
                        line.Time = def.Time;
                        if (_editTimeLine == line) _editTimeBuf = line.TimeText;
                        C.Save();
                    }
                }
                else
                {
                    ImGui.TextDisabled("No baked default for this line.");
                }
                ImGui.Separator();
                LineContextItems(fight, line, i, ref deferred, ref toDelete);
                ImGui.EndPopup();
            }

            // Per-line offset: + fires just this call earlier (blank = none).
            ImGui.TableNextColumn();
            if (_editOffLine == line)
            {
                ImGui.SetNextItemWidth(-1);
                if (_offFocusPending) { ImGui.SetKeyboardFocusHere(); _offFocusPending = false; }
                ImGui.InputText("##off", ref _editOffBuf, 8);
                if (ImGui.IsItemDeactivated())
                {
                    if (_editOffLine == line && ImGui.IsItemDeactivatedAfterEdit()
                        && float.TryParse(_editOffBuf, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var ov))
                    {
                        line.OffsetSeconds = Math.Clamp(ov, -30f, 30f);
                        line.OffsetManual = true; // hand-set: the auto cooldown timer won't touch it
                        C.Save();
                        _plugin.SheetViewWindow.MarkPlanDirty();
                    }
                    if (_editOffLine == line) _editOffLine = null;
                }
            }
            else
            {
                var offLabel = line.OffsetSeconds == 0f ? " " : line.OffsetSeconds.ToString("+0.#;-0.#");
                if (line.OffsetSeconds != 0f) ImGui.PushStyleColor(ImGuiCol.Text, 0xFF5C9EF5); // orange (ABGR)
                if (ImGui.Selectable(offLabel + "##off", false))
                {
                    CommitPendingOffset();
                    _editOffLine = line;
                    _editOffBuf = _editOffSeed = line.OffsetSeconds == 0f ? ""
                        : line.OffsetSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                    _offFocusPending = true;
                }
                if (line.OffsetSeconds != 0f) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Offset just this call: + = earlier, - = later. Click to edit."
                        + (line.OffsetSeconds != 0f ? $"\nCurrently {line.OffsetSeconds:+0.#;-0.#}s." : ""));
            }

            ImGui.TableNextColumn();
            var mech = line.Mechanic;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##mech", ref mech, 256))
            {
                PreserveBakedEdit(fight, line); // before the first keystroke lands
                line.Mechanic = mech;
                C.Save();
            }
            if (ImGui.BeginPopupContextItem("##mechctx"))
            {
                var def = DefaultLineFor(fight, line);
                if (def != null && !string.Equals(def.Mechanic.Trim(), line.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    if (ImGui.MenuItem($"Reset mechanic to \"{Ellipsis(def.Mechanic, 40)}\"")) { line.Mechanic = def.Mechanic; C.Save(); }
                }
                else ImGui.TextDisabled(def == null ? "No baked default for this line." : "Already the default.");
                ImGui.Separator();
                LineContextItems(fight, line, i, ref deferred, ref toDelete);
                ImGui.EndPopup();
            }

            ImGui.TableNextColumn();
            var icon = Icons.For(line, _plugin.ActiveJobAbbreviation());
            if (icon != 0)
            {
                var h = ImGui.GetFrameHeight();
                Icons.Draw(icon, new Vector2(h, h));
                ImGui.SameLine(0, 4);
            }
            var action = line.Action;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##action", ref action, 256))
            {
                // Tombstone here too, not just on time/mechanic edits: otherwise
                // editing the ACTION first would leave later tombstones recording
                // the mutated action, which no longer matches the baked original.
                PreserveBakedEdit(fight, line);
                line.Action = action;
                C.Save();
            }
            if (ImGui.BeginPopupContextItem("##actionctx"))
            {
                var def = DefaultLineFor(fight, line);
                if (def != null && !string.Equals(def.Action.Trim(), line.Action.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    if (ImGui.MenuItem($"Reset action to \"{Ellipsis(def.Action, 40)}\"")) { line.Action = def.Action; C.Save(); }
                }
                else ImGui.TextDisabled(def == null ? "No baked default for this line." : "Already the default.");
                ImGui.Separator();
                LineContextItems(fight, line, i, ref deferred, ref toDelete);
                ImGui.EndPopup();
            }

            ImGui.TableNextColumn();
            DrawJobsCell(line);

            ImGui.TableNextColumn();
            if (ImGui.SmallButton("...")) ImGui.OpenPopup("lineopt");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Per-line lead / speech / color / mute");
            DrawLineOptionsPopup(line);

            ImGui.TableNextColumn();
            if (ImGui.SmallButton("X")) toDelete = line;

            ImGui.PopID();
        }

        ImGui.EndTable();

        deferred?.Invoke();
        if (toDelete != null)
        {
            // Sheet-baked lines get a tombstone so the zone-in top-up / slot
            // switches / sheet re-bakes can't resurrect them, while custom lines
            // exist only in the saved lists, so removal alone is final for those.
            if (!toDelete.Custom && Builtin.Has(fight.TerritoryId) && !string.IsNullOrEmpty(fight.Slot))
            {
                fight.DeletedCalls.Add(new DeletedCall
                {
                    Slot = fight.Slot,
                    Time = toDelete.Time,
                    Mechanic = toDelete.Mechanic,
                    Action = toDelete.Action,
                });
                FlashBuiltin("Line deleted. It stays deleted; Restore (above the table) brings it back.");
            }
            fight.Lines.Remove(toDelete);
            // Keep the slot's saved copy in step even right after a config reload,
            // when Lines and SavedSlots hold separate list objects.
            if (!string.IsNullOrEmpty(fight.Slot))
                fight.SavedSlots[fight.Slot] = fight.Lines;
            C.Save();
        }
    }

    // Right-click line menu shared by the time / mechanic / action cells (copy a
    // line to the in-memory clipboard, paste above / below / over this one,
    // duplicate, reorder, or delete), with list-mutating actions deferred so the
    // caller can run them once the row loop finishes.
    private void LineContextItems(FightProfile fight, MitLine line, int index, ref Action? deferred, ref MitLine? toDelete)
    {
        if (ImGui.MenuItem("Copy line")) _copiedLine = CloneLine(line);

        var hasCopy = _copiedLine != null;
        if (ImGui.MenuItem("Paste above", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            var clip = CloneLine(_copiedLine);
            var at = index;
            deferred = () => { fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), clip); C.Save(); };
        }
        if (ImGui.MenuItem("Paste below", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            var clip = CloneLine(_copiedLine);
            var at = index + 1;
            deferred = () => { fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), clip); C.Save(); };
        }
        if (ImGui.MenuItem("Paste over this line", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            PreserveBakedEdit(fight, line); // pasting over rewrites time/mechanic
            OverwriteLine(line, _copiedLine);
            _plugin.SheetViewWindow.MarkPlanDirty();
            C.Save();
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Duplicate line"))
        {
            var dup = CloneLine(line);
            var at = index + 1;
            deferred = () => { fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), dup); C.Save(); };
        }
        if (ImGui.MenuItem("Move up", string.Empty, false, index > 0))
        {
            var at = index;
            deferred = () => { (fight.Lines[at - 1], fight.Lines[at]) = (fight.Lines[at], fight.Lines[at - 1]); C.Save(); };
        }
        if (ImGui.MenuItem("Move down", string.Empty, false, index < fight.Lines.Count - 1))
        {
            var at = index;
            deferred = () => { (fight.Lines[at + 1], fight.Lines[at]) = (fight.Lines[at], fight.Lines[at + 1]); C.Save(); };
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Delete line")) toDelete = line;
    }

    // Editing the time or mechanic of a sheet-baked line breaks its identity with
    // the bake (SameCall keys on time + mechanic), so preserve it the same way
    // delete does by recording a tombstone at the ORIGINAL coordinates (call
    // BEFORE mutating the line) and flagging the line Custom so it's the user's
    // from here on.
    private static void PreserveBakedEdit(FightProfile fight, MitLine line)
        => Builtin.PreserveEdit(fight, fight.Slot, line);

    // Copy every field of src onto target in place (used by "Paste over").
    private static void OverwriteLine(MitLine target, MitLine src)
    {
        target.Time = src.Time;
        target.Mechanic = src.Mechanic;
        target.Action = src.Action;
        target.Jobs = new List<string>(src.Jobs);
        target.Enabled = src.Enabled;
        target.LeadOverride = src.LeadOverride;
        target.OffsetSeconds = src.OffsetSeconds;
        target.OffsetManual = src.OffsetManual; // carry the hand-set flag so the copy stays protected
        target.CoverUntil = src.CoverUntil;
        target.Tts = src.Tts;
        target.Sound = src.Sound;
        target.Color = src.Color;
        target.IconId = src.IconId;
    }

    private void DrawJobsCell(MitLine line)
    {
        var label = line.Jobs.Count == 0 ? "All" : string.Join(",", line.Jobs);
        if (label.Length > 14) label = label[..12] + "...";
        if (ImGui.Button(label + "##jobs", new Vector2(-1, 0)))
            ImGui.OpenPopup("jobspopup");

        if (ImGui.BeginPopup("jobspopup"))
        {
            if (ImGui.Button("All jobs")) { line.Jobs.Clear(); C.Save(); }

            foreach (var role in Enum.GetValues<JobRole>())
            {
                SeparatorText(RoleLabel(role));
                var first = true;
                foreach (var abbr in Jobs.AbbreviationsForRole(role))
                {
                    if (!first) ImGui.SameLine();
                    first = false;
                    var has = line.Jobs.Contains(abbr, StringComparer.OrdinalIgnoreCase);
                    if (GreenCheckbox(abbr, ref has))
                    {
                        if (has && !line.Jobs.Contains(abbr)) line.Jobs.Add(abbr);
                        else line.Jobs.RemoveAll(j => string.Equals(j, abbr, StringComparison.OrdinalIgnoreCase));
                        C.Save();
                    }
                }
                ImGui.SameLine();
                if (ImGui.SmallButton($"+all##{role}"))
                {
                    foreach (var abbr in Jobs.AbbreviationsForRole(role))
                        if (!line.Jobs.Contains(abbr)) line.Jobs.Add(abbr);
                    C.Save();
                }
            }
            ImGui.EndPopup();
        }
    }
}
