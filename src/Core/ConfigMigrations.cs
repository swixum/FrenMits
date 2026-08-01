using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

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
            ResetDutyFights(config, f => IkuyaTimelines.Has(f.TerritoryId));
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
            DmuRebake.SmartRebake(config);
            config.Version = 8;
            config.Save();
        }

        // v9: re-run the re-bake with the hardened de-overlap.
        if (config.Version < 9)
        {
            DmuRebake.SmartRebake(config);
            config.Version = 9;
            config.Save();
        }

        // v10: ship the full sheet refresh, keeping custom lines.
        if (config.Version < 10)
        {
            DmuRebake.SmartRebake(config);
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
            DmuRebake.SmartRebake(config);
            config.Version = 16;
            config.Save();
        }

        // v17: restore the WHM Asylum calls the v16 bake dropped.
        if (config.Version < 17)
        {
            SnapshotDmu(host, "before restoring the WHM Asylum calls");
            DmuRebake.SmartRebake(config);
            config.Version = 17;
            config.Save();
        }

        // v18: added tank and job-mit lines upgraded to sheet v5.0.
        if (config.Version < 18)
        {
            SnapshotDmu(host, "before the v5.0 tank and job-mitigation update");
            DmuRebake.UpgradeTankAndExtraLines(config);
            config.Version = 18;
            config.Save();
        }

        // v19: the sheet's P2 "Ultimate Embrance" typo is baked fixed.
        if (config.Version < 19)
        {
            SnapshotDmu(host, "before the Ultimate Embrace typo fix");
            DmuRebake.SmartRebake(config);
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
            config.AutoCooldownTiming = false;
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
