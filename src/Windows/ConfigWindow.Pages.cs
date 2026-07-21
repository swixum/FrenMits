using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;

namespace FrenMits.Windows;

// The individual config pages (Party Mit Recap, Combat Timer, Display, Next Mits,
// Audio) plus their small helpers, split out of the ConfigWindow partial so the
// nav/scaffold file stays navigable.
public partial class ConfigWindow
{
    // ---- Party Mit Recap --------------------------------------------------

    private void DrawPartyRecapPage()
    {
        SeparatorText("Party Mit Recap");
        ImGui.TextWrapped("After a wipe, a full recap of the pull's mitigation in its own window: the damage-downs "
                          + "on the boss (Reprisal / Feint / Addle / Dismantle) plus the party's defensive cooldowns "
                          + "(Rampart, Sacred Soil, Kerachole, ...), who used them and when, and which standard raid "
                          + "mits never landed.");
        ImGui.Spacing();

        C.RecapEnabled = CfgCheck("Enable Party Mit Recap", C.RecapEnabled);
        Tip("Off by default. When on, every pull is tracked automatically and a small \"Mit Recap\" popup offers the recap after each wipe.");

        if (C.RecapEnabled)
        {
            var locked = C.RecapPopupLocked;
            if (GreenCheckbox("Lock popup position", ref locked)) { C.RecapPopupLocked = locked; _plugin.RecapButtonWindow.RequestReposition(); C.Save(); }
            ImGui.SameLine();
            ImGui.TextDisabled("(unlock, then drag the popup to place it)");
        }

        ImGui.Spacing();
        if (ImGui.Button("Open recap window")) _plugin.RecapWindow.IsOpen = true;
        Tip("Opens the movable recap window with the last pull's data.");
        ImGui.SameLine();
        if (ImGui.Button("Preview"))
        {
            // A real pull previews better than the fake one, and must not be
            // clobbered just to drag windows around.
            if (!_plugin.Recap.HasData) _plugin.Recap.LoadSample();
            _plugin.Recap.ShowTestPopup();          // popup appears so it can be dragged
            _plugin.RecapWindow.IsOpen = true;      // window opens for placement too
        }
        Tip("Fills the recap with a sample pull and pops up the window + popup, so you can see the look and drag everything into place without wiping.");
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

        if (ImGui.BeginTabItem("Placement"))
        {
            ImGui.Spacing();
            C.CombatTimerLocked = CfgCheck("Lock position (click-through)", C.CombatTimerLocked);
            ImGui.SameLine();
            ImGui.TextDisabled(C.CombatTimerLocked ? "(unlock to drag)" : "(drag it, or use the sliders; auto-locks in combat)");

            var pos = C.CombatTimerPosition;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Horizontal", ref pos.X, 0f, 1f, "%.2f"))
            { C.CombatTimerPosition = pos; C.Save(); _plugin.CombatTimerWindow.RequestReposition(); }
            ImGui.SameLine(0, 18);
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Vertical", ref pos.Y, 0f, 1f, "%.2f"))
            { C.CombatTimerPosition = pos; C.Save(); _plugin.CombatTimerWindow.RequestReposition(); }
            ImGui.SameLine(0, 12);
            if (ImGui.SmallButton("Center top"))
            {
                C.CombatTimerPosition = new Vector2(0.5f, 0.08f);
                C.Save();
                _plugin.CombatTimerWindow.RequestReposition();
            }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Font"))
        {
            ImGui.Spacing();
            var fonts = FontManager.FamilyNames;
            var fIdx = Math.Max(0, Array.IndexOf(fonts, C.CombatTimerFontFamily));
            ImGui.SetNextItemWidth(200f);
            if (ImGui.Combo("Font", ref fIdx, fonts, fonts.Length)) { C.CombatTimerFontFamily = fonts[fIdx]; C.Save(); }
            ImGui.SameLine(0, 12);
            var bold = C.CombatTimerFontBold;
            if (GreenCheckbox("Bold", ref bold)) { C.CombatTimerFontBold = bold; C.Save(); }
            ImGui.SameLine();
            var italic = C.CombatTimerFontItalic;
            if (GreenCheckbox("Italic", ref italic)) { C.CombatTimerFontItalic = italic; C.Save(); }
            if (C.CombatTimerFontFamily == "Default" && (C.CombatTimerFontBold || C.CombatTimerFontItalic))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(pick a font)");
            }
            var px = C.CombatTimerFontSizePx;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Text size", ref px, 12f, 120f, "%.0f px")) { C.CombatTimerFontSizePx = px; C.Save(); }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Colors"))
        {
            ImGui.Spacing();
            var col = ColorToVec4(C.CombatTimerColor);
            if (ImGui.ColorEdit4("Text color", ref col, ImGuiColorEditFlags.NoInputs)) { C.CombatTimerColor = Vec4ToColor(col); C.Save(); }

            C.CombatTimerShowBackground = CfgCheck("Draw a background box", C.CombatTimerShowBackground);
            if (C.CombatTimerShowBackground)
            {
                ImGui.SameLine(0, 14);
                var bg = ColorToVec4(C.CombatTimerBackgroundColor);
                if (ImGui.ColorEdit4("Color##ctbg", ref bg, ImGuiColorEditFlags.NoInputs)) { C.CombatTimerBackgroundColor = Vec4ToColor(bg); C.Save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Drag the alpha channel down for a translucent box.");
            }
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawDisplayTab()
    {
        // One-click reset of everything on this tab. To preview while you adjust,
        // use the "Test" toggle in the header (always visible).
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, "Reset display")) ResetDisplayDefaults();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset every setting on this tab to defaults.");

        if (!ImGui.BeginTabBar("##displaytabs", ImGuiTabBarFlags.None)) return;

        if (ImGui.BeginTabItem("General"))
        {
            ImGui.Spacing();
            SeparatorText("Accessibility");
            C.ColorblindMode = CfgCheck("Colorblind-safe status colors", C.ColorblindMode);
            Theme.Colorblind = C.ColorblindMode; // keep the live palette in sync with the setting
            Tip("Swaps the green / amber / red status colors (recap, coverage counts, plan check) "
                + "for an Okabe-Ito set - bluish-green, orange, reddish-purple - that stays distinct "
                + "under the common forms of color blindness.");

            ImGui.Spacing();
            SeparatorText("Timing");
            var warn = C.WarningSeconds;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Show ahead", ref warn, 1f, 12f, "%.1fs")) { C.WarningSeconds = warn; C.Save(); }
            Tip("How early a call appears before its mit time. Per-line leads override this.");
            ImGui.SameLine(0, 18);
            var hold = C.HoldSeconds;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Hold on screen", ref hold, 0f, 6f, "%.1fs")) { C.HoldSeconds = hold; C.Save(); }
            Tip("How long a call stays up after its time passes.");
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Placement"))
        {
            ImGui.Spacing();
            C.OverlayLocked = CfgCheck("Lock overlay (click-through)", C.OverlayLocked);
            ImGui.SameLine();
            ImGui.TextDisabled(C.OverlayLocked ? "(unlock to drag)" : "(drag it, or use the sliders; auto-locks in combat)");

            var pos = C.OverlayPosition;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Horizontal", ref pos.X, 0f, 1f, "%.2f"))
            { C.OverlayPosition = pos; C.Save(); _plugin.OverlayWindow.RequestReposition(); }
            ImGui.SameLine(0, 18);
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Vertical", ref pos.Y, 0f, 1f, "%.2f"))
            { C.OverlayPosition = pos; C.Save(); _plugin.OverlayWindow.RequestReposition(); }
            ImGui.SameLine(0, 12);
            if (ImGui.SmallButton("Center"))
            {
                C.OverlayPosition = new Vector2(0.5f, 0.35f);
                C.Save();
                _plugin.OverlayWindow.RequestReposition();
            }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Font"))
        {
            ImGui.Spacing();
            var fonts = FontManager.FamilyNames;
            var fIdx = Math.Max(0, Array.IndexOf(fonts, C.OverlayFontFamily));
            ImGui.SetNextItemWidth(200f);
            if (ImGui.Combo("Font", ref fIdx, fonts, fonts.Length)) { C.OverlayFontFamily = fonts[fIdx]; C.Save(); }
            ImGui.SameLine(0, 12);
            var bold = C.OverlayFontBold;
            if (GreenCheckbox("Bold", ref bold)) { C.OverlayFontBold = bold; C.Save(); }
            ImGui.SameLine();
            var italic = C.OverlayFontItalic;
            if (GreenCheckbox("Italic", ref italic)) { C.OverlayFontItalic = italic; C.Save(); }
            if (C.OverlayFontFamily == "Default" && (C.OverlayFontBold || C.OverlayFontItalic))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(pick a font)");
            }
            var callPx = C.OverlayFontSizePx;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Call size", ref callPx, 12f, 120f, "%.0f px")) { C.OverlayFontSizePx = callPx; C.Save(); }
            ImGui.SameLine(0, 18);
            var align = C.OverlayTextAlign;
            ImGui.SetNextItemWidth(110f);
            if (ImGui.Combo("Align", ref align, new[] { "Left", "Center", "Right" }, 3))
            { C.OverlayTextAlign = align; C.Save(); }
            // The timeline's own text size + color live with the timeline toggle in
            // the "Next-mits timeline" section below.
            if (C.ShowAbilityIcon)
            {
                var iconScale = C.IconScale;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderFloat("Icon size", ref iconScale, 0.4f, 1.5f, "%.2fx")) { C.IconScale = iconScale; C.Save(); }
            }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Content"))
        {
            ImGui.Spacing();
            if (ImGui.TreeNode("Advanced format"))
            {
                var fmt = C.HeadlineFormat;
                ImGui.SetNextItemWidth(280f);
                if (ImGui.InputText("Call format", ref fmt, 128)) { C.HeadlineFormat = fmt; C.Save(); }
                ImGui.TextDisabled("Placeholders: {action} {mechanic} {time} {count} {remaining}");
                var suffix = C.ActiveSuffix;
                ImGui.SetNextItemWidth(280f);
                if (ImGui.InputText("\"NOW\" suffix", ref suffix, 64)) { C.ActiveSuffix = suffix; C.Save(); }
                ImGui.TreePop();
            }

            ImGui.Spacing();
            if (ImGui.BeginTable("##texttoggles", 2, ImGuiTableFlags.SizingStretchSame))
            {
                C.ShowAbilityIcon = GridCheck("Ability icon", C.ShowAbilityIcon,
                    "Matched from the action name; pin one per line with the \"...\" button.");
                C.ShowRadialRing = GridCheck("Radial ring", C.ShowRadialRing,
                    "A depleting countdown ring around the call icon.");
                C.ShowMechanicLine = GridCheck("Mechanic 2nd line", C.ShowMechanicLine);
                C.ShowCountdownNumber = GridCheck("Countdown number", C.ShowCountdownNumber);
                C.TextShadow = GridCheck("Drop shadow", C.TextShadow,
                    "Improves readability over busy backgrounds.");
                C.CooldownAwareCalls = GridCheck("Cooldown warnings", C.CooldownAwareCalls,
                    "Reddens the main call ([CD Ns]) and dims it in the upcoming list when your mit is still on cooldown past the call time. Your job's mits only.");
                C.ShowDtrBar = GridCheck("Server-bar next mit", C.ShowDtrBar,
                    "Shows the next mit on the server-info bar.");
                C.ShowMitBar = GridCheck("Active-mits bar", C.ShowMitBar,
                    "A row of your active defensive buffs with seconds remaining, tinted by mit type.");
                ImGui.EndTable();
            }
            if (C.ShowMitBar)
            {
                var locked = C.MitBarLocked;
                if (GreenCheckbox("Lock active-mits position", ref locked)) { C.MitBarLocked = locked; _plugin.MitBarWindow.RequestReposition(); C.Save(); }
                ImGui.TextDisabled("Auto-locks in combat; move it out of combat or with Live preview.");
            }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Colors"))
        {
            ImGui.Spacing();
            var imminent = ColorToVec4(C.OverlayColorImminent);
            if (ImGui.ColorEdit4("Counting down", ref imminent, ImGuiColorEditFlags.NoInputs)) { C.OverlayColorImminent = Vec4ToColor(imminent); C.Save(); }
            ImGui.SameLine(0, 14);
            var active = ColorToVec4(C.OverlayColorActive);
            if (ImGui.ColorEdit4("NOW", ref active, ImGuiColorEditFlags.NoInputs)) { C.OverlayColorActive = Vec4ToColor(active); C.Save(); }
            ImGui.SameLine(0, 14);
            var mechCol = ColorToVec4(C.OverlayColorMechanic);
            if (ImGui.ColorEdit4("Mechanic", ref mechCol, ImGuiColorEditFlags.NoInputs)) { C.OverlayColorMechanic = Vec4ToColor(mechCol); C.Save(); }
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
                if (ImGui.ColorEdit4("Party mit", ref party, ImGuiColorEditFlags.NoInputs)) { C.MitColorParty = Vec4ToColor(party); C.Save(); }
                ImGui.SameLine(0, 14);
                var tank = ColorToVec4(C.MitColorTank);
                if (ImGui.ColorEdit4("Tank", ref tank, ImGuiColorEditFlags.NoInputs)) { C.MitColorTank = Vec4ToColor(tank); C.Save(); }
                ImGui.SameLine(0, 14);
                var personal = ColorToVec4(C.MitColorPersonal);
                if (ImGui.ColorEdit4("Personal", ref personal, ImGuiColorEditFlags.NoInputs)) { C.MitColorPersonal = Vec4ToColor(personal); C.Save(); }
            }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Box & bar"))
        {
            ImGui.Spacing();
            SeparatorText("Countdown bar");
            C.ShowProgressBar = CfgCheck("Countdown bar under the call", C.ShowProgressBar);
            if (C.ShowProgressBar)
            {
                ImGui.SameLine(0, 14);
                var barH = C.ProgressBarHeight;
                ImGui.SetNextItemWidth(140f);
                if (ImGui.SliderFloat("Height", ref barH, 2f, 24f, "%.0f px")) { C.ProgressBarHeight = barH; C.Save(); }
            }
            C.PulseWhenImminent = CfgCheck("Pulse the text in the last second", C.PulseWhenImminent);

            ImGui.Spacing();
            SeparatorText("Background");
            C.ShowBackground = CfgCheck("Draw a background box", C.ShowBackground);
            if (C.ShowBackground)
            {
                ImGui.SameLine(0, 14);
                var bg = ColorToVec4(C.BackgroundColor);
                if (ImGui.ColorEdit4("Color##overlaybg", ref bg, ImGuiColorEditFlags.NoInputs)) { C.BackgroundColor = Vec4ToColor(bg); C.Save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Drag the alpha channel down for a translucent box.");
            }
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    // ---- Next Mits board ---------------------------------------------------

    private void DrawNextMitsPage()
    {
        SeparatorText("Next Mits & Timeline");
        ImGui.TextWrapped("Every upcoming mechanic as a countdown bar, with the planned mits underneath. "
                          + "Gold is your next press; green means press it now, in step with the main call.");
        ImGui.Spacing();

        // One control strip: the things you touch most, on two tight rows.
        C.ShowUpcoming = CfgCheck("Show the window", C.ShowUpcoming);
        ImGui.SameLine(0, 16);
        if (ImGuiComponents.IconButtonWithText(_nextMitsPreview ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play,
                _nextMitsPreview ? "Stop preview" : "Preview"))
            _nextMitsPreview = !_nextMitsPreview;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Plays a looping Dancing Mad sample in the real window, right where it sits in fights.\n"
                             + "Unlock and drag it to place it; every change below shows there live.");
        ImGui.SameLine(0, 8);
        if (ImGui.SmallButton("Reset position"))
        {
            C.TimelinePosition = new Vector2(0.5f, 0.62f);
            C.Save();
            _plugin.TimelineWindow.RequestReposition();
        }
        ImGui.SameLine(0, 8);
        if (ImGui.SmallButton("Reset all")) ResetNextMitsDefaults();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Everything on this page back to the FrenMits defaults.");

        C.TimelineLocked = CfgCheck("Lock the window (click-through)", C.TimelineLocked);
        ImGui.SameLine();
        ImGui.TextDisabled(C.TimelineLocked ? "(unlock to drag)" : "(drag it to move; auto-locks in combat)");

        // Precise placement too, for anyone who'd rather not drag.
        var tpos = C.TimelinePosition;
        ImGui.SetNextItemWidth(150f);
        if (ImGui.SliderFloat("Horizontal##tl", ref tpos.X, 0f, 1f, "%.2f")) { C.TimelinePosition = tpos; C.Save(); }
        ImGui.SameLine(0, 18);
        ImGui.SetNextItemWidth(150f);
        if (ImGui.SliderFloat("Vertical##tl", ref tpos.Y, 0f, 1f, "%.2f")) { C.TimelinePosition = tpos; C.Save(); }

        if (_nextMitsPreview) _plugin.TimelineWindow.PingScreenPreview();
        ImGui.Spacing();

        if (!C.ShowUpcoming) return;

        var boardStyle = Math.Clamp(C.UpcomingStyle, 0, 1) == 1;

        if (!ImGui.BeginTabBar("##nmtabs", ImGuiTabBarFlags.None)) return;

        if (ImGui.BeginTabItem("Layout"))
        {
            ImGui.Spacing();
            var style = Math.Clamp(C.UpcomingStyle, 0, 1);
            var styles = new[]
            {
                "Compact list (just your next calls)",
                "Mechanic board (every hit, countdown bars)",
            };
            ImGui.SetNextItemWidth(320f);
            if (ImGui.Combo("##nmstyle", ref style, styles, styles.Length)) { C.UpcomingStyle = style; C.Save(); }
            boardStyle = style == 1;

            if (boardStyle)
            {
                var brows = C.UpcomingBoardRows;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderInt("Rows", ref brows, 3, 12)) { C.UpcomingBoardRows = brows; C.Save(); }
                ImGui.SameLine(0, 18);
                var blook = C.UpcomingBoardLookaheadSeconds;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderFloat("Look-ahead", ref blook, 15f, 180f, "%.0fs")) { C.UpcomingBoardLookaheadSeconds = blook; C.Save(); }
                HelpMarker("How many bars at once, and how far ahead the board looks: bars are full at that edge, empty at the hit.");

                ImGui.Spacing();
                var bw = C.UpcomingBoardWidth;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderFloat("Bar width", ref bw, 220f, 560f, "%.0f px")) { C.UpcomingBoardWidth = bw; C.Save(); }
                ImGui.SameLine(0, 18);
                var upPx = C.UpcomingFontSizePx;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderFloat("Text size", ref upPx, 10f, 60f, "%.0f px")) { C.UpcomingFontSizePx = upPx; C.Save(); }

                ImGui.Spacing();
                C.UpcomingBoardOnlyMine = CfgCheck("Only hits I have a press for", C.UpcomingBoardOnlyMine);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Off = the whole fight shows, with your mits highlighted on their rows.");
                ImGui.SameLine(300f);
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
                    Tip("Your seat and job ('T1 · WAR') as a small badge in the header.");
                    ImGui.SameLine(0, 14);
                    C.UpcomingHeaderSync = CfgCheck("Synced note", C.UpcomingHeaderSync);
                    Tip("Shows what the clock last locked onto for a few seconds after each resync.");
                }
            }
            else
            {
                var count = C.UpcomingCount;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderInt("Lines", ref count, 1, 8)) { C.UpcomingCount = count; C.Save(); }
                ImGui.SameLine(0, 18);
                var look = C.UpcomingLookaheadSeconds;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderFloat("Look-ahead", ref look, 5f, 90f, "%.0fs")) { C.UpcomingLookaheadSeconds = look; C.Save(); }

                var upPx = C.UpcomingFontSizePx;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderFloat("Text size", ref upPx, 10f, 60f, "%.0f px")) { C.UpcomingFontSizePx = upPx; C.Save(); }
                ImGui.SameLine(0, 18);
                var upCol = ColorToVec4(C.OverlayColorUpcoming);
                if (ImGui.ColorEdit4("Text color", ref upCol, ImGuiColorEditFlags.NoInputs)) { C.OverlayColorUpcoming = Vec4ToColor(upCol); C.Save(); }
            }
            ImGui.EndTabItem();
        }

        if (boardStyle && ImGui.BeginTabItem("Look"))
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
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderInt("Opacity", ref op, 0, 100, "%d%%")) { C.UpcomingBoardBgOpacity = op / 100f; C.Save(); }
            ImGui.SameLine(0, 18);
            var pad = C.UpcomingBoardBarPad;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Thickness", ref pad, 2f, 24f, "+%.0f px")) { C.UpcomingBoardBarPad = pad; C.Save(); }

            ImGui.Spacing();
            var gap = C.UpcomingBoardRowGap;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Row spacing", ref gap, -8f, 16f, "%.0f px")) { C.UpcomingBoardRowGap = gap; C.Save(); }
            HelpMarker("Below zero pulls the bars into each other for an overlapped look.");
            ImGui.SameLine(0, 18);
            var rnd = C.UpcomingBoardRounding;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Rounding", ref rnd, 0f, 12f, "%.0f px")) { C.UpcomingBoardRounding = rnd; C.Save(); }

            ImGui.Spacing();
            C.UpcomingBoardStripe = CfgCheck("Accent stripe on the left edge", C.UpcomingBoardStripe);
            ImGui.SameLine(300f);
            C.UpcomingBoardDrain = CfgCheck("Bars drain toward the hit", C.UpcomingBoardDrain);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Unticked, bars FILL toward the hit instead.");
            ImGui.EndTabItem();
        }

        if (boardStyle && ImGui.BeginTabItem("On the rows"))
        {
            ImGui.Spacing();
            // Two tidy columns, each row on its own line with a little breathing
            // room. (The left column splits at 300px; keep two per row, no more.)
            C.UpcomingBoardTimeText = CfgCheck("Countdown seconds", C.UpcomingBoardTimeText);
            ImGui.SameLine(300f);
            C.UpcomingBoardShowActions = CfgCheck("Planned mits", C.UpcomingBoardShowActions);
            ImGui.Spacing();
            C.UpcomingBoardShowNotes = CfgCheck("Sheet notes (highlighted row)", C.UpcomingBoardShowNotes);
            ImGui.SameLine(300f);
            C.UpcomingBoardShowSeverity = CfgCheck("Severity marks (! !! !!!)", C.UpcomingBoardShowSeverity);
            ImGui.Spacing();
            C.UpcomingBoardShowType = CfgCheck("Hit-type icons (raidwide / buster)", C.UpcomingBoardShowType);
            Tip("A small icon on each row: cyan people = raidwide, orange shield = tank buster.");
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Every duty"))
        {
            ImGui.Spacing();
            C.UniversalTimelines = CfgCheck("Run a boss timeline in every duty (no sheet needed)", C.UniversalTimelines);
            ImGui.TextDisabled("Dungeons, trials, raids: the board lists the bosses' casts even with no sheet.");
            ImGui.TextDisabled("No mits, no audio; a real sheet always takes over automatically.");
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    // On-screen preview toggle for the Next Mits page. Starts OFF; the Play
    // button starts it. Not saved - each settings visit starts quiet.
    private bool _nextMitsPreview;

    // A compact color row: swatch-style picker plus a label and hover help.
    private void BoardColor(string label, string help, Func<uint> get, Action<uint> set)
    {
        var v = ColorToVec4(get());
        if (ImGui.ColorEdit4(label, ref v, ImGuiColorEditFlags.NoInputs)) { set(Vec4ToColor(v)); C.Save(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(help);
    }

    // Everything on the Next Mits page back to the FrenMits defaults.
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
        C.UpcomingBoardShowActions = true; C.UpcomingBoardShowNotes = true; C.UpcomingBoardShowSeverity = true;
        C.UpcomingBoardShowType = true;
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

        if (ImGui.BeginTabItem("Voice"))
        {
            ImGui.Spacing();
            C.TtsEnabled = CfgCheck("Speak the action", C.TtsEnabled);

            // Engine: online neural (Edge) for the nice custom voices, or offline Windows.
            var online = C.TtsUseEdge;
            if (ImGui.RadioButton("Online neural voices", online)) { C.TtsUseEdge = true; C.Save(); }
            ImGui.SameLine();
            if (ImGui.RadioButton("Windows voices (offline)", !online)) { C.TtsUseEdge = false; C.Save(); }
            HelpMarker("Online uses Microsoft Edge's free Read-Aloud voices (Aria, Guy, Jenny, ...). No key, "
                       + "no install, needs internet; falls back to a Windows voice if offline. Windows uses the "
                       + "voices installed on your PC.");

            if (C.TtsUseEdge)
            {
                // Snap any unknown/old saved voice (e.g. the removed child voice) to a valid one.
                var cur = Array.Find(Audio.EdgeVoices, v => v.Id == C.TtsEdgeVoice);
                if (cur.Id == null) { cur = Audio.EdgeVoices[0]; C.TtsEdgeVoice = cur.Id; C.Save(); }
                var female = cur.Female;

                if (ImGui.RadioButton("Female", female) && !female)
                { C.TtsEdgeVoice = Audio.EdgeVoices.First(v => v.Female).Id; C.Save(); female = true; }
                ImGui.SameLine();
                if (ImGui.RadioButton("Male", !female) && female)
                { C.TtsEdgeVoice = Audio.EdgeVoices.First(v => !v.Female).Id; C.Save(); female = false; }

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
                // Every installed SAPI voice; female voices (Zira, Hazel) appear if installed.
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
                { C.TtsCustomVoice = custom; C.Save(); }
                ImGui.TextDisabled("Overrides the picker above. Full list: the Edge / Azure neural voice catalog.");
                if (!string.IsNullOrWhiteSpace(C.TtsCustomVoice) && ImGui.SmallButton("Use the picker instead"))
                { C.TtsCustomVoice = ""; C.Save(); }
                ImGui.TreePop();
            }

            var rate = C.TtsRate;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderInt("Speed", ref rate, -10, 10)) { C.TtsRate = rate; C.Save(); }
            ImGui.SameLine(0, 18);
            var vol = C.TtsVolume;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderInt("Volume", ref vol, 0, 100)) { C.TtsVolume = vol; C.Save(); }

            ImGui.Spacing();
            var mech = C.TtsSpeakMechanic;
            if (ImGui.RadioButton("Speak the mit", !mech)) { C.TtsSpeakMechanic = false; C.Save(); }
            Tip("Reads the action you press, e.g. \"Reprisal\". Override the exact words per line with the \"...\" button.");
            ImGui.SameLine();
            if (ImGui.RadioButton("Speak the mechanic", mech)) { C.TtsSpeakMechanic = true; C.Save(); }

            if (ImGui.TreeNode("Advanced"))
            {
                var gap = C.TtsMinGapSeconds;
                ImGui.SetNextItemWidth(220f);
                if (ImGui.SliderFloat("Min gap between cues (s)", ref gap, 0f, 5f, "%.1f")) { C.TtsMinGapSeconds = gap; C.Save(); }
                Tip("Skips a cue if one was spoken within this many seconds. 0 = never skip.");
                ImGui.TreePop();
            }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Test"))
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

    // ---- per-line overrides popup ---------------------------------------

    private string _iconSearch = "";
    private int _iconBrowseStart = 405; // action icons start around here
    private const int IconPage = 64;

    private void DrawLineOptionsPopup(MitLine line)
    {
        if (!ImGui.BeginPopup("lineopt")) return;

        var fight = (_selectedFight >= 0 && _selectedFight < C.Fights.Count) ? C.Fights[_selectedFight] : null;
        if (fight != null)
        {
            var idx = fight.Lines.IndexOf(line);
            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp) && idx > 0)
            {
                (fight.Lines[idx - 1], fight.Lines[idx]) = (fight.Lines[idx], fight.Lines[idx - 1]);
                C.Save();
            }
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown) && idx >= 0 && idx < fight.Lines.Count - 1)
            {
                (fight.Lines[idx + 1], fight.Lines[idx]) = (fight.Lines[idx], fight.Lines[idx + 1]);
                C.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("reorder");
            ImGui.Separator();
        }

        SeparatorText("Icon");
        var resolved = Icons.For(line, _plugin.ActiveJobAbbreviation());
        Icons.Draw(resolved, new Vector2(40, 40));
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextUnformatted(line.IconId != 0 ? $"pinned (#{line.IconId})"
            : (resolved != 0 ? "auto (action / status / keyword)" : "none"));
        if (ImGui.SmallButton("Use auto")) { line.IconId = 0; C.Save(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("Potion")) { line.IconId = Icons.PotionIconFor(line); C.Save(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Pin the potion (Gemdraught) icon to this line.");
        ImGui.SameLine();
        var iconId = (int)line.IconId;
        ImGui.SetNextItemWidth(110f);
        if (ImGui.InputInt("##iconid", ref iconId)) { line.IconId = (uint)Math.Max(0, iconId); C.Save(); }
        ImGui.SameLine();
        ImGui.TextDisabled("id");
        ImGui.EndGroup();

        // Search actions + statuses by name -> clickable icon grid.
        ImGui.SetNextItemWidth(240f);
        ImGui.InputTextWithHint("##iconsearch", "search actions & statuses...", ref _iconSearch, 64);
        if (!string.IsNullOrWhiteSpace(_iconSearch))
        {
            var n = 0;
            foreach (var (name, ic) in Icons.Search(_iconSearch, 40))
            {
                if (Icons.Button(ic, new Vector2(32, 32), $"##s{ic}_{n}")) { line.IconId = ic; C.Save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{name}  (#{ic})");
                if (++n % 8 != 0) ImGui.SameLine();
            }
            ImGui.NewLine();
        }

        // Quick palette: the keyword "bucket" (Bait, Stun, Bind, Heal, Knockback …).
        // Click one to pin it. Typing the same word on a line auto-fills it too.
        if (ImGui.TreeNode("Common mechanic icons"))
        {
            var n = 0;
            foreach (var (label, ic) in Icons.Common())
            {
                if (Icons.Button(ic, new Vector2(32, 32), $"##c{ic}_{n}")) { line.IconId = ic; C.Save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{label}  (#{ic})");
                if (++n % 8 != 0) ImGui.SameLine();
            }
            ImGui.NewLine();
            ImGui.TreePop();
        }

        // Browse any icon by id (paged grid) — pick literally anything.
        if (ImGui.TreeNode("Browse all icons"))
        {
            ImGui.SetNextItemWidth(120f);
            ImGui.InputInt("Start id", ref _iconBrowseStart, 8, 64);
            _iconBrowseStart = Math.Clamp(_iconBrowseStart, 0, 250000);
            ImGui.SameLine();
            if (ImGui.ArrowButton("##icoprev", ImGuiDir.Left)) _iconBrowseStart = Math.Max(0, _iconBrowseStart - IconPage);
            ImGui.SameLine();
            if (ImGui.ArrowButton("##iconext", ImGuiDir.Right)) _iconBrowseStart += IconPage;
            ImGui.SameLine();
            ImGui.TextDisabled($"{_iconBrowseStart}-{_iconBrowseStart + IconPage - 1}");

            if (ImGui.BeginChild("##iconbrowse", new Vector2(0, 220), true))
            {
                for (var k = 0; k < IconPage; k++)
                {
                    var id = (uint)(_iconBrowseStart + k);
                    if (Icons.Button(id, new Vector2(32, 32), $"##b{id}")) { line.IconId = id; C.Save(); }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"#{id}");
                    if ((k + 1) % 8 != 0) ImGui.SameLine();
                }
            }
            ImGui.EndChild();
            ImGui.TreePop();
        }

        SeparatorText("Overrides (0 / empty = global)");

        var lead = line.LeadOverride;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputFloat("Warning lead (s)", ref lead, 0.5f, 1f, "%.1f"))
        {
            line.LeadOverride = MathF.Max(0f, lead);
            C.Save();
        }

        var off = line.OffsetSeconds;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputFloat("Offset (s)", ref off, 0.5f, 1f, "%.1f"))
        {
            line.OffsetSeconds = Math.Clamp(off, -30f, 30f);
            C.Save();
            _plugin.SheetViewWindow.MarkPlanDirty();
        }
        Tip("Shift only THIS call: + fires it earlier, - later. The plan time stays put and "
            + "resync can't cancel it. A big + offset on a very early call can push it before "
            + "the pull starts (it won't fire). Also editable in the ±s column.");

        var tts = line.Tts;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.InputText("Speak instead", ref tts, 128)) { line.Tts = tts; C.Save(); }
        ImGui.TextDisabled("Empty = speak the action.");

        var sound = line.Sound;
        if (GreenCheckbox("Play audio cue for this line", ref sound)) { line.Sound = sound; C.Save(); }

        var useColor = line.Color != 0;
        if (GreenCheckbox("Custom text color", ref useColor))
        {
            line.Color = useColor ? 0xFF55FFFF : 0u;
            C.Save();
        }
        if (line.Color != 0)
        {
            var col = ColorToVec4(line.Color);
            if (ImGui.ColorEdit4("Color", ref col)) { line.Color = Vec4ToColor(col); C.Save(); }
        }

        ImGui.EndPopup();
    }

    // ---- share via clipboard --------------------------------------------

    private void ExportFight(FightProfile fight)
    {
        try
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(fight);
            // FRENMITS2 = gzip-compressed, so a full raid plan is a much shorter,
            // paste-friendly code to share. (FRENMITS1 plain base64 still imports.)
            var raw = System.Text.Encoding.UTF8.GetBytes(json);
            using var ms = new System.IO.MemoryStream();
            using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal))
                gz.Write(raw, 0, raw.Length);
            ImGui.SetClipboardText("FRENMITS2:" + Convert.ToBase64String(ms.ToArray()));
            FlashBuiltin("Plan code copied to clipboard.");
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: export failed");
        }
    }

    // Decode + merge live in Plugin.ImportPlanCode (shared with the Sheet View's
    // Import button); this wrapper adds the fight-page niceties on top.
    private void ImportFightFromClipboard()
    {
        var (fight, isNew, message) = _plugin.ImportPlanCode(ImGui.GetClipboardText());
        if (fight != null && isNew)
        {
            // Drop it into the category you're currently viewing and expand it.
            if (_nav == NavKind.Fights) { fight.Category = _navCategory; C.Save(); }
            _selectedFight = C.Fights.IndexOf(fight);
            _expandFightId = fight.Id;
        }
        FlashBuiltin(message);
    }

    // ---- helpers ---------------------------------------------------------

    // The best-matching baked built-in line, for the right-click "reset to default"
    // options on time / mechanic / action. Scored by time proximity with a strong
    // bonus for a matching mechanic and/or action — so whichever field you typo'd,
    // the OTHER fields (and the time) still pin the right baked line. Null when
    // there's no baked default (custom / tank / potion lines, non-built-in fights).
    private MitLine? DefaultLineFor(FightProfile fight, MitLine line)
    {
        if (!Builtin.Has(fight.TerritoryId)) return null;
        var baked = Builtin.BuildLines(fight.TerritoryId, fight.Slot);
        if (baked.Count == 0) return null;

        var mech = line.Mechanic.Trim();
        var act = line.Action.Trim();

        MitLine? best = null;
        var bestScore = float.MaxValue;
        var bestHasMatch = false;
        foreach (var b in baked)
        {
            var mMatch = mech.Length > 0 && string.Equals(b.Mechanic.Trim(), mech, StringComparison.OrdinalIgnoreCase);
            var aMatch = act.Length > 0 && string.Equals(b.Action.Trim(), act, StringComparison.OrdinalIgnoreCase);
            var hasMatch = mMatch || aMatch;
            var score = MathF.Abs(b.Time - line.Time) - (mMatch ? 1000f : 0f) - (aMatch ? 1000f : 0f);
            // Prefer any line that shares a field; among those, the lowest score.
            if (best == null || (hasMatch && !bestHasMatch) || (hasMatch == bestHasMatch && score < bestScore))
            {
                best = b; bestScore = score; bestHasMatch = hasMatch;
            }
        }
        // Only offer a default when a baked line actually corresponds to this one
        // (shares its mechanic or action) — not just the nearest in time.
        return bestHasMatch ? best : null;
    }

    private static string Ellipsis(string s, int max) => s.Length > max ? s[..max] + "..." : s;

    private static MitLine CloneLine(MitLine l) => new()
    {
        Time = l.Time, Mechanic = l.Mechanic, Action = l.Action,
        Jobs = new List<string>(l.Jobs), Enabled = l.Enabled,
        LeadOverride = l.LeadOverride, OffsetSeconds = l.OffsetSeconds,
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

    private static uint Vec4ToColor(Vector4 v) =>
        ((uint)(Math.Clamp(v.W, 0, 1) * 255) << 24) |
        ((uint)(Math.Clamp(v.Z, 0, 1) * 255) << 16) |
        ((uint)(Math.Clamp(v.Y, 0, 1) * 255) << 8) |
        (uint)(Math.Clamp(v.X, 0, 1) * 255);
}
