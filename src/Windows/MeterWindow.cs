using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// Fren Meter: the parser-fed damage meter overlay, everything mid-pull on its
// right-click menu.
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

        ImGui.SetNextWindowSize(C.MeterSize, C.MeterLocked || _applySize ? ImGuiCond.Always : ImGuiCond.Appearing);
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
        return C.TestMode || View() != null || !C.MeterLocked;
    }

    // The encounter on screen: a history pick, else live data, else the sample.
    private MeterEncounter? View()
    {
        var m = _plugin.Meter;
        // A fresh pull always pulls the meter back to live.
        if (m.Current is { Active: true }) _histIdx = -1;
        if (_histIdx >= 0 && _histIdx < m.History.Count) return m.History[_histIdx];
        _histIdx = -1;
        return m.Current ?? (C.TestMode ? m.Sample() : null);
    }

    public override void Draw()
    {
        SaveIfMoved();
        var enc = View();
        if (enc != null) enc = Smoothed(enc);

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

        if (enc == null)
        {
            var titleW = ImGui.CalcTextSize("Fren Meter").X;
            var status = _plugin.Meter.StatusText;
            var statusW = ImGui.CalcTextSize(status).X;
            var midY = ws.Y * 0.5f - ImGui.GetTextLineHeight();
            BText(dl, wp + new Vector2((ws.X - titleW) * 0.5f, midY),
                C.MeterTextColor, "Fren Meter");
            BText(dl, wp + new Vector2((ws.X - statusW) * 0.5f, midY + ImGui.GetTextLineHeight() + 3f),
                C.MeterSubColor, status);
            ContextMenu();
            return;
        }

        var pad = 9f;
        var y = 6f;
        DrawHeader(enc, dl, wp, ws, pad, ref y);

        var cols = DisplayColumns();
        if (C.MeterColumnHeader && C.MeterHeaderStyle != 2)
            DrawColumnHeader(cols, dl, wp, ws, pad, ref y);

        // Bars scroll in their own region when the pull outgrows the window.
        // Click-through must reach into it too, or the mouse snags on the bars.
        var footerH = (C.MeterButtons || C.MeterHealingTab) && !C.MeterClickThrough
            ? MathF.Ceiling(ImGui.GetTextLineHeight()) + 10f
            : 0f;
        _overheadY = y + footerH + 4f;
        ImGui.SetCursorPos(new Vector2(0, y));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0u);
        if (ImGui.BeginChild("##meterrows", new Vector2(ws.X, ws.Y - y - footerH - 4f), false,
                ImGuiWindowFlags.NoScrollbar
                | (C.MeterClickThrough ? ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoNav : ImGuiWindowFlags.None)))
            DrawRows(enc, cols, pad);
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

        // Quiet unless there is something to say: paused, or viewing a past pull.
        var label = m.Paused ? "paused" : _histIdx >= 0 ? $"pull -{_histIdx + 1}" : "";
        if (label.Length > 0)
        {
            var lw = ImGui.CalcTextSize(label).X;
            BText(dl,
                wp + new Vector2(ws.X - pad - lw, cy + (chipH - ImGui.GetTextLineHeight()) * 0.5f),
                m.Paused ? C.MeterAccentColor : C.MeterSubColor, label);
        }

        TabRenamePopup();
    }

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
        if (ImGui.MenuItem("Current", "", _histIdx < 0)) _histIdx = -1;
        for (var i = 0; i < m.History.Count; i++)
        {
            var h = m.History[i];
            if (ImGui.MenuItem($"{h.Duration}  {Clip(h.Title, 220f)}##hist{i}", "", _histIdx == i))
                _histIdx = i;
        }
    }

    // Live values glide from the previous parser tick to the newest one, so
    // the bars grow continuously instead of stepping once a second.
    private MeterEncounter Smoothed(MeterEncounter enc)
    {
        var m = _plugin.Meter;
        if (m.Paused || enc != m.Current || !enc.Active || m.Previous is not { } prev) return enc;
        var t = (float)((DateTime.UtcNow - m.CurrentAt).TotalSeconds / m.LerpSpan);
        if (t >= 1f) return enc;
        t = MathF.Max(0f, t);
        double L(double a, double b) => a + (b - a) * t;

        var mix = new MeterEncounter
        {
            Title = enc.Title, Duration = enc.Duration, Seconds = enc.Seconds, Active = enc.Active,
            TotalDps = L(prev.TotalDps, enc.TotalDps), TotalDamage = L(prev.TotalDamage, enc.TotalDamage),
            TotalHps = L(prev.TotalHps, enc.TotalHps), TotalTaken = L(prev.TotalTaken, enc.TotalTaken),
            TotalDeaths = enc.TotalDeaths, RaidRDps = L(prev.RaidRDps, enc.RaidRDps), When = enc.When,
        };
        foreach (var r in enc.Rows)
        {
            MeterCombatant? p = null;
            foreach (var c in prev.Rows)
                if (c.Name == r.Name) { p = c; break; }
            if (p == null) { mix.Rows.Add(r); continue; }
            mix.Rows.Add(new MeterCombatant
            {
                Name = r.Name, Display = r.Display, Job = r.Job,
                Dps = L(p.Dps, r.Dps), RDps = L(p.RDps, r.RDps), Damage = L(p.Damage, r.Damage),
                DamagePct = r.DamagePct, CritPct = L(p.CritPct, r.CritPct),
                DirectHitPct = L(p.DirectHitPct, r.DirectHitPct),
                Hps = L(p.Hps, r.Hps), Healed = L(p.Healed, r.Healed),
                OverhealPct = L(p.OverhealPct, r.OverhealPct),
                Taken = L(p.Taken, r.Taken), Deaths = r.Deaths, MaxHit = r.MaxHit,
            });
        }
        return mix;
    }

    // ---- header ------------------------------------------------------------

    private void DrawHeader(MeterEncounter enc, ImDrawListPtr dl, Vector2 wp, Vector2 ws, float pad, ref float y)
    {
        if (C.MeterHeaderStyle == 2) { y += 2f; return; }
        var lineH = ImGui.GetTextLineHeight();
        var top = y;

        var title = enc.Title.Length > 0 ? enc.Title : "Fren Meter";
        if (_histIdx >= 0) title = $"{title} (history)";
        var mode = C.MeterMode;

        // In damage mode the raid rDPS IS the headline; other modes show their
        // own total with the raid chip beside it.
        var main = mode switch
        {
            1 => $"{Num(enc.TotalHps)} HPS",
            2 => $"{Num(enc.TotalTaken)} taken",
            3 => $"{enc.TotalDeaths} deaths",
            _ => $"Raid {Num(enc.RaidRDps)} rDPS",
        };

        var timeText = enc.Duration.Length > 0 ? enc.Duration : "0:00";
        if (C.MeterHeaderStyle == 1)
        {
            // Slim: one line, clock + title left, headline right.
            var mainW = ImGui.CalcTextSize(main).X;
            var timeW = ImGui.CalcTextSize(timeText).X;
            BText(dl, wp + new Vector2(pad, y), C.MeterTimerColor, timeText);
            TitleWithPicker(dl, wp, pad + timeW + 8f, y,
                Clip(title, ws.X - pad * 2 - mainW - timeW - 34f));
            BText(dl, wp + new Vector2(ws.X - pad - mainW, y), C.MeterAccentColor, main);
            y += lineH + 5f;
        }
        else
        {
            var mainW = ImGui.CalcTextSize(main).X;
            TitleWithPicker(dl, wp, pad, y, Clip(title, ws.X - pad * 2 - mainW - 26f));
            BText(dl, wp + new Vector2(ws.X - pad - mainW, y), C.MeterAccentColor, main);
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

        dl.AddLine(wp + new Vector2(pad, y - 2f), wp + new Vector2(ws.X - pad, y - 2f), 0x22FFFFFF);

        // Double-click the header band to cycle full / slim / hidden.
        if (!C.MeterClickThrough
            && ImGui.IsMouseHoveringRect(wp + new Vector2(0, top), wp + new Vector2(ws.X, y))
            && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            C.MeterHeaderStyle = (C.MeterHeaderStyle + 1) % 3;
            C.SaveSettings();
        }
    }

    // The encounter name with a small caret: click to look back at past pulls.
    private void TitleWithPicker(ImDrawListPtr dl, Vector2 wp, float x, float y, string shown)
    {
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
        if (ImGui.InvisibleButton("##titlepick", new Vector2(tw + cw + 9f, ImGui.GetTextLineHeight() + 2f)))
            ImGui.OpenPopup("##meterpulls");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Pick a pull");
    }

    // ---- columns -----------------------------------------------------------

    private readonly record struct Col(string Key, string Label, string Sample, Func<MeterCombatant, string> Text);

    private static readonly Col[] AllCols =
    {
        new("rdps", "rDPS", "999.9k", c => Num(c.RDps)),
        new("dps", "DPS", "999.9k", c => Num(c.Dps)),
        new("dmgpct", "D%", "99.9%", c => c.DamagePct.Length > 0 ? c.DamagePct : "-"),
        new("crit", "CRIT", "99.9%", c => $"{c.CritPct:0.#}%"),
        new("dh", "DH", "99.9%", c => $"{c.DirectHitPct:0.#}%"),
        new("hps", "HPS", "999.9k", c => Num(c.Hps)),
        new("healed", "HEALED", "9.99M", c => Num(c.Healed)),
        new("overheal", "OH%", "99.9%", c => $"{c.OverhealPct:0.#}%"),
        new("taken", "TAKEN", "999.9k", c => Num(c.Taken)),
        new("deaths", "D", "9", c => c.Deaths.ToString()),
    };

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

    private void DrawColumnHeader(List<Col> cols, ImDrawListPtr dl, Vector2 wp, Vector2 ws, float pad, ref float y)
    {
        var lineH = ImGui.GetTextLineHeight();
        var rowTop = y;
        var x = ws.X - pad;
        var rects = new List<(string Key, float X0, float X1)>();
        foreach (var slot in Slots(cols))
        {
            x -= slot.Width;
            var w = ImGui.CalcTextSize(slot.Col.Label).X;
            var dragging = _dragCol == slot.Col.Key;
            BText(dl, wp + new Vector2(x + slot.Width - w, y),
                dragging ? 0x55FFFFFFu : C.MeterSubColor, slot.Col.Label);
            rects.Add((slot.Col.Key, x, x + slot.Width));

            // Each label is a grab handle: drag it left or right to reorder.
            // The mode-injected metric column is pinned, so it gets no handle.
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

        // Where the drop would land: the slot under the mouse, before or after
        // its center.
        var mouseX = ImGui.GetMousePos().X - wp.X;
        string? over = null;
        var after = false;
        foreach (var r in rects)
            if (mouseX >= r.X0 - ColGap * 0.5f && mouseX <= r.X1 + ColGap * 0.5f)
            {
                over = r.Key;
                after = mouseX > (r.X0 + r.X1) * 0.5f;
            }

        // Ghost label on the cursor plus an insertion mark, on the foreground
        // list so the bars can't paint over them.
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

        // Dropped: reorder in the saved list (a drop on the pinned metric or
        // off the row leaves things as they were).
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

    // Right-to-left column slots, each wide enough for its label and its
    // biggest plausible value so every row lines up.
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

    // ---- bars --------------------------------------------------------------

    private void DrawRows(MeterEncounter enc, List<Col> cols, float pad)
    {
        var rows = new List<MeterCombatant>(enc.Rows);
        rows.Sort((a, b) => Metric(b).CompareTo(Metric(a)));
        var max = 1.0;
        foreach (var r in rows) max = Math.Max(max, Metric(r));
        // Ranks are fixed before any trimming, so a shown row keeps its real place.
        var ranked = new List<(MeterCombatant Row, int Rank)>(rows.Count);
        for (var i = 0; i < rows.Count; i++) ranked.Add((rows[i], i + 1));
        // A row cap keeps the top of the list, plus your own row wherever it sits.
        if (C.MeterMaxRows > 0 && ranked.Count > C.MeterMaxRows)
        {
            var me = rows.FindIndex(r => IsYou(r, Plugin.LocalPlayer?.Name.ToString() ?? ""));
            var keep = ranked.GetRange(0, C.MeterMaxRows);
            if (me >= C.MeterMaxRows) keep[^1] = ranked[me];
            ranked = keep;
        }

        var slots = Slots(cols);
        var lineH = ImGui.GetTextLineHeight();
        var rowH = MathF.Max(lineH + 4f, C.MeterBarHeight);
        _rowStride = rowH + C.MeterBarGap;
        var you = Plugin.LocalPlayer?.Name.ToString() ?? "";

        // Content width, not window width, so a scrollbar never overlaps the bars.
        var w = MathF.Max(60f, ImGui.GetContentRegionAvail().X);
        foreach (var (r, rank) in ranked)
        {
            var p = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton($"##bar{rank}", new Vector2(w, rowH));
            var hovered = !C.MeterClickThrough && ImGui.IsItemHovered();
            var dl = ImGui.GetWindowDrawList();

            var jobColor = C.MeterJobColors && JobColors.TryGetValue(r.Job, out var jc) ? jc : C.MeterAccentColor;
            var rgb = jobColor & 0x00FFFFFF;

            dl.AddRectFilled(p + new Vector2(pad - 3f, 0), p + new Vector2(w - pad + 3f, rowH),
                hovered ? Brighten(C.MeterRowColor) : C.MeterRowColor, 4f);
            var fill = (float)(Metric(r) / max) * (w - pad * 2 + 6f);
            if (fill > 2f)
            {
                var a = p + new Vector2(pad - 3f, 0);
                var b = p + new Vector2(pad - 3f + fill, rowH);
                var op = C.MeterBarOpacity;
                switch (C.MeterBarStyle)
                {
                    case 1: // glass: solid fill with a shine across the top half
                        dl.AddRectFilled(a, b, Fade(rgb | 0x5C000000, op), 4f);
                        dl.AddRectFilledMultiColor(a + new Vector2(1f, 1f), new Vector2(b.X, p.Y + rowH * 0.55f),
                            0x24FFFFFF, 0x24FFFFFF, 0x00FFFFFF, 0x00FFFFFF);
                        break;
                    case 2: // gradient: strong at the left, fading right
                        dl.AddRectFilledMultiColor(a + new Vector2(0f, 1f), b - new Vector2(0f, 1f),
                            Fade(rgb | 0x8C000000, op), Fade(rgb | 0x26000000, op),
                            Fade(rgb | 0x26000000, op), Fade(rgb | 0x8C000000, op));
                        break;
                    case 3: // outline: a hollow bar with a bright edge
                        dl.AddRectFilled(a, b, Fade(rgb | 0x1A000000, op), 4f);
                        dl.AddRect(a, b, Fade(rgb | 0xCC000000, op), 4f);
                        break;
                    case 4: // minimal: a rule under the row, no fill
                        dl.AddRectFilled(new Vector2(a.X, b.Y - 2f), b, Fade(rgb | 0xD9000000, op));
                        break;
                    default: // flat
                        dl.AddRectFilled(a, b, Fade(rgb | 0x5C000000, op), 4f);
                        break;
                }
                if (C.MeterBarStyle != 4)
                    dl.AddRectFilled(a, p + new Vector2(pad, rowH), Fade(rgb | 0xE6000000, op), 2f);
            }
            if (C.MeterHighlightYou && IsYou(r, you))
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
            var x = p.X + pad + 4f;
            if (C.MeterShowRank)
            {
                var rankText = $"{rank}.";
                var rankW = ImGui.CalcTextSize(rows.Count >= 10 ? "88." : "8.").X;
                BText(dl,
                    new Vector2(x + rankW - ImGui.CalcTextSize(rankText).X, ty),
                    C.MeterSubColor, rankText);
                x += rankW + 5f;
            }
            if (C.MeterShowJobIcons && Jobs.ByAbbreviation(r.Job) is { } job)
            {
                var sz = rowH - 5f;
                ImGui.SetCursorScreenPos(new Vector2(x, p.Y + 2.5f));
                Icons.Draw(62100u + job.RowId, new Vector2(sz, sz));
                x += sz + 5f;
            }

            // Numeric slots right-to-left; the name gets whatever is left.
            var rx = p.X + w - pad;
            foreach (var slot in slots)
            {
                rx -= slot.Width;
                var text = slot.Col.Text(r);
                var tw = ImGui.CalcTextSize(text).X;
                // The leading (leftmost-configured) column reads bright.
                var bright = slot.Col.Key == cols[0].Key;
                BText(dl, new Vector2(rx + slot.Width - tw, ty),
                    bright ? C.MeterTextColor : C.MeterSubColor, text);
                rx -= ColGap;
            }

            var name = DisplayName(r, you);
            var nameMax = rx - x - 4f;
            if (nameMax > 12f)
                BText(dl, new Vector2(x, ty),
                    IsYou(r, you) ? C.MeterYouColor : C.MeterTextColor, Clip(name, nameMax));

            if (hovered)
            {
                PushMenuTheme();
                RowTooltip(r);
                PopMenuTheme();
            }
            ImGui.SetCursorScreenPos(new Vector2(p.X, p.Y + rowH + C.MeterBarGap));
        }
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

    private void RowTooltip(MeterCombatant r)
    {
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(r.Display.Length > 0 ? r.Display : r.Name);
        ImGui.Separator();
        ImGui.TextUnformatted($"rDPS {Num(r.RDps)}   DPS {Num(r.Dps)}   damage {Num(r.Damage)}");
        ImGui.TextColored(Theme.V(Theme.Muted),
            $"crit {r.CritPct:0.#}%  direct {r.DirectHitPct:0.#}%  deaths {r.Deaths}");
        if (r.Hps > 0)
            ImGui.TextColored(Theme.V(Theme.Muted), $"HPS {Num(r.Hps)}  overheal {r.OverhealPct:0.#}%");
        if (r.MaxHit.Length > 0)
            ImGui.TextColored(Theme.V(Theme.Muted), $"biggest hit: {r.MaxHit.Replace('-', ' ')}");
        ImGui.EndTooltip();
    }

    // ---- right-click menu --------------------------------------------------

    private void ContextMenu()
    {
        if (C.MeterClickThrough) return;
        // Open on right-click anywhere over the meter; the bars live in a child
        // region, so the stock context-window helper would miss most of it.
        if (!_tabMenuOpen && ImGui.IsMouseReleased(ImGuiMouseButton.Right)
            && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
            ImGui.OpenPopup("##metermenu");

        PushMenuTheme();
        if (!ImGui.BeginPopup("##metermenu")) { PopMenuTheme(); return; }

        var m = _plugin.Meter;
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
                if (ImGui.MenuItem("Drop shadow", "", C.MeterTextShadow)) { C.MeterTextShadow = !C.MeterTextShadow; C.SaveSettings(); }
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
            // Turning this on removes the right-click menu too; the config page
            // is the way back.
            C.MeterClickThrough = !C.MeterClickThrough;
            C.SaveSettings();
        }
        ImGui.Separator();
        if (ImGui.MenuItem("Clear data")) { m.ResetEncounter(); _histIdx = -1; }
        if (ImGui.MenuItem("Settings...")) _plugin.ConfigWindow.OpenMeterPage();

        ImGui.EndPopup();
        PopMenuTheme();
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
        var size = ImGui.GetWindowSize();
        if ((size - C.MeterSize).LengthSquared() > 1f) { C.MeterSize = size; _sizeDirty = true; }
        if ((_posDirty || _sizeDirty) && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (_sizeDirty && _rowStride > 4f)
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

    private static string Clip(string s, float maxW)
    {
        if (ImGui.CalcTextSize(s).X <= maxW) return s;
        while (s.Length > 1 && ImGui.CalcTextSize(s + "…").X > maxW) s = s[..^1];
        return s + "…";
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
        "dmgpct" => "Damage %",
        "crit" => "Crit %",
        "dh" => "Direct hit %",
        "hps" => "HPS",
        "healed" => "Healed total",
        "overheal" => "Overheal %",
        "taken" => "Damage taken",
        _ => "Deaths",
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
