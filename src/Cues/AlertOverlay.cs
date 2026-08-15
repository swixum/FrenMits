using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenMits.Cues;

// The boss alert on screen: what to do, how long until it lands, and the game's
// own art for whatever it is about.
//
// It sits above the middle of the screen by default, where the eye already is
// during a mechanic. Nothing here decides anything: the runner raised the call
// and worked out when it resolves, and this draws that.
public sealed class AlertOverlay : Window
{
    // Inside this, the countdown turns red: it is the last of the warning.
    public const float Urgent = 3f;

    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    private bool _applyPos = true;

    public AlertOverlay(Plugin plugin) : base("FrenMits Alerts##bossalerts")
    {
        _plugin = plugin;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
    }

    // Unlocked means the player is placing it, so it has to be on screen to be
    // grabbed at all. Locked, it only exists while it has something to say.
    // Placing it beats the lock: otherwise the one moment you need to drag it
    // is the one moment it refuses to be dragged.
    private bool Locked => OverlayChrome.Locked(C.OverlayLocked, C)
                           && !_plugin.Callouts.Placing(ImGui.GetFrameCount());

    private bool _saidWhy;

    public override bool DrawConditions()
    {
        var live = _plugin.Callouts.Live.Count > 0;
        var ok = (C.BossAlertsEnabled && C.BossAlertsDraw && live)
                 || _plugin.Callouts.Testing
                 || _plugin.Callouts.Placing(ImGui.GetFrameCount())
                 || !Locked;

        // Said once per refusal, so a banner that never appears says why rather
        // than leaving nothing to go on.
        if (!ok && _plugin.Callouts.Live.Count > 0 && !_saidWhy)
        {
            _saidWhy = true;
            Service.Log.Information(
                $"[FrenMits] alert overlay held back: on={C.BossAlertsEnabled} "
                + $"draw={C.BossAlertsDraw} locked={Locked}");
        }
        if (ok) _saidWhy = false;
        return ok;
    }

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.AlwaysAutoResize;

        if (Locked)
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
                     | ImGuiWindowFlags.NoMouseInputs;

        // The same placement every other overlay uses. Locked, it is pinned
        // every frame; unlocked, it is placed once and then left alone so a
        // drag survives the frame it happens on.
        OverlayChrome.ApplyPosition(C.AlertOverlayPosition, Locked, ref _applyPos);
    }

    public override void PostDraw() => SavePositionIfDragged();

    private bool _posDirty;

    private void SavePositionIfDragged()
    {
        if (Locked) return;
        if (OverlayChrome.MovedCenterFrac(C.AlertOverlayPosition) is { } frac)
        { C.AlertOverlayPosition = frac; _posDirty = true; }

        // One disk write when the drag ends, not one per frame.
        if (_posDirty && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        { C.SaveSettings(); _posDirty = false; }
    }

    public override void Draw()
    {
        var live = _plugin.Callouts.Live;
        if (live.Count == 0)
        {
            // Nothing is happening, so a sample stands in. It is drawn the way a
            // real one is, at the size and colors set, so what is being placed
            // is what will be seen.
            Row(new LiveAlert("Knockback",
                _plugin.Callouts.SampleIcon(Service.ClientState.TerritoryType),
                FrenMits.Callouts.CallSeverity.Danger,
                Lands: 2.4f, Until: 0f, Personal: true), now: 0f);
            ImGui.TextColored(Theme.V(Theme.Muted), "Sample. Drag to move.");
            return;
        }

        var now = _plugin.Callouts.Clock;
        for (var i = 0; i < live.Count; i++) Row(live[i], now);
    }

    private void Row(LiveAlert alert, float now)
    {
        var left = MathF.Max(0f, alert.Lands - now);
        var color = Theme.V(Color(alert.Level));
        var dl = ImGui.GetWindowDrawList();

        // Yours reads a little larger, so it stands out of a stack of four.
        // Through the shared helper, which hands back what Push returned rather
        // than the handle. Disposing the handle itself kills it for good, and
        // every later frame throws on it.
        using var font = OverlayChrome.PushFont(_plugin.Fonts,
            C.AlertFontSizePx * (alert.Personal ? 1.18f : 1f), "Default", bold: true, italic: false);

        var art = ImGui.GetTextLineHeight();
        ImGui.BeginGroup();

        // The art leads, so a debuff is recognized before the words are read.
        if (alert.Icon != 0)
        {
            var at = ImGui.GetCursorScreenPos();
            Icons.DrawTo(dl, alert.Icon, at, new Vector2(art, art));
            ImGui.Dummy(new Vector2(art, art));
            ImGui.SameLine(0, art * 0.35f);
        }

        // A call aimed at this player says so before it says anything else.
        if (alert.Personal)
        {
            ImGui.TextColored(Theme.V(Theme.Gold), "YOU");
            ImGui.SameLine(0, art * 0.35f);
        }

        ImGui.TextColored(color, alert.Text);

        // The countdown only earns its place while there is something to count.
        if (left > 0.05f)
        {
            ImGui.SameLine(0, art * 0.45f);
            ImGui.TextColored(
                Theme.V(left <= Urgent ? Theme.Danger : Theme.Muted),
                left >= 10f ? $"{left:0}s" : $"{left:0.0}s");
        }

        ImGui.EndGroup();
    }

    private static uint Color(FrenMits.Callouts.CallSeverity level) => level switch
    {
        FrenMits.Callouts.CallSeverity.Danger => Theme.Danger,
        FrenMits.Callouts.CallSeverity.Warn => Theme.Warn,
        _ => Theme.TextBright,
    };
}
