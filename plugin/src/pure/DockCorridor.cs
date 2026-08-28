// DragonScreen — DockCorridor  (autopilot rebuild L3 docking: the approach-corridor / KOS-breach test)
// ============================================================================================
// Real Crew Dragon rule (PHASE_4_DOCKING_RESEARCH / IRSIS / SEQUENCE_MAP §1A): inside the 200 m KEEP-OUT
// SPHERE the vehicle must stay within the docking-axis APPROACH CORRIDOR; any unplanned KOS penetration OFF
// that corridor commands an automatic RETREAT (KosRetreat), never a launch escape. This pure predicate is
// that geometry test — the glue (DockingControl) calls it on the V-bar terminal legs and routes a breach to
// the abort responder.
//
// The corridor is a CONE about the +along-track (V-bar) axis toward the port at the LVLH origin: inside the
// KOS the lateral offset from that axis must stay within a cone half-angle, floored at a minimum half-width
// near the port. OUTSIDE the KOS the corridor is not enforced (the R-bar climb + the WP0→WP1 swing legitimately
// arc outside it). ⚠ The exact SpaceX corridor half-angle is NOT public (SEQUENCE_MAP §1A honesty log); the
// glue passes a researched ~10° cone as a [Tunable] to confirm from a flown approach — the nominal guidance
// holds the axis (lateral → 0), so any sane cone never false-triggers the nominal path. PURE + headless-tested.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class DockCorridor
    {
        // Within the V-bar approach corridor? True outside the KOS (not enforced there). Inside the KOS, the
        // lateral offset from the docking axis must be within the cone half-width (floored near the port).
        public static bool OnCorridor(LvlhState rel, double kosRadiusM, double coneHalfAngleRad, double minHalfWidthM)
        {
            if (rel.RangeM >= kosRadiusM) return true;                 // corridor enforced only inside the KOS
            double along = Math.Abs(rel.Ry);                          // distance from the port along the V-bar axis
            double lateral = Math.Sqrt(rel.Rx * rel.Rx + rel.Rz * rel.Rz);   // offset from the axis
            double halfWidth = along * Math.Tan(coneHalfAngleRad);
            if (halfWidth < minHalfWidthM) halfWidth = minHalfWidthM;
            return lateral <= halfWidth;
        }

        // A KOS breach that must abort: inside the KOS AND off the corridor.
        public static bool Breached(LvlhState rel, double kosRadiusM, double coneHalfAngleRad, double minHalfWidthM)
        {
            return rel.RangeM < kosRadiusM && !OnCorridor(rel, kosRadiusM, coneHalfAngleRad, minHalfWidthM);
        }
    }
}
