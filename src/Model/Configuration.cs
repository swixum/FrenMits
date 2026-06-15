using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;

namespace FrenMits;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public List<FightProfile> Fights { get; set; } = new();

    // Built-in fight territories already auto-added to the list, so a newly
    // shipped built-in shows up directly (no button) while a deleted one stays gone.
    public List<uint> SeededTerritories { get; set; } = new();

    // "Auto" follows your current job; otherwise a job abbreviation override.
    public string JobSelection { get; set; } = "Auto";

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

    public void Save() => Service.PluginInterface.SavePluginConfig(this);
}
