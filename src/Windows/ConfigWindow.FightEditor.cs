using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

// Settings: one fight's editor - its cards for tank busters, sheet columns,
// practice, potions, job extras and the advanced options.
public partial class ConfigWindow
{
    private bool DrawFightEditor(FightProfile fight)
    {
        // Built-in fights (the ones shipped with the plugin) are locked, so only
        // user-added fights can be renamed or deleted.
        if (IsOfficial(fight))
        {
            ImGui.AlignTextToFramePadding();
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                ImGui.TextColored(GoldStar, FontAwesomeIcon.Star.ToIconString());
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Official sheet.");
            ImGui.SameLine(0, 5);
            ImGui.TextUnformatted(fight.Name);
            ImGui.SameLine(0, 8);
            ImGui.TextDisabled("(official sheet)");
            Tip("Line times are seconds from the pull, one continuous timeline across every phase; resets on a wipe.");
            return true;
        }

        var name = fight.Name;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputText("Name", ref name, 128)) { fight.Name = name; C.Save(); }
        Tip("Line times are seconds from the pull, one continuous timeline across every phase; resets on a wipe.");

        var ci = Array.IndexOf(FightTypes, fight.Category);
        if (ci < 0) ci = FightTypes.Length - 1;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.Combo("Type", ref ci, FightTypes, FightTypes.Length))
        {
            fight.Category = FightTypes[ci];
            C.Save();
        }
        Tip("Ultimate / Savage / Extreme: which sidebar group this fight files under.");

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF2A2AB0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF3A3AC8);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.TrashAlt, "Delete"))
            ImGui.OpenPopup("##delfight");
        ImGui.PopStyleColor(2);
        return !DrawDeleteFightConfirm(fight);
    }

    // Deleting a fight is the most destructive click in the plugin (a custom
    // sheet can be hours of work), so it confirms and snapshots first.
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
        if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF2222C8);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF3333DD);
        if (ImGui.Button("Delete", new Vector2(120, 0)))
        {
            _plugin.Snapshots.Save(fight, "before delete");
            confirmed = true;
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndPopup();
        return confirmed;
    }

    // Tank slots across every fight's slot list (MT/OT, or FRU's T1/T2).
    private static readonly string[] TankSlots = { "MT", "OT", "T1", "T2" };
    private static bool IsTankSlot(string? slot)
        => slot != null && TankSlots.Contains(slot, StringComparer.OrdinalIgnoreCase);

    // Tank-buster mit plan from the fight's sheet (pick your pairing, add your
    // job's lines), shown only for fights that have tank-combo data when you're set
    // to a tank slot (MT/OT/T1/T2), since it's irrelevant on any other role.
    private void DrawTankSection(FightProfile fight)
    {
        if (!TankMits.Has(fight.TerritoryId)) return;
        if (!IsTankSlot(fight.Slot)) return;
        // Check BEFORE BeginCard: returning between Begin/EndCard would leak the
        // draw-list channel split + indent and corrupt the next card this frame.
        var comps = TankMits.Comps(fight.TerritoryId);
        if (comps.Length == 0) return;

        // FRU's tank tabs come from its own sheet, not the Ikuya set the rest do.
        var source = fight.TerritoryId == Builtin.FruTerritory ? "from the FRU sheet" : "from Ikuya";
        BeginCard(FontAwesomeIcon.ShieldAlt, ImGuiColors.TankBlue, "Tank busters", source);
        ImGui.TextDisabled("Pick your tank pairing, then add your job's tank-buster mit plan. Re-adding replaces it.");
        // The pick is stored on the fight profile so it's remembered per fight
        // across sessions (and per character config).
        var tankComp = Array.IndexOf(comps, fight.TankPairing);
        if (tankComp < 0) tankComp = 0;
        ImGui.SetNextItemWidth(140f);
        if (ImGui.Combo("Tank pairing", ref tankComp, comps, comps.Length))
        {
            fight.TankPairing = comps[tankComp];
            C.Save();
        }

        var comp = comps[tankComp];
        var myJob = _plugin.ActiveJobAbbreviation();
        foreach (var j in TankMits.Jobs(comp))
        {
            var entries = TankMits.For(fight.TerritoryId, comp, j);
            ImGui.SameLine();
            var label = j == myJob ? $"Add {j} (yours)" : $"Add {j}";
            if (ImGui.Button($"{label}##tank{j}"))
            {
                var merged = new List<MitLine>(fight.Lines);
                // Replace any existing tank lines for this job, then add fresh.
                merged.RemoveAll(l => l.Mechanic.StartsWith("Tank:", StringComparison.Ordinal)
                                      && l.Jobs.Contains(j, StringComparer.OrdinalIgnoreCase));
                foreach (var e in entries)
                    merged.Add(new MitLine
                    {
                        Time = e.Time,
                        Mechanic = $"Tank: {e.Mechanic}",
                        Action = e.Action,
                        Jobs = new List<string> { j },
                        Enabled = true,
                        Custom = true,
                    });
                SetFightLines(fight, merged.OrderBy(l => l.Time).ToList());
                FlashBuiltin($"Added {entries.Length} {j} tank-buster line(s).");
            }
        }
        ImGui.TextDisabled("Lines are tagged to the job, so they only show when you're on it.");
        EndCard();
    }

    private Vector2 _cardTopLeft;
    private float _cardWidth;

    // Begin an auto-height styled card (panel background + left accent bar + an
    // icon title, drawn behind the content via draw-list channels so the panel fits
    // whatever's inside); every BeginCard must be paired with EndCard.
    private void BeginCard(FontAwesomeIcon icon, Vector4 iconColor, string title, string subtitle = "")
    {
        ImGui.Spacing();
        _cardTopLeft = ImGui.GetCursorScreenPos();
        _cardWidth = ImGui.GetContentRegionAvail().X;

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1); // content on the foreground channel

        ImGui.Indent(12f);
        ImGui.Dummy(new Vector2(0, 6));
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            ImGui.TextColored(iconColor, icon.ToIconString());
        ImGui.SameLine(0, 8);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(title);
        if (!string.IsNullOrEmpty(subtitle))
        {
            ImGui.SameLine(0, 10);
            ImGui.TextColored(new Vector4(0.55f, 0.59f, 0.66f, 1f), subtitle);
        }
        ImGui.Spacing();
    }

    private void EndCard()
    {
        ImGui.Dummy(new Vector2(0, 8));
        ImGui.Unindent(12f);

        var dl = ImGui.GetWindowDrawList();
        var min = _cardTopLeft;
        var max = new Vector2(_cardTopLeft.X + _cardWidth, ImGui.GetCursorScreenPos().Y);
        dl.ChannelsSetCurrent(0); // background channel
        dl.AddRectFilled(min, max, Theme.PanelBg, 8f);
        dl.AddRectFilled(min + new Vector2(0, 8), new Vector2(min.X + 3, max.Y - 8), Theme.Accent);
        dl.ChannelsMerge();
        ImGui.Spacing();
    }

    // A rounded "pill" showing one potion window (mm:ss).
    private static void TimePill(string text)
    {
        var dl = ImGui.GetWindowDrawList();
        var pad = new Vector2(8, 3);
        var sz = ImGui.CalcTextSize(text);
        var p = ImGui.GetCursorScreenPos();
        var box = sz + pad * 2;
        dl.AddRectFilled(p, p + box, 0xFF2A2017, 6f);
        dl.AddRect(p, p + box, Theme.Accent, 6f);
        dl.AddText(p + pad, 0xFFECE8E6, text);
        ImGui.Dummy(box);
    }

    private static string Mmss(float t) => Fmt.MmssFloor(t);

    // Custom sheets: the same "Your slot" row the built-in fights get, so a
    // custom fight reads exactly like an official one on its page.
    private void DrawCustomColumnRow(FightProfile fight)
    {
        var slots = fight.CustomSlots.ToArray();
        if (slots.Length == 0) return;
        var idx = Array.FindIndex(slots, s => string.Equals(s, fight.Slot, StringComparison.OrdinalIgnoreCase));

        ImGui.SetNextItemWidth(170f);
        // idx -1 (no column picked yet) shows an empty preview until they pick.
        if (ImGui.Combo("Your slot##customslot", ref idx, slots, slots.Length)
            && idx >= 0 && !string.Equals(slots[idx], fight.Slot, StringComparison.OrdinalIgnoreCase))
        {
            // SetSlot parks the old column's lines and gives a never-picked
            // column a FRESH list; assigning fight.Lines here instead would
            // alias two columns to one list.
            _plugin.SetSlot(fight, slots[idx]);
            _plugin.SheetViewWindow.MarkPlanDirty();
        }
        Tip("Which column of this sheet is YOURS; that column's lines are what the overlay calls.");
        var slot = idx >= 0 ? slots[idx] : slots[0];

        ImGui.SameLine();
        if (ImGui.SmallButton("Reset this column")) ImGui.OpenPopup("##confirm-customreset");
        Tip("Empties this column's mits. The rows, grades and notes stay; a snapshot is saved first.");

        ImGui.SameLine();
        if (ImGui.SmallButton("Reset all columns")) ImGui.OpenPopup("##confirm-customresetall");
        Tip("Empties EVERY column's mits; rows, grades and notes stay. A snapshot is saved first, "
            + "so Sheet View > Plan > History can restore the old plan.");

        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudYellow, _builtinMsg);
        }

        DrawCustomResetConfirm(fight, slot);
        DrawCustomResetAllConfirm(fight);
    }

    private void ClearCustomColumn(FightProfile fight, string slot)
    {
        // Clear IN PLACE: Sheet View and SavedSlots share these list objects.
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

        if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF1E40C0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF2046D0);
        if (ImGui.Button("Empty this column", new Vector2(160, 0)))
        {
            _plugin.Snapshots.Save(fight, $"before reset {slot}");
            ClearCustomColumn(fight, slot);
            C.Save();
            _plugin.SheetViewWindow.MarkPlanDirty();
            FlashBuiltin($"{slot} emptied. History restores the old plan.");
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
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

        if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF1E40C0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF2046D0);
        if (ImGui.Button("Empty every column", new Vector2(170, 0)))
        {
            _plugin.Snapshots.Save(fight, "before reset all columns");
            fight.Lines.Clear();
            foreach (var saved in fight.SavedSlots.Values) saved.Clear();
            C.Save();
            _plugin.SheetViewWindow.MarkPlanDirty();
            FlashBuiltin("Every column emptied. History restores the old plan.");
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndPopup();
    }

    private int _pracRowIdx;
    private readonly Dictionary<string, int> _pracRowIdxs = new(); // per fight; headers can co-exist

    // Practice, contextual: one row of phase-jump buttons inside the fight it
    // belongs to (the old Practice page, dissolved), with custom sheets (having no
    // baked phases) practicing from any of their own rows instead.
    private void DrawPracticeRow(FightProfile fight)
    {
        var phases = Builtin.PhaseStarts(fight.TerritoryId);
        if (phases.Count == 0)
        {
            var rows = fight.CustomRows.OrderBy(r => r.Time).ToList();
            if (rows.Count == 0) return;
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("Practice:");
            Tip("Jump the overlay to a row to preview and place its calls; no pull needed.\nPicking a row turns on Test Mode; Stop (or a real pull) ends it.");
            _pracRowIdx = Math.Clamp(_pracRowIdxs.GetValueOrDefault(fight.Id), 0, rows.Count - 1);
            var labels = rows.Select(r => $"{Mmss(r.Time)}  {r.Mechanic}").ToArray();
            ImGui.SameLine(0, 6);
            ImGui.SetNextItemWidth(240f);
            ImGui.Combo("##pracrow", ref _pracRowIdx, labels, labels.Length);
            _pracRowIdxs[fight.Id] = _pracRowIdx;
            ImGui.SameLine(0, 4);
            if (ImGui.SmallButton("Go##pracrow")) _plugin.PracticeJump(fight, rows[_pracRowIdx].Time);
            if (Plugin.PreviewFight == fight && C.TestMode)
            {
                ImGui.SameLine(0, 8);
                if (ImGui.SmallButton("Stop##pracrow")) _plugin.StopPractice();
            }
            return;
        }

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Practice:");
        Tip("Jump the overlay to a phase to preview and place its calls; no pull needed.\nPicking a phase turns on Test Mode; Stop (or a real pull) ends it.");
        for (var i = 0; i < phases.Count; i++)
        {
            ImGui.SameLine(0, 4);
            if (ImGui.SmallButton($"{phases[i].Name}##prac{i}"))
                _plugin.PracticeJump(fight, phases[i].Time);
            Tip($"Preview from {(int)phases[i].Time / 60}:{(int)phases[i].Time % 60:00} (~6s before the first call).");
        }
        if (Plugin.PreviewFight == fight && C.TestMode)
        {
            ImGui.SameLine(0, 8);
            if (ImGui.SmallButton("Stop##prac")) _plugin.StopPractice();
            ImGui.SameLine(0, 6);
            ImGui.TextColored(ImGuiColors.DalamudYellow, "previewing");
        }
    }

    // Potions card: baked top-log potion windows for your job with a one-click add,
    // or the standard 2-minute burst meta for custom sheets (pot the opener, re-pot
    // each 6:00 burst that fits the fight).
    private void DrawPotionsSection(FightProfile fight)
    {
        var customPots = PotionTimings.BossSlug(fight.TerritoryId) == null
            && fight.CustomSlots.Count > 0 && fight.CustomRows.Count > 0;
        if (PotionTimings.BossSlug(fight.TerritoryId) == null && !customPots) return;

        var job = _plugin.ActiveJobAbbreviation();
        var stat = PotionTimings.Stat(job);

        BeginCard(FontAwesomeIcon.Flask, ImGuiColors.DalamudViolet, "Potions",
            customPots ? "2-minute burst meta" : "top-log windows");

        if (string.IsNullOrEmpty(job) || string.IsNullOrEmpty(stat))
        {
            ImGui.TextDisabled("Pick your job (top of the sidebar) to see its potion timings.");
            EndCard();
            return;
        }

        var times = customPots
            ? PotionTimings.GenericWindows(fight.CustomRows.Max(r => r.Time))
            : PotionTimings.DefaultsFor(fight.TerritoryId, job);

        // Window pills.
        ImGui.TextColored(new Vector4(0.62f, 0.66f, 0.72f, 1f), $"{job} · {stat}");
        if (times.Count == 0) { ImGui.SameLine(0, 10); ImGui.TextDisabled("no windows"); }
        foreach (var t in times)
        {
            ImGui.SameLine(0, 6);
            TimePill(Mmss(t));
        }

        // Add to the timeline.
        ImGui.Spacing();
        ImGui.BeginDisabled(times.Count == 0);
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, $"Add {times.Count} potion line(s)"))
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
        ImGui.PopStyleColor(2);
        ImGui.EndDisabled();
        Tip("Adds these as job-tagged lines (replacing any existing potion lines for this job), so they only show when you're on it.");

        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, _builtinMsg);
        }
        EndCard();
    }

    // Job-mitigation card: optional job-specific mit timers from logs (Asylum-style,
    // e.g. BRD Nature's Minne, MNK Mantra, PLD Passage of Arms), shown only when
    // you're on a job that has one for this fight.
    private void DrawJobExtrasSection(FightProfile fight)
    {
        var job = _plugin.ActiveJobAbbreviation();
        if (string.IsNullOrEmpty(job)) return; // also lets the compiler see job is non-null below
        // Baked schedule(s) for built-ins, or computed from a custom sheet's own
        // rows (hardest-graded hits first); a job may offer several (e.g. DNC's
        // Curing Waltz + Improvisation), optional either way like the Ikuya sheets'
        // Extras column.
        var extras = JobExtras.AllFor(fight, job);
        if (extras.Count == 0) return;
        var custom = JobExtras.For(fight.TerritoryId, job) == null; // no baked zone schedule -> from the sheet

        BeginCard(FontAwesomeIcon.Shield, ImGuiColors.HealerGreen, "Job extras", "optional");
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
                // Sequence extra (SMN summons): each step is its own action, and
                // bursts group consecutive summons (up to 3, split on a >20s gap) so
                // it can be added as one spoken cue per burst ("Garuda / Titan /
                // Ifrit") or one per summon.
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
                    if (grouped)
                        foreach (var b in bursts)
                            lines.Add(new MitLine
                            {
                                Time = b[0].Time,
                                Action = string.Join(" / ", b.Select(x => x.Summon)),
                                // Spoken with commas so the burst reads cleanly ("Garuda,
                                // Titan, Ifrit") instead of the slashes in the label.
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
                    FlashBuiltin($"Added {(grouped ? bursts.Count : steps.Length)} {job} summon cue(s).");
                }

                ImGui.SameLine(0, 10);
                ImGui.TextDisabled($"{steps.Length} summons in {bursts.Count} bursts");
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, $"Grouped ({bursts.Count})"))
                    AddSummons(true);
                Tip("One cue per burst of three (\"Garuda / Titan / Ifrit\") - the fewest lines.");
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, $"Singles ({steps.Length})"))
                    AddSummons(false);
                Tip($"One cue per summon. Either replaces any existing summon cues and is {job}-only.");
            }
            else
            {
                ImGui.SameLine(0, 10);
                ImGui.TextDisabled($"{extra.Lines.Length} casts, spaced to its {extra.Recast:0}s recast");
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, $"Add {extra.Lines.Length} {extra.Action} line(s)"))
                {
                    var lines = new List<MitLine>(fight.Lines);
                    lines.RemoveAll(l => string.Equals(l.Action, extra.Action, StringComparison.OrdinalIgnoreCase)
                                         && l.Jobs.Contains(job, StringComparer.OrdinalIgnoreCase));
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
                    FlashBuiltin($"Added {extra.Lines.Length} {job} {extra.Action} line(s).");
                }
                Tip($"Adds {extra.Action} as {job}-tagged lines (replacing any existing ones), so they only show on {job}.");
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

    // Rarely-touched share / duplicate actions + zone & timing knobs, behind a
    // collapsing header so the editor opens lean.
    private void DrawAdvancedFightSettings(FightProfile fight)
    {
        if (!Section("Manage & advanced")) return;
        ImGui.Indent(10f);

        var locked = IsOfficial(fight);
        if (!locked)  // duplicating a built-in would make a same-zone copy that's then locked
        {
            if (ImGui.Button("Duplicate"))
            {
                AddFight(new FightProfile
                {
                    Name = fight.Name + " copy",
                    TerritoryId = fight.TerritoryId,
                    Category = fight.Category,
                    TimerOffset = fight.TimerOffset,
                    // The copy starts disabled because with both live the original
                    // would keep winning the zone and edits to the copy would
                    // silently never fire.
                    Enabled = false,
                    Slot = fight.Slot,
                    Lines = fight.Lines.Select(CloneLine).ToList(),
                    // Deep-copy the rest too: for a custom fight the hand-built
                    // anchors are its most laborious data, and sharing object
                    // references between two profiles would make edits bleed over.
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
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The copy starts disabled - enable whichever version should be live.");
            ImGui.SameLine();
        }
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Upload, "Export to clipboard")) ExportFight(fight);
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Download, "Import from clipboard")) ImportFightFromClipboard();
        Tip("Share a whole fight (lines included) with a friend via a clipboard code.");

        ImGui.Spacing();
        ImGui.BeginDisabled(locked); // a built-in's zone is fixed
        var territory = (int)fight.TerritoryId;
        ImGui.SetNextItemWidth(120f);
        // Official-sheet zones are refused here: pointing a custom fight at one
        // creates a duplicate that never fires (the built-in wins the zone).
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

        ImGui.Unindent(10f);
    }

    // Reassign a fight's lines while keeping the active slot's saved copy in sync,
    // so per-slot storage never goes stale after a sort / import.
    private void SetFightLines(FightProfile fight, List<MitLine> lines)
    {
        fight.Lines = lines;
        if (!string.IsNullOrEmpty(fight.Slot))
            fight.SavedSlots[fight.Slot] = lines;
        C.Save();
    }
}
