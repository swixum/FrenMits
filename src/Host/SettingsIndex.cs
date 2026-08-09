using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FrenMits.Host;

// Every setting the search box can find, plus the config property behind it, so
// searching and the "changed" marks read from one list.
internal static class SettingsIndex
{
    internal sealed class Entry
    {
        public ConfigWindow.NavKind Nav;
        public string Tab = "";
        public string Label = "";
        public string Prop = "";
        public string Extra = "";   // words worth finding it by that aren't in the label

        private PropertyInfo? _pi;
        private object? _default;
        private bool _resolved;

        // The property and its fresh-install value, looked up once.
        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _pi = typeof(Configuration).GetProperty(Prop, BindingFlags.Public | BindingFlags.Instance);
            _default = _pi?.GetValue(Defaults);
        }

        public bool IsChanged(Configuration c)
        {
            Resolve();
            if (_pi == null) return false;
            var now = _pi.GetValue(c);
            return !Equals(now, _default);
        }

        public void Reset(Configuration c)
        {
            Resolve();
            if (_pi is { CanWrite: true }) _pi.SetValue(c, _default);
        }
    }

    private static readonly Configuration Defaults = new();

    public static string PageName(ConfigWindow.NavKind nav) => nav switch
    {
        ConfigWindow.NavKind.Display => "Call Display",
        ConfigWindow.NavKind.NextMits => "Next Mits",
        ConfigWindow.NavKind.Audio => "Audio",
        ConfigWindow.NavKind.PartyRecap => "Mit Recap",
        ConfigWindow.NavKind.CombatTimer => "Combat Timer",
        ConfigWindow.NavKind.PrepCheck => "Food & Pot",
        ConfigWindow.NavKind.Meter => "Fren Meter",
        ConfigWindow.NavKind.Appearance => "Appearance",
        _ => "Home",
    };

    public static string Where(Entry e)
        => e.Tab.Length == 0 ? PageName(e.Nav) : $"{PageName(e.Nav)}  >  {e.Tab}";

    private static Entry E(ConfigWindow.NavKind nav, string tab, string label, string prop, string extra = "")
        => new() { Nav = nav, Tab = tab, Label = label, Prop = prop, Extra = extra };

    public static readonly Entry[] All =
    {
        // ---- Call Display ----
        E(ConfigWindow.NavKind.Display, "Style", "Layout", nameof(Configuration.OverlayStyle), "classic board icon clock call style"),
        E(ConfigWindow.NavKind.Display, "Style", "Font", nameof(Configuration.OverlayFontFamily), "typeface family bold italic"),
        E(ConfigWindow.NavKind.Display, "Style", "Call Size", nameof(Configuration.OverlayFontSizePx), "text size bigger smaller"),
        E(ConfigWindow.NavKind.Display, "Style", "Align", nameof(Configuration.OverlayTextAlign), "left center right"),
        E(ConfigWindow.NavKind.Display, "Style", "Bold", nameof(Configuration.OverlayFontBold), "font weight"),
        E(ConfigWindow.NavKind.Display, "Style", "Italic", nameof(Configuration.OverlayFontItalic), "font slant"),
        E(ConfigWindow.NavKind.Display, "Style", "Icon Size", nameof(Configuration.IconScale), "scale"),
        E(ConfigWindow.NavKind.Display, "Call", "Ability Icon", nameof(Configuration.ShowAbilityIcon), "action image"),
        E(ConfigWindow.NavKind.Display, "Call", "Mechanic Line", nameof(Configuration.ShowMechanicLine), "second line"),
        E(ConfigWindow.NavKind.Display, "Call", "Drop Shadow", nameof(Configuration.TextShadow), "outline readability"),
        E(ConfigWindow.NavKind.Display, "Call", "Cooldown Warnings", nameof(Configuration.CooldownAwareCalls), "cd red"),
        E(ConfigWindow.NavKind.Display, "Call", "Countdown Number", nameof(Configuration.ShowCountdownNumber), "seconds"),
        E(ConfigWindow.NavKind.Display, "Call", "Radial Ring", nameof(Configuration.ShowRadialRing), "countdown circle"),
        E(ConfigWindow.NavKind.Display, "Call", "Call Panel", nameof(Configuration.OverlayCallPanel), "plate background"),
        E(ConfigWindow.NavKind.Display, "Call", "Text Spark", nameof(Configuration.OverlayTextSpark), "wipe bar edge"),
        E(ConfigWindow.NavKind.Display, "Colors", "Counting Down", nameof(Configuration.OverlayColorImminent), "color soon"),
        E(ConfigWindow.NavKind.Display, "Colors", "Now", nameof(Configuration.OverlayColorActive), "color active"),
        E(ConfigWindow.NavKind.Display, "Colors", "Mechanic Line", nameof(Configuration.OverlayColorMechanic), "color"),
        E(ConfigWindow.NavKind.Display, "Colors", "Party", nameof(Configuration.MitColorParty), "mit type colour"),
        E(ConfigWindow.NavKind.Display, "Colors", "Tank", nameof(Configuration.MitColorTank), "mit type colour"),
        E(ConfigWindow.NavKind.Display, "Colors", "Personal", nameof(Configuration.MitColorPersonal), "mit type colour"),
        E(ConfigWindow.NavKind.Display, "Colors", "By Mit Type", nameof(Configuration.ColorByMitType), "party tank personal"),
        E(ConfigWindow.NavKind.Display, "Timing", "Show Ahead", nameof(Configuration.WarningSeconds), "lead warning early"),
        E(ConfigWindow.NavKind.Display, "Timing", "Hold on Screen", nameof(Configuration.HoldSeconds), "linger"),
        E(ConfigWindow.NavKind.Display, "Timing", "Usage Window", nameof(Configuration.ShowUseWindows), "press window span duration"),
        E(ConfigWindow.NavKind.Display, "Timing", "Window Opens In", nameof(Configuration.UseWindowLeadSeconds), "lead early"),
        E(ConfigWindow.NavKind.Display, "Timing", "Longest Window", nameof(Configuration.MaxUseWindowSeconds), "clamp max duration"),
        E(ConfigWindow.NavKind.Display, "Timing", "Start on Countdown", nameof(Configuration.StartOnCountdown), "prepull"),
        E(ConfigWindow.NavKind.Display, "Place", "Locked", nameof(Configuration.OverlayLocked), "drag move position click-through"),
        E(ConfigWindow.NavKind.Display, "Place", "Position", nameof(Configuration.OverlayPosition), "where left centre right nudge"),
        E(ConfigWindow.NavKind.Display, "Place", "Countdown Bar", nameof(Configuration.ShowProgressBar), "progress"),
        E(ConfigWindow.NavKind.Display, "Place", "Bar Height", nameof(Configuration.ProgressBarHeight), "thickness"),
        E(ConfigWindow.NavKind.Display, "Place", "Pulse at 1s", nameof(Configuration.PulseWhenImminent), "flash"),
        E(ConfigWindow.NavKind.Display, "Place", "Background Box", nameof(Configuration.ShowBackground), "backdrop"),
        E(ConfigWindow.NavKind.Display, "More", "Server Bar", nameof(Configuration.ShowDtrBar), "dtr server info next mit"),
        E(ConfigWindow.NavKind.Display, "More", "Active Mits Bar", nameof(Configuration.ShowMitBar), "buffs active"),
        E(ConfigWindow.NavKind.Display, "More", "Box Color", nameof(Configuration.BackgroundColor), "background"),
        E(ConfigWindow.NavKind.Display, "More", "Active mits locked", nameof(Configuration.MitBarLocked), "drag"),
        E(ConfigWindow.NavKind.Display, "More", "Active mits text size", nameof(Configuration.MitBarFontSizePx), "font"),
        E(ConfigWindow.NavKind.Display, "More", "Call Format", nameof(Configuration.HeadlineFormat), "placeholder template"),
        E(ConfigWindow.NavKind.Display, "More", "NOW Suffix", nameof(Configuration.ActiveSuffix), "now text"),

        // ---- Next Mits ----
        E(ConfigWindow.NavKind.NextMits, "", "Show the window", nameof(Configuration.ShowUpcoming), "board timeline"),
        E(ConfigWindow.NavKind.NextMits, "Board", "Layout", nameof(Configuration.UpcomingStyle), "compact list mechanic board"),
        E(ConfigWindow.NavKind.NextMits, "Board", "Rows", nameof(Configuration.UpcomingBoardRows), "how many bars"),
        E(ConfigWindow.NavKind.NextMits, "Board", "Look Ahead", nameof(Configuration.UpcomingBoardLookaheadSeconds), "seconds ahead"),
        E(ConfigWindow.NavKind.NextMits, "Board", "Bar Width", nameof(Configuration.UpcomingBoardWidth), "size"),
        E(ConfigWindow.NavKind.NextMits, "Board", "Text Size", nameof(Configuration.UpcomingFontSizePx), "font"),
        E(ConfigWindow.NavKind.NextMits, "Board", "Only My Hits", nameof(Configuration.UpcomingBoardOnlyMine), "mine filter"),
        E(ConfigWindow.NavKind.NextMits, "Board", "Lines", nameof(Configuration.UpcomingCount), "compact list count"),
        E(ConfigWindow.NavKind.NextMits, "Board", "Position", nameof(Configuration.TimelinePosition), "where left centre right nudge"),
        E(ConfigWindow.NavKind.NextMits, "Board", "Text Color", nameof(Configuration.OverlayColorUpcoming), "compact list colour"),
        E(ConfigWindow.NavKind.NextMits, "Board", "Locked", nameof(Configuration.TimelineLocked), "drag move click-through"),
        E(ConfigWindow.NavKind.NextMits, "Rows", "Countdown Seconds", nameof(Configuration.UpcomingBoardTimeText), "time text"),
        E(ConfigWindow.NavKind.NextMits, "Rows", "Planned Mits", nameof(Configuration.UpcomingBoardShowActions), "actions"),
        E(ConfigWindow.NavKind.NextMits, "Rows", "Severity", nameof(Configuration.UpcomingBoardShowSeverity), "danger marks"),
        E(ConfigWindow.NavKind.NextMits, "Rows", "Type Chip", nameof(Configuration.UpcomingBoardTypeChip), "tag buster raid aoe enrage"),
        E(ConfigWindow.NavKind.NextMits, "Rows", "Short Labels", nameof(Configuration.UpcomingBoardTypeChipShort), "tb aoe enr"),
        E(ConfigWindow.NavKind.NextMits, "Rows", "Buster Icon", nameof(Configuration.UpcomingBoardShowType), "shield tank"),
        E(ConfigWindow.NavKind.NextMits, "Rows", "Reposition Calls", nameof(Configuration.UpcomingBossPosition), "return spot"),
        E(ConfigWindow.NavKind.NextMits, "Rows", "Phase Dividers", nameof(Configuration.UpcomingBoardPhases), "separator"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Opacity", nameof(Configuration.UpcomingBoardBgOpacity), "transparent"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Bar Thickness", nameof(Configuration.UpcomingBoardBarPad), "height"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Row Spacing", nameof(Configuration.UpcomingBoardRowGap), "gap overlap"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Rounding", nameof(Configuration.UpcomingBoardRounding), "corners"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Left Stripe", nameof(Configuration.UpcomingBoardStripe), "accent edge"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Drain Toward the Hit", nameof(Configuration.UpcomingBoardDrain), "fill direction"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Base Color", nameof(Configuration.UpcomingBoardAccentColor), "accent stripe"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Next", nameof(Configuration.UpcomingBoardNextColor), "colour gold"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Now", nameof(Configuration.UpcomingBoardNowColor), "colour green"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Header name", nameof(Configuration.UpcomingHeaderTitle), "title"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Header clock", nameof(Configuration.UpcomingHeaderClock), "time"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Header rule", nameof(Configuration.UpcomingHeaderRule), "underline"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Header slot", nameof(Configuration.UpcomingHeaderSlot), "badge seat"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Header sync", nameof(Configuration.UpcomingHeaderSync), "synced note"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Show a Header", nameof(Configuration.UpcomingShowHeader), "title clock slot sync"),
        E(ConfigWindow.NavKind.NextMits, "No sheet", "Every Duty", nameof(Configuration.UniversalTimelines), "dungeon trial raid universal"),
        E(ConfigWindow.NavKind.NextMits, "No sheet", "Learn from Pulls", nameof(Configuration.LearnTimelines), "learning casts"),

        // ---- Audio ----
        E(ConfigWindow.NavKind.Audio, "", "Enable audio cues", nameof(Configuration.AudioEnabled), "sound"),
        E(ConfigWindow.NavKind.Audio, "", "Speak the Call", nameof(Configuration.TtsEnabled), "tts voice talk"),
        E(ConfigWindow.NavKind.Audio, "", "Speak", nameof(Configuration.TtsSpeakMechanic), "mit mechanic"),
        E(ConfigWindow.NavKind.Audio, "", "Speed", nameof(Configuration.TtsRate), "rate"),
        E(ConfigWindow.NavKind.Audio, "", "Volume", nameof(Configuration.TtsVolume), "loud"),
        E(ConfigWindow.NavKind.Audio, "", "Minimum Gap", nameof(Configuration.TtsMinGapSeconds), "spam between cues"),
        E(ConfigWindow.NavKind.Audio, "", "Custom Voice", nameof(Configuration.TtsCustomVoice), "edge id neural"),

        // ---- Combat Timer ----
        E(ConfigWindow.NavKind.CombatTimer, "", "Show the combat timer", nameof(Configuration.ShowCombatTimer), "stopwatch clock"),
        E(ConfigWindow.NavKind.CombatTimer, "", "Locked", nameof(Configuration.CombatTimerLocked), "drag click-through"),
        E(ConfigWindow.NavKind.CombatTimer, "", "Position", nameof(Configuration.CombatTimerPosition), "where left centre right nudge"),
        E(ConfigWindow.NavKind.CombatTimer, "", "Bold", nameof(Configuration.CombatTimerFontBold), "font weight"),
        E(ConfigWindow.NavKind.CombatTimer, "", "Italic", nameof(Configuration.CombatTimerFontItalic), "font slant"),
        E(ConfigWindow.NavKind.CombatTimer, "", "Box Color", nameof(Configuration.CombatTimerBackgroundColor), "background"),
        E(ConfigWindow.NavKind.CombatTimer, "", "Font", nameof(Configuration.CombatTimerFontFamily), "typeface"),
        E(ConfigWindow.NavKind.CombatTimer, "", "Text Size", nameof(Configuration.CombatTimerFontSizePx), "size"),
        E(ConfigWindow.NavKind.CombatTimer, "", "Color", nameof(Configuration.CombatTimerColor), "text colour"),
        E(ConfigWindow.NavKind.CombatTimer, "", "Background Box", nameof(Configuration.CombatTimerShowBackground), "backdrop"),

        // ---- Food & Pot ----
        E(ConfigWindow.NavKind.PrepCheck, "", "Enable Food & Pot", nameof(Configuration.PrepCheckEnabled), "meal potion"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Crafter", nameof(Configuration.PrepCheckWarnWrongFood), "warn wrong food"),
        E(ConfigWindow.NavKind.PrepCheck, "", "NQ", nameof(Configuration.PrepCheckWarnNq), "warn hq quality"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Running Out", nameof(Configuration.PrepCheckUseFightLength), "fight length duration"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Under", nameof(Configuration.PrepCheckWarnMinutes), "warn minutes"),
        E(ConfigWindow.NavKind.PrepCheck, "", "On Ready Check", nameof(Configuration.PrepCheckOnReadyCheck), "notify"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Always Show the Timer", nameof(Configuration.PrepCheckAlwaysShowFood), "persistent"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Potion Reminder", nameof(Configuration.PrepCheckPotion), "pot"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Count Down to It", nameof(Configuration.PrepCheckPotCountdown), "recast"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Only Fights with a Sheet", nameof(Configuration.PrepCheckSheetsOnly), "duty filter"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Show How Many Are Left", nameof(Configuration.PrepCheckShowCounts), "count bags"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Speak It", nameof(Configuration.PrepCheckTts), "tts"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Position", nameof(Configuration.PrepCheckPosition), "where left centre right nudge"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Locked", nameof(Configuration.PrepCheckLocked), "drag position"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Text Size", nameof(Configuration.PrepCheckFontSizePx), "font"),

        // ---- Mit Recap ----
        E(ConfigWindow.NavKind.PartyRecap, "", "Enable Mit Recap", nameof(Configuration.RecapEnabled), "wipe review party"),
        E(ConfigWindow.NavKind.PartyRecap, "", "Popup Locked", nameof(Configuration.RecapPopupLocked), "drag"),

        // ---- Fren Meter ----
        E(ConfigWindow.NavKind.Meter, "", "Enable Fren Meter", nameof(Configuration.MeterEnabled), "damage dps parse"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Show", nameof(Configuration.MeterShowMode), "always after a pull only in combat hide"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Names", nameof(Configuration.MeterNameStyle), "full first initial"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Rows Shown", nameof(Configuration.MeterMaxRows), "how many"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Refresh", nameof(Configuration.MeterRefreshSeconds), "numbers settle"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Rank Numbers", nameof(Configuration.MeterShowRank), "position"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Job Icons", nameof(Configuration.MeterShowJobIcons), "class"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Limit Break Row", nameof(Configuration.MeterLimitBreakRow), "lb"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Split DPS and HPS", nameof(Configuration.MeterSplitHealing), "healing"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Header", nameof(Configuration.MeterHeaderStyle), "full slim hidden"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Raid rDPS Total", nameof(Configuration.MeterShowRaidTotal), "sum"),
        E(ConfigWindow.NavKind.Meter, "Rows", "DPS and HPS Tabs", nameof(Configuration.MeterHealingTab), "tabs"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Buttons Bar", nameof(Configuration.MeterButtons), "history pause reset"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Death Count", nameof(Configuration.MeterFooterDeaths), "deaths footer"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Click-through", nameof(Configuration.MeterClickThrough), "mouse"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Action Icons", nameof(Configuration.MeterBreakdownIcons), "breakdown"),
        E(ConfigWindow.NavKind.Meter, "Rows", "Color Each Ability", nameof(Configuration.MeterBreakdownColors), "breakdown"),
        E(ConfigWindow.NavKind.Meter, "Bars", "Fill", nameof(Configuration.MeterBarStyle), "flat glass gradient outline minimal"),
        E(ConfigWindow.NavKind.Meter, "Bars", "Color by Job", nameof(Configuration.MeterJobColors), "bar color"),
        E(ConfigWindow.NavKind.Meter, "Bars", "Solid", nameof(Configuration.MeterBarSolid), "opaque"),
        E(ConfigWindow.NavKind.Meter, "Bars", "Height", nameof(Configuration.MeterBarHeight), "size"),
        E(ConfigWindow.NavKind.Meter, "Bars", "Spacing", nameof(Configuration.MeterBarGap), "gap"),
        E(ConfigWindow.NavKind.Meter, "Bars", "Rounding", nameof(Configuration.MeterRounding), "corners"),
        E(ConfigWindow.NavKind.Meter, "Bars", "Opacity", nameof(Configuration.MeterBarOpacity), "transparent"),
        E(ConfigWindow.NavKind.Meter, "Bars", "Position", nameof(Configuration.MeterPosition), "where left centre right nudge"),
        E(ConfigWindow.NavKind.Meter, "Bars", "Locked", nameof(Configuration.MeterLocked), "drag position size"),
        E(ConfigWindow.NavKind.Meter, "Text", "Font", nameof(Configuration.MeterFontFamily), "typeface"),
        E(ConfigWindow.NavKind.Meter, "Text", "Bold", nameof(Configuration.MeterFontBold), "font weight"),
        E(ConfigWindow.NavKind.Meter, "Text", "Italic", nameof(Configuration.MeterFontItalic), "font slant"),
        E(ConfigWindow.NavKind.Meter, "Text", "Size", nameof(Configuration.MeterFontSizePx), "text"),
        E(ConfigWindow.NavKind.Meter, "Text", "Drop Shadow", nameof(Configuration.MeterTextShadow), "outline"),
        E(ConfigWindow.NavKind.Meter, "Text", "Call Your Row You", nameof(Configuration.MeterYou), "name"),
        E(ConfigWindow.NavKind.Meter, "Text", "Highlight It", nameof(Configuration.MeterHighlightYou), "your row"),
        E(ConfigWindow.NavKind.Meter, "Text", "Style", nameof(Configuration.MeterHighlightStyle), "wash outline stripe"),
        E(ConfigWindow.NavKind.Meter, "Text", "Strength", nameof(Configuration.MeterHighlightStrength), "highlight"),
        E(ConfigWindow.NavKind.Meter, "Colors", "Names", nameof(Configuration.MeterTextColor), "text colour"),
        E(ConfigWindow.NavKind.Meter, "Colors", "Details", nameof(Configuration.MeterSubColor), "secondary"),
        E(ConfigWindow.NavKind.Meter, "Colors", "Title", nameof(Configuration.MeterTitleColor), "encounter name"),
        E(ConfigWindow.NavKind.Meter, "Colors", "Timer", nameof(Configuration.MeterTimerColor), "clock"),
        E(ConfigWindow.NavKind.Meter, "Colors", "Name", nameof(Configuration.MeterYouColor), "yours"),
        E(ConfigWindow.NavKind.Meter, "Colors", "Highlight", nameof(Configuration.MeterHighlightColor), "wash"),
        E(ConfigWindow.NavKind.Meter, "Colors", "Accent", nameof(Configuration.MeterAccentColor), "totals"),
        E(ConfigWindow.NavKind.Meter, "Colors", "Background", nameof(Configuration.MeterBgColor), "window"),
        E(ConfigWindow.NavKind.Meter, "Colors", "Rows", nameof(Configuration.MeterRowColor), "bar background"),
        E(ConfigWindow.NavKind.Meter, "Colors", "Border", nameof(Configuration.MeterBorderColor), "outline"),
        E(ConfigWindow.NavKind.Meter, "Columns", "Column labels", nameof(Configuration.MeterColumnHeader), "header"),
        E(ConfigWindow.NavKind.Meter, "Connection", "Source", nameof(Configuration.MeterConnection), "act iinact parser websocket"),
        E(ConfigWindow.NavKind.Meter, "Connection", "Address", nameof(Configuration.MeterSocketAddress), "ws wss port"),

        // ---- Appearance ----
        E(ConfigWindow.NavKind.Appearance, "", "Accent", nameof(Configuration.AccentColor), "theme tint highlight colour"),
        E(ConfigWindow.NavKind.Appearance, "", "Overlays Follow It", nameof(Configuration.OverlaysFollowAccent), "accent overlays"),
        E(ConfigWindow.NavKind.Appearance, "", "UI Scale", nameof(Configuration.UiScale), "text size window bigger zoom"),
        E(ConfigWindow.NavKind.Appearance, "", "Colorblind Safe", nameof(Configuration.ColorblindMode), "accessibility deuteranopia"),
    };

    // Settings on one tab that are off their defaults.
    public static List<Entry> ChangedIn(Configuration c, ConfigWindow.NavKind nav, string tab)
        => All.Where(e => e.Nav == nav && e.Tab == tab && e.IsChanged(c)).ToList();

    // Every setting on a page, back to how it ships. Read by reflection from a
    // fresh Configuration, so a hand-written list of defaults cannot go stale.
    public static void ResetPage(Configuration c, ConfigWindow.NavKind nav)
    {
        foreach (var e in All) if (e.Nav == nav) e.Reset(c);
    }

    // Every setting whose label, page or keywords contain the query.
    public static List<Entry> Search(string query)
    {
        var q = query.Trim();
        if (q.Length < 2) return new List<Entry>();
        bool Hit(string s) => s.Contains(q, StringComparison.OrdinalIgnoreCase);
        return All
            .Where(e => Hit(e.Label) || Hit(e.Extra) || Hit(e.Tab) || Hit(PageName(e.Nav)))
            // Label matches first, since that's what people type.
            .OrderByDescending(e => Hit(e.Label))
            .ThenBy(e => e.Nav)
            .ThenBy(e => e.Tab, StringComparer.Ordinal)
            .Take(40)
            .ToList();
    }
}
