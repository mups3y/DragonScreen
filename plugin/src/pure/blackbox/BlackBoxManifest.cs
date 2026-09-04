// DragonScreen — BlackBox / MANIFEST  (register BB1; spec: §4.3 "which is not optional")
// ============================================================================================
// PURE. §3.4 files the sidecar manifest BUILD FRESH: neither prior recorder recorded its own schema
// version, units, provenance, mod versions or build hash, and §1.5's last row is blunt about the
// consequence — WITHOUT IT A FILE IS UNDECODABLE IN SIX MONTHS. Real FDR data is undecodable without
// its parameter/conversion documentation, and the NTSB handbook treats that documentation as a
// controlled artefact in its own right. This is ours.
//
// ---- THE TWO THINGS IT BUYS THAT NO COLUMN CAN (§4.3) ----
//  1. §14.4(e)/(f) MARKING, ONCE, CHEAPLY. Every simulated value is declared `provenance: "SIMULATED"`
//     in ONE place, so the overseer can never mistake a marked simulation for a measurement — and no
//     per-column provenance string is written 120 000 times. A simulated value in a recording is
//     evidence about the MODEL, never about the VEHICLE, and BB3's §0 section labels it so.
//  2. THE TUNE IS REPRODUCIBLE. `tunables{}` and the MechJeb cfg hash record WHAT THE VEHICLE WAS
//     FLOWN WITH. §B5 changes one parameter at a time; without this, two recordings cannot be told
//     apart, which makes a one-parameter tune unfalsifiable.
//
// ---- AND ONE MORE, WHICH IS BB1'S OWN ----
// `columns[].fit` + `columns[].note`. §4.3's field list did not have it because §4.3 was written
// before S76's ghost-column finding was folded into this line. A blank cell means one of three
// different things (not sampled this row / no signal / not fitted at all) and only the manifest can
// say which, per column, without a per-cell sentinel. See `BlackBoxSchema`'s header for the taxonomy.
//
// ---- WHY THE JSON IS HAND-BUILT ----
// There is no JSON serialiser in KSP's mscorlib profile that is safe to depend on, the document is
// written exactly twice per mission (open and close), and its shape is fixed. A hand-built writer is
// ~80 lines, has no dependency, and cannot be surprised by a culture setting. `System.Globalization`
// invariant formatting is used throughout for the same reason the CSV uses it: a European locale
// writes "1,5" and the file stops being JSON.
// ============================================================================================
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DragonScreen.BlackBox
{
    /// <summary>Everything the manifest records that is NOT derivable from the schema table.</summary>
    public struct ManifestInfo
    {
        // ---- provenance of the software that produced the file ----
        public string DragonScreenAsmVersion;
        /// <summary>
        /// SHA-256 of the DLL that actually flew. §4.3 asks for `dragonscreen_git_sha`; a git sha is
        /// not available to a running plugin without a build-time stamp, and the DLL hash is STRICTLY
        /// STRONGER for the purpose — it identifies the exact binary including a dirty working tree,
        /// which a git sha silently does not. Named for what it is rather than for what it stands in for.
        /// </summary>
        public string DragonScreenDllSha256;
        public string KspVersion;
        /// <summary>Name + version per loaded GameData assembly, in load order.</summary>
        public List<string> ModVersions;

        // ---- the mission ----
        public string MissionId;
        public string Vessel;
        public uint VesselPersistentId;
        public List<string> Crew;
        public string Body;
        public string TargetName;

        // ---- ⭐ BB2: WHICH STREAM OF THE MISSION THIS IS, AND WHERE ITS SIBLINGS ARE ----
        // §4.4's two-vessel rule ("the same mission id") is what makes two streams one mission, and
        // this is where a reader learns it WITHOUT globbing a directory and guessing from filenames —
        // which is precisely how the old paired `Crew-2_*.csv` / `Crew-2_Probe_*.csv` streams had to be
        // associated, by timestamp, and why §4.4 calls this the fix for them.
        /// <summary>"focused" (opened for the camera holder) or "tracked" (an unfocused vessel, §B16.7).</summary>
        public string StreamRole;
        /// <summary>
        /// Did this stream's vessel EVER hold the camera? The `Scope.Capsule` columns are written only
        /// while it does, so this is the flag that says whether their blanks are expected — and it is
        /// what `BlackBoxCoverage.Findings(everFocused)` was given at close.
        /// </summary>
        public bool EverFocused;
        /// <summary>This stream's own parameter file, by name — `<MissionId>[.<Vessel>].params.csv`.</summary>
        public string ParamsFile;
        /// <summary>
        /// The mission's SHARED event log. One per mission, not one per stream (§4.1: "per mission,
        /// three artefacts"; §4.4 qualifies only the params file per vessel), so both vessels' events
        /// are already one ordered narrative — which is what §4.10's new §10 section asks for.
        /// </summary>
        public string EventsFile;
        /// <summary>
        /// The MISSION's launch reference, latched once when its first stream opened, and shared by
        /// every stream so `downrange_m` means the same thing on both. A booster stream that latched
        /// its own reference at separation would measure downrange from the separation point, and its
        /// deck-miss (§4.10 §4) would be uncomparable with the capsule's by exactly that offset.
        /// </summary>
        public double LaunchLatDeg, LaunchLonDeg;
        public bool HaveLaunchRef;

        // ---- §4.5's UT / MET / wall correlation record (the SCLK-SCET kernel analogue) ----
        public double LaunchUt;
        public double UtAtOpen;
        public double WallAtOpen;
        public string RealWorldUtcAtOpen;

        // ---- the cadence actually flown (BB1-Q1 lands here, whichever way it is decided) ----
        public RatePolicy Policy;
        public string DynamicPhaseRule;

        // ---- what the vehicle was flown WITH ----
        public string MechJebCfgSha;
        /// <summary>"Type.Field = value" per `[Tunable]`, so a one-parameter tune step is identifiable.</summary>
        public List<string> Tunables;

        // ---- filled at close ----
        public bool Closed;
        public double ClosedUt;
        public string ClosedReason;
        public long RowsWritten;
        public long EventsWritten;
        public int WriteErrors;
        public double MaxRecBuildUs;
        /// <summary>
        /// BB7: a single max misreports a well-behaved recorder as wildly over budget when the max is a
        /// one-off spike (a GC pause) rather than a sustained cost — median/p90 are what tell a reader
        /// which. Estimated from a bounded-memory <see cref="LatencyHistogram"/>, not the exact value;
        /// see that file's header for why and for the accuracy this buys. 0 on a mission with no rows.
        /// </summary>
        public double P50RecBuildUs, P90RecBuildUs, P99RecBuildUs;
        /// <summary>The coverage pass's findings, rendered — see <see cref="BlackBoxCoverage"/>.</summary>
        public List<CoverageFinding> Coverage;

        public static ManifestInfo Fresh()
        {
            ManifestInfo m = new ManifestInfo();
            m.ModVersions = new List<string>();
            m.Crew = new List<string>();
            m.Tunables = new List<string>();
            m.Coverage = new List<CoverageFinding>();
            m.Policy = RatePolicy.Adaptive();
            return m;
        }
    }

    public static class BlackBoxManifest
    {
        static string S(string v) { return BlackBoxEvents.JsonString(v); }
        static string N(double v) { return BlackBoxEvents.JsonNum(v); }
        static string I(long v) { return v.ToString(CultureInfo.InvariantCulture); }

        static void Arr(StringBuilder sb, string key, List<string> items)
        {
            sb.Append("  ").Append(S(key)).Append(": [");
            if (items != null)
                for (int i = 0; i < items.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(S(items[i])); }
            sb.Append("],\n");
        }

        /// <summary>
        /// The whole document. Written at open with `Closed = false`, and REWRITTEN at close with the
        /// tail filled — §4.3 says "written once at open and finalised at close", and rewriting the
        /// small sidecar is strictly safer than seeking into it. If the game dies between the two, the
        /// open-time manifest still decodes the stream; only the tail is missing, and `closed: false`
        /// says exactly that rather than leaving a reader to guess from a truncated file.
        /// </summary>
        public static string Build(ManifestInfo m)
        {
            var sb = new StringBuilder(16384);
            sb.Append("{\n");
            sb.Append("  \"schema_version\": ").Append(I(BlackBoxSchema.SchemaVersion)).Append(",\n");
            sb.Append("  \"recorder_version\": ").Append(S(BlackBoxSchema.RecorderVersion)).Append(",\n");
            sb.Append("  \"dragonscreen_asm_version\": ").Append(S(m.DragonScreenAsmVersion)).Append(",\n");
            sb.Append("  \"dragonscreen_dll_sha256\": ").Append(S(m.DragonScreenDllSha256)).Append(",\n");
            sb.Append("  \"ksp_version\": ").Append(S(m.KspVersion)).Append(",\n");
            Arr(sb, "mod_versions", m.ModVersions);

            sb.Append("  \"mission_id\": ").Append(S(m.MissionId)).Append(",\n");
            sb.Append("  \"vessel\": ").Append(S(m.Vessel)).Append(",\n");
            sb.Append("  \"vessel_persistent_id\": ").Append(I(m.VesselPersistentId)).Append(",\n");
            Arr(sb, "crew", m.Crew);
            sb.Append("  \"body\": ").Append(S(m.Body)).Append(",\n");
            sb.Append("  \"target_name\": ").Append(S(m.TargetName)).Append(",\n");

            // ---- BB2: this stream's place in the mission, and the join that reunites the two ----
            sb.Append("  \"stream_role\": ").Append(S(m.StreamRole)).Append(",\n");
            sb.Append("  \"ever_focused\": ").Append(m.EverFocused ? "true" : "false").Append(",\n");
            sb.Append("  \"params_file\": ").Append(S(m.ParamsFile)).Append(",\n");
            sb.Append("  \"events_file\": ").Append(S(m.EventsFile)).Append(",\n");
            // Stated rather than implied. S59 §6.1 Q3 settled ONE STREAM PER VESSEL "joined by the
            // shared mission_id and ut", and a reader should not have to know that from a research doc.
            sb.Append("  \"stream_join_on\": [\"mission_id\", \"ut\"],\n");
            sb.Append("  \"launch_lat_deg\": ").Append(m.HaveLaunchRef ? N(m.LaunchLatDeg) : "null").Append(",\n");
            sb.Append("  \"launch_lon_deg\": ").Append(m.HaveLaunchRef ? N(m.LaunchLonDeg) : "null").Append(",\n");

            // ---- §4.5: the one owner of the time base, and one documented offset ----
            sb.Append("  \"launch_ut\": ").Append(N(m.LaunchUt)).Append(",\n");
            sb.Append("  \"ut_at_open\": ").Append(N(m.UtAtOpen)).Append(",\n");
            sb.Append("  \"wall_at_open\": ").Append(N(m.WallAtOpen)).Append(",\n");
            sb.Append("  \"real_world_utc_at_open\": ").Append(S(m.RealWorldUtcAtOpen)).Append(",\n");

            // ---- the cadence, so no analyser ever assumes a row period (§3.4 files that as BREAK:
            // ---- five literal `.2`s in the old analyser, and B changed to 0.25 s) ----
            sb.Append("  \"row_rate_mode\": ").Append(S(m.Policy.Mode == RateMode.Fixed ? "fixed" : "adaptive")).Append(",\n");
            sb.Append("  \"row_rate_dynamic_hz\": ").Append(N(m.Policy.RowRateDynamicHz)).Append(",\n");
            sb.Append("  \"row_rate_quiescent_hz\": ").Append(N(m.Policy.RowRateQuiescentHz)).Append(",\n");
            sb.Append("  \"warp_wall_floor_s\": ").Append(N(m.Policy.WarpWallFloorS)).Append(",\n");
            sb.Append("  \"dynamic_phase_rule\": ").Append(S(m.DynamicPhaseRule)).Append(",\n");

            sb.Append("  \"mechjeb_cfg_sha\": ").Append(S(m.MechJebCfgSha)).Append(",\n");
            Arr(sb, "tunables", m.Tunables);

            // ---- columns[]: the dataframe layout. DERIVED from the one ordered table. ----
            sb.Append("  \"columns\": [\n");
            Col[] cols = BlackBoxSchema.Columns;
            for (int i = 0; i < cols.Length; i++)
            {
                Col c = cols[i];
                double period = BlackBoxSchema.PeriodOf(c.Tier);
                sb.Append("    {\"i\": ").Append(I(i));
                sb.Append(", \"name\": ").Append(S(c.Name));
                sb.Append(", \"units\": ").Append(S(c.Units));
                sb.Append(", \"tier\": ").Append(S(BlackBoxSchema.TierName(c.Tier)));
                // -1 for Every/R0 means "rides the row rate", which is not a period and must not be
                // read as one. null says so in the type system instead of encoding a magic number.
                sb.Append(", \"period_s\": ").Append(period > 0.0 ? N(period) : "null");
                sb.Append(", \"provenance\": ").Append(S(c.Provenance));
                sb.Append(", \"source\": ").Append(S(c.Source));
                sb.Append(", \"fit\": ").Append(S(BlackBoxSchema.FitName(c.Fit)));
                // BB2. "vessel" = true of the craft named in this file's `vessel` column; "capsule" =
                // a DragonScreen/conductor singleton, written ONLY while this stream's vessel holds the
                // camera and blank otherwise. Without it, a reader of the booster's file cannot tell a
                // withheld capsule column from a broken one.
                sb.Append(", \"scope\": ").Append(S(BlackBoxSchema.ScopeName(c.Scope)));
                sb.Append(", \"note\": ").Append(c.Note == null ? "null" : S(c.Note));
                sb.Append('}');
                if (i < cols.Length - 1) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append("  ],\n");

            // ---- the tail: written at close, present-but-null before it ----
            sb.Append("  \"closed\": ").Append(m.Closed ? "true" : "false").Append(",\n");
            sb.Append("  \"closed_ut\": ").Append(m.Closed ? N(m.ClosedUt) : "null").Append(",\n");
            sb.Append("  \"closed_reason\": ").Append(m.Closed ? S(m.ClosedReason) : "null").Append(",\n");
            sb.Append("  \"rows_written\": ").Append(I(m.RowsWritten)).Append(",\n");
            sb.Append("  \"events_written\": ").Append(I(m.EventsWritten)).Append(",\n");
            sb.Append("  \"write_errors\": ").Append(I(m.WriteErrors)).Append(",\n");
            sb.Append("  \"max_rec_build_us\": ").Append(N(m.MaxRecBuildUs)).Append(",\n");
            // BB7: the distribution alongside the max, so a reader can tell a spike from a regression
            // without re-deriving it from the CSV's own per-row `rec_build_us` column.
            sb.Append("  \"p50_rec_build_us\": ").Append(N(m.P50RecBuildUs)).Append(",\n");
            sb.Append("  \"p90_rec_build_us\": ").Append(N(m.P90RecBuildUs)).Append(",\n");
            sb.Append("  \"p99_rec_build_us\": ").Append(N(m.P99RecBuildUs)).Append(",\n");

            // ---- the coverage verdict. An EMPTY array is a positive statement, not an absence: every
            // ---- Live column produced values and no Unfitted one did. That is the property S76 had to
            // ---- discover by auditing the corpus after the flights.
            sb.Append("  \"coverage\": [");
            if (m.Coverage != null)
                for (int i = 0; i < m.Coverage.Count; i++)
                {
                    CoverageFinding f = m.Coverage[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("\n    {\"column\": ").Append(S(f.Column));
                    sb.Append(", \"kind\": ").Append(S(f.Kind));
                    sb.Append(", \"defect\": ").Append(f.Defect ? "true" : "false");
                    sb.Append(", \"declared\": ").Append(S(f.Declared)).Append('}');
                }
            sb.Append(m.Coverage != null && m.Coverage.Count > 0 ? "\n  ]\n" : "]\n");
            sb.Append("}\n");
            return sb.ToString();
        }
    }
}
