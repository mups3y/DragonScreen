/*
 * DragonScreen - FlightRecorder
 *
 * GLUE. Writes a CSV of every flight at 5 Hz, with the GUIDANCE COMMAND and the VEHICLE RESPONSE on
 * the same row.
 *
 * ---- WHY THIS EXISTS ----
 * `ls bb_*.csv` returned NOTHING after five test flights. The black box is `F9I/blackbox.ks`, a kOS
 * script, and it only records while the F9I program is running - we fly under the C# autopilot with
 * kOS idle, so not one of those flights was recorded. Every failure this session was diagnosed from
 * 2 Hz text lines that I had usually added AFTER the flight that needed them.
 *
 * ---- AND THE COMMAND IS THE HALF THAT MATTERS ----
 * Even with the black box running it would not be enough. It records what the vehicle DID - pitch,
 * rates, throttle, q - and not what it was ASKED to do. F9I hit exactly this and solved it with its
 * x1..x4 scratch columns, and its own comment is the argument for this whole file:
 *
 *      these two are the reason a bad landing can be diagnosed at all - they record what the
 *      guidance ASKED for, which no KSP telemetry exposes
 *
 * Without the pair you can see the vehicle was twelve degrees off and still not know whether the
 * guidance asked for the wrong attitude or the controller failed to reach the right one. Every open
 * tuning question on this project is that distinction.
 *
 * ---- WHAT IT IS DELIBERATELY NOT ----
 * Not a replacement for the black box, which stays F9I's for kOS flights. Not a debug log - it is
 * columns, so it can be plotted and differenced. And not buffered to the end: it flushes as it goes,
 * because the flights worth diagnosing are the ones that end unexpectedly.
 */
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DragonScreen
{
    public static class FlightRecorder
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>5 Hz, matching the black box so the two are comparable.</summary>
        private const double IntervalS = 0.2;

        /// <summary>Rows buffered before a flush. Small: an unexpected end must not lose the end.</summary>
        private const int FlushEvery = 25;

        private static StreamWriter writer;
        private static StringBuilder pending = new StringBuilder();
        private static int pendingRows;
        private static double lastSample = -999.0;
        private static double startedUt;
        private static uint vesselId;

        public static bool Recording { get { return writer != null; } }

        // ------------------------------------------------------------------ lifecycle

        public static void Start(Vessel v)
        {
            if (writer != null || v == null) return;
            try
            {
                string dir = Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "DragonScreen_capture");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string name = "flight_" + DateTime.Now.ToString("MMdd_HHmmss") + ".csv";
                writer = new StreamWriter(Path.Combine(dir, name), false);
                writer.WriteLine(Header);
                writer.Flush();

                startedUt = Planetarium.GetUniversalTime();
                vesselId = v.persistentId;
                lastSample = -999.0;
                Debug.Log(Tag + "recording -> " + name);
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "could not start the recorder: " + e.Message);
                writer = null;
            }
        }

        public static void Stop(string why)
        {
            if (writer == null) return;
            try
            {
                Flush();
                writer.Close();
                Debug.Log(Tag + "recording stopped - " + why);
            }
            catch (Exception) { }
            writer = null;
            pending.Length = 0;
            pendingRows = 0;
        }

        private static void Flush()
        {
            if (writer == null || pending.Length == 0) return;
            writer.Write(pending.ToString());
            writer.Flush();
            pending.Length = 0;
            pendingRows = 0;
        }

        // ------------------------------------------------------------------ the row

        // ---- ⛔ ONE COLUMN BLOCK PER VEHICLE. IT USED TO SAMPLE FlightGlobals.ActiveVessel. ----
        // Which meant every telemetry column changed which CRAFT it described the moment focus moved
        // to the booster. In the 22:18 recording `massT` steps 35.55 -> 59.13 at MET 102 and then
        // wanders between the two vehicles for the rest of the flight, so nothing in the file can be
        // differenced or plotted without first working out who each row is about. Diagnosing the
        // RTLS from it took hours and several wrong conclusions, which is exactly the cost the
        // recorder exists to avoid.
        //
        // `a_` is the ASCENT vehicle - AutoPilot's, held by reference, so it stays the same craft
        // from liftoff to insertion even after the S2 comes off. `b_` is the BOOSTER. Both are
        // written every row; a vehicle that does not exist writes zeros and its phase reads "-".
        private const string Header =
            "met,ut,focus," +
            // ================= ASCENT VEHICLE =================
            "a_phase,a_note," +
            "a_altAsl,a_altRadar,a_lat,a_lon,a_vertSpeed,a_srfSpeed,a_orbSpeed,a_mach,a_qKpa," +
            "a_apoKm,a_periKm,a_incDeg,a_timeToApS," +
            "a_massT,a_availThrustKn,a_moiX,a_moiY,a_moiZ,a_torqueX,a_torqueY,a_torqueZ," +
            // guidance command
            "a_cmdPitchDeg,a_cmdHeadingDeg,a_cmdThrottle,a_cmdStage,a_cmdSepS2,a_cmdUllage," +
            "a_cmdRcs,a_circDvMps," +
            // attitude: commanded vs achieved
            "a_attErrDeg,a_phiPitch,a_phiRoll,a_phiYaw," +
            "a_tgtOmegaP,a_tgtOmegaR,a_tgtOmegaY,a_omegaP,a_omegaR,a_omegaY," +
            "a_tgtTorqueP,a_tgtTorqueR,a_tgtTorqueY,a_actP,a_actR,a_actY," +
            "a_ctlPitch,a_ctlYaw,a_ctlRoll,a_ctlThrottle," +
            // ================= BOOSTER =================
            "b_phase," +
            "b_altAsl,b_altRadar,b_lat,b_lon,b_vertSpeed,b_srfSpeed,b_mach,b_qKpa," +
            "b_massT,b_availThrustKn,b_torqueX,b_torqueY,b_torqueZ," +
            // landing command and the two numbers that judge it
            "b_cmdThrottle,b_ignitionAlt,b_engines,b_aim,b_legs," +
            "b_downrangeKm,b_predMissKm,b_initMissKm," +
            "b_attErrDeg,b_omegaP,b_omegaR,b_omegaY,b_actP,b_actR,b_actY," +
            "b_ctlPitch,b_ctlYaw,b_ctlRoll,b_ctlThrottle";

        /// <summary>
        /// Called every frame by the painter; samples at 5 Hz. Cheap enough to call unconditionally
        /// and the rate limit lives here, so no caller has to remember it.
        /// </summary>
        public static void Tick()
        {
            if (writer == null) return;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            double ut = Planetarium.GetUniversalTime();
            if (ut - lastSample < IntervalS) return;
            lastSample = ut;

            try { WriteRow(v, ut); }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "recorder row failed, stopping: " + e.Message);
                Stop("row failed");
            }
        }

        private static void WriteRow(Vessel v, double ut)
        {
            StringBuilder r = pending;

            F(r, ut - startedUt); F(r, ut);
            Vessel focus = FlightGlobals.ActiveVessel;
            S(r, focus != null ? focus.vesselName : "-");

            // ---------------- ascent vehicle ----------------
            Vessel a = AutoPilot.AscentVessel;
            S(r, AutoPilot.Engaged ? Ascent.Name(AutoPilot.Phase) : "-");
            S(r, AutoPilot.Command.Note);
            Motion(r, a, true);

            AscentCommand c = AutoPilot.Command;
            F(r, c.PitchDeg); F(r, c.HeadingDeg); F(r, c.Throttle);
            F(r, c.Stage ? 1.0 : 0.0); F(r, c.SeparateS2 ? 1.0 : 0.0); F(r, c.UllageFore);
            F(r, c.Rcs ? 1.0 : 0.0);
            F(r, AutoPilot.LastCircDvMps);
            Attitude(r, AttitudeController.Ascent, true);
            Controls(r, a);

            // ---------------- booster ----------------
            Vessel b = BoosterRecovery.BoosterVessel;
            S(r, BoosterRecovery.Active ? Landing.Name(BoosterRecovery.Phase) : "-");
            Motion(r, b, false);

            LandingCommand lc = BoosterRecovery.Command;
            F(r, lc.Throttle); F(r, lc.IgnitionAltitude); F(r, lc.Engines);
            F(r, (double)(int)lc.Aim); F(r, lc.DeployLegs ? 1.0 : 0.0);
            F(r, BoosterRecovery.DownrangeM / 1000.0);
            F(r, BoosterRecovery.PredictedMissM / 1000.0);
            F(r, BoosterRecovery.InitialMissM / 1000.0);
            Attitude(r, AttitudeController.Booster, false);
            Controls(r, b);

            r.Length -= 1;                // trailing comma
            r.Append("\n");

            if (++pendingRows >= FlushEvery) Flush();
        }

        /// <summary>Where it is and what it weighs. `orbital` adds the orbit block.</summary>
        private static void Motion(StringBuilder r, Vessel v, bool orbital)
        {
            if (v == null || v.state == Vessel.State.DEAD)
            {
                int n = orbital ? 21 : 13;
                for (int i = 0; i < n; i++) F(r, 0.0);
                return;
            }

            F(r, v.altitude); F(r, v.radarAltitude); F(r, v.latitude); F(r, v.longitude);
            F(r, v.verticalSpeed); F(r, v.srfSpeed);
            if (orbital) F(r, v.obt_speed);
            F(r, v.mach); F(r, v.dynamicPressurekPa);

            if (orbital)
            {
                Orbit o = v.orbit;
                F(r, o != null ? o.ApA / 1000.0 : 0.0);
                F(r, o != null ? o.PeA / 1000.0 : 0.0);
                F(r, o != null ? o.inclination : 0.0);
                F(r, o != null ? o.timeToAp : 0.0);
            }

            F(r, v.GetTotalMass());
            F(r, Thrust(v));
            if (orbital) V(r, v.MOI);
            V(r, AttitudeController.For(v) != null ? AttitudeController.For(v).Torque : Vector3d.zero);
        }

        /// <summary>The control loop's internals. `full` writes phi and the target torques too.</summary>
        private static void Attitude(StringBuilder r, AttitudeController ac, bool full)
        {
            F(r, ac.ErrorDeg);
            if (full)
            {
                V(r, ac.Phi);
                V(r, ac.TargetOmega);
            }
            V(r, ac.Omega);
            if (full) V(r, ac.TargetTorque);
            V(r, ac.Actuation);
        }

        /// <summary>
        /// What actually reached the axes. Dead in all 554 kOS recordings - cooked steering and
        /// stock SAS both bypass FlightCtrlState - and live since our controller took over. This is
        /// the half that makes system identification possible at all.
        /// </summary>
        private static void Controls(StringBuilder r, Vessel v)
        {
            if (v == null || v.state == Vessel.State.DEAD || v.ctrlState == null)
            {
                F(r, 0.0); F(r, 0.0); F(r, 0.0); F(r, 0.0);
                return;
            }
            F(r, v.ctrlState.pitch); F(r, v.ctrlState.yaw); F(r, v.ctrlState.roll);
            F(r, v.ctrlState.mainThrottle);
        }

        private static double Thrust(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (es[m].EngineIgnited && !es[m].flameout) t += es[m].finalThrust;
            }
            return t;
        }

        /// <summary>Invariant culture, always - a CSV with commas for decimals is not a CSV.</summary>
        private static void F(StringBuilder r, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) d = 0.0;
            r.Append(d.ToString("G6", System.Globalization.CultureInfo.InvariantCulture));
            r.Append(',');
        }

        private static void V(StringBuilder r, Vector3d v) { F(r, v.x); F(r, v.y); F(r, v.z); }

        private static void S(StringBuilder r, string s)
        {
            // Commas in a note would shift every later column, and the notes are written by hand.
            r.Append(string.IsNullOrEmpty(s) ? "-" : s.Replace(',', ';'));
            r.Append(',');
        }
    }
}
