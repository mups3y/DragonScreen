// DragonScreen — IgnitionGate  (PURE: the clamp-release + ullage-settle decisions)
// ============================================================================================
// The two go/no-go gates around lighting an engine, decided in pure code so they are headless-tested:
//
//  • CLAMP RELEASE (pad): after the octaweb is commanded lit, HOLD the hold-downs until the measured
//    thrust reaches ≥99% of available with at least one engine lit — then release. If thrust has not
//    come up within MaxHoldS (Merlin spool is INSTANT, so this only trips on a failed light), SAFE-ABORT:
//    shut the engine down and keep the clamps — never release onto a bad engine (the flight-1/2 fix, plan §3.4).
//
//  • ULLAGE SETTLE (every relight in flight): RealFuels needs the propellant settled before ignition. Fire
//    the aft RCS until the ullage stability ≥0.996, then light (MechJeb ProcessUllage, plan §3.3). A minimum
//    coast lets the spent stage physically clear first; a maximum settle time is a best-effort backstop.
// ============================================================================================
namespace DragonScreen
{
    public enum ClampAction { Hold, Release, SafeAbort }

    public static class IgnitionGate
    {
        // ---- clamp release ----
        public const double ReleaseThrustFrac = 0.99;   // release only at ≥99% of available thrust
        public const double MaxHoldS = 2.0;             // spool is instant; still low by now ⇒ a light failed

        public static ClampAction Evaluate(double thrustN, double availableN, int litCount, double heldS)
        {
            if (availableN > 1.0 && litCount >= 1 && thrustN >= ReleaseThrustFrac * availableN)
                return ClampAction.Release;
            if (heldS > MaxHoldS)
                return ClampAction.SafeAbort;           // failed to reach thrust in time — keep clamps, shut down
            return ClampAction.Hold;
        }

        // ---- ullage settle ----
        public const double UllageStable = 0.996;       // RealFuels stability threshold to allow ignition

        // Ready to light: past the minimum separation coast AND (propellant settled OR the settle backstop hit).
        public static bool UllageReady(double stability, double settledS, double minCoastS, double maxSettleS)
        {
            if (settledS <= minCoastS) return false;
            return stability >= UllageStable || settledS > maxSettleS;
        }
    }
}
