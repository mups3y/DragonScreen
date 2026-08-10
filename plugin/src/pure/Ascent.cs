/*
 * DragonScreen - Ascent
 *
 * PURE. Ascent guidance: given where the vehicle is, where should it point and how hard should it
 * burn. No KSP, no Unity, so the whole profile can be flown headless before it is flown for real.
 *
 * ---- THIS IS A GRAVITY TURN, NOT THE OPTIMAL-CONTROL ASCENT IN THE PLAN ----
 * `docs/FLIGHT_SOFTWARE_PLAN.md` step 5 is MechJebLib's PSG - a primer-vector optimal-control
 * solver, the same family real launch vehicles use. That needs the numerical core ported first
 * (steps 1-2) and it is a solver, not a formula: ported carelessly it fails to converge with no
 * obvious reason why.
 *
 * This is the honest interim: a closed-loop gravity turn with dynamic-pressure limiting and an
 * apoapsis cutoff. It is what it says it is. It will not fly a fuel-optimal trajectory, and when PSG
 * lands, only Guide() changes - everything around it stays.
 *
 * ---- WHY A GRAVITY TURN IS THE RIGHT INTERIM, NOT A PITCH TABLE ----
 * A pitch table is open loop: it flies the same profile whatever the vehicle does, so a heavy load
 * or a lost engine just makes it wrong. This reads ALTITUDE and closes the loop through it, and cuts
 * on APOAPSIS rather than on a clock. That is the difference between guidance and a recording.
 *
 * ---- THE NUMBERS, AND WHERE THEY COME FROM ----
 * Scaled off the atmosphere depth rather than hard-coded to Kerbin, so RSS/RO does not silently get
 * a 70 km profile. The turn ends at 62% of the atmosphere because that is where a Falcon-shaped
 * vehicle is through the thick of it; the Q limit is set below the level at which our own recorded
 * flights showed control trouble.
 */
namespace DragonScreen
{
    public enum AscentPhase : byte
    {
        Idle = 0,
        /// <summary>Straight up, clear of the pad.</summary>
        VerticalRise,
        /// <summary>Pitching over and following the turn.</summary>
        GravityTurn,
        /// <summary>Engines off, holding attitude, about to separate. A DISCRETE STEP.</summary>
        Meco,
        /// <summary>
        /// Separated, still coasting, MVac NOT YET LIT. The gap that keeps the booster alive.
        /// </summary>
        StageSep,
        /// <summary>Second stage raising apoapsis to the real orbit.</summary>
        BurnToApoapsis,
        /// <summary>Apoapsis made, engines off, waiting to reach it.</summary>
        Coast,
        /// <summary>Burning prograde at apoapsis to raise periapsis.</summary>
        Circularise,
        Done
    }

    public struct AscentTarget
    {
        /// <summary>Target apoapsis AND the circular altitude, metres above sea level.</summary>
        public double AltitudeM;
        /// <summary>Launch azimuth, degrees. 90 is due east - the cheapest direction.</summary>
        public double HeadingDeg;

        // ---- F9I'S ASCENT CONSTANTS. FLOWN, NOT CHOSEN HERE. ----
        // `SPACEX/PARAM.ks` RTLSmode(). Three landing profiles exist and they differ in exactly
        // these numbers, which is the tell that they were tuned against flights rather than derived:
        //
        //      RTLS      MECOangle 45   tgtAlt 60 km   pitchGain 110
        //      ASDS      MECOangle 40   tgtAlt 70 km   pitchGain  97
        //      expend    MECOangle 10   tgtAlt 70 km   pitchGain  72.5
        //
        // RTLS is ours: the booster comes home, so it separates steeper and earlier.
        /// <summary>Pitch the first stage holds from the floor to MECO, degrees.</summary>
        public double MecoAngleDeg;
        /// <summary>Shapes how fast the first stage pitches over. Per cent.</summary>
        public double PitchGain;
        /// <summary>First-stage reference altitude, metres.</summary>
        public double StageAltM;

        /// <summary>
        /// Space X Station sits at 86.8 x 85.8 km, inclination 0.133 - MEASURED over four flights,
        /// not a round number. An 86 km circular orbit due east is the ferry mission's insertion.
        /// </summary>
        public static AscentTarget Station()
        {
            AscentTarget t = new AscentTarget();
            t.AltitudeM = 86000.0;
            t.HeadingDeg = 90.0;
            t.MecoAngleDeg = 45.0;      // RTLSmode()
            t.PitchGain = 110.0;
            t.StageAltM = 60000.0;
            return t;
        }
    }

    public struct AscentInputs
    {
        public bool Valid;
        public double RadarAltitude;
        public double Altitude;
        public double ApoapsisM, PeriapsisM;
        public double AtmosphereDepthM;
        public double VerticalSpeed;
        public double DynamicPressureKpa;
        public double TimeToApoapsisS;
        /// <summary>Thrust the vehicle could make right now, kN. Zero means nothing is lit.</summary>
        public double AvailableThrust;
        public bool Landed;
        /// <summary>True once the booster is gone - the two stages fly different laws.</summary>
        public bool SecondStage;
        /// <summary>Seconds since the current phase began. MECO and the ullage burn are timed.</summary>
        public double PhaseElapsedS;

        // ---- CIRCULARISATION, F9I-STYLE ----
        // "Make the orbit circular HERE, NOW" - FalconCircBurnVecNow, F9_payload.ks:265. The GLUE
        // computes the vector because it needs the state vectors; pure decides what to do with it.
        /// <summary>Magnitude of the dv that would circularise at the CURRENT radius, m/s.</summary>
        public double CircDvMps;
        /// <summary>True once that dv has REVERSED against its value at burn start - overshoot.</summary>
        public bool CircDvFlipped;
    }

    public struct AscentCommand
    {
        public AscentPhase Phase;
        /// <summary>Degrees above the horizon. 90 is straight up.</summary>
        public double PitchDeg;
        public double HeadingDeg;
        public double Throttle;
        /// <summary>RCS fore command for the ullage settle, 0..1.</summary>
        public double UllageFore;
        /// <summary>Separate the spent stage now.</summary>
        public bool Stage;
        /// <summary>Drop the S2 off the Dragon. A DIFFERENT decoupler - see VehicleParts.</summary>
        public bool SeparateS2;
        /// <summary>RCS should be on. The unpowered coasts have no gimbal and need it.</summary>
        public bool Rcs;
        public string Note;
    }

    public static class Ascent
    {
        /// <summary>Straight up to here, so the vehicle is clear before it leans. Metres, radar.</summary>
        public const double VerticalRiseM = 250.0;

        /// <summary>Turn completes at this fraction of the atmosphere - scales to any body.</summary>
        public const double TurnEndFraction = 0.62;

        /// <summary>
        /// Dynamic pressure ceiling, kPa. Falcon 9 throttles down through max Q and so do we; this
        /// sits below where our own recorded flights showed the stack getting unhappy.
        /// </summary>
        public const double MaxQKpa = 20.0;

        /// <summary>Stop circularising once periapsis is within this of the target.</summary>
        public const double PeriapsisToleranceM = 2000.0;

        /// <summary>Start the circularisation burn this long before apoapsis.</summary>
        public const double CircBurnLeadS = 12.0;

        // ---- ⛔ MECO IS A DISCRETE STEP AT ITS OWN, LOWER, APOAPSIS TARGET ----
        // The thing I had completely wrong until the port map forced an end-to-end read. F9I carries
        // TWO altitude targets and they are not the same number:
        //
        //      tgtAlt    60 km   `Ascent()` exits when apoapsis passes THIS - it is the MECO target
        //      tgtOrbPE  86 km   the final orbit, which the SECOND stage reaches
        //
        // Ours flew the first stage at the 86 km figure, so the booster burned to depletion and the
        // recovery inherited a dry stage with nothing left for boostback. The whole point of MECO at
        // 60 km is that the booster still HAS propellant when it separates.
        //
        // `MECO()` at F9_payload.ks:543 is then a real sequence, not a consequence of running out:
        // throttle to zero, hold the separation attitude, wait 2.5 s, stage, wait 3 s.

        /// <summary>Hold after the engines cut before separating. F9I waits 2.5 s.</summary>
        public const double MecoHoldS = 2.5;

        /// <summary>
        /// Ullage: RCS fore 0.75 with the engine at 0.075 for six seconds, settling the propellant
        /// before the second stage is asked for real thrust. `BurnToApoapsis`, F9_payload.ks:571.
        /// </summary>
        public const double UllageSeconds = 6.0;

        /// <summary>
        /// Coast between SEPARATION and MVac ignition, seconds. F9I's MECO(): `wait 2.5. SafeStage().
        /// core:part:controlfrom(). wait 3.` - the second wait is this one, and it is the only thing
        /// standing between the MVac plume and the booster we are trying to recover.
        /// `falcon-open-issues` lists "booster killed by MVac exhaust on sep" as its number one.
        ///
        /// We had NO gap: the stage command and the transition into BurnToApoapsis fired on the same
        /// tick, so the MVac lit at 7% ullage throttle with the booster still at zero range.
        /// </summary>
        public const double PostSepHoldS = 3.0;

        /// <summary>
        /// Periapsis at which the S2 is dropped, metres. F9I `sepPeTarget`, and the reasoning is in
        /// FalconSepS2: 64.8 km is "technically" inside the 70 km atmosphere but sits where there is
        /// no meaningful air, so the stage stays up more or less indefinitely. 40 km is deep enough
        /// that "inside the atmosphere" and "comes down" are the same claim.
        ///
        /// MEASURED, bb_upper_CrewDragon_069: "S2 SEP: separating at 85.4 x 40.2 km", Dracos lit one
        /// second later, orbit closed 7 s after that.
        /// </summary>
        public const double SepPeTargetM = 40000.0;
        public const double UllageThrottle = 0.075;
        public const double UllageFore = 0.75;

        // ---- CIRCULARISATION: THE BURN THAT PUT US ON AN ESCAPE TRAJECTORY ----
        // The first version burned PROGRADE at full throttle until periapsis reached the target
        // altitude. That has NO FIXED POINT: burning prograde raises apoapsis faster than periapsis,
        // so the cutoff recedes as you chase it. Flight 13:36 ran the second stage dry doing exactly
        // that and left Kerbin altogether.
        //
        // F9I solved this and wrote down why (`F9_payload.ks:265`, FalconCircBurnVecNow):
        //
        //      "Make the orbit circular HERE, NOW" ... This has a true fixed point (dv -> 0 exactly
        //      when the orbit is circular at the current radius), so the closed loop converges by
        //      construction instead of chasing a target that has expired.
        //
        //      dv = (horizontal_unit * sqrt(mu / r)) - v
        //
        // The glue computes that vector; this file only has to decide throttle and when to stop.
        // Because dv shrinks to zero as the orbit rounds out, throttling on |dv| cannot run away.

        /// <summary>Circular within this and the burn is finished, m/s.</summary>
        public const double CircDvToleranceMps = 0.5;

        /// <summary>|dv| that commands full throttle. Below it the burn eases in proportion.</summary>
        public const double CircDvFullMps = 25.0;

        /// <summary>Floor, so the last fraction of a m/s still closes instead of stalling.</summary>
        public const double CircThrottleMin = 0.05;

        /// <summary>
        /// ⛔ RUNAWAY BACKSTOP. If apoapsis ever exceeds the target by this factor the burn is
        /// wrong, whatever the guidance believes, and it stops. This is a SECOND line of defence
        /// behind the fixed point above - the escape trajectory is the one failure on this project
        /// that loses the whole vehicle and the crew with it, so it gets a guard that does not
        /// depend on the guidance being correct.
        /// </summary>
        public const double ApoapsisRunawayFactor = 1.5;

        public static AscentCommand Guide(AscentInputs s, AscentTarget t, AscentPhase phase)
        {
            AscentCommand c = new AscentCommand();
            c.HeadingDeg = t.HeadingDeg;
            c.Phase = phase;

            if (!s.Valid) { c.Phase = AscentPhase.Idle; c.Note = "no vessel"; return c; }

            bool stageNow = false;
            bool sepS2Now = false;

            // ---- ⛔ EXACTLY ONE TRANSITION PER CALL. THIS `else` CHAIN IS LOAD-BEARING. ----
            // Flight 16:58 was a chain of plain `if`s, and it cost the entire mission. On the tick
            // apoapsis crossed 60 km, the first test set phase = Meco - and then the NEXT test read
            // `PhaseElapsedS`, which was still the eighty-odd seconds spent in GRAVITY TURN, decided
            // the 2.5 s MECO hold was long over, and advanced straight to BurnToApoapsis in the same
            // call. MECO never happened. It was never logged, never held, and never staged.
            //
            // Everything else that went wrong that flight followed from those two lines:
            //   no stage command  -> the booster stayed attached
            //   booster attached  -> SecondStage stayed false
            //   SecondStage false -> the FIRST-stage pitch law ran the whole flight, floored at 45
            //   45 degrees        -> the burn raised apoapsis and never periapsis (60 -> 129 km,
            //                        periapsis -598.4 -> -597.7, i.e. it never left the lob)
            //
            // A state machine must advance one step per tick, or a per-phase timer is meaningless
            // because the phase it is timing may already have been left.
            if (phase == AscentPhase.Idle || phase == AscentPhase.VerticalRise)
            {
                phase = (s.RadarAltitude < VerticalRiseM && !Above(s, t))
                      ? AscentPhase.VerticalRise : AscentPhase.GravityTurn;
            }
            // FIRST STAGE ends at the MECO target, not at the orbit target.
            else if (phase == AscentPhase.GravityTurn && s.ApoapsisM >= StageTarget(t))
                phase = AscentPhase.Meco;

            // MECO holds, then separates. The command below raises Stage on the tick it expires.
            else if (phase == AscentPhase.Meco && s.PhaseElapsedS >= MecoHoldS)
            {
                // ⛔ THE STAGE COMMAND BELONGS ON THE TRANSITION, NOT IN THE Meco CASE.
                // It was  inside  - which can
                // never be true, because on the tick the hold expires THIS branch fires first and
                // the switch renders BurnToApoapsis instead. The booster would have stayed attached
                // for a second flight running, with the  fix in place and looking correct.
                // ---- ⛔ AND IT GOES TO StageSep, NOT STRAIGHT TO THE MVac. ----
                // Separating and lighting the second stage on the same tick is what puts the MVac
                // plume into the booster. PostSepHoldS is the gap; see the constant.
                phase = AscentPhase.StageSep;
                stageNow = true;
            }

            else if (phase == AscentPhase.StageSep && s.PhaseElapsedS >= PostSepHoldS)
                phase = AscentPhase.BurnToApoapsis;

            // ---- SECOND STAGE RAISES APOAPSIS, THEN THE S2 IS DROPPED ----
            // Exit on EITHER the apoapsis target or a 40 km periapsis, whichever comes first, and
            // shed the S2 on the way past. F9I calls FalconSepS2 at exactly this point so the S2
            // leaves on a trajectory that re-enters, and the Dragon closes the orbit on its own
            // SuperDracos - 228 kN on the pod, ~400 m/s in the tank, ~37 m/s needed.
            //
            // Without this the capsule circularises with the whole S2 still bolted to it and then
            // tries to de-orbit and land as a 20.5 t stack. It does not work, and the 21:01 flight
            // ended exactly that way.
            else if (phase == AscentPhase.BurnToApoapsis
                     && (Above(s, t) || s.PeriapsisM >= SepPeTargetM))
            {
                phase = AscentPhase.Coast;
                sepS2Now = true;
            }

            else if (phase == AscentPhase.Coast
                     && (s.TimeToApoapsisS <= CircBurnLeadS || s.PeriapsisM > t.AltitudeM * 0.5))
            {
                // Lead the burn so it straddles apoapsis instead of starting at it - burning entirely
                // after apoapsis raises the wrong side of the orbit.
                phase = AscentPhase.Circularise;
            }
            else if (phase == AscentPhase.Circularise && Circularised(s, t))
                phase = AscentPhase.Done;

            // ---- ⛔ AND A DIVERGING BURN IS NOT A FINISHED ONE ----
            // Same flight: circDv ROSE from 2098 to 2174 m/s while the burn ran, the dv direction
            // swung more than 90 degrees off where it started, the overshoot test read that as
            // "we have burned past it", and the autopilot announced INSERTION COMPLETE on a
            // trajectory with a periapsis of MINUS 598 km. A false success is worse than a failure:
            // it disengages and hands back a vehicle nobody is flying.
            if (phase == AscentPhase.Circularise && s.CircDvFlipped
                && s.CircDvMps > CircDvDivergedMps)
            {
                c.Phase = AscentPhase.Done;
                c.Throttle = 0.0;
                c.Note = "ABORT - CIRCULARISATION DIVERGING";
                return c;
            }

            // ⛔ THE BACKSTOP, CHECKED IN EVERY PHASE AND NOT JUST THE BURN.
            if (s.ApoapsisM > t.AltitudeM * ApoapsisRunawayFactor)
            {
                c.Phase = AscentPhase.Done;
                c.Throttle = 0.0;
                c.PitchDeg = 0.0;
                c.Note = "ABORT - APOAPSIS RUNAWAY";
                return c;
            }

            c.Phase = phase;

            switch (phase)
            {
                case AscentPhase.VerticalRise:
                    c.PitchDeg = 90.0;
                    c.Throttle = 1.0;
                    c.Note = "VERTICAL RISE";
                    break;

                case AscentPhase.GravityTurn:
                    c.PitchDeg = TurnPitch(s, t);
                    c.Throttle = QThrottle(s);
                    c.Note = "GRAVITY TURN";
                    break;

                case AscentPhase.Meco:
                    // Engines off, hold the separation attitude. BOOSTER.ks separates on
                    // `heading(azimuth, MECOangle)` and depends on the stack holding exactly this.
                    c.PitchDeg = (t.MecoAngleDeg > 0.0) ? t.MecoAngleDeg : 45.0;
                    c.Throttle = 0.0;
                    // F9I's MECO() does `rcs on` before the hold, and the recording shows why: with
                    // the engines out the gimbal goes with them, torque falls from 2300 kN.m to the
                    // 9.5 of the reaction wheels alone, and the pitch axis saturates at +-1 for the
                    // whole coast. RCS is the only authority the stack has here.
                    c.Rcs = true;
                    c.Note = "MECO";
                    break;

                case AscentPhase.StageSep:
                    // Separated and drifting apart. Engines STAY OFF - this hold exists so the MVac
                    // does not light into the booster.
                    c.PitchDeg = (t.MecoAngleDeg > 0.0) ? t.MecoAngleDeg : 45.0;
                    c.Throttle = 0.0;
                    c.Rcs = true;
                    c.Note = "STAGE SEP";
                    break;

                case AscentPhase.BurnToApoapsis:
                    c.PitchDeg = TurnPitch(s, t);
                    if (s.PhaseElapsedS < UllageSeconds)
                    {
                        // Settle the propellant before asking for thrust.
                        c.Throttle = UllageThrottle;
                        c.UllageFore = UllageFore;
                        c.Note = "ULLAGE";
                    }
                    else
                    {
                        c.Throttle = ApoapsisThrottle(s, t);
                        c.Note = "BURN TO APOAPSIS";
                    }
                    break;

                case AscentPhase.Coast:
                    // Hold the prograde-ish attitude rather than flopping to the horizon: the vehicle
                    // is still in thin atmosphere and a sideways capsule bleeds apoapsis.
                    c.PitchDeg = 0.0;
                    c.Throttle = 0.0;
                    c.Note = "COAST TO APOAPSIS";
                    break;

                case AscentPhase.Circularise:
                    // Variable, on the remaining dv. Full throttle at 25 m/s to go, easing to the
                    // floor as it converges - a full-throttle finish overshoots between ticks and
                    // there is no way to take it back.
                    c.PitchDeg = 0.0;
                    c.Throttle = CircThrottle(s.CircDvMps);
                    c.Note = "CIRCULARISE";
                    break;

                default:
                    c.PitchDeg = 0.0;
                    c.Throttle = 0.0;
                    c.Note = (phase == AscentPhase.Done) ? "INSERTION COMPLETE" : "IDLE";
                    break;
            }

            c.Stage = stageNow;
            c.SeparateS2 = sepS2Now;
            return c;
        }

        /// <summary>The MECO apoapsis - the FIRST stage's target, well below the orbit.</summary>
        public static double StageTarget(AscentTarget t)
        {
            return (t.StageAltM > 0.0) ? t.StageAltM : 60000.0;
        }

        /// <summary>
        /// Second-stage throttle, `BurnToApoapsis`:
        ///
        ///     min( max(0.1, (target - apoapsis) / (target - atmHeight))
        ///          + max(0, (30 - etaApoapsis) * 0.075), 1 )
        ///
        /// Proportional to the apoapsis DEFICIT, normalised by how much of the climb is above the
        /// atmosphere, floored at 0.1 so it always closes, plus the same avoidFireDeath term that
        /// pitches up when apoapsis is about to arrive. Variable, not full throttle.
        /// </summary>
        public static double ApoapsisThrottle(AscentInputs s, AscentTarget t)
        {
            double span = t.AltitudeM - s.AtmosphereDepthM;
            if (span < 1000.0) span = 1000.0;
            double deficit = (t.AltitudeM - s.ApoapsisM) / span;
            if (deficit < 0.1) deficit = 0.1;

            double fire = (30.0 - s.TimeToApoapsisS) * 0.075;
            if (fire < 0.0) fire = 0.0;

            double th = deficit + fire;
            if (th > 1.0) th = 1.0;
            return th;
        }

        /// <summary>
        /// A reversed dv this large means the burn is DIVERGING, not overshooting. Above it the
        /// autopilot aborts and says so instead of claiming success.
        /// </summary>
        public const double CircDvDivergedMps = 5.0;

        /// <summary>
        /// ⛔ IS IT ACTUALLY IN ORBIT? Not "did the guidance run out of things to do".
        ///
        /// Flight 16:58 declared INSERTION COMPLETE with apoapsis 129 km and periapsis MINUS 598 km.
        /// Every individual condition that fired was defensible; none of them asked the only
        /// question that matters. Periapsis must clear the atmosphere, or it is not an orbit and
        /// saying so is a lie the crew cannot check.
        /// </summary>
        public static bool Circularised(AscentInputs s, AscentTarget t)
        {
            if (s.PeriapsisM <= s.AtmosphereDepthM) return false;
            if (s.CircDvMps <= CircDvToleranceMps) return true;
            return s.PeriapsisM >= t.AltitudeM - PeriapsisToleranceM;
        }

        /// <summary>Throttle for the circularisation burn, from the dv still to go.</summary>
        public static double CircThrottle(double dvMps)
        {
            if (dvMps <= CircDvToleranceMps) return 0.0;
            double t = dvMps / CircDvFullMps;
            if (t > 1.0) t = 1.0;
            if (t < CircThrottleMin) t = CircThrottleMin;
            return t;
        }

        private static bool Above(AscentInputs s, AscentTarget t)
        {
            return s.ApoapsisM >= t.AltitudeM;
        }

        /// <summary>
        /// The turn itself. Square root of the fraction of the way through, which spends more of the
        /// turn near vertical early - the shape a rocket wants, because pitching hard low down is
        /// where the drag and the bending loads are.
        /// </summary>
        public static double TurnPitch(AscentInputs s) { return TurnPitch(s, AscentTarget.Station()); }

        /// <summary>
        /// ---- F9I'S ASCENT LAW, PORTED. NOT THE sqrt CURVE I INVENTED FIRST. ----
        /// `F9I/F9_payload.ks:527-530` for the first stage and `:622-624` for the second:
        ///
        ///     stage 1   tgtPitch = max( 90 * (1 - alt / (tgtAlt * pitchGain/100)), MECOangle )
        ///     stage 2   tgtPitch = min( max(90 * (1 - alt / tanAlt), 0.1) + avoidFireDeath, MECOangle )
        ///
        /// LINEAR in altitude with a floor at the MECO angle, not a square root. The floor is the
        /// important part and the sqrt version had nothing like it: the first stage pitches over to
        /// 45 degrees and then HOLDS there to staging, which is what sets up the booster's return -
        /// BOOSTER.ks separates on `heading(azimuth, MECOangle)` and depends on the stack having
        /// been holding exactly that.
        ///
        /// `avoidFireDeath` is F9I's guard for the second stage: inside 30 s to apoapsis it adds up
        /// to about 11 degrees of pitch-up to stop the apoapsis running away underneath the burn.
        ///
        /// These constants are tuned per landing profile against real flights - RTLS 45/60 km/110,
        /// droneship 40/70 km/97, expendable 10/70 km/72.5. Do not retune them here.
        /// </summary>
        public static double TurnPitch(AscentInputs s, AscentTarget t)
        {
            double meco = (t.MecoAngleDeg > 0.0) ? t.MecoAngleDeg : 45.0;

            if (s.SecondStage)
            {
                double tanAlt = (s.AtmosphereDepthM > 0.0) ? s.AtmosphereDepthM : 70000.0;
                double p = 90.0 * (1.0 - s.Altitude / tanAlt);
                if (p < 0.1) p = 0.1;
                // avoidFireDeath: 5 * max(0, (30 - eta:apoapsis) * 0.075)
                double eta = s.TimeToApoapsisS;
                double fire = 5.0 * ((30.0 - eta) * 0.075);
                if (fire < 0.0) fire = 0.0;
                p += fire;
                return (p > meco) ? meco : p;
            }

            double gain = (t.PitchGain > 0.0) ? t.PitchGain : 110.0;
            double stageAlt = (t.StageAltM > 0.0) ? t.StageAltM : 60000.0;
            double denom = stageAlt * (gain / 100.0);
            if (denom <= 0.0) denom = 66000.0;

            double pitch = 90.0 * (1.0 - s.Altitude / denom);
            // The MECO floor. Never below the horizon either - a negative pitch here would be a
            // guidance bug driving the stack into the ground at full throttle.
            if (pitch < meco) pitch = meco;
            return (pitch < 0.0) ? 0.0 : pitch;
        }

        /// <summary>
        /// Throttle back through max Q. Proportional above the limit rather than a hard cut, so the
        /// vehicle eases off instead of pogoing between full and nothing.
        /// </summary>
        public static double QThrottle(AscentInputs s)
        {
            if (s.DynamicPressureKpa <= MaxQKpa) return 1.0;
            double over = (s.DynamicPressureKpa - MaxQKpa) / MaxQKpa;
            double th = 1.0 - over * 2.0;
            if (th < 0.35) th = 0.35;      // never below the level that keeps the engines happy
            return th;
        }

        public static string Name(AscentPhase p)
        {
            switch (p)
            {
                case AscentPhase.VerticalRise: return "VERTICAL RISE";
                case AscentPhase.GravityTurn:  return "GRAVITY TURN";
                case AscentPhase.Meco:         return "MECO";
                case AscentPhase.StageSep:     return "STAGE SEP";
                case AscentPhase.BurnToApoapsis: return "BURN TO APOAPSIS";
                case AscentPhase.Coast:        return "COAST";
                case AscentPhase.Circularise:  return "CIRCULARISE";
                case AscentPhase.Done:         return "INSERTION COMPLETE";
                default:                       return "STANDBY";
            }
        }
    }
}
