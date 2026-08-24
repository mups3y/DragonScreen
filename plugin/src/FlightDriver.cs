/*
 * DragonScreen - FlightDriver
 *
 * GLUE. The heartbeat for everything that must keep running whether or not anyone is looking at a
 * screen: the autopilot, the booster recovery, the console's armed burns, and the recorder.
 *
 * ---- ⛔ WHY THIS FILE EXISTS: THE AUTOPILOT WAS LIVING INSIDE A DISPLAY WIDGET ----
 * All four used to be ticked from `ScreenPainter.Update()`. ScreenPainter is a MonoBehaviour on a
 * GameObject owned by DragonScreenMonitor, which is an `InternalModule` on the Dragon's IVA - and
 * `DragonScreenMonitor.OnDestroy` explicitly destroys that GameObject when the IVA is torn down.
 *
 * KSP despawns the IVA of a vessel that is not active (`Part.DespawnIVA`). So the exact call that
 * starts a booster recovery -
 *
 *      FlightGlobals.ForceSetActiveVessel(booster)
 *
 * - made the Dragon inactive, which despawned its IVA, which destroyed the painter, which removed
 * the ONLY caller of BoosterRecovery.Tick(). The booster took focus and then fell completely
 * unguided: no boostback, no entry burn, no landing burn, and no recording of any of it. Worse,
 * Finish() could never run either, so focus never returned to the upper stage and `Active` stayed
 * true forever - which meant even a rebuilt painter would hit the early return in AutoPilot.Tick
 * and do nothing for the rest of the session.
 *
 * That is the real reason "booster recovery has never run once", and it would have stayed true
 * after every constant in Landing.cs was made correct. It is not a tuning bug; it is flight
 * software scoped to a screen.
 *
 * ---- THE FIX IS SCOPE, AND IT IS THE STANDARD ONE ----
 * MAS solves the same problem the same way: `MASFlightComputer` is a PartModule and
 * `MASVesselComputer` a vessel-level MonoBehaviour, never an InternalModule - only the display
 * lives in the prop. A `[KSPAddon(Startup.Flight, false)]` is the strongest version of that: it is
 * created when the flight scene loads and destroyed when it unloads, so it is indifferent to which
 * vessel is active, which camera mode is up, and whether any IVA exists at all.
 *
 * `false` means "not once per game" - recreate it on every entry to the flight scene, which is what
 * makes the statics get re-validated rather than carried across a revert.
 *
 * The painter still draws. It no longer flies.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FlightDriver : MonoBehaviour
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>realtime of the last map-trajectory refresh, to throttle it to ~2 Hz.</summary>
        private float lastMapUpdate;

        public void Start()
        {
            // Discover the [Tunable] fields, dump the reference catalogue, apply any overrides. Once
            // per flight-scene entry, before anything ticks, so the first frame already runs on the
            // tuned values rather than a frame of defaults.
            Tuning.Build();
            MapTrajectory.Start();      // the map-view re-entry overlay (replaces the Trajectories add-on)
            FlightTrajectory.Start();   // the same overlay, projected over the flight view
            Debug.Log(Tag + "flight driver up - autopilot, recovery and recorder now tick "
                          + "independently of the IVA");
        }

        public void Update()
        {
            // Live tuning: re-read PluginData/tuning.cfg if it changed. Throttled to ~1 s internally,
            // so this is a cheap call every frame and a disk touch only once a second.
            Tuning.Poll();

            // ---- AUTO-RESUME THE ASCENT AFTER A RECOVERY HANDBACK. ----
            // The booster-recovery handback tears down and rebuilds the flight scene; OnDestroy
            // disengaged the ascent mid-climb and the crew had to restart the sequence. If that
            // teardown flagged a resume, bring the ascent straight back - but only when the active
            // vessel is genuinely still climbing (airborne, periapsis below the atmosphere), so a
            // finished mission is never re-launched. `AutoPilot.Engage` refuses from a stable orbit
            // anyway, which is the belt to this braces.
            if (AutoPilot.ResumeAscent && !AutoPilot.Engaged)
            {
                Vessel av = FlightGlobals.ActiveVessel;
                bool climbing = av != null && av.orbit != null && av.mainBody != null
                                && av.situation != Vessel.Situations.PRELAUNCH
                                && av.situation != Vessel.Situations.LANDED
                                && av.orbit.PeA < av.mainBody.atmosphereDepth;
                if (climbing)
                {
                    AutoPilot.ResumeAscent = false;
                    AutoPilot.Engage();
                    Debug.Log(Tag + "ascent auto-sequence resumed after the recovery handback");
                }
                else if (av != null && av.orbit != null && av.mainBody != null
                         && av.orbit.PeA >= av.mainBody.atmosphereDepth)
                {
                    AutoPilot.ResumeAscent = false;   // already in orbit - nothing to resume
                }
            }

            // Order matters only in that the recorder samples AFTER the guidance has run, so a row
            // carries this frame's command rather than the previous one's.
            FlightCommands.Tick();
            BargeWaypoint.Ensure();   // cosmetic: drop the droneship map/navball marker once, when able
            // The conductor runs BEFORE the controllers it supervises: it may engage the next phase this
            // frame, and that controller then ticks in the same frame rather than a frame late.
            AutoSequence.Tick();
            AutoPilot.Tick();
            // The node executor before the things that plan burns, so a burn armed this frame is
            // flown from the next one rather than sitting a frame behind its own ignition time.
            // Sample drag on BOTH vehicles before anything reads a prediction. The estimate is
            // per-vessel and only improves while it is being measured, so it is taken every tick
            // rather than when someone happens to ask.
            ImpactPredictor.Sample(AutoPilot.AscentVessel);
            ImpactPredictor.Sample(BoosterRecovery.BoosterVessel);
            // And the returning capsule, which is neither of those by the time it comes home: the
            // autopilot let go at insertion and the booster is long since down. Without this its drag
            // is never measured and every entry prediction quietly falls back to a vacuum solve.
            ImpactPredictor.Sample(DeorbitOps.Vehicle);
            ImpactPredictor.Sample(EntryOps.Vehicle);

            // ---- ⛔ THE RECORDER STARTS HERE, NOT AT LAUNCH. ----
            // `FlightRecorder.Start` had exactly one caller: `AutoPilot` engaging for a launch. So a
            // flight scene entered any other way was NEVER RECORDED - and that is every return from
            // orbit. On 2026-08-11 the crew flew the whole de-orbit sequence and there is no CSV of
            // it at all; the only evidence is a dozen log lines. The recorder exists so that a
            // failure can be diagnosed from data, and the phases most likely to fail were the ones
            // it was not watching.
            if (!FlightRecorder.Recording) FlightRecorder.Start(FlightGlobals.ActiveVessel);

            NodeExecutor.Tick();
            StationApproach.Tick();
            DockingOps.Tick();
            DockedRefuel.Tick();          // fill the capsule the whole time it is berthed, not just at undock
            UndockOps.Tick();
            UndockPush.Tick();            // a manual undock still gets the retro push + shroud close

            // ⛔ LAST, AND UNCONDITIONALLY. The chutes must not depend on any sequence having been
            // started - see ChuteGuard. A crew flying the entry by hand had none deploy at all.
            ChuteGuard.Tick();
            PhaseDownOps.Tick();
            DeorbitOps.Tick();
            EntryOps.Tick();
            FlightRecorder.Tick();

            // ---- THE WATCH RUNS LAST, AND OUTSIDE EVERYTHING. ----
            // Deliberately after every controller and outside all of them: a controller that throws
            // and detaches must not take the monitor with it, which is the exact failure mode the
            // monitor exists to report. It owns no actuator - see its header.
            FlightMonitor.Tick();

            // ---- PREDICTED-IMPACT TRAJECTORY (map AND flight view; replaces the Trajectories add-on). ----
            // TWO PROFILES, selected by which vehicle is coming down (user 2026-08-24):
            //   * the CREW DRAGON return - EntryOps/DeorbitOps engaged, flown on the capsule's KNOWN
            //     ballistic coefficient (CapsuleBcKgM2), aimed at the splashdown target;
            //   * the BOOSTER recovery - BoosterRecovery.Active, flown on the booster's LIVE-MEASURED
            //     drag (bcOverride 0 -> use the measured bc), aimed at the droneship.
            // The path integration is the same cost the guidance already pays and is now consumed by
            // BOTH the map overlay and the in-flight overlay, so it runs whenever a descent is being
            // flown - someone is always looking at one view or the other. Throttled to ~2 Hz.
            {
                float now = Time.realtimeSinceStartup;
                if (now - lastMapUpdate > 0.5f)
                {
                    lastMapUpdate = now;
                    Vessel rv = null; double tlat = 0.0, tlon = 0.0; double bcov = 0.0;
                    if (EntryOps.Engaged && EntryOps.Vehicle != null)
                    { rv = EntryOps.Vehicle; tlat = EntryOps.TargetLatDeg; tlon = EntryOps.TargetLonDeg;
                      bcov = EntryGuidance.CapsuleBcKgM2; }
                    else if (DeorbitOps.Engaged && DeorbitOps.Vehicle != null)
                    { rv = DeorbitOps.Vehicle; tlat = DeorbitOps.TargetLatDeg; tlon = DeorbitOps.TargetLonDeg;
                      bcov = EntryGuidance.CapsuleBcKgM2; }
                    else if (BoosterRecovery.Active && BoosterRecovery.BoosterVessel != null
                             && !BoosterRecovery.BoosterVessel.packed)
                    {
                        rv = BoosterRecovery.BoosterVessel;
                        if (BoosterRecovery.HavePad)
                        { tlat = BoosterRecovery.PadLat; tlon = BoosterRecovery.PadLon; }
                        else
                        { tlat = BoosterRecovery.DroneshipEarthLatDeg; tlon = BoosterRecovery.DroneshipEarthLonDeg; }
                        bcov = 0.0;   // booster flies its OWN measured drag, not the capsule's known bc
                    }

                    if (rv != null)
                        ImpactPredictor.UpdateMapTrajectory(rv, bcov, tlat, tlon);
                    else
                        ImpactPredictor.MapValid = false;
                }
            }
            MapTrajectory.Update();      // draws in map view
            FlightTrajectory.Update();   // draws the same path + target X over the flight view
        }

        public void OnDestroy()
        {
            // ---- REMEMBER TO COME BACK IF THE ASCENT WAS STILL FLYING. ----
            // The booster-recovery handback rebuilds the flight scene and fires this OnDestroy, which
            // disengages the ascent mid-climb. If we were flying an ascent that had not finished, flag
            // it so the rebuilt driver picks it straight back up (see Update). Set BEFORE Disengage.
            if (AutoPilot.Engaged && AutoPilot.Phase != AscentPhase.Done
                && AutoPilot.Phase != AscentPhase.Idle)
                AutoPilot.ResumeAscent = true;

            // Leaving the flight scene ends the flight. Close the file rather than leaving the last
            // rows buffered - the flights worth reading are the ones that end unexpectedly.
            FlightRecorder.Stop("left the flight scene");

            MapTrajectory.Destroy();      // tear down the map camera component and its meshes
            FlightTrajectory.Destroy();   // and the flight-view overlay component + material

            // ---- AND CLEAR THE STATICS HERE, WHICH IS THE HONEST PLACE FOR IT ----
            // A revert or a scene change is what invalidates them - not a camera move, which is what
            // AutoPilot's old persistentId watch was actually detecting. Everything below holds a
            // reference to a vessel that is about to stop existing.
            try
            {
                AutoPilot.Disengage("left the flight scene");
                BoosterRecovery.Reset();
                StationApproach.Reset();
                DirectApproachOps.Reset();
                NodeExecutor.Reset();
                DockingOps.Reset();
                DockedRefuel.Reset();
                UndockPush.Reset();
                UndockOps.Reset();
            ChuteGuard.Reset();
                PhaseDownOps.Reset();
                DeorbitOps.Reset();
                EntryOps.Reset();
                ImpactPredictor.Reset();
                FlightMonitor.Reset();
                VehicleCheck.Reset();
            }
            catch (Exception e)
            {
                // The scene is being torn down; a throw here would be logged against nothing useful.
                Debug.LogWarning(Tag + "cleanup on scene exit: " + e.Message);
            }
        }
    }
}
