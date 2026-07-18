<img src="images/icon.png" width="128" align="right" alt="Fren Mits icon"/>

# Fren Mits

Turn a mitigation sheet into clean on-screen call-outs. Pick your job and Fren Mits
tells you exactly which mit to press, per fight, synced to combat, fully editable.

> It's mits with frens.

## Installation

1. In Dalamud, open **Settings** (`/xlsettings`) > **Experimental**.
2. Under **Custom Plugin Repositories**, add:

   ```
   https://swixum.github.io/FrenMits/repo.json
   ```

3. Save, then install **Fren Mits** from the plugin installer (`/xlplugins`).

Beta: updates arrive automatically through the repo.

## Features

- Per-fight timelines that auto-load in the zone, start on the pull, resync on boss casts, and keep your edits.
- Center-screen countdown call with ability icons, crisp custom fonts and colors, and free placement.
- **Next Mits board**: every upcoming mechanic as a draining countdown bar with your presses underneath. Gold marks your next mit, green means press it now, in lockstep with the main call. Fully customizable (colors, bars, header, your-mits-only), with a live preview in settings, or switch to a compact list.
- Job and role aware; per-fight and per-call timer offsets.
- Optional voice cues: free online neural voices or any Windows voice.
- Party Mit Recap after every pull: what landed, who it covered, what never went out.
- Potion windows, tank-buster plans, and optional job extras per fight.

## Sheet View

The whole raid plan as one spreadsheet, in game. Rows are mechanics, columns are
slots, and your column is what the overlay calls.

- Edit like a spreadsheet: Enter moves down, Tab moves right, Ctrl+Z undoes anything; re-time a mechanic for every slot at once.
- Cooldown checking: a cell turns red when a mit is planned before it can be back.
- Phase notes and your own per-mechanic notes; filter, search and replace; pinned columns.
- Automatic plan snapshots with one-click restore; share codes for friends; export straight to Google Sheets, Excel, or Discord.

## Custom sheets for any fight

- **New sheet**: a blank grid for any duty (search by name, zone, or boss).
- **Build from pull**: your own pulls become the timeline, casts captured automatically.
- **Import log**: any FFLogs report becomes rows, resync anchors, and damage grades.
- **Auto-plan**: one click plans every column's cooldowns like the reference sheets. Hits are graded by real unmitigated damage and where players actually pressed; deadly hits stack deep, debuffs rotate one per hit, tanks get a buster plan with invuln swaps, and every kit rolls so nothing sits unused.

## Commands

| Command | Does |
| --- | --- |
| `/fm` | Open the config window |
| `/fm sheet` | Open Sheet View |
| `/fm mini` | Open the Mit Tuner (pocket sheet with live +/- nudges) |
| `/fm sync` | Zero the timer to now |
| `/fm reset` | Clear the timer |
| `/fm test` | Toggle a sample call for placement |
| `/fm p2` | Practice-jump the overlay to phase 2 (any phase number) |

## Built-in fights

| Category | Fights |
| --- | --- |
| Ultimate | Dancing Mad (UMAD), Futures Rewritten (FRU), Unending Coil of Bahamut (UCOB), Weapon's Refrain (UWU), Epic of Alexander (TEA), Dragonsong's Reprise (DSR), The Omega Protocol (TOP) |
| Savage | M12S (Lindwurm) |

Every other duty works through custom sheets, hand-added fights, or clipboard imports.
