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
    // Column display order: pinned columns first (into the frozen area).
    private int[] _order = Array.Empty<int>();
    private int _pinnedCount;

    private bool IsPinnedColumn(int i)
        => C.SheetPinnedSlots.Contains(_slots[i], StringComparer.OrdinalIgnoreCase);

    // A user-made sheet: a non-builtin fight with its own column layout.
    private static bool IsCustomSheet(FightProfile f)
        => !Builtin.Has(f.TerritoryId) && f.CustomSlots.Count > 0;

    // Set from the current fight each Rebuild; custom sheets have no bake, so
    // ghosts, tombstone resets and the official notes panel don't apply.
    private bool _isCustom;
    // Lines whose mit repeats before its cooldown can be back (message per line).
    private readonly Dictionary<MitLine, string> _conflicts = new();
    // Lines whose mit is above the duty's level sync (message per line).
    private readonly Dictionary<MitLine, string> _levelWarns = new();
    // Valid press windows ("press between X and Y"), from coverage + squeeze.
    private readonly Dictionary<MitLine, string> _windows = new();
    // Text filter: show only rows whose mechanic or any mit matches.
    private string _filter = "";

    // Search-and-replace popup state.
    private string _replFind = "";
    private string _replWith = "";
    private bool _replMineOnly;

    // ---- undo (Ctrl+Z) -----------------------------------------------------
    // Snapshot-based: every sheet edit pushes a deep copy of the fight's plan
    // BEFORE mutating; undo swaps it back. In-memory only, capped.

    private sealed class PlanSnapshot
    {
        public FightProfile Fight = null!;
        public string Label = "";
        public List<MitLine> Lines = new();
        public Dictionary<string, List<MitLine>> SavedSlots = new();
        public List<DeletedCall> DeletedCalls = new();
        public List<SheetNote> Notes = new();
        public List<CustomRow> CustomRows = new();
        public List<string> CustomSlots = new();
        public List<SyncPoint> SyncPoints = new();
        public List<BossAnchor> BossAnchors = new();
        public string Slot = "";
        public float TimerOffset;
    }

    private readonly List<PlanSnapshot> _undoStack = new();
    private bool _noteUndoArmed;   // one undo entry per note-popup session
    private bool _offsetUndoArmed; // one undo entry per cell-menu session

    private static T Clone<T>(T value)
        => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Newtonsoft.Json.JsonConvert.SerializeObject(value))!;

    private void PushUndo(string label)
    {
        if (_fight == null) return;
        _undoStack.Add(new PlanSnapshot
        {
            Fight = _fight,
            Label = label,
            Lines = Clone(_fight.Lines),
            SavedSlots = Clone(_fight.SavedSlots),
            DeletedCalls = Clone(_fight.DeletedCalls),
            Notes = Clone(_fight.Notes),
            CustomRows = Clone(_fight.CustomRows),
            CustomSlots = Clone(_fight.CustomSlots),
            SyncPoints = Clone(_fight.SyncPoints),
            BossAnchors = Clone(_fight.BossAnchors),
            Slot = _fight.Slot,
            TimerOffset = _fight.TimerOffset,
        });
        if (_undoStack.Count > 20) _undoStack.RemoveAt(0);
    }

    private void PopUndo() // for ops that turn out to be no-ops after pushing
    {
        if (_undoStack.Count > 0) _undoStack.RemoveAt(_undoStack.Count - 1);
    }

    private void Undo()
    {
        CommitPending();
        if (_undoStack.Count == 0) { Flash("Nothing to undo."); return; }
        var s = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        if (!C.Fights.Contains(s.Fight)) { Flash("Can't undo: that fight no longer exists."); return; }

        // Jumping to another fight's undo entry: reset the view filters (they
        // may not exist there) and keep a disk snapshot as insurance, since the
        // user may not have that fight's current state in their head.
        var jumped = s.Fight != _fight;
        if (jumped)
        {
            _plugin.SnapshotPlan(s.Fight, "before undo");
            _phaseFilter = "";
            _filter = "";
        }

        s.Fight.Lines = s.Lines;
        s.Fight.SavedSlots = s.SavedSlots;
        s.Fight.DeletedCalls = s.DeletedCalls;
        s.Fight.Notes = s.Notes;
        s.Fight.CustomRows = s.CustomRows;
        s.Fight.CustomSlots = s.CustomSlots;
        s.Fight.SyncPoints = s.SyncPoints;
        s.Fight.BossAnchors = s.BossAnchors;
        s.Fight.Slot = s.Slot;
        s.Fight.TimerOffset = s.TimerOffset;
        // Restore the active-slot alias (Lines IS SavedSlots[slot] normally).
        if (!string.IsNullOrEmpty(s.Slot) && s.Fight.SavedSlots.ContainsKey(s.Slot))
            s.Fight.SavedSlots[s.Slot] = s.Fight.Lines;

        _fight = s.Fight;
        C.Save();
        _dirty = true;
        Flash(jumped ? $"Undid: {s.Label} (in {s.Fight.Name})." : $"Undid: {s.Label}.");
    }

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
        public bool JobExtra;       // every line is a job-restricted custom (e.g. Nature's Minne)
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

    // Spreadsheet keyboard flow: Enter commits and edits the cell below, Tab
    // the next column. Held as coordinates, not references, because the commit
    // rebuilds every row object before the next edit can start.
    private (float Time, string Mech, int Slot)? _pendingEdit;

    private string _flash = "";
    private DateTime _flashAt;
    private void Flash(string msg) { _flash = msg; _flashAt = DateTime.Now; }

    // Notes: the row the mouse is on (its note shows in the footer strip) and
    // the edit buffer for the right-click note popup. _hoverRow is sticky (the
    // footer keeps the last note readable); _hoverLive is only the row the
    // mouse is on THIS frame, driving the row highlight.
    private Row? _hoverRow;
    private Row? _hoverLive;
    private Row? _hoverLivePrev;
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
        if (_noteUndoArmed) { PushUndo($"edit \"{row.Mechanic}\" note"); _noteUndoArmed = false; }
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
        _pendingEdit = null;
        _dirty = true;
        IsOpen = true;
        BringToFront(); // safe outside a draw frame, unlike ImGui.SetWindowFocus
    }

    private static bool Sheetable(FightProfile f) => Builtin.Has(f.TerritoryId) || IsCustomSheet(f);

    private FightProfile? PickDefaultFight()
    {
        var terr = Service.ClientState.TerritoryType;
        // Prefer fights that already have a slot picked: the grid needs one.
        return C.Fights.FirstOrDefault(f => Sheetable(f) && f.TerritoryId == terr && f.Enabled)
            ?? C.Fights.FirstOrDefault(f => Sheetable(f) && f.Id == C.LastSheetFightId)
            ?? C.Fights.FirstOrDefault(f => f.TerritoryId == Builtin.DmuTerritory && !string.IsNullOrEmpty(f.Slot))
            ?? C.Fights.FirstOrDefault(f => Sheetable(f) && !string.IsNullOrEmpty(f.Slot))
            ?? C.Fights.FirstOrDefault(f => f.TerritoryId == Builtin.DmuTerritory)
            ?? C.Fights.FirstOrDefault(Sheetable);
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
        if (_fight == null || !Sheetable(_fight)) return;

        _isCustom = IsCustomSheet(_fight);
        _slots = _isCustom ? _fight.CustomSlots.ToArray() : Builtin.Slots(_fight.TerritoryId);
        // Pinned columns (right-click a header) ride first, inside the frozen
        // area. Stable sort keeps the sheet's slot order within each group.
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
                // Job-restricted customs (the Job extras schedules) don't count
                // as "edited": they get their own "job extra" tag instead.
                row.Edited |= line.Custom && line.Jobs.Count == 0;
            }
        }

        // The same grid straight from the bake (unfiltered): reset anchors,
        // deleted-detection, and ghost rows all come from here. Custom sheets
        // have no bake at all (Builtin.BuildLines would leak DMU's defaults for
        // any slot code that happens to match, so don't even ask).
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

        // Custom sheets: scaffold rows exist even before any lines are written
        // into them, so Build > Add row gives you a plannable grid immediately.
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

            // On a custom sheet everything is yours: no "edited" state exists.
            if (_isCustom) r.Edited = false;

            // A row made ENTIRELY of job-restricted custom lines is a job extra
            // (e.g. Nature's Minne riding 1s off its mechanic's row). Tagged so
            // it doesn't read as a mysterious duplicate.
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
    }

    // Flag any line whose mit is used again before its cooldown (with charges
    // honored) can possibly be back, per slot. Uses plan times, not the live
    // recast, so it works while planning at the aetheryte.
    private void FindCooldownConflicts()
    {
        _conflicts.Clear();
        _levelWarns.Clear();
        _windows.Clear();
        var syncLevel = _fight != null ? Cooldowns.DutySyncLevel(_fight.TerritoryId) : 0;

        for (var i = 0; i < _slots.Length; i++)
        {
            // Abilities in the same recast GROUP share one timer (Bloodwhetting /
            // Nascent Flash / Raw Intuition), so group-mates pool their uses.
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
                    // CUE time, not plan time: a per-call offset genuinely moves
                    // the press, so it must count in the timer math. The job tag
                    // rides along: differently-tagged variants ("Party Mit
                    // (WAR/PLD)" vs "(GNB/DRK)", or a WAR's vs a DRK's tank
                    // lines) are different players' presses, not one timer.
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
                // (GNB/DRK)" and "(WAR/PLD)" never share one timer. Run the
                // check once per tag, each time with that tag's uses plus the
                // untagged ones (those apply to any job, so they chain with all).
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

    // One mit timer's worth of uses (same recast group + compatible job tags):
    // press-window computation and the serial-recharge conflict check.
    private void CheckMitTimer(List<(float Time, MitLine Line, string Name, string Tag)> list, float recast, int charges)
    {
                // Press windows: coverage pushes the press EARLIER (the buff
                // must reach the last covered hit), a same-timer reuse caps how
                // LATE it can go (squeeze). Charges make squeeze moot.
                for (var u = 0; u < list.Count; u++)
                {
                    var (t, line, name, _) = list[u];
                    var pm = Cooldowns.PlanMits(line.Action).FirstOrDefault(m =>
                        string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                    var lo = float.NegativeInfinity;
                    if (line.CoverUntil > t + 0.5f && pm.Duration > 0f)
                        lo = line.CoverUntil - pm.Duration;

                    var hi = t;
                    var squeezedBy = "";
                    if (charges == 1 && u + 1 < list.Count)
                    {
                        var next = list[u + 1];
                        var latest = next.Time - recast;
                        if (latest < hi) { hi = latest; squeezedBy = $"{next.Name} at {TimeText(next.Time)}"; }
                    }

                    if (lo > hi + 0.5f)
                    {
                        var msg = line.CoverUntil > t + 0.5f && pm.Duration > 0f && lo > t
                            ? $"{name}'s {pm.Duration:0}s duration can't reach {TimeText(line.CoverUntil)}; press it later or shorten the coverage."
                            : $"{name} can't cover through {TimeText(line.CoverUntil)} AND be back for {squeezedBy}.";
                        AppendOnce(_conflicts, line, msg);
                    }
                    else if ((lo > float.NegativeInfinity || hi < t - 0.5f) && hi >= 0f)
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

                // Serial recharge, like the game: charges regenerate one at a
                // time, so Oblation @0 and @5 is back at 60 and 120, not 60/65.
                var max = charges;
                var avail = max;
                var nextAt = float.PositiveInfinity; // when a charge next finishes
                var prevName = "";
                foreach (var (t, line, name, _) in list)
                {
                    // Regenerate charges finished by now (1s resync tolerance).
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
                        // Spell out the offset-aware press time, so it's obvious
                        // the math counted (or didn't get) a per-call offset.
                        var offNote = line.OffsetSeconds != 0f
                            ? $" (this call presses at {TimeText(t)}, offset {line.OffsetSeconds:+0.#;-0.#}s counted)"
                            : "";
                        var msg = $"{name}: not back for another {nextAt - t:0}s here "
                                + $"({recast:0}s cooldown, pressed at {TimeText(nextAt - recast)}"
                                + (max > 1 ? $", {max} charges)" : ")") + shared + "." + offNote;
                        AppendOnce(_conflicts, line, msg);
                        // The plan presumably slips to use the charge the moment
                        // it lands, so its recharge slot is consumed.
                        nextAt += recast;
                    }
                    else
                    {
                        if (avail == max) nextAt = t + recast; // pipeline starts
                        avail--;
                    }
                    prevName = name;
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

    // Enter = the visible row below (same column); Tab = the next column (same
    // row). Coordinates are captured now and resolved after the rebuild.
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
    // second line alone. Shared by inline editing and the right-click paste.
    private void ApplyCellText(Row row, int i, string raw)
    {
        if (_fight == null || row.Ghost || AbortIfStale()) return;
        var text = raw.Trim();
        var cell = row.Cells[i];

        if (cell.Count > 0 && text == cell[0].Action.Trim()) return; // no-op

        // Clearing the cell = delete this slot's line (tombstoned like a delete
        // on the fight page, so it stays gone; the undo button restores).
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
            // sheet renamed. The sheet-true state is "nothing here", so reset
            // clears the row's lines. Undo brings them back.
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
            ImGui.TextDisabled("No fight to show yet.");
            ImGui.Spacing();
            if (ImGui.Button("New sheet...")) OpenNewSheetPopup();
            DrawNewSheetPopup();
            return;
        }
        if (string.IsNullOrEmpty(_fight.Slot))
        {
            DrawFightPicker(); // still allow switching to a fight that HAS a slot
            ImGui.Spacing();
            if (IsCustomSheet(_fight))
            {
                // Custom sheets pick their column right here: no fight-page trip.
                ImGui.TextWrapped("Pick your column for this sheet; that column becomes the plan your overlay calls.");
                ImGui.Spacing();
                foreach (var s in _fight.CustomSlots)
                {
                    if (ImGui.Button(s)) PickCustomSlot(s);
                    ImGui.SameLine(0, 6);
                }
                ImGui.NewLine();
            }
            else
            {
                ImGui.TextWrapped("Pick your slot for this fight first (fight page, \"Your slot\"), then come back; "
                    + "the sheet needs to know which column is yours.");
            }
            return;
        }

        // The sheet reopens where you left off, across sessions.
        if (_fight.Id != C.LastSheetFightId) { C.LastSheetFightId = _fight.Id; C.Save(); }

        // Regaining focus re-reads every slot, so edits made on the fight page
        // while this window sat in the background always show up. Never rebuild
        // mid-edit: commits set _dirty and it lands right after.
        var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (focused && !_wasFocused) _dirty = true;
        _wasFocused = focused;
        if (_dirty && !Editing) Rebuild();

        // A queued Enter/Tab edit lands once the grid is rebuilt and idle.
        if (_pendingEdit is { } pe && !Editing && !_dirty)
        {
            _pendingEdit = null;
            var target = _rows.FirstOrDefault(r => !r.Ghost
                && MechEquals(r.Mechanic, pe.Mech) && MathF.Abs(r.Time - pe.Time) < 0.9f
                // Must be VISIBLE: an editor on a filtered-out row never draws,
                // which would wedge the edit state machine.
                && (_phaseFilter.Length == 0 || r.Phase == _phaseFilter) && MatchesFilter(r));
            if (target != null && pe.Slot >= 0 && pe.Slot < _slots.Length)
            {
                _editCellRow = target;
                _editCellSlot = pe.Slot;
                _cellBuf = _cellSeed = target.Cells[pe.Slot].Count > 0 ? target.Cells[pe.Slot][0].Action : "";
                _focusPending = true;
            }
        }

        // Ctrl+Z undoes the last sheet edit. Skipped while a text field is
        // active: InputText has its own internal Ctrl+Z for the typed buffer.
        if (focused && !ImGui.GetIO().WantTextInput
            && ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Z, false))
            Undo();

        DrawToolbar();
        ImGui.Spacing();
        DrawGrid();
        DrawNotesPanel();
        DrawFooter();
    }

    // ---- sheet notes (the per-phase "Notes" footer from the sheet's tabs) ----

    private float NotesBodyHeight() => Math.Clamp(C.SheetNotesHeight, 60f, 600f);
    private const float NotesGripHeight = 6f;

    // Vertical space the notes panel takes below the grid, so the table can
    // shrink to make room (header row + the body when expanded).
    private float NotesReserve()
    {
        if (_phaseNotes.Count == 0) return 0f;
        var h = ImGui.GetFrameHeightWithSpacing();
        if (C.SheetNotesOpen)
            h += NotesBodyHeight() + NotesGripHeight + ImGui.GetStyle().ItemSpacing.Y * 2f;
        return h;
    }

    private void DrawNotesPanel()
    {
        if (_fight == null || _phaseNotes.Count == 0) return;

        // Drag handle on the panel's top edge: pull it up for more notes, down
        // for more grid. The height is remembered.
        if (C.SheetNotesOpen)
        {
            ImGui.InvisibleButton("##notesgrip", new Vector2(-1, NotesGripHeight));
            var gMin = ImGui.GetItemRectMin();
            var gMax = ImGui.GetItemRectMax();
            var hot = ImGui.IsItemHovered() || ImGui.IsItemActive();
            var midY = (gMin.Y + gMax.Y) * 0.5f;
            ImGui.GetWindowDrawList().AddLine(
                new Vector2(gMin.X + 4f, midY), new Vector2(gMax.X - 4f, midY),
                hot ? Theme.Accent : 0x30FFFFFF, hot ? 3f : 2f);
            if (hot) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
            if (ImGui.IsItemActive())
                C.SheetNotesHeight = Math.Clamp(NotesBodyHeight() - ImGui.GetIO().MouseDelta.Y, 60f, 600f);
            if (ImGui.IsItemDeactivated()) C.Save();
        }

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
        var fights = C.Fights.Where(Sheetable).ToList();
        if (fights.Count == 0) return;

        ImGui.SetNextItemWidth(230f);
        // Popup sized to the longest fight name plus the slot tag (so nothing
        // overlaps), and height-capped so a long list scrolls.
        var nameW = ImGui.CalcTextSize("+ New sheet...").X;
        foreach (var f in fights) nameW = MathF.Max(nameW, ImGui.CalcTextSize(f.Name).X);
        var popupW = nameW + 96f;
        ImGui.SetNextWindowSizeConstraints(new Vector2(popupW, 0f), new Vector2(popupW, 340f));
        var openNew = false;
        if (ImGui.BeginCombo("##sheetfight", _fight!.Name))
        {
            var groups = fights
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
                        _pendingEdit = null;
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

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            // OpenPopup can't run inside the combo (wrong ID scope); flag it.
            if (ImGui.Selectable("+ New sheet...")) openNew = true;
            ImGui.EndCombo();
        }
        if (openNew) OpenNewSheetPopup();
        DrawNewSheetPopup();
        ImGui.SameLine();
    }

    // ---- new custom sheet ---------------------------------------------------

    private string _newName = "";
    private int _newTemplate;
    private string _newSlotsBuf = "";
    private int _newMySlot;

    private static readonly string[] SlotTemplates =
    {
        "Full party (MT OT H1 H2 D1-D4)",
        "Full party, job healers (MT OT WHM AST SCH SGE D1-D4)",
        "Light party (T H D1 D2)",
        "Custom columns",
    };

    private string[] TemplateSlots() => _newTemplate switch
    {
        0 => new[] { "MT", "OT", "H1", "H2", "D1", "D2", "D3", "D4" },
        // The official sheets' layout: healer columns are job columns, which
        // also lets Auto-plan use each healer's real kit.
        1 => new[] { "MT", "OT", "WHM", "AST", "SCH", "SGE", "D1", "D2", "D3", "D4" },
        2 => new[] { "T", "H", "D1", "D2" },
        _ => _newSlotsBuf.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                         .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
    };

    private void OpenNewSheetPopup()
    {
        _newName = "";
        _newTemplate = 0;
        _newSlotsBuf = "";
        _newMySlot = 0;
        _newCat = 4; // "Other" until the duty name suggests better
        _newCatTouched = false;
        // Prefill with the zone you're standing in; editable, so a sheet can be
        // made for any duty from anywhere.
        var here = (uint)Service.ClientState.TerritoryType;
        _newZoneBuf = here != 0 ? here.ToString() : "";
        ImGui.OpenPopup("##newsheet");
    }

    private string _newZoneBuf = "";
    private int _newCat = 4;
    private bool _newCatTouched;

    // Where the sheet files in the sidebar. Same list the fight picker groups by.
    private static readonly string[] NewSheetCategories = { "Ultimate", "Savage", "Extreme", "Raids", "Other" };

    // Best guess from the duty's name; the user's own pick always wins.
    private static int GuessCategory(string dutyName)
    {
        if (dutyName.Contains("(Ultimate)", StringComparison.OrdinalIgnoreCase)) return 0;
        if (dutyName.Contains("(Savage)", StringComparison.OrdinalIgnoreCase)) return 1;
        if (dutyName.Contains("(Extreme)", StringComparison.OrdinalIgnoreCase)
            || dutyName.StartsWith("The Minstrel's Ballad", StringComparison.OrdinalIgnoreCase)) return 2;
        return 4;
    }

    // True when the id is a real TerritoryType row (a typo'd id would bind the
    // sheet to a zone that can never fire).
    private static bool ZoneExists(uint terr)
    {
        try { return Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.HasRow(terr) == true; }
        catch { return false; }
    }

    // Friendly label for a zone id: the duty's name when it is a duty, else the
    // map's place name, else "".
    private static string ZoneLabel(uint terr)
    {
        try
        {
            var tt = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRowOrDefault(terr);
            if (tt == null) return "";
            var duty = tt.Value.ContentFinderCondition.ValueNullable?.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(duty)) return duty!;
            return tt.Value.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
        }
        catch { return ""; }
    }

    // Duties whose name contains the query, as (zone id, duty name).
    private static List<(uint Terr, string Name)> SearchDuties(string query, int max)
    {
        var found = new List<(uint, string)>();
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ContentFinderCondition>();
            if (sheet != null)
                foreach (var row in sheet)
                {
                    var name = row.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (!name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                    var terr = row.TerritoryType.RowId;
                    if (terr == 0) continue;
                    found.Add((terr, name));
                    if (found.Count >= max) break;
                }
        }
        catch { /* sheet hiccup: search just returns nothing */ }
        return found;
    }


    // Duties whose boss has this id (a BNpcBase DataId, the id the game exposes
    // on the boss object), as (zone id, duty name). Lets a sheet be bound by
    // boss id when the zone id is the hard thing to know.
    private static List<(uint Terr, string Name)> BossDuties(uint bossId)
    {
        var found = new List<(uint, string)>();
        try
        {
            var cfcs = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ContentFinderCondition>();
            var ics = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.InstanceContent>();
            if (cfcs != null && ics != null)
                foreach (var row in cfcs)
                {
                    if (row.ContentLinkType != 1) continue; // 1 = InstanceContent
                    var ic = ics.GetRowOrDefault(row.Content.RowId);
                    if (ic == null || ic.Value.BNpcBaseBoss.RowId != bossId) continue;
                    var terr = row.TerritoryType.RowId;
                    var name = row.Name.ExtractText();
                    if (terr == 0 || string.IsNullOrWhiteSpace(name)) continue;
                    found.Add((terr, name));
                }
        }
        catch { /* sheet hiccup: lookup just returns nothing */ }
        return found;
    }

    private void DrawNewSheetPopup()
    {
        // Modal so a stray click outside cannot dismiss the form; the X,
        // Escape, or its own buttons close it.
        var stay = true;
        if (!ImGui.BeginPopupModal("##newsheet", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;

        PopupHeader("New custom sheet", 380f);
        ImGui.SetNextItemWidth(250f);
        ImGui.InputTextWithHint("##nsname", "sheet name (usually the fight)", ref _newName, 64);
        ImGui.SetNextItemWidth(250f);
        ImGui.Combo("##nstpl", ref _newTemplate, SlotTemplates, SlotTemplates.Length);
        if (_newTemplate == 3)
        {
            ImGui.SetNextItemWidth(250f);
            ImGui.InputTextWithHint("##nscols", "columns, comma-separated (e.g. MT,OT,H1,H2)", ref _newSlotsBuf, 128);
            ImGui.TextDisabled("Tip: name a column after a job (WHM, MCH...) and Auto-plan");
            ImGui.TextDisabled("uses that job's real mitigation kit for it.");
        }
        var slots = TemplateSlots();
        if (slots.Length > 0)
        {
            _newMySlot = Math.Clamp(_newMySlot, 0, slots.Length - 1);
            ImGui.SetNextItemWidth(250f);
            ImGui.Combo("your column##nsmine", ref _newMySlot, slots, slots.Length);
        }
        ImGui.SetNextItemWidth(250f);
        if (ImGui.Combo("fight type##nscat", ref _newCat, NewSheetCategories, NewSheetCategories.Length))
            _newCatTouched = true;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Ultimate / Savage / Extreme / Raids / Other: which sidebar group the\nsheet files under. Auto-guessed from the duty name; your pick wins.");

        // The zone the sheet binds to: prefilled with where you stand, or type a
        // zone id, or type a duty name and pick it from the matches.
        ImGui.SetNextItemWidth(250f);
        ImGui.InputTextWithHint("zone##nszone", "zone id, boss id, or duty name", ref _newZoneBuf, 64);

        var buf = _newZoneBuf.Trim();
        uint terr = 0;
        var zoneBlocked = false;
        if (buf.Length > 0 && !uint.TryParse(buf, out terr))
        {
            // Name search: picking a match drops its zone id into the field.
            // The list scrolls in a fixed box so a broad search ("savage") can
            // never grow the popup past the screen.
            var matches = SearchDuties(buf, 40);
            if (matches.Count == 0)
                ImGui.TextDisabled("no duty matches that name");
            else
            {
                var h = MathF.Min(150f, matches.Count * ImGui.GetTextLineHeightWithSpacing() + 10f);
                if (ImGui.BeginChild("##nszlist", new Vector2(356f, h), true))
                    foreach (var (t, name) in matches)
                        if (ImGui.Selectable($"{name}  ({t})##nsz{t}", false, ImGuiSelectableFlags.DontClosePopups))
                            _newZoneBuf = t.ToString();
                ImGui.EndChild();
            }
            zoneBlocked = true; // until a match is picked or an id typed
        }
        else if (terr == 0)
        {
            // A zone-less sheet can never fire (and re-imports of its code would
            // stack duplicates, since imports match by territory).
            ImGui.TextColored(ImGuiColors.DalamudYellow,
                "You're not in a duty. Type the duty's name or zone id above.");
            zoneBlocked = true;
        }
        else if (!ZoneExists(terr))
        {
            // Not a zone: maybe it is a boss id. Picking a hit fills the zone in.
            var byBoss = BossDuties(terr);
            if (byBoss.Count > 0)
            {
                ImGui.TextDisabled("that boss id belongs to:");
                var h = MathF.Min(150f, byBoss.Count * ImGui.GetTextLineHeightWithSpacing() + 10f);
                if (ImGui.BeginChild("##nsblist", new Vector2(356f, h), true))
                    foreach (var (t, name) in byBoss)
                        if (ImGui.Selectable($"{name}  ({t})##nsb{t}", false, ImGuiSelectableFlags.DontClosePopups))
                            _newZoneBuf = t.ToString();
                ImGui.EndChild();
            }
            else
                ImGui.TextColored(ImGuiColors.DalamudYellow, $"{terr} is not a zone id or boss id.");
            zoneBlocked = true;
        }
        else if (Builtin.Has(terr))
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow,
                "That zone already has an official sheet; edit that one instead.");
            zoneBlocked = true;
        }
        else if (C.Fights.FirstOrDefault(f => f.TerritoryId == terr) is { } already)
        {
            ImGui.TextDisabled($"\"{already.Name}\" already covers that zone: Create adds these");
            ImGui.TextDisabled("columns to it (its current lines become your column).");
        }
        else
        {
            var label = ZoneLabel(terr);
            var here = terr == (uint)Service.ClientState.TerritoryType ? " - you're here" : "";
            ImGui.TextDisabled($"Binds to {(label.Length > 0 ? label : $"zone {terr}")}{here}; the calls fire there.");
            if (!_newCatTouched) _newCat = GuessCategory(label);
        }

        var ok = !zoneBlocked && _newName.Trim().Length > 0 && slots.Length is > 0 and <= 12;
        ImGui.BeginDisabled(!ok);
        if (ImGui.Button("Create", new Vector2(110, 0)))
        {
            CreateCustomSheet(_newName.Trim(), slots, slots[_newMySlot], terr, NewSheetCategories[_newCat]);
            _openAutoPlan = true; // offer the mit auto-planner right away
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    private void CreateCustomSheet(string name, string[] slots, string mySlot, uint terr, string category)
    {
        // A fight for this zone already exists: UPGRADE it into a sheet instead
        // of adding a duplicate profile (ActiveFight and imports take the first
        // territory match, so a duplicate would never fire).
        var existing = C.Fights.FirstOrDefault(f => f.TerritoryId == terr && !Builtin.Has(f.TerritoryId));
        if (existing != null)
        {
            existing.CustomSlots = slots.ToList();
            existing.Category = category;
            if (string.IsNullOrEmpty(existing.Slot)
                || !slots.Contains(existing.Slot, StringComparer.OrdinalIgnoreCase))
                existing.Slot = mySlot;
            existing.SavedSlots[existing.Slot] = existing.Lines;
            C.Save();
            _fight = existing;
            _phaseFilter = "";
            _filter = "";
            _dirty = true;
            Flash($"\"{existing.Name}\" is a sheet now; its existing lines are the {existing.Slot} column.");
            return;
        }

        var f = new FightProfile
        {
            Name = name,
            TerritoryId = terr,
            Category = category,
            CustomSlots = slots.ToList(),
            Slot = mySlot,
        };
        f.SavedSlots[mySlot] = f.Lines;
        C.Fights.Add(f);
        C.Save();
        _fight = f;
        _phaseFilter = "";
        _filter = "";
        _dirty = true;
        Flash($"\"{name}\" created. Build > Add row adds mechanics; click cells to write mits; Share plan sends it to friends.");
    }

    // ---- auto-plan mits (custom sheets) -------------------------------------

    private bool _openAutoPlan;
    private int _autoPlanPerRow = 2;
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
    // column. Your own seat becomes your own job (lines and active column
    // follow); leftover jobs slot in right after. Runs inside Auto-plan's undo.
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

    // Each job's CORE party-wide mitigation for auto-planning, mirroring what
    // the reference sheets put in their main columns: personal mits live in the
    // tank tabs, and extras-card abilities (Dismantle, Magick Barrier, Nature's
    // Minne, ...) stay optional extras. Recasts here are fallbacks; the game's
    // own numbers (Cooldowns.PlanInfo) win when available.
    private static readonly Dictionary<string, (string Name, float Recast)[]> JobPartyKit =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["WAR"] = new[] { ("Reprisal", 60f), ("Shake It Off", 90f) },
            ["PLD"] = new[] { ("Reprisal", 60f), ("Divine Veil", 90f) },
            ["DRK"] = new[] { ("Reprisal", 60f), ("Dark Missionary", 90f) },
            ["GNB"] = new[] { ("Reprisal", 60f), ("Heart of Light", 90f) },
            ["WHM"] = new[] { ("Temperance", 120f), ("Liturgy of the Bell", 180f), ("Asylum", 90f), ("Plenary Indulgence", 60f) },
            ["SCH"] = new[] { ("Expedient", 120f), ("Seraph", 120f), ("Fey Illumination", 120f), ("Whispering Dawn", 60f), ("Sacred Soil", 30f) },
            ["AST"] = new[] { ("Neutral Sect", 120f), ("Macrocosmos", 180f), ("Collective Unconscious", 60f) },
            ["SGE"] = new[] { ("Holos", 120f), ("Panhaima", 120f), ("Philosophia", 180f), ("Physis II", 60f), ("Kerachole", 30f) },
            ["MNK"] = new[] { ("Feint", 90f) }, ["DRG"] = new[] { ("Feint", 90f) },
            ["NIN"] = new[] { ("Feint", 90f) }, ["SAM"] = new[] { ("Feint", 90f) },
            ["RPR"] = new[] { ("Feint", 90f) }, ["VPR"] = new[] { ("Feint", 90f) },
            ["BRD"] = new[] { ("Troubadour", 90f) },
            ["MCH"] = new[] { ("Tactician", 90f) },
            ["DNC"] = new[] { ("Shield Samba", 90f) },
            ["BLM"] = new[] { ("Addle", 90f) }, ["SMN"] = new[] { ("Addle", 90f) },
            ["RDM"] = new[] { ("Addle", 90f) }, ["PCT"] = new[] { ("Addle", 90f) },
        };

    // A column's toolset. A column NAMED for a job (WHM, SGE, MCH...) plans
    // with that job's real party kit; a role column (MT, H1, D3...) gets the
    // generic terms that resolve per job at call time.
    private static (string Term, float Recast)[] PoolFor(string slot)
    {
        var t = slot.Trim().ToUpperInvariant();
        if (JobPartyKit.TryGetValue(t, out var kit))
            return kit.Select(k => (k.Name, Cooldowns.PlanInfo(k.Name)?.Recast is { } r and > 5f ? r : k.Recast)).ToArray();
        return t switch
        {
            "MT" or "OT" or "T" or "T1" or "T2" or "TANK" => new[] { ("Reprisal", 60f), ("Party Mit", 90f) },
            "D1" or "D2" or "M1" or "M2" or "MELEE" or "D" or "DPS" => new[] { ("Feint", 90f) },
            "D3" or "R1" => new[] { ("Party Mit", 90f) },
            "D4" or "R2" => new[] { ("Addle", 90f), ("Party Mit", 120f) },
            // Healer party mits differ per job; the generic term resolves at
            // call time, spaced to the slowest of them so the button is never dead.
            var h when h.StartsWith("H") => new[] { ("Party Mit", 120f) },
            _ => Array.Empty<(string, float)>(),
        };
    }

    // Mits that land as a debuff ON THE ENEMY: a second source on the same hit
    // is wasted, so the planner allows one of each per hit, party-wide.
    private static readonly HashSet<string> DebuffMits = new(StringComparer.OrdinalIgnoreCase)
        { "Reprisal", "Feint", "Addle", "Dismantle" };

    private sealed class PlanTool
    {
        public string Slot = "";
        public string Term = "";
        public float Recast;
        public float ReadyAt;
        public float LastUse = -9999f;
        public int Order;
    }

    // The planner, patterned on how the reference sheets actually play:
    // - Deadly hits stack the whole party (every column contributes; healers
    //   and named-job columns may pair two mits, "Plenary Indulgence + Asylum"
    //   style). Hurts takes about half the party; light takes one press;
    //   ungraded rows follow the slider.
    // - Big cooldowns are saved for big hits: a long mit is not spent on a
    //   light row when a deadly row lands inside its recast.
    // - Every press respects its recast (the game's own numbers when
    //   available), the load rotates least-recently-used first, hits within
    //   15s share one press, and cells you wrote are never touched.
    private int AutoPlanMits(FightProfile fight, int perRow)
    {
        var rows = fight.CustomRows.OrderBy(r => r.Time).ToList();
        if (rows.Count == 0) return 0;
        var deadlyTimes = rows.Where(r => r.Hurt >= 3).Select(r => r.Time).ToList();
        var sync = Cooldowns.DutySyncLevel(fight.TerritoryId);

        // How long a planned line's mitigation actually lasts: the shortest
        // buff in it (game data; generic terms fall back to a Reprisal-ish 15s).
        static float LineCover(MitLine l) => l.Action
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Cooldowns.PlanInfo(part)?.Duration is { } d and > 0f ? d : 15f)
            .DefaultIfEmpty(15f).Min();

        var tools = new List<PlanTool>();
        var lists = new Dictionary<string, List<MitLine>>(StringComparer.OrdinalIgnoreCase);
        var order = 0;
        foreach (var slot in fight.CustomSlots)
        {
            if (!fight.SavedSlots.TryGetValue(slot, out var list))
            {
                list = string.Equals(slot, fight.Slot, StringComparison.OrdinalIgnoreCase)
                    ? fight.Lines : new List<MitLine>();
                fight.SavedSlots[slot] = list;
            }
            lists[slot] = list;
            foreach (var (term, recast) in PoolFor(slot))
            {
                // Old synced duties: skip anything the sync level locks out
                // (generic terms carry no level and always pass).
                if (sync > 0 && Cooldowns.PlanInfo(term)?.Level is { } lv and > 0 && lv > sync) continue;
                tools.Add(new PlanTool { Slot = slot, Term = term, Recast = recast, Order = order++ });
            }
        }
        if (tools.Count == 0) return 0;

        // Spending this tool now would steal it from an upcoming deadly hit.
        bool StealsFromDeadly(PlanTool t, float now)
            => t.Recast >= 100f && deadlyTimes.Any(td => td > now && td < now + t.Recast);

        var added = 0;
        var lastCovered = -9999f;
        var lastCoveredHurt = 0;
        var lastAdded = new List<MitLine>(); // this run's presses at lastCovered
        foreach (var row in rows)
        {
            // Hits inside the previous press's window ride it, UNLESS this one
            // is graded harder than what that press was sized for, or none of
            // our presses' buffs actually last this long (real durations from
            // the game data). Riding presses get a press window (CoverUntil)
            // capped to what each buff can truly cover, so the grid never
            // promises coverage a 10s buff cannot deliver.
            if (row.Time - lastCovered < 15f && row.Hurt <= lastCoveredHurt
                && (lastAdded.Count == 0 || lastAdded.Any(l => row.Time <= l.Time + LineCover(l) + 0.01f)))
            {
                foreach (var l in lastAdded)
                    if (l.CoverUntil < row.Time && row.Time <= l.Time + LineCover(l) + 0.01f)
                        l.CoverUntil = row.Time;
                continue;
            }
            var have = lists.Values.Count(l => l.Any(x =>
                MathF.Abs(x.Time - row.Time) < 1f && !string.IsNullOrWhiteSpace(x.Action)));
            // Depth per severity, matching the reference sheets' stacking: the
            // slider only drives ungraded rows.
            var target = row.Hurt switch
            {
                3 => lists.Count,
                2 => Math.Max(3, lists.Count / 2),
                1 => 1,
                _ => perRow,
            };
            var need = target - have;
            if (need <= 0)
            {
                lastCovered = row.Time;
                lastCoveredHurt = row.Hurt;
                lastAdded.Clear(); // that coverage is the user's, not ours to extend
                continue;
            }

            var ready = tools
                .Where(t => t.ReadyAt <= row.Time + 0.01f)
                .Where(t => !lists[t.Slot].Any(x => MathF.Abs(x.Time - row.Time) < 1f))
                .ToList();
            // Save the big buttons for the big hits.
            if (row.Hurt is 1 or 0)
                ready.RemoveAll(t => StealsFromDeadly(t, row.Time));

            // Enemy debuffs don't stack from two sources: one Reprisal, one
            // Feint, one Addle per hit, party-wide; the sheets rotate WHO casts
            // them and the LRU ordering reproduces that. Seed with whatever the
            // user already wrote on this row.
            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in lists.Values)
                foreach (var x in l)
                    if (MathF.Abs(x.Time - row.Time) < 1f)
                        foreach (var d in DebuffMits)
                            if (x.Action.Contains(d, StringComparison.OrdinalIgnoreCase)) claimed.Add(d);

            // Per column, its candidates in preference order: the biggest ready
            // mit on deadly rows (two of them joined, like the sheets' worst
            // hits), the smallest that does the job on light rows, and on
            // medium rows whatever does not rob an upcoming deadly hit.
            var byCol = ready.GroupBy(t => t.Slot).Select(g => (row.Hurt switch
                {
                    3 => g.OrderByDescending(t => t.Recast).ThenBy(t => t.Order),
                    1 or 0 => g.OrderBy(t => t.Recast).ThenBy(t => t.Order),
                    _ => g.OrderBy(t => StealsFromDeadly(t, row.Time) ? 1 : 0)
                          .ThenByDescending(t => t.Recast).ThenBy(t => t.Order),
                }).ToList())
                .OrderBy(opts => row.Hurt == 2 && StealsFromDeadly(opts[0], row.Time) ? 1 : 0)
                .ThenBy(opts => opts[0].LastUse).ThenBy(opts => opts[0].Order)
                .ToList();

            var rowLines = new List<MitLine>();
            foreach (var opts in byCol)
            {
                if (rowLines.Count >= need) break;
                var set = new List<PlanTool>();
                foreach (var t in opts)
                {
                    if (set.Count >= (row.Hurt >= 3 ? 2 : 1)) break;
                    if (DebuffMits.Contains(t.Term) && claimed.Contains(t.Term)) continue;
                    set.Add(t);
                }
                if (set.Count == 0) continue;
                var line = new MitLine
                {
                    Time = row.Time,
                    Mechanic = row.Mechanic,
                    Action = string.Join(" + ", set.Select(t => t.Term)),
                    Enabled = true,
                    Custom = true,
                };
                lists[set[0].Slot].Add(line);
                rowLines.Add(line);
                foreach (var t in set)
                {
                    if (DebuffMits.Contains(t.Term)) claimed.Add(t.Term);
                    t.ReadyAt = row.Time + t.Recast;
                    t.LastUse = row.Time;
                }
                added++;
            }
            if (rowLines.Count > 0 || have > 0)
            {
                lastCovered = row.Time;
                lastCoveredHurt = row.Hurt;
                lastAdded = rowLines;
            }
        }

        foreach (var l in lists.Values)
        {
            var sorted = l.OrderBy(x => x.Time).ToList();
            l.Clear();
            l.AddRange(sorted);
        }
        return added;
    }

    private void DrawAutoPlanPopup()
    {
        var stay = true;
        if (!ImGui.BeginPopupModal("##autoplan", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;

        PopupHeader("Auto-plan mits", 520f);
        if (_fight == null || !_isCustom)
        {
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }
        if (_fight.CustomRows.Count == 0)
        {
            ImGui.TextUnformatted("Want the mits planned for you? Add the mechanics first.");
            ImGui.TextDisabled("Build > Add row (or Build from pull / Import FFLogs log) creates the");
            ImGui.TextDisabled("rows; then Build > Auto-plan mits fills every column with cooldowns");
            ImGui.TextDisabled("that line up: spaced to their recasts, spread across the party.");
            ImGui.Spacing();
            if (ImGui.Button("Got it", new Vector2(110, 0))) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        var gradedRows = _fight.CustomRows.Count(r => r.Hurt > 0);
        ImGui.TextUnformatted($"Fill the grid with party cooldowns for {_fight.CustomRows.Count} rows?");
        ImGui.TextDisabled("Planned the way the official sheets play it: deadly hits stack the whole");
        ImGui.TextDisabled("party (healers pair big mits), hurts takes about half, light gets one");
        ImGui.TextDisabled("press, and long cooldowns are saved for the big hits so they line up.");
        ImGui.TextDisabled("Reprisal/Feint/Addle are never doubled on one hit; sources rotate instead.");
        ImGui.TextDisabled("Columns named for a job (WHM, SGE, MCH...) plan with that job's real");
        ImGui.TextDisabled("kit; other role columns (MT, D3...) get terms that speak as each");
        ImGui.TextDisabled("player's own ability. Recasts always respected; your cells never touched.");

        // Healer seats: the four healer jobs' kits barely overlap, so the
        // sheets carry a column per healer JOB. Planning expands the seats.
        var healerCols = GenericHealerCols();
        if (healerCols.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(ImGuiColors.HealerGreen,
                "Healer seats become WHM, AST, SCH and SGE columns, like the official");
            ImGui.TextColored(ImGuiColors.HealerGreen,
                "sheets: every healer job gets its real cooldowns planned. Pick your");
            ImGui.TextColored(ImGuiColors.HealerGreen,
                "own column AFTER planning, from the column headers or fight page.");
        }
        var noKit = _fight.CustomSlots.Where(sl => PoolFor(sl).Length == 0).ToList();
        if (noKit.Count > 0)
            ImGui.TextColored(ImGuiColors.DalamudYellow,
                $"No kit for: {string.Join(", ", noKit)}. Rename to a job (WHM) or role (H1, D3) to include them.");
        ImGui.Spacing();
        if (gradedRows > 0)
            ImGui.TextDisabled($"{gradedRows} row(s) are graded by how hard they hit (log damage or your own");
        if (gradedRows > 0)
            ImGui.TextDisabled("grades) and set their own depth. The slider covers ungraded rows.");
        else
            ImGui.TextDisabled("Tip: import an FFLogs log and rows get graded by real unmitigated");
        if (gradedRows == 0)
            ImGui.TextDisabled("damage; graded rows then set their own stacking depth.");
        ImGui.TextDisabled("Job-specific cooldowns (Dismantle, Curing Waltz, ...) stay optional");
        ImGui.TextDisabled("extras on the fight page, like the sheet's Extras column.");
        ImGui.Spacing();
        ImGui.SetNextItemWidth(160f);
        ImGui.SliderInt("mits per hit", ref _autoPlanPerRow, 1, 3);
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        if (ImGui.Button("Plan mits", new Vector2(110, 0)))
        {
            PushUndo("auto-plan mits");
            _plugin.SnapshotPlan(_fight, "before auto-plan");
            // Healer seats become ALL FOUR healer job columns first (the
            // sheets' convention), so every healer's kit is covered.
            ExpandHealerSeats(_fight);
            var n = AutoPlanMits(_fight, _autoPlanPerRow);
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
        if (ImGui.Button("Not now", new Vector2(110, 0))) ImGui.CloseCurrentPopup();
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

    // Short hover delay for informational tooltips on the toolbar sweep path.
    private static Vector2 _ttPos;
    private static double _ttSince;
    private static int _ttFrame;

    private static bool DelayedHover(ImGuiHoveredFlags flags = ImGuiHoveredFlags.None)
    {
        if (!ImGui.IsItemHovered(flags)) return false;
        // The item rect is a fine identity for "did the hovered thing change";
        // a frame gap means the mouse left and the delay starts over.
        var pos = ImGui.GetItemRectMin();
        var now = ImGui.GetTime();
        var frame = ImGui.GetFrameCount();
        if (pos != _ttPos || frame - _ttFrame > 2) { _ttPos = pos; _ttSince = now; }
        _ttFrame = frame;
        return now - _ttSince >= 0.35;
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

        // Text filter across mechanics and mits ("Reprisal" = every Reprisal row).
        ImGui.SameLine(0, 10);
        ImGui.SetNextItemWidth(140f);
        ImGui.InputTextWithHint("##sheetfilter", "filter...", ref _filter, 64);
        if (DelayedHover() && !ImGui.IsItemActive())
            ImGui.SetTooltip("Show only rows whose mechanic or any slot's mit contains this text.");
        if (_filter.Length > 0)
        {
            ImGui.SameLine(0, 2);
            if (ImGui.SmallButton("x##clearfilter")) _filter = "";
        }

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
                "Click a time to re-time a mechanic for every slot; click a cell to edit that slot only.\n"
                + "While editing: Enter moves down, Tab moves right. Ctrl+Z undoes any edit.\n"
                + "Orange * = your edit; red cell = cooldown conflict; amber = above the duty's level sync; dim = deleted.\n"
                + "Drag column edges to resize (double-click to fit) or drag headers to reorder.\n"
                + "Right-click cells, mechanics and column headers; most tools live there.");

        // Right side: Undo | Build (custom sheets) | Plan | Share plan. The
        // one-click accent stays on Share; everything else folds into menus.
        var rightW = ImGui.CalcTextSize("Undo").X + ImGui.CalcTextSize("Plan").X
                   + ImGui.CalcTextSize("Share plan").X + 96f
                   + (_isCustom ? ImGui.CalcTextSize("Build").X + 32f : 0f);
        ImGui.SameLine(MathF.Max(ImGui.GetCursorPosX() + 8f, ImGui.GetContentRegionMax().X - rightW));
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
        var openDelete = false;

        if (_isCustom)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Build")) ImGui.OpenPopup("##buildmenu");
            if (DelayedHover())
                ImGui.SetTooltip("Grow this sheet: add rows by hand, from your own pulls, or from an FFLogs kill.");
            if (ImGui.BeginPopup("##buildmenu"))
            {
                if (ImGui.MenuItem("Add row...")) openAddRow = true;
                if (ImGui.MenuItem("Build from pull...")) openBuildPull = true;
                if (ImGui.MenuItem("Import FFLogs log...")) openLog = true;
                ImGui.Separator();
                if (ImGui.MenuItem("Auto-plan mits...")) _openAutoPlan = true;
                if (ImGui.IsItemHovered())
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
            if (ImGui.MenuItem("Export as text"))
            {
                CommitPending();
                if (_dirty) Rebuild();
                ExportText();
            }
            if (ImGui.MenuItem("Import plan code")) ImportPlan();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Paste a friend's Share plan code from your clipboard.\nTheir slot is replaced; your other slots are kept.");
            if (ImGui.MenuItem("Replace a mit...")) openReplace = true;
            if (ImGui.MenuItem("Plan history...")) openHistory = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Snapshots taken automatically before imports, replaces and\ncolumn pastes; restore any of them.");
            if (!_isCustom && ImGui.MenuItem("Reset all columns...")) _openResetAll = true;
            if (!_isCustom && ImGui.IsItemHovered())
                ImGui.SetTooltip("Reload EVERY column from the baked sheet: all slots' edits and\ndeletions go, including added potion, job and tank lines.\nA snapshot is saved first; Plan > History restores it.");
            if (ImGui.MenuItem("Open fight page")) _plugin.ConfigWindow.OpenFightPage(_fight!);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Per-line options, anchors and import tools live there.");
            if (ImGui.MenuItem("Open Mit Tuner")) _plugin.MiniSheetWindow.IsOpen = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A pocket version for mid-pull use: the calls around now,\neach with +/- nudges for its offset. Also /fm mini.");
            if (_isCustom)
            {
                ImGui.Separator();
                if (ImGui.MenuItem("Delete this sheet...")) openDelete = true;
            }
            ImGui.Separator();
            if (ImGui.MenuItem("Color mits by type", "", C.SheetColorByType))
            {
                C.SheetColorByType = !C.SheetColorByType;
                C.Save();
            }
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        if (ImGui.SmallButton("Share plan")) SharePlan();
        ImGui.PopStyleColor(2);
        if (DelayedHover())
            ImGui.SetTooltip("Copy the whole plan as a clipboard code. Friends use Plan > Import plan code\n(or the fight page); it updates their fight in place (their slot's plan).");

        if (openReplace) { _replFind = _filter; ImGui.OpenPopup("##sheetreplace"); }
        DrawReplacePopup();
        if (openHistory)
        {
            _snapList = _plugin.ListSnapshots(_fight!.Id);
            ImGui.OpenPopup("##sheethistory");
        }
        DrawHistoryPopup();
        if (openDelete) ImGui.OpenPopup("##sheetdelete");
        DrawDeleteSheetPopup();
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
        ImGui.SetNextItemWidth(200f);
        ImGui.InputTextWithHint("##armech", "mechanic name", ref _rowMech, 64);
        ImGui.SetNextItemWidth(200f);
        ImGui.InputTextWithHint("##artime", "time (m:ss or seconds)", ref _rowTime, 16);
        ImGui.SetNextItemWidth(200f);
        ImGui.Combo("hits##arhurt", ref _rowHurt, HurtChoices, HurtChoices.Length);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("How hard the hit is unmitigated. Auto-plan stacks mitigation deeper\non harder hits; log imports grade this automatically from real damage.");
        var okRow = _rowMech.Trim().Length > 0 && SheetImport.TryParseTime(_rowTime, out _);
        ImGui.BeginDisabled(!okRow);
        if (ImGui.Button("Add row", new Vector2(110, 0)))
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
        var (fight, _, message) = _plugin.ImportPlanCode(ImGui.GetClipboardText());
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

    private void PhaseButton(string name, bool on)
    {
        if (on)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentText);
        }
        if (ImGui.SmallButton($"{name}###ph{name}"))
        {
            // Land any open editor BEFORE the filter hides its row, or the edit
            // state would linger unseen (blocking rebuilds) until a later click.
            CommitPending();
            _phaseFilter = name == "All" ? "" : name;
        }
        if (on) ImGui.PopStyleColor(3);
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
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Every column's plan, rows, notes and learned anchors go with it.");
        ImGui.TextDisabled("A snapshot is saved first. To recover: recreate a sheet in this duty,");
        ImGui.TextDisabled("then History > Find this duty's older snapshots.");
        ImGui.Spacing();

        if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF2222C8);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF3333DD);
        if (ImGui.Button("Delete", new Vector2(120, 0)))
        {
            var f = _fight!;
            _plugin.SnapshotPlan(f, "before delete");
            _undoStack.RemoveAll(u => u.Fight == f);
            C.Fights.Remove(f);
            C.Save();
            _fight = null;
            _dirty = true;
            ImGui.CloseCurrentPopup();
            Flash($"\"{f.Name}\" deleted. A snapshot was kept.");
        }
        ImGui.PopStyleColor(2);
        ImGui.EndPopup();
    }

    // ---- plan snapshots (History) -------------------------------------------

    private List<Plugin.SnapshotInfo> _snapList = new();

    private void DrawHistoryPopup()
    {
        // Modal so a stray click outside cannot dismiss the form; the X,
        // Escape, or its own buttons close it.
        var stay = true;
        if (!ImGui.BeginPopupModal("##sheethistory", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;

        PopupHeader("Plan snapshots (this fight)", 440f);
        if (ImGui.SmallButton("Snapshot now"))
        {
            _plugin.SnapshotPlan(_fight!, "manual snapshot");
            _snapList = _plugin.ListSnapshots(_fight!.Id);
            Flash("Snapshot saved.");
        }
        ImGui.Separator();

        if (_snapList.Count == 0)
        {
            ImGui.TextDisabled("None yet. Snapshots are taken automatically before");
            ImGui.TextDisabled("imports, replaces, column pastes and sheet refreshes.");
        }
        foreach (var s in _snapList)
        {
            ImGui.TextUnformatted($"{s.When:MMM d, h:mm tt}");
            ImGui.SameLine(0, 8);
            ImGui.TextDisabled(s.Reason);
            ImGui.SameLine(0, 12);
            if (ImGui.SmallButton($"Restore##{s.File}"))
            {
                CommitPending();
                PushUndo("restore snapshot"); // restoring is itself undoable
                var msg = _plugin.RestoreSnapshot(_fight!, s.File);
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
            if (ImGui.SmallButton("Find this duty's older snapshots"))
                _snapList = _plugin.ListSnapshots(_fight!.Id)
                    .Concat(_plugin.ListOrphanSnapshots(_fight.TerritoryId, _fight.Id))
                    .ToList();
            if (ImGui.IsItemHovered())
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
        for (var i = 0; i < _slots.Length; i++)
        {
            if (row.Cells[i].Count == 0) continue;
            EnsureBacked(i);
            foreach (var l in row.Cells[i].ToList()) _slotLines[i].Remove(l);
            Resort(i);
        }
        _fight.CustomRows.RemoveAll(cr =>
            MechEquals(cr.Mechanic, row.Mechanic) && MathF.Abs(cr.Time - row.Time) < 2f);
        C.Save();
        _dirty = true;
        Flash($"\"{row.Mechanic}\" removed. Ctrl+Z brings it back.");
    }

    // ---- build from pull -----------------------------------------------------
    // In a custom-sheet duty, SyncEngine records every NPC cast of the pull
    // automatically; this turns that capture into mechanic rows + cast anchors.

    private bool _bpRows = true;
    private bool _bpAnchors = true;

    private static string ActionName(uint id)
    {
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            var row = sheet?.GetRowOrDefault(id);
            return row?.Name.ExtractText() ?? "";
        }
        catch { return ""; }
    }

    private void DrawBuildFromPullPopup()
    {
        // Modal so a stray click outside cannot dismiss the form; the X,
        // Escape, or its own buttons close it.
        var stay = true;
        if (!ImGui.BeginPopupModal("##buildpull", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;

        PopupHeader("Build from last pull", 400f);

        // Only offer a capture that came from THIS duty: building duty A's
        // casts into duty B's sheet would replace B's anchors with nonsense.
        var casts = _fight != null && _plugin.Sync.LastPullTerritory == _fight.TerritoryId
            ? _plugin.Sync.LastPull.Where(cp => !cp.IsBoss).ToList()
            : new List<SyncEngine.Capture>();
        if (casts.Count == 0)
        {
            ImGui.TextDisabled("Nothing captured from this duty yet. Do a pull (even a");
            ImGui.TextDisabled("short wipe); the boss's casts are recorded automatically.");
            ImGui.EndPopup();
            return;
        }

        ImGui.TextUnformatted($"{casts.Count} casts captured from the last pull.");
        ImGui.Checkbox("Add mechanic rows", ref _bpRows);
        ImGui.Checkbox("Set resync anchors", ref _bpAnchors);
        if (_bpAnchors)
            ImGui.TextDisabled("Replaces this fight's existing cast anchors.");

        ImGui.BeginDisabled(!_bpRows && !_bpAnchors);
        if (ImGui.Button("Build", new Vector2(110, 0)))
        {
            BuildFromPull(casts, _bpRows, _bpAnchors);
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    private readonly record struct BuildEvent(uint Id, float Time, string Name, bool Anchorable);

    // Grade an ability's hardest unmitigated hit against the fight's hardest
    // RAIDWIDE (busters are excluded from the yardstick): the top band is
    // deadly, the middle hurts, anything known is at least light. Bands are
    // tuned for that clean scale; a hit within three quarters of the worst
    // raidwide deserves the full stack.
    private static int HurtLevel(long dmg, long max)
        => dmg <= 0 || max <= 0 ? 0
         : dmg >= max * 0.75 ? 3
         : dmg >= max * 0.40 ? 2
         : 1;

    private void BuildFromPull(List<SyncEngine.Capture> casts, bool rows, bool anchors)
    {
        // Live captures come from cast bars, so every one is anchorable.
        var events = SiftEvents(casts.OrderBy(cp => cp.Time).Select(cp => (cp.Id, cp.Time, Anchorable: true)));
        ApplyBuild(events, rows, anchors, "the last pull");
    }

    // Resolve names from the game data; drop unnamed casts, auto-attacks, and
    // back-to-back repeats of the same ability (double casts).
    private static List<BuildEvent> SiftEvents(IEnumerable<(uint Id, float Time, bool Anchorable)> raw)
    {
        var events = new List<BuildEvent>();
        foreach (var (id, time, anchorable) in raw)
        {
            var name = ActionName(id);
            if (name.Length == 0) continue;
            if (string.Equals(name, "attack", StringComparison.OrdinalIgnoreCase)) continue;
            if (events.Count > 0 && events[^1].Id == id && time - events[^1].Time < 3f) continue;
            events.Add(new BuildEvent(id, time, name, anchorable));
        }
        return events;
    }

    private void ApplyBuild(List<BuildEvent> events, bool rows, bool anchors, string source,
        Dictionary<uint, FFLogsClient.AbilityDamage>? damage = null)
    {
        // Custom sheets only: replacing a BUILTIN fight's anchors would destroy
        // the official ones (unreachable via UI today; cheap insurance).
        if (_fight == null || !_isCustom || AbortIfStale()) return;
        if (events.Count == 0)
        {
            Flash("No usable casts found (only auto-attacks).");
            return;
        }

        PushUndo($"build from {source}");
        _plugin.SnapshotPlan(_fight, $"before build from {source}");

        // Severity from the log's real damage: each ability's hardest
        // unmitigated hit, graded against the fight's hardest RAIDWIDE. Busters
        // (an ability that only ever hit a player or two) are excluded from the
        // yardstick and capped below deadly: they are the tanks' problem, not a
        // stack-the-party moment.
        var maxDmg = 0L;
        if (damage is { Count: > 0 })
            foreach (var e in events)
                if (damage.TryGetValue(e.Id, out var d) && d.Targets > 3 && d.Worst > maxDmg) maxDmg = d.Worst;

        var addedRows = 0;
        var graded = 0;
        if (rows)
            foreach (var e in events)
            {
                var hurt = 0;
                if (damage != null && damage.TryGetValue(e.Id, out var d))
                {
                    hurt = HurtLevel(d.Worst, maxDmg);
                    if (d.Targets <= 3 && hurt > 2) hurt = 2;
                }
                // A row that already exists still learns its grade from the log.
                var existing = _fight.CustomRows.FirstOrDefault(cr =>
                    MechEquals(cr.Mechanic, e.Name) && MathF.Abs(cr.Time - e.Time) < 2f);
                if (existing != null)
                {
                    if (existing.Hurt == 0 && hurt > 0) { existing.Hurt = hurt; graded++; }
                    continue;
                }
                if (_rows.Any(r => !r.Ghost && MechEquals(r.Mechanic, e.Name) && MathF.Abs(r.Time - e.Time) < 2f))
                    continue;
                _fight.CustomRows.Add(new CustomRow { Time = MathF.Round(e.Time), Mechanic = e.Name, Hurt = hurt });
                if (hurt > 0) graded++;
                addedRows++;
            }

        var anchorCount = 0;
        var noAnchorable = anchors && !events.Any(e => e.Anchorable);
        if (anchors && noAnchorable)
        {
            // Nothing in this source had a cast bar: leave the fight's existing
            // anchors alone rather than wiping them to (nearly) nothing.
            anchors = false;
        }
        if (anchors)
        {
            // A captured cast IS an anchor: ability id + the time it resolved.
            // A cast after a long quiet stretch (downtime, phase transition)
            // re-bases the whole clock; the rest trim drift with tight windows.
            // NOT the first cast though: at pull start the timer is already
            // exact, and a phase anchor's wide backward window there could yank
            // a later pull's clock back to the opener.
            var points = new List<SyncPoint>();
            var prev = 0f;
            var pendingPhase = false;
            var lastById = new Dictionary<uint, float>();
            foreach (var e in events)
            {
                // The gap detector runs over EVERY event; the phase flag then
                // lands on the next anchorable cast (a log's instant abilities
                // can't anchor, but they mustn't hide the downtime gap either).
                if (e.Time - prev > 90f) pendingPhase = true;
                prev = e.Time;
                if (!e.Anchorable) continue;
                // Same ability again within ~two match windows: skip the anchor
                // (multi-hit raidwides), or overlapping windows could snap the
                // clock to the wrong instance. The row above still exists.
                if (lastById.TryGetValue(e.Id, out var lt) && e.Time - lt < 18f) continue;
                lastById[e.Id] = e.Time;
                points.Add(new SyncPoint { Ability = e.Id, Time = e.Time, IsPhase = pendingPhase, Label = e.Name });
                pendingPhase = false;
            }
            // Keep any previously learned anchors BEYOND this pull's end, so a
            // short wipe never truncates coverage a longer pull already earned.
            var end = events[^1].Time;
            points.AddRange(_fight.SyncPoints.Where(sp => sp.Time > end + 10f));
            _fight.SyncPoints = points;
            anchorCount = points.Count;
        }

        if (addedRows == 0 && anchorCount == 0 && graded == 0)
        {
            PopUndo();
            Flash("Nothing new there (rows already covered, anchors unticked).");
            return;
        }

        C.Save();
        _dirty = true;
        var gradeNote = graded > 0 ? $" {graded} row(s) graded by how hard they hit." : "";
        Flash(noAnchorable
            ? $"Built from {source}: {addedRows} new row(s).{gradeNote} No cast-bar casts found, so existing anchors were left untouched."
            : $"Built from {source}: {addedRows} new row(s), {anchorCount} anchor(s).{gradeNote} "
              + "Build again any time; anchors past this build's end are kept.");
    }

    // ---- FFLogs import ---------------------------------------------------
    // Paste a report URL, pick the fight, and its enemy casts become rows +
    // anchors via the same builder "Build from pull" uses. Results from the
    // background tasks land in these fields as whole-list assignments, which
    // the draw thread reads; only the Import click mutates the plan.

    private string _flUrl = "";
    private string _flStatus = "";
    private volatile bool _flBusy;
    private List<FFLogsClient.FightInfo>? _flFights;
    private int _flPick;
    private List<FFLogsClient.LogCast>? _flCasts;
    private Dictionary<uint, FFLogsClient.AbilityDamage>? _flDamage;
    private int _flCastsForFight = -1;
    private string _flIdBuf = "";
    private string _flSecretBuf = "";
    private FightProfile? _flForFight; // whose sheet the cached report state belongs to

    private void DrawFFLogsPopup()
    {
        // Modal so a stray click outside cannot dismiss the form; the X,
        // Escape, or its own buttons close it.
        var stay = true;
        if (!ImGui.BeginPopupModal("##fflogs", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;

        PopupHeader("Import an FFLogs report", 460f);

        // Cached report state is per fight: duty A's casts must never sit one
        // click away from being imported into duty B's sheet.
        if (ImGui.IsWindowAppearing() && _flForFight != _fight)
        {
            _flFights = null;
            _flCasts = null;
            _flDamage = null;
            _flCastsForFight = -1;
            _flStatus = "";
            _flForFight = _fight;
        }

        // One-time credentials: the user makes an API client on the FFLogs
        // site and pastes the two strings here. Stored locally only.
        if (C.FflogsClientId.Length == 0 || C.FflogsClientSecret.Length == 0)
        {
            ImGui.TextDisabled("One-time setup (about two minutes)");
            ImGui.TextWrapped("FFLogs' API needs a personal client. Create one (any name, no redirect "
                + "URL needed), then paste its id and secret here. They stay on this PC.");
            if (ImGui.SmallButton("Open fflogs.com/api/clients"))
                Dalamud.Utility.Util.OpenLink("https://www.fflogs.com/api/clients");
            ImGui.SetNextItemWidth(300f);
            ImGui.InputTextWithHint("##flid", "client id", ref _flIdBuf, 128);
            ImGui.SetNextItemWidth(300f);
            ImGui.InputTextWithHint("##flsecret", "client secret", ref _flSecretBuf, 128, ImGuiInputTextFlags.Password);
            ImGui.BeginDisabled(_flIdBuf.Trim().Length == 0 || _flSecretBuf.Trim().Length == 0);
            if (ImGui.Button("Save credentials", new Vector2(160, 0)))
            {
                C.FflogsClientId = _flIdBuf.Trim();
                C.FflogsClientSecret = _flSecretBuf.Trim();
                C.Save();
            }
            ImGui.EndDisabled();
            ImGui.EndPopup();
            return;
        }

        ImGui.SetNextItemWidth(320f);
        ImGui.InputTextWithHint("##flurl", "FFLogs report link (or code)", ref _flUrl, 256);
        ImGui.SameLine();
        ImGui.BeginDisabled(_flBusy || FFLogsClient.ParseReportCode(_flUrl) == null);
        if (ImGui.SmallButton("Fetch")) FetchFights();
        ImGui.EndDisabled();
        ImGui.SameLine();
        // Typo'd credentials must be fixable without config-file surgery.
        if (ImGui.SmallButton("Credentials..."))
        {
            _flIdBuf = C.FflogsClientId;
            _flSecretBuf = "";
            C.FflogsClientId = "";
            C.FflogsClientSecret = "";
            C.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Re-enter your FFLogs API client id and secret.");

        if (_flStatus.Length > 0) ImGui.TextDisabled(_flStatus);

        if (_flFights is { Count: > 0 } fights)
        {
            var labels = fights.Select(f =>
                $"#{f.Id}  {f.Name}  {(f.Kill ? "KILL" : "wipe")}  {(int)f.DurationSec / 60}:{(int)f.DurationSec % 60:00}").ToArray();
            _flPick = Math.Clamp(_flPick, 0, fights.Count - 1);
            ImGui.SetNextItemWidth(320f);
            if (ImGui.Combo("##flfight", ref _flPick, labels, labels.Length))
            {
                _flCasts = null; // picked a different fight: refetch its casts
                _flDamage = null;
                _flCastsForFight = -1;
            }

            var picked = fights[_flPick];
            if (_flCasts == null || _flCastsForFight != picked.Id)
            {
                ImGui.BeginDisabled(_flBusy);
                if (ImGui.Button("Load casts", new Vector2(120, 0))) FetchCasts(picked);
                ImGui.EndDisabled();
            }
            else
            {
                ImGui.TextUnformatted($"{_flCasts.Count} enemy casts loaded.");
                ImGui.Checkbox("Add mechanic rows", ref _bpRows);
                ImGui.Checkbox("Set resync anchors", ref _bpAnchors);
                ImGui.TextDisabled("Their kill's timings become this sheet's skeleton; anchors");
                ImGui.TextDisabled("snap it to YOUR pulls live. Make sure the log is this duty.");
                ImGui.BeginDisabled(!_bpRows && !_bpAnchors);
                if (ImGui.Button("Import", new Vector2(120, 0)))
                {
                    var events = SiftEvents(_flCasts.OrderBy(c => c.Time)
                        .Select(c => (c.AbilityId, c.Time, Anchorable: c.HasCastBar)));
                    ApplyBuild(events, _bpRows, _bpAnchors, "the log", _flDamage);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndDisabled();
            }
        }

        ImGui.EndPopup();
    }

    private void FetchFights()
    {
        var code = FFLogsClient.ParseReportCode(_flUrl);
        if (code == null) return;
        _flBusy = true;
        _flStatus = "Fetching report...";
        _flFights = null;
        _flCasts = null;
        _flCastsForFight = -1;
        _flPick = 0;              // reset on the draw thread: it also clamps this
        _flForFight = _fight;
        var (id, secret) = (C.FflogsClientId, C.FflogsClientSecret);
        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var fights = await _plugin.FFLogs.GetFightsAsync(id, secret, code);
                _flFights = fights;
                _flStatus = fights.Count == 0 ? "No boss fights in that report." : $"{fights.Count} fight(s); kills listed first.";
            }
            catch (Exception ex)
            {
                _flStatus = ex.Message;
                Service.Log.Warning(ex, "FrenMits: FFLogs fights fetch failed");
            }
            finally { _flBusy = false; }
        });
    }

    private void FetchCasts(FFLogsClient.FightInfo fight)
    {
        var code = FFLogsClient.ParseReportCode(_flUrl);
        if (code == null) return;
        _flBusy = true;
        _flStatus = $"Loading {fight.Name}'s casts...";
        var (id, secret) = (C.FflogsClientId, C.FflogsClientSecret);
        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var casts = await _plugin.FFLogs.GetCastsAsync(id, secret, code, fight);
                // Damage grades are a bonus: a fetch hiccup must not block the import.
                Dictionary<uint, FFLogsClient.AbilityDamage>? dmg = null;
                try { dmg = await _plugin.FFLogs.GetDamageAsync(id, secret, code, fight); }
                catch (Exception dex) { Service.Log.Warning(dex, "FrenMits: FFLogs damage fetch failed"); }
                _flCasts = casts;
                _flDamage = dmg;
                _flCastsForFight = fight.Id;
                _flStatus = "";
            }
            catch (Exception ex)
            {
                _flStatus = ex.Message;
                Service.Log.Warning(ex, "FrenMits: FFLogs casts fetch failed");
            }
            finally { _flBusy = false; }
        });
    }

    // ---- search & replace --------------------------------------------------

    private void DrawReplacePopup()
    {
        // Modal so a stray click outside cannot dismiss the form; the X,
        // Escape, or its own buttons close it.
        var stay = true;
        if (!ImGui.BeginPopupModal("##sheetreplace", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;

        PopupHeader("Replace a mit across the sheet", 420f);
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

        var would = 0;
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_replMineOnly && !IsActiveSlot(i)) continue;
            would += _slotLines[i].Count(l => WouldReplace(l.Action, find, with) != null);
        }
        if (would == 0) { Flash($"No mits containing \"{find}\"."); return; }

        // Bulk edit: undoable AND snapshotted to disk (see the History button).
        PushUndo($"replace \"{find}\"");
        _plugin.SnapshotPlan(_fight, $"before replacing \"{find}\"");

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

        if (changed == 0) { PopUndo(); Flash($"No mits containing \"{find}\"."); return; }
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
    // Title + close X for a popup. `width` pins the header row's span: the X
    // must NOT be placed from the live window width, because in an auto-resizing
    // popup that feeds the window its own width plus padding every frame and it
    // balloons across the whole screen.
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
        // column to its content. Reorderable: drag a header to move the column.
        // (Both are the Google-sheets gestures.)
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
                // visible at a glance. Icon font: the text font has no pin glyph.
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
        // Right-click the mechanic name = note editor. The footer strip shows the
        // note for whatever row the mouse is on, zero clicks to read.
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

    // Remove a job-extra row's lines everywhere. They're custom lines, so no
    // tombstones are needed: the sheet's top-up never re-adds custom lines.
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
        // On a custom sheet EVERY line is technically Custom, so the orange
        // "your edit" treatment would cover the whole grid; skip it there.
        var jobOnly = cell.Count > 0 && cell.All(l => l.Custom && l.Jobs.Count > 0);
        var custom = !_isCustom && !jobOnly && cell.Any(l => l.Custom);
        var off = cell.Count > 0 && cell.All(l => !l.Enabled);
        // Every line here is another job's press (a "(WAR/PLD)" style tag, or a
        // job-tagged extra): dim it, since it will never fire on your current
        // job. A cell that is partly yours stays normal.
        var foreign = !string.IsNullOrEmpty(_gridJob) && cell.Count > 0
            && cell.All(l => !l.AppliesTo(_gridJob));

        // Cooldown conflicts tint the cell red; level-sync problems amber
        // (red wins when both apply). Details go in the tooltip.
        string? warn = null;
        string? lvl = null;
        foreach (var l in cell)
        {
            if (_conflicts.TryGetValue(l, out var w)) warn = warn == null ? w : warn + "\n" + w;
            if (_levelWarns.TryGetValue(l, out var v)) lvl = lvl == null ? v : lvl + "\n" + v;
        }
        if (warn != null) ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, WarnCellBg);
        else if (lvl != null) ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, LevelCellBg);

        // Merged cells stack their lines instead of hiding behind a "+1".
        var body = cell.Count > 1 ? string.Join("\n", cell.Select(l => l.Action)) : first;
        var label = (custom ? "* " : "") + (body.Length == 0 ? " " : body) + (off ? "  (off)" : "");

        // Text color: your edits stay orange, disabled lines dim, and with the
        // Colors box ticked the rest is colored by mit type (overlay colors).
        var kindCol = C.SheetColorByType && !custom && !off && first.Length > 0
            ? MitTypes.Color(MitTypes.Classify(first), C) : 0u;
        var pushed = true;
        if (custom) ImGui.PushStyleColor(ImGuiCol.Text, EditedColor);
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
                ? $"Click to add a mit for {_slots[i]} here (that slot only)"
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
                    C.Save();
                    _dirty = true;
                }
                ImGui.EndDisabled();
                if (line.CoverUntil > row.Time && ImGui.MenuItem($"Clear coverage (through {TimeText(line.CoverUntil)})") && !AbortIfStale())
                {
                    PushUndo($"clear {row.Mechanic} coverage");
                    EnsureBacked(i);
                    line.CoverUntil = 0f;
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

    // Other windows edit per-line settings in place (offsets from the fight page
    // or the Mit Tuner, enable toggles, paste-over). The conflict + press-window
    // math bakes cue times in at Rebuild, so those edits must poke the grid or
    // the red cooldown cells go stale.
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
            _plugin.SnapshotPlan(_fight, "before Reset all columns");
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

    // Is this mit's timer free at `t`, given the column's existing plan? Counts
    // same-family uses inside one recast on either side against the charges.
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
    // spreadsheet. Pasted lines are flagged Custom so sheet re-bakes keep them,
    // and target-baked calls the new plan doesn't carry are tombstoned so the
    // zone-in top-up can't resurrect them.
    private void PasteColumn(int dst)
    {
        if (_fight == null || AbortIfStale()) return;
        var src = Array.FindIndex(_slots, s => s.Equals(_copyColSlot, StringComparison.OrdinalIgnoreCase));
        if (src < 0 || src == dst) return;

        PushUndo($"paste {_slots[src]}'s plan into {_slots[dst]}");
        _plugin.SnapshotPlan(_fight, $"before pasting {_slots[src]} into {_slots[dst]}");
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
