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
        // B4 actuator-lag lead compensation. OFF (2026-09-01, RESEARCH, not a guess): read MechJeb's actual
        // BetterController.cs + PIDLoop2.cs in full — our PID is already IDENTICAL to MechJeb's (PosKp 2.03,
        // VelKp 7.98, P-only velocity, SmoothIn/Out=1.0=pass-through). MechJeb's controller has NO lag comp; this
        // was OUR non-MechJeb addition, and it over-drives the gimbal command to the ±1 rail every tick — that
        // railed command IS the visible steering jitter. Removing it = the faithful MechJeb loop, no extra loop.
        [Tunable] public static bool UseLagComp = false;
        // Campaign 6: no longer a GATE — the loop now ALWAYS takes max(stock-reported, geometric) RCS torque
        // (the stock report flickers ~2 N·m 91% of RCS-on ticks, above any sane gate, and saturated the Dracos).
        // Retained as the DIAGNOSTIC threshold: when the geometric estimate exceeds the report by more than this,
        // log once that the stock report is under-reading. Kept [Tunable] so saved configs stay valid.
        [Tunable] public static double RcsTorqueFloorNm = 1.0;
        // HOLD AUTHORITY SCALE — multiply the loop's POSITION-hold gain (AttitudeLoop.PosKp0). REVERTED 1.5→1.0
        // (2026-09-01): the 1.5× added loop gain that fed the attitude limit cycle (rate-pitch std 0.20→0.28) with
        // no benefit — our loop was already pointed (att_point 0.1°) and never flipped (the flip was stock SAS).
        [Tunable] public static double HoldAuthorityScale = 1.0;
        // ⭐ RCS-hold phase-plane deadband (DS-ASC-007 fuel fix): when the RCS is the ATTITUDE actuator (no gimbal),
        // the loop COASTS within this (angle, rate) box instead of chattering the Dracos to hold a tiny error —
        // measured cause of ~97% of rendezvous fuel (52% attitude-only + 45% simultaneous). Applied by
        // AttitudeController ONLY when the gimbal term is ~0, so the flight-proven ascent is untouched. Wide here
        // (coast/approach economy); a tighter terminal-dock band is future work. 0 disables.
        // Reverted to 0 (OFF = pre-change default) 2026-09-01 per owner ("control tuning back to defaults").
        // Also moot while UseGimbalLoop=false (KSP SAS flies attitude; this custom loop is bypassed).
        [Tunable] public static double RcsHoldDeadbandDeg = 0.0;
        [Tunable] public static double RcsHoldRateDbDps   = 0.0;
        public static double RcsHoldDeadbandRad { get { return RcsHoldDeadbandDeg * 0.0174532925199433; } }
        public static double RcsHoldRateDbRadps { get { return RcsHoldRateDbDps * 0.0174532925199433; } }

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
        public static double GeoTorquePitchNm { get { return active.GeoTorquePitchNm; } }
        public static double GeoTorqueYawNm { get { return active.GeoTorqueYawNm; } }
        public static double GeoTorqueRollNm { get { return active.GeoTorqueRollNm; } }
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
