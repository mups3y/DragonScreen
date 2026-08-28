// Tests for L3 docking corridor / KOS-breach geometry (pure/DockCorridor.cs).
using System;
using DragonScreen;

public static class DockCorridorTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    static LvlhState Rel(double rx, double ry, double rz)
    { return new LvlhState { Rx = rx, Ry = ry, Rz = rz }; }

    public static int Run()
    {
        Console.WriteLine("DragonScreen L3 dock-corridor tests");
        const double kos = 200.0;
        double cone = 10.0 * Math.PI / 180.0;   // ~10° half-angle
        const double floor = 5.0;

        // OUTSIDE the KOS → corridor not enforced (true regardless of lateral offset).
        Check("outside KOS on the R-bar (400 below) is on-corridor", DockCorridor.OnCorridor(Rel(-400, 0, 0), kos, cone, floor), "");
        Check("outside KOS with a big lateral offset is still on-corridor", DockCorridor.OnCorridor(Rel(0, 300, 150), kos, cone, floor), "");
        Check("outside KOS is not a breach", !DockCorridor.Breached(Rel(-400, 0, 0), kos, cone, floor), "");

        // INSIDE the KOS, ON the V-bar axis → on-corridor.
        Check("inside KOS on-axis at 20 m is on-corridor", DockCorridor.OnCorridor(Rel(0, 20, 0), kos, cone, floor), "");
        Check("inside KOS at contact (on-axis) is on-corridor", DockCorridor.OnCorridor(Rel(0, 0.3, 0), kos, cone, floor), "");
        Check("inside KOS on-axis is not a breach", !DockCorridor.Breached(Rel(0, 20, 0), kos, cone, floor), "");

        // INSIDE the KOS, OFF the axis beyond the cone → breach.
        // at Ry=100, cone half-width = 100·tan(10°) ≈ 17.6 m; 60 m lateral is well outside.
        Check("inside KOS 60 m off-axis at 100 m along is off-corridor", !DockCorridor.OnCorridor(Rel(60, 100, 0), kos, cone, floor), "");
        Check("inside KOS 60 m off-axis is a BREACH", DockCorridor.Breached(Rel(60, 100, 0), kos, cone, floor), "");
        // a small offset inside the cone is fine (10 m < 17.6 m half-width at 100 m).
        Check("inside KOS 10 m off-axis at 100 m along is on-corridor", DockCorridor.OnCorridor(Rel(10, 100, 0), kos, cone, floor), "");

        // NEAR the port the cone floors at minHalfWidth (so a tight cone doesn't demand impossible precision).
        // at Ry=5, cone half-width = 5·tan(10°) ≈ 0.88 m, floored to 5 m; a 3 m offset is within the floor.
        Check("near the port the floor half-width applies (3 m offset ok)", DockCorridor.OnCorridor(Rel(3, 5, 0), kos, cone, floor), "");
        Check("near the port beyond the floor is a breach (8 m offset)", DockCorridor.Breached(Rel(8, 5, 0), kos, cone, floor), "");

        // lateral combines both off-axis components (Rx and Rz).
        Check("lateral is sqrt(Rx^2+Rz^2): 40+40 off-axis at 100 m is a breach",
              DockCorridor.Breached(Rel(40, 100, 40), kos, cone, floor), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
