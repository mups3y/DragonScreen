import math
from orbmech import MU, R, state_from_elements, elements_from_state
import algo
from algo import run, sep, elts

# REAL geometry from flight_0820_232611: station ~120 km circular, vessel 134 x 120 (eccentric ABOVE)
STN_real = elts(120, 120)
print("REAL station: 120 km circular, SMA %.0f km\n" % (elements_from_state(*STN_real)[0]/1000))
for phase in [-0.12, -0.20, 0.15]:
    vr, vv = elts(134, 120, 0.0, phase)
    d0 = sep(vr, STN_real[0])
    ok, dv, log = run(list(vr), list(vv), list(STN_real[0]), list(STN_real[1]), "real")
    print("== vessel 134x120 (ecc), phase %+.2f, start sep %.0f km -> %s  dv=%.1f m/s" % (
        phase, d0/1000, "CONVERGED" if ok else "FAILED", dv))
    for l in log[:12]: print(l)
    print()

# for contrast: the WRONG geometry the sim used (station 133)
STN_wrong = elts(133, 133)
vr, vv = elts(133, 120, 0.0, -0.12)
ok, dv, log = run(list(vr), list(vv), list(STN_wrong[0]), list(STN_wrong[1]), "wrong")
print("== (what the sim ran) station 133, vessel 133x120 -> %s dv=%.1f m/s" % ("OK" if ok else "FAIL", dv))
