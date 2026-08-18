/*
 * DragonScreen - EntryGuidance
 *
 * PURE. The lifting-entry range and cross-range controller, ported from
 * `F9I/dragon_deorbit.ks:2109 DgEntryGuidance`. The altitude schedule it steers to is
 * `pure/EntryMargin.cs`.
 *
 * ---- WHAT IT IS DOING ----
 * A Dragon controls where it lands by BANKING its lift vector - lift down shortens, lift up
 * stretches. This decides how much to bank, from how far the predicted impact is from where the
 * altitude schedule says it should be right now.
 *
 * "On profile" does NOT mean "aimed at the target". It means sitting the right distance PAST the
 * target for how high we still are, with the margin walking itself to zero by drogue deploy. Drag
 * only ever shortens, so a capsule aimed exactly at the pad arrives short.
 *
 * ---- ⛔ FOUR THINGS HERE ARE COUNTER-INTUITIVE AND EVERY ONE COST A FLIGHT ----
 *
 * 1. THE MARGIN IS CLAMPED TO THE RANGE STILL AHEAD. The schedule is a function of ALTITUDE alone,
 *    so it has no idea where the target is. On CargoDragon_045 it asked for 11.7 km of overshoot
 *    while the capsule was 0.4 km from the target, and still 4.2 km when it was 4.8 km PAST it -
 *    demanding downrange that no longer existed. `Ahead` is the LZ range projected onto the ground
 *    track: positive approaching, zero overhead, negative once past.
 *
 * 2. DIFFERENTIATE THE ERROR, NOT THE DISTANCE. The range to the impact point closes at 400-800 m/s
 *    for the whole descent simply because the capsule is flying towards it. That is not "the error
 *    is closing", it is "the vehicle is moving" - which is always true. Fed into a 20 s lead it
 *    produced 8-16 km of phantom credit, and flights 048 and 053 both sat at zero command from 32 km
 *    down while still 4.0 and 6.7 km long, coasting to the ground open-loop. Same code, same aim,
 *    288 m and 3452 m - the difference was only how far off each happened to be when the loop went
 *    quiet.
 *
 * 3. THE LEAD TERM IS CAPPED AT HALF THE ERROR. Differentiating the error shrank the phantom credit
 *    but did not BOUND it, and unbounded it still reverses the command. Reproduced exactly from
 *    CrewDragon_025:
 *        errNow -7502, 20*rate +5596 -> leadErr -1906 -> cmd -0.10
 *        errNow -5671, 20*rate +5887 -> leadErr  +217 -> cmd  0.00
 *    The lead overtook the error and flipped its sign, so the loop went quiet at 41 km with 5.7 km
 *    still correctable and never spoke again. The capsule fell from 5.7 km LONG to 47 km SHORT and
 *    splashed 49.3 km out. A derivative term exists to DAMP the proportional term, never to outvote
 *    it.
 *
 * 4. IT CAN SHORTEN OR COAST. IT CANNOT EXTEND. Flight 023 proved extend does nothing at all below
 *    ~36 km, so commanding it just wastes the descent pretending to correct. The command is clamped
 *    at zero on the positive side, and a persistent positive error is REPORTED instead - it means
 *    the de-orbit aim was too short and the landing is already lost.
 *
 * ---- ⚠ THIS IS NOT `pure/Entry.cs`, AND THEY ARE NOT DUPLICATES ----
 * Entry.cs answers "which way does the heat shield point in this altitude band" and carries the
 * CargoDragon_012 lesson - an entire entry flown 134.9 degrees off the nose. This file answers "how
 * hard do we bank to land where we aimed". Attitude and range. They compose; do not merge them.
 *
 * ---- AND LATERAL OUTLIVES VERTICAL ----
 * The cross-track loop is deliberately outside the range loop's latch. Below 12 km there is no
 * useful range authority left, but a capsule still has plenty of lateral authority and about 90 s of
 * flight to use it. CrewDragon_025 finished 4.3 km off in cross-track and 2.4 km of that accumulated
 * after the range loop latched, with nothing commanding.
 */
namespace DragonScreen
{
    public struct EntryGuideInputs
    {
        /// <summary>Radar altitude, metres.</summary>
        public double AltitudeM;
        /// <summary>
        /// Signed downrange error, metres. NEGATIVE means the predicted impact is LONG - past the
        /// target - which is where the profile wants us for most of the descent.
        /// </summary>
        public double DownrangeErrM;
        /// <summary>Signed cross-track error, metres.</summary>
        public double CrossTrackM;
        /// <summary>Great-circle range from us to the landing zone, metres.</summary>
        public double LzRangeM;
        /// <summary>Bearing from us to the landing zone, degrees.</summary>
        public double LzBearingDeg;
        /// <summary>Bearing from us to the PREDICTED IMPACT, degrees - our ground track.</summary>
        public double TrackBearingDeg;
        /// <summary>Total predicted miss from the landing zone, metres. Negative = no prediction.</summary>
        public double MissM;
        /// <summary>Seconds since the last controller update. Zero on the first call.</summary>
        public double DtS;
    }

    /// <summary>Carried between ticks. The caller owns it; the controller only updates it.</summary>
    public struct EntryMemory
    {
        public double FilteredRate, LastError;
        public bool HaveRate;
        /// <summary>Most shortening ever commanded. Zero at the end means the loop never worked.</summary>
        public double LiftMin;
        /// <summary>Worst below-profile error seen, metres.</summary>
        public double WorstErrorM;
        /// <summary>
        /// The range loop has given up for good. `dgDrop`.
        ///
        /// ⚠ LATCHED ON PURPOSE. Set when the prediction lands inside `LzToleranceM` or the capsule
        /// falls below `TermAltM`, and never cleared. F9I: *"re-engaging lift this low would only throw
        /// away a good approach"* - a prediction that has arrived on the target is a result to protect,
        /// not an input to keep correcting, and a metre of altitude noise around the floor must not be
        /// able to switch the loop back on in the last 90 seconds of a descent.
        /// </summary>
        public bool Dropped;
    }

    public struct EntryGuideCommand
    {
        /// <summary>-1 full shorten, 0 coast. NEVER positive - see the header.</summary>
        public double VerticalCmd;
        /// <summary>-1..1 cross-track command.</summary>
        public double LateralCmd;
        /// <summary>The margin the controller ACTUALLY steered on, after clamping.</summary>
        public double WantLongM;
        /// <summary>The lead-corrected error it acted on.</summary>
        public double LeadErrorM;
        /// <summary>True when we are BELOW the profile and cannot climb back onto it.</summary>
        public bool BelowProfile;
        public string Note;
    }

    public static class EntryGuidance
    {
        // ---- F9I's CONSTANTS. dragon_deorbit.ks:277-475. ----
        /// <summary>Downrange error that saturates the command, metres. `dgDownScale`.</summary>
        [Tunable] public static double DownScaleM = 20000.0;
        /// <summary>Cross-track error that saturates the lateral command, metres. `dgCrossScale`.</summary>
        [Tunable] public static double CrossScaleM = 5000.0;
        /// <summary>Lead time on the error rate, seconds. `dgLead`.</summary>
        [Tunable] public static double LeadS = 20.0;
        /// <summary>
        /// The capsule's ballistic coefficient at entry, kg/m2. MEASURED ~440 - avg 448 over
        /// flight_0817_214211 and 436 over flight_0817_232723. Fed to ImpactPredictor.Predict(v, bc)
        /// so the de-orbit can predict a DRAG-AWARE landing BEFORE entry, where no drag is measured
        /// live yet. This is the input the adaptive de-orbit targeting is being built on.
        /// </summary>
        [Tunable] public static double CapsuleBcKgM2 = 440.0;
        /// <summary>Lead term capped at this fraction of the error. `dgLeadFrac`. See trap 3.</summary>
        public const double LeadFrac = 0.5;
        /// <summary>Range loop latches off here, metres. `dgTermAlt`.</summary>
        public const double TermAltM = 12000.0;
        /// <summary>Cross-track loop keeps working down to here, metres. `dgLatFloor`.</summary>
        public const double LatFloorM = 3000.0;
        /// <summary>Below-profile warning threshold, metres. `dgBelowWarn`.</summary>
        public const double BelowWarnM = 1500.0;
        /// <summary>Predicted miss inside this needs no steering at all, metres. `dgLzTol`.</summary>
        public const double LzToleranceM = 50.0;
        /// <summary>Rate filter: 0.7 old, 0.3 new. `dgDRate` update.</summary>
        public const double RateFilterOld = 0.7;
        /// <summary>Minimum interval between rate updates, seconds.</summary>
        public const double RateIntervalS = 0.5;

        // ---- HOW THE COMMAND BECOMES AN ATTITUDE. dragon_deorbit.ks:35, 322-323, 474, 2311-2320. ----

        /// <summary>
        /// The capsule's trim angle of attack off retrograde, degrees. `dgAoA`. L/D about 0.27.
        ///
        /// ⚠ A DRAGON IS NOT A STARSHIP. Starship holds 78-79° - nearly broadside - and inheriting that
        /// number throws the guidance most of a hemisphere off. A capsule flies heat-shield-first and
        /// trims only about 15°.
        /// </summary>
        public const double TrimAoaDeg = 15.0;

        /// <summary>
        /// Dynamic pressure below which there is nothing to steer with, kPa. `dgQsteer`.
        /// Above the interface the capsule just holds shield-forward; banking in vacuum does nothing.
        /// </summary>
        public const double QSteerKpa = 0.003;

        /// <summary>
        /// Pitch sign. `dgPitchSign`. ESTABLISHED BY MEASUREMENT ON FLIGHT 001, not assumed: with the
        /// impact 157 km long the law commanded −1, which through this sign put lift along +up, and the
        /// error shortened monotonically to −3.5 km. So **+up SHORTENS and −up EXTENDS**.
        /// </summary>
        public const double PitchSign = -1.0;

        /// <summary>Yaw sign. `dgYawSign`. Same provenance as the cross-track formula's sign.</summary>
        public const double YawSign = 1.0;

        /// <summary>Cancel entry warp at this radar altitude, metres. `dgEntryWarpAlt`.</summary>
        public const double WarpCancelAltM = 55000.0;
        /// <summary>Physics-warp index used above it: 0=1x 1=2x 2=3x 3=4x. `dgEntryWarpIdx`.</summary>
        public const int WarpIndex = 3;

        /// <summary>
        /// How much of full deflection the two commands add up to, 0..1.
        ///
        /// The vertical and lateral axes are perpendicular, so the magnitude is the plain hypotenuse -
        /// and it is capped at 1 because the AoA below is a TRIM angle, not a budget to overspend.
        /// </summary>
        public static double LiftFraction(double verticalCmd, double lateralCmd)
        {
            double m = System.Math.Sqrt(verticalCmd * verticalCmd + lateralCmd * lateralCmd);
            return (m > 1.0) ? 1.0 : m;
        }

        /// <summary>
        /// The angle of attack to actually fly, degrees.
        ///
        /// ⚠ SCALED, NOT BANG-BANG. A partial command flies a partial angle; a zero command collapses
        /// to exactly retrograde. Slamming to full deflection for any non-zero error is how a capsule
        /// with 20 s of lead time oscillates instead of converging.
        /// </summary>
        public static double AoaCommandDeg(double verticalCmd, double lateralCmd)
        {
            return TrimAoaDeg * LiftFraction(verticalCmd, lateralCmd);
        }

        /// <summary>True once there is enough air for the bank command to mean anything.</summary>
        public static bool CanSteer(double dynamicPressureKpa)
        {
            return dynamicPressureKpa > QSteerKpa;
        }

        /// <summary>
        /// The signed distance still to run to the landing zone ALONG THE GROUND TRACK.
        ///
        /// Positive while approaching, through zero overhead, negative once past. The schedule can
        /// never ask for more overshoot than this, and asks for nothing once the target is behind.
        /// </summary>
        public static double AheadM(EntryGuideInputs s)
        {
            double dBearing = (s.LzBearingDeg - s.TrackBearingDeg) * System.Math.PI / 180.0;
            return s.LzRangeM * System.Math.Cos(dBearing);
        }

        /// <summary>One controller step. `mem` is carried by the caller between ticks.</summary>
        public static EntryGuideCommand Update(EntryGuideInputs s, ref EntryMemory mem)
        {
            EntryGuideCommand c = new EntryGuideCommand();

            // The drop latch, before anything else reads it. Both conditions are one-way.
            if (s.MissM >= 0.0 && s.MissM < LzToleranceM) mem.Dropped = true;
            if (s.AltitudeM <= TermAltM) mem.Dropped = true;

            bool rangeLive = !mem.Dropped;
            if (rangeLive)
            {
                // Trap 1: the schedule knows altitude, not geography. Clamp it to what is left.
                // WantLongClampedM IS that clamp - the single live source of the rule. The hand-clamp
                // that used to sit here was an exact duplicate of it (removed 2026-08-18, audit D4;
                // equivalence verified for ahead >, =, <, and <= 0 vs. want before the swap).
                double ahead = AheadM(s);
                double margin = EntryMargin.WantLongClampedM(s.AltitudeM, ahead);
                c.WantLongM = margin;

                double errNow = s.DownrangeErrM + margin;

                // Trap 2: differentiate the ERROR. The scheduled margin falls at nearly the rate the
                // range closes, so their sum moves slowly - tens of m/s - and the lead buys a few
                // hundred metres of damping instead of silently disabling the loop.
                if (!mem.HaveRate)
                {
                    mem.LastError = errNow;
                    mem.HaveRate = true;
                }
                else if (s.DtS >= RateIntervalS)
                {
                    double raw = (errNow - mem.LastError) / s.DtS;
                    mem.FilteredRate = RateFilterOld * mem.FilteredRate
                                     + (1.0 - RateFilterOld) * raw;
                    mem.LastError = errNow;
                }

                // Trap 3: cap the lead at half the error. A derivative damps; it never outvotes.
                double leadRaw = LeadS * mem.FilteredRate;
                double cap = LeadFrac * System.Math.Abs(errNow);
                if (leadRaw > cap) leadRaw = cap;
                if (leadRaw < -cap) leadRaw = -cap;
                double leadErr = errNow + leadRaw;
                c.LeadErrorM = leadErr;

                // Trap 4: shorten or coast. Never extend.
                double cmd = leadErr / DownScaleM;
                if (cmd > 0.0) cmd = 0.0;
                if (cmd < -1.0) cmd = -1.0;
                c.VerticalCmd = cmd;

                if (cmd < mem.LiftMin) mem.LiftMin = cmd;
                if (leadErr > mem.WorstErrorM) mem.WorstErrorM = leadErr;

                if (leadErr > BelowWarnM)
                {
                    c.BelowProfile = true;
                    c.Note = "BELOW PROFILE by " + (leadErr / 1000.0).ToString("F1")
                           + " km - cannot extend; the de-orbit aim was too short";
                }
            }

            // Lateral outlives vertical - outside the range latch on purpose.
            if (s.MissM >= LzToleranceM && s.AltitudeM > LatFloorM)
            {
                double lat = s.CrossTrackM / CrossScaleM;
                if (lat > 1.0) lat = 1.0;
                if (lat < -1.0) lat = -1.0;
                c.LateralCmd = lat;
            }

            if (c.Note == null || c.Note.Length == 0)
                c.Note = rangeLive ? "ENTRY GUIDANCE" : "TERMINAL - range loop latched";
            return c;
        }

        /// <summary>
        /// Did the range loop ever have anything to do?
        ///
        /// A LiftMin of zero at the end means the descent flew OPEN LOOP, and F9I's calibration
        /// history keeps calling out that exact signature: "if x1 reads 0.00 again, the aim is STILL
        /// too low". Worth saying at the end rather than leaving to be spotted by eye.
        /// </summary>
        public static bool FlewOpenLoop(EntryMemory mem)
        {
            return mem.LiftMin > -0.001;
        }
    }
}
