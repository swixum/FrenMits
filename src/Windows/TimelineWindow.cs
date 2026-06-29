using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// The "next mits" timeline — a separate, independently placeable window that
// lists the upcoming calls. The main call-out window only ever shows the single
// imminent mit; everything still on the horizon lives here instead.
public class TimelineWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public TimelineWindow(Plugin plugin)
        : base("FrenMits Timeline##timeline")
    {
        _plugin = plugin;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
    }

    private bool _applyPos = true;

    // Locked for real if you ticked the lock OR you're in a live pull (but not
    // while previewing) — combat always pins it so it can't be grabbed mid-fight.
    private bool EffectiveLocked => C.TimelineLocked || (Plugin.InCombat && !C.TestMode);

    public void RequestReposition() => _applyPos = true;

    public override void PreDraw()
    {
        // NoTitleBar always on so locking can't shift the content vertically (a
        // title bar present only when unlocked would). Drag the body to move it.
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
        var pos = viewport.WorkPos + C.TimelinePosition * viewport.WorkSize;
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));

        if (EffectiveLocked)
        {
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.0f));
            _applyPos = true;
        }
        else if (_applyPos)
        {
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.0f));
            _applyPos = false;
        }
    }

    public override void PostDraw()
    {
        if (C.ShowBackground)
            ImGui.PopStyleColor();
    }

    public override bool DrawConditions()
    {
        if (!C.ShowUpcoming) return false;
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
            using (PushFont(C.UpcomingFontSizePx))
            {
                Row(Icons.ResolveFromText("Addle"), "+12s  Addle");
                Row(Icons.ResolveFromText("Rampart"), "+28s  Rampart");
                Row(Icons.ResolveFromText("Reprisal"), "+41s  Reprisal");
            }
            return;
        }

        var fight = _plugin.ActiveFight();
        if (fight == null) return;

        var job = _plugin.ActiveJobAbbreviation();
        var elapsed = _plugin.ElapsedFor(fight);

        // Show lines that are beyond their lead window (a line inside its lead is on
        // the main call, so it isn't duplicated here) and within the look-ahead.
        var upcoming = fight.OrderedLines
            .Where(l => l.Enabled && l.AppliesTo(job)
                        && l.Time - elapsed > (l.LeadOverride > 0f ? l.LeadOverride : C.WarningSeconds)
                        && l.Time - elapsed <= C.UpcomingLookaheadSeconds)
            .OrderBy(l => l.Time)
            .Take(Math.Max(0, C.UpcomingCount))
            .ToList();

        if (upcoming.Count == 0)
        {
            // Keep the window from collapsing to a dot between calls.
            ImGui.Dummy(new Vector2(1f, 1f));
            return;
        }

        using (PushFont(C.UpcomingFontSizePx))
            foreach (var l in upcoming)
            {
                var inSec = (int)MathF.Round(l.Time - elapsed);
                var name = string.IsNullOrWhiteSpace(l.Action) ? l.Mechanic : Icons.DisplayAction(l.Action, job);
                var icon = C.ShowAbilityIcon ? Icons.For(l, job) : 0u;
                // Dim a line whose mit won't be off cooldown by the time it's called.
                var notReady = C.CooldownAwareCalls
                    && Cooldowns.Remaining(l.Action) is { } cd && cd > (l.Time - elapsed) + 0.5f;
                Row(icon, $"+{inSec}s  {name}{(notReady ? "  (cd)" : "")}", notReady);
            }
    }

    private void Row(uint iconId, string text, bool dim = false)
    {
        if (dim) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.4f);
        var color = C.OverlayColorUpcoming;
        if (iconId == 0)
        {
            CenteredText(text, color);
        }
        else
        {
            var lineH = ImGui.GetTextLineHeight();
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var total = lineH + spacing + ImGui.CalcTextSize(text).X;
            var offset = (ImGui.GetContentRegionAvail().X - total) * 0.5f;
            if (offset > 0) ImGui.SetCursorPosX(MathF.Round(ImGui.GetCursorPosX() + offset));

            Icons.Draw(iconId, new Vector2(lineH, lineH));
            ImGui.SameLine(0, spacing);
            DrawText(text, color);
        }
        if (dim) ImGui.PopStyleVar();
    }

    private void CenteredText(string text, uint color)
    {
        var offset = (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X) * 0.5f;
        if (offset > 0) ImGui.SetCursorPosX(MathF.Round(ImGui.GetCursorPosX() + offset));
        DrawText(text, color);
    }

    private void DrawText(string text, uint color)
    {
        if (C.TextShadow)
        {
            var p = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddText(p + new Vector2(1.5f, 1.5f), 0xE0000000, text);
        }
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private IDisposable PushFont(float sizePx)
    {
        var handle = _plugin.Fonts.Get(sizePx, C.OverlayFontFamily, C.OverlayFontBold, C.OverlayFontItalic);
        if (handle is { Available: true })
            return handle.Push();
        ImGui.SetWindowFontScale(MathF.Max(0.5f, sizePx / 18f));
        return new ResetFontScale();
    }

    private sealed class ResetFontScale : IDisposable
    {
        public void Dispose() => ImGui.SetWindowFontScale(1f);
    }

    private void SavePositionIfDragged()
    {
        if (EffectiveLocked) return;
        var viewport = ImGui.GetMainViewport();
        var current = ImGui.GetWindowPos();
        var center = new Vector2(current.X + ImGui.GetWindowWidth() * 0.5f, current.Y);
        var frac = (center - viewport.WorkPos) / viewport.WorkSize;
        if ((frac - C.TimelinePosition).LengthSquared() > 0.0000001f)
        {
            C.TimelinePosition = frac;
            C.Save();
        }
    }
}
