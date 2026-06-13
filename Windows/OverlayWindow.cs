using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

public class OverlayWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public OverlayWindow(Plugin plugin)
        : base("FrenMits##overlay")
    {
        _plugin = plugin;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
    }

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.AlwaysAutoResize;

        if (!C.ShowBackground)
            Flags |= ImGuiWindowFlags.NoBackground;

        if (C.OverlayLocked)
            Flags |= ImGuiWindowFlags.NoTitleBar
                     | ImGuiWindowFlags.NoResize
                     | ImGuiWindowFlags.NoMove
                     | ImGuiWindowFlags.NoMouseInputs;

        if (C.ShowBackground)
            ImGui.PushStyleColor(ImGuiCol.WindowBg, C.BackgroundColor);

        var viewport = ImGui.GetMainViewport();
        var pos = viewport.WorkPos + C.OverlayPosition * viewport.WorkSize;
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y)); // whole pixels = sharp text

        // Locked: pin to the saved spot every frame (click-through HUD).
        // Unlocked: place it once, then let you drag it freely; the drag is saved
        // in Draw(). Forcing the position every frame is what blocked dragging.
        if (C.OverlayLocked)
        {
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.0f));
            _applyPos = true; // re-apply once the moment we unlock / on reset
        }
        else if (_applyPos)
        {
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.0f));
            _applyPos = false;
        }
    }

    private bool _applyPos = true;

    // Snap the overlay back to the saved position next frame (used by Reset).
    public void RequestReposition() => _applyPos = true;

    public override void PostDraw()
    {
        if (C.ShowBackground)
            ImGui.PopStyleColor();
    }

    public override bool DrawConditions()
    {
        if (C.TestMode) return true;
        if (_plugin.ActiveFight() is not { } fight) return false;
        if (C.OnlyInTargetTerritory && fight.TerritoryId != Service.ClientState.TerritoryType) return false;
        return _plugin.Timer.Running;
    }

    public override void Draw()
    {
        SavePositionIfDragged();

        if (C.TestMode && !_plugin.Timer.Running)
        {
            DrawCurrent("Reprisal / Feint", "Reprisal", 1.4f, true, 0, C.WarningSeconds,
                Icons.ResolveFromText("Reprisal"));
            return;
        }

        var fight = _plugin.ActiveFight();
        if (fight == null) return;

        var job = _plugin.ActiveJobAbbreviation();
        var elapsed = _plugin.Timer.Elapsed + fight.TimerOffset;

        var lines = fight.OrderedLines.Where(l => l.Enabled && l.AppliesTo(job)).ToList();

        MitLine? current = null;
        var bestScore = float.MaxValue;
        foreach (var line in lines)
        {
            var remaining = line.Time - elapsed;
            if (remaining > C.WarningSeconds) continue;
            if (remaining < -C.HoldSeconds) continue;
            var score = remaining >= 0 ? remaining : 1000f - remaining;
            if (score < bestScore) { bestScore = score; current = line; }
        }

        if (current is { } call)
        {
            var remaining = call.Time - elapsed;
            var lead = call.LeadOverride > 0f ? call.LeadOverride : C.WarningSeconds;
            var icon = C.ShowAbilityIcon ? Icons.For(call) : 0u;
            DrawCurrent(call.Mechanic, call.Action, MathF.Max(0f, remaining), remaining > 0f, call.Color, lead, icon);
        }
    }

    private void DrawCurrent(string mechanic, string action, float remaining, bool imminent,
        uint colorOverride, float lead, uint iconId = 0)
    {
        var baseColor = colorOverride != 0 ? colorOverride : (imminent ? C.OverlayColorImminent : C.OverlayColorActive);
        var color = imminent && C.PulseWhenImminent && remaining < 1.5f ? Pulse(baseColor) : baseColor;
        var headline = FormatHeadline(mechanic, action, remaining, imminent);

        using (PushFont(C.OverlayFontSizePx))
            CenteredIconText(iconId, headline, color);

        if (C.ShowMechanicLine
            && !string.IsNullOrWhiteSpace(mechanic)
            && !string.Equals(mechanic, action, StringComparison.OrdinalIgnoreCase))
        {
            using (PushFont(C.OverlayFontSizePx * 0.55f))
                CenteredText(mechanic, C.OverlayColorMechanic);
        }

        if (C.ShowProgressBar && lead > 0.01f)
            DrawProgressBar(Math.Clamp(remaining / lead, 0f, 1f), color);
    }

    private void DrawProgressBar(float frac, uint color)
    {
        var width = ImGui.GetContentRegionAvail().X;
        if (width < 4f) width = 200f;
        const float height = 6f;
        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(origin, origin + new Vector2(width, height), 0x80202020, 2f);
        dl.AddRectFilled(origin, origin + new Vector2(width * frac, height), color, 2f);
        ImGui.Dummy(new Vector2(width, height));
    }

    // Brightness oscillation for the imminent pulse, preserving alpha.
    private static uint Pulse(uint abgr)
    {
        var t = (MathF.Sin((float)ImGui.GetTime() * 12f) * 0.5f + 0.5f);
        var factor = 0.55f + 0.45f * t;
        var a = abgr & 0xFF000000;
        var b = (uint)(((abgr >> 16) & 0xFF) * factor) & 0xFF;
        var g = (uint)(((abgr >> 8) & 0xFF) * factor) & 0xFF;
        var r = (uint)((abgr & 0xFF) * factor) & 0xFF;
        return a | (b << 16) | (g << 8) | r;
    }

    private string FormatHeadline(string mechanic, string action, float remaining, bool imminent)
    {
        var label = string.IsNullOrWhiteSpace(action) ? mechanic : action;

        // Once we're at/after the call time, drop the countdown and show "NOW".
        if (!imminent)
            return label + C.ActiveSuffix;

        // Counting down: clean "Raidwide (3.3)" style from the format template.
        var count = MathF.Ceiling(remaining).ToString("0");
        var text = C.HeadlineFormat
            .Replace("{action}", label)
            .Replace("{mechanic}", mechanic)
            .Replace("{time}", TimeText(remaining))
            .Replace("{remaining}", remaining.ToString("0.0"))
            .Replace("{count}", count);

        // Optional legacy append, only if the format itself has no number in it.
        if (C.ShowCountdownNumber
            && !C.HeadlineFormat.Contains("{remaining}")
            && !C.HeadlineFormat.Contains("{count}"))
            text = $"{text}   {count}";
        return text;
    }

    private static string TimeText(float seconds)
    {
        var t = (int)MathF.Round(seconds);
        return $"{t / 60}:{t % 60:00}";
    }

    // Pushes a crisp Dalamud font handle at the given px size, falling back to
    // SetWindowFontScale if the handle is not ready yet. Returns an IDisposable.
    private IDisposable PushFont(float sizePx)
    {
        var handle = _plugin.Fonts.Get(sizePx);
        if (handle is { Available: true })
            return handle.Push();

        // Fallback: approximate via window font scale.
        ImGui.SetWindowFontScale(MathF.Max(0.5f, sizePx / 18f));
        return new ResetFontScale();
    }

    private sealed class ResetFontScale : IDisposable
    {
        public void Dispose() => ImGui.SetWindowFontScale(1f);
    }

    // Centers an optional ability icon followed by the text as one group.
    private void CenteredIconText(uint iconId, string text, uint color)
    {
        if (iconId == 0)
        {
            CenteredText(text, color);
            return;
        }

        var lineH = ImGui.GetTextLineHeight();
        var iconH = MathF.Round(lineH * Math.Clamp(C.IconScale, 0.4f, 1.5f));
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var textWidth = ImGui.CalcTextSize(text).X;
        var total = iconH + spacing + textWidth;
        var avail = ImGui.GetContentRegionAvail().X;
        var offset = (avail - total) * 0.5f;
        if (offset > 0) ImGui.SetCursorPosX(MathF.Round(ImGui.GetCursorPosX() + offset));

        // Vertically centre the (smaller) icon against the text line, then restore
        // the baseline so the text itself isn't nudged down.
        var baseY = ImGui.GetCursorPosY();
        ImGui.SetCursorPosY(MathF.Round(baseY + (lineH - iconH) * 0.5f));
        Icons.Draw(iconId, new Vector2(iconH, iconH));
        ImGui.SameLine(0, spacing);
        ImGui.SetCursorPosY(baseY);

        if (C.TextShadow)
        {
            var p = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddText(p + new Vector2(1.5f, 1.5f), 0xE0000000, text);
        }
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private void CenteredText(string text, uint color)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var textWidth = ImGui.CalcTextSize(text).X;
        var offset = (avail - textWidth) * 0.5f;
        if (offset > 0) ImGui.SetCursorPosX(MathF.Round(ImGui.GetCursorPosX() + offset));

        if (C.TextShadow)
        {
            var p = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddText(p + new Vector2(1.5f, 1.5f), 0xE0000000, text);
        }

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private void SavePositionIfDragged()
    {
        if (C.OverlayLocked) return;
        var viewport = ImGui.GetMainViewport();
        var current = ImGui.GetWindowPos();
        var centre = new Vector2(current.X + ImGui.GetWindowWidth() * 0.5f, current.Y);
        var frac = (centre - viewport.WorkPos) / viewport.WorkSize;
        if ((frac - C.OverlayPosition).LengthSquared() > 0.0000001f)
        {
            C.OverlayPosition = frac;
            C.Save();
        }
    }
}
