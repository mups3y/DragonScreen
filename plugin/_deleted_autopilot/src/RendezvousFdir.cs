// DragonScreen - RendezvousFdir
// ---- THE FAULT THIS ANSWERS ----
// ---- ⛔ IT MUST NOT FIGHT AN INTENTIONAL HOLD ----
using UnityEngine;

namespace DragonScreen
{
    public static class RendezvousFdir
    {
        private const string Tag = "[DragonScreen] ";

        [Tunable] public static bool Enabled = true;
        [Tunable] public static double RetryStallS = 90.0;
        [Tunable] public static double AbortStallS = 300.0;

        // ---- state exposed for the recorder ----
        public static HealthVerdict Verdict { get { return state.Verdict; } }
        public static double StallS { get { return state.StallS; } }
        public static int Replans { get; private set; }
        public static string LastAction = "-";

        private static RvProgressState state;
        private static bool replanIssued;

        public static void Reset()
        {
            state = RendezvousProgress.Fresh();
            replanIssued = false;
            Replans = 0;
            LastAction = "-";
        }

        public static void Tick()
        {
            Vessel v = FlightGlobals.ActiveVessel;

            bool active = Enabled
                && v != null
                && NamedRendezvousOps.Engaged
                && !WaypointApproachOps.Engaged
                && !DockedSide.Docked(v)
                && !DeorbitOps.Engaged && !EntryOps.Engaged
                && !ReturnFallback.Triggered
                && !CrewProcedureOps.CrewActionNeeded();

            RvProgressSample s;
            s.Engaged = active;
            s.WarpActive = TimeWarp.CurrentRateIndex > 0;
            s.NodeActive = NodeExecutor.Active;
            s.RemainingDvMps = NodeExecutor.RemainingDvMps;
            s.PointingErrorDeg = NodeExecutor.PointingErrorDeg;
            s.RangeM = NamedRendezvousOps.RangeKm * 1000.0;
            s.LegIndex = (int)NamedRendezvousOps.Leg;

            RvProgressCfg cfg = RendezvousProgress.Default();
            cfg.RetryStallS = RetryStallS;
            cfg.AbortStallS = AbortStallS;
            state = RendezvousProgress.Step(state, s, cfg, TimeWarp.fixedDeltaTime);

            if (!active) { replanIssued = false; return; }

            Recovery r = FaultResponse.Decide(RendezvousProgress.Kind(state), state.Verdict,
                                              FaultDomain.Rendezvous);
            switch (r)
            {
                case Recovery.Replan:
                    if (!replanIssued)
                    {
                        replanIssued = true;
                        Replans++;
                        LastAction = "REPLAN";
                        Debug.LogWarning(Tag + "FDIR: rendezvous FROZEN " + state.StallS.ToString("F0")
                            + " s - RE-PLANNING (aborting the stuck node so the sequence recomputes).");
                        ScreenMessages.PostScreenMessage("FDIR: rendezvous stalled - re-planning",
                                                         6f, ScreenMessageStyle.UPPER_CENTER);
                        if (NodeExecutor.Active) NodeExecutor.Abort("FDIR re-plan - rendezvous frozen");
                    }
                    break;

                case Recovery.Downmode:
                    LastAction = "ABORT-TO-HOME";
                    Debug.LogWarning(Tag + "FDIR: rendezvous FROZEN " + state.StallS.ToString("F0")
                        + " s and re-plan did not recover - ABORT-TO-HOME (coming home).");
                    ReturnFallback.AbortToHome(v,
                        "rendezvous frozen (FDIR) - re-plan did not recover, coming home for re-entry data");
                    break;

                default:
                    replanIssued = false;
                    if (state.Verdict == HealthVerdict.Nominal) LastAction = "-";
                    break;
            }
        }
    }
}
