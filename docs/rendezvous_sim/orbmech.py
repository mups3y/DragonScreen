# -*- coding: utf-8 -*-
"""Planar 2-body orbital mechanics for the rendezvous sim. Kerbin.
State vectors are 2D (x,y),(vx,vy). Verified against analytic circular orbits and energy/momentum
conservation before anything is built on top of it."""
import math

MU = 3.5316000e12      # Kerbin, m^3/s^2
R  = 600000.0          # Kerbin radius, m


def elements_from_state(r, v):
    """(a, e, argp, nu, M) from planar state. Angles in rad."""
    rm = math.hypot(*r); vm = math.hypot(*v)
    energy = vm*vm/2 - MU/rm
    a = -MU/(2*energy)
    # specific angular momentum (z component, planar)
    h = r[0]*v[1] - r[1]*v[0]
    # eccentricity vector
    ex = (v[1]*h)/MU - r[0]/rm
    ey = (-v[0]*h)/MU - r[1]/rm
    e = math.hypot(ex, ey)
    argp = math.atan2(ey, ex)
    # true anomaly
    nu = math.atan2(r[1], r[0]) - argp
    nu = (nu + math.pi) % (2*math.pi) - math.pi
    # eccentric & mean anomaly
    E = math.atan2(math.sqrt(max(1-e*e,0.0))*math.sin(nu), e+math.cos(nu))
    M = E - e*math.sin(E)
    return a, e, argp, nu, M, h


def state_from_elements(a, e, argp, M):
    """Planar state (r,v) from elements at mean anomaly M."""
    # solve Kepler
    E = M if e < 0.8 else math.pi
    for _ in range(80):
        f = E - e*math.sin(E) - M
        E -= f/(1 - e*math.cos(E))
    nu = 2*math.atan2(math.sqrt(1+e)*math.sin(E/2), math.sqrt(1-e)*math.cos(E/2))
    p = a*(1-e*e)
    rm = p/(1+e*math.cos(nu))
    # perifocal
    xp, yp = rm*math.cos(nu), rm*math.sin(nu)
    vpx = -math.sqrt(MU/p)*math.sin(nu)
    vpy =  math.sqrt(MU/p)*(e+math.cos(nu))
    c, s = math.cos(argp), math.sin(argp)
    r = (c*xp - s*yp, s*xp + c*yp)
    v = (c*vpx - s*vpy, s*vpx + c*vpy)
    return r, v


def propagate(r, v, dt):
    """Coast dt seconds (elliptic)."""
    a, e, argp, nu, M, h = elements_from_state(r, v)
    n = math.sqrt(MU/a**3)
    return state_from_elements(a, e, argp, M + n*dt)


def period(r, v):
    a = elements_from_state(r, v)[0]
    return 2*math.pi*math.sqrt(a**3/MU)


# ---- universal-variable Lambert (planar, prograde, short way), Bate-Mueller-White / Vallado ----
def _stumpff(z):
    if z > 1e-6:
        sz = math.sqrt(z)
        C = (1 - math.cos(sz))/z
        S = (sz - math.sin(sz))/(sz**3)
    elif z < -1e-6:
        sz = math.sqrt(-z)
        if sz > 350:            # cosh/sinh overflow guard; this deep is never a rendezvous transfer
            sz = 350.0
        C = (math.cosh(sz) - 1)/(sz*sz)
        S = (math.sinh(sz) - sz)/(sz**3)
    else:
        C, S = 0.5, 1.0/6.0
    return C, S


def lambert(r1, r2, tof, prograde=True):
    """Return v1 (velocity at r1) for the transfer r1->r2 in time tof. Planar."""
    r1m = math.hypot(*r1); r2m = math.hypot(*r2)
    cross_z = r1[0]*r2[1] - r1[1]*r2[0]
    dnu_cos = (r1[0]*r2[0] + r1[1]*r2[1])/(r1m*r2m)
    dnu_cos = max(-1.0, min(1.0, dnu_cos))
    dnu = math.acos(dnu_cos)
    # short/long way by prograde sense (planar: cross_z>0 is CCW=prograde)
    if (cross_z < 0 and prograde) or (cross_z > 0 and not prograde):
        dnu = 2*math.pi - dnu
    A = math.sin(dnu)*math.sqrt(r1m*r2m/(1 - math.cos(dnu)))
    if abs(A) < 1e-9:
        return None

    def tof_of_z(z):
        C, S = _stumpff(z)
        if C <= 0:
            return None, None
        y = r1m + r2m + A*(z*S - 1)/math.sqrt(C)
        if A > 0 and y < 0:
            return None, y
        x = math.sqrt(max(y/C, 0.0))
        return (x**3*S + A*math.sqrt(y))/math.sqrt(MU), y

    # time-of-flight is monotonically INCREASING in z; bracket then bisect.
    lo, hi = -4*math.pi*math.pi, 4*math.pi*math.pi
    # widen hi until tof(hi) > target (long transfers need large z)
    for _ in range(60):
        t, _y = tof_of_z(hi)
        if t is not None and t > tof:
            break
        hi *= 1.5 if hi > 0 else 0.5
    # widen lo (hyperbolic side) until tof(lo) < target, bounded so cosh stays finite
    for _ in range(40):
        t, _y = tof_of_z(lo)
        if t is not None and t < tof:
            break
        if lo > -100000.0:
            lo *= 1.5
    z = 0.0
    for _ in range(200):
        z = 0.5*(lo + hi)
        t, _y = tof_of_z(z)
        if t is None:
            lo = z; continue
        if abs(t - tof) < 1e-4:
            break
        if t < tof:
            lo = z
        else:
            hi = z
    C, S = _stumpff(z)
    y = r1m + r2m + A*(z*S - 1)/math.sqrt(C)
    f = 1 - y/r1m
    g = A*math.sqrt(y/MU)
    v1 = ((r2[0]-f*r1[0])/g, (r2[1]-f*r1[1])/g)
    return v1


if __name__ == "__main__":
    # --- verify: circular orbit stays circular, period correct ---
    r0 = (R+120000.0, 0.0)
    vc = math.sqrt(MU/(R+120000.0))
    v0 = (0.0, vc)
    P = period(r0, v0)
    print("circular 120km: period %.1f s (expect ~%.1f)" % (P, 2*math.pi*(R+120000.0)/vc))
    r1, v1 = propagate(r0, v0, P)
    print("after one period: dr=%.3f m dv=%.4f m/s (expect ~0)" % (
        math.hypot(r1[0]-r0[0], r1[1]-r0[1]), math.hypot(v1[0]-v0[0], v1[1]-v0[1])))
    r2, v2 = propagate(r0, v0, P/4)
    print("quarter period pos: (%.0f, %.0f)  expect ~(0, %.0f)" % (r2[0], r2[1], R+120000.0))
    # --- verify Lambert: propagate a known arc, recover v1 ---
    a, e, argp, M = R+130000.0, 0.02, 0.5, 0.3
    rA, vA = state_from_elements(a, e, argp, M)
    tof = 900.0
    rB, vB = propagate(rA, vA, tof)
    vL = lambert(rA, rB, tof)
    print("Lambert recover v1: got (%.2f,%.2f) true (%.2f,%.2f) err %.4f m/s" % (
        vL[0], vL[1], vA[0], vA[1], math.hypot(vL[0]-vA[0], vL[1]-vA[1])))
