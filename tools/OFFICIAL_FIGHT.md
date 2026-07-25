# Making an official fight (dev pipeline)

How a log-built sheet becomes a built-in fight. These tools are dev-only and are
NOT shipped in the plugin DLL. Run them from `tools/`.

You need FFLogs API creds. They live outside the repo, so they can never be
committed:

    ~/.config/frenmits/fflogs.json      (chmod 600)
    { "client_id": "...", "client_secret": "..." }

The tools pick that up with no flags. `--id` / `--secret` and the
`FFLOGS_CLIENT_ID` / `FFLOGS_CLIENT_SECRET` env vars still override it.
(`--creds FrenMits.json` reads the client id, but the plugin stores the secret
DPAPI-encrypted, so the secret itself has to come from one of the above.)
Make the client at <https://www.fflogs.com/api/clients>, named "FrenMits", no
redirect URL needed.

Every profile source works: a single exported fight, the plugin's `plans.json`
(fight plans moved out of the config in 1.0.0.346), or an old `FrenMits.json`.
When the file holds several fights, pick one with `--territory` or
`--name-match`.

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
       [--tolerance 2.0] [--write-corrected corrected_profile.json] \
       [--write-unreliable unanchorable.json]
   ```

   Read the UNANCHORABLE CASTS section, and keep the file it writes for step 6.
   Some mechanics are cast under one of several ability ids, drawn per pull - M9S
   picks one of two Half Moons, M11S one of four Assault Evolveds. The sheet
   recorded whichever one its own import drew, and in a pull that draws a
   different one that id fires somewhere else entirely. The plugin matches a
   mechanic anchor within 8s in either direction, so it doesn't shrug that off: it
   re-bases the clock onto the wrong moment and every call after it is early or
   late. Those casts must never be anchors. Their ROWS are fine - the mechanic
   happens on schedule, only the id it is cast under moves.

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
       [--unreliable unanchorable.json] [--out ../src/Data/P8sData.cs]
   ```

   Pass step 4's `--unreliable` file whenever it found any; without it the
   generator has no way to know a cast id is drawn at random and will happily
   anchor one.

7. **Wire it in and eyeball.** Apply the printed registration edits, build, and
   review the baked plan in-game. The mit assignments are the Auto-planner's, so a
   human should confirm them (step 5's cross-check is your reference).

## Notes

- Steps 4 and 5 are the "make it consensus" pair: 4 pins the *timings* across
  kills, 5 sanity-checks the *mit choices* against real practice.
- A row and its anchor are matched by NAME, not by whichever cast is nearest.
  Both come out of the same import, so a row is its cast's name; letting
  proximity decide handed M9S's Half Moon row the anchor of the Coffinfiller
  beside it, and gave every row in a one-second window the same cast id.
- The whole pipeline runs from one exported profile; nothing is guessed and no
  step writes back into the plan except `verify --write-corrected`, which only
  nudges existing rows toward the log median (it never fabricates a planned row).

## What checks a fight has to pass

Three layers, and none of them are optional or manual. A fight that skips one is
a fight that ships broken - each of these exists because M11S actually hit it.

**1. The generator checks itself.** `gen_official_fight.py` re-reads the file it
just wrote and proves it still says what the sheet said. It **exits 1** and writes
nothing further if any of this fails:

  * every row the sheet had is still a row (the generator used to delete rows it
    read as one cast baked twice, and it was wrong about which ones - M9S lost
    twelve, M11S twenty-seven, each taking its column of mits with it)
  * every planned press survived, in its own column (a mit is pressed BEFORE its
    mechanic, so the attach window runs 15s early / 2.5s late - a symmetric one
    silently dropped 15 presses)
  * every graded row kept at least the grade it had
  * no cast is anchored twice (that breaks replay auto-start)
  * rows in time order, no blank names, every column present

It prints `self-check: N/N presses baked, N graded rows`. If those numbers don't
match the sheet, stop.

**2. Multi-log verification** (`verify_fight_logs.py`) - your one import against
several real kills. Pass `--encounter <id>` or use ranking links with `#fight=N`:
a raid report holds a kill of EVERY boss that night, and without it the tool
happily analyses the wrong one.

Read the drift report, don't apply it blindly. A mechanic whose variants are
randomised per pull (M11S's Assault Evolved) reports as drift every time, and the
row is not what's wrong - `--write-corrected` would move it onto another
variant's median and drag its mits along. Those show up under UNANCHORABLE CASTS
instead; hand that list to the generator and the rows stay exactly where they are.

**3. The test suite covers a new fight automatically.** Add it to
`Builtin.Fights` and it inherits a dozen invariants from `BuiltinSheetTests`
(columns, aliases, anchors, phase-jump targets, downtime sanity) plus the wiring
checks in `NewFightWiringTests`:

  * a Data class with grades that nothing serves -> fails, naming the missing
    `Builtin.CustomRows` case
  * a Data class with phases that nothing serves -> fails, naming the missing
    `SheetTimeline.PhaseMarks` case
  * grades surviving BOTH load paths (`ApplySlot` and `ResetSlot`)
  * serving grades never wiping a user's own custom rows

So the only way to ship a half-registered fight is to not run the tests.

## Registering it

The generator prints the exact edits to `src/Data/<Class>_registration.txt`
(gitignored - it's a scratch note, not source). Eight places in `Builtin.cs`:
territory const, `Fights[]`, `Has`, `Name`, `BuildLines`, `SyncPoints`,
`BossAnchors`, `CustomRows`. Then `SheetTimeline.PhaseMarks` if it has phases,
and `Downtimes.For` if it has untargetable windows.

Miss one of the last three and the tests tell you which.
