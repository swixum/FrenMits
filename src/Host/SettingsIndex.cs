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
        ConfigWindow.NavKind.Display => "Display",
        ConfigWindow.NavKind.NextMits => "Next Mits & Timeline",
        ConfigWindow.NavKind.Audio => "Audio",
        ConfigWindow.NavKind.PartyRecap => "Party Mit Recap",
        ConfigWindow.NavKind.CombatTimer => "Combat Timer",
        ConfigWindow.NavKind.PrepCheck => "Food & Pot",
        ConfigWindow.NavKind.Meter => "Fren Meter",
        _ => "Home",
    };

    public static string Where(Entry e)
        => e.Tab.Length == 0 ? PageName(e.Nav) : $"{PageName(e.Nav)}  >  {e.Tab}";

    private static Entry E(ConfigWindow.NavKind nav, string tab, string label, string prop, string extra = "")
        => new() { Nav = nav, Tab = tab, Label = label, Prop = prop, Extra = extra };

    public static readonly Entry[] All =
    {
        // ---- Display ----
        E(ConfigWindow.NavKind.Display, "Placement", "Lock overlay (click-through)", nameof(Configuration.OverlayLocked), "drag move position"),
        E(ConfigWindow.NavKind.Display, "Style", "Call style", nameof(Configuration.OverlayStyle), "classic board icon clock"),
        E(ConfigWindow.NavKind.Display, "Style", "Ability icon", nameof(Configuration.ShowAbilityIcon), "action image"),
        E(ConfigWindow.NavKind.Display, "Style", "Radial ring", nameof(Configuration.ShowRadialRing), "countdown circle"),
        E(ConfigWindow.NavKind.Display, "Style", "Call panel", nameof(Configuration.OverlayCallPanel), "plate plate background"),
        E(ConfigWindow.NavKind.Display, "Style", "Text spark", nameof(Configuration.OverlayTextSpark), "wipe bar edge"),
        E(ConfigWindow.NavKind.Display, "Style", "Mechanic 2nd line", nameof(Configuration.ShowMechanicLine), "second line"),
        E(ConfigWindow.NavKind.Display, "Style", "Countdown number", nameof(Configuration.ShowCountdownNumber), "seconds"),
        E(ConfigWindow.NavKind.Display, "Style", "Drop shadow", nameof(Configuration.TextShadow), "outline readability"),
        E(ConfigWindow.NavKind.Display, "Style", "Cooldown warnings", nameof(Configuration.CooldownAwareCalls), "cd red"),
        E(ConfigWindow.NavKind.Display, "Style", "Call format", nameof(Configuration.HeadlineFormat), "placeholder template"),
        E(ConfigWindow.NavKind.Display, "Style", "\"NOW\" suffix", nameof(Configuration.ActiveSuffix), "now text"),
        E(ConfigWindow.NavKind.Display, "Font", "Font", nameof(Configuration.OverlayFontFamily), "typeface family"),
        E(ConfigWindow.NavKind.Display, "Font", "Call size", nameof(Configuration.OverlayFontSizePx), "text size bigger smaller"),
        E(ConfigWindow.NavKind.Display, "Font", "Align", nameof(Configuration.OverlayTextAlign), "left center right"),
        E(ConfigWindow.NavKind.Display, "Font", "Icon size", nameof(Configuration.IconScale), "scale"),
        E(ConfigWindow.NavKind.Display, "Colors", "Counting down", nameof(Configuration.OverlayColorImminent), "color"),
        E(ConfigWindow.NavKind.Display, "Colors", "NOW", nameof(Configuration.OverlayColorActive), "color active"),
        E(ConfigWindow.NavKind.Display, "Colors", "Mechanic", nameof(Configuration.OverlayColorMechanic), "color"),
        E(ConfigWindow.NavKind.Display, "Colors", "Color the call by mit type", nameof(Configuration.ColorByMitType), "party tank personal"),
        E(ConfigWindow.NavKind.Display, "Bar & box", "Countdown bar under the call", nameof(Configuration.ShowProgressBar), "progress"),
        E(ConfigWindow.NavKind.Display, "Bar & box", "Height", nameof(Configuration.ProgressBarHeight), "bar thickness"),
        E(ConfigWindow.NavKind.Display, "Bar & box", "Pulse the text in the last second", nameof(Configuration.PulseWhenImminent), "flash"),
        E(ConfigWindow.NavKind.Display, "Bar & box", "Draw a background box", nameof(Configuration.ShowBackground), "backdrop"),
        E(ConfigWindow.NavKind.Display, "Timing", "Start on the pull countdown", nameof(Configuration.StartOnCountdown), "prepull"),
        E(ConfigWindow.NavKind.Display, "Timing", "Show ahead", nameof(Configuration.WarningSeconds), "lead warning early"),
        E(ConfigWindow.NavKind.Display, "Timing", "Hold on screen", nameof(Configuration.HoldSeconds), "linger"),
        E(ConfigWindow.NavKind.Display, "Timing", "Usage window", nameof(Configuration.ShowUseWindows), "press span"),
        E(ConfigWindow.NavKind.Display, "Timing", "Max window duration", nameof(Configuration.MaxUseWindowSeconds), "clamp"),
        E(ConfigWindow.NavKind.Display, "Extras", "Server-bar next mit", nameof(Configuration.ShowDtrBar), "dtr server info"),
        E(ConfigWindow.NavKind.Display, "Extras", "Active-mits bar", nameof(Configuration.ShowMitBar), "buffs active"),
        E(ConfigWindow.NavKind.Display, "Extras", "Colorblind-safe status colors", nameof(Configuration.ColorblindMode), "accessibility deuteranopia"),
        E(ConfigWindow.NavKind.Display, "Look", "Accent color", nameof(Configuration.AccentColor), "theme tint highlight"),
        E(ConfigWindow.NavKind.Display, "Look", "UI scale", nameof(Configuration.UiScale), "text size window bigger zoom"),

        // ---- Next Mits & Timeline ----
        E(ConfigWindow.NavKind.NextMits, "", "Show the window", nameof(Configuration.ShowUpcoming), "board timeline"),
        E(ConfigWindow.NavKind.NextMits, "", "Lock the window (click-through)", nameof(Configuration.TimelineLocked), "drag move"),
        E(ConfigWindow.NavKind.NextMits, "Layout", "Rows", nameof(Configuration.UpcomingBoardRows), "how many bars"),
        E(ConfigWindow.NavKind.NextMits, "Layout", "Look-ahead", nameof(Configuration.UpcomingBoardLookaheadSeconds), "seconds ahead"),
        E(ConfigWindow.NavKind.NextMits, "Layout", "Bar width", nameof(Configuration.UpcomingBoardWidth), "size"),
        E(ConfigWindow.NavKind.NextMits, "Layout", "Text size", nameof(Configuration.UpcomingFontSizePx), "font"),
        E(ConfigWindow.NavKind.NextMits, "Layout", "Only hits I have a press for", nameof(Configuration.UpcomingBoardOnlyMine), "mine filter"),
        E(ConfigWindow.NavKind.NextMits, "Layout", "Lines", nameof(Configuration.UpcomingCount), "compact list count"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Opacity", nameof(Configuration.UpcomingBoardBgOpacity), "transparent"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Thickness", nameof(Configuration.UpcomingBoardBarPad), "bar height"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Row spacing", nameof(Configuration.UpcomingBoardRowGap), "gap overlap"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Rounding", nameof(Configuration.UpcomingBoardRounding), "corners"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Accent stripe on the left edge", nameof(Configuration.UpcomingBoardStripe), "bar"),
        E(ConfigWindow.NavKind.NextMits, "Look", "Bars drain toward the hit", nameof(Configuration.UpcomingBoardDrain), "fill direction"),
        E(ConfigWindow.NavKind.NextMits, "On the rows", "Countdown seconds", nameof(Configuration.UpcomingBoardTimeText), "time text"),
        E(ConfigWindow.NavKind.NextMits, "On the rows", "Planned mits", nameof(Configuration.UpcomingBoardShowActions), "actions"),
        E(ConfigWindow.NavKind.NextMits, "On the rows", "Severity marks (! !! !!!)", nameof(Configuration.UpcomingBoardShowSeverity), "danger"),
        E(ConfigWindow.NavKind.NextMits, "On the rows", "Tank buster icon", nameof(Configuration.UpcomingBoardShowType), "shield"),
        E(ConfigWindow.NavKind.NextMits, "On the rows", "Mechanic type chip", nameof(Configuration.UpcomingBoardTypeChip), "tag buster raid aoe enrage"),
        E(ConfigWindow.NavKind.NextMits, "On the rows", "Short chip labels", nameof(Configuration.UpcomingBoardTypeChipShort), "tb aoe enr"),
        E(ConfigWindow.NavKind.NextMits, "On the rows", "Boss reposition calls", nameof(Configuration.UpcomingBossPosition), "return spot"),
        E(ConfigWindow.NavKind.NextMits, "On the rows", "Phase dividers", nameof(Configuration.UpcomingBoardPhases), "separator"),
        E(ConfigWindow.NavKind.NextMits, "Every duty", "Run a boss timeline in every duty (no sheet needed)", nameof(Configuration.UniversalTimelines), "dungeon trial raid"),
        E(ConfigWindow.NavKind.NextMits, "Every duty", "Learn a boss's timeline from your own pulls", nameof(Configuration.LearnTimelines), "learning casts"),

        // ---- Audio ----
        E(ConfigWindow.NavKind.Audio, "", "Enable audio cues", nameof(Configuration.AudioEnabled), "sound"),
        E(ConfigWindow.NavKind.Audio, "Voice", "Speak the action", nameof(Configuration.TtsEnabled), "tts voice talk"),
        E(ConfigWindow.NavKind.Audio, "Voice", "Speed", nameof(Configuration.TtsRate), "rate"),
        E(ConfigWindow.NavKind.Audio, "Voice", "Volume", nameof(Configuration.TtsVolume), "loud"),
        E(ConfigWindow.NavKind.Audio, "Voice", "Min gap between cues (s)", nameof(Configuration.TtsMinGapSeconds), "spam"),

        // ---- Combat Timer ----
        E(ConfigWindow.NavKind.CombatTimer, "", "Show the combat timer", nameof(Configuration.ShowCombatTimer), "stopwatch clock"),
        E(ConfigWindow.NavKind.CombatTimer, "Placement", "Lock position (click-through)", nameof(Configuration.CombatTimerLocked), "drag"),
        E(ConfigWindow.NavKind.CombatTimer, "Font", "Font", nameof(Configuration.CombatTimerFontFamily), "typeface"),
        E(ConfigWindow.NavKind.CombatTimer, "Font", "Text size", nameof(Configuration.CombatTimerFontSizePx), "size"),
        E(ConfigWindow.NavKind.CombatTimer, "Colors", "Text color", nameof(Configuration.CombatTimerColor), "color"),
        E(ConfigWindow.NavKind.CombatTimer, "Colors", "Draw a background box", nameof(Configuration.CombatTimerShowBackground), "backdrop"),

        // ---- Food & Pot ----
        E(ConfigWindow.NavKind.PrepCheck, "", "Enable Food & Pot", nameof(Configuration.PrepCheckEnabled), "meal potion"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Only in fights with a sheet", nameof(Configuration.PrepCheckSheetsOnly), "duty filter"),
        E(ConfigWindow.NavKind.PrepCheck, "", "Show how many you have left", nameof(Configuration.PrepCheckShowCounts), "count bags"),
        E(ConfigWindow.NavKind.PrepCheck, "Food", "Notify on Ready Check", nameof(Configuration.PrepCheckOnReadyCheck), "ready"),
        E(ConfigWindow.NavKind.PrepCheck, "Food", "Use the fight's own length", nameof(Configuration.PrepCheckUseFightLength), "duration"),
        E(ConfigWindow.NavKind.PrepCheck, "Food", "Warn under", nameof(Configuration.PrepCheckWarnMinutes), "minutes"),
        E(ConfigWindow.NavKind.PrepCheck, "Food", "Warn on crafter food", nameof(Configuration.PrepCheckWarnWrongFood), "wrong"),
        E(ConfigWindow.NavKind.PrepCheck, "Food", "Warn on NQ food", nameof(Configuration.PrepCheckWarnNq), "hq quality"),
        E(ConfigWindow.NavKind.PrepCheck, "Food", "Always show the timer", nameof(Configuration.PrepCheckAlwaysShowFood), "persistent"),
        E(ConfigWindow.NavKind.PrepCheck, "Potion", "Potion reminder", nameof(Configuration.PrepCheckPotion), "pot"),
        E(ConfigWindow.NavKind.PrepCheck, "Potion", "Count down to it", nameof(Configuration.PrepCheckPotCountdown), "recast"),
        E(ConfigWindow.NavKind.PrepCheck, "Voice", "Speak it", nameof(Configuration.PrepCheckTts), "tts"),
        E(ConfigWindow.NavKind.PrepCheck, "Placement", "Lock position", nameof(Configuration.PrepCheckLocked), "drag"),
        E(ConfigWindow.NavKind.PrepCheck, "Placement", "Text size", nameof(Configuration.PrepCheckFontSizePx), "font"),

        // ---- Party Mit Recap ----
        E(ConfigWindow.NavKind.PartyRecap, "", "Enable Party Mit Recap", nameof(Configuration.RecapEnabled), "wipe review"),
        E(ConfigWindow.NavKind.PartyRecap, "", "Lock popup position", nameof(Configuration.RecapPopupLocked), "drag"),

        // ---- Fren Meter ----
        E(ConfigWindow.NavKind.Meter, "", "Enable Fren Meter", nameof(Configuration.MeterEnabled), "damage dps parse"),
        E(ConfigWindow.NavKind.Meter, "Display", "Lock position and size", nameof(Configuration.MeterLocked), "drag"),
        E(ConfigWindow.NavKind.Meter, "Display", "Click-through", nameof(Configuration.MeterClickThrough), "mouse"),
        E(ConfigWindow.NavKind.Meter, "Display", "Rank numbers", nameof(Configuration.MeterShowRank), "position"),
        E(ConfigWindow.NavKind.Meter, "Display", "Job icons", nameof(Configuration.MeterShowJobIcons), "class"),
        E(ConfigWindow.NavKind.Meter, "Columns", "Column labels", nameof(Configuration.MeterColumnHeader), "header"),
        E(ConfigWindow.NavKind.Meter, "Style", "Call your row \"You\"", nameof(Configuration.MeterYou), "name"),
        E(ConfigWindow.NavKind.Meter, "Display", "Limit break row", nameof(Configuration.MeterLimitBreakRow), "lb"),
        E(ConfigWindow.NavKind.Meter, "Display", "Split DPS/HPS", nameof(Configuration.MeterSplitHealing), "healing"),
        E(ConfigWindow.NavKind.Meter, "Display", "Rows shown", nameof(Configuration.MeterMaxRows), "how many"),
        E(ConfigWindow.NavKind.Meter, "Display", "Raid rDPS total", nameof(Configuration.MeterShowRaidTotal), "sum"),
        E(ConfigWindow.NavKind.Meter, "Display", "DPS / HPS tabs", nameof(Configuration.MeterHealingTab), "tabs"),
        E(ConfigWindow.NavKind.Meter, "Display", "Buttons bar", nameof(Configuration.MeterButtons), "history pause reset"),
        E(ConfigWindow.NavKind.Meter, "Display", "Death count", nameof(Configuration.MeterFooterDeaths), "deaths footer"),
        E(ConfigWindow.NavKind.Meter, "Display", "Show", nameof(Configuration.MeterShowMode), "always after a pull only in combat visible auto hide"),
        E(ConfigWindow.NavKind.Meter, "Display", "Action icons", nameof(Configuration.MeterBreakdownIcons), "breakdown"),
        E(ConfigWindow.NavKind.Meter, "Display", "Color each ability", nameof(Configuration.MeterBreakdownColors), "breakdown"),
        E(ConfigWindow.NavKind.Meter, "Style", "Color by job", nameof(Configuration.MeterJobColors), "bar color"),
        E(ConfigWindow.NavKind.Meter, "Style", "Solid bars", nameof(Configuration.MeterBarSolid), "opaque"),
        E(ConfigWindow.NavKind.Meter, "Style", "Highlight your row", nameof(Configuration.MeterHighlightYou), "you"),
        E(ConfigWindow.NavKind.Meter, "Style", "Drop shadow", nameof(Configuration.MeterTextShadow), "outline"),
    };

    // Settings on one tab that are off their defaults.
    public static List<Entry> ChangedIn(Configuration c, ConfigWindow.NavKind nav, string tab)
        => All.Where(e => e.Nav == nav && e.Tab == tab && e.IsChanged(c)).ToList();

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
