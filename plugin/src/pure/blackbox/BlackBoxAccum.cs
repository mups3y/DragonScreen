// DragonScreen — BlackBox / R0 ACCUMULATORS  (register BB1; spec: §2.0's R0 tier, §2.4, §3.2)
// ============================================================================================
// PURE. The physics-rate tier: accumulate over the interval, emit with the row, reset. Never sampled.
//
// ---- ⛔ THE RETRACTION THIS TIER EXISTS BECAUSE OF (§2.0, §3.2) ----
// The deleted corpus's `act_*` per-tick snapshots produced a "68-82 % RCS duty cycle" figure that had
// to be WITHDRAWN. A 10 Hz snapshot of a ~0.06 s pulse dwell is an alias, not a measurement: it
// answers "was a pulse in progress at the instant I looked", which correlates with duty only by
// accident. Only the physics-rate accumulators settled the question. §2.0's rule, stated once:
// ANYTHING THAT PULSES IS ACCUMULATED, NOT SAMPLED.
//
// The same argument applies to extrema. §B11's peak-axial-g target (~4 g) and §B8's max-Q band
// (30-35 kPa) are PEAKS, and a peak is exactly what a snapshot misses — a 30 kPa maximum passing in a
// few seconds can fall between two 10 Hz samples of a rising curve and never appear at all. So the
// peaks are latched here at the physics rate and ride out beside their R1 snapshot: `accel_g` says
// what it was when we looked, `accel_g_peak` says what it actually reached.
//
// ---- WHAT IS DELIBERATELY ABSENT ----
// §2.4 also lists `acc_att_imp` / `acc_trans_imp` / `acc_both_imp` — DELIVERED RCS impulse by category,
// the basis for per-category propellant attribution. Those need `pure/RcsAccounting.cs`, which was
// deleted with the autopilot and is not in this tree. Declaring the columns and never filling them is
// precisely the `torque_cmd` ghost BB1 exists to stop, so they are not declared. Logged as its own
// register line (C1.1), not smuggled in as a zero.
//
// ---- COST (§4.7's < 0.05 ms per FixedUpdate) ----
// Pure arithmetic on pre-allocated doubles in one struct. No allocation, no reflection, no LINQ, no
// branch on anything but a comparison. It is called once per physics tick and does ~30 flops.
// ============================================================================================
using System;

namespace DragonScreen.BlackBox
{
    /// <summary>
    /// One row-interval's worth of physics-rate accumulation. A struct held by value in the glue and
    /// reset the instant its contents are written into a row, so an interval is never double-counted.
    /// </summary>
    public struct BlackBoxAccum
    {
        // ---- interval ----
        public double IntervalS;

        // ---- §2.4's four mutually-exclusive actuation categories, in SECONDS ----
        // Mutually exclusive by construction: exactly one of the four advances per tick, so the four
        // always sum to IntervalS. That identity is what makes a duty cycle computable without
        // assuming anything about the sampling, and BB3 checks it as a self-check.
        public double AttS, TransS, BothS, NoneS;

        // ---- command-seconds: the integral of the applied command magnitude ----
        public double AppAttCmdS, AppTransCmdS;

        // ---- saturation: time at or beyond the authority limit ----
        public double SatS;

        // ---- in-interval extrema ----
        public double PeakAccelG, PeakQPa, PeakRateDps;

        /// <summary>The threshold below which a command counts as "not commanded". Deadband-free.</summary>
        public const double CommandEpsilon = 1e-3;
        /// <summary>§2.4: saturation is |command| >= 0.99 — the definition of out of control authority.</summary>
        public const double SaturationLevel = 0.99;

        public static BlackBoxAccum Fresh() { return new BlackBoxAccum(); }

        /// <summary>
        /// One physics tick. `dt` is the tick's own length (`Time.fixedDeltaTime` scaled by physics
        /// warp), NOT a wall clock and NOT the row period — under physics warp a tick covers more UT,
        /// and the accumulator must reflect that or every duty cycle is wrong by the warp factor.
        /// </summary>
        public void Add(double dt, double attCmd, double transCmd, double accelG, double qPa, double rateDps)
        {
            if (dt <= 0.0 || double.IsNaN(dt) || double.IsInfinity(dt)) return;
            IntervalS += dt;

            bool att = Math.Abs(attCmd) > CommandEpsilon;
            bool trans = Math.Abs(transCmd) > CommandEpsilon;
            if (att && trans) BothS += dt;
            else if (att) AttS += dt;
            else if (trans) TransS += dt;
            else NoneS += dt;

            if (att) AppAttCmdS += Math.Abs(attCmd) * dt;
            if (trans) AppTransCmdS += Math.Abs(transCmd) * dt;
            if (Math.Abs(attCmd) >= SaturationLevel) SatS += dt;

            // NaN never wins a comparison, so a bad read leaves the peak alone rather than poisoning it
            // — which is the behaviour wanted: §4.6's NaN-to-blank rule applies to the SNAPSHOT column,
            // and a peak that silently became NaN would blank a whole interval's worth of evidence.
            if (accelG > PeakAccelG) PeakAccelG = accelG;
            if (qPa > PeakQPa) PeakQPa = qPa;
            if (rateDps > PeakRateDps) PeakRateDps = rateDps;
        }

        /// <summary>True once at least one tick has landed. An empty interval writes blank, not zero.</summary>
        public bool Any { get { return IntervalS > 0.0; } }

        /// <summary>Write the interval into a row, then <see cref="Fresh"/> it in the caller.</summary>
        public void Put(string[] c)
        {
            if (!Any) return;   // §4.6: nothing accumulated is NO VALUE, and a zero duty cycle is a claim
            BlackBoxSchema.Set(c, BlackBoxCols.AccIntS, IntervalS);
            BlackBoxSchema.Set(c, BlackBoxCols.AccAttS, AttS);
            BlackBoxSchema.Set(c, BlackBoxCols.AccTransS, TransS);
            BlackBoxSchema.Set(c, BlackBoxCols.AccBothS, BothS);
            BlackBoxSchema.Set(c, BlackBoxCols.AccNoneS, NoneS);
            BlackBoxSchema.Set(c, BlackBoxCols.AccAppAtt, AppAttCmdS);
            BlackBoxSchema.Set(c, BlackBoxCols.AccAppTrans, AppTransCmdS);
            BlackBoxSchema.Set(c, BlackBoxCols.ActSatS, SatS);
            BlackBoxSchema.Set(c, BlackBoxCols.AccelGPeak, PeakAccelG);
            BlackBoxSchema.Set(c, BlackBoxCols.QPaPeak, PeakQPa);
            BlackBoxSchema.Set(c, BlackBoxCols.RatePeakDps, PeakRateDps);
        }
    }
}
