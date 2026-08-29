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
        static double launchLat, launchLon;   // captured on open, for the real downrange
        static bool haveLaunchRef;

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
                // launch reference for downrange: the pad position (or wherever the log opens).
                launchLat = v.latitude; launchLon = v.longitude; haveLaunchRef = true;
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
                double downrangeM = DownrangeM(v);
                FlightRecorder.PutNav(row,
                    v.altitude, v.obt_speed, v.verticalSpeed,
                    v.dynamicPressurekPa * 1000.0, v.mach,
                    downrangeM, v.totalMass * 1000.0);
                // ⛔ ALWAYS-ON SNAPSHOT (MechJeb principle): phase/mode + surface speed + AoA + felt g +
                // measured thrust + RCS + abort state, from live vessel data, so nothing is lost when no
                // controller Fill is active (an abort, a coast, an engine cutout). See FlightRecorder.PutBase.
                AbortMode am = FlightDriver.Aborting ? AbortControl.Mode : AbortMode.None;
                FlightRecorder.PutBase(row, CrewProcedureOps.ActivePhase, CrewProcedureOps.CurrentMode,
                    v.srfSpeed, Steering.AngleOfAttackDeg(v), v.geeForce, Actuator.TotalActiveThrustN(v),
                    Actuator.IsRcsOn(v), FlightDriver.Aborting, am);
                FlightRecorder.PutGate(row, CrewProcedureOps.CurrentGateId, CrewProcedureOps.Proc.Phase,
                                       CrewProcedureOps.CrewActionNeeded());
                // ⛔ ALWAYS-ON COMMAND SNAPSHOT: the full applied control (throttle + RCS translation + the
                // attitude-loop actuation/pointing/rates) EVERY phase, so a coast/abort is never blind. This is
                // the gap that hid the stranded capsule's real behaviour. See FlightRecorder.PutCommand.
                FlightRecorder.PutCommand(row, FlightDriver.CmdThrottle,
                    FlightDriver.CmdTransX, FlightDriver.CmdTransY, FlightDriver.CmdTransZ,
                    AttitudePilot.PointErrDeg, AttitudePilot.RateCmdRads, AttitudePilot.RateMeasRads,
                    AttitudePilot.ActPitch, AttitudePilot.ActYaw, AttitudePilot.ActRoll,
                    AttitudePilot.CtrlTorquePitchNm, AttitudePilot.CtrlTorqueYawNm);
                // measured body angular rates (deg/s), pitch/roll/yaw = angularVelocity x/y/z (AttitudePilot's axis
                // order) — the raw control-rate signal the tuning DB aggregates per phase.
                Vector3 av = v.angularVelocity * Mathf.Rad2Deg;
                FlightRecorder.PutRates(row, av.x, av.y, av.z);
                // B3/T5 RCS-balance DIAGNOSTIC (records only; see Actuator.RcsInducedTorque + docs/RCS_BALANCE_FINDING.md):
                // how much rotation the commanded RCS translation induces, and how much a torque-nulling balance would
                // remove. Runs ONLY when RCS translation is actually commanded (prox-ops) — else the columns stay blank.
                double txc = FlightDriver.CmdTransX, tyc = FlightDriver.CmdTransY, tzc = FlightDriver.CmdTransZ;
                if (System.Math.Abs(txc) + System.Math.Abs(tyc) + System.Math.Abs(tzc) > 1e-3)
                {
                    double nT, bT, fF; bool feas;
                    if (Actuator.RcsInducedTorque(v, new Vector3((float)txc, (float)tyc, (float)tzc),
                                                  out nT, out bT, out fF, out feas))
                        FlightRecorder.PutRcsBalance(row, nT, bT, fF);
                }
                // control AUTHORITY + orbit state: roll torque (pitch/yaw already in the command snapshot), MOI per
                // axis, RCS thrust in use, and the orbit shape/plane — so the DB can flag where authority is
                // marginal (actuation saturating, torque/MOI too low, RCS maxed) and track the plane/guidance.
                Vector3 moi = v.MOI;
                Orbit o = v.orbit;
                FlightRecorder.PutAuthority(row, AttitudePilot.CtrlTorqueRollNm, moi.x, moi.y, moi.z,
                    Actuator.RcsThrustN(v),
                    o != null ? o.ApA / 1000.0 : double.NaN, o != null ? o.PeA / 1000.0 : double.NaN,
                    o != null ? o.inclination : double.NaN, o != null ? o.LAN : double.NaN);
                // KER soft cross-check: what Kerbal Engineer's fuel-flow sim reports for the active vessel, so the
                // corpus can verify it agrees with our own StageStats/UPFG before any consumer trusts KER over us.
                KerStage[] ks;
                if (KerBridge.TryGetStages(out ks))
                {
                    KerBridge.RequestSimulation();   // keep KER's result fresh for the next sample
                    KerStage cur = KerData.Current(ks), fin = KerData.Final(ks);
                    FlightRecorder.PutKer(row, true, KerData.RemainingDeltaV(ks), fin.Valid ? fin.TotalDeltaVMps : 0.0,
                                          cur.Twr, cur.ThrustN);
                }
                else FlightRecorder.PutKer(row, false, 0, 0, 0, 0);

                // ⭐ P0.0 INSTRUMENT: the live warp multiplier + the active vessel's main-engine ignition state.
                double warpRate = 1.0; bool onRailsWarp = false;
                try
                {
                    warpRate = TimeWarp.CurrentRate;
                    onRailsWarp = TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRateIndex > 0;
                }
                catch { }
                int engIgn, engFlame; EngineState(v, out engIgn, out engFlame);
                FlightRecorder.PutInstrument(row, warpRate, engIgn, engFlame);

                Action<string[]> fill = Fill;   // the active controller's PHASE-SPECIFIC columns on top of the base
                if (fill != null) { try { fill(row); } catch { } }

                // ⭐ P0.0 (I1): on-rails warp → the physics loop is OFF, so every delivered/measured control value
                // above is a FROZEN stale read. Void them (LAST, so nothing re-fills them) — a warp_rate>1 row must
                // never be read as live control. Nav/orbit/MOI/phase/eng-state remain valid.
                if (onRailsWarp) FlightRecorder.ZeroControlColumnsForWarp(row);

                writer.WriteLine(FlightRecorder.Row(row));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] flight-log sample failed: " + e.Message);
            }
        }

        // ⭐ P0.0 (I2): count the active vessel's MAIN engines (ModuleEngines, not RCS) that are commanded-on
        // (EngineIgnited) and that have flamed out — so an ignition ATTEMPT is provable in the CSV even when the
        // delivered thrust column reads 0 (was the booster ambiguity: engine-not-firing vs not-captured). Guarded.
        static void EngineState(Vessel v, out int ignited, out int flameout)
        {
            ignited = 0; flameout = 0;
            if (v == null || v.parts == null) return;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        ModuleEngines e = p.Modules[m] as ModuleEngines;
                        if (e == null) continue;
                        if (e.EngineIgnited) ignited++;
                        if (e.flameout) flameout++;
                    }
                }
            }
            catch { }
        }

        // Great-circle surface distance from the launch reference to the current sub-point (metres).
        // The real downrange, replacing the old hardcoded NaN so ascent/entry footprints read directly.
        static double DownrangeM(Vessel v)
        {
            if (!haveLaunchRef || v.mainBody == null) return double.NaN;
            double R = v.mainBody.Radius;
            if (R <= 0.0) return double.NaN;
            double lat1 = launchLat * Math.PI / 180.0, lat2 = v.latitude * Math.PI / 180.0;
            double dLat = lat2 - lat1;
            double dLon = (v.longitude - launchLon) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            if (a < 0.0) a = 0.0; else if (a > 1.0) a = 1.0;
            return 2.0 * R * Math.Asin(Math.Sqrt(a));
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
