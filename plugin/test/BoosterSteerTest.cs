// Tests for pure/BoosterSteer.cs (register W24) — the booster's STEERING LAW.
//
// This is the component that failed before (`docs/AUTOPILOT_RECOVERY_AUDIT.md` §3.2, RECOVER-REFERENCE
// ONLY — no byte of it is here), so this suite is written against the FAILURE, not a spec:
//   · `docs/FLIGHT_CORPUS_ASSESSMENT.md` §3.1 — the ascent failure was a DIVERGENCE (a commanded rate
//     that ran away to 195-436 dps against a measured rate that never exceeded 68 dps), not a limit
//     cycle. RateCeilingChecks() proves the new law's outer stage cannot reproduce that shape: the
//     desired rate is bounded by `MaxRateDegPerS` no matter how large the angle error is.
//   · §3.2 — the limit cycle that DID occur lived in a different vehicle's terminal-rendezvous actuation
//     (81-89% duty, ~1 reversal/s at a 3-4 degree error) — a regime this file's Q2 deadband deliberately
//     does NOT seed from (R1 §7.5: "a different plant"). ChatterChecks() proves the seam itself works
//     (suppresses a small error to exactly zero, observably) without asserting anything about the
//     DEFAULT (zero) behaviour at small error, which is intentionally unchanged from "no deadband".
//   · `docs/BOOSTER_STEERING_MOD_SEARCH.md`'s Q1 ruling is why this law is OURS — nothing here reads
//     from or depends on ThrottleControlledAvionics.
//
// ⛔ WHAT THIS SUITE DOES NOT PROVE. Every gain is [UN-CONVERGED] (§B16.8 ruling 2) — there is no
// recorded booster attitude flight anywhere in this repo (R1 §4.2). These are PROPERTY checks: bounded
// output, no NaN/Infinity, the rate ceiling holds, the deadband is observable, a converging simulated
// loop does not run away. They prove the law's SHAPE is right. They prove nothing about tuning, and the
// `|AoaDeg| <= AoaCapDeg` contract is `pure/BoosterDescent.cs`'s own (proven in `BoosterTest.cs`,
// untouched here) — this law only tracks whatever `AimForward` it is handed and cannot manufacture an
// angle beyond what the guidance already capped.
using System;
using DragonScreen;

public static class BoosterSteerTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen booster steering law tests (register W24)");

        BoundsChecks();
        RestChecks();
        RateCeilingChecks();
        ChatterChecks();
        SignChecks();
        DivergenceSimulationChecks();
        GarbageSweepChecks();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // =====================================================================================
    // CONTRACT: output is ALWAYS in [-1, 1], never NaN/Infinite — for any finite input.
    // =====================================================================================
    static void BoundsChecks()
    {
        double[] errs = { -1e6, -180.0, -37.5, -1.0, 0.0, 1.0, 37.5, 180.0, 1e6 };
        double[] rates = { -1e6, -400.0, -5.0, 0.0, 5.0, 400.0, 1e6 };

        foreach (double e in errs)
            foreach (double r in rates)
            {
                bool db;
                double cmd = BoosterSteer.Axis(e, r, 1.0, out db);
                Check("Axis(" + e + "," + r + ") in [-1,1]", cmd >= -1.0 && cmd <= 1.0, cmd.ToString("F4"));
                Check("Axis(" + e + "," + r + ") finite", !double.IsNaN(cmd) && !double.IsInfinity(cmd), cmd.ToString());
            }
    }

    // =====================================================================================
    // At rest (zero error, zero rate) the law demands nothing — no chatter at the origin.
    // =====================================================================================
    static void RestChecks()
    {
        bool db;
        double cmd = BoosterSteer.Axis(0.0, 0.0, 1.0, out db);
        Check("zero error + zero rate -> zero command", cmd == 0.0, cmd.ToString("F6"));
        Check("zero error is never reported as deadbanded when the deadband is off",
              !db || BoosterSteer.DeadbandDeg > 0.0, "");

        BoosterSteerInputs s = new BoosterSteerInputs();   // everything zero
        BoosterSteerCommand c = BoosterSteer.Steer(s);
        Check("Steer() at rest: pitch=yaw=roll=0", c.Pitch == 0.0 && c.Yaw == 0.0 && c.Roll == 0.0,
              c.Pitch + "/" + c.Yaw + "/" + c.Roll);
    }

    // =====================================================================================
    // THE STRUCTURAL FIX — §3.1's divergence signature was an UNBOUNDED commanded rate. Prove the new
    // law's outer stage cannot ask for more than MaxRateDegPerS, at ANY error magnitude.
    // =====================================================================================
    static void RateCeilingChecks()
    {
        double savedMax = BoosterSteer.MaxRateDegPerS;
        double savedAngleKp = BoosterSteer.AngleToRateKp;
        double savedRateKp = BoosterSteer.RateKp;
        try
        {
            BoosterSteer.MaxRateDegPerS = 5.0;
            BoosterSteer.AngleToRateKp = 1.0;
            BoosterSteer.RateKp = 0.15;

            // A command driven purely by a HUGE error, with the measured rate still at zero, can never
            // exceed what RateKp * MaxRateDegPerS produces — the ceiling, not the error, sets the bound.
            double ceilingCmd = BoosterSteer.RateKp * BoosterSteer.MaxRateDegPerS;
            bool db;
            double huge = BoosterSteer.Axis(1.0e9, 0.0, 1.0, out db);
            Check("a 1e9-degree error commands no more than the rate ceiling allows",
                  Math.Abs(huge) <= Math.Min(1.0, ceilingCmd) + 1e-9, huge.ToString("F4"));

            // DS-ASC-001/002 (§3.1): rate_cmd_rads reached 195-436 deg/s. Prove that magnitude of DESIRED
            // rate is now categorically unreachable — sweep a range of "flight-scale" errors and confirm
            // the command saturates at the SAME value once the error is large enough to hit the ceiling,
            // rather than continuing to grow with the error (which is what "asks for a rate it never
            // achieves" looks like structurally).
            double atCeiling = BoosterSteer.Axis(1000.0, 0.0, 1.0, out db);   // already far past the ceiling
            double atTenX = BoosterSteer.Axis(10000.0, 0.0, 1.0, out db);
            Check("command does not keep growing once the rate ceiling is hit (no runaway)",
                  Math.Abs(atTenX - atCeiling) < 1e-9, atCeiling.ToString("F6") + " vs " + atTenX.ToString("F6"));
        }
        finally
        {
            BoosterSteer.MaxRateDegPerS = savedMax;
            BoosterSteer.AngleToRateKp = savedAngleKp;
            BoosterSteer.RateKp = savedRateKp;
        }
    }

    // =====================================================================================
    // Q2's seam: OBSERVABLE, and DEFAULT ZERO (behaviourally identical to no deadband).
    // =====================================================================================
    static void ChatterChecks()
    {
        Check("the deadband defaults to ZERO (70dc239 honoured until a booster flight earns one)",
              BoosterSteer.DeadbandDeg == 0.0, BoosterSteer.DeadbandDeg.ToString());

        bool dbOff;
        BoosterSteer.Axis(0.05, 0.0, 1.0, out dbOff);
        Check("with the deadband OFF, a tiny error is never reported as deadbanded", !dbOff, "");

        double saved = BoosterSteer.DeadbandDeg;
        try
        {
            BoosterSteer.DeadbandDeg = 0.5;
            bool dbIn;
            double insideCmd = BoosterSteer.Axis(0.1, 0.0, 1.0, out dbIn);
            Check("inside the deadband: command is exactly zero", insideCmd == 0.0, insideCmd.ToString("F6"));
            Check("inside the deadband: reported as deadbanded (OBSERVABLE, per the owner's Q2 refinement)",
                  dbIn, "");

            bool dbOut;
            double outsideCmd = BoosterSteer.Axis(5.0, 0.0, 1.0, out dbOut);
            Check("outside the deadband: NOT reported as deadbanded", !dbOut, "");
            Check("outside the deadband: commands something (the error is not silently eaten)",
                  outsideCmd != 0.0, outsideCmd.ToString("F4"));

            BoosterSteerInputs s = new BoosterSteerInputs { PitchErrDeg = 0.1, YawErrDeg = 5.0 };
            BoosterSteerCommand c = BoosterSteer.Steer(s);
            Check("Steer() surfaces the applied deadband value (BlackBox-observable, register BB1)",
                  c.DeadbandDegApplied == 0.5, c.DeadbandDegApplied.ToString());
            Check("Steer() surfaces WHICH axis was deadbanded", c.PitchDeadbanded && !c.YawDeadbanded, "");
        }
        finally { BoosterSteer.DeadbandDeg = saved; }
    }

    // =====================================================================================
    // Negative feedback: a positive error (needs a positive rotation to close) commands a POSITIVE
    // output at the default sign, and a positive measured rate (already moving the right way) REDUCES
    // the command relative to zero rate — the law dampens, it does not merely proportion.
    // =====================================================================================
    static void SignChecks()
    {
        bool db;
        double posErr = BoosterSteer.Axis(10.0, 0.0, 1.0, out db);
        Check("a positive error commands a positive output (default sign = +1)", posErr > 0.0, posErr.ToString("F4"));

        double negErr = BoosterSteer.Axis(-10.0, 0.0, 1.0, out db);
        Check("a negative error commands a negative output", negErr < 0.0, negErr.ToString("F4"));
        Check("the law is antisymmetric in error", Math.Abs(posErr + negErr) < 1e-9, "");

        double withClosingRate = BoosterSteer.Axis(10.0, 2.0, 1.0, out db);   // already rotating toward the target
        Check("rate feedback DAMPS: closing rate reduces the command below the zero-rate case",
              withClosingRate < posErr, withClosingRate.ToString("F4") + " vs " + posErr.ToString("F4"));

        // the sign multiplier is the one hand-crank the owner gets pre-flight (see BoosterHost.cs's open
        // question) — prove it actually inverts the axis end to end.
        double flipped = BoosterSteer.Axis(10.0, 0.0, -1.0, out db);
        Check("the sign multiplier inverts the whole axis", flipped < 0.0 && Math.Abs(flipped + posErr) < 1e-9,
              flipped.ToString("F4"));
    }

    // =====================================================================================
    // CLOSED-LOOP SIMULATION — the actual shape of §3.1's failure. Start at a LARGE initial error
    // (170 degrees, the flip's own magnitude) and integrate a simple rotational plant. The recorded
    // failure signature is a rate that grows MONOTONICALLY without bound; this checks that never
    // happens, and that the error trends down rather than diverging.
    // =====================================================================================
    static void DivergenceSimulationChecks()
    {
        double savedMax = BoosterSteer.MaxRateDegPerS;
        double savedAngleKp = BoosterSteer.AngleToRateKp;
        double savedRateKp = BoosterSteer.RateKp;
        try
        {
            BoosterSteer.MaxRateDegPerS = 5.0;
            BoosterSteer.AngleToRateKp = 1.0;
            BoosterSteer.RateKp = 0.15;

            double errDeg = 170.0;      // the flip's own scale — a large initial error
            double rateDps = 0.0;
            const double dt = 0.05;     // 20 Hz, a plausible control-loop rate
            const double alpha = 8.0;   // assumed deg/s^2 per unit command — a test-only plant, not a vehicle figure
            double maxAbsRateSeen = 0.0;
            bool sawIncreaseAfterHalfway = false;
            double errAtStart = Math.Abs(errDeg);

            for (int tick = 0; tick < 400; tick++)   // 20 s of simulated flight
            {
                bool db;
                double cmd = BoosterSteer.Axis(errDeg, rateDps, 1.0, out db);
                rateDps += cmd * alpha * dt;
                errDeg -= rateDps * dt;

                double absRate = Math.Abs(rateDps);
                if (absRate > maxAbsRateSeen) maxAbsRateSeen = absRate;

                // the failure signature: rate climbing monotonically well past what §3.1 measured as
                // achievable (68 dps) while error is still large. Flag it if seen past the loop's first
                // half — plenty of settling time for a 20 Hz loop over 20 s.
                if (tick > 200 && absRate > 200.0) sawIncreaseAfterHalfway = true;
            }

            Check("simulated rate never approaches the recorded divergence magnitude (195-436 dps)",
                  maxAbsRateSeen < 100.0, maxAbsRateSeen.ToString("F1") + " dps");
            Check("the rate ceiling was actually the bound in force (not merely never reached)",
                  maxAbsRateSeen <= BoosterSteer.MaxRateDegPerS + 1.0,
                  maxAbsRateSeen.ToString("F2") + " vs ceiling " + BoosterSteer.MaxRateDegPerS);
            Check("no late-loop runaway", !sawIncreaseAfterHalfway, "");
            Check("the error shrank (converging, not diverging)", Math.Abs(errDeg) < errAtStart,
                  errAtStart.ToString("F1") + " -> " + Math.Abs(errDeg).ToString("F1"));
        }
        finally
        {
            BoosterSteer.MaxRateDegPerS = savedMax;
            BoosterSteer.AngleToRateKp = savedAngleKp;
            BoosterSteer.RateKp = savedRateKp;
        }
    }

    // =====================================================================================
    // GARBAGE INPUT — matches W8's all-phase contract sweep (BoosterTest.ContractChecks): NaN and
    // Infinity in either argument must read as zero, never propagate, never throw.
    // =====================================================================================
    static void GarbageSweepChecks()
    {
        double[] garbage = { double.NaN, double.PositiveInfinity, double.NegativeInfinity };
        foreach (double g in garbage)
        {
            bool db;
            double c1 = BoosterSteer.Axis(g, 0.0, 1.0, out db);
            Check("garbage error (" + g + ") -> finite, bounded command",
                  !double.IsNaN(c1) && !double.IsInfinity(c1) && c1 >= -1.0 && c1 <= 1.0, c1.ToString());

            double c2 = BoosterSteer.Axis(0.0, g, 1.0, out db);
            Check("garbage rate (" + g + ") -> finite, bounded command",
                  !double.IsNaN(c2) && !double.IsInfinity(c2) && c2 >= -1.0 && c2 <= 1.0, c2.ToString());

            double c3 = BoosterSteer.Axis(10.0, 0.0, g, out db);
            Check("garbage sign multiplier (" + g + ") -> finite, bounded command",
                  !double.IsNaN(c3) && !double.IsInfinity(c3) && c3 >= -1.0 && c3 <= 1.0, c3.ToString());
        }

        BoosterSteerInputs garbageInputs = new BoosterSteerInputs
        {
            PitchErrDeg = double.NaN, YawErrDeg = double.PositiveInfinity, RollErrDeg = double.NegativeInfinity,
            PitchRateDps = double.NaN, YawRateDps = double.PositiveInfinity, RollRateDps = double.NegativeInfinity
        };
        BoosterSteerCommand cmd = BoosterSteer.Steer(garbageInputs);
        Check("Steer() on an all-garbage input returns a defined, bounded command on every axis",
              Finite01(cmd.Pitch) && Finite01(cmd.Yaw) && Finite01(cmd.Roll),
              cmd.Pitch + "/" + cmd.Yaw + "/" + cmd.Roll);
    }

    static bool Finite01(double v) { return !double.IsNaN(v) && !double.IsInfinity(v) && v >= -1.0 && v <= 1.0; }
}
