/*
 * DragonScreen - DockApproachTest
 *
 * ⛔ THE WHOLE APPROACH, FLOWN HEADLESS. THE TEST THAT SHOULD HAVE EXISTED FIRST.
 *
 * `DockControlTest.Converge` flies the servo for 40 000 steps and passes, and docking has never
 * succeeded in flight, because it feeds the servo the distance STRAIGHT TO THE PORT. The real code
 * feeds it the distance to whichever waypoint the stage machine picked, and that is where every
 * failure has been.
 *
 * This flies the real `DockApproach.Select` AND the real `DockControl.Solve` against a 3-D plant:
 * keep-out sphere, gate, standoff, corridor, port. It reproduces the flown failures from the
 * recordings before it is allowed to certify a fix.
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

    /// <summary>What one simulated approach did.</summary>
    struct Run
    {
        public bool Captured;
        public double Seconds;
        public double MonoUnits;
        public double MinRangeM;
        public double WorstContactMps;
        public double MaxLateralInFinalM;
        public bool WentBehind;
        public string Ended;
    }

    /// <summary>
    /// Fly one approach. The station sits at the origin with its port at `port`, facing `axis`.
    ///
    /// The plant is deliberately simple - command in, acceleration out, integrate - because what
    /// is under test is the DECISION CHAIN, not KSP's physics. `DockControl.RcsAccel` is the same
    /// figure the flight code sizes its braking curve with, so the two cannot disagree.
    /// </summary>
    static Run Fly(V start, V startVel, V axis, double keepOutR, double acquireM, double safeM,
                   double maxSeconds)
    {
        Run r = new Run();
        r.MinRangeM = double.MaxValue;
        r.Ended = "timeout";

        V port = axis * keepOutR;            // the port sits on the hull, on its own axis
        V pos = start, vel = startVel;
        Pid pf = new Pid(), ps = new Pid(), pt = new Pid();
        DockStage reached = DockStage.ToGate;
        const double dt = 0.05;
        int steps = (int)(maxSeconds / dt);

        // The capsule's own axes. It holds its nose on -axis throughout, which is what
        // `FlyTo` commands, so the frame is fixed and the lateral axes are any two orthogonals.
        V nose = axis * -1.0;
        V outward = new V(0, 0, 0);
        bool haveOut = false;
        V refv = (Math.Abs(axis.x) < 0.9) ? new V(1, 0, 0) : new V(0, 1, 0);
        V side = new V(axis.y * refv.z - axis.z * refv.y,
                       axis.z * refv.x - axis.x * refv.z,
                       axis.x * refv.y - axis.y * refv.x).Unit;
        V top = new V(axis.y * side.z - axis.z * side.y,
                      axis.z * side.x - axis.x * side.z,
                      axis.x * side.y - axis.y * side.x).Unit;

        for (int i = 0; i < steps; i++)
        {
            V toPort = port - pos;
            double axial = toPort.Dot(axis) * -1.0;      // + when we are IN FRONT of the port
            V lateralVec = toPort - axis * toPort.Dot(axis);
            double lateral = lateralVec.Mag;
            double range = toPort.Mag;
            if (range < r.MinRangeM) r.MinRangeM = range;
            if (axial < 0.0) r.WentBehind = true;

            V standoff = port + axis * DockApproach.StandoffM;
            V gate = port + axis * DockGeometry.GateDistanceM(0.0, keepOutR * keepOutR, keepOutR);

            DockApproachInputs ai = new DockApproachInputs();
            ai.Valid = true;
            ai.AxialM = axial;
            ai.LateralM = lateral;
            ai.ToStandoffM = (standoff - pos).Mag;
            ai.ToGateM = (gate - pos).Mag;
            ai.PathClear = true;
            ai.AcquireM = acquireM;
            ai.SafeM = safeM;

            DockApproachResult sel = DockApproach.Select(ai, reached);
            if (sel.Waypoint != DockWaypoint.SideStep) haveOut = false;
            if (DockApproach.Rank(sel.Stage) > DockApproach.Rank(reached)) reached = sel.Stage;

            if (sel.Captured)
            {
                r.Captured = true;
                r.Ended = "captured";
                r.Seconds = i * dt;
                r.WorstContactMps = Math.Max(r.WorstContactMps, vel.Mag);
                return r;
            }

            V aim;
            switch (sel.Waypoint)
            {
                case DockWaypoint.Port:     aim = port; break;
                case DockWaypoint.Standoff: aim = standoff; break;
                case DockWaypoint.BackOut:  aim = standoff; break;
                case DockWaypoint.SideStep:
                    // ⛔ A FIXED POINT, NOT AN OFFSET FROM WHERE WE ARE. `pos + lateral*safe` is
                    // recomputed every tick and therefore recedes for ever - the capsule chased it
                    // backwards past the station. This is the point at OUR OWN axial station, out
                    // at the safe radius: stepping off the axis without drifting further behind.
                    //
                    // ⛔ AND THE OUTWARD DIRECTION IS LATCHED. Taken from the live lateral vector it
                    // FLIPS every time the capsule crosses the axis, so the aim reverses and the
                    // capsule oscillates about the axis for ever - measured here at 55.00 m for the
                    // whole 1800 s run, lateral sawing between 0.20 and 1.92 m. "Which way is out"
                    // is a decision made once per sidestep, not a measurement taken every tick.
                    if (!haveOut)
                    {
                        outward = (lateral > 0.05) ? lateralVec.Unit : side;
                        haveOut = true;
                    }
                    aim = (pos - lateralVec) + outward * (safeM + 2.0);
                    break;
                default:                    aim = gate; break;
            }

            if (sel.Stage == DockStage.Axial && lateral > r.MaxLateralInFinalM)
                r.MaxLateralInFinalM = lateral;

            V to = aim - pos;
            DockState st = new DockState();
            st.Valid = true;
            st.DistF = to.Dot(nose);
            st.DistS = to.Dot(side);
            st.DistT = to.Dot(top);
            // ⛔ NOT NEGATED. `VelF` is the CLOSING rate - positive means the gap is shrinking -
            // and `nose` already points at the aim. DockControlTest's own comment records what the
            // negation does: "made the loop positive-feedback and the simulated capsule departed to
            // 474 km". I wrote it here anyway and the trace showed cmdF pinned at +0.50 while DistF
            // read -23.89, which is that failure exactly.
            st.VelF = vel.Dot(nose);
            st.VelS = vel.Dot(side);
            st.VelT = vel.Dot(top);
            st.SpeedCap = DockControl.SpeedCapFor(range);

            DockCommand c = DockControl.Solve(st, pf, ps, pt, dt);

            V acc = nose * (c.Fore * DockControl.RcsAccel)
                  + side * (c.Starboard * DockControl.RcsAccel)
                  + top * (c.Top * DockControl.RcsAccel);
            vel = vel + acc * dt;
            pos = pos + vel * dt;

            // Monopropellant, on the same basis the ledger reads it: thruster-seconds. The
            // constant only has to be consistent to compare one approach against another.
            r.MonoUnits += (Math.Abs(c.Fore) + Math.Abs(c.Starboard) + Math.Abs(c.Top))
                         * dt * 0.9;
            r.Seconds = i * dt;
            if (Trace && i % 40 == 0)
                Console.WriteLine(string.Format(
                    "   t{0,6:F1} stage {1,-9} wp {2,-9} ax {3,7:F2} lat {4,6:F2} rng {5,7:F2}"
                    + " | F {6,7:F2} S {7,6:F2} T {8,6:F2} | cmdF {9,6:F2} cmdS {10,6:F2}"
                    + " cmdT {11,6:F2} bal {12}",
                    i * dt, sel.Stage, sel.Waypoint, axial, lateral, range,
                    st.DistF, st.DistS, st.DistT, c.Fore, c.Starboard, c.Top, c.Balanced));

            if (range > 5000.0) { r.Ended = "diverged"; return r; }
        }
        return r;
    }

    public static int Run_()
    {
        Console.WriteLine("DragonScreen docking approach tests");
        checks = failures = 0;

        const double keepOut = 30.0, acquire = 0.25, safe = 12.0;
        V axis = new V(0, 0, 1);

        // ---- 1. THE FLOWN CASE. 45 m out, a few metres off the axis. ----
        // This is the 2026-08-12 18:11 geometry, which reached `Axial` and stalled at 13.0 m.
        Trace = Environment.GetEnvironmentVariable("DOCKTRACE") == "1";
        Run flown = Fly(new V(3.5, 0.0, 90.0), new V(0, 0, -0.5), axis, keepOut, acquire, safe, 900);
        Trace = false;
        Check("the flown approach now CAPTURES", flown.Captured, flown.Ended
              + ", closest " + flown.MinRangeM.ToString("F2") + " m");
        Check("...without stalling at 13 m", flown.MinRangeM < 1.0,
              flown.MinRangeM.ToString("F2") + " m");
        Check("...on a sane propellant budget", flown.MonoUnits < 40.0,
              flown.MonoUnits.ToString("F1") + " units");
        Check("...arriving slowly", flown.WorstContactMps < 0.5,
              flown.WorstContactMps.ToString("F3") + " m/s");
        Check("...and lined up before the final run",
              flown.MaxLateralInFinalM <= DockApproach.CorridorAbortM,
              flown.MaxLateralInFinalM.ToString("F2") + " m");

        // ---- 2. THE STALL IS ARITHMETIC, AND IT IS GONE ----
        // `StandoffM 25 - StandoffToleranceM 12 = 13`. Both flights pinned at 13.0-13.6 m. The
        // old rule committed to the final run at 15 m of lateral; the new one at 1 m.
        Check("the corridor commits at 1 m, not 15", DockApproach.CorridorRadiusM <= 1.0,
              DockApproach.CorridorRadiusM.ToString("F1"));

        DockApproachInputs off = new DockApproachInputs();
        off.Valid = true; off.AxialM = 13.0; off.LateralM = 4.0;
        off.ToStandoffM = 12.0; off.ToGateM = 40.0; off.PathClear = true;
        off.AcquireM = acquire; off.SafeM = safe;
        DockApproachResult offRes = DockApproach.Select(off, DockStage.Corridor);
        Check("4 m off the axis at 13 m does NOT start the final run",
              offRes.Waypoint == DockWaypoint.Standoff, offRes.Waypoint.ToString());

        off.LateralM = 0.4;
        DockApproachResult onRes = DockApproach.Select(off, DockStage.Corridor);
        Check("...and 0.4 m off the axis DOES", onRes.Waypoint == DockWaypoint.Port
              && onRes.Stage == DockStage.Axial, onRes.Waypoint.ToString());

        // ---- 3. NEVER REVERSE THROUGH THE STATION ----
        DockApproachInputs behind = new DockApproachInputs();
        behind.Valid = true; behind.AxialM = -20.0; behind.LateralM = 1.0;
        behind.ToStandoffM = 45.0; behind.ToGateM = 60.0; behind.PathClear = true;
        behind.AcquireM = acquire; behind.SafeM = safe;
        DockApproachResult b = DockApproach.Select(behind, DockStage.Corridor);
        Check("behind the port, on the axis, we clear the axis FIRST",
              b.Waypoint == DockWaypoint.SideStep, b.Waypoint.ToString());

        behind.LateralM = 20.0;
        DockApproachResult b2 = DockApproach.Select(behind, DockStage.Corridor);
        Check("...then go round the front, never through", b2.Waypoint == DockWaypoint.Gate,
              b2.Waypoint.ToString());

        behind.AxialM = -2.0; behind.LateralM = 0.5;
        DockApproachResult b3 = DockApproach.Select(behind, DockStage.Axial);
        Check("just behind the port, back straight out", b3.Waypoint == DockWaypoint.BackOut,
              b3.Waypoint.ToString());

        // ---- 4. STARTING FROM BEHIND STILL DOCKS ----
        Trace = Environment.GetEnvironmentVariable("DOCKTRACE2") == "1";
        Run fromBehind = Fly(new V(2.0, 0.0, -25.0), new V(0, 0, 0), axis, keepOut, acquire,
                             safe, 1800);
        Trace = false;
        Check("an approach that starts BEHIND the port still captures", fromBehind.Captured,
              fromBehind.Ended + ", closest " + fromBehind.MinRangeM.ToString("F2") + " m");

        // ---- 5. THE MONOTONE RULE STILL HOLDS ----
        DockApproachInputs far = new DockApproachInputs();
        far.Valid = true; far.AxialM = 40.0; far.LateralM = 0.5;
        far.ToStandoffM = 15.0; far.ToGateM = 5.0; far.PathClear = true;
        far.AcquireM = acquire; far.SafeM = safe;
        Check("a reached stage is never given back",
              DockApproach.Rank(DockApproach.Select(far, DockStage.Axial).Stage)
              >= DockApproach.Rank(DockStage.Axial), "");

        // ---- 6. THE TIME BALANCE ACTUALLY SLOWS THE CLOSURE ----
        // Big axial, big lateral: the fore setpoint must come down so the lateral can catch up.
        Pid qf = new Pid(), qs = new Pid(), qt = new Pid();
        DockState wide = new DockState();
        // Close in, badly off-axis: arriving BEFORE lining up is the case that must be slowed.
        wide.Valid = true; wide.DistF = 5.0; wide.DistS = 8.0; wide.DistT = 0.0;
        wide.SpeedCap = 1.0;
        DockCommand wc = DockControl.Solve(wide, qf, qs, qt, 0.1);
        Check("a big lateral SLOWS the closure", wc.Balanced, "not balanced");

        Pid rf = new Pid(), rs = new Pid(), rt = new Pid();
        DockState lined = new DockState();
        lined.Valid = true; lined.DistF = 30.0; lined.DistS = 0.02; lined.DistT = 0.0;
        lined.SpeedCap = 1.0;
        DockCommand lc = DockControl.Solve(lined, rf, rs, rt, 0.1);
        Check("...and a lined-up approach is not slowed", !lc.Balanced, "balanced when lined up");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
