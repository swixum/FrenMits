using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Planning;

// A once-per-entry check-in on which column is yours.
public class SlotPopupWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    private FightProfile? _fight;
    private string[] _slots = Array.Empty<string>();
    private bool _rememberPref = false;

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
            ImGui.SetNextItemWidth(120f);
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
            if (ImGui.Button("OK", new Vector2(50, 0))) 
            {
                if (_rememberPref && !string.IsNullOrEmpty(current))
                {
                    var job = Jobs.ByAbbreviation(_plugin.ActiveJobAbbreviation());
                    if (job != null)
                    {
                        C.GlobalRolePreferences[job.Value.Role] = current;
                        C.Save();
                    }
                }
                IsOpen = false;
            }

            var activeJob = Jobs.ByAbbreviation(_plugin.ActiveJobAbbreviation());
            if (activeJob != null && !string.IsNullOrEmpty(current))
            {
                var roleName = activeJob.Value.Role switch
                {
                    JobRole.Tank => "Tank",
                    JobRole.Healer => "Healer",
                    JobRole.Melee => "Melee",
                    JobRole.PhysicalRanged => "Phys Ranged",
                    JobRole.Caster => "Caster",
                    _ => "Role"
                };
                ImGui.Checkbox($"Remember as my default {roleName} slot", ref _rememberPref);
            }

            if (string.IsNullOrEmpty(_fight.Slot))
                ImGui.TextDisabled("No slot picked yet; pick one so the calls know whose column to read.");
        }
        finally { Theme.PopWidgets(); }
    }
}
