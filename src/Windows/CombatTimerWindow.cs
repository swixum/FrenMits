using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// A plain combat stopwatch (mm:ss of the current pull), shown as its own
// independently placeable, independently styled overlay.
public class CombatTimerWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;
    private bool _applyPos = true;

    private bool EffectiveLocked => OverlayChrome.Locked(C.CombatTimerLocked, C);

    public CombatTimerWindow(Plugin plugin) : base("FrenMits Combat Timer##combattimer")
    {
        _plugin = plugin;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
    }

    public void RequestReposition() => _applyPos = true;

    public override void PreDraw()
    {
        // NoTitleBar always on so locking can't shift it vertically.
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoTitleBar;

        if (!C.CombatTimerShowBackground)
            Flags |= ImGuiWindowFlags.NoBackground;

        if (EffectiveLocked)
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoMouseInputs;

        if (C.CombatTimerShowBackground)
            ImGui.PushStyleColor(ImGuiCol.WindowBg, C.CombatTimerBackgroundColor);

        OverlayChrome.ApplyPosition(C.CombatTimerPosition, EffectiveLocked, ref _applyPos);
    }

    public override void PostDraw()
    {
        if (C.CombatTimerShowBackground)
            ImGui.PopStyleColor();
    }

    public override bool DrawConditions()
    {
        if (!C.ShowCombatTimer) return false;
        if (C.TestMode) return true;
        if (Plugin.CutsceneActive) return false;
        return _plugin.Timer.CombatRunning;
    }

    public override void Draw()
    {
        SavePositionIfDragged();

        // Live combat time; a sample (2:32) when previewing out of combat.
        var secs = C.TestMode && !_plugin.Timer.CombatRunning ? 152f : _plugin.Timer.CombatElapsed;

        using var _ = PushFont(C.CombatTimerFontSizePx);
        ImGui.PushStyleColor(ImGuiCol.Text, C.CombatTimerColor);
        ImGui.TextUnformatted(Fmt.MmssFloor(MathF.Max(0f, secs)));
        ImGui.PopStyleColor();
    }

    private void SavePositionIfDragged()
    {
        if (EffectiveLocked) return;
        if (OverlayChrome.MovedCenterFrac(C.CombatTimerPosition) is { } frac) { C.CombatTimerPosition = frac; _posDirty = true; }
        // ONE disk write when the drag ends - not sixty full-config saves a
        // second while the window is being moved.
        if (_posDirty && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { C.Save(); _posDirty = false; }
    }

    private bool _posDirty;

    private IDisposable PushFont(float sizePx)
        => OverlayChrome.PushFont(_plugin.Fonts, sizePx, C.CombatTimerFontFamily, C.CombatTimerFontBold, C.CombatTimerFontItalic);
}
