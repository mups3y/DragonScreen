/*
 * Tests for the station LVLH frame (pure/Lvlh.cs): +radial up, +along ahead, +cross out of plane.
 * Pins the SIGN of each axis - the L-approach turns these into RCS translation, so a flipped `along`
 * slides the capsule away from the port, which "looks identical to a tuning problem right up until
 * the capsule burns the wrong way" (StationApproach's own warning).
 */
using System;
using DragonScreen;

public static class LvlhTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }
    static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-6; }

    public static int Run()
    {
        Console.WriteLine("DragonScreen LVLH-frame tests");

        // ---- canonical station: at +X radius, moving +Y. xh=+X, yh=+Y, zh=+Z. ----
        // stnR = (7e6,0,0), stnV = (0,7500,0)
        double sRx = 7.0e6, sRy = 0.0, sRz = 0.0, sVx = 0.0, sVy = 7500.0, sVz = 0.0;

        // 400 m below the station = 400 m toward the body = -radial.
        LvlhState below = Lvlh.Project(sRx, sRy, sRz, sVx, sVy, sVz, -400.0, 0.0, 0.0, 0.0, 0.0, 0.0);
        Check("400 m below reads radial -400", Near(below.RadialM, -400.0), below.RadialM.ToString("F3"));
        Check("...along and cross zero", Near(below.AlongM, 0.0) && Near(below.CrossM, 0.0),
              below.AlongM + "/" + below.CrossM);
        Check("...range 400", Near(below.RangeM, 400.0), below.RangeM.ToString("F3"));

        // 220 m ahead = +along (the velocity direction).
        LvlhState ahead = Lvlh.Project(sRx, sRy, sRz, sVx, sVy, sVz, 0.0, 220.0, 0.0, 0.0, 0.0, 0.0);
        Check("220 m in the +velocity direction reads along +220",
              Near(ahead.AlongM, 220.0) && Near(ahead.RadialM, 0.0), ahead.AlongM.ToString("F3"));

        // out of plane -> cross; a +Z rate -> +cross rate.
        LvlhState oop = Lvlh.Project(sRx, sRy, sRz, sVx, sVy, sVz, 0.0, 0.0, 50.0, 0.0, 0.0, 3.0);
        Check("50 m in +Z reads cross +50", Near(oop.CrossM, 50.0), oop.CrossM.ToString("F3"));
        Check("a +Z relative velocity reads cross-rate +3", Near(oop.CrossRateMps, 3.0),
              oop.CrossRateMps.ToString("F3"));

        // ---- a TILTED station (velocity carries a radial part - eccentric) still resolves the axes.
        // Station at +Y radius moving -X (with a radial +Y contamination that must be removed).
        double tRx = 0.0, tRy = 6.9e6, tRz = 0.0, tVx = -7400.0, tVy = 120.0, tVz = 0.0;
        // "ahead" here is the -X direction; put the ship 220 m along -X.
        LvlhState tilt = Lvlh.Project(tRx, tRy, tRz, tVx, tVy, tVz, -220.0, 0.0, 0.0, 0.0, 0.0, 0.0);
        Check("tilted station: 220 m along its velocity reads along +220",
              Near(tilt.AlongM, 220.0), tilt.AlongM.ToString("F3"));
        Check("tilted station: no radial leak from the removed velocity component",
              Near(tilt.RadialM, 0.0), tilt.RadialM.ToString("F6"));

        // ---- OffsetToWorld then Project round-trips (same basis both ways) ----
        double ox, oy, oz;
        Lvlh.OffsetToWorld(tRx, tRy, tRz, tVx, tVy, tVz, -400.0, 220.0, -30.0, out ox, out oy, out oz);
        LvlhState rt = Lvlh.Project(tRx, tRy, tRz, tVx, tVy, tVz, ox, oy, oz, 0.0, 0.0, 0.0);
        Check("round-trip radial", Near(rt.RadialM, -400.0), rt.RadialM.ToString("F4"));
        Check("round-trip along", Near(rt.AlongM, 220.0), rt.AlongM.ToString("F4"));
        Check("round-trip cross", Near(rt.CrossM, -30.0), rt.CrossM.ToString("F4"));

        // ---- the published WP0 offset, taken to world and back, is 400 m below ----
        double wx, wy, wz;
        Lvlh.OffsetToWorld(sRx, sRy, sRz, sVx, sVy, sVz,
                           WaypointApproach.WP0RadialM, WaypointApproach.WP0AlongM, 0.0,
                           out wx, out wy, out wz);
        Check("WP0 world offset points 400 m toward the body (-X here)",
              Near(wx, -400.0) && Near(wy, 0.0) && Near(wz, 0.0),
              wx.ToString("F2") + "," + wy.ToString("F2") + "," + wz.ToString("F2"));

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
