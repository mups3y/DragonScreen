// DragonScreen — FlightRecorder  (autopilot rebuild L7: instrument + record EVERYTHING)
// ============================================================================================
// The standing rule (memory: INSTRUMENT + RECORD EVERYTHING): every controller's internals — decisions,
// gates, targets, triggers, planned + delivered Δv, FDIR faults/recovery, and the self-cal estimates —
// go into a per-flight CSV, written the SAME pass the controller is built, and PREFER MORE COLUMNS. This
// is the pure, headless-tested core: the ordered column SCHEMA (single source of truth; indices are
// looked up from it so they can never drift), invariant-culture formatting (⛔ never locale-formatted —
// a European locale would write "1,5" and shred the CSV), CSV escaping, and a `Put*` filler per
// controller that takes the controller's actual command struct so recording is not ad-hoc. The glue (a
// KSP behaviour) samples each tick: NewRow() → the Put* fillers for whatever is active → append Row() to
// the flight's CSV file. File I/O + sampling live in the glue; this file owns the schema + formatting.
// ============================================================================================
using System;
using System.Text;
using System.Globalization;

namespace DragonScreen
{
    public static class FlightRecorder
    {
        // ---- THE SCHEMA — the single ordered source of truth. Add columns freely (prefer more). ----
        public static readonly string[] Schema =
        {
            // time + mode/gate
            "met_s", "mission_phase", "mode_index", "mode_holding", "mode_flying",
            "gate_id", "gate_phase", "crew_action",
            // nav / state  (BOTH surface + orbital speed — MechJeb records both; the screenshots show surface,
            // the guidance works in orbital. accel_g = felt g (thrust/cutout, always on). thrust_n = measured.)
            "alt_m", "speed_mps", "srf_speed_mps", "vspeed_mps", "q_pa", "mach", "downrange_m", "mass_kg",
            "accel_g", "thrust_n",
            // control (throttle + the RCS translation command drive every burn — always recorded)
            "att_err_deg", "rate_cmd_rads", "throttle", "torque_cmd", "rcs_on",
            "trans_x", "trans_y", "trans_z",
            // attitude loop (direct gimbal/RCS pointing — the max-Q fix; AttitudePilot internals)
            "att_point_deg", "att_rate_cmd", "att_rate_meas", "act_pitch", "act_yaw", "act_roll",
            "ctrl_tq_pitch", "ctrl_tq_yaw",
            // measured body angular RATES (deg/s) — the raw control-rate signal for the tuning database
            "rate_pitch_dps", "rate_roll_dps", "rate_yaw_dps",
            // CONTROL AUTHORITY: available torque per axis (ctrl_tq_roll completes pitch/yaw), MOI per axis, and
            // the RCS thrust in use — so the DB shows angular-accel authority (torque/MOI) + saturation per phase.
            "ctrl_tq_roll", "moi_pitch", "moi_roll", "moi_yaw", "rcs_thrust_n",
            // orbit state — for plane / guidance tuning (apoapsis/periapsis, inclination, RAAN)
            "ap_km", "pe_km", "inc_deg", "raan_deg",
            // ignition gates (ullage settle + clamp release)
            "ullage_stab", "clamp_frac", "clamp_held",
            // ascent (UPFG)
            "upfg_tgo_s", "upfg_vgo_mps", "pitch_deg", "azimuth_deg", "ascent_phase",
            // booster
            "boost_phase", "boost_aoa_deg", "engine_mode", "ignite_alt_m", "descent_speed_mps",
            // rendezvous
            "rv_phase", "lvlh_rx", "lvlh_ry", "lvlh_rz", "rv_range_m", "rv_burn_dv",
            // docking
            "dock_phase", "dock_range_m", "close_speed_mps", "dock_hold",
            // return
            "dep_phase", "deorbit_phase", "entry_phase", "bank_deg", "com_descent_mode",
            "chute_phase", "drogue", "main",
            // Δv accounting
            "dv_planned_mps", "dv_delivered_mps", "dv_residual_mps",
            // FDIR
            "fdir_fault", "fdir_recovery", "fdir_abort", "abort_mode",
            // self-cal
            "cal_thrust_n", "cal_beta", "cal_invI", "cal_ld", "steer_sign",
        };

        static int Index(string name)
        {
            for (int i = 0; i < Schema.Length; i++) if (Schema[i] == name) return i;
            return -1;
        }

        // ---- drift-proof indices: looked up FROM the schema, so re-ordering the schema just works ----
        public static readonly int MetS = Index("met_s"), MissionPhase = Index("mission_phase"),
            ModeIndex = Index("mode_index"), ModeHolding = Index("mode_holding"), ModeFlying = Index("mode_flying"),
            GateIdC = Index("gate_id"), GatePhaseC = Index("gate_phase"), CrewAction = Index("crew_action"),
            AltM = Index("alt_m"), SpeedMps = Index("speed_mps"), SrfSpeed = Index("srf_speed_mps"),
            VspeedMps = Index("vspeed_mps"),
            QPa = Index("q_pa"), Mach = Index("mach"), DownrangeM = Index("downrange_m"), MassKg = Index("mass_kg"),
            AccelG = Index("accel_g"), ThrustN = Index("thrust_n"),
            AttErrDeg = Index("att_err_deg"), RateCmd = Index("rate_cmd_rads"), Throttle = Index("throttle"),
            TorqueCmd = Index("torque_cmd"), RcsOn = Index("rcs_on"),
            TransX = Index("trans_x"), TransY = Index("trans_y"), TransZ = Index("trans_z"),
            AttPointDeg = Index("att_point_deg"), AttRateCmd = Index("att_rate_cmd"), AttRateMeas = Index("att_rate_meas"),
            ActPitchC = Index("act_pitch"), ActYawC = Index("act_yaw"), ActRollC = Index("act_roll"),
            CtrlTqPitch = Index("ctrl_tq_pitch"), CtrlTqYaw = Index("ctrl_tq_yaw"),
            RatePitchDps = Index("rate_pitch_dps"), RateRollDps = Index("rate_roll_dps"), RateYawDps = Index("rate_yaw_dps"),
            CtrlTqRoll = Index("ctrl_tq_roll"), MoiPitch = Index("moi_pitch"), MoiRoll = Index("moi_roll"),
            MoiYaw = Index("moi_yaw"), RcsThrustN = Index("rcs_thrust_n"),
            ApKm = Index("ap_km"), PeKm = Index("pe_km"), IncDeg = Index("inc_deg"), RaanDeg = Index("raan_deg"),
            UllageStab = Index("ullage_stab"), ClampFrac = Index("clamp_frac"), ClampHeldC = Index("clamp_held"),
            UpfgTgo = Index("upfg_tgo_s"), UpfgVgo = Index("upfg_vgo_mps"), PitchDeg = Index("pitch_deg"),
            AzimuthDeg = Index("azimuth_deg"), AscentPhase = Index("ascent_phase"),
            BoostPhase = Index("boost_phase"), BoostAoaDeg = Index("boost_aoa_deg"), EngineMode = Index("engine_mode"),
            IgniteAltM = Index("ignite_alt_m"), DescentSpeedMps = Index("descent_speed_mps"),
            RvPhase = Index("rv_phase"), LvlhRx = Index("lvlh_rx"), LvlhRy = Index("lvlh_ry"), LvlhRz = Index("lvlh_rz"),
            RvRangeM = Index("rv_range_m"), RvBurnDv = Index("rv_burn_dv"),
            DockPhaseC = Index("dock_phase"), DockRangeM = Index("dock_range_m"), CloseSpeedMps = Index("close_speed_mps"),
            DockHold = Index("dock_hold"),
            DepPhaseC = Index("dep_phase"), DeorbitPhaseC = Index("deorbit_phase"), EntryPhaseC = Index("entry_phase"),
            BankDeg = Index("bank_deg"), ComDescentMode = Index("com_descent_mode"), ChutePhaseC = Index("chute_phase"),
            Drogue = Index("drogue"), Main = Index("main"),
            DvPlanned = Index("dv_planned_mps"), DvDelivered = Index("dv_delivered_mps"), DvResidual = Index("dv_residual_mps"),
            FdirFault = Index("fdir_fault"), FdirRecovery = Index("fdir_recovery"), FdirAbort = Index("fdir_abort"),
            AbortModeC = Index("abort_mode"),
            CalThrustN = Index("cal_thrust_n"), CalBeta = Index("cal_beta"), CalInvI = Index("cal_invI"),
            CalLd = Index("cal_ld"), SteerSignC = Index("steer_sign");

        // ---- formatting ----
        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            return v.ToString("0.######", CultureInfo.InvariantCulture);   // INVARIANT — never a locale comma
        }
        static string Bit(bool b) { return b ? "1" : "0"; }

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public static string[] NewRow()
        {
            string[] cells = new string[Schema.Length];
            for (int i = 0; i < cells.Length; i++) cells[i] = "";   // unset → blank cell
            return cells;
        }

        public static string Header()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Schema.Length; i++) { if (i > 0) sb.Append(','); sb.Append(Escape(Schema[i])); }
            return sb.ToString();
        }

        public static string Row(string[] cells)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Schema.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Escape(i < cells.Length ? cells[i] : ""));
            }
            return sb.ToString();
        }

        static void Set(string[] c, int col, double v) { if (col >= 0 && col < c.Length) c[col] = Num(v); }
        static void Set(string[] c, int col, string v) { if (col >= 0 && col < c.Length) c[col] = v == null ? "" : v; }
        static void Set(string[] c, int col, bool v) { if (col >= 0 && col < c.Length) c[col] = Bit(v); }

        // ============================ the per-controller fillers ============================
        // Each takes the controller's ACTUAL command/state so recording is never ad-hoc; the glue calls
        // the ones relevant to the active phase. Unset columns stay blank.

        public static void PutTime(string[] c, double metS) { Set(c, MetS, metS); }

        public static void PutMode(string[] c, ModeStep m, MissionPhase phase)
        {
            Set(c, MissionPhase, Mission.Name(phase));
            Set(c, ModeIndex, m.Index);
            Set(c, ModeHolding, m.Holding);
            Set(c, ModeFlying, m.Flying);
        }

        // ⛔ THE ALWAYS-ON SNAPSHOT (MechJeb FlightRecorder principle): the universally-available state is
        // recorded EVERY sample from live vessel data, INDEPENDENT of which flying controller is active — so
        // an abort, a coast, or an engine cutout is never lost just because no controller's Fill was set. This
        // is the fix for the recorder freezing on the ascent filler through the whole abort + chute descent.
        // Writes: mission_phase + mode, surface speed, AoA, felt g, measured thrust, RCS master, abort state.
        public static void PutBase(string[] c, MissionPhase phase, ModeStep mode, double srfSpeedMps,
                                   double aoaDeg, double accelG, double thrustN, bool rcsOn,
                                   bool aborting, AbortMode abortMode)
        {
            PutMode(c, mode, phase);
            Set(c, SrfSpeed, srfSpeedMps); Set(c, AttErrDeg, aoaDeg); Set(c, AccelG, accelG);
            Set(c, ThrustN, thrustN); Set(c, RcsOn, rcsOn);
            Set(c, FdirAbort, aborting); Set(c, AbortModeC, abortMode.ToString());
        }

        // Chute state during an ABORT descent (the abort path owns the chutes, not ReturnControl).
        public static void PutAbortChutes(string[] c, ChutePhase chute)
        {
            Set(c, ChutePhaseC, chute.ToString());
            Set(c, Drogue, chute >= ChutePhase.Drogue);
            Set(c, Main, chute >= ChutePhase.Main);
        }

        public static void PutGate(string[] c, GateId id, GatePhase phase, bool crewActionNeeded)
        {
            Set(c, GateIdC, id.ToString());
            Set(c, GatePhaseC, phase.ToString());
            Set(c, CrewAction, crewActionNeeded);
        }

        public static void PutNav(string[] c, double altM, double speed, double vspeed, double qPa,
                                  double mach, double downrangeM, double massKg)
        {
            Set(c, AltM, altM); Set(c, SpeedMps, speed); Set(c, VspeedMps, vspeed);
            Set(c, QPa, qPa); Set(c, Mach, mach); Set(c, DownrangeM, downrangeM); Set(c, MassKg, massKg);
        }

        public static void PutControl(string[] c, double attErrDeg, double rateCmd, double throttle,
                                      double torqueCmd, bool rcsOn)
        {
            Set(c, AttErrDeg, attErrDeg); Set(c, RateCmd, rateCmd); Set(c, Throttle, throttle);
            Set(c, TorqueCmd, torqueCmd); Set(c, RcsOn, rcsOn);
        }

        // ⛔ THE ALWAYS-ON COMMAND SNAPSHOT — the full applied control every phase, from the LIVE command state
        // (FlightDriver + AttitudePilot), so no phase is ever blind. This is what was missing: the abort (and any
        // coast) recorded nothing about how the vehicle was being flown, so a stranded, mis-pointed capsule looked
        // like empty cells. Records the throttle, the RCS translation demand (every deorbit/rendezvous/docking burn
        // is a translation), and the attitude-loop actuation + pointing error + rates + authority. Runs in the base
        // sample for EVERY row; a controller Fill may still refine its own columns on top.
        public static void PutCommand(string[] c, double throttle, double transX, double transY, double transZ,
                                      double pointErrDeg, double rateCmdRads, double rateMeasRads,
                                      double actPitch, double actYaw, double actRoll,
                                      double ctrlTqPitchNm, double ctrlTqYawNm)
        {
            Set(c, Throttle, throttle);
            Set(c, TransX, transX); Set(c, TransY, transY); Set(c, TransZ, transZ);
            Set(c, RateCmd, rateCmdRads);
            Set(c, AttPointDeg, pointErrDeg); Set(c, AttRateCmd, rateCmdRads); Set(c, AttRateMeas, rateMeasRads);
            Set(c, ActPitchC, actPitch); Set(c, ActYawC, actYaw); Set(c, ActRollC, actRoll);
            Set(c, CtrlTqPitch, ctrlTqPitchNm); Set(c, CtrlTqYaw, ctrlTqYawNm);
        }

        // The direct gimbal/RCS loop internals (AttitudePilot) — pointing error, commanded vs measured rate,
        // per-axis actuation, and the live control-torque authority. So the max-Q attitude is judged from the
        // CSV, not by eye ([[instrument-everything]]).
        public static void PutAttitude(string[] c, double pointErrDeg, double rateCmdRads, double rateMeasRads,
                                       double actPitch, double actYaw, double actRoll,
                                       double ctrlTqPitchNm, double ctrlTqYawNm)
        {
            Set(c, AttPointDeg, pointErrDeg); Set(c, AttRateCmd, rateCmdRads); Set(c, AttRateMeas, rateMeasRads);
            Set(c, ActPitchC, actPitch); Set(c, ActYawC, actYaw); Set(c, ActRollC, actRoll);
            Set(c, CtrlTqPitch, ctrlTqPitchNm); Set(c, CtrlTqYaw, ctrlTqYawNm);
        }

        // Measured body angular rates (deg/s) — the raw pitch/roll/yaw rate signal for the tuning database.
        public static void PutRates(string[] c, double pitchDps, double rollDps, double yawDps)
        {
            Set(c, RatePitchDps, pitchDps); Set(c, RateRollDps, rollDps); Set(c, RateYawDps, yawDps);
        }

        // Control AUTHORITY + orbit state for the tuning DB: available roll torque (pitch/yaw already in the
        // command snapshot), MOI per axis (angular-accel authority = torque/MOI), the RCS thrust in use, and the
        // orbit shape/plane (apoapsis/periapsis km, inclination, RAAN deg).
        public static void PutAuthority(string[] c, double ctrlTqRollNm, double moiPitch, double moiRoll,
                                        double moiYaw, double rcsThrustN,
                                        double apKm, double peKm, double incDeg, double raanDeg)
        {
            Set(c, CtrlTqRoll, ctrlTqRollNm);
            Set(c, MoiPitch, moiPitch); Set(c, MoiRoll, moiRoll); Set(c, MoiYaw, moiYaw);
            Set(c, RcsThrustN, rcsThrustN);
            Set(c, ApKm, apKm); Set(c, PeKm, peKm); Set(c, IncDeg, incDeg); Set(c, RaanDeg, raanDeg);
        }

        // The ignition gates: RealFuels ullage stability (during the S2 settle) + the pad clamp-hold thrust
        // fraction and whether the hold-downs are still on — so the CSV shows the settle and the release.
        public static void PutIgnition(string[] c, double ullageStability, double clampThrustFrac, bool clampHeld)
        {
            Set(c, UllageStab, ullageStability); Set(c, ClampFrac, clampThrustFrac); Set(c, ClampHeldC, clampHeld);
        }

        public static void PutAscent(string[] c, double tgoS, double vgoMps, double pitchDeg,
                                     double azimuthDeg, string ascentPhase)
        {
            Set(c, UpfgTgo, tgoS); Set(c, UpfgVgo, vgoMps); Set(c, PitchDeg, pitchDeg);
            Set(c, AzimuthDeg, azimuthDeg); Set(c, AscentPhase, ascentPhase);
        }

        public static void PutBooster(string[] c, BoosterCommand b, double igniteAltM, double descentSpeedMps)
        {
            Set(c, BoostPhase, b.Phase.ToString()); Set(c, BoostAoaDeg, b.AoaDeg);
            Set(c, EngineMode, b.EngineMode); Set(c, Throttle, b.Throttle);
            Set(c, IgniteAltM, igniteAltM); Set(c, DescentSpeedMps, descentSpeedMps);
        }

        public static void PutRendezvous(string[] c, RendezvousCommand r, LvlhState rel)
        {
            Set(c, RvPhase, r.Phase.ToString());
            Set(c, LvlhRx, rel.Rx); Set(c, LvlhRy, rel.Ry); Set(c, LvlhRz, rel.Rz);
            Set(c, RvRangeM, rel.RangeM); Set(c, RvBurnDv, r.BurnDvMps);
        }

        public static void PutDocking(string[] c, DockCommand d, double rangeM, double closeSpeedMps)
        {
            Set(c, DockPhaseC, d.Phase.ToString()); Set(c, DockRangeM, rangeM);
            Set(c, CloseSpeedMps, closeSpeedMps); Set(c, DockHold, d.Hold);
        }

        public static void PutReturn(string[] c, DepPhase dep, DeorbitPhase deo, EntryPhase ent,
                                     double bankRad, bool comDescentMode, ChutePhase chute, bool drogue, bool main)
        {
            Set(c, DepPhaseC, dep.ToString()); Set(c, DeorbitPhaseC, deo.ToString());
            Set(c, EntryPhaseC, ent.ToString()); Set(c, BankDeg, bankRad * 180.0 / Math.PI);
            Set(c, ComDescentMode, comDescentMode); Set(c, ChutePhaseC, chute.ToString());
            Set(c, Drogue, drogue); Set(c, Main, main);
        }

        public static void PutDv(string[] c, double plannedMps, double deliveredMps)
        {
            Set(c, DvPlanned, plannedMps); Set(c, DvDelivered, deliveredMps);
            Set(c, DvResidual, plannedMps - deliveredMps);
        }

        public static void PutFdir(string[] c, FdirReport r, AbortMode abortMode)
        {
            Set(c, FdirFault, r.Fault.ToString()); Set(c, FdirRecovery, r.Response.ToString());
            Set(c, FdirAbort, r.Abort); Set(c, AbortModeC, abortMode.ToString());
        }

        public static void PutSelfCal(string[] c, SelfCalState s)
        {
            Set(c, CalThrustN, s.Thrust.Theta);
            Set(c, CalBeta, s.InvBeta.Theta > 1e-9 ? 1.0 / s.InvBeta.Theta : double.NaN);
            Set(c, CalInvI, s.InvInertia.Theta);
            Set(c, CalLd, s.LoverD.Theta);
            Set(c, SteerSignC, SelfCal.SteerSign(s));
        }
    }
}
