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
        // ⭐ LET IT FALL (Chris 2026-08-30): "just drop the booster with enough fuel to land but let it fall to
        // earth. Get upper stage to orbit." Flight 194334 proved WHY this matters: the recovery FSM lit the
        // engines at FULL THRUST 0.3 s after MECO while still at 0 km from the stack, its attitude diverged
        // 2°→85° and the booster was DESTROYED in ~10 s — and its 0-km burn kicked the upper stage (yaw +14.7 dps)
        // right as S2 lit. LetFall = separate cleanly, hold a stable attitude, and NEVER fire the engines: all
        // landing fuel is preserved and the stack is not disturbed. Turn OFF only to actually attempt a recovery.
        [Tunable] public static bool LetFall = true;

        static BoosterPhase phase = BoosterPhase.Idle;
        static int currentMode = -1;         // -1 = none selected; VehicleParts consts 0=All, 1=ThreeEngine, 2=CentreOnly
        static bool legsDown, finsOut;
        static double smoothedBc;            // measured ballistic coefficient (for the impact predictor)

        static double lastAoa, lastIgniteAlt, lastDescentSpeed;
        static string lastPhaseWord = "IDLE";
        static double boosterSepUT = -1.0;     // ⭐ UT of the first recovery tick ≈ separation time
        static bool predictedImpactLogged;     // ⭐ one-shot: log the predicted impact at sep + ImpactLogDelayS
        [Tunable] public static double ImpactLogDelayS = 5.0;   // owner: record the predicted impact this long after separation

        // ⭐ C2 Step-2: the booster's OWN attitude loop, INDEPENDENT of the Dragon's (AttitudePilot's active
        // instance). When the Dragon stays active and the booster is flown on its own OnFlyByWire, the two loops
        // must not share PID/smoothing state — this instance is the booster's. Written into the booster's own
        // FlightCtrlState (never FlightDriver's active-vessel channels) by DriveNonActive.
        static readonly AttitudeController att = new AttitudeController();
        static double lastDriveLogUT = -999.0;   // rate-limit the non-active recovery KSP.log line
        static double lastThrottle;              // ⭐ R1: throttle written to the booster's ctrlState (for the recorder)

        public static void Reset()
        {
            phase = BoosterPhase.Idle; currentMode = -1; legsDown = false; finsOut = false;
            smoothedBc = 0.0;
            boosterSepUT = -1.0; predictedImpactLogged = false;
            att.Reset(); lastDriveLogUT = -999.0; lastThrottle = 0.0;
            BoosterLog.Close();                  // ⭐ R1: close any open booster-recovery stream
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

        // ACTIVE-vessel recovery (the booster IS the focused craft): actuation routes through the active-vessel
        // sinks (Steering.Point → AttitudePilot; FlightDriver.SetThrottle). Kept for the case where focus is on
        // the booster; the committed C2 path is DriveNonActive below.
        public static void Tick(Vessel v)
        {
            try { Fly(v, null); }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] booster tick failed: " + e.Message);
                FlightDriver.ReleaseThrottle();
            }
        }

        // ⭐ C2 Step-2 — NON-ACTIVE recovery: fly the SAME FSM on the separated booster while the Dragon stays
        // active, writing every command into the booster's OWN FlightCtrlState `s` (the one KSP hands its
        // OnFlyByWire). Attitude comes from the booster's own AttitudeController instance (no state collision with
        // the Dragon's loop); throttle is the literal s.mainThrottle write (the C1 "throttle never raised" fix);
        // engine-mode/fins/legs act directly on the booster's part modules (non-active-safe). Runs inside KSP's
        // callback → its own guard; a fault logs and leaves the axes untouched this tick.
        public static void DriveNonActive(Vessel v, FlightCtrlState s)
        {
            try { Fly(v, s); }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] booster non-active drive failed: " + e.Message); }
        }

        // s == null → ACTIVE path (active-vessel sinks). s != null → NON-ACTIVE path (write into the booster's
        // own FlightCtrlState). Everything above the actuation is identical.
        static void Fly(Vessel v, FlightCtrlState s)
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

            // ⭐ PREDICTED-IMPACT RECORD (owner 2026-09-01): ImpactLogDelayS (5 s) after separation, log the L1
            //    impact predictor's touchdown point — the precise predicted impact for this drop.
            double nowUT = Planetarium.GetUniversalTime();
            if (boosterSepUT < 0.0) boosterSepUT = nowUT;
            if (!predictedImpactLogged && nowUT - boosterSepUT >= ImpactLogDelayS)
            {
                predictedImpactLogged = true;
                Vector3d impactW;
                if (body != null && BoosterTargeting.PredictImpact(v, smoothedBc, 0.0, out impactW))
                    Debug.LogWarning("[DragonScreen] ⭐ BOOSTER predicted impact @ sep+" + ImpactLogDelayS.ToString("F0")
                        + "s: lat " + body.GetLatitude(impactW).ToString("F5") + "  lon " + body.GetLongitude(impactW).ToString("F5")
                        + "  (dist " + ((impactW - v.CoM).magnitude / 1000.0).ToString("F1") + " km, alt "
                        + v.radarAltitude.ToString("F0") + " m, spd " + speed.ToString("F0") + " m/s, bc " + smoothedBc.ToString("F0") + ")");
                else
                    Debug.LogWarning("[DragonScreen] ⭐ BOOSTER predicted impact @ sep+" + ImpactLogDelayS.ToString("F0")
                        + "s: no usable ballistic prediction yet (alt " + v.radarAltitude.ToString("F0")
                        + " m, spd " + speed.ToString("F0") + " m/s — still ascending/no drag data)");
            }

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
            bool active = (s == null);

            // ⭐ LET IT FALL: force engines OFF (mode 0, throttle 0) so the booster never burns — it keeps all its
            //   landing fuel and just falls, and its exhaust can't disturb the stack at 0 km. Attitude hold stays
            //   (a stable ballistic fall). This is the committed behaviour per Chris; clear LetFall to recover.
            if (LetFall) { bc.EngineMode = 0; bc.Throttle = 0.0; }

            Vector3d aimDir = new Vector3d(bc.AimForward.X, bc.AimForward.Y, bc.AimForward.Z);
            // ⭐ PROGRADE HOLD (owner 2026-09-01): the booster holds PROGRADE (nose along the surface velocity) — a
            //    stable, level coast — through the fall, rather than the descent FSM's retrograde flip. (LetFall.)
            if (LetFall && speed > 1.0) aimDir = srfVel.normalized;

            // ---- engine mode select (absolutely, while off; one ignition per mode) — acts on the booster's
            //      part modules directly, so it is correct whether or not the booster is the active vessel ----
            SelectEngineMode(v, bc.EngineMode);
            double thr = bc.EngineMode > 0 ? bc.Throttle : 0.0;
            lastThrottle = thr;   // ⭐ R1: for the booster recorder stream

            // ---- attitude + throttle: to the ACTIVE-vessel sinks, or into the booster's own FlightCtrlState ----
            if (active)
            {
                Steering.Point(v, aimDir);                              // → AttitudePilot (active instance)
                if (bc.EngineMode > 0) FlightDriver.SetThrottle(thr); else FlightDriver.ReleaseThrottle();
            }
            else
            {
                AttitudeCmd c = att.Compute(v, aimDir, true, Vector3d.zero);   // booster's OWN loop, dampRoll
                s.pitch = Clamp1f(c.Pitch); s.yaw = Clamp1f(c.Yaw);
                if (c.HasRoll) s.roll = Clamp1f(c.Roll);
                s.mainThrottle = Clamp01f(thr);                        // ⭐ the literal C1 throttle-raise fix
            }

            // ---- aero surfaces + legs (direct part-module actuation — non-active-safe) ----
            if (bc.DeployFins && !finsOut) { Actuator.DeployGridFins(v); finsOut = true; }
            if (bc.DeployLegs && !legsDown) { Actuator.DeployLegs(v); legsDown = true; }   // ⛔ direct: ModuleWheelDeployment (no Gear AG)

            // ---- instrument. Active: contribute the booster columns to ITS CSV row. Non-active: the Dragon owns
            //      the CSV, so log the booster's recovery state to KSP.log instead (the cross-check evidence). ----
            if (active) FlightLog.Fill = FillRow;
            else { LogDrive(v, thr); BoosterLog.Sample(v); }   // ⭐ R1: the booster's own CSV stream (non-active)
        }

        // ⛔ NaN GUARD (Phase 2, rule N2): NaN comparisons are all false, so a NaN would pass straight through
        // into the non-active booster's FlightCtrlState. Sanitize NaN → 0 so a bad command can't reach the
        // booster's actuators as NaN (finite values clamp exactly as before).
        static float Clamp1f(double d)  { if (double.IsNaN(d)) return 0f; return (float)(d < -1.0 ? -1.0 : (d > 1.0 ? 1.0 : d)); }
        static float Clamp01f(double d) { if (double.IsNaN(d)) return 0f; return (float)(d < 0.0 ? 0.0 : (d > 1.0 ? 1.0 : d)); }

        // Rate-limited KSP.log line of the non-active booster's recovery state — phase / altitude / vertical
        // speed / engine mode / throttle / engines lit — the log-side cross-check for the flight (the Dragon's
        // CSV can't record it). ~every 2 s while recovering.
        static void LogDrive(Vessel v, double thr)
        {
            double now = Planetarium.GetUniversalTime();
            if (now - lastDriveLogUT < 2.0) return;
            lastDriveLogUT = now;
            int lit = 0; foreach (ModuleEngines e in Engines(v)) if (e.EngineIgnited) lit++;
            Debug.Log("[DragonScreen] booster recovery drive: phase=" + lastPhaseWord
                      + " alt=" + v.radarAltitude.ToString("F0") + "m vspd=" + lastDescentSpeed.ToString("F0")
                      + "m/s mode=" + ModeName(currentMode) + " thr=" + thr.ToString("F2")
                      + " engLit=" + lit + " att.err=" + att.PointErrDeg.ToString("F0") + "°");
        }

        // ⛔ Select the engine set by ACTIVATING the matching-engineID ModuleEngines and shutting the rest —
        // only on a MODE CHANGE (so a lit mode is never re-ignited mid-burn: one ignition per octaweb mode).
        // Throttle is NOT applied here (split out in C2 Step-2): the caller writes it to the correct sink —
        // FlightDriver.SetThrottle for the active booster, or s.mainThrottle for the non-active one.
        static void SelectEngineMode(Vessel v, int mode)
        {
            if (mode <= 0)
            {
                if (currentMode != 0) { ShutdownAll(v); currentMode = 0; }
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

        // ⭐ R1: fill a full FlightRecorder row from the NON-ACTIVE booster's live state (called by BoosterLog).
        // Mirrors FlightLog's base snapshot but for THIS vessel + the booster's OWN attitude loop — so the
        // booster recovery is a proper recording, not sparse log text. The H1b-critical columns: `eng_ignited`
        // (did the octaweb LIGHT?), `ullage_stab` (was there settled ullage at the light?), throttle, plus the
        // full control loop + reentry heating.
        public static void FillRecorderRow(string[] row, Vessel v)
        {
            if (v == null) return;
            try
            {
                FlightRecorder.PutTime(row, v.missionTime);
                FlightRecorder.PutNav(row, v.altitude, v.obt_speed, v.verticalSpeed,
                    v.dynamicPressurekPa * 1000.0, v.mach, double.NaN, v.totalMass * 1000.0);
                FlightRecorder.PutBase(row, MissionPhase.Unknown, new ModeStep(), v.srfSpeed,
                    Steering.AngleOfAttackDeg(v), v.geeForce, Actuator.TotalActiveThrustN(v),
                    Actuator.IsRcsOn(v), false, AbortMode.None);   // booster: no mission ModeManager mode; engine_mode carries the octaweb mode
                // the booster's OWN attitude loop (its AttitudeController instance) — the whole point of R1.
                FlightRecorder.PutCommand(row, lastThrottle, 0.0, 0.0, 0.0,
                    att.PointErrDeg, att.RateCmdRads, att.RateMeasRads,
                    att.ActPitch, att.ActYaw, att.ActRoll, att.CtrlTorquePitchNm, att.CtrlTorqueYawNm);
                Vector3 av = v.angularVelocity * Mathf.Rad2Deg;
                FlightRecorder.PutRates(row, av.x, av.y, av.z);
                Vector3 moi = v.MOI; Orbit o = v.orbit;
                FlightRecorder.PutAuthority(row, att.CtrlTorqueRollNm, moi.x, moi.y, moi.z, Actuator.RcsThrustN(v),
                    o != null ? o.ApA / 1000.0 : double.NaN, o != null ? o.PeA / 1000.0 : double.NaN,
                    o != null ? o.inclination : double.NaN, o != null ? o.LAN : double.NaN);
                // warp + the booster's main-engine ignition state (H1b: PROVES whether the octaweb lit).
                double warpRate = 1.0; try { warpRate = TimeWarp.CurrentRate; } catch { }
                int ign = 0, flame = 0;
                foreach (ModuleEngines e in Engines(v)) { if (e.EngineIgnited) ign++; if (e.flameout) flame++; }
                FlightRecorder.PutInstrument(row, warpRate, ign, flame);
                // the booster octaweb's ullage (H1b: was there settled ullage at the light attempt?).
                double ull = double.NaN;
                foreach (ModuleEngines e in Engines(v)) { ull = Ullage.Stability(e); break; }
                FlightRecorder.PutIgnition(row, ull, double.NaN, false);
                // the booster FSM columns + reentry heating (MMH/NTO N/A — the booster burns RP-1/LOX).
                BoosterCommand snap = new BoosterCommand { Phase = phase, AoaDeg = lastAoa, EngineMode = currentMode, Throttle = lastThrottle };
                FlightRecorder.PutBooster(row, snap, lastIgniteAlt, lastDescentSpeed);
                FlightRecorder.PutEnvironment(row, double.NaN, double.NaN, BoosterSkinFrac(v));
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] booster row fill failed: " + e.Message); }
        }

        // The hottest part's skin-temperature fraction (0..1) — the booster's reentry heating; records only,
        // no logging (the Dragon's FlightLog owns the rate-limited thermal log line).
        static double BoosterSkinFrac(Vessel v)
        {
            if (v == null || v.parts == null) return double.NaN;
            double worst = 0.0;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    double mx = p.skinMaxTemp;
                    if (mx > 0.0) { double f = p.skinTemperature / mx; if (f > worst) worst = f; }
                }
            }
            catch { return double.NaN; }
            return worst > 0.0 ? worst : double.NaN;
        }
    }
}
