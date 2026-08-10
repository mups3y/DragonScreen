/*
 * DragonScreen - ImpactPredictor
 *
 * GLUE. Measures each vehicle's drag in flight and integrates its trajectory to a ground impact.
 * Law in `pure/Trajectory.cs`.
 *
 * ---- WHAT THIS REPLACES ----
 * A vacuum ballistic solve, which is fine for a boostback that deliberately overshoots by 2.7 km and
 * useless for a de-orbit burn with a 50 m stop tolerance. Drag only ever shortens a trajectory, so
 * the vacuum answer is always LONG - by tens of kilometres on an entry.
 *
 * ---- ⛔ THE DRAG IS MEASURED FROM THE VEHICLE, NOT MODELLED ----
 * a_drag = a_total − a_gravity − a_thrust, taken from the vessel's own velocity change over a
 * physics tick. Everything KSP's aerodynamics does - drag cubes, occlusion, orientation, deployed
 * fins, a jettisoned trunk - arrives in that one number without anyone writing a coefficient down.
 *
 * `falcon-detect-by-capability` is the same principle: ask the vehicle what it is, do not describe
 * it. A hard-coded Cd·A would be wrong the first time a booster put its grid fins out.
 *
 * ---- ⚠ AND IT IS ONLY VALID WHERE IT WAS MEASURED ----
 * A coefficient measured at Mach 5 in thin air does not describe the same vehicle subsonic at sea
 * level, and KSP's drag really does vary with Mach. The estimate is continuously re-measured and
 * time-filtered, so it tracks; it is NOT a constant fitted once. Where no measurement is possible -
 * vacuum, or barely moving - it reports zero and the integrator falls back to a vacuum solve and
 * says which it did. A prediction that quietly changes meaning is worse than one that admits it.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    /// <summary>A predicted impact, in the terms the guidance actually wants.</summary>
    public struct Impact
    {
        public bool Valid;
        public double LatDeg, LonDeg;
        public double TimeToImpactS, SpeedMps;
        /// <summary>True when the integration actually modelled drag.</summary>
        public bool DragModelled;
        public string Note;
    }

    public static class ImpactPredictor
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Ballistic coefficient estimate per vessel, kg/m². Zero means not yet known.</summary>
        private static readonly System.Collections.Generic.Dictionary<uint, double> bc =
            new System.Collections.Generic.Dictionary<uint, double>();

        private static readonly System.Collections.Generic.Dictionary<uint, Vector3d> lastVel =
            new System.Collections.Generic.Dictionary<uint, Vector3d>();

        /// <summary>
        /// When each vessel was last sampled.
        ///
        /// ---- ⛔ THIS WAS ONE SHARED FIELD, AND IT MEANT ONLY ONE VEHICLE WAS EVER MEASURED ----
        /// `FlightDriver` samples the ascent vehicle and then the booster in the same frame. With a
        /// single timestamp the first call stamped it to now, so the second computed dt = 0, hit the
        /// `dt &lt;= 0` guard and returned - every frame, for the whole flight. The booster's ballistic
        /// coefficient stayed at zero, which silently downgrades its impact prediction to a VACUUM
        /// solve: always long, by kilometres, on the one vehicle whose whole descent is a drag problem.
        /// It fails invisibly because `Predict` still returns an answer and only `DragModelled` says
        /// which kind. Per-vessel, like everything else in here.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<uint, double> lastSampleUt =
            new System.Collections.Generic.Dictionary<uint, double>();

        public static void Reset() { bc.Clear(); lastVel.Clear(); lastSampleUt.Clear(); }

        /// <summary>The current estimate for a vessel, for the recorder. Zero = unknown.</summary>
        public static double BallisticCoefficient(Vessel v)
        {
            if (v == null) return 0.0;
            double b;
            return bc.TryGetValue(v.persistentId, out b) ? b : 0.0;
        }

        /// <summary>
        /// Sample a vessel's drag. Call every tick for every vehicle whose trajectory matters.
        ///
        /// The measurement is only taken when it can mean something: in air, moving, and with a
        /// decelerating component that is not just thrust. Anything else leaves the estimate alone
        /// rather than dragging it toward a meaningless number.
        /// </summary>
        public static void Sample(Vessel v)
        {
            if (v == null || v.mainBody == null || v.packed) return;
            double now = Planetarium.GetUniversalTime();
            uint id = v.persistentId;

            Vector3d vel = v.obt_velocity;
            Vector3d prev;
            bool havePrev = lastVel.TryGetValue(id, out prev);
            lastVel[id] = vel;

            double was;
            bool haveWas = lastSampleUt.TryGetValue(id, out was);
            lastSampleUt[id] = now;
            double dt = haveWas ? now - was : 0.0;
            if (!havePrev || dt <= 0.0 || dt > 1.0) return;      // a warp jump is not a measurement

            CelestialBody b = v.mainBody;
            double alt = v.altitude;
            if (alt >= b.atmosphereDepth) return;                // nothing to measure in vacuum

            // a_total from the vessel's own velocity change.
            Vector3d aTotal = (vel - prev) / dt;

            // Subtract gravity.
            Vector3d r = v.CoM - b.position;
            double rm = r.magnitude;
            if (rm < 1.0) return;
            Vector3d aGrav = -r.normalized * (b.gravParameter / (rm * rm));

            // Subtract thrust, along the nose.
            double thrust = LiveThrust(v);
            double mass = v.GetTotalMass();
            if (mass <= 0.0) return;
            Vector3d aThrust = (Vector3d)v.ReferenceTransform.up * (thrust / mass);

            Vector3d aDrag = aTotal - aGrav - aThrust;

            // Only the component OPPOSING the surface velocity is drag. Lift and noise are not, and
            // taking the whole magnitude would inflate the estimate on a lifting entry.
            Vector3d srf = v.srf_velocity;
            if (srf.magnitude < 10.0) return;
            double along = Vector3d.Dot(aDrag, -srf.normalized);
            if (along <= 0.0) return;                            // accelerating; not a drag sample

            double rho = b.GetDensity(b.GetPressure(alt), b.GetTemperature(alt));
            double sample = Trajectory.BallisticCoefficientFrom(rho, srf.magnitude, along);
            if (sample <= 0.0) return;

            double old;
            if (!bc.TryGetValue(id, out old)) old = 0.0;
            bc[id] = Trajectory.SmoothBc(old, sample, dt, Trajectory.BcFilterTauS);
        }

        /// <summary>
        /// Integrate this vessel's trajectory to a ground impact.
        ///
        /// The impact comes out as a LAT/LON, with the body's rotation during the flight already
        /// removed - a 600 km body turning once per six hours moves 175 m/s at the equator, which is
        /// kilometres of error over a long entry.
        /// </summary>
        public static Impact Predict(Vessel v)
        {
            Impact im = new Impact();
            if (v == null || v.mainBody == null) { im.Note = "no vessel"; return im; }
            CelestialBody b = v.mainBody;

            TrajectoryInputs s = new TrajectoryInputs();
            Vector3d r = v.CoM - b.position;
            Vector3d vel = v.obt_velocity;
            s.Px = r.x; s.Py = r.y; s.Pz = r.z;
            s.Vx = vel.x; s.Vy = vel.y; s.Vz = vel.z;
            s.Mu = b.gravParameter;
            s.BodyRadiusM = b.Radius;
            s.AtmosphereDepthM = b.atmosphereDepth;
            s.BallisticCoefficient = BallisticCoefficient(v);
            s.ImpactAltitudeM = 0.0;

            // ⚠ THE ROTATION AXIS IS THE BODY'S, NOT THE FRAME'S +Z. Integrating about the wrong
            // axis puts the ground track sideways, which reads as a cross-range error nobody can
            // account for. KSP gives the body's angular velocity directly; use it.
            s.BodyOmega = b.angularVelocity.magnitude;

            // The integrator works in a frame whose +Z is the rotation axis, so hand it a state
            // already expressed there rather than rotating the answer back afterwards.
            Vector3d axis = b.angularVelocity.normalized;
            if (axis.sqrMagnitude < 0.5) axis = b.transform.up;
            QuaternionD toAxis = (QuaternionD)Quaternion.FromToRotation((Vector3)axis, Vector3.forward);
            Vector3d rp = toAxis * r;
            Vector3d vp = toAxis * vel;
            s.Px = rp.x; s.Py = rp.y; s.Pz = rp.z;
            s.Vx = vp.x; s.Vy = vp.y; s.Vz = vp.z;

            TrajectoryResult t = Trajectory.Solve(s, delegate(double alt)
            {
                if (alt < 0.0 || alt >= b.atmosphereDepth) return 0.0;
                return b.GetDensity(b.GetPressure(alt), b.GetTemperature(alt));
            });

            im.Note = t.Note;
            im.DragModelled = t.DragModelled;
            if (!t.Ok) return im;

            // Back out of the rotated frame, then take the rotation the body did during the flight
            // off the longitude - the ground moved under us.
            Vector3d ip = new Vector3d(t.Ix, t.Iy, t.Iz);
            Vector3d world = (QuaternionD)Quaternion.Inverse(
                Quaternion.FromToRotation((Vector3)axis, Vector3.forward)) * ip;

            im.LatDeg = b.GetLatitude(b.position + world);
            im.LonDeg = b.GetLongitude(b.position + world)
                      - t.BodyRotationRad * 180.0 / Math.PI;
            while (im.LonDeg < -180.0) im.LonDeg += 360.0;
            while (im.LonDeg > 180.0) im.LonDeg -= 360.0;

            im.TimeToImpactS = t.TimeToImpactS;
            im.SpeedMps = t.ImpactSpeedMps;
            im.Valid = true;
            return im;
        }

        /// <summary>
        /// Ground distance from a predicted impact to a target, metres. Negative when the predictor
        /// has no answer - a distinct case from a zero miss, and the de-orbit law relies on that.
        /// </summary>
        public static double MissTo(Vessel v, double latDeg, double lonDeg)
        {
            Impact im = Predict(v);
            if (!im.Valid || v.mainBody == null) return -1.0;
            return BoosterRecovery.GroundRange(v.mainBody, im.LatDeg, im.LonDeg, latDeg, lonDeg);
        }

        private static double LiveThrust(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (es[m].EngineIgnited && !es[m].flameout) t += es[m].finalThrust;
            }
            return t;
        }
    }
}
