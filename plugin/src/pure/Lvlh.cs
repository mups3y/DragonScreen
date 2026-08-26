// DragonScreen — Lvlh  (autopilot rebuild L3 rendezvous: the target's local frame)
// ============================================================================================
// Relative motion near the station is expressed in its Local-Vertical/Local-Horizontal frame
// (PHASE_3_RENDEZVOUS_RESEARCH §4.1): x = RADIAL (out, +R-bar up), y = ALONG-TRACK (+V-bar, prograde),
// z = CROSS-TRACK (orbit normal). The frame co-rotates with the orbit at mean motion n, so a body at
// rest in it (co-orbiting) has zero LVLH velocity even though its inertial relative velocity is ω×r.
// Built fresh. Feeds the CW two-impulse targeting and the waypoint geometry.
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct LvlhState
    {
        public double Rx, Ry, Rz;   // radial, along-track, cross-track (m)
        public double Vx, Vy, Vz;   // rotating-frame relative velocity (m/s)
        public double RangeM { get { return Math.Sqrt(Rx * Rx + Ry * Ry + Rz * Rz); } }
    }

    public static class Lvlh
    {
        // Build the LVLH basis from the target's world state and project a chaser's world relative
        // position + velocity into it (velocity in the ROTATING frame: v_rot = v_rel − ω×r, ω = n·ẑ).
        public static LvlhState Project(Vec3 targetR, Vec3 targetV, Vec3 relPos, Vec3 relVel, double n)
        {
            Vec3 xh = targetR.Normalized;                       // radial out
            Vec3 zh = Vec3.Cross(targetR, targetV).Normalized;  // cross-track (orbit normal)
            Vec3 yh = Vec3.Cross(zh, xh);                       // along-track (prograde)

            LvlhState s;
            s.Rx = Vec3.Dot(relPos, xh); s.Ry = Vec3.Dot(relPos, yh); s.Rz = Vec3.Dot(relPos, zh);
            double vpx = Vec3.Dot(relVel, xh), vpy = Vec3.Dot(relVel, yh), vpz = Vec3.Dot(relVel, zh);
            // ω×r with ω=(0,0,n): (−n·Ry, n·Rx, 0); rotating velocity = projected − ω×r.
            s.Vx = vpx - (-n * s.Ry);
            s.Vy = vpy - (n * s.Rx);
            s.Vz = vpz;
            return s;
        }

        // An LVLH offset (radial, along, cross) taken to a world position offset from the target.
        public static Vec3 OffsetToWorld(Vec3 targetR, Vec3 targetV, double radial, double along, double cross)
        {
            Vec3 xh = targetR.Normalized;
            Vec3 zh = Vec3.Cross(targetR, targetV).Normalized;
            Vec3 yh = Vec3.Cross(zh, xh);
            return xh * radial + yh * along + zh * cross;
        }

        public static double MeanMotion(double mu, double semiMajorAxisM)
        {
            if (mu <= 0.0 || semiMajorAxisM <= 0.0) return 0.0;
            return Math.Sqrt(mu / (semiMajorAxisM * semiMajorAxisM * semiMajorAxisM));
        }
    }
}
