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

        // ---- FDIR (L5 safety spine) — OBSERVE-ONLY by default (tasks T2 + T2b) ----
        // The tested pure Fdir (pure/Fdir.cs) runs at ~4 Hz on the live feeds and LOGS any tripped fault. It
        // does NOT command an abort unless FdirActing is turned on (a false abort kills a good flight → acting is
        // flight-gated). HONEST feed status (all shaped by pure/FdirFeeds.cs so an UNMEASURABLE moment reads
        // nominal, never a false trip):
        //   • ResourceCritical  — LIVE from the Dragon's RCS/Draco propellant margin (the real mission-ending
        //     resource; ~full through ascent → no false trip, true margin for rendezvous/deorbit).           [T2]
        //   • ThrustShortfall   — LIVE (T2b): Σ finalThrust / (throttle·Σ full-max) over the COMMANDED main
        //     engines. A flamed-out-but-commanded engine drops the ratio → the honest engine-out signal. Reads
        //     nominal on a coast, a Draco-only burn, or while the hold-downs are clamped (thrust ramping).
        //   • NoControlSolution — LIVE (T2b): the no-authority TUMBLE — actively holding attitude, ~zero control
        //     authority, spinning past a tumble rate, pointing far off (the RCS-GetPotentialTorque-zero case). A
        //     healthy hard slew HAS authority → excluded. Max-Q gimbal saturation is caught upstream (AscentControl).
        //   • ConvergenceStall  — LIVE (T2b): the near-field closing rate published by RendezvousControl, only
        //     while it is actively closing (an intended phasing coast leaves it nominal).
        //   • TrajectoryDivergence — still NOMINAL: no honest UNIFORM position-error residual without inventing
        //     one; ascent divergence is covered by AscentControl's q·α/AoA + the structural-g abort, and near-field
        //     drift by DockingControl's own corridor/KOS-breach abort — so a nominal feed here is honest, not owed.
        // The KOS-breach abort is owned by DockingControl (it acts), so FDIR does not double it here.
        [Tunable] public static bool FdirActing = false;      // OFF = observe + log only; ON = FDIR may abort
        [Tunable] public static double FdirTickS = 0.25;       // ~4 Hz cadence (keeps the hot path light)
        // NoControlSolution tumble thresholds (conservative → only an unambiguous no-authority tumble trips):
        [Tunable] public static double CtrlAuthFloorNm = 50.0;    // best-axis control torque below this = ~no authority
        [Tunable] public static double CtrlTumbleRateRads = 0.15;  // spinning faster than this (~8.6°/s) = tumbling
        [Tunable] public static double CtrlLostErrDeg = 30.0;      // and pointing this far off target
        static FdirState fdirState;
        // ⭐ R2 recorder fidelity: the LAST FDIR report, so FlightLog can write it into the CSV every sample.
        // Before this, PutFdir was never called → the fdir_fault/recovery/abort/abort_mode columns stayed blank
        // even while KSP.log logged 10+ faults (flight 144114). Observe-only faults ARE recorded (they're the
        // point — a fault that never reaches the CSV can't be correlated with the control state).
        static FdirReport lastFdirReport;
        public static FdirReport LastFdirReport { get { return lastFdirReport; } }
        static double fdirAccumS, lastFdirLogUT = -999.0;
        static readonly List<int> rcsPropIds = new List<int>();   // cached Draco/RCS propellant resource ids

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
            abortIsRecovery = false; AuthorityManager.Reset();
            MissionConductor.Reset();
            fdirState = new FdirState(); fdirAccumS = 0.0; lastFdirLogUT = -999.0; rcsPropIds.Clear();
            lastFdirReport = new FdirReport();   // R2: fresh scene starts with a nominal (no-fault) recorded FDIR
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
            if (v != null) { v.OnFlyByWire += OnFlyByWire; boundVessel = v; CacheRcsProps(v); }
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
        // ⛔ NaN GUARD (Phase 2, rule N2): NaN < -1 and NaN > 1 are BOTH false, so a NaN command would fall
        // straight through this clamp into FlightCtrlState and hit the actuators. Sanitize NaN → 0 (neutral):
        // a bad controller output must never reach the thrusters as NaN. A finite value clamps to [-1,1] as before.
        static float Clamp1(double d)
        {
            if (double.IsNaN(d)) return 0f;
            return (float)(d < -1.0 ? -1.0 : (d > 1.0 ? 1.0 : d));
        }

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

        // ⭐ PWPF / phase-plane RCS pulse modulation (Tier-2, pure/RcsPulse) — turns the continuous RCS
        // command into thruster pulses whose average tracks it, with a deadband that KILLS the two-sided
        // limit cycle that thrashed the Dracos and wasted MMH/NTO (Campaign-6). A near-full command passes
        // through continuous (sustained burns intact); only trim commands are pulsed. MechJeb never does this
        // for RCS — our improvement (docs/MECHJEB_MASTER_MAP.md §3.3). Tunable; false = continuous fallback.
        [Tunable] public static bool   UseRcsPulse      = true;
        [Tunable] public static double RcsPulseDeadband = 0.05;   // |cmd| below → command nothing (chatter kill)
        [Tunable] public static double RcsPulseMinOn    = 0.06;   // s, minimum pulse width
        [Tunable] public static double RcsPulseMinOff   = 0.06;   // s, minimum gap
        [Tunable] public static double RcsPulseFull     = 0.90;   // |cmd| at/above → continuous thrust
        static RcsPulseState pX = RcsPulseState.Fresh, pY = RcsPulseState.Fresh, pZ = RcsPulseState.Fresh;
        static RcsPulseState pPitch = RcsPulseState.Fresh, pYaw = RcsPulseState.Fresh, pRoll = RcsPulseState.Fresh;
        // ⭐ INSTRUMENTATION (read-only, no behaviour change): the APPLIED post-pulse actuation actually written to
        // FlightCtrlState this tick, + whether the RCS attitude/translation pulse stage was active. Lets the recorder
        // show REQUESTED (AttitudePilot.Act*/CmdTrans*, pre-pulse) vs APPLIED, so delivered RCS firing is measured,
        // not inferred from pre-pulse controller demand (DS-ASC-004 verification, 2026-08-31).
        public static float AppliedPitch, AppliedYaw, AppliedRoll, AppliedTransX, AppliedTransY, AppliedTransZ;
        public static bool  PulseAttActive, PulseTransActive;
        static float Pulse(ref RcsPulseState st, float cmd, double dt)
        { return (float)RcsPulse.Step(ref st, cmd, dt, RcsPulseDeadband, RcsPulseMinOn, RcsPulseMinOff, RcsPulseFull); }

        void OnFlyByWire(FlightCtrlState st)
        {
            // Only take an axis when a controller is actively commanding it; otherwise leave the
            // player/idle in control. Pitch/yaw pointing is the direct gimbal loop (AttitudePilot).
            // pulse only at realtime / physics warp (on-rails HIGH warp freezes control anyway); and pulse
            // ATTITUDE only when the main engine is OFF — i.e. RCS is the attitude actuator (coast / rendezvous
            // / deorbit / entry / dock). During gimbal ascent (throttle on) attitude stays CONTINUOUS.
            bool pulse = UseRcsPulse && (TimeWarp.WarpMode != TimeWarp.Modes.HIGH || TimeWarp.CurrentRateIndex == 0);
            double dt = TimeWarp.fixedDeltaTime;
            bool rcsAtt = pulse && !(throttleOwned && cmdThrottle > 0.01);

            if (throttleOwned) st.mainThrottle = (float)cmdThrottle;
            if (transOwned)
            {
                if (pulse) { st.X = Pulse(ref pX, transX, dt); st.Y = Pulse(ref pY, transY, dt); st.Z = Pulse(ref pZ, transZ, dt); }
                else       { st.X = transX; st.Y = transY; st.Z = transZ; }
            }
            if (attitudeOwned)
            {
                if (rcsAtt) { st.pitch = Pulse(ref pPitch, cmdPitch, dt); st.yaw = Pulse(ref pYaw, cmdYaw, dt); }
                else        { st.pitch = cmdPitch; st.yaw = cmdYaw; }
            }
            if (attRollOwned) st.roll = rcsAtt ? Pulse(ref pRoll, cmdAttRoll, dt) : cmdAttRoll; // AttitudePilot roll damping
            if (rollOwned)    st.roll = rcsAtt ? Pulse(ref pRoll, cmdRoll, dt)    : cmdRoll;     // entry bank (mutually exclusive)

            // instrumentation snapshot: the APPLIED (post-pulse) actuation + pulse-stage state (read-only).
            AppliedPitch = st.pitch; AppliedYaw = st.yaw; AppliedRoll = st.roll;
            AppliedTransX = st.X; AppliedTransY = st.Y; AppliedTransZ = st.Z;
            PulseAttActive = (attitudeOwned || attRollOwned || rollOwned) && rcsAtt;
            PulseTransActive = transOwned && pulse;
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
                    SyncAuthority(v);
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
                    deorbitRescuePending = false; abortIsRecovery = false; AuthorityManager.Reset();
            MissionConductor.Reset();
                    return;
                }

                Bind(v);   // ensure our throttle hook is on the current vessel (follows handover)

                // A lone SEPARATED BOOSTER (the active vessel after a focus-switch) flies its own autonomous
                // recovery — the mission conductor tracks the Dragon, not this vehicle.
                if (BoosterControl.IsRecoverableBooster(v))
                {
                    BoosterControl.Tick(v);
                    SyncAuthority(v);
                    FlightLog.Sample(v);
                    return;
                }

                // otherwise: the Dragon / mission vessel. Keep the booster FSM IDLE — UNLESS a non-active booster
                // recovery is in progress (C2 Step-2): its FSM state lives in BoosterControl statics and is driven
                // from the booster's own OnFlyByWire, so it MUST survive the Dragon's frames. Resetting it every
                // tick would re-select a mode each frame → re-ignite the octaweb (violates one-ignition-per-mode).
                if (!MissionConductor.BoosterRecoveryActive) BoosterControl.Reset();

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
                        SyncAuthority(v);
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

                // 3a) the mission conductor: never run a live burn under warp (universal guard), and — opt-in —
                //     hand focus to a separated booster for a recovery segment (MissionConductor.AutoRecoverBooster).
                MissionConductor.Tick(v);

                // 3c) FDIR safety spine — observe-only (logs faults; acts only if FdirActing is on).
                TickFdir(v);

                // 3b) the clamp-release gate holds the hold-downs until full thrust is confirmed
                if (clampHeld) ClampGate(v);

                // 4) instrument (only while engaged — one CSV per autopilot flight)
                SyncAuthority(v);
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
            // one-shot deployables: solar/antenna out on a stable orbit, retracted before the return deorbit.
            DeployablesControl.Tick(v, CrewProcedureOps.ActivePhase, CrewProcedureOps.IsReturn);
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
                    DeployablesControl.Reset();   // re-arm the deploy/retract one-shots for a fresh mission
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
                if (LaunchAutoWarp && MissionConductor.AutoWarpEnabled) WarpTo(launchWindowUT - LaunchLeadS);
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
            else if (LaunchAutoWarp && MissionConductor.AutoWarpEnabled && TimeWarp.CurrentRateIndex == 0
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

        // ============================ CONTROL AUTHORITY (Phase 2) ============================
        // Publish WHO owns the vehicle into the shared AuthorityManager each tick, and expose it to the crew
        // screens. READ-ONLY w.r.t. the flight loop: SyncAuthority derives from the SAME latch/abort state
        // OnFlyByWire already acts on and writes only to the AuthorityManager model — it cannot change what
        // the vehicle does (this is the behaviour-preserving extraction; the Manual/Recovery command paths
        // route THROUGH the manager in Phase 7, under their own regression). CAMERA FOCUS IS NOT AN INPUT —
        // the mission owns authority, not the view (the dual-vessel rule).
        static bool abortIsRecovery;   // the current abort is a controlled deorbit-rescue, not a fault abort
        public static ControlMode MissionMode { get { return AuthorityManager.Dragon.Mode; } }
        public static ControlMode BoosterMode { get { return AuthorityManager.Booster.Mode; } }

        static void SyncAuthority(Vessel v)
        {
            VehicleAuthority d = AuthorityManager.Dragon, b = AuthorityManager.Booster;

            // The active vessel is a lone separated booster flying its own recovery via these latches → it IS
            // the booster slot; there is no crewed Dragon owning authority in this scene.
            if (v != null && BoosterControl.IsRecoverableBooster(v))
            {
                d.SetWhole(AuthSource.None);
                b.SetAutopilot(true, false, true, true);
                return;
            }

            // The crewed Dragon / mission vessel.
            if (aborting || abortPending || CrewProcedureOps.AbortActive)
                d.SetWhole(abortIsRecovery ? AuthSource.Recovery : AuthSource.Abort);
            else if (!CrewProcedureOps.Engaged)
                d.SetWhole(AuthSource.None);
            else
                d.SetAutopilot(throttleOwned, transOwned, attitudeOwned, attRollOwned || rollOwned);

            // A non-active booster running a PARALLEL recovery on its own OnFlyByWire (dual-vessel).
            if (MissionConductor.BoosterRecoveryActive) b.SetAutopilot(true, false, true, true);
            else b.SetWhole(AuthSource.None);
        }

        // ---- FDIR observe-only tick (tasks T2 + T2b) ----
        // Runs the pure Fdir at ~4 Hz on the live feeds and logs a rate-limited line on any tripped fault.
        // Acting (commanding the abort) is gated on FdirActing (default OFF) so a not-yet-flight-tuned monitor
        // can't false-abort a good flight. Resource + Thrust + Control + Stall are LIVE (honestly shaped by
        // pure/FdirFeeds.cs); TrajectoryDivergence stays nominal by design (see the field comment for why each).
        static void TickFdir(Vessel v)
        {
            try
            {
                fdirAccumS += Time.fixedDeltaTime;
                if (fdirAccumS < FdirTickS) return;
                double dt = fdirAccumS; fdirAccumS = 0.0;

                FdirInputs fi = new FdirInputs();
                fi.Valid = true; fi.Dt = dt;
                fi.Phase = CrewProcedureOps.ActivePhase;
                fi.GateHolding = CrewProcedureOps.AtGate;
                fi.Powered = CmdThrottle > 0.01
                             || (Math.Abs(CmdTransX) + Math.Abs(CmdTransY) + Math.Abs(CmdTransZ)) > 0.001;

                // THRUST SHORTFALL (T2b): delivered / expected over the commanded main engines. Suppressed while
                // the hold-downs are clamped (thrust is nominally ramping to the 99% release), and self-nominal on
                // a coast / Draco-only burn (no committed ModuleEngines) via FdirFeeds' guards.
                double actKn, expKn;
                MainEngineCommittedThrust(v, out actKn, out expKn);
                fi.ThrustDeliveredFrac = clampHeld ? 1.0
                    : FdirFeeds.ThrustDeliveredFrac(actKn, expKn, CmdThrottle);

                fi.TrajErrorM = 0.0;            // NOMINAL (honest): no uniform position-error residual — see the field note

                // CONVERGENCE STALL (T2b): the near-field closing rate, only while RendezvousControl is actively
                // closing (an intended coast leaves it nominal). Gated to outbound rendezvous so a stale value from
                // a past phase can't feed a later one.
                bool rvActive = fi.Phase == MissionPhase.Phasing && !CrewProcedureOps.IsReturn
                                && RendezvousControl.NearClosingActive;
                fi.PlanProgressRate = FdirFeeds.ClosingProgress(RendezvousControl.NearClosingRateMps, rvActive);

                // NO CONTROL SOLUTION (T2b): the no-authority tumble (see FdirFeeds.ControlLost). Only meaningful
                // while the attitude loop is actively holding — attitudeOwned is that gate.
                double bestAuthNm = Math.Max(AttitudePilot.CtrlTorquePitchNm,
                                    Math.Max(AttitudePilot.CtrlTorqueYawNm, AttitudePilot.CtrlTorqueRollNm));
                double spinRads = v.angularVelocity.magnitude;
                fi.ControlSolutionOk = !FdirFeeds.ControlLost(attitudeOwned, bestAuthNm, spinRads,
                    AttitudePilot.PointErrDeg, CtrlAuthFloorNm, CtrlTumbleRateRads, CtrlLostErrDeg);

                fi.ResourceMargin01 = RcsPropMargin(v);                 // LIVE
                fi.KosRadiusM = 0.0; fi.KosRangeM = 0.0; fi.CorridorOk = true;   // KOS abort owned by DockingControl

                FdirReport rep = Fdir.Update(ref fdirState, fi);
                lastFdirReport = rep;   // ⭐ R2: publish for the recorder (FlightLog.Sample writes it via PutFdir)
                if (rep.Fault != FaultKind.None)
                {
                    double now = Planetarium.GetUniversalTime();
                    if (now - lastFdirLogUT > 5.0)
                    {
                        lastFdirLogUT = now;
                        Debug.Log("[DragonScreen] FDIR " + (FdirActing ? "" : "(observe) ") + rep.Fault
                                  + " → " + rep.Response + (rep.Abort ? " [ABORT]" : "") + "  phase=" + fi.Phase
                                  + " resMargin=" + fi.ResourceMargin01.ToString("F2"));
                    }
                    if (FdirActing && rep.Abort) RequestAbort();
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] FDIR tick failed: " + e.Message); }
        }

        // Cache the RCS/Draco propellant resource ids from the live ModuleRCS modules (by capability, not name),
        // so the per-tick margin read is allocation-free. Rebuilt on each vessel Bind (handover).
        static void CacheRcsProps(Vessel v)
        {
            rcsPropIds.Clear();
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        ModuleRCS rcs = p.Modules[m] as ModuleRCS;
                        if (rcs == null || rcs.propellants == null) continue;
                        for (int k = 0; k < rcs.propellants.Count; k++)
                        {
                            int id = rcs.propellants[k].id;
                            if (!rcsPropIds.Contains(id)) rcsPropIds.Add(id);
                        }
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] RCS-prop cache failed: " + e.Message); }
        }

        // The worst (min) fraction among the Draco/RCS propellants on this vessel [0,1]; 1 if none/unknown.
        // This is the return fuel — full through ascent (no false trip), the true margin for rendezvous/deorbit.
        static double RcsPropMargin(Vessel v)
        {
            if (rcsPropIds.Count == 0) return 1.0;
            double worst = 1.0;
            try
            {
                for (int i = 0; i < rcsPropIds.Count; i++)
                {
                    double amt, max;
                    v.GetConnectedResourceTotals(rcsPropIds[i], out amt, out max, true);
                    if (max > 1.0) { double frac = amt / max; if (frac < worst) worst = frac; }
                }
            }
            catch { return 1.0; }
            return worst;
        }

        // Sum the COMMANDED main-engine thrust for the FDIR shortfall feed (T2b). actualKn = Σ finalThrust of the
        // operational commanded engines; expectedFullKn = Σ current-conditions FULL-throttle max of ALL commanded
        // (EngineIgnited) engines — a flamed-out-but-commanded engine still counts in expected but adds 0 to actual,
        // so its lost share drops the ratio (the honest engine-out signal). Uses the same maxFuelFlow·flowMultiplier·
        // Isp·g0 current-conditions max as Actuator.EngineThrust (vacuum maxThrust would read ~82% at sea level and
        // false-trip a healthy launch), folding in the per-engine thrustPercentage limiter so an intentionally
        // throttle-limited engine still reads healthy. Only ModuleEngines counted → a Draco-only (ModuleRCS) burn
        // yields 0/0 → FdirFeeds returns nominal (correct: not a main-engine burn). Runs at ~4 Hz, off the hot path.
        static void MainEngineCommittedThrust(Vessel v, out double actualKn, out double expectedFullKn)
        {
            actualKn = 0.0; expectedFullKn = 0.0;
            if (v == null) return;
            const double g0 = 9.80665;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null || !e.EngineIgnited) continue;   // only engines the autopilot has commanded ON
                    double isp = e.realIsp > 1f ? e.realIsp : 0.0;
                    double eMaxKn = e.maxFuelFlow * e.flowMultiplier * isp * g0;   // full-throttle current-conditions max (kN)
                    if (!(eMaxKn > 0.0)) eMaxKn = e.maxThrust;                     // fallback: static config max
                    eMaxKn *= Math.Max(e.thrustPercentage * 0.01, 0.0);           // fold in the per-engine limiter
                    expectedFullKn += eMaxKn;
                    if (e.isOperational) actualKn += e.finalThrust;               // flamed-out → adds 0 (drops the ratio)
                }
            }
        }

        // ⭐ DEORBIT RESCUE (the "DEORBIT NOW" / "WATER DEORBIT" panel buttons). A CONTROLLED deorbit-and-land,
        // not a fault abort: it reuses the abort machinery's DeorbitReturn engine (trunk jettison → g-limited
        // retrograde burn → shield-forward entry → chutes → touchdown) but FORCES that mode and suppresses the
        // klaxon/DON'T PANIC FX. landAnywhere = DEORBIT NOW (immediate, any safe site, gear after a land
        // touchdown); false = WATER DEORBIT (nearest open water, splashdown). Runs on the ACTIVE vessel, engaged
        // or not — so a stranded vessel just needs to be focused. As fast as possible while staying survivable.
        static bool deorbitRescuePending, deorbitRescueLandAnywhere;
        public static void RequestDeorbit(bool landAnywhere)
        {
            deorbitRescuePending = true; deorbitRescueLandAnywhere = landAnywhere; abortPending = true;
            abortIsRecovery = true;   // Phase 2: a controlled rescue shows as RECOVERY authority, not ABORT
        }

        static void UpdateAbort(Vessel v)
        {
            if (!aborting)
            {
                aborting = true;
                ReleaseThrottle(); ReleaseTranslation(); ReleaseRoll(); AttitudePilot.Reset();
                if (deorbitRescuePending)
                {
                    // controlled rescue: force DeorbitReturn with the land/water flag, no alarm FX.
                    abortFxSuppressed = true;
                    AbortControl.ForceDeorbit(deorbitRescueLandAnywhere);
                    deorbitRescuePending = false;
                    Debug.Log("[DragonScreen] ⭐ DEORBIT RESCUE engaging — "
                              + (deorbitRescueLandAnywhere ? "DEORBIT NOW (land anywhere safe)" : "WATER DEORBIT"));
                }
                else
                {
                    abortFxSuppressed = false;   // a fresh fault abort re-arms the alarm
                    Debug.Log("[DragonScreen] ⛔ ABORT — regime-aware response engaging.");
                    AbortControl.Reset();
                }
            }

            AbortControl.Tick(v);   // fly the (forced or decided) procedure to a safe touchdown
            UpdateAbortFx(v);       // alarm klaxon + red IVA-light strobe (suppressed for a rescue / at splashdown)
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
