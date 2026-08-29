// DragonScreen — AttitudePilot  (KSP glue: the ACTIVE-vessel attitude facade over AttitudeController)
// ============================================================================================
// The direct gimbal/RCS attitude loop — replaces SAS. Since C2 Step-2 this is a THIN STATIC FACADE over a
// single default AttitudeController instance (AttitudeController.cs) that flies the ACTIVE vessel (the
// Dragon). The loop math + frame conversion moved to AttitudeController verbatim; this facade just holds the
// one Dragon instance, forwards the existing static API + diagnostics (so Steering / FlightDriver / FlightLog
// / AscentControl are unchanged), and does the FlightDriver.SetAttitude/… writes that route the command to
// the active vessel's OnFlyByWire. A SECOND AttitudeController instance flies the non-active booster
// (BoosterControl) with its own state, writing into the booster's own FlightCtrlState — that is the whole
// reason the loop was made instantiable (two vessels, two independent loops).
//
// The [Tunable] knobs (UseLagComp, RcsTorqueFloorNm) stay HERE, shared by every AttitudeController instance,
// so the tuning surface is exactly what it was. The frame/law documentation now lives in AttitudeController.
// ============================================================================================
using UnityEngine;

namespace DragonScreen
{
    public static class AttitudePilot
    {
        // The single loop that flies the ACTIVE vessel (the Dragon). The booster's loop is a separate instance
        // owned by BoosterControl — that is what lets the two vehicles be driven at once without state collision.
        static readonly AttitudeController active = new AttitudeController();
        public static AttitudeController ActiveController { get { return active; } }

        // ---- shared [Tunable] config (read by every AttitudeController instance) ----
        // B4 lag compensation: command the gimbal harder when it slews slowly (snappier loop through max-Q).
        [Tunable] public static bool UseLagComp = true;
        // Campaign 6: no longer a GATE — the loop now ALWAYS takes max(stock-reported, geometric) RCS torque
        // (the stock report flickers ~2 N·m 91% of RCS-on ticks, above any sane gate, and saturated the Dracos).
        // Retained as the DIAGNOSTIC threshold: when the geometric estimate exceeds the report by more than this,
        // log once that the stock report is under-reading. Kept [Tunable] so saved configs stay valid.
        [Tunable] public static double RcsTorqueFloorNm = 1.0;

        // ---- diagnostics forwarded from the active instance (the recorder / FDIR / AscentControl read these) ----
        public static double PointErrDeg { get { return active.PointErrDeg; } }
        public static double RateCmdRads { get { return active.RateCmdRads; } }
        public static double RateMeasRads { get { return active.RateMeasRads; } }
        public static double ActPitch { get { return active.ActPitch; } }
        public static double ActYaw { get { return active.ActYaw; } }
        public static double ActRoll { get { return active.ActRoll; } }
        public static double CtrlTorquePitchNm { get { return active.CtrlTorquePitchNm; } }
        public static double CtrlTorqueYawNm { get { return active.CtrlTorqueYawNm; } }
        public static double CtrlTorqueRollNm { get { return active.CtrlTorqueRollNm; } }
        public static double PitchAccelRadS2 { get { return active.PitchAccelRadS2; } }

        // Clear the active loop's integrators without dropping the hold (used while the rocket is clamped).
        public static void ResetIntegrators() { active.ResetIntegrators(); }

        // Full reset (scene load / handover): reset the active loop AND release its FlightDriver channels.
        public static void Reset()
        {
            active.Reset();
            FlightDriver.ReleaseAttitude();
        }

        // Point the nose at a world direction (active vessel). dampRoll = also null the roll RATE; pass false
        // where a separate roll channel owns roll (the entry bank).
        public static void Point(Vessel v, Vector3d worldDir, bool dampRoll)
        { Point(v, worldDir, dampRoll, Vector3d.zero); }

        // rollUpRef: a WORLD "up" the dorsal axis should track — HOLDS roll (keeps the launch plane); zero →
        // the old behaviour (roll reference = current roll, i.e. pitch+yaw only, roll left free).
        public static void Point(Vessel v, Vector3d worldDir, bool dampRoll, Vector3d rollUpRef)
        {
            AttitudeCmd c = active.Compute(v, worldDir, dampRoll, rollUpRef);
            FlightDriver.SetAttitude(c.Pitch, c.Yaw);
            if (c.HasRoll) FlightDriver.SetAttitudeRoll(c.Roll);
            else FlightDriver.ReleaseAttitudeRoll();
        }
    }
}
