/*
 * DragonScreen - DockApproachTest
 *
 * ⛔ THE WHOLE APPROACH, FLOWN HEADLESS. THE TEST THAT SHOULD HAVE EXISTED FIRST.
 *
 * `DockControlTest.Converge` flies the servo for 40 000 steps and passes, and docking failed on
 * every flight for weeks, because it feeds the servo the distance STRAIGHT TO THE PORT. The real
 * code feeds it the distance to whichever waypoint the profile picked, and that is where every
 * failure has been.
 *
 * This flies the real `DockApproach.Select` and the real `DockControl.Solve` against a 3-D plant
 * in the station's own LVLH frame, over the REAL Crew Dragon profile:
 *
 *      behind and below  ->  WP0, 400 m BELOW, hold
 *                        ->  WP1, 220 m out on the docking axis, hold
 *                        ->  WP2, 20 m, hold      (this leg penetrates the 200 m KOS)
 *                        ->  contact and capture
 *
 * Reboots are the scarce resource on this project. This file is what stops them being spent
 * discovering arithmetic.
 */
using System;
using DragonScreen;

public static class DockApproachTest
{
    static int checks, failures;
    public static bool Trace;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL: " + what + "  [" + detail + "]"); }
    }

    // ---- a minimal 3-D vector, local to the test. `src/pure` deliberately has none. ----
    struct V
    {
        public double x, y, z;
        public V(double a, double b, double c) { x = a; y = b; z = c; }
        public static V operator +(V a, V b) { return new V(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static V operator -(V a, V b) { return new V(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static V operator *(V a, double k) { return new V(a.x * k, a.y * k, a.z * k); }
        public double Dot(V b) { return x * b.x + y * b.y + z * b.z; }
        public double Mag { get { return Math.Sqrt(x * x + y * y + z * z); } }
        public V Unit { get { double m = Mag; return m > 1e-9 ? this * (1.0 / m) : new V(0, 0, 0); } }
    }

    struct Run
    {
        public bool Captured;
        public double Seconds, MonoUnits, MinRangeM, WorstContactMps, MaxLateralInFinalM;
        public double LateralAtCaptureM, ClosingAtCaptureMps;
        public bool HeldWp0, HeldWp1, HeldWp2;
        public double DeepestKosBreachM;   // how far inside the KOS we got while OFF the corridor
        public string Ended;
    }

    // The station's LVLH frame. ALONG is the V-bar (direction of travel), RADIAL is the R-bar
    // (positive away from the planet), CROSS completes it. The port faces FORWARD along the V-bar,
    // which is IDA-2's arrangement on Node 2 forward.
    static readonly V ALONG = new V(1, 0, 0);
    static readonly V RADIAL = new V(0, 1, 0);
    static readonly V CROSS = new V(0, 0, 1);

    static Run Fly(V start, double stationRadiusM, double acquireM, double maxSeconds)
    {
        Run r = new Run();
        r.MinRangeM = double.MaxValue;
        r.Ended = "timeout";

        V axis = ALONG;                                  // the port's outward normal
        V port = axis * stationRadiusM;
        V wp0 = RADIAL * -DockApproach.Wp0BelowM;
        V wp1 = port + axis * DockApproach.Wp1AxialM;
        V wp2 = port + axis * DockApproach.Wp2AxialM;

        V pos = start, vel = new V(0, 0, 0);
        Pid pf = new Pid(), ps = new Pid(), pt = new Pid();
        DockStage reached = DockStage.ToGate;
        double holdStable = 0.0;
        const double dt = 0.05;
        int steps = (int)(maxSeconds / dt);

        // The capsule holds its nose on -axis throughout, so its frame is fixed.
        V nose = axis * -1.0, side = CROSS, top = RADIAL;

        for (int i = 0; i < steps; i++)
        {
            V toPort = port - pos;
            double axial = -toPort.Dot(axis);
            V lateralVec = toPort - axis * toPort.Dot(axis);
            double lateral = lateralVec.Mag;
            double range = toPort.Mag;
            if (range < r.MinRangeM) r.MinRangeM = range;

            DockApproachInputs ai = new DockApproachInputs();
            ai.Valid = true;
            ai.AxialM = axial;
            ai.LateralM = lateral;
            ai.RadialM = pos.Dot(RADIAL);
            ai.AlongM = pos.Dot(ALONG);
            ai.CrossM = pos.Dot(CROSS);
            ai.RelSpeedMps = vel.Mag;
            ai.HoldStableS = holdStable;
            ai.AcquireM = acquireM;
            ai.SafeM = stationRadiusM;
            ai.PathClear = true;
            ai.ToStandoffM = (wp1 - pos).Mag;
            ai.ToGateM = (wp0 - pos).Mag;

            // ---- THE KEEP OUT SPHERE MAY ONLY BE ENTERED ON THE CORRIDOR ----
            // "penetrate the keep out sphere in the assigned approach corridor". The WP1 -> WP2 leg
            // is that corridor; anywhere else inside 200 m is a violation and worth measuring.
            if (DockApproach.InsideKos(ai)
                && DockApproach.Rank(reached) < DockApproach.Rank(DockStage.HoldWp1))
            {
                double depth = DockApproach.KeepOutRadiusM - pos.Mag;
                if (depth > r.DeepestKosBreachM) r.DeepestKosBreachM = depth;
            }

            DockApproachResult sel = DockApproach.Select(ai, reached);
            if (DockApproach.Rank(sel.Stage) > DockApproach.Rank(reached)) reached = sel.Stage;
            if (sel.Stage == DockStage.HoldWp0) r.HeldWp0 = true;
            if (sel.Stage == DockStage.HoldWp1) r.HeldWp1 = true;
            if (sel.Stage == DockStage.HoldWp2) r.HeldWp2 = true;

            if (sel.Captured)
            {
                r.Captured = true; r.Ended = "captured"; r.Seconds = i * dt;
                r.WorstContactMps = Math.Max(r.WorstContactMps, vel.Mag);
                // ⛔ WHAT MATTERS AT CONTACT: how far OFF THE PORT CENTRE, and how fast.
                r.LateralAtCaptureM = lateral;
                r.ClosingAtCaptureMps = -vel.Dot(axis);
                return r;
            }

            V aim;
            switch (sel.Waypoint)
            {
                case DockWaypoint.Wp0:  aim = wp0; break;
                case DockWaypoint.Wp1:  aim = wp1; break;
                case DockWaypoint.Wp2:  aim = wp2; break;
                // Forward at WP0's depth, level with WP1. The corner, flown as two sides.
                case DockWaypoint.Wp1Transit:
                    aim = ALONG * (wp1.Dot(ALONG)) + RADIAL * (-DockApproach.Wp0BelowM);
                    break;
                case DockWaypoint.Port: aim = port; break;
                default:                aim = wp0; break;
            }

            // A hold accumulates only while we are inside the box AND slow.
            bool inBox = (aim - pos).Mag <= DockApproach.Wp1ToleranceM;
            holdStable = (inBox && vel.Mag <= DockApproach.HoldSpeedMps) ? holdStable + dt : 0.0;

            // ⚠ THE FINAL RUN IS AFTER WP2, NOT THE WHOLE OF `Axial`. `Axial` also covers the
            // WP1 -> WP2 transit, where a large lateral is the profile working, not a fault.
            if (DockApproach.Rank(reached) >= DockApproach.Rank(DockStage.HoldWp2)
                && lateral > r.MaxLateralInFinalM)
                r.MaxLateralInFinalM = lateral;

            V to = aim - pos;
            DockState st = new DockState();
            st.Valid = true;
            st.DistF = to.Dot(nose); st.DistS = to.Dot(side); st.DistT = to.Dot(top);
            // NOT negated - `VelF` is the CLOSING rate. See DockControlTest's own note: negating it
            // makes the loop positive-feedback and the capsule departs.
            st.VelF = vel.Dot(nose); st.VelS = vel.Dot(side); st.VelT = vel.Dot(top);
            st.SpeedCap = DockControl.SpeedCapFor(to.Mag);

            DockCommand c = DockControl.Solve(st, pf, ps, pt, dt);
            V acc = nose * (c.Fore * DockControl.RcsAccel)
                  + side * (c.Starboard * DockControl.RcsAccel)
                  + top * (c.Top * DockControl.RcsAccel);
            vel = vel + acc * dt;
            pos = pos + vel * dt;

            r.MonoUnits += (Math.Abs(c.Fore) + Math.Abs(c.Starboard) + Math.Abs(c.Top)) * dt * 0.9;
            r.Seconds = i * dt;

            if (Trace && i % 400 == 0)
                Console.WriteLine(string.Format(
                    "   t{0,6:F0} {1,-9} wp {2,-5} radial {3,8:F1} along {4,8:F1} ax {5,7:F1}"
                    + " lat {6,6:F1} v {7,5:F2} hold {8,4:F1}",
                    i * dt, sel.Stage, sel.Waypoint, ai.RadialM, ai.AlongM, axial, lateral,
                    vel.Mag, holdStable));

            if (pos.Mag > 20000.0) { r.Ended = "diverged"; return r; }
        }
        return r;
    }

    public static int Run_()
    {
        Console.WriteLine("DragonScreen docking approach tests");
        checks = failures = 0;

        const double stationR = 15.0, acquire = 0.25;

        // ---- 1. THE PROFILE'S GEOMETRY IS THE RESEARCHED ONE ----
        Check("WP0 is 400 m below", DockApproach.Wp0BelowM == 400.0, "");
        Check("WP1 is 220 m out on the axis", DockApproach.Wp1AxialM == 220.0, "");
        Check("WP2 is 20 m from the port", DockApproach.Wp2AxialM == 20.0, "");
        Check("the keep-out sphere is a 200 m RADIUS", DockApproach.KeepOutRadiusM == 200.0, "");
        Check("the approach ellipsoid is 2000 x 1000 m",
              DockApproach.AeAlongM == 2000.0 && DockApproach.AeCrossM == 1000.0, "");

        // ---- 2. THE FULL APPROACH, FROM BEHIND AND BELOW ----
        // Where Approach Initiation leaves the vehicle: trailing the station and beneath it.
        Trace = Environment.GetEnvironmentVariable("DOCKTRACE") == "1";
        Run full = Fly(new V(-900.0, -500.0, 0.0), stationR, acquire, 6000);
        Trace = false;

        Check("the full profile CAPTURES", full.Captured,
              full.Ended + ", closest " + full.MinRangeM.ToString("F2") + " m");
        Check("...holding at WP0 on the way", full.HeldWp0, "never held at WP0");
        Check("...and at WP1", full.HeldWp1, "never held at WP1");
        Check("...and at WP2", full.HeldWp2, "never held at WP2");
        Check("...entering the KOS only on the corridor", full.DeepestKosBreachM < 1.0,
              full.DeepestKosBreachM.ToString("F0") + " m inside the KOS off-corridor");
        Check("...arriving slowly", full.WorstContactMps < 0.5,
              full.WorstContactMps.ToString("F3") + " m/s");
        Check("CONTACT: lateral error at capture is within a real capture envelope",
              full.LateralAtCaptureM < 0.10,
              full.LateralAtCaptureM.ToString("F3") + " m off the port centre");
        Check("CONTACT: closing speed at capture is gentle",
              full.ClosingAtCaptureMps > 0.0 && full.ClosingAtCaptureMps < 0.20,
              full.ClosingAtCaptureMps.ToString("F3") + " m/s");
        Check("...lined up before the final run",
              full.MaxLateralInFinalM <= DockApproach.CorridorAbortM,
              full.MaxLateralInFinalM.ToString("F2") + " m");

        // ---- 3. THE HOLDS ARE REAL HOLDS ----
        DockApproachInputs at0 = new DockApproachInputs();
        at0.Valid = true; at0.RadialM = -400.0; at0.AlongM = 0.0; at0.CrossM = 0.0;
        at0.AxialM = 400.0; at0.LateralM = 400.0; at0.AcquireM = acquire; at0.SafeM = stationR;

        at0.RelSpeedMps = 2.0; at0.HoldStableS = 0.0;
        Check("arriving fast at WP0 does NOT release the next leg",
              DockApproach.Select(at0, DockStage.ToGate).Stage == DockStage.HoldWp0
              && DockApproach.Select(at0, DockStage.ToGate).Waypoint == DockWaypoint.Wp0,
              DockApproach.Select(at0, DockStage.ToGate).Note);

        at0.RelSpeedMps = 0.05; at0.HoldStableS = 10.0;
        Check("...and a settled hold DOES",
              DockApproach.Select(at0, DockStage.ToGate).Waypoint == DockWaypoint.Wp1,
              DockApproach.Select(at0, DockStage.ToGate).Note);

        at0.RelSpeedMps = 2.0; at0.HoldStableS = 0.0; at0.CrewGo = true;
        Check("...or a crew GO overrides the dwell",
              DockApproach.Select(at0, DockStage.ToGate).Waypoint == DockWaypoint.Wp1, "");

        // ---- 3b. ⛔ TRIM ALL THE WAY TO CONTACT. THE PORT IS MISSED OTHERWISE. ----
        // The failure this guards: arriving inside the axial capture range while still off the
        // port CENTRE. Capture used to be gated on `CorridorRadiusM` - a full metre - so the
        // guidance would stop thrusting a metre off-axis and hand over to magnets that cannot
        // reach. The ports never mate and the capsule drifts on into the station.
        DockApproachInputs close = new DockApproachInputs();
        close.Valid = true; close.AcquireM = 0.25; close.SafeM = stationR;
        close.AxialM = 0.20; close.LateralM = 0.50;         // in range axially, OFF CENTRE
        DockApproachResult off = DockApproach.Select(close, DockStage.HoldWp2);
        Check("off-centre inside the axial range is NOT a capture", !off.Captured,
              off.Note);
        Check("...and the final run keeps commanding the port so the trim continues",
              off.Waypoint == DockWaypoint.Port, off.Waypoint.ToString());

        close.LateralM = 0.10;                               // now inside the port's envelope
        Check("...and inside the port's own envelope it IS a capture",
              DockApproach.Select(close, DockStage.HoldWp2).Captured, "");

        // The servo must SLOW the closure while a lateral error remains at close range - that is
        // what stops the capsule arriving before it is lined up.
        Pid tf = new Pid(), ts = new Pid(), tt = new Pid();
        DockState crab = new DockState();
        // ⚠ THE GEOMETRY MATTERS. At 3 m axial with 1.2 m lateral the lateral nulls FIRST - 8 s
        // against 20 s - so slowing would be wrong and the controller correctly does not. The case
        // that needs slowing is CLOSE AND OFF-CENTRE: about to arrive before being lined up.
        crab.Valid = true; crab.DistF = 0.5; crab.DistS = 1.2; crab.DistT = 0.0;
        crab.SpeedCap = DockControl.SpeedCapFor(1.3);
        DockCommand cc = DockControl.Solve(crab, tf, ts, tt, 0.05);
        Check("a crabbed final approach slows the closure", cc.Balanced,
              "not balanced - the capsule would arrive off-centre");
        Check("...while still commanding lateral trim", Math.Abs(cc.Starboard) > 0.01,
              cc.Starboard.ToString("F3"));

        // ---- 4. THE STAGE MACHINE IS STILL MONOTONE ----
        int last = 0; bool monotone = true;
        DockStage[] order = { DockStage.ToGate, DockStage.HoldWp0, DockStage.Corridor,
                              DockStage.HoldWp1, DockStage.Axial, DockStage.HoldWp2,
                              DockStage.Docked };
        foreach (DockStage st in order)
        {
            if (DockApproach.Rank(st) <= last) monotone = false;
            last = DockApproach.Rank(st);
        }
        Check("the profile's stages rank in flight order", monotone, "");

        // ---- 5. THE ZONE TESTS ----
        DockApproachInputs z = new DockApproachInputs();
        z.Valid = true; z.AlongM = 1500.0; z.RadialM = 0.0; z.CrossM = 0.0;
        Check("1500 m along-track is INSIDE the approach ellipsoid", DockApproach.InsideAe(z), "");
        z.AlongM = 2500.0;
        Check("2500 m is outside it", !DockApproach.InsideAe(z), "");
        z.AlongM = 150.0;
        Check("150 m from the station is inside the keep-out sphere", DockApproach.InsideKos(z), "");
        z.AlongM = 250.0;
        Check("250 m is outside it", !DockApproach.InsideKos(z), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
