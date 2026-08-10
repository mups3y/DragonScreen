/*
 * DragonScreen - DeorbitOrbit
 *
 * PURE. The two-burn phase-down from the station's orbit onto the orbit every landing aim was fitted
 * from. Ported from `F9I/station_ops.ks:2560 StPhaseToDeorbitOrbit`, constants at `:78-80`.
 *
 * ---- ⛔ 85.1 × 79.2 km IS NOT A ROUND NUMBER, IT IS A CALIBRATION ----
 * `dgTrimOver` - how far past the landing zone the de-orbit burn aims the impact - was fitted flight
 * by flight FROM THIS ORBIT. 286 000 m for a crew S2 de-orbit gave a 159 m landing; 315 450 m for
 * cargo gave 331 m. Those numbers describe a specific entry energy. De-orbiting from the station's
 * 86.8 × 85.8 km instead would carry a different one, and every aim in `pure/Deorbit.cs` would be
 * describing an orbit the capsule is not on.
 *
 * F9I's own source says it plainly: *"THE landing-calibrated orbit. Do not change without re-fitting
 * dgTrimOver."* So the phase-down is not tidying-up before the burn - it is what makes the burn's
 * constants true.
 *
 * ---- ⚠ AND IT IS SKIPPABLE, DELIBERATELY ----
 * Two Hohmann half-burns cost propellant the landing also needs. If we are already within
 * `ToleranceM` of the orbit, F9I does not burn: *"a needless burn only spends the margin the landing
 * depends on."* The caller must also be able to give up and de-orbit from where it is - see the
 * no-thrust case in `PhaseDownOps`, which is a real failure that has happened.
 */
namespace DragonScreen
{
    /// <summary>One half of the Hohmann transfer down. Δv is along PROGRADE; negative lowers.</summary>
    public struct PhaseDownBurn
    {
        public bool Needed;
        /// <summary>Δv along prograde, m/s. Negative lowers the opposite apsis.</summary>
        public double DvMps;
        /// <summary>True to burn at apoapsis, false at periapsis.</summary>
        public bool AtApoapsis;
        public string Label;
    }

    public static class DeorbitOrbit
    {
        // ---- F9I's CONSTANTS. station_ops.ks:78-80. ----

        /// <summary>Apoapsis of the landing-calibrated orbit, metres. `stDeorbitAp`.</summary>
        public const double TargetApoapsisM = 85100.0;
        /// <summary>Periapsis of the landing-calibrated orbit, metres. `stDeorbitPe`.</summary>
        public const double TargetPeriapsisM = 79200.0;
        /// <summary>How close to it counts as arrived, metres. `stOrbTol`.</summary>
        public const double ToleranceM = 1500.0;

        /// <summary>
        /// Are we already on the landing orbit? BOTH apsides must be inside the tolerance - F9I nests
        /// the two tests, and an orbit with the right apoapsis and the wrong periapsis is not the
        /// orbit the aim was fitted from.
        /// </summary>
        public static bool AlreadyOnOrbit(double apoapsisM, double periapsisM)
        {
            double da = apoapsisM - TargetApoapsisM;
            double dp = periapsisM - TargetPeriapsisM;
            if (da < 0.0) da = -da;
            if (dp < 0.0) dp = -dp;
            return da < ToleranceM && dp < ToleranceM;
        }

        /// <summary>
        /// Burn 1: at APOAPSIS, lower the periapsis to the target.
        ///
        /// Burning at the high point moves the low point - the apsis you are NOT at is the one that
        /// changes. Getting that backwards is a burn in the right direction at the wrong place, which
        /// costs the propellant and does not reach the orbit.
        /// </summary>
        public static PhaseDownBurn LowerPeriapsis(double mu, double bodyRadiusM,
                                                   double apoapsisM, double smaNowM)
        {
            PhaseDownBurn b = new PhaseDownBurn();
            b.AtApoapsis = true;
            b.Label = "lower periapsis to " + (TargetPeriapsisM / 1000.0).ToString("F1") + " km";

            double r1 = bodyRadiusM + apoapsisM;
            double rt = bodyRadiusM + TargetPeriapsisM;
            double smaTarget = (r1 + rt) / 2.0;
            b.DvMps = Orbital.VisViva(mu, r1, smaTarget) - Orbital.VisViva(mu, r1, smaNowM);
            b.Needed = System.Math.Abs(b.DvMps) > 0.05;
            return b;
        }

        /// <summary>Burn 2: at the new PERIAPSIS, lower the apoapsis to the target.</summary>
        public static PhaseDownBurn LowerApoapsis(double mu, double bodyRadiusM,
                                                  double periapsisM, double smaNowM)
        {
            PhaseDownBurn b = new PhaseDownBurn();
            b.AtApoapsis = false;
            b.Label = "lower apoapsis to " + (TargetApoapsisM / 1000.0).ToString("F1") + " km";

            double r2 = bodyRadiusM + periapsisM;
            double ra = bodyRadiusM + TargetApoapsisM;
            double smaTarget = (r2 + ra) / 2.0;
            b.DvMps = Orbital.VisViva(mu, r2, smaTarget) - Orbital.VisViva(mu, r2, smaNowM);
            b.Needed = System.Math.Abs(b.DvMps) > 0.05;
            return b;
        }

        /// <summary>
        /// Total Δv the phase-down will cost from here, m/s. For the budget report, before committing.
        ///
        /// Approximate by construction: burn 2 is solved from the periapsis burn 1 will produce, which
        /// is the target periapsis if burn 1 lands exactly. Close enough to decide whether there is
        /// propellant for it, which is the only question being asked.
        /// </summary>
        public static double TotalDvMps(double mu, double bodyRadiusM,
                                        double apoapsisM, double periapsisM, double smaNowM)
        {
            PhaseDownBurn one = LowerPeriapsis(mu, bodyRadiusM, apoapsisM, smaNowM);
            double r1 = bodyRadiusM + apoapsisM;
            double rt = bodyRadiusM + TargetPeriapsisM;
            double smaAfterOne = (r1 + rt) / 2.0;
            PhaseDownBurn two = LowerApoapsis(mu, bodyRadiusM, TargetPeriapsisM, smaAfterOne);
            return System.Math.Abs(one.DvMps) + System.Math.Abs(two.DvMps);
        }
    }
}
