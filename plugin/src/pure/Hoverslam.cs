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
//
// ⚠ PROVENANCE + STATUS (W3, Wave C, 2026-09-04). RESTORED VERBATIM from `8b81816^`; every W3 edit here
// is COMMENT-ONLY (comment-stripped diff against `8b81816^`: identical).
//
// ⛔ A GEN-1 `Hoverslam.cs` EXISTS AND WAS DELIBERATELY NOT TAKEN. `0d6423d` / `158eb2a^` carry a
// 7,819-byte gen-1 file of the same name whose `HoverslamInputs` is a DIFFERENT STRUCT — `VerticalSpeed`,
// `MassT`, `ThrustKn`, `MdotTps`, `DragRefAccel`, `DragRefSpeed` instead of this file's `DescentSpeedMps`,
// `ThrustAccelMps2`, `TerminalSpeedMps`. It is therefore NOT a comment-stripped copy of this one, so W1's
// mechanical rule applies unchanged: the gen-1 file is taken only when the CODE is byte-identical, and
// here it is not. Nothing from gen 1 is imported (R1 §0.2 — two generations share class names; taking
// both produces duplicate types and the build fails).
//
// ⛔ THE IGNITION ANCHORS ARE UN-CONVERGED AND THEIR REGIME IS RECORDED NOWHERE — R1 §7.4 names this
// exact file, with `test/BoosterTest.cs`, as a "constants with NO STATED REGIME" defect. Read that
// precisely: THIS FILE HAS NO PHYSICAL CONSTANT. Its only literal is the `Dt = 0.05` integration step,
// which is arithmetic, not tuning. Every anchor — descent speed, thrust acceleration, terminal speed,
// dead time, spool — is an INPUT supplied by the caller, so the defect lives in the FIXTURE and in the
// future caller, not in the solver. The only anchor set ever written down is gen-1 `HoverslamTest.cs`'s
// *"the real 0824 landing (v_term 244, 31 t, 1925 kN, spool 3.5 s)"*, and **whether that landing was ours
// (RSS-RO) or F9I's (stock) is recorded nowhere in this repo** (R1 §7.4). ⚠ Worse, and W3 checked this on
// the bytes rather than repeating R1: that gen-1 header disagrees with its OWN fixture, which sets
// `SpoolS = 1.2` and `DeadTimeS = 5.4`, and both disagree with this wave's `test/BoosterTest.cs`
// (2,227 kN, `DeadTimeS = 6.0`, `SpoolS = 0.0`). Three different anchor sets for one named landing.
// ⇒ NO number reached through this solver is evidence of anything until a RECORDED RSS-RO flight
// re-establishes it (§B16.8 ruling 3 — BlackBox + an owner glass gate; not reachable under preview-only).
//
// ⚠ The `DeadTimeS` input is the ULLAGE-SETTLE dead fall — the same discipline as §B16.3 ("settle
// propellant with RCS before EVERY relight") and R1 §7.1's UNFIXED ullage/ignition defect (register H1b,
// the failure that lost the booster). This solver ASSUMES a dead time it is given; it does not enforce
// one, and it cannot tell a settled stage from an unsettled one.
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

        // Fewest engines that can still arrest from here: try the centre engine first, then three, each
        // asked the SAME question — can it ignite low enough to arrest in the altitude that is left?
        // Returns 1 or 3 (0 if even three cannot stop → the caller flags an un-recoverable landing to FDIR).
        public static int EnginesFor(HoverslamInputs sOne, HoverslamInputs sThree)
        {
            // if one engine can ignite low enough that its ignition altitude is under the current altitude
            // with margin, one engine suffices; otherwise three are needed to arrest the speed.
            if (sOne.ThrustAccelMps2 > sOne.GravityMps2 && IgnitionAltitude(sOne) < sOne.AltitudeM)
                return 1;
            // OCT8 (2026-09-05) — SYMMETRIC with the branch above. The old test asked only whether three
            // engines out-thrust gravity, never whether three could arrest in the altitude actually left,
            // so a stage too low and too fast for even the three-engine bank still got "3" and a burn it
            // could not fly — not the un-recoverable verdict this function's own comment always promised.
            // ⚠ `IgnitionAltitude` returns `AltitudeM` (i.e. "light now") when `a <= g`, which would make a
            // naive `IgnitionAltitude(sThree) < sThree.AltitudeM` false at that boundary for the WRONG
            // reason — but the TWR conjunct immediately before it already excludes `a <= g`, so by the time
            // `IgnitionAltitude(sThree)` runs here it is always a genuinely solved value, never the
            // fallback, and that trap cannot fire.
            if (sThree.ThrustAccelMps2 > sThree.GravityMps2 && IgnitionAltitude(sThree) < sThree.AltitudeM)
                return 3;
            return 0;
        }
    }
}
