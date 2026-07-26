using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Every versioned config migration, run once at load in order. Each block
// bumps Config.Version and saves, so a crash mid-chain resumes where it left
// off. Keep new migrations at the bottom.
public static class ConfigMigrations
{
    public static void Run(IMigrationHost host)
    {
        var config = host.Config;

        // v2: split the upcoming list into its own timeline window and switch the
        // main call to the clean "Raidwide (3.3)" countdown shown 3s ahead.
        if (config.Version < 2)
        {
            config.HeadlineFormat = "{action} ({remaining})";
            config.ShowCountdownNumber = false;
            config.WarningSeconds = 3f;
            config.Version = 2;
            config.Save();
        }

        // v3: assign sidebar categories: built-ins are ultimates, everything else
        // starts in "Other".
        if (config.Version < 3)
        {
            foreach (var f in config.Fights)
                if (string.IsNullOrEmpty(f.Category))
                    f.Category = Builtin.Has(f.TerritoryId) ? "Ultimate" : "Other";
            config.Version = 3;
            config.Save();
        }

        // v4: per-pull diagnostics on by default (local only), flipped on once for
        // existing profiles.
        if (config.Version < 4)
        {
            config.Diagnostics = true;
            config.Version = 4;
            config.Save();
        }

        // v5: the Ikuya sheet had a big v3.0 mit rework, so rebake all built-in
        // fights once to clear stale lines and start fresh on the new plan.
        if (config.Version < 5)
        {
            host.ResetAllBuiltins();
            config.Version = 5;
            config.Save();
        }

        // v6: the legacy ultimate timelines (UCOB/UWU/TEA/DSR/TOP) were re-timed
        // from real logs clears (the old cactbot-derived times were inflated 2-4x).
        if (config.Version < 6)
        {
            ResetDutyFights(config, f => IkuyaTimelines.Has(f.TerritoryId));
            config.Version = 6;
            config.Save();
        }

        // v7: Dancing Mad mits resynced to the Ikuya sheet v4.0 (action + timing
        // overwrites, line splits, new rows) and WHM Asylum added from logs.
        if (config.Version < 7)
        {
            ResetDutyFights(config, f => f.TerritoryId == Builtin.DmuTerritory);
            config.Version = 7;
            config.Save();
        }

        // v8: re-bake DMU to the new timeline, but KEEP custom lines people added -
        // a smart merge that only replaces the lines matching the previous bake
        // (DmuLegacy snapshot).
        if (config.Version < 8)
        {
            DmuRebake.SmartRebake(config);
            config.Version = 8;
            config.Save();
        }

        // v9: re-run the smart re-bake with the hardened de-overlap so nothing
        // doubles up, and flag surviving custom lines so future sheet updates keep
        // them cleanly.
        if (config.Version < 9)
        {
            DmuRebake.SmartRebake(config);
            config.Version = 9;
            config.Save();
        }

        // v10: ship the full sheet refresh to everyone - re-bake DMU to the latest
        // baked timings (the smart merge keeps every custom line people added).
        if (config.Version < 10)
        {
            DmuRebake.SmartRebake(config);
            config.Version = 10;
            config.Save();
        }

        // v11: a deliberate one-time CLEAN reset of Dancing Mad to the sheet, wiping
        // any custom lines too (to clear overlapping/stale data from earlier merges).
        if (config.Version < 11)
        {
            ResetDutyFights(config, f => f.TerritoryId == Builtin.DmuTerritory);
            config.Version = 11;
            config.Save();
        }

        // v12: force another clean reset of Dancing Mad for everyone so the newly
        // re-timed sheet lands cleanly.
        if (config.Version < 12)
        {
            ResetDutyFights(config, f => f.TerritoryId == Builtin.DmuTerritory);
            config.Version = 12;
            config.Save();
        }

        // v13: hard reset Dancing Mad again so everyone is freshly baked from the
        // current sheet (now that generic mits resolve to each job's icon).
        if (config.Version < 13)
        {
            ResetDutyFights(config, f => f.TerritoryId == Builtin.DmuTerritory);
            config.Version = 13;
            config.Save();
        }

        // v14: hard reset Dancing Mad once more so the latest baked timeline is in
        // for everyone (pairs with calls now showing each job's real ability name).
        if (config.Version < 14)
        {
            ResetDutyFights(config, f => f.TerritoryId == Builtin.DmuTerritory);
            config.Version = 14;
            config.Save();
        }

        // v15: normalize any em dash in stored fight names to a plain hyphen, which
        // the game font otherwise renders as an empty box.
        if (config.Version < 15)
        {
            foreach (var f in config.Fights)
                if (f.Name.Contains('—'))
                    f.Name = f.Name.Replace('—', '-');
            config.Version = 15;
            config.Save();
        }

        // v16: Dancing Mad re-baked to the Ikuya sheet v5.0 (P3 Reprisal/Addle
        // moves, P4 healer reshuffle, P5 Forsaken hits renamed and reassigned).
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

        // v18: upgrade tank-buster and BRD/MNK/PLD job-mitigation lines users
        // already added to the sheet v5.0 data, keeping edited lines.
        if (config.Version < 18)
        {
            SnapshotDmu(host, "before the v5.0 tank and job-mitigation update");
            DmuRebake.UpgradeTankAndExtraLines(config);
            config.Version = 18;
            config.Save();
        }

        // v19: the sheet's "Ultimate Embrance" typo (P2, 3:41) is now baked
        // corrected as "Ultimate Embrace".
        if (config.Version < 19)
        {
            SnapshotDmu(host, "before the Ultimate Embrace typo fix");
            DmuRebake.SmartRebake(config);
            config.Version = 19;
            config.Save();
        }

        // v20: migrate the old M12S placeholder zone (1320) to the real one (1327).
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

        // v21: force auto cooldown timing off once for existing configs (it shipped
        // default-on in early builds, then became a big opt-in feature).
        if (config.Version < 21)
        {
            config.AutoCooldownTiming = false;
            config.Version = 21;
            config.Save();
        }

        // v22: switch audio on for SMN summon cues already imported into a config
        // (they shipped silent but now speak each call).
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

        // v23: the logs client secret moved to DPAPI-encrypted storage; pull
        // any old plaintext value into the encrypted field and wipe the old key.
        if (config.Version < 23)
        {
            config.MigrateFflogsSecret();
            config.Version = 23;
            config.Save();
        }

        // v24: phase dividers on the board are on by default. They shipped OFF in
        // 1.0.0.344-345, so those two builds wrote a stored "false" that a changed
        // C# default would never reach.
        if (config.Version < 24)
        {
            config.UpcomingBoardPhases = true;
            config.Version = 24;
            config.Save();
        }

        // v25: M9S/M10S/M11S were built as custom sheets and Auto-planned before the
        // planner stopped handing the caster column a "Party Mit" it has no button
        // for. Now that they ship as official fights those sheets aren't editable in
        // game, so a saved copy - which wins over the bake - would keep calling it
        // forever with no way to take it out. Reset them onto the corrected bake,
        // snapshotting first so the old plan is still restorable.
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

        // v26: the sheets built-ins are transcribed from repeat a mit on every hit
        // one press covers, which reads as a second press of a button that has most
        // of its recast left - FRU asks the melee for Feint at 5:23 and again at
        // 5:33. New bakes drop those; this takes them out of the plans already
        // saved, since an official sheet can't be edited in game. Nothing you wrote
        // yourself is touched, and the hit still shows what covers it as a
        // carry-over arrow.
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

        // v27: FRU's Pandora phase was stacked on top of itself. Akh Morn 1, 2 and
        // 3 all sat at 17:48 and both Polarizing Strikes at 18:27, because the rows
        // were matched to a cast id and every repeat landed on the first one - so
        // two thirds of the Akh Morn calls never fired at all. The logs put them
        // 113s and 91s apart, the same in all eight kills read, and three stray
        // rows tagged P4 turned out to be Pandora's too. Re-bake so the moved calls
        // land, snapshotting first because a saved plan wins over the bake.
        if (config.Version < 27)
        {
            foreach (var f in config.Fights)
                if (f.TerritoryId == Builtin.FruTerritory)
                    host.SnapshotFight(f, "before the Pandora phase was un-stacked");
            ResetDutyFights(config, f => f.TerritoryId == Builtin.FruTerritory);
            config.Version = 27;
            config.Save();
        }

        // v28: FRU's timeline was only ever the mit sheet, so the board had 31 rows
        // for a twenty-minute fight and nothing at all to show for the six minutes
        // of the duo. It carries every damaging cast now, measured from six kills
        // and checked against five more it wasn't built from, with the severity and
        // tank-buster flags it never had. Re-bake so the new rows land.
        if (config.Version < 28)
        {
            foreach (var f in config.Fights)
                if (f.TerritoryId == Builtin.FruTerritory)
                    host.SnapshotFight(f, "before the full timeline landed");
            ResetDutyFights(config, f => f.TerritoryId == Builtin.FruTerritory);
            config.Version = 28;
            config.Save();
        }

        // v29: severity and tank-buster flags for the fights that never had any -
        // Dancing Mad and all five legacy ultimates drew every mechanic as the same
        // flat bar, with no buster shields and no DPS-check gates. Measured from six
        // kills each.
        //
        // A refresh, not a re-bake: the grades live on the built-in and the new rows
        // carry no calls, so nothing in anyone's plan has to move. Every clean reset
        // since v14 has deliberately kept custom lines, and this keeps them too.
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

        // v30: FRU's calls re-read off the official sheet, row by row.
        //
        // The first pass matched the sheet's rows to the timeline by name and kept
        // the earliest hit for each, so a mechanic the fight does twice collected
        // both rows' calls on the first one and left the second blank. Cyclonic
        // Break 2's mits sat on break 1, both Burnished Glory rows on the 1:26 one,
        // the Light Rampant stack on the Mirror Mirror one, the Gaia tab's Dark
        // Water 49s early, and the duo's Path of Light five minutes late inside
        // Pandora - a plan for a mechanic that had already happened. The whole duo
        // phase and both Relativity sets had no calls at all, though the sheet
        // plans them.
        //
        // A re-bake rather than a refresh, because unlike v29 these calls do move.
        if (config.Version < 30)
        {
            foreach (var f in config.Fights)
                if (f.TerritoryId == Builtin.FruTerritory)
                    host.SnapshotFight(f, "before the sheet's calls were re-read");
            ResetDutyFights(config, f => f.TerritoryId == Builtin.FruTerritory);
            config.Version = 30;
            config.Save();
        }

        // v31: the five legacy ultimates rebuilt, and their resync anchors fixed.
        //
        // The anchors first, because they were the bigger problem. A phase anchor
        // gets a very wide forward window so a clock still in an earlier segment can
        // land on the next phase; UCOB and UWU each had one sitting on an ability the
        // boss also casts much earlier, with no anchor of its own, so meeting that
        // early cast threw the clock most of a phase forward and left it there. Read
        // against six kills apiece that none of the data came from, UCOB's board was
        // more than thirty seconds wrong on 96 readings out of 168 and UWU's on 120
        // out of 150. Both are down to a third of a second now.
        //
        // Then the rows. These sheets name strategy points rather than abilities, so
        // nothing matched a log by name, and the fights gate their phases on the
        // boss's HP, so no fixed time is right for every party - which is why the
        // earlier pass could only grade what it could name. Measuring each row as a
        // distance back to the anchor before it solves both at once, and holds to a
        // third of a second on kills it was never fitted to.
        //
        // A refresh, not a re-bake. Every existing row kept its time, its name and
        // its calls - what changed is the grades on them, the rows added around them
        // (which carry no calls), and the anchors, all of which live on the built-in.
        // Saved plans hold their own copy of the anchor list, so that copy is the one
        // that has to be replaced or the fixed anchors never reach anybody.
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

        // v32: an official fight is named by Builtin, not by whatever it was called
        // on the day it was added.
        //
        // A profile stores its own Name, set once when the fight was added and never
        // revisited, so renaming a built-in in code only reached people who had
        // never added it. Zelenia went on reading "Zelenia (EX)" in the sidebar next
        // to a plain "Enuo" and "Doomtrain" - and those two were only plain because
        // THEY predate the suffix. The list was a record of release order rather
        // than a list of fights.
        //
        // Custom sheets keep their names: those are the user's to choose.
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
    }

    // A custom sheet whose duty has since become an official fight.
    //
    // This is how every official fight starts: someone builds it as a custom sheet,
    // and it ships as a built-in a release later. The moment it does, that sheet
    // stops being editable in game - Build and Auto-plan are custom-only - while
    // the user's own copy still WINS over the bake, because saved slots always do.
    // So whatever it held before it was cleaned up stays on the board with nothing
    // left to reach it: Doomtrain kept 53 rows of boss auto-attack the shipped
    // version doesn't have, and M9S-M11S kept caster calls no caster can press.
    //
    // Unversioned on purpose - it runs every load, because the next fight to be
    // promoted needs it too and shouldn't need a migration written for it.
    // CustomSlots is the marker (an official fight has no business carrying its own
    // column list), and clearing it is what stops this running twice.
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
            if (!string.IsNullOrEmpty(f.Slot)) Builtin.ResetSlot(f, f.Slot);
            else { f.Lines.Clear(); f.AutoLoaded = false; }
            adopted++;
        }
        return adopted;
    }

    // The clean-reset shape shared by v6/v7/v11-v14: wipe the duty's saved slots
    // and freshly bake the active one (or leave it for auto-load).
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
