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
 *
 * ⚠ PROVENANCE (W1, 2026-09-04). Recovered from the tree deleted 2026-09-01. The CODE below is
 * `8b81816^`'s (gen 2) UNCHANGED - including the lift / bank / L-D-band model `a266420` added after
 * `0d6423d`, which the header above therefore does not describe (the LIFT block inside
 * TrajectoryInputs does). Only this header is restored from `0d6423d`: `158eb2a` had stripped it to
 * its four section banners, leaving the file's whole rationale unexplained. R1 §3.5 directs the
 * recovery. No gen-1 logic came with it.
 */
using System;

namespace DragonScreen
{
    public delegate double DensityAt(double altitudeM);

    public delegate double SpeedOfSoundAt(double altitudeM);

    public delegate double DragFactorAt(double mach, double pseudoReynolds);

    public struct TrajectoryInputs
    {
        public double Px, Py, Pz;
        public double Vx, Vy, Vz;
        public double Mu;
        public double BodyRadiusM;
        public double BodyOmega;
        public double AtmosphereDepthM;
        public double BallisticCoefficient;
        public double ImpactAltitudeM;

        // ---- LIFT (from reading the original Trajectories mod, which samples the real aero force at a
        // ---- descent AoA — drag AND lift). We model lift as (L/D)*drag, perpendicular to the surface-
        // ---- relative velocity, rolled by BankRad about it (bank 0 = lift in the vertical plane → range;
        // ---- bank ±90° = lift to a side → crossrange). L/D = 0 (default) is the old drag-only solve, so
        // ---- the vacuum/drag verification still holds. This is what lets the predictor track a grid-fin
        // ---- steered booster and a bank-modulated lifting entry, not just a ballistic fall.
        public double LiftToDrag;    // |lift|/|drag| ; 0 = no lift (drag-only)
        public double BankRad;       // roll of the lift vector about the velocity vector
        public bool UseLdBand;       // B8: model L/D with the 4-band EntryLdBand schedule (by atmosphere-depth
                                     // ratio) instead of the fixed LiftToDrag — a predictor prior for the bands
                                     // not yet measured. Predictor-only; it does NOT command the CoM shifter.

        public System.Collections.Generic.List<PathSample> Path;

        public DragFactorAt DragFactor;
        public SpeedOfSoundAt SoundSpeed;
    }

    public struct PathSample
    {
        public double X, Y, Z, Rot;
    }

    public struct TrajectoryResult
    {
        public bool Ok;
        public double Ix, Iy, Iz;
        public double TimeToImpactS;
        public double ImpactSpeedMps;
        public double BodyRotationRad;
        public bool DragModelled;
        public string Note;
    }

    public static class Trajectory
    {
        public const double VacuumStepS = 2.0;
        public const double AtmoStepS = 0.25;
        public const double DenseStepS = 0.05;
        public const double MaxFlightS = 3600.0;

        public const double PathIntervalS = 3.0;

        public static TrajectoryResult Solve(TrajectoryInputs s, DensityAt density)
        {
            TrajectoryResult r = new TrajectoryResult();
            r.Ok = false;

            double px = s.Px, py = s.Py, pz = s.Pz;
            double vx = s.Vx, vy = s.Vy, vz = s.Vz;
            double t = 0.0;
            double rot = 0.0;
            double lastPathT = -1e9;
            bool draggedEver = false;

            double impactR = s.BodyRadiusM + s.ImpactAltitudeM;
            if (s.Mu <= 0.0 || s.BodyRadiusM <= 0.0) { r.Note = "no body"; return r; }
            if (s.Path != null) { s.Path.Add(Sample(px, py, pz, rot)); lastPathT = 0.0; }

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
                bool inAir = alt < s.AtmosphereDepthM
                             && (s.DragFactor != null || s.BallisticCoefficient > 0.0);
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
                    double prevR = Mag(px, py, pz);
                    double f = (prevR - impactR) / (prevR - newR);
                    if (f < 0.0) f = 0.0; else if (f > 1.0) f = 1.0;
                    r.Ix = px + (nx - px) * f;
                    r.Iy = py + (ny - py) * f;
                    r.Iz = pz + (nz - pz) * f;
                    t += dt * f;
                    rot += s.BodyOmega * dt * f;
                    if (s.Path != null) s.Path.Add(Sample(r.Ix, r.Iy, r.Iz, rot));
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
                if (s.Path != null && t - lastPathT >= PathIntervalS)
                {
                    s.Path.Add(Sample(px, py, pz, rot));
                    lastPathT = t;
                }
            }

            r.Note = "no impact within " + MaxFlightS.ToString("F0") + " s - this does not come down";
            return r;
        }

        private static void Accel(double px, double py, double pz,
                                  double vx, double vy, double vz,
                                  TrajectoryInputs s, DensityAt density,
                                  out double ax, out double ay, out double az)
        {
            double r = Mag(px, py, pz);
            if (r < 1.0) { ax = 0; ay = 0; az = 0; return; }

            double g = -s.Mu / (r * r * r);
            ax = g * px; ay = g * py; az = g * pz;

            bool haveStock = s.DragFactor != null && s.SoundSpeed != null;
            if (!haveStock && s.BallisticCoefficient <= 0.0) return;
            double alt = r - s.BodyRadiusM;
            if (alt >= s.AtmosphereDepthM || alt < 0.0) return;

            double rho = density(alt);
            if (rho <= 0.0) return;

            double srx = vx + s.BodyOmega * py;
            double sry = vy - s.BodyOmega * px;
            double srz = vz;
            double sv = Mag(srx, sry, srz);
            if (sv < 0.1) return;

            double factor;
            if (haveStock)
            {
                double ss = s.SoundSpeed(alt);
                double mach = (ss > 1e-6) ? sv / ss : 0.0;
                if (mach > 25.0) mach = 25.0;
                factor = s.DragFactor(mach, rho * sv);
                if (factor <= 0.0) return;
            }
            else
            {
                factor = 1.0 / s.BallisticCoefficient;
            }

            double a = 0.5 * rho * sv * sv * factor;
            ax -= a * srx / sv;
            ay -= a * sry / sv;
            az -= a * srz / sv;

            // ---- LIFT: (L/D)*drag, perpendicular to the surface-relative velocity, banked about it.
            // ---- Same decomposition the entry guidance uses: vertical lift L·cos(bank) flies RANGE,
            // ---- horizontal lift L·sin(bank) flies CROSSRANGE (Apollo/Orion). L/D=0 → no lift.
            double ld = s.UseLdBand ? EntryLdBand(alt / s.AtmosphereDepthM) : s.LiftToDrag;
            if (ld > 0.0)
            {
                double aL = ld * a;
                double vhx = srx / sv, vhy = sry / sv, vhz = srz / sv;         // unit surface-rel velocity
                double upx = px / r, upy = py / r, upz = pz / r;               // local radial-up
                double dot = upx * vhx + upy * vhy + upz * vhz;
                double lux = upx - dot * vhx, luy = upy - dot * vhy, luz = upz - dot * vhz; // lift-up (bank 0)
                double lulen = Mag(lux, luy, luz);
                if (lulen > 1e-6)                                              // ill-defined on a radial fall
                {
                    lux /= lulen; luy /= lulen; luz /= lulen;
                    double lrx = vhy * luz - vhz * luy;                        // lift-right = vhat x liftUp
                    double lry = vhz * lux - vhx * luz;
                    double lrz = vhx * luy - vhy * lux;
                    double cb = Math.Cos(s.BankRad), sb = Math.Sin(s.BankRad);
                    ax += aL * (cb * lux + sb * lrx);
                    ay += aL * (cb * luy + sb * lry);
                    az += aL * (cb * luz + sb * lrz);
                }
            }
        }

        private static double Mag(double x, double y, double z)
        {
            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static PathSample Sample(double x, double y, double z, double rot)
        {
            PathSample p; p.X = x; p.Y = y; p.Z = z; p.Rot = rot; return p;
        }

        // ------------------------------------------------------------------ the measurement

        public static double BallisticCoefficientFrom(double densityKgM3, double surfaceSpeedMps,
                                                      double dragAccelMps2)
        {
            if (densityKgM3 <= 1e-9 || surfaceSpeedMps < 10.0 || dragAccelMps2 < 1e-4) return 0.0;
            return 0.5 * densityKgM3 * surfaceSpeedMps * surfaceSpeedMps / dragAccelMps2;
        }

        // ------------------------------------------------------------------ the vessel's live aero profile
        // The prediction must be TAILORED TO THE VESSEL FLYING NOW — a grid-fin booster at angle of attack
        // and a Dragon capsule in a bank-modulated lifting entry have very different L/D and bank, so a
        // drag-only assumption is wrong for both. Rather than assume a profile, MEASURE it, exactly as the
        // ballistic coefficient is measured: from the vessel's actual AERO acceleration (its measured
        // acceleration minus gravity — the glue passes it). Split that into DRAG (opposite the surface
        // velocity), LIFT (perpendicular), and the BANK of the lift about the velocity. Feeds β, L/D and
        // bank straight into Solve, so the predicted footprint reflects the real lifting flight.

        public struct AeroProfile
        {
            public double DragAccel;    // m/s^2 along −velocity (the ballistic-coefficient source)
            public double LiftAccel;    // m/s^2 perpendicular to velocity
            public double LiftToDrag;   // |lift| / |drag|
            public double BankRad;      // orientation of the lift vector about velocity (0 = lift up)
            public bool   Valid;
        }

        public static AeroProfile MeasureAero(double aax, double aay, double aaz,   // aero accel, world
                                              double svx, double svy, double svz,   // surface-rel velocity
                                              double upx, double upy, double upz)   // local radial-up
        {
            AeroProfile p = new AeroProfile();
            double sv = Mag(svx, svy, svz);
            if (sv < 10.0) return p;
            double vhx = svx / sv, vhy = svy / sv, vhz = svz / sv;

            double along = aax * vhx + aay * vhy + aaz * vhz;      // <0 while decelerating
            double drag = -along;                                  // drag magnitude (positive)
            double lx = aax - along * vhx, ly = aay - along * vhy, lz = aaz - along * vhz;
            double lift = Mag(lx, ly, lz);
            p.DragAccel = drag; p.LiftAccel = lift;
            if (drag <= 1e-4) return p;                            // no measurable drag → no profile yet
            p.LiftToDrag = lift / drag; p.Valid = true;

            // bank: angle of the lift vector between local "up" (radial ⟂ v) and "right" (v × up).
            double uup = upx * vhx + upy * vhy + upz * vhz;
            double lux = upx - uup * vhx, luy = upy - uup * vhy, luz = upz - uup * vhz;
            double ll = Mag(lux, luy, luz);
            if (ll > 1e-6 && lift > 1e-6)
            {
                lux /= ll; luy /= ll; luz /= ll;
                double lrx = vhy * luz - vhz * luy, lry = vhz * lux - vhx * luz, lrz = vhx * luy - vhy * lux;
                double cu = (lx * lux + ly * luy + lz * luz) / lift;
                double cr = (lx * lrx + ly * lry + lz * lrz) / lift;
                p.BankRad = Math.Atan2(cr, cu);
            }
            return p;
        }

        public static double SmoothBc(double previous, double sample, double dt, double tauS)
        {
            if (sample <= 0.0) return previous;
            if (previous <= 0.0) return sample;
            if (tauS <= 0.0 || dt <= 0.0) return sample;
            double k = dt / (tauS + dt);
            return previous + (sample - previous) * k;
        }

        public const double BcFilterTauS = 3.0;

        // ------------------------------------------------------------------ B8: 4-band entry L/D schedule
        // The blunt fixed-CoM capsule's trim AoA — and hence L/D — varies naturally with Mach/altitude across
        // the descent; a single constant is only right for the band being flown NOW. This schedules L/D vs
        // atmosphere-depth ratio (alt / atmosphereDepth) in the four Trajectories bands, Lerp between band
        // centres (AUTOPILOT_MINING_3 §2c): AtmosEntry 50–100%, HighAltitude 25–50%, LowAltitude 5–25%,
        // FinalApproach <5%. Values within the Dragon L/D 0.18–0.27 envelope. ⛔ This is a PREDICTOR MODEL only
        // (used when TrajectoryInputs.UseLdBand is set) — it does NOT command the CoM shifter, which is engaged
        // ONCE and never toggled to steer (the entry hard rule). The live MeasureAero L/D still overrides when
        // available; the schedule is the prior for bands not yet flown.
        //
        // ⛔ UN-CONVERGED FOR RSS-RO (§B16.8 ruling 2, W22 2026-09-04). R1 §5.1: no lifting entry has ever been
        // flown, so a value inside the "Dragon L/D 0.18–0.27 envelope" above is a prior, not a measurement —
        // honestly self-marked in the prose above (the comment already says "not yet measured"/"not yet
        // flown"), but that disclosure carried no `[UN-CONVERGED]` tag until now, unlike every other recovered
        // file holding an unattributed number (`Hoverslam`, `GridFin`, `ThrustBalance`, `RcsBalance`,
        // `WarpPlan`, `BoosterDescent`, `Actuator`). Re-converge from a recorded RSS-RO lifting-entry flight
        // before trusting the schedule for a commanded divert (ruling 3 — needs glass time, a SEPARATE owner
        // gate). ℹ Today's booster path does NOT touch this schedule: `BoosterDescent.cs:463-464` sets
        // `UseLdBand = false` for the booster's vacuum-fallback solve, so this is a marking gap, not a live
        // wrong number — the only live consumer is entry guidance, once §B16.5/W16 wires it up.
        [Tunable] public static double LdAtmosEntry    = 0.18;   // [UN-CONVERGED] 50–100% depth (thin air, hypersonic)
        [Tunable] public static double LdHighAltitude  = 0.20;   // [UN-CONVERGED] 25–50%
        [Tunable] public static double LdLowAltitude   = 0.26;   // [UN-CONVERGED] 5–25% (dense, near peak L/D)
        [Tunable] public static double LdFinalApproach = 0.24;   // [UN-CONVERGED] <5% (subsonic terminal)

        public static double EntryLdBand(double altRatio)
        {
            double r = altRatio < 0.0 ? 0.0 : (altRatio > 1.0 ? 1.0 : altRatio);
            // band centres by ratio: FinalApproach .025, LowAltitude .15, HighAltitude .375, AtmosEntry .75
            if (r >= 0.75)  return LdAtmosEntry;
            if (r >= 0.375) return Lerp(r, 0.375, 0.75, LdHighAltitude, LdAtmosEntry);
            if (r >= 0.15)  return Lerp(r, 0.15, 0.375, LdLowAltitude, LdHighAltitude);
            if (r >= 0.025) return Lerp(r, 0.025, 0.15, LdFinalApproach, LdLowAltitude);
            return LdFinalApproach;
        }

        private static double Lerp(double x, double x0, double x1, double y0, double y1)
        {
            double t = (x - x0) / (x1 - x0);
            return y0 + (y1 - y0) * t;
        }
    }
}
