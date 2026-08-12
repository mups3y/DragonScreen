/*
 * DragonScreen - FlipRollTest
 *
 * ⛔ THE BOOSTER FLIP'S ROLL AXIS, FLOWN HEADLESS AGAINST THE REAL CASCADE.
 *
 * The flip rolled 759 degrees through a 180 degree turn that requires EXACTLY ZERO roll, with the
 * roll actuator railed on 80% of ticks. Three separate "fixes" were shipped for it without ever
 * once simulating the axis, and two of them were no-ops. This is that simulation.
 *
 * The plant is the measured booster: `b_moiY = 48 t.m2`, `b_torqueY = 41.13 kN.m`, both read off
 * the 2026-08-13 recording. The controller is the real `KosPid` rate loop, the real `TorquePi`, and
 * the real `AttitudeCascade` - so what is under test is the actual control law, not a model of it.
 */
using System;
using DragonScreen;

public static class FlipRollTest
{
    static int checks, failures;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL: " + what + "  [" + detail + "]"); }
    }

    struct Result
    {
        public double TravelDeg;      // total roll swept, both directions
        public double PeakRateDps;
        public double SaturatedFrac;  // ticks with |actuation| >= 0.99
        public double FinalErrDeg;
    }

    /// <summary>
    /// Fly the roll axis for `seconds`, starting `startErrDeg` off its reference.
    ///
    /// The reference does not move: rotating about `flipAxis` with a roll reference of `-flipAxis`
    /// leaves the target top perpendicular to the rotation plane throughout. That is the whole
    /// point - the correct answer for this axis is "do nothing", and any travel is error.
    /// </summary>
    static Result Fly(double startErrDeg, double capDps, double stoppingTime, double seconds)
    {
        const double moi = 48.0;        // t.m2   - measured, b_moiY
        const double torque = 41.13;    // kN.m   - measured, b_torqueY
        const double dt = 0.02;

        KosPid rate = new KosPid(1.0, 0.1, 0.0, true);
        TorquePi pi = new TorquePi();

        double phi = startErrDeg * Math.PI / 180.0;   // roll error, radians
        double omega = 0.0;                            // roll rate, rad/s
        double act = 0.0;
        Result r = new Result();
        int steps = (int)(seconds / dt), sat = 0;

        for (int i = 0; i < steps; i++)
        {
            double cap = capDps * Math.PI / 180.0;
            double maxOmega = AttitudeCascade.MaxOmegaCapped(torque, moi, stoppingTime, cap);
            double target = rate.Update(-phi, 0.0, maxOmega, dt);
            double tq = pi.Update(omega, target, moi, torque, dt);
            act = AttitudeCascade.Actuation(tq, torque, act);
            if (act > 1.0) act = 1.0;
            if (act < -1.0) act = -1.0;

            omega += (act * torque / moi) * dt;
            phi -= omega * dt;
            // Shortest way round: the error wraps at +/-180, which is what made the real command
            // alternate instead of arresting.
            while (phi > Math.PI) phi -= 2.0 * Math.PI;
            while (phi < -Math.PI) phi += 2.0 * Math.PI;

            double rateDeg = Math.Abs(omega) * 180.0 / Math.PI;
            r.TravelDeg += rateDeg * dt;
            if (rateDeg > r.PeakRateDps) r.PeakRateDps = rateDeg;
            if (Math.Abs(act) >= 0.99) sat++;
        }
        r.SaturatedFrac = (double)sat / steps;
        r.FinalErrDeg = Math.Abs(phi) * 180.0 / Math.PI;
        return r;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen flip roll tests");
        checks = failures = 0;

        // ---- 1. THE PERMITTED RATE WAS THE BUG ----
        double uncapped = AttitudeCascade.MaxOmega(41.13, 48.0, Landing.FlipStoppingTime)
                          * 180.0 / Math.PI;
        double pitch = AttitudeCascade.MaxOmega(458.16, 4916.0, Landing.FlipStoppingTime)
                       * 180.0 / Math.PI;
        Check("uncapped, the flip permits a huge roll rate", uncapped > 100.0,
              uncapped.ToString("F0") + " deg/s");
        Check("...far beyond the pitch rate the flip is actually trying to achieve",
              uncapped > pitch * 5.0,
              "roll " + uncapped.ToString("F0") + " vs pitch " + pitch.ToString("F0") + " deg/s");

        double capped = AttitudeCascade.MaxOmegaCapped(41.13, 48.0, Landing.FlipStoppingTime,
                            Landing.FlipRollRateCapDps * Math.PI / 180.0) * 180.0 / Math.PI;
        Check("the cap brings it to F9I's measured envelope", capped <= 15.01,
              capped.ToString("F1") + " deg/s");

        // ---- 2. ⛔ THIS MODEL DOES NOT REPRODUCE THE FLIGHT, AND THAT IS THE FINDING. ----
        // Uncapped, the roll axis ALONE settles in 71 deg with the actuator railed on 3% of ticks.
        // The real flip travels 765 deg railed 80% of the time (000853) and 710 deg railed 88%
        // (005927) - a sustained limit cycle, not a runaway. So whatever drives it is NOT in the
        // single-axis law: it is cross-axis coupling, the shared RCS nozzles, or a roll REFERENCE
        // that is itself moving. The booster's per-axis error was never recorded, so it cannot be
        // settled from the data we hold; `b_phiRollDeg` was added on 2026-08-13 to close that.
        //
        // This is pinned as a NEGATIVE result so the next session does not re-derive it: the rate
        // ceiling below is a BOUND on a runaway, and it is NOT a fix for the flip.
        Result old = Fly(60.0, 0.0, Landing.FlipStoppingTime, 25.0);
        Check("the single-axis law is STABLE on its own - the bug is not here",
              old.TravelDeg < 150.0 && old.SaturatedFrac < 0.2,
              old.TravelDeg.ToString("F0") + " deg, "
              + (old.SaturatedFrac * 100.0).ToString("F0") + "% saturated");

        // ---- 3. THE FIX ----
        Result now = Fly(60.0, Landing.FlipRollRateCapDps, Landing.FlipStoppingTime, 25.0);
        Check("CAPPED: the roll is bounded", now.TravelDeg < 120.0,
              now.TravelDeg.ToString("F0") + " deg travelled");
        Check("CAPPED: it beats F9I's own flip+boostback figure of 138 deg",
              now.TravelDeg < 138.0, now.TravelDeg.ToString("F0") + " deg");
        Check("CAPPED: the peak rate stays inside the measured envelope",
              now.PeakRateDps <= Landing.FlipRollRateCapDps * 1.15,
              now.PeakRateDps.ToString("F1") + " deg/s");
        Check("CAPPED: and it actually ARRIVES", now.FinalErrDeg < 5.0,
              now.FinalErrDeg.ToString("F2") + " deg of roll error left");
        Check("CAPPED: without living on the rail", now.SaturatedFrac < 0.25,
              (now.SaturatedFrac * 100.0).ToString("F0") + "% saturated");

        // ---- 4. THE CAP MUST NOT MAKE THINGS WORSE ----
        // All it is entitled to claim. It bounds the commanded rate; it does not cure the flip.
        Check("the cap never costs travel", now.TravelDeg <= old.TravelDeg + 1.0,
              old.TravelDeg.ToString("F0") + " -> " + now.TravelDeg.ToString("F0") + " deg");
        Check("...and never costs saturation", now.SaturatedFrac <= old.SaturatedFrac + 0.02,
              (old.SaturatedFrac * 100.0).ToString("F0") + "% -> "
              + (now.SaturatedFrac * 100.0).ToString("F0") + "%");

        // ---- 5. A STAGE ALREADY ON ITS REFERENCE MUST NOT BE DISTURBED ----
        Result still = Fly(0.0, Landing.FlipRollRateCapDps, Landing.FlipStoppingTime, 25.0);
        Check("a stage already lined up stays still", still.TravelDeg < 1.0,
              still.TravelDeg.ToString("F2") + " deg");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
