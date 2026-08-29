// DragonScreen — AbortControl  (KSP glue: the SELF-AWARE abort executor — runs the REAL procedure per regime)
// ============================================================================================
// The crew hits ABORT (or FDIR commands it); this decides WHERE in the mission the vehicle is from its LIVE
// physical state (pure/AbortResponder), then flies the matching real Crew Dragon procedure end-to-end. It
// reuses the existing direct-control actuation (Actuator) and the pure guidance (DeorbitGuidance / Chutes),
// so an abort is never a bespoke one-off — it is the mission's own building blocks, sequenced correctly.
//
// Regimes (docs/ABORT_PROCEDURES_RESEARCH.md):
//   LaunchEscape  : SuperDraco escape → drop the stack → TRUNK JETTISON → open nose → hold SHIELD-FORWARD
//                   on the Dracos (no more tumble) → compressed chutes → splash.
//   AbortToOrbit  : near orbital energy → point prograde, thrust to a safe periapsis, do NOT splash.
//   DeorbitReturn : (crew hits ABORT in orbit) trunk jettison → retrograde deorbit burn timed so the landing
//                   falls on the NEAREST OPEN WATER (never a mountainside) → shield-forward entry → chutes.
//   EmergencyUndock: docked → release the hooks, back off, then DeorbitReturn home.
//   KosRetreat    : prox-ops → back out of the corridor to a safe standoff.
//   RideItDown    : already entering → hold shield-forward + chutes.
//   SafeHold      : nothing to escape with (pad, LES not armed) → safe the vehicle.
//
// The mode is LATCHED at the first tick of the abort (the situation at the moment of the abort decides the
// procedure; it does not flip mid-descent). Instrumented into the FlightRecorder (abort_mode column).
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class AbortControl
    {
        // ---- tunables (first-cut; calibrate against the abort recording) ----
        [Tunable] public static double EscapeBurnS = 6.0;         // SuperDraco push (real ~6 s to depletion)
        [Tunable] public static double DeorbitTargetPeM = 50000.0;// entry-corridor periapsis (matches return)
        [Tunable] public static double SettleS = 3.0;             // dwell after trunk sep before the deorbit burn
        [Tunable] public static double AttitudeReadyDeg = 10.0;   // "pointed" gate for a burn
        [Tunable] public static double ForwardSign = -1.0;        // Draco fore/aft translation sign (= return)
        [Tunable] public static double SafeOrbitPeM = 140000.0;   // abort-to-orbit target periapsis
        [Tunable] public static double MinGlideM = 1.0e6;         // deorbit→splash downrange window (reachable now)
        [Tunable] public static double MaxGlideM = 12.0e6;
        [Tunable] public static double GroundStepS = 45.0;        // ground-track sample step
        [Tunable] public static int GroundSamples = 130;          // ~ one 90-min orbit of look-ahead
        // ⛔ 3000→120 (flight 173320): the on-orbit abort NEVER deorbited — SafeSiteReachable never found a
        // reachable water site, and the 3000 s (50 min!) backstop was never reached, so the crew coasted stranded.
        // An abort MUST bring them home. 120 s still lets the site search time the burn for an ocean when it works,
        // but GUARANTEES a controlled deorbit within two minutes even if water-finding fails. Never strand.
        [Tunable] public static double SiteSearchTimeoutS = 120.0;

        static AbortMode mode = AbortMode.None;
        static string note = "";
        static bool latched;
        static double onsetUT;

        // sub-phase flags/state
        static bool escapeFired, trunkGone, shroudOpen, undocked, atoDone, atoSeparated;
        static bool deorbitCommitted, deorbitDone;
        static ChutePhase chutePhase = ChutePhase.Idle;
        static bool aDroguesArmed, aMainsArmed;   // arm each canopy once (idempotent latch)
        static double abortDrogueTime;
        // the shared Draco deorbit burn (DeorbitBurn) + its burn-local state — same engine/path as the
        // nominal return; the old SuperDraco-throttle deorbit fired an EMPTY engine (it is abort-reserved).
        static DeorbitBurnState deo = new DeorbitBurnState();
        static bool haveSite; static double siteLatDeg, siteLonDeg;
        static bool siteLogged;   // one-shot diagnostic for the deorbit water scan
        // ⭐ DEORBIT RESCUE (the "DEORBIT NOW" / "WATER DEORBIT" panel buttons — ForceDeorbit): reuse the
        // DeorbitReturn engine but under crew command. deorbitLandOK = true → "DEORBIT NOW" (land ANYWHERE
        // safe, commit immediately = fastest; deploy the gear AFTER a LAND touchdown); false → "WATER
        // DEORBIT" (wait for the nearest reachable OPEN WATER, splashdown, no gear). Rescue = not an emergency
        // abort, so the klaxon/DON'T PANIC FX are suppressed by the caller.
        static bool isRescue;        // this DeorbitReturn was crew-commanded (a rescue), not a fault abort
        static bool deorbitLandOK;   // rescue mode: land on land OK (DEORBIT NOW) vs water-only (WATER DEORBIT)
        static bool gearDeployed;    // one-shot: legs extended after a land touchdown

        public static AbortMode Mode { get { return mode; } }
        public static string Note { get { return note; } }

        public static void Reset()
        {
            mode = AbortMode.None; note = ""; latched = false; onsetUT = 0;
            escapeFired = trunkGone = shroudOpen = undocked = atoDone = atoSeparated = false;
            deorbitCommitted = deorbitDone = false;
            chutePhase = ChutePhase.Idle; deo.Reset();
            aDroguesArmed = aMainsArmed = false;
            abortDrogueTime = 0;
            haveSite = false; siteLatDeg = siteLonDeg = 0; siteLogged = false;
            isRescue = false; deorbitLandOK = false; gearDeployed = false;
        }

        // ⭐ Crew-commanded controlled deorbit (the panel rescue buttons). Latches DeorbitReturn directly —
        // bypassing the fault-mode Decide() — with the land-vs-water flag. landAnywhere = "DEORBIT NOW" (fastest,
        // any safe site, gear after a land touchdown); false = "WATER DEORBIT" (nearest open water, splashdown).
        public static void ForceDeorbit(bool landAnywhere)
        {
            Reset();
            mode = AbortMode.DeorbitReturn;
            note = landAnywhere ? "DEORBIT NOW" : "WATER DEORBIT";
            latched = true; onsetUT = Now();
            isRescue = true; deorbitLandOK = landAnywhere;
            Debug.Log("[DragonScreen] ⭐ DEORBIT RESCUE armed — " + note
                      + (landAnywhere ? " (land anywhere safe, gear after land touchdown)" : " (nearest open water, splashdown)"));
        }

        public static void Tick(Vessel v)
        {
            if (v == null) return;
            try { Fly(v); }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] abort tick failed: " + e.Message); }
        }

        static void Fly(Vessel v)
        {
            if (!latched)
            {
                mode = Decide(v); note = mode.ToString();
                latched = true; onsetUT = Now();
                Debug.Log("[DragonScreen] ⛔ ABORT MODE = " + mode + "  (alt " + (v.altitude / 1000.0).ToString("F0")
                          + " km, Pe " + (PeA(v) / 1000.0).ToString("F0") + " km, v " + v.srfSpeed.ToString("F0") + " m/s)");
            }

            switch (mode)
            {
                case AbortMode.LaunchEscape:    FlyLaunchEscape(v); break;
                case AbortMode.AbortToOrbit:    FlyAbortToOrbit(v); break;
                case AbortMode.DeorbitReturn:
                case AbortMode.EmergencyUndock: FlyDeorbitReturn(v); break;
                case AbortMode.KosRetreat:      FlyRetreat(v); break;
                case AbortMode.RideItDown:      FlyRideItDown(v); break;
                default:                        FlySafeHold(v); break;
            }
            FlightLog.Fill = FillRow;
        }

        // ---------------------------------------------------------------- the self-aware mode decision
        static AbortMode Decide(Vessel v)
        {
            CelestialBody body = v.mainBody;
            double mu = body != null ? body.gravParameter : 0.0;
            double r = body != null ? (v.CoM - body.position).magnitude : 0.0;

            AbortInputs ai = new AbortInputs();
            ai.Triggered = true;
            ai.Phase = CrewProcedureOps.ActivePhase;
            ai.LesArmed = FlightCommands.EscapeArmed;
            ai.AltitudeM = v.altitude;
            ai.AtmTopM = body != null ? body.atmosphereDepth : 140000.0;
            ai.SurfaceSpeedMps = v.srfSpeed;
            ai.OrbitalSpeedMps = (mu > 0 && r > 0) ? Math.Sqrt(mu / r) : 0.0;
            ai.PeriapsisAltM = v.orbit != null ? v.orbit.PeA : 0.0;
            ai.ApoapsisAltM = v.orbit != null ? v.orbit.ApA : 0.0;
            ai.Docked = IsDocked(v);
            ai.NearStation = NearStation(v);

            return AbortResponder.Respond(ai).Mode;
        }

        // ---------------------------------------------------------------- LAUNCH ESCAPE (pad / ascent)
        static void FlyLaunchEscape(Vessel v)
        {
            double t = Now() - onsetUT;

            // 1) the escape burn: SuperDracos to full, drop the stack (once).
            if (!escapeFired)
            {
                Actuator.FireAbort(v);   // SuperDraco motor(s) at full + separate from the stack (drop S2)
                escapeFired = true;
            }

            if (t < EscapeBurnS)
            {
                // push away from the stack; hold the nose along the escape direction (prograde) so it flies true.
                FlightDriver.SetThrottle(1.0);
                Steering.Point(v, Steering.Prograde(v));
                return;
            }

            // 2) burn done → coast + prepare for entry.
            FlightDriver.ReleaseThrottle();
            if (!trunkGone) { Actuator.JettisonTrunk(v); trunkGone = true; }   // clear the heat shield
            if (!shroudOpen) { Actuator.OpenNoseShroud(v); shroudOpen = true; }// expose the Dracos
            Actuator.EnableRcs(v);

            // 3) hold SHIELD-FORWARD (nose retrograde to the surface flow) — this is what stops the tumble.
            HoldShieldForward(v);

            // 4) compressed abort chutes on the measured descent (drogues → cut → mains).
            DriveAbortChutes(v);
        }

        // ---------------------------------------------------------------- ABORT-TO-ORBIT (very late ascent)
        static void FlyAbortToOrbit(Vessel v)
        {
            // Once a safe orbit is reached, don't just loiter — BRING THE CREW HOME: cut the capsule free of the
            // spent stack (so the SuperDracos deorbit the light capsule, not the whole stack), then run the
            // controlled deorbit-return (trunk jettison → SuperDraco deorbit to the nearest safe water → shield-
            // forward entry → chutes). This is the real abort-to-orbit outcome: reach orbit, then a planned
            // deorbit. The crew is safe in orbit the whole time (a valid await-rescue state) and ends splashed down.
            if (atoDone)
            {
                if (!atoSeparated) { Actuator.SeparateDragon(v); atoSeparated = true; onsetUT = Now(); }
                FlyDeorbitReturn(v);
                return;
            }
            Actuator.OpenNoseShroud(v);
            Actuator.EnableRcs(v);

            Vector3d pro = Prograde(v);
            Steering.Point(v, pro);
            bool ready = Steering.PointingErrorDeg(v, pro) <= AttitudeReadyDeg;

            // keep whatever stage is lit thrusting, and push forward on the Dracos, until the periapsis is safe.
            if (ready)
            {
                FlightDriver.SetThrottle(1.0);
                FlightDriver.SetTranslation(0, 0, ForwardSign);
            }
            else { FlightDriver.ReleaseThrottle(); FlightDriver.ReleaseTranslation(); }

            if (PeA(v) >= SafeOrbitPeM)
            {
                atoDone = true;
                FlightDriver.ReleaseThrottle(); FlightDriver.ReleaseTranslation();
                Debug.Log("[DragonScreen] ABORT-TO-ORBIT complete — periapsis " + (PeA(v) / 1000.0).ToString("F0") + " km (safe)");
            }
        }

        // ---------------------------------------------------------------- DEORBIT RETURN (on-orbit / undock)
        static void FlyDeorbitReturn(Vessel v)
        {
            // docked → release the hooks first and let the springs open a gap.
            if (mode == AbortMode.EmergencyUndock && !undocked)
            {
                undocked = Actuator.Undock(v);
                if (undocked) onsetUT = Now();   // restart the clock post-undock
                Steering.Point(v, Retro(v));
                return;
            }

            Actuator.OpenNoseShroud(v);
            Actuator.EnableRcs(v);

            if (!deorbitDone)
            {
                // Hold retrograde, ready to burn, while we wait for the NEAREST SAFE (ocean) site to enter the
                // reachable window — then commit the deorbit. Earth is mostly ocean, so this is usually "now".
                if (!deorbitCommitted)
                {
                    Steering.Point(v, Retro(v));
                    // ⭐ DEORBIT NOW (land anywhere) commits IMMEDIATELY = fastest; the chutes make any touchdown
                    // survivable and the gear deploys after a land touchdown. WATER DEORBIT (and a fault abort)
                    // holds retrograde until the nearest reachable OPEN WATER is in the window (120 s backstop —
                    // never strand). Either way, once retrograde is held we can commit.
                    bool landNow = isRescue && deorbitLandOK;
                    if (landNow || SafeSiteReachable(v) || (Now() - onsetUT) > SiteSearchTimeoutS)
                    {
                        deorbitCommitted = true;
                        Debug.Log(landNow
                            ? "[DragonScreen] DEORBIT committed — DEORBIT NOW: immediate deorbit to the nearest safe site (land or water)"
                            : haveSite
                            ? "[DragonScreen] DEORBIT committed — nearest safe splashdown at "
                              + siteLatDeg.ToString("F1") + "," + siteLonDeg.ToString("F1")
                            : "[DragonScreen] DEORBIT committed — no water site resolved; deorbiting to a controlled entry anyway");
                    }
                    else return;   // keep coasting/holding until a safe site is reachable
                }

                RunDeorbitBurn(v);
                return;
            }

            // deorbit done → shield-forward controlled entry + chutes → touchdown.
            FlightDriver.ReleaseTranslation();
            HoldShieldForward(v);
            DriveAbortChutes(v);
            TouchdownGear(v);
        }

        // ⭐ Landing gear on a LAND touchdown only (DEORBIT NOW): the gear deploys IMMEDIATELY AFTER touchdown
        // on solid ground (v.Landed), never during descent, and NEVER on a water splashdown (v.Splashed → no
        // gear). One-shot. Water DEORBIT + fault aborts always splash, so this is a no-op there. Idempotent —
        // Actuator.DeployLegs only extends retracted legs, so a vehicle without gear is a harmless no-op.
        static void TouchdownGear(Vessel v)
        {
            if (gearDeployed || !deorbitLandOK) return;
            if (v.Landed && !v.Splashed)
            {
                Actuator.DeployLegs(v);
                gearDeployed = true;
                Debug.Log("[DragonScreen] LAND touchdown — landing gear deployed (post-touchdown).");
            }
        }

        // The shared Draco retrograde deorbit burn (DeorbitBurn) — the SAME engine + path as the nominal
        // return. ⛔ NOT the SuperDraco: it is ABORT-reserved and EMPTY on a return, so the retired path here
        // (which throttled EngineRole.PodAbort) fired an empty engine → ZERO thrust → the crew STRANDED
        // (flight 024400: throttle 0.36, thrust_n 0, pe unchanged). Draco RCS translation, closed-loop on
        // MEASURED pe; trunk jettison (ModuleTundraDecoupler) + shroud timing handled inside; Δv planned/
        // delivered → `deo` (recorded by FillRow). (docs/ABORT_PROCEDURES_RESEARCH.md: real Dragon deorbits
        // on the Dracos and reserves the SuperDracos.)
        static void RunDeorbitBurn(Vessel v)
        {
            FlightDriver.ReleaseThrottle();   // the Draco deorbit is RCS translation, never main-engine throttle
            if (DeorbitBurn.Tick(v, deo, ref trunkGone, DeorbitTargetPeM, AttitudeReadyDeg, SettleS, ForwardSign))
                deorbitDone = true;
        }

        // ---------------------------------------------------------------- KOS RETREAT (prox-ops)
        static void FlyRetreat(Vessel v)
        {
            Actuator.OpenNoseShroud(v);
            Actuator.EnableRcs(v);
            // hold attitude and back straight out of the corridor (aft translation opens the range).
            Steering.Point(v, Prograde(v));
            FlightDriver.SetTranslation(0, 0, -ForwardSign);   // opposite of a closing push = retreat
        }

        // ---------------------------------------------------------------- RIDE IT DOWN (already entering)
        static void FlyRideItDown(Vessel v)
        {
            Actuator.EnableRcs(v);
            if (!trunkGone) { Actuator.JettisonTrunk(v); trunkGone = true; }   // clear the shield if still on
            HoldShieldForward(v);
            DriveAbortChutes(v);
        }

        // ---------------------------------------------------------------- SAFE HOLD (nothing to escape with)
        static void FlySafeHold(Vessel v)
        {
            FlightDriver.ReleaseThrottle(); FlightDriver.ReleaseTranslation();
            Steering.Point(v, Steering.Up(v));   // hold a safe, stable attitude
        }

        // ================================ shared sequence pieces ================================

        // hold the heat shield into the oncoming flow: nose RETROGRADE to the SURFACE velocity (entry attitude).
        static void HoldShieldForward(Vessel v)
        {
            Vector3d srf = v.srf_velocity;
            Vector3d retro = srf.magnitude > 1 ? -((Vector3d)srf).normalized : Steering.Up(v);
            Steering.PointNoRoll(v, retro);
        }

        // the compressed abort chute sequence (drogues → ~2.5 s dwell → CUT + mains, or the low-altitude floor).
        static void DriveAbortChutes(Vessel v)
        {
            ChuteInputs ci = new ChuteInputs
            {
                Valid = true, AltitudeM = v.radarAltitude, DescentRateMps = -v.verticalSpeed,
                DrogueAltM = Mission.DrogueAltitude, MainAltM = Mission.MainAltitude, SeaAltM = 0.0
            };
            double tInDrogue = abortDrogueTime > 0.0 ? (Now() - abortDrogueTime) : 0.0;
            ChuteCommand cc = Chutes.SequenceAbort(ci, chutePhase, tInDrogue);
            chutePhase = cc.Phase;
            // Arm each canopy ONCE (RealChute arming is idempotent, but latching keeps the log clean and skips the
            // per-tick part scan). The drogues are NOT cut in an abort — they stay out as a backstop while RealChute
            // deploys the mains at their own lower envelope (see Chutes.SequenceAbort / Actuator.DeployChutePart).
            if (cc.DeployDrogues && !aDroguesArmed) { Actuator.DeployChutes(v, true); aDroguesArmed = true; if (abortDrogueTime <= 0.0) abortDrogueTime = Now(); }
            if (cc.DeployMains && !aMainsArmed) { Actuator.DeployChutes(v, false); aMainsArmed = true; }
        }

        // ================================ safe-site selection ================================

        // Scan the orbit's ground track ahead; is the NEAREST open-water splashdown reachable by a deorbit now?
        // Sets haveSite + site lat/lon when yes. Pure selection in SafeLandingSite; the body sampling is here.
        static bool SafeSiteReachable(Vessel v)
        {
            CelestialBody body = v.mainBody;
            if (body == null || v.orbit == null) return true;   // no body model → just deorbit
            if (!body.ocean) return true;                       // an ocean-less body → any controlled entry is "safe"

            double now = Now();
            double period = v.orbit.period > 1 ? v.orbit.period : 5400.0;
            double vGround = v.srfSpeed > 50 ? v.srfSpeed : 7000.0;   // for downrange-from-time
            Vector3d p0 = v.CoM;
            double lat0 = body.GetLatitude(p0), lon0 = body.GetLongitude(p0);

            GroundSample[] samples = new GroundSample[GroundSamples];
            for (int i = 0; i < GroundSamples; i++)
            {
                double dt = (i + 1) * GroundStepS;
                double ut = now + dt;
                Vector3d p = v.orbit.getPositionAtUT(ut);
                double lat = body.GetLatitude(p);
                // longitude in the body-fixed frame at the FUTURE ut: subtract the body's rotation over dt.
                double rot = body.rotationPeriod > 1 ? 360.0 * (dt / body.rotationPeriod) : 0.0;
                double lon = NormLon(body.GetLongitude(p) - rot);
                samples[i].DownrangeM = vGround * dt;
                samples[i].LatDeg = lat; samples[i].LonDeg = lon;
                // ⭐ F4 FIX (2026-08-29): the DEFAULT TerrainAltitude(lat,lon) CLAMPS ocean depth to 0
                // (allowNegative=false), so "< 0" was NEVER true over water → the RSS scan read 0/130 and the
                // abort never found an ocean. The THREE-ARG overload TerrainAltitude(lat,lon,true) returns the
                // real seabed height (negative under the ocean) — confirmed from MechJeb (KSP 1.12) which reads
                // it exactly this way (CelestialBodyExtensions/HoverslamSimulation + VesselState clamps
                // `ocean && ASL<0 → 0` for display). So a body-ocean point below the datum = open water.
                samples[i].Water = body.TerrainAltitude(lat, lon, true) < 0.0;   // below sea level ⇒ under ocean
            }

            int idx = SafeLandingSite.PickDeorbitTarget(samples, MinGlideM, MaxGlideM);
            if (idx < 0) idx = SafeLandingSite.PickNearestWater(samples, MinGlideM);

            // ⛔ INSTRUMENT the water gate (flight 173320: it never committed — need to see if TerrainAltitude
            // even reports ocean under RSS). Log once how many of the sampled ground-track points read as water.
            if (!siteLogged)
            {
                siteLogged = true;
                int water = 0; for (int i = 0; i < GroundSamples; i++) if (samples[i].Water) water++;
                Debug.Log("[DragonScreen] deorbit site scan: " + water + "/" + GroundSamples
                          + " ground-track samples over water; nearest-in-window idx=" + idx
                          + (idx >= 0 ? " at " + samples[idx].LatDeg.ToString("F1") + "," + samples[idx].LonDeg.ToString("F1") : ""));
            }

            if (idx < 0) return false;
            haveSite = true; siteLatDeg = samples[idx].LatDeg; siteLonDeg = samples[idx].LonDeg;
            return true;
        }

        // ================================ helpers ================================
        static double Now() { return Planetarium.GetUniversalTime(); }
        static double PeA(Vessel v) { return v.orbit != null ? v.orbit.PeA : 0.0; }
        static Vector3d Prograde(Vessel v)
        { Vector3d o = v.obt_velocity; return o.magnitude > 1 ? o.normalized : Steering.Prograde(v); }
        static Vector3d Retro(Vessel v)
        { Vector3d o = v.obt_velocity; return o.magnitude > 1 ? -o.normalized : Steering.Up(v); }

        static bool IsDocked(Vessel v)
        {
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    ModuleDockingNode nd = v.parts[i].Modules.GetModule<ModuleDockingNode>();
                    if (nd != null && nd.otherNode != null) return true;
                }
            }
            catch { }
            return false;
        }

        static bool NearStation(Vessel v)
        {
            ITargetable tgt = v.targetObject;
            if (tgt == null || tgt.GetTransform() == null) return false;
            double range = (v.CoM - (Vector3d)tgt.GetTransform().position).magnitude;
            return range <= Mission.ApproachRange * 3.0;   // within a few km of the station = prox-ops
        }

        static double NormLon(double lon)
        {
            while (lon > 180.0) lon -= 360.0;
            while (lon < -180.0) lon += 360.0;
            return lon;
        }

        static void FillRow(string[] row)
        {
            // reuse the return columns for the deorbit/chute detail an abort exercises.
            FlightRecorder.PutReturn(row, DepPhase.Idle, deo.Phase, EntryPhase.Idle, 0.0,
                                     false, chutePhase, chutePhase != ChutePhase.Idle,
                                     chutePhase == ChutePhase.Main || chutePhase == ChutePhase.Splashed);
            FlightRecorder.PutDv(row, deo.DvPlannedMps, deo.DvDeliveredMps);   // deorbit burn Δv (shared DeorbitBurn)
        }
    }
}
