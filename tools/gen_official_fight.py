#!/usr/bin/env python3
"""
FrenMits official-fight generator (dev-only; NOT shipped in the plugin DLL).

Turns a finished in-game sheet into a built-in fight WITH its Auto-planned mit
sheet baked into every column. The intended flow (the last step of the
official-fight pipeline - see tools/OFFICIAL_FIGHT.md) is:

    Build from FFLogs  ->  Auto-plan  ->  tweak in-game  ->  verify_fight_logs
    ->  pro_plan_crosscheck (pre-ship check)  ->  run this

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


# How far from its row a planned press may sit and still belong to it.
#
# Asymmetric on purpose: a mitigation is pressed BEFORE the hit it covers, often
# several seconds early so it is already up when the damage lands (the planner
# does this deliberately - Meteorain's cover goes down 6s ahead of the cast). A
# press after its own mechanic is nearly always a different one, so that side
# stays tight. A symmetric 2s window silently dropped every prep press.
#
# 15s is the duration of the standard party mitigations (Reprisal, Feint, Addle,
# Sacred Soil, Kerachole): press one earlier than that and it has expired before
# the hit, so it cannot have been meant for it.
#
# Only a floor, though. A mit that lasts longer can legitimately be planned
# further ahead - Doomtrain leads Asylum (24s) twenty seconds into Dead Man's
# Blastpipe - so the real bound is the buff's own length, read below.
EARLY_S = 15.0
LATE_S = 2.5


def _load_durations():
    """Buff lengths, read out of the plugin's own hand-curated table.

    Parsed rather than copied so there is one place to maintain: the generator
    and the plugin can't disagree about how long Asylum lasts.
    """
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                        "..", "src", "Core", "Cooldowns.cs")
    try:
        with open(path, encoding="utf-8-sig") as fh:
            text = fh.read()
        block = text[text.index("Durations = new"):]
        block = block[:block.index("};")]
        return {m.group(1): float(m.group(2))
                for m in re.finditer(r'\["([^"]+)"\] = (\d+(?:\.\d+)?)', block)}
    except Exception as ex:                                   # noqa: BLE001
        print(f"! note: couldn't read buff durations ({ex}); using {EARLY_S:g}s for every press.",
              file=sys.stderr)
        return {}


DURATIONS = _load_durations()


def early_window(action):
    """How far ahead of its mechanic a press may sit and still belong to it.

    The longest buff named in the cell, floored at the standard 15s. Longest,
    not shortest, because this only decides which ROW the press belongs to, and
    assign_lines already takes the nearest row of that name - being generous here
    cannot pull a press onto a different instance, it can only stop one being
    orphaned.
    """
    lower = action.lower()
    hits = [d for name, d in DURATIONS.items() if name.lower() in lower]
    return max(hits + [EARLY_S])


# FFLogs names an ability it doesn't know "unknown_<hex>", and for some the game's
# own Action sheet has no name either. Those are never mechanics: Doomtrain's
# unknown_b294 is the boss auto-attack, 104 hits on exactly two targets, and the
# import turned it into 53 of that sheet's 118 rows. A bar with no name to show is
# not worth a row, and the tank mits the planner hung on them were being spent on
# auto-attacks.
UNNAMED_RE = re.compile(r"^unknown[_ ]?[0-9a-f]+$", re.IGNORECASE)


def is_unnamed(mechanic):
    return bool(UNNAMED_RE.match((mechanic or "").strip()))


# A generic mit term reads as whatever the player looking at it actually presses,
# which only works for a column whose every job HAS one. Casters have Addle and
# melee have Feint, and that is all: Magick Barrier and Tempera Grassa belong to
# RDM and PCT alone, and they are JobExtras with their own schedules. A Black Mage
# handed a "Party Mit" call has no button behind it.
#
# Auto-plan used to write one into the caster column, so a sheet planned before
# that was fixed carries calls nobody can answer. An official fight can't be
# re-planned in game - it isn't editable - so the bake is where they come out.
NO_PARTY_MIT = {"M1", "M2", "R2"}


def pressable(action, slot):
    """`action` with any segment this column cannot press removed ("" if none is).

    A QUALIFIED term is left alone: "Party Mit (RDM)" names the job it is for, so
    whoever wrote it meant it. Only the bare generic is a planner artefact.
    """
    if slot not in NO_PARTY_MIT:
        return action
    kept = [p for p in (part.strip() for part in action.split("+"))
            if p and not mech_eq(p, "Party Mit")]
    return " + ".join(kept)


def assign_lines(custom_rows, slot_lines, canon_slots, dropped=None):
    """Attach every planned press to exactly ONE row: the nearest row sharing its
    mechanic name. Same rule the plugin's own board uses, so what gets baked is
    what the sheet showed - a mechanic that recurs can't collect another
    instance's presses just by being inside a wide window."""
    per_row = [{slot: [] for slot in canon_slots} for _ in custom_rows]
    for slot in canon_slots:
        for l in slot_lines[slot]:
            if not l.get("Enabled", True):
                continue
            act = (l.get("Action", "") or "").strip()
            if not act:
                continue
            keep = pressable(act, slot)
            if keep != act and dropped is not None:
                dropped.append((slot, float(l.get("Time", 0)), act, l.get("Mechanic", "")))
            act = keep
            if not act:
                continue
            lt = float(l.get("Time", 0))
            lm = l.get("Mechanic", "")
            early = early_window(act)
            best, best_gap = None, None
            for i, cr in enumerate(custom_rows):
                if not mech_eq(cr.get("Mechanic", ""), lm):
                    continue
                rt = float(cr.get("Time", 0))
                delta = rt - lt              # + => the press is early, which is normal
                if delta > early or delta < -LATE_S:
                    continue
                gap = abs(delta)
                if best_gap is None or gap < best_gap:
                    best, best_gap = i, gap
            if best is None:
                continue
            bucket = per_row[best][slot]
            if act not in bucket:
                bucket.append(act)
    return [[" + ".join(cells[slot]) for slot in canon_slots] for cells in per_row]


# How far an anchor may sit from the row it belongs to. A row is written at
# MathF.Round of its own cast's time, so its cast is inside half a second of it and
# anything further away is a DIFFERENT cast. Being generous here does real damage:
# at 3s, M9S's Coffinfiller row at 114s took the anchor for the cast at 111s, and
# an anchor that names the wrong moment re-bases the clock to the wrong moment.
ANCHOR_S = 1.0


def assign_syncs(rows, syncs, unreliable=()):
    """Give each row the cast that IS it, and give each cast to one row only.

    Anchors carry the ability's name (Label), and so do rows, so the two can be
    matched on what they ARE rather than on which happens to be closest. That
    matters because a nearest-in-time lookup is a fuzzy match, not an identity:
    it hands the SAME cast id to every row inside its window, and two rows a
    second apart are normally two different mechanics, not one baked twice.

    That mistake used to be repaired downstream by deleting one of the two rows,
    which lost real hits and the mits written for them - M9S alone lost twelve
    (Coffinfiller landing on the same second as Half Moon, Plummet on
    Gravegrazer, Ultrasonic Spread on Blood Lash, all with their own casts and
    their own presses). Assigning correctly in the first place means nothing has
    to be thrown away, and the invariant that no cast is anchored twice holds by
    construction rather than by repair.

    The name has to agree, not merely help: both sides were written from the same
    log by the same import, so a row IS its cast's name. Letting proximity alone
    stand in gave M9S's Half Moon row the anchor for the Coffinfiller landing
    beside it - an anchor that snaps at the right moment while naming the wrong
    mechanic. Among the casts that do agree, the nearest wins, and both the row
    and the cast are then spent. A row that matches nothing keeps 0 - it simply
    isn't a resync point, which costs nothing.

    `unreliable` is the id list verify_fight_logs.py produces for casts whose time
    moves from pull to pull (a mechanic drawn from several ability ids at random).
    The sheet only ever saw one of them, so anchoring it would re-base the clock
    onto a moment that pull didn't have.
    """
    unreliable = set(unreliable)
    pairs = []
    for ri, r in enumerate(rows):
        rt, rm = float(r.get("Time", 0)), r.get("Mechanic", "")
        for si, sp in enumerate(syncs):
            if (int(sp.get("Ability", 0)) & 0xFFFFFFFF) in unreliable:
                continue
            if not mech_eq(sp.get("Label", ""), rm):
                continue
            gap = abs(float(sp.get("Time", 0)) - rt)
            if gap >= ANCHOR_S:
                continue
            pairs.append((gap, ri, si))
    pairs.sort()

    out = [0] * len(rows)
    used_rows, used_syncs, placed = set(), set(), []
    for _, ri, si in pairs:
        if ri in used_rows or si in used_syncs:
            continue
        ability = int(syncs[si].get("Ability", 0)) & 0xFFFFFFFF
        if not ability:
            continue
        # The one thing that genuinely breaks: the same ability id anchoring two
        # rows a second apart, which leaves replay auto-start unable to tell
        # which one it just saw. Distinct anchors that share an id are fine when
        # they're far enough apart - that's just a mechanic recurring.
        t = float(rows[ri].get("Time", 0))
        if any(a == ability and abs(t - pt) <= 1.0 for a, pt in placed):
            continue
        used_rows.add(ri)
        used_syncs.add(si)
        placed.append((ability, t))
        out[ri] = ability
    return out


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

    public sealed record Entry(int Time, string Phase, string Mechanic, uint Sync, string[] Actions,
                              int Hurt = 0, bool Buster = false, bool Enrage = false);

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
        var seen = new HashSet<(int Time, string Action)>();
        foreach (var e in Timeline)
        {{
            var action = e.Actions[idx].Trim();
            if (action.Length == 0) continue;
            // The same button on the same second is one press, however many rows
            // ask for it. Keyed on the ACTION, not the row's cast: two mechanics
            // do land on one second, and a player told to press different things
            // for each is being told to press both.
            if (!seen.Add((e.Time, action))) continue;
            list.Add(new MitLine {{ Time = e.Time, Mechanic = e.Mechanic, Action = action, Enabled = true }});
        }}
        return list;
    }}

    // Resync anchors: the first synced cast of each phase re-bases the clock
    // (wide window), so a faster/slower kill still snaps into place.
    // The board's severity marks (! !! !!!) and tank-buster icon come off
    // FightProfile.CustomRows - Hurt/Buster live nowhere else - so a built-in has
    // to hand them over the same way SyncPoints and BossAnchors are handed over,
    // or an official sheet silently loses every grade the source sheet had.
    public static List<CustomRow> CustomRows()
    {{
        var rows = new List<CustomRow>();
        foreach (var e in Timeline)
            if (e.Hurt > 0 || e.Buster || e.Enrage)
                rows.Add(new CustomRow {{ Time = e.Time, Mechanic = e.Mechanic, Hurt = e.Hurt,
                                          Buster = e.Buster, Enrage = e.Enrage }});
        return rows;
    }}

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
        tail = ""
        if r.get("hurt") or r.get("buster") or r.get("enrage"):
            tail = f', {int(r.get("hurt") or 0)}, {"true" if r.get("buster") else "false"}'
            if r.get("enrage"):
                tail += ", true"
        lines.append(
            f'        new({r["time"]}, {cs_str(r["phase"])}, {cs_str(r["mech"])}, '
            f'{sync}, new[]{{{acts}}}{tail}),')
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
    L.append(f"8) CustomRows(): add a case (severity marks + buster icons):")
    L.append(f"   {cls}Territory => {cls}Data.CustomRows(),")
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


def verify_bake(out_path, profile, canon_slots, legacy_slots, rows):
    """Read the file we just wrote back and prove it still says what the sheet said.

    Every check here exists because the generator silently got it wrong once:
    presses vanished (the attach window was symmetric, but mits are pressed
    BEFORE their mechanic), grades vanished (the baked shape had nowhere to put
    them), and one cast produced two anchors (which breaks replay auto-start).

    Returns a list of problems; empty means the bake is faithful.
    """
    import re as _re
    text = open(out_path, encoding="utf-8").read()
    baked = []
    for m in _re.finditer(
            r'new\((\d+), "(\w+)", "([^"]*)", (0x[0-9A-Fa-f]+|0), new\[\]\{(.*?)\}'
            r'(?:, (\d+), (true|false)(?:, (true|false))?)?\),',
            text):
        cells = _re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(5))
        baked.append({"t": int(m.group(1)), "mech": m.group(3), "sync": m.group(4),
                      "cells": dict(zip(legacy_slots, cells)),
                      "hurt": int(m.group(6) or 0), "buster": m.group(7) == "true",
                      "enrage": m.group(8) == "true"})

    problems = []
    if len(baked) != len(rows):
        problems.append(f"wrote {len(rows)} rows but only {len(baked)} parsed back")

    # 0. every row the sheet had is still a row. The generator used to delete
    #    rows it believed were one cast baked twice, and it was wrong about which
    #    ones - a hit that vanishes here takes its whole column of mits with it.
    left = list(baked)
    for cr in profile.get("CustomRows") or []:
        t, mech = float(cr.get("Time", 0)), (cr.get("Mechanic") or "")
        if is_unnamed(mech):
            continue                        # skipped on purpose, and reported
        hit = next((b for b in left if mech_eq(b["mech"], mech) and abs(b["t"] - t) <= 1), None)
        if hit is None:
            problems.append(f"row lost: {mech!r} at {t:g}s")
        else:
            left.remove(hit)

    # 1. every planned press survived, in its own column
    slot_lines = slot_line_map(profile, canon_slots)
    leg = dict(zip(canon_slots, legacy_slots))
    total = missing = 0
    for slot in canon_slots:
        col = leg[slot]
        for l in slot_lines[slot]:
            if not l.get("Enabled", True):
                continue
            # What the sheet asked for, minus anything this column has no button
            # for. Deliberately dropped, and reported above, so it is not missing.
            act = pressable((l.get("Action", "") or "").strip(), slot)
            if not act or is_unnamed(l.get("Mechanic")):
                continue
            total += 1
            lt = float(l.get("Time", 0))
            early = early_window(act) + 2.0
            if not any(act in b["cells"].get(col, "")
                       for b in baked if -LATE_S <= b["t"] - lt <= early):
                missing += 1
                if missing <= 5:
                    problems.append(f"press dropped: {slot} {act!r} at {lt:g}s ({l.get('Mechanic','')})")
    if missing > 5:
        problems.append(f"...and {missing - 5} more dropped presses")

    # 2. every graded row still carries at least the grade it had
    for cr in profile.get("CustomRows") or []:
        hurt, bust = int(cr.get("Hurt", 0) or 0), bool(cr.get("Buster"))
        if not (hurt or bust) or is_unnamed(cr.get("Mechanic")):
            continue
        t, mech = float(cr.get("Time", 0)), (cr.get("Mechanic") or "")
        near = [b for b in baked if abs(b["t"] - t) <= 3.5]
        if not any(b["hurt"] >= hurt and (b["buster"] or not bust) for b in near):
            problems.append(f"grade lost: {mech!r} at {t:g}s (Hurt={hurt}, Buster={bust})")

    # 3. no cast anchored twice - replay auto-start can only use an ability that
    #    appears exactly once, and the plugin's own test suite rejects this too
    by_sync = {}
    for b in baked:
        if b["sync"] != "0":
            by_sync.setdefault(b["sync"], []).append(b["t"])
    for sync, ts in by_sync.items():
        ts.sort()
        for a, c in zip(ts, ts[1:]):
            if c - a <= 1:
                problems.append(f"cast {sync} anchored twice, at {a}s and {c}s")

    # 4. rows in time order, nothing blank, every column present
    for i in range(1, len(baked)):
        if baked[i]["t"] < baked[i - 1]["t"]:
            problems.append(f"rows out of order at {baked[i]['t']}s")
            break
    for b in baked:
        if not b["mech"].strip():
            problems.append(f"blank mechanic name at {b['t']}s")
            break
        if len(b["cells"]) != len(legacy_slots):
            problems.append(f"row at {b['t']}s has {len(b['cells'])} columns, expected {len(legacy_slots)}")
            break

    return problems, total, missing


def main():
    ap = argparse.ArgumentParser(description="Generate a built-in FrenMits fight from a saved sheet.")
    ap.add_argument("config", help="FrenMits.json or a single exported FightProfile JSON")
    ap.add_argument("--class", dest="cls", required=True, help="C# class stem, e.g. P8s -> P8sData")
    ap.add_argument("--name", help="display name (defaults to the profile's Name)")
    ap.add_argument("--category", default="Savage", help='e.g. "Ultimate" or "Savage" (default Savage)')
    ap.add_argument("--territory", type=int, help="pick the fight by TerritoryId")
    ap.add_argument("--name-match", help="pick the fight whose Name contains this")
    ap.add_argument("--index", type=int, help="pick the fight by position in the config")
    ap.add_argument("--unreliable", help="JSON list of ability ids that must never be anchors "
                                         "(verify_fight_logs.py --write-unreliable writes it)")
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

    unnamed = [r for r in custom_rows if is_unnamed(r.get("Mechanic"))]
    if unnamed:
        custom_rows = [r for r in custom_rows if not is_unnamed(r.get("Mechanic"))]
        names = sorted({(r.get("Mechanic") or "").strip() for r in unnamed})
        print(f"  skipped {len(unnamed)} row(s) the game has no name for ({', '.join(names)}) - "
              f"{len(custom_rows)} left", file=sys.stderr)
        if not custom_rows:
            sys.exit("Every row was an unnamed ability - nothing to bake.")

    unreliable = []
    if args.unreliable:
        with open(args.unreliable, "r", encoding="utf-8-sig") as fh:
            unreliable = [int(str(x), 0) & 0xFFFFFFFF for x in json.load(fh)]

    dropped = []
    all_actions = assign_lines(custom_rows, slot_lines, canon_slots, dropped)
    all_syncs = assign_syncs(custom_rows, syncs, unreliable)

    if dropped:
        print(f"  dropped {len(dropped)} call(s) the column cannot press:", file=sys.stderr)
        for slot, t, act, mech in dropped:
            left = pressable(act, slot)
            print(f"     {slot} {t:g}s {mech}: {act!r} -> {left or '(nothing left)'}", file=sys.stderr)

    rows = []
    planned = 0
    for ri, cr in enumerate(custom_rows):
        t = float(cr.get("Time", 0))
        acts = all_actions[ri]
        if any(a for a in acts):
            planned += 1
        rows.append({
            "time": int(round(t)),
            "phase": phase_of(t, bounds),
            "mech": cr.get("Mechanic", ""),
            "sync": all_syncs[ri],
            "actions": acts,
            "hurt": int(cr.get("Hurt", 0) or 0),
            "buster": bool(cr.get("Buster", False)),
            "enrage": bool(cr.get("Enrage", False)),
        })

    anchored = sum(1 for r in rows if r["sync"])
    skipped = f", {len(unreliable)} cast(s) held back as unanchorable" if unreliable else ""
    print(f"  {len(rows)} rows, {anchored} of them resync anchors{skipped}", file=sys.stderr)

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

    problems, presses, dropped = verify_bake(out, profile, canon_slots, legacy_slots, rows)
    print(f"  self-check: {presses - dropped}/{presses} presses baked, "
          f"{sum(1 for r in rows if r.get('hurt') or r.get('buster'))} graded rows", file=sys.stderr)
    if problems:
        print("\nSELF-CHECK FAILED - this bake is NOT faithful to the sheet:", file=sys.stderr)
        for pr in problems:
            print(f"  ! {pr}", file=sys.stderr)
        sys.exit(1)

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
