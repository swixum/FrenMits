using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// A tiny once-per-entry check-in: entering a duty that has a sheet (official
// or one you built) shows which column is yours, with a picker to change it.
// Opt-in (off by default), opens exactly once per zone-in, and hides itself
// the moment combat starts.
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

    // Called from the territory-change handler: once per entry, never re-shown
    // mid-instance.
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

            var idx = Math.Max(0, Array.FindIndex(_slots,
                s => s.Equals(_fight.Slot, StringComparison.OrdinalIgnoreCase)));
            ImGui.SetNextItemWidth(90f);
            if (ImGui.Combo("##slotpick", ref idx, _slots, _slots.Length)
                && !string.Equals(_slots[idx], _fight.Slot, StringComparison.OrdinalIgnoreCase))
                _plugin.SetSlot(_fight, _slots[idx]);

            ImGui.SameLine();
            if (ImGui.Button("OK", new Vector2(50, 0))) IsOpen = false;

            // The global role picker, popup-sized: one pick maps every official
            // fight to that role's slot (custom sheets have no canonical roles).
            if (Builtin.Has(_fight.TerritoryId))
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled("Role:");
                ImGui.SameLine();
                var roles = Builtin.Roles;
                var rIdx = string.IsNullOrEmpty(C.RoleSelection)
                    ? -1 : Array.IndexOf(roles, C.RoleSelection);
                ImGui.SetNextItemWidth(120f);
                if (ImGui.BeginCombo("##rolepick", rIdx >= 0 ? roles[rIdx] : "(pick)"))
                {
                    foreach (var role in roles)
                        if (ImGui.Selectable(role, role == C.RoleSelection))
                            _plugin.SetRoleForAll(role);
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
