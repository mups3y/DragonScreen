// DragonScreen - BoosterRecovery
// ---- THE HANDOVER IS COPIED FROM OUR OWN kOS INTERFACE, WHICH ALREADY SOLVED THIS ----
// ---- THE PHYSICS RANGE: OUR OWN PhysicsRangeExtender PORT, NO PRE DEPENDENCY ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class BoosterRecovery
    {
        private const string Tag = "[DragonScreen] ";

        // ---- RAISED TO 1500 km FOR THE 250 km TOURIST MISSION (user 2026-08-21) ----
        [Tunable] public static float RangeMetres = 1500000f;

        [Tunable] public static double BoosterRollRangeDeg = 130.0;

        // ---- RSS/RO DRONESHIP: "Of Course I Still Love You" (Crew-2's ASDS) ----
        [Tunable] public static double DroneshipEarthLatDeg = 32.787551;
        [Tunable] public static double DroneshipEarthLonDeg = -76.644507;

        public static bool Active { get; private set; }
        public static LandingPhase Phase { get; private set; }
        public static LandingCommand Command;

        private static Vessel booster, upperStage;

        private static double settleUntil;
        private static int noBoosterLooks;
        private const int NoBoosterQuietLooks = 15;

        public static bool Settling { get { return settleUntil > 0.0; } }

        public const double SettleAfterLandingS = 10.0;

        private const double EntryBurnUllageSettleS = 2.0;

        internal static Vessel Tracked { get { return booster; } }
        private static double startedAt;

        public static Vessel BoosterVessel { get { return booster; } }

        private static double phaseStartedAt;

        private static bool noBoosterReported;
        private static bool packedReported;

        // ---- FOR THE RECORDER ----
        public static double TrueRadar;
        public static double DownrangeM;
        public static double PredictedMissM;
        public static double DownMissM, CrossMissM;
        public static double RecoveryPropFrac = -1.0;
        public static double RecoveryPropUnitsNow;
        public static double InitialMissM;

        // ---- EVERYTHING BELOW IS A DECISION INPUT THE RECORDER COULD NOT SEE ----
        public static double RangeToPartnerM;
        public static double PhaseElapsedS;
        public static int OctaMode = -1;

        // ---- ⛔ DIRECT ACTUATOR AUTHORITY, the autopilot's own (VehicleControl, handles from the craft
        // corpus if a phase ever wants less (e.g. to stop fighting the airflow), never blind. ----
        [Tunable] public static double GimbalLimitPct = 100.0;
        [Tunable] public static double FinAuthorityPct = 100.0;
        public static int EnginesLit;
        public static bool GridFinsOut;

        // ---- ⛔ LANDING-BURN DIAGNOSTICS: WHY THE ENGINES LIT BUT MADE NO THRUST (recorder bl_ columns) ----
        public static double LiveThrustKn;
        public static double ColdGasFrac = -1.0;
        public static bool UllageOn;
        public static int IgniteAttempts { get { return igniteAttempts; } }
        public static double LeanFrac, AoaDeg;

        public static LandingProfile Profile = LandingProfile.Rtls;

        public static double PadLat, PadLon;
        public static bool HavePad;

        /// ---- ⛔ THIS USED TO BE `PadLat = v.latitude` AT LIFTOFF. ----
        public static void RememberPad(Vessel v)
        {
            if (v == null || HavePad) return;

            Vessel drone = FindDroneship();
            if (drone != null)
            {
                PadLat = drone.latitude; PadLon = drone.longitude; HavePad = true;
                Debug.Log(Tag + "landing zone: droneship '" + drone.vesselName + "' at "
                          + PadLat.ToString("F6") + ", " + PadLon.ToString("F6"));
                return;
            }
            PadLat = DroneshipEarthLatDeg; PadLon = DroneshipEarthLonDeg; HavePad = true;
            Debug.Log(Tag + "landing zone: droneship STATIC 'Of Course I Still Love You' at "
                      + PadLat.ToString("F6") + ", " + PadLon.ToString("F6")
                      + " (no vessel; fixed deck coordinate)");
        }

        private static Vessel FindDroneship()
        {
            List<Vessel> all = FlightGlobals.Vessels;
            Vessel byPart = null;
            for (int i = 0; i < all.Count; i++)
            {
                Vessel s = all[i];
                if (s == null || s.state == Vessel.State.DEAD) continue;
                if (s.vesselName == LandingSites.DroneshipVesselName) return s;
                if (byPart == null && IsDroneshipVessel(s)) byPart = s;
            }
            return byPart;
        }

        private static bool IsDroneshipVessel(Vessel v)
        {
            if (v.loaded)
            {
                for (int i = 0; i < v.parts.Count; i++)
                    if (VehicleParts.IsDroneship(v.parts[i].name)) return true;
                return false;
            }
            if (v.protoVessel != null)
            {
                List<ProtoPartSnapshot> pps = v.protoVessel.protoPartSnapshots;
                for (int i = 0; pps != null && i < pps.Count; i++)
                    if (VehicleParts.IsDroneship(pps[i].partName)) return true;
            }
            return false;
        }

        public static void Reset()
        {
            Active = false; Phase = LandingPhase.Idle;
            booster = null; upperStage = null; HavePad = false;
            gridFinsOut = false;
            recoveryPropStart = -1.0;
            handedOverToOne = false;
            settleUntil = 0.0;
            burnLatchedEngines = 0; lastBurnPhase = LandingPhase.Idle;
            noBoosterLooks = 0;
            phaseStartedAt = 0.0; noBoosterReported = false; packedReported = false;
            TrueRadar = 0.0; DownrangeM = 0.0; initialMiss = 0.0;
            PredictedMissM = 0.0; DownMissM = 0.0; CrossMissM = 0.0; InitialMissM = 0.0;
            RangeToPartnerM = 0.0; PhaseElapsedS = 0.0; OctaMode = -1; EnginesLit = 0;
            GridFinsOut = false; LeanFrac = 0.0; AoaDeg = 0.0;
            LiveThrustKn = 0.0; ColdGasFrac = -1.0; UllageOn = false;
            flipSeeded = false; flipComplete = false;
            rollStartedAt = 0.0; rollSettledAt = 0.0;
            slewSeeded = false;
            slewSign = 0.0;
            lastIgniteAttemptAt = 0.0; igniteAttempts = 0;
            landingBurnLitAt = 0.0;
            descentErrSeeded = false; descentErrPrev = Vector3d.zero;
            descentErrPrevUt = 0.0; descentErrRate = Vector3d.zero;
        }

        // ---- DESCENT LEAD/DAMPING (reduce the target overshoot during the guided descent) ----
        private static bool descentErrSeeded;
        private static Vector3d descentErrPrev, descentErrRate;
        private static double descentErrPrevUt;

        private static Vector3d DescentLeadError(Vector3d err)
        {
            double now = Planetarium.GetUniversalTime();
            double dt = descentErrSeeded ? now - descentErrPrevUt : 0.0;
            if (descentErrSeeded && dt > 1e-3 && dt < 1.0)
            {
                Vector3d raw = (err - descentErrPrev) / dt;
                double a = dt / (Landing.DescentLeadFilterS + dt);
                descentErrRate = descentErrRate + a * (raw - descentErrRate);
            }
            descentErrPrev = err; descentErrPrevUt = now; descentErrSeeded = true;
            return err + Landing.DescentLeadTauS * descentErrRate;
        }

        // ------------------------------------------------------------------ handover

        public static bool TryHandover(Vessel active)
        {
            if (Active || active == null) return false;

            Vessel b = FindBooster(active);
            if (b == null)
            {
                // ---- SAY SO. A HANDOVER THAT NEVER FIRES MUST NOT BE SILENT. ----
                noBoosterLooks++;
                if (!noBoosterReported && noBoosterLooks >= NoBoosterQuietLooks)
                {
                    noBoosterReported = true;
                    Debug.LogWarning(Tag + "no booster to recover - looked for a loaded, unpacked, "
                                         + "uncrewed vessel with a '" + VehicleParts.BoosterMarker
                                         + "' part and a working engine, and found none among "
                                         + FlightGlobals.VesselsLoaded.Count + " loaded vessel(s). "
                                         + "If it separated cleanly it has probably unloaded: the "
                                         + "physics range must be raised BEFORE separation.");
                }
                return false;
            }

            upperStage = active;
            booster = b;
            Active = true;
            startedAt = Planetarium.GetUniversalTime();
            phaseStartedAt = startedAt;

            SnapshotRanges(upperStage);
            Extend(upperStage);
            Extend(booster);

            SetGridFinControl(booster, false);

            Phase = Landing.InitialPhase(Read(booster));
            initialMiss = Math.Abs(PredictedMiss(booster));

            FlightGlobals.ForceSetActiveVessel(booster);

            Debug.Log(Tag + "booster recovery: focus -> '" + booster.vesselName
                      + "' at " + Landing.Name(Phase)
                      + ", upper stage '" + upperStage.vesselName + "' coasts to apoapsis. "
                      + "Physics range held at " + (RangeMetres / 1000f).ToString("F0")
                      + " km (re-applied each tick; no PRE needed).");
            return true;
        }

        /// ---- TAKE THE HEAVIEST CANDIDATE THAT CAN STILL FLY, NOT THE FIRST ONE SEEN ----
        private static Vessel FindBooster(Vessel active)
        {
            List<Vessel> all = FlightGlobals.VesselsLoaded;
            Vessel best = null;
            double bestMass = 0.0;

            for (int i = 0; i < all.Count; i++)
            {
                Vessel v = all[i];
                if (v == null || v == active || v.packed) continue;
                if (v.situation == Vessel.Situations.LANDED
                    || v.situation == Vessel.Situations.SPLASHED
                    || v.situation == Vessel.Situations.PRELAUNCH) continue;
                if (v.GetCrewCount() > 0) continue;

                bool isBooster = false;
                for (int p = 0; p < v.parts.Count && !isBooster; p++)
                    if (VehicleParts.IsBooster(v.parts[p].name)) isBooster = true;
                if (!isBooster) continue;

                if (!HasEngine(v)) continue;

                double m = v.GetTotalMass();
                if (m > bestMass) { bestMass = m; best = v; }
            }

            if (best != null)
                Debug.Log(Tag + "booster candidate '" + best.vesselName + "', "
                              + bestMass.ToString("F1") + " t");
            return best;
        }

        public static int CountLit(Vessel v)
        {
            if (v == null) return 0;
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (es[m].EngineIgnited && !es[m].flameout) n++;
            }
            return n;
        }

        private static bool HasEngine(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (!es[m].flameout) return true;
            }
            return false;
        }

        private static double ProducingThrustKn(Vessel v)
        {
            if (v == null) return 0.0;
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++) t += es[m].finalThrust;
            }
            return t;
        }

        private const double BoosterLitThrustKn = 50.0;

        public static void PrepareForSeparation(Vessel v)
        {
            if (v == null) return;
            Extend(v);
            Debug.Log(Tag + "physics range raised to " + (RangeMetres / 1000f).ToString("F0")
                          + " km on '" + v.vesselName + "' before separation (held by Tick each frame)");
        }

        private static void Extend(Vessel v)
        {
            if (v == null) return;
            try
            {
                VesselRanges r = v.vesselRanges;
                if (r == null) return;
                Set(r.flying); Set(r.subOrbital); Set(r.orbit); Set(r.escaping);
                v.vesselRanges = r;
            }
            catch (Exception e) { Debug.LogWarning(Tag + "could not extend range: " + e.Message); }
        }

        private static void Set(VesselRanges.Situation s)
        {
            if (s == null) return;
            s.load = RangeMetres;
            s.unload = RangeMetres * 1.05f;
            s.pack = RangeMetres * 1.10f;
            s.unpack = RangeMetres * 0.99f;
        }

        // ---- ⛔ THE EXTENDED RANGE IS ON ONLY WHILE FOCUS IS AWAY ON THE BOOSTER. ----
        private static float[] savedUpperRanges;

        private static void SnapshotRanges(Vessel v)
        {
            savedUpperRanges = null;
            if (v == null || v.vesselRanges == null) return;
            VesselRanges r = v.vesselRanges;
            savedUpperRanges = new float[]
            {
                r.flying.load,     r.flying.unload,     r.flying.pack,     r.flying.unpack,
                r.subOrbital.load, r.subOrbital.unload, r.subOrbital.pack, r.subOrbital.unpack,
                r.orbit.load,      r.orbit.unload,      r.orbit.pack,      r.orbit.unpack,
                r.escaping.load,   r.escaping.unload,   r.escaping.pack,   r.escaping.unpack,
            };
        }

        private static void RestoreRanges(Vessel v)
        {
            if (savedUpperRanges == null) return;
            try
            {
                if (v != null && v.vesselRanges != null)
                {
                    VesselRanges r = v.vesselRanges;
                    Put(r.flying, 0); Put(r.subOrbital, 4); Put(r.orbit, 8); Put(r.escaping, 12);
                    v.vesselRanges = r;
                    Debug.Log(Tag + "physics range restored on '" + v.vesselName
                              + "' - the extended range was only needed while the booster had focus");
                }
            }
            catch (Exception e) { Debug.LogWarning(Tag + "could not restore range: " + e.Message); }
            savedUpperRanges = null;
        }

        private static void Put(VesselRanges.Situation s, int i)
        {
            if (s == null) return;
            s.load = savedUpperRanges[i]; s.unload = savedUpperRanges[i + 1];
            s.pack = savedUpperRanges[i + 2]; s.unpack = savedUpperRanges[i + 3];
        }

        // ------------------------------------------------------------------ flying it

        public static void Tick()
        {
            if (settleUntil > 0.0)
            {
                if (Planetarium.GetUniversalTime() < settleUntil) return;
                settleUntil = 0.0;
                Debug.Log(Tag + "booster settled - releasing the camera");
                Finish("settled");
                return;
            }

            if (!Active) return;

            if (booster == null || booster.state == Vessel.State.DEAD)
            {
                Finish("booster lost");
                return;
            }

            // ---- ⛔ HOLD THE EXTENDED RANGE EVERY TICK - THIS IS THE PhysicsRangeExtender PORT. ----
            Extend(booster);
            Extend(upperStage);

            // ---- ⛔ A PACKED STAGE MUST NOT ADVANCE ITS OWN PHASE MACHINE. ----
            if (booster.packed)
            {
                if (!packedReported)
                {
                    packedReported = true;
                    Debug.LogWarning(Tag + "booster is on rails - recovery frozen at "
                                         + Landing.Name(Phase) + " until it reloads");
                }
                return;
            }
            packedReported = false;

            LandingInputs s = Read(booster);
            LandingCommand c = Landing.Guide(s, Phase);

            TrueRadar = s.AltitudeRadar;
            DownrangeM = s.DownrangeM;
            RangeToPartnerM = s.RangeToPartnerM;
            PhaseElapsedS = s.PhaseElapsedS;
            OctaMode = LitOctawebMode(booster);
            EnginesLit = CountLit(booster);
            LiveThrustKn = ProducingThrustKn(booster);
            ColdGasFrac = ColdGasFraction(booster);
            UllageOn = false;
            GridFinsOut = gridFinsOut;
            PredictedMissM = s.PredictedMissM;
            DownMissM = s.DownMissM; CrossMissM = s.CrossMissM;
            InitialMissM = s.InitialMissM;
            RecoveryPropFrac = s.RecoveryPropFrac;
            RecoveryPropUnitsNow = RecoveryPropUnits(booster);

            if (c.Phase != Phase)
            {
                Debug.Log(Tag + "booster -> " + Landing.Name(c.Phase)
                          + "  alt " + (s.AltitudeRadar / 1000.0).ToString("F1")
                          + " km, v " + s.SurfaceSpeed.ToString("F0")
                          + " m/s, downrange " + (s.DownrangeM / 1000.0).ToString("F1")
                          + " km, ignition at " + (c.IgnitionAltitude / 1000.0).ToString("F2")
                          + " km on " + c.Engines + " engine(s)");
                phaseStartedAt = Planetarium.GetUniversalTime();
            }
            Phase = c.Phase;
            Command = c;

            if (Phase == LandingPhase.Touchdown) { Finish("booster down"); return; }

            // ---- NO SOLUTION IS AN END STATE AND NOTHING TREATED IT AS ONE ----
            if (Phase == LandingPhase.NoSolution)
            {
                Debug.LogError(Tag + "BOOSTER CANNOT STOP - thrust-to-weight below 1 at "
                                   + (s.AltitudeRadar / 1000.0).ToString("F2") + " km, "
                                   + s.SurfaceSpeed.ToString("F0") + " m/s. Burning what is left.");
                Finish("no landing solution");
                return;
            }

            // ---- ⛔ THE "ONLY FLY IT WHILE FOCUSED" GUARD IS GONE, DELIBERATELY. ----

            // ---- FINS OUT AT THE TOP OF THE ARC, NOT AT THE ENTRY BURN ----
            // ---- FINS OUT ON THE DESCENT, NOT ON A PHASE ----
            if (s.VerticalSpeed <= Landing.ArcOverVs) DeployGridFins(booster);

            // ---- ⛔ ULLAGE SETTLE BEFORE EVERY RELIGHT (Real Fuels), ENTRY BURN AND LANDING BURN. ----
            if ((Phase == LandingPhase.EntryBurn || Phase == LandingPhase.LandingBurn)
                && Planetarium.GetUniversalTime() - phaseStartedAt < EntryBurnUllageSettleS)
            {
                if (!booster.ActionGroups[KSPActionGroup.RCS])
                    booster.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
                AttitudeController.Booster.UllageFore = 1.0;
                UllageOn = true;
                SetEngines(booster, 0, false);
                Aim(booster, c, s);
                AttitudeController.Booster.Throttle = 0.0;
                if (FlightGlobals.ActiveVessel == booster) FlightInputHandler.state.mainThrottle = 0f;
                return;
            }
            bool relightLit = ProducingThrustKn(booster) > BoosterLitThrustKn;
            if ((Phase == LandingPhase.EntryBurn || Phase == LandingPhase.LandingBurn) && !relightLit)
            {
                if (!booster.ActionGroups[KSPActionGroup.RCS])
                    booster.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
                AttitudeController.Booster.UllageFore = 1.0;
                UllageOn = true;
            }
            else
                AttitudeController.Booster.UllageFore = 0.0;

            SetEngines(booster, c.Engines, Landing.FiresEngine(c.Phase));
            Aim(booster, c, s);
            // ---- FULL THROTTLE THROUGH THE LANDING-BURN SPOOL. ----
            double throttle = c.Throttle;
            if (c.Phase == LandingPhase.LandingBurn)
            {
                double nowT = Planetarium.GetUniversalTime();
                if (landingBurnLitAt <= 0.0) landingBurnLitAt = nowT;
                if (nowT - landingBurnLitAt < Landing.LandingSpoolS) throttle = 1.0;
            }
            else landingBurnLitAt = 0.0;
            AttitudeController.Booster.Throttle = throttle;
            if (FlightGlobals.ActiveVessel == booster)
                FlightInputHandler.state.mainThrottle = (float)throttle;

            if (c.DeployLegs) booster.ActionGroups.SetGroup(KSPActionGroup.Gear, true);

            if (booster.ActionGroups[KSPActionGroup.RCS] != c.Rcs)
                booster.ActionGroups.SetGroup(KSPActionGroup.RCS, c.Rcs);

            // ---- ⛔ DIRECT ACTUATOR OWNERSHIP. Assert the gimbal + grid-fin authority through the parts'
            VehicleControl.SetGimbalLimit(booster, GimbalLimitPct);
            VehicleControl.SetFinAuthority(booster, FinAuthorityPct);
        }

        private static void Finish(string why)
        {
            Debug.Log(Tag + "booster recovery complete - " + why
                      + " after " + (Planetarium.GetUniversalTime() - startedAt).ToString("F0") + " s");
            Active = false;

            AttitudeController.Booster.Release(booster);
            if (FlightGlobals.ActiveVessel == booster) FlightInputHandler.state.mainThrottle = 0f;

            // ---- LET THE STAGE SETTLE ON ITS LEGS BEFORE THE CAMERA LEAVES. ----
            if (upperStage != null && upperStage.state != Vessel.State.DEAD
                && why == "booster down" && settleUntil <= 0.0)
            {
                settleUntil = Planetarium.GetUniversalTime() + SettleAfterLandingS;
                Debug.Log(Tag + "settling on the pad for " + SettleAfterLandingS.ToString("F0")
                          + " s before the camera goes back to the upper stage");
                return;
            }

            if (upperStage != null && upperStage.state != Vessel.State.DEAD)
            {
                FlightGlobals.ForceSetActiveVessel(upperStage);

                // ---- ⛔ RE-ASSERT CONTROL AFTER THE FORCED SWITCH. ----
                try
                {
                    InputLockManager.ClearControlLocks();
                    if (CameraManager.Instance != null)
                        CameraManager.Instance.SetCameraFlight();
                    upperStage.MakeActive();
                }
                catch (Exception e)
                {
                    Debug.LogWarning(Tag + "control re-assert after handback failed: " + e.Message);
                }

                RestoreRanges(upperStage);

                Debug.Log(Tag + "focus -> '" + upperStage.vesselName + "' (it never stopped flying)");

                // ---- REMOVE THE LANDED BOOSTER IN PLACE - NO RECOVERY EVENT, NO SCENE CHANGE. ----
                if (booster != null && booster.state != Vessel.State.DEAD
                    && booster != FlightGlobals.ActiveVessel
                    && FlightGlobals.ActiveVessel == upperStage
                    && booster.LandedOrSplashed)
                {
                    try
                    {
                        booster.Die();
                        Debug.Log(Tag + "landed booster removed in place - no scene change, focus stays "
                                  + "on '" + upperStage.vesselName + "'");
                    }
                    catch (Exception e) { Debug.LogWarning(Tag + "in-place booster removal failed: " + e.Message); }
                }
            }
        }

        private static LandingInputs Read(Vessel v)
        {
            LandingInputs s = new LandingInputs();
            s.Valid = true;
            s.AltitudeRadar = v.radarAltitude;
            s.AltitudeAsl = v.altitude;
            s.VerticalSpeed = v.verticalSpeed;
            s.SurfaceSpeed = v.srfSpeed;
            s.HorizontalSpeed = Math.Sqrt(Math.Max(0.0,
                v.srfSpeed * v.srfSpeed - v.verticalSpeed * v.verticalSpeed));
            s.Gravity = (v.mainBody != null)
                ? v.mainBody.gMagnitudeAtCenter / Math.Pow(v.mainBody.Radius + v.altitude, 2.0) : 9.81;
            s.AtmosphereDepthM = (v.mainBody != null) ? v.mainBody.atmosphereDepth : 70000.0;
            s.DynamicPressureKpa = v.dynamicPressurekPa;
            s.Landed = (v.situation == Vessel.Situations.LANDED
                     || v.situation == Vessel.Situations.SPLASHED);
            s.Droneship = (Profile == LandingProfile.Droneship);

            double pressureAtm = (v.mainBody != null)
                ? v.mainBody.GetPressure(v.altitude) / 101.325 : 0.0;

            // ---- ⛔ ISSUE 8. THIS SUMMED THREE MUTUALLY EXCLUSIVE ENGINE MODES. ----
            double thrust = 0.0, thrustThree = 0.0, thrustOne = 0.0; int n = 0;
            bool haveCentre = false, haveThree = false;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    if (es[m].flameout) continue;
                    float isp0 = es[m].atmosphereCurve.Evaluate(0f);
                    float ispNow = es[m].atmosphereCurve.Evaluate((float)pressureAtm);
                    double scale = (isp0 > 0.01f) ? ispNow / isp0 : 1.0;
                    double t1 = es[m].maxThrust * scale;

                    string id = es[m].engineID;
                    if (Contains(id, VehicleParts.EngineIdCentre)) { thrustOne = t1; haveCentre = true; }
                    else if (Contains(id, VehicleParts.EngineIdThree)) { thrustThree = t1; haveThree = true; }
                    else { thrust += t1; n++; }
                }
            }

            double mass = v.GetTotalMass();
            s.Mass = mass;
            bool octaweb = haveCentre && haveThree;
            bool additive = octaweb && FindEngineSwitch(v) == null;
            if (additive)
            {
                s.AccelOneEngine   = (mass > 0.0) ? thrustOne / mass : 0.0;
                s.AccelThreeEngine = (mass > 0.0) ? (thrustOne + thrustThree) / mass : 0.0;
                s.MaxThrustAccel   = (mass > 0.0) ? (thrustOne + thrustThree + thrust) / mass : 0.0;
            }
            else
            {
                s.MaxThrustAccel   = (mass > 0.0) ? thrust / mass : 0.0;
                s.AccelThreeEngine = (mass > 0.0) ? thrustThree / mass : 0.0;
                s.AccelOneEngine   = (mass > 0.0) ? thrustOne / mass : 0.0;
            }
            s.EngineCount = octaweb ? VehicleParts.OctawebEngineCount : n;
            s.PhaseElapsedS = Planetarium.GetUniversalTime() - phaseStartedAt;

            // ---- RECOVERY PROPELLANT FRACTION: 1.0 at handover, so the entry burn can reserve the
            double propNow = RecoveryPropUnits(v);
            if (recoveryPropStart <= 0.0 && propNow > 0.0)
            {
                recoveryPropStart = propNow;
                Debug.Log(Tag + "recovery reserve baseline latched: " + recoveryPropStart.ToString("F0")
                          + " units (Kerosene+LqdOxygen) at phase " + Landing.Name(Phase)
                          + ", mass " + v.GetTotalMass().ToString("F1") + " t");
            }
            s.RecoveryPropFrac = (recoveryPropStart > 0.0) ? propNow / recoveryPropStart : -1.0;

            // ---- ADAPTIVE LANDING RESERVE INPUTS (sensed live). ----
            s.RecoveryPropKg = RecoveryPropMassKg(v);
            double rhoLand = (v.mainBody != null)
                ? v.mainBody.GetDensity(v.mainBody.GetPressure(1000.0), v.mainBody.GetTemperature(1000.0))
                : 1.2;
            double bcLand = BoosterDrag.BcAtMach(0.8);
            double vTermEst = (rhoLand > 1e-4 && bcLand > 0.0)
                ? Math.Sqrt(2.0 * s.Gravity * bcLand / rhoLand) : 280.0;
            if (vTermEst < 200.0) vTermEst = 200.0; else if (vTermEst > 380.0) vTermEst = 380.0;
            s.TerminalSpeedMps = vTermEst;

            if (Phase == LandingPhase.EntryBurn && Time.realtimeSinceStartup - lastReserveLog > 1.5)
            {
                lastReserveLog = Time.realtimeSinceStartup;
                Debug.Log(Tag + "ENTRY reserve frac " + s.RecoveryPropFrac.ToString("F3")
                          + " (cut at <= " + Landing.EntryBurnReserveFrac.ToString("F2")
                          + ", droneship=" + s.Droneship + ")  propNow " + propNow.ToString("F0")
                          + "  mass " + v.GetTotalMass().ToString("F1") + " t  srfV "
                          + v.srfSpeed.ToString("F0") + "  vSpd " + s.VerticalSpeed.ToString("F0"));
            }

            s.DownrangeM = HavePad && v.mainBody != null
                ? GroundRange(v.mainBody, v.latitude, v.longitude, PadLat, PadLon) : 0.0;

            s.RangeToPartnerM = AutoPilot.Range(v, upperStage);
            s.FlipDone = flipComplete;
            Vector3d bUp = (v.CoM - v.mainBody.position).normalized;
            Vector3d retro = v.srf_velocity;
            s.HorizRetroMag = (retro.sqrMagnitude > 1.0)
                ? Vector3d.Exclude(bUp, retro.normalized).magnitude : 0.0;
            bool missValid;
            s.PredictedMissM = PredictedMiss(v, out missValid);
            s.PredictedMissValid = missValid;
            // ---- SPLIT THE IMPACT ERROR INTO DOWNRANGE + CROSSRANGE (the guidance steers on the whole
            Vector3d errH = ImpactErrorHoriz(v);
            Vector3d downHat = Vector3d.Exclude(bUp, retro);
            if (downHat.sqrMagnitude > 1.0 && errH.sqrMagnitude > 1e-6)
            {
                downHat = downHat.normalized;
                Vector3d crossHat = Vector3d.Cross(bUp, downHat).normalized;
                s.DownMissM = Vector3d.Dot(errH, downHat);
                s.CrossMissM = Vector3d.Dot(errH, crossHat);
            }
            else { s.DownMissM = 0.0; s.CrossMissM = 0.0; }
            s.InitialMissM = initialMiss;
            return s;
        }

        /// ---- WHY A PREDICTION AND NOT A VELOCITY ERROR ----
        /// ---- WHAT THIS DELIBERATELY IS NOT ----
        private static double PredictedMiss(Vessel v) { bool drag; return PredictedMiss(v, out drag); }

        private static double PredictedMiss(Vessel v, out bool dragModelled)
        {
            dragModelled = false;
            if (!HavePad || v == null || v.mainBody == null) return 0.0;
            CelestialBody b = v.mainBody;

            Vector3d up = (v.CoM - b.position).normalized;
            Vector3d vel = v.srf_velocity;
            double vz = Vector3d.Dot(vel, up);
            double alt = v.altitude;
            double r = b.Radius + alt;
            double g = b.gMagnitudeAtCenter / (r * r);
            if (g <= 0.0) return 0.0;

            double disc = vz * vz + 2.0 * g * alt;
            if (disc < 0.0) return 0.0;
            double t = (vz + Math.Sqrt(disc)) / g;
            if (t <= 0.0) return 0.0;

            // ---- THE SAME PREDICTION THE GUIDANCE STEERS ON. See ImpactPointHoriz. ----
            Vector3d toImpact;
            if (!ImpactPointHoriz(v, up, alt, out toImpact, out dragModelled)) return 0.0;

            Vector3d toLz = Vector3d.Exclude(up,
                b.GetWorldSurfacePosition(PadLat, PadLon, alt) - v.CoM);

            Vector3d err = toImpact - toLz;
            double miss = err.magnitude;
            return (Vector3d.Dot(err, -toLz) > 0.0) ? miss : -miss;
        }

        private static double initialMiss;

        /// ---- ⛔ ONE PREDICTION, NOT TWO. THIS FILE HAD BOTH AND THE GUIDANCE USED THE WRONG ONE. ----
        private static Vector3d ImpactErrorHoriz(Vessel v)
        {
            if (!HavePad || v == null || v.mainBody == null) return Vector3d.zero;
            CelestialBody b = v.mainBody;

            Vector3d up = (v.CoM - b.position).normalized;
            double alt = v.altitude;

            Vector3d toImpact;
            if (!ImpactPointHoriz(v, up, alt, out toImpact)) return Vector3d.zero;

            Vector3d toLz = Vector3d.Exclude(up,
                b.GetWorldSurfacePosition(PadLat, PadLon, alt) - v.CoM);
            return toImpact - toLz;
        }

        private static bool ImpactPointHoriz(Vessel v, Vector3d up, double alt, out Vector3d toImpact)
        {
            bool drag;
            return ImpactPointHoriz(v, up, alt, out toImpact, out drag);
        }

        private static bool ImpactPointHoriz(Vessel v, Vector3d up, double alt, out Vector3d toImpact,
                                             out bool dragModelled)
        {
            toImpact = Vector3d.zero;
            dragModelled = false;
            CelestialBody b = v.mainBody;

            Impact im = ImpactPredictor.PredictBooster(v);
            if (im.Valid && im.DragModelled)
            {
                Vector3d impactPos = b.GetWorldSurfacePosition(im.LatDeg, im.LonDeg, alt);
                toImpact = Vector3d.Exclude(up, impactPos - v.CoM);
                dragModelled = true;
                return true;
            }

            Vector3d vel = v.srf_velocity;
            double vz = Vector3d.Dot(vel, up);
            double r = b.Radius + alt;
            double g = b.gMagnitudeAtCenter / (r * r);
            if (g <= 0.0) return false;
            double disc = vz * vz + 2.0 * g * alt;
            if (disc < 0.0) return false;
            double t = (vz + Math.Sqrt(disc)) / g;
            if (t <= 0.0) return false;

            toImpact = Vector3d.Exclude(up, vel) * t;
            return true;
        }

        // ---- FLIP STATE. The vectors are the glue's; the schedule is Landing's. ----
        private static Vector3d flipVec, flipFinal, flipAxis;
        private static bool flipSeeded, flipComplete;
        private static double rollStartedAt, rollSettledAt;

        // ---- SINGLE-AXIS SLEW STATE. The persistent aim the coast/entry/descent walk toward. ----
        private static Vector3d slewVec;
        private static bool slewSeeded;

        // ---- COAST AIM WALK. A persistent aim stepped toward the phase target at a bounded rate, so the
        // Up->retrograde reorientation is smooth and never a saturating jump. See Aim's coast block. ----
        private static Vector3d coastAimVec;
        private static bool coastAimSeeded;
        [Tunable] public static double CoastAimRateDps = 2.5;

        // ---- ROLL-REFERENCE SIGN. Which of the two in-plane tops (±flipAxis x dir) matches the roll
        // the stage launched with. Captured once, when flipAxis is first known, and held. 0 = not yet. ----
        private static double slewSign;

        private static Vector3d SingleAxisSlew(Vessel v, Vector3d target)
        {
            if (target.sqrMagnitude < 1e-6) return target;
            target = target.normalized;
            Vector3d fwd = v.ReferenceTransform.up;
            if (!slewSeeded) { slewVec = fwd; slewSeeded = true; }

            double toGo = Vector3d.Angle(slewVec, target);
            if (toGo <= Landing.FlipFineDeg)
            {
                slewVec = target;
                return slewVec;
            }

            if (Vector3d.Angle(fwd, slewVec) < Landing.FlipNoseCatchDeg)
            {
                Vector3d axis = Vector3d.Cross(slewVec, target);
                if (axis.sqrMagnitude > 1e-9)
                {
                    double step = (toGo < Landing.FlipPowerDeg) ? toGo : Landing.FlipPowerDeg;
                    slewVec = ((QuaternionD)Quaternion.AngleAxis((float)step, (Vector3)axis.normalized)
                               * slewVec).normalized;
                }
            }
            return slewVec;
        }

        private static Vector3d TopVector(Vessel v)
        {
            QuaternionD rot = (QuaternionD)(v.ReferenceTransform.rotation
                                            * Quaternion.Euler(-90f, 0f, 0f));
            return rot * Vector3d.up;
        }

        private static Vector3d StepCoastAim(Vessel v, Vector3d target)
        {
            if (!coastAimSeeded || coastAimVec.sqrMagnitude < 0.5)
            {
                coastAimVec = v.ReferenceTransform.up;
                coastAimSeeded = true;
            }
            if (target.sqrMagnitude < 1e-6) return coastAimVec.normalized;
            double dt = Time.fixedDeltaTime; if (dt <= 0.0) dt = 0.02;
            float maxRad = (float)(CoastAimRateDps * Math.PI / 180.0 * dt);
            coastAimVec = Vector3d.RotateTowards(coastAimVec, target.normalized, maxRad, 0f).normalized;
            return coastAimVec;
        }

        private static Vector3d StepFlip(Vessel v)
        {
            Vector3d up = (v.CoM - v.mainBody.position).normalized;

            if (!flipSeeded)
            {
                // ---- ⛔ THE GEOMETRY IS IN pure/FlipGeometry.cs AND IT IS THERE FOR A REASON. ----
                double rX, rY, rZ, aX, aY, aZ, fX, fY, fZ;
                double deg = LandingSites.FlipDeg(Profile);
                if (!FlipGeometry.Solve(up.x, up.y, up.z,
                                        v.srf_velocity.x, v.srf_velocity.y, v.srf_velocity.z,
                                        deg,
                                        out rX, out rY, out rZ,
                                        out aX, out aY, out aZ,
                                        out fX, out fY, out fZ))
                    return v.ReferenceTransform.up;

                flipAxis = new Vector3d(aX, aY, aZ);
                flipFinal = new Vector3d(fX, fY, fZ);
                flipVec = v.ReferenceTransform.up;
                flipSeeded = true;
                flipComplete = false;
                rollSettledAt = 0.0;
                rollStartedAt = Planetarium.GetUniversalTime();
                Debug.Log(Tag + "flip: " + deg.ToString("F0")
                          + " deg about the plane of flight, finishing "
                          + FlipGeometry.AngleDeg(fX, fY, fZ, rX, rY, rZ).ToString("F1")
                          + " deg off flat retrograde (must be ~0 for RTLS - see FlipGeometry)");
            }

            // ---- ⛔ SETTLE THE ROLL BEFORE PITCHING. THREE CONSTANTS EXISTED; THE GATE DID NOT. ----
            if (rollSettledAt <= 0.0)
            {
                double nowUt = Planetarium.GetUniversalTime();
                double inRoll = nowUt - rollStartedAt;

                Vector3d wantTop = Vector3d.Exclude(v.ReferenceTransform.up, -flipAxis);
                Vector3d haveTop = TopVector(v);
                double rollErr = (wantTop.sqrMagnitude > 1e-6)
                               ? Vector3d.Angle(haveTop, wantTop.normalized) : 0.0;

                bool settled = rollErr < Landing.FlipRollToleranceDeg
                               && inRoll > Landing.FlipRollMinS;
                if (settled || inRoll > Landing.FlipRollMaxS)
                {
                    rollSettledAt = nowUt;
                    Debug.Log(Tag + "flip roll settled at " + rollErr.ToString("F1")
                              + " deg after " + inRoll.ToString("F1") + " s"
                              + (settled ? "" : " (ceiling reached - flipping anyway)"));
                }
                else return flipVec;
            }

            double toGo = Vector3d.Angle(flipFinal, flipVec);
            if (toGo < Landing.FlipFineDeg)
            {
                flipVec = flipFinal;
                if (!flipComplete)
                {
                    flipComplete = true;
                    Debug.Log(Tag + "flip complete");
                }
                return flipVec;
            }

            bool coarse = toGo >= Landing.FlipCoarseDeg;
            bool noseCaught = Vector3d.Angle(v.ReferenceTransform.up, flipVec)
                              < Landing.FlipNoseCatchDeg;
            if (!coarse || noseCaught)
                flipVec = (QuaternionD)Quaternion.AngleAxis((float)Landing.FlipPowerDeg,
                                                            (Vector3)flipAxis) * flipVec;
            return flipVec;
        }

        public static double GroundRange(CelestialBody b, double lat1, double lon1,
                                         double lat2, double lon2)
        {
            double p1 = lat1 * Math.PI / 180.0, p2 = lat2 * Math.PI / 180.0;
            double dp = (lat2 - lat1) * Math.PI / 180.0;
            double dl = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dp / 2) * Math.Sin(dp / 2)
                     + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
            return 2.0 * b.Radius * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        }

        private static void SetEngines(Vessel v, int want, bool wantThrust)
        {
            PartModule em = FindEngineSwitch(v);
            if (em != null) { SetOctawebMode(v, em, want, wantThrust); return; }
            SetEnginesIndividually(v, want, wantThrust);
        }

        // ------------------------------------------------------------------ the octaweb

        /// ---- WHY THIS IS NOT Activate()/Shutdown() ON INDIVIDUAL ENGINES ----
        /// ---- STEP AND VERIFY, BECAUSE THE PART ONLY OFFERS "NEXT" ----
        private static void SetOctawebMode(Vessel v, PartModule em, int want, bool wantThrust)
        {
            if (want <= 0)
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
                    for (int m = 0; m < es.Count; m++)
                        if (es[m].EngineIgnited) es[m].Shutdown();
                }
                return;
            }

            // ---- ⛔ THE 3->1 HANDOVER LATCHES. IT MUST NOT BE RE-DECIDED PER TICK. ----
            if (Phase != lastBurnPhase) { burnLatchedEngines = 0; lastBurnPhase = Phase; }
            if (Phase == LandingPhase.EntryBurn)
            {
                if (burnLatchedEngines == 0) burnLatchedEngines = want;
                else want = burnLatchedEngines;
            }
            if (Phase == LandingPhase.LandingBurn && want <= 1) handedOverToOne = true;
            if (handedOverToOne && Phase == LandingPhase.LandingBurn) want = 1;

            int wantMode = VehicleParts.OctawebModeFor(want);

            // ---- ⛔ DIRECT MODULE CONTROL - NO ENGINE-SWITCH CYCLE (user-verified 2026-08-26). ----
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    bool mine = VehicleParts.EngineIdIsMode(es[m].engineID, wantMode);
                    if (mine && wantThrust) IgniteWithRetry(es[m]);
                    else if (es[m].EngineIgnited) es[m].Shutdown();
                }
            }
        }

        /// ---- ⛔ THE RETRY THE LANDING BURN NEVER HAD ----
        private static void IgniteWithRetry(ModuleEngines e)
        {
            if (e.EngineIgnited && !e.flameout) { igniteAttempts = 0; return; }
            double t = Planetarium.GetUniversalTime();
            if (t - lastIgniteAttemptAt < IgniteRetryIntervalS) return;
            lastIgniteAttemptAt = t;
            e.Activate();
            if (++igniteAttempts > 1)
                Debug.LogWarning(Tag + "ignition did not take - retry " + (igniteAttempts - 1)
                                     + " (spends one of the finite relights)");
        }

        private const double IgniteRetryIntervalS = 0.4;
        private static double lastIgniteAttemptAt;
        private static int igniteAttempts;

        private static double landingBurnLitAt;

        private static bool handedOverToOne;
        private static int burnLatchedEngines;
        private static LandingPhase lastBurnPhase;

        private static PartModule FindEngineSwitch(Vessel v)
        {
            if (v == null) return null;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                    if (p.Modules[m].moduleName == VehicleParts.EngineSwitchModule)
                        return p.Modules[m];
            }
            return null;
        }

        private static int LitOctawebMode(Vessel v)
        {
            if (v == null) return -1;
            int best = -1;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    if (!es[m].EngineIgnited || es[m].flameout) continue;
                    for (int mode = 0; mode <= 2; mode++)
                        if (VehicleParts.EngineIdIsMode(es[m].engineID, mode)
                            && (best < 0 || mode < best)) best = mode;
                }
            }
            if (best >= 0) return best;
            return ReadOctawebMode(FindEngineSwitch(v));
        }

        private static int ReadOctawebMode(PartModule em)
        {
            if (em == null) return -1;
            try
            {
                for (int i = 0; i < em.Fields.Count; i++)
                {
                    BaseField f = em.Fields[i];
                    if (f == null) continue;
                    if (!Same(f.name, "mode") && !Same(f.guiName, "mode")) continue;
                    object val = f.GetValue(em);
                    if (val == null) return -1;
                    string d = val.ToString();
                    if (Contains(d, VehicleParts.EngineIdThree)) return VehicleParts.ModeThreeEngine;
                    if (Contains(d, VehicleParts.EngineIdCentre)) return VehicleParts.ModeCentreOnly;
                    return VehicleParts.ModeAllEngines;
                }
            }
            catch (Exception) { }
            return -1;
        }

        private static bool Same(string a, string b)
        {
            return a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string s, string part)
        {
            return s != null && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ------------------------------------------------------------------ conventional engines

        private static void SetEnginesIndividually(Vessel v, int want, bool wantThrust)
        {
            List<ModuleEngines> all = new List<ModuleEngines>();
            for (int i = 0; i < v.parts.Count; i++)
                all.AddRange(v.parts[i].Modules.GetModules<ModuleEngines>());
            if (all.Count == 0) return;

            if (want <= 0)
            {
                for (int i = 0; i < all.Count; i++) if (all[i].EngineIgnited) all[i].Shutdown();
                return;
            }

            // ---- ADDITIVE OCTAWEB (individual-engine patch): three INDEPENDENT groups by engineID, lit
            ModuleEngines centre = null, pair = null;
            List<ModuleEngines> outer = new List<ModuleEngines>();
            for (int i = 0; i < all.Count; i++)
            {
                string id = all[i].engineID;
                if (Contains(id, VehicleParts.EngineIdCentre)) centre = all[i];
                else if (Contains(id, VehicleParts.EngineIdThree)) pair = all[i];
                else outer.Add(all[i]);
            }
            if (centre != null && pair != null)
            {
                if (Phase == LandingPhase.LandingBurn)
                {
                    if (want < landingCountFloor) landingCountFloor = want;
                    want = landingCountFloor;
                }
                else landingCountFloor = 99;
                SetGroup(centre, wantThrust && want >= 1);
                SetGroup(pair,   wantThrust && want >= 3);
                for (int i = 0; i < outer.Count; i++) SetGroup(outer[i], wantThrust && want > 3);
                return;
            }

            Vector3 axis = v.ReferenceTransform.up;
            Vector3 com = v.CoM;
            all.Sort(delegate (ModuleEngines a, ModuleEngines b)
            {
                return Off(a, com, axis).CompareTo(Off(b, com, axis));
            });

            for (int i = 0; i < all.Count; i++)
            {
                if (i < want && wantThrust) IgniteWithRetry(all[i]);
                else if (all[i].EngineIgnited) all[i].Shutdown();
            }
        }

        private static float Off(ModuleEngines e, Vector3 com, Vector3 axis)
        {
            if (e == null || e.part == null) return float.MaxValue;
            return Vector3.ProjectOnPlane(e.part.transform.position - com, axis).sqrMagnitude;
        }

        private static void SetGroup(ModuleEngines e, bool on)
        {
            if (e == null) return;
            if (on) IgniteWithRetry(e);
            else if (e.EngineIgnited) e.Shutdown();
        }

        private static int landingCountFloor = 99;

        // ------------------------------------------------------------------ grid fins

        private static void DeployGridFins(Vessel v)
        {
            if (gridFinsOut || v == null) return;
            gridFinsOut = true;

            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleAnimateGeneric> mods =
                    v.parts[i].Modules.GetModules<ModuleAnimateGeneric>();
                for (int m = 0; m < mods.Count; m++)
                {
                    if (mods[m].animationName != VehicleParts.GridFinAnimation) continue;
                    if (mods[m].Progress > 0.5f) continue;
                    mods[m].Toggle();
                    n++;
                }
            }
            SetGridFinControl(v, true);
            Debug.Log(Tag + "grid fins deployed (" + n + ")");
            if (n == 0)
                Debug.LogWarning(Tag + "no grid fins found - looked for ModuleAnimateGeneric '"
                                     + VehicleParts.GridFinAnimation + "'. Entry will have no "
                                     + "aerodynamic authority.");
        }

        private static double recoveryPropStart = -1.0;

        private static double lastReserveLog = -10.0;

        private static double RecoveryPropUnits(Vessel v)
        {
            if (v == null) return 0.0;
            double u = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                PartResourceList rs = v.parts[i].Resources;
                for (int r = 0; r < rs.Count; r++)
                {
                    string nm = rs[r].resourceName;
                    if (nm == "RP-1" || nm == "CooledRP-1" || nm == "LqdOxygen" || nm == "CooledLqdOxygen"
                        || nm == "Kerosene" || nm == "LiquidFuel" || nm == "Oxidizer")
                        u += rs[r].amount;
                }
            }
            return u;
        }

        private static double RecoveryPropMassKg(Vessel v)
        {
            if (v == null) return 0.0;
            double kg = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                PartResourceList rs = v.parts[i].Resources;
                for (int r = 0; r < rs.Count; r++)
                {
                    string nm = rs[r].resourceName;
                    if (nm == "RP-1" || nm == "CooledRP-1" || nm == "LqdOxygen" || nm == "CooledLqdOxygen"
                        || nm == "Kerosene" || nm == "LiquidFuel" || nm == "Oxidizer")
                        kg += rs[r].amount * rs[r].info.density * 1000.0;
                }
            }
            return kg;
        }

        private static double ColdGasFraction(Vessel v)
        {
            if (v == null) return -1.0;
            double amt = 0.0, cap = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                PartResourceList rs = v.parts[i].Resources;
                for (int r = 0; r < rs.Count; r++)
                {
                    string nm = rs[r].resourceName;
                    if (nm == "Nitrogen" || nm == "MonoPropellant" || nm == "NitrogenGas" || nm == "HTP")
                    { amt += rs[r].amount; cap += rs[r].maxAmount; }
                }
            }
            return (cap > 0.0) ? amt / cap : -1.0;
        }

        private static void SetGridFinControl(Vessel v, bool active)
        {
            if (v == null) return;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                bool isFin = false;
                List<ModuleAnimateGeneric> anims = p.Modules.GetModules<ModuleAnimateGeneric>();
                for (int m = 0; m < anims.Count; m++)
                    if (anims[m].animationName == VehicleParts.GridFinAnimation) { isFin = true; break; }
                if (!isFin) continue;
                List<ModuleControlSurface> cs = p.Modules.GetModules<ModuleControlSurface>();
                for (int m = 0; m < cs.Count; m++)
                {
                    cs[m].ignorePitch = !active;
                    cs[m].ignoreYaw = !active;
                    cs[m].ignoreRoll = !active;
                }
            }
        }

        private static bool gridFinsOut;

        private static void Aim(Vessel v, LandingCommand c, LandingInputs s)
        {
            Vector3d up = (v.CoM - v.mainBody.position).normalized;
            Vector3d dir = up;

            // ---- ⛔ COAST: WALK THE AIM, NEVER JUMP IT (flight_0824_183939). ----
            bool coastWalked = false;
            if (c.Phase == LandingPhase.Coast)
            {
                Vector3d srf = v.srf_velocity;
                Vector3d target = (c.Aim == LandingAim.SurfaceRetrograde && srf.sqrMagnitude > 1.0)
                                  ? -srf.normalized : up;
                dir = StepCoastAim(v, target);
                coastWalked = true;
            }
            else coastAimSeeded = false;

            // ---- ⛔ Hold DID NOT HOLD - IT POINTED THE STAGE STRAIGHT UP. ----
            if (c.Aim == LandingAim.Hold) dir = v.ReferenceTransform.up;

            // ---- THE STEPPED TURNAROUND. BOOSTER.ks:295-381 Flip1. ----
            if (c.Aim == LandingAim.Flip) dir = StepFlip(v);

            if (c.Aim == LandingAim.FlatRetrograde)
            {
                Vector3d flat = Vector3d.Exclude(up, v.srf_velocity);
                if (flat.sqrMagnitude > 1.0) dir = -flat.normalized;
            }

            if (c.Aim == LandingAim.SurfaceRetrograde && !coastWalked)
            {
                Vector3d srf = v.srf_velocity;
                if (srf.sqrMagnitude > 1.0) dir = -srf.normalized;
            }
            else if (c.Aim == LandingAim.TowardTarget && HavePad)
            {
                // ---- ⛔ PURELY HORIZONTAL, TOWARD THE LZ. THE 20-DEGREE PITCH-UP WAS INVENTED. ----
                Vector3d toPad = (v.mainBody.GetWorldSurfacePosition(PadLat, PadLon, v.altitude)
                                  - v.CoM);
                Vector3d horiz = Vector3d.Exclude(up, toPad);
                if (horiz.sqrMagnitude > 1.0) dir = horiz.normalized;
            }

            // ---- ⛔ ISSUE 5. LandingZoneGuidance WAS PORTED AND NEVER CONNECTED. ----
            if (HavePad && c.GuidedLean && c.Aim == LandingAim.SurfaceRetrograde && s.DownrangeM > 0.0)
            {
                // ---- THE ERROR IS WHERE IT WILL LAND, NOT WHERE IT IS. ----
                Vector3d errHoriz = ImpactErrorHoriz(v);
                if (c.Throttle <= 0.01 && errHoriz.sqrMagnitude > 1.0)
                    errHoriz = DescentLeadError(errHoriz);
                if (errHoriz.sqrMagnitude > 1.0)
                {
                    double aoa = Landing.GuidanceAoaDeg(s.AltitudeRadar, c.Throttle > 0.01,
                                                        c.Engines == 1);

                    // ================================================================================
                    // ================================================================================
                    Vector3d velVec = -v.srf_velocity;
                    Vector3d naive = velVec + errHoriz;
                    AoaDeg = aoa;

                    // ---- ⚠ THE TWO BRANCHES MUST MEET AT THE CEILING, OR THE COMMAND STEPS. ----
                    if (naive.sqrMagnitude > 1e-6)
                    {
                        double naiveLean = Math.Tan(Vector3d.Angle(naive, velVec) * Math.PI / 180.0);
                        double ceiling = Landing.LeanFraction(errHoriz.magnitude, aoa);
                        double lean = Landing.ClampLean(naiveLean, ceiling);
                        LeanFrac = lean;
                        dir = (velVec.normalized + lean * errHoriz.normalized).normalized;
                    }
                }
            }

            // ---- ⛔ EVERY RETROGRADE-HOLDING PHASE REORIENTS AS A SINGLE-AXIS SLEW. (user, 2026-08-20) ----
            if (c.Aim == LandingAim.SurfaceRetrograde) dir = SingleAxisSlew(v, dir);
            else slewSeeded = false;

            // ---- THE GAIN IS THE GUIDANCE'S CHOICE, NOT A TERNARY HERE ----
            // ---- ⛔ THE FOUR kOS SCALE FACTORS ARE GONE. ONE TIME CONSTANT REPLACES THEM. ----
            AttitudeController.Booster.TimeConstantS =
                (c.Phase == LandingPhase.LandingBurn) ? 0.35 : Attitude.DefaultTimeConstantS;

            // ---- AND THE RATE CEILING FOR THIS PHASE. EVERY FIGURE MEASURED. ----
            switch (c.Phase)
            {
                case LandingPhase.Flip:
                case LandingPhase.BoostbackKill:
                case LandingPhase.Boostback:
                    AttitudeController.Booster.MaxRateDps = Attitude.FlipMaxRateDps; break;
                case LandingPhase.Coast:
                    AttitudeController.Booster.MaxRateDps = Attitude.CoastMaxRateDps; break;
                case LandingPhase.EntryBurn:
                    AttitudeController.Booster.MaxRateDps = Attitude.EntryMaxRateDps; break;
                case LandingPhase.Descent:
                    AttitudeController.Booster.MaxRateDps = Attitude.DescentMaxRateDps; break;
                default:
                    AttitudeController.Booster.MaxRateDps = Attitude.LandingMaxRateDps; break;
            }

            // ---- ⛔ FLY THE BOOSTER ON PITCH ALONE, ROLL AND YAW HELD - "SAS + W/S". ----
            Vector3d upHint = Vector3d.zero;
            if (flipSeeded && flipAxis.sqrMagnitude > 0.5)
            {
                Vector3d inPlaneTop = Vector3d.Cross(flipAxis, dir);
                if (inPlaneTop.sqrMagnitude > 1e-6)
                {
                    inPlaneTop = inPlaneTop.normalized;
                    if (slewSign == 0.0)
                        slewSign = (Vector3d.Dot(inPlaneTop, TopVector(v)) < 0.0) ? -1.0 : 1.0;
                    upHint = slewSign * inPlaneTop;
                }
            }

            // ---- ⛔ THE COAST DROPS THE ROLL REFERENCE - JUST DAMP THE RATE (user 2026-08-21) ----
            bool coastDampRoll = (c.Phase == LandingPhase.Coast);
            AttitudeController.Booster.LockRoll = coastDampRoll;
            if (coastDampRoll) upHint = Vector3d.zero;
            AttitudeController.Booster.RollControlRangeDeg = BoosterRollRangeDeg;

            AttitudeController.Booster.SteerTo(v, dir, upHint);
        }
    }
}
