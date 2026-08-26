// DragonScreen - FlightMonitor
// ---- WHY THIS EXISTS, WITH THE NUMBERS ----
// ---- ⛔ IT MUST NEVER BECOME A CONTROLLER ----
// ---- AND IT MUST NOT CRY WOLF ----
using System;
using UnityEngine;

namespace DragonScreen
{
    internal static class FlightMonitor
    {
        private const string Tag = "[DragonScreen] MONITOR: ";

        private const double SettleS = 3.0;

        private const double AttitudeLostDeg = 45.0;

        private const double AttitudeLostS = 20.0;

        private static bool saidRcs, saidRotation, saidRange, saidAttitude, saidAuthority;
        private static double rcsSince, rotSince, rangeSince, attSince, authSince;

        internal static void Reset()
        {
            saidRcs = saidRotation = saidRange = saidAttitude = saidAuthority = false;
            rcsSince = rotSince = rangeSince = attSince = authSince = 0.0;
        }

        internal static void Tick()
        {
            try { Check(); }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "the monitor itself failed and has stopped: " + e.Message);
                saidRcs = saidRotation = saidRange = saidAttitude = saidAuthority = true;
            }
        }

        private static void Check()
        {
            double now = Planetarium.GetUniversalTime();
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.state == Vessel.State.DEAD) return;

            bool held = AutoPilot.Engaged || DirectApproachOps.Engaged || DockingOps.Engaged
                        || UndockOps.Engaged || DeorbitOps.Engaged || EntryOps.Engaged;

            // ---- 1. RCS: asked for, and actually on? ----
            bool wantRcs = AutoPilot.Command.Rcs || DirectApproachOps.Engaged
                           || DockingOps.Engaged || UndockOps.Engaged;
            bool haveRcs = v.ActionGroups[KSPActionGroup.RCS];
            Watch(wantRcs && !haveRcs, now, ref rcsSince, ref saidRcs,
                  "RCS is being commanded but the action group is OFF - every translation and "
                  + "attitude command is going nowhere.");

            // ---- 2. Is something outside this software driving the rotation? ----
            AttitudeController ac = AttitudeController.For(v);
            if (ac != null && held && v.ctrlState != null)
            {
                Vector3d act = ac.Actuation;
                double ours = Math.Abs(act.x) + Math.Abs(act.y) + Math.Abs(act.z);
                double got = Math.Abs(v.ctrlState.pitch) + Math.Abs(v.ctrlState.roll)
                           + Math.Abs(v.ctrlState.yaw);
                Watch(got > 1.5 && ours < 0.15, now, ref rotSince, ref saidRotation,
                      "the vehicle is receiving large rotation commands (" + got.ToString("F2")
                      + ") that this controller did not issue (" + ours.ToString("F2")
                      + ") - SAS, another mod, or manual input is fighting the autopilot.");
            }

            // ---- 3. Two sources for the range to the station ----
            if (DockingOps.Engaged && StationApproach.Station != null
                && DockingOps.RangeToPortM > 1.0 && StationApproach.RangeM > 1.0)
            {
                double gap = Math.Abs(StationApproach.RangeM - DockingOps.RangeToPortM);
                Watch(gap > 200.0, now, ref rangeSince, ref saidRange,
                      "the two range sources disagree by " + gap.ToString("F0")
                      + " m (approach " + StationApproach.RangeM.ToString("F0")
                      + ", docking " + DockingOps.RangeToPortM.ToString("F0")
                      + ") - one of them is stale.");
            }

            // ---- 4. Is the vehicle tracking what it was told at all? ----
            if (ac != null && held)
            {
                Watch(ac.ErrorDeg > AttitudeLostDeg, now, ref attSince, ref saidAttitude,
                      "attitude error has stayed above " + AttitudeLostDeg.ToString("F0")
                      + " degrees for " + AttitudeLostS.ToString("F0")
                      + " s while a controller holds the vehicle - it is not tracking its command.",
                      AttitudeLostS);

                // ---- 5. Commanding, burning, and not responding ----
                bool commanding = Math.Abs(ac.UllageFore) > 0.4 || Math.Abs(ac.TranslateX) > 0.4
                                  || Math.Abs(ac.TranslateY) > 0.4;
                Watch(commanding && haveRcs && DockingOps.Engaged
                      && DockingOps.ClosingMps < 0.02 && DockingOps.ClosingMps > -0.02,
                      now, ref authSince, ref saidAuthority,
                      "translation is commanded near full and RCS is on, but the closing rate has "
                      + "been ~zero for " + AttitudeLostS.ToString("F0")
                      + " s - the command is not reaching the vehicle, or it is being cancelled.",
                      AttitudeLostS);
            }
        }

        private static void Watch(bool bad, double now, ref double since, ref bool said,
                                  string what) { Watch(bad, now, ref since, ref said, what, SettleS); }

        private static void Watch(bool bad, double now, ref double since, ref bool said,
                                  string what, double holdS)
        {
            if (!bad) { since = 0.0; return; }
            if (since <= 0.0) { since = now; return; }
            if (now - since < holdS || said) return;
            said = true;
            Debug.LogWarning(Tag + what);
        }
    }
}
