#!/usr/bin/env python3
"""
FrenMits multi-log verifier (dev-only; NOT shipped in the plugin DLL).

You import ONE log in-game and Auto-plan it; this cross-checks that single
import against SEVERAL FFLogs kills of the same fight, so an official sheet's
timings, anchors, and untargetable windows are consensus, not one pull's quirks.

It reports:
  * ANCHOR DRIFT   - each anchored mechanic's time in your profile vs the median
                     resolve time across the logs (flags > tolerance).
  * MISSING        - telegraphed (cast-bar) mechanics present in most logs but
                     absent from your profile's anchors (your one import missed).
  * DOWNTIME       - median untargetable start/duration across logs vs yours.
It can also write a timing-corrected copy of the profile (medians nudged in,
existing rows only - it never fabricates a planned row) for the generator.

Two ways to feed it logs:
  * live:    --reports URL... with FFLogs creds (--creds FrenMits.json, or
             --id/--secret). Mirrors the plugin's FFLogsClient queries.
  * offline: --casts-json a.json b.json ...  (each a list of
             {"AbilityId":int,"Time":float,"HasCastBar":bool}) - no network,
             used for testing / when casts are already exported.

Usage:
  verify_fight_logs.py PROFILE.json --reports LINK1 LINK2 LINK3 --creds FrenMits.json
  verify_fight_logs.py PROFILE.json --casts-json l1.json l2.json l3.json
     [--tolerance 2.0] [--write-corrected corrected_profile.json]
"""

import argparse
import base64
import json
import math
import os
import statistics
import sys
import urllib.parse
import urllib.request

FFLOGS_TOKEN_URL = "https://www.fflogs.com/oauth/token"
FFLOGS_API_URL = "https://www.fflogs.com/api/v2/client"


# ---- FFLogs client (ported from src/Core/FFLogsClient.cs) ------------------

class FFLogs:
    def __init__(self, client_id, secret):
        self._id = client_id
        self._secret = secret
        self._token = None

    def _token_get(self):
        if self._token:
            return self._token
        auth = base64.b64encode(f"{self._id}:{self._secret}".encode()).decode()
        body = urllib.parse.urlencode({"grant_type": "client_credentials"}).encode()
        req = urllib.request.Request(FFLOGS_TOKEN_URL, data=body, method="POST")
        req.add_header("Authorization", "Basic " + auth)
        req.add_header("Content-Type", "application/x-www-form-urlencoded")
        with urllib.request.urlopen(req, timeout=30) as r:
            j = json.load(r)
        self._token = j["access_token"]
        return self._token

    def _query(self, query, variables):
        payload = json.dumps({"query": query, "variables": variables}).encode()
        req = urllib.request.Request(FFLOGS_API_URL, data=payload, method="POST")
        req.add_header("Authorization", "Bearer " + self._token_get())
        req.add_header("Content-Type", "application/json")
        with urllib.request.urlopen(req, timeout=30) as r:
            j = json.load(r)
        if j.get("errors"):
            raise RuntimeError("FFLogs: " + str(j["errors"][0].get("message", "query failed")))
        return j

    def fights(self, code):
        q = ("query($code:String!){reportData{report(code:$code){"
             "fights{id name kill startTime endTime encounterID}}}}")
        j = self._query(q, {"code": code})
        fights = (j.get("data", {}).get("reportData", {}).get("report", {}) or {}).get("fights")
        if fights is None:
            raise RuntimeError("Report not found (public? code right?).")
        out = []
        for f in fights:
            if (f.get("encounterID") or 0) == 0:
                continue
            out.append(f)
        return out

    def casts(self, code, fight):
        q = ("query($code:String!,$fid:Int!,$start:Float!,$end:Float!){reportData{report(code:$code){"
             "events(fightIDs:[$fid],dataType:Casts,hostilityType:Enemies,"
             "startTime:$start,endTime:$end,limit:10000){data nextPageTimestamp}}}}")
        start0 = fight["startTime"]
        castbar, casts = set(), []
        start = start0
        for _ in range(6):
            j = self._query(q, {"code": code, "fid": fight["id"], "start": start, "end": fight["endTime"]})
            ev = (j.get("data", {}).get("reportData", {}).get("report", {}) or {}).get("events") or {}
            data = ev.get("data") or []
            for e in data:
                raw = e.get("abilityGameID") or 0
                if raw <= 0 or raw > 0xFFFFFFFF:
                    continue
                t = ((e.get("timestamp") or 0) - start0) / 1000.0
                typ = e.get("type")
                if typ == "begincast":
                    castbar.add(raw)
                elif typ == "cast":
                    casts.append({"AbilityId": raw, "Time": t, "HasCastBar": False})
            nxt = ev.get("nextPageTimestamp")
            if not nxt:
                break
            start = nxt
        for c in casts:
            if c["AbilityId"] in castbar:
                c["HasCastBar"] = True
        return casts


# ---- analysis (pure; unit-testable without network) ------------------------

def median(xs):
    return statistics.median(xs) if xs else None


def anchors_from_profile(profile):
    """Anchored mechanics: (ability_id, time, label) from SyncPoints."""
    out = []
    for sp in profile.get("SyncPoints") or []:
        aid = int(sp.get("Ability", 0)) & 0xFFFFFFFF
        if aid:
            out.append({"ability": aid, "time": float(sp.get("Time", 0)),
                        "label": sp.get("Label", "")})
    return out


def nearest_time(casts, ability, near, window):
    """Resolve time of `ability` in one log nearest `near` within `window`s, else None."""
    best, best_gap = None, window
    for c in casts:
        if c["AbilityId"] != ability:
            continue
        gap = abs(c["Time"] - near)
        if gap <= best_gap:
            best_gap, best = gap, c["Time"]
    return best


def verify_anchors(profile, logs, tol):
    """Per anchor: median log time, drift vs profile, presence count."""
    rows = []
    for a in anchors_from_profile(profile):
        times = []
        for lg in logs:
            t = nearest_time(lg["casts"], a["ability"], a["time"], window=20.0)
            if t is not None:
                times.append(t)
        med = median(times)
        rows.append({
            "ability": a["ability"], "label": a["label"],
            "profile": a["time"], "median": med,
            "present": len(times), "of": len(logs),
            "drift": (med - a["time"]) if med is not None else None,
            "flag": (med is not None and abs(med - a["time"]) > tol),
        })
    return rows


def find_missing(profile, logs, min_share):
    """Cast-bar abilities present in >= min_share of logs that your sheet has no
    row for at all - a telegraphed mechanic your single import missed. An ability
    whose time lines up with an existing row (even an unanchored one) is NOT
    missing, just unanchored, so it isn't flagged."""
    known = {a["ability"] for a in anchors_from_profile(profile)}
    row_times = sorted(float(r.get("Time", 0)) for r in (profile.get("CustomRows") or []))

    def near_row(t):
        return any(abs(t - rt) < 3.0 for rt in row_times)

    # per log, the distinct cast-bar ability ids and each one's first time
    seen_count, times = {}, {}
    for lg in logs:
        firsts = {}
        for c in lg["casts"]:
            if not c["HasCastBar"]:
                continue
            aid = c["AbilityId"]
            if aid not in firsts or c["Time"] < firsts[aid]:
                firsts[aid] = c["Time"]
        for aid, t in firsts.items():
            seen_count[aid] = seen_count.get(aid, 0) + 1
            times.setdefault(aid, []).append(t)
    out = []
    need = math.ceil(min_share * len(logs))
    for aid, cnt in seen_count.items():
        if aid in known or cnt < need:
            continue
        mt = median(times[aid])
        if mt is not None and near_row(mt):
            continue  # already a row, just not anchored
        out.append({"ability": aid, "seen": cnt, "of": len(logs), "median_time": mt})
    return sorted(out, key=lambda x: x["median_time"] or 0)


def downtime_consensus(logs, min_gap=20.0):
    """Median untargetable windows across logs, from silence in enemy casts
    (mirrors the plugin's gap derivation: start=prev+3, dur=gap-5)."""
    per_log = []
    for lg in logs:
        ts = sorted(c["Time"] for c in lg["casts"])
        wins = []
        for i in range(1, len(ts)):
            gap = ts[i] - ts[i - 1]
            if gap < min_gap:
                continue
            wins.append((round(ts[i - 1] + 3), round(gap - 5)))
        per_log.append(wins)
    # cluster windows across logs by start proximity (<15s)
    flat = sorted((s, d, li) for li, ws in enumerate(per_log) for (s, d) in ws)
    clusters = []
    for s, d, li in flat:
        if clusters and s - clusters[-1]["ref"] < 15:
            clusters[-1]["starts"].append(s)
            clusters[-1]["durs"].append(d)
            clusters[-1]["logs"].add(li)
        else:
            clusters.append({"ref": s, "starts": [s], "durs": [d], "logs": {li}})
    return [{"start": round(median(c["starts"])), "dur": round(median(c["durs"])),
             "seen": len(c["logs"]), "of": len(logs)} for c in clusters]


# ---- report + correction ---------------------------------------------------

def report(profile, logs, tol):
    n = len(logs)
    L = [f"Verifying against {n} log(s)  (tolerance {tol:g}s)", "=" * 60, ""]

    anchors = verify_anchors(profile, logs, tol)
    L.append("ANCHOR TIMING  (profile time -> median across logs)")
    if not anchors:
        L.append("  (profile has no anchored mechanics)")
    for a in anchors:
        med = f"{a['median']:.1f}" if a["median"] is not None else "  -  "
        drift = f"{a['drift']:+.1f}s" if a["drift"] is not None else "  n/a "
        mark = "  <-- DRIFT" if a["flag"] else ("  (only %d/%d logs)" % (a["present"], a["of"])
                                                if a["present"] < a["of"] else "")
        L.append(f"  0x{a['ability']:04X} {a['label'][:34]:34} "
                 f"{a['profile']:6.1f} -> {med:>6}  ({a['present']}/{a['of']}) {drift}{mark}")
    L.append("")

    missing = find_missing(profile, logs, min_share=0.5)
    L.append("POSSIBLY MISSING  (cast-bar mechanics in >=half the logs, not in your sheet)")
    if not missing:
        L.append("  none - your import anchored every telegraphed mechanic the logs agree on.")
    for m in missing:
        L.append(f"  0x{m['ability']:04X}  ~{m['median_time']:.0f}s   seen {m['seen']}/{m['of']} logs")
    L.append("")

    prof_dt = profile.get("CustomDowntimes") or []
    cons_dt = downtime_consensus(logs)
    L.append("UNTARGETABLE WINDOWS  (log consensus vs your profile)")
    if not cons_dt:
        L.append("  logs show no downtime gaps.")
    for w in cons_dt:
        match = next((d for d in prof_dt if abs(float(d.get("Start", 0)) - w["start"]) < 15), None)
        yours = f"yours {float(match['Start']):.0f}/{float(match['Duration']):.0f}s" if match else "NOT in your profile"
        L.append(f"  ~{w['start']:.0f}s  dur ~{w['dur']:.0f}s   ({w['seen']}/{w['of']} logs)   {yours}")
    L.append("")

    drift_n = sum(1 for a in anchors if a["flag"])
    L.append("SUMMARY")
    L.append(f"  {drift_n} anchor(s) drift > {tol:g}s, {len(missing)} possibly-missing mechanic(s), "
             f"{sum(1 for w in cons_dt if not any(abs(float(d.get('Start',0))-w['start'])<15 for d in prof_dt))} "
             f"downtime(s) the logs show that you don't.")
    L.append("  Fixable timings can be nudged to the median with --write-corrected;")
    L.append("  missing mechanics you add in-game and re-Auto-plan (the tool never fabricates a plan).")
    return "\n".join(L), anchors


def write_corrected(profile, anchors, tol, out_path):
    """Nudge each drifted anchor INSTANCE toward the log median: its SyncPoint, its
    CustomRow, and that mechanic's plan lines, all together (moving the row without
    its lines would orphan the mits - the generator matches lines to rows within
    2s). Only anchors seen in >=half the logs and drifting more than the report's
    tolerance are touched. Never adds rows; never fabricates a plan.

    Keyed by (ability, time), NOT ability alone, so an id reused across a fight
    (e.g. one Towers cast id on eight rows) has each instance corrected on its own.
    Rows are matched by the anchor's mechanic label, so a distinct mechanic
    resolving within 2s of the anchor is never dragged onto its time."""
    # one correction target per drifted anchor instance
    targets = {}
    for a in anchors:
        if a["median"] is None or a["present"] * 2 < a["of"] or abs(a["drift"]) <= tol:
            continue
        targets[(a["ability"], round(a["profile"], 2))] = a

    def shift_lines(lst, old, new, mechs):
        for l in lst or []:
            if abs(float(l.get("Time", 0)) - old) < 2.0 \
                    and (l.get("Mechanic", "") or "").strip().casefold() in mechs:
                l["Time"] = new

    moved = 0
    for sp in profile.get("SyncPoints") or []:
        aid = int(sp.get("Ability", 0)) & 0xFFFFFFFF
        a = targets.get((aid, round(float(sp.get("Time", 0)), 2)))
        if not a:
            continue
        old, new = float(sp["Time"]), round(a["median"], 1)
        label = (a["label"] or "").strip().casefold()
        sp["Time"] = new
        # move only THIS anchor's own mechanic row(s): matched by the anchor's
        # label when it has one, so a different mechanic within 2s stays put.
        mechs = set()
        for cr in profile.get("CustomRows") or []:
            if abs(float(cr.get("Time", 0)) - old) >= 2.0:
                continue
            m = (cr.get("Mechanic", "") or "").strip().casefold()
            if label and m != label:
                continue
            mechs.add(m)
            cr["Time"] = new
        if mechs:
            shift_lines(profile.get("Lines"), old, new, mechs)
            for lst in (profile.get("SavedSlots") or {}).values():
                shift_lines(lst, old, new, mechs)
        moved += 1

    with open(out_path, "w", encoding="utf-8") as fh:
        json.dump(profile, fh, indent=2)
    return moved


# ---- fetch orchestration ---------------------------------------------------

def parse_code(link):
    import re
    m = re.search(r"reports/((?:a:)?[A-Za-z0-9]{8,})", link or "")
    if m:
        return m.group(1)
    if re.fullmatch(r"(?:a:)?[A-Za-z0-9]{8,}", link or ""):
        return link
    return None


def fetch_logs(client, reports):
    logs = []
    for link in reports:
        code = parse_code(link)
        if not code:
            print(f"! skipping unparseable report: {link}", file=sys.stderr)
            continue
        fights = client.fights(code)
        kills = [f for f in fights if f.get("kill")]
        fight = (kills or fights)[0]
        casts = client.casts(code, fight)
        logs.append({"report": code, "fight": fight["name"], "casts": casts})
        print(f"  fetched {code}: {fight['name']} ({'kill' if fight.get('kill') else 'wipe'}), "
              f"{len(casts)} casts", file=sys.stderr)
    return logs


def load_offline(paths):
    logs = []
    for p in paths:
        with open(p, "r", encoding="utf-8-sig") as fh:
            casts = json.load(fh)
        logs.append({"report": os.path.basename(p), "fight": "(offline)", "casts": casts})
    return logs


def main():
    ap = argparse.ArgumentParser(description="Cross-check one FightProfile against several FFLogs kills.")
    ap.add_argument("profile", help="the single-import FightProfile JSON (same one the generator takes)")
    ap.add_argument("--reports", nargs="+", help="FFLogs report links/codes to verify against")
    ap.add_argument("--casts-json", nargs="+", help="offline: pre-fetched per-log cast lists (skip network)")
    ap.add_argument("--creds", help="FrenMits.json to read FflogsClientId/Secret from")
    ap.add_argument("--id", help="FFLogs client id (overrides --creds)")
    ap.add_argument("--secret", help="FFLogs client secret")
    ap.add_argument("--tolerance", type=float, default=2.0, help="drift flag threshold, seconds (default 2)")
    ap.add_argument("--write-corrected", help="write a timing-corrected copy of the profile here")
    args = ap.parse_args()

    with open(args.profile, "r", encoding="utf-8-sig") as fh:
        data = json.load(fh)
    profile = data
    if isinstance(data, dict) and "Fights" in data:
        sys.exit("Pass a single FightProfile (export it), not the whole FrenMits.json, to the verifier.")

    if args.casts_json:
        logs = load_offline(args.casts_json)
    elif args.reports:
        cid, secret = args.id, args.secret
        if (not cid or not secret) and args.creds:
            with open(args.creds, "r", encoding="utf-8-sig") as fh:
                cfg = json.load(fh)
            cid = cid or cfg.get("FflogsClientId")
            secret = secret or cfg.get("FflogsClientSecret")
            # Newer configs store the secret DPAPI-encrypted (FflogsClientSecretEnc),
            # which this tool can't read; fall through to env / --secret.
            if not secret and cfg.get("FflogsClientSecretEnc"):
                print("note: --creds config stores the secret encrypted now; pass --secret or set FFLOGS_CLIENT_SECRET.", file=sys.stderr)
        cid = cid or os.environ.get("FFLOGS_CLIENT_ID")
        secret = secret or os.environ.get("FFLOGS_CLIENT_SECRET")
        if not cid or not secret:
            sys.exit("Need FFLogs creds: --id/--secret, FFLOGS_CLIENT_ID/FFLOGS_CLIENT_SECRET env, or --creds FrenMits.json.")
        print("Fetching logs...", file=sys.stderr)
        logs = fetch_logs(FFLogs(cid, secret), args.reports)
    else:
        sys.exit("Give logs to verify against: --reports LINK... or --casts-json FILE...")

    if not logs:
        sys.exit("No usable logs.")

    text, anchors = report(profile, logs, args.tolerance)
    print(text)

    if args.write_corrected:
        moved = write_corrected(profile, anchors, args.tolerance, args.write_corrected)
        print(f"\nWrote {args.write_corrected}: nudged {moved} anchor time(s) to the log median.")


if __name__ == "__main__":
    main()
