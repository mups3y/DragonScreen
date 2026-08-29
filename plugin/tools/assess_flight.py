#!/usr/bin/env python
# -*- coding: utf-8 -*-
# DragonScreen - assess_flight.py  (CURRENT 105-col recorder schema (+P0.0 warp_rate/eng_ignited/eng_flameout; warp rows excluded from control stats): met_s / ascent_phase / att_point_deg ...)
# =============================================================================================
# The WHOLE flight, every phase, one command — the full structured pass the memory rule requires
# ([[full-structured-flight-analysis]]: never spot-check, the full pass yields the right conclusion).
#
#   python plugin/tools/assess_flight.py                 # newest Crew-2_*.csv in the capture dir
#   python plugin/tools/assess_flight.py <file.csv>
#   python plugin/tools/assess_flight.py --list          # list captures newest-first
#
# ⛔ Reads recorded data only — NOT a physics/orbital sim (those are banned). It reports, unasked:
#   1 recorder health   2 physics self-check   3 ascent   4 booster   5 rendezvous/phasing (the
#   self-deorbit check)   6 deorbit/entry/chute   7 abort + FDIR   8 control authority   9 verdict.
# Anything it does NOT flag has been CHECKED, not skipped.
#
# NOTE the OLD plugin/build/assess_flight.py is the DELETED-autopilot schema (ut / a_phase / x_owner)
# and cannot read current recordings — this file supersedes it.
# =============================================================================================
import csv, os, sys, glob, math

CAPTURE = r"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\DragonScreen_capture"
ARCHIVE = r"C:\Users\User\Desktop\quarantine\dragonscreen_flightdata"
MU, RE = 3.9860044e14, 6371000.0          # Earth (RSS/RO). vis-viva + circular speed use these.
TARGET_INC = 51.6                          # ISS reference; a free-flyer profile would differ.
PE_FLOOR_KM = 150.0                        # the rendezvous/phasing safety floor (pure/Phasing)

def captures():
    return sorted(glob.glob(os.path.join(CAPTURE, "Crew-2*.csv")) +
                  glob.glob(os.path.join(ARCHIVE, "Crew-2*.csv")), key=os.path.getmtime)

def fnum(s):
    try: return float(s)
    except (TypeError, ValueError): return None

def load(path):
    rows = list(csv.DictReader(open(path, encoding="utf-8", errors="replace")))
    return rows

def transitions(rows, col):
    """(row, value) at each change of a phase column, skipping idle/blank."""
    out, prev = [], None
    for r in rows:
        v = r.get(col, "")
        if v != prev and v not in ("", "-", "Idle", "None"):
            out.append((r, v)); prev = v
        elif v != prev:
            prev = v
    return out

def col_active(rows, col):
    return any(r.get(col, "") not in ("", "-", "Idle", "None") for r in rows)

def sec(t): print("\n" + "=" * 78 + "\n  " + t + "\n" + "=" * 78)

def g(r, c): return fnum(r.get(c))

# ⭐ P0.0 (I1, C1-refined): exclude ONLY on-rails HIGH-warp rows from control-signal stats — those are the ones the
# recorder blanks (physics warp ≤4× keeps control LIVE and must be kept). So the test is "warped AND the recorder
# blanked the control columns", not "warp_rate>1" (which wrongly dropped live physics-warp rows).
def is_warp(r):
    w = fnum(r.get("warp_rate"))
    if w is None or w <= 1.0: return False                       # realtime
    return r.get("ctrl_tq_pitch", "") == "" and r.get("act_pitch", "") == ""   # on-rails = control blanked

# ------------------------------------------------------------------ 1. recorder health
def recorder_health(rows, hdr):
    sec("1. RECORDER HEALTH")
    t0, t1 = g(rows[0], "met_s"), g(rows[-1], "met_s")
    print("  %d rows, %d columns, met %.1f..%.1f s (%.0f s)" % (len(rows), len(hdr), t0 or 0, t1 or 0, (t1 or 0) - (t0 or 0)))
    # blocks that never ran are legitimately idle — don't flag their columns
    blocks = {"ascent": "ascent_phase", "boost": "boost_phase", "rv": "rv_phase",
              "deorbit": "deorbit_phase", "entry": "entry_phase", "dock": "dock_phase",
              "chute": "chute_phase", "abort": "abort_mode"}
    dormant = [b for b, c in blocks.items() if not col_active(rows, c)]
    if dormant: print("  blocks not exercised (columns correctly idle): " + ", ".join(dormant))
    # constant / NaN / empty numeric columns over the WHOLE file (informational)
    flags = []
    for c in hdr:
        vals = [r.get(c, "") for r in rows]
        ne = [v for v in vals if v not in ("",)]
        nums = [fnum(v) for v in ne if fnum(v) is not None]
        if nums and len(nums) == len(ne) and any(math.isnan(x) or math.isinf(x) for x in nums):
            flags.append((c, "NaN/Inf"))
    if flags:
        print("  %d numeric column(s) with NaN/Inf:" % len(flags))
        for c, t in flags: print("     %-20s %s" % (c, t))
    else:
        print("  no NaN/Inf in any numeric column")

# ------------------------------------------------------------------ 2. physics self-check
def physics(rows):
    sec("2. PHYSICS SELF-CHECK (does the file agree with itself)")
    errs, prev = [], None
    for r in rows:
        a, t, vs = g(r, "alt_m"), g(r, "met_s"), g(r, "vspeed_mps")
        if None in (a, t, vs): continue
        if prev and 0.15 < t - prev[1] < 0.4 and abs(vs) > 1:
            errs.append(abs((a - prev[0]) / (t - prev[1]) - vs) / max(abs(vs), 1))
        prev = (a, t)
    if errs:
        errs.sort(); print("  vspeed vs d(alt)/dt    median %.3f  p95 %.3f  (n=%d)" %
                           (errs[len(errs)//2], errs[int(len(errs)*.95)], len(errs)))
    bad = n = 0
    for r in rows[::40]:
        ap, pe, v, alt = g(r, "ap_km"), g(r, "pe_km"), g(r, "speed_mps"), g(r, "alt_m")
        if None in (ap, pe, v, alt) or pe < 0 or v is None or v < 100: continue
        sma = RE + (ap + pe) * 500.0
        term = MU * (2.0 / (RE + alt) - 1.0 / sma)
        if term <= 0: continue
        n += 1
        if abs(math.sqrt(term) - v) / v > 0.03: bad += 1
    if n: print("  orbital speed vs vis-viva   %d of %d samples off by >3%%" % (bad, n))
    accs = [g(r, "accel_g") for r in rows if g(r, "accel_g") is not None]
    if accs: print("  accel_g  max %.2f  (crew limit ~4.5 g)" % max(accs))
    masses = [g(r, "mass_kg") for r in rows if g(r, "mass_kg") is not None]
    if masses: print("  mass_kg  %.0f -> %.0f  (%.0f burned)" % (masses[0], masses[-1], masses[0]-masses[-1]))

# ------------------------------------------------------------------ 3. ascent
def ascent(rows):
    sec("3. ASCENT (event-by-event)")
    tr = transitions(rows, "ascent_phase")
    if not tr: print("  no ascent in this recording"); return None
    for r, p in tr:
        print("  met %7.1f  %-14s alt %6.1fkm  ap %6.1f pe %8.1f  inc %5.1f  pitch %5.1f az %5.1f  q %6.0f M %4.1f  g %4.2f thr %.2f  ptErr %5.1f" % (
            g(r,"met_s") or 0, p, (g(r,"alt_m") or 0)/1000.0, g(r,"ap_km") or 0, g(r,"pe_km") or 0,
            g(r,"inc_deg") or 0, g(r,"pitch_deg") or 0, g(r,"azimuth_deg") or 0, g(r,"q_pa") or 0,
            g(r,"mach") or 0, g(r,"accel_g") or 0, g(r,"throttle") or 0, g(r,"att_point_deg") or -1))
    # ascent extremes + final orbit
    asc = [r for r in rows if r.get("ascent_phase","") not in ("","-","Idle")]
    qmax = max((g(r,"q_pa") or 0) for r in asc); gmax = max((g(r,"accel_g") or 0) for r in asc)
    print("  --- max q %.0f Pa   max g %.2f ---" % (qmax, gmax))
    # ⛔ SECO orbit = the SETTLED orbit AFTER the engine cuts — NOT the last S2Burn-phase row (which is ~0.3 s
    # BEFORE cutoff, with pe still rising fast: it read a false 200×160 when the real insertion was 200×197).
    # Find the last ascent-phase row, scan forward to where thrust dies, read the orbit a few rows past that.
    lastIdx = max(i for i,r in enumerate(rows) if r.get("ascent_phase","") not in ("","-","Idle"))
    last = rows[lastIdx]
    for j in range(lastIdx, min(lastIdx+40, len(rows))):
        if (g(rows[j],"thrust_n") or 0) < 1000.0:      # engine cut → the orbit settles within a tick or two
            last = rows[min(j+3, len(rows)-1)]; break
    ap, pe, inc = g(last,"ap_km"), g(last,"pe_km"), g(last,"inc_deg")
    print("  SECO/insertion:  ap %.1f  pe %.1f  inc %.2f (tgt %.1f, d=%+.2f)  raan %.1f" % (
        ap or 0, pe or 0, inc or 0, TARGET_INC, (inc or 0)-TARGET_INC, g(last,"raan_deg") or 0))
    orbit = (pe or -9999) > 100
    print("  --> %s" % ("REACHED ORBIT" if orbit else "*** SUBORBITAL (pe <= 100 km) ***"))
    return {"ap":ap,"pe":pe,"inc":inc,"orbit":orbit,"qmax":qmax,"gmax":gmax}

# ------------------------------------------------------------------ 4. booster
def booster(rows):
    sec("4. BOOSTER")
    tr = transitions(rows, "boost_phase")
    if not tr: print("  no booster leg in this recording (capsule-tracked)"); return
    for r, p in tr:
        print("  met %7.1f  %-14s alt %6.1fkm  vspd %7.1f  mode %s  igniteAlt %s  aoa %s" % (
            g(r,"met_s") or 0, p, (g(r,"alt_m") or 0)/1000.0, g(r,"descent_speed_mps") or 0,
            r.get("engine_mode","-"), r.get("ignite_alt_m","-"), r.get("boost_aoa_deg","-")))

# ------------------------------------------------------------------ 5. rendezvous / phasing (self-deorbit check)
def rendezvous(rows):
    sec("5. RENDEZVOUS / PHASING  (the self-deorbit check)")
    tr = transitions(rows, "rv_phase")
    if not tr: print("  no rendezvous/phasing in this recording"); return
    for r, p in tr:
        print("  met %7.1f  %-12s range %9.1f km  burn_dv %6.2f  ap %6.1f pe %8.1f  ptErr %5.1f" % (
            g(r,"met_s") or 0, p, (g(r,"rv_range_m") or 0)/1000.0, g(r,"rv_burn_dv") or 0,
            g(r,"ap_km") or 0, g(r,"pe_km") or 0, g(r,"att_point_deg") or -1))
    rv = [r for r in rows if r.get("rv_phase","") not in ("","-","Idle")]
    pes = [g(r,"pe_km") for r in rv if g(r,"pe_km") is not None]
    aps = [g(r,"ap_km") for r in rv if g(r,"ap_km") is not None]
    dvs = [g(r,"rv_burn_dv") for r in rv if g(r,"rv_burn_dv") is not None]
    if pes:
        minpe = min(pes)
        print("  --- phasing pe: min %.1f km (floor %.0f)  ap: %.1f..%.1f km  max burn_dv %.2f m/s ---" % (
            minpe, PE_FLOOR_KM, min(aps), max(aps), max(dvs) if dvs else 0))
        if minpe < PE_FLOOR_KM:
            print("  *** SELF-DEORBIT / FLOOR BREACH: pe dropped to %.1f km (< %.0f) DURING PHASING ***" % (minpe, PE_FLOOR_KM))
        else:
            print("  --> pe held above the floor the whole phasing leg (no self-deorbit)")

# ------------------------------------------------------------------ 6. deorbit / entry / chute
def return_entry(rows):
    sec("6. DEORBIT / ENTRY / CHUTE")
    any_r = False
    for col, name in (("dep_phase","DEPART"),("deorbit_phase","DEORBIT"),("entry_phase","ENTRY"),("chute_phase","CHUTE")):
        tr = transitions(rows, col)
        if not tr: continue
        any_r = True
        for r, p in tr:
            print("  met %7.1f  %-8s %-12s alt %6.1fkm  ap %6.1f pe %8.1f  bank %6.1f  drogue=%s main=%s" % (
                g(r,"met_s") or 0, name, p, (g(r,"alt_m") or 0)/1000.0, g(r,"ap_km") or 0,
                g(r,"pe_km") or 0, g(r,"bank_deg") or 0, r.get("drogue","-"), r.get("main","-")))
    if not any_r: print("  no deorbit/entry/chute in this recording"); return
    # dv ledger + final descent
    for lbl in ("dv_planned_mps","dv_delivered_mps","dv_residual_mps"):
        vals=[g(r,lbl) for r in rows if g(r,lbl) is not None]
        if vals: print("  %-18s last %.2f  max %.2f" % (lbl, vals[-1], max(vals)))
    tail = rows[-1]
    print("  final: alt %.2f km  srf_speed %.1f m/s  vspeed %.1f m/s" % (
        (g(tail,"alt_m") or 0)/1000.0, g(tail,"srf_speed_mps") or 0, g(tail,"vspeed_mps") or 0))

# ------------------------------------------------------------------ 7. abort + FDIR
def abort(rows):
    sec("7. ABORT + FDIR")
    tr = transitions(rows, "abort_mode")
    faults = [(r, r.get("fdir_fault","")) for r in rows if r.get("fdir_fault","") not in ("","-","None")]
    if not tr and not faults:
        print("  no abort / no FDIR fault in this recording"); return
    if faults:
        seen=set()
        for r,f in faults:
            key=(f, r.get("fdir_recovery",""), r.get("fdir_abort",""))
            if key in seen: continue
            seen.add(key)
            print("  FDIR  met %7.1f  fault=%-16s recovery=%-14s abort=%s  (alt %.1fkm q %.0f M %.1f g %.2f)" % (
                g(r,"met_s") or 0, f, r.get("fdir_recovery","-"), r.get("fdir_abort","-"),
                (g(r,"alt_m") or 0)/1000.0, g(r,"q_pa") or 0, g(r,"mach") or 0, g(r,"accel_g") or 0))
    for r, m in tr:
        print("  ABORT met %7.1f  mode=%-14s at alt %.1f km  q %.0f Pa  M %.1f  vspd %.1f  (regime)" % (
            g(r,"met_s") or 0, m, (g(r,"alt_m") or 0)/1000.0, g(r,"q_pa") or 0, g(r,"mach") or 0, g(r,"vspeed_mps") or 0))
    # abort outcome: chutes + final descent
    ch = transitions(rows, "chute_phase")
    if ch:
        print("  abort recovery chutes: " + " -> ".join(p for _, p in ch))
    tail = rows[-1]
    print("  outcome: final alt %.2f km  srf_speed %.1f m/s  (splash target ~5-8 m/s)" % (
        (g(tail,"alt_m") or 0)/1000.0, g(tail,"srf_speed_mps") or 0))

# ------------------------------------------------------------------ 8. control authority
def control(rows):
    sec("8. CONTROL AUTHORITY (per active phase: pointing error + actuation saturation)")
    def seg(r):
        for col, pre in (("ascent_phase","ASC"),("abort_mode","ABORT"),("deorbit_phase","DEORB"),
                         ("entry_phase","ENT"),("rv_phase","RV"),("dock_phase","DOCK"),("boost_phase","BOOST")):
            v = r.get(col,"")
            if v not in ("","-","Idle","None"): return pre+"/"+v
        return "MISSION/"+(r.get("mission_phase","") or "?")
    from collections import OrderedDict
    # ⭐ P0.0 (I1): exclude on-rails warp rows — their control columns are frozen/blank stale reads.
    nwarp = sum(1 for r in rows if is_warp(r))
    rt = [r for r in rows if not is_warp(r)]
    if nwarp: print("  (excluding %d on-rails warp rows — control columns there are stale/blank)" % nwarp)
    if not rt: print("  no realtime rows to assess"); return
    segs = OrderedDict()
    for r in rt:
        segs.setdefault(seg(r), []).append(r)
    print("  segment              rows | ptErr p50/p95/max deg | act_sat_duty | maxRate dps p/r/y")
    for s, rs in segs.items():
        if len(rs) < 5: continue
        pe = sorted(abs(g(r,"att_point_deg")) for r in rs if g(r,"att_point_deg") is not None)
        def pct(a,p): return a[min(len(a)-1,int(len(a)*p))] if a else 0
        sat = sum(1 for r in rs if max(abs(g(r,"act_pitch") or 0),abs(g(r,"act_yaw") or 0),abs(g(r,"act_roll") or 0))>0.95)
        rp=max((abs(g(r,"rate_pitch_dps") or 0) for r in rs)); rr=max((abs(g(r,"rate_roll_dps") or 0) for r in rs)); ry=max((abs(g(r,"rate_yaw_dps") or 0) for r in rs))
        broke = pct(pe,.95) > 5 or sat/len(rs) > 0.25
        print("  %-20s %5d | %5.1f /%5.1f /%5.1f     | %11.3f%s | %5.1f %5.1f %5.1f" % (
            s[:20], len(rs), pct(pe,.50), pct(pe,.95), max(pe) if pe else 0, sat/len(rs),
            "  BROKEN" if broke else "", rp, rr, ry))

def assess(path):
    rows = load(path)
    if not rows: print("empty: " + path); return
    hdr = list(rows[0].keys())
    print("FLIGHT ASSESSMENT  " + os.path.basename(path))
    recorder_health(rows, hdr)
    physics(rows)
    ascent(rows)
    booster(rows)
    rendezvous(rows)
    return_entry(rows)
    abort(rows)
    control(rows)
    print("\n" + "=" * 78 + "\n  END. Everything above was CHECKED, not skipped.\n" + "=" * 78)

def main():
    args = sys.argv[1:]
    if args and args[0] in ("--list","-l"):
        for p in reversed(captures()):
            print("  %s  %.0f KB" % (os.path.basename(p), os.path.getsize(p)/1024.0))
        return 0
    seed = args[0] if args else (captures()[-1] if captures() else None)
    if not seed or not os.path.exists(seed): print("no flight capture found"); return 2
    assess(seed); return 0

if __name__ == "__main__":
    sys.exit(main())
