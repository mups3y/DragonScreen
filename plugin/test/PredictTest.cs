/*
 * DragonScreen headless tests - the predictor (GNC tranche 2).
 *
 * ---- THE SEARCHES ARE TESTED AGAINST FUNCTIONS WHOSE ANSWER IS KNOWN EXACTLY ----
 * That is the whole reason the sampling is a delegate. A closest-approach search fed a parabola has
 * one right answer and it is arithmetic; fed a real orbit it has an answer nobody can check by hand.
 * So the search logic is proved here, and the glue only has to supply honest samples.
 */
using System;
using DragonScreen;

public static class PredictTest
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

    const double Mu = 3.5316000e12, R = 600000.0;

    public static int Run()
    {
        Console.WriteLine("DragonScreen predictor tests");
        Timing();
        GroundShift();
        ImpactSolve();
        Approach();
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    static void Timing()
    {
        double sma = R + 100000.0;
        double period = Orbital.Period(Mu, sma);

        // On a CIRCULAR orbit, time between anomalies is just the fraction of a period. Exact.
        Near("quarter orbit is a quarter period",
             Predict.TimeBetweenTrueAnomalies(0.0, period, 0.0, Math.PI / 2.0), period / 4.0, 1e-6);
        Near("half orbit is half a period",
             Predict.TimeBetweenTrueAnomalies(0.0, period, 0.0, Math.PI), period / 2.0, 1e-6);

        // Going "backwards" means the long way round, never a negative time.
        double back = Predict.TimeBetweenTrueAnomalies(0.0, period, Math.PI, 0.0);
        Near("backwards is the long way round", back, period / 2.0, 1e-6);
        for (double a = 0.0; a < 6.2; a += 0.4)
            for (double b = 0.0; b < 6.2; b += 0.4)
            {
                double t = Predict.TimeBetweenTrueAnomalies(0.3, period, a, b);
                Check("never negative, never over a period", t >= 0.0 && t <= period + 1e-6,
                      t.ToString("F3"));
            }

        // On an ELLIPSE, periapsis to apoapsis is still exactly half a period - Kepler's second law
        // says the two halves take equal time however lopsided the ellipse is.
        Near("ellipse: periapsis to apoapsis is half a period",
             Predict.TimeBetweenTrueAnomalies(0.6, period, 0.0, Math.PI), period / 2.0, 1e-6);
    }

    static void GroundShift()
    {
        // Kerbin turns 360 degrees in 21 549.4 s -> about 0.0167 deg/s.
        double rate = 360.0 / 21549.425;

        Near("no time, no shift", Predict.GroundTrackLongitude(50.0, rate, 0.0), 50.0, 1e-9);

        // The body turns EAST under us, so the ground point drifts WEST. Getting this backwards is
        // the classic predicted-impact-on-the-wrong-side-of-the-planet bug.
        Check("the ground point drifts WEST", Predict.GroundTrackLongitude(0.0, rate, 600.0) < 0.0,
              Predict.GroundTrackLongitude(0.0, rate, 600.0).ToString("F3"));
        Near("ten minutes is about 10 degrees",
             Predict.GroundTrackLongitude(0.0, rate, 600.0), -rate * 600.0, 1e-6);

        // A full rotation returns to the same longitude.
        Near("one full rotation is a no-op",
             Predict.GroundTrackLongitude(30.0, rate, 21549.425), 30.0, 1e-3);

        // Always wrapped into -180..180, whatever it is handed.
        for (double dt = 0.0; dt < 200000.0; dt += 3137.0)
        {
            double l = Predict.GroundTrackLongitude(170.0, rate, dt);
            Check("longitude stays in range at dt=" + dt, l >= -180.0 && l <= 180.0,
                  l.ToString("F3"));
        }

        // ⚠ AND THE REASON IT MATTERS, worked properly rather than estimated.
        //     Kerbin turns 360 deg in 21 549.4 s      = 0.016706 deg/s
        //     a ten-minute descent                    = 10.02 deg
        //     a Kerbin degree is 10 472 m             = 105 km
        // The first version of this check asserted "about 260 km" from a mental estimate and the
        // test failed on its own arithmetic, not on the code. 105 km is still enormous - it is
        // twenty times the whole width of the KSC - so the point stands, but the NUMBER is now
        // derived rather than guessed.
        double err = Math.Abs(Predict.GroundTrackLongitude(0.0, rate, 600.0));
        Near("ten minutes of rotation is 10.02 degrees", err, rate * 600.0, 1e-9);
        double metres = Orbital.GroundRange(R, 0.0, 0.0, 0.0, err);
        Near("which is 105 km on the ground", metres, err * 10472.0, 200.0);
        Check("skipping the shift is a six-figure error", metres > 100000.0,
              (metres / 1000.0).ToString("F0") + " km");
    }

    static void ImpactSolve()
    {
        // An orbit that reaches the ground: periapsis below the surface.
        double pe = -40000.0, ap = 86000.0;
        double sma = R + (pe + ap) * 0.5;
        double ecc = ((ap + R) - (pe + R)) / ((ap + R) + (pe + R));
        double period = Orbital.Period(Mu, sma);

        // FLAT terrain at sea level: converges immediately and the height is 0.
        Predict.Impact flat = Predict.SolveImpact(sma, ecc, R, pe, ap, Math.PI, period,
                                                  delegate (double t) { return 0.0; }, 1.0, 20);
        Check("flat terrain converges", flat.Converged, flat.Iterations.ToString());
        Near("flat terrain height is zero", flat.TerrainHeightM, 0.0, 1.0);
        Check("impact is in the future", flat.TimeS > 0.0, flat.TimeS.ToString("F1"));
        Check("impact is within one orbit", flat.TimeS <= period + 1.0, flat.TimeS.ToString("F1"));

        // CONSTANT high terrain - a 3 km plateau. It must settle ON the plateau, not at sea level.
        Predict.Impact hill = Predict.SolveImpact(sma, ecc, R, pe, ap, Math.PI, period,
                                                  delegate (double t) { return 3000.0; }, 1.0, 40);
        Check("plateau converges", hill.Converged, hill.Iterations.ToString());
        Near("settles on the plateau", hill.TerrainHeightM, 3000.0, 20.0);
        Check("a higher impact happens SOONER", hill.TimeS < flat.TimeS,
              hill.TimeS.ToString("F1") + " vs " + flat.TimeS.ToString("F1"));

        // ---- THE DAMPING IS WHAT MAKES IT CONVERGE ----
        // Terrain that swings hard with the predicted time is what an undamped iteration oscillates
        // on: a peak, then the valley behind it, forever. Averaging halves the step each pass.
        Predict.Impact rough = Predict.SolveImpact(sma, ecc, R, pe, ap, Math.PI, period,
            delegate (double t) { return 2000.0 + 1500.0 * Math.Sin(t / 40.0); }, 5.0, 60);
        Check("rough terrain still converges", rough.Converged,
              rough.Iterations + " iterations, height " + rough.TerrainHeightM.ToString("F1"));
        Check("and settles inside the terrain band",
              rough.TerrainHeightM > 400.0 && rough.TerrainHeightM < 3700.0,
              rough.TerrainHeightM.ToString("F1"));

        // It must STOP rather than spin: bounded iterations, reported honestly.
        Predict.Impact wild = Predict.SolveImpact(sma, ecc, R, pe, ap, Math.PI, period,
            delegate (double t) { return (t % 2.0 < 1.0) ? 0.0 : 9000.0; }, 0.001, 12);
        Check("pathological terrain stops at the iteration cap", wild.Iterations <= 12,
              wild.Iterations.ToString());
        Check("and says it did not converge", !wild.Converged, "");
    }

    static void Approach()
    {
        // ---- A PARABOLA HAS ONE MINIMUM AND WE KNOW WHERE IT IS ----
        double at = 137.0;
        Func<double, double> parab = delegate (double t)
        {
            return 500.0 + (t - at) * (t - at) * 0.5;
        };

        Predict.Approach a = Predict.ClosestApproach(parab, 600.0, 100, 4);
        Check("found an approach", a.Valid, "");
        Near("closest approach time", a.TimeS, at, 0.5);
        Near("closest approach distance", a.DistanceM, 500.0, 1.0);

        // Refinement must actually refine - more passes, tighter answer.
        Predict.Approach coarse = Predict.ClosestApproach(parab, 600.0, 20, 0);
        Predict.Approach fine = Predict.ClosestApproach(parab, 600.0, 20, 5);
        Check("refinement improves the answer",
              Math.Abs(fine.TimeS - at) <= Math.Abs(coarse.TimeS - at),
              fine.TimeS.ToString("F3") + " vs " + coarse.TimeS.ToString("F3"));

        // A minimum at the very start of the window - the off-by-one case a scan from i=1 misses.
        Predict.Approach edge = Predict.ClosestApproach(
            delegate (double t) { return 100.0 + t; }, 600.0, 50, 3);
        Near("a minimum at t=0 is found", edge.TimeS, 0.0, 1e-6);
        Near("and its distance is right", edge.DistanceM, 100.0, 1e-6);

        // Monotonically closing: the best is the far end of the window.
        Predict.Approach far = Predict.ClosestApproach(
            delegate (double t) { return 5000.0 - t * 2.0; }, 600.0, 50, 3);
        Near("a minimum at the end is found", far.TimeS, 600.0, 1.0);

        // Nonsense in, no crash out.
        Check("no sampler is not valid", !Predict.ClosestApproach(null, 600, 50, 3).Valid, "");
        Check("no window is not valid", !Predict.ClosestApproach(parab, 0, 50, 3).Valid, "");

        // ---- CLOSING RATE SIGN. POSITIVE MEANS CLOSING. ----
        // The one place a sign error reads as good news on the DOCKING page.
        Check("approaching is POSITIVE",
              Predict.ClosingRate(delegate (double t) { return 1000.0 - 5.0 * t; }, 1.0) > 0.0, "");
        Check("opening is NEGATIVE",
              Predict.ClosingRate(delegate (double t) { return 1000.0 + 5.0 * t; }, 1.0) < 0.0, "");
        Near("and it is the actual rate",
             Predict.ClosingRate(delegate (double t) { return 1000.0 - 5.0 * t; }, 1.0), 5.0, 1e-9);
    }
}
