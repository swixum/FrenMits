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

        ImGui.SetNextWindowSize(C.MeterSize, C.MeterLocked ? ImGuiCond.Always : ImGuiCond.Appearing);
        OverlayChrome.ApplyPosition(C.MeterPosition, C.MeterLocked, ref _applyPos);
    }

    public override bool DrawConditions()
    {
        if (!C.MeterEnabled) return false;
        if (Plugin.CutsceneActive && !C.TestMode) return false;
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
        dl.AddRect(wp, wp + ws, 0x24FFFFFF, C.MeterRounding);

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
            ImGui.SetCursorPos(new Vector2(10, 8));
            ImGui.TextColored(Theme.V(C.MeterTextColor), "Fren Meter");
            ImGui.SetCursorPosX(10);
            ImGui.TextColored(Theme.V(C.MeterSubColor), _plugin.Meter.StatusText);
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
        var footerH = (C.MeterButtons || C.MeterHealingTab) && !C.MeterClickThrough ? 21f : 0f;
        ImGui.SetCursorPos(new Vector2(0, y));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0u);
        if (ImGui.BeginChild("##meterrows", new Vector2(ws.X, ws.Y - y - footerH - 4f), false,
                C.MeterClickThrough ? ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoNav : ImGuiWindowFlags.None))
            DrawRows(enc, cols, pad);
        ImGui.EndChild();
        ImGui.PopStyleColor();

        if (footerH > 0) DrawFooter(dl, wp, ws, footerH, pad);
        ContextMenu();
    }

    // ---- footer buttons ----------------------------------------------------

    private void DrawFooter(ImDrawListPtr dl, Vector2 wp, Vector2 ws, float h, float pad)
    {
        var m = _plugin.Meter;
        var top = ws.Y - h - 2f;
        dl.AddLine(wp + new Vector2(pad, top), wp + new Vector2(ws.X - pad, top), 0x22FFFFFF);

        ImGui.SetCursorPos(new Vector2(pad - 3f, top + 3f));
        ImGui.PushStyleColor(ImGuiCol.Button, 0u);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0x2EFFFFFFu);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0x45FFFFFFu);
        ImGui.PushStyleColor(ImGuiCol.Text, C.MeterSubColor);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(5f, 0f));

        if (C.MeterHealingTab)
        {
            TabChip(dl, "Damage", 0);
            ImGui.SameLine(0, 2);
            TabChip(dl, "Healing", 1);
            if (C.MeterButtons) ImGui.SameLine(0, 10);
        }

        if (C.MeterButtons)
        {
            if (FooterBtn(FontAwesomeIcon.ChevronLeft, "Older pull") && _histIdx < m.History.Count - 1)
                _histIdx++;
            ImGui.SameLine(0, 2);
            if (FooterBtn(FontAwesomeIcon.List, "Pulls")) ImGui.OpenPopup("##meterpulls");
            ImGui.SameLine(0, 2);
            if (FooterBtn(FontAwesomeIcon.ChevronRight, "Newer pull") && _histIdx >= 0)
                _histIdx--;
            ImGui.SameLine(0, 8);
            if (FooterBtn(m.Paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause,
                    m.Paused ? "Resume" : "Pause"))
                m.Paused = !m.Paused;
            ImGui.SameLine(0, 2);
            if (FooterBtn(FontAwesomeIcon.Undo, "Reset")) { m.Clear(); _histIdx = -1; }
        }

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);

        // What the meter is showing right now.
        var label = m.Paused ? "paused" : _histIdx >= 0 ? $"pull -{_histIdx + 1}" : "live";
        var lw = ImGui.CalcTextSize(label).X;
        OverlayChrome.BoardText(dl, wp + new Vector2(ws.X - pad - lw, top + 4f),
            m.Paused ? C.MeterAccentColor : C.MeterSubColor, label, true);

        if (ImGui.BeginPopup("##meterpulls"))
        {
            DrawPullList();
            ImGui.EndPopup();
        }
    }

    // A footer mode tab, underlined in the accent while active.
    private void TabChip(ImDrawListPtr dl, string label, int mode)
    {
        var active = C.MeterMode == mode;
        if (active) ImGui.PushStyleColor(ImGuiCol.Text, C.MeterTextColor);
        var clicked = ImGui.SmallButton(label);
        if (active)
        {
            ImGui.PopStyleColor();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            dl.AddLine(new Vector2(min.X + 2, max.Y), new Vector2(max.X - 2, max.Y), C.MeterAccentColor, 2f);
        }
        if (clicked && !active) { C.MeterMode = mode; C.SaveSettings(); }
    }

    private bool FooterBtn(FontAwesomeIcon icon, string tip)
    {
        bool clicked;
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            clicked = ImGui.Button($"{icon.ToIconString()}##ft{tip}");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
        return clicked;
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
        if (enc != m.Current || !enc.Active || m.Previous is not { } prev) return enc;
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
            OverlayChrome.BoardText(dl, wp + new Vector2(pad, y), C.MeterTimerColor, timeText, true);
            OverlayChrome.BoardText(dl, wp + new Vector2(pad + timeW + 8f, y), C.MeterTextColor,
                Clip(title, ws.X - pad * 2 - mainW - timeW - 20f), true);
            OverlayChrome.BoardText(dl, wp + new Vector2(ws.X - pad - mainW, y), C.MeterAccentColor, main, true);
            y += lineH + 5f;
        }
        else
        {
            var mainW = ImGui.CalcTextSize(main).X;
            OverlayChrome.BoardText(dl, wp + new Vector2(pad, y), C.MeterTextColor,
                Clip(title, ws.X - pad * 2 - mainW - 12f), true);
            OverlayChrome.BoardText(dl, wp + new Vector2(ws.X - pad - mainW, y), C.MeterAccentColor, main, true);
            y += lineH + 2f;

            var players = 0;
            foreach (var r in enc.Rows)
                if (r.Job.Length > 0)
                    players++;
            OverlayChrome.BoardText(dl, wp + new Vector2(pad, y), C.MeterTimerColor, timeText, true);
            OverlayChrome.BoardText(dl, wp + new Vector2(pad + ImGui.CalcTextSize(timeText).X, y),
                C.MeterSubColor, $"  ·  {players} in party", true);
            if (C.MeterShowRaidTotal && mode != 0)
            {
                var chip = $"Raid {Num(enc.RaidRDps)} rDPS";
                var w = ImGui.CalcTextSize(chip).X;
                OverlayChrome.BoardText(dl, wp + new Vector2(ws.X - pad - w, y), C.MeterSubColor, chip, true);
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

    // The configured columns, with the active mode's own metric always present.
    private List<Col> DisplayColumns()
    {
        var keys = new List<string>(C.MeterColumns);
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
            OverlayChrome.BoardText(dl, wp + new Vector2(x + slot.Width - w, y),
                dragging ? 0x55FFFFFFu : C.MeterSubColor, slot.Col.Label, true);
            rects.Add((slot.Col.Key, x, x + slot.Width));

            // Each label is a grab handle: drag it left or right to reorder.
            // The mode-injected metric column is pinned, so it gets no handle.
            if (!C.MeterClickThrough && C.MeterColumns.Contains(slot.Col.Key))
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
        if (_dragCol is not { } drag || !C.MeterColumns.Contains(drag)) { _dragCol = null; return; }

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
            var keys = C.MeterColumns;
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

        var slots = Slots(cols);
        var lineH = ImGui.GetTextLineHeight();
        var rowH = MathF.Max(lineH + 4f, C.MeterBarHeight);
        var you = Plugin.LocalPlayer?.Name.ToString() ?? "";
        var rank = 0;

        // Content width, not window width, so a scrollbar never overlaps the bars.
        var w = MathF.Max(60f, ImGui.GetContentRegionAvail().X);
        foreach (var r in rows)
        {
            rank++;
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
                switch (C.MeterBarStyle)
                {
                    case 1: // glass: solid fill with a shine across the top half
                        dl.AddRectFilled(a, b, rgb | 0x5C000000, 4f);
                        dl.AddRectFilledMultiColor(a + new Vector2(1f, 1f), new Vector2(b.X, p.Y + rowH * 0.55f),
                            0x24FFFFFF, 0x24FFFFFF, 0x00FFFFFF, 0x00FFFFFF);
                        break;
                    case 2: // gradient: strong at the left, fading right
                        dl.AddRectFilledMultiColor(a + new Vector2(0f, 1f), b - new Vector2(0f, 1f),
                            rgb | 0x8C000000, rgb | 0x26000000, rgb | 0x26000000, rgb | 0x8C000000);
                        break;
                    default: // flat
                        dl.AddRectFilled(a, b, rgb | 0x5C000000, 4f);
                        break;
                }
                dl.AddRectFilled(a, p + new Vector2(pad, rowH), rgb | 0xE6000000, 2f);
            }
            if (C.MeterHighlightYou && IsYou(r, you))
            {
                var yrgb = C.MeterYouColor & 0x00FFFFFF;
                dl.AddRectFilled(p + new Vector2(pad - 3f, 0), p + new Vector2(w - pad + 3f, rowH), yrgb | 0x12000000, 4f);
                dl.AddRect(p + new Vector2(pad - 3f, 0), p + new Vector2(w - pad + 3f, rowH), yrgb | 0x8C000000, 4f);
            }

            var ty = p.Y + (rowH - lineH) * 0.5f;
            var x = p.X + pad + 4f;
            if (C.MeterShowRank)
            {
                OverlayChrome.BoardText(dl, new Vector2(x, ty), C.MeterSubColor, $"{rank}.", true);
                x += ImGui.CalcTextSize($"{rank}.").X + 5f;
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
                OverlayChrome.BoardText(dl, new Vector2(rx + slot.Width - tw, ty),
                    bright ? C.MeterTextColor : C.MeterSubColor, text, true);
                rx -= ColGap;
            }

            var name = DisplayName(r, you);
            var nameMax = rx - x - 4f;
            if (nameMax > 12f)
                OverlayChrome.BoardText(dl, new Vector2(x, ty),
                    IsYou(r, you) ? C.MeterYouColor : C.MeterTextColor, Clip(name, nameMax), true);

            if (hovered) RowTooltip(r);
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
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Right)
            && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
            ImGui.OpenPopup("##metermenu");
        if (!ImGui.BeginPopup("##metermenu")) return;

        var m = _plugin.Meter;
        if (ImGui.BeginMenu("Pulls"))
        {
            DrawPullList();
            ImGui.EndMenu();
        }
        if (ImGui.MenuItem(m.Paused ? "Resume" : "Pause")) m.Paused = !m.Paused;

        ImGui.Separator();
        var mode = C.MeterMode;
        if (ImGui.MenuItem("Damage", "", mode == 0)) { C.MeterMode = 0; C.SaveSettings(); }
        if (ImGui.MenuItem("Healing", "", mode == 1)) { C.MeterMode = 1; C.SaveSettings(); }
        if (ImGui.MenuItem("Damage taken", "", mode == 2)) { C.MeterMode = 2; C.SaveSettings(); }
        if (ImGui.MenuItem("Deaths", "", mode == 3)) { C.MeterMode = 3; C.SaveSettings(); }

        ImGui.Separator();
        if (ImGui.BeginMenu("Columns"))
        {
            foreach (var col in AllCols)
            {
                var on = C.MeterColumns.Contains(col.Key);
                if (ImGui.MenuItem(ColumnLabel(col.Key), "", on))
                {
                    if (on) C.MeterColumns.Remove(col.Key);
                    else C.MeterColumns.Add(col.Key);
                    C.SaveSettings();
                }
            }
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
        if (ImGui.BeginMenu("Theme"))
        {
            foreach (var t in Themes)
                if (ImGui.MenuItem(t.Name))
                    ApplyTheme(C, t);
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
            var bars = C.MeterBarStyle;
            if (ImGui.MenuItem("Bars: flat", "", bars == 0)) { C.MeterBarStyle = 0; C.SaveSettings(); }
            if (ImGui.MenuItem("Bars: glass", "", bars == 1)) { C.MeterBarStyle = 1; C.SaveSettings(); }
            if (ImGui.MenuItem("Bars: gradient", "", bars == 2)) { C.MeterBarStyle = 2; C.SaveSettings(); }
            var style = C.MeterHeaderStyle;
            if (ImGui.MenuItem("Header: full", "", style == 0)) { C.MeterHeaderStyle = 0; C.SaveSettings(); }
            if (ImGui.MenuItem("Header: slim", "", style == 1)) { C.MeterHeaderStyle = 1; C.SaveSettings(); }
            if (ImGui.MenuItem("Header: hidden", "", style == 2)) { C.MeterHeaderStyle = 2; C.SaveSettings(); }
            ImGui.EndMenu();
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Lock position", "", C.MeterLocked)) { C.MeterLocked = !C.MeterLocked; C.SaveSettings(); }
        if (ImGui.MenuItem("Click-through", "", C.MeterClickThrough))
        {
            // Turning this on removes the right-click menu too; the config page
            // is the way back.
            C.MeterClickThrough = !C.MeterClickThrough;
            C.SaveSettings();
        }
        ImGui.Separator();
        if (ImGui.MenuItem("Clear data")) { m.Clear(); _histIdx = -1; }
        if (ImGui.MenuItem("Settings...")) _plugin.ConfigWindow.OpenMeterPage();

        ImGui.EndPopup();
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
            C.SaveSettings();
            _posDirty = _sizeDirty = false;
        }
    }

    // ---- helpers -----------------------------------------------------------

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

    public readonly record struct MeterTheme(
        string Name, uint Accent, uint Text, uint Sub, uint Bg, uint Rows, float Rounding, bool JobColors,
        int BarStyle);

    public static readonly MeterTheme[] Themes =
    {
        new("Fren Mits", 0xFFF6823B, 0xFFFFFFFF, 0xFFFFFFFF, 0xB80D0A09, 0x17FFFFFF, 5f, true, 0),
        new("Dark Mode", 0xFFFFB48A, 0xFFFFFFFF, 0xFFD8CDC8, 0xE6000000, 0x12FFFFFF, 6f, true, 0),
        new("Glass", 0xFFC5D14F, 0xFFFFFFFF, 0xFFF2F0E6, 0x5916120E, 0x22FFFFFF, 10f, true, 1),
        new("Ember", 0xFF3C8AFF, 0xFFFFFFFF, 0xFFC0D9FF, 0xCC060B14, 0x145C9AFF, 5f, true, 2),
        new("Jade", 0xFF99D334, 0xFFFFFFFF, 0xFFDCE8D2, 0xCC101307, 0x16FFFFFF, 5f, true, 2),
        new("Mono", 0xFFB8A99D, 0xFFFFFFFF, 0xFFC4C4C4, 0xD8101010, 0x1AFFFFFF, 2f, false, 0),
    };

    public static void ApplyTheme(Configuration c, MeterTheme t)
    {
        c.MeterAccentColor = t.Accent;
        c.MeterTextColor = t.Text;
        c.MeterSubColor = t.Sub;
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
