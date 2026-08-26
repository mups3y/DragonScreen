// DragonScreen - UndockPush
using UnityEngine;

namespace DragonScreen
{
    public static class UndockPush
    {
        private const string Tag = "[DragonScreen] ";

        [Tunable] public static double PushRateMps = 2.0;
        [Tunable] public static double PushClearM = 60.0;
        [Tunable] public static double PushMaxS = 8.0;

        private static bool wasDocked, active, shroudDone;
        private static double startedAt;

        public static void Tick()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) { wasDocked = false; return; }

            bool docked = DockedSide.Docked(v);
            if (wasDocked && !docked && !UndockOps.Engaged)
            {
                active = true; shroudDone = false;
                startedAt = Planetarium.GetUniversalTime();
                Debug.Log(Tag + "undocked - auto retro-push to open the gap, and closing the shroud");
            }
            wasDocked = docked;
            if (!active) return;

            if (docked) { Stop(v); return; }
            Vessel stn = StationApproach.Station;
            if (stn == null) stn = StationApproach.Find();
            if (stn == null) { Stop(v); return; }

            if (!shroudDone) { DockShroud.Close(v); shroudDone = true; }

            double now = Planetarium.GetUniversalTime();
            Vector3d away = (v.CoM - stn.CoM).normalized;
            double opening = Vector3d.Dot(v.obt_velocity - stn.obt_velocity, away);
            double sep = Vector3d.Distance(v.CoM, stn.CoM);

            if (opening >= PushRateMps || sep >= PushClearM || now - startedAt > PushMaxS)
            {
                Debug.Log(Tag + "retro-push done - opening at " + opening.ToString("F2") + " m/s, "
                          + sep.ToString("F0") + " m clear");
                Stop(v);
                return;
            }

            if (!v.ActionGroups[KSPActionGroup.RCS])
                v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            Vector3d nose = v.ReferenceTransform.up;
            double push = (Vector3d.Dot(nose, away) >= 0.0) ? 1.0 : -1.0;
            AttitudeController.Ascent.SteerTo(v, nose, Vector3d.zero);
            AttitudeController.Ascent.UllageFore = push;
        }

        private static void Stop(Vessel v)
        {
            active = false;
            AttitudeController.Ascent.UllageFore = 0.0;
            if (v != null) AttitudeController.Ascent.Release(v);
        }

        public static void Reset() { wasDocked = false; active = false; shroudDone = false; }
    }
}
