/*
 * DragonScreen headless tests - the GNC orbital toolbox.
 *
 * ---- CHECKED AGAINST CLOSED-FORM ANSWERS, NOT AGAINST ITSELF ----
 * A maths library tested only for self-consistency will happily be self-consistently wrong. So every
 * check here compares against something independent: a hand-worked circular orbit, a textbook
 * Hohmann, a known great-circle distance, or a round trip through an inverse.
 *
 * Kerbin's real numbers are used throughout - mu 3.5316e12, radius 600 km - because the constants
 * this toolbox will be asked about are Kerbin's, and `kerbin-degree-to-metres` records that
 * assuming Earth's figures has already cost this project real miss distances.
 */
using System;
using DragonScreen;

public static class OrbitalTest
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

    // Kerbin.
    const double Mu = 3.5316000e12;
    const double R = 600000.0;

    public static int Run()
    {
        Console.WriteLine("DragonScreen orbital toolbox tests");
        Basics();
        Anomalies();
        Phasing();
        Ground();
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    static void Basics()
    {
        // Circular speed at 86 km - the ferry's parking orbit. sqrt(mu/r) by hand.
        double r = R + 86000.0;
        Near("circular speed at 86 km", Orbital.CircularSpeed(Mu, r), Math.Sqrt(Mu / r), 1e-6);

        // Vis-viva must AGREE with circular speed when sma == r. Two formulas, one answer.
        Near("vis-viva equals circular when a = r",
             Orbital.VisViva(Mu, r, r), Orbital.CircularSpeed(Mu, r), 1e-6);

        // A hyperbolic orbit has NEGATIVE sma. This must not return NaN - the escape trajectory on
        // 2026-08-06 is exactly the state a guard would be asked about.
        double hyp = Orbital.VisViva(Mu, r, -r);
        Check("hyperbolic vis-viva is finite", !double.IsNaN(hyp) && hyp > 0.0, hyp.ToString());
        Check("unbound orbit has no period", Orbital.Period(Mu, -r) == 0.0, "");

        // Period of an 86 km circular orbit, against 2*pi*sqrt(a^3/mu).
        Near("period at 86 km", Orbital.Period(Mu, r),
             2.0 * Math.PI * Math.Sqrt(r * r * r / Mu), 1e-6);
        // And a sanity band: Kerbin low orbit is about half an hour.
        Check("period is plausible", Orbital.Period(Mu, r) > 1800.0
              && Orbital.Period(Mu, r) < 2100.0, Orbital.Period(Mu, r).ToString("F1"));

        // ---- HOHMANN, AGAINST THE TEXTBOOK ----
        double r1 = R + 80000.0, r2 = R + 120000.0;
        double dv1, dv2;
        Orbital.Hohmann(Mu, r1, r2, out dv1, out dv2);
        Check("raising costs positive dv on both burns", dv1 > 0.0 && dv2 > 0.0,
              dv1.ToString("F2") + " / " + dv2.ToString("F2"));
        double dvl1, dvl2;
        Orbital.Hohmann(Mu, r2, r1, out dvl1, out dvl2);
        Check("lowering costs negative dv on both burns", dvl1 < 0.0 && dvl2 < 0.0,
              dvl1.ToString("F2") + " / " + dvl2.ToString("F2"));
        // Reversibility: the same transfer costs the same either way.
        Near("Hohmann is reversible", Math.Abs(dv1) + Math.Abs(dv2),
             Math.Abs(dvl1) + Math.Abs(dvl2), 1e-6);

        // ---- THE FIXED POINT THAT WOULD HAVE PREVENTED THE ESCAPE ----
        double vc = Orbital.CircularSpeed(Mu, r);
        Near("circularisation dv is ZERO on a circular orbit",
             Orbital.CircularisationDv(Mu, r, vc, 0.0), 0.0, 1e-9);
        Check("dv is positive when slow",
              Orbital.CircularisationDv(Mu, r, vc - 50.0, 0.0) > 0.0, "");
        Check("dv is positive when FAST too - overspeed still needs correcting",
              Orbital.CircularisationDv(Mu, r, vc + 50.0, 0.0) > 0.0, "");
        Check("vertical speed always costs dv",
              Orbital.CircularisationDv(Mu, r, vc, 30.0) > 0.0, "");
        // Monotonic away from the fixed point in both directions - that is what makes it converge.
        Check("dv grows with the error",
              Orbital.CircularisationDv(Mu, r, vc - 100.0, 0.0)
              > Orbital.CircularisationDv(Mu, r, vc - 50.0, 0.0), "");
    }

    static void Anomalies()
    {
        // On a CIRCULAR orbit every anomaly is the same angle. Strong independent check.
        for (double ta = 0.2; ta < 6.0; ta += 0.7)
        {
            Near("circular: true = eccentric at " + ta.ToString("F1"),
                 Orbital.TrueToEccentric(ta, 0.0), ta, 1e-9);
            Near("circular: true = mean at " + ta.ToString("F1"),
                 Orbital.TrueToMean(ta, 0.0), ta, 1e-9);
        }

        // At periapsis and apoapsis all three coincide on ANY ellipse.
        Near("periapsis: mean is 0", Orbital.TrueToMean(0.0, 0.4), 0.0, 1e-9);
        Near("apoapsis: mean is pi", Orbital.TrueToMean(Math.PI, 0.4), Math.PI, 1e-9);

        // ---- ALTITUDE TO TRUE ANOMALY ----
        // 80 x 120 km. sma and ecc from the apsides.
        double pe = 80000.0, ap = 120000.0;
        double sma = R + (pe + ap) * 0.5;
        double ecc = ((ap + R) - (pe + R)) / ((ap + R) + (pe + R));
        double up, down;

        // At periapsis the answer must be 0, at apoapsis pi - the two places it is knowable exactly.
        Orbital.AltitudeToTrueAnomaly(sma, ecc, R, pe, pe, ap, out up, out down);
        Near("periapsis is true anomaly 0", up, 0.0, 1e-6);
        Orbital.AltitudeToTrueAnomaly(sma, ecc, R, ap, pe, ap, out up, out down);
        Near("apoapsis is true anomaly pi", up, Math.PI, 1e-6);

        // The two roots are symmetric about the apsides.
        Orbital.AltitudeToTrueAnomaly(sma, ecc, R, 100000.0, pe, ap, out up, out down);
        Near("the two crossings are symmetric", up + down, 2.0 * Math.PI, 1e-9);
        Check("climbing crossing is in the first half", up > 0.0 && up < Math.PI, up.ToString("F4"));

        // ---- THE CLAMP. ASKING FOR THE IMPOSSIBLE MUST NOT THROW. ----
        // arccos outside -1..1 is undefined, and flight 020 sat in a wait for an altitude above
        // apoapsis for an entire window.
        Orbital.AltitudeToTrueAnomaly(sma, ecc, R, 500000.0, pe, ap, out up, out down);
        Check("above apoapsis clamps instead of throwing",
              !double.IsNaN(up) && Math.Abs(up - Math.PI) < 1e-6, up.ToString("F4"));
        Orbital.AltitudeToTrueAnomaly(sma, ecc, R, -50000.0, pe, ap, out up, out down);
        Check("below periapsis clamps too", !double.IsNaN(up) && Math.Abs(up) < 1e-6,
              up.ToString("F4"));

        // ---- TIME TO ALTITUDE ----
        double period = Orbital.Period(Mu, sma);
        // From periapsis, the time to apoapsis is exactly half a period.
        double t = Orbital.TimeToAltitude(Mu, sma, ecc, R, pe, ap, 0.0, ap, 0);
        Near("periapsis to apoapsis is half a period", t, period * 0.5, 1.0);
        // Every answer must lie within one period, and never be negative.
        for (double ta = 0.0; ta < 6.2; ta += 0.5)
            for (int mode = 0; mode < 3; mode++)
            {
                double tt = Orbital.TimeToAltitude(Mu, sma, ecc, R, pe, ap, ta, 100000.0, mode);
                Check("time to altitude is sane at ta=" + ta.ToString("F1") + " mode " + mode,
                      tt >= 0.0 && tt <= period + 1.0, tt.ToString("F1"));
            }
        // Mode 0 is the SOONER of the two crossings, by definition.
        double t0 = Orbital.TimeToAltitude(Mu, sma, ecc, R, pe, ap, 1.0, 100000.0, 0);
        double t1 = Orbital.TimeToAltitude(Mu, sma, ecc, R, pe, ap, 1.0, 100000.0, 1);
        double t2 = Orbital.TimeToAltitude(Mu, sma, ecc, R, pe, ap, 1.0, 100000.0, 2);
        Check("mode 0 is the sooner crossing", t0 <= t1 + 1e-9 && t0 <= t2 + 1e-9,
              t0 + " vs " + t1 + " / " + t2);
    }

    static void Phasing()
    {
        double ours = R + 80000.0, theirs = R + 86800.0;   // us below the station

        // ---- NEVER RETURN A WAIT IN THE PAST ----
        // GNC.ks returns abs(t) and its own comment admits it "cannot tell you that you have JUST
        // missed the window". Ours rolls forward to the next one instead.
        for (double phase = 0.0; phase < 360.0; phase += 15.0)
        {
            double w = Orbital.PhaseWaitSeconds(Mu, ours, theirs, phase);
            Check("phase wait is never negative at " + phase, w >= 0.0, w.ToString("F1"));
        }

        // ...and never more than one synodic period away.
        double syn = Orbital.SynodicPeriod(Mu, ours, theirs);
        Check("synodic period is positive", syn > 0.0, syn.ToString("F1"));
        for (double phase = 0.0; phase < 360.0; phase += 15.0)
            Check("phase wait is within one synodic period at " + phase,
                  Orbital.PhaseWaitSeconds(Mu, ours, theirs, phase) <= syn + 1.0, "");

        // Two identical orbits never line up differently - 0 means "never", not a divide by zero.
        Check("matched orbits have no synodic period",
              Orbital.SynodicPeriod(Mu, ours, ours) == 0.0, "");
        // A lower orbit catches up, so its synodic period is finite and sensible.
        Check("synodic period is longer than either orbit",
              syn > Orbital.Period(Mu, ours) && syn > Orbital.Period(Mu, theirs), syn.ToString("F0"));
    }

    static void Ground()
    {
        // ---- ⛔ A KERBIN DEGREE IS 10 472 m, NOT EARTH'S 111 320 ----
        // This is the check that stops that mistake being made a fourth time.
        double oneDeg = Orbital.GroundRange(R, 0.0, 0.0, 0.0, 1.0);
        Near("one degree of Kerbin is 10 472 m", oneDeg, 10472.0, 2.0);

        // Same point is zero; antipode is half the circumference.
        Near("zero distance to itself", Orbital.GroundRange(R, 10.0, 20.0, 10.0, 20.0), 0.0, 1e-6);
        Near("antipode is half the circumference",
             Orbital.GroundRange(R, 0.0, 0.0, 0.0, 180.0), Math.PI * R, 1.0);
        // Symmetric.
        Near("range is symmetric", Orbital.GroundRange(R, 12.0, 34.0, -5.0, 100.0),
             Orbital.GroundRange(R, -5.0, 100.0, 12.0, 34.0), 1e-9);

        // ---- BEARING ----
        Near("due north is 0", Orbital.Bearing(0.0, 0.0, 10.0, 0.0), 0.0, 1e-6);
        Near("due east is 90", Orbital.Bearing(0.0, 0.0, 0.0, 10.0), 90.0, 1e-6);
        Near("due south is 180", Orbital.Bearing(0.0, 0.0, -10.0, 0.0), 180.0, 1e-6);
        Near("due west is 270", Orbital.Bearing(0.0, 0.0, 0.0, -10.0), 270.0, 1e-6);

        // ---- OFFSET, AND THE ROUND TRIP ----
        // Offsetting by a distance along a bearing, then measuring back, must return that distance.
        // This is the aim-point arithmetic the whole de-orbit targeting stands on.
        double lat, lon;
        for (double brg = 0.0; brg < 360.0; brg += 45.0)
        {
            Orbital.OffsetLatLon(R, 5.0, -70.0, brg, 30000.0, out lat, out lon);
            Near("offset round-trips at bearing " + brg,
                 Orbital.GroundRange(R, 5.0, -70.0, lat, lon), 30000.0, 1.0);
            Near("bearing round-trips at " + brg, Orbital.Bearing(5.0, -70.0, lat, lon), brg, 0.5);
        }

        // A 35 km overshoot due east of the KSC pad - the real dgOvershoot case.
        Orbital.OffsetLatLon(R, -0.0972, -74.5577, 90.0, Deorbit.OvershootM, out lat, out lon);
        Near("35 km overshoot lands 35 km away",
             Orbital.GroundRange(R, -0.0972, -74.5577, lat, lon), Deorbit.OvershootM, 1.0);
        Check("and it is to the EAST", lon > -74.5577, lon.ToString("F4"));
    }
}
