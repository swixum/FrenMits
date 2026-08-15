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
    private bool Locked => C.OverlayLocked;

    public override bool DrawConditions()
        => C.BossAlertsEnabled && C.BossAlertsDraw
           && (_plugin.Callouts.Live.Count > 0 || !Locked);

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

        var viewport = ImGui.GetMainViewport();
        var pos = viewport.WorkPos + C.AlertOverlayPosition * viewport.WorkSize;
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));  // whole pixels, sharp text

        // Pinned, except while it is being dragged.
        if (Locked || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            _applyPos = true;
        }
        else if (_applyPos)
        {
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            _applyPos = false;
        }
    }

    public override void Draw()
    {
        var live = _plugin.Callouts.Live;
        if (live.Count == 0)
        {
            // Unlocked and quiet: something has to be here to grab and drag.
            ImGui.TextColored(Theme.V(Theme.Muted), "Boss alerts appear here. Drag to move.");
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

        using var font = _plugin.Fonts.Get(
            ImGui.GetFontSize() * (alert.Personal ? 1.9f : 1.6f), "Default", true, false);
        using var pushed = font is { Available: true } ? font.Push() : null;

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
