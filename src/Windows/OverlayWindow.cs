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

    // The line currently being counted down / held, and the run it belongs to.
    private readonly List<MitLine> _activeLines = new();
    private int _lastGen = -1;

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
        // NoTitleBar is always on: a title bar shown only when unlocked shifts the
        // content down by its height, so the display would jump up the moment you
        // lock it. Without one the content top IS the window top in both states, so
        // locking never moves it. Unlocked, you drag the body (move-from-titlebar-
        // only is off), so a grab handle isn't needed.
        Flags = ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.AlwaysAutoResize;

        if (!C.ShowBackground)
            Flags |= ImGuiWindowFlags.NoBackground;

        if (EffectiveLocked)
            Flags |= ImGuiWindowFlags.NoResize
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
        if (EffectiveLocked)
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

    // Locked for real if you ticked the lock OR you're in a live pull (but not
    // while previewing). Combat always pins/click-throughs the overlay so it
    // can't be grabbed and "stuck" mid-fight; drag it out of combat or in preview.
    private bool EffectiveLocked => C.OverlayLocked || (Plugin.InCombat && !C.TestMode);

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
        if (Plugin.InCutscene) return false; // hide while a cutscene is playing
        if (_plugin.Cues.Holding) return false; // and until the post-cutscene resync lands
        if (_plugin.ActiveFight() is not { } fight) return false;
        if (C.OnlyInTargetTerritory && !Plugin.Replaying && fight.TerritoryId != Service.ClientState.TerritoryType) return false;
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
        var elapsed = _plugin.CueClockFor(fight); // call schedule, not sheet position

        var lines = fight.OrderedLines.Where(l => l.Enabled && l.AppliesTo(job)).ToList();

        // Reset the held call when the run restarts (pull / wipe / manual sync) so a
        // stale line from the previous run can't carry over.
        if (_plugin.Timer.Generation != _lastGen) { _lastGen = _plugin.Timer.Generation; _activeLines.Clear(); }

        // The calls we count down to: the soonest line inside its lead window, plus
        // any other line tied at (about) the same time — so simultaneous mits stack
        // instead of one hiding the other. Lead window is the per-line override, else
        // the global warning lead.
        var bestRemaining = float.MaxValue;
        foreach (var line in lines)
        {
            var remaining = line.Time - elapsed;
            var lead = line.LeadOverride > 0f ? line.LeadOverride : C.WarningSeconds;
            if (remaining < 0f || remaining > lead) continue;
            if (remaining < bestRemaining) bestRemaining = remaining;
        }

        const float tieWindow = 0.75f; // lines within this of the soonest stack together
        List<MitLine> group;
        if (bestRemaining < float.MaxValue)
        {
            group = lines.Where(l =>
            {
                var rem = l.Time - elapsed;
                var lead = l.LeadOverride > 0f ? l.LeadOverride : C.WarningSeconds;
                return rem >= 0f && rem <= lead && rem <= bestRemaining + tieWindow;
            }).OrderBy(l => l.Time).ToList();
            _activeLines.Clear();
            _activeLines.AddRange(group); // remember what we're actively counting down
        }
        else
        {
            // Nothing upcoming: briefly hold the calls we actually counted down so
            // "NOW" lingers, but never resurrect a line the clock snapped past (it
            // was never in the active set).
            group = _activeLines
                .Where(l => { var rem = l.Time - elapsed; return rem <= 0f && rem >= -C.HoldSeconds; })
                .OrderBy(l => l.Time).ToList();
            if (group.Count == 0) _activeLines.Clear();
        }

        for (var i = 0; i < group.Count; i++)
        {
            if (i > 0) ImGui.Spacing();
            var call = group[i];
            var remaining = call.Time - elapsed;
            var lead = call.LeadOverride > 0f ? call.LeadOverride : C.WarningSeconds;
            var icon = C.ShowAbilityIcon ? Icons.For(call, job) : 0u;
            var action = Icons.DisplayAction(call.Action, job);
            DrawCurrent(call.Mechanic, action, MathF.Max(0f, remaining), remaining > 0f, call.Color, lead, icon);
        }
    }

    private void DrawCurrent(string mechanic, string action, float remaining, bool imminent,
        uint colorOverride, float lead, uint iconId = 0)
    {
        // Colour priority: per-line override > mit-type colour > default imminent/active.
        var typeColor = C.ColorByMitType ? MitTypes.Color(MitTypes.Classify(action, mechanic), C) : 0u;
        var baseColor = colorOverride != 0 ? colorOverride
            : typeColor != 0 ? typeColor
            : (imminent ? C.OverlayColorImminent : C.OverlayColorActive);
        var color = imminent && C.PulseWhenImminent && remaining < 1.5f ? Pulse(baseColor) : baseColor;
        var headline = FormatHeadline(mechanic, action, remaining, imminent);

        // Cooldown-aware: if your mit won't be off recast by the call, flag it (and
        // tint the call to the warning colour). Guarded; null = not a tracked mit.
        if (C.CooldownAwareCalls && imminent && Cooldowns.Remaining(action) is { } cd && cd > remaining + 0.5f)
        {
            headline += $"  [CD {MathF.Ceiling(cd):0}s]";
            color = 0xFF3C3CF0; // red-ish warning
        }

        // Depleting ring around the icon while counting down (full at the lead, empty
        // at the call). -1 = no ring.
        var ringFrac = C.ShowRadialRing && imminent && lead > 0.01f
            ? Math.Clamp(remaining / lead, 0f, 1f) : -1f;

        using (PushFont(C.OverlayFontSizePx))
            CenteredIconText(iconId, headline, color, ringFrac, baseColor);

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
        var height = MathF.Max(1f, C.ProgressBarHeight);
        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(origin, origin + new Vector2(width, height), 0x80202020, 2f);
        dl.AddRectFilled(origin, origin + new Vector2(width * frac, height), color, 2f);
        ImGui.Dummy(new Vector2(width, height));
    }

    // Depleting countdown ring around the call icon: a faint full ring plus a
    // coloured arc that shrinks from full (at the lead) to empty (at the call).
    private void DrawRing(Vector2 iconTopLeft, float iconH, float frac, uint color)
    {
        var dl = ImGui.GetWindowDrawList();
        var center = iconTopLeft + new Vector2(iconH * 0.5f, iconH * 0.5f);
        var radius = iconH * 0.5f + MathF.Max(2f, iconH * 0.12f);
        var thickness = MathF.Max(2f, iconH * 0.14f);

        dl.AddCircle(center, radius, 0x40FFFFFF, 40, thickness);
        if (frac > 0.001f)
        {
            const float start = -MathF.PI / 2f; // 12 o'clock
            dl.PathArcTo(center, radius, start, start + frac * MathF.PI * 2f, 40);
            dl.PathStroke(color != 0 ? color : 0xFFFFFFFF, ImDrawFlags.None, thickness);
        }
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
        var handle = _plugin.Fonts.Get(sizePx, C.OverlayFontFamily, C.OverlayFontBold, C.OverlayFontItalic);
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
    private void CenteredIconText(uint iconId, string text, uint color, float ringFrac = -1f, uint ringColor = 0)
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
        var offset = AlignOffset(ImGui.GetContentRegionAvail().X, total);
        if (offset > 0) ImGui.SetCursorPosX(MathF.Round(ImGui.GetCursorPosX() + offset));

        // Vertically center the (smaller) icon against the text line, then restore
        // the baseline so the text itself isn't nudged down.
        var baseY = ImGui.GetCursorPosY();
        ImGui.SetCursorPosY(MathF.Round(baseY + (lineH - iconH) * 0.5f));
        var iconTopLeft = ImGui.GetCursorScreenPos();
        Icons.Draw(iconId, new Vector2(iconH, iconH));
        if (ringFrac >= 0f) DrawRing(iconTopLeft, iconH, ringFrac, ringColor);
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

    // Horizontal offset for the configured alignment (0 left, 1 center, 2 right).
    private float AlignOffset(float avail, float contentWidth) => C.OverlayTextAlign switch
    {
        0 => 0f,
        2 => MathF.Max(0f, avail - contentWidth),
        _ => MathF.Max(0f, (avail - contentWidth) * 0.5f),
    };

    private void CenteredText(string text, uint color)
    {
        var textWidth = ImGui.CalcTextSize(text).X;
        var offset = AlignOffset(ImGui.GetContentRegionAvail().X, textWidth);
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
        if (EffectiveLocked) return;
        var viewport = ImGui.GetMainViewport();
        var current = ImGui.GetWindowPos();
        var center = new Vector2(current.X + ImGui.GetWindowWidth() * 0.5f, current.Y);
        var frac = (center - viewport.WorkPos) / viewport.WorkSize;
        if ((frac - C.OverlayPosition).LengthSquared() > 0.0000001f)
        {
            C.OverlayPosition = frac;
            C.Save();
        }
    }
}
