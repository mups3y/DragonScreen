// DragonScreen - ImpactPredictor
// ---- WHAT THIS REPLACES ----
// ---- ⛔ THE DRAG IS MEASURED FROM THE VEHICLE, NOT MODELLED ----
// ---- ⚠ AND IT IS ONLY VALID WHERE IT WAS MEASURED ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public struct Impact
    {
        public bool Valid;
        public double LatDeg, LonDeg;
        public double TimeToImpactS, SpeedMps;
        public bool DragModelled;
        public string Note;
    }

    public static class ImpactPredictor
    {
        private const string Tag = "[DragonScreen] ";

        // ---- ⛔ FAR-CLEAN DRAG SAMPLING (2026-08-23) ----

        public const double ThrustCleanMaxKn = 1.0;

        public const double QSampleFloorPa = 100.0;

        private static readonly System.Collections.Generic.Dictionary<uint, double> bc =
            new System.Collections.Generic.Dictionary<uint, double>();

        private static readonly System.Collections.Generic.Dictionary<uint, Vector3d> lastVel =
            new System.Collections.Generic.Dictionary<uint, Vector3d>();

        /// ---- ⛔ THIS WAS ONE SHARED FIELD, AND IT MEANT ONLY ONE VEHICLE WAS EVER MEASURED ----
        private static readonly System.Collections.Generic.Dictionary<uint, double> lastSampleUt =
            new System.Collections.Generic.Dictionary<uint, double>();

        public static void Reset()
        {
            bc.Clear(); lastVel.Clear(); lastSampleUt.Clear();
            MapValid = false; if (MapPath != null) MapPath.Clear();
        }

        public static double BallisticCoefficient(Vessel v)
        {
            if (v == null) return 0.0;
            double b;
            return bc.TryGetValue(v.persistentId, out b) ? b : 0.0;
        }

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
            if (!havePrev || dt <= 0.0 || dt > 1.0) return;

            CelestialBody b = v.mainBody;
            double alt = v.altitude;
            if (alt >= b.atmosphereDepth) return;

            Vector3d aTotal = (vel - prev) / dt;

            Vector3d r = v.CoM - b.position;
            double rm = r.magnitude;
            if (rm < 1.0) return;
            Vector3d aGrav = -r.normalized * (b.gravParameter / (rm * rm));

            double thrust = LiveThrust(v);
            if (thrust > ThrustCleanMaxKn) return;

            double mass = v.GetTotalMass();
            if (mass <= 0.0) return;
            Vector3d aThrust = (Vector3d)v.ReferenceTransform.up * (thrust / mass);

            Vector3d aDrag = aTotal - aGrav - aThrust;

            Vector3d srf = v.srf_velocity;
            if (srf.magnitude < 10.0) return;
            double along = Vector3d.Dot(aDrag, -srf.normalized);
            if (along <= 0.0) return;

            double rho = b.GetDensity(b.GetPressure(alt), b.GetTemperature(alt));

            double q = 0.5 * rho * srf.magnitude * srf.magnitude;
            if (q < QSampleFloorPa) return;

            double sample = Trajectory.BallisticCoefficientFrom(rho, srf.magnitude, along);
            if (sample <= 0.0) return;

            double old;
            if (!bc.TryGetValue(id, out old)) old = 0.0;
            bc[id] = Trajectory.SmoothBc(old, sample, dt, Trajectory.BcFilterTauS);
        }

        public static Impact Predict(Vessel v) { return Predict(v, 0.0); }

        public static Impact Predict(Vessel v, double bcOverride)
        {
            if (v == null || v.mainBody == null) { Impact im = new Impact(); im.Note = "no vessel"; return im; }
            CelestialBody b = v.mainBody;
            // ---- ⛔ MEASURED DRAG, NOT THE STOCK DRAG-CUBE TABLE. FAR IS THE AERODYNAMICS ON EARTH. ----
            double measured = BallisticCoefficient(v);
            double useBc = measured > 0.0 ? measured : bcOverride;
            return PredictFromState(b, v.CoM - b.position, v.obt_velocity, useBc);
        }

        public static Impact PredictFromState(CelestialBody b, Vector3d posRelBody, Vector3d velWorld,
                                              double bc)
        {
            return PredictFromState(b, posRelBody, velWorld, bc, null, null);
        }

        public static Impact PredictBooster(Vessel v)
        {
            if (v == null || v.mainBody == null) { Impact im = new Impact(); im.Note = "no vessel"; return im; }
            CelestialBody b = v.mainBody;
            return PredictFromState(b, v.CoM - b.position, v.obt_velocity, 0.0,
                                    BoosterDrag.DragFactor, SoundSpeedFor(b));
        }

        private static SpeedOfSoundAt SoundSpeedFor(CelestialBody b)
        {
            CelestialBody body = b;
            return delegate(double alt)
            {
                double p = body.GetPressure(alt);
                double rho = StockDensity(body, alt);
                return (rho > 1e-6 && p > 0.0) ? Math.Sqrt(1.4 * p / rho) : 0.0;
            };
        }

        public static Impact PredictFromState(CelestialBody b, Vector3d posRelBody, Vector3d velWorld,
                                              double bc, DragFactorAt dragFactor, SpeedOfSoundAt soundSpeed)
        {
            Impact im = new Impact();
            if (b == null) { im.Note = "no body"; return im; }

            TrajectoryInputs s = new TrajectoryInputs();
            Vector3d r = posRelBody;
            Vector3d vel = velWorld;
            s.Px = r.x; s.Py = r.y; s.Pz = r.z;
            s.Vx = vel.x; s.Vy = vel.y; s.Vz = vel.z;
            s.Mu = b.gravParameter;
            s.BodyRadiusM = b.Radius;
            s.AtmosphereDepthM = b.atmosphereDepth;
            s.BallisticCoefficient = bc;
            s.DragFactor = dragFactor;
            s.SoundSpeed = soundSpeed;
            s.ImpactAltitudeM = 0.0;

            s.BodyOmega = b.angularVelocity.magnitude;

            Vector3d axis = b.angularVelocity.normalized;
            if (axis.sqrMagnitude < 0.5) axis = b.transform.up;
            QuaternionD toAxis = (QuaternionD)Quaternion.FromToRotation((Vector3)axis, Vector3.forward);
            Vector3d rp = toAxis * r;
            Vector3d vp = toAxis * vel;
            s.Px = rp.x; s.Py = rp.y; s.Pz = rp.z;
            s.Vx = vp.x; s.Vy = vp.y; s.Vz = vp.z;

            CelestialBody db = b;
            TrajectoryResult t = Trajectory.Solve(s, delegate(double alt)
            {
                if (alt < 0.0 || alt >= db.atmosphereDepth) return 0.0;
                return StockDensity(db, alt);
            });

            im.Note = t.Note;
            im.DragModelled = t.DragModelled;
            if (!t.Ok) return im;

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

        // ------------------------------------------------------------------ map-view trajectory

        public static List<Vector3d> MapPath = new List<Vector3d>();
        public static Vector3d MapImpact;
        public static Vector3d MapTarget;
        public static CelestialBody MapBody;
        public static bool MapValid;
        public static double MapStampUt;

        public static void UpdateMapTrajectory(Vessel v, double bcOverride, double tgtLatDeg, double tgtLonDeg)
        {
            if (v == null || v.mainBody == null) { MapValid = false; return; }
            CelestialBody b = v.mainBody;

            double measured = BallisticCoefficient(v);
            double useBc = measured > 0.0 ? measured : bcOverride;

            Vector3d r = v.CoM - b.position;
            Vector3d vel = v.obt_velocity;

            TrajectoryInputs s = new TrajectoryInputs();
            s.Mu = b.gravParameter;
            s.BodyRadiusM = b.Radius;
            s.AtmosphereDepthM = b.atmosphereDepth;
            s.BallisticCoefficient = useBc;
            s.ImpactAltitudeM = 0.0;
            s.BodyOmega = b.angularVelocity.magnitude;

            Vector3d axis = b.angularVelocity.normalized;
            if (axis.sqrMagnitude < 0.5) axis = b.transform.up;
            QuaternionD toAxis = (QuaternionD)Quaternion.FromToRotation((Vector3)axis, Vector3.forward);
            QuaternionD fromAxis = (QuaternionD)Quaternion.Inverse(
                Quaternion.FromToRotation((Vector3)axis, Vector3.forward));
            Vector3d rp = toAxis * r, vp = toAxis * vel;
            s.Px = rp.x; s.Py = rp.y; s.Pz = rp.z;
            s.Vx = vp.x; s.Vy = vp.y; s.Vz = vp.z;

            List<PathSample> raw = new List<PathSample>();
            s.Path = raw;

            CelestialBody db = b;
            TrajectoryResult t = Trajectory.Solve(s, delegate(double alt)
            {
                if (alt < 0.0 || alt >= db.atmosphereDepth) return 0.0;
                return StockDensity(db, alt);
            });

            if (!t.Ok || raw.Count < 2) { MapValid = false; return; }

            MapPath.Clear();
            for (int i = 0; i < raw.Count; i++)
            {
                PathSample ps = raw[i];
                double c = Math.Cos(-ps.Rot), sn = Math.Sin(-ps.Rot);
                Vector3d deRot = new Vector3d(ps.X * c - ps.Y * sn, ps.X * sn + ps.Y * c, ps.Z);
                MapPath.Add(fromAxis * deRot);
            }
            MapImpact = MapPath[MapPath.Count - 1];
            MapTarget = (Vector3d)b.GetWorldSurfacePosition(tgtLatDeg, tgtLonDeg, 0.0) - b.position;
            MapBody = b;
            MapValid = true;
            MapStampUt = Planetarium.GetUniversalTime();
        }

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

        // ------------------------------------------------------------------ stock aero (Trajectories)

        public static double StockDensity(CelestialBody body, double altitude)
        {
            if (body == null || !body.atmosphere || altitude > body.atmosphereDepth || altitude < 0.0)
                return 0.0;
            double pressure = body.GetPressure(altitude);
            const double sunDot = 0.5;
            const float sunAxialDot = 0f;
            double tempOffset = body.latitudeTemperatureBiasCurve.Evaluate(0f)
                              + body.latitudeTemperatureSunMultCurve.Evaluate(0f) * sunDot
                              + body.axialTemperatureSunMultCurve.Evaluate(sunAxialDot);
            double temperature = body.GetTemperature(altitude)
                               + body.atmosphereTemperatureSunMultCurve.Evaluate((float)altitude) * tempOffset;
            return body.GetDensity(pressure, temperature);
        }
    }
}
