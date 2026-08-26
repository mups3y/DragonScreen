// DragonScreen — FaultMonitor  (autopilot rebuild L5 FDIR: the shared detect+debounce primitive)
// ============================================================================================
// The one monitor primitive every FDIR check is built on (docs/TRUE_AUTOPILOT_ARCHITECTURE.md §9): a
// residual (expected − actual) crossing a threshold TRIPS a fault — but only after it PERSISTS for a
// confirmation time, and it clears on a separate, LOWER hysteresis threshold held for a clear time, so it
// does not FLAP around the threshold. Timing is in ELAPSED REAL TIME (dt seconds), never tick counts, so
// it means the same thing under time-warp or at any frame rate.
//
//   over  = residual is above the TRIP threshold      → accrue hot time; trip at confirmS
//   under = residual is below the CLEAR threshold      → accrue cool time; clear at clearS (if tripped)
//   between (the hysteresis band) = hold the current state, reset both timers
//
// PURE + deterministic. FaultMonitor (not `Monitor`) to avoid any confusion with System.Threading.Monitor.
// ============================================================================================
namespace DragonScreen
{
    public struct MonitorState
    {
        public bool Tripped;
        public double HotS;    // seconds the fault has been asserted (over the trip threshold)
        public double CoolS;   // seconds it has been clear (under the hysteresis threshold)
    }

    public static class FaultMonitor
    {
        // Advance one monitor by dt. `over`/`under` are the two threshold tests (the caller applies the
        // trip and the lower hysteresis threshold to the residual). Returns the debounced Tripped state.
        public static bool Update(ref MonitorState m, bool over, bool under, double dt,
                                  double confirmS, double clearS)
        {
            if (dt <= 0.0) return m.Tripped;

            if (over)
            {
                m.CoolS = 0.0;
                m.HotS += dt;
                if (m.HotS >= confirmS) m.Tripped = true;
            }
            else if (under)
            {
                m.HotS = 0.0;
                m.CoolS += dt;
                if (m.Tripped && m.CoolS >= clearS) m.Tripped = false;
            }
            else
            {
                // in the deadband between the two thresholds — hold the state, stop accruing.
                m.HotS = 0.0;
                m.CoolS = 0.0;
            }
            return m.Tripped;
        }

        public static void Reset(ref MonitorState m) { m.Tripped = false; m.HotS = 0.0; m.CoolS = 0.0; }
    }
}
