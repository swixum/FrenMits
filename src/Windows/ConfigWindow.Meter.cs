using System;
using System.Collections.Generic;
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
        ImGui.TextWrapped("A damage meter fed by ACT, with rDPS computed from the combat log.");
        ImGui.Spacing();

        C.MeterEnabled = CfgCheck("Enable Fren Meter", C.MeterEnabled);
        if (!C.MeterEnabled) return;

        var connected = _plugin.Meter.Connected;
        StatusDot(connected ? ImGuiColors.HealerGreen : ImGuiColors.DalamudYellow);
        ImGui.SameLine(0, 6);
        ImGui.TextColored(connected ? ImGuiColors.HealerGreen : ImGuiColors.DalamudYellow,
            _plugin.Meter.StatusText);
        ImGui.Spacing();

        if (!ImGui.BeginTabBar("##metertabs", ImGuiTabBarFlags.None)) return;
        if (ImGui.BeginTabItem("Display")) { DrawMeterDisplayTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Style")) { DrawMeterStyleTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Themes")) { DrawMeterThemesTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Columns")) { DrawMeterColumnsTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Profiles")) { ImGui.Spacing(); DrawMeterProfiles(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Connection")) { DrawMeterConnectionTab(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }

    // ---- Display: placement and what shows --------------------------------

    private void DrawMeterDisplayTab()
    {
        SeparatorText("Placement");
        C.MeterLocked = CfgCheck("Lock position and size", C.MeterLocked);
        Tip("Unlock, then drag the meter or its edges.");
        ImGui.SameLine(0, 18);
        C.MeterClickThrough = CfgCheck("Click-through", C.MeterClickThrough);
        Tip("Mouse ignores the meter, menu included; turn it back off here.");

        var pos = C.MeterPosition;
        if (Widgets.SliderInput("Horizontal", ref pos.X, 0f, 1f, "%.2f"))
        { C.MeterPosition = pos; C.SaveSettings(); _plugin.MeterWindow.RequestReposition(); }
        ImGui.SameLine(0, 18);
        if (Widgets.SliderInput("Vertical", ref pos.Y, 0f, 1f, "%.2f"))
        { C.MeterPosition = pos; C.SaveSettings(); _plugin.MeterWindow.RequestReposition(); }

        SeparatorText("Show");
        if (ImGui.BeginTable("##metershowgrid", 2))
        {
            C.MeterShowRank = GridCheck("Rank numbers", C.MeterShowRank);
            C.MeterShowJobIcons = GridCheck("Job icons", C.MeterShowJobIcons);
            C.MeterColumnHeader = GridCheck("Column labels", C.MeterColumnHeader);
            C.MeterShowRaidTotal = GridCheck("Raid rDPS total", C.MeterShowRaidTotal);
            C.MeterYou = GridCheck("Call your row \"You\"", C.MeterYou);
            C.MeterHighlightYou = GridCheck("Highlight your row", C.MeterHighlightYou);
            C.MeterButtons = GridCheck("Buttons bar", C.MeterButtons, "Pulls, pause and reset at the bottom.");
            C.MeterHealingTab = GridCheck("DPS / HPS tabs", C.MeterHealingTab, "Right-click a tab to rename it.");
            C.MeterHideOutOfCombat = GridCheck("Hide out of combat", C.MeterHideOutOfCombat);
            C.MeterBreakdownIcons = GridCheck("Breakdown icons", C.MeterBreakdownIcons,
                "Action icons when you click a player.");
            C.MeterBreakdownColors = GridCheck("Color each ability", C.MeterBreakdownColors,
                "Off = the player's job color throughout.");
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(200f);
        var header = C.MeterHeaderStyle;
        if (ImGui.Combo("Header", ref header, "Full\0Slim\0Hidden\0"))
        { C.MeterHeaderStyle = header; C.SaveSettings(); }
        Tip("Double-click the meter's header to cycle.");

        ImGui.SetNextItemWidth(200f);
        var names = C.MeterNameStyle;
        if (ImGui.Combo("Names", ref names, "Full name\0First name\0First name + initial\0"))
        { C.MeterNameStyle = names; C.SaveSettings(); }

        ImGui.SetNextItemWidth(200f);
        var maxRows = C.MeterMaxRows;
        if (ImGui.SliderInt("Rows shown", ref maxRows, 0, 24, maxRows == 0 ? "everyone" : "%d"))
        { C.MeterMaxRows = maxRows; C.SaveSettings(); }
        Tip("Your own row always shows.");

        ImGui.Spacing();
        ImGui.TextDisabled("Turn on Test mode in the header to place it with a sample pull.");
    }

    // ---- Style: bars, text, colors ----------------------------------------

    private void DrawMeterStyleTab()
    {
        SeparatorText("Bars");
        ImGui.SetNextItemWidth(200f);
        var barStyle = C.MeterBarStyle;
        if (ImGui.Combo("Fill", ref barStyle, "Flat\0Glass\0Gradient\0Outline\0Minimal\0"))
        { C.MeterBarStyle = barStyle; C.SaveSettings(); }
        ImGui.SameLine(0, 18);
        C.MeterJobColors = CfgCheck("Color by job", C.MeterJobColors);
        Tip("Off = every bar uses the accent color.");

        var barH = C.MeterBarHeight;
        if (Widgets.SliderInput("Height", ref barH, 16f, 44f, "%.0f px")) { C.MeterBarHeight = barH; C.SaveSettings(); }
        ImGui.SameLine(0, 18);
        var gap = C.MeterBarGap;
        if (Widgets.SliderInput("Spacing", ref gap, 0f, 10f, "%.0f px")) { C.MeterBarGap = gap; C.SaveSettings(); }
        var round = C.MeterRounding;
        if (Widgets.SliderInput("Rounding", ref round, 0f, 14f, "%.0f px")) { C.MeterRounding = round; C.SaveSettings(); }
        ImGui.SameLine(0, 18);
        var barOp = C.MeterBarOpacity;
        if (Widgets.SliderInput("Bar opacity", ref barOp, 0.2f, 1.6f, "%.2f"))
        { C.MeterBarOpacity = barOp; C.SaveSettings(); }

        SeparatorText("Your row");
        ImGui.SetNextItemWidth(200f);
        var hl = C.MeterHighlightStyle;
        if (ImGui.Combo("Highlight", ref hl, "Wash + outline\0Wash\0Outline\0Side stripe\0"))
        { C.MeterHighlightStyle = hl; C.SaveSettings(); }
        ImGui.SameLine(0, 18);
        C.MeterHighlightYou = CfgCheck("Highlight your row", C.MeterHighlightYou);
        var hlStr = C.MeterHighlightStrength;
        if (Widgets.SliderInput("Strength", ref hlStr, 0.2f, 2.5f, "%.2f"))
        { C.MeterHighlightStrength = hlStr; C.SaveSettings(); }

        SeparatorText("Text");
        var fonts = FontManager.FamilyNames;
        var fIdx = Math.Max(0, Array.IndexOf(fonts, C.MeterFontFamily));
        ImGui.SetNextItemWidth(200f);
        if (ImGui.Combo("Font", ref fIdx, fonts, fonts.Length)) { C.MeterFontFamily = fonts[fIdx]; C.SaveSettings(); }
        ImGui.SameLine(0, 12);
        var bold = C.MeterFontBold;
        if (GreenCheckbox("Bold", ref bold)) { C.MeterFontBold = bold; C.SaveSettings(); }
        ImGui.SameLine();
        var italic = C.MeterFontItalic;
        if (GreenCheckbox("Italic", ref italic)) { C.MeterFontItalic = italic; C.SaveSettings(); }
        if (C.MeterFontFamily == "Default" && (C.MeterFontBold || C.MeterFontItalic))
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(pick a font)");
        }
        var px = C.MeterFontSizePx;
        if (Widgets.SliderInput("Size", ref px, 11f, 26f, "%.0f px")) { C.MeterFontSizePx = px; C.SaveSettings(); }
        ImGui.SameLine(0, 18);
        C.MeterTextShadow = CfgCheck("Drop shadow", C.MeterTextShadow);

        SeparatorText("Colors");
        if (ImGui.BeginTable("##metercolorgrid", 4))
        {
            ImGui.TableNextColumn();
            MeterColor("Text", () => C.MeterTextColor, v => C.MeterTextColor = v);
            ImGui.TableNextColumn();
            MeterColor("Details", () => C.MeterSubColor, v => C.MeterSubColor = v);
            Tip("Ranks, labels, secondary columns.");
            ImGui.TableNextColumn();
            MeterColor("Accent", () => C.MeterAccentColor, v => C.MeterAccentColor = v);
            Tip("Totals, and bars when job colors are off.");
            ImGui.TableNextColumn();
            MeterColor("Title", () => C.MeterTitleColor, v => C.MeterTitleColor = v);
            Tip("The encounter name.");
            ImGui.TableNextColumn();
            MeterColor("You", () => C.MeterYouColor, v => C.MeterYouColor = v);
            Tip("Your name in the list.");
            ImGui.TableNextColumn();
            MeterColor("Highlight", () => C.MeterHighlightColor, v => C.MeterHighlightColor = v);
            Tip("The wash over your row.");
            ImGui.TableNextColumn();
            MeterColor("Timer", () => C.MeterTimerColor, v => C.MeterTimerColor = v);
            ImGui.TableNextColumn();
            MeterColor("Border", () => C.MeterBorderColor, v => C.MeterBorderColor = v);
            Tip("Alpha to zero hides it.");
            ImGui.TableNextColumn();
            MeterColor("Background", () => C.MeterBgColor, v => C.MeterBgColor = v);
            ImGui.TableNextColumn();
            MeterColor("Rows", () => C.MeterRowColor, v => C.MeterRowColor = v);
            ImGui.TableNextColumn();
            if (ImGui.SmallButton("Reset colors"))
            {
                MeterWindow.ApplyTheme(C, MeterWindow.Themes[0]);
                C.MeterBarStyle = 0;
                C.SaveSettings();
            }
            ImGui.EndTable();
        }
    }

    // ---- Themes ------------------------------------------------------------

    private void DrawMeterThemesTab()
    {
        ImGui.Spacing();
        var i = 0;
        foreach (var t in MeterWindow.Themes)
        {
            if (i++ % 4 != 0) ImGui.SameLine(0, 10);
            ImGui.BeginGroup();
            ImGui.ColorButton($"##sw_{t.Name}", ColorToVec4(t.Accent),
                ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop);
            ImGui.SameLine(0, 5);
            if (ImGui.Button($"{t.Name}##theme", new System.Numerics.Vector2(112f, 0f)))
                MeterWindow.ApplyTheme(C, t);
            ImGui.EndGroup();
        }
        ImGui.Spacing();
        ImGui.TextDisabled("A theme sets the colors and bar look; tweak anything after in Style.");
    }

    // ---- Columns -----------------------------------------------------------

    private void DrawMeterColumnsTab()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Drag the labels on the meter to reorder, or use the arrows.");
        ImGui.Spacing();
        ImGui.TextColored(new System.Numerics.Vector4(0.55f, 0.75f, 0.98f, 1f), "DAMAGE VIEW");
        foreach (var key in AllMeterColumnKeys)
            DrawColumnRow(key, C.MeterColumns, "d");
        ImGui.Spacing();
        ImGui.TextColored(new System.Numerics.Vector4(0.55f, 0.75f, 0.98f, 1f), "HEALING VIEW");
        foreach (var key in AllMeterColumnKeys)
            DrawColumnRow(key, C.MeterHealColumns, "h");
    }

    // ---- Connection --------------------------------------------------------

    private void DrawMeterConnectionTab()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(240f);
        var conn = C.MeterConnection;
        if (ImGui.Combo("Source", ref conn, "Auto\0Parser plugin\0ACT WebSocket\0"))
        { C.MeterConnection = conn; C.SaveSettings(); ReconnectMeter(); }
        Tip("Auto tries the parser plugin, then ACT.");

        if (C.MeterConnection != 1)
        {
            ImGui.SetNextItemWidth(300f);
            var addr = C.MeterSocketAddress;
            if (ImGui.InputText("WebSocket address", ref addr, 128))
            { C.MeterSocketAddress = addr; C.SaveSettings(); }
        }

        ImGui.Spacing();
        if (ImGui.Button("Reconnect")) ReconnectMeter();
    }

    // ---- Profiles ----------------------------------------------------------

    private void DrawMeterProfiles()
    {
        var active = C.MeterProfileName;
        var saved = active.Length > 0 && C.MeterProfiles.ContainsKey(active);

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Profile");
        ImGui.SameLine(0, 8);
        ImGui.SetNextItemWidth(220f);
        if (ImGui.BeginCombo("##mprofsel", saved ? active : "(unsaved)"))
        {
            foreach (var kv in C.MeterProfiles)
                if (ImGui.Selectable(kv.Key, kv.Key == active))
                    ApplyMeterProfile(kv.Key);
            ImGui.EndCombo();
        }

        if (saved)
        {
            ImGui.SameLine(0, 8);
            ImGui.TextDisabled("changes save into it automatically");
            ImGui.SameLine(0, 8);
            // Two-click delete so one stray click can't eat a profile.
            if ((DateTime.Now - _meterDeleteAt).TotalSeconds < 3)
            {
                if (ImGui.SmallButton("Sure?"))
                {
                    C.MeterProfiles.Remove(active);
                    C.MeterProfileName = "";
                    C.SaveSettings();
                    MeterFlash("Profile deleted.");
                }
            }
            else if (ImGui.SmallButton("Delete")) _meterDeleteAt = DateTime.Now;

            if (_meterRenameFor != active) { _meterRenameFor = active; _meterRenameBuf = active; }
            ImGui.SetNextItemWidth(180f);
            ImGui.InputText("##mprofrename", ref _meterRenameBuf, 48);
            ImGui.SameLine(0, 6);
            if (ImGui.SmallButton("Rename"))
            {
                var name = _meterRenameBuf.Trim();
                if (name.Length == 0 || (C.MeterProfiles.ContainsKey(name) && name != active))
                    MeterFlash("That name is taken or empty.", ok: false);
                else if (name != active)
                {
                    C.MeterProfiles[name] = C.MeterProfiles[active];
                    C.MeterProfiles.Remove(active);
                    C.MeterProfileName = name;
                    C.SaveSettings();
                    MeterFlash("Profile renamed.");
                }
            }
        }

        ImGui.SetNextItemWidth(180f);
        ImGui.InputTextWithHint("##mprofnew", "new profile name", ref _meterNameBuf, 48);
        ImGui.SameLine(0, 6);
        if (ImGui.Button("Save as profile"))
        {
            var name = _meterNameBuf.Trim();
            if (name.Length == 0) MeterFlash("Give the profile a name first.", ok: false);
            else
            {
                C.MeterProfiles[name] = MeterProfile.Export(C);
                C.MeterProfileName = name;
                C.SaveSettings();
                _meterNameBuf = "";
                MeterFlash($"Saved as \"{name}\".");
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled("Share codes carry the whole layout and look.");
        if (ImGui.Button("Copy share code"))
        {
            ImGui.SetClipboardText(MeterProfile.Export(C));
            MeterFlash("Code copied to clipboard.");
        }
        ImGui.SameLine(0, 10);
        if (ImGui.Button("Import from clipboard"))
            ImportMeterProfile(ImGui.GetClipboardText());

        ImGui.SetNextItemWidth(320f);
        ImGui.InputText("##mprofilecode", ref _meterProfileBuf, 4096);
        ImGui.SameLine(0, 6);
        if (ImGui.Button("Import")) ImportMeterProfile(_meterProfileBuf);

        if (_meterFlash.Length > 0 && (DateTime.Now - _meterFlashAt).TotalSeconds < 4)
            ImGui.TextColored(_meterFlashOk ? ImGuiColors.HealerGreen : ImGuiColors.DalamudYellow, _meterFlash);
    }

    private void ApplyMeterProfile(string name)
    {
        if (!C.MeterProfiles.TryGetValue(name, out var code)) return;
        if (MeterProfile.Import(C, code))
        {
            C.MeterProfileName = name;
            C.SaveSettings();
            _plugin.MeterWindow.RequestReposition();
            MeterFlash($"Profile \"{name}\" applied.");
        }
        else
            MeterFlash("That profile could not be read.", ok: false);
    }

    private string _meterProfileBuf = "";
    private string _meterNameBuf = "";
    private string _meterRenameBuf = "";
    private string _meterRenameFor = "";
    private DateTime _meterDeleteAt = DateTime.MinValue;
    private string _meterFlash = "";
    private bool _meterFlashOk = true;
    private DateTime _meterFlashAt = DateTime.MinValue;

    private void MeterFlash(string text, bool ok = true)
    {
        _meterFlash = text;
        _meterFlashOk = ok;
        _meterFlashAt = DateTime.Now;
    }

    private void ImportMeterProfile(string code)
    {
        if (MeterProfile.Import(C, code ?? ""))
        {
            C.MeterProfileName = ""; // an imported look starts unsaved
            C.SaveSettings();
            _plugin.MeterWindow.RequestReposition();
            _meterProfileBuf = "";
            MeterFlash("Imported. Use \"Save as profile\" to keep it.");
        }
        else
            MeterFlash("That code didn't read as a meter profile.", ok: false);
    }

    private static readonly string[] AllMeterColumnKeys =
        { "rdps", "dps", "dmgpct", "crit", "dh", "hps", "healed", "overheal", "taken", "deaths" };

    private void DrawColumnRow(string key, List<string> list, string view)
    {
        var label = MeterWindow.ColumnLabel(key);
        var idx = list.IndexOf(key);
        var on = idx >= 0;
        if (GreenCheckbox($"##mcol_{view}_{key}", ref on))
        {
            if (on) list.Add(key);
            else list.Remove(key);
            C.SaveSettings();
        }
        ImGui.SameLine(0, 8);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        if (idx >= 0)
        {
            ImGui.SameLine(210f);
            ImGui.PushID($"{view}_{key}");
            if (ImGui.ArrowButton("up", ImGuiDir.Up) && idx > 0)
            {
                (list[idx - 1], list[idx]) = (list[idx], list[idx - 1]);
                C.SaveSettings();
            }
            ImGui.SameLine(0, 4);
            if (ImGui.ArrowButton("down", ImGuiDir.Down) && idx < list.Count - 1)
            {
                (list[idx + 1], list[idx]) = (list[idx], list[idx + 1]);
                C.SaveSettings();
            }
            ImGui.PopID();
        }
    }

    private void MeterColor(string label, Func<uint> get, Action<uint> set)
    {
        var col = ColorToVec4(get());
        if (ImGui.ColorEdit4($"{label}##meter", ref col, ImGuiColorEditFlags.NoInputs))
        { set(Vec4ToColor(col)); C.SaveSettings(); }
    }

    // Drop the link; the next framework tick reconnects with the fresh settings.
    private void ReconnectMeter() => _plugin.Meter.Link.RetryNow();
}
