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
- **Mit timeline**: your own calls as a compact running list, ticking down in order through the whole pull, pinned wherever you want it.
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

These ship planned: every column filled, mechanics graded by real damage, and
resync anchors so the clock follows your pull. Load one and raid.

| Expansion | Tier | Fight |
| --- | --- | --- |
| **Dawntrail** | Ultimate | Futures Rewritten (FRU) |
| | Ultimate | Dancing Mad (UMAD) |
| | Savage | M1S - Black Cat |
| | Savage | M2S - Honey B. Lovely |
| | Savage | M3S - Brute Bomber |
| | Savage | M4S - Wicked Thunder |
| | Savage | M5S - Dancing Green |
| | Savage | M6S - Sugar Riot |
| | Savage | M7S - Brute Abombinator |
| | Savage | M8S - Howling Blade |
| | Savage | M9S - Vamp Fatale |
| | Savage | M10S - Red Hot / Deep Blue |
| | Savage | M11S - The Tyrant |
| | Savage | M12S - Lindwurm |
| | Extreme | Doomtrain |
| | Extreme | Enuo |
| | Extreme | Zelenia |
| **Endwalker** | Ultimate | Dragonsong's Reprise (DSR) |
| | Ultimate | The Omega Protocol (TOP) |
| **Shadowbringers** | Ultimate | Epic of Alexander (TEA) |
| **Stormblood** | Ultimate | Unending Coil of Bahamut (UCOB) |
| | Ultimate | Weapon's Refrain (UWU) |

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
