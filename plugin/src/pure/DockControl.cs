// DragonScreen — DockControl  (autopilot rebuild L3 docking: the 6-DOF glideslope servo)
// ============================================================================================
// Terminal relative control for the final approach (PHASE_4_DOCKING_RESEARCH §5b). A per-axis
// position+velocity servo in the station LVLH frame with a CLOSING-SPEED CAP that tapers with range —
// fast far out, down to the ~8 cm/s contact speed at the port — so the approach is slow and monotone and
// stays abortable. Lateral offsets off the docking axis are nulled the same way. Output is a translation
// acceleration the glue turns into Draco s.X/Y/Z; L2 clamps it. Attitude (aligning the docking ring to
// the port) is the L2 attitude law pointed by the FSM — done FIRST, then this translates (full control).
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class DockControl
    {
        // Closing-speed cap by range to the target: contact speed at the port, ramping up to farSpeed by
        // taperRange. Keeps the final metres at cm/s while allowing faster transit between waypoints.
        public static double SpeedCap(double rangeM, double contactSpeedMps, double farSpeedMps, double taperRangeM)
        {
            if (taperRangeM <= 0.0) return contactSpeedMps;
            double f = rangeM / taperRangeM;
            if (f < 0.0) f = 0.0; else if (f > 1.0) f = 1.0;
            return contactSpeedMps + (farSpeedMps - contactSpeedMps) * f;
        }

        // Per-axis glideslope acceleration: command a velocity toward the target (−kPos·err) capped at
        // vMax, then close the velocity error (kVel·(vCmd−rate)). Drives err→0 with a bounded closing speed.
        public static double Accel(double errM, double rateMps, double vMaxMps, double kPos, double kVel)
        {
            double vCmd = -kPos * errM;
            if (vCmd > vMaxMps) vCmd = vMaxMps; else if (vCmd < -vMaxMps) vCmd = -vMaxMps;
            return kVel * (vCmd - rateMps);
        }

        // The full 6-DOF translation demand toward a target point (LVLH), with the closing cap applied by
        // total range. errAxis/lat are (position − target); rate* are the relative velocity components.
        public struct Demand { public double Radial, Along, Cross; public double ClosingCapMps; }

        public static Demand Translate(double errRadial, double errAlong, double errCross,
                                       double rateRadial, double rateAlong, double rateCross,
                                       double contactSpeedMps, double farSpeedMps, double taperRangeM,
                                       double kPos, double kVel)
        {
            double range = Math.Sqrt(errRadial * errRadial + errAlong * errAlong + errCross * errCross);
            double vMax = SpeedCap(range, contactSpeedMps, farSpeedMps, taperRangeM);
            Demand d;
            d.ClosingCapMps = vMax;
            d.Radial = Accel(errRadial, rateRadial, vMax, kPos, kVel);
            d.Along = Accel(errAlong, rateAlong, vMax, kPos, kVel);
            d.Cross = Accel(errCross, rateCross, vMax, kPos, kVel);
            return d;
        }
    }
}
