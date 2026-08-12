/*
 * DragonScreen - FlightMonitor
 *
 * THE INDEPENDENT WATCH. It flies nothing and owns no actuator; it only compares things that should
 * agree and says so, once, when they do not.
 *
 * ---- WHY THIS EXISTS, WITH THE NUMBERS ----
 * An outside review of the repo said the project had "no independent safety monitor" and that a
 * single bad state could leave the vehicle unsteered while the software believed it was in control.
 * I initially dismissed the redundancy half of that argument as pattern-matching from real Dragon
 * hardware - three identical deterministic controllers cannot vote usefully - and that was a reply
 * to a strawman. The useful form is DISSIMILAR redundancy: two independent paths to one quantity,
 * watched for disagreement.
 *
 * Tested against the archive before writing a line of it. A disagreement sweep over the 2026-08-12
 * flights would have caught, unprompted:
 *
 *     x_rcsCmd vs x_rcsOn            disagree on 89% of rows, EVERY FLIGHT   (dead command column)
 *     a_cmdThrottle vs a_ctlThrottle disagree while the engine is at 1.00    (dead command column)
 *     m_stationKm vs x_dkRangeM      frozen source vs live one               (stale telemetry)
 *
 * Three of that week's faults, none of which any of the ~7,900 headless checks could see, because
 * they are all disagreements between two live sources rather than wrong arithmetic.
 *
 * ---- ⛔ IT MUST NEVER BECOME A CONTROLLER ----
 * The temptation with a monitor is to let it "fix" what it finds. It does not. It has no vessel
 * reference it can steer with and no actuator authority, and that is deliberate: a monitor that can
 * act is a second controller, and this project has already lost a flight to two controllers holding
 * one set of thrusters. It reports. The crew and the owning controller decide.
 *
 * ---- AND IT MUST NOT CRY WOLF ----
 * Every check latches. A warning that repeats every tick is a warning that gets filtered out, which
 * is how `no booster to recover` survived four flights being wrong. One line per fault per flight.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    internal static class FlightMonitor
    {
        private const string Tag = "[DragonScreen] MONITOR: ";

        /// <summary>Seconds a disagreement must persist before it is worth saying. </summary>
        private const double SettleS = 3.0;

        /// <summary>Sustained attitude error that means the vehicle is not tracking, degrees.</summary>
        private const double AttitudeLostDeg = 45.0;

        /// <summary>...and how long it must stay there. Long enough to clear a legitimate slew.</summary>
        private const double AttitudeLostS = 20.0;

        private static bool saidRcs, saidRotation, saidRange, saidAttitude, saidAuthority;
        private static double rcsSince, rotSince, rangeSince, attSince, authSince;

        internal static void Reset()
        {
            saidRcs = saidRotation = saidRange = saidAttitude = saidAuthority = false;
            rcsSince = rotSince = rangeSince = attSince = authSince = 0.0;
        }

        /// <summary>
        /// One pass. Called from the flight driver, outside every controller, so a controller that
        /// throws or detaches does not take the watch with it.
        /// </summary>
        internal static void Tick()
        {
            try { Check(); }
            catch (Exception e)
            {
                // ⚠ THE MONITOR MUST NOT BE THE THING THAT BREAKS THE FLIGHT. If it cannot run,
                // it says so once and stops - it never propagates into the driver's tick.
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
            // Our own actuation against what the vessel finally received. They should track. On
            // 2026-08-12 the final state read +/-1.0 on all three axes for an entire docking while
            // our actuation sat near zero, and no column existed that could show it.
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
                // Port-to-port and centre-to-centre legitimately differ by the vehicles' own size.
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
                // The docking stall in one check: full command, propellant leaving the tank, nothing
                // happening. 98 units went that way on 2026-08-12 with no net motion.
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

        /// <summary>
        /// Say it once, and only after it has persisted. `since` is the clock, `said` the latch.
        /// </summary>
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
