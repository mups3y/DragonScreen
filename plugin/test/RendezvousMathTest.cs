// Tests for the rendezvous math: LVLH frame (pure/Lvlh.cs), Clohessy-Wiltshire two-impulse targeting
// (pure/Cw.cs), and Hohmann phasing (pure/Hohmann.cs). CW is asserted by its own self-consistency: the
// transfer it solves, propagated by the SAME free-drift dynamics, must reach the aim point.
using System;
using DragonScreen;

public static class RendezvousMathTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, got.ToString("F3") + " vs " + want.ToString("F3")); }

    const double Mu = 3.986004418e14;

    public static int Run()
    {
        Console.WriteLine("DragonScreen rendezvous-math tests");

        // ---- LVLH frame ----
        double a = 6791000.0;                       // ~420 km circular
        double vc = Math.Sqrt(Mu / a);
        double n = Math.Sqrt(Mu / (a * a * a));
        Vec3 tR = new Vec3(a, 0, 0), tV = new Vec3(0, vc, 0);

        // a co-orbiting body (along-track offset, moving with the frame) reads ZERO LVLH velocity.
        double d = 5000.0;
        Vec3 relPos = new Vec3(0, d, 0);            // +along (ahead)
        Vec3 relVel = new Vec3(-n * d, 0, 0);       // its inertial relative velocity = ω×r
        LvlhState co = Lvlh.Project(tR, tV, relPos, relVel, n);
        Near("along-track offset reads +along", co.Ry, d, 1.0);
        Check("a co-orbiting body has ~zero LVLH velocity", Math.Abs(co.Vx) < 1e-6 && Math.Abs(co.Vy) < 1e-6, co.Vx + "/" + co.Vy);
        Vec3 below = Lvlh.OffsetToWorld(tR, tV, -400.0, 0, 0);   // 400 m below = −radial
        Near("400 m below is −radial in world (−x here)", below.X, -400.0, 1e-6);

        // ---- CW: the solved two-impulse transfer's free-drift reaches the aim ----
        // chaser 10 km below, 20 km behind, at rest in the frame; aim an OFFSET 500 m behind on the V-bar.
        double x0 = -10000, y0 = -20000, z0 = 0, vx0 = 0, vy0 = 0, vz0 = 0;
        double xf = 0, yf = -500, zf = 0, tof = 1800.0;
        CwSolution sol = Cw.TwoImpulse(x0, y0, z0, vx0, vy0, vz0, xf, yf, zf, n, tof);
        Check("the two-impulse solve succeeds", sol.Ok, "");
        double dx, dy, dz;
        Cw.FreeDrift(x0, y0, z0, sol.Vx1, sol.Vy1, sol.Vz1, n, tof, out dx, out dy, out dz);
        Near("...its free-drift reaches the aim (x)", dx, xf, 1.0);
        Near("...its free-drift reaches the aim (y)", dy, yf, 1.0);
        Check("a station-keeping transfer costs a sane Δv (< 50 m/s)", Cw.TotalDv(sol) < 50.0, Cw.TotalDv(sol).ToString("F2"));

        // free-drift identity at t=0
        double ix, iy, iz;
        Cw.FreeDrift(x0, y0, z0, 1.0, 2.0, 3.0, n, 0.0, out ix, out iy, out iz);
        Check("free-drift at t=0 returns r0", Math.Abs(ix - x0) < 1e-6 && Math.Abs(iy - y0) < 1e-6, "");

        // passive-abort: a drift aimed at the station gets a SMALL min range; an along-track offset stays clear.
        double minToward = Cw.FreeDriftMinRangeM(-5000, 0, 0, 5.0, 0, 0, n, 400, 2.0 * Math.PI / n);
        Check("a drift toward the station reports a small min range", minToward < 5000.0, minToward.ToString("F0"));

        // ---- Hohmann ----
        double r1 = 6571000.0, r2 = 6791000.0;      // 200 km → 420 km
        Near("circular speed at 200 km ≈ 7788", Hohmann.CircularSpeed(r1, Mu), 7788.0, 30.0);
        Near("circular speed at 420 km ≈ 7659", Hohmann.CircularSpeed(r2, Mu), 7659.0, 30.0);
        Check("raise burn is prograde", Hohmann.Dv1(r1, r2, Mu) > 0.0, "");
        Near("...≈ 60 m/s for a 220 km climb", Hohmann.Dv1(r1, r2, Mu), 60.0, 25.0);
        Near("circularise ≈ 60 m/s", Hohmann.Dv2(r1, r2, Mu), 60.0, 25.0);
        Near("total 200→420 ≈ 120 m/s", Hohmann.Total(r1, r2, Mu), 120.0, 40.0);
        Near("transfer time ≈ 45 min", Hohmann.TransferTimeS(r1, r2, Mu) / 60.0, 45.3, 1.0);
        Check("lowering is retrograde", Hohmann.Dv1(r2, r1, Mu) < 0.0, "");
        double lead = Hohmann.PhaseLeadRad(r1, r2, Mu);
        Check("phase-lead angle is in [0,2π)", lead >= 0.0 && lead < 2.0 * Math.PI, lead.ToString("F3"));

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
