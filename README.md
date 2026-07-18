<img src="images/icon.png" width="128" align="right" alt="Fren Mits icon"/>

# Fren Mits

Turns a mitigation sheet into on-screen call-outs. Pick your job and it tells you
which mit to press, per fight, synced to combat, fully editable.

> It's mits with frens.

## Install

Fren Mits is still in **beta**, but you can use it right now:

1. Dalamud **Settings** (`/xlsettings`) > **Experimental** > **Custom Plugin Repositories**, add:

   ```
   https://swixum.github.io/FrenMits/repo.json
   ```

2. Install **Fren Mits** from `/xlplugins`.

Updates arrive automatically through the repo. Being a beta, things move fast;
if something breaks, an update is usually already on the way.

## What it does

- Fight timelines auto-load when you enter the zone, start on the pull, resync on boss casts, and keep your edits.
- Big countdown call with ability icons, crisp custom fonts and colors, drop shadow, and free placement.
- **Next Mits board**: every upcoming mechanic as a draining countdown bar with your presses underneath. Gold is your next mit, green means press it now, in lockstep with the main call. Fully customizable with a live preview in settings, or switch to a compact list.
- Optional voice cues: free online neural voices (Aria, Guy, Jenny) or any Windows voice.
- **Party Mit Recap** after each pull: what went out, who it covered, what never did.
- Potion windows, tank-buster plans, job extras, and timing tweaks per fight and per call.

## Sheet View

The whole raid plan as one spreadsheet, in game. Rows are mechanics, columns are
slots, and your column is what the overlay calls.

- Edit like a spreadsheet: Enter moves down, Tab moves right, Ctrl+Z undoes anything; re-time a mechanic for every slot at once.
- Cooldown checking: a cell turns red when a mit is planned before it can be back.
- Phase notes, your own per-mechanic notes, filter, search and replace.
- Automatic plan snapshots with one-click restore, share codes for friends, and export straight to Google Sheets, Excel, or Discord.

## Custom sheets for any fight

- **New sheet**: a blank grid for any duty (search by name, zone, or boss).
- **Build from pull**: your own pulls become the timeline, boss casts captured automatically.
- **Import log**: any FFLogs report becomes rows, resync anchors, and damage grades.
- **Auto-plan**: one click plans every column's cooldowns like the reference sheets. Hits are graded by real unmitigated damage and where players actually pressed; deadly hits stack deep, debuffs rotate one per hit, tanks get a buster plan with invuln swaps, and every kit keeps rolling so nothing sits unused.

## Built-in fights

Ultimates: **UMAD, FRU, UCOB, UWU, TEA, DSR, TOP**. Savage: **M12S**.
Everything else works through custom sheets or shared plan codes.

<details>
<summary><b>Commands</b></summary>

| Command | Does |
| --- | --- |
| `/fm` | Open the config window |
| `/fm sheet` | Open Sheet View |
| `/fm mini` | Open the Mit Tuner (pocket sheet with live +/- nudges) |
| `/fm sync` | Zero the timer to now |
| `/fm reset` | Clear the timer |
| `/fm test` | Toggle a sample call for placement |
| `/fm p2` | Practice-jump the overlay to phase 2 (any phase number) |

</details>
