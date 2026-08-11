/*
 * DragonScreen headless tests - the docking controller (GNC tranche 4).
 *
 * ---- THE ONE PROPERTY THAT MATTERS MOST IS "CAN IT STOP" ----
 * Everything else here is convergence and sign conventions. The check that would actually save the
 * station is that the commanded approach speed at any range is one the vehicle can still brake from,
 * and that it goes to a crawl at contact. A docking controller that arrives unable to stop has no
 * recovery, which is why the braking curve is asserted across the whole range band rather than at a
 * couple of sample points.
 */
using System;
using DragonScreen;

public static class DockControlTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    static void Near(string what, double got, double want, double tol)
    {
        Check(what, Math.Abs(got - want) <= tol,
              "got " + got.ToString("G8") + " want " + want.ToString("G8"));
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen docking controller tests");
        Braking();
        Servo();
        Mixing();
        Converge();
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    static void Braking()
    {
        // ---- CAN IT ALWAYS STOP? ----
        // The commanded speed must never exceed what the assumed RCS authority can brake from in the
        // distance remaining. This is v^2 = 2ad checked directly, at every range.
        for (double d = 0.5; d <= 2000.0; d += 0.5)
        {
            double v = DockControl.AxisSpeedLimit(d, 100.0);   // cap high, so the curve is exposed
            double stopDist = v * v / (2.0 * DockControl.RcsAccel);
            Check("can stop from " + d + " m", stopDist <= d + 1e-6,
                  "needs " + stopDist.ToString("F3") + " has " + d);
        }

        // Signed: the sign says which way, the magnitude how fast.
        Check("negative offset gives negative speed",
              DockControl.AxisSpeedLimit(-50.0, 10.0) < 0.0, "");
        Near("and the magnitudes match",
             Math.Abs(DockControl.AxisSpeedLimit(-50.0, 10.0)),
             DockControl.AxisSpeedLimit(50.0, 10.0), 1e-12);
        Near("no offset, no speed", DockControl.AxisSpeedLimit(0.0, 10.0), 0.0, 1e-12);

        // The cap actually caps.
        Check("cap is respected", DockControl.AxisSpeedLimit(100000.0, 2.0) <= 2.0,
              DockControl.AxisSpeedLimit(100000.0, 2.0).ToString("F3"));

        // Monotonic - further away is never slower.
        double prev = -1.0;
        for (double d = 0.0; d <= 500.0; d += 1.0)
        {
            double v = DockControl.AxisSpeedLimit(d, 100.0);
            Check("monotonic at " + d, v >= prev - 1e-12, v + " after " + prev);
            prev = v;
        }

        // ---- THE STANDOFF SPHERE ----
        // Three equal axis components in quadrature must be exactly the requested distance.
        double per = DockControl.StandoffPerAxis(300.0);
        Near("standoff splits into a sphere, not a cube",
             Math.Sqrt(3.0 * per * per), 300.0, 1e-9);

        // ---- AND IT AGREES WITH THE APPROACH LADDER ----
        // A controller with its own idea of a safe speed is how the ladder and the autopilot end up
        // disagreeing about what "careful" means.
        //
        // ⛔ THIS CHECK USED TO BE A TAUTOLOGY. It compared `SpeedCapFor` against
        // `Rendezvous.CorridorRate` - the function `SpeedCapFor` itself called - so it restated the
        // implementation and could never fail. Meanwhile the two curves it was supposed to be
        // reconciling differed by up to 2.5x. Compare against the LADDER, which is the thing the
        // comment above actually claims.
        for (double r = Approach.BandNearD; r < 3000.0; r += 25.0)
            Near("speed cap matches the ladder at " + r,
                 DockControl.SpeedCapFor(r), Approach.SpeedCap(r), 1e-12);
        Check("and it is never FASTER than the ladder anywhere",
              DockControl.SpeedCapFor(50.0) <= Approach.SpeedCap(50.0)
              && DockControl.SpeedCapFor(5.0) <= Approach.SpeedCap(5.0), "");
        Check("contact speed is a crawl", DockControl.SpeedCapFor(5.0) < 0.3,
              DockControl.SpeedCapFor(5.0).ToString("F3"));
    }

    static void Servo()
    {
        Pid p = new Pid();

        // At the setpoint, a settled controller commands nothing much.
        p.Setpoint = 0.0;
        double o = p.Update(0.0, 0.1);
        Near("no error, no command", o, 0.0, 1e-9);

        // Sign: too SLOW means push forward.
        p.Reset(); p.Setpoint = 1.0;
        Check("below setpoint commands positive", p.Update(0.0, 0.1) > 0.0, "");
        p.Reset(); p.Setpoint = -1.0;
        Check("above setpoint commands negative", p.Update(0.0, 0.1) < 0.0, "");

        // Output is always a valid actuator command.
        p.Reset(); p.Setpoint = 1000.0;
        for (int i = 0; i < 50; i++)
        {
            double v = p.Update(0.0, 0.1);
            Check("output stays in -1..1", v >= -1.0 && v <= 1.0, v.ToString("F3"));
        }

        // ---- INTEGRAL WINDUP IS CLAMPED ----
        // Fifty seconds held against an unreachable setpoint, then the error flips. Without a clamp
        // the stored integral dumps into the actuator and the capsule lurches.
        p.Reset(); p.Setpoint = 100.0;
        for (int i = 0; i < 500; i++) p.Update(0.0, 0.1);
        p.Setpoint = 0.0;
        double after = p.Update(0.0, 0.1);
        Check("integral does not dump on reversal", Math.Abs(after) <= 1.0, after.ToString("F3"));

        // The first call must not produce a derivative spike off a phantom previous error.
        Pid fresh = new Pid(); fresh.P = 0.0; fresh.I = 0.0; fresh.D = 1.0;
        fresh.Setpoint = 5.0;
        Near("no derivative kick on the first call", fresh.Update(0.0, 0.1), 0.0, 1e-9);

        // dt of zero must not divide.
        Pid z = new Pid(); z.Setpoint = 1.0;
        Near("zero dt is a no-op", z.Update(0.0, 0.0), 0.0, 1e-12);
    }

    static void Mixing()
    {
        // The dominant axis gets half authority, the others a quarter. Which one dominates is
        // reported, so a log can say why the capsule is moving the way it is.
        DockState s = new DockState();
        s.Valid = true; s.SpeedCap = 1.0;

        s.DistF = 100.0; s.DistS = 0.1; s.DistT = 0.1;
        DockCommand c = DockControl.Solve(s, new Pid(), new Pid(), new Pid(), 0.1);
        Check("a forward offset is AXIAL", c.Note == "AXIAL", c.Note);

        s.DistF = 0.1; s.DistS = 100.0; s.DistT = 0.1;
        c = DockControl.Solve(s, new Pid(), new Pid(), new Pid(), 0.1);
        Check("a starboard offset is LATERAL", c.Note == "LATERAL", c.Note);

        s.DistF = 0.1; s.DistS = 0.1; s.DistT = 100.0;
        c = DockControl.Solve(s, new Pid(), new Pid(), new Pid(), 0.1);
        Check("a top offset is VERTICAL", c.Note == "VERTICAL", c.Note);

        // Every command is a legal actuator input, whatever the geometry.
        foreach (double f in new[] { -500.0, -1.0, 0.0, 1.0, 500.0 })
            foreach (double st in new[] { -500.0, 0.0, 500.0 })
            {
                DockState g = new DockState();
                g.Valid = true; g.SpeedCap = 1.0; g.DistF = f; g.DistS = st; g.DistT = -st;
                DockCommand gc = DockControl.Solve(g, new Pid(), new Pid(), new Pid(), 0.1);
                Check("command in range for " + f + "/" + st,
                      Math.Abs(gc.Fore) <= 1.0 && Math.Abs(gc.Starboard) <= 1.0
                      && Math.Abs(gc.Top) <= 1.0, gc.Fore + "," + gc.Starboard + "," + gc.Top);
            }

        // Range is the vector magnitude, not a sum.
        DockState r = new DockState();
        r.Valid = true; r.DistF = 3.0; r.DistS = 4.0; r.DistT = 0.0;
        Near("range is the magnitude", DockControl.Range(r), 5.0, 1e-9);

        // Invalid in, nothing out.
        DockCommand bad = DockControl.Solve(new DockState(), new Pid(), new Pid(), new Pid(), 0.1);
        Check("invalid commands nothing",
              bad.Fore == 0.0 && bad.Starboard == 0.0 && bad.Top == 0.0, "");
        Check("and says so", bad.Note == "NO SOLUTION", bad.Note);
    }

    static void Converge()
    {
        // ---- FLY THE WHOLE APPROACH AND CHECK IT ARRIVES SLOWLY ----
        // A crude simulation: the command accelerates us, we integrate, the range closes. What is
        // being tested is that the LOOP is stable and the arrival is gentle - not the fidelity of
        // the plant model.
        Pid pf = new Pid(), ps = new Pid(), pt = new Pid();
        double dF = 200.0, dS = 12.0, dT = -8.0;
        double vF = 0.0, vS = 0.0, vT = 0.0;
        const double dt = 0.1;
        double worstSpeed = 0.0;

        for (int i = 0; i < 40000; i++)
        {
            DockState s = new DockState();
            s.Valid = true;
            s.DistF = dF; s.DistS = dS; s.DistT = dT;
            // ---- SIGN CONVENTION, AND THE FIRST VERSION OF THIS TEST HAD IT BACKWARDS ----
            // `rVel` is OUR velocity minus the target's, on our own axes. A POSITIVE VelF with a
            // POSITIVE DistF means we are closing. Negating it here made the loop positive-feedback
            // and the simulated capsule departed to 474 km - which is what a sign error in a docking
            // controller actually looks like, so it is worth the comment.
            s.VelF = vF; s.VelS = vS; s.VelT = vT;
            s.SpeedCap = DockControl.SpeedCapFor(DockControl.Range(s));

            DockCommand c = DockControl.Solve(s, pf, ps, pt, dt);

            vF += c.Fore * DockControl.RcsAccel * dt;
            vS += c.Starboard * DockControl.RcsAccel * dt;
            vT += c.Top * DockControl.RcsAccel * dt;

            dF -= vF * dt; dS -= vS * dt; dT -= vT * dt;

            double sp = Math.Sqrt(vF * vF + vS * vS + vT * vT);
            double rng = Math.Sqrt(dF * dF + dS * dS + dT * dT);
            if (rng < 20.0 && sp > worstSpeed) worstSpeed = sp;
            if (rng < 0.5) break;
        }

        double finalRange = Math.Sqrt(dF * dF + dS * dS + dT * dT);
        Check("the approach closes", finalRange < 20.0, finalRange.ToString("F2") + " m");
        // ⛔ The one that would save the station: it must be crawling by the time it is close.
        Check("and it is slow inside 20 m", worstSpeed < 2.0, worstSpeed.ToString("F3") + " m/s");
        Check("it never diverges", !double.IsNaN(finalRange) && finalRange < 500.0,
              finalRange.ToString("F1"));
    }
}
