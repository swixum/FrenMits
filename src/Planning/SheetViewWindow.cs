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

// The whole raid plan as one sheet, in game.
public partial class SheetViewWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    // The ### keeps the window id stable across renames.
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
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10) * Theme.Scale);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(1);
        Theme.PopWindow();
    }

    // ---- state ----

    private FightProfile? _fight;
    private string _phaseFilter = "";              // "" = all phases
    private bool _dirty = true;
    private bool _wasFocused;

    private string[] _slots = Array.Empty<string>();
    private string[] _gridCols = Array.Empty<string>();
    private int[] _gridToSlot = Array.Empty<int>();
    private List<MitLine>[] _slotLines = Array.Empty<List<MitLine>>();
    private bool[] _slotBacked = Array.Empty<bool>(); // list already lives in the fight profile
    private List<Row> _rows = new();
    private List<BakedRow> _bakedRows = new();
    private List<(string Name, float Time)> _phases = new();
    // The per-phase notes footer, for phases that have one.
    private List<(string Name, string Title, string Text)> _phaseNotes = new();
    // Column order: pinned first, into the frozen area.
    private int[] _order = Array.Empty<int>();
    private int _pinnedCount;

    private bool IsPinnedColumn(int i)
        => C.SheetPinnedSlots.Contains(_gridCols[i], StringComparer.OrdinalIgnoreCase);

    // A user-made sheet: not built in, with its own layout.
    private static bool IsCustomSheet(FightProfile f)
        => !Builtin.Has(f.TerritoryId) && (f.CustomSlots.Count > 0 || f.CustomRows.Count > 0);

    // Set each Rebuild, since custom sheets have no bake.
    private bool _isCustom;
    // Lines whose mit repeats before its cooldown is back.
    private readonly Dictionary<MitLine, string> _conflicts = new();
    // Lines whose mit is above the duty's level sync.
    private readonly Dictionary<MitLine, string> _levelWarns = new();
    // Valid press windows, from coverage and squeeze.
    private readonly Dictionary<MitLine, string> _windows = new();
    // Text filter: show only rows whose mechanic or any mit matches.
    private string _filter = "";
    // Action Type filters
    private bool _showPartyMits = true;
    private bool _showPersonalMits = true;
    // Whole-row filter: job-restricted extras (Mantra, Curing Waltz, ...) mixed
    // in at their own time, distinct from a shared party-mit row.
    private bool _showJobExtra = true;
    // Whole-row filter: only rows holding a cooldown clash.
    private bool _clashOnly;
    private int _clashRowCount;

    public bool HasConflict(FightProfile fight, MitLine line, out string reason)
    {
        if (_fight != fight)
        {
            _fight = fight;
            _dirty = true;
        }
        if (_dirty && !Editing) Rebuild();
        if (_conflicts.TryGetValue(line, out var r))
        {
            reason = r;
            return true;
        }
        reason = "";
        return false;
    }

    // Search-and-replace popup state.
    private string _replFind = "";
    private string _replWith = "";
    private bool _replMineOnly;

    // ---- undo ----

    private sealed class PlanSnapshot
    {
        public FightProfile Fight = null!;
        public string Label = "";
        public List<MitLine> Lines = new();
        public Dictionary<string, List<MitLine>> SavedSlots = new();
        public List<DeletedCall> DeletedCalls = new();
        public List<SheetNote> Notes = new();
        public List<CustomRow> CustomRows = new();
        public List<DowntimeWindow> CustomDowntimes = new();
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

    private void PushUndo(string label) => PushUndo(_fight, label);

    // The fight page shares this stack: one plan, two places to edit it, so an
    // edit made on either page is undone from either page.
    public void PushUndo(FightProfile? fight, string label)
    {
        if (fight == null) return;
        _undoStack.Add(new PlanSnapshot
        {
            Fight = fight,
            Label = label,
            Lines = Clone(fight.Lines),
            SavedSlots = Clone(fight.SavedSlots),
            DeletedCalls = Clone(fight.DeletedCalls),
            Notes = Clone(fight.Notes),
            CustomRows = Clone(fight.CustomRows),
            CustomDowntimes = Clone(fight.CustomDowntimes),
            CustomSlots = Clone(fight.CustomSlots),
            SyncPoints = Clone(fight.SyncPoints),
            BossAnchors = Clone(fight.BossAnchors),
            Slot = fight.Slot,
            TimerOffset = fight.TimerOffset,
        });
        if (_undoStack.Count > 20) _undoStack.RemoveAt(0);
    }

    // What this fight's undo would take back, for a page that shows one fight.
    public string? UndoLabelFor(FightProfile fight)
    {
        for (var i = _undoStack.Count - 1; i >= 0; i--)
            if (_undoStack[i].Fight == fight) return _undoStack[i].Label;
        return null;
    }

    // Roll one fight back, skipping any other fight's entries: the fight page
    // shows a single fight, so undoing another's would come out of nowhere.
    public string? UndoFor(FightProfile fight)
    {
        for (var i = _undoStack.Count - 1; i >= 0; i--)
        {
            if (_undoStack[i].Fight != fight) continue;
            var s = _undoStack[i];
            _undoStack.RemoveAt(i);
            if (!C.Fights.Contains(s.Fight)) return null;
            Restore(s);
            return s.Label;
        }
        return null;
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

        // Jumping to another fight's entry resets the filters first.
        var jumped = s.Fight != _fight;
        if (jumped)
        {
            _plugin.Snapshots.Save(s.Fight, "before undo");
            _phaseFilter = "";
            _filter = "";
        }

        Restore(s);
        _fight = s.Fight;
        Flash(jumped ? $"Undid: {s.Label} (in {s.Fight.Name})." : $"Undid: {s.Label}.");
    }

    private void Restore(PlanSnapshot s)
    {
        s.Fight.Lines = s.Lines;
        s.Fight.SavedSlots = s.SavedSlots;
        s.Fight.DeletedCalls = s.DeletedCalls;
        s.Fight.Notes = s.Notes;
        s.Fight.CustomRows = s.CustomRows;
        s.Fight.CustomDowntimes = s.CustomDowntimes;
        s.Fight.CustomSlots = s.CustomSlots;
        s.Fight.SyncPoints = s.SyncPoints;
        s.Fight.BossAnchors = s.BossAnchors;
        s.Fight.Slot = s.Slot;
        s.Fight.TimerOffset = s.TimerOffset;
        // Restore the active-slot alias.
        if (!string.IsNullOrEmpty(s.Slot) && s.Fight.SavedSlots.ContainsKey(s.Slot))
            s.Fight.SavedSlots[s.Slot] = s.Fight.Lines;

        C.Save();
        _dirty = true;
    }

    // Sticky-phase pill state, from the top visible row.
    private float _headerY;
    private int _rowIdxDrawing = -1;
    private int _firstDrawnIdx = -1;
    private int _stickyRowIdx = -1;
    private string _stickyTitle = "";

    // A mechanic instance as the sheet bakes it, unfiltered.
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
        public List<MitLine> RawLines = new();
        public BakedRow? Bake;      // nearest same-mechanic baked instance
        public bool Edited;
        public bool Ghost;          // baked instance deleted from every slot
        public bool JobExtra;       // every line is a job-restricted custom (e.g. Nature's Minne)
        public List<string>?[]? Carry;   // per column: earlier presses whose buffs still cover this row
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

    // Coordinates, not references, since the commit rebuilds rows.
    private (float Time, string Mech, int Slot)? _pendingEdit;

    // The full call editor, opened by double-clicking a cell. Coordinates for
    // the same reason, one frame apart so a just-typed call is in the rows.
    private (float Time, string Mech, int Slot)? _cellEditOpening;
    private (float Time, string Mech, int Slot)? _cellEditAt;
    private MitLine? _cellEditDraft;   // an empty cell's line, held until it has an action
    private bool _cellEditUndoArmed;   // one undo entry per opening, not per keystroke

    private string _flash = "";
    private DateTime _flashAt;
    private void Flash(string msg) { _flash = msg; _flashAt = DateTime.Now; }

    // The hovered row and the note popup's edit buffer.
    private Row? _hoverRow;
    private Row? _hoverLive;
    private Row? _hoverLivePrev;
    private string _noteBuf = "";

    // A tight window, since some mechanics repeat under 10s.
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

    // ---- opening ----

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
        // Prefer fights with a slot picked, since the grid needs one.
        return C.Fights.FirstOrDefault(f => Sheetable(f) && f.TerritoryId == terr && f.Enabled)
            ?? C.Fights.FirstOrDefault(f => Sheetable(f) && f.Id == C.LastSheetFightId)
            ?? C.Fights.FirstOrDefault(f => f.TerritoryId == Builtin.DmuTerritory && !string.IsNullOrEmpty(f.Slot))
            ?? C.Fights.FirstOrDefault(f => Sheetable(f) && !string.IsNullOrEmpty(f.Slot))
            ?? C.Fights.FirstOrDefault(f => f.TerritoryId == Builtin.DmuTerritory)
            ?? C.Fights.FirstOrDefault(Sheetable);
    }

    // ---- drawing ----

    public override void Draw()
    {
        Theme.Accent = C.AccentColor;
        Theme.Scale = Math.Clamp(C.UiScale, 0.8f, 1.6f);
        Theme.PushWidgets();
        using var uiFont = Widgets.PushUiFont(_plugin.Fonts, Theme.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 16f * Theme.Scale);
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
            ImGui.TextUnformatted("No sheets yet");
            ImGui.PushTextWrapPos(Theme.S(460f));
            ImGui.TextDisabled("A sheet is one fight's mit plan, a column per player. Start from a "
                               + "built-in fight, or make your own from a pull or a kill log.");
            ImGui.PopTextWrapPos();
            ImGui.Spacing();
            if (Widgets.AccentButton("New sheet...")) OpenNewSheetPopup();
            ImGui.SameLine(0, Theme.S(8f));
            if (ImGui.Button("Add a built-in fight")) _plugin.ConfigWindow.IsOpen = true;
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip("Opens Fren Mits, where the Ultimate, Savage and Extreme lists live.");
            DrawNewSheetPopup();
            return;
        }
        if (string.IsNullOrEmpty(_fight.Slot))
        {
            DrawFightPicker(); // still allow switching to a fight that HAS a slot
            ImGui.Spacing();
            if (IsCustomSheet(_fight))
            {
                // Custom sheets pick their column right here.
                ImGui.TextWrapped("Pick your column for this sheet; that column becomes the plan your overlay calls.");
                ImGui.Spacing();
                foreach (var s in _fight.CustomSlots)
                {
                    if (ImGui.Button(s)) PickCustomSlot(s);
                    ImGui.SameLine(0, Theme.S(6f));
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

        // Regaining focus re-reads every slot, so edits show up.
        var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (focused && !_wasFocused) _dirty = true;
        _wasFocused = focused;
        if (_dirty && !Editing) Rebuild();

        // A queued edit lands once the grid is rebuilt and idle.
        if (_pendingEdit is { } pe && !Editing && !_dirty)
        {
            _pendingEdit = null;
            var target = _rows.FirstOrDefault(r => !r.Ghost
                && MechEquals(r.Mechanic, pe.Mech) && MathF.Abs(r.Time - pe.Time) < 0.9f
                // Must be visible, or the editor never draws and wedges.
                && (_phaseFilter.Length == 0 || r.Phase == _phaseFilter) && MatchesFilter(r));
            if (target != null && pe.Slot >= 0 && pe.Slot < _slots.Length)
            {
                _editCellRow = target;
                _editCellSlot = pe.Slot;
                _cellBuf = _cellSeed = target.Cells[pe.Slot].Count > 0 ? target.Cells[pe.Slot][0].Action : "";
                _focusPending = true;
            }
        }

        // Ctrl+Z undoes the last sheet edit.
        if (focused && !ImGui.GetIO().WantTextInput
            && ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Z, false))
            Undo();

        DrawToolbar();
        // The delete popup can remove the sheet mid-frame.
        if (_fight == null) return;
        ImGui.Spacing();
        DrawGrid();
        DrawNotesPanel();
        DrawFooter();
    }

    // ---- sheet notes ----

    private float NotesBodyHeight() => Math.Clamp(C.SheetNotesHeight, 60f, 600f);
    private const float NotesGripHeight = 6f;

    // Space the notes panel takes, so the table can shrink.
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

        // Drag the top edge for more notes or more grid.
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
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip("Notes from the bottom of each phase tab.");
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

    // Fight picker (only when there's a choice to make).
    private static readonly string[] PickerCategories = { "Ultimate", "Savage", "Extreme", "Raids", "Other" };

    private void DrawFightPicker()
    {
        var fights = C.Fights.Where(Sheetable).ToList();
        if (fights.Count == 0) return;

        ImGui.SetNextItemWidth(Theme.S(230f));
        // Sized to the longest name plus its tag, and height-capped.
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
                    // Your slot for that fight, right-aligned.
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
            // OpenPopup can't run inside the combo, so flag it.
            if (ImGui.Selectable("+ New sheet...")) openNew = true;
            ImGui.EndCombo();
        }
        if (openNew) OpenNewSheetPopup();
        DrawNewSheetPopup();
        ImGui.SameLine();
    }

}
