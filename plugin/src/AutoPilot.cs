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
 * ---- STEERING BORROWS SAS, DELIBERATELY, FOR NOW ----
 * Our own attitude controller with per-configuration gains is step 4 of the flight-software plan.
 * Until it exists, pointing SAS at a direction is honest and works: it is KSP's own controller doing
 * the inner loop while OUR guidance decides where to point. When the real controller lands, only
 * Steer() changes.
 *
 * ⚠ And it has a known consequence worth stating: SAS does not write `ctlPitch/ctlYaw/ctlRoll`
 * either, so the black-box columns that are dead in the existing corpus STAY dead until step 4. The
 * system-identification pass in the flight-software plan is still blocked.
 *
 * ---- STAGING IS OBSERVED, NOT SCHEDULED ----
 * Stage when we are ASKING for thrust and not getting any, for long enough that it is not a flicker.
 * A staging clock would be wrong the first time an engine failed, which is the one time it matters.
 */
using System;
using KSP.UI.Screens;      // StageManager. Namespace confirmed in MechJebModuleStagingController.cs:4
using UnityEngine;

namespace DragonScreen
{
    public static class AutoPilot
    {
        private const string Tag = "[DragonScreen] ";

        public static bool Engaged { get; private set; }
        public static AscentPhase Phase { get; private set; }
        public static AscentTarget Target = AscentTarget.Station();

        /// <summary>Last command flown, for the page to display. Never computed twice.</summary>
        public static AscentCommand Command;

        /// <summary>Last circularisation dv. On the recorder's row, not the command struct.</summary>
        public static double LastCircDvMps;

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

            Engaged = true;
            Phase = AscentPhase.Idle;
            // A fresh engagement is a fresh mission: never inherit a previous flight's recovery.
            BoosterRecovery.Reset();
            lastVesselId = v.persistentId;
            starvedFor = 0.0;
            blindStages = 0;
            phaseStartedAt = Planetarium.GetUniversalTime();
            lastCommanded = Vector3d.zero;
            FlightRecorder.Start(v);

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
            Debug.Log(Tag + "autopilot ENGAGED - target " + (Target.AltitudeM / 1000.0).ToString("F0")
                      + " km, heading " + Target.HeadingDeg.ToString("F0")
                      + ". ⚠ INTERIM: gravity turn, not the PSG ascent in FLIGHT_SOFTWARE_PLAN.md");
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            Phase = AscentPhase.Idle;
            Vessel v = FlightGlobals.ActiveVessel;
            // Hand the axes back, or the controller keeps flying a vehicle nobody is commanding.
            AttitudeController.Release(v);
            if (v != null) v.ctrlState.mainThrottle = 0f;
            FlightInputHandler.state.mainThrottle = 0f;
            Debug.Log(Tag + "autopilot DISENGAGED - " + why);
            // Keep recording through a booster recovery - that is the half we have never seen.
            if (!BoosterRecovery.Active) FlightRecorder.Stop(why);
        }

        /// <summary>
        /// One step. Called from the painter's Update, frame-guarded because three screens call it -
        /// the same guard as VesselData and FlightCommands, and here it would otherwise mean three
        /// autopilots fighting over one throttle.
        /// </summary>
        public static void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            // ⛔ ISSUE 7. `if (!Engaged) return;` used to come FIRST, so disengaging the ascent -
            // including by touching the stick - abandoned a booster in the middle of its landing
            // burn with the throttle wherever it happened to be. The recovery is its own mission
            // once it has started and outlives the ascent that spawned it.
            if (BoosterRecovery.Active) { BoosterRecovery.Tick(); return; }

            if (!Engaged) return;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.orbit == null || v.mainBody == null)
            {
                Disengage("no vessel");
                return;
            }

            // ---- ⛔ ISSUES 3 AND 4. STATICS MUST VALIDATE, NOT REMEMBER. ----
            // CLAUDE.md already carries this rule - it was written after the NAV map and navball
            // vanished on a revert - and the new flight software broke it again.
            // `BoosterRecovery.Reset()` existed and was called from NOWHERE, so across a revert or a
            // scene change `Active` could still be true, `booster`/`upperStage` held references to
            // destroyed vessels, and `HavePad` carried the PREVIOUS flight's landing zone.
            // VesselData does this correctly by watching persistentId; same pattern here.
            if (v.persistentId != lastVesselId)
            {
                if (lastVesselId != 0)
                {
                    Debug.Log(Tag + "new vessel - autopilot and booster recovery reset");
                    BoosterRecovery.Reset();
                    Phase = AscentPhase.Idle;
                    Engaged = false;
                    lastVesselId = v.persistentId;
                    return;
                }
                lastVesselId = v.persistentId;
            }

            BoosterRecovery.RememberPad(v);

            // ---- ⛔ HAND OVER AT COAST, AND ONLY AT COAST ----
            // The gate used to be GravityTurn || Coast. The booster separates at MECO, so by the
            // time one EXISTS the phase is BurnToApoapsis - which the gate excluded. The handover
            // could never fire.
            //
            // Coast is also the only phase where it is SAFE. We can fly exactly one vessel at a
            // time (see BoosterRecovery on the ~300 km physics clamp), so handing over during
            // BurnToApoapsis would abandon the upper stage mid-ascent and it would never reach
            // orbit. At Coast the apoapsis is already made and the stage is ballistic, so it can be
            // left alone while the booster flies its recovery.
            //
            // The cost is stated rather than hidden: the booster falls unguided for the ~30 s
            // between separation and Coast. F9I hands over immediately because its booster has its
            // own CPU; ours does not, and pretending otherwise would strand the payload.
            if (Phase == AscentPhase.Coast)
                if (BoosterRecovery.TryHandover(v)) return;

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
            if (PilotInput()) { Disengage("manual input"); return; }

            AscentInputs a = new AscentInputs();
            a.Valid = true;
            a.RadarAltitude = v.radarAltitude;
            a.Altitude = v.altitude;
            a.ApoapsisM = v.orbit.ApA;
            a.PeriapsisM = v.orbit.PeA;
            a.AtmosphereDepthM = v.mainBody.atmosphereDepth;
            a.VerticalSpeed = v.verticalSpeed;
            a.DynamicPressureKpa = v.dynamicPressurekPa;
            a.TimeToApoapsisS = v.orbit.timeToAp;
            a.AvailableThrust = AvailableThrust(v);
            a.Landed = (v.situation == Vessel.Situations.LANDED
                     || v.situation == Vessel.Situations.PRELAUNCH
                     || v.situation == Vessel.Situations.SPLASHED);
            // The two stages fly DIFFERENT laws in F9I, so the guidance has to know which it is on.
            a.SecondStage = !HasBooster(v);
            a.PhaseElapsedS = Planetarium.GetUniversalTime() - phaseStartedAt;

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
                          + " km, pe " + (a.PeriapsisM / 1000.0).ToString("F1") + " km");
            if (c.Phase != Phase) phaseStartedAt = Planetarium.GetUniversalTime();
            Phase = c.Phase;
            Command = c;

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
                Disengage(string.IsNullOrEmpty(c.Note) ? "insertion complete" : c.Note);
                return;
            }

            Steer(v, c);
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
            if (c.UllageFore > 0.01)
            {
                v.ctrlState.Z = -(float)c.UllageFore;
                if (!v.ActionGroups[KSPActionGroup.RCS])
                    v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
            }

            // ---- MECO STAGES ON COMMAND, NOT ON STARVATION ----
            // The guidance decides when the first stage is done, at its own 60 km apoapsis target,
            // while the booster still has propellant for boostback. Starvation staging is kept only
            // as the FALLBACK for an engine that quits unexpectedly.
            if (c.Stage && Planetarium.GetUniversalTime() - lastStageAt > 2.0)
            {
                lastStageAt = Planetarium.GetUniversalTime();
                blindStages = 0;
                StageManager.ActivateNextStage();
                Debug.Log(Tag + "MECO - staged on command, now stage " + StageManager.CurrentStage);
            }
            else Stage(v, c, a);
        }

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
            if (c.Phase == AscentPhase.Coast)
            {
                Vector3d pro = v.obt_velocity.normalized;
                if (pro.sqrMagnitude > 0.5) dir = pro;
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
            Vector3d upHint = (v.CoM - v.mainBody.position).normalized;
            AttitudeController.SteerTo(v, dir, upHint);
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
        private static bool PilotInput()
        {
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
        private static uint lastVesselId;
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
