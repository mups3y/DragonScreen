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
        Approach();
        Reentry();
        Return();
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
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

        // ---- ⛔ NOTHING LIGHTS WHILE THE TWO VEHICLES ARE STILL ALONGSIDE ----
        // 23:19 flight: the booster lit three engines at full throttle 11.4 m from the upper stage,
        // the vertical gap went NEGATIVE - they passed through each other - and the booster came
        // apart, 59.20 t down to 9.40 t. The 3 s coast bought no clearance because there is almost
        // no separation impulse; the gap was still ~11 m when it expired. Distance is the condition.
        LandingInputs alongside = Fall(29000.0, 700.0, 780.0, 43.0, 9);
        alongside.RangeToPartnerM = 11.4;
        Check("a booster still alongside starts in SEPARATING",
              Landing.InitialPhase(alongside) == LandingPhase.Separating, "");
        LandingCommand sepc = Landing.Guide(alongside, LandingPhase.Separating);
        Check("and it burns nothing", Math.Abs(sepc.Throttle) < 1e-9, sepc.Throttle.ToString("F3"));
        Check("and it stays there while it is close",
              sepc.Phase == LandingPhase.Separating, sepc.Phase.ToString());
        // ⛔ AND IT DOES NOT ROTATE. The stage is climbing at 700 m/s, so surface retrograde is very
        // nearly straight down - a 180-degree flip with the upper stage 11 m away. A 40 m booster
        // cannot rotate through that without hitting it.
        Check("a booster alongside holds its attitude, it does not flip",
              sepc.Aim == LandingAim.Hold, sepc.Aim.ToString());

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

        LandingInputs clear = Fall(29000.0, 700.0, 780.0, 43.0, 9);
        clear.RangeToPartnerM = Landing.SafeSeparationM + 1.0;
        Check("clear of the stage, the boostback begins",
              Landing.Guide(clear, LandingPhase.Separating).Phase == LandingPhase.Boostback, "");

        // A stage that never drifts clear must not hold attitude all the way to the ground.
        LandingInputs stuck = Fall(29000.0, 700.0, 780.0, 43.0, 9);
        stuck.RangeToPartnerM = 11.4; stuck.PhaseElapsedS = Landing.MaxSeparationWaitS + 1.0;
        Check("but it does not wait for ever",
              Landing.Guide(stuck, LandingPhase.Separating).Phase == LandingPhase.Boostback, "");

        // No partner at all - a solo booster test - must not be gated on a range it cannot measure.
        LandingInputs solo = Fall(29000.0, 700.0, 780.0, 43.0, 9);
        solo.RangeToPartnerM = 0.0;
        Check("an unmeasurable range is not a reason to sit still",
              Landing.InitialPhase(solo) != LandingPhase.Separating, "");

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
        Check("one-engine accel comes from the vehicle, not from a division",
              Math.Abs(Landing.PhaseAccel(LandingPhase.LandingBurn, octa) - 27.0) < 1e-9,
              Landing.PhaseAccel(LandingPhase.LandingBurn, octa).ToString("F2"));
        Check("three-engine accel comes from the vehicle too",
              Math.Abs(Landing.PhaseAccel(LandingPhase.Boostback, octa) - 60.0) < 1e-9,
              Landing.PhaseAccel(LandingPhase.Boostback, octa).ToString("F2"));
        // A conventional cluster of identical engines has no discrete modes, and there the linear
        // estimate is exactly right - so it has to survive.
        LandingInputs plain = Fall(5000.0, -200.0, 200.0, 180.0, 9);
        Check("without discrete modes the linear estimate still applies",
              Math.Abs(Landing.PhaseAccel(LandingPhase.LandingBurn, plain) - 180.0 / 9.0) < 1e-9,
              Landing.PhaseAccel(LandingPhase.LandingBurn, plain).ToString("F2"));
        Check("a light booster lands on one",
              Landing.EnginesFor(LandingPhase.LandingBurn, Fall(5000.0, -200.0, 200.0, 180.0, 9)) == 1, "");
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
        Check("unpowered is the 15 degree trim",
              Math.Abs(Landing.GuidanceAoaDeg(3000.0, false) - 15.0) < 1e-9, "");

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
        // aoaRetro from bb_dragon_CrewDragon_072, binned by altitude. A flight that landed 6.3 km out.
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
        Check("Draco crew aim is flight 076's", Deorbit.AimDracoCrew == 270700.0, "");
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
