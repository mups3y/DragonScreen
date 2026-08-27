// Tests for pure/StageStats.cs (B1) — the per-stage Δv/TWR/burn-time budget + the MECO recovery reserve.
// Checked against hand-computed rocket-equation cases (ve = Isp·g0; ΔV = ve·ln(m0/m1)) and a round-trip
// through the inverse (PropMassForDeltaV must undo Compute's ΔV).
using System;
using DragonScreen;

public static class StageStatsTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, "got " + got.ToString("F4") + " want " + want.ToString("F4")); }

    public static int Run()
    {
        Console.WriteLine("DragonScreen B1 StageStats tests");
        const double g0 = 9.80665;

        // ---- Compute: Isp 300 (ve = 2941.995), m0 100 t, burn 50 t, F 900 kN, ref g = g0 ----
        double isp = 300.0, ve = isp * g0;
        StageStat s = StageStats.Compute(100000.0, 50000.0, 900000.0, isp, g0);
        Near("ve = Isp*g0", s.VeMps, 2941.995, 0.01);
        Near("end mass = start - prop", s.EndMassKg, 50000.0, 1e-6);
        Near("dV = ve*ln(m0/m1)", s.DeltaVMps, ve * Math.Log(2.0), 0.01);        // ~2039.35 m/s
        Near("burn = mprop*ve/F", s.BurnTimeS, 50000.0 * ve / 900000.0, 0.01);   // ~163.44 s
        Near("start TWR = F/(m0*g)", s.StartTwr, 900000.0 / (100000.0 * g0), 1e-4); // ~0.9177
        Near("end TWR = F/(m1*g)", s.EndTwr, 900000.0 / (50000.0 * g0), 1e-4);      // ~1.835

        // ---- RemainingDeltaV: from 75 t down to 50 t dry ----
        Near("remaining dV = ve*ln(75/50)", StageStats.RemainingDeltaV(75000.0, 50000.0, isp),
             ve * Math.Log(1.5), 0.01);                                          // ~1192.9 m/s

        // ---- inverse round-trip: prop for a given dV must reproduce the burn that made that dV ----
        double dvFull = s.DeltaVMps;
        Near("PropMassForDeltaV inverts Compute", StageStats.PropMassForDeltaV(50000.0, dvFull, isp),
             50000.0, 1.0);

        // ---- recovery-reserve gate ----
        // at 75 t (dry 50 t) the booster has ~1192.9 m/s left:
        Check("reserve OK when remaining >= req+margin",
              StageStats.HasRecoveryReserve(75000.0, 50000.0, isp, 1000.0, 100.0), "");   // 1192.9 >= 1100
        Check("reserve FAILS when remaining < req+margin",
              !StageStats.HasRecoveryReserve(75000.0, 50000.0, isp, 1150.0, 100.0), "");  // 1192.9 < 1250

        // ---- total dV over ordered stages ----
        StageStat[] two = { s, StageStats.Compute(40000.0, 20000.0, 200000.0, 340.0, g0) };
        Near("total dV = sum of stage dV", StageStats.TotalDeltaV(two), s.DeltaVMps + two[1].DeltaVMps, 1e-6);

        // ---- degenerate guards return a guarded zero, never NaN ----
        Check("no propellant -> dV 0", StageStats.Compute(100000.0, 0.0, 900000.0, isp, g0).DeltaVMps == 0.0, "");
        Check("prop >= start mass -> dV 0", StageStats.Compute(50000.0, 60000.0, 900000.0, isp, g0).DeltaVMps == 0.0, "");
        Check("zero Isp -> dV 0", StageStats.Compute(100000.0, 50000.0, 900000.0, 0.0, g0).DeltaVMps == 0.0, "");
        Check("RemainingDeltaV current<=dry -> 0", StageStats.RemainingDeltaV(50000.0, 50000.0, isp) == 0.0, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
