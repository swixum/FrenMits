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

// Settings: importing a plan from a share code.
public partial class ConfigWindow
{
    // ---- Import ----

    private void DrawImportSection(FightProfile fight)
    {
        if (!ImGui.CollapsingHeader("Import from a sheet (paste rows)"))
            return;

        ImGui.TextWrapped("Copy rows straight out of Google Sheets / Excel and paste below. "
                          + "Pick which columns hold the time, mechanic, and the action you press. "
                          + "Rows without a readable time (headers, blanks) are skipped.");

        // Per-fight scratch, so one paste can't land in another.
        _importBuffer = _importBufs.GetValueOrDefault(fight.Id, "");
        _importGrid = _importGrids.GetValueOrDefault(fight.Id);

        ImGui.InputTextMultiline("##importbuf", ref _importBuffer, 65536, new Vector2(-1, 120));
        _importBufs[fight.Id] = _importBuffer;

        if (ImGui.Button("Parse")) _importGrids[fight.Id] = _importGrid = SheetImport.ParseGrid(_importBuffer, out _importDelimiter);
        ImGui.SameLine();
        if (ImGui.Button("Clear")) { _importBufs[fight.Id] = _importBuffer = ""; _importGrids[fight.Id] = _importGrid = null; }

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
        if (GreenCheckbox("First row is a header", ref header)) _importHeader = header;

        ImGui.TextUnformatted("Assign imported lines to:");
        ImGui.RadioButton("Everyone", ref _importJobMode, 0); ImGui.SameLine();
        ImGui.RadioButton("My selected job", ref _importJobMode, 1); ImGui.SameLine();
        ImGui.RadioButton("Pick below", ref _importJobMode, 2);

        var pickedJobs = new List<string>();
        if (_importJobMode == 2)
        {
            foreach (var role in Enum.GetValues<JobRole>())
            {
                ImGui.TextDisabled(RoleLabel(role) + ":");
                foreach (var abbr in Jobs.AbbreviationsForRole(role))
                {
                    ImGui.SameLine();
                    var on = _importPickedJobs.Contains(abbr);
                    if (GreenCheckbox(abbr + "##imp", ref on))
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

        if (ImGui.Button("Add to current mits"))
        {
            if (_importJobMode == 1 && pickedJobs.Count == 0)
            {
                // The selected job resolved to nothing.
                FlashBuiltin("Couldn't resolve your job - pick jobs manually or set Job selection first.");
            }
            else
            {
                // Always additive: imports append, then sort.
                var imported = SheetImport.BuildLines(_importGrid, opt);
                var merged = new List<MitLine>(fight.Lines);
                merged.AddRange(imported);
                SetFightLines(fight, merged.OrderBy(l => l.Time).ToList());
                FlashBuiltin($"Added {imported.Count} imported line(s).");
            }
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Imported lines are added onto your current slot.");
    }

    private string HeaderHint(int col)
    {
        if (_importGrid == null || !_importHeader || _importGrid.Count == 0) return "";
        var header = Get(_importGrid[0], col);
        return string.IsNullOrWhiteSpace(header) ? "" : $" ({Trunc(header, 14)})";
    }
}
