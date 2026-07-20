using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// The Party Mit Recap as its own movable / resizable window, themed to match the
// config UI: what mits were on the boss and the party this pull, when, by whom,
// and which standard raid mits never landed.
public class RecapWindow : Window
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public RecapWindow(Plugin plugin) : base("Party Mit Recap###recapwin")
    {
        _plugin = plugin;
        Size = new Vector2(470, 540);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw()
    {
        Theme.PushWindow();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        Theme.PopWindow();
    }

    public override void Draw()
    {
        Theme.PushWidgets();
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 16f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 7));

        DrawBody();

        ImGui.PopStyleVar(3);
        Theme.PopWidgets();
    }

    private void DrawBody()
    {
        var r = _plugin.Recap;

        if (!r.HasData)
        {
            // Empty state: say what will appear here and how to get it, and
            // offer the sample so the window can be judged without a wipe.
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Accent), "No pull recorded yet");
            ImGui.Spacing();
            if (C.RecapEnabled)
            {
                ImGui.TextColored(Theme.V(Theme.Muted), "Do a pull; the boss's mits, the party's cooldowns and");
                ImGui.TextColored(Theme.V(Theme.Muted), "anything missing will show here after it ends.");
            }
            else
            {
                ImGui.TextColored(Theme.V(Theme.Muted), "The recap is switched off. Enable it on the");
                ImGui.TextColored(Theme.V(Theme.Muted), "Party Mit Recap page of the FrenMits settings.");
            }
            ImGui.Spacing();
            if (Button("Load sample pull")) r.LoadSample();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Fill the recap with a fake pull to see how everything reads.");
            return;
        }

        // ---- header: boss + wipe time, Copy on the right -------------------
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.V(Theme.Accent), string.IsNullOrEmpty(r.BossName) ? "Last pull" : r.BossName);
        if (r.CaptureElapsed > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(Theme.V(Theme.Muted), $"·  ended {Mmss(r.CaptureElapsed)} in");
        }
        var copyW = ImGui.CalcTextSize("Copy").X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(MathF.Max(ImGui.GetCursorPosX() + 12, ImGui.GetContentRegionMax().X - copyW));
        if (Button("Copy")) ImGui.SetClipboardText(r.ToText());
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Copy the recap as text; paste it into Discord or your notes.");

        // Pull history: the last few wipes stay browsable, newest first, so a
        // fix can be checked against the pull it was made for.
        if (r.History.Count > 1)
        {
            ImGui.Spacing();
            ImGui.BeginDisabled(r.View >= r.History.Count - 1);
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                if (ImGui.SmallButton(FontAwesomeIcon.ChevronLeft.ToIconString() + "##pullolder"))
                    r.View = Math.Min(r.View + 1, r.History.Count - 1);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Older pull");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(Theme.V(Theme.Accent),
                r.View == 0 ? "Latest pull" : $"{r.View} pull{(r.View == 1 ? "" : "s")} back");
            ImGui.SameLine();
            ImGui.TextColored(Theme.V(Theme.Muted), $"· {r.History.Count} kept");
            ImGui.SameLine();
            ImGui.BeginDisabled(r.View <= 0);
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                if (ImGui.SmallButton(FontAwesomeIcon.ChevronRight.ToIconString() + "##pullnewer"))
                    r.View = Math.Max(0, r.View - 1);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Newer pull");
        }

        // At a glance: one chip per question a raid lead asks after a wipe.
        // Green = fine, amber/red = look closer; the sections below hold the
        // detail in the same order.
        ImGui.Dummy(new Vector2(0, 2));
        var missed = r.NotSeen();
        Widgets.Chip("raid mits", $"{MitRecap.StandardRaidMits.Length - missed.Count}/{MitRecap.StandardRaidMits.Length}",
            missed.Count == 0 ? Theme.Good : Theme.Warn);
        ImGui.SameLine(0, 6);
        Widgets.Chip("deaths", r.LastDeaths.Count.ToString(), r.LastDeaths.Count == 0 ? Theme.Good : Theme.Danger);
        if (r.Shown.PlanTotal > 0)
        {
            ImGui.SameLine(0, 6);
            Widgets.Chip("on plan", $"{r.Shown.PlanGood}/{r.Shown.PlanTotal}",
                r.Shown.PlanGood == r.Shown.PlanTotal ? Theme.Good : Theme.Warn);
        }
        ImGui.SameLine(0, 6);
        Widgets.Chip("unused CDs", r.Shown.Unused.Count.ToString(), r.Shown.Unused.Count == 0 ? Theme.Good : Theme.Warn);

        // Missing standard raid mits, spelled out.
        if (missed.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Warn), "Never landed:  " + string.Join("   ", missed));
            ImGui.TextColored(Theme.V(Theme.Muted), "comp-dependent: no caster = no Addle, no MCH = no Dismantle");
        }

        // Plan vs. actual: the sheet graded against the pull. Late and missing
        // presses read as one-line stories; on-plan presses just count.
        if (r.Shown.PlanTotal > 0)
        {
            Widgets.SectionHeader("Plan check");
            var good = r.Shown.PlanGood;
            var total = r.Shown.PlanTotal;
            ImGui.TextColored(good == total ? Theme.V(Theme.Good) : Theme.V(Theme.TextBright),
                good == total
                    ? $"All {total} planned mits went out on plan."
                    : $"{good} of {total} planned mits went out on plan.");
            var plh = ImGui.GetTextLineHeight();
            foreach (var h in r.Shown.PlanProblems.Take(10))
            {
                if (h.Icon != 0) { Icons.Draw(h.Icon, new Vector2(plh, plh)); ImGui.SameLine(0, 6); }
                ImGui.TextColored(h.Missed ? Theme.V(Theme.Danger) : Theme.V(Theme.Warn), h.Mit);
                ImGui.SameLine();
                ImGui.TextColored(Theme.V(Theme.Muted),
                    $"· {(int)h.Time / 60}:{(int)h.Time % 60:00}"
                    + (h.Missed ? " · never went out" : $" · {h.Delta:0}s late")
                    + (h.Mechanic.Length > 0 ? $" · {h.Mechanic}" : ""));
            }
            if (r.Shown.PlanProblems.Count > 10)
                ImGui.TextColored(Theme.V(Theme.Muted), $"+{r.Shown.PlanProblems.Count - 10} more in Copy");
        }

        // Cooldowns that sat unused all pull - the most actionable line a
        // raid lead can read after a wipe.
        if (r.Shown.Unused.Count > 0)
        {
            Widgets.SectionHeader("Left on the table");
            var lh = ImGui.GetTextLineHeight();
            foreach (var (who, mit, note, icon) in r.Shown.Unused)
            {
                if (icon != 0) { Icons.Draw(icon, new Vector2(lh, lh)); ImGui.SameLine(0, 6); }
                ImGui.TextColored(Theme.V(Theme.Warn), mit);
                ImGui.SameLine();
                ImGui.TextColored(Theme.V(Theme.Muted), $"· {who} · {note}");
            }
        }

        // What was still running when the pull ended.
        Widgets.SectionHeader("Still up at the end");
        if (r.Snapshot.Count == 0) ImGui.TextColored(Theme.V(Theme.Muted), "Nothing was active.");
        else foreach (var m in r.Snapshot.OrderByDescending(m => m.OnBoss).ThenBy(m => m.Source))
        {
            if (m.Icon != 0) { Icons.Draw(m.Icon, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight())); ImGui.SameLine(0, 6); }
            var col = MitTypes.Color(m.Kind, C);
            ImGui.TextColored(col != 0 ? Theme.V(col) : Theme.V(Theme.TextBright), m.Mit);
            ImGui.SameLine();
            ImGui.TextColored(Theme.V(Theme.Muted), $"· {m.Source} · {m.Remaining:0}s");
        }

        // Full timeline: ONE row per mechanic, its mits inline with coverage
        // counts, so a wipe reads at a glance instead of as a long scroll.
        Widgets.SectionHeader("Timeline");
        ImGui.TextColored(Theme.V(Theme.Muted), "mit colors:");
        foreach (var (kind, label) in new[]
                 {
                     (MitTypes.Kind.Party, "party"), (MitTypes.Kind.Tank, "tank"),
                     (MitTypes.Kind.Personal, "personal"),
                 })
        {
            ImGui.SameLine();
            var kc = MitTypes.Color(kind, C);
            ImGui.TextColored(kc != 0 ? Theme.V(kc) : Theme.V(Theme.TextBright), label);
        }
        ImGui.SameLine();
        ImGui.TextColored(Theme.V(Theme.Muted), "· 7/8 = party coverage, hover for names");
        var events = r.LastEvents();
        var party = r.LastParty;
        var fight = _plugin.ActiveFight();
        // The pull being shown may be from another zone (wipe, then teleport
        // out): its rows must not be grouped under THIS zone's mechanic names.
        if (fight != null && fight.TerritoryId != r.Territory) fight = null;
        if (ImGui.BeginTable("##recapwin", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX,
                new Vector2(0, 0)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 52);
            ImGui.TableSetupColumn("Mechanic", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Mits", ImGuiTableColumnFlags.WidthStretch, 1);
            ImGui.TableHeadersRow();

            var ih = ImGui.GetTextLineHeight();
            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deaths = r.LastDeaths.OrderBy(d => d.Time).ToList();
            var dIdx = 0;
            var idx = 0;
            while (idx < events.Count)
            {
                // Consecutive events under the same mechanic share one row.
                var mech = MechanicFor(fight, events[idx].Time);
                var group = new List<MitRecap.MitEvent> { events[idx] };
                var next = idx + 1;
                while (next < events.Count
                       && MechanicFor(fight, events[next].Time) == mech
                       && events[next].Time - events[idx].Time < 25f)
                    group.Add(events[next++]);
                idx = next;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(Theme.V(Theme.Muted), $"{(int)group[0].Time / 60}:{(int)group[0].Time % 60:00}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(mech.Length > 0 ? mech : "-");
                ImGui.TableNextColumn();

                var n = 0;
                foreach (var e in group)
                {
                    // Three mits per visual line, then wrap inside the cell, so
                    // heavy mechanics don't clip off the right edge.
                    if (n > 0 && n % 3 != 0) { ImGui.SameLine(0, 2); ImGui.TextColored(Theme.V(Theme.Muted), " · "); ImGui.SameLine(0, 2); }
                    n++;
                    if (e.Icon != 0) { Icons.Draw(e.Icon, new Vector2(ih, ih)); ImGui.SameLine(0, 4); }
                    var col = MitTypes.Color(e.Kind, C);
                    ImGui.TextColored(col != 0 ? Theme.V(col) : Theme.V(Theme.TextBright), e.Mit);

                    if (e.OnBoss)
                    {
                        ImGui.SameLine(0, 4);
                        ImGui.TextColored(Theme.V(Theme.Muted), "(boss)");
                    }
                    else if (e.Kind == MitTypes.Kind.Party && party.Count is > 1 and <= 8)
                    {
                        // Coverage only for party-wide buffs, and only with a sane
                        // 8-man denominator (alliance zones would read "8/24").
                        // Coverage: how many of the party the buff actually hit.
                        ImGui.SameLine(0, 4);
                        var full = e.Covered.Count >= party.Count;
                        ImGui.TextColored(full ? Theme.V(Theme.Good) : Theme.V(Theme.Warn),
                            $"{e.Covered.Count}/{party.Count}");
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextColored(Theme.V(Theme.Muted), $"{e.Mit} coverage:");
                            foreach (var name in party)
                            {
                                // Check/cross via the icon font: the text font
                                // has no glyph for either symbol.
                                var hit = e.Covered.Contains(name);
                                var rowCol = hit ? Theme.V(Theme.Good) : Theme.V(Theme.Danger);
                                using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                                    ImGui.TextColored(rowCol,
                                        (hit ? FontAwesomeIcon.Check : FontAwesomeIcon.Times).ToIconString());
                                ImGui.SameLine(0, 5);
                                ImGui.TextColored(rowCol, name);
                            }
                            ImGui.EndTooltip();
                        }
                    }
                    else if (e.Covered.Count == 1)
                    {
                        ImGui.SameLine(0, 4);
                        ImGui.TextColored(Theme.V(Theme.Muted), e.Covered[0]);
                    }
                }

                // Deaths land on the mechanic they happened during: usually the
                // whole wipe story in one line.
                var groupEnd = idx < events.Count ? events[idx].Time : float.MaxValue;
                while (dIdx < deaths.Count && deaths[dIdx].Time < groupEnd)
                {
                    // Each death carries its story: how fast they dropped and
                    // what they still had running (or that nothing was up).
                    var d = deaths[dIdx++];
                    using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                        ImGui.TextColored(Theme.V(Theme.Danger), FontAwesomeIcon.SkullCrossbones.ToIconString());
                    ImGui.SameLine(0, 5);
                    ImGui.TextColored(Theme.V(Theme.Danger), d.Name);
                    var story = new List<string>();
                    if (d.FromPct > 0f && d.Seconds > 0f)
                        story.Add($"{(int)(d.FromPct * 100)}% to dead in {d.Seconds:0.0}s");
                    story.Add(d.Had.Length > 0 ? "had " + d.Had : "nothing up");
                    ImGui.SameLine();
                    ImGui.TextColored(Theme.V(Theme.Muted), "· " + string.Join(" · ", story));
                }

                // The plan-vs-reality delta: what the sheet expected around this
                // moment that never appeared (or only partially landed). Only
                // graded when the capture came from THIS duty.
                if (fight != null && fight.TerritoryId == r.Territory)
                {
                    var delta = PlanDelta(fight, group, events, party, reported);
                    if (delta.Length > 0)
                    {
                        ImGui.PushTextWrapPos(0f);
                        ImGui.TextColored(Theme.V(Theme.Warn), delta);
                        ImGui.PopTextWrapPos();
                    }
                }
            }
            ImGui.EndTable();
        }
    }

    // What the plan expected near this moment that the recap never saw. Only
    // mits the recap can recognize (tracked status names) are judged, so a
    // fancy plan label can't produce a phantom "missing". A mit planned by two
    // slots but seen once is treated as covered: the recap can't tell whose it
    // was. Alias and blind-spot tables live in MitRecap, shared with the
    // capture-time plan check.

    private string PlanDelta(FightProfile fight, List<MitRecap.MitEvent> group,
        List<MitRecap.MitEvent> events, List<string> party, HashSet<string> reported)
    {
        var t0 = group[0].Time;

        // "Applied" looks beyond this group: a mit pressed early lands in the
        // previous group but still satisfies this moment's plan.
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in events)
        {
            if (MathF.Abs(e.Time - t0) > 15f) continue;
            applied.Add(e.Mit.Trim());
            foreach (var pm in Cooldowns.PlanMits(e.Mit)) applied.Add(pm.Name);
            foreach (var (part, canon) in MitRecap.StatusAliases)
                if (e.Mit.Contains(part, StringComparison.OrdinalIgnoreCase)) applied.Add(canon);
        }

        var partial = new List<string>();
        foreach (var e in group)
            if (e.Kind == MitTypes.Kind.Party && party.Count is > 1 and <= 8
                && !e.OnBoss && e.Covered.Count < party.Count)
                partial.Add($"{e.Mit} {e.Covered.Count}/{party.Count}");

        var missing = new List<string>();
        foreach (var (slot, line) in PlannedLinesNear(fight, t0, _plugin.ActiveJobAbbreviation()))
            foreach (var pm in Cooldowns.PlanMits(line.Action))
            {
                if (applied.Contains(pm.Name)) continue;
                if (MitRecap.DeltaBlind.Contains(pm.Name)) continue;
                if (MitTypes.Classify(pm.Name) == MitTypes.Kind.Other) continue; // recap can't see it
                if (!reported.Add(pm.Name)) continue; // one report per pull, not per adjacent group
                missing.Add(slot.Length > 0 ? $"{pm.Name} ({slot})" : pm.Name);
            }

        var parts = new List<string>();
        if (missing.Count > 0) parts.Add("missing: " + string.Join(", ", missing));
        if (partial.Count > 0) parts.Add("partial: " + string.Join(", ", partial));
        return string.Join("   ", parts);
    }

    // Every slot's planned lines near a moment - the shared iterator in
    // MitRecap, narrowed to a 9s window around the group.
    private static IEnumerable<(string Slot, MitLine Line)> PlannedLinesNear(FightProfile fight, float time, string? myJob)
        => MitRecap.PlannedLines(fight, myJob).Where(x => MathF.Abs(x.Line.Time - time) < 9f);

    // The plan mechanic nearest this moment (within a window), so recap rows can
    // group under the same names the calls used. Empty when no fight is active
    // or nothing on the plan is close.
    private static string MechanicFor(FightProfile? fight, float time)
    {
        if (fight == null) return "";
        MitLine? best = null;
        var bestDist = 9f; // window: within 9s of a planned call
        foreach (var l in fight.Lines)
        {
            var d = MathF.Abs(l.Time - time);
            if (d < bestDist && !string.IsNullOrWhiteSpace(l.Mechanic)) { best = l; bestDist = d; }
        }
        return best?.Mechanic.Trim() ?? "";
    }

    private static string Mmss(float t) => $"{(int)t / 60}:{(int)t % 60:00}";

    private static bool Button(string label) => ImGui.Button(label);
}
