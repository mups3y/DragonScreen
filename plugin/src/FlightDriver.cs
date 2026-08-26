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
            clampHeld = false; launchUT = 0.0;
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
                    clampHeld = false;
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

                // ⛔ STRUCTURAL G ABORT (crewed vehicle, ANY phase): felt g past the structural limit means a
                // break-up, aero overload, or collision — get the crew out INSTANTLY. This is the universal
                // backstop that catches what the per-phase monitors miss (the stack RUD spiked to 5+ g the
                // instant it broke). Only reached when not already aborting (the abort path returns above).
                if (v.geeForce > StructuralAbortG)
                {
                    Debug.LogWarning("[DragonScreen] ⛔ G ABORT — felt " + v.geeForce.ToString("F1") + " g > "
                                     + StructuralAbortG.ToString("F1") + " g structural limit — ABORT");
                    RequestAbort();
                    UpdateAbort(v);
                    FlightLog.Sample(v);
                    return;
                }

                // 1) the crew-gate conductor advances on measured state + the crew's GO
                CrewProcedureOps.Tick(v);

                // 2) the seam's single countdown actuation: ignition on the launch GO
                if (CrewProcedureOps.ConsumeLaunch()) Ignite(v);

                // 3) the flying-phase controller for the active phase
                DriveActivePhase(v);

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

        static void Ignite(Vessel v)
        {
            try
            {
                Debug.Log("[DragonScreen] LAUNCH — crew GO cleared; igniting (hold-downs held until full thrust).");
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
                    Actuator.ReleaseHoldDowns(v);   // ⛔ free the clamps AND the erector — full thrust confirmed
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
        // A previous "abort" fired the action group and walked away — so the escaped capsule tumbled and
        // hit the ground with the crew still aboard. A real abort is not done until the chutes are out. So:
        // fire the SuperDraco escape (stock Abort AG) ONCE, then FLY THE CAPSULE DOWN — deploy the drogues
        // and mains on measured altitude/descent until splashdown.
        static bool aborting, abortPending;
        static ChutePhase abortChute = ChutePhase.Idle;

        // ⛔ Felt-g above this triggers an INSTANT abort in any phase (mission-spec structural limit). Nominal
        // ascent is throttle-limited to 3.5 g; blowing past 4.5 g means the vehicle is coming apart.
        [Tunable] public static double StructuralAbortG = 4.5;

        public static void RequestAbort() { abortPending = true; }
        public static bool Aborting { get { return aborting; } }

        static void UpdateAbort(Vessel v)
        {
            if (!aborting)
            {
                aborting = true; abortChute = ChutePhase.Idle;
                Debug.Log("[DragonScreen] ⛔ ABORT — SuperDraco launch escape, then chutes to splashdown.");
                ReleaseThrottle(); ReleaseTranslation(); ReleaseRoll(); AttitudePilot.Reset();
                FlightLog.Fill = AbortFillRow;   // ⛔ replace the stale ascent filler so the abort + chute descent record
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

        // The abort's recorder columns (the always-on base already logs phase/AoA/thrust/abort; this adds the
        // chute detail the abort descent owns). Replaces the frozen ascent filler for the rest of the flight.
        static void AbortFillRow(string[] row) { FlightRecorder.PutAbortChutes(row, abortChute); }

        // ⛔ Chute deploy (RealChute-aware) moved to Actuator.DeployChutes.
    }
}
