using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

namespace FrenMits.Host;

// Settings: the Fights list and its per-fight controls.
public partial class ConfigWindow
{
    // ---- Fights page ----

    // Jump from Sheet View straight to a fight's page.
    public void OpenFightPage(FightProfile fight)
    {
        IsOpen = true;
        BringToFront();
        _nav = NavKind.Fights;
        _navCategory = CategoryOf(fight);
        _expandFightId = fight.Id;
    }

    // The expansion a fight's zone belongs to, cached per territory.
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

    // Quick filter, shared so a search follows you between tabs.
    private string _fightFilter = "";

    private void DrawFightCategoryPage(string category)
    {
        var fights = C.Fights.Where(f => CategoryOf(f) == category).ToList();

        SeparatorText($"{category}: {fights.Count} fight{(fights.Count == 1 ? "" : "s")}");
        DrawCategoryToolbar(category);
        // Type-to-narrow, since the list outgrows scrolling fast.
        ImGui.SetNextItemWidth(Theme.S(240f));
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

        // Group by expansion, newest first.
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

            // Drag handle to reorder fights within their expansion group.
            DrawReorderGrip(fights, i);
            ImGui.SameLine();

            // Enable toggle + an expandable dropdown per fight.
            var enabled = fight.Enabled;
            if (GreenCheckbox("##en", ref enabled)) { fight.Enabled = enabled; C.Save(); }
            ImGui.SameLine();

            if (fight.Id == _expandFightId) { ImGui.SetNextItemOpen(true); _expandFightId = ""; }
            // Gold star after the name = official, drawn in the icon font.
            var official = Builtin.Has(fight.TerritoryId);
            var headerStartX = ImGui.GetCursorPosX();

            // Everything that shares this line, measured before the name is
            // drawn: the name is then elided to what is left, so a long one
            // cannot end up underneath the star, the slot tag or the button.
            var hasSheet = official || fight.CustomSlots.Count > 0;
            var slotTag = !hasSheet ? "" : string.IsNullOrEmpty(fight.Slot) ? "no slot" : fight.Slot;
            var btnW = hasSheet ? Theme.S(28f) : 0f;
            var tagW = slotTag.Length > 0 ? ImGui.CalcTextSize(slotTag).X + Theme.S(14f) : 0f;
            var starW = ImGui.GetTextLineHeight() + Theme.S(10f);
            var nameRoom = ImGui.GetContentRegionMax().X - headerStartX
                           - ImGui.GetTreeNodeToLabelSpacing() - ImGui.GetStyle().FramePadding.X
                           - starW - tagW - btnW;

            // The ### id ignores the label, so eliding it changes nothing else.
            var headerLabel = Widgets.Elide(fight.Name, nameRoom);
            var open = ImGui.CollapsingHeader($"{headerLabel}###fh-{fight.Id}");
            // Without allow-overlap the header would swallow the star.
            ImGui.SetItemAllowOverlap();
            var headMin = ImGui.GetItemRectMin();
            var headMax = ImGui.GetItemRectMax();
            // The open fight gets the sidebar's accent bar, so selection reads the same everywhere.
            if (open)
                ImGui.GetWindowDrawList().AddRectFilled(
                    new Vector2(headMin.X, headMin.Y + 2f), new Vector2(headMin.X + Theme.S(3f), headMax.Y - 2f),
                    Theme.Accent, 2f);
            // A framed tree node indents its label one extra padding.
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
            // The tooltip lives on the symbol, so the list stays silent.
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip(official ? "Official sheet." : "User created.");

            // Measured off the star itself, so the tag never lands on it. The
            // cursor is back at the line start by now and would not have said.
            var starRight = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;

            // Your slot, right-aligned: the one thing that decides whether calls fire.
            if (slotTag.Length > 0)
            {
                ImGui.SameLine(MathF.Max(starRight + Theme.S(8f),
                    ImGui.GetContentRegionMax().X - btnW - tagW));
                ImGui.AlignTextToFramePadding();
                // Role tint, the same one the sheet grid gives that column.
                ImGui.TextColored(Theme.V(string.IsNullOrEmpty(fight.Slot)
                    ? Theme.Warn
                    : Theme.RoleColor(fight.Slot)), slotTag);
                if (Widgets.HoveredDelayed())
                    ImGui.SetTooltip(string.IsNullOrEmpty(fight.Slot)
                        ? "No slot picked yet, so nothing is called for this fight."
                        : $"Your column for this fight is {fight.Slot}.");
            }

            // Quick jump into Sheet View for any fight that has a sheet.
            if (hasSheet)
            {
                ImGui.SameLine(ImGui.GetContentRegionMax().X - btnW);
                using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                {
                    if (ImGui.SmallButton(FontAwesomeIcon.Table.ToIconString() + "##opensheet"))
                        _plugin.SheetViewWindow.Open(fight);
                }
                if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Open in Sheet View");
            }

            if (open)
            {
                ImGui.Indent(Theme.S(10f));
                _selectedFight = C.Fights.IndexOf(fight); // drives the per-line options popup
                if (!DrawFightEditor(fight))
                {
                    toDelete = fight;
                }
                else
                {
                    // Setup row carries the offset, so no separate offset line.
                    if (Builtin.Has(fight.TerritoryId)) DrawBuiltinLoad(fight);
                    else if (fight.CustomSlots.Count > 0) DrawCustomColumnRow(fight);
                    else DrawOffsetRow(fight);
                    DrawPracticeRow(fight);
                    // Add-ons live behind one fold, so a fight reads simply.
                    var job = _plugin.GetActiveJobAbbr(fight);
                    // Your job's kit extras (Mantra, Curing Waltz, ...) ride into
                    // the line list on their own, same as a baked sheet call;
                    // idempotent, so this is a no-op once they're already there.
                    if (!string.IsNullOrEmpty(fight.Slot) && JobExtras.EnsureAutoLines(fight, job))
                    {
                        C.Save();
                        _plugin.SheetViewWindow.MarkPlanDirty();
                    }
                    // Potions and personal timers are a row each, so they sit in
                    // the list with everything else. Job extras keeps its card:
                    // it is the only one with more than a single control.
                    DrawPotionsSection(fight);
                    DrawPersonalTimersSection(fight);
                    DrawJobExtrasSection(fight);
                    ImGui.Separator();
                    
                    if (string.IsNullOrEmpty(fight.Slot))
                    {
                        ImGui.Spacing();
                        ImGui.TextColored(ImGuiColors.DalamudYellow, "Please select your slot above to view the mitigations timeline.");
                        ImGui.Spacing();
                    }
                    else
                    {
                        DrawLineTable(fight);
                        ImGui.Spacing();
                    }
                    
                    DrawImportSection(fight);
                    DrawAdvancedFightSettings(fight);
                }
                ImGui.Unindent(Theme.S(10f));
            }

            ImGui.PopID();
        }

        if (toDelete != null) { C.Fights.Remove(toDelete); C.Save(); }
    }

    // A grip you drag to reorder a fight in its group.
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
        // Wait for a real drag, so a wobble never nudges the order.
        if (MathF.Abs(dy) < ImGui.GetFrameHeightWithSpacing() * 0.5f) return;

        var j = i + (dy < 0 ? -1 : 1);
        if (j < 0 || j >= shown.Count) return;
        if (ExpansionOf(shown[j]) != ExpansionOf(shown[i])) return;

        var a = C.Fights.IndexOf(shown[i]);
        var b = C.Fights.IndexOf(shown[j]);
        if (a < 0 || b < 0) return;
        (C.Fights[a], C.Fights[b]) = (C.Fights[b], C.Fights[a]);
        ImGui.ResetMouseDragDelta();
        C.Save();
    }

    // One menu, since a button row grows every tier.
    private void DrawCategoryToolbar(string category)
    {
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, "Add fight"))
            ImGui.OpenPopup("##addfight");
        if (!ImGui.BeginPopup("##addfight")) return;

        // A blank fight in an official zone would be a locked duplicate.
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
            // Builtin.Fights is already newest first, so just head each run.
            var shown = "";
            foreach (var (territory, name, cat, expansion) in presets)
            {
                if (expansion != shown)
                {
                    shown = expansion;
                    ImGui.TextDisabled($"  {expansion}");
                }
                if (ImGui.MenuItem(name))
                    AddFight(new FightProfile { Name = name, TerritoryId = territory, Category = cat });
            }
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

    // Friendly names for the raw slot codes in the picker.
    private static string SlotLabel(string code) => SlotNames.Canon(code);

    private string _builtinMsg = "";
    private DateTime _builtinMsgAt = DateTime.MinValue;

    // True when your lines differ from a fresh bake of this slot.
    private bool HasBuiltinEdits(FightProfile fight, string slot)
    {
        if (fight.Lines.Count == 0) return false;
        var baked = Builtin.BuildLines(fight.TerritoryId, slot);
        if (fight.Lines.Count != baked.Count) return true;
        foreach (var b in baked)
        {
            // A moment can hold several calls, so pair on the call itself:
            // pairing by row picked whichever came first and then read the
            // action mismatch as an edit the user never made.
            if (!fight.Lines.Any(l => Builtin.SamePress(l, b))) return true;
        }
        return false;
    }

    // Switch the active slot and load only its mits.
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

        RowLabel("Slot");
        var useSetup = C.UseSetup;
        if (GreenCheckbox("Auto##usesetup", ref useSetup))
        {
            C.UseSetup = useSetup;
            C.Save();
        }
        Tip("Pick your slot from your Job and Role Preferences. The header shows which one you got.");

        string activeSlot;
        if (C.UseSetup)
        {
            activeSlot = Builtin.DefaultSlotForJob(fight.TerritoryId, _plugin.ActiveJobAbbreviation(), C.SlotPrefs);
            if (!string.IsNullOrEmpty(activeSlot) && activeSlot != fight.Slot)
                SelectBuiltinSlot(fight, activeSlot);
        }
        else
        {
            activeSlot = fight.Slot;
        }

        // Manual: the job being planned as, then the seat. Auto needs neither,
        // since the header already shows the slot it landed on.
        if (string.IsNullOrEmpty(activeSlot) || !C.UseSetup)
        {
            if (!C.UseSetup)
            {
                var simJobIdx = Math.Max(0, Array.IndexOf(Jobs.Abbreviations, fight.SimulatedJob));
                if (string.IsNullOrEmpty(fight.SimulatedJob) && !string.IsNullOrEmpty(_plugin.ActiveJobAbbreviation()))
                    simJobIdx = Math.Max(0, Array.IndexOf(Jobs.Abbreviations, _plugin.ActiveJobAbbreviation()));

                ImGui.SameLine(0, Theme.S(8f));
                ImGui.SetNextItemWidth(Theme.S(65f));
                if (ImGui.Combo("##simjob", ref simJobIdx, Jobs.Abbreviations, Jobs.Abbreviations.Length))
                {
                    fight.SimulatedJob = Jobs.Abbreviations[simJobIdx];
                    C.Save();
                }
                Tip("Plan as this job instead of your current one.");
            }

            // Show the fight's active slot, falling back to the first.
            var savedIdx = Array.IndexOf(slots, fight.Slot);
            _builtinSlot = savedIdx >= 0 ? savedIdx : 0;
            _builtinSlot = Math.Clamp(_builtinSlot, 0, slots.Length - 1);

            var slotLabels = slots.Select(SlotLabel).ToArray();
            ImGui.SameLine(0, Theme.S(8f));
            ImGui.SetNextItemWidth(Theme.S(150f));
            if (ImGui.Combo("##yourslot", ref _builtinSlot, slotLabels, slotLabels.Length))
                SelectBuiltinSlot(fight, slots[_builtinSlot]);  // load that slot now
            Tip("Your seat. Each slot keeps its own edits.");
            activeSlot = slots.Length > 0 ? slots[_builtinSlot] : "";
        }

        DrawOffsetInline(fight);
        DrawResetOverrides(fight, activeSlot);
        DrawResetAllConfirm(fight, activeSlot);
    }

    // Only worth offering once something is actually overridden.
    private static bool HasOverrides(FightProfile fight)
        => fight.SavedSlots.Count > 0 || fight.DeletedCalls.Count > 0;

    // Right-aligned on the setup row, and absent on a clean fight.
    private void DrawResetOverrides(FightProfile fight, string activeSlot)
    {
        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine(0, Theme.S(10f));
            ImGui.TextColored(ImGuiColors.DalamudYellow, _builtinMsg);
        }
        if (!HasOverrides(fight)) return;

        var w = ImGui.CalcTextSize("Reset").X + ImGui.GetStyle().FramePadding.X * 2f;
        var end = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
        ImGui.SameLine(MathF.Max(end + Theme.S(10f), ImGui.GetContentRegionMax().X - w));
        if (ImGui.SmallButton("Reset")) ImGui.OpenPopup("##confirm-resetall");
        Tip("Reload every column from the sheet. A snapshot is saved first.");
    }

    // Full reset across every column; snapshot-first and confirmed.
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

        if (ImGui.Button("Cancel", Theme.Sz(120f))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        if (Widgets.DangerButton("Reset every column", Theme.Sz(180f)))
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

        if (Widgets.DangerButton("Reset and lose my edits", Theme.Sz(220f)))
        {
            ResetBuiltinSlot(fight, slot);
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel", Theme.Sz(100f)))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    // The fight-wide offset, which shifts every call. Rides on the setup row:
    // the label leads, and InputFloat's own trailing label is suppressed.
    private void DrawOffsetInline(FightProfile fight)
    {
        ImGui.SameLine(0, Theme.S(18f));
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Offset");
        ImGui.SameLine(0, Theme.S(8f));
        DrawOffsetControl(fight);
    }

    // Its own row, for a fight with no slots to share one with.
    private void DrawOffsetRow(FightProfile fight)
    {
        RowLabel("Offset");
        DrawOffsetControl(fight);
    }

    private void DrawOffsetControl(FightProfile fight)
    {
        var offset = fight.TimerOffset;
        ImGui.SetNextItemWidth(Theme.S(104f));
        if (ImGui.InputFloat("##offset", ref offset, 0.1f, 1f, "%.1f"))
        {
            fight.TimerOffset = Math.Clamp(offset, -30f, 30f);
            C.Save();
        }
        HelpMarker("Shifts when this fight's calls fire: +10 makes every call come 10s sooner, "
                   + "even with resync on. Minus is later. For one call only, use the ±s column in "
                   + "the line table. Heads up: a big + shift can swallow calls timed inside the "
                   + "first seconds of a pull. The timer auto-starts on combat and resets on a wipe.");
    }


    // Set when a zone edit is refused, to warn for a few seconds.
    private double _zoneRejectUntil;

    // The canonical profile for a built-in zone is the first.
    private bool IsOfficial(FightProfile f)
        => Builtin.Has(f.TerritoryId)
           && ReferenceEquals(C.Fights.FirstOrDefault(x => x.TerritoryId == f.TerritoryId), f);
}
