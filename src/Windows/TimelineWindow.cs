using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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

    private bool _dragging;

    // Locked for real if you ticked the lock OR you're in a live pull (but not
    // while previewing) — combat always pins it so it can't be grabbed mid-fight.
    private bool EffectiveLocked => C.TimelineLocked || (Plugin.InCombat && !C.TestMode);

    // The window now always follows C.TimelinePosition, so a reset (or a slider
    // change) just takes effect next frame; nothing to schedule.
    public void RequestReposition() { }

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

        // Movement is handled manually (HandleManualDrag). ImGui only moves a
        // window from its title bar in this build, and NoTitleBar stays on so
        // locking never shifts the content - so ImGui would never move this
        // window. Instead we always pin it to the saved position and let a drag
        // edit that saved value. When locked, it's click-through.
        Flags |= ImGuiWindowFlags.NoMove;
        if (EffectiveLocked)
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMouseInputs;

        if (C.ShowBackground)
            ImGui.PushStyleColor(ImGuiCol.WindowBg, C.BackgroundColor);

        var viewport = ImGui.GetMainViewport();
        var pos = viewport.WorkPos + C.TimelinePosition * viewport.WorkSize;
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.0f));
    }

    public override void PostDraw()
    {
        if (C.ShowBackground)
            ImGui.PopStyleColor();
    }

    public override bool DrawConditions()
    {
        // The settings page's on-screen preview: while the Next Mits page is
        // open, the REAL window shows and plays a sample at its actual spot.
        if (ScreenPreviewing) return true;
        if (!C.ShowUpcoming) return false;
        if (C.TestMode) return true;
        // Stays up through cutscenes and the post-cutscene resync now, so a
        // downtime (with its countdown) reads on the board instead of vanishing.
        if (_plugin.ActiveFight() is not { } fight) return false;
        if (C.OnlyInTargetTerritory && fight.TerritoryId != Service.ClientState.TerritoryType) return false;
        return _plugin.Timer.Running;
    }

    public override void Draw()
    {
        HandleManualDrag();

        // Both preview paths (the header's Live preview and the Next Mits
        // settings page) play the same real sample in the real window.
        if ((C.TestMode || ScreenPreviewing) && !_plugin.Timer.Running)
        {
            DrawDmuSample();
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
        // Universal timelines have NO main call (the overlay is gated off), so
        // their lines stay listed through the lead instead of vanishing early.
        var upcoming = fight.OrderedLines
            .Where(l => l.Enabled && l.AppliesTo(job)
                        && l.CueTime - elapsed > (fight.TimelineOnly
                            ? -C.HoldSeconds
                            : l.LeadOverride > 0f ? l.LeadOverride : C.WarningSeconds)
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

    // Board palette (ABGR) - FrenMits' own look: the near-black panels and blue
    // accent from the config window's theme, at over-game friendly alphas.
    private const uint BoardBarBorder = 0x66594A3F; // soft slate border
    private const uint BoardBright = 0xFFECE8E6;    // Theme text
    private const uint BoardRaidCol = 0xFFE0C860;   // raidwide: cool cyan-blue
    private const uint BoardBusterCol = 0xFF4090F0; // tank buster: warm orange
    private const uint BoardMuted = 0xFFA89A90;     // muted gray
    private const uint BoardPanelRgb = 0x0014110E;  // Theme.PanelBg, opacity applied on top

    // The customizable colors, guarded so a zeroed config value falls back to
    // the FrenMits defaults instead of erasing a state.
    private uint AccentCol => C.UpcomingBoardAccentColor != 0 ? C.UpcomingBoardAccentColor : 0xFFF6823B;
    private uint NextCol => C.UpcomingBoardNextColor != 0 ? C.UpcomingBoardNextColor : 0xFF28BEFF;
    private uint NowCol => C.UpcomingBoardNowColor != 0 ? C.UpcomingBoardNowColor : 0xFF64DC64;
    private float BoardRound => Math.Clamp(C.UpcomingBoardRounding, 0f, 12f);

    // The fight's full mechanic list is derived from every column of its sheet,
    // so it's cached: rebuilt when the fight or pull changes, and refreshed out
    // of combat so sheet edits show up while you're arranging things.
    private List<SheetTimeline.MechRow> _board = new();
    private string _boardFightId = "";
    private int _boardGen = -1;
    private int _boardStamp = -1;
    private DateTime _boardBuiltAt = DateTime.MinValue;

    private List<SheetTimeline.MechRow> BoardRows(FightProfile fight)
    {
        // Cheap change fingerprint so plan edits show up even mid-combat
        // (Auto-plan / a line added from Sheet View during a pull). Counts
        // alone miss equal-count edits (retime, rename, delete-one-add-one),
        // so fold times and actions in - including the OTHER columns' stashes,
        // which feed the board's rows too.
        var stamp = fight.Lines.Count * 31 + fight.CustomRows.Count;
        unchecked
        {
            foreach (var l in fight.Lines)
                stamp = stamp * 31 + (int)(l.Time * 8f) + l.Action.Length;
            foreach (var r in fight.CustomRows)
                stamp = stamp * 31 + (int)(r.Time * 8f) + r.Hurt;
            foreach (var kv in fight.SavedSlots)
            {
                stamp = stamp * 17 + kv.Value.Count;
                foreach (var l in kv.Value) stamp = stamp * 31 + (int)(l.Time * 8f);
            }
        }
        var stale = _boardFightId != fight.Id
                    || _boardGen != _plugin.Timer.Generation
                    || _boardStamp != stamp
                    || (!Plugin.InCombat && (DateTime.Now - _boardBuiltAt).TotalSeconds > 4);
        if (stale)
        {
            _board = SheetTimeline.Build(fight);
            _boardFightId = fight.Id;
            _boardGen = _plugin.Timer.Generation;
            _boardStamp = stamp;
            _boardBuiltAt = DateTime.Now;
        }
        return _board;
    }

    private static readonly List<MitLine> NoLines = new();
    private static readonly List<SheetTimeline.MechRow> NoRows = new();

    // Credit: the idea of surfacing boss untargetable/targetable windows on a fight
    // timeline, and the timing data these windows are built from, come from cactbot
    // (github.com/OverlayPlugin/cactbot, Apache License 2.0, Copyright the cactbot
    // authors). FrenMits adapts their published timeline files - the untargetable/
    // targetable marker times are anchored to each fight's mechanics and converted
    // onto FrenMits' own clock (see Data/Downtimes.cs) - and renders them its own
    // way. Thanks to the cactbot authors.
    //
    // Learned downtimes as inline board rows: an Untargetable entry when the boss
    // goes away and a Targetable one when it returns, each counting down.
    private List<SheetTimeline.MechRow> DowntimeRows(FightProfile fight)
    {
        var list = Downtimes.Effective(fight.TerritoryId, C.LearnedDowntimes);
        if (list.Count == 0) return NoRows;
        var rows = new List<SheetTimeline.MechRow>(list.Count * 2);
        foreach (var w in list)
        {
            rows.Add(new SheetTimeline.MechRow { Time = w.Start, Mechanic = "Untargetable" });
            rows.Add(new SheetTimeline.MechRow { Time = w.Start + w.Duration, Mechanic = "Targetable" });
        }
        return rows;
    }

    // The hardcoded gate HP for the Untargetable at rowStart (-1 if that lull has
    // no DPS check): the boss HP fraction you must push it below before it goes away.
    private float DowntimeTargetHp(FightProfile fight, float rowStart)
    {
        foreach (var w in Downtimes.Effective(fight.TerritoryId, C.LearnedDowntimes))
            if (MathF.Abs(w.Start - rowStart) < 2f) return w.TargetHp;
        return -1f;
    }

    // How long before the boss becomes targetable the green "Targetable" heads-up
    // replaces the neutral downtime row. Before this, the lull reads as the cutscene
    // or untargetable stretch you're sitting through; inside it you get the
    // get-ready-to-resume cue.
    private const float TargetableHeadsup = 10f;

    // Is the lull ending at targetableTime an actual cutscene (vs a plain
    // untargetable transition)? Drives the "Cutscene" vs "Untargetable" label.
    private bool DowntimeIsCutscene(FightProfile fight, float targetableTime)
    {
        foreach (var w in Downtimes.Effective(fight.TerritoryId, C.LearnedDowntimes))
            if (MathF.Abs(w.Start + w.Duration - targetableTime) < 2f) return w.Cutscene;
        return false;
    }

    private void DrawBoard(FightProfile fight, string? job, float elapsed,
        List<SheetTimeline.MechRow>? rowsOverride = null, float? widthOverride = null)
    {
        var look = MathF.Max(10f, C.UpcomingBoardLookaheadSeconds);
        var width = widthOverride ?? MathF.Max(180f, C.UpcomingBoardWidth);
        // A just-hit row lingers 2s at "now" so it doesn't vanish mid-press.
        var windowRows = (rowsOverride ?? BoardRows(fight)).Concat(DowntimeRows(fight))
            .Where(r => r.Time - elapsed >= -2f && r.Time - elapsed <= look)
            .OrderBy(r => r.Time)
            .ToList();

        if (HeaderVisible) DrawBoardHeader(fight, elapsed, width);

        // Learned downtimes ride inline as their own rows (Untargetable / Targetable).
        // The banner is only the fallback while we're still LEARNING a lull the first
        // time (no row exists yet).
        if (_plugin.DowntimeActive && _plugin.DowntimeRemaining < 0f) DrawDowntimeBanner(width);

        // Attach each of your presses to its single NEAREST row, so a mechanic
        // repeating a few seconds apart can't show one press under both bars.
        // The 2.5s window still catches job extras riding ~1s off their row.
        // Universal (timeline-only) duties have no presses at all: every bar
        // stays neutral instead of the whole fight lighting up gold.
        var mineByRow = new Dictionary<SheetTimeline.MechRow, List<MitLine>>();
        foreach (var l in fight.TimelineOnly ? Enumerable.Empty<MitLine>() : fight.OrderedLines)
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

        // Green matches the main call EXACTLY: a press is "now" when it enters
        // its warning window on the cue clock, so per-line offsets (a call set
        // to fire early or late) keep the board and the big call in lockstep.
        bool InWindow(MitLine l)
            => l.CueTime - elapsed <= (l.LeadOverride > 0f ? l.LeadOverride : C.WarningSeconds);

        // Gold marks your next press that isn't already green: while a call is
        // in (or just past) its window, the one after it keeps its own marker.
        var nextIdx = -1;
        for (var i = 0; i < visible.Count && nextIdx < 0; i++)
            if (mine[i].Count > 0 && !mine[i].Any(InWindow))
                nextIdx = i;

        // Negative spacing pulls bars into each other (overlap look).
        var rowGap = Math.Clamp(C.UpcomingBoardRowGap, -8f, 16f);
        for (var i = 0; i < visible.Count; i++)
        {
            if (i > 0 && rowGap > 0f) ImGui.Dummy(new Vector2(1f, rowGap));
            else if (i > 0 && rowGap < 0f) ImGui.SetCursorPosY(ImGui.GetCursorPosY() + rowGap);
            var r = visible[i];
            var rem = r.Time - elapsed;
            var useNow = mine[i].Count > 0 && mine[i].Any(InWindow);
            var isNext = i == nextIdx;
            var accent = useNow ? NowCol : isNext ? NextCol : 0u;
            var pulse = useNow && C.PulseWhenImminent && rem < 1.5f;

            // A row with no mechanic label (a bare user timer) is named by the
            // press itself, so its action doesn't repeat underneath. Someone
            // else's bare timer falls back to the action stored on the row.
            var name = r.Mechanic;
            var bareTimer = string.IsNullOrWhiteSpace(name);
            if (bareTimer)
                name = mine[i].Count > 0
                    ? Icons.DisplayAction(mine[i][0].ActionFor(job), job)
                    : r.Fallback;

            // Row kind: lull markers (untargetable/targetable) or the mechanic's own
            // hit type. An upcoming Untargetable turns into a "push it or fail"
            // skull once the boss is within ~10% above that gate's hardcoded target
            // HP (the phase's DPS check): its countdown is the time-to-kill, and the
            // label carries the % you must be under by then.
            var gate = false;
            var gateTgt = -1f;
            if (r.Mechanic == "Untargetable")
            {
                gateTgt = DowntimeTargetHp(fight, r.Time);
                // Only real DPS checks (the boss got pushed low, <=40%), not brief
                // mid-phase untargetable moments at high HP.
                gate = _plugin.BossHpFraction > 0f && gateTgt is >= 0f and <= 0.40f
                    && _plugin.BossHpFraction <= gateTgt + 0.10f;
            }
            var kind = r.Mechanic == "Untargetable" ? (gate ? 3 : 4)
                : r.Mechanic == "Targetable" ? 5
                : RowKind(r, bareTimer);
            if (kind == 3) name = $"DPS check ({gateTgt * 100f:0}%)";
            // A targetable still more than the heads-up window away reads as the
            // lull you're sitting through, not a green "you can hit it" tick: a real
            // cutscene shows "Cutscene", a plain transition stays "Untargetable".
            // Only its last few seconds - boss about to return, get ready to resume -
            // flip to the green Targetable cue.
            if (kind == 5 && rem > TargetableHeadsup)
            {
                if (DowntimeIsCutscene(fight, r.Time)) { kind = 6; name = "Cutscene"; }
                else { kind = 4; name = "Untargetable"; }
            }
            BoardBar(name, rem, look, width, accent, r.Hurt, pulse, kind);

            if (C.UpcomingBoardShowActions && !bareTimer && mine[i].Count > 0)
                BoardActions(mine[i], job, elapsed, width, accent);
            // No under-bar text: prep/coverage cues live in the main call's alert
            // (as a "use between" press window), not as notes on the board.
        }
    }

    // True when the header has anything left to draw.
    private bool HeaderVisible => C.UpcomingShowHeader
        && (C.UpcomingHeaderTitle || C.UpcomingHeaderClock || C.UpcomingHeaderRule);

    private void DrawBoardHeader(FightProfile fight, float elapsed, float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var lineH = ImGui.GetTextLineHeight();
        var accent = AccentCol;
        var textH = C.UpcomingHeaderTitle || C.UpcomingHeaderClock ? lineH : 0f;

        var clockW = 0f;
        if (C.UpcomingHeaderClock)
        {
            var clock = TimeText(MathF.Max(0f, elapsed));
            clockW = ImGui.CalcTextSize(clock).X;
            BoardText(dl, new Vector2(pos.X + width - clockW, pos.Y), accent, clock);
        }

        // Your seat, as a little badge left of the clock ("T1 · WAR").
        var badgeW = 0f;
        if (C.UpcomingHeaderSlot && !fight.TimelineOnly && !string.IsNullOrEmpty(fight.Slot))
        {
            var job = _plugin.ActiveJobAbbreviation();
            var btext = job != null && !string.Equals(job, fight.Slot, StringComparison.OrdinalIgnoreCase)
                ? $"{fight.Slot} · {job}" : fight.Slot;
            var ts = ImGui.CalcTextSize(btext);
            var padX = 6f;
            var bx = pos.X + width - clockW - (C.UpcomingHeaderClock ? 10f : 0f) - ts.X - padX * 2f;
            if (bx > pos.X + 60f) // only when the header has room for it
            {
                var r0 = new Vector2(bx, pos.Y - 1f);
                var r1 = new Vector2(bx + ts.X + padX * 2f, pos.Y + lineH + 1f);
                dl.AddRectFilled(r0, r1, 0xB0000000 | BoardPanelRgb, 4f);
                dl.AddRect(r0, r1, (accent & 0x00FFFFFF) | 0x66000000, 4f);
                BoardText(dl, new Vector2(bx + padX, pos.Y), accent, btext);
                badgeW = ts.X + padX * 2f + 8f;
            }
        }

        if (C.UpcomingHeaderTitle)
        {
            // The little FrenMits tick: an accent diamond in front of the name.
            // Sized and spaced off the font so big overlay fonts don't collide.
            var d = MathF.Max(3.5f, lineH * 0.18f);
            var c = new Vector2(pos.X + d + 1f, MathF.Round(pos.Y + lineH * 0.5f));
            dl.AddQuadFilled(c + new Vector2(0f, -d), c + new Vector2(d, 0f),
                c + new Vector2(0f, d), c + new Vector2(-d, 0f), accent);

            var nameX = 2f * d + 8f;
            var clipW = C.UpcomingHeaderClock || badgeW > 0f
                ? MathF.Max(40f, width - clockW - badgeW - 10f) : width;
            dl.PushClipRect(pos, pos + new Vector2(clipW, lineH + 2f), true);
            BoardText(dl, pos + new Vector2(nameX, 0f), BoardBright, fight.Name);
            dl.PopClipRect();
        }

        var h = textH;
        if (C.UpcomingHeaderRule)
        {
            // A thin accent rule under the header, fading out to the right.
            var y = pos.Y + textH + (textH > 0f ? 3f : 0f);
            dl.AddRectFilledMultiColor(new Vector2(pos.X, y), new Vector2(pos.X + width, y + 2f),
                accent, accent & 0x00FFFFFF, accent & 0x00FFFFFF, accent);
            h += (textH > 0f ? 3f : 0f) + 2f;
        }

        // Trust line: what the clock last locked onto, for a few seconds after
        // each resync snap, fading out as it goes.
        if (C.UpcomingHeaderSync && _plugin.Sync.LastSyncNice.Length > 0)
        {
            var age = (float)(DateTime.UtcNow - _plugin.Sync.LastSyncAt).TotalSeconds;
            if (age >= 0f && age < 8f && _plugin.Timer.Running)
            {
                var alpha = (byte)(age < 6f ? 0xA8 : (int)(0xA8 * (8f - age) / 2f));
                var col = ((uint)alpha << 24) | (BoardMuted & 0x00FFFFFF);
                dl.PushClipRect(new Vector2(pos.X, pos.Y + h + 2f), new Vector2(pos.X + width, pos.Y + h + 2f + lineH), true);
                BoardText(dl, new Vector2(pos.X, pos.Y + h + 2f), col, "synced · " + _plugin.Sync.LastSyncNice);
                dl.PopClipRect();
                h += lineH + 2f;
            }
        }
        ImGui.Dummy(new Vector2(width, h + 4f));
    }

    // A neutral banner shown while the boss is untargetable (phase transition,
    // cutscene, jumped away): a lull, with a running timer of how long it's lasted.
    private void DrawDowntimeBanner(float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        var barH = MathF.Round(lineH + Math.Clamp(C.UpcomingBoardBarPad, 2f, 24f));
        var round = BoardRound;
        var p0 = ImGui.GetCursorScreenPos();
        var p1 = p0 + new Vector2(width, barH);

        var back = ((uint)(Math.Clamp(C.UpcomingBoardBgOpacity, 0f, 1f) * 255f) << 24) | BoardPanelRgb;
        dl.AddRectFilled(p0, p1, back, round);
        dl.AddRectFilled(p0, new Vector2(p0.X + 3f, p1.Y), 0xFFB0A594u, round, ImDrawFlags.RoundCornersLeft);
        dl.AddRect(p0, p1, BoardBarBorder, round);

        var cy = p0.Y + (barH - lineH) * 0.5f;
        // Once we've learned this lull's length, count DOWN to targetable; the
        // first time we just measure it (elapsed) while we learn it.
        var remain = _plugin.DowntimeRemaining;
        var known = remain >= 0f;
        BoardText(dl, new Vector2(p0.X + 10f, cy), BoardMuted, known ? "Downtime" : "Downtime (not targetable)");
        if (C.UpcomingBoardTimeText)
        {
            var t = known ? $"targetable in {remain:0.0}s" : $"{_plugin.DowntimeElapsed:0.0}s";
            var tw = ImGui.CalcTextSize(t).X;
            BoardText(dl, new Vector2(p1.X - tw - 8f, cy), known ? BoardBright : BoardMuted, t);
        }
        ImGui.Dummy(new Vector2(width, barH));
        ImGui.Dummy(new Vector2(1f, 4f));
    }

    private void BoardBar(string name, float rem, float look, float width, uint accent, int hurt, bool pulse = false, int kind = 0)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        var barH = MathF.Round(lineH + Math.Clamp(C.UpcomingBoardBarPad, 2f, 24f));
        var round = BoardRound;
        var p0 = ImGui.GetCursorScreenPos();
        var p1 = p0 + new Vector2(width, barH);

        var back = ((uint)(Math.Clamp(C.UpcomingBoardBgOpacity, 0f, 1f) * 255f) << 24) | BoardPanelRgb;
        dl.AddRectFilled(p0, p1, back, round);
        // Kind wash: fill the whole bar with a soft color keyed to what the row
        // IS, so its nature reads before you parse the text - red for a DPS check
        // to push, green for targetable (get ready), grey for a plain untargetable
        // lull, purple for a cutscene. Ordinary mit rows (kind 0-2) get none.
        var wash = kind switch
        {
            3 => 0xFF4646FFu, // DPS check: red
            4 => 0xFF9AA0A8u, // untargetable: grey
            5 => 0xFF7BD88Bu, // targetable: green
            6 => 0xFFB48C96u, // cutscene: purple
            _ => 0u,
        };
        if (wash != 0)
            dl.AddRectFilled(p0, p1, (wash & 0x00FFFFFFu) | 0x40000000u, round);
        // The fill tracks the countdown. Draining (default): full at the
        // look-ahead edge, empty at the hit. Filling: the opposite, growing
        // toward full as the hit lands - some folks read urgency that way.
        var frac = Math.Clamp(rem / look, 0f, 1f);
        if (!C.UpcomingBoardDrain) frac = 1f - frac;
        if (frac > 0.004f) // countdown fill on every row, lull markers included
        {
            // Lull/gate rows drain in their own wash color; ordinary rows use the
            // press accent (or the board accent when no press owns the row).
            var baseCol = (wash != 0 ? wash : accent == 0 ? AccentCol : accent) & 0x00FFFFFF;
            var edgeX = p0.X + width * frac;
            // Brighter than before (was ~30% alpha, hard to read over the game):
            // a solid base plus a gradient that peaks at the moving edge, so the
            // sweep pops without washing out the text on top.
            var corners = frac >= 0.999f ? ImDrawFlags.RoundCornersAll : ImDrawFlags.RoundCornersLeft;
            dl.AddRectFilled(p0, new Vector2(edgeX, p1.Y), baseCol | 0x66000000, round, corners);
            dl.AddRectFilledMultiColor(p0, new Vector2(edgeX, p1.Y),
                baseCol | 0x14000000, baseCol | 0x7A000000, baseCol | 0x7A000000, baseCol | 0x14000000);
            // A crisp bright edge rides the boundary so the countdown is obvious
            // at a glance. Hidden right at the ends, where it would just double
            // the bar's own border.
            if (frac > 0.02f && frac < 0.985f)
                dl.AddRectFilled(new Vector2(edgeX - 1.5f, p0.Y + 1f),
                    new Vector2(edgeX + 0.5f, p1.Y - 1f), baseCol | 0xF0000000);
        }
        // The FrenMits signature: a slim stripe on the left edge - the accent
        // blue normally, gold/green when the row is yours, pulsing at go time.
        if (C.UpcomingBoardStripe)
        {
            var stripe = kind switch
            {
                3 => 0xFF4646FFu,   // at-risk: red
                4 => 0xFF9AA0A8u,   // untargetable: slate
                5 => 0xFF7BD88Bu,   // targetable: green
                6 => 0xFFB48C96u,   // cutscene / downtime: muted lavender
                _ => accent == 0 ? (AccentCol & 0x00FFFFFF) | 0xB3000000 : accent,
            };
            if (pulse) stripe = Pulse(stripe);
            dl.AddRectFilled(p0, new Vector2(p0.X + 3f, p1.Y), stripe, round, ImDrawFlags.RoundCornersLeft);
        }
        dl.AddRect(p0, p1, BoardBarBorder, round);

        // Every row's text is the SAME bright color for consistency; the wash,
        // stripe and icon carry each row's identity, not the text. (A press row
        // still tints its text gold/green so your own calls read at a glance.)
        var textCol = accent == 0 ? BoardBright : accent;
        var textY = p0.Y + (barH - lineH) * 0.5f;
        var isNow = rem < 0f;
        // Under 3s, count down with one decimal so the last moments read finely
        // (2.4s, 1.8s...); above that, whole seconds.
        var timeText = isNow ? "NOW" : rem < 3f ? $"{rem:0.0}s" : $"{MathF.Ceiling(rem):0}s";
        var timeW = C.UpcomingBoardTimeText ? ImGui.CalcTextSize(timeText).X : 0f;

        // Row icon, left of the name: the tank-buster shield (toggle), or an
        // always-on marker for an at-risk mit (skull), a lull start (untargetable)
        // or its end (targetable). Raidwide rows carry no icon.
        var nameX = p0.X + 10f;
        var showIcon = kind switch
        {
            2 => C.UpcomingBoardShowType,
            3 or 4 or 5 or 6 => true,
            _ => false,
        };
        if (showIcon)
        {
            var (glyph, iconCol) = kind switch
            {
                3 => (FontAwesomeIcon.Skull, 0xFF4646FFu),
                4 => (FontAwesomeIcon.Ban, 0xFF9AA0A8u),
                5 => (FontAwesomeIcon.Crosshairs, 0xFF7BD88Bu),
                6 => (FontAwesomeIcon.Film, 0xFFB48C96u),
                _ => (FontAwesomeIcon.Shield, BoardBusterCol),
            };
            var isz = lineH * 0.82f;
            BoardIcon(dl, new Vector2(nameX + isz * 0.5f, p0.Y + barH * 0.5f), isz, iconCol, glyph);
            nameX += isz + 6f;
        }

        // Clip the name so a long mechanic can't run under the countdown
        // (or off the bar, when the countdown text is hidden).
        dl.PushClipRect(p0, new Vector2(p1.X - (timeW > 0f ? timeW + 14f : 8f), p1.Y), true);
        BoardText(dl, new Vector2(nameX, textY), textCol, name);
        // Severity marks from a graded custom sheet: ! light, !! hurts, !!! deadly.
        if (C.UpcomingBoardShowSeverity && hurt > 0)
        {
            var markCol = hurt >= 3 ? 0xFF4646FFu : hurt == 2 ? 0xFF008CFFu : 0xFF00D7FFu;
            BoardText(dl, new Vector2(nameX + ImGui.CalcTextSize(name).X + 6f, textY),
                markCol, new string('!', Math.Min(3, hurt)));
        }
        dl.PopClipRect();

        if (C.UpcomingBoardTimeText)
        {
            var tp = new Vector2(p1.X - timeW - 8f, textY);
            // At go time, "NOW" gets a filled accent badge that flashes (when
            // pulsing is on) so the press moment is impossible to miss. Solid
            // badge when pulsing is off, so it's still louder than plain text.
            if (isNow && accent != 0)
            {
                var beat = pulse ? MathF.Sin((float)ImGui.GetTime() * 10f) * 0.5f + 0.5f : 1f;
                var badge = (accent & 0x00FFFFFF) | ((uint)(0x40 + 0xA0 * beat) << 24);
                dl.AddRectFilled(tp - new Vector2(5f, 2f),
                    new Vector2(tp.X + timeW + 5f, tp.Y + lineH + 2f), badge, 4f);
                BoardText(dl, tp, 0xFFFFFFFFu, timeText);
            }
            else
                BoardText(dl, tp, textCol, timeText);
        }

        // Completion spark: as the countdown crosses zero, pop a spark at the left
        // end where the fill drains out, tinted the row's color. rem drives it, so
        // it plays for ~0.6s once and needs no state.
        if (rem <= 0.05f && rem > -0.55f)
        {
            var sparkCol = wash != 0 ? wash : accent == 0 ? AccentCol : accent;
            var sp = new Vector2(p0.X + 5f, p0.Y + barH * 0.5f);
            BoardSpark(dl, sp, (0.05f - rem) / 0.6f, sparkCol);
        }
        ImGui.Dummy(new Vector2(width, barH));
    }

    // What kind of hit a row is, for its board icon: 2 = tank buster, 1 =
    // raidwide (party damage), 0 = no icon. Explicit tags win (custom sheets and
    // log import set Buster/Hurt); otherwise we guess from the mechanic name so
    // built-in fights still show something. A bare user timer stays iconless.
    private static int RowKind(SheetTimeline.MechRow r, bool bareTimer)
    {
        if (r.Buster) return 2;
        if (r.Hurt > 0) return 1;
        if (bareTimer) return 0;
        var n = r.Mechanic.ToLowerInvariant();
        if (n.Contains("buster") || n.Contains("cleave")) return 2;
        if (n.Contains("enrage")) return 0; // lethal, not something you mit
        return 1; // a named mechanic on a mit board is there because it hits
    }

    // Draw a FontAwesome glyph into the draw list, centered on `center` and sized
    // to `size`. The board renders through the draw list (not ImGui widgets), so
    // we borrow the icon font for the one glyph and scale it to the board font.
    private void BoardIcon(ImDrawListPtr dl, Vector2 center, float size, uint col, FontAwesomeIcon icon)
    {
        var glyph = icon.ToIconString();
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var font = ImGui.GetFont();
            var ts = ImGui.CalcTextSize(glyph);
            var w = font.FontSize > 0f ? ts.X * (size / font.FontSize) : ts.X;
            var pos = new Vector2(center.X - w * 0.5f, center.Y - size * 0.5f);
            if (C.TextShadow) dl.AddText(font, size, pos + new Vector2(1f, 1f), 0xC0000000, glyph);
            dl.AddText(font, size, pos, col, glyph);
        }
    }

    // A brief spark when a countdown finishes: a white-hot core that expands into
    // a fading ring with a few radiating rays. Driven purely off how far past zero
    // the row is (progress 0->1 over ~0.6s), so it needs no stored per-row state.
    private static void BoardSpark(ImDrawListPtr dl, Vector2 c, float progress, uint color)
    {
        var p = Math.Clamp(progress, 0f, 1f);
        var fade = 1f - p;
        var rgb = color & 0x00FFFFFF;
        // expanding ring, white and fading fast
        var ringA = (uint)(0xC0 * fade * fade) << 24;
        dl.AddCircle(c, 2f + p * 9f, ringA | 0x00FFFFFF, 14, 1.6f * fade + 0.4f);
        // white-hot core
        var coreA = (uint)(0xF0 * fade) << 24;
        dl.AddCircleFilled(c, 1.4f + fade * 1.8f, coreA | 0x00FFFFFF);
        // rays in the row's own color, thrown outward as it fades
        var rayA = ((uint)(0xB0 * fade * fade) << 24) | rgb;
        for (var i = 0; i < 6; i++)
        {
            var ang = i * (MathF.PI / 3f) + 0.4f;
            var dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
            var r0 = 2.5f + p * 5f;
            dl.AddLine(c + dir * r0, c + dir * (r0 + 3.5f + p * 5f), rayA, 1.3f * fade + 0.3f);
        }
    }

    private void BoardActions(List<MitLine> mine, string? job, float elapsed, float width, uint accent)
    {
        var parts = new List<string>();
        var icon = 0u;
        var cdWarn = false;
        foreach (var l in mine)
        {
            var text = Icons.DisplayAction(l.ActionFor(job), job);
            if (string.IsNullOrWhiteSpace(text)) continue;
            // Off-row presses take the mit-type tint (party/tank/personal),
            // dimmed so gold/green still own the eye. Same colors as the call.
            if (accent == 0 && C.ColorByMitType && MitTypes.Color(MitTypes.Classify(text, l.Mechanic), C) is not 0 and var tc)
                accent = (tc & 0x00FFFFFF) | 0xC8000000;
            // Cooldown-aware: flag a press that won't be back up by ITS call
            // moment (the cue clock, so per-line offsets are honored).
            if (C.CooldownAwareCalls && Cooldowns.Remaining(l.Action) is { } cd && cd > l.CueTime - elapsed + 0.5f)
            { text += " (cd)"; cdWarn = true; }
            if (!parts.Contains(text)) parts.Add(text);
            // Icon from the first line that actually contributes text.
            if (icon == 0 && C.ShowAbilityIcon) icon = Icons.For(l, job);
        }
        if (parts.Count == 0) return;
        // A press that won't be back in time BLINKS between two warning reds,
        // so it catches the eye mid-fight instead of hiding in muted text.
        if (cdWarn)
            accent = ImGui.GetTime() % 0.9 < 0.45 ? 0xFF4646FF : 0xFF3535B4;
        BoardActionText(string.Join(" + ", parts), icon, accent, width);
    }

    private void BoardActionText(string text, uint iconId, uint accent, float width)
    {
        var color = accent == 0 ? BoardMuted : accent;
        var startX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(startX + 10f);
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

    // Brightness oscillation for the go-time stripe, preserving alpha (same
    // rhythm as the main call's imminent pulse).
    private static uint Pulse(uint abgr)
    {
        var t = MathF.Sin((float)ImGui.GetTime() * 12f) * 0.5f + 0.5f;
        var factor = 0.55f + 0.45f * t;
        var a = abgr & 0xFF000000;
        var b = (uint)(((abgr >> 16) & 0xFF) * factor) & 0xFF;
        var g = (uint)(((abgr >> 8) & 0xFF) * factor) & 0xFF;
        var r = (uint)((abgr & 0xFF) * factor) & 0xFF;
        return a | (b << 16) | (g << 8) | r;
    }

    // Draw-list text with the overlay's readability shadow.
    private void BoardText(ImDrawListPtr dl, Vector2 pos, uint color, string text)
    {
        if (C.TextShadow) dl.AddText(pos + new Vector2(1.5f, 1.5f), 0xE0000000, text);
        dl.AddText(pos, color, text);
    }

    private static string TimeText(float seconds)
    {
        var t = (int)MathF.Round(seconds);
        return $"{t / 60}:{t % 60:00}";
    }

    // ---- on-screen preview -------------------------------------------------
    // The Next Mits settings page pings this every frame it's open; while the
    // pings are fresh, the REAL window shows on screen and plays a sample, so
    // you're placing and styling the actual thing. Stops within a blink of
    // leaving the page or closing settings. A running pull always wins.
    private DateTime _screenPreviewPing = DateTime.MinValue;
    public void PingScreenPreview() => _screenPreviewPing = DateTime.Now;
    private bool ScreenPreviewing => (DateTime.Now - _screenPreviewPing).TotalSeconds < 0.3;

    // The sample both previews play: Dancing Mad's real rows with the MT
    // column's presses, looping through the opener so bars drain, a press goes
    // gold, fires green, and lingers - exactly like a pull. Rows are built
    // once and kept separate from the live board's cache.
    private FightProfile? _previewFight;
    private List<SheetTimeline.MechRow>? _previewRows;

    private void DrawDmuSample()
    {
        _previewFight ??= new FightProfile
        {
            TerritoryId = Builtin.DmuTerritory,
            Name = "Dancing Mad (UMAD)",
            Slot = "T1",
            Lines = Builtin.BuildLines(Builtin.DmuTerritory, "T1"),
        };
        _previewRows ??= SheetTimeline.Build(_previewFight);

        // Loop DMU's opener: Double-Trouble Trap, Light of Judgment and both
        // Gravitas II hits pass through every sweep.
        var elapsed = 45f + (float)(ImGui.GetTime() % 62.0);

        using var _ = PushFont(C.UpcomingFontSizePx);
        if (C.UpcomingStyle == 1)
        {
            DrawBoard(_previewFight, null, elapsed, _previewRows);
            return;
        }

        // Compact list style: the same DMU moment, classic look.
        var upcoming = _previewFight.OrderedLines
            .Where(l => l.Enabled
                        && l.CueTime - elapsed > C.WarningSeconds
                        && l.CueTime - elapsed <= C.UpcomingLookaheadSeconds)
            .OrderBy(l => l.CueTime)
            .Take(Math.Max(1, C.UpcomingCount))
            .ToList();
        if (upcoming.Count == 0)
        {
            // Quiet stretch of the loop: keep something visible for placement.
            Row(0u, "", "(next mits show here)", true);
            return;
        }
        foreach (var l in upcoming)
        {
            var inSec = (int)MathF.Round(l.CueTime - elapsed);
            var nm = string.IsNullOrWhiteSpace(l.Action) ? l.Mechanic : Icons.DisplayAction(l.Action, null);
            Row(C.ShowAbilityIcon ? Icons.For(l, null) : 0u, $"+{inSec}s  ", nm);
        }
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

    // Drag the board to move it (when unlocked). We do this ourselves rather than
    // lean on ImGui's window-move, which in this build only triggers from a title
    // bar - and the board deliberately has none. A press over the window starts
    // the drag; we track it to release even if the cursor slips off the
    // auto-resizing window, and edit the saved position by the raw mouse delta.
    private void HandleManualDrag()
    {
        if (EffectiveLocked) { _dragging = false; return; }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (_dragging) { _dragging = false; C.Save(); } // persist once, on release
            return;
        }
        if (!_dragging)
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsWindowHovered())
                _dragging = true;
            else
                return;
        }

        var d = ImGui.GetIO().MouseDelta;
        if (d.X == 0f && d.Y == 0f) return;
        var work = ImGui.GetMainViewport().WorkSize;
        var frac = C.TimelinePosition + new Vector2(d.X / work.X, d.Y / work.Y);
        C.TimelinePosition = new Vector2(Math.Clamp(frac.X, 0f, 1f), Math.Clamp(frac.Y, 0f, 1f));
    }
}
