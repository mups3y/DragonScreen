// DragonScreen - FlightRecorder
// ---- WHY THIS EXISTS ----
// ---- AND THE COMMAND IS THE HALF THAT MATTERS ----
// ---- WHAT IT IS DELIBERATELY NOT ----
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DragonScreen
{
    public static class FlightRecorder
    {
        private const string Tag = "[DragonScreen] ";

        private const double IntervalS = 0.2;

        private const int FlushEvery = 25;

        private static StreamWriter writer;
        private static StringBuilder pending = new StringBuilder();
        private static int pendingRows;
        private static double lastSample = -999.0;
        private static double startedUt;
        private static uint vesselId;
        private static Vessel primary;

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
                primary = v;
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
            widthChecked = false;
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
        private const string Header =
            "met,ut,focus,warp," +
            // ================= ASCENT VEHICLE =================
            "a_phase,a_note," +
            "a_altAsl,a_altRadar,a_lat,a_lon,a_vertSpeed,a_srfSpeed,a_orbSpeed,a_mach,a_qKpa," +
            "a_apoKm,a_periKm,a_incDeg,a_timeToApS," +
            "a_massT,a_availThrustKn,a_moiX,a_moiY,a_moiZ,a_torqueX,a_torqueY,a_torqueZ," +
            "a_lfFrac,a_oxFrac,a_monoFrac,a_ecFrac,a_maxSkinK,a_packed,a_enginesLit," +
            "a_aoaDeg,a_geeForce," +
            "a_phaseElapsedS,a_rangeToBoosterKm," +
            "a_cmdPitchDeg,a_cmdHeadingDeg,a_cmdThrottle,a_cmdStage,a_cmdSepS2,a_cmdUllage," +
            "a_cmdRcs,a_circDvMps,a_ullage," +
            "a_attErrDeg,a_phiPitchDeg,a_phiRollDeg,a_phiYawDeg," +
            "a_tgtOmegaPdps,a_tgtOmegaRdps,a_tgtOmegaYdps,a_omegaPdps,a_omegaRdps,a_omegaYdps," +
            "a_tgtTorqueP,a_tgtTorqueR,a_tgtTorqueY,a_actP,a_actR,a_actY," +
            "a_ctlPitch,a_ctlYaw,a_ctlRoll,a_ctlThrottle," +
            // ================= BOOSTER =================
            "b_phase," +
            "b_altAsl,b_altRadar,b_lat,b_lon,b_vertSpeed,b_srfSpeed,b_mach,b_qKpa," +
            "b_massT,b_availThrustKn,b_moiX,b_moiY,b_moiZ,b_torqueX,b_torqueY,b_torqueZ," +
            "b_lfFrac,b_oxFrac,b_maxSkinK,b_packed,b_enginesLit,b_octaMode,b_finsOut,b_gearOut," +
            "b_phaseElapsedS,b_rangeToPartnerKm,b_trueRadar,b_leanFrac,b_aoaDeg," +
            "b_cmdThrottle,b_ignitionAlt,b_engines,b_aim,b_legs," +
            "b_downrangeKm,b_predMissKm,b_missDownKm,b_missCrossKm,b_initMissKm," +
            "b_attErrDeg,b_phiPitchDeg,b_phiRollDeg,b_phiYawDeg," +
            "b_tgtOmegaPdps,b_tgtOmegaRdps,b_tgtOmegaYdps," +
            "b_omegaPdps,b_omegaRdps,b_omegaYdps," +
            "b_tgtTorqueP,b_tgtTorqueR,b_tgtTorqueY,b_actP,b_actR,b_actY," +
            "b_ctlPitch,b_ctlYaw,b_ctlRoll,b_ctlThrottle," +
            // ================= THE RETURN =================
            "r_stage,r_note,r_method,r_vertCmd,r_latCmd,r_aoaCmd," +
            "r_alongKm,r_crossM,r_missM,r_wantLongKm,r_trimErrM,r_belowProfile," +
            "r_liftMin,r_worstErrKm,r_dropped,r_drogues,r_mains,r_burnThr," +
            "r_phaseDown,r_deorbitMissKm,r_deorbitThr,r_nodePhase,r_nodeDvLeft," +
            "r_bcAscent,r_bcBooster," +
            // ================= THE MIDDLE OF THE MISSION =================
            "m_rndz,m_dock,m_undock,m_stationKm,m_closingMps,m_monoOurs,m_monoCap," +
            // ================= WHAT THE VEHICLE WAS ACTUALLY TOLD TO DO =================
            "x_owner,x_thrCmd,x_fore,x_transX,x_transY,x_rcsCmd,x_rcsOn," +
            "x_daPhase,x_daRangeM,x_daClosing,x_daDv,x_daWant,x_daAimErr,x_daThr," +
            "x_laPhase,x_laRangeM,x_laRadial,x_laAlong,x_laCross," +
            "x_dkStage,x_dkRangeM,x_dkClosing,x_dkAxisErr," +
            "x_dkDistF,x_dkDistS,x_dkDistT,x_dkVelF,x_dkVelS,x_dkVelT," +
            "x_leg,x_alongKm,x_lateral,x_udSepM,x_udOpening,x_refuelFrac," +
            // ---- ⛔ THE TRANSLATION RESPONSE. THE LAST UNINSTRUMENTED PART OF THE PATH. ----
            "x_ctlX,x_ctlY,x_ctlZ,x_actP,x_actR,x_actY," +
            // ================= DIAGNOSTICS (cross-phase) =================
            "d_recovFrac,d_recovUnits,d_autoStep,d_autoOn,d_returnFrac,d_rcsPct," +
            // ================= THE BURN (NodeExecutor) - EVERY orbital burn goes through this =================
            "nd_phase,nd_initDv,nd_remainDv,nd_deliveredDv,nd_pointErrDeg,nd_throttle,nd_rcs,nd_tIgnS," +
            // ================= CREW PROCEDURE GATES =================
            "g_gate,g_gatePhase,g_actionNeeded,g_returnArmed,g_releasedHold," +
            // ================= ABORT RESPONDER =================
            "ab_lesArmed,ab_aborting,ab_mode," +
            // ================= LIFE SUPPORT (TAC) - the Dragon's OWN side (DockedSide.Ours) =================
            "ls_present,ls_o2Frac,ls_co2Frac,ls_o2Days,ls_foodDays,ls_waterDays," +
            // ================= NAMED-BURN RENDEZVOUS (NamedRendezvousOps) =================
            "rv_leg,rv_rangeKm,rv_phaseDeg,rv_alongKm,rv_radialKm,rv_elevDeg,rv_leadDeg,rv_gapDeg," +
            "rv_coAltKm,rv_lastBurn,rv_lastDv,rv_arrRelMps,rv_passiveM,rv_warp," +
            // ================= BOOSTER LANDING DIAGNOSTICS (the lit-but-no-thrust failure) =================
            "bl_liveThrustKn,bl_coldGasFrac,bl_ullageOn,bl_igniteAttempts," +
            // ================= FDIR (Layer 3 - the autonomy layer, OBSERVE-ONLY) =================
            "fd_thrVerdict,fd_thrRaw,fd_thrResid,fd_thrKind,fd_thrRecovery," +
            // ================= DIRECT ACTUATOR AUTHORITY (autopilot-owned) =================
            "ctrl_gimbalPct," +
            // ================= RE-ENTRY HEAT + STEERING (per atmosphere layer) =================
            "he_steerTest,he_steerSeg,he_atmDensity,he_ablatorFrac,he_charFrac,he_shieldK,he_shieldFluxKw," +
            // ================= RENDEZVOUS FDIR (Layer-3 responder - it ACTS) =================
            "rvfd_stallS,rvfd_verdict,rvfd_replans,rvfd_action";

        public static void Tick()
        {
            if (writer == null) return;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            double ut = Planetarium.GetUniversalTime();
            if (ut - lastSample < IntervalS) return;
            lastSample = ut;

            try { WriteRow(v, ut); VerifyWidth(); }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "recorder row failed, stopping: " + e.Message);
                Stop("row failed");
            }
        }

        private static bool widthChecked;

        private static void VerifyWidth()
        {
            if (widthChecked || pending.Length == 0) return;
            widthChecked = true;

            string row = pending.ToString();
            int nl = row.IndexOf("\n", StringComparison.Ordinal);
            if (nl > 0) row = row.Substring(0, nl);

            int cols = Count(row, ',') + 1;
            int want = Count(Header, ',') + 1;
            if (cols == want)
            {
                Debug.Log(Tag + "recorder verified: " + cols + " columns");
                return;
            }

            Debug.LogError(Tag + "RECORDER COLUMN MISMATCH - header has " + want
                               + " columns, the row wrote " + cols
                               + ". Every column past the difference is mislabelled and the file "
                               + "cannot be trusted. Fix Header/WriteRow before reading this flight.");
        }

        private static int Count(string s, char c)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (s[i] == c) n++;
            return n;
        }

        /// ---- ⛔ THIS USED TO BE `AutoPilot.AscentVessel` AND IT WENT NULL AT INSERTION. ----
        private static Vessel Primary()
        {
            // ---- ⛔ A RETURN IN PROGRESS OUTRANKS THE LATCH. (flight_0820_054631) ----
            if (DeorbitOps.Engaged && DeorbitOps.Vehicle != null
                && DeorbitOps.Vehicle.state != Vessel.State.DEAD)
                return primary = DeorbitOps.Vehicle;
            if (EntryOps.Engaged && EntryOps.Vehicle != null
                && EntryOps.Vehicle.state != Vessel.State.DEAD)
                return primary = EntryOps.Vehicle;

            if (primary != null && primary.state != Vessel.State.DEAD) return primary;

            Vessel a = AutoPilot.AscentVessel;
            if (a == null || a.state == Vessel.State.DEAD) a = EntryOps.Vehicle;
            if (a == null || a.state == Vessel.State.DEAD) a = DeorbitOps.Vehicle;
            if (a == null || a.state == Vessel.State.DEAD)
            {
                a = FlightGlobals.ActiveVessel;
                if (a != null && a == BoosterRecovery.BoosterVessel) a = null;
            }
            primary = a;
            return primary;
        }

        private static bool stageLatched, sepLatched;

        private static double lastMassA, lastMassB;

        private static void WriteRow(Vessel v, double ut)
        {
            StringBuilder r = pending;

            F(r, ut - startedUt); F(r, ut);
            Vessel focus = FlightGlobals.ActiveVessel;
            S(r, focus != null ? focus.vesselName : "-");
            F(r, TimeWarp.CurrentRate);

            // ---------------- ascent vehicle ----------------
            Vessel a = Primary();
            S(r, AutoPilot.Engaged ? Ascent.Name(AutoPilot.Phase) : "-");
            S(r, AutoPilot.Command.Note);
            Motion(r, a, true);
            Margins(r, a, true);
            F(r, AutoPilot.PhaseElapsedS);
            F(r, AutoPilot.RangeToBoosterM / 1000.0);

            AscentCommand c = AutoPilot.Command;
            F(r, c.PitchDeg); F(r, c.HeadingDeg); F(r, c.Throttle);
            // ---- ⛔ EDGES MUST LATCH OR A 5 Hz SAMPLER NEVER SEES THEM. ----
            if (c.Stage) stageLatched = true;
            if (c.SeparateS2) sepLatched = true;
            F(r, stageLatched ? 1.0 : 0.0); F(r, sepLatched ? 1.0 : 0.0);
            stageLatched = false; sepLatched = false;
            F(r, c.UllageFore);
            F(r, c.Rcs ? 1.0 : 0.0);
            F(r, AutoPilot.LastCircDvMps);
            int ullN; F(r, UllageProbe.VesselWorst(a, null, out ullN));
            Attitude(r, AttitudeController.Ascent, true);
            Controls(r, a);

            // ---------------- booster ----------------
            Vessel b = BoosterRecovery.BoosterVessel;
            S(r, BoosterRecovery.Active ? Landing.Name(BoosterRecovery.Phase) : "-");
            Motion(r, b, false);
            Margins(r, b, false);
            F(r, BoosterRecovery.EnginesLit);
            F(r, BoosterRecovery.OctaMode);
            F(r, BoosterRecovery.GridFinsOut ? 1.0 : 0.0);
            F(r, (b != null && b.ActionGroups[KSPActionGroup.Gear]) ? 1.0 : 0.0);
            F(r, BoosterRecovery.PhaseElapsedS);
            F(r, BoosterRecovery.RangeToPartnerM / 1000.0);
            F(r, BoosterRecovery.TrueRadar);
            F(r, BoosterRecovery.LeanFrac);
            F(r, BoosterRecovery.AoaDeg);

            LandingCommand lc = BoosterRecovery.Command;
            F(r, lc.Throttle); F(r, lc.IgnitionAltitude); F(r, lc.Engines);
            F(r, (double)(int)lc.Aim); F(r, lc.DeployLegs ? 1.0 : 0.0);
            F(r, BoosterRecovery.DownrangeM / 1000.0);
            F(r, BoosterRecovery.PredictedMissM / 1000.0);
            F(r, BoosterRecovery.DownMissM / 1000.0);
            F(r, BoosterRecovery.CrossMissM / 1000.0);
            F(r, BoosterRecovery.InitialMissM / 1000.0);
            Attitude(r, AttitudeController.Booster, true);
            Controls(r, b);

            Return(r);
            Commanded(r);
            Diagnostics(r);
            Deep(r);

            r.Length -= 1;
            r.Append("\n");

            if (++pendingRows >= FlushEvery) Flush();
        }

        private static void Commanded(StringBuilder r)
        {
            AttitudeController ac = AttitudeController.Ascent;
            Vessel a = Primary();

            S(r, Owner());
            F(r, ac.Throttle);
            F(r, ac.UllageFore);
            F(r, ac.TranslateX);
            F(r, ac.TranslateY);
            // ---- ⚠ THE COMMANDED SIDE WAS DEAD, EXACTLY LIKE a_cmdThrottle. ----
            bool rcsWanted = AutoPilot.Command.Rcs || DirectApproachOps.Engaged
                             || WaypointApproachOps.Engaged
                             || DockingOps.Engaged || UndockOps.Engaged || DeorbitOps.Engaged;
            F(r, rcsWanted ? 1.0 : 0.0);
            F(r, (a != null && a.ActionGroups[KSPActionGroup.RCS]) ? 1.0 : 0.0);

            S(r, DirectApproachOps.Engaged ? DirectApproachOps.Phase.ToString() : "-");
            F(r, DirectApproachOps.RangeM);
            F(r, DirectApproachOps.ClosingMps);
            F(r, DirectApproachOps.DvMps);
            F(r, DirectApproachOps.WantMps);
            F(r, DirectApproachOps.AimErrorDeg);
            F(r, DirectApproachOps.ThrottleCmd);

            S(r, WaypointApproachOps.Engaged ? WaypointApproachOps.Phase.ToString() : "-");
            F(r, WaypointApproachOps.RangeM);
            F(r, WaypointApproachOps.RadialM);
            F(r, WaypointApproachOps.AlongM);
            F(r, WaypointApproachOps.CrossM);

            S(r, DockingOps.Stage.ToString());
            F(r, DockingOps.RangeToPortM);
            F(r, DockingOps.ClosingMps);
            F(r, DockingOps.AxisErrorDeg);
            F(r, DockingOps.DistF); F(r, DockingOps.DistS); F(r, DockingOps.DistT);
            F(r, DockingOps.VelF);  F(r, DockingOps.VelS);  F(r, DockingOps.VelT);

            S(r, StationApproach.Engaged ? StationApproach.Leg.ToString() : "-");
            F(r, StationApproach.AlongTrackM / 1000.0);
            F(r, StationApproach.LateralMps);
            F(r, UndockOps.SeparationM);
            F(r, UndockOps.OpeningMps);
            F(r, a != null ? Refuel.Fraction(a) : 0.0);

            if (a != null && a.ctrlState != null)
            { F(r, a.ctrlState.X); F(r, a.ctrlState.Y); F(r, a.ctrlState.Z); }
            else { F(r, 0.0); F(r, 0.0); F(r, 0.0); }
            V(r, ac.Actuation);
        }

        private static string Owner()
        {
            int live = 0;
            if (AutoPilot.Engaged) live++;
            if (DockingOps.Engaged) live++;
            if (DirectApproachOps.Engaged) live++;
            if (UndockOps.Engaged) live++;
            if (DeorbitOps.Engaged) live++;
            if (EntryOps.Engaged) live++;
            if (NodeExecutor.Active) live++;

            string top = "-";
            if (AutoPilot.Engaged) top = "ascent";
            else if (EntryOps.Engaged) top = "entry";
            else if (DeorbitOps.Engaged) top = "deorbit";
            else if (NodeExecutor.Active) top = "node";
            else if (UndockOps.Engaged) top = "undock";
            else if (DockingOps.Engaged) top = "docking";
            else if (DirectApproachOps.Engaged) top = "approach";

            if (live > 1)
            {
                bool benign = NodeExecutor.Active && live == 2
                              && (DeorbitOps.Engaged || AutoPilot.Engaged);
                if (!benign) return "CONTENDED:" + top;
            }
            return top;
        }

        private static void Return(StringBuilder r)
        {
            S(r, EntryOps.Stage.ToString());
            S(r, EntryOps.Engaged ? EntryOps.Note : "-");
            F(r, (double)(int)EntryOps.Method);
            F(r, EntryOps.VerticalCmd);
            F(r, EntryOps.LateralCmd);
            F(r, EntryOps.AoaCmdDeg);

            F(r, EntryOps.AlongTrackM / 1000.0);
            F(r, EntryOps.CrossTrackM);
            F(r, EntryOps.MissM);
            F(r, EntryOps.WantLongM / 1000.0);
            F(r, EntryOps.TrimErrorM);
            F(r, EntryOps.BelowProfile ? 1.0 : 0.0);

            F(r, EntryOps.LiftMin);
            F(r, EntryOps.WorstErrorM / 1000.0);
            F(r, EntryOps.Dropped ? 1.0 : 0.0);
            F(r, (EntryOps.DroguesDeployed || ChuteGuard.DroguesDeployed) ? 1.0 : 0.0);
            F(r, (EntryOps.MainsDeployed || ChuteGuard.MainsDeployed) ? 1.0 : 0.0);
            F(r, EntryOps.ThrottleCmd);

            S(r, PhaseDownOps.Stage.ToString());
            F(r, DeorbitOps.AimMissM / 1000.0);
            F(r, DeorbitOps.ThrottleCmd);
            S(r, NodeExecutor.Phase.ToString());
            F(r, NodeExecutor.RemainingDvMps);

            // ---- ⛔ THE RETURN'S BC MUST COME FROM THE RETURN'S VEHICLE. ----
            Vessel bcOurs = EntryOps.Vehicle;
            if (bcOurs == null) bcOurs = DeorbitOps.Vehicle;
            if (bcOurs == null) bcOurs = AutoPilot.AscentVessel;
            F(r, ImpactPredictor.BallisticCoefficient(bcOurs));
            F(r, ImpactPredictor.BallisticCoefficient(BoosterRecovery.BoosterVessel));

            // ---- the middle of the mission. Seven columns, no branches - see the note above. ----
            S(r, DirectApproachOps.Engaged ? ("DIRECT-" + DirectApproachOps.Phase)
                 : (StationApproach.Engaged ? StationApproach.Leg.ToString() : "-"));
            S(r, DockingOps.Stage.ToString());
            S(r, UndockOps.Stage.ToString());

            Vessel me = Primary();
            F(r, StationApproach.RangeM / 1000.0);
            F(r, StationApproach.ClosingMps);
            F(r, me != null ? DockedSide.Mono(me) : 0.0);
            F(r, me != null ? DockedSide.MonoCapacity(me) : 0.0);
        }

        private static void Motion(StringBuilder r, Vessel v, bool orbital)
        {
            if (v == null || v.state == Vessel.State.DEAD)
            {
                // ---- ⛔ A BLOCK OF ZEROS IS NOT "NO DATA", IT IS A LIE THAT PARSES. ----
                int n = orbital ? 21 : 16;
                for (int k = 0; k < n; k++)
                {
                    if (k == (orbital ? 13 : 8)) F(r, orbital ? lastMassA : lastMassB);
                    else F(r, 0.0);
                }
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

            // ---- ⛔ A PACKED VESSEL'S MASS IS NOT ITS MASS. ----
            double massT = v.GetTotalMass();
            if (v.packed)
            {
                double kept = orbital ? lastMassA : lastMassB;
                if (kept > 0.0) massT = kept;
            }
            else if (orbital) lastMassA = massT; else lastMassB = massT;
            F(r, massT);
            F(r, Thrust(v));
            V(r, v.MOI);
            V(r, AttitudeController.For(v) != null ? AttitudeController.For(v).Torque : Vector3d.zero);
        }

        /// ---- PROPELLANT IS THE ONE THAT MATTERS MOST AND WAS ENTIRELY ABSENT ----
        private static void Margins(StringBuilder r, Vessel v, bool full)
        {
            if (v == null || v.state == Vessel.State.DEAD)
            {
                int n = full ? 9 : 4;
                for (int i = 0; i < n; i++) F(r, 0.0);
                return;
            }

            double lf = 0.0, lfMax = 0.0, ox = 0.0, oxMax = 0.0;
            double mono = 0.0, monoMax = 0.0, ec = 0.0, ecMax = 0.0, skin = 0.0;

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p.skinTemperature > skin) skin = p.skinTemperature;
                for (int k = 0; k < p.Resources.Count; k++)
                {
                    PartResource res = p.Resources[k];
                    string nm = res.resourceName;
                    if (nm == "LiquidFuel" || nm == "RP-1" || nm == "CooledRP-1" || nm == "Kerosene")
                        { lf += res.amount; lfMax += res.maxAmount; }
                    else if (nm == "Oxidizer" || nm == "LqdOxygen" || nm == "CooledLqdOxygen")
                        { ox += res.amount; oxMax += res.maxAmount; }
                    else if (nm == "MonoPropellant") { mono += res.amount; monoMax += res.maxAmount; }
                    else if (nm == "ElectricCharge") { ec += res.amount; ecMax += res.maxAmount; }
                }
            }

            F(r, Frac(lf, lfMax));
            F(r, Frac(ox, oxMax));
            if (full) { F(r, Frac(mono, monoMax)); F(r, Frac(ec, ecMax)); }
            F(r, skin);
            F(r, v.packed ? 1.0 : 0.0);
            if (full)
            {
                F(r, BoosterRecovery.CountLit(v));
                double aoa = 0.0;
                if (v.srfSpeed > 1.0 && v.ReferenceTransform != null)
                    aoa = Vector3d.Angle(v.srf_velocity, v.ReferenceTransform.up);
                F(r, aoa);
                F(r, v.geeForce);
            }
        }

        private static double Frac(double a, double max) { return (max > 0.0) ? a / max : 0.0; }

        private static void Diagnostics(StringBuilder r)
        {
            F(r, BoosterRecovery.RecoveryPropFrac);
            F(r, BoosterRecovery.RecoveryPropUnitsNow);
            S(r, CrewProcedureOps.Engaged ? CrewProcedureOps.PhaseName : "-");
            F(r, CrewProcedureOps.Engaged ? 1.0 : 0.0);
            // ---- RETURN PROPELLANT (MMH+NTO) + the Draco strength - the falsifiable "does the mission
            Vessel av = FlightGlobals.ActiveVessel;
            F(r, av != null ? DockedSide.ReturnFraction(av) : -1.0);
            F(r, CapsuleRcs.CurrentPct);
        }

        private static void Deep(StringBuilder r)
        {
            // ---- THE BURN (NodeExecutor) ----
            S(r, NodeExecutor.Phase.ToString());
            F(r, NodeExecutor.InitialDvMps);
            F(r, NodeExecutor.RemainingDvMps);
            F(r, NodeExecutor.DeliveredDvMps);
            F(r, NodeExecutor.PointingErrorDeg);
            F(r, NodeExecutor.ThrottleCmd);
            F(r, NodeExecutor.RcsBurn ? 1.0 : 0.0);
            F(r, NodeExecutor.TimeToIgnitionS);

            // ---- CREW PROCEDURE GATES ----
            bool ce = CrewProcedureOps.Engaged;
            S(r, ce ? CrewProcedureOps.CurrentGate().Id.ToString() : "-");
            S(r, ce ? CrewProcedureOps.Proc.Phase.ToString() : "-");
            F(r, CrewProcedureOps.CrewActionNeeded() ? 1.0 : 0.0);
            F(r, CrewProcedureOps.ReturnArmed ? 1.0 : 0.0);
            S(r, CrewProcedureOps.ReleasedHold.ToString());

            // ---- ABORT RESPONDER ----
            F(r, AbortResponder.LesArmed ? 1.0 : 0.0);
            F(r, AbortResponder.Aborting ? 1.0 : 0.0);
            S(r, AbortResponder.Mode.ToString());

            // ---- LIFE SUPPORT (TAC), the Dragon's own side, one part-walk ----
            LsSample ls = LifeSupportBridge.Sample(FlightGlobals.ActiveVessel);
            F(r, ls.Present ? 1.0 : 0.0);
            F(r, ls.Oxygen01);
            F(r, ls.Co201);
            F(r, Days(ls.Margins.OxygenDays));
            F(r, Days(ls.Margins.FoodDays));
            F(r, Days(ls.Margins.WaterDays));

            // ---- NAMED-BURN RENDEZVOUS (the climb legs + the CW terminal internals) ----
            bool re = NamedRendezvousOps.Engaged;
            S(r, re ? NamedRendezvousOps.Leg.ToString() : "-");
            F(r, NamedRendezvousOps.RangeKm);
            F(r, NamedRendezvousOps.PhaseDeg);
            F(r, NamedRendezvousOps.AlongKm);
            F(r, NamedRendezvousOps.RadialKm);
            F(r, NamedRendezvousOps.ElevDeg);
            F(r, NamedRendezvousOps.LeadDeg);
            F(r, NamedRendezvousOps.GapDeg);
            F(r, NamedRendezvousOps.CoAltKm);
            S(r, NamedRendezvousOps.LastBurn);
            F(r, NamedRendezvousOps.LastDvMps);
            F(r, NamedRendezvousOps.ArrRelMps);
            F(r, NamedRendezvousOps.PassiveMarginM);
            F(r, TimeWarp.CurrentRateIndex);

            // ---- BOOSTER LANDING DIAGNOSTICS (lit-but-no-thrust) ----
            F(r, BoosterRecovery.LiveThrustKn);
            F(r, BoosterRecovery.ColdGasFrac);
            F(r, BoosterRecovery.UllageOn ? 1.0 : 0.0);
            F(r, BoosterRecovery.IgniteAttempts);

            // ---- FDIR: the node-executor thrust-delivery monitor (Layer 3, observe-only) ----
            S(r, HealthMonitor.Name(NodeExecutor.DeliveryVerdict));
            S(r, HealthMonitor.Name(NodeExecutor.DeliveryRaw));
            F(r, NodeExecutor.DeliveryResidual);
            S(r, NodeExecutor.DeliveryFault.ToString());
            S(r, FaultResponse.Name(FaultResponse.Decide(
                    NodeExecutor.DeliveryFault, NodeExecutor.DeliveryVerdict, FaultDomainNow())));

            // ---- DIRECT ACTUATOR AUTHORITY ----
            F(r, BoosterRecovery.GimbalLimitPct);

            // ---- RE-ENTRY HEAT + STEERING (per atmosphere layer) ----
            Vessel entv = FlightGlobals.ActiveVessel;
            F(r, EntryOps.SteeringTest ? 1.0 : 0.0);
            F(r, EntryOps.SweepSegment);
            F(r, entv != null ? entv.atmDensity : 0.0);
            HeatSample hs = EntryHeat.Sample(entv);
            F(r, hs.AblatorFrac);
            F(r, hs.CharFrac);
            F(r, hs.ShieldK);
            F(r, hs.FluxKw);

            // ---- rendezvous FDIR (the Layer-3 responder that ACTS on a frozen rendezvous) ----
            F(r, RendezvousFdir.StallS);
            S(r, HealthMonitor.Name(RendezvousFdir.Verdict));
            F(r, RendezvousFdir.Replans);
            S(r, RendezvousFdir.LastAction);
        }

        private static FaultDomain FaultDomainNow()
        {
            if (BoosterRecovery.Active) return FaultDomain.BoosterRecovery;
            if (AutoPilot.Engaged) return FaultDomain.Ascent;
            if (EntryOps.Engaged) return FaultDomain.Entry;
            if (DeorbitOps.Engaged) return FaultDomain.Deorbit;
            if (NamedRendezvousOps.Engaged || WaypointApproachOps.Engaged
                || DockingOps.Engaged || DirectApproachOps.Engaged || StationApproach.Engaged)
                return FaultDomain.Rendezvous;
            return FaultDomain.OrbitCoast;
        }

        private static double Days(double d) { return (double.IsInfinity(d) || d > 9999.0) ? 9999.0 : d; }

        private static void Attitude(StringBuilder r, AttitudeController ac, bool full)
        {
            F(r, ac.ErrorDeg);
            if (full)
            {
                Vdeg(r, ac.Phi);
                Vdeg(r, ac.TargetOmega);
            }
            Vdeg(r, ac.Omega);
            if (full) V(r, ac.TargetTorque);
            V(r, ac.Actuation);
        }

        private static void Vdeg(StringBuilder r, Vector3d v)
        {
            const double D = 180.0 / System.Math.PI;
            F(r, v.x * D); F(r, v.y * D); F(r, v.z * D);
        }

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

        private static void F(StringBuilder r, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) d = 0.0;
            r.Append(d.ToString("G6", System.Globalization.CultureInfo.InvariantCulture));
            r.Append(',');
        }

        private static void V(StringBuilder r, Vector3d v) { F(r, v.x); F(r, v.y); F(r, v.z); }

        private static void S(StringBuilder r, string s)
        {
            r.Append(string.IsNullOrEmpty(s) ? "-" : s.Replace(',', ';'));
            r.Append(',');
        }
    }
}
