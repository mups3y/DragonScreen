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

        // ================================================================================
        //  ⛔ THE GATE -> STANDOFF LEG. Its absence deadlocked every docking to 2026-08-12:
        //  1904 rows, all `ToGate`, parked at the corridorGate for six minutes across four attempts.
        // ================================================================================
        // The measured geometry from that flight: keep-out 31 m, so the corridorGate sits at the sphere
        // exit plus the 20 m pad, and the standoff is 25 m.
        // ⚠ THE PORT IS NOT AT THE STATION'S CENTRE, AND A FIXTURE THAT PUTS IT THERE IS
        // DEGENERATE - the axis misses the sphere entirely and GateDistanceM falls back to the
        // plain standoff, which quietly tests nothing. `c` runs from the PORT to the centre, so it
        // points opposite the outward axis: cDotU is negative and |c| is the port's offset in.
        double corridorKeepOut = 31.0;            // measured on the station, 2026-08-12
        double portOffset = 10.0;                 // port to station centre
        double corridorGate = DockGeometry.GateDistanceM(
            -portOffset, portOffset * portOffset, corridorKeepOut);
        Check("the corridorGate really is outside the standoff, which is why a leg is needed",
              corridorGate > DockGeometry.StandoffM + DockGeometry.StandoffToleranceM,
              "corridorGate " + corridorGate.ToString("F1") + " vs standoff " + DockGeometry.StandoffM);
        Check("...and sitting at the corridorGate does NOT count as being at the standoff",
              !DockGeometry.AtStandoff(corridorGate - DockGeometry.StandoffM),
              (corridorGate - DockGeometry.StandoffM).ToString("F1") + " m apart");
        Check("...but it DOES count as being at the corridorGate, which starts the corridor",
              DockGeometry.AtGate(0.0), "");
        Check("arriving with a few metres of residual drift still counts",
              DockGeometry.AtGate(DockGeometry.GateToleranceM - 1.0), "");
        Check("but being a long way off does not",
              !DockGeometry.AtGate(DockGeometry.GateToleranceM + 1.0), "");
        Check("the corridorGate tolerance is wider than the standoff's - it is a waypoint, not a hold",
              DockGeometry.GateToleranceM > DockGeometry.StandoffToleranceM, "");
        // And the far end: having run the corridor, the standoff must promote to axial.
        Check("reaching the standoff promotes", DockGeometry.AtStandoff(0.0), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
