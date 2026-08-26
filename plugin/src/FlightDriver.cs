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
            FlightLog.Fill = null;
            FlightLog.Close();
            aborting = false; abortPending = false; abortChute = ChutePhase.Idle;
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
                    FlightLog.Fill = null;
                    FlightLog.Close();
                    aborting = false; abortPending = false; abortChute = ChutePhase.Idle;
                    return;
                }

                Bind(v);   // ensure our throttle hook is on the current vessel (follows handover)

                // ⛔ ABORT takes over everything and does not stop until the crew is under chutes.
                if (aborting || abortPending || CrewProcedureOps.AbortActive)
                {
                    UpdateAbort(v);
                    FlightLog.Sample(v);
                    return;
                }

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

                // 1) the crew-gate conductor advances on measured state + the crew's GO
                CrewProcedureOps.Tick(v);

                // 2) the seam's single countdown actuation: ignition on the launch GO
                if (CrewProcedureOps.ConsumeLaunch()) Ignite(v);

                // 3) the flying-phase controller for the active phase
                DriveActivePhase(v);

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

        static void Ignite(Vessel v)
        {
            try
            {
                Debug.Log("[DragonScreen] LAUNCH — crew GO cleared; igniting.");
                SetThrottle(1.0);         // full throttle AT ignition — never light the engines against a 0 throttle
                Actuator.IgniteOctawebLiftoff(v);   // ⛔ direct: light ONLY the octaweb all-engines mode (no staging)
                Actuator.ReleaseHoldDowns(v);       // ⛔ free EVERYTHING holding the rocket: clamps AND the erector.
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] ignition command failed: " + e.Message);
            }
        }

        // ⛔ Hold-down release + decoupler firing moved to Actuator (Actuator.ReleaseHoldDowns / FireDecoupler).

        // ============================ ABORT — and it must LAND THE CREW ============================
        // A previous "abort" fired the action group and walked away — so the escaped capsule tumbled and
        // hit the ground with the crew still aboard. A real abort is not done until the chutes are out. So:
        // fire the SuperDraco escape (stock Abort AG) ONCE, then FLY THE CAPSULE DOWN — deploy the drogues
        // and mains on measured altitude/descent until splashdown.
        static bool aborting, abortPending;
        static ChutePhase abortChute = ChutePhase.Idle;

        public static void RequestAbort() { abortPending = true; }
        public static bool Aborting { get { return aborting; } }

        static void UpdateAbort(Vessel v)
        {
            if (!aborting)
            {
                aborting = true; abortChute = ChutePhase.Idle;
                Debug.Log("[DragonScreen] ⛔ ABORT — SuperDraco launch escape, then chutes to splashdown.");
                ReleaseThrottle(); ReleaseTranslation(); ReleaseRoll();
                Actuator.FireAbort(v);   // ⛔ direct: SuperDraco motor at full + capsule separation (no action group)
            }

            // fly the escaped capsule down: deploy chutes on measured altitude + descent (safety backstop).
            try
            {
                ChuteInputs ci = new ChuteInputs
                {
                    Valid = true, AltitudeM = v.radarAltitude, DescentRateMps = -v.verticalSpeed,
                    DrogueAltM = Mission.DrogueAltitude, MainAltM = Mission.MainAltitude, SeaAltM = 0.0
                };
                ChuteCommand cc = Chutes.Sequence(ci, abortChute);
                abortChute = cc.Phase;
                if (cc.DeployDrogues) Actuator.DeployChutes(v, true);
                if (cc.DeployMains) Actuator.DeployChutes(v, false);
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] abort descent failed: " + e.Message); }
        }

        // ⛔ Chute deploy (RealChute-aware) moved to Actuator.DeployChutes.
    }
}
