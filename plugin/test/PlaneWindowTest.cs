/*
 * Tests for the launch-into-plane window (pure/PlaneWindow.cs). The invariant that matters: at the time
 * it returns, the (rotated) launch site really is in the target plane, and the crossing has the right
 * north/south sense. Verified by rotating the site and checking dot(site, normal) ~ 0.
 */
using System;
using DragonScreen;

public static class PlaneWindowTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // dot(Rz(omega t) * r, n)
    static double DotAt(double rx, double ry, double rz, double nx, double ny, double nz,
                        double omega, double t)
    {
        double th = omega * t, c = Math.Cos(th), s = Math.Sin(th);
        double x = rx * c - ry * s, y = rx * s + ry * c, z = rz;
        return x * nx + y * ny + z * nz;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen plane-window tests");

        double omega = 7.2921159e-5;                 // Earth rotation, rad/s
        double inc = 51.6 * Math.PI / 180.0, lan = 0.0;
        double nx, ny, nz;
        PlaneWindow.NormalFromElements(inc, lan, out nx, out ny, out nz);
        Check("plane normal is unit length",
              Math.Abs(Math.Sqrt(nx * nx + ny * ny + nz * nz) - 1.0) < 1e-9, "");
        Check("a 51.6 deg plane normal tilts off the pole by 51.6 deg (n.z = cos inc)",
              Math.Abs(nz - Math.Cos(inc)) < 1e-9, nz.ToString("F4"));

        // LC-39A latitude 28.6 deg; sweep the site's inertial longitude and confirm the window lands
        // the site ON the plane, from the correct side, every time.
        double lat = 28.6 * Math.PI / 180.0;
        double maxDot = 0.0; int bad = 0;
        for (int k = 0; k < 24; k++)
        {
            double lon = k * (2.0 * Math.PI / 24.0);
            double rx = Math.Cos(lat) * Math.Cos(lon);
            double ry = Math.Cos(lat) * Math.Sin(lon);
            double rz = Math.Sin(lat);

            double t = PlaneWindow.SecondsToPlane(rx, ry, rz, nx, ny, nz, omega, true);
            if (t < 0.0) { bad++; continue; }
            // must be within one rotation
            if (t > 2.0 * Math.PI / omega + 1.0) bad++;
            double d = DotAt(rx, ry, rz, nx, ny, nz, omega, t);
            if (Math.Abs(d) > maxDot) maxDot = Math.Abs(d);
            // north sense: dot should be RISING through zero -> dot just after t is positive
            double dAfter = DotAt(rx, ry, rz, nx, ny, nz, omega, t + 30.0);
            if (dAfter <= 0.0) bad++;
        }
        Check("north window puts the site ON the plane at t (|dot| ~ 0 for all 24 longitudes)",
              maxDot < 1e-6, "max|dot| " + maxDot.ToString("E2"));
        Check("...and it is the NORTHBOUND (rising) crossing, within one rotation, every time",
              bad == 0, bad + " bad");

        // South window: dot should be FALLING through zero.
        double lon2 = 1.0;
        double r2x = Math.Cos(lat) * Math.Cos(lon2), r2y = Math.Cos(lat) * Math.Sin(lon2), r2z = Math.Sin(lat);
        double ts = PlaneWindow.SecondsToPlane(r2x, r2y, r2z, nx, ny, nz, omega, false);
        Check("south window returns a valid time", ts > 0.0, ts.ToString("F0"));
        Check("south window lands on the plane", Math.Abs(DotAt(r2x, r2y, r2z, nx, ny, nz, omega, ts)) < 1e-6, "");
        Check("...and it is the DESCENDING (falling) crossing",
              DotAt(r2x, r2y, r2z, nx, ny, nz, omega, ts + 30.0) < 0.0, "");

        // North and south crossings differ (they are the two nodes), roughly half a day apart in the
        // worst case but always distinct.
        double tn = PlaneWindow.SecondsToPlane(r2x, r2y, r2z, nx, ny, nz, omega, true);
        Check("north and south windows are distinct times", Math.Abs(tn - ts) > 1.0,
              tn.ToString("F0") + " vs " + ts.ToString("F0"));

        // Degenerate: a site on the rotation axis never crosses -> -1.
        Check("a site on the pole returns -1 (never crosses)",
              PlaneWindow.SecondsToPlane(0, 0, 1, nx, ny, nz, omega, true) < 0.0, "");

        // ---- MechJeb TimeToPlane (the live launch window) ----
        // Anchors computed independently in scratchpad/verify_ttp.py (rotPeriod 86164 s, lat 28.6084,
        // inc 51.6 - LC-39A into the ISS plane).
        double R = 86164.0, latD = 28.6084, incD = 51.6;
        Check("TimeToPlane(cel 0, lan 0) ~ 6130 s",
              Math.Abs(PlaneWindow.TimeToPlane(R, latD, 0.0, 0.0, incD) - 6130.25) < 1.0,
              PlaneWindow.TimeToPlane(R, latD, 0.0, 0.0, incD).ToString("F1"));
        Check("TimeToPlane(cel 90, lan 0) ~ 70753 s",
              Math.Abs(PlaneWindow.TimeToPlane(R, latD, 90.0, 0.0, incD) - 70753.25) < 1.0,
              PlaneWindow.TimeToPlane(R, latD, 90.0, 0.0, incD).ToString("F1"));
        Check("TimeToPlane(cel 0, lan 90) ~ 27671 s",
              Math.Abs(PlaneWindow.TimeToPlane(R, latD, 0.0, 90.0, incD) - 27671.25) < 1.0,
              PlaneWindow.TimeToPlane(R, latD, 0.0, 90.0, incD).ToString("F1"));
        Check("equatorial target -> launch now (0 s)",
              PlaneWindow.TimeToPlane(R, latD, 123.0, 45.0, 0.0) == 0.0, "");
        Check("TimeToPlane always in [0, rotationPeriod)",
              PlaneWindow.TimeToPlane(R, latD, 200.0, 113.5, incD) >= 0.0
              && PlaneWindow.TimeToPlane(R, latD, 200.0, 113.5, incD) < R, "");

        // THE INVARIANT THAT KILLS THE X: launching at t makes the orbit's ascending-node LAN equal the
        // target LAN (coplanar), for EVERY pad longitude and target LAN. North-going orbit LAN =
        // celLon(t) - angleEastOfAN, with angleEastOfAN = asin(tan lat / tan inc).
        double aEast = Math.Asin(Math.Tan(latD * Math.PI / 180.0) / Math.Tan(incD * Math.PI / 180.0))
                       * 180.0 / Math.PI;
        int planeBad = 0;
        double[] targets = { 0.0, 45.0, 113.5, 250.0, 359.0 };
        foreach (double targetLan in targets)
            for (int c = 0; c < 360; c += 30)
            {
                double t = PlaneWindow.TimeToPlane(R, latD, c, targetLan, incD);
                double lanAtLaunch = (c + 360.0 * t / R - aEast) % 360.0;
                double err = ((lanAtLaunch - targetLan + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
                if (Math.Abs(err) > 0.01) planeBad++;
            }
        Check("launching at TimeToPlane always lands the orbit LAN on the target LAN (coplanar, no X)",
              planeBad == 0, planeBad + " of 60 off-plane");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
