<img src="images/icon.png" width="128" align="right" alt="Fren Mits icon"/>

# Fren Mits

Turns a mitigation sheet into on-screen call-outs. Pick your job and it tells you
which mit to press, synced to the pull.

> It's mits with frens.

## Install

Fren Mits is still in **beta**. To install:

1. Dalamud **Settings** (`/xlsettings`) > **Experimental** > **Custom Plugin Repositories**, add:

   ```
   https://swixum.github.io/FrenMits/repo.json
   ```

2. Install **Fren Mits** from `/xlplugins`.

Updates arrive on their own. Things move fast in beta; if something breaks, a fix
is usually already on the way.

## What it does

- Big countdown call for your next mit, with ability icons. Put it anywhere.
- **Next Mits board**: upcoming mechanics as draining bars with your presses underneath. Gold is next, green means press it now.
- **Mit timeline**: your calls as a compact list, ticking down through the whole pull.
- **A timeline in every duty**: nearly every instanced duty gets a live boss timeline, no sheet needed. Loads on zone-in, starts on the pull, resyncs on boss casts.
- Optional voice cues: free online neural voices or any Windows voice.
- **Party Mit Recap** after each pull: what went out, who it covered, what never did.
- **Fren Meter**: damage and healing, with **rDPS** off the combat log, per-pull history and ability breakdowns. Reads ACT or IINACT.

## Sheet View

The whole raid plan as one spreadsheet, in game, for any fight. Rows are
mechanics, columns are slots, and your column is what the overlay calls.

- Edit like a spreadsheet: Enter moves down, Tab moves right, Ctrl+Z undoes anything.
- A cell turns red when a mit is planned before it can be back.
- Start blank, capture your own pulls, or import an FFLogs report.
- **Auto-plan**: one click fills every column, graded by real damage.
- Snapshots with one-click restore, share codes for friends, export to Google Sheets, Excel or Discord.

## Built-in fights

All three Dawntrail savage tiers ship planned, with both Dawntrail ultimates,
several extremes and the legacy ultimates: every column filled, mechanics graded
by real damage, resync anchors so the clock follows your pull. The in-game
Fights list is the full roster. Load one and raid.

Every other duty still gets a timeline; add a custom sheet when you want calls.

<details>
<summary><b>Commands</b></summary>

| Command | Does |
| --- | --- |
| `/fm` | Open the config window |
| `/fm sheet` | Open Sheet View |
| `/fm mini` | Open the Mit Tuner (pocket sheet with live +/- nudges) |

</details>

---

<sub>**Disclaimer:** use at your own risk. Fren Mits is unofficial, not affiliated with or endorsed by Square Enix.</sub>
