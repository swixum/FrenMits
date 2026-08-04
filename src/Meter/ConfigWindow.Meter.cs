using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;

namespace FrenMits.Host;

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
        if (ImGui.BeginTabItem("Profiles")) { DrawMeterProfiles(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Connection")) { DrawMeterConnectionTab(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }

    // ---- Display ----

    private void DrawMeterDisplayTab()
    {
        SeparatorText("Placement");
        C.MeterLocked = CfgCheck("Lock position and size", C.MeterLocked);
        Tip("Unlock, then drag the meter or its edges.");
        ImGui.SameLine(0, 18);
        C.MeterClickThrough = CfgCheck("Click-through", C.MeterClickThrough);
        Tip("Mouse ignores the meter, menu included; turn it back off here.");
        ImGui.SameLine(0, 18);
        C.MeterCollapsed = CfgCheck("Collapsed", C.MeterCollapsed);
        Tip("Rolled up to its header; the chevron on the meter does this too.");

        var pos = C.MeterPosition;
        if (Widgets.SliderInput("Horizontal", ref pos.X, 0f, 1f, "%.2f"))
        { C.MeterPosition = pos; C.SaveSettings(); _plugin.MeterWindow.RequestReposition(); }
        ImGui.SameLine(0, 18);
        if (Widgets.SliderInput("Vertical", ref pos.Y, 0f, 1f, "%.2f"))
        { C.MeterPosition = pos; C.SaveSettings(); _plugin.MeterWindow.RequestReposition(); }

        SeparatorText("Rows");
        if (ImGui.BeginTable("##meterrowgrid", 2))
        {
            C.MeterShowRank = GridCheck("Rank numbers", C.MeterShowRank);
            C.MeterShowJobIcons = GridCheck("Job icons", C.MeterShowJobIcons);
            C.MeterColumnHeader = GridCheck("Column labels", C.MeterColumnHeader);
            C.MeterYou = GridCheck("Call your row \"You\"", C.MeterYou);
            C.MeterLimitBreakRow = GridCheck("Limit break row", C.MeterLimitBreakRow,
                "A short row under the party.");
            C.MeterSplitHealing = GridCheck("Split DPS/HPS", C.MeterSplitHealing,
                "DPS on top, healer HPS below.");
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(200f);
        var names = C.MeterNameStyle;
        if (ImGui.Combo("Names", ref names, "Full name\0First name\0First name + initial\0"))
        { C.MeterNameStyle = names; C.SaveSettings(); }

        var maxRows = C.MeterMaxRows;
        if (Widgets.SliderInput("Rows shown", ref maxRows, 0, 24, maxRows == 0 ? "everyone" : "%d"))
        { C.MeterMaxRows = maxRows; C.SaveSettings(); }
        Tip("Your own row always shows.");

        var refresh = C.MeterRefreshSeconds;
        if (Widgets.SliderInput("Number refresh", ref refresh, 0f, 3f,
                refresh <= 0f ? "every frame" : "%.1f s"))
        { C.MeterRefreshSeconds = refresh; C.SaveSettings(); }
        Tip("How long the numbers hold still. Bars keep moving.");

        SeparatorText("Header and footer");
        ImGui.SetNextItemWidth(200f);
        var header = C.MeterHeaderStyle;
        if (ImGui.Combo("Header", ref header, "Full\0Slim\0Hidden\0"))
        { C.MeterHeaderStyle = header; C.SaveSettings(); }
        Tip("Double-click the meter's header to cycle.");
        ImGui.Spacing();
        if (ImGui.BeginTable("##meterchromegrid", 2))
        {
            C.MeterShowRaidTotal = GridCheck("Raid rDPS total", C.MeterShowRaidTotal);
            C.MeterHealingTab = GridCheck("DPS / HPS tabs", C.MeterHealingTab, "Right-click a tab to rename it.");
            C.MeterButtons = GridCheck("Buttons bar", C.MeterButtons, "History, pause and reset at the bottom.");
            C.MeterFooterDeaths = GridCheck("Death count", C.MeterFooterDeaths,
                "The pull's deaths in the footer; hover it for who.");
            ImGui.EndTable();
        }

        SeparatorText("When to show");
        if (ImGui.BeginTable("##metervisgrid", 2))
        {
            C.MeterAlwaysShow = GridCheck("Always on screen", C.MeterAlwaysShow,
                "Stays put with no pull to show, so a reset cannot hide it.");
            C.MeterHideOutOfCombat = GridCheck("Hide out of combat", C.MeterHideOutOfCombat);
            ImGui.EndTable();
        }

        SeparatorText("Breakdown");
        if (ImGui.BeginTable("##meterbreakgrid", 2))
        {
            C.MeterBreakdownIcons = GridCheck("Action icons", C.MeterBreakdownIcons,
                "Icons beside each ability when you click a player.");
            C.MeterBreakdownColors = GridCheck("Color each ability", C.MeterBreakdownColors,
                "Off = the player's job color throughout.");
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Turn on Test mode in the header to place it with a sample pull.");
    }

    // ---- Style ----

    private void DrawMeterStyleTab()
    {
        SeparatorText("Bars");
        ImGui.SetNextItemWidth(200f);
        var barStyle = C.MeterBarStyle;
        if (ImGui.Combo("Fill", ref barStyle, "Flat\0Glass\0Gradient\0Outline\0Minimal\0"))
        { C.MeterBarStyle = barStyle; C.SaveSettings(); }

        C.MeterJobColors = CfgCheck("Color by job", C.MeterJobColors);
        Tip("Off = every bar uses the accent color.");
        ImGui.SameLine(0, 18);
        C.MeterBarSolid = CfgCheck("Solid bars", C.MeterBarSolid);
        Tip("Fill bars with the job color instead of a wash you can see through.");

        var barH = C.MeterBarHeight;
        if (Widgets.SliderInput("Height", ref barH, 16f, 44f, "%.0f px")) { C.MeterBarHeight = barH; C.SaveSettings(); }
        ImGui.SameLine(0, 18);
        var gap = C.MeterBarGap;
        if (Widgets.SliderInput("Spacing", ref gap, 0f, 10f, "%.0f px")) { C.MeterBarGap = gap; C.SaveSettings(); }
        var round = C.MeterRounding;
        if (Widgets.SliderInput("Rounding", ref round, 0f, 14f, "%.0f px")) { C.MeterRounding = round; C.SaveSettings(); }
        ImGui.SameLine(0, 18);
        var barOp = C.MeterBarOpacity;
        if (Widgets.SliderInput("Opacity", ref barOp, 0.2f, 1.6f, "%.2f"))
        { C.MeterBarOpacity = barOp; C.SaveSettings(); }

        SeparatorText("Your row");
        C.MeterHighlightYou = CfgCheck("Highlight your row", C.MeterHighlightYou);
        ImGui.BeginDisabled(!C.MeterHighlightYou);
        ImGui.SetNextItemWidth(200f);
        var hl = C.MeterHighlightStyle;
        if (ImGui.Combo("Highlight", ref hl, "Wash + outline\0Wash\0Outline\0Side stripe\0"))
        { C.MeterHighlightStyle = hl; C.SaveSettings(); }
        var hlStr = C.MeterHighlightStrength;
        if (Widgets.SliderInput("Strength", ref hlStr, 0.2f, 2.5f, "%.2f"))
        { C.MeterHighlightStrength = hlStr; C.SaveSettings(); }
        ImGui.EndDisabled();

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
            ImGui.EndTable();
        }

        ImGui.Spacing();
        if (ImGui.SmallButton("Reset colors"))
        {
            MeterWindow.ApplyTheme(C, MeterWindow.Themes[0]);
            C.MeterBarStyle = 0;
            C.SaveSettings();
        }
        Tip("Back to the first theme's colors and a flat bar.");
    }

    // ---- Themes ----

    private void DrawMeterThemesTab()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("A theme sets the colors and bar look; tweak anything after in Style.");
        ImGui.Spacing();

        var size = new Vector2(152f, ImGui.GetFrameHeight() + 4f);
        var i = 0;
        foreach (var t in MeterWindow.Themes)
        {
            if (i++ % 3 != 0) ImGui.SameLine(0, 8);
            DrawThemeButton(t, size);
        }
    }

    // A theme's own accent on the button, and a ring around the one in use.
    private void DrawThemeButton(MeterWindow.MeterTheme t, Vector2 size)
    {
        var live = C.MeterAccentColor == t.Accent && C.MeterBgColor == t.Bg
            && C.MeterRowColor == t.Rows && C.MeterBarStyle == t.BarStyle;
        var p = ImGui.GetCursorScreenPos();

        if (live) ImGui.PushStyleColor(ImGuiCol.Button, 0xFF34271F);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0f, 0.5f));
        if (ImGui.Button($"       {t.Name}##theme", size)) MeterWindow.ApplyTheme(C, t);
        ImGui.PopStyleVar();
        if (live) ImGui.PopStyleColor();

        var dl = ImGui.GetWindowDrawList();
        var mid = p.Y + size.Y * 0.5f;
        dl.AddRectFilled(new Vector2(p.X + 9f, mid - 7f), new Vector2(p.X + 22f, mid + 7f), t.Accent, 3f);
        if (live) dl.AddRect(p, p + size, Theme.Accent, 5f, ImDrawFlags.None, 1.5f);
    }

    // ---- Columns ----

    private void DrawMeterColumnsTab()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Top here is leftmost on the meter; dragging its labels reorders too.");
        ImGui.Spacing();
        // Both sides pad to the longer list, so the two boxes sit level.
        var rows = Math.Max(Math.Max(C.MeterColumns.Count, C.MeterHealColumns.Count), 1);
        if (ImGui.BeginTable("##metercols", 2, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            DrawColumnList("Damage view", C.MeterColumns, "d", rows);
            ImGui.TableNextColumn();
            DrawColumnList("Healing view", C.MeterHealColumns, "h", rows);
            ImGui.EndTable();
        }
        ImGui.Spacing();
        ImGui.TextDisabled("Damage taken and Deaths reuse the damage list, each with its own number in front.");
    }

    // ---- Connection ----

    private void DrawMeterConnectionTab()
    {
        // Nothing to explain to someone already connected, but once it's up it stays
        // up for the session, so following the steps ends in a green line, not a blank.
        if (!C.MeterSetupDone && (_setupCard || !_plugin.Meter.Connected))
        {
            _setupCard = true;
            DrawMeterSetupCard();
        }

        SeparatorText("Parser");
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
            // Read before the tooltip, which leaves its own text as the last item.
            var addressEdited = ImGui.IsItemDeactivatedAfterEdit();
            Tip("Wherever the parser listens, e.g. wss://127.0.0.1:10501/ws for SSL,\n"
                + "or ws://192.168.1.50:10501/ws for ACT on another PC.\n"
                + "Both ws:// and wss:// are tried, so ACT's SSL setting can stay as it is.");
            // A new address takes effect without hunting for the button.
            if (addressEdited) ReconnectMeter();
            DrawLinkState();
        }

        ImGui.Spacing();
        if (ImGui.Button("Reconnect")) ReconnectMeter();
        Tip("Drops the link and picks it up again.");

        if (C.MeterSetupDone)
        {
            ImGui.SameLine(0, 10);
            if (ImGui.Button("ACT steps"))
            {
                C.MeterSetupDone = false;
                _setupCard = true;
                C.SaveSettings();
            }
            Tip("Show the three setup steps again.");
        }
    }

    // True once the card has shown this session, so a late connect doesn't yank it away.
    private bool _setupCard;

    // Three steps, up until it's working and read.
    private void DrawMeterSetupCard()
    {
        var connected = _plugin.Meter.Connected;
        var st = ImGui.GetStyle();
        var h = ImGui.GetTextLineHeightWithSpacing() * 6 + st.ItemSpacing.Y * 4
            + st.WindowPadding.Y * 2 + 4f;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.PanelBg);
        if (ImGui.BeginChild("##metersetup", new Vector2(0, h), true, ImGuiWindowFlags.NoScrollbar))
        {
            var dl = ImGui.GetWindowDrawList();
            var wp = ImGui.GetWindowPos();
            dl.AddRectFilled(wp, wp + new Vector2(3, ImGui.GetWindowHeight()), Theme.Accent);

            ImGui.TextUnformatted("First time? Set ACT up like this.");
            ImGui.Spacing();
            SetupStep(1, "Run ACT, with its FFXIV plugin.");
            SetupStep(2, "Plugins > OverlayPlugin.dll > WSServer > Start.");
            SetupStep(3, "Options > Main Table/Encounters > Idle Limit: 180.");
            Tip("Lower than that splits a fight at its own downtime.");

            ImGui.Spacing();
            if (connected) ImGui.TextColored(Theme.V(Theme.Good), "Connected. Nothing else to do.");
            else ImGui.TextDisabled("Leave ACT running; this finds it on its own.");

            ImGui.SameLine(0, 12);
            if (ImGui.SmallButton("Got it")) { C.MeterSetupDone = true; C.SaveSettings(); }
            ImGui.TextDisabled("On IINACT instead? Nothing to set up.");
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // A numbered line, the number in the accent color.
    private static void SetupStep(int n, string text)
    {
        ImGui.TextColored(Theme.V(Theme.Accent), $"{n}");
        ImGui.SameLine(0, 10);
        ImGui.TextUnformatted(text);
    }

    // What the socket is doing, since "searching" alone explains nothing.
    private void DrawLinkState()
    {
        var link = _plugin.Meter.Link;
        if (link.Status == MeterLink.LinkStatus.Socket && link.ActiveAddress.Length > 0)
        {
            ImGui.TextColored(Theme.V(Theme.Good), $"Connected on {link.ActiveAddress}");
            return;
        }
        if (link.LastError.Length == 0) return;
        ImGui.TextColored(Theme.V(Theme.Warn), "Last attempt failed");
        ImGui.PushTextWrapPos(0f);
        ImGui.TextDisabled(link.LastError);
        ImGui.PopTextWrapPos();
    }

    // ---- Profiles ----

    private void DrawMeterProfiles()
    {
        var active = C.MeterProfileName;
        var saved = active.Length > 0 && C.MeterProfiles.ContainsKey(active);

        SeparatorText("Saved looks");
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
            ImGui.InputTextWithHint("##mprofrename", "rename this one", ref _meterRenameBuf, 48);
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

        SeparatorText("Share");
        ImGui.TextDisabled("A code carries the whole layout and look.");
        ImGui.Spacing();
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

    // One view's columns, in the order they sit on the meter.
    private void DrawColumnList(string title, List<string> list, string view, int rows)
    {
        ImGui.PushID($"mcol_{view}");
        SeparatorText(title);

        // Edits land after the loop, so the rows drawn this frame stay put.
        var move = -1;
        var dir = 0;
        var drop = -1;

        for (var i = 0; i < list.Count; i++)
        {
            ImGui.PushID(i);
            var on = true;
            if (GreenCheckbox("##on", ref on)) drop = i;
            Tip("Take this column off.");

            ImGui.SameLine(0, 8);
            ImGui.BeginDisabled(i == 0);
            if (ImGui.ArrowButton("up", ImGuiDir.Up)) { move = i; dir = -1; }
            ImGui.EndDisabled();
            ImGui.SameLine(0, 3);
            ImGui.BeginDisabled(i == list.Count - 1);
            if (ImGui.ArrowButton("down", ImGuiDir.Down)) { move = i; dir = 1; }
            ImGui.EndDisabled();

            ImGui.SameLine(0, 8);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(MeterWindow.ColumnLabel(list[i]));
            ImGui.PopID();
        }

        if (list.Count == 0) ImGui.TextDisabled("No numbers, just bars.");

        if (move >= 0)
        {
            var j = move + dir;
            (list[j], list[move]) = (list[move], list[j]);
            C.SaveSettings();
        }
        else if (drop >= 0)
        {
            list.RemoveAt(drop);
            C.SaveSettings();
        }

        var drawn = Math.Max(list.Count, 1);
        if (drawn < rows)
            ImGui.Dummy(new Vector2(0f,
                (rows - drawn) * (ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y)));

        DrawColumnBox(list);
        ImGui.PopID();
    }

    // The rest of the columns, boxed so a long list can't take over the tab.
    private void DrawColumnBox(List<string> list)
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Not shown");

        var h = ImGui.GetTextLineHeightWithSpacing() * 5 + ImGui.GetStyle().WindowPadding.Y * 2;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.PanelBg);
        if (ImGui.BeginChild("##offcols", new Vector2(0, h), true))
        {
            var left = 0;
            foreach (var key in MeterWindow.ColumnKeys)
            {
                if (list.Contains(key)) continue;
                left++;
                var on = false;
                ImGui.PushID(key);
                // Checked here means "add it", so it lands at the end of the order.
                if (GreenCheckbox("##off", ref on)) { list.Add(key); C.SaveSettings(); }
                ImGui.SameLine(0, 8);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(MeterWindow.ColumnLabel(key));
                ImGui.PopID();
            }
            if (left == 0) ImGui.TextDisabled("Every column is in.");
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void MeterColor(string label, Func<uint> get, Action<uint> set)
    {
        var col = ColorToVec4(get());
        if (ImGui.ColorEdit4($"{label}##meter", ref col, ImGuiColorEditFlags.NoInputs))
        { set(Vec4ToColor(col)); C.SaveSettings(); }
    }

    // Drop the link; the next tick reconnects with new settings.
    private void ReconnectMeter() => _plugin.Meter.Link.RetryNow();
}
