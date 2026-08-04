# Testing Fren Mits

## 1. Build it

You need the **.NET 10 SDK** (your Dalamud runs on net10) and a Dalamud install at
the default path. From the project folder:

```powershell
dotnet build src -c Debug
```

Output: `src\bin\x64\Debug\FrenMits.dll` with `FrenMits.json` next to it. If the
build can't find Dalamud refs, pass the path explicitly:

```powershell
dotnet build src -c Debug -p:DalamudLibPath="$env:AppData\XIVLauncher\addon\Hooks\dev\"
```

## 2. Load it as a dev plugin

1. In game: `/xlsettings` → **Experimental** → **Dev Plugin Locations** → **+** →
   add the folder `...\FrenMits\src\bin\x64\Debug` (the folder, not the dll). Save.
2. `/xlplugins` → **Dev Tools** (or scroll the list) → enable **Fren Mits**.
3. `/fm` opens the config.

When you rebuild, Dalamud hot-reloads dev plugins automatically. If it doesn't, toggle
the plugin off/on in `/xlplugins`, or use the reload button on the dev-plugins list.

## 3. Test without a real pull

- **Overlay look/placement:** `/fm test` (or the **Test** checkbox). A sample call
  appears so you can size/color/drag it. Unlock on the Display tab to move it.
- **Audio:** Audio tab → pick a **Voice** (any installed Windows voice) → **Test voice**.
- **Built-in mits:** Fights tab → pick the ultimate → **Your slot** → **Load mits**.
  Open the line table to confirm the timeline + icons populated.
- **Timeline + resync without going in:** use the in-game **Duty Recorder**
  (`/duty` recordings) or a **replay** of the fight. Play it back — the plugin runs
  exactly as in a live pull: combat starts the timer, boss casts resync the clock,
  cues fire. Watch the **Timer** tab's "Last sync" line to confirm anchors are firing.
- **Capture anchors:** Timer tab → **Record boss casts this pull** during a replay to
  log casts/boss appearances, then **+phase / +mech / +boss anchor** to build P4/P5
  (or any) anchors.

## 4. Automated tests

The logic that doesn't need a game — sheet bakes, slot naming, share codes, the
cooldown solver, the board's row builder and the config migration chain — is
covered offline:

```powershell
dotnet test tests
```

Runs in about a second, with no game, no Dalamud services and no ImGui.

The suite lives in `tests/` on this machine only - it's gitignored, so it never
reaches GitHub and CI can't run it. Run it before you commit; nothing else will.

Worth knowing:

- **The migration tests are the point.** They replay the whole v1..v23 chain
  against configs shaped the way each era's really were, built from `DmuLegacy`
  (the frozen previous bake the repo keeps for exactly this). A bad migration is
  unrecoverable once a user has run it, so it's the one path worth proving
  offline.
- **The built-in sheet tests walk every shipped fight and every column**, so a
  malformed row in a new savage fails here instead of mid-pull. Adding a fight
  picks that coverage up for free.
- **The anchor replay runs real kills through the real resync** - see below.
- Anything that reads the game's Excel sheets (icons, cooldown recasts, boss name
  ids) can't be checked here and still needs a replay or a real pull.

## 4b. The anchor replay

Anchors are the highest-churn surface in the plugin and the one whose bugs cost
the most: a re-base landing in the wrong place puts the board a whole phase away
and leaves it there. Both times that shipped, it was only found by pointing the
board at several kills and reading how wrong it was.

`AnchorReplayTests` does that offline. Three real kills per built-in fight are
cached on disk; their enemy casts run through the same `SyncCore` the game calls,
and every anchored mechanic is asked one question: when this actually happened,
what did the board say the time was? The whole corpus replays in about a second.

**Fetch the kills once** (they're gitignored, like `tests/` itself, so a fresh
clone starts empty and the tests say so rather than passing on nothing):

```bash
python3 tools/fetch_log_fixtures.py --kills 3
```

Needs FFLogs creds in `~/.config/frenmits/fflogs.json`, same as the rest of
`tools/`. It writes `tests/fixtures/logs/<territory>/<report>-<fight>.json`,
skips anything already there, and takes a few minutes for all 22 fights.

**What it measures.** For each kill, which log cast IS the mechanic behind each
anchor is decided up front from the two fixed lists, never from what the clock
did - ground truth an engine worked out for itself would agree with the engine by
construction. Then:

- **median** board error at a mechanic (most fights: under a second),
- **run** - the longest stretch of consecutive mechanics the board got wrong,
  which is the real signal. One mechanic out is drift or a sheet row a few
  seconds off; a run of them is the clock off the fight with nothing pulling it
  back, and that is the shape both shipped bugs had.

`PuttingTheHistoricalBugBackBreaksTheReplay` stages that exact shape on a fight
that reads clean today and fails if the replay *doesn't* catch it, so the limits
are demonstrated rather than asserted.

**Reading the results.** `WriteTheReplayReport` writes every fight's numbers to
`%TEMP%\frenmits_anchor_replay.txt`. Run it before and after an anchor change.

**The recorded baselines.** Two tables at the top of the test file name what the
corpus says is wrong today - fights whose board drifts off for a stretch (Enuo,
M6S, DSR P6) and anchors sitting on an ability id the game never telegraphs. They
are ceilings, not permission: the numbers can't grow, and a pair of ratchet tests
fail when an entry has been fixed but not deleted.

## 4b. The load and dispose guards

A plugin update runs `Dispose` and the next constructor on the game's thread, so
anything slow on either path is a freeze the player feels. Both are timed: every
load and unload logs a line like

```
[FrenMits] init - live instance #1 - load 99ms (config 63, migrations 8, seeding 10, windows 18, commands 0)
[FrenMits] dispose - live instances now 0 - dispose 38ms (save 0, unhook 0, engines 2, windows 0, fonts 1, audio 35)
```

Those come from `LoadClock`; find them in `dalamud.log` when a stutter is
reported, and mark any new phase you add so the parts still add up.

The protections themselves are pinned by `LoadGuardTests`, which reads the source
and fails with the reason if one goes missing: the `SavePending` check before the
dispose-time config write, `FFLogsClient.Shutdown()`, the two off-thread warms,
the single coalesced load-frame save, the timing report on both paths, and the
short audio-worker join. Each was a measured freeze at least once. Run the suite
after any change that touches `Plugin.cs` or `Audio.cs`.

## 5. Iterate

Edit code → `dotnet build` → Dalamud reloads → re-test. The config persists in
`%AppData%\XIVLauncher\pluginConfigs\FrenMits.json`; delete it to start fresh.

## Quick smoke checklist

- [ ] `/fm` opens, status header shows job/zone/timer.
- [ ] Test mode shows the sample call with an icon; dragging + lock work.
- [ ] Test voice produces sound (try a female voice if installed).
- [ ] Load mits for your slot fills the line table (icons resolve).
- [ ] In a replay: timer starts on combat, calls fire ~3s early, "Last sync" updates,
      DTR bar shows the next mit, it all resets on wipe.

## Package boundaries

`FrenMits.Encounters` is a separate project with no Dalamud, Lumina,
FFXIVClientStructs or ImGui reference. That absence is the enforcement, and it
is worth proving it still fires: add `using Dalamud.Plugin;` to any file under
`src/FrenMits.Encounters/`, confirm `dotnet build src` fails, and revert. A
guard never seen to fail is not known to work.

The remaining packages are folders and namespaces only. Nothing enforces their
edges, so the table in `docs/PACKAGES.md` is the rule and review is the check.

Moving a persisted type needs a matching entry in `Plugin.TypeMoves`; see the
persisted-types note in `docs/PACKAGES.md` and `tests/ConfigTypeMoveTests.cs`.
