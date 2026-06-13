# Fren Mits

A Dalamud plugin that turns a mitigation sheet into on-screen call-outs. Import a
mit sheet per encounter, pick your job/role, and get a center-screen warning (with
a 3-second lead by default) telling you exactly which mitigation to press. It only
fires in the fight's territory and syncs to combat start. Every line is editable.

## Install

Add the custom plugin repository to Dalamud, then install **Fren Mits** from the
plugin installer:

```
https://swixum.github.io/FrenMits/repo.json
```

(Dalamud → Settings → Experimental → Custom Plugin Repositories → paste → save.)

## Features

- **Per-fight timelines** gated to a specific territory (zone). Only the fight you
  are currently in fires.
- **Seamless auto-load** — walk into a supported boss room and the latest baked
  timeline loads for your slot automatically. Each slot keeps its own edits;
  switching slots loads that slot fresh, and imports add onto the current slot.
  Re-entering only tops up new lines — your edits are always kept, no prompts.
- **Job / role select** at the top (Auto-follows your current job, or override).
  Each line can target specific jobs/roles, so you only see your mits.
- **Combat-synced timer** — auto-starts on the pull. `/fm sync` zeroes it to a known
  mechanic; per-fight offset nudges the whole sheet.
- **Center-screen countdown** with a configurable warning lead, crisp scalable font,
  custom text template, colors, optional background, drop shadow, a countdown bar,
  a last-second pulse, and free placement.
- **Audio cues (optional)** — text-to-speech using any installed Windows voice
  (male or female), fired once per pull when a call comes up, even if the overlay
  is hidden.
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

## Supported fights

Baked timelines (with resync anchors where available) ship for these encounters
and auto-load when you enter the zone. Any other fight can be added and imported
by hand from a sheet.

| Category | Fight |
| --- | --- |
| Ultimate | Dancing Mad, Futures Rewritten |
| Savage | M12S (Lindwurm) |
