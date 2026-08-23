/*
 * DragonScreen headless tests - the launch-azimuth solve for RSS/Crew-1.
 *
 * The numbers are the real ones: LC-39A is at 28.6 N and the ISS plane is 51.6 deg, so the inertial
 * heading is ~45 (north-east) and the ground heading ~43 once Earth's spin is taken out. The stock
 * near-equatorial case must still come out ~90 (due east), because that is the heading the ascent has
 * always flown and this must not change it.
 */
using System;
using DragonScreen;

public static class LaunchAzimuthTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    static void Near(string what, double got, double want, double tol)
    {
        Check(what, Math.Abs(got - want) <= tol, got.ToString("F3") + " vs " + want.ToString("F3"));
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen launch-azimuth tests");

        // ---- STOCK near-equatorial: must stay ~90 (due east), unchanged behaviour ----
        Near("stock 0.133 deg from the equator is ~due east",
             LaunchAzimuth.InertialHeadingDeg(0.133, 0.0), 89.87, 0.2);

        // ---- CREW-1: 51.6 deg plane from LC-39A (28.6 N) ----
        Near("Crew-1 inertial azimuth is ~45 (north-east)",
             LaunchAzimuth.InertialHeadingDeg(51.6, 28.6), 45.0, 0.2);

        // ---- polar and minimum-inclination edges ----
        Near("a polar orbit launches due north", LaunchAzimuth.InertialHeadingDeg(90.0, 28.6), 0.0, 1e-6);
        Near("inclination == latitude launches due east",
             LaunchAzimuth.InertialHeadingDeg(28.6, 28.6), 90.0, 1e-6);

        // ---- unreachable: inclination below the launch latitude clamps to due east ----
        Near("an inclination below the pad latitude clamps to due east",
             LaunchAzimuth.InertialHeadingDeg(20.0, 28.6), 90.0, 1e-6);

        // ---- retrograde returns a heading west of north ----
        Check("a retrograde target heads west of north",
              LaunchAzimuth.InertialHeadingDeg(98.0, 28.6) < 0.0,
              LaunchAzimuth.InertialHeadingDeg(98.0, 28.6).ToString("F2"));

        // ---- surface eastward speed: real Earth at the Cape ~408 m/s, zero at the pole ----
        Near("Earth surface speed at 28.6 N is ~408 m/s",
             LaunchAzimuth.SurfaceEastwardSpeedMps(6371000.0, 86164.0, 28.6), 408.0, 3.0);
        Near("...and zero at the pole",
             LaunchAzimuth.SurfaceEastwardSpeedMps(6371000.0, 86164.0, 90.0), 0.0, 1e-6);
        Check("...and larger at the equator than at the Cape",
              LaunchAzimuth.SurfaceEastwardSpeedMps(6371000.0, 86164.0, 0.0)
              > LaunchAzimuth.SurfaceEastwardSpeedMps(6371000.0, 86164.0, 28.6), "");

        // ---- ground azimuth: Earth's spin pulls the Crew-1 heading a couple of degrees north of 45 ----
        double vOrb200 = Math.Sqrt(3.986004418e14 / (6371000.0 + 200000.0));   // ~7788 m/s
        double vEq = LaunchAzimuth.SurfaceEastwardSpeedMps(6371000.0, 86164.0, 28.6);
        Near("Crew-1 ground azimuth is ~43 (north of the inertial 45)",
             LaunchAzimuth.GroundHeadingDeg(51.6, 28.6, vOrb200, vEq), 42.8, 0.5);
        Check("...and the spin correction moves it north of the inertial heading",
              LaunchAzimuth.GroundHeadingDeg(51.6, 28.6, vOrb200, vEq)
              < LaunchAzimuth.InertialHeadingDeg(51.6, 28.6), "");

        // ---- with no rotation the ground azimuth collapses to the inertial one ----
        Near("no spin -> ground azimuth equals inertial",
             LaunchAzimuth.GroundHeadingDeg(51.6, 28.6, vOrb200, 0.0),
             LaunchAzimuth.InertialHeadingDeg(51.6, 28.6), 1e-6);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
