using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// Past pulls in a window of their own, opened from the meter's footer.
public class MeterHistoryWindow : Window
{
    private readonly Plugin _plugin;

    public MeterHistoryWindow(Plugin plugin) : base("Pull history###fmmeterhistory")
    {
        _plugin = plugin;
        Size = new Vector2(520, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 190),
            MaximumSize = new Vector2(1400, 1400),
        };
    }

    public override void PreDraw()
    {
        Theme.PushWindow();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
        Theme.PopWindow();
    }

    public override void Draw()
    {
        Theme.PushWidgets();
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, HeaderBg);
        ImGui.PushStyleColor(ImGuiCol.TableRowBg, 0u);
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, RowAlt);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(8, 4));
        try { DrawBody(); }
        finally
        {
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);
            Theme.PopWidgets();
        }
    }

    // The header strip, the every-other-row wash, and the two row states.
    private const uint HeaderBg = 0xFF34271F;
    private const uint RowAlt = 0x09FFFFFF;
    private const uint RowHot = 0xFF50362A;
    private const uint RowPicked = 0xFF634032;

    private void DrawBody()
    {
        var m = _plugin.Meter;
        var style = ImGui.GetStyle();
        var footerH = ImGui.GetFrameHeight() + style.ItemSpacing.Y * 2;

        // Every column is sized off its own widest value, so a font change follows.
        var timeW = ImGui.CalcTextSize("00:00").X + 6f;
        var resultW = ImGui.CalcTextSize("wiped at 99.9%").X + 22f;
        var rdpsW = ImGui.CalcTextSize("Raid rDPS").X + 6f;
        var deathsW = ImGui.CalcTextSize("Deaths").X + 6f;
        var whenW = ImGui.CalcTextSize("just now").X + 6f;

        var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX;
        if (ImGui.BeginTable("##pullhistory", 6, flags, new Vector2(0, -footerH)))
        {
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, timeW);
            ImGui.TableSetupColumn("Fight", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Result", ImGuiTableColumnFlags.WidthFixed, resultW);
            ImGui.TableSetupColumn("Raid rDPS", ImGuiTableColumnFlags.WidthFixed, rdpsW);
            ImGui.TableSetupColumn("Deaths", ImGuiTableColumnFlags.WidthFixed, deathsW);
            ImGui.TableSetupColumn("When", ImGuiTableColumnFlags.WidthFixed, whenW);
            ImGui.TableSetupScrollFreeze(0, 1);
            DrawColumnHeader();

            DrawLiveRow(m.Current);
            for (var i = 0; i < m.History.Count; i++) DrawPullRow(m.History[i], i);
            ImGui.EndTable();
        }

        DrawFooter(m, style);
    }

    // The stock header row, with the two number columns pushed right.
    private void DrawColumnHeader()
    {
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        for (var col = 0; col < 6; col++)
        {
            if (!ImGui.TableSetColumnIndex(col)) continue;
            var name = ImGui.TableGetColumnName(col);
            if (col is 3 or 4) RightText(name, Theme.TextBright);
            else ImGui.TableHeader(name);
        }
    }

    // The pull in progress, so live and past sit in one list.
    private void DrawLiveRow(MeterEncounter? live)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        var clock = live?.Duration is { Length: > 0 } d ? d : "0:00";
        if (RowHead("##livepull", _plugin.MeterWindow.HistoryIndex < 0, clock))
            _plugin.MeterWindow.HistoryIndex = -1;
        var hovered = ImGui.IsItemHovered();

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(live is { Title.Length: > 0 } ? live.Title : "Current pull");

        ImGui.TableNextColumn();
        if (live is { Active: true }) Pill("in progress", Theme.Accent);
        else Pill(live == null ? "nothing yet" : "ended", Theme.Muted);

        ImGui.TableNextColumn();
        RightText(live != null ? MeterWindow.Num(live.RaidRDps) : "-", Theme.TextBright);

        ImGui.TableNextColumn();
        RightText(live != null ? live.TotalDeaths.ToString() : "-",
            live is { TotalDeaths: > 0 } ? Theme.Danger : Theme.Muted);

        ImGui.TableNextColumn();
        ImGui.TextColored(Theme.V(Theme.Muted), "now");

        if (hovered && live is { Rows.Count: > 0 }) TopTooltip(live, "in progress");
    }

    private void DrawPullRow(MeterEncounter enc, int index)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        if (RowHead($"##pull{index}", _plugin.MeterWindow.HistoryIndex == index,
                enc.Duration.Length > 0 ? enc.Duration : "0:00"))
            _plugin.MeterWindow.HistoryIndex = index;
        var hovered = ImGui.IsItemHovered();

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(enc.Title.Length > 0 ? enc.Title : "Encounter");

        ImGui.TableNextColumn();
        var outcome = MeterWindow.Outcome(enc);
        if (outcome.Length > 0) Pill(outcome, Tint(enc));
        else Pill("unknown", Theme.Muted);

        ImGui.TableNextColumn();
        RightText(MeterWindow.Num(enc.RaidRDps), Theme.TextBright);

        ImGui.TableNextColumn();
        RightText(enc.TotalDeaths.ToString(), enc.TotalDeaths > 0 ? Theme.Danger : Theme.Muted);

        ImGui.TableNextColumn();
        ImGui.TextColored(Theme.V(Theme.Muted), Ago(enc.When));

        if (hovered) TopTooltip(enc, enc.When.ToString("t"));
    }

    // The row's hit area, with the clock laid over it and a stripe when picked.
    private static bool RowHead(string id, bool selected, string clock)
    {
        var cellW = ImGui.GetContentRegionAvail().X;
        var start = ImGui.GetCursorScreenPos();

        // The wash is painted per row instead, so it covers the cell padding too.
        ImGui.PushStyleColor(ImGuiCol.Header, 0u);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0u);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0u);
        var picked = ImGui.Selectable(id, selected, ImGuiSelectableFlags.SpanAllColumns);
        ImGui.PopStyleColor(3);
        if (selected) ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, RowPicked);
        else if (ImGui.IsItemHovered()) ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, RowHot);

        var dl = ImGui.GetWindowDrawList();
        var lineH = ImGui.GetTextLineHeight();
        dl.AddText(new Vector2(start.X + cellW - ImGui.CalcTextSize(clock).X, start.Y),
            selected ? Theme.TextBright : Theme.Muted, clock);
        if (selected)
            dl.AddRectFilled(new Vector2(start.X - 6f, start.Y - 4f),
                new Vector2(start.X - 3.5f, start.Y + lineH + 4f), Theme.Accent, 1.5f);
        return picked;
    }

    // A tinted capsule, so the result reads before the words do.
    private static void Pill(string label, uint color)
    {
        var size = ImGui.CalcTextSize(label);
        const float pad = 7f;
        var p = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        // The capsule leans into the cell padding, so its text still lines up.
        dl.AddRectFilled(new Vector2(p.X, p.Y - 1f), new Vector2(p.X + size.X + pad * 2, p.Y + size.Y + 1f),
            (color & 0x00FFFFFFu) | 0x30000000u, 4f);
        dl.AddText(new Vector2(p.X + pad, p.Y), color, label);
        ImGui.Dummy(new Vector2(size.X + pad * 2, size.Y));
    }

    // Numbers read as a column only when their right edges line up.
    private static void RightText(string text, uint color)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var w = ImGui.CalcTextSize(text).X;
        if (avail > w) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - w);
        ImGui.TextColored(Theme.V(color), text);
    }

    // Who carried the pull, without having to open it.
    private void TopTooltip(MeterEncounter enc, string when)
    {
        _top.Clear();
        foreach (var r in enc.Rows)
            if (!r.LimitBreak)
                _top.Add(r);
        if (_top.Count == 0) return;
        _top.Sort((a, b) => b.RDps.CompareTo(a.RDps));
        var shown = Math.Min(TooltipRows, _top.Count);

        // The rDPS column starts past the longest name, so the numbers align.
        var nameW = 0f;
        for (var i = 0; i < shown; i++)
            nameW = MathF.Max(nameW, ImGui.CalcTextSize($"{i + 1}. {Who(_top[i])} {_top[i].Job}").X);

        ImGui.BeginTooltip();
        ImGui.TextColored(Theme.V(Theme.Muted), when);
        ImGui.Separator();
        for (var i = 0; i < shown; i++)
        {
            var r = _top[i];
            ImGui.TextColored(Theme.V(Theme.Muted), $"{i + 1}.");
            ImGui.SameLine(0, 5);
            ImGui.TextColored(Theme.V(Theme.TextBright), Who(r));
            if (r.Job.Length > 0)
            {
                ImGui.SameLine(0, 5);
                ImGui.TextColored(Theme.V(JobTint(r.Job)), r.Job);
            }
            ImGui.SameLine(nameW + 18f);
            ImGui.TextColored(Theme.V(Theme.Muted), $"{MeterWindow.Num(r.RDps)} rDPS");
        }
        if (_top.Count > shown)
            ImGui.TextColored(Theme.V(Theme.Muted), $"+{_top.Count - shown} more");
        ImGui.EndTooltip();
    }

    private const int TooltipRows = 4;

    private readonly List<MeterCombatant> _top = new();

    private static string Who(MeterCombatant c) => c.Display.Length > 0 ? c.Display : c.Name;

    private static uint JobTint(string job)
        => MeterWindow.JobColors.TryGetValue(job, out var c) ? c : Theme.Muted;

    private void DrawFooter(Meter m, ImGuiStylePtr style)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.V(Theme.Muted), m.History.Count switch
        {
            0 => "Pulls land here as they finish",
            1 => "1 past pull",
            var n => $"{n} past pulls",
        });

        if (m.History.Count == 0) return;
        var clearW = ImGui.CalcTextSize("Clear").X + style.FramePadding.X * 2;
        ImGui.SameLine(MathF.Max(0f, ImGui.GetContentRegionMax().X - clearW));
        if (ImGui.Button("Clear")) ImGui.OpenPopup("##clearhistory");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Forget every past pull.");

        if (!ImGui.BeginPopup("##clearhistory")) return;
        ImGui.TextUnformatted("Forget every past pull?");
        ImGui.TextColored(Theme.V(Theme.Muted), "The pull on the board stays.");
        ImGui.Spacing();
        if (ImGui.Button("Clear"))
        {
            m.History.Clear();
            _plugin.MeterWindow.HistoryIndex = -1;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private static uint Tint(MeterEncounter enc)
        => enc.Ended == PullEnd.Kill ? Theme.Good
            : enc.BossLeft > 0f && enc.BossLeft <= MeterWindow.NearMiss ? Theme.Warn
            : Theme.Danger;

    private static string Ago(DateTime when)
    {
        var mins = (DateTime.Now - when).TotalMinutes;
        if (mins < 1) return "just now";
        if (mins < 60) return $"{(int)mins}m ago";
        return $"{(int)(mins / 60)}h ago";
    }
}
