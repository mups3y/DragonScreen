/*
 * DragonScreen headless tests - the ported MechJebLib foundation (docs/MECHJEBLIB_PORT.md, step 1).
 *
 * These guard the primitives the FuelFlowSimulation stands on BEFORE any of the sim proper is ported:
 *   * Statics.Clamp / EPS      - trivial, but the sim calls Clamp x27 and EPS x5
 *   * H1 (double Hermite curve) - the engine-curve evaluator; if this is wrong every Isp is a constant
 *
 * The H1 checks compare H1.Evaluate against Functions.Interpolants.CubicHermiteInterpolant directly,
 * so they prove the PLUMBING (object pool, HBase bracket search, tangent handling, Interpolant
 * dispatch, endpoint extrapolation) rather than restating the cubic - the cubic is the same function
 * on both sides on purpose.
 */
using System;
using MechJebLib.Primitives;
using MechJebLib.Utils;
using MechJebLib.Functions;

public static class MechJebLibTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen MechJebLib foundation tests");

        // ---- Statics: Clamp + EPS ----
        Check("Clamp(double) clamps high", Statics.Clamp(5.0, 0.0, 1.0) == 1.0, "");
        Check("Clamp(double) clamps low", Statics.Clamp(-1.0, 0.0, 1.0) == 0.0, "");
        Check("Clamp(double) passes interior", Statics.Clamp(0.5, 0.0, 1.0) == 0.5, "");
        Check("Clamp(int) clamps high", Statics.Clamp(9, 0, 3) == 3, "");
        Check("Clamp01 clamps", Statics.Clamp01(2.0) == 1.0 && Statics.Clamp01(-2.0) == 0.0, "");
        Check("EPS is real machine epsilon", Statics.EPS > 0.0 && (1.0 + Statics.EPS) != 1.0, "");
        Check("EPS2 is twice EPS", Statics.EPS2 == Statics.EPS * 2.0, "");

        // ---- H1: a straight line y=x (unit interval, unit tangents) is exact at the midpoint ----
        H1 line = H1.Get();
        line.Add(0.0, 0.0, 1.0, 1.0);
        line.Add(1.0, 1.0, 1.0, 1.0);
        Check("H1 straight line is exact at the midpoint",
              Math.Abs(line.Evaluate(0.5) - 0.5) < 1e-12, line.Evaluate(0.5).ToString("R"));
        line.Dispose();

        // ---- H1: three-keyframe curve, interior bracket + exact-key + endpoint extrapolation ----
        H1 h = H1.Get();
        h.Add(0.0, 0.0, 10.0, 10.0);
        h.Add(1.0, 10.0, 10.0, 10.0);
        h.Add(2.0, 20.0, 10.0, 10.0);

        // interior point in the [0,1] bracket: must match the raw cubic on that segment
        double expMid = Interpolants.CubicHermiteInterpolant(0.0, 0.0, 10.0, 1.0, 10.0, 10.0, 0.5);
        Check("H1 dispatches the correct bracket [0,1]",
              Math.Abs(h.Evaluate(0.5) - expMid) < 1e-12,
              h.Evaluate(0.5).ToString("R") + " vs " + expMid.ToString("R"));

        // interior point in the [1,2] bracket
        double expHi = Interpolants.CubicHermiteInterpolant(1.0, 10.0, 10.0, 2.0, 20.0, 10.0, 1.5);
        Check("H1 dispatches the correct bracket [1,2]",
              Math.Abs(h.Evaluate(1.5) - expHi) < 1e-12,
              h.Evaluate(1.5).ToString("R") + " vs " + expHi.ToString("R"));

        // exact interior keyframe returns the stored value regardless of tangents
        Check("H1 returns an exact interior keyframe value",
              Math.Abs(h.Evaluate(1.0) - 10.0) < 1e-12, h.Evaluate(1.0).ToString("R"));

        // below-min extrapolation uses the in-tangent: value - inTangent*(MinT - t) = 0 - 10*(0-(-1))
        Check("H1 extrapolates below min along the in-tangent",
              Math.Abs(h.Evaluate(-1.0) - (-10.0)) < 1e-9, h.Evaluate(-1.0).ToString("R"));

        // above-max extrapolation uses the out-tangent: value + outTangent*(t - MaxT) = 20 + 10*(3-2)
        Check("H1 extrapolates above max along the out-tangent",
              Math.Abs(h.Evaluate(3.0) - 30.0) < 1e-9, h.Evaluate(3.0).ToString("R"));
        h.Dispose();

        // ---- H1 UnityCompat: no extrapolation, clamp to the end values ----
        H1 u = H1.Get(true);
        u.Add(0.0, 5.0, 1.0, 1.0);
        u.Add(1.0, 7.0, 1.0, 1.0);
        Check("UnityCompat clamps below min to the first value",
              u.Evaluate(-5.0) == 5.0, u.Evaluate(-5.0).ToString("R"));
        Check("UnityCompat clamps above max to the last value",
              u.Evaluate(5.0) == 7.0, u.Evaluate(5.0).ToString("R"));
        u.Dispose();

        // ---- the object pool actually recycles (Get after Dispose returns a usable, cleared curve) ----
        H1 a = H1.Get();
        a.Add(0.0, 1.0, 0.0, 0.0);
        a.Add(1.0, 2.0, 0.0, 0.0);
        a.Dispose();
        H1 b = H1.Get();          // may be the same recycled instance
        b.Add(10.0, 100.0, 0.0, 0.0);
        b.Add(20.0, 200.0, 0.0, 0.0);
        Check("a recycled H1 evaluates its own fresh keyframes",
              Math.Abs(b.Evaluate(15.0) - 150.0) < 1e-9, b.Evaluate(15.0).ToString("R"));
        b.Dispose();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
