using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FrenAlerts.Engine;
using FrenAlerts.Engine.Alerts;

namespace FrenAlerts.Ui;

// Calls that named their own place on screen.
//
// A hand-written trigger can say where its call goes and how big it is, and somebody
// sets that for a reason: the mechanic it warns about is read while looking at a
// different corner of the screen from where the fight's calls sit.
//
// Its own window, covering the work area and drawing straight to the list. Two
// reasons rather than one. A window per call would fight the window system for
// focus, and these must never join the stack: a placed call that pushed a fight's
// call down the screen would be a setting that quietly moves something else.
public sealed class PlacedCalls : Window
{
    private readonly Configuration _config;
    private readonly FontManager _fonts;
    private readonly AlertBoard _board;

    private Configuration C => _config;

    public PlacedCalls(Configuration config, FontManager fonts, AlertBoard board)
        : base("Fren Alerts Placed##faplaced")
    {
        _config = config;
        _fonts = fonts;
        _board = board;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoSavedSettings;
    }

    // Only drawn when something asked to be placed, so the usual install never has a
    // second window at all.
    //
    // Off with the game's own interface, the same as the stack. That key is the one
    // people press to take a screenshot, and this window ignored it: hiding the UI took
    // the fight's calls off and left a placed one sitting in the picture. A call that
    // was dragged out of the stack is still one of the plugin's calls.
    public override bool DrawConditions() =>
        !UiServices.GameUiHidden && C.AlertsEnabled && Any();

    // One reading of the board a frame, shared by the question of whether to be on
    // screen and by what to put there. Each ask reads the clock and drops whatever has
    // run out since, so asking twice lets a call end between the two answers.
    private readonly FrameSnapshot<IReadOnlyList<AlertBoard.Shown>> _snap = new([]);

    private IReadOnlyList<AlertBoard.Shown> LiveThisFrame() =>
        _snap.Of(ImGui.GetFrameCount(), _board.Live);

    private bool Any()
    {
        foreach (var shown in LiveThisFrame())
            if (shown.Call.Placed) return true;

        return false;
    }

    public override void PreDraw()
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.WorkPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(vp.WorkSize, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    }

    public override void PostDraw() => ImGui.PopStyleVar();

    public override void Draw()
    {
        var now = _board.Now;
        var vp = ImGui.GetMainViewport();
        var dl = ImGui.GetWindowDrawList();

        foreach (var shown in LiveThisFrame())
        {
            if (!shown.Call.Placed) continue;
            DrawOne(dl, vp.WorkPos, vp.WorkSize, shown, now);
        }
    }

    private void DrawOne(ImDrawListPtr dl, Vector2 origin, Vector2 work,
        AlertBoard.Shown shown, double now)
    {
        var call = shown.Call;
        var at = call.At ?? new Vector2(0.5f, 0.32f);

        // Clamped, because a trigger somebody imported can carry a place from a
        // screen that is not this one, and a call drawn off the edge is a call that
        // never arrived.
        var centre = origin + new Vector2(
            Math.Clamp(at.X, 0f, 1f) * work.X,
            Math.Clamp(at.Y, 0f, 1f) * work.Y);

        var counting = shown.Counting(now);
        var (line, _) = OverlayState.Countdown(
            CallText.Sentence(call.Text), C.ShowCountdown, counting, shown.Remaining(now));

        // Arrives and leaves the way a stacked call does. It used to appear and vanish
        // outright, which beside a fight's calls fading reads as the overlay having
        // glitched rather than as a call ending.
        var age = (float)(now - shown.At);
        var alpha = CallLook.AlphaAt(age, (float)(shown.EndsAt - now));
        if (!CallLook.WorthDrawing(alpha)) return;

        var px = MathF.Max(10f, C.CallFontSizePx * Math.Clamp(call.Scale, 0.25f, 4f))
                 * CallLook.ScaleAt(age);
        var colour = call.Tint != 0 ? call.Tint : ColorFor(call.Level);
        if (C.PulseWhenClose && counting && shown.Remaining(now) < 1.5f)
            colour = OverlayChrome.Pulse(colour);

        using (OverlayChrome.PushFont(_fonts, px))
        {
            var font = ImGui.GetFont();
            var drawn = ImGui.GetFontSize();

            // Split into its coloured runs and measured without the tags. Drawn whole,
            // a call that colours a word showed the <red> markup on screen and centred
            // itself around characters nobody could see.
            var pieces = CallText.Pieces(line);
            var size = ImGui.CalcTextSize(CallText.Plain(line));

            // The stack's geometry, asked the same way, because a call that moved to its
            // own spot is the same call. These drew no icon at all: a trigger somebody
            // wrote could tick "Show an icon", pick the art, drag the call out of the
            // stack, and the picture stayed behind with the stack it had left.
            var withIcon = C.ShowCallIcon && shown.Icon.Any && Icons.Has(shown.Icon);
            var iconPx = withIcon
                ? drawn * CallLook.IconSize * Math.Clamp(C.CallIconScale, 0.4f, 1.6f)
                : 0f;
            var lead = withIcon ? iconPx + drawn * CallLook.IconGap : 0f;

            // Centred on the whole of it, icon included, or a call grows sideways out of
            // the spot it was dragged to the moment its debuff lands.
            var pen = centre - new Vector2(lead + size.X, size.Y) * 0.5f;

            if (withIcon)
                Icons.Draw(shown.Icon, dl, new Vector2(pen.X, pen.Y + (size.Y - iconPx) * 0.5f),
                    iconPx, OverlayChrome.Faded(0xFFFFFFFF, alpha), false);

            // The same ring the stack draws, off the same switch. This read the old
            // shadow setting, which the Call Display page retired: nothing sets it any
            // more and the config migration switches it off, so a placed call quietly
            // lost its edge with nothing left to bring it back.
            OverlayChrome.DrawPieces(dl, font, drawn, new Vector2(pen.X + lead, pen.Y),
                pieces, colour, alpha, C.TextOutline);
        }
    }

    // The stack's, so a placed call is the same colour as the same level in the stack.
    private uint ColorFor(CallLevel level) => OverlayChrome.CallColor(C, level);
}
