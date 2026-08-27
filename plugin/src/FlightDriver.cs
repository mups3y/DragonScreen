// DragonScreen — FlightDriver  (KSP glue: the autopilot host, a flight-scene KSPAddon)
// ============================================================================================
// THE AUTOPILOT TICK LIVES HERE, not on the IVA screen objects. A KSPAddon(Flight) is scoped to the
// flight SCENE, so it survives the active-vessel switch a booster handover performs — the IVA (and the
// ScreenPainter on it) is destroyed the instant the Dragon stops being the active vessel, which is why
// nothing vehicle-wide may be ticked from there (see ScreenPainter.Update). This host:
//   • runs the crew-gate CONDUCTOR (CrewProcedureOps) against the live vessel each physics frame,
//   • performs the seam's single ACTUATION — ignition on the crew's launch GO,
//   • hands an ABORT to the phase-correct responder (pure/AbortResponder.cs),
//   • writes the per-flight FlightLog CSV.
// The flying-phase controllers (ascent/booster/rendezvous/docking/return) attach through OnFlyByWire in
// later seams; this seam establishes the host + the countdown → launch path. Defensive throughout — a
// glue fault logs and carries on, never taking the flight down.
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FlightDriver : MonoBehaviour
    {
        static FlightDriver instance;
        Vessel boundVessel;

        public void Start()
        {
            instance = this;
            // ⛔ A NEW flight scene (fresh launch, revert-to-VAB/launch, load) must start with the autopilot
            // FULLY IDLE. All the controllers hold STATIC state that survives a scene change, so without this
            // the last flight's engaged/mid-mission state carries onto the next vehicle and AUTO SEQUENCE is
            // still "on" the moment you roll out — flying a fresh pad rocket into the ground. Reset it all.
            ResetAll();
            EnsureKlaxon();
            Debug.Log("[DragonScreen] FlightDriver up (flight-scene autopilot host) — state reset");
        }

        static void ResetAll()
        {
            CrewProcedureOps.ForceReset();
            AscentControl.Reset();
            BoosterControl.Reset();
            RendezvousControl.Reset();
            DockingControl.Reset();
            ReturnControl.Reset();
            Steering.Release();
            ReleaseThrottle(); ReleaseTranslation(); ReleaseRoll();
            clampHeld = false; launchUT = 0.0; erectorClearing = false; autoTargetDone = false;
            launchHolding = false; gOverStartUT = -1.0;
            FlightLog.Fill = null;
            FlightLog.Close();
            aborting = false; abortPending = false; abortFxSuppressed = false; AbortControl.Reset();
            StopAbortFx();
        }

        public void OnDestroy()
        {
            if (instance == this) instance = null;
            Unbind();
            FlightLog.Close();
        }

        // ---- throttle authority: the active flying controller sets this; our OnFlyByWire applies it ----
        void Bind(Vessel v)
        {
            if (boundVessel == v) return;
            Unbind();
            if (v != null) { v.OnFlyByWire += OnFlyByWire; boundVessel = v; }
        }
        void Unbind()
        {
            if (boundVessel != null) { boundVessel.OnFlyByWire -= OnFlyByWire; boundVessel = null; }
        }
        // throttle authority — whichever flying controller is active sets it; OnFlyByWire applies it.
        static double cmdThrottle;
        static bool throttleOwned;
        public static void SetThrottle(double t)
        {
            cmdThrottle = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t); throttleOwned = true;
        }
        public static void ReleaseThrottle() { throttleOwned = false; }

        // RCS translation authority (Dracos) — the capsule's rendezvous/prox-ops burns. Control-frame
        // axes: X = right, Y = up, Z = fore/aft (KSP FlightCtrlState translation).
        static float transX, transY, transZ;
        static bool transOwned;
        public static void SetTranslation(double x, double y, double z)
        { transX = Clamp1(x); transY = Clamp1(y); transZ = Clamp1(z); transOwned = true; }
        public static void ReleaseTranslation() { transOwned = false; transX = transY = transZ = 0f; }

        // Live command readbacks for the always-on recorder snapshot — the ACTUALLY-APPLIED command
        // (0 when the axis is released, so a coast reads a clean 0, not a stale value).
        public static double CmdThrottle { get { return throttleOwned ? cmdThrottle : 0.0; } }
        public static double CmdTransX { get { return transOwned ? transX : 0.0; } }
        public static double CmdTransY { get { return transOwned ? transY : 0.0; } }
        public static double CmdTransZ { get { return transOwned ? transZ : 0.0; } }
        static float Clamp1(double d) { return (float)(d < -1.0 ? -1.0 : (d > 1.0 ? 1.0 : d)); }

        // Roll authority — the entry bank loop rolls the capsule about the velocity axis to the commanded
        // bank σ while SAS (a direction-only target) holds the nose retrograde. SAS leaves roll free, so
        // this st.roll coexists with the SAS pointing hold.
        static float cmdRoll;
        static bool rollOwned;
        public static void SetRoll(double r) { cmdRoll = Clamp1(r); rollOwned = true; }
        public static void ReleaseRoll() { rollOwned = false; cmdRoll = 0f; }

        // Attitude authority (the direct gimbal/RCS loop, AttitudePilot) — pitch+yaw always, roll optionally
        // (ascent/booster damp roll here; the entry bank keeps roll on the separate SetRoll channel above).
        static float cmdPitch, cmdYaw, cmdAttRoll;
        static bool attitudeOwned, attRollOwned;
        public static void SetAttitude(double pitch, double yaw)
        { cmdPitch = Clamp1(pitch); cmdYaw = Clamp1(yaw); attitudeOwned = true; }
        public static void SetAttitudeRoll(double roll) { cmdAttRoll = Clamp1(roll); attRollOwned = true; }
        public static void ReleaseAttitudeRoll() { attRollOwned = false; cmdAttRoll = 0f; }
        public static void ReleaseAttitude()
        { attitudeOwned = false; attRollOwned = false; cmdPitch = cmdYaw = cmdAttRoll = 0f; }

        void OnFlyByWire(FlightCtrlState st)
        {
            // Only take an axis when a controller is actively commanding it; otherwise leave the
            // player/idle in control. Pitch/yaw pointing is the direct gimbal loop (AttitudePilot).
            if (throttleOwned) st.mainThrottle = (float)cmdThrottle;
            if (transOwned) { st.X = transX; st.Y = transY; st.Z = transZ; }
            if (attitudeOwned) { st.pitch = cmdPitch; st.yaw = cmdYaw; }
            if (attRollOwned) st.roll = cmdAttRoll;   // AttitudePilot roll damping (ascent/booster)
            if (rollOwned) st.roll = cmdRoll;         // entry bank — wins if both ever set (mutually exclusive by phase)
        }

        // Physics-rate tick — control cadence, not display cadence.
        public void FixedUpdate()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || !HighLogic.LoadedSceneIsFlight) return;

            try
            {
                // ⛔ LAUNCH-TO-RENDEZVOUS (user 2026-08-27): lock the ISS as the target while on the pad, so the
                // ascent flies IN the ISS orbital plane (AscentControl reads v.targetObject). Runs only while
                // PRELAUNCH; once found (or the crew targeted something) it stops.
                if (!autoTargetDone && v.situation == Vessel.Situations.PRELAUNCH) AutoTargetStation(v);

                // ⛔ ABORT is ALWAYS live — even before AUTO SEQUENCE is engaged (a real EJECT handle works
                // anytime, e.g. a pad abort). It takes over everything and does not stop until under chutes.
                // Checked BEFORE the idle branch, which would otherwise clear abortPending on the pad.
                if (aborting || abortPending || CrewProcedureOps.AbortActive)
                {
                    Bind(v);
                    UpdateAbort(v);
                    FlightLog.Sample(v);
                    return;
                }

                if (!CrewProcedureOps.Engaged)
                {
                    // idle: not flying anyone. Close any open log, drop control, reset latches.
                    Unbind();
                    AscentControl.Reset();
                    BoosterControl.Reset();
                    RendezvousControl.Reset();
                    DockingControl.Reset();
                    ReturnControl.Reset();
                    ReleaseThrottle();
                    ReleaseTranslation();
                    ReleaseRoll();
                    AttitudePilot.Reset();
                    clampHeld = false; erectorClearing = false; launchHolding = false;
                    FlightLog.Fill = null;
                    FlightLog.Close();
                    aborting = false; abortPending = false; abortFxSuppressed = false; AbortControl.Reset();
                    return;
                }

                Bind(v);   // ensure our throttle hook is on the current vessel (follows handover)

                // A lone SEPARATED BOOSTER (the active vessel after a focus-switch) flies its own autonomous
                // recovery — the mission conductor tracks the Dragon, not this vehicle.
                if (BoosterControl.IsRecoverableBooster(v))
                {
                    BoosterControl.Tick(v);
                    FlightLog.Sample(v);
                    return;
                }

                // otherwise: the Dragon / mission vessel.
                BoosterControl.Reset();

                // ⛔ STRUCTURAL G ABORT (crewed vehicle, ANY phase): felt g past the structural limit means a
                // break-up, aero overload, or collision — get the crew out INSTANTLY. This is the universal
                // backstop that catches what the per-phase monitors miss. It is NOT the primary abort trigger —
                // real Crew Dragon aborts on DETECTED ANOMALIES (loss of thrust / loss of control), which the AoA
                // + Q monitors (AscentControl) and FDIR cover; this only catches a genuine sustained STRUCTURAL
                // overload. Only reached when not already aborting (the abort path returns above).
                //
                // ⛔ PHASE-AWARE + WIDE WINDOW (2026-08-27, researched — docs/ABORT_PROCEDURES_RESEARCH.md §G):
                //  • the limit is DISABLED during re-entry/descent, where 4–8 g is a NOMINAL re-entry load and the
                //    crew is already coming home (aborting on entry g is meaningless);
                //  • during ascent/orbit it sits above the ~4.5 g nominal ceiling;
                //  • it must PERSIST StructuralAbortDwellS (0.5 s) — a real break-up holds high g; a separation /
                //    staging / chute jolt is a sub-half-second spike and must NOT abort a good flight.
                double gAbortLimit = StructuralAbortLimitG();
                if (!double.IsNaN(gAbortLimit) && v.geeForce > gAbortLimit)
                {
                    if (gOverStartUT < 0.0) gOverStartUT = Planetarium.GetUniversalTime();
                    if (Planetarium.GetUniversalTime() - gOverStartUT >= StructuralAbortDwellS)
                    {
                        Debug.LogWarning("[DragonScreen] ⛔ G ABORT — felt " + v.geeForce.ToString("F1") + " g > "
                                         + gAbortLimit.ToString("F1") + " g structural limit for "
                                         + StructuralAbortDwellS.ToString("F2") + " s — ABORT");
                        RequestAbort();
                        UpdateAbort(v);
                        FlightLog.Sample(v);
                        return;
                    }
                }
                else gOverStartUT = -1.0;

                // 1) the crew-gate conductor advances on measured state + the crew's GO
                CrewProcedureOps.Tick(v);

                // 2) the launch sequence: on the GO, first HOLD for the target-plane window (launch-to-
                //    rendezvous RAAN — the pad must rotate under the ISS plane); then MOVE THE ERECTOR AWAY;
                //    ignite once clear; the clamp gate holds the hold-downs to full thrust, then releases.
                if (CrewProcedureOps.ConsumeLaunch()) StartLaunch(v);
                if (launchHolding) TickLaunchHold(v);
                if (erectorClearing) TickErectorClear(v);

                // 3) the flying-phase controller — but NOT while holding on the pad for the window (the
                //    vehicle is clamped, engines off; don't let the ascent loop steer/throttle a held rocket).
                if (!launchHolding) DriveActivePhase(v);

                // 3b) the clamp-release gate holds the hold-downs until full thrust is confirmed
                if (clampHeld) ClampGate(v);

                // 4) instrument (only while engaged — one CSV per autopilot flight)
                FlightLog.Sample(v);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] FlightDriver tick failed: " + e.Message);
            }
        }

        // Dispatch the active mission phase to its controller. Seam 2 wires ASCENT; the remaining
        // phases (booster/rendezvous/docking/return) attach here as their seams land. Between a
        // controller's phases the throttle authority is dropped so nothing is left commanding.
        static void DriveActivePhase(Vessel v)
        {
            switch (CrewProcedureOps.ActivePhase)
            {
                case MissionPhase.Ascent:
                    AscentControl.Tick(v, CrewProcedureOps.Profile);
                    break;
                case MissionPhase.Phasing:
                    // outbound Phasing = rendezvous; return Phasing = departure (same enum, IsReturn splits)
                    if (CrewProcedureOps.IsReturn) ReturnControl.TickDeparture(v, CrewProcedureOps.Profile);
                    else RendezvousControl.Tick(v, CrewProcedureOps.Profile);
                    break;
                case MissionPhase.Approach:
                case MissionPhase.Docked:
                    DockingControl.Tick(v, CrewProcedureOps.Profile);
                    break;
                case MissionPhase.Entry:
                    ReturnControl.TickDeorbitEntry(v, CrewProcedureOps.Profile);
                    break;
                case MissionPhase.Drogues:
                case MissionPhase.Mains:
                case MissionPhase.Splashdown:
                    ReturnControl.TickChutes(v);
                    break;
                default:
                    AscentControl.Reset();
                    RendezvousControl.Reset();
                    DockingControl.Reset();
                    ReturnControl.Reset();
                    ReleaseThrottle();
                    ReleaseTranslation();
                    ReleaseRoll();
                    FlightLog.Fill = null;   // no flying controller contributing columns right now
                    break;
            }
        }

        // ⛔ CLAMP-RELEASE GATE (plan §3.4): light the octaweb, but HOLD the hold-downs until the measured
        // thrust reaches ≥99% of available (a failed light before release = pad safe-abort, never lift off on
        // a bad engine). ClampGate runs each tick until release; the gimbal integral is reset while clamped.
        static bool clampHeld;
        static double launchUT;
        public static bool ClampHeld { get { return clampHeld; } }
        public static double ClampThrustFrac { get { return lastClampThrustFrac; } }

        // ⛔ REAL LAUNCH ORDER (user 2026-08-27): (1) MOVE THE ERECTOR AWAY, (2) ignite + confirm thrust,
        // (3) release the hold-down clamps. The erector is swung clear BEFORE ignition — never decoupled onto
        // a lifting-off rocket. erectorClearing gates the ignition until the "Open Erector" animation finishes
        // (or ErectorMaxClearS, a backstop if the animation can't be read).
        static bool erectorClearing;
        static double erectorStartUT;
        [Tunable] public static double ErectorMaxClearS = 8.0;   // ignite-anyway backstop if the arm never reads clear

        // ⛔ Lock the ISS/station as the launch target so the ascent flies into its plane (launch-to-rendezvous).
        // Picks the highest station-class vessel in a real orbit around this body. Idempotent; stops once set.
        static bool autoTargetDone;
        static void AutoTargetStation(Vessel v)
        {
            try
            {
                if (v.targetObject != null) { autoTargetDone = true; return; }   // crew already chose a target
                Vessel best = null;
                var list = FlightGlobals.Vessels;
                for (int i = 0; i < list.Count; i++)
                {
                    Vessel s = list[i];
                    if (s == null || s == v || s.orbit == null || s.mainBody != v.mainBody) continue;
                    bool isStation = s.vesselType == VesselType.Station
                        || (s.vesselName != null &&
                            (s.vesselName.IndexOf("ISS", StringComparison.OrdinalIgnoreCase) >= 0
                             || s.vesselName.IndexOf("Station", StringComparison.OrdinalIgnoreCase) >= 0));
                    if (!isStation || s.orbit.PeA < 100000.0) continue;          // must be a real orbit, not on a pad
                    if (best == null || s.orbit.ApA > best.orbit.ApA) best = s;
                }
                if (best != null)
                {
                    FlightGlobals.fetch.SetVesselTarget(best, true);
                    Debug.Log("[DragonScreen] LAUNCH-TO-RENDEZVOUS — ISS locked as target: " + best.vesselName
                              + " (inc " + best.orbit.inclination.ToString("F2") + "°, "
                              + (best.orbit.PeA / 1000.0).ToString("F0") + "×" + (best.orbit.ApA / 1000.0).ToString("F0") + " km)");
                    autoTargetDone = true;
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] auto-target failed: " + e.Message); }
        }

        // ---- LAUNCH-TO-RENDEZVOUS PLANE WINDOW (RAAN) ----
        // Correct rendezvous needs the right RAAN, and RAAN is set by WHEN you lift off — the pad must rotate
        // THROUGH the target's orbital plane. On the crew GO we compute the time to that crossing (LaunchWindow),
        // and if it is more than a few seconds away we HOLD the countdown (warping there), then ignite in-plane.
        [Tunable] public static bool LaunchWindowHold = true;    // hold for the coplanar (RAAN) window
        [Tunable] public static bool LaunchAutoWarp = true;      // warp to the window automatically
        [Tunable] public static double LaunchNodeSign = 1.0;     // which node opportunity — flip if RAAN 180° off
        [Tunable] public static double LaunchWindowTolS = 4.0;   // GO when within this of the window
        [Tunable] public static double LaunchLeadS = 8.0;        // stop the warp this long before the window
        static bool launchHolding;
        static double launchWindowUT;

        // Decide on the crew GO: hold for the plane window, or launch now.
        static void StartLaunch(Vessel v)
        {
            double tSec;
            if (LaunchWindowHold && v != null && v.situation == Vessel.Situations.PRELAUNCH
                && ComputeLaunchWindowS(v, out tSec) && tSec > LaunchWindowTolS)
            {
                launchHolding = true;
                launchWindowUT = Planetarium.GetUniversalTime() + tSec;
                Debug.Log("[DragonScreen] ⏸ LAUNCH HOLD — target-plane (RAAN) window in " + tSec.ToString("F0")
                          + " s (" + (tSec / 60.0).ToString("F1") + " min); holding for a COPLANAR launch.");
                if (LaunchAutoWarp) WarpTo(launchWindowUT - LaunchLeadS);
            }
            else BeginLaunch(v);
        }

        // Held each tick during the plane-window wait: GO when the window is within tolerance, else keep warping.
        static void TickLaunchHold(Vessel v)
        {
            double now = Planetarium.GetUniversalTime();
            double tSec;
            bool have = ComputeLaunchWindowS(v, out tSec);   // recompute live (the stored UT is the fallback)
            if ((have && tSec <= LaunchWindowTolS) || now >= launchWindowUT - LaunchWindowTolS)
            {
                if (TimeWarp.CurrentRateIndex != 0) TimeWarp.SetRate(0, true);   // out of warp before ignition
                launchHolding = false;
                Debug.Log("[DragonScreen] ▶ LAUNCH WINDOW OPEN — igniting for a coplanar insertion.");
                BeginLaunch(v);
            }
            else if (LaunchAutoWarp && TimeWarp.CurrentRateIndex == 0
                     && (launchWindowUT - now) > LaunchLeadS + 5.0)
            {
                WarpTo(launchWindowUT - LaunchLeadS);   // re-arm the warp if it stopped early
            }
        }

        // Seconds to the next crossing of the target's orbital plane by the launch site. Plane normal from the
        // ORBIT (r1×r2, world frame — safe for the unloaded ISS); axis + rate from the body's spin.
        static bool ComputeLaunchWindowS(Vessel v, out double tSec)
        {
            tSec = 0.0;
            try
            {
                CelestialBody b = v.mainBody;
                ITargetable tgt = v.targetObject;
                Orbit o = (tgt != null) ? tgt.GetOrbit() : null;
                if (b == null || o == null) return false;
                double now = Planetarium.GetUniversalTime();
                Vector3d bpos = b.position;
                Vector3d nrm = Vector3d.Cross(o.getPositionAtUT(now) - bpos, o.getPositionAtUT(now + 120.0) - bpos);
                if (nrm.magnitude < 1.0) return false;
                Vector3d site = v.CoM - bpos;
                Vector3d w = b.angularVelocity;
                double omega = w.magnitude;
                Vector3d axis = omega > 1e-9 ? w : (Vector3d)b.transform.up;
                if (omega <= 1e-9) omega = (b.rotationPeriod > 0.0) ? 2.0 * Math.PI / b.rotationPeriod : 0.0;
                return LaunchWindow.TimeToCrossing(W(site), W(nrm), W(axis), omega, (int)LaunchNodeSign, out tSec);
            }
            catch { return false; }
        }

        static void WarpTo(double ut)
        {
            try { if (TimeWarp.fetch != null && ut > Planetarium.GetUniversalTime()) TimeWarp.fetch.WarpTo(ut); }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] launch warp failed: " + e.Message); }
        }

        static Vec3 W(Vector3d u) { return new Vec3(u.x, u.y, u.z); }

        static void BeginLaunch(Vessel v)
        {
            try
            {
                Debug.Log("[DragonScreen] LAUNCH — crew GO cleared; STEP 1: moving the erector away.");
                Actuator.OpenErector(v);            // ⛔ retract the strongback FIRST (before ignition)
                erectorClearing = true;
                erectorStartUT = Planetarium.GetUniversalTime();
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] launch begin failed: " + e.Message); }
        }

        // Wait for the erector to swing clear, THEN ignite. (Backstop: ignite after ErectorMaxClearS regardless.)
        static void TickErectorClear(Vessel v)
        {
            double waited = Planetarium.GetUniversalTime() - erectorStartUT;
            if (Actuator.ErectorClear(v) || waited >= ErectorMaxClearS)
            {
                erectorClearing = false;
                Ignite(v);
            }
        }

        static void Ignite(Vessel v)
        {
            try
            {
                Debug.Log("[DragonScreen] LAUNCH — STEP 2: erector clear; igniting (hold-downs held until full thrust).");
                SetThrottle(1.0);         // full throttle AT ignition — never light the engines against a 0 throttle
                Actuator.IgniteOctawebLiftoff(v);   // ⛔ direct: light ONLY the octaweb all-engines mode (no staging)
                clampHeld = true;
                launchUT = Planetarium.GetUniversalTime();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] ignition command failed: " + e.Message);
            }
        }

        // Held every tick between ignition and hold-down release. Releases at ≥99% thrust; safe-aborts a
        // failed light (keep clamps, shut down); keeps the gimbal integral zeroed so it cannot kick at release.
        static double lastClampThrustFrac;
        static void ClampGate(Vessel v)
        {
            AttitudePilot.ResetIntegrators();   // bolted down → no windup

            double thrustN, availN; int lit;
            Actuator.EngineThrust(v, EngineRole.OctawebAll, out thrustN, out availN, out lit);
            lastClampThrustFrac = availN > 1.0 ? thrustN / availN : 0.0;
            double heldS = Planetarium.GetUniversalTime() - launchUT;

            switch (IgnitionGate.Evaluate(thrustN, availN, lit, heldS))
            {
                case ClampAction.Release:
                    // STEP 3: full thrust confirmed → release the hold-down clamps (the erector's decoupler).
                    // The erector ARM was already swung clear before ignition (BeginLaunch), so this only frees
                    // the hold-down — the rocket lifts off past an erector that is already out of the way.
                    Actuator.ReleaseHoldDowns(v);
                    clampHeld = false;
                    Debug.Log("[DragonScreen] CLAMP RELEASE — thrust " + (lastClampThrustFrac * 100.0).ToString("F0") + "% of available, liftoff");
                    break;
                case ClampAction.SafeAbort:
                    // Failed light while still bolted down = a SAFE state: shut the engine, KEEP the clamps,
                    // drop throttle. Do NOT fire the SuperDracos — the vehicle is held to the pad, not falling.
                    Actuator.ShutdownBoosterEngines(v);
                    SetThrottle(0.0); ReleaseThrottle(); clampHeld = false;
                    Debug.LogWarning("[DragonScreen] ⛔ PAD SAFE-ABORT — octaweb failed to reach thrust ("
                                     + (lastClampThrustFrac * 100.0).ToString("F0") + "%); engines shut, clamps held.");
                    break;
                default:
                    break;   // Hold — keep waiting for thrust
            }
        }

        // ⛔ Hold-down release + decoupler firing moved to Actuator (Actuator.ReleaseHoldDowns / FireDecoupler).

        // ============================ ABORT — and it must LAND THE CREW ============================
        // A real abort runs the REGIME-CORRECT procedure (AbortControl): the mode is chosen from the live
        // state (pad/ascent launch-escape with trunk jettison + shield-forward reorient; near-orbital abort-
        // to-orbit; on-orbit deorbit to the nearest SAFE splashdown; prox-ops retreat; docked emergency
        // undock; ride-it-down), then flown to splashdown. FlightDriver just latches the abort, safes the
        // controls, and drives the alarm FX + the DON'T PANIC screens (from Aborting).
        static bool aborting, abortPending;

        // Phase-aware structural-g abort limit (g), or NaN to DISABLE it this phase. Researched per phase
        // (docs/ABORT_PROCEDURES_RESEARCH.md §G): nominal loads are S1 ~3.3 g, S2 ~4.5 g, coast ~0 g, and
        // re-entry a NOMINAL 4–8 g (lifting → ballistic). So during ascent/orbit the limit is the structural
        // backstop above the ~4.5 g ceiling; during re-entry & descent (Entry/Drogues/Mains/Splashdown) it is
        // DISABLED — high g there is expected and the crew is already returning. An active abort is handled
        // before this check, so the abort's own SuperDraco/entry g never reaches here.
        static double StructuralAbortLimitG()
        {
            MissionPhase ph = CrewProcedureOps.ActivePhase;
            if (ph == MissionPhase.Entry || ph == MissionPhase.Drogues || ph == MissionPhase.Mains
                || ph == MissionPhase.Splashdown || ph == MissionPhase.Landed)
                return double.NaN;                     // re-entry / descent — no g-abort
            return StructuralAbortG;                   // ascent / coast / prox-ops structural backstop
        }

        // ⛔ Felt-g above this, HELD for StructuralAbortDwellS, triggers an abort (mission-spec structural limit).
        // Raised 4.5→6.0: the crew g-limit already caps nominal ascent at ~4.0 g, so 4.5 sat only 0.5 g above the
        // operating point and a sep transient tripped it (flight 173320 false-aborted a good orbit). 6.0 g is a
        // real overload; the dwell rejects single-frame jolts. Below this, the g-limit does the protecting.
        [Tunable] public static double StructuralAbortG = 6.0;
        // Must persist this long to abort. 0.5 s: a real structural break-up / thrust-overload HOLDS high g for
        // longer than this, while a separation / staging / decoupler / chute jolt is a sub-half-second spike (the
        // 190114 sep spike was ~0.26 s) and must NOT trip a good flight. Wider than a hair-trigger by design —
        // the g-abort is a backstop, and the fast anomaly triggers (loss of thrust / control) are elsewhere.
        [Tunable] public static double StructuralAbortDwellS = 0.5;
        static double gOverStartUT = -1.0;

        // ---- ABORT FX ---- the alarm klaxon + the red IVA-light strobe (the flashing DON'T PANIC screens
        // themselves are drawn by ScreenPainter/AbortOverlay from FlightDriver.Aborting).
        static AudioSource klaxon;
        static List<Light> ivaLights;
        static List<Color> ivaOrig;

        // ⛔ SUPPRESS RESPONSE (the panel "SURPRESS FIRE" button during an abort): silences the klaxon + stops
        // the red cabin-light strobe, and drops the on-screen alert to the CENTRE screen only. It does NOT
        // cancel the abort — the procedure runs to splashdown regardless (there is no cancelling an abort).
        static bool abortFxSuppressed;
        public static bool AbortFxSuppressed { get { return abortFxSuppressed; } }
        public static void SuppressAbortFx()
        {
            abortFxSuppressed = true;
            Debug.Log("[DragonScreen] SUPPRESS RESPONSE — klaxon + red cabin lights off, centre-screen alert kept; the abort CONTINUES.");
        }

        public static void RequestAbort() { abortPending = true; }
        public static bool Aborting { get { return aborting; } }

        static void UpdateAbort(Vessel v)
        {
            if (!aborting)
            {
                aborting = true; abortFxSuppressed = false;   // a fresh abort re-arms the alarm
                Debug.Log("[DragonScreen] ⛔ ABORT — regime-aware response engaging.");
                ReleaseThrottle(); ReleaseTranslation(); ReleaseRoll(); AttitudePilot.Reset();
                AbortControl.Reset();
            }

            AbortControl.Tick(v);   // decide the mode + fly the real procedure to a safe splashdown
            UpdateAbortFx(v);       // alarm klaxon + red IVA-light strobe (silenced by suppress / at splashdown)
        }

        // A looping two-tone klaxon, generated in code (no bundled sound file). Created once on this addon's
        // GameObject; Unity's fake-null makes a scene-destroyed source re-create cleanly next flight.
        static void EnsureKlaxon()
        {
            if (klaxon != null || instance == null) return;
            try
            {
                const int rate = 44100, len = rate;   // 1 s, looped
                float[] data = new float[len];
                for (int i = 0; i < len; i++)
                {
                    double t = (double)i / rate;
                    double f = (t < 0.5) ? 600.0 : 850.0;                       // two-tone hi-lo alarm
                    data[i] = (float)(Math.Sign(Math.Sin(2.0 * Math.PI * f * t)) * 0.20);   // square wave
                }
                AudioClip clip = AudioClip.Create("dragon_klaxon", len, 1, rate, false);
                clip.SetData(data, 0);
                klaxon = instance.gameObject.AddComponent<AudioSource>();
                klaxon.clip = clip; klaxon.loop = true; klaxon.volume = 0.55f;
                klaxon.spatialBlend = 0f; klaxon.playOnAwake = false; klaxon.ignoreListenerPause = true;
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] klaxon setup failed: " + e.Message); }
        }

        // Play the klaxon + strobe the IVA lights red in sync with the screen flash — until safely splashed.
        static void UpdateAbortFx(Vessel v)
        {
            bool splashed = v != null && (v.situation == Vessel.Situations.SPLASHED || v.situation == Vessel.Situations.LANDED);
            bool silent = splashed || abortFxSuppressed;   // suppress response OR safely down → no klaxon/lights
            EnsureKlaxon();
            if (klaxon != null)
            {
                if (silent) { if (klaxon.isPlaying) klaxon.Stop(); }
                else if (!klaxon.isPlaying) klaxon.Play();
            }
            bool on = !silent && ((int)(Time.time * 3f) & 1) == 0;   // same ~1.5 Hz square flash as the screens
            StrobeIva(v, on, silent);   // restore = silent → cabin lights back to their own colour
        }

        // Tint the crew-cabin (IVA) lights red on the flash, back to their own colour off it. Only works while
        // the IVA is instantiated (the crew is looking at it anyway); a no-op otherwise. Guarded end-to-end.
        static void StrobeIva(Vessel v, bool on, bool restore)
        {
            try
            {
                if (ivaLights == null)
                {
                    ivaLights = new List<Light>(); ivaOrig = new List<Color>();
                    for (int i = 0; i < v.parts.Count; i++)
                    {
                        InternalModel im = v.parts[i].internalModel;
                        if (im == null) continue;
                        foreach (Light L in im.GetComponentsInChildren<Light>())
                        {
                            if (L == null) continue;
                            ivaLights.Add(L); ivaOrig.Add(L.color);
                        }
                    }
                }
                for (int i = 0; i < ivaLights.Count; i++)
                {
                    if (ivaLights[i] == null) continue;
                    ivaLights[i].color = (restore || !on) ? ivaOrig[i] : Color.red;
                }
            }
            catch { }
        }

        // Silence the klaxon + restore the IVA lights (scene reset / fresh flight).
        static void StopAbortFx()
        {
            try { if (klaxon != null && klaxon.isPlaying) klaxon.Stop(); } catch { }
            try
            {
                if (ivaLights != null)
                    for (int i = 0; i < ivaLights.Count; i++)
                        if (ivaLights[i] != null) ivaLights[i].color = ivaOrig[i];
            }
            catch { }
            ivaLights = null; ivaOrig = null;
        }

        // ⛔ Chute deploy (RealChute-aware) moved to Actuator.DeployChutes; the abort's chute/deorbit columns
        // are filled by AbortControl (it owns the abort descent), so no separate filler here.
    }
}
