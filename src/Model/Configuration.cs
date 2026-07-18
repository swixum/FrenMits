using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;

namespace FrenMits;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // Last plugin version whose "What's New" panel was dismissed. Shows the panel
    // once after an update with notes, then records the version so it stays hidden.
    public string LastWhatsNew { get; set; } = "";

    public List<FightProfile> Fights { get; set; } = new();

    // Whether the Sheet View's per-phase "Sheet notes" panel is expanded.
    public bool SheetNotesOpen { get; set; } = true;

    // Height (px) of the Sheet View notes panel; dragged via its top edge.
    public float SheetNotesHeight { get; set; } = 150f;

    // The fight Sheet View last showed, so it reopens where you left off.
    public string LastSheetFightId { get; set; } = "";

    // Show a tiny once-per-entry popup naming your slot for the duty's sheet
    // (official or custom), with a picker to change it. Off by default.
    public bool ShowSlotPopupOnEntry { get; set; }

    // Color Sheet View mits by type (party / tank / personal). Off by default;
    // it's a lot of color on a full grid.
    public bool SheetColorByType { get; set; }

    // Slot codes the user pinned in Sheet View (right-click a column header).
    // Pinned columns ride next to Mechanic inside the frozen area.
    public List<string> SheetPinnedSlots { get; set; } = new();

    // FFLogs API client credentials (the user creates a client once at
    // fflogs.com/api/clients); used by Sheet View's "Import log" to turn a
    // report's casts into rows + anchors. Local only, never sent anywhere else.
    public string FflogsClientId { get; set; } = "";
    public string FflogsClientSecret { get; set; } = "";

    // Built-in fight territories already auto-added to the list, so a newly
    // shipped built-in shows up directly (no button) while a deleted one stays gone.
    public List<uint> SeededTerritories { get; set; } = new();

    // "Auto" follows your current job; otherwise a job abbreviation override.
    public string JobSelection { get; set; } = "Auto";

    // Global sheet-role pick (e.g. "Melee 1", "Main Tank"). When set, it's applied
    // to every built-in fight, mapping to whichever slot code that fight uses for
    // the role. Empty = pick a slot per fight. See Builtin.Roles / Builtin.RoleSlot.
    public string RoleSelection { get; set; } = "";

    // Seconds of lead time the warning appears before the mit time.
    public float WarningSeconds { get; set; } = 3f;
    // How long the call stays on screen after its time passes.
    public float HoldSeconds { get; set; } = 2f;

    // Only run the overlay while in the fight's territory.
    public bool OnlyInTargetTerritory { get; set; } = true;
    // Show the overlay even out of combat / out of duty for placement + testing.
    public bool TestMode { get; set; }

    // Overlay appearance.
    public float OverlayFontSizePx { get; set; } = 40f;     // crisp font size for the call
    public float UpcomingFontSizePx { get; set; } = 20f;    // crisp font size for upcoming list
    public float OverlayFontScale { get; set; } = 2.4f;     // fallback scale if font build is unavailable
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

    // Next-mits timeline style: 1 = mechanic board (every upcoming hit as a
    // countdown bar with your presses underneath), 0 = compact list of just
    // your own calls (the original look).
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
    // Board style: the countdown seconds on the right of each bar.
    public bool UpcomingBoardTimeText { get; set; } = true;

    // Run a boss timeline in EVERY instanced duty, even without a sheet: the
    // board lists the bosses' casts (no mits, no audio). Baked data covers
    // nearly every dungeon, trial and raid.
    public bool UniversalTimelines { get; set; } = true;
    // Board style: trim the board to just the rows you have a press for.
    // Off by default, so the whole fight shows with your presses highlighted.
    public bool UpcomingBoardOnlyMine { get; set; }

    // Board appearance (all defaults = the FrenMits look). Colors are ABGR;
    // a zeroed color falls back to the theme default so nothing can vanish.
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
    public bool UpcomingBoardShowNotes { get; set; } = true;         // sheet notes under gold/green rows
    public bool UpcomingBoardShowSeverity { get; set; } = true;      // !/!!/!!! marks from graded sheets

    // The next-mits timeline lives in its own window with its own placement.
    public bool TimelineLocked { get; set; }
    public Vector2 TimelinePosition { get; set; } = new(0.5f, 0.62f);

    // Capture the recap automatically every pull (always on unless unticked) so you
    // never have to trigger it by hand.
    public bool RecapAutoCapture { get; set; } = true;
    // Auto-show the "Mit Recap" popup after every wipe. Off by default (opt-in);
    // when on it always appears. Both the popup and the recap window are movable.
    public bool ShowRecapButton { get; set; } = false;
    public Vector2 RecapPopupPosition { get; set; } = new(0.5f, 0.28f);
    public bool RecapPopupLocked { get; set; }

    // Active-mitigations indicator (your live defensive buffs).
    public bool ShowMitBar { get; set; }
    public bool MitBarLocked { get; set; } = true;
    public Vector2 MitBarPosition { get; set; } = new(0.5f, 0.88f);
    public float MitBarFontSizePx { get; set; } = 18f;

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

    // Text templates. Placeholders: {action} {mechanic} {time} {count} {remaining}
    // Default mirrors the "Raidwide (3.3)" style: name + a one-decimal countdown.
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

    // Colour the call text by the kind of mit (party / tank / personal). Only
    // applies to lines without their own colour override.
    public bool ColorByMitType { get; set; } = true;
    public uint MitColorParty { get; set; } = 0xFFF68C3C;    // blue
    public uint MitColorTank { get; set; } = 0xFF3C5AF0;     // red
    public uint MitColorPersonal { get; set; } = 0xFF78C846; // green
    // Radial countdown ring around the call icon.
    public bool ShowRadialRing { get; set; } = true;

    // Cooldown-aware calls: read your real recast and warn on a call when the mit
    // won't be ready in time. Reads game state, so off by default.
    public bool CooldownAwareCalls { get; set; }
    // Icon size relative to the call text height (1.0 = same height as the text).
    public float IconScale { get; set; } = 0.8f;

    // Server-info (DTR) bar entry showing the next mit.
    public bool ShowDtrBar { get; set; } = true;

    // Write a per-pull diagnostics file (resync + cue events) to the plugin's
    // diagnostics/ folder. Local only; for reviewing resync accuracy. On by default.
    public bool Diagnostics { get; set; } = true;

    // Resync: snap the pull-clock when known boss casts happen.
    public bool EnableSync { get; set; } = true;
    public float SyncWindowSeconds { get; set; } = 8f;        // backward window, mechanic anchors (fine drift)
    public float SyncPhaseWindowSeconds { get; set; } = 60f;  // backward window, phase anchors (re-base)
    // Forward window, like cactbot's wide sync windows: how far AHEAD of the clock
    // an anchor may be and still snap onto it. Lets a loop/jump-coordinate timeline
    // (the legacy ultimates) jump the clock forward onto the next segment. Large by
    // design; a tight backward window keeps it from snapping back on repeats.
    public float SyncForwardWindowSeconds { get; set; } = 2000f;

    // Which sheet slot (MT/OT/WHM/AST/SCH/SGE/D1..D4/Extras) the baked DMU
    // timeline was last loaded for, for display.
    public string DmuSlot { get; set; } = "";

    // Audio cues (text-to-speech).
    public bool AudioEnabled { get; set; }
    public bool TtsEnabled { get; set; } = true;
    public int TtsRate { get; set; } = 1;     // -10..10
    public int TtsVolume { get; set; } = 90;  // 0..100
    public string TtsVoice { get; set; } = ""; // SAPI voice (empty = system default)
    // Online neural voices (Microsoft Edge "Read Aloud" — free, no key). Falls back
    // to a Windows voice if offline.
    public bool TtsUseEdge { get; set; } = true;
    public string TtsEdgeVoice { get; set; } = "en-US-AriaNeural";
    // Optional override: any Edge voice id (e.g. "en-US-AvaMultilingualNeural").
    // When set, used instead of the picker selection.
    public string TtsCustomVoice { get; set; } = "";
    // Speak the mechanic name instead of the action (unless a per-line override is set).
    public bool TtsSpeakMechanic { get; set; }
    // Minimum seconds between any two spoken cues (0 = no limit). Prevents pile-ups.
    public float TtsMinGapSeconds { get; set; }

    // Overlay placement. When not locked it can be dragged.
    public bool OverlayLocked { get; set; }
    public bool UseCustomPosition { get; set; }
    public Vector2 OverlayPosition { get; set; } = new(0.5f, 0.35f); // fractions of the screen

    // Set when the on-disk config existed but could not be loaded. We keep working
    // defaults in memory for the session but must NOT write them back, or we would
    // overwrite the user's real (recoverable) settings with defaults.
    public static bool SuppressSave;

    // When the config last hit disk, so the UI can show a truthful live status
    // ("All changes saved · 3s ago") instead of a ceremonial Save button.
    public static DateTime LastSavedAt { get; private set; } = DateTime.MinValue;

    public void Save()
    {
        if (SuppressSave) return;
        Service.PluginInterface.SavePluginConfig(this);
        LastSavedAt = DateTime.Now;
    }
}
