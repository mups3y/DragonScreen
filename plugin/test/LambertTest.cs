// Tests for pure/Lambert.cs + pure/Maneuver.cs (B7). The decisive Lambert check is SELF-INVERSION against our
// own propagator: take (r1, v1) on a known orbit, propagate to r2 over tof with Conic.Propagate, then Lambert
// must recover v1 (and v2). Plus the finite-burn timing + the Lambert-based intercept Δv.
using System;
using DragonScreen;

public static class LambertTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }
    static void Near(string what, double got, double want, double tol)
    { checks++; if (Math.Abs(got - want) > tol) { failures++; Console.WriteLine("  FAIL  " + what + "   got " + got.ToString("F4") + " want " + want.ToString("F4")); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen B7 Lambert + Maneuver tests");
        double mu = 3.986004e14;                 // Earth
        double r = 6771000.0;                    // 400 km circular
        double vc = Math.Sqrt(mu / r);           // circular speed
        double period = 2.0 * Math.PI * Math.Sqrt(r * r * r / mu);

        // ---- self-inversion on a circular orbit (short way, ~108° transfer) ----
        {
            Vec3 r1 = new Vec3(r, 0, 0), v1t = new Vec3(0, vc, 0);
            double tof = 0.3 * period;
            Vec3 r2, v2t; bool ok = Conic.Propagate(r1, v1t, mu, tof, out r2, out v2t);
            Check("propagation set up the target point", ok, "");
            LambertSolution s = Lambert.Solve(r1, r2, tof, mu, true);
            Check("Lambert converged", s.Ok, "iters=" + s.Iterations);
            Near("Lambert recovers v1 (circular)", (s.V1 - v1t).Magnitude, 0.0, 0.5);   // m/s
            Near("Lambert recovers v2 (circular)", (s.V2 - v2t).Magnitude, 0.0, 0.5);
        }

        // ---- self-inversion on an ELLIPTICAL transfer (v1 = 1.15× circular, radial-out component) ----
        {
            Vec3 r1 = new Vec3(r, 0, 0), v1t = new Vec3(60.0, 1.12 * vc, 0);   // eccentric, prograde+radial
            double tof = 0.25 * period;
            Vec3 r2, v2t; bool ok = Conic.Propagate(r1, v1t, mu, tof, out r2, out v2t);
            LambertSolution s = Lambert.Solve(r1, r2, tof, mu, true);
            Check("elliptical: Lambert converged", s.Ok, "iters=" + s.Iterations);
            Near("elliptical: recovers v1", (s.V1 - v1t).Magnitude, 0.0, 0.8);
            // round-trip: propagate the Lambert v1 for tof → reach r2
            Vec3 rr, vv; Conic.Propagate(r1, s.V1, mu, tof, out rr, out vv);
            Near("round-trip: v1 propagates back to r2", (rr - r2).Magnitude, 0.0, 50.0);   // metres
        }

        // ---- degenerate 180° geometry (r2 opposite r1) → no solution ----
        {
            Vec3 r1 = new Vec3(r, 0, 0), r2 = new Vec3(-r, 0, 0);
            LambertSolution s = Lambert.Solve(r1, r2, 0.5 * period, mu, true);
            Check("180° transfer geometry → Lambert reports no solution", !s.Ok, "");
        }

        // ---- Maneuver: finite-burn duration (rocket equation) ----
        {
            double dv = 100.0, thrust = 2000.0, isp = 300.0, mass = 10000.0;
            double ve = isp * 9.80665;
            double m1 = mass * Math.Exp(-dv / ve), expect = (mass - m1) * ve / thrust;
            Near("BurnTimeS matches the rocket equation", Maneuver.BurnTimeS(dv, thrust, isp, mass), expect, 1e-6);
            Near("center-of-burn lead is half the burn", Maneuver.CenterOfBurnLeadS(491.0), 245.5, 1e-6);
            Check("zero dv → zero burn", Maneuver.BurnTimeS(0.0, thrust, isp, mass) == 0.0, "");
        }

        // ---- Maneuver.InterceptDv: on the transfer orbit already → ~zero burn; off it → the corrective Δv ----
        {
            Vec3 r1 = new Vec3(r, 0, 0), v1t = new Vec3(0, vc, 0);
            double tof = 0.3 * period;
            Vec3 r2, v2t; Conic.Propagate(r1, v1t, mu, tof, out r2, out v2t);
            bool ok;
            Vec3 dvOnTrack = Maneuver.InterceptDv(r1, v1t, r2, tof, mu, true, out ok);
            Check("intercept: solved", ok, "");
            Near("already on the transfer → ~zero intercept dv", dvOnTrack.Magnitude, 0.0, 0.5);
            Vec3 slow = new Vec3(0, 0.9 * vc, 0);
            Vec3 dvOff = Maneuver.InterceptDv(r1, slow, r2, tof, mu, true, out ok);
            Check("slower chaser → a real prograde intercept dv", dvOff.Magnitude > 50.0, "dv=" + dvOff.Magnitude.ToString("F1"));
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
