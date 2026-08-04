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

// Sheet View: making a brand new sheet for a duty that has none.
public partial class SheetViewWindow
{
    // ---- new custom sheet ----

    private string _newName = "";
    private int _newTemplate;
    private string _newSlotsBuf = "";
    private int _newMySlot;

    private static readonly string[] SlotTemplates =
    {
        "Full party (MT OT H1 H2 M1 M2 R1 R2)",
        "Full party, job healers (MT OT WHM AST SCH SGE M1 M2 R1 R2)",
        "Light party (T H M1 M2)",
        "Custom columns",
    };

    private string[] TemplateSlots() => _newTemplate switch
    {
        0 => new[] { "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2" },
        // The official layout, where healer columns are job columns.
        1 => new[] { "MT", "OT", "WHM", "AST", "SCH", "SGE", "M1", "M2", "R1", "R2" },
        2 => new[] { "T", "H", "M1", "M2" },
        // Hand-typed columns still run through the standard names.
        _ => _newSlotsBuf.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                         .Select(SlotNames.Canon)
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
        // Prefilled with the zone you're in, but editable.
        var here = (uint)Service.ClientState.TerritoryType;
        _newZoneBuf = here != 0 ? here.ToString() : "";
        ImGui.OpenPopup("##newsheet");
    }

    private string _newZoneBuf = "";
    private int _newCat = 2;
    private bool _newCatTouched;

    // Where the sheet files in the sidebar.
    private static readonly string[] NewSheetCategories = { "Ultimate", "Savage", "Extreme" };

    // Best guess from the duty name; your pick wins.
    private static int GuessCategory(string dutyName)
    {
        if (dutyName.Contains("(Ultimate)", StringComparison.OrdinalIgnoreCase)) return 0;
        if (dutyName.Contains("(Savage)", StringComparison.OrdinalIgnoreCase)) return 1;
        if (dutyName.Contains("(Extreme)", StringComparison.OrdinalIgnoreCase)
            || dutyName.StartsWith("The Minstrel's Ballad", StringComparison.OrdinalIgnoreCase)) return 2;
        return 2;
    }

    // True when the id is a real TerritoryType row.
    private static bool ZoneExists(uint terr)
    {
        try { return Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.HasRow(terr) == true; }
        catch { return false; }
    }

    // Friendly label for a zone id, or "" when unknown.
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
                }
        }
        catch { /* sheet hiccup: search just returns nothing */ }
        // Over the cap, keep the newest, or old content buries the tier.
        if (found.Count > max) found.RemoveRange(0, found.Count - max);
        return found;
    }


    // Duties whose boss has this id, as zone and duty name.
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
        // Modal, so a stray click outside cannot dismiss the form.
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
            ImGui.SetTooltip("Which sidebar group the sheet files under.");

        // The zone the sheet binds to, by id or by duty name.
        ImGui.SetNextItemWidth(250f);
        ImGui.InputTextWithHint("zone##nszone", "zone id, boss id, or duty name", ref _newZoneBuf, 64);

        var buf = _newZoneBuf.Trim();
        uint terr = 0;
        var zoneBlocked = false;
        if (buf.Length > 0 && !uint.TryParse(buf, out terr))
        {
            // Name search: picking a match drops its zone id into the field.
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
            // A zone-less sheet can never fire, and re-imports would stack.
            ImGui.TextColored(ImGuiColors.DalamudYellow,
                "You're not in a duty. Type the duty's name or zone id above.");
            zoneBlocked = true;
        }
        else if (!ZoneExists(terr))
        {
            // Not a zone: maybe it is a boss id.
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
        // A fight for this zone exists, so upgrade instead of adding.
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
}
