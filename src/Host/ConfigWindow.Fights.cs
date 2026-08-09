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

        // One title row: the group, how many, then filter and add.
        var frameH = ImGui.GetFrameHeight();
        var headStart = ImGui.GetCursorPos();
        var (headH, headEnd) = PageTitle(category);

        ImGui.SetCursorPos(new Vector2(headEnd + Theme.S(10f),
            headStart.Y + (headH - ImGui.GetTextLineHeightWithSpacing()) * 0.5f));
        Widgets.Chip("", fights.Count.ToString(), Theme.TextBright);
        var used = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;

        var addW = IconBtnWidth(FontAwesomeIcon.Plus, "Add");
        var right = Theme.S(150f) + addW + Theme.S(8f) + Theme.S(4f);
        ImGui.SetCursorPos(new Vector2(
            MathF.Max(used + Theme.S(12f), ImGui.GetContentRegionMax().X - right),
            headStart.Y + (headH - frameH) * 0.5f));
        ImGui.SetNextItemWidth(Theme.S(150f));
        ImGui.InputTextWithHint("##fightfilter", "Filter", ref _fightFilter, 64);
        ImGui.SameLine(0, Theme.S(8f));
        DrawCategoryToolbar(category);
        ImGui.SetCursorPos(new Vector2(headStart.X, headStart.Y + headH));
        var filter = _fightFilter.Trim();
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

            if (fight.Id == _expandFightId) { ImGui.SetNextItemOpen(true); _expandFightId = ""; }
            var official = Builtin.Has(fight.TerritoryId);
            var headerStartX = ImGui.GetCursorPosX();

            // Everything that shares this line, measured before the name is
            // drawn: the name is then cut to what is left, so a long one cannot
            // end up under the slot chip or the on switch.
            var hasSheet = official || fight.CustomSlots.Count > 0;
            var slotTag = !hasSheet ? "" : string.IsNullOrEmpty(fight.Slot) ? "no slot" : fight.Slot;

            // Measured, not guessed: the sheet button is an icon glyph plus frame
            // padding, and a chip is its text plus its own padding. Estimating
            // either one low pushes the last control off the right edge.
            var gap = Theme.S(6f);
            var edge = Theme.S(4f);
            var checkW = ImGui.GetFrameHeight();
            var sheetW = 0f;
            if (hasSheet)
            {
                using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                    sheetW = ImGui.CalcTextSize(FontAwesomeIcon.Table.ToIconString()).X;
                sheetW += ImGui.GetStyle().FramePadding.X * 2f;
            }
            var tagW = slotTag.Length > 0 ? Widgets.ChipWidth("", slotTag) : 0f;

            // Placed from the right edge inwards, so every row ends in one column.
            var checkX = ImGui.GetContentRegionMax().X - edge - checkW;
            var sheetX = checkX - (hasSheet ? gap + sheetW : 0f);
            var tagX = sheetX - (tagW > 0f ? gap + tagW : 0f);

            var starW = ImGui.GetTextLineHeight() + Theme.S(8f);
            var labelX = headerStartX + ImGui.GetTreeNodeToLabelSpacing() + ImGui.GetStyle().FramePadding.X;
            var nameRoom = tagX - labelX - starW - gap;

            // An empty label leaves just the arrow, so the star can lead the name.
            var open = ImGui.CollapsingHeader($"###fh-{fight.Id}");
            ImGui.SetItemAllowOverlap();
            var headMin = ImGui.GetItemRectMin();
            var headMax = ImGui.GetItemRectMax();
            // The open fight gets the sidebar's accent bar, so selection reads the same everywhere.
            if (open)
                ImGui.GetWindowDrawList().AddRectFilled(
                    new Vector2(headMin.X, headMin.Y + 2f), new Vector2(headMin.X + Theme.S(3f), headMax.Y - 2f),
                    Theme.Accent, 2f);

            // Star, then the name. A category mark reads before the thing it marks.
            ImGui.SameLine(labelX);
            ImGui.AlignTextToFramePadding();
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            {
                if (!official) ImGui.SetWindowFontScale(0.8f);
                ImGui.TextColored(official ? GoldStar : UserBlue,
                    (official ? FontAwesomeIcon.Star : FontAwesomeIcon.User).ToIconString());
                if (!official) ImGui.SetWindowFontScale(1f);
            }
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip(official ? "Official sheet." : "User created.");

            ImGui.SameLine(0, Theme.S(8f));
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(Widgets.Elide(fight.Name, nameRoom));

            // Your slot: the one thing that decides whether calls fire.
            var rowEnd = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
            if (slotTag.Length > 0)
            {
                ImGui.SameLine(MathF.Max(rowEnd + gap, tagX));
                Widgets.Chip("", slotTag, string.IsNullOrEmpty(fight.Slot)
                    ? Theme.Warn : Theme.RoleColor(fight.Slot));
                if (Widgets.HoveredDelayed())
                    ImGui.SetTooltip(string.IsNullOrEmpty(fight.Slot)
                        ? "No slot picked yet, so nothing is called for this fight."
                        : $"Your column for this fight is {fight.Slot}.");
                rowEnd = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
            }

            // Straight into Sheet View, for any fight that has one.
            if (hasSheet)
            {
                ImGui.SameLine(MathF.Max(rowEnd + gap, sheetX));
                using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                {
                    if (ImGui.SmallButton(FontAwesomeIcon.Table.ToIconString() + "##opensheet"))
                        _plugin.SheetViewWindow.Open(fight);
                }
                if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Open in Sheet View");
                rowEnd = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
            }

            // On or off, in the same column on every row.
            ImGui.SameLine(MathF.Max(rowEnd + gap, checkX));
            var enabled = fight.Enabled;
            if (GreenCheckbox("##en", ref enabled)) { fight.Enabled = enabled; C.Save(); }
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip(enabled ? "On. Untick to skip this fight." : "Off. Nothing is called here.");

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
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, "Add"))
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
