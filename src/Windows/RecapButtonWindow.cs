using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// The small post-wipe popup: when "auto-show recap" is on it appears after every
// pull ends, offering to open the recap window. Movable (drag it; position saved)
// or lockable. Hidden in combat and once the window passes.
public class RecapButtonWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;
    private bool _applyPos = true;

    public RecapButtonWindow(Plugin plugin) : base("FrenMits Recap##recapbtn")
    {
        _plugin = plugin;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
    }

    public void RequestReposition() => _applyPos = true;

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize;
        if (C.RecapPopupLocked)
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;

        var vp = ImGui.GetMainViewport();
        var pos = vp.WorkPos + C.RecapPopupPosition * vp.WorkSize;
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));
        if (C.RecapPopupLocked) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); _applyPos = true; }
        else if (_applyPos) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); _applyPos = false; }
    }

    public override bool DrawConditions()
    {
        if (!C.ShowRecapButton || Plugin.InCutscene) return false;
        if (Service.Condition[ConditionFlag.InCombat]) return false; // only after the pull ends
        if (_plugin.Recap.PopupDismissed || _plugin.Recap.CapturedAt == default) return false;
        return (DateTime.UtcNow - _plugin.Recap.CapturedAt).TotalSeconds < 30; // brief window after a wipe
    }

    public override void Draw()
    {
        SavePositionIfDragged();

        ImGui.TextUnformatted("Mit Recap ready");
        if (ImGui.Button("View"))
            _plugin.RecapWindow.IsOpen = true;
        ImGui.SameLine();
        if (ImGui.SmallButton("Dismiss"))
            _plugin.Recap.Dismiss();
        if (!C.RecapPopupLocked)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(drag to move)");
        }
    }

    private void SavePositionIfDragged()
    {
        if (C.RecapPopupLocked) return;
        var vp = ImGui.GetMainViewport();
        var cur = ImGui.GetWindowPos();
        var center = new Vector2(cur.X + ImGui.GetWindowWidth() * 0.5f, cur.Y + ImGui.GetWindowHeight() * 0.5f);
        var frac = (center - vp.WorkPos) / vp.WorkSize;
        if ((frac - C.RecapPopupPosition).LengthSquared() > 0.0000001f) { C.RecapPopupPosition = frac; C.Save(); }
    }
}
