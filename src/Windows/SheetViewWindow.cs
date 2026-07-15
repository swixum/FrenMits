using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
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

    public SheetViewWindow(Plugin plugin) : base("Fren Mits — Sheet View##fmsheet")
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
        _phases = Builtin.PhaseStarts(_fight.TerritoryId);
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
        Flash("The plan changed on the fight page — reloaded. Make the edit again.");
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
        Flash($"Shifted \"{row.Mechanic}\" by {delta:+0.0;-0.0}s — {lines} line(s) across {slots} slot(s). Kept through sheet updates.");
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

        EnsureBacked(i);
        if (text.Length == 0)
        {
            if (cell.Count == 0) return;
            // Clearing the cell = delete this slot's line (tombstoned like a
            // delete on the fight page, so it stays gone; ⟲ restores).
            var line = cell[0];
            if (!line.Custom)
                _fight.DeletedCalls.Add(new DeletedCall
                { Slot = _slots[i], Time = line.Time, Mechanic = line.Mechanic, Action = line.Action });
            _slotLines[i].Remove(line);
            Flash($"{_slots[i]}'s mit for \"{row.Mechanic}\" removed. ⟲ on the row brings the sheet's version back.");
        }
        else if (cell.Count == 0)
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
            Flash("Plan code copied — friends paste it into Import and their slot updates.");
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
            ImGui.TextWrapped("Pick your slot for this fight first (fight page → \"Your slot\"), then come back — "
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
        DrawFooter();
    }

    // Fight picker (only when there's a choice to make). Also shown on the
    // pick-your-slot screen so a slotless default is never a dead end.
    private void DrawFightPicker()
    {
        var builtins = C.Fights.Where(f => Builtin.Has(f.TerritoryId)).ToList();
        if (builtins.Count <= 1) return;
        ImGui.SetNextItemWidth(210f);
        if (ImGui.BeginCombo("##sheetfight", _fight!.Name))
        {
            foreach (var f in builtins)
                if (ImGui.Selectable("★ " + f.Name, f == _fight))
                {
                    CommitPending();
                    _fight = f;
                    _phaseFilter = "";
                    _dirty = true;
                }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
    }

    private void DrawToolbar()
    {
        DrawFightPicker();

        // Phase filter: All + one button per phase, like the sheet's tabs.
        PhaseButton("All", _phaseFilter.Length == 0);
        foreach (var (name, _) in _phases)
        {
            ImGui.SameLine(0, 4);
            PhaseButton(name, _phaseFilter == name);
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"·  {_rows.Count(r => !r.Ghost)} mechanics, {_slots.Length} slots");

        // Right side: refresh + share.
        var shareW = ImGui.CalcTextSize("Share plan").X + 60f + ImGui.CalcTextSize("Refresh").X + 24f;
        ImGui.SameLine(MathF.Max(ImGui.GetCursorPosX() + 8f, ImGui.GetContentRegionMax().X - shareW));
        if (ImGui.SmallButton("Refresh")) { CommitPending(); _dirty = true; Flash("Reloaded from your saved plans."); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Re-read every slot (picks up edits made on the fight page).");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        if (ImGui.SmallButton("Share plan")) SharePlan();
        ImGui.PopStyleColor(2);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Copy the whole plan as a clipboard code. Friends use Import from clipboard\non the fight page; it updates their fight in place (their slot's plan).");
    }

    private void PhaseButton(string label, bool on)
    {
        if (on)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentText);
        }
        if (ImGui.SmallButton(label))
        {
            // Land any open editor BEFORE the filter hides its row, or the edit
            // state would linger unseen (blocking rebuilds) until a later click.
            CommitPending();
            _phaseFilter = label == "All" ? "" : label;
        }
        if (on) ImGui.PopStyleColor(3);
    }

    private static readonly string[] TankSlots = { "MT", "OT", "T1", "T2" };
    private static readonly string[] HealSlots = { "WHM", "AST", "SCH", "SGE", "H1", "H2" };

    private static Vector4 RoleColor(string slot)
        => TankSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? ImGuiColors.TankBlue
         : HealSlots.Contains(slot, StringComparer.OrdinalIgnoreCase) ? ImGuiColors.HealerGreen
         : ImGuiColors.DPSRed;

    private static readonly Vector4 EditedColor = new(0.96f, 0.62f, 0.36f, 1f);
    private const uint YouCellBg = 0x2233AA33;   // faint green tint (ABGR)

    private bool _editorDrawn; // safety net: an open editor whose row got hidden

    private void DrawGrid()
    {
        _editorDrawn = false;
        // Two footer lines now: the note strip + the flash/legend line.
        var footerH = ImGui.GetTextLineHeightWithSpacing() * 2 + 12f;
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY
                  | ImGuiTableFlags.ScrollX | ImGuiTableFlags.SizingFixedFit;
        if (!ImGui.BeginTable("##sheetgrid", 2 + _slots.Length, flags, new Vector2(0, -footerH)))
            return;

        ImGui.TableSetupScrollFreeze(2, 1);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 62);
        ImGui.TableSetupColumn("Mechanic", ImGuiTableColumnFlags.WidthFixed, 240);
        foreach (var s in _slots)
            ImGui.TableSetupColumn(s, ImGuiTableColumnFlags.WidthFixed, 130);

        // Header row with role colors + a star on your active slot.
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableNextColumn(); ImGui.TableHeader("Time");
        ImGui.TableNextColumn(); ImGui.TableHeader("Mechanic");
        for (var i = 0; i < _slots.Length; i++)
        {
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, RoleColor(_slots[i]));
            ImGui.TableHeader(IsActiveSlot(i) ? $"{_slots[i]} ★" : _slots[i]);
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(IsActiveSlot(i)
                    ? $"{SlotTip(_slots[i])} — your slot. These are the lines your overlay calls."
                    : SlotTip(_slots[i]));
        }

        var lastPhase = "";
        for (var r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            if (_phaseFilter.Length > 0 && row.Phase != _phaseFilter) continue;

            if (row.Phase != lastPhase)
            {
                lastPhase = row.Phase;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, 0xFF221B17);
                ImGui.TableNextColumn();
                ImGui.TextDisabled($"—  {row.Phase}  —");
                for (var i = 0; i < _slots.Length; i++) ImGui.TableNextColumn();
            }

            ImGui.PushID(r);
            ImGui.TableNextRow();

            DrawTimeCell(row);
            DrawMechanicCell(row);
            for (var i = 0; i < _slots.Length; i++) DrawSlotCell(row, i);

            ImGui.PopID();
        }

        ImGui.EndTable();

        // An editor whose row was hidden this frame (filter change, rebuild race)
        // can never deactivate normally; land it now instead of leaving a zombie
        // edit that silently blocks rebuilds and commits minutes later.
        // _focusPending exempts an edit that STARTED this frame - its editor
        // legitimately hasn't rendered yet (it draws next frame).
        if (Editing && !_editorDrawn && !_focusPending) CommitPending();
    }

    private void DrawTimeCell(Row row)
    {
        ImGui.TableNextColumn();
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
            if (row.Edited) { ImGui.TextColored(EditedColor, "•"); ImGui.SameLine(0, 3); }
            if (ImGui.Selectable(TimeText(row.Time) + "##time", false) && !CommitPending())
            {
                _editTimeRow = row;
                _timeBuf = _timeSeed = row.Time.ToString("0.##", CultureInfo.InvariantCulture);
                _focusPending = true;
            }
            if (ImGui.IsItemHovered())
            {
                _hoverRow = row;
                ImGui.SetTooltip($"{row.Time:0.#}s — click to re-time \"{row.Mechanic}\" for EVERY slot at once");
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
            if (ImGui.SmallButton("⟲##reset")) ResetRow(row);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("This mechanic is deleted from your plan. ⟲ restores the sheet's version.");
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
            ImGui.TextDisabled($"Note — {row.Mechanic}");
            if (ImGui.InputTextMultiline("##notetxt", ref _noteBuf, 1000, new Vector2(360, 84)))
                SaveNote(row, _noteBuf);
            ImGui.TextDisabled("Saved as you type. Clear the text to remove the note.");
            ImGui.EndPopup();
        }
        if (NoteFor(row) != null)
        {
            ImGui.SameLine(0, 5);
            ImGui.TextColored(new Vector4(0.42f, 0.66f, 0.96f, 1f), "✎");
        }
        if (row.Edited)
        {
            ImGui.SameLine(0, 6);
            ImGui.TextColored(EditedColor, "edited");
            ImGui.SameLine(0, 4);
            if (ImGui.SmallButton("⟲##reset")) ResetRow(row);
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
        var extra = cell.Count > 1 ? $"  (+{cell.Count - 1})" : "";
        var custom = cell.Any(l => l.Custom);
        var label = (custom ? "• " : "") + (first.Length == 0 ? " " : first + extra);
        if (custom) ImGui.PushStyleColor(ImGuiCol.Text, EditedColor);
        var clicked = ImGui.Selectable($"{label}##c{i}", false);
        if (custom) ImGui.PopStyleColor();
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
            ImGui.SetTooltip(cell.Count == 0
                ? $"Click to add a mit for {_slots[i]} here (that slot only)"
                : cell.Count == 1
                    ? $"{first}\nClick to edit {_slots[i]}'s mit (that slot only). Clear the text to remove it."
                    : $"{string.Join("  ·  ", cell.Select(l => l.Action))}\nTwo lines share this moment; "
                      + "editing changes the first one only. Fine-tune both on the fight page.");
        }
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

    private void DrawFooter()
    {
        ImGui.Spacing();

        // Note strip: the hovered row's note, zero clicks to read (Ikuya-footer
        // style). Sticky on the last hovered row so it stays readable while the
        // mouse travels down here.
        var note = _hoverRow != null ? NoteFor(_hoverRow) : null;
        if (note != null)
        {
            ImGui.TextColored(new Vector4(0.42f, 0.66f, 0.96f, 1f), "✎");
            ImGui.SameLine(0, 6);
            ImGui.TextUnformatted($"{_hoverRow!.Mechanic}:");
            ImGui.SameLine(0, 6);
            var text = note.Text.Replace('\n', ' ');
            ImGui.TextDisabled(text.Length > 220 ? text[..220] + "…" : text);
            if (ImGui.IsItemHovered() && note.Text.Length > 220) ImGui.SetTooltip(note.Text);
        }
        else
        {
            ImGui.TextDisabled("Notes: right-click a mechanic to write one; hover a ✎ row and it shows here.");
        }

        if ((DateTime.Now - _flashAt).TotalSeconds < 4.5 && _flash.Length > 0)
        {
            ImGui.TextColored(ImGuiColors.ParsedGreen, _flash);
        }
        else
        {
            ImGui.TextDisabled("Click a time = re-time that mechanic for every slot  ·  click a cell = edit that slot only  ·  "
                + "• orange = your edit, kept through sheet updates  ·  dim rows = deleted (⟲ restores)");
        }
    }
}
