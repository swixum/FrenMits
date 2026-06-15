using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// A small "Mit Recap" button that appears for a few seconds after a pull ends
// (the recap is captured automatically then). Clicking it re-captures and opens
// the Party Mit Recap page. Hidden in combat and once the window passes.
public class RecapButtonWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public RecapButtonWindow(Plugin plugin) : base("FrenMits Recap##recapbtn")
    {
        _plugin = plugin;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize;
    }

    public override void PreDraw()
    {
        var vp = ImGui.GetMainViewport();
        var pos = vp.WorkPos + new Vector2(vp.WorkSize.X * 0.5f, vp.WorkSize.Y * 0.30f);
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
    }

    public override bool DrawConditions()
    {
        if (!C.ShowRecapButton || Plugin.InCutscene) return false;
        if (Service.Condition[ConditionFlag.InCombat]) return false; // only after the pull ends
        if (_plugin.Recap.CapturedAt == default) return false;
        return (DateTime.UtcNow - _plugin.Recap.CapturedAt).TotalSeconds < 30; // brief window after a wipe
    }

    public override void Draw()
    {
        if (ImGui.Button("  Mit Recap  "))
        {
            _plugin.Recap.Capture();
            _plugin.ConfigWindow.OpenPartyRecap();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("See which mits landed on the boss this pull, and which were missing.");
    }
}
