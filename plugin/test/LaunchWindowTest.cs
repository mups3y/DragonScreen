// Tests for LaunchWindow (pure/LaunchWindow.cs) — the launch-to-rendezvous plane-crossing / RAAN window.
// Checked against the closed form sin(λ+a) = tanφ/tan i for a plane with ascending node at longitude 0,
// and by rotating the site to the returned time and confirming it lands IN the plane (dot ≈ 0).
using System;
using DragonScreen;

public static class LaunchWindowTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    // Rotate a vector about a unit axis by angle a (Rodrigues) — mirrors the site's spin.
    static Vec3 Rot(Vec3 v, Vec3 ax, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return v * c + Vec3.Cross(ax, v) * s + ax * (Vec3.Dot(ax, v) * (1 - c));
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen launch-window (RAAN) tests");
        const double D2R = Math.PI / 180.0;

        // Site at 28.6°N, longitude 0; spin axis +z, one-day rotation.
        double phiLat = 28.6 * D2R;
        Vec3 site = new Vec3(Math.Cos(phiLat), 0, Math.Sin(phiLat));
        Vec3 axis = new Vec3(0, 0, 1);
        double omega = 2.0 * Math.PI / 86400.0;

        // Target plane, inclination 51.6°, ascending node at longitude 0 → normal n = (0, -sin i, cos i).
        double inc = 51.6 * D2R;
        Vec3 n = new Vec3(0, -Math.Sin(inc), Math.Cos(inc));

        double t;
        bool ok = LaunchWindow.TimeToCrossing(site, n, axis, omega, +1, out t);
        Check("a window exists (inc 51.6 > lat 28.6)", ok, "");
        Check("window is in the future", t > 0.0, t.ToString("F1"));
        // rotate the site to the window and confirm it lies IN the plane (perpendicular to the normal).
        Vec3 sAtT = Rot(site, axis, omega * t);
        double d = Vec3.Dot(sAtT.Normalized, n);
        Check("site is IN the plane at the window (dot≈0)", Math.Abs(d) < 1e-6, d.ToString("E3"));

        // The two node opportunities are distinct — the other sign gives a different (also valid) crossing.
        double t2;
        bool ok2 = LaunchWindow.TimeToCrossing(site, n, axis, omega, -1, out t2);
        Check("the other node also has a window", ok2, "");
        Vec3 sAtT2 = Rot(site, axis, omega * t2);
        Check("...also in the plane", Math.Abs(Vec3.Dot(sAtT2.Normalized, n)) < 1e-6, "");
        Check("...and the two opportunities differ", Math.Abs(t - t2) > 1.0, "t=" + t.ToString("F0") + " t2=" + t2.ToString("F0"));

        // Unreachable: target inclination BELOW the site latitude → no window.
        Vec3 nLow = new Vec3(0, -Math.Sin(20 * D2R), Math.Cos(20 * D2R));   // inc 20° < 28.6° lat
        double t3;
        Check("no window when inclination < latitude", !LaunchWindow.TimeToCrossing(site, nLow, axis, omega, +1, out t3), "");

        // Equatorial site (lat 0): any prograde plane is reachable and the window exists.
        Vec3 siteEq = new Vec3(1, 0, 0);
        double t4;
        Check("equator site has a window to an inclined plane",
              LaunchWindow.TimeToCrossing(siteEq, n, axis, omega, +1, out t4), "");

        // Degenerate guards: zero omega / zero vectors → no window, no throw.
        double t5;
        Check("zero omega → no window", !LaunchWindow.TimeToCrossing(site, n, axis, 0.0, +1, out t5), "");
        Check("zero normal → no window", !LaunchWindow.TimeToCrossing(site, Vec3.Zero, axis, omega, +1, out t5), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }
}
