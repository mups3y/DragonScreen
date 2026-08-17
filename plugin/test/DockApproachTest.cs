/*
 * DragonScreen - DockApproachTest
 *
 * ⛔ THE WHOLE APPROACH, FLOWN HEADLESS, FROM WHERE THE RENDEZVOUS ACTUALLY HANDS OVER.
 *
 * `DockControlTest.Converge` flies the servo for 40 000 steps and passes, and docking failed on
 * every flight for weeks, because it feeds the servo the distance STRAIGHT TO THE PORT. The real
 * code feeds it the distance to whichever waypoint the profile picked, and that is where every
 * failure has been.
 *
 * ⛔ AND THE START POINT MATTERS AS MUCH AS THE LOGIC. The previous version of this test began at
 * `V(-900, -500, 0)` - 900 m behind and 500 m below - to exercise the LVLH "L" profile, and it
 * PASSED while the L emptied the tank in flight (flight_0814_172345.csv). The reason was the
 * handover: `StationApproach.Arrived()` engages docking at ~60 m, co-orbital, IN FRONT of the port -
 * not behind and below. The L kept routing a near-port capsule back out to a point 400 m below and
 * the RCS slammed both ways until the tank was dry. This test now starts where the rendezvous really
 * leaves the vehicle, and flies the SIMPLIFIED gate -> standoff -> axial profile:
 *
 *      ~60 m in front (+ lateral)  ->  gate  ->  standoff, lined up  ->  axial run  ->  capture
 *
 * It uses the real `DockApproach.Select` and the real `DockControl.Solve` against a 3-D plant whose
 * geometry is computed exactly as `DockingOps.Tick` computes it - gate from `DockGeometry`, path
 * clearance from `DockGeometry`, skirt from `DockGeometry` - so the thing under test is the thing
 * that flies. Reboots are the scarce resource; this is what stops them being spent on arithmetic.
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
        /// <summary>Component of this perpendicular to unit direction n. Mirrors Vector3d.Exclude.</summary>
        public V Exclude(V n) { return this - n * this.Dot(n); }
    }

    // The station's LVLH frame, purely for placing the start points. ALONG is the V-bar, the port's
    // outward normal; RADIAL and CROSS complete it. The port faces forward along the V-bar.
    static readonly V ALONG = new V(1, 0, 0);
    static readonly V RADIAL = new V(0, 1, 0);
    static readonly V CROSS = new V(0, 0, 1);

    struct Run
    {
        public bool Captured;
        public double Seconds, MonoUnits, MinRangeM;
        public double LateralAtCaptureM, ClosingAtCaptureMps;
        public double MaxDistFM, MinDistFM;
        public string Ended;
    }

    // The station's own bounding radius stands in for `keepOutR`; the port sits on the axis at that
    // radius, as an arm-tip berth does. The skirt built here is the same one `DockingOps.Skirt`
    // builds, so a blocked path rounds the hull identically.
    static V Skirt(V pos, V gate, double keepOutR)
    {
        V center = new V(0, 0, 0);
        V c = center - pos;
        V side = (gate - pos).Exclude(c.Unit);
        if (side.Mag < 1.0) side = CROSS;                 // gate dead behind the station; any perp
        return center + side.Unit * DockGeometry.SkirtRadiusM(keepOutR);
    }

    static Run Fly(V start, double keepOutR, double acquireM, double maxSeconds, string label)
    {
        Run r = new Run();
        r.MinRangeM = double.MaxValue;
        r.MinDistFM = double.MaxValue;
        r.Ended = "timeout";

        V center = new V(0, 0, 0);
        V axis = ALONG;                                   // the port's outward normal
        V port = axis * keepOutR;                         // arm-tip berth on the axis
        V standoff = port + axis * DockApproach.StandoffM;

        // The gate: where the port axis leaves the keep-out sphere, plus the pad. Computed exactly
        // as DockingOps does, so the test's gate is the flight code's gate.
        V cToCentre = center - port;                      // port -> station centre
        double gateD = DockGeometry.GateDistanceM(cToCentre.Dot(axis), cToCentre.Mag * cToCentre.Mag,
                                                  keepOutR);
        V gate = port + axis * gateD;

        V pos = start, vel = new V(0, 0, 0);
        Pid pf = new Pid(), ps = new Pid(), pt = new Pid();
        DockStage reached = DockStage.ToGate;

        // The capsule holds its nose on -axis throughout, so its frame is fixed.
        V nose = axis * -1.0, sideAx = CROSS, topAx = RADIAL;

        const double dt = 0.05;
        int steps = (int)(maxSeconds / dt);

        for (int i = 0; i < steps; i++)
        {
            V toPort = port - pos;
            double axial = -toPort.Dot(axis);
            V lateralVec = toPort - axis * toPort.Dot(axis);
            double lateral = lateralVec.Mag;
            double range = toPort.Mag;
            if (range < r.MinRangeM) r.MinRangeM = range;

            V toGate = gate - pos;
            V cs = center - pos;                          // us -> station centre
            bool clear = (toGate.Mag > 1e-3)
                         && DockGeometry.PathClear(cs.Mag, cs.Mag * cs.Mag,
                                                   cs.Dot(toGate.Unit), toGate.Mag, keepOutR);

            DockApproachInputs ai = new DockApproachInputs();
            ai.Valid = true;
            ai.AxialM = axial;
            ai.LateralM = lateral;
            ai.ToStandoffM = (standoff - pos).Mag;
            ai.ToGateM = toGate.Mag;
            ai.PathClear = clear;
            ai.AcquireM = acquireM;
            ai.SafeM = keepOutR;

            DockApproachResult sel = DockApproach.Select(ai, reached);
            if (DockApproach.Rank(sel.Stage) > DockApproach.Rank(reached)) reached = sel.Stage;

            if (sel.Captured)
            {
                r.Captured = true; r.Ended = "captured"; r.Seconds = i * dt;
                r.LateralAtCaptureM = lateral;
                r.ClosingAtCaptureMps = -vel.Dot(axis);   // + means closing on the port
                return r;
            }

            V aim;
            switch (sel.Waypoint)
            {
                case DockWaypoint.Port:     aim = port; break;
                case DockWaypoint.Standoff: aim = standoff; break;
                case DockWaypoint.Skirt:    aim = Skirt(pos, gate, keepOutR); break;
                default:                    aim = clear ? gate : Skirt(pos, gate, keepOutR); break;
            }

            V to = aim - pos;
            DockState st = new DockState();
            st.Valid = true;
            st.DistF = to.Dot(nose); st.DistS = to.Dot(sideAx); st.DistT = to.Dot(topAx);
            // NOT negated - `VelF` is the CLOSING rate. See DockControlTest's own note: negating it
            // makes the loop positive-feedback and the capsule departs.
            st.VelF = vel.Dot(nose); st.VelS = vel.Dot(sideAx); st.VelT = vel.Dot(topAx);
            st.SpeedCap = DockControl.SpeedCapFor(to.Mag);

            if (st.DistF > r.MaxDistFM) r.MaxDistFM = st.DistF;
            if (st.DistF < r.MinDistFM) r.MinDistFM = st.DistF;

            DockCommand c = DockControl.Solve(st, pf, ps, pt, dt);
            V acc = nose * (c.Fore * DockControl.RcsAccel)
                  + sideAx * (c.Starboard * DockControl.RcsAccel)
                  + topAx * (c.Top * DockControl.RcsAccel);
            vel = vel + acc * dt;
            pos = pos + vel * dt;

            r.MonoUnits += (Math.Abs(c.Fore) + Math.Abs(c.Starboard) + Math.Abs(c.Top)) * dt * 0.9;
            r.Seconds = i * dt;

            if (Trace && i % 400 == 0)
                Console.WriteLine(string.Format(
                    "   {0,-6} t{1,6:F0} {2,-8} wp {3,-8} ax {4,7:F1} lat {5,6:F1}"
                    + " rng {6,7:F1} v {7,5:F2} mono {8,5:F1}",
                    label, i * dt, sel.Stage, sel.Waypoint, axial, lateral, range,
                    vel.Mag, r.MonoUnits));

            if (pos.Mag > 20000.0) { r.Ended = "diverged"; return r; }
        }
        return r;
    }

    // ---- assert a whole approach: it captures, gently, on centre, and without a runaway. ----
    static void CheckApproach(string label, V start, double keepOutR, double acquire,
                              double maxSeconds, double monoBudget)
    {
        double startRange = (new V(keepOutR, 0, 0) - start).Mag;
        Run run = Fly(start, keepOutR, acquire, maxSeconds, label);

        Check(label + ": CAPTURES", run.Captured,
              run.Ended + ", closest " + run.MinRangeM.ToString("F2") + " m, "
              + run.MonoUnits.ToString("F1") + " units");
        // The number that proves the thrash is gone: the 2026-08-14 runaway spent ~54 units and ran
        // the tank dry doing nothing. A converging approach spends a fraction of that.
        Check(label + ": monopropellant under " + monoBudget.ToString("F0") + " units",
              run.MonoUnits < monoBudget, run.MonoUnits.ToString("F1") + " units");
        // No back-out to a distant waypoint. The L flung `DistF` to +456 m from 7 m out; a working
        // approach never aims at a point much further than where it started.
        Check(label + ": never routes back out to a distant waypoint",
              run.MaxDistFM < startRange + 40.0,
              "max DistF " + run.MaxDistFM.ToString("F0") + " m vs start " + startRange.ToString("F0"));
        if (run.Captured)
        {
            Check(label + ": lateral at capture within the port envelope",
                  run.LateralAtCaptureM < 0.10,
                  run.LateralAtCaptureM.ToString("F3") + " m off centre");
            Check(label + ": closing at capture is gentle",
                  run.ClosingAtCaptureMps > 0.0 && run.ClosingAtCaptureMps < 0.20,
                  run.ClosingAtCaptureMps.ToString("F3") + " m/s");
        }
    }

    public static int Run_()
    {
        Console.WriteLine("DragonScreen docking approach tests");
        checks = failures = 0;

        const double keepOutR = 30.0, acquire = 0.25;
        Trace = Environment.GetEnvironmentVariable("DOCKTRACE") == "1";

        // ---- 1. THE COMMIT GEOMETRY IS THE RESEARCHED ONE ----
        Check("commit tolerance is 1 m of lateral", DockApproach.CorridorRadiusM == 1.0, "");
        Check("the abort band is 2 m", DockApproach.CorridorAbortM == 2.0, "");
        Check("the standoff is 25 m out from the port", DockApproach.StandoffM == 25.0, "");

        // ---- 2. THE REAL HANDOVERS. Each is where the rendezvous can leave the vehicle. ----
        // The nominal one: ~60 m in front on the axis, 4 m off it. This is the state the old test
        // never used, and the state the L failed in.
        // The budgets are plant-proxy units, not game units; they are set to catch a THRASH - the
        // 2026-08-14 runaway saturated for 200 s and spent 1800+ proxy units here - while passing a
        // converging approach. What proves the fix is capture + no runaway + gentle contact, checked
        // separately in CheckApproach.
        CheckApproach("FRONT",     new V(keepOutR + 60.0, 4.0, 0.0),  keepOutR, acquire, 1500, 35.0);
        // Handed over abeam the station, so the direct path to the gate is blocked and it must round.
        CheckApproach("SIDE",      new V(0.0, keepOutR + 65.0, 0.0),  keepOutR, acquire, 2500, 85.0);
        // Handed over on the FAR side of the station: axial is negative, so it must round to the
        // front (via the gate/skirt logic) rather than reversing through the hull.
        CheckApproach("WRONGSIDE", new V(-(keepOutR + 50.0), 30.0, 0.0), keepOutR, acquire, 3000, 115.0);
        Trace = false;

        // ---- 3. ⛔ TRIM ALL THE WAY TO CONTACT. THE PORT IS MISSED OTHERWISE. ----
        // Arriving inside the axial capture range while still off the port CENTRE must NOT be a
        // capture - the guidance has to keep commanding the port so the trim continues, or it hands
        // over to magnets that cannot reach and the capsule drifts on into the station.
        DockApproachInputs close = new DockApproachInputs();
        close.Valid = true; close.AcquireM = 0.25; close.SafeM = keepOutR;
        close.AxialM = 0.20; close.LateralM = 0.50;         // in range axially, OFF CENTRE
        DockApproachResult off = DockApproach.Select(close, DockStage.Axial);
        Check("off-centre inside the axial range is NOT a capture", !off.Captured, off.Note);
        Check("...and the final run keeps commanding the port so the trim continues",
              off.Waypoint == DockWaypoint.Port, off.Waypoint.ToString());

        close.LateralM = 0.10;                               // now inside the port's envelope
        Check("...and inside the port's own envelope it IS a capture",
              DockApproach.Select(close, DockStage.Axial).Captured, "");

        // The servo must SLOW the closure while a lateral error remains at close range - that is
        // what stops the capsule arriving before it is lined up.
        Pid tf = new Pid(), ts = new Pid(), tt = new Pid();
        DockState crab = new DockState();
        // At 3 m axial with 1.2 m lateral the lateral nulls FIRST, so slowing would be wrong and the
        // controller correctly does not. The case that needs slowing is CLOSE AND OFF-CENTRE.
        crab.Valid = true; crab.DistF = 0.5; crab.DistS = 1.2; crab.DistT = 0.0;
        crab.SpeedCap = DockControl.SpeedCapFor(1.3);
        DockCommand cc = DockControl.Solve(crab, tf, ts, tt, 0.05);
        Check("a crabbed final approach slows the closure", cc.Balanced,
              "not balanced - the capsule would arrive off-centre");
        Check("...while still commanding lateral trim", Math.Abs(cc.Starboard) > 0.01,
              cc.Starboard.ToString("F3"));

        // ---- 4. THE STAGE MACHINE IS STILL MONOTONE ----
        int last = 0; bool monotone = true;
        DockStage[] order = { DockStage.ToGate, DockStage.Corridor, DockStage.Axial, DockStage.Docked };
        foreach (DockStage st in order)
        {
            if (DockApproach.Rank(st) <= last) monotone = false;
            last = DockApproach.Rank(st);
        }
        Check("the profile's stages rank in flight order", monotone, "");

        // ---- 5. THE 13 m STALL DOES NOT COME BACK ----
        // The old `AtStandoff` made the axial commit fail at 13 m from the port BECAUSE the run had
        // worked. Here a capsule lined up (lateral inside the corridor) and at/inside the standoff
        // commits to the final run regardless of how far along it is - it cannot self-falsify.
        DockApproachInputs lined = new DockApproachInputs();
        lined.Valid = true; lined.AcquireM = acquire; lined.SafeM = keepOutR;
        lined.AxialM = 13.0; lined.LateralM = 0.4;           // 13 m out, lined up - the old stall point
        lined.ToStandoffM = 12.0; lined.ToGateM = 5.0;
        Check("lined up at the old 13 m stall point commits to the axial run",
              DockApproach.Select(lined, DockStage.Corridor).Stage == DockStage.Axial
              && DockApproach.Select(lined, DockStage.Corridor).Waypoint == DockWaypoint.Port,
              DockApproach.Select(lined, DockStage.Corridor).Note);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
