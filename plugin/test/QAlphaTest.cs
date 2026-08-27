// Tests for pure/QAlpha.cs (B2 q·α moderation) + the SelfCal.AeroPitchStiffness online estimator.
// The controllability cap α_max = factor·aCtrlMax/|kAero| must tighten as q (kAero) rises, be smaller when
// statically unstable, vanish (no cap) below the q-gate, and floor to keep steering authority. The estimator
// must recover a known aero stiffness from (AoA, aero-angular-accel) samples.
using System;
using DragonScreen;

public static class QAlphaTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, "got " + got.ToString("F5") + " want " + want.ToString("F5")); }

    public static int Run()
    {
        Console.WriteLine("DragonScreen B2 q-alpha moderation tests");
        double aCtrl = 0.1;   // rad/s² of control authority

        // ---- the q-seed scales with dynamic pressure ----
        Near("kAero seed = coeff*q", QAlpha.AeroStiffnessSeed(15000.0), QAlpha.StiffnessSeedPerPa * 15000.0, 1e-9);

        // ---- explicit sizing check: unstable cap ≈ MinAoa (5°) at the calibration point ----
        double kCal = QAlpha.AeroStiffnessSeed(15000.0);            // ≈ 0.57 /s²
        QAlphaLimit cal = QAlpha.Limit(kCal, aCtrl, false, 15000.0);
        Near("cap ≈ 5° at 15 kPa, unstable, 0.1 rad/s²", cal.AoaMaxRad, 5.0 * Math.PI / 180.0, 0.01);

        // ---- cap TIGHTENS as q rises (kAero grows) ----
        double capLoQ = QAlpha.Limit(QAlpha.AeroStiffnessSeed(8000.0), aCtrl, false, 8000.0).AoaMaxRad;
        double capHiQ = QAlpha.Limit(QAlpha.AeroStiffnessSeed(30000.0), aCtrl, false, 30000.0).AoaMaxRad;
        Check("higher q → smaller AoA cap", capHiQ < capLoQ, "loQ=" + capLoQ.ToString("F4") + " hiQ=" + capHiQ.ToString("F4"));

        // ---- unstable holds INSIDE the stable limit (factor 0.5 vs 1.0) ----
        double kk = 0.6;
        double capStable = QAlpha.Limit(kk, aCtrl, true, 20000.0).AoaMaxRad;
        double capUnstable = QAlpha.Limit(kk, aCtrl, false, 20000.0).AoaMaxRad;
        Near("unstable cap = UnstableFactor/StableFactor × stable cap",
             capUnstable, capStable * (QAlpha.UnstableFactor / QAlpha.StableFactor), 1e-9);
        Check("unstable cap < stable cap", capUnstable < capStable, "");

        // ---- gates & degenerate cases ----
        Check("below q-gate → no cap (+∞)", double.IsPositiveInfinity(QAlpha.Limit(kk, aCtrl, false, 500.0).AoaMaxRad), "");
        Check("no aero (kAero≈0) → no cap (+∞)", double.IsPositiveInfinity(QAlpha.Limit(0.0, aCtrl, false, 30000.0).AoaMaxRad), "");
        Check("no authority → hold zero AoA", QAlpha.Limit(kk, 0.0, false, 30000.0).AoaMaxRad == 0.0, "");

        // ---- clamp ----
        Near("clamp: within → unchanged", QAlpha.Clamp(0.03, 0.087), 0.03, 1e-9);
        Near("clamp: over → +max", QAlpha.Clamp(0.20, 0.087), 0.087, 1e-9);
        Near("clamp: under → −max", QAlpha.Clamp(-0.20, 0.087), -0.087, 1e-9);
        Near("clamp: +∞ → passthrough", QAlpha.Clamp(0.5, double.PositiveInfinity), 0.5, 1e-9);

        // ---- effective cap composed with steering floor + guidance ceiling ----
        double floor = 5.0 * Math.PI / 180.0, ceil = 8.0 * Math.PI / 180.0;
        Near("effective: physics inside band → itself", QAlpha.EffectiveCapRad(6.5 * Math.PI / 180.0, floor, ceil), 6.5 * Math.PI / 180.0, 1e-9);
        Near("effective: physics below floor → floor (keep steering)", QAlpha.EffectiveCapRad(0.5 * Math.PI / 180.0, floor, ceil), floor, 1e-9);
        Near("effective: physics above ceil → ceil", QAlpha.EffectiveCapRad(20.0 * Math.PI / 180.0, floor, ceil), ceil, 1e-9);

        // ---- SelfCal online estimator recovers a known kAero from (AoA, aero-accel) samples ----
        SelfCalState s = new SelfCalState();
        double kTrue = 0.6;
        var rng = new Random(12345);
        for (int i = 0; i < 60; i++)
        {
            double aoa = 0.02 + 0.08 * rng.NextDouble();          // 0.02..0.10 rad
            double accel = kTrue * aoa;                           // aero angular accel = kAero·AoA
            SelfCal.AeroPitchStiffness(ref s, accel, aoa);
        }
        Near("SelfCal AeroPitchStiffness → kAero", s.AeroStiff.Theta, kTrue, 0.03);
        double before = s.AeroStiff.Theta;
        Check("AoA≈0 → estimator unchanged", SelfCal.AeroPitchStiffness(ref s, 0.0, 0.0) == before, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
