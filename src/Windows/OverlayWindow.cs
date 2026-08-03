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

    // The press being counted down, and the run it belongs to.
    private readonly List<MitPress> _activeLines = new();
    private int _lastGen = -1;

    // Per-frame scratch for this job's presses and the call group.
    private readonly List<MitPress> _lines = new();
    private readonly List<MitPress> _group = new();

    // Stable, so calls tied on the clock keep their baked order.
    private static void StableSortByCueTime(List<MitPress> lines)
    {
        for (var i = 1; i < lines.Count; i++)
        {
            var l = lines[i];
            var j = i - 1;
            while (j >= 0 && lines[j].WindowStart > l.WindowStart) { lines[j + 1] = lines[j]; j--; }
            lines[j + 1] = l;
        }
    }

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
        // No title bar ever, or the content jumps when you lock it.
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

        // Pin to the saved spot, except while the mouse is held.
        if (EffectiveLocked || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.0f));
            _applyPos = true; // re-apply the moment a drag ends / on reset
        }
        else if (_applyPos)
        {
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.0f));
            _applyPos = false;
        }
    }

    private bool _applyPos = true;

    // Locked if you ticked the lock or you're in a live pull.
    private bool EffectiveLocked => OverlayChrome.Locked(C.OverlayLocked, C);

    // Snap back to the saved position next frame.
    public void RequestReposition() => _applyPos = true;

    public override void PostDraw()
    {
        if (C.ShowBackground)
            ImGui.PopStyleColor();
    }

    public override bool DrawConditions()
    {
        // Test mode shows the sample, never a universal timeline.
        if (C.TestMode)
            return _plugin.ActiveFight() is not { TimelineOnly: true } || !_plugin.Timer.Live;
        if (Plugin.CutsceneActive) return false; // hide while a cutscene is playing
        if (_plugin.Cues.Holding) return false; // and until the post-cutscene resync lands
        if (_plugin.ActiveFight() is not { } fight) return false;
        if (fight.TimelineOnly) return false; // board-only: no center call
        if (C.OnlyInTargetTerritory && fight.TerritoryId != Service.ClientState.TerritoryType) return false;
        return _plugin.Timer.Live;
    }

    // How far before its window opens a call first appears.
    private float LeadFor(MitPress line)
    {
        if (line.SourceLine.LeadOverride > 0f) return line.SourceLine.LeadOverride;
        return C.WarningSeconds;
    }

    public override void Draw()
    {
        SavePositionIfDragged();

        // Right-click menu, only while the overlay takes the mouse.
        if (ImGui.BeginPopupContextWindow("##fmoverlayctx"))
        {
            if (ImGui.MenuItem("Lock position", "", C.OverlayLocked))
            {
                C.OverlayLocked = !C.OverlayLocked;
                C.SaveSettings();
            }
            if (ImGui.MenuItem("Open settings"))
            {
                _plugin.ConfigWindow.IsOpen = true;
                _plugin.ConfigWindow.BringToFront();
            }
            if (ImGui.MenuItem("Open Sheet View"))
            {
                var f = _plugin.ActiveFight();
                _plugin.SheetViewWindow.Open(
                    f != null && (Builtin.Has(f.TerritoryId) || f.CustomSlots.Count > 0) ? f : null);
            }
            if (ImGui.MenuItem("Open Mit Tuner"))
                _plugin.MiniSheetWindow.IsOpen = true;
            ImGui.EndPopup();
        }

        if (C.TestMode && !_plugin.Timer.Live)
        {
            if (C.OverlayStyle == 1)
                using (PushFont(C.OverlayFontSizePx))
                {
                    var w = BoardWidth();
                    w = FitBoardWidth(w, "Reprisal", 1.4f, true);
                    w = FitBoardWidth(w, "Feint", 3.2f, true);
                    DrawBoardCall("Wave Cannon", "Reprisal", 1.4f, true, 0, C.WarningSeconds, 0.5f, 0.28f, Icons.ResolveFromText("Reprisal"), w);
                    ImGui.Dummy(new Vector2(1f, 4f));
                    DrawBoardCall("Wave Cannon", "Feint", 3.2f, true, 0, C.WarningSeconds, 0.5f, 0.28f, Icons.ResolveFromText("Feint"), w);
                }
            else if (C.OverlayStyle == 2)
            {
                var d = IconClockDiameter();
                DrawIconClock(Icons.ResolveFromText("Reprisal"), "Reprisal", 1.4f, true, C.WarningSeconds, 0.5f, 0.28f, 0, d);
                ImGui.SameLine(0, 10f);
                DrawIconClock(Icons.ResolveFromText("Feint"), "Feint", 3.2f, true, C.WarningSeconds, 0.5f, 0.28f, 0, d);
            }
            else
                DrawCurrent("Reprisal / Feint", "Reprisal", 1.4f, true, 0, C.WarningSeconds, 0.5f, 0.28f,
                    Icons.ResolveFromText("Reprisal"));
            return;
        }

        var fight = _plugin.ActiveFight();
        if (fight == null) return;

        (float RemNew, float LeadNew, bool Hidden) GetDynamicTiming(MitPress call, float currentElapsed)
        {
            var rem = call.WindowStart - currentElapsed;
            var lead = LeadFor(call);

            // Before the window at all, so a wipe or a resync drops the latch.
            if (rem > lead)
            {
                call.ComputedDelay = null;
            }

            var cd = Cooldowns.Remaining(call.MitName) ?? 0f;

            // Gone until well past the window, which is also how a press hides it.
            if (currentElapsed + cd > call.WindowEnd + 5.0f)
            {
                call.ComputedDelay = null;
                return (rem, lead, true);
            }

            // What the delay would be right now.
            var freshDelay = 0f;
            if (cd > 0f)
            {
                freshDelay = MathF.Max(0f, currentElapsed + cd - call.WindowStart);
            }

            // Latch it as the call spawns, so the countdown never jumps after.
            if (!call.ComputedDelay.HasValue && freshDelay > 0f)
            {
                if (rem + freshDelay <= lead)
                {
                    call.ComputedDelay = freshDelay;
                }
            }

            var delay = call.ComputedDelay ?? freshDelay;

            return (rem + delay, lead, false);
        }

        var job = _plugin.ActiveJobAbbreviation();
        var elapsed = _plugin.CueClockFor(fight); // call schedule, not sheet position

        // A reused buffer, since the overlay redraws continuously.
        var lines = _lines;
        lines.Clear();
        foreach (var l in _plugin.ActivePresses())
            if (l.SourceLine.Enabled && l.SourceLine.AppliesTo(job)) lines.Add(l);

        // Reset the held call on a new run, so nothing carries over.
        if (_plugin.Timer.Generation != _lastGen) { _lastGen = _plugin.Timer.Generation; _activeLines.Clear(); }

        // The calls we count to: the soonest, plus anything tied.
        var bestRemaining = float.MaxValue;
        foreach (var line in lines)
        {
            var dt = GetDynamicTiming(line, elapsed);
            if (dt.Hidden) continue;
            var remaining = dt.RemNew;
            var lead = dt.LeadNew;
            if (remaining < 0f || remaining > lead) continue;
            if (remaining < bestRemaining) bestRemaining = remaining;
        }

        const float tieWindow = 0.75f; // lines within this of the soonest stack together
        var group = _group;
        group.Clear();
        if (bestRemaining < float.MaxValue)
        {
            foreach (var l in lines)
            {
                var dt = GetDynamicTiming(l, elapsed);
                if (dt.Hidden) continue;
                var rem = dt.RemNew;
                if (rem >= 0f && rem <= dt.LeadNew && rem <= bestRemaining + tieWindow) group.Add(l);
            }
            StableSortByCueTime(group);
            // Keep an open call up for its window and hold, stacked with the next.
            var heldCount = 0;
            foreach (var l in _activeLines)
            {
                var dt = GetDynamicTiming(l, elapsed);
                if (dt.Hidden) continue;
                var activeDur = l.WindowEnd - l.WindowStart;
                var remNew = dt.RemNew;
                var activePhase = remNew <= 0f && remNew >= -activeDur;
                var pastPhase = remNew < -activeDur && remNew >= -activeDur - C.HoldSeconds;
                if ((activePhase || pastPhase) && !group.Contains(l))
                { group.Insert(heldCount++, l); }
            }
            if (heldCount > 0) StableSortByCueTime(group);
            _activeLines.Clear();
            _activeLines.AddRange(group); // remember what we're actively counting down
        }
        else
        {
            // Nothing upcoming: hold what we counted, never a skipped call.
            foreach (var l in _activeLines)
            {
                var dt = GetDynamicTiming(l, elapsed);
                if (dt.Hidden) continue;
                var activeDur = l.WindowEnd - l.WindowStart;
                var remNew = dt.RemNew;
                var activePhase = remNew <= 0f && remNew >= -activeDur;
                var pastPhase = remNew < -activeDur && remNew >= -activeDur - C.HoldSeconds;
                if (activePhase || pastPhase) group.Add(l);
            }
            StableSortByCueTime(group);
            if (group.Count == 0) _activeLines.Clear();
        }

        if (C.OverlayStyle == 1)
        {
            using (PushFont(C.OverlayFontSizePx))
            {
                var width = BoardWidth();
                foreach (var call in group)
                {
                    var dt = GetDynamicTiming(call, elapsed);
                    if (dt.Hidden) continue;
                    width = FitBoardWidth(width, Icons.DisplayAction(call.MitName, job),
                        MathF.Max(0f, dt.RemNew), dt.RemNew > 0f);
                }
                for (var i = 0; i < group.Count; i++)
                {
                    if (i > 0) ImGui.Dummy(new Vector2(1f, 4f));
                    var call = group[i];
                    var dt = GetDynamicTiming(call, elapsed);
                    if (dt.Hidden) continue;
                    var remaining = dt.RemNew;
                    var activeDur = call.WindowEnd - call.WindowStart;
                    var imminent = remaining > 0f;
                    var baseLead = LeadFor(call);
                    var delay = MathF.Max(0f, remaining - (call.WindowStart - elapsed));
                    var totalDur = baseLead + activeDur - delay;
                    var activeRem = call.WindowEnd - elapsed;
                    var barFrac = totalDur > 0.01f ? Math.Clamp(activeRem / totalDur, 0f, 1f) : 0f;
                    var tickFrac = totalDur > 0.01f ? Math.Clamp((activeDur - delay) / totalDur, 0f, 1f) : 0f;
                    var lead = dt.LeadNew;
                    var icon = C.ShowAbilityIcon ? Icons.ForMitPress(call, job) : 0u;
                    var action = Icons.DisplayAction(call.MitName, job);
                    DrawBoardCall(call.SourceLine.Mechanic, action, MathF.Max(0f, remaining), imminent,
                        call.SourceLine.Color, lead, barFrac, tickFrac, icon, width);
                }
            }
            return;
        }

        if (C.OverlayStyle == 2)
        {
            var d = IconClockDiameter();
            for (var i = 0; i < group.Count; i++)
            {
                if (i > 0) ImGui.SameLine(0, 10f);
                var call = group[i];
                var dt = GetDynamicTiming(call, elapsed);
                if (dt.Hidden) continue;
                var remaining = dt.RemNew;
                var activeDur = call.WindowEnd - call.WindowStart;
                var imminent = remaining > 0f;
                var baseLead = LeadFor(call);
                var delay = MathF.Max(0f, remaining - (call.WindowStart - elapsed));
                var totalDur = baseLead + activeDur - delay;
                var activeRem = call.WindowEnd - elapsed;
                var barFrac = totalDur > 0.01f ? Math.Clamp(activeRem / totalDur, 0f, 1f) : 0f;
                var tickFrac = totalDur > 0.01f ? Math.Clamp((activeDur - delay) / totalDur, 0f, 1f) : 0f;
                var lead = dt.LeadNew;
                var action = Icons.DisplayAction(call.MitName, job);
                DrawIconClock(Icons.ForMitPress(call, job), action, remaining, imminent,
                    lead, barFrac, tickFrac, call.SourceLine.Color, d);
            }
            return;
        }

        for (var i = 0; i < group.Count; i++)
        {
            if (i > 0) ImGui.Spacing();
            var call = group[i];
            var dt = GetDynamicTiming(call, elapsed);
            if (dt.Hidden) continue;
            var remaining = dt.RemNew;
            var activeDur = call.WindowEnd - call.WindowStart;
            var imminent = remaining > 0f;
            var baseLead = LeadFor(call);
            var delay = MathF.Max(0f, remaining - (call.WindowStart - elapsed));
            var totalDur = baseLead + activeDur - delay;
            var activeRem = call.WindowEnd - elapsed;
            var barFrac = totalDur > 0.01f ? Math.Clamp(activeRem / totalDur, 0f, 1f) : 0f;
            var tickFrac = totalDur > 0.01f ? Math.Clamp((activeDur - delay) / totalDur, 0f, 1f) : 0f;
            var lead = dt.LeadNew;
            var icon = C.ShowAbilityIcon ? Icons.ForMitPress(call, job) : 0u;
            var action = Icons.DisplayAction(call.MitName, job);
            DrawCurrent(call.SourceLine.Mechanic, action, remaining, imminent, call.SourceLine.Color, lead, barFrac, tickFrac, icon);
        }
    }

    // ---- board style ----

    // Board palette, on the same config keys as the board itself.
    private uint BoardAccent => C.UpcomingBoardAccentColor != 0 ? C.UpcomingBoardAccentColor : 0xFFF6823B;
    private uint BoardNow => C.UpcomingBoardNowColor != 0 ? C.UpcomingBoardNowColor : 0xFF64DC64;
    private const uint BoardBright = 0xFFECE8E6;
    private const uint BoardMuted = 0xFFA89A90;
    private const uint BoardBorder = 0x66594A3F;
    private const uint BoardPanelRgb = 0x0014110E;

    private float BoardRound => Math.Clamp(C.UpcomingBoardRounding, 0f, 12f);
    private uint BoardPanel => ((uint)(Math.Clamp(C.UpcomingBoardBgOpacity, 0f, 1f) * 255f) << 24) | BoardPanelRgb;

    // The board's layered fill: a solid base, a gradient body, a crisp moving edge.
    private static void BoardFill(ImDrawListPtr dl, Vector2 p0, Vector2 p1, float frac, uint color, float round)
    {
        if (frac <= 0.004f) return;
        var rgb = color & 0x00FFFFFF;
        var edgeX = p0.X + (p1.X - p0.X) * frac;
        var corners = frac >= 0.999f ? ImDrawFlags.RoundCornersAll : ImDrawFlags.RoundCornersLeft;
        dl.AddRectFilled(p0, new Vector2(edgeX, p1.Y), rgb | 0x66000000, round, corners);
        dl.AddRectFilledMultiColor(p0, new Vector2(edgeX, p1.Y),
            rgb | 0x14000000, rgb | 0x7A000000, rgb | 0x7A000000, rgb | 0x14000000);
        if (frac > 0.02f && frac < 0.985f)
            dl.AddRectFilled(new Vector2(edgeX - 1.5f, p0.Y + 1f),
                new Vector2(edgeX + 0.5f, p1.Y - 1f), rgb | 0xF0000000);
    }

    // A uniform bar width, so stacked bars line up.
    private float BoardWidth()
    {
        return MathF.Max(170f, C.ProgressBarWidthPx);
    }

    // Bars grow to fit their text; the set width is the minimum, so short calls sit still.
    private float FitBoardWidth(float width, string action, float remaining, bool imminent)
    {
        var lineH = ImGui.GetTextLineHeight();
        var iconW = C.ShowAbilityIcon ? MathF.Round(lineH * Math.Clamp(C.IconScale, 0.4f, 1.5f)) + 8f : 0f;
        var timeText = imminent ? $"{MathF.Ceiling(remaining):0}s" : "NOW";
        // The time slot never measures under two digits, so a 10s to 9s tick can't wiggle the bar.
        var timeW = MathF.Max(ImGui.CalcTextSize(timeText).X, ImGui.CalcTextSize("88s").X);
        var need = 10f + iconW + ImGui.CalcTextSize(action).X + 24f + timeW + 10f;
        return MathF.Max(width, MathF.Ceiling(need));
    }

    private void DrawBoardCall(string mechanic, string action, float remaining, bool imminent,
        uint colorOverride, float lead, float barFrac, float tickFrac, uint iconId, float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        var barH = MathF.Round(lineH + 12f);
        const float round = 6f;
        var p0 = ImGui.GetCursorScreenPos();
        var p1 = p0 + new Vector2(width, barH);

        var typeColor = C.ColorByMitType ? MitTypes.Color(MitTypes.Classify(action, mechanic), C) : 0u;
        var baseCol = colorOverride != 0 ? colorOverride
            : typeColor != 0 ? typeColor
            : BoardAccent;
        // At go-time the whole bar goes green, like the board.
        var barCol = imminent ? baseCol : BoardNow;

        // Panel.
        var back = ((uint)(Math.Clamp(C.UpcomingBoardBgOpacity, 0f, 1f) * 255f) << 24) | BoardPanelRgb;
        dl.AddRectFilled(p0, p1, back, round);

        // Draining countdown fill (full at the lead, empty at the call).
        if (lead > 0.01f)
        {
            BoardFill(dl, p0, p1, barFrac, barCol, round);
            // The mark the fill reaches as the press first becomes usable.
            if (imminent && tickFrac > 0.001f && tickFrac < 0.999f)
            {
                var tickX = p0.X + width * tickFrac;
                dl.AddLine(new Vector2(tickX, p0.Y), new Vector2(tickX, p1.Y), 0x80FFFFFF, 2f);
            }
        }

        // Left accent stripe, pulsing at go time.
        var stripe = barCol;
        if (imminent && C.PulseWhenImminent && remaining < 1.5f) stripe = Pulse(stripe);
        dl.AddRectFilled(p0, new Vector2(p0.X + 3f, p1.Y), stripe, round, ImDrawFlags.RoundCornersLeft);
        dl.AddRect(p0, p1, BoardBorder, round);

        var cy = p0.Y + (barH - lineH) * 0.5f;
        var nameX = p0.X + 10f;
        if (iconId != 0)
        {
            var iconH = MathF.Round(lineH * Math.Clamp(C.IconScale, 0.4f, 1.5f));
            ImGui.SetCursorScreenPos(new Vector2(nameX, p0.Y + (barH - iconH) * 0.5f));
            Icons.Draw(iconId, new Vector2(iconH, iconH));
            nameX += iconH + 8f;
        }

        var textCol = imminent ? (colorOverride != 0 ? colorOverride : BoardBright) : BoardNow;
        BoardText(dl, new Vector2(nameX, cy), textCol, action);
        var timeText = imminent ? $"{MathF.Ceiling(remaining):0}s" : "NOW";
        var timeW = ImGui.CalcTextSize(timeText).X;
        BoardText(dl, new Vector2(p1.X - timeW - 10f, cy), textCol, timeText);

        // Reserve the bar in layout, then the muted sublines beneath it.
        ImGui.SetCursorScreenPos(p0);
        ImGui.Dummy(new Vector2(width, barH));

        var subX = nameX - p0.X;
        if (C.ShowMechanicLine && !string.IsNullOrWhiteSpace(mechanic)
            && !string.Equals(mechanic, action, StringComparison.OrdinalIgnoreCase))
            using (PushFont(C.OverlayFontSizePx * 0.5f))
                SubText(mechanic, BoardMuted, subX);
    }

    // Draw-list text with a readability shadow.
    private void BoardText(ImDrawListPtr dl, Vector2 pos, uint color, string text)
    {
        if (C.TextShadow) dl.AddText(pos + new Vector2(1.5f, 1.5f), 0xE0000000, text);
        dl.AddText(pos, color, text);
    }

    // A small left-indented subline under a board bar.
    private void SubText(string text, uint color, float indent)
    {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
        if (C.TextShadow)
        {
            var p = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddText(p + new Vector2(1f, 1f), 0xE0000000, text);
        }
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    // ---- icon and clock style ----

    private float IconClockDiameter()
        => MathF.Round(Math.Clamp(C.OverlayFontSizePx * 2.4f, 40f, 220f));

    private void DrawIconClock(uint iconId, string action, float remaining, bool imminent,
        float lead, float barFrac, float tickFrac, uint colorOverride, float diam)
    {
        var dl = ImGui.GetWindowDrawList();
        var box = ImGui.GetCursorScreenPos();
        // The rim arc rides outside the tile, so inset the tile to leave it room.
        var rimW = MathF.Max(2f, diam * 0.05f);
        var pad = rimW * 2f;              // keeps the arc AND its notch inside the box
        var tile = MathF.Max(8f, diam - pad * 2f);
        var p0 = box + new Vector2(pad, pad);
        var p1 = p0 + new Vector2(tile, tile);
        var center = p0 + new Vector2(tile * 0.5f, tile * 0.5f);
        var accent = colorOverride != 0 ? colorOverride : BoardAccent;
        var rounding = tile * 0.14f;
        const float Top = -MathF.PI * 0.5f;   // 12 o'clock
        var frac = Math.Clamp(barFrac, 0f, 1f);

        // The icon itself (or a themed disc when it can't be resolved).
        if (iconId != 0)
        {
            ImGui.SetCursorScreenPos(p0);
            Icons.Draw(iconId, new Vector2(tile, tile));
        }
        else
        {
            dl.AddRectFilled(p0, p1, (accent & 0x00FFFFFF) | 0xB4000000, rounding);
        }

        // Cooldown sweep: a dark wedge growing clockwise.
        if (lead > 0.01f)
        {
            var covered = 1f - frac;
            if (covered > 0.001f)
            {
                dl.PathLineTo(center);
                dl.PathArcTo(center, tile * 0.72f, Top, Top + covered * MathF.PI * 2f, 96);
                dl.PathFillConvex(0xC0000000);
            }
        }

        // Once the window is open the tile washes green, like the board's NOW badge.
        if (!imminent)
        {
            var beat = C.PulseWhenImminent ? MathF.Sin((float)ImGui.GetTime() * 10f) * 0.5f + 0.5f : 1f;
            dl.AddRectFilled(p0, p1, (BoardNow & 0x00FFFFFF) | ((uint)(0x20 + 0x38 * beat) << 24), rounding);
        }

        // The rim arc: what is left of the lead and the window, with a bright head.
        var rimR = tile * 0.5f + pad * 0.4f;
        dl.PathArcTo(center, rimR, Top, Top + MathF.PI * 2f, 64);
        dl.PathStroke(0x30FFFFFF, ImDrawFlags.None, rimW);
        if (lead > 0.01f && frac > 0.004f)
        {
            var arcCol = ((imminent ? accent : BoardNow) & 0x00FFFFFF) | 0xF0000000;
            dl.PathArcTo(center, rimR, Top, Top + frac * MathF.PI * 2f, 64);
            dl.PathStroke(arcCol, ImDrawFlags.None, rimW);
            var head = Top + frac * MathF.PI * 2f;
            dl.AddCircleFilled(center + new Vector2(MathF.Cos(head), MathF.Sin(head)) * rimR,
                rimW * 0.6f, 0xF0FFFFFF);
        }

        // The notch the arc reaches as the press first becomes usable.
        if (imminent && tickFrac > 0.001f && tickFrac < 0.999f)
        {
            var a = Top + tickFrac * MathF.PI * 2f;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            dl.AddLine(center + dir * (rimR - rimW), center + dir * (rimR + rimW), 0xB0FFFFFF, 2f);
        }

        // A spark on that notch as the window opens.
        if (remaining <= 0.05f && remaining > -0.55f && tickFrac > 0.001f)
        {
            var a = Top + tickFrac * MathF.PI * 2f;
            // Sized to the room between the rim and the edge of the reserved box.
            var k = Math.Clamp((diam * 0.5f - rimR) / 11f, 0.25f, 1f);
            OverlayChrome.Spark(dl, center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rimR,
                (0.05f - remaining) / 0.6f, accent, k);
        }

        // Border, green + pulsing at go time.
        var ring = imminent ? accent : BoardNow;
        if (!imminent && C.PulseWhenImminent) ring = Pulse(ring);
        dl.AddRect(p0, p1, (ring & 0x00FFFFFF) | 0xE0000000, rounding, ImDrawFlags.None, 2.5f);

        // Centered countdown, outlined so it reads over busy icon art.
        var shown = MathF.Max(0f, remaining);
        var num = !imminent ? "" : shown < 3f ? $"{shown:0.0}" : $"{MathF.Ceiling(shown):0}";
        if (num.Length > 0)
            using (PushFont(MathF.Round(tile * 0.42f)))
            {
                var np = center - ImGui.CalcTextSize(num) * 0.5f;
                for (var oy = -1; oy <= 1; oy++)
                    for (var ox = -1; ox <= 1; ox++)
                        if (ox != 0 || oy != 0)
                            dl.AddText(np + new Vector2(ox * 1.6f, oy * 1.6f), 0xE6000000, num);
                dl.AddText(np, 0xFFFFFFFF, num);
            }

        ImGui.SetCursorScreenPos(box);
        ImGui.Dummy(new Vector2(diam, diam));
    }

    private void DrawCurrent(string mechanic, string action, float remaining, bool imminent,
        uint colorOverride, float lead, float barFrac, float tickFrac, uint iconId = 0)
    {
        var dl = ImGui.GetWindowDrawList();
        var panel = C.OverlayCallPanel;
        // Split so the plate can be drawn behind content that has not been measured yet.
        if (panel) { dl.ChannelsSplit(2); dl.ChannelsSetCurrent(1); }
        var top = ImGui.GetCursorScreenPos();
        if (panel) ImGui.Dummy(new Vector2(1f, 5f));

        // Color priority: override, mit type, then default.
        var typeColor = C.ColorByMitType ? MitTypes.Color(MitTypes.Classify(action, mechanic), C) : 0u;
        var baseColor = colorOverride != 0 ? colorOverride
            : typeColor != 0 ? typeColor
            : (imminent ? C.OverlayColorImminent : C.OverlayColorActive);
        var shown = MathF.Max(0f, remaining);
        var color = imminent && C.PulseWhenImminent && shown < 1.5f ? Pulse(baseColor) : baseColor;
        var headline = FormatHeadline(mechanic, action, shown, imminent);

        // Flag a call whose mit won't be off recast in time.
        if (C.CooldownAwareCalls && imminent && Cooldowns.Remaining(action) is { } cd && cd > shown + 0.5f)
        {
            headline += $"  [CD {MathF.Ceiling(cd):0}s]";
            color = 0xFF3C3CF0; // red-ish warning
        }

        // A depleting ring across the lead and the window; -1 means none.
        var ringFrac = C.ShowRadialRing && lead > 0.01f && barFrac > 0.001f
            ? Math.Clamp(barFrac, 0f, 1f) : -1f;

        // Both marks ride the bar's own geometry, so the text and the bar stay in step.
        TextMarks? marks = null;
        if (C.OverlayTextSpark && lead > 0.01f)
        {
            var avail = ImGui.GetContentRegionAvail().X;
            var barW = C.ShowProgressBar ? MathF.Max(avail, C.ProgressBarWidthPx) : avail;
            var barX = ImGui.GetCursorScreenPos().X;
            var onBar = tickFrac > 0.001f && tickFrac < 0.999f;
            marks = new TextMarks(
                barX + barW * Math.Clamp(barFrac, 0f, 1f),
                onBar ? barX + barW * tickFrac : float.NaN,
                imminent,
                remaining <= 0.05f && remaining > -0.55f ? (0.05f - remaining) / 0.6f : float.NaN);
        }

        using (PushFont(C.OverlayFontSizePx))
            CenteredIconText(iconId, headline, color, ringFrac, baseColor, marks);

        if (C.ShowMechanicLine
            && !string.IsNullOrWhiteSpace(mechanic)
            && !string.Equals(mechanic, action, StringComparison.OrdinalIgnoreCase))
        {
            // Its own countdown, so the mechanic line ticks down too.
            var mechText = imminent
                ? $"{mechanic}   {MathF.Ceiling(shown):0}"
                : mechanic;
            using (PushFont(C.OverlayFontSizePx * 0.55f))
                CenteredText(mechText, C.OverlayColorMechanic);
        }

        if (C.ShowProgressBar && lead > 0.01f)
            DrawProgressBar(barFrac, tickFrac, remaining, color, imminent);

        if (!panel) return;
        ImGui.Dummy(new Vector2(1f, 5f));

        // The plate: the board's panel, border and go-time stripe.
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var q0 = new Vector2(top.X, top.Y);
        var q1 = new Vector2(top.X + width, ImGui.GetCursorScreenPos().Y);
        dl.ChannelsSetCurrent(0);
        var round = BoardRound;
        dl.AddRectFilled(q0, q1, BoardPanel, round);
        var stripe = imminent ? (colorOverride != 0 ? colorOverride : BoardAccent) : BoardNow;
        if (!imminent && C.PulseWhenImminent) stripe = Pulse(stripe);
        dl.AddRectFilled(q0, new Vector2(q0.X + 3f, q1.Y), stripe, round, ImDrawFlags.RoundCornersLeft);
        dl.AddRect(q0, q1, BoardBorder, round);
        dl.ChannelsMerge();
    }

    private void DrawProgressBar(float frac, float tickFrac, float remaining, uint color, bool imminent)
    {
        var width = MathF.Max(ImGui.GetContentRegionAvail().X, C.ProgressBarWidthPx);
        var height = MathF.Max(1f, C.ProgressBarHeight);
        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var far = origin + new Vector2(width, height);
        var round = MathF.Min(BoardRound, height * 0.5f);
        dl.AddRectFilled(origin, far, 0x80202020, round);
        BoardFill(dl, origin, far, frac, color, round);
        dl.AddRect(origin, far, BoardBorder, round);
        // The mark the fill reaches as the press first becomes usable.
        if (imminent && tickFrac > 0.001f && tickFrac < 0.999f)
        {
            var tickX = origin.X + width * tickFrac;
            dl.AddLine(new Vector2(tickX, origin.Y - 1f), new Vector2(tickX, far.Y + 1f), 0xB0FFFFFF, 2f);
        }
        // A spark on that mark as the window opens.
        if (remaining <= 0.05f && remaining > -0.55f && tickFrac > 0.001f)
            OverlayChrome.Spark(dl, new Vector2(origin.X + width * tickFrac, origin.Y + height * 0.5f),
                (0.05f - remaining) / 0.6f, color);
        ImGui.Dummy(new Vector2(width, height));
    }

    // A faint full ring plus a colored arc that shrinks to empty.
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

    // Brightness oscillation for the imminent pulse.
    private static uint Pulse(uint abgr) => OverlayChrome.Pulse(abgr);

    // How far outside the word the lines fade in and back out.
    private const float SparkMargin = 14f;

    // What the classic call draws over its text: the bar's edge, where it stops, and the hit.
    private readonly record struct TextMarks(float LineX, float TickX, bool TickLive, float Burst);

    // Draws the call text, with a light line on the bar's edge crossing it.
    private void CallText(string text, uint color, TextMarks? marks)
    {
        var p = ImGui.GetCursorScreenPos();
        if (C.TextShadow)
            ImGui.GetWindowDrawList().AddText(p + new Vector2(1.5f, 1.5f), 0xE0000000, text);

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
        if (marks is not { } m) return;

        var size = ImGui.CalcTextSize(text);
        var dl = ImGui.GetWindowDrawList();
        // Taller than the glyphs, so it is already fading where the letters end.
        var pad = size.Y * 0.12f;
        var y0 = p.Y - pad;
        var y1 = p.Y + size.Y + pad;
        var core = MathF.Max(1f, MathF.Round(size.Y * 0.03f));
        var halo = MathF.Max(3f, size.Y * 0.16f);

        // The mark the line lands on as the press becomes usable, then the line itself.
        if (m.TickLive) Line(m.TickX, 0x7A, 0x22);
        Line(m.LineX, 0xFF, 0x52);

        // And a spark on that mark the moment the line reaches it.
        if (!float.IsNaN(m.Burst) && Over(m.TickX) > 0f)
            OverlayChrome.Spark(dl, new Vector2(MathF.Round(m.TickX), p.Y + size.Y * 0.5f),
                m.Burst, color, Math.Clamp(size.Y / 32f, 0.5f, 3f));

        // Full strength while it is over the word, fading out past either end.
        float Over(float x)
            => float.IsNaN(x) ? 0f
                : Math.Clamp(MathF.Min(x - (p.X - SparkMargin), p.X + size.X + SparkMargin - x) / SparkMargin, 0f, 1f);

        void Line(float x, uint coreA, uint haloA)
        {
            var t = Over(x);
            if (t <= 0f) return;
            OverlayChrome.Beam(dl, x, y0, y1, halo, color & 0x00FFFFFF, (uint)(haloA * t), true);
            OverlayChrome.Beam(dl, MathF.Round(x), y0, y1, core, 0x00FFFFFF, (uint)(coreA * t), false);
        }
    }

    private string FormatHeadline(string mechanic, string action, float remaining, bool imminent)
    {
        var label = string.IsNullOrWhiteSpace(action) ? mechanic : action;

        // At or after the call time, show NOW instead of a count.
        if (!imminent)
            return label + C.ActiveSuffix;

        // Counting down, in the format template's style.
        var count = MathF.Ceiling(remaining).ToString("0");
        var text = C.HeadlineFormat
            .Replace("{action}", label)
            .Replace("{mechanic}", mechanic)
            .Replace("{time}", TimeText(remaining))
            .Replace("{remaining}", remaining.ToString("0.0"))
            .Replace("{count}", count);

        // Optional append, only when the format has no number.
        if (C.ShowCountdownNumber
            && !C.HeadlineFormat.Contains("{remaining}")
            && !C.HeadlineFormat.Contains("{count}"))
            text = $"{text}   {count}";
        return text;
    }

    private static string TimeText(float seconds) => Fmt.MmssRound(seconds);

    // Push a crisp font handle, falling back to a scale.
    private IDisposable PushFont(float sizePx)
        => OverlayChrome.PushFont(_plugin.Fonts, sizePx, C.OverlayFontFamily, C.OverlayFontBold, C.OverlayFontItalic);

    // Centers an optional icon and the text as one group.
    private void CenteredIconText(uint iconId, string text, uint color, float ringFrac = -1f, uint ringColor = 0,
        TextMarks? marks = null)
    {
        if (iconId == 0)
        {
            CenteredText(text, color, marks);
            return;
        }

        var lineH = ImGui.GetTextLineHeight();
        var iconH = MathF.Round(lineH * Math.Clamp(C.IconScale, 0.4f, 1.5f));
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var textWidth = ImGui.CalcTextSize(text).X;
        var total = iconH + spacing + textWidth;
        var offset = AlignOffset(ImGui.GetContentRegionAvail().X, total);
        if (offset > 0) ImGui.SetCursorPosX(MathF.Round(ImGui.GetCursorPosX() + offset));

        // Center the icon against the text, then restore the baseline.
        var baseY = ImGui.GetCursorPosY();
        ImGui.SetCursorPosY(MathF.Round(baseY + (lineH - iconH) * 0.5f));
        var iconTopLeft = ImGui.GetCursorScreenPos();
        Icons.Draw(iconId, new Vector2(iconH, iconH));
        if (ringFrac >= 0f) DrawRing(iconTopLeft, iconH, ringFrac, ringColor);
        ImGui.SameLine(0, spacing);
        ImGui.SetCursorPosY(baseY);
        CallText(text, color, marks);
    }

    // Horizontal offset for the configured alignment.
    private float AlignOffset(float avail, float contentWidth) => C.OverlayTextAlign switch
    {
        0 => 0f,
        2 => MathF.Max(0f, avail - contentWidth),
        _ => MathF.Max(0f, (avail - contentWidth) * 0.5f),
    };

    private void CenteredText(string text, uint color, TextMarks? marks = null)
    {
        var textWidth = ImGui.CalcTextSize(text).X;
        var offset = AlignOffset(ImGui.GetContentRegionAvail().X, textWidth);
        if (offset > 0) ImGui.SetCursorPosX(MathF.Round(ImGui.GetCursorPosX() + offset));
        CallText(text, color, marks);
    }

    private void SavePositionIfDragged()
    {
        if (EffectiveLocked) return;
        // Only capture a real drag, or a stray hold saves drift.
        if (!ImGui.IsMouseDragging(ImGuiMouseButton.Left) || !ImGui.IsWindowFocused()) return;
        var viewport = ImGui.GetMainViewport();
        var current = ImGui.GetWindowPos();
        var center = new Vector2(current.X + ImGui.GetWindowWidth() * 0.5f, current.Y);
        var frac = (center - viewport.WorkPos) / viewport.WorkSize;
        if ((frac - C.OverlayPosition).LengthSquared() > 0.0000001f)
        {
            C.OverlayPosition = frac;
            C.SaveSettings();
        }
    }
}
