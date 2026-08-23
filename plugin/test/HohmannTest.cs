/*
 * Tests for the Hohmann transfer math (pure/Hohmann.cs). The rendezvous uses it to climb from the
 * insertion orbit to the station's altitude, so the dv signs and magnitudes have to be right.
 */
using System;
using DragonScreen;

public static class HohmannTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }
    static bool Near(double a, double b, double tol) { return Math.Abs(a - b) <= tol; }

    public static int Run()
    {
        Console.WriteLine("DragonScreen Hohmann transfer tests");

        // Earth: mu, radius. Insertion ~200 km circular; ISS ~420 km circular.
        double mu = 3.986004418e14;
        double Re = 6.371e6;
        double r1 = Re + 200000.0;   // 200 km
        double r2 = Re + 420000.0;   // 420 km (station)

        // circular speeds
        double v1 = Hohmann.SpeedAt(r1, r1, mu);
        Check("circular speed at 200 km ~ 7788 m/s", Near(v1, 7788.0, 30.0), v1.ToString("F1"));
        double v2 = Hohmann.SpeedAt(r2, r2, mu);
        Check("circular speed at 420 km ~ 7659 m/s", Near(v2, 7659.0, 30.0), v2.ToString("F1"));

        // First burn: raise apoapsis from a 200 km circular orbit to touch 420 km. Small POSITIVE dv.
        double dv1 = Hohmann.RaiseOppositeApsisDv(r1, r1, r2, mu);
        Check("raise-to-station burn is prograde (positive)", dv1 > 0.0, dv1.ToString("F1"));
        Check("...and about 60 m/s for a 220 km climb", Near(dv1, 60.0, 25.0), dv1.ToString("F1"));

        // After that burn our orbit is 200 x 420. Circularise at apoapsis (420 km): another positive dv.
        double aTransfer = Hohmann.TransferSma(r1, r2);
        double dv2 = Hohmann.CirculariseDv(r2, aTransfer, mu);
        Check("circularise-at-apoapsis burn is prograde (positive)", dv2 > 0.0, dv2.ToString("F1"));
        Check("...and about 60 m/s", Near(dv2, 60.0, 25.0), dv2.ToString("F1"));

        // Total Hohmann 200->420 km is ~120 m/s.
        Check("total Hohmann 200->420 km ~ 120 m/s", Near(dv1 + dv2, 120.0, 40.0), (dv1 + dv2).ToString("F1"));

        // Lowering: from a 420 km circular orbit, lower periapsis to 200 km at apoapsis - RETROGRADE.
        double dvLow = Hohmann.RaiseOppositeApsisDv(r2, r2, r1, mu);
        Check("lower-to-station burn is retrograde (negative)", dvLow < 0.0, dvLow.ToString("F1"));

        // Sanity: a burn that does not change the orbit is zero dv.
        Check("no-op burn (aNew == aOld) is 0 dv", Near(Hohmann.ApsisBurnDv(r1, r1, r1, mu), 0.0, 1e-6), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
