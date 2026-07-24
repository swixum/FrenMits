using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Every versioned config migration, run once at load in order. Each block
// bumps Config.Version and saves, so a crash mid-chain resumes where it left
// off. Keep new migrations at the bottom.
public static class ConfigMigrations
{
    public static void Run(Plugin plugin)
    {
        var config = plugin.Config;

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
            plugin.ResetAllBuiltins();
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
            SnapshotDmu(plugin, "before the sheet v5.0 update");
            DmuRebake.SmartRebake(config);
            config.Version = 16;
            config.Save();
        }

        // v17: restore the WHM Asylum calls the v16 bake dropped.
        if (config.Version < 17)
        {
            SnapshotDmu(plugin, "before restoring the WHM Asylum calls");
            DmuRebake.SmartRebake(config);
            config.Version = 17;
            config.Save();
        }

        // v18: upgrade tank-buster and BRD/MNK/PLD job-mitigation lines users
        // already added to the sheet v5.0 data, keeping edited lines.
        if (config.Version < 18)
        {
            SnapshotDmu(plugin, "before the v5.0 tank and job-mitigation update");
            DmuRebake.UpgradeTankAndExtraLines(config);
            config.Version = 18;
            config.Save();
        }

        // v19: the sheet's "Ultimate Embrance" typo (P2, 3:41) is now baked
        // corrected as "Ultimate Embrace".
        if (config.Version < 19)
        {
            SnapshotDmu(plugin, "before the Ultimate Embrace typo fix");
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

    private static void SnapshotDmu(Plugin plugin, string reason)
    {
        foreach (var f in plugin.Config.Fights)
            if (f.TerritoryId == Builtin.DmuTerritory)
                plugin.Snapshots.Save(f, reason);
    }
}
