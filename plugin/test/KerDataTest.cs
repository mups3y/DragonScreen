// Tests for pure/KerData.cs — the stage-selection logic over the mirrored Kerbal Engineer data. The reflection
// reader (src/KerBridge.cs) needs the game; THIS logic (which stage is current/final, remaining Δv, reserve) is
// pure and headless-tested with synthetic stages, and the empty-array path (KER absent) must degrade cleanly.
using System;
using DragonScreen;

public static class KerDataTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }

    static KerStage S(int number, double dv, double totalDv)
    {
        return new KerStage { Number = number, DeltaVMps = dv, TotalDeltaVMps = totalDv, Valid = true };
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen KerData tests");

        // ---- empty (KER absent) → clean fallback ----
        Check("empty: Current is invalid", !KerData.Current(null).Valid && !KerData.Current(new KerStage[0]).Valid, "");
        Check("empty: remaining Δv is 0", KerData.RemainingDeltaV(null) == 0.0, "");
        Check("empty: reserve fails (no data)", !KerData.HasRecoveryReserve(null, 100.0), "");

        // ---- a 3-stage vehicle: stage 2 burning now (highest number), stage 0 the final ----
        // number: 0 = final (e.g. the S2/deorbit), 2 = current (booster). Total Δv accumulates from this stage down.
        KerStage[] v = {
            S(0, 4200, 4200),   // final stage alone: 4200
            S(1, 1500, 5700),   // stage 1 + below
            S(2, 1300, 7000),   // CURRENT: 1300 this stage, 7000 remaining from here down
        };
        Check("Current = the highest-numbered (currently burning) stage", KerData.Current(v).Number == 2, KerData.Current(v).Number.ToString());
        Check("Final = the lowest-numbered (last-to-burn) stage", KerData.Final(v).Number == 0, KerData.Final(v).Number.ToString());
        Check("remaining Δv = current stage's cumulative total", Math.Abs(KerData.RemainingDeltaV(v) - 7000.0) < 1e-9, KerData.RemainingDeltaV(v).ToString("F0"));
        Check("this-stage Δv is distinct from remaining", Math.Abs(KerData.Current(v).DeltaVMps - 1300.0) < 1e-9, "");

        // ---- reserve check ----
        Check("reserve holds when remaining exceeds it", KerData.HasRecoveryReserve(v, 5000.0), "");
        Check("reserve fails when remaining is below it", !KerData.HasRecoveryReserve(v, 8000.0), "");

        // ---- single stage ----
        KerStage[] one = { S(0, 3000, 3000) };
        Check("single stage: current == final", KerData.Current(one).Number == 0 && KerData.Final(one).Number == 0, "");
        Check("single stage: remaining = its Δv", Math.Abs(KerData.RemainingDeltaV(one) - 3000.0) < 1e-9, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
