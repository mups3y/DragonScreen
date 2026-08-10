/*
 * DragonScreen - BurnExec
 *
 * PURE. The node-execution law, ported from `F9I/station_ops.ks:2469 StBurnNode` (which is itself
 * F9I's port of `DgExecNode` from dragon_deorbit.ks, with a proportional throttle instead of a
 * locked 1.0).
 *
 * ---- ⛔ THIS FILE WAS MINE AND IS NOW A PORT. THE THROTTLE LAW CHANGED. ----
 * The previous version was written from the idea of a node executor rather than from F9I's, and its
 * throttle law was not F9I's. Replaced wholesale per the port plan.
 *
 * ---- WHY THE THROTTLE IS PROPORTIONAL AND NOT LOCKED AT 1 ----
 * F9I's own note: DgExecNode holds `lock throttle to 1.` and stays that way, and that is right for
 * the de-orbit, which is calibrated on it. On the approach it is not: "580 kN against ~20 t is
 * 28 m/s^2, so a 10 m/s CW burn is over in 0.36 s of full blast and the engine slams on and off."
 * So: hold a cruise ACCELERATION, then taper the last of the dv over a few seconds so the burn ends
 * at near-zero thrust rather than being chopped off. Same dv, smoothly delivered.
 *
 * ---- AND WHY THE HALF-BURN LEAD IS SIZED ON THE ACCELERATION WE WILL USE ----
 * Not on full thrust. F9I: sized at full thrust "we would arrive late for our own burn", because the
 * burn actually runs at the cruise acceleration and therefore takes longer than full thrust implies.
 *
 * ---- THE STOP CONDITION IS AN OVERSHOOT TEST, NOT A COUNTDOWN ----
 * `vdot(dir0, remaining) < 0` - the remaining dv has REVERSED against where it pointed at ignition.
 * A burn that stops on a timer or on a dv total keeps pushing through the target when the estimate
 * was wrong; this one cannot.
 */
namespace DragonScreen
{
    public struct BurnState
    {
        /// <summary>Δv still to deliver, m/s.</summary>
        public double RemainingDvMps;
        /// <summary>Δv the node asked for at ignition, m/s.</summary>
        public double InitialDvMps;
        /// <summary>Vehicle mass, tonnes.</summary>
        public double MassT;
        /// <summary>Thrust available right now, kN. Zero means nothing is lit.</summary>
        public double AvailableThrustKn;
        /// <summary>Angle between the nose and the Δv direction, degrees.</summary>
        public double PointingErrorDeg;
        /// <summary>
        /// True once the remaining Δv has reversed against its direction at ignition - we have
        /// burned past the node and must stop.
        /// </summary>
        public bool Overshot;
        /// <summary>Seconds since ignition. The runaway backstop is on this.</summary>
        public double ElapsedS;
    }

    public static class BurnExec
    {
        // ---- F9I's CONSTANTS. station_ops.ks:597-681. ----
        /// <summary>Cruise acceleration for approach burns, m/s^2. `stBurnAccel`. TUNABLE.</summary>
        public const double CruiseAccel = 1.5;
        /// <summary>Taper the last of the Δv over this long, seconds. `stBurnTaper`. TUNABLE.</summary>
        public const double TaperS = 4.0;
        /// <summary>Never command less than this while burning. `stThrMin`.</summary>
        public const double ThrottleMin = 0.01;
        /// <summary>Stop when this little Δv remains, m/s. `stBurnStop`.</summary>
        public const double StopDvMps = 0.05;
        /// <summary>Finish the turn this long before ignition, seconds. `stAlignPad`.</summary>
        public const double AlignPadS = 2.0;
        /// <summary>Inside this many seconds to ignition, buy RCS to finish the turn. `stAlignRcsBelow`.</summary>
        public const double AlignRcsBelowS = 45.0;
        /// <summary>Aligned enough to ignite, degrees. StBurnNode's `vang(...) < 3`.</summary>
        public const double AlignedDeg = 3.0;
        /// <summary>Runaway backstop, seconds. StBurnNode's `stEndT is time:seconds + 300`.</summary>
        public const double MaxBurnDurationS = 300.0;

        /// <summary>
        /// The acceleration the burn will actually run at: the cruise figure, or all we have if that
        /// is less. Floored so the lead below cannot divide by nothing.
        /// </summary>
        public static double BurnAccel(double massT, double availableThrustKn)
        {
            double have = availableThrustKn / (massT > 0.1 ? massT : 0.1);
            double a = (CruiseAccel < have) ? CruiseAccel : have;
            return (a < 0.05) ? 0.05 : a;
        }

        /// <summary>
        /// Half the burn happens BEFORE the node time, so the impulse straddles it.
        ///
        /// ⚠ Sized on <see cref="BurnAccel"/>, not on full thrust. F9I is explicit: at full thrust
        /// "we would arrive late for our own burn".
        /// </summary>
        public static double HalfBurnS(double dvMps, double massT, double availableThrustKn)
        {
            return dvMps / (2.0 * BurnAccel(massT, availableThrustKn));
        }

        /// <summary>Latest moment the turn may still be running, as an offset from ignition.</summary>
        public static double AlignDeadlineS(double secondsToIgnition)
        {
            return secondsToIgnition - AlignPadS;
        }

        /// <summary>Pointed close enough to light the engine?</summary>
        public static bool Aligned(double pointingErrorDeg)
        {
            return pointingErrorDeg < AlignedDeg;
        }

        /// <summary>
        /// Should RCS be bought to finish the turn?
        ///
        /// ⚠ RCS IS THE DE-ORBIT AND LANDING BUDGET. F9I: "It is not free attitude authority, so it
        /// is bought only when the clock says reaction wheels alone will not finish the turn in
        /// time." Long coast: wheels. Short coast: a little RCS, cheaper than missing the match
        /// point and re-planning.
        /// </summary>
        public static bool NeedRcsToAlign(double secondsToIgnition, double pointingErrorDeg)
        {
            return !Aligned(pointingErrorDeg) && secondsToIgnition < AlignRcsBelowS;
        }

        /// <summary>
        /// The throttle. `min(cruise, remaining/taper)` as an acceleration, converted against the
        /// CURRENT mass and thrust, floored so the engine never sits at literally zero mid-burn.
        ///
        /// Far from the end the cruise term wins and the throttle is steady; inside the last
        /// <see cref="TaperS"/> seconds the second term takes over and walks it down to nothing.
        /// </summary>
        public static double Throttle(BurnState s)
        {
            if (s.AvailableThrustKn <= 0.0 || s.MassT <= 0.0) return 0.0;
            if (Complete(s)) return 0.0;

            double wantA = s.RemainingDvMps / TaperS;
            if (CruiseAccel < wantA) wantA = CruiseAccel;

            double th = wantA * s.MassT / (s.AvailableThrustKn > 1.0 ? s.AvailableThrustKn : 1.0);
            if (th > 1.0) th = 1.0;
            if (th < ThrottleMin) th = ThrottleMin;
            return th;
        }

        /// <summary>
        /// Is the burn over? Overshoot, or the remainder is inside the stop threshold, or the
        /// backstop has fired.
        ///
        /// The overshoot test is the important one: a burn that stops on a countdown keeps pushing
        /// through the target whenever the estimate was wrong.
        /// </summary>
        public static bool Complete(BurnState s)
        {
            if (s.Overshot) return true;
            if (s.RemainingDvMps < StopDvMps) return true;
            return s.ElapsedS > MaxBurnDurationS;
        }

        /// <summary>Why it ended, for the log. Empty while it is still running.</summary>
        public static string CompletionNote(BurnState s)
        {
            if (s.Overshot) return "burned past the node";
            if (s.RemainingDvMps < StopDvMps) return "residual inside the stop threshold";
            if (s.ElapsedS > MaxBurnDurationS) return "ABORTED - burn ran past its backstop";
            return "";
        }
    }
}
