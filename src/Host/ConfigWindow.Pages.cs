using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;

namespace FrenMits.Host;

// The individual config pages plus their small helpers. Every page opens with
// the same title row, then a run of setting rows. Pages with more than about
// eight settings show the few that matter and keep the rest behind "All".
public partial class ConfigWindow
{
    // ---- shared pieces ----

    // Where each overlay ships, so Reset puts it back exactly there.
    private static readonly Vector2 CallHome = new(0.5f, 0.35f);
    private static readonly Vector2 BoardHome = new(0.5f, 0.62f);
    private static readonly Vector2 TimerHome = new(0.5f, 0.08f);
    private static readonly Vector2 PrepHome = new(0.5f, 0.72f);
    private static readonly Vector2 MeterHome = new(0.8f, 0.72f);

    // Left / Center / Right move an overlay sideways and keep its height.
    // Every overlay places itself this way.
    private static readonly (string Name, float X)[] XPresets =
        { ("Left", 0.18f), ("Center", 0.5f), ("Right", 0.82f) };

    private static bool PositionRow(ref Vector2 pos, Vector2 home)
    {
        var w = Widgets.SmallWidth("Left", "Center", "Right", "Reset") + Theme.S(8f);
        Widgets.RowBegin("Position", "Drag it in game, or use these", w, ctlHeight: Widgets.SmallHeight);
        var moved = false;
        Widgets.SegmentBegin();
        for (var i = 0; i < XPresets.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (Widgets.Segment(XPresets[i].Name + "##pos", MathF.Abs(pos.X - XPresets[i].X) < 0.02f))
            { pos.X = XPresets[i].X; moved = true; }
        }
        Widgets.SegmentEnd();
        ImGui.SameLine(0, Theme.S(8f));
        if (ImGui.SmallButton("Reset##pos")) { pos = home; moved = true; }
        Widgets.RowEnd();
        return moved;
    }

    // The exact spot, for anyone who would rather not drag.
    private static bool NudgeRow(ref Vector2 pos)
    {
        var w = Theme.S(150f);
        Widgets.RowBegin("Nudge", "", w, sub: true);
        var hit = false;
        ImGui.SetNextItemWidth(Theme.S(70f));
        if (ImGui.DragFloat("##nudgex", ref pos.X, 0.005f, 0f, 1f, "%.2f", ImGuiSliderFlags.AlwaysClamp)) hit = true;
        ImGui.SameLine(0, Theme.S(8f));
        ImGui.SetNextItemWidth(Theme.S(70f));
        if (ImGui.DragFloat("##nudgey", ref pos.Y, 0.005f, 0f, 1f, "%.2f", ImGuiSliderFlags.AlwaysClamp)) hit = true;
        Widgets.RowEnd();
        return hit;
    }

    // Family, bold and italic on one row, since they are one decision.
    private bool FontRow(ref string family, ref bool bold, ref bool italic)
    {
        var fonts = FontManager.FamilyNames;
        var idx = Math.Max(0, Array.IndexOf(fonts, family));
        var w = Theme.S(140f) + Widgets.SmallWidth("B", "I") + Theme.S(8f);
        Widgets.RowBegin("Font", family == "Default" && (bold || italic) ? "Pick a font to use bold or italic" : "", w);
        var hit = false;
        ImGui.SetNextItemWidth(Theme.S(140f));
        if (ImGui.Combo("##fontfam", ref idx, fonts, fonts.Length)) { family = fonts[idx]; hit = true; }
        ImGui.SameLine(0, Theme.S(8f));
        Widgets.SegmentBegin();
        if (Widgets.Segment("B##fnt", bold)) { bold = !bold; hit = true; }
        ImGui.SameLine();
        if (Widgets.Segment("I##fnt", italic)) { italic = !italic; hit = true; }
        Widgets.SegmentEnd();
        Widgets.RowEnd();
        return hit;
    }

    // A row of buttons, right-aligned like every other control.
    private static void ButtonRow(string name, string hint, params string[] labels)
        => Widgets.RowBegin(name, hint, Widgets.SmallWidth(labels) + Theme.S(4f), ctlHeight: Widgets.SmallHeight);

    // ---- Mit Recap ----

    private void DrawPartyRecapPage()
    {
        C.RecapEnabled = PageHead("Mit Recap", "After a wipe", C.RecapEnabled);
        if (!C.RecapEnabled) return;

        Widgets.ListBegin();
        var locked = C.RecapPopupLocked;
        if (Widgets.RowCheck("Popup locked", "Unlock to drag it", ref locked))
        { C.RecapPopupLocked = locked; _plugin.RecapButtonWindow.RequestReposition(); C.SaveSettings(); }

        ButtonRow("Recap window", "The last pull, in a window you can move", "Open", "Sample");
        if (ImGui.SmallButton("Open##recap")) _plugin.RecapWindow.IsOpen = true;
        ImGui.SameLine(0, Theme.S(4f));
        if (ImGui.SmallButton("Sample##recap"))
        {
            // A real pull previews better, so never clobber one.
            if (!_plugin.Recap.HasData) _plugin.Recap.LoadSample();
            _plugin.Recap.ShowTestPopup();
            _plugin.RecapWindow.IsOpen = true;
        }
        Widgets.RowEnd();
        Widgets.ListEnd();

        ImGui.Spacing();
        ImGui.PushTextWrapPos(0f);
        ImGui.TextColored(Theme.V(Theme.Muted),
            "Tracks the boss's damage-downs and the party's defensives, who pressed them and when, "
            + "and which raid mits never landed.");
        ImGui.PopTextWrapPos();
    }

    // ---- Food & Pot ----

    private void DrawPrepCheckPage()
    {
        C.PrepCheckEnabled = PageHead("Food & Pot", "Check food before a pull, your pot when it is back",
            C.PrepCheckEnabled, hasModes: true, reset: () => ResetPage(NavKind.PrepCheck));
        if (!C.PrepCheckEnabled) return;

        if (AllMode) { DrawPrepAll(); return; }

        Widgets.ListBegin();
        DrawFoodWarnRow();
        DrawFoodLengthRow();

        var pot = C.PrepCheckPotion;
        if (Widgets.RowCheck("Potion reminder", "Calls it once when it comes back up", ref pot))
        { C.PrepCheckPotion = pot; C.SaveSettings(); }

        var pos = C.PrepCheckPosition;
        if (PositionRow(ref pos, PrepHome))
        { C.PrepCheckPosition = pos; C.SaveSettings(); _plugin.PrepWindow.RequestReposition(); }
        Widgets.ListEnd();

        ImGui.Spacing();
        Widgets.ListBegin();
        if (Widgets.RowDoor("All settings", "6 more")) SetAllMode(true);
        Widgets.ListEnd();
    }

    // No food at all is always flagged, so the choices here are the two you can
    // still be holding: crafter food and NQ.
    private void DrawFoodWarnRow()
    {
        Widgets.RowBegin("Warn me about", "No food is always flagged",
            Widgets.SmallWidth("Crafter", "NQ"), ctlHeight: Widgets.SmallHeight);
        Widgets.SegmentBegin();
        if (Widgets.Segment("Crafter##warn", C.PrepCheckWarnWrongFood))
        { C.PrepCheckWarnWrongFood = !C.PrepCheckWarnWrongFood; C.SaveSettings(); }
        Tip("Food whose stats are all crafting ones.");
        ImGui.SameLine();
        if (Widgets.Segment("NQ##warn", C.PrepCheckWarnNq))
        { C.PrepCheckWarnNq = !C.PrepCheckWarnNq; C.SaveSettings(); }
        Tip("HQ food caps noticeably higher.");
        Widgets.SegmentEnd();
        Widgets.RowEnd();
    }

    private void DrawFoodLengthRow()
    {
        var useFight = C.PrepCheckUseFightLength;
        Widgets.RowBegin("Running out", "Warn when it won't last the pull",
            Widgets.SmallWidth("This fight", "Under") + Theme.S(78f), ctlHeight: Widgets.SmallHeight);
        Widgets.SegmentBegin();
        if (Widgets.Segment("This fight##len", useFight)) { C.PrepCheckUseFightLength = true; C.SaveSettings(); }
        ImGui.SameLine();
        if (Widgets.Segment("Under##len", !useFight)) { C.PrepCheckUseFightLength = false; C.SaveSettings(); }
        Widgets.SegmentEnd();
        ImGui.SameLine(0, Theme.S(8f));
        ImGui.BeginDisabled(useFight);
        var mins = C.PrepCheckWarnMinutes;
        ImGui.SetNextItemWidth(Theme.S(70f));
        if (ImGui.DragFloat("##warnmin", ref mins, 0.2f, 1f, 30f, "%.0f min", ImGuiSliderFlags.AlwaysClamp))
        { C.PrepCheckWarnMinutes = mins; C.SaveSettings(); }
        ImGui.EndDisabled();
        Widgets.RowEnd();
    }

    private void DrawPrepAll()
    {
        Widgets.GroupLabel("Food");
        Widgets.ListBegin();
        DrawFoodWarnRow();
        DrawFoodLengthRow();
        var ready = C.PrepCheckOnReadyCheck;
        if (Widgets.RowCheck("On ready check", "Re-check on every ready check", ref ready))
        { C.PrepCheckOnReadyCheck = ready; C.SaveSettings(); }
        var always = C.PrepCheckAlwaysShowFood;
        if (Widgets.RowCheck("Always show the timer", "Even when the food is fine", ref always))
        { C.PrepCheckAlwaysShowFood = always; C.SaveSettings(); }
        Widgets.ListEnd();

        Widgets.GroupLabel("Potion");
        Widgets.ListBegin();
        var pot = C.PrepCheckPotion;
        if (Widgets.RowCheck("Potion reminder", "Starts once you pot", ref pot))
        { C.PrepCheckPotion = pot; C.SaveSettings(); }
        var count = C.PrepCheckPotCountdown;
        if (Widgets.RowCheck("Count down to it", "Shows \"Pot 1:23\" while it recasts", ref count, sub: true))
        { C.PrepCheckPotCountdown = count; C.SaveSettings(); }
        Widgets.ListEnd();

        Widgets.GroupLabel("Where and how");
        Widgets.ListBegin();
        var sheets = C.PrepCheckSheetsOnly;
        if (Widgets.RowCheck("Only fights with a sheet", "", ref sheets))
        { C.PrepCheckSheetsOnly = sheets; C.SaveSettings(); }
        var counts = C.PrepCheckShowCounts;
        if (Widgets.RowCheck("Show how many are left", "Counts what is in your bags", ref counts))
        { C.PrepCheckShowCounts = counts; C.SaveSettings(); }
        var tts = C.PrepCheckTts;
        if (Widgets.RowCheck("Speak it", "Uses the Audio page voice", ref tts))
        { C.PrepCheckTts = tts; C.SaveSettings(); }

        var pos = C.PrepCheckPosition;
        var pmoved = PositionRow(ref pos, PrepHome);
        if (NudgeRow(ref pos) || pmoved)
        { C.PrepCheckPosition = pos; C.SaveSettings(); _plugin.PrepWindow.RequestReposition(); }
        var locked = C.PrepCheckLocked;
        if (Widgets.RowCheck("Locked", "Auto-locks in combat", ref locked))
        { C.PrepCheckLocked = locked; _plugin.PrepWindow.RequestReposition(); C.SaveSettings(); }
        var px = C.PrepCheckFontSizePx;
        if (Widgets.RowDrag("Text size", "", ref px, 10f, 48f, "%.0f px", 86f))
        { C.PrepCheckFontSizePx = px; C.SaveSettings(); }
        Widgets.ListEnd();
    }

    // ---- Combat Timer ----

    private void DrawCombatTimerPage()
    {
        C.ShowCombatTimer = PageHead("Combat Timer", "Stopwatch of the current pull",
            C.ShowCombatTimer, reset: () => ResetPage(NavKind.CombatTimer));
        if (!C.ShowCombatTimer) return;

        DrawTimerSample();
        ImGui.Spacing();

        Widgets.ListBegin();
        var pos = C.CombatTimerPosition;
        var tmoved = PositionRow(ref pos, TimerHome);
        if (NudgeRow(ref pos) || tmoved)
        { C.CombatTimerPosition = pos; C.SaveSettings(); _plugin.CombatTimerWindow.RequestReposition(); }

        var locked = C.CombatTimerLocked;
        if (Widgets.RowCheck("Locked", "Click-through. Locks itself on pull.", ref locked))
        { C.CombatTimerLocked = locked; C.SaveSettings(); }

        var fam = C.CombatTimerFontFamily;
        var bold = C.CombatTimerFontBold;
        var ital = C.CombatTimerFontItalic;
        if (FontRow(ref fam, ref bold, ref ital))
        {
            C.CombatTimerFontFamily = fam; C.CombatTimerFontBold = bold; C.CombatTimerFontItalic = ital;
            C.SaveSettings();
        }

        var px = C.CombatTimerFontSizePx;
        if (Widgets.RowDrag("Text size", "", ref px, 12f, 120f, "%.0f px", 86f))
        { C.CombatTimerFontSizePx = px; C.SaveSettings(); }

        var col = ColorToVec4(C.CombatTimerColor);
        if (Widgets.RowColor("Color", "", ref col)) { C.CombatTimerColor = Vec4ToColor(col); C.SaveSettings(); }

        var bg = C.CombatTimerShowBackground;
        if (Widgets.RowCheck("Background box", "", ref bg)) { C.CombatTimerShowBackground = bg; C.SaveSettings(); }
        if (C.CombatTimerShowBackground)
        {
            var bgc = ColorToVec4(C.CombatTimerBackgroundColor);
            if (Widgets.RowColor("Box color", "Lower the alpha to see through it", ref bgc, sub: true))
            { C.CombatTimerBackgroundColor = Vec4ToColor(bgc); C.SaveSettings(); }
        }
        Widgets.ListEnd();
    }

    // ---- previews ----
    // Small stills of the real overlay, so a color or a size can be judged
    // here rather than by alt-tabbing into the game. Each line asks the font
    // manager for a real font at that pixel size: scaling the window font
    // instead just stretches the glyph atlas, which reads as blurry.

    // The real overlay, drawn inside a panel here. Anything hand-drawn in this
    // window would be an imitation, and an imitation drifts the moment the
    // overlay changes. The meter preview has always worked this way.
    //
    // A real child, not a group: the classic call centres itself on the room it
    // is given, and inside a group that room is the whole page.

    // Measured as the sample draws, applied next frame. The same one-frame
    // settle the sidebar and the label column use. Width as well as height:
    // the overlay sizes itself in real pixels from its own settings
    // (ProgressBarWidthPx, UpcomingBoardWidth, the call font size) and knows
    // nothing about this panel, so the panel has to fit itself around it.
    private readonly Dictionary<string, Vector2> _sampleSize = new();

    private void DrawLiveSample(string key, Action draw)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var pad = Theme.S(10f);
        var st = ImGui.GetStyle();
        var known = _sampleSize.TryGetValue(key, out var k)
            ? k
            : new Vector2(Theme.S(410f), ImGui.GetFrameHeight() * 3f);

        // Hug the overlay, but never wider than the page. A sample that runs
        // past its panel reads as broken; one that runs off the page is worse.
        var w = MathF.Min(avail, MathF.Max(Theme.S(220f), known.X + pad * 2f));
        // Wider than the page: scroll it rather than cut it, so a big call size
        // still shows what it really looks like.
        var scrolls = known.X + pad * 2f > avail + 1f;
        var h = known.Y + pad * 2f + (scrolls ? st.ScrollbarSize : 0f);

        var p = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + new Vector2(w, h), 0xFF0C0907, Theme.S(7f));
        dl.AddRect(p, p + new Vector2(w, h), Widgets.CardBorder, Theme.S(7f));

        ImGui.SetCursorScreenPos(p);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0u);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad));
        var flags = ImGuiWindowFlags.NoScrollWithMouse
                    | (scrolls ? ImGuiWindowFlags.HorizontalScrollbar : ImGuiWindowFlags.NoScrollbar);
        if (ImGui.BeginChild(key, new Vector2(w, h), false, flags))
        {
            var origin = ImGui.GetCursorScreenPos();
            ImGui.BeginGroup();
            draw();
            ImGui.EndGroup();
            var max = ImGui.GetItemRectMax();
            _sampleSize[key] = new Vector2(max.X - origin.X, max.Y - origin.Y);
        }
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();

        ImGui.SetCursorScreenPos(p);
        ImGui.Dummy(new Vector2(w, h));
    }

    private void DrawCallSample() => DrawLiveSample("##callsample", _plugin.OverlayWindow.DrawSampleCalls);

    private void DrawTimerSample() => DrawLiveSample("##timersample", _plugin.CombatTimerWindow.DrawSampleClock);

    private void DrawBoardSample() => DrawLiveSample("##boardsample", _plugin.TimelineWindow.DrawSampleBoard);

    // ---- Call Display ----

    private void DrawDisplayTab()
    {
        PageHead("Call Display", "", false, hasMaster: false, hasModes: true, reset: () => ResetPage(NavKind.Display));
        DrawCallSample();
        ImGui.Spacing();

        if (AllMode) { DrawDisplayAll(); return; }

        Widgets.ListBegin();
        DrawLookPresetRow();

        var px = C.OverlayFontSizePx;
        if (Widgets.RowDrag("Size", "", ref px, 12f, 120f, "%.0f px", 86f))
        { C.OverlayFontSizePx = px; C.SaveSettings(); }

        var pos = C.OverlayPosition;
        if (PositionRow(ref pos, CallHome))
        { C.OverlayPosition = pos; C.SaveSettings(); _plugin.OverlayWindow.RequestReposition(); }

        var locked = C.OverlayLocked;
        if (Widgets.RowCheck("Locked", "Click-through. Locks itself on pull.", ref locked))
        { C.OverlayLocked = locked; C.SaveSettings(); }

        var warn = C.WarningSeconds;
        if (Widgets.RowDrag("Show ahead", "How early a call shows", ref warn, 1f, 12f, "%.1fs", 86f))
        { C.WarningSeconds = warn; C.SaveSettings(); }

        // Flagship setting, so it stays on the short page.
        var useWinB = C.ShowUseWindows;
        if (Widgets.RowCheck("Usage Window", "For mits with a duration, not instants", ref useWinB))
        { C.ShowUseWindows = useWinB; C.SaveSettings(); _plugin.InvalidateSolverCache(); }

        var tts = C.TtsEnabled;
        if (Widgets.RowCheck("Speak it", "The voice is set on the Audio page", ref tts))
        { C.TtsEnabled = tts; C.SaveSettings(); }
        Widgets.ListEnd();
    }

    // Four saved looks. Most people pick one and never open All.
    private sealed record LookPreset(string Name, int Style, bool Icon, bool Mech, bool Panel,
        bool Spark, bool Bar, bool Number, bool Pulse);

    private static readonly LookPreset[] LookPresets =
    {
        new("Minimal", 0, false, false, false, false, false, false, false),
        new("Classic", 0, true, true, false, false, true, false, true),
        new("Board",   1, true, true, true, false, true, false, true),
        new("Loud",    0, true, true, true, true, true, true, true),
    };

    private bool IsLook(LookPreset p)
        => C.OverlayStyle == p.Style && C.ShowAbilityIcon == p.Icon && C.ShowMechanicLine == p.Mech
           && C.OverlayCallPanel == p.Panel && C.OverlayTextSpark == p.Spark
           && C.ShowProgressBar == p.Bar && C.ShowCountdownNumber == p.Number
           && C.PulseWhenImminent == p.Pulse;

    private void ApplyLook(LookPreset p)
    {
        C.OverlayStyle = p.Style; C.ShowAbilityIcon = p.Icon; C.ShowMechanicLine = p.Mech;
        C.OverlayCallPanel = p.Panel; C.OverlayTextSpark = p.Spark; C.ShowProgressBar = p.Bar;
        C.ShowCountdownNumber = p.Number; C.PulseWhenImminent = p.Pulse;
        C.Save();
    }

    private void DrawLookPresetRow()
    {
        var names = LookPresets.Select(p => p.Name).ToArray();
        Widgets.RowBegin("Look", "Changes entire look",
            Widgets.SmallWidth(names), ctlHeight: Widgets.SmallHeight);
        Widgets.SegmentBegin();
        for (var i = 0; i < LookPresets.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (Widgets.Segment(LookPresets[i].Name + "##look", IsLook(LookPresets[i])))
                ApplyLook(LookPresets[i]);
        }
        Widgets.SegmentEnd();
        Widgets.RowEnd();
    }

    private void DrawDisplayAll()
    {
        if (!ImGui.BeginTabBar("##displaytabs", ImGuiTabBarFlags.None)) return;

        if (TabItem("Style")) { DrawDisplayStyleTab(); ImGui.EndTabItem(); }
        if (TabItem("Call")) { DrawDisplayCallTab(); ImGui.EndTabItem(); }
        if (TabItem("Colors")) { DrawDisplayColorsTab(); ImGui.EndTabItem(); }
        if (TabItem("Timing")) { DrawDisplayTimingTab(); ImGui.EndTabItem(); }
        if (TabItem("Place")) { DrawDisplayPlaceTab(); ImGui.EndTabItem(); }
        if (TabItem("More")) { DrawDisplayMoreTab(); ImGui.EndTabItem(); }

        ImGui.EndTabBar();
    }

    private void DrawDisplayStyleTab()
    {
        Widgets.ListBegin();
        DrawLookPresetRow();

        var style = C.OverlayStyle;
        if (Widgets.RowCombo("Layout", "How the call is drawn", ref style,
                "Classic\0Board\0Icon + clock\0", 150f))
        { C.OverlayStyle = style; C.SaveSettings(); }

        var fam = C.OverlayFontFamily;
        var bold = C.OverlayFontBold;
        var ital = C.OverlayFontItalic;
        if (FontRow(ref fam, ref bold, ref ital))
        { C.OverlayFontFamily = fam; C.OverlayFontBold = bold; C.OverlayFontItalic = ital; C.SaveSettings(); }

        var px = C.OverlayFontSizePx;
        if (Widgets.RowDrag("Call size", "", ref px, 12f, 120f, "%.0f px", 86f))
        { C.OverlayFontSizePx = px; C.SaveSettings(); }

        var align = C.OverlayTextAlign;
        Widgets.RowBegin("Align", "", Widgets.SmallWidth("Left", "Center", "Right"), ctlHeight: Widgets.SmallHeight);
        Widgets.SegmentBegin();
        var aligns = new[] { "Left", "Center", "Right" };
        for (var i = 0; i < aligns.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (Widgets.Segment(aligns[i] + "##al", align == i)) { C.OverlayTextAlign = i; C.SaveSettings(); }
        }
        Widgets.SegmentEnd();
        Widgets.RowEnd();

        if (C.ShowAbilityIcon)
        {
            var iconScale = C.IconScale;
            if (Widgets.RowDrag("Icon size", "", ref iconScale, 0.4f, 1.5f, "%.2fx", 86f))
            { C.IconScale = iconScale; C.SaveSettings(); }
        }
        Widgets.ListEnd();
    }

    private void DrawDisplayCallTab()
    {
        Widgets.ListBegin();
        var v = C.ShowAbilityIcon;
        if (Widgets.RowCheck("Ability icon", "Matched from the action name", ref v))
        { C.ShowAbilityIcon = v; C.SaveSettings(); }

        v = C.ShowMechanicLine;
        if (Widgets.RowCheck("Mechanic line", "Second line, the mechanic name", ref v))
        { C.ShowMechanicLine = v; C.SaveSettings(); }

        v = C.TextShadow;
        if (Widgets.RowCheck("Drop shadow", "Readable over a busy background", ref v))
        { C.TextShadow = v; C.SaveSettings(); }

        v = C.CooldownAwareCalls;
        if (Widgets.RowCheck("Cooldown warnings", "Reds out a call still on CD", ref v))
        { C.CooldownAwareCalls = v; C.SaveSettings(); }

        v = C.ShowCountdownNumber;
        if (Widgets.RowCheck("Countdown number", "", ref v)) { C.ShowCountdownNumber = v; C.SaveSettings(); }

        v = C.ShowRadialRing;
        if (Widgets.RowCheck("Radial ring", "Countdown ring on the icon", ref v))
        { C.ShowRadialRing = v; C.SaveSettings(); }

        v = C.OverlayCallPanel;
        if (Widgets.RowCheck("Call panel", "Dark plate behind the call", ref v))
        { C.OverlayCallPanel = v; C.SaveSettings(); }

        v = C.OverlayTextSpark;
        if (Widgets.RowCheck("Text spark", "Spark that rides the bar across the text", ref v))
        { C.OverlayTextSpark = v; C.SaveSettings(); }
        Widgets.ListEnd();
    }

    private void DrawDisplayColorsTab()
    {
        Widgets.ListBegin();
        var c = ColorToVec4(C.OverlayColorImminent);
        if (Widgets.RowColor("Counting down", "Before the call fires", ref c))
        { C.OverlayColorImminent = Vec4ToColor(c); C.SaveSettings(); }

        c = ColorToVec4(C.OverlayColorActive);
        if (Widgets.RowColor("Now", "While the call is live", ref c))
        { C.OverlayColorActive = Vec4ToColor(c); C.SaveSettings(); }

        c = ColorToVec4(C.OverlayColorMechanic);
        if (Widgets.RowColor("Mechanic line", "", ref c))
        { C.OverlayColorMechanic = Vec4ToColor(c); C.SaveSettings(); }

        var byType = C.ColorByMitType;
        if (Widgets.RowCheck("By mit type", "Party, tank and personal get their own color", ref byType))
        { C.ColorByMitType = byType; C.SaveSettings(); }
        if (C.ColorByMitType)
        {
            c = ColorToVec4(C.MitColorParty);
            if (Widgets.RowColor("Party", "", ref c, sub: true)) { C.MitColorParty = Vec4ToColor(c); C.SaveSettings(); }
            c = ColorToVec4(C.MitColorTank);
            if (Widgets.RowColor("Tank", "", ref c, sub: true)) { C.MitColorTank = Vec4ToColor(c); C.SaveSettings(); }
            c = ColorToVec4(C.MitColorPersonal);
            if (Widgets.RowColor("Personal", "", ref c, sub: true)) { C.MitColorPersonal = Vec4ToColor(c); C.SaveSettings(); }
        }
        Widgets.ListEnd();
    }

    private void DrawDisplayTimingTab()
    {
        Widgets.ListBegin();
        var warn = C.WarningSeconds;
        if (Widgets.RowDrag("Show ahead", "How early a call shows", ref warn, 1f, 12f, "%.1fs", 86f))
        { C.WarningSeconds = warn; C.SaveSettings(); }

        var hold = C.HoldSeconds;
        if (Widgets.RowDrag("Hold on screen", "How long it stays after the hit", ref hold, 0f, 6f, "%.1fs", 86f))
        { C.HoldSeconds = hold; C.SaveSettings(); }

        var useWin = C.ShowUseWindows;
        if (Widgets.RowCheck("Usage Window", "For mits with a duration, not instants", ref useWin))
        { C.ShowUseWindows = useWin; C.SaveSettings(); _plugin.InvalidateSolverCache(); }
        if (C.ShowUseWindows)
        {
            var lead = C.UseWindowLeadSeconds;
            if (Widgets.RowDrag("Window opens in", "How early the window shows",
                    ref lead, 0f, 12f, "%.1fs", 86f, sub: true))
            { C.UseWindowLeadSeconds = lead; C.SaveSettings(); }

            var maxDur = C.MaxUseWindowSeconds;
            if (Widgets.RowDrag("Longest window", "Caps the window",
                    ref maxDur, 1f, 30f, "%.1fs", 86f, sub: true))
            { C.MaxUseWindowSeconds = maxDur; C.SaveSettings(); }
            if (ImGui.IsItemDeactivatedAfterEdit()) _plugin.InvalidateSolverCache();
        }

        var start = C.StartOnCountdown;
        if (Widgets.RowCheck("Start on countdown", "Runs during the pull countdown", ref start))
        { C.StartOnCountdown = start; C.SaveSettings(); }
        Widgets.ListEnd();
    }

    private void DrawDisplayPlaceTab()
    {
        Widgets.ListBegin();
        var pos = C.OverlayPosition;
        var omoved = PositionRow(ref pos, CallHome);
        if (NudgeRow(ref pos) || omoved)
        { C.OverlayPosition = pos; C.SaveSettings(); _plugin.OverlayWindow.RequestReposition(); }

        var locked = C.OverlayLocked;
        if (Widgets.RowCheck("Locked", "Click-through. Locks itself on pull.", ref locked))
        { C.OverlayLocked = locked; C.SaveSettings(); }

        var bar = C.ShowProgressBar;
        if (Widgets.RowCheck("Countdown bar", "", ref bar))
        { C.ShowProgressBar = bar; C.SaveSettings(); }
        if (C.ShowProgressBar)
        {
            var barH = C.ProgressBarHeight;
            if (Widgets.RowDrag("Bar height", "", ref barH, 2f, 24f, "%.0f px", 86f, sub: true))
            { C.ProgressBarHeight = barH; C.SaveSettings(); }
        }

        var pulse = C.PulseWhenImminent;
        if (Widgets.RowCheck("Pulse at 1s", "", ref pulse)) { C.PulseWhenImminent = pulse; C.SaveSettings(); }

        var box = C.ShowBackground;
        if (Widgets.RowCheck("Background box", "", ref box)) { C.ShowBackground = box; C.SaveSettings(); }
        if (C.ShowBackground)
        {
            var bg = ColorToVec4(C.BackgroundColor);
            if (Widgets.RowColor("Box color", "Lower the alpha to see through it", ref bg, sub: true))
            { C.BackgroundColor = Vec4ToColor(bg); C.SaveSettings(); }
        }
        Widgets.ListEnd();
    }

    private void DrawDisplayMoreTab()
    {
        Widgets.ListBegin();
        var dtr = C.ShowDtrBar;
        if (Widgets.RowCheck("Server bar", "Next mit on the server info bar", ref dtr))
        { C.ShowDtrBar = dtr; C.SaveSettings(); }

        var mitBar = C.ShowMitBar;
        if (Widgets.RowCheck("Active mits bar", "Your mits and the time left on them", ref mitBar))
        { C.ShowMitBar = mitBar; C.SaveSettings(); }
        if (C.ShowMitBar)
        {
            var locked = C.MitBarLocked;
            if (Widgets.RowCheck("Locked", "Auto-locks in combat", ref locked, sub: true))
            { C.MitBarLocked = locked; _plugin.MitBarWindow.RequestReposition(); C.SaveSettings(); }
            var mbPx = C.MitBarFontSizePx;
            if (Widgets.RowDrag("Text size", "", ref mbPx, 10f, 48f, "%.0f px", 86f, sub: true))
            { C.MitBarFontSizePx = mbPx; C.SaveSettings(); }
        }

        var fmt = C.HeadlineFormat;
        Widgets.RowBegin("Call format", "{action} {mechanic} {time} {count} {remaining}", Theme.S(190f));
        if (ImGui.InputText("##callformat", ref fmt, 128)) { C.HeadlineFormat = fmt; C.SaveSettings(); }
        Widgets.RowEnd();

        var suffix = C.ActiveSuffix;
        Widgets.RowBegin("NOW suffix", "Added while the call is live", Theme.S(190f));
        if (ImGui.InputText("##nowsuffix", ref suffix, 64)) { C.ActiveSuffix = suffix; C.SaveSettings(); }
        Widgets.RowEnd();
        Widgets.ListEnd();
    }

    // ---- Appearance ----

    private void DrawAppearancePage()
    {
        PageHead("Appearance", "These windows only", false, hasMaster: false, reset: () => ResetPage(NavKind.Appearance));

        Widgets.ListBegin();
        DrawAccentRow();

        var follow = C.OverlaysFollowAccent;
        if (Widgets.RowCheck("Overlays follow it", "Off = each overlay keeps its own", ref follow))
        { C.OverlaysFollowAccent = follow; C.SaveSettings(); }

        var scale = C.UiScale;
        if (Widgets.RowDrag("UI scale", "Text and spacing in these windows", ref scale, 0.8f, 1.6f, "%.2fx", 96f))
        { C.UiScale = scale; Theme.Scale = scale; C.SaveSettings(); }

        var cb = C.ColorblindMode;
        if (Widgets.RowCheck("Colorblind safe", "Swaps red and green for blue and orange", ref cb))
        { C.ColorblindMode = cb; Theme.Colorblind = cb; C.SaveSettings(); }
        Widgets.ListEnd();

        Widgets.GroupLabel("Sample");
        Widgets.ListBegin();
        var demo = true;
        Widgets.RowCheck("A setting", "With its hint underneath", ref demo);
        var demoIdx = 0;
        Widgets.RowCombo("A value", "", ref demoIdx, "Option\0Another\0", 110f);
        Widgets.RowBegin("A choice", "", Widgets.SmallWidth("One", "Two", "Three"), ctlHeight: Widgets.SmallHeight);
        Widgets.SegmentBegin();
        Widgets.Segment("One##demo", true); ImGui.SameLine();
        Widgets.Segment("Two##demo", false); ImGui.SameLine();
        Widgets.Segment("Three##demo", false);
        Widgets.SegmentEnd();
        Widgets.RowEnd();
        Widgets.ListEnd();
    }

    // Five presets and a picker, all on one row.
    private void DrawAccentRow()
    {
        var size = ImGui.GetFrameHeight();
        var w = (size + Theme.S(7f)) * (AccentPresets.Length + 1);
        Widgets.RowBegin("Accent", "Selected tabs, sliders, buttons and headers", w);
        foreach (var (name, col) in AccentPresets)
        {
            if (AccentSwatch(name, col)) { C.AccentColor = col; Theme.Accent = col; C.SaveSettings(); }
            ImGui.SameLine(0, Theme.S(7f));
        }
        var custom = ColorToVec4(C.AccentColor);
        ImGui.SetNextItemWidth(size);
        if (ImGui.ColorEdit4("##accentpick", ref custom, ImGuiColorEditFlags.NoInputs))
        {
            C.AccentColor = Vec4ToColor(custom);
            Theme.Accent = C.AccentColor;
            C.SaveSettings();
        }
        Tip("Any color you like.");
        Widgets.RowEnd();
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
        Tip(name);
        return clicked;
    }

    // ---- Next Mits ----

    private void DrawNextMitsPage()
    {
        C.ShowUpcoming = PageHead("Next Mits", "", C.ShowUpcoming, hasModes: true, reset: () => ResetPage(NavKind.NextMits));
        if (!C.ShowUpcoming) return;

        DrawBoardSample();
        // A sample in the real window while you place it, never during a pull.
        if (!_plugin.Timer.Running) _plugin.TimelineWindow.PingScreenPreview();
        ImGui.Spacing();

        var board = Math.Clamp(C.UpcomingStyle, 0, 1) == 1;
        if (AllMode) { DrawNextMitsAll(board); return; }

        Widgets.ListBegin();
        DrawLayoutRow();
        if (board)
        {
            var rows = C.UpcomingBoardRows;
            if (Widgets.RowDragInt("Rows", "How many bars at once", ref rows, 3, 12, "%d", 80f))
            { C.UpcomingBoardRows = rows; C.SaveSettings(); }
            var look = C.UpcomingBoardLookaheadSeconds;
            if (Widgets.RowDrag("Look ahead", "Bars start full at that edge", ref look, 15f, 180f, "%.0fs", 80f))
            { C.UpcomingBoardLookaheadSeconds = look; C.SaveSettings(); }
        }
        else
        {
            var lines = C.UpcomingCount;
            if (Widgets.RowDragInt("Lines", "How many calls to list", ref lines, 1, 8, "%d", 80f))
            { C.UpcomingCount = lines; C.SaveSettings(); }
            var look = C.UpcomingLookaheadSeconds;
            if (Widgets.RowDrag("Look ahead", "", ref look, 5f, 90f, "%.0fs", 80f))
            { C.UpcomingLookaheadSeconds = look; C.SaveSettings(); }
        }

        var pos = C.TimelinePosition;
        if (PositionRow(ref pos, BoardHome))
        { C.TimelinePosition = pos; C.SaveSettings(); _plugin.TimelineWindow.RequestReposition(); }

        var locked = C.TimelineLocked;
        if (Widgets.RowCheck("Locked", "Click-through. Locks itself on pull.", ref locked))
        { C.TimelineLocked = locked; C.SaveSettings(); }
        Widgets.ListEnd();
    }

    private void DrawLayoutRow()
    {
        Widgets.RowBegin("Layout", "", Widgets.SmallWidth("Compact list", "Mechanic board"),
            ctlHeight: Widgets.SmallHeight);
        var style = Math.Clamp(C.UpcomingStyle, 0, 1);
        Widgets.SegmentBegin();
        if (Widgets.Segment("Compact list##lay", style == 0)) { C.UpcomingStyle = 0; C.SaveSettings(); }
        Tip("Just your next calls.");
        ImGui.SameLine();
        if (Widgets.Segment("Mechanic board##lay", style == 1)) { C.UpcomingStyle = 1; C.SaveSettings(); }
        Tip("Every hit, as countdown bars.");
        Widgets.SegmentEnd();
        Widgets.RowEnd();
    }

    private void DrawNextMitsAll(bool board)
    {
        if (!ImGui.BeginTabBar("##nmtabs", ImGuiTabBarFlags.None)) return;

        if (TabItem("Board")) { DrawNextMitsBoardTab(board); ImGui.EndTabItem(); }
        if (board && TabItem("Rows")) { DrawNextMitsRowsTab(); ImGui.EndTabItem(); }
        if (board && TabItem("Look")) { DrawNextMitsLookTab(); ImGui.EndTabItem(); }
        if (TabItem("No sheet")) { DrawNextMitsNoSheetTab(); ImGui.EndTabItem(); }

        ImGui.EndTabBar();
    }

    private void DrawNextMitsBoardTab(bool board)
    {
        Widgets.ListBegin();
        DrawLayoutRow();
        if (board)
        {
            var rows = C.UpcomingBoardRows;
            if (Widgets.RowDragInt("Rows", "How many bars at once", ref rows, 3, 12, "%d", 80f))
            { C.UpcomingBoardRows = rows; C.SaveSettings(); }
            var look = C.UpcomingBoardLookaheadSeconds;
            if (Widgets.RowDrag("Look ahead", "Bars start full at that edge", ref look, 15f, 180f, "%.0fs", 80f))
            { C.UpcomingBoardLookaheadSeconds = look; C.SaveSettings(); }
            var bw = C.UpcomingBoardWidth;
            if (Widgets.RowDrag("Bar width", "", ref bw, 220f, 560f, "%.0f px", 86f))
            { C.UpcomingBoardWidth = bw; C.SaveSettings(); }
            var px = C.UpcomingFontSizePx;
            if (Widgets.RowDrag("Text size", "", ref px, 10f, 60f, "%.0f px", 86f))
            { C.UpcomingFontSizePx = px; C.SaveSettings(); }
            var mine = C.UpcomingBoardOnlyMine;
            if (Widgets.RowCheck("Only my hits", "Off = the whole fight", ref mine))
            { C.UpcomingBoardOnlyMine = mine; C.SaveSettings(); }
        }
        else
        {
            var lines = C.UpcomingCount;
            if (Widgets.RowDragInt("Lines", "How many calls to list", ref lines, 1, 8, "%d", 80f))
            { C.UpcomingCount = lines; C.SaveSettings(); }
            var look = C.UpcomingLookaheadSeconds;
            if (Widgets.RowDrag("Look ahead", "", ref look, 5f, 90f, "%.0fs", 80f))
            { C.UpcomingLookaheadSeconds = look; C.SaveSettings(); }
            var px = C.UpcomingFontSizePx;
            if (Widgets.RowDrag("Text size", "", ref px, 10f, 60f, "%.0f px", 86f))
            { C.UpcomingFontSizePx = px; C.SaveSettings(); }
            var col = ColorToVec4(C.OverlayColorUpcoming);
            if (Widgets.RowColor("Text color", "", ref col))
            { C.OverlayColorUpcoming = Vec4ToColor(col); C.SaveSettings(); }
        }

        var pos = C.TimelinePosition;
        var nmoved = PositionRow(ref pos, BoardHome);
        if (NudgeRow(ref pos) || nmoved)
        { C.TimelinePosition = pos; C.SaveSettings(); _plugin.TimelineWindow.RequestReposition(); }
        var locked = C.TimelineLocked;
        if (Widgets.RowCheck("Locked", "Click-through. Locks itself on pull.", ref locked))
        { C.TimelineLocked = locked; C.SaveSettings(); }
        Widgets.ListEnd();
    }

    private void DrawNextMitsRowsTab()
    {
        Widgets.ListBegin();
        var v = C.UpcomingBoardTimeText;
        if (Widgets.RowCheck("Countdown seconds", "", ref v)) { C.UpcomingBoardTimeText = v; C.SaveSettings(); }
        v = C.UpcomingBoardShowActions;
        if (Widgets.RowCheck("Planned mits", "", ref v)) { C.UpcomingBoardShowActions = v; C.SaveSettings(); }
        v = C.UpcomingBoardShowSeverity;
        if (Widgets.RowCheck("Severity", "! !! !!! by how hard it hits", ref v))
        { C.UpcomingBoardShowSeverity = v; C.SaveSettings(); }
        v = C.UpcomingBoardTypeChip;
        if (Widgets.RowCheck("Type chip", "Buster, Raid AOE, Enrage", ref v))
        { C.UpcomingBoardTypeChip = v; C.SaveSettings(); }
        if (C.UpcomingBoardTypeChip)
        {
            v = C.UpcomingBoardTypeChipShort;
            if (Widgets.RowCheck("Short labels", "TB / AOE / ENR", ref v, sub: true))
            { C.UpcomingBoardTypeChipShort = v; C.SaveSettings(); }
        }
        v = C.UpcomingBoardShowType;
        if (Widgets.RowCheck("Buster icon", "Shield on TB rows", ref v))
        { C.UpcomingBoardShowType = v; C.SaveSettings(); }
        v = C.UpcomingBossPosition;
        if (Widgets.RowCheck("Reposition calls", "Counts down to the boss coming back", ref v))
        { C.UpcomingBossPosition = v; C.SaveSettings(); }
        v = C.UpcomingBoardPhases;
        if (Widgets.RowCheck("Phase dividers", "Line at each phase change", ref v))
        { C.UpcomingBoardPhases = v; C.SaveSettings(); }
        Widgets.ListEnd();
    }

    private void DrawNextMitsLookTab()
    {
        Widgets.ListBegin();
        ImGui.BeginDisabled(C.OverlaysFollowAccent);
        var c = ColorToVec4(C.UpcomingBoardAccentColor);
        if (Widgets.RowColor("Base color", C.OverlaysFollowAccent
                ? "Held: Appearance has the overlays following the accent"
                : "Stripe, drain fill, header", ref c))
        { C.UpcomingBoardAccentColor = Vec4ToColor(c); C.Save(); }
        ImGui.EndDisabled();

        c = ColorToVec4(C.UpcomingBoardNextColor);
        if (Widgets.RowColor("Next", "Your next press", ref c))
        { C.UpcomingBoardNextColor = Vec4ToColor(c); C.Save(); }
        c = ColorToVec4(C.UpcomingBoardNowColor);
        if (Widgets.RowColor("Now", "The press that is live", ref c))
        { C.UpcomingBoardNowColor = Vec4ToColor(c); C.Save(); }

        var op = (int)MathF.Round(Math.Clamp(C.UpcomingBoardBgOpacity, 0f, 1f) * 100f);
        if (Widgets.RowDragInt("Opacity", "", ref op, 0, 100, "%d%%", 80f))
        { C.UpcomingBoardBgOpacity = op / 100f; C.SaveSettings(); }
        var pad = C.UpcomingBoardBarPad;
        if (Widgets.RowDrag("Bar thickness", "", ref pad, 2f, 24f, "+%.0f px", 86f))
        { C.UpcomingBoardBarPad = pad; C.SaveSettings(); }
        var gap = C.UpcomingBoardRowGap;
        if (Widgets.RowDrag("Row spacing", "Below zero overlaps them", ref gap, -8f, 16f, "%.0f px", 86f))
        { C.UpcomingBoardRowGap = gap; C.SaveSettings(); }
        var rnd = C.UpcomingBoardRounding;
        if (Widgets.RowDrag("Rounding", "", ref rnd, 0f, 12f, "%.0f px", 86f))
        { C.UpcomingBoardRounding = rnd; C.SaveSettings(); }

        var v = C.UpcomingBoardStripe;
        if (Widgets.RowCheck("Left stripe", "", ref v)) { C.UpcomingBoardStripe = v; C.SaveSettings(); }
        v = C.UpcomingBoardDrain;
        if (Widgets.RowCheck("Drain toward the hit", "Off = bars fill instead", ref v))
        { C.UpcomingBoardDrain = v; C.SaveSettings(); }
        Widgets.ListEnd();

        Widgets.GroupLabel("Header");
        Widgets.ListBegin();
        v = C.UpcomingShowHeader;
        if (Widgets.RowCheck("Show a header", "", ref v)) { C.UpcomingShowHeader = v; C.SaveSettings(); }
        if (C.UpcomingShowHeader)
        {
            Widgets.RowBegin("Show", "", Widgets.SmallWidth("Name", "Clock", "Rule", "Slot", "Sync"),
                sub: true, ctlHeight: Widgets.SmallHeight);
            Widgets.SegmentBegin();
            if (Widgets.Segment("Name##hd", C.UpcomingHeaderTitle)) { C.UpcomingHeaderTitle = !C.UpcomingHeaderTitle; C.SaveSettings(); }
            ImGui.SameLine();
            if (Widgets.Segment("Clock##hd", C.UpcomingHeaderClock)) { C.UpcomingHeaderClock = !C.UpcomingHeaderClock; C.SaveSettings(); }
            ImGui.SameLine();
            if (Widgets.Segment("Rule##hd", C.UpcomingHeaderRule)) { C.UpcomingHeaderRule = !C.UpcomingHeaderRule; C.SaveSettings(); }
            ImGui.SameLine();
            if (Widgets.Segment("Slot##hd", C.UpcomingHeaderSlot)) { C.UpcomingHeaderSlot = !C.UpcomingHeaderSlot; C.SaveSettings(); }
            ImGui.SameLine();
            if (Widgets.Segment("Sync##hd", C.UpcomingHeaderSync)) { C.UpcomingHeaderSync = !C.UpcomingHeaderSync; C.SaveSettings(); }
            Widgets.SegmentEnd();
            Widgets.RowEnd();
        }
        Widgets.ListEnd();
    }

    private void DrawNextMitsNoSheetTab()
    {
        Widgets.ListBegin();
        var v = C.UniversalTimelines;
        if (Widgets.RowCheck("Every duty", "Boss casts in any duty. No mits, no voice.", ref v))
        { C.UniversalTimelines = v; C.SaveSettings(); }
        v = C.LearnTimelines;
        if (Widgets.RowCheck("Learn from pulls", "Builds a timeline from your pulls. A sheet always wins.", ref v))
        { C.LearnTimelines = v; C.SaveSettings(); }
        if (C.LearnedFights.Count > 0)
        {
            var known = C.LearnedFights.Values.OrderByDescending(f => f.LastSeen).ToList();
            if (Widgets.RowDoor("Learned so far", $"{known.Count} boss{(known.Count == 1 ? "" : "es")}"))
                _learnedOpen = !_learnedOpen;
        }
        Widgets.ListEnd();

        if (_learnedOpen && C.LearnedFights.Count > 0) DrawLearnedTable();
    }

    private bool _learnedOpen;

    private void DrawLearnedTable()
    {
        ImGui.Spacing();
        var known = C.LearnedFights.Values.OrderByDescending(f => f.LastSeen).ToList();
        if (!ImGui.BeginTable("##learnedfights", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;
        ImGui.TableSetupColumn("Boss", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Casts", ImGuiTableColumnFlags.WidthFixed, Theme.S(46f));
        ImGui.TableSetupColumn("Pulls", ImGuiTableColumnFlags.WidthFixed, Theme.S(46f));
        ImGui.TableSetupColumn("##act", ImGuiTableColumnFlags.WidthFixed, Theme.S(62f));
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

    // ---- Audio ----

    private void DrawAudioTab()
    {
        C.AudioEnabled = PageHead("Audio", "Once per call, per pull", C.AudioEnabled, hasModes: true);
        if (!C.AudioEnabled) return;

        Widgets.ListBegin();
        Widgets.RowBegin("Speak", "", Widgets.SmallWidth("The mit", "The mechanic"), ctlHeight: Widgets.SmallHeight);
        Widgets.SegmentBegin();
        if (Widgets.Segment("The mit##sp", !C.TtsSpeakMechanic)) { C.TtsSpeakMechanic = false; C.SaveSettings(); }
        Tip("Reads the action you press, e.g. \"Reprisal\".");
        ImGui.SameLine();
        if (Widgets.Segment("The mechanic##sp", C.TtsSpeakMechanic)) { C.TtsSpeakMechanic = true; C.SaveSettings(); }
        Widgets.SegmentEnd();
        Widgets.RowEnd();

        var speak = C.TtsEnabled;
        if (Widgets.RowCheck("Speak the call", "Off = beep only", ref speak))
        { C.TtsEnabled = speak; C.SaveSettings(); }

        Widgets.RowBegin("Voices", "Online needs internet, falls back to Windows",
            Widgets.SmallWidth("Online", "Windows"), ctlHeight: Widgets.SmallHeight);
        Widgets.SegmentBegin();
        if (Widgets.Segment("Online##eng", C.TtsUseEdge)) { C.TtsUseEdge = true; C.SaveSettings(); }
        ImGui.SameLine();
        if (Widgets.Segment("Windows##eng", !C.TtsUseEdge)) { C.TtsUseEdge = false; C.SaveSettings(); }
        Widgets.SegmentEnd();
        Widgets.RowEnd();

        DrawVoiceRow();

        var rate = C.TtsRate;
        if (Widgets.RowDragInt("Speed", "", ref rate, -10, 10, "%d", 80f)) { C.TtsRate = rate; C.SaveSettings(); }
        var vol = C.TtsVolume;
        if (Widgets.RowDragInt("Volume", "", ref vol, 0, 100, "%d", 80f)) { C.TtsVolume = vol; C.SaveSettings(); }

        Widgets.RowBegin("Try it", "", Theme.S(150f) + Widgets.SmallWidth("Speak"));
        ImGui.SetNextItemWidth(Theme.S(150f));
        ImGui.InputTextWithHint("##testtext", "Reprisal", ref _ttsTestText, 128);
        ImGui.SameLine(0, Theme.S(6f));
        if (ImGui.SmallButton("Speak##tts")) SpeakTest();
        Widgets.RowEnd();
        Widgets.ListEnd();

        var status = _plugin.Audio.LastTtsStatus;
        if (!string.IsNullOrEmpty(status))
        {
            ImGui.Spacing();
            var ok = status.StartsWith("Online OK") || status == "Windows voice";
            ImGui.TextColored(ok ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudYellow, status);
        }

        if (!AllMode)
        {
            ImGui.Spacing();
            Widgets.ListBegin();
            if (Widgets.RowDoor("All settings", "2 more")) SetAllMode(true);
            Widgets.ListEnd();
            return;
        }

        Widgets.GroupLabel("More");
        Widgets.ListBegin();
        var custom = C.TtsCustomVoice;
        Widgets.RowBegin("Custom voice", "Voice id. Overrides the picker.", Theme.S(190f));
        if (ImGui.InputTextWithHint("##customvoice", "en-US-AvaMultilingualNeural", ref custom, 64))
        { C.TtsCustomVoice = custom; C.SaveSettings(); }
        Widgets.RowEnd();

        var gap = C.TtsMinGapSeconds;
        if (Widgets.RowDrag("Minimum gap", "Skip a repeat within this. 0 = never.",
                ref gap, 0f, 5f, "%.1fs", 86f))
        { C.TtsMinGapSeconds = gap; C.SaveSettings(); }
        Widgets.ListEnd();
    }

    private void SpeakTest()
    {
        var t = string.IsNullOrWhiteSpace(_ttsTestText) ? "Reprisal" : _ttsTestText;
        var voice = C.TtsUseEdge
            ? (string.IsNullOrWhiteSpace(C.TtsCustomVoice) ? C.TtsEdgeVoice : C.TtsCustomVoice)
            : C.TtsVoice;
        _plugin.Audio.Speak(t, C.TtsRate, C.TtsVolume, C.TtsUseEdge, voice);
    }

    // Online voices split by sex; Windows lists whatever is installed.
    private void DrawVoiceRow()
    {
        if (C.TtsUseEdge)
        {
            // Snap an old saved voice onto a valid one.
            var cur = Array.Find(Audio.EdgeVoices, v => v.Id == C.TtsEdgeVoice);
            if (cur.Id == null) { cur = Audio.EdgeVoices[0]; C.TtsEdgeVoice = cur.Id; C.SaveSettings(); }
            var female = cur.Female;

            var list = Audio.EdgeVoices.Where(v => v.Female == female).ToArray();
            var names = list.Select(v => v.Name).ToArray();
            var idx = Math.Max(0, Array.FindIndex(list, v => v.Id == C.TtsEdgeVoice));

            Widgets.RowBegin("Voice", "", Widgets.SmallWidth("F", "M", "Play") + Theme.S(130f));
            Widgets.SegmentBegin();
            if (Widgets.Segment("F##sex", female) && !female)
            { C.TtsEdgeVoice = Audio.EdgeVoices.First(v => v.Female).Id; C.Save(); }
            ImGui.SameLine();
            if (Widgets.Segment("M##sex", !female) && female)
            { C.TtsEdgeVoice = Audio.EdgeVoices.First(v => !v.Female).Id; C.Save(); }
            Widgets.SegmentEnd();
            ImGui.SameLine(0, Theme.S(8f));
            ImGui.SetNextItemWidth(Theme.S(130f));
            if (ImGui.Combo("##edgevoice", ref idx, names, names.Length)) { C.TtsEdgeVoice = list[idx].Id; C.Save(); }
            ImGui.SameLine(0, Theme.S(6f));
            if (ImGui.SmallButton("Play##v")) SpeakTest();
            Tip("The first use of a voice fetches it, then it is instant.");
            Widgets.RowEnd();
            return;
        }

        var voices = new List<string> { "System default" };
        voices.AddRange(_plugin.Audio.VoiceNames());
        var vi = string.IsNullOrEmpty(C.TtsVoice) ? 0 : Math.Max(0, voices.IndexOf(C.TtsVoice));
        Widgets.RowBegin("Voice", voices.Count <= 1 ? "Add more in Windows, Time & language, Speech" : "",
            Theme.S(190f) + Widgets.SmallWidth("Play"));
        ImGui.SetNextItemWidth(Theme.S(190f));
        if (ImGui.Combo("##sapivoice", ref vi, voices.ToArray(), voices.Count))
        { C.TtsVoice = vi == 0 ? "" : voices[vi]; C.Save(); }
        ImGui.SameLine(0, Theme.S(6f));
        if (ImGui.SmallButton("Play##v")) SpeakTest();
        Widgets.RowEnd();
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
