using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// The damage meter overlay, with everything mid-pull on its right-click menu.
public class MeterWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    private bool _applyPos = true;
    private bool _posDirty;
    private bool _sizeDirty;
    private int _histIdx = -1; // -1 = live encounter

    // How long a finished pull stays on screen when hiding out of combat.
    private const double LingerSeconds = 12;

    public MeterWindow(Plugin plugin) : base("FrenMits Meter##frenmeter")
    {
        _plugin = plugin;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(230, 84),
            MaximumSize = new Vector2(2000, 1600),
        };
    }

    public void RequestReposition() => _applyPos = true;

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBackground;
        if (C.MeterLocked) Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
        if (C.MeterClickThrough) Flags |= ImGuiWindowFlags.NoMouseInputs;

        // Collapsed: hold the window at header height and let go of the grip.
        if (C.MeterCollapsed)
        {
            Flags |= ImGuiWindowFlags.NoResize;
            // The normal floor would quietly clamp a collapsed meter back open.
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(230, 20),
                MaximumSize = new Vector2(2000, 1600),
            };
            ImGui.SetNextWindowSize(new Vector2(C.MeterSize.X, _collapsedH), ImGuiCond.Always);
        }
        else
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(230, 84),
                MaximumSize = new Vector2(2000, 1600),
            };
            ImGui.SetNextWindowSize(C.MeterSize,
                C.MeterLocked || _applySize ? ImGuiCond.Always : ImGuiCond.Appearing);
        }
        _applySize = false;
        OverlayChrome.ApplyPosition(C.MeterPosition, C.MeterLocked, ref _applyPos);
    }

    public override bool DrawConditions()
    {
        if (!C.MeterEnabled) return false;
        if (Plugin.CutsceneActive && !C.TestMode) return false;
        // Out of combat: stay up long enough to read the pull, then get out.
        if (C.MeterHideOutOfCombat && !C.TestMode && !Plugin.InCombat && _histIdx < 0
            && _plugin.Meter.Current is not { Active: true }
            && (DateTime.UtcNow - _plugin.Meter.CurrentAt).TotalSeconds > LingerSeconds)
            return false;
        // Without this a reset would take the window until the next pull starts.
        return C.TestMode || C.MeterAlwaysShow || View() != null || !C.MeterLocked;
    }

    // The encounter on screen: a history pick, else live data, else the sample.
    private MeterEncounter? View()
    {
        var m = _plugin.Meter;
        // A fresh pull pulls the meter back to live and out of an old breakdown.
        if (m.Current is { Active: true } live)
        {
            _histIdx = -1;
            if (_detailFor.Length > 0 && live.Seconds + 1f < _detailSeconds) _detailFor = "";
            _detailSeconds = live.Seconds;
        }
        if (_histIdx >= 0 && _histIdx < m.History.Count) return m.History[_histIdx];
        _histIdx = -1;
        return m.Current ?? (C.TestMode ? m.Sample() : null);
    }

    public override void Draw()
    {
        SaveIfMoved();
        // The held numbers come off the real encounter; only the bars glide.
        var real = View();
        _live = real == null ? null : Smoothed(real);
        var enc = Held(real);
        _shown = enc;

        var wp = ImGui.GetWindowPos();
        var ws = ImGui.GetWindowSize();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(wp, wp + ws, C.MeterBgColor, C.MeterRounding);
        if (C.MeterBorderColor >> 24 != 0)
            dl.AddRect(wp, wp + ws, C.MeterBorderColor, C.MeterRounding);

        if (C.MeterBarStyle == 1)
        {
            // Glass style gets a soft top sheen on the window too.
            var sheenH = MathF.Min(46f, ws.Y * 0.4f);
            dl.AddRectFilledMultiColor(wp + new Vector2(1, 1), wp + new Vector2(ws.X - 1, sheenH),
                0x14FFFFFF, 0x14FFFFFF, 0x00FFFFFF, 0x00FFFFFF);
        }

        using var font = OverlayChrome.PushFont(
            _plugin.Fonts, C.MeterFontSizePx, C.MeterFontFamily, C.MeterFontBold, C.MeterFontItalic);

        // Rolled up: the header is the whole window, and it carries the way back.
        if (C.MeterCollapsed)
        {
            DrawCollapsed(enc, dl, wp, ws);
            ContextMenu();
            return;
        }

        if (enc == null)
        {
            // An empty board needs no saying; only a missing link is worth a line.
            var lineH = ImGui.GetTextLineHeight();
            var status = _plugin.Meter.Connected ? "" : _plugin.Meter.StatusText;
            var midY = ws.Y * 0.5f - (status.Length > 0 ? lineH : lineH * 0.5f);
            BText(dl, wp + new Vector2((ws.X - ImGui.CalcTextSize("Fren Meter").X) * 0.5f, midY),
                C.MeterTextColor, "Fren Meter");
            if (status.Length > 0)
                BText(dl, wp + new Vector2((ws.X - ImGui.CalcTextSize(status).X) * 0.5f, midY + lineH + 3f),
                    C.MeterSubColor, status);
            ContextMenu();
            return;
        }

        var pad = 9f;
        var y = 6f;
        DrawHeader(enc, dl, wp, ws, pad, ref y);

        EnsureColumns();
        if (C.MeterColumnHeader && C.MeterHeaderStyle != 2 && _detailFor.Length == 0)
            DrawColumnHeader(dl, wp, ws, pad, ref y);

        // Bars scroll in their own region, which click-through has to reach into.
        var footerH = (C.MeterButtons || C.MeterHealingTab || C.MeterFooterDeaths) && !C.MeterClickThrough
            ? MathF.Ceiling(ImGui.GetTextLineHeight()) + 10f
            : 0f;
        _overheadY = y + footerH + 4f;
        // Re-established every frame by whichever row the mouse is actually over.
        _rowUnderMouse = "";
        ImGui.SetCursorPos(new Vector2(0, y));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0u);
        if (ImGui.BeginChild("##meterrows", new Vector2(ws.X, ws.Y - y - footerH - 4f), false,
                ImGuiWindowFlags.NoScrollbar
                | (C.MeterClickThrough ? ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoNav : ImGuiWindowFlags.None)))
        {
            if (_detailFor.Length > 0) DrawDetail(enc, pad);
            else DrawRows(enc, pad);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();

        if (footerH > 0) DrawFooter(dl, wp, ws, footerH, pad);
        PushMenuTheme();
        if (ImGui.BeginPopup("##meterpulls"))
        {
            DrawPullList();
            ImGui.EndPopup();
        }
        PopMenuTheme();
        ContextMenu();
    }

    // ---- footer bar --------------------------------------------------------

    private bool _applySize;
    private float _overheadY = 80f;
    private float _rowStride = 27f;
    private int _renameTab = -1;
    private string _renameTabBuf = "";
    private bool _tabMenuOpen;
    private string _menuPlayer = "";
    private MeterEncounter? _shown;

    private void DrawFooter(ImDrawListPtr dl, Vector2 wp, Vector2 ws, float h, float pad)
    {
        var m = _plugin.Meter;
        var top = ws.Y - h;
        dl.AddLine(wp + new Vector2(pad, top), wp + new Vector2(ws.X - pad, top), 0x1EFFFFFF);

        var chipH = h - 7f;
        var cy = top + 4f;
        var x = pad - 3f;

        if (C.MeterHealingTab)
        {
            TabChip(dl, ref x, cy, chipH, C.MeterTabNameDamage.Length > 0 ? C.MeterTabNameDamage : "DPS", 0);
            TabChip(dl, ref x, cy, chipH, C.MeterTabNameHealing.Length > 0 ? C.MeterTabNameHealing : "HPS", 1);
            if (C.MeterButtons) x += 8f;
        }

        if (C.MeterButtons)
        {
            if (IconChip(dl, ref x, cy, chipH, FontAwesomeIcon.List, "Pulls"))
                ImGui.OpenPopup("##meterpulls");
            if (IconChip(dl, ref x, cy, chipH, m.Paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause,
                    m.Paused ? "Resume" : "Pause", accent: m.Paused))
                m.Paused = !m.Paused;
            if (IconChip(dl, ref x, cy, chipH, FontAwesomeIcon.Undo, "Reset"))
            {
                m.ResetEncounter();
                _histIdx = -1;
            }
        }

        // The right end fills in from the edge back towards the chips.
        var ty = cy + (chipH - ImGui.GetTextLineHeight()) * 0.5f;
        var rx = ws.X - pad;

        // Quiet unless there is something to say: paused, or viewing a past pull.
        var label = m.Paused ? "paused" : _histIdx >= 0 ? $"pull -{_histIdx + 1}" : "";
        if (label.Length > 0)
        {
            rx -= ImGui.CalcTextSize(label).X;
            BText(dl, wp + new Vector2(rx, ty), m.Paused ? C.MeterAccentColor : C.MeterSubColor, label);
            rx -= 12f;
        }

        // A gap the chips keep, so the two never read as one run of text.
        if (C.MeterFooterDeaths) DrawDeathTotal(dl, wp, x + 10f, rx, ty, chipH);

        TabRenamePopup();
    }

    // The pull's death count, shortened and then dropped as the room runs out.
    private void DrawDeathTotal(ImDrawListPtr dl, Vector2 wp, float leftEdge, float rx, float ty, float chipH)
    {
        if (_shown is not { } enc) return;
        var deaths = enc.TotalDeaths;

        var text = $"Total Deaths: {deaths}";
        var tw = ImGui.CalcTextSize(text).X;
        if (rx - tw < leftEdge)
        {
            text = $"Deaths: {deaths}";
            tw = ImGui.CalcTextSize(text).X;
            if (rx - tw < leftEdge) return;
        }

        rx -= tw;
        BText(dl, wp + new Vector2(rx, ty), deaths > 0 ? BadTint : C.MeterSubColor, text);
        if (deaths == 0) return;

        // Hovering it names them, which is the part a bare count leaves out.
        ImGui.SetCursorPos(new Vector2(rx, ty - 2f));
        ImGui.InvisibleButton("##deathtotal", new Vector2(tw, chipH));
        if (!ImGui.IsItemHovered()) return;
        var list = Deaths(enc, "");
        if (list.Count == 0) return;

        PushMenuTheme();
        ImGui.BeginTooltip();
        ImGui.TextUnformatted("Who died");
        ImGui.Separator();
        var shown = Math.Min(TooltipDeaths, list.Count);
        for (var i = 0; i < shown; i++)
        {
            var d = list[i];
            ImGui.TextColored(Theme.V(Theme.Muted),
                $"{(int)d.At / 60}:{(int)d.At % 60:00}   {d.Name}" +
                (d.Killer.Length > 0 ? $"   {d.Killer}" : ""));
        }
        if (list.Count > shown)
            ImGui.TextColored(Theme.V(Theme.Muted), $"+{list.Count - shown} more");
        ImGui.EndTooltip();
        PopMenuTheme();
    }

    private const int TooltipDeaths = 8;

    // A footer icon in a uniform chip: hover wash, centered glyph.
    private bool IconChip(ImDrawListPtr dl, ref float x, float cy, float h, FontAwesomeIcon icon, string tip,
        bool accent = false)
    {
        var w = h + 9f;
        ImGui.SetCursorPos(new Vector2(x, cy));
        var clicked = ImGui.InvisibleButton($"##fc{tip}", new Vector2(w, h));
        var hovered = ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        if (hovered) dl.AddRectFilled(min, min + new Vector2(w, h), 0x22FFFFFF, 4f);
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var g = icon.ToIconString();
            var gs = ImGui.CalcTextSize(g);
            dl.AddText(min + new Vector2((w - gs.X) * 0.5f, (h - gs.Y) * 0.5f),
                accent ? C.MeterAccentColor : hovered ? C.MeterTextColor : C.MeterSubColor, g);
        }
        if (hovered)
        {
            PushMenuTheme();
            ImGui.SetTooltip(tip);
            PopMenuTheme();
        }
        x += w + 2f;
        return clicked;
    }

    // A mode tab: accent pill while active, right-click to rename.
    private void TabChip(ImDrawListPtr dl, ref float x, float cy, float h, string label, int mode)
    {
        var w = ImGui.CalcTextSize(label).X + 16f;
        ImGui.SetCursorPos(new Vector2(x, cy));
        var clicked = ImGui.InvisibleButton($"##tab{mode}", new Vector2(w, h));
        var hovered = ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        var max = min + new Vector2(w, h);
        var active = C.MeterMode == mode;
        if (active)
        {
            dl.AddRectFilled(min, max, (C.MeterAccentColor & 0x00FFFFFF) | 0x30000000, 4f);
            dl.AddLine(new Vector2(min.X + 3, max.Y - 1), new Vector2(max.X - 3, max.Y - 1), C.MeterAccentColor, 2f);
        }
        else if (hovered)
            dl.AddRectFilled(min, max, 0x1CFFFFFF, 4f);
        BText(dl, new Vector2(min.X + 8f, min.Y + (h - ImGui.GetTextLineHeight()) * 0.5f),
            active ? C.MeterTextColor : C.MeterSubColor, label);
        if (clicked && !active) { C.MeterMode = mode; C.SaveSettings(); }
        if (hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            _renameTab = mode;
            _renameTabBuf = label;
            _tabMenuOpen = true;
            ImGui.OpenPopup("##tabrename");
        }
        x += w + 2f;
    }

    private void TabRenamePopup()
    {
        PushMenuTheme();
        if (!ImGui.BeginPopup("##tabrename")) { _tabMenuOpen = false; PopMenuTheme(); return; }
        if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
        ImGui.SetNextItemWidth(140f);
        var enter = ImGui.InputText("##tabname", ref _renameTabBuf, 24, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine(0, 4);
        if (ImGui.SmallButton("Save") || enter)
        {
            var name = _renameTabBuf.Trim();
            if (name.Length > 0)
            {
                if (_renameTab == 0) C.MeterTabNameDamage = name;
                else C.MeterTabNameHealing = name;
                C.SaveSettings();
            }
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
        PopMenuTheme();
    }

    private void DrawPullList()
    {
        var m = _plugin.Meter;

        // Sized to the widest entry, so clock, pull and time line up as columns.
        var durW = ImGui.CalcTextSize(Now).X;
        var bodyW = ImGui.CalcTextSize("Current").X;
        var whenW = 0f;
        foreach (var h in m.History)
        {
            durW = MathF.Max(durW, ImGui.CalcTextSize(h.Duration).X);
            whenW = MathF.Max(whenW, ImGui.CalcTextSize(Ago(h.When)).X);
            bodyW = MathF.Max(bodyW, ImGui.CalcTextSize(h.Title + Dot + Outcome(h)).X);
        }
        bodyW = MathF.Min(bodyW, 250f);
        var total = durW + 10f + bodyW + 18f + whenW;

        if (PullRow("cur", _histIdx < 0, total, durW, Now, "Current", "", 0u, "")) _histIdx = -1;
        if (m.History.Count == 0)
        {
            ImGui.TextDisabled("no past pulls yet");
            return;
        }
        ImGui.Separator();

        for (var i = 0; i < m.History.Count; i++)
        {
            var h = m.History[i];
            // The boss name gives way first; how far the pull got is worth more.
            var outcome = Outcome(h);
            var ow = outcome.Length > 0 ? ImGui.CalcTextSize(Dot + outcome).X : 0f;
            // The time is what tells two pulls of the same boss apart.
            if (PullRow($"h{i}", _histIdx == i, total, durW, h.Duration,
                    Clip(h.Title, bodyW - ow), outcome, OutcomeTint(h), Ago(h.When)))
                _histIdx = i;
        }
    }

    // One line of the pull list, drawn by hand so only part of it takes a color.
    private bool PullRow(string id, bool selected, float total, float durW,
        string clock, string title, string outcome, uint tint, string when)
    {
        var picked = ImGui.Selectable($"##pull{id}", selected, ImGuiSelectableFlags.None,
            new Vector2(total, ImGui.GetTextLineHeight()));
        var p = ImGui.GetItemRectMin();
        var dl = ImGui.GetWindowDrawList();

        dl.AddText(p, C.MeterSubColor, clock);
        var x = p.X + durW + 10f;
        dl.AddText(new Vector2(x, p.Y), C.MeterTextColor, title);
        if (outcome.Length > 0)
        {
            x += ImGui.CalcTextSize(title).X;
            dl.AddText(new Vector2(x, p.Y), C.MeterSubColor, Dot);
            dl.AddText(new Vector2(x + ImGui.CalcTextSize(Dot).X, p.Y), tint, outcome);
        }
        if (when.Length > 0)
            dl.AddText(new Vector2(p.X + total - ImGui.CalcTextSize(when).X, p.Y), C.MeterSubColor, when);
        return picked;
    }

    private const string Dot = " · ";
    private const string Now = "now";

    // How a finished pull reads, saying nothing where it cannot vouch for one.
    public static string Outcome(MeterEncounter enc) => OutcomeText(enc.Ended, enc.BossLeft);

    public static string OutcomeText(PullEnd ended, float bossLeft)
    {
        if (ended == PullEnd.Kill) return "kill";
        if (ended != PullEnd.Wipe) return "";
        // How far the pull got, when there was a raid-sized enemy to measure.
        if (bossLeft <= 0f) return "wiped";
        var pct = bossLeft * 100f;
        // A sliver of a huge health bar would otherwise round away to nothing.
        return pct < 0.1f ? "wiped at <0.1%" : $"wiped at {pct:0.#}%";
    }

    // This close to the end was nearly a kill, not a reset at seventy percent.
    public const float NearMiss = 0.05f;

    private uint OutcomeTint(MeterEncounter enc)
        => enc.Ended == PullEnd.Kill ? GoodTint
            : enc.BossLeft > 0f && enc.BossLeft <= NearMiss ? WarnTint
            : BadTint;

    private static string Ago(DateTime when)
    {
        var mins = (DateTime.Now - when).TotalMinutes;
        if (mins < 1) return "just now";
        if (mins < 60) return $"{(int)mins}m ago";
        return $"{(int)(mins / 60)}h ago";
    }

    // Refilled every frame rather than reallocated; nothing keeps a reference.
    private readonly MeterEncounter _mix = new();

    private static double L(double a, double b, float t) => a + (b - a) * t;

    // Live values glide from the previous parser tick to the newest one.
    private MeterEncounter Smoothed(MeterEncounter enc)
    {
        var m = _plugin.Meter;
        if (m.Paused || enc != m.Current || !enc.Active || m.Previous is not { } prev) return enc;
        var t = (float)((DateTime.UtcNow - m.CurrentAt).TotalSeconds / m.LerpSpan);
        if (t >= 1f) return enc;
        t = MathF.Max(0f, t);

        var mix = _mix;
        mix.Title = enc.Title;
        mix.Duration = enc.Duration;
        mix.Seconds = enc.Seconds;
        mix.Active = enc.Active;
        mix.When = enc.When;
        mix.TotalDeaths = enc.TotalDeaths;
        mix.TotalDps = L(prev.TotalDps, enc.TotalDps, t);
        mix.TotalDamage = L(prev.TotalDamage, enc.TotalDamage, t);
        mix.TotalHps = L(prev.TotalHps, enc.TotalHps, t);
        mix.TotalTaken = L(prev.TotalTaken, enc.TotalTaken, t);
        mix.RaidRDps = L(prev.RaidRDps, enc.RaidRDps, t);

        while (mix.Rows.Count < enc.Rows.Count) mix.Rows.Add(new MeterCombatant());
        if (mix.Rows.Count > enc.Rows.Count)
            mix.Rows.RemoveRange(enc.Rows.Count, mix.Rows.Count - enc.Rows.Count);

        for (var i = 0; i < enc.Rows.Count; i++)
        {
            var r = enc.Rows[i];
            var d = mix.Rows[i];
            MeterCombatant? p = null;
            foreach (var c in prev.Rows)
                if (c.Name == r.Name) { p = c; break; }
            p ??= r;
            d.Name = r.Name;
            d.Display = r.Display;
            d.Job = r.Job;
            d.LimitBreak = r.LimitBreak;
            d.DamagePct = r.DamagePct;
            d.HealedPct = r.HealedPct;
            d.MaxHit = r.MaxHit;
            d.Deaths = r.Deaths;
            d.Dps = L(p.Dps, r.Dps, t);
            d.RDps = L(p.RDps, r.RDps, t);
            d.Damage = L(p.Damage, r.Damage, t);
            d.CritPct = L(p.CritPct, r.CritPct, t);
            d.DirectHitPct = L(p.DirectHitPct, r.DirectHitPct, t);
            d.ADps = L(p.ADps, r.ADps, t);
            d.Hps = L(p.Hps, r.Hps, t);
            d.Healed = L(p.Healed, r.Healed, t);
            d.Shielded = L(p.Shielded, r.Shielded, t);
            d.OverhealPct = L(p.OverhealPct, r.OverhealPct, t);
            d.Taken = L(p.Taken, r.Taken, t);
        }
        return mix;
    }

    // The newest values, kept beside the held ones so the bars can keep moving.
    private MeterEncounter? _live;
    private MeterEncounter? _held;
    private DateTime _heldAt;

    // Digits that move every frame cannot be read, so a live pull holds a set,
    // order included.
    private MeterEncounter? Held(MeterEncounter? enc)
    {
        if (enc is not { Active: true } || _plugin.Meter.Paused)
        {
            _held = null;
            return enc;
        }
        var now = DateTime.UtcNow;
        // A pull starting over or somebody joining shows at once.
        if (_held == null || _held.Rows.Count != enc.Rows.Count || enc.Seconds < _held.Seconds
            || Meter.DueToRefresh((now - _heldAt).TotalSeconds, C.MeterRefreshSeconds))
        {
            _held = enc;
            _heldAt = now;
        }
        return _held;
    }

    // ---- breakdown cache ---------------------------------------------------

    // A sorted breakdown is held per open list rather than rebuilt every frame.
    private sealed class BreakdownSlot
    {
        public object? Enc;
        public string Player = "";
        public int Kind = -1;
        public DateTime At;
        public List<AbilityStat>? List;
    }

    private readonly BreakdownSlot[] _breakdowns =
    {
        new(), new(), new(), new(),
    };

    private List<AbilityStat> Breakdown(MeterEncounter enc, string player, int kind)
    {
        BreakdownSlot? free = null;
        foreach (var s in _breakdowns)
        {
            if (s.List != null && s.Kind == kind && ReferenceEquals(s.Enc, enc) && s.At == _heldAt
                && string.Equals(s.Player, player, StringComparison.Ordinal))
                return s.List;
            if (free == null || (s.List == null && free.List != null)) free = s;
        }
        var slot = free!;
        slot.Enc = enc;
        slot.Player = player;
        slot.Kind = kind;
        slot.At = _heldAt;
        return slot.List = _plugin.Meter.Breakdown(enc, player, kind);
    }

    // The same, for the death list a player's tab and the footer total read.
    private object? _deathsEnc;
    private string _deathsPlayer = "";
    private DateTime _deathsAt;
    private List<DeathRecord>? _deathsList;

    private List<DeathRecord> Deaths(MeterEncounter enc, string player)
    {
        if (_deathsList != null && ReferenceEquals(_deathsEnc, enc) && _deathsAt == _heldAt
            && string.Equals(_deathsPlayer, player, StringComparison.Ordinal))
            return _deathsList;
        _deathsEnc = enc;
        _deathsPlayer = player;
        _deathsAt = _heldAt;
        return _deathsList = player.Length > 0
            ? _plugin.Meter.Deaths(enc, player)
            : _plugin.Meter.Deaths(enc);
    }

    // The same row as it stands right now, for the length of its bar.
    private MeterCombatant LiveRow(MeterCombatant held)
    {
        if (_live is { } live && !ReferenceEquals(live, _held))
            foreach (var c in live.Rows)
                if (c.Name == held.Name) return c;
        return held;
    }

    // ---- header ------------------------------------------------------------

    private void DrawHeader(MeterEncounter enc, ImDrawListPtr dl, Vector2 wp, Vector2 ws, float pad, ref float y)
    {
        // A hidden header still shows while collapsed, or nothing is left to click.
        var slim = C.MeterHeaderStyle == 1 || C.MeterCollapsed;
        if (C.MeterHeaderStyle == 2 && !C.MeterCollapsed) { y += 2f; return; }
        var lineH = ImGui.GetTextLineHeight();
        var top = y;
        var chevW = CollapseButton(dl, wp, ws, pad, y, lineH);

        var title = enc.Title.Length > 0 ? enc.Title : "Fren Meter";
        if (_histIdx >= 0) title = $"{title} (history)";
        // Say a dead feed where the numbers are, not only in a log nobody reads.
        else if (_plugin.Meter.FeedStaleInReplay) title = $"{title} (replay: not parsed)";
        else if (_plugin.Meter.FeedStale) title = $"{title} (parser not sending)";
        var mode = C.MeterMode;

        // Damage mode makes raid rDPS the headline; other modes show their own.
        var main = mode switch
        {
            1 => $"{Num(enc.TotalHps)} HPS",
            2 => $"{Num(enc.TotalTaken)} taken",
            3 => $"{enc.TotalDeaths} deaths",
            _ => $"Raid {Num(enc.RaidRDps)} rDPS",
        };

        // The clock ticks by itself, so holding it would make it skip one.
        var clock = _live?.Duration is { Length: > 0 } d ? d : enc.Duration;
        var timeText = clock.Length > 0 ? clock : "0:00";
        if (slim)
        {
            // Slim: one line, clock + title left, headline right.
            var mainW = ImGui.CalcTextSize(main).X;
            var timeW = ImGui.CalcTextSize(timeText).X;
            BText(dl, wp + new Vector2(pad, y), C.MeterTimerColor, timeText);
            TitleWithPicker(dl, wp, pad + timeW + 8f, y,
                Clip(title, ws.X - pad * 2 - mainW - timeW - 34f - chevW));
            BText(dl, wp + new Vector2(ws.X - pad - chevW - mainW, y), C.MeterAccentColor, main);
            y += lineH + 5f;
        }
        else
        {
            var mainW = ImGui.CalcTextSize(main).X;
            TitleWithPicker(dl, wp, pad, y, Clip(title, ws.X - pad * 2 - mainW - 26f - chevW));
            BText(dl, wp + new Vector2(ws.X - pad - chevW - mainW, y), C.MeterAccentColor, main);
            y += lineH + 2f;

            var players = 0;
            foreach (var r in enc.Rows)
                if (r.Job.Length > 0)
                    players++;
            BText(dl, wp + new Vector2(pad, y), C.MeterTimerColor, timeText);
            BText(dl, wp + new Vector2(pad + ImGui.CalcTextSize(timeText).X, y),
                C.MeterSubColor, $"  ·  {players} in party");
            if (C.MeterShowRaidTotal && mode != 0)
            {
                var chip = $"Raid {Num(enc.RaidRDps)} rDPS";
                var w = ImGui.CalcTextSize(chip).X;
                BText(dl, wp + new Vector2(ws.X - pad - w, y), C.MeterSubColor, chip);
            }
            y += lineH + 5f;
        }

        if (!C.MeterCollapsed)
            dl.AddLine(wp + new Vector2(pad, y - 2f), wp + new Vector2(ws.X - pad, y - 2f), 0x22FFFFFF);

        // Double-click the header to cycle its style, but not over the chevron.
        if (!C.MeterClickThrough && !C.MeterCollapsed
            && ImGui.IsMouseHoveringRect(wp + new Vector2(0, top), wp + new Vector2(ws.X, y))
            && !ImGui.IsMouseHoveringRect(_chevronMin, _chevronMax)
            && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            C.MeterHeaderStyle = (C.MeterHeaderStyle + 1) % 3;
            C.SaveSettings();
        }
    }

    // The whole meter while it is rolled up: one line, and the chevron back out.
    private void DrawCollapsed(MeterEncounter? enc, ImDrawListPtr dl, Vector2 wp, Vector2 ws)
    {
        const float pad = 9f;
        var y = 6f;
        if (enc != null)
        {
            DrawHeader(enc, dl, wp, ws, pad, ref y);
        }
        else
        {
            var lineH = ImGui.GetTextLineHeight();
            var chevW = CollapseButton(dl, wp, ws, pad, y, lineH);
            BText(dl, wp + new Vector2(pad, y), C.MeterTextColor,
                Clip("Fren Meter", ws.X - pad * 2 - chevW));
            y += lineH + 5f;
        }
        _collapsedH = y + 3f;
    }

    // The roll-up chevron, returning the width the rest of the header must avoid.
    private float CollapseButton(ImDrawListPtr dl, Vector2 wp, Vector2 ws, float pad, float y, float lineH)
    {
        _chevronMin = _chevronMax = Vector2.Zero;
        if (C.MeterClickThrough) return 0f;

        float gw;
        var icon = C.MeterCollapsed ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronUp;
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var g = icon.ToIconString();
            gw = ImGui.CalcTextSize(g).X;
            var hot = ImGui.IsMouseHoveringRect(
                wp + new Vector2(ws.X - pad - gw - 4f, y - 2f),
                wp + new Vector2(ws.X - pad + 2f, y + lineH + 2f));
            dl.AddText(wp + new Vector2(ws.X - pad - gw, y + 1f),
                hot ? C.MeterTextColor : C.MeterSubColor, g);
        }

        _chevronMin = wp + new Vector2(ws.X - pad - gw - 4f, y - 2f);
        _chevronMax = wp + new Vector2(ws.X - pad + 2f, y + lineH + 2f);
        ImGui.SetCursorPos(new Vector2(ws.X - pad - gw - 4f, y - 2f));
        if (ImGui.InvisibleButton("##metercollapse", new Vector2(gw + 6f, lineH + 4f)))
            ToggleCollapsed();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(C.MeterCollapsed ? "Expand" : "Collapse");
        return gw + 8f;
    }

    private Vector2 _chevronMin;
    private Vector2 _chevronMax;
    private float _collapsedH = 34f;

    private void ToggleCollapsed()
    {
        C.MeterCollapsed = !C.MeterCollapsed;
        // Coming back out, put the stored size back on the window.
        if (!C.MeterCollapsed) _applySize = true;
        C.SaveSettings();
    }

    // The encounter name with a caret for past pulls, or the way out of a breakdown.
    private void TitleWithPicker(ImDrawListPtr dl, Vector2 wp, float x, float y, string shown)
    {
        var lineH = ImGui.GetTextLineHeight();
        if (_detailFor.Length > 0)
        {
            // A back arrow, then whose breakdown this is.
            float bw;
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            {
                var g = FontAwesomeIcon.CaretLeft.ToIconString();
                bw = ImGui.CalcTextSize(g).X;
                dl.AddText(wp + new Vector2(x, y + 1f), C.MeterSubColor, g);
            }
            BText(dl, wp + new Vector2(x + bw + 5f, y), C.MeterTitleColor, _detailFor);
            if (C.MeterClickThrough) return;
            ImGui.SetCursorPos(new Vector2(x, y - 1f));
            var hit = bw + ImGui.CalcTextSize(_detailFor).X + 9f;
            if (ImGui.InvisibleButton("##titleback", new Vector2(hit, lineH + 2f))) _detailFor = "";
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Back to the list");
            return;
        }

        BText(dl, wp + new Vector2(x, y), C.MeterTitleColor, shown);
        var tw = ImGui.CalcTextSize(shown).X;
        float cw;
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var g = FontAwesomeIcon.CaretDown.ToIconString();
            cw = ImGui.CalcTextSize(g).X;
            dl.AddText(wp + new Vector2(x + tw + 5f, y + 1f), C.MeterSubColor, g);
        }
        if (C.MeterClickThrough) return;
        ImGui.SetCursorPos(new Vector2(x, y - 1f));
        if (ImGui.InvisibleButton("##titlepick", new Vector2(tw + cw + 9f, lineH + 2f)))
            ImGui.OpenPopup("##meterpulls");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Pick a pull");
    }

    // ---- columns -----------------------------------------------------------

    private readonly record struct Col(string Key, string Label, string Sample, Func<MeterCombatant, string> Text);

    private static readonly Col[] AllCols =
    {
        new("rdps", "rDPS", "999.9k", c => Num(c.RDps)),
        new("dps", "DPS", "999.9k", c => Num(c.Dps)),
        new("adps", "aDPS", "999.9k", c => Num(c.ADps)),
        new("dmgpct", "D%", "99.9%", c => c.DamagePct.Length > 0 ? c.DamagePct : "-"),
        new("crit", "CRIT", "99.9%", c => $"{c.CritPct:0.#}%"),
        new("dh", "DH", "99.9%", c => $"{c.DirectHitPct:0.#}%"),
        new("maxhit", "MAX", "9.99M", c => Num(Meter.MaxHitValue(c.MaxHit))),
        new("hps", "HPS", "999.9k", c => Num(c.Hps)),
        new("healpct", "H%", "99.9%", c => c.HealedPct.Length > 0 ? c.HealedPct : "-"),
        new("healed", "HEALED", "9.99M", c => Num(c.Healed)),
        new("dshield", "D.SHIELD", "999.9k", c => Num(c.Shielded)),
        new("overheal", "OH%", "99.9%", c => $"{c.OverhealPct:0.#}%"),
        new("taken", "TAKEN", "999.9k", c => Num(c.Taken)),
        new("deaths", "D", "9", c => c.Deaths.ToString()),
    };

    // Every column there is, in menu order, which the settings page lists too.
    public static readonly string[] ColumnKeys = Keys();

    private static string[] Keys()
    {
        var keys = new string[AllCols.Length];
        for (var i = 0; i < AllCols.Length; i++) keys[i] = AllCols[i].Key;
        return keys;
    }

    private static Col? ColOf(string key)
    {
        foreach (var c in AllCols)
            if (c.Key == key)
                return c;
        return null;
    }

    // Which saved column list the active mode edits and shows.
    private List<string> ActiveColumnList()
        => C.MeterMode == 1 ? C.MeterHealColumns : C.MeterColumns;

    // The configured columns, with the active mode's own metric always present.
    private List<Col> DisplayColumns()
    {
        var keys = new List<string>(ActiveColumnList());
        var need = C.MeterMode switch { 1 => "hps", 2 => "taken", 3 => "deaths", _ => null };
        if (need != null && !keys.Contains(need)) keys.Insert(0, need);
        var cols = new List<Col>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in keys)
            if (seen.Add(k) && ColOf(k) is { } c)
                cols.Add(c);
        return cols;
    }

    // A column label mid-drag (its key), for reordering right on the meter.
    private string? _dragCol;

    private readonly List<(string Key, float X0, float X1)> _headerRects = new();

    private void DrawColumnHeader(ImDrawListPtr dl, Vector2 wp, Vector2 ws, float pad, ref float y)
    {
        var lineH = ImGui.GetTextLineHeight();
        var rowTop = y;
        var x = ws.X - pad;
        var rects = _headerRects;
        rects.Clear();
        foreach (var slot in _slots)
        {
            x -= slot.Width;
            var w = ImGui.CalcTextSize(slot.Col.Label).X;
            var dragging = _dragCol == slot.Col.Key;
            BText(dl, wp + new Vector2(x + slot.Width - w, y),
                dragging ? 0x55FFFFFFu : C.MeterSubColor, slot.Col.Label);
            rects.Add((slot.Col.Key, x, x + slot.Width));

            // Each label is a grab handle, except the pinned mode metric.
            if (!C.MeterClickThrough && ActiveColumnList().Contains(slot.Col.Key))
            {
                ImGui.SetCursorPos(new Vector2(x - ColGap * 0.5f, y - 2f));
                ImGui.InvisibleButton($"##colgrab_{slot.Col.Key}", new Vector2(slot.Width + ColGap, lineH + 4f));
                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 4f))
                    _dragCol = slot.Col.Key;
                else if (_dragCol == null && ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{ColumnLabel(slot.Col.Key)} · drag to reorder");
            }
            x -= ColGap;
        }
        y += lineH + 3f;

        if (_dragCol != null) HandleColumnDrag(wp, rects, rowTop, lineH);
    }

    private void HandleColumnDrag(Vector2 wp, List<(string Key, float X0, float X1)> rects, float rowTop, float lineH)
    {
        var keys = ActiveColumnList();
        if (_dragCol is not { } drag || !keys.Contains(drag)) { _dragCol = null; return; }

        // Where the drop would land: the slot under the mouse, and which side.
        var mouseX = ImGui.GetMousePos().X - wp.X;
        string? over = null;
        var after = false;
        foreach (var r in rects)
            if (mouseX >= r.X0 - ColGap * 0.5f && mouseX <= r.X1 + ColGap * 0.5f)
            {
                over = r.Key;
                after = mouseX > (r.X0 + r.X1) * 0.5f;
            }

        // Ghost label and insertion mark, drawn where the bars cannot paint over.
        var fg = ImGui.GetForegroundDrawList();
        var mouse = ImGui.GetMousePos();
        fg.AddText(new Vector2(mouse.X + 10f, mouse.Y - lineH * 0.5f), 0xDDFFFFFF, ColumnLabel(drag));
        if (over != null && over != drag)
            foreach (var r in rects)
                if (r.Key == over)
                {
                    var ix = wp.X + (after ? r.X1 + ColGap * 0.5f : r.X0 - ColGap * 0.5f);
                    fg.AddLine(new Vector2(ix, wp.Y + rowTop - 2f), new Vector2(ix, wp.Y + rowTop + lineH + 2f),
                        C.MeterAccentColor, 2f);
                }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left)) return;

        // Dropped: reorder the saved list, unless it landed nowhere useful.
        if (over != null && over != drag)
        {
            keys.Remove(drag);
            var idx = keys.IndexOf(over);
            idx = idx < 0 ? 0 : idx + (after ? 1 : 0);
            keys.Insert(Math.Clamp(idx, 0, keys.Count), drag);
            C.SaveSettings();
        }
        _dragCol = null;
    }

    private const float ColGap = 10f;

    private readonly record struct Slot(Col Col, float Width);

    // Right-to-left slots, each wide enough for its label and its biggest value.
    private List<Slot> Slots(List<Col> cols)
    {
        var slots = new List<Slot>(cols.Count);
        for (var i = cols.Count - 1; i >= 0; i--)
        {
            var w = MathF.Max(ImGui.CalcTextSize(cols[i].Label).X, ImGui.CalcTextSize(cols[i].Sample).X);
            slots.Add(new Slot(cols[i], w));
        }
        return slots;
    }

    // ---- cached layout -----------------------------------------------------

    // Sorting, measuring and formatting only change when the numbers settle.
    private readonly List<Col> _cols = new();
    private readonly List<Slot> _slots = new();
    private readonly List<float> _slotX = new();
    private readonly List<MeterCombatant> _sorted = new();
    private readonly List<RowEntry> _entries = new();
    private RowEntry? _lb;
    private int _entryCount;
    private int _colsKey;
    private bool _colsBuilt;
    private int _rowsKey;
    private object? _rowsEnc;
    private DateTime _rowsAt;
    private float _rowsWidth;
    private float _rankW;
    private float _nameX;
    private float _nameMax;

    // The same layout again for the healers' half of a split board.
    private readonly List<Col> _healCols = new();
    private readonly List<Slot> _healSlots = new();
    private readonly List<float> _healSlotX = new();
    private readonly List<MeterCombatant> _healSorted = new();
    private readonly List<RowEntry> _healEntries = new();
    private int _healCount;
    private float _healNameX;
    private float _healNameMax;

    // The split only exists on the damage board; every other mode is whole-window.
    private bool SplitOn => C.MeterSplitHealing && C.MeterMode == 0;

    public static bool IsHealer(MeterCombatant c)
        => Jobs.ByAbbreviation(c.Job) is { Role: JobRole.Healer };

    // One row as it will be drawn, values and widths already worked out.
    private sealed class RowEntry
    {
        public MeterCombatant Row = null!;
        public string Who = "";
        public string Id = "";
        public string RankText = "";
        public float RankTextWidth;
        public string Name = "";
        public uint Icon;
        public bool You;
        public int Rank;
        public readonly List<string> Values = new();
        public readonly List<float> Widths = new();
    }

    // Your own name, off the game object at most once a second.
    private string _you = "";
    private DateTime _youAt;

    private string You()
    {
        if (_youAt != DateTime.MinValue && (DateTime.UtcNow - _youAt).TotalSeconds < 1) return _you;
        _youAt = DateTime.UtcNow;
        return _you = Plugin.LocalPlayer?.Name.ToString() ?? "";
    }

    // Column widths move with the font, so the live size is part of the key.
    private int ColumnsKey()
    {
        var h = new HashCode();
        h.Add(C.MeterMode);
        h.Add(SplitOn);
        h.Add(ImGui.GetFontSize());
        foreach (var k in ActiveColumnList()) h.Add(k, StringComparer.Ordinal);
        if (SplitOn)
            foreach (var k in C.MeterHealColumns) h.Add(k, StringComparer.Ordinal);
        return h.ToHashCode();
    }

    private void EnsureColumns()
    {
        var key = ColumnsKey();
        if (_colsBuilt && key == _colsKey) return;
        _colsKey = key;
        _colsBuilt = true;
        _cols.Clear();
        _cols.AddRange(DisplayColumns());
        _slots.Clear();
        _slots.AddRange(Slots(_cols));
        _healCols.Clear();
        _healSlots.Clear();
        if (SplitOn)
        {
            var keys = new List<string>(C.MeterHealColumns);
            if (!keys.Contains("hps")) keys.Insert(0, "hps");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in keys)
                if (seen.Add(k) && ColOf(k) is { } c)
                    _healCols.Add(c);
            _healSlots.AddRange(Slots(_healCols));
        }
        _rowsEnc = null;
    }

    private int RowsKey()
    {
        var h = new HashCode();
        h.Add(_colsKey);
        h.Add(C.MeterShowRank);
        h.Add(C.MeterShowJobIcons);
        h.Add(C.MeterYou);
        h.Add(C.MeterNameStyle);
        h.Add(C.MeterMaxRows);
        h.Add(C.MeterLimitBreakRow);
        h.Add(C.MeterBarHeight);
        h.Add(You(), StringComparer.Ordinal);
        return h.ToHashCode();
    }

    private RowEntry Entry(int i)
    {
        while (_entries.Count <= i) _entries.Add(new RowEntry());
        return _entries[i];
    }

    private void EnsureRows(MeterEncounter enc, float w, float pad)
    {
        var key = RowsKey();
        if (ReferenceEquals(_rowsEnc, enc) && _rowsAt == _heldAt && key == _rowsKey
            && MathF.Abs(_rowsWidth - w) < 0.5f)
            return;
        _rowsEnc = enc;
        _rowsAt = _heldAt;
        _rowsKey = key;
        _rowsWidth = w;

        var lineH = ImGui.GetTextLineHeight();
        var rowH = MathF.Max(lineH + 4f, C.MeterBarHeight);
        var you = You();

        // Players only; the limit break belongs to the party, not to a person.
        _sorted.Clear();
        MeterCombatant? lbRow = null;
        foreach (var r in enc.Rows)
        {
            if (r.LimitBreak) lbRow = r;
            else _sorted.Add(r);
        }
        _sorted.Sort((a, b) => Metric(b).CompareTo(Metric(a)));

        _rankW = ImGui.CalcTextSize(_sorted.Count >= 10 ? "88." : "8.").X;
        _nameX = pad + 4f
                 + (C.MeterShowRank ? _rankW + 5f : 0f)
                 + (C.MeterShowJobIcons ? rowH : 0f);

        // Numeric slots right-to-left; the name gets whatever is left.
        _slotX.Clear();
        var rx = w - pad;
        foreach (var slot in _slots)
        {
            rx -= slot.Width;
            _slotX.Add(rx);
            rx -= ColGap;
        }
        _nameMax = rx - _nameX - 4f;

        // A row cap keeps the top of the list, plus your own row wherever it sits.
        var count = _sorted.Count;
        var keep = C.MeterMaxRows > 0 && count > C.MeterMaxRows ? C.MeterMaxRows : count;
        var me = -1;
        if (keep < count)
            for (var i = 0; i < count; i++)
                if (IsYou(_sorted[i], you)) { me = i; break; }

        _entryCount = 0;
        for (var i = 0; i < keep; i++)
        {
            var idx = keep < count && i == keep - 1 && me >= keep ? me : i;
            Fill(Entry(_entryCount++), _sorted[idx], idx + 1, you);
        }

        // The limit break only means anything on the damage board.
        _lb = null;
        if (lbRow != null && C.MeterLimitBreakRow && C.MeterMode == 0 && lbRow.Damage > 0)
        {
            _lbEntry ??= new RowEntry();
            Fill(_lbEntry, lbRow, 0, you);
            // The limit break the pull actually used, by id so it needs no name.
            _lbEntry.Icon = C.MeterShowJobIcons ? Icons.ByActionId(enc.LimitBreakAction) : 0u;
            _lb = _lbEntry;
        }

        // The healers' half of a split board, laid out on its own columns.
        _healCount = 0;
        if (SplitOn && _healCols.Count > 0)
        {
            _healSorted.Clear();
            foreach (var r in _sorted)
                if (IsHealer(r))
                    _healSorted.Add(r);
            _healSorted.Sort((a, b) => b.Hps.CompareTo(a.Hps));

            _healSlotX.Clear();
            var hx = w - pad;
            foreach (var slot in _healSlots)
            {
                hx -= slot.Width;
                _healSlotX.Add(hx);
                hx -= ColGap;
            }
            _healNameX = pad + 4f + (C.MeterShowJobIcons ? rowH : 0f);
            _healNameMax = hx - _healNameX - 4f;

            foreach (var r in _healSorted)
            {
                while (_healEntries.Count <= _healCount) _healEntries.Add(new RowEntry());
                Fill(_healEntries[_healCount], r, _healCount + 1, you, heal: true);
                _healCount++;
            }
        }
    }

    private RowEntry? _lbEntry;

    private void Fill(RowEntry e, MeterCombatant r, int rank, string you, bool heal = false)
    {
        var nameMax = heal ? _healNameMax : _nameMax;
        e.Row = r;
        e.Rank = rank;
        e.Who = r.Display.Length > 0 ? r.Display : r.Name;
        e.You = !r.LimitBreak && IsYou(r, you);
        e.Id = heal ? $"##hb{rank}" : r.LimitBreak ? "##barlb" : $"##bar{rank}";
        e.RankText = !heal && rank > 0 ? $"{rank}." : "";
        e.RankTextWidth = e.RankText.Length > 0 ? ImGui.CalcTextSize(e.RankText).X : 0f;
        e.Icon = !r.LimitBreak && C.MeterShowJobIcons && Jobs.ByAbbreviation(r.Job) is { } job
            ? 62100u + job.RowId
            : 0u;
        e.Name = nameMax > 12f
            ? Clip(r.LimitBreak ? e.Who : DisplayName(r, you), nameMax)
            : "";

        e.Values.Clear();
        e.Widths.Clear();
        foreach (var slot in heal ? _healSlots : _slots)
        {
            var text = r.LimitBreak && !LimitBreakColumn(slot.Col.Key) ? "" : slot.Col.Text(r);
            e.Values.Add(text);
            e.Widths.Add(text.Length > 0 ? ImGui.CalcTextSize(text).X : 0f);
        }
    }

    // Columns that say anything about a limit break; the rest stay blank.
    public static bool LimitBreakColumn(string key)
        => key is "rdps" or "dps" or "adps" or "dmgpct" or "maxhit";

    // ---- bars --------------------------------------------------------------

    private void DrawRows(MeterEncounter enc, float pad)
    {
        // Content width, not window width, so a scrollbar never overlaps the bars.
        EnsureRows(enc, MathF.Max(60f, ImGui.GetContentRegionAvail().X), pad);

        var lineH = ImGui.GetTextLineHeight();
        var rowH = MathF.Max(lineH + 4f, C.MeterBarHeight);
        _rowStride = rowH + C.MeterBarGap;

        // Bars scale against the newest numbers, which is what lets them grow.
        var max = 1.0;
        foreach (var r in _sorted) max = Math.Max(max, Metric(LiveRow(r)));

        for (var i = 0; i < _entryCount; i++)
            DrawRow(enc, _entries[i], pad, rowH, lineH, max);
        if (_lb != null) DrawLimitBreakRow(_lb, pad, rowH, lineH, max);
        if (_healCount > 0) DrawHealSection(enc, pad, rowH, lineH);
    }

    // The healers' HPS block under the damage rows, scaled to its own biggest bar.
    private void DrawHealSection(MeterEncounter enc, float pad, float rowH, float lineH)
    {
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        dl.AddLine(new Vector2(p.X + pad, p.Y + 2f), new Vector2(p.X + _rowsWidth - pad, p.Y + 2f), 0x22FFFFFF);
        var y = 6f;

        if (C.MeterColumnHeader)
        {
            for (var i = 0; i < _healSlots.Count && i < _healSlotX.Count; i++)
            {
                var lw = ImGui.CalcTextSize(_healSlots[i].Col.Label).X;
                BText(dl, new Vector2(p.X + _healSlotX[i] + _healSlots[i].Width - lw, p.Y + y),
                    C.MeterSubColor, _healSlots[i].Col.Label);
            }
            y += lineH + 3f;
        }
        ImGui.SetCursorScreenPos(new Vector2(p.X, p.Y + y));

        var max = 1.0;
        foreach (var r in _healSorted) max = Math.Max(max, LiveRow(r).Hps);
        for (var i = 0; i < _healCount; i++)
            DrawRow(enc, _healEntries[i], pad, rowH, lineH, max, heal: true);
    }

    private void DrawRow(MeterEncounter enc, RowEntry e, float pad, float rowH, float lineH, double max,
        bool heal = false)
    {
        var w = _rowsWidth;
        var r = e.Row;
        var nameX = heal ? _healNameX : _nameX;
        var p = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton(e.Id, new Vector2(w, rowH));
        // Both read off the bar before the icon and tooltip become the last item.
        var hovered = !C.MeterClickThrough && ImGui.IsItemHovered();
        var opened = hovered && ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var dl = ImGui.GetWindowDrawList();

        var jobColor = C.MeterJobColors && JobColors.TryGetValue(r.Job, out var jc) ? jc : C.MeterAccentColor;
        var rgb = jobColor & 0x00FFFFFF;

        dl.AddRectFilled(p + new Vector2(pad - 3f, 0), p + new Vector2(w - pad + 3f, rowH),
            hovered ? Brighten(C.MeterRowColor) : C.MeterRowColor, 4f);
        var metric = heal ? LiveRow(r).Hps : Metric(LiveRow(r));
        var fill = (float)(metric / max) * (w - pad * 2 + 6f);
        DrawBar(dl, p + new Vector2(pad - 3f, 0), fill, rowH, rgb, p.X + pad);
        if (C.MeterHighlightYou && e.You)
        {
            var hrgb = C.MeterHighlightColor & 0x00FFFFFF;
            var s = C.MeterHighlightStrength;
            var a = p + new Vector2(pad - 3f, 0);
            var b = p + new Vector2(w - pad + 3f, rowH);
            var style = C.MeterHighlightStyle;
            if (style is 0 or 1)
                dl.AddRectFilled(a, b, Fade(hrgb | 0x12000000, s), 4f);
            if (style is 0 or 2)
                dl.AddRect(a, b, Fade(hrgb | 0x8C000000, s), 4f);
            if (style == 3)
                dl.AddRectFilled(a - new Vector2(3f, 0), new Vector2(a.X, b.Y), Fade(hrgb | 0xF2000000, s));
        }

        var ty = p.Y + (rowH - lineH) * 0.5f;
        if (!heal && C.MeterShowRank)
            BText(dl, new Vector2(p.X + pad + 4f + _rankW - e.RankTextWidth, ty), C.MeterSubColor, e.RankText);
        if (e.Icon != 0)
        {
            var sz = rowH - 5f;
            ImGui.SetCursorScreenPos(new Vector2(p.X + nameX - sz - 5f, p.Y + 2.5f));
            Icons.Draw(e.Icon, new Vector2(sz, sz));
        }

        DrawValues(dl, e, p.X, ty, heal);
        if (e.Name.Length > 0)
            BText(dl, new Vector2(p.X + nameX, ty), e.You ? C.MeterYouColor : C.MeterTextColor, e.Name);

        if (hovered)
        {
            _rowUnderMouse = e.Who;
            PushMenuTheme();
            RowTooltip(enc, r);
            PopMenuTheme();
            // A healer row opens on their healing, not their damage.
            if (opened) OpenDetail(e.Who, heal ? 3 : -1);
        }
        ImGui.SetCursorScreenPos(new Vector2(p.X, p.Y + rowH + C.MeterBarGap));
    }

    private void DrawValues(ImDrawListPtr dl, RowEntry e, float left, float ty, bool heal = false)
    {
        var slots = heal ? _healSlots : _slots;
        var slotX = heal ? _healSlotX : _slotX;
        var cols = heal ? _healCols : _cols;
        for (var i = 0; i < slots.Count && i < e.Values.Count && i < slotX.Count; i++)
        {
            if (e.Values[i].Length == 0) continue;
            // The leading (leftmost-configured) column reads bright.
            var bright = cols.Count > 0 && slots[i].Col.Key == cols[0].Key;
            BText(dl, new Vector2(left + slotX[i] + slots[i].Width - e.Widths[i], ty),
                bright ? C.MeterTextColor : C.MeterSubColor, e.Values[i]);
        }
    }

    // The party's limit break, in a short row under everyone who has a job.
    private void DrawLimitBreakRow(RowEntry e, float pad, float rowH, float lineH, double max)
    {
        var w = _rowsWidth;
        var h = MathF.Max(lineH + 2f, rowH * 0.72f);
        var p = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton(e.Id, new Vector2(w, h));
        var hovered = !C.MeterClickThrough && ImGui.IsItemHovered();
        var opened = hovered && ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var dl = ImGui.GetWindowDrawList();
        var rgb = C.MeterAccentColor & 0x00FFFFFF;

        dl.AddRectFilled(p + new Vector2(pad - 3f, 0), p + new Vector2(w - pad + 3f, h),
            hovered ? Brighten(C.MeterRowColor) : C.MeterRowColor, 4f);
        var span = w - pad * 2 + 6f;
        var fill = MathF.Min(span, (float)(Metric(LiveRow(e.Row)) / max) * span);
        DrawBar(dl, p + new Vector2(pad - 3f, 0), fill, h, rgb, p.X + pad, 0.85f);

        var ty = p.Y + (h - lineH) * 0.5f;
        // Smaller than a job icon, but right-aligned into the same slot.
        if (e.Icon != 0)
        {
            var sz = h - 4f;
            ImGui.SetCursorScreenPos(new Vector2(p.X + _nameX - 5f - sz, p.Y + (h - sz) * 0.5f));
            Icons.Draw(e.Icon, new Vector2(sz, sz));
        }
        DrawValues(dl, e, p.X, ty);
        if (e.Name.Length > 0) BText(dl, new Vector2(p.X + _nameX, ty), C.MeterSubColor, e.Name);

        if (hovered)
        {
            PushMenuTheme();
            ImGui.SetTooltip($"{Num(e.Row.Damage)} limit break damage\nclick for the breakdown");
            PopMenuTheme();
            if (opened) OpenDetail(RdpsEngine.LimitBreakName, 0);
        }
        ImGui.SetCursorScreenPos(new Vector2(p.X, p.Y + h + C.MeterBarGap));
    }

    // One bar in whichever fill style is set, shared with the breakdown.
    private void DrawBar(ImDrawListPtr dl, Vector2 a, float fill, float rowH, uint rgb, float capEndX,
        float scale = 1f)
    {
        if (fill <= 2f) return;
        var b = new Vector2(a.X + fill, a.Y + rowH);
        var op = C.MeterBarOpacity * scale;
        // Solid bars fill with the color; the shine and cap still tell styles apart.
        var solid = C.MeterBarSolid;
        var wash = solid ? 0xFF000000u : 0x5C000000u;
        var lead = solid ? 0xFF000000u : 0x8C000000u;
        var trail = solid ? 0x99000000u : 0x26000000u;
        switch (C.MeterBarStyle)
        {
            case 1: // glass: solid fill with a shine across the top half
                dl.AddRectFilled(a, b, Fade(rgb | wash, op), 4f);
                dl.AddRectFilledMultiColor(a + new Vector2(1f, 1f), new Vector2(b.X, a.Y + rowH * 0.55f),
                    0x24FFFFFF, 0x24FFFFFF, 0x00FFFFFF, 0x00FFFFFF);
                break;
            case 2: // gradient: strong at the left, fading right
                dl.AddRectFilledMultiColor(a + new Vector2(0f, 1f), b - new Vector2(0f, 1f),
                    Fade(rgb | lead, op), Fade(rgb | trail, op),
                    Fade(rgb | trail, op), Fade(rgb | lead, op));
                break;
            case 3: // outline: a hollow bar with a bright edge
                dl.AddRectFilled(a, b, Fade(rgb | (solid ? 0xFF000000u : 0x1A000000u), op), 4f);
                dl.AddRect(a, b, Fade(rgb | 0xCC000000, op), 4f);
                break;
            case 4: // minimal: a rule under the row, no fill
                dl.AddRectFilled(new Vector2(a.X, b.Y - 2f), b, Fade(rgb | 0xD9000000, op));
                return;
            default: // flat
                dl.AddRectFilled(a, b, Fade(rgb | wash, op), 4f);
                break;
        }
        dl.AddRectFilled(a, new Vector2(capEndX, b.Y), Fade(rgb | 0xE6000000, op), 2f);
    }

    // ---- one player's breakdown --------------------------------------------

    private string _detailFor = "";
    private int _detailKind;          // 0 abilities, 1 targets, 2 taken
    private string _rowUnderMouse = "";
    private float _detailSeconds;     // the clock when the open breakdown was drawn

    private readonly record struct DetailTab(int Kind, string Label);

    // Kinds: 0 abilities, 1 targets, 2 taken, 3 heals, 4 healed, 5 received,
    // 6 contributed, 8 deaths.
    private static readonly DetailTab[] DamageTabs =
    {
        new(0, "Abilities"), new(1, "Targets"), new(2, "Taken"), new(6, "Contributed"), new(8, "Deaths"),
    };

    private static readonly DetailTab[] HealTabs =
    {
        new(3, "Heals"), new(4, "Healed"), new(5, "Received"), new(8, "Deaths"),
    };

    // A breakdown opened off a heal row keeps the healing tabs whatever the mode.
    private bool _detailHeal;

    private DetailTab[] Tabs()
        => C.MeterMode == 1 || (_detailHeal && _detailFor.Length > 0) ? HealTabs : DamageTabs;

    // Where a click on a row lands: whatever the list itself is about.
    private int DefaultKind() => C.MeterMode switch { 1 => 3, 3 => 8, _ => 0 };

    private void OpenDetail(string player, int kind = -1)
    {
        _detailFor = player;
        _detailKind = kind >= 0 ? kind : DefaultKind();
        _detailHeal = _detailKind is 3 or 4 or 5;
    }

    // Switching views changes which tabs exist, so land on one that does.
    private void ClampDetailKind()
    {
        foreach (var t in Tabs())
            if (t.Kind == _detailKind)
                return;
        _detailKind = DefaultKind();
    }

    private void DrawDetail(MeterEncounter enc, float pad)
    {
        ClampDetailKind();
        var w = MathF.Max(60f, ImGui.GetContentRegionAvail().X);
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();

        // Chips wrap onto another line rather than run off a narrow meter.
        var tabs = Tabs();
        var x = pad - 3f;
        var rowY = ImGui.GetCursorPosY();
        for (var i = 0; i < tabs.Length; i++)
        {
            var tw = ImGui.CalcTextSize(tabs[i].Label).X + 14f;
            if (x > pad - 3f && x + tw > w - pad) { x = pad - 3f; rowY += lineH + 9f; }
            ImGui.SetCursorPos(new Vector2(x, rowY));
            var clicked = ImGui.InvisibleButton($"##dt{i}", new Vector2(tw, lineH + 6f));
            var min = ImGui.GetItemRectMin();
            var on = _detailKind == tabs[i].Kind;
            if (on) dl.AddRectFilled(min, min + new Vector2(tw, lineH + 6f),
                (C.MeterAccentColor & 0x00FFFFFF) | 0x33000000, 4f);
            BText(dl, min + new Vector2(7f, 3f), on ? C.MeterTextColor : C.MeterSubColor, tabs[i].Label);
            if (clicked) _detailKind = tabs[i].Kind;
            x += tw + 4f;
        }
        ImGui.SetCursorPos(new Vector2(0, rowY + lineH + 12f));

        switch (_detailKind)
        {
            case 6: DrawCredit(enc, pad, w); break;
            case 8: DrawDeaths(enc, pad, w); break;
            default: DrawStatList(enc, pad, w); break;
        }
    }

    // Whatever the open tab lists, one bar a row.
    private void DrawStatList(MeterEncounter enc, float pad, float w)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        var healing = _detailKind is 3 or 4 or 5;
        var list = Breakdown(enc, _detailFor, _detailKind);
        if (list.Count == 0)
        {
            Empty(dl, w, _detailKind switch
            {
                2 => "nothing hit them",
                3 or 4 => "they healed nobody",
                5 => "nothing healed them",
                _ => "nothing recorded yet",
            });
            return;
        }

        var total = 0.0;
        var max = 1.0;
        foreach (var a in list)
        {
            total += a.Damage;
            max = Math.Max(max, healing ? a.Raw : a.Damage);
        }

        // Their own job color, so a breakdown still reads as that player's.
        var jobRgb = JobRgbFor(enc, _detailFor);

        var rowH = MathF.Max(lineH + 4f, C.MeterBarHeight);
        var span = w - pad * 2 + 6f;
        var i2 = 0;
        foreach (var a in list)
        {
            var p = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton($"##ab{i2++}", new Vector2(w, rowH));
            var hovered = !C.MeterClickThrough && ImGui.IsItemHovered();

            // One color per name, so a skill always reads the same shade.
            var rgb = C.MeterBreakdownColors ? TintFor(a.Name) : jobRgb;

            dl.AddRectFilled(p + new Vector2(pad - 3f, 0), p + new Vector2(w - pad + 3f, rowH),
                hovered ? Brighten(C.MeterRowColor) : C.MeterRowColor, 4f);
            // The whole cast faintly with what landed on top, so overheal reads.
            if (healing && a.Over > 0)
                DrawBar(dl, p + new Vector2(pad - 3f, 0), (float)(a.Raw / max) * span, rowH, rgb,
                    p.X + pad, 0.34f);
            DrawBar(dl, p + new Vector2(pad - 3f, 0), (float)(a.Damage / max) * span, rowH, rgb, p.X + pad);

            var ty = p.Y + (rowH - lineH) * 0.5f;
            var x2 = p.X + pad + 4f;
            // A row naming a person or an enemy has no icon worth guessing at.
            if (C.MeterBreakdownIcons && _detailKind is not (1 or 4 or 5))
            {
                var icon = a.IsStatus ? Icons.ByStatusId(a.Id) : Icons.ByActionId(a.Id);
                if (icon == 0) icon = Icons.ResolveFromText(a.Name);
                if (icon != 0)
                {
                    var sz = rowH - 5f;
                    ImGui.SetCursorScreenPos(new Vector2(x2, p.Y + 2.5f));
                    Icons.Draw(icon, new Vector2(sz, sz));
                    x2 += sz + 5f;
                }
            }

            var pct = total > 0 ? a.Damage / total * 100 : 0;
            var right = healing && a.Over > 0
                ? $"{Num(a.Damage)}  ({a.OverPct:0.#}% OH)"
                : $"{Num(a.Damage)}  ({pct:0.#}%)";
            var rw = ImGui.CalcTextSize(right).X;
            BText(dl, new Vector2(p.X + w - pad - rw, ty), C.MeterTextColor, right);
            var nameMax = p.X + w - pad - rw - x2 - 6f;
            if (nameMax > 12f)
                BText(dl, new Vector2(x2, ty), C.MeterSubColor, Clip(a.Name, nameMax));

            if (hovered)
            {
                PushMenuTheme();
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(a.Name);
                ImGui.Separator();
                if (healing)
                {
                    ImGui.TextUnformatted(
                        $"{Num(a.Damage)} healing over {a.Hits} cast{(a.Hits == 1 ? "" : "s")}");
                    ImGui.TextColored(Theme.V(Theme.Muted),
                        $"average {Num(a.Average)}   biggest {Num(a.Max)}");
                    if (a.Over > 0)
                        ImGui.TextColored(Theme.V(Theme.Muted),
                            $"overheal {Num(a.Over)}  ({a.OverPct:0.#}%)");
                }
                else
                {
                    ImGui.TextUnformatted($"{Num(a.Damage)} damage over {a.Hits} hit{(a.Hits == 1 ? "" : "s")}");
                    ImGui.TextColored(Theme.V(Theme.Muted),
                        $"average {Num(a.Average)}   biggest {Num(a.Max)}");
                    if (_detailKind != 1)
                        ImGui.TextColored(Theme.V(Theme.Muted),
                            $"crit {a.CritPct:0.#}%   direct {a.DhPct:0.#}%");
                }
                ImGui.EndTooltip();
                PopMenuTheme();
            }
            ImGui.SetCursorScreenPos(new Vector2(p.X, p.Y + rowH + C.MeterBarGap));
        }
    }

    // The two halves of this player's rDPS, given out and taken in.
    private void DrawCredit(MeterEncounter enc, float pad, float w)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        var given = Breakdown(enc, _detailFor, 6);
        var got = Breakdown(enc, _detailFor, 7);
        if (given.Count == 0 && got.Count == 0)
        {
            Empty(dl, w, "no buffs traded");
            return;
        }

        double gave = 0, took = 0, max = 1;
        foreach (var a in given) { gave += a.Damage; max = Math.Max(max, a.Damage); }
        foreach (var a in got) { took += a.Damage; max = Math.Max(max, a.Damage); }

        var net = gave - took;
        var head = $"gave {Num(gave)}   got {Num(took)}   net {(net < 0 ? "-" : "+")}{Num(Math.Abs(net))}";
        BText(dl, ImGui.GetCursorScreenPos() + new Vector2(pad, 0), C.MeterSubColor, Clip(head, w - pad * 2));
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + lineH + 7f);

        CreditRows(enc, "Given to", given, pad, w, max);
        CreditRows(enc, "Received from", got, pad, w, max);
    }

    private void CreditRows(MeterEncounter enc, string label, List<AbilityStat> rows,
        float pad, float w, double max)
    {
        if (rows.Count == 0) return;
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        BText(dl, ImGui.GetCursorScreenPos() + new Vector2(pad, 0), C.MeterSubColor, label);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + lineH + 4f);

        var rowH = MathF.Max(lineH + 4f, C.MeterBarHeight);
        var span = w - pad * 2 + 6f;
        var seconds = MathF.Max(1f, enc.Seconds);
        var i = 0;
        foreach (var a in rows)
        {
            // Keyed by whose tab it is, so the same name opens under one player only.
            var key = $"{_detailFor}|{label}|{a.Name}";
            var parts = a.Parts is { Count: > 0 } ? a.Parts : null;
            var open = parts != null && _creditOpen.Contains(key);

            var p = ImGui.GetCursorScreenPos();
            var clicked = ImGui.InvisibleButton($"##cr{label}{i++}", new Vector2(w, rowH));
            var hovered = !C.MeterClickThrough && ImGui.IsItemHovered();
            var rgb = JobRgbFor(enc, a.Name);

            dl.AddRectFilled(p + new Vector2(pad - 3f, 0), p + new Vector2(w - pad + 3f, rowH),
                hovered ? Brighten(C.MeterRowColor) : C.MeterRowColor, 4f);
            DrawBar(dl, p + new Vector2(pad - 3f, 0), (float)(a.Damage / max) * span, rowH, rgb, p.X + pad);

            var ty = p.Y + (rowH - lineH) * 0.5f;
            var right = Num(a.Damage);
            var rw = ImGui.CalcTextSize(right).X;
            BText(dl, new Vector2(p.X + w - pad - rw, ty), C.MeterTextColor, right);

            var nx = p.X + pad + 4f;
            // A caret where there are buffs to open, so the row says it can be.
            if (parts != null)
            {
                using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                {
                    var g = (open ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight).ToIconString();
                    dl.AddText(new Vector2(nx, ty + 1f), C.MeterSubColor, g);
                    nx += ImGui.CalcTextSize(g).X + 5f;
                }
            }
            var nameMax = p.X + w - pad - rw - nx - 6f;
            if (nameMax > 12f) BText(dl, new Vector2(nx, ty), C.MeterSubColor, Clip(a.Name, nameMax));

            if (hovered)
            {
                PushMenuTheme();
                ImGui.SetTooltip(parts == null
                    ? $"{Num(a.Damage / seconds)} rDPS over the pull"
                    : $"{Num(a.Damage / seconds)} rDPS over the pull\n{(open ? "click to close" : "click for the buffs behind it")}");
                PopMenuTheme();
            }
            if (clicked && parts != null && !_creditOpen.Remove(key)) _creditOpen.Add(key);

            ImGui.SetCursorScreenPos(new Vector2(p.X, p.Y + rowH + C.MeterBarGap));
            if (open) CreditParts(key, parts!, pad, w, span, max, seconds);
        }
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5f);
    }

    // Which player rows in the Contributed tab are opened up.
    private readonly HashSet<string> _creditOpen = new(StringComparer.Ordinal);

    // The buffs behind one share, indented and drawn against the same scale.
    private void CreditParts(string key, List<AbilityStat> parts, float pad, float w, float span,
        double max, float seconds)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        var rowH = lineH + 4f;
        const float indent = 15f;
        var i = 0;
        foreach (var b in parts)
        {
            var p = ImGui.GetCursorScreenPos();
            // Index first, or a key ending in a digit would share the next row's id.
            ImGui.InvisibleButton($"##cp{i++}_{key}", new Vector2(w, rowH));
            var hovered = !C.MeterClickThrough && ImGui.IsItemHovered();
            var rgb = TintFor(b.Name);

            var left = p + new Vector2(pad - 3f + indent, 0);
            dl.AddRectFilled(left, p + new Vector2(w - pad + 3f, rowH),
                hovered ? Brighten(C.MeterRowColor) : C.MeterRowColor, 3f);
            DrawBar(dl, left, (float)(b.Damage / max) * (span - indent), rowH, rgb, left.X + 5f, 0.8f);

            var ty = p.Y + (rowH - lineH) * 0.5f;
            var right = Num(b.Damage);
            var rw = ImGui.CalcTextSize(right).X;
            BText(dl, new Vector2(p.X + w - pad - rw, ty), C.MeterSubColor, right);
            var nx = left.X + 7f;
            var nameMax = p.X + w - pad - rw - nx - 6f;
            if (nameMax > 12f) BText(dl, new Vector2(nx, ty), C.MeterSubColor, Clip(b.Name, nameMax));

            if (hovered)
            {
                PushMenuTheme();
                ImGui.SetTooltip($"{b.Name}: {Num(b.Damage / seconds)} rDPS over the pull");
                PopMenuTheme();
            }
            ImGui.SetCursorScreenPos(new Vector2(p.X, p.Y + rowH + 2f));
        }
    }

    // Every death this player had, with the killing blow and the run-up to it.
    private void DrawDeaths(MeterEncounter enc, float pad, float w)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        var list = Deaths(enc, _detailFor);
        if (list.Count == 0)
        {
            Empty(dl, w, "they made it through");
            return;
        }

        var n = 0;
        foreach (var d in list)
        {
            var p = ImGui.GetCursorScreenPos();
            var blockH = (1 + d.Lead.Count) * (lineH + 3f) + 8f;
            ImGui.InvisibleButton($"##dth{n++}", new Vector2(w, blockH));
            dl.AddRectFilled(p + new Vector2(pad - 3f, 0), p + new Vector2(w - pad + 3f, blockH),
                C.MeterRowColor, 4f);
            dl.AddRectFilled(p + new Vector2(pad - 3f, 0), p + new Vector2(pad, blockH),
                (C.MeterHighlightColor & 0x00FFFFFF) | 0xCC000000);

            var y = p.Y + 4f;
            var clock = $"{(int)d.At / 60}:{(int)d.At % 60:00}";
            BText(dl, new Vector2(p.X + pad + 5f, y), C.MeterTimerColor, clock);
            var blow = d.KillingBlow > 0 ? Num(d.KillingBlow) : "";
            var bw = blow.Length > 0 ? ImGui.CalcTextSize(blow).X : 0f;
            if (blow.Length > 0) BText(dl, new Vector2(p.X + w - pad - bw, y), C.MeterTextColor, blow);
            var kx = p.X + pad + 5f + ImGui.CalcTextSize(clock).X + 8f;
            var kMax = p.X + w - pad - bw - kx - 6f;
            if (kMax > 12f)
                BText(dl, new Vector2(kx, y), C.MeterTextColor,
                    Clip(d.Killer.Length > 0 ? d.Killer : "unknown", kMax));
            y += lineH + 3f;

            foreach (var h in d.Lead)
            {
                var ago = $"-{Math.Max(0, d.Sec - h.Sec)}s";
                var val = (h.Heal ? "+" : "") + Num(h.Amount);
                var vw = ImGui.CalcTextSize(val).X;
                var color = h.Heal ? GoodTint : C.MeterSubColor;
                BText(dl, new Vector2(p.X + pad + 14f, y), C.MeterSubColor, ago);
                BText(dl, new Vector2(p.X + w - pad - vw, y), color, val);
                var nx = p.X + pad + 14f + ImGui.CalcTextSize(ago).X + 8f;
                var nMax = p.X + w - pad - vw - nx - 6f;
                if (nMax > 12f) BText(dl, new Vector2(nx, y), color, Clip(h.Name, nMax));
                y += lineH + 3f;
            }
            ImGui.SetCursorScreenPos(new Vector2(p.X, p.Y + blockH + C.MeterBarGap + 2f));
        }
    }

    private static readonly uint GoodTint = Rgb(0x6EE7B7);
    private static readonly uint WarnTint = Rgb(0xFBBF24);
    private static readonly uint BadTint = Rgb(0xF87171);

    private void Empty(ImDrawListPtr dl, float w, string msg)
        => BText(dl, new Vector2(ImGui.GetWindowPos().X + (w - ImGui.CalcTextSize(msg).X) * 0.5f,
            ImGui.GetCursorScreenPos().Y + 8f), C.MeterSubColor, msg);

    // A named player's job color, for rows that are about somebody else.
    private uint JobRgbFor(MeterEncounter enc, string name)
    {
        foreach (var r in enc.Rows)
            if (string.Equals(r.Display.Length > 0 ? r.Display : r.Name, name,
                    StringComparison.OrdinalIgnoreCase))
                return (C.MeterJobColors && JobColors.TryGetValue(r.Job, out var jc)
                    ? jc
                    : C.MeterAccentColor) & 0x00FFFFFF;
        return C.MeterAccentColor & 0x00FFFFFF;
    }

    private double Metric(MeterCombatant c) => C.MeterMode switch
    {
        1 => c.Hps,
        2 => c.Taken,
        3 => c.Deaths,
        _ => C.MeterColumns.Contains("rdps") ? c.RDps : c.Dps,
    };

    private static bool IsYou(MeterCombatant c, string you)
        => you.Length > 0 && string.Equals(c.Display, you, StringComparison.OrdinalIgnoreCase);

    private string DisplayName(MeterCombatant c, string you)
    {
        if (C.MeterYou && IsYou(c, you)) return "You";
        var name = c.Display;
        if (C.MeterNameStyle == 0 || !name.Contains(' ')) return name;
        var parts = name.Split(' ', 2);
        return C.MeterNameStyle == 1 ? parts[0] : $"{parts[0]} {parts[1][..1]}.";
    }

    private void RowTooltip(MeterEncounter enc, MeterCombatant r)
    {
        var who = r.Display.Length > 0 ? r.Display : r.Name;
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(who);
        ImGui.Separator();
        ImGui.TextUnformatted($"rDPS {Num(r.RDps)}   DPS {Num(r.Dps)}   damage {Num(r.Damage)}");
        ImGui.TextColored(Theme.V(Theme.Muted),
            $"crit {r.CritPct:0.#}%  direct {r.DirectHitPct:0.#}%  deaths {r.Deaths}");
        if (r.Hps > 0)
            ImGui.TextColored(Theme.V(Theme.Muted),
                r.Shielded > 0
                    ? $"HPS {Num(r.Hps)}  shielded {Num(r.Shielded)}  overheal {r.OverhealPct:0.#}%"
                    : $"HPS {Num(r.Hps)}  overheal {r.OverhealPct:0.#}%");
        if (r.MaxHit.Length > 0)
            ImGui.TextColored(Theme.V(Theme.Muted), $"biggest hit: {r.MaxHit.Replace('-', ' ')}");

        // The top of what they have been casting, straight off the log.
        var top = Breakdown(enc, who, C.MeterMode == 1 ? 3 : 0);
        if (top.Count > 0)
        {
            var total = 0.0;
            foreach (var a in top) total += a.Damage;
            ImGui.Separator();
            var shown = Math.Min(TooltipAbilities, top.Count);
            for (var i = 0; i < shown; i++)
            {
                var a = top[i];
                ImGui.TextColored(Theme.V(Theme.Muted),
                    $"{a.Name}   {Num(a.Damage)}  ({(total > 0 ? a.Damage / total * 100 : 0):0.#}%)");
            }
            if (top.Count > shown)
                ImGui.TextColored(Theme.V(Theme.Muted), $"+{top.Count - shown} more, click for all");
            else
                ImGui.TextColored(Theme.V(Theme.Muted), "click for the full breakdown");
        }
        ImGui.EndTooltip();
    }

    private const int TooltipAbilities = 5;

    // ---- right-click menu --------------------------------------------------

    private void ContextMenu()
    {
        if (C.MeterClickThrough) return;
        // Open anywhere over the meter, which the stock helper would miss.
        if (!_tabMenuOpen && ImGui.IsMouseReleased(ImGuiMouseButton.Right)
            && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
        {
            _menuPlayer = _rowUnderMouse;
            ImGui.OpenPopup("##metermenu");
        }

        PushMenuTheme();
        if (!ImGui.BeginPopup("##metermenu")) { PopMenuTheme(); return; }

        var m = _plugin.Meter;
        if (_menuPlayer.Length > 0)
        {
            if (ImGui.BeginMenu(_menuPlayer))
            {
                foreach (var t in Tabs())
                    if (ImGui.MenuItem(t.Label))
                        OpenDetail(_menuPlayer, t.Kind);
                ImGui.EndMenu();
            }
            ImGui.Separator();
        }
        else if (_detailFor.Length > 0)
        {
            if (ImGui.MenuItem("Back to the list")) _detailFor = "";
            ImGui.Separator();
        }
        if (ImGui.BeginMenu("View"))
        {
            var mode = C.MeterMode;
            if (ImGui.MenuItem("Damage", "", mode == 0)) { C.MeterMode = 0; C.SaveSettings(); }
            if (ImGui.MenuItem("Healing", "", mode == 1)) { C.MeterMode = 1; C.SaveSettings(); }
            if (ImGui.MenuItem("Damage taken", "", mode == 2)) { C.MeterMode = 2; C.SaveSettings(); }
            if (ImGui.MenuItem("Deaths", "", mode == 3)) { C.MeterMode = 3; C.SaveSettings(); }
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Past pulls"))
        {
            DrawPullList();
            ImGui.EndMenu();
        }
        if (ImGui.MenuItem(m.Paused ? "Resume" : "Pause")) m.Paused = !m.Paused;
        if (ImGui.MenuItem(C.MeterCollapsed ? "Expand" : "Collapse")) ToggleCollapsed();

        ImGui.Separator();
        if (ImGui.BeginMenu("Appearance"))
        {
            if (ImGui.BeginMenu("Columns"))
            {
                var list = ActiveColumnList();
                foreach (var col in AllCols)
                {
                    var on = list.Contains(col.Key);
                    if (ImGui.MenuItem(ColumnLabel(col.Key), "", on))
                    {
                        if (on) list.Remove(col.Key);
                        else list.Add(col.Key);
                        C.SaveSettings();
                    }
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Display"))
            {
                if (ImGui.MenuItem("Rank numbers", "", C.MeterShowRank)) { C.MeterShowRank = !C.MeterShowRank; C.SaveSettings(); }
                if (ImGui.MenuItem("Job icons", "", C.MeterShowJobIcons)) { C.MeterShowJobIcons = !C.MeterShowJobIcons; C.SaveSettings(); }
                if (ImGui.MenuItem("Column labels", "", C.MeterColumnHeader)) { C.MeterColumnHeader = !C.MeterColumnHeader; C.SaveSettings(); }
                if (ImGui.MenuItem("Raid total", "", C.MeterShowRaidTotal)) { C.MeterShowRaidTotal = !C.MeterShowRaidTotal; C.SaveSettings(); }
                if (ImGui.MenuItem("\"You\" instead of your name", "", C.MeterYou)) { C.MeterYou = !C.MeterYou; C.SaveSettings(); }
                if (ImGui.MenuItem("Highlight your row", "", C.MeterHighlightYou)) { C.MeterHighlightYou = !C.MeterHighlightYou; C.SaveSettings(); }
                if (ImGui.MenuItem("Buttons bar", "", C.MeterButtons)) { C.MeterButtons = !C.MeterButtons; C.SaveSettings(); }
                if (ImGui.MenuItem("Healing tab", "", C.MeterHealingTab)) { C.MeterHealingTab = !C.MeterHealingTab; C.SaveSettings(); }
                if (ImGui.MenuItem("Death count", "", C.MeterFooterDeaths))
                { C.MeterFooterDeaths = !C.MeterFooterDeaths; C.SaveSettings(); }
                if (ImGui.MenuItem("Limit break row", "", C.MeterLimitBreakRow))
                { C.MeterLimitBreakRow = !C.MeterLimitBreakRow; C.SaveSettings(); }
                if (ImGui.MenuItem("Split DPS/HPS", "", C.MeterSplitHealing))
                { C.MeterSplitHealing = !C.MeterSplitHealing; C.SaveSettings(); }
                if (ImGui.MenuItem("Drop shadow", "", C.MeterTextShadow)) { C.MeterTextShadow = !C.MeterTextShadow; C.SaveSettings(); }
                if (ImGui.MenuItem("Breakdown icons", "", C.MeterBreakdownIcons))
                { C.MeterBreakdownIcons = !C.MeterBreakdownIcons; C.SaveSettings(); }
                if (ImGui.MenuItem("Color each ability", "", C.MeterBreakdownColors))
                { C.MeterBreakdownColors = !C.MeterBreakdownColors; C.SaveSettings(); }
                if (ImGui.MenuItem("Always on screen", "", C.MeterAlwaysShow))
                { C.MeterAlwaysShow = !C.MeterAlwaysShow; C.SaveSettings(); }
                if (ImGui.MenuItem("Hide out of combat", "", C.MeterHideOutOfCombat))
                { C.MeterHideOutOfCombat = !C.MeterHideOutOfCombat; C.SaveSettings(); }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Bars"))
            {
                var bars = C.MeterBarStyle;
                if (ImGui.MenuItem("Flat", "", bars == 0)) { C.MeterBarStyle = 0; C.SaveSettings(); }
                if (ImGui.MenuItem("Glass", "", bars == 1)) { C.MeterBarStyle = 1; C.SaveSettings(); }
                if (ImGui.MenuItem("Gradient", "", bars == 2)) { C.MeterBarStyle = 2; C.SaveSettings(); }
                if (ImGui.MenuItem("Outline", "", bars == 3)) { C.MeterBarStyle = 3; C.SaveSettings(); }
                if (ImGui.MenuItem("Minimal", "", bars == 4)) { C.MeterBarStyle = 4; C.SaveSettings(); }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Your row"))
            {
                if (ImGui.MenuItem("Highlight", "", C.MeterHighlightYou))
                { C.MeterHighlightYou = !C.MeterHighlightYou; C.SaveSettings(); }
                ImGui.Separator();
                var hl = C.MeterHighlightStyle;
                if (ImGui.MenuItem("Wash + outline", "", hl == 0)) { C.MeterHighlightStyle = 0; C.SaveSettings(); }
                if (ImGui.MenuItem("Wash", "", hl == 1)) { C.MeterHighlightStyle = 1; C.SaveSettings(); }
                if (ImGui.MenuItem("Outline", "", hl == 2)) { C.MeterHighlightStyle = 2; C.SaveSettings(); }
                if (ImGui.MenuItem("Side stripe", "", hl == 3)) { C.MeterHighlightStyle = 3; C.SaveSettings(); }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Header"))
            {
                var style = C.MeterHeaderStyle;
                if (ImGui.MenuItem("Full", "", style == 0)) { C.MeterHeaderStyle = 0; C.SaveSettings(); }
                if (ImGui.MenuItem("Slim", "", style == 1)) { C.MeterHeaderStyle = 1; C.SaveSettings(); }
                if (ImGui.MenuItem("Hidden", "", style == 2)) { C.MeterHeaderStyle = 2; C.SaveSettings(); }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Theme"))
            {
                foreach (var t in Themes)
                    if (ImGui.MenuItem(t.Name))
                        ApplyTheme(C, t);
                ImGui.EndMenu();
            }
            if (C.MeterProfiles.Count > 0 && ImGui.BeginMenu("Profile"))
            {
                foreach (var kv in C.MeterProfiles)
                    if (ImGui.MenuItem(kv.Key, "", C.MeterProfileName == kv.Key))
                    {
                        if (MeterProfile.Import(C, kv.Value))
                        {
                            C.MeterProfileName = kv.Key;
                            C.SaveSettings();
                            RequestReposition();
                        }
                        break;
                    }
                ImGui.EndMenu();
            }
            ImGui.EndMenu();
        }
        if (ImGui.MenuItem("Lock position", "", C.MeterLocked)) { C.MeterLocked = !C.MeterLocked; C.SaveSettings(); }
        if (ImGui.MenuItem("Click-through", "", C.MeterClickThrough))
        {
            // This takes the right-click menu with it; the config page is the way back.
            C.MeterClickThrough = !C.MeterClickThrough;
            C.SaveSettings();
        }
        ImGui.Separator();
        if (ImGui.MenuItem("Copy summary")) CopySummary();
        if (ImGui.MenuItem("Clear data")) { m.ClearAll(); _histIdx = -1; }
        if (ImGui.MenuItem("Settings...")) _plugin.ConfigWindow.OpenMeterPage();

        ImGui.EndPopup();
        PopMenuTheme();
    }

    // The pull on screen as plain text, ready to paste anywhere.
    private void CopySummary()
    {
        if (_shown is not { Rows.Count: > 0 } enc) return;
        var mode = C.MeterMode;
        var sb = new System.Text.StringBuilder();
        sb.Append(enc.Title.Length > 0 ? enc.Title : "Encounter")
            .Append("  ").Append(enc.Duration.Length > 0 ? enc.Duration : "0:00")
            .Append("  ·  ")
            .Append(mode switch
            {
                1 => $"raid {Num(enc.TotalHps)} HPS",
                2 => $"raid {Num(enc.TotalTaken)} taken",
                3 => $"{enc.TotalDeaths} deaths",
                _ => $"raid {Num(enc.RaidRDps)} rDPS",
            });

        var rows = new List<MeterCombatant>(enc.Rows);
        rows.Sort((a, b) => Metric(b).CompareTo(Metric(a)));
        MeterCombatant? lb = null;
        var rank = 1;
        foreach (var r in rows)
        {
            if (r.LimitBreak) { lb = r; continue; }
            sb.Append('\n').Append(rank++).Append(". ")
                .Append(r.Display.Length > 0 ? r.Display : r.Name);
            if (r.Job.Length > 0) sb.Append(" (").Append(r.Job).Append(')');
            sb.Append("  ").Append(mode switch
            {
                1 => $"{Num(r.Hps)} HPS",
                2 => $"{Num(r.Taken)} taken",
                3 => $"{r.Deaths} death{(r.Deaths == 1 ? "" : "s")}",
                _ => $"{Num(r.RDps)} rDPS",
            });
            if (mode == 0 && r.DamagePct.Length > 0) sb.Append("  ").Append(r.DamagePct);
        }
        // The limit break is nobody's line, so it goes under the party.
        if (lb != null && mode == 0 && lb.Damage > 0)
        {
            sb.Append("\nLimit Break  ").Append(Num(lb.Dps)).Append(" DPS");
            if (lb.DamagePct.Length > 0) sb.Append("  ").Append(lb.DamagePct);
        }
        // A split board copies its healer half too.
        if (SplitOn)
            foreach (var r in rows)
            {
                if (!IsHealer(r)) continue;
                sb.Append('\n').Append(r.Display.Length > 0 ? r.Display : r.Name)
                    .Append(" (").Append(r.Job).Append(")  ").Append(Num(r.Hps)).Append(" HPS");
                if (r.Shielded > 0) sb.Append("  ").Append(Num(r.Shielded)).Append(" shielded");
                if (r.HealedPct.Length > 0) sb.Append("  ").Append(r.HealedPct);
            }
        ImGui.SetClipboardText(sb.ToString());
    }

    // ---- menu theme --------------------------------------------------------

    // The meter's own look carried into its popups, in place of stock ImGui.
    private void PushMenuTheme()
    {
        var accent = C.MeterAccentColor & 0x00FFFFFF;
        ImGui.PushStyleColor(ImGuiCol.PopupBg, (C.MeterBgColor & 0x00FFFFFF) | 0xF4000000);
        ImGui.PushStyleColor(ImGuiCol.Border, 0x3CFFFFFF);
        ImGui.PushStyleColor(ImGuiCol.Text, C.MeterTextColor);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, (C.MeterSubColor & 0x00FFFFFF) | 0x99000000);
        ImGui.PushStyleColor(ImGuiCol.Header, accent | 0x3A000000);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, accent | 0x55000000);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, accent | 0x6E000000);
        ImGui.PushStyleColor(ImGuiCol.Separator, 0x24FFFFFF);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, C.MeterAccentColor);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, 0x1FFFFFFF);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 9f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(11f, 9f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(9f, 6f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
    }

    private static void PopMenuTheme()
    {
        ImGui.PopStyleVar(5);
        ImGui.PopStyleColor(10);
    }

    // ---- persistence -------------------------------------------------------

    private void SaveIfMoved()
    {
        if (C.MeterLocked) return;
        if (OverlayChrome.MovedCenterFrac(C.MeterPosition) is { } frac) { C.MeterPosition = frac; _posDirty = true; }
        // While collapsed the height is ours, so only the width is remembered.
        var size = ImGui.GetWindowSize();
        if (C.MeterCollapsed)
        {
            if (MathF.Abs(size.X - C.MeterSize.X) > 1f)
            {
                C.MeterSize = new Vector2(size.X, C.MeterSize.Y);
                _sizeDirty = true;
            }
        }
        else if ((size - C.MeterSize).LengthSquared() > 1f) { C.MeterSize = size; _sizeDirty = true; }
        if ((_posDirty || _sizeDirty) && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (_sizeDirty && !C.MeterCollapsed && _rowStride > 4f)
            {
                var rows = MathF.Max(2f, MathF.Round((C.MeterSize.Y - _overheadY) / _rowStride));
                C.MeterSize = new Vector2(C.MeterSize.X, _overheadY + rows * _rowStride);
                _applySize = true;
            }
            C.SaveSettings();
            _posDirty = _sizeDirty = false;
        }
    }

    // ---- helpers -----------------------------------------------------------

    // Every piece of text on the meter, honoring the shadow toggle.
    private void BText(ImDrawListPtr dl, Vector2 pos, uint color, string text)
        => OverlayChrome.BoardText(dl, pos, color, text, C.MeterTextShadow);

    // A stable color per ability, so a skill keeps its shade between pulls.
    private static readonly Dictionary<string, uint> TintCache = new(StringComparer.Ordinal);

    public static uint TintFor(string name)
    {
        if (TintCache.TryGetValue(name, out var cached)) return cached;
        var h = 2166136261u;
        foreach (var ch in name) { h = (h ^ ch) * 16777619u; }
        // Spread the hues rather than letting the hash clump them.
        var hue = h % 360u / 360f;
        return TintCache[name] = Hsv(hue, 0.55f, 0.98f);
    }

    // Hue, saturation and value into the packed color the draw list wants.
    private static uint Hsv(float h, float s, float v)
    {
        var i = (int)MathF.Floor(h * 6f);
        var f = h * 6f - i;
        var p = v * (1f - s);
        var q = v * (1f - f * s);
        var t = v * (1f - (1f - f) * s);
        var (r, g, b) = (i % 6) switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
        return (uint)(b * 255f) << 16 | (uint)(g * 255f) << 8 | (uint)(r * 255f);
    }

    // A color's alpha scaled by a factor, clamped to a byte.
    private static uint Fade(uint abgr, float scale)
    {
        var a = (uint)Math.Clamp((abgr >> 24) * scale, 0f, 255f);
        return (a << 24) | (abgr & 0x00FFFFFF);
    }

    // The row color with a touch more alpha for hover.
    private static uint Brighten(uint abgr)
    {
        var a = Math.Min(255u, (abgr >> 24) + 0x20u);
        return (a << 24) | (abgr & 0x00FFFFFF);
    }

    public static string Num(double v) => v switch
    {
        >= 1e6 => $"{v / 1e6:0.00}M",
        >= 1000 => $"{v / 1000:0.0}k",
        _ => $"{v:0}",
    };

    // Binary search, so a long name costs a handful of measures and not one each.
    private static string Clip(string s, float maxW)
    {
        if (ImGui.CalcTextSize(s).X <= maxW) return s;
        int lo = 1, hi = s.Length;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (ImGui.CalcTextSize(s[..mid] + "…").X <= maxW) lo = mid;
            else hi = mid - 1;
        }
        return s[..lo] + "…";
    }

    // ---- themes ------------------------------------------------------------

    // Accent, Text and Sub are plain RGB; Bg and Rows carry their own alpha.
    public readonly record struct MeterTheme(
        string Name, uint Accent, uint Text, uint Sub, uint Bg, uint Rows, float Rounding, bool JobColors,
        int BarStyle);

    // 0xAARRGGBB, the way a color picker spells it, into the packed order.
    private static uint Argb(uint argb)
        => (argb & 0xFF000000u) | ((argb & 0xFF) << 16) | (argb & 0xFF00) | ((argb >> 16) & 0xFF);

    public static readonly MeterTheme[] Themes =
    {
        new("Fren Mits", Rgb(0x3B82F6), Rgb(0xFFFFFF), Rgb(0xFFFFFF), Argb(0xB8090A0D), Argb(0x17FFFFFF), 5f, true, 0),
        new("Dark Mode", Rgb(0x8AB4FF), Rgb(0xFFFFFF), Rgb(0xC8CDD8), Argb(0xE6000000), Argb(0x12FFFFFF), 6f, true, 0),
        new("Glass", Rgb(0x4FD1C5), Rgb(0xFFFFFF), Rgb(0xE6F0F2), Argb(0x590E1216), Argb(0x22FFFFFF), 10f, true, 1),
        new("Ember", Rgb(0xFF8A3C), Rgb(0xFFFFFF), Rgb(0xFFD9C0), Argb(0xCC140B06), Argb(0x14FF9A5C), 5f, true, 2),
        new("Jade", Rgb(0x34D399), Rgb(0xFFFFFF), Rgb(0xD2E8DC), Argb(0xCC071310), Argb(0x16FFFFFF), 5f, true, 2),
        new("Mono", Rgb(0x9DA9B8), Rgb(0xFFFFFF), Rgb(0xC4C4C4), Argb(0xD8101010), Argb(0x1AFFFFFF), 2f, false, 0),
        new("Midnight", Rgb(0x818CF8), Rgb(0xF2F3FF), Rgb(0xB9BEDC), Argb(0xE00B0D1A), Argb(0x18A5B4FC), 7f, true, 0),
        new("Crimson", Rgb(0xF04658), Rgb(0xFFFFFF), Rgb(0xE8C4C8), Argb(0xDB120608), Argb(0x18F04658), 4f, true, 2),
        new("Sakura", Rgb(0xF9A8D4), Rgb(0xFFF5FA), Rgb(0xE3C2D5), Argb(0xD41A0E17), Argb(0x1AF9A8D4), 9f, true, 1),
        new("Frost", Rgb(0x67E8F9), Rgb(0xF4FEFF), Rgb(0xC3DDE3), Argb(0xDB08161A), Argb(0x1667E8F9), 6f, true, 1),
        new("Sunset", Rgb(0xFBBF24), Rgb(0xFFFBF0), Rgb(0xE8D5AE), Argb(0xD41A1004), Argb(0x1AFBBF24), 8f, true, 2),
        new("Forest", Rgb(0x84CC16), Rgb(0xF7FFEE), Rgb(0xCBD8B8), Argb(0xD40C1206), Argb(0x1684CC16), 3f, true, 0),
        new("Vapor", Rgb(0xE879F9), Rgb(0xFDF2FF), Rgb(0xCDB8E0), Argb(0xE0140A1F), Argb(0x1CE879F9), 10f, true, 2),
        new("Neon", Rgb(0xCCFF00), Rgb(0xFFFFFF), Rgb(0xB6C48C), Argb(0xF0000000), Argb(0x14CCFF00), 0f, true, 3),
        new("Slate", Rgb(0x94A3B8), Rgb(0xF1F5F9), Rgb(0xB4C0CE), Argb(0xE00F141B), Argb(0x14FFFFFF), 3f, false, 4),
        new("Parchment", Rgb(0xB45309), Rgb(0x231A10), Rgb(0x5C4A36), Argb(0xEFF2EADA), Argb(0x1A6B5334), 6f, true, 0),
    };

    public static void ApplyTheme(Configuration c, MeterTheme t)
    {
        c.MeterAccentColor = t.Accent;
        c.MeterTextColor = t.Text;
        c.MeterSubColor = t.Sub;
        c.MeterTitleColor = t.Text;
        c.MeterTimerColor = t.Text;
        c.MeterYouColor = t.Accent;
        c.MeterHighlightColor = t.Accent;
        c.MeterBorderColor = (t.Accent & 0x00FFFFFF) | 0x2E000000;
        c.MeterBgColor = t.Bg;
        c.MeterRowColor = t.Rows;
        c.MeterRounding = t.Rounding;
        c.MeterJobColors = t.JobColors;
        c.MeterBarStyle = t.BarStyle;
        c.SaveSettings();
    }

    // Long-form column names, shared with the settings page.
    public static string ColumnLabel(string key) => key switch
    {
        "rdps" => "rDPS",
        "dps" => "DPS",
        "adps" => "Active DPS",
        "dmgpct" => "Damage %",
        "crit" => "Crit %",
        "dh" => "Direct hit %",
        "maxhit" => "Biggest hit",
        "hps" => "HPS",
        "healpct" => "Healing %",
        "healed" => "Healed total",
        "dshield" => "Damage shielded",
        "overheal" => "Overheal %",
        "taken" => "Damage taken",
        "deaths" => "Deaths",
        _ => key,
    };

    // The community job palette (ABGR).
    private static uint Rgb(uint rgb)
        => 0xFF000000u | ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | (rgb >> 16);

    public static readonly Dictionary<string, uint> JobColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PLD"] = Rgb(0xA8D2E6), ["WAR"] = Rgb(0xCF2621), ["DRK"] = Rgb(0xD126CC), ["GNB"] = Rgb(0x796D30),
        ["WHM"] = Rgb(0xFFF0DC), ["SCH"] = Rgb(0x8657FF), ["AST"] = Rgb(0xFFE74A), ["SGE"] = Rgb(0x80A0F0),
        ["MNK"] = Rgb(0xD69C00), ["DRG"] = Rgb(0x4164CD), ["NIN"] = Rgb(0xAF1964), ["SAM"] = Rgb(0xE46D04),
        ["RPR"] = Rgb(0x965A90), ["VPR"] = Rgb(0x108210),
        ["BRD"] = Rgb(0x91BA5E), ["MCH"] = Rgb(0x6EE1D6), ["DNC"] = Rgb(0xE2B0AF),
        ["BLM"] = Rgb(0xA579D6), ["SMN"] = Rgb(0x2D9B78), ["RDM"] = Rgb(0xE87B7B), ["PCT"] = Rgb(0xFC92E1),
        ["BLU"] = Rgb(0x3366CC),
    };
}
