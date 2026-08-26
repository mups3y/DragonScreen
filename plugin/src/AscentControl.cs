// DragonScreen — AscentControl  (KSP glue: seam 2, the ascent phase controller)
// ============================================================================================
// Flies the launch → orbit phase with the PURE guidance (pure/Ascent.cs S1 pitch program + FSM,
// pure/Upfg.cs S2 closed-loop insertion, pure/LaunchAzimuth.cs plane targeting) against the live vessel.
// It reads the measured state, computes the commanded pitch/heading (S1) or thrust vector (S2), holds it
// with Steering (SAS inner loop), meters the throttle (Ascent's max-Q bucket + g-limit), stages at MECO /
// S2 ignition, cuts at SECO, separates the Dragon, then signals the conductor the phase is complete.
//
// ⛔ INSTRUMENTED: it feeds the FlightRecorder ascent columns + SelfCal (thrust from accel×mass) every
// tick, so the FIRST flight is judged from the CSV, not by eye (the standing rule). Defensive — a fault
// logs and the vehicle is left to the crew. S1 (pitch program) is the most reliable part; S2/UPFG target
// construction is a first cut to VALIDATE in flight (the Iy plane normal, the cutoff) — tune one change
// per flight against the recording.
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class AscentControl
    {
        // ⛔ the atmospheric angle-of-attack cap — never command the nose more than this off surface
        // prograde (zero-AoA gravity turn, load relief). Exceeding it RUDs the stack at max-Q.
        // AoA allowed at LOW q to establish the gravity turn (a real pitch-kick is ~8-10°); it ramps to 0
        // by QAoaZeroPa so max-Q is flown at 0 AoA. Larger = faster turn/shallower climb = reaches orbit.
        [Tunable] public static double MaxAoaDeg = 8.0;
        // if the MEASURED AoA exceeds this in powered atmospheric flight, control is lost → abort the crew out.
        [Tunable] public static double AbortAoaDeg = 25.0;
        // the AoA cap ramps from MaxAoaDeg (low q) to 0 at this dynamic pressure, so max-Q is flown at 0 AoA.
        [Tunable] public static double QAoaZeroPa = 15000.0;
        // S2 ullage: minimum post-MECO coast before S2 may light (lets S1 clear — the forward settle push
        // also opens the gap), and the settle backstop (ignite anyway if RealFuels never reports settled).
        [Tunable] public static double MinCoastS = 2.0;
        [Tunable] public static double MaxUllageSettleS = 6.0;

        static AscentPhase phase = AscentPhase.Idle;
        static UpfgState upfg;
        static bool s2Ignited;
        static double coastStartUT = -1;
        static bool dragonSeparated;
        static SelfCalState cal;

        // last commanded values, for the recorder
        static double Throttle;
        static double lastPitchCmd = 90, lastAzDeg, lastTgo, lastVgo, lastAoaDeg;
        static double lastUllage = 1.0;
        static bool lastRcsOn;
        static string lastPhaseWord = "IDLE";

        public static void Reset()
        {
            phase = AscentPhase.Idle; upfg = new UpfgState(); s2Ignited = false;
            coastStartUT = -1; dragonSeparated = false; Throttle = 0;
            Steering.Release();
        }

        public static void Tick(Vessel v, MissionProfile mission)
        {
            if (v == null) { FlightDriver.ReleaseThrottle(); return; }
            try { Fly(v, mission); }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] ascent tick failed: " + e.Message);
                FlightDriver.ReleaseThrottle();
            }
        }

        static void Fly(Vessel v, MissionProfile mission)
        {
            CelestialBody body = v.mainBody;
            double mu = (body != null) ? body.gravParameter : 0.0;
            double R = (body != null) ? body.Radius : 0.0;

            // target orbit (ISS default ~200 km circular; a free-flyer carries its own apoapsis)
            double targetAltM = (mission.ApoKm > 0 ? mission.ApoKm : 200.0) * 1000.0;
            double targetRadiusM = R + targetAltM;

            // ---- measured vehicle numbers ----
            bool s2Lit = AnyStageEngineLit(v, false);
            double activeThrustN, ve;
            ActivePropulsion(v, out activeThrustN, out ve);
            double massKg = v.totalMass * 1000.0;
            double axialAccel = AxialAccel(v);

            AscentInputs ai = new AscentInputs();
            ai.Valid = true;
            ai.AltitudeM = v.altitude;
            ai.SurfaceSpeedMps = v.srfSpeed;
            ai.ApoapsisM = (v.orbit != null) ? v.orbit.ApA : 0.0;
            ai.TargetApoapsisM = targetAltM;
            ai.DynamicPressurePa = v.dynamicPressurekPa * 1000.0;
            ai.MassKg = massKg;
            ai.FullThrustN = activeThrustN > 1.0 ? activeThrustN : 1.0;
            ai.GLimitG = s2Lit ? 4.0 : 3.5;                 // crew axial-g caps (LAUNCH_AND_ASCENT §5.2)
            ai.SecondStage = s2Lit;

            AscentCommand ac = Ascent.Guide(ai, phase);
            phase = ac.Phase;
            lastPhaseWord = phase.ToString();

            // ---- staging ----
            if (ac.Stage) Actuator.Meco(v);   // ⛔ direct: octaweb cutoff + interstage decoupler (no staging)

            if (phase == AscentPhase.Coast)
            {
                // ⛔ ULLAGE SETTLE before S2 ignition (plan §3.3): after MECO the propellant floats off the
                // MVac intake in free-fall; fire the aft RCS (forward push, s.Z=-1) until RealFuels reports it
                // settled, THEN light — there is no retry. A minimum coast lets the spent S1 clear first.
                if (coastStartUT < 0) coastStartUT = Planetarium.GetUniversalTime();
                if (!s2Lit && !s2Ignited)
                {
                    double settledS = Planetarium.GetUniversalTime() - coastStartUT;
                    lastUllage = Ullage.Stability(Actuator.FindEngine(v, EngineRole.SecondStage));
                    if (IgnitionGate.UllageReady(lastUllage, settledS, MinCoastS, MaxUllageSettleS))
                    {
                        Actuator.IgniteSecondStage(v); s2Ignited = true;
                        FlightDriver.ReleaseTranslation();     // stop settling; RCS master stays on (S2 roll control)
                    }
                    else
                    {
                        Actuator.EnableRcs(v);                 // settle: seat the propellant on the intake
                        FlightDriver.SetTranslation(0, 0, -1); // s.Z=-1 = forward push (MechJeb ProcessUllage)
                    }
                }
            }

            // ---- steering ----
            Vector3d aim;
            if (!s2Lit)
            {
                // S1: pitch program on the plane azimuth
                double azRad;
                double lat = v.latitude * Math.PI / 180.0;
                double incRad = mission.IncDeg * Math.PI / 180.0;
                double bodyRot = (body != null && body.rotationPeriod > 0) ? 2.0 * Math.PI / body.rotationPeriod : 0.0;
                bool descending = mission.IncDeg > 90.0;    // polar-south (Fram2) is the descending pass
                double vorb = (mu > 0 && targetRadiusM > 0) ? Math.Sqrt(mu / targetRadiusM) : 7800.0;
                if (!LaunchAzimuth.GroundRad(incRad, lat, vorb, R, bodyRot, descending, out azRad))
                    azRad = descending ? Math.PI : Math.PI / 2.0;   // unreachable inc → due S / due E fallback
                lastAzDeg = azRad * 180.0 / Math.PI;
                lastPitchCmd = ac.PitchDeg;

                // ⛔ THE REAL TECHNIQUE (LAUNCH_AND_ASCENT_RESEARCH §4.3/§6.1): a ZERO-AoA gravity turn —
                // the nose held near the velocity vector so aero side loads stay ~0 (load relief), which is
                // what lets the unstable airframe survive the dense atmosphere. But it must ALSO pitch over
                // enough to reach orbit, so we track the DM-1 pitch program with an AoA that is allowed at
                // LOW q and SHRINKS TO 0 through max-Q: turn is established early, then flown at 0 AoA through
                // the danger band, then tracking resumes up high. Holding a fixed AoA at max-Q is what
                // diverged and RUD'd the stack (flights 1-3).
                Vector3d up = Steering.Up(v);
                double qPa = v.dynamicPressurekPa * 1000.0;
                if (v.srfSpeed < Ascent.KickSpeedMps)
                {
                    aim = up;                                            // vertical rise, clear the tower
                }
                else
                {
                    double qFrac = qPa / QAoaZeroPa; if (qFrac > 1.0) qFrac = 1.0; if (qFrac < 0.0) qFrac = 0.0;
                    double aoaCap = MaxAoaDeg * (1.0 - qFrac);           // → 0 through max-Q, MaxAoaDeg at low q
                    Vector3d target = Steering.PitchHeadingDir(v, ac.PitchDeg, azRad);
                    aim = Steering.LimitToProgradeCone(v, target, aoaCap);
                }
            }
            else
            {
                // S2: closed-loop UPFG thrust vector (world/inertial frame)
                aim = UpfgAim(v, mu, targetRadiusM, activeThrustN, ve, massKg);
            }
            Steering.Point(v, aim);
            lastAoaDeg = Steering.AngleOfAttackDeg(v);
            lastRcsOn = Actuator.IsRcsOn(v);

            // ⛔ LOSS-OF-CONTROL ABORT: if the vehicle departs (AoA runs away) in the powered atmospheric
            // ascent, PUNCH OUT before the stack RUDs — the crew survives on the SuperDracos + chutes. The
            // guidance commands ≤MaxAoaDeg; an AoA this far past it means control is lost, not commanded.
            if ((phase == AscentPhase.VerticalRise || phase == AscentPhase.GravityTurn)
                && v.dynamicPressurekPa > 1.0 && lastAoaDeg > AbortAoaDeg)
            {
                Debug.LogWarning("[DragonScreen] loss of control — AoA " + lastAoaDeg.ToString("F0")
                                 + "° > " + AbortAoaDeg.ToString("F0") + "° — ABORT");
                FlightDriver.RequestAbort();
            }

            // ---- throttle ----
            if (phase == AscentPhase.Done || phase == AscentPhase.Seco)
                Throttle = 0.0;
            else if (phase == AscentPhase.Coast)
                Throttle = s2Ignited ? 1.0 : 0.0;   // full the instant S2 is commanded, else coasting = off
            else Throttle = ac.Throttle;            // powered (VerticalRise / GravityTurn / S2Burn)

            // ---- SECO + Dragon separation ----
            bool inOrbit = (v.orbit != null) && (v.orbit.PeA >= targetAltM - 5000.0);
            if (s2Lit && (inOrbit || (upfg.Init && lastTgo > 0 && lastTgo < 0.15)))
            {
                Throttle = 0.0;
                if (!dragonSeparated) { Actuator.SeparateDragon(v); dragonSeparated = true; }
            }

            FlightDriver.SetThrottle(Throttle);

            if (dragonSeparated)
            {
                FlightDriver.ReleaseThrottle();
                CrewProcedureOps.PhaseComplete();   // hand back to the conductor (→ Phasing/return)
            }

            // ---- self-cal + recorder ----
            if (activeThrustN > 1.0 && massKg > 1.0) SelfCal.Thrust(ref cal, axialAccel, massKg);
            FlightLog.Fill = FillRow;
        }

        // ---- UPFG: build the target + step; return the world thrust direction ----
        static Vector3d UpfgAim(Vessel v, double mu, double targetRadiusM, double thrustN, double ve, double massKg)
        {
            if (mu <= 0 || v.orbit == null || thrustN <= 1.0)
                return Steering.Prograde(v);

            Vec3 r = W(v.CoM - v.mainBody.position);
            Vec3 vel = W(v.obt_velocity);

            UpfgTarget t;
            // plane normal OPPOSITE the angular momentum (in-plane launch assumption; note is the sign trap)
            Vec3 h = Vec3.Cross(r, vel);
            t.Iy = h.Magnitude > 1e-3 ? (h * -1.0).Normalized : new Vec3(0, 1, 0);
            t.RadiusM = targetRadiusM;
            t.SpeedMps = Math.Sqrt(mu / targetRadiusM);      // circular insertion
            t.GammaRad = 0.0;

            UpfgVehicle veh;
            veh.ExhaustVel = ve > 100 ? ve : 3383.0;
            veh.ThrustN = thrustN;
            veh.MassKg = massKg;

            if (!upfg.Init) upfg = Upfg.Init(r, vel, mu, t, veh);
            UpfgGuidance g = Upfg.Step(r, vel, mu, t, veh, ref upfg);
            lastTgo = g.TgoS; lastVgo = upfg.Vgo.Magnitude;
            if (g.Valid) return new Vector3d(g.IF.X, g.IF.Y, g.IF.Z).normalized;
            return Steering.Prograde(v);                     // fallback: prograde raise
        }

        // ---- actuation helpers ---- (staging + separation now live in Actuator: Meco / SeparateDragon)

        static bool AnyStageEngineLit(Vessel v, bool booster)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                bool match = booster ? VehicleParts.IsBooster(p.name) : VehicleParts.IsSecondStage(p.name);
                if (!match) continue;
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e != null && e.EngineIgnited && e.finalThrust > 0.1f) return true;
                }
            }
            return false;
        }

        // sum of currently-lit engines' thrust (N) + a representative exhaust velocity (m/s)
        static void ActivePropulsion(Vessel v, out double thrustN, out double ve)
        {
            thrustN = 0.0; double ispSum = 0.0, wSum = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null || !e.EngineIgnited || !e.isOperational) continue;
                    double thr = e.finalThrust > 0.1f ? e.finalThrust * 1000.0 : e.maxThrust * 1000.0;
                    thrustN += thr;
                    double isp = e.realIsp > 1f ? e.realIsp : 340.0;
                    ispSum += isp * thr; wSum += thr;
                }
            }
            ve = (wSum > 0) ? (ispSum / wSum) * Upfg.G0 : 3383.0;
        }

        // Felt (accelerometer) axial acceleration — INDEPENDENT of the thrust model, so SelfCal.Thrust
        // (F = a·m) is a genuine cross-check, not a tautology. geeForce excludes gravity (freefall = 0),
        // so under power it is ≈ thrust/mass along the axis.
        static double AxialAccel(Vessel v) { return v.geeForce * 9.80665; }

        static Vec3 W(Vector3d v) { return new Vec3(v.x, v.y, v.z); }

        // ---- recorder contribution (invoked by FlightLog while a row is built) ----
        static void FillRow(string[] row)
        {
            FlightRecorder.PutAscent(row, lastTgo, lastVgo, lastPitchCmd, lastAzDeg, lastPhaseWord);
            // att_err_deg column now carries the REAL angle of attack (nose vs surface velocity) — the
            // Q·α that RUDs the vehicle. This was hardcoded 0 before, which is why the first flights were
            // flown blind to their own cause of death.
            FlightRecorder.PutControl(row, lastAoaDeg, 0, Throttle, double.NaN, lastRcsOn);
            FlightRecorder.PutAttitude(row, AttitudePilot.PointErrDeg, AttitudePilot.RateCmdRads,
                AttitudePilot.RateMeasRads, AttitudePilot.ActPitch, AttitudePilot.ActYaw, AttitudePilot.ActRoll,
                AttitudePilot.CtrlTorquePitchNm, AttitudePilot.CtrlTorqueYawNm);
            FlightRecorder.PutIgnition(row, lastUllage, FlightDriver.ClampThrustFrac, FlightDriver.ClampHeld);
            FlightRecorder.PutSelfCal(row, cal);
        }
    }
}
