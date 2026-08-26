// DragonScreen — Hoverslam  (autopilot rebuild L3 booster: the landing-burn ignition solver)
// ============================================================================================
// Built fresh from the research (PHASE_2_BOOSTER_RECOVERY_RESEARCH §6/§8.1, BOOSTER_GUIDANCE_DESIGN).
// The stage cannot hover — even one Merlin at ~40% min throttle out-thrusts the near-empty stage — so
// landing is a HOVERSLAM: one continuous max-thrust brake timed to reach v=0 exactly at h=0, igniting as
// LATE as possible so aero drag does maximum free braking. The ignition altitude is the drop consumed by
// (a) the ullage-settle DEAD TIME the stage free-falls before real thrust, (b) the spool ramp, then
// (c) the braking distance under thrust − gravity + drag. Solved numerically with the measured drag so
// it is correct for the real stage. Engine mode is the FEWEST engines that can still arrest (3 → 1), the
// centre engine flying the touchdown. Sources cite §5.0: Merlin spool is instant (throttleResponseRate),
// so the dead time is ullage-settle, not spool.
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct HoverslamInputs
    {
        public double AltitudeM;         // height above the deck
        public double DescentSpeedMps;   // current descent speed (positive magnitude)
        public double ThrustAccelMps2;   // full-thrust acceleration of the braking engines (F/m)
        public double GravityMps2;
        public double TerminalSpeedMps;  // measured terminal fall speed (drag == gravity there)
        public double DeadTimeS;         // ullage-settle dead-fall before real thrust
        public double SpoolS;            // thrust ramp (≈0 for the instant-spool Merlin)
    }

    public static class Hoverslam
    {
        const double Dt = 0.05;

        // Altitude at which to IGNITE so a full brake (after the dead-time and spool) nulls v at h=0.
        // The caller lights the engines when the true altitude falls to this value.
        public static double IgnitionAltitude(HoverslamInputs s)
        {
            double v = Math.Abs(s.DescentSpeedMps);
            double a = s.ThrustAccelMps2, g = s.GravityMps2;
            if (v < 1.0) return 0.0;                 // not descending → nothing to solve
            if (a <= g) return s.AltitudeM;          // cannot decelerate against gravity → light now

            double vterm = s.TerminalSpeedMps > 1.0 ? s.TerminalSpeedMps : v;
            double h = 0.0, vv = v;

            // (a) DEAD TIME: engines lit but ullage settling → free-fall under gravity minus drag.
            for (double t = 0.0; t < s.DeadTimeS; t += Dt)
            {
                double drag = g * (vv * vv) / (vterm * vterm);   // drag == g at terminal, ∝ v²
                vv += (g - drag) * Dt;
                if (vv < 0.0) vv = 0.0;
                h += vv * Dt;
            }

            // (b)+(c) BRAKE: thrust ramps over the spool, then full; drag + thrust decelerate, gravity adds.
            for (double t = 0.0; vv > 0.0 && t < 300.0; t += Dt)
            {
                double thr = s.SpoolS > 1e-6 ? Math.Min(1.0, t / s.SpoolS) : 1.0;
                double drag = g * (vv * vv) / (vterm * vterm);
                double netDown = g - drag - a * thr;             // <0 while braking
                vv += netDown * Dt;
                if (vv < 0.0) vv = 0.0;
                h += vv * Dt;
            }
            return h;
        }

        // The stop altitude if the stage ignites at hIgnite with the current descent speed — should be ~0
        // when hIgnite == IgnitionAltitude. (Used to verify the solver and as a live safety check.)
        public static double StopAltitude(double hIgnite, HoverslamInputs s)
        {
            return hIgnite - IgnitionAltitude(s);
        }

        // Fewest engines that can still arrest from here: try the centre engine first, then three. Returns
        // 1 or 3 (0 if even three cannot stop → the caller flags an un-recoverable landing to FDIR).
        public static int EnginesFor(HoverslamInputs sOne, HoverslamInputs sThree)
        {
            // if one engine can ignite low enough that its ignition altitude is under the current altitude
            // with margin, one engine suffices; otherwise three are needed to arrest the speed.
            if (sOne.ThrustAccelMps2 > sOne.GravityMps2 && IgnitionAltitude(sOne) < sOne.AltitudeM)
                return 1;
            if (sThree.ThrustAccelMps2 > sThree.GravityMps2)
                return 3;
            return 0;
        }
    }
}
