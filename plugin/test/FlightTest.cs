/*
 * DragonScreen headless tests - the flight software: booster recovery, rendezvous, entry.
 *
 * ---- WHY THESE ARE WORTH MORE THAN ANY OTHER SUITE HERE ----
 * Every other test in this project protects a layout. These protect three rules that were each paid
 * for with a LOST FLIGHT:
 *
 *      the periapsis floor        flight 012 de-orbited itself while its display said "closing"
 *      retrograde on every band   CargoDragon_012 flew an entire entry 134.9 deg off the nose
 *      the landing TWR check      at 11% propellant one engine gives TWR 0.81 and cannot land
 *
 * A layout bug costs a restart. These cost a vehicle, and two of them were invisible from inside the
 * cockpit while they were happening. That is exactly the class of thing a headless check is for.
 */
using System;
using DragonScreen;

public static class FlightTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok)
        {
            failures++;
            Console.WriteLine("  FAIL  " + what + "   " + detail);
        }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen flight software tests");
        Recovery();
        PhaseSweep();
        Approach();
        Reentry();
        Return();
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // ------------------------------------------------------------------ the sweep

    /// <summary>
    /// Drive EVERY landing phase against EVERY representative vehicle state and assert the handful
    /// of things that must be true regardless of which pair you picked.
    ///
    /// ---- WHY THIS EXISTS ----
    /// Three consecutive "fix" commits each introduced a defect, and every one of them was a local
    /// change that did not account for the state machine around it:
    ///
    ///   - a new phase inherited SurfaceRetrograde because its case never set Aim, which would have
    ///     flipped a 40 m booster 180 degrees with the upper stage 11 m away
    ///   - the grid-fin gate enumerated phases and a phase was inserted before it without revisiting
    ///   - NoSolution returned before c.Engines was assigned, so it asked for full throttle on zero
    ///     engines
    ///   - sequential `if`s let Boostback cascade to LandingBurn in one tick, at 44 km, climbing
    ///
    /// Reading carefully did not catch these; I wrote three of them while reading carefully. A sweep
    /// does, because it does not care which phase is new - it asks the same questions of all of them.
    /// Anything added to LandingPhase is covered the moment it exists.
    /// </summary>
    static void PhaseSweep()
    {
        LandingPhase[] phases = (LandingPhase[])Enum.GetValues(typeof(LandingPhase));

        // Representative states, each one a situation the booster genuinely passes through.
        LandingInputs[] states = new LandingInputs[6];
        string[] names = new string[6];

        states[0] = Fall(29000.0, 700.0, 780.0, 43.0, 9);        // just separated, alongside
        states[0].RangeToPartnerM = 11.4; states[0].AccelOneEngine = 13.0;
        names[0] = "alongside";

        states[1] = Fall(40000.0, 500.0, 900.0, 43.0, 9);        // boosting back, climbing
        states[1].AccelOneEngine = 15.0; states[1].InitialMissM = 60000.0;
        states[1].PredictedMissM = 30000.0; states[1].DownrangeM = 40000.0;
        names[1] = "climbing";

        states[2] = Fall(60000.0, -50.0, 400.0, 43.0, 9);        // over the top, falling
        states[2].AccelOneEngine = 15.0; states[2].DownrangeM = 20000.0;
        names[2] = "high falling";

        states[3] = Fall(20000.0, -500.0, 1200.0, 43.0, 9);      // in the entry band
        states[3].AccelOneEngine = 15.0; states[3].DownrangeM = 8000.0;
        names[3] = "entry band";

        states[4] = Fall(1500.0, -160.0, 165.0, 43.0, 9);        // terminal, solvable
        states[4].AccelOneEngine = 16.0; states[4].DownrangeM = 400.0;
        names[4] = "terminal";

        states[5] = Fall(3000.0, -200.0, 210.0, 9.0, 9);         // cannot stop at any altitude
        states[5].AccelOneEngine = 5.0;
        names[5] = "no thrust margin";

        for (int p = 0; p < phases.Length; p++)
        {
            // A phase in the enum with no name is a phase somebody forgot to finish. Idle is the
            // one legitimate STANDBY - it is the "not flying" state.
            if (phases[p] != LandingPhase.Idle)
                Check("phase " + phases[p] + " has a display name",
                      Landing.Name(phases[p]) != "STANDBY", Landing.Name(phases[p]));

            for (int i = 0; i < states.Length; i++)
            {
                LandingCommand c = Landing.Guide(states[i], phases[p]);
                string w = phases[p] + " / " + names[i];

                Check(w + ": throttle is a real number in range",
                      !double.IsNaN(c.Throttle) && !double.IsInfinity(c.Throttle)
                      && c.Throttle >= 0.0 && c.Throttle <= 1.0, c.Throttle.ToString("F3"));

                // Thrust with nothing lit is the NoSolution bug: SetEngines(v, 0) shuts the octaweb
                // down and the throttle command reaches nothing.
                Check(w + ": asking for thrust means asking for engines",
                      !(c.Throttle > 0.001 && c.Engines <= 0),
                      "thr " + c.Throttle.ToString("F2") + " eng " + c.Engines);

                // Nothing burns next to the vehicle it just left, from ANY starting phase.
                // Touchdown is exempt: a stage that is already down is not manoeuvring.
                if (Landing.NearPartner(states[i]) && phases[p] != LandingPhase.Touchdown)
                {
                    Check(w + ": nothing burns while alongside",
                          Math.Abs(c.Throttle) < 1e-9, c.Throttle.ToString("F3"));
                    // NOT "and nothing slews". F9I DOES rotate while the two are still close - it
                    // just waits 2 s for the vessel split to resolve first (WaitForSep) and another
                    // 2 s settling before the rotation starts. Forbidding the slew outright would
                    // forbid the flip, which is the manoeuvre that gets the stage home.
                    Check(w + ": and it is quiet for the first seconds after the split",
                          !(states[i].PhaseElapsedS < Landing.SepQuietS
                            && c.Aim != LandingAim.Hold), c.Aim.ToString());
                }

                // A landing burn is a thing you do while FALLING. (Descent and EntryBurn are
                // reachable as forced inputs while climbing, but both command zero throttle, so
                // they are harmless; the burn is not.)
                // A landing burn is a thing you do while FALLING. It cannot be ENTERED while
                // climbing; forced in as a starting phase it must at least stop pushing, because
                // BurnThrottle is computed from speed and does not care which way that speed points.
                if (states[i].VerticalSpeed > 0.0)
                {
                    if (phases[p] != LandingPhase.LandingBurn)
                        Check(w + ": a climbing stage never enters a landing burn",
                              c.Phase != LandingPhase.LandingBurn, c.Phase.ToString());
                    Check(w + ": and a climbing stage is never pushed further up",
                          !(c.Phase == LandingPhase.LandingBurn && c.Throttle > 0.0),
                          c.Throttle.ToString("F3"));
                }

                // The glue divides by nothing and guesses at nothing: every flying phase states the
                // steering gain it wants.
                if (c.Phase != LandingPhase.Idle && c.Phase != LandingPhase.Touchdown)
                    Check(w + ": states a steering gain", c.StoppingTime > 0.0,
                          c.StoppingTime.ToString("F3"));

                // Guide must settle. Feeding its own answer back must not ping-pong for ever.
                LandingPhase again = Landing.Guide(states[i], c.Phase).Phase;
                LandingPhase third = Landing.Guide(states[i], again).Phase;
                Check(w + ": the state machine settles", again == third,
                      c.Phase + " -> " + again + " -> " + third);
            }
        }
    }

    // ------------------------------------------------------------------ booster recovery

    static LandingInputs Fall(double alt, double vSpeed, double srfSpeed, double accel, int engines)
    {
        LandingInputs s = new LandingInputs();
        s.Valid = true;
        // ASL as well as radar: F9I gates the entry burn on ASL, and over a landing zone at sea
        // level the two are the same. Setting only one of them is how the first version of this
        // fixture passed a gate it should not have.
        s.AltitudeRadar = alt; s.AltitudeAsl = alt;
        s.VerticalSpeed = vSpeed; s.SurfaceSpeed = srfSpeed;
        s.HorizontalSpeed = Math.Sqrt(Math.Max(0.0, srfSpeed * srfSpeed - vSpeed * vSpeed));
        s.MaxThrustAccel = accel; s.EngineCount = engines;
        s.Gravity = 9.81; s.AtmosphereDepthM = 70000.0;
        s.RecoveryPropFrac = 1.0;   // full recovery load unless a test lowers it
        return s;
    }

    static void Recovery()
    {
        // ---- MECHJEB'S LAW, CHECKED AGAINST ARITHMETIC RATHER THAN AGAINST ITSELF ----
        double v = Landing.MaxAllowedSpeed(1000.0, 20.0, 10.0);
        Check("hoverslam curve", Math.Abs(v - 0.9 * Math.Sqrt(2 * 10.0 * 1000.0)) < 1e-6,
              v.ToString("F3"));
        Check("higher allows faster",
              Landing.MaxAllowedSpeed(2000.0, 20.0, 10.0) > Landing.MaxAllowedSpeed(1000.0, 20.0, 10.0), "");
        Check("on the deck you may not be moving",
              Landing.MaxAllowedSpeed(0.0, 20.0, 10.0) == 0.0, "");

        // ---- NO SOLUTION IS REPORTED, NOT DIVIDED BY ----
        // falcon-booster-landing-twr: TWR 0.81 on one engine at 11% propellant is a REAL case.
        Check("no thrust margin gives no ignition altitude",
              Landing.IgnitionAltitude(300.0, 9.0, 9.81) == 0.0, "");
        Check("no thrust margin gives no allowed speed",
              Landing.MaxAllowedSpeed(5000.0, 9.0, 9.81) == 0.0, "");

        // The curve and its inverse must agree, or the booster lights at the wrong height.
        double ign = Landing.IgnitionAltitude(200.0, 25.0, 9.81);
        double back = Landing.MaxAllowedSpeed(ign, 25.0, 9.81);
        Check("ignition altitude inverts the curve", Math.Abs(back - 200.0) < 0.5, back.ToString("F2"));

        // ---- ENGINE COUNTS ARE THE REAL PROFILE: 3 / 3 / 1 ----
        LandingInputs heavy = Fall(40000.0, -400.0, 420.0, 90.0, 9);
        Check("boostback on three", Landing.EnginesFor(LandingPhase.Boostback, heavy) == 3,
              Landing.EnginesFor(LandingPhase.Boostback, heavy).ToString());
        // ---- ⛔ ONE TRANSITION PER TICK, AND THE HOVERSLAM ONLY ON THE WAY DOWN ----
        // 22:18 flight: boostback completed, the sequential `if`s let Coast fall straight through to
        // LandingBurn on the SAME TICK, and the booster lit its landing burn at 44 km while CLIMBING
        // at 828 m/s. It burned 370 s, flew to 90 km, ran dry and reported NO SOLUTION at 6.8 km.
        // `ign` was 89.6 km because it is derived from SURFACE speed, which during boostback is
        // mostly horizontal - so the gate was open from the moment of handover.
        LandingInputs climbFast = Fall(44000.0, 828.0, 830.0, 43.0, 9);
        climbFast.PredictedMissM = -9000.0;              // boostback overshoot achieved
        climbFast.InitialMissM = 60000.0;
        LandingCommand cascade = Landing.Guide(climbFast, LandingPhase.Boostback);
        Check("boostback leaves to COAST, never straight to the landing burn",
              cascade.Phase == LandingPhase.Coast, cascade.Phase.ToString());
        Check("a climbing stage is never in a landing burn",
              Landing.Guide(climbFast, LandingPhase.Coast).Phase != LandingPhase.LandingBurn,
              Landing.Guide(climbFast, LandingPhase.Coast).Phase.ToString());

        // The same state, but falling, must arm normally - the gate has to still work.
        LandingInputs fallFast = Fall(2000.0, -180.0, 190.0, 43.0, 9);
        fallFast.AccelOneEngine = 15.0;
        Check("a falling stage inside the ignition altitude does light",
              Landing.Guide(fallFast, LandingPhase.Descent).Phase == LandingPhase.LandingBurn,
              Landing.Guide(fallFast, LandingPhase.Descent).Phase.ToString());

        // ---- ⛔ A DRONESHIP ENTRY BURN RESERVES THE LANDING PROPELLANT (flight_0823_082234). ----
        // Still falling fast in the entry band (vs < -300), so the vertical-speed cut has NOT tripped.
        LandingInputs ebDrn = Fall(25000.0, -880.0, 2430.0, 43.0, 9);   // huge horizontal, deep in the band
        ebDrn.Droneship = true;
        ebDrn.RecoveryPropFrac = 0.8;   // plenty of recovery propellant left
        Check("a droneship entry burn keeps burning while propellant is above the reserve",
              Landing.Guide(ebDrn, LandingPhase.EntryBurn).Phase == LandingPhase.EntryBurn,
              Landing.Guide(ebDrn, LandingPhase.EntryBurn).Phase.ToString());
        ebDrn.RecoveryPropFrac = 0.45;  // hit the 0.5 reserve - the landing burn's share
        Check("...and CUTS to Descent the moment the reserve is reached, saving landing fuel",
              Landing.Guide(ebDrn, LandingPhase.EntryBurn).Phase == LandingPhase.Descent,
              Landing.Guide(ebDrn, LandingPhase.EntryBurn).Phase.ToString());
        // An RTLS booster (not droneship) is NOT cut on the reserve - it keeps F9I's vertical-speed cut.
        LandingInputs ebRtls = Fall(25000.0, -880.0, 2430.0, 43.0, 9);
        ebRtls.Droneship = false; ebRtls.RecoveryPropFrac = 0.1;
        Check("an RTLS entry burn ignores the reserve (it boosted back, so it is short anyway)",
              Landing.Guide(ebRtls, LandingPhase.EntryBurn).Phase == LandingPhase.EntryBurn,
              Landing.Guide(ebRtls, LandingPhase.EntryBurn).Phase.ToString());

        // ---- BOOSTBACK STOPS ON A PREDICTED IMPACT POINT, DELIBERATELY LONG ----
        LandingInputs bbShort = Fall(40000.0, 500.0, 900.0, 43.0, 9);
        bbShort.InitialMissM = 60000.0; bbShort.PredictedMissM = 30000.0;
        Check("still short of the pad keeps burning",
              Landing.Guide(bbShort, LandingPhase.Boostback).Phase == LandingPhase.Boostback, "");
        bbShort.PredictedMissM = -1000.0;   // past the pad, but not by the overshoot margin
        Check("just past the pad is not far enough - drag will eat it",
              Landing.Guide(bbShort, LandingPhase.Boostback).Phase == LandingPhase.Boostback, "");
        bbShort.PredictedMissM = -(Landing.BoostbackOvershootM + 1.0);
        Check("overshooting by the margin ends the burn",
              Landing.Guide(bbShort, LandingPhase.Boostback).Phase == LandingPhase.Coast, "");
        Check("and the overshoot is F9I's 2.7 km",
              Math.Abs(Landing.BoostbackOvershootM - 2700.0) < 1e-9, "");

        // ---- DRONESHIP (Crew-2, ASDS) SKIPS BOOSTBACK ----
        LandingInputs dsClimb = Fall(60000.0, 200.0, 2400.0, 20.0, 9);   // climbing after sep
        dsClimb.Droneship = true;
        Check("a droneship booster still climbing coasts to entry, does not boost back",
              Landing.InitialPhase(dsClimb) == LandingPhase.Coast,
              Landing.InitialPhase(dsClimb).ToString());
        LandingInputs rtlsClimb = dsClimb; rtlsClimb.Droneship = false;
        Check("...whereas an RTLS booster still climbing boosts back",
              Landing.InitialPhase(rtlsClimb) == LandingPhase.Boostback, "");
        // Flip hands straight to COAST for a droneship, skipping BoostbackKill + Boostback.
        LandingInputs dsFlip = Fall(60000.0, 200.0, 2400.0, 20.0, 9);
        dsFlip.Droneship = true; dsFlip.FlipDone = true; dsFlip.RangeToPartnerM = 0.0;
        Check("a droneship flip hands to COAST (no boostback)",
              Landing.Guide(dsFlip, LandingPhase.Flip).Phase == LandingPhase.Coast,
              Landing.Guide(dsFlip, LandingPhase.Flip).Phase.ToString());
        LandingInputs rtlsFlip = dsFlip; rtlsFlip.Droneship = false;
        Check("...whereas an RTLS flip hands to BOOSTBACK KILL",
              Landing.Guide(rtlsFlip, LandingPhase.Flip).Phase == LandingPhase.BoostbackKill, "");

        // Throttle tapers with the error and never drops below the floor that keeps the gimbal live.
        LandingInputs bbT = Fall(40000.0, 500.0, 900.0, 43.0, 9);
        bbT.InitialMissM = 40000.0; bbT.PredictedMissM = 40000.0;
        Check("boostback opens at full throttle",
              Math.Abs(Landing.BoostbackThrottle(bbT) - 1.0) < 1e-9, "");
        bbT.PredictedMissM = 20000.0;
        Check("half the error left is half throttle",
              Math.Abs(Landing.BoostbackThrottle(bbT) - 0.5) < 1e-9, "");
        bbT.PredictedMissM = 100.0;
        Check("and it floors at 25%, not at zero",
              Math.Abs(Landing.BoostbackThrottle(bbT) - Landing.BoostbackMinThrottle) < 1e-9, "");

        // ---- THE LANDING ZONE IS NOT THE LAUNCH PAD, AND THE PROFILES ARE NOT INTERCHANGEABLE ----
        Check("LZ-1 is a real surveyed coordinate, not the origin",
              Math.Abs(LandingSites.Lz1.LatDeg) > 0.0001 && Math.Abs(LandingSites.Lz1.LonDeg) > 1.0,
              "");
        Check("LZ-1 and LZ-2 are different pads",
              Math.Abs(LandingSites.Lz1.LatDeg - LandingSites.Lz2.LatDeg) > 1e-6, "");
        double ma, sa, pg, mp, ma2, sa2, pg2, mp2;
        LandingSites.AscentFor(LandingProfile.Rtls, out ma, out sa, out pg, out mp);
        LandingSites.AscentFor(LandingProfile.Droneship, out ma2, out sa2, out pg2, out mp2);
        Check("RTLS stages earlier and steeper than a droneship flight",
              ma > ma2 && sa < sa2, ma + "/" + sa + " vs " + ma2 + "/" + sa2);
        Check("RTLS is PARAM.ks RTLSmode: 45 deg, 60 km, gain 110",
              Math.Abs(ma - 45.0) < 1e-9 && Math.Abs(sa - 60000.0) < 1e-9
              && Math.Abs(pg - 110.0) < 1e-9, "");
        Check("a droneship flight can carry more payload",  mp2 > mp, "");
        Check("RTLS reverses fully, a droneship flight does not",
              Math.Abs(LandingSites.FlipDeg(LandingProfile.Rtls) - 180.0) < 1e-9
              && Math.Abs(LandingSites.FlipDeg(LandingProfile.Droneship) - 170.0) < 1e-9, "");
        Check("an expendable stage is not recovered",
              !LandingSites.Recovers(LandingProfile.Expendable)
              && LandingSites.Recovers(LandingProfile.Rtls), "");

        // ---- ForBody: Kerbin stays FLOWN, RSS/Earth uses constants MEASURED off flight_0822_011349 ----
        // Crew-2 (like Crew-1) flies the Droneship profile (booster to the barge downrange).
        AscentTarget kerbin = AscentTarget.Station(LandingProfile.Droneship);
        Check("Kerbin keeps the flown 20 kPa max-Q ceiling and no decoupled pitch scale",
              Math.Abs(kerbin.MaxQKpa - Ascent.MaxQKpa) < 1e-9 && kerbin.PitchRefAltM == 0.0,
              kerbin.MaxQKpa + "/" + kerbin.PitchRefAltM);

        // RSS/RO Earth: 200 km parking orbit below the ISS.
        AscentTarget earth = AscentTarget.ForBody(LandingProfile.Droneship, 200000.0);
        Check("Earth targets the real ~200 km parking orbit, not Kerbin's 120 km",
              Math.Abs(earth.AltitudeM - 200000.0) < 1e-9, earth.AltitudeM.ToString());
        Check("Earth's MECO apoapsis is ~110 km (MechJeb staged at 121), below the parking orbit",
              Math.Abs(earth.StageAltM - 110000.0) < 1e-9 && earth.StageAltM < earth.AltitudeM,
              earth.StageAltM.ToString());
        // Decoupled from the 110 km staging and well under the ~135 km that lofts. Tuned 40->50 km on
        // 2026-08-22 to ease the first-stage AoA (flight_0822_112918 peaked 17 deg); the range, not a
        // point, is what matters - it must not equal StageAltM and must stay below the loft threshold.
        Check("the pitch-over scale is DECOUPLED from staging and below the loft threshold",
              earth.PitchRefAltM >= 30000.0 && earth.PitchRefAltM <= 70000.0
              && earth.PitchRefAltM != earth.StageAltM, earth.PitchRefAltM.ToString());
        Check("Earth stages far shallower than Kerbin - MECO floor 25 vs 40 deg",
              earth.MecoAngleDeg < kerbin.MecoAngleDeg && Math.Abs(earth.MecoAngleDeg - 25.0) < 1e-9,
              earth.MecoAngleDeg + " vs " + kerbin.MecoAngleDeg);
        Check("Earth's max-Q ceiling clears the measured 31 kPa peak so it flies at full thrust",
              earth.MaxQKpa > 31.0 && earth.MaxQKpa > kerbin.MaxQKpa,
              earth.MaxQKpa.ToString());

        // The decoupled pitch scale must actually make the Earth turn pitch over FASTER than the
        // Kerbin law would at the same altitude (the whole point - the atmosphere-scaling first
        // attempt lofted). At 20 km: Earth ~45 deg vs Kerbin's ~63 deg.
        AscentInputs at20 = new AscentInputs();
        at20.Altitude = 20000.0; at20.SecondStage = false;
        Check("the decoupled Earth scale pitches over faster than the Kerbin law at 20 km",
              Ascent.TurnPitch(at20, earth) < Ascent.TurnPitch(at20, kerbin),
              Ascent.TurnPitch(at20, earth).ToString("F1") + " vs "
              + Ascent.TurnPitch(at20, kerbin).ToString("F1"));

        // ---- MECO on FLAMEOUT, not only the apoapsis target (real F9 stages on depletion) ----
        AscentInputs spent = new AscentInputs();
        spent.Valid = true; spent.SecondStage = false; spent.Altitude = 80000.0; spent.AvailableThrust = 0.0;
        Check("a booster making no thrust up high is spent", Ascent.FirstStageSpent(spent), "");
        AscentInputs burning = spent; burning.AvailableThrust = 6000.0;
        Check("a booster still making thrust is not spent", !Ascent.FirstStageSpent(burning), "");
        AscentInputs lowPad = spent; lowPad.Altitude = 500.0;
        Check("no thrust near the pad is a transient, not spent", !Ascent.FirstStageSpent(lowPad), "");
        AscentInputs s2spent = spent; s2spent.SecondStage = true;
        Check("the second stage is never 'first stage spent'", !Ascent.FirstStageSpent(s2spent), "");
        // A flameout BELOW the MECO apoapsis target still triggers MECO (the 31 s dead-stage gap).
        AscentInputs gt = spent; gt.ApoapsisM = 90000.0;   // below the 110 km Earth MECO target
        Check("flameout below the MECO target still MECOs, not coast a dead stage",
              Ascent.Guide(gt, earth, AscentPhase.GravityTurn).Phase == AscentPhase.Meco,
              Ascent.Guide(gt, earth, AscentPhase.GravityTurn).Phase.ToString());

        // ---- ⛔ ULLAGE IS HELD UNTIL THE MVac CATCHES, not a fixed 6 s (flight_0822_205453). ----
        // The MVac needs settled propellant; stopping the RCS-fore on a clock while the engine had not
        // yet built thrust flamed it out on "No propellants" and the stage never circularised.
        AscentInputs ull = new AscentInputs();
        ull.Valid = true; ull.SecondStage = true; ull.Altitude = 70000.0;
        ull.PhaseElapsedS = 3.0; ull.AvailableThrust = 0.0;   // inside the settle window
        AscentCommand uSettle = Ascent.Guide(ull, earth, AscentPhase.BurnToApoapsis);
        Check("ullage fires during the settle window",
              uSettle.UllageFore > 0.5, uSettle.UllageFore.ToString("F2"));

        AscentInputs held = ull; held.PhaseElapsedS = 9.0; held.AvailableThrust = 0.0;   // past 6 s, no thrust yet
        AscentCommand uHeld = Ascent.Guide(held, earth, AscentPhase.BurnToApoapsis);
        Check("ullage is HELD past the settle window while the engine has not caught  [the fix]",
              uHeld.UllageFore > 0.5, uHeld.UllageFore.ToString("F2"));
        Check("...and it commands real throttle at the same time (light it WHILE settling)",
              uHeld.Throttle > Ascent.UllageThrottle, uHeld.Throttle.ToString("F3"));

        AscentInputs caught = ull; caught.PhaseElapsedS = 9.0; caught.AvailableThrust = 800.0;   // MVac running
        AscentCommand uCaught = Ascent.Guide(caught, earth, AscentPhase.BurnToApoapsis);
        Check("ullage RELEASES once the engine is really thrusting (it self-settles)",
              uCaught.UllageFore < 1e-9, uCaught.UllageFore.ToString("F2"));

        // ---- ⛔ RCS OFF DURING POWERED FLIGHT, ON ONLY WHEN THE GIMBAL IS GONE (crew, flight_0823). ----
        // The glue turns RCS on iff (c.Rcs || UllageFore>0.01); these pin which phases ask for it.
        AscentInputs pw = new AscentInputs(); pw.Valid = true; pw.Altitude = 5000.0;
        Check("vertical rise wants NO RCS (engine gimbal holds it)",
              !Ascent.Guide(pw, earth, AscentPhase.VerticalRise).Rcs, "");
        pw.Altitude = 20000.0;
        Check("gravity turn wants NO RCS", !Ascent.Guide(pw, earth, AscentPhase.GravityTurn).Rcs, "");
        AscentInputs circ = new AscentInputs(); circ.Valid = true; circ.CircDvMps = 50.0;
        Check("circularise (MVac burning) wants NO RCS",
              !Ascent.Guide(circ, earth, AscentPhase.Circularise).Rcs, "");
        AscentInputs held2 = ull; held2.PhaseElapsedS = 9.0; held2.AvailableThrust = 800.0;
        Check("a thrusting BurnToApoapsis wants NO RCS (gimbal, and ullage released)",
              !Ascent.Guide(held2, earth, AscentPhase.BurnToApoapsis).Rcs
              && Ascent.Guide(held2, earth, AscentPhase.BurnToApoapsis).UllageFore < 1e-9, "");
        AscentInputs up = new AscentInputs(); up.Valid = true;
        Check("MECO hold wants RCS (engines out)", Ascent.Guide(up, earth, AscentPhase.Meco).Rcs, "");
        Check("stage-sep hold wants RCS", Ascent.Guide(up, earth, AscentPhase.StageSep).Rcs, "");
        AscentInputs cst = new AscentInputs(); cst.Valid = true;
        cst.TimeToApoapsisS = 300.0; cst.PeriapsisM = -100000.0;   // still coasting up, not yet circularising
        Check("coast wants RCS (engines out, no gimbal)", Ascent.Guide(cst, earth, AscentPhase.Coast).Rcs,
              Ascent.Guide(cst, earth, AscentPhase.Coast).Phase.ToString());
        Check("shutdown wants RCS", Ascent.Guide(up, earth, AscentPhase.Shutdown).Rcs, "");

        // ---- ⛔ NOTHING LIGHTS WHILE THE TWO VEHICLES ARE STILL ALONGSIDE ----
        // 23:19 flight: the booster lit three engines at full throttle 11.4 m from the upper stage,
        // the vertical gap went NEGATIVE - they passed through each other - and the booster came
        // apart, 59.20 t down to 9.40 t. The 3 s coast bought no clearance because there is almost
        // no separation impulse; the gap was still ~11 m when it expired. Distance is the condition.
        LandingInputs alongside = Fall(29000.0, 700.0, 780.0, 43.0, 9);
        alongside.RangeToPartnerM = 11.4;
        Check("a booster still alongside starts in SEPARATING",
              Landing.InitialPhase(alongside) == LandingPhase.Flip, "");
        LandingCommand sepc = Landing.Guide(alongside, LandingPhase.Flip);
        Check("and it burns nothing", Math.Abs(sepc.Throttle) < 1e-9, sepc.Throttle.ToString("F3"));
        Check("and it stays there while it is close",
              sepc.Phase == LandingPhase.Flip, sepc.Phase.ToString());
        // ⛔ AND IT DOES NOT ROTATE. The stage is climbing at 700 m/s, so surface retrograde is very
        // nearly straight down - a 180-degree flip with the upper stage 11 m away. A 40 m booster
        // cannot rotate through that without hitting it.
        Check("a booster holds still for the first seconds - the split is still resolving",
              sepc.Aim == LandingAim.Hold, sepc.Aim.ToString());
        // ...and then it flips, because that is the manoeuvre, not a hazard.
        LandingInputs flipping = alongside; flipping.PhaseElapsedS = Landing.FlipHoldS + 0.1;
        Check("and then it starts the turnaround",
              Landing.Guide(flipping, LandingPhase.Flip).Aim == LandingAim.Flip, "");
        Check("still burning nothing while it does",
              Math.Abs(Landing.Guide(flipping, LandingPhase.Flip).Throttle) < 1e-9, "");

        // ---- A BOOSTBACK THAT CANNOT SOLVE MUST STILL END ----
        // PredictedMissM is 0 when the predictor cannot answer, and 0 < -2700 is false for ever.
        LandingInputs blind = Fall(40000.0, 300.0, 900.0, 43.0, 9);
        blind.PredictedMissM = 0.0; blind.InitialMissM = 0.0;
        blind.PhaseElapsedS = Landing.MaxBoostbackS + 1.0;
        Check("an unsolvable boostback stops instead of burning dry",
              Landing.Guide(blind, LandingPhase.Boostback).Phase == LandingPhase.Coast, "");
        blind.PhaseElapsedS = 10.0;
        Check("but not before it has had a fair run",
              Landing.Guide(blind, LandingPhase.Boostback).Phase == LandingPhase.Boostback, "");

        // The flip ends when the stage is ROUND, not when a clock says so.
        LandingInputs flipDone = Fall(29000.0, 700.0, 780.0, 43.0, 9);
        flipDone.RangeToPartnerM = Landing.SafeSeparationM + 1.0;
        flipDone.PhaseElapsedS = 18.0;
        Check("the flip holds until the stage is round",
              Landing.Guide(flipDone, LandingPhase.Flip).Phase == LandingPhase.Flip, "");
        flipDone.FlipDone = true;
        Check("and then the burn kills downrange velocity first",
              Landing.Guide(flipDone, LandingPhase.Flip).Phase == LandingPhase.BoostbackKill, "");

        // ---- BOOSTBACK IS TWO HALVES, AND I ONLY EVER BUILT THE SECOND ----
        // BOOSTER.ks:417 holds FLAT RETROGRADE until the horizontal velocity collapses, and only
        // then aims at the pad: "once the horizontal velocity is dead the retrograde direction is
        // meaningless as a steering reference". Aiming at the pad from the first tick is why the
        // landings went to the wrong place.
        LandingInputs killing = Fall(45000.0, 400.0, 800.0, 43.0, 9);
        killing.AccelOneEngine = 15.0; killing.HorizRetroMag = 0.6; killing.PhaseElapsedS = 5.0;
        LandingCommand kc = Landing.Guide(killing, LandingPhase.BoostbackKill);
        Check("the first half holds flat retrograde",
              kc.Aim == LandingAim.FlatRetrograde, kc.Aim.ToString());
        Check("at full throttle once the ramp is done",
              Math.Abs(kc.Throttle - 1.0) < 1e-9, kc.Throttle.ToString("F3"));
        Check("and the throttle RAMPS rather than stepping",
              Landing.Ramp(0.0) < 0.01 && Landing.Ramp(0.25) < 0.5
              && Math.Abs(Landing.Ramp(1.0) - 1.0) < 1e-9, "");
        killing.HorizRetroMag = Landing.HorizVelocityDead * 0.5;
        Check("downrange dead, now aim at the pad",
              Landing.Guide(killing, LandingPhase.BoostbackKill).Phase == LandingPhase.Boostback,
              "");
        Check("and the boostback holds steady, not fast - 15 s, not 1",
              Math.Abs(Landing.BoostbackStoppingTime - 15.0) < 1e-9, "");

        // No partner at all - a solo booster test - must not be gated on a range it cannot measure.
        LandingInputs solo = Fall(29000.0, 700.0, 780.0, 43.0, 9);
        solo.RangeToPartnerM = 0.0;
        Check("an unmeasurable range is not a reason to sit still",
              Landing.InitialPhase(solo) != LandingPhase.Flip, "");

        // ---- ⛔ THE ENTRY BURN FLIES STRAIGHT RETROGRADE, NEVER LEANED ----
        // BOOSTER.ks:716: "leaning the stage off retrograde while doing it puts a large side load on
        // it." Our glue leaned on any retrograde aim with a downrange error, which is every phase -
        // full thrust, maximum dynamic pressure, and an angle of attack on top of it.
        LandingInputs inEntry = Fall(20000.0, -500.0, 1200.0, 43.0, 9);
        inEntry.PhaseElapsedS = 2.0; inEntry.DownrangeM = 30000.0;
        LandingCommand eb = Landing.Guide(inEntry, LandingPhase.EntryBurn);
        Check("the entry burn is not steered", !eb.GuidedLean, "");
        Check("and it is loose on the stick so it does not fight the air",
              Math.Abs(eb.StoppingTime - Landing.EntryStoppingTime) < 1e-9,
              eb.StoppingTime.ToString("F2"));

        // AccelOneEngine matters here: without it PhaseAccel falls back to accel/9, which is under
        // gravity, and Guide correctly answers NO SOLUTION instead of the phase being asked about.
        LandingInputs glide = Fall(8000.0, -250.0, 260.0, 43.0, 9);
        glide.AccelOneEngine = 16.0; glide.DownrangeM = 4000.0;
        LandingCommand gl = Landing.Guide(glide, LandingPhase.Descent);
        Check("the glide IS steered - it is where drag does the work", gl.GuidedLean, "");
        Check("and it tightens up for the glide",
              Math.Abs(gl.StoppingTime - Landing.GlideStoppingTime) < 1e-9, "");

        LandingInputs lb = Fall(600.0, -120.0, 125.0, 43.0, 9);
        lb.AccelOneEngine = 16.0; lb.DownrangeM = 300.0;
        LandingCommand lbc = Landing.Guide(lb, LandingPhase.LandingBurn);
        Check("the landing burn steers too", lbc.GuidedLean, "");
        Check("and it is tightest of all",
              Math.Abs(lbc.StoppingTime - Landing.LandingStoppingTime) < 1e-9, "");
        Check("the three gains are genuinely different, loosest first",
              Landing.EntryStoppingTime > Landing.GlideStoppingTime
              && Landing.GlideStoppingTime > Landing.LandingStoppingTime, "");
        Check("every flying phase asks for a gain - zero would mean the glue guesses",
              Landing.Guide(Fall(40000.0, 400.0, 900.0, 43.0, 9),
                            LandingPhase.Boostback).StoppingTime > 0.0, "");

        // ---- JOIN THE PROFILE WHERE THE STAGE ACTUALLY IS ----
        // Handover is late by design (the upper stage cannot be abandoned mid-ascent), so starting
        // at Boostback unconditionally would point a falling booster back up the range. The 21:01
        // flight handed over 155 s after separation.
        LandingInputs climbing = Fall(35000.0, 250.0, 900.0, 90.0, 9);
        Check("a stage still climbing gets a boostback",
              Landing.InitialPhase(climbing) == LandingPhase.Boostback, "");
        LandingInputs highFall = Fall(50000.0, -150.0, 900.0, 90.0, 9);
        Check("a stage falling from above the gate coasts first",
              Landing.InitialPhase(highFall) == LandingPhase.Coast, "");
        LandingInputs lowFall = Fall(20000.0, -400.0, 1100.0, 90.0, 9);
        Check("a stage already below the gate goes straight to the entry burn",
              Landing.InitialPhase(lowFall) == LandingPhase.EntryBurn, "");
        LandingInputs down = Fall(0.0, 0.0, 0.0, 90.0, 9); down.Landed = true;
        Check("a stage on the ground is not flown at all",
              Landing.InitialPhase(down) == LandingPhase.Touchdown, "");
        // The boundary is the entry gate itself, and it must be the SAME constant the burn uses -
        // two copies one metre apart is how a gate opens in the wrong place.
        LandingInputs atGate = Fall(Landing.EntryBurnGateAsl, -300.0, 1000.0, 90.0, 9);
        Check("the gate boundary belongs to the entry burn",
              Landing.InitialPhase(atGate) == LandingPhase.EntryBurn, "");

        // ---- THE ENTRY BURN'S SOFT START ----
        // BOOSTER.ks:721: centre engine alone for 0.75 s, then the outboards. Lighting three at once
        // into supersonic flow is the shock the stage does not need. Both halves are asserted -
        // testing only the settled value would let a soft start that never ends pass.
        LandingInputs entryOpen = heavy; entryOpen.PhaseElapsedS = 0.0;
        LandingInputs entrySettled = heavy; entrySettled.PhaseElapsedS = 1.0;
        Check("entry burn opens on the centre engine",
              Landing.EnginesFor(LandingPhase.EntryBurn, entryOpen) == 1,
              Landing.EnginesFor(LandingPhase.EntryBurn, entryOpen).ToString());
        Check("entry burn goes to three after the soft start",
              Landing.EnginesFor(LandingPhase.EntryBurn, entrySettled) == 3,
              Landing.EnginesFor(LandingPhase.EntryBurn, entrySettled).ToString());
        Check("the soft start is short enough to be a start, not a phase",
              Landing.EntrySoftStartS > 0.0 && Landing.EntrySoftStartS < 3.0, "");

        // ---- THE OCTAWEB'S MODES ARE NOT MULTIPLES OF ONE ENGINE ----
        // 2560 / 1706 / 764 kN for nine / three / one. Scaling the all-engine figure by an engine
        // COUNT overstates the one-engine landing burn by 2.2x, and that number sets the hoverslam
        // ignition altitude. When the vehicle reports its real modes, they must be used verbatim.
        LandingInputs octa = Fall(5000.0, -200.0, 200.0, 180.0, 9);
        octa.AccelThreeEngine = 60.0;
        octa.AccelOneEngine = 27.0;
        // The landing burn STARTS on three engines - Land() hands over to one only once the stage
        // is slow enough and provably able to finish. So a fast, high booster reads the three-engine
        // figure here, and only a slow low one reads the centre engine's.
        Check("the landing burn opens on the three-engine figure",
              Math.Abs(Landing.PhaseAccel(LandingPhase.LandingBurn, octa) - 60.0) < 1e-9,
              Landing.PhaseAccel(LandingPhase.LandingBurn, octa).ToString("F2"));
        LandingInputs handed = Fall(200.0, -30.0, 32.0, 180.0, 9);
        handed.AccelThreeEngine = 60.0; handed.AccelOneEngine = 27.0;
        Check("and after the handover, the centre engine's",
              Math.Abs(Landing.PhaseAccel(LandingPhase.LandingBurn, handed) - 27.0) < 1e-9,
              Landing.PhaseAccel(LandingPhase.LandingBurn, handed).ToString("F2"));
        Check("three-engine accel comes from the vehicle too",
              Math.Abs(Landing.PhaseAccel(LandingPhase.Boostback, octa) - 60.0) < 1e-9,
              Landing.PhaseAccel(LandingPhase.Boostback, octa).ToString("F2"));
        // A conventional cluster of identical engines has no discrete modes, and there the linear
        // estimate is exactly right - so it has to survive.
        LandingInputs plain = Fall(5000.0, -200.0, 200.0, 180.0, 9);
        Check("without discrete modes the linear estimate still applies",
              Math.Abs(Landing.PhaseAccel(LandingPhase.LandingBurn, plain) - 180.0 * 3.0 / 9.0)
              < 1e-9,
              Landing.PhaseAccel(LandingPhase.LandingBurn, plain).ToString("F2"));
        // ---- THE HANDOVER IS BOTH CONDITIONS, NOT A TWR CHECK ----
        // Land:805. "At low propellant this stage does not have TWR 1 on one Merlin, and a handover
        // that happens too early cannot be undone." Ours committed to one engine up front.
        LandingInputs fastLow = Fall(5000.0, -200.0, 200.0, 180.0, 9);
        fastLow.AccelThreeEngine = 60.0; fastLow.AccelOneEngine = 27.0;
        Check("still fast: the burn stays on three",
              Landing.EnginesFor(LandingPhase.LandingBurn, fastLow) == 3,
              Landing.EnginesFor(LandingPhase.LandingBurn, fastLow).ToString());
        LandingInputs slowHigh = Fall(200.0, -30.0, 32.0, 180.0, 9);
        slowHigh.AccelThreeEngine = 60.0; slowHigh.AccelOneEngine = 27.0;
        Check("slow, and one engine could still stop it: hand over",
              Landing.EnginesFor(LandingPhase.LandingBurn, slowHigh) == 1,
              Landing.EnginesFor(LandingPhase.LandingBurn, slowHigh).ToString());
        LandingInputs slowLow = Fall(45.0, -30.0, 32.0, 180.0, 9);
        slowLow.AccelThreeEngine = 60.0; slowLow.AccelOneEngine = 27.0;
        Check("slow but no room left: do NOT hand over",
              Landing.EnginesFor(LandingPhase.LandingBurn, slowLow) == 3,
              Landing.EnginesFor(LandingPhase.LandingBurn, slowLow).ToString());
        Check("a booster without the TWR takes three",
              Landing.EnginesFor(LandingPhase.LandingBurn, heavy) == 3,
              Landing.EnginesFor(LandingPhase.LandingBurn, heavy).ToString());

        // Solving on all nine and then lighting one is how a booster arrives still doing 200 m/s.
        double all = Landing.IgnitionAltitude(200.0, 180.0, 9.81);
        double one = Landing.IgnitionAltitude(200.0, 180.0 / 9.0, 9.81);
        Check("one engine must ignite far higher than nine", one > all * 5.0,
              one.ToString("F0") + " vs " + all.ToString("F0"));

        // Dropped from rest, 1000 m at 10 m/s^2: sqrt(2h/g) = 14.142 s.
        double t = Landing.TimeToGround(1000.0, 0.0, 10.0);
        Check("ballistic fall time", Math.Abs(t - 14.142) < 0.01, t.ToString("F3"));

        // ---- F9I'S CONSTANTS, PINNED SO A "TIDY-UP" CANNOT QUIETLY RETUNE A FLOWN VEHICLE ----
        Check("entry gate is 32 500 m ASL", Landing.EntryBurnGateAsl == 32500.0, "");
        Check("entry cut is -300 m/s vertical", Landing.EntryBurnCutVs == -300.0, "");
        Check("booster height is 31.02 m", Math.Abs(Landing.BoosterHeightM - 31.02) < 0.001, "");
        Check("bulk margin 6%", Math.Abs(Landing.BulkMargin - 0.06) < 1e-9, "");
        Check("flare is 34% in the last 25 m",
              Math.Abs(Landing.FlareMargin - 0.34) < 1e-9 && Landing.FlareRadarM == 25.0, "");
        Check("one-engine ratio is the cfg number 2.23",
              Math.Abs(Landing.OneEngineRatio - 2.23) < 1e-9, "");

        // ---- THE ENGINES FLY OUT LESS HEIGHT THAN THE RADAR REPORTS ----
        // Forgetting BoosterHeight lands the stage 31 m underground.
        Check("true radar subtracts the booster height",
              Math.Abs(Landing.TrueRadar(Fall(131.02, -10, 10, 90, 9)) - 100.0) < 0.01,
              Landing.TrueRadar(Fall(131.02, -10, 10, 90, 9)).ToString("F2"));
        Check("true radar never goes negative",
              Landing.TrueRadar(Fall(5.0, -1, 1, 90, 9)) == 0.0, "");

        // ---- THE RATIO IS THE WHOLE BURN ----
        // At exactly the stopping distance the throttle must read 1: any less and it is too late.
        LandingInputs onCurve = Fall(1031.02, -100.0, 100.0, 0.0, 1);
        onCurve.MaxThrustAccel = 5.0 + 9.81;          // decel of exactly 5 m/s^2
        // v^2/2a = 10000/10 = 1000 m, and TrueRadar is 1000 m.
        Check("throttle is 1.0 exactly on the curve",
              Math.Abs(Landing.BurnThrottle(onCurve, onCurve.MaxThrustAccel) - 1.0) < 1e-6,
              Landing.BurnThrottle(onCurve, onCurve.MaxThrustAccel).ToString("F4"));
        LandingInputs high = Fall(2031.02, -100.0, 100.0, 0.0, 1);
        high.MaxThrustAccel = 5.0 + 9.81;
        Check("higher up needs less throttle",
              Landing.BurnThrottle(high, high.MaxThrustAccel) < 1.0, "");
        Check("too late reads above 1",
              Landing.BurnThrottle(Fall(531.02, -100.0, 100.0, 14.81, 1), 14.81) > 1.0, "");

        // ---- THE HANDOVER IS GUARDED BOTH WAYS ----
        // F9I: "a handover that happens too early cannot be undone."
        Check("no handover while still fast",
              !Landing.HandoverReady(Fall(500.0, -80.0, 80.0, 90.0, 9), 90.0), "");
        Check("handover when slow and provably able",
              Landing.HandoverReady(Fall(500.0, -10.0, 10.0, 90.0, 9), 90.0), "");

        // ---- THE SEQUENCE ----
        LandingCommand c = Landing.Guide(Fall(30000.0, -400.0, 900.0, 90.0, 9), LandingPhase.Coast);
        Check("entry burn triggers at the gate", c.Phase == LandingPhase.EntryBurn, c.Phase.ToString());
        Check("entry burn is full throttle", c.Throttle == 1.0, c.Throttle.ToString());
        c = Landing.Guide(Fall(30000.0, -250.0, 900.0, 90.0, 9), LandingPhase.EntryBurn);
        Check("entry burn cuts at -300 m/s", c.Phase == LandingPhase.Descent, c.Phase.ToString());
        c = Landing.Guide(Fall(1.0, -0.5, 0.5, 90.0, 9), LandingPhase.LandingBurn);
        Check("touchdown ends it", c.Phase == LandingPhase.Touchdown, c.Phase.ToString());
        Check("touchdown cuts the throttle", c.Throttle == 0.0, c.Throttle.ToString());
        c = Landing.Guide(Fall(150.0, -30.0, 30.0, 90.0, 9), LandingPhase.LandingBurn);
        Check("legs out below 200 m", c.DeployLegs, "");

        // ---- ⛔ THE AoA SIGN FLIPS WHEN THE ENGINES LIGHT. THIS IS THE TRAP. ----
        // Unpowered the lean works AERODYNAMICALLY and a positive angle walks the impact the right
        // way. Under thrust the force is along the NOSE, so the same lean pushes the OPPOSITE way -
        // the guidance would drive the error open instead of closed.
        Check("unpowered lean is POSITIVE", Landing.GuidanceAoaDeg(3000.0, false) > 0.0,
              Landing.GuidanceAoaDeg(3000.0, false).ToString("F2"));
        Check("powered lean is NEGATIVE", Landing.GuidanceAoaDeg(3000.0, true) < 0.0,
              Landing.GuidanceAoaDeg(3000.0, true).ToString("F2"));
        // The unpowered ceiling is 15 only ABOVE 4 km. Below it F9I follows alt:radar/100 - about
        // 40 deg at 4 km, and it keeps going all the way down. AtmGNC:754.
        Check("unpowered above 4 km is the 15 degree trim",
              Math.Abs(Landing.GuidanceAoaDeg(6000.0, false) - Landing.AeroAoaDeg) < 1e-9,
              Landing.GuidanceAoaDeg(6000.0, false).ToString("F2"));
        Check("and below 4 km the ceiling opens up to alt/100",
              Math.Abs(Landing.GuidanceAoaDeg(3000.0, false) - 30.0) < 1e-9,
              Landing.GuidanceAoaDeg(3000.0, false).ToString("F2"));

        // ---- ⛔ THERE IS NO 15 DEGREE FLOOR, AND THIS CHECK USED TO ASSERT ONE. ----
        // It read `GuidanceAoaDeg(500, false) >= AeroAoaDeg` and passed against a `max(15, alt/100)`
        // in the law. Both were mine. `BOOSTER.ks:755` is one statement with no clamp of any kind -
        //     set F9L_AOA to (alt:radar / 100).
        // - so the ceiling really does keep shrinking: 10 deg at 1 km, 1 deg at 100 m. The comment
        // that stood here said the taper decays "to 15 at 1500 m" and then treated 15 as the bottom;
        // 1500 m is simply where the curve happens to cross 15 on its way down.
        //
        // Flooring it held a fifteen-degree angle of attack on a stage in the last hundred metres of
        // a landing burn approach - the regime F9I deliberately gives almost no steering authority,
        // "at this point the only job is to arrive upright".
        Check("the taper keeps going below 15 - there is no floor",
              Math.Abs(Landing.GuidanceAoaDeg(1000.0, false) - 10.0) < 1e-9,
              Landing.GuidanceAoaDeg(1000.0, false).ToString("F2"));
        Check("...right down to 1 degree at 100 m",
              Math.Abs(Landing.GuidanceAoaDeg(100.0, false) - 1.0) < 1e-9,
              Landing.GuidanceAoaDeg(100.0, false).ToString("F2"));

        // Under power the authority TAPERS with height: 4 degrees down to 1, floor at 75 m, so the
        // stage stops steering and starts standing up as it arrives.
        Check("high up it uses full powered authority",
              Math.Abs(Landing.GuidanceAoaDeg(3000.0, true) - (-4.0)) < 1e-9,
              Landing.GuidanceAoaDeg(3000.0, true).ToString("F2"));
        Check("at 75 m it is down to the floor",
              Math.Abs(Landing.GuidanceAoaDeg(75.0, true) - (-1.0)) < 1e-9,
              Landing.GuidanceAoaDeg(75.0, true).ToString("F2"));
        Check("and on the deck it is still the floor, not zero",
              Math.Abs(Landing.GuidanceAoaDeg(0.0, true) - (-1.0)) < 1e-9, "");
        // Monotonic: authority only ever decreases as it descends under power.
        double prevA = -99.0;
        for (double h = 0.0; h <= 600.0; h += 5.0)
        {
            double aoa = Landing.GuidanceAoaDeg(h, true);
            Check("powered AoA within band at " + h, aoa >= -4.0 && aoa <= -1.0,
                  aoa.ToString("F3"));
            Check("authority never grows on the way down at " + h, aoa <= prevA + 1e-12 || h == 0.0,
                  aoa + " after " + prevA);
            prevA = aoa;
        }

        // ---- THE LEAN IS CLAMPED TO EXACTLY THE AoA, AND WINDS DOWN INSIDE 5 m ----
        double full = Landing.LeanFraction(500.0, 15.0);
        Check("a big error leans the full tan(AoA)",
              Math.Abs(full - Math.Tan(15.0 * Math.PI / 180.0)) < 1e-9, full.ToString("F5"));
        Check("inside the deadband the lean winds down",
              Landing.LeanFraction(2.5, 15.0) < full * 0.6, Landing.LeanFraction(2.5, 15.0).ToString("F5"));
        Check("on the pad there is no lean at all",
              Math.Abs(Landing.LeanFraction(0.0, 15.0)) < 1e-12, "");
        Check("a powered lean is signed the other way",
              Landing.LeanFraction(500.0, -3.0) < 0.0,
              Landing.LeanFraction(500.0, -3.0).ToString("F5"));

        // ================================================================================
        //  ⛔ THE CANCELLATION. This is the property the descent's stability rests on, and the
        //  three checks above could not catch it breaking: they call LeanFraction with the right
        //  argument, and the 2026-08-11 fault was the CALLER passing a different quantity.
        //
        //  As the impact error shrinks its DIRECTION becomes meaningless - a predicted impact
        //  point that jitters by a metre swings its azimuth arbitrarily, up to a full reversal.
        //  What keeps the command still is that the lean it is multiplied by shrinks at the same
        //  time. So the thing to test is the WORST-CASE COMMANDED SWING: how far the aim moves if
        //  the error direction reverses completely.
        //
        //      swing = 2 * atan(LeanFraction(error, aoa))
        //
        //  It must go to zero with the error. Scaled on downrange - which is never small - it does
        //  not: it sits at 2*atan(tan(15)) = 30 degrees all the way to touchdown, which is the
        //  measured 4-second limit cycle with actuation saturated 30% of the descent.
        // ================================================================================
        double swing500 = 2.0 * Math.Atan(Landing.LeanFraction(500.0, 15.0)) * 180.0 / Math.PI;
        double swing5 = 2.0 * Math.Atan(Landing.LeanFraction(5.0, 15.0)) * 180.0 / Math.PI;
        double swing1 = 2.0 * Math.Atan(Landing.LeanFraction(1.0, 15.0)) * 180.0 / Math.PI;
        double swing0 = 2.0 * Math.Atan(Landing.LeanFraction(0.05, 15.0)) * 180.0 / Math.PI;

        Check("a reversed direction on a LARGE error is allowed to swing the aim",
              swing500 > 25.0, swing500.ToString("F1") + " deg");
        Check("at the 5 m deadband edge the worst-case swing is already bounded",
              swing5 <= swing500 + 1e-9, swing5.ToString("F1") + " deg");
        Check("at 1 m of error a full reversal moves the aim under 7 deg",
              swing1 < 7.0, swing1.ToString("F1") + " deg");
        Check("at 5 cm it is under half a degree - the noisy azimuth cannot be felt",
              swing0 < 0.5, swing0.ToString("F3") + " deg");
        Check("and the swing is monotone in the error, so there is no worst case in between",
              swing0 < swing1 && swing1 < swing5 && swing5 <= swing500, "");

        // The counter-case, stated so the regression is unmistakable: feeding a downrange-like
        // number where the error belongs pins the swing at the full 30 degrees no matter how
        // perfectly the stage is tracking.
        double swingWrong = 2.0 * Math.Atan(Landing.LeanFraction(600.0, 15.0)) * 180.0 / Math.PI;
        Check("scaling on downrange would leave a 30 deg swing on a perfect track",
              swingWrong > 29.0 && swingWrong < 31.0, swingWrong.ToString("F1") + " deg");

        // ================================================================================
        //  ⛔ THE APPROACH MUST BE ABLE TO BRAKE. The cap belongs to the accelerate loop only.
        //  2026-08-12: 24.4 m/s closing at 528 m against a 5 m/s cap, because the gate refused to
        //  fire exactly when the correction was a braking one.
        // ================================================================================
        double fastClose = 24.4, atRange = 528.0;
        Check("while ACCELERATING the cap still blocks building more speed",
              !DirectApproach.Burn(5.0, 1.0, fastClose, atRange, true), "");
        Check("while COASTING the same state is allowed to burn - that burn is the brake",
              DirectApproach.Burn(5.0, 1.0, fastClose, atRange, false), "");
        Check("a tiny correction is still not worth a burn in either phase",
              !DirectApproach.Burn(0.0, 1.0, fastClose, atRange, false), "");
        Check("and neither is one we are not pointed at",
              !DirectApproach.Burn(5.0, 90.0, fastClose, atRange, false), "");
        Check("under the cap, accelerating burns normally",
              DirectApproach.Burn(5.0, 1.0, 0.5, atRange, true), "");

        // ---- THE LANDING ROLL REFERENCE IS SKIPPED NEAR THE lookdirup SINGULARITY ----
        Check("a vertical stage over the pad does not take the horizontal roll reference",
              5.0 < Landing.RollRefMinDeg, "");
        Check("...nor one pointed nearly opposite it",
              170.0 > Landing.RollRefMaxDeg, "");
        Check("but a leaning stage does", 45.0 >= Landing.RollRefMinDeg
              && 45.0 <= Landing.RollRefMaxDeg, "");
    }

    // ------------------------------------------------------------------ rendezvous

    static ApproachInputs Gap(double range, double closing, double peri)
    {
        ApproachInputs a = new ApproachInputs();
        a.Valid = true; a.HasTarget = true;
        a.RangeM = range; a.ClosingMps = closing; a.PeriapsisM = peri;
        a.PeriodS = 1900.0; a.TargetPeriodS = 1900.0;
        return a;
    }

    static void Approach()
    {
        // ---- THE LADDER, EXACTLY AS RECORDED ----
        Check("beyond 3 km is phasing",
              Rendezvous.Classify(Gap(5000.0, 0, 86000.0)) == ApproachRung.Phasing, "");
        Check("3 km to 500 m is CW",
              Rendezvous.Classify(Gap(1500.0, 0, 86000.0)) == ApproachRung.Clohessy, "");
        Check("inside 500 m is RCS",
              Rendezvous.Classify(Gap(200.0, 0, 86000.0)) == ApproachRung.Rcs, "");
        Check("inside 50 m is final",
              Rendezvous.Classify(Gap(20.0, 0, 86000.0)) == ApproachRung.Final, "");
        Check("no target is idle",
              Rendezvous.Classify(new ApproachInputs()) == ApproachRung.Idle, "");

        // ---- ⛔ THE PERIAPSIS FLOOR. THIS IS THE FLIGHT-012 GUARD. ----
        Check("floor is 75 km", Rendezvous.PeriapsisFloorM == 75000.0,
              Rendezvous.PeriapsisFloorM.ToString());
        ApproachCommand c = Rendezvous.Guide(Gap(2000.0, 5.0, 60000.0));
        Check("below the floor the approach HOLDS", c.FloorViolated, "");
        Check("below the floor it commands no closing", c.TargetClosingMps == 0.0,
              c.TargetClosingMps.ToString());
        c = Rendezvous.Guide(Gap(2000.0, 5.0, 86000.0));
        Check("above the floor it proceeds", !c.FloorViolated, "");
        Check("above the floor it wants to close", c.TargetClosingMps > 0.0, "");

        // ---- THE CORRIDOR ----
        // Capped, never negative, crawling at contact. A controller allowed to close at 50 m/s from
        // 500 m cannot stop, and cannot-stop at a crewed station has no recovery.
        for (double r = 20.0; r <= 3000.0; r += 20.0)
        {
            double vv = Rendezvous.CorridorRate(r);
            Check("corridor capped at " + r, vv <= 12.0, vv.ToString("F2"));
            Check("corridor non-negative at " + r, vv >= 0.0, vv.ToString("F2"));
        }
        Check("contact speed is a crawl", Rendezvous.CorridorRate(5.0) < 0.3,
              Rendezvous.CorridorRate(5.0).ToString("F2"));
        Check("far means faster",
              Rendezvous.CorridorRate(2000.0) > Rendezvous.CorridorRate(200.0), "");
    }

    // ------------------------------------------------------------------ entry

    static EntryInputs Down(double alt)
    {
        EntryInputs e = new EntryInputs();
        e.Valid = true; e.AltitudeM = alt; e.SurfaceSpeed = 2000.0; e.VerticalSpeed = -200.0;
        return e;
    }

    static void Reentry()
    {
        // ---- ⛔ RETROGRADE ON EVERY BAND. THIS COST CargoDragon_012. ----
        for (double alt = 0; alt <= 80000.0; alt += 2500.0)
            Check("heat shield leads at " + alt, Entry.Guide(Down(alt)).Retrograde, "");

        // ---- THE MEASURED SCHEDULE, NOT A DERIVED ONE ----
        // aoaRetro from bb_dragon_CrewDragon_072 (⛔ NOT IN OUR ARCHIVE - the numbers are quoted from F9I's own comment at dragon_deorbit.ks:38-44, which IS verifiable; the recording behind them is not), binned by altitude. A flight that landed 6.3 km out.
        Check("interface is pure retrograde", Entry.AngleFor(EntryBand.Interface) == 0.0, "");
        Check("high band is full trim", Math.Abs(Entry.AngleFor(EntryBand.High) - 15.00) < 0.001,
              Entry.AngleFor(EntryBand.High).ToString("F3"));
        Check("low band bleeds off", Math.Abs(Entry.AngleFor(EntryBand.Low) - 8.25) < 0.001,
              Entry.AngleFor(EntryBand.Low).ToString("F3"));
        Check("final is nearly retrograde", Math.Abs(Entry.AngleFor(EntryBand.Final) - 1.95) < 0.001,
              Entry.AngleFor(EntryBand.Final).ToString("F3"));

        Check("above 70 km is coast", Entry.Guide(Down(75000.0)).Band == EntryBand.Coast, "");
        Check("60 km is the interface", Entry.Guide(Down(60000.0)).Band == EntryBand.Interface, "");
        Check("40 km is the lifting phase", Entry.Guide(Down(40000.0)).Band == EntryBand.High, "");
        Check("20 km is lift bleeding off", Entry.Guide(Down(20000.0)).Band == EntryBand.Low, "");
        Check("8 km is terminal", Entry.Guide(Down(8000.0)).Band == EntryBand.Final, "");

        // ---- CHUTES AT THE REAL ALTITUDES: 18 000 ft and 6 000 ft ----
        Check("drogues at 18 000 ft", Math.Abs(Entry.DrogueAltitude - 5486.0) < 1.0, "");
        Check("mains at 6 000 ft", Math.Abs(Entry.MainAltitude - 1830.0) < 1.0, "");
        Check("drogues fire below 5486 m", Entry.Guide(Down(5000.0)).DeployDrogues, "");
        Check("drogues do NOT fire at 20 km", !Entry.Guide(Down(20000.0)).DeployDrogues, "");

        EntryInputs dr = Down(1500.0); dr.DroguesOut = true;
        Check("mains fire below 1830 m under drogues", Entry.Guide(dr).DeployMains, "");
        EntryInputs dr2 = Down(4000.0); dr2.DroguesOut = true;
        Check("mains wait above 1830 m", !Entry.Guide(dr2).DeployMains, "");

        // Chutes outrank the aerodynamic schedule: a band that says "hold 1.95 deg" at 4 km while
        // the vehicle needs a canopy is worse than useless.
        EntryInputs sp = Down(0.0); sp.Splashed = true;
        Check("splashdown ends it", Entry.Guide(sp).Band == EntryBand.Splashdown, "");
    }

    // ------------------------------------------------------------------ deorbit and station ops

    static void Return()
    {
        // ---- THE FITTED CONSTANTS. THE SOURCE RECORDS THE MISS EACH ONE PRODUCED. ----
        Check("S2 crew aim is the 159 m fit", Deorbit.AimS2Crew == 286000.0, "");
        Check("S2 cargo aim is the 331 m fit", Deorbit.AimS2Cargo == 315450.0, "");
        // ⛔ THE AIM IS NOT A MONOTONIC LEVER - see Deorbit.AimDracoCrew. 221500 -> 47 km short,
        // 256000 -> 260 km short (a LONGER aim burned deeper and landed SHORTER). Reverted to 221500,
        // the least-bad known, pending the closed-loop bank-entry rework that replaces this constant
        // as the range lever.
        Check("Draco crew aim reverted to 221500 pending closed-loop entry", Deorbit.AimDracoCrew == 221500.0, "");

        // ⛔ THE OLD TEST CLAIMED "the shallow Draco entry needs the LONGER aim (> S2)". MEASURED FALSE.
        // The aim is where the de-orbit puts the impact and the entry then SHORTENS to the LZ. A
        // shallow entry has LITTLE shorten authority, so it needs LESS overshoot to reel back - a
        // SHORTER aim, not longer. At 295400 (longer than the S2's 286000) the entry saturated and
        // overshot 49.5 km; the measured fit puts the Draco aim BELOW the S2's, which is what the
        // vehicle actually needs. The periapsis ordering is unchanged and correct: the Draco entry
        // really is shallower (-31.8 vs -40.8 km).
        Check("the shallow Draco entry (low shorten authority) needs the SHORTER aim",
              Deorbit.AimDracoCrew < Deorbit.AimS2Crew
              && Deorbit.PeriapsisTargetDraco > Deorbit.PeriapsisTargetS2,
              Deorbit.AimDracoCrew.ToString("F0") + " vs " + Deorbit.AimS2Crew.ToString("F0"));

        // The altitude scaling: a higher orbit carries more energy through the interface and needs
        // a longer aim. Nothing to fit against yet - see AimRange - but the SIGN must be right.
        DeorbitInputs hi = new DeorbitInputs();
        hi.Valid = true; hi.OnDraco = true; hi.Crewed = true;
        hi.OrbitAltM = 120000.0;
        DeorbitInputs fit = hi; fit.OrbitAltM = Deorbit.AimFitAltM;
        Check("a higher orbit aims longer", Deorbit.AimRange(hi) > Deorbit.AimRange(fit),
              Deorbit.AimRange(hi).ToString("F0") + " vs " + Deorbit.AimRange(fit).ToString("F0"));
        Check("...and the fitted altitude is unchanged by the scaling",
              Math.Abs(Deorbit.AimRange(fit) - Deorbit.AimDracoCrew) < 1.0, "");
        Check("...by a few percent, not a rewrite",
              Deorbit.AimRange(hi) / Deorbit.AimRange(fit) < 1.10,
              (Deorbit.AimRange(hi) / Deorbit.AimRange(fit)).ToString("F4"));
        Check("S2 periapsis target", Deorbit.PeriapsisTargetS2 == -40800.0, "");
        Check("Draco aims the entry directly", Deorbit.PeriapsisTargetDraco == -31800.0, "");
        Check("landing-calibrated orbit", StationOps.DeorbitApM == 85100.0
              && StationOps.DeorbitPeM == 79200.0, "");

        // Draco has no trim authority, so it must aim SHALLOWER than the S2 does.
        Check("Draco periapsis is shallower than S2's",
              Deorbit.PeriapsisTargetDraco > Deorbit.PeriapsisTargetS2, "");

        // ---- ⛔ VARIABLE THRUST. NOTHING HERE IS BANG-BANG. ----
        Check("no error means no throttle", Deorbit.BurnThrottle(0.0, 0.0) == 0.0, "");
        Check("a big error saturates at the ceiling, not at 1.0",
              Math.Abs(Deorbit.BurnThrottle(500000.0, 500000.0) - Deorbit.ThrottleMax) < 1e-9,
              Deorbit.BurnThrottle(500000.0, 500000.0).ToString("F3"));
        Check("the ceiling is 0.70 so the burn stays shortenable",
              Deorbit.ThrottleMax == 0.70, "");
        Check("a tiny error still gets the floor",
              Deorbit.BurnThrottle(1.0, 0.0) >= Deorbit.ThrottleMin, "");
        // Monotonic in the error, which is what makes it a controller rather than a table.
        double last = -1.0;
        for (double e = 0.0; e <= 40000.0; e += 500.0)
        {
            double t = Deorbit.BurnThrottle(e, 0.0);
            Check("deorbit throttle never decreases with error at " + e, t >= last - 1e-9,
                  t.ToString("F4") + " after " + last.ToString("F4"));
            last = t;
        }

        // The sqrt trims: strong while far, tapering hard so the capsule stops chasing overshoot.
        Check("coarse trim caps at 0.60", Deorbit.TrimThrottle(1e9, true) <= 0.60, "");
        Check("fine trim caps at 0.35", Deorbit.TrimThrottle(1e9, false) <= 0.35, "");
        Check("fine trim is gentler than coarse at the same miss",
              Deorbit.TrimThrottle(50000.0, false) < Deorbit.TrimThrottle(50000.0, true), "");
        Check("trim tapers - half the miss is more than half the throttle",
              Deorbit.TrimThrottle(50000.0, true) > Deorbit.TrimThrottle(100000.0, true) * 0.5, "");

        // ---- THE LANDING FLOOR FADES OUT INSTEAD OF HOLDING 5% INTO THE GROUND ----
        Check("landing floor at height", Deorbit.LandingThrottle(100.0, 0.0) == 0.05,
              Deorbit.LandingThrottle(100.0, 0.0).ToString("F4"));
        Check("landing floor fades at touchdown", Deorbit.LandingThrottle(1.0, 0.0) < 0.05,
              Deorbit.LandingThrottle(1.0, 0.0).ToString("F4"));
        Check("landing throttle caps at 1", Deorbit.LandingThrottle(10.0, 99999.0) == 1.0, "");

        // ---- NEVER LIGHT AN ENGINE AT THE PORT ----
        // Not a rendezvous error - a collision.
        Check("no burn inside the safe distance", !StationOps.SafeToBurn(50.0), "");
        Check("burn allowed outside it", StationOps.SafeToBurn(500.0), "");
        Check("safe distance is 150 m", StationOps.SafeDistanceM == 150.0, "");
        Check("docking handover at 300 m", StationOps.DockHandoverM == 300.0, "");

        // The reserve depends on HOW we land - propulsive needs four times the chute reserve.
        DeorbitInputs prop = new DeorbitInputs(); prop.Valid = true; prop.Crewed = true;
        DeorbitInputs chute = prop; chute.ChuteLanding = true;
        Check("propulsive reserve is 50 units", Deorbit.MonoReserve(prop) == 50.0, "");
        Check("chute reserve is 12 units", Deorbit.MonoReserve(chute) == 12.0, "");

        // ---- THE SEQUENCE ----
        DeorbitInputs d = new DeorbitInputs();
        d.Valid = true; d.Crewed = true;
        d.PeriapsisM = 79200.0; d.PredictedRangeM = 0.0;
        DeorbitCommand c = Deorbit.Guide(d, DeorbitPhase.Idle);
        Check("starts burning", c.Phase == DeorbitPhase.Burn, c.Phase.ToString());
        Check("burn has real throttle", c.Throttle > 0.0 && c.Throttle <= Deorbit.ThrottleMax,
              c.Throttle.ToString("F3"));

        d.PeriapsisM = -45000.0; d.PredictedRangeM = 300000.0;
        c = Deorbit.Guide(d, DeorbitPhase.Burn);
        Check("hands to trim when both targets are met", c.Phase == DeorbitPhase.Trim,
              c.Phase.ToString());
        Check("trim uses RCS, not the engines", c.Throttle == 0.0, c.Throttle.ToString());

        d.PredictedRangeM = Deorbit.AimS2Crew; d.CrossTrackM = 100.0;
        c = Deorbit.Guide(d, DeorbitPhase.Trim);
        Check("hands to entry once trimmed", c.Phase == DeorbitPhase.Entry, c.Phase.ToString());
    }
}
