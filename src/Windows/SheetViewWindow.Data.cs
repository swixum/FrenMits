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

// Sheet View: building the grid and applying edits back.
public partial class SheetViewWindow
{
    // ---- data ----

    private static bool MechEquals(string a, string b)
        => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private bool IsActiveSlot(int i)
        => _fight != null && string.Equals(_slots[i], _fight.Slot, StringComparison.OrdinalIgnoreCase);

    private void Rebuild()
    {
        _dirty = false;
        _rows = new List<Row>();
        _bakedRows = new List<BakedRow>();
        _editTimeRow = null;
        _editCellRow = null;
        if (_fight == null || !Sheetable(_fight)) return;

        _isCustom = IsCustomSheet(_fight);
        _slots = _isCustom ? _fight.CustomSlots.ToArray() : Builtin.Slots(_fight.TerritoryId);
        // Pinned columns ride first, inside the frozen area.
        _order = Enumerable.Range(0, _slots.Length).OrderBy(i => IsPinnedColumn(i) ? 0 : 1).ToArray();
        _pinnedCount = _order.Count(IsPinnedColumn);
        _phases = _isCustom ? new() : Builtin.PhaseStarts(_fight.TerritoryId);
        _phaseNotes = _phases
            .Select(p => (p.Name,
                          Title: Builtin.PhaseTitle(_fight.TerritoryId, p.Name),
                          Text: Builtin.PhaseNotes(_fight.TerritoryId, p.Name)))
            .Where(p => p.Text.Length > 0)
            .ToList();
        _slotLines = new List<MitLine>[_slots.Length];
        _slotBacked = new bool[_slots.Length];

        for (var i = 0; i < _slots.Length; i++)
        {
            if (IsActiveSlot(i))
            {
                _slotLines[i] = _fight.Lines;
                _slotBacked[i] = true;
            }
            else if (_fight.SavedSlots.TryGetValue(_slots[i], out var saved) && saved.Count > 0)
            {
                _slotLines[i] = saved;
                _slotBacked[i] = true;
            }
            else if (_isCustom)
            {
                // Custom sheets have no bake: an untouched column starts empty.
                _slotLines[i] = new List<MitLine>();
                _slotBacked[i] = false;
            }
            else
            {
                // The same list object, so an edit keeps the row references.
                _slotLines[i] = Builtin.BuildLines(_fight.TerritoryId, _slots[i])
                    .Where(b => !Builtin.IsDeleted(_fight, _slots[i], b)).ToList();
                _slotBacked[i] = false;
            }
        }

        // Merge slots into rows: the same mechanic within a second.
        for (var i = 0; i < _slots.Length; i++)
        {
            foreach (var line in _slotLines[i].OrderBy(l => l.Time))
            {
                var row = _rows.FirstOrDefault(r =>
                    MathF.Abs(r.Time - line.Time) < 0.9f && MechEquals(r.Mechanic, line.Mechanic));
                if (row == null)
                {
                    row = new Row { Time = line.Time, Mechanic = line.Mechanic };
                    row.Cells = NewCellArray();
                    _rows.Add(row);
                }
                row.Cells[i].Add(line);
                row.Time = MathF.Min(row.Time, line.Time);
                // Job-restricted customs get their own tag, not "edited".
                row.Edited |= line.Custom && line.Jobs.Count == 0;
            }
        }

        // The same grid straight from the bake, unfiltered.
        for (var i = 0; !_isCustom && i < _slots.Length; i++)
        {
            foreach (var line in Builtin.BuildLines(_fight.TerritoryId, _slots[i]).OrderBy(l => l.Time))
            {
                var br = _bakedRows.FirstOrDefault(b =>
                    MathF.Abs(b.Time - line.Time) < 0.9f && MechEquals(b.Mechanic, line.Mechanic));
                if (br == null)
                {
                    br = new BakedRow { Time = line.Time, Mechanic = line.Mechanic };
                    br.Cells = NewCellArray();
                    _bakedRows.Add(br);
                }
                br.Cells[i].Add(line);
                br.Time = MathF.Min(br.Time, line.Time);
            }
        }

        // Anchor live rows per mechanic in order, not by nearest time.
        var referenced = new HashSet<BakedRow>();
        AnchorRows(referenced);
        foreach (var row in _rows)
        {
            if (row.Bake == null) continue;
            for (var i = 0; i < _slots.Length && !row.Edited; i++)
                row.Edited |= row.Bake.Cells[i].Any(b => Builtin.IsDeleted(_fight, _slots[i], b));
        }

        // Ghost rows: baked instances no live row carries anymore.
        foreach (var br in _bakedRows)
        {
            if (referenced.Contains(br)) continue;

            var carried = false;
            for (var i = 0; i < _slots.Length && !carried; i++)
                carried = br.Cells[i].Any(b => _slotLines[i].Any(l =>
                    MathF.Abs(l.Time - b.Time) < 0.9f
                    && string.Equals(l.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase)));
            if (carried) continue;

            var anyDeleted = false;
            for (var i = 0; i < _slots.Length && !anyDeleted; i++)
                anyDeleted = br.Cells[i].Any(b => Builtin.IsDeleted(_fight, _slots[i], b));
            if (!anyDeleted) continue;
            _rows.Add(new Row
            {
                Time = br.Time,
                Mechanic = br.Mechanic,
                Cells = NewCellArray(),
                Bake = br,
                Edited = true,
                Ghost = true,
            });
        }

        // Scaffold rows exist before any lines are written in.
        if (_isCustom && _fight.CustomRows.Count > 0)
            foreach (var cr in _fight.CustomRows)
                if (!_rows.Any(r => MechEquals(r.Mechanic, cr.Mechanic) && MathF.Abs(r.Time - cr.Time) < 2f))
                    _rows.Add(new Row { Time = cr.Time, Mechanic = cr.Mechanic, Cells = NewCellArray() });

        _rows = _rows.OrderBy(r => r.Time).ToList();
        foreach (var r in _rows)
        {
            var ph = "";
            foreach (var (name, time) in _phases)
                if (time <= r.Time + 0.5f) ph = name;
            r.Phase = ph.Length > 0 ? ph : (_phases.Count > 0 ? _phases[0].Name : "");

            // On a custom sheet everything is yours.
            if (_isCustom) r.Edited = false;

            // A row made only of job-restricted lines is a job extra.
            if (r.Ghost || _isCustom) continue;
            var any = false;
            var all = true;
            foreach (var cell in r.Cells)
                foreach (var l in cell)
                {
                    any = true;
                    if (!(l.Custom && l.Jobs.Count > 0)) all = false;
                }
            r.JobExtra = any && all;
        }

        FindCooldownConflicts();
        BuildCarryGhosts();
    }

    // Carry-over ghosts: a dim arrow where a buff still covers.
    private void BuildCarryGhosts()
    {
        foreach (var row in _rows) row.Carry = null;
        for (var i = 0; i < _slots.Length; i++)
        {
            var lines = _slotLines[i].Where(l => l.Enabled).OrderBy(l => l.Time).ToList();
            if (lines.Count == 0) continue;
            var start = 0;
            foreach (var row in _rows)
            {
                if (row.Ghost || row.Cells.Length <= i || row.Cells[i].Count > 0) continue;
                // Only lines close enough that any buff could still be up.
                var horizon = Cooldowns.LongestWindow + 5f;
                while (start < lines.Count && lines[start].Time < row.Time - horizon
                       && lines[start].CoverUntil < row.Time - 0.5f) start++;
                List<string>? parts = null;
                for (var k = start; k < lines.Count && lines[k].Time < row.Time - 0.5f; k++)
                {
                    var l = lines[k];
                    foreach (var pm in Cooldowns.PlanMits(l.Action))
                    {
                        var end = pm.Duration > 0f ? l.CueTime + pm.Duration : 0f;
                        if (l.CoverUntil > end) end = l.CoverUntil; // stretched coverage counts
                        if (row.Time > end + 0.01f) continue;
                        parts ??= new List<string>();
                        if (!parts.Contains(pm.Name)) parts.Add(pm.Name);
                    }
                }
                if (parts == null) continue;
                row.Carry ??= new string?[_slots.Length];
                row.Carry[i] = "-> " + string.Join(" + ", parts);
            }
        }
    }

    // Flag a mit used again before its cooldown can be back.
    private void FindCooldownConflicts()
    {
        _conflicts.Clear();
        _levelWarns.Clear();
        _windows.Clear();
        var syncLevel = _fight != null ? Cooldowns.DutySyncLevel(_fight.TerritoryId) : 0;

        for (var i = 0; i < _slots.Length; i++)
        {
            // Abilities in one recast group pool their uses.
            var uses = new Dictionary<string, (float Recast, int Charges, List<(float Time, MitLine Line, string Name, string Tag)> Uses)>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in _slotLines[i])
            {
                if (!l.Enabled) continue;
                foreach (var pm in Cooldowns.PlanMits(l.Action))
                {
                    if (syncLevel > 0 && pm.Level > syncLevel)
                    {
                        var lvlMsg = $"{pm.Name} needs level {pm.Level}; this duty syncs to {syncLevel}.";
                        _levelWarns[l] = _levelWarns.TryGetValue(l, out var lw) ? lw + "\n" + lvlMsg : lvlMsg;
                    }

                    var key = pm.Family.Length > 0 ? $"family:{pm.Family}" : pm.Name;
                    if (!uses.TryGetValue(key, out var entry))
                        uses[key] = entry = (pm.Recast, pm.Charges, new List<(float, MitLine, string, string)>());
                    // Cue time, since a per-call offset really moves the press.
                    var tag = MitLine.JobTagFor(l.Action, pm.Name);
                    if (tag.Length == 0 && l.Jobs.Count > 0)
                        tag = string.Join("/", l.Jobs
                            .Select(j2 => j2.ToUpperInvariant())
                            .OrderBy(j2 => j2, StringComparer.Ordinal));
                    entry.Uses.Add((l.CueTime, l, pm.Name, tag));
                }
            }

            foreach (var (recast, charges, list) in uses.Values)
            {
                list.Sort((a, b) => a.Time.CompareTo(b.Time));

                // Job-tagged variants are different players, so separate timers.
                var tags = list.Select(u => u.Tag).Where(t2 => t2.Length > 0)
                    .Distinct().ToList();
                if (tags.Count == 0) { CheckMitTimer(list, recast, charges); continue; }
                foreach (var tag in tags)
                    CheckMitTimer(
                        list.Where(u => u.Tag.Length == 0 || u.Tag == tag).ToList(),
                        recast, charges);
            }
        }
    }

    // Never repeat one message on a line.
    private static void AppendOnce(Dictionary<MitLine, string> map, MitLine line, string msg)
    {
        if (!map.TryGetValue(line, out var old)) map[line] = msg;
        else if (!old.Contains(msg, StringComparison.Ordinal)) map[line] = old + "\n" + msg;
    }

    // One timer's uses: press-window hints plus the walk.
    private void CheckMitTimer(List<(float Time, MitLine Line, string Name, string Tag)> list, float recast, int charges)
    {
                // Coverage pushes a press earlier, a reuse caps how late.
                for (var u = 0; u < list.Count; u++)
                {
                    var (t, line, name, _) = list[u];
                    var pm = Cooldowns.PlanMits(line.Action).FirstOrDefault(m =>
                        string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                    var lo = float.NegativeInfinity;
                    if (line.CoverUntil > t + 0.5f && pm.Duration > 0f)
                        lo = line.CoverUntil - pm.Duration;

                    // Coverage the buff can't reach even pressed at the hit.
                    if (lo > t + 0.5f)
                    {
                        AppendOnce(_conflicts, line,
                            $"{name}'s {pm.Duration:0}s duration can't reach {TimeText(line.CoverUntil)}; press it later or shorten the coverage.");
                        continue;
                    }

                    var hi = t;
                    var squeezedBy = "";
                    if (charges == 1 && u + 1 < list.Count)
                    {
                        var next = list[u + 1];
                        var latest = next.Time - recast;
                        if (latest < hi) { hi = latest; squeezedBy = $"{next.Name} at {TimeText(next.Time)}"; }
                    }

                    // Tension with the next press is left to the walk.
                    if (lo > hi + 0.5f) continue;
                    if ((lo > float.NegativeInfinity || hi < t - 0.5f) && hi >= 0f)
                    {
                        var loText = lo > float.NegativeInfinity ? TimeText(MathF.Max(lo, 0f)) : "any time";
                        var win = lo > float.NegativeInfinity && hi < t - 0.5f
                            ? $"Press {name} between {loText} and {TimeText(hi)} (covers through {TimeText(line.CoverUntil)}; needed again for {squeezedBy})."
                            : lo > float.NegativeInfinity
                                ? $"Press {name} between {loText} and {TimeText(t)} to cover through {TimeText(line.CoverUntil)}."
                                : $"Press {name} by {TimeText(hi)}; it's needed again for {squeezedBy}.";
                        AppendOnce(_windows, line, win);
                    }
                }

                if (list.Count < 2) return;

                if (charges > 1)
                {
                    // Serial recharge, like the game: one charge at a time.
                    var max = charges;
                    var avail = max;
                    var nextAt = float.PositiveInfinity; // when a charge next finishes
                    var prevName = "";
                    foreach (var (t, line, name, _) in list)
                    {
                        while (avail < max && nextAt <= t + 1f)
                        {
                            avail++;
                            nextAt = avail < max ? nextAt + recast : float.PositiveInfinity;
                        }

                        if (avail == 0)
                        {
                            var shared = prevName.Length > 0
                                && !string.Equals(prevName, name, StringComparison.OrdinalIgnoreCase)
                                ? $"; it shares a cooldown with {prevName}" : "";
                            var offNote = line.OffsetSeconds != 0f
                                ? $" (this call presses at {TimeText(t)}, offset {line.OffsetSeconds:+0.#;-0.#}s counted)"
                                : "";
                            AppendOnce(_conflicts, line,
                                $"{name}: not back for another {nextAt - t:0}s here "
                                + $"({recast:0}s cooldown, pressed at {TimeText(nextAt - recast)}, {max} charges)"
                                + shared + "." + offNote);
                            nextAt += recast;
                        }
                        else
                        {
                            if (avail == max) nextAt = t + recast;
                            avail--;
                        }
                        prevName = name;
                    }
                    return;
                }

                // Top-down feasibility, the way you'd plan by hand.
                var ready = float.NegativeInfinity;
                var walkPrev = "";
                foreach (var (t, line, name, _) in list)
                {
                    var pm = Cooldowns.PlanMits(line.Action).FirstOrDefault(m =>
                        string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
                    var lo = pm.Duration > 0f ? t - (pm.Duration - 1f) : t;
                    if (line.CoverUntil > t + 0.5f && pm.Duration > 0f)
                        lo = MathF.Max(lo, line.CoverUntil - pm.Duration);
                    lo = MathF.Min(lo, t); // reach-impossible coverage already flagged
                    var p = MathF.Max(lo, ready);
                    if (p > t + 0.5f)
                    {
                        var shared = walkPrev.Length > 0
                            && !string.Equals(walkPrev, name, StringComparison.OrdinalIgnoreCase)
                            ? $"; it shares a cooldown with {walkPrev}" : "";
                        var offNote = line.OffsetSeconds != 0f
                            ? $" (this call presses at {TimeText(t)}, offset {line.OffsetSeconds:+0.#;-0.#}s counted)"
                            : "";
                        AppendOnce(_conflicts, line,
                            $"{name}: not possible here. Even pressing the earlier ones as early as their buffs allow, "
                            + $"it is on cooldown until {TimeText(p)} ({recast:0}s cooldown, previous press ~{TimeText(p - recast)})"
                            + shared + "." + offNote);
                        // Assume the plan slips to press the moment it's back.
                    }
                    ready = p + recast;
                    walkPrev = name;
                }
    }

    // Pair live rows with baked ones, order-preserving.
    private void AnchorRows(HashSet<BakedRow> referenced)
    {
        const float skipCost = 30f;
        foreach (var group in _rows.GroupBy(r => r.Mechanic.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var lives = group.OrderBy(r => r.Time).ToList();
            var bakes = _bakedRows
                .Where(b => MechEquals(b.Mechanic, lives[0].Mechanic))
                .OrderBy(b => b.Time).ToList();
            if (bakes.Count == 0) continue;

            if (lives.Count == bakes.Count)
            {
                for (var k = 0; k < lives.Count; k++)
                {
                    lives[k].Bake = bakes[k];
                    referenced.Add(bakes[k]);
                }
                continue;
            }

            var n = lives.Count;
            var m = bakes.Count;
            var dp = new float[n + 1, m + 1];
            for (var i = 0; i <= n; i++)
                for (var j = 0; j <= m; j++)
                {
                    if (i == 0 && j == 0) continue;
                    var best = float.MaxValue;
                    if (i > 0 && j > 0)
                        best = dp[i - 1, j - 1] + MathF.Abs(lives[i - 1].Time - bakes[j - 1].Time);
                    if (j > 0) best = MathF.Min(best, dp[i, j - 1] + skipCost);
                    if (i > 0) best = MathF.Min(best, dp[i - 1, j] + skipCost);
                    dp[i, j] = best;
                }
            var (ri, rj) = (n, m);
            while (ri > 0 && rj > 0)
            {
                var match = dp[ri - 1, rj - 1] + MathF.Abs(lives[ri - 1].Time - bakes[rj - 1].Time);
                if (MathF.Abs(dp[ri, rj] - match) < 0.001f)
                {
                    lives[ri - 1].Bake = bakes[rj - 1];
                    referenced.Add(bakes[rj - 1]);
                    ri--; rj--;
                }
                else if (MathF.Abs(dp[ri, rj] - (dp[ri, rj - 1] + skipCost)) < 0.001f) rj--;
                else ri--;
            }
        }
    }

    private List<MitLine>[] NewCellArray()
    {
        var cells = new List<MitLine>[_slots.Length];
        for (var k = 0; k < _slots.Length; k++) cells[k] = new List<MitLine>();
        return cells;
    }

    // Every commit verifies our references against the plan.
    private bool PlanChangedElsewhere()
    {
        if (_fight == null) return true;
        for (var i = 0; i < _slots.Length; i++)
        {
            if (!_slotBacked[i]) continue;
            List<MitLine>? expected = IsActiveSlot(i)
                ? _fight.Lines
                : _fight.SavedSlots.TryGetValue(_slots[i], out var s) ? s : null;
            if (!ReferenceEquals(expected, _slotLines[i])) return true;
        }
        return false;
    }

    private bool AbortIfStale()
    {
        if (!PlanChangedElsewhere()) return false;
        _dirty = true;
        Flash("The plan changed on the fight page, so it was reloaded. Make the edit again.");
        return true;
    }

    // Adopt a preview slot on first edit, so it persists.
    private void EnsureBacked(int i)
    {
        if (_fight == null || _slotBacked[i]) return;
        _fight.SavedSlots[_slots[i]] = _slotLines[i];
        _slotBacked[i] = true;
    }

    private void Resort(int i)
    {
        if (_fight == null) return;
        var sorted = _slotLines[i].OrderBy(l => l.Time).ToList();
        _slotLines[i] = sorted;
        if (IsActiveSlot(i))
        {
            _fight.Lines = sorted;
            if (!string.IsNullOrEmpty(_fight.Slot)) _fight.SavedSlots[_fight.Slot] = sorted;
        }
        else
        {
            _fight.SavedSlots[_slots[i]] = sorted;
        }
    }

    // ---- edits ----

    // Land any edit in progress, since draw order can skip it.
    private bool CommitPending()
    {
        var committed = false;
        if (_editTimeRow != null)
        {
            if (_timeBuf != _timeSeed) { CommitTime(_editTimeRow); committed = true; }
            _editTimeRow = null;
        }
        if (_editCellRow != null)
        {
            if (_cellBuf != _cellSeed) { CommitCell(_editCellRow, _editCellSlot); committed = true; }
            _editCellRow = null;
        }
        return committed;
    }

    private void CommitTime(Row row)
    {
        if (_fight == null || row.Ghost || AbortIfStale()) return;
        if (!SheetImport.TryParseTime(_timeBuf, out var newTime) || MathF.Abs(newTime - row.Time) < 0.05f)
            return;

        PushUndo($"re-time \"{row.Mechanic}\"");
        var delta = newTime - row.Time;
        // The row's note (matched at the old coordinates) rides along.
        if (NoteFor(row) is { } note) note.Time += delta;
        // On a custom sheet the scaffold row entry moves too.
        if (_isCustom)
            foreach (var cr in _fight.CustomRows)
                if (MechEquals(cr.Mechanic, row.Mechanic) && MathF.Abs(cr.Time - row.Time) < 2f)
                    cr.Time += delta;
        var lines = 0;
        var slots = 0;
        for (var i = 0; i < _slots.Length; i++)
        {
            if (row.Cells[i].Count == 0) continue;
            EnsureBacked(i);
            foreach (var line in row.Cells[i])
            {
                Builtin.PreserveEdit(_fight, _slots[i], line);
                line.Time += delta;
                lines++;
            }
            Resort(i);
            slots++;
        }
        C.Save();
        _dirty = true;
        Flash($"Shifted \"{row.Mechanic}\" by {delta:+0.0;-0.0}s: {lines} line(s) across {slots} slot(s). Kept through sheet updates.");
    }

    private void CommitCell(Row row, int i) => ApplyCellText(row, i, _cellBuf);

    // Enter goes down a row, Tab across to the next column.
    private void QueueNeighborEdit(Row row, int i, bool right)
    {
        if (right)
        {
            // Follows the pin order, so Tab can jump non-adjacently.
            var k = Array.IndexOf(_order, i);
            if (k < 0 || k + 1 >= _order.Length) return; // last column: stay put
            _pendingEdit = (row.Time, row.Mechanic, _order[k + 1]);
            return;
        }

        Row? below = null;
        var seen = false;
        foreach (var r in _rows)
        {
            if (r == row) { seen = true; continue; }
            if (!seen || r.Ghost) continue;
            if (_phaseFilter.Length > 0 && r.Phase != _phaseFilter) continue;
            if (!MatchesFilter(r)) continue;
            below = r;
            break;
        }
        if (below != null) _pendingEdit = (below.Time, below.Mechanic, i);
    }

    // Cell edits touch the first line in the cell only.
    private void ApplyCellText(Row row, int i, string raw)
    {
        if (_fight == null || row.Ghost || AbortIfStale()) return;
        var text = raw.Trim();
        var cell = row.Cells[i];

        if (cell.Count > 0 && text == cell[0].Action.Trim()) return; // no-op

        // Clearing the cell deletes this slot's line, tombstoned.
        if (text.Length == 0)
        {
            DeleteCellLine(row, i);
            return;
        }

        PushUndo($"edit {_slots[i]}'s \"{row.Mechanic}\"");
        EnsureBacked(i);
        if (cell.Count == 0)
        {
            _slotLines[i].Add(new MitLine
            {
                Time = row.Time,
                Mechanic = row.Mechanic,
                Action = text,
                Enabled = true,
                Custom = true,
            });
            Flash($"Added \"{text}\" for {_slots[i]} at {row.Mechanic} (that slot only).");
        }
        else
        {
            Builtin.PreserveEdit(_fight, _slots[i], cell[0]);
            cell[0].Action = text;
            Flash($"{_slots[i]}'s mit for \"{row.Mechanic}\" updated (that slot only).");
        }
        Resort(i);
        C.Save();
        _dirty = true;
    }

    // Reset one mechanic instance to the baked sheet, every slot.
    private void ResetRow(Row row)
    {
        if (_fight == null || AbortIfStale()) return;
        if (row.Bake == null)
        {
            // No baked instance pairs with this row.
            PushUndo($"remove \"{row.Mechanic}\" (not on the sheet)");
            var removed = 0;
            for (var i = 0; i < _slots.Length; i++)
            {
                if (row.Cells[i].Count == 0) continue;
                EnsureBacked(i);
                foreach (var line in row.Cells[i].ToList())
                {
                    _slotLines[i].Remove(line);
                    removed++;
                }
                Resort(i);
            }
            if (removed == 0) { PopUndo(); Flash("This row has no lines to remove."); return; }
            C.Save();
            _dirty = true;
            Flash($"Removed {removed} line(s): \"{row.Mechanic}\" isn't on the baked sheet. Undo brings them back.");
            return;
        }

        PushUndo($"reset \"{row.Mechanic}\"");
        var touched = 0;
        for (var i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            var candidates = row.Bake!.Cells[i];
            if (row.Cells[i].Count == 0 && candidates.Count == 0) continue;

            // Skip slots already on the sheet, so previews stay unfrozen.
            var pristine = row.Cells[i].All(l => !l.Custom)
                && row.Cells[i].Count == candidates.Count
                && candidates.All(b => row.Cells[i].Any(l => Builtin.SameCall(l, b)))
                && !_fight.DeletedCalls.Any(d => candidates.Any(b => Builtin.MatchesTombstone(d, slot, b)));
            if (pristine) continue;

            EnsureBacked(i);
            foreach (var line in row.Cells[i].ToList()) _slotLines[i].Remove(line);
            foreach (var b in candidates)
            {
                _fight.DeletedCalls.RemoveAll(d => Builtin.MatchesTombstone(d, slot, b));
                // Never create a same-moment duplicate of one action.
                if (!_slotLines[i].Any(l => Builtin.SameCall(l, b)
                        || (MathF.Abs(l.Time - b.Time) < 0.9f
                            && string.Equals(l.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase))))
                    _slotLines[i].Add(b);
            }
            Resort(i);
            touched++;
        }
        if (touched == 0) PopUndo(); // nothing changed; don't log a no-op undo
        C.Save();
        _dirty = true;
        Flash(touched > 0
            ? $"\"{row.Mechanic}\" reset to the sheet across {touched} slot(s)."
            : $"\"{row.Mechanic}\" already matches the sheet.");
    }

    private void SharePlan()
    {
        if (_fight == null) return;
        try
        {
            ImGui.SetClipboardText(PlanCodes.Encode(_fight));
            Flash("Plan code copied. Friends paste it into Import and their slot updates.");
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: sheet view export failed");
        }
    }
}
