/*
 * DragonScreen - Lvlh
 *
 * PURE. The station's local-vertical/local-horizontal frame, and the projection of one vehicle's
 * state into it. This is the geometry the L-approach (pure/WaypointApproach.cs) is written in:
 * RADIAL (+ up, away from the body), ALONG-track (+ ahead, the velocity direction the forward port
 * faces), CROSS-track (out of plane).
 *
 * ---- WHY THIS IS ITS OWN TESTED FILE ----
 * `StationApproach.BuildState` builds exactly this basis inline and the CW ladder reads Rx/Ry/Rz off
 * it, but nothing tests it - and the file's own header warns that "a frame error here looks identical
 * to a tuning problem right up until the capsule burns the wrong way." The L-approach turns those
 * three offsets into RCS translation through DockControl, so the sign of `along` decides which way the
 * capsule slides toward the port. That is worth a test that pins it, so this is the frame in ONE
 * place, in plain doubles (no Unity - the headless build compiles src/pure only), with the glue
 * converting its Vector3d state to doubles at the boundary exactly as DockingOps does for DockControl.
 *
 * ---- THE BASIS (identical to StationApproach.BuildState) ----
 *     xh = radial  = the station's position from the body centre, normalised (up)
 *     yh = along   = the station's velocity with the radial part removed, normalised (in-track)
 *     zh = cross   = xh x yh
 * Near-circular orbits have velocity almost perpendicular to radius already, so removing the radial
 * part of the velocity is a small correction - but it is the correction that makes yh a true in-track
 * axis rather than "roughly forward", which matters at the metre scale the docking works at.
 */
namespace DragonScreen
{
    /// <summary>A vehicle's state in a station's LVLH frame, metres and m/s.</summary>
    public struct LvlhState
    {
        public bool Valid;
        /// <summary>+ ABOVE the station (higher orbit), - below. Metres.</summary>
        public double RadialM;
        /// <summary>+ AHEAD (velocity direction, where the forward port faces), - behind. Metres.</summary>
        public double AlongM;
        /// <summary>Out of plane, metres.</summary>
        public double CrossM;
        /// <summary>Straight-line separation of the two centres, metres.</summary>
        public double RangeM;
        /// <summary>The relative velocity resolved onto the same three axes, m/s.</summary>
        public double RadialRateMps, AlongRateMps, CrossRateMps;
    }

    public static class Lvlh
    {
        // ---- tiny vector helpers in plain doubles (no Unity in src/pure) ----
        private static double Dot(double ax, double ay, double az, double bx, double by, double bz)
        {
            return ax * bx + ay * by + az * bz;
        }

        private static void Cross(double ax, double ay, double az, double bx, double by, double bz,
                                  out double cx, out double cy, out double cz)
        {
            cx = ay * bz - az * by;
            cy = az * bx - ax * bz;
            cz = ax * by - ay * bx;
        }

        private static void Normalise(ref double x, ref double y, ref double z)
        {
            double m = System.Math.Sqrt(x * x + y * y + z * z);
            if (m < 1e-12) { x = 0.0; y = 0.0; z = 0.0; return; }
            x /= m; y /= m; z /= m;
        }

        /// <summary>
        /// Build the station's LVLH basis from its position (from the body centre) and velocity.
        /// The three output axes are unit and mutually orthogonal for any non-degenerate orbit.
        /// </summary>
        private static void Basis(
            double stnRx, double stnRy, double stnRz, double stnVx, double stnVy, double stnVz,
            out double xhx, out double xhy, out double xhz,
            out double yhx, out double yhy, out double yhz,
            out double zhx, out double zhy, out double zhz)
        {
            xhx = stnRx; xhy = stnRy; xhz = stnRz;
            Normalise(ref xhx, ref xhy, ref xhz);

            // yh = velocity with the radial component removed (Vector3d.Exclude(xh, v)).
            double proj = Dot(stnVx, stnVy, stnVz, xhx, xhy, xhz);
            yhx = stnVx - proj * xhx; yhy = stnVy - proj * xhy; yhz = stnVz - proj * xhz;
            Normalise(ref yhx, ref yhy, ref yhz);

            Cross(xhx, xhy, xhz, yhx, yhy, yhz, out zhx, out zhy, out zhz);
            Normalise(ref zhx, ref zhy, ref zhz);
        }

        /// <summary>
        /// Project a vehicle's RELATIVE state into the station's LVLH frame.
        ///
        /// `relR*` is (ship - station) position and `relV*` is (ship - station) velocity, both in the
        /// same world/body frame the station's own `stnR*`/`stnV*` are given in. The caller supplies
        /// relative vectors so this never has to know where the body centre is.
        /// </summary>
        public static LvlhState Project(
            double stnRx, double stnRy, double stnRz, double stnVx, double stnVy, double stnVz,
            double relRx, double relRy, double relRz, double relVx, double relVy, double relVz)
        {
            LvlhState s = new LvlhState();

            double xhx, xhy, xhz, yhx, yhy, yhz, zhx, zhy, zhz;
            Basis(stnRx, stnRy, stnRz, stnVx, stnVy, stnVz,
                  out xhx, out xhy, out xhz, out yhx, out yhy, out yhz, out zhx, out zhy, out zhz);
            if (xhx == 0.0 && xhy == 0.0 && xhz == 0.0) return s;   // degenerate station state

            s.RadialM = Dot(relRx, relRy, relRz, xhx, xhy, xhz);
            s.AlongM = Dot(relRx, relRy, relRz, yhx, yhy, yhz);
            s.CrossM = Dot(relRx, relRy, relRz, zhx, zhy, zhz);
            s.RadialRateMps = Dot(relVx, relVy, relVz, xhx, xhy, xhz);
            s.AlongRateMps = Dot(relVx, relVy, relVz, yhx, yhy, yhz);
            s.CrossRateMps = Dot(relVx, relVy, relVz, zhx, zhy, zhz);
            s.RangeM = System.Math.Sqrt(relRx * relRx + relRy * relRy + relRz * relRz);
            s.Valid = true;
            return s;
        }

        /// <summary>
        /// The world-frame vector of an LVLH offset relative to the station (radial, along, cross).
        /// Add it to the station's centre of mass to get the world point a waypoint sits at. Uses the
        /// SAME basis as Project, so a point projected out and back round-trips.
        /// </summary>
        public static void OffsetToWorld(
            double stnRx, double stnRy, double stnRz, double stnVx, double stnVy, double stnVz,
            double radial, double along, double cross,
            out double ox, out double oy, out double oz)
        {
            double xhx, xhy, xhz, yhx, yhy, yhz, zhx, zhy, zhz;
            Basis(stnRx, stnRy, stnRz, stnVx, stnVy, stnVz,
                  out xhx, out xhy, out xhz, out yhx, out yhy, out yhz, out zhx, out zhy, out zhz);
            ox = radial * xhx + along * yhx + cross * zhx;
            oy = radial * xhy + along * yhy + cross * zhy;
            oz = radial * xhz + along * yhz + cross * zhz;
        }
    }
}
