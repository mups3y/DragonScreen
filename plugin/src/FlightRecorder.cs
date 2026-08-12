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
        // Which meant every telemetry column changed which CRAFT it described the moment focus moved
        // to the booster. In the 22:18 recording `massT` steps 35.55 -> 59.13 at MET 102 and then
        // wanders between the two vehicles for the rest of the flight, so nothing in the file can be
        // differenced or plotted without first working out who each row is about. Diagnosing the
        // RTLS from it took hours and several wrong conclusions, which is exactly the cost the
        // recorder exists to avoid.
        //
        // `a_` is the ASCENT vehicle - AutoPilot's, held by reference, so it stays the same craft
        // from liftoff to insertion even after the S2 comes off. `b_` is the BOOSTER. Both are
        // written every row; a vehicle that does not exist writes zeros and its phase reads "-".
        private const string Header =
            "met,ut,focus,warp," +
            // ================= ASCENT VEHICLE =================
            "a_phase,a_note," +
            "a_altAsl,a_altRadar,a_lat,a_lon,a_vertSpeed,a_srfSpeed,a_orbSpeed,a_mach,a_qKpa," +
            "a_apoKm,a_periKm,a_incDeg,a_timeToApS," +
            "a_massT,a_availThrustKn,a_moiX,a_moiY,a_moiZ,a_torqueX,a_torqueY,a_torqueZ," +
            // margins and validity - what is left, and whether physics is even running on it
            "a_lfFrac,a_oxFrac,a_monoFrac,a_ecFrac,a_maxSkinK,a_packed,a_enginesLit," +
            "a_phaseElapsedS,a_rangeToBoosterKm," +
            // guidance command
            "a_cmdPitchDeg,a_cmdHeadingDeg,a_cmdThrottle,a_cmdStage,a_cmdSepS2,a_cmdUllage," +
            "a_cmdRcs,a_circDvMps," +
            // attitude: commanded vs achieved
            // ⛔ EVERY ANGLE AND RATE IN THIS FILE IS DEGREES. THE NAMES SAY SO ON PURPOSE.
            // `Phi` and `Omega` are RADIANS inside the controller - KSP's `angularVelocity` is
            // rad/s and `phi` is compared against `RollControlRangeDeg * Deg2Rad`. They used to be
            // written raw, next to `attErrDeg` and `aoaDeg` which are degrees, with nothing in the
            // header to tell them apart. On 2026-08-12 that cost a wrong diagnosis: booster roll
            // travel was reported as "5.6 degrees through the flip, minor" when the figure was 5.6
            // RADIANS - 320 degrees - and the true total for the recovery was 1330 degrees, nearly
            // four full turns, which the crew could plainly see and the analysis could not.
            //
            // Converted at write time and suffixed, so the mistake cannot be made again.
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
            // landing command and the two numbers that judge it
            "b_cmdThrottle,b_ignitionAlt,b_engines,b_aim,b_legs," +
            "b_downrangeKm,b_predMissKm,b_initMissKm," +
            "b_attErrDeg,b_omegaPdps,b_omegaRdps,b_omegaYdps,b_actP,b_actR,b_actY," +
            "b_ctlPitch,b_ctlYaw,b_ctlRoll,b_ctlThrottle," +
            // ================= THE RETURN =================
            // Not a third vehicle - the same craft as `a_`, in the phases after insertion. Kept in its
            // own block because these columns are all "-" or zero for the whole ascent, so a reader
            // can ignore them until the return and a return recording is readable on its own.
            //
            // ⛔ r_liftMin IS THE COLUMN THAT JUDGES AN ENTRY. Zero at touchdown means the range loop
            // never commanded any shortening: the descent flew OPEN LOOP, the de-orbit aim was too
            // short, and the miss is aim error rather than guidance error. Every aim constant in
            // pure/Deorbit.cs was fitted by reading exactly this out of a recording.
            "r_stage,r_note,r_method,r_vertCmd,r_latCmd,r_aoaCmd," +
            "r_alongKm,r_crossM,r_missM,r_wantLongKm,r_trimErrM,r_belowProfile," +
            "r_liftMin,r_worstErrKm,r_dropped,r_drogues,r_mains,r_burnThr," +
            "r_phaseDown,r_deorbitMissKm,r_deorbitThr,r_nodePhase,r_nodeDvLeft," +
            // The measured ballistic coefficients. Everything the impact predictions are built on
            // comes from these two numbers, and until now they only existed inside the predictor.
            "r_bcAscent,r_bcBooster," +
            // ================= THE MIDDLE OF THE MISSION =================
            // Rendezvous, docking and undock. Added the day they first became reachable from a
            // button - recording a phase for the first time it flies is the whole point, and the
            // 2026-08-11 return went unrecorded precisely because nobody had done this in advance.
            "m_rndz,m_dock,m_undock,m_stationKm,m_closingMps,m_monoOurs,m_monoCap," +
            // ================= WHAT THE VEHICLE WAS ACTUALLY TOLD TO DO =================
            // ⛔ ADDED 2026-08-12 BECAUSE THE COMMAND HALF OF THIS RECORDER WAS DEAD AFTER
            // INSERTION, AND THAT IS THE HALF IT EXISTS FOR.
            //
            // `a_cmd*` above all read `AutoPilot.Command`, which stops being written the instant the
            // ascent disengages. Everything after insertion - the approach, the docking, the
            // de-orbit - flies the SAME vessel through `AttitudeController.Ascent`, and none of it
            // appeared anywhere. Measured on the 2026-08-12 flight: `a_cmdThrottle` read 0.000 for
            // all 7560 rows of the approach while `a_ctlThrottle` hit 1.00 on 783 of them.
            //
            // Worse, there were NO TRANSLATION COLUMNS AT ALL. Translation is the docking
            // controller's entire output, so when the approach and the docking fought each other for
            // eleven minutes and emptied the tank, the conflict had to be inferred from range - the
            // commands that caused it were never written down.
            //
            // `x_owner` is the column that would have made that a one-glance diagnosis instead of an
            // afternoon: it names the controller holding the vehicle on each row. Two controllers
            // cannot both be the owner, so a flicker in this column IS the bug.
            "x_owner,x_thrCmd,x_fore,x_transX,x_transY,x_rcsCmd,x_rcsOn," +
            // The approach, in its own numbers rather than inferred from range.
            "x_daPhase,x_daRangeM,x_daClosing,x_daDv,x_daWant,x_daAimErr,x_daThr," +
            // The docking controller. `x_dkRangeM` is PORT to PORT, which is not `m_stationKm`.
            "x_dkStage,x_dkRangeM,x_dkClosing,x_dkAxisErr," +
            // The docking controller's INPUTS. Paired with x_fore/transX/transY above:
            // a command holding one sign while its own offset grows is an inverted axis.
            "x_dkDistF,x_dkDistS,x_dkDistT,x_dkVelF,x_dkVelS,x_dkVelT," +
            // The ladder, and the undock.
            "x_leg,x_alongKm,x_lateral,x_udSepM,x_udOpening,x_refuelFrac";

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

            try { WriteRow(v, ut); VerifyWidth(); }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "recorder row failed, stopping: " + e.Message);
                Stop("row failed");
            }
        }

        /// <summary>
        /// ⛔ A ROW THAT IS NOT AS WIDE AS THE HEADER IS WORSE THAN NO RECORDING AT ALL.
        ///
        /// Every column after the mismatch is then labelled with its neighbour's name, and the file
        /// looks perfectly well-formed while telling you that the throttle was 29 000. Nothing
        /// headless can catch it - the row is built from live vessel state, so the test suite never
        /// executes this path - and the columns are now written by four helpers whose widths depend
        /// on flags. So it checks itself, once, on the first row, in the game.
        ///
        /// Once is enough: the width is structural. If the first row matches, they all do.
        /// </summary>
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

        /// <summary>
        /// The craft the `a_` block describes.
        ///
        /// ---- ⛔ THIS USED TO BE `AutoPilot.AscentVessel` AND IT WENT NULL AT INSERTION. ----
        /// The 08:40 recording: from "INSERTION COMPLETE" onward every `a_` column reads ZERO -
        /// altitude, apoapsis, mass, the lot - because the autopilot had let go of its reference and
        /// `Motion` writes zeros for a null vessel. The capsule was in a perfectly good 86 × 83 km
        /// orbit at the time. Everything the return stack does happens AFTER that point, so a return
        /// would have been recorded with no telemetry at all about the vehicle flying it.
        ///
        /// The block still means ONE CRAFT - that is the rule the whole a_/b_ split exists for - it is
        /// just latched here instead of borrowed from whoever happens to be steering. The latch only
        /// moves when the craft it points at is gone.
        /// </summary>
        private static Vessel Primary()
        {
            if (primary != null && primary.state != Vessel.State.DEAD) return primary;

            Vessel a = AutoPilot.AscentVessel;
            if (a == null || a.state == Vessel.State.DEAD) a = EntryOps.Vehicle;
            if (a == null || a.state == Vessel.State.DEAD) a = DeorbitOps.Vehicle;
            // ⚠ The active vessel is the LAST resort, never the first: taking it every row is the
            // original defect this whole column block was built to fix, where `massT` stepped between
            // two craft the moment focus moved to the booster.
            if (a == null || a.state == Vessel.State.DEAD)
            {
                a = FlightGlobals.ActiveVessel;
                // ...and not if the booster is what has focus, or the two blocks describe one craft.
                if (a != null && a == BoosterRecovery.BoosterVessel) a = null;
            }
            primary = a;
            return primary;
        }

        /// <summary>Edges held until a row carries them. See the latch note in WriteRow.</summary>
        private static bool stageLatched, sepLatched;

        /// <summary>Last real mass per block, so a null vessel cannot report a massless one.</summary>
        private static double lastMassA, lastMassB;

        private static void WriteRow(Vessel v, double ut)
        {
            StringBuilder r = pending;

            F(r, ut - startedUt); F(r, ut);
            Vessel focus = FlightGlobals.ActiveVessel;
            S(r, focus != null ? focus.vesselName : "-");
            // Physics under warp is not the physics we are tuning. A row taken at 4x is not
            // comparable with one taken at 1x, and nothing in the file said which it was.
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
            // `c.Stage` and `c.SeparateS2` are true for the single tick that issues the command. The
            // recorder samples five times a second, so it sampled between them every time: both
            // columns read 0 for the whole 2026-08-12 flight while the log shows `autopilot staged`
            // and `S2 SEP - dropped on TE.19.C.Dragon.Decoupler`. A column that can only be true on
            // a tick nobody looks at is not instrumentation.
            //
            // Latched until written, so exactly one row carries each edge and none are lost.
            if (c.Stage) stageLatched = true;
            if (c.SeparateS2) sepLatched = true;
            F(r, stageLatched ? 1.0 : 0.0); F(r, sepLatched ? 1.0 : 0.0);
            stageLatched = false; sepLatched = false;
            F(r, c.UllageFore);
            F(r, c.Rcs ? 1.0 : 0.0);
            F(r, AutoPilot.LastCircDvMps);
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
            F(r, BoosterRecovery.InitialMissM / 1000.0);
            Attitude(r, AttitudeController.Booster, false);
            Controls(r, b);

            Return(r);
            Commanded(r);

            r.Length -= 1;                // trailing comma
            r.Append("\n");

            if (++pendingRows >= FlushEvery) Flush();
        }

        /// <summary>
        /// WHAT THE VEHICLE WAS TOLD TO DO, from the controller that was actually driving it.
        ///
        /// ⛔ FIXED WIDTH, NO BRANCHES - the same rule as `Return`. Every source returns a resting
        /// value when it is idle rather than being skipped, because a block whose width depends on
        /// state is this recorder's characteristic bug and it has already shifted a column once.
        ///
        /// The `a_cmd*` block above describes the ASCENT GUIDANCE. This one describes the ACTUATOR,
        /// whoever is holding it - which after insertion is nobody the old block could see.
        /// </summary>
        private static void Commanded(StringBuilder r)
        {
            AttitudeController ac = AttitudeController.Ascent;
            Vessel a = Primary();

            S(r, Owner());
            F(r, ac.Throttle);
            F(r, ac.UllageFore);
            F(r, ac.TranslateX);
            F(r, ac.TranslateY);
            // Commanded RCS versus the group's ACTUAL state. They disagreeing is a real fault and
            // there was no way to see it: the approach turns RCS on and nothing turned it off.
            F(r, AutoPilot.Command.Rcs ? 1.0 : 0.0);
            F(r, (a != null && a.ActionGroups[KSPActionGroup.RCS]) ? 1.0 : 0.0);

            S(r, DirectApproachOps.Engaged ? DirectApproachOps.Phase.ToString() : "-");
            F(r, DirectApproachOps.RangeM);
            F(r, DirectApproachOps.ClosingMps);
            F(r, DirectApproachOps.DvMps);
            F(r, DirectApproachOps.WantMps);
            F(r, DirectApproachOps.AimErrorDeg);
            F(r, DirectApproachOps.ThrottleCmd);

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
        }

        /// <summary>
        /// Which controller is holding the vehicle this tick.
        ///
        /// ⛔ THE POINT OF THIS COLUMN IS THAT IT SHOULD NEVER BE AMBIGUOUS. On 2026-08-12 the
        /// station approach and the docking controller both drove the capsule for eleven minutes,
        /// pulling opposite ways, and emptied the tank - and nothing in 145 columns said so. The
        /// order below is the priority the code is SUPPOSED to enforce, so if two are live at once
        /// this reports the winner and `CONTENDED:` names the clash outright.
        /// </summary>
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

            // The node executor legitimately runs INSIDE the de-orbit and the phasing, so it does
            // not count as a clash with those two.
            if (live > 1)
            {
                bool benign = NodeExecutor.Active && live == 2
                              && (DeorbitOps.Engaged || AutoPilot.Engaged);
                if (!benign) return "CONTENDED:" + top;
            }
            return top;
        }

        /// <summary>
        /// The return block - 25 columns, written unconditionally.
        ///
        /// ⚠ NO BRANCHES IN HERE. `Margins()` once wrote five fields down its null path and four down
        /// its real one, which would have shifted every later column for the pre-separation phase of
        /// every flight. A block whose width depends on state is the recorder's characteristic bug, so
        /// this one has a fixed shape and lets the sources return zero when they are idle.
        /// </summary>
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
            F(r, EntryOps.DroguesDeployed ? 1.0 : 0.0);
            F(r, EntryOps.MainsDeployed ? 1.0 : 0.0);
            F(r, EntryOps.ThrottleCmd);

            S(r, PhaseDownOps.Stage.ToString());
            F(r, DeorbitOps.AimMissM / 1000.0);
            F(r, DeorbitOps.ThrottleCmd);
            S(r, NodeExecutor.Phase.ToString());
            F(r, NodeExecutor.RemainingDvMps);

            F(r, ImpactPredictor.BallisticCoefficient(AutoPilot.AscentVessel));
            F(r, ImpactPredictor.BallisticCoefficient(BoosterRecovery.BoosterVessel));

            // ---- the middle of the mission. Seven columns, no branches - see the note above. ----
            // ⚠ The DIRECT branch is a different law from the ladder, so the column has to say
            // which one flew - a rendezvous logged only as "Terminal" cannot be told apart from one
            // that never entered the gate at all.
            S(r, DirectApproachOps.Engaged ? ("DIRECT-" + DirectApproachOps.Phase)
                 : (StationApproach.Engaged ? StationApproach.Leg.ToString() : "-"));
            S(r, DockingOps.Stage.ToString());
            S(r, UndockOps.Stage.ToString());

            // ⚠ StationApproach's OWN numbers, not a second computation of the same thing. Two
            // sources for one quantity is how a screen and a log end up disagreeing - the exact
            // defect CLAUDE.md's rule 2 exists to prevent.
            Vessel me = Primary();
            F(r, StationApproach.RangeM / 1000.0);
            F(r, StationApproach.ClosingMps);
            // ⚠ OUR tank, not the merged vessel's - see DockedSide. Recording the pair added
            // together is how the live F9I ends up telling the crew a full Dragon is empty.
            F(r, me != null ? DockedSide.Mono(me) : 0.0);
            F(r, me != null ? DockedSide.MonoCapacity(me) : 0.0);
        }

        /// <summary>Where it is and what it weighs. `orbital` adds the orbit block.</summary>
        private static void Motion(StringBuilder r, Vessel v, bool orbital)
        {
            if (v == null || v.state == Vessel.State.DEAD)
            {
                // ---- ⛔ A BLOCK OF ZEROS IS NOT "NO DATA", IT IS A LIE THAT PARSES. ----
                // The width must stay fixed - see Margins - but zero is a legal reading for most of
                // these and a physically impossible one for mass. `a_massT` read 0.000 t on 42 rows
                // of the 2026-08-12 flight, and a mass column that reports a massless spacecraft
                // will silently poison any thrust-to-weight or propellant sum computed from the file.
                //
                // So the shape is unchanged and the one field that cannot legitimately be zero is
                // carried forward from the last real sample instead.
                int n = orbital ? 21 : 16;
                for (int k = 0; k < n; k++)
                {
                    // Index of the mass field within this block. ORBITAL carries obt_speed and the
                    // four orbit fields that the booster block does not, so the two differ:
                    //   orbital  alt,radar,lat,lon,vert,srf,OBT,mach,q,ap,pe,inc,tAp,MASS  -> 13
                    //   booster  alt,radar,lat,lon,vert,srf,    mach,q,            MASS    ->  8
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
            // On 2026-08-12 `a_massT` read 1174.20 t through the launch-window hold with
            // `a_packed = 1`, then 174.19 t on the single tick it unpacked - a thousand tonnes that
            // never existed. On rails KSP is not maintaining the same quantity, and anything derived
            // from it (thrust-to-weight, propellant mass, the landing solve) is nonsense for those
            // rows. Carry the last real reading instead, the same rule as the null path above.
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

        /// <summary>
        /// What is LEFT, and whether the row is trustworthy.
        ///
        /// ---- PROPELLANT IS THE ONE THAT MATTERS MOST AND WAS ENTIRELY ABSENT ----
        /// `falcon-booster-landing-twr`: "at 11% propellant our F9 has TWR 0.81 on one engine and
        /// CANNOT land". That is a decision made against a propellant FRACTION, and no recording
        /// this project has ever taken contained one. Every margin question - can the booster get
        /// home, does the S2 have the dv, has the Dragon enough mono for the rendezvous - is
        /// unanswerable without these four columns, and each would have cost a flight to re-ask.
        ///
        /// `packed` is here for the same reason F9I hunts for "the ~300 km gap in ut": an unloaded
        /// vessel is on rails, so rows either side of that gap are not continuous and nothing we
        /// commanded in between reached anything.
        /// </summary>
        private static void Margins(StringBuilder r, Vessel v, bool full)
        {
            if (v == null || v.state == Vessel.State.DEAD)
            {
                // ⛔ THESE COUNTS MUST MATCH THE LIVE PATH BELOW EXACTLY.
                //   full : lf, ox, mono, ec, skin, packed, enginesLit          = 7
                //   not   : lf, ox,           skin, packed                     = 4
                // This said 5, and the booster is null for the whole pre-separation stretch of every
                // flight - so the first hundred seconds of every recording would have had every
                // column after this one shifted by one, with the header still looking correct.
                int n = full ? 7 : 4;
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
                    if (res.resourceName == "LiquidFuel") { lf += res.amount; lfMax += res.maxAmount; }
                    else if (res.resourceName == "Oxidizer") { ox += res.amount; oxMax += res.maxAmount; }
                    else if (res.resourceName == "MonoPropellant") { mono += res.amount; monoMax += res.maxAmount; }
                    else if (res.resourceName == "ElectricCharge") { ec += res.amount; ecMax += res.maxAmount; }
                }
            }

            F(r, Frac(lf, lfMax));
            F(r, Frac(ox, oxMax));
            if (full) { F(r, Frac(mono, monoMax)); F(r, Frac(ec, ecMax)); }
            F(r, skin);
            F(r, v.packed ? 1.0 : 0.0);
            if (full) F(r, BoosterRecovery.CountLit(v));
        }

        private static double Frac(double a, double max) { return (max > 0.0) ? a / max : 0.0; }

        /// <summary>The control loop's internals. `full` writes phi and the target torques too.</summary>
        private static void Attitude(StringBuilder r, AttitudeController ac, bool full)
        {
            F(r, ac.ErrorDeg);
            if (full)
            {
                Vdeg(r, ac.Phi);
                Vdeg(r, ac.TargetOmega);
            }
            Vdeg(r, ac.Omega);
            if (full) V(r, ac.TargetTorque);          // kN.m - a torque, not an angle
            V(r, ac.Actuation);                       // fraction of available torque
        }

        /// <summary>An angle or rate held in radians, written in DEGREES. See the header note.</summary>
        private static void Vdeg(StringBuilder r, Vector3d v)
        {
            const double D = 180.0 / System.Math.PI;
            F(r, v.x * D); F(r, v.y * D); F(r, v.z * D);
        }

        /// <summary>
        /// What actually reached the axes. Dead in all 554 kOS recordings - cooked steering and
        /// stock SAS both bypass FlightCtrlState - and live since our controller took over. This is
        /// the half that makes system identification possible at all.
        /// </summary>
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
