// DragonScreen — BlackBox / THE RECORDER  (register BB1; spec: docs/BLACKBOX_RESEARCH.md §4)
// ============================================================================================
// KSP GLUE. The flight recorder's one entry point: a `[KSPAddon(Flight)]` behaviour with a
// `FixedUpdate`, three output streams, and NOTHING IN THE TREE DEPENDING ON IT.
//
// ⛔ ================== EXCISABLE BY DESIGN (owner, 2026-09-03; S59 §6.1 Q5) ==================
// The whole recorder must be removable for a public release WITHOUT a refactor anywhere else. It is:
//   delete `plugin/src/pure/blackbox/`, delete `plugin/src/BlackBoxRecorder.cs`, delete
//   `plugin/test/BlackBoxTest.cs`, and remove ONE line from `plugin/test/TestMain.cs`.
// That last line is the harness's suite registry — `TestMain`'s own header says a suite is registered
// there "only once the module it proves is actually in the tree", so registering and unregistering is
// what that file is FOR. It is not a dependency on the recorder; it is the test runner naming a test.
//
// The dependency arrow points ONE WAY: BlackBox reads the tree, the tree never reads BlackBox. No file
// outside the four above mentions it, and BB1 added no accessor, no hook and no call site anywhere
// else. If removing the recorder ever turns out to be a refactor, BB1 failed its own constraint.
//
// ⛔ ========================= §4.8 — WHAT THIS MUST NEVER DO =========================
//   • NEVER FLY THE VEHICLE. Read-only. It never writes `FlightCtrlState`, never stages, never sets a
//     MechJeb field. §14.4(a)/(f)'s actuation boundary is untouched by this file.
//   • NEVER FABRICATE. No interpolation, no smoothing, no gap-filling, no "reasonable default". Blank.
//   • NEVER RE-SIMULATE. It records what the model produced; it does not produce it.
//   • NEVER MODIFY THE REPO. It writes only into `<KSP>/DragonScreen_capture/` — the deploy target,
//     already git-ignored, already where the screen PNGs go (§5 / C7: write-only runtime, not a source).
//   • NEVER BLOCK A FRAME. Buffered writer, no per-row file I/O, no synchronous large writes.
//   • ⭐ NEVER DRIVE A PROCESSOR (§4.7's load-bearing rule). It reads what is ALREADY computed. In
//     particular it must NOT call `KerBridge.RequestUpdate()` — `VesselData.cs:719` already drives
//     KER's fuel-flow solve on the screens' tick, and a second driver would double a whole-part-tree
//     simulation. `PageState.Ker` is read; nothing is requested.
//
// ---- HOST: WHY A [KSPAddon] AND NOT ScreenPainter ----
// §4.7: `ScreenPainter.OnPostRender` runs once per SCREEN (three times a frame), it DIES WITH THE IVA
// — which is exactly what happens at a booster hand-off, the one moment §B16.7 most needs recorded —
// and it keeps firing while KSP is PAUSED. It also cannot use `Time.realtimeSinceStartup` as a
// sampling clock for that last reason; a bug already fixed once in the power-flow clock
// (`VesselData.cs:147-158`). So: own addon, own `FixedUpdate`, UT as the sampling clock.
//
// ---- THE FOUR S76 DEFECTS THIS BUILD IS REQUIRED NOT TO REPEAT (BB1's register line) ----
//  (1) `torque_cmd` declared and never written  → `BlackBoxCoverage`, and the `Fit` taxonomy that makes
//      "blank" mean something specific per column. Reported as `rec.column_never_written` at close.
//  (2) `mode_holding` / `mode_flying` carried no state → same mechanism; and no column is declared here
//      without either a writer below or an explicit `Fit.Unfitted` naming the register line for it.
//  (3) The probe files ended in a TORN ROW (36 and 77 of 116 fields) which a reader silently turned
//      into a phantom row → four independent guards, see `Flush` and `Close` below.
//  (4) WARP ROWS POLLUTED EVERY STATISTIC with no way to exclude them → `warp_rate`/`warp_rails` on
//      every row (§2.1) and `BlackBoxVoid` blanking the control block, so a reader filters without
//      inferring anything.
// ============================================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using KSP.UI.Screens;   // StageManager — §2.3's `stage` column
using UnityEngine;

namespace DragonScreen.BlackBox
{
    /// <summary>
    /// The host. `FixedUpdate` because the R0 accumulators are physics-rate by definition and because
    /// a render-path hook keeps running while the game is paused. One per flight scene.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BlackBoxAddon : MonoBehaviour
    {
        void Start() { BlackBoxRecorder.SceneStart(); }
        void FixedUpdate() { BlackBoxRecorder.Tick(); }
        // Scene teardown is the ONE moment a torn row is most likely (S76 defect 3: the old streams cut
        // mid-line on a revert), so this is not a courtesy — it is the flush that makes the file whole.
        void OnDestroy() { BlackBoxRecorder.Close("scene_change"); }
    }

    public static class BlackBoxRecorder
    {
        const string Tag = "[DragonScreen/BlackBox] ";

        // ---- the kill switch + the one knob BB1-Q1 lands on (§4.7) ----
        // `Tuning` hot-reloads `[Tunable]` statics from PluginData/tuning.cfg at 1 Hz, so both of these
        // change between flights with no rebuild.
        [Tunable] public static bool Enabled = true;
        /// <summary>
        /// ⚠ BB1-Q1 IS OPEN (C1.14) and is NOT decided here. 0 = §2.0's ADAPTIVE ladder as SPECIFIED
        /// (10 Hz dynamic / 2 Hz quiescent), which is what BB1's done-criteria require. A positive value
        /// is a FIXED rate in Hz — so an owner ruling for option (b) fixed-10 or (c) fixed-5 is this one
        /// number, from a cfg edit, with no rebuild and no other file touched. Whichever is flown, the
        /// manifest records it, so no analyser ever has to assume a row period (§3.4 files assuming one
        /// as BREAK: five literal `.2`s in the old analyser, and Recorder B changed to 0.25 s).
        /// </summary>
        [Tunable] public static double FixedRowRateHz = 0.0;
        /// <summary>Explicit flush cadence (Recorder A's `FlushEvery = 25`). 25 rows = 2.5 s at 10 Hz.</summary>
        [Tunable] public static int FlushEveryRows = 25;
        /// <summary>Consecutive write failures before the recorder disables itself (§4.7).</summary>
        [Tunable] public static int SelfDisableAfter = 5;
        /// <summary>§4.4's hard ceiling. Should never be reached; present so an unattended run cannot fill a disk.</summary>
        [Tunable] public static double MaxFileMB = 512.0;

        static BlackBoxStream capsule;
        static bool disabled;
        static string captureDir;

        // ---- resource ids, resolved ONCE from the game-global library (composed from Recorder B) ----
        static bool resIdsResolved;
        static int mmhId, ntoId, ecId;

        public static bool Recording { get { return capsule != null && capsule.Open; } }

        // ============================== lifecycle ==============================

        public static void SceneStart()
        {
            // A new flight scene is a new host. Any stream left from the previous one belongs to a
            // vessel this scene may not even contain, so it is closed rather than adopted.
            Close("scene_start");
            disabled = false;
        }

        public static void Tick()
        {
            if (disabled || !Enabled) return;
            try
            {
                Vessel v = FlightGlobals.ActiveVessel;
                if (v == null || v.orbit == null) return;

                if (capsule == null || capsule.PersistentId != v.persistentId)
                {
                    // BB1 records ONE stream — the vessel that has the camera. A second, UNFOCUSED
                    // stream (the §B16 booster, which flies without the camera and would otherwise fly
                    // unrecorded) is register **BB2**, built on this core: it opens
                    // `<MissionId>.<Vessel>.params.csv` under the SAME mission id. The structure here
                    // is per-stream state in `Stream` precisely so that is an added instance and not a
                    // rewrite.
                    if (capsule != null) capsule.Emit(BlackBoxEvents.RecVesselChange,
                        new[] { Kv.Str("from", capsule.VesselName), Kv.Str("to", v.vesselName) });
                    OpenFor(v);
                }
                if (capsule == null || !capsule.Open) return;

                capsule.Tick(v);
            }
            catch (Exception e)
            {
                // The recorder must never take the flight down (§4.7). A failure OUTSIDE a row is
                // logged and swallowed; a failure building a ROW stops the stream (see BlackBoxStream.Tick).
                Debug.LogWarning(Tag + "tick failed: " + e.Message);
            }
        }

        public static void Close(string reason)
        {
            if (capsule == null) return;
            try { capsule.Close(reason); } catch (Exception e) { Debug.LogWarning(Tag + "close: " + e.Message); }
            capsule = null;
        }

        /// <summary>Stop everything for the rest of the session — §4.7's self-disable rung.</summary>
        static void Disable(string why)
        {
            if (capsule != null) capsule.Emit(BlackBoxEvents.RecSelfDisable, new[] { Kv.Str("why", why) });
            Debug.LogError(Tag + "SELF-DISABLED: " + why + ". No further recording this session.");
            Close("self_disable");
            disabled = true;
        }

        static void OpenFor(Vessel v)
        {
            Close("vessel_change");
            try
            {
                // §4.4 / §5: the deploy target, already git-ignored, already where the screen PNGs go.
                // Nothing in the repo is written by the recorder and no build ever reads from here.
                if (captureDir == null)
                    captureDir = Path.Combine(KSPUtil.ApplicationRootPath, "DragonScreen_capture");
                Directory.CreateDirectory(captureDir);
                capsule = new BlackBoxStream(captureDir, v, Policy());
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "could not open a recording: " + e.Message);
                capsule = null;
            }
        }

        internal static RatePolicy Policy()
        {
            return FixedRowRateHz > 0.0 ? RatePolicy.Fixed(FixedRowRateHz) : RatePolicy.Adaptive();
        }

        internal static void ReportWriteFailure(int consecutive, string why)
        {
            if (consecutive >= SelfDisableAfter) Disable(why);
        }

        // ============================== shared reads ==============================
        // Everything below is a DIRECT read of live KSP state, so the load-bearing physics columns do
        // not depend on the screens being alive: a booster hand-off destroys the IVA, and that is
        // exactly the moment §B16.7 wants recorded.

        internal static void ResolveResIds()
        {
            if (resIdsResolved) return;
            try
            {
                PartResourceLibrary lib = PartResourceLibrary.Instance;
                if (lib == null) return;   // not ready yet — retry next tick
                PartResourceDefinition m = lib.GetDefinition("MMH"); mmhId = (m != null) ? m.id : 0;
                PartResourceDefinition n = lib.GetDefinition("NTO"); ntoId = (n != null) ? n.id : 0;
                PartResourceDefinition e = lib.GetDefinition("ElectricCharge"); ecId = (e != null) ? e.id : 0;
                resIdsResolved = true;
            }
            catch { }
        }

        internal static int MmhId { get { return mmhId; } }
        internal static int NtoId { get { return ntoId; } }
        internal static int EcId { get { return ecId; } }

        /// <summary>
        /// Connected fraction [0,1] of a resource. NaN — hence BLANK, not zero (§4.6) — when the id is
        /// unset (the install does not define it) or the vessel carries no capacity for it. "This
        /// vehicle has no MMH" and "this vehicle's MMH is empty" are different facts.
        /// </summary>
        internal static double ResFrac(Vessel v, int id)
        {
            if (id == 0 || v == null) return double.NaN;
            try
            {
                double amt, max;
                v.GetConnectedResourceTotals(id, out amt, out max, true);
                return (max > 1e-9) ? amt / max : double.NaN;
            }
            catch { return double.NaN; }
        }
    }

    // ============================================================================================
    // ONE RECORDED VESSEL = ONE STREAM. All per-stream state lives here, so BB2's second (unfocused)
    // vessel is a second instance and not a second code path.
    // ============================================================================================
    internal sealed class BlackBoxStream
    {
        const string Tag = "[DragonScreen/BlackBox] ";

        readonly string dir;
        readonly RatePolicy policy;

        StreamWriter csv, events;
        readonly StringBuilder pending = new StringBuilder(8192);
        int pendingRows;

        public string MissionId { get; private set; }
        public string VesselName { get; private set; }
        public uint PersistentId { get; private set; }
        public bool Open { get { return csv != null; } }

        long seq;
        long eventsWritten;
        int writeErrors, consecutiveWriteErrors;
        double maxRecBuildUs;
        bool widthChecked;
        bool closing;

        RateState rate = RateState.Fresh();
        BlackBoxAccum accum = BlackBoxAccum.Fresh();
        readonly BlackBoxCoverage coverage = new BlackBoxCoverage();
        ManifestInfo manifest;

        // ---- launch reference for downrange (composed from Recorder B's FlightLog) ----
        double launchLat, launchLon;
        bool haveLaunchRef;

        // ---- edge state for the event log (§2.9). Every one of these exists because a TRANSITION is a
        // ---- fact with a time (§1.4(d)) and quantising it to the row period throws the narrative away.
        double lastUt = double.NaN;
        double lastMet;
        double lastPhysUt = double.NaN;
        MissionPhase lastPhase = (MissionPhase)255;
        ControlMode lastMode = (ControlMode)255;
        double lastWarpRate = -1.0;
        string lastFocus;
        int lastStage = int.MinValue;
        int lastIgnited = -1, lastFlameout = -1;
        int lastAlarmMask = -1;
        bool lastBus1, lastBus2, haveBusState;
        bool lastFire, lastLeak;
        bool liftoffSeen, maxQSeen, droguesSeen, mainsSeen, downSeen;
        double peakQSoFar;
        int lastPageL = -1, lastPageC = -1, lastPageR = -1;

        // ---- rate-limited thermal detail, composed from Recorder B ----
        double lastThermalLogUt = -1e9;

        public BlackBoxStream(string dir, Vessel v, RatePolicy policy)
        {
            this.dir = dir;
            this.policy = policy;
            OpenFiles(v, null);
        }

        // ============================== files ==============================

        void OpenFiles(Vessel v, string revertSuffix)
        {
            // §4.4: `<SanitizedVesselName>_<yyyyMMdd_HHmmss>`. Keeps the existing `Crew-2*` glob in
            // `plugin/tools/assess_flight.py` working and groups the three files by prefix. The id is
            // ALSO a column on every row, so a file that is moved or renamed still self-identifies —
            // which is the fix for "a recording is half a mission".
            VesselName = v.vesselName;
            PersistentId = v.persistentId;
            MissionId = Sanitize(v.vesselName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                      + (revertSuffix ?? "");

            string stem = Path.Combine(dir, MissionId);
            csv = new StreamWriter(stem + ".params.csv", false, new UTF8Encoding(false));
            csv.NewLine = "\n";                       // §4.1: UTF-8, \n, RFC-4180
            csv.WriteLine(BlackBoxSchema.Header());
            csv.Flush();

            events = new StreamWriter(stem + ".events.jsonl", false, new UTF8Encoding(false));
            events.NewLine = "\n";

            seq = 0;
            rate = RateState.Fresh();
            accum = BlackBoxAccum.Fresh();
            widthChecked = false;
            closing = false;   // a revert reopens THIS instance — without this, the new stream could never close
            pending.Length = 0; pendingRows = 0;

            launchLat = v.latitude; launchLon = v.longitude; haveLaunchRef = true;
            // so `rec.open` carries the real clock rather than 0 — an event with a wrong ut is
            // worse than one with none, because it is joinable to the wrong row.
            lastUt = Planetarium.GetUniversalTime();
            lastMet = v.missionTime;

            BuildManifest(v);
            WriteManifest();

            Emit(BlackBoxEvents.RecOpen, new[]
            {
                Kv.Str("mission_id", MissionId),
                Kv.Str("vessel", VesselName),
                Kv.Int("schema_version", BlackBoxSchema.SchemaVersion),
                Kv.Int("columns", BlackBoxSchema.Width),
                Kv.Str("row_rate_mode", policy.Mode == RateMode.Fixed ? "fixed" : "adaptive"),
                Kv.Num("row_rate_dynamic_hz", policy.RowRateDynamicHz),
                Kv.Num("row_rate_quiescent_hz", policy.RowRateQuiescentHz),
            });
            Debug.Log(Tag + "recording -> " + MissionId + ".{params.csv,events.jsonl,manifest.json}  ("
                      + BlackBoxSchema.Width + " columns, schema v" + BlackBoxSchema.SchemaVersion + ")");
        }

        public void Close(string reason)
        {
            if (csv == null || closing) return;
            closing = true;
            try
            {
                // ---- S76 DEFECT 3, GUARD 1: the last thing in the event log says the stream ENDED.
                // A reader that finds no `rec.stream_end` knows the recording was cut, and knows it
                // WITHOUT having to guess from a short final CSV line.
                Emit(BlackBoxEvents.RecStreamEnd, new[] { Kv.Str("reason", reason), Kv.Int("rows", (int)seq) });

                Flush();
                csv.Flush(); csv.Close();
                events.Flush(); events.Close();
            }
            catch (Exception e) { Debug.LogWarning(Tag + "close: " + e.Message); }
            csv = null; events = null;

            try
            {
                // ---- S76 DEFECT 1/2: the ghost-column verdict, computed once, into the manifest. ----
                manifest.Closed = true;
                manifest.ClosedReason = reason;
                manifest.ClosedUt = double.IsNaN(lastUt) ? 0.0 : lastUt;
                manifest.RowsWritten = seq;
                manifest.EventsWritten = eventsWritten;
                manifest.WriteErrors = writeErrors;
                manifest.MaxRecBuildUs = maxRecBuildUs;
                manifest.Coverage = coverage.Findings();
                WriteManifest();

                int defects = 0;
                for (int i = 0; i < manifest.Coverage.Count; i++)
                {
                    CoverageFinding f = manifest.Coverage[i];
                    if (!f.Defect) continue;
                    defects++;
                    // LogError, not LogWarning: a declared column with no writer is the defect that took
                    // an audit of the whole corpus to find last time. It goes in the log LOUDLY.
                    Debug.LogError(Tag + "COVERAGE DEFECT (" + f.Kind + "): column '" + f.Column
                                   + "' — " + f.Declared);
                }
                Debug.Log(Tag + "closed " + MissionId + " (" + reason + "): " + seq + " rows, "
                          + eventsWritten + " events, " + writeErrors + " write error(s), "
                          + defects + " coverage defect(s), max rec_build "
                          + maxRecBuildUs.ToString("F0", CultureInfo.InvariantCulture) + " us");
            }
            catch (Exception e) { Debug.LogWarning(Tag + "manifest finalise: " + e.Message); }
        }

        void WriteManifest()
        {
            try
            {
                File.WriteAllText(Path.Combine(dir, MissionId + ".manifest.json"),
                                  BlackBoxManifest.Build(manifest), new UTF8Encoding(false));
            }
            catch (Exception e) { Debug.LogWarning(Tag + "manifest write: " + e.Message); }
        }

        // ============================== the tick ==============================

        public void Tick(Vessel v)
        {
            double ut = Planetarium.GetUniversalTime();
            double wall = Time.realtimeSinceStartup;

            // ---- §4.4: a REVERT is UT moving backwards. Recorder A's chainer had to detect this as
            // ---- "a negative gap"; making it explicit retires the heuristic AND closes the old stream
            // ---- cleanly, which is the fix for S76 defect 3 (the probe files cut mid-line on revert).
            if (!double.IsNaN(lastUt) && ut < lastUt - 1.0)
            {
                double from = lastUt;
                Emit(BlackBoxEvents.RecRevert, new[] { Kv.Num("from_ut", from), Kv.Num("to_ut", ut) });
                string nextSuffix = NextRevertSuffix(MissionId);
                Close("revert");
                try { OpenFiles(v, nextSuffix); }
                catch (Exception e) { Debug.LogWarning(Tag + "revert reopen: " + e.Message); return; }
                lastUt = ut;
                return;
            }

            // ---- R0: accumulate EVERY physics tick, whatever the row cadence is (§2.0). ----
            AccumulateTick(v, ut);

            bool rails = RailsWarp();
            RateInputs now;
            now.Ut = ut; now.Wall = wall; now.RailsWarp = rails;

            MissionPhase classified = Classify(v);
            double thrustN, availN; int ignited, flameout;
            EngineState(v, out ignited, out flameout, out thrustN, out availN);
            FlightCtrlState cs = v.ctrlState;
            bool transCmd = cs != null && (Math.Abs(cs.X) + Math.Abs(cs.Y) + Math.Abs(cs.Z)) > BlackBoxAccum.CommandEpsilon;
            bool hasTarget = v.targetObject != null;
            double tgtRange = hasTarget ? TargetRange(v) : double.NaN;

            now.Dynamic = BlackBoxRate.IsDynamic(classified, FlightDriver.Aborting, thrustN,
                                                 transCmd, tgtRange, hasTarget);

            // ---- edge events are latched at the instant they are DETECTED, with their OWN ut, not
            // ---- the next row's (§2.9's sub-frame edge latching). So they run BEFORE the row gate.
            lastMet = v.missionTime;
            DetectEvents(v, ut, classified, ignited, flameout, thrustN, rails);
            lastUt = ut;

            RowPlan plan = BlackBoxRate.Plan(policy, rate, now);
            if (!plan.Due) return;

            long t0 = TickNow();
            string[] row;
            try
            {
                row = BuildRow(v, ut, wall, plan, rails, classified, thrustN, availN, ignited, flameout,
                               hasTarget, tgtRange);
            }
            catch (Exception e)
            {
                // ⛔ Recorder A's rule, kept: A ROW THAT THROWS STOPS THE RECORDER rather than writing
                // garbage. A half-built row is a torn row with extra steps, and a torn row that looks
                // well-formed is worse than no recording at all.
                Emit(BlackBoxEvents.RecWriteError, new[] { Kv.Str("where", "row"), Kv.Str("error", e.Message) });
                Debug.LogError(Tag + "row build failed, stopping this recording: " + e);
                Close("row_failed");
                return;
            }

            double us = ElapsedUs(t0);
            if (us > maxRecBuildUs) maxRecBuildUs = us;
            BlackBoxSchema.Set(row, BlackBoxCols.RecBuildUs, us);

            // ---- §4.6, LAST so nothing can re-fill a voided cell: on rails the physics loop is OFF
            // ---- and every control value is a frozen stale read. BLANK, not zero — a zero is a
            // ---- legitimate control value and a blank is not.
            if (rails) BlackBoxVoid.Apply(row);

            coverage.Note(row);
            WriteRow(row);
            accum = BlackBoxAccum.Fresh();
            rate = BlackBoxRate.Advance(rate, plan, now);
        }

        // ============================== the row ==============================

        string[] BuildRow(Vessel v, double ut, double wall, RowPlan plan, bool rails,
                          MissionPhase classified, double thrustN, double availN,
                          int ignited, int flameout, bool hasTarget, double tgtRange)
        {
            string[] c = BlackBoxSchema.NewRow();
            Orbit o = v.orbit;
            FlightCtrlState cs = v.ctrlState;
            CelestialBody body = v.mainBody;

            // ---------- A: every row, unconditionally (§2.1) ----------
            seq++;
            BlackBoxSchema.Set(c, BlackBoxCols.MissionId, MissionId);
            BlackBoxSchema.Set(c, BlackBoxCols.Seq, (double)seq);
            BlackBoxSchema.Set(c, BlackBoxCols.Ut, ut);
            BlackBoxSchema.Set(c, BlackBoxCols.MetS, v.missionTime);
            BlackBoxSchema.Set(c, BlackBoxCols.WallS, wall);
            BlackBoxSchema.Set(c, BlackBoxCols.WarpRate, TimeWarp.CurrentRate);
            BlackBoxSchema.Set(c, BlackBoxCols.WarpRails, rails);
            BlackBoxSchema.Set(c, BlackBoxCols.Vessel, v.vesselName);
            Vessel act = FlightGlobals.ActiveVessel;
            BlackBoxSchema.Set(c, BlackBoxCols.Focus, act != null ? act.vesselName : "");
            // rec_build_us is set by the caller — it cannot be known until the row is finished.

            // ---------- B: R1 dynamic ----------
            BlackBoxSchema.Set(c, BlackBoxCols.Mach, v.mach);
            BlackBoxSchema.Set(c, BlackBoxCols.QPa, v.dynamicPressurekPa * 1000.0);
            BlackBoxSchema.Set(c, BlackBoxCols.AccelG, v.geeForce);
            Transform rt = v.ReferenceTransform;
            if (rt != null)
                BlackBoxSchema.Set(c, BlackBoxCols.AccelAxialG,
                    Vector3d.Dot(v.acceleration, (Vector3d)rt.up) / 9.80665);
            double pitch, heading, roll;
            if (SurfaceAttitude(v, out pitch, out heading, out roll))
            {
                BlackBoxSchema.Set(c, BlackBoxCols.PitchDeg, pitch);
                BlackBoxSchema.Set(c, BlackBoxCols.HeadingDeg, heading);
                BlackBoxSchema.Set(c, BlackBoxCols.RollDeg, roll);
            }
            double aoa, aos;
            if (FlowAngles(v, out aoa, out aos))
            {
                BlackBoxSchema.Set(c, BlackBoxCols.AoaDeg, aoa);
                BlackBoxSchema.Set(c, BlackBoxCols.AosDeg, aos);
            }
            Vector3 av = v.angularVelocity * Mathf.Rad2Deg;
            BlackBoxSchema.Set(c, BlackBoxCols.RatePitchDps, av.x);
            BlackBoxSchema.Set(c, BlackBoxCols.RateRollDps, av.y);
            BlackBoxSchema.Set(c, BlackBoxCols.RateYawDps, av.z);
            BlackBoxSchema.Set(c, BlackBoxCols.AttRateMeas, av.magnitude);

            // ---------- R0: the accumulated tier, emitted with the row ----------
            accum.Put(c);

            // ---------- C: propulsion, R1 ----------
            if (cs != null) BlackBoxSchema.Set(c, BlackBoxCols.Throttle, cs.mainThrottle);
            BlackBoxSchema.Set(c, BlackBoxCols.ThrustN, thrustN);
            BlackBoxSchema.Set(c, BlackBoxCols.EngIgnited, ignited);
            BlackBoxSchema.Set(c, BlackBoxCols.EngFlameout, flameout);

            // ---------- D: applied actuation, R1 ----------
            // ⛔ APPLIED, not delivered and not requested (§2.4's three kinds). This is what was written
            // to FlightCtrlState, whoever wrote it. KSP's RCS solver owns delivered force, and these are
            // per-tick snapshots that ALIAS the pulse dwell — the acc_* block above is the un-aliased basis.
            if (cs != null)
            {
                BlackBoxSchema.Set(c, BlackBoxCols.AppPitch, cs.pitch);
                BlackBoxSchema.Set(c, BlackBoxCols.AppYaw, cs.yaw);
                BlackBoxSchema.Set(c, BlackBoxCols.AppRoll, cs.roll);
                BlackBoxSchema.Set(c, BlackBoxCols.AppTx, cs.X);
                BlackBoxSchema.Set(c, BlackBoxCols.AppTy, cs.Y);
                BlackBoxSchema.Set(c, BlackBoxCols.AppTz, cs.Z);
            }

            // ---------- ⭐ the booster observability block (owner Q2 refinement, 2026-09-04) ----------
            PutBooster(c, v);

            // ---------- H: R1 abort ----------
            BlackBoxSchema.Set(c, BlackBoxCols.Aborting, FlightDriver.Aborting);
            BlackBoxSchema.Set(c, BlackBoxCols.AbortMode, AbortControl.Mode.ToString());
            if (hasTarget) BlackBoxSchema.Set(c, BlackBoxCols.ClosingMps, ClosingRate(v));

            // ================= R2: the state block =================
            if (plan.FillR2)
            {
                BlackBoxSchema.Set(c, BlackBoxCols.AltM, v.altitude);
                BlackBoxSchema.Set(c, BlackBoxCols.AltRadarM, v.radarAltitude);
                BlackBoxSchema.Set(c, BlackBoxCols.SpeedMps, v.obt_speed);
                BlackBoxSchema.Set(c, BlackBoxCols.SrfSpeedMps, v.srfSpeed);
                BlackBoxSchema.Set(c, BlackBoxCols.VspeedMps, v.verticalSpeed);
                BlackBoxSchema.Set(c, BlackBoxCols.LatDeg, v.latitude);
                BlackBoxSchema.Set(c, BlackBoxCols.LonDeg, v.longitude);
                BlackBoxSchema.Set(c, BlackBoxCols.DownrangeM, DownrangeM(v));
                BlackBoxSchema.Set(c, BlackBoxCols.AtmDensity, v.atmDensity);
                BlackBoxSchema.Set(c, BlackBoxCols.MassKg, v.totalMass * 1000.0);
                Vector3 moi = v.MOI;
                BlackBoxSchema.Set(c, BlackBoxCols.MoiPitch, moi.x);
                BlackBoxSchema.Set(c, BlackBoxCols.MoiRoll, moi.y);
                BlackBoxSchema.Set(c, BlackBoxCols.MoiYaw, moi.z);
                if (o != null)
                {
                    BlackBoxSchema.Set(c, BlackBoxCols.ApKm, o.ApA / 1000.0);
                    BlackBoxSchema.Set(c, BlackBoxCols.PeKm, o.PeA / 1000.0);
                    BlackBoxSchema.Set(c, BlackBoxCols.IncDeg, o.inclination);
                    BlackBoxSchema.Set(c, BlackBoxCols.RaanDeg, o.LAN);
                    BlackBoxSchema.Set(c, BlackBoxCols.Ecc, o.eccentricity);
                    BlackBoxSchema.Set(c, BlackBoxCols.SmaM, o.semiMajorAxis);
                    BlackBoxSchema.Set(c, BlackBoxCols.ArgpDeg, o.argumentOfPeriapsis);
                    BlackBoxSchema.Set(c, BlackBoxCols.TaDeg, o.trueAnomaly);
                    BlackBoxSchema.Set(c, BlackBoxCols.PeriodS, o.period);
                    BlackBoxSchema.Set(c, BlackBoxCols.TApS, o.timeToAp);
                    BlackBoxSchema.Set(c, BlackBoxCols.TPeS, o.timeToPe);
                }
                BlackBoxSchema.Set(c, BlackBoxCols.Stage, StageManager.CurrentStage);
                BlackBoxSchema.Set(c, BlackBoxCols.RcsOn, v.ActionGroups[KSPActionGroup.RCS]);

                BlackBoxRecorder.ResolveResIds();
                BlackBoxSchema.Set(c, BlackBoxCols.EcFrac, BlackBoxRecorder.ResFrac(v, BlackBoxRecorder.EcId));
                BlackBoxSchema.Set(c, BlackBoxCols.MmhFrac, BlackBoxRecorder.ResFrac(v, BlackBoxRecorder.MmhId));
                BlackBoxSchema.Set(c, BlackBoxCols.NtoFrac, BlackBoxRecorder.ResFrac(v, BlackBoxRecorder.NtoId));

                double tqP, tqY, tqR, rcsN;
                Authority(v, out tqP, out tqY, out tqR, out rcsN);
                BlackBoxSchema.Set(c, BlackBoxCols.CtrlTqPitch, tqP);
                BlackBoxSchema.Set(c, BlackBoxCols.CtrlTqYaw, tqY);
                BlackBoxSchema.Set(c, BlackBoxCols.CtrlTqRoll, tqR);
                BlackBoxSchema.Set(c, BlackBoxCols.RcsThrustN, rcsN);

                double frac, tempC;
                if (HottestSkin(v, ut, out frac, out tempC))
                {
                    BlackBoxSchema.Set(c, BlackBoxCols.SkinTempFrac, frac);
                    BlackBoxSchema.Set(c, BlackBoxCols.HullTempC, tempC);
                }

                // ---- E/F: the idle seams. §2.5 is explicit that recording the CONSTANT is itself the
                // ---- proof the seam was idle — so these are Live columns with a real (constant) writer,
                // ---- not Unfitted ones. On the day §B12.5 flips one, the column starts moving and the
                // ---- file shows exactly when.
                BlackBoxSchema.Set(c, BlackBoxCols.GncEngaged, AutoPilot.Engaged);
                BlackBoxSchema.Set(c, BlackBoxCols.ModeIndex, FlightDriver.MissionMode.ToString());
                MissionPhase authoritative = Mission.AuthoritativePhase(
                    CrewProcedureOps.Engaged, CrewProcedureOps.ActivePhase, classified);
                BlackBoxSchema.Set(c, BlackBoxCols.MissionPhase, Mission.Name(authoritative));
                // ⭐ Recorded SEPARATELY from the authoritative phase (§2.6) so a conductor/classifier
                // disagreement is VISIBLE rather than resolved silently — a (b)-class independent
                // cross-check on our own FSM, for the price of one column.
                BlackBoxSchema.Set(c, BlackBoxCols.PhaseClassified, Mission.Name(classified));

                Gate g = CrewProcedureOps.CurrentGate();
                ProcState pr = CrewProcedureOps.Proc;
                BlackBoxSchema.Set(c, BlackBoxCols.GateId, g.Id.ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.GatePhase, pr.Phase.ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.CrewAction, CrewProcedureOps.CrewActionNeeded());
                BlackBoxSchema.Set(c, BlackBoxCols.GateSatisfiedMask, PackBits(pr.Satisfied));

                FdirReport fr = FlightDriver.LastFdirReport;
                BlackBoxSchema.Set(c, BlackBoxCols.FdirFault, fr.Fault.ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.FdirRecovery, fr.Response.ToString());

                SystemsState sys = FlightCommands.State;
                BlackBoxSchema.Set(c, BlackBoxCols.Bus1On, sys.Bus1On);
                BlackBoxSchema.Set(c, BlackBoxCols.Bus2On, sys.Bus2On);
                BlackBoxSchema.Set(c, BlackBoxCols.StrA1, sys.A1.ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.StrB1, sys.B1.ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.StrC1, sys.C1.ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.StrA2, sys.A2.ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.StrB2, sys.B2.ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.StrC2, sys.C2.ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.FireIntensity, sys.FireIntensity);
                BlackBoxSchema.Set(c, BlackBoxCols.Suppressant, sys.Suppressant);
                BlackBoxSchema.Set(c, BlackBoxCols.LeakRate, sys.LeakRate);
                BlackBoxSchema.Set(c, BlackBoxCols.Isolating, sys.Isolating);

                if (hasTarget) BlackBoxSchema.Set(c, BlackBoxCols.RangeM, tgtRange);

                PutScreens(c, v, false);
            }

            // ================= R3: the slow block =================
            if (plan.FillR3)
            {
                if (body != null) BlackBoxSchema.Set(c, BlackBoxCols.Body, body.bodyName);
                SystemsState sys = FlightCommands.State;
                BlackBoxSchema.Set(c, BlackBoxCols.O2Store, sys.Oxygen);
                BlackBoxSchema.Set(c, BlackBoxCols.N2Store, sys.Nitrogen);
                BlackBoxSchema.Set(c, BlackBoxCols.CanisterUsed, sys.CanisterUsed);
                BlackBoxSchema.Set(c, BlackBoxCols.IsReturn, CrewProcedureOps.IsReturn);

                CommNet.CommNetVessel conn = null;
                try { conn = CommNet.CommNetScenario.CommNetEnabled ? v.Connection : null; } catch { }
                BlackBoxSchema.Set(c, BlackBoxCols.CommLinked, conn != null && conn.IsConnected);
                if (conn != null) BlackBoxSchema.Set(c, BlackBoxCols.CommSignal, conn.SignalStrength);

                // ---- TAC-LS via the bridge that already exists. `ls_present` is Live and always
                // written (§1.4(e): log WHETHER there is a result, so a blank is never read as a zero);
                // the margins themselves are Conditional on it.
                LsMargins ls = default(LsMargins);
                try { ls = LifeSupportBridge.Margins(v); } catch { }
                BlackBoxSchema.Set(c, BlackBoxCols.LsPresent, ls.Present);
                if (ls.Present)
                {
                    BlackBoxSchema.Set(c, BlackBoxCols.LsO2Days, ls.OxygenDays);
                    BlackBoxSchema.Set(c, BlackBoxCols.LsWaterDays, ls.WaterDays);
                    BlackBoxSchema.Set(c, BlackBoxCols.LsFoodDays, ls.FoodDays);
                    BlackBoxSchema.Set(c, BlackBoxCols.LsLimitingDays, ls.LimitingDays);
                }

                PutScreens(c, v, true);
            }

            return c;
        }

        /// <summary>
        /// The columns whose source is the SCREENS' own 5 Hz pass (`VesselData.State`).
        ///
        /// ⛔ THE GUARD IS THE POINT. `PageState` is a struct copy of work done for the FOCUSED vessel
        /// while the IVA is alive. Recording it for an unfocused vessel, or after the IVA has died,
        /// writes a STALE COPY OF SOMEBODY ELSE'S STATE and calls it a measurement — which is the same
        /// class of error as the frozen-under-warp control values that manufactured a phantom RCS
        /// thrash. So: focused vessel + `Valid` only, and BLANK otherwise. §4.6's "no signal" state.
        /// </summary>
        void PutScreens(string[] c, Vessel v, bool slow)
        {
            Vessel act = FlightGlobals.ActiveVessel;
            if (act == null || act.persistentId != v.persistentId) return;
            PageState ps = VesselData.State;
            if (!ps.Valid) return;

            if (!slow)
            {
                BlackBoxSchema.Set(c, BlackBoxCols.PropFrac, ps.Propellant01);
                BlackBoxSchema.Set(c, BlackBoxCols.CabinPsia, ps.Cabin.PressPsia);
                BlackBoxSchema.Set(c, BlackBoxCols.StepAckMask, ps.Steps.Acknowledged);
                BlackBoxSchema.Set(c, BlackBoxCols.SevSystem, Alarms.SystemSeverity(ps).ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.SevVehicle, Alarms.VehicleSeverity(ps).ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.SevLs, Alarms.LifeSupport(ps.Cabin).ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.SevThermal, Alarms.Thermal(ps.Cabin).ToString());
                BlackBoxSchema.Set(c, BlackBoxCols.AlarmMask, Alarms.Mask(ps));
                if (ps.ScreenPages != null && ps.ScreenPages.Length >= 4)
                {
                    BlackBoxSchema.Set(c, BlackBoxCols.PageL, ps.ScreenPages[1]);
                    BlackBoxSchema.Set(c, BlackBoxCols.PageC, ps.ScreenPages[2]);
                    BlackBoxSchema.Set(c, BlackBoxCols.PageR, ps.ScreenPages[3]);
                }
                if (ps.HasTarget)
                {
                    BlackBoxSchema.Set(c, BlackBoxCols.AlignDeg, ps.Align01 * 90.0);
                    BlackBoxSchema.Set(c, BlackBoxCols.RollErrDeg, ps.RollDeg);
                    BlackBoxSchema.Set(c, BlackBoxCols.PitchErrDeg, ps.PitchDeg);
                    BlackBoxSchema.Set(c, BlackBoxCols.YawErrDeg, ps.YawDeg);
                }
            }
            else
            {
                BlackBoxSchema.Set(c, BlackBoxCols.Ppo2Psia, ps.Cabin.Ppo2Psia);
                BlackBoxSchema.Set(c, BlackBoxCols.Co2Mmhg, ps.Cabin.Co2MmHg);
                BlackBoxSchema.Set(c, BlackBoxCols.CabinTempC, ps.Cabin.CabinTempC);
                BlackBoxSchema.Set(c, BlackBoxCols.LoopAC, ps.Cabin.LoopAC);
                BlackBoxSchema.Set(c, BlackBoxCols.LoopBC, ps.Cabin.LoopBC);
                BlackBoxSchema.Set(c, BlackBoxCols.CamView, ps.CameraView);

                // ⛔ READ ONLY. KER's fuel-flow solve is ALREADY driven by `VesselData.cs:719` on the
                // screens' tick; calling `KerBridge.RequestUpdate()` here would double a whole-part-tree
                // simulation, which is precisely what §4.7's zero-new-computation rule forbids.
                KerPerformance k = ps.Ker;
                BlackBoxSchema.Set(c, BlackBoxCols.KerAvail, k.HasResult);
                if (k.HasResult)
                {
                    BlackBoxSchema.Set(c, BlackBoxCols.KerStageDv, k.DeltaVMps);
                    BlackBoxSchema.Set(c, BlackBoxCols.KerTotalDv, k.RemainingDeltaVMps);
                    BlackBoxSchema.Set(c, BlackBoxCols.KerTwr, k.Twr);
                    BlackBoxSchema.Set(c, BlackBoxCols.KerIsp, k.IspS);
                    BlackBoxSchema.Set(c, BlackBoxCols.KerBurnS, k.BurnTimeS);
                    BlackBoxSchema.Set(c, BlackBoxCols.KerStageMassKg, k.StageMassKg);
                    BlackBoxSchema.Set(c, BlackBoxCols.KerThrustAvailN, k.ThrustN);
                }
            }
        }

        /// <summary>
        /// ⭐ THE OWNER'S Q2 OBSERVABILITY REFINEMENT (2026-09-04), read at last.
        ///
        /// `pure/BoosterSteer.cs`'s header names THIS register line as the reader for the deadband seam,
        /// in as many words: "a knob enable-able from config that never appears in a recording cannot be
        /// diagnosed". `src/BoosterHost.cs` publishes the state read-only so the steering law never had
        /// to invent a recording channel of its own; this is the channel.
        ///
        /// `DeadbandDeg` defaults to 0.0 — behaviourally identical to no deadband — so on a default
        /// flight these read 0/0/0 and 0.0. THAT IS THE POINT: an inert seam that is RECORDED as inert
        /// is provably inert, and one that is merely believed inert is not.
        /// </summary>
        void PutBooster(string[] c, Vessel v)
        {
            Vessel b = null;
            try { b = BoosterHost.Booster; } catch { }
            if (b == null || b.persistentId != v.persistentId) return;   // §4.6: not this vessel → blank

            BlackBoxSchema.Set(c, BlackBoxCols.BoostDbPitch, BoosterHost.SteerPitchDeadbanded);
            BlackBoxSchema.Set(c, BlackBoxCols.BoostDbYaw, BoosterHost.SteerYawDeadbanded);
            BlackBoxSchema.Set(c, BlackBoxCols.BoostDbRoll, BoosterHost.SteerRollDeadbanded);
            BlackBoxSchema.Set(c, BlackBoxCols.BoostDbDeg, BoosterHost.SteerDeadbandDeg);
            BlackBoxSchema.Set(c, BlackBoxCols.BoostSteerPitch, BoosterHost.SteerPitch);
            BlackBoxSchema.Set(c, BlackBoxCols.BoostSteerYaw, BoosterHost.SteerYaw);
            BlackBoxSchema.Set(c, BlackBoxCols.BoostSteerRoll, BoosterHost.SteerRoll);
            BlackBoxSchema.Set(c, BlackBoxCols.BoostThrottle, BoosterHost.Throttle);
            BlackBoxSchema.Set(c, BlackBoxCols.BoostPhase, BoosterHost.Phase.ToString());
            BlackBoxSchema.Set(c, BlackBoxCols.BoostUncommanded, BoosterHost.AttitudeUncommanded);
        }

        // ============================== writing ==============================

        void WriteRow(string[] row)
        {
            try
            {
                string line = BlackBoxSchema.Row(row);

                // ---- S76 DEFECT 3, GUARD 2: VERIFY THE WIDTH ONCE, IN THE GAME. ----
                // ⛔ A ROW THAT IS NOT AS WIDE AS THE HEADER IS WORSE THAN NO RECORDING AT ALL — every
                // column after the mismatch is labelled with its neighbour's name and the file looks
                // perfectly well-formed while telling you the throttle was 29 000. Nothing headless can
                // catch it: the row is built from live vessel state. The width is STRUCTURAL, so once
                // is enough — if the first row matches, they all do.
                if (!widthChecked)
                {
                    widthChecked = true;
                    int got = BlackBoxSchema.CountFields(line);
                    if (got != BlackBoxSchema.Width)
                    {
                        Emit(BlackBoxEvents.RecWidthMismatch,
                             new[] { Kv.Int("header", BlackBoxSchema.Width), Kv.Int("row", got) });
                        Debug.LogError(Tag + "WIDTH MISMATCH: header has " + BlackBoxSchema.Width
                                       + " fields, first row has " + got + ". Stopping — a misaligned "
                                       + "recording is worse than none.");
                        Close("width_mismatch");
                        return;
                    }
                }

                // ---- S76 DEFECT 3, GUARD 3: only COMPLETE lines ever enter the buffer. The pending
                // buffer is appended a whole line at a time, so a flush can never emit half a row —
                // the only remaining tear window is a process kill inside `StreamWriter.Write`.
                pending.Append(line).Append('\n');
                consecutiveWriteErrors = 0;
                if (++pendingRows >= FlushEveryRowsSafe()) Flush();
            }
            catch (Exception e) { OnWriteError("row", e); }
        }

        int FlushEveryRowsSafe()
        {
            int n = BlackBoxRecorder.FlushEveryRows;
            return n < 1 ? 1 : n;
        }

        /// <summary>
        /// Explicit flush. §3.4: Recorder B relied on implicit `StreamWriter` buffering plus `Close()`
        /// and that was a REGRESSION — an unexpected end loses the end, which is the part that matters.
        /// Recorder A's every-25-rows rule is composed back in; at 10 Hz that bounds the loss at 2.5 s.
        /// </summary>
        void Flush()
        {
            if (csv == null || pending.Length == 0) return;
            try
            {
                csv.Write(pending.ToString());
                csv.Flush();
                pending.Length = 0;
                pendingRows = 0;
                CheckRotate();
            }
            catch (Exception e) { OnWriteError("flush", e); }
        }

        void OnWriteError(string where, Exception e)
        {
            writeErrors++;
            consecutiveWriteErrors++;
            Debug.LogWarning(Tag + "write failed (" + where + "): " + e.Message);
            try
            {
                Emit(BlackBoxEvents.RecWriteError,
                     new[] { Kv.Str("where", where), Kv.Str("error", e.Message),
                             Kv.Int("consecutive", consecutiveWriteErrors) });
            }
            catch { }
            BlackBoxRecorder.ReportWriteFailure(consecutiveWriteErrors, where + ": " + e.Message);
        }

        /// <summary>§4.4's hard ceiling. Not expected to fire; present so an unattended run cannot fill a disk.</summary>
        void CheckRotate()
        {
            try
            {
                // Never during a close: Close -> Flush -> CheckRotate -> Close would recurse.
                if (closing || BlackBoxRecorder.MaxFileMB <= 0.0) return;
                var fi = new FileInfo(Path.Combine(dir, MissionId + ".params.csv"));
                if (!fi.Exists || fi.Length < BlackBoxRecorder.MaxFileMB * 1024.0 * 1024.0) return;
                Emit(BlackBoxEvents.RecRotate, new[] { Kv.Num("bytes", fi.Length) });
                Debug.LogWarning(Tag + "size ceiling reached; stopping this stream cleanly rather than "
                                 + "continuing into an unbounded file.");
                Close("size_ceiling");
            }
            catch { }
        }

        /// <summary>
        /// One event, written and FLUSHED IMMEDIATELY (§4.7). Events are rare (~0.1/s) and each one is
        /// a transition somebody will want to correlate against a log line or a screenshot, so the
        /// per-event flush costs nothing and guarantees the narrative survives an unexpected end even
        /// when the last few parameter rows do not.
        /// </summary>
        public void Emit(string kind, Kv[] payload)
        {
            if (events == null) return;
            try
            {
                // ⛔ THIS STREAM'S vessel's MET, latched on the last tick — never the ACTIVE vessel's.
                // MET restarts per vessel (§4.5), so borrowing the camera-holder's clock would put a
                // booster event on the capsule's timeline, which is the exact class of error the
                // shared `ut` exists to make impossible.
                double ut = double.IsNaN(lastUt) ? 0.0 : lastUt;
                events.WriteLine(BlackBoxEvents.Line(MissionId, VesselName, ut, lastMet, seq, kind, payload));
                events.Flush();
                eventsWritten++;
            }
            catch (Exception e) { OnWriteError("event", e); }
        }

        // ============================== event edges (§2.9) ==============================

        void DetectEvents(Vessel v, double ut, MissionPhase classified, int ignited, int flameout,
                          double thrustN, bool rails)
        {
            // ---- phase, mode, focus, warp ----
            MissionPhase authoritative = Mission.AuthoritativePhase(
                CrewProcedureOps.Engaged, CrewProcedureOps.ActivePhase, classified);
            if (authoritative != lastPhase)
            {
                if (lastPhase != (MissionPhase)255)
                    Emit(BlackBoxEvents.PhaseTransition, new[]
                    {
                        Kv.Str("from", Mission.Name(lastPhase)),
                        Kv.Str("to", Mission.Name(authoritative)),
                        Kv.Str("classified", Mission.Name(classified)),
                        Kv.Bit("conductor_engaged", CrewProcedureOps.Engaged),
                        Kv.Num("alt_m", v.altitude), Kv.Num("srf_speed_mps", v.srfSpeed),
                    });
                lastPhase = authoritative;
            }

            ControlMode mode = FlightDriver.MissionMode;
            if (mode != lastMode)
            {
                if (lastMode != (ControlMode)255)
                    Emit(BlackBoxEvents.GncModeChange,
                         new[] { Kv.Str("from", lastMode.ToString()), Kv.Str("to", mode.ToString()) });
                lastMode = mode;
            }

            Vessel act = FlightGlobals.ActiveVessel;
            string focus = act != null ? act.vesselName : null;
            if (focus != lastFocus)
            {
                if (lastFocus != null)
                    Emit(BlackBoxEvents.RecFocusChange,
                         new[] { Kv.Str("from", lastFocus), Kv.Str("to", focus) });
                lastFocus = focus;
            }

            double warp = TimeWarp.CurrentRate;
            if (Math.Abs(warp - lastWarpRate) > 1e-6)
            {
                if (lastWarpRate >= 0.0)
                    Emit(BlackBoxEvents.RecWarpChange, new[]
                    {
                        Kv.Num("from", lastWarpRate), Kv.Num("to", warp), Kv.Bit("rails", rails),
                    });
                lastWarpRate = warp;
            }

            // ---- staging + engines. §2.3: eng_ignited/eng_flameout exist because "delivered thrust
            // ---- = 0" cannot distinguish DID NOT COMMAND from COMMANDED AND FAILED.
            int stage = StageManager.CurrentStage;
            if (stage != lastStage)
            {
                if (lastStage != int.MinValue)
                    Emit(BlackBoxEvents.FlightStaged, new[]
                    {
                        Kv.Int("from", lastStage), Kv.Int("to", stage),
                        Kv.Num("alt_m", v.altitude), Kv.Num("mass_kg", v.totalMass * 1000.0),
                    });
                lastStage = stage;
            }
            if (ignited != lastIgnited)
            {
                if (lastIgnited >= 0)
                    Emit(ignited > lastIgnited ? BlackBoxEvents.EngineIgnite : BlackBoxEvents.EngineShutdown,
                         new[]
                         {
                             Kv.Int("from", lastIgnited), Kv.Int("to", ignited),
                             Kv.Num("thrust_n", thrustN), Kv.Int("stage", stage),
                         });
                lastIgnited = ignited;
            }
            if (flameout != lastFlameout)
            {
                if (lastFlameout >= 0 && flameout > lastFlameout)
                    Emit(BlackBoxEvents.EngineFlameout, new[]
                    {
                        Kv.Int("count", flameout), Kv.Num("thrust_n", thrustN), Kv.Int("stage", stage),
                    });
                lastFlameout = flameout;
            }

            // ---- the once-per-mission flight milestones ----
            if (!liftoffSeen && v.missionTime > 0.0 && v.verticalSpeed > 1.0
                && classified == MissionPhase.Ascent)
            {
                liftoffSeen = true;
                Emit(BlackBoxEvents.FlightLiftoff,
                     new[] { Kv.Num("ut", ut), Kv.Num("met_s", v.missionTime), Kv.Num("mass_kg", v.totalMass * 1000.0) });
            }
            // max-Q is a PEAK, so it is detected on the fall, not on a threshold: q rose, then dropped
            // clear of the peak by 5 %. §B8's whole tune is quoted against this instant.
            double q = v.dynamicPressurekPa * 1000.0;
            if (q > peakQSoFar) peakQSoFar = q;
            if (!maxQSeen && peakQSoFar > 1000.0 && q < peakQSoFar * 0.95)
            {
                maxQSeen = true;
                Emit(BlackBoxEvents.FlightMaxQ, new[]
                {
                    Kv.Num("peak_q_pa", peakQSoFar), Kv.Num("alt_m", v.altitude),
                    Kv.Num("mach", v.mach), Kv.Num("met_s", v.missionTime),
                });
            }
            if (!droguesSeen && classified == MissionPhase.Drogues)
            {
                droguesSeen = true;
                Emit(BlackBoxEvents.FlightDrogue,
                     new[] { Kv.Num("alt_radar_m", v.radarAltitude), Kv.Num("srf_speed_mps", v.srfSpeed) });
            }
            if (!mainsSeen && classified == MissionPhase.Mains)
            {
                mainsSeen = true;
                Emit(BlackBoxEvents.FlightMain,
                     new[] { Kv.Num("alt_radar_m", v.radarAltitude), Kv.Num("srf_speed_mps", v.srfSpeed) });
            }
            if (!downSeen && (classified == MissionPhase.Splashdown || classified == MissionPhase.Landed))
            {
                downSeen = true;
                Emit(classified == MissionPhase.Splashdown ? BlackBoxEvents.FlightSplashdown
                                                           : BlackBoxEvents.FlightTouchdown,
                     new[] { Kv.Num("lat_deg", v.latitude), Kv.Num("lon_deg", v.longitude),
                             Kv.Num("vspeed_mps", v.verticalSpeed) });
            }

            // ---- systems edges: a trip cascade is instantaneous, so the EDGE is the event and the
            // ---- R2 column is only context (§2.8).
            SystemsState sys = FlightCommands.State;
            if (!haveBusState) { lastBus1 = sys.Bus1On; lastBus2 = sys.Bus2On; haveBusState = true; }
            else
            {
                if (sys.Bus1On != lastBus1)
                {
                    Emit(BlackBoxEvents.SysBusTrip, new[] { Kv.Int("bus", 1), Kv.Bit("on", sys.Bus1On) });
                    lastBus1 = sys.Bus1On;
                }
                if (sys.Bus2On != lastBus2)
                {
                    Emit(BlackBoxEvents.SysBusTrip, new[] { Kv.Int("bus", 2), Kv.Bit("on", sys.Bus2On) });
                    lastBus2 = sys.Bus2On;
                }
            }
            if (sys.Fire != lastFire)
            {
                Emit(sys.Fire ? BlackBoxEvents.SysFireStart : BlackBoxEvents.SysFireOut,
                     new[] { Kv.Num("intensity", sys.FireIntensity), Kv.Num("suppressant", sys.Suppressant) });
                lastFire = sys.Fire;
            }
            if (sys.Leaking != lastLeak)
            {
                Emit(sys.Leaking ? BlackBoxEvents.SysLeakStart : BlackBoxEvents.SysIsolate,
                     new[] { Kv.Num("leak_rate", sys.LeakRate), Kv.Bit("isolating", sys.Isolating) });
                lastLeak = sys.Leaking;
            }

            // ---- the alarm channel + the page timeline: screens-derived, so guarded the same way the
            // ---- screens-derived COLUMNS are. A stale mask would raise a fault that is not happening.
            if (act != null && act.persistentId == PersistentId)
            {
                PageState ps = VesselData.State;
                if (ps.Valid)
                {
                    int mask = Alarms.Mask(ps);
                    if (mask != lastAlarmMask)
                    {
                        if (lastAlarmMask >= 0)
                            Emit(mask > lastAlarmMask ? BlackBoxEvents.FaultRaised : BlackBoxEvents.FaultCleared,
                                 new[]
                                 {
                                     Kv.Int("from_mask", lastAlarmMask), Kv.Int("to_mask", mask),
                                     Kv.Str("sev_system", Alarms.SystemSeverity(ps).ToString()),
                                     Kv.Str("sev_vehicle", Alarms.VehicleSeverity(ps).ToString()),
                                 });
                        lastAlarmMask = mask;
                    }
                    // §2.7: a page SELECTION is a state and a PRESS is an act. The state half is here.
                    // The press half needs a hook at `ScreenPainter.TouchDown` / `PanelButton.OnMouseDown`
                    // plus §2.7's flat `control_id` namespace — a tree edit, which BB1 may not make
                    // (excisable by design). Logged as its own register line, not half-built here.
                    int[] pg = ps.ScreenPages;
                    if (pg != null && pg.Length >= 4)
                    {
                        PageEdge(0, pg[1], ref lastPageL);
                        PageEdge(1, pg[2], ref lastPageC);
                        PageEdge(2, pg[3], ref lastPageR);
                    }
                }
            }
        }

        void PageEdge(int screen, int page, ref int last)
        {
            if (page == last) return;
            if (last >= 0)
                Emit(BlackBoxEvents.CrewPageChange,
                     new[] { Kv.Int("screen", screen), Kv.Int("from", last), Kv.Int("to", page) });
            last = page;
        }

        // ============================== physics-rate accumulation ==============================

        /// <summary>
        /// §2.0's R0 tier, run EVERY `FixedUpdate` whatever the row cadence. The tick length is taken
        /// from UT rather than `Time.fixedDeltaTime` because physics warp makes a tick cover more UT,
        /// and a duty cycle computed against the unscaled tick is wrong by the warp factor.
        /// </summary>
        void AccumulateTick(Vessel v, double ut)
        {
            double dt = double.IsNaN(lastPhysUt) ? 0.0 : ut - lastPhysUt;
            lastPhysUt = ut;
            if (dt <= 0.0 || dt > 1.0) return;   // first tick, a revert, or an on-rails jump: no interval

            FlightCtrlState cs = v.ctrlState;
            double att = 0.0, trans = 0.0;
            if (cs != null)
            {
                att = Max3(Math.Abs(cs.pitch), Math.Abs(cs.yaw), Math.Abs(cs.roll));
                trans = Max3(Math.Abs(cs.X), Math.Abs(cs.Y), Math.Abs(cs.Z));
            }
            accum.Add(dt, att, trans, v.geeForce, v.dynamicPressurekPa * 1000.0,
                      (v.angularVelocity * Mathf.Rad2Deg).magnitude);
        }

        static double Max3(double a, double b, double c)
        {
            double m = a > b ? a : b;
            return m > c ? m : c;
        }

        // ============================== the ~15 new KSP reads (§4.7) ==============================

        /// <summary>
        /// Engine state in ONE part walk: commanded-on count, flamed-out count, delivered thrust and
        /// available thrust. §2.3's `thrust_n` is the value Recorder B read at `:420` and THREW AWAY
        /// behind a `> 0.1f` test; here it is kept, because thrust/mass reproducing measured
        /// acceleration is the (b)-class cross-check the physics self-check runs on.
        /// </summary>
        static void EngineState(Vessel v, out int ignited, out int flameout, out double thrustN, out double availN)
        {
            ignited = 0; flameout = 0; thrustN = 0.0; availN = 0.0;
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
                        thrustN += e.finalThrust * 1000.0;
                        availN += e.maxThrust * 1000.0;
                    }
                }
            }
            catch { }
        }

        /// <summary>Available control torque per axis (kN·m) and the RCS thrust in use (N), one walk.</summary>
        static void Authority(Vessel v, out double pitch, out double yaw, out double roll, out double rcsN)
        {
            pitch = 0.0; yaw = 0.0; roll = 0.0; rcsN = 0.0;
            if (v == null || v.parts == null) return;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        PartModule pm = p.Modules[m];
                        ITorqueProvider tp = pm as ITorqueProvider;
                        if (tp != null)
                        {
                            Vector3 pos, neg;
                            try { tp.GetPotentialTorque(out pos, out neg); } catch { continue; }
                            // Stock reports a positive-going and a negative-going capability per axis.
                            // The larger of the two is the authority available in the worse direction's
                            // opposite, and taking max() is what the deleted loop did; the number is a
                            // CAPABILITY, so summing per-axis magnitudes is the honest aggregate.
                            pitch += Math.Max(Math.Abs(pos.x), Math.Abs(neg.x));
                            roll  += Math.Max(Math.Abs(pos.y), Math.Abs(neg.y));
                            yaw   += Math.Max(Math.Abs(pos.z), Math.Abs(neg.z));
                        }
                        ModuleRCS rcs = pm as ModuleRCS;
                        if (rcs != null && rcs.rcsEnabled) rcsN += rcs.thrusterPower * 1000.0;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Hottest part's skin temperature, as a fraction of its limit and in degrees C.
        /// COMPOSED from Recorder B, which added it after a max-Q "Overheat!" was INVISIBLE in the CSV
        /// and visible only in a screenshot — a §0-class failure closed by one column.
        /// </summary>
        bool HottestSkin(Vessel v, double ut, out double frac, out double tempC)
        {
            frac = 0.0; tempC = 0.0;
            if (v == null || v.parts == null) return false;
            Part hottest = null;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    double mx = p.skinMaxTemp;
                    if (mx <= 0.0) continue;
                    double f = p.skinTemperature / mx;
                    if (f > frac) { frac = f; hottest = p; }
                }
            }
            catch { return false; }
            if (hottest == null || frac <= 0.0) return false;
            tempC = hottest.skinTemperature - 273.15;
            if (frac >= 0.85 && ut - lastThermalLogUt > 5.0)
            {
                lastThermalLogUt = ut;
                string nm = (hottest.partInfo != null) ? hottest.partInfo.title : hottest.name;
                Debug.LogWarning(Tag + "THERMAL: hottest part '" + nm + "' at "
                                 + (frac * 100.0).ToString("F0", CultureInfo.InvariantCulture) + "% of limit");
            }
            return true;
        }

        /// <summary>
        /// Surface-frame pitch / heading / roll.
        ///
        /// §2.2 records that these are computed in `NavBallRenderer.Orient()` (`:245-248`) and are only
        /// `Debug.Log`ged — NEVER PUBLISHED — and that without the pitch trace the recorder "cannot
        /// serve the tune at all", since §B8's diagnosis reads it directly. The FORMULA is the one
        /// already in this tree (MASVesselComputer.UpdateAttitude, ported verbatim there because the
        /// corrections are empirical); it is applied here to THIS STREAM'S vessel, which the navball's
        /// active-vessel-only version cannot do and which BB2's unfocused booster needs.
        /// </summary>
        static bool SurfaceAttitude(Vessel v, out double pitch, out double heading, out double roll)
        {
            pitch = 0.0; heading = 0.0; roll = 0.0;
            Transform rt = v.ReferenceTransform;
            CelestialBody body = v.mainBody;
            if (rt == null || body == null) return false;
            try
            {
                Quaternion attitude = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Inverse(rt.rotation);
                Vector3 up = (rt.position - body.position).normalized;
                Quaternion relative = attitude * Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(up + body.transform.up, up), up);
                Vector3 e = Quaternion.Inverse(relative).eulerAngles;
                // Euler angles come back in [0,360). Fold pitch and roll into the signed ranges every
                // reader expects (-90..90 and -180..180); heading is genuinely 0..360 and stays.
                pitch = e.x > 180f ? 360f - e.x : -e.x;
                heading = e.y;
                roll = e.z > 180f ? e.z - 360f : e.z;
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Angle of attack and sideslip, in the vessel's own pitch and yaw planes.
        ///
        /// DERIVED, not simulated: both are fully determined by live KSP state (surface velocity vs the
        /// reference transform) and neither invents a quantity the game does not have — so §14.4(e)'s
        /// simulate-and-mark ladder does not apply and the manifest marks them `derived`.
        /// BLANK below 1 m/s: an angle of attack against a velocity of nothing is not a small number,
        /// it is not a number, and §4.6 says blank rather than plausible.
        /// </summary>
        static bool FlowAngles(Vessel v, out double aoa, out double aos)
        {
            aoa = 0.0; aos = 0.0;
            Transform rt = v.ReferenceTransform;
            if (rt == null) return false;
            Vector3d vel = v.srf_velocity;
            if (vel.magnitude < 1.0) return false;
            try
            {
                Vector3d fwd = rt.up;        // KSP's reference transform points +Y "up" along the nose
                Vector3d rgt = rt.right;
                Vector3d dn = rt.forward;
                Vector3d u = vel.normalized;
                // AoA: the flow's component along the belly axis, against its component along the nose.
                aoa = Math.Atan2(Vector3d.Dot(u, dn), Vector3d.Dot(u, fwd)) * 180.0 / Math.PI;
                aos = Math.Atan2(Vector3d.Dot(u, rgt), Vector3d.Dot(u, fwd)) * 180.0 / Math.PI;
                return true;
            }
            catch { return false; }
        }

        static double TargetRange(Vessel v)
        {
            try
            {
                ITargetable t = v.targetObject;
                if (t == null) return double.NaN;
                Transform tt = t.GetTransform();
                if (tt == null) return double.NaN;
                return (tt.position - v.transform.position).magnitude;
            }
            catch { return double.NaN; }
        }

        /// <summary>Range RATE, negative when closing (§2.8's convention). Direct from both velocities.</summary>
        static double ClosingRate(Vessel v)
        {
            try
            {
                ITargetable t = v.targetObject;
                if (t == null) return double.NaN;
                Transform tt = t.GetTransform();
                if (tt == null) return double.NaN;
                Vector3d rel = (Vector3d)(tt.position - v.transform.position);
                double r = rel.magnitude;
                if (r < 1e-6) return double.NaN;
                Vector3d dv = t.GetObtVelocity() - v.obt_velocity;
                return Vector3d.Dot(dv, rel / r);
            }
            catch { return double.NaN; }
        }

        /// <summary>Great-circle surface distance from the latched launch reference. Composed from Recorder B.</summary>
        double DownrangeM(Vessel v)
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

        MissionPhase Classify(Vessel v)
        {
            // The pure classifier, fed from THIS stream's vessel — so the phase is the recorded
            // vessel's own, not the focused one's. `VesselData` builds the same inputs for the screens;
            // building them here is what lets an unfocused vessel (BB2's booster) carry a real phase.
            MissionInputs mi = new MissionInputs();
            mi.Regime = Regime(v.situation);
            mi.RadarAltitude = v.radarAltitude;
            mi.VerticalSpeed = v.verticalSpeed;
            mi.Docked = (v.situation == Vessel.Situations.DOCKED);
            mi.Splashed = (v.situation == Vessel.Situations.SPLASHED);
            mi.HasTarget = (v.targetObject != null);
            mi.TargetRange = mi.HasTarget ? TargetRange(v) : 0.0;
            mi.OrbitClosed = v.mainBody != null && v.orbit != null && v.orbit.PeA > v.mainBody.atmosphereDepth;
            Chutes(v, ref mi);
            return Mission.Classify(mi);
        }

        static FlightRegime Regime(Vessel.Situations s)
        {
            switch (s)
            {
                case Vessel.Situations.LANDED:
                case Vessel.Situations.PRELAUNCH:
                case Vessel.Situations.SPLASHED: return FlightRegime.Ground;
                case Vessel.Situations.FLYING: return FlightRegime.Atmosphere;
                default: return FlightRegime.Space;
            }
        }

        static void Chutes(Vessel v, ref MissionInputs mi)
        {
            if (v.parts == null) return;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        // ⛔ THE SAME TEST `VesselData.Chutes` USES (`:262-278`), deliberately identical.
                        // `phase_classified` is recorded so a disagreement with the conductor is visible
                        // (§2.6); a disagreement with the SCREENS because the recorder read chutes
                        // differently would be noise in exactly that signal.
                        ModuleParachute ch = p.Modules[m] as ModuleParachute;
                        if (ch == null) continue;
                        bool open = (ch.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED
                                  || ch.deploymentState == ModuleParachute.deploymentStates.DEPLOYED);
                        if (!open) continue;
                        if (ch.deployAltitude >= Mission.MainAltitude * 1.5) mi.DroguesOut = true;
                        else mi.MainsOut = true;
                    }
                }
            }
            catch { }
        }

        static int PackBits(bool[] flags)
        {
            if (flags == null) return 0;
            int mask = 0;
            int n = flags.Length < 32 ? flags.Length : 32;
            for (int i = 0; i < n; i++) if (flags[i]) mask |= (1 << i);
            return mask;
        }

        // ============================== manifest ==============================

        void BuildManifest(Vessel v)
        {
            manifest = ManifestInfo.Fresh();
            manifest.MissionId = MissionId;
            manifest.Vessel = VesselName;
            manifest.VesselPersistentId = PersistentId;
            manifest.Body = v.mainBody != null ? v.mainBody.bodyName : null;
            manifest.TargetName = v.targetObject != null ? v.targetObject.GetName() : null;
            manifest.Policy = policy;
            manifest.DynamicPhaseRule =
                "Ascent | Entry | Drogues | Mains | aborting | thrust_n > 0 | RCS translation commanded "
                + "| Approach inside 1 km  (BlackBoxRate.IsDynamic)";

            double ut = Planetarium.GetUniversalTime();
            manifest.UtAtOpen = ut;
            manifest.WallAtOpen = Time.realtimeSinceStartup;
            manifest.RealWorldUtcAtOpen = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            // §4.5's correlation record: MET restarts per vessel and jumps on a revert, so it is the
            // PRESENTATION frame only. launch_ut is what makes MET mappable onto the analysis clock —
            // the SCLK↔SCET kernel analogue, and the single thing Recorder B had no way to express.
            manifest.LaunchUt = ut - v.missionTime;

            try
            {
                for (int i = 0; i < v.GetVesselCrew().Count; i++)
                    manifest.Crew.Add(v.GetVesselCrew()[i].name);
            }
            catch { }

            try { manifest.KspVersion = Versioning.VersionString; } catch { }
            CollectAssemblyInfo();
            CollectTunables();
            manifest.MechJebCfgSha = null;   // no MechJeb core is embedded yet — T15. Honest null, not "".
        }

        /// <summary>
        /// What was FLOWN, identified exactly.
        ///
        /// §4.3 asks for `dragonscreen_git_sha`. A running plugin has no git; the DLL's SHA-256 is
        /// strictly stronger for the same purpose — it identifies the exact binary INCLUDING a dirty
        /// working tree, which a git sha silently does not. It is named for what it is.
        /// </summary>
        void CollectAssemblyInfo()
        {
            try
            {
                Assembly self = Assembly.GetExecutingAssembly();
                manifest.DragonScreenAsmVersion = self.GetName().Version.ToString();
                try
                {
                    string loc = self.Location;
                    if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
                    {
                        using (var sha = new SHA256Managed())
                        using (var fs = File.OpenRead(loc))
                        {
                            byte[] h = sha.ComputeHash(fs);
                            var sb = new StringBuilder(64);
                            for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2", CultureInfo.InvariantCulture));
                            manifest.DragonScreenDllSha256 = sb.ToString();
                        }
                    }
                }
                catch { }

                // Every loaded GameData assembly with its version — §4.3's mod_versions. This is what
                // makes a recording decodable in six months: "RealFuels 13.3.2" is the difference
                // between a reproducible finding and an anecdote.
                if (AssemblyLoader.loadedAssemblies != null)
                {
                    for (int i = 0; i < AssemblyLoader.loadedAssemblies.Count; i++)
                    {
                        var la = AssemblyLoader.loadedAssemblies[i];
                        if (la == null || la.assembly == null) continue;
                        manifest.ModVersions.Add(la.name + " " + la.assembly.GetName().Version);
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning(Tag + "assembly info: " + e.Message); }
        }

        /// <summary>
        /// Every `[Tunable]` static in the assembly, with its value at open.
        ///
        /// §4.3's second promise: THE TUNE IS REPRODUCIBLE. §B5 changes one parameter at a time, and
        /// without this two recordings cannot be told apart — which makes a one-parameter tune
        /// unfalsifiable. `Tuning` already builds this catalogue by reflection for its own cfg; this is
        /// the same scan, read rather than written, so no coupling to `Tuning`'s state is introduced.
        /// </summary>
        void CollectTunables()
        {
            try
            {
                foreach (Type t in Assembly.GetExecutingAssembly().GetTypes())
                {
                    FieldInfo[] fs = t.GetFields(BindingFlags.Public | BindingFlags.Static);
                    for (int i = 0; i < fs.Length; i++)
                    {
                        FieldInfo f = fs[i];
                        if (f.IsLiteral || f.IsInitOnly) continue;
                        if (!f.IsDefined(typeof(TunableAttribute), false)) continue;
                        object val = null;
                        try { val = f.GetValue(null); } catch { }
                        manifest.Tunables.Add(t.Name + "." + f.Name + " = "
                            + Convert.ToString(val, CultureInfo.InvariantCulture));
                    }
                }
                manifest.Tunables.Sort(StringComparer.Ordinal);
            }
            catch (Exception e) { Debug.LogWarning(Tag + "tunable scan: " + e.Message); }
        }

        // ============================== small helpers ==============================

        // ⛔ NOT `DateTime.UtcNow.Ticks`. Its granularity on Windows is ~15 ms, so a sub-millisecond
        // row build would measure as either 0 or 15 625 us and `rec_build_us` would be worse than
        // absent — §1.4(b) exists so "the recorder cost us frames" is a MEASUREMENT rather than an
        // opinion, and a quantised zero is an opinion with a number on it.
        static readonly double TicksToUs = 1000000.0 / System.Diagnostics.Stopwatch.Frequency;
        static long TickNow() { return System.Diagnostics.Stopwatch.GetTimestamp(); }
        static double ElapsedUs(long t0) { return (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * TicksToUs; }

        /// <summary>§4.4: a revert branches the mission id with `_r2`, `_r3`, … rather than a new mission.</summary>
        static string NextRevertSuffix(string missionId)
        {
            int at = missionId.LastIndexOf("_r", StringComparison.Ordinal);
            if (at > 0)
            {
                int n;
                if (int.TryParse(missionId.Substring(at + 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                    return "_r" + (n + 1);
            }
            return "_r2";
        }

        static bool RailsWarp()
        {
            try { return TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRateIndex > 0; }
            catch { return false; }
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "flight";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
                sb.Append((char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') ? ch : '_');
            return sb.ToString();
        }
    }
}
