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
        ImGui.TextWrapped("A damage meter fed by ACT or IINACT, with rDPS computed from the combat log.");
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
        if (TabItem("Display")) { DrawMeterDisplayTab(); ImGui.EndTabItem(); }
        if (TabItem("Style")) { DrawMeterStyleTab(); ImGui.EndTabItem(); }
        if (TabItem("Themes")) { DrawMeterThemesTab(); ImGui.EndTabItem(); }
        if (TabItem("Columns")) { DrawMeterColumnsTab(); ImGui.EndTabItem(); }
        if (TabItem("Profiles")) { DrawMeterProfiles(); ImGui.EndTabItem(); }
        if (TabItem("Connection")) { DrawMeterConnectionTab(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }

    // ---- Display ----

    private void DrawMeterDisplayTab()
    {
        ImGui.Spacing();
        DrawMeterCard(new MeterWindow.SampleView { Rows = 3, Chrome = true });
        ImGui.TextDisabled("Every switch below lands here.");

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
        if (ImGui.BeginTable("##meterrowgrid", GridCols()))
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
        if (ImGui.BeginTable("##meterchromegrid", GridCols()))
        {
            C.MeterShowRaidTotal = GridCheck("Raid rDPS total", C.MeterShowRaidTotal);
            C.MeterHealingTab = GridCheck("DPS / HPS tabs", C.MeterHealingTab, "Right-click a tab to rename it.");
            C.MeterButtons = GridCheck("Buttons bar", C.MeterButtons, "History, pause and reset at the bottom.");
            C.MeterFooterDeaths = GridCheck("Death count", C.MeterFooterDeaths,
                "The pull's deaths in the footer; hover it for who.");
            ImGui.EndTable();
        }

        SeparatorText("When to show");
        if (ImGui.BeginTable("##metervisgrid", GridCols()))
        {
            C.MeterAlwaysShow = GridCheck("Always on screen", C.MeterAlwaysShow,
                "Stays put with no pull to show, so a reset cannot hide it.");
            C.MeterHideOutOfCombat = GridCheck("Hide out of combat", C.MeterHideOutOfCombat);
            ImGui.EndTable();
        }

        SeparatorText("Breakdown");
        if (ImGui.BeginTable("##meterbreakgrid", GridCols()))
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
        ImGui.Spacing();
        DrawMeterCard(new MeterWindow.SampleView { Rows = 2 });
        ImGui.TextDisabled("Row one counts as yours, so the highlight always shows.");

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
            ImGui.BeginDisabled(C.OverlaysFollowAccent);
            MeterColor("Accent", () => C.MeterAccentColor, v => C.MeterAccentColor = v);
            ImGui.EndDisabled();
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
        ImGui.TextDisabled(_themePeek is { } peek
            ? $"Showing {peek.Name}; let go and it goes back."
            : "Hover a theme to try it, click to keep it.");
        ImGui.Spacing();
        DrawMeterCard(new MeterWindow.SampleView { Rows = 2, Theme = _themePeek });
        ImGui.Spacing();

        // Cleared here, then set again by whichever button is hovered below.
        _themePeek = null;
        var size = new Vector2(152f, ImGui.GetFrameHeight() + 4f);
        var i = 0;
        foreach (var t in MeterWindow.Themes)
        {
            if (i++ % 3 != 0) ImGui.SameLine(0, 8);
            DrawThemeButton(t, size);
        }
    }

    // The theme under the cursor, drawn in the card until the cursor leaves.
    private MeterWindow.MeterTheme? _themePeek;

    // A theme's own accent on the button, and a ring around the one in use.
    private void DrawThemeButton(MeterWindow.MeterTheme t, Vector2 size)
    {
        var live = C.MeterAccentColor == t.Accent && C.MeterBgColor == t.Bg
            && C.MeterRowColor == t.Rows && C.MeterBarStyle == t.BarStyle;
        var p = ImGui.GetCursorScreenPos();

        if (live) ImGui.PushStyleColor(ImGuiCol.Button, 0xFF34271F);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0f, 0.5f));
        if (ImGui.Button($"       {t.Name}##theme", size)) MeterWindow.ApplyTheme(C, t);
        // The card above shows whatever is hovered, next frame.
        if (ImGui.IsItemHovered() && !live) _themePeek = t;
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
        DrawColumnView("Damage view", C.MeterColumns, "d");
        DrawColumnView("Healing view", C.MeterHealColumns, "h");
        ImGui.Spacing();
        ImGui.TextDisabled("Damage taken and Deaths reuse the damage list, each with its own number in front.");
    }

    // ---- Connection ----

    private void DrawMeterConnectionTab()
    {
        // Nothing to explain to someone already connected, but once it's up it stays
        // up for the session, so following the steps ends in a green line, not a blank.
        var card = !C.MeterSetupDone && (_setupCard || !_plugin.Meter.Connected);
        if (card)
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

        // The same steps, always here to check against, once the card is gone.
        if (card) return;

        // IINACT first, since Auto reaches for it first.
        SeparatorText("In IINACT");
        ImGui.TextDisabled("Nothing to connect: this links straight to it. On its Parser tab:");
        SetupToggle(1, "Disable Damage Shield Estimates", false, "or shields read zero.");
        SetupToggle(2, "End encounter automatically after leaving combat", true);
        SetupStep(3, "Player name: leave it as YOU.");
        Tip("The parser says YOU and the meter fills your name in.");
        ImGui.TextDisabled("Writing out the network log file is for uploading logs, not for this.");

        SeparatorText("In ACT");
        SetupStep(1, "Run ACT, with its FFXIV plugin.");
        SetupStep(2, "Plugins > OverlayPlugin.dll > WSServer > Start.");
        SetupStep(3, "Options > Main Table/Encounters > Idle Limit: 180.");
        ImGui.TextDisabled("Lower than that splits a fight at its own downtime.");
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
            ImGui.TextDisabled("On IINACT instead? It connects itself.");
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // A step whose whole instruction is one word, so the word carries the color.
    private static void SetupToggle(int n, string setting, bool on, string why = "")
    {
        ImGui.TextColored(Theme.V(Theme.Accent), $"{n}");
        ImGui.SameLine(0, 10);
        ImGui.TextUnformatted(setting + ":");
        ImGui.SameLine(0, 5);
        ImGui.TextColored(Theme.V(on ? Theme.Good : Theme.Danger), on ? "ON" : "OFF");
        if (why.Length == 0) return;
        ImGui.SameLine(0, 5);
        ImGui.TextDisabled(why);
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

    // One view: a sample of the meter itself, then every column as a checkbox.
    private void DrawColumnView(string title, List<string> list, string view)
    {
        ImGui.PushID($"mcol_{view}");
        SeparatorText(title);
        DrawColumnSample(list, view);

        ImGui.Spacing();
        if (ImGui.BeginTable("##colgrid", 3))
        {
            foreach (var key in MeterWindow.ColumnKeys)
            {
                ImGui.TableNextColumn();
                var on = list.Contains(key);
                ImGui.PushID(key);
                if (GreenCheckbox("##col", ref on))
                {
                    if (on) list.Add(key);   // new ones land on the right
                    else list.Remove(key);
                    C.SaveSettings();
                }
                ImGui.SameLine(0, 8);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(MeterWindow.ColumnLabel(key));
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.PopID();
    }

    // The sample card, drawn by the meter itself, wherever a tab wants one.
    private void DrawMeterCard(MeterWindow.SampleView v, float maxW = 430f)
    {
        var w = MathF.Min(ImGui.GetContentRegionAvail().X, maxW);
        var p = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        // Content first on its own channel, so the panel can be drawn behind it once
        // the card's height is known.
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        var h = _plugin.MeterWindow.DrawSample(dl, p, w, v);
        dl.ChannelsSetCurrent(0);
        dl.AddRectFilled(p, p + new Vector2(w, h), Theme.PanelBg, 6f);
        dl.AddRectFilled(p, p + new Vector2(w, h), v.Theme?.Bg ?? C.MeterBgColor, 6f);
        dl.AddRect(p, p + new Vector2(w, h),
            v.Theme is { } t ? (t.Accent & 0x00FFFFFFu) | 0x2E000000u : C.MeterBorderColor, 6f);
        dl.ChannelsMerge();

        ImGui.SetCursorScreenPos(p);
        ImGui.Dummy(new Vector2(w, h));
    }

    // Two rows of the real sample pull, drawn with this view's columns.
    private void DrawColumnSample(List<string> list, string view)
    {
        var slots = new List<(string Key, float X0, float X1)>();
        var heal = view == "h";
        var card = new MeterWindow.SampleView { Rows = 2, Heal = heal, Keys = list, Slots = slots };
        var p = ImGui.GetCursorScreenPos();
        DrawMeterCard(card);
        // Where the tab carries on, since the handles below move the cursor into the card.
        var below = ImGui.GetCursorScreenPos();

        // Headings are the handles: click drops a column, dragging moves it.
        var lineH = ImGui.GetTextLineHeight();
        var headY = p.Y + 10f;
        foreach (var s in slots)
        {
            if (!list.Contains(s.Key)) continue;
            ImGui.SetCursorScreenPos(new Vector2(s.X0 - MeterWindow.SampleGap * 0.5f, headY - 3f));
            ImGui.InvisibleButton($"##head_{s.Key}",
                new Vector2(s.X1 - s.X0 + MeterWindow.SampleGap, lineH + 6f));
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (_colDrag == null && Widgets.HoveredDelayed())
                    ImGui.SetTooltip($"{MeterWindow.ColumnLabel(s.Key)} - click to drop, drag to move");
            }
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 4f))
            { _colDrag = s.Key; _colDragView = view; }
            // Released on the heading, having never dragged: that's a click.
            else if (ImGui.IsItemDeactivated() && _colDrag == null && ImGui.IsItemHovered())
            { list.Remove(s.Key); C.SaveSettings(); }
        }

        ImGui.SetCursorScreenPos(below);
        ImGui.TextDisabled("Click a heading to drop it, or drag one to reorder.");
        if (_colDrag != null && _colDragView == view) DragColumn(list, slots, headY, lineH);
    }

    private string? _colDrag;
    private string _colDragView = "";

    // Ghost label, an insertion mark, and the reorder once the mouse comes up.
    private void DragColumn(List<string> list, List<(string Key, float X0, float X1)> slots,
        float headY, float lineH)
    {
        if (_colDrag is not { } drag || !list.Contains(drag)) { _colDrag = null; return; }

        var mouse = ImGui.GetMousePos();
        string? over = null;
        var after = false;
        foreach (var s in slots)
            if (mouse.X >= s.X0 - MeterWindow.SampleGap * 0.5f && mouse.X <= s.X1 + MeterWindow.SampleGap * 0.5f)
            {
                over = s.Key;
                after = mouse.X > (s.X0 + s.X1) * 0.5f;
            }

        var fg = ImGui.GetForegroundDrawList();
        fg.AddText(new Vector2(mouse.X + 10f, mouse.Y - lineH * 0.5f), 0xDDFFFFFF, MeterWindow.ColumnLabel(drag));
        if (over != null && over != drag)
            foreach (var s in slots)
                if (s.Key == over)
                {
                    var ix = after ? s.X1 + MeterWindow.SampleGap * 0.5f : s.X0 - MeterWindow.SampleGap * 0.5f;
                    fg.AddLine(new Vector2(ix, headY - 3f), new Vector2(ix, headY + lineH + 3f), Theme.Accent, 2f);
                }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left)) return;

        if (over != null && over != drag)
        {
            list.Remove(drag);
            var idx = list.IndexOf(over);
            idx = idx < 0 ? 0 : idx + (after ? 1 : 0);
            list.Insert(Math.Clamp(idx, 0, list.Count), drag);
            C.SaveSettings();
        }
        _colDrag = null;
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
