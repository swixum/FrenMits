# Fren Mits

A Dalamud plugin that turns a mitigation sheet into on-screen call-outs. Import a
mit sheet per encounter, pick your job/role, and get a center-screen warning (with
a 3-second lead by default) telling you exactly which mitigation to press. It only
fires in the fight's territory and syncs to combat start. Every line is editable.

## Features

- **Per-fight timelines** gated to a specific territory (zone). Only the fight you
  are currently in fires.
- **Job / role select** at the top (Auto-follows your current job, or override).
  Each line can target specific jobs/roles, so you only see your mits.
- **Combat-synced timer** — auto-starts on the pull. `/fm sync` zeroes it to a known
  mechanic; per-fight offset nudges the whole sheet.
- **Center-screen countdown** with a configurable warning lead, crisp scalable font,
  custom text template, colors, optional background, drop shadow, a countdown bar,
  a last-second pulse, and free placement.
- **Audio cues (optional)** — text-to-speech (built-in Windows voice, no extra
  installs) and/or a tunable beep, fired once per pull when a call comes up, even if
  the overlay is hidden.
- **Sheet importer** — paste rows straight from Google Sheets / Excel, map the time /
  mechanic / action columns, and assign the lines to a job, role, or everyone.
- **Ability icons** — each call shows the real action icon, matched automatically from
  the action name (or pin a specific icon per line with a searchable picker).
- **Per-line overrides** — each line can have its own warning lead, spoken text,
  icon, text colour, reorder, or be muted (the "…" button on the line).
- **Server-info (DTR) bar** entry showing the next mit and its countdown.
- **Share fights** — export a whole fight (lines included) to the clipboard and import
  it on another character or send it to a friend.
- **Polished UI** — status header (fight / job / timer / audio at a glance), icon
  toolbar buttons, and help tooltips.
- **Fully editable** — add/remove/edit/reorder every line, per fight.

## Commands

- `/frenmits` or `/fm` — open the config window.
- `/fm sync` — zero the timer to right now (align to a known mechanic).
- `/fm reset` — clear the timer.
- `/fm test` — toggle test mode (shows a sample call so you can place/size the overlay).

## Building

Requires the **.NET 10 SDK** (your Dalamud runs on net10) and a Dalamud install at
the default path. From this folder:

```
dotnet build -c Release
```

The reference assemblies are resolved from
`%appdata%\XIVLauncher\addon\Hooks\dev\`. If yours is elsewhere, pass
`-p:DalamudLibPath=C:\path\to\dev\` (trailing slash).

Output: `bin\x64\Release\FrenMits.dll` (with `FrenMits.json` beside it).

## Installing (dev plugin)

1. In game: `/xlsettings` → **Experimental** → **Dev Plugin Locations** → add the
   folder containing `FrenMits.dll` (or the dll path).
2. `/xlplugins` → **Dev Tools** / installed list → enable **Fren Mits**.
3. `/fm` to configure.

## Built-in ultimates

Profiles for **Dancing Mad (Ultimate)** (territory `1363`) and **Futures Rewritten
(Ultimate)** (territory `1238`) are created automatically on first launch. Each is
**per fight** — only runs in its own zone.

**Built-in mits.** Both mit sheets are **baked into the plugin** (DMU = Ikuya
Kirishima; FRU matched to cactbot's timeline). In the Fights tab, pick **Your slot**
(your tank/DPS slot, or your healer job) and click **Load mits** — no copy-pasting.
You can still import or hand-edit anything. FRU's times are taken from cactbot's
`futures_rewritten` timeline so the per-role mits land on accurate, continuous times
even though the sheet itself mixes per-phase and continuous timing.

**Boss-presence anchors.** Besides cast resync, a phase can re-anchor when its boss
*appears* (by NameId) — a cast-free safety net for phases with no public ability
timeline. DMU ships with the Chaos→P3 anchor; capture more (P4/P5, or FRU bosses)
from a pull on the Timer tab.

**cactbot-style resync.** The timeline is one continuous clock from the pull, but a
fixed clock drifts as kill speed changes phase lengths. To stay accurate, the plugin
watches boss **cast bars**: when a known ability (matched to cactbot's `dancing_mad`
timeline ability ids) starts casting, the clock snaps so that ability **resolves on
its scripted time** — exactly how cactbot keeps its timeline honest, but without
hooking the game. Toggle + window are on the **Timer** tab; the baked DMU data ships
with the resync anchors already attached.

It auto-starts at combat and **resets on a wipe or when the duty ends**, re-armed for
the next pull without leaving the instance. If the preset is gone, the Fights tab
shows a **"+ Dancing Mad (Ultimate) preset"** button.

## Quick start

1. Open `/fm`, go to **Fights**. The **Dancing Mad (Ultimate)** profile is already
   there (or click the preset button / **Add fight** + **Use current zone**).
2. Open **Import from a sheet**, paste your mit sheet rows, click **Parse**, map the
   Time / Mechanic / Action columns, choose which job(s) the lines apply to, and
   **Append**.
3. Set **Your job** at the top (or leave Auto).
4. **Display** tab: size/color/place the overlay (turn on **Test mode** to see it).
5. Pull. The call appears 3s before each mit. Use `/fm sync` on a known mechanic if
   your sheet's t=0 differs from combat start, or set a per-fight **Timer offset**.

## Notes

- Built against Dalamud API level 15. If the installer reports an API mismatch,
  change `DalamudApiLevel` in `FrenMits.json` to match your Dalamud.
- The timer syncs to the in-combat flag, which lands a beat after the literal pull;
  the per-fight offset and `/fm sync` exist to align it exactly.
