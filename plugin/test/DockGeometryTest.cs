/*
 * DragonScreen headless tests - the docking geometry.
 *
 * The two that matter are both deadlocks the source hit and documented:
 *   * the sphere test evaluated from INSIDE the sphere is trivially "blocked", so the approach
 *     converged onto the hull and sat there at 0.03 m/s reporting "rounding hull"
 *   * a skirt target sitting exactly ON the sphere leaves us exactly on the sphere, which the test
 *     can never call clear - so the skirt aims at R + pad, and that gap IS the hysteresis
 */
using System;
using DragonScreen;

public static class DockGeometryTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen dock geometry tests");

        // ---- THE GATE IS ON THE SPHERE, NOT ON THE PORT ----
        // Berth on an arm tip: the port is 40 m from the station centre, the keep-out is 60 m, and
        // the port's axis points straight out along the arm. c runs port -> centre, so u.c = -40.
        double R = 60.0;
        double cDotU = -40.0;
        double cSqr = 40.0 * 40.0;
        double gate = DockGeometry.GateDistanceM(cDotU, cSqr, R);
        // Exit is at u.c + sqrt((u.c)^2 - |c|^2 + R^2) = -40 + sqrt(1600 - 1600 + 3600) = 20 m.
        Check("the gate is the sphere exit plus the pad",
              Math.Abs(gate - (20.0 + DockGeometry.KeepOutPadM)) < 1e-6, gate.ToString("F2"));
        // ...and NOT two arm-lengths out, which is what standing off from the PORT would give.
        Check("and it is nowhere near an arm-length past the hull",
              gate < R + DockGeometry.KeepOutPadM + 1.0, gate.ToString("F2"));
        Check("but never closer than the plain standoff",
              DockGeometry.GateDistanceM(0.0, 1e9, 1.0) >= DockGeometry.StandoffM, "");

        // ---- THE PATH TEST ----
        // Station centre 500 m abeam of a 1000 m segment: the segment passes 500 m away, clear of a
        // 60 m sphere.
        double segLen = 1000.0;
        double abeamSqr = 500.0 * 500.0 + 500.0 * 500.0;   // 500 along, 500 across
        Check("a path that passes well abeam is clear",
              DockGeometry.PathClear(Math.Sqrt(abeamSqr), abeamSqr, 500.0, segLen, R), "");
        // Station centre dead ahead at 300 m: the segment goes straight through it.
        Check("a path straight through the station is NOT clear",
              !DockGeometry.PathClear(300.0, 300.0 * 300.0, 300.0, segLen, R), "");
        // An obstacle BEHIND us is not in the way - the projection is clamped to the segment.
        Check("an obstacle behind us is not in the way",
              DockGeometry.PathClear(300.0, 300.0 * 300.0, -300.0, segLen, R), "");

        // ---- ⛔ THE DEADLOCK GUARD ----
        // Inside the sphere the closest approach is trivially <= R and every direction reads blocked.
        // 16.8 m from the centre of a 60 m sphere is the case that actually stuck.
        Check("inside the sphere, the path is always reported clear",
              DockGeometry.PathClear(16.8, 16.8 * 16.8, 16.8, 100.0, R), "");
        Check("exactly on the surface counts as inside",
              DockGeometry.PathClear(R, R * R, R, 100.0, R), "");
        Check("and just outside it starts testing again",
              !DockGeometry.PathClear(R + 0.1, (R + 0.1) * (R + 0.1), R + 0.1, 100.0, R), "");

        // ---- THE SKIRT AIMS OUTSIDE THE SPHERE, WHICH IS THE HYSTERESIS ----
        Check("the skirt aims at R plus the pad, never at R",
              DockGeometry.SkirtRadiusM(R) > R, DockGeometry.SkirtRadiusM(R).ToString("F1"));
        Check("by exactly the pad",
              Math.Abs(DockGeometry.SkirtRadiusM(R) - (R + DockGeometry.KeepOutPadM)) < 1e-9, "");
        // A capsule at the skirt radius is OUTSIDE, so the path test can flip back to clear - which
        // is the whole point. At R it could not.
        Check("a capsule at the skirt radius is outside the sphere",
              DockGeometry.SkirtRadiusM(R) > R, "");

        // ---- ARRIVAL ----
        Check("inside the tolerance is arrived",
              DockGeometry.AtStandoff(DockGeometry.StandoffToleranceM - 0.1), "");
        Check("outside it is not",
              !DockGeometry.AtStandoff(DockGeometry.StandoffToleranceM + 0.1), "");
        // The tolerance must be smaller than the standoff or "arrived" would include the port itself.
        Check("the tolerance is well inside the standoff",
              DockGeometry.StandoffToleranceM < DockGeometry.StandoffM, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
