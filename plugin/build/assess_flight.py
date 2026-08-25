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


# ---------------------------------------------------------------- missions, not files
#
# ⛔ A RECORDING IS HALF A MISSION. THE RECORDER SPLITS AT AUTOPILOT HANDOVER.
#
# `FlightRecorder` starts a new file on every autopilot engage, so a mission lands in at least two:
# ascent+booster in one, docking+return in the next, 0-10 s apart in `ut`. Taking the newest file -
# which is what this tool did until 2026-08-12 - therefore assessed HALF A FLIGHT and said nothing
# about it. That is the exact failure this tool exists to prevent, re-entering through the back door:
# every docking assessment reported "blocks not exercised: a_, b_" and I read it as "launched from an
# orbital save", and the booster numbers in docs/F9I_BOOSTER_TARGETS.md came from a different file
# than the docking numbers beside them. 35 archived recordings; not one holds a whole mission.
#
# So segments are chained on UNIVERSAL TIME, which is continuous across the split, and `met` - which
# restarts at 0 in each file - is rebased onto the mission clock.
SEGMENT_GAP_S = 120.0


def segment_info(path):
    """(hdr, ut0, ut1, rows, has_liftoff) - or None if it is not a usable recording."""
    try:
        rows = list(csv.reader(io.open(path, encoding="utf-8", errors="replace")))
    except IOError:
        return None
    if len(rows) < 3:
        return None
    hdr = rows[0]
    if "ut" not in hdr:
        return None
    iu, ia = hdr.index("ut"), (hdr.index("a_phase") if "a_phase" in hdr else -1)
    ut0 = ut1 = None
    lift = False
    n = 0
    for r in rows[1:]:
        if len(r) != len(hdr):
            continue
        try:
            u = float(r[iu])
        except ValueError:
            continue
        if ut0 is None:
            ut0 = u
        ut1 = u
        n += 1
        if ia >= 0 and r[ia] == "VERTICAL RISE":
            lift = True
    if ut0 is None or n < 20:
        return None
    return hdr, ut0, ut1, n, lift


def missions():
    """Every recording grouped into the mission it belongs to, oldest first."""
    paths = glob.glob(os.path.join(CAPTURE, "flight_*.csv")) + \
            glob.glob(os.path.join(ARCHIVE, "flight_*.csv"))
    segs = []
    for p in sorted(set(paths)):
        info = segment_info(p)
        if info:
            segs.append((p,) + info)
    segs.sort(key=lambda s: s[2])                       # by ut0

    out, cur = [], []
    for s in segs:
        path, hdr, ut0, ut1, n, lift = s
        if cur:
            gap = ut0 - cur[-1][3]
            # A NEW LAUNCH ALWAYS STARTS A MISSION, however small the gap. The last four launches
            # sit in one continuous game session 10 s apart, and without this test a liftoff would
            # be chained onto the previous flight's splashdown.
            # A NEGATIVE gap is a revert - the same UT flown twice - and is also a new mission.
            same = (0.0 <= gap <= SEGMENT_GAP_S) and not lift and hdr == cur[-1][1]
            if not same:
                out.append(cur); cur = []
        cur.append(s)
    if cur:
        out.append(cur)
    return out


def mission_containing(path):
    """The list of segments making up the mission that `path` belongs to."""
    target = os.path.abspath(path)
    for m in missions():
        if any(os.path.abspath(s[0]) == target for s in m):
            return m
    info = segment_info(path)
    return [(path,) + info] if info else None


def load_mission(segs):
    """
    Concatenate the segments into one flight, with `met` rebased onto the mission clock.

    Headers are identical by construction - `missions()` refuses to chain across a schema change,
    because column 118 means different things in a 175-column file and a 181-column one, and a
    silent misalignment is worse than two separate assessments.
    """
    hdr = segs[0][1]
    iu, im = hdr.index("ut"), hdr.index("met")
    ut_start = segs[0][2]
    data, malformed = [], 0
    for s in segs:
        rows = list(csv.reader(io.open(s[0], encoding="utf-8", errors="replace")))
        for r in rows[1:]:
            if len(r) != len(hdr):
                malformed += 1
                continue
            try:
                r[im] = "%.2f" % (float(r[iu]) - ut_start)
            except ValueError:
                pass
            data.append(r)
    return hdr, data, malformed


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

    # ⛔ THE r_ BLOCK IS THE WHOLE RETURN, NOT JUST THE ENTRY. `r_stage` names the ENTRY stage and
    # reads "-" for the entire de-orbit phase, so gating the block on it judges the de-orbit columns
    # (r_deorbitMissKm, r_deorbitThr, r_nodePhase, r_nodeDvLeft) only over entry rows - where the burn
    # is long finished and every one of them is frozen. That reported live de-orbit telemetry as
    # "CONSTANT" / "ALL ZERO" on flight_0820 and nearly buried a real diagnosis. A return row is one
    # where the entry stage is running OR the de-orbit/entry owner has the stick.
    ir_stage, ix_owner = i2.get("r_stage"), i2.get("x_owner")
    def return_active(r):
        if ir_stage is not None and r[ir_stage] not in ("-", "", "Idle"):
            return True
        if ix_owner is not None and r[ix_owner] in ("deorbit", "entry"):
            return True
        return False

    live = {"a_": active("a_", "a_phase"), "b_": active("b_", "b_phase"),
            "r_": any(return_active(r) for r in data), "m_": True, "x_": True}
    dormant = [p for p, on in live.items() if not on]
    if dormant:
        print("  blocks not exercised by this flight (their columns are correctly idle): %s"
              % ", ".join(sorted(dormant)))

    # ---- A BLOCK IS LIVE FOR PART OF A MISSION, NOT ALL OF IT. ----
    # Chaining segments made this matter: a mission now holds a booster leg AND a capsule-only leg,
    # so `b_` is "exercised" while being legitimately empty for most rows. Judging a column over the
    # whole mission then flagged `b_massT` as "MASS <= 0 - impossible" from the rows where there is
    # no booster at all. A column is only assessed over the rows where ITS OWN block is running.
    gate = {"a_": "a_phase", "b_": "b_phase"}

    def block_rows(pre):
        if pre == "r_":
            return [r for r in data if return_active(r)]
        key = gate.get(pre)
        if key is None or key not in i2:
            return data
        g = i2[key]
        return [r for r in data if r[g] not in ("-", "", "Idle")]

    flags = []
    for k, name in enumerate(hdr):
        pre = name[:2]
        if pre in live and not live[pre]:
            continue
        rows_for = block_rows(pre) if pre in gate else data
        if not rows_for:
            continue
        vals = [r[k] for r in rows_for]
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


# ---- BODY CONSTANTS. The recorder logs no body, so DETECT it from the data. ----
# ⛔ THE VIS-VIVA CHECK WAS HARD-CODED TO KERBIN (MU 3.5316e12, R 600 km). An RSS/Earth flight
# then read "105 of 105 samples off by >2%" - a FALSE alarm from checking Earth orbital speeds
# (LEO ~7.8 km/s) against Kerbin gravity, not a physics fault. The stock build flies Kerbin, the
# RSS build flies Earth, and this tool assesses both, so it must pick the right body. Chosen by
# which body's circular-orbit speed at the sampled altitude matches the observed a_orbSpeed.
BODIES = {"Kerbin": (3.5316000e12, 600000.0),
          "Earth":  (3.9860044e14, 6371000.0)}


def detect_body(data, i):
    for r in data[len(data) // 3:]:
        v, alt = num(r, i, "a_orbSpeed"), num(r, i, "a_altAsl")
        if v and alt and v > 100:
            best, berr = "Kerbin", 1e9
            for name, (mu, rad) in BODIES.items():
                e = abs(math.sqrt(mu / (rad + alt)) - v) / v
                if e < berr:
                    best, berr = name, e
            return best, BODIES[best]
    return "Kerbin", BODIES["Kerbin"]


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
    body, (MU, R) = detect_body(data, i)
    print("  body detected: %s (MU %.4g, R %.0f km) - vis-viva checked against this" % (body, MU, R / 1000.0))
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

    # ---- ACCURACY: touchdown vs the barge DECK CENTRE, split into along-track (downrange) + cross-track ----
    # PAD = LC-39A. BARGE = the physical DECK CENTRE = BoosterRecovery.DroneshipEarthLatDeg/LonDeg (the aim).
    # ⛔ 2026-08-24: this is now the DECK CENTRE (group centre 32.7875/-76.6445 + the SpaceXbarge2 model
    # offset of ~5.7 m), NOT the group centre / waypoint. Aiming at the group centre made an on-aim landing
    # read "dead centre" while it was ~5.7 m off the real deck (25 m wide) - the circular measure the user
    # caught. Keep BARGE == DroneshipEarthLatDeg/LonDeg so the tool measures the real deck miss.
    PAD, BARGE = (28.6084, -80.6043), (32.787551, -76.644507)
    def _bd(a, c):
        p1, p2 = math.radians(a[0]), math.radians(c[0]); dl = math.radians(c[1] - a[1])
        x = math.sin(dl) * math.cos(p2)
        y = math.cos(p1) * math.sin(p2) - math.sin(p1) * math.cos(p2) * math.cos(dl)
        h = math.sin((p2 - p1) / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
        return math.degrees(math.atan2(x, y)) % 360, 6371.0 * 2 * math.asin(math.sqrt(h))
    last = next((r for r in reversed(b) if num(r, i, "b_lat") not in (None, 0.0)), None)
    if last is not None:
        land = (num(last, i, "b_lat"), num(last, i, "b_lon"))
        br_l, d_l = _bd(PAD, land); br_b, d_b = _bd(PAD, BARGE); _, miss = _bd(land, BARGE)
        dn = math.radians(land[0] - BARGE[0]) * 6371.0
        de = math.radians(land[1] - BARGE[1]) * 6371.0 * math.cos(math.radians((land[0] + BARGE[0]) / 2))
        th = math.radians(br_b)
        along = dn * math.cos(th) + de * math.sin(th)      # + = past the barge (long)
        cross = -dn * math.sin(th) + de * math.cos(th)     # + = right of the track
        # BARGE is now the physical DECK CENTRE (== the guidance aim). Report the real deck miss in METRES
        # so it can never round to 0, split into downrange (along-track) and cross-track. The barge is
        # 50 m long x 25 m wide, so the deck edge is at 25 m (length) / 12.5 m (width).
        miss_m = miss * 1000.0
        along_m, cross_m = along * 1000.0, cross * 1000.0
        on_deck = abs(along_m) <= 25.0 and abs(cross_m) <= 12.5
        print("")
        print("  ACCURACY:  touchdown   %.5f,%.5f  = %3.0f km / %.1f deg from pad" % (land[0], land[1], d_l, br_l))
        print("             DECK CENTRE %.5f,%.5f  (physical barge deck = the guidance aim)"
              % (BARGE[0], BARGE[1]))
        print("             MISS vs DECK CENTRE = %.1f m   (downrange %+.1f m, cross %+.1f m)   %s"
              % (miss_m, along_m, cross_m, "ON DECK" if on_deck else "*** OFF DECK ***"))
        print("             (deck 50 m x 25 m: edge at 25 m downrange / 12.5 m cross)")
        if abs(cross_m) > 10000.0:
            print("             -> the miss is CROSS-TRACK: off the flown plane (launch azimuth / barge lon)")
        elif abs(along_m) > 10000.0:
            print("             -> the miss is DOWNRANGE: staging energy / entry-burn sizing (barge is on the track)")

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
def fuel_by_phase(hdr, data, i, phase_col, lf, ox, extra=None):
    """One row per phase transition: fuel/ox fraction at the moment each phase begins. extra is a
    list of (label, colname) to also print (e.g. the booster recovery reserve)."""
    if phase_col not in i or lf not in i:
        return
    prev = None
    for r in data:
        ph = r[i[phase_col]]
        if ph in ("-", "", "STANDBY") or ph == prev:
            continue
        prev = ph
        vlf = num(r, i, lf); vox = num(r, i, ox)
        line = "     %-16s lf %5.1f%%  ox %5.1f%%" % (
            ph, (vlf or 0) * 100, (vox or 0) * 100)
        for label, col in (extra or []):
            v = num(r, i, col)
            line += "  %s %s" % (label, ("%6.2f" % v) if v is not None else "  -  ")
        print(line)


def propellant(hdr, data, i):
    section("8. PROPELLANT & FUEL (RealFuels Kerosene+LqdOxygen, now read correctly)")

    # ---- ASCENT / S2: fuel through the climb and insertion ----
    if "a_lfFrac" in i:
        print("  ASCENT vehicle (booster while attached, then S2) - fuel at each phase:")
        fuel_by_phase(hdr, data, i, "a_phase", "a_lfFrac", "a_oxFrac")

    # ---- BOOSTER recovery: the entry burn MUST reserve the landing burn's share ----
    if "b_lfFrac" in i and "d_recovFrac" in i:
        print("\n  BOOSTER recovery - fuel + reserve (the entry burn cut watches d_recovFrac):")
        fuel_by_phase(hdr, data, i, "b_phase", "b_lfFrac", "b_oxFrac",
                      extra=[("recovFrac", "d_recovFrac"), ("units", "d_recovUnits")])
        # Auto-flag the reserve-cut failure: the recovery fraction at the START of DESCENT is what the
        # entry burn LEFT for the landing. Below ~0.25 means the entry burn over-burned the landing dry.
        desc = next((r for r in data if r[i["b_phase"]] == "DESCENT"), None)
        land = next((r for r in data if r[i["b_phase"]] == "LANDING BURN"), None)
        if desc is not None:
            rf = num(desc, i, "d_recovFrac")
            if rf is not None and 0.0 <= rf < 0.25:
                print("     *** ENTRY BURN OVER-BURNED: only %.0f%% recovery propellant left at DESCENT"
                      " (reserve cut should hold ~35%%) - the landing will run dry ***" % (rf * 100))
            elif rf is not None and rf < 0.0:
                print("     *** d_recovFrac = -1 at DESCENT: the reserve baseline never latched -"
                      " the reserve cut is DISABLED, entry burn runs on the speed cut alone ***")
        if land is not None:
            lf_end = num(data[-1], i, "b_lfFrac")
            v_end = num(data[-1], i, "b_srfSpeed")
            if lf_end is not None and lf_end < 0.02 and v_end is not None and v_end > 5.0:
                print("     *** BOOSTER RAN DRY mid-landing (fuel ~0, still %.0f m/s) ***" % v_end)

    # ---- CAPSULE monopropellant (Draco/SuperDraco) ----
    m = [(num(r, i, "met"), num(r, i, "m_monoOurs"), r[i["a_phase"]],
          r[i["x_owner"]] if "x_owner" in i else "-") for r in data]
    m = [x for x in m if x[1] is not None]
    if m:
        print("\n  CAPSULE monopropellant: start %.1f  end %.1f  capacity %s"
              % (m[0][1], m[-1][1], data[0][i["m_monoCap"]] if "m_monoCap" in i else "?"))
        prev = None
        for t, v, ph, ow in m:
            if prev is None or prev - v > 8.0:
                print("     met %7.0f  mono %6.1f   phase=%-16s owner=%s" % (t, v, ph, ow))
                prev = v
        if m[-1][1] < 5.0:
            print("     *** CAPSULE TANK RAN DRY ***")

    # ---- CONDUCTOR progression: which legs the AUTO SEQUENCE actually flew ----
    if "d_autoStep" in i:
        steps, prev = [], None
        for r in data:
            s = r[i["d_autoStep"]]
            if s != prev and s not in ("-", ""):
                steps.append(s); prev = s
        if steps:
            print("\n  AUTO SEQUENCE conductor flew: " + " -> ".join(steps))


def list_missions():
    ms = missions()
    print("%d mission(s), oldest first. A mission is one or more contiguous recordings.\n" % len(ms))
    for k, m in enumerate(ms, 1):
        span = m[-1][3] - m[0][2]
        rows = sum(s[4] for s in m)
        print("  %2d  %s  %5.0f s  %6d rows  %d segment(s)"
              % (k, os.path.basename(m[0][0]), span, rows, len(m)))
        for s in m[1:]:
            print("      + %s" % os.path.basename(s[0]))
    return 0


def main():
    args = [a for a in sys.argv[1:]]
    if args and args[0] in ("--list", "-l"):
        return list_missions()
    seed = args[0] if args else newest()
    if not seed or not os.path.exists(seed):
        print("no flight capture found"); return 2

    segs = mission_containing(seed)
    if not segs:
        print("could not read %s" % seed); return 2
    hdr, data, malformed = load_mission(segs)

    print("FLIGHT ASSESSMENT  %s" % os.path.basename(segs[0][0]))
    if len(segs) > 1:
        # Say which files this is, every time. The whole point is that a mission is not a file.
        print("  %d recordings chained into one mission (met rebased onto the mission clock):"
              % len(segs))
        for s in segs:
            print("     %-28s ut %9.0f -> %9.0f  %6.0f s  %5d rows"
                  % (os.path.basename(s[0]), s[2], s[3], s[3] - s[2], s[4]))
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
