// DragonScreen — LaunchAzimuth  (autopilot rebuild L3 ascent: the plane problem)
// ============================================================================================
// The heading to fly to place the vehicle in the target orbital plane, from the research
// (LAUNCH_AND_ASCENT_RESEARCH §6.3). Inertial azimuth from inclination i and launch latitude φ:
//     sin(β_inertial) = cos(i) / cos(φ)         (reachable only when i ≥ |φ|)
// then corrected for the launch site's own eastward speed from the body's rotation:
//     V_east = V_orbit·sin β − V_rot ,  V_north = V_orbit·cos β ,  β_ground = atan2(V_east, V_north)
// Azimuth is measured CLOCKWISE FROM NORTH. The ascending-node pass gives the north-easterly heading;
// the descending pass (a southward launch, e.g. a polar Fram2) is π − β.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class LaunchAzimuth
    {
        // Inertial azimuth (rad, clockwise from north) for the ascending pass. Returns false if i < |φ|.
        public static bool InertialRad(double incRad, double latRad, out double azRad)
        {
            azRad = 0.0;
            double cphi = Math.Cos(latRad);
            if (Math.Abs(cphi) < 1e-9) return false;
            double s = Math.Cos(incRad) / cphi;
            if (s > 1.0 || s < -1.0) return false;      // inclination below the launch latitude: unreachable
            azRad = Math.Asin(s);                       // in [−π/2, π/2] from north (north-easterly)
            return true;
        }

        // Ground azimuth (rad, clockwise from north), correcting for the site's eastward rotation speed.
        // orbitalSpeedMps ≈ the target insertion speed; bodyRotationRadPerS·R·cosφ is the site's east speed.
        public static bool GroundRad(double incRad, double latRad, double orbitalSpeedMps,
                                     double bodyRadiusM, double bodyRotationRadPerS,
                                     bool descending, out double azRad)
        {
            azRad = 0.0;
            double azI;
            if (!InertialRad(incRad, latRad, out azI)) return false;
            if (descending) azI = Math.PI - azI;        // southward pass (retrograde/polar-south)

            double vRot = bodyRotationRadPerS * bodyRadiusM * Math.Cos(latRad);   // eastward site speed
            double vEast = orbitalSpeedMps * Math.Sin(azI) - vRot;
            double vNorth = orbitalSpeedMps * Math.Cos(azI);
            azRad = Math.Atan2(vEast, vNorth);
            if (azRad < 0.0) azRad += 2.0 * Math.PI;
            return true;
        }

        public static double Deg(double rad) { return rad * 180.0 / Math.PI; }
        public static double Rad(double deg) { return deg * Math.PI / 180.0; }
    }
}
