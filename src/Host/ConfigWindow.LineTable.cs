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

// Settings: the per-call table where a plan is edited.
public partial class ConfigWindow
{
    // Armed when an editor opens or a rename starts, spent on the first real
    // change, so a session of typing is one undo entry.
    private bool _lineEditUndoArmed;
    private MitLine? _mechUndoArmed;

    private static string Ellipsis(string s, int max) => s.Length > max ? s[..max] + "..." : s;

    class MechanicGroup
    {
        public float Time;
        public string Mechanic = "";
        public bool IsOfficial;
        public List<MitLine> Actions = new();
    }

    // Narrow the table to mechanics holding a cooldown clash.
    private bool _lineClashOnly;

    // Right side of the lines toolbar: what is recoverable, then what you can do.
    private void DrawLineToolbarActions(FightProfile fight, Action<string> undoable)
    {
        var style = ImGui.GetStyle();
        float BtnW(string s) => ImGui.CalcTextSize(s).X + style.FramePadding.X * 2f;

        // Deleted sheet calls are remembered, so offer the way back.
        var dead = fight.DeletedCalls.Count(d => string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase));
        var undoLabel = _plugin.SheetViewWindow.UndoLabelFor(fight);

        var right = BtnW("View") + BtnW("Add") + BtnW("Undo") + style.ItemSpacing.X * 2f;
        if (dead > 0) right += ImGui.CalcTextSize($"{dead} deleted").X + BtnW("Restore") + style.ItemSpacing.X * 2f;
        var end = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
        ImGui.SameLine(MathF.Max(end + Theme.S(12f), ImGui.GetContentRegionMax().X - right));

        if (dead > 0)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled($"{dead} deleted");
            ImGui.SameLine(0, Theme.S(6f));
            if (ImGui.SmallButton("Restore"))
            {
                undoable("restore deleted calls");
                fight.DeletedCalls.RemoveAll(d => string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase));
                var back = Builtin.ApplySlot(fight, fight.Slot);
                C.Save();
                FlashBuiltin($"Restored {back} deleted sheet call{(back == 1 ? "" : "s")}.");
            }
            Tip("Put this slot's deleted calls back.");
            ImGui.SameLine(0, Theme.S(10f));
        }

        // Display toggles behind a menu, the way Sheet View already does it.
        if (ImGui.SmallButton("View")) ImGui.OpenPopup("##lineview");
        Tip("What the table shows.");
        if (ImGui.BeginPopup("##lineview"))
        {
            var showEmpty = C.ShowEmptyMechanics;
            if (ImGui.MenuItem("Show Empty Mechanics", "", ref showEmpty))
            {
                C.ShowEmptyMechanics = showEmpty;
                C.Save();
            }
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip("Mechanics with no mit assigned, as blank reference rows.");
            ImGui.EndPopup();
        }

        ImGui.SameLine(0, Theme.S(6f));
        if (ImGui.SmallButton("Add"))
        {
            undoable("add a mechanic");
            var newLine = new MitLine { Custom = true, Personal = true };
            fight.Lines.Add(newLine);
            fight.Lines = fight.Lines.OrderBy(a => a.Time).ToList();
            _scrollToLine = newLine;
            C.Save();
        }
        Tip("Add a mechanic. Mechanics group actions together.\nOfficial ones cannot be renamed.");

        ImGui.SameLine(0, Theme.S(6f));
        ImGui.BeginDisabled(undoLabel == null);
        if (ImGui.SmallButton("Undo") && _plugin.SheetViewWindow.UndoFor(fight) is { } undone)
            FlashBuiltin($"Undid: {undone}.");
        ImGui.EndDisabled();
        if (Widgets.HoveredDelayed(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(undoLabel == null
                ? "Nothing to undo on this fight yet."
                : $"Undo: {undoLabel}. Shared with Sheet View, so it takes back edits made there too.");
    }

    private void DrawLineTable(FightProfile fight)
    {
        // One shared stack with Sheet View, so either page takes back the
        // other's edits and there is no second history to reason about.
        void Undoable(string label) => _plugin.SheetViewWindow.PushUndo(fight, label);

        var jobAbbr = _plugin.GetActiveJobAbbr(fight);
        var bakedForSlotAll = Builtin.BakedLinesForFight(fight, fight.Slot);

        var toDelete = new List<MitLine>();
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

        // Counted over the groups the table will actually draw, not over every
        // line: a hidden mechanic or a hidden-empty row would make the chip
        // promise rows the filter then has nothing to show.
        bool GroupClashes(MechanicGroup g)
            => g.Actions.Any(a => _plugin.SheetViewWindow.HasConflict(fight, a, out _));
        var clashGroups = groups.Count(GroupClashes);
        if (_lineClashOnly) groups.RemoveAll(g => !GroupClashes(g));

        // One toolbar: what the list is on the left, what you can do on the right.
        ImGui.Spacing();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Lines");
        ImGui.SameLine(0, Theme.S(8f));
        Widgets.Chip("", fight.Lines.Count.ToString(), Theme.TextBright);
        if (clashGroups > 0 || _lineClashOnly)
        {
            ImGui.SameLine(0, Theme.S(6f));
            if (Widgets.ChipButton("Clashes", clashGroups.ToString(), Theme.Danger, _lineClashOnly))
                _lineClashOnly = !_lineClashOnly;
            // Names the slot, since Sheet View's chip counts every column and
            // the two numbers are meant to differ.
            var scope = string.IsNullOrEmpty(fight.Slot) ? "your plan" : fight.Slot;
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip(_lineClashOnly
                    ? $"Showing only mechanics with a clash in {scope}. Click to show them all."
                    : $"Mechanics where a mit repeats before its cooldown is back, in {scope}.\nClick to show only those.");
        }

        DrawLineToolbarActions(fight, Undoable);

        // Grow to fill, leaving room for the import header..
        var avail = ImGui.GetContentRegionAvail().Y;
        var tableH = MathF.Max(200f, avail - ImGui.GetFrameHeightWithSpacing() - 8f);

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;
        if (!ImGui.BeginTable("##lines", 4, flags, new Vector2(0, tableH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, Theme.S(70f));
        ImGui.TableSetupColumn("Mechanic", ImGuiTableColumnFlags.WidthFixed, Theme.S(230f));
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, Theme.S(28f));
        ImGui.TableHeadersRow();

        // Every clash fixed while the filter is up: say so in the stretched
        // column, or the message clips inside the fixed-width Mechanic one.
        if (_lineClashOnly && groups.Count == 0)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.TextDisabled("No clashes left. Click the chip above to show every mechanic.");
        }

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
                            Undoable($"re-time \"{group.Mechanic}\"");
                            foreach (var l in group.Actions)
                            {
                                PreserveBakedEdit(fight, l);
                                l.Time = sec;
                            }
                            deferred = () => { fight.Lines = fight.Lines.OrderBy(a => a.Time).ToList(); _scrollToLine = repLine; C.Save(); };
                        }
                        if (_editTimeLine == repLine) _editTimeLine = null;
                    }
                    if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Type m:ss (e.g. 2:30) or seconds");
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
                    // Armed on focus, spent on the first keystroke: one entry
                    // for the rename, not one per letter.
                    if (ImGui.IsItemActivated()) _mechUndoArmed = repLine;
                    if (mechChanged)
                    {
                        if (_mechUndoArmed == repLine) { Undoable($"rename \"{group.Mechanic}\""); _mechUndoArmed = null; }
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
                
                var chip = MitColors.Color(MitTypes.Classify(line.Action, line.Mechanic), C);
                var btnColor = chip != 0 ? (chip & 0x00FFFFFFu) | 0x66000000u : ImGui.GetColorU32(ImGuiCol.Button);
                
                var isAutoExtra = JobExtras.IsAutoExtra(line);
                var actionOverride = (line.Custom && !isAutoExtra) || (DefaultLineFor(fight, line, bakedForSlotAll) is { } d && (!string.Equals(line.Action.Trim(), d.Action.Trim(), StringComparison.OrdinalIgnoreCase) || line.OffsetSeconds != d.OffsetSeconds || !line.Jobs.OrderBy(x=>x).SequenceEqual(d.Jobs.OrderBy(x=>x))));
                var hasConflict = _plugin.SheetViewWindow.HasConflict(fight, line, out var conflictReason);
                
                if (hasConflict) btnColor = Theme.Danger;
                
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
                
                if (hasConflict && Widgets.HoveredDelayed()) ImGui.SetTooltip(conflictReason);

                if (ImGui.BeginPopupContextItem("##actionctx"))
                {
                    LineContextItems(fight, line, fight.Lines.IndexOf(line), Undoable, ref deferred, toDelete);
                    ImGui.EndPopup();
                }

                if (ImGui.BeginPopup($"edit_action_{i}"))
                {
                    // One undo entry per opening, not per keystroke.
                    if (ImGui.IsWindowAppearing()) _lineEditUndoArmed = true;
                    var defForLine = DefaultLineFor(fight, line, bakedForSlotAll);
                    var target = line;
                    var named = string.IsNullOrWhiteSpace(line.Action) ? group.Mechanic : line.Action;
                    void ArmedUndo(string verb)
                    {
                        if (!_lineEditUndoArmed) return;
                        Undoable($"{verb} \"{Ellipsis(named, 28)}\"");
                        _lineEditUndoArmed = false;
                    }

                    Action? reset = null;
                    if (!line.Custom && defForLine is { } baked)
                        reset = () =>
                        {
                            ArmedUndo("reset");
                            OverwriteLine(target, baked);
                            target.Custom = false;
                            fight.DeletedCalls.RemoveAll(d => MathF.Abs(d.Time - baked.Time) < 0.1f && d.Slot == fight.Slot);
                            C.Save();
                            _plugin.SheetViewWindow.MarkPlanDirty();
                        };

                    MitLineEditor.Draw(line, C, new MitLineEditor.Hooks
                    {
                        // Only a rewrite tombstones the sheet's call.
                        BeforeEdit = (l, rewrite) => { ArmedUndo("edit"); if (rewrite) PreserveBakedEdit(fight, l); },
                        Save = () => { C.Save(); _plugin.SheetViewWindow.MarkPlanDirty(); },
                        Delete = () => { ArmedUndo("delete"); toDelete.Add(target); },
                        Default = defForLine,
                        Job = _plugin.GetActiveJobAbbr(fight),
                        Context = $"{Fmt.MmssSigned(group.Time)}  ·  {group.Mechanic}",
                        Reset = reset,
                    });
                    ImGui.EndPopup();
                }
                
                if (string.IsNullOrWhiteSpace(line.Action) && !ImGui.IsPopupOpen($"edit_action_{i}"))
                {
                    toDelete.Add(line);
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
                deferred = () =>
                {
                    Undoable($"add a call to \"{group.Mechanic}\"");
                    fight.Lines.Add(newLine);
                    fight.Lines = fight.Lines.OrderBy(a => a.Time).ToList();
                    _focusNewAction = newLine;
                    C.Save();
                };
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
                            Undoable($"reset \"{group.Mechanic}\"");
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
                    if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Reset this mechanic's actions to default for your slot");
                }
            }
            else
            {
                bool clicked = false;
                Widgets.PushDangerOutline();
                using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                {
                    clicked = ImGui.SmallButton(Dalamud.Interface.FontAwesomeIcon.Times.ToIconString() + "##delgrp");
                }
                Widgets.PopDanger();
                
                if (clicked)
                {
                    deferred = () =>
                    {
                        Undoable($"delete \"{group.Mechanic}\"");
                        toDelete.AddRange(group.Actions);
                    };
                }
                if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Delete this mechanic and all its actions");
            }

            ImGui.PopID();
        }

        ImGui.EndTable();

        deferred?.Invoke();
        if (toDelete.Count > 0) DeleteLines(fight, toDelete);

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

    // Drop lines from the plan, tombstoning the ones a re-bake would revive.
    private void DeleteLines(FightProfile fight, List<MitLine> lines)
    {
        var extras = 0;
        var kept = 0;
        var seen = new HashSet<MitLine>();
        foreach (var line in lines)
        {
            // A blank line can reach here twice in one frame, from its own
            // cleanup and from its mechanic's delete: one tombstone is enough.
            if (!seen.Add(line)) continue;
            // A job-extra line (Mantra, Curing Waltz, ...) needs a tombstone
            // too, or the auto-mix would just put it right back.
            var isAutoExtra = JobExtras.IsAutoExtra(line);
            if ((!line.Custom || isAutoExtra) && !string.IsNullOrEmpty(fight.Slot))
            {
                fight.DeletedCalls.Add(new DeletedCall
                {
                    Slot = fight.Slot,
                    Time = line.Time,
                    Mechanic = line.Mechanic,
                    Action = line.Action,
                });
                if (isAutoExtra) extras++; else kept++;
            }
            fight.Lines.Remove(line);
        }

        if (extras > 0 || kept > 0)
            FlashBuiltin(extras > 0 && kept == 0
                ? "Job extra deleted. It stays out of the auto-mix; Restore (above the table) brings it back."
                : "Deleted. It stays deleted; Restore (above the table) brings it back.");

        // Keep the saved copy in step after a config reload.
        if (!string.IsNullOrEmpty(fight.Slot))
            fight.SavedSlots[fight.Slot] = fight.Lines;
        C.Save();
        _plugin.SheetViewWindow.MarkPlanDirty();
    }

    // Right-click menu shared by the editable cells.
    private void LineContextItems(FightProfile fight, MitLine line, int index, Action<string> undoable,
        ref Action? deferred, List<MitLine> toDelete)
    {
        if (ImGui.MenuItem("Copy Action")) _copiedLine = CloneLine(line);

        var hasCopy = _copiedLine != null;
        if (ImGui.MenuItem("Paste Above", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            var clip = CloneLine(_copiedLine);
            var at = index;
            deferred = () => { undoable("paste a call"); fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), clip); C.Save(); };
        }
        if (ImGui.MenuItem("Paste Below", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            var clip = CloneLine(_copiedLine);
            var at = index + 1;
            deferred = () => { undoable("paste a call"); fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), clip); C.Save(); };
        }
        if (ImGui.MenuItem("Paste Over This Action", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            undoable($"paste over \"{Ellipsis(line.Action, 28)}\"");
            PreserveBakedEdit(fight, line); // pasting over rewrites time/mechanic
            OverwriteLine(line, _copiedLine);
            deferred = () => { fight.Lines = fight.Lines.OrderBy(a => a.Time).ToList(); _scrollToLine = line; C.Save(); _plugin.SheetViewWindow.MarkPlanDirty(); };
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Duplicate Action"))
        {
            var dup = CloneLine(line);
            var at = index + 1;
            deferred = () => { undoable($"duplicate \"{Ellipsis(line.Action, 28)}\""); fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), dup); C.Save(); };
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Delete Action"))
        {
            undoable($"delete \"{Ellipsis(line.Action, 28)}\"");
            toDelete.Add(line);
        }
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

}
