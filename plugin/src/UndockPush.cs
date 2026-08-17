/*
 * DragonScreen - UndockPush
 *
 * GLUE. The moment the capsule leaves the port - however it left, including a MANUAL undock that never
 * touched UndockOps - give it a short retro RCS push to open the gap, and shut the docking shroud.
 *
 * ⛔ WHY: the auto-undock sequence (UndockOps) already backs away and closes the shroud, but a crew
 * that undocks by hand bypasses all of it, so the capsule drifts off the station at the ~0.1 m/s the
 * undock spring gives - "painfully slow" (crew report 2026-08-17) - and the shroud stays open through
 * the coast. This watches the docked->undocked transition and does both, once, for either kind of
 * undock. It yields entirely while UndockOps is running so the two never fight over the thrusters.
 *
 * The push holds the capsule's CURRENT attitude and translates along the nose in the away direction
 * (sign from the geometry, not a guess), so it does not slew - it just pushes off and lets go.
 */
using UnityEngine;

namespace DragonScreen
{
    public static class UndockPush
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Open at this closing-away rate, then stop, m/s. Tunable - raise for a brisker push.</summary>
        [Tunable] public static double PushRateMps = 2.0;
        /// <summary>...or once this far clear of the station, metres.</summary>
        [Tunable] public static double PushClearM = 60.0;
        /// <summary>Hard cap on the burst, seconds, so it can never run away.</summary>
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

            // Hold the current heading (no slew) and translate along the nose in the AWAY direction.
            // UllageFore = +1 drives forward (along the nose); pick the sign that points that at `away`.
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
