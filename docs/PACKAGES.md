# Packages

`src/` is grouped by feature, not by technical kind. Namespace mirrors folder
exactly: `src/Meter/MeterEngine.cs` is `namespace FrenMits.Meter;`.

`FrenMits.Encounters` is its own project and its own assembly. It references no
Dalamud, Lumina, FFXIVClientStructs or ImGui assembly, and the compiler is what
enforces that: adding such a reference fails the build. Everything the client
has to supply arrives through a provider the host wires in
`Plugin.WireEncounters`, which must stay the first call in the constructor.

| Package | May reference |
| --- | --- |
| `Encounters` (own assembly) | nothing but the BCL and Newtonsoft |
| `Game` | `Encounters` |
| `Ui` | `Encounters`, `Game` |
| `Timing` | `Encounters`, `Game` |
| `Planning`, `Cues`, `Recap`, `Prep`, `Meter` | `Encounters`, `Timing`, `Game`, `Ui` |
| `Host` | everything |
| root `FrenMits` | `Encounters`, `Game` |

No feature package may reference another feature package. If `Recap` needs
something from `Planning`, push the shared type down into `Encounters` rather
than widening this table. Only the `Encounters` edge is compiler-enforced; the
rest is convention.

Root holds `Plugin.cs`, `Configuration.cs`, `Swallowed.cs` and `LoadClock.cs`.

## Persisted types

Dalamud serialises plugin configs with `TypeNameHandling.Objects`, so every
persisted type's full name is written into the file as a `$type` string, and a
single stale entry throws for the **whole document** and resets every setting.
Moving a type that a config can reach therefore needs an entry in
`Plugin.TypeMoves`, which rewrites the file before Dalamud reads it.

Types a config can reach: `Configuration`, `FightProfile`, `MitLine`,
`SyncPoint`, `BossAnchor`, `CustomRow`, `SheetNote`, `DeletedCall`,
`DowntimeWindow`, `JobRole`, `LearnedFight`, `LearnedCast`. Note `JobRole` is an
enum but still counts: its name is baked into the `$type` of the dictionary that
is keyed by it.

`PlanStore`, `PlanCodes` and `SnapshotStore` use bare `JsonConvert` with no
`TypeNameHandling`, so anything reachable only through those files is free to
move.
