// Tests for pure/CourseCorrect.cs (B8). The decisive check: against a KNOWN LINEAR impact model the
// finite-difference solve must recover the exact divert that nulls the miss (with damping = 1), and with the
// default damping it must leave exactly the residual fraction. Plus observability/rank refusals and the 1×1.
using System;
using DragonScreen;

public static class CourseCorrectTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }
    static void Near(string what, double got, double want, double tol)
    { checks++; if (Math.Abs(got - want) > tol) { failures++; Console.WriteLine("  FAIL  " + what + "   got " + got.ToString("F5") + " want " + want.ToString("F5")); } }

    // a linear impact model: down = D0 + Kd1*u1 + Kd2*u2 ; cross = C0 + Kc1*u1 + Kc2*u2
    static ImpactError Model(double D0, double C0, double Kd1, double Kd2, double Kc1, double Kc2, double u1, double u2)
        => new ImpactError(D0 + Kd1 * u1 + Kd2 * u2, C0 + Kc1 * u1 + Kc2 * u2);

    public static int Run()
    {
        Console.WriteLine("DragonScreen B8 CourseCorrect tests");
        double savedGain = CourseCorrect.DampingGain;

        // ---- decoupled linear model: axis1→downrange, axis2→crossrange ----
        {
            CourseCorrect.DampingGain = 1.0;   // full Newton step for the exact-recovery check
            double D0 = 800, C0 = -300, Kd1 = -50, Kd2 = 0, Kc1 = 0, Kc2 = 40, du1 = 1, du2 = 1;
            ImpactError e0 = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, 0, 0);
            ImpactError e1 = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, du1, 0);
            ImpactError e2 = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, 0, du2);
            DivertResult r = CourseCorrect.Solve2x2(e0, e1, e2, du1, du2, 1e9);
            Check("decoupled: solve ok", r.Ok, "");
            // apply the divert to the model → miss should be nulled
            ImpactError res = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, r.Du1, r.Du2);
            Near("decoupled: downrange nulled", res.DownrangeM, 0.0, 1e-6);
            Near("decoupled: crossrange nulled", res.CrossrangeM, 0.0, 1e-6);
            Near("decoupled: det = Kd1*Kc2", r.Det, Kd1 * Kc2, 1e-9);
        }

        // ---- fully COUPLED linear model (off-diagonal sensitivity) ----
        {
            CourseCorrect.DampingGain = 1.0;
            double D0 = 1200, C0 = 500, Kd1 = -30, Kd2 = 12, Kc1 = 8, Kc2 = -45, du1 = 0.5, du2 = 0.5;
            ImpactError e0 = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, 0, 0);
            ImpactError e1 = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, du1, 0);
            ImpactError e2 = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, 0, du2);
            DivertResult r = CourseCorrect.Solve2x2(e0, e1, e2, du1, du2, 1e9);
            Check("coupled: solve ok", r.Ok, "");
            ImpactError res = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, r.Du1, r.Du2);
            Near("coupled: downrange nulled", res.DownrangeM, 0.0, 1e-6);
            Near("coupled: crossrange nulled", res.CrossrangeM, 0.0, 1e-6);
        }

        // ---- default damping leaves exactly (1 − gain) of the miss ----
        {
            CourseCorrect.DampingGain = 0.7;
            double D0 = 1000, C0 = 0, Kd1 = -50, Kd2 = 0, Kc1 = 0, Kc2 = 50, du1 = 1, du2 = 1;
            ImpactError e0 = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, 0, 0);
            ImpactError e1 = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, du1, 0);
            ImpactError e2 = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, 0, du2);
            DivertResult r = CourseCorrect.Solve2x2(e0, e1, e2, du1, du2, 1e9);
            ImpactError res = Model(D0, C0, Kd1, Kd2, Kc1, Kc2, r.Du1, r.Du2);
            Near("damped: 30% of the downrange miss remains", res.DownrangeM, 0.3 * D0, 1e-6);
        }

        // ---- rank-deficient Jacobian (both axes push the impact the SAME direction) → refuse ----
        {
            CourseCorrect.DampingGain = 1.0;
            // both perturbations move impact along (down,cross)=(−50,+50): parallel columns
            double D0 = 400, C0 = 400;
            ImpactError e0 = new ImpactError(D0, C0);
            ImpactError e1 = new ImpactError(D0 - 50, C0 + 50);
            ImpactError e2 = new ImpactError(D0 - 100, C0 + 100);   // = 2× axis1 direction
            DivertResult r = CourseCorrect.Solve2x2(e0, e1, e2, 1, 1, 1e9);
            Check("rank-deficient (parallel columns) → refuse", !r.Ok, "det=" + r.Det.ToString("F3"));
        }

        // ---- unobservable: a perturbation that barely moves the impact → refuse ----
        {
            ImpactError e0 = new ImpactError(600, 0);
            ImpactError e1 = new ImpactError(600.2, 0);   // < MinSensitivityM (1 m)
            ImpactError e2 = new ImpactError(600, 40);
            DivertResult r = CourseCorrect.Solve2x2(e0, e1, e2, 1, 1, 1e9);
            Check("unobservable axis → refuse", !r.Ok, "");
        }

        // ---- zero perturbation → refuse (nothing measured) ----
        {
            ImpactError e0 = new ImpactError(600, 100);
            DivertResult r = CourseCorrect.Solve2x2(e0, e0, e0, 0, 0, 1e9);
            Check("zero perturbation → refuse", !r.Ok, "");
        }

        // ---- clamp a single correction to MaxStep ----
        {
            CourseCorrect.DampingGain = 1.0;
            double D0 = 100000, Kd1 = -1;   // huge miss, weak sensitivity → big raw step
            ImpactError e0 = new ImpactError(D0, 0);
            ImpactError e1 = new ImpactError(D0 + Kd1 * 1, 0);
            ImpactError e2 = new ImpactError(D0, 40);
            DivertResult r = CourseCorrect.Solve2x2(e0, e1, e2, 1, 1, 5.0);
            Check("clamp: |Du1| ≤ maxStep", Math.Abs(r.Du1) <= 5.0 + 1e-9, "Du1=" + r.Du1.ToString("F3"));
        }

        // ---- 1×1 Newton (entry range channel): recovers −err0/slope, nulls the miss (gain 1) ----
        {
            CourseCorrect.DampingGain = 1.0;
            double D0 = 900, K = -60, du = 1;   // down = D0 + K*u
            double down0 = D0, down1 = D0 + K * du;
            DivertResult r = CourseCorrect.Solve1x1(down0, down1, du, 1e9);
            Check("1x1: solve ok", r.Ok, "");
            double res = D0 + K * r.Du1;
            Near("1x1: downrange nulled", res, 0.0, 1e-6);
            Near("1x1: slope reported", r.Det, K, 1e-9);
        }

        CourseCorrect.DampingGain = savedGain;   // ⛔ restore the shared tunable
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
