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

    // Show a tiny once-per-entry popup naming your slot for the duty's sheet
    // (official or custom), with a picker to change it.
    public bool ShowSlotPopupOnEntry { get; set; }

    // Color Sheet View mits by type (party / tank / personal).
    public bool SheetColorByType { get; set; }

    // Colorblind-safe status colors: an Okabe-Ito set in place of green/amber/red.
    public bool ColorblindMode { get; set; }

    // Learned downtime lengths per territory (key = territory id as string): filled
    // the first time you see each lull, then the timeline counts down to targetable.
    public Dictionary<string, List<DowntimeWindow>> LearnedDowntimes { get; set; } = new();

    // Slot codes the user pinned in Sheet View (right-click a column header).
    public List<string> SheetPinnedSlots { get; set; } = new();

    // logs API client credentials, used by Sheet View's log import.
    public string FflogsClientId { get; set; } = "";
    public string FflogsClientSecretEnc { get; set; } = "";

    // Plaintext view of the secret for call sites: decrypts lazily (cached, so
    // per-frame UI checks stay cheap) and re-encrypts on set.
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

    // Catches the old plaintext "FflogsClientSecret" key from pre-v23 configs
    // so MigrateFflogsSecret can move it into the encrypted field.
    [Newtonsoft.Json.JsonProperty("FflogsClientSecret")]
    private string LegacyFflogsSecret { get; set; } = "";

    // Move a pre-v23 plaintext secret into the encrypted slot; clears the old
    // key either way.
    public bool MigrateFflogsSecret()
    {
        if (LegacyFflogsSecret.Length == 0) return false;
        if (FflogsClientSecretEnc.Length == 0) FflogsClientSecret = LegacyFflogsSecret;
        LegacyFflogsSecret = "";
        return true;
    }

    // Built-in fight territories already auto-added to the list, so a newly
    // shipped built-in shows up directly (no button) while a deleted one stays gone.
    public List<uint> SeededTerritories { get; set; } = new();

    // "Auto" follows your current job; otherwise a job abbreviation override.
    public string JobSelection { get; set; } = "Auto";

    // Global sheet-role pick, applied to every built-in fight.
    public string RoleSelection { get; set; } = "";

    // Seconds of lead time the warning appears before the mit time.
    public float WarningSeconds { get; set; } = 3f;
    // How long the call stays on screen after its time passes.
    public float HoldSeconds { get; set; } = 2f;

    // Reaction window for AUTO-TIMED cooldown presses.
    public float CooldownLeadSeconds { get; set; } = 5f;

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

    // Run a boss timeline in EVERY instanced duty, even without a sheet: the
    // board lists the bosses' casts (no mits, no audio).
    public bool UniversalTimelines { get; set; } = true;

    // Learn a boss's timeline from your own pulls where there's no baked one.
    public bool LearnTimelines { get; set; } = true;

    // Learned boss timelines, keyed by the boss's NameId - see TimelineLearner.
    public Dictionary<string, LearnedFight> LearnedFights { get; set; } = new();
    // Board style: trim the board to just the rows you have a press for.
    public bool UpcomingBoardOnlyMine { get; set; }

    // Board appearance (all defaults = the FrenMits look).
    public uint UpcomingBoardAccentColor { get; set; } = 0xFFF6823B; // stripe/fill/header (FrenMits blue)
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

    // Food check: inside a duty, out of combat, warn when your food is missing
    // or about to expire mid-pull.
    public bool PrepCheckEnabled { get; set; }
    // Minutes of food left at which the warning starts.
    public float PrepCheckWarnMinutes { get; set; } = 4f;
    // Mid-fight "Potion is Available!" note when a used pot comes off recast.
    public bool PrepCheckPotion { get; set; }
    // Speak each of the above once as it appears, using the voice from the Audio
    // page.
    public bool PrepCheckTts { get; set; }

    // Optional extras, ALL off by default: with every one of these false the
    // check behaves exactly as it did when it shipped.
    public bool PrepCheckUseFightLength { get; set; }
    // Flag food whose every stat is a crafting one (raiding on crafter food).
    public bool PrepCheckWarnWrongFood { get; set; }
    // Flag food that isn't HQ.
    public bool PrepCheckWarnNq { get; set; }
    // Keep the food timer on screen even when there's nothing wrong.
    public bool PrepCheckAlwaysShowFood { get; set; }
    // Also answer the in-game ready check, in combat and outside a duty included
    // - the one moment somebody is actually asking whether you're ready.
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

    // Cooldown-aware calls: read your real recast and warn on a call when the mit
    // won't be ready in time.
    public bool CooldownAwareCalls { get; set; }

    // Auto cooldown timing: run the offset solver on zone-in and slot change.
    public bool AutoCooldownTiming { get; set; }

    // Prep press-window text: when a solved press fires early to stay up for a
    // later mechanic, add a "(use between X and Y)" line under the main call.
    public bool PrepAlerts { get; set; }
    // Icon size relative to the call text height (1.0 = same height as the text).
    public float IconScale { get; set; } = 0.8f;

    // Server-info (DTR) bar entry showing the next mit.
    public bool ShowDtrBar { get; set; } = true;

    // Write a per-pull diagnostics file (resync + cue events) to the plugin's
    // diagnostics/ folder.
    public bool Diagnostics { get; set; } = true;

    // Resync: snap the pull-clock when known boss casts happen.
    public bool EnableSync { get; set; } = true;
    public float SyncWindowSeconds { get; set; } = 8f;        // backward window, mechanic anchors (fine drift)
    public float SyncPhaseWindowSeconds { get; set; } = 60f;  // backward window, phase anchors (re-base)
    // Forward window, like cactbot's wide sync windows: how far AHEAD of the clock
    // an anchor may be and still snap onto it.
    public float SyncForwardWindowSeconds { get; set; } = 2000f;

    // Start the clock on the party's countdown instead of the combat flag.
    public bool StartOnCountdown { get; set; } = true;

    // Which sheet slot (MT/OT/WHM/AST/SCH/SGE/D1..D4/Extras) the baked DMU
    // timeline was last loaded for, for display.
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
    // Optional override: any Edge voice id (e.g. "en-US-AvaMultilingualNeural").
    public string TtsCustomVoice { get; set; } = "";
    // Speak the mechanic name instead of the action (unless a per-line override is set).
    public bool TtsSpeakMechanic { get; set; }
    // Minimum seconds between any two spoken cues (0 = no limit).
    public float TtsMinGapSeconds { get; set; }

    // Overlay placement; draggable when not locked.
    public bool OverlayLocked { get; set; }
    public Vector2 OverlayPosition { get; set; } = new(0.5f, 0.35f); // fractions of the screen

    // The config existed but wouldn't load: work from defaults but never write them
    // back.
    public static bool SuppressSave;

    // When the config last hit disk, so the UI can show a truthful live status
    // ("All changes saved · 3s ago") instead of a ceremonial Save button.
    public static DateTime LastSavedAt { get; private set; } = DateTime.MinValue;

    // Settings AND plans.
    public void Save()
    {
        SaveSettings();
        PlanStore.Save(Fights);
    }

    // Settings only, for paths that provably cannot have touched a plan - the
    // config window's toggles and the overlays' drag-to-move.
    public void SaveSettings()
    {
        if (SuppressSave) return;
        Service.PluginInterface.SavePluginConfig(this);
        LastSavedAt = DateTime.Now;
    }
}
