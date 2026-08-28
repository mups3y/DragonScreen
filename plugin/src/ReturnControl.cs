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
// target periapsis + cutoff, the trunk/undock actuation, and — BANK-ANGLE ENTRY STEERING IS NOT YET WIRED
// (shield-forward lifting entry with the CoM engaged, chutes deploy; the S-turn bank modulation to hit the
// splashdown zone is the next refinement, like booster targeting was). Instrumented into the FlightRecorder.
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

        static DepPhase depPhase = DepPhase.Idle;
        static DeorbitPhase deoPhase = DeorbitPhase.Idle;
        static EntryPhase entPhase = EntryPhase.Idle;
        static ChutePhase chutePhase = ChutePhase.Idle;
        static bool rDroguesArmed, rMainsArmed;   // arm each canopy once (idempotent latch)
        static bool undocked, trunkGone, shroudClosed, comEngaged, deorbitDone;
        static double settleStartUT = -1;
        static double lastBankDeg;
        static int lastBankSign = 1;
        // deorbit-burn Δv instrumentation: planned (formula from measured orbit) + delivered (∫ measured RCS
        // thrust). Delivered also feeds di.DvAppliedMps — the guidance's own backstop cutoff (was never set).
        static double deorbitDvPlanned, deorbitDvDelivered, lastBurnUT = -1;

        public static void Reset()
        {
            depPhase = DepPhase.Idle; deoPhase = DeorbitPhase.Idle; entPhase = EntryPhase.Idle;
            chutePhase = ChutePhase.Idle;
            rDroguesArmed = rMainsArmed = false;
            undocked = trunkGone = shroudClosed = comEngaged = deorbitDone = false;
            settleStartUT = -1; lastBankSign = 1;
            deorbitDvPlanned = deorbitDvDelivered = 0; lastBurnUT = -1;
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
            Vector3d velI = v.obt_velocity;

            if (!deorbitDone)
            {
                Actuator.EnableRcs(v);   // ⛔ direct: per-thruster rcsEnabled + master (no craft AG binding)

                DeorbitInputs di = new DeorbitInputs();
                di.Valid = true;
                di.Velocity = new Vec3(velI.x, velI.y, velI.z);
                di.Up = new Vec3(up.x, up.y, up.z);
                di.PeriapsisAltM = (v.orbit != null) ? v.orbit.PeA : 0.0;
                di.EntryInterfaceAltM = DeorbitTargetPeM;
                di.TrunkAttached = !trunkGone;
                di.SettleS = SettleS;
                di.SettleElapsedS = settleStartUT > 0 ? Planetarium.GetUniversalTime() - settleStartUT : 0.0;
                di.DvAppliedMps = deorbitDvDelivered;   // feed the guidance's backstop cutoff (was unset → 0)

                // planned deorbit Δv = retrograde Δv to lower pe from the current radius to the entry interface
                // (measured-state formula, not a sim). Recompute each tick as r_c/pe evolve through the burn.
                if (v.orbit != null && v.mainBody != null)
                    deorbitDvPlanned = DeorbitGuidance.DeorbitDvMps(
                        (v.CoM - v.mainBody.position).magnitude, v.mainBody.Radius + DeorbitTargetPeM,
                        v.mainBody.gravParameter);

                // point retrograde (the burn axis) and check ready
                Vector3d retro = velI.magnitude > 1 ? -velI.normalized : up;
                di.AttitudeReady = Steering.PointingErrorDeg(v, retro) <= AttitudeReadyDeg;
                di.AllNominal = true;

                DeorbitCommand dc = DeorbitGuidance.Guide(di, deoPhase);
                deoPhase = dc.Phase;

                if (dc.JettisonTrunk && !trunkGone) { JettisonTrunk(v); trunkGone = true; if (!shroudClosed) { CloseNoseShroud(v); shroudClosed = true; } }
                if (deoPhase == DeorbitPhase.Settle && settleStartUT < 0) settleStartUT = Planetarium.GetUniversalTime();

                Steering.Point(v, retro);
                bool ready = Steering.PointingErrorDeg(v, retro) <= AttitudeReadyDeg;
                // the "throttle" is the Draco retrograde burn: point retrograde + translate forward
                double nowUT = Planetarium.GetUniversalTime();
                if (dc.Throttle > 0.0 && ready)
                {
                    FlightDriver.SetTranslation(0, 0, ForwardSign);
                    // integrate delivered Δv = ∫ (measured RCS thrust / mass) dt while the burn is actually firing
                    double massKg = v.totalMass * 1000.0;
                    if (lastBurnUT > 0 && massKg > 1.0)
                        deorbitDvDelivered += Actuator.RcsThrustN(v) / massKg * (nowUT - lastBurnUT);
                    lastBurnUT = nowUT;
                }
                else { FlightDriver.ReleaseTranslation(); lastBurnUT = -1; }

                if (dc.Complete) { deorbitDone = true; FlightDriver.ReleaseTranslation(); }
                FlightLog.Fill = FillRow;
                return;
            }

            // ---- lifting bank-angle entry ----
            FlightDriver.ReleaseTranslation();

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
            lastBankDeg = ec.BankRad * 180.0 / Math.PI;
            EntrySteering.LastSigmaRad = ec.BankRad;   // the predictor assumes this bank next tick

            // ⛔ engage the CoM shifter Descent Mode ONCE (the correct use — a mode, not a steering actuator)
            if (ec.EngageDescentMode && !comEngaged) { comEngaged = EngageCoMShifter(v); }

            // 2) hold the heat shield into the flow (nose RETROGRADE) with SAS...
            Vector3d retroSrf = v.srf_velocity.magnitude > 1 ? -((Vector3d)v.srf_velocity).normalized : up;
            Steering.PointNoRoll(v, retroSrf);   // roll is owned by the bank loop below (FlightDriver.SetRoll)

            // 3) ...and BANK to σ with the roll loop (only once actually in the entry, with a target + aero)
            if (haveTarget && entPhase == EntryPhase.Entry && v.srfSpeed > 1.0)
            {
                double bank = EntrySteering.MeasuredBankRad(v);
                double bankErr = Wrap(ec.BankRad - bank);
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

        static void JettisonTrunk(Vessel v)
        {
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    ModuleDecouple d = p.Modules.GetModule<ModuleDecouple>();
                    if (d != null && !d.isDecoupled) { d.Decouple(); Debug.Log("[DragonScreen] TRUNK JETTISON"); return; }
                    ModuleAnchoredDecoupler a = p.Modules.GetModule<ModuleAnchoredDecoupler>();
                    if (a != null && !a.isDecoupled) { a.Decouple(); Debug.Log("[DragonScreen] TRUNK JETTISON"); return; }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] trunk jettison failed: " + e.Message); }
        }

        static void CloseNoseShroud(Vessel v)
        {
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    List<ModuleAnimateGeneric> an = v.parts[i].Modules.GetModules<ModuleAnimateGeneric>();
                    for (int m = 0; m < an.Count; m++)
                        if (an[m].animationName == "TE_23_CD2_NOSECONE_ANI" && an[m].Progress > 0.5f)
                        { an[m].Toggle(); Debug.Log("[DragonScreen] nose shroud CLOSED (protect the port on entry)"); return; }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] nose shroud close failed: " + e.Message); }
        }

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

        static void FillRow(string[] row)
        {
            FlightRecorder.PutReturn(row, depPhase, deoPhase, entPhase, lastBankDeg * Math.PI / 180.0,
                                     comEngaged, chutePhase, chutePhase != ChutePhase.Idle,
                                     chutePhase == ChutePhase.Main || chutePhase == ChutePhase.Splashed);
            FlightRecorder.PutDv(row, deorbitDvPlanned, deorbitDvDelivered);   // deorbit burn Δv (was never recorded)
        }
    }
}
