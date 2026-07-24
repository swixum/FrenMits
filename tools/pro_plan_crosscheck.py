#!/usr/bin/env python3
"""
FrenMits pro-plan cross-check (dev-only; NOT shipped in the plugin DLL).

When building an OFFICIAL sheet from logs, this shows what the world's best
parties actually DID for mitigation: the median time they pressed each defensive
across the current top kills. It's a reference to read next to the generator's
Auto-planned sheet before shipping it, so our plan is sanity-checked against real
practice. It never copies their timings into the plan.

Two outputs:
  * PRO TIMELINE  - every mit-press moment (median time across the top N kills),
                    which mit, and how many of the N logs pressed it there.
  * CROSS-CHECK   - with --profile (the same FightProfile the generator takes),
                    each pro moment is put on the SHEET clock via the profile's
                    SyncPoint anchors and lined up against the mits our Auto-plan
                    assigned near that time, so drift and omissions show.

Timings are on the SHEET clock when --profile supplies anchors (the axis our
sheet uses), else fight-relative real seconds (a caveat is printed; top kills
pace similarly but not identically).

Usage:
  pro_plan_crosscheck.py --name "Futures Rewritten" --creds FrenMits.json
  pro_plan_crosscheck.py --encounter-id 1079 --logs 8 --creds FrenMits.json \\
      --profile fru_profile.json [--window 5]
"""

import argparse
import base64
import os
import json
import re
import statistics
import sys
import urllib.parse
import urllib.request
from collections import defaultdict

FFLOGS_TOKEN_URL = "https://www.fflogs.com/oauth/token"
FFLOGS_API_URL = "https://www.fflogs.com/api/v2/client"

# The defensive actions we treat as "a mit press" - identical to the plugin's
# FFLogsClient.MitNames, so the pro data matches what the importer infers from.
MIT_NAMES = [
    "Reprisal", "Feint", "Addle", "Dismantle",
    "Shake It Off", "Divine Veil", "Dark Missionary", "Heart of Light",
    "Temperance", "Liturgy of the Bell", "Asylum", "Plenary Indulgence",
    "Collective Unconscious", "Neutral Sect", "Macrocosmos",
    "Sacred Soil", "Expedient", "Fey Illumination", "Summon Seraph", "Whispering Dawn",
    "Kerachole", "Holos", "Panhaima", "Physis II", "Philosophia",
    "Magick Barrier", "Troubadour", "Tactician", "Shield Samba",
    "Nature's Minne", "Mantra", "Curing Waltz", "Tempera Grassa", "Passage of Arms",
    "Rampart", "Bloodwhetting", "Nascent Flash", "The Blackest Night", "Oblation",
    "Holy Sheltron", "Intervention", "Heart of Corundum", "Aurora",
    "Vengeance", "Damnation", "Sentinel", "Guardian", "Camouflage", "Nebula",
    "Thrill of Battle", "Dark Mind", "Bulwark",
    "Holmgang", "Hallowed Ground", "Living Dead", "Superbolide",
]


# ---- FFLogs client (mirrors src/Core/FFLogsClient.cs) ----------------------

class FFLogs:
    def __init__(self, client_id, secret):
        self._id, self._secret, self._token = client_id, secret, None

    def _token_get(self):
        if self._token:
            return self._token
        auth = base64.b64encode(f"{self._id}:{self._secret}".encode()).decode()
        body = urllib.parse.urlencode({"grant_type": "client_credentials"}).encode()
        req = urllib.request.Request(FFLOGS_TOKEN_URL, data=body, method="POST")
        req.add_header("Authorization", "Basic " + auth)
        req.add_header("Content-Type", "application/x-www-form-urlencoded")
        with urllib.request.urlopen(req, timeout=30) as r:
            self._token = json.load(r)["access_token"]
        return self._token

    def q(self, query, variables=None):
        payload = json.dumps({"query": query, "variables": variables or {}}).encode()
        req = urllib.request.Request(FFLOGS_API_URL, data=payload, method="POST")
        req.add_header("Authorization", "Bearer " + self._token_get())
        req.add_header("Content-Type", "application/json")
        with urllib.request.urlopen(req, timeout=45) as r:
            j = json.load(r)
        if j.get("errors"):
            raise RuntimeError("FFLogs: " + str(j["errors"][0].get("message", "query failed")))
        return j["data"]

    def find_encounter(self, name):
        """(id, name) for the exact match, else the shortest name that contains
        the query - same rule as FFLogsClient.FindEncounterAsync."""
        needle = (name or "").strip().casefold()
        d = self.q("{worldData{expansions{zones{encounters{id name}}}}}")
        exact = contains = None
        for exp in d["worldData"]["expansions"]:
            for z in exp["zones"]:
                for e in z["encounters"]:
                    en = e.get("name") or ""
                    eid = e.get("id") or 0
                    if not eid or not en:
                        continue
                    lc = en.casefold()
                    if lc == needle:
                        exact = (eid, en)
                    elif needle in lc and (contains is None or len(en) < len(contains[1])):
                        contains = (eid, en)
        return exact or contains

    def top_kills(self, encounter_id, metric, count):
        """Up to `count` distinct (code, fightID) from the encounter's rankings."""
        metric = "execution" if metric == "execution" else "speed"
        q = ("query($e:Int!){worldData{encounter(id:$e){"
             f"fightRankings(metric:{metric},page:1)}}}}}}")
        rk = self.q(q, {"e": encounter_id})["worldData"]["encounter"]["fightRankings"]
        if isinstance(rk, str):
            rk = json.loads(rk)
        out, seen = [], set()
        for r in rk.get("rankings", []):
            rep = r.get("report") or {}
            code, fid = rep.get("code"), rep.get("fightID")
            if not code or not fid or (code, fid) in seen:
                continue
            seen.add((code, fid))
            out.append((code, fid, r.get("duration")))
            if len(out) >= count:
                break
        return out

    def fight_window(self, code, fid):
        q = ("query($c:String!,$f:Int!){reportData{report(code:$c){"
             "fights(fightIDs:[$f]){startTime endTime name}}}}")
        f = self.q(q, {"c": code, "f": fid})["reportData"]["report"]["fights"][0]
        return f["startTime"], f["endTime"], f.get("name", "?")

    def _events(self, code, fid, start, end, data_type, hostility, filt=None):
        base = ("query($c:String!,$f:Int!,$s:Float!,$e:Float!"
                + (",$flt:String!" if filt else "") +
                "){reportData{report(code:$c){events(fightIDs:[$f],dataType:" + data_type +
                ",hostilityType:" + hostility +
                (",filterExpression:$flt" if filt else "") +
                ",startTime:$s,endTime:$e,limit:10000){data nextPageTimestamp}}}}")
        out, ps = [], start
        for _ in range(8):
            v = {"c": code, "f": fid, "s": ps, "e": end}
            if filt:
                v["flt"] = filt
            ev = self.q(base, v)["reportData"]["report"]["events"]
            for e in ev.get("data") or []:
                if data_type == "Casts" and e.get("type") != "cast":
                    continue
                out.append((e.get("abilityGameID") or 0, (e["timestamp"] - start) / 1000.0))
            nxt = ev.get("nextPageTimestamp")
            if not nxt:
                break
            ps = nxt
        return out

    def mit_presses(self, code, fid, start, end):
        """(name, sec) for the party's defensive presses in one fight."""
        flt = "ability.name IN (" + ", ".join(f'"{n}"' for n in MIT_NAMES) + ")"
        # We filtered by NAME, so fetch the name too via a second projection is
        # overkill - map ids back through a masterData lookup instead.
        raw = self._events(code, fid, start, end, "Casts", "Friendlies", flt)
        names = self.ability_names(code)
        out = []
        for aid, t in raw:
            nm = names.get(aid)
            if nm:
                out.append((nm, t))
        return out

    def enemy_casts(self, code, fid, start, end, ids):
        """(ability_id, sec) for enemy casts whose id is in `ids` (anchor abilities)."""
        return [(a, t) for a, t in self._events(code, fid, start, end, "Casts", "Enemies")
                if a in ids]

    _names_cache = None
    _names_code = None

    def ability_names(self, code):
        if self._names_code == code and self._names_cache is not None:
            return self._names_cache
        q = ("query($c:String!){reportData{report(code:$c){"
             "masterData{abilities{gameID name}}}}}")
        abilities = (self.q(q, {"c": code})["reportData"]["report"]["masterData"]["abilities"]) or []
        m = {}
        for a in abilities:
            gid = a.get("gameID") or 0
            nm = a.get("name")
            if gid and nm:
                m[gid] = nm.strip()
        self._names_cache, self._names_code = m, code
        return m


# ---- sheet-clock conversion (ported from the scratchpad converter) ---------

def build_map(anchor_casts, sheet_anchors):
    """real->sheet control points from one log. Two-pass: pin abilities that
    occur exactly once in both the sheet anchors and the log (unambiguous), then
    assign every other sheet anchor to the log cast nearest its predicted real
    time. Returns a sorted, monotonic list of (real, sheet) points."""
    byabil = defaultdict(list)
    for aid, t in anchor_casts:
        byabil[aid].append(t)
    for k in byabil:
        byabil[k].sort()
    sheetby = defaultdict(list)
    for aid, sh in sheet_anchors:
        sheetby[aid].append(sh)
    for k in sheetby:
        sheetby[k].sort()

    p1 = [(byabil[a][0], s[0]) for a, s in sheetby.items()
          if len(s) == 1 and len(byabil.get(a, [])) == 1]
    p1.sort()
    m0 = []
    for r, s in p1:
        if not m0 or s > m0[-1][1] + 0.1:
            m0.append((r, s))
    if len(m0) < 2:
        return m0

    def inv(sheet):
        if sheet <= m0[0][1]:
            (r0, s0), (r1, s1) = m0[0], m0[1]
        elif sheet >= m0[-1][1]:
            (r0, s0), (r1, s1) = m0[-2], m0[-1]
        else:
            i = 1
            while m0[i][1] < sheet:
                i += 1
            (r0, s0), (r1, s1) = m0[i - 1], m0[i]
        return r0 + (sheet - s0) * (r1 - r0) / (s1 - s0)

    pairs = []
    for a, shs in sheetby.items():
        rts = byabil.get(a, [])
        for sh in shs:
            pr = inv(sh)
            cand = [t for t in rts if abs(t - pr) < 20]
            if cand:
                pairs.append((min(cand, key=lambda t: abs(t - pr)), sh))
    pairs.sort()
    m = []
    for r, s in pairs:
        if not m or (r > m[-1][0] + 0.1 and s > m[-1][1] + 0.1):
            m.append((r, s))
    return m if len(m) >= 2 else m0


def interp(m, t):
    """real seconds -> sheet seconds via the piecewise-linear map. BETWEEN anchors
    the local slope captures FRU-style forcejump inflation; OUTSIDE the anchored
    span we have no evidence of drift, so assume 1:1 (slope 1) rather than
    extrapolating an end segment's slope - that turned a few seconds past the last
    anchor into minutes of phantom offset."""
    if len(m) < 2:
        return t
    if t <= m[0][0]:
        return m[0][1] + (t - m[0][0])
    for i in range(1, len(m)):
        if t <= m[i][0]:
            (r0, s0), (r1, s1) = m[i - 1], m[i]
            return s0 + (t - r0) * (s1 - s0) / (r1 - r0)
    return m[-1][1] + (t - m[-1][0])


# ---- analysis --------------------------------------------------------------

def sheet_anchors_from_profile(profile):
    out = []
    for sp in profile.get("SyncPoints") or []:
        aid = int(sp.get("Ability", 0)) & 0xFFFFFFFF
        if aid:
            out.append((aid, float(sp.get("Time", 0))))
    return out


def plan_lines(profile):
    """Every planned call across all slots: (time, action). Deduped so one mit
    at one time isn't listed once per column."""
    seen = set()
    out = []
    lists = [profile.get("Lines") or []]
    lists += list((profile.get("SavedSlots") or {}).values())
    for lst in lists:
        for l in lst or []:
            act = (l.get("Action") or "").strip()
            if not act:
                continue
            key = (round(float(l.get("Time", 0))), act.casefold())
            if key in seen:
                continue
            seen.add(key)
            out.append((float(l.get("Time", 0)), act))
    out.sort()
    return out


def cluster(presses, gap):
    """presses: list of (name, time, log_index). Per mit name, group times within
    `gap`s; each group -> (name, median_time, distinct_logs). distinct_logs (not
    raw press count) is the agreement signal, so two tanks pressing one mit in one
    kill still counts as a single log agreeing. Returned sorted by time."""
    bymit = defaultdict(list)
    for nm, t, li in presses:
        bymit[nm].append((t, li))
    moments = []
    for nm, tl in bymit.items():
        tl.sort()
        grp = [tl[0]]
        def flush(g):
            moments.append((nm, statistics.median([x[0] for x in g]), len({x[1] for x in g})))
        for t, li in tl[1:]:
            if t - grp[-1][0] <= gap:
                grp.append((t, li))
            else:
                flush(grp)
                grp = [(t, li)]
        flush(grp)
    moments.sort(key=lambda m: m[1])
    return moments


def group_moments(moments, span):
    """Bucket per-mit moments that land within `span`s into shared timeline
    moments: (median_time, [(name, time, logs), ...])."""
    out = []
    for nm, t, logs in moments:
        if out and t - out[-1][0] <= span:
            out[-1][1].append((nm, t, logs))
            out[-1][0] = statistics.median([x[1] for x in out[-1][1]])
        else:
            out.append([t, [(nm, t, logs)]])
    return out


# ---- report ----------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(description="Cross-check the pros' mit timings against our Auto-plan.")
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--name", help="fight name to resolve (e.g. \"Futures Rewritten\")")
    g.add_argument("--encounter-id", type=int, help="FFLogs encounter id (skip the name lookup)")
    ap.add_argument("--metric", default="speed", choices=["speed", "execution"], help="rankings metric (default speed)")
    ap.add_argument("--logs", type=int, default=8, help="how many top kills to pool (default 8)")
    ap.add_argument("--profile", help="FightProfile JSON (enables sheet-clock + Auto-plan cross-check)")
    ap.add_argument("--window", type=float, default=5.0, help="match window vs our plan, seconds (default 5)")
    ap.add_argument("--gap", type=float, default=8.0, help="cluster gap for one mit's presses, seconds (default 8)")
    ap.add_argument("--creds", help="FrenMits.json to read FflogsClientId/Secret from")
    ap.add_argument("--id", help="FFLogs client id (overrides --creds)")
    ap.add_argument("--secret", help="FFLogs client secret")
    args = ap.parse_args()

    cid, secret = args.id, args.secret
    if (not cid or not secret) and args.creds:
        with open(args.creds, "r", encoding="utf-8-sig") as fh:
            cfg = json.load(fh)
        cid = cid or cfg.get("FflogsClientId")
        secret = secret or cfg.get("FflogsClientSecret")
        # Newer configs store the secret DPAPI-encrypted (FflogsClientSecretEnc),
        # which this tool can't read; fall through to env / --secret.
        if not secret and cfg.get("FflogsClientSecretEnc"):
            print("note: --creds config stores the secret encrypted now; pass --secret or set FFLOGS_CLIENT_SECRET.")
    cid = cid or os.environ.get("FFLOGS_CLIENT_ID")
    secret = secret or os.environ.get("FFLOGS_CLIENT_SECRET")
    if not cid or not secret:
        sys.exit("Need FFLogs creds (--id/--secret, FFLOGS_CLIENT_ID/FFLOGS_CLIENT_SECRET env, or --creds FrenMits.json).")

    profile = None
    sheet_anchors = []
    if args.profile:
        with open(args.profile, "r", encoding="utf-8-sig") as fh:
            profile = json.load(fh)
        if isinstance(profile, dict) and "Fights" in profile:
            sys.exit("Pass a single FightProfile (export it), not the whole FrenMits.json.")
        sheet_anchors = sheet_anchors_from_profile(profile)

    fl = FFLogs(cid, secret)

    if args.encounter_id:
        enc_id, enc_name = args.encounter_id, f"encounter {args.encounter_id}"
    else:
        found = fl.find_encounter(args.name)
        if not found:
            sys.exit(f"No encounter matches \"{args.name}\".")
        enc_id, enc_name = found
    print(f"# {enc_name} (encounter {enc_id}) - top {args.logs} {args.metric} kills", file=sys.stderr)

    kills = fl.top_kills(enc_id, args.metric, args.logs)
    if not kills:
        sys.exit("That encounter has no ranked kills yet.")

    anchor_ids = {a for a, _ in sheet_anchors}
    on_sheet = bool(sheet_anchors)
    presses = []          # (name, clock_time) pooled across logs
    used = skipped = 0
    for code, fid, dur in kills:
        try:
            start, end, fname = fl.fight_window(code, fid)
            mits = fl.mit_presses(code, fid, start, end)
            conv = None
            if on_sheet:
                ac = fl.enemy_casts(code, fid, start, end, anchor_ids)
                m = build_map(ac, sheet_anchors)
                if len(m) < 2:
                    print(f"  ! {code}#{fid}: too few anchors resolved; skipped for sheet-clock.", file=sys.stderr)
                    skipped += 1
                    continue
                conv = m
            for nm, t in mits:
                presses.append((nm, interp(conv, t) if conv else t, used))
            used += 1
            print(f"  {code}#{fid}: {fname}, {len(mits)} mit presses"
                  + (f" ({dur/1000:.0f}s)" if dur else ""), file=sys.stderr)
        except Exception as ex:  # one bad log must not sink the run
            print(f"  ! {code}#{fid} failed: {ex}", file=sys.stderr)
            skipped += 1

    if used == 0:
        sys.exit("No logs contributed data.")

    clock = "sheet clock" if on_sheet else "real seconds (no --profile: pacing varies between kills)"
    moments = cluster(presses, args.gap)
    # Keep the ones a majority of the used logs agree on: a real party moment,
    # not one player's off-plan press.
    strong = [(nm, t, logs) for (nm, t, logs) in moments if logs * 2 >= used]
    # On the sheet clock, trust only the anchored span (+ a short lead/tail for
    # prepull and cleanup): outside it there's no anchor pinning the conversion,
    # so a converted time there is a guess, not a fact.
    if on_sheet and sheet_anchors:
        lo = min(s for _, s in sheet_anchors) - 15
        hi = max(s for _, s in sheet_anchors) + 15
        dropped = sum(1 for _, t, _ in strong if not lo <= t <= hi)
        strong = [(nm, t, logs) for (nm, t, logs) in strong if lo <= t <= hi]
        if dropped:
            print(f"  ({dropped} press moment(s) outside the anchored span {lo:.0f}..{hi:.0f}s were dropped.)",
                  file=sys.stderr)

    print(f"\n=== PRO MIT TIMELINE ({used} logs, {clock}) ===")
    print("   Each row: median press time, mit, and how many of the logs pressed it there.")
    for t, mm in group_moments(strong, args.window):
        mins = f"{int(t)//60}:{int(t)%60:02d}"
        parts = ", ".join(f"{nm} ({logs}/{used})" for nm, _, logs in sorted(mm, key=lambda x: -x[2]))
        print(f"  {mins:>6} ({t:6.1f}s)  {parts}")

    if not profile:
        print("\n(Pass --profile to line these up against our Auto-plan on the sheet clock.)")
        return

    plan = plan_lines(profile)
    print(f"\n=== CROSS-CHECK vs our Auto-plan ({len(plan)} planned calls, +/-{args.window:.0f}s) ===")
    print("   PRO = what the top parties pressed there; OURS = what Auto-plan assigned nearby.")
    for t, mm in group_moments(strong, args.window):
        mins = f"{int(t)//60}:{int(t)%60:02d}"
        pro = ", ".join(sorted({nm for nm, _, _ in mm}))
        ours = sorted({act for lt, act in plan if abs(lt - t) <= args.window})
        ours_s = "; ".join(ours) if ours else "(nothing within window)"
        flag = "" if ours else "   <-- pros mit here, our plan doesn't"
        print(f"  {mins:>6} ({t:6.1f}s){flag}")
        print(f"           PRO : {pro}")
        print(f"           OURS: {ours_s}")
    # Our plan moments the pros don't cover (possible over-planning).
    covered = [t for t, _ in group_moments(strong, args.window)]
    lonely = [(lt, act) for lt, act in plan
              if all(abs(lt - t) > args.window for t in covered)]
    if lonely:
        print(f"\n  Our plan presses with NO pro mit within {args.window:.0f}s (worth a look):")
        for lt, act in lonely:
            print(f"    {int(lt)//60}:{int(lt)%60:02d} ({lt:6.1f}s)  {act}")


if __name__ == "__main__":
    main()
