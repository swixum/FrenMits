using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using FrenAlerts.Engine;
using FrenAlerts.Engine.Alerts;

namespace FrenAlerts.Ui;

public partial class ConfigWindow : Window, IDisposable
{
    private readonly Configuration _config;
    private readonly FontManager _fonts;
    private readonly AlertBoard _board;
    private readonly AlertOverlay _overlay;

    private Configuration C => _config;

    public ConfigWindow(Configuration config, FontManager fonts, AlertBoard board, AlertOverlay overlay)
        : base("Fren Alerts###faconfig")
    {
        _config = config;
        _fonts = fonts;
        _board = board;
        _overlay = overlay;
        // Opens in proportion to the text it will hold: at 1.6x a fixed 740 wide
        // window puts the sidebar and the page in a fight over the same pixels.
        Size = new Vector2(740, 620) * Math.Clamp(config.UiScale, 0.8f, 1.6f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    // Left-sidebar navigation.
    internal enum NavKind { Home, Fights, Fight, Plan, Roles, CallDisplay, Mine, Tts, Appearance, Parser }

    private NavKind _nav = NavKind.Home;
    private string _navCategory = FightCatalog.DefaultCategory;

    // Off the sidebar for now, cooldown tracker settings and all; both still run.
    private static readonly bool ShowMine = false;

    // The fight whose calls are open, held as its territory rather than the entry
    // itself: the list is rebuilt when the pack lands, and a page redrawing from
    // a copy taken before that would show counts that no longer exist.
    private uint _navFightId;
    private string _openCall = "";
    private string _callFilter = "";

    // Which calls the fight page is showing: all, the checked ones, or the ones
    // the import could not check.

    private string _callWords = "";

    public Game.Runner? Runner { get; set; }

    // The tracker's own window, so the page can hand it into placing mode.
    public CooldownOverlay? Cooldowns { get; set; }

    // Standing in a fight that has been turned off by hand: the one case where an
    // untouched setup goes quiet on purpose, so it is said out loud.
    private bool MutedHere =>
        Runner is { TriggerCount: > 0 } && C.IsMuted(Service.ClientState.TerritoryType);

    // Window theming has to be applied before the window begins.
    public override void PreDraw()
    {
        Theme.PushWindow();
        // Only the two this window wants tighter than the theme's defaults.
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6f);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(2);
        Theme.PopWindow();
    }

    public override void Draw()
    {
        Theme.Accent = C.AccentColor;
        Theme.Colorblind = C.ColorblindMode;
        Theme.Scale = Math.Clamp(C.UiScale, 0.8f, 1.6f);
        Theme.PushWidgets();
        using var uiFont = Widgets.PushUiFont(_fonts, Theme.Scale);
        // Fatter scrollbars (easier to grab) + softer rounded controls.
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 18f * Theme.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 9f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 4f);

        DrawStatusHeader();
        ImGui.Separator();

        // Content sits above a pinned footer, beside the sidebar.
        var footerH = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y + 4f;
        if (ImGui.BeginChild("##content", new Vector2(0, -footerH), false))
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.PanelBg);
            if (ImGui.BeginChild("##sidebar", new Vector2(_sidebarW, 0), true))
                DrawSidebar();
            ImGui.EndChild();
            ImGui.PopStyleColor();

            ImGui.SameLine();
            if (ImGui.BeginChild("##page", new Vector2(0, 0), false))
            {
                ImGui.Spacing();
                ImGui.Indent(Theme.S(4f));
                if (Searching) DrawSearchResults();
                else DrawSelectedPage();
                ImGui.Unindent(Theme.S(4f));
            }
            ImGui.EndChild();
        }
        ImGui.EndChild();

        DrawFooter();
        ImGui.PopStyleVar(4);
        Theme.PopWidgets();

        Widgets.RollLabelCols();
    }

    // Every edit is kept the moment it happens; the write follows a breath later.
    private void DrawFooter()
    {
        ImGui.Separator();

        if (Configuration.SuppressSave)
        {
            StatusDot(Theme.V(Theme.Warn));
            ImGui.SameLine(0, Theme.S(6f));
            ImGui.TextColored(Theme.V(Theme.Warn),
                "Your config file would not read, so nothing is saved this session.");
            return;
        }

        // A drag holds its write until it stops, so say so rather than look stale.
        if (C.SavePending)
        {
            StatusDot(Theme.V(Theme.Warn));
            ImGui.SameLine(0, Theme.S(6f));
            ImGui.TextDisabled("Saving...");
            return;
        }

        var last = Configuration.LastSavedAt;
        var recent = last != DateTime.MinValue && (DateTime.Now - last).TotalSeconds < 3;
        StatusDot(Theme.V(recent ? Theme.GoodBright : Theme.Good));
        ImGui.SameLine(0, Theme.S(6f));
        ImGui.TextDisabled(last == DateTime.MinValue
            ? "Everything is saved."
            : recent
                ? "Saved just now."
                : $"Everything is saved, last {Ago(last)}.");
    }

    private static string Ago(DateTime t)
    {
        var s = (DateTime.Now - t).TotalSeconds;
        return s < 90 ? $"{(int)s}s ago" : s < 5400 ? $"{(int)(s / 60)}m ago" : $"{(int)(s / 3600)}h ago";
    }

    // How long a pull runs before an unanchored timeline is called a fault rather
    // than a clock still looking. Long enough to cover a phase-block fight's opening,
    // short enough that a whole pull without countdowns is not the first anybody
    // hears of it.
    private const double TimelineGrace = 45d;

    // Tooltip with a hover delay, so sweeping a page stays quiet.
    private static void Tip(string text) => Widgets.Tooltip(text);

    // One reading on the status line: the dot's colour, the words, what it says on
    // hover, and whether the words are loud.
    private readonly record struct Pip(uint Dot, string Label, string Tip = "", bool Loud = false);

    // Gathered before any of them is drawn, because the row has to be measured to be
    // wrapped and sized. Cleared and refilled each frame; sixteen at the very most, and
    // that is a count of the branches below rather than a bound anything enforces.
    private readonly List<Pip> _pips = [];

    private void Pip1(bool on, string label, string tip = "") =>
        _pips.Add(new Pip(on ? Theme.Good : Theme.Muted, label, tip));

    private void PipWarn(string label, string tip = "") =>
        _pips.Add(new Pip(Theme.Warn, label, tip, Loud: true));

    private static float PipWidth(string label) =>
        ImGui.GetTextLineHeight() + Theme.S(4f) + ImGui.CalcTextSize(label).X;

    // How many lines this many readings need at this width.
    //
    // They were laid on one line with a SameLine each and no end to it, inside a child
    // that is a fixed two lines tall and does not scroll. So the row ran off the right
    // edge and was clipped, and what fell off was whatever was drawn last: "No hit
    // calls", "This fight is off", "Feed dropped 40". The three that say a whole kind of
    // call cannot fire, and they were behind "Next: Longitudinal Implosion/Latitudinal
    // Implosion (castbar)", which is a real Dancing Mad line and 54 characters of it.
    private int PipRows(float room)
    {
        var rows = 1;
        var x = 0f;

        foreach (var pip in _pips)
        {
            var w = PipWidth(pip.Label);
            if (x > 0f && x + Theme.S(18f) + w > room) { rows++; x = w; }
            else x += (x > 0f ? Theme.S(18f) : 0f) + w;
        }

        return rows;
    }

    private void DrawPips(float room)
    {
        var x = 0f;

        foreach (var pip in _pips)
        {
            // No single reading is ever wider than the row it is on, whatever a fight
            // calls its mechanics.
            var label = Widgets.Elide(pip.Label, room - ImGui.GetTextLineHeight() - Theme.S(4f));
            var w = PipWidth(label);

            if (x > 0f && x + Theme.S(18f) + w > room) x = 0f;
            else if (x > 0f) { ImGui.SameLine(0, Theme.S(18f)); x += Theme.S(18f); }

            StatusDot(Theme.V(pip.Dot));
            ImGui.SameLine(0, Theme.S(4f));
            if (pip.Loud) ImGui.TextColored(Theme.V(Theme.Warn), label);
            else ImGui.TextUnformatted(label);

            if (pip.Tip.Length > 0 && Widgets.HoveredDelayed()) ImGui.SetTooltip(pip.Tip);
            x += w;
        }
    }

    private static void StatusDot(Vector4 color, bool frameAligned = false)
    {
        var size = ImGui.GetTextLineHeight();
        var h = frameAligned ? ImGui.GetFrameHeight() : size;
        var pos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddCircleFilled(
            new Vector2(pos.X + size * 0.5f, pos.Y + (frameAligned ? h * 0.5f : size * 0.55f)),
            size * 0.22f, ImGui.ColorConvertFloat4ToU32(color));
        ImGui.Dummy(new Vector2(size, h));
    }

    // Everything the status line can say, in the order it says it.
    //
    // Order matters more than it did: what does not fit now wraps rather than falling off
    // the edge, but the first row is still the one read at a glance, so the two that are
    // always there lead and the faults follow the readings that explain them.
    private void GatherPips()
    {
        _pips.Clear();

        Pip1(C.AlertsEnabled, C.AlertsEnabled ? "Calls: on" : "Calls: off");

        var live = _board.Live().Count;
        Pip1(live > 0, live > 0 ? $"On screen: {live}" : "On screen: nothing");

        // In a pull or not, which the zone alone cannot say: an ultimate is thirty wipes
        // in one instance.
        if (Runner is { } run && (run.InPull || run.Pulls > 0))
            Pip1(run.InPull, run.InPull
                ? $"Pull {run.Pulls}"
                : $"{run.Pulls} pull{(run.Pulls == 1 ? "" : "s")}");

        // Only for a fight that names what moves it on. Everywhere else the number would
        // sit at 1 for the whole pull and read as a stuck phase.
        if (Runner is { InPull: true, PhasesKnown: true } phased)
            Pip1(true, $"Phase {phased.Phase}");

        // Where the fight thinks it is, and what it expects next. Only once the clock has
        // been anchored: a countdown against a clock nobody has placed would be a
        // confident number pointing at the wrong mechanic.
        if (Runner is { InPull: true, TimelineRunning: true } tl)
        {
            var next = tl.Upcoming(1).FirstOrDefault();
            Pip1(true, next.Mechanic is { Length: > 0 } m
                    ? $"Next: {m} in {Math.Max(0d, next.In):0}"
                    : "Timeline running",
                $"{tl.TimelineAt:0}s into this fight's timeline, "
                + $"{tl.TimelineResyncs} resync{(tl.TimelineResyncs == 1 ? "" : "s")}.\n"
                + $"Running {(tl.TimelineDrift >= 0 ? "ahead of" : "behind")} the fight "
                + $"by {Math.Abs(tl.TimelineDrift):0.0}s on average.");
        }
        // A timeline that has not placed itself yet. Muted while it is still reasonable,
        // amber once it is not: the countdowns are simply absent either way, and silently
        // absent is how that would otherwise be found.
        else if (Runner is { InPull: true, HasTimeline: true } waiting)
        {
            var late = waiting.PullSeconds > TimelineGrace;
            var why = late
                ? "Nothing has matched an anchor cast this pull, so there are no\n"
                  + "countdowns. The calls that do not need one still fire."
                : "Waiting for a cast it recognises. A fight written in phase\n"
                  + "blocks has nothing to count from until its first anchor.";

            if (late) PipWarn("Timeline not anchored", why);
            else Pip1(false, "Timeline waiting", why);
        }

        // Not a fault, but it explains why the calls are keeping their own time.
        //
        // Whole seconds as mm:ss rather than a decimal. A tenth redrawn every frame
        // changes the label's width sixty times a second, and every dot after it on the
        // row shuffles sideways to match.
        if (Runner is { InReplay: true } replaying)
        {
            var at = TimeSpan.FromSeconds(Math.Max(0, replaying.ClockSeconds));
            Pip1(true, $"Replay {at:mm\\:ss}",
                "Watching a recording. Calls run on the recording's own clock, so\n"
                + "they stop when you pause and keep pace when you fast forward.\n\n"
                + $"Fight clock {at:mm\\:ss}, running at {replaying.ReplaySpeed:0.##}x.\n"
                + "The clock should climb while the speed is above zero. It sitting\n"
                + "still at a speed above zero is a fault worth reporting.");
        }

        // Not a second "Calls are off": the reading above already says so, and saying it
        // twice on one row cost the width of a fault that had nowhere else to appear.
        if (C.TestMode) PipWarn("Test mode is on");

        // A recorder writing to disk is never invisible. It is also the one that carries
        // a number, because a recording that stopped at its bound and a recording that is
        // still going look identical otherwise.
        if (Runner is { Diary.On: true } rec)
            PipWarn(rec.Diary.Full ? "Recording full" : $"Recording {rec.Diary.Lines}",
                rec.Diary.Full
                    ? $"This pull hit {Engine.Diary.MaxLines} lines and stopped writing.\n"
                      + "What is already down is kept. The next pull starts fresh."
                    : "Writing what every call did to pulls.log. Each pull is written\n"
                      + "as it ends, so it is safe to leave on. Turn it off on Home.");

        if (Runner is { ControlAvailable: false })
            PipWarn("No direction calls",
                "This patch moved where the left and right calls are read from,\n"
                + "so they stay quiet. The rest still calls.");

        // The other maintained address, and the bigger one: sixty-six calls ride on what
        // an ability actually hit.
        //
        // Read from the runner rather than off the address, because a reading parser
        // answers hits instead and the hook stands down on purpose while it does.
        // Warning on the address alone called the hits dead at exactly the moment
        // something else was supplying them.
        if (Runner is { HitsCovered: false })
            PipWarn("No hit calls",
                "This patch moved where damage is read from, so calls that wait\n"
                + "on a hit stay quiet. Casts, debuffs and tethers still call.\n"
                + "A running parser would cover these.");

        // Not a fault: the parser answers these better, so the client's own reads stand
        // down while it is up. Worth saying, because it is the answer to the fight
        // behaving differently with a parser open.
        if (Runner is { ClientReadsStoodDown: true })
            Pip1(true, "Parser feed",
                "Casts, hits, debuffs and tethers are coming from the parser,\n"
                + "which reads them better than the client can. The plugin's\n"
                + "own reads take over again the moment it stops.");

        // These are gone rather than late: the frame stopped draining the feed.
        if (Runner is { ParserDropped: > 0 } fed)
            PipWarn($"Feed dropped {fed.ParserDropped}",
                "The parser handed over more than the frame could take, so some\n"
                + "events were dropped. Calls riding on those never happened.");

        if (MutedHere) PipWarn("This fight is off");

        if (_board.Dropped > 0)
            PipWarn($"Dropped {_board.Dropped}",
                $"More calls at once than the screen holds ({AlertBoard.Capacity}).\n"
                + "The furthest out got dropped. Counted for this pull only.");
    }

    private void DrawStatusHeader()
    {
        // Gathered and measured before the panel is opened, because the panel has to be
        // tall enough for the rows they need. Everything read here comes off the runner
        // and the config, so none of it depends on being inside the child.
        GatherPips();
        var room = MathF.Max(Theme.S(120f),
            ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X * 2f - Theme.S(6f));
        var rows = PipRows(room);

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.PanelBg);
        var height = ImGui.GetTextLineHeightWithSpacing() * (1 + rows) + 16;
        if (ImGui.BeginChild("##status", new Vector2(0, height), true, ImGuiWindowFlags.NoScrollbar))
        {
            // Accent bar down the left edge of the panel.
            var dl = ImGui.GetWindowDrawList();
            var wp = ImGui.GetWindowPos();
            dl.AddRectFilled(wp, wp + new Vector2(Theme.S(3f), ImGui.GetWindowHeight()), Theme.Accent);

            // The Test control's room, reserved before anything else is drawn:
            // measuring it first is what lets the line be cut to fit.
            var right = ImGui.GetWindowWidth()
                - (ImGui.CalcTextSize("Test").X + ImGui.GetFrameHeight()
                   + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.GetStyle().WindowPadding.X + Theme.S(12f));

            // What is loaded right now beats what exists: standing in the fight,
            // the line names it and says how much it has to say.
            var fights = FightCatalog.All.Count;
            var here = Service.ClientState.TerritoryType;
            var name = FightCatalog.At(here)?.Name;
            // A fight the plugin knows by name but has no calls for yet. Without
            // this the line reads "14 fights loaded" while you stand in one of the
            // fourteen hearing nothing, which looks like something broke rather
            // than like a fight nobody has written yet.
            var named = name is null ? Shipped.At((ushort)here)?.Name : null;
            var line = Runner is { TriggerCount: > 0 } r
                ? r.SpeakingCount == r.TriggerCount
                    ? $"{name ?? r.Fight}, {r.TriggerCount} call{(r.TriggerCount == 1 ? "" : "s")} ready"
                    : $"{name ?? r.Fight}, {r.SpeakingCount} of {r.TriggerCount} speaking"
                : named is not null ? $"{named}, no calls written yet"
                : fights > 0 ? $"{fights} fight{(fights == 1 ? "" : "s")} loaded" : null;
            if (line is not null)
                ImGui.TextUnformatted(Widgets.Elide(line, right - ImGui.GetCursorPosX()));
            else
                ImGui.TextColored(Theme.V(Theme.Heading), "No fights built yet");

            // Never left of what was just drawn, whatever the line turned out to be.
            var lineEnd = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
            ImGui.SameLine(0, 0);
            ImGui.SetCursorPosX(MathF.Max(right, lineEnd + Theme.S(10f)));
            var test = C.TestMode;
            if (Widgets.GreenCheckbox("Test", ref test)) { C.TestMode = test; C.Save(); }
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip("Sample call on screen. Drag it where you want it.");

            DrawPips(room);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // ---- left sidebar nav ----

    // The sidebar fits its longest nav label, measured while it draws.
    private const float SidebarMinWidth = 186f;
    private float _sidebarW = SidebarMinWidth;
    private float _navNeed;

    private static FontAwesomeIcon CategoryIcon(string cat) => cat switch
    {
        "Ultimate" => FontAwesomeIcon.Crown,
        "Savage" => FontAwesomeIcon.Skull,
        _ => FontAwesomeIcon.LayerGroup,
    };

    private void DrawSidebar()
    {
        _navNeed = 0f;
        DrawSidebarSearch();
        if (NavItem(FontAwesomeIcon.Home, "Home", _nav == NavKind.Home)) _nav = NavKind.Home;

        ImGui.Spacing();
        SidebarHeading("FIGHTS");
        foreach (var cat in FightCatalog.Categories)
        {
            var count = FightCatalog.CountIn(cat);
            // A fight's own page keeps its category lit, so the sidebar still says
            // where you are rather than going blank one level down.
            var here = (_nav == NavKind.Fights && _navCategory == cat)
                || (_nav == NavKind.Fight && FightCatalog.All
                        .FirstOrDefault(f => f.TerritoryId == _navFightId)?.Category == cat);
            if (NavItem(CategoryIcon(cat), cat, here, count))
            {
                _nav = NavKind.Fights;
                _navCategory = cat;
            }
        }
        if (FightCatalog.All.Count == 0)
            SidebarWarning("Nothing built yet");
        // Null, not zero: the badge draws whatever it is given, and "Raid Plan 0"
        // reads as a broken plan rather than as no plan.
        if (NavItem(FontAwesomeIcon.ClipboardList, "Raid Plan", _nav == NavKind.Plan,
            Runner is { PlanCalls: > 0 } rp ? rp.PlanCalls : null)) _nav = NavKind.Plan;
        // Beside the plan, because the plan is written in these seats. The badge is
        // how many were named by hand, and null rather than zero so a group that has
        // never set one reads as no seats rather than as none found.
        if (NavItem(FontAwesomeIcon.UserFriends, "Roles", _nav == NavKind.Roles,
            C.PartySeats.Count > 0 ? C.PartySeats.Count : null)) _nav = NavKind.Roles;

        ImGui.Spacing();
        SidebarHeading("ON SCREEN");
        if (NavItem(FontAwesomeIcon.Desktop, "Call Display", _nav == NavKind.CallDisplay,
            dot: C.AlertsEnabled ? 0u : Theme.Warn)) _nav = NavKind.CallDisplay;

        // Its own heading rather than a settings row: a hand-written trigger is a
        // thing that speaks, like a fight, not a preference.
        if (ShowMine)
        {
            ImGui.Spacing();
            SidebarHeading("MINE");
            if (NavItem(FontAwesomeIcon.Bolt, "My Triggers", _nav == NavKind.Mine,
                Runner is { Mine.Live: > 0 } m ? m.Mine.Live : null,
                dot: C.UserTriggersEnabled ? 0u : Theme.Warn)) _nav = NavKind.Mine;
        }

        ImGui.Spacing();
        SidebarHeading("SETTINGS");
        if (NavItem(FontAwesomeIcon.VolumeUp, "TTS", _nav == NavKind.Tts,
            dot: C.VoiceEnabled ? 0u : Theme.Warn)) _nav = NavKind.Tts;
        if (Runner is { Voice.Unavailable: true }) SidebarWarning("No voice here");
        if (NavItem(FontAwesomeIcon.Palette, "Appearance", _nav == NavKind.Appearance)) _nav = NavKind.Appearance;

        DrawSidebarConnection();

        // Next frame's width, so no label is clipped once a scrollbar appears.
        var bar = ImGui.GetScrollMaxY() > 0f ? ImGui.GetStyle().ScrollbarSize : 0f;
        _sidebarW = MathF.Max(SidebarMinWidth * Theme.Scale, _navNeed + bar);
    }

    // Whether the parser is feeding, in the corner of every page, and the way in to
    // the page about it.
    //
    // On means feeding, not merely present. A parser that answered and then never opened
    // its channel is off as far as any call is concerned, and off is muted rather than
    // amber because it is not a fault: every kind of event has a client route of its own,
    // so a fight with no parser at all still calls.
    private void DrawSidebarConnection()
    {
        ImGui.Spacing();
        SidebarHeading("Connection");

        var on = Runner is { ParserReading: true };
        if (ConnectionRow("Parser", on ? "on" : "off", on ? Theme.Good : Theme.Muted,
                on ? "Events are coming from the parser. Click for the setup."
                    : "No parser. The client reads every kind on its own. Click for the setup.",
                selected: _nav == NavKind.Parser))
            _nav = NavKind.Parser;
    }

    // One read-only line: a muted label, then the state in its own color.
    //
    // The line is one hit target and the words are drawn over it, the same way a nav row
    // is built. Hung on the text instead, only the four letters of the state would
    // answer a hover, and the label is the part somebody points at.
    private bool ConnectionRow(string label, string state, uint color, string tip,
        bool selected = false)
    {
        var pad = Theme.S(8f);
        var lineH = ImGui.GetTextLineHeightWithSpacing();

        var at = ImGui.GetCursorScreenPos();
        var back = ImGui.GetCursorPos();
        ImGui.InvisibleButton("##conn" + label, new Vector2(ImGui.GetContentRegionAvail().X, lineH));
        var clicked = ImGui.IsItemClicked();
        Widgets.Tooltip(tip);

        // The same wash and edge bar a selected nav row gets, drawn under the words
        // rather than over them, so the row reads as the one that is open.
        if (selected)
        {
            var dlSel = ImGui.GetWindowDrawList();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            dlSel.AddRectFilled(min, max, (Theme.Accent & 0x00FFFFFFu) | 0x2A000000u);
            dlSel.AddRectFilled(new Vector2(min.X, min.Y + 1f),
                new Vector2(min.X + Theme.S(3f), max.Y - 1f), Theme.Accent, 2f);
        }

        var name = $"{label}:";
        var nameW = ImGui.CalcTextSize(name).X;
        var dl = ImGui.GetWindowDrawList();
        dl.AddText(at + new Vector2(pad, 0f), Theme.Muted, name);
        dl.AddText(at + new Vector2(pad + nameW + Theme.S(6f), 0f), color, state);

        // Measured like every other row, so the sidebar widens for it rather than
        // clipping the state off the end.
        _navNeed = MathF.Max(_navNeed,
            pad + nameW + Theme.S(6f) + ImGui.CalcTextSize(state).X + Theme.S(12f));

        ImGui.SetCursorPos(new Vector2(back.X, back.Y + lineH));
        return clicked;
    }

    private static void SidebarHeading(string text)
    {
        ImGui.Spacing();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Theme.S(8f));
        // Muted, so the accent belongs to the selected row alone.
        ImGui.TextColored(Theme.V(Theme.Heading), text.ToUpperInvariant());
        ImGui.Spacing();
    }

    private void SidebarWarning(params string[] lines)
    {
        foreach (var line in lines)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Theme.S(20f));
            ImGui.TextColored(Theme.V(Theme.Warn), line);
            _navNeed = MathF.Max(_navNeed, ImGui.GetItemRectSize().X + Theme.S(28f));
        }
        ImGui.Spacing();
    }

    private bool NavItem(FontAwesomeIcon icon, string label, bool selected, int? count = null, uint dot = 0)
    {
        var startX = ImGui.GetCursorPosX();
        var startY = ImGui.GetCursorPosY();

        // A wash plus an edge bar, so the accent reads without shouting.
        var rgb = Theme.Accent & 0x00FFFFFFu;
        if (selected)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, rgb | 0x2A000000u);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, rgb | 0x3C000000u);
        }
        var rowH = Theme.S(27f);
        var clicked = ImGui.Selectable($"##nav-{label}", selected, ImGuiSelectableFlags.None, new Vector2(0, rowH));
        var navMin = ImGui.GetItemRectMin();
        var navMax = ImGui.GetItemRectMax();
        if (selected) ImGui.PopStyleColor(2);
        if (selected)
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddRectFilled(
                new Vector2(min.X, min.Y + 2f), new Vector2(min.X + Theme.S(3f), max.Y - 2f), Theme.Accent, 2f);
        }

        var endX = ImGui.GetCursorPosX();
        var endY = ImGui.GetCursorPosY();
        var col = selected ? new Vector4(1f, 1f, 1f, 1f) : Theme.V(Theme.NavText);

        // Icon (icon font) + label drawn over the selectable row.
        var textY = startY + (rowH - ImGui.GetTextLineHeight()) * 0.5f;
        var labelX = startX + Theme.S(38f);
        ImGui.SameLine();
        ImGui.SetCursorPos(new Vector2(startX + Theme.S(12f), textY));
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            ImGui.TextColored(col, icon.ToIconString());
        ImGui.SameLine();
        ImGui.SetCursorPos(new Vector2(labelX, textY));
        ImGui.TextColored(col, label);
        // The tail is the right padding plus any count badge.
        _navNeed = MathF.Max(_navNeed,
            labelX + ImGui.CalcTextSize(label).X + Theme.S(count is null ? 12f : 40f));

        if (count is { } n)
        {
            var txt = n.ToString();
            ImGui.SameLine();
            // Never left of the label, so a long one pushes the badge instead
            // of having the badge drawn across it.
            var badgeX = MathF.Max(labelX + ImGui.CalcTextSize(label).X + Theme.S(8f),
                ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(txt).X - Theme.S(10f));
            ImGui.SetCursorPos(new Vector2(badgeX, textY));
            ImGui.TextDisabled(txt);
        }

        // A small status dot on the right, where a count would sit.
        if (dot != 0)
            ImGui.GetWindowDrawList().AddCircleFilled(
                new Vector2(navMax.X - Theme.S(12f), (navMin.Y + navMax.Y) * 0.5f), Theme.S(3f), dot);

        ImGui.SetCursorPos(new Vector2(endX, endY)); // resume normal flow below the row
        // Picking a page by hand ends any search that was up.
        if (clicked) _search = "";
        return clicked;
    }

    // ---- settings search ----

    private string _search = "";

    // Which result the keyboard is on, and whether Enter was pressed in the box.
    private int _searchSel;
    private bool _searchEntered;
    private string _searchPrev = "";

    // One fixed place for the box, so no page shifts down a line to make room.
    private void DrawSidebarSearch()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - Theme.S(2f));
        _searchEntered = ImGui.InputTextWithHint("##settingsearch", "Search", ref _search, 64,
            ImGuiInputTextFlags.EnterReturnsTrue);
        // A new query starts back at the top of the list.
        if (_search != _searchPrev) { _searchPrev = _search; _searchSel = 0; }
        ImGui.Spacing();
    }

    // True while a query is up, so the page gives way to the results.
    private bool Searching => _search.Trim().Length >= 2;

    // A call found by its words, and the fight it belongs to.
    private readonly record struct CallHit(FightEntry Fight, CallEntry Call);

    private const int MaxCallHits = 30;

    // What this call says on screen: the player's own wording once they have changed
    // it, the shipped line until then.
    //
    // Both halves where it has two. A buster's row read "Tank Buster on someone",
    // which is true and is not what anybody is scanning the list for; the half that
    // matters is the one that fires when it is on you. An edited call shows the
    // edit alone, because those are the player's own words for every case.
    private string Wording(CallEntry call) =>
        C.EditFor(call.Key)?.Text is { Length: > 0 } mine ? mine
        : TriggerSample.Join(call.OnYou, call.Text);

    private List<CallHit> SearchCalls(string text)
    {
        var found = new List<CallHit>();
        // Trimmed once rather than per call. This sat in the inner loop, so every call
        // in every fight allocated its own copy of the same string, on every frame the
        // search box had anything in it.
        var needle = text.Trim();
        if (needle.Length < 2) return found;

        foreach (var fight in FightCatalog.All)
        {
            foreach (var call in FightCatalog.CallsIn(fight.TerritoryId))
            {
                // Either wording finds it. Somebody who renamed a call searches what
                // they named it; somebody reading a strat searches what it shipped as.
                if (!Wording(call).Contains(needle, StringComparison.OrdinalIgnoreCase)
                    && !call.Text.Contains(needle, StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(new CallHit(fight, call));
                if (found.Count >= MaxCallHits) return found;
            }
        }
        return found;
    }

    private void DrawSearchResults()
    {
        var hits = SettingsIndex.Search(_search);
        var calls = SearchCalls(_search);

        // Ahead of everything else, the nothing-matched line included.
        //
        // It sat below that early return, so the one state somebody most wants out of was
        // the one state escape did not answer: a query matching nothing left them deleting
        // it by hand, unless the box happened to still hold focus and ImGui's own revert
        // caught it.
        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false)) { _search = ""; _searchSel = 0; return; }

        if (hits.Count == 0 && calls.Count == 0)
        {
            ImGui.TextDisabled($"Nothing matches \"{_search.Trim()}\".");
            ImGui.TextDisabled("Try a setting, a page, or what a call says.");
            return;
        }

        // The keys drive the settings while there are any and the calls otherwise.
        //
        // They only ever drove the settings. A query matching nothing but what a call says
        // still listed the calls, said "up / down to move, enter to open" nowhere, and ate
        // the enter that followed: the list was there, the keys were dead, and nothing on
        // screen said which.
        var onCalls = hits.Count == 0;
        var count = onCalls ? calls.Count : hits.Count;

        var moved = false;
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow, true)) { _searchSel++; moved = true; }
        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow, true)) { _searchSel--; moved = true; }
        _searchSel = Math.Clamp(_searchSel, 0, Math.Max(0, count - 1));

        var go = !onCalls && _searchEntered ? _searchSel : -1;

        if (hits.Count > 0)
        {
            ImGui.TextDisabled(
                $"{hits.Count} setting{(hits.Count == 1 ? "" : "s")}   ·   up / down to move, enter to open");
            ImGui.Spacing();
        }
        for (var i = 0; i < hits.Count; i++)
        {
            var e = hits[i];
            ImGui.PushID(e.Prop);
            if (ImGui.Selectable("##hit", i == _searchSel, ImGuiSelectableFlags.None,
                    new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 1.6f)))
                go = i;
            if (moved && i == _searchSel) ImGui.SetScrollHereY(0.5f);
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var dl = ImGui.GetWindowDrawList();
            // Two lines centered in the row, and each cut to the row's width so
            // a long label cannot run out past the selection.
            var textX = Theme.S(8f);
            var room = max.X - min.X - textX * 2f;
            var lineH = ImGui.GetTextLineHeight();
            var top = min.Y + (max.Y - min.Y - lineH * 2f) * 0.5f;
            dl.AddText(new Vector2(min.X + textX, top), Theme.TextBright, Widgets.Elide(e.Label, room));
            dl.AddText(new Vector2(min.X + textX, top + lineH), Theme.Muted,
                Widgets.Elide(SettingsIndex.Where(e), room));
            ImGui.PopID();
        }

        if (go >= 0)
        {
            _nav = hits[go].Nav;
            _search = "";
            _searchSel = 0;
            return;
        }

        DrawCallHits(calls, onCalls ? _searchSel : -1, onCalls && _searchEntered, moved);
    }

    // The calls a query found. `selected` is -1 when the keys are driving the settings
    // above instead, which is what keeps one highlight on screen rather than two.
    private void DrawCallHits(List<CallHit> calls, int selected, bool enter, bool moved)
    {
        if (calls.Count == 0) return;

        ImGui.Spacing();
        var many = calls.Count >= MaxCallHits
            ? $"first {calls.Count} calls"
            : $"{calls.Count} call{(calls.Count == 1 ? "" : "s")}";
        ImGui.TextDisabled(selected >= 0
            ? $"{many}   ·   up / down to move, enter to open"
            : many);
        ImGui.Spacing();

        for (var i = 0; i < calls.Count; i++)
        {
            var hit = calls[i];
            ImGui.PushID(hit.Fight.TerritoryId + hit.Call.Key);
            if (ImGui.Selectable("##callhit", i == selected, ImGuiSelectableFlags.None,
                    new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 1.6f))
                || (enter && i == selected))
            {
                OpenFight(hit.Fight);
                OpenCall(hit.Call);
                _search = "";
                _searchSel = 0;
                ImGui.PopID();
                return;
            }
            if (moved && i == selected) ImGui.SetScrollHereY(0.5f);
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var dl = ImGui.GetWindowDrawList();
            var textX = Theme.S(8f);
            var room = max.X - min.X - textX * 2f;
            var lineH = ImGui.GetTextLineHeight();
            var top = min.Y + (max.Y - min.Y - lineH * 2f) * 0.5f;
            dl.AddText(new Vector2(min.X + textX, top), Theme.TextBright,
                Widgets.Elide(CallText.Sentence(Wording(hit.Call)), room));
            dl.AddText(new Vector2(min.X + textX, top + lineH), Theme.Muted,
                Widgets.Elide(hit.Fight.Name, room));
            ImGui.PopID();
        }
    }

    private void DrawSelectedPage()
    {
        // Nothing routes to a page that has no way in, so it lands on Home instead.
        if (_nav == NavKind.Mine && !ShowMine) _nav = NavKind.Home;

        Widgets.LabelScope(_nav.ToString());
        switch (_nav)
        {
            case NavKind.Home: DrawHomePage(); break;
            case NavKind.CallDisplay: DrawCallDisplayPage(); break;
            case NavKind.Mine: DrawMinePage(); break;
            case NavKind.Tts: DrawTtsPage(); break;
            case NavKind.Appearance: DrawAppearancePage(); break;
            case NavKind.Fight: DrawFightPage(); break;
            case NavKind.Plan: DrawPlanPage(); break;
            case NavKind.Roles: DrawRolesPage(); break;
            case NavKind.Parser: DrawParserPage(); break;
            default: DrawFightCategoryPage(_navCategory); break;
        }
    }

    // ---- page header ----
    // Every page opens with the same row, so the master switch and the reset are
    // always in the same three pixels.

    private (float RowH, float EndX) PageTitle(string name, FontAwesomeIcon icon = FontAwesomeIcon.None)
    {
        var frameH = ImGui.GetFrameHeight();
        var px = MathF.Round(ImGui.GetFontSize() * 1.35f);
        var font = _fonts.Get(px);
        var big = font is { Available: true };

        var start = ImGui.GetCursorPos();
        var scr = ImGui.GetCursorScreenPos();

        Vector2 sz;
        if (big) using (font!.Push()) sz = ImGui.CalcTextSize(name);
        else sz = ImGui.CalcTextSize(name);

        var rowH = MathF.Max(sz.Y, frameH);
        var top = (rowH - sz.Y) * 0.5f;

        ImGui.GetWindowDrawList().AddRectFilled(
            new Vector2(scr.X, scr.Y + top + Theme.S(2f)),
            new Vector2(scr.X + Theme.S(3f), scr.Y + top + sz.Y - Theme.S(2f)),
            Theme.Accent, 2f);

        // The page's emblem, the same one its nav row carries.
        var textX = start.X + Theme.S(11f);
        if (icon != FontAwesomeIcon.None)
        {
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            {
                var ic = icon.ToIconString();
                var isz = ImGui.CalcTextSize(ic);
                ImGui.SetCursorPos(new Vector2(textX, start.Y + (rowH - isz.Y) * 0.5f));
                ImGui.TextColored(Theme.V(Theme.Accent), ic);
                textX += isz.X + Theme.S(8f);
            }
        }

        ImGui.SetCursorPos(new Vector2(textX, start.Y + top));
        if (big) using (font!.Push()) ImGui.TextColored(Theme.V(Theme.Accent), name);
        else ImGui.TextColored(Theme.V(Theme.Accent), name);

        ImGui.SetCursorPos(start);
        return (rowH, textX + sz.X);
    }

    // Returns the master switch's new value, or null when the caller must not
    // write one back: the reset wrote the defaults straight into the config, and
    // the value passed in is what it looked like a moment before that.
    private bool? PageHead(string name, string note, bool master,
        bool hasMaster = true, Action? reset = null,
        FontAwesomeIcon icon = FontAwesomeIcon.None, uint noteCol = 0)
    {
        var wasReset = false;
        var frameH = ImGui.GetFrameHeight();
        var start = ImGui.GetCursorPos();
        var (rowH, endX) = PageTitle(name, icon);

        var used = endX;
        if (note.Length > 0)
        {
            var lh = ImGui.GetTextLineHeight();
            ImGui.SetCursorPos(new Vector2(endX + Theme.S(12f), start.Y + (rowH - lh) * 0.5f));
            ImGui.TextColored(Theme.V(noteCol == 0 ? Theme.Muted : noteCol), note);
            used = endX + Theme.S(12f) + ImGui.CalcTextSize(note).X;
        }

        // The reset button is an icon plus side padding, wider than a frame is tall.
        var resetW = 0f;
        if (reset != null)
        {
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                resetW = ImGui.CalcTextSize(FontAwesomeIcon.Undo.ToIconString()).X;
            resetW += ImGui.GetStyle().FramePadding.X * 2f;
        }
        var right = (reset != null ? resetW + Theme.S(8f) : 0f) + (hasMaster ? frameH + Theme.S(8f) : 0f);
        // Never left of the name, whatever it turned out to be.
        ImGui.SetCursorPos(new Vector2(
            MathF.Max(used + Theme.S(12f), ImGui.GetContentRegionMax().X - right),
            start.Y + (rowH - frameH) * 0.5f));

        if (reset != null)
        {
            if (ImGuiComponents.IconButton("##pgreset", FontAwesomeIcon.Undo)) { reset(); wasReset = true; }
            if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Back to defaults.");
            ImGui.SameLine(0, Theme.S(8f));
        }
        if (hasMaster)
        {
            var v = master;
            if (Widgets.GreenCheckbox("##pgmaster", ref v)) { master = v; C.Save(); }
        }
        ImGui.SetCursorPos(new Vector2(start.X, start.Y + rowH));
        ImGui.Spacing();
        return wasReset ? null : master;
    }

    // Everything on one page back to how it ships.
    private void ResetPage(NavKind nav)
    {
        SettingsIndex.ResetPage(C, nav);
        C.Save();
        Theme.Accent = C.AccentColor;
        Theme.Colorblind = C.ColorblindMode;
        Theme.Scale = Math.Clamp(C.UiScale, 0.8f, 1.6f);
        _overlay.RequestReposition();
    }

    private static string Version => typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1.0.0.0";

    // Approximate width of an icon button, for centering.
    private static float IconBtnWidth(FontAwesomeIcon icon, string text)
    {
        float iw;
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            iw = ImGui.CalcTextSize(icon.ToIconString()).X;
        var st = ImGui.GetStyle();
        return iw + st.ItemInnerSpacing.X + ImGui.CalcTextSize(text).X + st.FramePadding.X * 2f;
    }
}
