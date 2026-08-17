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

        /// <summary>
        /// How far the booster's TOTAL attitude error may be and still have its roll axis actively
        /// held, degrees. The controller default is 45 (`Attitude.RollControlRangeDeg`), but the
        /// booster's coast runs a ~52 deg pitch error, so at 45 the roll axis went uncontrolled for
        /// most of the coast and aero torque free-rolled it 367 deg. Wider keeps roll held throughout
        /// so the stage does not re-clock after separation. Tunable - if the flip or descent looks
        /// worse, dial it back toward 45.
        /// </summary>
        [Tunable] public static double BoosterRollRangeDeg = 130.0;

        public static bool Active { get; private set; }
        public static LandingPhase Phase { get; private set; }
        public static LandingCommand Command;

        private static Vessel booster, upperStage;

        /// <summary>
        /// Universal time the post-landing settle ends. Zero when not settling.
        ///
        /// See the hold in <see cref="Finish"/> for why this is a deadline checked on later ticks
        /// rather than a wait: `Finish` runs inside the flight driver, and blocking there stops the
        /// upper stage flying too.
        /// </summary>
        private static double settleUntil;
        /// <summary>Consecutive failed booster searches. See the quiet-first-look note.</summary>
        private static int noBoosterLooks;
        /// <summary>Searches to stay quiet for - separation takes about 2 s at 5 Hz.</summary>
        private const int NoBoosterQuietLooks = 15;

        /// <summary>
        /// True while the booster is down and standing before the camera leaves.
        ///
        /// ⛔ THE CALLER MUST TICK ON THIS AS WELL AS ON `Active`. `AutoPilot` guards the tick with
        /// `if (BoosterRecovery.Active)`, and `Finish` clears `Active` - so the first version of this
        /// hold set a deadline that nothing would ever come back to check, and the camera would have
        /// stayed on a landed booster for the rest of the flight. Exactly the failure CLAUDE.md
        /// records twice: the transition was right and the branch was unreachable.
        /// </summary>
        public static bool Settling { get { return settleUntil > 0.0; } }

        /// <summary>
        /// Seconds the booster stands on the pad before the camera goes back. User's call,
        /// 2026-08-12. Long enough for the legs to take the load and the engine to spool down.
        /// </summary>
        public const double SettleAfterLandingS = 10.0;

        /// <summary>
        /// The booster we are flying, for anything that needs to look at it rather than fly it.
        ///
        /// Read-only on purpose: `HullCams` renders its interstage cameras while it is still loaded,
        /// and a second owner of this reference is exactly the kind of thing that ends up steering.
        /// </summary>
        internal static Vessel Tracked { get { return booster; } }
        private static double startedAt;

        /// <summary>The booster, for the recorder. Null when no recovery is running.</summary>
        public static Vessel BoosterVessel { get { return booster; } }

        /// <summary>When the CURRENT landing phase began. The entry burn's soft start is timed off it.</summary>
        private static double phaseStartedAt;

        private static bool noBoosterReported;
        private static bool packedReported;

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
            handedOverToOne = false;
            settleUntil = 0.0;
            burnLatchedEngines = 0; lastBurnPhase = LandingPhase.Idle;
            noBoosterLooks = 0;
            phaseStartedAt = 0.0; noBoosterReported = false; packedReported = false;
            TrueRadar = 0.0; DownrangeM = 0.0; initialMiss = 0.0;
            PredictedMissM = 0.0; InitialMissM = 0.0;
            RangeToPartnerM = 0.0; PhaseElapsedS = 0.0; OctaMode = -1; EnginesLit = 0;
            GridFinsOut = false; LeanFrac = 0.0; AoaDeg = 0.0;
            flipSeeded = false; flipComplete = false;
            rollStartedAt = 0.0; rollSettledAt = 0.0;
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
                // ⚠ AND NOT ON THE FIRST LOOK. This fires at MECO, about two seconds before
                // separation completes, on every flight - four in a row - and then finds the
                // booster immediately afterwards. A warning that is wrong more often than it is
                // right trains the reader to skip it, which is the one thing a warning must not do.
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

            // Remember the upper stage's normal ranges so they can be put back on handback - the
            // extended range is only wanted WHILE focus is away on the booster. See RestoreRanges.
            SnapshotRanges(upperStage);
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

        // ---- ⛔ THE EXTENDED RANGE IS ON ONLY WHILE FOCUS IS AWAY ON THE BOOSTER. ----
        // With PhysicsRangeExtender installed the 1500 km ask is honoured, so the upper stage stays
        // LOADED and keeps flying itself through the whole recovery instead of unloading at ~300 km
        // and coming back a rebooted vessel (which is what disengaged the ascent and lost the orbit).
        // But a 1500 km range on the active vessel is a standing cost - it loads everything else in
        // the system too, the station included, long before anything needs it. So the range is a
        // LOAN: snapshot the normal ranges when we take the booster, put them back on handback.
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
            // The post-landing settle runs even though the recovery is finished flying: the booster
            // is down but the camera has not left yet. Checked first so it cannot be skipped by any
            // of the early returns below.
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

            // ---- ⛔ A PACKED STAGE MUST NOT ADVANCE ITS OWN PHASE MACHINE. ----
            // The packed test used to sit BELOW `Landing.Guide` and below `Phase = c.Phase`, so a
            // booster on rails walked its whole descent in the log while nothing was applied to it -
            // a flight that reads as flown and did nothing. Nothing we write reaches a packed vessel
            // (`falcon-physics-range-clamp`), so the honest thing is to freeze and wait for it to
            // reload, which is what F9I sees too.
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

            // ---- FINS OUT AT THE TOP OF THE ARC, NOT AT THE ENTRY BURN ----
            // F9I's AtmGNC opens with "grid fins out, entry burn, guided descent" - the fins are out
            // BEFORE the gate at 32.5 km, so the stage is already stable when it meets the thick air
            // rather than deploying into it.
            // ⚠ AND NOT WHILE SEPARATING. The gate listed Idle and Boostback and I added a phase
            // before both without revisiting it, so the fins would have deployed at 29 km, climbing
            // at 700 m/s, 11 m from the upper stage. They belong at the top of the arc.
            // ---- FINS OUT ON THE DESCENT, NOT ON A PHASE ----
            // AtmGNC:667 deploys them on the -50 m/s transition, immediately after the arc:
            // "they are the only control authority that works before the engines relight, and they
            // need to be out before the air arrives, not when it does." Our gate was a list of
            // phases, and Coast begins while the stage is still CLIMBING - so the fins came out
            // going up, into vacuum, with the whole arc still to fly.
            if (s.VerticalSpeed <= Landing.ArcOverVs) DeployGridFins(booster);

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

            // RCS follows the guidance: on for the coast, off for the landing burn. AtmGNC:664
            // `rcs on.` and Land:780 `rcs off.` - "below here the gimbal has all the authority
            // needed and the cold gas is only spending propellant to fight it", and that propellant
            // is the landing's.
            if (booster.ActionGroups[KSPActionGroup.RCS] != c.Rcs)
                booster.ActionGroups.SetGroup(KSPActionGroup.RCS, c.Rcs);
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

            // ---- LET THE STAGE SETTLE ON ITS LEGS BEFORE THE CAMERA LEAVES. ----
            // User's call, 2026-08-12: *"we also need to settle the booster after landing it for 10
            // seconds before switching back to the upper stage."* The focus change used to happen on
            // the same tick as TOUCHDOWN, so the one moment the whole recovery exists for went past
            // in a frame - and a stage still absorbing its landing, with legs compressing and the
            // engine spooling down, is exactly when a hop or a tip would show.
            //
            // ⚠ A HOLD, NOT A SLEEP. `Finish` runs inside the flight driver's tick, so blocking
            // here would block the upper stage too. The recovery marks the touchdown time and the
            // handback is checked on later ticks by `SettleTick`, which is why `Active` stays true
            // through the hold - the booster is still ours until we let go of it.
            if (upperStage != null && upperStage.state != Vessel.State.DEAD
                && why == "booster down" && settleUntil <= 0.0)
            {
                settleUntil = Planetarium.GetUniversalTime() + SettleAfterLandingS;
                Debug.Log(Tag + "settling on the pad for " + SettleAfterLandingS.ToString("F0")
                          + " s before the camera goes back to the upper stage");
                return;
            }

            // Hand focus back so the crew can watch the vehicle that still has somewhere to be. The
            // upper stage has been flying itself throughout - this is a camera move, not a handover.
            if (upperStage != null && upperStage.state != Vessel.State.DEAD)
            {
                FlightGlobals.ForceSetActiveVessel(upperStage);

                // ---- ⛔ RE-ASSERT CONTROL AFTER THE FORCED SWITCH. ----
                // `ForceSetActiveVessel` onto a vessel whose IVA was despawned while it was inactive
                // (KSP `Part.DespawnIVA`) leaves the crew unable to steer, throttle or enter IVA
                // until a FULL SCENE RELOAD - which is the Tracking Center round-trip the crew was
                // doing by hand, and which itself ends the flight (`FlightDriver.OnDestroy`
                // disengages the autopilot, so the ascent then has to be restarted and re-runs its
                // staging). Measured 2026-08-17. Doing here what that round-trip does:
                //   · clear any control locks the switch left set, so the stick and throttle answer;
                //   · reset the camera to flight, so the IVA the recovery despawned can be entered.
                // Guarded: this runs during a scene transition, and a throw here would abort the
                // handback and strand focus on the booster.
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

                // Give the upper stage its normal physics range back - the extended range was a loan
                // for the recovery window, and it is the active vessel now, so it needs no reach.
                RestoreRanges(upperStage);

                Debug.Log(Tag + "focus -> '" + upperStage.vesselName + "' (it never stopped flying)");

                // ⛔ RECOVER THE LANDED BOOSTER SO IT STOPS STEALING THE CAMERA. With PhysicsRange
                // Extender keeping it loaded, KSP kept flipping focus between the down booster and the
                // Dragon in orbit for the rest of the flight (crew report 2026-08-17). A recovered
                // booster is gone from the world, so there is nothing to flip to - and it is the real
                // end of the recovery: it is down, settled, and ours no longer.
                RecoverBooster();
            }
        }

        /// <summary>
        /// Recover the landed booster through KSP's own recovery, or failing that remove it, so it
        /// cannot keep stealing focus from the returning capsule. Only ever the booster, only once it
        /// is down, and never the active vessel (KSP refuses to recover the one you are flying).
        /// </summary>
        private static void RecoverBooster()
        {
            if (booster == null || booster.state == Vessel.State.DEAD) return;
            if (booster == FlightGlobals.ActiveVessel) return;
            if (!booster.LandedOrSplashed) return;
            try
            {
                GameEvents.OnVesselRecoveryRequested.Fire(booster);
                Debug.Log(Tag + "booster recovered - removed from the flight so it cannot steal focus");
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "booster recovery request failed (" + e.Message
                                 + ") - removing it instead");
                try { booster.Die(); } catch (Exception e2) { Debug.LogWarning(Tag + e2.Message); }
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
            s.FlipDone = flipComplete;
            // The 0.03 test is on the UNIT retrograde's horizontal part - not a speed. It reaches
            // zero when the velocity has become purely vertical, i.e. downrange travel has stopped.
            Vector3d bUp = (v.CoM - v.mainBody.position).normalized;
            Vector3d retro = v.srf_velocity;
            s.HorizRetroMag = (retro.sqrMagnitude > 1.0)
                ? Vector3d.Exclude(bUp, retro.normalized).magnitude : 0.0;
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

            // ---- THE SAME PREDICTION THE GUIDANCE STEERS ON. See ImpactPointHoriz. ----
            // This readout and the descent guidance used to solve this separately, and only this one
            // asked ImpactPredictor. They cannot disagree now.
            Vector3d toImpact;
            if (!ImpactPointHoriz(v, up, alt, out toImpact)) return 0.0;

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
        /// The horizontal vector from the landing zone to where this stage will actually come down.
        ///
        /// ---- ⛔ ONE PREDICTION, NOT TWO. THIS FILE HAD BOTH AND THE GUIDANCE USED THE WRONG ONE. ----
        /// `PredictedMiss` already prefers `ImpactPredictor.Predict` whenever the vehicle has told us
        /// its drag, falling back to a vacuum parabola only when it has not. This function - the one
        /// the DESCENT GUIDANCE steers on - was a second, private copy of the fallback, and it never
        /// asked the predictor at all. So the number on the screen and the number being flown were
        /// computed by different physics, in the same class, forty lines apart.
        ///
        /// A vacuum solve in atmosphere predicts LONG and, worse, predicts UNSTEADILY: it takes the
        /// current horizontal velocity and multiplies by a free-fall time, so every metre per second
        /// the airflow removes swings the answer by tens of metres. Measured on 2026-08-12, through
        /// the descent, with the stage's ballistic coefficient sitting in the recorder the whole time
        /// at `r_bcBooster` 1116-1516:
        ///
        ///     predicted miss oscillated 45 m -> 287 m -> 45 m -> 248 m on a ~5 second cycle
        ///     it is SMOOTH, not noisy - 11 m per 0.2 s sample - so this is a real closed loop
        ///     each pass through zero REVERSES `errHoriz`, flipping the commanded lean side to side
        ///     attitude error jumped 8 deg -> 27 deg in ONE sample as the command flipped
        ///     pitch actuation saturated 35% of the descent, yaw 44%
        ///
        /// The stage was chasing a phantom: it landed 0.0 km downrange while being told it was
        /// 231 m out on average. CLAUDE.md check #2 - a rule a second place needs goes in ONE
        /// function both callers use - in its most literal form.
        /// </summary>
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
            return toImpact - toLz;                      // LZ -> impact
        }

        /// <summary>
        /// Where this stage comes down, as a horizontal offset from it. The integrated prediction
        /// when drag has been measured, the vacuum parabola when it has not.
        ///
        /// ⚠ THE FALLBACK IS STILL A FALLBACK AND IT STILL PREDICTS LONG. That is deliberate and
        /// paired with `BoostbackOvershootM` - see `PredictedMiss`. What is NOT acceptable is using
        /// it while a measured coefficient sits unused, which is what the descent guidance did.
        /// </summary>
        private static bool ImpactPointHoriz(Vessel v, Vector3d up, double alt, out Vector3d toImpact)
        {
            toImpact = Vector3d.zero;
            CelestialBody b = v.mainBody;

            Impact im = ImpactPredictor.Predict(v);
            if (im.Valid && im.DragModelled)
            {
                Vector3d impactPos = b.GetWorldSurfacePosition(im.LatDeg, im.LonDeg, alt);
                toImpact = Vector3d.Exclude(up, impactPos - v.CoM);
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

        /// <summary>
        /// The controller's TOP vector.
        ///
        /// ⚠ NOT `ReferenceTransform.forward`, which is MINUS it. That substitution is what span
        /// the stack at 64 deg/s on the pad; see AttitudeController.SteerTo's note.
        /// </summary>
        private static Vector3d TopVector(Vessel v)
        {
            QuaternionD rot = (QuaternionD)(v.ReferenceTransform.rotation
                                            * Quaternion.Euler(-90f, 0f, 0f));
            return rot * Vector3d.up;
        }

        /// <summary>
        /// Walk the aim vector round toward the final attitude and return where to point NOW.
        ///
        /// `flipAxis` is perpendicular to both the ground track and "down", so rotating about it
        /// swings the nose through the vertical PLANE OF FLIGHT and never yaws the stage sideways
        /// (BOOSTER.ks:335). `flipFinal` is the ground track reversed by FlipDeg - 180 for RTLS,
        /// 170 for a droneship, which only needs to point back far enough to trim.
        /// </summary>
        private static Vector3d StepFlip(Vessel v)
        {
            Vector3d up = (v.CoM - v.mainBody.position).normalized;

            if (!flipSeeded)
            {
                // ---- ⛔ THE GEOMETRY IS IN pure/FlipGeometry.cs AND IT IS THERE FOR A REASON. ----
                // This block used to build the tangent from `v.srf_velocity` - PROgrade - where F9I
                // uses `srfretrograde`. Both then negate and rotate 180°, so the flip finished flat
                // PROGRADE, exactly reversed, and BoostbackKill inherited a 149.7° error at the
                // instant three Merlins reached full throttle. The stage tumbled, burned 24 t, drove
                // itself 15 km further downrange and was lost. Do not re-derive this here.
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
                // Seeded from where the stage is ACTUALLY pointing, not from a fresh command:
                // WaitForSep refreshes flipVec at 10 Hz for exactly this reason, so the rotation
                // starts from the attitude the booster was flying at MECO.
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
            // `FlipRollToleranceDeg`, `FlipRollMinS` and `FlipRollMaxS` were transcribed from Flip1
            // and read by nothing - the same trap as `FlipDeg`, which was documented "NOT WIRED"
            // while being called. F9I's reason, verbatim: "Rolling and pitching at once on a stage
            // with this much inertia cross-couples and the flip wanders off the plane of flight.
            // Three guards, all needed: the tolerance (10 deg is as good as it gets without burning
            // RCS for nothing), a 1 s floor so a lucky first frame does not count as settled, and an
            // 8 s ceiling so a stage that will not roll still gets flipped rather than sitting here
            // until it hits the sea."
            //
            // Holding `flipVec` still is what "do not pitch yet" means here: the aim vector simply
            // does not advance, so the controller keeps chasing the attitude it already has while
            // the roll axis converges.
            if (rollSettledAt <= 0.0)
            {
                double nowUt = Planetarium.GetUniversalTime();
                double inRoll = nowUt - rollStartedAt;

                // The roll reference the flip will use: the stage's top should lie in the plane of
                // flight, i.e. perpendicular to the rotation axis.
                // The same target the controller is given above, resolved the same way SteerTo
                // resolves it - so the gate cannot pass while the controller disagrees, or wait on
                // something the controller was never asked for.
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
                else return flipVec;          // hold the aim; do not start walking it round yet
            }

            double toGo = Vector3d.Angle(flipFinal, flipVec);
            if (toGo < Landing.FlipFineDeg)
            {
                // Snap to the exact final attitude - the stepping only ever gets within 15 deg, and
                // the boostback needs a clean reference, not wherever the last step landed.
                flipVec = flipFinal;
                if (!flipComplete)
                {
                    flipComplete = true;
                    Debug.Log(Tag + "flip complete");
                }
                return flipVec;
            }

            // Coarse: only advance when the NOSE has caught up. Fine (last 25 deg): advance every
            // tick, because by then the rate is established and waiting only makes the finish crawl.
            bool coarse = toGo >= Landing.FlipCoarseDeg;
            bool noseCaught = Vector3d.Angle(v.ReferenceTransform.up, flipVec)
                              < Landing.FlipNoseCatchDeg;
            if (!coarse || noseCaught)
                flipVec = (QuaternionD)Quaternion.AngleAxis((float)Landing.FlipPowerDeg,
                                                            (Vector3)flipAxis) * flipVec;
            return flipVec;
        }

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

            // ---- ⛔ THE 3->1 HANDOVER LATCHES. IT MUST NOT BE RE-DECIDED PER TICK. ----
            // `HandoverReady` tests vertical speed against -40 and a stopping distance that both
            // change fast in the last seconds, so the answer flickers - and on 2026-08-11 the mode
            // machine chased it: "octaweb 1 -> stepping toward 2" then "2 -> stepping toward 1",
            // 0.3 s apart, in the last second before touchdown. F9I calls EngSwitch ONCE at the
            // handover point. Once the stage is on the centre engine it stays there; there is no
            // altitude at which going back to three is the right answer.
            // ⚠ SCOPED TO THE LANDING BURN, AND THE CHURN IS IN THE ENTRY BURN. On three
            // consecutive flights the mode went 1 -> 2 -> 0 -> 1 inside ENTRY BURN, a full wasted
            // round trip mid-burn (2026-08-11 16:08:37, 2026-08-12 09:37:33). The latch only ever
            // covered the last phase. A mode change during a burn is never free - it steps the
            // one-way cycle through modes the burn did not ask for - so the rule is now: once a
            // BURN has settled on an engine count, it keeps it for the rest of that burn.
            // ⛔ THE LATCH IS ON THE ENTRY BURN ONLY. LATCHING THE LANDING BURN BROKE THE 3->1.
            // My previous version latched the engine count for BOTH burns, which made
            // `want <= 1` structurally unreachable in the landing burn - so the handover to the
            // centre engine could never fire and 2026-08-12 flew the whole landing on three
            // (`b_engines = 3` for all 94 rows). Real Falcon 9 lands on one, and F9I's 2.23 ratio
            // handover at `BOOSTER.ks:801` is the thing that decides when.
            //
            // The churn being guarded against was always in the ENTRY burn - 1 -> 2 -> 0 -> 1 on
            // three consecutive flights - and the landing burn's own `handedOverToOne` latch below
            // already stops it oscillating once it has committed to the centre engine. So the two
            // burns get the two different rules they actually need.
            if (Phase != lastBurnPhase) { burnLatchedEngines = 0; lastBurnPhase = Phase; }
            if (Phase == LandingPhase.EntryBurn)
            {
                if (burnLatchedEngines == 0) burnLatchedEngines = want;
                else want = burnLatchedEngines;
            }
            if (Phase == LandingPhase.LandingBurn && want <= 1) handedOverToOne = true;
            if (handedOverToOne && Phase == LandingPhase.LandingBurn) want = 1;

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
        private static bool handedOverToOne;
        /// <summary>Engine count a burn settled on, held for that burn. See the churn note.</summary>
        private static int burnLatchedEngines;
        private static LandingPhase lastBurnPhase;
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

            // ---- THE STEPPED TURNAROUND. BOOSTER.ks:295-381 Flip1. ----
            // The aim vector is WALKED round, not commanded in one go: "A single big command makes
            // the steering manager saturate and the stage tumbles; stepping keeps the demand inside
            // what RCS and gimbal can actually deliver." Coarse phase advances only when the nose
            // has caught up to within 7.5 deg, so the stage leads the target rather than the other
            // way round - that is what stops the flip diverging.
            if (c.Aim == LandingAim.Flip) dir = StepFlip(v);

            // Retrograde FLATTENED ONTO THE HORIZON. BOOSTER.ks:417 - while the stage is killing
            // downrange velocity it must not pitch down at the ground.
            if (c.Aim == LandingAim.FlatRetrograde)
            {
                Vector3d flat = Vector3d.Exclude(up, v.srf_velocity);
                if (flat.sqrMagnitude > 1.0) dir = -flat.normalized;
            }

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
                // ---- THE ERROR IS WHERE IT WILL LAND, NOT WHERE IT IS. ----
                // LandingZoneGuidance:553  `local errorVec is impactPos:position - LZ:position.`
                // where impactPos is the PREDICTED impact point. Ours used the stage's CURRENT
                // horizontal offset from the pad, which is a different quantity entirely: it asks a
                // vehicle moving at hundreds of metres per second to be OVER the pad now, instead of
                // asking it to arrive there. On a descent from 30 km those two vectors can point in
                // opposite directions, and steering on the wrong one is why the landings missed.
                Vector3d errHoriz = ImpactErrorHoriz(v);
                if (errHoriz.sqrMagnitude > 1.0)
                {
                    // handedOver: on one engine the stage stops steering and stands up (-0.25 deg).
                    double aoa = Landing.GuidanceAoaDeg(s.AltitudeRadar, c.Throttle > 0.01,
                                                        c.Engines == 1);

                    // ================================================================================
                    //  ⛔ THE REBUILD IS CONDITIONAL. THE AoA IS A CEILING, NOT A DEMAND.
                    //
                    //  `BOOSTER.ks:360-364`, the whole of it:
                    //
                    //      local result is velVec + errorVec.
                    //      if (vAng(result, velVec) > F9L_AOA) {
                    //          set result to velVec:normalized
                    //                      + tan(F9L_AOA) * F9L_ErrScale * errorVec:normalized.
                    //      }
                    //
                    //  So F9I NORMALLY FLIES THE NAIVE SUM. `velVec` is the retrograde vector at full
                    //  speed - hundreds of m/s - and `errorVec` is an impact error in metres, so the
                    //  sum leans by `atan(error / speed)`: a few degrees for a small miss, growing
                    //  smoothly as the miss grows. The rebuild fires ONLY when that natural lean
                    //  already exceeds the allowed angle, and then it clamps it back to exactly the
                    //  allowed angle. The angle of attack is a LIMITER on a proportional law.
                    //
                    //  We had no conditional at all: every tick commanded `tan(aoa) * scale`, the
                    //  full clamped lean, whatever the error. Below 4 km `aoa` is `alt/100` - forty
                    //  degrees at 4 km - so the guidance demanded a 40 degree lean on a stage that
                    //  needed two. Measured on 2026-08-12: `b_leanFrac` reached 0.838, which is
                    //  tan(40), with pitch actuation saturated 35% of the descent and yaw 44%. That
                    //  is the side-to-side swinging, and it got WORSE after the previous change
                    //  because the previous change only touched the scale.
                    //
                    //  ⚠ MY OWN COMMENT ON `LeanFraction` TALKED ME OUT OF THIS. It described the
                    //  conditional correctly and then said the rebuild happens "nearly always, because
                    //  the naive sum compares metres of error against metres per second of velocity
                    //  and is meaningless when the error is large". The second half is true and the
                    //  "nearly always" was an assumption I never checked: at 250 m/s a 100 m error
                    //  leans 21.8 degrees, well under the ceiling, so the rebuild does NOT fire and
                    //  the naive sum is exactly the law being flown. Port rule 2, in one line - I took
                    //  F9I's constant and wrote my own mechanism around it.
                    // ================================================================================
                    Vector3d velVec = -v.srf_velocity;                 // BOOSTER.ks:355
                    Vector3d naive = velVec + errHoriz;                // :360, metres + m/s, as flown
                    AoaDeg = aoa;

                    // ---- ⚠ THE TWO BRANCHES MUST MEET AT THE CEILING, OR THE COMMAND STEPS. ----
                    // F9I's rebuild is `velVec:normalized + tan(AOA) * ErrScale * err:normalized`
                    // and its naive sum is `velVec + errorVec`. Those agree at the switch point ONLY
                    // when ErrScale is 1; inside the 5 m deadband ErrScale tapers and the rebuild
                    // drops below the naive lean, so crossing the boundary jumps the command.
                    //
                    // Ours crossed it repeatedly. Measured 2026-08-12 through the descent, the
                    // commanded lean collapsed and recovered - 0.268, 0.167, 0.060, 0.074, 0.033,
                    // 0.005, 0.019 - between 20 km and 13 km, which is exactly the band the crew
                    // reported as unstable and exactly where the impact error crosses zero. Below
                    // 12 km the error stays one side of zero and it settles.
                    //
                    // Taking the SMALLER of the two removes the step: the rebuild is a ceiling, so
                    // below the ceiling the naive lean is already smaller and wins, and at the
                    // ceiling they are equal by construction. The command is now continuous through
                    // the crossing and still never exceeds the angle of attack.
                    if (naive.sqrMagnitude > 1e-6)
                    {
                        double naiveLean = Math.Tan(Vector3d.Angle(naive, velVec) * Math.PI / 180.0);
                        double ceiling = Landing.LeanFraction(errHoriz.magnitude, aoa);
                        double lean = (naiveLean < ceiling) ? naiveLean : ceiling;
                        LeanFrac = lean;
                        dir = (velVec.normalized + lean * errHoriz.normalized).normalized;
                    }
                }
            }

            // Same controller the ascent uses. A landing booster wants a TIGHT stopping time -
            // BOOSTER.ks sets maxstoppingtime 0.05 for the landing burn against 1 for the coast,
            // because the two want opposite behaviour and one setting cannot serve both.
            // ---- THE GAIN IS THE GUIDANCE'S CHOICE, NOT A TERNARY HERE ----
            // F9I retunes maxstoppingtime three times down the descent: 10 through the entry burn so
            // the controller does not fight the airflow, 1 for the glide, 0.05 for the landing burn.
            // This only knew about the last one.
            // ---- ⛔ THE FOUR kOS SCALE FACTORS ARE GONE. ONE TIME CONSTANT REPLACES THEM. ----
            // `maxstoppingtime`, `pitchts`, `rollts` and `rolltorquefactor` were kOS's knobs, and
            // porting them was the mistake: eleven changes to this block in one session, three of
            // them with the direction of the effect backwards. `pure/Attitude.cs` derives the rate
            // bound from the vehicle's own torque and inertia, so there is no per-phase tuning to
            // get wrong - the flip, the glide and the landing burn all use the same law and the
            // law works out their differences from the vehicle.
            //
            // What remains is a TIME: how long to take arresting a rate error. The landing burn
            // wants to be crisp about it, everything else does not.
            AttitudeController.Booster.TimeConstantS =
                (c.Phase == LandingPhase.LandingBurn) ? 0.35 : Attitude.DefaultTimeConstantS;

            // ---- AND THE RATE CEILING FOR THIS PHASE. EVERY FIGURE MEASURED. ----
            // F9I's own peaks over bb_booster_001..008 - the vehicle that lands half a metre from
            // the pad. A ceiling, not a target: the flip needs 15 deg/s of authority and the
            // landing burn needs almost none, and asking for more than the reference vehicle ever
            // used is how an axis saturates.
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

            // ---- ⛔ AND FROM THE GLIDE DOWN, ROLL IS COMMANDED. F9I COMMANDS IT TOO. ----
            // The note below is right about the FLIP and was then applied to the whole descent,
            // which F9I does not do. `LandingZoneGuidance` - the function this guidance is a port of
            // - ends every call with a roll reference:
            //
            //     BOOSTER.ks:365  if F9L_LandingRoll < 0 { return lookdirup(result, facing:topvector). }
            //     BOOSTER.ks:366  local _rollRef is heading(F9L_LandingRoll, 0):vector.
            //     BOOSTER.ks:367  if vang(result, _rollRef) < 15  { return lookdirup(result, facing:topvector). }
            //     BOOSTER.ks:368  if vang(result, _rollRef) > 165 { return lookdirup(result, facing:topvector). }
            //     BOOSTER.ks:369  return lookdirup(result, _rollRef).
            //
            // with `F9L_LandingRoll is 0`, so the reference is heading 000 - HORIZONTAL NORTH. That
            // is not the local vertical the warning below is about, and it does not turn the fins out
            // of plane; it pins them to a fixed plane against the ground instead of letting the roll
            // wander wherever the last disturbance left it. "Holding" the launch roll is passive:
            // nothing commands it back, so every gust accumulates, which is the rolling seen from
            // separation to landing on 2026-08-11.
            //
            // The two fallbacks are not optional. `lookdirup` has no answer when the aim is parallel
            // to the reference, and on an RTLS the stage IS pointed near-vertically while the
            // reference is horizontal only when it is nearly on top of the pad - so both the 15 and
            // the 165 degree escapes fire in normal flight, and both mean "keep the roll you have".
            // ---- ⛔ ROLL IS HELD, NOT COMMANDED. THE GRID FINS DEPEND ON IT. ----
            // Passing the local vertical as the roll reference rolls the stage to put its "top"
            // along it - which turns the grid fins out of the plane they were built to work in.
            // BOOSTER.ks:315: the flip "blends the roll reference by tgtRotation so the booster keeps
            // the same roll it launched with and the grid fins stay in the plane they expect."
            //
            // Zero here means "no roll reference", and SteerTo then holds the roll the stage already
            // has - which is the launch roll, because nothing has commanded it since.
            // ---- ⛔ THE FLIP IS THE ONE PHASE THAT NEEDS A ROLL REFERENCE. ----
            // Everywhere else zero is right and its reason above stands: an uncommanded roll keeps
            // the launch roll and the grid fins stay in the plane they were built for.
            //
            // But Flip1 does NOT hold roll - it locks `lookdirup(flipVec, -rotateVector)`, putting
            // the stage's top along the flip axis so the rotation stays in the plane of flight. With
            // zero here the controller was told "keep whatever roll you have", so the roll-settle
            // gate below waited eight seconds for a roll NOTHING WAS COMMANDING and gave up at
            // 89.8 deg - which is the 2026-08-11 13:37 log line, and very nearly a right angle
            // because that is exactly how far it had to go and never went.
            // Keep the roll axis held even through the coast's larger pitch error - see the tunable.
            AttitudeController.Booster.RollControlRangeDeg = BoosterRollRangeDeg;

            // ---- ⛔ ONE ROLL FROM FLIP TO TOUCHDOWN: THE PLANE-OF-FLIGHT NORMAL. ----
            // The crew's requirement is ZERO roll rotation after separation. The earlier NORTH
            // reference SWUNG: re-projected perpendicular to a rotating aim it jumped whenever the aim
            // neared north, driving phiRoll from -34 to +119 deg and 345 deg of coast roll (measured
            // flight_0817_162926). The FLIP AXIS does not swing - it is perpendicular to the plane of
            // flight (FlipGeometry / StepFlip), so it is ALWAYS perpendicular to the aim, never
            // degenerate and never gated, and holding the booster's top to it keeps the grid fins in
            // the flight plane the whole way down. It is already the flip's own roll reference; every
            // phase uses it now, so the stage never re-clocks.
            //
            // Seeded at the flip. Before that (the instant between separation and flip) upHint is zero
            // = hold current roll, which is right: there is nothing to re-clock to yet.
            Vector3d upHint = (flipSeeded && flipAxis.sqrMagnitude > 0.5) ? -flipAxis : Vector3d.zero;

            AttitudeController.Booster.SteerTo(v, dir, upHint);
        }
    }
}
