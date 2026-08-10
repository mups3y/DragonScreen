/*
 * DragonScreen - EntryOps
 *
 * GLUE. Everything from "the de-orbit burn has finished" to "the capsule is on the ground": drop the
 * trunk, coast shield-forward, trim the range, fly the lifting entry, and land on chutes or on
 * SuperDracos. Ported from `F9I/dragon_deorbit.ks` - `DgSepStack:1859`, `DgPreEntryTrim:1910`,
 * `DgCoastToEI:1970`, `DgEntryGuidance:2109`, `DgTerminal:2411` and the tail of `DgRecoveryMain`.
 *
 * Laws live in `pure/EntryGuidance.cs` (the bank controller), `pure/EntryMargin.cs` (the altitude
 * schedule it steers to), `pure/Terminal.cs` (the chute and burn gates) and `pure/Deorbit.cs` (the aim
 * and the monopropellant reserves). This file is the sequence and the vectors, nothing else.
 *
 * ---- ⛔ FIVE THINGS HERE ARE COUNTER-INTUITIVE, AND EACH ONE IS A FLIGHT ----
 *
 * 1. ONE SEPARATION EVENT, IN THE RETROGRADE ATTITUDE. The TRUNK decoupler takes trunk + decoupler +
 *    second stage together (`falcon-dragon-two-decouplers`). Doing it facing retrograde - rather than
 *    turning prograde to "throw the trunk backwards" - means the decoupler pushes the trunk PROGRADE
 *    while the burn that follows drives us retrograde, so separation and burn help each other, and the
 *    capsule is already pointed for the next phase instead of turning 180° twice on RCS.
 *
 * 2. THE DEAD COAST IS NOT DEAD - IT IS THE LAST CHANCE TO FIX THE RANGE. Flight log 001: the de-orbit
 *    cut 31 km long, and the capsule then sat shield-forward from 76 km to 70 km doing nothing but
 *    printing its altitude. The entry guidance that follows is SHORTEN-ONLY, so range still long here
 *    has to be flown off by the atmosphere and range that ends up short can never be recovered at all.
 *
 * 3. THE TRIM RUNS ON TRANSLATION, NOT ATTITUDE. Steering stays locked on surface-retrograde
 *    throughout, so the capsule never leaves the shield-forward attitude it must hold at the interface.
 *    Positive `UllageFore` with the nose on retrograde pushes retrograde, which SHORTENS.
 *
 * 4. THE CHUTES ARE CUT ONLY AFTER THE ENGINES ARE PROVEN LIT. See `pure/Terminal.cs`.
 *
 * 5. THE GEAR GOES DOWN AFTER TOUCHDOWN, AND NEVER ON A SPLASHDOWN. It lives in the heat shield: out
 *    early it is drag hanging in the airflow and, on a propulsive landing, it sits in the SuperDraco
 *    plume. In the water there is nothing to stand on and legs only risk breaking the capsule up.
 *
 * ---- ⚠ WHAT IS DELIBERATELY NOT HERE ----
 * **Warp.** F9I rails-warps the coast and physics-warps the entry above 55 km. `docs/PORT_PLAN.md`
 * lists warp automation as out of scope - the recorder logs `warp` so a warped row is identifiable,
 * and taking the time controls off the crew is a separate decision from flying the vehicle. The coast
 * is therefore real time unless the crew warps it themselves, which is safe: every gate in this file
 * is an altitude or a speed, not a clock.
 *
 * **The Trajectories descent profile.** `DgSetProfile` exists only to tell that mod which attitude to
 * assume. Our predictor measures the vehicle's actual drag instead, so there is nothing to tell.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public enum EntryStage : byte
    {
        Idle = 0,
        /// <summary>Pointing retrograde, then one decouple. Trap 1.</summary>
        Separating,
        /// <summary>Shield forward, falling towards the interface.</summary>
        CoastToInterface,
        /// <summary>RCS translation against the predicted range. Traps 2 and 3.</summary>
        Trimming,
        /// <summary>The bank controller. Holds retrograde until there is air to steer with.</summary>
        LiftingEntry,
        /// <summary>Below handover, waiting for a speed the drogues survive.</summary>
        AwaitDrogue,
        /// <summary>Drogues out, coming down on chutes.</summary>
        DroguesOut,
        /// <summary>Drogues out, SuperDracos lighting underneath them.</summary>
        ArmingEngines,
        /// <summary>Chutes cut, engines lit, waiting for the burn gate.</summary>
        Committed,
        LandingBurn,
        /// <summary>Mains out - either the planned chute landing, or the propulsive abandon.</summary>
        MainsOut,
        Down,
        Failed
    }

    public static class EntryOps
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>
        /// Seconds between impact predictions during the entry.
        ///
        /// ⚠ NOT every frame. `ImpactPredictor.Predict` integrates the whole remaining trajectory
        /// through the atmosphere with RK4; `falcon-performance` measured the de-orbit scan as one of
        /// only two things on this project that actually cost frames. F9I ran its loop at 0.1 s against
        /// a prediction another mod had already computed - we pay for ours, so we buy it at the rate
        /// the de-orbit burn already uses and hold the answer in between.
        /// </summary>
        public const double PredictIntervalS = 0.5;

        /// <summary>Degrees off retrograde that counts as pointed, for the separation.</summary>
        public const double SepAlignedDeg = 25.0;
        /// <summary>...and the longest we will wait for it, seconds.</summary>
        public const double SepAlignMaxS = 20.0;
        /// <summary>Settle time after a decouple, seconds.</summary>
        public const double SepSettleS = 1.5;

        /// <summary>Start trimming this far above the atmosphere, metres.</summary>
        public const double TrimStartAboveM = 5000.0;
        /// <summary>
        /// ...and stop this far above it. The last kilometre is left clear so the attitude is settled
        /// and the controls neutral before the air starts doing the flying.
        /// </summary>
        public const double TrimFloorAboveM = 1000.0;

        /// <summary>Seconds to wait under the drogues before lighting the SuperDracos.</summary>
        public const double EngineArmDelayS = 2.0;

        // ---- state ----
        public static EntryStage Stage { get; private set; }
        public static bool Engaged { get; private set; }
        public static string Note = "-";

        /// <summary>Where we are trying to land. Defaults to LZ-1, same as the de-orbit.</summary>
        public static double TargetLatDeg = LandingSites.Lz1.LatDeg;
        public static double TargetLonDeg = LandingSites.Lz1.LonDeg;

        /// <summary>
        /// Does the crew want a SuperDraco touchdown? F9I's `dragonPropulsive`, and like it this
        /// defaults to FALSE - parachutes are the real Crew Dragon procedure and the one every flight
        /// in the calibration history flew. Asking for propulsive is a request, not a guarantee:
        /// `Terminal.Choose` still has to find the engines and the propellant.
        /// </summary>
        public static bool PropulsiveRequested = false;

        /// <summary>Chute state, for the pages and the recorder.</summary>
        public static bool DroguesDeployed, MainsDeployed;

        /// <summary>Live guidance, for the pages and the recorder.</summary>
        public static double VerticalCmd, LateralCmd, AoaCmdDeg;
        public static double AlongTrackM, CrossTrackM, MissM = -1.0, WantLongM;
        public static double TrimErrorM, ThrottleCmd;
        public static bool BelowProfile;
        public static LandingMethod Method = LandingMethod.Parachute;

        /// <summary>
        /// The controller's own memory, exposed for the recorder.
        ///
        /// ⛔ `LiftMin` IS THE ONE NUMBER THAT JUDGES AN ENTRY. Zero at the end means the loop never
        /// commanded any shortening - it flew OPEN LOOP, the aim was too short, and the miss is aim
        /// error rather than guidance error. F9I's whole calibration history turns on reading that
        /// value out of a recording, so it belongs in the file rather than only in a log line.
        /// </summary>
        public static double LiftMin { get { return mem.LiftMin; } }
        public static double WorstErrorM { get { return mem.WorstErrorM; } }
        public static bool Dropped { get { return mem.Dropped; } }
        public static Vessel Vehicle { get { return ship; } }

        private static Vessel ship;
        private static EntryMemory mem;
        private static double stageStartedAt, lastPredictAt, lastGuideAt, sepFiredAt;
        private static bool haveImpact, enginesCommanded;
        private static double impactLat, impactLon;

        // ------------------------------------------------------------------ engage

        public static void Engage(Vessel v)
        {
            if (v == null) return;
            ship = v;
            Engaged = true;
            mem = new EntryMemory();
            haveImpact = false; enginesCommanded = false;
            DroguesDeployed = false; MainsDeployed = false;
            // ⚠ sepFiredAt is a LATCH, not a timestamp to leave lying around. Left set from a previous
            // run, Separate() believes it has already fired and skips the decouple entirely - the
            // capsule would enter with the trunk still on it and no message saying why.
            sepFiredAt = 0.0;
            MissM = -1.0; VerticalCmd = 0.0; LateralCmd = 0.0; AoaCmdDeg = 0.0;
            lastPredictAt = 0.0; lastGuideAt = 0.0;
            AttitudeController.Ascent.Throttle = 0.0;
            if (!v.ActionGroups[KSPActionGroup.RCS]) v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            bool stacked = HasPart(new PartMatch(VehicleParts.IsTrunk))
                        || HasPart(new PartMatch(VehicleParts.IsSecondStage));
            Go(stacked ? EntryStage.Separating : EntryStage.CoastToInterface);
            Debug.Log(Tag + "entry sequence engaged - " + (stacked ? "stack still attached" : "clean capsule")
                      + ", target " + TargetLatDeg.ToString("F4") + ", " + TargetLonDeg.ToString("F4"));
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            AttitudeController.Ascent.Throttle = 0.0;
            AttitudeController.Ascent.UllageFore = 0.0;
            AttitudeController.Ascent.Release(ship);
            Debug.Log(Tag + "entry sequence disengaged - " + why);
            ship = null;
        }

        public static void Reset()
        {
            Engaged = false; Stage = EntryStage.Idle; ship = null; Note = "-";
            mem = new EntryMemory();
            haveImpact = false; enginesCommanded = false; sepFiredAt = 0.0;
            DroguesDeployed = false; MainsDeployed = false;
            VerticalCmd = 0.0; LateralCmd = 0.0; AoaCmdDeg = 0.0; ThrottleCmd = 0.0;
            AlongTrackM = 0.0; CrossTrackM = 0.0; MissM = -1.0; WantLongM = 0.0;
            TrimErrorM = 0.0; BelowProfile = false;
        }

        private static void Go(EntryStage s)
        {
            Stage = s;
            stageStartedAt = Planetarium.GetUniversalTime();
        }

        // ------------------------------------------------------------------ the loop

        public static void Tick()
        {
            if (!Engaged) return;
            if (ship == null || ship.state == Vessel.State.DEAD) { Disengage("vessel lost"); return; }

            double now = Planetarium.GetUniversalTime();
            double inStage = now - stageStartedAt;

            // Down is down, from any stage. The terminal states can be reached out of order (an
            // abandoned propulsive landing arrives under mains from a different branch), so the test
            // belongs here rather than in each of them.
            if (Stage != EntryStage.Down && Landed())
            {
                Touchdown();
                return;
            }

            switch (Stage)
            {
                case EntryStage.Separating: Separate(now, inStage); break;
                case EntryStage.CoastToInterface: Coast(); break;
                case EntryStage.Trimming: Trim(); break;
                case EntryStage.LiftingEntry: Fly(now); break;
                case EntryStage.AwaitDrogue: AwaitDrogue(); break;
                case EntryStage.DroguesOut: UnderDrogues(); break;
                case EntryStage.ArmingEngines: ArmEngines(inStage); break;
                case EntryStage.Committed: Commit(); break;
                case EntryStage.LandingBurn: Burn(inStage); break;
                case EntryStage.MainsOut: UnderMains(); break;
            }
        }

        // ------------------------------------------------------------------ E6

        /// <summary>
        /// Trap 1: point retrograde, then ONE decouple.
        ///
        /// The fallback matters. If the trunk decoupler does not take the second stage with it - a
        /// differently-built craft tree - we use the Dragon decoupler rather than entering with a spent
        /// stage on the nose, which no heat shield can be held forward of.
        /// </summary>
        private static void Separate(double now, double inStage)
        {
            AttitudeController.Ascent.Throttle = 0.0;
            Vector3d retro = -ship.obt_velocity.normalized;
            AttitudeController.Ascent.SteerTo(ship, retro, Vector3d.zero);
            double off = Vector3d.Angle(ship.ReferenceTransform.up, retro);

            if (sepFiredAt <= 0.0)
            {
                Note = "SEPARATION - aligning, " + off.ToString("F0") + " deg";
                if (off > SepAlignedDeg && inStage < SepAlignMaxS) return;

                if (HasPart(new PartMatch(VehicleParts.IsTrunk)))
                {
                    int n = Decouple(new PartMatch(VehicleParts.IsTrunk));
                    Debug.Log(Tag + "trunk jettison commanded (" + n + " decoupler(s)) at "
                              + off.ToString("F0") + " deg off retrograde");
                }
                sepFiredAt = now;
                return;
            }

            if (now - sepFiredAt < SepSettleS) { Note = "SEPARATION - clearing"; return; }

            if (HasPart(new PartMatch(VehicleParts.IsTrunk)))
                Debug.LogError(Tag + "TRUNK WILL NOT JETTISON - entry attitude cannot be held");

            if (HasPart(new PartMatch(VehicleParts.IsSecondStage)))
            {
                Debug.LogWarning(Tag + "second stage still attached after trunk jettison - "
                                 + "using the Dragon decoupler");
                Decouple(new PartMatch(VehicleParts.IsDragonDecoupler));
            }

            sepFiredAt = 0.0;
            Go(EntryStage.CoastToInterface);
        }

        /// <summary>Shield forward, falling. Nothing to do but hold the attitude until the trim window.</summary>
        private static void Coast()
        {
            HoldRetrograde();
            double atm = ship.mainBody.atmosphereDepth;
            double alt = ship.radarAltitude;
            Note = "COAST - shield forward, " + (alt / 1000.0).ToString("F1") + " km";

            if (alt < atm + TrimStartAboveM) Go(EntryStage.Trimming);
        }

        /// <summary>
        /// Traps 2 and 3: spend the coast taking the long bias out, on translation only.
        ///
        /// ⛔ THE LANDING RESERVE OUTRANKS THE RANGE, EVERY TIME. The chutes and the terminal guidance
        /// need that monopropellant more than the impact point does, and this is the same test the
        /// de-orbit burn loop uses so the two cannot disagree.
        /// </summary>
        private static void Trim()
        {
            HoldRetrograde();
            double atm = ship.mainBody.atmosphereDepth;
            double alt = ship.radarAltitude;

            if (alt < atm + TrimFloorAboveM)
            {
                AttitudeController.Ascent.UllageFore = 0.0;
                Debug.Log(Tag + "pre-entry trim finished at " + (alt / 1000.0).ToString("F1")
                          + " km - range error " + TrimErrorM.ToString("F0") + " m, mono "
                          + Mono(ship).ToString("F1"));
                Go(EntryStage.LiftingEntry);
                return;
            }

            Predict();
            if (!haveImpact)
            {
                AttitudeController.Ascent.UllageFore = 0.0;
                Note = "COAST - no impact prediction yet, " + (alt / 1000.0).ToString("F1") + " km";
                return;
            }

            // How far PAST the target the impact currently sits, against how far past it should.
            double past = Orbital.GroundRange(ship.mainBody.Radius, TargetLatDeg, TargetLonDeg,
                                              impactLat, impactLon);
            TrimErrorM = past - Aim();

            double cmd = 0.0;
            if (Mono(ship) <= Reserve()) cmd = 0.0;
            else if (TrimErrorM > Deorbit.TrimToleranceM) cmd = 1.0;
            else if (TrimErrorM < -Deorbit.TrimToleranceM) cmd = -1.0;
            AttitudeController.Ascent.UllageFore = cmd;

            Note = "PRE-ENTRY TRIM - long by " + TrimErrorM.ToString("F0") + " m, "
                 + (alt / 1000.0).ToString("F1") + " km";
        }

        // ------------------------------------------------------------------ E7

        /// <summary>
        /// The lifting entry. Holds surface-retrograde until there is air, then banks.
        ///
        /// The law is `pure/EntryGuidance.cs` and its four traps are documented there. What happens
        /// here is only the vector arithmetic that turns two scalar commands into an attitude.
        /// </summary>
        private static void Fly(double now)
        {
            double alt = ship.radarAltitude;

            if (alt < Terminal.HandoverAltM)
            {
                Handover();
                return;
            }

            // ---- the frame the lift is expressed in ----
            Vector3d vn = ship.srf_velocity.normalized;
            Vector3d up = (ship.CoM - ship.mainBody.position).normalized;
            Vector3d upRaw = Vector3d.Exclude(vn, up);
            if (upRaw.magnitude < 0.05)
            {
                // ⚠ A near-vertical fall makes local up almost parallel to the velocity, where the
                // exclusion collapses to nothing and normalising it is garbage. The vehicle's own top
                // vector is horizontal exactly when the velocity is vertical, which is the case that
                // needs rescuing. This is the same degeneracy that spun the booster at 64 deg/s, and
                // the same answer.
                QuaternionD rot = (QuaternionD)(ship.ReferenceTransform.rotation
                                                * Quaternion.Euler(-90f, 0f, 0f));
                upRaw = Vector3d.Exclude(vn, rot * Vector3d.up);
            }
            Vector3d upC = upRaw.normalized;
            Vector3d rt = Vector3d.Cross(upC, vn).normalized;

            VerticalCmd = 0.0;
            LateralCmd = 0.0;

            Predict();
            bool canSteer = EntryGuidance.CanSteer(ship.dynamicPressurekPa);

            if (haveImpact && canSteer)
            {
                double along, cross, miss;
                Orbital.DownCross(ship.mainBody.Radius,
                                  ship.latitude, ship.longitude,
                                  impactLat, impactLon,
                                  TargetLatDeg, TargetLonDeg,
                                  out along, out cross, out miss);
                AlongTrackM = along; CrossTrackM = cross; MissM = miss;

                EntryGuideInputs s = new EntryGuideInputs();
                s.AltitudeM = alt;
                s.DownrangeErrM = along;
                s.CrossTrackM = cross;
                s.MissM = miss;
                s.LzRangeM = Orbital.GroundRange(ship.mainBody.Radius, ship.latitude, ship.longitude,
                                                 TargetLatDeg, TargetLonDeg);
                s.LzBearingDeg = Orbital.Bearing(ship.latitude, ship.longitude,
                                                 TargetLatDeg, TargetLonDeg);
                s.TrackBearingDeg = Orbital.Bearing(ship.latitude, ship.longitude,
                                                    impactLat, impactLon);
                s.DtS = (lastGuideAt > 0.0) ? now - lastGuideAt : 0.0;
                lastGuideAt = now;

                EntryGuideCommand c = EntryGuidance.Update(s, ref mem);
                VerticalCmd = c.VerticalCmd;
                LateralCmd = c.LateralCmd;
                WantLongM = c.WantLongM;
                BelowProfile = c.BelowProfile;

                string word = (along < 0.0) ? "LONG" : "SHORT";
                Note = "LIFTING ENTRY - " + word + " " + (Math.Abs(along) / 1000.0).ToString("F1")
                     + " km, want long " + (c.WantLongM / 1000.0).ToString("F2")
                     + " km, lift " + c.VerticalCmd.ToString("F2") + " / "
                     + c.LateralCmd.ToString("F2");
            }
            else
            {
                Note = canSteer
                     ? "ENTRY - no impact prediction, holding retrograde"
                     : "ENTRY - shield forward, too thin to steer ("
                       + (alt / 1000.0).ToString("F1") + " km)";
            }

            // ---- two scalars into an attitude ----
            Vector3d lift = (upC * (VerticalCmd * EntryGuidance.PitchSign))
                          + (rt * (LateralCmd * EntryGuidance.YawSign));
            if (lift.magnitude > 0.001)
            {
                AoaCmdDeg = EntryGuidance.AoaCommandDeg(VerticalCmd, LateralCmd);
                Vector3d fwd = -vn + (Math.Tan(AoaCmdDeg * Math.PI / 180.0) * lift.normalized);
                AttitudeController.Ascent.SteerTo(ship, fwd, upC);
            }
            else
            {
                AoaCmdDeg = 0.0;
                AttitudeController.Ascent.SteerTo(ship, -vn, upC);
            }
        }

        /// <summary>Entry guidance is finished. Say how it went, then pick a landing method.</summary>
        private static void Handover()
        {
            AttitudeController.Ascent.UllageFore = 0.0;

            // ⛔ THE OPEN-LOOP SIGNATURE. A run that never commanded any shortening had nothing it
            // could do: the aim was too short and the miss is all aim error, not guidance error. F9I's
            // calibration history keeps calling this out by eye; say it instead.
            if (EntryGuidance.FlewOpenLoop(mem))
            {
                double raise = mem.WorstErrorM / Deorbit.AimGain;
                Debug.LogWarning(Tag + "ENTRY FLEW OPEN LOOP - the lift command never went negative "
                                 + "(worst deficit " + (mem.WorstErrorM / 1000.0).ToString("F1")
                                 + " km). Raise the aim for this mode by about "
                                 + raise.ToString("F0") + " m: " + Aim().ToString("F0") + " -> "
                                 + (Aim() + raise).ToString("F0") + ".");
            }
            else
            {
                Debug.Log(Tag + "entry loop CLOSED - most shortening commanded "
                          + mem.LiftMin.ToString("F2") + ". Predicted miss "
                          + (MissM >= 0.0 ? MissM.ToString("F0") + " m" : "unknown"));
            }

            string why;
            Method = Terminal.Choose(PropulsiveRequested,
                                     PodEngines.Present(ship), Mono(ship), out why);
            if (PropulsiveRequested && Method == LandingMethod.Parachute)
                Debug.LogWarning(Tag + "propulsive landing unavailable (" + why + ") - parachutes");
            Debug.Log(Tag + "terminal descent: " + Method);
            Go(EntryStage.AwaitDrogue);
        }

        // ------------------------------------------------------------------ E8

        private static void AwaitDrogue()
        {
            HoldRetrograde();
            double alt = ship.radarAltitude;
            Note = "TERMINAL - awaiting the drogue window, " + alt.ToString("F0") + " m, "
                 + ship.srfSpeed.ToString("F0") + " m/s";

            if (!Terminal.DrogueReady(ship.srfSpeed, alt, Method)) return;

            DroguesDeployed = Deploy(new PartMatch(VehicleParts.IsDrogues)) > 0;
            Debug.Log(Tag + "drogues deployed at " + alt.ToString("F0") + " m, "
                      + ship.srfSpeed.ToString("F0") + " m/s");

            if (Method == LandingMethod.Propulsive)
            {
                Go(EntryStage.ArmingEngines);
                return;
            }
            ship.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
            Go(EntryStage.DroguesOut);
        }

        private static void UnderDrogues()
        {
            double alt = ship.radarAltitude;
            Note = "DROGUES OUT - " + alt.ToString("F0") + " m, " + ship.srfSpeed.ToString("F0") + " m/s";
            if (!Terminal.MainsReady(alt)) return;
            Mains("planned chute landing");
        }

        /// <summary>
        /// Light the SuperDracos UNDER the drogues, and do not cut anything until they answer.
        ///
        /// Trap 4. If they will not light, the chutes stay out and this becomes a parachute landing -
        /// slower than planned and entirely survivable, which cutting first would not have been.
        /// </summary>
        private static void ArmEngines(double inStage)
        {
            HoldRetrograde();
            double trueRadar = Terminal.TrueRadarM(ship.radarAltitude);
            double stop = StopDistance();

            if (inStage < EngineArmDelayS)
            {
                Note = "DROGUES OUT - settling before ignition";
                return;
            }
            if (!enginesCommanded)
            {
                PodEngines.On(ship);
                enginesCommanded = true;
                Note = "LIGHTING SUPERDRACOS";
                return;
            }

            Note = "ENGINES ARMED - " + trueRadar.ToString("F0") + " m, need "
                 + stop.ToString("F0") + " m, " + ship.verticalSpeed.ToString("F1") + " m/s";

            if (!Terminal.ArmGate(trueRadar, stop)) return;

            if (!PodEngines.Available(ship))
            {
                // Abandon. Chutes STAY OUT.
                Debug.LogError(Tag + "PROPULSIVE ABANDONED at the chute-cut point - no thrust. "
                               + "The drogues stay out and this becomes a parachute landing.");
                Mains("propulsive abandoned - no thrust");
                return;
            }

            Cut(new PartMatch(VehicleParts.IsDrogues));
            Debug.Log(Tag + "chutes cut at " + trueRadar.ToString("F0") + " m, "
                      + PodEngines.ThrustKn(ship).ToString("F0") + " kN available");
            Go(EntryStage.Committed);
        }

        private static void Commit()
        {
            HoldRetrograde();
            double trueRadar = Terminal.TrueRadarM(ship.radarAltitude);
            double stop = StopDistance();
            Note = "CHUTES CUT - " + trueRadar.ToString("F0") + " m, need " + stop.ToString("F0") + " m";
            if (Terminal.BurnGate(trueRadar, stop)) Go(EntryStage.LandingBurn);
        }

        private static void Burn(double inStage)
        {
            HoldRetrograde();
            double trueRadar = Terminal.TrueRadarM(ship.radarAltitude);
            double stop = StopDistance();

            ThrottleCmd = Terminal.HoverHandover(ship.verticalSpeed)
                        ? Terminal.HoverThrottle(ship.GetTotalMass(), Gravity(), PodEngines.ThrustKn(ship))
                        : Terminal.LandingThrottle(trueRadar, stop);
            AttitudeController.Ascent.Throttle = ThrottleCmd;

            Note = "SUPERDRACO LANDING BURN - " + trueRadar.ToString("F0") + " m, "
                 + ship.verticalSpeed.ToString("F1") + " m/s, thr "
                 + (ThrottleCmd * 100.0).ToString("F0") + "%";

            if (inStage > Terminal.TouchdownTimeoutS)
            {
                Debug.LogWarning(Tag + "landing burn timed out at " + trueRadar.ToString("F0")
                                 + " m - shutting down");
                AttitudeController.Ascent.Throttle = 0.0;
                PodEngines.Off(ship);
                Stage = EntryStage.Failed;
                Engaged = false;
            }
        }

        private static void UnderMains()
        {
            Note = "MAINS OUT - " + ship.radarAltitude.ToString("F0") + " m, "
                 + ship.verticalSpeed.ToString("F1") + " m/s";
        }

        private static void Mains(string why)
        {
            MainsDeployed = Deploy(new PartMatch(VehicleParts.IsMains)) > 0;
            ship.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
            AttitudeController.Ascent.Throttle = 0.0;
            Debug.Log(Tag + "mains deployed at " + ship.radarAltitude.ToString("F0") + " m - " + why);
            Go(EntryStage.MainsOut);
        }

        /// <summary>
        /// Trap 5: gear AFTER touchdown, and only on land.
        ///
        /// The gear is in the heat shield. Out early it is drag in the airflow and, on a propulsive
        /// landing, it is in the plume. On a splashdown there is nothing to stand on and legs only risk
        /// breaking the capsule up on impact.
        /// </summary>
        private static void Touchdown()
        {
            AttitudeController.Ascent.Throttle = 0.0;
            PodEngines.Off(ship);
            bool splashed = ship.situation == Vessel.Situations.SPLASHED;

            if (!splashed)
            {
                int n = DoEvent(new PartMatch(VehicleParts.IsHeatShield), "deploy gear");
                if (n == 0) Debug.LogWarning(Tag + "no 'deploy gear' event found on the heat shield");
            }

            double miss = Orbital.GroundRange(ship.mainBody.Radius, ship.latitude, ship.longitude,
                                              TargetLatDeg, TargetLonDeg);
            Note = (splashed ? "SPLASHDOWN" : "LANDED") + " - " + miss.ToString("F0") + " m from target";
            Debug.Log(Tag + "DRAGON RECOVERED: " + Note);
            Stage = EntryStage.Down;
            Engaged = false;
            AttitudeController.Ascent.Release(ship);
        }

        // ------------------------------------------------------------------ helpers

        private static bool Landed()
        {
            return ship.situation == Vessel.Situations.LANDED
                || ship.situation == Vessel.Situations.SPLASHED;
        }

        private static void HoldRetrograde()
        {
            Vector3d v = ship.srf_velocity;
            if (v.sqrMagnitude < 1.0) return;
            AttitudeController.Ascent.SteerTo(ship, -v.normalized, Vector3d.zero);
        }

        private static double Gravity()
        {
            CelestialBody b = ship.mainBody;
            double r = (ship.CoM - b.position).magnitude;
            if (r < 1.0) return 9.81;
            return b.gravParameter / (r * r);
        }

        private static double StopDistance()
        {
            double decel = Terminal.MaxDecelMps2(PodEngines.ThrustKn(ship),
                                                 ship.GetTotalMass(), Gravity());
            return Terminal.StopDistanceM(ship.verticalSpeed, decel);
        }

        /// <summary>Rate-limited impact prediction. See <see cref="PredictIntervalS"/>.</summary>
        private static void Predict()
        {
            double now = Planetarium.GetUniversalTime();
            if (now - lastPredictAt < PredictIntervalS) return;
            lastPredictAt = now;

            Impact im = ImpactPredictor.Predict(ship);
            haveImpact = im.Valid;
            if (im.Valid) { impactLat = im.LatDeg; impactLon = im.LonDeg; }
        }

        /// <summary>The aim this vehicle's de-orbit mode was fitted with, metres past the target.</summary>
        private static double Aim()
        {
            DeorbitInputs s = new DeorbitInputs();
            s.Valid = true;
            s.OnDraco = !HasPart(new PartMatch(VehicleParts.IsSecondStage));
            s.Crewed = PodEngines.Present(ship);
            return Deorbit.AimRange(s);
        }

        private static double Reserve()
        {
            DeorbitInputs s = new DeorbitInputs();
            s.Valid = true;
            s.ChuteLanding = !(PropulsiveRequested && PodEngines.Present(ship));
            return Deorbit.MonoReserve(s);
        }

        private static double Mono(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
                for (int k = 0; k < v.parts[i].Resources.Count; k++)
                    if (v.parts[i].Resources[k].resourceName == "MonoPropellant")
                        t += v.parts[i].Resources[k].amount;
            return t;
        }

        // ---- part plumbing ----
        // A named delegate rather than a lambda: the build is C# 5 and method-group conversion is the
        // idiom the rest of the plugin already uses for this.
        private delegate bool PartMatch(string partName);

        private static bool HasPart(PartMatch m)
        {
            if (ship == null) return false;
            for (int i = 0; i < ship.parts.Count; i++)
                if (m(ship.parts[i].name)) return true;
            return false;
        }

        private static int Decouple(PartMatch m)
        {
            int n = DoEvent(m, "decouple");
            if (n == 0) n = DoEvent(m, "decouple node");
            return n;
        }

        private static int Deploy(PartMatch m) { return DoEvent(m, "deploy chute"); }
        private static int Cut(PartMatch m) { return DoEvent(m, "cut chute"); }

        /// <summary>
        /// Fire a named part event on every matching part.
        ///
        /// Matched on the event's GUI NAME rather than a module type, because the modules differ:
        /// the trunk is a `ModuleTundraDecoupler` and the Dragon decoupler a stock `ModuleDecouple`,
        /// and both answer to "decouple". `falcon-detect-by-capability` again - ask what it can do.
        /// </summary>
        private static int DoEvent(PartMatch m, string eventName)
        {
            if (ship == null) return 0;
            int n = 0;
            for (int i = 0; i < ship.parts.Count; i++)
            {
                Part p = ship.parts[i];
                if (!m(p.name)) continue;
                for (int mod = 0; mod < p.Modules.Count; mod++)
                {
                    PartModule pm = p.Modules[mod];
                    for (int e = 0; e < pm.Events.Count; e++)
                    {
                        BaseEvent ev = pm.Events[e];
                        if (ev == null || ev.guiName == null) continue;
                        if (!string.Equals(ev.guiName, eventName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!ev.active) continue;
                        ev.Invoke();
                        n++;
                    }
                }
            }
            return n;
        }
    }
}
