using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// Two small readouts sharing one line of screen, both duty-only:
//
//   the food check   - pre-pull, and persistent while the problem lasts
//   the potion note  - mid-fight, once, when the pot you used comes back
//
// Silent the rest of the time, and never outside a duty.
public class PrepWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;
    private bool _applyPos = true;

    // Resolved once per frame in DrawConditions, which runs whether or not the
    // window draws.
    private bool _prePull;   // food rows are worth showing
    private bool _potNote;   // the potion note is inside its few seconds

    private readonly PrepCheck.PotionTimer _potTimer = new();
    private readonly PrepCheck.Announcer _foodSay = new();
    private readonly PrepCheck.Announcer _potSay = new();
    private uint _territory = uint.MaxValue;

    private bool EffectiveLocked => OverlayChrome.Locked(C.PrepCheckLocked, C);

    public PrepWindow(Plugin plugin) : base("FrenMits Prep##prep")
    {
        _plugin = plugin;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
    }

    public void RequestReposition() => _applyPos = true;

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground;

        if (EffectiveLocked)
            Flags |= ImGuiWindowFlags.NoResize
                     | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoMouseInputs;

        OverlayChrome.ApplyPosition(C.PrepCheckPosition, EffectiveLocked, ref _applyPos);
    }

    public override bool DrawConditions()
    {
        // This runs every frame whether or not the window draws, which is what
        // makes it the only safe home for the potion clock: Medicated is up for
        // 30 seconds, and a frame spent not looking is a use missed.
        var on = C.PrepCheckEnabled
                 && !Plugin.CutsceneActive
                 // No player yet (zoning in) reads exactly like "no food is up",
                 // so stay quiet until there's someone to read.
                 && Plugin.LocalPlayer != null;

        // Leaving the duty forgets the potion clock. Nothing else does: it has to
        // survive combat starting and ending, or it would never reach the full recast.
        var territory = Service.ClientState.TerritoryType;
        if (territory != _territory)
        {
            _territory = territory;
            _potTimer.Reset();
            _potSay.Reset();
        }

        // The potion clock runs for as long as you're in the duty, combat included.
        // The pot's OWN recast is handed to the timer, read off the tincture that's
        // up, so a future item with a different number just works.
        _potNote = on && Plugin.InDuty && C.PrepCheckPotion && PotionTick();
        // Once the note's few seconds are up, re-arm the speech so a SECOND pot
        // later in the same fight is announced too.
        if (!_potNote) _potSay.Reset();

        // Food is a pre-pull matter only.
        _prePull = PrepCheck.ShouldShow(on, Plugin.InDuty, Plugin.InCombat);
        if (!_prePull) _foodSay.Reset();

        if (!C.PrepCheckEnabled) return false;
        // Test mode draws a placement sample; it is the ONLY thing that ever puts
        // this on screen outside a duty.
        if (C.TestMode) return true;
        return _prePull || _potNote;
    }

    public override void Draw()
    {
        SavePositionIfDragged();
        using var _ = PushFont(C.PrepCheckFontSizePx);

        var drew = false;

        if (_prePull)
        {
            var warn = PrepCheck.WarnSeconds(C.PrepCheckWarnMinutes);
            var food = PrepCheck.Read(PrepCheck.WellFedStatus);
            var grade = PrepCheck.GradeOf(food, warn);
            var text = PrepCheck.FoodLine(food, warn);
            if (text.Length > 0)
            {
                Row(PrepCheck.FoodIcon(food), text,
                    grade == PrepCheck.Grade.Missing ? Theme.Danger : Theme.Warn);
                drew = true;
            }
            Announce(_foodSay, PrepCheck.SpeechFor(grade));
        }

        if (_potNote)
        {
            Row(PrepCheck.StatusIcon(PrepCheck.MedicatedStatus), PrepCheck.PotionText, Theme.Good);
            Announce(_potSay, PrepCheck.PotionSpeech);
            drew = true;
        }

        // Test mode always leaves something on screen to drag, whatever your real
        // food and pot happen to be doing - otherwise the one moment you'd want to
        // place this (stood in a duty, well fed) is the moment it draws nothing.
        if (!drew && C.TestMode)
        {
            Row(PrepCheck.StatusIcon(PrepCheck.WellFedStatus), "Food 3:41", Theme.Warn);
            if (C.PrepCheckPotion)
                Row(PrepCheck.StatusIcon(PrepCheck.MedicatedStatus), PrepCheck.PotionText, Theme.Good);
            drew = true;
        }

        // Keep the window alive between warnings so it doesn't collapse to a dot
        // and jump around when one appears.
        if (!drew) ImGui.Dummy(new Vector2(1f, 1f));
    }

    private bool PotionTick()
    {
        var medicated = PrepCheck.Read(PrepCheck.MedicatedStatus);
        return _potTimer.Update(medicated.Present, PrepCheck.RecastFor(medicated), ImGui.GetTime());
    }

    // Speak a phrase the first frame it becomes true.
    //
    // The announcer is advanced whether or not speech is switched on, so that
    // turning it on mid-duty doesn't immediately announce a state that had
    // already been true and on screen for a minute.
    //
    // Deliberately independent of the combat-cue audio switch: wanting these
    // shouldn't mean turning on in-fight callouts as well.
    private void Announce(PrepCheck.Announcer announcer, string phrase)
    {
        var say = announcer.Next(phrase);
        if (say == null || !C.PrepCheckTts) return;
        var voice = C.TtsUseEdge
            ? (string.IsNullOrWhiteSpace(C.TtsCustomVoice) ? C.TtsEdgeVoice : C.TtsCustomVoice)
            : C.TtsVoice;
        _plugin.Audio.Speak(say, C.TtsRate, C.TtsVolume, C.TtsUseEdge, voice);
    }

    private static void Row(uint iconId, string text, uint color)
    {
        var h = ImGui.GetTextLineHeight();
        if (iconId != 0)
        {
            Icons.Draw(iconId, new Vector2(h, h));
            ImGui.SameLine(0, 6f);
        }
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private void SavePositionIfDragged()
    {
        if (EffectiveLocked) return;
        if (OverlayChrome.MovedCenterFrac(C.PrepCheckPosition) is { } frac) { C.PrepCheckPosition = frac; _posDirty = true; }
        // ONE disk write when the drag ends, not a full config save per frame.
        if (_posDirty && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { C.Save(); _posDirty = false; }
    }

    private bool _posDirty;

    private IDisposable PushFont(float sizePx)
        => OverlayChrome.PushFont(_plugin.Fonts, sizePx, C.OverlayFontFamily, C.OverlayFontBold, C.OverlayFontItalic);
}
