using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenAlerts.Ui;

internal static class SettingsIndex
{
    internal sealed record Entry(
        string Label,
        ConfigWindow.NavKind Nav,
        string Prop,
        Func<Configuration, bool> Changed,
        Action<Configuration> Reset,
        string Keywords = "",
        ConfigWindow.NavKind? Also = null)
    {
        // Every page this switch is actually on.
        //
        // Nav is its home: where search sends somebody, and the page it is listed under
        // once. Also is a second page that draws the same switch, which reset and the
        // changed marker both have to know about and search must not, or the same
        // setting comes back twice in the results.
        //
        // TTS is why. It is on the TTS page and again on Call Display, and it was filed
        // once: resetting Call Display put every switch on it back except that one,
        // which is a reset button quietly not doing what it says.
        public bool On(ConfigWindow.NavKind nav) => Nav == nav || Also == nav;
    }

    private static readonly Configuration Defaults = new();

    private static readonly Entry[] Items =
    {
        // ---- fights ----
        New("Call everything", ConfigWindow.NavKind.Fights, nameof(Configuration.AllCallsOn),
            c => c.AllCallsOn != Defaults.AllCallsOn, c => c.AllCallsOn = Defaults.AllCallsOn,
            "silent checked unchecked speak all"),

        // ---- call display ----
        New("Alerts", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.AlertsEnabled),
            c => c.AlertsEnabled != Defaults.AlertsEnabled, c => c.AlertsEnabled = Defaults.AlertsEnabled,
            "master on off"),
        New("Test Mode", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.TestMode),
            c => c.TestMode != Defaults.TestMode, c => c.TestMode = Defaults.TestMode,
            "sample place drag try"),
        New("Text Size", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.CallFontSizePx),
            c => Off(c.CallFontSizePx, Defaults.CallFontSizePx), c => c.CallFontSizePx = Defaults.CallFontSizePx,
            "font big small px"),
        New("Alignment", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.CallTextAlign),
            c => c.CallTextAlign != Defaults.CallTextAlign, c => c.CallTextAlign = Defaults.CallTextAlign,
            "left center right"),
        // "debuff" and "status" are the words for it now that it is the only thing this
        // draws. "glyph" was left over from the crosshair that used to stand in for a
        // head marker, so the one word nobody can type any more was in the index and
        // the two they would type were not.
        New("Icon", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.ShowCallIcon),
            c => c.ShowCallIcon != Defaults.ShowCallIcon, c => c.ShowCallIcon = Defaults.ShowCallIcon,
            "debuff status symbol picture art"),
        New("Icon Size", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.CallIconScale),
            c => Off(c.CallIconScale, Defaults.CallIconScale), c => c.CallIconScale = Defaults.CallIconScale,
            "debuff status bigger smaller"),
        New("Countdown", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.ShowCountdown),
            c => c.ShowCountdown != Defaults.ShowCountdown, c => c.ShowCountdown = Defaults.ShowCountdown,
            "seconds number parentheses timer"),
        New("Info Color", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.ColorInfo),
            c => c.ColorInfo != Defaults.ColorInfo, c => c.ColorInfo = Defaults.ColorInfo,
            "color severity level worth knowing"),
        New("Warning Color", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.ColorAlert),
            c => c.ColorAlert != Defaults.ColorAlert, c => c.ColorAlert = Defaults.ColorAlert,
            "color severity level act now alert"),
        New("Danger Color", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.ColorAlarm),
            c => c.ColorAlarm != Defaults.ColorAlarm, c => c.ColorAlarm = Defaults.ColorAlarm,
            "color severity level deadly alarm"),
        New("Outline", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.TextOutline),
            c => c.TextOutline != Defaults.TextOutline, c => c.TextOutline = Defaults.TextOutline,
            "readable edge ring shadow"),
        New("Pulse on Go", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.PulseWhenClose),
            c => c.PulseWhenClose != Defaults.PulseWhenClose, c => c.PulseWhenClose = Defaults.PulseWhenClose,
            "flash blink near close"),
        New("Background", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.ShowBackground),
            c => c.ShowBackground != Defaults.ShowBackground, c => c.ShowBackground = Defaults.ShowBackground,
            "panel plate box"),
        New("Background color", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.BackgroundColor),
            c => c.BackgroundColor != Defaults.BackgroundColor, c => c.BackgroundColor = Defaults.BackgroundColor,
            "panel plate"),
        New("Lock position", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.OverlayLocked),
            c => c.OverlayLocked != Defaults.OverlayLocked, c => c.OverlayLocked = Defaults.OverlayLocked,
            "drag move pin"),
        New("Position", ConfigWindow.NavKind.CallDisplay, nameof(Configuration.OverlayPosition),
            c => c.OverlayPosition != Defaults.OverlayPosition,
            c => c.OverlayPosition = Defaults.OverlayPosition, "place drag move"),

        // ---- voice ----
        // Drawn on Call Display as well, so a reset there covers it.
        New("TTS", ConfigWindow.NavKind.Tts, nameof(Configuration.VoiceEnabled),
            c => c.VoiceEnabled != Defaults.VoiceEnabled, c => c.VoiceEnabled = Defaults.VoiceEnabled,
            "speak sound audio voice", also: ConfigWindow.NavKind.CallDisplay),
        New("Volume", ConfigWindow.NavKind.Tts, nameof(Configuration.VoiceVolume),
            c => Off(c.VoiceVolume, Defaults.VoiceVolume), c => c.VoiceVolume = Defaults.VoiceVolume,
            "loud quiet audio"),
        New("Speed", ConfigWindow.NavKind.Tts, nameof(Configuration.VoiceSpeed),
            c => Off(c.VoiceSpeed, Defaults.VoiceSpeed), c => c.VoiceSpeed = Defaults.VoiceSpeed,
            "fast slow rate pace"),
        New("Use it", ConfigWindow.NavKind.Tts, nameof(Configuration.UseLocalVoice),
            c => c.UseLocalVoice != Defaults.UseLocalVoice, c => c.UseLocalVoice = Defaults.UseLocalVoice,
            "local neural system voice"),
        New("Voice", ConfigWindow.NavKind.Tts, nameof(Configuration.LocalVoiceName),
            c => c.LocalVoiceName != Defaults.LocalVoiceName,
            c => c.LocalVoiceName = Defaults.LocalVoiceName,
            "female male speaker accent american british pick"),

        // ---- appearance ----
        New("Accent Color", ConfigWindow.NavKind.Appearance, nameof(Configuration.AccentColor),
            c => c.AccentColor != Defaults.AccentColor, c => c.AccentColor = Defaults.AccentColor,
            "theme highlight"),
        New("Window Scale", ConfigWindow.NavKind.Appearance, nameof(Configuration.UiScale),
            c => Off(c.UiScale, Defaults.UiScale), c => c.UiScale = Defaults.UiScale, "size text bigger"),
        New("Colorblind Mode", ConfigWindow.NavKind.Appearance, nameof(Configuration.ColorblindMode),
            c => c.ColorblindMode != Defaults.ColorblindMode, c => c.ColorblindMode = Defaults.ColorblindMode,
            "red green accessibility"),

        // ---- roles ----

        // Looked for by the role's own name as often as by the page's, because the
        // role is what a call says out loud.
        New("Party Roles", ConfigWindow.NavKind.Roles, nameof(Configuration.PartySeats),
            c => c.PartySeats.Count > 0, c => c.ClearPartySeats(),
            "party role mt ot h1 h2 m1 m2 r1 r2 seat assign melee ranged"),
    };

    private static Entry New(string label, ConfigWindow.NavKind nav, string prop,
        Func<Configuration, bool> changed, Action<Configuration> reset, string keywords = "",
        ConfigWindow.NavKind? also = null)
        => new(label, nav, prop, changed, reset, keywords, also);

    private static bool Off(float value, float dflt) => MathF.Abs(value - dflt) > 0.0001f;

    public static string Where(Entry e) => e.Nav switch
    {
        ConfigWindow.NavKind.CallDisplay => "Call Display",
        ConfigWindow.NavKind.Tts => "TTS",
        ConfigWindow.NavKind.Appearance => "Appearance",
        ConfigWindow.NavKind.Plan => "Raid Plan",
        ConfigWindow.NavKind.Roles => "Roles",
        _ => e.Nav.ToString(),
    };

    public static List<Entry> Search(string query)
    {
        var q = query.Trim();
        if (q.Length < 2) return new List<Entry>();
        return Items
            .Where(e => e.Label.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || e.Keywords.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || Where(e).Contains(q, StringComparison.OrdinalIgnoreCase))
            // A name match beats a keyword match, so the obvious hit leads.
            .OrderByDescending(e => e.Label.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(e => e.Label.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ThenBy(e => e.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<Entry> ChangedOn(Configuration c, ConfigWindow.NavKind nav)
        => Items.Where(e => e.On(nav) && e.Changed(c)).ToList();

    public static bool IsChanged(Configuration c, string prop)
        => Items.FirstOrDefault(e => e.Prop == prop)?.Changed(c) ?? false;

    public static void ResetPage(Configuration c, ConfigWindow.NavKind nav)
    {
        foreach (var e in Items.Where(e => e.On(nav))) e.Reset(c);
    }
}
