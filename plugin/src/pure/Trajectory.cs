/*
 * DragonScreen - Trajectory
 *
 * PURE. Forward integration of a ballistic trajectory THROUGH AN ATMOSPHERE, to an impact point.
 *
 * ---- WHY WE HAVE OUR OWN INSTEAD OF TAKING TRAJECTORIES ----
 * F9I gets its impact point from the Trajectories add-on. We do not take that dependency, and the
 * vacuum solve we had instead is not good enough for everything: it is fine for a boostback that
 * deliberately overshoots by 2.7 km, and useless for a de-orbit burn whose stop condition is a 50 m
 * tolerance. Drag only ever shortens a trajectory, so a vacuum answer is always LONG, and by tens of
 * kilometres on an entry.
 *
 * ---- ⛔ THE DRAG TERM IS MEASURED, NOT MODELLED ----
 * This is the part that makes it worth having. KSP's drag comes from per-part drag cubes, occlusion
 * and orientation; computing it analytically means reimplementing the game's aerodynamics and being
 * wrong in a new way. Instead the glue MEASURES the vessel's actual drag acceleration in flight -
 * total acceleration minus gravity minus thrust - and back-solves the ballistic coefficient:
 *
 *      a_drag = 0.5 * rho * v² / BC        ->        BC = 0.5 * rho * v² / a_drag
 *
 * BC = m/(Cd·A) carries mass, shape, orientation and occlusion in one number the vehicle tells us
 * about itself. It is re-measured continuously, so a capsule that jettisons a trunk, deploys fins or
 * turns broadside updates its own prediction without anyone writing down a coefficient.
 *
 * That is the same principle as everything else that works on this project: `falcon-detect-by-
 * capability`, the measured keep-out radius, the measured ascent time. Measure the vehicle, do not
 * describe it.
 *
 * ---- INTEGRATION ----
 * RK4 in an inertial frame centred on the body. Gravity is Newtonian; drag acts along the
 * SURFACE-relative velocity, because that is what the air sees. The body's rotation is carried
 * separately so the impact point comes out as a ground position rather than an inertial one - a
 * 600 km body turning once per six hours moves 175 m/s at the equator, which is kilometres of miss
 * over a long entry.
 *
 * The atmosphere is supplied as a callback so this file stays free of KSP. The glue passes
 * `body.GetDensity(body.GetPressure(alt), body.GetTemperature(alt))`, which is the game's own model
 * rather than an approximation of it.
 */
using System;

namespace DragonScreen
{
    /// <summary>Density at an altitude above sea level, kg/m³. The glue supplies KSP's own model.</summary>
    public delegate double DensityAt(double altitudeM);

    public struct TrajectoryInputs
    {
        /// <summary>Position relative to the body centre, metres, inertial.</summary>
        public double Px, Py, Pz;
        /// <summary>Inertial velocity, m/s.</summary>
        public double Vx, Vy, Vz;
        /// <summary>Body gravitational parameter.</summary>
        public double Mu;
        /// <summary>Body radius, metres.</summary>
        public double BodyRadiusM;
        /// <summary>Body rotation rate, rad/s. Sign follows the +Z axis of the frame the glue uses.</summary>
        public double BodyOmega;
        /// <summary>Top of the atmosphere, metres. Above this the drag term is skipped entirely.</summary>
        public double AtmosphereDepthM;
        /// <summary>
        /// Ballistic coefficient m/(Cd·A), kg/m². MEASURED - see the header. Zero or negative means
        /// "unknown", and the integration runs as a vacuum solve and says so.
        /// </summary>
        public double BallisticCoefficient;
        /// <summary>Altitude the impact is declared at - terrain height, or zero for sea level.</summary>
        public double ImpactAltitudeM;
    }

    public struct TrajectoryResult
    {
        public bool Ok;
        /// <summary>Impact position relative to the body centre, in the INERTIAL frame.</summary>
        public double Ix, Iy, Iz;
        /// <summary>Seconds from now to impact.</summary>
        public double TimeToImpactS;
        /// <summary>Speed at impact, m/s. Worth knowing before you arrive.</summary>
        public double ImpactSpeedMps;
        /// <summary>
        /// Radians the body turned during the flight. The glue subtracts this from the impact
        /// longitude to get a GROUND position.
        /// </summary>
        public double BodyRotationRad;
        /// <summary>True when drag was actually modelled rather than skipped.</summary>
        public bool DragModelled;
        public string Note;
    }

    public static class Trajectory
    {
        /// <summary>Coarse step used in vacuum, seconds. Nothing interesting happens up there.</summary>
        public const double VacuumStepS = 2.0;
        /// <summary>Fine step used inside the atmosphere, seconds.</summary>
        public const double AtmoStepS = 0.25;
        /// <summary>
        /// Finer still in the dense lower atmosphere, seconds. Below a quarter of the atmosphere the
        /// density is changing fast enough that a 0.25 s step is visibly lossy over a long entry.
        /// </summary>
        public const double DenseStepS = 0.05;
        /// <summary>Give up after this much simulated time, seconds.</summary>
        public const double MaxFlightS = 3600.0;

        /// <summary>
        /// Integrate to impact.
        ///
        /// Returns Ok = false rather than a wrong answer when the trajectory does not come down
        /// inside <see cref="MaxFlightS"/> - an orbit that does not intersect the ground has no
        /// impact point, and reporting one would be worse than reporting none.
        /// </summary>
        public static TrajectoryResult Solve(TrajectoryInputs s, DensityAt density)
        {
            TrajectoryResult r = new TrajectoryResult();
            r.Ok = false;

            double px = s.Px, py = s.Py, pz = s.Pz;
            double vx = s.Vx, vy = s.Vy, vz = s.Vz;
            double t = 0.0;
            double rot = 0.0;
            bool draggedEver = false;

            double impactR = s.BodyRadiusM + s.ImpactAltitudeM;
            if (s.Mu <= 0.0 || s.BodyRadiusM <= 0.0) { r.Note = "no body"; return r; }

            // Already at or below the impact radius: the answer is "here", not a simulation.
            double r0 = Mag(px, py, pz);
            if (r0 <= impactR)
            {
                r.Ok = true; r.Ix = px; r.Iy = py; r.Iz = pz;
                r.ImpactSpeedMps = Mag(vx, vy, vz);
                r.Note = "already at the surface";
                return r;
            }

            while (t < MaxFlightS)
            {
                double alt = Mag(px, py, pz) - s.BodyRadiusM;
                bool inAir = alt < s.AtmosphereDepthM && s.BallisticCoefficient > 0.0;
                if (inAir) draggedEver = true;

                double dt = VacuumStepS;
                if (inAir) dt = (alt < s.AtmosphereDepthM * 0.25) ? DenseStepS : AtmoStepS;

                // ---- RK4 ----
                double ax1, ay1, az1, ax2, ay2, az2, ax3, ay3, az3, ax4, ay4, az4;
                Accel(px, py, pz, vx, vy, vz, s, density, out ax1, out ay1, out az1);
                Accel(px + vx * dt / 2, py + vy * dt / 2, pz + vz * dt / 2,
                      vx + ax1 * dt / 2, vy + ay1 * dt / 2, vz + az1 * dt / 2,
                      s, density, out ax2, out ay2, out az2);
                Accel(px + (vx + ax1 * dt / 2) * dt / 2, py + (vy + ay1 * dt / 2) * dt / 2,
                      pz + (vz + az1 * dt / 2) * dt / 2,
                      vx + ax2 * dt / 2, vy + ay2 * dt / 2, vz + az2 * dt / 2,
                      s, density, out ax3, out ay3, out az3);
                Accel(px + (vx + ax2 * dt / 2) * dt, py + (vy + ay2 * dt / 2) * dt,
                      pz + (vz + az2 * dt / 2) * dt,
                      vx + ax3 * dt, vy + ay3 * dt, vz + az3 * dt,
                      s, density, out ax4, out ay4, out az4);

                double nx = px + dt / 6.0 * (vx + 2 * (vx + ax1 * dt / 2) + 2 * (vx + ax2 * dt / 2)
                                             + (vx + ax3 * dt));
                double ny = py + dt / 6.0 * (vy + 2 * (vy + ay1 * dt / 2) + 2 * (vy + ay2 * dt / 2)
                                             + (vy + ay3 * dt));
                double nz = pz + dt / 6.0 * (vz + 2 * (vz + az1 * dt / 2) + 2 * (vz + az2 * dt / 2)
                                             + (vz + az3 * dt));
                vx += dt / 6.0 * (ax1 + 2 * ax2 + 2 * ax3 + ax4);
                vy += dt / 6.0 * (ay1 + 2 * ay2 + 2 * ay3 + ay4);
                vz += dt / 6.0 * (az1 + 2 * az2 + 2 * az3 + az4);

                double newR = Mag(nx, ny, nz);
                if (newR <= impactR)
                {
                    // ---- LINEAR INTERPOLATION ONTO THE SURFACE ----
                    // Stopping at the first step that is underground would place the impact up to a
                    // whole step past the ground - 0.05 s at 300 m/s is 15 m, which is a third of the
                    // de-orbit burn's entire 50 m tolerance. Interpolate the crossing instead.
                    double prevR = Mag(px, py, pz);
                    double f = (prevR - impactR) / (prevR - newR);
                    if (f < 0.0) f = 0.0; else if (f > 1.0) f = 1.0;
                    r.Ix = px + (nx - px) * f;
                    r.Iy = py + (ny - py) * f;
                    r.Iz = pz + (nz - pz) * f;
                    t += dt * f;
                    rot += s.BodyOmega * dt * f;
                    r.Ok = true;
                    r.TimeToImpactS = t;
                    r.ImpactSpeedMps = Mag(vx, vy, vz);
                    r.BodyRotationRad = rot;
                    r.DragModelled = draggedEver;
                    r.Note = draggedEver ? "integrated with measured drag" : "vacuum solve";
                    return r;
                }

                px = nx; py = ny; pz = nz;
                t += dt;
                rot += s.BodyOmega * dt;
            }

            r.Note = "no impact within " + MaxFlightS.ToString("F0") + " s - this does not come down";
            return r;
        }

        /// <summary>
        /// Gravity plus drag. Drag acts along the SURFACE-relative velocity, which is what the air
        /// sees - using the inertial velocity instead is a real error at 175 m/s of equatorial
        /// rotation.
        /// </summary>
        private static void Accel(double px, double py, double pz,
                                  double vx, double vy, double vz,
                                  TrajectoryInputs s, DensityAt density,
                                  out double ax, out double ay, out double az)
        {
            double r = Mag(px, py, pz);
            if (r < 1.0) { ax = 0; ay = 0; az = 0; return; }

            double g = -s.Mu / (r * r * r);
            ax = g * px; ay = g * py; az = g * pz;

            if (s.BallisticCoefficient <= 0.0) return;
            double alt = r - s.BodyRadiusM;
            if (alt >= s.AtmosphereDepthM || alt < 0.0) return;

            double rho = density(alt);
            if (rho <= 0.0) return;

            // Surface-relative velocity: inertial minus the local rotation, omega about +Z.
            double srx = vx + s.BodyOmega * py;
            double sry = vy - s.BodyOmega * px;
            double srz = vz;
            double sv = Mag(srx, sry, srz);
            if (sv < 0.1) return;

            // a = 0.5 * rho * v² / BC, opposing the surface-relative velocity.
            double a = 0.5 * rho * sv * sv / s.BallisticCoefficient;
            ax -= a * srx / sv;
            ay -= a * sry / sv;
            az -= a * srz / sv;
        }

        private static double Mag(double x, double y, double z)
        {
            return Math.Sqrt(x * x + y * y + z * z);
        }

        // ------------------------------------------------------------------ the measurement

        /// <summary>
        /// Back-solve the ballistic coefficient from an observed drag acceleration.
        ///
        /// `BC = 0.5 * rho * v² / a_drag`, kg/m². The glue measures a_drag as the part of the
        /// vessel's acceleration that is neither gravity nor thrust.
        ///
        /// Returns zero when the measurement cannot mean anything - no air, barely moving, or no
        /// measurable deceleration. Zero propagates as "unknown" and the solve falls back to vacuum
        /// rather than inventing a coefficient.
        /// </summary>
        public static double BallisticCoefficientFrom(double densityKgM3, double surfaceSpeedMps,
                                                      double dragAccelMps2)
        {
            if (densityKgM3 <= 1e-9 || surfaceSpeedMps < 10.0 || dragAccelMps2 < 1e-4) return 0.0;
            return 0.5 * densityKgM3 * surfaceSpeedMps * surfaceSpeedMps / dragAccelMps2;
        }

        /// <summary>
        /// Smooth successive measurements. Drag is noisy tick to tick - the vessel wobbles, parts
        /// occlude each other - and feeding raw samples to the integrator makes the predicted impact
        /// jitter by kilometres, which then makes the guidance chase it.
        ///
        /// A first-order filter with a time constant, so the response does not depend on frame rate.
        /// </summary>
        public static double SmoothBc(double previous, double sample, double dt, double tauS)
        {
            if (sample <= 0.0) return previous;          // a bad sample never poisons the estimate
            if (previous <= 0.0) return sample;          // first good sample seeds it
            if (tauS <= 0.0 || dt <= 0.0) return sample;
            double k = dt / (tauS + dt);
            return previous + (sample - previous) * k;
        }

        /// <summary>Time constant for the BC filter, seconds.</summary>
        public const double BcFilterTauS = 3.0;
    }
}
