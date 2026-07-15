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

// The whole raid plan as one sheet - the in-game version of the Google sheet
// everyone plans from. Rows are the fight's mechanics in timeline order, columns
// are the sheet slots. Your slot's column IS the live plan (same line objects the
// overlay and fight page read); the other columns edit your saved copy of each
// slot, which reaches friends via Share plan -> their import.
//
// The Time cell is the bulk edit: re-timing a mechanic shifts every slot's line
// for it at once. Every edit routes through Builtin.PreserveEdit, so sheet
// updates and zone-ins keep it, exactly like edits made on the fight page.
// Rows the sheet bakes but every slot has deleted show as dimmed "deleted"
// ghost rows, so the restore path is always visible.
public class SheetViewWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    // ### so the ImGui window ID stays "fmsheet" no matter how the visible
    // title changes; future renames won't reset the saved position/size again.
    public SheetViewWindow(Plugin plugin) : base("Fren Mits - Sheet View###fmsheet")
    {
        _plugin = plugin;
        Size = new Vector2(1150, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(660, 320),
            MaximumSize = new Vector2(4096, 4096),
        };
    }

    public override void PreDraw()
    {
        Theme.PushWindow();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(1);
        Theme.PopWindow();
    }

    // ---- state ------------------------------------------------------------

    private FightProfile? _fight;
    private string _phaseFilter = "";              // "" = all phases
    private bool _dirty = true;
    private bool _wasFocused;

    private string[] _slots = Array.Empty<string>();
    private List<MitLine>[] _slotLines = Array.Empty<List<MitLine>>();
    private bool[] _slotBacked = Array.Empty<bool>(); // list already lives in the fight profile
    private List<Row> _rows = new();
    private List<BakedRow> _bakedRows = new();
    private List<(string Name, float Time)> _phases = new();
    // The sheet's per-phase "Notes" footer (only phases that have one).
    private List<(string Name, string Title, string Text)> _phaseNotes = new();
    // Column display order: your slot first (pinned into the frozen area).
    private int[] _order = Array.Empty<int>();
    // Lines whose mit repeats before its cooldown can be back (message per line).
    private readonly Dictionary<MitLine, string> _conflicts = new();
    // Text filter: show only rows whose mechanic or any mit matches.
    private string _filter = "";

    // Search-and-replace popup state.
    private string _replFind = "";
    private string _replWith = "";
    private bool _replMineOnly;

    // Sticky-phase pill state, recomputed every frame from the top visible row.
    private float _headerY;
    private int _rowIdxDrawing = -1;
    private int _firstDrawnIdx = -1;
    private int _stickyRowIdx = -1;
    private string _stickyTitle = "";

    // A mechanic instance as the SHEET bakes it (unfiltered), used as the anchor
    // for row resets, edited/deleted detection, and ghost rows.
    private sealed class BakedRow
    {
        public float Time;
        public string Mechanic = "";
        public List<MitLine>[] Cells = Array.Empty<List<MitLine>>();
    }

    private sealed class Row
    {
        public float Time;
        public string Mechanic = "";
        public string Phase = "";
        public List<MitLine>[] Cells = Array.Empty<List<MitLine>>();
        public BakedRow? Bake;      // nearest same-mechanic baked instance
        public bool Edited;
        public bool Ghost;          // baked instance deleted from every slot
    }

    private Row? _editTimeRow;
    private string _timeBuf = "";
    private string _timeSeed = "";
    private Row? _editCellRow;
    private int _editCellSlot = -1;
    private string _cellBuf = "";
    private string _cellSeed = "";
    private bool _focusPending;
    private bool Editing => _editTimeRow != null || _editCellRow != null;

    private string _flash = "";
    private DateTime _flashAt;
    private void Flash(string msg) { _flash = msg; _flashAt = DateTime.Now; }

    // Notes: the row the mouse is on (its note shows in the footer strip) and
    // the edit buffer for the right-click note popup.
    private Row? _hoverRow;
    private string _noteBuf = "";

    // Tight window (4s): some mechanics repeat under 10s apart with the same
    // label (Ultimate Embrace at 371/378), and a wider match would cross-link
    // one note to both rows.
    private SheetNote? NoteFor(Row row)
        => _fight?.Notes.FirstOrDefault(n =>
            MechEquals(n.Mechanic, row.Mechanic) && MathF.Abs(n.Time - row.Time) < 4f);

    private void SaveNote(Row row, string text)
    {
        if (_fight == null) return;
        var note = NoteFor(row);
        if (string.IsNullOrWhiteSpace(text))
        {
            if (note != null) _fight.Notes.Remove(note);
        }
        else if (note == null)
        {
            _fight.Notes.Add(new SheetNote { Time = row.Time, Mechanic = row.Mechanic, Text = text });
        }
        else
        {
            note.Text = text;
        }
        C.Save();
    }

    // ---- opening ----------------------------------------------------------

    public void Open(FightProfile? fight = null)
    {
        _fight = fight ?? PickDefaultFight();
        _dirty = true;
        IsOpen = true;
        BringToFront(); // safe outside a draw frame, unlike ImGui.SetWindowFocus
    }

    private FightProfile? PickDefaultFight()
    {
        var terr = Service.ClientState.TerritoryType;
        // Prefer fights that already have a slot picked: the grid needs one.
        return C.Fights.FirstOrDefault(f => Builtin.Has(f.TerritoryId) && f.TerritoryId == terr && f.Enabled)
            ?? C.Fights.FirstOrDefault(f => f.TerritoryId == Builtin.DmuTerritory && !string.IsNullOrEmpty(f.Slot))
            ?? C.Fights.FirstOrDefault(f => Builtin.Has(f.TerritoryId) && !string.IsNullOrEmpty(f.Slot))
            ?? C.Fights.FirstOrDefault(f => f.TerritoryId == Builtin.DmuTerritory)
            ?? C.Fights.FirstOrDefault(f => Builtin.Has(f.TerritoryId));
    }

    // ---- data -------------------------------------------------------------

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
        if (_fight == null || !Builtin.Has(_fight.TerritoryId)) return;

        _slots = Builtin.Slots(_fight.TerritoryId);
        // Your slot's column first, pinned next to Mechanic in the frozen area.
        var act = -1;
        for (var i = 0; i < _slots.Length; i++) if (IsActiveSlot(i)) act = i;
        _order = Enumerable.Range(0, _slots.Length).OrderBy(i => i == act ? -1 : i).ToArray();
        _phases = Builtin.PhaseStarts(_fight.TerritoryId);
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
            else
            {
                // Fresh bake preview, minus this slot's deleted calls. Kept as the
                // SAME list object so a later edit can adopt it into SavedSlots
                // without breaking the row -> line references.
                _slotLines[i] = Builtin.BuildLines(_fight.TerritoryId, _slots[i])
                    .Where(b => !Builtin.IsDeleted(_fight, _slots[i], b)).ToList();
                _slotBacked[i] = false;
            }
        }

        // Merge the slot plans into sheet rows: same mechanic within ~a second is
        // the same row. A per-slot renamed mechanic becomes its own row - honest,
        // since its call really does differ now.
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
                row.Edited |= line.Custom;
            }
        }

        // The same grid straight from the bake (unfiltered): reset anchors,
        // deleted-detection, and ghost rows all come from here.
        for (var i = 0; i < _slots.Length; i++)
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

        // Anchor live rows to baked instances ORDER-PRESERVINGLY per mechanic,
        // not by raw nearest time: a row re-timed past the midpoint between two
        // repeats of one mechanic must still anchor to ITS instance, or reset
        // would wipe it and its twin would double up.
        var referenced = new HashSet<BakedRow>();
        AnchorRows(referenced);
        foreach (var row in _rows)
        {
            if (row.Bake == null) continue;
            for (var i = 0; i < _slots.Length && !row.Edited; i++)
                row.Edited |= row.Bake.Cells[i].Any(b => Builtin.IsDeleted(_fight, _slots[i], b));
        }

        // Ghost rows: instances the sheet bakes but no live row carries anymore
        // (deleted everywhere) - shown dimmed so restore is always one click.
        // "Carried" is checked by time + action too, so a mechanic RENAMED on the
        // fight page (same call, new label) is not mistaken for a deleted one.
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

        _rows = _rows.OrderBy(r => r.Time).ToList();
        foreach (var r in _rows)
        {
            var ph = "";
            foreach (var (name, time) in _phases)
                if (time <= r.Time + 0.5f) ph = name;
            r.Phase = ph.Length > 0 ? ph : (_phases.Count > 0 ? _phases[0].Name : "");
        }

        FindCooldownConflicts();
    }

    // Flag any line whose mit is used again before its cooldown (with charges
    // honored) can possibly be back, per slot. Uses plan times, not the live
    // recast, so it works while planning at the aetheryte.
    private void FindCooldownConflicts()
    {
        _conflicts.Clear();
        for (var i = 0; i < _slots.Length; i++)
        {
            var uses = new Dictionary<string, (Cooldowns.PlanMit Mit, List<(float Time, MitLine Line)> Uses)>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in _slotLines[i])
            {
                if (!l.Enabled) continue;
                foreach (var pm in Cooldowns.PlanMits(l.Action))
                {
                    if (!uses.TryGetValue(pm.Name, out var entry))
                        uses[pm.Name] = entry = (pm, new List<(float, MitLine)>());
                    entry.Uses.Add((l.Time, l));
                }
            }

            foreach (var (mit, list) in uses.Values)
            {
                if (list.Count < 2) continue;
                list.Sort((a, b) => a.Time.CompareTo(b.Time));

                // Serial recharge, like the game: charges regenerate one at a
                // time, so Oblation @0 and @5 is back at 60 and 120, not 60/65.
                var max = mit.Charges;
                var avail = max;
                var nextAt = float.PositiveInfinity; // when a charge next finishes
                foreach (var (t, line) in list)
                {
                    // Regenerate charges finished by now (1s resync tolerance).
                    while (avail < max && nextAt <= t + 1f)
                    {
                        avail++;
                        nextAt = avail < max ? nextAt + mit.Recast : float.PositiveInfinity;
                    }

                    if (avail == 0)
                    {
                        var msg = $"{mit.Name}: not back for another {nextAt - t:0}s here "
                                + $"({mit.Recast:0}s cooldown" + (max > 1 ? $", {max} charges)." : ").");
                        _conflicts[line] = _conflicts.TryGetValue(line, out var old) ? old + "\n" + msg : msg;
                        // The plan presumably slips to use the charge the moment
                        // it lands, so its recharge slot is consumed.
                        nextAt += mit.Recast;
                    }
                    else
                    {
                        if (avail == max) nextAt = t + mit.Recast; // pipeline starts
                        avail--;
                    }
                }
            }
        }
    }

    // Pair each mechanic's live rows with its baked instances, order-preserving
    // and minimizing total time distance. Equal counts pair by index (handles
    // arbitrary shifts); unequal counts run a tiny min-cost alignment where
    // skipping an instance costs a flat penalty.
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

    // The fight page can REPLACE list objects (imports, sort, reset to sheet).
    // Our cached references would then write stale data back, so every commit
    // verifies them first and turns a mismatch into a harmless "try again".
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
    // the edit persists. Object identity is preserved: the rendered rows point
    // into this same list.
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
    // skip the old editor's commit frame. Returns true if something committed;
    // the caller then swallows its own action so the next click operates on
    // freshly rebuilt rows instead of a stale grid.
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

        var delta = newTime - row.Time;
        // The row's note (matched at the old coordinates) rides along.
        if (NoteFor(row) is { } note) note.Time += delta;
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

    // Cell edits touch the FIRST line in the cell only; a cell holding two real
    // lines (rare merge of near-simultaneous casts) shows "+1" and leaves the
    // second line alone.
    private void CommitCell(Row row, int i)
    {
        if (_fight == null || row.Ghost || AbortIfStale()) return;
        var text = _cellBuf.Trim();
        var cell = row.Cells[i];

        if (cell.Count > 0 && text == cell[0].Action.Trim()) return; // no-op

        // Clearing the cell = delete this slot's line (tombstoned like a delete
        // on the fight page, so it stays gone; the undo button restores).
        if (text.Length == 0)
        {
            DeleteCellLine(row, i);
            return;
        }

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

    // Reset one mechanic instance to the baked sheet, every slot: precise to the
    // anchored instance (row.Bake), so neighbors and other instances of the same
    // mechanic are never touched.
    private void ResetRow(Row row)
    {
        if (_fight == null || AbortIfStale()) return;
        if (row.Bake == null)
        {
            Flash("Can't match this row to the sheet (renamed mechanic?). Use the fight page's Reset to sheet instead.");
            return;
        }

        var touched = 0;
        for (var i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            var candidates = row.Bake.Cells[i];
            if (row.Cells[i].Count == 0 && candidates.Count == 0) continue;

            // Skip slots already exactly on the sheet, so resetting one row
            // doesn't freeze untouched preview columns into SavedSlots.
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
                // Never create a same-moment same-action duplicate (a renamed
                // line at the baked time already covers this call).
                if (!_slotLines[i].Any(l => Builtin.SameCall(l, b)
                        || (MathF.Abs(l.Time - b.Time) < 0.9f
                            && string.Equals(l.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase))))
                    _slotLines[i].Add(b);
            }
            Resort(i);
            touched++;
        }
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
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(_fight);
            var raw = System.Text.Encoding.UTF8.GetBytes(json);
            using var ms = new System.IO.MemoryStream();
            using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal))
                gz.Write(raw, 0, raw.Length);
            ImGui.SetClipboardText("FRENMITS2:" + Convert.ToBase64String(ms.ToArray()));
            Flash("Plan code copied. Friends paste it into Import and their slot updates.");
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: sheet view export failed");
        }
    }

    // ---- drawing ----------------------------------------------------------

    public override void Draw()
    {
        Theme.PushWidgets();
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 16f);
        try { DrawBody(); }
        finally
        {
            ImGui.PopStyleVar(2);
            Theme.PopWidgets();
        }
    }

    private void DrawBody()
    {
        if (_fight != null && !C.Fights.Contains(_fight)) { _fight = null; _dirty = true; }
        if (_fight == null) _fight = PickDefaultFight();
        if (_fight == null)
        {
            ImGui.TextDisabled("No built-in fight to show. Add one on the Home page first.");
            return;
        }
        if (string.IsNullOrEmpty(_fight.Slot))
        {
            DrawFightPicker(); // still allow switching to a fight that HAS a slot
            ImGui.Spacing();
            ImGui.TextWrapped("Pick your slot for this fight first (fight page, \"Your slot\"), then come back; "
                + "the sheet needs to know which column is yours.");
            return;
        }

        // Regaining focus re-reads every slot, so edits made on the fight page
        // while this window sat in the background always show up. Never rebuild
        // mid-edit: commits set _dirty and it lands right after.
        var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (focused && !_wasFocused) _dirty = true;
        _wasFocused = focused;
        if (_dirty && !Editing) Rebuild();

        DrawToolbar();
        ImGui.Spacing();
        DrawGrid();
        DrawNotesPanel();
        DrawFooter();
    }

    // ---- sheet notes (the per-phase "Notes" footer from the sheet's tabs) ----

    private float NotesBodyHeight() => ImGui.GetTextLineHeightWithSpacing() * 7f;

    // Vertical space the notes panel takes below the grid, so the table can
    // shrink to make room (header row + the body when expanded).
    private float NotesReserve()
    {
        if (_phaseNotes.Count == 0) return 0f;
        var h = ImGui.GetFrameHeightWithSpacing();
        if (C.SheetNotesOpen) h += NotesBodyHeight() + ImGui.GetStyle().ItemSpacing.Y;
        return h;
    }

    private void DrawNotesPanel()
    {
        if (_fight == null || _phaseNotes.Count == 0) return;

        ImGui.SetNextItemOpen(C.SheetNotesOpen, ImGuiCond.Always);
        var label = _phaseFilter.Length > 0 ? $"Sheet notes ({_phaseFilter})" : "Sheet notes";
        var open = ImGui.CollapsingHeader($"{label}###sheetnotes");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("The notes section from the bottom of each phase's sheet tab.");
        if (open != C.SheetNotesOpen) { C.SheetNotesOpen = open; C.Save(); }
        if (!open) return;

        if (ImGui.BeginChild("##sheetnotesbody", new Vector2(0, NotesBodyHeight()), true))
        {
            var first = true;
            foreach (var (name, title, text) in _phaseNotes)
            {
                if (_phaseFilter.Length > 0 && name != _phaseFilter) continue;
                if (!first) { ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing(); }
                first = false;
                ImGui.TextColored(NoteBlue, title);
                ImGui.TextWrapped(text);
            }
        }
        ImGui.EndChild();
    }

    // Fight picker (only when there's a choice to make). Also shown on the
    // pick-your-slot screen so a slotless default is never a dead end.
    // Grouped by category (Ultimate / Savage / ...) with your slot shown per
    // fight, so the list stays scannable as more fights ship.
    private static readonly string[] PickerCategories = { "Ultimate", "Savage", "Extreme", "Raids", "Other" };

    private void DrawFightPicker()
    {
        var builtins = C.Fights.Where(f => Builtin.Has(f.TerritoryId)).ToList();
        if (builtins.Count <= 1) return;

        ImGui.SetNextItemWidth(230f);
        // Popup sized to the longest fight name plus the slot tag (so nothing
        // overlaps), and height-capped so a long list scrolls.
        var nameW = 0f;
        foreach (var f in builtins) nameW = MathF.Max(nameW, ImGui.CalcTextSize(f.Name).X);
        var popupW = nameW + 96f;
        ImGui.SetNextWindowSizeConstraints(new Vector2(popupW, 0f), new Vector2(popupW, 320f));
        if (ImGui.BeginCombo("##sheetfight", _fight!.Name))
        {
            var groups = builtins
                .GroupBy(f =>
                {
                    var c = string.IsNullOrEmpty(f.Category) ? Builtin.Category(f.TerritoryId) : f.Category;
                    return PickerCategories.Contains(c) ? c : "Other";
                })
                .OrderBy(g => Array.IndexOf(PickerCategories, g.Key));

            var firstGroup = true;
            foreach (var g in groups)
            {
                if (!firstGroup) { ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing(); }
                firstGroup = false;
                ImGui.TextDisabled(g.Key.ToUpperInvariant());
                foreach (var f in g)
                {
                    if (ImGui.Selectable(f.Name, f == _fight))
                    {
                        CommitPending();
                        _fight = f;
                        _phaseFilter = "";
                        _dirty = true;
                    }
                    // Your slot for that fight, right-aligned; fights without one
                    // land on the pick-your-slot screen when chosen. Clamped so it
                    // can never sit on top of a long fight name.
                    var tag = string.IsNullOrEmpty(f.Slot) ? "no slot" : f.Slot;
                    ImGui.SameLine(MathF.Max(
                        ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(tag).X - 6f,
                        ImGui.CalcTextSize(f.Name).X + 24f));
                    ImGui.TextDisabled(tag);
                }
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
    }

    private void DrawToolbar()
    {
        DrawFightPicker();

        // Phase filter: All + one button per phase (with row counts), like the
        // sheet's tabs.
        PhaseButton("All", null, _phaseFilter.Length == 0);
        foreach (var (name, _) in _phases)
        {
            ImGui.SameLine(0, 4);
            PhaseButton(name, _rows.Count(r => !r.Ghost && r.Phase == name), _phaseFilter == name);
        }

        // Text filter across mechanics and mits ("Reprisal" = every Reprisal row).
        ImGui.SameLine(0, 10);
        ImGui.SetNextItemWidth(140f);
        ImGui.InputTextWithHint("##sheetfilter", "filter...", ref _filter, 64);
        if (ImGui.IsItemHovered() && !ImGui.IsItemActive())
            ImGui.SetTooltip("Show only rows whose mechanic or any slot's mit contains this text.");
        if (_filter.Length > 0)
        {
            ImGui.SameLine(0, 2);
            if (ImGui.SmallButton("x##clearfilter")) _filter = "";
        }

        // Search-and-replace across mits ("all my Vengeance becomes Damnation").
        ImGui.SameLine(0, 4);
        if (ImGui.SmallButton("Replace..."))
        {
            _replFind = _filter;
            ImGui.OpenPopup("##sheetreplace");
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Rename a mit across the whole sheet in one go.");
        DrawReplacePopup();

        // Type coloring is opt-in: a full grid of colored text is a lot.
        ImGui.SameLine(0, 8);
        var colors = C.SheetColorByType;
        if (ImGui.Checkbox("Colors##sheetcolors", ref colors)) { C.SheetColorByType = colors; C.Save(); }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Color mits by type (party / tank / personal), using your overlay's mit colors.\nRed cooldown warnings show either way.");

        ImGui.SameLine(0, 8);
        var filtered = _phaseFilter.Length > 0 || _filter.Length > 0;
        var shown = _rows.Count(r => !r.Ghost
            && (_phaseFilter.Length == 0 || r.Phase == _phaseFilter) && MatchesFilter(r));
        ImGui.TextDisabled(filtered
            ? $"·  {shown} of {_rows.Count(r => !r.Ghost)} mechanics"
            : $"·  {_rows.Count(r => !r.Ghost)} mechanics, {_slots.Length} slots");

        // The how-to lives here now instead of a permanent footer line.
        ImGui.SameLine(0, 8);
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "Click a time to re-time that mechanic for every slot at once.\n"
                + "Click a cell to edit that slot only; clear the text to remove it.\n"
                + "Right-click a cell for delete / reset / a per-call offset; right-click a mechanic for notes.\n"
                + "Orange * = your edit, kept through sheet updates.\n"
                + "Dim rows are deleted; the undo button restores the sheet's version.\n"
                + "A red cell means that mit is planned again before its cooldown can be back.\n"
                + "Tick Colors to tint mits by type (party / tank / personal, overlay colors).\n"
                + "Your slot's column is pinned next to Mechanic.\n"
                + "Drag a column edge to resize it; double-click the edge to fit the text.");

        // Right side: refresh + export + import + share.
        var rightW = ImGui.CalcTextSize("Refresh").X + ImGui.CalcTextSize("Export").X
                   + ImGui.CalcTextSize("Import").X + ImGui.CalcTextSize("Share plan").X + 128f;
        ImGui.SameLine(MathF.Max(ImGui.GetCursorPosX() + 8f, ImGui.GetContentRegionMax().X - rightW));
        if (ImGui.SmallButton("Refresh")) { CommitPending(); _dirty = true; Flash("Reloaded from your saved plans."); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Re-read every slot (picks up edits made on the fight page).");
        ImGui.SameLine();
        // Land any half-typed edit and fold it into the rows first, so the
        // clipboard never captures a pre-edit grid.
        if (ImGui.SmallButton("Export"))
        {
            CommitPending();
            if (_dirty) Rebuild();
            ExportText();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Copy the whole grid as tab-separated text: paste straight into\nGoogle Sheets / Excel (lands in columns) or Discord.");
        ImGui.SameLine();
        if (ImGui.SmallButton("Import")) ImportPlan();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Paste a plan code from your clipboard (a friend's \"Share plan\").\n"
                + "Updates that fight in place: the sender's slot is replaced, your other slots are kept.");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        if (ImGui.SmallButton("Share plan")) SharePlan();
        ImGui.PopStyleColor(2);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Copy the whole plan as a clipboard code. Friends press Import here\n(or on the fight page); it updates their fight in place (their slot's plan).");
    }

    // Import a friend's plan code from the clipboard, then jump to the fight it
    // touched so the result is on screen immediately.
    private void ImportPlan()
    {
        CommitPending();
        var (fight, _, message) = _plugin.ImportPlanCode(ImGui.GetClipboardText());
        if (fight != null && Builtin.Has(fight.TerritoryId))
        {
            _fight = fight;
            _phaseFilter = "";
        }
        _dirty = true;
        Flash(message);
    }

    private void PhaseButton(string name, int? count, bool on)
    {
        if (on)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentText);
        }
        // ### keeps the button id stable while the row count in the label moves.
        var label = count.HasValue ? $"{name} ({count.Value})###ph{name}" : $"{name}###ph{name}";
        if (ImGui.SmallButton(label))
        {
            // Land any open editor BEFORE the filter hides its row, or the edit
            // state would linger unseen (blocking rebuilds) until a later click.
            CommitPending();
            _phaseFilter = name == "All" ? "" : name;
        }
        if (on) ImGui.PopStyleColor(3);
    }

    // ---- search & replace --------------------------------------------------

    private void DrawReplacePopup()
    {
        if (!ImGui.BeginPopup("##sheetreplace")) return;

        ImGui.TextDisabled("Replace a mit across the sheet");
        ImGui.SetNextItemWidth(230f);
        ImGui.InputTextWithHint("##rfind", "find (e.g. Vengeance)", ref _replFind, 64);
        ImGui.SetNextItemWidth(230f);
        ImGui.InputTextWithHint("##rwith", "replace with (e.g. Damnation)", ref _replWith, 64);
        ImGui.Checkbox("My column only", ref _replMineOnly);

        var find = _replFind.Trim();
        var with = _replWith.Trim();
        var lines = 0;
        var slots = 0;
        if (find.Length > 0)
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_replMineOnly && !IsActiveSlot(i)) continue;
                // Same "would it actually change" test the apply uses, so the
                // preview never promises edits an identity replace won't make.
                var n = _slotLines[i].Count(l => WouldReplace(l.Action, find, with) != null);
                if (n > 0) { lines += n; slots++; }
            }
        ImGui.TextDisabled(find.Length == 0 ? "type something to find"
            : lines == 0 ? "no matches"
            : $"will change {lines} line(s) across {slots} slot(s)");
        if (string.IsNullOrWhiteSpace(_replWith) && lines > 0)
            ImGui.TextDisabled("empty replacement = those calls are DELETED");

        ImGui.BeginDisabled(lines == 0);
        if (ImGui.Button("Replace", new Vector2(120, 0)))
        {
            ApplyReplace(find);
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    private void ApplyReplace(string find)
    {
        if (_fight == null || find.Length == 0 || AbortIfStale()) return;
        var with = _replWith.Trim();
        var changed = 0;
        var slotsTouched = 0;
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_replMineOnly && !IsActiveSlot(i)) continue;
            var touched = false;
            var remove = new List<MitLine>();
            foreach (var l in _slotLines[i])
            {
                if (WouldReplace(l.Action, find, with) is not { } replaced) continue;
                EnsureBacked(i);
                touched = true;
                changed++;
                if (replaced.Length == 0)
                {
                    // Replacing with nothing = delete the call, tombstoned like
                    // any other delete so sheet updates don't resurrect it.
                    if (!l.Custom)
                        _fight.DeletedCalls.Add(new DeletedCall
                        { Slot = _slots[i], Time = l.Time, Mechanic = l.Mechanic, Action = l.Action });
                    remove.Add(l);
                }
                else
                {
                    Builtin.PreserveEdit(_fight, _slots[i], l);
                    l.Action = replaced;
                }
            }
            foreach (var l in remove) _slotLines[i].Remove(l);
            if (touched) { Resort(i); slotsTouched++; }
        }

        if (changed == 0) { Flash($"No mits containing \"{find}\"."); return; }
        C.Save();
        _dirty = true;
        Flash(string.IsNullOrWhiteSpace(with)
            ? $"Deleted \"{find}\" from {changed} line(s) across {slotsTouched} slot(s)."
            : $"Replaced \"{find}\" in {changed} line(s) across {slotsTouched} slot(s). Kept through sheet updates.");
    }

    // The action text after a real replacement, or null when nothing would
    // change. Joins are only tidied when the raw replace changed something, so
    // an identity replace can't silently normalize (and Custom-flag) a line.
    private static string? WouldReplace(string action, string find, string with)
    {
        var raw = action.Replace(find, with, StringComparison.OrdinalIgnoreCase);
        if (raw == action) return null;
        var replaced = TidyJoins(raw);
        return replaced == action ? null : replaced;
    }

    // Re-join a "A + B + C" action string, dropping empty segments left behind
    // by a replacement and normalizing the separators.
    private static string TidyJoins(string s)
        => string.Join(" + ", s.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    // ---- export -------------------------------------------------------------

    // The whole grid as tab-separated text: pastes into Google Sheets / Excel
    // as real columns, and reads fine in Discord. Phase notes ride at the bottom.
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

    private static readonly string[] TankSlots = { "MT", "OT", "T1", "T2" };
    private static readonly string[] HealSlots = { "WHM", "AST", "SCH", "SGE", "H1", "H2" };

    private static Vector4 RoleColor(string slot)
        => TankSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? ImGuiColors.TankBlue
         : HealSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? ImGuiColors.HealerGreen
         : ImGuiColors.DPSRed;

    private static readonly Vector4 EditedColor = new(0.96f, 0.62f, 0.36f, 1f);
    private static readonly Vector4 NoteBlue = new(0.42f, 0.66f, 0.96f, 1f);
    private const uint YouCellBg = 0x2233AA33;   // faint green tint (ABGR)
    private const uint WarnCellBg = 0x483040E6;  // translucent red: cooldown conflict

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

    private bool _editorDrawn; // safety net: an open editor whose row got hidden

    private void DrawGrid()
    {
        _editorDrawn = false;
        // Below the grid: the sheet-notes panel plus one footer line (flash
        // message, or the hovered row's note).
        var footerH = ImGui.GetTextLineHeightWithSpacing() + 10f + NotesReserve();
        // Resizable: drag a column edge, or double-click it to auto-fit the
        // column to its content (the Google-sheets gesture).
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY
                  | ImGuiTableFlags.ScrollX | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Resizable;
        if (!ImGui.BeginTable("##sheetgrid", 2 + _slots.Length, flags, new Vector2(0, -footerH)))
            return;

        // Your column rides in the frozen area (pinned right after Mechanic).
        ImGui.TableSetupScrollFreeze(_order.Length > 0 && IsActiveSlot(_order[0]) ? 3 : 2, 1);
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
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(IsActiveSlot(i)
                    ? $"{SlotTip(_slots[i])}, your slot. These are the lines your overlay calls."
                    : SlotTip(_slots[i]));
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
                ImGui.TextDisabled(Builtin.PhaseTitle(_fight!.TerritoryId, row.Phase));
                for (var i = 0; i < _slots.Length; i++) ImGui.TableNextColumn();
            }

            if (_firstDrawnIdx < 0) _firstDrawnIdx = r;
            _rowIdxDrawing = r;

            ImGui.PushID(r);
            ImGui.TableNextRow();

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
        // _focusPending exempts an edit that STARTED this frame - its editor
        // legitimately hasn't rendered yet (it draws next frame).
        if (Editing && !_editorDrawn && !_focusPending) CommitPending();
    }

    // A quiet pill in the grid's top-right corner naming the phase you're
    // scrolled into, since the phase separator rows scroll away with the rows.
    // Hidden at the very top (the separator is on screen) and while filtering.
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
        dl.AddText(p0 + pad, ImGui.GetColorU32(ImGuiCol.TextDisabled), _stickyTitle);
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
                _hoverRow = row;
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
            _hoverRow = row;
            ImGui.SetTooltip("Right-click to add or edit this mechanic's note.");
        }
        // Right-click the mechanic name = note editor. The footer strip shows the
        // note for whatever row the mouse is on, zero clicks to read.
        if (ImGui.BeginPopupContextItem("##notectx"))
        {
            if (ImGui.IsWindowAppearing()) _noteBuf = NoteFor(row)?.Text ?? "";
            ImGui.TextDisabled($"Note: {row.Mechanic}");
            if (ImGui.InputTextMultiline("##notetxt", ref _noteBuf, 1000, new Vector2(360, 84)))
                SaveNote(row, _noteBuf);
            ImGui.TextDisabled("Saved as you type. Clear the text to remove the note.");
            ImGui.EndPopup();
        }
        if (NoteFor(row) != null)
        {
            ImGui.SameLine(0, 5);
            IconText(FontAwesomeIcon.PencilAlt, NoteBlue);
        }
        if (row.Edited)
        {
            ImGui.SameLine(0, 6);
            ImGui.TextColored(EditedColor, "edited");
            ImGui.SameLine(0, 4);
            if (IconSmallButton(FontAwesomeIcon.Undo, "##reset")) ResetRow(row);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Reset this mechanic to the baked sheet, every slot.");
        }
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
                if (ImGui.IsItemDeactivatedAfterEdit()) CommitCell(row, i);
                _editCellRow = null;
            }
            return;
        }

        var cell = row.Cells[i];
        var first = cell.Count == 0 ? "" : cell[0].Action;
        var custom = cell.Any(l => l.Custom);
        var off = cell.Count > 0 && cell.All(l => !l.Enabled);

        // Cooldown conflicts tint the cell red (details go in the tooltip).
        string? warn = null;
        foreach (var l in cell)
            if (_conflicts.TryGetValue(l, out var w))
                warn = warn == null ? w : warn + "\n" + w;
        if (warn != null) ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, WarnCellBg);

        // Merged cells stack their lines instead of hiding behind a "+1".
        var body = cell.Count > 1 ? string.Join("\n", cell.Select(l => l.Action)) : first;
        var label = (custom ? "* " : "") + (body.Length == 0 ? " " : body) + (off ? "  (off)" : "");

        // Text color: your edits stay orange, disabled lines dim, and with the
        // Colors box ticked the rest is colored by mit type (overlay colors).
        var kindCol = C.SheetColorByType && !custom && !off && first.Length > 0
            ? MitTypes.Color(MitTypes.Classify(first), C) : 0u;
        var pushed = true;
        if (custom) ImGui.PushStyleColor(ImGuiCol.Text, EditedColor);
        else if (off) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
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
            _hoverRow = row;
            var tip = cell.Count == 0
                ? $"Click to add a mit for {_slots[i]} here (that slot only)"
                : cell.Count == 1
                    ? $"{first}\nClick to edit {_slots[i]}'s mit (that slot only). Clear the text to remove it."
                    : $"{string.Join("  ·  ", cell.Select(l => l.Action))}\nTwo lines share this moment; "
                      + "editing changes the first one only. Fine-tune both on the fight page.";
            if (off) tip = "Disabled on the fight page (won't be called).\n" + tip;
            if (warn != null) tip = warn + "\n\n" + tip;
            ImGui.SetTooltip(tip);
        }

        // Right-click: quick actions + the per-call offset, sheet-side.
        if (ImGui.BeginPopupContextItem($"##cellctx{i}"))
        {
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
                    EnsureBacked(i);
                    line.OffsetSeconds = Math.Clamp(offset, -30f, 30f);
                    C.Save();
                }
                ImGui.TextDisabled("+ fires this one call earlier, - later.");
                ImGui.Separator();
                if (ImGui.MenuItem("Delete this mit")) DeleteCellLine(row, i);
            }
            if (ImGui.MenuItem("Reset this cell to the sheet")) ResetCell(row, i);
            ImGui.EndPopup();
        }
    }

    // Delete one slot's line at this row: tombstoned exactly like clearing the
    // cell's text, so sheet updates don't resurrect it.
    private void DeleteCellLine(Row row, int i)
    {
        if (_fight == null || row.Ghost || AbortIfStale()) return;
        var cell = row.Cells[i];
        if (cell.Count == 0) return;
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
        if (row.Bake == null)
        {
            Flash("Can't match this row to the sheet (renamed mechanic?). Use the fight page's Reset to sheet instead.");
            return;
        }
        var slot = _slots[i];
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

    private static string TimeText(float t)
    {
        var s = (int)MathF.Round(t);
        var sign = s < 0 ? "-" : "";
        s = Math.Abs(s);
        return $"{sign}{s / 60}:{s % 60:00}";
    }

    private static string SlotTip(string slot)
        => TankSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? "Tank slot"
         : HealSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? "Healer slot"
         : "DPS slot";

    // One quiet line: a flash message when something just happened, otherwise
    // the hovered row's note (Ikuya-footer style, sticky on the last hovered
    // row so it stays readable while the mouse travels down here). Empty rest
    // of the time; the how-to lives in the toolbar's (?) tooltip.
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
