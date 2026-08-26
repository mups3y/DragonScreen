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
using KSP.UI.Screens;

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
            Debug.Log("[DragonScreen] FlightDriver up (flight-scene autopilot host)");
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

        void OnFlyByWire(FlightCtrlState st)
        {
            // Only take an axis when a controller is actively commanding it; otherwise leave the
            // player/idle in control. Pitch/yaw pointing is held by Steering (SAS), not here.
            if (throttleOwned) st.mainThrottle = (float)cmdThrottle;
            if (transOwned) { st.X = transX; st.Y = transY; st.Z = transZ; }
            if (rollOwned) st.roll = cmdRoll;
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
                    FlightLog.Fill = null;
                    FlightLog.Close();
                    abortFired = false;
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

                // 1) the crew-gate conductor advances on measured state + the crew's GO
                CrewProcedureOps.Tick(v);

                // 2) the seam's single countdown actuation: ignition on the launch GO
                if (CrewProcedureOps.ConsumeLaunch()) Ignite(v);

                // 3) the flying-phase controller for the active phase
                if (CrewProcedureOps.AbortActive) HandleAbort(v);
                else DriveActivePhase(v);

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
                StageManager.ActivateNextStage();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] ignition command failed: " + e.Message);
            }
        }

        static bool abortFired;
        static void HandleAbort(Vessel v)
        {
            if (abortFired) return;
            try
            {
                AbortInputs ai;
                ai.Triggered = true;
                ai.Phase = CrewProcedureOps.ActivePhase;
                ai.LesArmed = FlightCommands.EscapeArmed;
                AbortCommand c = AbortResponder.Respond(ai);
                Debug.Log("[DragonScreen] ABORT: " + c.Mode + " — " + c.Note);
                if (c.FireSuperDracos || c.Separate)
                    v.ActionGroups.SetGroup(KSPActionGroup.Abort, true);   // stock Abort AG = SuperDraco escape
                abortFired = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] abort command failed: " + e.Message);
            }
        }
    }
}
