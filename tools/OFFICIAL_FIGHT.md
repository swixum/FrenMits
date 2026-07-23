# Making an official fight (dev pipeline)

How a log-built sheet becomes a built-in fight. These tools are dev-only and are
NOT shipped in the plugin DLL. Run them from `tools/`.

You need FFLogs API creds. The tools read them from a `FrenMits.json`
(`FflogsClientId` / `FflogsClientSecret`), the same file the plugin stores, or
pass `--id` / `--secret`.

## The steps

1. **Build the skeleton in-game.** In a custom sheet: `Build > Build from
   FFLogs...`, then either type the fight name (pulls the current top-speed kill)
   or paste a specific log. Its casts become rows + resync anchors, graded by real
   unmitigated damage, with the untargetable windows derived from the log's gaps.

2. **Auto-plan it.** `Build > Auto-plan mits...` fills every column. Tweak by hand
   in-game until it reads right.

3. **Export the profile.** `Plan > Export...` (or hand a single-fight JSON /
   the whole `FrenMits.json` to the tools below).

4. **Verify timings against several kills.** You imported ONE log; this checks it
   against many so the anchors, seams and windows are consensus, not one pull's
   quirks. It can write a timing-corrected copy for the generator.

   ```
   verify_fight_logs.py PROFILE.json --reports LINK1 LINK2 LINK3 --creds FrenMits.json \
       [--tolerance 2.0] [--write-corrected corrected_profile.json]
   ```

5. **Cross-check the plan against the pros (standard pre-ship check).** Shows what
   the world's best parties actually pressed, on YOUR sheet clock, next to what
   Auto-plan assigned. Read it before shipping: it flags moments the pros mit but
   the plan doesn't, and plan presses with no pro coverage. It never writes into
   the plan; the assignments stay the Auto-planner's (and yours).

   ```
   pro_plan_crosscheck.py --name "FIGHT NAME" --profile PROFILE.json --creds FrenMits.json \
       [--logs 8] [--metric speed|execution] [--window 5]
   ```

   Only trust the sheet-clock times inside the anchored span (the tool drops the
   rest). A thin profile with few/duplicated anchors converts the opener and tail
   poorly; a real generated profile has dense, clean anchors and lines up tightly.

6. **Generate the built-in fight.** Bakes the Auto-plan into every column and
   emits a self-contained `<Class>Data.cs`, plus the exact `Builtin.cs` /
   `Downtimes.cs` edits to wire it in.

   ```
   gen_official_fight.py PROFILE.json --class P8s --name "Abaddon (P8S)" \
       --category Savage [--territory 1088 | --name-match Abaddon | --index 0] \
       [--out ../src/Data/P8sData.cs]
   ```

7. **Wire it in and eyeball.** Apply the printed registration edits, build, and
   review the baked plan in-game. The mit assignments are the Auto-planner's, so a
   human should confirm them (step 5's cross-check is your reference).

## Notes

- Steps 4 and 5 are the "make it consensus" pair: 4 pins the *timings* across
  kills, 5 sanity-checks the *mit choices* against real practice.
- The whole pipeline runs from one exported profile; nothing is guessed and no
  step writes back into the plan except `verify --write-corrected`, which only
  nudges existing rows toward the log median (it never fabricates a planned row).
