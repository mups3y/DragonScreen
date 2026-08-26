/*
 * DragonScreen - RendezvousFdir
 *
 * GLUE. The RESPONDER that makes the rendezvous self-healing - the Layer-3 "notice a failing plan and act
 * on it yourself" piece a true autopilot needs (docs/LAYER3_AUTONOMY_PLAN.md, P2: let FaultResponse DRIVE
 * a recovery, not just observe). It watches RendezvousProgress (the pure stall detector) and drives the
 * FaultResponse ladder from the verdict:
 *
 *      DEGRADED (frozen ~90 s)   -> REPLAN     abort the stuck node so the sequence recomputes the leg
 *      FAILED   (frozen ~300 s)  -> DOWNMODE   abort the rendezvous and come home (ReturnFallback)
 *
 * ---- THE FAULT THIS ANSWERS ----
 * flight_0826_014654: the CLOSE burn could not reorient (the capsule was capped at the ascent slew rate),
 * the orbit sat frozen, and a HUMAN watched it do nothing and pressed CANCEL. That is precisely the job an
 * autopilot must do itself. The slew-rate fix removes THAT cause; this is the safety net for any future or
 * unknown cause - a hung node, a solver that will not converge, an off insertion the climb cannot close.
 *
 * ---- ⛔ IT MUST NOT FIGHT AN INTENTIONAL HOLD ----
 * A rendezvous legitimately STOPS at the crew GO gates and the L-approach waypoint holds - station-keeping
 * that looks exactly like a freeze. So this only watches the AUTONOMOUS stretch (NamedRendezvousOps: the
 * climb + CW terminal), and pauses whenever a crew action is pending (CrewProcedureOps.CrewActionNeeded)
 * or the L-approach has taken over (WaypointApproachOps.Engaged). A crew hold is healthy, not a fault.
 *
 * Ticked before ReturnFallback / DeorbitOps in FlightDriver so a fired abort flies from the same frame.
 */
using UnityEngine;

namespace DragonScreen
{
    public static class RendezvousFdir
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Master switch for the rendezvous fault responder. [Tunable].</summary>
        [Tunable] public static bool Enabled = true;
        /// <summary>Seconds frozen before the first, local recovery (re-plan the stuck node). [Tunable].</summary>
        [Tunable] public static double RetryStallS = 90.0;
        /// <summary>Seconds frozen before escalating to abort-to-home. [Tunable].</summary>
        [Tunable] public static double AbortStallS = 300.0;

        // ---- state exposed for the recorder ----
        public static HealthVerdict Verdict { get { return state.Verdict; } }
        public static double StallS { get { return state.StallS; } }
        public static int Replans { get; private set; }
        /// <summary>The last recovery this responder took, for the recorder / logs.</summary>
        public static string LastAction = "-";

        private static RvProgressState state;
        private static bool replanIssued;   // one re-plan per stall episode, then escalate

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

            // Watch ONLY the autonomous climb + CW terminal, and never while a crew hold / the L-approach /
            // the way home is in charge - those "stops" are intentional, not frozen faults.
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
                    // First, local recovery: abort the stuck node so NamedRendezvousOps recomputes the leg
                    // from the measured orbit next idle tick. One re-plan per episode; if it does not
                    // restore progress the stall clock climbs to the abort threshold below.
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
                    // The re-plan did not recover it: the crew's safety over the docking objective. Abandon
                    // the rendezvous and come home - the same abort-to-home ReturnFallback flies, now
                    // triggered by the fault itself instead of a human noticing.
                    LastAction = "ABORT-TO-HOME";
                    Debug.LogWarning(Tag + "FDIR: rendezvous FROZEN " + state.StallS.ToString("F0")
                        + " s and re-plan did not recover - ABORT-TO-HOME (coming home).");
                    ReturnFallback.AbortToHome(v,
                        "rendezvous frozen (FDIR) - re-plan did not recover, coming home for re-entry data");
                    break;

                default:
                    // Nominal / no fault: clear the one-shot latch so a LATER episode can re-plan again.
                    replanIssued = false;
                    if (state.Verdict == HealthVerdict.Nominal) LastAction = "-";
                    break;
            }
        }
    }
}
