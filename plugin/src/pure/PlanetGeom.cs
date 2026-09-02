/*
 * DragonScreen - PlanetGeom
 *
 * PURE. The scaled-space geometry behind the LIVE 3D planet view (docs/MAP_MFD_RESEARCH.md §2): where
 * to put the camera that renders the real globe, and what that camera hides.
 *
 * ---- WHY THIS IS PURE, WHEN THE CAMERA IS NOT ----
 * §2 puts a dedicated Unity camera into scaled space, CopyFrom(ScaledCamera.Instance.cam) so it
 * inherits the exact culling mask and projection the map draws planets with, and renders it to a
 * RenderTexture the page draws (ImageId.ScaledPlanetLive). That camera CANNOT exist with the game
 * closed - it is S10b, and it needs install + glass time. But everything that DECIDES what the camera
 * sees is arithmetic: a placement, a distance, a projection and a ray/sphere test. All of that lives
 * here, is headless-tested, and is the same arithmetic whether a camera ever renders or not.
 *
 * So S10b is left holding a transform to apply and a texture to hand over. The judgements it still
 * owns are the ones a PNG genuinely cannot answer - does the globe render, does the line track, does
 * the framing read at cabin distance - and those are S18's glass checklist, tagged S10.
 *
 * ---- THE ONE NUMBER THAT TIES THE TWO GLOBES TOGETHER ----
 * NavPage already draws a globe: the textured strip disc (NavPage.Globe), at radius
 * 0.44 * min(well) - i.e. DiscFillOfHalfHeight (0.88) of the well's HALF-HEIGHT. The RT camera must
 * put its limb in the SAME place, or switching between them would jump and the orthographic orbit
 * overlay drawn over it would sit off the planet. So the camera distance is not a tuned constant: it
 * is SOLVED from that fill fraction and the camera's own vertical field of view (Distance below).
 * One number, two globes, and a test that round-trips it.
 *
 * ---- HANDEDNESS ----
 * ScaledVec is our own three doubles, so it has no handedness of its own; it takes whatever the
 * caller feeds it. Project() assumes UNITY's camera basis - forward is +Z, up is +Y, and right is
 * Cross(up, forward) - because the values that come in are Unity's and the values that go out are
 * compared against Camera.WorldToViewportPoint (viewport origin BOTTOM-LEFT, y up). ViewportToPanel
 * is the one place that flip is undone, for a page whose y runs DOWN.
 */
using System;

namespace DragonScreen
{
    /// <summary>A scaled-space vector - three doubles and the handful of operations the framing needs.
    /// Pure code cannot reference UnityEngine.Vector3, and scaled space is small enough numbers that
    /// double costs nothing; the glue converts at the boundary.</summary>
    public struct ScaledVec
    {
        public double X, Y, Z;

        public ScaledVec(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static ScaledVec Add(ScaledVec a, ScaledVec b)
        { return new ScaledVec(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }

        public static ScaledVec Sub(ScaledVec a, ScaledVec b)
        { return new ScaledVec(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }

        public static ScaledVec Mul(ScaledVec a, double k)
        { return new ScaledVec(a.X * k, a.Y * k, a.Z * k); }

        public static double Dot(ScaledVec a, ScaledVec b)
        { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }

        public static ScaledVec Cross(ScaledVec a, ScaledVec b)
        {
            return new ScaledVec(a.Y * b.Z - a.Z * b.Y,
                                 a.Z * b.X - a.X * b.Z,
                                 a.X * b.Y - a.Y * b.X);
        }

        public double Length { get { return Math.Sqrt(Dot(this, this)); } }

        /// <summary>Unit vector, or (0,0,0) when there is no direction to give - a zero length, or a
        /// NaN that came in from a degenerate orbit. Returning zero rather than throwing is the same
        /// contract the rest of the display math keeps: a bad input produces a state the caller can
        /// test for, never an exception in the draw path.</summary>
        public static ScaledVec Norm(ScaledVec v)
        {
            double l = v.Length;
            if (l <= 1e-12 || double.IsNaN(l) || double.IsInfinity(l)) return new ScaledVec(0, 0, 0);
            return new ScaledVec(v.X / l, v.Y / l, v.Z / l);
        }

        public static bool IsZero(ScaledVec v)
        {
            double l = v.Length;
            return double.IsNaN(l) || l <= 1e-12;
        }
    }

    /// <summary>Where the scaled-space camera goes this frame. Valid=false when the inputs could not
    /// describe a placement (no body, no radius, no plane) - the page then shows the no-signal state
    /// rather than aiming a camera at nothing.</summary>
    public struct PlanetCamFrame
    {
        /// <summary>Camera position, scaled space.</summary>
        public ScaledVec Eye;
        /// <summary>Unit look direction (toward the body centre) and the camera's up.</summary>
        public ScaledVec Forward, Up;
        /// <summary>Eye-to-body-centre distance, scaled units. Always greater than the body radius.</summary>
        public double Distance;
        /// <summary>The body's apparent radius as a fraction of the render's half-height - the number
        /// Distance was solved from. 0.88 at zoom 0, matching NavPage's textured disc.</summary>
        public double Fill;
        public bool Valid;
    }

    public static class PlanetGeom
    {
        private const double Deg2Rad = Math.PI / 180.0;

        // ---------------------------------------------------------------- framing

        /// <summary>
        /// The textured disc's radius as a fraction of the map well's HALF-HEIGHT, at zoom 0.
        /// NavPage.Planet sizes its disc at half this times min(well) - the two must be the same
        /// number or the RT globe and the fallback disc would put their limbs in different places.
        /// </summary>
        public const double DiscFillOfHalfHeight = 0.88;

        /// <summary>Growth per zoom step, the SAME 1.25 the textured disc grows by, so a zoom press
        /// moves both globes identically and the crew cannot tell which one they are looking at from
        /// how it responds.</summary>
        public const double ZoomBase = 1.25;

        /// <summary>Fill is clamped rather than trusted: MapProjection's PlanetZoom range is -5..+6,
        /// which lands inside this, but a future range change must not be able to solve a distance
        /// that puts the camera inside the planet or a thousand radii out.</summary>
        public const double MinFill = 0.05, MaxFill = 8.0;

        /// <summary>
        /// The default 3/4 view: the camera sits BEHIND the vehicle along-track and ABOVE the orbit
        /// plane, which is §2.1's "orbital chase, framing body + vessel".
        ///
        /// ⚠ CHOSEN, NOT MEASURED - and marked as such for the same reason T11a marked its gearing.
        /// -55 deg of azimuth puts the vehicle well inside the near hemisphere (the limb is at 90) so
        /// it is always visible, off-centre toward the leading edge with the orbit sweeping past it;
        /// 30 deg of pitch opens the orbit from a line into an ellipse. Whether that READS well at
        /// cabin distance is a glass question, and it is on S18's checklist tagged S10. If it is
        /// wrong, these two numbers are the whole of the fix.
        /// </summary>
        public const double DefaultAzimuthDeg = -55.0, DefaultPitchDeg = 30.0;

        /// <summary>Pitch is clamped short of the pole: at exactly 90 the up vector is parallel to
        /// the view direction and the roll of the frame becomes undefined.</summary>
        public const double MaxPitchDeg = 85.0;

        /// <summary>The apparent fill the camera should be placed for at this zoom step.</summary>
        public static double Fill(int zoomStep)
        {
            double f = DiscFillOfHalfHeight * Math.Pow(ZoomBase, zoomStep);
            if (f < MinFill) f = MinFill;
            if (f > MaxFill) f = MaxFill;
            return f;
        }

        /// <summary>
        /// Distance from the body CENTRE that makes a body of this radius fill <paramref name="fill"/>
        /// of the render's half-height, through a camera of this VERTICAL field of view.
        ///
        /// The body's angular radius seen from distance d is asin(R/d); it fills the half-height in
        /// the ratio tan(asin(R/d)) / tan(fov/2). Setting that to `fill` and solving:
        ///     d = R / sin(atan(fill * tan(fov/2)))
        /// The sine of an arctangent is always below 1, so d is always greater than R: this cannot put
        /// the camera inside the planet however hard the crew zooms, which the test asserts.
        /// </summary>
        public static double Distance(double bodyRadius, double fovDeg, double fill)
        {
            if (bodyRadius <= 0.0 || fovDeg <= 0.0 || fovDeg >= 180.0 || fill <= 0.0) return 0.0;
            double s = Math.Sin(Math.Atan(fill * Math.Tan(fovDeg * 0.5 * Deg2Rad)));
            if (s <= 1e-9) return 0.0;
            return bodyRadius / s;
        }

        /// <summary>The inverse of Distance: what fraction of the half-height a body of this radius
        /// actually fills from this distance. Used by the test to round-trip the solve, and by S10b
        /// to sanity-check a frame before it applies it.</summary>
        public static double ApparentFill(double bodyRadius, double distance, double fovDeg)
        {
            if (bodyRadius <= 0.0 || distance <= bodyRadius || fovDeg <= 0.0 || fovDeg >= 180.0)
                return 0.0;
            double half = Math.Tan(fovDeg * 0.5 * Deg2Rad);
            if (half <= 1e-9) return 0.0;
            return Math.Tan(Math.Asin(bodyRadius / distance)) / half;
        }

        /// <summary>
        /// Place the scaled-space camera.
        ///
        /// The basis is the ORBIT's, not the body's: u is the vehicle's radial direction with the
        /// orbit normal taken out of it, w = n x u runs along-track (for a prograde orbit n = r x v,
        /// so n x r-hat is v-hat), and n is the normal itself. Azimuth swings the eye round in that
        /// plane from the vehicle's radial, pitch lifts it out of the plane, and both are the crew's
        /// pan added to the default - so CTR (which zeroes MapView.PlanetRotDeg and PlanetZoom)
        /// returns exactly to the default 3/4 view.
        ///
        /// UP IS THE ORBIT NORMAL, not north: it makes the orbit plane read level in frame, which is
        /// §2.1's "so the orbit reads like a map". It is re-orthogonalised against the view direction
        /// because after a pitch the two are no longer perpendicular.
        ///
        /// <paramref name="fallbackNormal"/> is used when the orbit normal is degenerate - landed, or
        /// no orbit at all - and the body's north axis is the honest stand-in for it. If BOTH are
        /// degenerate the frame comes back Valid=false rather than aiming at an invented direction.
        /// </summary>
        public static bool Frame(ScaledVec bodyCentre, double bodyRadius, ScaledVec vesselPos,
                                 ScaledVec orbitNormal, ScaledVec fallbackNormal,
                                 double rotDeg, double pitchDeg, int zoomStep, double fovDeg,
                                 out PlanetCamFrame f)
        {
            f = new PlanetCamFrame();
            if (bodyRadius <= 0.0 || double.IsNaN(bodyRadius) || double.IsInfinity(bodyRadius))
                return false;

            ScaledVec n = ScaledVec.Norm(orbitNormal);
            if (ScaledVec.IsZero(n)) n = ScaledVec.Norm(fallbackNormal);
            if (ScaledVec.IsZero(n)) return false;

            // The in-plane radial direction: the vehicle's, with the normal component removed. With no
            // vehicle (or one sitting exactly on the axis) any perpendicular will do - the view is then
            // "some 3/4 of this orbit plane", which is still a true statement about the geometry.
            ScaledVec rv = ScaledVec.Sub(vesselPos, bodyCentre);
            ScaledVec u = ScaledVec.Norm(ScaledVec.Sub(rv, ScaledVec.Mul(n, ScaledVec.Dot(rv, n))));
            if (ScaledVec.IsZero(u)) u = AnyPerpendicular(n);
            if (ScaledVec.IsZero(u)) return false;

            ScaledVec w = ScaledVec.Norm(ScaledVec.Cross(n, u));      // along-track
            if (ScaledVec.IsZero(w)) return false;

            double az = (DefaultAzimuthDeg + rotDeg) * Deg2Rad;
            double pitch = Clamp(DefaultPitchDeg + pitchDeg, -MaxPitchDeg, MaxPitchDeg) * Deg2Rad;

            ScaledVec inPlane = ScaledVec.Add(ScaledVec.Mul(u, Math.Cos(az)),
                                              ScaledVec.Mul(w, Math.Sin(az)));
            ScaledVec dir = ScaledVec.Norm(ScaledVec.Add(ScaledVec.Mul(inPlane, Math.Cos(pitch)),
                                                         ScaledVec.Mul(n, Math.Sin(pitch))));
            if (ScaledVec.IsZero(dir)) return false;

            double fill = Fill(zoomStep);
            double dist = Distance(bodyRadius, fovDeg, fill);
            if (dist <= bodyRadius) return false;

            f.Eye = ScaledVec.Add(bodyCentre, ScaledVec.Mul(dir, dist));
            f.Forward = ScaledVec.Mul(dir, -1.0);                     // look at the body centre
            ScaledVec up = ScaledVec.Norm(ScaledVec.Sub(n, ScaledVec.Mul(f.Forward,
                                                        ScaledVec.Dot(n, f.Forward))));
            f.Up = ScaledVec.IsZero(up) ? w : up;                     // straight down the normal: roll on w
            f.Distance = dist;
            f.Fill = fill;
            f.Valid = true;
            return true;
        }

        /// <summary>Some unit vector perpendicular to n. Cross with whichever axis n leans on least,
        /// so the cross is never near-parallel and so never near-zero.</summary>
        private static ScaledVec AnyPerpendicular(ScaledVec n)
        {
            ScaledVec axis = (Math.Abs(n.X) <= Math.Abs(n.Y) && Math.Abs(n.X) <= Math.Abs(n.Z))
                             ? new ScaledVec(1, 0, 0)
                             : (Math.Abs(n.Y) <= Math.Abs(n.Z) ? new ScaledVec(0, 1, 0)
                                                               : new ScaledVec(0, 0, 1));
            return ScaledVec.Norm(ScaledVec.Cross(n, axis));
        }

        // ---------------------------------------------------------------- occlusion

        /// <summary>
        /// Does the solid body hide <paramref name="point"/> from <paramref name="eye"/>?
        ///
        /// The segment eye-to-point against the sphere: substituting the parametrised segment into
        /// |p - centre|^2 = R^2 gives a quadratic in t, and the point is hidden exactly when a root
        /// lands strictly INSIDE the segment (0 &lt; t &lt; 1) - the sphere is crossed on the way. A point
        /// ON the near surface has its root at t = 1 (the segment ends where the sphere begins) and is
        /// correctly visible; a point below the surface has a root before it and is correctly hidden.
        ///
        /// This is the true-geometry twin of GlobeProjection's orthographic test - the same question,
        /// asked of a perspective camera at a finite distance instead of a viewer at infinity. It is
        /// what culls the orbit line behind the RENDERED globe once S10b's camera exists.
        /// </summary>
        public static bool Occluded(ScaledVec eye, ScaledVec point, ScaledVec centre, double radius)
        {
            if (radius <= 0.0) return false;
            ScaledVec d = ScaledVec.Sub(point, eye);
            ScaledVec l = ScaledVec.Sub(eye, centre);

            double a = ScaledVec.Dot(d, d);
            if (a <= 1e-18) return false;                       // point AT the eye: nothing between
            double b = 2.0 * ScaledVec.Dot(l, d);
            double c = ScaledVec.Dot(l, l) - radius * radius;

            double disc = b * b - 4.0 * a * c;
            if (disc <= 0.0) return false;                      // misses the sphere, or grazes it

            double sq = Math.Sqrt(disc);
            double t0 = (-b - sq) / (2.0 * a);
            double t1 = (-b + sq) / (2.0 * a);
            const double Eps = 1e-9;
            return Between(t0, Eps) || Between(t1, Eps);
        }

        private static bool Between(double t, double eps) { return t > eps && t < 1.0 - eps; }

        // ---------------------------------------------------------------- projection

        /// <summary>
        /// Project a scaled-space point through the frame, to Unity VIEWPORT coordinates: 0..1 across
        /// and up the render, origin BOTTOM-LEFT - the convention Camera.WorldToViewportPoint uses.
        ///
        /// §2 has the GLUE project the overlay with the camera's own WorldToViewportPoint, so the line
        /// cannot drift off the render it is drawn over; this is the tested twin of that call, so the
        /// two can be checked against each other on the one occasion it matters (S10b's first render)
        /// instead of being taken on trust.
        ///
        /// <paramref name="aspect"/> is width/height. inFront is false BEHIND the camera, where the
        /// viewport values are meaningless and the caller must drop the point rather than draw it
        /// mirrored through the origin.
        /// </summary>
        public static void Project(PlanetCamFrame f, double fovDeg, double aspect, ScaledVec point,
                                   out double vx, out double vy, out bool inFront)
        {
            vx = 0.0; vy = 0.0; inFront = false;
            if (!f.Valid || aspect <= 0.0 || fovDeg <= 0.0 || fovDeg >= 180.0) return;

            ScaledVec right = ScaledVec.Cross(f.Up, f.Forward);      // Unity's left-handed camera basis
            ScaledVec rel = ScaledVec.Sub(point, f.Eye);

            double z = ScaledVec.Dot(rel, f.Forward);
            if (z <= 1e-12) return;                                   // at or behind the lens
            inFront = true;

            double halfH = z * Math.Tan(fovDeg * 0.5 * Deg2Rad);
            double halfW = halfH * aspect;
            if (halfH <= 1e-12 || halfW <= 1e-12) { inFront = false; return; }

            vx = 0.5 + ScaledVec.Dot(rel, right) / (2.0 * halfW);
            vy = 0.5 + ScaledVec.Dot(rel, f.Up) / (2.0 * halfH);
        }

        /// <summary>Viewport (y UP from the bottom) to panel pixels (y DOWN from the top) inside the
        /// rect the render is drawn into. The one place the flip happens.</summary>
        public static void ViewportToPanel(double vx, double vy,
                                           float rx, float ry, float rw, float rh,
                                           out float px, out float py)
        {
            px = rx + (float)(vx * rw);
            py = ry + (float)((1.0 - vy) * rh);
        }

        // ---------------------------------------------------------------- the no-signal state

        /// <summary>
        /// What the 3D PLANET view prints when there is no live camera behind it - which is EVERY
        /// state today, and every state in the PNG preview for ever (there is no Unity camera with the
        /// game closed).
        ///
        /// The view does not go blank: it keeps drawing the textured strip disc and the projected
        /// orbit, which are real and correct, and says plainly that they are not the live render. That
        /// is §14.4(e) - a coherent MARKED stand-in, never a dash over a quantity that exists - and it
        /// is the same marking T11a used for the placeholder capsule sequence. The label names the
        /// task that fills it in, so the wording stays true as the build moves.
        /// </summary>
        public const string NoSignalLabel = "LIVE 3D — NO SIGNAL";

        /// <summary>The second line: what IS on the glass, and who replaces it.</summary>
        public const string NoSignalDetail = "S10b RENDERS THE SCALED-SPACE CAMERA — GLOBE + ORBIT ARE REAL";

        private static double Clamp(double v, double lo, double hi)
        { return (v < lo) ? lo : (v > hi) ? hi : v; }
    }
}
