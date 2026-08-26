// DragonScreen - ReturnFallback
// ---- AND THE RETURN IT FLIES IS A STEERING TEST (user 2026-08-26) ----
// ---- TWO TRIGGERS, BOTH CONSERVATIVE ----
using UnityEngine;

namespace DragonScreen
{
    public static class ReturnFallback
    {
        private const string Tag = "[DragonScreen] ";

        [Tunable] public static double AbandonReturnFrac = 0.40;
        [Tunable] public static double AbandonAfterHours = 6.0;
        [Tunable] public static bool Enabled = true;

        public static bool Triggered { get; private set; }
        public static string Note = "-";
        private static double rendezvousStartedAt = -1.0;

        public static void Reset()
        {
            Triggered = false; rendezvousStartedAt = -1.0; Note = "-";
            EntryOps.SteeringTest = false;
        }

        public static void Tick()
        {
            if (!Enabled || Triggered) return;
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            if (CrewProcedureOps.ReturnArmed) { rendezvousStartedAt = -1.0; return; }
            if (DockedSide.Docked(v)) { rendezvousStartedAt = -1.0; return; }
            if (DeorbitOps.Engaged || EntryOps.Engaged) return;
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
            else rendezvousStartedAt = -1.0;
        }

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

            if (StationApproach.Engaged)     StationApproach.Disengage("return fallback");
            if (WaypointApproachOps.Engaged) WaypointApproachOps.Disengage("return fallback");
            if (DirectApproachOps.Engaged)   DirectApproachOps.Disengage("return fallback");
            if (DockingOps.Engaged)          DockingOps.Reset();
            if (CrewProcedureOps.Engaged)    CrewProcedureOps.Disengage("return fallback");

            EntryOps.SteeringTest = true;

            if (!DeorbitOps.Engaged && !EntryOps.Engaged) DeorbitOps.Engage();
        }
    }
}
