using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// The food check before the pull and the potion note during it, on one line.
public class PrepWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;
    private bool _applyPos = true;

    // Resolved once per frame in DrawConditions, which runs whether or not the
    // window draws.
    private bool _prePull;     // food rows are worth showing
    private bool _readyCheck;  // a ready check is up: answer it, one way or the other
    private bool _potNote;     // the potion note is inside its few seconds
    private float _potLeft;    // seconds until the pot is back (0 = don't show)

    // The last food we saw you eating, so "No food" can still say how many are
    // in your bag - with none up there's no status to read the item from.
    private uint _lastFoodItem;
    private bool _lastFoodHq;
    private uint _lastPotItem;
    private bool _lastPotHq;

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
        // This runs every frame whether or not the window draws, which the potion clock
        // needs.
        var on = C.PrepCheckEnabled
                 && !Plugin.CutsceneActive
                 // No player yet (zoning in) reads exactly like "no food is up",
                 // so stay quiet until there's someone to read.
                 && Plugin.LocalPlayer != null
                 // Optional: only where you have a real sheet, so a leveling
                 // roulette (where nobody brings food) stays quiet.
                 && (!C.PrepCheckSheetsOnly || _plugin.ActiveFight() is { TimelineOnly: false });

        // Leaving the duty forgets the potion clock.
        var territory = Service.ClientState.TerritoryType;
        if (territory != _territory)
        {
            _territory = territory;
            _potTimer.Reset();
            _potSay.Reset();
        }

        // The potion clock runs for as long as you're in the duty, combat
        // included.
        var potLive = on && Plugin.InDuty && C.PrepCheckPotion;
        _potNote = potLive && PotionTick();
        // Once the note's few seconds are up, re-arm the speech so a SECOND pot
        // later in the same fight is announced too.
        if (!_potNote) _potSay.Reset();

        // Optional running countdown to the pot coming back, which is a readout
        // rather than an alert and so never speaks.
        _potLeft = potLive && C.PrepCheckPotCountdown && !_potNote
            ? _potTimer.Remaining(ImGui.GetTime()) : 0f;

        // Food is a pre-pull matter only - unless a ready check is up, which is
        // pre-pull by definition wherever it happens.
        var readyCheck = on && C.PrepCheckOnReadyCheck && PrepCheck.ReadyCheckActive();
        // A ready check re-arms the speech, so "no food" is said again when it's asked.
        if (readyCheck && !_readyCheck) _foodSay.Reset();
        _readyCheck = readyCheck;

        _prePull = PrepCheck.ShouldShow(on, Plugin.InDuty, Plugin.InCombat, readyCheck);
        if (!_prePull) _foodSay.Reset();

        if (!C.PrepCheckEnabled) return false;
        // Test mode draws a placement sample.
        if (C.TestMode) return true;
        return _prePull || _potNote || _potLeft > 0f;
    }

    public override void Draw()
    {
        SavePositionIfDragged();
        using var _ = PushFont(C.PrepCheckFontSizePx);

        var drew = false;

        if (_prePull)
        {
            var food = PrepCheck.Read(PrepCheck.WellFedStatus);
            if (food.Present) { _lastFoodItem = PrepCheck.ItemOf(food); _lastFoodHq = PrepCheck.IsHq(food); }

            // Each optional check is only RESOLVED when it's switched on, so an
            // extra nobody uses costs nothing per frame.
            var warn = PrepCheck.WarnSecondsFor(C.PrepCheckUseFightLength, C.PrepCheckWarnMinutes,
                C.PrepCheckUseFightLength ? PrepCheck.FightSeconds(_plugin.ActiveFight()) : 0f);
            var verdict = PrepCheck.FoodVerdict(food,
                !C.PrepCheckWarnWrongFood || PrepCheck.IsBattleFood(food),
                !C.PrepCheckWarnNq || PrepCheck.IsHq(food),
                // A ready check gets an answer either way, so healthy food shows a
                // muted timer.
                new PrepCheck.FoodOpts(warn, C.PrepCheckWarnWrongFood, C.PrepCheckWarnNq,
                    C.PrepCheckAlwaysShowFood || _readyCheck));

            if (verdict.Any)
            {
                Row(PrepCheck.FoodIcon(food), verdict.Text + FoodCount(food), LevelColor(verdict.Level));
                drew = true;
            }
            // Speech follows the ORIGINAL grade, so the optional extras stay
            // visual and nothing new starts talking without being asked.
            Announce(_foodSay, PrepCheck.SpeechFor(PrepCheck.GradeOf(food, warn)));
        }

        if (_potNote)
        {
            Row(PrepCheck.StatusIcon(PrepCheck.MedicatedStatus),
                PrepCheck.PotionText + PotCount(), Theme.Good);
            Announce(_potSay, PrepCheck.PotionSpeech);
            drew = true;
        }
        else if (_potLeft > 0f)
        {
            // A readout, not an alert: muted, and never spoken.
            Row(PrepCheck.StatusIcon(PrepCheck.MedicatedStatus),
                $"Pot {PrepCheck.Clock(_potLeft)}", Theme.Muted);
            drew = true;
        }

        // Test mode always leaves something on screen to drag.
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

    private static uint LevelColor(PrepCheck.Level level) => level switch
    {
        PrepCheck.Level.Danger => Theme.Danger,
        PrepCheck.Level.Warn => Theme.Warn,
        _ => Theme.Muted,
    };

    // "(12 left)" for the dish in question: the one you're eating, or - when
    // there's none up to read - the last one we saw you eat.
    private string FoodCount(PrepCheck.Buff food)
    {
        if (!C.PrepCheckShowCounts) return "";
        var (item, hq) = food.Present
            ? (PrepCheck.ItemOf(food), PrepCheck.IsHq(food))
            : (_lastFoodItem, _lastFoodHq);
        return PrepCheck.Count(PrepCheck.BagCount(item, hq));
    }

    private string PotCount()
        => C.PrepCheckShowCounts
            ? PrepCheck.Count(PrepCheck.BagCount(_lastPotItem, _lastPotHq))
            : "";

    private bool PotionTick()
    {
        var medicated = PrepCheck.Read(PrepCheck.MedicatedStatus);
        // Remember the tincture while it's up: it is long gone by the time the note
        // fires.
        if (medicated.Present)
        {
            _lastPotItem = PrepCheck.ItemOf(medicated);
            _lastPotHq = PrepCheck.IsHq(medicated);
        }
        return _potTimer.Update(medicated.Present, PrepCheck.RecastFor(medicated),
            ImGui.GetTime(), Plugin.InCombat);
    }

    // Speak a phrase the first frame it becomes true.
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
        if (_posDirty && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { C.SaveSettings(); _posDirty = false; }
    }

    private bool _posDirty;

    private IDisposable PushFont(float sizePx)
        => OverlayChrome.PushFont(_plugin.Fonts, sizePx, C.OverlayFontFamily, C.OverlayFontBold, C.OverlayFontItalic);
}
