// DragonScreen - EntryOps
// ---- ⛔ FIVE THINGS HERE ARE COUNTER-INTUITIVE, AND EACH ONE IS A FLIGHT ----
// ---- ⚠ WHAT IS DELIBERATELY NOT HERE ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public enum EntryStage : byte
    {
        Idle = 0,
        Separating,
        CoastToInterface,
        Trimming,
        LiftingEntry,
        AwaitDrogue,
        DroguesOut,
        ArmingEngines,
        Committed,
        LandingBurn,
        MainsOut,
        Down,
        Failed
    }

    public static class EntryOps
    {
        private const string Tag = "[DragonScreen] ";

        public const double PredictIntervalS = 0.5;

        public const double SepAlignedDeg = 25.0;
        public const double SepAlignMaxS = 20.0;
        public const double SepSettleS = 1.5;

        public const double TrimStartAboveM = 20000.0;
        public const double TrimQCutKpa = 0.30;

        public const double TrimScaleM = 5000.0;
        public const double TrimDeadbandM = 200.0;

        [Tunable] public static double AlongBiasM = 1700.0;

        [Tunable] public static double CrossBiasM = 310.0;

        public const double EntryHoldM = 10000.0;

        public const double EngineArmDelayS = 2.0;

        // ---- state ----
        public static EntryStage Stage { get; private set; }
        public static bool Engaged { get; private set; }
        public static string Note = "-";

        public static double TargetLatDeg = LandingSites.Lz1.LatDeg;
        public static double TargetLonDeg = LandingSites.Lz1.LonDeg;

        public static bool PropulsiveRequested = false;

        public static bool DroguesDeployed, MainsDeployed;
        private static bool gearDeployed;

        private static bool noPredictionReported;

        private static bool ballisticHold;

        public static double VerticalCmd, LateralCmd, AoaCmdDeg;
        public static double AlongTrackM, CrossTrackM, MissM = -1.0, WantLongM;
        public static double TrimErrorM, ThrottleCmd;
        public static bool BelowProfile;
        public static LandingMethod Method = LandingMethod.Parachute;

        [Tunable] public static bool PassiveComEntry = true;

        [Tunable] public static bool SteeringTest = false;
        [Tunable] public static double SweepSegmentS = 20.0;
        [Tunable] public static double SweepBackoffTempFrac = 0.92;
        [Tunable] public static double SweepBackoffAblatorFrac = 0.15;
        public static int SweepSegment = -1;
        private static double sweepStartAt = -1.0;

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
            haveImpact = false; enginesCommanded = false; ballisticHold = false;
            DroguesDeployed = false; MainsDeployed = false; gearDeployed = false;
            sepFiredAt = 0.0;
            MissM = -1.0; VerticalCmd = 0.0; LateralCmd = 0.0; AoaCmdDeg = 0.0;
            lastPredictAt = 0.0; lastGuideAt = 0.0;
            AttitudeController.Ascent.Throttle = 0.0;
            if (!v.ActionGroups[KSPActionGroup.RCS]) v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            DockShroud.Close(v);

            bool stacked = HasPart(new PartMatch(VehicleParts.IsTrunk))
                        || HasPart(new PartMatch(VehicleParts.IsSecondStage));
            Go(stacked ? EntryStage.Separating : EntryStage.CoastToInterface);
            Debug.Log(Tag + "entry sequence engaged - " + (stacked ? "stack still attached" : "clean capsule")
                      + ", target " + TargetLatDeg.ToString("F4") + ", " + TargetLonDeg.ToString("F4"));

            // ---- ⛔ VALIDATE THE KNOWN-BC PREDICTOR BEFORE IT MOVES ANYTHING (2026-08-18). ----
            if (v.mainBody != null)
            {
                Impact pk = ImpactPredictor.Predict(v, EntryGuidance.CapsuleBcKgM2);
                if (pk.Valid)
                    Debug.Log(Tag + "known-bc(" + EntryGuidance.CapsuleBcKgM2.ToString("F0")
                              + ") drag-aware landing prediction: " + pk.LatDeg.ToString("F3") + ", "
                              + pk.LonDeg.ToString("F3") + " = "
                              + (BoosterRecovery.GroundRange(v.mainBody, pk.LatDeg, pk.LonDeg,
                                     TargetLatDeg, TargetLonDeg) / 1000.0).ToString("F1")
                              + " km from target - COMPARE to the actual landing miss.");
                else
                    Debug.Log(Tag + "known-bc predictor: no answer yet (" + pk.Note + ")");
            }
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
            haveImpact = false; enginesCommanded = false; sepFiredAt = 0.0; ballisticHold = false;
            DroguesDeployed = false; MainsDeployed = false; gearDeployed = false;
            VerticalCmd = 0.0; LateralCmd = 0.0; AoaCmdDeg = 0.0; ThrottleCmd = 0.0;
            AlongTrackM = 0.0; CrossTrackM = 0.0; MissM = -1.0; WantLongM = 0.0;
            TrimErrorM = 0.0; BelowProfile = false;
            sweepStartAt = -1.0; SweepSegment = -1;
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

            CapsuleRcs.Set(ship, CapsuleRcs.AttitudePct);

            double now = Planetarium.GetUniversalTime();
            double inStage = now - stageStartedAt;

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
                    if (n == 0)
                        Debug.LogError(Tag + "trunk jettison found NO decoupler to fire - the trunk "
                                     + "part is attached but exposes neither a 'decouple' event nor "
                                     + "a 'decouple' action");
                }
                sepFiredAt = now;
                return;
            }

            if (now - sepFiredAt < SepSettleS) { Note = "SEPARATION - clearing"; return; }

            if (HasPart(new PartMatch(VehicleParts.IsTrunk)))
                Debug.LogError(Tag + "TRUNK WILL NOT JETTISON - it is still attached "
                             + SepSettleS.ToString("F1") + " s after the decouple command. The heat "
                             + "shield cannot be held forward of a trunk.");

            if (HasPart(new PartMatch(VehicleParts.IsSecondStage)))
            {
                Debug.LogWarning(Tag + "second stage still attached after trunk jettison - "
                                 + "using the Dragon decoupler");
                Decouple(new PartMatch(VehicleParts.IsDragonDecoupler));
            }

            sepFiredAt = 0.0;
            Go(EntryStage.CoastToInterface);
        }

        private static void Coast()
        {
            HoldRetrograde();
            double atm = ship.mainBody.atmosphereDepth;
            double alt = ship.radarAltitude;
            Note = "COAST - shield forward, " + (alt / 1000.0).ToString("F1") + " km";

            if (alt < atm + TrimStartAboveM) Go(EntryStage.Trimming);
        }

        private static void Trim()
        {
            HoldRetrograde();
            double atm = ship.mainBody.atmosphereDepth;
            double alt = ship.radarAltitude;

            if (ship.dynamicPressurekPa > TrimQCutKpa)
            {
                Neutral();
                Debug.Log(Tag + "course correction finished at " + (alt / 1000.0).ToString("F1")
                          + " km (q " + ship.dynamicPressurekPa.ToString("F2") + " kPa) - predicted miss "
                          + (MissM >= 0.0 ? MissM.ToString("F0") + " m" : "unknown")
                          + ", mono " + Mono(ship).ToString("F1"));
                Go(EntryStage.LiftingEntry);
                return;
            }

            // ---- ⛔ MID-COURSE RCS TRIM: PUT THE PREDICTED IMPACT DIRECTLY ON THE LZ, BOTH AXES. ----
            Predict(EntryGuidance.CapsuleBcKgM2);
            if (!haveImpact)
            {
                Neutral();
                Note = "TRIM - no impact prediction yet, " + (alt / 1000.0).ToString("F1") + " km";
                return;
            }

            CelestialBody b = ship.mainBody;
            double along, cross, missM;
            Orbital.DownCross(b.Radius, ship.latitude, ship.longitude, impactLat, impactLon,
                              TargetLatDeg, TargetLonDeg, out along, out cross, out missM);
            AlongTrackM = along; CrossTrackM = cross; MissM = missM; TrimErrorM = -along;

            // ---- ⛔ TWO-WAY RCS COURSE CORRECTION, AIMED A HAIR DOWNRANGE (2026-08-20, MechJeb-style). ----
            Vector3d up = (ship.CoM - b.position).normalized;
            Vector3d impactPos = (Vector3d)b.GetWorldSurfacePosition(impactLat, impactLon, 0.0) - b.position;
            Vector3d lzPos = (Vector3d)b.GetWorldSurfacePosition(TargetLatDeg, TargetLonDeg, 0.0) - b.position;
            Vector3d downrange = Vector3d.Exclude(up, ship.srf_velocity);
            if (downrange.sqrMagnitude > 1e-6) lzPos += AlongBiasM * downrange.normalized;
            Vector3d northHoriz = Vector3d.Exclude(up, b.angularVelocity);
            if (northHoriz.sqrMagnitude > 1e-6) lzPos += CrossBiasM * northHoriz.normalized;
            Vector3d toAim = Vector3d.Exclude(up, lzPos - impactPos);
            double missToAim = toAim.magnitude;

            if (Mono(ship) <= Reserve() || missToAim < TrimDeadbandM)
            {
                Neutral();
                Note = (missToAim < TrimDeadbandM ? "CORRECTION - ON TARGET, " : "CORRECTION - reserve held, ")
                     + missM.ToString("F0") + " m to LZ, " + (alt / 1000.0).ToString("F1") + " km";
                return;
            }

            if (toAim.sqrMagnitude < 1e-6) { Neutral(); return; }
            Vector3d dir = toAim.normalized;

            double frac = missToAim / TrimScaleM;
            if (frac > 1.0) frac = 1.0;

            Transform rt = ship.ReferenceTransform;
            AttitudeController.Ascent.UllageFore = Vector3d.Dot(dir, (Vector3d)rt.up) * frac;
            AttitudeController.Ascent.TranslateX = Vector3d.Dot(dir, (Vector3d)rt.right) * frac;
            AttitudeController.Ascent.TranslateY = Vector3d.Dot(dir, -(Vector3d)rt.forward) * frac;

            Note = "COURSE CORRECTION - " + (missM / 1000.0).ToString("F2") + " km ("
                 + (Math.Abs(along) / 1000.0).ToString("F1") + (along >= 0.0 ? " short" : " long") + ", "
                 + (Math.Abs(cross) / 1000.0).ToString("F1") + " cross), " + (alt / 1000.0).ToString("F1") + " km";
        }

        private static void Neutral()
        {
            AttitudeController.Ascent.UllageFore = 0.0;
            AttitudeController.Ascent.TranslateX = 0.0;
            AttitudeController.Ascent.TranslateY = 0.0;
        }

        // ------------------------------------------------------------------ E7

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
                QuaternionD rot = (QuaternionD)(ship.ReferenceTransform.rotation
                                                * Quaternion.Euler(-90f, 0f, 0f));
                upRaw = Vector3d.Exclude(vn, rot * Vector3d.up);
            }
            Vector3d upC = upRaw.normalized;
            Vector3d rt = Vector3d.Cross(upC, vn).normalized;

            VerticalCmd = 0.0;
            LateralCmd = 0.0;

            Predict(EntryGuidance.CapsuleBcKgM2);
            bool canSteer = EntryGuidance.CanSteer(ship.dynamicPressurekPa);

            // ---- ⛔ STEERING-LIMITS TEST: sweep the pitch/yaw envelope instead of aiming (SteeringTest). ----
            if (SteeringTest && canSteer)
            {
                if (haveImpact)
                {
                    double sa, sc, sm;
                    Orbital.DownCross(ship.mainBody.Radius, ship.latitude, ship.longitude,
                                      impactLat, impactLon, TargetLatDeg, TargetLonDeg, out sa, out sc, out sm);
                    AlongTrackM = sa; CrossTrackM = sc; MissM = sm;
                }
                SteeringSweep(alt, now);
            }
            else if (haveImpact && canSteer)
            {
                noPredictionReported = false;

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

                // ---- ⛔ THE BURN NAILED IT - HOLD BALLISTIC, DO NOT LIFT-STEER. (user, 2026-08-20) ----
                if (miss < EntryHoldM) ballisticHold = true;
                if (ballisticHold) { VerticalCmd = 0.0; LateralCmd = 0.0; }

                string word = (along < 0.0) ? "LONG" : "SHORT";
                Note = ballisticHold
                     ? "BALLISTIC HOLD - burn on target, " + miss.ToString("F0") + " m, "
                       + (alt / 1000.0).ToString("F1") + " km"
                     : "LIFTING ENTRY - " + word + " " + (Math.Abs(along) / 1000.0).ToString("F1")
                       + " km, want long " + (c.WantLongM / 1000.0).ToString("F2")
                       + " km, lift " + c.VerticalCmd.ToString("F2") + " / "
                       + c.LateralCmd.ToString("F2");
            }
            else
            {
                // ---- ⛔ NO PREDICTION IN STEERABLE AIR MEANS NO STEERING AT ALL. SAY SO. ----
                if (canSteer && !noPredictionReported)
                {
                    noPredictionReported = true;
                    Debug.LogError(Tag + "ENTRY IS NOT STEERING - q is "
                        + ship.dynamicPressurekPa.ToString("F2") + " kPa, which is enough to fly "
                        + "with, but the impact predictor has no solution. The capsule is "
                        + "BALLISTIC from here and the de-orbit aim is the only thing setting "
                        + "where it lands. Check r_bcAscent - a zero ballistic coefficient means "
                        + "drag was never measured.");
                }
                Note = canSteer
                     ? "ENTRY - NOT STEERING, no impact prediction"
                     : "ENTRY - shield forward, too thin to steer ("
                       + (alt / 1000.0).ToString("F1") + " km)";
            }

            // ---- ⛔ THE LANDING RESERVE OUTRANKS THE RANGE HERE TOO. ----
            if (Mono(ship) <= Reserve())
            {
                if (VerticalCmd != 0.0 || LateralCmd != 0.0)
                    Note += "  RESERVE - lift stowed, " + Mono(ship).ToString("F1") + " units left";
                VerticalCmd = 0.0;
                LateralCmd = 0.0;
            }

            // ---- two scalars into an attitude ----
            Vector3d lift = (upC * (VerticalCmd * EntryGuidance.PitchSign))
                          + (rt * (LateralCmd * EntryGuidance.YawSign));

            // ---- ⛔ REAL PASSIVE-CoM LIFTING ENTRY (opt-in, see PassiveComEntry). ----
            if (PassiveComEntry)
            {
                VehicleControl.SetDescentMode(ship, true);
                VehicleControl.SetComOffset(ship, EntryGuidance.LiftFraction(VerticalCmd, LateralCmd));
            }

            if (lift.magnitude > 0.001)
            {
                Vector3d liftDir = lift.normalized;
                AoaCmdDeg = EntryGuidance.AoaCommandDeg(VerticalCmd, LateralCmd);
                Vector3d fwd = -vn + (Math.Tan(AoaCmdDeg * Math.PI / 180.0) * liftDir);
                AttitudeController.Ascent.SteerTo(ship, fwd, liftDir);
            }
            else
            {
                AoaCmdDeg = 0.0;
                AttitudeController.Ascent.SteerTo(ship, -vn, upC);
            }
        }

        private static void SteeringSweep(double alt, double now)
        {
            HeatSample h = EntryHeat.Sample(ship);
            if (h.Present && (h.ShieldTempFrac > SweepBackoffTempFrac || h.AblatorFrac < SweepBackoffAblatorFrac))
            {
                VerticalCmd = 0.0; LateralCmd = 0.0; SweepSegment = -1;
                Note = "STEERING TEST - BACKED OFF at the heat limit (shield "
                     + (h.ShieldTempFrac * 100.0).ToString("F0") + "% / ablator "
                     + (h.AblatorFrac * 100.0).ToString("F0") + "%), " + (alt / 1000.0).ToString("F1") + " km";
                return;
            }

            if (sweepStartAt <= 0.0) sweepStartAt = now;
            double seg = SweepSegmentS > 1.0 ? SweepSegmentS : 1.0;
            int s = ((int)((now - sweepStartAt) / seg)) % 4;
            SweepSegment = s;
            switch (s)
            {
                case 0: VerticalCmd = -1.0; LateralCmd =  0.0; break;
                case 1: VerticalCmd =  0.0; LateralCmd =  1.0; break;
                case 2: VerticalCmd =  0.0; LateralCmd = -1.0; break;
                default: VerticalCmd = 0.0; LateralCmd =  0.0; break;
            }
            Note = "STEERING TEST seg " + s + "  pitch " + VerticalCmd.ToString("F1")
                 + " / yaw " + LateralCmd.ToString("F1") + "  " + (alt / 1000.0).ToString("F1")
                 + " km  q " + ship.dynamicPressurekPa.ToString("F2") + " kPa";
        }

        private static void Handover()
        {
            AttitudeController.Ascent.UllageFore = 0.0;

            if (PassiveComEntry && ship != null)
            {
                VehicleControl.SetDescentMode(ship, false);
                VehicleControl.SetComOffset(ship, 0.0);
            }

            if (EntryGuidance.FlewOpenLoop(mem))
            {
                // ---- ⛔ CALIBRATE ON WHERE IT LANDED, NOT ON THE WORST TRANSIENT. ----
                double settled = (MissM >= 0.0) ? MissM : mem.WorstErrorM;
                double raise = settled / Deorbit.AimGain;
                Debug.LogWarning(Tag + "ENTRY FLEW OPEN LOOP - the lift command never went negative, "
                                 + "so it was short the whole way and this miss is AIM error, not "
                                 + "guidance error. Settled miss " + (settled / 1000.0).ToString("F1")
                                 + " km (worst lead-compensated transient was "
                                 + (mem.WorstErrorM / 1000.0).ToString("F1")
                                 + " km - do NOT calibrate on that). Raise the aim for this mode by "
                                 + "about " + raise.ToString("F0") + " m: " + Aim().ToString("F0")
                                 + " -> " + (Aim() + raise).ToString("F0") + ".");
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

            // ---- ⛔ FULLY PROPULSIVE: NO CHUTES (user 2026-08-21) ----
            if (Method == LandingMethod.Propulsive)
            {
                Go(EntryStage.ArmingEngines);
                return;
            }

            DroguesDeployed = Deploy(new PartMatch(VehicleParts.IsDrogues)) > 0;
            Debug.Log(Tag + "drogues deployed at " + alt.ToString("F0") + " m, "
                      + ship.srfSpeed.ToString("F0") + " m/s");
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
                // ---- ENGINE-FAILURE ABORT ----
                Debug.LogError(Tag + "PROPULSIVE ABANDONED at " + trueRadar.ToString("F0")
                               + " m - no thrust. Deploying chutes as an abort.");
                DroguesDeployed = Deploy(new PartMatch(VehicleParts.IsDrogues)) > 0;
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
                Go(EntryStage.DroguesOut);
                return;
            }

            Cut(new PartMatch(VehicleParts.IsDrogues));
            Debug.Log(Tag + "engines proven lit at " + trueRadar.ToString("F0") + " m, "
                      + PodEngines.ThrustKn(ship).ToString("F0") + " kN - committing the hoverslam");
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

            if (!gearDeployed && trueRadar < Terminal.GearDeployAltM)
            {
                DoEvent(new PartMatch(VehicleParts.IsHeatShield), "deploy gear");
                gearDeployed = true;
                Debug.Log(Tag + "landing gear out at " + trueRadar.ToString("F0") + " m");
            }

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

        private static void Touchdown()
        {
            AttitudeController.Ascent.Throttle = 0.0;
            PodEngines.Off(ship);
            bool splashed = ship.situation == Vessel.Situations.SPLASHED;

            if (!splashed && !gearDeployed)
            {
                int n = DoEvent(new PartMatch(VehicleParts.IsHeatShield), "deploy gear");
                if (n == 0) Debug.LogWarning(Tag + "no 'deploy gear' event found on the heat shield");
                gearDeployed = n > 0;
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

        private static void Predict() { Predict(0.0); }

        private static void Predict(double bcOverride)
        {
            double now = Planetarium.GetUniversalTime();
            if (now - lastPredictAt < PredictIntervalS) return;
            lastPredictAt = now;

            Impact im = ImpactPredictor.Predict(ship, bcOverride);
            haveImpact = im.Valid;
            if (im.Valid) { impactLat = im.LatDeg; impactLon = im.LonDeg; }
        }

        private static double Aim()
        {
            DeorbitInputs s = new DeorbitInputs();
            s.Valid = true;
            s.OnDraco = !HasPart(new PartMatch(VehicleParts.IsSecondStage));
            s.Crewed = PodEngines.Present(ship);
            s.OrbitAltM = (ship != null && ship.orbit != null)
                          ? ship.orbit.ApA : Deorbit.AimFitAltM;
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
            return DockedSide.Mono(v);
        }

        // ---- part plumbing ----
        private delegate bool PartMatch(string partName);

        private static bool HasPart(PartMatch m)
        {
            if (ship == null) return false;
            List<Part> ours = DockedSide.Ours(ship);
            for (int i = 0; i < ours.Count; i++)
                if (m(ours[i].name)) return true;
            return false;
        }

        private static int Decouple(PartMatch m)
        {
            int n = DoEvent(m, "decouple");
            if (n == 0) n = DoEvent(m, "decouple node");
            return n;
        }

        private static int Deploy(PartMatch m) { return DoEvent(m, "deploy chute"); }
        private static int Cut(PartMatch m) { return DoEvent(m, "cut main chute"); }

        /// ---- ⛔ AN INACTIVE EVENT IS NOT A MISSING ONE, AND THE ACTION WORKS EITHER WAY. ----
        private static int DoEvent(PartMatch m, string eventName)
        {
            if (ship == null) return 0;
            int n = 0, inactive = 0;
            List<Part> ours = DockedSide.Ours(ship);
            for (int i = 0; i < ours.Count; i++)
            {
                Part p = ours[i];
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
                        if (!ev.active) { inactive++; continue; }
                        ev.Invoke();
                        n++;
                    }
                }
            }
            if (n > 0) return n;
            if (inactive > 0)
                Debug.LogWarning(Tag + "'" + eventName + "' matched " + inactive + " event(s) but "
                               + "every one of them was inactive - trying the action instead");
            return DoAction(m, eventName);
        }

        private static int DoAction(PartMatch m, string actionName)
        {
            if (ship == null) return 0;
            int n = 0;
            List<Part> ours = DockedSide.Ours(ship);
            for (int i = 0; i < ours.Count; i++)
            {
                Part p = ours[i];
                if (!m(p.name)) continue;
                for (int mod = 0; mod < p.Modules.Count; mod++)
                {
                    PartModule pm = p.Modules[mod];
                    for (int a = 0; a < pm.Actions.Count; a++)
                    {
                        BaseAction ac = pm.Actions[a];
                        if (ac == null || ac.guiName == null) continue;
                        if (!string.Equals(ac.guiName, actionName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        ac.Invoke(new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate));
                        n++;
                    }
                }
            }
            return n;
        }
    }
}
