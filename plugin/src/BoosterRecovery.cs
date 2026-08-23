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
 * ---- THE PHYSICS RANGE: OUR OWN PhysicsRangeExtender PORT, NO PRE DEPENDENCY ----
 * The stock game unloads a non-focused vessel near ~300 km, so the upper stage used to unload during
 * the booster's descent (`falcon-physics-range-clamp` measured 297-341 km). That is NOT a hard KSP
 * limit - it is that KSP RESETS a vessel's `vesselRanges` on every situation change, so a one-time ask
 * falls straight back. PhysicsRangeExtender's whole trick is to re-apply the ranges on those events;
 * we now do the same ourselves - `Extend` is re-applied to both vessels on the recovery's own tick
 * (see `Tick`), so the range holds without PRE installed.
 *
 * The range is 1500 km ([Tunable] RangeMetres): the 120 km ferry peaks at ~360 km separation at
 * booster touchdown (flight_0821_004239), which 500 km covered, but the 250 km TOURIST mission flies
 * the upper stage far further downrange, so it is raised to F9I's 1500 km. It is taken as a LOAN only
 * while focus is on the booster and handed back on landing (SnapshotRanges/Extend/RestoreRanges) - a
 * standing range
 * would load the whole system, the station included, and cost frames the recovery does not need.
 * `Set` matches PRE's own band proportions so both vessels stay UNPACKED (full physics, controllable)
 * out to the range, not merely loaded-on-rails. With PRE also installed there is no conflict - its
 * global ranges and ours agree; uninstalling it changes nothing.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class BoosterRecovery
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>
        /// The physics range we hold during a recovery, metres. Our own PhysicsRangeExtender port
        /// (see `Extend`/`Tick`) so the mod no longer depends on PRE being installed.
        ///
        /// 1500 km. The 120 km ferry separates by only ~360 km at booster touchdown
        /// (flight_0821_004239) and 500 km covered it, but the 250 km TOURIST mission puts the upper
        /// stage far further downrange while the booster flies RTLS, so 500 km loses one of them. F9I
        /// used 1500 km; match it. [Tunable] - drop it back toward 500 km for a station-only flight if
        /// the physics load bites.
        /// </summary>
        // ---- RAISED TO 1500 km FOR THE 250 km TOURIST MISSION (user 2026-08-21) ----
        // 500 km covered the 120 km station ferry - booster and upper stage stayed loaded. The tourist
        // mission flies to 250 km, so the upper stage is much further downrange while the booster does
        // RTLS, and 500 km "won't cut it": one of them unloads and is lost. F9I set 1500 km
        // (`SetLoadDistances(ship, 1500000)`) for exactly this, so match it. [Tunable] - lower it back
        // toward 500 km for a lighter station-only flight if the physics load bites.
        [Tunable] public static float RangeMetres = 1500000f;

        /// <summary>
        /// How far the booster's TOTAL attitude error may be and still have its roll axis actively
        /// held, degrees. The controller default is 45 (`Attitude.RollControlRangeDeg`); at 45 the roll
        /// axis went uncontrolled for most of the coast and free-rolled to 367 deg, so this was widened
        /// to 130 to keep roll held. That gave 347 deg (flight_0818_104520) - barely different.
        ///
        /// Since 2026-08-20 the booster holds its roll to the IN-PLANE top (`flipAxis x dir`, see the
        /// pitch-only block at `upHint` in Aim), so this gate is active again and does its real job: keep
        /// roll HELD through the coast's large pitch error rather than releasing it mid-turn. 130 covers
        /// the ~130 deg coast reorientation. The 45-vs-130 history (45 -> 367, 130 -> 347) was measured
        /// under the old `upHint = zero`, chasing a phantom roll error either way; not a live result.
        /// </summary>
        [Tunable] public static double BoosterRollRangeDeg = 130.0;

        // ---- RSS/RO DRONESHIP: "Of Course I Still Love You" (Crew-2's ASDS) ----
        // In RSS the barge is placed as a KerbalKonstructs STATIC (too unstable to float as a vessel),
        // which is NOT in FlightGlobals - FindDroneship cannot see a static. So on Earth the booster
        // aims at this fixed coordinate: the exact spot the static was set (KK RefLatitude/RefLongitude,
        // 2026-08-22). [Tunable] - move the barge and update these to re-aim, no rebuild. On Kerbin the
        // stock open-water fallback (Lz0) is kept.
        [Tunable] public static double DroneshipEarthLatDeg = 31.559906;
        [Tunable] public static double DroneshipEarthLonDeg = -76.679988;
        /// <summary>Body radius above which the world is RSS/RO Earth (Kerbin 600 km, Earth 6371 km).</summary>
        public const double EarthRadiusThresholdM = 1.0e6;

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
        /// Seconds to RCS-settle the propellant before lighting the ENTRY burn (Real Fuels).
        ///
        /// ⛔ A ballistically-coasting stage has UNSETTLED propellant: free fall is weightless, so gravity
        /// does NOT settle a falling tank. Only a non-gravitational force on the airframe does - drag, or
        /// active ullage - and at the ~65 km entry-burn altitude the air is too thin for drag to settle it
        /// in time. Without a settle the Merlin will not light under Real Fuels even WITH ignitor fluid
        /// (TEATEB). So hold the engine OFF for this long at the top of the entry burn while RCS fires
        /// forward (UllageFore), settling propellant onto the engine feed, THEN light. Mirrors the M-Vac's
        /// S2SettleBeforeIgniteS. NOT applied to the landing burn: by then the stage is in dense air (drag
        /// settles it), the entry burn just ran, and a hoverslam must not be delayed.
        /// </summary>
        private const double EntryBurnUllageSettleS = 2.0;

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
                // No droneship VESSEL. In RSS the barge is a KerbalKonstructs STATIC (not a vessel), so
                // on Earth aim at the fixed OCISLY coordinate the static sits at; on Kerbin keep the
                // stock open-water fallback. See the DroneshipEarth* tunables.
                if (v.mainBody != null && v.mainBody.Radius > EarthRadiusThresholdM)
                {
                    PadLat = DroneshipEarthLatDeg; PadLon = DroneshipEarthLonDeg; HavePad = true;
                    Debug.Log(Tag + "landing zone: droneship STATIC 'Of Course I Still Love You' at "
                              + PadLat.ToString("F6") + ", " + PadLon.ToString("F6")
                              + " (no vessel; fixed Earth coordinate)");
                    return;
                }
                PadLat = LandingSites.Lz0.LatDeg; PadLon = LandingSites.Lz0.LonDeg; HavePad = true;
                Debug.LogWarning(Tag + "no droneship vessel and not on Earth - falling back to "
                                     + LandingSites.Lz0.Name + ". The booster will aim at open water.");
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
            // The configured name wins (Crew-2: "Of Course I Still Love You"); but a rename must not
            // silently leave the booster aiming at open water, so fall back to any vessel that IS a
            // droneship - it carries the droneship part. The barge is usually UNLOADED downrange, so
            // read its proto-parts when it is not loaded. falcon-detect-by-capability.
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
            // Every latch below is a static that would otherwise carry a PREVIOUS flight's state
            // into this one: fins "already out" on a stage that has not launched, a mode machine
            // that thinks it is mid-step, a failure already reported so never reported again.
            gridFinsOut = false;
            recoveryPropStart = -1.0;
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
            slewSeeded = false;
            slewSign = 0.0;
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

            // ⛔ STOW THE GRID FINS' CONTROL RESPONSE. They are retracted at separation and do not
            // deploy until the arc-over, but KSP deflects every ModuleControlSurface from ctrlState
            // regardless of deploy state - so through the flip and coast the RETRACTED fins actuated to
            // the (railed) attitude command. Gate their control on actually being out; DeployGridFins
            // restores it. flight_0822_205453: b_finsOut 0 while the actuators railed +-1 the whole coast.
            SetGridFinControl(booster, false);

            // Join the profile where the stage actually is. Handover is late by design - see
            // Landing.InitialPhase - so assuming Boostback would fly a falling booster back up.
            Phase = Landing.InitialPhase(Read(booster));
            // Latch the error the boostback starts with; the throttle tapers against it.
            initialMiss = Math.Abs(PredictedMiss(booster));

            FlightGlobals.ForceSetActiveVessel(booster);

            Debug.Log(Tag + "booster recovery: focus -> '" + booster.vesselName
                      + "' at " + Landing.Name(Phase)
                      + ", upper stage '" + upperStage.vesselName + "' coasts to apoapsis. "
                      + "Physics range held at " + (RangeMetres / 1000f).ToString("F0")
                      + " km (re-applied each tick; no PRE needed).");
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
        /// AutoPilot.Engage - see the note there for the circular dependency this replaces. The range
        /// must be up before the separation crosses stock's ~2.5 km, which the data puts at ~met 287,
        /// DURING the boostback burn - so "raise it after boostback" is too late; the upper stage would
        /// already have unloaded. `Tick` then re-applies it every frame so KSP's resets cannot drop it.
        /// </summary>
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
            // ⛔ MATCH PhysicsRangeExtender'S OWN PROPORTIONS. The old unpack of 200 m would have left a
            // vessel PACKED (on rails, no physics, no control) past 200 m; it only ever flew because
            // PRE's global ranges - unpack = 0.99 x range - overrode ours. Replacing PRE means we must
            // carry that ourselves: unpacked (full physics) out to ~range, loaded to range, with PRE's
            // hysteresis so a vessel hovering at the edge does not flap between states.
            s.load = RangeMetres;
            s.unload = RangeMetres * 1.05f;
            s.pack = RangeMetres * 1.10f;
            s.unpack = RangeMetres * 0.99f;
        }

        // ---- ⛔ THE EXTENDED RANGE IS ON ONLY WHILE FOCUS IS AWAY ON THE BOOSTER. ----
        // Held at 500 km, the upper stage stays LOADED and UNPACKED and keeps flying itself through the
        // whole recovery instead of unloading at ~300 km and coming back a rebooted vessel (which is
        // what disengaged the ascent and lost the orbit). But a standing 500 km range is still a cost -
        // it loads everything else in range, the station included, when it happens to be near. So the
        // range is a LOAN: snapshot the normal ranges when we take the booster, put them back on
        // handback so the moment focus returns to the upper stage the system unloads and frames return.
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

            // ---- ⛔ HOLD THE EXTENDED RANGE EVERY TICK - THIS IS THE PhysicsRangeExtender PORT. ----
            // Setting `vesselRanges` ONCE is not enough: KSP RESETS a vessel's ranges every time it
            // changes situation - the booster ENTRY -> DESCENT -> LANDED, the upper stage subOrbital ->
            // orbit - which is why the old one-time ask fell back to the ~300 km the stock game allows,
            // and the upper stage unloaded during the booster's final descent (falcon-physics-range-
            // clamp). PRE's whole trick is to re-apply on those situation-change events; re-applying
            // here on the recovery's own tick does the same thing more simply, for the two vessels that
            // matter, only while the recovery is live. This is what lets us drop the PRE dependency.
            Extend(booster);
            Extend(upperStage);

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

            // ---- ⛔ ULLAGE SETTLE BEFORE THE ENTRY-BURN RELIGHT (Real Fuels). ----
            // The stage has been coasting ballistically, so its propellant is unsettled (free fall is
            // weightless), and at ~65 km the air is too thin for drag to settle it. Under Real Fuels the
            // Merlin will not light on unsettled propellant even with TEATEB. So for the first
            // EntryBurnUllageSettleS of the entry burn, keep the engine OFF and fire RCS forward
            // (UllageFore -> Drive's s.Z, through the booster's own OnFlyByWire) to settle propellant onto
            // the engine feed; then fall through and light. Aim still runs so the stage holds retrograde
            // and the settle pushes the right way. See EntryBurnUllageSettleS.
            if (Phase == LandingPhase.EntryBurn
                && Planetarium.GetUniversalTime() - phaseStartedAt < EntryBurnUllageSettleS)
            {
                if (!booster.ActionGroups[KSPActionGroup.RCS])
                    booster.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
                AttitudeController.Booster.UllageFore = 1.0;   // -Z forward, settles propellant aft
                SetEngines(booster, 0);                        // hold off - do NOT spend an ignition yet
                Aim(booster, c, s);
                AttitudeController.Booster.Throttle = 0.0;
                if (FlightGlobals.ActiveVessel == booster) FlightInputHandler.state.mainThrottle = 0f;
                return;
            }
            AttitudeController.Booster.UllageFore = 0.0;        // settle done (or not the entry burn)

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

                // ---- REMOVE THE LANDED BOOSTER IN PLACE - NO RECOVERY EVENT, NO SCENE CHANGE. ----
                // User directive 2026-08-20: *"auto recover the booster AFTER returning to the upper
                // stage, without returning to the space centre - we must keep focus on the upper
                // stage."* `OnVesselRecoveryRequested` cannot do that: it tears down and rebuilds the
                // flight scene (see the FlightDriver header) and returns to the space centre even for a
                // booster that is no longer the active vessel - which is exactly the trip we are killing.
                // `Vessel.Die()` removes the stage from the world directly, so it stops stealing focus,
                // WITHOUT any scene transition - the upper stage stays active and under control, its
                // orbit unbroken. Guarded so it can only ever remove the LANDED booster once focus is
                // provably on the upper stage.
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
            // Crew-2 is a droneship (ASDS) recovery - no boostback, the booster runs downrange to the
            // barge. Landing.Guide skips BoostbackKill+Boostback when this is set. See Landing.cs.
            s.Droneship = (Profile == LandingProfile.Droneship);

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

            // ---- RECOVERY PROPELLANT FRACTION: 1.0 at handover, so the entry burn can reserve the
            // landing burn's share (Landing.EntryBurnReserveFrac). recoveryPropStart is latched on the
            // first read after separation, when everything left in the tanks IS the recovery budget.
            double propNow = RecoveryPropUnits(v);
            if (recoveryPropStart <= 0.0 && propNow > 0.0) recoveryPropStart = propNow;
            s.RecoveryPropFrac = (recoveryPropStart > 0.0) ? propNow / recoveryPropStart : -1.0;

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

        // ---- SINGLE-AXIS SLEW STATE. The persistent aim the coast/entry/descent walk toward. ----
        private static Vector3d slewVec;
        private static bool slewSeeded;

        // ---- ROLL-REFERENCE SIGN. Which of the two in-plane tops (±flipAxis x dir) matches the roll
        // the stage launched with. Captured once, when flipAxis is first known, and held. 0 = not yet. ----
        private static double slewSign;

        /// <summary>
        /// ⛔ WALK THE AIM TOWARD <paramref name="target"/> ABOUT ONE AXIS, SO NO ROLL IS INDUCED.
        /// (user, 2026-08-20: "build the single-axis coast reorientation.")
        ///
        /// The controller nulls pitch and yaw INDEPENDENTLY. Hand it a target 90-130 deg off the nose
        /// in one go - as snapping straight to surface-retrograde at coast start does - and it nulls a
        /// large pitch error AND a large yaw error at once, which is a rotation about a TILTED body axis
        /// and cross-couples into roll: flight_0820 measured 42 deg of coast roll (222 on the descent)
        /// with the roll COMMAND at zero (LockRoll) - purely induced. In vacuum the grid fins give NO
        /// roll authority, so that induced roll cannot be taken back out; it must not be induced.
        ///
        /// This is exactly why the FLIP rolls only 4 deg: `StepFlip` never hands the controller a big
        /// error - it walks the aim about a single axis in small steps, nose-catch gated, so each tick's
        /// rotation is about one axis (perpendicular to the nose) and touches roll not at all. Generalise
        /// that: rotate a persistent aim toward the target along the shortest arc (about `slewVec x
        /// target`, which stays perpendicular to the nose the vehicle is tracking), advancing only when
        /// the nose has caught up. Once within `FlipFineDeg` hand the exact target over and track it.
        /// </summary>
        private static Vector3d SingleAxisSlew(Vessel v, Vector3d target)
        {
            if (target.sqrMagnitude < 1e-6) return target;
            target = target.normalized;
            Vector3d fwd = v.ReferenceTransform.up;          // the controller's forward IS the nose
            if (!slewSeeded) { slewVec = fwd; slewSeeded = true; }

            double toGo = Vector3d.Angle(slewVec, target);
            if (toGo <= Landing.FlipFineDeg)                 // aligned - hand the exact target over
            {
                slewVec = target;
                return slewVec;
            }

            // Advance only when the nose has caught up, so the aim leads by at most FlipNoseCatchDeg and
            // never runs away from what the stage can deliver - StepFlip's rule, and what stops it
            // diverging. Between advances the stage keeps turning toward the aim it already has.
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
            SetGridFinControl(v, true);      // now they are out, let them fly the attitude
            Debug.Log(Tag + "grid fins deployed (" + n + ")");
            if (n == 0)
                Debug.LogWarning(Tag + "no grid fins found - looked for ModuleAnimateGeneric '"
                                     + VehicleParts.GridFinAnimation + "'. Entry will have no "
                                     + "aerodynamic authority.");
        }

        /// <summary>
        /// Enable or disable the grid fins' aerodynamic control RESPONSE (not the deploy animation).
        ///
        /// ⛔ KSP applies the vessel's ctrlState to EVERY ModuleControlSurface the instant the vessel is
        /// controlled - it does not care whether the fin's deploy animation is retracted - so a stowed
        /// grid fin still deflects to the pitch/yaw/roll command. That is the "fins actuating while
        /// retracted" the crew reported, and during the powerless coast the command is railed at +-1, so
        /// they slam to their stops. Setting ignorePitch/Yaw/Roll while stowed makes the surface ignore
        /// the command until DeployGridFins turns it back on. Matched on the grid-fin deploy animation so
        /// it can only ever touch the grid fins.
        /// </summary>
        /// <summary>Propellant at recovery handover, in resource units - the denominator for RecoveryPropFrac.
        /// Latched on the first post-sep read, reset per flight.</summary>
        private static double recoveryPropStart = -1.0;

        /// <summary>
        /// The booster's remaining LIQUID PROPELLANT (RP-1/LOX family, cooled or not), in resource units.
        /// Read directly off the parts so it sees RealFuels' resources, which `b_lfFrac`/`b_oxFrac` cannot.
        /// Used only as a RATIO (now / at-handover), so units and mixture ratio cancel.
        /// </summary>
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

            // ---- ⛔ EVERY RETROGRADE-HOLDING PHASE REORIENTS AS A SINGLE-AXIS SLEW. (user, 2026-08-20) ----
            // Coast, entry and descent all aim surface-retrograde, and each one turns the nose: the coast
            // swings ~90-130 deg off the boostback attitude, the descent tracks retrograde round as the
            // velocity stands up. Snapping the aim makes the controller null pitch+yaw at once and induces
            // roll the vacuum fins cannot remove (see SingleAxisSlew). Walk it instead - one axis, no
            // roll. Applied to the final aim (after the lean) so descent pad-steering is walked too, and
            // only to SurfaceRetrograde so the flip (its own StepFlip), boostback and the vertical
            // landing aim are untouched; leaving those phases re-seeds the walk from the current nose.
            if (c.Aim == LandingAim.SurfaceRetrograde) dir = SingleAxisSlew(v, dir);
            else slewSeeded = false;

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

            // ---- ⛔ FLY THE BOOSTER ON PITCH ALONE, ROLL AND YAW HELD - "SAS + W/S". ----
            // (user, 2026-08-20: "if a user were flying the booster, all they would need is to switch on
            // SAS and use the W and S keys to steer. All other inputs would be unnecessary." And earlier:
            // not 1 degree of roll change, launch pad to landing zone.)
            //
            // That IS the spec, and it is geometric. The whole recovery happens in ONE plane - the plane
            // of flight - and the stage's pitch axis (starboard) already lies along that plane's normal,
            // `flipAxis`. So pointing the nose anywhere it must go (flip, boostback, retrograde, the
            // landing lean) is PURE PITCH about flipAxis - W and S; roll and yaw only ever need HOLDING.
            // Hand the controller a target attitude that says exactly that and it steers on pitch alone.
            //
            // The target TOP that does it is the IN-PLANE one, `flipAxis x dir`: perpendicular to flipAxis
            // (so it lies IN the plane of flight - that is the launch roll, top in-plane, starboard on
            // flipAxis), perpendicular to `dir` (a valid top for the aim), and being a cross product it can
            // NEVER be parallel to dir, so it never goes degenerate the way a vertical or north reference
            // does. Holding it pins starboard to flipAxis, so every nose move is pure pitch and never yaws
            // the stage out of plane. This is exactly what the earlier `-flipAxis` try got wrong by 90
            // degrees: -flipAxis is the plane NORMAL (cross-track), so it drove the top OUT of plane and
            // rolled the stage 90 deg to reach it (flight_0820_031245, the flip regression). `flipAxis x
            // dir` is that normal CROSSED with the aim - in-plane - the roll the stage launched with.
            //
            // ⚠ SIGN: `flipAxis x dir` and its negative are two in-plane tops 180 deg apart; only one is
            // the launch roll. Capture which, ONCE, when flipAxis is first known, by matching the stage's
            // actual top - so establishing the reference commands no roll and the -flipAxis regression
            // cannot recur. Before flipAxis exists (sep quiet) there is no plane yet, so hold current roll.
            //
            // One reference, replacing the LockRoll / atmospheric-hold split it superseded: because the
            // nose moves on pitch ONLY, the coast reorientation INDUCES no roll (vacuum, where the fins
            // give nothing), and because the roll is actively HELD, the descent's aero roll is fought by
            // the fins the moment they bite. Nothing to fight when nothing disturbs it - near-zero effort.
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
            // The in-plane top above was meant to make the coast reorientation "pure pitch, no roll
            // induced". flight_0821_060847 disagrees: 211 deg of roll on the coast (2.1x F9I), the weak
            // roll authority fighting the reference the whole ~180 deg nose-up - the "elephant walk"
            // the user described. LockRoll turns the roll channel into a pure RATE DAMPER (no reference
            // to chase), and this controller's own note records it FLOWN at ~42 deg on the coast -
            // because there is nothing for the weak authority to fight. It was switched off on the
            // theory the data above disproves. So the COAST now holds NO roll reference and just nulls
            // the rate; every other phase keeps the in-plane top (the descent needs it once the fins
            // bite, and roll does not affect the landing point). Any residual roll is corrected in the
            // entry burn / descent, which have the authority the coast lacks - so nothing that lands
            // the booster changes.
            bool coastDampRoll = (c.Phase == LandingPhase.Coast);
            AttitudeController.Booster.LockRoll = coastDampRoll;
            if (coastDampRoll) upHint = Vector3d.zero;   // remove the reference the authority was fighting
            AttitudeController.Booster.RollControlRangeDeg = BoosterRollRangeDeg;

            AttitudeController.Booster.SteerTo(v, dir, upHint);
        }
    }
}
