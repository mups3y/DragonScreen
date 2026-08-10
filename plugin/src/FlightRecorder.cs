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

        private const string Header =
            "met,ut," +
            "phase,note," +
            // --- where it is ---
            "altAsl,altRadar,lat,lon,vertSpeed,srfSpeed,orbSpeed,mach,qKpa," +
            "apoKm,periKm,incDeg,timeToApS," +
            // --- what it weighs and can push with ---
            "massT,availThrustKn,moiX,moiY,moiZ,torqueX,torqueY,torqueZ," +
            // --- GUIDANCE COMMAND ---
            "cmdPitchDeg,cmdHeadingDeg,cmdThrottle,cmdStage,cmdUllage,circDvMps," +
            // --- ATTITUDE: commanded vs achieved ---
            "attErrDeg,phiPitch,phiRoll,phiYaw," +
            "tgtOmegaP,tgtOmegaR,tgtOmegaY,omegaP,omegaR,omegaY," +
            "tgtTorqueP,tgtTorqueR,tgtTorqueY,actP,actR,actY," +
            // --- what actually reached the axes. Dead in every kOS recording; live now. ---
            "ctlPitch,ctlYaw,ctlRoll,ctlThrottle," +
            // --- booster recovery ---
            "landPhase,trueRadar,burnThrottle,ignitionAlt,engines,downrangeKm";

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
            S(r, AutoPilot.Engaged ? Ascent.Name(AutoPilot.Phase) : "-");
            S(r, AutoPilot.Command.Note);

            F(r, v.altitude); F(r, v.radarAltitude); F(r, v.latitude); F(r, v.longitude);
            F(r, v.verticalSpeed); F(r, v.srfSpeed); F(r, v.obt_speed);
            F(r, v.mach); F(r, v.dynamicPressurekPa);

            Orbit o = v.orbit;
            F(r, o != null ? o.ApA / 1000.0 : 0.0);
            F(r, o != null ? o.PeA / 1000.0 : 0.0);
            F(r, o != null ? o.inclination : 0.0);
            F(r, o != null ? o.timeToAp : 0.0);

            F(r, v.GetTotalMass());
            F(r, Thrust(v));
            V(r, v.MOI);
            V(r, AttitudeController.Torque);

            AscentCommand c = AutoPilot.Command;
            F(r, c.PitchDeg); F(r, c.HeadingDeg); F(r, c.Throttle);
            F(r, c.Stage ? 1.0 : 0.0); F(r, c.UllageFore);
            // circDv is not on the command, so take it from the last inputs the autopilot built.
            F(r, AutoPilot.LastCircDvMps);

            F(r, AttitudeController.ErrorDeg);
            V(r, AttitudeController.Phi);
            V(r, AttitudeController.TargetOmega);
            V(r, AttitudeController.Omega);
            V(r, AttitudeController.TargetTorque);
            V(r, AttitudeController.Actuation);

            // ---- THE COLUMNS THAT WERE DEAD IN ALL 554 kOS RECORDINGS ----
            // kOS cooked steering and stock SAS both bypass FlightCtrlState, so these were always
            // zero. Our controller writes them, so from here they are real - and that is what makes
            // system identification possible at all.
            F(r, v.ctrlState.pitch); F(r, v.ctrlState.yaw); F(r, v.ctrlState.roll);
            F(r, v.ctrlState.mainThrottle);

            LandingCommand lc = BoosterRecovery.Command;
            S(r, BoosterRecovery.Active ? Landing.Name(BoosterRecovery.Phase) : "-");
            F(r, 0.0);                    // trueRadar - only meaningful on the booster
            F(r, lc.Throttle);
            F(r, lc.IgnitionAltitude);
            F(r, lc.Engines);
            F(r, 0.0);                    // downrange - filled when the recovery is flying
            r.Length -= 1;                // trailing comma
            r.Append('\n');

            if (++pendingRows >= FlushEvery) Flush();
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
