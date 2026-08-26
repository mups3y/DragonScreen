// DragonScreen - RelativeMotion
// ---- ⛔ WHY THIS FILE EXISTS: TWO FILES DEFINED IT OPPOSITE WAYS AND ONE OF THEM WAS WRONG ----
// ---- THE CONVENTION, ONCE ----
using UnityEngine;

namespace DragonScreen
{
    public struct RelState
    {
        public bool Valid;
        public double RangeM;
        public Vector3d Los;
        public Vector3d Relative;
        public double ClosingMps;
        public double LateralMps;
    }

    public static class RelativeMotion
    {
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

        public static Vector3d Correction(RelState s, double wantMps)
        {
            if (!s.Valid) return Vector3d.zero;
            return (s.Los * wantMps) - s.Relative;
        }
    }
}
