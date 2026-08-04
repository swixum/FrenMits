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

// Sheet View: building the grid's rows from the fight, and applying edits back
// onto the plan. No drawing lives here.
public partial class SheetViewWindow
{
    // ---- data -------------------------------------------------------------

    private static bool MechEquals(string a, string b)
        => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private bool IsActiveSlot(int i)
        => _fight != null && string.Equals(_slots[i], _fight.Slot, StringComparison.OrdinalIgnoreCase);


    // Whether a job-gated line belongs in this grid column.
    //
    // A built-in grid splits H1 into WHM/AST columns and H2 into SCH/SGE, so a
    // job-named column speaks for that job and nothing else. The active-slot
    // fallback exists for columns that name a SLOT rather than a job (custom
    // sheets, and MT/OT/M1/...), where the only way to know whose line it is is
    // the player's own job. Letting that fallback run on a job-named column is
    // what put a WHM's calls in the AST column, and SCH's in SGE's.
    public static bool ShowsInColumn(IReadOnlyList<string> lineJobs, string column,
                                       bool isActiveSlot, string? activeJob)
    {
        if (lineJobs.Contains(column, StringComparer.OrdinalIgnoreCase)) return true;
        if (!isActiveSlot || Jobs.ByAbbreviation(column) != null) return false;
        return !string.IsNullOrEmpty(activeJob)
               && lineJobs.Contains(activeJob!, StringComparer.OrdinalIgnoreCase);
    }
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
        
        if (_isCustom)
        {
            _gridCols = _slots;
            _gridToSlot = Enumerable.Range(0, _slots.Length).ToArray();
        }
        else
        {
            var cols = new List<string>();
            var map = new List<int>();
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == "H1")
                {
                    cols.Add("WHM"); map.Add(i);
                    cols.Add("AST"); map.Add(i);
                }
                else if (_slots[i] == "H2")
                {
                    cols.Add("SCH"); map.Add(i);
                    cols.Add("SGE"); map.Add(i);
                }
                else
                {
                    cols.Add(_slots[i]); map.Add(i);
                }
            }
            _gridCols = cols.ToArray();
            _gridToSlot = map.ToArray();
        }

        // Pinned columns (right-click a header) ride first, inside the frozen
        // area.
        _order = Enumerable.Range(0, _gridCols.Length).OrderBy(i => IsPinnedColumn(i) ? 0 : 1).ToArray();
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
                // Kept as the SAME list object so a later edit can adopt it into
                // SavedSlots without breaking the row -> line references. Run
                // through ApplyGrid so a swapped priority phase shows this
                // (non-active) column exchanged with its counterpart, same as
                // the active column already does via ApplySlot.
                _slotLines[i] = TankPriority.ApplyGrid(_fight, _fight.Slot, _slots[i],
                    Builtin.BuildLines(_fight.TerritoryId, _slots[i])
                        .Where(b => !Builtin.IsDeleted(_fight, _slots[i], b)).ToList());
                _slotBacked[i] = false;
            }
        }

        // Merge the slot plans into sheet rows: same mechanic within ~a second is
        // the same row.
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
                
                row.RawLines.Add(line);
                
                for (var c = 0; c < _gridCols.Length; c++)
                {
                    if (_gridToSlot[c] == i)
                    {
                        if (!_isCustom && line.Jobs.Count > 0)
                        {
                            var show = ShowsInColumn(line.Jobs, _gridCols[c], IsActiveSlot(i),
                                                     _plugin.GetActiveJobAbbr(_fight));

                            if (show)
                                row.Cells[c].Add(line);
                        }
                        else
                        {
                            row.Cells[c].Add(line);
                        }
                    }
                }
                row.Time = MathF.Min(row.Time, line.Time);
                // Job-restricted customs (the Job extras schedules) don't count
                // as "edited": they get their own "job extra" tag instead.
                row.Edited |= line.Personal || (line.Custom && line.Jobs.Count == 0);
            }
        }

        // The same grid straight from the bake (unfiltered): reset anchors,
        // deleted-detection, and ghost rows all come from here. Resolved the
        // same way each column's live cells are (ApplySlot for the active
        // slot, ApplyGrid for the rest), or a swapped priority phase would
        // diff a resolved live cell against its literal, un-swapped bake and
        // read as "edited" for no reason.
        for (var i = 0; !_isCustom && i < _slots.Length; i++)
        {
            var bakedForSlot = IsActiveSlot(i)
                ? Builtin.BakedLinesForFight(_fight, _slots[i], includeDeleted: true)
                : Builtin.BakedLinesForGrid(_fight, _slots[i], includeDeleted: true);
            foreach (var line in bakedForSlot.OrderBy(l => l.Time))
            {
                var br = _bakedRows.FirstOrDefault(b =>
                    MathF.Abs(b.Time - line.Time) < 0.9f && MechEquals(b.Mechanic, line.Mechanic));
                if (br == null)
                {
                    br = new BakedRow { Time = line.Time, Mechanic = line.Mechanic };
                    br.Cells = NewCellArray();
                    _bakedRows.Add(br);
                }
                
                for (var c = 0; c < _gridCols.Length; c++)
                {
                    if (_gridToSlot[c] == i)
                    {
                        if (!_showJobExtra && (line.IsJobExtra || JobExtras.IsAutoExtra(line)))
                            continue;

                        if (!_isCustom && line.Jobs.Count > 0)
                        {
                            var show = ShowsInColumn(line.Jobs, _gridCols[c], IsActiveSlot(i),
                                                     _plugin.GetActiveJobAbbr(_fight));

                            if (show)
                                br.Cells[c].Add(line);
                        }
                        else
                        {
                            br.Cells[c].Add(line);
                        }
                    }
                }
                br.Time = MathF.Min(br.Time, line.Time);
            }
        }

        // Anchor live rows to baked instances ORDER-PRESERVINGLY per mechanic,
        // not by raw nearest time: a row re-timed past the midpoint between two
        // repeats of one mechanic must still anchor to ITS instance, or reset
        // would wipe it and its twin would double up.
        var referenced = new HashSet<BakedRow>();
        AnchorRows(referenced);
        foreach (var row in _rows)
        {
            if (row.Bake == null) continue;
            for (var i = 0; i < _gridCols.Length && !row.Edited; i++)
                row.Edited |= row.Bake.Cells[i].Any(b => Builtin.IsDeleted(_fight, _slots[_gridToSlot[i]], b));
        }

        // Ghost rows: instances the sheet bakes but no live row carries anymore
        // (deleted everywhere) - shown dimmed so restore is always one click.
        foreach (var br in _bakedRows)
        {
            if (referenced.Contains(br)) continue;

            var carried = false;
            for (var i = 0; i < _gridCols.Length && !carried; i++)
                carried = br.Cells[i].Any(b => _slotLines[_gridToSlot[i]].Any(l =>
                    MathF.Abs(l.Time - b.Time) < 0.9f
                    && string.Equals(l.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase)));
            if (carried) continue;

            var anyDeleted = false;
            for (var i = 0; i < _gridCols.Length && !anyDeleted; i++)
                anyDeleted = br.Cells[i].Any(b => Builtin.IsDeleted(_fight, _slots[_gridToSlot[i]], b));
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

        // Scaffold rows: mechanics that exist before any mit does. Custom
        // sheets always show these (Build > Add row needs a plannable grid
        // immediately); a built-in's full mechanic list usually runs well
        // past what any column actually presses a mit for, so those are
        // gated behind "show mechanics with no actions".
        if (_fight.CustomRows.Count > 0 && (_isCustom || C.ShowEmptyMechanics))
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

            // On a custom sheet everything is yours: no "edited" state exists.
            if (_isCustom) r.Edited = false;

            // A row made ENTIRELY of job-restricted custom lines is a job extra
            // (e.g. Nature's Minne riding 1s off its mechanic's row).
            if (r.Ghost || _isCustom) continue;
            
            // A row is purely a Job Extra row if ALL of its source lines are job extras.
            if (r.RawLines.Count > 0 && r.RawLines.All(l => l.IsJobExtra || JobExtras.IsAutoExtra(l)))
            {
                r.JobExtra = true;
            }
            else
            {
                r.JobExtra = false;
            }
        }

        FindCooldownConflicts();

        IReadOnlyList<MitPress>? presses = null;
        if (_fight != null)
        {
            var hitTimes = _rows.Where(r => !r.Ghost).Select(r => r.Time).ToList();
            presses = TimingSolver.Solve(_fight, hitTimes, C.ShowUseWindows, C.MaxUseWindowSeconds);
            foreach (var p in presses)
            {
                var hitTime = p.SourceLine.Time;
                var relStart = p.WindowStart - hitTime;
                var relEnd = p.WindowEnd - hitTime;
                var w1 = $"Usage window: {relStart:0.0}s to {relEnd:0.0}s relative to this hit.";
                AppendOnce(_windows, p.SourceLine, w1);
            }
        }

        BuildCarryGhosts(presses);
    }

    // Carry-over ghosts, like the reference sheets' arrows: an empty cell shows
    // a dim "-> Name" when a buff pressed on an EARLIER row is still up on this
    // one (real durations, per part), so while building you can SEE what a hit
    // is already covered by before adding more.
    private void BuildCarryGhosts(IReadOnlyList<MitPress>? presses)
    {
        foreach (var row in _rows) row.Carry = null;
        for (var c = 0; c < _gridCols.Length; c++)
        {
            var slotIdx = _gridToSlot[c];
            var lines = _slotLines[slotIdx].Where(l => l.Enabled && (l.Jobs.Count == 0 || l.Jobs.Contains(_gridCols[c], StringComparer.OrdinalIgnoreCase))).OrderBy(l => l.Time).ToList();
            if (lines.Count == 0) continue;
            var start = 0;
            foreach (var row in _rows)
            {
                if (row.Ghost || row.Cells.Length <= c) continue;
                // Only lines close enough that any buff could still be up. The
                // horizon comes from the duration table rather than a number typed
                // here, which was 45s - exactly Excogitation's length, so the
                // longest buff in the game sat right on the edge of being missed.
                var horizon = AbilityBook.LongestWindow + 5f;
                while (start < lines.Count && lines[start].Time < row.Time - horizon
                       && lines[start].CoverUntil < row.Time - 0.5f) start++;
                List<string>? parts = null;
                for (var k = start; k < lines.Count && lines[k].Time < row.Time - 0.5f; k++)
                {
                    var l = lines[k];
                    foreach (var pm in CooldownTracker.PlanMits(l.Action))
                    {
                        if (AbilityBook.IsNoCarryOver(pm.Name)) continue;

                        var end = pm.Duration > 0f ? l.CueTime + pm.Duration : 0f;
                        if (presses != null)
                        {
                            var press = presses.FirstOrDefault(p => p.SourceLine == l && p.MitName == pm.Name);
                            if (press != null) end = pm.Duration > 0f ? press.WindowEnd + pm.Duration : 0f;
                        }
                        if (l.CoverUntil > end) end = l.CoverUntil; // stretched coverage counts
                        if (row.Time > end + 0.01f) continue;
                        parts ??= new List<string>();
                        if (!parts.Contains(pm.Name)) parts.Add(pm.Name);
                    }
                }
                if (parts == null) continue;
                row.Carry ??= new List<string>?[_gridCols.Length];
                row.Carry[c] = parts;
            }
        }
    }

    // Flag any line whose mit is used again before its cooldown (with charges
    // honored) can possibly be back, per slot.
    private void FindCooldownConflicts()
    {
        _conflicts.Clear();
        _levelWarns.Clear();
        _windows.Clear();
        var syncLevel = _fight != null ? CooldownTracker.DutySyncLevel(_fight.TerritoryId) : 0;

        for (var i = 0; i < _slots.Length; i++)
        {
            // Abilities in the same recast GROUP share one timer (Bloodwhetting /
            // Nascent Flash / Raw Intuition), so group-mates pool their uses.
            var uses = new Dictionary<string, (float Recast, int Charges, List<(float Time, MitLine Line, string Name, string Tag)> Uses)>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in _slotLines[i])
            {
                if (!l.Enabled) continue;
                foreach (var pm in CooldownTracker.PlanMits(l.Action))
                {
                    if (syncLevel > 0 && pm.Level > syncLevel)
                    {
                        var lvlMsg = $"{pm.Name} needs level {pm.Level}; this duty syncs to {syncLevel}.";
                        _levelWarns[l] = _levelWarns.TryGetValue(l, out var lw) ? lw + "\n" + lvlMsg : lvlMsg;
                    }

                    var key = pm.Family.Length > 0 ? $"family:{pm.Family}" : pm.Name;
                    if (!uses.TryGetValue(key, out var entry))
                        uses[key] = entry = (pm.Recast, pm.Charges, new List<(float, MitLine, string, string)>());
                    // CUE time, not plan time: a per-call offset genuinely moves
                    // the press, so it must count in the timer math.
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

                // Job-tagged variants are different players' presses: "Party Mit
                // (GNB/DRK)" and "(WAR/PLD)" never share one timer.
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

    // Never repeat one message on a line (untagged uses run through several
    // tag groups, and each group would otherwise re-append the same text).
    private static void AppendOnce(Dictionary<MitLine, string> map, MitLine line, string msg)
    {
        if (!map.TryGetValue(line, out var old)) map[line] = msg;
        else if (!old.Contains(msg, StringComparison.Ordinal)) map[line] = old + "\n" + msg;
    }

    // Rows that only restate a mit already running, dropped from the timer walk
    // and told so in their tooltip. The list must be one player's, in time order.
    private List<(float Time, MitLine Line, string Name, string Tag)> DropLingering(
        List<(float Time, MitLine Line, string Name, string Tag)> list)
    {
        if (list.Count < 2) return list;

        var uses = list
            .Select(u => (u.Name, u.Time,
                          CooldownTracker.PlanMits(u.Line.Action)
                              .FirstOrDefault(m => string.Equals(m.Name, u.Name, StringComparison.OrdinalIgnoreCase))
                              .Duration,
                          u.Line.CoverUntil))
            .ToList();
        var carried = CarryOver.Mark(uses);

        var kept = new List<(float Time, MitLine Line, string Name, string Tag)>(list.Count);
        var pressedAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < list.Count; i++)
        {
            if (!carried[i]) { pressedAt[list[i].Name] = list[i].Time; kept.Add(list[i]); continue; }
            var from = pressedAt.GetValueOrDefault(list[i].Name);
            AppendOnce(_windows, list[i].Line, MathF.Abs(from - list[i].Time) < 0.5f
                ? $"{list[i].Name} is already called on another row at this moment; one press covers both."
                : $"{list[i].Name} is already up here from the press at {TimeText(from)}; "
                  + "this row is that same press, not a second one.");
        }
        return kept;
    }

    // One mit timer's worth of uses (same recast group + compatible job tags):
    // press-window hints plus the top-down feasibility walk.
    private void CheckMitTimer(List<(float Time, MitLine Line, string Name, string Tag)> list, float recast, int charges)
    {
                // A sheet names a mit again on every mechanic its own buff still
                // reaches, so those rows are one press, not several. Take them
                // out before any timer math: a repeat inside the window neither
                // spends the cooldown nor can be impossible.
                list = DropLingering(list);

                // Press-window HINTS: coverage pushes the press EARLIER (the
                // buff must reach the last covered hit), a same-timer reuse
                // caps how LATE it can go.
                for (var u = 0; u < list.Count; u++)
                {
                    var (t, line, name, _) = list[u];
                    var pm = CooldownTracker.PlanMits(line.Action).FirstOrDefault(m =>
                        string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                    var lo = float.NegativeInfinity;
                    if (line.CoverUntil > t + 0.5f && pm.Duration > 0f)
                        lo = line.CoverUntil - pm.Duration;

                    // Coverage the buff can't physically reach even pressed AT
                    // the hit: that one is wrong on its own, chain or no chain.
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

                    // Tension between coverage and the next press is left to
                    // the walk (the next press may float earlier than its hit).
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
                    // Serial recharge, like the game: charges regenerate one at
                    // a time, so Oblation @0 and @5 is back at 60 and 120.
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

                // Top-down feasibility, first mechanic first - the way you'd
                // build the plan by hand.
                var ready = float.NegativeInfinity;
                var walkPrev = "";
                foreach (var (t, line, name, _) in list)
                {
                    var pm = CooldownTracker.PlanMits(line.Action).FirstOrDefault(m =>
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

    // Pair each mechanic's live rows with its baked instances, order-preserving
    // and minimizing total time distance.
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
        var cells = new List<MitLine>[_gridCols.Length];
        for (var k = 0; k < _gridCols.Length; k++) cells[k] = new List<MitLine>();
        return cells;
    }

    // Our cached references would write stale data back if the fight page
    // replaced a list object, so every commit verifies them first and turns a
    // mismatch into a harmless "try again".
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

    // Adopt a bake-preview slot into the profile the first time it's edited, so
    // the edit persists.
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

    // ---- edits ------------------------------------------------------------

    // Land any edit still in progress: clicking from a half-typed cell into an
    // earlier row (or the toolbar) must not drop the text, since draw order can
    // skip the old editor's commit frame.
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
        var processedLines = new HashSet<MitLine>();
        for (var i = 0; i < _gridCols.Length; i++)
        {
            if (row.Cells[i].Count == 0) continue;
            var slotIdx = _gridToSlot[i];
            EnsureBacked(slotIdx);
            var resort = false;
            foreach (var line in row.Cells[i])
            {
                if (!processedLines.Add(line)) continue;
                Builtin.PreserveEdit(_fight, _slots[slotIdx], line);
                line.Time += delta;
                lines++;
                resort = true;
            }
            if (resort)
            {
                Resort(slotIdx);
                slots++;
            }
        }
        C.Save();
        _dirty = true;
        Flash($"Shifted \"{row.Mechanic}\" by {delta:+0.0;-0.0}s: {lines} line(s) across {slots} slot(s). Kept through sheet updates.");
    }

    private void CommitCell(Row row, int i) => ApplyCellText(row, i, _cellBuf);

    // Enter = the visible row below (same column); Tab = the next column (same
    // row).
    private void QueueNeighborEdit(Row row, int i, bool right)
    {
        if (right)
        {
            // Follows the pin/submission order; a hand-dragged display order
            // isn't readable from ImGui, so Tab may jump non-adjacently there.
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

    // Cell edits touch the FIRST line in the cell only; a cell holding two real
    // lines (rare merge of near-simultaneous casts) stacks them and leaves the
    // second line alone.
    private void ApplyCellText(Row row, int i, string raw)
    {
        if (_fight == null || row.Ghost || AbortIfStale()) return;
        var slotIdx = _gridToSlot[i];
        var text = raw.Trim();
        var cell = GetCellLinesForJob(row, i);

        if (cell.Count > 0 && text == cell[0].Action.Trim()) return; // no-op

        // Clearing the cell = delete this slot's line (tombstoned like a delete
        // on the fight page, so it stays gone; the undo button restores).
        if (text.Length == 0)
        {
            DeleteCellLine(row, i);
            return;
        }

        PushUndo($"edit {_slots[slotIdx]}'s \"{row.Mechanic}\"");
        EnsureBacked(slotIdx);
        if (cell.Count == 0)
        {
            var jobs = new List<string>();
            if (!_isCustom && Jobs.ByAbbreviation(_gridCols[i]) != null)
                jobs.Add(_gridCols[i]);

            _slotLines[slotIdx].Add(new MitLine
            {
                Time = row.Time,
                Mechanic = row.Mechanic,
                Action = text,
                Enabled = true,
                Custom = true,
                Personal = true,
                Jobs = jobs,
            });
            Flash($"Added \"{text}\" for {_slots[slotIdx]} at {row.Mechanic} (that slot only).");
        }
        else
        {
            Builtin.PreserveEdit(_fight, _slots[slotIdx], cell[0]);
            cell[0].Action = text;
            cell[0].Personal = true;
            if (!_isCustom && Jobs.ByAbbreviation(_gridCols[i]) != null)
            {
                if (!cell[0].Jobs.Contains(_gridCols[i], StringComparer.OrdinalIgnoreCase))
                    cell[0].Jobs.Add(_gridCols[i]);
            }
            Flash($"{_slots[slotIdx]}'s mit for \"{row.Mechanic}\" updated (that slot only).");
        }
        Resort(slotIdx);
        C.Save();
        _dirty = true;
    }

    // Reset one mechanic instance to the baked sheet, every slot: precise to the
    // anchored instance (row.Bake), so neighbors and other instances of the same
    // mechanic are never touched.
    private void ResetRow(Row row)
    {
        if (_fight == null || AbortIfStale()) return;
        if (row.Bake == null)
        {
            // No baked instance pairs with this row: it's an extra instance the
            // sheet doesn't have, or a leftover edit under a mechanic name the
            // sheet renamed.
            PushUndo($"remove \"{row.Mechanic}\" (not on the sheet)");
            var removed = 0;
            var processedSlots = new HashSet<int>();
            for (var i = 0; i < _gridCols.Length; i++)
            {
                if (row.Cells[i].Count == 0) continue;
                var slotIdx = _gridToSlot[i];
                if (!processedSlots.Add(slotIdx)) continue;
                EnsureBacked(slotIdx);
                foreach (var line in row.Cells[i].ToList())
                {
                    _slotLines[slotIdx].Remove(line);
                    removed++;
                }
                Resort(slotIdx);
            }
            if (removed == 0) { PopUndo(); Flash("This row has no lines to remove."); return; }
            C.Save();
            _dirty = true;
            Flash($"Removed {removed} line(s): \"{row.Mechanic}\" isn't on the baked sheet. Undo brings them back.");
            return;
        }

        PushUndo($"reset \"{row.Mechanic}\"");
        var touched = 0;
        var processedResetSlots = new HashSet<int>();
        for (var i = 0; i < _gridCols.Length; i++)
        {
            var slotIdx = _gridToSlot[i];
            if (!processedResetSlots.Add(slotIdx)) continue;
            var slot = _slots[slotIdx];
            // Since we're resetting the whole slot, we need to gather all cells that map to this slot
            var slotCells = new List<MitLine>();
            var bakeCells = new List<MitLine>();
            for (var j = 0; j < _gridCols.Length; j++)
            {
                if (_gridToSlot[j] == slotIdx)
                {
                    slotCells.AddRange(row.Cells[j]);
                    bakeCells.AddRange(row.Bake!.Cells[j]);
                }
            }
            slotCells = slotCells.Distinct().ToList();
            bakeCells = bakeCells.Distinct().ToList();
            
            if (slotCells.Count == 0 && bakeCells.Count == 0) continue;

            // Skip slots already exactly on the sheet, so resetting one row
            // doesn't freeze untouched preview columns into SavedSlots.
            var pristine = slotCells.All(l => !l.Custom)
                && slotCells.Count == bakeCells.Count
                && bakeCells.All(b => slotCells.Any(l => Builtin.SamePress(l, b)))
                && !_fight.DeletedCalls.Any(d => bakeCells.Any(b => Builtin.MatchesTombstone(d, slot, b)));
            if (pristine) continue;

            EnsureBacked(slotIdx);
            foreach (var line in slotCells) _slotLines[slotIdx].Remove(line);
            foreach (var b in bakeCells)
            {
                _fight.DeletedCalls.RemoveAll(d => Builtin.MatchesTombstone(d, slot, b));
                
                // Aggressively remove any existing copy of this exact action at this time to prevent duplication bugs
                _slotLines[slotIdx].RemoveAll(l => Builtin.SameCall(l, b) && string.Equals(l.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase));
                
                _slotLines[slotIdx].Add(b);
            }
            Resort(slotIdx);
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

