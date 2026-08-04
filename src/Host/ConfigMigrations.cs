using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Host;

// Every versioned config migration, run once at load.
public static class ConfigMigrations
{
    public static void Run(IMigrationHost host)
    {
        var config = host.Config;

        // v2: split the upcoming list into its own timeline window.
        if (config.Version < 2)
        {
            config.HeadlineFormat = "{action} ({remaining})";
            config.ShowCountdownNumber = false;
            config.WarningSeconds = 3f;
            config.Version = 2;
            config.Save();
        }

        // v3: assign sidebar categories to existing fights.
        if (config.Version < 3)
        {
            foreach (var f in config.Fights)
                if (string.IsNullOrEmpty(f.Category))
                    f.Category = Builtin.Has(f.TerritoryId) ? "Ultimate" : "Other";
            config.Version = 3;
            config.Save();
        }

        // v4: per-pull diagnostics on by default, local only.
        if (config.Version < 4)
        {
            config.Diagnostics = true;
            config.Version = 4;
            config.Save();
        }

        // v5: rebake every built-in after the sheet v3.0 rework.
        if (config.Version < 5)
        {
            host.ResetAllBuiltins();
            config.Version = 5;
            config.Save();
        }

        // v6: the legacy ultimates re-timed from real clears.
        if (config.Version < 6)
        {
            ResetDutyFights(config, f => f.TerritoryId is Builtin.UcobTerritory or Builtin.UwuTerritory or Builtin.TeaTerritory or Builtin.DsrTerritory or Builtin.TopTerritory);
            config.Version = 6;
            config.Save();
        }

        // v7: Dancing Mad resynced to sheet v4.0, plus WHM Asylum.
        if (config.Version < 7)
        {
            ResetDutyFights(config, f => f.TerritoryId == Builtin.DmuTerritory);
            config.Version = 7;
            config.Save();
        }

        // v8: re-bake DMU to the new timeline, keeping custom lines.
        if (config.Version < 8)
        {
            DmuRebake.SmartRebake(config.Fights);
            config.Version = 8;
            config.Save();
        }

        // v9: re-run the re-bake with the hardened de-overlap.
        if (config.Version < 9)
        {
            DmuRebake.SmartRebake(config.Fights);
            config.Version = 9;
            config.Save();
        }

        // v10: ship the full sheet refresh, keeping custom lines.
        if (config.Version < 10)
        {
            DmuRebake.SmartRebake(config.Fights);
            config.Version = 10;
            config.Save();
        }

        // v11: one-time clean reset of Dancing Mad to the sheet.
        if (config.Version < 11)
        {
            ResetDutyFights(config, f => f.TerritoryId == Builtin.DmuTerritory);
            config.Version = 11;
            config.Save();
        }

        // v12: another clean reset so the re-timed sheet lands.
        if (config.Version < 12)
        {
            ResetDutyFights(config, f => f.TerritoryId == Builtin.DmuTerritory);
            config.Version = 12;
            config.Save();
        }

        // v13: hard reset again, now that generic mits resolve icons.
        if (config.Version < 13)
        {
            ResetDutyFights(config, f => f.TerritoryId == Builtin.DmuTerritory);
            config.Version = 13;
            config.Save();
        }

        // v14: hard reset once more, for real ability names.
        if (config.Version < 14)
        {
            ResetDutyFights(config, f => f.TerritoryId == Builtin.DmuTerritory);
            config.Version = 14;
            config.Save();
        }

        // v15: stored fight names lose the dash the game can't draw.
        if (config.Version < 15)
        {
            foreach (var f in config.Fights)
                if (f.Name.Contains('—'))
                    f.Name = f.Name.Replace('—', '-');
            config.Version = 15;
            config.Save();
        }

        // v16: Dancing Mad re-baked to the sheet v5.0.
        if (config.Version < 16)
        {
            SnapshotDmu(host, "before the sheet v5.0 update");
            DmuRebake.SmartRebake(config.Fights);
            config.Version = 16;
            config.Save();
        }

        // v17: restore the WHM Asylum calls the v16 bake dropped.
        if (config.Version < 17)
        {
            SnapshotDmu(host, "before restoring the WHM Asylum calls");
            DmuRebake.SmartRebake(config.Fights);
            config.Version = 17;
            config.Save();
        }

        // v18: added tank and job-mit lines upgraded to sheet v5.0.
        if (config.Version < 18)
        {
            SnapshotDmu(host, "before the v5.0 tank and job-mitigation update");
            DmuRebake.UpgradeTankAndExtraLines(config.Fights);
            config.Version = 18;
            config.Save();
        }

        // v19: the sheet's P2 "Ultimate Embrance" typo is baked fixed.
        if (config.Version < 19)
        {
            SnapshotDmu(host, "before the Ultimate Embrace typo fix");
            DmuRebake.SmartRebake(config.Fights);
            config.Version = 19;
            config.Save();
        }

        // v20: migrate the old M12S placeholder zone to the real one.
        if (config.Version < 20)
        {
            foreach (var f in config.Fights)
                if (f.TerritoryId == 1320)
                {
                    f.TerritoryId = Builtin.M12sTerritory;
                    f.Category = "Savage";
                }
            config.Version = 20;
            config.Save();
        }

        // v21: force auto cooldown timing off once for old configs.
        if (config.Version < 21)
        {
            config.Version = 21;
            config.Save();
        }

        // v22: switch audio on for already-imported SMN summon cues.
        if (config.Version < 22)
        {
            var primals = new HashSet<string>(new[] { "Garuda", "Titan", "Ifrit" }, StringComparer.OrdinalIgnoreCase);
            void FixSummons(List<MitLine>? lines)
            {
                if (lines == null) return;
                foreach (var l in lines)
                {
                    if (l.Sound || !l.Custom || l.Jobs == null) continue;
                    if (!l.Jobs.Contains("SMN", StringComparer.OrdinalIgnoreCase)) continue;
                    var parts = l.Action.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length == 0 || !parts.All(p => primals.Contains(p))) continue;
                    l.Sound = true;
                    if (parts.Length > 1 && string.IsNullOrWhiteSpace(l.Tts))
                        l.Tts = string.Join(", ", parts);
                }
            }
            foreach (var f in config.Fights)
            {
                FixSummons(f.Lines);
                if (f.SavedSlots != null)
                    foreach (var slot in f.SavedSlots.Values) FixSummons(slot);
            }
            config.Version = 22;
            config.Save();
        }

        // v23: the logs client secret moved to encrypted storage.
        if (config.Version < 23)
        {
            config.MigrateFflogsSecret();
            config.Version = 23;
            config.Save();
        }

        // v24: phase dividers on the board are on by default.
        if (config.Version < 24)
        {
            config.UpcomingBoardPhases = true;
            config.Version = 24;
            config.Save();
        }

        // v25: M9S/M10S/M11S were Auto-planned before the caster fix.
        if (config.Version < 25)
        {
            var fixedUp = new ushort[] { Builtin.M9sTerritory, Builtin.M10sTerritory, Builtin.M11sTerritory };
            foreach (var f in config.Fights)
                if (Array.IndexOf(fixedUp, (ushort)f.TerritoryId) >= 0)
                    host.SnapshotFight(f, "before the caster-column fix");
            ResetDutyFights(config, f => Array.IndexOf(fixedUp, (ushort)f.TerritoryId) >= 0);
            config.Version = 25;
            config.Save();
        }

        // v26: drop repeat presses that still have most of a recast.
        if (config.Version < 26)
        {
            foreach (var f in config.Fights)
            {
                if (!Builtin.Has(f.TerritoryId)) continue;
                CoveredRepeats.Strip(f.Lines);
                foreach (var slot in f.SavedSlots.Values) CoveredRepeats.Strip(slot);
            }
            config.Version = 26;
            config.Save();
        }

        // v27: FRU's Pandora phase was stacked on top of itself.
        if (config.Version < 27)
        {
            foreach (var f in config.Fights)
                if (f.TerritoryId == Builtin.FruTerritory)
                    host.SnapshotFight(f, "before the Pandora phase was un-stacked");
            ResetDutyFights(config, f => f.TerritoryId == Builtin.FruTerritory);
            config.Version = 27;
            config.Save();
        }

        // v28: FRU's timeline was only the mit sheet, so 31 rows.
        if (config.Version < 28)
        {
            foreach (var f in config.Fights)
                if (f.TerritoryId == Builtin.FruTerritory)
                    host.SnapshotFight(f, "before the full timeline landed");
            ResetDutyFights(config, f => f.TerritoryId == Builtin.FruTerritory);
            config.Version = 28;
            config.Save();
        }

        // v29: severity and buster flags for the fights that had none.
        if (config.Version < 29)
        {
            foreach (var f in config.Fights)
            {
                if (!Builtin.Has(f.TerritoryId) || f.CustomSlots.Count > 0) continue;
                var graded = Builtin.CustomRows(f.TerritoryId);
                if (graded.Count > 0) f.CustomRows = graded;
            }
            config.Version = 29;
            config.Save();
        }

        // v30: FRU's calls re-read off the official sheet.
        if (config.Version < 30)
        {
            foreach (var f in config.Fights)
                if (f.TerritoryId == Builtin.FruTerritory)
                    host.SnapshotFight(f, "before the sheet's calls were re-read");
            ResetDutyFights(config, f => f.TerritoryId == Builtin.FruTerritory);
            config.Version = 30;
            config.Save();
        }

        // v31: the five legacy ultimates rebuilt, anchors fixed.
        if (config.Version < 31)
        {
            foreach (var f in config.Fights)
            {
                if (!Builtin.Has(f.TerritoryId) || f.CustomSlots.Count > 0) continue;
                f.SyncPoints = Builtin.SyncPoints(f.TerritoryId);
                var graded = Builtin.CustomRows(f.TerritoryId);
                if (graded.Count > 0) f.CustomRows = graded;
            }
            config.Version = 31;
            config.Save();
        }

        // v32: an official fight is named by Builtin.
        if (config.Version < 32)
        {
            foreach (var f in config.Fights)
            {
                if (!Builtin.Has(f.TerritoryId) || f.CustomSlots.Count > 0) continue;
                var proper = Builtin.Name(f.TerritoryId);
                if (!string.IsNullOrEmpty(proper)) f.Name = proper;
            }
            config.Version = 32;
            config.Save();
        }

        // v33: the anchor repairs, and the DMU calls that moved.
        if (config.Version < 33)
        {
            RefreshBuiltins(config, Builtin.DmuTerritory, DmuRetimed);
            config.Version = 33;
            config.Save();
        }

        // v34: the same again for the rest of the roster.
        if (config.Version < 34)
        {
            RefreshBuiltins(config, Builtin.M2sTerritory, M2sRetimed);
            config.Version = 34;
            config.Save();
        }

        // v35: the meter's slim header became the default look.
        if (config.Version < 35)
        {
            config.MeterHeaderStyle = 1;
            config.Version = 35;
            config.Save();
        }

        // v36: meter tabs default to DPS and HPS; renames stay.
        if (config.Version < 36)
        {
            if (config.MeterTabNameDamage == "Damage") config.MeterTabNameDamage = "DPS";
            if (config.MeterTabNameHealing == "Healing") config.MeterTabNameHealing = "HPS";
            config.Version = 36;
            config.Save();
        }

        // v37: meter highlight, title and border became own colors.
        if (config.Version < 37)
        {
            config.MeterHighlightColor = config.MeterYouColor;
            config.MeterTitleColor = config.MeterTextColor;
            config.MeterBorderColor = (config.MeterAccentColor & 0x00FFFFFF) | 0x2E000000;
            config.Version = 37;
            config.Save();
        }

        // v38: drop derived downtimes a fight now ships correctly.
        if (config.Version < 38)
        {
            foreach (var f in config.Fights) DropStaleDowntimes(f);
            config.Version = 38;
            config.Save();
        }

        // v39: the meter grew healing-share and shield columns.
        if (config.Version < 39)
        {
            var heal = config.MeterHealColumns;
            if (!heal.Contains("healpct")) heal.Insert(heal.IndexOf("hps") + 1, "healpct");
            if (!heal.Contains("dshield"))
            {
                var over = heal.IndexOf("overheal");
                heal.Insert(over >= 0 ? over : heal.Count, "dshield");
            }
            config.Version = 39;
            config.Save();
        }

        // v40: usage windows replace the offsets the old auto-timer wrote.
        if (config.Version < 40)
        {
            // Only a plan the solver actually ran on, so hand tweaks survive.
            if (config.AutoCooldownTiming)
            {
                var seen = new HashSet<List<MitLine>>();
                void ClearSolved(List<MitLine>? lines)
                {
                    // A slot's stash is often the very same list as fight.Lines.
                    if (lines == null || !seen.Add(lines)) return;
                    foreach (var l in lines)
                    {
                        if (l == null || l.OffsetManual) continue;
                        l.OffsetSeconds = 0f;
                        l.CoverUntil = 0f;
                    }
                }
                foreach (var f in config.Fights)
                {
                    ClearSolved(f.Lines);
                    if (f.SavedSlots != null)
                        foreach (var slot in f.SavedSlots.Values) ClearSolved(slot);
                }
                config.AutoCooldownTiming = false;
            }
            config.Version = 40;
            config.Save();
        }

        // v41: the classic call plate is opt-in, so it stops surprising anyone on 1.0.0.426.
        if (config.Version < 41)
        {
            config.OverlayCallPanel = false;
            config.Version = 41;
            config.Save();
        }

        // v43: a windowed call leads by its own setting. Adopting the plain lead
        // here (as v42 briefly did) defeats the point - the two are split because
        // a window wants a SHORT lead: the window itself is the "you have time"
        // signal, and a long lead on top only stretches the bar. So take the
        // standalone default and let it be tuned from there.
        if (config.Version < 43)
        {
            config.UseWindowLeadSeconds = 2f;
            config.Version = 43;
            config.Save();
        }

        // v44: the Dancing Mad sheet re-timed rows, renamed mechanics onto their
        // real casts, and dropped the calls with no cast behind them. A plan holds
        // the lines it was baked from, and the top-up only clears a stale one
        // sitting within 6s of its replacement - so a row that moved further (a
        // Curing Waltz by 9s) and every dropped row would linger as a duplicate or
        // an orphan. Clear the ones the sheet no longer bakes and let the top-up
        // restore the current set.
        // Only with the sheet on disk: against a missing sheet the prune would
        // read an empty bake and strip the whole plan. Left unstamped, so the
        // cleanup still runs once the sheet is back.
        if (config.Version < 44 && Builtin.Has(Builtin.DmuTerritory))
        {
            foreach (var f in config.Fights)
            {
                if (f.TerritoryId != Builtin.DmuTerritory || f.CustomSlots.Count > 0) continue;
                host.SnapshotFight(f, "before the re-timed sheet rows were cleaned up");
                PruneStaleBaked(f);
                // UpdateLines, not ApplySlot: migrations run from the plugin
                // constructor, off the main thread, and ApplySlot resolves tank
                // priority - which reads the local player and throws there.
                if (!string.IsNullOrEmpty(f.Slot)) Builtin.UpdateLines(f, f.Slot);
            }
            config.Version = 44;
            config.Save();
        }

        // v45: an older bake saved built-in lines without their job gate, so a
        // WHM's plan carried the AST calls too (and SCH carried SGE's). The
        // grid has nothing to filter on, so both jobs' calls render in both
        // columns. The sheet has always carried the gate; only the saved copy
        // lost it, and a saved plan never re-bakes itself.
        if (config.Version < 45)
        {
            var fixedUp = 0;
            foreach (var f in config.Fights)
            {
                if (f.CustomSlots.Count > 0 || string.IsNullOrEmpty(f.Slot)) continue;
                if (!Builtin.Has(f.TerritoryId)) continue;
                fixedUp += RestoreLostJobGates(f, f.Slot);
                foreach (var key in new List<string>(f.SavedSlots.Keys))
                    fixedUp += RestoreLostJobGates(f, key, f.SavedSlots[key]);
            }
            if (fixedUp > 0)
                EncounterLog.Info($"[FrenMits] restored the job gate on {fixedUp} saved line(s).");
            config.Version = 45;
            config.Save();
        }

        // v46: the top-up matched a saved call on (time, mechanic) alone, so
        // where a healer pair shares a mechanic the saved line for one job
        // counted as the other job's line too, and that job's row was never
        // added. v45 gated what was there; this restores what was missing.
        if (config.Version < 46)
        {
            foreach (var f in config.Fights)
            {
                if (f.CustomSlots.Count > 0 || string.IsNullOrEmpty(f.Slot)) continue;
                if (!Builtin.Has(f.TerritoryId)) continue;
                var before = f.Lines.Count;
                // UpdateLines, not ApplySlot: migrations run off the main thread
                // and ApplySlot resolves tank priority, which reads the player.
                Builtin.UpdateLines(f, f.Slot);
                if (f.Lines.Count != before)
                    EncounterLog.Info($"[FrenMits] {f.Name}: restored "
                                      + $"{f.Lines.Count - before} missing call(s).");
            }
            config.Version = 46;
            config.Save();
        }

        // v47: the sheets got back the calls they name again while an earlier
        // press is still running - FRU's Temperance across three mechanics, and
        // the rest of what the reference sheet carries. A saved plan never
        // re-bakes itself, and a column you are not standing in never even sees
        // the active slot's top-up, so hand the new rows to both.
        if (config.Version < 47)
        {
            foreach (var f in config.Fights)
            {
                if (f.CustomSlots.Count > 0 || string.IsNullOrEmpty(f.Slot)) continue;
                if (!Builtin.Has(f.TerritoryId)) continue;
                var before = f.Lines.Count;
                Builtin.UpdateLines(f, f.Slot);
                var added = f.Lines.Count - before;
                foreach (var (slot, saved) in f.SavedSlots)
                    added += Builtin.TopUpSaved(f, slot, saved);
                if (added > 0)
                    EncounterLog.Info($"[FrenMits] {f.Name}: added {added} call(s) the sheet carries.");
            }
            config.Version = 47;
            config.Save();
        }

        // v48: v45 handed a saved line the job gate off ANY gated sheet row at
        // its mechanic when that row was the only gated one there, without
        // checking the actions matched. UMAD 5:43 carries a PLD's Passage of
        // Arms next to the melee's ungated Feint, so the Feint came out gated
        // to PLD and vanished from every other job's column. Take back a gate
        // the sheet does not give, and refresh the mechanic names a saved plan
        // froze before the sheet's spelling was corrected.
        if (config.Version < 48)
        {
            var ungated = 0;
            var renamed = 0;
            foreach (var f in config.Fights)
            {
                if (f.CustomSlots.Count > 0 || !Builtin.Has(f.TerritoryId)) continue;
                if (!string.IsNullOrEmpty(f.Slot)) ungated += DropGatesTheSheetDoesNotGive(f, f.Slot, f.Lines);
                foreach (var (slot, saved) in f.SavedSlots)
                    ungated += DropGatesTheSheetDoesNotGive(f, slot, saved);
                renamed += AdoptTheSheetsMechanicNames(f);
            }
            if (ungated > 0)
                EncounterLog.Info($"[FrenMits] freed {ungated} call(s) from a job gate the sheet never gave them.");
            if (renamed > 0)
                EncounterLog.Info($"[FrenMits] corrected {renamed} saved mechanic name(s).");
            config.Version = 48;
            config.Save();
        }

        // v49: v48 matched a renamed mechanic row by letter distance, which is
        // too blunt - "Negatron Stream" is four letters from "Electron Stream"
        // and stayed behind as a second, empty row. Match by position instead.
        if (config.Version < 49)
        {
            var renamed = 0;
            foreach (var f in config.Fights)
            {
                if (f.CustomSlots.Count > 0 || !Builtin.Has(f.TerritoryId)) continue;
                renamed += AdoptTheSheetsMechanicNames(f);
                renamed += AdoptTheSheetsMechanicNamesOnLines(f);
            }
            if (renamed > 0)
                EncounterLog.Info($"[FrenMits] corrected {renamed} saved mechanic name(s).");
            config.Version = 49;
            config.Save();
        }
    }

    // A saved line whose action IS an ungated row on the sheet must not carry a
    // job gate. Only an untouched sheet line is considered: your own edits and
    // job extras keep whatever they say.
    private static int DropGatesTheSheetDoesNotGive(FightProfile f, string slot, List<MitLine> lines)
    {
        if (string.IsNullOrEmpty(slot)) return 0;
        var baked = Builtin.BuildLines(f.TerritoryId, slot);
        if (baked.Count == 0) return 0;

        var n = 0;
        foreach (var l in lines)
        {
            if (l.Custom || l.Personal || l.IsJobExtra || l.Jobs.Count == 0) continue;
            var here = baked.FindAll(b => MathF.Abs(b.Time - l.Time) < 0.9f
                                          && string.Equals(b.Mechanic.Trim(), l.Mechanic.Trim(),
                                                           StringComparison.OrdinalIgnoreCase));
            // The sheet row this line actually is. If the sheet gates that row
            // too, the gate is real and stays.
            var mine = here.Find(b => string.Equals(b.Action.Trim(), l.Action.Trim(),
                                                    StringComparison.OrdinalIgnoreCase));
            if (mine == null || mine.Jobs.Count > 0) continue;
            l.Jobs = new List<string>();
            n++;
        }
        return n;
    }

    // A plan keeps its own copy of the mechanic list, and it is only re-seeded
    // when a column is applied or reset. So a sheet whose name was corrected
    // leaves the plan showing the old one beside the new one as two rows.
    //
    // Matched by position, not by spelling: a plan row the sheet no longer names,
    // sitting where the sheet names something the plan is missing, IS that row
    // renamed. Letter distance was too blunt - "Negatron Stream" is four letters
    // from "Electron Stream" and would have been left behind.
    private static int AdoptTheSheetsMechanicNames(FightProfile f)
    {
        var sheet = Builtin.CustomRows(f.TerritoryId);
        if (sheet.Count == 0) return 0;

        bool Same(string a, string b)
            => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        var n = 0;
        foreach (var r in f.CustomRows)
        {
            if (sheet.Any(s => Same(s.Mechanic, r.Mechanic))) continue;   // still a real name
            // A name the plan already carries AT THIS MOMENT is spoken for. The
            // check has to be per-moment: a mechanic that repeats is renamed
            // once per instance, and a global check stops after the first.
            var here = sheet.FindAll(s => MathF.Abs(s.Time - r.Time) < 0.9f
                                          && !f.CustomRows.Any(x => Same(x.Mechanic, s.Mechanic)
                                                                    && MathF.Abs(x.Time - s.Time) < 0.9f));
            // Exactly one candidate, or there is no telling which it became.
            if (here.Count != 1) continue;
            r.Mechanic = here[0].Mechanic;
            n++;
        }
        return n;
    }

    // The saved CALLS carry the mechanic name too, and a renamed sheet strands
    // them: they stop pairing with the bake, so they draw their own duplicate
    // row and the next top-up adds the sheet's copy beside them.
    private static int AdoptTheSheetsMechanicNamesOnLines(FightProfile f)
    {
        var n = 0;
        var columns = new List<(string Slot, List<MitLine> Lines)>();
        if (!string.IsNullOrEmpty(f.Slot)) columns.Add((f.Slot, f.Lines));
        foreach (var (slot, saved) in f.SavedSlots) columns.Add((slot, saved));

        bool Same(string a, string b)
            => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        foreach (var (slot, lines) in columns)
        {
            var baked = Builtin.BuildLines(f.TerritoryId, slot);
            if (baked.Count == 0) continue;
            var drop = new List<MitLine>();
            foreach (var l in lines)
            {
                if (string.IsNullOrWhiteSpace(l.Mechanic)) continue;
                var here = baked.FindAll(b => MathF.Abs(b.Time - l.Time) < 0.9f);
                if (here.Count == 0) continue;
                if (here.Any(b => Same(b.Mechanic, l.Mechanic))) continue;
                // One name at this moment, or there is no telling which it became.
                var names = here.Select(b => b.Mechanic.Trim())
                                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (names.Count != 1) continue;

                // A plan can hold the call under BOTH names - the top-up added
                // the sheet's copy beside the stranded one. Renaming would leave
                // two identical rows, so let the one already correct stand.
                var twin = lines.Any(o => !ReferenceEquals(o, l) && !drop.Contains(o)
                                          && MathF.Abs(o.Time - l.Time) < 0.01f
                                          && Same(o.Mechanic, names[0])
                                          && Same(o.Action, l.Action)
                                          && o.Jobs.Count == l.Jobs.Count
                                          && !o.Jobs.Except(l.Jobs, StringComparer.OrdinalIgnoreCase).Any());
                if (twin) { drop.Add(l); continue; }

                l.Mechanic = names[0];
                n++;
            }
            foreach (var d in drop) { lines.Remove(d); n++; }
        }
        return n;
    }

    // Give an ungated saved line back the gate its sheet row carries. Matched on
    // (time, mechanic) within the slot; where a mechanic has one row per job the
    // action text picks between them. Anything still ambiguous is left alone.
    private static int RestoreLostJobGates(FightProfile f, string slot, List<MitLine>? target = null)
    {
        var lines = target ?? f.Lines;
        var baked = Builtin.BuildLines(f.TerritoryId, slot);
        if (baked.Count == 0) return 0;

        var n = 0;
        foreach (var l in lines)
        {
            if (l.Custom || l.Jobs.Count > 0) continue;
            var here = baked.FindAll(b => b.Jobs.Count > 0
                                          && MathF.Abs(b.Time - l.Time) < 0.9f
                                          && string.Equals(b.Mechanic.Trim(), l.Mechanic.Trim(),
                                                           StringComparison.OrdinalIgnoreCase));
            if (here.Count == 0) continue;

            // The action has to line up, however few candidates there are. A
            // mechanic can carry one gated row that is nothing to do with this
            // line - UMAD 5:43 has a PLD's Passage of Arms beside the melee's
            // ungated Feint - and handing that gate over hides the call from
            // everyone but the one job.
            var pick = here.Find(b => string.Equals(b.Action.Trim(), l.Action.Trim(), StringComparison.OrdinalIgnoreCase))
                  ?? here.Find(b => b.Action.Contains(l.Action.Trim(), StringComparison.OrdinalIgnoreCase)
                                    || l.Action.Contains(b.Action.Trim(), StringComparison.OrdinalIgnoreCase))
                  // The saved copy may still be written in the sheet's old
                  // shorthand, so fall back to what each side actually names.
                  ?? here.Find(b => NamesTheSameMit(b.Action, l.Action));
            if (pick == null) continue;   // nothing this line matches - leave it

            l.Jobs = new List<string>(pick.Jobs);
            n++;
        }
        return n;
    }

    // Do two action cells call for any of the same mit? Resolves shorthand on
    // both sides, so "CU" and "Collective Unconscious" count as a match.
    private static bool NamesTheSameMit(string a, string b)
    {
        var left = AbilityBook.BuffsIn(a).Select(x => x.Name).ToList();
        if (left.Count == 0) return false;
        foreach (var (name, _) in AbilityBook.BuffsIn(b))
            if (left.Contains(name, StringComparer.OrdinalIgnoreCase)) return true;
        return false;
    }

    // Drop every line the sheet no longer bakes anywhere. Only untouched sheet
    // lines go: an edit marks its line Custom (PreserveEdit) and a personal
    // override marks it Personal, so nothing the user wrote is at risk.
    //
    // Matched across ALL slots rather than the line's own, because a tank line
    // borrowed through a PriorityPhase legitimately lives in the other column's
    // list - checking one slot would prune it as stale every time.
    private static void PruneStaleBaked(FightProfile fight)
    {
        var baked = new HashSet<(int Time, string Mech, string Action)>();
        foreach (var slot in Builtin.Slots(fight.TerritoryId))
            foreach (var b in Builtin.BakedLines(fight.TerritoryId, slot))
                baked.Add(Key(b));

        static (int, string, string) Key(MitLine l)
            => ((int)MathF.Round(l.Time * 10f),
                l.Mechanic.Trim().ToLowerInvariant(),
                l.Action.Trim().ToLowerInvariant());

        var seen = new HashSet<List<MitLine>>();
        foreach (var lines in AllLineSets(fight))
        {
            // A slot's stash is often the very same list as fight.Lines.
            if (lines == null || !seen.Add(lines)) continue;
            lines.RemoveAll(l => l != null && !l.Custom && !l.Personal && !baked.Contains(Key(l)));
        }

        // A tombstone for a call the sheet dropped would suppress nothing, and
        // would keep counting toward the "deleted sheet calls" restore prompt.
        fight.DeletedCalls.RemoveAll(d => !Builtin.Slots(fight.TerritoryId).Any(s =>
            Builtin.BakedLines(fight.TerritoryId, s).Any(b => Builtin.MatchesTombstone(d, d.Slot, b))));
    }

    // A fight's verified windows drop a profile's derived ones.
    public static int DropStaleDowntimes(FightProfile fight)
    {
        if (fight.CustomDowntimes.Count == 0) return 0;
        if (Downtimes.For(fight.TerritoryId).Count == 0) return 0;
        var dropped = fight.CustomDowntimes.Count;
        fight.CustomDowntimes.Clear();
        return dropped;
    }

    // Hand built-ins the current anchors, and re-time moved rows.
    private static void RefreshBuiltins(Configuration config, ushort retimed,
                                        (float Old, string Mechanic, float New)[] moves)
    {
        foreach (var f in config.Fights)
        {
            if (!Builtin.Has(f.TerritoryId) || f.CustomSlots.Count > 0) continue;
            f.SyncPoints = Builtin.SyncPoints(f.TerritoryId);
            var graded = Builtin.CustomRows(f.TerritoryId);
            if (graded.Count > 0) f.CustomRows = graded;
            if (f.TerritoryId != retimed) continue;

            foreach (var lines in AllLineSets(f))
                foreach (var l in lines)
                    foreach (var (old, mech, now) in moves)
                        if (MathF.Abs(l.Time - old) < 0.6f && l.Mechanic == mech)
                        {
                            l.Time = now;
                            break;
                        }
            foreach (var lines in AllLineSets(f))
                lines.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
    }

    // M2S rows re-timed in 1.0.0.374.
    private static readonly (float Old, string Mechanic, float New)[] M2sRetimed =
    {
        (136f, "Loveseeker", 144f),
        (150f, "Love Me Tender", 158f),
        (187f, "Honey B. Finale", 195f),
        (201f, "Killer Sting", 209f),
    };

    // Dancing Mad rows re-timed in 1.0.0.373.
    private static readonly (float Old, string Mechanic, float New)[] DmuRetimed =
    {
        (507f, "Ultima Blaster", 511f),
        (763f, "Grand Cross", 759f),
        (778f, "Grand Cross", 774f),
        (793f, "Grand Cross", 789f),
        (971f, "Celestriad", 963f),
    };

    // The live slot plus every saved one.
    private static IEnumerable<List<MitLine>> AllLineSets(FightProfile fight)
    {
        yield return fight.Lines;
        foreach (var key in fight.SavedSlots.Keys)
            yield return fight.SavedSlots[key];
    }

    // A custom sheet whose duty has become an official fight.
    public static int AdoptSupersededSheets(Configuration config, Action<FightProfile, string>? snapshot = null)
    {
        var adopted = 0;
        foreach (var f in config.Fights)
        {
            if (f.CustomSlots.Count == 0 || !Builtin.Has(f.TerritoryId)) continue;
            snapshot?.Invoke(f, "before the official sheet took over");
            f.CustomSlots.Clear();
            f.SavedSlots.Clear();
            f.CustomRows.Clear();
            // The name goes with everything else.
            var official = Builtin.Name(f.TerritoryId);
            if (!string.IsNullOrEmpty(official)) f.Name = official;
            if (!string.IsNullOrEmpty(f.Slot)) Builtin.ResetSlot(f, f.Slot);
            else { f.Lines.Clear(); f.AutoLoaded = false; }
            adopted++;
        }
        return adopted;
    }

    // The clean-reset shape shared by v6, v7 and v11 to v14.
    private static void ResetDutyFights(Configuration config, Func<FightProfile, bool> match)
    {
        foreach (var f in config.Fights)
        {
            if (!match(f)) continue;
            f.SavedSlots.Clear();
            if (!string.IsNullOrEmpty(f.Slot))
                Builtin.ResetSlot(f, f.Slot);
            else { f.Lines.Clear(); f.AutoLoaded = false; }
        }
    }

    private static void SnapshotDmu(IMigrationHost host, string reason)
    {
        foreach (var f in host.Config.Fights)
            if (f.TerritoryId == Builtin.DmuTerritory)
                host.SnapshotFight(f, reason);
    }
}
