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

    // Dot separated, skipping the empties, so a row with nothing to add on the
    // left does not open with a dangling separator.
    private static string Join(string a, string b)
        => a.Length == 0 ? b : b.Length == 0 ? a : a + "  ·  " + b;

    // Who this row is for, in words. Four rows off one trigger read the same
    // otherwise: it is the audience that tells them apart, not the wording.
    private string Audience(BossAlert a)
    {
        var parts = new List<string>();
        if (a.Match.Target == FrenMits.Callouts.ActorScope.Me) parts.Add("on you");
        else if (a.Match.Target == FrenMits.Callouts.ActorScope.OtherPlayer) parts.Add("on someone else");

        foreach (var role in RolesOf(a).Split(',', StringSplitOptions.RemoveEmptyEntries))
            parts.Add(role.Trim() switch
            {
                "tank" => "tanks",
                "healer" => "healers",
                "dps" => "dps",
                var other => other,
            });

        // Jobs only when there are few enough to read. A call for twenty of
        // them is a call for everyone, and listing them says nothing.
        if (parts.Count == 0 && a.Jobs.Split(',', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 and <= 3 } jobs)
            parts.AddRange(jobs);

        return string.Join(" and ", parts);
    }

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
    // The duties worth offering: the ones the plugin has an official sheet for
    // and that the pack has calls for. Cached, because Builtin.Has reads a file.
    private uint[]? _alertDuties;

    // Hardest first, which is the order the sidebar already uses, and newest
    // first inside a tier so the fight being progged is near the top.
    private static int TierOrder(uint duty) => Builtin.Category(duty) switch
    {
        "Ultimate" => 0,
        "Chaotic" => 1,
        "Savage" => 2,
        "Extreme" => 3,
        "Occult Crescent" => 4,
        _ => 5,
    };

    private static string TierOf(uint duty)
        => Builtin.Category(duty) is { Length: > 0 } c && c != "Other" ? c : "Other duties";

    // The duty's own name, without the tier that its group heading already says.
    private static string DutyName(uint duty)
    {
        var full = DutyDetail(duty);
        if (full.Length == 0) return $"Duty {duty}";
        var dot = full.IndexOf('·');
        return dot > 0 ? full[(dot + 1)..].Trim() : full;
    }

    private uint[] AlertDuties => _alertDuties ??= Alerts.Duties
        .Where(Builtin.Has)
        .OrderBy(TierOrder)
        .ThenByDescending(d => d)
        .ToArray();

    private uint AlertsDuty()
    {
        var offered = AlertDuties;
        if (offered.Length == 0) return 0u;

        var here = _plugin.ActiveFight()?.TerritoryId ?? 0u;
        if (Array.IndexOf(offered, here) >= 0) return here;
        if (Array.IndexOf(offered, C.LastAlertsDuty) >= 0) return C.LastAlertsDuty;
        return offered[0];
    }

    private void DrawBossAlertsPage()
    {
        // Tells the overlay to hold a sample up while this page is open.
        _plugin.Callouts.PreviewFrame = ImGui.GetFrameCount();

        var duty = AlertsDuty();
        _plugin.Callouts.PreviewDuty = duty;

        // Beside the title, in amber, the way the meter says "Not Connected". A
        // broken pack is the more urgent of the two, so it takes the slot.
        var note = Alerts.Problem.Length > 0 ? Alerts.Problem : "Work in progress";
        C.BossAlertsEnabled = PageHead("Boss Alerts", note, C.BossAlertsEnabled,
            reset: ResetBossAlerts, icon: FontAwesomeIcon.Bullhorn, noteCol: Theme.Warn);

        if (Alerts.Problem.Length > 0)
        {
            ImGui.TextWrapped("Calls ship with the plugin. If this keeps saying so, reinstall.");
            return;
        }
        // Off is off, the way every other page reads: the header, and nothing
        // else to look at. Held-but-visible left a page of gray controls.
        if (!C.BossAlertsEnabled) return;

        DrawAlertSample(duty);
        ImGui.Spacing();

        Widgets.ListBegin();
        var speak = C.BossAlertsSpeak;
        if (Widgets.RowCheck("Enable TTS", "", ref speak))
        { C.BossAlertsSpeak = speak; C.Save(); }

        var draw = C.BossAlertsDraw;
        if (Widgets.RowCheck("Text Overlay", "", ref draw))
        { C.BossAlertsDraw = draw; C.Save(); }

        var size = C.AlertFontSizePx;
        if (Widgets.RowDrag("Text Size", "", ref size, 14f, 90f, "%.0f px", 86f))
        { C.AlertFontSizePx = size; C.Save(); }

        var pos = C.AlertOverlayPosition;
        if (PlaceRows(ref pos, AlertHome)) { C.AlertOverlayPosition = pos; C.Save(); }
        Widgets.ListEnd();

        ImGui.Spacing();
        DrawAlertCalls(duty, Alerts.For(duty).Where(a => !a.NamedOnly).ToList());
    }

    // The card at the top. With a call open it is that call, so typing in its
    // Says box rewrites the banner in front of you, at the size, color and art
    // it will land in. With nothing open it is the stand-in.
    private void DrawAlertSample(uint duty)
    {
        var open = OpenAlert(duty);
        DrawLiveSample("##alertsample", () =>
        {
            if (open is not { } a) { _plugin.AlertOverlay.DrawSampleBanner(); return; }
            _plugin.AlertOverlay.DrawSampleBanner(SaysOf(a), a.Icon,
                (FrenMits.Callouts.CallSeverity)(int)LevelOf(a),
                a.Match.Target == FrenMits.Callouts.ActorScope.Me);
        });

        if (open is null) return;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - Theme.S(2f));
        ImGui.TextColored(Theme.V(Theme.Muted), "The call you have open.");
    }

    // The call whose editor is open, if it is one of this fight's.
    private BossAlert? OpenAlert(uint duty)
    {
        if (_alertOpen.Length == 0) return null;
        foreach (var a in Alerts.For(duty))
            if (TweakKey(a) == _alertOpen) return a;
        return null;
    }

    // The calls themselves: which fight, how many are on, and then the list.
    private void DrawAlertCalls(uint duty, List<BossAlert> all)
    {
        Widgets.GroupLabel("Calls");

        if (AlertDuties.Length == 0)
        {
            ImGui.TextColored(Theme.V(Theme.Muted), "No fight here has calls yet.");
            return;
        }

        DrawAlertFight(duty, all);
        DrawAlertFilters();
        ImGui.Spacing();

        var shown = Filtered(all);
        if (shown.Count == 0)
        {
            ImGui.TextColored(Theme.V(Theme.Muted),
                _alertSearch.Length > 0 ? "Nothing matches that." : "No calls here yet.");
            return;
        }

        DrawAlertPhases(shown.GroupBy(a => a.Group)
            .OrderBy(g => PhaseOrder(g.Key))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList());
    }

    // Fight order, not alphabet: phase two comes after phase one, phase ten
    // after phase nine, and whatever has no phase of its own comes last.
    private static int PhaseOrder(string group)
        => group.StartsWith("Phase ", StringComparison.Ordinal)
           && int.TryParse(group[6..], out var n) ? n : int.MaxValue;

    // The fight, and what it stands at. The counts belong beside the picker
    // rather than up in the title: they are about this fight, not the page.
    private void DrawAlertFight(uint duty, IReadOnlyList<BossAlert> all)
    {
        // Grouped under its tier, hardest first, because that is how anyone
        // thinks about which fight they want. A flat list of four hundred
        // duty names in territory order is not a list anyone reads.
        ImGui.SetNextItemWidth(Theme.S(300f));
        if (ImGui.BeginCombo("##alertduty", DutyName(duty)))
        {
            var tier = "";
            foreach (var d in AlertDuties)
            {
                if (TierOf(d) is var t && t != tier)
                {
                    if (tier.Length > 0) ImGui.Spacing();
                    tier = t;
                    ImGui.TextColored(Theme.V(Theme.Muted), tier.ToUpperInvariant());
                }
                if (ImGui.Selectable("   " + DutyName(d), d == duty))
                {
                    C.LastAlertsDuty = d;
                    _alertOpen = "";
                    C.Save();
                }
            }
            ImGui.EndCombo();
        }

        var on = all.Count(OnOf);
        var changed = all.Count(a => TweakFor(a) is { Empty: false });
        var onText = $"{on} of {all.Count}";

        // Measured, so the chips sit against the right edge and never land on
        // top of the picker on a narrow window.
        var need = Widgets.ChipWidth("On", onText);
        if (changed > 0) need += Theme.S(6f) + Widgets.ChipWidth("Changed", changed.ToString());
        ImGui.SameLine(0, Theme.S(10f));
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), ImGui.GetContentRegionMax().X - need));
        // A chip is shorter than the picker beside it, so it is centred on it
        // rather than left sitting a couple of pixels high.
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + Theme.S(2f));
        Widgets.Chip("On", onText, on > 0 ? Theme.Good : Theme.Muted);
        if (changed > 0)
        {
            ImGui.SameLine(0, Theme.S(6f));
            Widgets.Chip("Changed", changed.ToString(), Theme.Accent);
        }
    }

    // Search on the left with the room to be typed in, the filter against the
    // right. Both are frame height, so the line is level.
    private void DrawAlertFilters()
    {
        var gap = Theme.S(8f);
        var segW = Widgets.SegmentWidth(AlertFilters);
        var room = ImGui.GetContentRegionAvail().X - segW - gap;

        ImGui.SetNextItemWidth(MathF.Max(Theme.S(120f), room));
        var search = _alertSearch;
        if (ImGui.InputTextWithHint("##alertsearch", "Search calls", ref search, 64))
            _alertSearch = search;

        ImGui.SameLine(0, gap);
        Widgets.SegmentBegin();
        for (var i = 0; i < AlertFilters.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (Widgets.SegmentTall(AlertFilters[i] + "##af", _alertFilter == i)) _alertFilter = i;
        }
        Widgets.SegmentEnd();
    }

    private List<BossAlert> Filtered(IReadOnlyList<BossAlert> all)
    {
        var needle = _alertSearch.Trim();
        return all.Where(a =>
        {
            if (a.NamedOnly) return false;
            if (_alertFilter == 1 && !OnOf(a)) return false;
            if (_alertFilter == 2 && TweakFor(a) is not { Empty: false }) return false;
            if (needle.Length == 0) return true;
            return SaysOf(a).Contains(needle, StringComparison.OrdinalIgnoreCase)
                || a.Key.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    // A phase per tab. Stacked, the phases were five panels deep and the one
    // being worked on was wherever the scrollbar had left it; a tab bar puts
    // them side by side and opens on the one last looked at.
    private void DrawAlertPhases(List<IGrouping<string, BossAlert>> phases)
    {
        if (phases.Count == 1)
        {
            DrawAlertPhase(phases[0].Key, phases[0].ToList());
            return;
        }

        // Scrolls rather than shrinking every tab down to an unreadable stub.
        if (!ImGui.BeginTabBar("##alertphases", ImGuiTabBarFlags.FittingPolicyScroll))
            return;

        foreach (var phase in phases)
        {
            var calls = phase.ToList();
            // A dot for a phase carrying changes of yours, the same mark the
            // settings tabs use. The ### keeps the id fixed, so the dot coming
            // and going cannot reselect the tab.
            var edited = calls.Any(a => TweakFor(a) is { Empty: false });
            var label = edited ? $"{phase.Key} ·###ph{phase.Key}" : $"{phase.Key}###ph{phase.Key}";
            if (!ImGui.BeginTabItem(label)) continue;
            DrawAlertPhase(phase.Key, calls);
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    // What the open phase stands at, one switch for the lot of it, then its
    // calls.
    private void DrawAlertPhase(string name, List<BossAlert> calls)
    {
        var on = calls.Count(OnOf);
        var allOn = on == calls.Count;

        // Indented to the row column below, so the count reads as a caption on
        // the panel rather than as a line of its own floating above it.
        ImGui.Spacing();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Widgets.RowPad);
        ImGui.TextColored(Theme.V(on > 0 ? Theme.Good : Theme.Muted), $"{on}");
        ImGui.SameLine(0, Theme.S(4f));
        ImGui.TextColored(Theme.V(Theme.Muted), $"of {calls.Count} on");

        // With a search or a filter up, the switch only reaches what is on
        // screen, so it says so rather than reading like it takes the phase.
        var narrowed = _alertFilter != 0 || _alertSearch.Trim().Length > 0;
        var tail = narrowed ? " Shown" : "";

        // Both labels flip, so the wider of the pair is what has to fit, and it
        // ends on the same edge as the switches under it.
        var btnW = MathF.Max(Widgets.ButtonSize("All" + tail), Widgets.ButtonSize("None" + tail));
        ImGui.SameLine(0, Theme.S(10f));
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(),
            ImGui.GetContentRegionMax().X - Widgets.RowPad - btnW));
        if (ImGui.SmallButton((allOn ? "None" : "All") + tail + "##" + name))
        {
            foreach (var a in calls) { EditFor(a).On = !allOn; TidyTweak(a); }
            C.Save();
        }
        Tip(allOn ? "Turn every call listed here off." : "Turn every call listed here on.");
        ImGui.Spacing();

        // The rows scroll on their own, so the phase tabs, the search and the
        // fight stay where they were put. Forty calls in a page that scrolls as
        // one meant picking through a list with no way back to the tabs but the
        // scrollbar. A floor, since a short window would otherwise leave a strip
        // of list too thin to read.
        var room = MathF.Max(Theme.S(210f), ImGui.GetContentRegionAvail().Y - Theme.S(4f));
        if (ImGui.BeginChild("##rows" + name, new Vector2(0, room), false))
        {
            // One trigger upstream can watch eight ability ids, and the pack
            // needs a row per id to match them. Listing eight identical lines is
            // not what that trigger is, so rows saying the same thing about the
            // same mechanic are shown once and switched together.
            // The audience is part of what makes two rows different: one trigger
            // reads to tanks as a warning and to dps as a note, in the same words.
            Widgets.ListBegin();
            foreach (var same in calls.GroupBy(a => (a.Mechanic, SaysOf(a), Audience(a))))
                DrawAlertRow(same.ToList());
            Widgets.ListEnd();
        }
        ImGui.EndChild();
    }

    // One call. The whole row opens it, so nothing has to be aimed at.
    private void DrawAlertRow(List<BossAlert> same)
    {
        var a = same[0];
        var key = TweakKey(a);
        var open = _alertOpen == key;
        var tweak = TweakFor(a);
        var on = OnOf(a);

        // What it calls, in the words it calls it. That is the whole reason the
        // page exists, so it is the line, not a quote tucked under a generated
        // name like "Hyperdrive no target others".
        var label = SaysOf(a);
        if (a.NamedOnly && TweakFor(a)?.Text is null) label = a.Mechanic;

        // Underneath: the mechanic it comes off, then only what the line above
        // does not already say. A call honestly named after its own mechanic
        // would print the same words twice, so it prints them once.
        var under = string.Equals(a.Mechanic, label, StringComparison.OrdinalIgnoreCase) ? "" : a.Mechanic;
        if (a.Written) under = Join(under, "wording worked out per pull");
        if (Audience(a) is { Length: > 0 } who) under = Join(under, who);
        // Kept on the line, since it says what this row is: one call the pack
        // had to write several times to catch every id the fight casts.
        if (same.Count > 1) under = Join(under, $"{same.Count} casts");

        // Off for a reason, and the reason is worth reading before turning it on.
        if (a.Partial) under = Join(under, "off: only right on some pulls");
        if (a.NamedOnly && TweakFor(a)?.Text is null) under = Join("no wording yet", under);

        // The lead time gets a column of its own rather than a fourth clause in
        // a run of dots: down a long list it is the one number worth comparing
        // row to row, and a column is the only way to compare anything.
        var lead = LeadOf(a) > 0f ? $"{LeadOf(a):0.#}s early" : "";

        var top = ImGui.GetCursorScreenPos();
        var flip = on;
        if (Widgets.RowCheckClick(label, under, ref flip, id: key, gameIcon: a.Icon,
            changed: tweak is { Empty: false }))
        {
            foreach (var one in same) { EditFor(one).On = flip; TidyTweak(one); }
            C.Save();
        }
        if (Widgets.RowClicked) _alertOpen = open ? "" : key;

        RowDetail(top, lead, on);
        LevelBand(top, LevelOf(a), on);
        if (open) DrawAlertEditor(same);
    }

    // A muted note in its own column, right of the row and left of the switch.
    // Drawn rather than laid out, so it cannot push the name around.
    private static void RowDetail(Vector2 top, string text, bool on)
    {
        if (text.Length == 0) return;
        var bottom = ImGui.GetCursorScreenPos().Y;
        var right = top.X + ImGui.GetContentRegionAvail().X
                    - Widgets.RowPad - ImGui.GetFrameHeight() - Theme.S(12f);
        var size = ImGui.CalcTextSize(text);
        ImGui.GetWindowDrawList().AddText(
            new Vector2(right - size.X, top.Y + (bottom - top.Y - size.Y) * 0.5f),
            on ? Theme.Muted : (Theme.Muted & 0x00FFFFFFu) | 0x88000000u, text);
    }

    // A band down the left edge of a row, in the color the banner will be, for
    // the two loud levels only. Faint while the call is off, so a loud one you
    // have switched off can still be picked out of a list without shouting.
    private static void LevelBand(Vector2 top, AlertLevel level, bool on)
    {
        if (level == AlertLevel.Info) return;
        var bottom = ImGui.GetCursorScreenPos().Y;
        var inset = Theme.S(4f);
        var col = LevelColor(level);
        ImGui.GetWindowDrawList().AddRectFilled(
            new Vector2(top.X + 1f, top.Y + inset),
            new Vector2(top.X + 1f + Theme.S(3f), bottom - inset),
            on ? col : (col & 0x00FFFFFFu) | 0x4D000000u, 2f);
    }

    // What a text box in the editor gets: the row less its label column and the
    // padding, floored so it stays typeable on a narrow window.
    private static float AlertBoxWidth()
        => MathF.Max(Theme.S(220f), ImGui.GetContentRegionAvail().X - Theme.S(240f));

    private void DrawAlertEditor(List<BossAlert> same)
    {
        var a = same[0];
        var t = EditFor(a);
        var dirty = false;
        Widgets.LabelScope("alertedit");

        // A written call builds its words from the pull, so there is no one line
        // to edit. Showing an editable box that the fight then ignores is worse
        // than showing none.
        if (a.Written)
        {
            Widgets.RowBegin("Says", "This one is written for the fight and picks its own words",
                0f, sub: true);
            Widgets.RowEnd();
            Widgets.RowNote(a.Text);
            DrawWrittenTail(same, t);
            return;
        }

        var says = t.Text ?? a.Text;

        // The two boxes take whatever the row has left over, so a wide window
        // gets a wide box instead of three hundred pixels marooned in the
        // middle of it. The floor keeps them usable on a narrow one.
        var boxW = AlertBoxWidth();

        // The row reserves exactly this much and puts the cursor at its left
        // edge, so anything sharing the row has to be counted here. Asking for
        // the box alone drew the Test button past the window and out of reach.
        var testW = Widgets.ButtonSize("Test") + Theme.S(8f);
        Widgets.RowBegin("Says", "What the banner reads", boxW + testW, sub: true);
        ImGui.SetNextItemWidth(boxW);
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
        // The same reserve, so this box sits directly under the one above it.
        Widgets.RowBegin("Speak", "Blank says the line below", boxW + testW, sub: true);
        ImGui.SetNextItemWidth(boxW);
        if (ImGui.InputTextWithHint("##tts" + a.Key, SpeaksOf(a), ref spoken, 128))
        { t.Tts = spoken.Length == 0 ? null : spoken; dirty = true; }
        Widgets.RowEnd();

        var level = (int)(t.Level ?? a.Level);
        // Reserved, not guessed. Asked for nothing, the run started at the right
        // edge and ran out past the panel.
        Widgets.RowBegin("Loudness", "Picks the color and the alert sound",
            Widgets.SegmentWidth(LevelNames), ctlHeight: Widgets.SmallHeight, sub: true);
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
        if (Widgets.RowCombo("Who Hears It", "", ref whoIdx, WhoNames, sub: true))
        { t.Roles = whoIdx == 0 ? null : WhoRoles[whoIdx]; dirty = true; }

        if (TweakFor(a) is { Empty: false })
        {
            Widgets.RowBegin("Your Changes", "", Widgets.ButtonSize("Reset"),
                ctlHeight: Widgets.SmallHeight, sub: true);
            Widgets.PushDangerOutline();
            if (ImGui.SmallButton("Reset##" + a.Key))
            {
                foreach (var one in same) C.BossAlertTweaks.Remove(TweakKey(one));
                dirty = true;
            }
            Widgets.PopDanger();
            Tip("Drop your changes to this call and put back the one that shipped.");
            Widgets.RowEnd();
        }

        Widgets.LabelScope("");
        if (!dirty) return;

        // Whatever was just changed on the first goes to the rest of the ids.
        for (var i = 1; i < same.Count; i++)
        {
            if (C.BossAlertTweaks.TryGetValue(TweakKey(a), out var edited))
                C.BossAlertTweaks[TweakKey(same[i])] = new AlertTweak
                {
                    On = edited.On, Text = edited.Text, Tts = edited.Tts,
                    Sound = edited.Sound, Level = edited.Level, Roles = edited.Roles,
                };
            else C.BossAlertTweaks.Remove(TweakKey(same[i]));
        }
        TidyTweak(a);
        C.Save();
    }

    // The bit of the editor a written call still gets: who hears it, and a way
    // back to how it shipped.
    private void DrawWrittenTail(List<BossAlert> same, AlertTweak t)
    {
        var a = same[0];
        var roles = t.Roles ?? a.Roles;
        var whoIdx = Math.Max(0, Array.IndexOf(WhoRoles, roles));
        var dirty = Widgets.RowCombo("Who Hears It", "", ref whoIdx, WhoNames, sub: true);
        if (dirty) t.Roles = whoIdx == 0 ? null : WhoRoles[whoIdx];

        if (TweakFor(a) is { Empty: false })
        {
            Widgets.RowBegin("Your Changes", "", Widgets.ButtonSize("Reset"),
                ctlHeight: Widgets.SmallHeight, sub: true);
            Widgets.PushDangerOutline();
            if (ImGui.SmallButton("Reset##w" + a.Key))
            {
                foreach (var one in same) C.BossAlertTweaks.Remove(TweakKey(one));
                dirty = true;
            }
            Widgets.PopDanger();
            Widgets.RowEnd();
        }

        Widgets.LabelScope("");
        if (dirty) { TidyTweak(a); C.Save(); }
    }

    // Say it now, the way a pull would, so nobody has to guess.
    private void TestAlert(BossAlert a)
    {
        // Test means show me this, so it turns the banner on rather than being
        // ignored when the banner is off. Off stays off afterwards only if it is
        // switched off again, which now hides it on the spot.
        if (!C.BossAlertsEnabled || !C.BossAlertsDraw)
        {
            C.BossAlertsEnabled = true;
            C.BossAlertsDraw = true;
            C.Save();
        }

        _plugin.Callouts.ShowTest(SaysOf(a), a.Icon,
            (FrenMits.Callouts.CallSeverity)(int)LevelOf(a),
            a.Match.Target == FrenMits.Callouts.ActorScope.Me);

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
        C.AlertFontSizePx = 34f;
        C.BossAlertsRecord = false;
        _alertSearch = "";
        _alertFilter = 0;
        _alertOpen = "";
        C.Save();
    }
}
