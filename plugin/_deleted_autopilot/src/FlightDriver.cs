// DragonScreen - FlightDriver
// ---- ⛔ WHY THIS FILE EXISTS: THE AUTOPILOT WAS LIVING INSIDE A DISPLAY WIDGET ----
// ---- THE FIX IS SCOPE, AND IT IS THE STANDARD ONE ----
using System;
using UnityEngine;

namespace DragonScreen
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FlightDriver : MonoBehaviour
    {
        private const string Tag = "[DragonScreen] ";

        private float lastMapUpdate;

        public void Start()
        {
            Tuning.Build();
            MapTrajectory.Start();
            FlightTrajectory.Start();
            Debug.Log(Tag + "flight driver up - autopilot, recovery and recorder now tick "
                          + "independently of the IVA");
        }

        public void Update()
        {
            Tuning.Poll();

            CraftDump.Auto();

            // ---- AUTO-RESUME THE ASCENT AFTER A RECOVERY HANDBACK. ----
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
                    AutoPilot.ResumeAscent = false;
                }
            }

            FlightCommands.Tick();
            BargeWaypoint.Ensure();
            CrewProcedureOps.Tick();
            AbortResponder.Tick();
            AutoPilot.Tick();
            ImpactPredictor.Sample(AutoPilot.AscentVessel);
            ImpactPredictor.Sample(BoosterRecovery.BoosterVessel);
            ImpactPredictor.Sample(DeorbitOps.Vehicle);
            ImpactPredictor.Sample(EntryOps.Vehicle);

            // ---- ⛔ THE RECORDER STARTS HERE, NOT AT LAUNCH. ----
            if (!FlightRecorder.Recording) FlightRecorder.Start(FlightGlobals.ActiveVessel);

            NodeExecutor.Tick();
            StationApproach.Tick();
            DockingOps.Tick();
            DockedRefuel.Tick();
            UndockOps.Tick();
            UndockPush.Tick();

            ChuteGuard.Tick();
            RendezvousFdir.Tick();
            ReturnFallback.Tick();
            PhaseDownOps.Tick();
            DeorbitOps.Tick();
            EntryOps.Tick();
            FlightRecorder.Tick();

            // ---- THE WATCH RUNS LAST, AND OUTSIDE EVERYTHING. ----
            FlightMonitor.Tick();

            // ---- PREDICTED-IMPACT TRAJECTORY (map AND flight view; replaces the Trajectories add-on). ----
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
                        bcov = 0.0;
                    }

                    if (rv != null)
                        ImpactPredictor.UpdateMapTrajectory(rv, bcov, tlat, tlon);
                    else
                        ImpactPredictor.MapValid = false;
                }
            }
            MapTrajectory.Update();
            FlightTrajectory.Update();
        }

        public void OnDestroy()
        {
            // ---- REMEMBER TO COME BACK IF THE ASCENT WAS STILL FLYING. ----
            if (AutoPilot.Engaged && AutoPilot.Phase != AscentPhase.Done
                && AutoPilot.Phase != AscentPhase.Idle)
                AutoPilot.ResumeAscent = true;

            FlightRecorder.Stop("left the flight scene");

            MapTrajectory.Destroy();
            FlightTrajectory.Destroy();

            // ---- AND CLEAR THE STATICS HERE, WHICH IS THE HONEST PLACE FOR IT ----
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
                CraftDump.Reset();
                RendezvousFdir.Reset();
                ReturnFallback.Reset();
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "cleanup on scene exit: " + e.Message);
            }
        }
    }
}
