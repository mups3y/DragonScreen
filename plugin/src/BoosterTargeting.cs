// DragonScreen — BoosterTargeting  (KSP glue: land the booster ON the droneship / RTLS pad)
// ============================================================================================
// Turns seam-3's retrograde-hold hoverslam into a TARGETED landing. It runs the L1 pure impact predictor
// (pure/Trajectory.cs — RK4 with MEASURED drag) from the booster's live state to a predicted touchdown
// point, compares it to the landing TARGET (a droneship vessel, or whatever the booster has targeted —
// e.g. a Cape Canaveral RTLS pad), and returns the down/cross-range error for pure/GridFin.cs to steer
// out. Closed-loop: re-predicted every tick, so as the fins bite the error shrinks.
//
// ⛔ THE BODY TURNS UNDERNEATH: the predictor lands in the fixed frame it integrated in, but the target is
// fixed to the ROTATING surface, so over a multi-minute fall the target moves tens of km. We correct by
// rotating the predicted inertial impact BACK by the body rotation over the fall (r.BodyRotationRad) into
// the current body-fixed frame before comparing (the same rotation the ground-track uses).
//
// ⚠ FIRST CUT (validate in flight): the ballistic-coefficient measurement, the rotation-correction SIGN,
// and the down/cross-range SIGN into GridFin (a mirrored steer shows in the CSV — one-constant fix). Uses
// drag-only prediction (L/D 0); the fins bend the real path and the closed loop absorbs the difference.
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class BoosterTargeting
    {
        [Tunable] public static double AoaMaxDeg = 20.0;
        [Tunable] public static double GainDegPerKm = 2.0;
        [Tunable] public static double LeadTauS = 3.0;
        [Tunable] public static double CrossSign = 1.0;   // flip if the crossrange steer is mirrored in flight

        public static double LastDownErrM, LastCrossErrM;
        public static bool LastHadTarget;

        // Find the landing target's world position. Prefers the booster's explicit target (the crew can
        // target the droneship or an RTLS pad); else auto-finds a droneship vessel by part marker.
        public static bool FindTarget(Vessel v, out Vector3d targetWorld)
        {
            targetWorld = Vector3d.zero;
            if (v.targetObject != null && v.targetObject.GetTransform() != null)
            { targetWorld = v.targetObject.GetTransform().position; return true; }

            for (int i = 0; i < FlightGlobals.Vessels.Count; i++)
            {
                Vessel o = FlightGlobals.Vessels[i];
                if (o == null || o == v || o.parts == null) continue;
                for (int p = 0; p < o.parts.Count; p++)
                    if (VehicleParts.IsDroneship(o.parts[p].name)) { targetWorld = o.CoM; return true; }
            }
            return false;
        }

        // Predict the booster's touchdown point in the CURRENT body-fixed frame (rotation-corrected).
        public static bool PredictImpact(Vessel v, double ballisticCoeff, double targetAltM, out Vector3d impactWorld)
        {
            impactWorld = Vector3d.zero;
            CelestialBody body = v.mainBody;
            if (body == null || ballisticCoeff <= 0.0) return false;

            TrajectoryInputs ti = new TrajectoryInputs();
            Vector3d rel = (Vector3d)v.CoM - body.position;
            ti.Px = rel.x; ti.Py = rel.y; ti.Pz = rel.z;
            Vector3d vel = v.obt_velocity;                       // body-centred inertial
            ti.Vx = vel.x; ti.Vy = vel.y; ti.Vz = vel.z;
            ti.Mu = body.gravParameter;
            ti.BodyRadiusM = body.Radius;
            ti.BodyOmega = body.rotationPeriod > 0 ? 2.0 * Math.PI / body.rotationPeriod : 0.0;
            ti.AtmosphereDepthM = body.atmosphereDepth;
            ti.BallisticCoefficient = ballisticCoeff;
            ti.ImpactAltitudeM = targetAltM;
            ti.LiftToDrag = 0.0; ti.BankRad = 0.0;

            DensityAt density = delegate (double alt)
            {
                if (!body.atmosphere || alt < 0.0 || alt > body.atmosphereDepth) return 0.0;
                double pres = body.GetPressure(alt);
                double temp = body.GetTemperature(alt);
                double d = body.GetDensity(pres, temp);
                return d > 0.0 ? d : 0.0;
            };

            TrajectoryResult r = Trajectory.Solve(ti, density);
            if (!r.Ok) return false;

            // rotation-correct: rotate the inertial impact back into the current body-fixed frame.
            Vector3d impactRel = new Vector3d(r.Ix, r.Iy, r.Iz) - body.position;
            Vector3d axis = ((Vector3d)body.transform.up).normalized;   // spin axis (north)
            float deg = (float)(-r.BodyRotationRad * 180.0 / Math.PI);
            Vector3d fixedRel = Quaternion.AngleAxis(deg, (Vector3)axis) * (Vector3)impactRel;
            impactWorld = body.position + fixedRel;
            return true;
        }

        // Build the grid-fin steering input from the predicted-impact error vs the target. If there is no
        // target or no usable prediction, returns zero error (retrograde hold) and clears LastHadTarget.
        public static GridFinInputs Steer(Vessel v, double ballisticCoeff)
        {
            GridFinInputs fin = new GridFinInputs();
            fin.AoaMaxDeg = AoaMaxDeg; fin.GainDegPerKm = GainDegPerKm; fin.LeadTauS = LeadTauS;
            LastHadTarget = false; LastDownErrM = 0; LastCrossErrM = 0;

            Vector3d target;
            if (!FindTarget(v, out target)) return fin;

            CelestialBody body = v.mainBody;
            double targetAlt = (body != null) ? body.GetAltitude(target) : 0.0;
            Vector3d impact;
            if (!PredictImpact(v, ballisticCoeff, targetAlt, out impact)) return fin;

            // decompose (impact − target) into the booster's ground-track frame
            Vector3d up = Steering.Up(v);
            Vector3d srf = v.srf_velocity;
            Vector3d horiz = srf - up * Vector3d.Dot(srf, up);
            if (horiz.magnitude < 1.0) return fin;                    // no ground track yet (near-vertical)
            Vector3d downHat = horiz.normalized;
            Vector3d crossHat = Vector3d.Cross(up, downHat).normalized;

            Vector3d err = impact - target;                          // + downrange = predicted LONG
            double downErr = Vector3d.Dot(err, downHat);
            double crossErr = CrossSign * Vector3d.Dot(err, crossHat);

            fin.DownrangeErrM = downErr;
            fin.CrossrangeErrM = crossErr;
            fin.DownrangeRateMps = 0.0;   // first cut: no lead term
            fin.CrossrangeRateMps = 0.0;

            LastDownErrM = downErr; LastCrossErrM = crossErr; LastHadTarget = true;
            return fin;
        }
    }
}
