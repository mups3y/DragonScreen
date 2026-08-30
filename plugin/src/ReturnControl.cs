// DragonScreen — ReturnControl  (KSP glue: seam 6, the return — undock → splashdown)
// ============================================================================================
// Flies the whole back half with the pure guidance: undock + the departure burns + phasing
// (pure/Departure.cs), the deorbit burn (pure/DeorbitGuidance.cs), the lifting entry (pure/Entry.cs), and
// the chutes (pure/Chutes.cs). Dispatched by phase:
//   • return Phasing → FlyDeparture (undock the docking node, then Departure's CW burns on the Dracos to a
//     stable point below the station, then the phasing burn),
//   • Entry → FlyDeorbitEntry (trunk jettison → retrograde Draco deorbit burn on measured periapsis →
//     close the nose shroud → ENGAGE THE CoM SHIFTER + shield-forward lifting entry),
//   • Drogues/Mains/Splashdown → FlyChutes (state-based drogue/main deploy → splashdown).
//
// ⛔ USE THE CoM SHIFTER CORRECTLY (user): the AdjustableCoMShifter Descent Mode is engaged ONCE before
// entry (ToggleMode event) — a mode, not a steering actuator; the shield is held into the flow and (as a
// refinement) banked by an RCS roll, never by toggling the shifter. ATTITUDE-FIRST-THEN-TRANSLATE on the
// Dracos for every burn; the deorbit burn points RETROGRADE and pushes forward (= retrograde Δv).
//
// ⚠ FIRST CUT (validate in flight): the departure/deorbit RCS translation sign (ForwardSign), the deorbit
// target periapsis + cutoff, and the trunk/undock actuation are best-guess and confirmed from the CSV.
// The BANK-ANGLE lifting entry IS wired (FlyDeorbitEntry below: EntrySteering footprint → Entry.Guide →
// RollSign bank loop, CoM shifter engaged once, chutes) — the SIGNS (RollSign, RollRefSign/CrossSign) are
// the best-guess part that a flown entry confirms. Instrumented into the FlightRecorder.
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class ReturnControl
    {
        [Tunable] public static double ForwardSign = -1.0;       // forward RCS translation (KSP H = Z −1)
        [Tunable] public static double AttitudeReadyDeg = 5.0;
        [Tunable] public static double DeorbitTargetPeM = 50000.0;  // lower Pe to here for the entry corridor
        [Tunable] public static double SettleS = 3.0;
        [Tunable] public static double RollKp = 0.6;             // bank loop: st.roll per rad of bank error
        [Tunable] public static double RollSign = 1.0;           // flip if the capsule banks the wrong way
        [Tunable] public static double EntryInterfaceAltM = 120000.0; // atmosphere-entry altitude (entry begins here)
        [Tunable] public static bool CoastWarp = true;           // warp-to-maneuvers through the post-deorbit coast
        [Tunable] public static double CoastWarpFallbackHorizonS = 5400.0; // bounded look-ahead if the period is unusable
        [Tunable] public static double EntryWarpMarginM = 5000.0;// stop warping this far above the interface (1× entry buffer)

        // ⭐ SAFE-LANDING-SITE LZ (SafeLandingSite → nominal return). The lifting entry steers its footprint at a
        // splashdown target; the nominal return has no orbital target to aim at, so — like the abort — we scan the
        // descending ground track for the nearest reachable OPEN WATER and hand it to EntrySteering. Latched once
        // committed (you don't re-pick an LZ mid-entry). Off → the entry steers at v.targetObject (legacy).
        [Tunable] public static bool UseSafeLandingSite = true;
        [Tunable] public static int LzGroundSamples = 130;
        [Tunable] public static double LzGroundStepS = 45.0;
        [Tunable] public static double LzMinGlideM = 1.0e6;
        [Tunable] public static double LzMaxGlideM = 12.0e6;
        static bool lzSelected; static double lzLatDeg, lzLonDeg;

        static DepPhase depPhase = DepPhase.Idle;
        static EntryPhase entPhase = EntryPhase.Idle;
        static ChutePhase chutePhase = ChutePhase.Idle;
        static bool rDroguesArmed, rMainsArmed;   // arm each canopy once (idempotent latch)
        static bool undocked, trunkGone, comEngaged, deorbitDone;
        static double lastBankDeg;
        static int lastBankSign = 1;
        // the shared Draco deorbit burn (DeorbitBurn) + its burn-local state (phase, settle clock, planned/
        // delivered Δv, shroud-closed latch). trunkGone stays a controller field (spans deorbit + entry).
        static DeorbitBurnState deo = new DeorbitBurnState();

        // ---- B8/T6 CourseCorrect entry range channel (1×1: control = bank |σ|) ----
        // OFF by default: it RUNS + RECORDS the Newton-step bank correction (from the predictor's live
        // d(downrange)/d|σ| sensitivity) so a flown CSV reveals whether it beats the Entry.Guide heuristic; it
        // APPLIES the correction (replaces the heuristic |σ| with the corrected magnitude, keeping the S-turn sign)
        // only when UseCourseCorrectEntry is on. ⚠ SIGN-SENSITIVE + FLIGHT-GATED: the slope sign (does more bank
        // shorten or lengthen the range?) is a convention to read off the CSV first. Recomputed at ~4 Hz (one extra
        // impact prediction) so the realtime entry hot path stays light. (The BOOSTER 2×2 is deliberately NOT wired —
        // its impact predictor is ballistic (L/D=0), so a fin-control perturbation moves nothing; see docs.)
        [Tunable] public static bool UseCourseCorrectEntry = false;
        [Tunable] public static double CcEntryPerturbDeg = 5.0;    // finite-difference bank perturbation dσ
        [Tunable] public static double CcEntryMaxStepDeg = 15.0;   // clamp on a single |σ| correction
        [Tunable] public static double CcEntryMaxBankDeg = 80.0;   // never command |σ| beyond this
        [Tunable] public static double CcEntryTickS = 0.25;        // recompute cadence (~4 Hz)
        static double ccAccumS, ccCorrectedMagRad, ccLastDsigmaDeg = double.NaN, ccLastSlope = double.NaN;
        static bool ccHaveCorrection;

        public static void Reset()
        {
            depPhase = DepPhase.Idle; entPhase = EntryPhase.Idle;
            chutePhase = ChutePhase.Idle;
            rDroguesArmed = rMainsArmed = false;
            undocked = trunkGone = comEngaged = deorbitDone = false;
            lastBankSign = 1;
            lzSelected = false;
            deo.Reset();
            ccAccumS = 0; ccCorrectedMagRad = 0; ccLastDsigmaDeg = double.NaN; ccLastSlope = double.NaN;
            ccHaveCorrection = false;
            EntrySteering.Reset();
            FlightDriver.ReleaseTranslation(); FlightDriver.ReleaseRoll(); Steering.Release();
        }

        // ---------------------------------------------------------------- departure (return Phasing)
        public static void TickDeparture(Vessel v, MissionProfile mission)
        {
            try { FlyDeparture(v, mission); }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] departure tick failed: " + e.Message); FlightDriver.ReleaseTranslation(); }
        }

        static void FlyDeparture(Vessel v, MissionProfile mission)
        {
            MissionConductor.Realtime();   // departure is flown near the station KOS at 1× (precision); no warp here
            if (!undocked) { UndockNode(v); undocked = true; }
            Actuator.EnableRcs(v);   // ⛔ direct: per-thruster rcsEnabled + master (no craft AG binding)

            CelestialBody body = v.mainBody;
            ITargetable tgt = v.targetObject;
            if (body == null || tgt == null || tgt.GetOrbit() == null)
            { FlightDriver.ReleaseTranslation(); CrewProcedureOps.PhaseComplete(); return; }   // no station ref → hand on

            double mu = body.gravParameter;
            Vector3d tgtPos = tgt.GetTransform() != null ? (Vector3d)tgt.GetTransform().position
                                                         : tgt.GetOrbit().getPositionAtUT(Planetarium.GetUniversalTime());
            Vector3d tgtVel = tgt.GetObtVelocity();
            double n = Lvlh.MeanMotion(mu, tgt.GetOrbit().semiMajorAxis);
            Vec3 targetR = V(tgtPos - body.position), targetV = V(tgtVel);
            LvlhState rel = Lvlh.Project(targetR, targetV, V((Vector3d)v.CoM - tgtPos), V(v.obt_velocity - tgtVel), n);

            DepartureInputs di = new DepartureInputs();
            di.Valid = true; di.Rel = rel; di.N = n; di.AllNominal = true; di.KosRadiusM = 200;
            di.CoEllipticBelowM = 10000; di.CoEllipticBehindM = 20000;
            di.OrbitRadiusM = (v.CoM - body.position).magnitude;
            di.PhasingLowerM = 10000; di.Mu = mu;

            Vec3 aimL = FirstNonZero(LastDepAim, new Vec3(0, -1, 0));
            Vector3d aim = W(Lvlh.OffsetToWorld(targetR, targetV, aimL.X, aimL.Y, aimL.Z));
            di.AttitudeReady = Steering.PointingErrorDeg(v, aim) <= AttitudeReadyDeg;

            DepartureCommand cmd = Departure.Guide(di, depPhase);
            depPhase = cmd.Phase; LastDepAim = cmd.AimLvlh;

            aim = W(Lvlh.OffsetToWorld(targetR, targetV, cmd.AimLvlh.X, cmd.AimLvlh.Y, cmd.AimLvlh.Z));
            Steering.Point(v, aim);
            bool ready = Steering.PointingErrorDeg(v, aim) <= AttitudeReadyDeg;

            if (cmd.Burn && ready && cmd.BurnDvMps > 0.02) FlightDriver.SetTranslation(0, 0, ForwardSign);
            else FlightDriver.ReleaseTranslation();

            if (cmd.Departed) { FlightDriver.ReleaseTranslation(); CrewProcedureOps.PhaseComplete(); }

            FlightLog.Fill = FillRow;
        }
        static Vec3 LastDepAim = new Vec3(0, -1, 0);

        // ---------------------------------------------------------------- deorbit + lifting entry
        public static void TickDeorbitEntry(Vessel v, MissionProfile mission)
        {
            try { FlyDeorbitEntry(v, mission); }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] deorbit/entry tick failed: " + e.Message); FlightDriver.ReleaseTranslation(); }
        }

        static void FlyDeorbitEntry(Vessel v, MissionProfile mission)
        {
            CelestialBody body = v.mainBody;
            if (body == null) return;
            Vector3d up = Steering.Up(v);

            if (!deorbitDone)
            {
                // the ONE shared Draco retrograde deorbit burn (DeorbitBurn): trunk jettison → settle →
                // retrograde Draco translation closed-loop on MEASURED pe → orient shield-forward. The nose
                // shroud is kept OPEN through the burn (forward Dracos = attitude authority) and closed on
                // completion; Δv planned/delivered live in `deo` for the recorder (PutDv in FillRow).
                deorbitDone = DeorbitBurn.Tick(v, deo, ref trunkGone,
                                               DeorbitTargetPeM, AttitudeReadyDeg, SettleS, ForwardSign);
                FlightLog.Fill = FillRow;
                return;
            }

            // ---- lifting bank-angle entry ----
            FlightDriver.ReleaseTranslation();

            // ⭐ select the safe open-water LZ ONCE (shared LandingSiteScan) and hand it to the footprint steering,
            // so the lifting entry aims at real water instead of the stale orbital target. Retries (window slides)
            // until a site is found, then latches — you commit to one LZ. Off → EntrySteering falls back to v.targetObject.
            if (UseSafeLandingSite && !lzSelected)
            {
                SiteScanResult r = LandingSiteScan.FindWaterSite(v, LzGroundSamples, LzGroundStepS, LzMinGlideM, LzMaxGlideM);
                if (r.Found)
                {
                    lzSelected = true; lzLatDeg = r.LatDeg; lzLonDeg = r.LonDeg;
                    EntrySteering.SetSplashTarget(lzLatDeg, lzLonDeg);
                    Debug.Log("[DragonScreen] return LZ selected: open water at " + lzLatDeg.ToString("F2")
                              + "," + lzLonDeg.ToString("F2") + " (" + r.WaterCount + "/" + r.SampleCount + " samples water)");
                }
            }

            // ⭐ WARP-TO-MANEUVERS: the deorbit burn is done, so the capsule now COASTS ballistically from orbit
            // down to the entry interface (~120 km) — up to ~half an orbit of dead time. Warp toward the interface
            // crossing (CoastEta on ALTITUDE: "range" = alt above interface, closing as it descends), then drop to
            // 1× at the interface so the lifting bank loop is flown in realtime. No burn here → only altitude gates it.
            if (CoastWarp && v.altitude > EntryInterfaceAltM + EntryWarpMarginM)
            {
                double horizonS = (v.orbit != null && v.orbit.period > 60.0) ? v.orbit.period : CoastWarpFallbackHorizonS;
                double etaS = CoastEta.TimeToRange(v.altitude, v.verticalSpeed, EntryInterfaceAltM, horizonS);
                MissionConductor.WarpToEvent(Planetarium.GetUniversalTime() + etaS);
            }
            else MissionConductor.Realtime();   // at/below the interface → realtime for the bank loop

            // 1) the predicted FOOTPRINT error vs the splashdown target drives the bank magnitude/sign
            EntrySteering.MeasureBc(v);
            double downErr, crossErr;
            bool haveTarget = EntrySteering.FootprintError(v, out downErr, out crossErr);

            EntryInputs ei = new EntryInputs();
            ei.Valid = true;
            ei.Velocity = new Vec3(v.srf_velocity.x, v.srf_velocity.y, v.srf_velocity.z);
            ei.Up = new Vec3(up.x, up.y, up.z);
            ei.AltitudeM = v.altitude;
            ei.EntryInterfaceAltM = EntryInterfaceAltM;
            ei.DrogueAltM = Mission.DrogueAltitude;
            ei.SpeedMps = v.srfSpeed;
            ei.PrevBankSign = lastBankSign;
            ei.TargetLoverD = EntrySteering.EntryLoverD;
            ei.DownrangeErrM = downErr; ei.CrossrangeErrM = crossErr;

            EntryCommand ec = Entry.Guide(ei, entPhase);
            entPhase = ec.Phase;
            lastBankSign = ec.BankSign;

            // ⭐ B8/T6 CourseCorrect (entry range 1×1): recompute the bank correction at ~4 Hz from the live
            // d(downrange)/d|σ| sensitivity and RECORD it; apply it (corrected magnitude, heuristic S-turn sign) only
            // behind UseCourseCorrectEntry. Off → the Entry.Guide heuristic bank is commanded unchanged.
            double cmdBankRad = ec.BankRad;
            if (haveTarget && entPhase == EntryPhase.Entry)
            {
                double corrected;
                if (TickCourseCorrectEntry(v, ec.BankRad, out corrected) && UseCourseCorrectEntry)
                    cmdBankRad = corrected;
            }

            lastBankDeg = cmdBankRad * 180.0 / Math.PI;
            EntrySteering.LastSigmaRad = cmdBankRad;   // the predictor assumes the COMMANDED bank next tick

            // ⛔ engage the CoM shifter Descent Mode ONCE (the correct use — a mode, not a steering actuator)
            if (ec.EngageDescentMode && !comEngaged) { comEngaged = EngageCoMShifter(v); }

            // 2) hold the heat shield into the flow (nose RETROGRADE) with SAS...
            Vector3d retroSrf = v.srf_velocity.magnitude > 1 ? -((Vector3d)v.srf_velocity).normalized : up;
            Steering.PointNoRoll(v, retroSrf);   // roll is owned by the bank loop below (FlightDriver.SetRoll)

            // 3) ...and BANK to σ with the roll loop (only once actually in the entry, with a target + aero)
            if (haveTarget && entPhase == EntryPhase.Entry && v.srfSpeed > 1.0)
            {
                double bank = EntrySteering.MeasuredBankRad(v);
                double bankErr = Wrap(cmdBankRad - bank);
                FlightDriver.SetRoll(RollSign * RollKp * bankErr);
            }
            else FlightDriver.ReleaseRoll();

            if (ec.HandToChutes) { FlightDriver.ReleaseRoll(); CrewProcedureOps.PhaseComplete(); }   // → Drogues
            FlightLog.Fill = FillRow;
        }

        // ---------------------------------------------------------------- chutes → splashdown
        public static void TickChutes(Vessel v)
        {
            try { FlyChutes(v); }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] chutes tick failed: " + e.Message); }
        }

        static void FlyChutes(Vessel v)
        {
            ChuteInputs ci = new ChuteInputs();
            ci.Valid = true;
            ci.AltitudeM = v.radarAltitude;
            ci.DescentRateMps = -v.verticalSpeed;   // + = descending
            ci.DrogueAltM = Mission.DrogueAltitude; ci.MainAltM = Mission.MainAltitude; ci.SeaAltM = 0.0;

            ChuteCommand cc = Chutes.Sequence(ci, chutePhase);
            chutePhase = cc.Phase;
            // Arm each canopy ONCE — RealChute arming is idempotent, but re-invoking deploy every tick reset its
            // inflation (the abort 122 m/s bug), so latch here too (see Actuator.DeployChutePart).
            if (cc.DeployDrogues && !rDroguesArmed) { Actuator.DeployChutes(v, true); rDroguesArmed = true; }
            if (cc.DeployMains && !rMainsArmed) { Actuator.DeployChutes(v, false); rMainsArmed = true; }

            if (cc.Splashed || v.situation == Vessel.Situations.SPLASHED || v.situation == Vessel.Situations.LANDED)
                CrewProcedureOps.PhaseComplete();   // mission complete
            FlightLog.Fill = FillRow;
        }

        // ---------------------------------------------------------------- actuation helpers
        static void UndockNode(Vessel v)
        {
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    ModuleDockingNode nd = v.parts[i].Modules.GetModule<ModuleDockingNode>();
                    if (nd != null && nd.otherNode != null)
                    { nd.Undock(); Debug.Log("[DragonScreen] UNDOCK — hooks open, backing away"); return; }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] undock failed: " + e.Message); }
        }

        // (trunk jettison + shroud close now live in the shared DeorbitBurn — Actuator.JettisonTrunk fires the
        //  ModuleTundraDecoupler by name, and the shroud is closed only AFTER the burn, not at trunk sep.)

        // ⛔ engage the offset-CoM Descent Mode via the AdjustableCoMShifter's ToggleMode event (once).
        static bool EngageCoMShifter(Vessel v)
        {
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        PartModule pm = p.Modules[m];
                        if (pm.moduleName != "AdjustableCoMShifter") continue;
                        BaseEvent ev = pm.Events["ToggleMode"];
                        if (ev != null) { ev.Invoke(); Debug.Log("[DragonScreen] CoM shifter Descent Mode ENGAGED (offset CoM → entry trim/L/D)"); return true; }
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] CoM shifter engage failed: " + e.Message); }
            return false;
        }

        // (chute deployment is RealChute-aware and lives in Actuator.DeployChutes)

        static Vec3 V(Vector3d d) { return new Vec3(d.x, d.y, d.z); }
        static Vector3d W(Vec3 p) { return new Vector3d(p.X, p.Y, p.Z); }
        static Vec3 FirstNonZero(Vec3 a, Vec3 f) { return a.Magnitude > 1e-6 ? a : f; }
        static double Wrap(double rad)   // to [−π, π]
        {
            while (rad > Math.PI) rad -= 2.0 * Math.PI;
            while (rad < -Math.PI) rad += 2.0 * Math.PI;
            return rad;
        }

        // B8/T6: at ~4 Hz, perturb the predicted bank |σ| and solve the 1×1 CourseCorrect Newton step that nulls
        // the downrange miss. Returns true + the CORRECTED signed bank (heuristic S-turn sign, corrected magnitude);
        // records the step (deg) + the sensitivity slope (m/rad) EVERY recompute for the flight sign-check. false →
        // refuse/hold (caller keeps the heuristic). Between the 4 Hz recomputes the last corrected magnitude holds.
        static bool TickCourseCorrectEntry(Vessel v, double heuristicBankRad, out double correctedBankRad)
        {
            correctedBankRad = heuristicBankRad;
            ccAccumS += TimeWarp.fixedDeltaTime;
            if (ccAccumS >= CcEntryTickS)
            {
                ccAccumS = 0.0;
                const double Deg2Rad = Math.PI / 180.0;
                double mag0 = Math.Abs(heuristicBankRad);
                double du = CcEntryPerturbDeg * Deg2Rad;
                double e0, e1;
                if (EntrySteering.PredictDownErrAtBank(v, mag0, out e0)
                    && EntrySteering.PredictDownErrAtBank(v, mag0 + du, out e1))
                {
                    DivertResult dr = CourseCorrect.Solve1x1(e0, e1, du, CcEntryMaxStepDeg * Deg2Rad);
                    ccLastSlope = dr.Det;   // d(downrange)/dσ (m/rad) — the sign to verify from the CSV
                    if (dr.Ok)
                    {
                        double maxBank = CcEntryMaxBankDeg * Deg2Rad;
                        double m = mag0 + dr.Du1;
                        if (m < 0.0) m = 0.0; else if (m > maxBank) m = maxBank;
                        ccCorrectedMagRad = m;
                        ccLastDsigmaDeg = dr.Du1 / Deg2Rad;
                        ccHaveCorrection = true;
                    }
                    else { ccLastDsigmaDeg = double.NaN; ccHaveCorrection = false; }   // not observable → hold heuristic
                }
                else { ccLastDsigmaDeg = double.NaN; ccLastSlope = double.NaN; ccHaveCorrection = false; }
            }
            if (!ccHaveCorrection) return false;
            double sign = heuristicBankRad >= 0.0 ? 1.0 : -1.0;   // keep the S-turn sign; correct only the magnitude
            correctedBankRad = sign * ccCorrectedMagRad;
            return true;
        }

        static void FillRow(string[] row)
        {
            FlightRecorder.PutReturn(row, depPhase, deo.Phase, entPhase, lastBankDeg * Math.PI / 180.0,
                                     comEngaged, chutePhase, chutePhase != ChutePhase.Idle,
                                     chutePhase == ChutePhase.Main || chutePhase == ChutePhase.Splashed);
            FlightRecorder.PutDv(row, deo.DvPlannedMps, deo.DvDeliveredMps);   // deorbit burn Δv (shared DeorbitBurn)
            FlightRecorder.PutCourseCorrect(row, ccLastDsigmaDeg, ccLastSlope);   // B8/T6 entry divert (records-first)
        }
    }
}
