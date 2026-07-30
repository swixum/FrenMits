using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;

namespace FrenMits.Windows;

// The Fren Meter settings page.
public partial class ConfigWindow
{
    public void OpenMeterPage()
    {
        IsOpen = true;
        _nav = NavKind.Meter;
    }

    private void DrawMeterPage()
    {
        SeparatorText("Fren Meter");
        ImGui.TextWrapped("A live damage meter fed by ACT, drawn in-game as its own compact overlay. "
                          + "Bars are colored by job, the columns are yours to pick, and everything mid-pull "
                          + "lives on the meter's right-click menu: mode, past pulls, columns, lock.");
        ImGui.Spacing();
        ImGui.TextWrapped("It also shows rDPS, worked out here in the plugin from the raw combat log: each "
                          + "player's damage minus what other people's raid buffs added to it, plus what their "
                          + "own buffs added to everyone else's.");
        ImGui.Spacing();

        C.MeterEnabled = CfgCheck("Enable Fren Meter", C.MeterEnabled);
        Tip("Connects to your parser and shows the meter overlay.");
        if (!C.MeterEnabled) return;

        // Connection status, truthful and live.
        var connected = _plugin.Meter.Connected;
        StatusDot(connected ? ImGuiColors.HealerGreen : ImGuiColors.DalamudYellow);
        ImGui.SameLine(0, 6);
        ImGui.TextColored(connected ? ImGuiColors.HealerGreen : ImGuiColors.DalamudYellow,
            _plugin.Meter.StatusText);

        if (!ImGui.BeginTabBar("##metertabs", ImGuiTabBarFlags.None)) return;

        if (ImGui.BeginTabItem("Connection"))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Auto finds the parser by itself: the in-process parser plugin first, then "
                              + "ACT's overlay WebSocket. Pick one explicitly only if you run both.");
            ImGui.Spacing();

            ImGui.SetNextItemWidth(240f);
            var conn = C.MeterConnection;
            if (ImGui.Combo("Source", ref conn, "Auto\0Parser plugin (in-process)\0ACT WebSocket\0"))
            { C.MeterConnection = conn; C.SaveSettings(); ReconnectMeter(); }
            Tip("Where the combat data comes from.");

            if (C.MeterConnection != 1)
            {
                ImGui.SetNextItemWidth(300f);
                var addr = C.MeterSocketAddress;
                if (ImGui.InputText("WebSocket address", ref addr, 128))
                { C.MeterSocketAddress = addr; C.SaveSettings(); }
                Tip("ACT: OverlayPlugin WSServer, usually ws://127.0.0.1:10501/ws.");
            }

            ImGui.Spacing();
            if (ImGui.Button("Reconnect")) ReconnectMeter();
            Tip("Drops the link and searches again now.");

            ImGui.Spacing();
            ImGui.TextDisabled("The meter needs ACT (or a parser plugin) running with the FFXIV plugin loaded.");
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Display"))
        {
            ImGui.Spacing();
            C.MeterLocked = CfgCheck("Lock position and size", C.MeterLocked);
            Tip("Unlock, then drag the meter or its edges.");
            C.MeterClickThrough = CfgCheck("Click-through", C.MeterClickThrough);
            Tip("Mouse ignores the meter entirely. The right-click menu goes with it; turn it back off here.");

            var pos = C.MeterPosition;
            if (Widgets.SliderInput("Horizontal", ref pos.X, 0f, 1f, "%.2f"))
            { C.MeterPosition = pos; C.SaveSettings(); _plugin.MeterWindow.RequestReposition(); }
            ImGui.SameLine(0, 18);
            if (Widgets.SliderInput("Vertical", ref pos.Y, 0f, 1f, "%.2f"))
            { C.MeterPosition = pos; C.SaveSettings(); _plugin.MeterWindow.RequestReposition(); }

            ImGui.Spacing();
            var px = C.MeterFontSizePx;
            if (Widgets.SliderInput("Text size", ref px, 11f, 26f, "%.0f px")) { C.MeterFontSizePx = px; C.SaveSettings(); }
            var barH = C.MeterBarHeight;
            if (Widgets.SliderInput("Bar height", ref barH, 16f, 44f, "%.0f px")) { C.MeterBarHeight = barH; C.SaveSettings(); }
            var gap = C.MeterBarGap;
            if (Widgets.SliderInput("Bar spacing", ref gap, 0f, 10f, "%.0f px")) { C.MeterBarGap = gap; C.SaveSettings(); }
            var op = C.MeterBgOpacity;
            if (Widgets.SliderInput("Background", ref op, 0f, 1f, "%.2f")) { C.MeterBgOpacity = op; C.SaveSettings(); }
            var round = C.MeterRounding;
            if (Widgets.SliderInput("Corner rounding", ref round, 0f, 14f, "%.0f px")) { C.MeterRounding = round; C.SaveSettings(); }

            ImGui.Spacing();
            if (ImGui.BeginTable("##meterdisplaygrid", 2))
            {
                C.MeterShowRank = GridCheck("Rank numbers", C.MeterShowRank);
                C.MeterShowJobIcons = GridCheck("Job icons", C.MeterShowJobIcons);
                C.MeterJobColors = GridCheck("Color bars by job", C.MeterJobColors, "Off = every bar uses the accent color.");
                C.MeterColumnHeader = GridCheck("Column labels", C.MeterColumnHeader);
                C.MeterShowRaidTotal = GridCheck("Raid rDPS total", C.MeterShowRaidTotal,
                    "The whole group's combined rDPS at the top right.");
                C.MeterYou = GridCheck("Call your row \"You\"", C.MeterYou);
                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.SetNextItemWidth(220f);
            var header = C.MeterHeaderStyle;
            if (ImGui.Combo("Header", ref header, "Full (title + totals)\0Slim (one line)\0Hidden\0"))
            { C.MeterHeaderStyle = header; C.SaveSettings(); }
            Tip("Double-clicking the meter's header cycles this too.");

            ImGui.SetNextItemWidth(220f);
            var names = C.MeterNameStyle;
            if (ImGui.Combo("Names", ref names, "Full name\0First name\0First name + initial\0"))
            { C.MeterNameStyle = names; C.SaveSettings(); }

            if (!C.MeterJobColors)
            {
                var col = ColorToVec4(C.MeterAccentColor);
                if (ImGui.ColorEdit4("Accent color", ref col, ImGuiColorEditFlags.NoInputs))
                { C.MeterAccentColor = Vec4ToColor(col); C.SaveSettings(); }
            }

            ImGui.Spacing();
            ImGui.TextDisabled("Turn on Test mode in the header to place it with a sample pull.");
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Columns"))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Ticked columns show on the meter, in this order. Reorder with the arrows here, "
                              + "or just drag the column labels around on the meter itself. The active mode's "
                              + "own number is always shown first.");
            ImGui.Spacing();

            foreach (var key in new[] { "rdps", "dps", "dmgpct", "crit", "dh", "hps", "overheal", "taken", "deaths" })
                DrawColumnRow(key);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("About rDPS"))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("rDPS answers \"what did this player really bring\": their own damage, minus the "
                              + "part other people's raid buffs added to it, plus the damage their buffs added "
                              + "to everyone else. A support job that pumps the party reads honestly instead of "
                              + "looking flat.");
            ImGui.Spacing();
            ImGui.TextWrapped("It is computed here from the raw combat log line by line. Flat buffs (Embolden, "
                              + "Divination, Dokumori, ...) are split out exactly; crit and direct-hit buffs "
                              + "(Battle Litany, Chain Stratagem, Battle Voice, Devilment) are priced at their "
                              + "expected value per hit. Numbers land within a percent or two of the logs "
                              + "site's live figure; that site can also read gear, so treat theirs as the "
                              + "final word.");
            ImGui.Spacing();
            ImGui.TextDisabled("rDPS needs the raw line stream, so it fills in a second or two behind the bars.");
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawColumnRow(string key)
    {
        var label = MeterWindow.ColumnLabel(key);
        var idx = C.MeterColumns.IndexOf(key);
        var on = idx >= 0;
        if (GreenCheckbox($"##mcol_{key}", ref on))
        {
            if (on) C.MeterColumns.Add(key);
            else C.MeterColumns.Remove(key);
            C.SaveSettings();
        }
        ImGui.SameLine(0, 8);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        if (idx >= 0)
        {
            ImGui.SameLine(210f);
            ImGui.PushID(key);
            if (ImGui.ArrowButton("up", ImGuiDir.Up) && idx > 0)
            {
                (C.MeterColumns[idx - 1], C.MeterColumns[idx]) = (C.MeterColumns[idx], C.MeterColumns[idx - 1]);
                C.SaveSettings();
            }
            ImGui.SameLine(0, 4);
            if (ImGui.ArrowButton("down", ImGuiDir.Down) && idx < C.MeterColumns.Count - 1)
            {
                (C.MeterColumns[idx + 1], C.MeterColumns[idx]) = (C.MeterColumns[idx], C.MeterColumns[idx + 1]);
                C.SaveSettings();
            }
            ImGui.PopID();
        }
    }

    // Drop the link; the next framework tick reconnects with the fresh settings.
    private void ReconnectMeter() => _plugin.Meter.Link.RetryNow();
}
