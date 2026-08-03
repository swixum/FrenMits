# Fight sheet format

Baked sheets live in `src/Data/Sheets/*.json`, one per fight, and are copied
next to the DLL at build time. `src/Data/Sheets/DancingMad(UMAD).json` is the
reference sheet: it is the only one currently held to the full invariant, and
new or cleaned-up sheets should match its shape.

`scripts/validate-sheets.ps1` enforces this as a PreBuild step. A sheet is
added to that script's `$Strict` list once it passes.

## Sections

| Key | Purpose |
| --- | --- |
| `TerritoryId` | Duty id; must match the constant in `Builtin.cs`. |
| `Name` | Display name; must match `Builtin.Fights`, since the filename is derived from it. |
| `Timeline` | **The authoritative list of boss casts.** Every `DefaultAction` points into it. |
| `DefaultActions` | The baked mit calls, per column and per job. |
| `SyncPoints` | Ability ids that re-sync the combat clock. |
| `PhaseStarts` | Phase boundaries, used for dividers and titles. |
| `CustomRows` | Log-derived severity grading (`Hurt` 1-3, `Buster`) for *every* damaging cast. A superset of `Timeline`. |
| `BossAnchors` | Fallback phase labels when `PhaseStarts` is empty. |
| `PriorityPhases` | Windows where MT/OT tank lines mean priority 1/2 rather than literal enmity. |

`Timeline` is reference data — no code reads it at runtime. The mechanic list
the UI actually builds comes from `DefaultActions` via `Builtin.BakedLines`.
That is exactly why it needs a build-time check: a bad mechanic name here
produces a wrong sheet, never an error.

## The invariant

> Every `DefaultAction` must have a `(Mechanic, Time)` pair that **exactly**
> matches a `Timeline` entry, unless it is marked `"Hidden": true`.

Exact, not approximate. An action a second or two off its mechanic renders as
its own row in the config list, which is how one cast ends up looking like
three. Use `MitLine.OffsetSeconds` if a call needs to *fire* early or late —
that shifts the cue without splitting the row.

```json
{
  "Time": 63,
  "Mechanic": "Light of Judgment",
  "Slot": "MT",
  "Action": "Reprisal"
}
```

Key order is `Time`, `Mechanic`, `Slot`, `Action`, `Jobs`, `Hidden`.

- **`Slot`** is omitted for a job-only line (a job extra). `Builtin.Bake` reads
  a blank slot as "applies to whoever plays that job", so it is not filtered by
  column; `AppliesTo(jobAbbr)` gates it at render time.
- **`Jobs`** restricts the line to those job abbreviations. Omit for a line
  every job in the column takes.

## `Hidden`

`"Hidden": true` marks an action whose `Mechanic` is a **personal timer rather
than a boss cast** — currently only the summoner's `Summon` cycle (Ifrit /
Titan / Garuda), 51 rows in UMAD.

A hidden action still bakes, and the job that owns it still sees the row as a
normal part of the sheet. For everyone else it is absent entirely — not an
official mechanic they are missing actions for, and not a custom row either.
It also carries no delete affordance in Sheet View: the sheet keeps baking it,
so a tombstone would only fight the next top-up.

The lookup is by mechanic *name*, through `Builtin.IsHiddenMechanic`, not by a
flag on `MitLine`. A plan saved before the flag existed carries no `Hidden` of
its own, so the sheet has to be the authority or old saves resolve differently
from new ones.

It is an escape hatch for non-mechanics only. The validator rejects `Hidden` on
any name that *is* in the `Timeline`, since that would quietly drop a real cast
off the mechanic list.

## Adding a mechanic

If an action needs a cast the `Timeline` lacks, add the `Timeline` entry rather
than nudging the action's time. Prefer the time recorded in `CustomRows` — that
section is built from kill logs, so it is the most accurate record of when the
cast lands.

Both are ordered by `Time`; keep them that way.
