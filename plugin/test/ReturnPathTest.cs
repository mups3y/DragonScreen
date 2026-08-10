/*
 * DragonScreen headless tests - the return path: overflight, phase-down, terminal descent, and the
 * along/cross split the entry loop is steered by.
 *
 * Every check here is a flight F9I already paid for:
 *   070   the landing site was evaluated at the OVERFLIGHT and not at TOUCHDOWN - 55 km cross-track
 *   072/074  the lag was DERIVED rather than measured, and the correction went through zero and out
 *   053   the obvious cross-track formula manufactured 1 222 m of phantom offset from a 0.22 deg
 *         bearing rotation, and the yaw loop steered to that false null
 *   the first station return  phase-down burns planned with no engine lit, so a node executor with
 *         no thrust shoved on RCS and raised the orbit instead of lowering it
 */
using System;
using DragonScreen;

public static class ReturnPathTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // Kerbin. `kerbin-degree-to-metres`: a degree here is 10 472 m, not Earth's 111 320.
    const double R = 600000.0;
    const double Mu = 3.5316000e12;
    const double RotationS = 21549.425;

    public static int Run()
    {
        Console.WriteLine("DragonScreen return-path tests");
        Flips();
        Overflights();
        PhaseDown();
        TerminalDescent();
        AlongAndCross();
        EntryActuation();
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // ================================================================== the turnaround

    /// <summary>
    /// ⛔ THE ONE INVARIANT THIS FILE WAS ADDED FOR, AFTER THE 08:40 BOOSTER WAS LOST:
    /// **a 180-degree RTLS flip must finish pointing where the boostback begins.**
    ///
    /// It did not. The flip built its tangent from PROgrade where F9I uses `srfretrograde`, so it
    /// finished flat prograde - exactly reversed - and `BoostbackKill` inherited a 149.7 deg attitude
    /// error at the instant three Merlins hit full throttle. The stage tumbled at 0.85 rad/s, burned
    /// 24 tonnes, drove itself 15 km FURTHER downrange during the burn meant to reverse it, ran dry at
    /// 11 km and was destroyed.
    ///
    /// The bug survived a line-by-line audit of the whole booster phase because the arithmetic lived
    /// inside a method that takes a `Vessel` and could not be tested. That is why it is pure now.
    /// </summary>
    static void Flips()
    {
        double rx, ry, rz, ax, ay, az, fx, fy, fz;

        // Flying due +X, "up" is +Z. Flat retrograde is therefore -X.
        bool ok = FlipGeometry.Solve(0, 0, 1, 500, 0, 0, 180.0,
                                     out rx, out ry, out rz, out ax, out ay, out az,
                                     out fx, out fy, out fz);
        Check("the flip geometry solves for a vehicle with a ground track", ok, "");
        Check("flat retrograde is the reverse of the ground track",
              Math.Abs(rx + 1.0) < 1e-9 && Math.Abs(ry) < 1e-9 && Math.Abs(rz) < 1e-9,
              rx.ToString("F3") + "," + ry.ToString("F3") + "," + rz.ToString("F3"));

        // THE CHECK. Had this existed, the flight would not have been lost.
        Check("a 180 deg RTLS flip finishes FLAT RETROGRADE - where BoostbackKill starts",
              FlipGeometry.AngleDeg(fx, fy, fz, rx, ry, rz) < 1e-6,
              FlipGeometry.AngleDeg(fx, fy, fz, rx, ry, rz).ToString("F1") + " deg off");
        Check("...and NOT prograde, which is what it used to do",
              FlipGeometry.AngleDeg(fx, fy, fz, -rx, -ry, -rz) > 179.0, "");

        // The axis is perpendicular to both the track and the vertical, so the nose swings through
        // the PLANE OF FLIGHT and the stage never yaws sideways.
        Check("the rotation axis is perpendicular to the ground track",
              Math.Abs(ax * rx + ay * ry + az * rz) < 1e-9, "");
        Check("and to the vertical", Math.Abs(az) < 1e-9, az.ToString("F6"));

        // A droneship only turns back far enough to trim, so it finishes 10 deg SHORT of retrograde -
        // short, not long, and on the retrograde side.
        FlipGeometry.Solve(0, 0, 1, 500, 0, 0, 170.0,
                           out rx, out ry, out rz, out ax, out ay, out az, out fx, out fy, out fz);
        double off = FlipGeometry.AngleDeg(fx, fy, fz, rx, ry, rz);
        Check("a 170 deg droneship flip finishes 10 deg off retrograde, on the retrograde side",
              Math.Abs(off - 10.0) < 1e-6, off.ToString("F2"));

        // The invariant must hold for any attitude, not just the axis-aligned one that happens to be
        // easy to reason about - an inclined, climbing booster is the real case.
        double[] ups = { 0.0, 0.3, -0.6 };
        bool all = true;
        for (int i = 0; i < ups.Length; i++)
        {
            double ux = ups[i], uy = 0.2, uz = 1.0;
            double n = Math.Sqrt(ux * ux + uy * uy + uz * uz);
            FlipGeometry.Solve(ux / n, uy / n, uz / n, 400, 250, 120, 180.0,
                               out rx, out ry, out rz, out ax, out ay, out az,
                               out fx, out fy, out fz);
            if (FlipGeometry.AngleDeg(fx, fy, fz, rx, ry, rz) > 1e-6) all = false;
        }
        Check("and it holds for a climbing booster on an inclined track too", all, "");

        // Straight up has no ground track to reverse, and saying so beats returning a garbage axis.
        Check("a vertical climb has no flip geometry and admits it",
              !FlipGeometry.Solve(0, 0, 1, 0, 0, 900, 180.0,
                                  out rx, out ry, out rz, out ax, out ay, out az,
                                  out fx, out fy, out fz), "");
    }

    // ================================================================== E3 / E4

    static void Overflights()
    {
        // ---- THE LANDING LAG ----
        // At our own orbit the calibrated fraction lands on F9I's measured 164 s. That agreement is
        // the whole reason to trust it, and it is worth pinning: the value was fitted at these
        // periods, so a lag far from 164 s means the caller is asking about a different orbit.
        double sma = R + 82150.0;                     // the 85.1 x 79.2 landing orbit
        double period = 2.0 * Math.PI * Math.Sqrt(sma * sma * sma / Mu);
        double lag = Overflight.LandLagS(period);
        Check("the landing lag at the landing orbit is F9I's measured ~164 s",
              lag > 150.0 && lag < 190.0, lag.ToString("F1"));

        // ⛔ AND IT IS NOT THE DERIVED ONE. dgPhaseFrac (0.255) is the burn's own phasing fraction and
        // reads like the right number; using it gives ~366 s, and flights 072/074 flew that and threw
        // the cross-track from +53 km to −64 km. The two must stay far apart or the file has been
        // "tidied" back into the bug.
        double derived = Overflight.DescentTimeS - (0.255 * period);
        Check("and it is NOT the plausible-looking derived one",
              Math.Abs(derived - lag) > 100.0, derived.ToString("F1"));

        // ---- THE GROUND MOVES UNDER US ----
        Check("with no elapsed time the track miss is just the great-circle distance",
              Math.Abs(Overflight.TrackMissM(R, RotationS, 0.0, 0.0, 0.0, 0.0, 1.0)
                       - Orbital.GroundRange(R, 0, 0, 0, 1)) < 1e-6, "");

        // A quarter of a rotation puts a sub-satellite point that is overhead NOW a quarter of the way
        // round from where the ground will be.
        double quarter = Overflight.TrackMissM(R, RotationS, 0.0, 0.0, RotationS / 4.0, 0.0, 0.0);
        Check("a quarter rotation of lag is a quarter circumference of miss",
              Math.Abs(quarter - (Math.PI / 2.0 * R)) < 1.0, quarter.ToString("F1"));

        Check("the site's own longitude walks forward with time",
              Math.Abs(Overflight.SiteLonAtDeg(0.0, RotationS / 4.0, RotationS) - 90.0) < 1e-9, "");
        Check("and longitude stays folded into -180..180",
              Overflight.SiteLonAtDeg(170.0, RotationS / 2.0, RotationS) < 0.0, "");

        // ---- OFF-PLANE ----
        Check("a site on the equator is in a polar-normal plane",
              Math.Abs(Overflight.OffPlaneDeg(90.0, 0.0, 0.0, 0.0)) < 1e-9, "");
        Check("and 10 deg off the normal's equator is 10 deg out of plane",
              Math.Abs(Overflight.OffPlaneDeg(90.0, 0.0, 10.0, 0.0) - 10.0) < 1e-9, "");
        Check("which is a real cross-track distance",
              Math.Abs(Overflight.CrossTrackFromOffPlaneM(10.0, R) - (10.0 * Math.PI / 180.0 * R))
                  < 1e-6, "");

        // ---- THE SEARCH ----
        // A V with its minimum at a time no coarse sample lands on. If the refinement is wrong, the
        // answer comes back on a 60 s grid and the de-orbit fires up to 45 km of ground track early.
        searchTarget = 1234.5;
        OverflightResult r = Overflight.Search(0.0, 1900.0, new TrackMissAtUt(Vee));
        Check("the search finds a minimum that no coarse sample sits on",
              r.Ok && Math.Abs(r.Ut - searchTarget) < 0.25, r.Ut.ToString("F2"));
        Check("and reports how far away it is", Math.Abs(r.InS - r.Ut) < 1e-9, "");

        // The refinement must not search backwards past now - a pass in the past is not a pass.
        searchTarget = 5.0;
        OverflightResult early = Overflight.Search(1000.0, 1900.0, new TrackMissAtUt(Vee));
        Check("and never returns a time before now", early.Ut >= 1000.0 - 1e-9,
              early.Ut.ToString("F2"));

        OverflightResult none = Overflight.Search(0.0, 0.0, new TrackMissAtUt(Vee));
        Check("no orbit means no answer, and it says so", !none.Ok, none.Note);
    }

    static double searchTarget;
    static double Vee(double ut) { return Math.Abs(ut - searchTarget); }

    // ================================================================== E2

    static void PhaseDown()
    {
        Check("the landing orbit recognises itself",
              DeorbitOrbit.AlreadyOnOrbit(DeorbitOrbit.TargetApoapsisM,
                                          DeorbitOrbit.TargetPeriapsisM), "");

        // ⛔ THE STATION'S ORBIT IS NOT THE LANDING ORBIT, AND THAT IS THE POINT OF THE PHASE-DOWN.
        // `falcon-station-ferry`: the station was MEASURED at 86.8 x 85.8 km. Every aim constant in
        // pure/Deorbit.cs was fitted from 85.1 x 79.2. If this ever returns true the phase-down stops
        // running and the aims silently start describing an orbit the capsule is not on.
        Check("the station's measured orbit is NOT it", !DeorbitOrbit.AlreadyOnOrbit(86800, 85800), "");
        Check("nor is the right apoapsis with the wrong periapsis",
              !DeorbitOrbit.AlreadyOnOrbit(DeorbitOrbit.TargetApoapsisM, 85800), "");

        double smaStation = R + (86800.0 + 85800.0) / 2.0;
        PhaseDownBurn one = DeorbitOrbit.LowerPeriapsis(Mu, R, 86800.0, smaStation);
        Check("burn 1 is retrograde - it LOWERS the periapsis", one.DvMps < 0.0,
              one.DvMps.ToString("F2"));
        Check("and it happens at apoapsis", one.AtApoapsis, "");
        Check("it is needed from the station orbit", one.Needed, "");

        double smaAfterOne = ((R + 86800.0) + (R + DeorbitOrbit.TargetPeriapsisM)) / 2.0;
        PhaseDownBurn two = DeorbitOrbit.LowerApoapsis(Mu, R, DeorbitOrbit.TargetPeriapsisM,
                                                       smaAfterOne);
        Check("burn 2 is retrograde too", two.DvMps < 0.0, two.DvMps.ToString("F2"));
        Check("and it happens at periapsis", !two.AtApoapsis, "");

        double total = DeorbitOrbit.TotalDvMps(Mu, R, 86800.0, 85800.0, smaStation);
        Check("the whole phase-down is a handful of m/s, not a burn worth refusing",
              total > 1.0 && total < 40.0, total.ToString("F2"));

        // Already there: nothing to spend. A needless burn only spends the margin the landing needs.
        double smaTarget = R + (DeorbitOrbit.TargetApoapsisM + DeorbitOrbit.TargetPeriapsisM) / 2.0;
        PhaseDownBurn nil = DeorbitOrbit.LowerPeriapsis(Mu, R, DeorbitOrbit.TargetApoapsisM,
                                                        smaTarget);
        Check("on the orbit already, burn 1 is not needed", !nil.Needed, nil.DvMps.ToString("F3"));
    }

    // ================================================================== E8

    static void TerminalDescent()
    {
        string why;

        Check("nobody asked for propulsive, so it is chutes",
              Terminal.Choose(false, true, 200.0, out why) == LandingMethod.Parachute, why);

        Check("asked for, but there are no engines aboard",
              Terminal.Choose(true, false, 200.0, out why) == LandingMethod.Parachute, why);
        Check("and the crew are told WHICH reason", why.Contains("engines"), why);

        Check("asked for, engines aboard, but the tank is under the gate",
              Terminal.Choose(true, true, Terminal.MonoGateUnits - 1.0, out why)
                  == LandingMethod.Parachute, why);
        Check("and that reason names the propellant", why.Contains("mono"), why);

        Check("all three true is the only way to land on engines",
              Terminal.Choose(true, true, Terminal.MonoGateUnits, out why)
                  == LandingMethod.Propulsive, why);

        // ---- THE DROGUE WINDOW ----
        Check("fast and high, the drogues stay in",
              !Terminal.DrogueReady(2000.0, 6000.0, LandingMethod.Parachute), "");
        Check("slow enough, they come out",
              Terminal.DrogueReady(Terminal.DrogueMaxSpeedMps - 1.0, 6000.0,
                                   LandingMethod.Parachute), "");
        // ⚠ THE PROPULSIVE FLOOR IS HIGHER ON PURPOSE: everything after it - light the engines, prove
        // they lit, cut the chutes - has to fit before the landing burn is due.
        Check("at 4 800 m and still fast, a propulsive descent is already out of altitude to wait in",
              Terminal.DrogueReady(2000.0, 4800.0, LandingMethod.Propulsive), "");
        Check("...but a chute descent may still wait",
              !Terminal.DrogueReady(2000.0, 4800.0, LandingMethod.Parachute), "");

        // ---- THE SOLVE ----
        Check("no thrust never produces a zero or negative deceleration",
              Terminal.MaxDecelMps2(0.0, 8.0, 9.81) > 0.0, "");
        Check("stopping distance is v squared over 2a",
              Math.Abs(Terminal.StopDistanceM(-50.0, 20.0) - 62.5) < 1e-9, "");

        // ---- ⛔ THE INVARIANT THIS WHOLE FILE EXISTS FOR ----
        // The chutes are cut at the BURN gate. If that ever fired higher than the ARM gate, the capsule
        // would cut its parachutes before it had proved the engines would light - the one ordering the
        // propulsive path's entire safety argument rests on.
        bool ordered = true;
        for (double stop = 0.0; stop <= 2000.0; stop += 7.0)
        {
            double armAlt = Math.Max(Terminal.ArmFloorM, stop * Terminal.ArmFactor);
            double burnAlt = (stop * Terminal.BurnFactor) + Terminal.HeightOffsetM;
            if (burnAlt > armAlt) { ordered = false; break; }
        }
        Check("the burn gate is never reached before the arm gate, at any stopping distance",
              ordered, "");

        Check("the arm gate has a floor so it cannot be missed by a tiny solve",
              Terminal.ArmGate(Terminal.ArmFloorM - 1.0, 0.0), "");
        Check("and does not fire above it", !Terminal.ArmGate(Terminal.ArmFloorM + 1.0, 0.0), "");

        // ---- HOVER ----
        Check("a capsule that cannot lift itself asks for everything, not more than everything",
              Math.Abs(Terminal.HoverThrottle(8.0, 9.81, 10.0) - 1.0) < 1e-9, "");
        double hov = Terminal.HoverThrottle(8.0, 9.81, 200.0);
        Check("and one that can carries the 5% margin",
              Math.Abs(hov - (1.05 * 8.0 * 9.81 / 200.0)) < 1e-9, hov.ToString("F4"));
        Check("the handover waits until the descent is nearly arrested",
              !Terminal.HoverHandover(-20.0) && Terminal.HoverHandover(-1.0), "");

        Check("the landing throttle is the booster's ratio, not a second one",
              Math.Abs(Terminal.LandingThrottle(100.0, 40.0)
                       - Deorbit.LandingThrottle(100.0, 40.0)) < 1e-12, "");

        // The handover altitude is where the entry loop stops and this file starts. If they ever
        // disagree the capsule falls through the gap with nothing steering it.
        Check("terminal starts where entry guidance hands over",
              Terminal.HandoverAltM > Terminal.DrogueFloorPropulsiveM, "");
    }

    // ================================================================== the split

    static void AlongAndCross()
    {
        double along, cross, miss;

        Orbital.DownCross(R, 0, 0, 0, 1, 0, 1, out along, out cross, out miss);
        Check("an impact on the target is no miss at all",
              miss < 1e-6 && Math.Abs(along) < 1e-6 && Math.Abs(cross) < 1e-6,
              along.ToString("F3") + " / " + cross.ToString("F3"));

        // Ship at the origin heading east. Target at 1 deg, impact at 2 deg: we are LONG.
        Orbital.DownCross(R, 0, 0, 0, 2, 0, 1, out along, out cross, out miss);
        Check("an impact PAST the target reads NEGATIVE along-track - that is what 'long' means",
              along < 0.0, along.ToString("F1"));
        Check("and its magnitude is the miss", Math.Abs(Math.Abs(along) - miss) < 1.0, "");

        Orbital.DownCross(R, 0, 0, 0, 1, 0, 2, out along, out cross, out miss);
        Check("an impact SHORT of the target reads positive", along > 0.0, along.ToString("F1"));

        // ---- ⛔ THE 053 TRAP ----
        // Target and impact both on ONE great circle out of the ship. The true perpendicular offset is
        // exactly zero however far away they are. The obvious formula - miss x sin(bearing difference)
        // - measures the ship→impact bearing AT THE SHIP and applies it AT THE IMPACT, hundreds of
        // kilometres away, where the great circle has rotated under it. On flight 053 that invented
        // 1 222 m of cross-track out of 0.22 deg of rotation, and the yaw loop steered to it.
        double tLat, tLon, iLat, iLon;
        Orbital.OffsetLatLon(R, 0.0, 0.0, 45.0, 300000.0, out tLat, out tLon);
        Orbital.OffsetLatLon(R, 0.0, 0.0, 45.0, 900000.0, out iLat, out iLon);
        Orbital.DownCross(R, 0, 0, iLat, iLon, tLat, tLon, out along, out cross, out miss);

        Check("a target sitting exactly on the ground track has NO cross-track",
              Math.Abs(cross) < 1.0, cross.ToString("F1"));
        Check("and it still reads long, because the impact is past it", along < 0.0,
              along.ToString("F1"));

        double track = Orbital.Bearing(0, 0, iLat, iLon);
        double toTgt = Orbital.Bearing(iLat, iLon, tLat, tLon);
        double naive = miss * Math.Sin((toTgt - track) * Math.PI / 180.0);
        Check("...while the obvious formula invents kilometres of it from the same geometry",
              Math.Abs(naive) > 1000.0, naive.ToString("F1"));

        // A target genuinely off to one side must still be seen - the fix must not have flattened the
        // signal along with the bias.
        Orbital.DownCross(R, 0, 0, 0, 2, 1, 2, out along, out cross, out miss);
        Check("a real cross-track offset is still measured", Math.Abs(cross) > 5000.0,
              cross.ToString("F1"));
    }

    // ================================================================== E7 actuation

    static void EntryActuation()
    {
        Check("no command is exactly retrograde",
              Math.Abs(EntryGuidance.AoaCommandDeg(0.0, 0.0)) < 1e-12, "");
        Check("full shorten flies the capsule's trim angle",
              Math.Abs(EntryGuidance.AoaCommandDeg(-1.0, 0.0) - EntryGuidance.TrimAoaDeg) < 1e-12, "");
        Check("half a command flies half the angle",
              Math.Abs(EntryGuidance.AoaCommandDeg(-0.5, 0.0) - (EntryGuidance.TrimAoaDeg / 2.0))
                  < 1e-12, "");
        // ⚠ THE AoA IS A TRIM ANGLE, NOT A BUDGET TO OVERSPEND. Two saturated axes still fly 15 deg.
        Check("both axes saturated still flies the trim angle, not 21",
              Math.Abs(EntryGuidance.AoaCommandDeg(-1.0, 1.0) - EntryGuidance.TrimAoaDeg) < 1e-12,
              EntryGuidance.AoaCommandDeg(-1.0, 1.0).ToString("F3"));

        // ⛔ A DRAGON IS NOT A STARSHIP. Inheriting the 78 deg belly-forward profile threw both navball
        // markers most of a hemisphere off on CargoDragon_012 and cost the whole entry.
        Check("the trim angle is the capsule's 15 deg",
              Math.Abs(EntryGuidance.TrimAoaDeg - 15.0) < 1e-12, "");
        // Flight 001 established this sign by measurement. Flipping it inverts every lift command.
        Check("the pitch sign is the measured -1",
              Math.Abs(EntryGuidance.PitchSign + 1.0) < 1e-12, "");

        Check("there is nothing to steer with in vacuum",
              !EntryGuidance.CanSteer(0.0) && !EntryGuidance.CanSteer(EntryGuidance.QSteerKpa), "");
        Check("and there is once the air arrives",
              EntryGuidance.CanSteer(EntryGuidance.QSteerKpa * 2.0), "");

        // ---- THE DROP LATCH IS ONE-WAY ----
        EntryMemory m = new EntryMemory();
        EntryGuideInputs onLz = Sample(30000.0, -60000.0, 60000.0);
        onLz.MissM = 10.0;                                   // prediction already on the pad
        EntryGuidance.Update(onLz, ref m);
        Check("a prediction on the LZ drops the range loop", m.Dropped, "");

        EntryGuideInputs later = Sample(30000.0, -150000.0, 60000.0);
        later.MissM = 150000.0;                              // a big error arrives afterwards
        Check("and it stays dropped - a good approach is not thrown away",
              Math.Abs(EntryGuidance.Update(later, ref m).VerticalCmd) < 1e-12, "");

        EntryMemory low = new EntryMemory();
        EntryGuidance.Update(Sample(8000.0, -50000.0, 60000.0), ref low);
        Check("falling below the terminal altitude drops it too", low.Dropped, "");
        Check("and a metre of altitude noise cannot switch it back on",
              Math.Abs(EntryGuidance.Update(Sample(20000.0, -150000.0, 60000.0),
                                            ref low).VerticalCmd) < 1e-12, "");
    }

    static EntryGuideInputs Sample(double altM, double downErrM, double lzRangeM)
    {
        EntryGuideInputs s = new EntryGuideInputs();
        s.AltitudeM = altM;
        s.DownrangeErrM = downErrM;
        s.LzRangeM = lzRangeM;
        s.LzBearingDeg = 90.0;
        s.TrackBearingDeg = 90.0;
        s.MissM = Math.Abs(downErrM);
        s.DtS = 1.0;
        return s;
    }
}
