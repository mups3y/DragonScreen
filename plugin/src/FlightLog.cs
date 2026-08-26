// DragonScreen — FlightLog  (KSP glue: the per-flight CSV writer for the FlightRecorder)
// ============================================================================================
// The glue side of L7 instrumentation: it opens one CSV per flight under <KSP>/DragonScreen_capture/,
// writes the FlightRecorder header once, and appends a row at a fixed sample rate. The pure recorder
// (pure/FlightRecorder.cs) owns the schema + formatting + the Put* fillers; this owns only the file and
// the sampling clock. Everything is wrapped so a logging fault can never take the flight down.
//
// This seam records time + nav + the gate/mode state; each flying controller's columns fill in as its
// glue lands (the fillers already exist — the controller just calls them into the row before append).
// ============================================================================================
using System;
using System.IO;
using UnityEngine;

namespace DragonScreen
{
    public static class FlightLog
    {
        [Tunable] public static double SampleIntervalS = 0.25;   // 4 Hz — plenty for post-flight analysis

        // The active flying controller sets this to add ITS columns to each row (ascent, booster, …).
        // Cleared when no controller is flying, so a row only carries the columns that are live.
        public static Action<string[]> Fill;

        static StreamWriter writer;
        static uint openVesselId;
        static double lastSampleT = -1e9;
        static double startUT;

        static void Open(Vessel v)
        {
            Close();
            try
            {
                string dir = Path.Combine(KSPUtil.ApplicationRootPath, "DragonScreen_capture");
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string name = Sanitize(v.vesselName) + "_" + stamp + ".csv";
                writer = new StreamWriter(Path.Combine(dir, name), false);
                writer.WriteLine(FlightRecorder.Header());
                writer.Flush();
                openVesselId = v.persistentId;
                startUT = Planetarium.GetUniversalTime();
                lastSampleT = -1e9;   // a revert can move UT backwards; force the first sample to fire
                Debug.Log("[DragonScreen] flight log → " + name);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] could not open flight log: " + e.Message);
                writer = null;
            }
        }

        public static void Close()
        {
            if (writer == null) return;
            try { writer.Flush(); writer.Close(); } catch { }
            writer = null;
        }

        // Called each physics frame by FlightDriver. Opens on first use / vessel change; rate-limited.
        public static void Sample(Vessel v)
        {
            if (v == null) return;
            try
            {
                if (writer == null || v.persistentId != openVesselId) Open(v);
                if (writer == null) return;

                double ut = Planetarium.GetUniversalTime();
                if (ut - lastSampleT < SampleIntervalS) return;
                lastSampleT = ut;

                string[] row = FlightRecorder.NewRow();
                FlightRecorder.PutTime(row, v.missionTime);
                FlightRecorder.PutNav(row,
                    v.altitude, v.obt_speed, v.verticalSpeed,
                    v.dynamicPressurekPa * 1000.0, v.mach,
                    double.NaN, v.totalMass * 1000.0);
                // ⛔ ALWAYS-ON SNAPSHOT (MechJeb principle): phase/mode + surface speed + AoA + felt g +
                // measured thrust + RCS + abort state, from live vessel data, so nothing is lost when no
                // controller Fill is active (an abort, a coast, an engine cutout). See FlightRecorder.PutBase.
                AbortMode am = FlightDriver.Aborting ? AbortMode.LaunchEscape : AbortMode.None;
                FlightRecorder.PutBase(row, CrewProcedureOps.ActivePhase, CrewProcedureOps.CurrentMode,
                    v.srfSpeed, Steering.AngleOfAttackDeg(v), v.geeForce, Actuator.TotalActiveThrustN(v),
                    Actuator.IsRcsOn(v), FlightDriver.Aborting, am);
                FlightRecorder.PutGate(row, CrewProcedureOps.CurrentGateId, CrewProcedureOps.Proc.Phase,
                                       CrewProcedureOps.CrewActionNeeded());
                Action<string[]> fill = Fill;   // the active controller's PHASE-SPECIFIC columns on top of the base
                if (fill != null) { try { fill(row); } catch { } }
                writer.WriteLine(FlightRecorder.Row(row));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] flight-log sample failed: " + e.Message);
            }
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "flight";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append((char.IsLetterOrDigit(c) || c == '-' || c == '_') ? c : '_');
            return sb.ToString();
        }
    }
}
