// DragonScreen — AscentControl  (KSP glue: seam 2, the ascent phase controller)
// ============================================================================================
// Flies the launch → orbit phase with the PURE guidance (pure/Ascent.cs S1 pitch program + FSM,
// pure/Upfg.cs S2 closed-loop insertion, pure/LaunchAzimuth.cs plane targeting) against the live vessel.
// It reads the measured state, computes the commanded pitch/heading (S1) or thrust vector (S2), holds it
// with Steering (SAS inner loop), meters the throttle (Ascent's max-Q bucket + g-limit), stages at MECO /
// S2 ignition, cuts at SECO, separates the Dragon, then signals the conductor the phase is complete.
//
// ⛔ INSTRUMENTED: it feeds the FlightRecorder ascent columns + SelfCal (thrust from accel×mass) every
// tick, so the FIRST flight is judged from the CSV, not by eye (the standing rule). Defensive — a fault
// logs and the vehicle is left to the crew. S1 (pitch program) is the most reliable part; S2/UPFG target
// construction is a first cut to VALIDATE in flight (the Iy plane normal, the cutoff) — tune one change
// per flight against the recording.
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class AscentControl
    {
        // ⛔ the atmospheric angle-of-attack cap — never command the nose more than this off surface
        // prograde (zero-AoA gravity turn, load relief). Exceeding it RUDs the stack at max-Q.
        // AoA allowed at LOW q to establish the gravity turn (a real pitch-kick is ~8-10°); it ramps to 0
        // by QAoaZeroPa so max-Q is flown at 0 AoA. Larger = faster turn/shallower climb = reaches orbit.
        [Tunable] public static double MaxAoaDeg = 8.0;
        // ⛔ FLOOR on the AoA cap — the guidance must KEEP steering authority through the whole powered climb.
        // Flight 235215 RUD root cause: aoaCap ramped to 0 the moment q passed QAoaZeroPa (~MET 35) and stayed
        // there, so from then on the vehicle had NO authority to hold the pitch program — it handed itself to a
        // free gravity turn, fpa collapsed 80°→0°→negative at only 14.8 km, and it flew back into dense air
        // under thrust (q 273 kPa) → break-up. Never zero the cap: hold enough authority to fly the program.
        // Load-safe: the steeper program keeps real max-Q ~30-35 kPa, so 5°·35 kPa ≈ 3.0 kPa·rad is within limits.
        [Tunable] public static double MinAoaDeg = 5.0;
        // if the MEASURED AoA exceeds this in powered atmospheric flight, control is lost → abort the crew out.
        [Tunable] public static double AbortAoaDeg = 25.0;
        // ⛔ STRUCTURAL Q ABORT: dynamic pressure past this (≈1.5× the max-Q cap) means the ascent has diverged
        // — punch out WHILE STILL INTACT (the decoupler still exists → clean sep), before q RUDs the stack.
        [Tunable] public static double AbortQPa = 52000.0;
        // the AoA cap ramps from MaxAoaDeg (low q) to 0 at this dynamic pressure, so max-Q is flown at 0 AoA.
        [Tunable] public static double QAoaZeroPa = 15000.0;
        // S2 ullage: minimum post-MECO coast before S2 may light (lets S1 clear — the forward settle push
        // also opens the gap), and the settle backstop (ignite anyway if RealFuels never reports settled).
        [Tunable] public static double MinCoastS = 1.5;   // light the MVac SOON — while the prop is still settled
                                                          // from the S1 burn (a long coast lets it float off → flameout)
        [Tunable] public static double MaxUllageSettleS = 6.0;

        static AscentPhase phase = AscentPhase.Idle;
        static UpfgState upfg;
        static bool s2Ignited;
        static bool s2ThrustConfirmed;   // the MVac's REAL thrust has been measured (not just commanded)
        static double s2ThrustUpUT = -1.0;   // UT the MVac thrust first crossed the sustained threshold
        static bool s2Lighting;              // ignition cycle: false = settle@throttle-0, true = light@throttle-up
        static double s2PhaseUT = -1.0;      // UT the current settle/light phase began
        // RealFuels throttle-0-reset ignition cycle (see the S2 block):
        [Tunable] public static double S2SettleS = 2.0;       // throttle-0 settle/reset before each light attempt
        [Tunable] public static double S2LightWindowS = 2.0;  // hold throttle up this long before resetting to retry
        [Tunable] public static double S2GLimitG = 4.1;       // S2 crew axial-g cap setpoint. The "~0.4 g overshoot"
                                                              // (setpoint 4.1 → felt 4.53 g, flights 134620/144114/
                                                              // 155116) was NOT limiter lag — it was the MVac's 0.3854
                                                              // RealFuels throttle floor, unmodelled by the g-cap
                                                              // (Campaign 5 root-fix in ControlLaw.ThrottleLimit +
                                                              // MinThrottle01). With the floor mapped, this setpoint
                                                              // now yields a felt peak ≈ its own value. NOTE (fidelity,
                                                              // Chris decision): the real Crew Dragon S2 peaks ~4.5 g —
                                                              // if a post-fix flight confirms ≈4.1 g, RAISE this toward
                                                              // 4.5 (the margin the overshoot forced is no longer needed).
        [Tunable] public static double SecoVgoMps = 2.0;      // SECO cutoff when velocity-to-go drops below this (delivers
                                                              // the FULL Δv → circular target orbit, even under g-limit throttle taper)
        static double coastStartUT = -1;
        static bool dragonSeparated;
        static double secoUT = -1.0;   // SECO engine-cut time, to delay separation until thrust has died
        static SelfCalState cal;
        static AscentLoss ascentLoss;   // B9: live steering/gravity/drag Δv-loss decomposition (recorded)

        // ---- B2 q·α aero-stiffness self-cal FEED (task T4) ----
        // The SelfCal.AeroPitchStiffness estimator (kAero = M_α/I) is FED each powered tick from the isolated aero
        // pitch angular-accel = (measured pitch ω̇) − (control-commanded ω̇), regressed on the SIGNED pitch-plane
        // AoA. It RUNS + RECORDS always (cal_kaero + the raw inputs → the CSV); but the q·α cap only USES the
        // estimate when UseAeroStiffnessFeed is on AND it has converged — until then, and by default, the cap stays
        // on the tested q-seed, so the estimate cannot move the nominal flight. ⚠ SIGN-SENSITIVE + FLIGHT-GATED: the
        // sign linking ω̇, the control accel and AoA is a KSP frame convention — leave the feed OFF, read the raw
        // pairs (aero_ang_accel vs aoa_signed_deg) off a flown CSV, then set AeroFeedSign + flip the flag on. A
        // zero-AoA ascent barely excites this, so expect slow/noisy convergence (an honest observability limit).
        [Tunable] public static bool UseAeroStiffnessFeed = false;   // OFF = estimate runs+records only; cap stays on the seed
        [Tunable] public static double AeroFeedSign = 1.0;           // measurement-vs-AoA sign convention (resolve from a CSV)
        [Tunable] public static double AeroFeedMinQPa = 2000.0;      // only feed where aero is meaningful (matches the q·α gate)
        // Trust the estimate only after it has absorbed this many WELL-EXCITED samples (|AoA| above the noise floor).
        // ⚠ NOT the RLS covariance P: a zero-AoA ascent gives weak excitation, so P actually GROWS (÷λ per step) and
        // never falls — an excited-sample COUNT is the honest "has it seen enough real AoA to be believed" signal.
        [Tunable] public static int AeroFeedMinSamples = 300;
        [Tunable] public static double AeroFeedMinAoaRad = 0.002;    // ~0.11° — above this an AoA sample counts as excitation
        static double prevPitchRate, prevCtrlAngAccel, prevSignedAoaRad;
        static bool aeroPrevValid;
        static int aeroSamples;                                       // count of well-excited samples absorbed
        static double lastQAlphaCapDeg = double.NaN, lastAeroAngAccel, lastSignedAoaDeg;  // recorder readbacks

        // last commanded values, for the recorder
        static double Throttle;
        static double lastPitchCmd = 90, lastAzDeg, lastTgo, lastVgo, lastAoaDeg;
        static double lastUllage = 1.0;
        static bool lastRcsOn;
        static string lastPhaseWord = "IDLE";
        static double lastPlaneLogUT = -999;   // rate-limit the plane diagnostic
        static double lastDiffWarnUT = -999;   // rate-limit the B3 engine-out "insufficient authority" warning

        public static void Reset()
        {
            phase = AscentPhase.Idle; upfg = new UpfgState(); s2Ignited = false;
            s2ThrustConfirmed = false; s2ThrustUpUT = -1.0; s2Lighting = false; s2PhaseUT = -1.0;
            coastStartUT = -1; dragonSeparated = false; secoUT = -1.0; Throttle = 0;
            ascentLoss.Reset();
            // ⛔ fresh scene = fresh estimators (stale RLS state must not carry to a new vehicle). Clears the B2
            // aero-stiffness feed's paired-tick state too, so the first interval of a new flight isn't a bad sample.
            cal = new SelfCalState();
            prevPitchRate = 0.0; prevCtrlAngAccel = 0.0; prevSignedAoaRad = 0.0; aeroPrevValid = false;
            aeroSamples = 0;
            lastQAlphaCapDeg = double.NaN; lastAeroAngAccel = 0.0; lastSignedAoaDeg = 0.0;
            Steering.Release();
        }

        public static void Tick(Vessel v, MissionProfile mission)
        {
            if (v == null) { FlightDriver.ReleaseThrottle(); return; }
            try { Fly(v, mission); }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] ascent tick failed: " + e.Message);
                FlightDriver.ReleaseThrottle();
            }
        }

        static void Fly(Vessel v, MissionProfile mission)
        {
            CelestialBody body = v.mainBody;
            double mu = (body != null) ? body.gravParameter : 0.0;
            double R = (body != null) ? body.Radius : 0.0;

            // target orbit (ISS default ~200 km circular; a free-flyer carries its own apoapsis)
            double targetAltM = (mission.ApoKm > 0 ? mission.ApoKm : 200.0) * 1000.0;
            double targetRadiusM = R + targetAltM;

            // ---- measured vehicle numbers ----
            bool s2Lit = AnyStageEngineLit(v, false);
            double activeThrustN, ve;
            ActivePropulsion(v, out activeThrustN, out ve);
            double massKg = v.totalMass * 1000.0;
            double axialAccel = AxialAccel(v);

            AscentInputs ai = new AscentInputs();
            ai.Valid = true;
            ai.AltitudeM = v.altitude;
            ai.SurfaceSpeedMps = v.srfSpeed;
            ai.ApoapsisM = (v.orbit != null) ? v.orbit.ApA : 0.0;
            ai.TargetApoapsisM = targetAltM;
            ai.DynamicPressurePa = v.dynamicPressurekPa * 1000.0;
            ai.MassKg = massKg;
            // ⛔ g-LIMIT DENOMINATOR MUST BE 100% THRUST, not the current (already-throttled) thrust — else
            // ControlLaw's tg = gLimit·g0·m/F is circular and the cap never bites (flight 090123 hit 8.6 g at
            // SECO because activeThrustN was the throttled value). FullThrust100 recovers the full-throttle
            // thrust in the current conditions (finalThrust/currentThrottle), so the g-limit is exact.
            double fullThrustN = FullThrust100(v);
            ai.FullThrustN = fullThrustN > 1.0 ? fullThrustN : (activeThrustN > 1.0 ? activeThrustN : 1.0);
            // crew axial-g caps matching the REAL Crew Dragon ascent (researched): S1 peaks ~3.3 g just before
            // MECO, S2 climbs to ~4.5 g by SECO (astronaut accounts). The throttle bucket holds these.
            // ⚠ the SECO g "overshoot" seen repeatedly (flight 131412 → 4.77 g, and 134620/144114/155116 → 4.53 g
            // at setpoint 4.1) was NOT limiter lag — it was the MVac's 0.3854 minThrottle floor unmodelled by the
            // g-cap. Fixed below (MinThrottle01 → ControlLaw): the cap now maps to the main throttle correctly, so
            // ⛔ do NOT lower S2GLimitG to compensate — the setpoint is now the felt peak (raise it toward 4.5 for
            // fidelity if a flight confirms ≈4.1). This is the g tune knob AFTER the mapping, not a lag workaround.
            ai.GLimitG = s2Lit ? S2GLimitG : 3.5;
            // ⛔ The g-cap denominator (FullThrustN) is the ENGINE throttle=1 thrust, so the cap yields an
            // ENGINE throttle. RealFuels floors the engine at minThrottle, so the guidance's MAIN throttle
            // must be mapped through it or the felt g overshoots by (minThr + tgEng·(1−minThr))/tgEng — the
            // MVac's 0.3854 floor turned a 4.1 g setpoint into 4.53 g (flights 134620/144114/155116).
            ai.MinThrottle01 = MinThrottle01(v);
            ai.SecondStage = s2Lit;

            AscentCommand ac = Ascent.Guide(ai, phase);
            phase = ac.Phase;
            lastPhaseWord = phase.ToString();

            // ---- staging ----
            if (ac.Stage) Actuator.Meco(v);   // ⛔ direct: octaweb cutoff + interstage decoupler (no staging)

            // ⛔ S2 IGNITION — the RealFuels PROCEDURE (RF Readme_RF.txt, verified for RF 15.15 / KSP 1.12.5;
            // researched 2026-08-27 after flight 053613 re-lit the MVac 19× at throttle=1 with ullage reading
            // "stable" and got 0 thrust). An RF engine lit into momentarily-unsettled prop gets VAPOR in the
            // feed lines and flames out — and it does NOT relight by re-Activating at throttle. The DOCUMENTED
            // fix is a THROTTLE-0 RESET: (1) throttle to 0 to clear the vapor lock, (2) stabilise the prop with
            // the aft RCS (RCS/solids are NOT subject to ullage), (3) throttle UP to restart. So we CYCLE:
            // settle@throttle-0 (S2SettleS) → light@throttle-up (S2LightWindowS) → if no SUSTAINED thrust, reset
            // and retry. (The old "hold throttle up + re-Activate" never cleared the vapor lock → dead S2.)
            if ((phase == AscentPhase.Coast || phase == AscentPhase.S2Burn) && !s2ThrustConfirmed)
            {
                double now = Planetarium.GetUniversalTime();
                if (s2PhaseUT < 0.0) s2PhaseUT = now;
                Actuator.EnableRcs(v);
                FlightDriver.SetTranslation(0, 0, -1);          // ALWAYS settle: aft push seats the prop
                lastUllage = Ullage.Stability(Actuator.FindEngine(v, EngineRole.SecondStage));

                double s2ThrustN, s2MaxN; int s2LitCount;
                Actuator.EngineThrust(v, EngineRole.SecondStage, out s2ThrustN, out s2MaxN, out s2LitCount);
                if (s2ThrustN > 50000.0)                         // real MVac thrust (a transient is far less)...
                {
                    if (s2ThrustUpUT < 0.0) s2ThrustUpUT = now;
                    if (now - s2ThrustUpUT >= 0.5)              // ...held ≥0.5 s = truly running
                    {
                        s2ThrustConfirmed = true;
                        FlightDriver.ReleaseTranslation();
                        Actuator.DisableRcs(v);                 // gimbal steers the S2 — stop the non-stop Draco firing
                    }
                }
                else
                {
                    s2ThrustUpUT = -1.0;
                    double dt = now - s2PhaseUT;
                    if (!s2Lighting)
                    {
                        // SETTLE / RESET: the throttle section holds 0 here (clears the vapor lock) while the
                        // aft RCS stabilises the prop. After S2SettleS, attempt the light.
                        if (dt >= S2SettleS) { s2Lighting = true; s2PhaseUT = now; Actuator.IgniteSecondStage(v); s2Ignited = true; }
                    }
                    else
                    {
                        // LIGHT: the throttle section drives it UP; Activate again in case it fully shut. If it
                        // does not catch within the window, drop back to a throttle-0 reset and retry.
                        Actuator.IgniteSecondStage(v);
                        if (dt >= S2LightWindowS) { s2Lighting = false; s2PhaseUT = now; }
                    }
                }
            }

            // ---- steering: FLY IN THE TARGET'S ORBITAL PLANE (launch-to-rendezvous) ----
            // ⛔ (user 2026-08-27) The fixed-inclination azimuth gave the right INCLINATION but not the right
            // PLANE — launched off the ISS's node we ended up co-inclined but not co-planar, so we could never
            // rendezvous, and holding roll to the (rotating) radial-up snapped/barrel-rolled. FIX: with the ISS
            // targeted, build the aim IN the ISS orbital plane (pitch program tilted from radial toward the in-
            // plane prograde), and use the PLANE NORMAL as the roll reference — it is inertially fixed and always
            // ⊥ the nose, so the roll is stable (no snap, no barrel-roll) and the whole climb stays in the plane
            // → correct inclination AND coplanar with the ISS. No target → fall back to the inclination azimuth.
            Vector3d planeNormal;
            bool haveTargetPlane = TargetPlaneNormal(v, out planeNormal);
            Vector3d up = Steering.Up(v);
            Vector3d aim;
            if (!s2Lit)
            {
                lastPitchCmd = ac.PitchDeg;
                // ⛔ LAUNCH AZIMUTH FROM INCLINATION (flight 173320): the instantaneous "fly in the target plane"
                // aim (planeNormal×up) UNDERSHOT — it locked the orbit at 40.84° inclination, not the ISS's 51.6°,
                // because the AoA cone limiter cannot steer the nose fully into the plane. The TESTED
                // LaunchAzimuth.GroundRad from the target's inclination gives the right inclination reliably; the
                // plane normal is still the roll reference below. (Correct RAAN/coplanar needs a launch-window HOLD
                // — the pad must rotate under the ISS plane before liftoff — a separate, larger fix.)
                double incDeg = mission.IncDeg;
                if (haveTargetPlane && v.targetObject != null && v.targetObject.GetOrbit() != null)
                    incDeg = v.targetObject.GetOrbit().inclination;
                double lat = v.latitude * Math.PI / 180.0;
                double incRad = incDeg * Math.PI / 180.0;
                double bodyRot = (body != null && body.rotationPeriod > 0) ? 2.0 * Math.PI / body.rotationPeriod : 0.0;
                bool descending = incDeg > 90.0;
                double vorb = (mu > 0 && targetRadiusM > 0) ? Math.Sqrt(mu / targetRadiusM) : 7800.0;
                double azRad;
                if (!LaunchAzimuth.GroundRad(incRad, lat, vorb, R, bodyRot, descending, out azRad))
                    azRad = descending ? Math.PI : Math.PI / 2.0;
                lastAzDeg = azRad * 180.0 / Math.PI;
                Vector3d aimBase = Steering.PitchHeadingDir(v, ac.PitchDeg, azRad);

                // zero-ish-AoA gravity turn: vertical to the kick, then track the program within the AoA cap
                // (floored at MinAoaDeg so the guidance keeps authority through high q — see flight 235215).
                double qPa = v.dynamicPressurekPa * 1000.0;
                if (v.srfSpeed < Ascent.KickSpeedMps)
                {
                    aim = up;                                            // vertical rise, clear the tower
                    lastQAlphaCapDeg = double.NaN;                       // no q·α cap applied here (blank in the CSV)
                }
                else
                {
                    // ⭐ B2 q·α moderation: cap AoA at the CONTROLLABILITY region (the AoA whose aero pitching
                    // moment the live gimbal authority can still hold), not a blind q-schedule. aCtrlMax = the
                    // pitch angular authority (AttitudePilot, last tick); kAero = aero stiffness (q-seed for now —
                    // the SelfCal.AeroPitchStiffness online estimate feeds in once its aero-accel isolation is
                    // wired). FAR is statically UNSTABLE transonically → the conservative factor there. Composed
                    // with the [MinAoaDeg, MaxAoaDeg] band: ⛔ never below MinAoaDeg (flight 235215 lost steering
                    // when a cap hit 0). aCtrlMax≈0 (authority not yet read / coast) → cap floors to MinAoaDeg.
                    const double Deg2Rad = Math.PI / 180.0;
                    double aCtrlMax = AttitudePilot.PitchAccelRadS2;
                    // kAero: the tested q-seed by default; the LIVE SelfCal estimate only once the feed is enabled
                    // (T4) AND the RLS has converged (P below the gate) — otherwise the estimate never moves the cap.
                    double kAero;
                    if (UseAeroStiffnessFeed && cal.AeroStiff.Init && aeroSamples >= AeroFeedMinSamples)
                        kAero = cal.AeroStiff.Theta;                     // trusted live estimate (M_α/I)
                    else
                        kAero = QAlpha.AeroStiffnessSeed(qPa);           // seed until enough excitation / flag off
                    bool stable = v.mach < 0.8 || v.mach > 1.3;
                    double physCapRad = QAlpha.Limit(kAero, aCtrlMax, stable, qPa).AoaMaxRad;
                    double aoaCap = QAlpha.EffectiveCapRad(physCapRad, MinAoaDeg * Deg2Rad, MaxAoaDeg * Deg2Rad) / Deg2Rad;
                    lastQAlphaCapDeg = aoaCap;                           // record the cap actually applied
                    aim = Steering.LimitToProgradeCone(v, aimBase, aoaCap);
                }
            }
            else
            {
                // S2: closed-loop UPFG thrust vector (world/inertial frame). Feed the TARGET plane normal so
                // UPFG steers the insertion onto the ISS plane (corrects the S1 inclination undershoot) — NOT
                // the current plane, which only holds whatever S1 left. (flights 014906/020539/023613: S1 sets
                // inc 46.5°, S2 held it unchanged to SECO because UPFG was fed its own current h.)
                aim = UpfgAim(v, mu, targetRadiusM, activeThrustN, ve, massKg, planeNormal, haveTargetPlane);
                lastQAlphaCapDeg = double.NaN;   // no atmospheric q·α cap on the S2 vacuum ascent (blank in the CSV)
            }

            // ⛔ ROLL REFERENCE — TWO requirements, both met (perfect control includes crew orientation):
            //   (1) PLANE STABILITY: it MUST be ⊥ the nose, or LookRotation tilts the control frame, the
            //       pitch/yaw euler decomposition couples, the nose drifts off azimuth and the plane winds to
            //       RETROGRADE (flight 190114: a non-⊥ ISS-normal ref gave inc 116° instead of 51.6°).
            //   (2) CREW ORIENTATION: the crew must ride BACKS-TO-THE-SKY (belly toward Earth), pressed into
            //       their seats — NOT lying on their sides. The old `up × aim` ref is ⊥ the nose but points the
            //       dorsal axis CROSS-TRACK (sideways) — a 90° roll that puts the crew on their side.
            // FIX that satisfies both: RADIAL-OUT projected ⊥ the nose (Gram-Schmidt). It is ⊥ the nose by
            // construction (plane-stable, exactly like up×aim was) AND points the dorsal axis radial-out
            // (backs to the sky), so the crew ride correctly. As the vehicle pitches over this rotates smoothly
            // in the vertical plane (a single deliberate roll-to-belly-down = the real roll program), no snap.
            // Vertical rise (aim ∥ up → projection degenerate): hold current roll (orientation is irrelevant
            // going straight up; AttitudePilot damps the rate). ⚠ FLIGHT-VERIFY roll stays bounded + inc holds;
            // one-line revert to `Vector3d.Cross(up, aim)` if any barrel-roll returns.
            Vector3d aimN = aim.magnitude > 1e-6 ? aim.normalized : up;
            Vector3d rollRef = up - aimN * Vector3d.Dot(up, aimN);   // radial-out ⊥ the nose (Gram-Schmidt)
            rollRef = rollRef.magnitude > 1e-3 ? rollRef.normalized : Vector3d.zero;
            Steering.PointHoldRoll(v, aim, rollRef);
            lastAoaDeg = Steering.AngleOfAttackDeg(v);
            lastRcsOn = Actuator.IsRcsOn(v);

            // ⭐ B9: accumulate the ascent Δv-loss decomposition (steering/gravity/drag) while powered — the tuner
            // objective + the zero-AoA diagnostic (steer_loss should stay ~0; a growing one = the nose is off
            // prograde). Drag accel ≈ thrustAccel − felt accel (geeForce excludes gravity, so felt ≈ (F−D)/m along
            // the near-aligned axis of a zero-AoA ascent); clamped ≥0. Only integrates under thrust.
            if (activeThrustN > 1.0 && massKg > 1.0 && body != null)
            {
                double rNow = (v.CoM - body.position).magnitude;
                double gRad = (mu > 0.0 && rNow > 1.0) ? mu / (rNow * rNow) : 9.80665;
                double fpaRad = v.srfSpeed > 1.0
                    ? Math.Asin(Math.Max(-1.0, Math.Min(1.0, v.verticalSpeed / v.srfSpeed))) : Math.PI / 2.0;
                double thrustAccel = activeThrustN / massKg;
                double dragAccel = Math.Max(0.0, thrustAccel - v.geeForce * 9.80665);
                ascentLoss.Step(TimeWarp.fixedDeltaTime, gRad, fpaRad, dragAccel, thrustAccel,
                                lastAoaDeg * Math.PI / 180.0);
                // B2/T4: feed the aero pitch-stiffness estimator (runs+records; the cap uses it only when enabled).
                FeedAeroStiffness(v, TimeWarp.fixedDeltaTime, v.dynamicPressurekPa * 1000.0);
            }

            // ⛔ INSTRUMENT THE PLANE (the "wrong inc" symptom): the azimuth column can be a bad log, so also
            // print the ACHIEVED orbit inclination + the flight-path angle once/2 s. az should be ~42.8° and inc
            // should track the ISS's 51.64° once the fix works; fpa near MECO tells us if the turn still lofts.
            double nowLog = Planetarium.GetUniversalTime();
            if (nowLog - lastPlaneLogUT > 2.0 && v.orbit != null)
            {
                lastPlaneLogUT = nowLog;
                double sv = v.srfSpeed, vsp = v.verticalSpeed;
                double fpa = (sv > 1.0) ? Math.Asin(Math.Max(-1.0, Math.Min(1.0, vsp / sv))) * 180.0 / Math.PI : 90.0;
                double tgtInc = double.NaN, tgtLan = double.NaN;
                if (v.targetObject != null && v.targetObject.GetOrbit() != null)
                { tgtInc = v.targetObject.GetOrbit().inclination; tgtLan = v.targetObject.GetOrbit().LAN; }
                Debug.Log("[DragonScreen] ascent " + lastPhaseWord + ": az=" + lastAzDeg.ToString("F1")
                          + "° inc=" + v.orbit.inclination.ToString("F2") + "/" + tgtInc.ToString("F2")
                          + "° RAAN=" + v.orbit.LAN.ToString("F1") + "/" + tgtLan.ToString("F1")
                          + "° fpa=" + fpa.ToString("F0") + "° alt=" + (v.altitude / 1000.0).ToString("F0")
                          + "km srfV=" + sv.ToString("F0") + " (plane " + (haveTargetPlane ? "LOCKED" : "none") + ")");
            }

            // ⛔ LOSS-OF-CONTROL ABORT: if the vehicle departs (AoA runs away) in the powered atmospheric
            // ascent, PUNCH OUT before the stack RUDs — the crew survives on the SuperDracos + chutes. The
            // guidance commands ≤MaxAoaDeg; an AoA this far past it means control is lost, not commanded.
            if ((phase == AscentPhase.VerticalRise || phase == AscentPhase.GravityTurn)
                && v.dynamicPressurekPa > 1.0 && lastAoaDeg > AbortAoaDeg)
            {
                Debug.LogWarning("[DragonScreen] loss of control — AoA " + lastAoaDeg.ToString("F0")
                                 + "° > " + AbortAoaDeg.ToString("F0") + "° — ABORT");
                FlightDriver.RequestAbort();
            }

            // ⛔ STRUCTURAL Q ABORT: q past the safe ceiling = the ascent has diverged (this is what RUD'd the
            // stack at 247 kPa). Abort NOW, while intact and the decoupler still exists, so the capsule
            // separates cleanly — unlike a late AoA abort after the couplers already failed.
            double qAbortNow = v.dynamicPressurekPa * 1000.0;
            if ((phase == AscentPhase.VerticalRise || phase == AscentPhase.GravityTurn) && qAbortNow > AbortQPa)
            {
                Debug.LogWarning("[DragonScreen] ⛔ Q ABORT — dynamic pressure " + (qAbortNow / 1000.0).ToString("F0")
                                 + " kPa > " + (AbortQPa / 1000.0).ToString("F0") + " kPa structural limit — ABORT");
                FlightDriver.RequestAbort();
            }

            // ---- throttle ----
            if ((phase == AscentPhase.Coast || phase == AscentPhase.S2Burn) && !s2ThrustConfirmed)
                // ⛔ S2 IGNITION CYCLE owns the throttle until the MVac is truly running: 0 to RESET the vapor
                // lock (settle), 1 to LIGHT. (Holding it at 1 the whole time is what left the MVac dead.)
                Throttle = s2Lighting ? 1.0 : 0.0;
            else if (phase == AscentPhase.Done || phase == AscentPhase.Seco)
                Throttle = 0.0;
            else if (phase == AscentPhase.Coast)
                Throttle = 0.0;
            else Throttle = ac.Throttle;            // confirmed S2Burn (or S1) = the guided throttle (bucket + g-limit)

            // ---- SECO + Dragon separation ----
            // ⛔ CUT THE ENGINE, THEN SEPARATE (flight 190114): separating the Dragon while the MVac was still
            // throttled ~0.65 slammed the light capsule (g spiked to 7 g) — you must never decouple a thrusting
            // stage. Shut the MVac down first, wait until its measured thrust has actually died, THEN fire the
            // decoupler (3 s backstop so a stuck thrust reading can't hang the sequence forever).
            // ⛔ CUT ON VELOCITY-TO-GO, NOT TIME-TO-GO (flight 131412: SECO'd 200×158 km, not circular). The
            // g-limit tapers the throttle in the final seconds, so tgo (a time estimate) hits 0 while ~12.7 m/s
            // of vgo is still UNDELIVERED → the engine cut ~12.7 m/s short → pe fell 42 km below target. vgo is
            // the guidance's true "orbit achieved" signal: cutting on it delivers the full Δv regardless of the
            // throttle taper → the target (circular) orbit. inOrbit (pe reached) stays as the backstop.
            bool inOrbit = (v.orbit != null) && (v.orbit.PeA >= targetAltM - 5000.0);
            if (s2Lit && (inOrbit || (upfg.Init && lastVgo >= 0.0 && lastVgo < SecoVgoMps)))
            {
                Throttle = 0.0;
                Actuator.ShutdownEngines(v, EngineRole.SecondStage);
                if (secoUT < 0.0) secoUT = Planetarium.GetUniversalTime();
                double s2tN, s2mN; int s2lc;
                Actuator.EngineThrust(v, EngineRole.SecondStage, out s2tN, out s2mN, out s2lc);
                if (!dragonSeparated && (s2tN < 5000.0 || Planetarium.GetUniversalTime() - secoUT > 3.0))
                { Actuator.SeparateDragon(v); dragonSeparated = true; }
            }

            FlightDriver.SetThrottle(Throttle);

            // ⭐ B3 engine-out differential octaweb throttle: on a genuine engine-OUT (a vehicle with ≥2 independent
            // engine modules), trim the surviving engines so the gimbal isn't saturated fighting the missing
            // engine's steady moment. ⛔ INAPPLICABLE to OUR octaweb — it is a SINGLE multi-nozzle module, so
            // BalanceOctawebThrust holds it at full (the n<2 guard) rather than throttling the one engine down to
            // null the gimbal's own steering torque (which safe-aborted the 2026-08-28 launch at ~40% thrust).
            if (phase == AscentPhase.VerticalRise || phase == AscentPhase.GravityTurn)
            {
                bool balanced = Actuator.BalanceOctawebThrust(v, EngineRole.OctawebAll, Vec3.Zero);
                if (!balanced && Planetarium.GetUniversalTime() - lastDiffWarnUT > 2.0)
                {
                    lastDiffWarnUT = Planetarium.GetUniversalTime();
                    Debug.LogWarning("[DragonScreen] differential throttle could not null the engine-out torque — insufficient authority");
                }
            }

            if (dragonSeparated)
            {
                FlightDriver.ReleaseThrottle();
                CrewProcedureOps.PhaseComplete();   // hand back to the conductor (→ Phasing/return)
            }

            // ---- self-cal + recorder ----
            if (activeThrustN > 1.0 && massKg > 1.0) SelfCal.Thrust(ref cal, axialAccel, massKg);
            FlightLog.Fill = FillRow;
        }

        // ---- UPFG: build the target + step; return the world thrust direction ----
        static Vector3d UpfgAim(Vessel v, double mu, double targetRadiusM, double thrustN, double ve, double massKg,
                                Vector3d targetPlaneNormal, bool haveTargetPlane)
        {
            if (mu <= 0 || v.orbit == null || thrustN <= 1.0)
                return Steering.Prograde(v);

            Vec3 r = W(v.CoM - v.mainBody.position);
            Vec3 vel = W(v.obt_velocity);

            UpfgTarget t;
            // UPFG steers to the plane whose normal is t.Iy (Iy is OPPOSITE the angular momentum — the sign trap).
            // ⭐ Feed the TARGET plane normal so S2 corrects the S1 inclination shortfall; TargetPlaneNormal gives
            // the PROGRADE normal (r1×r2, +r×v sense), so Iy = -targetPlaneNormal. Only when a target plane is
            // known — else hold the CURRENT plane (Iy = -(r×v)), the safe fallback for a no-target free-flyer.
            if (haveTargetPlane && targetPlaneNormal.magnitude > 1e-6)
                t.Iy = W(-targetPlaneNormal.normalized);
            else
            {
                Vec3 h = Vec3.Cross(r, vel);
                t.Iy = h.Magnitude > 1e-3 ? (h * -1.0).Normalized : new Vec3(0, 1, 0);
            }
            t.RadiusM = targetRadiusM;
            t.SpeedMps = Math.Sqrt(mu / targetRadiusM);      // circular insertion
            t.GammaRad = 0.0;

            UpfgVehicle veh;
            veh.ExhaustVel = ve > 100 ? ve : 3383.0;
            veh.ThrustN = thrustN;
            veh.MassKg = massKg;

            if (!upfg.Init) upfg = Upfg.Init(r, vel, mu, t, veh);
            UpfgGuidance g = Upfg.Step(r, vel, mu, t, veh, ref upfg);
            lastTgo = g.TgoS; lastVgo = upfg.Vgo.Magnitude;
            if (g.Valid) return new Vector3d(g.IF.X, g.IF.Y, g.IF.Z).normalized;
            return Steering.Prograde(v);                     // fallback: prograde raise
        }

        // ---- actuation helpers ---- (staging + separation now live in Actuator: Meco / SeparateDragon)

        static bool AnyStageEngineLit(Vessel v, bool booster)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                bool match = booster ? VehicleParts.IsBooster(p.name) : VehicleParts.IsSecondStage(p.name);
                if (!match) continue;
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e != null && e.EngineIgnited && e.finalThrust > 0.1f) return true;
                }
            }
            return false;
        }

        // sum of currently-lit engines' thrust (N) + a representative exhaust velocity (m/s)
        static void ActivePropulsion(Vessel v, out double thrustN, out double ve)
        {
            thrustN = 0.0; double ispSum = 0.0, wSum = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null || !e.EngineIgnited || !e.isOperational) continue;
                    double thr = e.finalThrust > 0.1f ? e.finalThrust * 1000.0 : e.maxThrust * 1000.0;
                    thrustN += thr;
                    double isp = e.realIsp > 1f ? e.realIsp : 340.0;
                    ispSum += isp * thr; wSum += thr;
                }
            }
            ve = (wSum > 0) ? (ispSum / wSum) * Upfg.G0 : 3383.0;
        }

        // The FULL-THROTTLE thrust (N) of the currently-lit engines in the CURRENT conditions — the correct
        // denominator for the crew g-limit (ControlLaw.ThrottleLimit). finalThrust/currentThrottle recovers the
        // 100% thrust even while the engine is throttled (accounting for atmosphere via the live finalThrust);
        // falls back to the config maxThrust if the throttle is ~0.
        static double FullThrust100(Vessel v)
        {
            double sum = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null || !e.EngineIgnited || !e.isOperational) continue;
                    // ⛔ MEASURED full-throttle thrust = finalThrust / currentThrottle, NOT e.maxThrust. finalThrust
                    // and currentThrottle are the SAME instant's values, so their ratio is the thrust at throttle
                    // 1.0 in the CURRENT condition — self-consistent, no lag error. Data (flight 201648): at
                    // throttle 0.727 the MVac made 775967 N → full = 1067 kN, but e.maxThrust reads only 934 kN
                    // (14% low, the vacuum thrust exceeds the config max), so maxThrust made the g-limit under-
                    // throttle and g overshot to 5.14 g. The measured ratio holds the target g correctly.
                    double ct = e.currentThrottle;
                    sum += (ct > 0.05 && e.finalThrust > 0.1f) ? (e.finalThrust / ct) * 1000.0 : e.maxThrust * 1000.0;
                }
            }
            return sum;
        }

        // The lit stage's RealFuels throttle FLOOR: the engine runs at minThrottle + mainThrottle·(1−minThrottle),
        // so a MAIN throttle of 0 still delivers minThrottle·F_full. The g-cap needs it to map its engine-throttle
        // target back to a main throttle (ControlLaw.ThrottleLimit). Read from the engine's KSPField (RO/RealFuels
        // is always present under RO); 0 when absent (stock) so the cap keeps its old linear behaviour. The lit
        // stage's engines are identical, so the first operational one is representative.
        static double MinThrottle01(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null || !e.EngineIgnited || !e.isOperational) continue;
                    try
                    {
                        BaseField bf = e.Fields["_minThrottle"];
                        if (bf != null)
                        {
                            object o = bf.GetValue(e);
                            if (o != null)
                            {
                                double mt = Convert.ToDouble(o);
                                if (mt > 0.0 && mt < 1.0) return mt;
                            }
                        }
                    }
                    catch { }
                    return 0.0;   // an operational engine with no readable floor → keep stock (linear) behaviour
                }
            }
            return 0.0;
        }

        // Felt (accelerometer) axial acceleration — INDEPENDENT of the thrust model, so SelfCal.Thrust
        // (F = a·m) is a genuine cross-check, not a tautology. geeForce excludes gravity (freefall = 0),
        // so under power it is ≈ thrust/mass along the axis.
        static double AxialAccel(Vessel v) { return v.geeForce * 9.80665; }

        static Vec3 W(Vector3d v) { return new Vec3(v.x, v.y, v.z); }

        // The targeted ISS's orbital-plane normal (world, prograde sense = r × v) so the ascent can fly IN that
        // plane (launch-to-rendezvous) and use it as a stable roll reference.
        //
        // ⛔ (user 2026-08-27, flight 090123) The old version used tgt.GetTransform().position × GetObtVelocity().
        // For an UNLOADED target — and the ISS is always unloaded on the pad, hundreds of km away — the transform
        // position is a placeholder, NOT the real orbital position, so the normal came out ~90° wrong and the
        // ascent flew azimuth 307.8° (a retrograde plane) instead of ~42.8°. FIX: derive the normal ONLY from
        // the ORBIT, sampling two world positions a couple of minutes apart (getPositionAtUT returns the world
        // frame, so no internal-frame swizzle to get wrong). r1 × r2 is the prograde normal, unambiguously.
        static bool TargetPlaneNormal(Vessel v, out Vector3d n)
        {
            n = Vector3d.zero;
            try
            {
                ITargetable tgt = v.targetObject;
                Orbit o = (tgt != null) ? tgt.GetOrbit() : null;
                if (o == null || v.mainBody == null) return false;
                double now = Planetarium.GetUniversalTime();
                Vector3d bpos = v.mainBody.position;
                Vector3d r1 = o.getPositionAtUT(now) - bpos;
                Vector3d r2 = o.getPositionAtUT(now + 120.0) - bpos;   // ~2 min downstream (prograde)
                Vector3d h = Vector3d.Cross(r1, r2);                    // ∝ +orbit normal (prograde)
                if (h.magnitude < 1.0) return false;
                n = h.normalized;
                return true;
            }
            catch { return false; }
        }

        // Compass heading (deg CW from north) of a world direction — informational, for the azimuth column.
        static double HeadingDeg(Vessel v, Vector3d dir)
        {
            try
            {
                Vector3d up = v.upAxis;
                Vector3d north = (Vector3d)v.mainBody.transform.up;              // body spin axis ≈ north
                Vector3d east = Vector3d.Cross(north, up).normalized;
                Vector3d locN = Vector3d.Cross(up, east).normalized;            // local horizontal north
                Vector3d horiz = dir - up * Vector3d.Dot(dir, up);
                if (horiz.magnitude < 1e-6) return lastAzDeg;
                double deg = Math.Atan2(Vector3d.Dot(horiz, east), Vector3d.Dot(horiz, locN)) * 180.0 / Math.PI;
                return deg < 0 ? deg + 360.0 : deg;
            }
            catch { return lastAzDeg; }
        }

        // ---- recorder contribution (invoked by FlightLog while a row is built) ----
        static void FillRow(string[] row)
        {
            // att_err_deg (real AoA), throttle and the attitude-loop internals now come from the ALWAYS-ON
            // command snapshot (FlightLog.PutCommand), so every phase records them — not just ascent. Here we
            // add only the ascent-UNIQUE columns.
            FlightRecorder.PutAscent(row, lastTgo, lastVgo, lastPitchCmd, lastAzDeg, lastPhaseWord);
            FlightRecorder.PutIgnition(row, lastUllage, FlightDriver.ClampThrustFrac, FlightDriver.ClampHeld);
            FlightRecorder.PutSelfCal(row, cal);
            FlightRecorder.PutAscentLoss(row, ascentLoss);
            FlightRecorder.PutQAlpha(row, lastQAlphaCapDeg, lastAeroAngAccel, lastSignedAoaDeg);   // B2/T4
        }

        // Signed pitch-plane AoA (rad): magnitude = angle(nose, surface-velocity); sign from which side of the
        // nose the velocity lies about the body pitch axis (rt.right). The exact ± convention relative to the
        // pitch angular-rate is a KSP frame detail resolved from a flown CSV (AeroFeedSign) — here we only need a
        // CONSISTENT signed regressor. Zero when there is no meaningful surface velocity.
        static double SignedAoaRad(Vessel v)
        {
            Transform rt = v.ReferenceTransform;
            if (rt == null || v.srf_velocity.magnitude < 1.0) return 0.0;
            Vector3d nose = rt.up;
            Vector3d vel = ((Vector3d)v.srf_velocity).normalized;
            double mag = Vector3d.Angle(nose, vel) * Math.PI / 180.0;
            double s = Vector3d.Dot(Vector3d.Cross(nose, vel), (Vector3d)rt.right);   // ± about the body pitch axis
            return mag * (s >= 0.0 ? 1.0 : -1.0);
        }

        // B2/T4: feed the aero pitch-stiffness estimator from the ISOLATED aero angular-accel, and stash the raw
        // inputs for the recorder. Pairs THIS interval's realized pitch angular-accel with the control accel + AoA
        // that acted DURING it (prev* from last tick) — the correct causal pairing. Only feeds where aero is
        // meaningful (the q gate) so coast / vertical-rise noise can't pollute the estimate; SelfCal itself skips
        // |AoA|≈0. Runs whether or not the cap uses the result (UseAeroStiffnessFeed) — so we always collect data.
        static void FeedAeroStiffness(Vessel v, double dt, double qPa)
        {
            double pitchRate = v.angularVelocity.x;                                       // body pitch rate (axis 0)
            double ctrlAngAccel = AttitudePilot.ActPitch * AttitudePilot.PitchAccelRadS2; // control-produced pitch ω̇
            double signedAoa = SignedAoaRad(v);

            if (aeroPrevValid && dt > 1e-4 && qPa >= AeroFeedMinQPa)
            {
                double measAngAccel = (pitchRate - prevPitchRate) / dt;                   // realized over the last interval
                double aeroAngAccel = AeroFeedSign * (measAngAccel - prevCtrlAngAccel);   // isolate the aero moment
                SelfCal.AeroPitchStiffness(ref cal, aeroAngAccel, prevSignedAoaRad);
                lastAeroAngAccel = aeroAngAccel;
                lastSignedAoaDeg = prevSignedAoaRad * 180.0 / Math.PI;
                if (Math.Abs(prevSignedAoaRad) > AeroFeedMinAoaRad && aeroSamples < int.MaxValue)
                    aeroSamples++;                                                        // count only excited samples
            }
            prevPitchRate = pitchRate; prevCtrlAngAccel = ctrlAngAccel; prevSignedAoaRad = signedAoa;
            aeroPrevValid = true;
        }
    }
}
