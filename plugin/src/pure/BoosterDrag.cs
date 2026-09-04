/*
 * DragonScreen - BoosterDrag
 *
 * PURE. The Falcon 9 booster's ballistic coefficient as a function of Mach - the empirical drag curve,
 * MINED FROM THE RECORDED CORPUS (user 2026-08-25: "use the flights to build the perfect trajectory
 * predictions"). 18,080 clean unpowered in-atmosphere descent samples across 48 recorded RSS/RO flights,
 * binned by Mach (median bc per 0.5-Mach bin):
 *
 *      Mach  0.5   1.0   1.5   2.0   2.5   3.0   3.5   4.0   4.5   5.0
 *      bc    2582  1485  1796  1075  1331  1321  1481  1580  1582  1439   kg/m2
 *
 * ---- ⛔ WHY A CURVE, NOT A SCALAR (the bug this fixes) ----
 * The booster's bc is NOT constant - it drops ~2600 (subsonic, low Cd) to ~1075 through the transonic
 * drag rise (Mach 2), then ~1400-1580 hypersonic. Feeding the trajectory integrator ONE scalar bc (the
 * last live measurement) mis-predicts wherever the Mach along the fall differs from where it was measured;
 * worse, the live bc is HELD at a garbage near-vacuum value (~37000) through the entry burn - exactly when
 * the burn needs to aim - so the predicted impact was tens of km wrong (flight_0825_184857: entry burn
 * left the impact 25 km long, drag then over-shortened it to 16 km short of the barge). The integrator
 * already supports a Mach-dependent drag (Trajectory.DragFactorAt, ported from the Trajectories add-on);
 * it was just never fed one. This is that curve, from OUR vehicle's own recorded drag.
 *
 * The drag FACTOR the integrator wants is 1/bc (drag accel = 0.5*rho*v^2 / bc). Reynolds is unused - the
 * corpus is binned on Mach alone, which is what dominates the Falcon booster's Cd here.
 *
 * ⚠ PROVENANCE (W1, 2026-09-04). Recovered from the tree deleted 2026-09-01. The CODE is
 * byte-identical between `0d6423d` (the last pre-comment-strip commit) and `8b81816^` (the recovery
 * target) - verified by a comment-stripped diff of the two - so this file is the `0d6423d` copy,
 * taken solely to recover the commentary `158eb2a` stripped, exactly as R1 §3.5 directs. No gen-1
 * LOGIC is imported by that choice.
 * ⚠ The curve is a FALCON-9 BOOSTER curve (R1 §5.1): it is not valid for the Dragon capsule or S2.
 * Nothing in Wave A calls it yet - it is the dataset §B16's booster core will read.
 *
 * ---- ⛔ §B16.8 RULING 1 - REFERENCE WITH STATED PROVENANCE, NOT SEED TRUTH (added by W3, Wave C,
 * 2026-09-04; comment-only, not one digit of the table touched) ----
 * THE RAW EVIDENCE BEHIND THIS CURVE IS GONE. The 18,080 samples across 48 recorded RSS/RO flights were
 * `flight_0825_*.csv` and their siblings: GITIGNORED AND NEVER COMMITTED (R1 §3.5, §4.3 - only the
 * DS-ASC-00x and Crew-2 recordings were ever force-added). The ten numbers in the table above, and the
 * per-phase aggregate in the also-deleted `docs/tuning/TUNING_DB.json`, are the ONLY SURVIVING
 * DISTILLATES. **Neither can be re-derived, re-binned or re-checked from anything in this repository.**
 * If a digit here is ever doubted - or silently changed - there is nothing in the tree to check it
 * against, and the symptom would be a landing miss, not a test failure - which is why the table is now
 * pinned by `test/BoosterDragTest.cs` (register line S63, DONE 2026-09-04).
 * ⇒ Treat this curve as THE BEST NUMBER WE HAVE AND NOT AS EVIDENCE. A number you cannot re-derive is
 * still worth keeping; it has simply stopped being proof of anything. Owner decision, 2026-09-03 (R1
 * open question Q2): RE-FLY. The corpus is rebuilt by RECORDED re-flights, which needs the BlackBox
 * (`docs/BLACKBOX_RESEARCH.md`) and a SEPARATE owner glass gate (S0 banner) - no task can converge this
 * curve under the preview-only gate; it can only build the thing that would.
 * ⚠ What such a re-flight MUST record, or the back-solve `BC = 0.5*rho*v^2 / a_drag` cannot be redone at
 * all (§B16.8): per sample - atmospheric DENSITY, MACH, DRAG ACCELERATION (or total accel + gravity +
 * thrust), MASS, and an EXPLICIT UNPOWERED-PHASE FLAG. Without that flag the powered samples poison the
 * bins - the surviving 18,080 were "clean unpowered in-atmosphere descent" samples for that reason.
 */
namespace DragonScreen
{
    public static class BoosterDrag
    {
        // The corpus curve: Mach breakpoints and the median measured bc (kg/m2) in each bin.
        private static readonly double[] Mach = { 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0 };
        private static readonly double[] Bc   = { 2582, 1485, 1796, 1075, 1331, 1321, 1481, 1580, 1582, 1439 };

        /// <summary>
        /// Ballistic coefficient (kg/m2) at a Mach number, linearly interpolated over the corpus curve.
        /// Below Mach 0.5 it holds the subsonic value; above Mach 5 (the entry-burn regime, where the
        /// corpus has few clean samples because thrust blocks measurement) it holds the top hypersonic
        /// value - a far better estimate than the scalar it replaces.
        /// </summary>
        public static double BcAtMach(double mach)
        {
            if (mach <= Mach[0]) return Bc[0];
            int n = Mach.Length;
            if (mach >= Mach[n - 1]) return Bc[n - 1];
            for (int i = 1; i < n; i++)
            {
                if (mach <= Mach[i])
                {
                    double f = (mach - Mach[i - 1]) / (Mach[i] - Mach[i - 1]);
                    return Bc[i - 1] + (Bc[i] - Bc[i - 1]) * f;
                }
            }
            return Bc[n - 1];
        }

        /// <summary>
        /// The Trajectory.DragFactorAt the integrator wants: the inverse ballistic coefficient at this
        /// Mach, so drag accel = 0.5*rho*v^2*factor. Reynolds (rhoV) is ignored - the corpus curve is
        /// Mach-only. Wire this into ImpactPredictor for the booster so the whole entry+descent is
        /// integrated with the vehicle's own recorded drag instead of one stale scalar.
        /// </summary>
        public static double DragFactor(double mach, double pseudoReynolds)
        {
            double bc = BcAtMach(mach);
            return (bc > 1.0) ? 1.0 / bc : 0.0;
        }
    }
}
