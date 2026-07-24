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

// Settings: the Fights list - each category's page, reordering, and loading or
// resetting a built-in fight's baked plan.
public partial class ConfigWindow
{
    // ---- Fights page ------------------------------------------------------

    // Jump from Sheet View straight to a fight's page (per-line options and
    // import tools live there).
    public void OpenFightPage(FightProfile fight)
    {
        IsOpen = true;
        BringToFront();
        _nav = NavKind.Fights;
        _navCategory = CategoryOf(fight);
        _expandFightId = fight.Id;
    }

    // The expansion a fight's zone belongs to, from the game data
    // (TerritoryType.ExVersion), cached per territory since this runs inside the
    // per-frame sort.
    private static readonly Dictionary<uint, uint> ExCache = new();

    private static uint ExpansionOf(FightProfile f)
    {
        if (ExCache.TryGetValue(f.TerritoryId, out var hit)) return hit;
        uint ex;
        try
        {
            var t = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRowOrDefault(f.TerritoryId);
            ex = t?.ExVersion.RowId ?? uint.MaxValue;
        }
        catch { ex = uint.MaxValue; }
        ExCache[f.TerritoryId] = ex;
        return ex;
    }

    private static string ExpansionName(uint ex)
    {
        try
        {
            var name = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ExVersion>()?.GetRowOrDefault(ex)?.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) return name!;
        }
        catch { /* fall through */ }
        return "Other";
    }

    // Quick filter for the fights pages, shared across categories so a search
    // follows you between tabs.
    private string _fightFilter = "";

    private void DrawFightCategoryPage(string category)
    {
        var fights = C.Fights.Where(f => CategoryOf(f) == category).ToList();

        SeparatorText($"{category}: {fights.Count} fight{(fights.Count == 1 ? "" : "s")}");
        DrawCategoryToolbar(category);
        // Type-to-narrow, matching Sheet View's duty search - with many custom
        // sheets the list outgrows scrolling fast.
        ImGui.SetNextItemWidth(240f);
        ImGui.InputTextWithHint("##fightfilter", "Search fights...", ref _fightFilter, 64);
        var filter = _fightFilter.Trim();
        if (filter.Length > 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear##fightfilter")) { _fightFilter = ""; filter = ""; }
        }
        ImGui.Spacing();

        if (filter.Length > 0)
            fights = fights.Where(f => f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        if (fights.Count == 0)
        {
            ImGui.TextDisabled(filter.Length > 0
                ? "No fights here match the search."
                : "No fights here yet. Add one above, or load a preset.");
            return;
        }

        // Group by expansion, newest first (unknown zones sink to the bottom).
        fights = fights
            .OrderByDescending(f => ExpansionOf(f) == uint.MaxValue ? -1L : ExpansionOf(f))
            .ToList();
        var lastEx = uint.MaxValue - 1; // sentinel that matches no real value

        FightProfile? toDelete = null;
        for (int i = 0; i < fights.Count; i++)
        {
            var fight = fights[i];
            var ex = ExpansionOf(fight);
            if (ex != lastEx)
            {
                lastEx = ex;
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.55f, 0.75f, 0.98f, 1f),
                    ex == uint.MaxValue ? "Other" : ExpansionName(ex));
                ImGui.Spacing();
            }

            ImGui.PushID(fight.Id);

            // Drag handle to reorder fights within their expansion group; the list
            // is a stable sort of C.Fights, so swapping two same-group fights in
            // C.Fights is all it takes and the display and save follow.
            DrawReorderGrip(fights, i);
            ImGui.SameLine();

            // Enable toggle + an expandable dropdown per fight.
            var enabled = fight.Enabled;
            if (GreenCheckbox("##en", ref enabled)) { fight.Enabled = enabled; C.Save(); }
            ImGui.SameLine();

            if (fight.Id == _expandFightId) { ImGui.SetNextItemOpen(true); _expandFightId = ""; }
            // Gold star after the name = official (ships with the plugin, baked
            // from the community sheet), drawn in the icon font since the text font
            // has no star.
            var official = Builtin.Has(fight.TerritoryId);
            var headerStartX = ImGui.GetCursorPosX();
            var headerLabel = fight.Name;
            var open = ImGui.CollapsingHeader($"{headerLabel}###fh-{fight.Id}");
            // The star tooltip and the sheet button are drawn ON TOP of this
            // header row; without allow-overlap the header claims the mouse
            // first and they can never be hovered or clicked.
            ImGui.SetItemAllowOverlap();
            // A framed tree node indents its label one extra FramePadding.X
            // beyond GetTreeNodeToLabelSpacing().
            ImGui.SameLine(headerStartX + ImGui.GetTreeNodeToLabelSpacing()
                + ImGui.GetStyle().FramePadding.X + ImGui.CalcTextSize(headerLabel).X + 8f);
            ImGui.AlignTextToFramePadding();
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            {
                if (!official) ImGui.SetWindowFontScale(0.8f);
                ImGui.TextColored(official ? GoldStar : UserBlue,
                    (official ? FontAwesomeIcon.Star : FontAwesomeIcon.User).ToIconString());
                if (!official) ImGui.SetWindowFontScale(1f);
            }
            // The tooltip lives on the symbol, not the whole header: sweeping
            // the fight list stays silent, hovering the symbol explains it.
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(official ? "Official sheet." : "User created.");
            // Quick jump into Sheet View for any fight that has a sheet.
            if (Builtin.Has(fight.TerritoryId) || fight.CustomSlots.Count > 0)
            {
                ImGui.SameLine(ImGui.GetContentRegionMax().X - 28f);
                using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                {
                    if (ImGui.SmallButton(FontAwesomeIcon.Table.ToIconString() + "##opensheet"))
                        _plugin.SheetViewWindow.Open(fight);
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open in Sheet View");
            }

            if (open)
            {
                ImGui.Indent(10f);
                _selectedFight = C.Fights.IndexOf(fight); // drives the per-line options popup
                if (!DrawFightEditor(fight))
                {
                    toDelete = fight;
                }
                else
                {
                    if (Builtin.Has(fight.TerritoryId)) DrawBuiltinLoad(fight);
                    else if (fight.CustomSlots.Count > 0) DrawCustomColumnRow(fight);
                    DrawFightOffsetRow(fight);
                    DrawPracticeRow(fight);
                    // Optional add-ons live behind one fold, so an expanded fight
                    // reads as offset + line table by default.
                    var job = _plugin.ActiveJobAbbreviation();
                    var hasExtras = PotionTimings.BossSlug(fight.TerritoryId) != null
                        || (fight.CustomSlots.Count > 0 && fight.CustomRows.Count > 0)
                        || (!string.IsNullOrEmpty(job) && JobExtras.AllFor(fight, job).Count > 0)
                        || (TankMits.Has(fight.TerritoryId) && IsTankSlot(fight.Slot));
                    if (hasExtras && Section("Extras: potions, job mits, tank busters", false))
                    {
                        DrawPotionsSection(fight);
                        DrawJobExtrasSection(fight);
                        DrawTankSection(fight);
                    }
                    ImGui.Separator();
                    DrawLineTable(fight);
                    ImGui.Spacing();
                    DrawImportSection(fight);
                    DrawAdvancedFightSettings(fight);
                }
                ImGui.Unindent(10f);
            }

            ImGui.PopID();
        }

        if (toDelete != null) { C.Fights.Remove(toDelete); C.Save(); }
    }

    // A small grip you drag up/down to reorder a fight within its expansion group;
    // only same-group neighbours swap, since crossing a group line would just be
    // snapped back by the group header sort.
    private void DrawReorderGrip(List<FightProfile> shown, int i)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, 0u);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0x22FFFFFFu);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0x33FFFFFFu);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Muted);
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            ImGui.Button(FontAwesomeIcon.GripVertical.ToIconString() + "##grip",
                new Vector2(18f, ImGui.GetFrameHeight()));
        ImGui.PopStyleColor(4);

        var held = ImGui.IsItemActive();
        if (held || ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
        if (ImGui.IsItemHovered() && !held) ImGui.SetTooltip("Drag to reorder");

        if (!held) return;
        var dy = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left).Y;
        // Wait for a real drag past half a row before swapping, so a click or
        // tiny wobble on the grip never nudges the order.
        if (MathF.Abs(dy) < ImGui.GetFrameHeightWithSpacing() * 0.5f) return;

        var j = i + (dy < 0 ? -1 : 1);
        if (j < 0 || j >= shown.Count) return;
        if (ExpansionOf(shown[j]) != ExpansionOf(shown[i])) return;

        var a = C.Fights.IndexOf(shown[i]);
        var b = C.Fights.IndexOf(shown[j]);
        if (a < 0 || b < 0) return;
        (C.Fights[a], C.Fights[b]) = (C.Fights[b], C.Fights[a]);
        (shown[i], shown[j]) = (shown[j], shown[i]); // keep this frame's list in step
        ImGui.ResetMouseDragDelta();
        C.Save();
    }

    // One menu instead of a button row that grows every tier: blank fight,
    // paste a code, and any not-yet-added official sheets for this category.
    private void DrawCategoryToolbar(string category)
    {
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, "Add fight"))
            ImGui.OpenPopup("##addfight");
        if (!ImGui.BeginPopup("##addfight")) return;

        // A blank fight in an official-sheet zone would be a locked, never-firing
        // duplicate of the built-in (ActiveFight takes the first match), so the
        // item goes disabled there.
        var zone = Service.ClientState.TerritoryType;
        if (Builtin.Has(zone))
            ImGui.MenuItem("New blank fight (this zone has an official sheet)", false);
        else if (ImGui.MenuItem("New blank fight (this zone)"))
            AddFight(new FightProfile
            {
                Name = "New fight",
                TerritoryId = zone,
                Category = category,
            });
        if (ImGui.MenuItem("Paste fight code from clipboard")) ImportFightFromClipboard();

        var presets = Builtin.Fights
            .Where(f => f.Category == category && C.Fights.All(x => x.TerritoryId != f.Territory))
            .ToList();
        if (presets.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextDisabled("Official sheets");
            foreach (var (territory, name, cat) in presets)
                if (ImGui.MenuItem(name))
                    AddFight(new FightProfile { Name = name, TerritoryId = territory, Category = cat });
        }
        ImGui.EndPopup();
    }

    // Adds a fight and auto-expands its dropdown.
    private void AddFight(FightProfile fight)
    {
        C.Fights.Add(fight);
        _selectedFight = C.Fights.Count - 1;
        _expandFightId = fight.Id;
        C.Save();
    }

    private int _builtinSlot;

    // "PhysicalRanged" -> "Phys Ranged" for the role headers.
    private static string RoleLabel(JobRole role) => role switch
    {
        JobRole.PhysicalRanged => "Phys Ranged",
        _ => role.ToString(),
    };

    // Friendly names for the raw sheet-slot codes shown in the slot picker.
    private static string SlotLabel(string code) => SlotNames.Canon(code) switch
    {
        "M1" => "Melee 1",
        "M2" => "Melee 2",
        "R1" => "Phys Ranged",
        "R2" => "Caster",
        "T1" => "Main Tank",
        "T2" => "Off Tank",
        var c => c,
    };

    private string _builtinMsg = "";
    private DateTime _builtinMsgAt = DateTime.MinValue;

    // True if your current lines differ from a fresh bake of this slot (added,
    // removed, or a changed action) — i.e. a Replace would throw away your work.
    private bool HasBuiltinEdits(FightProfile fight, string slot)
    {
        if (fight.Lines.Count == 0) return false;
        var baked = Builtin.BuildLines(fight.TerritoryId, slot);
        if (fight.Lines.Count != baked.Count) return true;
        foreach (var b in baked)
        {
            var m = fight.Lines.FirstOrDefault(l => Builtin.SameCall(l, b));
            if (m == null) return true;
            if (!string.Equals((m.Action ?? "").Trim(), (b.Action ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Switch the active slot and load only that slot's mits (keeping its own edits).
    private void SelectBuiltinSlot(FightProfile fight, string slot)
    {
        Builtin.ApplySlot(fight, slot);
        C.DmuSlot = fight.Slot;
        C.Save();
        FlashBuiltin($"Loaded {SlotLabel(fight.Slot)} mits.");
    }

    private void ResetBuiltinSlot(FightProfile fight, string slot)
    {
        Builtin.ResetSlot(fight, slot);
        C.DmuSlot = fight.Slot;
        C.Save();
        FlashBuiltin($"Reset {SlotLabel(slot)} to the baked sheet.");
    }

    private void FlashBuiltin(string msg) { _builtinMsg = msg; _builtinMsgAt = DateTime.Now; }

    private void DrawBuiltinLoad(FightProfile fight)
    {
        var slots = Builtin.Slots(fight.TerritoryId);

        // Reflect the fight's active slot in the picker, falling back to the first
        // slot when this fight has no valid slot yet (fresh profile / removed legacy
        // slot) rather than whatever index the LAST fight's picker used, which would
        // bake this fight onto a stale slot.
        var savedIdx = Array.IndexOf(slots, fight.Slot);
        _builtinSlot = savedIdx >= 0 ? savedIdx : 0;
        _builtinSlot = Math.Clamp(_builtinSlot, 0, slots.Length - 1);

        var slotLabels = slots.Select(SlotLabel).ToArray();
        ImGui.SetNextItemWidth(170f);
        if (ImGui.Combo("Your slot", ref _builtinSlot, slotLabels, slotLabels.Length))
            SelectBuiltinSlot(fight, slots[_builtinSlot]);  // load that slot now
        Tip("Pick your slot and its mits load automatically (and again when you enter the zone). Each slot keeps its own edits; tanks pick a tank slot, healers their job, DPS their role slot.");
        var slot = slots[_builtinSlot];

        ImGui.SameLine();
        if (ImGui.SmallButton("Reset to sheet"))
        {
            if (HasBuiltinEdits(fight, slot)) ImGui.OpenPopup("##confirm-replace");
            else ResetBuiltinSlot(fight, slot);
        }
        Tip("Reloads this slot from the baked sheet, discarding only this slot's edits.");

        ImGui.SameLine();
        if (ImGui.SmallButton("Reset all columns")) ImGui.OpenPopup("##confirm-resetall");
        Tip("Reloads EVERY column from the baked sheet: all slots' edits and deletions go, "
            + "including added potion, job and tank lines. A snapshot is saved first, so "
            + "Sheet View > Plan > History can restore the old plan.");

        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudYellow, _builtinMsg);
        }

        DrawReplaceConfirm(fight, slot);
        DrawResetAllConfirm(fight, slot);
    }

    // Full reset across every column, for when single-slot resets aren't enough
    // (stale edits living in OTHER slots' preview columns); snapshot-first and
    // confirmed, so it's safe to reach for.
    private void DrawResetAllConfirm(FightProfile fight, string slot)
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##confirm-resetall", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        ImGui.TextUnformatted("Reset every column to the baked sheet?");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "All slots' edits and deletions go, including added potion, job and tank lines.");
        ImGui.TextDisabled("A snapshot is saved first; Sheet View > Plan > History restores it.");
        ImGui.Spacing();

        if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF1E40C0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF2046D0);
        if (ImGui.Button("Reset every column", new Vector2(180, 0)))
        {
            _plugin.Snapshots.Save(fight, "before Reset all columns");
            fight.SavedSlots.Clear();
            fight.DeletedCalls.Clear();
            Builtin.ResetSlot(fight, slot);
            C.DmuSlot = fight.Slot;
            C.Save();
            FlashBuiltin("Every column reset to the baked sheet. History restores the old plan.");
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndPopup();
    }

    private void DrawReplaceConfirm(FightProfile fight, string slot)
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##confirm-replace", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        ImGui.TextUnformatted($"You've customized the {SlotLabel(slot)} slot.");
        ImGui.TextDisabled("Resetting will discard this slot's changes and load the baked sheet fresh.");
        ImGui.Separator();

        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF1E40C0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF2046D0);
        if (ImGui.Button("Reset and lose my edits", new Vector2(220, 0)))
        {
            ResetBuiltinSlot(fight, slot);
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(100, 0)))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    // The fight-wide offset, up top where it's findable, shifts EVERY call; the
    // per-line ±s column below handles individual calls.
    private void DrawFightOffsetRow(FightProfile fight)
    {
        var offset = fight.TimerOffset;
        ImGui.SetNextItemWidth(110f);
        if (ImGui.InputFloat("Timer offset (s)", ref offset, 0.1f, 1f, "%.1f"))
        {
            fight.TimerOffset = Math.Clamp(offset, -30f, 30f);
            C.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("+ fires every call earlier, - later. Survives resync.");
        HelpMarker("Shifts when this fight's calls fire: +10 makes every call come 10s sooner, "
                   + "even with resync on. For one call only, use the ±s column in the line table. "
                   + "Heads up: a big + shift can swallow calls timed inside the first seconds of a "
                   + "pull. The timer auto-starts on combat and resets on a wipe / duty end.");
    }

    // Set when a zone edit is refused (official-sheet zone); shows a warning
    // line under the territory controls for a few seconds.
    private double _zoneRejectUntil;

    // The canonical profile for a built-in zone is the first in the list, like
    // ActiveFight resolves; a stray DUPLICATE on a built-in zone (old configs
    // could produce one) stays a normal editable fight so it can be deleted.
    private bool IsOfficial(FightProfile f)
        => Builtin.Has(f.TerritoryId)
           && ReferenceEquals(C.Fights.FirstOrDefault(x => x.TerritoryId == f.TerritoryId), f);
}
