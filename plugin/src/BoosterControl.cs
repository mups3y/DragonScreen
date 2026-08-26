// DragonScreen — BoosterControl  (KSP glue: seam 3, the first-stage recovery controller)
// ============================================================================================
// Flies the SEPARATED first stage back to a landing with the PURE guidance (pure/BoosterDescent.cs FSM,
// pure/Hoverslam.cs ignition solver, pure/GridFin.cs steering). After MECO the booster is its OWN vessel;
// this runs when it is the ACTIVE vessel (the player focuses it after sep — KSP only fully simulates the
// focused craft, and HullCams follows the booster for exactly this). The FlightDriver KSPAddon persists
// across the focus-switch, so control carries over.
//
// The descent: FLIP engines-first (hold retrograde) → ENTRY BURN (3-engine, ThreeLanding) to shed the
// re-entry speed → aero descent on the grid fins → HOVERSLAM on the single CENTRE engine (CenterOnly),
// one continuous max-thrust brake to v=0 at the deck. ⛔ ENGINE MODES ARE SELECTED ABSOLUTELY BY
// ACTIVATING THE MATCHING-engineID ModuleEngines WHILE OFF — never NextEngineMode (which cycles), and the
// centre engine is lit ONCE and never re-lit mid-burn (each octaweb mode has one ignition — craftdump).
//
// ⚠ FIRST CUT (validate in flight): retrograde-hold descent + hoverslam (lands the booster upright at the
// ballistic point). Precise droneship/RTLS targeting via the L1 impact predictor + a target in the
// profile is the next refinement. Instrumented into the FlightRecorder; attitude on the SAS inner loop
// (gimbal when lit, cold-gas/fins otherwise). Defensive throughout.
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class BoosterControl
    {
        static BoosterPhase phase = BoosterPhase.Idle;
        static int currentMode = -1;         // -1 = none selected; 0/3/1 = AllEngines/ThreeLanding/CenterOnly
        static bool legsDown, finsOut;
        static double smoothedBc;            // measured ballistic coefficient (for the impact predictor)

        static double lastAoa, lastIgniteAlt, lastDescentSpeed;
        static string lastPhaseWord = "IDLE";

        public static void Reset()
        {
            phase = BoosterPhase.Idle; currentMode = -1; legsDown = false; finsOut = false;
            smoothedBc = 0.0;
        }

        // The active vessel is a lone booster to recover: it carries S1 parts and NO Dragon pod (so it is
        // the separated first stage, not the full stack on the pad), and it is airborne.
        public static bool IsRecoverableBooster(Vessel v)
        {
            if (v == null || !HighLogic.LoadedSceneIsFlight) return false;
            if (v.situation == Vessel.Situations.PRELAUNCH || v.situation == Vessel.Situations.LANDED
                || v.situation == Vessel.Situations.SPLASHED) return false;
            bool hasBooster = false, hasPod = false;
            for (int i = 0; i < v.parts.Count; i++)
            {
                string n = v.parts[i].name;
                if (VehicleParts.IsPod(n)) { hasPod = true; break; }
                if (VehicleParts.IsBooster(n)) hasBooster = true;
            }
            return hasBooster && !hasPod;
        }

        public static void Tick(Vessel v)
        {
            try { Fly(v); }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] booster tick failed: " + e.Message);
                FlightDriver.ReleaseThrottle();
            }
        }

        static void Fly(Vessel v)
        {
            CelestialBody body = v.mainBody;
            double mu = (body != null) ? body.gravParameter : 0.0;
            double r = (body != null) ? (v.CoM - body.position).magnitude : 0.0;
            double g = (r > 1.0 && mu > 0.0) ? mu / (r * r) : 9.80665;

            Vector3d up = Steering.Up(v);
            Vector3d srfVel = v.srf_velocity;
            double speed = srfVel.magnitude;
            double descentSpeed = -Vector3d.Dot(srfVel, up);      // + = descending
            double alt = v.radarAltitude;
            double massKg = v.totalMass * 1000.0;
            lastDescentSpeed = descentSpeed;

            // braking-engine acceleration for the hoverslam solve (centre engine at full)
            double centreThrustN = ModeThrustN(v, VehicleParts.ModeCentreOnly);
            double threeThrustN = ModeThrustN(v, VehicleParts.ModeThreeEngine);
            double aCentre = massKg > 1.0 ? centreThrustN / massKg : 0.0;

            HoverslamInputs land;
            land.AltitudeM = alt;
            land.DescentSpeedMps = descentSpeed;
            land.ThrustAccelMps2 = aCentre > 0 ? aCentre : (threeThrustN / Math.Max(1.0, massKg));
            land.GravityMps2 = g;
            land.TerminalSpeedMps = descentSpeed > 1.0 ? descentSpeed : 100.0;   // measured proxy
            land.DeadTimeS = 2.5;                                                // ullage settle
            land.SpoolS = 0.0;                                                   // instant (Merlin)
            lastIgniteAlt = Hoverslam.IgnitionAltitude(land);

            // ---- measure the ballistic coefficient while COASTING (thrust masks drag when lit) ----
            if (currentMode <= 0 && body != null && body.atmosphere && alt < body.atmosphereDepth && speed > 50.0)
            {
                double pres = body.GetPressure(alt), temp = body.GetTemperature(alt);
                double rho = body.GetDensity(pres, temp);
                double dragAccel = v.geeForce * 9.80665;                 // felt decel ≈ drag when engine off
                double bcSample = Trajectory.BallisticCoefficientFrom(rho, speed, dragAccel);
                smoothedBc = Trajectory.SmoothBc(smoothedBc, bcSample, TimeWarp.fixedDeltaTime, Trajectory.BcFilterTauS);
            }

            // ---- targeting: steer the predicted impact onto the droneship / RTLS pad (L1 predictor) ----
            GridFinInputs fin = BoosterTargeting.Steer(v, smoothedBc);

            BoosterInputs bi = new BoosterInputs();
            bi.Valid = true;
            bi.SurfaceVelocity = new Vec3(srfVel.x, srfVel.y, srfVel.z);
            bi.Up = new Vec3(up.x, up.y, up.z);
            bi.AltitudeM = alt;
            bi.SpeedMps = speed;
            bi.DescentSpeedMps = descentSpeed;
            bi.AllNominal = BoosterTargeting.LastHadTarget;   // aim AT the deck once we have a target (else hold)
            bi.OffsetToMissM = 0.0;
            bi.Fin = fin;
            bi.Land = land;

            BoosterCommand bc = BoosterDescent.Guide(bi, phase);
            phase = bc.Phase;
            lastPhaseWord = phase.ToString();
            lastAoa = bc.AoaDeg;

            // ---- attitude: hold the commanded engines-first / retrograde(+AoA) axis ----
            Steering.Point(v, new Vector3d(bc.AimForward.X, bc.AimForward.Y, bc.AimForward.Z));

            // ---- engine mode + throttle (select absolutely while off; one ignition per mode) ----
            ApplyEngineMode(v, bc.EngineMode, bc.Throttle);

            // ---- aero surfaces + legs ----
            if (bc.DeployFins && !finsOut) { Actuator.DeployGridFins(v); finsOut = true; }
            if (bc.DeployLegs && !legsDown) { Actuator.DeployLegs(v); legsDown = true; }   // ⛔ direct: ModuleWheelDeployment (no Gear AG)

            FlightLog.Fill = FillRow;
        }

        // ⛔ Select the engine set by ACTIVATING the matching-engineID ModuleEngines and shutting the rest —
        // only on a MODE CHANGE (so a lit mode is never re-ignited mid-burn: one ignition per octaweb mode).
        static void ApplyEngineMode(Vessel v, int mode, double throttle)
        {
            if (mode <= 0)
            {
                if (currentMode != 0) { ShutdownAll(v); currentMode = 0; }
                FlightDriver.ReleaseThrottle();
                return;
            }
            if (mode != currentMode)
            {
                // entering a new mode: shut everything, then light exactly this mode's engines (once).
                foreach (ModuleEngines e in Engines(v))
                {
                    bool wants = VehicleParts.EngineIdIsMode(e.engineID, mode);
                    if (wants) { if (!e.EngineIgnited) TryActivate(e); }
                    else if (e.EngineIgnited) e.Shutdown();
                }
                Debug.Log("[DragonScreen] booster engine mode → " + ModeName(mode)
                          + " (activated by engineID, not NextEngineMode)");
                currentMode = mode;
            }
            FlightDriver.SetThrottle(throttle);
        }

        static void ShutdownAll(Vessel v)
        {
            foreach (ModuleEngines e in Engines(v)) if (e.EngineIgnited) e.Shutdown();
        }

        static void TryActivate(ModuleEngines e)
        {
            try { e.Activate(); } catch (Exception ex) { Debug.LogWarning("[DragonScreen] engine activate failed: " + ex.Message); }
        }

        static IEnumerable<ModuleEngines> Engines(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (!VehicleParts.IsBooster(p.name)) continue;
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e != null) yield return e;
                }
            }
        }

        // sum of a given mode's engines' max thrust (N)
        static double ModeThrustN(Vessel v, int mode)
        {
            double t = 0.0;
            foreach (ModuleEngines e in Engines(v))
                if (VehicleParts.EngineIdIsMode(e.engineID, mode)) t += e.maxThrust * 1000.0;
            return t;
        }

        // (grid-fin + leg deploy now live in Actuator.DeployGridFins / DeployLegs)

        static string ModeName(int m)
        {
            return m == VehicleParts.ModeCentreOnly ? "CenterOnly"
                 : m == VehicleParts.ModeThreeEngine ? "ThreeLanding" : "AllEngines";
        }

        static void FillRow(string[] row)
        {
            BoosterCommand dummy = new BoosterCommand { Phase = phase, AoaDeg = lastAoa, EngineMode = currentMode, Throttle = 0 };
            FlightRecorder.PutBooster(row, dummy, lastIgniteAlt, lastDescentSpeed);
        }
    }
}
