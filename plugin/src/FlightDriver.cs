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

        public void Start()
        {
            Debug.Log(Tag + "flight driver up - autopilot, recovery and recorder now tick "
                          + "independently of the IVA");
        }

        public void Update()
        {
            // Order matters only in that the recorder samples AFTER the guidance has run, so a row
            // carries this frame's command rather than the previous one's.
            FlightCommands.Tick();
            AutoPilot.Tick();
            // The node executor before the things that plan burns, so a burn armed this frame is
            // flown from the next one rather than sitting a frame behind its own ignition time.
            // Sample drag on BOTH vehicles before anything reads a prediction. The estimate is
            // per-vessel and only improves while it is being measured, so it is taken every tick
            // rather than when someone happens to ask.
            ImpactPredictor.Sample(AutoPilot.AscentVessel);
            ImpactPredictor.Sample(BoosterRecovery.BoosterVessel);

            NodeExecutor.Tick();
            StationApproach.Tick();
            DockingOps.Tick();
            UndockOps.Tick();
            FlightRecorder.Tick();
        }

        public void OnDestroy()
        {
            // Leaving the flight scene ends the flight. Close the file rather than leaving the last
            // rows buffered - the flights worth reading are the ones that end unexpectedly.
            FlightRecorder.Stop("left the flight scene");

            // ---- AND CLEAR THE STATICS HERE, WHICH IS THE HONEST PLACE FOR IT ----
            // A revert or a scene change is what invalidates them - not a camera move, which is what
            // AutoPilot's old persistentId watch was actually detecting. Everything below holds a
            // reference to a vessel that is about to stop existing.
            try
            {
                AutoPilot.Disengage("left the flight scene");
                BoosterRecovery.Reset();
                StationApproach.Reset();
                NodeExecutor.Reset();
                DockingOps.Reset();
                UndockOps.Reset();
                ImpactPredictor.Reset();
            }
            catch (Exception e)
            {
                // The scene is being torn down; a throw here would be logged against nothing useful.
                Debug.LogWarning(Tag + "cleanup on scene exit: " + e.Message);
            }
        }
    }
}
