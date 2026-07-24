using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// A compact readout of YOUR currently-active mitigations: a row of status icons
// with seconds remaining, tinted by mit type.
public class MitBarWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;
    private bool _applyPos = true;

    private bool EffectiveLocked => OverlayChrome.Locked(C.MitBarLocked, C);

    public MitBarWindow(Plugin plugin) : base("FrenMits Mits##mitbar")
    {
        _plugin = plugin;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
    }

    public void RequestReposition() => _applyPos = true;

    public override void PreDraw()
    {
        // NoTitleBar always on so locking can't shift the bar vertically (a title
        // bar present only when unlocked would).
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground;

        if (EffectiveLocked)
            Flags |= ImGuiWindowFlags.NoResize
                     | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoMouseInputs;

        OverlayChrome.ApplyPosition(C.MitBarPosition, EffectiveLocked, ref _applyPos);
    }

    public override bool DrawConditions()
    {
        if (!C.ShowMitBar) return false;
        if (C.TestMode) return true;
        if (Plugin.CutsceneActive) return false;
        return _plugin.Timer.Running || Plugin.LocalPlayer != null;
    }

    public override void Draw()
    {
        SavePositionIfDragged();

        var mits = MitWatch.Current();
        if (mits.Count == 0)
        {
            ImGui.Dummy(new Vector2(1, 1)); // keep the window alive between buffs
            return;
        }

        using var _ = PushFont(C.MitBarFontSizePx);
        var first = true;
        foreach (var m in mits)
        {
            if (!first) ImGui.SameLine(0, 10f);
            first = false;

            var color = MitTypes.Color(m.Kind, C);
            if (color == 0) color = 0xFFFFFFFF;

            var h = ImGui.GetTextLineHeight();
            if (m.IconId != 0)
            {
                Icons.Draw(m.IconId, new Vector2(h, h));
                ImGui.SameLine(0, 4f);
            }
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted($"{(int)MathF.Ceiling(m.Remaining)}s");
            ImGui.PopStyleColor();
        }
    }

    private void SavePositionIfDragged()
    {
        if (EffectiveLocked) return;
        if (OverlayChrome.MovedCenterFrac(C.MitBarPosition) is { } frac) { C.MitBarPosition = frac; _posDirty = true; }
        // ONE disk write when the drag ends - not sixty full-config saves a
        // second while the window is being moved.
        if (_posDirty && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { C.Save(); _posDirty = false; }
    }

    private bool _posDirty;

    private IDisposable PushFont(float sizePx)
        => OverlayChrome.PushFont(_plugin.Fonts, sizePx, C.OverlayFontFamily, C.OverlayFontBold, C.OverlayFontItalic);
}
