/*
 * Tests for the drag-aware suicide-burn solver (pure/Hoverslam.cs). Property-based: the ignition altitude
 * must arrest the stage at the deck, and must move the RIGHT way with drag, spool and speed. Anchors
 * mirror scratchpad/hoverslam_val.py against the real 0824 landing (v_term 244, 31 t, 1925 kN, spool 3.5 s).
 */
using System;
using DragonScreen;

public static class HoverslamTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    static HoverslamInputs Flight()
    {
        double thrust = 1925.0;                       // 3-engine, kN
        return new HoverslamInputs
        {
            AltitudeM = 3000.0,
            VerticalSpeed = -244.0,                   // terminal descent
            MassT = 31.0,
            GravityMps2 = 9.8,
            ThrustKn = thrust,
            MdotTps = thrust / (282.0 * 9.8),         // Isp 282 s -> ~0.697 t/s
            DragRefAccel = 9.8,                       // at terminal, drag == gravity
            DragRefSpeed = 244.0,
            DeadTimeS = 5.4,                          // MEASURED settle + chamber build (flight_0824_031348)
            SpoolS = 1.2                              // MEASURED thrust ramp
        };
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen hoverslam solver tests");

        HoverslamInputs s = Flight();
        double hIgn = HoverslamSolver.IgnitionAltitude(s);

        // With the MEASURED 5.4 s dead time the stage free-falls ~1.3 km before it can brake, so the
        // ignition point sits ~1.8-2.0 km up - NOT the 925 m the dead-time-blind model gave (which lit at
        // 1571 m and hit the deck at 192 m/s on flight_0824_031348).
        Check("ignition altitude accounts for the dead-time free-fall (1500-2300 band)",
              hIgn > 1500.0 && hIgn < 2300.0, hIgn.ToString("F0") + " m");

        // The defining property: light the burn there and the stage rests AT the deck (v=0 at h~0).
        double rest = HoverslamSolver.StopAltitude(hIgn, s);
        Check("a full burn from the ignition altitude arrests the stage at the deck (|rest| < 5 m)",
              Math.Abs(rest) < 5.0, "rest " + rest.ToString("F1") + " m");

        // Drag HELPS - a drag-aware solve ignites LOWER than a drag-free one (waits longer, drag brakes).
        HoverslamInputs noDrag = s; noDrag.DragRefAccel = 0.0;
        double hIgnNoDrag = HoverslamSolver.IgnitionAltitude(noDrag);
        Check("drag lowers the ignition altitude (aware < drag-free)",
              hIgn < hIgnNoDrag - 20.0, hIgn.ToString("F0") + " vs " + hIgnNoDrag.ToString("F0"));

        // The SPOOL raises it - a slow engine must light HIGHER than an instant one.
        HoverslamInputs noSpool = s; noSpool.SpoolS = 0.0;
        double hIgnNoSpool = HoverslamSolver.IgnitionAltitude(noSpool);
        Check("the spool raises the ignition altitude (spool > instant)",
              hIgn > hIgnNoSpool + 20.0, hIgn.ToString("F0") + " vs " + hIgnNoSpool.ToString("F0"));

        // The DEAD TIME raises it a lot - the stage free-falls ~1.3 km at 244 m/s before braking, so a
        // solver blind to it lights far too low (the flight_0824_031348 crash). This is the headline fix.
        HoverslamInputs noDead = s; noDead.DeadTimeS = 0.0;
        double hIgnNoDead = HoverslamSolver.IgnitionAltitude(noDead);
        Check("the dead time raises the ignition altitude by ~the free-fall (>1 km higher)",
              hIgn > hIgnNoDead + 1000.0, hIgn.ToString("F0") + " vs no-dead " + hIgnNoDead.ToString("F0"));

        // And it beats the drag-blind, spool-blind closed form we replace - which under-calls it badly.
        double closedForm = 244.0 * 244.0 / (2.0 * (1925.0 / 31.0 - 9.8));
        Check("the closed-form StopDist under-calls the ignition altitude (why we lit too late)",
              hIgn > closedForm + 100.0, hIgn.ToString("F0") + " vs closed " + closedForm.ToString("F0"));

        // Monotonic in speed: falling faster must light higher.
        HoverslamInputs faster = s; faster.VerticalSpeed = -300.0;
        Check("faster descent ignites higher (monotonic in speed)",
              HoverslamSolver.IgnitionAltitude(faster) > hIgn, "");

        // Cannot-stop guard: a puny engine returns the current altitude so the caller lights immediately.
        HoverslamInputs weak = s; weak.ThrustKn = 200.0;   // TWR < 1
        Check("a stage that cannot stop returns the current altitude (light now)",
              Math.Abs(HoverslamSolver.IgnitionAltitude(weak) - weak.AltitudeM) < 1.0, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
