// DragonScreen - AbortResponder
// ---- THE LAUNCH ESCAPE USES THE VEHICLE'S OWN ABORT WIRING ----
using UnityEngine;

namespace DragonScreen
{
    public static class AbortResponder
    {
        private const string Tag = "[DragonScreen] ";

        public enum AbortMode : byte { None, LaunchEscape, Retreat, SafeHold }

        public static bool LesArmed { get; private set; }
        public static bool Aborting { get; private set; }
        public static AbortMode Mode { get; private set; }

        public static void Arm()
        {
            LesArmed = true;
            Debug.Log(Tag + "LAUNCH ESCAPE SYSTEM ARMED");
        }

        public static void Disarm() { LesArmed = false; }

        public static void Trigger(string reason)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || Aborting) return;

            Aborting = true;
            Mode = PickMode(v);
            FlightCommands.CancelAllSequences();

            switch (Mode)
            {
                case AbortMode.LaunchEscape:
                    v.ActionGroups.SetGroup(KSPActionGroup.Abort, true);
                    Debug.LogWarning(Tag + "ABORT - LAUNCH ESCAPE (" + reason + ")");
                    break;
                case AbortMode.Retreat:
                    if (!v.ActionGroups[KSPActionGroup.RCS]) v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
                    Debug.LogWarning(Tag + "ABORT - RETREAT from the station (" + reason + ")");
                    break;
                default:
                    Debug.LogWarning(Tag + "ABORT - SAFE HOLD (" + reason + ")");
                    break;
            }
            ScreenMessages.PostScreenMessage("DRAGON ABORT - " + Mode.ToString().ToUpper(), 8f,
                                             ScreenMessageStyle.UPPER_CENTER);
        }

        public static void Reset()
        {
            Aborting = false;
            Mode = AbortMode.None;
        }

        public static void Tick()
        {
            if (!Aborting) return;
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            switch (Mode)
            {
                case AbortMode.Retreat: DriveRetreat(v); break;
                case AbortMode.SafeHold: DriveSafeHold(v); break;
            }
        }

        // ---- back out of the keep-out sphere along the line away from the station, then safe-hold ----
        private static void DriveRetreat(Vessel v)
        {
            Vessel station = StationApproach.Station;
            if (station == null) { Mode = AbortMode.SafeHold; return; }

            Vector3d away = (v.CoM - station.CoM);
            double range = away.magnitude;
            if (range > WaypointApproach.KeepOutRadiusM * 1.5)
            {
                StopTranslating();
                Mode = AbortMode.SafeHold;
                Debug.Log(Tag + "abort retreat complete - clear at " + range.ToString("F0") + " m; safe hold");
                return;
            }

            Vector3d up = (v.CoM - v.mainBody.position).normalized;
            AttitudeController.Ascent.SteerTo(v, away.normalized, up);
            AttitudeController.Ascent.Throttle = 0.0;
            CapsuleRcs.Set(v, CapsuleRcs.ApproachPct);
            AttitudeController.Ascent.UllageFore = 1.0;
            AttitudeController.Ascent.TranslateX = 0.0;
            AttitudeController.Ascent.TranslateY = 0.0;
        }

        private static void DriveSafeHold(Vessel v)
        {
            AttitudeController.Ascent.Throttle = 0.0;
            StopTranslating();
            if (!v.ActionGroups[KSPActionGroup.RCS]) v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
        }

        private static void StopTranslating()
        {
            AttitudeController.Ascent.UllageFore = 0.0;
            AttitudeController.Ascent.TranslateX = 0.0;
            AttitudeController.Ascent.TranslateY = 0.0;
        }

        private static AbortMode PickMode(Vessel v)
        {
            bool onPad = v.situation == Vessel.Situations.PRELAUNCH;
            bool ascending = v.situation == Vessel.Situations.FLYING
                             || v.situation == Vessel.Situations.SUB_ORBITAL;
            if ((onPad || ascending) && LesArmed) return AbortMode.LaunchEscape;

            if (StationApproach.Engaged || WaypointApproachOps.Engaged || DockingOps.Engaged)
                return AbortMode.Retreat;

            return AbortMode.SafeHold;
        }
    }
}
