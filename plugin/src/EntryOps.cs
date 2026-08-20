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

        /// <summary>
        /// Start the coast course-correction this far above the atmosphere, metres. Raised to 20 km
        /// (~90 km ASL) - the STOCK-drag predictor now returns a valid impact the whole coast (verified
        /// flight_0820_112928: 104/104 rows valid from 75 km), so the correction gets the FULL ~200 s
        /// coast to null the miss with RCS instead of a 20 s sliver at the interface. Time matters: the
        /// de-orbit burn no longer aims, so the correction can have tens of km to take out, two-way.
        /// </summary>
        public const double TrimStartAboveM = 20000.0;
        /// <summary>
        /// Stop the course correction when dynamic pressure passes this, kPa. ⛔ Not an altitude: the
        /// correction is RCS translation, and it stays effective only while the air is thin enough that
        /// the thrusters out-push the drag. Running it INTO the thin upper atmosphere (rather than
        /// stopping 1 km above the interface) lets it null the miss deeper, where the prediction is more
        /// accurate and the entry drift is already developing - flight_0820_160013 handed off at 419 m at
        /// 71 km and then drifted to 1.4 km with nothing steering. ~0.3 kPa is around 52 km on Kerbin.
        /// </summary>
        public const double TrimQCutKpa = 0.30;

        /// <summary>Miss at which the RCS trim reaches full deflection, metres. Proportional below it.</summary>
        public const double TrimScaleM = 5000.0;
        /// <summary>Inside this the predicted impact is on the LZ; the trim stops pushing, metres.</summary>
        public const double TrimDeadbandM = 200.0;

        /// <summary>
        /// Aim the course correction this far DOWNRANGE of the LZ, metres. [Tunable]. The drag-only
        /// prediction lands LONG of where the capsule actually comes down (body lift is not in the stock
        /// table, and the RCS cutoff leaves the last stretch unsteered), so aiming the predicted impact a
        /// matching amount long makes the real one land on the LZ. It moves the landing ~1:1: 165413
        /// landed 234 m short on 1.3 km (-> 1.55), 172452 landed 163 m short on 1.55 km (-> 1.70). A
        /// calibration constant - refine from the recorded landing miss.
        /// </summary>
        [Tunable] public static double AlongBiasM = 1700.0;

        /// <summary>
        /// Aim the course correction this far NORTH of the LZ, metres. [Tunable]. Flights land a
        /// systematic ~130-290 m SOUTH of the LZ, so aim a matching amount north: 172452 landed 133 m
        /// south on 180 m -> 310 m. Refine from the recorded landing miss; negative aims south.
        /// </summary>
        [Tunable] public static double CrossBiasM = 310.0;

        /// <summary>
        /// Below this predicted miss the lifting entry stops steering and flies SHIELD-FORWARD, landing
        /// on the burn's ballistic solution. Any AoA extends the range, so once the vectored burn has the
        /// impact this close, lift only makes it worse. Latched once tripped - see the note in Fly.
        /// </summary>
        public const double EntryHoldM = 10000.0;

        /// <summary>Seconds to wait under the drogues before lighting the SuperDracos.</summary>
        public const double EngineArmDelayS = 2.0;

        // ---- state ----
        public static EntryStage Stage { get; private set; }
        public static bool Engaged { get; private set; }
        public static string Note = "-";

        /// <summary>Where we are trying to land. LZ-1, same as the de-orbit.</summary>
        // ⛔ LZ-1, BY USER DIRECTIVE (2026-08-20): "the predicted impact must be DIRECTLY ON LZ1."
        // Was LandingSites.Splashdown (a crew capsule comes down in water); the crew wants the pad,
        // and the mid-course RCS trim below now has the authority to actually hit it.
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

        /// <summary>Latched so a lost prediction is reported once per entry, not every frame.</summary>
        private static bool noPredictionReported;

        /// <summary>Latched once the predicted impact is within <see cref="EntryHoldM"/>: fly ballistic.</summary>
        private static bool ballisticHold;

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
            haveImpact = false; enginesCommanded = false; ballisticHold = false;
            DroguesDeployed = false; MainsDeployed = false;
            // ⚠ sepFiredAt is a LATCH, not a timestamp to leave lying around. Left set from a previous
            // run, Separate() believes it has already fired and skips the decouple entirely - the
            // capsule would enter with the trunk still on it and no message saying why.
            sepFiredAt = 0.0;
            MissM = -1.0; VerticalCmd = 0.0; LateralCmd = 0.0; AoaCmdDeg = 0.0;
            lastPredictAt = 0.0; lastGuideAt = 0.0;
            AttitudeController.Ascent.Throttle = 0.0;
            if (!v.ActionGroups[KSPActionGroup.RCS]) v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            // ⛔ NOSE SHUT BEFORE ENTRY, WHATEVER HAPPENED AT UNDOCK. The undock closes the shroud on
            // a clean back-away, but a manual undock or an early DEORBIT NOW can skip that - and an
            // open docking hatch is not something to take through entry heating. Closing here is the
            // authoritative point; harmless if it is already shut (the event will not be found).
            DockShroud.Close(v);

            bool stacked = HasPart(new PartMatch(VehicleParts.IsTrunk))
                        || HasPart(new PartMatch(VehicleParts.IsSecondStage));
            Go(stacked ? EntryStage.Separating : EntryStage.CoastToInterface);
            Debug.Log(Tag + "entry sequence engaged - " + (stacked ? "stack still attached" : "clean capsule")
                      + ", target " + TargetLatDeg.ToString("F4") + ", " + TargetLonDeg.ToString("F4"));

            // ---- ⛔ VALIDATE THE KNOWN-BC PREDICTOR BEFORE IT MOVES ANYTHING (2026-08-18). ----
            // The de-orbit POINT is placed with F9I's fixed DescentTimeS (845 s) and lands us hundreds
            // of km off, before any entry guidance can matter. The approved fix is to place the burn
            // from a DRAG-AWARE prediction using our OWN ballistic coefficient. This logs what that
            // predictor says the landing will be NOW, on the entry trajectory just set, so the next
            // flight can compare it to the actual DRAGON RECOVERED miss. If it is close, the predictor
            // is trustworthy and the next step lets it drive the de-orbit point. It commands nothing.
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
                    // ⚠ NOTHING FIRED IS ITS OWN FAILURE AND MUST BE SAID HERE. Reporting it
                    // 1.5 s later as "entry attitude cannot be held" sent the 2026-08-11 debug at the
                    // steering, which was holding 0 deg off retrograde perfectly. The attitude was
                    // never the problem; no decoupler answered.
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
            // (user, 2026-08-20: "the DE-ORBIT BURN must put the predicted trajectory impact point
            // PERFECTLY DIRECTLY ON LZ1 - no over/undershoot, no cross error.") The retrograde burn tunes
            // only depth from a fixed ignition, so it lands ~10-15 km from its miss minimum and CANNOT do
            // cross-track at all. This spends the whole coast closing the rest with RCS, in BOTH axes,
            // aimed straight at the LZ. Authority is proven: flight_0820_070846 moved the predicted impact
            // 12 km in 20 s on fore alone, and this now has the full ~200 s coast.
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
            // The de-orbit burn only lowers periapsis now - it does NOT aim - so this is where the impact
            // is put on target, both ways: prograde/retro RCS (fore) to lengthen OR shorten along-track,
            // lateral RCS to slide cross-track either side. It aims at the LZ offset AlongBiasM DOWNRANGE,
            // because the drag-only prediction lands ~1.3 km long of the real touchdown - biasing the
            // predicted impact that far past the LZ makes the real one land on it.
            Vector3d up = (ship.CoM - b.position).normalized;
            Vector3d impactPos = (Vector3d)b.GetWorldSurfacePosition(impactLat, impactLon, 0.0) - b.position;
            Vector3d lzPos = (Vector3d)b.GetWorldSurfacePosition(TargetLatDeg, TargetLonDeg, 0.0) - b.position;
            Vector3d downrange = Vector3d.Exclude(up, ship.srf_velocity);      // horizontal prograde
            if (downrange.sqrMagnitude > 1e-6) lzPos += AlongBiasM * downrange.normalized;   // aim downrange of LZ
            // ...and a touch NORTH, to cancel the systematic south cross-track offset. North is the spin
            // axis with its vertical part removed (local horizontal north).
            Vector3d northHoriz = Vector3d.Exclude(up, b.angularVelocity);
            if (northHoriz.sqrMagnitude > 1e-6) lzPos += CrossBiasM * northHoriz.normalized;
            Vector3d toAim = Vector3d.Exclude(up, lzPos - impactPos);          // horizontal, impact -> aim point
            double missToAim = toAim.magnitude;

            // Reserve outranks it, and it stops as soon as the impact is on the aim point.
            if (Mono(ship) <= Reserve() || missToAim < TrimDeadbandM)
            {
                Neutral();
                Note = (missToAim < TrimDeadbandM ? "CORRECTION - ON TARGET, " : "CORRECTION - reserve held, ")
                     + missM.ToString("F0") + " m to LZ, " + (alt / 1000.0).ToString("F1") + " km";
                return;
            }

            // Resolve the push onto the capsule's own axes with the docking controller's MEASURED
            // convention (fore = rt.up, starboard = rt.right, top = -rt.forward; DockingOps:738-750) so
            // every sign is proven. Fore is the ALONG axis (nose on retrograde: +fore shortens) - two-way.
            if (toAim.sqrMagnitude < 1e-6) { Neutral(); return; }
            Vector3d dir = toAim.normalized;

            double frac = missToAim / TrimScaleM;                        // proportional, tapers near zero
            if (frac > 1.0) frac = 1.0;

            Transform rt = ship.ReferenceTransform;
            AttitudeController.Ascent.UllageFore = Vector3d.Dot(dir, (Vector3d)rt.up) * frac;
            AttitudeController.Ascent.TranslateX = Vector3d.Dot(dir, (Vector3d)rt.right) * frac;
            AttitudeController.Ascent.TranslateY = Vector3d.Dot(dir, -(Vector3d)rt.forward) * frac;

            Note = "COURSE CORRECTION - " + (missM / 1000.0).ToString("F2") + " km ("
                 + (Math.Abs(along) / 1000.0).ToString("F1") + (along >= 0.0 ? " short" : " long") + ", "
                 + (Math.Abs(cross) / 1000.0).ToString("F1") + " cross), " + (alt / 1000.0).ToString("F1") + " km";
        }

        /// <summary>Neutral translation - no fore/aft, no lateral. Used whenever the trim is not pushing.</summary>
        private static void Neutral()
        {
            AttitudeController.Ascent.UllageFore = 0.0;
            AttitudeController.Ascent.TranslateX = 0.0;
            AttitudeController.Ascent.TranslateY = 0.0;
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

            // ⛔ SAME bc AS THE BURN AND THE TRIM (2026-08-20). The entry used the LIVE-measured bc,
            // which in thin entry air is noisy and DISAGREED with the burn's known-bc(440) solution -
            // flight_0820 read 385 m ballistic at the interface but the live-bc loop saw it swinging
            // km's and ramped 11° of AoA to "correct" it. All three phases now predict with the one bc,
            // so the entry sees the same on-target impact the burn left instead of a phantom error.
            Predict(EntryGuidance.CapsuleBcKgM2);
            bool canSteer = EntryGuidance.CanSteer(ship.dynamicPressurekPa);

            if (haveImpact && canSteer)
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
                // The vectored de-orbit now puts the ballistic impact ON the LZ, both axes. Flying ANY
                // AoA from there only EXTENDS the range - flight_0820 was 385 m at the interface and the
                // lift loop flew it 15.8 km LONG under an 11° AoA it ramped chasing a phantom. Inside
                // EntryHoldM the burn's solution beats anything lift can do, so fly shield-forward and
                // land on it. Latched, so prediction noise cannot flap it back on in mid-descent.
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
                // `r_liftMin` has read 0.00 on every return we have ever flown, and it was never
                // clear whether the loop had nothing to correct or was never consulted. These are
                // completely different faults and the file could not tell them apart, because the
                // no-prediction case only ever set a Note that scrolls past. In air thick enough
                // to steer with, flying without a prediction is the capsule going ballistic - it
                // will land wherever the atmosphere puts it, and nothing will try to fix that.
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
            // `Trim()` above already refuses to spend below `Reserve()`, and its own comment calls
            // that "the same test the de-orbit burn loop uses so the two cannot disagree". THE
            // LIFTING ENTRY WAS THE ONE CALLER THAT DID NOT APPLY IT, and on 2026-08-12 that cost
            // the whole tank: 84 km short along-track, `LateralCmd` railed at 1.00, a full 15 deg
            // of commanded AoA held against the airstream for 350 s, and 57 of 185 units burned
            // between 37 km and 11 km - reaching zero with 234 s still to fly. Attitude authority
            // on the chutes matters more than an impact point that is already unreachable.
            //
            // ⚠ THIS IS A BOUND, NOT A TUNING KNOB, AND IT IS NOT A FIX FOR THE 84 km. It stops
            // the vehicle spending propellant it needs on an error it cannot correct - an L/D of
            // 0.27 was never going to retrieve 84 km. The aim is a separate question and is
            // deliberately left alone; see the note on AimDracoCrew.
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
                // ---- ⛔ CALIBRATE ON WHERE IT LANDED, NOT ON THE WORST TRANSIENT. ----
                // This divided `WorstErrorM` by the aim gain, and `WorstErrorM` accumulates
                // `leadErr` - the RATE-COMPENSATED error, which looks far ahead and spikes while the
                // prediction is still settling. On 2026-08-12 it reported a "worst deficit" of
                // 407.7 km and advised raising the aim 270 700 -> 879 219 m, a factor of 3.2 on a
                // constant F9I confirmed by flight. The capsule's actual miss never exceeded 46 km
                // and settled at 9.2; it landed 9.6 km out.
                //
                // Acting on that advice would have thrown the entry hundreds of kilometres long.
                // The number to calibrate against is the one the flight ended with.
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
        private static void Predict() { Predict(0.0); }

        /// <summary>
        /// Rate-limited impact prediction with an optional ballistic-coefficient override.
        ///
        /// bcOverride &lt;= 0 uses the LIVE-measured bc, which is right INSIDE the atmosphere where the
        /// air is doing the measuring - that is what <see cref="Fly"/> wants. ABOVE the interface there
        /// is no drag to measure and the live value collapses to a vacuum solve, tens of km long; there
        /// the pre-entry trim hands in the capsule's KNOWN bc so the prediction is the real drag-aware
        /// impact, the SAME one the de-orbit burn aims (`DeorbitOps` uses the identical override).
        /// </summary>
        private static void Predict(double bcOverride)
        {
            double now = Planetarium.GetUniversalTime();
            if (now - lastPredictAt < PredictIntervalS) return;
            lastPredictAt = now;

            Impact im = ImpactPredictor.Predict(ship, bcOverride);
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
            // ⛔ THE ORBIT THE DE-ORBIT WAS FLOWN FROM. Without it the aim silently uses the
            // altitude it was FITTED at (86 km) whatever orbit we are actually in - which is
            // exactly how moving the station to 120 km invalidated it with nothing to show for it.
            // Apoapsis, not current altitude: by the time this is read the burn has already
            // lowered periapsis, and the aim is a property of the orbit we left.
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
            // OUR side of any docking joint, never the merged vessel - see DockedSide. Correct
            // today only because both of these run after the undock; one caller moved earlier and
            // the budget would silently become the station's.
            return DockedSide.Mono(v);
        }

        // ---- part plumbing ----
        // A named delegate rather than a lambda: the build is C# 5 and method-group conversion is the
        // idiom the rest of the plugin already uses for this.
        private delegate bool PartMatch(string partName);

        /// <summary>
        /// ⚠ OUR SIDE ONLY. While berthed, `ship.parts` is the station as well - a trunk or a spent
        /// stage on another docked Dragon would read as ours, and `DoEvent` below would fire ITS
        /// decoupler or ITS parachutes. `DockedSide` stops the walk at the docking joint.
        /// </summary>
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
        private static int Cut(PartMatch m) { return DoEvent(m, "cut chute"); }

        /// <summary>
        /// Fire a named capability on every matching part - as an EVENT if the part offers one, and
        /// otherwise as an ACTION.
        ///
        /// Matched on the GUI NAME rather than a module type, because the modules differ: the trunk
        /// is a `ModuleTundraDecoupler` and the Dragon decoupler a stock `ModuleDecouple`, and both
        /// answer to "decouple". `falcon-detect-by-capability` again - ask what it can do.
        ///
        /// ---- ⛔ AN INACTIVE EVENT IS NOT A MISSING ONE, AND THE ACTION WORKS EITHER WAY. ----
        /// On 2026-08-11 this scanned `pm.Events` only and logged
        /// `trunk jettison commanded (0 decoupler(s))` - a jettison that never happened - followed
        /// 1.5 s later by `TRUNK WILL NOT JETTISON`. The capsule then sat in orbit behind a spent
        /// stage for the 22 minutes to the end of the session.
        ///
        /// The partdump settles what the part offers, and it is BOTH:
        ///     MODULE: ModuleTundraDecoupler
        ///        events : [decouple]
        ///        actions: [decouple]
        /// So the name matched and the part was in `DockedSide.Ours` - `HasPart` found it with the
        /// same list one line earlier. The only filter left between the two is `ev.active`, which
        /// this skipped in silence. Hence both changes below: the skip is now COUNTED AND REPORTED
        /// rather than swallowed, so the next flight names the cause outright instead of leaving it
        /// to be inferred.
        ///
        /// And the fallback is a port, not a guess. F9I never used events for this at all:
        /// `dragon_deorbit.ks:892` is `DgDoAction(dgTRUNK, "ModuleTundraDecoupler", "decouple")`,
        /// and `DgDoAction:522` is `if dgM:hasaction(dgAct) { dgM:doaction(dgAct, true) }`. An action
        /// carries no `active` flag, which is exactly why the proven path never hit this.
        /// </summary>
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

        /// <summary>
        /// The same search over `pm.Actions`. See <see cref="DoEvent"/> for why both exist.
        ///
        /// `KSPActionParam` is constructed exactly as kOS's `doaction(name, true)` does: the group is
        /// None because we are not staging, and the state is Active because we are asking for the
        /// action to happen, not to be undone.
        /// </summary>
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
