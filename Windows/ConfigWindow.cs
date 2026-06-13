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

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    private int _selectedFight;

    // Import state.
    private string _importBuffer = "";
    private List<string[]>? _importGrid;
    private char _importDelimiter = '\t';
    private int _timeCol, _mechCol = 1, _actionCol = 2;
    private bool _importHeader = true;
    private int _importJobMode; // 0 = all, 1 = current selection, 2 = pick
    private readonly HashSet<string> _importPickedJobs = new(StringComparer.OrdinalIgnoreCase);

    public ConfigWindow(Plugin plugin)
        : base("Fren Mits##config")
    {
        _plugin = plugin;
        Size = new Vector2(740, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawStatusHeader();
        DrawJobSelector();
        ImGui.Separator();

        if (ImGui.BeginTabBar("##frenmits-tabs"))
        {
            if (ImGui.BeginTabItem("Fights")) { DrawFightsTab(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Timer")) { DrawTimerTab(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Display")) { DrawDisplayTab(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Audio")) { DrawAudioTab(); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
    }

    // Config-bound checkbox: edits a local copy, saves on change, returns the new value.
    private bool CfgCheck(string label, bool value)
    {
        if (ImGui.Checkbox(label, ref value)) C.Save();
        return value;
    }

    // This Dalamud ImGui binding has no SeparatorText; emulate it.
    private static void SeparatorText(string text)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled(text);
    }

    private static void Dot(bool on, string label)
    {
        ImGui.TextColored(on ? ImGuiColors.HealerGreen : ImGuiColors.DalamudGrey, "●");
        ImGui.SameLine(0, 4);
        ImGui.TextUnformatted(label);
    }

    private static void HelpMarker(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 28f);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    private void DrawStatusHeader()
    {
        var fight = _plugin.ActiveFight();
        var job = _plugin.ActiveJobAbbreviation();
        var running = _plugin.Timer.Running;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0x22FFFFFF);
        if (ImGui.BeginChild("##status", new Vector2(0, ImGui.GetTextLineHeightWithSpacing() + 12), true,
                ImGuiWindowFlags.NoScrollbar))
        {
            Dot(fight != null, fight != null ? $"In fight: {fight.Name}" : "No fight here");
            ImGui.SameLine(0, 18);
            Dot(job != null, $"Job: {job ?? "?"}");
            ImGui.SameLine(0, 18);
            Dot(running, running ? $"Timer: {_plugin.Timer.Elapsed:0.0}s" : "Timer: idle");
            ImGui.SameLine(0, 18);
            Dot(C.AudioEnabled, "Audio");

            ImGui.SameLine();
            var right = ImGui.GetWindowWidth() - 170;
            if (right > 0) ImGui.SameLine(right);
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Stopwatch)) _plugin.Timer.SyncNow();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Sync timer to now (/fm sync)");
            ImGui.SameLine();
            var test = C.TestMode;
            if (ImGui.Checkbox("Test", ref test)) { C.TestMode = test; C.Save(); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Show a sample call so you can place / size the overlay");
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // ---- Top: job / role selection ---------------------------------------

    private void DrawJobSelector()
    {
        ImGui.TextUnformatted("Your job:");
        ImGui.SameLine();

        var options = new List<string> { "Auto (follow current job)" };
        options.AddRange(Jobs.Abbreviations);
        var currentIndex = C.JobSelection == "Auto"
            ? 0
            : Math.Max(0, Array.IndexOf(Jobs.Abbreviations, C.JobSelection) + 1);

        ImGui.SetNextItemWidth(240f);
        if (ImGui.Combo("##jobsel", ref currentIndex, options.ToArray(), options.Count))
        {
            C.JobSelection = currentIndex == 0 ? "Auto" : Jobs.Abbreviations[currentIndex - 1];
            C.Save();
        }

        ImGui.SameLine();
        var resolved = _plugin.ActiveJobAbbreviation();
        ImGui.TextDisabled($"(active: {resolved ?? "?"})");
        HelpMarker("Auto follows your current job. Lines only show for the jobs they target (or all). "
                   + "Use this to preview another job's calls.");
    }

    // ---- Fights ----------------------------------------------------------

    private void DrawFightsTab()
    {
        DrawFightListBar();

        if (_selectedFight < 0 || _selectedFight >= C.Fights.Count)
        {
            ImGui.TextDisabled("Add a fight to begin, or import a sheet.");
            return;
        }

        var fight = C.Fights[_selectedFight];
        ImGui.Separator();
        DrawFightHeader(fight);
        if (Builtin.Has(fight.TerritoryId))
        {
            ImGui.Separator();
            DrawBuiltinLoad(fight);
        }
        ImGui.Separator();
        DrawLineTable(fight);
        ImGui.Separator();
        DrawImportSection(fight);
    }

    private int _builtinSlot;

    private void DrawBuiltinLoad(FightProfile fight)
    {
        var slots = Builtin.Slots(fight.TerritoryId);
        SeparatorText($"Built-in mits — {Builtin.Name(fight.TerritoryId)}");
        ImGui.TextWrapped("One-click load of the baked timeline for your slot — every phase, matched to cactbot's "
                          + "timeline for accurate times + resync anchors. Tanks pick a tank slot, DPS your role slot, "
                          + "healers your job.");
        _builtinSlot = Math.Clamp(_builtinSlot, 0, slots.Length - 1);
        ImGui.SetNextItemWidth(120f);
        ImGui.Combo("Your slot##builtin", ref _builtinSlot, slots, slots.Length);
        ImGui.SameLine();
        if (ImGui.Button("Load mits"))
        {
            var slot = slots[_builtinSlot];
            fight.Lines = Builtin.BuildLines(fight.TerritoryId, slot);
            fight.SyncPoints = Builtin.SyncPoints(fight.TerritoryId);
            fight.BossAnchors = Builtin.BossAnchors(fight.TerritoryId);
            C.DmuSlot = slot;
            C.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Replaces this fight's lines with the baked sheet for the chosen slot.");
        if (!string.IsNullOrEmpty(C.DmuSlot))
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, $"loaded: {C.DmuSlot}");
        }
        ImGui.SameLine();
        if (ImGui.Button("Sync anchors only"))
        {
            fight.SyncPoints = Builtin.SyncPoints(fight.TerritoryId);
            fight.BossAnchors = Builtin.BossAnchors(fight.TerritoryId);
            C.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Add the resync anchors without touching your lines (e.g. if you imported the sheet yourself).");
    }

    private void DrawFightListBar()
    {
        ImGui.SetNextItemWidth(300f);
        var names = C.Fights.Select(f => f.Enabled ? f.Name : f.Name + " (off)").ToArray();
        if (names.Length == 0) names = new[] { "<no fights>" };
        ImGui.Combo("##fightlist", ref _selectedFight, names, names.Length);

        ImGui.SameLine();
        ImGui.BeginGroup();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
        {
            C.Fights.Add(new FightProfile { Name = "New fight", TerritoryId = Service.ClientState.TerritoryType });
            _selectedFight = C.Fights.Count - 1;
            C.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add a fight");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Copy)
            && _selectedFight >= 0 && _selectedFight < C.Fights.Count)
        {
            var src = C.Fights[_selectedFight];
            C.Fights.Add(new FightProfile
            {
                Name = src.Name + " copy",
                TerritoryId = src.TerritoryId,
                TimerOffset = src.TimerOffset,
                Enabled = src.Enabled,
                Lines = src.Lines.Select(CloneLine).ToList()
            });
            _selectedFight = C.Fights.Count - 1;
            C.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Duplicate the selected fight");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)
            && _selectedFight >= 0 && _selectedFight < C.Fights.Count)
        {
            C.Fights.RemoveAt(_selectedFight);
            _selectedFight = Math.Clamp(_selectedFight, 0, Math.Max(0, C.Fights.Count - 1));
            C.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delete the selected fight");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport)) ImportFightFromClipboard();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Import a fight from the clipboard");

        foreach (var (territory, name) in Builtin.Fights)
        {
            if (C.Fights.Any(f => f.TerritoryId == territory)) continue;
            if (ImGui.Button($"+ {name} preset"))
            {
                C.Fights.Add(new FightProfile { Name = name, TerritoryId = territory });
                _selectedFight = C.Fights.Count - 1;
                C.Save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Add a fight pre-set to territory {territory}.");
        }
        ImGui.EndGroup();
    }

    private void DrawFightHeader(FightProfile fight)
    {
        var name = fight.Name;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputText("Name", ref name, 128)) { fight.Name = name; C.Save(); }

        ImGui.SameLine();
        var enabled = fight.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled)) { fight.Enabled = enabled; C.Save(); }

        var territory = (int)fight.TerritoryId;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputInt("Territory id", ref territory)) { fight.TerritoryId = (uint)Math.Max(0, territory); C.Save(); }

        ImGui.SameLine();
        if (ImGui.Button($"Use current zone ({Service.ClientState.TerritoryType})"))
        {
            fight.TerritoryId = Service.ClientState.TerritoryType;
            C.Save();
        }
        var zoneName = TerritoryName(fight.TerritoryId);
        if (!string.IsNullOrEmpty(zoneName)) { ImGui.SameLine(); ImGui.TextDisabled(zoneName); }

        var offset = fight.TimerOffset;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputFloat("Timer offset (s)", ref offset, 0.1f, 1f, "%.1f")) { fight.TimerOffset = offset; C.Save(); }
        ImGui.SameLine();
        ImGui.TextDisabled("+ shifts every call earlier. /fm sync zeroes the live timer.");

        if (ImGui.Button("Export to clipboard")) ExportFight(fight);
        ImGui.SameLine();
        if (ImGui.Button("Import from clipboard")) ImportFightFromClipboard();
        ImGui.SameLine();
        ImGui.TextDisabled("Share a whole fight (lines included) with a friend.");

        if (Builtin.Has(fight.TerritoryId))
        {
            ImGui.TextColored(ImGuiColors.ParsedGreen, Builtin.Name(fight.TerritoryId));
            ImGui.SameLine();
            ImGui.TextDisabled("— continuous clock from the pull through every phase; resets on a wipe.");
        }
        else
        {
            ImGui.TextDisabled("Line times are seconds from the pull (one continuous timeline across all phases).");
        }
        HelpMarker("The timer starts at combat and resets automatically on a wipe or when the duty ends, "
                   + "so it is ready for the next pull. Use a per-fight offset or /fm sync to align the sheet's "
                   + "t=0 with your pull.");
    }

    private void DrawLineTable(FightProfile fight)
    {
        ImGui.TextUnformatted($"Lines ({fight.Lines.Count})");
        ImGui.SameLine();
        if (ImGui.SmallButton("Add line")) { fight.Lines.Add(new MitLine()); C.Save(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("Sort by time")) { fight.Lines = fight.Lines.OrderBy(l => l.Time).ToList(); C.Save(); }

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;
        if (!ImGui.BeginTable("##lines", 7, flags, new Vector2(0, 280)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("Mechanic", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("Jobs", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("##opt", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableHeadersRow();

        MitLine? toDelete = null;
        for (var i = 0; i < fight.Lines.Count; i++)
        {
            var line = fight.Lines[i];
            ImGui.TableNextRow();
            ImGui.PushID(i);

            ImGui.TableNextColumn();
            var on = line.Enabled;
            if (ImGui.Checkbox("##on", ref on)) { line.Enabled = on; C.Save(); }

            ImGui.TableNextColumn();
            var time = line.Time;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputFloat("##time", ref time, 0, 0, "%.1f")) { line.Time = time; C.Save(); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(line.TimeText);

            ImGui.TableNextColumn();
            var mech = line.Mechanic;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##mech", ref mech, 256)) { line.Mechanic = mech; C.Save(); }

            ImGui.TableNextColumn();
            var icon = Icons.For(line);
            if (icon != 0)
            {
                var h = ImGui.GetFrameHeight();
                Icons.Draw(icon, new Vector2(h, h));
                ImGui.SameLine(0, 4);
            }
            var action = line.Action;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##action", ref action, 256)) { line.Action = action; C.Save(); }

            ImGui.TableNextColumn();
            DrawJobsCell(line);

            ImGui.TableNextColumn();
            if (ImGui.SmallButton("…")) ImGui.OpenPopup("lineopt");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Per-line lead / speech / colour / mute");
            DrawLineOptionsPopup(line);

            ImGui.TableNextColumn();
            if (ImGui.SmallButton("X")) toDelete = line;

            ImGui.PopID();
        }

        ImGui.EndTable();

        if (toDelete != null) { fight.Lines.Remove(toDelete); C.Save(); }
    }

    private void DrawJobsCell(MitLine line)
    {
        var label = line.Jobs.Count == 0 ? "All" : string.Join(",", line.Jobs);
        if (label.Length > 14) label = label[..12] + "…";
        if (ImGui.Button(label + "##jobs", new Vector2(-1, 0)))
            ImGui.OpenPopup("jobspopup");

        if (ImGui.BeginPopup("jobspopup"))
        {
            if (ImGui.Button("All jobs")) { line.Jobs.Clear(); C.Save(); }

            foreach (var role in Enum.GetValues<JobRole>())
            {
                SeparatorText(role.ToString());
                var first = true;
                foreach (var abbr in Jobs.AbbreviationsForRole(role))
                {
                    if (!first) ImGui.SameLine();
                    first = false;
                    var has = line.Jobs.Contains(abbr, StringComparer.OrdinalIgnoreCase);
                    if (ImGui.Checkbox(abbr, ref has))
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

    // ---- Import ----------------------------------------------------------

    private void DrawImportSection(FightProfile fight)
    {
        if (!ImGui.CollapsingHeader("Import from a sheet (paste rows)"))
            return;

        ImGui.TextWrapped("Copy rows straight out of Google Sheets / Excel and paste below. "
                          + "Pick which columns hold the time, mechanic, and the action you press. "
                          + "Rows without a readable time (headers, blanks) are skipped.");

        ImGui.InputTextMultiline("##importbuf", ref _importBuffer, 65536, new Vector2(-1, 120));

        if (ImGui.Button("Parse")) _importGrid = SheetImport.ParseGrid(_importBuffer, out _importDelimiter);
        ImGui.SameLine();
        if (ImGui.Button("Clear")) { _importBuffer = ""; _importGrid = null; }

        if (_importGrid == null || _importGrid.Count == 0) return;

        var cols = _importGrid.Max(r => r.Length);
        ImGui.TextDisabled($"Detected {_importGrid.Count} rows, {cols} columns, delimiter = "
                           + (_importDelimiter == '\t' ? "Tab" : "Comma"));

        var colNames = Enumerable.Range(0, cols).Select(i => $"Col {i}{HeaderHint(i)}").ToArray();
        _timeCol = Math.Clamp(_timeCol, 0, cols - 1);
        _mechCol = Math.Clamp(_mechCol, 0, cols - 1);
        _actionCol = Math.Clamp(_actionCol, 0, cols - 1);

        ImGui.SetNextItemWidth(220f);
        ImGui.Combo("Time column", ref _timeCol, colNames, colNames.Length);
        ImGui.SetNextItemWidth(220f);
        ImGui.Combo("Mechanic column", ref _mechCol, colNames, colNames.Length);
        ImGui.SetNextItemWidth(220f);
        ImGui.Combo("Action column (your mit)", ref _actionCol, colNames, colNames.Length);

        var header = _importHeader;
        if (ImGui.Checkbox("First row is a header", ref header)) _importHeader = header;

        ImGui.TextUnformatted("Assign imported lines to:");
        ImGui.RadioButton("Everyone", ref _importJobMode, 0); ImGui.SameLine();
        ImGui.RadioButton("My selected job", ref _importJobMode, 1); ImGui.SameLine();
        ImGui.RadioButton("Pick below", ref _importJobMode, 2);

        var pickedJobs = new List<string>();
        if (_importJobMode == 2)
        {
            foreach (var role in Enum.GetValues<JobRole>())
            {
                ImGui.TextDisabled(role + ":");
                foreach (var abbr in Jobs.AbbreviationsForRole(role))
                {
                    ImGui.SameLine();
                    var on = _importPickedJobs.Contains(abbr);
                    if (ImGui.Checkbox(abbr + "##imp", ref on))
                    {
                        if (on) _importPickedJobs.Add(abbr); else _importPickedJobs.Remove(abbr);
                    }
                }
            }
            pickedJobs = _importPickedJobs.ToList();
        }
        else if (_importJobMode == 1)
        {
            var active = _plugin.ActiveJobAbbreviation();
            if (active != null) pickedJobs.Add(active);
        }

        var previewRow = _importGrid.Skip(_importHeader ? 1 : 0).FirstOrDefault();
        if (previewRow != null)
        {
            var okTime = SheetImport.TryParseTime(Get(previewRow, _timeCol), out var sec);
            ImGui.TextDisabled($"Preview: time={(okTime ? sec.ToString("0.#") + "s" : "??")}  "
                               + $"mech=\"{Get(previewRow, _mechCol)}\"  action=\"{Get(previewRow, _actionCol)}\"");
        }

        var opt = new SheetImport.Options
        {
            TimeColumn = _timeCol,
            MechanicColumn = _mechCol,
            ActionColumn = _actionCol,
            FirstRowIsHeader = _importHeader,
            Jobs = pickedJobs
        };

        if (ImGui.Button("Append to lines"))
        {
            fight.Lines.AddRange(SheetImport.BuildLines(_importGrid, opt));
            fight.Lines = fight.Lines.OrderBy(l => l.Time).ToList();
            C.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Replace all lines"))
        {
            fight.Lines = SheetImport.BuildLines(_importGrid, opt).OrderBy(l => l.Time).ToList();
            C.Save();
        }
    }

    private string HeaderHint(int col)
    {
        if (_importGrid == null || !_importHeader || _importGrid.Count == 0) return "";
        var header = Get(_importGrid[0], col);
        return string.IsNullOrWhiteSpace(header) ? "" : $" ({Trunc(header, 14)})";
    }

    // ---- Timer / Display tabs -------------------------------------------

    private void DrawTimerTab()
    {
        var warn = C.WarningSeconds;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.SliderFloat("Warning lead (s)", ref warn, 1f, 10f, "%.1f")) { C.WarningSeconds = warn; C.Save(); }
        ImGui.TextDisabled("How early the call appears before the mit time (default 3s).");

        var hold = C.HoldSeconds;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.SliderFloat("Hold on screen (s)", ref hold, 0f, 6f, "%.1f")) { C.HoldSeconds = hold; C.Save(); }
        ImGui.TextDisabled("How long the call stays up after its time passes.");

        ImGui.Separator();
        ImGui.TextUnformatted($"Timer: {(_plugin.Timer.Running ? _plugin.Timer.Elapsed.ToString("0.0") + "s" : "not running")}");
        if (ImGui.Button("Sync now (zero timer)")) _plugin.Timer.SyncNow();
        ImGui.SameLine();
        if (ImGui.Button("Reset timer")) _plugin.Timer.Reset();
        ImGui.TextDisabled("Auto-starts on combat. Sync aligns it to a known mechanic (also /fm sync).");

        SeparatorText("Resync (cactbot-style)");
        C.EnableSync = CfgCheck("Resync the clock on boss casts", C.EnableSync);
        HelpMarker("When a known boss ability begins casting, the timer snaps so that ability resolves on its scripted "
                   + "time. This corrects the drift between phases caused by kill speed, the same way cactbot keeps its "
                   + "timeline accurate. Only abilities with a cast bar can be caught; the continuous clock covers the rest.");
        var win = C.SyncWindowSeconds;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.SliderFloat("Mechanic window (s)", ref win, 2f, 20f, "%.0f")) { C.SyncWindowSeconds = win; C.Save(); }
        var pwin = C.SyncPhaseWindowSeconds;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.SliderFloat("Phase window (s)", ref pwin, 15f, 120f, "%.0f")) { C.SyncPhaseWindowSeconds = pwin; C.Save(); }
        HelpMarker("Phase anchors (the first known cast of each phase) re-base the whole clock with this wider window, so "
                   + "a phase that starts well off the sheet's nominal time still locks on. Mechanic anchors only nudge "
                   + "within the tighter window.");
        ImGui.TextDisabled($"Last sync: {(_plugin.Sync.LastSync.Length > 0 ? _plugin.Sync.LastSync : "-")}");

        var fight = _plugin.ActiveFight();
        if (fight is { SyncPoints.Count: > 0 })
        {
            var phases = fight.SyncPoints.Count(s => s.IsPhase);
            ImGui.TextDisabled($"This fight: {fight.SyncPoints.Count} anchors ({phases} phase).");
        }

        DrawCaptureSection(fight);

        ImGui.Separator();
        C.OnlyInTargetTerritory = CfgCheck("Only run in the fight's territory", C.OnlyInTargetTerritory);
    }

    private void DrawCaptureSection(FightProfile? fight)
    {
        SeparatorText("Build anchors from a pull (advanced)");
        ImGui.TextWrapped("Public timelines only cover DMU through phase 3. To make phases 4-5 self-correct, record a clean "
                          + "pull: every boss cast is logged with the time it lands, then promote the phase-start casts to "
                          + "anchors. This is exactly how cactbot timelines are authored.");

        var rec = _plugin.Sync.Recording;
        if (ImGui.Checkbox("Record boss casts this pull", ref rec)) _plugin.Sync.Recording = rec;
        ImGui.SameLine();
        if (ImGui.Button("Clear captures")) _plugin.Sync.Captured.Clear();

        if (_plugin.Sync.Captured.Count == 0) return;

        if (ImGui.BeginTable("##caps", 4,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 180)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Ability", ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("Caster", ImGuiTableColumnFlags.WidthStretch, 1);
            ImGui.TableSetupColumn("Add", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableHeadersRow();

            // newest first
            for (var i = _plugin.Sync.Captured.Count - 1; i >= 0; i--)
            {
                var cap = _plugin.Sync.Captured[i];
                ImGui.TableNextRow();
                ImGui.PushID(i);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{(int)cap.Time / 60}:{(int)cap.Time % 60:00}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(cap.IsBoss ? $"boss {cap.Id}" : $"0x{cap.Id:X}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(cap.Caster);
                ImGui.TableNextColumn();
                if (cap.IsBoss)
                {
                    if (fight != null && ImGui.SmallButton("+boss anchor"))
                        AddBossAnchor(fight, cap);
                }
                else
                {
                    if (fight != null && ImGui.SmallButton("+phase"))
                        AddAnchor(fight, cap, true);
                    ImGui.SameLine();
                    if (fight != null && ImGui.SmallButton("+mech"))
                        AddAnchor(fight, cap, false);
                }
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        if (fight == null) ImGui.TextDisabled("Enter the fight's zone to add anchors to it.");
    }

    private void AddAnchor(FightProfile fight, SyncEngine.Capture cap, bool isPhase)
    {
        fight.SyncPoints.RemoveAll(s => s.Ability == cap.Id && MathF.Abs(s.Time - cap.Time) < 4f);
        fight.SyncPoints.Add(new SyncPoint
        {
            Ability = cap.Id,
            Time = cap.Time,
            IsPhase = isPhase,
            Label = $"{cap.Caster} (captured)"
        });
        C.Save();
    }

    private void AddBossAnchor(FightProfile fight, SyncEngine.Capture cap)
    {
        fight.BossAnchors.RemoveAll(b => b.NameId == cap.Id);
        fight.BossAnchors.Add(new BossAnchor { NameId = cap.Id, Time = cap.Time, Label = $"{cap.Caster} (captured)" });
        C.Save();
    }

    private void DrawDisplayTab()
    {
        SeparatorText("Position");
        C.OverlayLocked = CfgCheck("Lock overlay (click-through)", C.OverlayLocked);
        ImGui.TextDisabled("Unlock to drag the call text anywhere, then lock it. Turn on Test mode to see it.");
        ImGui.SameLine();
        if (ImGui.Button("Reset to centre"))
        {
            C.OverlayPosition = new Vector2(0.5f, 0.35f);
            C.Save();
            _plugin.OverlayWindow.RequestReposition();
        }
        ImGui.SameLine();
        ImGui.TextDisabled(C.OverlayLocked ? "(unlock to drag)" : "(drag the title bar to move)");
        C.TestMode = CfgCheck("Test mode (show a sample call so you can place/size it)", C.TestMode);

        SeparatorText("Size (crisp font, in pixels)");
        var callPx = C.OverlayFontSizePx;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat("Call text size", ref callPx, 12f, 120f, "%.0f px")) { C.OverlayFontSizePx = callPx; C.Save(); }
        var upPx = C.UpcomingFontSizePx;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat("Upcoming text size", ref upPx, 10f, 60f, "%.0f px")) { C.UpcomingFontSizePx = upPx; C.Save(); }

        SeparatorText("Text");
        var fmt = C.HeadlineFormat;
        ImGui.SetNextItemWidth(280f);
        if (ImGui.InputText("Call format", ref fmt, 128)) { C.HeadlineFormat = fmt; C.Save(); }
        ImGui.TextDisabled("Placeholders: {action} {mechanic} {time} {count} {remaining}");
        var suffix = C.ActiveSuffix;
        ImGui.SetNextItemWidth(280f);
        if (ImGui.InputText("\"NOW\" suffix", ref suffix, 64)) { C.ActiveSuffix = suffix; C.Save(); }
        C.ShowCountdownNumber = CfgCheck("Append countdown number while counting down", C.ShowCountdownNumber);
        C.ShowMechanicLine = CfgCheck("Show mechanic name on a second line", C.ShowMechanicLine);
        C.ShowAbilityIcon = CfgCheck("Show the ability icon next to the call", C.ShowAbilityIcon);
        HelpMarker("Icons are matched from the action name automatically; pin a specific one per line with the \"…\" button.");
        C.ShowDtrBar = CfgCheck("Show next mit on the server-info bar", C.ShowDtrBar);

        SeparatorText("Colors");
        var imminent = ColorToVec4(C.OverlayColorImminent);
        if (ImGui.ColorEdit4("Counting down", ref imminent)) { C.OverlayColorImminent = Vec4ToColor(imminent); C.Save(); }
        var active = ColorToVec4(C.OverlayColorActive);
        if (ImGui.ColorEdit4("Active (NOW)", ref active)) { C.OverlayColorActive = Vec4ToColor(active); C.Save(); }
        var mechCol = ColorToVec4(C.OverlayColorMechanic);
        if (ImGui.ColorEdit4("Mechanic line", ref mechCol)) { C.OverlayColorMechanic = Vec4ToColor(mechCol); C.Save(); }
        var upCol = ColorToVec4(C.OverlayColorUpcoming);
        if (ImGui.ColorEdit4("Upcoming list", ref upCol)) { C.OverlayColorUpcoming = Vec4ToColor(upCol); C.Save(); }
        C.TextShadow = CfgCheck("Drop shadow (improves readability)", C.TextShadow);

        SeparatorText("Countdown bar");
        C.ShowProgressBar = CfgCheck("Show countdown bar under the call", C.ShowProgressBar);
        C.PulseWhenImminent = CfgCheck("Pulse the text in the last second", C.PulseWhenImminent);

        SeparatorText("Background");
        C.ShowBackground = CfgCheck("Draw a background box", C.ShowBackground);
        if (C.ShowBackground)
        {
            var bg = ColorToVec4(C.BackgroundColor);
            if (ImGui.ColorEdit4("Background color", ref bg)) { C.BackgroundColor = Vec4ToColor(bg); C.Save(); }
        }

        SeparatorText("Upcoming list");
        C.ShowUpcoming = CfgCheck("Show upcoming list", C.ShowUpcoming);
        if (C.ShowUpcoming)
        {
            var count = C.UpcomingCount;
            ImGui.SetNextItemWidth(120f);
            if (ImGui.SliderInt("Upcoming lines", ref count, 1, 8)) { C.UpcomingCount = count; C.Save(); }
            var look = C.UpcomingLookaheadSeconds;
            ImGui.SetNextItemWidth(160f);
            if (ImGui.SliderFloat("Look-ahead (s)", ref look, 5f, 90f, "%.0f")) { C.UpcomingLookaheadSeconds = look; C.Save(); }
        }
    }

    private void DrawAudioTab()
    {
        C.AudioEnabled = CfgCheck("Enable audio cues", C.AudioEnabled);
        ImGui.TextDisabled("Plays when a call enters its warning window, once per pull, even if the overlay is hidden.");

        SeparatorText("Text-to-speech");
        C.TtsEnabled = CfgCheck("Speak the action (Windows TTS)", C.TtsEnabled);
        var rate = C.TtsRate;
        ImGui.SetNextItemWidth(200f);
        if (ImGui.SliderInt("Speech rate", ref rate, -10, 10)) { C.TtsRate = rate; C.Save(); }
        var vol = C.TtsVolume;
        ImGui.SetNextItemWidth(200f);
        if (ImGui.SliderInt("Speech volume", ref vol, 0, 100)) { C.TtsVolume = vol; C.Save(); }
        if (ImGui.Button("Test voice")) _plugin.Audio.Speak("Reprisal", C.TtsRate, C.TtsVolume);

        SeparatorText("Beep");
        C.BeepEnabled = CfgCheck("Play a beep", C.BeepEnabled);
        var freq = C.BeepFrequency;
        ImGui.SetNextItemWidth(200f);
        if (ImGui.SliderFloat("Frequency (Hz)", ref freq, 200f, 2000f, "%.0f")) { C.BeepFrequency = freq; C.Save(); }
        var ms = C.BeepMs;
        ImGui.SetNextItemWidth(200f);
        if (ImGui.SliderInt("Length (ms)", ref ms, 40, 600)) { C.BeepMs = ms; C.Save(); }
        var bvol = C.BeepVolume;
        ImGui.SetNextItemWidth(200f);
        if (ImGui.SliderInt("Beep volume", ref bvol, 0, 100)) { C.BeepVolume = bvol; C.Save(); }
        if (ImGui.Button("Test beep")) _plugin.Audio.Beep(C.BeepFrequency, C.BeepMs, C.BeepVolume);

        ImGui.Separator();
        ImGui.TextDisabled("Per line you can override the spoken text or mute the cue (the \"…\" button on each line).");
    }

    // ---- per-line overrides popup ---------------------------------------

    private string _iconSearch = "";

    private void DrawLineOptionsPopup(MitLine line)
    {
        if (!ImGui.BeginPopup("lineopt")) return;

        var fight = (_selectedFight >= 0 && _selectedFight < C.Fights.Count) ? C.Fights[_selectedFight] : null;
        if (fight != null)
        {
            var idx = fight.Lines.IndexOf(line);
            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp) && idx > 0)
            {
                (fight.Lines[idx - 1], fight.Lines[idx]) = (fight.Lines[idx], fight.Lines[idx - 1]);
                C.Save();
            }
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown) && idx >= 0 && idx < fight.Lines.Count - 1)
            {
                (fight.Lines[idx + 1], fight.Lines[idx]) = (fight.Lines[idx], fight.Lines[idx + 1]);
                C.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("reorder");
            ImGui.Separator();
        }

        SeparatorText("Icon");
        var resolved = Icons.For(line);
        Icons.Draw(resolved, new Vector2(32, 32));
        ImGui.SameLine();
        ImGui.TextUnformatted(line.IconId != 0 ? "pinned" : (resolved != 0 ? "auto" : "none"));
        ImGui.SameLine();
        if (ImGui.SmallButton("Auto##icon")) { line.IconId = 0; C.Save(); }
        var iconId = (int)line.IconId;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputInt("Icon id", ref iconId)) { line.IconId = (uint)Math.Max(0, iconId); C.Save(); }
        ImGui.SetNextItemWidth(220f);
        ImGui.InputTextWithHint("##iconsearch", "search action…", ref _iconSearch, 64);
        if (!string.IsNullOrWhiteSpace(_iconSearch))
        {
            foreach (var (name, ic) in Icons.Search(_iconSearch, 10))
            {
                Icons.Draw(ic, new Vector2(20, 20));
                ImGui.SameLine();
                if (ImGui.Selectable($"{name}##pick{ic}")) { line.IconId = ic; C.Save(); _iconSearch = ""; }
            }
        }

        SeparatorText("Overrides (0 / empty = global)");

        var lead = line.LeadOverride;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputFloat("Warning lead (s)", ref lead, 0.5f, 1f, "%.1f"))
        {
            line.LeadOverride = MathF.Max(0f, lead);
            C.Save();
        }

        var tts = line.Tts;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.InputText("Speak instead", ref tts, 128)) { line.Tts = tts; C.Save(); }
        ImGui.TextDisabled("Empty = speak the action.");

        var sound = line.Sound;
        if (ImGui.Checkbox("Play audio cue for this line", ref sound)) { line.Sound = sound; C.Save(); }

        var useColor = line.Color != 0;
        if (ImGui.Checkbox("Custom text colour", ref useColor))
        {
            line.Color = useColor ? 0xFF55FFFF : 0u;
            C.Save();
        }
        if (line.Color != 0)
        {
            var col = ColorToVec4(line.Color);
            if (ImGui.ColorEdit4("Colour", ref col)) { line.Color = Vec4ToColor(col); C.Save(); }
        }

        ImGui.EndPopup();
    }

    // ---- share via clipboard --------------------------------------------

    private void ExportFight(FightProfile fight)
    {
        try
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(fight);
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
            ImGui.SetClipboardText("FRENMITS1:" + b64);
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: export failed");
        }
    }

    private void ImportFightFromClipboard()
    {
        try
        {
            var text = ImGui.GetClipboardText() ?? "";
            const string prefix = "FRENMITS1:";
            if (!text.StartsWith(prefix)) return;
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(text[prefix.Length..]));
            var fight = Newtonsoft.Json.JsonConvert.DeserializeObject<FightProfile>(json);
            if (fight == null) return;
            fight.Id = Guid.NewGuid().ToString("N");
            C.Fights.Add(fight);
            _selectedFight = C.Fights.Count - 1;
            C.Save();
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: import failed");
        }
    }

    // ---- helpers ---------------------------------------------------------

    private static MitLine CloneLine(MitLine l) => new()
    {
        Time = l.Time, Mechanic = l.Mechanic, Action = l.Action,
        Jobs = new List<string>(l.Jobs), Enabled = l.Enabled,
        LeadOverride = l.LeadOverride, Tts = l.Tts, Sound = l.Sound, Color = l.Color, IconId = l.IconId
    };

    private static string Get(string[] row, int i) => i >= 0 && i < row.Length ? row[i] : "";
    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    private static string TerritoryName(uint id)
    {
        if (id == 0) return "";
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
            var row = sheet?.GetRowOrDefault(id);
            var name = row?.PlaceName.ValueNullable?.Name.ExtractText();
            return string.IsNullOrWhiteSpace(name) ? "" : name!;
        }
        catch
        {
            return "";
        }
    }

    private static Vector4 ColorToVec4(uint abgr) => new(
        (abgr & 0xFF) / 255f,
        ((abgr >> 8) & 0xFF) / 255f,
        ((abgr >> 16) & 0xFF) / 255f,
        ((abgr >> 24) & 0xFF) / 255f);

    private static uint Vec4ToColor(Vector4 v) =>
        ((uint)(Math.Clamp(v.W, 0, 1) * 255) << 24) |
        ((uint)(Math.Clamp(v.Z, 0, 1) * 255) << 16) |
        ((uint)(Math.Clamp(v.Y, 0, 1) * 255) << 8) |
        (uint)(Math.Clamp(v.X, 0, 1) * 255);
}
