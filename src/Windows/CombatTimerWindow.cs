using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// A plain combat stopwatch (mm:ss of the current pull), shown as its own
// independently placeable, independently styled overlay. Opt-in via the
// Combat Timer tool page. Mirrors the other HUD windows' lock/drag behaviour.
public class CombatTimerWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;
    private bool _applyPos = true;

    // Locked for real if you ticked the lock OR you're in a live pull (but not
    // while previewing) — combat always pins it so it can't be grabbed mid-fight.
    private bool EffectiveLocked => C.CombatTimerLocked || (Plugin.InCombat && !C.TestMode);

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
        // NoTitleBar always on so locking can't shift it vertically. Drag the body.
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

        var vp = ImGui.GetMainViewport();
        var pos = vp.WorkPos + C.CombatTimerPosition * vp.WorkSize;
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));
        if (EffectiveLocked) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); _applyPos = true; }
        else if (_applyPos) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); _applyPos = false; }
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
        ImGui.TextUnformatted(Format(secs));
        ImGui.PopStyleColor();
    }

    private static string Format(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        var total = (int)seconds;
        return $"{total / 60}:{total % 60:D2}";
    }

    private void SavePositionIfDragged()
    {
        if (EffectiveLocked) return;
        var vp = ImGui.GetMainViewport();
        var cur = ImGui.GetWindowPos();
        var center = new Vector2(cur.X + ImGui.GetWindowWidth() * 0.5f, cur.Y + ImGui.GetWindowHeight() * 0.5f);
        var frac = (center - vp.WorkPos) / vp.WorkSize;
        if ((frac - C.CombatTimerPosition).LengthSquared() > 0.0000001f) { C.CombatTimerPosition = frac; _posDirty = true; }
        // ONE disk write when the drag ends - not sixty full-config saves a
        // second while the window is being moved.
        if (_posDirty && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { C.Save(); _posDirty = false; }
    }

    private bool _posDirty;

    private IDisposable PushFont(float sizePx)
    {
        var handle = _plugin.Fonts.Get(sizePx, C.CombatTimerFontFamily, C.CombatTimerFontBold, C.CombatTimerFontItalic);
        if (handle is { Available: true }) return handle.Push();
        ImGui.SetWindowFontScale(MathF.Max(0.5f, sizePx / 18f));
        return new Reset();
    }

    private sealed class Reset : IDisposable { public void Dispose() => ImGui.SetWindowFontScale(1f); }
}
