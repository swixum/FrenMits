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

        // Same as the main overlay: pinned center-anchored whenever a drag can't
        // be in progress, so the auto-resizing list can't wander as it re-widths.
        if (EffectiveLocked || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
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
        if (Plugin.CutsceneActive) return false; // hide while a cutscene is playing
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
                if (C.UpcomingStyle == 1)
                {
                    DrawBoardPreview();
                }
                else
                {
                    Row(Icons.ResolveFromText("Addle"), "+12s  ", "Addle");
                    Row(Icons.ResolveFromText("Rampart"), "+28s  ", "Rampart");
                    Row(Icons.ResolveFromText("Reprisal"), "+41s  ", "Reprisal", true);
                }
            }
            return;
        }

        var fight = _plugin.ActiveFight();
        if (fight == null) return;

        var job = _plugin.ActiveJobAbbreviation();
        // Cue clock, same as the main call overlay, so the hand-off between this
        // list and the live call stays seamless when a timer offset is set.
        var elapsed = _plugin.CueClockFor(fight);

        if (C.UpcomingStyle == 1)
        {
            using (PushFont(C.UpcomingFontSizePx))
                DrawBoard(fight, job, elapsed);
            return;
        }

        // Show lines that are beyond their lead window (a line inside its lead is on
        // the main call, so it isn't duplicated here) and within the look-ahead.
        var upcoming = fight.OrderedLines
            .Where(l => l.Enabled && l.AppliesTo(job)
                        && l.CueTime - elapsed > (l.LeadOverride > 0f ? l.LeadOverride : C.WarningSeconds)
                        && l.CueTime - elapsed <= C.UpcomingLookaheadSeconds)
            .OrderBy(l => l.CueTime) // fire order, so offsets can't cut the soonest call
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
                var inSec = (int)MathF.Round(l.CueTime - elapsed);
                var name = string.IsNullOrWhiteSpace(l.Action) ? l.Mechanic : Icons.DisplayAction(l.ActionFor(job), job);
                var icon = C.ShowAbilityIcon ? Icons.For(l, job) : 0u;
                // Mark a mit that won't be off cooldown by the time it's called:
                // only the ABILITY dims, so the time and the rest of the line
                // stay fully readable.
                var notReady = C.CooldownAwareCalls
                    && Cooldowns.Remaining(l.Action) is { } cd && cd > (l.CueTime - elapsed) + 0.5f;
                Row(icon, $"+{inSec}s  ", name + (notReady ? "  (cd)" : ""), notReady);
            }
    }

    // ---- mechanic board ----------------------------------------------------
    // The board style: every upcoming mechanic is a draining countdown bar
    // (name left, seconds right), with YOUR presses written under the rows they
    // belong to. Your next press glows gold; when its warning window opens it
    // turns green, matching the moment the main call fires. Row notes from the
    // sheet ride under the highlighted row.

    // Board palette (ABGR).
    private const uint BoardBarBack = 0xC01A1512;
    private const uint BoardBarFill = 0xB89E703A;
    private const uint BoardBarBorder = 0x26FFFFFF;
    private const uint BoardGold = 0xFF28BEFF;
    private const uint BoardGreen = 0xFF69EB5F;
    private const uint BoardBright = 0xFFF8F4F0;
    private const uint BoardMuted = 0xFFC4BAB2;

    // The fight's full mechanic list is derived from every column of its sheet,
    // so it's cached: rebuilt when the fight or pull changes, and refreshed out
    // of combat so sheet edits show up while you're arranging things.
    private List<SheetTimeline.MechRow> _board = new();
    private string _boardFightId = "";
    private int _boardGen = -1;
    private DateTime _boardBuiltAt = DateTime.MinValue;

    private List<SheetTimeline.MechRow> BoardRows(FightProfile fight)
    {
        var stale = _boardFightId != fight.Id
                    || _boardGen != _plugin.Timer.Generation
                    || (!Plugin.InCombat && (DateTime.Now - _boardBuiltAt).TotalSeconds > 4);
        if (stale)
        {
            _board = SheetTimeline.Build(fight);
            _boardFightId = fight.Id;
            _boardGen = _plugin.Timer.Generation;
            _boardBuiltAt = DateTime.Now;
        }
        return _board;
    }

    private static readonly List<MitLine> NoLines = new();

    private void DrawBoard(FightProfile fight, string? job, float elapsed)
    {
        var look = MathF.Max(10f, C.UpcomingBoardLookaheadSeconds);
        var width = MathF.Max(180f, C.UpcomingBoardWidth);
        // A just-hit row lingers 2s at "now" so it doesn't vanish mid-press.
        var windowRows = BoardRows(fight)
            .Where(r => r.Time - elapsed >= -2f && r.Time - elapsed <= look)
            .ToList();

        if (C.UpcomingShowHeader) DrawBoardHeader(fight.Name, elapsed, width);

        // Attach each of your presses to its single NEAREST row, so a mechanic
        // repeating a few seconds apart can't show one press under both bars.
        // The 2.5s window still catches job extras riding ~1s off their row.
        var mineByRow = new Dictionary<SheetTimeline.MechRow, List<MitLine>>();
        foreach (var l in fight.OrderedLines)
        {
            if (!l.Enabled || !l.AppliesTo(job)) continue;
            if (l.Time < elapsed - 6f || l.Time > elapsed + look + 4f) continue;
            SheetTimeline.MechRow? best = null;
            var bestGap = 2.5f;
            foreach (var r in windowRows)
            {
                var gap = MathF.Abs(l.Time - r.Time);
                if (gap < bestGap && SheetTimeline.MechEquals(l.Mechanic, r.Mechanic)) { best = r; bestGap = gap; }
            }
            if (best == null) continue;
            if (!mineByRow.TryGetValue(best, out var list)) mineByRow[best] = list = new List<MitLine>();
            list.Add(l);
        }

        List<MitLine> MineFor(SheetTimeline.MechRow r)
            => mineByRow.TryGetValue(r, out var list) ? list : NoLines;

        // "Just my own mits": trim to the rows you actually press on, BEFORE
        // the row cap, so your later presses aren't crowded out by other hits.
        var visible = (C.UpcomingBoardOnlyMine
                ? windowRows.Where(r => MineFor(r).Count > 0)
                : windowRows)
            .Take(Math.Max(1, C.UpcomingBoardRows))
            .ToList();

        if (visible.Count == 0)
        {
            ImGui.Dummy(new Vector2(width, 1f));
            return;
        }

        var mine = visible.Select(MineFor).ToList();
        float LeadFor(int i) => mine[i].Count == 0
            ? C.WarningSeconds
            : mine[i].Min(l => l.LeadOverride > 0f ? l.LeadOverride : C.WarningSeconds);

        // Gold marks your next press that isn't already green: while a call is
        // in (or just past) its window, the one after it keeps its own marker.
        var nextIdx = -1;
        for (var i = 0; i < visible.Count && nextIdx < 0; i++)
            if (mine[i].Count > 0 && visible[i].Time - elapsed > LeadFor(i))
                nextIdx = i;

        for (var i = 0; i < visible.Count; i++)
        {
            if (i > 0) ImGui.Dummy(new Vector2(1f, 4f));
            var r = visible[i];
            var rem = r.Time - elapsed;
            var useNow = mine[i].Count > 0 && rem <= LeadFor(i);
            var isNext = i == nextIdx;
            var accent = useNow ? BoardGreen : isNext ? BoardGold : 0u;

            // A row with no mechanic label (a bare user timer) is named by the
            // press itself, so its action doesn't repeat underneath.
            var name = r.Mechanic;
            var bareTimer = string.IsNullOrWhiteSpace(name);
            if (bareTimer && mine[i].Count > 0)
                name = Icons.DisplayAction(mine[i][0].ActionFor(job), job);

            BoardBar(name, rem, look, width, accent, r.Hurt);

            if (!bareTimer && mine[i].Count > 0)
                BoardActions(mine[i], job, rem, width, accent);

            if ((useNow || isNext) && NoteText(fight, r) is { Length: > 0 } note)
                BoardNote(note, width);
        }
    }

    private void DrawBoardHeader(string name, float elapsed, float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var clock = TimeText(MathF.Max(0f, elapsed));
        var clockW = ImGui.CalcTextSize(clock).X;
        dl.PushClipRect(pos, pos + new Vector2(MathF.Max(40f, width - clockW - 10f), ImGui.GetTextLineHeight() + 2f), true);
        BoardText(dl, pos, 0xFFFFFFFF, name);
        dl.PopClipRect();
        BoardText(dl, pos + new Vector2(width - clockW, 0f), BoardMuted, clock);
        ImGui.Dummy(new Vector2(width, ImGui.GetTextLineHeight() + 5f));
    }

    private void BoardBar(string name, float rem, float look, float width, uint accent, int hurt)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        var barH = MathF.Round(lineH + 8f);
        var p0 = ImGui.GetCursorScreenPos();
        var p1 = p0 + new Vector2(width, barH);

        dl.AddRectFilled(p0, p1, BoardBarBack, 4f);
        // The fill drains as the hit approaches: full at the look-ahead edge,
        // empty at the hit, so bar length IS time at a glance.
        var frac = Math.Clamp(rem / look, 0f, 1f);
        if (frac > 0.004f)
        {
            var fill = accent == 0 ? BoardBarFill : (accent & 0x00FFFFFF) | 0x73000000;
            dl.AddRectFilled(p0, new Vector2(p0.X + width * frac, p1.Y), fill, 4f);
        }
        dl.AddRect(p0, p1, BoardBarBorder, 4f);

        var textCol = accent == 0 ? BoardBright : accent;
        var textY = p0.Y + (barH - lineH) * 0.5f;
        var timeText = rem < 0f ? "now" : $"{MathF.Ceiling(rem):0}s";
        var timeW = ImGui.CalcTextSize(timeText).X;

        // Clip the name so a long mechanic can't run under the countdown.
        dl.PushClipRect(p0, new Vector2(p1.X - timeW - 14f, p1.Y), true);
        BoardText(dl, new Vector2(p0.X + 8f, textY), textCol, name);
        // Severity marks from a graded custom sheet: ! light, !! hurts, !!! deadly.
        if (hurt > 0)
        {
            var markCol = hurt >= 3 ? 0xFF4646FFu : hurt == 2 ? 0xFF008CFFu : 0xFF00D7FFu;
            BoardText(dl, new Vector2(p0.X + 8f + ImGui.CalcTextSize(name).X + 6f, textY),
                markCol, new string('!', Math.Min(3, hurt)));
        }
        dl.PopClipRect();

        BoardText(dl, new Vector2(p1.X - timeW - 8f, textY), textCol, timeText);
        ImGui.Dummy(new Vector2(width, barH));
    }

    private void BoardActions(List<MitLine> mine, string? job, float rem, float width, uint accent)
    {
        var parts = new List<string>();
        var icon = 0u;
        foreach (var l in mine)
        {
            var text = Icons.DisplayAction(l.ActionFor(job), job);
            if (string.IsNullOrWhiteSpace(text)) continue;
            // Cooldown-aware: flag a press that won't be back up by the hit.
            if (C.CooldownAwareCalls && Cooldowns.Remaining(l.Action) is { } cd && cd > rem + 0.5f)
                text += " (cd)";
            if (!parts.Contains(text)) parts.Add(text);
            // Icon from the first line that actually contributes text.
            if (icon == 0 && C.ShowAbilityIcon) icon = Icons.For(l, job);
        }
        if (parts.Count == 0) return;
        BoardActionText(string.Join(" + ", parts), icon, accent, width);
    }

    private void BoardActionText(string text, uint iconId, uint accent, float width)
    {
        var color = accent == 0 ? BoardMuted : accent;
        var startX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(startX + 8f);
        if (iconId != 0)
        {
            var lineH = ImGui.GetTextLineHeight();
            Icons.Draw(iconId, new Vector2(lineH, lineH));
            ImGui.SameLine(0, 5f);
        }
        ImGui.PushTextWrapPos(startX + width - 4f);
        DrawText(text, color);
        ImGui.PopTextWrapPos();
    }

    private void BoardNote(string note, float width)
    {
        var startX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(startX + 8f);
        ImGui.PushTextWrapPos(startX + width - 4f);
        DrawText(note, (BoardMuted & 0x00FFFFFF) | 0xA0000000);
        ImGui.PopTextWrapPos();
    }

    // Draw-list text with the overlay's readability shadow.
    private void BoardText(ImDrawListPtr dl, Vector2 pos, uint color, string text)
    {
        if (C.TextShadow) dl.AddText(pos + new Vector2(1.5f, 1.5f), 0xE0000000, text);
        dl.AddText(pos, color, text);
    }

    private static string NoteText(FightProfile fight, SheetTimeline.MechRow r)
        => fight.Notes.FirstOrDefault(n => SheetTimeline.MechEquals(n.Mechanic, r.Mechanic)
                                           && MathF.Abs(n.Time - r.Time) < 4f)?.Text ?? "";

    private static string TimeText(float seconds)
    {
        var t = (int)MathF.Round(seconds);
        return $"{t / 60}:{t % 60:00}";
    }

    // Placement preview for Live preview mode: a static sample board.
    private void DrawBoardPreview()
    {
        var look = MathF.Max(10f, C.UpcomingBoardLookaheadSeconds);
        var width = MathF.Max(180f, C.UpcomingBoardWidth);
        if (C.UpcomingShowHeader) DrawBoardHeader("FrenMits", 2f, width);
        BoardBar("Heavy raidwide", 9f, look, width, BoardGold, 0);
        BoardActionText("Reprisal", C.ShowAbilityIcon ? Icons.ResolveFromText("Reprisal") : 0u, BoardGold, width);
        if (!C.UpcomingBoardOnlyMine)
        {
            ImGui.Dummy(new Vector2(1f, 4f));
            BoardBar("Tank buster", 16f, look, width, 0u, 0);
            ImGui.Dummy(new Vector2(1f, 4f));
            BoardBar("Adds spawn", 24f, look, width, 0u, 0);
        }
        ImGui.Dummy(new Vector2(1f, 4f));
        BoardBar("Big raidwide", 31f, look, width, 0u, 2);
        BoardActionText("Party Mit", C.ShowAbilityIcon ? Icons.ResolveFromText("Addle") : 0u, 0u, width);
    }

    private void Row(uint iconId, string prefix, string name, bool dimName = false)
    {
        var color = C.OverlayColorUpcoming;
        // The dim variant of the upcoming colour: same hue, 40% alpha.
        var dimColor = (color & 0x00FFFFFFu) | ((uint)(((color >> 24) & 0xFF) * 0.4f) << 24);

        var lineH = ImGui.GetTextLineHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var total = (iconId != 0 ? lineH + spacing : 0f)
                    + ImGui.CalcTextSize(prefix).X + ImGui.CalcTextSize(name).X;
        var offset = (ImGui.GetContentRegionAvail().X - total) * 0.5f;
        if (offset > 0) ImGui.SetCursorPosX(MathF.Round(ImGui.GetCursorPosX() + offset));

        if (iconId != 0)
        {
            if (dimName) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.4f);
            Icons.Draw(iconId, new Vector2(lineH, lineH));
            if (dimName) ImGui.PopStyleVar();
            ImGui.SameLine(0, spacing);
        }
        DrawText(prefix, color);
        ImGui.SameLine(0, 0);
        DrawText(name, dimName ? dimColor : color);
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
        // Only capture during a REAL drag of this window (focused + mouse drag):
        // the anchor derives from the window CENTER, and AlwaysAutoResize width
        // changes during any stray left-button hold (camera turns) would
        // otherwise be saved as position drift.
        if (!ImGui.IsMouseDragging(ImGuiMouseButton.Left) || !ImGui.IsWindowFocused()) return;
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
