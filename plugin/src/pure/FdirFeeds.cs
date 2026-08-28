// DragonScreen — FdirFeeds  (autopilot rebuild L5 FDIR: honest residual shaping for the monitor feeds)
// ============================================================================================
// Task T2b. The FDIR spine (pure/Fdir.cs) consumes normalised residuals (ThrustDeliveredFrac, PlanProgressRate,
// ControlSolutionOk, …). This turns the RAW live signals the glue gathers off the vessel into those honest
// monitor inputs — with the guards that keep an unmeasurable moment reading NOMINAL instead of false-tripping.
// Kept PURE so every threshold + guard is headless-tested; the glue (FlightDriver / RendezvousControl) only
// reads the KSP values and calls these. Units are explicit on every argument (our #1 bug class).
//
// The honesty rule (why some inputs deliberately return nominal): FDIR must never claim a fault it cannot
// actually measure. A Draco-only burn has no ModuleEngines to read → thrust fraction is UNMEASURABLE → return
// 1.0 (nominal), not 0. An intended rendezvous coast is not a stall → return progress 1.0. Loss-of-control is
// asserted ONLY on the unambiguous no-authority tumble, never on a healthy hard slew (which has authority).
// ============================================================================================
namespace DragonScreen
{
    public static class FdirFeeds
    {
        // Delivered / expected MAIN-ENGINE thrust fraction (1 = nominal, <1 = shortfall). Both sums are over the
        // engines the autopilot has COMMANDED on (EngineIgnited): a flamed-out engine still counts in expectedFull
        // but contributes 0 to actual, so its lost share drops the ratio — the honest engine-out signal.
        //   actualKn       = Σ finalThrust of the operational commanded engines (kN)
        //   expectedFullKn = Σ current-conditions FULL-throttle max of ALL commanded engines (kN)
        //   throttle01     = the commanded main throttle [0,1]
        // Returns 1.0 (nominal, unmeasurable) when no main-engine burn is committed — a coast, or a Draco-only
        // RCS burn (no ModuleEngines) — so those never read as a thrust shortfall.
        public static double ThrustDeliveredFrac(double actualKn, double expectedFullKn, double throttle01)
        {
            if (throttle01 < 0.05) return 1.0;              // no main-engine burn commanded → nothing to measure
            if (expectedFullKn < 1.0) return 1.0;          // no committed main engines (RCS-only burn / coast)
            double expected = throttle01 * expectedFullKn; // healthy delivered ≈ throttle × full-max
            if (expected < 1.0) return 1.0;
            double f = actualKn / expected;
            return f < 0.0 ? 0.0 : f;
        }

        // Loss-of-control (tumble) predicate for the NoControlSolution monitor. TRUE only when ALL hold:
        //   • the loop is ACTIVELY holding attitude (holdingAttitude) — a coast with the loop released is not a fault,
        //   • the best-axis available control authority is essentially ZERO (bestAuthNm < authFloorNm),
        //   • the vehicle is spinning past a tumble rate (spinRads > tumbleRateRads), and
        //   • it is pointing far off target (errDeg > lostErrDeg).
        // That combination is the genuine no-authority tumble (the RCS-GetPotentialTorque-zero case that killed a
        // crew). A healthy hard slew is excluded: it HAS authority, so bestAuthNm is high → never trips. The
        // gimbal-saturation max-Q divergence is caught upstream by AscentControl's q·α / AoA monitors + the
        // structural-g abort, so this stays scoped to the no-authority tumble it can honestly assert.
        public static bool ControlLost(bool holdingAttitude, double bestAuthNm, double spinRads, double errDeg,
                                       double authFloorNm, double tumbleRateRads, double lostErrDeg)
        {
            if (!holdingAttitude) return false;
            return bestAuthNm < authFloorNm && spinRads > tumbleRateRads && errDeg > lostErrDeg;
        }

        // Plan-progress rate fed to the ConvergenceStall monitor. When the controller is ACTIVELY closing on the
        // target, feed the closing rate (+ = closing = progressing, ≤0 = stalled). When it is NOT actively closing
        // (an intended phasing coast, a stationkeeping hold, idle) return a nominal +1 so an intended pause is
        // never mistaken for a frozen plan. The caller decides `activelyClosing` from its own intent — the honest
        // source, since only the controller knows whether it MEANT to be closing right now.
        public static double ClosingProgress(double closingRateMps, bool activelyClosing)
        {
            return activelyClosing ? closingRateMps : 1.0;
        }
    }
}
