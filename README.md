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

- Big countdown call for your next mit: ability icons, crisp fonts and colors, place it anywhere.
- **Next Mits board**: upcoming mechanics as draining countdown bars with your presses underneath. Gold is your next mit, green means press it now.
- **A timeline in every duty**: nearly every instanced duty in the game gets a live boss timeline, no sheet needed. Auto-loads on zone-in, starts on the pull, resyncs on boss casts.
- Optional voice cues: free online neural voices or any Windows voice.
- **Party Mit Recap** after each pull: what went out, who it covered, what never did.

## Sheet View

The whole raid plan as one spreadsheet, in game - and you can build one for any
fight. Rows are mechanics, columns are slots, and your column is what the
overlay calls.

- Edit like a spreadsheet: Enter moves down, Tab moves right, Ctrl+Z undoes anything.
- Cooldown checking: a cell turns red when a mit is planned before it can be back.
- Build a sheet for any duty: start blank, capture your own pulls, or import an FFLogs report.
- **Auto-plan**: one click plans every column's cooldowns like the reference sheets, graded by real damage.
- Plan snapshots with one-click restore, share codes for friends, export to Google Sheets, Excel, or Discord.

## Built-in fights

Ultimates: **UMAD, FRU, UCOB, UWU, TEA, DSR, TOP**. Savage: **M12S**.
Every other duty still gets a timeline; add a custom sheet when you want calls.

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
