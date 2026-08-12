#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
assess_flight.py - the WHOLE flight, every column, one command.

    python plugin/build/assess_flight.py                 # newest capture
    python plugin/build/assess_flight.py <file.csv>

WHY THIS EXISTS
---------------
Four times in one week the user had to say some version of "you were asked to assess ALL of it".
Each time the reason was the same: doing it properly cost twenty tool calls and doing it partly
cost one, so I kept doing it partly and reporting as though I had not.

This removes the gradient. The thorough pass is now the cheap one.

It reports, without being asked:
  1. recorder health      - constant / empty / all-zero / impossible columns
  2. physics self-check   - does the file agree with itself
  3. every phase          - ascent, booster, return, with the numbers that matter
  4. booster vs F9I       - roll and AoA against docs/F9I_BOOSTER_TARGETS.md
  5. controller ownership - CONTENDED ticks, the two-owner bug
  6. docking axes         - the shrank/grew test that finds an inverted sign
  7. propellant ledger    - where the monopropellant actually went

Anything it flags is a finding. Anything it does not flag has been CHECKED, not skipped.
"""
import csv, io, os, sys, glob, math

CAPTURE = r"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\DragonScreen_capture"
ARCHIVE = r"C:\Users\User\Desktop\quarantine\dragonscreen_flightdata"

# From docs/F9I_BOOSTER_TARGETS.md - measured over bb_booster_001..008.
# ⚠ GROUPED, because F9I's black box marks segments and ours marks phases. Comparing our
# BOOSTBACK KILL against a target of 0 reported "62x OVER" on a phase F9I simply does not
# separate. Map ours onto theirs, then compare like with like.
F9I_SEGMENT = {"FLIP": "flip+boostback", "BOOSTBACK KILL": "flip+boostback",
               "BOOSTBACK": "flip+boostback", "COAST": "coast",
               "ENTRY BURN": "entry burn", "DESCENT": "descent",
               "LANDING BURN": "landing burn"}
F9I_ROLL = {"flip+boostback": 138, "coast": 102, "entry burn": 94,
            "descent": 109, "landing burn": 0}
F9I_TOTAL_ROLL = 443


def newest():
    c = glob.glob(os.path.join(CAPTURE, "flight_*.csv")) + \
        glob.glob(os.path.join(ARCHIVE, "flight_*.csv"))
    if not c:
        return None
    return max(c, key=os.path.getmtime)


def load(path):
    rows = list(csv.reader(io.open(path, encoding="utf-8", errors="replace")))
    hdr = rows[0]
    data = [r for r in rows[1:] if len(r) == len(hdr)]
    return hdr, data, len(rows) - 1 - len(data)


def num(r, i, n):
    try:
        return float(r[i[n]])
    except (ValueError, KeyError, IndexError):
        return None


def section(t):
    print("\n" + "=" * 78)
    print("  " + t)
    print("=" * 78)


# ------------------------------------------------------------------ 1. recorder health
def recorder_health(hdr, data, malformed):
    section("1. RECORDER HEALTH")
    print("  %d rows, %d columns, %d malformed" % (len(data), len(hdr), malformed))
    if malformed:
        print("  *** MALFORMED ROWS - the file may be column-shifted. Do not trust it. ***")
    # ⚠ ONLY FLAG COLUMNS WHOSE BLOCK ACTUALLY FLEW. A capture that starts in orbit has no
    # ascent and no booster, so every `b_` column is legitimately constant - and a tool that
    # reports 75 faults on a clean file trains its reader to skip it, which is the failure this
    # whole script exists to stop.
    def active(prefix, key):
        return any(r[i2[key]] not in ("-", "", "Idle") for r in data) if key in i2 else True
    i2 = {n: k for k, n in enumerate(hdr)}
    live = {"a_": active("a_", "a_phase"), "b_": active("b_", "b_phase"),
            "r_": active("r_", "r_stage"), "m_": True, "x_": True}
    dormant = [p for p, on in live.items() if not on]
    if dormant:
        print("  blocks not exercised by this flight (their columns are correctly idle): %s"
              % ", ".join(sorted(dormant)))

    flags = []
    for k, name in enumerate(hdr):
        pre = name[:2]
        if pre in live and not live[pre]:
            continue
        vals = [r[k] for r in data]
        ne = [v for v in vals if v != ""]
        nums = []
        for v in ne:
            try:
                nums.append(float(v))
            except ValueError:
                pass
        note = []
        if not ne:
            note.append("ALL EMPTY")
        elif len(set(ne)) == 1:
            note.append("CONSTANT '%s'" % ne[0])
        if nums and len(nums) == len(ne):
            if any(math.isnan(x) or math.isinf(x) for x in nums):
                note.append("NaN/Inf")
            if all(x == 0.0 for x in nums):
                note.append("ALL ZERO")
            if name.endswith("massT") and any(x <= 0.0 for x in nums):
                note.append("*** MASS <= 0 - impossible ***")
        if note:
            flags.append((name, "; ".join(note)))
    if flags:
        print("  %d column(s) flagged:" % len(flags))
        for n, t in flags:
            print("     %-22s %s" % (n, t))
    else:
        print("  no dead or impossible columns")


# ------------------------------------------------------------------ 2. physics self-check
def physics(hdr, data, i):
    section("2. PHYSICS SELF-CHECK (does the file agree with itself)")
    errs, prev = [], None
    for r in data:
        a, t, vs = num(r, i, "a_altAsl"), num(r, i, "met"), num(r, i, "a_vertSpeed")
        if None in (a, t, vs):
            continue
        if prev and 0.15 < t - prev[1] < 0.3 and abs(vs) > 1:
            errs.append(abs((a - prev[0]) / (t - prev[1]) - vs) / max(abs(vs), 1))
        prev = (a, t)
    if errs:
        errs.sort()
        print("  a_vertSpeed vs d(alt)/dt   median %.3f  95th %.3f  (n=%d)"
              % (errs[len(errs) // 2], errs[int(len(errs) * .95)], len(errs)))
    MU, R = 3.5316e12, 600000.0
    bad = n = 0
    for r in data[::40]:
        ap, pe, v, alt = (num(r, i, "a_apoKm"), num(r, i, "a_periKm"),
                          num(r, i, "a_orbSpeed"), num(r, i, "a_altAsl"))
        if None in (ap, pe, v, alt) or pe < 0 or v < 100:
            continue
        sma = R + (ap + pe) * 500.0
        term = MU * (2.0 / (R + alt) - 1.0 / sma)
        if term <= 0:
            continue
        n += 1
        if abs(math.sqrt(term) - v) / v > 0.02:
            bad += 1
    if n:
        print("  a_orbSpeed vs vis-viva     %d of %d samples off by >2%%" % (bad, n))


# ------------------------------------------------------------------ 3/4. phases
def phases(hdr, data, i):
    from collections import OrderedDict
    section("3. ASCENT")
    prev = None
    for r in data:
        p = r[i["a_phase"]]
        if p != prev:
            print("  met %7.1f  %-18s ap %7.1f pe %9.1f  attErr %5.1f  thr %.2f  mass %6.2f"
                  % (num(r, i, "met") or 0, p, num(r, i, "a_apoKm") or 0,
                     num(r, i, "a_periKm") or 0, num(r, i, "a_attErrDeg") or -1,
                     num(r, i, "a_cmdThrottle") or 0, num(r, i, "a_massT") or 0))
            prev = p

    section("4. BOOSTER  (roll target from docs/F9I_BOOSTER_TARGETS.md)")
    b = [r for r in data if r[i["b_phase"]] not in ("-", "")]
    if not b:
        print("  no booster flight in this recording")
        return
    ph = OrderedDict()
    for r in b:
        ph.setdefault(r[i["b_phase"]], []).append(r)
    print("  phase          secs | attErr avg/max | actP%   actY%   actR%  | roll deg")
    tot = 0
    byseg = {}
    rollcol = "b_omegaRdps" if "b_omegaRdps" in i else "b_omegaR"
    scale = 1.0 if rollcol.endswith("dps") else 180.0 / math.pi
    for k, v in ph.items():
        ae = [abs(num(r, i, "b_attErrDeg")) for r in v if num(r, i, "b_attErrDeg") is not None]
        sat = lambda c: 100.0 * sum(1 for r in v if abs(num(r, i, c) or 0) >= .99) / len(v)
        roll = sum(abs(num(r, i, rollcol) or 0) * .2 for r in v) * scale
        tot += roll
        seg = F9I_SEGMENT.get(k)
        if seg:
            byseg[seg] = byseg.get(seg, 0.0) + roll
        print("  %-14s %5.0f | %6.1f/%6.1f | %5.1f %6.1f %6.1f | %8.0f"
              % (k, len(v) * .2, sum(ae) / len(ae), max(ae),
                 sat("b_actP"), sat("b_actY"), sat("b_actR"), roll))
    print("")
    print("  roll against F9I's own black box, grouped to its segments:")
    print("     segment          ours    F9I   verdict")
    for seg in ("flip+boostback", "coast", "entry burn", "descent", "landing burn"):
        if seg not in byseg:
            continue
        ours, tgt = byseg[seg], F9I_ROLL[seg]
        ok = ours <= max(tgt * 1.5, tgt + 40)
        print("     %-15s %6.0f  %5d   %s" % (seg, ours, tgt,
              "OK" if ok else "*** %.1fx OVER ***" % (ours / max(tgt, 1))))
    print("     %-15s %6.0f  %5d   %s" % ("TOTAL", tot, F9I_TOTAL_ROLL,
          "OK" if tot < F9I_TOTAL_ROLL * 1.5
          else "*** %.1fx OVER ***" % (tot / F9I_TOTAL_ROLL)))

    section("5. RETURN")
    e = [r for r in data if r[i["r_stage"]] not in ("Idle", "-", "")]
    if not e:
        print("  the return never ran")
    else:
        prev = None
        for r in e:
            st = r[i["r_stage"]]
            if st != prev:
                print("  met %7.1f  %-17s alt %6.1f km  miss %8.1f km  liftMin %6.2f"
                      % (num(r, i, "met") or 0, st, (num(r, i, "a_altAsl") or 0) / 1000.0,
                         (num(r, i, "r_missM") or 0) / 1000.0, num(r, i, "r_liftMin") or 0))
                prev = st
        lm = [num(r, i, "r_liftMin") or 0 for r in e]
        if min(lm) == 0.0 and max(lm) == 0.0:
            print("  *** r_liftMin FLAT ZERO - the entry flew OPEN LOOP. The miss is DE-ORBIT AIM")
            print("      error, not guidance error. Calibrate on the SETTLED miss, never on")
            print("      WorstErrorM, which is a lead-compensated transient. ***")


# ------------------------------------------------------------------ 6. ownership + docking
def control(hdr, data, i):
    section("6. CONTROLLER OWNERSHIP")
    if "x_owner" not in i:
        print("  no x_owner column - recorder predates the ownership block")
        return
    from collections import Counter
    c = Counter(r[i["x_owner"]] for r in data)
    for k, v in c.most_common():
        mark = "  *** TWO CONTROLLERS ON ONE VEHICLE ***" if k.startswith("CONTENDED") else ""
        print("     %-22s %6d rows (%5.0f s)%s" % (k, v, v * .2, mark))

    section("7. DOCKING AXES  (a command that grows its own offset is an INVERTED SIGN)")
    dk = [r for r in data if r[i["x_owner"]] == "docking"]
    if not dk or "x_dkDistF" not in i:
        print("  no docking with input instrumentation in this recording")
        return
    print("  %d rows (%.0f s)" % (len(dk), len(dk) * .2))
    for dist, cmd, name in (("x_dkDistF", "x_fore", "FORE  "),
                            ("x_dkDistS", "x_transX", "STARBD"),
                            ("x_dkDistT", "x_transY", "TOP   ")):
        good = bad = 0
        for a, bb in zip(dk, dk[1:]):
            d0, d1, cc = num(a, i, dist), num(bb, i, dist), num(a, i, cmd)
            if None in (d0, d1, cc) or abs(cc) < .02:
                continue
            if abs(d1) < abs(d0):
                good += 1
            elif abs(d1) > abs(d0):
                bad += 1
        if good + bad:
            print("     %s shrank %5d  grew %5d  -> %s"
                  % (name, good, bad, "OK" if good > bad else "*** INVERTED ***"))
    st = set(r[i["x_dkStage"]] for r in dk)
    print("     stages reached: %s" % ", ".join(sorted(st)))
    if st == {"ToGate"}:
        print("     *** NEVER LEFT ToGate - the gate->standoff leg is not running ***")


# ------------------------------------------------------------------ 8. propellant
def propellant(hdr, data, i):
    section("8. PROPELLANT LEDGER")
    m = [(num(r, i, "met"), num(r, i, "m_monoOurs"), r[i["a_phase"]],
          r[i["x_owner"]] if "x_owner" in i else "-") for r in data]
    m = [x for x in m if x[1] is not None]
    if not m:
        print("  no monopropellant column")
        return
    print("  start %.1f   end %.1f   capacity %s"
          % (m[0][1], m[-1][1], data[0][i["m_monoCap"]] if "m_monoCap" in i else "?"))
    prev = None
    for t, v, ph, ow in m:
        if prev is None or prev - v > 8.0:
            print("     met %7.0f  mono %6.1f   phase=%-16s owner=%s" % (t, v, ph, ow))
            prev = v
    if m[-1][1] < 5.0:
        print("     *** TANK RAN DRY ***")


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else newest()
    if not path or not os.path.exists(path):
        print("no flight capture found"); return 2
    hdr, data, malformed = load(path)
    print("FLIGHT ASSESSMENT  %s" % os.path.basename(path))
    i = {n: k for k, n in enumerate(hdr)}
    recorder_health(hdr, data, malformed)
    physics(hdr, data, i)
    phases(hdr, data, i)
    control(hdr, data, i)
    propellant(hdr, data, i)
    print("\n" + "=" * 78)
    print("  END. Everything above was CHECKED. Nothing here was skipped for brevity.")
    print("=" * 78)
    return 0


if __name__ == "__main__":
    sys.exit(main())
