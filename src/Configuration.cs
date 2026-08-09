using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;

namespace FrenMits;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // Last plugin version whose "What's New" panel was dismissed.
    public string LastWhatsNew { get; set; } = "";

    // Whether to show official mechanics that have no actions assigned
    public bool ShowEmptyMechanics { get; set; } = true;

    // Your fight plans.
    [Newtonsoft.Json.JsonIgnore]
    public List<FightProfile> Fights { get; set; } = new();

    // Where fights used to sit, INSIDE the config.
    [Newtonsoft.Json.JsonProperty("Fights")]
    public List<FightProfile>? LegacyFights { get; set; }

    public bool ShouldSerializeLegacyFights() => false;

    // Whether the Sheet View's per-phase "Sheet notes" panel is expanded.
    public bool SheetNotesOpen { get; set; } = true;

    // Height (px) of the Sheet View notes panel; dragged via its top edge.
    public float SheetNotesHeight { get; set; } = 150f;

    // The fight Sheet View last showed, so it reopens where you left off.
    public string LastSheetFightId { get; set; } = "";

    // Bumped by "Reset column widths". It rides in the grid's table id, so a
    // bump hands ImGui a table it has no saved widths for. Stored, or the old
    // id would come back on the next launch and with it the old widths.
    public int SheetWidthReset { get; set; }

    // When the meter is on screen: 0 always, 1 after a pull, 2 only in combat.
    // The two booleans behind it can contradict each other and the hide one wins,
    // so nothing sets them directly any more; both are kept so old configs and
    // shared profiles still load.
    [Newtonsoft.Json.JsonIgnore]
    public int MeterShowMode
    {
        get => MeterHideOutOfCombat ? 2 : MeterAlwaysShow ? 0 : 1;
        set
        {
            MeterHideOutOfCombat = value == 2;
            MeterAlwaysShow = value == 0;
        }
    }

    // A small once-per-entry popup naming your slot for the duty.
    public bool ShowSlotPopupOnEntry { get; set; }

    // Color Sheet View mits by type (party / tank / personal).
    public bool SheetColorByType { get; set; }

    // Colorblind-safe status colors: an Okabe-Ito set in place of green/amber/red.
    public bool ColorblindMode { get; set; }

    // The one interactive color every plugin window is drawn with.
    public uint AccentColor { get; set; } = Ui.Theme.DefaultAccent;

    // Text and spacing multiplier for the plugin's own windows.
    public float UiScale { get; set; } = 1f;

    // In-game overlays take the accent above instead of their own. Off by
    // default, so an existing board keeps whatever color it was set to.
    public bool OverlaysFollowAccent { get; set; }

    // What the Next Mits board and the meter actually draw with.
    [Newtonsoft.Json.JsonIgnore]
    public uint BoardAccent => OverlaysFollowAccent ? AccentColor : UpcomingBoardAccentColor;

    [Newtonsoft.Json.JsonIgnore]
    public uint MeterAccent => OverlaysFollowAccent ? AccentColor : MeterAccentColor;

    // Learned downtime lengths per territory, keyed by id.
    public Dictionary<string, List<DowntimeWindow>> LearnedDowntimes { get; set; } = new();

    // Slot codes the user pinned in Sheet View (right-click a column header).
    public List<string> SheetPinnedSlots { get; set; } = new();

    // logs API client credentials, used by Sheet View's log import.
    public string FflogsClientId { get; set; } = "";
    public string FflogsClientSecretEnc { get; set; } = "";

    // Plaintext view of the secret, decrypted lazily and cached.
    [Newtonsoft.Json.JsonIgnore]
    public string FflogsClientSecret
    {
        get
        {
            if (!string.Equals(_secretCacheFor, FflogsClientSecretEnc, StringComparison.Ordinal))
            {
                _secretCache = SecretVault.Unprotect(FflogsClientSecretEnc);
                _secretCacheFor = FflogsClientSecretEnc;
            }
            return _secretCache;
        }
        set
        {
            FflogsClientSecretEnc = SecretVault.Protect(value);
            _secretCache = value ?? "";
            _secretCacheFor = FflogsClientSecretEnc;
        }
    }
    private string _secretCache = "";
    private string? _secretCacheFor;

    // Catches the old plaintext key from pre-v23 configs.
    [Newtonsoft.Json.JsonProperty("FflogsClientSecret")]
    private string LegacyFflogsSecret { get; set; } = "";

    // Move a pre-v23 plaintext secret into the encrypted slot.
    public bool MigrateFflogsSecret()
    {
        if (LegacyFflogsSecret.Length == 0) return false;
        if (FflogsClientSecretEnc.Length == 0) FflogsClientSecret = LegacyFflogsSecret;
        LegacyFflogsSecret = "";
        return true;
    }

    // Built-ins already auto-added, so a deleted one stays gone.
    public List<uint> SeededTerritories { get; set; } = new();

    // Global sheet-role pick, applied to every built-in fight.
    public string RoleSelection { get; set; } = "";

    // Toggle to use the "Setup" preferences instead of manual overrides
    public bool UseSetup { get; set; } = true;

    // Legacy role preferences, preserved for smooth JSON loading.
    public Dictionary<JobRole, string> GlobalRolePreferences { get; set; } = new();

    // The seat preferences a default-slot guess reads, derived not stored.
    [Newtonsoft.Json.JsonIgnore]
    public SlotPrefs SlotPrefs => new(JobSlotPreferences, GlobalRolePreferences);

    // Preferences per specific job (e.g., RDM -> M2, PCT -> D4)
    public Dictionary<string, string> JobSlotPreferences { get; set; } = new();

    // Seconds of lead time the warning appears before the mit time.
    public float WarningSeconds { get; set; } = 3f;
    // How long the call stays on screen after its time passes.
    public float HoldSeconds { get; set; } = 2f;
    // The same lead, for a call the solver gave a real usage window. Its own
    // setting because the two are different asks: a plain call is one moment
    // you want warning of, a windowed call is a span you want to be inside.
    public float UseWindowLeadSeconds { get; set; } = 2f;

    // How early a call appears. A per-line override wins; otherwise a windowed
    // press leads by its own setting.
    public float LeadFor(MitPress p)
        => p.SourceLine.LeadOverride > 0f ? p.SourceLine.LeadOverride
           : p.HasWindow && !p.SourceLine.HasCallOffset ? UseWindowLeadSeconds : WarningSeconds;

    // How long a call lingers once its moment has passed. A windowed press
    // never lingers: the window IS its time on screen, so holding it past the
    // close would go on saying "press this" after the chance to press it went.
    public float HoldFor(MitPress p) => p.HasWindow && !p.SourceLine.HasCallOffset ? 0f : HoldSeconds;

    // Only run the overlay while in the fight's territory.
    public bool OnlyInTargetTerritory { get; set; } = true;
    // Show the overlay even out of combat / out of duty for placement + testing.
    public bool TestMode { get; set; }

    // Overlay appearance.
    public float OverlayFontSizePx { get; set; } = 40f;     // crisp font size for the call
    public float UpcomingFontSizePx { get; set; } = 20f;    // crisp font size for upcoming list
    public string OverlayFontFamily { get; set; } = "Default"; // "Default" or a Windows font name
    public bool OverlayFontBold { get; set; }
    public bool OverlayFontItalic { get; set; }
    public int OverlayTextAlign { get; set; } = 1;          // 0 = left, 1 = center, 2 = right
    public uint OverlayColorImminent { get; set; } = 0xFF55FFFF; // ABGR (yellow)
    public uint OverlayColorActive { get; set; } = 0xFF55FF55;   // ABGR (green)
    public uint OverlayColorMechanic { get; set; } = 0xC0FFFFFF; // ABGR (white)
    public uint OverlayColorUpcoming { get; set; } = 0xB0FFFFFF;
    public bool ShowCountdownNumber { get; set; } = false;
    public bool ShowUpcoming { get; set; } = true;
    public int UpcomingCount { get; set; } = 3;
    public float UpcomingLookaheadSeconds { get; set; } = 30f;

    // Next-mits style: 1 = mechanic board, 0 = compact list of your own calls.
    public int UpcomingStyle { get; set; } = 1;
    // Board style: how many mechanic bars show at once.
    public int UpcomingBoardRows { get; set; } = 8;
    // Board style: its own look-ahead window (the bars drain across this span).
    public float UpcomingBoardLookaheadSeconds { get; set; } = 60f;
    // Board style: bar width in px.
    public float UpcomingBoardWidth { get; set; } = 340f;
    // Board style: the header above the bars, with each piece toggleable.
    public bool UpcomingShowHeader { get; set; } = true;
    public bool UpcomingHeaderTitle { get; set; } = true;   // fight name (+ the accent diamond)
    public bool UpcomingHeaderClock { get; set; } = true;   // fight clock on the right
    public bool UpcomingHeaderRule { get; set; } = true;    // accent underline
    public bool UpcomingHeaderSlot { get; set; } = true;    // your slot + job badge
    public bool UpcomingHeaderSync { get; set; } = true;    // brief "synced" note after a resync
    // Board style: the countdown seconds on the right of each bar.
    public bool UpcomingBoardTimeText { get; set; } = true;

    // Run a boss timeline in every duty, with no mits or audio.
    public bool UniversalTimelines { get; set; } = true;

    // Learn a boss's timeline from your own pulls where there's no baked one.
    public bool LearnTimelines { get; set; } = true;

    // Learned boss timelines, keyed by the boss's NameId - see TimelineLearner.
    public Dictionary<string, LearnedFight> LearnedFights { get; set; } = new();
    // Board style: trim the board to just the rows you have a press for.
    public bool UpcomingBoardOnlyMine { get; set; }

    // Board appearance (all defaults = the FrenMits look).
    public uint UpcomingBoardAccentColor { get; set; } = Ui.Theme.DefaultAccent; // stripe/fill/header (FrenMits blue)
    public uint UpcomingBoardNextColor { get; set; } = 0xFF28BEFF;   // your next press (gold)
    public uint UpcomingBoardNowColor { get; set; } = 0xFF64DC64;    // press it now (green)
    public float UpcomingBoardBgOpacity { get; set; } = 0.85f;       // bar background opacity
    public float UpcomingBoardRounding { get; set; } = 5f;           // bar corner rounding (px)
    public float UpcomingBoardBarPad { get; set; } = 8f;             // bar thickness beyond the text (px)
    public float UpcomingBoardRowGap { get; set; } = 4f;             // space between rows (px)
    public bool UpcomingBoardStripe { get; set; } = true;            // left accent stripe on each bar
    public bool UpcomingBoardDrain { get; set; } = true;             // true = bar drains as the hit nears
    public bool UpcomingBoardShowActions { get; set; } = true;       // presses under the rows
    public bool UpcomingBoardShowSeverity { get; set; } = true;      // !/!!/!!! marks from graded sheets
    public bool UpcomingBoardShowType { get; set; } = true;          // raidwide / tank-buster icon per row
    public bool UpcomingBoardTypeChip { get; set; } = true;          // Buster / Raid AOE / Enrage chip per row
    public bool UpcomingBoardTypeChipShort { get; set; }             // TB / AOE / ENR instead of full labels
    public bool UpcomingBossPosition { get; set; } = true;           // live boss compass position row (North/Middle...)
    public bool UpcomingBoardPhases { get; set; } = true;            // labelled rule where a phase begins

    // The next-mits timeline lives in its own window with its own placement.
    public bool TimelineLocked { get; set; }
    public Vector2 TimelinePosition { get; set; } = new(0.5f, 0.62f);

    // Party Mit Recap master switch.
    public bool RecapEnabled { get; set; }
    public Vector2 RecapPopupPosition { get; set; } = new(0.5f, 0.28f);
    public bool RecapPopupLocked { get; set; }

    // Active-mitigations indicator (your live defensive buffs).
    public bool ShowMitBar { get; set; }
    public bool MitBarLocked { get; set; } = true;
    public Vector2 MitBarPosition { get; set; } = new(0.5f, 0.88f);
    public float MitBarFontSizePx { get; set; } = 18f;

    // Food check: warn when your food is missing or about to go.
    public bool PrepCheckEnabled { get; set; }
    // Minutes of food left at which the warning starts.
    public float PrepCheckWarnMinutes { get; set; } = 4f;
    // Mid-fight "Potion is Available!" note when a used pot comes off recast.
    public bool PrepCheckPotion { get; set; }
    // Speak each of these once, in the Audio page's voice.
    public bool PrepCheckTts { get; set; }

    // Optional extras, all off so the check behaves as it shipped.
    public bool PrepCheckUseFightLength { get; set; }
    // Flag food whose every stat is a crafting one (raiding on crafter food).
    public bool PrepCheckWarnWrongFood { get; set; }
    // Flag food that isn't HQ.
    public bool PrepCheckWarnNq { get; set; }
    // Keep the food timer on screen even when there's nothing wrong.
    public bool PrepCheckAlwaysShowFood { get; set; }
    // Also answer the in-game ready check, wherever you are.
    public bool PrepCheckOnReadyCheck { get; set; }
    // Count down to the pot being ready, rather than only saying so when it is.
    public bool PrepCheckPotCountdown { get; set; }
    // Append how many you have left in your bags.
    public bool PrepCheckShowCounts { get; set; }
    // Only run where you have a real sheet, so leveling roulettes stay quiet.
    public bool PrepCheckSheetsOnly { get; set; }
    public bool PrepCheckLocked { get; set; } = true;
    public Vector2 PrepCheckPosition { get; set; } = new(0.5f, 0.72f);
    public float PrepCheckFontSizePx { get; set; } = 18f;

    // Combat timer: a plain stopwatch (mm:ss) of the current pull, its own overlay.
    public bool ShowCombatTimer { get; set; }
    public bool CombatTimerLocked { get; set; } = true;
    public Vector2 CombatTimerPosition { get; set; } = new(0.5f, 0.08f);
    public string CombatTimerFontFamily { get; set; } = "Default";
    public bool CombatTimerFontBold { get; set; }
    public bool CombatTimerFontItalic { get; set; }
    public float CombatTimerFontSizePx { get; set; } = 28f;
    public uint CombatTimerColor { get; set; } = 0xFFFFFFFF;        // ABGR (white)
    public bool CombatTimerShowBackground { get; set; }
    public uint CombatTimerBackgroundColor { get; set; } = 0xB0000000; // ABGR (dim black)

    // Fren Meter: the parser-fed damage meter overlay with rDPS.
    public bool MeterEnabled { get; set; }
    public int MeterConnection { get; set; }             // 0 auto, 1 in-process parser, 2 WebSocket
    // Hidden, so a split fight can be restitched by hand.
    public bool MeterStitchSegments { get; set; }
    public string MeterSocketAddress { get; set; } = "ws://127.0.0.1:10501/ws";
    // The ACT steps stay up until someone says they have read them.
    public bool MeterSetupDone { get; set; }
    public bool MeterLocked { get; set; }
    public bool MeterClickThrough { get; set; }
    public Vector2 MeterPosition { get; set; } = new(0.8f, 0.72f);
    public Vector2 MeterSize { get; set; } = new(360f, 300f);
    public int MeterMode { get; set; }                   // 0 damage, 1 healing, 2 taken, 3 deaths
    // Replace, or a reload appends into the seeded defaults.
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public List<string> MeterColumns { get; set; } = new() { "rdps", "dps", "dmgpct" };

    // The healing view keeps its own columns: healing numbers, no damage ones.
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public List<string> MeterHealColumns { get; set; } = new() { "hps", "healpct", "dshield", "overheal" };

    // Repairs a doubled column list, keeping the saved order.
    public static bool DedupeMeterColumns(List<string> cols)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = false;
        for (var i = cols.Count - 1; i >= 0; i--)
            if (!seen.Add(cols[i]))
            {
                cols.RemoveAt(i);
                changed = true;
            }
        return changed;
    }
    public int MeterHeaderStyle { get; set; } = 1;       // 0 full, 1 slim, 2 none
    public int MeterBarStyle { get; set; }               // 0 flat, 1 glass, 2 gradient
    public bool MeterButtons { get; set; } = true;       // footer bar: pulls, pause, reset
    public bool MeterHealingTab { get; set; } = true;    // Damage / Healing tabs in the footer
    public string MeterTabNameDamage { get; set; } = "DPS";
    public string MeterTabNameHealing { get; set; } = "HPS";
    public bool MeterHighlightYou { get; set; } = true;  // outline your own row
    public bool MeterColumnHeader { get; set; } = true;
    public bool MeterShowRank { get; set; } = true;
    public bool MeterShowJobIcons { get; set; } = true;
    public int MeterNameStyle { get; set; }              // 0 full, 1 first name, 2 initials
    public bool MeterYou { get; set; } = true;           // your own row reads "You"
    public bool MeterShowRaidTotal { get; set; } = true;
    public float MeterFontSizePx { get; set; } = 15f;
    public string MeterFontFamily { get; set; } = "Default";
    public bool MeterFontBold { get; set; }
    public bool MeterFontItalic { get; set; }
    public float MeterBarHeight { get; set; } = 24f;
    public float MeterBarGap { get; set; } = 3f;
    public float MeterRounding { get; set; } = 5f;
    public bool MeterJobColors { get; set; } = true;
    public uint MeterAccentColor { get; set; } = Ui.Theme.DefaultAccent;
    public uint MeterTextColor { get; set; } = 0xFFFFFFFF;    // names and lead values
    public uint MeterSubColor { get; set; } = 0xFFFFFFFF;     // ranks, labels, other columns
    public uint MeterBgColor { get; set; } = 0xB80D0A09;      // window, alpha included
    public uint MeterRowColor { get; set; } = 0x17FFFFFF;     // bar background
    public uint MeterYouColor { get; set; } = Ui.Theme.DefaultAccent;     // your name in the list
    public uint MeterTimerColor { get; set; } = 0xFFFFFFFF;   // the encounter clock
    public uint MeterHighlightColor { get; set; } = Ui.Theme.DefaultAccent; // the wash over your row
    public uint MeterTitleColor { get; set; } = 0xFFFFFFFF;   // the encounter name
    public uint MeterBorderColor { get; set; } = (Ui.Theme.DefaultAccent & 0x00FFFFFF) | 0x2E000000; // the window edge
    public int MeterHighlightStyle { get; set; }              // 0 wash + outline, 1 wash, 2 outline, 3 stripe
    public float MeterHighlightStrength { get; set; } = 1f;
    public float MeterBarOpacity { get; set; } = 1f;
    // Fills bars with the job color itself instead of a wash of it.
    public bool MeterBarSolid { get; set; }
    public int MeterMaxRows { get; set; }                     // 0 shows everyone
    public bool MeterTextShadow { get; set; } = true;
    public bool MeterHideOutOfCombat { get; set; }
    public bool MeterBreakdownIcons { get; set; } = true;   // action icons in a player's breakdown
    public bool MeterBreakdownColors { get; set; } = true;  // a color per ability, not one per job
    public bool MeterFooterDeaths { get; set; } = true;     // the pull's death count in the footer
    public bool MeterLimitBreakRow { get; set; } = true;    // limit break in its own row under the party
    public bool MeterSplitHealing { get; set; }             // DPS on top, the healers' HPS below
    // No UI for this: the meter's session diag file, off unless /fm meterdiag.
    public bool MeterDiagFile { get; set; }
    public float MeterRefreshSeconds { get; set; } = 1f;    // how often values settle; 0 is every frame
    public bool MeterCollapsed { get; set; }                // rolled up to just its header
    public bool MeterAlwaysShow { get; set; } = true;       // stay on screen with no pull to show

    // Saved meter profiles (name -> share code) and which one is active.
    public Dictionary<string, string> MeterProfiles { get; set; } = new();
    public string MeterProfileName { get; set; } = "";

    // Preset memory: a look or theme keeps your tweaks for when you come back.
    public string OverlayLookName { get; set; } = "";
    public Dictionary<string, LookMemory> SavedLooks { get; set; } = new();
    public string MeterThemeName { get; set; } = "";
    public Dictionary<string, MeterThemeMemory> SavedMeterThemes { get; set; } = new();

    public sealed class LookMemory
    {
        public int Style;
        public bool Icon, Mech, Panel, Spark, Bar, Number, Pulse;
    }

    public sealed class MeterThemeMemory
    {
        public uint Accent, Text, Sub, Title, Timer, You, Highlight, Border, Bg, Rows;
        public float Rounding;
        public bool JobColors;
        public int BarStyle;
    }

    // Text templates; placeholders: {action} {mechanic} {time} {count} {remaining}.
    public string HeadlineFormat { get; set; } = "{action} ({remaining})";
    public string ActiveSuffix { get; set; } = "  NOW";
    public bool ShowMechanicLine { get; set; } = true;

    // Background + outline for readability over the game.
    public bool ShowBackground { get; set; }
    public uint BackgroundColor { get; set; } = 0xB0000000; // ABGR (dim black)
    public bool TextShadow { get; set; } = true;

    // Countdown bar under the call.
    public bool ShowProgressBar { get; set; } = true;
    public float ProgressBarHeight { get; set; } = 6f;
    public float ProgressBarWidthPx { get; set; } = 280f;
    public bool PulseWhenImminent { get; set; } = true;
    public bool ShowAbilityIcon { get; set; } = true;

    // Center-call presentation: 0 = classic text, 1 = board look, 2 = icon + clock.
    public int OverlayStyle { get; set; }

    // Colour the call text by the kind of mit (party / tank / personal).
    public bool ColorByMitType { get; set; } = true;
    public uint MitColorParty { get; set; } = 0xFFF68C3C;    // blue
    public uint MitColorTank { get; set; } = 0xFF3C5AF0;     // red
    public uint MitColorPersonal { get; set; } = 0xFF78C846; // green
    // Radial countdown ring around the call icon.
    public bool ShowRadialRing { get; set; } = true;

    // Board-style backing plate behind the classic call.
    public bool OverlayCallPanel { get; set; }

    // A spark riding the bar edge across the classic call text.
    public bool OverlayTextSpark { get; set; } = true;

    // Height of the Pull history window's detail panel, dragged via its top edge.
    public float MeterHistoryDetailHeight { get; set; } = 170f;

    // Cooldown-aware calls: warn when a mit won't be ready.
    public bool CooldownAwareCalls { get; set; }

    // Read on load only, so v40 can tell a solved plan from a hand-tuned one.
    public bool AutoCooldownTiming { get; set; }
    public bool ShouldSerializeAutoCooldownTiming() => false;

    // Icon size relative to the call text height (1.0 = same height as the text).
    public float IconScale { get; set; } = 0.8f;

    // Server-info (DTR) bar entry showing the next mit.
    public bool ShowDtrBar { get; set; } = true;

    // Write a per-pull diagnostics file to the plugin folder.
    public bool Diagnostics { get; set; } = true;

    // Resync: snap the pull-clock when known boss casts happen.
    public bool EnableSync { get; set; } = true;
    public float SyncWindowSeconds { get; set; } = 8f;        // backward window, mechanic anchors (fine drift)
    public bool ShowUseWindows { get; set; } = true;

    // Widest a usage window may be drawn.
    public float MaxUseWindowSeconds { get; set; } = 7.5f;
    public float SyncPhaseWindowSeconds { get; set; } = 60f;  // backward window, phase anchors (re-base)
    // Forward window: how far ahead an anchor may still snap.
    public float SyncForwardWindowSeconds { get; set; } = 2000f;

    // Start the clock on the party's countdown instead of the combat flag.
    public bool StartOnCountdown { get; set; } = true;

    // Which sheet slot the baked DMU timeline last loaded, for display.
    public string DmuSlot { get; set; } = "";

    // Audio cues (text-to-speech).
    public bool AudioEnabled { get; set; }
    public bool TtsEnabled { get; set; } = true;
    public int TtsRate { get; set; } = 1;     // -10..10
    public int TtsVolume { get; set; } = 90;  // 0..100
    public string TtsVoice { get; set; } = ""; // SAPI voice (empty = system default)
    // Online neural voices (Microsoft Edge "Read Aloud" - free, no key).
    public bool TtsUseEdge { get; set; } = true;
    public string TtsEdgeVoice { get; set; } = "en-US-AriaNeural";
    // Any Edge voice id may override (e.g. "en-US-AvaMultilingualNeural").
    public string TtsCustomVoice { get; set; } = "";
    // Speak the mechanic instead of the action.
    public bool TtsSpeakMechanic { get; set; }
    // Minimum seconds between any two spoken cues (0 = no limit).
    public float TtsMinGapSeconds { get; set; }

    // Overlay placement; draggable when not locked.
    public bool OverlayLocked { get; set; }
    public Vector2 OverlayPosition { get; set; } = new(0.5f, 0.35f); // fractions of the screen

    // The config wouldn't load, so use defaults and never write.
    public static bool SuppressSave;

    // When the config last hit disk, for a truthful saved status.
    public static DateTime LastSavedAt { get; private set; } = DateTime.MinValue;

    [Newtonsoft.Json.JsonIgnore]
    public Action? PlanMutated;

    // Settings AND plans.
    public void Save()
    {
        SaveSettings();
        PlanStore.Save(Fights);
        PlanMutated?.Invoke();
    }

    // Bumped on every change, so watchers can skip work when nothing has changed.
    public static int SaveTick { get; private set; }

    // A drag asks to save every frame, so the write waits for the quiet.
    public const double QuietSeconds = 0.4;
    public const double HoldCeilingSeconds = 2;

    // Write once the asks stop, and never hold a change longer than the ceiling.
    public static bool WriteDue(double sinceLastAsk, double sinceFirstAsk)
        => sinceLastAsk >= QuietSeconds || sinceFirstAsk >= HoldCeilingSeconds
           // A clock that stepped backwards must not park the change forever.
           || sinceLastAsk < 0 || sinceFirstAsk < 0;

    private bool _dirty;
    private DateTime _firstAsk;
    private DateTime _lastAsk;

    // True while a change is still waiting on disk.
    public bool SavePending => _dirty;

    // Settings only, for paths that cannot have touched a plan.
    public void SaveSettings()
    {
        if (SuppressSave) return;
        var now = DateTime.UtcNow;
        if (!_dirty) { _dirty = true; _firstAsk = now; }
        _lastAsk = now;
        // Watchers follow the change itself, not the disk write behind it.
        SaveTick++;
    }

    // Called every frame: the write lands once the changes stop coming.
    public void FlushSettings()
    {
        if (!_dirty) return;
        var now = DateTime.UtcNow;
        if (!WriteDue((now - _lastAsk).TotalSeconds, (now - _firstAsk).TotalSeconds)) return;
        try { SaveSettingsNow(); }
        catch (Exception ex)
        {
            // Hold the change and try again shortly, rather than spin on a bad disk.
            _firstAsk = _lastAsk = now;
            Swallowed.Report("settings save", ex);
        }
    }

    // For anything that cannot wait, unload above all.
    public void SaveSettingsNow()
    {
        if (SuppressSave) { _dirty = false; return; }
        Service.PluginInterface.SavePluginConfig(this);
        LastSavedAt = DateTime.Now;
        // Cleared last, so a failed write is tried again.
        _dirty = false;
    }
}
