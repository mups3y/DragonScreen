/*
 * DragonScreen - BoosterRecovery
 *
 * GLUE. Flies `pure/Landing.cs` on the BOOSTER after separation, hands focus over to it, and hands
 * focus back to the upper stage when it is down.
 *
 * ---- THE HANDOVER IS COPIED FROM OUR OWN kOS INTERFACE, WHICH ALREADY SOLVED THIS ----
 * `Ships/Script/falcon9.ks:4723` FalconExtendRange and FalconFocusBooster:
 *
 *      SetLoadDistances(ship, 1500000)              raise physics range to 1500 km
 *      wait until a vessel named "Booster" is loaded
 *      SetLoadDistances(vessel("Booster"), 1500000)  raise it on the booster too
 *      kuniverse:forceactive(vessel("Booster"))      switch focus
 *
 * The C# equivalents were confirmed present in the game's own assembly rather than recalled:
 * `Vessel.vesselRanges` (Assembly-CSharp even carries the hint "Use Vessel.vesselRanges instead")
 * and `FlightGlobals.ForceSetActiveVessel`, which MechJeb also uses at
 * MechJebModuleStagingController.cs:402.
 *
 * ---- ⚠ AND F9I'S OWN WARNING APPLIES TO US IDENTICALLY ----
 * Its HUD text says it plainly: "KSP will clamp this without PhysicsRangeExtender, so expect the
 * upper stage to unload near 300 km." `falcon-physics-range-clamp` MEASURED that on four flights -
 * 1500 km is requested, 297-341 km is what you get, because PhysicsRangeExtender is NOT installed.
 *
 * So we CANNOT fly both vehicles at once for a whole recovery. What we do instead is honest and
 * works: the upper stage's apoapsis is already set by the ascent, so it COASTS - on rails once it
 * unloads, which preserves its orbit exactly - while the booster flies its recovery. When the
 * booster is down we take focus back and circularise. If the coast has already carried the upper
 * stage past apoapsis, circularisation happens at the NEXT one; nothing is lost but time.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class BoosterRecovery
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>What F9I asks for. KSP clamps it; asking is still correct.</summary>
        public const float RangeMetres = 1500000f;

        public static bool Active { get; private set; }
        public static LandingPhase Phase { get; private set; }
        public static LandingCommand Command;

        private static Vessel booster, upperStage;
        private static double startedAt;

        /// <summary>The booster, for the recorder. Null when no recovery is running.</summary>
        public static Vessel BoosterVessel { get { return booster; } }

        /// <summary>When the CURRENT landing phase began. The entry burn's soft start is timed off it.</summary>
        private static double phaseStartedAt;

        private static bool noBoosterReported;

        // ---- FOR THE RECORDER ----
        // Both columns wrote a hard-coded 0.0, so the two numbers that decide whether a landing was
        // any good - how high the stage really was and how far it missed by - were absent from every
        // row of every recording. They belong to the BOOSTER, which is not the vessel the recorder
        // samples, so they have to be published from here.
        /// <summary>Booster height above the terrain, metres. Zero when no recovery is running.</summary>
        public static double TrueRadar;
        /// <summary>Booster ground distance to the landing zone, metres.</summary>
        public static double DownrangeM;
        /// <summary>Signed predicted miss, metres. Negative = the impact point is past the LZ.</summary>
        public static double PredictedMissM;
        /// <summary>The miss the boostback started with, metres. The throttle tapers against it.</summary>
        public static double InitialMissM;

        // ---- EVERYTHING BELOW IS A DECISION INPUT THE RECORDER COULD NOT SEE ----
        // Each one is something the guidance acts on, so without it in the CSV a bad landing can be
        // observed but not explained. That is the same argument that produced this file.
        /// <summary>Distance to the upper stage, metres. The separation gate is on this.</summary>
        public static double RangeToPartnerM;
        /// <summary>Seconds in the current landing phase. Soft start and the timeouts key on it.</summary>
        public static double PhaseElapsedS;
        /// <summary>Octaweb mode ACTUALLY read back: 0 all, 1 three, 2 centre, -1 no answer.</summary>
        public static int OctaMode = -1;
        /// <summary>Engines actually ignited, against the count the guidance asked for.</summary>
        public static int EnginesLit;
        /// <summary>Grid fins commanded out.</summary>
        public static bool GridFinsOut;
        /// <summary>Lean fraction applied toward the pad, and the AoA it was computed from.</summary>
        public static double LeanFrac, AoaDeg;

        /// <summary>Which recovery this mission is flying. Drives the LZ and the ascent.</summary>
        public static LandingProfile Profile = LandingProfile.Rtls;

        /// <summary>The landing zone. NOT where we launched from - see below.</summary>
        public static double PadLat, PadLon;
        public static bool HavePad;

        /// <summary>
        /// Resolve the landing zone for this mission.
        ///
        /// ---- ⛔ THIS USED TO BE `PadLat = v.latitude` AT LIFTOFF. ----
        /// The launch pad is not LZ-1. They are a few hundred metres apart, and "a few hundred
        /// metres" is the entire problem: the boosters this is copied from land 0.34-0.56 m from the
        /// mark, so a target error of that size is three orders of magnitude bigger than the thing
        /// being tuned. It would never have shown up as a bug, only as a landing that was always
        /// slightly wrong for reasons that looked like guidance.
        ///
        /// The droneship is not a coordinate at all. BOOSTER.ks:147: it "is parked by hand and moves
        /// between missions", so it is found by name and asked where it currently is.
        /// </summary>
        public static void RememberPad(Vessel v)
        {
            if (v == null || HavePad) return;

            if (Profile == LandingProfile.Droneship)
            {
                Vessel drone = FindDroneship();
                if (drone != null)
                {
                    PadLat = drone.latitude; PadLon = drone.longitude; HavePad = true;
                    Debug.Log(Tag + "landing zone: droneship '" + drone.vesselName + "' at "
                              + PadLat.ToString("F6") + ", " + PadLon.ToString("F6"));
                    return;
                }
                PadLat = LandingSites.Lz0.LatDeg; PadLon = LandingSites.Lz0.LonDeg; HavePad = true;
                Debug.LogWarning(Tag + "no vessel named '" + LandingSites.DroneshipVesselName
                                     + "' in the world - falling back to " + LandingSites.Lz0.Name
                                     + ". The booster will aim at open water.");
                return;
            }

            LandingSite site = LandingSites.For(Profile);
            PadLat = site.LatDeg; PadLon = site.LonDeg; HavePad = true;
            Debug.Log(Tag + "landing zone: " + site.Name + " at " + PadLat.ToString("F6")
                      + ", " + PadLon.ToString("F6") + " (profile " + Profile + ")");
        }

        private static Vessel FindDroneship()
        {
            List<Vessel> all = FlightGlobals.Vessels;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].vesselName == LandingSites.DroneshipVesselName)
                    return all[i];
            return null;
        }

        public static void Reset()
        {
            Active = false; Phase = LandingPhase.Idle;
            booster = null; upperStage = null; HavePad = false;
            // Every latch below is a static that would otherwise carry a PREVIOUS flight's state
            // into this one: fins "already out" on a stage that has not launched, a mode machine
            // that thinks it is mid-step, a failure already reported so never reported again.
            gridFinsOut = false;
            lastModeStepAt = 0.0; modeSteps = 0; lastWantMode = -1; modeFailReported = false;
            phaseStartedAt = 0.0; noBoosterReported = false;
            TrueRadar = 0.0; DownrangeM = 0.0; initialMiss = 0.0;
            PredictedMissM = 0.0; InitialMissM = 0.0;
            RangeToPartnerM = 0.0; PhaseElapsedS = 0.0; OctaMode = -1; EnginesLit = 0;
            GridFinsOut = false; LeanFrac = 0.0; AoaDeg = 0.0;
        }

        // ------------------------------------------------------------------ handover

        /// <summary>
        /// Look for a separated booster. Called while the ascent is running; returns true once the
        /// handover has happened and the caller should stop flying the upper stage.
        /// </summary>
        public static bool TryHandover(Vessel active)
        {
            if (Active || active == null) return false;

            Vessel b = FindBooster(active);
            if (b == null)
            {
                // ---- SAY SO. A HANDOVER THAT NEVER FIRES MUST NOT BE SILENT. ----
                // The 21:01 flight recorded `landPhase = "-"` for all 1371 rows and the log said
                // nothing at all, so "the recovery did not happen" and "the recovery was never
                // attempted" looked identical. Once per attempt window, not per tick.
                if (!noBoosterReported)
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

            Extend(upperStage);
            Extend(booster);

            // Join the profile where the stage actually is. Handover is late by design - see
            // Landing.InitialPhase - so assuming Boostback would fly a falling booster back up.
            Phase = Landing.InitialPhase(Read(booster));
            // Latch the error the boostback starts with; the throttle tapers against it.
            initialMiss = Math.Abs(PredictedMiss(booster));

            FlightGlobals.ForceSetActiveVessel(booster);

            Debug.Log(Tag + "booster recovery: focus -> '" + booster.vesselName
                      + "' at " + Landing.Name(Phase)
                      + ", upper stage '" + upperStage.vesselName + "' coasts to apoapsis. "
                      + "Physics range asked " + (RangeMetres / 1000f).ToString("F0")
                      + " km; KSP clamps near 300 km without PhysicsRangeExtender.");
            return true;
        }

        /// <summary>
        /// The separated first stage: a loaded vessel that is not us, is not on the ground, and
        /// carries a booster tank. Matched on part name the same way FlightCommands finds the trunk.
        ///
        /// ---- TAKE THE HEAVIEST CANDIDATE THAT CAN STILL FLY, NOT THE FIRST ONE SEEN ----
        /// This used to return the first loaded vessel carrying a `.S1.` part, in whatever order
        /// FlightGlobals happened to hold them. A shed interstage, a broken-off tank section, or the
        /// wreckage of an earlier booster still carries that marker, and picking one of those means
        /// focus jumps to debris while the real stage falls unguided and the recovery then "completes"
        /// on the wrong object. Two extra tests settle it: it must have an engine to fly with, and
        /// among what is left the real booster is the heavy one by a wide margin.
        /// </summary>
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
                if (v.GetCrewCount() > 0) continue;            // never take a crewed vehicle

                // See VehicleParts: this matched "K1" from a PAW title and therefore never fired,
                // so the recovery could not run even once the handover gate was right.
                bool isBooster = false;
                for (int p = 0; p < v.parts.Count && !isBooster; p++)
                    if (VehicleParts.IsBooster(v.parts[p].name)) isBooster = true;
                if (!isBooster) continue;

                // Debris carries the marker; debris does not carry a working engine.
                if (!HasEngine(v)) continue;

                double m = v.GetTotalMass();
                if (m > bestMass) { bestMass = m; best = v; }
            }

            if (best != null)
                Debug.Log(Tag + "booster candidate '" + best.vesselName + "', "
                              + bestMass.ToString("F1") + " t");
            return best;
        }

        /// <summary>Engines actually burning, as against the number the guidance asked for.</summary>
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

        /// <summary>
        /// Raise the launch vehicle's physics range BEFORE anything separates from it.
        ///
        /// The range on the ACTIVE vessel is what decides how far away another vessel may drift and
        /// still be loaded, so this has to happen while the booster is still part of us. Called from
        /// AutoPilot.Engage - see the note there for the circular dependency this replaces.
        ///
        /// ⚠ KSP clamps it. `falcon-physics-range-clamp` measured 297-341 km against the 1500 km
        /// asked for, on four flights, because PhysicsRangeExtender is not installed. Asking is still
        /// correct: 300 km is far more than a booster recovery needs, and 22.5 km is far less.
        /// </summary>
        public static void PrepareForSeparation(Vessel v)
        {
            if (v == null) return;
            Extend(v);
            Debug.Log(Tag + "physics range raised on '" + v.vesselName + "' before separation - "
                          + "asked " + (RangeMetres / 1000f).ToString("F0")
                          + " km, KSP clamps near 300 km without PhysicsRangeExtender");
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
            s.unload = RangeMetres * 1.1f;
            s.pack = RangeMetres * 0.9f;
            s.unpack = 200f;
        }

        // ------------------------------------------------------------------ flying it

        public static void Tick()
        {
            if (!Active) return;

            if (booster == null || booster.state == Vessel.State.DEAD)
            {
                Finish("booster lost");
                return;
            }

            LandingInputs s = Read(booster);
            LandingCommand c = Landing.Guide(s, Phase);

            TrueRadar = s.AltitudeRadar;
            DownrangeM = s.DownrangeM;
            RangeToPartnerM = s.RangeToPartnerM;
            PhaseElapsedS = s.PhaseElapsedS;
            OctaMode = ReadOctawebMode(FindEngineSwitch(booster));
            EnginesLit = CountLit(booster);
            GridFinsOut = gridFinsOut;
            PredictedMissM = s.PredictedMissM;
            InitialMissM = s.InitialMissM;

            if (c.Phase != Phase)
            {
                Debug.Log(Tag + "booster -> " + Landing.Name(c.Phase)
                          + "  alt " + (s.AltitudeRadar / 1000.0).ToString("F1")
                          + " km, v " + s.SurfaceSpeed.ToString("F0")
                          + " m/s, downrange " + (s.DownrangeM / 1000.0).ToString("F1")
                          + " km, ignition at " + (c.IgnitionAltitude / 1000.0).ToString("F2")
                          + " km on " + c.Engines + " engine(s)");
                // The entry burn's soft start is timed from the start of its phase, so this clock
                // has to be reset on the transition and not merely at handover.
                phaseStartedAt = Planetarium.GetUniversalTime();
            }
            Phase = c.Phase;
            Command = c;

            if (Phase == LandingPhase.Touchdown) { Finish("booster down"); return; }

            // ---- NO SOLUTION IS AN END STATE AND NOTHING TREATED IT AS ONE ----
            // Landing.Guide returns NoSolution with throttle 1.0 - "everything we have, it is still
            // correct" - but nothing here ever acted on it, so the recovery stayed Active, kept
            // commanding full thrust, and only stopped when the booster hit the ground and went
            // DEAD. In the 22:18 recording that is the last 33 seconds of the flight. Finish() is
            // what releases the controller and hands focus back; without it a failed landing also
            // strands the camera.
            if (Phase == LandingPhase.NoSolution)
            {
                Debug.LogError(Tag + "BOOSTER CANNOT STOP - thrust-to-weight below 1 at "
                                   + (s.AltitudeRadar / 1000.0).ToString("F2") + " km, "
                                   + s.SurfaceSpeed.ToString("F0") + " m/s. Burning what is left.");
                Finish("no landing solution");
                return;
            }

            // ---- ⛔ THE "ONLY FLY IT WHILE FOCUSED" GUARD IS GONE, DELIBERATELY. ----
            // It used to return here unless the booster was the active vessel. That was true of the
            // old throttle path - FlightInputHandler.state belongs to whoever has focus - but it is
            // not true of KSP: every LOADED, unpacked vessel is simulated, and its own OnFlyByWire
            // callback is called whether or not the camera is on it. That is how F9I flies a booster
            // and an upper stage at the same time from two CPUs, and how MechJeb flies unfocused
            // craft.
            //
            // What still matters is LOADED. Beyond the physics range the booster goes on rails and
            // nothing here reaches it - which is why the range is raised before separation now.
            if (booster.packed) return;

            // ---- FINS OUT AT THE TOP OF THE ARC, NOT AT THE ENTRY BURN ----
            // F9I's AtmGNC opens with "grid fins out, entry burn, guided descent" - the fins are out
            // BEFORE the gate at 32.5 km, so the stage is already stable when it meets the thick air
            // rather than deploying into it.
            // ⚠ AND NOT WHILE SEPARATING. The gate listed Idle and Boostback and I added a phase
            // before both without revisiting it, so the fins would have deployed at 29 km, climbing
            // at 700 m/s, 11 m from the upper stage. They belong at the top of the arc.
            if (Phase != LandingPhase.Idle && Phase != LandingPhase.Separating
                && Phase != LandingPhase.Boostback) DeployGridFins(booster);

            SetEngines(booster, c.Engines);
            Aim(booster, c, s);
            // The BOOSTER's own throttle, through its own controller. Not FlightInputHandler.state,
            // which is the focused vessel's - that would have put the landing-burn throttle on the
            // upper stage the moment the camera moved.
            AttitudeController.Booster.Throttle = c.Throttle;
            // Cosmetic only, and only when it is the vessel on screen: keeps the throttle gauge
            // honest. MechJebModuleThrustController.cs:282 does the same for the same reason.
            if (FlightGlobals.ActiveVessel == booster)
                FlightInputHandler.state.mainThrottle = (float)c.Throttle;

            // ⛔ ISSUE 6. This was a second, hard-coded copy of the legs rule, so the DeployLegs
            // the guidance computes was ignored and the two could disagree. One source.
            if (c.DeployLegs) booster.ActionGroups.SetGroup(KSPActionGroup.Gear, true);
        }

        private static void Finish(string why)
        {
            Debug.Log(Tag + "booster recovery complete - " + why
                      + " after " + (Planetarium.GetUniversalTime() - startedAt).ToString("F0") + " s");
            Active = false;

            // Let the booster go: zeroes its throttle through its own control state and puts the
            // 0.05 landing-burn stopping time back to the default.
            AttitudeController.Booster.Release(booster);
            if (FlightGlobals.ActiveVessel == booster) FlightInputHandler.state.mainThrottle = 0f;

            // Hand focus back so the crew can watch the vehicle that still has somewhere to be. The
            // upper stage has been flying itself throughout - this is a camera move, not a handover.
            if (upperStage != null && upperStage.state != Vessel.State.DEAD)
            {
                FlightGlobals.ForceSetActiveVessel(upperStage);
                Debug.Log(Tag + "focus -> '" + upperStage.vesselName + "' (it never stopped flying)");
            }
        }

        private static LandingInputs Read(Vessel v)
        {
            LandingInputs s = new LandingInputs();
            s.Valid = true;
            s.AltitudeRadar = v.radarAltitude;
            // ⛔ ISSUE 1. This was NEVER SET, so it defaulted to 0 - and `InEntryBand` tests
            // `AltitudeAsl < 32500`, which 0 always satisfies. The entry-burn gate was therefore
            // permanently OPEN, and the burn would have lit the moment vertical speed passed
            // -300 m/s at ANY altitude, including straight after separation at 60 km.
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

            // Ambient pressure in ATMOSPHERES - atmosphereCurve is keyed in atm, GetPressure is kPa.
            double pressureAtm = (v.mainBody != null)
                ? v.mainBody.GetPressure(v.altitude) / 101.325 : 0.0;

            // ---- ⛔ ISSUE 8. THIS SUMMED THREE MUTUALLY EXCLUSIVE ENGINE MODES. ----
            // The Tundra first stage is ONE part carrying THREE ModuleEnginesFX - AllEngines 2560 kN,
            // ThreeLanding 1706, CenterOnly 764 - of which exactly one is ever lit. Summing every
            // non-flameout ModuleEngines gave 5030 kN, about double the real figure, and that number
            // is the input to the hoverslam. It also made `EngineCount` 3, so the "one engine" case
            // was modelled as a third of a thrust the stage does not have: 1676 kN against the real
            // 764. Both errors point the same way - too much thrust, ignition too late, into the pad.
            //
            // So the modes are read SEPARATELY, by engineID, and the guidance is given all three.
            // Anything that is not a named mode accumulates into the all-engines figure, which is
            // what a conventional cluster of identical engines should do.
            double thrust = 0.0, thrustThree = 0.0, thrustOne = 0.0; int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    if (es[m].flameout) continue;
                    // ⛔ ISSUE 2. This was MaxThrustOutputVac - VACUUM thrust - for a booster
                    // landing THROUGH AN ATMOSPHERE. A Merlin makes materially less at sea level,
                    // so the solve believed in thrust the stage did not have, put the hoverslam
                    // ignition altitude too LOW, and would have flown it into the pad at speed.
                    // Atmospheric output at the CURRENT pressure is the honest number.
                    // Thrust scales with Isp at fixed mass flow, and `atmosphereCurve` IS the
                    // engine's Isp-against-pressure curve - so the sea-level/vacuum Isp ratio is
                    // the thrust ratio. Built from `maxThrust` and `atmosphereCurve` because both
                    // are plain ModuleEngines fields; the first attempt at this called
                    // `MaxThrustOutputAtm(..., v.staticAmbientTemperature, ...)` and the compiler
                    // rejected it, which is trigger #3 doing its job.
                    float isp0 = es[m].atmosphereCurve.Evaluate(0f);
                    float ispNow = es[m].atmosphereCurve.Evaluate((float)pressureAtm);
                    double scale = (isp0 > 0.01f) ? ispNow / isp0 : 1.0;
                    double t1 = es[m].maxThrust * scale;

                    string id = es[m].engineID;
                    if (Contains(id, VehicleParts.EngineIdCentre)) thrustOne = t1;
                    else if (Contains(id, VehicleParts.EngineIdThree)) thrustThree = t1;
                    else { thrust += t1; n++; }
                }
            }

            double mass = v.GetTotalMass();
            s.MaxThrustAccel = (mass > 0.0) ? thrust / mass : 0.0;
            s.AccelThreeEngine = (mass > 0.0) ? thrustThree / mass : 0.0;
            s.AccelOneEngine = (mass > 0.0) ? thrustOne / mass : 0.0;
            // With the octaweb the "all" mode is a single module standing for nine engines, so the
            // count has to come from the vehicle rather than from how many modules were summed.
            s.EngineCount = (FindEngineSwitch(v) != null) ? VehicleParts.OctawebEngineCount : n;
            s.PhaseElapsedS = Planetarium.GetUniversalTime() - phaseStartedAt;

            s.DownrangeM = HavePad && v.mainBody != null
                ? GroundRange(v.mainBody, v.latitude, v.longitude, PadLat, PadLon) : 0.0;

            s.RangeToPartnerM = AutoPilot.Range(v, upperStage);
            s.PredictedMissM = PredictedMiss(v);
            s.InitialMissM = initialMiss;
            return s;
        }

        /// <summary>
        /// Signed miss of the BALLISTIC IMPACT POINT against the landing zone, metres.
        /// POSITIVE = the impact is still short of the LZ, on the booster's side of it.
        /// NEGATIVE = it has walked past.
        ///
        /// ---- WHY A PREDICTION AND NOT A VELOCITY ERROR ----
        /// BOOSTER.ks flies its whole boostback against `Impact(1, landProfile, LZ)` and stops when
        /// the predicted impact has overshot the pad by 2.7 km. Our old test multiplied a closing
        /// speed error by a time-to-ground - and during boostback the stage is CLIMBING, so the
        /// time-to-ground was meaningless and so was the answer.
        ///
        /// ---- WHAT THIS DELIBERATELY IS NOT ----
        /// F9I gets its impact point from the Trajectories add-on, which integrates DRAG. We cannot
        /// take that dependency, so this is a vacuum ballistic solve: constant gravity, no
        /// atmosphere, flat over the range involved. It therefore predicts LONG, because drag can
        /// only ever shorten a trajectory - which is the same direction as the deliberate 2.7 km
        /// overshoot and is why aiming long is safe. Do not "correct" it toward the truth without
        /// also revisiting BoostbackOvershootM; the two are a pair.
        /// </summary>
        private static double PredictedMiss(Vessel v)
        {
            if (!HavePad || v == null || v.mainBody == null) return 0.0;
            CelestialBody b = v.mainBody;

            Vector3d up = (v.CoM - b.position).normalized;
            Vector3d vel = v.srf_velocity;
            double vz = Vector3d.Dot(vel, up);
            double alt = v.altitude;
            double r = b.Radius + alt;
            double g = b.gMagnitudeAtCenter / (r * r);
            if (g <= 0.0) return 0.0;

            // alt + vz*t - g*t^2/2 = 0, taking the future root. Positive vz simply means it climbs
            // first, which is exactly the case the old estimate could not handle.
            double disc = vz * vz + 2.0 * g * alt;
            if (disc < 0.0) return 0.0;
            double t = (vz + Math.Sqrt(disc)) / g;
            if (t <= 0.0) return 0.0;

            Vector3d toImpact = Vector3d.Exclude(up, vel) * t;         // horizontal, from us
            Vector3d toLz = Vector3d.Exclude(up,
                b.GetWorldSurfacePosition(PadLat, PadLon, alt) - v.CoM);

            Vector3d err = toImpact - toLz;                             // LZ -> impact
            double miss = err.magnitude;
            // Sign: is the impact point on OUR side of the LZ (short) or past it? `-toLz` points
            // from the LZ back toward us, so a positive projection means short.
            return (Vector3d.Dot(err, -toLz) > 0.0) ? miss : -miss;
        }

        /// <summary>|PredictedMiss| latched when the boostback burn began, for the throttle taper.</summary>
        private static double initialMiss;

        /// <summary>
        /// Great-circle ground distance, metres.
        ///
        /// ⚠ NOT degrees times 111 320. `kerbin-degree-to-metres`: a Kerbin degree is 10 472 m, and
        /// hard-coding Earth's figure has cost this project real miss distances before. Using the
        /// body's OWN radius makes it right on any body without a table.
        /// </summary>
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

        /// <summary>
        /// Light exactly <paramref name="want"/> engines, choosing the ones NEAREST THE CENTRELINE.
        ///
        /// Falcon 9's single-engine landing burn is the CENTRE engine, and it has to be: an outboard
        /// engine alone would put the thrust vector off the centre of mass. Sorting by distance from
        /// the axis finds it without needing to know the part's name.
        /// </summary>
        private static void SetEngines(Vessel v, int want)
        {
            PartModule em = FindEngineSwitch(v);
            if (em != null) { SetOctawebMode(v, em, want); return; }
            SetEnginesIndividually(v, want);
        }

        // ------------------------------------------------------------------ the octaweb

        /// <summary>
        /// Drive the Tundra engine switch to the mode that flies <paramref name="want"/> engines.
        ///
        /// ---- WHY THIS IS NOT Activate()/Shutdown() ON INDIVIDUAL ENGINES ----
        /// It used to be, and it could not work: all nine Merlins are ONE part with three
        /// mutually exclusive ModuleEnginesFX on it. Sorting them by distance from the centreline
        /// sorted three modules at the SAME position - an arbitrary order - and then lit the first
        /// `want` of them, which could light AllEngines and ThreeLanding together.
        ///
        /// ---- STEP AND VERIFY, BECAUSE THE PART ONLY OFFERS "NEXT" ----
        /// There is no "set mode", only a one-way cycle, so reaching a mode means stepping and
        /// reading back. BOOSTER.ks:898 gives the reason for reading rather than counting: "a step
        /// that does not take would otherwise leave this script's idea of the octaweb permanently
        /// one place off the real one", and the landing solve is computed against the mode it
        /// believes it is in. F9I waits 0.3 s between steps; we have no `wait`, so the interval is
        /// enforced across ticks.
        ///
        /// The guard is one full cycle. Exceeding it means the part is not answering, and that is
        /// said out loud once - the landing that follows will probably be wrong and the recorder
        /// should not be the only place that knows.
        /// </summary>
        private static void SetOctawebMode(Vessel v, PartModule em, int want)
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

            int wantMode = VehicleParts.OctawebModeFor(want);
            if (wantMode != lastWantMode) { lastWantMode = wantMode; modeSteps = 0; }

            int now = ReadOctawebMode(em);
            if (now != wantMode && now >= 0)
            {
                double t = Planetarium.GetUniversalTime();
                if (t - lastModeStepAt >= ModeStepIntervalS && modeSteps <= 3)
                {
                    lastModeStepAt = t;
                    modeSteps++;
                    if (!InvokeAction(em, VehicleParts.EngineSwitchAction))
                        Debug.LogWarning(Tag + "engine switch has no '"
                                             + VehicleParts.EngineSwitchAction + "' action");
                    else
                        Debug.Log(Tag + "octaweb " + now + " -> stepping toward " + wantMode);
                }
                else if (modeSteps > 3 && !modeFailReported)
                {
                    modeFailReported = true;
                    Debug.LogError(Tag + "OCTAWEB WILL NOT REACH MODE " + wantMode
                                       + " (stuck in " + now + ") - the landing solve is being "
                                       + "computed against the wrong thrust");
                }
                return;                     // do not light anything until the mode is right
            }

            // Mode is correct (or unreadable, in which case light what is there and say so once).
            if (now < 0 && !modeFailReported)
            {
                modeFailReported = true;
                Debug.LogWarning(Tag + "cannot read the octaweb mode - lighting whatever is enabled");
            }

            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    bool mine = (now < 0) || VehicleParts.EngineIdIsMode(es[m].engineID, wantMode);
                    bool on = mine && !es[m].flameout;
                    if (on && !es[m].EngineIgnited) es[m].Activate();
                    else if (!on && es[m].EngineIgnited) es[m].Shutdown();
                }
            }
        }

        /// <summary>Seconds between mode steps. BOOSTER.ks:926 waits 0.3 s and reads back.</summary>
        private const double ModeStepIntervalS = 0.3;

        private static double lastModeStepAt;
        private static int modeSteps, lastWantMode = -1;
        private static bool modeFailReported;

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

        /// <summary>
        /// 0 = all nine, 1 = three, 2 = centre only, -1 = no answer.
        ///
        /// -1 is a DISTINCT case from any real mode and is treated as such rather than guessed at,
        /// exactly as OctaRead does (BOOSTER.ks:880). A Falcon Heavy side booster or a future variant
        /// may not carry the switch at all.
        /// </summary>
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

        private static bool InvokeAction(PartModule pm, string guiName)
        {
            try
            {
                for (int i = 0; i < pm.Actions.Count; i++)
                {
                    BaseAction a = pm.Actions[i];
                    if (a == null || !Same(a.guiName, guiName)) continue;
                    a.Invoke(new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate));
                    return true;
                }
            }
            catch (Exception e) { Debug.LogWarning(Tag + "action '" + guiName + "' threw: " + e.Message); }
            return false;
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

        /// <summary>
        /// The original behaviour, kept for any vehicle WITHOUT the mode switch: light exactly
        /// <paramref name="want"/> engines, choosing the ones nearest the centreline.
        ///
        /// Falcon 9's single-engine landing burn is the centre engine and it has to be - an outboard
        /// engine alone would put the thrust vector off the centre of mass. Sorting by distance from
        /// the axis finds it without needing to know the part's name.
        /// </summary>
        private static void SetEnginesIndividually(Vessel v, int want)
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

            Vector3 axis = v.ReferenceTransform.up;
            Vector3 com = v.CoM;
            all.Sort(delegate (ModuleEngines a, ModuleEngines b)
            {
                return Off(a, com, axis).CompareTo(Off(b, com, axis));
            });

            for (int i = 0; i < all.Count; i++)
            {
                bool on = i < want && !all[i].flameout;
                if (on && !all[i].EngineIgnited) all[i].Activate();
                else if (!on && all[i].EngineIgnited) all[i].Shutdown();
            }
        }

        private static float Off(ModuleEngines e, Vector3 com, Vector3 axis)
        {
            if (e == null || e.part == null) return float.MaxValue;
            return Vector3.ProjectOnPlane(e.part.transform.position - com, axis).sqrMagnitude;
        }

        // ------------------------------------------------------------------ grid fins

        /// <summary>
        /// Put the grid fins out, once.
        ///
        /// The action is a TOGGLE, so the latch is not an optimisation - it is what stops a second
        /// call folding the fins back in during entry, and BOOSTER.ks:941 is explicit that the cost
        /// of losing it is the stage. The Progress test is the belt to that braces: it also covers a
        /// stage whose fins were already deployed by hand, the same way F9I's DeployLegs checks each
        /// leg's state rather than trusting a flag.
        ///
        /// Matched on ANIMATION NAME rather than part name, as FlightCommands does for the nose cone.
        /// Once they are out they also start contributing real torque, because a grid fin carries
        /// ModuleControlSurface and AttitudeController now counts every ITorqueProvider.
        /// </summary>
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
                    if (mods[m].Progress > 0.5f) continue;      // already out
                    mods[m].Toggle();
                    n++;
                }
            }
            Debug.Log(Tag + "grid fins deployed (" + n + ")");
            if (n == 0)
                Debug.LogWarning(Tag + "no grid fins found - looked for ModuleAnimateGeneric '"
                                     + VehicleParts.GridFinAnimation + "'. Entry will have no "
                                     + "aerodynamic authority.");
        }

        private static bool gridFinsOut;

        private static void Aim(Vessel v, LandingCommand c, LandingInputs s)
        {
            Vector3d up = (v.CoM - v.mainBody.position).normalized;
            Vector3d dir = up;

            // ---- ⛔ Hold DID NOT HOLD - IT POINTED THE STAGE STRAIGHT UP. ----
            // `dir` starts as the local vertical and only the two branches below ever changed it, so
            // LandingAim.Hold - whose entire job is "do not slew" - commanded a slew to vertical. At
            // a 45-degree separation attitude that is a 45-degree rotation for no reason.
            //
            // The controller's forward IS ReferenceTransform.up: its frame is
            // rotation * Euler(-90,0,0), and -90 about X maps +Z onto +Y, which is the axis out of a
            // KSP command part's nose. Steering at the current facing is therefore a true hold - zero
            // attitude error, nothing commanded.
            if (c.Aim == LandingAim.Hold) dir = v.ReferenceTransform.up;

            if (c.Aim == LandingAim.SurfaceRetrograde)
            {
                Vector3d srf = v.srf_velocity;
                if (srf.sqrMagnitude > 1.0) dir = -srf.normalized;
            }
            else if (c.Aim == LandingAim.TowardTarget && HavePad)
            {
                // ---- ⛔ PURELY HORIZONTAL, TOWARD THE LZ. THE 20-DEGREE PITCH-UP WAS INVENTED. ----
                // BOOSTER.ks:452 is exactly this and nothing more:
                //     lock BBvec to vxcl(up:vector, LZ:altitudeposition(ship:altitude)):normalized.
                // The pitch-up was my own idea about what "the real flip-and-burn looks like", and
                // in the 22:18 recording it drove vertical speed UP from 676 to 828 m/s across the
                // burn - the booster gained 13 km of altitude during a manoeuvre whose entire job is
                // to reverse horizontal velocity. Every metre of that had to be paid for twice.
                Vector3d toPad = (v.mainBody.GetWorldSurfacePosition(PadLat, PadLon, v.altitude)
                                  - v.CoM);
                Vector3d horiz = Vector3d.Exclude(up, toPad);
                if (horiz.sqrMagnitude > 1.0) dir = horiz.normalized;
            }

            // ---- ⛔ ISSUE 5. LandingZoneGuidance WAS PORTED AND NEVER CONNECTED. ----
            // `Landing.LeanFraction` and `Landing.GuidanceAoaDeg` were written last session, tested,
            // documented as DONE in the port map - and referenced from nowhere. The descent flew
            // plain retrograde, so the booster had NO steering toward the pad at all and the whole
            // landing-accuracy port was doing nothing.
            //
            // The lean is retrograde plus tan(AoA) x errScale toward the miss, exactly as
            // BOOSTER.ks:571. The AoA SIGN FLIPS under thrust - positive works aerodynamically,
            // negative is needed once the force is along the nose - which is why it is asked for
            // per-tick with the engine state rather than held as a constant.
            // ⛔ AND ONLY WHEN THE GUIDANCE SAYS SO. This used to lean on any retrograde aim, which
            // includes the ENTRY BURN - full thrust into the thickest air with an angle of attack on
            // top. BOOSTER.ks:716 forbids exactly that; see LandingCommand.GuidedLean.
            if (HavePad && c.GuidedLean && c.Aim == LandingAim.SurfaceRetrograde && s.DownrangeM > 0.0)
            {
                Vector3d toPad = v.mainBody.GetWorldSurfacePosition(PadLat, PadLon, v.altitude)
                                 - v.CoM;
                Vector3d errHoriz = Vector3d.Exclude(up, -toPad);   // from the pad toward us = miss
                if (errHoriz.sqrMagnitude > 1.0)
                {
                    double aoa = Landing.GuidanceAoaDeg(s.AltitudeRadar, c.Throttle > 0.01);
                    double lean = Landing.LeanFraction(s.DownrangeM, aoa);
                    AoaDeg = aoa; LeanFrac = lean;
                    dir = (dir.normalized + lean * errHoriz.normalized).normalized;
                }
            }

            // Same controller the ascent uses. A landing booster wants a TIGHT stopping time -
            // BOOSTER.ks sets maxstoppingtime 0.05 for the landing burn against 1 for the coast,
            // because the two want opposite behaviour and one setting cannot serve both.
            // ---- THE GAIN IS THE GUIDANCE'S CHOICE, NOT A TERNARY HERE ----
            // F9I retunes maxstoppingtime three times down the descent: 10 through the entry burn so
            // the controller does not fight the airflow, 1 for the glide, 0.05 for the landing burn.
            // This only knew about the last one.
            AttitudeController.Booster.MaxStoppingTime =
                (c.StoppingTime > 0.0) ? c.StoppingTime : Landing.GlideStoppingTime;

            // ---- ⛔ ROLL IS HELD, NOT COMMANDED. THE GRID FINS DEPEND ON IT. ----
            // Passing the local vertical as the roll reference rolls the stage to put its "top"
            // along it - which turns the grid fins out of the plane they were built to work in.
            // BOOSTER.ks:315: the flip "blends the roll reference by tgtRotation so the booster keeps
            // the same roll it launched with and the grid fins stay in the plane they expect."
            //
            // Zero here means "no roll reference", and SteerTo then holds the roll the stage already
            // has - which is the launch roll, because nothing has commanded it since.
            AttitudeController.Booster.SteerTo(v, dir, Vector3d.zero);
        }
    }
}
