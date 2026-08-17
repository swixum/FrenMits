using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using FrenAlerts.Engine;
using FrenAlerts.Engine.Alerts;
using FrenAlerts.Engine.UserTriggers;

namespace FrenAlerts.Ui;

// What the tracked cooldowns look like.
//
// Its own window rather than a corner of the call overlay, because the two are read
// at different moments: a call is read the instant it appears, and this is glanced at
// between mechanics. Sharing one window would also mean sharing a position, and the
// one place a call must never be is under something else.
//
// Nothing is worked out here. What is running and how far through it is are the
// board's answers; this draws them.
public sealed class CooldownOverlay : Window
{
    private const float Slot = 44f;
    private const float Gap = 6f;

    private readonly Configuration _config;
    private readonly Game.Cooldowns _cooldowns;
    private readonly Func<double> _clock;

    private bool _applyPos = true;

    private Configuration C => _config;

    public CooldownOverlay(Configuration config, Game.Cooldowns cooldowns, Func<double> clock)
        : base("Fren Alerts Cooldowns##facd")
    {
        _config = config;
        _cooldowns = cooldowns;
        _clock = clock;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;
    }

    // Dragged into place like the call overlay is, and locked the same way.
    public bool Placing { get; set; }

    public void RequestReposition() => _applyPos = true;

    // What this frame has to draw, asked once and used by both the question of whether
    // to be on screen and the drawing itself.
    //
    // Showing() walks the entries and skips the ones that are ready, so an empty answer
    // is an ordinary state rather than an edge: HideWhenReady exists to produce it. The
    // window has no background flag of its own, so it takes ImGui's, and a window with
    // nothing in it is a small empty panel sitting on screen.
    private readonly FrameSnapshot<IReadOnlyList<CooldownEntry>> _snap = new([]);

    private IReadOnlyList<CooldownEntry> ShowingThisFrame() =>
        _snap.Of(ImGui.GetFrameCount(),
            () => _cooldowns.Board.Showing(_cooldowns.Job, _clock()).ToList());

    public override bool DrawConditions()
    {
        if (!C.CooldownsEnabled) return false;
        if (Placing) return true;

        var wanted = _cooldowns.Board.Visibility switch
        {
            CooldownVisibility.Always => true,
            CooldownVisibility.InCombat => Service.Condition[ConditionFlag.InCombat],
            _ => Service.Condition[ConditionFlag.BoundByDuty],
        };

        // Nothing to show is off screen, not an empty box. The call overlay already
        // works this way; this one opened whenever the visibility rule passed.
        return wanted && ShowingThisFrame().Count > 0;
    }

    public override void PreDraw()
    {
        Flags = Placing
            ? Flags & ~(ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs)
            : Flags | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;

        // The same rule the call overlay follows: hold the saved place except while
        // a drag is actually happening, so the window follows the mouse and then
        // snaps back to whatever was written down.
        var pos = SavedScreenPos();
        if (!Placing || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
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

    // Whole pixels, or an icon lands on a half one and blurs.
    private Vector2 SavedScreenPos()
    {
        var vp = ImGui.GetMainViewport();
        var at = vp.WorkPos + C.CooldownPosition * vp.WorkSize;
        return new Vector2(MathF.Round(at.X), MathF.Round(at.Y));
    }

    // Where it was dragged to, written down.
    //
    // It was not, which made "Move it" a switch that moved the tracker until the
    // next reload and then put it back. Kept on screen, or a tracker dragged off an
    // edge is gone with no way to grab it again.
    public override void PostDraw()
    {
        if (!Placing) return;
        if (!ImGui.IsMouseDragging(ImGuiMouseButton.Left) || !ImGui.IsWindowFocused()) return;
        if (OverlayChrome.MovedCenterFrac(C.CooldownPosition) is not { } frac) return;

        C.CooldownPosition = new Vector2(Math.Clamp(frac.X, 0.02f, 0.98f),
                                         Math.Clamp(frac.Y, 0.02f, 0.98f));
        C.Save();
    }

    public override void Draw()
    {
        var now = _clock();
        var dl = ImGui.GetWindowDrawList();
        var scale = Theme.Scale;
        var gap = Gap * scale;
        var room = ImGui.GetMainViewport().WorkSize.X;
        var drawn = 0;
        var row = 0f;

        foreach (var entry in ShowingThisFrame())
        {
            var wide = WidthOf(entry, scale);

            // Nothing is said and no line is broken until the row is actually full, so
            // the usual handful of cooldowns draws exactly as it did.
            if (OverlayState.Wraps(row, wide, gap, room)) row = 0f;
            else if (drawn > 0) { ImGui.SameLine(0f, gap); row += gap; }

            DrawOne(dl, entry, now, scale);
            row += wide;
            drawn++;
        }

        // Something to drag while placing it, since an empty tracker has no size at
        // all and cannot be picked up.
        if (drawn == 0 && Placing) DrawPlaceholder(dl, scale);
    }

    // How big one tracker is, in one place. The row asks before it is drawn and the
    // layout asks as it is drawn, and two answers that drift apart wrap on a width
    // nothing takes up.
    private static float SizeOf(CooldownEntry entry, float scale) =>
        Slot * scale * Math.Clamp(entry.Scale, 0.5f, 3f);

    private static Vector2 BoxOf(CooldownEntry entry, float size) =>
        entry.Style == CooldownStyle.Bar
            ? new Vector2(size * 3f, size * 0.5f)
            : new Vector2(size, size);

    private static float WidthOf(CooldownEntry entry, float scale) =>
        BoxOf(entry, SizeOf(entry, scale)).X;

    private void DrawOne(ImDrawListPtr dl, CooldownEntry entry, double now, float scale)
    {
        var left = _cooldowns.Board.Left(entry.Id, now);
        var done = _cooldowns.Board.Progress(entry.Id, now);
        var box = BoxOf(entry, SizeOf(entry, scale));
        var at = ImGui.GetCursorScreenPos();
        var tint = entry.UseColor
            ? Widgets.ToColor(new Vector4(entry.ColorR, entry.ColorG, entry.ColorB, 1f))
            : 0xFFFFFFFF;

        if (entry.Style == CooldownStyle.Bar) DrawBar(dl, entry, at, box, left, done, tint);
        else DrawIcon(dl, entry, at, box, left, done, tint);

        ImGui.Dummy(box);
    }

    private void DrawIcon(ImDrawListPtr dl, CooldownEntry entry, Vector2 at, Vector2 box,
        float left, float done, uint tint)
    {
        if (entry.IconId == 0 || !Icons.DrawTo(dl, entry.IconId, at, box))
        {
            dl.AddRectFilled(at, at + box, 0xB0202020, 5f * Theme.Scale);
            dl.AddRect(at, at + box, 0x40FFFFFF, 5f * Theme.Scale);
        }

        // The part still to come, darkened over the icon, which is how every timer
        // in this game reads: what is left is the shadow, not the light.
        if (left > 0f)
        {
            var covered = at + new Vector2(box.X, box.Y * (1f - done));
            dl.AddRectFilled(new Vector2(at.X, at.Y), covered, 0x90000000);
            Label(dl, $"{left:0}", at, box, tint);
        }
        else if (entry.ShowName && entry.Name.Length > 0)
        {
            Label(dl, entry.Name, at, box, tint);
        }
    }

    private void DrawBar(ImDrawListPtr dl, CooldownEntry entry, Vector2 at, Vector2 box,
        float left, float done, uint tint)
    {
        var round = 3f * Theme.Scale;

        dl.AddRectFilled(at, at + box, 0xB0202020, round);
        dl.AddRectFilled(at, at + new Vector2(box.X * done, box.Y), tint & 0x60FFFFFF, round);
        dl.AddRect(at, at + box, 0x40FFFFFF, round);

        var words = entry.ShowName && entry.Name.Length > 0
            ? left > 0f ? $"{entry.Name}  {left:0}" : entry.Name
            : left > 0f ? $"{left:0}" : "";

        if (words.Length > 0) Label(dl, words, at, box, tint);
    }

    private static void Label(ImDrawListPtr dl, string text, Vector2 at, Vector2 box, uint tint)
    {
        var size = ImGui.CalcTextSize(text);
        var pos = at + (box - size) * 0.5f;

        // Shadowed, because half of these are drawn over an icon and white on gold
        // is unreadable without it.
        dl.AddText(pos + new Vector2(1f, 1f), 0xC0000000, text);
        dl.AddText(pos, tint, text);
    }

    private void DrawPlaceholder(ImDrawListPtr dl, float scale)
    {
        var at = ImGui.GetCursorScreenPos();
        var box = new Vector2(Slot * scale * 3f, Slot * scale);

        dl.AddRectFilled(at, at + box, 0x50FFFFFF, 5f * scale);
        Label(dl, "Cooldowns go here", at, box, 0xFFFFFFFF);
        ImGui.Dummy(box);
    }
}
