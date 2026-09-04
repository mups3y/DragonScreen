// DragonScreen — BlackBox / DERIVED COLUMN INDICES  (register BB1)
// ============================================================================================
// PURE. ⛔ GENERATED SHAPE, HAND-MAINTAINED CONTENT — one `static readonly int` per column, each
// looked up FROM `BlackBoxSchema.Columns` BY NAME at type-init.
//
// This is Recorder B's single best idea and §3.4 says COMPOSE IT VERBATIM: because every index is
// derived from the ordered table rather than written as a literal, RE-ORDERING THE SCHEMA JUST WORKS
// and positional drift is impossible. Recorder A's alternative — block-prefixed names whose position
// the analyser inferred (`pre = name[:2]`) — is filed as BREAK for exactly the failure this prevents.
//
// A name that is not in the table resolves to -1, and every `BlackBoxSchema.Set` overload ignores -1.
// So DELETING a column does not crash its writers: they stop writing, the column stops existing, and
// `BlackBoxCoverage` has nothing to report because there is no declaration left to be unmet. That is
// the intended failure mode — a removed column must never be able to shift its neighbours.
//
// Keep this list in the SAME ORDER as the table it indexes. Nothing enforces that (the lookup is by
// name), but a reader comparing the two files should not have to search.
// ============================================================================================

namespace DragonScreen.BlackBox
{
    public static class BlackBoxCols
    {
        public static readonly int MissionId = BlackBoxSchema.Index("mission_id");
        public static readonly int Seq = BlackBoxSchema.Index("seq");
        public static readonly int Ut = BlackBoxSchema.Index("ut");
        public static readonly int MetS = BlackBoxSchema.Index("met_s");
        public static readonly int WallS = BlackBoxSchema.Index("wall_s");
        public static readonly int WarpRate = BlackBoxSchema.Index("warp_rate");
        public static readonly int WarpRails = BlackBoxSchema.Index("warp_rails");
        public static readonly int Vessel = BlackBoxSchema.Index("vessel");
        public static readonly int Focus = BlackBoxSchema.Index("focus");
        public static readonly int RecBuildUs = BlackBoxSchema.Index("rec_build_us");
        public static readonly int AltM = BlackBoxSchema.Index("alt_m");
        public static readonly int AltRadarM = BlackBoxSchema.Index("alt_radar_m");
        public static readonly int SpeedMps = BlackBoxSchema.Index("speed_mps");
        public static readonly int SrfSpeedMps = BlackBoxSchema.Index("srf_speed_mps");
        public static readonly int VspeedMps = BlackBoxSchema.Index("vspeed_mps");
        public static readonly int LatDeg = BlackBoxSchema.Index("lat_deg");
        public static readonly int LonDeg = BlackBoxSchema.Index("lon_deg");
        public static readonly int DownrangeM = BlackBoxSchema.Index("downrange_m");
        public static readonly int AtmDensity = BlackBoxSchema.Index("atm_density");
        public static readonly int MassKg = BlackBoxSchema.Index("mass_kg");
        public static readonly int MoiPitch = BlackBoxSchema.Index("moi_pitch");
        public static readonly int MoiRoll = BlackBoxSchema.Index("moi_roll");
        public static readonly int MoiYaw = BlackBoxSchema.Index("moi_yaw");
        public static readonly int ApKm = BlackBoxSchema.Index("ap_km");
        public static readonly int PeKm = BlackBoxSchema.Index("pe_km");
        public static readonly int IncDeg = BlackBoxSchema.Index("inc_deg");
        public static readonly int RaanDeg = BlackBoxSchema.Index("raan_deg");
        public static readonly int Ecc = BlackBoxSchema.Index("ecc");
        public static readonly int SmaM = BlackBoxSchema.Index("sma_m");
        public static readonly int ArgpDeg = BlackBoxSchema.Index("argp_deg");
        public static readonly int TaDeg = BlackBoxSchema.Index("ta_deg");
        public static readonly int PeriodS = BlackBoxSchema.Index("period_s");
        public static readonly int TApS = BlackBoxSchema.Index("t_ap_s");
        public static readonly int TPeS = BlackBoxSchema.Index("t_pe_s");
        public static readonly int Mach = BlackBoxSchema.Index("mach");
        public static readonly int QPa = BlackBoxSchema.Index("q_pa");
        public static readonly int AccelG = BlackBoxSchema.Index("accel_g");
        public static readonly int AccelAxialG = BlackBoxSchema.Index("accel_axial_g");
        public static readonly int PitchDeg = BlackBoxSchema.Index("pitch_deg");
        public static readonly int HeadingDeg = BlackBoxSchema.Index("heading_deg");
        public static readonly int RollDeg = BlackBoxSchema.Index("roll_deg");
        public static readonly int AoaDeg = BlackBoxSchema.Index("aoa_deg");
        public static readonly int AosDeg = BlackBoxSchema.Index("aos_deg");
        public static readonly int RatePitchDps = BlackBoxSchema.Index("rate_pitch_dps");
        public static readonly int RateRollDps = BlackBoxSchema.Index("rate_roll_dps");
        public static readonly int RateYawDps = BlackBoxSchema.Index("rate_yaw_dps");
        public static readonly int AccelGPeak = BlackBoxSchema.Index("accel_g_peak");
        public static readonly int QPaPeak = BlackBoxSchema.Index("q_pa_peak");
        public static readonly int RatePeakDps = BlackBoxSchema.Index("rate_peak_dps");
        public static readonly int Body = BlackBoxSchema.Index("body");
        public static readonly int Throttle = BlackBoxSchema.Index("throttle");
        public static readonly int ThrustN = BlackBoxSchema.Index("thrust_n");
        public static readonly int EngIgnited = BlackBoxSchema.Index("eng_ignited");
        public static readonly int EngFlameout = BlackBoxSchema.Index("eng_flameout");
        public static readonly int Stage = BlackBoxSchema.Index("stage");
        public static readonly int RcsOn = BlackBoxSchema.Index("rcs_on");
        public static readonly int EcFrac = BlackBoxSchema.Index("ec_frac");
        public static readonly int MmhFrac = BlackBoxSchema.Index("mmh_frac");
        public static readonly int NtoFrac = BlackBoxSchema.Index("nto_frac");
        public static readonly int PropFrac = BlackBoxSchema.Index("prop_frac");
        public static readonly int KerAvail = BlackBoxSchema.Index("ker_avail");
        public static readonly int KerStageDv = BlackBoxSchema.Index("ker_stage_dv");
        public static readonly int KerTotalDv = BlackBoxSchema.Index("ker_total_dv");
        public static readonly int KerTwr = BlackBoxSchema.Index("ker_twr");
        public static readonly int KerIsp = BlackBoxSchema.Index("ker_isp");
        public static readonly int KerBurnS = BlackBoxSchema.Index("ker_burn_s");
        public static readonly int KerStageMassKg = BlackBoxSchema.Index("ker_stage_mass_kg");
        public static readonly int KerThrustAvailN = BlackBoxSchema.Index("ker_thrust_avail_n");
        public static readonly int DvPlanned = BlackBoxSchema.Index("dv_planned");
        public static readonly int DvDelivered = BlackBoxSchema.Index("dv_delivered");
        public static readonly int DvResidual = BlackBoxSchema.Index("dv_residual");
        public static readonly int DvGravLoss = BlackBoxSchema.Index("dv_grav_loss");
        public static readonly int DvDragLoss = BlackBoxSchema.Index("dv_drag_loss");
        public static readonly int DvSteerLoss = BlackBoxSchema.Index("dv_steer_loss");
        public static readonly int AppPitch = BlackBoxSchema.Index("app_pitch");
        public static readonly int AppYaw = BlackBoxSchema.Index("app_yaw");
        public static readonly int AppRoll = BlackBoxSchema.Index("app_roll");
        public static readonly int AppTx = BlackBoxSchema.Index("app_tx");
        public static readonly int AppTy = BlackBoxSchema.Index("app_ty");
        public static readonly int AppTz = BlackBoxSchema.Index("app_tz");
        public static readonly int AttRateMeas = BlackBoxSchema.Index("att_rate_meas");
        public static readonly int CtrlTqPitch = BlackBoxSchema.Index("ctrl_tq_pitch");
        public static readonly int CtrlTqYaw = BlackBoxSchema.Index("ctrl_tq_yaw");
        public static readonly int CtrlTqRoll = BlackBoxSchema.Index("ctrl_tq_roll");
        public static readonly int RcsThrustN = BlackBoxSchema.Index("rcs_thrust_n");
        public static readonly int AccIntS = BlackBoxSchema.Index("acc_int_s");
        public static readonly int AccAttS = BlackBoxSchema.Index("acc_att_s");
        public static readonly int AccTransS = BlackBoxSchema.Index("acc_trans_s");
        public static readonly int AccBothS = BlackBoxSchema.Index("acc_both_s");
        public static readonly int AccNoneS = BlackBoxSchema.Index("acc_none_s");
        public static readonly int AccAppAtt = BlackBoxSchema.Index("acc_app_att");
        public static readonly int AccAppTrans = BlackBoxSchema.Index("acc_app_trans");
        public static readonly int ActSatS = BlackBoxSchema.Index("act_sat_s");
        public static readonly int ActPitch = BlackBoxSchema.Index("act_pitch");
        public static readonly int ActYaw = BlackBoxSchema.Index("act_yaw");
        public static readonly int ActRoll = BlackBoxSchema.Index("act_roll");
        public static readonly int AccReqAtt = BlackBoxSchema.Index("acc_req_att");
        public static readonly int AccReqTrans = BlackBoxSchema.Index("acc_req_trans");
        public static readonly int AttErrDeg = BlackBoxSchema.Index("att_err_deg");
        public static readonly int AttRateCmd = BlackBoxSchema.Index("att_rate_cmd");
        public static readonly int BoostDbPitch = BlackBoxSchema.Index("boost_db_pitch");
        public static readonly int BoostDbYaw = BlackBoxSchema.Index("boost_db_yaw");
        public static readonly int BoostDbRoll = BlackBoxSchema.Index("boost_db_roll");
        public static readonly int BoostDbDeg = BlackBoxSchema.Index("boost_db_deg");
        public static readonly int BoostSteerPitch = BlackBoxSchema.Index("boost_steer_pitch");
        public static readonly int BoostSteerYaw = BlackBoxSchema.Index("boost_steer_yaw");
        public static readonly int BoostSteerRoll = BlackBoxSchema.Index("boost_steer_roll");
        public static readonly int BoostThrottle = BlackBoxSchema.Index("boost_throttle");
        public static readonly int BoostPhase = BlackBoxSchema.Index("boost_phase");
        public static readonly int BoostUncommanded = BlackBoxSchema.Index("boost_uncommanded");
        public static readonly int BoostBlock = BlackBoxSchema.Index("boost_block");   // BB9
        public static readonly int BoostCmdNotIgnited = BlackBoxSchema.Index("boost_cmd_not_ignited");   // [[OCT11]]
        public static readonly int GncEngaged = BlackBoxSchema.Index("gnc_engaged");
        public static readonly int ModeIndex = BlackBoxSchema.Index("mode_index");
        public static readonly int GncModule = BlackBoxSchema.Index("gnc_module");
        public static readonly int GncStatus = BlackBoxSchema.Index("gnc_status");
        public static readonly int PvgVgoMps = BlackBoxSchema.Index("pvg_vgo_mps");
        public static readonly int PvgTgoS = BlackBoxSchema.Index("pvg_tgo_s");
        public static readonly int CmdPitchDeg = BlackBoxSchema.Index("cmd_pitch_deg");
        public static readonly int CmdHeadingDeg = BlackBoxSchema.Index("cmd_heading_deg");
        public static readonly int CmdThrottle = BlackBoxSchema.Index("cmd_throttle");
        public static readonly int TgtApKm = BlackBoxSchema.Index("tgt_ap_km");
        public static readonly int TgtPeKm = BlackBoxSchema.Index("tgt_pe_km");
        public static readonly int TgtIncDeg = BlackBoxSchema.Index("tgt_inc_deg");
        public static readonly int NodeDvLeft = BlackBoxSchema.Index("node_dv_left");
        public static readonly int NodePointErr = BlackBoxSchema.Index("node_point_err");
        public static readonly int ReplanCount = BlackBoxSchema.Index("replan_count");
        public static readonly int DeviationM = BlackBoxSchema.Index("deviation_m");
        public static readonly int DeviationMps = BlackBoxSchema.Index("deviation_mps");
        public static readonly int MissionPhase = BlackBoxSchema.Index("mission_phase");
        public static readonly int PhaseClassified = BlackBoxSchema.Index("phase_classified");
        public static readonly int GateId = BlackBoxSchema.Index("gate_id");
        public static readonly int GatePhase = BlackBoxSchema.Index("gate_phase");
        public static readonly int CrewAction = BlackBoxSchema.Index("crew_action");
        public static readonly int GateSatisfiedMask = BlackBoxSchema.Index("gate_satisfied_mask");
        public static readonly int IsReturn = BlackBoxSchema.Index("is_return");
        public static readonly int StepAckMask = BlackBoxSchema.Index("step_ack_mask");
        public static readonly int StepId = BlackBoxSchema.Index("step_id");
        public static readonly int StepState = BlackBoxSchema.Index("step_state");
        public static readonly int PageL = BlackBoxSchema.Index("page_l");
        public static readonly int PageC = BlackBoxSchema.Index("page_c");
        public static readonly int PageR = BlackBoxSchema.Index("page_r");
        public static readonly int CamView = BlackBoxSchema.Index("cam_view");
        public static readonly int BrightnessL = BlackBoxSchema.Index("brightness_l");   // S86
        public static readonly int BrightnessC = BlackBoxSchema.Index("brightness_c");   // S86
        public static readonly int BrightnessR = BlackBoxSchema.Index("brightness_r");   // S86
        public static readonly int CoverCamL = BlackBoxSchema.Index("cover_cam_l");       // S94 (S86-Q1)
        public static readonly int CoverCamC = BlackBoxSchema.Index("cover_cam_c");       // S94 (S86-Q1)
        public static readonly int CoverCamR = BlackBoxSchema.Index("cover_cam_r");       // S94 (S86-Q1)
        public static readonly int CoverPhaseL = BlackBoxSchema.Index("cover_phase_l");   // S94 (S86-Q1)
        public static readonly int CoverPhaseC = BlackBoxSchema.Index("cover_phase_c");   // S94 (S86-Q1)
        public static readonly int CoverPhaseR = BlackBoxSchema.Index("cover_phase_r");   // S94 (S86-Q1)
        public static readonly int Bus1On = BlackBoxSchema.Index("bus1_on");
        public static readonly int Bus2On = BlackBoxSchema.Index("bus2_on");
        public static readonly int StrA1 = BlackBoxSchema.Index("str_a1");
        public static readonly int StrB1 = BlackBoxSchema.Index("str_b1");
        public static readonly int StrC1 = BlackBoxSchema.Index("str_c1");
        public static readonly int StrA2 = BlackBoxSchema.Index("str_a2");
        public static readonly int StrB2 = BlackBoxSchema.Index("str_b2");
        public static readonly int StrC2 = BlackBoxSchema.Index("str_c2");
        public static readonly int FireIntensity = BlackBoxSchema.Index("fire_intensity");
        public static readonly int Suppressant = BlackBoxSchema.Index("suppressant");
        public static readonly int LeakRate = BlackBoxSchema.Index("leak_rate");
        public static readonly int Isolating = BlackBoxSchema.Index("isolating");
        public static readonly int O2Store = BlackBoxSchema.Index("o2_store");
        public static readonly int N2Store = BlackBoxSchema.Index("n2_store");
        public static readonly int CanisterUsed = BlackBoxSchema.Index("canister_used");
        public static readonly int CabinPsia = BlackBoxSchema.Index("cabin_psia");
        public static readonly int Ppo2Psia = BlackBoxSchema.Index("ppo2_psia");
        public static readonly int Co2Mmhg = BlackBoxSchema.Index("co2_mmhg");
        public static readonly int CabinTempC = BlackBoxSchema.Index("cabin_temp_c");
        public static readonly int LoopAC = BlackBoxSchema.Index("loop_a_c");
        public static readonly int LoopBC = BlackBoxSchema.Index("loop_b_c");
        public static readonly int LsPresent = BlackBoxSchema.Index("ls_present");
        public static readonly int LsO2Days = BlackBoxSchema.Index("ls_o2_days");
        public static readonly int LsWaterDays = BlackBoxSchema.Index("ls_water_days");
        public static readonly int LsFoodDays = BlackBoxSchema.Index("ls_food_days");
        public static readonly int LsLimitingDays = BlackBoxSchema.Index("ls_limiting_days");
        public static readonly int SkinTempFrac = BlackBoxSchema.Index("skin_temp_frac");
        public static readonly int HullTempC = BlackBoxSchema.Index("hull_temp_c");
        public static readonly int SevSystem = BlackBoxSchema.Index("sev_system");
        public static readonly int SevVehicle = BlackBoxSchema.Index("sev_vehicle");
        public static readonly int SevLs = BlackBoxSchema.Index("sev_ls");
        public static readonly int SevThermal = BlackBoxSchema.Index("sev_thermal");
        public static readonly int AlarmMask = BlackBoxSchema.Index("alarm_mask");
        public static readonly int FdirFault = BlackBoxSchema.Index("fdir_fault");
        public static readonly int FdirRecovery = BlackBoxSchema.Index("fdir_recovery");
        public static readonly int Aborting = BlackBoxSchema.Index("aborting");
        public static readonly int AbortMode = BlackBoxSchema.Index("abort_mode");
        public static readonly int CommLinked = BlackBoxSchema.Index("comm_linked");
        public static readonly int CommSignal = BlackBoxSchema.Index("comm_signal");
        public static readonly int RangeM = BlackBoxSchema.Index("range_m");
        public static readonly int ClosingMps = BlackBoxSchema.Index("closing_mps");
        public static readonly int AlignDeg = BlackBoxSchema.Index("align_deg");
        public static readonly int RollErrDeg = BlackBoxSchema.Index("roll_err_deg");
        public static readonly int PitchErrDeg = BlackBoxSchema.Index("pitch_err_deg");
        public static readonly int YawErrDeg = BlackBoxSchema.Index("yaw_err_deg");
    }
}
