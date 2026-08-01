using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// A once-per-entry check-in on which column is yours.
public class SlotPopupWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    private FightProfile? _fight;
    private string[] _slots = Array.Empty<string>();

    public SlotPopupWindow(Plugin plugin) : base("Your slot###fmslotpop")
    {
        _plugin = plugin;
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
    }

    // Once per entry, never re-shown mid-instance.
    public void OpenFor(FightProfile fight)
    {
        _fight = fight;
        _slots = Builtin.Has(fight.TerritoryId)
            ? Builtin.Slots(fight.TerritoryId)
            : fight.CustomSlots.ToArray();
        if (_slots.Length == 0) return;
        IsOpen = true;
    }

    public override bool DrawConditions()
    {
        if (_fight == null || !C.Fights.Contains(_fight)) return false;
        if (Plugin.InCombat) return false; // never in the way of a pull
        // Left the duty: close for good (re-entry calls OpenFor again).
        if (_fight.TerritoryId != Service.ClientState.TerritoryType)
        {
            IsOpen = false;
            return false;
        }
        return true;
    }

    public override void PreDraw() => Theme.PushWindow();
    public override void PostDraw() => Theme.PopWindow();

    public override void Draw()
    {
        Theme.PushWidgets();
        try
        {
            ImGui.TextUnformatted(_fight!.Name);
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("Your slot:");
            ImGui.SameLine();

            // No slot yet must show as "(pick)", never as the first entry.
            var current = _fight.Slot ?? "";
            var preview = string.IsNullOrEmpty(current) ? "(pick)" : current;
            ImGui.SetNextItemWidth(90f);
            if (ImGui.BeginCombo("##slotpick", preview))
            {
                foreach (var slot in _slots)
                    if (ImGui.Selectable(slot, slot.Equals(current, StringComparison.OrdinalIgnoreCase))
                        && !slot.Equals(current, StringComparison.OrdinalIgnoreCase))
                    {
                        _plugin.SetSlot(_fight, slot);
                        _plugin.SheetViewWindow.MarkPlanDirty(); // background grid follows
                    }
                ImGui.EndCombo();
            }

            ImGui.SameLine();
            if (ImGui.Button("OK", new Vector2(50, 0))) IsOpen = false;

            // Job pick, the same one the sidebar owns.
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("Job:");
            ImGui.SameLine(58f);
            var jobPreview = C.JobSelection == "Auto" ? "Auto (current job)" : C.JobSelection;
            ImGui.SetNextItemWidth(120f);
            if (ImGui.BeginCombo("##jobpick", jobPreview))
            {
                if (ImGui.Selectable("Auto (current job)", C.JobSelection == "Auto") && C.JobSelection != "Auto")
                { C.JobSelection = "Auto"; C.SaveSettings(); }
                foreach (var job in Jobs.Abbreviations)
                    if (ImGui.Selectable(job, string.Equals(job, C.JobSelection, StringComparison.OrdinalIgnoreCase))
                        && !string.Equals(job, C.JobSelection, StringComparison.OrdinalIgnoreCase))
                    { C.JobSelection = job; C.SaveSettings(); }
                ImGui.EndCombo();
            }

            // Role pick: one pick maps every sheet to that role's slot.
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled("Role:");
                ImGui.SameLine(58f);
                var rolePreview = string.IsNullOrEmpty(C.RoleSelection) ? "(pick)" : C.RoleSelection;
                ImGui.SetNextItemWidth(120f);
                if (ImGui.BeginCombo("##rolepick", rolePreview))
                {
                    foreach (var role in Builtin.Roles)
                        if (ImGui.Selectable(role, string.Equals(role, C.RoleSelection, StringComparison.OrdinalIgnoreCase))
                            && !string.Equals(role, C.RoleSelection, StringComparison.OrdinalIgnoreCase))
                        {
                            _plugin.SetRoleForAll(role);
                            _plugin.SheetViewWindow.MarkPlanDirty(); // background grid follows
                        }
                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                ImGui.TextDisabled("(every fight)");
            }

            if (string.IsNullOrEmpty(_fight.Slot))
                ImGui.TextDisabled("No slot picked yet; pick one so the calls know whose column to read.");
        }
        finally { Theme.PopWidgets(); }
    }
}
