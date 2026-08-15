# Notice

FrenMits includes work derived from other projects. This file records what, from
where, and under which licence. It ships with the plugin.

## cactbot

<https://github.com/OverlayPlugin/cactbot> — Apache License 2.0, Copyright the
cactbot authors.

Three parts of FrenMits are derived from that project's data:

**Boss alerts.** The call pack in `src/Data/Callouts/triggers.fmtrig` is built
from cactbot's raidboss trigger definitions. What each call says is copied word
for word on purpose, so a group already used to those calls hears the same
words. The timing, the audience and the conditions come from the same place. The
spoken variant of each call is derived by FrenMits, and the geometry, arena
measurements and direction calls are its own.

**Downtime windows.** The boss untargetable and targetable windows in
`src/FrenMits.Encounters/Downtimes.cs`, anchored to FrenMits' own clock. See the
note at the top of that file.

**Fight timelines.** The timing data behind the timeline board, adapted and
rendered by FrenMits. See the note in `src/Cues/TimelineWindow.cs`.

A copy of the Apache License 2.0 is at
<https://www.apache.org/licenses/LICENSE-2.0>.

This is attribution, not endorsement. Nothing here implies that project's
authors are involved with FrenMits or have reviewed it.

## FFXIV data

Ability names, ability ids, territory ids and job data come from the game's own
sheets. FINAL FANTASY XIV © SQUARE ENIX CO., LTD.
