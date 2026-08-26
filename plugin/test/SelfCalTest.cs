// Tests for L6 self-calibration: the RLS-with-variable-forgetting primitive (pure/Rls.cs) and the
// concurrent estimator bank that turns tuned constants into live measurements (pure/SelfCal.cs).
using System;
using DragonScreen;

public static class SelfCalTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, got.ToString("G6") + " vs " + want.ToString("G6")); }

    public static int Run()
    {
        Console.WriteLine("DragonScreen L6 self-cal tests");

        // ============================ RLS primitive ============================
        // smoother converges to the mean of a noisy constant (mean 5.0)
        RlsScalar sm = new RlsScalar();
        double v = 0;
        for (int i = 0; i < 300; i++) v = Rls.Smooth(ref sm, (i % 2 == 0) ? 4.9 : 5.1, 1.0, 0.9, 1.0);
        Near("RLS smoother converges to the noisy mean", v, 5.0, 0.1);
        Check("RLS covariance shrinks as it settles", sm.P < 0.5, sm.P.ToString("G4"));

        // phi = 0 → the parameter is unobservable, estimate unchanged
        RlsScalar un = RlsScalar.Seed(3.0, 1.0);
        Check("phi=0 leaves the estimate unchanged", Rls.Update(ref un, 0.0, 999.0, 1.0, 0.9, 1.0) == 3.0, "");

        // regression y = phi·θ recovers θ (θ = 3), clean data
        RlsScalar rg = new RlsScalar();
        double[] phis = { 1, 2, 0.5, 3, 1.5, 4 };
        double th = 0;
        for (int i = 0; i < 60; i++) { double p = phis[i % phis.Length]; th = Rls.Update(ref rg, p, p * 3.0, 1.0, 0.9, 1.0); }
        Near("RLS regression recovers the true parameter", th, 3.0, 0.01);

        // VFF tracks a step change faster than a (near-)non-forgetting baseline
        RlsScalar vff = new RlsScalar();
        RlsScalar slow = new RlsScalar();
        for (int i = 0; i < 150; i++) { Rls.Smooth(ref vff, 5.0, 1.0, 0.9, 0.5); Rls.Smooth(ref slow, 5.0, 1.0, 0.9, 1e12); }
        double vffV = 5, slowV = 5;
        for (int i = 0; i < 100; i++) { vffV = Rls.Smooth(ref vff, 9.0, 1.0, 0.9, 0.5); slowV = Rls.Smooth(ref slow, 9.0, 1.0, 0.9, 1e12); }
        Check("VFF tracks a step faster than a slow-forgetting filter", vffV > slowV, vffV.ToString("G4") + " vs " + slowV.ToString("G4"));
        Check("VFF reaches most of the step", vffV > 8.0, vffV.ToString("G4"));

        // ============================ SELF-CAL bank ============================
        // thrust F = a·m, then track a throttle-down (VFF)
        SelfCalState sc = new SelfCalState();
        Near("thrust estimate = accel × mass", SelfCal.Thrust(ref sc, 20.0, 500000.0), 1.0e7, 1.0);
        double f = 0;
        for (int i = 0; i < 80; i++) f = SelfCal.Thrust(ref sc, 10.0, 500000.0);   // throttle to F = 5e6
        Check("thrust estimate tracks a throttle-down", f > 4.9e6 && f < 7.5e6, f.ToString("G4"));

        // ballistic coefficient from measured drag: dragAccel = q·(1/β), β = 2000
        SelfCalState scb = new SelfCalState();
        double beta = 0;
        double[] qs = { 4.0e5, 2.0e5, 3.0e5, 5.0e5 };
        for (int i = 0; i < 40; i++) { double q = qs[i % qs.Length]; beta = SelfCal.BallisticCoefficient(ref scb, q / 2000.0, q); }
        Near("ballistic coefficient recovered from drag", beta, 2000.0, 20.0);

        // control effectiveness: α = τ·(1/I), I = 1e5 → 1/I = 1e-5
        SelfCalState sci = new SelfCalState();
        double invI = 0;
        double[] taus = { 5.0e4, 3.0e4, 8.0e4, 1.0e5 };
        for (int i = 0; i < 40; i++) { double tau = taus[i % taus.Length]; invI = SelfCal.InverseInertia(ref sci, tau / 1.0e5, tau); }
        Near("inverse inertia (control effectiveness) recovered", invI, 1.0e-5, 1.0e-7);
        Near("torque needed for a desired angular accel", SelfCal.TorqueFor(sci, 0.2), 2.0e4, 200.0);

        // entry L/D smoothing (noisy 0.19/0.21 → 0.20)
        SelfCalState scl = new SelfCalState();
        double ld = 0;
        for (int i = 0; i < 60; i++) ld = SelfCal.LiftToDrag(ref scl, (i % 2 == 0) ? 0.19 : 0.21);
        Near("entry L/D smoothed to the true trim", ld, 0.20, 0.02);

        // steering sign/scale — the flipped-frame guard
        SelfCalState fresh = new SelfCalState();
        Check("steer sign defaults to +1 before any observation", SelfCal.SteerSign(fresh) == +1, "");
        SelfCalState nom = new SelfCalState();
        for (int i = 0; i < 20; i++) SelfCal.SteerResponse(ref nom, +2.0, +1.0);   // response follows command
        Check("nominal steering response → sign +1", SelfCal.SteerSign(nom) == +1, "");
        SelfCalState flip = new SelfCalState();
        for (int i = 0; i < 20; i++) SelfCal.SteerResponse(ref flip, -2.0, +1.0);  // response OPPOSES command
        Check("flipped-frame steering response → sign −1 (detected, not tuned)", SelfCal.SteerSign(flip) == -1, "");
        SelfCalState noCmd = new SelfCalState();
        SelfCal.SteerResponse(ref noCmd, 5.0, 0.0);   // no command → nothing learned
        Check("no steering command → nothing learned, sign stays nominal", SelfCal.SteerSign(noCmd) == +1, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
