/*
 * DragonScreen - AutoPilot
 *
 * GLUE. Flies the ascent guidance in `pure/Ascent.cs`: points the vehicle, sets the throttle, and
 * stages when the current stage is spent.
 *
 * ---- NO MECHJEB, NO kOS ----
 * MechJeb is uninstalled and this mod has a standing no-kOS-dependency rule, so every line of this
 * is ours. The two engine APIs it leans on were checked against MechJeb's SOURCE (still on disk at
 * Desktop/mechjeb_src) rather than recalled, which is trigger #3 in CLAUDE.md:
 *
 *      vessel.Autopilot.SAS.SetTargetOrientation(dir, false)   MechJebModuleAttitudeController.cs:361
 *      StageManager.ActivateNextStage()                        MechJebModuleAscentBaseAutopilot.cs:119
 *
 * ---- IT FLIES ONE NAMED VESSEL, NOT "THE ACTIVE ONE" ----
 * `ascentVessel` is captured at engage and held. That is what lets the booster recovery run at the
 * same time: focus moves to the booster for the landing while this keeps flying the upper stage,
 * exactly as F9I's two CPUs do. Reading FlightGlobals.ActiveVessel here would have pointed the
 * ascent guidance at whichever vehicle the camera happened to be on.
 *
 * Throttle goes through `AttitudeController.Ascent.Throttle`, into this vessel's own
 * FlightCtrlState. `FlightInputHandler.state` is the FOCUSED vessel's and is written only to keep
 * the on-screen gauge honest - the same split MechJeb makes.
 *
 * ---- STEERING IS OURS, NOT SAS ----
 * `AttitudeController` is a port of kOS's steering manager. SAS could not take a roll reference and
 * had no torque feed-forward, which is most of what the early wandering was. It also never wrote
 * `ctlPitch/ctlYaw/ctlRoll`, so those columns were dead in all 554 black-box flights; they are live
 * now, which is what unblocks the system-identification pass in FLIGHT_SOFTWARE_PLAN.md.
 *
 * ---- STAGING IS OBSERVED, NOT SCHEDULED ----
 * Stage when we are ASKING for thrust and not getting any, for long enough that it is not a flicker.
 * A staging clock would be wrong the first time an engine failed, which is the one time it matters.
 */
using System;
using KSP.UI.Screens;      // StageManager. Namespace confirmed in MechJebModuleStagingController.cs:4
using UnityEngine;
using MechJebLib.Primitives; // V3, for the UPFG second-stage guidance

namespace DragonScreen
{
    public static class AutoPilot
    {
        private const string Tag = "[DragonScreen] ";

        public static bool Engaged { get; private set; }
        public static AscentPhase Phase { get; private set; }
        public static AscentTarget Target = AscentTarget.Station();

        /// <summary>
        /// Degrees ADDED to the station's inclination before solving the launch azimuth, RSS only.
        ///
        /// ⛔ WHY A BIAS IS NEEDED. The azimuth solver is correct for a vehicle that reaches ORBITAL
        /// velocity holding its heading. But UPFG's frame-agnostic Iy LOCKS whatever plane MECO delivers
        /// (it holds the current velocity plane, it does not steer toward the target plane). At MECO the
        /// vehicle is only ~2.5 km/s, where Earth's rotation (~0.41 km/s) pulls the inertial azimuth ~4
        /// deg further east than at 7.8 km/s - so the same ground azimuth sets a LOWER inclination, and
        /// UPFG then holds it. MEASURED flight_0822_112918: solver commanded 42.8 deg for a 51.60 plane,
        /// insertion came out 47.9 deg (3.7 short). Adding the deficit to the solver's target aims a more
        /// northerly azimuth so MECO sets 51.6 directly. One flight - confirm inc reads ~51.6 next launch.
        ///
        /// ⚠ RE-MEASURED flight_0822_221316 (octaweb + ullage fixes in): the CORRECTED ascent flies a
        /// cleaner gravity turn and carries more horizontal velocity through MECO, so Earth's rotation
        /// drags the plane LESS - a 3.7 bias OVERSHOT to 53.78 deg (2.18 deg over the 51.6 target). The
        /// deficit the bias must cancel is therefore ~1.5 deg, not 3.7. Tunable; confirm ~51.6 next launch.
        ///
        /// ⚠ RE-MEASURED AGAIN flight_0823_201351 (MECO VELOCITY CAP in): staging now cuts at the real
        /// ~2300 m/s instead of ~2440, so MECO is SLOWER and Earth's rotation drags the plane MORE again -
        /// insertion came out 49.2 deg at a 53.1 target (1.5 bias), a 3.9 deg loss, 2.4 deg short of 51.6.
        /// Back up to ~3.9 to cancel it (near the pre-cap 3.7). This is the launch->rendezvous blocker: a
        /// 2.4 deg plane error the coplanar named-burn rendezvous cannot cheaply remove. Confirm ~51.6.
        /// </summary>
        [Tunable] public static double AscentInclinationBiasDeg = 3.9;
        // Parking orbit for the RSS ferry, m. Below the ISS (420 km) - the rendezvous phases up;
        // real Crew-1 inserted near ~200 km. INTERIM, tune from flight.
        const double RssParkingAltitudeM = 200000.0;

        // ---- RSS second-stage LOFT (interim, see Steer) ----
        // A TWR<1 upper stage must aim above prograde to climb out of the 140 km atmosphere. The loft
        // angle = deficit(0..1) * gain, capped at max, where deficit is how far apoapsis is short of
        // target. [Tunable] so it can be tuned in-flight without a rebuild. Superseded by PSG.
        [Tunable] public static double S2LoftGainDeg = 70.0;
        [Tunable] public static double S2MaxLoftDeg = 45.0;

        /// <summary>
        /// Set when a mid-ascent teardown (the booster-recovery handback) disengaged us, so the
        /// rebuilt FlightDriver re-engages the ascent on its own rather than leaving the crew to
        /// restart it. See FlightDriver.OnDestroy (sets it) and FlightDriver.Update (acts on it).
        /// </summary>
        public static bool ResumeAscent;

        /// <summary>Last command flown, for the page to display. Never computed twice.</summary>
        public static AscentCommand Command;

        /// <summary>Last circularisation dv. On the recorder's row, not the command struct.</summary>
        public static double LastCircDvMps;

        /// <summary>Seconds in the current ascent phase. Every hold and timeout keys on it.</summary>
        public static double PhaseElapsedS;
        /// <summary>Distance to the booster, metres. The MVac clearance gate is on this.</summary>
        public static double RangeToBoosterM;

        private static int lastFrame = -1;
        private static double starvedFor;
        private static double lastStageAt = -99.0;

        public static void Toggle()
        {
            if (Engaged) Disengage("crew"); else Engage();
        }

        public static void Engage()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            // ---- ⛔ THIS IS AN ASCENT AUTOPILOT. IT MUST REFUSE TO "ASCEND" FROM ORBIT. ----
            // 2026-08-10, four times in four minutes: AUTO SEQUENCE was pressed on a Dragon already
            // in an 86 x 84 km orbit. Guide() starts at Idle and walks its state machine from the
            // beginning, so it ran VERTICAL RISE -> GRAVITY TURN -> MECO on an orbiting capsule. The
            // pitch law at 86 km asks for a horizon-relative angle that has nothing to do with where
            // the vehicle is pointing, so the controller slewed hard: peak attitude errors of 45,
            // 99, 112 and 134 degrees in the four recordings.
            //
            // And the MECO transition issues a STAGE command. On a launch vehicle that is staging;
            // on a Dragon in orbit it is the trunk, the chutes, or whatever is next in the stack.
            // The only reason nothing was destroyed is that the crew disengaged within seconds.
            //
            // Periapsis above the atmosphere means the job this autopilot exists to do is already
            // done. Refuse, say so, and leave the vehicle alone.
            if (v.orbit != null && v.mainBody != null && v.orbit.PeA > v.mainBody.atmosphereDepth)
            {
                Debug.LogWarning(Tag + "AUTO SEQUENCE refused - already in orbit ("
                                     + (v.orbit.PeA / 1000.0).ToString("F1") + " x "
                                     + (v.orbit.ApA / 1000.0).ToString("F1")
                                     + " km). The ascent autopilot flies to orbit; it will not fly "
                                     + "from one. Use the manoeuvre page for orbital burns.");
                return;
            }

            // ---- ⛔ HOLD FOR THE PHASE WINDOW. §1 OF THE MISSION, AND IT WAS UNREACHABLE. ----
            // `pure/LaunchWindow.cs` was ported, tested, marked DONE - and called by nothing. F9I
            // launches on PHASE ANGLE rather than plane (the station is at 0.133 deg, so the plane
            // window is degenerate at the equator), and arriving at the wrong phase is what made its
            // first ferry "spend 7.3 HOURS phasing for only 39 LF". Only on the pad: mid-flight there
            // is nothing to hold.
            windowOpensUt = 0.0;
            windowWarped = false;
            if (v.situation == Vessel.Situations.PRELAUNCH
                || v.situation == Vessel.Situations.LANDED)
            {
                double wait = LaunchWindowOps.SecondsToWait(v);
                if (wait > 0.0)
                {
                    windowOpensUt = Planetarium.GetUniversalTime() + wait;
                    Debug.Log(Tag + "LAUNCH WINDOW: " + LaunchWindowOps.Note);
                }
                else Debug.Log(Tag + "launch window open now - " + LaunchWindowOps.Note);
            }

            Engaged = true;
            ResumeAscent = false;              // engaging satisfies any pending resume
            Phase = AscentPhase.Idle;
            // A fresh engagement is a fresh mission: never inherit a previous flight's recovery.
            BoosterRecovery.Reset();
            // ---- CREW-2: DRONESHIP recovery downrange, ascent scaled to Earth. ----
            // Crew-2 recovers the booster on the droneship (ASDS) downrange - NOT RTLS: from Earth the
            // boostback is one the booster cannot close (flight_0822_105240 ran BOOSTBACK KILL ->
            // BOOSTBACK and stayed ~3110 km from any target). The droneship profile skips boostback
            // (Flip->Coast->Entry->Landing, Landing.cs) and aims the barge downrange. Set the profile
            // BEFORE the target - ForBody reads it. ForBody scales the turn and the ~200 km parking orbit
            // off the live Earth atmosphere depth.
            BoosterRecovery.Profile = LandingProfile.Droneship;
            Target = AscentTarget.ForBody(BoosterRecovery.Profile, RssParkingAltitudeM);
            ascentVessel = v;

            // ---- TARGET THE STATION (ISS) FROM LAUNCH. ----
            // Puts the station on the navball at liftoff; the rendezvous retargets to the docking PORT at
            // handover, and the undock clears it. Guarded and harmless if the station is not in this save.
            {
                Vessel stn = StationApproach.Find();
                if (stn != null)
                {
                    DockingOps.SetTarget(stn, "launch - targeting the station");

                    // ---- LAUNCH INTO THE STATION'S PLANE, NOT BLINDLY DUE EAST. ----
                    // 'heading 90' is a Kerbin artefact: the stock station is at 0.133 deg over an
                    // equatorial pad, so due east IS the plane and LaunchAzimuth returns ~90 here -
                    // nothing changes. RSS/Crew-1: the ISS is at 51.6 deg and LC-39A at 28.6 N, so
                    // due east misses the plane by ~23 deg, which a gravity turn cannot recover.
                    // The azimuth is solved from the TARGET's own inclination and corrected for the
                    // body's spin, so it is right for whatever station is in the world. See
                    // pure/LaunchAzimuth.cs and docs/REAL_CREW_DRAGON_MISSION.md.
                    if (stn.orbit != null && v.mainBody != null)
                    {
                        double r = v.mainBody.Radius + Target.AltitudeM;
                        double vOrb = Math.Sqrt(v.mainBody.gravParameter / r);
                        double vEq = LaunchAzimuth.SurfaceEastwardSpeedMps(
                            v.mainBody.Radius, v.mainBody.rotationPeriod, v.latitude);
                        // Aim at the plane PLUS the bias that cancels UPFG's MECO plane-lock (see
                        // AscentInclinationBiasDeg).
                        double incTarget = stn.orbit.inclination + AscentInclinationBiasDeg;
                        Target.HeadingDeg = LaunchAzimuth.GroundHeadingDeg(
                            incTarget, v.latitude, vOrb, vEq);
                        Debug.Log(Tag + "launch azimuth " + Target.HeadingDeg.ToString("F1")
                                  + " deg for a " + incTarget.ToString("F2") + " deg aim (station "
                                  + stn.orbit.inclination.ToString("F2") + " + bias "
                                  + (incTarget - stn.orbit.inclination).ToString("F2")
                                  + ", pad " + v.latitude.ToString("F2") + " N)");
                    }
                }
            }
            packedReported = false;

            // ---- ⛔ THE PAD HOLD LIVES IN Tick(), NOT HERE. ----
            // It was here, and its `return` skipped TEN statements below it - including
            // `PrepareForSeparation`, which is why the 10:37 booster unloaded and was lost; and
            // `phaseStartedAt`, which left every phase timeout running on the PREVIOUS flight's
            // clock; and `s2Separated`, which meant a second flight in one session would never
            // separate the second stage at all. Engage sets the window up; Tick holds against it.
            // Engage must fall through to the bottom of this method on every path.
            s2Separated = false;
            boosterSeparated = false;
            s2IgnitionAttempts = 0;
            lastS2IgniteAt = -99.0;
            upfgState.Initialised = false;     // fresh guidance solve for this ascent
            upfgActive = false;
            clampReleased = false;
            s1IgniteAt = -1.0;
            lastHandoverTry = 0.0;
            starvedFor = 0.0;
            blindStages = 0;
            // ---- ⛔ RESET THE STAGING LOCKOUT, OR A REVERT-TO-LAUNCH NEVER IGNITES. ----
            // `lastStageAt` is a UT and the auto-ignition holds a 2 s lockout against it
            // (`Stage`: `if (now - lastStageAt < 2.0) return;`). Reverting to launch moves UT
            // BACKWARDS - the new launch clock is earlier than a previous flight's last staging -
            // so `now - lastStageAt` goes large-negative, the lockout never clears, and the initial
            // ignition can never fire: engines stay unlit, no liftoff, and the launch window
            // re-arms. Measured 2026-08-17: two revert-launches sat in VERTICAL RISE at full
            // throttle with availThrust 0 for 30 s each and never staged, while fresh-load launches
            // the same session ignited normally. `starvedFor`/`blindStages` were reset here; this
            // one was missed. The same "statics must validate, not remember" rule as BoosterRecovery.
            lastStageAt = -99.0;
            phaseStartedAt = Planetarium.GetUniversalTime();
            lastCommanded = Vector3d.zero;

            // ---- ⛔ EXTEND THE PHYSICS RANGE NOW, NOT AT HANDOVER. ----
            // This was the reason `landPhase` is "-" for all 1371 rows of the 21:01 flight: the
            // ranges were only extended INSIDE TryHandover, which runs after FindBooster has already
            // succeeded. FindBooster searches FlightGlobals.VesselsLoaded, and KSP's default load
            // distance is 22.5 km - so the booster unloaded seconds after separation and was simply
            // not in the list by the time anything went looking. The extension could never happen
            // because it was gated on finding the thing it existed to keep loaded.
            //
            // F9I gets the order right and says why: FalconExtendRange raises the range on the SHIP
            // first, then waits for the booster to appear, then raises it on the booster too.
            BoosterRecovery.PrepareForSeparation(v);
            // Cleared, not set: liftoff has not happened yet. Tick stamps it when the vehicle
            // actually leaves the pad - see the note there. Setting it here is what made
            // LaunchWindowOps.MeasureAtInsertion dead code for its entire life.
            //
            // ⛔ BUT ONLY A GROUND LAUNCH CLEARS IT. A re-engage IN THE AIR - which the booster
            // recovery handback triggers - must NOT re-stamp the clock, or MeasureAtInsertion times
            // the ascent from the re-engage instead of the real liftoff. Measured 2026-08-17: a 520 s
            // ascent was fit as 89.6 s that way, leaving the window seed stale and the capsule 16 km
            // off. On the pad, clear it; already flying, keep whatever liftoff we stamped.
            if (v.situation == Vessel.Situations.PRELAUNCH || v.situation == Vessel.Situations.LANDED)
            {
                liftoffUt = 0.0;
                liftoffLonDeg = 0.0;
            }
            // ---- READ THE VEHICLE BEFORE FLYING IT. ----
            // Three flights were lost to the software's idea of the vehicle differing from the part
            // configs - wheels-only torque, three engine modules summed as three engines, a PAW
            // title matched instead of a part name. All three were findable on the pad. See
            // VehicleCheck; it reports and never refuses.
            VehicleCheck.Report(v);

            Debug.Log(Tag + "autopilot ENGAGED - target " + (Target.AltitudeM / 1000.0).ToString("F0")
                      + " km, heading " + Target.HeadingDeg.ToString("F0")
                      + ". ⚠ INTERIM: gravity turn, not the PSG ascent in FLIGHT_SOFTWARE_PLAN.md");
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            Phase = AscentPhase.Idle;
            // Hand the axes back, or the controller keeps flying a vehicle nobody is commanding.
            AttitudeController.Ascent.Release(ascentVessel);
            if (FlightGlobals.ActiveVessel == ascentVessel)
                FlightInputHandler.state.mainThrottle = 0f;
            ascentVessel = null;
            Debug.Log(Tag + "autopilot DISENGAGED - " + why);
            // ⚠ THE RECORDER IS NOT OURS TO STOP. `FlightDriver` starts it on scene entry and stops
            // it on scene exit, and it restarts anything it finds stopped - so a Stop() here just
            // split the flight across two CSVs at insertion. One owner.

        }

        /// <summary>
        /// One step, driven by FlightDriver. The frame guard is kept from when three IVA screens
        /// each called this - it costs nothing and it is the difference between one autopilot and
        /// three fighting over a throttle if anything ever calls it twice again.
        /// </summary>
        public static void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;

            // ---- ⛔ BOTH VEHICLES FLY. THIS USED TO `return` HERE, AND THAT WAS THE CEILING. ----
            // The recovery is its own mission and outlives the ascent that spawned it (ISSUE 7) -
            // but returning meant the upper stage stopped being flown the instant a booster was
            // taken over, so recovery could only be bought by giving up the payload. Since our
            // upper stage separates on a suborbital arc, that forced handover to wait until after
            // insertion, three minutes past the boostback window.
            //
            // KSP simulates every LOADED vessel and calls each one's own OnFlyByWire, so there was
            // never a reason to fly only one. F9I proves it: two CPUs, and FalconFocusBooster's
            // message is "Focus -> Booster for landing. The upper stage circularizes on its own."
            // Now that the controller is per-vehicle and the throttle goes through each vessel's own
            // control state, we do the same.
            // ...and while it is settling on the pad, when Active is already false. See Settling.
            if (BoosterRecovery.Active || BoosterRecovery.Settling) BoosterRecovery.Tick();

            if (!Engaged) return;

            // ---- FLY THE VESSEL WE LAUNCHED, NOT WHICHEVER ONE THE CAMERA IS ON ----
            // Reading FlightGlobals.ActiveVessel here would have pointed the ascent guidance at the
            // BOOSTER the moment focus moved to watch it land.
            Vessel v = ascentVessel;
            if (v == null || v.state == Vessel.State.DEAD || v.orbit == null || v.mainBody == null)
            {
                Disengage("upper stage lost");
                return;
            }

            // On rails beyond the physics range: nothing we write reaches it, and pretending
            // otherwise is how a flight looks fine in the log and does nothing in the world.
            // `falcon-physics-range-clamp` measured that boundary at 297-341 km on four flights.
            // Stay engaged - it comes back when it reloads, which is exactly what F9I sees.
            if (v.packed)
            {
                if (!packedReported)
                {
                    packedReported = true;
                    Debug.LogWarning(Tag + "upper stage has gone on rails (beyond the physics "
                                         + "range) - guidance is suspended until it reloads");
                }
                return;
            }
            packedReported = false;

            // ---- THE PAD HOLD. Nothing is commanded until the phase comes round. ----
            // §1 of the mission: F9I launches on PHASE ANGLE, not plane, because the station sits at
            // 0.133 deg and a plane window is degenerate at the equator. Arriving at the wrong phase
            // is what made its first ferry "spend 7.3 HOURS phasing for only 39 LF".
            //
            // ⚠ AND IT BELONGS HERE, AFTER THE PACKED CHECK, BECAUSE IT RETURNS. Anything this
            // block skips is skipped for the whole hold; in Engage that was the vehicle's own setup.
            if (windowOpensUt > 0.0)
            {
                double leftS = windowOpensUt - Planetarium.GetUniversalTime();
                if (leftS > 0.0)
                {
                    Command.Note = "HOLD FOR PHASE WINDOW - T-" + leftS.ToString("F0") + " s";
                    // Warp there. A hold that has to be sat through in real time is a hold nobody
                    // uses - which is how the de-orbit's 25-minute wait went unnoticed until a flight.
                    if (!windowWarped && leftS > NodeExecutor.WarpWorthwhileS)
                    {
                        windowWarped = true;
                        TimeWarp.fetch.WarpTo(windowOpensUt - WindowWarpLeadS);
                    }
                    else if (leftS <= WindowWarpLeadS && TimeWarp.CurrentRateIndex > 0)
                        TimeWarp.SetRate(0, true);
                    return;
                }
                if (TimeWarp.CurrentRateIndex > 0) { TimeWarp.SetRate(0, true); return; }
                windowOpensUt = 0.0;
                Debug.Log(Tag + "launch window OPEN - releasing the countdown");
            }

            // ---- STAMP LIFTOFF WHEN IT HAPPENS, NOT WHEN THE CREW ARMED THE AUTOPILOT. ----
            // `MeasureAtInsertion` needs the real ascent duration to re-fit the launch window, and it
            // early-returns on a zero liftoff time - so with this set in Engage it never ran once.
            if (liftoffUt <= 0.0
                && v.situation != Vessel.Situations.PRELAUNCH
                && v.situation != Vessel.Situations.LANDED)
            {
                liftoffUt = Planetarium.GetUniversalTime();
                liftoffLonDeg = v.longitude;
                Debug.Log(Tag + "liftoff - clock started for the launch-window fit");
            }

            // ---- ⛔ ISSUES 3 AND 4. STATICS MUST VALIDATE, NOT REMEMBER. ----
            // CLAUDE.md already carries this rule - it was written after the NAV map and navball
            // vanished on a revert - and the new flight software broke it again.
            // `BoosterRecovery.Reset()` existed and was called from NOWHERE, so across a revert or a
            // scene change `Active` could still be true, `booster`/`upperStage` held references to
            // destroyed vessels, and `HavePad` carried the PREVIOUS flight's landing zone.
            // VesselData does this correctly by watching persistentId; same pattern here.
            // The persistentId watch that used to live here compared ActiveVessel against the one we
            // engaged on. It cannot do that job now - we hold the vessel by reference, so the two
            // are the same object by construction - and it would have fired spuriously the moment
            // focus moved to the booster. Scene-level validation moved to FlightDriver.OnDestroy,
            // which is the honest place for it: a revert or a scene change is what invalidates
            // these statics, not a camera move.

            BoosterRecovery.RememberPad(v);

            // ---- ⛔ TAKE THE BOOSTER AS SOON AS ONE EXISTS. ----
            // This gate has now been wrong twice in opposite directions. It was GravityTurn||Coast,
            // which excluded the only phases where a booster exists, so it could never fire. Then it
            // was Coast alone, which fired 155 s after separation - the 21:01 flight measured it -
            // by which time boostback (16-55 s after separation in F9I's data) was long gone.
            //
            // Waiting was only ever necessary because taking the booster meant dropping the upper
            // stage. It no longer does: both are flown, so the right moment is the FIRST one, which
            // is what the real vehicle does and what F9I does.
            //
            // Only after MECO, because before separation the only vessel carrying a `.S1.` part is
            // us. Rate-limited because FindBooster walks every part of every loaded vessel.
            // ⚠ StageSep IS IN THIS LIST AND MUST BE: it is the phase during which the booster
            // first exists as a separate vessel. Leaving it out delayed every handover by the full
            // 3 s post-separation coast and made the "no booster" warning fire during MECO, when
            // there legitimately is not one yet.
            if (Phase == AscentPhase.Meco || Phase == AscentPhase.StageSep
                || Phase == AscentPhase.BurnToApoapsis || Phase == AscentPhase.Coast)
            {
                double nowUt = Planetarium.GetUniversalTime();
                if (nowUt - lastHandoverTry > 0.5)
                {
                    lastHandoverTry = nowUt;
                    BoosterRecovery.TryHandover(v);
                }
            }

            // ---- HAND BACK RATHER THAN FIGHT ----
            // If the crew grabs the stick, get out of the way instead of arguing through the same
            // controls.
            //
            // ⚠ THIS MUST READ THE PILOT, NOT `ctrlState`. First flight 2026-08-06: this tested
            // `v.ctrlState.pitch/yaw`, which is the FINAL control state - and since we steer with
            // SAS, SAS's own output lands in exactly those fields. The autopilot detected itself as
            // the pilot and disengaged within seconds of every single engagement, five times over.
            //
            // MechJeb's own test (`MechJebModuleAttitudeController.cs:395`, s.pitch vs s.pitchTrim)
            // does NOT transfer: MechJeb turns SAS OFF and drives the axes itself, so for MechJeb
            // ctrlState really is just the pilot. Reading the raw bindings is the equivalent for a
            // controller that leaves SAS on.
            if (PilotInput(v)) { Disengage("manual input"); return; }

            AscentInputs a = new AscentInputs();
            a.Valid = true;
            a.RadarAltitude = v.radarAltitude;
            a.Altitude = v.altitude;
            a.ApoapsisM = v.orbit.ApA;
            a.PeriapsisM = v.orbit.PeA;
            a.AtmosphereDepthM = v.mainBody.atmosphereDepth;
            a.VerticalSpeed = v.verticalSpeed;
            a.SurfaceSpeed = v.srfSpeed;                 // caps MECO at the real staging velocity
            a.DynamicPressureKpa = v.dynamicPressurekPa;
            a.TimeToApoapsisS = v.orbit.timeToAp;
            a.AvailableThrust = AvailableThrust(v);
            a.MassT = v.GetTotalMass();                  // for the g-limit throttle (Crew Dragon ~4 g cap)
            a.Landed = (v.situation == Vessel.Situations.LANDED
                     || v.situation == Vessel.Situations.PRELAUNCH
                     || v.situation == Vessel.Situations.SPLASHED);
            // The two stages fly DIFFERENT laws in F9I, so the guidance has to know which it is on.
            a.SecondStage = !HasBooster(v);
            a.PhaseElapsedS = Planetarium.GetUniversalTime() - phaseStartedAt;
            a.RangeToBoosterM = Range(v, BoosterRecovery.BoosterVessel);
            PhaseElapsedS = a.PhaseElapsedS;
            RangeToBoosterM = a.RangeToBoosterM;

            // ---- "MAKE THE ORBIT CIRCULAR HERE, NOW" ----
            // FalconCircBurnVecNow, F9_payload.ks:265, ported exactly:
            //     dv = (horizontal_unit * sqrt(mu / r)) - v
            // It has a TRUE FIXED POINT, which the prograde-until-periapsis version did not, and
            // that is the whole reason the last flight left Kerbin.
            circDv = CirculariseDv(v);
            a.CircDvMps = circDv.magnitude;
            LastCircDvMps = a.CircDvMps;
            // Overshoot: the required dv has reversed against where it pointed at burn start.
            a.CircDvFlipped = circDvStart.sqrMagnitude > 0.01
                              && Vector3d.Dot(circDv, circDvStart) < 0.0;

            AscentCommand c = Ascent.Guide(a, Target, Phase);
            // Latch the dv direction at burn start so the overshoot test has a reference.
            if (c.Phase == AscentPhase.Circularise && Phase != AscentPhase.Circularise)
                circDvStart = circDv;
            if (c.Phase != AscentPhase.Circularise) circDvStart = Vector3d.zero;

            // ---- INSTRUMENT THE BURN, NOT JUST THE TRANSITIONS ----
            // The escape flight logged nothing between CIRCULARISE and running dry, so the log could
            // not say whether periapsis was rising. Once every two seconds during a burn is cheap
            // and it is the difference between diagnosing the next one and guessing at it.
            if (c.Throttle > 0.01 && Time.realtimeSinceStartup - lastBurnLog > 2f)
            {
                lastBurnLog = Time.realtimeSinceStartup;
                Debug.Log(Tag + "ascent " + Ascent.Name(c.Phase)
                          + "  ap " + (a.ApoapsisM / 1000.0).ToString("F1")
                          + "  pe " + (a.PeriapsisM / 1000.0).ToString("F1")
                          + "  circDv " + a.CircDvMps.ToString("F1")
                          + "  thr " + c.Throttle.ToString("F2"));
            }

            // Log the NOTE when there is one - it is the only place an abort reason exists.
            if (c.Phase != Phase)
                Debug.Log(Tag + "ascent -> "
                          + (string.IsNullOrEmpty(c.Note) ? Ascent.Name(c.Phase) : c.Note)
                          + "  ap " + (a.ApoapsisM / 1000.0).ToString("F1")
                          + " km, pe " + (a.PeriapsisM / 1000.0).ToString("F1") + " km"
                          + Crew2Sync(v));
            if (c.Phase != Phase) phaseStartedAt = Planetarium.GetUniversalTime();
            Phase = c.Phase;
            Command = c;

            // ---- ⛔ RSS/RO: UPFG OWNS THE SECOND STAGE ALL THE WAY TO ORBIT. ----
            // Once the M-Vac is lit, the closed-loop UPFG guidance (pure/Upfg.cs) flies the whole
            // insertion - loft, apoapsis raise and circularise in one law - which is the only thing that
            // reliably closes a real orbit with a TWR<1 upper stage (every fixed-pitch heuristic failed).
            // ⛔ ONCE ACTIVE IT OWNS EVERY TICK until SECO, IGNORING the pure phase machine - because the
            // gravity turn's apoapsis-runaway backstop fired at 300 km and ABORTED UPFG mid-insertion
            // (flight_0822_090116). UPFG manages its own trajectory and cut; the pure Done/abort must not
            // pre-empt it. Falls through to the loft only while a solve is invalid, never a dead stage.
            // ⛔ DO NOT ACTIVATE (and Init) UPFG UNTIL THE M-VAC IS ACTUALLY AT THRUST. Init seeds the
            // predictor-corrector's persistent state (tu = ve*m/F, Rgrav, Rd, Vgo). If it runs during
            // ULLAGE, `a.AvailableThrust` (summed finalThrust of ignited engines) reads ~1 kN, tu goes
            // astronomical, and the poisoned state never recovers under single-stepping - the stage holds
            // ~84 deg and lofts to a 2400 km apoapsis while orbital speed barely moves. PROVEN in the
            // point-mass sim (scratchpad/sim): a 6 s ullage-poisoned Init reproduces flight_0822_095330
            // to the degree (apo 2378 km, iF stuck 84 deg); waiting for real thrust reaches orbit. The
            // fingerprint is the first log line `UPFG tgo 315343s`. UpfgMinThrustKn separates the lit
            // M-Vac (~800 kN) from ullage (~1 kN); the loft holds prograde for the ~6 s until then.
            if (!s2Separated)
            {
                if (UpfgEnabledS2 && !upfgActive && a.AvailableThrust > UpfgMinThrustKn
                    && (c.Phase == AscentPhase.BurnToApoapsis || c.Phase == AscentPhase.Coast
                        || c.Phase == AscentPhase.Circularise))
                    upfgActive = true;
                if (upfgActive && UpfgFlyS2(v, a))
                {
                    // ---- ⛔ UPFG RETURNS BEFORE THE UllageFore APPLICATION BELOW - SO APPLY IT HERE. ----
                    // Once UPFG owns the S2 it flies at real thrust and the engine self-settles: the RCS
                    // ullage must stop. This early return skipped line ~627, so the RCS-fore held its last
                    // ULLAGE value (0.75) for the WHOLE burn - flight_0822_221316: x_fore stuck at 0.75
                    // through 894 kN of MVac, RCS firing pointlessly alongside the engine, wasting cold gas
                    // and disturbing the attitude UPFG was steering. `Ascent.Guide` already drops
                    // c.UllageFore to 0 the moment the engine has thrust; apply that decision here too.
                    AttitudeController.Ascent.UllageFore = c.UllageFore;
                    SetAscentRcs(v, c);   // MVac lit under UPFG: the gimbal steers, RCS off - this path
                                          // returns before the manager below, so set it here too.
                    return;
                }
            }

            if (Phase == AscentPhase.Done)
            {
                // ---- ⛔ REPORT WHY, NOT WHAT THE ENUM IS CALLED ----
                // This said "insertion complete" unconditionally, and the transition log printed
                // Ascent.Name(Done) which is the string "INSERTION COMPLETE". So BOTH failed flights
                // ended with the runaway backstop firing at exactly 129.0 km - 86 x 1.5 - and the
                // log announced a successful orbital insertion. The abort reason was computed into
                // c.Note and then thrown away, twice, and I read those logs and did not notice.
                //
                // A guard that fires silently is worse than no guard: it converts a loud failure
                // into a quiet lie.
                FlightInputHandler.state.mainThrottle = 0f;

                // ---- ⛔ SEPARATE HERE, BECAUSE THIS BRANCH RETURNS. ----
                // The S2 now finishes the insertion and is shed on the SAME tick the ascent
                // completes, and the generic `if (c.SeparateS2)` handler lives sixty lines BELOW
                // this `return`. Left there the command would have been computed, logged and
                // silently dropped - the capsule would reach orbit still bolted to a spent second
                // stage, every flight, with nothing in the log to say why.
                //
                // Exactly the failure CLAUDE.md records twice: "after fixing a state machine,
                // re-trace what each branch RENDERS, not just which branch is taken."
                if (c.SeparateS2) SeparateSecondStage(v);

                // Measure what this ascent actually did, so the NEXT launch window is fitted to the
                // ascent we fly rather than to the one F9I flew. See LaunchWindowOps - reading these
                // from a constant is precisely what makes the window drift.
                LaunchWindowOps.MeasureAtInsertion(v, liftoffUt, liftoffLonDeg);
                Disengage(string.IsNullOrEmpty(c.Note) ? "insertion complete" : c.Note);
                return;
            }

            Steer(v, c);
            // THIS vessel's throttle, through its own control state - see AttitudeController's header.
            // FlightInputHandler.state belongs to whoever has focus, so writing it here would have
            // put the ascent throttle on the booster the moment the camera moved to watch it land.
            AttitudeController.Ascent.Throttle = c.Throttle;
            if (FlightGlobals.ActiveVessel == v)
                FlightInputHandler.state.mainThrottle = (float)c.Throttle;
            // ---- ULLAGE. THE SIGN IS CONFIRMED; THE DELIVERY IS BEST-EFFORT. ----
            // NEGATIVE Z is FORWARD translation. Verified at three independent MechJeb sites rather
            // than assumed, one of which is literally this manoeuvre:
            //
            //      MechJebModuleNodeExecutor.cs:193   enable RCS, wait for alignment, s.Z = -1.0F
            //      MechJebModuleNodeExecutor.cs:161   node burned on RCS only,        s.Z = -1.0F
            //      MechJebModuleThrustController.cs:559  s.Z = -s.mainThrottle, RCS assisting thrust
            //
            // ⚠ BUT MechJeb writes into the FlightCtrlState handed to its OnFlyByWire callback,
            // and we are writing `v.ctrlState` from Update(). KSP rebuilds ctrlState from input each
            // FixedUpdate, so this write may simply be overwritten before it does anything.
            //
            // Left as-is deliberately: STOCK KSP DOES NOT MODEL ULLAGE AT ALL - there is no settling
            // requirement without Real Fuels - so nothing depends on it working. It is realism
            // flavour during the six-second settle. If a fuel mod ever makes ullage matter, this has
            // to move to an OnFlyByWire callback, and that is the fix rather than a bigger number.
            // Only written while actually ullaging, so it cannot stamp on anything else.
            // ---- ⛔ ULLAGE GOES THROUGH THE CALLBACK NOW, NOT `v.ctrlState.Z` FROM HERE. ----
            // KSP rebuilds ctrlState from input every FixedUpdate, so a Z written from Update was
            // overwritten before physics ever saw it - the settle command was almost certainly a
            // no-op for its entire life. It cost nothing in stock, which has no ullage model, and
            // would have cost an ignition under Real Fuels. The controller owns the FlightCtrlState,
            // so it owns this too.
            AttitudeController.Ascent.UllageFore = c.UllageFore;
            SetAscentRcs(v, c);

            // ---- DROP THE S2 AND FINISH ON THE DRACOS ----
            if (c.SeparateS2) SeparateSecondStage(v);

            // ---- MECO STAGES ON COMMAND, NOT ON STARVATION ----
            // The guidance decides when the first stage is done, at its own 60 km apoapsis target,
            // while the booster still has propellant for boostback. Starvation staging is kept only
            // as the FALLBACK for an engine that quits unexpectedly.
            //
            // ---- ⛔ AND ONLY WHILE THERE IS A BOOSTER TO SEPARATE. ----
            // The MECO stage command exists to shed the FIRST STAGE. If the autopilot is re-engaged
            // AFTER the booster is already gone - which is exactly what a crew does after a
            // booster-recovery handback disengages it - `Ascent.Guide` re-runs from the start and
            // re-issues MECO, and this then stages AGAIN with nothing to shed, dropping the S2 and
            // then the payload one stage at a time. Measured 2026-08-17: a re-engage at 120 km
            // apoapsis ran MECO->stage 4->CIRCULARISE->stage 3->stage 2 and disengaged "staged twice
            // with no thrust", stripping the circularisation engine and losing the orbit. `HasBooster`
            // is the correct gate: you can only separate a booster that is still attached.
            if (c.Stage && HasBooster(v) && Planetarium.GetUniversalTime() - lastStageAt > 2.0)
            {
                lastStageAt = Planetarium.GetUniversalTime();
                blindStages = 0;
                // ---- SEPARATE THE S1 BY CAPABILITY (Crew-2). ----
                // A blind StageManager.ActivateNextStage() collapsed the S1 decouple and the MVac
                // activation into one frame and destroyed the engine (measured 2026-08-22). So fire
                // exactly the interstage decoupler and light the MVac later, after ullage.
                if (SeparateBooster(v))
                    Debug.Log(Tag + "MECO - booster separated by capability (interstage decoupler)");
                else
                {
                    StageManager.ActivateNextStage();      // fallback: no interstage decoupler found
                    Debug.LogWarning(Tag + "MECO - no interstage decoupler found; fell back to staging, "
                                         + "now stage " + StageManager.CurrentStage);
                }
            }
            else if (c.Phase == AscentPhase.VerticalRise)
                // Pad start: ignite the S1 by capability, spool, confirm thrust, THEN release the
                // erector/clamp by capability - the real Falcon-9 sequence.
                RoLaunch(v, a);
            else if (c.Phase == AscentPhase.BurnToApoapsis)
                // The capability ignition OWNS the second stage - during the ullage settle the throttle is
                // up with no thrust yet, and the starvation Stage() fallback would read that as a dead
                // stage and fire ActivateNextStage, the cascade we removed.
                IgniteSecondStageWhenSettled(v, c, a);
            else
                Stage(v, c, a);
        }

        /// <summary>
        /// RCS on ONLY when there is no engine gimbal to hold attitude, and off otherwise.
        ///
        /// ---- ⛔ RCS THROUGH POWERED FLIGHT IS WASTE. ----
        /// While any engine is lit the gimbal controls attitude, so cold gas / monopropellant spent on
        /// RCS during the vertical rise, the gravity turn and the MVac burn buys nothing - the crew flew
        /// flight_0823 with RCS on for the ENTIRE ascent. RCS is wanted only when the phase is UNPOWERED
        /// (Meco/StageSep/Coast/Shutdown holds, which set c.Rcs) or we are settling propellant for an
        /// ignition (UllageFore). SET it either way - never just raise it - so it is turned OFF the moment
        /// the engine relights and the gimbal takes over. Docking/entry manage their own RCS elsewhere.
        /// </summary>
        private static void SetAscentRcs(Vessel v, AscentCommand c)
        {
            if (v == null) return;
            bool want = c.Rcs || c.UllageFore > 0.01;
            if (v.ActionGroups[KSPActionGroup.RCS] != want)
                v.ActionGroups.SetGroup(KSPActionGroup.RCS, want);
        }

        /// <summary>
        /// A one-line comparison of our flight to the REAL Crew-2 launch clock, appended to the
        /// phase-transition log - the "match the livestream" reference (user 2026-08-22). Empty on stock
        /// (Kerbin) or before liftoff, so it never touches the stock build. See pure/Crew2Timeline.cs.
        /// </summary>
        private static string Crew2Sync(Vessel v)
        {
            if (liftoffUt <= 0.0) return "";
            double met = Planetarium.GetUniversalTime() - liftoffUt;
            if (met < 0.0) return "";
            Crew2Event cur = Crew2Timeline.Current(met);
            string s = "  | Crew-2 T+" + met.ToString("F0") + "s: " + cur.Name;
            Crew2Event nxt;
            if (Crew2Timeline.Next(met, out nxt))
                s += ", next " + nxt.Name + " in " + Crew2Timeline.TimeToNext(met).ToString("F0") + "s";
            return s;
        }

        // ---- UPFG SECOND-STAGE GUIDANCE (RSS/RO) ----
        private static UpfgState upfgState;
        /// <summary>Once the M-Vac is lit, UPFG owns the stage to SECO - even past the pure runaway abort.</summary>
        private static bool upfgActive;

        /// <summary>
        /// Whether UPFG may fly the second-stage insertion. DEFAULT OFF, on the flight record.
        ///
        /// ⛔ MEASURED 0/8: across flight_0823_201351 .. flight_0824_031348, UPFG reached orbit ZERO times
        /// and the gravity-turn + CIRCULARISE fallback reached it SEVEN times (pe 178-184 km). The one
        /// flight where UPFG actually engaged (flight_0824_031348) is the ONLY suborbital one: it held
        /// ~55 deg nose-up too long, lofted the apoapsis to 1564 km, and ran the marginal S2 dry 330 m/s
        /// short. UPFG "working" is the failure mode here. Until its over-loft is fixed and re-proven in
        /// the point-mass sim, the S2 flies the path that has a 7/7 record. Set true to re-test UPFG.
        /// </summary>
        [Tunable] public static bool UpfgEnabledS2 = false;
        /// <summary>Command SECO when UPFG's time-to-go falls to this, seconds.</summary>
        private const double UpfgSecoTgoS = 0.2;
        /// <summary>Minimum M-Vac thrust (kN) before UPFG may Init/Step. Above ullage (~1 kN), below the
        /// lit M-Vac (~800 kN). Guards the predictor-corrector from an ullage-poisoned seed. See the
        /// activation block and scratchpad/sim (Case B vs C).</summary>
        private const double UpfgMinThrustKn = 200.0;
        private static double lastUpfgLog;

        /// <summary>
        /// Fly the second stage to orbit under UPFG for one tick: build the target/vehicle from the live
        /// state, solve, steer the thrust vector, hold full throttle, and cut at SECO. Returns false if the
        /// solve is not usable (no thrust/mass/Isp or a bad solution), so the caller falls back to the
        /// gravity-turn loft. The inertial frame is the instantaneous world frame (position relative to the
        /// body, orbital velocity) - consistent for R, V and iF within a tick, which is all UPFG needs.
        /// </summary>
        private static bool UpfgFlyS2(Vessel v, AscentInputs a)
        {
            if (v.mainBody == null) return false;
            V3 r = ToV3(v.CoM - v.mainBody.position);
            V3 vel = ToV3(v.obt_velocity);
            double mu = v.mainBody.gravParameter;
            if (r.magnitude <= 0.0 || vel.magnitude <= 0.0) return false;

            // Target: the plane we are already in (the launch put us in the ISS plane), the parking orbit,
            // circular.
            // ⛔ FRAME-AGNOSTIC Iy. KSP's world frame is LEFT-handed, so `-r x v` points the wrong way and
            // UPFG's target velocity came out RADIAL, not prograde - the stage lofted to a 300 km apoapsis
            // while orbital speed barely moved (flight_0822_090116). Instead derive Iy from the ACTUAL
            // horizontal velocity: with `Iy = prograde x up`, block 8's `iz = up x Iy` returns `prograde`
            // exactly (double-cross identity), so the desired cutoff velocity is horizontal-prograde
            // regardless of handedness. Self-consistent because every cross here uses the same formula.
            UpfgTarget t = new UpfgTarget();
            V3 up = V3.Normalize(r);
            V3 prograde = V3.Normalize(vel - V3.Dot(vel, up) * up);   // velocity with the radial part removed
            t.Iy = V3.Normalize(V3.Cross(prograde, up));
            t.RadiusM = v.mainBody.Radius + Target.AltitudeM;
            t.SpeedMps = Math.Sqrt(mu / t.RadiusM);
            t.GammaRad = 0.0;

            UpfgVehicle veh = new UpfgVehicle();
            veh.ThrustN = a.AvailableThrust * 1000.0;         // kN -> N
            veh.MassKg = v.GetTotalMass() * 1000.0;           // t -> kg
            veh.ExhaustVel = S2ExhaustVel(v);
            if (veh.MassKg <= 0.0 || veh.ExhaustVel <= 0.0) return false;
            // ⛔ A thrust reading below the ullage/lit-engine floor must NEVER reach Step: it would seed or
            // rescale the persistent state with an astronomical tu and loft the stage. Hold prograde this
            // tick instead (rare post-activation - throttle is pinned full - but a hard guard, not a hope).
            if (a.AvailableThrust < UpfgMinThrustKn)
            {
                AttitudeController.Ascent.SteerTo(v, new Vector3d(prograde.x, prograde.y, prograde.z),
                    (v.CoM - v.mainBody.position).normalized);
                AttitudeController.Ascent.Throttle = 1.0;
                if (FlightGlobals.ActiveVessel == v) FlightInputHandler.state.mainThrottle = 1.0f;
                return true;
            }

            UpfgGuidance g = Upfg.Step(r, vel, mu, t, veh, ref upfgState);
            if (!g.Valid || double.IsNaN(g.TgoS)) return false;

            if (g.TgoS <= UpfgSecoTgoS)
            {
                // SECO - orbit insertion complete. Shed the S2 and hand to the capsule, as at Done.
                AttitudeController.Ascent.Throttle = 0.0;
                if (FlightGlobals.ActiveVessel == v) FlightInputHandler.state.mainThrottle = 0f;
                SeparateSecondStage(v);
                LaunchWindowOps.MeasureAtInsertion(v, liftoffUt, liftoffLonDeg);
                Debug.Log(Tag + "SECO - UPFG orbit insertion complete" + Crew2Sync(v));
                Disengage("SECO - UPFG orbit insertion");
                return true;
            }

            // Steer the thrust vector and hold full throttle.
            Vector3d iF = new Vector3d(g.IF.x, g.IF.y, g.IF.z);
            Vector3d upW = (v.CoM - v.mainBody.position).normalized;
            AttitudeController.Ascent.SteerTo(v, iF, upW);
            AttitudeController.Ascent.Throttle = 1.0;
            if (FlightGlobals.ActiveVessel == v) FlightInputHandler.state.mainThrottle = 1.0f;

            if (Time.realtimeSinceStartup - lastUpfgLog > 2f)
            {
                lastUpfgLog = Time.realtimeSinceStartup;
                // iF pitch above local horizontal - the one-glance health check: it should lay over from
                // ~58 deg toward 0, NOT sit near 84 (the ullage-poisoned loft signature).
                double ifPitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, V3.Dot(g.IF, up)))) * 57.29578;
                Debug.Log(Tag + "UPFG  tgo " + g.TgoS.ToString("F0") + "s  pitch "
                          + ifPitch.ToString("F0") + "deg  ap "
                          + (a.ApoapsisM / 1000.0).ToString("F1") + "  pe "
                          + (a.PeriapsisM / 1000.0).ToString("F1") + "  orb "
                          + v.obt_velocity.magnitude.ToString("F0") + "/" + t.SpeedMps.ToString("F0")
                          + Crew2Sync(v));
            }
            return true;
        }

        private static V3 ToV3(Vector3d w) { return new V3(w.x, w.y, w.z); }

        // ---- RO LAUNCH: ignite, spool, confirm thrust, THEN release the clamp (by capability) ----
        private static bool clampReleased;
        private static double s1IgniteAt = -1.0;
        /// <summary>Spool time before the clamp releases - real F9 ignites at T-3 s and lifts off at T-0.</summary>
        private const double LaunchSpoolS = 3.0;

        /// <summary>
        /// The real Falcon-9 pad start, RSS/RO. Ignite the first stage WHILE CLAMPED, let it spool for
        /// LaunchSpoolS, confirm it is actually making thrust-to-weight above 1 (excluding the erector's
        /// own mass, which stays on the ground), and only THEN release the hold-down - so the stack never
        /// drops onto engines that failed to light. Stock keeps its ActivateNextStage launch.
        /// </summary>
        private static void RoLaunch(Vessel v, AscentInputs a)
        {
            double now = Planetarium.GetUniversalTime();
            if (s1IgniteAt < 0.0)
            {
                int lit = IgniteFirstStage(v);
                if (lit > 0)
                {
                    s1IgniteAt = now;
                    Debug.Log(Tag + "S1 IGNITION - " + lit + " engine(s) lit, spooling up while clamped");
                }
                return;
            }
            if (clampReleased || now - s1IgniteAt < LaunchSpoolS || a.AvailableThrust <= 1.0) return;

            double rocketT = v.GetTotalMass() - ErectorMassT(v);
            double twr = (rocketT > 0.0) ? a.AvailableThrust / (rocketT * 9.80665) : 0.0;
            if (twr >= 1.0)
            {
                ReleaseLaunchClamp(v);
                clampReleased = true;
                Debug.Log(Tag + "LIFTOFF - clamp released at TWR " + twr.ToString("F2")
                              + " (rocket " + rocketT.ToString("F0") + " t)" + Crew2Sync(v));
            }
            else if (now - s1IgniteAt > LaunchSpoolS + 6.0)
                Debug.LogWarning(Tag + "PAD HOLD - TWR only " + twr.ToString("F2")
                                     + " after ignition; NOT releasing the clamp onto a stack that cannot fly");
        }

        /// <summary>Activate the first-stage engines by capability (not by staging). Returns the count lit.</summary>
        ///
        /// ---- ⛔ ONLY THE ALL-ENGINES MODE. THE OCTAWEB CARRIES THREE ENGINE MODULES. ----
        /// The Tundra octaweb (`TE_19_F9_S1_Engine`) is ONE part holding THREE `ModuleEngines` - the
        /// ascent "all nine", the entry-burn "three", and the landing "centre" - and all three ship
        /// `isEnabled = true`. Activating every ModuleEngines on the part therefore lit all three modes
        /// at once. Proven on flight_0822_201219: "S1 IGNITION - 3 engine(s) lit", ascent thrust ~11 900 kN
        /// (9 Merlins make ~8 500), a_enginesLit = 3, and the descent-engine PLUMES rendering during ascent -
        /// which is what the crew saw as "the plume set for the descending engine, not the ascending". Worse,
        /// the two descent modules' `heatProduction` (196-248 each) stacked onto the ascent module and cooked
        /// the S1 tank: skin climbed 300 -> 644 K through the climb WHILE dynamic pressure fell 25 -> 1.8 kPa
        /// (aero heating scales with rho*v^3 and was dropping - so the source was the engines, not the air),
        /// and the pre-heated tank let go during boostback (TE.19.F9.S1.Tank Exploded, T+6 s after sep).
        ///
        /// The octaweb starts in the all-engines mode (craft: `selectedIndex = 0`), so at ignition we light
        /// ONLY the engines that belong to that mode. `EngineIdIsMode(id, ModeAllEngines)` is `!three &&
        /// !centre`, so a plain engine (individual-Merlin boosters, empty/normal engineID) still lights -
        /// this only ever SKIPS the octaweb's descent modules. BoosterRecovery lights those later, when it
        /// steps the switch to the mode the entry/landing burn actually needs.
        private static int IgniteFirstStage(Vessel v)
        {
            int lit = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsBooster(v.parts[i].name)) continue;
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (!es[m].EngineIgnited && !es[m].flameout
                        && VehicleParts.EngineIdIsMode(es[m].engineID, VehicleParts.ModeAllEngines))
                    { es[m].Activate(); lit++; }
            }
            return lit;
        }

        /// <summary>Release the erector/launch clamp by capability: swing the arm clear, then decouple.</summary>
        private static void ReleaseLaunchClamp(Vessel v)
        {
            FireCapability(v, VehicleParts.IsErector, "open erector");
            int n = FireCapability(v, VehicleParts.IsErector, "decouple");
            if (n == 0)
                Debug.LogWarning(Tag + "clamp release: no 'decouple' answered on an '"
                                     + VehicleParts.ErectorMarker + "' part - is the erector on the craft?");
        }

        private static double ErectorMassT(Vessel v)
        {
            double m = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
                if (VehicleParts.IsErector(v.parts[i].name))
                    m += v.parts[i].mass + v.parts[i].GetResourceMass();
            return m;
        }

        /// <summary>
        /// Fire a named capability (the ACTIVE event if the module offers one, else the action) on every
        /// part matching `match`. The event-then-action pattern EntryOps documents - a ModuleTundraDecoupler
        /// answers to both, and an inactive event is not a missing one. Returns how many fired.
        /// </summary>
        private static int FireCapability(Vessel v, System.Func<string, bool> match, string cap)
        {
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (!match(p.name)) continue;
                for (int mod = 0; mod < p.Modules.Count; mod++)
                {
                    PartModule pm = p.Modules[mod];
                    for (int e = 0; e < pm.Events.Count; e++)
                    {
                        BaseEvent ev = pm.Events[e];
                        if (ev != null && ev.active && ev.guiName != null
                            && string.Equals(ev.guiName, cap, StringComparison.OrdinalIgnoreCase))
                        { ev.Invoke(); n++; }
                    }
                }
            }
            if (n > 0) return n;
            for (int i = 0; i < v.parts.Count; i++)      // fall back to the action
            {
                Part p = v.parts[i];
                if (!match(p.name)) continue;
                for (int mod = 0; mod < p.Modules.Count; mod++)
                {
                    PartModule pm = p.Modules[mod];
                    for (int act = 0; act < pm.Actions.Count; act++)
                    {
                        BaseAction ac = pm.Actions[act];
                        if (ac != null && ac.guiName != null
                            && string.Equals(ac.guiName, cap, StringComparison.OrdinalIgnoreCase))
                        { ac.Invoke(new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate)); n++; }
                    }
                }
            }
            return n;
        }

        /// <summary>Current effective exhaust velocity of the lit second-stage engine, m/s. Falls back
        /// to the M-Vac vacuum figure (Isp 345) if none is readable.</summary>
        private static double S2ExhaustVel(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsSecondStage(v.parts[i].name)) continue;
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (es[m].EngineIgnited && !es[m].flameout && es[m].realIsp > 1.0)
                        return es[m].realIsp * 9.80665;
            }
            return 345.0 * 9.80665;
        }

        // ---- SECOND-STAGE IGNITION: RO-CORRECT, BY CAPABILITY ----
        // Max attempts to light the MVac. The RO Merlin1DVac has ignitions = 4 (each consuming a
        // TEATEB charge); we spend at most this many, leaving a margin, and never spam - one attempt,
        // then a re-light only if the first produced no thrust (ignitionReliabilityStart ~0.967, so a
        // real ~3% chance of a dud light). See CREW2_RSS_RESEARCH.md.
        private const int MaxS2IgnitionAttempts = 3;
        /// <summary>Let RCS settle the propellant this long before the first ullage-gated light.</summary>
        private const double S2SettleBeforeIgniteS = 2.0;
        /// <summary>Give a commanded light this long to produce thrust before calling it a dud.</summary>
        private const double S2IgniteRetryGapS = 1.5;
        private static int s2IgnitionAttempts;
        private static double lastS2IgniteAt = -99.0;

        /// <summary>Live worst MVac ullage stability this tick, 0..1; -1 when not applicable (stock, or no
        /// ullage-limited engine live). For the pages and the recorder - the flight computer's read of
        /// exactly how settled the propellant is right now.</summary>
        public static double S2Ullage = -1.0;
        private static double lastUllageLog = -99.0;

        /// <summary>
        /// Light the MVac DIRECTLY once ullage has settled the tanks - not by staging. ModuleEnginesRF
        /// needs settled propellant (ullage = True), so we wait S2SettleBeforeIgniteS of RCS-fore
        /// settling first; if the light produces no thrust we retry, within the ignition budget, rather
        /// than staging the stack apart (the failure the whole capability path exists to prevent).
        /// </summary>
        private static void IgniteSecondStageWhenSettled(Vessel v, AscentCommand c, AscentInputs a)
        {
            if (c.Phase != AscentPhase.BurnToApoapsis) return;
            if (a.PhaseElapsedS < S2SettleBeforeIgniteS) return;           // let RCS settle first
            if (a.AvailableThrust > 1.0) return;                          // already lit and burning
            if (s2IgnitionAttempts >= MaxS2IgnitionAttempts) return;      // keep an ignition in reserve
            if (Planetarium.GetUniversalTime() - lastS2IgniteAt < S2IgniteRetryGapS) return;

            // ---- ⛔ LIGHT ONLY WHEN THE PROPELLANT IS ACTUALLY SETTLED (RealFuels LIVE ullage). ----
            // A blind timed light into unsettled propellant flames out on "No propellants" AND spends one
            // of the four ignitions - flight_0822_211853 did exactly that, with nothing on the glass. So
            // read the MVac's own ullage state every tick and hold the light until it is genuinely settled.
            // The guidance keeps the RCS-fore on meanwhile (ULLAGE + LIGHT). This never wastes an ignition
            // on floating propellant, and on a flight where the propellant IS settled from boost it lights
            // the instant it is ready. Stock engines report no ullage (count 0) and fall straight through.
            int ullageN;
            S2Ullage = UllageProbe.VesselWorst(v, delegate(Part p) { return VehicleParts.IsSecondStage(p.name); },
                                               out ullageN);
            if (ullageN > 0 && S2Ullage >= 0.0 && S2Ullage < UllageProbe.SettledStability)
            {
                double now = Planetarium.GetUniversalTime();
                if (now - lastUllageLog > 3.0)
                {
                    lastUllageLog = now;
                    Debug.Log(Tag + "MVac HOLDING for ullage - propellant "
                              + (S2Ullage * 100.0).ToString("F0") + "% settled, need "
                              + (UllageProbe.SettledStability * 100.0).ToString("F0")
                              + "%. RCS still settling; not spending an ignition.");
                }
                return;   // do NOT light into floating propellant
            }

            int lit = IgniteSecondStage(v);
            lastS2IgniteAt = Planetarium.GetUniversalTime();
            // Only a REAL (re)activation spends an ignition. An engine that is already lit but not yet
            // thrusting is just waiting for the RCS to finish settling the tanks - leave it, don't burn
            // a TEATEB charge on it. Without this, a stalled MVac would eat the whole 3-attempt budget
            // doing nothing ("activated 0"), which is exactly what the pre-ullage light looked like.
            if (lit > 0)
            {
                s2IgnitionAttempts++;
                Debug.Log(Tag + "MVac ignition attempt " + s2IgnitionAttempts + "/" + MaxS2IgnitionAttempts
                              + " - activated " + lit + " engine module(s) after ullage settle");
            }
        }

        /// <summary>
        /// Activate the second-stage engine(s) by capability. Returns the count of engines we actually
        /// commanded to light. An engine that flamed out on a failed ignition (RO ~3% dud rate) is shut
        /// down first so Activate re-attempts it; one that is lit and merely ullage-stalled is left
        /// alone to spool up on its own once the propellant settles.
        /// </summary>
        private static int IgniteSecondStage(Vessel v)
        {
            int lit = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsSecondStage(v.parts[i].name)) continue;
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    ModuleEngines e = es[m];
                    if (e.EngineIgnited && e.flameout) e.Shutdown();   // reset a failed light to re-try
                    if (!e.EngineIgnited) { e.Activate(); lit++; }
                }
            }
            return lit;
        }

        /// <summary>
        /// Separate the booster by firing the interstage decoupler as a CAPABILITY - the "decouple"
        /// event if the module offers an active one, else the action (a ModuleTundraDecoupler answers to
        /// both, and an inactive event is not a missing one - the same trap EntryOps documents). Returns
        /// true if a decoupler actually fired. See VehicleParts.IsInterstage and falcon-detect-by-capability.
        /// </summary>
        private static bool SeparateBooster(Vessel v)
        {
            if (boosterSeparated) return true;
            int n = 0, inactive = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (!VehicleParts.IsInterstage(p.name)) continue;
                for (int mod = 0; mod < p.Modules.Count; mod++)
                {
                    PartModule pm = p.Modules[mod];
                    for (int e = 0; e < pm.Events.Count; e++)
                    {
                        BaseEvent ev = pm.Events[e];
                        if (ev == null || ev.guiName == null) continue;
                        if (!string.Equals(ev.guiName, "decouple", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!ev.active) { inactive++; continue; }
                        ev.Invoke();
                        n++;
                    }
                }
            }
            if (n == 0)                                    // inactive event, or none: try the action
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    if (!VehicleParts.IsInterstage(p.name)) continue;
                    for (int mod = 0; mod < p.Modules.Count; mod++)
                    {
                        PartModule pm = p.Modules[mod];
                        for (int act = 0; act < pm.Actions.Count; act++)
                        {
                            BaseAction ac = pm.Actions[act];
                            if (ac == null || ac.guiName == null) continue;
                            if (!string.Equals(ac.guiName, "decouple", StringComparison.OrdinalIgnoreCase))
                                continue;
                            ac.Invoke(new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate));
                            n++;
                        }
                    }
                }
            }
            if (n > 0) { boosterSeparated = true; return true; }
            if (inactive > 0)
                Debug.LogWarning(Tag + "booster sep: '" + VehicleParts.InterstageMarker
                                     + "' decouple event(s) all inactive and no action answered");
            return false;
        }

        private static bool boosterSeparated;

        /// <summary>
        /// Point the vehicle. Pitch and heading are converted into a world direction through the
        /// LOCAL HORIZON frame, which is the frame those two angles are defined in - deriving it from
        /// the vessel's own transform instead would make the command mean something different at
        /// every attitude.
        /// </summary>
        private static void Steer(Vessel v, AscentCommand c)
        {
            Vector3d up = (v.CoM - v.mainBody.position).normalized;
            Vector3d north = Vector3d.Exclude(up, v.mainBody.transform.up).normalized;
            Vector3d east = Vector3d.Cross(up, north).normalized;

            double hdg = c.HeadingDeg * Math.PI / 180.0;
            double pit = c.PitchDeg * Math.PI / 180.0;

            Vector3d horizontal = north * Math.Cos(hdg) + east * Math.Sin(hdg);
            Vector3d dir = horizontal * Math.Cos(pit) + up * Math.Sin(pit);

            // COAST and CIRCULARISE want PROGRADE, not a horizon-relative pitch of zero: by then the
            // vehicle is nearly orbital and the two differ by several degrees, which is the whole
            // periapsis.
            // COAST holds prograde. CIRCULARISE steers along the CIRCULARISATION DV, not prograde -
            // near apoapsis those differ by several degrees, and steering prograde is what raises
            // apoapsis instead of periapsis. This is the pointing half of the escape-trajectory bug.
            //
            if (c.Phase == AscentPhase.Coast)
            {
                Vector3d pro = v.obt_velocity.normalized;
                if (pro.sqrMagnitude > 0.5) dir = pro;
            }
            // ---- ⛔ RSS/RO SECOND STAGE: LOFT ABOVE PROGRADE, THEN FLATTEN. ----
            // MEASURED flight_0822_045358: the RO M-Vac (TWR ~0.82 at ignition) apexes at ~113 km -
            // INSIDE Earth's 140 km atmosphere - and burning pure prograde there raises periapsis, not
            // apoapsis, so the apoapsis stalls at ~124 km and the stack DESCENDS back into the air
            // (skin 372 -> 1751 K) and re-enters. A low-TWR upper stage must keep climbing out of the
            // atmosphere: aim ABOVE prograde by an angle that scales with how far the apoapsis is short
            // of target, flattening to prograde as apoapsis approaches target, after which Coast +
            // Circularise finish it. Interim heuristic (superseded by PSG); gains are [Tunable]. Stock's
            // higher-TWR turn keeps its pitch law and never enters this branch.
            else if (c.Phase == AscentPhase.BurnToApoapsis)
            {
                Vector3d pro = v.obt_velocity.normalized;
                if (pro.sqrMagnitude > 0.5)
                {
                    double deficit = (Target.AltitudeM - v.orbit.ApA) / Target.AltitudeM;
                    if (deficit < 0.0) deficit = 0.0;
                    double loftDeg = deficit * S2LoftGainDeg;
                    if (loftDeg > S2MaxLoftDeg) loftDeg = S2MaxLoftDeg;
                    Vector3d upPerp = up - Vector3d.Project(up, pro);   // 'up' made perpendicular to pro
                    if (loftDeg > 0.05 && upPerp.sqrMagnitude > 1e-6)
                    {
                        double l = loftDeg * Math.PI / 180.0;
                        dir = (pro * Math.Cos(l) + upPerp.normalized * Math.Sin(l)).normalized;
                    }
                    else dir = pro;
                }
            }
            else if (c.Phase == AscentPhase.Circularise)
            {
                if (circDv.sqrMagnitude > 0.01) dir = circDv.normalized;
                else dir = v.obt_velocity.normalized;
            }

            // ---- NEVER CALL SetMode HERE. IT UNDOES THE LINE BELOW IT. ----
            // First flight, 2026-08-06: this called SetMode(StabilityAssist) every frame. Stability
            // assist means "hold the attitude you have", so it re-locked to the CURRENT attitude each
            // frame and threw away the target that was set immediately after. The vehicle simply held
            // whatever it was already doing - which at circularisation was still pitched up from the
            // turn, so a two-and-a-half minute burn raised APOAPSIS from 86 km to 271 km and left
            // periapsis at -515 km. The guidance was right the whole time; the steering never
            // listened.
            //
            // MechJebModuleAttitudeController.cs:355-372 is the pattern, and it does three things
            // this did not: it never touches SetMode, it only sets the action group when SAS is OFF,
            // and it passes reset=false on a large slew and reset=true on small corrections.
            // ---- OUR OWN CONTROLLER, NOT SAS ----
            // The roll reference is what SAS could not take:  commands where the TOP
            // faces as well as where the nose points, and an uncommanded roll on a launch vehicle
            // is most of what the wandering was. Up-hint is the local vertical during the
            // atmospheric climb and the orbit normal above it - the same choice F9I makes.
            // ---- ⛔ AND DURING VERTICAL FLIGHT THE LOCAL VERTICAL IS NOT A ROLL REFERENCE. ----
            // It IS the direction we are flying, so `up` and `dir` are parallel and the frame is
            // degenerate - which is how the controller's 180-degree fallback got exercised on every
            // single launch and spun the stack at 64 deg/s. Fixing the fallback stops the spin;
            // this stops us asking for the impossible in the first place.
            //
            // The horizontal heading, negated, is the continuous continuation of the same frame. As
            // pitch approaches 90 degrees, Exclude(dir, up) tends to exactly -horizontal, so the two
            // agree at the boundary and the vehicle does not snap when it crosses.
            Vector3d upHint = up;
            if (Math.Abs(Vector3d.Dot(up, dir)) > 0.999) upHint = -horizontal;
            AttitudeController.Ascent.SteerTo(v, dir, upHint);
            lastCommanded = dir;
        }

        private static Vector3d lastCommanded = Vector3d.zero;

        /// <summary>
        /// Is a human actually holding an attitude control? Reads the raw bindings, which contain
        /// only what the pilot is doing - unlike ctrlState, which also contains ours.
        ///
        /// Wrapped so a surprise in the input API costs the hand-back feature and not the autopilot:
        /// an exception here while flying a booster to a pad would be a bad trade.
        /// </summary>
        private static bool PilotInput(Vessel v)
        {
            // The raw bindings report what the PLAYER is holding, and the player is flying whichever
            // vessel has focus. While the camera is on the booster, a stick input is aimed at the
            // booster - disengaging the upper stage's ascent for it would be reading someone else's
            // controls.
            if (v == null || FlightGlobals.ActiveVessel != v) return false;
            try
            {
                if (Mathf.Abs(GameSettings.AXIS_PITCH.GetAxis()) > 0.2f) return true;
                if (Mathf.Abs(GameSettings.AXIS_YAW.GetAxis()) > 0.2f) return true;
                if (Mathf.Abs(GameSettings.AXIS_ROLL.GetAxis()) > 0.2f) return true;
                return GameSettings.PITCH_UP.GetKey() || GameSettings.PITCH_DOWN.GetKey()
                    || GameSettings.YAW_LEFT.GetKey() || GameSettings.YAW_RIGHT.GetKey();
            }
            catch (Exception) { return false; }
        }

        private static Vector3d circDv, circDvStart;
        private static double phaseStartedAt;

        /// <summary>The vehicle this autopilot is flying. NOT whichever one has the camera.</summary>
        private static Vessel ascentVessel;

        /// <summary>The upper stage, for the recorder - so its columns always mean one vehicle.</summary>
        public static Vessel AscentVessel { get { return ascentVessel; } }
        private static bool packedReported;
        private static double lastHandoverTry;
        private static double windowOpensUt, liftoffUt, liftoffLonDeg;
        /// <summary>Drop out of warp this long before the window opens, to settle.</summary>
        private const double WindowWarpLeadS = 5.0;
        private static bool windowWarped;
        private static float lastBurnLog = -999f;

        /// <summary>
        /// The dv that would make the orbit circular at the CURRENT radius.
        /// `vxcl(r, v)` in kOS is "v with the r component excluded" - the horizontal part of the
        /// velocity - which is Vector3d.Exclude here.
        /// </summary>
        private static Vector3d CirculariseDv(Vessel v)
        {
            if (v.mainBody == null || v.orbit == null) return Vector3d.zero;
            Vector3d r = v.CoM - v.mainBody.position;
            Vector3d vel = v.obt_velocity;
            double mag = Math.Sqrt(v.mainBody.gravParameter / r.magnitude);
            Vector3d horiz = Vector3d.Exclude(r, vel);
            if (horiz.sqrMagnitude < 1e-6) return Vector3d.zero;
            return horiz.normalized * mag - vel;
        }

        /// <summary>
        /// Is the first stage still attached?
        ///
        /// ⛔ This used to match `"K1"`, taken from the PAW title rather than `part.name`, so it was
        /// always false - which made SecondStage always TRUE and ran the FIRST stage on the SECOND
        /// stage's pitch law. That capped pitch at MECOangle, so the vehicle slammed 90 -> 45 degrees
        /// at 400 m in the thickest air. See VehicleParts.
        /// </summary>
        private static bool HasBooster(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
                if (VehicleParts.IsBooster(v.parts[i].name)) return true;
            return false;
        }

        /// <summary>
        /// Shed the S2 and light the capsule's own engines.
        ///
        /// ---- WHY THE ORBIT IS NOT FINISHED ON THE MVac ----
        /// It was, and the 21:01 flight ended in an 86 x 84 km orbit weighing 20.5 tonnes with the
        /// whole second stage still bolted on. Nothing after that works: the de-orbit burn is sized
        /// for a capsule, entry needs a heat shield facing forward and a 20 t stack will not hold
        /// that attitude, and the S2 is left in a stable orbit as permanent debris.
        ///
        /// F9I's FalconSepS2 does it at a 40 km periapsis so the S2 comes down promptly, then closes
        /// the orbit on the Dragon's SuperDracos - 228 kN on the pod, about 400 m/s in the tank,
        /// roughly 37 m/s needed from that gate. MEASURED, the S2 separation log.
        ///
        /// ⚠ THE RIGHT DECOUPLER. `TE.19.C.Dragon.Decoupler` drops the S2 alone. The trunk decoupler
        /// above it would take the trunk - and the solar panels and radiators - with it. See
        /// VehicleParts and `falcon-dragon-two-decouplers`.
        ///
        /// NO SETTLING PAUSE, deliberately: FalconSepS2's own note is that the Dracos are canted
        /// away from the S2, so unlike the MVac there is nothing to wait for.
        /// </summary>
        private static void SeparateSecondStage(Vessel v)
        {
            // ---- ⛔ THE CAPSULE IS NOT THE LAUNCH VEHICLE. GIVE IT ITS OWN RATE CEILING. ----
            // From here `AttitudeController.Ascent` flies the Dragon alone - a tenth the inertia of
            // the stack it was tuned for. Left at the ascent ceiling of 2 deg/s it could not slew
            // at all: MEASURED on the flight that docked and landed, the capsule averages 1.85
            // deg/s docking, 2.62 on approach and 1.87 through the de-orbit, so 2 sits AT the
            // working average rather than above it.
            //
            // 10 gives roughly four times the flown average. The 40-125 deg/s transients in that
            // same recording are the controller misbehaving, not a requirement, and are exactly
            // what a ceiling exists to prevent.
            AttitudeController.Ascent.MaxRateDps = Attitude.CapsuleMaxRateDps;
            if (s2Separated) return;
            s2Separated = true;

            // ---- ⛔ SHUT THE M-VAC DOWN BEFORE DROPPING THE S2. ----
            // At SECO the engine is still IGNITED (throttle 0 only idles it). Once decoupled the spent
            // stage is its own vessel and KSP resumes its last throttle, so it flies off UNDER POWER -
            // MEASURED flight_0822_105240: the 9.9 t S2 ran to 838 x 76 km at 5 G, stole the camera focus
            // and blocked the quicksave ("Cannot save"), and the ascent then re-engaged on that debris and
            // aborted. Shutting the engine while it is still ours makes the jettisoned stage inert. The
            // M-Vac carries the S2 marker (`.S2.`); the capsule lights its own Dracos below.
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsSecondStage(v.parts[i].name)) continue;
                System.Collections.Generic.List<ModuleEngines> se =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < se.Count; m++)
                    if (se[m].EngineIgnited) se[m].Shutdown();
            }

            bool fired = false;
            for (int i = 0; i < v.parts.Count && !fired; i++)
            {
                Part p = v.parts[i];
                if (!VehicleParts.IsDragonDecoupler(p.name)) continue;
                System.Collections.Generic.List<ModuleDecouple> ds =
                    p.Modules.GetModules<ModuleDecouple>();
                for (int m = 0; m < ds.Count; m++)
                {
                    if (ds[m].isDecoupled) continue;
                    ds[m].Decouple();
                    fired = true;
                    Debug.Log(Tag + "S2 SEP - dropped on '" + p.name + "' at "
                              + (v.orbit.ApA / 1000.0).ToString("F1") + " x "
                              + (v.orbit.PeA / 1000.0).ToString("F1") + " km");
                    break;
                }
            }

            if (!fired)
            {
                // Say it, do not silently stay stacked - that is the failure the crew has to know
                // about before they try to de-orbit a vehicle twice the mass they expect.
                Debug.LogWarning(Tag + "S2 SEP FAILED - no undecoupled '"
                                     + VehicleParts.DragonDecouplerMarker + "' on this vehicle. "
                                     + "Circularising STACKED; de-orbit and entry will not work.");
                return;
            }

            // Light the capsule's own engines. Staging would be the lazy way and it is dangerous
            // here: the next item in a Dragon's stack is the trunk decoupler.
            int lit = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsPod(v.parts[i].name)) continue;
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (!es[m].EngineIgnited && !es[m].flameout) { es[m].Activate(); lit++; }
            }
            Debug.Log(Tag + "Dracos armed (" + lit + " engine module(s)) - the capsule closes its "
                          + "own orbit from here");

            // ---- ⛔ DEPLOY THE SOLAR PANELS. Without this the battery drains to zero. ----
            // The Crew Dragon's array is on the trunk (TE.18.DRAGONV2.TRUNK), a ModuleDeployableSolarPanel
            // that LAUNCHES RETRACTED and nobody was extending it - so it made ~0 power and EC ran to zero
            // in orbit (flight_0824_013850: 1.0 -> 0), which then starves the RCS/guidance and kills the
            // rendezvous. The real vehicle's trunk cells are exposed once it is in orbit; do the same here,
            // now that we are out of the atmosphere (Dragon just separated at the parking orbit).
            DeploySolarPanels(v);
        }

        /// <summary>Extend every retractable solar panel on the vessel (once in orbit). A non-deployable /
        /// body-mounted panel is left alone; an already-extended one is a no-op. Safe to call repeatedly.</summary>
        private static void DeploySolarPanels(Vessel v)
        {
            if (v == null) return;
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleDeployableSolarPanel> ps =
                    v.parts[i].Modules.GetModules<ModuleDeployableSolarPanel>();
                for (int m = 0; m < ps.Count; m++)
                {
                    ModuleDeployableSolarPanel p = ps[m];
                    if (p.useAnimation && p.deployState != ModuleDeployablePart.DeployState.EXTENDED)
                    { p.Extend(); n++; }
                }
            }
            if (n > 0) Debug.Log(Tag + "solar panels deployed (" + n + ") - closing the power budget");
        }

        private static bool s2Separated;

        /// <summary>Metres between two vessels, or 0 when there is nothing to measure.</summary>
        public static double Range(Vessel a, Vessel b)
        {
            if (a == null || b == null || a == b) return 0.0;
            if (a.state == Vessel.State.DEAD || b.state == Vessel.State.DEAD) return 0.0;
            return Vector3d.Distance(a.CoM, b.CoM);
        }

        private static double AvailableThrust(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    ModuleEngines e = es[m];
                    if (!e.isEnabled || e.flameout) continue;
                    if (!e.EngineIgnited) continue;
                    t += e.finalThrust;
                }
            }
            return t;
        }

        /// <summary>
        /// Stage when thrust is being asked for and not delivered. Half a second of starvation, and
        /// a two-second lockout afterwards so one empty stage cannot cascade the whole stack.
        /// </summary>
        private static void Stage(Vessel v, AscentCommand c, AscentInputs a)
        {
            // ---- ⛔ IN RSS/RO, STARVATION STAGING IS FOR THE PAD START ONLY. ----
            // Stock keeps the previous behaviour: starvation staging recovers a dead stage in ANY phase.
            // In RSS that is a hazard - MEASURED flight_0822_022834, when the S1 flamed out a hair short
            // of the MECO target this fired in the gap and decoupled the booster + lit the MVac
            // destructively, before ullage, ahead of the clean capability sequence. On Earth the booster
            // hand-off is owned by the guidance (MECO on target OR flameout, Ascent.FirstStageSpent) and
            // SeparateBooster; the MVac by IgniteSecondStage. So there this only lights the S1 off the pad.
            if (c.Phase != AscentPhase.VerticalRise && c.Phase != AscentPhase.Idle) return;
            if (c.Throttle < 0.05) { starvedFor = 0.0; return; }

            if (a.AvailableThrust > 0.1)
            {
                // Thrust arrived, so whatever we last staged was the right thing to stage.
                starvedFor = 0.0;
                blindStages = 0;
                return;
            }
            starvedFor += Time.deltaTime;

            if (starvedFor < 0.5) return;
            if (Planetarium.GetUniversalTime() - lastStageAt < 2.0) return;
            if (StageManager.CurrentStage <= 0) { Disengage("out of stages"); return; }

            // ---- STOP AFTER TWO STAGINGS THAT PRODUCE NOTHING ----
            // First flight: the second stage ran dry during circularisation and this walked the
            // vehicle down through EVERY remaining stage - 5, 4, 3, then 2, 1, 0 - two seconds
            // apart, because each one produced no thrust and so justified the next. It stripped the
            // Dragon apart. The two-second lockout did not prevent the cascade, it only paced it.
            //
            // The bound has to be on FAILURE, not on rate: staging is how you recover from a spent
            // stage, so it must stay allowed, but a staging that does not light an engine is
            // evidence the ascent is over. Two of those in a row and we stop and say so, which is
            // always better than continuing to disassemble a crewed vehicle.
            if (blindStages >= 2)
            {
                Disengage("staged twice with no thrust - nothing left to light");
                return;
            }

            lastStageAt = Planetarium.GetUniversalTime();
            starvedFor = 0.0;
            blindStages++;
            StageManager.ActivateNextStage();
            Debug.Log(Tag + "autopilot staged - now stage " + StageManager.CurrentStage
                      + (blindStages > 1 ? "  (no thrust from the last one)" : ""));
        }

        private static int blindStages;
    }
}
