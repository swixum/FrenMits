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

    // The line being counted down, and the run it belongs to.
    private readonly List<MitLine> _activeLines = new();
    private int _lastGen = -1;

    // Per-frame scratch for this job's lines and the call group.
    private readonly List<MitLine> _lines = new();
    private readonly List<MitLine> _group = new();

    // Stable, so calls tied on the clock keep their baked order.
    private static void StableSortByCueTime(List<MitLine> lines)
    {
        for (var i = 1; i < lines.Count; i++)
        {
            var l = lines[i];
            var j = i - 1;
            while (j >= 0 && lines[j].CueTime > l.CueTime) { lines[j + 1] = lines[j]; j--; }
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

    // How far before its cue time a call first appears.
    private float LeadFor(MitLine line)
    {
        if (line.LeadOverride > 0f) return line.LeadOverride;
        if (line.OffsetManual || line.OffsetSeconds <= 1f) return C.WarningSeconds;
        // Cached and loop-free of LINQ, since this runs per line.
        var dur = MinDuration(Cooldowns.PlanMitsCached(line.Action));
        return MathF.Min(C.CooldownLeadSeconds, dur * 0.5f);
    }

    // Shortest buff among a call's mits, 15s when unknown.
    private static float MinDuration(IReadOnlyList<Cooldowns.PlanMit> mits)
    {
        if (mits.Count == 0) return 15f;
        var dur = float.MaxValue;
        for (var i = 0; i < mits.Count; i++)
            dur = MathF.Min(dur, mits[i].Duration > 0f ? mits[i].Duration : 15f);
        return dur;
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
                    var w = BoardWidth(new[] { ("Reprisal", 1.4f), ("Feint", 3.2f) });
                    DrawBoardCall("Wave Cannon", "Reprisal", 1.4f, true, 0, C.WarningSeconds, Icons.ResolveFromText("Reprisal"), "", w);
                    ImGui.Dummy(new Vector2(1f, 4f));
                    DrawBoardCall("Wave Cannon", "Feint", 3.2f, true, 0, C.WarningSeconds, Icons.ResolveFromText("Feint"), "", w);
                }
            else if (C.OverlayStyle == 2)
            {
                var d = IconClockDiameter();
                DrawIconClock(Icons.ResolveFromText("Reprisal"), "Reprisal", 1.4f, true, C.WarningSeconds, 0, d);
                ImGui.SameLine(0, 10f);
                DrawIconClock(Icons.ResolveFromText("Feint"), "Feint", 3.2f, true, C.WarningSeconds, 0, d);
            }
            else
                DrawCurrent("Reprisal / Feint", "Reprisal", 1.4f, true, 0, C.WarningSeconds,
                    Icons.ResolveFromText("Reprisal"));
            return;
        }

        var fight = _plugin.ActiveFight();
        if (fight == null) return;

        var job = _plugin.ActiveJobAbbreviation();
        var elapsed = _plugin.CueClockFor(fight); // call schedule, not sheet position

        // A reused buffer, since the overlay redraws continuously.
        var lines = _lines;
        lines.Clear();
        foreach (var l in fight.OrderedLines)
            if (l.Enabled && l.AppliesTo(job)) lines.Add(l);

        // Reset the held call on a new run, so nothing carries over.
        if (_plugin.Timer.Generation != _lastGen) { _lastGen = _plugin.Timer.Generation; _activeLines.Clear(); }

        // The calls we count to: the soonest, plus anything tied.
        var bestRemaining = float.MaxValue;
        foreach (var line in lines)
        {
            var remaining = line.CueTime - elapsed;
            var lead = LeadFor(line);
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
                var rem = l.CueTime - elapsed;
                if (rem >= 0f && rem <= LeadFor(l) && rem <= bestRemaining + tieWindow) group.Add(l);
            }
            StableSortByCueTime(group);
            // Keep a just-passed call up for its hold, stacked with the next.
            var heldCount = 0;
            foreach (var l in _activeLines)
            {
                var rem = l.CueTime - elapsed;
                if (rem <= 0f && rem >= -C.HoldSeconds && !group.Contains(l))
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
                var rem = l.CueTime - elapsed;
                if (rem <= 0f && rem >= -C.HoldSeconds) group.Add(l);
            }
            StableSortByCueTime(group);
            if (group.Count == 0) _activeLines.Clear();
        }

        if (C.OverlayStyle == 1)
        {
            using (PushFont(C.OverlayFontSizePx))
            {
                var width = BoardWidth(group.Select(l =>
                    (Icons.DisplayAction(l.ActionFor(job), job), l.CueTime - elapsed)));
                for (var i = 0; i < group.Count; i++)
                {
                    if (i > 0) ImGui.Dummy(new Vector2(1f, 4f));
                    var call = group[i];
                    var remaining = call.CueTime - elapsed;
                    var lead = LeadFor(call);
                    var icon = C.ShowAbilityIcon ? Icons.For(call, job) : 0u;
                    var action = Icons.DisplayAction(call.ActionFor(job), job);
                    DrawBoardCall(call.Mechanic, action, MathF.Max(0f, remaining), remaining > 0f,
                        call.Color, lead, icon, PrepText(call), width);
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
                var remaining = call.CueTime - elapsed;
                var lead = LeadFor(call);
                var action = Icons.DisplayAction(call.ActionFor(job), job);
                DrawIconClock(Icons.For(call, job), action, MathF.Max(0f, remaining), remaining > 0f,
                    lead, call.Color, d);
            }
            return;
        }

        for (var i = 0; i < group.Count; i++)
        {
            if (i > 0) ImGui.Spacing();
            var call = group[i];
            var remaining = call.CueTime - elapsed;
            var lead = LeadFor(call);
            var icon = C.ShowAbilityIcon ? Icons.For(call, job) : 0u;
            var action = Icons.DisplayAction(call.ActionFor(job), job);
            DrawCurrent(call.Mechanic, action, MathF.Max(0f, remaining), remaining > 0f, call.Color, lead, icon, PrepText(call));
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

    // A uniform bar width, so stacked bars line up.
    private float BoardWidth(IEnumerable<(string Action, float Remaining)> calls)
    {
        var lineH = ImGui.GetTextLineHeight();
        var iconSlot = C.ShowAbilityIcon ? MathF.Round(lineH * Math.Clamp(C.IconScale, 0.4f, 1.5f)) + 8f : 0f;
        var content = 0f;
        foreach (var (action, rem) in calls)
        {
            var time = rem > 0f ? $"{MathF.Ceiling(rem):0}s" : "NOW";
            content = MathF.Max(content, ImGui.CalcTextSize(action).X + ImGui.CalcTextSize(time).X);
        }
        return MathF.Max(170f, 14f + iconSlot + content + 22f + 10f);
    }

    private void DrawBoardCall(string mechanic, string action, float remaining, bool imminent,
        uint colorOverride, float lead, uint iconId, string prep, float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        var barH = MathF.Round(lineH + 12f);
        const float round = 6f;
        var p0 = ImGui.GetCursorScreenPos();
        var p1 = p0 + new Vector2(width, barH);

        var isPrep = prep.Length > 0 && imminent;
        var typeColor = C.ColorByMitType ? MitTypes.Color(MitTypes.Classify(action, mechanic), C) : 0u;
        var baseCol = colorOverride != 0 ? colorOverride
            : isPrep ? PrepCol
            : typeColor != 0 ? typeColor
            : BoardAccent;
        // At go-time the whole bar goes green, like the board.
        var barCol = imminent ? baseCol : BoardNow;

        // Panel.
        var back = ((uint)(Math.Clamp(C.UpcomingBoardBgOpacity, 0f, 1f) * 255f) << 24) | BoardPanelRgb;
        dl.AddRectFilled(p0, p1, back, round);

        // Draining countdown fill (full at the lead, empty at the call).
        if (imminent && lead > 0.01f)
        {
            var frac = Math.Clamp(remaining / lead, 0f, 1f);
            var edgeX = p0.X + width * frac;
            var rgb = barCol & 0x00FFFFFF;
            var corners = frac >= 0.999f ? ImDrawFlags.RoundCornersAll : ImDrawFlags.RoundCornersLeft;
            dl.AddRectFilled(p0, new Vector2(edgeX, p1.Y), rgb | 0x66000000, round, corners);
            if (frac > 0.02f && frac < 0.985f)
                dl.AddRectFilled(new Vector2(edgeX - 1.5f, p0.Y + 1f),
                    new Vector2(edgeX + 0.5f, p1.Y - 1f), rgb | 0xF0000000);
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
        if (isPrep)
            using (PushFont(C.OverlayFontSizePx * 0.5f))
                SubText(prep, PrepCol, subX);
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
        float lead, uint colorOverride, float diam)
    {
        var dl = ImGui.GetWindowDrawList();
        var p0 = ImGui.GetCursorScreenPos();
        var p1 = p0 + new Vector2(diam, diam);
        var center = p0 + new Vector2(diam * 0.5f, diam * 0.5f);
        var accent = colorOverride != 0 ? colorOverride : BoardAccent;
        var rounding = diam * 0.14f;

        // The icon itself (or a themed disc when it can't be resolved).
        if (iconId != 0)
        {
            ImGui.SetCursorScreenPos(p0);
            Icons.Draw(iconId, new Vector2(diam, diam));
        }
        else
        {
            dl.AddRectFilled(p0, p1, (accent & 0x00FFFFFF) | 0xB4000000, rounding);
        }

        // Cooldown sweep: a dark wedge growing clockwise.
        if (imminent && lead > 0.01f)
        {
            var frac = Math.Clamp(remaining / lead, 0f, 1f);
            var covered = 1f - frac;
            if (covered > 0.001f)
            {
                var start = -MathF.PI * 0.5f;
                dl.PathLineTo(center);
                dl.PathArcTo(center, diam * 0.72f, start, start + covered * MathF.PI * 2f, 96);
                dl.PathFillConvex(0xC0000000);
            }
        }

        // Border, green + pulsing at go time.
        var ring = imminent ? accent : BoardNow;
        if (!imminent && C.PulseWhenImminent) ring = Pulse(ring);
        dl.AddRect(p0, p1, (ring & 0x00FFFFFF) | 0xE0000000, rounding, ImDrawFlags.None, 2.5f);

        // Centered countdown, outlined so it reads over busy icon art.
        var num = !imminent ? "" : remaining < 3f ? $"{remaining:0.0}" : $"{MathF.Ceiling(remaining):0}";
        if (num.Length > 0)
            using (PushFont(MathF.Round(diam * 0.42f)))
            {
                var np = center - ImGui.CalcTextSize(num) * 0.5f;
                for (var oy = -1; oy <= 1; oy++)
                    for (var ox = -1; ox <= 1; ox++)
                        if (ox != 0 || oy != 0)
                            dl.AddText(np + new Vector2(ox * 1.6f, oy * 1.6f), 0xE6000000, num);
                dl.AddText(np, 0xFFFFFFFF, num);
            }

        ImGui.SetCursorScreenPos(p0);
        ImGui.Dummy(new Vector2(diam, diam));
    }

    // Green prep accent (matches the board's "now" green).
    private const uint PrepCol = 0xFF64DC64;

    // The press window for a call pulled early, else "".
    private string PrepText(MitLine call)
    {
        if (!C.PrepAlerts) return "";
        // A prep window only exists when the press covers a later hit.
        var windowEnd = call.Time - call.OffsetSeconds; // latest press: the front hit
        if (call.CoverUntil <= windowEnd + 2f) return ""; // covers only itself: nothing to prep
        var dur = MinDuration(Cooldowns.PlanMitsCached(call.Action));
        var windowStart = MathF.Max(0f, call.CoverUntil - dur); // earliest press still reaching the last hit
        return windowStart >= windowEnd - 0.5f
            ? $"(use at {AbsTime(windowEnd)})"
            : $"(use between {AbsTime(windowStart)} and {AbsTime(windowEnd)})";
    }

    private static string AbsTime(float t) => Fmt.MmssRound(t);

    private void DrawCurrent(string mechanic, string action, float remaining, bool imminent,
        uint colorOverride, float lead, uint iconId = 0, string prep = "")
    {
        // Color priority: override, prep, mit type, then default.
        var isPrep = prep.Length > 0 && imminent;
        var typeColor = C.ColorByMitType ? MitTypes.Color(MitTypes.Classify(action, mechanic), C) : 0u;
        var baseColor = colorOverride != 0 ? colorOverride
            : isPrep ? PrepCol
            : typeColor != 0 ? typeColor
            : (imminent ? C.OverlayColorImminent : C.OverlayColorActive);
        var color = imminent && C.PulseWhenImminent && remaining < 1.5f ? Pulse(baseColor) : baseColor;
        var headline = FormatHeadline(mechanic, action, remaining, imminent);

        // Flag a call whose mit won't be off recast in time.
        if (C.CooldownAwareCalls && imminent && Cooldowns.Remaining(action) is { } cd && cd > remaining + 0.5f)
        {
            headline += $"  [CD {MathF.Ceiling(cd):0}s]";
            color = 0xFF3C3CF0; // red-ish warning
        }

        // A depleting ring while counting down; -1 means none.
        var ringFrac = C.ShowRadialRing && imminent && lead > 0.01f
            ? Math.Clamp(remaining / lead, 0f, 1f) : -1f;

        using (PushFont(C.OverlayFontSizePx))
            CenteredIconText(iconId, headline, color, ringFrac, baseColor);

        if (C.ShowMechanicLine
            && !string.IsNullOrWhiteSpace(mechanic)
            && !string.Equals(mechanic, action, StringComparison.OrdinalIgnoreCase))
        {
            // Its own countdown, so the mechanic line ticks down too.
            var mechText = imminent
                ? $"{mechanic}   {MathF.Ceiling(remaining):0}"
                : mechanic;
            using (PushFont(C.OverlayFontSizePx * 0.55f))
                CenteredText(mechText, C.OverlayColorMechanic);
        }

        // Prep line: press now, it holds for the mechanic it covers.
        if (isPrep)
            using (PushFont(C.OverlayFontSizePx * 0.5f))
                CenteredText(prep, PrepCol);

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

        // Center the icon against the text, then restore the baseline.
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

    // Horizontal offset for the configured alignment.
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
