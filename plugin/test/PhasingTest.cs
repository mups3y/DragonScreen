// Tests for the far-field PHASE-TIMED HOHMANN transfer + the crew-safety floor (pure/Phasing.cs) and the
// CW-validity guard in Rendezvous.Guide. Two flights drove this: 214827 (CW at 13,000 km → 28 km/s retrograde
// → self-deorbit; fixed by FarField + the pe floor) and 103303 (the continuous "raise" pumped ap 200→772 km,
// never coasted, never closed; fixed by the bounded FarGuide FSM here). The far field must be prograde-or-coast
// only, STOP raising at the station altitude (no over-raise), gate every burn on the pe floor, and Guide must
// refuse to emit a CW burn far out.
using System;
using DragonScreen;

public static class PhasingTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen phasing / safety-floor tests");

        // ---- co-elliptic target = a set height BELOW the station ----
        Check("co-elliptic target is below the station", Math.Abs(Phasing.CoEllipticTargetAltM(420000, 10000) - 410000) < 1e-6, "");

        // ---- FarGuide FSM: PHASE (coast to lead angle) → TRANSFER (raise ap to station alt) → COAST (hand to CW) ----
        double mu = 3.986e14, r1 = 6.571e6, r2 = 6.790e6;        // ~200 km chaser vs ~419 km station
        double o1 = Math.Sqrt(mu / (r1 * r1 * r1)), o2 = Math.Sqrt(mu / (r2 * r2 * r2));
        double lead = Hohmann.PhaseLeadRad(r1, r2, mu);
        double tgtAlt = r2 - 6.371e6;                            // station altitude above the 6371 km body

        FarInputs fp = new FarInputs { PhaseNowRad = lead + 1.0, PhaseLeadRad = lead, Omega1 = o1, Omega2 = o2,
            ApAltM = 200000, TargetAltM = tgtAlt, RaiseTolM = 2000, PeAltM = 158000, FloorM = 150000 };
        FarCommand cp = Phasing.FarGuide(fp, FarPhase.Phase);
        Check("PHASE: not aligned → wait (no burn)", cp.Phase == FarPhase.Phase && !cp.Burn && cp.WaitS > 15,
              "wait=" + cp.WaitS.ToString("F0"));

        fp.PhaseNowRad = lead + 0.00001;                        // aligned
        FarCommand ct = Phasing.FarGuide(fp, FarPhase.Phase);
        Check("PHASE aligned → TRANSFER + burn", ct.Phase == FarPhase.Transfer && ct.Burn, "");

        FarInputs ftr = fp; ftr.ApAltM = 300000;                // in transfer, ap still below the station altitude
        FarCommand cb = Phasing.FarGuide(ftr, FarPhase.Transfer);
        Check("TRANSFER: ap below station → burn to raise ap", cb.Phase == FarPhase.Transfer && cb.Burn, "");

        // ⛔ THE OVER-RAISE FIX (flight 103303: ap 200→772): once ap reaches the station altitude, STOP → COAST.
        // (The slow near-apoapsis CIRCULARIZE was REMOVED — flight 165302: it drifted 246→6,000 km. CW does the
        // terminal rendezvous instead, once the wider hand-off catches the ~80 km transfer approach.)
        FarInputs fdone = ftr; fdone.ApAltM = tgtAlt;
        FarCommand cc = Phasing.FarGuide(fdone, FarPhase.Transfer);
        Check("TRANSFER: ap reached station → COAST, no burn (never over-raise, no slow circularize)",
              cc.Phase == FarPhase.Coast && !cc.Burn, "");

        FarInputs flow = ftr; flow.PeAltM = 149000;             // pe below the floor in transfer
        FarCommand cl = Phasing.FarGuide(flow, FarPhase.Transfer);
        Check("TRANSFER: pe below floor → burn HELD", !cl.Burn && cl.PeHeld, "");

        FarCommand cco = Phasing.FarGuide(fdone, FarPhase.Coast);
        Check("COAST: no burn, holds coast", cco.Phase == FarPhase.Coast && !cco.Burn, "");

        // ---- the hard periapsis floor ----
        Check("pe well above floor is safe", Phasing.PeSafe(178000, 150000), "");
        Check("pe below floor is UNSAFE (burns held)", !Phasing.PeSafe(149000, 150000), "");
        Check("pe exactly at floor is UNSAFE (strict)", !Phasing.PeSafe(150000, 150000), "");
        Check("the flight's decayed pe (−143 km) is UNSAFE", !Phasing.PeSafe(-143000, 150000), "");

        // ---- far/near split ----
        Check("13,322 km is FAR FIELD", Phasing.FarField(13322000, 50000), "");
        Check("40 km is NEAR FIELD (CW)", !Phasing.FarField(40000, 50000), "");
        Check("60 km is FAR FIELD", Phasing.FarField(60000, 50000), "");

        // ---- CW-validity guard in Guide: NO garbage burn far out, but still a unit aim (full control) ----
        RendezvousInputs far = new RendezvousInputs();
        far.Valid = true; far.N = 1.128e-3; far.AllNominal = true; far.AttitudeReady = true;
        far.CoEllipticBelowM = 10000; far.CoEllipticBehindM = 20000; far.AiRangeM = 7500; far.CorridorRangeM = 2000;
        far.Rel = new LvlhState { Rx = 0, Ry = -13322000, Rz = 0, Vx = 0, Vy = 0, Vz = 0 };
        RendezvousCommand fc = Rendezvous.Guide(far, RvPhase.Phasing);
        Check("Guide emits NO burn beyond CW's valid range", !fc.Burn && fc.BurnDvMps < 1e-9,
              "Burn=" + fc.Burn + " dv=" + fc.BurnDvMps.ToString("F1"));
        Check("...but still holds a unit aim (never floating)", Math.Abs(fc.AimLvlh.Magnitude - 1.0) < 1e-6, "");

        // ---- near field still computes a CW burn (regression guard for the existing terminal legs) ----
        RendezvousInputs near = far; near.Rel = new LvlhState { Rx = -2000, Ry = -7000, Rz = 0 };
        RendezvousCommand nc = Rendezvous.Guide(near, RvPhase.CoElliptic);
        Check("near field still computes a CW burn", nc.BurnDvMps > 0.0, nc.BurnDvMps.ToString("F3"));

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
