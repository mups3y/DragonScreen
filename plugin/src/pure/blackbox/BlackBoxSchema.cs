// DragonScreen — BlackBox / SCHEMA  (register BB1; spec: docs/BLACKBOX_RESEARCH.md §2, §4.1–§4.2, §4.6)
// ============================================================================================
// PURE. The ordered column table is the SINGLE SOURCE OF TRUTH for the parameter stream, and it is a
// table of RECORDS, not a bare `string[]`.
//
// ---- WHY A TABLE OF RECORDS AND NOT `string[] Schema` ----
// Recorder B's best idea was `Schema[]`-as-single-ordered-source-of-truth with `static readonly int
// MetS = Index("met_s")` derived indices, so re-ordering the schema just works and positional drift is
// impossible. §3.4 says COMPOSE that verbatim, and it is composed here — `Schema`, `Index`, `NewRow`,
// `Num`, `Escape`, `Row`, `Header` are B's, unchanged in behaviour.
//
// What is ADDED is the reason §3.4 also says the manifest must be BUILT FRESH: a column's UNITS, its
// SAMPLE PERIOD, its SOURCE and its PROVENANCE have to be recorded somewhere, and the two recorders
// that flew recorded them nowhere. Recorder A carried units in the column NAME (`b_omegaRdps` vs
// `b_omegaR`, `massT`) and §3.4 files that as BREAK: "a rename silently changes results by 57×".
// Putting the metadata in a parallel array next to the names re-creates exactly the positional drift
// the `Index()` pattern exists to kill. So the name and its metadata are ONE record, declared once,
// and both `Schema` (for the CSV header) and the manifest's `columns[]` are DERIVED from it. There is
// no second place to update and nothing to keep in step.
//
// ---- ⭐ THE GHOST-COLUMN RULE, WHICH IS WHY `Fit` EXISTS ----
// S76 found three columns in the flown corpus that were DECLARED and NEVER WRITTEN — `torque_cmd`,
// `mode_holding`, `mode_flying`. A column that exists and is always empty fakes coverage: a reader
// looking for the commanded torque finds the column, finds it blank, and concludes the loop commanded
// nothing. BB1's register line requires this to fail loudly.
//
// But §2.5 is equally explicit the other way: the GUIDANCE columns "exist from day one and read blank,
// which is the honest state and is also how a real recorder reports an unfitted system". Both are
// right, and the difference between them is not the blankness — it is whether the FILE SAYS WHY.
// So every column declares its fitness, once, and the manifest carries it:
//
//   Fit.Live        a writer exists NOW and runs on every mission. Blank across a whole mission is a
//                   DEFECT — `BlackBoxCoverage` raises `rec.column_never_written` at close.
//   Fit.Conditional a writer exists, but its source can legitimately be absent (no target, KER not
//                   installed, the screens not running, this stream's vessel is not the booster). The
//                   condition is stated in `Note` and lands in the manifest, so a blank is readable.
//   Fit.Unfitted    NO source exists in the tree yet. `Note` names the REGISTER LINE that will fill it.
//                   This is §2.5's honest unfitted-system report, and it is exempt from the coverage
//                   check BY BEING DECLARED, not by being forgotten.
//
// A `Fit.Unfitted` column is therefore not a ghost: a ghost is a column claiming a source it never had.
//
// ---- ⭐ AND THE SECOND AXIS, ADDED BY BB2: `Scope` ----
// BB1 recorded ONE vessel, so "whose state is this?" never had to be asked. BB2 records two, and the
// answer stops being obvious: `Vessel.altitude` is the recorded vessel's, but `FlightCommands.State`,
// `CrewProcedureOps`, `FlightDriver` and `VesselData.State` are STATICS describing the CAPSULE. Copying
// them onto an unfocused booster's row would report the Dragon's bus voltages and mission phase as the
// booster's — a fabrication, not a recording. Every column therefore also declares `Scope` (see the
// enum below), the glue writes `Scope.Capsule` columns only on the focused vessel's row, and
// `BlackBoxCoverage` reads the declaration so a booster's blank capsule column is a NOTE and a
// capsule value that LEAKED onto a never-focused stream is a defect.
// ⚠ An `Fit.Unfitted` column's scope is left at the default `Scope.Vessel` DELIBERATELY: nothing is
// written there, so the declaration would be a guess, and the register line that fits the column
// (T17/T18/T19/S55) is the one that knows whose state it turned out to be. Guessing now would leave a
// wrong declaration behind that reads as settled.
//
// ---- WHAT IS DELIBERATELY NOT DECLARED HERE ----
// §2 lists columns this build has no writer for at all — not "not yet fitted by Part B", but "the pure
// module that produced them is deleted" or "reading them needs an edit to a file outside the BlackBox".
// Declaring those as Unfitted would be dishonest in the other direction: Unfitted means a NAMED line
// will fill it. They are LOGGED as register lines instead (C1.1) and are absent from the schema:
//   • `acc_att_imp` / `acc_trans_imp` / `acc_both_imp` (delivered RCS impulse) — needs the deleted
//     `pure/RcsAccounting.cs`.
//   • (S86, then S94/S86-Q1 filled the rest: `brightness_l/c/r`, `cover_cam_l/c/r`, `cover_phase_l/c/r`
//     WERE listed here as "private to a `ScreenPainter` INSTANCE, no accessor" — that gap is closed;
//     see §2.7 below for all nine.)
//   • `crew.touch` / `crew.press` / `crew.dispatch` events and the flat `control_id` namespace (§2.7's
//     ⚠) — that is a hook at two choke points inside the screens, i.e. a tree edit, and a separate line.
//   • `off_x/y/z_m`, `phase_angle_rad`, `tgt_radius_m` — only their FORMATTED text reaches `PageState`.
// ============================================================================================
using System;
using System.Globalization;
using System.Text;

namespace DragonScreen.BlackBox
{
    /// <summary>§2.0's rate ladder. Every interval is in UT seconds and every tier obeys §4.6's warp rule.</summary>
    public enum Tier : byte
    {
        /// <summary>On every row, unconditionally (§2.1 — the A block).</summary>
        Every = 0,
        /// <summary>Accumulated at the physics rate and emitted with the row — never sampled.</summary>
        R0 = 1,
        /// <summary>10 Hz. The dynamic block.</summary>
        R1 = 2,
        /// <summary>2 Hz. The state block.</summary>
        R2 = 3,
        /// <summary>0.1 Hz. The slow block.</summary>
        R3 = 4
    }

    /// <summary>Whether a declared column has a writer, and what a blank in it means. See the header.</summary>
    public enum Fit : byte { Live = 0, Conditional = 1, Unfitted = 2 }

    /// <summary>
    /// ⭐ REGISTER BB2. WHOSE state a column describes — which is a different question from whether it
    /// has a writer (<see cref="Fit"/>), and the question a one-stream recorder never had to ask.
    ///
    ///   Scope.Vessel   read from THIS STREAM'S OWN `Vessel`. Every stream writes it, and the value is
    ///                  true of the vessel named in the row's `vessel` column. Almost everything.
    ///   Scope.Capsule  read from a DragonScreen/conductor SINGLETON — `FlightCommands.State`,
    ///                  `CrewProcedureOps`, `FlightDriver`, `AbortControl`, `VesselData.State`. Those
    ///                  statics describe the CAPSULE, whichever vessel happens to be asking. Writing
    ///                  them onto an unfocused booster's row would put the Dragon's bus voltages, fire
    ///                  suppressant and mission phase on the booster and call it a measurement — §4.8's
    ///                  NEVER FABRICATE, and the same class of error as the frozen-under-warp control
    ///                  values that manufactured a phantom RCS thrash. So a Capsule column is written
    ///                  ONLY on the row of the stream whose vessel currently holds the camera, and is
    ///                  blank everywhere else (§4.6: blank, never a plausible number).
    ///
    /// `BlackBoxCoverage` reads this so a booster stream's blank `bus1_on` is a NOTE ("this stream never
    /// held focus") and not the ghost-column DEFECT it would be on the capsule's stream — and so that a
    /// Capsule value LEAKING onto a never-focused stream is a defect in its own right.
    /// </summary>
    public enum Scope : byte { Vessel = 0, Capsule = 1 }

    /// <summary>One column: its name AND everything the manifest has to say about it, in one record.</summary>
    public struct Col
    {
        public string Name;
        /// <summary>SI or the plain unit word. Never in the name — §3.4 files that as BREAK.</summary>
        public string Units;
        public Tier Tier;
        public Fit Fit;
        /// <summary>BB2: whose state this is — this stream's vessel, or the capsule singletons.</summary>
        public Scope Scope;
        /// <summary>ksp-direct | derived | screens | ker | tac-ls | conductor | recorder | SIMULATED.</summary>
        public string Provenance;
        /// <summary>Where it comes from, concretely — the API or the type read.</summary>
        public string Source;
        /// <summary>Conditional: WHEN it is blank. Unfitted: the REGISTER LINE that will fill it.</summary>
        public string Note;
    }

    public static class BlackBoxSchema
    {
        /// <summary>
        /// Bumped when a column is REORDERED or REMOVED; a pure append keeps the version (§4.2).
        /// The manifest carries it so an analyser refuses to chain files across a change — the rule
        /// `plugin/build/assess_flight.py` already enforces for its own corpus.
        /// </summary>
        public const int SchemaVersion = 1;
        /// <summary>
        /// BB2 adds NO column, removes none and reorders none, so <see cref="SchemaVersion"/> stays 1
        /// and a BB1 recording still chains with a BB2 one (§4.2's rule). What changed is the RECORDER:
        /// two streams per mission, a shared event log, and per-column <see cref="Scope"/> in the
        /// manifest — so the recorder version moves and the manifest says which build wrote the file.
        /// </summary>
        public const string RecorderVersion = "BB2.0";

        // ---- tier periods, in UT seconds (§2.0). R0 is accumulated, so it has no period of its own. ----
        public const double R1IntervalS = 0.1;    // 10 Hz — the dynamic block
        public const double R2IntervalS = 0.5;    //  2 Hz — the state block
        public const double R3IntervalS = 10.0;   // 0.1 Hz — the slow block

        static Col C(string name, string units, Tier tier, string prov, string source)
        {
            Col c = new Col();
            c.Name = name; c.Units = units; c.Tier = tier; c.Fit = Fit.Live; c.Scope = Scope.Vessel;
            c.Provenance = prov; c.Source = source; c.Note = null;
            return c;
        }

        static Col Cond(string name, string units, Tier tier, string prov, string source, string when)
        {
            Col c = C(name, units, tier, prov, source); c.Fit = Fit.Conditional; c.Note = when; return c;
        }

        static Col Unfit(string name, string units, Tier tier, string source, string line)
        {
            Col c = C(name, units, tier, "conductor", source); c.Fit = Fit.Unfitted; c.Note = line; return c;
        }

        // ---- BB2: the same two, marked Scope.Capsule. See the `Scope` enum for why this is not a
        // ---- cosmetic label: it is what stops the Dragon's singletons landing on the booster's rows.
        static Col Cap(string name, string units, Tier tier, string prov, string source)
        {
            Col c = C(name, units, tier, prov, source); c.Scope = Scope.Capsule; return c;
        }

        static Col CondCap(string name, string units, Tier tier, string prov, string source, string when)
        {
            Col c = Cond(name, units, tier, prov, source, when); c.Scope = Scope.Capsule; return c;
        }

        // Recurring condition strings, named once so a hundred columns cannot drift into wording them
        // differently. The manifest prints them verbatim and BB3 groups on them.
        const string WhenScreens = "blank unless the screens are running AND this stream's vessel is the "
                                 + "focused one (PageState is the screens' 5 Hz copy — a stale one would be a "
                                 + "measurement that is not a measurement)";
        const string WhenKer     = "blank unless ker_avail = 1 (Kerbal Engineer installed, driven and reporting)";
        const string WhenTarget  = "blank unless a target is selected";
        const string WhenBooster = "blank unless this stream's vessel is the vessel BoosterHost has bound";
        const string WhenLs      = "blank unless a life-support mod supplies it (TAC-LS)";

        /// <summary>
        /// ⛔ THE ORDERED SOURCE OF TRUTH. Append freely inside a schema_version; reordering or removing
        /// bumps <see cref="SchemaVersion"/>. Nothing reads a column by position — see <see cref="Index"/>.
        /// </summary>
        public static readonly Col[] Columns =
        {
            // ================= A — TIME AND FRAME (§2.1: every row, unconditionally) =================
            C("mission_id",   "string", Tier.Every, "recorder",   "the recorder's own mission id"),
            C("seq",          "count",  Tier.Every, "recorder",   "monotonic row counter — a gap IS a dropped row"),
            C("ut",           "s",      Tier.Every, "ksp-direct", "Planetarium.GetUniversalTime()"),
            C("met_s",        "s",      Tier.Every, "ksp-direct", "Vessel.missionTime"),
            C("wall_s",       "s",      Tier.Every, "ksp-direct", "Time.realtimeSinceStartup"),
            C("warp_rate",    "x",      Tier.Every, "ksp-direct", "TimeWarp.CurrentRate"),
            C("warp_rails",   "0/1",    Tier.Every, "ksp-direct", "TimeWarp.WarpMode == HIGH && CurrentRateIndex > 0"),
            C("vessel",       "string", Tier.Every, "ksp-direct", "Vessel.vesselName (this stream's vessel)"),
            C("focus",        "string", Tier.Every, "ksp-direct", "FlightGlobals.ActiveVessel.vesselName"),
            C("rec_build_us", "us",     Tier.Every, "recorder",   "the recorder timing ITSELF (§1.4(b))"),

            // ================= B — VEHICLE STATE (§2.2) =================
            C("alt_m",         "m",     Tier.R2, "ksp-direct", "Vessel.altitude"),
            C("alt_radar_m",   "m",     Tier.R2, "ksp-direct", "Vessel.radarAltitude"),
            C("speed_mps",     "m/s",   Tier.R2, "ksp-direct", "Vessel.obt_speed"),
            C("srf_speed_mps", "m/s",   Tier.R2, "ksp-direct", "Vessel.srfSpeed"),
            C("vspeed_mps",    "m/s",   Tier.R2, "ksp-direct", "Vessel.verticalSpeed"),
            C("lat_deg",       "deg",   Tier.R2, "ksp-direct", "Vessel.latitude"),
            C("lon_deg",       "deg",   Tier.R2, "ksp-direct", "Vessel.longitude"),
            C("downrange_m",   "m",     Tier.R2, "derived",    "great-circle from the latched launch lat/lon"),
            C("atm_density",   "kg/m3", Tier.R2, "ksp-direct", "Vessel.atmDensity"),
            C("mass_kg",       "kg",    Tier.R2, "ksp-direct", "Vessel.totalMass x 1000"),
            C("moi_pitch",     "t.m2",  Tier.R2, "ksp-direct", "Vessel.MOI.x"),
            C("moi_roll",      "t.m2",  Tier.R2, "ksp-direct", "Vessel.MOI.y"),
            C("moi_yaw",       "t.m2",  Tier.R2, "ksp-direct", "Vessel.MOI.z"),
            C("ap_km",         "km",    Tier.R2, "ksp-direct", "Orbit.ApA / 1000"),
            C("pe_km",         "km",    Tier.R2, "ksp-direct", "Orbit.PeA / 1000"),
            C("inc_deg",       "deg",   Tier.R2, "ksp-direct", "Orbit.inclination"),
            C("raan_deg",      "deg",   Tier.R2, "ksp-direct", "Orbit.LAN"),
            C("ecc",           "-",     Tier.R2, "ksp-direct", "Orbit.eccentricity"),
            C("sma_m",         "m",     Tier.R2, "ksp-direct", "Orbit.semiMajorAxis"),
            C("argp_deg",      "deg",   Tier.R2, "ksp-direct", "Orbit.argumentOfPeriapsis"),
            C("ta_deg",        "deg",   Tier.R2, "ksp-direct", "Orbit.trueAnomaly"),
            C("period_s",      "s",     Tier.R2, "ksp-direct", "Orbit.period"),
            C("t_ap_s",        "s",     Tier.R2, "ksp-direct", "Orbit.timeToAp"),
            C("t_pe_s",        "s",     Tier.R2, "ksp-direct", "Orbit.timeToPe"),
            C("mach",          "-",     Tier.R1, "ksp-direct", "Vessel.mach"),
            C("q_pa",          "Pa",    Tier.R1, "ksp-direct", "Vessel.dynamicPressurekPa x 1000"),
            C("accel_g",       "g",     Tier.R1, "ksp-direct", "Vessel.geeForce"),
            C("accel_axial_g", "g",     Tier.R1, "derived",    "dot(Vessel.acceleration, ReferenceTransform.up)/9.80665"),
            C("pitch_deg",     "deg",   Tier.R1, "derived",    "surface-frame attitude from ReferenceTransform vs local up/north"),
            C("heading_deg",   "deg",   Tier.R1, "derived",    "same frame"),
            C("roll_deg",      "deg",   Tier.R1, "derived",    "same frame"),
            Cond("aoa_deg",    "deg",   Tier.R1, "derived",    "angle of the surface velocity below the nose, pitch plane",
                 "blank below 1 m/s surface speed, where an angle of attack has no meaning"),
            Cond("aos_deg",    "deg",   Tier.R1, "derived",    "the same angle in the yaw plane (sideslip)",
                 "blank below 1 m/s surface speed, where a sideslip angle has no meaning"),
            C("rate_pitch_dps", "deg/s", Tier.R1, "ksp-direct", "Vessel.angularVelocity.x x Rad2Deg"),
            C("rate_roll_dps",  "deg/s", Tier.R1, "ksp-direct", "Vessel.angularVelocity.y x Rad2Deg"),
            C("rate_yaw_dps",   "deg/s", Tier.R1, "ksp-direct", "Vessel.angularVelocity.z x Rad2Deg"),
            // ---- R0: the in-interval extrema a snapshot straddles (§2.0's justification for the tier) ----
            C("accel_g_peak",  "g",     Tier.R0, "derived", "max Vessel.geeForce over the row interval, at the physics rate"),
            C("q_pa_peak",     "Pa",    Tier.R0, "derived", "max dynamic pressure over the row interval"),
            C("rate_peak_dps", "deg/s", Tier.R0, "derived", "max |angular velocity| over the row interval"),
            C("body",          "string", Tier.R3, "ksp-direct", "Vessel.mainBody.bodyName — so no reader detects the body from the data"),

            // ================= C — PROPULSION (§2.3) =================
            C("throttle",     "0..1", Tier.R1, "ksp-direct", "Vessel.ctrlState.mainThrottle"),
            C("thrust_n",     "N",    Tier.R1, "ksp-direct", "sum of ModuleEngines.finalThrust x 1000"),
            C("eng_ignited",  "count", Tier.R1, "ksp-direct", "ModuleEngines.EngineIgnited count — a commanded ignition is PROVABLE"),
            C("eng_flameout", "count", Tier.R1, "ksp-direct", "ModuleEngines.flameout count"),
            C("stage",        "int",  Tier.R2, "ksp-direct", "StageManager.CurrentStage"),
            C("rcs_on",       "0/1",  Tier.R2, "ksp-direct", "Vessel.ActionGroups[RCS]"),
            C("ec_frac",      "0..1", Tier.R2, "ksp-direct", "GetConnectedResourceTotals(ElectricCharge)"),
            Cond("mmh_frac",  "0..1", Tier.R2, "ksp-direct", "GetConnectedResourceTotals(MMH)",
                 "blank where the install does not define MMH or the vessel carries none"),
            Cond("nto_frac",  "0..1", Tier.R2, "ksp-direct", "GetConnectedResourceTotals(NTO)",
                 "blank where the install does not define NTO or the vessel carries none"),
            CondCap("prop_frac", "0..1", Tier.R2, "screens", "PageState.Propellant01", WhenScreens),
            // §1.4(e) wants "log WHETHER there is a result" so a blank is never read as a zero — and
            // this column does exactly that FOR KER. But its own source is `PageState.Ker`, which only
            // exists while the screens are running, so the flag itself is conditional on the same thing
            // the values are. Declaring it Live would make every screens-less recording report a defect.
            CondCap("ker_avail", "0/1", Tier.R3, "screens", "PageState.Ker.HasResult", WhenScreens),
            CondCap("ker_stage_dv",      "m/s", Tier.R3, "ker", "PageState.Ker.DeltaVMps",          WhenKer),
            CondCap("ker_total_dv",      "m/s", Tier.R3, "ker", "PageState.Ker.RemainingDeltaVMps", WhenKer),
            CondCap("ker_twr",           "-",   Tier.R3, "ker", "PageState.Ker.Twr",                WhenKer),
            CondCap("ker_isp",           "s",   Tier.R3, "ker", "PageState.Ker.IspS",               WhenKer),
            CondCap("ker_burn_s",        "s",   Tier.R3, "ker", "PageState.Ker.BurnTimeS",          WhenKer),
            CondCap("ker_stage_mass_kg", "kg",  Tier.R3, "ker", "PageState.Ker.StageMassKg",        WhenKer),
            CondCap("ker_thrust_avail_n", "N",  Tier.R3, "ker", "PageState.Ker.ThrustN",            WhenKer),
            Unfit("dv_planned",    "m/s", Tier.R2, "conductor / Node Executor",     "T19"),
            Unfit("dv_delivered",  "m/s", Tier.R2, "conductor / Node Executor",     "T19"),
            Unfit("dv_residual",   "m/s", Tier.R2, "conductor / Node Executor",     "T19"),
            Unfit("dv_grav_loss",  "m/s", Tier.R2, "conductor ascent-loss decomposition", "T18"),
            Unfit("dv_drag_loss",  "m/s", Tier.R2, "conductor ascent-loss decomposition", "T18"),
            Unfit("dv_steer_loss", "m/s", Tier.R2, "conductor ascent-loss decomposition", "T18"),

            // ================= D — CONTROL AND ACTUATION (§2.4) =================
            // ⛔ §2.4's THREE KINDS MUST NOT BE CONFLATED, and the naming here is the whole guard:
            //   app_*  = APPLIED — what was written to FlightCtrlState, whoever wrote it (crew or a
            //            controller). NOT delivered force: KSP's RCS solver owns that. Per-tick
            //            snapshots, so they ALIAS the ~0.06 s RCS pulse dwell. Use acc_* for duty.
            //   act_*  = REQUESTED — a controller's pre-pulse demand. The only controller in the tree
            //            is BoosterHost, so these are Conditional on the booster, not Live.
            //   acc_*  = ACCUMULATED at the physics rate and reset each row. The only UN-ALIASED basis.
            C("app_pitch", "-1..1", Tier.R1, "ksp-direct", "Vessel.ctrlState.pitch"),
            C("app_yaw",   "-1..1", Tier.R1, "ksp-direct", "Vessel.ctrlState.yaw"),
            C("app_roll",  "-1..1", Tier.R1, "ksp-direct", "Vessel.ctrlState.roll"),
            C("app_tx",    "-1..1", Tier.R1, "ksp-direct", "Vessel.ctrlState.X"),
            C("app_ty",    "-1..1", Tier.R1, "ksp-direct", "Vessel.ctrlState.Y"),
            C("app_tz",    "-1..1", Tier.R1, "ksp-direct", "Vessel.ctrlState.Z"),
            C("att_rate_meas", "deg/s", Tier.R1, "derived", "|Vessel.angularVelocity| x Rad2Deg"),
            C("ctrl_tq_pitch", "kN.m", Tier.R2, "ksp-direct", "sum of ITorqueProvider.GetPotentialTorque, pitch"),
            C("ctrl_tq_yaw",   "kN.m", Tier.R2, "ksp-direct", "sum of ITorqueProvider.GetPotentialTorque, yaw"),
            C("ctrl_tq_roll",  "kN.m", Tier.R2, "ksp-direct", "sum of ITorqueProvider.GetPotentialTorque, roll"),
            C("rcs_thrust_n",  "N",    Tier.R2, "ksp-direct", "sum of enabled ModuleRCS.thrusterPower x 1000"),
            // ---- R0: the accumulators. §3.2's RETRACTION is why these exist: the act_* per-tick
            // ---- snapshots produced a "68-82 % duty" figure that had to be WITHDRAWN, and only the
            // ---- physics-rate accumulators settled it. Anything that pulses is accumulated, not sampled.
            C("acc_int_s",   "s", Tier.R0, "derived", "wall/UT length of the accumulation interval — the denominator"),
            C("acc_att_s",   "s", Tier.R0, "derived", "time with an attitude command and no translation command"),
            C("acc_trans_s", "s", Tier.R0, "derived", "time with a translation command and no attitude command"),
            C("acc_both_s",  "s", Tier.R0, "derived", "time with both commanded"),
            C("acc_none_s",  "s", Tier.R0, "derived", "time with neither commanded"),
            C("acc_app_att",   "cmd.s", Tier.R0, "derived", "integral of max|app_pitch,app_yaw,app_roll| dt"),
            C("acc_app_trans", "cmd.s", Tier.R0, "derived", "integral of max|app_tx,app_ty,app_tz| dt"),
            C("act_sat_s",   "s", Tier.R0, "derived", "time with max|applied attitude command| >= 0.99 — saturation IS out of authority"),
            Unfit("act_pitch", "-1..1", Tier.R1, "a controller's pre-pulse demand", "T17"),
            Unfit("act_yaw",   "-1..1", Tier.R1, "a controller's pre-pulse demand", "T17"),
            Unfit("act_roll",  "-1..1", Tier.R1, "a controller's pre-pulse demand", "T17"),
            Unfit("acc_req_att",   "cmd.s", Tier.R0, "requested (pre-pulse) attitude command-seconds",    "T17"),
            Unfit("acc_req_trans", "cmd.s", Tier.R0, "requested (pre-pulse) translation command-seconds", "T17"),
            Unfit("att_err_deg",  "deg",   Tier.R1, "the attitude loop's own pointing error", "T17"),
            Unfit("att_rate_cmd", "deg/s", Tier.R1, "the attitude loop's commanded rate",     "T17"),

            // ---- ⭐ THE BOOSTER OBSERVABILITY BLOCK — the owner's Q2 refinement, 2026-09-04. ----
            // `pure/BoosterSteer.cs`'s header names THIS REGISTER LINE as the reader for the deadband
            // seam: "a knob enable-able from config that never appears in a recording cannot be
            // diagnosed". `src/BoosterHost.cs` publishes them read-only; here is where they are read.
            // `DeadbandDeg` defaults to 0.0 (behaviourally no deadband), so on a default flight the
            // three flags read 0 and the value reads 0 — which is the PROOF the seam was inert, and is
            // exactly why it must be recorded rather than inferred.
            Cond("boost_db_pitch", "0/1", Tier.R1, "ksp-direct", "BoosterHost.SteerPitchDeadbanded", WhenBooster),
            Cond("boost_db_yaw",   "0/1", Tier.R1, "ksp-direct", "BoosterHost.SteerYawDeadbanded",   WhenBooster),
            Cond("boost_db_roll",  "0/1", Tier.R1, "ksp-direct", "BoosterHost.SteerRollDeadbanded",  WhenBooster),
            Cond("boost_db_deg",   "deg", Tier.R2, "ksp-direct", "BoosterHost.SteerDeadbandDeg — the value the deadband RAN at", WhenBooster),
            Cond("boost_steer_pitch", "-1..1", Tier.R1, "ksp-direct", "BoosterHost.SteerPitch (the REQUESTED command)", WhenBooster),
            Cond("boost_steer_yaw",   "-1..1", Tier.R1, "ksp-direct", "BoosterHost.SteerYaw",   WhenBooster),
            Cond("boost_steer_roll",  "-1..1", Tier.R1, "ksp-direct", "BoosterHost.SteerRoll",  WhenBooster),
            Cond("boost_throttle",    "0..1", Tier.R1, "ksp-direct", "BoosterHost.Throttle",    WhenBooster),
            Cond("boost_phase",       "enum", Tier.R2, "ksp-direct", "BoosterHost.Phase",       WhenBooster),
            Cond("boost_uncommanded", "0/1",  Tier.R2, "ksp-direct", "BoosterHost.AttitudeUncommanded — the axes were NOT held this tick", WhenBooster),
            // BB9: WHY boost_uncommanded is 1 this tick. `BoosterHost.Block`, the stable enum
            // `BoosterHostPlan.BlockedFor` returns (NotArmed/Packed/NoOctaweb/WrongEngineForPhase/
            // HoldOff/None) — recorded by name (`.ToString()`), exactly as boost_phase is, so a reader
            // filters on it without depending on BlockNote's prose, which Annunciation is free to reword.
            Cond("boost_block", "enum", Tier.R2, "ksp-direct", "BoosterHost.Block", WhenBooster),
            // [[OCT11]]: `currentRole` is a COMMAND record, not a reading (see BoosterHost.Dispatch's own
            // doc comment) — it can be wrong for one tick or many (an Activate() throw, or RealFuels
            // refusing ignition on ullage: flight Crew-2_20260829_144114, "eng_ignited=0 whole descent").
            // `eng_ignited` already lets a reader INFER that from the vessel-wide count; this STATES it,
            // per-bank, off the already-bound module. The retry policy (if any) is register W5's — this
            // column and its paired BlackBox event (`boost.commanded_not_ignited` / `.ignition_resolved`)
            // only announce.
            Cond("boost_cmd_not_ignited", "0/1", Tier.R2, "ksp-direct",
                 "BoosterHost.CommandedNotIgnited — currentRole commands a bank this module reports unlit", WhenBooster),

            // ================= E — GUIDANCE (§2.5) =================
            // The seams are idle (`src/_AutopilotStub.cs`). gnc_engaged and mode_index have REAL writers
            // that return a REAL constant, and §2.5 is explicit that recording that constant "is itself
            // the proof the seam was idle" — so they are Live, not Unfitted. The rest have no source at
            // all and each names the increment that fills it (§B12.5: one property per increment).
            Cap("gnc_engaged", "0/1",  Tier.R2, "conductor", "AutoPilot.Engaged — LIVE since W10: the crew-gate conductor is engaged (not flying)"),
            Cap("mode_index",  "enum", Tier.R2, "conductor", "FlightDriver.MissionMode (idle seam — constant Idle until T17)"),
            Unfit("gnc_module",   "string", Tier.R2, "the MechJeb module the conductor has engaged", "T17"),
            Unfit("gnc_status",   "string", Tier.R2, "that module's own status/convergence word",    "T17"),
            Unfit("pvg_vgo_mps",  "m/s",    Tier.R1, "PVG guidance",                                 "T18"),
            Unfit("pvg_tgo_s",    "s",      Tier.R1, "PVG guidance",                                 "T18"),
            Unfit("cmd_pitch_deg","deg",    Tier.R1, "the conductor's command struct",               "T18"),
            Unfit("cmd_heading_deg","deg",  Tier.R1, "the conductor's command struct",               "T18"),
            Unfit("cmd_throttle", "0..1",   Tier.R1, "the conductor's command struct",               "T18"),
            Unfit("tgt_ap_km",    "km",     Tier.R3, "the ascent settings the conductor loaded",     "T18"),
            Unfit("tgt_pe_km",    "km",     Tier.R3, "the ascent settings the conductor loaded",     "T18"),
            Unfit("tgt_inc_deg",  "deg",    Tier.R3, "the ascent settings the conductor loaded",     "T18"),
            Unfit("node_dv_left", "m/s",    Tier.R1, "Node Executor",                                "T19"),
            Unfit("node_point_err","deg",   Tier.R1, "Node Executor",                                "T19"),
            Unfit("replan_count", "count",  Tier.R2, "the §B12.4 re-plan loop",                      "T19"),
            Unfit("deviation_m",  "m",      Tier.R2, "conductor: predicted vs actual",               "T19"),
            Unfit("deviation_mps","m/s",    Tier.R2, "conductor: predicted vs actual",               "T19"),

            // ================= F — MISSION AND CREW GATES (§2.6) =================
            // ⭐ mission_phase and phase_classified are recorded SEPARATELY on purpose (§2.6): a
            // conductor/classifier disagreement must be VISIBLE rather than resolved silently. That is
            // a (b)-class independent cross-check on our own FSM, and it costs one column.
            Cap("mission_phase",    "enum", Tier.R2, "derived", "Mission.AuthoritativePhase(CrewProcedureOps..., classified)"),
            C("phase_classified", "enum", Tier.R2, "derived", "Mission.Classify(MissionInputs) built from THIS stream's vessel"),
            Cap("gate_id",       "enum", Tier.R2, "conductor", "CrewProcedureOps.CurrentGate().Id — LIVE since W10"),
            Cap("gate_phase",    "enum", Tier.R2, "conductor", "CrewProcedureOps.Proc.Phase — LIVE since W10"),
            Cap("crew_action",   "0/1",  Tier.R2, "conductor", "CrewProcedureOps.CrewActionNeeded() — LIVE since W10"),
            Cap("gate_satisfied_mask", "bits", Tier.R2, "conductor", "ProcState.Satisfied[] packed — WHICH items were satisfied at the release"),
            Cap("is_return",     "0/1",  Tier.R3, "conductor", "CrewProcedureOps.IsReturn — LIVE since W10"),
            CondCap("step_ack_mask", "bits", Tier.R2, "screens", "PageState.Steps.Acknowledged — the crew's own ack channel", WhenScreens),
            Unfit("step_id",    "enum", Tier.R2, "StepList's live step", "S55"),
            Unfit("step_state", "enum", Tier.R2, "StepList's live step state", "S55"),

            // ================= G — CREW AND SCREENS (§2.7, the CVR analogue) =================
            // §2.7: a page SELECTION is a state, recorded continuously; a PRESS is an act and is an
            // event. The press half needs a hook at the two choke points inside the screens — a tree
            // edit BB1 may not make (excisable by design) — and is logged as its own register line.
            CondCap("page_l",   "enum", Tier.R2, "screens", "PageState.ScreenPages[1]", WhenScreens),
            CondCap("page_c",   "enum", Tier.R2, "screens", "PageState.ScreenPages[2]", WhenScreens),
            CondCap("page_r",   "enum", Tier.R2, "screens", "PageState.ScreenPages[3]", WhenScreens),
            CondCap("cam_view", "int",  Tier.R3, "screens", "VesselData.CameraView",    WhenScreens),
            // S86: `ScreenPainter.Brightness` is a SHARED static (one cabin brightness knob, not a
            // per-panel one — see its own header) so all three columns legitimately carry the SAME
            // value every tick; that is the real behaviour of this mod, not a fabrication.
            CondCap("brightness_l", "tenths", Tier.R3, "screens", "ScreenPainter.Brightness", WhenScreens),
            CondCap("brightness_c", "tenths", Tier.R3, "screens", "ScreenPainter.Brightness", WhenScreens),
            CondCap("brightness_r", "tenths", Tier.R3, "screens", "ScreenPainter.Brightness", WhenScreens),
            // S94 — S86-Q1 (asked by S86, answered by the overseer 2026-09-05: option 1, split into
            // l/c/r). Unlike brightness, `coverCam`/`coverPhase` are independent per-`ScreenPainter`-
            // INSTANCE state (S86 verified: no cross-instance write, unlike `livePage`/`Publish()`), and
            // every screen can default onto Cover simultaneously — so a single `cover_cam`/`cover_phase`
            // column has no honest value to report and would silently drop two-thirds of the real state,
            // the exact failure §4.8/the `Scope` header exist to prevent (S86's option 2, refused). Rides
            // page_l/c/r's shape exactly: same tier (R2 — a camera/phase pick changes on the same touch
            // cadence as a page change, not a slow cabin setting like brightness), same Cond/Capsule/
            // WhenScreens. `cover_cam` is a real `CoverPage.CoverCam` enum — recorded by NAME (`.ToString()`),
            // never an ordinal, exactly as `boost_block`/`boost_phase` are. `cover_phase` is a plain
            // `int` in `ScreenPainter` (0..6), NOT a C# enum, and `CoverPage.PhaseName[]` cannot round-
            // trip it losslessly — indices 3 and 4 BOTH display "Procedure" — so the raw index is
            // recorded rather than a name that would conflate two distinct phases.
            CondCap("cover_cam_l",   "enum",  Tier.R2, "screens", "ScreenPainter.CoverCamL",   WhenScreens),
            CondCap("cover_cam_c",   "enum",  Tier.R2, "screens", "ScreenPainter.CoverCamC",   WhenScreens),
            CondCap("cover_cam_r",   "enum",  Tier.R2, "screens", "ScreenPainter.CoverCamR",   WhenScreens),
            CondCap("cover_phase_l", "index", Tier.R2, "screens", "ScreenPainter.CoverPhaseL", WhenScreens),
            CondCap("cover_phase_c", "index", Tier.R2, "screens", "ScreenPainter.CoverPhaseC", WhenScreens),
            CondCap("cover_phase_r", "index", Tier.R2, "screens", "ScreenPainter.CoverPhaseR", WhenScreens),

            // ================= H — SYSTEMS, ENVIRONMENT AND FAULTS (§2.8) =================
            // FlightCommands.State is the systems model itself and exists whether or not the screens
            // run, so these are Live. The CABIN readout is computed BY the screens' 5 Hz pass, so it is
            // Conditional — recording a stale copy would be a measurement that is not a measurement.
            Cap("bus1_on", "0/1", Tier.R2, "SIMULATED", "SystemsState.Bus1On (pure VehicleSystems — display-state model)"),
            Cap("bus2_on", "0/1", Tier.R2, "SIMULATED", "SystemsState.Bus2On"),
            Cap("str_a1", "enum", Tier.R2, "SIMULATED", "SystemsState.A1"),
            Cap("str_b1", "enum", Tier.R2, "SIMULATED", "SystemsState.B1"),
            Cap("str_c1", "enum", Tier.R2, "SIMULATED", "SystemsState.C1"),
            Cap("str_a2", "enum", Tier.R2, "SIMULATED", "SystemsState.A2"),
            Cap("str_b2", "enum", Tier.R2, "SIMULATED", "SystemsState.B2"),
            Cap("str_c2", "enum", Tier.R2, "SIMULATED", "SystemsState.C2"),
            Cap("fire_intensity", "0..1", Tier.R2, "SIMULATED", "SystemsState.FireIntensity"),
            Cap("suppressant",    "0..1", Tier.R2, "SIMULATED", "SystemsState.Suppressant"),
            Cap("leak_rate",      "-",    Tier.R2, "SIMULATED", "SystemsState.LeakRate"),
            Cap("isolating",      "0/1",  Tier.R2, "SIMULATED", "SystemsState.Isolating"),
            Cap("o2_store",       "0..1", Tier.R3, "SIMULATED", "SystemsState.Oxygen"),
            Cap("n2_store",       "0..1", Tier.R3, "SIMULATED", "SystemsState.Nitrogen"),
            Cap("canister_used",  "0..1", Tier.R3, "SIMULATED", "SystemsState.CanisterUsed"),
            CondCap("cabin_psia",   "psia", Tier.R2, "SIMULATED", "CabinReadout.PressPsia (pure CabinEnvironment, from real state)", WhenScreens),
            CondCap("ppo2_psia",    "psia", Tier.R3, "SIMULATED", "CabinReadout.Ppo2Psia",  WhenScreens),
            CondCap("co2_mmhg",     "mmHg", Tier.R3, "SIMULATED", "CabinReadout.Co2MmHg",   WhenScreens),
            CondCap("cabin_temp_c", "degC", Tier.R3, "SIMULATED", "CabinReadout.CabinTempC", WhenScreens),
            CondCap("loop_a_c",     "degC", Tier.R3, "SIMULATED", "CabinReadout.LoopAC",    WhenScreens),
            CondCap("loop_b_c",     "degC", Tier.R3, "SIMULATED", "CabinReadout.LoopBC",    WhenScreens),
            C("ls_present",      "0/1",  Tier.R3, "tac-ls", "LifeSupportBridge.Margins(v).Present — §1.4(e): log WHETHER there is a result"),
            Cond("ls_o2_days",   "days", Tier.R3, "tac-ls", "LsMargins.OxygenDays",   WhenLs),
            Cond("ls_water_days","days", Tier.R3, "tac-ls", "LsMargins.WaterDays",    WhenLs),
            Cond("ls_food_days", "days", Tier.R3, "tac-ls", "LsMargins.FoodDays",     WhenLs),
            Cond("ls_limiting_days","days", Tier.R3, "tac-ls", "LsMargins.LimitingDays", WhenLs),
            C("skin_temp_frac", "0..1", Tier.R2, "ksp-direct", "hottest part skinTemperature/skinMaxTemp — a max-Q Overheat! was invisible without it"),
            C("hull_temp_c",    "degC", Tier.R2, "ksp-direct", "that part's skin temperature, in degrees C"),
            CondCap("sev_system",  "enum", Tier.R2, "derived", "Alarms.SystemSeverity(PageState)",  WhenScreens),
            CondCap("sev_vehicle", "enum", Tier.R2, "derived", "Alarms.VehicleSeverity(PageState)", WhenScreens),
            CondCap("sev_ls",      "enum", Tier.R2, "derived", "Alarms.LifeSupport(CabinReadout)",  WhenScreens),
            CondCap("sev_thermal", "enum", Tier.R2, "derived", "Alarms.Thermal(CabinReadout)",      WhenScreens),
            CondCap("alarm_mask",  "bits", Tier.R2, "derived", "Alarms.Mask(PageState) — the file's own comment calls it THE ALARM CHANNEL", WhenScreens),
            Cap("fdir_fault",    "enum", Tier.R2, "conductor", "FlightDriver.LastFdirReport.Fault (idle seam)"),
            Cap("fdir_recovery", "enum", Tier.R2, "conductor", "FlightDriver.LastFdirReport.Response (idle seam)"),
            Cap("aborting",      "0/1",  Tier.R1, "conductor", "FlightDriver.Aborting (idle seam)"),
            Cap("abort_mode",    "enum", Tier.R1, "conductor", "AbortControl.Mode (idle seam)"),
            C("comm_linked",   "0/1",  Tier.R3, "ksp-direct", "Vessel.connection.IsConnected"),
            C("comm_signal",   "0..1", Tier.R3, "ksp-direct", "Vessel.connection.SignalStrength"),
            Cond("range_m",      "m",   Tier.R2, "ksp-direct", "distance to Vessel.targetObject",              WhenTarget),
            Cond("closing_mps",  "m/s", Tier.R1, "derived",    "range rate — NEGATIVE closing; R1 so it is differentiable", WhenTarget),
            CondCap("align_deg",    "deg", Tier.R2, "screens",    "PageState.Align01 x 90",                       WhenTarget + "; " + WhenScreens),
            CondCap("roll_err_deg", "deg", Tier.R2, "screens",    "PageState.RollDeg",                            WhenTarget + "; " + WhenScreens),
            CondCap("pitch_err_deg","deg", Tier.R2, "screens",    "PageState.PitchDeg",                           WhenTarget + "; " + WhenScreens),
            CondCap("yaw_err_deg",  "deg", Tier.R2, "screens",    "PageState.YawDeg",                             WhenTarget + "; " + WhenScreens),
        };

        /// <summary>The ordered column NAMES — derived, so it can never disagree with the table.</summary>
        public static readonly string[] Schema = BuildNames();

        static string[] BuildNames()
        {
            string[] n = new string[Columns.Length];
            for (int i = 0; i < Columns.Length; i++) n[i] = Columns[i].Name;
            return n;
        }

        public static int Width { get { return Columns.Length; } }

        /// <summary>
        /// COMPOSED VERBATIM from Recorder B (§3.4): indices are looked up FROM the schema, so
        /// re-ordering the schema just works and positional drift is impossible. -1 for an absent name,
        /// and <see cref="Set"/> ignores -1 — a column that is removed does not crash its writers, it
        /// simply stops being written, and then <see cref="BlackBoxCoverage"/> has nothing to complain
        /// about because the column is gone too.
        /// </summary>
        public static int Index(string name)
        {
            for (int i = 0; i < Columns.Length; i++) if (Columns[i].Name == name) return i;
            return -1;
        }

        /// <summary>Sample period in UT seconds for the manifest. R0/Every ride the row rate: -1.</summary>
        public static double PeriodOf(Tier t)
        {
            switch (t)
            {
                case Tier.R1: return R1IntervalS;
                case Tier.R2: return R2IntervalS;
                case Tier.R3: return R3IntervalS;
                default: return -1.0;   // Every and R0 are emitted with the row
            }
        }

        public static string TierName(Tier t)
        {
            switch (t)
            {
                case Tier.R0: return "R0";
                case Tier.R1: return "R1";
                case Tier.R2: return "R2";
                case Tier.R3: return "R3";
                default: return "every";
            }
        }

        public static string FitName(Fit f)
        {
            switch (f)
            {
                case Fit.Conditional: return "conditional";
                case Fit.Unfitted: return "unfitted";
                default: return "live";
            }
        }

        /// <summary>BB2: the manifest's `scope` word. See <see cref="Scope"/>.</summary>
        public static string ScopeName(Scope s)
        {
            return s == Scope.Capsule ? "capsule" : "vessel";
        }

        /// <summary>
        /// BB2: is this column one of the capsule singletons? Asked by the glue once per block and by
        /// <see cref="BlackBoxCoverage"/> at close, by INDEX, so no name string is compared per row.
        /// </summary>
        public static bool IsCapsule(int col)
        {
            return col >= 0 && col < Columns.Length && Columns[col].Scope == Scope.Capsule;
        }

        // ================================ formatting ================================
        // COMPOSED from Recorder B (§3.4). Recorder A destroyed commas (`s.Replace(',', ';')`) and
        // coerced NaN to 0.0 — both are data-falsifying; B's are correct, so B's are what is here.

        /// <summary>
        /// ⛔ INVARIANT CULTURE, ALWAYS. A European locale writes "1,5" and shreds the CSV.
        /// ⛔ NaN/Inf → BLANK, never 0.0 (§4.6). A zero is a legitimate measurement; a blank is not,
        /// which is the entire reason the distinction is worth a rule.
        /// </summary>
        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        public static string Bit(bool b) { return b ? "1" : "0"; }

        /// <summary>RFC-4180: quote on , " CR or LF, and double any inner quote.</summary>
        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        /// <summary>
        /// A row pre-filled with BLANK, which is the foundation of §4.6's validity rule: a cell that no
        /// writer touched this tick reads "not sampled", and is distinguishable from a measured zero.
        /// </summary>
        public static string[] NewRow()
        {
            string[] cells = new string[Columns.Length];
            for (int i = 0; i < cells.Length; i++) cells[i] = "";
            return cells;
        }

        public static string Header()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Columns.Length; i++) { if (i > 0) sb.Append(','); sb.Append(Escape(Columns[i].Name)); }
            return sb.ToString();
        }

        public static string Row(string[] cells)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Columns.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Escape(i < cells.Length ? cells[i] : ""));
            }
            return sb.ToString();
        }

        public static void Set(string[] c, int col, double v) { if (col >= 0 && col < c.Length) c[col] = Num(v); }
        // ⛔ NOT escaped here — `Row()` escapes on the way out. Escaping twice turns a comma into a quoted
        // quoted field and the value stops round-tripping, which is a silent data change.
        public static void Set(string[] c, int col, string v) { if (col >= 0 && col < c.Length) c[col] = v ?? ""; }
        public static void Set(string[] c, int col, bool v)   { if (col >= 0 && col < c.Length) c[col] = Bit(v); }
        public static void Set(string[] c, int col, int v)    { if (col >= 0 && col < c.Length) c[col] = v.ToString(CultureInfo.InvariantCulture); }

        /// <summary>
        /// ⛔ A ROW THAT IS NOT AS WIDE AS THE HEADER IS WORSE THAN NO RECORDING AT ALL — Recorder A's
        /// rationale, composed verbatim (§3.4). Every column after a mismatch is labelled with its
        /// neighbour's name, and the file looks perfectly well-formed while telling you the throttle was
        /// 29 000. Structural: if the first row matches, they all do, so this runs ONCE per file.
        /// Returns the row's field count; the caller compares it against <see cref="Width"/>.
        /// </summary>
        public static int CountFields(string csvLine)
        {
            if (csvLine == null) return 0;
            int n = 1; bool quoted = false;
            for (int i = 0; i < csvLine.Length; i++)
            {
                char ch = csvLine[i];
                if (ch == '"') quoted = !quoted;
                else if (ch == ',' && !quoted) n++;
            }
            return n;
        }
    }
}
