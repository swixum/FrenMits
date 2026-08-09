using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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
        C.MeterEnabled = PageHead("Fren Meter", _plugin.Meter.Connected ? "" : "Not Connected",
            C.MeterEnabled, hasModes: true, icon: FontAwesomeIcon.ChartBar, noteCol: Theme.Warn);
        if (!C.MeterEnabled) return;

        DrawMeterHeader(AllMode);
        if (AllMode) { DrawMeterAll(); return; }

        Widgets.ListBegin();
        DrawMeterShowRow();

        var barH = C.MeterBarHeight;
        if (Widgets.RowDrag("Bar Height", "", ref barH, 16f, 44f, "%.0f px", 86f))
        { C.MeterBarHeight = barH; C.SaveSettings(); }

        var maxRows = C.MeterMaxRows;
        if (Widgets.RowDragInt("Rows Shown", "Your own row always shows", ref maxRows, 0, 24,
                maxRows == 0 ? "Everyone" : "%d", 86f))
        { C.MeterMaxRows = maxRows; C.SaveSettings(); }

        var pos = C.MeterPosition;
        if (PositionRow(ref pos, MeterHome))
        { C.MeterPosition = pos; C.SaveSettings(); _plugin.MeterWindow.RequestReposition(); }

        var locked = C.MeterLocked;
        if (Widgets.RowCheck("Locked", "Position and size. Unlock to drag the edges.", ref locked))
        { C.MeterLocked = locked; C.SaveSettings(); }
        Widgets.ListEnd();

        ImGui.Spacing();
        Widgets.ListBegin();
        if (Widgets.RowDoor("All Settings", "Rows, bars, text and colors")) SetAllMode(true);
        if (Widgets.RowDoor("Columns", "Which numbers each row shows")) { SetAllMode(true); _jumpTab = "Columns"; }
        if (Widgets.RowDoor("Connection", _plugin.Meter.Connected ? _plugin.Meter.StatusText : "Not Connected"))
        { SetAllMode(true); _jumpTab = "Connection"; }
        Widgets.ListEnd();
    }

    private void DrawMeterAll()
    {
        if (!ImGui.BeginTabBar("##metertabs", ImGuiTabBarFlags.None)) return;
        if (TabItem("Rows")) { DrawMeterRowsTab(); ImGui.EndTabItem(); }
        if (TabItem("Bars")) { DrawMeterBarsTab(); ImGui.EndTabItem(); }
        if (TabItem("Text")) { DrawMeterTextTab(); ImGui.EndTabItem(); }
        if (TabItem("Colors")) { DrawMeterColorsTab(); ImGui.EndTabItem(); }
        if (TabItem("Columns")) { DrawMeterColumnsTab(); ImGui.EndTabItem(); }
        if (TabItem("Connection")) { DrawMeterConnectionTab(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }

    // Status, the profile being edited, the one preview and the themes. It sits
    // above everything so it never moves, and each tab edits the meter you see.
    private void DrawMeterHeader(bool all)
    {
        var connected = _plugin.Meter.Connected;
        StatusDot(Theme.V(connected ? Theme.Good : Theme.Warn), frameAligned: true);
        ImGui.SameLine(0, Theme.S(6f));
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.V(connected ? Theme.Good : Theme.Warn),
            _plugin.Meter.StatusText);

        DrawMeterProfileControl();
        ImGui.Spacing();
        // The peek is set by a theme chip below, so it lands here next frame.
        DrawMeterCard(new MeterWindow.SampleView { Rows = 3, Chrome = true, Theme = _themePeek });
        // Themes are a starting point, so they go once you are past presets.
        if (!all)
        {
            ImGui.Spacing();
            DrawMeterThemeChips();
        }
        ImGui.Spacing();
    }

    // Right under the preview, since that is what they change. Sixteen of them,
    // so the run wraps rather than running off the side of the page.
    private void DrawMeterThemeChips()
    {
        // Cleared here, then set again by whichever chip is hovered below.
        _themePeek = null;
        var gap = Theme.S(5f);
        var room = ImGui.GetContentRegionAvail().X;
        var x = 0f;
        foreach (var t in MeterWindow.Themes)
        {
            var w = Widgets.SwatchChipWidth(t.Name);
            if (x > 0f && x + gap + w > room) x = 0f;
            else if (x > 0f) { ImGui.SameLine(0, gap); x += gap; }
            x += w;

            // The named theme stays lit once tweaked; exact match covers old configs.
            var live = C.MeterThemeName.Length > 0 ? C.MeterThemeName == t.Name
                : C.MeterAccentColor == t.Accent && C.MeterBgColor == t.Bg
                  && C.MeterRowColor == t.Rows && C.MeterBarStyle == t.BarStyle;
            if (Widgets.SwatchChip(t.Name, t.Accent, live)) MeterWindow.ApplyTheme(C, t);
            if (ImGui.IsItemHovered() && !live) _themePeek = t;
            Tip(live ? "In use." : "Click to keep it; hover shows it above.");
        }
    }

    // Right-aligned on the status line: the saved look you are editing, and a
    // menu for the rest. Not a tab, since it saves and loads everything else.
    private void DrawMeterProfileControl()
    {
        var active = C.MeterProfileName;
        var saved = active.Length > 0 && C.MeterProfiles.ContainsKey(active);

        var w = Theme.S(210f) + ImGui.CalcTextSize("Profile").X;
        var end = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
        ImGui.SameLine(MathF.Max(end + Theme.S(12f), ImGui.GetContentRegionMax().X - w));
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Profile");
        ImGui.SameLine(0, Theme.S(8f));
        ImGui.SetNextItemWidth(Theme.S(170f));
        if (ImGui.BeginCombo("##mprofsel", saved ? active : "(Unsaved)"))
        {
            if (C.MeterProfiles.Count == 0) ImGui.TextDisabled("None saved yet");
            foreach (var kv in C.MeterProfiles)
                if (ImGui.Selectable(kv.Key, kv.Key == active))
                    ApplyMeterProfile(kv.Key);
            ImGui.EndCombo();
        }
        Tip(saved
            ? "Your changes save into this profile as you make them."
            : "This look is not saved to a profile yet.");

        ImGui.SameLine(0, Theme.S(4f));
        if (ImGui.SmallButton("...")) ImGui.OpenPopup("##mprofmenu");
        Tip("Save, rename, delete, share.");
        DrawMeterProfileMenu(active, saved);

        if (_meterFlash.Length > 0 && (DateTime.Now - _meterFlashAt).TotalSeconds < 4)
        {
            ImGui.SameLine(0, Theme.S(8f));
            ImGui.TextColored(Theme.V(_meterFlashOk ? Theme.Good : Theme.Warn), _meterFlash);
        }
    }

    // ---- Rows ----

    private void DrawMeterRowsTab()
    {
        Widgets.ListBegin();
        DrawMeterShowRow();

        var names = C.MeterNameStyle;
        if (Widgets.RowCombo("Names", "How names show on each row", ref names,
                "Full Name\0First Name\0First Name + Initial\0", 170f))
        { C.MeterNameStyle = names; C.SaveSettings(); }

        var maxRows = C.MeterMaxRows;
        if (Widgets.RowDragInt("Rows Shown", "Your own row always shows", ref maxRows, 0, 24,
                maxRows == 0 ? "Everyone" : "%d", 86f))
        { C.MeterMaxRows = maxRows; C.SaveSettings(); }

        var refresh = C.MeterRefreshSeconds;
        if (Widgets.RowDrag("Refresh", "How often numbers update. Bars stay smooth.",
                ref refresh, 0f, 3f, refresh <= 0f ? "Every frame" : "%.1f s", 96f))
        { C.MeterRefreshSeconds = refresh; C.SaveSettings(); }

        var v = C.MeterShowRank;
        if (Widgets.RowCheck("Rank Numbers", "", ref v)) { C.MeterShowRank = v; C.SaveSettings(); }
        v = C.MeterShowJobIcons;
        if (Widgets.RowCheck("Job Icons", "", ref v)) { C.MeterShowJobIcons = v; C.SaveSettings(); }
        v = C.MeterLimitBreakRow;
        if (Widgets.RowCheck("Limit Break Row", "LB bar under the party", ref v))
        { C.MeterLimitBreakRow = v; C.SaveSettings(); }
        v = C.MeterSplitHealing;
        if (Widgets.RowCheck("Split DPS and HPS", "DPS on top, healer HPS below", ref v))
        { C.MeterSplitHealing = v; C.SaveSettings(); }
        Widgets.ListEnd();

        Widgets.GroupLabel("Around the rows");
        Widgets.ListBegin();
        var header = C.MeterHeaderStyle;
        if (Widgets.RowCombo("Header", "Double-click the header to cycle", ref header,
                "Full\0Slim\0Hidden\0", 120f))
        { C.MeterHeaderStyle = header; C.SaveSettings(); }

        var v2 = C.MeterShowRaidTotal;
        if (Widgets.RowCheck("Raid rDPS Total", "", ref v2)) { C.MeterShowRaidTotal = v2; C.SaveSettings(); }
        v2 = C.MeterHealingTab;
        if (Widgets.RowCheck("DPS and HPS Tabs", "Right-click a tab to rename it", ref v2))
        { C.MeterHealingTab = v2; C.SaveSettings(); }
        v2 = C.MeterButtons;
        if (Widgets.RowCheck("Buttons Bar", "History, pause and reset at the bottom", ref v2))
        { C.MeterButtons = v2; C.SaveSettings(); }
        v2 = C.MeterFooterDeaths;
        if (Widgets.RowCheck("Death Count", "Deaths this pull. Hover for who.", ref v2))
        { C.MeterFooterDeaths = v2; C.SaveSettings(); }
        v2 = C.MeterClickThrough;
        if (Widgets.RowCheck("Click-through", "Mouse passes through, menu included", ref v2))
        { C.MeterClickThrough = v2; C.SaveSettings(); }
        Widgets.ListEnd();

        Widgets.GroupLabel("When you click a player");
        Widgets.ListBegin();
        var v3 = C.MeterBreakdownIcons;
        if (Widgets.RowCheck("Action Icons", "Beside each ability", ref v3))
        { C.MeterBreakdownIcons = v3; C.SaveSettings(); }
        v3 = C.MeterBreakdownColors;
        if (Widgets.RowCheck("Color Each Ability", "Off = job color throughout", ref v3))
        { C.MeterBreakdownColors = v3; C.SaveSettings(); }
        Widgets.ListEnd();
    }

    // One choice instead of two checkboxes that could contradict: the old pair
    // let you tick both, and the hide one silently won.
    private void DrawMeterShowRow()
    {
        var mode = C.MeterShowMode;
        if (Widgets.RowCombo("Show", "When the meter is on screen", ref mode,
                "Always\0After a Pull\0Only in Combat\0", 150f))
        { C.MeterShowMode = mode; C.SaveSettings(); }
        Tip("Always: stays put even with no pull, so a reset cannot hide it.\n"
            + "After a pull: the default; it appears once there is something to show.\n"
            + "Only in combat: gone a few seconds after the fight ends.");
    }

    // ---- Bars ----

    private void DrawMeterBarsTab()
    {
        Widgets.ListBegin();
        var fill = C.MeterBarStyle;
        if (Widgets.RowCombo("Fill", "", ref fill, "Flat\0Glass\0Gradient\0Outline\0Minimal\0", 130f))
        { C.MeterBarStyle = fill; C.SaveSettings(); }

        var v = C.MeterJobColors;
        if (Widgets.RowCheck("Color by Job", "Off = accent on every bar", ref v))
        { C.MeterJobColors = v; C.SaveSettings(); }
        v = C.MeterBarSolid;
        if (Widgets.RowCheck("Solid", "Full job color, not a wash", ref v))
        { C.MeterBarSolid = v; C.SaveSettings(); }

        var barH = C.MeterBarHeight;
        if (Widgets.RowDrag("Height", "", ref barH, 16f, 44f, "%.0f px", 86f))
        { C.MeterBarHeight = barH; C.SaveSettings(); }
        var gap = C.MeterBarGap;
        if (Widgets.RowDrag("Spacing", "", ref gap, 0f, 10f, "%.0f px", 86f))
        { C.MeterBarGap = gap; C.SaveSettings(); }
        var round = C.MeterRounding;
        if (Widgets.RowDrag("Rounding", "", ref round, 0f, 14f, "%.0f px", 86f))
        { C.MeterRounding = round; C.SaveSettings(); }
        var barOp = C.MeterBarOpacity;
        if (Widgets.RowDrag("Opacity", "", ref barOp, 0.2f, 1.6f, "%.2f", 86f))
        { C.MeterBarOpacity = barOp; C.SaveSettings(); }
        Widgets.ListEnd();

        Widgets.GroupLabel("Where it sits");
        Widgets.ListBegin();
        var pos = C.MeterPosition;
        var mmoved = PositionRow(ref pos, MeterHome);
        if (NudgeRow(ref pos) || mmoved)
        { C.MeterPosition = pos; C.SaveSettings(); _plugin.MeterWindow.RequestReposition(); }
        var locked = C.MeterLocked;
        if (Widgets.RowCheck("Locked", "Position and size. Unlock to drag the edges.", ref locked))
        { C.MeterLocked = locked; C.SaveSettings(); }
        Widgets.ListEnd();
    }

    // ---- Text ----

    private void DrawMeterTextTab()
    {
        Widgets.ListBegin();
        var fam = C.MeterFontFamily;
        var bold = C.MeterFontBold;
        var ital = C.MeterFontItalic;
        if (FontRow(ref fam, ref bold, ref ital))
        { C.MeterFontFamily = fam; C.MeterFontBold = bold; C.MeterFontItalic = ital; C.SaveSettings(); }

        var px = C.MeterFontSizePx;
        if (Widgets.RowDrag("Size", "", ref px, 11f, 26f, "%.0f px", 86f))
        { C.MeterFontSizePx = px; C.SaveSettings(); }

        var shadow = C.MeterTextShadow;
        if (Widgets.RowCheck("Drop Shadow", "", ref shadow)) { C.MeterTextShadow = shadow; C.SaveSettings(); }
        Widgets.ListEnd();

        Widgets.GroupLabel("Your row");
        Widgets.ListBegin();
        var you = C.MeterYou;
        if (Widgets.RowCheck("Call Your Row You", "", ref you)) { C.MeterYou = you; C.SaveSettings(); }
        var hi = C.MeterHighlightYou;
        if (Widgets.RowCheck("Highlight It", "", ref hi)) { C.MeterHighlightYou = hi; C.SaveSettings(); }
        if (C.MeterHighlightYou)
        {
            var style = C.MeterHighlightStyle;
            if (Widgets.RowCombo("Style", "", ref style, "Wash + Outline\0Wash\0Outline\0Side Stripe\0", 150f, sub: true))
            { C.MeterHighlightStyle = style; C.SaveSettings(); }
            var strength = C.MeterHighlightStrength;
            if (Widgets.RowDrag("Strength", "", ref strength, 0.2f, 2.5f, "%.2f", 86f, sub: true))
            { C.MeterHighlightStrength = strength; C.SaveSettings(); }
        }
        Widgets.ListEnd();
    }

    // ---- Colors ----
    // Grouped by what each one paints. Four are white by default, so a flat grid
    // gives you no way to tell them apart.

    private void DrawMeterColorsTab()
    {
        Widgets.GroupLabel("Text");
        Widgets.ListBegin();
        MeterColorRow("Names", "Party member names on their rows", () => C.MeterTextColor, v => C.MeterTextColor = v);
        MeterColorRow("Details", "Ranks, labels, secondary columns", () => C.MeterSubColor, v => C.MeterSubColor = v);
        MeterColorRow("Title", "The encounter name", () => C.MeterTitleColor, v => C.MeterTitleColor = v);
        MeterColorRow("Timer", "The encounter clock", () => C.MeterTimerColor, v => C.MeterTimerColor = v);
        Widgets.ListEnd();

        Widgets.GroupLabel("Yours");
        Widgets.ListBegin();
        MeterColorRow("Name", "Your own name in the list", () => C.MeterYouColor, v => C.MeterYouColor = v);
        MeterColorRow("Highlight", "The wash over your row", () => C.MeterHighlightColor, v => C.MeterHighlightColor = v);
        Widgets.ListEnd();

        Widgets.GroupLabel("Window");
        Widgets.ListBegin();
        ImGui.BeginDisabled(C.OverlaysFollowAccent);
        MeterColorRow("Accent", C.OverlaysFollowAccent
                ? "Held: Appearance has the overlays following the accent"
                : "Totals, and bars when job colors are off",
            () => C.MeterAccentColor, v => C.MeterAccentColor = v);
        ImGui.EndDisabled();
        MeterColorRow("Background", "", () => C.MeterBgColor, v => C.MeterBgColor = v);
        MeterColorRow("Rows", "The bar backgrounds", () => C.MeterRowColor, v => C.MeterRowColor = v);
        MeterColorRow("Border", "Alpha to zero hides it", () => C.MeterBorderColor, v => C.MeterBorderColor = v);
        Widgets.ListEnd();
    }

    private void MeterColorRow(string name, string hint, Func<uint> get, Action<uint> set)
    {
        var col = ColorToVec4(get());
        if (Widgets.RowColor(name, hint, ref col)) { set(Vec4ToColor(col)); C.SaveSettings(); }
    }

    // The theme under the cursor, drawn in the card until the cursor leaves.
    private MeterWindow.MeterTheme? _themePeek;

    // ---- Columns ----

    private void DrawMeterColumnsTab()
    {
        ImGui.Spacing();
        // Arrived from the Display tab: it is about columns, so it lives with them.
        C.MeterColumnHeader = CfgCheck("Column Labels", C.MeterColumnHeader);
        Tip("A heading row above the bars, naming each column.");
        ImGui.Spacing();
        ImGui.TextDisabled("Click a heading to drop it, or drag one to reorder.");
        HelpMarker("Damage taken and Deaths reuse the damage list, each with its own number in front.");
        DrawColumnView("Damage view", C.MeterColumns, "d");
        DrawColumnView("Healing view", C.MeterHealColumns, "h");
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
        LabelledWidth("Source", 240f);
        var conn = C.MeterConnection;
        if (ImGui.Combo("##msource", ref conn, "Auto\0Parser Plugin\0ACT WebSocket\0"))
        { C.MeterConnection = conn; C.SaveSettings(); ReconnectMeter(); }
        Tip("Auto tries the parser plugin, then ACT.");

        if (C.MeterConnection != 1)
        {
            LabelledWidth("Address", 300f);
            var addr = C.MeterSocketAddress;
            if (ImGui.InputText("##maddr", ref addr, 128))
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
        DrawActSteps();
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
            DrawActSteps();
            Tip("Lower than that splits a fight at its own downtime.");

            ImGui.Spacing();
            if (connected) ImGui.TextColored(Theme.V(Theme.Good), "Connected. Nothing else to do.");
            else ImGui.TextDisabled("Leave ACT running; this finds it on its own.");

            ImGui.SameLine(0, Theme.S(12f));
            if (ImGui.SmallButton("Got It")) { C.MeterSetupDone = true; C.SaveSettings(); }
            ImGui.TextDisabled("On IINACT instead? It connects itself.");
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // A step whose whole instruction is one word, so the word carries the color.
    private static void SetupToggle(int n, string setting, bool on, string why = "")
    {
        ImGui.TextColored(Theme.V(Theme.Accent), $"{n}");
        ImGui.SameLine(0, Theme.S(10f));
        ImGui.TextUnformatted(setting + ":");
        ImGui.SameLine(0, Theme.S(5f));
        ImGui.TextColored(Theme.V(on ? Theme.Good : Theme.Danger), on ? "ON" : "OFF");
        if (why.Length == 0) return;
        ImGui.SameLine(0, Theme.S(5f));
        ImGui.TextDisabled(why);
    }

    // A numbered line, the number in the accent color.
    private static void SetupStep(int n, string text)
    {
        ImGui.TextColored(Theme.V(Theme.Accent), $"{n}");
        ImGui.SameLine(0, Theme.S(10f));
        ImGui.TextUnformatted(text);
    }

    // One copy, drawn both in the first-run card and in the reference list, so
    // the two cannot drift apart.
    private static readonly string[] ActSteps =
    {
        "Run ACT, with its FFXIV plugin.",
        "Plugins > OverlayPlugin.dll > WSServer > Start.",
        "Options > Main Table/Encounters > Idle Limit: 180.",
    };

    private static void DrawActSteps()
    {
        for (var i = 0; i < ActSteps.Length; i++) SetupStep(i + 1, ActSteps[i]);
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

    // Everything that is not "which profile am I on", behind one menu.
    private void DrawMeterProfileMenu(string active, bool saved)
    {
        if (!ImGui.BeginPopup("##mprofmenu")) return;

        ImGui.SetNextItemWidth(Theme.S(180f));
        ImGui.InputTextWithHint("##mprofnew", "new profile name", ref _meterNameBuf, 48);
        ImGui.SameLine(0, Theme.S(6f));
        if (ImGui.Button("Save As"))
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
                ImGui.CloseCurrentPopup();
            }
        }

        if (saved)
        {
            ImGui.Separator();
            if (_meterRenameFor != active) { _meterRenameFor = active; _meterRenameBuf = active; }
            ImGui.SetNextItemWidth(Theme.S(180f));
            ImGui.InputTextWithHint("##mprofrename", "rename this one", ref _meterRenameBuf, 48);
            ImGui.SameLine(0, Theme.S(6f));
            if (ImGui.Button("Rename"))
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

            // Two-click delete so one stray click cannot eat a profile.
            if ((DateTime.Now - _meterDeleteAt).TotalSeconds < 3)
            {
                Widgets.PushDangerOutline();
                if (ImGui.Button("Sure? Delete it"))
                {
                    C.MeterProfiles.Remove(active);
                    C.MeterProfileName = "";
                    C.SaveSettings();
                    MeterFlash("Profile deleted.");
                    ImGui.CloseCurrentPopup();
                }
                Widgets.PopDanger();
            }
            else if (ImGui.Button("Delete")) _meterDeleteAt = DateTime.Now;
        }

        ImGui.Separator();
        if (ImGui.Button("Copy Share Code"))
        {
            ImGui.SetClipboardText(MeterProfile.Export(C));
            MeterFlash("Code copied to clipboard.");
        }
        Tip("A code carries the whole layout and look.");
        ImGui.SameLine(0, Theme.S(6f));
        if (ImGui.Button("Import from Clipboard"))
        {
            ImportMeterProfile(ImGui.GetClipboardText());
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void ApplyMeterProfile(string name)
    {
        if (!C.MeterProfiles.TryGetValue(name, out var code)) return;
        if (MeterProfile.Import(C, code))
        {
            C.MeterProfileName = name;
            // A profile is its own look, so no theme chip stays lit.
            C.MeterThemeName = "";
            C.SaveSettings();
            _plugin.MeterWindow.RequestReposition();
            MeterFlash($"Profile \"{name}\" applied.");
        }
        else
            MeterFlash("That profile could not be read.", ok: false);
    }

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
                ImGui.SameLine(0, Theme.S(8f));
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

    // Drop the link; the next tick reconnects with new settings.
    private void ReconnectMeter() => _plugin.Meter.Link.RetryNow();
}
