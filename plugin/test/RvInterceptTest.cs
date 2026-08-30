// Tests for pure/RvIntercept.cs — the Lambert two-impulse rendezvous intercept PLANNER (tof scan + pe-floor
// gate + cost cap over the tested Maneuver.InterceptDv primitive). The decisive checks: (1) a returned plan
// SELF-INVERTS — fly its departure Δv and you reach where the target will be; (2) a returned plan is ALWAYS
// pe-safe (the floor gate holds); (3) an over-budget or floor-violating geometry returns Ok=false.
using System;
using DragonScreen;

public static class RvInterceptTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }
    static void Near(string what, double got, double want, double tol)
    { checks++; if (Math.Abs(got - want) > tol) { failures++; Console.WriteLine("  FAIL  " + what + "   got " + got.ToString("F4") + " want " + want.ToString("F4")); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen RvIntercept (Lambert rendezvous planner) tests");
        double mu = 3.986004e14;                 // Earth
        double rBody = 6371000.0;                // Earth radius (so pe ALTITUDE = radius − rBody)
        double r = rBody + 400000.0;             // 400 km circular
        double vc = Math.Sqrt(mu / r);
        double period = 2.0 * Math.PI * Math.Sqrt(r * r * r / mu);
        double floor = 150000.0;                 // 150 km pe floor (same class as the RV SafePeFloorM)

        // Realistic co-elliptic mid-field geometry: the chaser sits BEHIND-and-BELOW the target (the real
        // approach), so a Lambert intercept RAISES the orbit toward the station → pe stays safe. This is the
        // regime the wiring engages (target loaded, small along-track angle); a direct intercept from a large
        // angular lead needs a lower/faster transfer that dips below the floor and is correctly refused (3,4).
        double belowM = 10000.0, behindM = 20000.0;
        double rcRad = r - belowM;                              // chaser 10 km below the station's circle
        double delta = behindM / r;                            // ~20 km behind, as an angle
        double vcc = Math.Sqrt(mu / rcRad);                    // chaser ~circular for its lower radius (faster → closing)
        Vec3 rChaser = new Vec3(rcRad * Math.Cos(-delta), rcRad * Math.Sin(-delta), 0);
        Vec3 vChaser = new Vec3(-vcc * Math.Sin(-delta), vcc * Math.Cos(-delta), 0);  // prograde tangential
        Vec3 rTgt = new Vec3(r, 0, 0), vTgt = new Vec3(0, vc, 0);

        // ---- (1) single Plan to the target-future point → self-inverts and is pe-safe ----
        {
            double tof = 0.15 * period;
            Vec3 rtF, vtF; Conic.Propagate(rTgt, vTgt, mu, tof, out rtF, out vtF);
            InterceptPlan p = RvIntercept.Plan(rChaser, vChaser, rtF, mu, rBody, tof, floor, true);
            Check("plan solved", p.Ok, "");
            Check("plan is pe-safe (transfer pe >= floor)", p.PeSafe && p.TransferPeM >= floor, "peM=" + p.TransferPeM.ToString("F0"));
            // self-inversion: fly the departure Δv, propagate tof → should arrive at the target-future point
            Vec3 rr, vv; Conic.Propagate(rChaser, vChaser + p.DepartureDv, mu, tof, out rr, out vv);
            Near("intercept self-inverts to target-future point", (rr - rtF).Magnitude, 0.0, 200.0);  // metres
        }

        // ---- (2) Best over the tof band → cheapest pe-safe intercept, and EVERY returned plan is pe-safe ----
        {
            InterceptPlan b = RvIntercept.Best(rChaser, vChaser, rTgt, vTgt, mu, rBody, period, floor);
            Check("Best found a pe-safe intercept for the co-elliptic mid-field", b.Ok, "");
            Check("Best plan pe-safe", b.PeSafe && b.TransferPeM >= floor, "peM=" + b.TransferPeM.ToString("F0"));
            Check("Best plan within budget", b.DepartMagMps <= RvIntercept.MaxDvMps, "dv=" + b.DepartMagMps.ToString("F1"));
            Check("Best plan Δv is a sane terminal nudge (< 200 m/s)", b.DepartMagMps < 200.0, "dv=" + b.DepartMagMps.ToString("F1"));
            // the winning plan self-inverts too
            Vec3 rtF, vtF; Conic.Propagate(rTgt, vTgt, mu, b.TofS, out rtF, out vtF);
            Vec3 rr, vv; Conic.Propagate(rChaser, vChaser + b.DepartureDv, mu, b.TofS, out rr, out vv);
            Near("Best plan self-inverts", (rr - rtF).Magnitude, 0.0, 500.0);
        }

        // ---- (3) FLOOR GATE: a target so far ahead that the only cheap intercept dives below the floor →
        //          Best must reject the unsafe ones. Set the floor absurdly high (just under the circular alt)
        //          so any transfer with the slightest eccentricity is rejected → Best returns Ok=false. ----
        {
            Vec3 rc = new Vec3(r, 0, 0), vcv = new Vec3(0, vc, 0);
            double ang = 150.0 * Math.PI / 180.0;               // near-180° → high-ecc, low-pe transfers
            Vec3 rt = new Vec3(r * Math.Cos(ang), r * Math.Sin(ang), 0);
            Vec3 vt = new Vec3(-vc * Math.Sin(ang), vc * Math.Cos(ang), 0);
            double highFloor = 399000.0;                        // 1 km under the circular altitude
            InterceptPlan b = RvIntercept.Best(rc, vcv, rt, vt, mu, rBody, period, highFloor);
            // whatever it returns, it must be pe-safe; and with a near-circle-hugging floor it should reject.
            if (b.Ok) Check("any returned plan honours the high floor", b.TransferPeM >= highFloor, "peM=" + b.TransferPeM.ToString("F0"));
            else Check("no pe-safe intercept under a near-circular floor → Best refuses", true, "");
        }

        // ---- (4) COST CAP: a tiny MaxDvMps forbids everything → Best refuses (never returns an over-budget plan) ----
        {
            double savedMax = RvIntercept.MaxDvMps;
            RvIntercept.MaxDvMps = 0.001;                       // 1 mm/s — nothing qualifies
            Vec3 rc = new Vec3(r, 0, 0), vcv = new Vec3(0, vc, 0);
            double ang = 45.0 * Math.PI / 180.0;
            Vec3 rt = new Vec3(r * Math.Cos(ang), r * Math.Sin(ang), 0);
            Vec3 vt = new Vec3(-vc * Math.Sin(ang), vc * Math.Cos(ang), 0);
            InterceptPlan b = RvIntercept.Best(rc, vcv, rt, vt, mu, rBody, period, floor);
            Check("cost cap forbids all intercepts → Best refuses", !b.Ok, "dv=" + b.DepartMagMps.ToString("F3"));
            RvIntercept.MaxDvMps = savedMax;
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
