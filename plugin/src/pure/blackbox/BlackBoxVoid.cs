// DragonScreen — BlackBox / THE WARP VOID  (register BB1; spec: §4.6, and §3.4's "COMPOSE, improved")
// ============================================================================================
// PURE. On-rails warp turns the physics loop OFF. Every delivered/measured CONTROL value is then a
// FROZEN STALE READ that masquerades as a live measurement, and the record of what that cost is on
// file: stale frozen control values under warp MANUFACTURED A PHANTOM "RCS THRASH" THAT WAS
// INVESTIGATED AS REAL.
//
// ---- THE ONE CHANGE FROM RECORDER B, AND WHY IT IS NOT COSMETIC ----
// Recorder B fixed the phantom by ZEROING these columns (`ZeroControlColumnsForWarp`). §3.4 files that
// as "COMPOSE, IMPROVED" and §4.6 states the improvement: BLANK them instead, BECAUSE A ZERO IS A
// LEGITIMATE CONTROL VALUE AND A BLANK IS NOT. "The throttle was 0 under warp" and "the throttle was
// not measurable under warp" are different facts, and the first one is false. Zeroing swapped one
// wrong conclusion (thrash) for a quieter one (a commanded idle that never happened).
//
// ---- WHAT IS *NOT* VOIDED, WHICH IS THE HALF THAT IS EASY TO GET WRONG ----
// Nav, orbit, mass, MOI, phase, resources, systems and engine state all remain VALID on rails — the
// orbit is exactly what on-rails propagation is for, and resources do not drain on rails, so a
// resource fraction under warp is a true reading of a true state. Voiding them would delete the only
// data a 17-hour phasing coast produces. Only the CONTROL-LOOP signals are frozen, and only they go.
//
// Blanking is done LAST, after every filler has run, so nothing can re-fill a voided cell. The row
// still carries `warp_rails = 1` (§2.1, every row) so a reader distinguishes "voided by warp" from
// "no signal" without inferring anything — which is the point of putting `warp_rate`/`warp_rails` on
// every row in the first place: S76 had to retrofit `is_warp()` filtering into `assess_flight.py`
// because the old corpus made a reader infer it.
// ============================================================================================

namespace DragonScreen.BlackBox
{
    public static class BlackBoxVoid
    {
        /// <summary>
        /// The control-loop columns, resolved once at type-init. Anything a controller or the crew
        /// COMMANDS, anything MEASURED by the physics loop, and anything ACCUMULATED from it.
        /// </summary>
        static readonly int[] Control =
        {
            // applied command (FlightCtrlState) + throttle
            BlackBoxCols.Throttle,
            BlackBoxCols.AppPitch, BlackBoxCols.AppYaw, BlackBoxCols.AppRoll,
            BlackBoxCols.AppTx, BlackBoxCols.AppTy, BlackBoxCols.AppTz,
            // requested command + the attitude loop (Unfitted today; voided anyway, so the rule does
            // not have to be revisited on the day T17 fills them)
            BlackBoxCols.ActPitch, BlackBoxCols.ActYaw, BlackBoxCols.ActRoll,
            BlackBoxCols.AttErrDeg, BlackBoxCols.AttRateCmd,
            // the booster steering block — same argument, different vessel
            BlackBoxCols.BoostSteerPitch, BlackBoxCols.BoostSteerYaw, BlackBoxCols.BoostSteerRoll,
            BlackBoxCols.BoostThrottle,
            BlackBoxCols.BoostDbPitch, BlackBoxCols.BoostDbYaw, BlackBoxCols.BoostDbRoll,
            // measured control response: body rates and the accelerations they produce
            BlackBoxCols.AttRateMeas,
            BlackBoxCols.RatePitchDps, BlackBoxCols.RateRollDps, BlackBoxCols.RateYawDps,
            BlackBoxCols.AccelG, BlackBoxCols.AccelAxialG,
            // delivered propulsion + authority
            BlackBoxCols.ThrustN, BlackBoxCols.RcsThrustN,
            BlackBoxCols.CtrlTqPitch, BlackBoxCols.CtrlTqYaw, BlackBoxCols.CtrlTqRoll,
            // aero: q and the angles are computed from a velocity the physics loop is not integrating
            BlackBoxCols.QPa, BlackBoxCols.AoaDeg, BlackBoxCols.AosDeg,
            // every R0 accumulator — they accumulate over an interval whose physics did not run
            BlackBoxCols.AccIntS, BlackBoxCols.AccAttS, BlackBoxCols.AccTransS,
            BlackBoxCols.AccBothS, BlackBoxCols.AccNoneS,
            BlackBoxCols.AccAppAtt, BlackBoxCols.AccAppTrans, BlackBoxCols.AccReqAtt, BlackBoxCols.AccReqTrans,
            BlackBoxCols.ActSatS,
            BlackBoxCols.AccelGPeak, BlackBoxCols.QPaPeak, BlackBoxCols.RatePeakDps,
        };

        /// <summary>Number of columns the warp rule voids. Exposed so a test can assert it is not zero.</summary>
        public static int ControlColumnCount { get { return Control.Length; } }

        public static bool IsControlColumn(int col)
        {
            for (int i = 0; i < Control.Length; i++) if (Control[i] == col) return true;
            return false;
        }

        /// <summary>
        /// Blank every control column. Call LAST, after all fillers, only when on-rails warp is active.
        /// Assigning "" directly rather than via `Set(..., double.NaN)` because the intent is "this cell
        /// holds nothing", not "this cell holds a number that failed to be a number" — the two paths
        /// produce the same byte and only one of them says what it means.
        /// </summary>
        public static void Apply(string[] cells)
        {
            if (cells == null) return;
            for (int i = 0; i < Control.Length; i++)
            {
                int col = Control[i];
                if (col >= 0 && col < cells.Length) cells[col] = "";
            }
        }
    }
}
