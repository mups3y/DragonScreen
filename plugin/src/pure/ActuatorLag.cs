// DragonScreen — ActuatorLag  (autopilot rebuild B4: actuator-lag model + lead compensation)
// ============================================================================================
// Gimbals (and, less so, RCS) do not reach a commanded deflection instantly — the servo slews at a finite
// response speed, so the TORQUE lags the command. A control loop blind to this over-commands (the plant has
// not caught up) → sluggishness and oscillation, worst through max-Q. The fix (docs/MECHJEB_CAPABILITY_
// INTEGRATION.md P0, MechJeb's TorqueReactionSpeed / PIDLoop SmoothIn-SmoothOut): model the actuator as a
// first-order lag and COMMAND HARDER when it is slow, so the lagged response still reaches the demand.
//
//   model (track the actual deflection):   a' = a + α·(cmd − a),   α = clamp(dt·responseSpeed, 0, 1)
//   lead comp (invert the model for cmd):  cmd = a + (desired − a)/α,  clamped to [−1,1]
//
// α→1 (fast actuator): cmd ≈ desired, no over-command. α small (slow): cmd is amplified toward the rail — the
// "command harder when slow" behavior, bounded. Pure + headless-tested; the glue tracks the per-axis actual
// deflection with Step() and issues Compensate()'d commands, feeding responseSpeed from the live gimbal
// (ModuleGimbal.gimbalResponseSpeed) / RCS. responseSpeed here is 1/τ in per-second units (gap-closing rate).
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class ActuatorLag
    {
        // First-order actuator step: the actual deflection closes a fraction α of the gap to the command each
        // tick. Use to TRACK the live actuator state between ticks. responseSpeedPerS = 1/τ (gap-closing rate).
        public static double Step(double actual, double command, double responseSpeedPerS, double dt)
        {
            double alpha = Clamp01(dt * responseSpeedPerS);
            return actual + alpha * (command - actual);
        }

        // Lead compensation: the command to ISSUE so the lagged actuator reaches `desired` this tick — the
        // inverse of Step. Commands harder when the actuator is slow (small α), clamped to the [−1,1] rail. A
        // frozen actuator (α≈0) slams toward the demand. desired==actual → no change.
        public static double Compensate(double actual, double desired, double responseSpeedPerS, double dt)
        {
            double alpha = Clamp01(dt * responseSpeedPerS);
            if (desired == actual) return actual;
            if (alpha < 1e-6) return desired > actual ? 1.0 : -1.0;   // frozen: drive to the rail toward the demand
            double cmd = actual + (desired - actual) / alpha;
            if (cmd > 1.0) return 1.0;
            if (cmd < -1.0) return -1.0;
            return cmd;
        }

        static double Clamp01(double x) { if (x < 0.0) return 0.0; if (x > 1.0) return 1.0; return x; }
    }
}
