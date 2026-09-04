#!/usr/bin/env python
# DragonScreen - tuning_db.py
# =============================================================================================
# THE CONTROL-TUNING DATABASE. Reads the WHOLE flight corpus (every Crew-2_*.csv the FlightRecorder
# has written) and builds a per-phase statistical profile of every control signal — angular rates
# (pitch/roll/yaw deg/s), actuation (act_*), pointing error, throttle + throttle-rate, RCS translation,
# control-torque authority, AoA, q, g. So over flights we learn "what control the vehicle actually needs
# and uses per phase / manoeuvre", and can then tune each gain/limit per situation from real numbers.
#
# ⛔ THIS IS ANALYSIS OF RECORDED DATA — NOT a physics/orbital simulation (those are banned). It only
# reads what actually flew. Re-run it after every flight; it re-reads the full corpus and rebuilds the DB,
# so it accumulates automatically as new recordings land. Output: docs/tuning/TUNING_DB.json (machine) +
# docs/tuning/TUNING_DB.md (human-readable per-phase tables) + a coverage note (how much data per phase).
#
#   python plugin/tools/tuning_db.py                       # the REPO corpus (docs/flights) -> docs/tuning
#   python plugin/tools/tuning_db.py <corpus_dir> <out_dir>
#
# CORPUS LOCATION (S76, 2026-09-04): the corpus is `docs/flights/` IN THE REPO (C7 - the repo is the only
# source of truth), not the KSP capture dir. The KSP capture dir and the quarantine archive are kept as
# named constants because that is where a NEW recording lands, but they are used only when passed
# explicitly as <corpus_dir>. Whatever is worth analysing belongs in `docs/flights/`.
#
# OVERWRITE GUARD (S76): `docs/tuning/TUNING_DB.{json,md}` in this repo is the RECOVERED distillate of a
# 55-flight corpus (BUILD_PLAN §B16.8) that this repo's 13 recordings CANNOT reproduce. Re-running over it
# would silently replace 55 flights of statistics with 13. So the script REFUSES to overwrite an existing
# TUNING_DB unless --force is passed; write somewhere else instead. (C1.16: research is never deleted.)
# =============================================================================================
import csv, os, sys, json, math, statistics, glob

REPO_ROOT    = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REPO_FLIGHTS = os.path.join(REPO_ROOT, "docs", "flights")
CAPTURE      = r"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\DragonScreen_capture"
ARCHIVE      = r"C:\Users\User\Desktop\quarantine\dragonscreen_flightdata"

# The control signals we profile. (label -> csv column). Absent columns (older CSVs) are skipped safely.
SIGNALS = {
    "rate_pitch_dps": "rate_pitch_dps", "rate_roll_dps": "rate_roll_dps", "rate_yaw_dps": "rate_yaw_dps",
    "act_pitch": "act_pitch", "act_yaw": "act_yaw", "act_roll": "act_roll",
    "point_err_deg": "att_point_deg", "att_rate_meas": "att_rate_meas", "att_rate_cmd": "att_rate_cmd",
    "throttle": "throttle", "trans_x": "trans_x", "trans_y": "trans_y", "trans_z": "trans_z",
    "ctrl_tq_pitch": "ctrl_tq_pitch", "ctrl_tq_yaw": "ctrl_tq_yaw", "ctrl_tq_roll": "ctrl_tq_roll",
    "moi_pitch": "moi_pitch", "moi_roll": "moi_roll", "moi_yaw": "moi_yaw", "rcs_thrust_n": "rcs_thrust_n",
    "ap_km": "ap_km", "pe_km": "pe_km", "inc_deg": "inc_deg", "raan_deg": "raan_deg",
    "aoa_deg": "att_err_deg", "q_pa": "q_pa", "accel_g": "accel_g", "mach": "mach",
}

# DERIVED per-row AUTHORITY metrics (the point of the DB — do we always have enough control authority?):
#   act_sat        = max|act_pitch,yaw,roll|  → 1.0 means the loop SATURATED = out of authority
#   angacc_*_auth  = ctrl_tq_axis / moi_axis  (rad/s²) → the angular acceleration the vehicle can COMMAND
def derived(r):
    d = {}
    ap, ay, ar = fnum(r.get("act_pitch")), fnum(r.get("act_yaw")), fnum(r.get("act_roll"))
    sat = [abs(x) for x in (ap, ay, ar) if x is not None]
    if sat: d["act_sat"] = max(sat)
    for axis in ("pitch", "roll", "yaw"):
        tq, moi = fnum(r.get("ctrl_tq_" + axis)), fnum(r.get("moi_" + axis))
        if tq is not None and moi is not None and moi > 1e-6:
            d["angacc_%s_auth" % axis] = tq / moi
    return d
DERIVED = ["act_sat", "angacc_pitch_auth", "angacc_roll_auth", "angacc_yaw_auth"]

def fnum(s):
    try: return float(s)
    except: return None

# ⭐ S77 (same idiom as assess_flight.py's is_warp()): exclude ONLY on-rails HIGH-warp rows — those are
# the ones the recorder blanks (physics warp ≤4x keeps control LIVE and must stay pooled). The test is
# "warped AND the recorder blanked the control columns", not "warp_rate>1" alone.
def is_warp(r):
    w = fnum(r.get("warp_rate"))
    if w is None or w <= 1.0: return False                       # realtime
    return r.get("ctrl_tq_pitch", "") == "" and r.get("act_pitch", "") == ""   # on-rails = control blanked

def segment_label(r):
    """The phase/manoeuvre this row belongs to — the most specific active phase column."""
    ap = r.get("ascent_phase", "")
    if ap: return "ASCENT/" + ap
    am = r.get("abort_mode", "")
    if am and am != "None": return "ABORT/" + am
    for col, pre in (("boost_phase", "BOOST"), ("entry_phase", "ENTRY"), ("deorbit_phase", "DEORBIT"),
                     ("dep_phase", "DEPART"), ("dock_phase", "DOCK"), ("rv_phase", "RV"),
                     ("chute_phase", "CHUTE")):
        v = r.get(col, "")
        if v and v != "Idle": return pre + "/" + v
    return "MISSION/" + (r.get("mission_phase", "") or "Unknown")

def stats(vals):
    v = [x for x in vals if x is not None and not math.isnan(x)]
    if not v: return None
    av = [abs(x) for x in v]; av.sort()
    def pct(a, p): return a[min(len(a) - 1, int(len(a) * p))]
    return {"n": len(v), "mean": round(statistics.mean(v), 5), "absmean": round(statistics.mean(av), 5),
            "p50": round(pct(av, 0.50), 5), "p95": round(pct(av, 0.95), 5), "p99": round(pct(av, 0.99), 5),
            "max": round(max(av), 5), "min": round(min(v), 5)}

def main():
    argv = [a for a in sys.argv[1:] if a != "--force"]
    force = "--force" in sys.argv[1:]
    cap = argv[0] if len(argv) > 0 else REPO_FLIGHTS
    out = os.path.abspath(argv[1] if len(argv) > 1 else os.path.join(REPO_ROOT, "docs", "tuning"))
    os.makedirs(out, exist_ok=True)
    existing = [f for f in ("TUNING_DB.json", "TUNING_DB.md") if os.path.exists(os.path.join(out, f))]
    if existing and not force:
        print("REFUSING to overwrite %s in %s." % (" + ".join(existing), out))
        print("  That file is the RECOVERED 55-flight distillate (BUILD_PLAN.md B16.8); the corpus that")
        print("  produced it is NOT in this repo, so a re-run REPLACES it with a smaller one and the")
        print("  original cannot be rebuilt. Pass an <out_dir> to write elsewhere, or --force if you")
        print("  genuinely intend to replace it.")
        return 2

    # S76: read ONE named corpus directory (default: the repo's docs/flights). It used to also sweep the
    # quarantine archive unconditionally, which mixed uncommitted, unreviewed files into every run - an
    # analysis that cannot be reproduced from the repo (C7). Point <corpus_dir> at CAPTURE or ARCHIVE
    # explicitly if that is what you want.
    # `*_geometry_dump_*.csv` matches the glob but is the GeometryDump instrument's own schema, not a
    # recorder tick stream - excluded, or every one of its rows pools in as a phantom MISSION/Unknown row.
    by_name = {}
    for p in glob.glob(os.path.join(cap, "Crew-2*.csv")):
        if "geometry_dump" in os.path.basename(p): continue
        by_name[os.path.basename(p)] = p
    files = sorted(by_name.values(), key=lambda p: os.path.basename(p))
    if not files:
        print("no recorder Crew-2*.csv in " + cap); return

    # ⛔ TRUST THE DATA ONLY WHEN IT IS CORRECT. A flight that flew a BROKEN trajectory (retrograde plane, a
    # loss-of-control, an aborted mess) records CORRECT numbers for a WRONG flight — pooling it poisons the
    # per-phase authority stats. So exclude known-contaminated flights (listed in docs/tuning/exclude.txt, one
    # filename-substring per line, '#' comments) and report which flights are IN vs OUT. Curate to a clean corpus
    # before tuning off it. (This does not delete anything — it just scopes the aggregate.)
    # exclude.txt lives beside the output; fall back to the repo's canonical copy when writing elsewhere.
    excl_path = os.path.join(out, "exclude.txt")
    if not os.path.exists(excl_path):
        excl_path = os.path.join(REPO_ROOT, "docs", "tuning", "exclude.txt")
    excludes = []
    if os.path.exists(excl_path):
        for ln in open(excl_path, encoding="utf-8"):
            ln = ln.split("#", 1)[0].strip()
            if ln: excludes.append(ln)
    included, excluded = [], []
    for f in files:
        bn = os.path.basename(f)
        (excluded if any(x in bn for x in excludes) else included).append(f)
    files = included

    # pooled raw values per (segment, signal) across the WHOLE corpus + per-flight coverage
    pool = {}          # seg -> signal -> [values]
    seg_flights = {}   # seg -> set(flight)
    throt_rate = {}    # seg -> [d(throttle)/dt]  (a derived rate: how fast we move the throttle)
    # per-flight QUALITY per segment — so a contaminated flight is VISIBLE, not silently pooled. ROBUST metrics,
    # NOT max: a single-frame actuation spike to 1.0 at liftoff/staging is normal, and a transient pointing swing
    # when UPFG grabs a new attitude is expected — neither means broken control. What means broken is SUSTAINED:
    #   point_err_p95  — the pointing error 95% of the phase stays under (a real loss of plane/control shows here)
    #   act_sat_duty   — FRACTION of the phase with |actuation| > 0.95 (sustained saturation = out of authority)
    fq = {}            # flight -> seg -> {rows, act_sat_hi, pe:[...]}
    nwarp = 0          # S77: on-rails warp rows excluded before pooling (control columns blanked there)
    for path in files:
        fn = os.path.basename(path)
        try:
            rows = list(csv.DictReader(open(path)))
        except Exception as e:
            print("skip " + fn + ": " + str(e)); continue
        prev_t = prev_thr = None
        for r in rows:
            if is_warp(r):
                nwarp += 1
                continue   # S77: on-rails, control columns blanked — not a real control-authority sample
            seg = segment_label(r)
            pool.setdefault(seg, {})
            seg_flights.setdefault(seg, set()).add(fn)
            q = fq.setdefault(fn, {}).setdefault(seg, {"rows": 0, "act_sat_hi": 0, "pe": []})
            q["rows"] += 1
            for label, col in SIGNALS.items():
                if col in r:
                    pool[seg].setdefault(label, []).append(fnum(r[col]))
            d = derived(r)
            for label, val in d.items():
                pool[seg].setdefault(label, []).append(val)
            if d.get("act_sat") is not None and d["act_sat"] > 0.95: q["act_sat_hi"] += 1
            pe = fnum(r.get("att_point_deg"))
            if pe is not None and not math.isnan(pe): q["pe"].append(abs(pe))
            # derived: throttle slew rate (per second)
            t = fnum(r.get("met_s")); thr = fnum(r.get("throttle"))
            if t is not None and thr is not None and prev_t is not None and prev_thr is not None and t > prev_t:
                throt_rate.setdefault(seg, []).append((thr - prev_thr) / (t - prev_t))
            if t is not None: prev_t, prev_thr = t, (thr if thr is not None else prev_thr)

    def quality(q):
        pe = sorted(q["pe"])
        def pct(a, p): return round(a[min(len(a) - 1, int(len(a) * p))], 3) if a else None
        return {"rows": q["rows"], "pe_p50": pct(pe, 0.50), "pe_p95": pct(pe, 0.95),
                "pe_max": round(max(pe), 2) if pe else None,
                "act_sat_duty": round(q["act_sat_hi"] / q["rows"], 3) if q["rows"] else None}
    db = {"corpus": [os.path.basename(f) for f in files],
          "excluded": [os.path.basename(f) for f in excluded],
          "warp_rows_excluded": nwarp,
          "flight_quality": {fn: {s: quality(q) for s, q in segs.items()} for fn, segs in fq.items()},
          "segments": {}}
    for seg in sorted(pool):
        sig = {}
        for label in list(SIGNALS) + DERIVED + ["throttle_rate_per_s"]:
            vals = throt_rate.get(seg, []) if label == "throttle_rate_per_s" else pool[seg].get(label, [])
            s = stats(vals)
            if s: sig[label] = s
        db["segments"][seg] = {"flights": sorted(seg_flights.get(seg, [])),
                               "flight_count": len(seg_flights.get(seg, [])), "signals": sig}

    with open(os.path.join(out, "TUNING_DB.json"), "w", encoding="utf-8") as f:
        json.dump(db, f, indent=1)

    # human-readable summary
    lines = ["# DragonScreen control-authority / tuning database", "",
             "Auto-generated by `tools/tuning_db.py` from the flight corpus (%d flights). Re-run after each flight"
             " — it re-reads the whole corpus and rebuilds, so it grows automatically. GOAL: verify PERFECT"
             " CONTROL AUTHORITY per phase and tune where it is marginal. The tells:" % len(files), "",
             "- **act_sat** (max|actuation|): approaching **1.0 = the attitude loop SATURATED = out of authority**"
             " → the vehicle needs more gimbal/RCS, or the commanded rate is too aggressive there.",
             "- **angacc_\\*_auth** = available torque / MOI (rad/s²): the angular acceleration the vehicle CAN"
             " command per axis — the raw authority. Compare against the rates actually demanded.",
             "- **point_err_deg**: the OUTCOME — if authority is enough, this stays tiny; if it grows, authority"
             " (or gains) fell short.",
             "- **rcs_thrust_n / trans_\\***: how hard the Dracos work (capsule authority); **ctrl_tq_\\***: torque"
             " available; **rate_\\*_dps**: the rates flown.",
             "", "Values are |abs| unless noted (min is signed).", "",
             "**Corpus (included):** " + (", ".join(db["corpus"]) or "none"),
             "**Excluded (contaminated, see exclude.txt):** " + (", ".join(db["excluded"]) or "none"),
             "**Warp rows excluded (on-rails, control columns blanked — not pooled into any statistic):** "
             + str(db["warp_rows_excluded"]), "",
             "### Flight quality — spot contamination BEFORE trusting the pooled stats",
             "A flight whose pointing error or actuation saturation is large in a phase flew that phase BROKEN "
             "(retrograde plane, loss of control). Add it to `exclude.txt` and re-run.", "",
             "Robust metrics (NOT max — a one-frame spike is not broken control): **pe_p95** = pointing error the"
             " phase stays under 95% of the time; **sat_duty** = fraction of the phase with actuation saturated."
             " BROKEN = pe_p95 > 5° (sustained mis-point) OR sat_duty > 0.25 (sustained out-of-authority).", "",
             "| flight | phase | rows | pe_p50 | pe_p95 | pe_max | sat_duty | verdict |",
             "|---|---|---|---|---|---|---|---|"]
    for fn in sorted(db["flight_quality"]):
        for seg in sorted(db["flight_quality"][fn]):
            q = db["flight_quality"][fn][seg]
            if q["rows"] < 5 or not seg.startswith(("ASCENT", "RV", "DEORBIT", "ENTRY", "DOCK", "DEPART", "ABORT")):
                continue
            broke = (q["pe_p95"] or 0) > 5 or (q["act_sat_duty"] or 0) > 0.25
            lines.append("| %s | %s | %d | %s | %s | %s | %s | %s |" % (
                fn, seg, q["rows"], q["pe_p50"], q["pe_p95"], q["pe_max"], q["act_sat_duty"],
                "BROKEN" if broke else "ok"))
    lines.append("")
    key_sig = ["act_sat", "angacc_pitch_auth", "angacc_roll_auth", "angacc_yaw_auth", "point_err_deg",
               "rate_pitch_dps", "rate_roll_dps", "rate_yaw_dps", "act_pitch", "act_yaw", "act_roll",
               "ctrl_tq_pitch", "ctrl_tq_yaw", "ctrl_tq_roll", "rcs_thrust_n", "trans_z",
               "throttle", "throttle_rate_per_s", "aoa_deg", "q_pa", "accel_g", "ap_km", "pe_km", "inc_deg"]
    for seg in sorted(db["segments"]):
        d = db["segments"][seg]
        lines.append("## %s   _(%d flight(s), coverage below)_" % (seg, d["flight_count"]))
        lines.append("| signal | n | abs-mean | p50 | p95 | p99 | max | min |")
        lines.append("|---|---|---|---|---|---|---|---|")
        for label in key_sig:
            s = d["signals"].get(label)
            if not s: continue
            lines.append("| %s | %d | %g | %g | %g | %g | %g | %g |" %
                         (label, s["n"], s["absmean"], s["p50"], s["p95"], s["p99"], s["max"], s["min"]))
        lines.append("")
    with open(os.path.join(out, "TUNING_DB.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

    print("tuning DB built from %d included flight(s) (%d excluded) -> %s" % (len(files), len(excluded), out))
    print("  %d on-rails warp row(s) excluded before pooling (control columns blanked there)" % nwarp)
    print("segments: " + ", ".join("%s(%d)" % (s, db["segments"][s]["flight_count"]) for s in sorted(db["segments"])))

if __name__ == "__main__":
    sys.exit(main() or 0)
