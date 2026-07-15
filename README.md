<img src="images/icon.png" width="128" align="right" alt="Fren Mits icon"/>

# Fren Mits

Turn a mitigation sheet into clean on-screen call-outs. Pick your job and Fren Mits
tells you exactly which mit to press with a center-screen countdown. Per fight,
synced to combat, and fully editable.

> It's mits with frens.

## Features

- Per-fight timelines that auto-load when you enter the zone. Only the fight you're in fires.
- Smart sync. The clock starts on the pull and resyncs on boss casts, and your edits are kept.
- Job and role aware, so each line only shows for the jobs it targets.
- Center-screen countdown with a crisp font, custom colors and text, drop shadow, and free placement.
- Optional voice cues using free online neural voices (Aria, Guy, Jenny) or any Windows voice.
- Ability icons on every call, plus optional potion windows, tank-buster plans, and job-specific extras.
- Party Mit Recap after every pull: what landed, who it covered (7/8 with names), and what never went out.
- Timer offsets per fight and per call, so "just this one earlier" is one click.

## Sheet View

The whole raid plan as one spreadsheet, in game. Rows are the fight's mechanics,
columns are all the slots, and your column is the live plan your overlay calls.

- Edit like a spreadsheet: click cells, Enter moves down, Tab moves right, Ctrl+Z undoes anything.
- Re-time a mechanic for every slot at once by clicking its time.
- The sheet's own phase notes appear at the bottom, and you can write per-mechanic notes of your own.
- Cooldown checking: a cell turns red when a mit is planned again before it can be back.
- Filter, search and replace across the whole plan; pin, resize and reorder columns.
- Plan history: automatic snapshots before imports and bulk changes, restorable any time.
- Share plan copies one code to your clipboard; friends import it and their fight updates in place.
- Export the grid as text that pastes straight into Google Sheets, Excel, or Discord.

## Custom sheets for any fight

No official sheet? Build your own:

- **New sheet** creates a blank grid bound to the duty you're standing in, with the column layout you pick.
- **Build from pull** turns your own pulls into the timeline: every boss cast is captured automatically,
  and one click makes it mechanic rows plus resync anchors. Wipe, build, prog further, build again.
- **Import log** builds the sheet from any FFLogs report link: pick the kill, and its casts become rows
  and anchors so you can prep a fight before ever entering. One-time free API client setup.

## Commands

| Command | Does |
| --- | --- |
| `/fm` | Open the config window |
| `/fm sheet` | Open Sheet View |
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
