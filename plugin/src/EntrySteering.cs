// DragonScreen — EntrySteering  (KSP glue: the lifting bank-angle entry — footprint + bank measurement)
// ============================================================================================
// The two live inputs the pure entry guidance (pure/Entry.cs) needs to fly the splashdown zone:
//   1. the predicted FOOTPRINT ERROR — run the L1 impact predictor (pure/Trajectory.cs) WITH LIFT
//      (L/D ≈ 0.2 from the offset CoM, banked by the current σ) and a measured ballistic coefficient,
//      rotation-corrected (the body turns under the long entry), compared to the splashdown TARGET
//      (the capsule's target — a recovery ship / splashdown waypoint the crew sets); and
//   2. the MEASURED BANK angle — the vehicle's roll about the velocity axis vs the lift-up reference —
//      so the roll loop can drive it to the commanded σ.
// Entry.Guide then sets |σ| to null downrange and reverses sign on the crossrange deadband (the S-turns);
// the roll loop (ReturnControl) banks the capsule to σ while SAS holds it shield-forward.
//
// ⚠ FIRST CUT (validate in flight): the ballistic-coefficient + L/D used, the rotation-correction sign,
// the down/cross-range signs into Entry, and the bank-measurement sign / body roll-reference axis (a
// constant roll offset shows as a steady-state bank error — a one-constant fix). No target → nominal
// reference bank (still a stable lifting entry, no precise footprint control).
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class EntrySteering
    {
        [Tunable] public static double EntryLoverD = 0.2;    // offset-CoM lift-to-drag for the predictor
        [Tunable] public static double CrossSign = 1.0;      // flip if the crossrange steer is mirrored
        [Tunable] public static double RollRefSign = 1.0;    // flip if the measured bank sign is inverted

        public static double LastSigmaRad;                   // the bank the predictor assumes (prev command)
        public static double LastDownErrM, LastCrossErrM;
        public static bool LastHadTarget;
        static double smoothedBc;

        // Measure the ballistic coefficient from the felt drag (accelerometer) while entering.
        public static void MeasureBc(Vessel v)
        {
            CelestialBody body = v.mainBody;
            if (body == null || !body.atmosphere) return;
            double alt = v.altitude;
            if (alt < 0 || alt > body.atmosphereDepth) return;
            double speed = v.srfSpeed;
            if (speed < 50.0) return;
            double rho = body.GetDensity(body.GetPressure(alt), body.GetTemperature(alt));
            double dragAccel = v.geeForce * 9.80665;
            double bc = Trajectory.BallisticCoefficientFrom(rho, speed, dragAccel);
            smoothedBc = Trajectory.SmoothBc(smoothedBc, bc, TimeWarp.fixedDeltaTime, Trajectory.BcFilterTauS);
        }

        // The predicted footprint error (downrange +, crossrange +) vs the splashdown target, in the
        // capsule's ground-track frame. Returns false (and zero error) if there is no target / prediction.
        public static bool FootprintError(Vessel v, out double downErr, out double crossErr)
        {
            downErr = 0; crossErr = 0; LastHadTarget = false;
            CelestialBody body = v.mainBody;
            if (body == null || smoothedBc <= 0.0) return false;
            if (v.targetObject == null || v.targetObject.GetTransform() == null) return false;
            Vector3d target = v.targetObject.GetTransform().position;
            double targetAlt = body.GetAltitude(target);

            TrajectoryInputs ti = new TrajectoryInputs();
            Vector3d rel = (Vector3d)v.CoM - body.position;
            ti.Px = rel.x; ti.Py = rel.y; ti.Pz = rel.z;
            Vector3d vi = v.obt_velocity;
            ti.Vx = vi.x; ti.Vy = vi.y; ti.Vz = vi.z;
            ti.Mu = body.gravParameter; ti.BodyRadiusM = body.Radius;
            ti.BodyOmega = body.rotationPeriod > 0 ? 2.0 * Math.PI / body.rotationPeriod : 0.0;
            ti.AtmosphereDepthM = body.atmosphereDepth;
            ti.BallisticCoefficient = smoothedBc;
            ti.ImpactAltitudeM = targetAlt;
            ti.LiftToDrag = EntryLoverD;              // LIFTING prediction
            ti.BankRad = LastSigmaRad;                // under the current bank

            DensityAt density = delegate (double alt)
            {
                if (!body.atmosphere || alt < 0.0 || alt > body.atmosphereDepth) return 0.0;
                double d = body.GetDensity(body.GetPressure(alt), body.GetTemperature(alt));
                return d > 0.0 ? d : 0.0;
            };
            TrajectoryResult r = Trajectory.Solve(ti, density);
            if (!r.Ok) return false;

            // rotation-correct the inertial impact into the current body-fixed frame
            Vector3d impactRel = new Vector3d(r.Ix, r.Iy, r.Iz) - body.position;
            Vector3d axis = ((Vector3d)body.transform.up).normalized;
            Vector3d fixedRel = Quaternion.AngleAxis((float)(-r.BodyRotationRad * 180.0 / Math.PI), (Vector3)axis) * (Vector3)impactRel;
            Vector3d impact = body.position + fixedRel;

            // decompose (impact − target) into the ground-track frame
            Vector3d up = Steering.Up(v);
            Vector3d srf = v.srf_velocity;
            Vector3d horiz = srf - up * Vector3d.Dot(srf, up);
            if (horiz.magnitude < 1.0) return false;
            Vector3d downHat = horiz.normalized;
            Vector3d crossHat = Vector3d.Cross(up, downHat).normalized;
            Vector3d err = impact - target;
            downErr = Vector3d.Dot(err, downHat);
            crossErr = CrossSign * Vector3d.Dot(err, crossHat);

            LastDownErrM = downErr; LastCrossErrM = crossErr; LastHadTarget = true;
            return true;
        }

        // The vehicle's current bank: the roll about the velocity axis of its roll-reference (ct.forward)
        // relative to the lift-up direction (radial, perpendicular to velocity). Same 0=lift-up convention
        // as Entry's σ; RollRefSign flips it if the body reference axis is inverted.
        public static double MeasuredBankRad(Vessel v)
        {
            Vector3d srf = v.srf_velocity;
            if (srf.magnitude < 1.0) return 0.0;
            Vector3d velHat = srf.normalized;
            Vector3d up = Steering.Up(v);
            Vector3d liftUp = (up - velHat * Vector3d.Dot(up, velHat));
            if (liftUp.magnitude < 1e-6) return 0.0;
            liftUp = liftUp.normalized;
            Vector3d liftRight = Vector3d.Cross(velHat, liftUp).normalized;

            Transform ct = v.ReferenceTransform;
            if (ct == null) return 0.0;
            Vector3d refA = RollRefSign * (Vector3d)ct.forward;
            Vector3d refPerp = refA - velHat * Vector3d.Dot(refA, velHat);
            if (refPerp.magnitude < 1e-6) return 0.0;
            refPerp = refPerp.normalized;
            double c = Vector3d.Dot(refPerp, liftUp), s = Vector3d.Dot(refPerp, liftRight);
            return Math.Atan2(s, c);
        }

        public static void Reset() { smoothedBc = 0; LastSigmaRad = 0; LastHadTarget = false; }
    }
}
