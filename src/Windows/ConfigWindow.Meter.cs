using System;
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

        if (!ImGui.BeginTabBar("##metertabs", ImGuiTabBarFlags.None)) return;

        if (ImGui.BeginTabItem("Connection"))
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
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Display"))
        {
            ImGui.Spacing();
            C.MeterLocked = CfgCheck("Lock position and size", C.MeterLocked);
            C.MeterClickThrough = CfgCheck("Click-through", C.MeterClickThrough);
            Tip("Also disables the right-click menu; turn it back off here.");

            var pos = C.MeterPosition;
            if (Widgets.SliderInput("Horizontal", ref pos.X, 0f, 1f, "%.2f"))
            { C.MeterPosition = pos; C.SaveSettings(); _plugin.MeterWindow.RequestReposition(); }
            ImGui.SameLine(0, 18);
            if (Widgets.SliderInput("Vertical", ref pos.Y, 0f, 1f, "%.2f"))
            { C.MeterPosition = pos; C.SaveSettings(); _plugin.MeterWindow.RequestReposition(); }

            var barH = C.MeterBarHeight;
            if (Widgets.SliderInput("Bar height", ref barH, 16f, 44f, "%.0f px")) { C.MeterBarHeight = barH; C.SaveSettings(); }
            var gap = C.MeterBarGap;
            if (Widgets.SliderInput("Bar spacing", ref gap, 0f, 10f, "%.0f px")) { C.MeterBarGap = gap; C.SaveSettings(); }
            var round = C.MeterRounding;
            if (Widgets.SliderInput("Rounding", ref round, 0f, 14f, "%.0f px")) { C.MeterRounding = round; C.SaveSettings(); }

            ImGui.Spacing();
            if (ImGui.BeginTable("##meterdisplaygrid", 2))
            {
                C.MeterShowRank = GridCheck("Rank numbers", C.MeterShowRank);
                C.MeterShowJobIcons = GridCheck("Job icons", C.MeterShowJobIcons);
                C.MeterJobColors = GridCheck("Color bars by job", C.MeterJobColors);
                C.MeterColumnHeader = GridCheck("Column labels", C.MeterColumnHeader);
                C.MeterShowRaidTotal = GridCheck("Raid rDPS total", C.MeterShowRaidTotal);
                C.MeterYou = GridCheck("Call your row \"You\"", C.MeterYou);
                C.MeterHighlightYou = GridCheck("Highlight your row", C.MeterHighlightYou);
                C.MeterButtons = GridCheck("Buttons bar", C.MeterButtons, "Pulls, pause and reset along the bottom.");
                C.MeterHealingTab = GridCheck("Healing tab", C.MeterHealingTab,
                    "Damage / Healing tabs at the bottom of the meter.");
                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.SetNextItemWidth(220f);
            var barStyle = C.MeterBarStyle;
            if (ImGui.Combo("Bars", ref barStyle, "Flat\0Glass\0Gradient\0"))
            { C.MeterBarStyle = barStyle; C.SaveSettings(); }

            ImGui.SetNextItemWidth(220f);
            var header = C.MeterHeaderStyle;
            if (ImGui.Combo("Header", ref header, "Full\0Slim\0Hidden\0"))
            { C.MeterHeaderStyle = header; C.SaveSettings(); }
            Tip("Double-click the meter's header to cycle.");

            ImGui.SetNextItemWidth(220f);
            var names = C.MeterNameStyle;
            if (ImGui.Combo("Names", ref names, "Full name\0First name\0First name + initial\0"))
            { C.MeterNameStyle = names; C.SaveSettings(); }

            ImGui.Spacing();
            MeterColor("Text", () => C.MeterTextColor, v => C.MeterTextColor = v);
            ImGui.SameLine(0, 14);
            MeterColor("Details", () => C.MeterSubColor, v => C.MeterSubColor = v);
            ImGui.SameLine(0, 14);
            MeterColor("Accent", () => C.MeterAccentColor, v => C.MeterAccentColor = v);
            Tip("Totals, and bars when job colors are off.");
            MeterColor("You", () => C.MeterYouColor, v => C.MeterYouColor = v);
            Tip("Your name and row highlight.");
            ImGui.SameLine(0, 14);
            MeterColor("Timer", () => C.MeterTimerColor, v => C.MeterTimerColor = v);
            ImGui.SameLine(0, 14);
            MeterColor("Background", () => C.MeterBgColor, v => C.MeterBgColor = v);
            MeterColor("Rows", () => C.MeterRowColor, v => C.MeterRowColor = v);
            ImGui.SameLine(0, 14);
            if (ImGui.SmallButton("Reset colors"))
            {
                C.MeterTextColor = 0xFFFFFFFF;
                C.MeterSubColor = 0xFFFFFFFF;
                C.MeterAccentColor = 0xFFF6823B;
                C.MeterYouColor = 0xFFF6823B;
                C.MeterTimerColor = 0xFFFFFFFF;
                C.MeterBgColor = 0xB80D0A09;
                C.MeterRowColor = 0x17FFFFFF;
                C.SaveSettings();
            }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Font"))
        {
            ImGui.Spacing();
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
            if (Widgets.SliderInput("Text size", ref px, 11f, 26f, "%.0f px")) { C.MeterFontSizePx = px; C.SaveSettings(); }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Themes"))
        {
            ImGui.Spacing();
            var i = 0;
            foreach (var t in MeterWindow.Themes)
            {
                if (i++ % 3 != 0) ImGui.SameLine(0, 10);
                ImGui.ColorButton($"##sw_{t.Name}", ColorToVec4(t.Accent),
                    ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop);
                ImGui.SameLine(0, 5);
                if (ImGui.Button(t.Name)) MeterWindow.ApplyTheme(C, t);
            }
            ImGui.Spacing();
            ImGui.TextDisabled("A theme sets the colors and rounding; tweak anything after in Display.");
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Columns"))
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Drag the labels on the meter to reorder, or use the arrows.");
            ImGui.Spacing();
            foreach (var key in new[] { "rdps", "dps", "dmgpct", "crit", "dh", "hps", "overheal", "taken", "deaths" })
                DrawColumnRow(key);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Profiles"))
        {
            ImGui.Spacing();
            DrawMeterProfiles();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

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

    private void MeterColor(string label, Func<uint> get, Action<uint> set)
    {
        var col = ColorToVec4(get());
        if (ImGui.ColorEdit4($"{label}##meter", ref col, ImGuiColorEditFlags.NoInputs))
        { set(Vec4ToColor(col)); C.SaveSettings(); }
    }

    // Drop the link; the next framework tick reconnects with the fresh settings.
    private void ReconnectMeter() => _plugin.Meter.Link.RetryNow();
}
