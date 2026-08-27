// Tests for pure/AscentLoss.cs + pure/LaunchTuner.cs (B9). AscentLoss is checked against the closed-form value
// of each loss term; the LaunchTuner is checked by REPLAY against a synthetic loss — a coordinate hill-climb
// must (a) never let the best loss increase, (b) stay in bounds, and (c) walk to the minimum and converge.
using System;
using DragonScreen;

public static class LaunchTunerTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }
    static void Near(string what, double got, double want, double tol)
    { checks++; if (Math.Abs(got - want) > tol) { failures++; Console.WriteLine("  FAIL  " + what + "   got " + got.ToString("F5") + " want " + want.ToString("F5")); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen B9 AscentLoss + LaunchTuner tests");

        // ================= AscentLoss decomposition =================
        double g = 9.81, PI = Math.PI;
        {
            AscentLoss L = new AscentLoss();
            L.Step(2.0, g, PI / 2, 0.0, 20.0, 0.0);   // vertical, zero AoA, thrust 20, no drag
            Near("vertical: gravity loss = g·dt", L.GravityLoss, g * 2.0, 1e-9);
            Near("zero AoA: steering loss = 0", L.SteeringLoss, 0.0, 1e-12);
            Near("no drag: drag loss = 0", L.DragLoss, 0.0, 1e-12);
        }
        {
            AscentLoss L = new AscentLoss();
            L.Step(1.0, g, 0.0, 5.0, 30.0, 0.0);      // horizontal
            Near("horizontal: gravity loss = 0", L.GravityLoss, 0.0, 1e-9);
            Near("drag loss = dragAccel·dt", L.DragLoss, 5.0, 1e-9);
        }
        {
            AscentLoss L = new AscentLoss();
            L.Step(1.0, 0.0, 0.0, 0.0, 100.0, PI / 3);   // 60° AoA, thrust 100
            Near("steering loss = thrust·(1−cos α)·dt", L.SteeringLoss, 100.0 * (1.0 - 0.5), 1e-9);
            Near("total sums the three", L.Total, L.GravityLoss + L.DragLoss + L.SteeringLoss, 1e-12);
        }
        {
            AscentLoss L = new AscentLoss();
            L.Step(1.0, g, PI / 4, 2.0, 10.0, 0.2);
            L.Reset();
            Check("reset zeroes all three", L.GravityLoss == 0 && L.DragLoss == 0 && L.SteeringLoss == 0, "");
            L.Step(0.0, g, PI / 2, 5, 5, 0.5);
            Check("dt = 0 accumulates nothing", L.Total == 0.0, "");
        }

        // ================= LaunchTuner: seed, rejection, monotonicity, convergence =================
        // synthetic separable quadratic loss with an OFF-GRID minimum
        double c0 = 1.35, c1 = -2.15;
        Func<double[], double> loss = p => (p[0] - c0) * (p[0] - c0) + (p[1] - c1) * (p[1] - c1);

        // ---- seed: the first trial is the initial guess (nothing scored yet) ----
        {
            var t = new LaunchTuner(new[] { 10.0, 10.0 }, new[] { -20.0, -20.0 }, new[] { 20.0, 20.0 },
                                    new[] { 2.0, 2.0 }, new[] { 0.02, 0.02 });
            double[] first = t.NextTrial();
            Check("seed: first trial = initial guess", first[0] == 10.0 && first[1] == 10.0, "");
        }

        // ---- a strictly-worse flown result never moves the best ----
        {
            var t = new LaunchTuner(new[] { 0.0, 0.0 }, new[] { -20.0, -20.0 }, new[] { 20.0, 20.0 },
                                    new[] { 2.0, 2.0 }, new[] { 0.02, 0.02 });
            t.Record(t.NextTrial(), 5.0);           // score the seed
            double[] worse = t.NextTrial();
            t.Record(worse, 999.0);                 // clearly worse
            Check("a worse trial does not change the best", t.Best[0] == 0.0 && t.Best[1] == 0.0 && t.BestLoss == 5.0, "");
        }

        // ---- full replay: monotone best loss, in-bounds, converges to the minimum ----
        {
            var t = new LaunchTuner(new[] { 10.0, 10.0 }, new[] { -20.0, -20.0 }, new[] { 20.0, 20.0 },
                                    new[] { 2.0, 2.0 }, new[] { 0.02, 0.02 });
            bool monotone = true, inBounds = true;
            int k = 0;
            for (; k < 600 && !t.Converged; k++)
            {
                double[] p = t.NextTrial();
                if (p[0] < -20 - 1e-9 || p[0] > 20 + 1e-9 || p[1] < -20 - 1e-9 || p[1] > 20 + 1e-9) inBounds = false;
                double prev = t.BestLoss;
                t.Record(p, loss(p));
                if (t.BestLoss > prev + 1e-12) monotone = false;   // the best can only improve
            }
            Check("replay: best loss is monotone non-increasing", monotone, "");
            Check("replay: every trial stayed within bounds", inBounds, "");
            Check("replay: converged within the launch budget", t.Converged, "launches=" + t.Launches);
            Near("replay: best param 0 → the true minimum", t.Best[0], c0, 0.12);
            Near("replay: best param 1 → the true minimum", t.Best[1], c1, 0.12);
            Check("replay: final loss is small", t.BestLoss < 0.05, "loss=" + t.BestLoss.ToString("F4"));
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
