// DragonScreen — LaunchWindow  (autopilot rebuild L3: launch-to-rendezvous PLANE window / RAAN)
// ============================================================================================
// Correct rendezvous needs BOTH the right inclination (the launch azimuth) AND the right RAAN — and RAAN
// is set by WHEN you launch, not how you steer: you must lift off as the rotating launch site passes
// THROUGH the target's orbital plane. This computes the time until the next such crossing, so the glue
// can HOLD the countdown (and warp) until the window, then launch on the inclination azimuth → coplanar.
//
// The site radius vector r(t) rotates about the body's spin axis at rate ω. It lies in the target plane
// when r(t)·n = 0 (n = the plane normal). Expanding the Rodrigues rotation, that is
//     d(a) = A·cos a + B·sin a + C ,   a = ω·t ,
//     A = r0·n − (â·n)(â·r0),  B = (â×r0)·n,  C = (â·n)(â·r0)
// which is zero at a = φ ± acos(−C/R),  R = √(A²+B²),  φ = atan2(B,A). Two crossings per rev (the
// ascending and descending opportunities); nodeSign selects which by the sign of d′(a) at the crossing.
// If |C/R| > 1 the site never reaches the plane (target inclination < site latitude) → no window.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class LaunchWindow
    {
        /// <summary>
        /// Seconds until the launch site next rotates INTO the target orbital plane, for the crossing whose
        /// derivative sign matches <paramref name="nodeSign"/> (+1 = crossing to the +normal side). Flip the
        /// sign if the achieved RAAN comes out ~180° off (the other node). All vectors world-frame; ω rad/s.
        /// Returns false if the plane is unreachable from this latitude (target inclination &lt; site latitude).
        /// </summary>
        public static bool TimeToCrossing(Vec3 site, Vec3 normal, Vec3 axis, double omega, int nodeSign,
                                          out double tSec)
        {
            tSec = 0.0;
            if (omega <= 1e-12) return false;
            Vec3 r0 = site.Normalized, n = normal.Normalized, ax = axis.Normalized;
            if (r0.SqrMagnitude < 0.5 || n.SqrMagnitude < 0.5 || ax.SqrMagnitude < 0.5) return false;

            double axr = Vec3.Dot(ax, r0), axn = Vec3.Dot(ax, n);
            double A = Vec3.Dot(r0, n) - axn * axr;
            double B = Vec3.Dot(Vec3.Cross(ax, r0), n);
            double C = axn * axr;
            double R = Math.Sqrt(A * A + B * B);
            if (R < 1e-12) return false;
            double rhs = -C / R;
            if (rhs < -1.0 || rhs > 1.0) return false;   // never crosses — inclination below the site latitude

            double phi = Math.Atan2(B, A);
            double ac = Math.Acos(rhs);
            const double TwoPi = 2.0 * Math.PI;
            double[] cands = { Wrap(phi + ac), Wrap(phi - ac) };

            // soonest future crossing (a>0) whose derivative sign matches nodeSign — check this rev + the next.
            double best = double.MaxValue;
            for (int k = 0; k < 2; k++)
                for (int rev = 0; rev < 2; rev++)
                {
                    double a = cands[k] + rev * TwoPi;
                    if (a < 1e-4) a += TwoPi;                 // "now" → take the next occurrence
                    double dp = -A * Math.Sin(a) + B * Math.Cos(a);
                    int s = dp >= 0.0 ? 1 : -1;
                    if (s == nodeSign && a < best) best = a;
                }
            if (best == double.MaxValue) return false;
            tSec = best / omega;
            return true;
        }

        static double Wrap(double a)
        {
            const double TwoPi = 2.0 * Math.PI;
            a %= TwoPi;
            if (a < 0.0) a += TwoPi;
            return a;
        }
    }
}
