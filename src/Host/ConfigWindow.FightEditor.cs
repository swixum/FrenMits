using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

namespace FrenMits.Host;

// Settings: one fight's editor and its cards.
public partial class ConfigWindow
{
    private bool DrawFightEditor(FightProfile fight)
    {
        // Built-in fights are locked, so only user ones can change. The header
        // row above already carries the name and the star, so nothing to draw.
        if (IsOfficial(fight)) return true;

        var name = fight.Name;
        ImGui.SetNextItemWidth(Theme.S(260f));
        if (ImGui.InputText("Name", ref name, 128)) { fight.Name = name; C.Save(); }
        Tip("Times are seconds from the pull.");

        var ci = Array.IndexOf(Categories, fight.Category);
        if (ci < 0) ci = Categories.Length - 1;
        ImGui.SetNextItemWidth(Theme.S(120f));
        if (ImGui.Combo("Type", ref ci, Categories, Categories.Length))
        {
            fight.Category = Categories[ci];
            C.Save();
        }
        Tip("Which sidebar group this fight files under.");

        ImGui.SameLine();
        Widgets.PushDanger();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.TrashAlt, "Delete"))
            ImGui.OpenPopup("##delfight");
        Widgets.PopDanger();
        return !DrawDeleteFightConfirm(fight);
    }

    // The most destructive click here, so it confirms first.
    private bool DrawDeleteFightConfirm(FightProfile fight)
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##delfight", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return false;

        ImGui.TextUnformatted($"Delete \"{fight.Name}\"?");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Every slot's plan, notes and anchors go with it.");
        ImGui.TextDisabled("A snapshot is saved first. To recover later: recreate a sheet in the");
        ImGui.TextDisabled("same duty, then History > Find this duty's older snapshots.");
        ImGui.Spacing();

        var confirmed = false;
        if (ImGui.Button("Cancel", Theme.Sz(120f))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        if (Widgets.DangerButton("Delete", Theme.Sz(120f)))
        {
            _plugin.Snapshots.Save(fight, "before delete");
            confirmed = true;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
        return confirmed;
    }



    private Vector2 _cardTopLeft;
    private float _cardWidth;
    // Remembered rather than recomputed, so the unindent matches the indent
    // even if the scale slider moved between the two.
    private float _cardIndent;

    // Begin a styled card; every BeginCard needs an EndCard.
    private void BeginCard(FontAwesomeIcon icon, Vector4 iconColor, string title, string subtitle = "")
    {
        ImGui.Spacing();
        _cardTopLeft = ImGui.GetCursorScreenPos();
        _cardWidth = ImGui.GetContentRegionAvail().X;

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1); // content on the foreground channel

        _cardIndent = Theme.S(12f);
        ImGui.Indent(_cardIndent);
        ImGui.Dummy(new Vector2(0, Theme.S(6f)));
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            ImGui.TextColored(iconColor, icon.ToIconString());
        ImGui.SameLine(0, Theme.S(8f));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(title);
        if (!string.IsNullOrEmpty(subtitle))
        {
            ImGui.SameLine(0, Theme.S(10f));
            ImGui.TextColored(new Vector4(0.55f, 0.59f, 0.66f, 1f), subtitle);
        }
        ImGui.Spacing();
    }

    private void EndCard()
    {
        ImGui.Dummy(new Vector2(0, Theme.S(8f)));
        ImGui.Unindent(_cardIndent);

        var dl = ImGui.GetWindowDrawList();
        var min = _cardTopLeft;
        var max = new Vector2(_cardTopLeft.X + _cardWidth, ImGui.GetCursorScreenPos().Y);
        dl.ChannelsSetCurrent(0); // background channel
        dl.AddRectFilled(min, max, Theme.PanelBg, 8f);
        dl.AddRectFilled(min + new Vector2(0, Theme.S(8f)),
            new Vector2(min.X + Theme.S(3f), max.Y - Theme.S(8f)), Theme.Accent);
        dl.ChannelsMerge();
        ImGui.Spacing();
    }

    // A rounded "pill" showing one potion window (mm:ss).
    private static void TimePill(string text)
    {
        var dl = ImGui.GetWindowDrawList();
        var pad = new Vector2(8, 3) * Theme.Scale;
        var sz = ImGui.CalcTextSize(text);
        var p = ImGui.GetCursorScreenPos();
        var box = sz + pad * 2;
        dl.AddRectFilled(p, p + box, 0xFF2A2017, 6f);
        dl.AddRect(p, p + box, Theme.Accent, 6f);
        dl.AddText(p + pad, 0xFFECE8E6, text);
        ImGui.Dummy(box);
    }

    private static string Mmss(float t) => Fmt.MmssFloor(t);

    // Custom sheets get the same Your slot row as built-ins.
    private void DrawCustomColumnRow(FightProfile fight)
    {
        var slots = fight.CustomSlots.ToArray();
        if (slots.Length == 0) return;
        var idx = Array.FindIndex(slots, s => string.Equals(s, fight.Slot, StringComparison.OrdinalIgnoreCase));

        RowLabel("Slot");
        ImGui.SetNextItemWidth(Theme.S(150f));
        // No column picked yet shows an empty preview.
        if (ImGui.Combo("##customslot", ref idx, slots, slots.Length)
            && idx >= 0 && !string.Equals(slots[idx], fight.Slot, StringComparison.OrdinalIgnoreCase))
        {
            // SetSlot parks the old lines; assigning here would alias them.
            _plugin.SetSlot(fight, slots[idx]);
            _plugin.SheetViewWindow.MarkPlanDirty();
        }
        Tip("Which column is yours.");
        var slot = idx >= 0 ? slots[idx] : slots[0];

        DrawOffsetInline(fight);

        // Both resets behind one menu: they are rare and they are destructive.
        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine(0, Theme.S(10f));
            ImGui.TextColored(ImGuiColors.DalamudYellow, _builtinMsg);
        }
        if (fight.SavedSlots.Count > 0 || fight.Lines.Count > 0)
        {
            var w = ImGui.CalcTextSize("Reset").X + ImGui.GetStyle().FramePadding.X * 2f;
            var end = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
            ImGui.SameLine(MathF.Max(end + Theme.S(10f), ImGui.GetContentRegionMax().X - w));
            if (ImGui.SmallButton("Reset")) ImGui.OpenPopup("##customresetmenu");
            Tip("Clear this column, or every column. A snapshot is saved first.");
            // A modal cannot be opened from inside a popup, so the choice is
            // taken here and acted on once the menu has closed.
            var one = false;
            var all = false;
            if (ImGui.BeginPopup("##customresetmenu"))
            {
                if (ImGui.MenuItem("This column")) one = true;
                if (ImGui.MenuItem("Every column")) all = true;
                ImGui.EndPopup();
            }
            if (one) ImGui.OpenPopup("##confirm-customreset");
            if (all) ImGui.OpenPopup("##confirm-customresetall");
        }

        DrawCustomResetConfirm(fight, slot);
        DrawCustomResetAllConfirm(fight);
    }

    private void ClearCustomColumn(FightProfile fight, string slot)
    {
        // Clear in place, since Sheet View shares these list objects.
        if (string.Equals(slot, fight.Slot, StringComparison.OrdinalIgnoreCase)) fight.Lines.Clear();
        if (fight.SavedSlots.TryGetValue(slot, out var saved)) saved.Clear();
    }

    private void DrawCustomResetConfirm(FightProfile fight, string slot)
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##confirm-customreset", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        ImGui.TextUnformatted($"Empty the {slot} column?");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Its mits go; the sheet's rows, grades and notes stay.");
        ImGui.TextDisabled("A snapshot is saved first; Sheet View > Plan > History restores it.");
        ImGui.Spacing();

        if (ImGui.Button("Cancel", Theme.Sz(120f))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        if (Widgets.DangerButton("Empty this column", Theme.Sz(160f)))
        {
            _plugin.Snapshots.Save(fight, $"before reset {slot}");
            ClearCustomColumn(fight, slot);
            C.Save();
            _plugin.SheetViewWindow.MarkPlanDirty();
            FlashBuiltin($"{slot} emptied. History restores the old plan.");
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void DrawCustomResetAllConfirm(FightProfile fight)
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##confirm-customresetall", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        ImGui.TextUnformatted("Empty every column of this sheet?");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "All columns' mits go; the rows, grades and notes stay.");
        ImGui.TextDisabled("A snapshot is saved first; Sheet View > Plan > History restores it.");
        ImGui.Spacing();

        if (ImGui.Button("Cancel", Theme.Sz(120f))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        if (Widgets.DangerButton("Empty every column", Theme.Sz(170f)))
        {
            _plugin.Snapshots.Save(fight, "before reset all columns");
            fight.Lines.Clear();
            foreach (var saved in fight.SavedSlots.Values) saved.Clear();
            C.Save();
            _plugin.SheetViewWindow.MarkPlanDirty();
            FlashBuiltin("Every column emptied. History restores the old plan.");
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private int _pracRowIdx;
    private readonly Dictionary<string, int> _pracRowIdxs = new(); // per fight; headers can co-exist
    // The phase a preview was started from, so its segment reads as lit.
    private string _pracPhase = "";

    // Practice: a row of phase-jump buttons for this fight.
    private void DrawPracticeRow(FightProfile fight)
    {
        var phases = Builtin.PhaseStarts(fight.TerritoryId);
        if (phases.Count == 0)
        {
            var rows = fight.CustomRows.OrderBy(r => r.Time).ToList();
            if (rows.Count == 0) return;
            RowLabel("Practice");
            Tip("Preview a row's calls. Turns on Test Mode.");
            _pracRowIdx = Math.Clamp(_pracRowIdxs.GetValueOrDefault(fight.Id), 0, rows.Count - 1);
            var labels = rows.Select(r => $"{Mmss(r.Time)}  {r.Mechanic}").ToArray();
            ImGui.SetNextItemWidth(Theme.S(240f));
            ImGui.Combo("##pracrow", ref _pracRowIdx, labels, labels.Length);
            _pracRowIdxs[fight.Id] = _pracRowIdx;
            ImGui.SameLine(0, Theme.S(4f));
            if (ImGui.SmallButton("Go##pracrow")) _plugin.PracticeJump(fight, rows[_pracRowIdx].Time);
            if (Plugin.PreviewFight == fight && C.TestMode)
            {
                ImGui.SameLine(0, Theme.S(8f));
                if (ImGui.SmallButton("Stop##pracrow")) _plugin.StopPractice();
            }
            return;
        }

        RowLabel("Practice");
        Tip("Preview a phase's calls. Turns on Test Mode.");

        // Which phase is running shows in the fill, which loose buttons never did.
        var previewing = Plugin.PreviewFight == fight && C.TestMode;
        Widgets.SegmentBegin();
        for (var i = 0; i < phases.Count; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (Widgets.Segment($"{phases[i].Name}##prac{i}", previewing && _pracPhase == phases[i].Name))
            {
                _pracPhase = phases[i].Name;
                _plugin.PracticeJump(fight, phases[i].Time);
            }
            Tip($"Preview from {(int)phases[i].Time / 60}:{(int)phases[i].Time % 60:00}.");
        }
        Widgets.SegmentEnd();
        if (previewing)
        {
            ImGui.SameLine(0, Theme.S(8f));
            if (ImGui.SmallButton("Stop##prac")) { _plugin.StopPractice(); _pracPhase = ""; }
            ImGui.SameLine(0, Theme.S(6f));
            ImGui.TextColored(ImGuiColors.DalamudYellow, "previewing");
        }

        DrawPriorityPhaseRow(fight, phases);
    }

    // For any phase whose tank busters follow job priority instead of literal
    // MT/OT (see PriorityPhase), a toggle to flip which of you is priority 1
    // when the auto pick (live party job ranking) is wrong or ambiguous.
    private void DrawPriorityPhaseRow(FightProfile fight, List<(string Name, float Time)> phases)
    {
        var priorityOnes = phases
            .Select(p => (p.Name, Phase: TankPriority.PhaseAt(fight.TerritoryId, p.Time)))
            .Where(p => p.Phase != null)
            .ToList();
        if (priorityOnes.Count == 0) return;

        // Rides on the Practice row: both are per-phase, so they read together.
        ImGui.SameLine(0, Theme.S(18f));
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Priority");
        Tip("These phases' tank busters follow job priority (ranked, not MT/OT).\nClick one if the auto pick has you backwards.");
        ImGui.SameLine(0, Theme.S(8f));

        Widgets.SegmentBegin();
        var first = true;
        foreach (var (name, phase) in priorityOnes)
        {
            var swapped = TankPriority.IsSwapped(fight, phase!);
            if (!first) ImGui.SameLine();
            first = false;
            // Amber, not accent: this means "you overrode it", not "selected".
            if (Widgets.Segment($"{name}###priswap{name}", swapped, Theme.Warn))
            {
                TankPriority.SetSwapped(fight, phase!, !swapped);
                Builtin.ReapplyPriority(fight);
                C.Save();
                _plugin.SheetViewWindow.MarkPlanDirty();
            }
            Tip(swapped ? "Swapped. Click to go back to the auto pick." : "Click to swap who gets priority 1 here.");
        }
        Widgets.SegmentEnd();
    }

    // Personal-timer cues the sheet bakes in, like the summoner's pet cycle on
    // Dancing Mad. They belong to one job rather than the party, so they get
    // their own switch instead of being deleted a burst at a time.
    private bool HasPersonalTimers(FightProfile fight, string? job)
        => Builtin.Has(fight.TerritoryId)
           && Builtin.HasHiddenMechanics(fight.TerritoryId)
           && !string.IsNullOrEmpty(fight.Slot)
           && !string.IsNullOrEmpty(job)
           && Builtin.BakedLinesForFight(fight, fight.Slot)
               .Any(b => Builtin.IsHiddenMechanic(fight.TerritoryId, b.Mechanic) && b.AppliesTo(job));

    private bool IsPersonalTimer(FightProfile fight, MitLine l, string job)
        => Builtin.IsHiddenMechanic(fight.TerritoryId, l.Mechanic) && l.AppliesTo(job);

    private void DrawPersonalTimersSection(FightProfile fight)
    {
        var job = _plugin.GetActiveJobAbbr(fight);
        if (!HasPersonalTimers(fight, job)) return;

        var mine = fight.Lines.Where(l => IsPersonalTimer(fight, l, job!)).ToList();
        var baked = Builtin.BakedLinesForFight(fight, fight.Slot)
            .Where(b => IsPersonalTimer(fight, b, job!)).ToList();
        var name = baked.Count > 0 ? baked[0].Mechanic : "Summon";

        // One row: the box is the control, and the box says on or off.
        RowIndent();
        var on = mine.Count > 0;
        if (GreenCheckbox($"{job} {name.ToLowerInvariant()} cues##hidcue", ref on))
        {
            _plugin.SheetViewWindow.PushUndo(fight, on ? $"restore {name} cues" : $"remove {name} cues");
            if (on) RestorePersonalTimers(fight);
            else RemovePersonalTimers(fight, mine, job!);
            _plugin.SheetViewWindow.MarkPlanDirty();
        }
        HelpMarker($"The sheet bakes {job}'s own {name.ToLowerInvariant()} timings in and calls them "
                   + $"like any other line. {baked.Count} of them. Untick if you already know your "
                   + "rotation; they come back if you tick it again.");
    }

    // A tombstone each, so the sheet's top-up cannot put them back.
    private void RemovePersonalTimers(FightProfile fight, List<MitLine> mine, string job)
    {
        foreach (var l in mine)
        {
            if (!l.Custom)
                fight.DeletedCalls.Add(new DeletedCall
                {
                    Slot = fight.Slot,
                    Time = l.Time,
                    Mechanic = l.Mechanic,
                    Action = l.Action,
                });
            fight.Lines.Remove(l);
        }
        SetFightLines(fight, fight.Lines);
        FlashBuiltin($"Removed {mine.Count} {job} cue(s). Tick the box to bring them back.");
    }

    // Lift only these tombstones, so other deletions stay deleted.
    private void RestorePersonalTimers(FightProfile fight)
    {
        fight.DeletedCalls.RemoveAll(d =>
            string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase)
            && Builtin.IsHiddenMechanic(fight.TerritoryId, d.Mechanic));
        var back = Builtin.ApplySlot(fight, fight.Slot);
        C.Save();
        FlashBuiltin($"Restored {back} cue(s) from the sheet.");
    }

    // Potions: baked windows, or the 2-minute meta for customs.
    private void DrawPotionsSection(FightProfile fight)
    {
        var customPots = PotionTimings.BossSlug(fight.TerritoryId) == null
            && fight.CustomSlots.Count > 0 && fight.CustomRows.Count > 0;
        if (PotionTimings.BossSlug(fight.TerritoryId) == null && !customPots) return;

        var job = _plugin.GetActiveJobAbbr(fight);
        var stat = PotionTimings.Stat(job);

        // One row: the flask says potions, the gold times say when, Add acts.
        RowLabelIcon(FontAwesomeIcon.Flask, Theme.Gold);
        Tip(customPots ? "Potions, on the 2-minute burst meta." : "Potions, on the top logs' windows.");

        if (string.IsNullOrEmpty(job) || string.IsNullOrEmpty(stat))
        {
            ImGui.TextDisabled("Pick your job in the sidebar to see potion timings.");
            return;
        }

        var times = customPots
            ? PotionTimings.GenericWindows(fight.CustomRows.Max(r => r.Time))
            : PotionTimings.DefaultsFor(fight.TerritoryId, job);

        // Window pills. The job and its stat are in the tooltip, not the row.
        if (times.Count == 0) ImGui.TextDisabled("no windows");
        var firstPill = true;
        foreach (var t in times)
        {
            if (!firstPill) ImGui.SameLine(0, Theme.S(6f));
            firstPill = false;
            TimePill(Mmss(t));
            Tip($"{job} · {stat}");
        }

        ImGui.SameLine(0, Theme.S(10f));
        ImGui.BeginDisabled(times.Count == 0);
        if (ImGui.SmallButton("Add"))
        {
            var lines = new List<MitLine>(fight.Lines);
            lines.RemoveAll(l => l.Mechanic.StartsWith("Potion", StringComparison.Ordinal)
                                 && l.Jobs.Contains(job, StringComparer.OrdinalIgnoreCase));
            foreach (var t in times)
                lines.Add(new MitLine
                {
                    Time = t,
                    Mechanic = $"Potion ({stat})",
                    Action = "Potion",
                    Jobs = new List<string> { job },
                    Enabled = true,
                    Custom = true,
                });
            SetFightLines(fight, lines.OrderBy(l => l.Time).ToList());
            FlashBuiltin($"Added {times.Count} {job} potion line(s).");
        }
        ImGui.EndDisabled();
        Tip($"Adds {times.Count} line(s), tagged to {job}.");

        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine(0, Theme.S(10f));
            ImGui.TextColored(ImGuiColors.ParsedGreen, _builtinMsg);
        }
    }

    // Job mitigation: optional job timers from logs.
    private void DrawJobExtrasSection(FightProfile fight)
    {
        var job = _plugin.GetActiveJobAbbr(fight);
        if (string.IsNullOrEmpty(job)) return; // also lets the compiler see job is non-null below
        // Baked for built-ins, computed from a custom sheet's rows.
        var extras = JobExtras.AllFor(fight, job);
        if (extras.Count == 0) return;
        var custom = JobExtras.For(fight.TerritoryId, job) == null; // no baked zone schedule -> from the sheet

        BeginCard(FontAwesomeIcon.Shield, ImGuiColors.HealerGreen, "Job extras", "auto-mixed in");
        ImGui.TextDisabled("Already mixed into your line list below, at their own time. Delete one there");
        ImGui.TextDisabled("(or in Sheet View) to drop it for good; these buttons reset it to the default.");
        if (custom)
            ImGui.TextDisabled("Spots picked from this sheet's rows, hardest-graded hits first.");

        foreach (var extra in extras)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.62f, 0.66f, 0.72f, 1f), $"{job} · {extra.Action}");

            ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);

            if (extra.Steps is { Length: > 0 } steps)
            {
                // A sequence extra, where each step is its own action.
                var names = steps.Select(s => s.Summon).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var bursts = new List<List<(int Time, string Summon)>>();
                foreach (var s in steps)
                {
                    if (bursts.Count == 0 || bursts[^1].Count >= 3 || s.Time - bursts[^1][^1].Time > 20f)
                        bursts.Add(new List<(int, string)>());
                    bursts[^1].Add(s);
                }

                void AddSummons(bool grouped)
                {
                    var lines = new List<MitLine>(fight.Lines);
                    lines.RemoveAll(l => l.Jobs.Contains(job, StringComparer.OrdinalIgnoreCase)
                        && names.Any(n => l.Action.Contains(n, StringComparison.OrdinalIgnoreCase)));
                    fight.DeletedCalls.RemoveAll(d => string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase)
                        && names.Any(n => d.Action.Contains(n, StringComparison.OrdinalIgnoreCase)));
                    if (grouped)
                        foreach (var b in bursts)
                            lines.Add(new MitLine
                            {
                                Time = b[0].Time,
                                Action = string.Join(" / ", b.Select(x => x.Summon)),
                                // Spoken with commas, so the burst reads cleanly.
                                Tts = string.Join(", ", b.Select(x => x.Summon)),
                                Jobs = new List<string> { job }, Enabled = true, Custom = true, Sound = true,
                            });
                    else
                        foreach (var (time, summon) in steps)
                            lines.Add(new MitLine
                            {
                                Time = time, Action = summon,
                                Jobs = new List<string> { job }, Enabled = true, Custom = true, Sound = true,
                            });
                    SetFightLines(fight, lines.OrderBy(l => l.Time).ToList());
                    FlashBuiltin($"Reset to {(grouped ? bursts.Count : steps.Length)} {job} summon cue(s).");
                }

                ImGui.SameLine(0, Theme.S(10f));
                ImGui.TextDisabled($"{steps.Length} summons in {bursts.Count} bursts");
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, $"Grouped ({bursts.Count})"))
                    AddSummons(true);
                Tip("Reset to one cue per burst of three (the auto-mixed default).");
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, $"Singles ({steps.Length})"))
                    AddSummons(false);
                Tip($"Switch to one cue per summon. {job} only.");
            }
            else
            {
                ImGui.SameLine(0, Theme.S(10f));
                ImGui.TextDisabled($"{extra.Lines.Length} casts, spaced to its {extra.Recast:0}s recast");
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, $"Reset {extra.Lines.Length} {extra.Action} line(s)"))
                {
                    var lines = new List<MitLine>(fight.Lines);
                    lines.RemoveAll(l => string.Equals(l.Action, extra.Action, StringComparison.OrdinalIgnoreCase)
                                         && l.Jobs.Contains(job, StringComparer.OrdinalIgnoreCase));
                    fight.DeletedCalls.RemoveAll(d => string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(d.Action.Trim(), extra.Action.Trim(), StringComparison.OrdinalIgnoreCase));
                    foreach (var (time, mech) in extra.Lines)
                        lines.Add(new MitLine
                        {
                            Time = time,
                            Mechanic = mech,
                            Action = extra.Action,
                            Jobs = new List<string> { job },
                            Enabled = true,
                            Custom = true,
                        });
                    SetFightLines(fight, lines.OrderBy(l => l.Time).ToList());
                    FlashBuiltin($"Reset {extra.Lines.Length} {job} {extra.Action} line(s) to the default schedule.");
                }
                Tip($"Back to {extra.Action}'s default timing, tagged to {job}. Also un-deletes any you removed.");
            }

            ImGui.PopStyleColor(2);
        }

        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, _builtinMsg);
        }
        EndCard();
    }

    // Rarely-touched actions, folded so the editor opens lean.
    private void DrawAdvancedFightSettings(FightProfile fight)
    {
        if (!Section("Manage & advanced")) return;
        ImGui.Indent(Theme.S(10f));

        var locked = IsOfficial(fight);
        if (!locked)  // a duplicate built-in would be a locked same-zone copy
        {
            if (ImGui.Button("Duplicate"))
            {
                AddFight(new FightProfile
                {
                    Name = fight.Name + " copy",
                    TerritoryId = fight.TerritoryId,
                    Category = fight.Category,
                    TimerOffset = fight.TimerOffset,
                    // The copy starts disabled, or the original keeps the zone.
                    Enabled = false,
                    Slot = fight.Slot,
                    Lines = fight.Lines.Select(CloneLine).ToList(),
                    // Deep-copy the rest, or edits bleed between profiles.
                    SyncPoints = fight.SyncPoints.Select(s => new SyncPoint
                    { Ability = s.Ability, Time = s.Time, IsPhase = s.IsPhase, Label = s.Label }).ToList(),
                    BossAnchors = fight.BossAnchors.Select(b => new BossAnchor
                    { NameId = b.NameId, Time = b.Time, Label = b.Label }).ToList(),
                    DeletedCalls = fight.DeletedCalls.Select(d => new DeletedCall
                    { Slot = d.Slot, Time = d.Time, Mechanic = d.Mechanic, Action = d.Action }).ToList(),
                    SavedSlots = fight.SavedSlots.ToDictionary(
                        kv => kv.Key, kv => kv.Value.Select(CloneLine).ToList()),
                    CustomSlots = fight.CustomSlots.ToList(),
                    CustomRows = fight.CustomRows.Select(cr => new CustomRow
                    { Time = cr.Time, Mechanic = cr.Mechanic }).ToList(),
                    CustomDowntimes = fight.CustomDowntimes.Select(w => new DowntimeWindow
                    { Start = w.Start, Duration = w.Duration, TargetHp = w.TargetHp, Cutscene = w.Cutscene }).ToList(),
                });
            }
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip("The copy starts disabled.");
            ImGui.SameLine();
        }
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Upload, "Export to clipboard")) ExportFight(fight);
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Download, "Import from clipboard")) ImportFightFromClipboard();
        Tip("Share the fight as a clipboard code.");

        ImGui.Spacing();
        ImGui.BeginDisabled(locked); // a built-in's zone is fixed
        var territory = (int)fight.TerritoryId;
        ImGui.SetNextItemWidth(Theme.S(120f));
        // Official zones are refused, since the built-in wins them.
        if (ImGui.InputInt("Territory id", ref territory))
        {
            var target = (uint)Math.Max(0, territory);
            if (Builtin.Has(target) && target != fight.TerritoryId) _zoneRejectUntil = ImGui.GetTime() + 4;
            else { fight.TerritoryId = target; C.Save(); }
        }
        ImGui.SameLine();
        if (ImGui.Button($"Use current zone ({Service.ClientState.TerritoryType})"))
        {
            var target = Service.ClientState.TerritoryType;
            if (Builtin.Has(target) && target != fight.TerritoryId) _zoneRejectUntil = ImGui.GetTime() + 4;
            else { fight.TerritoryId = target; C.Save(); }
        }
        var zoneName = TerritoryName(fight.TerritoryId);
        if (!string.IsNullOrEmpty(zoneName)) { ImGui.SameLine(); ImGui.TextDisabled(zoneName); }
        if (ImGui.GetTime() < _zoneRejectUntil)
            ImGui.TextColored(new Vector4(0.95f, 0.75f, 0.35f, 1f), "That zone already has an official sheet - it can't be assigned to a custom fight.");
        ImGui.EndDisabled();

        ImGui.TextDisabled("Timer offset now lives at the top of this fight, above the mit sections.");

        ImGui.Unindent(Theme.S(10f));
    }

    // Reassign lines while keeping the saved slot copy in sync.
    private void SetFightLines(FightProfile fight, List<MitLine> lines)
    {
        fight.Lines = lines;
        if (!string.IsNullOrEmpty(fight.Slot))
            fight.SavedSlots[fight.Slot] = lines;
        C.Save();
    }
}
