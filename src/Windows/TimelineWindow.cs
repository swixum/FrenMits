using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// The next-mits timeline: a separate window listing the upcoming calls.
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

    // Locked when you tick it, or during a live pull.
    private bool EffectiveLocked => OverlayChrome.Locked(C.TimelineLocked, C);

    // The window follows the saved position every frame.
    public void RequestReposition() { }

    public override void PreDraw()
    {
        // No title bar ever, so locking can't shift the content.
        Flags = ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.AlwaysAutoResize;

        if (!C.ShowBackground)
            Flags |= ImGuiWindowFlags.NoBackground;

        // Movement is manual, since ImGui moves from a title bar.
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
        // The settings preview plays a sample in the real window.
        if (ScreenPreviewing) return true;
        if (!C.ShowUpcoming) return false;
        if (C.TestMode) return true;
        // Stays up through cutscenes, so a downtime still reads.
        if (_plugin.ActiveFight() is not { } fight) return false;
        if (C.OnlyInTargetTerritory && fight.TerritoryId != Service.ClientState.TerritoryType) return false;
        // Live, so the board counts while a pull countdown runs.
        return _plugin.Timer.Live;
    }

    public override void Draw()
    {
        HandleManualDrag();

        // Both preview paths play the same sample here.
        if ((C.TestMode || ScreenPreviewing) && !_plugin.Timer.Live)
        {
            DrawDmuSample();
            return;
        }

        var fight = _plugin.ActiveFight();
        if (fight == null) return;

        var job = _plugin.ActiveJobAbbreviation();
        // Cue clock, so the hand-off to the live call stays seamless.
        var elapsed = _plugin.CueClockFor(fight);

        if (C.UpcomingStyle == 1)
        {
            using (PushFont(C.UpcomingFontSizePx))
                DrawBoard(fight, job, elapsed);
            return;
        }

        var earlyWindow = C.WarningSeconds;
        var upcoming = _plugin.ActivePresses()
            .Where(p =>
            {
                var l = p.SourceLine;
                if (!l.Enabled || !l.AppliesTo(job)) return false;
                // Resolve generic terms first, or their presses never match.
                var jobAction = Icons.DisplayAction(l.ActionFor(job), job);
                var mitsForJob = Cooldowns.PlanMitsCached(jobAction);
                var handlesThisPress = false;
                for (var i = 0; i < mitsForJob.Count; i++)
                    if (string.Equals(mitsForJob[i].Name, p.MitName, StringComparison.OrdinalIgnoreCase))
                        handlesThisPress = true;
                if (!handlesThisPress) return false;
                var rem = p.WindowStart - elapsed;
                return rem > earlyWindow && rem <= C.UpcomingLookaheadSeconds;
            })
            .OrderBy(p => p.WindowStart)
            .Take(Math.Max(0, C.UpcomingCount))
            .ToList();

        if (upcoming.Count == 0)
        {
            // Keep the window from collapsing to a dot between calls.
            ImGui.Dummy(new Vector2(1f, 1f));
            return;
        }

        using (PushFont(C.UpcomingFontSizePx))
            foreach (var p in upcoming)
            {
                var l = p.SourceLine;
                var inSec = (int)MathF.Round(p.WindowStart - elapsed);
                var name = string.IsNullOrWhiteSpace(p.MitName) ? l.Mechanic : Icons.DisplayAction(p.MitName, job);
                var icon = C.ShowAbilityIcon ? Icons.ResolveFromText(p.MitName) : 0u;
                // Mark a mit that won't be ready when it's called.
                var notReady = C.CooldownAwareCalls
                    && Cooldowns.Remaining(p.MitName) is { } cd && cd > (p.WindowStart - elapsed) + 0.5f;
                Row(icon, $"+{inSec}s  ", name + (notReady ? "  (cd)" : ""), notReady);
            }
    }

    // ---- mechanic board ----

    // Board palette, the config window's theme over the game.
    private const uint BoardBarBorder = 0x66594A3F; // soft slate border
    private const uint BoardBright = 0xFFECE8E6;    // Theme text
    private const uint BoardRaidCol = 0xFFE0C860;   // raidwide: cool cyan-blue
    private const uint BoardBusterCol = 0xFF4090F0; // tank buster: warm orange
    private const uint BoardMuted = 0xFFA89A90;     // muted gray
    private const uint BoardPanelRgb = 0x0014110E;  // Theme.PanelBg, opacity applied on top

    // The customizable colors, guarded against a zeroed value.
    private uint AccentCol => C.UpcomingBoardAccentColor != 0 ? C.UpcomingBoardAccentColor : 0xFFF6823B;
    private uint NextCol => C.UpcomingBoardNextColor != 0 ? C.UpcomingBoardNextColor : 0xFF28BEFF;
    private uint NowCol => C.UpcomingBoardNowColor != 0 ? C.UpcomingBoardNowColor : 0xFF64DC64;
    private float BoardRound => Math.Clamp(C.UpcomingBoardRounding, 0f, 12f);

    // Derived from every column, so it's cached.
    private List<SheetTimeline.MechRow> _board = new();
    private string _boardFightId = "";
    private int _boardGen = -1;
    private int _boardStamp = -1;
    private DateTime _boardBuiltAt = DateTime.MinValue;

    private List<SheetTimeline.MechRow> BoardRows(FightProfile fight)
    {
        // A cheap fingerprint, since counts miss equal-count edits.
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

    // Phase marks change only with the fight, so build once.
    private List<SheetTimeline.PhaseMark> _marks = new();
    private string _marksFightId = "";

    private List<SheetTimeline.PhaseMark> PhaseMarks(FightProfile fight)
    {
        if (_marksFightId != fight.Id)
        {
            _marks = SheetTimeline.PhaseMarks(fight);
            _marksFightId = fight.Id;
        }
        return _marks;
    }

    private static readonly List<MitPress> NoLines = new();
    private static readonly List<SheetTimeline.MechRow> NoRows = new();

    // ---- per-frame scratch ----
    private readonly List<SheetTimeline.MechRow> _windowRows = new();
    private readonly List<SheetTimeline.MechRow> _visibleRows = new();
    private readonly List<List<MitPress>> _mineForVisible = new();
    private readonly Dictionary<SheetTimeline.MechRow, List<MitPress>> _mineByRow = new();
    private readonly List<List<MitPress>> _linePool = new();
    private int _linePoolUsed;

    // A pooled press list, so rows don't allocate each frame.
    private List<MitPress> RentLineList()
    {
        if (_linePoolUsed == _linePool.Count) _linePool.Add(new List<MitPress>());
        var list = _linePool[_linePoolUsed++];
        list.Clear();
        return list;
    }

    // Rows inside the board's window, into the scratch buffer.
    private void AddWindowRows(List<SheetTimeline.MechRow> src, float elapsed, float look)
    {
        foreach (var r in src)
        {
            var rem = r.Time - elapsed;
            if (rem >= -2f && rem <= look) _windowRows.Add(r);
        }
    }

    // Insertion sort: stable, and the board holds few rows.
    private static void StableSortByTime(List<SheetTimeline.MechRow> rows)
    {
        for (var i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            var j = i - 1;
            while (j >= 0 && rows[j].Time > r.Time) { rows[j + 1] = rows[j]; j--; }
            rows[j + 1] = r;
        }
    }

    // Cached, rebuilt only when the windows actually move.
    private IReadOnlyList<DowntimeWindow>? _downWins;
    private List<DowntimeWindow>? _downBase;
    private string _downFightId = "";
    private List<DowntimeWindow>? _downCustomSrc;
    private int _downCustomCount = -1;
    private List<SheetTimeline.MechRow>? _downRows;
    private uint _posTerritory = uint.MaxValue;
    private List<SheetTimeline.MechRow>? _posRows;

    // Credit: the idea of surfacing boss untargetable/targetable windows on a
    // fight timeline, and the timing data these windows are built from, come
    // from cactbot (github.com/OverlayPlugin/cactbot, Apache License 2.0,
    // Copyright the cactbot authors), which FrenMits adapts onto its own clock
    // (see Data/Downtimes.cs) and renders its own way.
    private IReadOnlyList<DowntimeWindow> EffectiveDowntimes(FightProfile fight)
    {
        // Effective hands back a cached list, so identity signals change.
        var baseWins = Downtimes.Effective(fight.TerritoryId, C.LearnedDowntimes);
        if (_downWins != null && ReferenceEquals(_downBase, baseWins) && _downFightId == fight.Id
            && ReferenceEquals(_downCustomSrc, fight.CustomDowntimes)
            && _downCustomCount == fight.CustomDowntimes.Count)
            return _downWins;

        _downBase = baseWins;
        _downFightId = fight.Id;
        _downCustomSrc = fight.CustomDowntimes;
        _downCustomCount = fight.CustomDowntimes.Count;
        _downRows = null; // the row set below is derived from this
        if (fight.CustomDowntimes.Count == 0) return _downWins = baseWins;
        if (baseWins.Count == 0) return _downWins = fight.CustomDowntimes;
        return _downWins = Downtimes.Merge(baseWins, fight.CustomDowntimes);
    }

    // Learned downtimes as inline rows, each counting down.
    private List<SheetTimeline.MechRow> DowntimeRows(FightProfile fight)
    {
        var list = EffectiveDowntimes(fight); // clears _downRows if the windows moved
        if (list.Count == 0) return NoRows;
        if (_downRows != null) return _downRows;
        var rows = new List<SheetTimeline.MechRow>(list.Count * 2);
        foreach (var w in list)
        {
            rows.Add(new SheetTimeline.MechRow { Time = w.Start, Mechanic = "Untargetable" });
            rows.Add(new SheetTimeline.MechRow { Time = w.Start + w.Duration, Mechanic = "Targetable" });
        }
        return _downRows = rows;
    }

    // Scheduled boss-reposition rows, from the Positions data.
    private List<SheetTimeline.MechRow> PositionRows(FightProfile fight)
    {
        if (!C.UpcomingBossPosition) return NoRows;
        if (_posRows != null && _posTerritory == fight.TerritoryId) return _posRows;
        var spots = Positions.For(fight.TerritoryId);
        _posTerritory = fight.TerritoryId;
        if (spots.Count == 0) return _posRows = NoRows;
        var rows = new List<SheetTimeline.MechRow>(spots.Count);
        foreach (var s in spots)
            rows.Add(new SheetTimeline.MechRow { Time = s.Time, Mechanic = $"Boss: {s.Where}", Position = s.Where });
        return _posRows = rows;
    }

    // The gate health for this lull, or -1 with no check.
    private float DowntimeTargetHp(FightProfile fight, float rowStart)
    {
        foreach (var w in EffectiveDowntimes(fight))
            if (MathF.Abs(w.Start - rowStart) < 2f) return w.TargetHp;
        return -1f;
    }

    // How early the green Targetable heads-up replaces the row.
    private const float TargetableHeadsup = 10f;

    // Whether this lull is a cutscene, driving its label.
    private bool DowntimeIsCutscene(FightProfile fight, float targetableTime)
    {
        foreach (var w in EffectiveDowntimes(fight))
            if (MathF.Abs(w.Start + w.Duration - targetableTime) < 2f) return w.Cutscene;
        return false;
    }

    private void DrawBoard(FightProfile fight, string? job, float elapsed,
        List<SheetTimeline.MechRow>? rowsOverride = null, float? widthOverride = null)
    {
        var look = MathF.Max(10f, C.UpcomingBoardLookaheadSeconds);
        var width = widthOverride ?? MathF.Max(180f, C.UpcomingBoardWidth);
        // A just-hit row lingers, so it can't vanish mid-press.
        _windowRows.Clear();
        AddWindowRows(rowsOverride ?? BoardRows(fight), elapsed, look);
        AddWindowRows(DowntimeRows(fight), elapsed, look);
        AddWindowRows(PositionRows(fight), elapsed, look);
        StableSortByTime(_windowRows);
        var windowRows = _windowRows;

        if (HeaderVisible) DrawBoardHeader(fight, elapsed, width);

        // Downtimes ride inline; the banner is only the fallback.
        if (_plugin.DowntimeActive && _plugin.DowntimeRemaining < 0f) DrawDowntimeBanner(width);

        // Attach each press to its nearest row only.
        _mineByRow.Clear();
        _linePoolUsed = 0;
        if (!fight.TimelineOnly)
            foreach (var p in _plugin.ActivePresses())
            {
                var l = p.SourceLine;
                if (!l.Enabled || !l.AppliesTo(job)) continue;
                // Only the presses this job actually owns, with generic terms resolved.
                var mitsForJob = Cooldowns.PlanMitsCached(Icons.DisplayAction(l.ActionFor(job), job));
                var handlesThisPress = false;
                for (var i = 0; i < mitsForJob.Count; i++)
                    if (string.Equals(mitsForJob[i].Name, p.MitName, StringComparison.OrdinalIgnoreCase))
                        handlesThisPress = true;
                if (!handlesThisPress) continue;
                if (p.WindowStart < elapsed - 6f || p.WindowStart > elapsed + look + 4f) continue;
                SheetTimeline.MechRow? best = null;
                var bestGap = 2.5f;
                foreach (var r in windowRows)
                {
                    var gap = MathF.Abs(p.TargetHitTime - r.Time);
                    if (gap < bestGap && SheetTimeline.MechEquals(l.Mechanic, r.Mechanic)) { best = r; bestGap = gap; }
                }
                if (best == null) continue;
                if (!_mineByRow.TryGetValue(best, out var list)) _mineByRow[best] = list = RentLineList();
                list.Add(p);
            }

        List<MitPress> MineFor(SheetTimeline.MechRow r)
            => _mineByRow.TryGetValue(r, out var list) ? list : NoLines;

        // Trim to your own rows before the cap, not after.
        _visibleRows.Clear();
        _mineForVisible.Clear();
        var rowCap = Math.Max(1, C.UpcomingBoardRows);
        foreach (var r in windowRows)
        {
            if (_visibleRows.Count >= rowCap) break;
            var m = MineFor(r);
            if (C.UpcomingBoardOnlyMine && m.Count == 0) continue;
            _visibleRows.Add(r);
            _mineForVisible.Add(m);
        }
        var visible = _visibleRows;
        var mine = _mineForVisible;

        if (visible.Count == 0)
        {
            ImGui.Dummy(new Vector2(width, 1f));
            return;
        }

        // Green matches the main call, so offsets stay in lockstep.
        bool InWindow(MitPress p)
            => p.WindowStart - elapsed <= C.WarningSeconds;

        // A plain loop, or it's a delegate per row per frame.
        bool AnyInWindow(List<MitPress> lines)
        {
            for (var i = 0; i < lines.Count; i++)
                if (InWindow(lines[i])) return true;
            return false;
        }

        // Gold marks your next press that isn't already green.
        var nextIdx = -1;
        for (var i = 0; i < visible.Count && nextIdx < 0; i++)
            if (mine[i].Count > 0 && !AnyInWindow(mine[i]))
                nextIdx = i;

        // Negative spacing pulls bars into each other (overlap look).
        var rowGap = Math.Clamp(C.UpcomingBoardRowGap, -8f, 16f);
        for (var i = 0; i < visible.Count; i++)
        {
            var r = visible[i];
            // A phase beginning between this row and the one above it.
            var phase = i > 0 && C.UpcomingBoardPhases
                ? SheetTimeline.PhaseBetween(PhaseMarks(fight), visible[i - 1].Time, r.Time)
                : "";
            if (phase.Length > 0) BoardPhase(phase, width);
            else if (i > 0 && rowGap > 0f) ImGui.Dummy(new Vector2(1f, rowGap));
            else if (i > 0 && rowGap < 0f) ImGui.SetCursorPosY(ImGui.GetCursorPosY() + rowGap);
            var rem = r.Time - elapsed;
            var useNow = mine[i].Count > 0 && AnyInWindow(mine[i]);
            var isNext = i == nextIdx;
            var accent = useNow ? NowCol : isNext ? NextCol : 0u;
            var pulse = useNow && C.PulseWhenImminent && rem < 1.5f;

            // A bare timer row is named by its press.
            var name = r.Mechanic;
            var bareTimer = string.IsNullOrWhiteSpace(name);
            if (bareTimer)
                name = mine[i].Count > 0
                    ? Icons.DisplayAction(mine[i][0].MitName, job)
                    : r.Fallback;

            // Row kind: a lull marker, or the mechanic's hit type.
            var gate = false;
            var gatePassed = false;
            var gateTgt = -1f;
            if (r.Mechanic == "Untargetable")
            {
                gateTgt = DowntimeTargetHp(fight, r.Time);
                // Only real DPS checks, not brief lulls at high health.
                if (_plugin.BossHpFraction > 0f && gateTgt is >= 0f and <= 0.40f
                    && _plugin.BossHpFraction <= gateTgt + 0.10f)
                {
                    gate = true;
                    // Target hit: flip to the passed look instead of at-risk.
                    gatePassed = _plugin.BossHpFraction <= gateTgt;
                }
            }
            var kind = r.Mechanic == "Untargetable" ? (gate ? (gatePassed ? 8 : 3) : 4)
                : r.Mechanic == "Targetable" ? 5
                : r.Position.Length > 0 ? 7
                : RowKind(r, bareTimer);
            if (kind == 3 || kind == 8) name = $"DPS check ({gateTgt * 100f:0}%)";
            // A far-off targetable reads as the lull you're in.
            if (kind == 5 && rem > TargetableHeadsup)
            {
                if (DowntimeIsCutscene(fight, r.Time)) { kind = 6; name = "Cutscene"; }
                else { kind = 4; name = "Untargetable"; }
            }
            BoardBar(name, rem, look, width, accent, r.Hurt, pulse, kind);

            if (C.UpcomingBoardShowActions && !bareTimer && mine[i].Count > 0)
                BoardActions(mine[i], job, elapsed, width, accent);
            // No under-bar text: prep cues live on the main call.
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
            // Signed, so a countdown reads negative into the pull.
            var clock = elapsed < 0f ? Fmt.MmssSigned(elapsed) : TimeText(elapsed);
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
            // The little FrenMits tick, an accent diamond.
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

        // Trust line: what the clock last locked onto, fading out.
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

    // A neutral banner while the boss is untargetable.
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
        // Count down once the lull is learned, else just measure it.
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
        // Kind wash: a soft color so the row's nature reads first.
        var wash = kind switch
        {
            3 => 0xFF4646FFu, // DPS check (at risk): red
            4 => 0xFF9AA0A8u, // untargetable: grey
            5 => 0xFF7BD88Bu, // targetable: green
            6 => 0xFFB48C96u, // cutscene: purple
            7 => 0xFFDCC85Au, // boss reposition: cyan
            8 => 0xFF99D334u, // DPS check (passed): emerald (distinct from targetable lime)
            _ => 0u,
        };
        if (wash != 0)
            dl.AddRectFilled(p0, p1, (wash & 0x00FFFFFFu) | 0x40000000u, round);
        // The fill tracks the countdown, draining by default.
        var frac = Math.Clamp(rem / look, 0f, 1f);
        if (!C.UpcomingBoardDrain) frac = 1f - frac;
        if (frac > 0.004f) // countdown fill on every row, lull markers included
        {
            // Lull rows drain in their wash; ordinary rows use the accent.
            var baseCol = (wash != 0 ? wash : accent == 0 ? AccentCol : accent) & 0x00FFFFFF;
            var edgeX = p0.X + width * frac;
            // A solid base plus a gradient peaking at the moving edge.
            var corners = frac >= 0.999f ? ImDrawFlags.RoundCornersAll : ImDrawFlags.RoundCornersLeft;
            dl.AddRectFilled(p0, new Vector2(edgeX, p1.Y), baseCol | 0x66000000, round, corners);
            dl.AddRectFilledMultiColor(p0, new Vector2(edgeX, p1.Y),
                baseCol | 0x14000000, baseCol | 0x7A000000, baseCol | 0x7A000000, baseCol | 0x14000000);
            // A crisp bright edge rides the boundary, hidden at the ends.
            if (frac > 0.02f && frac < 0.985f)
                dl.AddRectFilled(new Vector2(edgeX - 1.5f, p0.Y + 1f),
                    new Vector2(edgeX + 0.5f, p1.Y - 1f), baseCol | 0xF0000000);
        }
        // The signature stripe: accent, or gold when the row is yours.
        if (C.UpcomingBoardStripe)
        {
            var stripe = kind switch
            {
                3 => 0xFF4646FFu,   // at-risk: red
                4 => 0xFF9AA0A8u,   // untargetable: slate
                5 => 0xFF7BD88Bu,   // targetable: green
                6 => 0xFFB48C96u,   // cutscene / downtime: muted lavender
                7 => 0xFFDCC85Au,   // boss reposition: cyan
                8 => 0xFF99D334u,   // DPS check passed: emerald
                _ => accent == 0 ? (AccentCol & 0x00FFFFFF) | 0xB3000000 : accent,
            };
            if (pulse) stripe = Pulse(stripe);
            dl.AddRectFilled(p0, new Vector2(p0.X + 3f, p1.Y), stripe, round, ImDrawFlags.RoundCornersLeft);
        }
        dl.AddRect(p0, p1, BoardBarBorder, round);

        // Every row's text is one color; the wash carries identity.
        var textCol = accent == 0 ? BoardBright : accent;
        var textY = p0.Y + (barH - lineH) * 0.5f;
        var isNow = rem < 0f;
        // Under 3s, one decimal so the last moments read finely.
        var timeText = isNow ? "NOW" : rem < 3f ? $"{rem:0.0}s" : $"{MathF.Ceiling(rem):0}s";
        var timeW = C.UpcomingBoardTimeText ? ImGui.CalcTextSize(timeText).X : 0f;

        // Row icon: buster shield, at-risk skull, or a lull marker.
        var nameX = p0.X + 10f;
        var showIcon = kind switch
        {
            2 => C.UpcomingBoardShowType,
            3 or 4 or 5 or 6 or 8 => true,
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
                8 => (FontAwesomeIcon.Check, 0xFF99D334u),
                _ => (FontAwesomeIcon.Shield, BoardBusterCol),
            };
            var isz = lineH * 0.82f;
            BoardIcon(dl, new Vector2(nameX + isz * 0.5f, p0.Y + barH * 0.5f), isz, iconCol, glyph);
            nameX += isz + 6f;
        }

        // Clip the name, so it can't run under the countdown.
        dl.PushClipRect(p0, new Vector2(p1.X - (timeW > 0f ? timeW + 14f : 8f), p1.Y), true);
        BoardText(dl, new Vector2(nameX, textY), textCol, name);
        // Severity marks from a graded custom sheet: !
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
            // At go time, NOW gets a filled badge that flashes.
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

        // A spark where the fill drains out, in the row's color.
        if (rem <= 0.05f && rem > -0.55f)
        {
            var sparkCol = wash != 0 ? wash : accent == 0 ? AccentCol : accent;
            var sp = new Vector2(p0.X + 5f, p0.Y + barH * 0.5f);
            BoardSpark(dl, sp, (0.05f - rem) / 0.6f, sparkCol);
        }
        ImGui.Dummy(new Vector2(width, barH));
    }

    // What kind of hit a row is: 2 buster, 1 raidwide, 0 none.
    private static int RowKind(SheetTimeline.MechRow r, bool bareTimer)
    {
        if (r.Buster) return 2;
        if (r.Hurt > 0) return 1;
        if (bareTimer) return 0;
        // Compare in place, since lower-casing allocated per row.
        var n = r.Mechanic;
        if (n.Contains("buster", StringComparison.OrdinalIgnoreCase)) return 2;
        if (n.Contains("enrage", StringComparison.OrdinalIgnoreCase)) return 0; // lethal, not something you mit
        return 1; // a named mechanic is on the board because it hits
    }

    // Draw a glyph into the draw list, centered and sized.
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

    // A brief spark: a hot core expanding into a fading ring.
    private static void BoardSpark(ImDrawListPtr dl, Vector2 c, float progress, uint color)
        => OverlayChrome.Spark(dl, c, progress, color);

    // Reused, since this runs once per visible row.
    private readonly List<string> _actionParts = new();

    private string _phaseSrc = "";
    private string _phaseUpper = "";

    // The phase divider: a tick, the name, and a hairline.
    private void BoardPhase(string label, float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        // Upper-cased, so it reads as a label not a mechanic.
        if (!string.Equals(label, _phaseSrc, StringComparison.Ordinal))
        {
            _phaseSrc = label;
            _phaseUpper = label.ToUpperInvariant();
        }
        var text = _phaseUpper;
        var accent = AccentCol & 0x00FFFFFF;

        var p0 = ImGui.GetCursorScreenPos();
        var h = MathF.Round(lineH * 1.25f); // a little air above and below
        var mid = MathF.Round(p0.Y + h * 0.5f);

        var tickH = MathF.Round(lineH * 0.62f);
        dl.AddRectFilled(new Vector2(p0.X, mid - tickH * 0.5f),
            new Vector2(p0.X + 3f, mid + tickH * 0.5f), accent | 0xFF000000, 1f);

        var textX = p0.X + 9f;
        BoardText(dl, new Vector2(textX, p0.Y + (h - lineH) * 0.5f), accent | 0xDD000000, text);

        // A hairline to the right edge, fading as it goes.
        var ruleX = textX + ImGui.CalcTextSize(text).X + 8f;
        var right = p0.X + width;
        if (right - ruleX > 8f)
            dl.AddRectFilledMultiColor(new Vector2(ruleX, mid), new Vector2(right, mid + 1f),
                accent | 0x80000000, accent | 0x00000000, accent | 0x00000000, accent | 0x80000000);

        ImGui.Dummy(new Vector2(width, h));
    }

    private void BoardActions(List<MitPress> mine, string? job, float elapsed, float width, uint accent)
    {
        var parts = _actionParts;
        parts.Clear();
        var icon = 0u;
        var cdWarn = false;
        foreach (var p in mine)
        {
            var text = Icons.DisplayAction(p.MitName, job);
            if (string.IsNullOrWhiteSpace(text)) continue;
            var l = p.SourceLine;
            // Off-row presses take the mit-type tint, dimmed.
            if (accent == 0 && C.ColorByMitType && MitTypes.Color(MitTypes.Classify(text, l.Mechanic), C) is not 0 and var tc)
                accent = (tc & 0x00FFFFFF) | 0xC8000000;
            // Flag a press that won't be back by its own call moment.
            if (C.CooldownAwareCalls && Cooldowns.Remaining(p.MitName) is { } cd && cd > (p.WindowStart - elapsed) + 0.5f)
            { text += " (cd)"; cdWarn = true; }
            if (!parts.Contains(text)) parts.Add(text);
            // Icon from the first press that actually contributes text.
            if (icon == 0 && C.ShowAbilityIcon) icon = Icons.ResolveFromText(p.MitName);
        }
        if (parts.Count == 0) return;
        // A press that won't be back blinks, so it catches the eye.
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

    // Brightness oscillation for the go-time stripe.
    private static uint Pulse(uint abgr) => OverlayChrome.Pulse(abgr);

    // Draw-list text with the overlay's readability shadow.
    private void BoardText(ImDrawListPtr dl, Vector2 pos, uint color, string text)
        => OverlayChrome.BoardText(dl, pos, color, text, C.TextShadow);

    private static string TimeText(float seconds) => Fmt.MmssRound(seconds);

    // ---- on-screen preview ----
    private DateTime _screenPreviewPing = DateTime.MinValue;
    public void PingScreenPreview() => _screenPreviewPing = DateTime.Now;
    private bool ScreenPreviewing => (DateTime.Now - _screenPreviewPing).TotalSeconds < 0.3;

    // The sample both previews play: Dancing Mad's opener on a loop.
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

        // DMU has no phase names, so dividers would show nothing.
        if (C.UpcomingBoardPhases && _previewFight.BossAnchors.Count == 0)
            foreach (var r in _previewRows)
                if (r.Time is >= 70f and <= 100f)
                {
                    _previewFight.BossAnchors.Add(new BossAnchor { Time = r.Time, Label = "P2 Kefka" });
                    break;
                }

        // Loop DMU's opener, so every sweep passes through.
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
            // Quiet stretch: keep something visible for placement.
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
        => OverlayChrome.PushFont(_plugin.Fonts, sizePx, C.OverlayFontFamily, C.OverlayFontBold, C.OverlayFontItalic);

    // Drag the board to move it, since there is no title bar.
    private void HandleManualDrag()
    {
        if (EffectiveLocked) { _dragging = false; return; }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (_dragging) { _dragging = false; C.SaveSettings(); } // persist once, on release
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
