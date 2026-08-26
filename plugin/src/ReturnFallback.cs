/*
 * DragonScreen - ReturnFallback
 *
 * GLUE. The autopilot's "always have a way home" safety net (user 2026-08-26). If the rendezvous does
 * not work - it never closes, or it burns down toward the propellant a return needs - the autopilot
 * ABANDONS it and de-orbits, so instead of stranding a capsule in orbit we at least come home and get
 * the re-entry data. This is a real Layer-3 DOWNMODE: give up the primary objective (dock) to save the
 * mission (return safely), which is exactly what an on-orbit fault response should do.
 *
 * ---- AND THE RETURN IT FLIES IS A STEERING TEST (user 2026-08-26) ----
 * Coming home to land ANYWHERE means there is no target to protect, so the entry is free to exercise the
 * full pitch/yaw steering envelope, fully instrumented (EntryHeat + the he_ recorder block), so the
 * autopilot can LEARN how RO re-entry steering actually behaves - the cross-range and range authority,
 * and the heating/ablator cost of using it. The fallback turns EntryOps.SteeringTest on when it fires.
 *
 * ---- TWO TRIGGERS, BOTH CONSERVATIVE ----
 *   PROPELLANT FLOOR - the return propellant has fallen to what a de-orbit + entry needs. Come home NOW,
 *                      while ReturnBudget.ReturnAllowed still passes, rather than after it is too late.
 *   TIMEOUT          - the rendezvous has been running this long without docking. Whatever the cause
 *                      (a knife-edge burn trigger, an off insertion), it is not going to close; return.
 * Both [Tunable]. It only ever acts on the OUTBOUND leg, in orbit, not docked - never on the way home.
 */
using UnityEngine;

namespace DragonScreen
{
    public static class ReturnFallback
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Return propellant fraction (DockedSide.ReturnFraction) at/below which we must come
        /// home while we still can. Conservative - a working rendezvous uses far less than this.</summary>
        [Tunable] public static double AbandonReturnFrac = 0.40;
        /// <summary>Game-hours the rendezvous may run without docking before it is abandoned. Generous -
        /// a real phasing rendezvous closes in ~1-3 h; this only fires on one that will not close.</summary>
        [Tunable] public static double AbandonAfterHours = 6.0;
        /// <summary>Master switch - the whole safety net can be disabled from tuning.cfg.</summary>
        [Tunable] public static bool Enabled = true;

        public static bool Triggered { get; private set; }
        public static string Note = "-";
        private static double rendezvousStartedAt = -1.0;

        public static void Reset()
        {
            Triggered = false; rendezvousStartedAt = -1.0; Note = "-";
            EntryOps.SteeringTest = false;   // a fresh flight is not a steering test until the fallback fires
        }

        public static void Tick()
        {
            if (!Enabled || Triggered) return;
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            // Only the OUTBOUND leg, in a stable orbit, not docked, not already coming home.
            if (CrewProcedureOps.ReturnArmed) { rendezvousStartedAt = -1.0; return; }
            if (DockedSide.Docked(v)) { rendezvousStartedAt = -1.0; return; }
            if (DeorbitOps.Engaged || EntryOps.Engaged) return;   // already on the way down
            if (v.orbit == null || v.mainBody == null || v.orbit.PeA < v.mainBody.atmosphereDepth) return;

            bool closing = StationApproach.Engaged || WaypointApproachOps.Engaged
                        || DirectApproachOps.Engaged || DockingOps.Engaged;

            // ---- trigger 1: the return propellant has reached the floor. ----
            double rf = DockedSide.ReturnFraction(v);
            if (rf >= 0.0 && rf < AbandonReturnFrac)
            {
                Fire(v, "return propellant at the floor (" + (rf * 100.0).ToString("F0")
                        + "% <= " + (AbandonReturnFrac * 100.0).ToString("F0") + "%)");
                return;
            }

            // ---- trigger 2: the rendezvous has run too long without closing. ----
            if (closing)
            {
                double now = Planetarium.GetUniversalTime();
                if (rendezvousStartedAt < 0.0) rendezvousStartedAt = now;
                if (now - rendezvousStartedAt > AbandonAfterHours * 3600.0)
                    Fire(v, "rendezvous did not close in " + AbandonAfterHours.ToString("F1")
                            + " h - returning for re-entry data");
            }
            else rendezvousStartedAt = -1.0;   // not currently trying; do not accumulate the clock
        }

        /// <summary>
        /// Public abort-to-home entry point, shared by this file's own propellant/timeout triggers AND
        /// the Layer-3 RendezvousFdir when it confirms a FROZEN rendezvous (a stuck plan the crew would
        /// otherwise have to cancel by hand). Idempotent - the Triggered latch means a second caller (or
        /// ReturnFallback.Tick) does nothing once one has fired.
        /// </summary>
        public static void AbortToHome(Vessel v, string why)
        {
            if (Triggered || v == null) return;
            Fire(v, why);
        }

        private static void Fire(Vessel v, string why)
        {
            Triggered = true;
            Note = why;
            Debug.LogWarning(Tag + "RETURN FALLBACK - " + why
                             + ". Abandoning the rendezvous and de-orbiting; this entry is a STEERING TEST.");
            ScreenMessages.PostScreenMessage("RETURN FALLBACK - coming home (re-entry steering test)",
                                             8f, ScreenMessageStyle.UPPER_CENTER);

            // Stop everything trying to rendezvous, and the crew conductor.
            if (StationApproach.Engaged)     StationApproach.Disengage("return fallback");
            if (WaypointApproachOps.Engaged) WaypointApproachOps.Disengage("return fallback");
            if (DirectApproachOps.Engaged)   DirectApproachOps.Disengage("return fallback");
            if (DockingOps.Engaged)          DockingOps.Reset();
            if (CrewProcedureOps.Engaged)    CrewProcedureOps.Disengage("return fallback");

            // This entry characterises the steering envelope - land anywhere, learn everything.
            EntryOps.SteeringTest = true;

            // Come home. DeorbitOps de-orbits toward the splashdown site; from an off phase it may miss,
            // but that is the point - we land somewhere survivable and get the entry data.
            if (!DeorbitOps.Engaged && !EntryOps.Engaged) DeorbitOps.Engage();
        }
    }
}
