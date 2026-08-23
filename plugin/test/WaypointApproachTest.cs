/*
 * Tests for the Crew Dragon L-approach: WP0 (400 m below) -> WP1 (220 m ahead) -> WP2 (20 m), each a
 * hold, inside a 200 m keep-out sphere with a V-bar docking corridor. LVLH frame: +radial up, +along
 * ahead. See pure/WaypointApproach.cs.
 */
using System;
using DragonScreen;

public static class WaypointApproachTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    static WpInputs At(double radial, double along, double cross, double range)
    {
        WpInputs s = new WpInputs();
        s.Valid = true; s.HasTarget = true;
        s.RadialM = radial; s.AlongM = along; s.CrossM = cross;
        s.RangeM = range;
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen L-approach (waypoint) tests");

        // ---- the published waypoints ----
        double r, a, c;
        WaypointApproach.Waypoint(WpPhase.ToWP0, out r, out a, out c);
        Check("WP0 is 400 m below on the R-bar", Math.Abs(r + 400.0) < 1e-9 && Math.Abs(a) < 1e-9, r + "/" + a);
        WaypointApproach.Waypoint(WpPhase.ToWP1, out r, out a, out c);
        Check("WP1 is 220 m ahead on the V-bar", Math.Abs(a - 220.0) < 1e-9 && Math.Abs(r) < 1e-9, r + "/" + a);
        WaypointApproach.Waypoint(WpPhase.ToWP2, out r, out a, out c);
        Check("WP2 is 20 m ahead", Math.Abs(a - 20.0) < 1e-9, a.ToString());

        // ---- keep-out sphere: breach inside 200 m off-corridor, OK on the V-bar corridor ----
        Check("100 m directly below the station is a keep-out breach",
              WaypointApproach.KeepOutBreached(At(-100.0, 0.0, 0.0, 100.0)), "");
        Check("100 m ahead on the docking axis is NOT a breach (on the corridor)",
              !WaypointApproach.KeepOutBreached(At(0.0, 100.0, 0.0, 100.0)), "");
        Check("400 m below (WP0) is outside the keep-out sphere",
              !WaypointApproach.KeepOutBreached(At(-400.0, 0.0, 0.0, 400.0)), "");
        Check("20 m ahead (WP2) is on the corridor, not a breach",
              !WaypointApproach.KeepOutBreached(At(0.0, 20.0, 0.0, 20.0)), "");

        // ---- a to-WP0 leg commands velocity TOWARD WP0 (downward, since we start above it) ----
        WpInputs far = At(0.0, 0.0, 0.0, 400.0);   // at the station's level, WP0 is 400 m below
        WpCommand cmd = WaypointApproach.Guide(far, WpPhase.ToWP0);
        Check("heading to WP0 commands a DOWNWARD (negative radial) rate",
              cmd.CmdRadialRateMps < 0.0, cmd.CmdRadialRateMps.ToString("F2"));
        Check("...and not yet arrived", !cmd.Arrived, "");

        // ---- arrival: parked on WP0 with low rate ----
        WpInputs onWp0 = At(-400.0, 0.0, 0.0, 400.0);
        onWp0.RadialRateMps = 0.05;
        WpCommand arr = WaypointApproach.Guide(onWp0, WpPhase.ToWP0);
        Check("parked on WP0 at low speed reads ARRIVED", arr.Arrived, "");
        Check("StepPhase advances an arrived ToWP0 into Hold0",
              WaypointApproach.StepPhase(onWp0, WpPhase.ToWP0, arr) == WpPhase.Hold0, "");

        // ---- a hold commands zero approach speed and auto-GOes after the hold time ----
        WpInputs holding = onWp0; holding.HoldElapsedS = 3.0;
        WpCommand h = WaypointApproach.Guide(holding, WpPhase.Hold0);
        Check("a hold that has not timed out stays put (no GO)",
              WaypointApproach.StepPhase(holding, WpPhase.Hold0, h) == WpPhase.Hold0, "");
        WpInputs held = onWp0; held.HoldElapsedS = WaypointApproach.HoldS + 1.0;
        Check("a hold past the hold time GOes onto the fly-around (ToArc1)",
              WaypointApproach.StepPhase(held, WpPhase.Hold0,
                  WaypointApproach.Guide(held, WpPhase.Hold0)) == WpPhase.ToArc1, "");
        WpInputs crewGo = onWp0; crewGo.Go = true;
        Check("a crew GO leaves the hold early (onto ToArc1)",
              WaypointApproach.StepPhase(crewGo, WpPhase.Hold0,
                  WaypointApproach.Guide(crewGo, WpPhase.Hold0)) == WpPhase.ToArc1, "");

        // ---- the fly-around: WP0 -> ToArc1 -> ToArc2 -> ToWP1, arc points fly THROUGH (Passed, not settled) ----
        double ar, aa;
        WaypointApproach.ArcPoint(1.0 / 3.0, out ar, out aa);
        WpInputs onArc1 = At(ar, aa, 0.0, Math.Sqrt(ar * ar + aa * aa));
        onArc1.AlongRateMps = 3.0;   // still MOVING through the arc point
        WpCommand ac1 = WaypointApproach.Guide(onArc1, WpPhase.ToArc1);
        Check("moving through an arc point reads Passed but NOT Arrived (settled)",
              ac1.Passed && !ac1.Arrived, ac1.Passed + "/" + ac1.Arrived);
        Check("ToArc1 advances to ToArc2 on Passed (does not stop)",
              WaypointApproach.StepPhase(onArc1, WpPhase.ToArc1, ac1) == WpPhase.ToArc2, "");
        WaypointApproach.ArcPoint(2.0 / 3.0, out ar, out aa);
        WpInputs onArc2 = At(ar, aa, 0.0, Math.Sqrt(ar * ar + aa * aa));
        onArc2.AlongRateMps = 3.0;
        Check("ToArc2 advances to ToWP1 on Passed",
              WaypointApproach.StepPhase(onArc2, WpPhase.ToArc2,
                  WaypointApproach.Guide(onArc2, WpPhase.ToArc2)) == WpPhase.ToWP1, "");

        // ---- ⛔ THE WHOLE POINT: the fly-around never breaches the keep-out sphere. ----
        // Sweep the arc densely and the CHORDS between successive samples; the straight WP0->WP1 chord
        // grazed ~193 m, this must never drop below the 200 m sphere off-corridor.
        double minRange = double.MaxValue;
        double pr = 0.0, pa = 0.0;
        bool anyBreach = false;
        for (int i = 0; i <= 200; i++)
        {
            double s = i / 200.0;
            double rr, aaa;
            WaypointApproach.ArcPoint(s, out rr, out aaa);
            double rng = Math.Sqrt(rr * rr + aaa * aaa);
            if (rng < minRange) minRange = rng;
            if (WaypointApproach.KeepOutBreached(At(rr, aaa, 0.0, rng))) anyBreach = true;
            if (i > 0)
            {
                // sample the chord to the previous point too (the flown path is piecewise-linear)
                for (int k = 1; k < 8; k++)
                {
                    double t = k / 8.0;
                    double cr = pr + (rr - pr) * t;
                    double ca = pa + (aaa - pa) * t;
                    double crng = Math.Sqrt(cr * cr + ca * ca);
                    if (crng < minRange) minRange = crng;
                    if (WaypointApproach.KeepOutBreached(At(cr, ca, 0.0, crng))) anyBreach = true;
                }
            }
            pr = rr; pa = aaa;
        }
        Check("the fly-around arc never breaches the keep-out sphere", !anyBreach, "min range " + minRange.ToString("F1"));
        Check("...and stays at radius >= 200 m throughout (>= WP1's 220 m in fact)",
              minRange >= 200.0, "min range " + minRange.ToString("F1"));
        // And prove the OLD straight chord WOULD have breached, so the test has teeth.
        double sr = double.MaxValue;
        for (int i = 0; i <= 200; i++)
        {
            double t = i / 200.0;
            double cr = WaypointApproach.WP0RadialM * (1 - t) + 0.0 * t;       // WP0 radial -> 0
            double ca = 0.0 * (1 - t) + WaypointApproach.WP1AlongM * t;         // 0 -> WP1 along
            double rng = Math.Sqrt(cr * cr + ca * ca);
            if (rng < sr) sr = rng;
        }
        Check("(sanity) the straight WP0->WP1 chord would have grazed inside 200 m",
              sr < 200.0, "straight-chord min " + sr.ToString("F1"));

        // ---- the full sequence walks the holds WP0 -> WP1 -> WP2 -> Handover ----
        Check("Hold1 GO -> ToWP2",
              WaypointApproach.StepPhase(held, WpPhase.Hold1,
                  WaypointApproach.Guide(held, WpPhase.Hold1)) == WpPhase.ToWP2, "");
        Check("Hold2 GO -> Handover (docking)",
              WaypointApproach.StepPhase(held, WpPhase.Hold2,
                  WaypointApproach.Guide(held, WpPhase.Hold2)) == WpPhase.Handover, "");

        // ---- a breach anywhere aborts ----
        WpInputs breach = At(-100.0, 0.0, 0.0, 100.0);     // 100 m below, off-corridor, inside KOS
        WpCommand bc = WaypointApproach.Guide(breach, WpPhase.ToWP1);
        Check("a keep-out breach commands ABORT", bc.Phase == WpPhase.Abort && bc.KeepOutBreach, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
