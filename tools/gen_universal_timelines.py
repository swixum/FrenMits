# Regenerates src/Data/universal_timelines.json.gz from a cactbot checkout.
# New expansion / new duties: clone (or pull) cactbot next to this script as
# ./cactbot (override with the CACTBOT env var), run this script, rebuild the
# plugin. The output is written straight into src/Data.
import os, re, json, gzip, glob, sys

HERE = os.path.dirname(os.path.abspath(__file__))
CB = os.environ.get("CACTBOT") or os.path.join(HERE, "cactbot")
DATA = os.path.join(CB, "ui", "raidboss", "data")
OUT = os.path.join(HERE, "..", "src", "Data", "universal_timelines.json.gz")

# zone name -> territory id
zid = {}
for m in re.finditer(r"'([A-Za-z0-9]+)':\s*(\d+)", open(os.path.join(CB,"resources","zone_id.ts")).read()):
    zid[m.group(1)] = int(m.group(2))
if not zid:
    sys.exit("zone_id.ts parsed to nothing - cactbot layout changed, fix the regex before trusting any output")

# trigger files -> (zone, timelineFile). zoneId is a scalar or an array
# (duties sharing one trigger/timeline file); take every zone either way.
mapping = []  # (territory, txt_path)
for ts in glob.glob(os.path.join(DATA, "**", "*.ts"), recursive=True):
    if os.path.basename(ts) == "test.ts": continue  # cactbot's striking-dummy test timeline
    src = open(ts, encoding="utf-8").read()
    tm = re.search(r"timelineFile:\s*'([^']+)'", src)
    if not tm: continue
    zm = re.search(r"zoneId:\s*ZoneId\.([A-Za-z0-9]+)", src)
    za = re.search(r"zoneId:\s*\[([^\]]*)\]", src, re.S)
    names = [zm.group(1)] if zm else re.findall(r"ZoneId\.([A-Za-z0-9]+)", za.group(1)) if za else []
    txt = os.path.join(os.path.dirname(ts), tm.group(1))
    if names and not os.path.exists(txt):
        print(f"WARN: {os.path.relpath(ts, DATA)} references missing timeline {tm.group(1)} - duty dropped")
        continue
    for n in names:
        if n in zid: mapping.append((zid[n], txt))

entry_re = re.compile(r'^\s*([0-9]+(?:\.[0-9]+)?)\s+(?:label\s+)?"([^"]*)"\s*(.*)$')
sync_re = re.compile(r'^(Ability|StartsUsing)\s*\{([^}]*)\}')
id_re = re.compile(r'id:\s*(?:"([0-9A-Fa-f]+)"|\[\s*"([0-9A-Fa-f]+)")')
win_re = re.compile(r'window\s+([0-9.]+)\s*,')

# Zones whose bosses spawn in arbitrary order, so no linear timeline can be
# correct: Bozjan Southern Front, Zadnor, Occult Crescent South Horn. Everything
# else that sits on far-apart jump blocks is linear and gets compacted instead.
FIELD_OPS = {920, 975, 1252}

# One encounter per 1000-second block, and how far the plugin's resync engine
# will jump forward to land on one (SyncEngine.TimelineBlockReach).
BLOCK = 1000.0
REACH = 20000.0
# The gap that separates one encounter from the next; matches the threshold the
# phase-anchor promotion below already uses.
BLOCK_GAP = 150.0


def compact_blocks(entries, syncs):
    """Renumber far-apart cactbot jump segments onto sequential 1000s blocks.

    Each segment keeps its internal timing exactly; only the base moves, so the
    whole duty fits inside the resync engine's forward window and every
    encounter stays reachable from a standing start.
    """
    starts = [entries[0][0]]
    for (prev_t, _), (t, _) in zip(entries, entries[1:]):
        if t - prev_t > BLOCK_GAP:
            starts.append(t)

    def remap(t):
        i = 0
        for j, s in enumerate(starts):
            if t >= s - 0.001:
                i = j
            else:
                break
        return round((i + 1) * BLOCK + (t - starts[i]), 1)

    # Syncs sitting before the first segment can't belong to any block (they're
    # the file's pre-pull bookkeeping); dropping them beats mapping them onto a
    # bogus coordinate the clock could then snap to.
    syncs = [s for s in syncs if s[0] >= starts[0] - 30.0]
    return ([[remap(t), n] for t, n in entries],
            [[remap(t), a, p] for t, a, p in syncs])


zones = {}
for terr, txt in mapping:
    entries, syncs = [], []
    for raw in open(txt, encoding="utf-8"):
        line = raw.strip()
        if not line or line.startswith("#") or line.startswith("hideall") or line.startswith("alertall"):
            continue
        m = entry_re.match(line)
        if not m: continue
        t = float(m.group(1)); name = m.group(2); rest = m.group(3)
        # strip trailing comment
        rest = rest.split("#", 1)[0].strip()
        sm = sync_re.match(rest)
        if sm:
            im = id_re.search(sm.group(2))
            if im:
                aid = int(im.group(1) or im.group(2), 16)
                wm = win_re.search(rest)
                phase = bool(wm and float(wm.group(1)) >= 60)
                syncs.append([round(t, 1), aid, 1 if phase else 0])
        if name.startswith("--") or 'label' in line.split('"')[0]:
            continue
        if not name or name in ("Start",): continue
        entries.append([round(t, 1), name])
    if len(entries) < 3: continue
    # The board consumes entries in file order; branching blocks in cactbot
    # files aren't time-sorted, so sort here.
    entries.sort(key=lambda e: e[0])
    if entries[-1][0] > 10000:
        # A cactbot "jump" timeline parks later segments tens of thousands of
        # seconds apart. That used to mean the duty was dropped outright, which
        # is why a whole alliance raid (Jeuno) had no timeline at all. Only true
        # FIELD OPERATIONS actually need dropping - their bosses spawn in
        # arbitrary order, so no linear timeline can be right. Everything else
        # is linear and just needs its segments brought back within reach.
        if terr in FIELD_OPS: continue
        entries, syncs = compact_blocks(entries, syncs)
        if entries[-1][0] > REACH:
            print(f"WARN: terr {terr} needs {entries[-1][0]:.0f}s of clock, past the "
                  f"{REACH:.0f}s resync reach - dropped")
            continue
    else:
        # Placeholder far-future lines ("Ultima Enrage" at 10000.0) are noise,
        # not mechanics. Only meaningful for timelines that DIDN'T get compacted;
        # in a compacted one every block is real content.
        entries = [e for e in entries if e[0] < 9999]
    if len(entries) < 3: continue
    # dedupe identical (time,name)
    seen = set(); e2 = []
    for t, n in entries:
        if (t, n) in seen: continue
        seen.add((t, n)); e2.append([t, n])
    # cap syncs (dense timelines have one per entry; thin them: keep phase
    # anchors and then at most one sync per ~10s bucket)
    syncs.sort()
    kept, lastt = [], -99.0
    for t, aid, ph in syncs:
        if ph or t - lastt >= 10:
            kept.append([t, aid, ph]); lastt = t
    # Timelines that use cactbot "jump" put later segments on far-apart time
    # blocks; cactbot hops there via jump, the plugin hops via phase anchors
    # with the wide forward sync window. Every block therefore needs at least one
    # PHASE anchor, or the clock can never enter it and that whole encounter
    # shows an empty board.
    #
    # Promote the first sync anywhere INSIDE each block. The old rule only looked
    # within 90s of the block's opening line, so a boss whose first anchorable
    # cast came later was left stranded with no way in at all - better to join
    # that encounter a little late than never.
    block_starts = [e2[0][0]]
    for (prev_t, _), (t, _) in zip(e2, e2[1:]):
        if t - prev_t > BLOCK_GAP:
            block_starts.append(t)
    for i, bs in enumerate(block_starts):
        be = block_starts[i + 1] if i + 1 < len(block_starts) else float("inf")
        for s in kept:
            if bs - 5 <= s[0] < be:
                s[2] = 1
                break
    z = {"e": e2, "s": kept}
    if terr in zones and len(zones[terr]["e"]) >= len(e2): continue
    zones[terr] = z

out = json.dumps(zones, separators=(",", ":"))
gz = gzip.compress(out.encode("utf-8"), 9)
open(OUT, "wb").write(gz)
ecount = sum(len(z["e"]) for z in zones.values())
scount = sum(len(z["s"]) for z in zones.values())
print(f"zones={len(zones)} entries={ecount} syncs={scount} json={len(out)//1024}KB gz={len(gz)//1024}KB -> {os.path.normpath(OUT)}")
# spot checks
for t in (1199, 968, 1122, 831, 1252):  # Alexandria? DSR, TOP, Twinning?, DT dungeon
    if t in zones: print(t, "entries:", len(zones[t]["e"]), "syncs:", len(zones[t]["s"]), "first:", zones[t]["e"][:2])
