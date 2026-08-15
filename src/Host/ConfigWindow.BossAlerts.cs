using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;

namespace FrenMits.Host;

// Settings: Boss Alerts, where every call a fight makes can be changed.
//
// The list is long, so the page shows one line per call and opens the rest only
// when asked. A line answers the three things worth knowing at a glance: is it
// on, what does it say, and when does it land. Everything else is a click away.
public partial class ConfigWindow
{
    private string _alertSearch = "";
    private string _alertOpen = "";
    private int _alertFilter;          // 0 all, 1 on, 2 changed
    private readonly HashSet<string> _alertShut = new(StringComparer.Ordinal);

    private static readonly string[] AlertFilters = { "All", "On", "Changed" };
    private static readonly string[] LevelNames = { "Info", "Warn", "Danger" };
    private static readonly string[] WhoNames = { "Everyone", "Tanks", "Healers", "DPS" };
    private static readonly string[] WhoRoles = { "", "tank", "healer", "dps" };

    // The plugin owns the one copy; loading a second here would parse nine
    // thousand rows twice and let the two drift.
    private AlertBook Alerts => _plugin.Alerts;

    private static string TweakKey(BossAlert a) => $"{a.Territory}|{a.Key}";

    private AlertTweak? TweakFor(BossAlert a)
        => C.BossAlertTweaks.TryGetValue(TweakKey(a), out var t) ? t : null;

    private AlertTweak EditFor(BossAlert a)
    {
        var key = TweakKey(a);
        if (!C.BossAlertTweaks.TryGetValue(key, out var t))
            C.BossAlertTweaks[key] = t = new AlertTweak();
        return t;
    }

    // A tweak that ends up saying nothing is dropped, so "changed" stays honest.
    private void TidyTweak(BossAlert a)
    {
        var key = TweakKey(a);
        if (C.BossAlertTweaks.TryGetValue(key, out var t) && t.Empty)
            C.BossAlertTweaks.Remove(key);
    }

    private string SaysOf(BossAlert a) => TweakFor(a)?.Text ?? a.Text;

    // What a voice reads out: the player's own line, else the sayable one the
    // bake derived from the banner, else the banner as it stands.
    private string SpeaksOf(BossAlert a)
    {
        var mine = TweakFor(a)?.Tts;
        if (!string.IsNullOrEmpty(mine)) return mine;
        return a.Tts.Length > 0 ? a.Tts : SaysOf(a);
    }

    private bool OnOf(BossAlert a) => TweakFor(a)?.On ?? a.On;
    private AlertLevel LevelOf(BossAlert a) => TweakFor(a)?.Level ?? a.Level;
    private string RolesOf(BossAlert a) => TweakFor(a)?.Roles ?? a.Roles;
    private float LeadOf(BossAlert a) => a.Lead;

    // Where the banner ships, so Reset puts it back exactly there.
    private static readonly Vector2 AlertHome = new(0.5f, 0.42f);

    private static uint LevelColor(AlertLevel level) => level switch
    {
        AlertLevel.Danger => Theme.Danger,
        AlertLevel.Warn => Theme.Warn,
        _ => Theme.Muted,
    };

    // Which duty the page is showing. The fight you are in wins, then whatever
    // was open last, then the first one that has any calls at all.
    private uint AlertsDuty()
    {
        var here = _plugin.ActiveFight()?.TerritoryId ?? 0u;
        if (here != 0 && Alerts.For(here).Count > 0) return here;
        if (C.LastAlertsDuty != 0 && Alerts.For(C.LastAlertsDuty).Count > 0) return C.LastAlertsDuty;
        return Alerts.Duties.Count > 0 ? Alerts.Duties.OrderBy(d => d).First() : 0u;
    }

    private void DrawBossAlertsPage()
    {
        var duty = AlertsDuty();
        var all = Alerts.For(duty);
        var changed = all.Count(a => TweakFor(a) is { Empty: false });

        var note = Alerts.Problem.Length > 0 ? Alerts.Problem
            : changed > 0 ? $"{all.Count} calls · {changed} changed"
            : $"{all.Count} calls";

        C.BossAlertsEnabled = PageHead("Boss Alerts", note, C.BossAlertsEnabled,
            reset: ResetBossAlerts, icon: FontAwesomeIcon.Bullhorn,
            noteCol: Alerts.Problem.Length > 0 ? Theme.Warn : 0u);

        // The sidebar says this too, but it scrolls away and the page does not.
        // The number is the honest one: casts, debuffs and spawns are read every
        // frame, and the rest waits on a hook that does not exist yet.
        ImGui.TextColored(Theme.V(Theme.Warn),
            "EXPERIMENTAL, WORK IN PROGRESS. Calls off casts, debuffs and spawns "
            + "are live. Head markers, tethers and map effects stay quiet.");
        ImGui.Spacing();

        if (Alerts.Problem.Length > 0)
        {
            ImGui.TextWrapped("Calls ship with the plugin. If this keeps saying so, reinstall.");
            return;
        }

        ImGui.BeginDisabled(!C.BossAlertsEnabled);

        Widgets.ListBegin();
        var speak = C.BossAlertsSpeak;
        if (Widgets.RowCheck("Read them out", "Spoken through your voice settings", ref speak))
        { C.BossAlertsSpeak = speak; C.Save(); }

        var draw = C.BossAlertsDraw;
        if (Widgets.RowCheck("Show them on screen", "A banner with a countdown", ref draw))
        { C.BossAlertsDraw = draw; C.Save(); }

        var pos = C.AlertOverlayPosition;
        if (PositionRow(ref pos, AlertHome)) { C.AlertOverlayPosition = pos; C.Save(); }
        if (NudgeRow(ref pos)) { C.AlertOverlayPosition = pos; C.Save(); }
        Widgets.ListEnd();

        ImGui.Spacing();
        DrawAlertTools(duty, all);
        ImGui.Spacing();

        var shown = Filtered(all);
        if (shown.Count == 0)
            ImGui.TextColored(Theme.V(Theme.Muted),
                _alertSearch.Length > 0 ? "Nothing matches that." : "No calls here yet.");
        else
            foreach (var group in shown.GroupBy(a => a.Group).OrderBy(g => g.Key, StringComparer.Ordinal))
                DrawAlertGroup(group.Key, group.ToList());

        ImGui.EndDisabled();
    }

    // Duty picker, search, and the three things worth filtering by.
    private void DrawAlertTools(uint duty, IReadOnlyList<BossAlert> all)
    {
        var duties = Alerts.Duties.OrderBy(d => d).ToArray();
        var names = duties.Select(d => DutyDetail(d) is { Length: > 0 } n ? n : $"Duty {d}").ToArray();
        var idx = Math.Max(0, Array.IndexOf(duties, duty));

        ImGui.SetNextItemWidth(Theme.S(260f));
        if (ImGui.Combo("##alertduty", ref idx, names, names.Length))
        {
            C.LastAlertsDuty = duties[idx];
            _alertOpen = "";
            C.Save();
        }

        ImGui.SameLine(0, Theme.S(10f));
        ImGui.SetNextItemWidth(Theme.S(220f));
        var search = _alertSearch;
        if (ImGui.InputTextWithHint("##alertsearch", "Search calls", ref search, 64))
            _alertSearch = search;

        ImGui.SameLine(0, Theme.S(10f));
        Widgets.SegmentBegin();
        for (var i = 0; i < AlertFilters.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (Widgets.Segment(AlertFilters[i] + "##af", _alertFilter == i)) _alertFilter = i;
        }
        Widgets.SegmentEnd();

        ImGui.SameLine(0, Theme.S(10f));
        if (ImGui.SmallButton(_alertShut.Count > 0 ? "Expand all" : "Collapse all"))
        {
            if (_alertShut.Count > 0) _alertShut.Clear();
            else foreach (var g in all.Select(a => a.Group).Distinct()) _alertShut.Add(g);
        }
    }

    private List<BossAlert> Filtered(IReadOnlyList<BossAlert> all)
    {
        var needle = _alertSearch.Trim();
        return all.Where(a =>
        {
            if (_alertFilter == 1 && !OnOf(a)) return false;
            if (_alertFilter == 2 && TweakFor(a) is not { Empty: false }) return false;
            if (needle.Length == 0) return true;
            return SaysOf(a).Contains(needle, StringComparison.OrdinalIgnoreCase)
                || a.Key.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    // A phase, with a count and one switch for the lot of it.
    private void DrawAlertGroup(string name, List<BossAlert> calls)
    {
        var open = !_alertShut.Contains(name);
        var on = calls.Count(OnOf);

        ImGui.Spacing();
        Widgets.GroupLabel($"{name}  ({on} of {calls.Count} on)");
        ImGui.SameLine();

        var right = ImGui.GetContentRegionMax().X - Theme.S(76f);
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), right));
        if (ImGui.SmallButton((on == calls.Count ? "None##" : "All##") + name))
        {
            var want = on != calls.Count;
            foreach (var a in calls) { EditFor(a).On = want; TidyTweak(a); }
            C.Save();
        }
        ImGui.SameLine(0, Theme.S(6f));
        if (ImGui.SmallButton((open ? "Hide##" : "Show##") + name))
        {
            if (open) _alertShut.Add(name); else _alertShut.Remove(name);
        }

        if (!open) return;

        Widgets.ListBegin();
        foreach (var a in calls) DrawAlertRow(a);
        Widgets.ListEnd();
    }

    // One call. The whole row opens it, so nothing has to be aimed at.
    private void DrawAlertRow(BossAlert a)
    {
        var key = TweakKey(a);
        var open = _alertOpen == key;
        var tweak = TweakFor(a);
        var on = OnOf(a);

        // The mechanic leads, because that is what you came looking for. What it
        // says follows, because that is what you came to change. A row with no
        // wording of its own would print its name twice, so it says so instead.
        var hint = a.NamedOnly && TweakFor(a)?.Text is null
            ? "no wording yet, names the mechanic only"
            : $"“{SaysOf(a)}”";
        hint += LeadOf(a) > 0f ? $"  ·  {LeadOf(a):0.#}s early" : "";

        var who = RolesOf(a);
        if (who.Length > 0) hint += "  ·  " + who;

        // Punctuation that reads fine does not survive being spoken, so a line
        // that comes out of a voice differently says so before it surprises you.
        var speaks = SpeaksOf(a);
        if (speaks != SaysOf(a)) hint += $"  ·  says “{speaks}”";
        if (tweak is { Empty: false }) hint += "  ·  changed";

        var flip = on;
        if (Widgets.RowCheckClick(a.Mechanic, hint, ref flip, id: key, gameIcon: a.Icon))
        {
            EditFor(a).On = flip;
            TidyTweak(a);
            C.Save();
        }
        if (Widgets.RowClicked) _alertOpen = open ? "" : key;

        if (open) DrawAlertEditor(a);
    }

    private void DrawAlertEditor(BossAlert a)
    {
        var t = EditFor(a);
        var dirty = false;
        Widgets.LabelScope("alertedit");

        var says = t.Text ?? a.Text;
        Widgets.RowBegin("Says", "What the banner reads", Theme.S(300f), sub: true);
        ImGui.SetNextItemWidth(Theme.S(300f));
        if (ImGui.InputText("##says" + a.Key, ref says, 128))
        {
            t.Text = says == a.Text ? null : says;
            dirty = true;
        }
        ImGui.SameLine(0, Theme.S(8f));
        if (ImGui.SmallButton("Test##" + a.Key)) TestAlert(a);
        Widgets.RowEnd();
        Widgets.RowNote("{target} who it landed on   {source} what cast it   {me} you   {n} which one");

        // The spoken line is the banner with its punctuation made sayable, so
        // the hint shows what a voice would actually read out.
        var spoken = t.Tts ?? "";
        Widgets.RowBegin("Speak", "Blank says the line below", Theme.S(300f), sub: true);
        ImGui.SetNextItemWidth(Theme.S(300f));
        if (ImGui.InputTextWithHint("##tts" + a.Key, SpeaksOf(a), ref spoken, 128))
        { t.Tts = spoken.Length == 0 ? null : spoken; dirty = true; }
        Widgets.RowEnd();

        var level = (int)(t.Level ?? a.Level);
        Widgets.RowBegin("Loudness", "Picks the color and the alert sound", 0f, sub: true);
        Widgets.SegmentBegin();
        for (var i = 0; i < LevelNames.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (Widgets.Segment(LevelNames[i] + "##lv" + a.Key, level == i, LevelColor((AlertLevel)i)))
            { t.Level = (AlertLevel)i; dirty = true; }
        }
        Widgets.SegmentEnd();
        Widgets.RowEnd();

        var roles = t.Roles ?? a.Roles;
        var whoIdx = Math.Max(0, Array.IndexOf(WhoRoles, roles));
        if (Widgets.RowCombo("Who hears it", "", ref whoIdx, WhoNames, sub: true))
        { t.Roles = whoIdx == 0 ? null : WhoRoles[whoIdx]; dirty = true; }

        if (TweakFor(a) is { Empty: false })
        {
            Widgets.RowBegin("", "", 0f, sub: true);
            Widgets.PushDangerOutline();
            if (ImGui.SmallButton("Reset##" + a.Key))
            {
                C.BossAlertTweaks.Remove(TweakKey(a));
                dirty = true;
            }
            Widgets.PopDanger();
            Tip("Drop your changes to this call and put back the one that shipped.");
            Widgets.RowEnd();
        }

        Widgets.LabelScope("");
        if (dirty) { TidyTweak(a); C.Save(); }
    }

    // Say it now, the way a pull would, so nobody has to guess.
    private void TestAlert(BossAlert a)
    {
        if (!C.BossAlertsSpeak) return;
        var spoken = SpeaksOf(a);

        var pick = C.TtsUseEdge
            ? (string.IsNullOrWhiteSpace(C.TtsCustomVoice) ? C.TtsEdgeVoice : C.TtsCustomVoice)
            : C.TtsVoice;
        _plugin.Audio.Speak(spoken, C.TtsRate, C.TtsVolume, C.TtsUseEdge, pick, Audio.AlertChannel);
    }

    private void ResetBossAlerts()
    {
        C.BossAlertTweaks.Clear();
        C.BossAlertsEnabled = true;
        C.BossAlertsSpeak = true;
        C.BossAlertsDraw = true;
        C.AlertOverlayPosition = AlertHome;
        C.BossAlertsRecord = false;
        _alertSearch = "";
        _alertFilter = 0;
        _alertShut.Clear();
        C.Save();
    }
}
