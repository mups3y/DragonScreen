# -*- coding: utf-8 -*-
"""The general rendezvous decision algorithm (MechJeb structure), run in the 2-body sim.
Proves it converges to the gate from ARBITRARY starting orbits, one burn at a time, cheaply."""
import math
from orbmech import MU, R, propagate, elements_from_state, state_from_elements, period, lambert

GATE = 10000.0        # DIRECT takes over here; the autopilot's job is to deliver to the gate
GATE_V = 5.0          # ...co-moving to within a few m/s (DIRECT's own speed cap handles the rest)
MAXDV = 250.0         # per-burn sanity cap (Approach.MaxDvMps)
MAX_CLOSE_V = 30.0    # cap closing speed on the run-in so arrival at the gate is gentle


def sep(rv, rs):
    return math.hypot(rv[0]-rs[0], rv[1]-rs[1])


def closest_approach(vr, vv, sr, sv, span, steps=400):
    """min separation and its time over [0,span]; coarse then refine."""
    best_t, best_d = 0.0, 1e18
    dt = span/steps
    for i in range(steps+1):
        t = i*dt
        rvv, _ = propagate(vr, vv, t)
        rss, _ = propagate(sr, sv, t)
        d = sep(rvv, rss)
        if d < best_d:
            best_d, best_t = d, t
    # refine
    lo, hi = max(0.0, best_t-dt), best_t+dt
    for _ in range(60):
        m1, m2 = lo+(hi-lo)/3, hi-(hi-lo)/3
        d1 = sep(propagate(vr,vv,m1)[0], propagate(sr,sv,m1)[0])
        d2 = sep(propagate(vr,vv,m2)[0], propagate(sr,sv,m2)[0])
        if d1 < d2: hi = m2
        else: lo = m1
    t = 0.5*(lo+hi)
    return t, sep(propagate(vr,vv,t)[0], propagate(sr,sv,t)[0])


def match_dv(vr, vv, sr, sv, t):
    """dv to match station velocity at time t (both propagated to t)."""
    _, vvt = propagate(vr, vv, t)
    _, svt = propagate(sr, sv, t)
    return (svt[0]-vvt[0], svt[1]-vvt[1])


def intercept_dv(vr, vv, sr, sv, closing_time, standoff=0.0):
    """Burn NOW to arrive after closing_time at a point `standoff` metres short of the station
       (on the vessel->station line), so we stop at the gate rather than ram the station.
       dv = lambert_v1 - current_v."""
    rst, _ = propagate(sr, sv, closing_time)         # where the station will be
    aim = rst
    if standoff > 0.0:
        dx, dy = rst[0]-vr[0], rst[1]-vr[1]; dm = math.hypot(dx, dy)
        if dm > standoff:
            aim = (rst[0]-standoff*dx/dm, rst[1]-standoff*dy/dm)
    v1 = lambert(vr, aim, closing_time)
    if v1 is None:
        return None
    return (v1[0]-vv[0], v1[1]-vv[1])


def run(vr, vv, sr, sv, label, verbose=True):
    total = 0.0
    t = 0.0
    log = []
    stn_sma = elements_from_state(sr, sv)[0]
    CLOSE_R = stn_sma/25.0        # MechJeb's "close" band (~29 km here)
    for step in range(60):
        d = sep(vr, sr)
        relv = math.hypot(vv[0]-sv[0], vv[1]-sv[1])
        stn_period = period(sr, sv)
        if d < GATE and relv < GATE_V:
            log.append("  AT GATE  d=%.0f m relv=%.2f m/s  total dv=%.1f m/s  t=%.0fs  (DIRECT takes over)" % (d, relv, total, t))
            return True, total, log

        # 1. inside the gate but still moving: kill relative velocity, hand to DIRECT next tick
        if d < GATE:
            dv = match_dv(vr, vv, sr, sv, 0.0)
            vv = (vv[0]+dv[0], vv[1]+dv[1]); total += math.hypot(*dv)
            log.append("  %2d %-20s d=%7.0f relv=%6.2f  dv=%6.2f" % (step,"match@gate",d,relv,math.hypot(*dv)))
            continue

        # 2. already close (< SMA/25): match at an imminent sub-gate approach, else run in to the gate
        if d < CLOSE_R:
            cat, cad = closest_approach(vr, vv, sr, sv, stn_period*1.6)
            if cad < GATE and cat < stn_period:
                vr,vv = propagate(vr,vv,cat); sr,sv = propagate(sr,sv,cat); t += cat
                dv = match_dv(vr, vv, sr, sv, 0.0)
                vv = (vv[0]+dv[0], vv[1]+dv[1]); total += math.hypot(*dv)
                log.append("  %2d %-20s coast %5.0fs d=%6.0f dv=%6.2f" % (step,"match@approach",cat,cad,math.hypot(*dv)))
                continue
            # run in to the gate: intercept a point one gate-radius short of the station, gently
            run = d - GATE
            closev = min(max(run/100.0, 1.0), MAX_CLOSE_V)
            ctime = max(60.0, run/closev)
            dv = intercept_dv(vr, vv, sr, sv, ctime, standoff=GATE)
            if dv is None or math.hypot(*dv) > MAXDV:
                dv = match_dv(vr, vv, sr, sv, 0.0); why = "match(fallback)"
            else:
                why = "run-in(%.0fs)" % ctime
            vv = (vv[0]+dv[0], vv[1]+dv[1]); total += math.hypot(*dv)
            log.append("  %2d %-20s d=%7.0f dv=%6.2f coast %.0fs" % (step,why,d,math.hypot(*dv),ctime))
            vr,vv = propagate(vr,vv,ctime); sr,sv = propagate(sr,sv,ctime); t += ctime
            continue

        # 3. far but a close pass is coming (< SMA/25): coast to it and match velocity there
        cat, cad = closest_approach(vr, vv, sr, sv, stn_period*1.6)
        if cad < CLOSE_R:
            vr,vv = propagate(vr,vv,cat); sr,sv = propagate(sr,sv,cat); t += cat
            dv = match_dv(vr, vv, sr, sv, 0.0)
            vv = (vv[0]+dv[0], vv[1]+dv[1]); total += math.hypot(*dv)
            log.append("  %2d %-20s coast %5.0fs d=%6.0f dv=%6.2f" % (step,"match@pass",cat,cad,math.hypot(*dv)))
            continue

        # 4. far, no close pass: a cheap direct intercept if one exists, else a classic phasing step
        dv, dep, tof = hohmann_intercept(vr, vv, sr, sv, stn_period)
        if dv is not None:
            if dep > 0:
                vr,vv = propagate(vr,vv,dep); sr,sv = propagate(sr,sv,dep); t += dep
            vv = (vv[0]+dv[0], vv[1]+dv[1]); total += math.hypot(*dv)
            log.append("  %2d %-20s d=%7.0f dv=%6.2f dep %.0fs coast %.0fs" % (step,"transfer",d,math.hypot(*dv),dep,tof))
            vr,vv = propagate(vr,vv,tof); sr,sv = propagate(sr,sv,tof); t += tof
        else:
            dv, coast = phasing_step(vr, vv, sr, sv)
            vv = (vv[0]+dv[0], vv[1]+dv[1]); total += math.hypot(*dv)
            log.append("  %2d %-20s d=%7.0f dv=%6.2f coast %.0fs" % (step,"phasing",d,math.hypot(*dv),coast))
            vr,vv = propagate(vr,vv,coast); sr,sv = propagate(sr,sv,coast); t += coast
    log.append("  DID NOT CONVERGE in 60 steps  total dv=%.1f" % total)
    return False, total, log


def hohmann_intercept(vr, vv, sr, sv, stn_period):
    """Find a cheap intercept over a long horizon. Searches departure delay and tof (up to a few
       station periods, so large phase gaps are closed by a phasing transfer, not one huge burn).
       Returns (dv_now, coast) where dv is applied NOW and the caller coasts `coast` = dep+tof.
       Departure is folded in by requiring the burn at now (dep=0) OR returning the post-dep plan."""
    best = None
    deps = [stn_period*f for f in [i*0.1 for i in range(0, 21)]]        # 0 .. 2.0 P
    tofs = [stn_period*f for f in [0.3+i*0.1 for i in range(0, 28)]]    # 0.3 .. 3.0 P
    for dep in deps:
        vrd, vvd = propagate(vr, vv, dep)
        for tof in tofs:
            rst, svt = propagate(sr, sv, dep+tof)
            v1 = lambert(vrd, rst, tof)
            if v1 is None: continue
            dv = math.hypot(v1[0]-vvd[0], v1[1]-vvd[1])
            if dv > MAXDV: continue
            # arrival velocity on the transfer (coast the transfer orbit), and the match relv there
            _, v2 = propagate(vrd, v1, tof)
            arr = math.hypot(v2[0]-svt[0], v2[1]-svt[1])
            cost = dv + arr                          # TOTAL: burn in + match at arrival
            if cost > MAXDV: continue                # a ram-and-brake is not a cheaper transfer
            if best is None or cost < best[0]:
                best = (cost, dep, tof, (v1[0]-vvd[0], v1[1]-vvd[1]))
    if best is None:
        return None, None, None
    dv0, dep, tof, dvvec = best
    return dvvec, dep, tof


def _prograde_hat(r, v):
    """Unit vector along the velocity's tangential sense at r."""
    rm = math.hypot(*r)
    h = r[0]*v[1] - r[1]*v[0]                 # >0 CCW
    if h >= 0: return (-r[1]/rm, r[0]/rm)
    return (r[1]/rm, -r[0]/rm)


def phasing_step(vr, vv, sr, sv):
    """One classic reducer when no direct intercept is cheap: circularise, then Hohmann to the
       station's radius, then a phasing orbit to close the angle. One burn per call; the loop chains
       them. Returns (dv, coast)."""
    r = math.hypot(*vr); a_s = math.hypot(*sr)
    a_v, e_v = elements_from_state(vr, vv)[:2]
    ph = _prograde_hat(vr, vv)
    # 1. circularise at the current radius if eccentric
    if e_v > 0.02:
        vc = math.sqrt(MU/r)
        dv = (ph[0]*vc-vv[0], ph[1]*vc-vv[1])
        return dv, 5.0
    # 2. not co-altitude: Hohmann half-transfer toward the station's radius
    if abs(r - a_s) > 3000.0:
        rp, ra = min(r, a_s), max(r, a_s); a_t = (rp+ra)/2
        v_t = math.sqrt(MU*(2/r - 1/a_t))
        dv = (ph[0]*v_t-vv[0], ph[1]*v_t-vv[1])
        return dv, math.pi*math.sqrt(a_t**3/MU)        # coast half the transfer to the far apsis
    # 3. co-altitude: phasing orbit to close the along-track angle over one lap
    P_s = 2*math.pi*math.sqrt(a_s**3/MU)
    # signed phase angle: where the station is relative to the vessel (ahead positive, prograde)
    angv = math.atan2(vr[1], vr[0]); angs = math.atan2(sr[1], sr[0])
    dth = (angs - angv)
    # prograde sense: if orbiting CCW, "ahead" is +angle
    if (vr[0]*vv[1]-vr[1]*vv[0]) < 0: dth = -dth
    dth = (dth + math.pi) % (2*math.pi) - math.pi     # -pi..pi ; +ve = station ahead
    # to CATCH UP to a station ahead, gain angle: shorten the period (drop the orbit)
    frac = dth/(2*math.pi)
    if frac > 0.45: frac -= 1.0                        # take the shorter way round
    P_p = P_s*(1 - frac)
    P_p = max(P_s*0.80, min(P_s*1.20, P_p))            # bound the period change per lap
    a_p = (MU*(P_p/(2*math.pi))**2)**(1/3.0)
    if a_p < R+75000.0: a_p = R+75000.0                # periapsis-floor-ish guard
    v_p = math.sqrt(MU*(2/r - 1/a_p))
    dv = (ph[0]*v_p-vv[0], ph[1]*v_p-vv[1])
    return dv, P_p


def elts(apo_km, peri_km, argp=0.0, M=0.0):
    ra, rp = R+apo_km*1000, R+peri_km*1000
    a = (ra+rp)/2; e = (ra-rp)/(ra+rp)
    return state_from_elements(a, e, argp, M)


if __name__ == "__main__":
    STN = elts(133, 133)                          # station: 133 km circular
    stn_sma = elements_from_state(*STN)[0]
    cases = [
        ("A flight_0820: 133x120 catching up",  elts(133,120, 0.0, -0.12)),
        ("B low circular 100 km, phase +0.6",   elts(100,100, 0.0,  0.6)),
        ("C high circular 165 km, phase -1.5",  elts(165,165, 0.0, -1.5)),
        ("D eccentric 150x95, phase +2.5",      elts(150, 95, 0.7,  2.5)),
        ("E co-orbital 133, 150 deg behind",    elts(133,133, 0.0, -2.618)),
        ("F very low 80 km, phase +3.0",        elts(80, 80,  0.0,  3.0)),
    ]
    print("station: 133 km circular, SMA %.0f km, close-band SMA/25 = %.1f km\n" % (stn_sma/1000, stn_sma/25/1000))
    summary = []
    for name, (vr, vv) in cases:
        d0 = sep(vr, STN[0])
        ok, dv, log = run(list(vr), list(vv), list(STN[0]), list(STN[1]), name)
        summary.append((name, ok, dv, d0))
        print("== %s  (start sep %.0f km)  ->  %s  dv=%.1f m/s" % (name, d0/1000, "CONVERGED" if ok else "FAILED", dv))
        for l in log: print(l)
        print()
    print("="*70, "\nSUMMARY")
    for name, ok, dv, d0 in summary:
        print("  %-40s start %6.0f km  %-9s  dv %6.1f m/s" % (name, d0/1000, "OK" if ok else "FAIL", dv))
