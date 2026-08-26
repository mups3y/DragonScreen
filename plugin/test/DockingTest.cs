// Tests for L3 docking: the glideslope servo (pure/DockControl.cs) and the L-approach FSM
// (pure/DockApproach.cs) — waypoint holds with GO gates, KOS-breach abort, and full-control aim.
using System;
using DragonScreen;

public static class DockingTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, got.ToString("F4") + " vs " + want.ToString("F4")); }

    static DockInputs In(double rx, double ry, double rz, bool corridor = true)
    {
        DockInputs s = new DockInputs();
        s.Valid = true; s.KosRadiusM = 200; s.WP0BelowM = 400; s.WP1FrontM = 200; s.WP2FrontM = 20;
        s.ArriveTolM = 5; s.CorridorOk = corridor;
        s.Rel = new LvlhState { Rx = rx, Ry = ry, Rz = rz };
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen L3 docking tests");

        // ---- GLIDESLOPE servo ----
        Near("speed cap is the contact speed at the port", DockControl.SpeedCap(0, 0.08, 1.0, 200), 0.08, 1e-9);
        Near("speed cap is the far speed at the taper range", DockControl.SpeedCap(200, 0.08, 1.0, 200), 1.0, 1e-9);
        Check("speed cap tapers with range", DockControl.SpeedCap(100, 0.08, 1.0, 200) > 0.08 && DockControl.SpeedCap(100, 0.08, 1.0, 200) < 1.0, "");
        Check("on target with no rate → no acceleration", DockControl.Accel(0, 0, 0.08, 0.1, 1.0) == 0.0, "");
        Check("a large error commands the capped closing speed (accel toward it)",
              DockControl.Accel(100, 0, 0.08, 0.1, 1.0) < 0.0, "");   // err>0 → close in −direction
        Check("closing speed never exceeds the cap",
              Math.Abs(DockControl.Accel(100, 0, 0.08, 0.1, 1.0)) <= 0.08 + 1e-9, "");
        DockControl.Demand dm = DockControl.Translate(0, 50, 0, 0, 0, 0, 0.08, 1.0, 200, 0.1, 1.0);
        Check("translate closes along-track toward the target", dm.Along < 0.0, dm.Along.ToString("F3"));

        // ---- FULL CONTROL: AimLvlh unit in every phase incl. invalid ----
        foreach (DockPhase ph in new[] { DockPhase.Idle, DockPhase.WP0Hold, DockPhase.ToWP1, DockPhase.WP1Hold,
                                         DockPhase.ToWP2, DockPhase.WP2Hold, DockPhase.Contact, DockPhase.Abort })
        {
            DockCommand cc = DockApproach.Guide(In(-400, 0, 0), ph);
            Check("AimLvlh is a UNIT vector in " + ph, Math.Abs(cc.AimLvlh.Magnitude - 1.0) < 1e-6, cc.AimLvlh.Magnitude.ToString("F6"));
        }
        Check("invalid vessel still gets a unit aim",
              Math.Abs(DockApproach.Guide(new DockInputs { Valid = false }, DockPhase.WP0Hold).AimLvlh.Magnitude - 1.0) < 1e-6, "");
        // the docking ring points AT the station (−relative position)
        DockCommand below = DockApproach.Guide(In(-400, 0, 0), DockPhase.WP0Hold);
        Check("from 400 m below, the ring points UP at the station (+radial)", below.AimLvlh.X > 0.99, below.AimLvlh.X.ToString("F3"));

        // ---- L-APPROACH sequence with GO gates ----
        DockInputs at0 = In(-400, 0, 0); at0.GoWP0 = false;
        Check("WP0 holds without GO", DockApproach.Guide(at0, DockPhase.WP0Hold).Phase == DockPhase.WP0Hold && DockApproach.Guide(at0, DockPhase.WP0Hold).Hold, "");
        at0.GoWP0 = true;
        Check("WP0 GO → swing to WP1", DockApproach.Guide(at0, DockPhase.WP0Hold).Phase == DockPhase.ToWP1, "");
        Check("reaching WP1 → WP1 hold", DockApproach.Guide(In(0, 200, 0), DockPhase.ToWP1).Phase == DockPhase.WP1Hold, "");
        DockInputs at1 = In(0, 200, 0); at1.GoWP1 = true;
        Check("WP1 GO → close to WP2", DockApproach.Guide(at1, DockPhase.WP1Hold).Phase == DockPhase.ToWP2, "");
        Check("reaching WP2 → WP2 hold", DockApproach.Guide(In(0, 20, 0), DockPhase.ToWP2).Phase == DockPhase.WP2Hold, "");
        DockInputs at2 = In(0, 20, 0); at2.GoWP2 = true;
        Check("WP2 GO for docking → contact", DockApproach.Guide(at2, DockPhase.WP2Hold).Phase == DockPhase.Contact, "");
        Check("contact within 0.3 m → captured/docked",
              DockApproach.Guide(In(0, 0.2, 0), DockPhase.Contact).Phase == DockPhase.Captured
              && DockApproach.Guide(In(0, 0.2, 0), DockPhase.Contact).Docked, "");

        // ---- ANY unplanned KOS breach → automatic abort ----
        Check("an unplanned KOS penetration commands ABORT",
              DockApproach.Guide(In(0, 100, 0, false), DockPhase.ToWP1).Phase == DockPhase.Abort, "");
        Check("on the planned corridor, inside the KOS is fine (no abort)",
              DockApproach.Guide(In(0, 100, 0, true), DockPhase.ToWP2).Phase != DockPhase.Abort, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
