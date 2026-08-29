// DragonScreen — BoosterLog  (KSP glue: the per-recovery CSV writer for the NON-ACTIVE booster)
// ============================================================================================
// R1 recorder-fidelity fix (holes found on flight 144114): the FlightRecorder only samples the ACTIVE
// vessel, so when the Dragon stays active and the booster is flown to a landing on its own OnFlyByWire
// (C2 Step-2), the booster's entire recovery had NO CSV — only sparse ~2 s KSP.log lines. This is the
// booster's OWN recording stream: same schema + assess tooling as the Dragon's, so `assess_flight.py`
// reads it identically. It owns only the file + the 4 Hz sample clock; BoosterControl fills the row
// (it holds the booster's FSM state + its own AttitudeController). Sampled from BoosterControl.DriveNonActive.
// Everything is guarded — a logging fault can never take the flight (or the recovery) down.
// ============================================================================================
using System;
using System.IO;
using UnityEngine;

namespace DragonScreen
{
    public static class BoosterLog
    {
        [Tunable] public static double SampleIntervalS = 0.25;   // 4 Hz — same cadence as the Dragon's log

        static StreamWriter writer;
        static uint openVesselId;
        static double lastSampleT = -1e9;

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
                lastSampleT = -1e9;
                Debug.Log("[DragonScreen] BOOSTER log → " + name + " (non-active recovery stream)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] could not open booster log: " + e.Message);
                writer = null;
            }
        }

        public static void Close()
        {
            if (writer == null) return;
            try { writer.Flush(); writer.Close(); } catch { }
            writer = null;
        }

        // Called each DriveNonActive tick (~50 Hz) — rate-limited to 4 Hz internally. Opens on first use /
        // vessel change; BoosterControl fills the booster columns; on-rails warp voids the frozen control
        // columns exactly as the Dragon's log does (the P0.0/I1 rule applies to any vessel).
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
                BoosterControl.FillRecorderRow(row, v);

                bool onRailsWarp = false;
                try { onRailsWarp = TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRateIndex > 0; }
                catch { }
                if (onRailsWarp) FlightRecorder.ZeroControlColumnsForWarp(row);

                writer.WriteLine(FlightRecorder.Row(row));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] booster-log sample failed: " + e.Message);
            }
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "booster";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append((char.IsLetterOrDigit(c) || c == '-' || c == '_') ? c : '_');
            return sb.ToString();
        }
    }
}
