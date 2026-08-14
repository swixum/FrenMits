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

// Sheet View: search and replace across a plan.
public partial class SheetViewWindow
{
    // ---- search and replace ----

    private void DrawReplacePopup()
    {
        // Modal, so a stray click outside cannot dismiss the form.
        var stay = true;
        if (!ImGui.BeginPopupModal("##sheetreplace", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;

        PopupHeader("Replace a mit across the sheet", 420f);
        ImGui.SetNextItemWidth(Theme.S(230f));
        ImGui.InputTextWithHint("##rfind", "find (e.g. Vengeance)", ref _replFind, 64);
        ImGui.SetNextItemWidth(Theme.S(230f));
        ImGui.InputTextWithHint("##rwith", "replace with (e.g. Damnation)", ref _replWith, 64);
        ImGui.Checkbox("My Column Only", ref _replMineOnly);

        var find = _replFind.Trim();
        var with = _replWith.Trim();
        var lines = 0;
        var slots = 0;
        if (find.Length > 0)
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_replMineOnly && !IsActiveSlot(i)) continue;
                // The same test the apply uses, so the preview can't lie.
                var n = _slotLines[i].Count(l => WouldReplace(l.Action, find, with) != null);
                if (n > 0) { lines += n; slots++; }
            }
        ImGui.TextDisabled(find.Length == 0 ? "type something to find"
            : lines == 0 ? "no matches"
            : $"will change {lines} line(s) across {slots} slot(s)");
        if (string.IsNullOrWhiteSpace(_replWith) && lines > 0)
            ImGui.TextDisabled("Empty replacement = those calls are DELETED");

        ImGui.BeginDisabled(lines == 0);
        if (ImGui.Button("Replace", Theme.Sz(120f)))
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

        // Bulk edit: undoable and snapshotted to disk.
        PushUndo($"replace \"{find}\"");
        _plugin.Snapshots.Save(_fight, $"before replacing \"{find}\"");

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
                    // Replacing with nothing deletes the call, tombstoned as usual.
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

    // The text after a real replacement, or null when unchanged.
    private static string? WouldReplace(string action, string find, string with)
    {
        var raw = action.Replace(find, with, StringComparison.OrdinalIgnoreCase).Trim();
        if (raw != action) return raw;
        // Typed the way the sheet reads it ("Physis 2"): swap the stored spelling.
        if (Fmt.StoredFragment(action, find) is not { } stored) return null;
        raw = action.Replace(stored, with, StringComparison.OrdinalIgnoreCase).Trim();
        return raw == action ? null : raw;
    }
}
