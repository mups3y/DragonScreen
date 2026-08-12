/*
 * DragonScreen - RelativeMotion
 *
 * GLUE. One definition of "how fast are we closing on that", used by everything that closes on
 * anything.
 *
 * ---- ⛔ WHY THIS FILE EXISTS: TWO FILES DEFINED IT OPPOSITE WAYS AND ONE OF THEM WAS WRONG ----
 * `StationApproach:147` had `relVel = station - ship` and `DirectApproachOps:115` had
 * `relVel = ship - station`, and BOTH then negated it before the dot product. So the same phrase -
 * `Dot(-relVel, los)` - meant closing-positive in one file and closing-NEGATIVE in the other.
 *
 * On the 2026-08-11 13:34 flight the direct approach therefore read its own closing rate with the
 * sign flipped, and two things followed from that one line:
 *
 *   · the hard speed cap could never bind. `Burn()` asks `closing < SpeedCap(range)`, and a negative
 *     number is always below 25, so it burned continuously. Closing went 5.9 -> 44.3 m/s against a
 *     cap of 25, apoapsis went 85.9 -> 166.7 km, and 45 of the capsule's 72 units of monopropellant
 *     went with it - the return budget, spent on the approach.
 *   · the correction vector added the velocity it should have removed. We want our velocity relative
 *     to the station to BE `want * los`, so the correction is `want*los - relVel`; written with the
 *     other sign it is `want*los + relVel`, which accelerates by exactly what we already had. The
 *     approach never left its Accelerating phase because the transition tests the same bad number.
 *
 * ---- THE CONVENTION, ONCE ----
 * `Relative` is OUR velocity in the target's frame: ours minus theirs.
 * `Closing` is POSITIVE WHEN THE GAP IS SHRINKING.
 *
 * That is F9I's convention and it is measured, not chosen. `StDirectApproach:1474`: *"POSITIVE =
 * CLOSING. Proven from flight 035: x2 was written as `0 - vdot(relVel, toTarget)` and read -10.0179
 * while genuinely closing, so `vdot(relVel, toTarget)` is positive while closing."*
 *
 * CLAUDE.md check 2: a rule a second place also needs goes in ONE function both callers use. This is
 * that function. Do not re-derive a closing rate anywhere else.
 */
using UnityEngine;

namespace DragonScreen
{
    /// <summary>Where a target is and how we are moving with respect to it.</summary>
    public struct RelState
    {
        public bool Valid;
        /// <summary>Metres between the two centres of mass.</summary>
        public double RangeM;
        /// <summary>Unit vector from US to THEM.</summary>
        public Vector3d Los;
        /// <summary>OUR velocity in the target's frame: ours minus theirs.</summary>
        public Vector3d Relative;
        /// <summary>Along the line of sight. POSITIVE WHEN THE GAP IS SHRINKING.</summary>
        public double ClosingMps;
        /// <summary>Relative speed perpendicular to the line of sight, m/s.</summary>
        public double LateralMps;
    }

    public static class RelativeMotion
    {
        /// <summary>
        /// Measure one vehicle against another. See the header for the sign convention, which is the
        /// only reason this is not written inline at each call site.
        /// </summary>
        public static RelState Of(Vessel ship, Vessel target)
        {
            RelState s = new RelState();
            if (ship == null || target == null) return s;
            if (ship.state == Vessel.State.DEAD || target.state == Vessel.State.DEAD) return s;

            Vector3d to = target.CoM - ship.CoM;
            s.RangeM = to.magnitude;
            if (s.RangeM < 1e-6) return s;

            s.Los = to / s.RangeM;
            s.Relative = ship.obt_velocity - target.obt_velocity;
            s.ClosingMps = Vector3d.Dot(s.Relative, s.Los);
            s.LateralMps = Vector3d.Exclude(s.Los, s.Relative).magnitude;
            s.Valid = true;
            return s;
        }

        /// <summary>
        /// The correction that makes our relative velocity `wantMps` straight down the line of sight.
        ///
        /// ⚠ ONE VECTOR - direction and speed together. `pure/DirectApproach.cs` trap 1 records what
        /// splitting them cost: flight 037 made three attempts and one of them closed at 10.84 m/s
        /// IN THE WRONG DIRECTION, because whenever drift exceeded tolerance the entire commanded
        /// burn was the lateral kill and no closing speed was ever built.
        /// </summary>
        public static Vector3d Correction(RelState s, double wantMps)
        {
            if (!s.Valid) return Vector3d.zero;
            return (s.Los * wantMps) - s.Relative;
        }
    }
}
