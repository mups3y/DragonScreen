// DragonScreen — GridFin  (autopilot rebuild L3 booster: grid-fin aero descent steering)
// ============================================================================================
// Built fresh from the research (PHASE_2_BOOSTER_RECOVERY_RESEARCH §5/§8b). The booster falls engines-
// first (retrograde) and STEERS by flying at a small, DELIBERATE angle of attack: the body lift it
// generates is pointed toward the target to null the predicted-impact error (L1 predictor). AoA
// magnitude ∝ the error (with a lead term to anticipate the aero lag), capped at ~20° — a controlled,
// held angle, NEVER an uncontrolled drift. The tilt DIRECTION points the corrective lift toward −error
// (magnitude → downrange, direction/bank → crossrange). The booster grid fins (SyncModuleControlSurface)
// hold this AoA + bank; L2 attitude closes the loop. Offset-to-miss (aim beside the deck until all
// systems nominal) is applied by the caller as a target bias, so a failed steer lands in the water.
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct GridFinInputs
    {
        public double DownrangeErrM;      // predicted impact − target, along the ground track (+ = long)
        public double CrossrangeErrM;     // perpendicular to the ground track (+ = to the +cross side)
        public double DownrangeRateMps;   // error rates, for the lead term
        public double CrossrangeRateMps;
        public double LeadTauS;           // anticipate the aero lag: steer on e + τ·ė
        public double AoaMaxDeg;          // cap (real: up to ~20°)
        public double GainDegPerKm;       // commanded AoA per km of horizontal error
    }

    public struct GridFinCommand
    {
        public double AoaDeg;             // 0..AoaMax — the deliberate angle of attack to hold
        public double TiltDown, TiltCross; // unit direction (down/cross plane) to tilt the nose toward
    }

    public static class GridFin
    {
        public static GridFinCommand Steer(GridFinInputs s)
        {
            GridFinCommand c = new GridFinCommand();

            double ed = s.DownrangeErrM + s.LeadTauS * s.DownrangeRateMps;   // lead-compensated error
            double ec = s.CrossrangeErrM + s.LeadTauS * s.CrossrangeRateMps;
            double mag = Math.Sqrt(ed * ed + ec * ec);
            if (mag < 1.0) { c.AoaDeg = 0.0; c.TiltDown = 0.0; c.TiltCross = 0.0; return c; }

            double aoa = s.GainDegPerKm * mag / 1000.0;
            if (aoa < 0.0) aoa = 0.0;
            if (s.AoaMaxDeg > 0.0 && aoa > s.AoaMaxDeg) aoa = s.AoaMaxDeg;   // controlled, capped — no wild AoA
            c.AoaDeg = aoa;

            // point the corrective lift toward −error (steer the impact back toward the target).
            c.TiltDown = -ed / mag;
            c.TiltCross = -ec / mag;
            return c;
        }
    }
}
