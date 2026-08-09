using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;

namespace FrenMits.Host;

// The individual config pages plus their small helpers.
public partial class ConfigWindow
{
    // ---- Party Mit Recap ----

    private void DrawPartyRecapPage()
    {
        SeparatorText("Party Mit Recap");
        ImGui.TextWrapped("After a wipe, a full recap of the pull's mitigation in its own window: the damage-downs "
                          + "on the boss (Reprisal / Feint / Addle / Dismantle) plus the party's defensive cooldowns "
                          + "(Rampart, Sacred Soil, Kerachole, ...), who used them and when, and which standard raid "
                          + "mits never landed.");
        ImGui.Spacing();

        C.RecapEnabled = CfgCheck("Enable Party Mit Recap", C.RecapEnabled);
        Tip("Tracks every pull and offers a recap after a wipe.");

        if (C.RecapEnabled)
        {
            var locked = C.RecapPopupLocked;
            if (GreenCheckbox("Lock popup position", ref locked)) { C.RecapPopupLocked = locked; _plugin.RecapButtonWindow.RequestReposition(); C.SaveSettings(); }
            ImGui.SameLine();
            ImGui.TextDisabled("(unlock, then drag the popup to place it)");
        }

        ImGui.Spacing();
        if (ImGui.Button("Open recap window")) _plugin.RecapWindow.IsOpen = true;
        Tip("Opens the movable recap window with the last pull's data.");
        ImGui.SameLine();
        if (ImGui.Button("Preview"))
        {
            // A real pull previews better, so never clobber one.
            if (!_plugin.Recap.HasData) _plugin.Recap.LoadSample();
            _plugin.Recap.ShowTestPopup();          // popup appears so it can be dragged
            _plugin.RecapWindow.IsOpen = true;      // window opens for placement too
        }
        Tip("Fills the recap with a sample pull.");
    }

    // ---- Food and Pot ----

    private void DrawPrepCheckPage()
    {
        SeparatorText("Food & Pot");
        ImGui.TextWrapped("Two small reminders on one line. Your food is checked before the pull and flagged "
                          + "if it's missing or would run out partway through, and again the moment a ready "
                          + "check goes out. Your pot is watched during the fight, and called out once when "
                          + "it comes back up.");
        ImGui.Spacing();

        C.PrepCheckEnabled = CfgCheck("Enable Food & Pot", C.PrepCheckEnabled);
        Tip("Nothing is drawn while your food is fine.");
        if (!C.PrepCheckEnabled) return;

        C.PrepCheckSheetsOnly = Toggle("Only in fights with a sheet", C.PrepCheckSheetsOnly);
        Tip("Only check duties you have a sheet for.");
        C.PrepCheckShowCounts = Toggle("Show how many you have left", C.PrepCheckShowCounts);
        Tip("Appends \"(12 left)\" from your bags.");

        if (!ImGui.BeginTabBar("##preptabs", ImGuiTabBarFlags.None)) return;

        if (TabItem("Food"))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Shown out of combat, and stays up for as long as there's a problem: red for "
                              + "no food at all, amber with a countdown once it's nearly gone. The icon is "
                              + "the dish you actually ate.");
            ImGui.Spacing();

            C.PrepCheckOnReadyCheck = Toggle("Notify on Ready Check", C.PrepCheckOnReadyCheck);
            Tip("Notifies on ready check.");
            ImGui.Spacing();

            C.PrepCheckUseFightLength = Toggle("Use the fight's own length", C.PrepCheckUseFightLength);
            Tip("Warn if your food won't last this fight.");

            if (!C.PrepCheckUseFightLength)
            {
                ImGui.Indent(20f);
                var warnMin = C.PrepCheckWarnMinutes;
                if (Widgets.SliderInput("Warn under", ref warnMin, 1f, 30f, "%.0f min", width: 200f))
                { C.PrepCheckWarnMinutes = warnMin; C.SaveSettings(); }
                Tip("How much food time left starts the warning.");
                ImGui.Unindent(20f);
            }

            ImGui.Spacing();
            C.PrepCheckWarnWrongFood = Toggle("Warn on crafter food", C.PrepCheckWarnWrongFood);
            Tip("Flags food whose stats are all crafting ones.");
            C.PrepCheckWarnNq = Toggle("Warn on NQ food", C.PrepCheckWarnNq);
            Tip("HQ food caps noticeably higher.");
            C.PrepCheckAlwaysShowFood = Toggle("Always show the timer", C.PrepCheckAlwaysShowFood);
            Tip("Keep the food timer on screen when it's fine.");
            ImGui.EndTabItem();
        }

        if (TabItem("Potion"))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Mid-fight, not pre-pull. It says nothing until it has seen you actually use "
                              + "a pot; from there it times that pot's own recast and shows "
                              + "\"Potion is Available!\" for five seconds when it's back, so the second one "
                              + "doesn't get forgotten. It belongs to the pull it was used on: a wipe clears "
                              + "it, and it never speaks up while you're stood out of combat.");
            ImGui.Spacing();

            C.PrepCheckPotion = Toggle("Potion reminder", C.PrepCheckPotion);
            Tip("Waits for the moment it matters, mid-fight.");

            if (C.PrepCheckPotion)
            {
                ImGui.Indent(20f);
                C.PrepCheckPotCountdown = Toggle("Count down to it", C.PrepCheckPotCountdown);
                Tip("Shows \"Pot 1:23\" while the recast runs.");
                ImGui.Unindent(20f);
            }
            ImGui.EndTabItem();
        }

        if (TabItem("Voice"))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Each one spoken once, as it appears - never repeated while it sits on screen.");
            ImGui.Spacing();

            C.PrepCheckTts = Toggle("Speak it", C.PrepCheckTts);
            Tip("Speaks these in the Audio page voice.");

            if (C.PrepCheckTts)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("Uses the voice, rate and volume from the Audio page.");
                ImGui.SameLine();
                if (ImGui.SmallButton("Open Audio")) _nav = NavKind.Audio;
            }
            ImGui.EndTabItem();
        }

        if (TabItem("Placement"))
        {
            ImGui.Spacing();
            var prepLocked = C.PrepCheckLocked;
            if (GreenCheckbox("Lock position", ref prepLocked))
            { C.PrepCheckLocked = prepLocked; _plugin.PrepWindow.RequestReposition(); C.SaveSettings(); }
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(prepLocked ? "(unlock to drag)" : "(drag it, or use the sliders; auto-locks in combat)");

            var pos = C.PrepCheckPosition;
            if (Widgets.SliderInput("Horizontal", ref pos.X, 0f, 1f, "%.2f"))
            { C.PrepCheckPosition = pos; C.SaveSettings(); _plugin.PrepWindow.RequestReposition(); }
            ImGui.SameLine(0, 18);
            if (Widgets.SliderInput("Vertical", ref pos.Y, 0f, 1f, "%.2f"))
            { C.PrepCheckPosition = pos; C.SaveSettings(); _plugin.PrepWindow.RequestReposition(); }

            var prepPx = C.PrepCheckFontSizePx;
            if (Widgets.SliderInput("Text size", ref prepPx, 10f, 48f, "%.0f px")) { C.PrepCheckFontSizePx = prepPx; C.SaveSettings(); }

            ImGui.Spacing();
            ImGui.TextDisabled("Turn on Test mode in the header to see a sample while you place it.");
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawCombatTimerPage()
    {
        SeparatorText("Combat Timer");
        ImGui.TextWrapped("A plain stopwatch of the current pull's combat time (mm:ss), shown as its own "
                          + "overlay. Use the \"Test\" toggle in the header to preview while you place and style it.");
        ImGui.Spacing();

        C.ShowCombatTimer = CfgCheck("Show the combat timer", C.ShowCombatTimer);
        if (!C.ShowCombatTimer) return;

        if (!ImGui.BeginTabBar("##cttabs", ImGuiTabBarFlags.None)) return;

        if (TabItem("Placement"))
        {
            ImGui.Spacing();
            C.CombatTimerLocked = CfgCheck("Lock position (click-through)", C.CombatTimerLocked);
            ImGui.SameLine();
            ImGui.TextDisabled(C.CombatTimerLocked ? "(unlock to drag)" : "(drag it, or use the sliders; auto-locks in combat)");

            var pos = C.CombatTimerPosition;
            if (Widgets.SliderInput("Horizontal", ref pos.X, 0f, 1f, "%.2f"))
            { C.CombatTimerPosition = pos; C.SaveSettings(); _plugin.CombatTimerWindow.RequestReposition(); }
            ImGui.SameLine(0, 18);
            if (Widgets.SliderInput("Vertical", ref pos.Y, 0f, 1f, "%.2f"))
            { C.CombatTimerPosition = pos; C.SaveSettings(); _plugin.CombatTimerWindow.RequestReposition(); }
            ImGui.SameLine(0, 12);
            if (ImGui.SmallButton("Center top"))
            {
                C.CombatTimerPosition = new Vector2(0.5f, 0.08f);
                C.Save();
                _plugin.CombatTimerWindow.RequestReposition();
            }
            ImGui.EndTabItem();
        }

        if (TabItem("Font"))
        {
            ImGui.Spacing();
            var fonts = FontManager.FamilyNames;
            var fIdx = Math.Max(0, Array.IndexOf(fonts, C.CombatTimerFontFamily));
            ImGui.SetNextItemWidth(200f);
            if (ImGui.Combo("Font", ref fIdx, fonts, fonts.Length)) { C.CombatTimerFontFamily = fonts[fIdx]; C.SaveSettings(); }
            ImGui.SameLine(0, 12);
            var bold = C.CombatTimerFontBold;
            if (GreenCheckbox("Bold", ref bold)) { C.CombatTimerFontBold = bold; C.SaveSettings(); }
            ImGui.SameLine();
            var italic = C.CombatTimerFontItalic;
            if (GreenCheckbox("Italic", ref italic)) { C.CombatTimerFontItalic = italic; C.SaveSettings(); }
            if (C.CombatTimerFontFamily == "Default" && (C.CombatTimerFontBold || C.CombatTimerFontItalic))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(pick a font)");
            }
            var px = C.CombatTimerFontSizePx;
            if (Widgets.SliderInput("Text size", ref px, 12f, 120f, "%.0f px")) { C.CombatTimerFontSizePx = px; C.SaveSettings(); }
            ImGui.EndTabItem();
        }

        if (TabItem("Colors"))
        {
            ImGui.Spacing();
            var col = ColorToVec4(C.CombatTimerColor);
            if (ImGui.ColorEdit4("Text color", ref col, ImGuiColorEditFlags.NoInputs)) { C.CombatTimerColor = Vec4ToColor(col); C.SaveSettings(); }

            C.CombatTimerShowBackground = CfgCheck("Draw a background box", C.CombatTimerShowBackground);
            if (C.CombatTimerShowBackground)
            {
                ImGui.SameLine(0, 14);
                var bg = ColorToVec4(C.CombatTimerBackgroundColor);
                if (ImGui.ColorEdit4("Color##ctbg", ref bg, ImGuiColorEditFlags.NoInputs)) { C.CombatTimerBackgroundColor = Vec4ToColor(bg); C.SaveSettings(); }
                if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Drag the alpha channel down for a translucent box.");
            }
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawDisplayTab()
    {
        // One-click reset of everything on this page.
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, "Reset display")) ResetDisplayDefaults();
        if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Reset every setting on this page to defaults.");

        // Tabs by concern, each scoped to the center call.
        if (!ImGui.BeginTabBar("##displaytabs", ImGuiTabBarFlags.None)) return;

        if (TabItem("Placement"))
        {
            ImGui.Spacing();
            C.OverlayLocked = CfgCheck("Lock overlay (click-through)", C.OverlayLocked);
            ImGui.SameLine();
            ImGui.TextDisabled(C.OverlayLocked ? "(unlock to drag)" : "(drag it, or use the sliders; auto-locks in combat)");

            var pos = C.OverlayPosition;
            if (Widgets.SliderInput("Horizontal", ref pos.X, 0f, 1f, "%.2f"))
            { C.OverlayPosition = pos; C.SaveSettings(); _plugin.OverlayWindow.RequestReposition(); }
            ImGui.SameLine(0, 18);
            if (Widgets.SliderInput("Vertical", ref pos.Y, 0f, 1f, "%.2f"))
            { C.OverlayPosition = pos; C.SaveSettings(); _plugin.OverlayWindow.RequestReposition(); }
            ImGui.SameLine(0, 12);
            if (ImGui.SmallButton("Center"))
            {
                C.OverlayPosition = new Vector2(0.5f, 0.35f);
                C.Save();
                _plugin.OverlayWindow.RequestReposition();
            }
            ImGui.EndTabItem();
        }

        if (TabItem("Style"))
        {
            ImGui.Spacing();
            {
                ImGui.SetNextItemWidth(220f);
                var style = C.OverlayStyle;
                if (ImGui.Combo("Call style", ref style,
                        "Classic (centered text)\0Board (timeline look)\0Icon + clock\0"))
                { C.OverlayStyle = style; C.SaveSettings(); }
                Tip("How the center call is drawn.");
            }
            if (ImGui.BeginTable("##texttoggles", GridCols(), ImGuiTableFlags.SizingStretchSame))
            {
                C.ShowAbilityIcon = GridCheck("Ability icon", C.ShowAbilityIcon,
                    "Matched from the action name; pin one per line with the \"...\" button.");
                C.ShowRadialRing = GridCheck("Radial ring", C.ShowRadialRing,
                    "A depleting countdown ring around the call icon.");
                C.OverlayCallPanel = GridCheck("Call panel", C.OverlayCallPanel,
                    "A board-style plate behind the classic call.");
                C.OverlayTextSpark = GridCheck("Text spark", C.OverlayTextSpark,
                    "Crosses the classic call text with the bar's edge, a mark where it stops, and a spark as they meet.");
                C.ShowMechanicLine = GridCheck("Mechanic 2nd line", C.ShowMechanicLine);
                C.ShowCountdownNumber = GridCheck("Countdown number", C.ShowCountdownNumber);
                C.TextShadow = GridCheck("Drop shadow", C.TextShadow,
                    "Improves readability over busy backgrounds.");
                C.CooldownAwareCalls = GridCheck("Cooldown warnings", C.CooldownAwareCalls,
                    "Reddens the main call ([CD Ns]) and dims it in the upcoming list when your mit is still on cooldown past the call time. Your job's mits only.");
                ImGui.EndTable();
            }
            if (ImGui.TreeNode("Advanced format"))
            {
                var fmt = C.HeadlineFormat;
                ImGui.SetNextItemWidth(280f);
                if (ImGui.InputText("Call format", ref fmt, 128)) { C.HeadlineFormat = fmt; C.SaveSettings(); }
                ImGui.TextDisabled("Placeholders: {action} {mechanic} {time} {count} {remaining}");
                var suffix = C.ActiveSuffix;
                ImGui.SetNextItemWidth(280f);
                if (ImGui.InputText("\"NOW\" suffix", ref suffix, 64)) { C.ActiveSuffix = suffix; C.SaveSettings(); }
                ImGui.TreePop();
            }
            ImGui.EndTabItem();
        }

        if (TabItem("Font"))
        {
            ImGui.Spacing();
            var fonts = FontManager.FamilyNames;
            var fIdx = Math.Max(0, Array.IndexOf(fonts, C.OverlayFontFamily));
            ImGui.SetNextItemWidth(200f);
            if (ImGui.Combo("Font", ref fIdx, fonts, fonts.Length)) { C.OverlayFontFamily = fonts[fIdx]; C.SaveSettings(); }
            ImGui.SameLine(0, 12);
            var bold = C.OverlayFontBold;
            if (GreenCheckbox("Bold", ref bold)) { C.OverlayFontBold = bold; C.SaveSettings(); }
            ImGui.SameLine();
            var italic = C.OverlayFontItalic;
            if (GreenCheckbox("Italic", ref italic)) { C.OverlayFontItalic = italic; C.SaveSettings(); }
            if (C.OverlayFontFamily == "Default" && (C.OverlayFontBold || C.OverlayFontItalic))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(pick a font)");
            }
            var callPx = C.OverlayFontSizePx;
            if (Widgets.SliderInput("Call size", ref callPx, 12f, 120f, "%.0f px")) { C.OverlayFontSizePx = callPx; C.SaveSettings(); }
            ImGui.SameLine(0, 18);
            var align = C.OverlayTextAlign;
            ImGui.SetNextItemWidth(110f);
            if (ImGui.Combo("Align", ref align, new[] { "Left", "Center", "Right" }, 3))
            { C.OverlayTextAlign = align; C.SaveSettings(); }
            if (C.ShowAbilityIcon)
            {
                var iconScale = C.IconScale;
                if (Widgets.SliderInput("Icon size", ref iconScale, 0.4f, 1.5f, "%.2fx")) { C.IconScale = iconScale; C.SaveSettings(); }
            }
            ImGui.EndTabItem();
        }

        if (TabItem("Colors"))
        {
            ImGui.Spacing();
            var imminent = ColorToVec4(C.OverlayColorImminent);
            if (ImGui.ColorEdit4("Counting down", ref imminent, ImGuiColorEditFlags.NoInputs)) { C.OverlayColorImminent = Vec4ToColor(imminent); C.SaveSettings(); }
            ImGui.SameLine(0, 14);
            var active = ColorToVec4(C.OverlayColorActive);
            if (ImGui.ColorEdit4("NOW", ref active, ImGuiColorEditFlags.NoInputs)) { C.OverlayColorActive = Vec4ToColor(active); C.SaveSettings(); }
            ImGui.SameLine(0, 14);
            var mechCol = ColorToVec4(C.OverlayColorMechanic);
            if (ImGui.ColorEdit4("Mechanic", ref mechCol, ImGuiColorEditFlags.NoInputs)) { C.OverlayColorMechanic = Vec4ToColor(mechCol); C.SaveSettings(); }
            ImGui.SameLine(0, 16);
            if (ImGui.SmallButton("Reset colors"))
            {
                C.OverlayColorImminent = 0xFF55FFFF; C.OverlayColorActive = 0xFF55FF55;
                C.OverlayColorMechanic = 0xC0FFFFFF; C.OverlayColorUpcoming = 0xB0FFFFFF;
                C.Save();
            }

            ImGui.Spacing();
            C.ColorByMitType = CfgCheck("Color the call by mit type", C.ColorByMitType);
            HelpMarker("Tints calls by what kind of mit they are. Lines with their own color override are left alone.");
            if (C.ColorByMitType)
            {
                var party = ColorToVec4(C.MitColorParty);
                if (ImGui.ColorEdit4("Party mit", ref party, ImGuiColorEditFlags.NoInputs)) { C.MitColorParty = Vec4ToColor(party); C.SaveSettings(); }
                ImGui.SameLine(0, 14);
                var tank = ColorToVec4(C.MitColorTank);
                if (ImGui.ColorEdit4("Tank", ref tank, ImGuiColorEditFlags.NoInputs)) { C.MitColorTank = Vec4ToColor(tank); C.SaveSettings(); }
                ImGui.SameLine(0, 14);
                var personal = ColorToVec4(C.MitColorPersonal);
                if (ImGui.ColorEdit4("Personal", ref personal, ImGuiColorEditFlags.NoInputs)) { C.MitColorPersonal = Vec4ToColor(personal); C.SaveSettings(); }
            }
            ImGui.EndTabItem();
        }

        if (TabItem("Bar & box"))
        {
            ImGui.Spacing();
            C.ShowProgressBar = CfgCheck("Countdown bar under the call", C.ShowProgressBar);
            if (C.ShowProgressBar)
            {
                ImGui.SameLine(0, 14);
                var barH = C.ProgressBarHeight;
                if (Widgets.SliderInput("Height", ref barH, 2f, 24f, "%.0f px", width: 140f)) { C.ProgressBarHeight = barH; C.SaveSettings(); }
            }
            C.PulseWhenImminent = CfgCheck("Pulse the text in the last second", C.PulseWhenImminent);
            C.ShowBackground = CfgCheck("Draw a background box", C.ShowBackground);
            if (C.ShowBackground)
            {
                ImGui.SameLine(0, 14);
                var bg = ColorToVec4(C.BackgroundColor);
                if (ImGui.ColorEdit4("Color##overlaybg", ref bg, ImGuiColorEditFlags.NoInputs)) { C.BackgroundColor = Vec4ToColor(bg); C.SaveSettings(); }
                if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Drag the alpha channel down for a translucent box.");
            }
            ImGui.EndTabItem();
        }

        if (TabItem("Timing"))
        {
            ImGui.Spacing();
            C.StartOnCountdown = Toggle("Start on the pull countdown", C.StartOnCountdown);
            Tip("Timeline, call and voice run through the countdown.");
            ImGui.Spacing();

            SeparatorText("Calls with no usage window");
            var warn = C.WarningSeconds;
            if (Widgets.SliderInput("Show ahead", ref warn, 1f, 12f, "%.1fs")) { C.WarningSeconds = warn; C.SaveSettings(); }
            Tip("How early a call appears.");
            ImGui.SameLine(0, 18);
            var hold = C.HoldSeconds;
            if (Widgets.SliderInput("Hold on screen", ref hold, 0f, 6f, "%.1fs")) { C.HoldSeconds = hold; C.SaveSettings(); }
            Tip("How long a call stays up after its time passes.");

            ImGui.Spacing();
            var useWinWas = C.ShowUseWindows;
            C.ShowUseWindows = Toggle("Usage window", C.ShowUseWindows);
            if (C.ShowUseWindows != useWinWas) { C.SaveSettings(); _plugin.InvalidateSolverCache(); }
            Tip("Gives a mit with a duration a span to press in, timed from that duration. "
                + "Instants have no such span and keep the timing above.");
            if (C.ShowUseWindows)
            {
                ImGui.Indent(20f);
                var winLead = C.UseWindowLeadSeconds;
                if (Widgets.SliderInput("Show ahead##usewin", ref winLead, 0f, 12f, "%.1fs"))
                {
                    C.UseWindowLeadSeconds = winLead;
                    C.SaveSettings();
                }
                Tip("How early a windowed call appears, counting down to the window opening.");
                ImGui.SameLine(0, 18);
                var maxDur = C.MaxUseWindowSeconds;
                if (Widgets.SliderInput("Max window duration", ref maxDur, 1f, 30f, "%.1fs", width: 200f))
                {
                    C.MaxUseWindowSeconds = maxDur;
                    C.SaveSettings();
                }
                if (ImGui.IsItemDeactivatedAfterEdit()) _plugin.InvalidateSolverCache();
                Tip("Clamps how wide a usage window may get.");
                ImGui.TextDisabled("Clears as the window closes - these never hold.");
                ImGui.Unindent(20f);
            }
            ImGui.EndTabItem();
        }

        if (TabItem("Extras"))
        {
            ImGui.Spacing();
            SeparatorText("Extra readouts");
            C.ShowDtrBar = CfgCheck("Server-bar next mit", C.ShowDtrBar);
            Tip("Shows the next mit on the server-info bar.");
            C.ShowMitBar = CfgCheck("Active-mits bar", C.ShowMitBar);
            Tip("Your active defensive buffs with seconds left.");
            if (C.ShowMitBar)
            {
                ImGui.Indent(20f);
                var locked = C.MitBarLocked;
                if (GreenCheckbox("Lock position", ref locked)) { C.MitBarLocked = locked; _plugin.MitBarWindow.RequestReposition(); C.SaveSettings(); }
                ImGui.SameLine();
                ImGui.TextDisabled("Auto-locks in combat; move it out of combat or with Live preview.");
                var mbPx = C.MitBarFontSizePx;
                if (Widgets.SliderInput("Text size##mitbar", ref mbPx, 10f, 48f, "%.0f px")) { C.MitBarFontSizePx = mbPx; C.SaveSettings(); }
                ImGui.Unindent(20f);
            }

            ImGui.Spacing();
            SeparatorText("Accessibility");
            C.ColorblindMode = CfgCheck("Colorblind-safe status colors", C.ColorblindMode);
            Theme.Colorblind = C.ColorblindMode; // keep the live palette in sync with the setting
            Tip("Color-blind safe status colors.");
            ImGui.EndTabItem();
        }

        if (TabItem("Look")) { DrawLookTab(); ImGui.EndTabItem(); }

        ImGui.EndTabBar();
    }

    // The plugin's own windows: one accent color, one size.
    private void DrawLookTab()
    {
        ImGui.Spacing();
        SeparatorText("Accent color");
        ImGui.TextDisabled("Drives every plugin window: selected tabs, sliders, buttons and headers.");
        ImGui.Spacing();

        var accent = ColorToVec4(C.AccentColor);
        if (ImGui.ColorEdit4("Accent color", ref accent, ImGuiColorEditFlags.NoInputs))
        {
            C.AccentColor = Vec4ToColor(accent);
            Theme.Accent = C.AccentColor;
            C.SaveSettings();
        }

        // One click each, so a color can be tried without opening the picker.
        ImGui.Spacing();
        foreach (var (name, col) in AccentPresets)
        {
            if (AccentSwatch(name, col)) { C.AccentColor = col; Theme.Accent = col; C.SaveSettings(); }
            ImGui.SameLine(0, 8);
        }
        ImGui.NewLine();

        ImGui.Spacing();
        if (ImGui.SmallButton("Match the overlays to this"))
        {
            C.UpcomingBoardAccentColor = C.AccentColor;
            C.MeterAccentColor = C.AccentColor;
            C.Save();
        }
        Tip("Points the Next Mits board and the meter at this color too.");

        ImGui.Spacing();
        SeparatorText("Size");
        var scale = C.UiScale;
        if (Widgets.SliderInput("UI scale", ref scale, 0.8f, 1.6f, "%.2fx", width: 220f))
        {
            C.UiScale = scale;
            Theme.Scale = scale;
            C.SaveSettings();
        }
        ImGui.TextDisabled("Text and spacing in the plugin's own windows. The in-game overlays keep");
        ImGui.TextDisabled("their own text-size sliders, since they sit over the game.");
    }

    // Packed ABGR, so these read reversed from their hex names.
    private static readonly (string Name, uint Color)[] AccentPresets =
    {
        ("Blue", Theme.DefaultAccent), ("Amber", 0xFF3B88F0), ("Violet", 0xFFF56B9B),
        ("Teal", 0xFFA8C93B), ("Rose", 0xFF7A5CF0),
    };

    // A round color chip; returns true when picked.
    private bool AccentSwatch(string name, uint color)
    {
        var size = ImGui.GetFrameHeight();
        var p = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton($"##sw{name}", new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var c = new Vector2(p.X + size * 0.5f, p.Y + size * 0.5f);
        var dl = ImGui.GetWindowDrawList();
        dl.AddCircleFilled(c, size * 0.42f, color);
        if (C.AccentColor == color || hovered)
            dl.AddCircle(c, size * 0.5f - 1f, hovered ? 0xFFFFFFFF : Theme.TextBright, 0, 2f);
        if (hovered) ImGui.SetTooltip(name);
        return clicked;
    }

    // ---- Next Mits board ----

    private void DrawNextMitsPage()
    {
        SeparatorText("Next Mits & Timeline");
        ImGui.TextWrapped("Every upcoming mechanic as a countdown bar, with the planned mits underneath. "
                          + "Gold is your next press; green means press it now, in step with the main call.");
        ImGui.Spacing();

        // One control strip for the things you touch most.
        C.ShowUpcoming = CfgCheck("Show the window", C.ShowUpcoming);
        ImGui.SameLine(0, 16);
        if (ImGuiComponents.IconButtonWithText(_nextMitsPreview ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play,
                _nextMitsPreview ? "Stop preview" : "Preview"))
            _nextMitsPreview = !_nextMitsPreview;
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip("Plays a sample in the real window so you can place it.");
        ImGui.SameLine(0, 8);
        if (ImGui.SmallButton("Reset position"))
        {
            C.TimelinePosition = new Vector2(0.5f, 0.62f);
            C.Save();
            _plugin.TimelineWindow.RequestReposition();
        }
        ImGui.SameLine(0, 8);
        if (ImGui.SmallButton("Reset all")) ResetNextMitsDefaults();
        if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Everything on this page back to the FrenMits defaults.");

        C.TimelineLocked = CfgCheck("Lock the window (click-through)", C.TimelineLocked);
        ImGui.SameLine();
        ImGui.TextDisabled(C.TimelineLocked ? "(unlock to drag)" : "(drag it to move; auto-locks in combat)");

        // Precise placement too, for anyone who'd rather not drag.
        var tpos = C.TimelinePosition;
        if (Widgets.SliderInput("Horizontal##tl", ref tpos.X, 0f, 1f, "%.2f")) { C.TimelinePosition = tpos; C.SaveSettings(); }
        ImGui.SameLine(0, 18);
        if (Widgets.SliderInput("Vertical##tl", ref tpos.Y, 0f, 1f, "%.2f")) { C.TimelinePosition = tpos; C.SaveSettings(); }

        if (_nextMitsPreview) _plugin.TimelineWindow.PingScreenPreview();
        ImGui.Spacing();

        if (!C.ShowUpcoming) return;

        var boardStyle = Math.Clamp(C.UpcomingStyle, 0, 1) == 1;

        if (!ImGui.BeginTabBar("##nmtabs", ImGuiTabBarFlags.None)) return;

        if (TabItem("Layout"))
        {
            ImGui.Spacing();
            var style = Math.Clamp(C.UpcomingStyle, 0, 1);
            var styles = new[]
            {
                "Compact list (just your next calls)",
                "Mechanic board (every hit, countdown bars)",
            };
            ImGui.SetNextItemWidth(320f);
            if (ImGui.Combo("##nmstyle", ref style, styles, styles.Length)) { C.UpcomingStyle = style; C.SaveSettings(); }
            boardStyle = style == 1;

            if (boardStyle)
            {
                var brows = C.UpcomingBoardRows;
                if (Widgets.SliderInput("Rows", ref brows, 3, 12)) { C.UpcomingBoardRows = brows; C.SaveSettings(); }
                ImGui.SameLine(0, 18);
                var blook = C.UpcomingBoardLookaheadSeconds;
                if (Widgets.SliderInput("Look-ahead", ref blook, 15f, 180f, "%.0fs")) { C.UpcomingBoardLookaheadSeconds = blook; C.SaveSettings(); }
                HelpMarker("How many bars at once, and how far ahead the board looks: bars are full at that edge, empty at the hit.");

                ImGui.Spacing();
                var bw = C.UpcomingBoardWidth;
                if (Widgets.SliderInput("Bar width", ref bw, 220f, 560f, "%.0f px")) { C.UpcomingBoardWidth = bw; C.SaveSettings(); }
                ImGui.SameLine(0, 18);
                var upPx = C.UpcomingFontSizePx;
                if (Widgets.SliderInput("Text size", ref upPx, 10f, 60f, "%.0f px")) { C.UpcomingFontSizePx = upPx; C.SaveSettings(); }

                ImGui.Spacing();
                C.UpcomingBoardOnlyMine = CfgCheck("Only hits I have a press for", C.UpcomingBoardOnlyMine);
                if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Off shows the whole fight.");
                NextColumn();
                C.UpcomingShowHeader = CfgCheck("Header:", C.UpcomingShowHeader);
                if (C.UpcomingShowHeader)
                {
                    ImGui.SameLine(0, 10);
                    C.UpcomingHeaderTitle = CfgCheck("Name", C.UpcomingHeaderTitle);
                    ImGui.SameLine(0, 10);
                    C.UpcomingHeaderClock = CfgCheck("Clock", C.UpcomingHeaderClock);
                    ImGui.SameLine(0, 10);
                    C.UpcomingHeaderRule = CfgCheck("Underline", C.UpcomingHeaderRule);
                    ImGui.SameLine(0, 14);
                    C.UpcomingHeaderSlot = CfgCheck("Slot badge", C.UpcomingHeaderSlot);
                    Tip("Your seat and job in the header.");
                    ImGui.SameLine(0, 14);
                    C.UpcomingHeaderSync = CfgCheck("Synced note", C.UpcomingHeaderSync);
                    Tip("Shows what the clock last locked onto.");
                }
            }
            else
            {
                var count = C.UpcomingCount;
                if (Widgets.SliderInput("Lines", ref count, 1, 8)) { C.UpcomingCount = count; C.SaveSettings(); }
                ImGui.SameLine(0, 18);
                var look = C.UpcomingLookaheadSeconds;
                if (Widgets.SliderInput("Look-ahead", ref look, 5f, 90f, "%.0fs")) { C.UpcomingLookaheadSeconds = look; C.SaveSettings(); }

                var upPx = C.UpcomingFontSizePx;
                if (Widgets.SliderInput("Text size", ref upPx, 10f, 60f, "%.0f px")) { C.UpcomingFontSizePx = upPx; C.SaveSettings(); }
                ImGui.SameLine(0, 18);
                var upCol = ColorToVec4(C.OverlayColorUpcoming);
                if (ImGui.ColorEdit4("Text color", ref upCol, ImGuiColorEditFlags.NoInputs)) { C.OverlayColorUpcoming = Vec4ToColor(upCol); C.SaveSettings(); }
            }
            ImGui.EndTabItem();
        }

        if (boardStyle && TabItem("Look"))
        {
            ImGui.Spacing();
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("Colors");
            ImGui.SameLine(0, 12);
            BoardColor("Accent", "The board's base color: stripe, drain fill, header.",
                () => C.UpcomingBoardAccentColor, v => C.UpcomingBoardAccentColor = v);
            ImGui.SameLine(0, 14);
            BoardColor("Next", "Your next mit's row (gold by default).",
                () => C.UpcomingBoardNextColor, v => C.UpcomingBoardNextColor = v);
            ImGui.SameLine(0, 14);
            BoardColor("Now", "The row whose call is firing (green by default).",
                () => C.UpcomingBoardNowColor, v => C.UpcomingBoardNowColor = v);
            ImGui.SameLine(0, 16);
            if (ImGui.SmallButton("Reset colors"))
            {
                C.UpcomingBoardAccentColor = 0xFFF6823B;
                C.UpcomingBoardNextColor = 0xFF28BEFF;
                C.UpcomingBoardNowColor = 0xFF64DC64;
                C.Save();
            }

            ImGui.Spacing();
            var op = (int)MathF.Round(Math.Clamp(C.UpcomingBoardBgOpacity, 0f, 1f) * 100f);
            if (Widgets.SliderInput("Opacity", ref op, 0, 100, "%d%%")) { C.UpcomingBoardBgOpacity = op / 100f; C.SaveSettings(); }
            ImGui.SameLine(0, 18);
            var pad = C.UpcomingBoardBarPad;
            if (Widgets.SliderInput("Thickness", ref pad, 2f, 24f, "+%.0f px")) { C.UpcomingBoardBarPad = pad; C.SaveSettings(); }

            ImGui.Spacing();
            var gap = C.UpcomingBoardRowGap;
            if (Widgets.SliderInput("Row spacing", ref gap, -8f, 16f, "%.0f px")) { C.UpcomingBoardRowGap = gap; C.SaveSettings(); }
            HelpMarker("Below zero pulls the bars into each other for an overlapped look.");
            ImGui.SameLine(0, 18);
            var rnd = C.UpcomingBoardRounding;
            if (Widgets.SliderInput("Rounding", ref rnd, 0f, 12f, "%.0f px")) { C.UpcomingBoardRounding = rnd; C.SaveSettings(); }

            ImGui.Spacing();
            C.UpcomingBoardStripe = CfgCheck("Accent stripe on the left edge", C.UpcomingBoardStripe);
            NextColumn();
            C.UpcomingBoardDrain = CfgCheck("Bars drain toward the hit", C.UpcomingBoardDrain);
            if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Unticked, bars FILL toward the hit instead.");
            ImGui.EndTabItem();
        }

        if (boardStyle && TabItem("On the rows"))
        {
            ImGui.Spacing();
            // Two tidy columns, two controls per row at most.
            C.UpcomingBoardTimeText = CfgCheck("Countdown seconds", C.UpcomingBoardTimeText);
            NextColumn();
            C.UpcomingBoardShowActions = CfgCheck("Planned mits", C.UpcomingBoardShowActions);
            ImGui.Spacing();
            C.UpcomingBoardShowSeverity = CfgCheck("Severity marks (! !! !!!)", C.UpcomingBoardShowSeverity);
            NextColumn();
            C.UpcomingBoardShowType = CfgCheck("Tank buster icon", C.UpcomingBoardShowType);
            Tip("An orange shield on tank-buster rows, unless the type chip is on.");
            ImGui.Spacing();
            C.UpcomingBoardTypeChip = CfgCheck("Mechanic type chip", C.UpcomingBoardTypeChip);
            Tip("A tinted tag in its own column: Buster, Raid AOE, Enrage.");
            NextColumn();
            ImGui.BeginDisabled(!C.UpcomingBoardTypeChip);
            C.UpcomingBoardTypeChipShort = CfgCheck("Short chip labels", C.UpcomingBoardTypeChipShort);
            Tip("TB / AOE / ENR, which gives the mechanic name more room.");
            ImGui.EndDisabled();
            ImGui.Spacing();
            C.UpcomingBossPosition = CfgCheck("Boss reposition calls", C.UpcomingBossPosition);
            Tip("Counts down to the boss returning to a known spot.");
            ImGui.Spacing();
            C.UpcomingBoardPhases = CfgCheck("Phase dividers", C.UpcomingBoardPhases);
            Tip("A labelled rule where each phase begins.");
            ImGui.EndTabItem();
        }

        if (TabItem("Every duty"))
        {
            ImGui.Spacing();
            C.UniversalTimelines = CfgCheck("Run a boss timeline in every duty (no sheet needed)", C.UniversalTimelines);
            ImGui.TextDisabled("Dungeons, trials, raids: the board lists the bosses' casts even with no sheet.");
            ImGui.TextDisabled("No mits, no audio; a real sheet always takes over automatically.");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            C.LearnTimelines = CfgCheck("Learn a boss's timeline from your own pulls", C.LearnTimelines);
            ImGui.TextDisabled("Older duties have no baked timeline at all. Here the plugin watches what");
            ImGui.TextDisabled("the boss casts and builds one itself, so the board fills in next time.");
            ImGui.TextDisabled("It sharpens with every pull, and a baked timeline always wins.");

            if (C.LearnedFights.Count > 0)
            {
                ImGui.Spacing();
                var known = C.LearnedFights.Values.OrderByDescending(f => f.LastSeen).ToList();
                if (ImGui.TreeNode($"Learned so far: {known.Count} bosses###learned"))
                {
                    if (ImGui.BeginTable("##learnedfights", 4,
                            ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("Boss", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Casts", ImGuiTableColumnFlags.WidthFixed, 46);
                        ImGui.TableSetupColumn("Pulls", ImGuiTableColumnFlags.WidthFixed, 46);
                        ImGui.TableSetupColumn("##act", ImGuiTableColumnFlags.WidthFixed, 62);
                        ImGui.TableHeadersRow();
                        LearnedFight? forget = null;
                        foreach (var f in known)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(f.BossName.Length > 0 ? f.BossName : $"#{f.BossNameId}");
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(f.Casts.Count.ToString());
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(f.Pulls.ToString());
                            ImGui.TableNextColumn();
                            ImGui.PushID((int)f.BossNameId);
                            if (ImGui.SmallButton("Forget")) forget = f;
                            ImGui.PopID();
                        }
                        ImGui.EndTable();
                        if (forget != null) { TimelineLearner.Forget(C, forget.BossNameId); C.Save(); }
                    }
                    ImGui.TreePop();
                }
            }
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    // Preview toggle, unsaved so each visit starts quiet.
    private bool _nextMitsPreview;

    // A compact color row: a swatch, a label and hover help.
    private void BoardColor(string label, string help, Func<uint> get, Action<uint> set)
    {
        var v = ColorToVec4(get());
        if (ImGui.ColorEdit4(label, ref v, ImGuiColorEditFlags.NoInputs)) { set(Vec4ToColor(v)); C.Save(); }
        if (Widgets.HoveredDelayed()) ImGui.SetTooltip(help);
    }

    // Everything on this page back to the defaults.
    private void ResetNextMitsDefaults()
    {
        C.ShowUpcoming = true;
        C.UpcomingStyle = 1; C.UpcomingBoardRows = 8; C.UpcomingBoardLookaheadSeconds = 60f;
        C.UpcomingBoardWidth = 340f; C.UpcomingShowHeader = true; C.UpcomingBoardOnlyMine = false;
        C.UpcomingHeaderTitle = true; C.UpcomingHeaderClock = true; C.UpcomingHeaderRule = true;
        C.UpcomingHeaderSlot = true; C.UpcomingHeaderSync = true;
        C.UpcomingBoardTimeText = true; C.UniversalTimelines = true;
        C.UpcomingBoardAccentColor = 0xFFF6823B; C.UpcomingBoardNextColor = 0xFF28BEFF;
        C.UpcomingBoardNowColor = 0xFF64DC64; C.UpcomingBoardBgOpacity = 0.85f;
        C.UpcomingBoardRounding = 5f; C.UpcomingBoardBarPad = 8f; C.UpcomingBoardRowGap = 4f;
        C.UpcomingBoardStripe = true; C.UpcomingBoardDrain = true;
        C.UpcomingBoardShowActions = true; C.UpcomingBoardShowSeverity = true;
        C.UpcomingBoardShowType = true; C.UpcomingBossPosition = true;
        C.UpcomingBoardTypeChip = true; C.UpcomingBoardTypeChipShort = false;
        C.UpcomingBoardPhases = true;
        C.UpcomingCount = 3; C.UpcomingLookaheadSeconds = 30f;
        C.UpcomingFontSizePx = 20f; C.OverlayColorUpcoming = 0xB0FFFFFF;
        C.TimelineLocked = false; C.TimelinePosition = new Vector2(0.5f, 0.62f);
        C.Save();
        _plugin.TimelineWindow.RequestReposition();
    }

    private void DrawAudioTab()
    {
        C.AudioEnabled = CfgCheck("Enable audio cues", C.AudioEnabled);
        ImGui.TextDisabled("Plays when a call enters its warning window, once per pull, even if the overlay is hidden.");

        if (!ImGui.BeginTabBar("##audiotabs", ImGuiTabBarFlags.None)) return;

        if (TabItem("Voice"))
        {
            ImGui.Spacing();
            C.TtsEnabled = CfgCheck("Speak the action", C.TtsEnabled);

            // Engine: online neural voices, or offline Windows.
            var online = C.TtsUseEdge;
            if (ImGui.RadioButton("Online neural voices", online)) { C.TtsUseEdge = true; C.SaveSettings(); }
            ImGui.SameLine();
            if (ImGui.RadioButton("Windows voices (offline)", !online)) { C.TtsUseEdge = false; C.SaveSettings(); }
            HelpMarker("Online uses Microsoft Edge's free Read-Aloud voices (Aria, Guy, Jenny, ...). No key, "
                       + "no install, needs internet; falls back to a Windows voice if offline. Windows uses the "
                       + "voices installed on your PC.");

            if (C.TtsUseEdge)
            {
                // Snap an old saved voice onto a valid one.
                var cur = Array.Find(Audio.EdgeVoices, v => v.Id == C.TtsEdgeVoice);
                if (cur.Id == null) { cur = Audio.EdgeVoices[0]; C.TtsEdgeVoice = cur.Id; C.SaveSettings(); }
                var female = cur.Female;

                if (ImGui.RadioButton("Female", female) && !female)
                { C.TtsEdgeVoice = Audio.EdgeVoices.First(v => v.Female).Id; C.SaveSettings(); female = true; }
                ImGui.SameLine();
                if (ImGui.RadioButton("Male", !female) && female)
                { C.TtsEdgeVoice = Audio.EdgeVoices.First(v => !v.Female).Id; C.SaveSettings(); female = false; }

                var list = Audio.EdgeVoices.Where(v => v.Female == female).ToArray();
                var names = list.Select(v => v.Name).ToArray();
                var idx = Math.Max(0, Array.FindIndex(list, v => v.Id == C.TtsEdgeVoice));
                ImGui.SetNextItemWidth(220f);
                if (ImGui.Combo("Voice##edge", ref idx, names, names.Length))
                {
                    C.TtsEdgeVoice = list[idx].Id;
                    C.Save();
                }
            }
            else
            {
                // Every installed SAPI voice.
                var voices = new List<string> { "System default" };
                voices.AddRange(_plugin.Audio.VoiceNames());
                var voiceIndex = string.IsNullOrEmpty(C.TtsVoice) ? 0 : Math.Max(0, voices.IndexOf(C.TtsVoice));
                ImGui.SetNextItemWidth(280f);
                if (ImGui.Combo("Voice##sapi", ref voiceIndex, voices.ToArray(), voices.Count))
                {
                    C.TtsVoice = voiceIndex == 0 ? "" : voices[voiceIndex];
                    C.Save();
                }
                if (voices.Count <= 1)
                    ImGui.TextDisabled("No extra voices found. Add more in Windows, Time & language, Speech.");
            }

            // Advanced: paste any Edge voice id to use one outside the list.
            if (C.TtsUseEdge && ImGui.TreeNode("More voices (advanced)"))
            {
                var custom = C.TtsCustomVoice;
                ImGui.SetNextItemWidth(280f);
                if (ImGui.InputTextWithHint("##customvoice", "e.g. en-US-AvaMultilingualNeural", ref custom, 64))
                { C.TtsCustomVoice = custom; C.SaveSettings(); }
                ImGui.TextDisabled("Overrides the picker above. Full list: the Edge / Azure neural voice catalog.");
                if (!string.IsNullOrWhiteSpace(C.TtsCustomVoice) && ImGui.SmallButton("Use the picker instead"))
                { C.TtsCustomVoice = ""; C.SaveSettings(); }
                ImGui.TreePop();
            }

            var rate = C.TtsRate;
            if (Widgets.SliderInput("Speed", ref rate, -10, 10)) { C.TtsRate = rate; C.SaveSettings(); }
            ImGui.SameLine(0, 18);
            var vol = C.TtsVolume;
            if (Widgets.SliderInput("Volume", ref vol, 0, 100)) { C.TtsVolume = vol; C.SaveSettings(); }

            ImGui.Spacing();
            var mech = C.TtsSpeakMechanic;
            if (ImGui.RadioButton("Speak the mit", !mech)) { C.TtsSpeakMechanic = false; C.SaveSettings(); }
            Tip("Reads the action you press, e.g. \"Reprisal\".");
            ImGui.SameLine();
            if (ImGui.RadioButton("Speak the mechanic", mech)) { C.TtsSpeakMechanic = true; C.SaveSettings(); }

            if (ImGui.TreeNode("Advanced"))
            {
                var gap = C.TtsMinGapSeconds;
                if (Widgets.SliderInput("Min gap between cues (s)", ref gap, 0f, 5f, "%.1f", width: 220f)) { C.TtsMinGapSeconds = gap; C.SaveSettings(); }
                Tip("Skip a cue spoken this recently. 0 = never.");
                ImGui.TreePop();
            }
            ImGui.EndTabItem();
        }

        if (TabItem("Test"))
        {
            ImGui.Spacing();
            ImGui.SetNextItemWidth(220f);
            ImGui.InputTextWithHint("##testtext", "text to test...", ref _ttsTestText, 128);
            ImGui.SameLine();
            if (ImGui.Button("Speak"))
            {
                var t = string.IsNullOrWhiteSpace(_ttsTestText) ? "Reprisal" : _ttsTestText;
                var voice = C.TtsUseEdge
                    ? (string.IsNullOrWhiteSpace(C.TtsCustomVoice) ? C.TtsEdgeVoice : C.TtsCustomVoice)
                    : C.TtsVoice;
                _plugin.Audio.Speak(t, C.TtsRate, C.TtsVolume, C.TtsUseEdge, voice);
            }
            if (C.TtsUseEdge)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(first use of a voice fetches it, then it's instant)");
            }

            var status = _plugin.Audio.LastTtsStatus;
            if (!string.IsNullOrEmpty(status))
            {
                var ok = status.StartsWith("Online OK") || status == "Windows voice";
                ImGui.TextColored(ok ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudYellow, "Status: " + status);
            }
            ImGui.TextDisabled("Per line you can override the spoken text or mute the cue (the \"...\" button).");
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private string _ttsTestText = "";


    // ---- share via clipboard ----

    private void ExportFight(FightProfile fight)
    {
        try
        {
            ImGui.SetClipboardText(PlanCodes.Encode(fight));
            FlashBuiltin("Plan code copied to clipboard.");
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: export failed");
        }
    }

    // Decode and merge live in PlanCodes.Import.
    private void ImportFightFromClipboard()
    {
        var (fight, isNew, message) = PlanCodes.Import(_plugin, ImGui.GetClipboardText());
        if (fight != null && isNew)
        {
            // Drop it into the category you're viewing, and expand it.
            if (_nav == NavKind.Fights) { fight.Category = _navCategory; C.Save(); }
            _selectedFight = C.Fights.IndexOf(fight);
            _expandFightId = fight.Id;
        }
        FlashBuiltin(message);
    }

    // ---- helpers ----

    // The best-matching baked line for the reset options. `baked` is the
    // priority-resolved baseline (Builtin.BakedLinesForFight) - pass the
    // caller's already-computed one when looping over many lines, since
    // building it re-bakes the sheet and it's not cheap to redo per line.
    private MitLine? DefaultLineFor(FightProfile fight, MitLine line, IReadOnlyList<MitLine>? baked = null)
    {
        if (!Builtin.Has(fight.TerritoryId)) return null;
        baked ??= Builtin.BakedLinesForFight(fight, fight.Slot);
        if (baked.Count == 0) return null;

        var mech = line.Mechanic.Trim();
        var act = line.Action.Trim();

        MitLine? best = null;
        var bestScore = float.MaxValue;
        var bestHasMatch = false;
        foreach (var b in baked)
        {
            if (line.Custom && !Builtin.IsDeleted(fight, fight.Slot, b)) continue;

            var mMatch = mech.Length > 0 && string.Equals(b.Mechanic.Trim(), mech, StringComparison.OrdinalIgnoreCase);
            var aMatch = act.Length > 0 && string.Equals(b.Action.Trim(), act, StringComparison.OrdinalIgnoreCase);
            var hasMatch = mMatch || aMatch;
            var score = MathF.Abs(b.Time - line.Time) - (mMatch ? 1000f : 0f) - (aMatch ? 1000f : 0f);
            // Prefer a line sharing a field, then the lowest score.
            if (best == null || (hasMatch && !bestHasMatch) || (hasMatch == bestHasMatch && score < bestScore))
            {
                best = b; bestScore = score; bestHasMatch = hasMatch;
            }
        }
        // Only offer a default when a baked line really matches.
        return bestHasMatch ? best : null;
    }

    private static MitLine CloneLine(MitLine l) => new()
    {
        Time = l.Time, Mechanic = l.Mechanic, Action = l.Action,
        Jobs = new List<string>(l.Jobs), Enabled = l.Enabled,
        LeadOverride = l.LeadOverride, OffsetSeconds = l.OffsetSeconds,
        OffsetManual = l.OffsetManual, CoverUntil = l.CoverUntil,
        Tts = l.Tts, Sound = l.Sound, Color = l.Color, IconId = l.IconId
    };

    private static string Get(string[] row, int i) => i >= 0 && i < row.Length ? row[i] : "";
    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    private static string TerritoryName(uint id)
    {
        if (id == 0) return "";
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
            var row = sheet?.GetRowOrDefault(id);
            var name = row?.PlaceName.ValueNullable?.Name.ExtractText();
            return string.IsNullOrWhiteSpace(name) ? "" : name!;
        }
        catch
        {
            return "";
        }
    }

    private static Vector4 ColorToVec4(uint abgr) => Theme.V(abgr); // reverse is Vec4ToColor below

    private static uint Vec4ToColor(Vector4 v) => Widgets.ToColor(v);
}
