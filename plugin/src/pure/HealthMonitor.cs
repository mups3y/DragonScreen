/*
 * DragonScreen - HealthMonitor
 *
 * PURE. The fault-DETECTION primitive of the Layer-3 autonomy stack (docs/LAYER3_AUTONOMY_PLAN.md).
 * One monitor watches one quantity and answers a single question the rest of the autopilot can act
 * on: is this system Nominal, Degraded or Failed?
 *
 * ---- THIS IS TEXTBOOK FDIR, AND THE SHAPE IS DELIBERATE ----
 * Fault Detection, Isolation and Recovery is how real spacecraft GN&C notices it is in trouble
 * (NASA/ESA FDIR; adcs-introduction.readthedocs.io; the model-based-FDIR survey literature). The
 * detector is always the same three steps:
 *
 *      RESIDUAL      expected - actual                (how far off are we?)
 *      EVALUATION    |residual| > threshold           (is that far enough to matter?)
 *      DECISION      a Nominal/Degraded/Failed verdict (what do we call it?)
 *
 * with two guards that stop it firing on noise, which every flight-real FDIR has and a naive
 * threshold does not:
 *
 *   CONFIRMATION TIME  the residual must stay over the threshold for a set TIME before the verdict
 *                      latches. A single bad frame - a physics glitch, one off-axis tick while a
 *                      slew settles - must never command a recovery. (Real FDIR calls this the
 *                      persistence / confirmation filter.)
 *   HYSTERESIS         a SEPARATE, lower clear threshold to recover. If the alarm cleared at the
 *                      same level it tripped, a residual sitting right on the line would flap between
 *                      Failed and Nominal every frame and no responder could act on it.
 *
 * ---- WHY TIME, NOT A TICK COUNT ----
 * The plan's first sketch debounced on "N consecutive ticks". Ticks are wrong here for the same
 * reason Trajectory.SmoothBc is written frame-rate-independent: this autopilot runs under time warp
 * and at whatever physics rate the machine gives, so "3 ticks" is a different amount of real time
 * every flight. Confirmation is a DURATION (seconds) and the caller passes dt, exactly as the burn
 * accounting integrates finalThrust over dt. A monitor tuned to "0.6 s of wrong-way thrust" then
 * means the same thing at 50 Hz, at 5 Hz, and at 4x warp.
 *
 * ---- STATELESS LAW, CALLER-OWNED STATE (the Attitude.cs discipline) ----
 * A monitor needs memory (the confirmation clock, the latched verdict), and this project has lost a
 * flight to a controller carrying stale state across a vehicle change (see Attitude.cs). So the state
 * is an explicit struct the CALLER holds and can RESET on a phase transition - Step() is a pure
 * function of (previous state, config, residual, dt). Nothing is hidden between calls; a monitor that
 * should forget the last phase is reset by the glue that owns it, not by a timer it cannot see.
 *
 * A monitor here decides ONLY the verdict. What to DO about it is FaultResponse.cs; the residual for
 * a given system is its own small concrete monitor (ThrustDeliveryMonitor is the first). This file is
 * just the evaluate-and-debounce core they all share.
 */
namespace DragonScreen
{
    /// <summary>A monitor's confirmed opinion of the system it watches. Ordered by severity, so a
    /// caller comparing two verdicts can take the worse one with a plain <c>&gt;</c>.</summary>
    public enum HealthVerdict : byte { Nominal = 0, Degraded = 1, Failed = 2 }

    /// <summary>
    /// The thresholds and confirmation times for one monitor. Constant thresholds on purpose - the
    /// FDIR literature's own note is that a constant threshold buys simplicity and reliability, and a
    /// self-tuning threshold is a second thing that can be wrong. The residual passed to Step is a
    /// SCALAR shaped by the concrete monitor so that positive magnitude means "worse"; these
    /// thresholds are on its absolute value.
    /// </summary>
    public struct MonitorConfig
    {
        /// <summary>|residual| at or above this is at least Degraded. 0 disables the Degraded band.</summary>
        public double DegradeThreshold;
        /// <summary>|residual| at or above this is Failed. 0 disables the Failed band.</summary>
        public double FailThreshold;
        /// <summary>|residual| must fall BELOW this (and stay, for ClearS) before the verdict recovers
        /// to Nominal. Lower than DegradeThreshold - that gap is the hysteresis.</summary>
        public double ClearThreshold;
        /// <summary>Seconds a worse-than-current residual must persist before the verdict escalates.
        /// The persistence filter: below it, a transient cannot latch a fault.</summary>
        public double ConfirmS;
        /// <summary>Seconds the residual must stay below ClearThreshold before the verdict de-latches
        /// to Nominal. Recovery is confirmed too, so a momentary dip does not clear a real fault.</summary>
        public double ClearS;
    }

    /// <summary>
    /// One monitor's carried state. Zero value is a healthy monitor (Nominal, no clocks running), so
    /// <c>default(MonitorState)</c> / <see cref="HealthMonitor.Fresh"/> is the reset the glue uses on
    /// a phase change. Residual and Raw are kept for the recorder - the fd_ block logs both the raw
    /// this-tick reading and the debounced verdict, so a flight shows WHY a responder did or did not act.
    /// </summary>
    public struct MonitorState
    {
        /// <summary>The debounced, latched verdict - the one a responder acts on.</summary>
        public HealthVerdict Verdict;
        /// <summary>This tick's verdict BEFORE debounce. For the recorder: Raw flickering while Verdict
        /// holds is the confirmation filter doing its job.</summary>
        public HealthVerdict Raw;
        /// <summary>The last residual fed in, signed as the monitor produced it. For the recorder.</summary>
        public double Residual;
        /// <summary>Seconds accumulated toward escalating (the confirmation clock).</summary>
        public double OverS;
        /// <summary>Seconds accumulated toward clearing.</summary>
        public double UnderS;
    }

    public static class HealthMonitor
    {
        /// <summary>A healthy, reset monitor. Use on a phase transition so a fault from the last phase
        /// cannot survive into the next - the staleness guard the whole caller-owned-state design buys.</summary>
        public static MonitorState Fresh() { return new MonitorState(); }

        /// <summary>Build a config without depending on field order.</summary>
        public static MonitorConfig Config(double degrade, double fail, double clear,
                                           double confirmS, double clearS)
        {
            MonitorConfig c = new MonitorConfig();
            c.DegradeThreshold = degrade;
            c.FailThreshold = fail;
            c.ClearThreshold = clear;
            c.ConfirmS = confirmS;
            c.ClearS = clearS;
            return c;
        }

        /// <summary>The instantaneous verdict for a residual magnitude - the EVALUATION step, before
        /// any debounce. A monitor with a zeroed band never reports that band.</summary>
        public static HealthVerdict Classify(double absResidual, MonitorConfig cfg)
        {
            if (cfg.FailThreshold > 0.0 && absResidual >= cfg.FailThreshold) return HealthVerdict.Failed;
            if (cfg.DegradeThreshold > 0.0 && absResidual >= cfg.DegradeThreshold) return HealthVerdict.Degraded;
            return HealthVerdict.Nominal;
        }

        /// <summary>
        /// Advance one monitor by <paramref name="dt"/> seconds with a fresh <paramref name="residual"/>.
        /// Pure: same inputs, same output; the caller owns the returned state.
        ///
        /// Escalation and recovery are both TIME-CONFIRMED and asymmetric on purpose:
        ///   - The verdict only WORSENS after the residual has been over the threshold for ConfirmS.
        ///   - It only RECOVERS to Nominal after the residual has been under the (lower) ClearThreshold
        ///     for ClearS. Recovery goes straight to Nominal rather than stepping down a level - it only
        ///     happens when the residual is genuinely small, so there is no lingering Degraded to hold.
        ///   - In the band between the clear and degrade thresholds the latched verdict simply HOLDS,
        ///     and neither clock advances toward a change.
        /// </summary>
        public static MonitorState Step(MonitorState prev, MonitorConfig cfg, double residual, double dt)
        {
            if (dt < 0.0) dt = 0.0;

            MonitorState s = prev;
            s.Residual = residual;
            double mag = residual < 0.0 ? -residual : residual;
            HealthVerdict raw = Classify(mag, cfg);
            s.Raw = raw;

            bool clear = mag < cfg.ClearThreshold;

            if ((byte)raw > (byte)s.Verdict)
            {
                // A worse reading than the latched verdict. Run the confirmation clock; only latch when
                // it has persisted long enough that it cannot be a single-frame transient.
                s.OverS += dt;
                s.UnderS = 0.0;
                if (s.OverS >= cfg.ConfirmS) { s.Verdict = raw; s.OverS = 0.0; }
            }
            else if (clear && s.Verdict != HealthVerdict.Nominal)
            {
                // Genuinely healthy again (below the hysteresis floor). Confirm the recovery too.
                s.UnderS += dt;
                s.OverS = 0.0;
                if (s.UnderS >= cfg.ClearS) { s.Verdict = HealthVerdict.Nominal; s.UnderS = 0.0; }
            }
            else
            {
                // Steady at or below the current verdict, but not clearly recovered - hold, and let
                // neither partial clock carry over into a later change.
                s.OverS = 0.0;
                if (!clear) s.UnderS = 0.0;
            }
            return s;
        }

        /// <summary>The worse of two verdicts. Lets a caller fuse several monitors into one system
        /// health without caring about the enum's numbers.</summary>
        public static HealthVerdict Worst(HealthVerdict a, HealthVerdict b)
        {
            return (byte)a >= (byte)b ? a : b;
        }

        public static string Name(HealthVerdict v)
        {
            switch (v)
            {
                case HealthVerdict.Degraded: return "DEGRADED";
                case HealthVerdict.Failed:   return "FAILED";
                default:                     return "NOMINAL";
            }
        }
    }
}
