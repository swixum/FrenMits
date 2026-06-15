using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// The small post-wipe popup: when "auto-show recap" is on it appears after every
// pull ends, offering to open the recap window. Themed to match the plugin, with
// an accent border + button. Movable (drag it; position saved) or lockable.
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

        Theme.PushWindow();
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Accent);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.5f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));

        var vp = ImGui.GetMainViewport();
        var pos = vp.WorkPos + C.RecapPopupPosition * vp.WorkSize;
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));
        if (C.RecapPopupLocked) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); _applyPos = true; }
        else if (_applyPos) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); _applyPos = false; }
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor();
        Theme.PopWindow();
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
        Theme.PushWidgets();
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
        SavePositionIfDragged();

        // Accent bar + bright title.
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var h = ImGui.GetTextLineHeight();
        dl.AddRectFilled(p + new Vector2(0, 1), p + new Vector2(3, h), Theme.Accent, 2f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10);
        ImGui.TextColored(Vec(Theme.Accent), "Mit Recap ready");

        ImGui.Spacing();

        // Accent "View recap" button.
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentHover);
        ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFFFFFu);
        var view = ImGui.Button("View recap");
        ImGui.PopStyleColor(4);
        if (view) _plugin.RecapWindow.IsOpen = !_plugin.RecapWindow.IsOpen; // toggle: click again to close

        ImGui.SameLine();
        if (ImGui.Button("Dismiss")) _plugin.Recap.Dismiss();
        if (!C.RecapPopupLocked)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(Vec(0xFF81766E), "drag to move");
        }

        ImGui.PopStyleVar();
        Theme.PopWidgets();
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

    private static Vector4 Vec(uint abgr) => new(
        (abgr & 0xFF) / 255f, ((abgr >> 8) & 0xFF) / 255f, ((abgr >> 16) & 0xFF) / 255f, ((abgr >> 24) & 0xFF) / 255f);
}
