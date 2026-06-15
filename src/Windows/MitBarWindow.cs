using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// A compact readout of YOUR currently-active mitigations: a row of status icons
// with seconds remaining, tinted by mit type. Opt-in, independently placeable.
public class MitBarWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;
    private bool _applyPos = true;

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
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoBackground;

        if (C.MitBarLocked)
            Flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize
                     | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoMouseInputs;

        var vp = ImGui.GetMainViewport();
        var pos = vp.WorkPos + C.MitBarPosition * vp.WorkSize;
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));
        if (C.MitBarLocked) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); _applyPos = true; }
        else if (_applyPos) { ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f)); _applyPos = false; }
    }

    public override bool DrawConditions()
    {
        if (!C.ShowMitBar) return false;
        if (C.TestMode) return true;
        if (Plugin.InCutscene) return false;
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
        if (C.MitBarLocked) return;
        var vp = ImGui.GetMainViewport();
        var cur = ImGui.GetWindowPos();
        var center = new Vector2(cur.X + ImGui.GetWindowWidth() * 0.5f, cur.Y + ImGui.GetWindowHeight() * 0.5f);
        var frac = (center - vp.WorkPos) / vp.WorkSize;
        if ((frac - C.MitBarPosition).LengthSquared() > 0.0000001f) { C.MitBarPosition = frac; C.Save(); }
    }

    private IDisposable PushFont(float sizePx)
    {
        var handle = _plugin.Fonts.Get(sizePx, C.OverlayFontFamily, C.OverlayFontBold, C.OverlayFontItalic);
        if (handle is { Available: true }) return handle.Push();
        ImGui.SetWindowFontScale(MathF.Max(0.5f, sizePx / 18f));
        return new Reset();
    }

    private sealed class Reset : IDisposable { public void Dispose() => ImGui.SetWindowFontScale(1f); }
}
