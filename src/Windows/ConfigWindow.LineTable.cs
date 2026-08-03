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

// Settings: the per-call table where a plan is edited.
public partial class ConfigWindow
{
    private string _iconSearch = "";

    class MechanicGroup
    {
        public float Time;
        public string Mechanic = "";
        public bool IsOfficial;
        public List<MitLine> Actions = new();
    }

    private void DrawLineTable(FightProfile fight)
    {
        ImGui.TextUnformatted($"Lines ({fight.Lines.Count})");
        if (ImGui.SmallButton("Add Mechanic"))
        {
            var newLine = new MitLine { Custom = true, Personal = true };
            fight.Lines.Add(newLine);
            fight.Lines = fight.Lines.OrderBy(a => a.Time).ToList();
            _scrollToLine = newLine;
            C.Save();
        }
        ImGui.SameLine();
        var showEmpty = C.ShowEmptyMechanics;
        if (ImGui.Checkbox("Show Mechanics with No Actions", ref showEmpty))
        {
            C.ShowEmptyMechanics = showEmpty;
            C.Save();
        }

        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Mechanics group multiple actions together. Official mechanics cannot be renamed.");

        // Deleted sheet calls are remembered, so offer the way back.
        var dead = fight.DeletedCalls.Count(d => string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase));
        if (dead > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"• {dead} deleted sheet call{(dead == 1 ? "" : "s")}");
            ImGui.SameLine();
            if (ImGui.SmallButton("Restore"))
            {
                fight.DeletedCalls.RemoveAll(d => string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase));
                var back = Builtin.ApplySlot(fight, fight.Slot);
                C.Save();
                FlashBuiltin($"Restored {back} deleted sheet call{(back == 1 ? "" : "s")}.");
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Restore this slot's deleted calls.");
        }

        var jobAbbr = _plugin.GetActiveJobAbbr(fight);
        var bakedForSlotAll = Builtin.BakedLinesForFight(fight, fight.Slot);

        // Grow to fill, leaving room for the import header..
        var avail = ImGui.GetContentRegionAvail().Y;
        var tableH = MathF.Max(200f, avail - ImGui.GetFrameHeightWithSpacing() - 8f);

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;
        if (!ImGui.BeginTable("##lines", 4, flags, new Vector2(0, tableH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("Mechanic", ImGuiTableColumnFlags.WidthFixed, 230);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableHeadersRow();

        MitLine? toDelete = null;
        Action? deferred = null;
        
        // A hidden mechanic names a personal timer (a summoner's pet cycle),
        // not a boss cast, so it belongs to the job that owns it and to nobody
        // else: a normal sheet row for that job, absent entirely for everyone
        // else rather than a mechanic the whole party is missing actions for.
        bool NotOurs(MitLine l)
            => Builtin.IsHiddenMechanic(fight.TerritoryId, l.Mechanic) && !l.AppliesTo(jobAbbr);

        // 1. Gather master list of official mechanics
        var groups = new List<MechanicGroup>();
        var officialMechanics = Builtin.Slots(fight.TerritoryId)
            .SelectMany(s => Builtin.BakedLines(fight.TerritoryId, s))
            .Where(b => !NotOurs(b) && !string.IsNullOrWhiteSpace(b.Mechanic))
            .GroupBy(b => new { Time = MathF.Round(b.Time, 1), Mech = b.Mechanic.Trim().ToLowerInvariant() })
            .Select(grp => grp.First())
            .ToList();
            
        foreach (var o in officialMechanics)
        {
            groups.Add(new MechanicGroup { Time = o.Time, Mechanic = o.Mechanic, IsOfficial = true });
        }
        
        // 2. Merge fight.Lines
        foreach (var line in fight.Lines)
        {
            // No official row was seeded for someone else's personal timer, so
            // merging it here would strand it as a custom mechanic.
            if (NotOurs(line)) continue;

            var match = groups.FirstOrDefault(g => MathF.Abs(g.Time - line.Time) < 0.1f && string.Equals(g.Mechanic.Trim(), line.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                match.Actions.Add(line);
            }
            else
            {
                var cg = groups.FirstOrDefault(g => !g.IsOfficial && MathF.Abs(g.Time - line.Time) < 0.1f && string.Equals(g.Mechanic.Trim(), line.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase));
                if (cg == null)
                {
                    cg = new MechanicGroup { Time = line.Time, Mechanic = line.Mechanic, IsOfficial = false };
                    groups.Add(cg);
                }
                cg.Actions.Add(line);
            }
        }
        
        // 3. Filter empty and sort
        if (!C.ShowEmptyMechanics)
        {
            groups.RemoveAll(g => g.IsOfficial && (g.Actions.Count == 0 || g.Actions.All(a => string.IsNullOrWhiteSpace(a.Action) || (!string.IsNullOrEmpty(jobAbbr) && !a.AppliesTo(jobAbbr)))));
        }
        groups = groups.OrderBy(g => g.Time).ToList();

        for (var g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            
            ImGui.TableNextRow();
            ImGui.PushID(g);

            if (_scrollToLine != null && group.Actions.Contains(_scrollToLine))
            {
                ImGui.SetScrollHereY(0.5f);
                _scrollToLine = null;
            }

            var isOfficial = group.IsOfficial;

            // TIME
            ImGui.TableNextColumn();
            var timeColX = ImGui.GetCursorScreenPos().X;
            
            if (isOfficial)
            {
                ImGui.TextUnformatted(Fmt.MmssSigned(group.Time));
            }
            else
            {
                var repLine = group.Actions.FirstOrDefault();
                if (repLine != null)
                {
                    var timeBuf = _editTimeLine == repLine ? _editTimeBuf : repLine.TimeText;
                    ImGui.SetNextItemWidth(-1);
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFF5C9EF5); // Orange for custom
                    if (ImGui.InputText("##time", ref timeBuf, 12)) _editTimeBuf = timeBuf;
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemActivated()) { _editTimeLine = repLine; _editTimeBuf = repLine.TimeText; }
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        if (_editTimeLine == repLine && SheetImport.TryParseTime(_editTimeBuf, out var sec) && MathF.Abs(sec - repLine.Time) > 0.001f)
                        {
                            foreach (var l in group.Actions)
                            {
                                PreserveBakedEdit(fight, l);
                                l.Time = sec;
                            }
                            deferred = () => { fight.Lines = fight.Lines.OrderBy(a => a.Time).ToList(); _scrollToLine = repLine; C.Save(); };
                        }
                        if (_editTimeLine == repLine) _editTimeLine = null;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Type m:ss (e.g. 2:30) or seconds");
                }
            }

            // MECHANIC
            ImGui.TableNextColumn();
            if (isOfficial)
            {
                ImGui.TextUnformatted(group.Mechanic);
            }
            else
            {
                var repLine = group.Actions.FirstOrDefault();
                if (repLine != null)
                {
                    var mechWidth = ImGui.GetContentRegionAvail().X;
                    ImGui.SetNextItemWidth(mechWidth);
                    var mech = repLine.Mechanic;
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFF5C9EF5); // Orange for custom
                    var mechChanged = ImGui.InputText("##mech", ref mech, 256);
                    ImGui.PopStyleColor();
                    if (mechChanged)
                    {
                        foreach (var l in group.Actions)
                        {
                            PreserveBakedEdit(fight, l);
                            l.Mechanic = mech;
                        }
                        C.Save();
                    }
                }
            }

            // ACTIONS (Pills)
            ImGui.TableNextColumn();
            var style = ImGui.GetStyle();
            var spacing = style.ItemSpacing.X;
            var startX = ImGui.GetCursorPosX();
            var endX = startX + ImGui.GetContentRegionAvail().X;
            var currentX = startX;

            for (var i = 0; i < group.Actions.Count; i++)
            {
                var line = group.Actions[i];
                if (!string.IsNullOrEmpty(jobAbbr) && !line.AppliesTo(jobAbbr)) continue;
                
                ImGui.PushID($"pill_{i}");

                if (_focusNewAction == line)
                {
                    ImGui.OpenPopup($"edit_action_{i}");
                    _focusNewAction = null;
                }


                
                var actionText = line.Action;
                var pillLabel = string.IsNullOrEmpty(actionText) ? "(Empty)" : actionText;
                
                if (line.OffsetSeconds != 0) pillLabel += $" ({line.OffsetSeconds:+0.#;-0.#}s)";
                if (line.Jobs.Count > 0) pillLabel += $" [{string.Join(",", line.Jobs)}]";
                if (!line.Enabled) pillLabel += " (Off)";
                
                var chip = MitTypes.Color(MitTypes.Classify(line.Action, line.Mechanic), C);
                var btnColor = chip != 0 ? (chip & 0x00FFFFFFu) | 0x66000000u : ImGui.GetColorU32(ImGuiCol.Button);
                
                var isAutoExtra = JobExtras.IsAutoExtra(line);
                var actionOverride = (line.Custom && !isAutoExtra) || (DefaultLineFor(fight, line, bakedForSlotAll) is { } d && (!string.Equals(line.Action.Trim(), d.Action.Trim(), StringComparison.OrdinalIgnoreCase) || line.OffsetSeconds != d.OffsetSeconds || !line.Jobs.OrderBy(x=>x).SequenceEqual(d.Jobs.OrderBy(x=>x))));
                var hasConflict = _plugin.SheetViewWindow.HasConflict(fight, line, out var conflictReason);
                
                if (hasConflict) btnColor = ImGui.ColorConvertFloat4ToU32(ImGuiColors.DalamudRed);
                
                ImGui.PushStyleColor(ImGuiCol.Button, btnColor);
                if (actionOverride) ImGui.PushStyleColor(ImGuiCol.Text, 0xFF5C9EF5);
                
                var icon = Icons.For(line, _plugin.GetActiveJobAbbr(fight));
                var hasIcon = icon != 0;
                var h = ImGui.GetFrameHeight();
                var textSz = ImGui.CalcTextSize(pillLabel);
                var pillWidth = textSz.X + style.FramePadding.X * 2 + (hasIcon ? h + 4 : 0);
                
                if (currentX + pillWidth > endX && currentX > startX)
                {
                    currentX = startX;
                    // Wrap manually
                }
                
                if (currentX > startX) ImGui.SameLine(0, spacing);
                
                var btnPos = ImGui.GetCursorScreenPos();
                if (ImGui.Button(hasIcon ? $"##btn_{i}" : pillLabel, new Vector2(pillWidth, h)))
                {
                    ImGui.OpenPopup($"edit_action_{i}");
                }
                
                if (hasIcon)
                {
                    var dl = ImGui.GetWindowDrawList();
                    var p0 = btnPos + new Vector2(style.FramePadding.X, (h - h) / 2);
                    Icons.DrawTo(dl, icon, p0, new Vector2(h, h));
                    
                    var textPos = btnPos + new Vector2(style.FramePadding.X + h + 4, style.FramePadding.Y);
                    dl.AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), pillLabel);
                }
                
                currentX += pillWidth + spacing;
                
                if (actionOverride) ImGui.PopStyleColor();
                ImGui.PopStyleColor();
                
                if (hasConflict && ImGui.IsItemHovered()) ImGui.SetTooltip(conflictReason);

                if (ImGui.BeginPopupContextItem("##actionctx"))
                {
                    LineContextItems(fight, line, fight.Lines.IndexOf(line), ref deferred, ref toDelete);
                    ImGui.EndPopup();
                }

                if (ImGui.BeginPopup($"edit_action_{i}"))
                {
                    var on = line.Enabled;
                    if (GreenCheckbox("Enabled", ref on)) { line.Enabled = on; C.Save(); _plugin.SheetViewWindow.MarkPlanDirty(); }
                    ImGui.Separator();
                    
                    var act = line.Action;
                    if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                    if (ImGui.InputText("Action", ref act, 256))
                    {
                        PreserveBakedEdit(fight, line);

                        line.Action = act;
                        C.Save();
                    }
                    var defForLine = DefaultLineFor(fight, line, bakedForSlotAll);
                    if (defForLine != null && ImGui.BeginPopupContextItem("##actionctx_pop"))
                    {
                        if (!string.Equals(defForLine.Action.Trim(), line.Action.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            if (ImGui.MenuItem($"Reset action to \"{Ellipsis(defForLine.Action, 40)}\"")) { line.Action = defForLine.Action; C.Save(); }
                        }
                        ImGui.EndPopup();
                    }

                    var off = line.OffsetSeconds;
                    ImGui.SetNextItemWidth(120);
                    if (ImGui.InputFloat("Offset (s)", ref off, 0.5f, 1f, "%.1f"))
                    {
                        line.OffsetSeconds = Math.Clamp(off, -30f, 30f);
                        line.OffsetManual = line.OffsetSeconds != 0;
                        C.Save();
                        _plugin.SheetViewWindow.MarkPlanDirty();
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("+ earlier, - later.");

                    ImGui.Spacing();
                    DrawJobsCell(line);
                    
                    ImGui.Spacing();
                    
                    if (ImGui.TreeNode("Advanced Options"))
                    {
                        SeparatorText("Icon");
                        var resolved = Icons.For(line, _plugin.GetActiveJobAbbr(fight));
                        Icons.Draw(resolved, new Vector2(40, 40));
                        ImGui.SameLine();
                        ImGui.BeginGroup();
                        ImGui.TextUnformatted(line.IconId != 0 ? $"pinned (#{line.IconId})"
                            : (resolved != 0 ? "auto (action / status / keyword)" : "none"));
                        if (ImGui.Button("Use auto")) { line.IconId = 0; C.Save(); }
                        ImGui.SameLine();
                        if (ImGui.Button("Potion")) { line.IconId = Icons.PotionIconFor(line); C.Save(); }
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Pin the potion (Gemdraught) icon to this line.");
                        ImGui.EndGroup();

                        ImGui.SetNextItemWidth(-1);
                        ImGui.InputTextWithHint("##iconsearch", "search actions & statuses...", ref _iconSearch, 64);
                        if (!string.IsNullOrWhiteSpace(_iconSearch))
                        {
                            var n = 0;
                            foreach (var (name, ic) in Icons.Search(_iconSearch, 40))
                            {
                                if (Icons.Button(ic, new Vector2(32, 32), $"##s{ic}_{n}")) { line.IconId = ic; C.Save(); }
                                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{name}  (#{ic})");
                                if (++n % 8 != 0) ImGui.SameLine();
                            }
                            ImGui.NewLine();
                        }

                        if (ImGui.TreeNode("Common mechanic icons"))
                        {
                            var n = 0;
                            foreach (var (label, ic) in Icons.Common())
                            {
                                if (Icons.Button(ic, new Vector2(32, 32), $"##c{ic}_{n}")) { line.IconId = ic; C.Save(); }
                                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{label}  (#{ic})");
                                if (++n % 8 != 0) ImGui.SameLine();
                            }
                            ImGui.NewLine();
                            ImGui.TreePop();
                        }

                        SeparatorText("Timing & Audio");
                        
                        var lead = line.LeadOverride;
                        ImGui.SetNextItemWidth(120f);
                        if (ImGui.InputFloat("Show ahead (s)", ref lead, 0.5f, 1f, "%.1f"))
                        {
                            line.LeadOverride = MathF.Max(0f, lead);
                            C.Save();
                        }

                        var tts = line.Tts;
                        ImGui.SetNextItemWidth(220f);
                        if (ImGui.InputText("Speak instead", ref tts, 128)) { line.Tts = tts; C.Save(); }
                        ImGui.TextDisabled("Empty = speak the action.");

                        var sound = line.Sound;
                        if (GreenCheckbox("Play audio cue", ref sound)) { line.Sound = sound; C.Save(); }

                        var useColor = line.Color != 0;
                        if (GreenCheckbox("Custom text color", ref useColor))
                        {
                            line.Color = useColor ? 0xFF55FFFF : 0u;
                            C.Save();
                        }
                        if (line.Color != 0)
                        {
                            var col = ColorToVec4(line.Color);
                            if (ImGui.ColorEdit4("Color", ref col)) { line.Color = Vec4ToColor(col); C.Save(); }
                        }
                        
                        ImGui.TreePop();
                    }
                    
                    ImGui.Separator();
                    if (ImGui.Button("Delete Action", new Vector2(-1, 0)))
                    {
                        toDelete = line;
                        ImGui.CloseCurrentPopup();
                    }
                    if (!line.Custom && defForLine != null)
                    {
                        if (ImGui.Button("Reset to Default", new Vector2(-1, 0)))
                        {
                            OverwriteLine(line, defForLine);
                            line.Custom = false;
                            fight.DeletedCalls.RemoveAll(d => MathF.Abs(d.Time - defForLine.Time) < 0.1f && d.Slot == fight.Slot);
                            C.Save();
                            _plugin.SheetViewWindow.MarkPlanDirty();
                            ImGui.CloseCurrentPopup();
                        }
                    }
                    if (ImGui.MenuItem("Delete this action")) toDelete = line;
                    ImGui.EndPopup();
                }
                
                if (string.IsNullOrWhiteSpace(line.Action) && !ImGui.IsPopupOpen($"edit_action_{i}"))
                {
                    toDelete = line;
                }

                ImGui.PopID();
            }
            
            // Add Action Button
            if (currentX > startX) ImGui.SameLine(0, spacing);
            if (ImGui.Button($"+##add{group.Time}_{group.Mechanic}", new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight())))
            {
                var newLine = new MitLine
                {
                    Time = group.Time,
                    Mechanic = group.Mechanic,
                    Custom = true,
                    Personal = true,
                    Action = ""
                };
                deferred = () => { fight.Lines.Add(newLine); fight.Lines = fight.Lines.OrderBy(a => a.Time).ToList(); _focusNewAction = newLine; C.Save(); };
            }

            ImGui.TableNextColumn();

            if (isOfficial)
            {
                // Job-extra lines (Mantra, Curing Waltz, ...) are merged in from an
                // official schedule, not a personal edit, so they don't count as
                // an override of this mechanic's baked calls. Both sides of the
                // comparison must drop them or the row reads as overridden for
                // nobody's edit: the sheet times an extra onto its mechanic, so
                // the bake carries extras this group's actions never will.
                var bakedForGroup = bakedForSlotAll
                    .Where(b => MathF.Abs(b.Time - group.Time) < 0.1f
                                && string.Equals(b.Mechanic.Trim(), group.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase)
                                && !JobExtras.IsAutoExtra(b))
                    .ToList();

                var validActions = group.Actions.Where(a => !string.IsNullOrWhiteSpace(a.Action) && !JobExtras.IsAutoExtra(a)).ToList();
                var hasOverride = validActions.Count != bakedForGroup.Count;
                if (!hasOverride)
                {
                    foreach (var l in validActions)
                    {
                        var d = bakedForGroup.FirstOrDefault(x => string.Equals(x.Action.Trim(), l.Action.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (d == null || l.Custom || l.OffsetSeconds != d.OffsetSeconds || !l.Jobs.OrderBy(j => j).SequenceEqual(d.Jobs.OrderBy(j => j)) || l.IconId != d.IconId || l.LeadOverride != d.LeadOverride)
                        {
                            hasOverride = true;
                            break;
                        }
                    }
                }
                
                if (hasOverride)
                {
                    bool clicked = false;
                    using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                    {
                        clicked = ImGui.SmallButton(Dalamud.Interface.FontAwesomeIcon.Undo.ToIconString() + "##delgrp");
                    }
                    
                    if (clicked)
                    {
                        deferred = () =>
                        {
                            foreach(var l in group.Actions.Where(a => !JobExtras.IsAutoExtra(a))) { fight.Lines.Remove(l); }
                            foreach(var b in bakedForGroup)
                            {
                                var newLine = CloneLine(b);
                                newLine.Custom = false;
                                fight.Lines.Add(newLine);
                            }
                            fight.Lines = fight.Lines.OrderBy(a => a.Time).ToList();
                            C.Save(); 
                        };
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset this mechanic's actions to default for your slot");
                }
            }
            else
            {
                bool clicked = false;
                using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                {
                    clicked = ImGui.SmallButton(Dalamud.Interface.FontAwesomeIcon.Times.ToIconString() + "##delgrp");
                }
                
                if (clicked)
                {
                    deferred = () => { foreach(var l in group.Actions) toDelete = l; };
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delete this mechanic and all its actions");
            }

            ImGui.PopID();
        }

        ImGui.EndTable();

        deferred?.Invoke();
        if (toDelete != null)
        {
            // Baked lines get a tombstone, so a re-bake can't revive them; a
            // job-extra line (Mantra, Curing Waltz, ...) needs the same, or
            // the auto-mix would just put it right back.
            var isAutoExtra = JobExtras.IsAutoExtra(toDelete);
            if ((!toDelete.Custom || isAutoExtra) && !string.IsNullOrEmpty(fight.Slot))
            {
                fight.DeletedCalls.Add(new DeletedCall
                {
                    Slot = fight.Slot,
                    Time = toDelete.Time,
                    Mechanic = toDelete.Mechanic,
                    Action = toDelete.Action,
                });
                FlashBuiltin(isAutoExtra
                    ? "Job extra deleted. It stays out of the auto-mix; Restore (above the table) brings it back."
                    : "Line deleted. It stays deleted; Restore (above the table) brings it back.");
            }
            fight.Lines.Remove(toDelete);
            // Keep the saved copy in step after a config reload.
            if (!string.IsNullOrEmpty(fight.Slot))
                fight.SavedSlots[fight.Slot] = fight.Lines;
            C.Save();
        }

        if (!ImGui.IsAnyItemActive())
        {
            var isSorted = true;
            for (var i = 1; i < fight.Lines.Count; i++)
                if (fight.Lines[i].Time < fight.Lines[i - 1].Time) { isSorted = false; break; }
            if (!isSorted)
            {
                fight.Lines = fight.Lines.OrderBy(l => l.Time).ToList();
                if (!string.IsNullOrEmpty(fight.Slot)) fight.SavedSlots[fight.Slot] = fight.Lines;
                C.Save();
            }
        }
    }

    // Right-click menu shared by the editable cells.
    private void LineContextItems(FightProfile fight, MitLine line, int index, ref Action? deferred, ref MitLine? toDelete)
    {
        if (ImGui.MenuItem("Copy action")) _copiedLine = CloneLine(line);

        var hasCopy = _copiedLine != null;
        if (ImGui.MenuItem("Paste above", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            var clip = CloneLine(_copiedLine);
            var at = index;
            deferred = () => { fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), clip); C.Save(); };
        }
        if (ImGui.MenuItem("Paste below", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            var clip = CloneLine(_copiedLine);
            var at = index + 1;
            deferred = () => { fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), clip); C.Save(); };
        }
        if (ImGui.MenuItem("Paste over this action", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            PreserveBakedEdit(fight, line); // pasting over rewrites time/mechanic
            OverwriteLine(line, _copiedLine);
            deferred = () => { fight.Lines = fight.Lines.OrderBy(a => a.Time).ToList(); _scrollToLine = line; C.Save(); _plugin.SheetViewWindow.MarkPlanDirty(); };
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Duplicate action"))
        {
            var dup = CloneLine(line);
            var at = index + 1;
            deferred = () => { fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), dup); C.Save(); };
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Delete action")) toDelete = line;
    }

    // An edit breaks a baked line's identity, so tombstone first.
    private static void PreserveBakedEdit(FightProfile fight, MitLine line)
        => Builtin.PreserveEdit(fight, fight.Slot, line);

    // Copy every field of src onto target in place.
    private static void OverwriteLine(MitLine target, MitLine src)
    {
        target.Time = src.Time;
        target.Mechanic = src.Mechanic;
        target.Action = src.Action;
        target.Jobs = new List<string>(src.Jobs);
        target.Enabled = src.Enabled;
        target.LeadOverride = src.LeadOverride;
        target.OffsetSeconds = src.OffsetSeconds;
        target.OffsetManual = src.OffsetManual; // carry the hand-set flag so the copy stays protected
        target.CoverUntil = src.CoverUntil;
        target.Tts = src.Tts;
        target.Sound = src.Sound;
        target.Color = src.Color;
        target.IconId = src.IconId;
    }

    private void DrawJobsCell(MitLine line)
    {
        var label = line.Jobs.Count == 0 ? "All Jobs" : string.Join(",", line.Jobs);
        if (label.Length > 18) label = label[..16] + "...";
        if (ImGui.Button(label + "##jobs", new Vector2(-1, 0)))
            ImGui.OpenPopup("jobspopup");

        if (ImGui.BeginPopup("jobspopup"))
        {
            if (ImGui.Button("All jobs")) { line.Jobs.Clear(); C.Save(); }

            foreach (var role in Enum.GetValues<JobRole>())
            {
                SeparatorText(RoleLabel(role));
                var first = true;
                foreach (var abbr in Jobs.AbbreviationsForRole(role))
                {
                    if (!first) ImGui.SameLine();
                    first = false;
                    var has = line.Jobs.Contains(abbr, StringComparer.OrdinalIgnoreCase);
                    if (GreenCheckbox(abbr, ref has))
                    {
                        if (has && !line.Jobs.Contains(abbr)) line.Jobs.Add(abbr);
                        else line.Jobs.RemoveAll(j => string.Equals(j, abbr, StringComparison.OrdinalIgnoreCase));
                        C.Save();
                    }
                }
                ImGui.SameLine();
                if (ImGui.SmallButton($"+all##{role}"))
                {
                    foreach (var abbr in Jobs.AbbreviationsForRole(role))
                        if (!line.Jobs.Contains(abbr)) line.Jobs.Add(abbr);
                    C.Save();
                }
            }
            ImGui.EndPopup();
        }
    }
}
