<img src="images/icon.png" width="128" align="right" alt="Fren Mits icon"/>

# Fren Mits

Turns a mitigation sheet into on-screen call-outs. Pick your job and it tells you
which mit to press, per fight, synced to combat, fully editable.

> It's mits with frens.

## Install

1. Dalamud **Settings** (`/xlsettings`) > **Experimental** > **Custom Plugin Repositories**, add:

   ```
   https://swixum.github.io/FrenMits/repo.json
   ```

2. Install **Fren Mits** from `/xlplugins`. Updates arrive automatically.

## What it does

- Fight timelines auto-load in the zone, start on the pull, resync on boss casts, and keep your edits.
- Big countdown call with ability icons, custom fonts and colors, and optional voice cues.
- **Next Mits board**: upcoming mechanics as draining countdown bars with your presses underneath. Gold is your next mit, green means press now. Customizable, live preview in settings, compact list option.
- **Sheet View**: the whole raid plan as an in-game spreadsheet. Edit with undo, bulk re-times, notes, cooldown warnings, plan history, share codes, and export to Sheets or Discord.
- **Custom sheets** for any duty: build from your own pulls or an FFLogs link. **Auto-plan** fills every column's cooldowns from real damage: deep stacks on deadly hits, one debuff per hit, tank buster plans, nothing left unused.
- **Party Mit Recap** after each pull: what went out, who it covered, what never did.
- Potion windows, tank plans, job extras, per-call timing tweaks.

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
