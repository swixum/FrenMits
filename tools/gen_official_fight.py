#!/usr/bin/env python3
"""
FrenMits official-fight generator (dev-only; NOT shipped in the plugin DLL).

Turns a finished in-game sheet into a built-in fight WITH its Auto-planned mit
sheet baked into every column. The intended flow (Option 2 of the official-fight
pipeline) is:

    import an FFLogs kill  ->  Auto-plan  ->  tweak in-game  ->  run this

It reads a saved FightProfile (from FrenMits.json, or a single-profile JSON) and
emits:

  * src/Data/<Class>Data.cs   - Slots, Timeline (every column's mit per mechanic,
                                straight from the Auto-plan), BuildLines,
                                SyncPoints, BossAnchors, PhaseStarts.
  * a printed registration block - the exact edits to make in Builtin.cs and the
                                Downtimes.cs window entry, so nothing is guessed.

The Data file is self-contained C# and compiles on its own; the Builtin.cs /
Downtimes.cs edits wire it in. SHOW the result for review before committing - the
mit assignments are the Auto-planner's, and a human should eyeball them.

Usage:
  gen_official_fight.py CONFIG.json --class P8s --name "Abaddon (P8S)" \\
      --category Savage [--territory 1088 | --name-match Abaddon | --index 0] \\
      [--out ../src/Data/P8sData.cs]

CONFIG.json may be the whole FrenMits.json (has a "Fights" array - then pick one
with --territory / --name-match / --index) or a single exported FightProfile.
"""

import argparse
import json
import os
import re
import sys

# ---- slot naming (ported from src/Core/SlotNames.cs) ----------------------

STANDARD = ["T1", "T2", "WHM", "AST", "SCH", "SGE", "M1", "M2", "R1", "R2"]

_CANON = {
    "MT": "T1", "T1": "T1",
    "OT": "T2", "T2": "T2",
    "D1": "M1", "M1": "M1",
    "D2": "M2", "M2": "M2",
    "D3": "R1", "R": "R1", "R1": "R1",
    "D4": "R2", "CASTER": "R2", "R2": "R2",
    "WHM": "WHM", "AST": "AST", "SCH": "SCH", "SGE": "SGE",
    "H1": "H1", "H2": "H2",
}

_TO_LEGACY = {"T1": "MT", "T2": "OT", "M1": "D1", "M2": "D2", "R1": "D3", "R2": "D4"}


def canon(slot):
    s = (slot or "").strip()
    return _CANON.get(s.upper(), s)


def to_legacy(slot):
    c = canon(slot)
    return _TO_LEGACY.get(c, c)


# ---- helpers --------------------------------------------------------------

def cs_str(s):
    """A C# double-quoted string literal for arbitrary text (control chars escaped
    so a stray newline in a mechanic/action name can't split the literal)."""
    s = (s or "").replace("\\", "\\\\").replace('"', '\\"')
    s = s.replace("\r", "\\r").replace("\n", "\\n").replace("\t", "\\t")
    return '"' + s + '"'


def mech_eq(a, b):
    return (a or "").strip().casefold() == (b or "").strip().casefold()


def pick_profile(data, territory, name_match, index):
    """Return the chosen FightProfile dict from a config or single profile."""
    if isinstance(data, dict) and "Fights" in data and isinstance(data["Fights"], list):
        fights = data["Fights"]
    elif isinstance(data, list):
        fights = data
    else:
        return data  # already a single profile
    if not fights:
        sys.exit("No fights in that config.")
    if territory is not None:
        hits = [f for f in fights if int(f.get("TerritoryId", 0)) == territory]
        if not hits:
            sys.exit(f"No fight with TerritoryId {territory}.")
        return hits[0]
    if name_match:
        nm = name_match.casefold()
        hits = [f for f in fights if nm in str(f.get("Name", "")).casefold()]
        if not hits:
            sys.exit(f'No fight whose name contains "{name_match}".')
        if len(hits) > 1:
            names = ", ".join(f'{f.get("Name")}' for f in hits)
            sys.exit(f'"{name_match}" matched several fights: {names}. Narrow it.')
        return hits[0]
    if index is not None:
        if not (0 <= index < len(fights)):
            sys.exit(f"--index {index} out of range (0..{len(fights)-1}).")
        return fights[index]
    if len(fights) == 1:
        return fights[0]
    listing = "\n".join(
        f'  [{i}] {f.get("Name","?")}  territory {f.get("TerritoryId","?")}'
        for i, f in enumerate(fights))
    sys.exit("Multiple fights; pick one with --territory / --name-match / --index:\n" + listing)


def slot_line_map(profile, canon_slots):
    """canon slot -> its list of MitLine dicts (active slot uses the fuller of
    Lines / its stash, since Lines is the live alias)."""
    saved = profile.get("SavedSlots") or {}
    active = canon(profile.get("Slot", ""))
    live = profile.get("Lines") or []
    out = {}
    for slot in canon_slots:
        stash = None
        for k, v in saved.items():
            if canon(k) == slot:
                stash = v
                break
        if slot == active and live:
            if stash is None or len(live) >= len(stash):
                stash = live
        out[slot] = stash or []
    return out


def actions_for(row, slot_lines, canon_slots):
    """Per-column mit strings for one mechanic row (joined when a column stacks
    more than one call on the same hit)."""
    t = float(row.get("Time", 0))
    mech = row.get("Mechanic", "")
    cells = []
    for slot in canon_slots:
        parts = []
        for l in slot_lines[slot]:
            if not l.get("Enabled", True):
                continue
            if not mech_eq(l.get("Mechanic", ""), mech):
                continue
            if abs(float(l.get("Time", 0)) - t) >= 2.0:
                continue
            act = (l.get("Action", "") or "").strip()
            if act and act not in parts:
                parts.append(act)
        cells.append(" + ".join(parts))
    return cells


def sync_for(row, syncs):
    """Nearest cast-bar anchor id to this row's time (0 if none within 3s)."""
    t = float(row.get("Time", 0))
    best, best_gap = 0, 3.0
    for sp in syncs:
        gap = abs(float(sp.get("Time", 0)) - t)
        if gap < best_gap:
            best_gap = gap
            best = int(sp.get("Ability", 0)) & 0xFFFFFFFF
    return best


def phase_of(t, bounds):
    """P1 before the first transition, P2 after it, and so on."""
    return "P" + str(1 + sum(1 for b in bounds if b <= t + 0.5))


# ---- emit -----------------------------------------------------------------

DATA_TEMPLATE = '''// AUTO-GENERATED by tools/gen_official_fight.py from an in-game FrenMits sheet
// ({source}). The Actions[] arrays ARE the Auto-planned mit for each column; times
// are seconds from the pull. Review the mit assignments before shipping - they are
// the planner's, not hand-authored. Regenerate by re-running the tool.
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

public static class {cls}Data
{{
    public static readonly string[] Slots = {{ {slots} }};

    public sealed record Entry(int Time, string Phase, string Mechanic, uint Sync, string[] Actions);

    public static readonly Entry[] Timeline =
    {{
{entries}
    }};

    // Phase start times for the practice phase-jump (derived from the Timeline).
    public static List<(string Name, float Time)> PhaseStarts()
        => Timeline.GroupBy(e => e.Phase)
                   .Select(g => (g.Key, g.Min(e => (float)e.Time)))
                   .OrderBy(x => x.Item2).ToList();

    // Build mit lines for a sheet slot (native MT/OT/D1-D4 labels).
    public static List<MitLine> BuildLines(string slot)
    {{
        var idx = Array.IndexOf(Slots, slot);
        var list = new List<MitLine>();
        if (idx < 0) return list;
        var seen = new HashSet<(int Time, uint Sync)>();
        foreach (var e in Timeline)
        {{
            var action = e.Actions[idx];
            if (string.IsNullOrWhiteSpace(action)) continue;
            // One player, one call per (time, ability): a mechanic listed on
            // several note-rows must not fire its cue twice.
            if (!seen.Add((e.Time, e.Sync))) continue;
            list.Add(new MitLine {{ Time = e.Time, Mechanic = e.Mechanic, Action = action.Trim(), Enabled = true }});
        }}
        return list;
    }}

    // Resync anchors: the first synced cast of each phase re-bases the clock
    // (wide window), so a faster/slower kill still snaps into place.
    public static List<SyncPoint> SyncPoints()
    {{
        var points = new List<SyncPoint>();
        var phaseSeen = new HashSet<string>();
        var prevTime = float.NegativeInfinity;
        foreach (var e in Timeline.Where(e => e.Sync != 0).OrderBy(e => e.Time))
        {{
            var isPhaseAnchor = phaseSeen.Add(e.Phase) || (e.Time - prevTime) > 90f;
            points.Add(new SyncPoint {{ Ability = e.Sync, Time = e.Time, IsPhase = isPhaseAnchor, Label = $"{{e.Phase}} {{e.Mechanic}}" }});
            prevTime = e.Time;
        }}
        return points;
    }}

    public static List<BossAnchor> BossAnchors() => new(){bossanchors};
}}
'''


def emit_data(cls, source, legacy_slots, rows, boss_anchors):
    slots = ", ".join(cs_str(s) for s in legacy_slots)
    lines = []
    for r in rows:
        acts = ", ".join(cs_str(a) for a in r["actions"])
        sync = f"0x{r['sync']:04X}" if r["sync"] else "0"
        lines.append(
            f'        new({r["time"]}, {cs_str(r["phase"])}, {cs_str(r["mech"])}, '
            f'{sync}, new[]{{{acts}}}),')
    entries = "\n".join(lines)

    if boss_anchors:
        ba = " {\n" + "\n".join(
            f'        new() {{ NameId = 0x{int(b.get("NameId",0)):X}, Time = {float(b.get("Time",0)):g}f, '
            f'Label = {cs_str(b.get("Label",""))} }},'
            for b in boss_anchors) + "\n    }"
    else:
        ba = ""

    return DATA_TEMPLATE.format(
        cls=cls, source=source, slots=slots, entries=entries, bossanchors=ba)


def emit_registration(cls, name, category, territory, downtimes):
    # These lines are pasted into C#, so the display strings must be escaped.
    name_cs = cs_str(name)
    cat_cs = cs_str(category)
    L = []
    L.append("=" * 72)
    L.append(f"REGISTER {cls} - apply these edits, then build (expect 0 warnings).")
    L.append("=" * 72)
    L.append("")
    L.append("--- src/Data/Builtin.cs ---")
    L.append("")
    L.append(f"1) territory const:")
    L.append(f"   public const ushort {cls}Territory = {territory};")
    L.append("")
    L.append(f"2) Fights[] array, add a row:")
    L.append(f'   ({cls}Territory, {name_cs}, {cat_cs}),')
    L.append("")
    L.append(f'3) Has(): add "or {cls}Territory" to the territory test.')
    L.append("")
    L.append(f"4) Name(): add a case:")
    L.append(f'   {cls}Territory => {name_cs},')
    L.append("")
    L.append(f"5) BuildLines(): add ABOVE the `_ =>` default (or DMU claims it):")
    L.append(f"   {cls}Territory => {cls}Data.BuildLines(SlotNames.ToLegacy(slot)),")
    L.append("")
    L.append(f"6) SyncPoints(): add a case:")
    L.append(f"   {cls}Territory => {cls}Data.SyncPoints(),")
    L.append("")
    L.append(f"7) BossAnchors(): add a case:")
    L.append(f"   {cls}Territory => {cls}Data.BossAnchors(),")
    L.append("")
    if downtimes:
        L.append("--- src/Data/Downtimes.cs ---")
        L.append("")
        L.append("8) For() switch, add a case:")
        L.append(f"   Builtin.{cls}Territory => {cls},")
        L.append("")
        L.append("9) add the window list:")
        L.append(f"   private static readonly List<DowntimeWindow> {cls} = new()")
        L.append("   {")
        for w in downtimes:
            hp = float(w.get("TargetHp", -1))
            cut = "true" if w.get("Cutscene", False) else "false"
            L.append(
                f"       new() {{ Start = {float(w.get('Start',0)):g}, "
                f"Duration = {float(w.get('Duration',0)):g}, "
                f"TargetHp = {hp:g}f, Cutscene = {cut} }},")
        L.append("   };")
        L.append("")
    else:
        L.append("(no untargetable windows in this profile - nothing to add to Downtimes.cs)")
        L.append("")
    return "\n".join(L)


# ---- main -----------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(description="Generate a built-in FrenMits fight from a saved sheet.")
    ap.add_argument("config", help="FrenMits.json or a single exported FightProfile JSON")
    ap.add_argument("--class", dest="cls", required=True, help="C# class stem, e.g. P8s -> P8sData")
    ap.add_argument("--name", help="display name (defaults to the profile's Name)")
    ap.add_argument("--category", default="Savage", help='e.g. "Ultimate" or "Savage" (default Savage)')
    ap.add_argument("--territory", type=int, help="pick the fight by TerritoryId")
    ap.add_argument("--name-match", help="pick the fight whose Name contains this")
    ap.add_argument("--index", type=int, help="pick the fight by position in the config")
    ap.add_argument("--out", help="output .cs path (default ../src/Data/<Class>Data.cs)")
    args = ap.parse_args()

    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9]*", args.cls):
        sys.exit("--class must be a bare identifier (letters/digits), e.g. P8s.")

    with open(args.config, "r", encoding="utf-8-sig") as fh:
        data = json.load(fh)

    profile = pick_profile(data, args.territory, args.name_match, args.index)
    territory = int(profile.get("TerritoryId", 0))
    if territory == 0:
        sys.exit("That profile has TerritoryId 0 - it isn't bound to a duty; bind it in-game first.")
    name = args.name or str(profile.get("Name", args.cls))

    canon_slots = [canon(s) for s in (profile.get("CustomSlots") or [])]
    if not canon_slots:
        sys.exit("That profile has no CustomSlots - it isn't a custom sheet (import a log + Auto-plan first).")
    if canon_slots != STANDARD:
        print(f"! note: columns are {canon_slots}, not the standard {STANDARD}. "
              f"Proceeding, but Builtin.Slots() serves the standard 10 - reconcile if they differ.",
              file=sys.stderr)
    legacy_slots = [to_legacy(s) for s in canon_slots]

    slot_lines = slot_line_map(profile, canon_slots)
    syncs = profile.get("SyncPoints") or []
    bounds = sorted(float(sp.get("Time", 0)) for sp in syncs
                    if sp.get("IsPhase") and float(sp.get("Time", 0)) > 2.0)

    custom_rows = sorted((profile.get("CustomRows") or []), key=lambda r: float(r.get("Time", 0)))
    if not custom_rows:
        sys.exit("That profile has no CustomRows - nothing to bake (import a log first).")

    rows = []
    planned = 0
    for cr in custom_rows:
        t = float(cr.get("Time", 0))
        acts = actions_for(cr, slot_lines, canon_slots)
        if any(a for a in acts):
            planned += 1
        rows.append({
            "time": int(round(t)),
            "phase": phase_of(t, bounds),
            "mech": cr.get("Mechanic", ""),
            "sync": sync_for(cr, syncs),
            "actions": acts,
        })

    downtimes = profile.get("CustomDowntimes") or []
    boss_anchors = profile.get("BossAnchors") or []

    # single-line only: this lands inside a // comment in the generated file.
    source = f'{name}, territory {territory}'.replace("\r", " ").replace("\n", " ")
    cs = emit_data(args.cls, source, legacy_slots, rows, boss_anchors)

    out = args.out
    if not out:
        here = os.path.dirname(os.path.abspath(__file__))
        out = os.path.join(here, "..", "src", "Data", f"{args.cls}Data.cs")
    out = os.path.abspath(out)
    with open(out, "w", encoding="utf-8", newline="\r\n") as fh:
        fh.write(cs)

    reg = emit_registration(args.cls, name, args.category, territory, downtimes)
    reg_path = os.path.join(os.path.dirname(out), args.cls + "_registration.txt")
    with open(reg_path, "w", encoding="utf-8") as fh:
        fh.write(reg + "\n")

    synced = sum(1 for r in rows if r["sync"])
    print(f"Wrote {out}")
    print(f"  {len(rows)} mechanic rows ({planned} with a planned mit), "
          f"{synced} anchors, {len(downtimes)} untargetable window(s), "
          f"{len(set(r['phase'] for r in rows))} phase(s).")
    print(f"Wrote {reg_path}")
    print()
    print(reg)


if __name__ == "__main__":
    main()
