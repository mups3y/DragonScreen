// Tests for L3 ascent: the launch azimuth (pure/LaunchAzimuth.cs) and the S1 pitch program + FSM
// (pure/Ascent.cs). The pitch program is the real DM-1 profile; the FSM must advance only on state.
using System;
using DragonScreen;

public static class AscentTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, got.ToString("F3") + " vs " + want.ToString("F3")); }

    static AscentInputs Pad()
    {
        AscentInputs s = new AscentInputs();
        s.Valid = true; s.SurfaceSpeedMps = 0; s.AltitudeM = 0; s.DynamicPressurePa = 0;
        s.MassKg = 500000; s.FullThrustN = 7000000; s.GLimitG = 4.0;
        s.TargetApoapsisM = 200000; s.ApoapsisM = 0; s.SecondStage = false;
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen ascent + launch-azimuth tests");

        // ---- LAUNCH AZIMUTH ----
        double az;
        Check("51.6 deg from the Cape (28.5N) resolves", LaunchAzimuth.InertialRad(D(51.6), D(28.5), out az), "");
        Near("...inertial azimuth ~45 deg", LaunchAzimuth.Deg(az), 45.05, 0.3);
        Check("due-east for i == latitude", LaunchAzimuth.InertialRad(D(28.5), D(28.5), out az) && Math.Abs(LaunchAzimuth.Deg(az) - 90.0) < 0.1, "");
        Check("an inclination below the launch latitude is UNREACHABLE",
              !LaunchAzimuth.InertialRad(D(20.0), D(28.5), out az), "");
        Check("polar (90 deg) is due north inertially",
              LaunchAzimuth.InertialRad(D(90.0), D(28.5), out az) && Math.Abs(LaunchAzimuth.Deg(az)) < 0.1, "");

        // ground correction: Earth's spin pulls the 51.6 heading toward north (the eastward assist).
        double azg;
        Check("ground azimuth resolves",
              LaunchAzimuth.GroundRad(D(51.6), D(28.5), 7800.0, 6371000.0, 7.292e-5, false, out azg), "");
        Check("...and the eastward assist pulls it north of the inertial 45",
              LaunchAzimuth.Deg(azg) < 45.05 && LaunchAzimuth.Deg(azg) > 40.0, LaunchAzimuth.Deg(azg).ToString("F1"));
        // Fram2's southward polar pass is ~180 deg
        Check("descending polar pass heads south (~180)",
              LaunchAzimuth.GroundRad(D(90.0), D(28.5), 7800.0, 6371000.0, 7.292e-5, true, out azg)
              && Math.Abs(LaunchAzimuth.Deg(azg) - 180.0) < 5.0, LaunchAzimuth.Deg(azg).ToString("F1"));

        // ---- S1 PITCH PROGRAM (FLATTENED from raw DM-1 — ground truth beats the doc) ----
        // ⛔ The raw DM-1 curve (47° final, shape 0.6) OVER-LOFTED our vehicle: flight 090123 reached MECO at
        // fpa 51°, arced to a 228 km apoapsis and went suborbital. Our stack's TWR differs from the real F9, so
        // it needs a FLATTER program than DM-1 to hold apoapsis near target — the flight CSV outranks the .md
        // template (authority: live > CSV > docs). These assert the flattened curve (30° final, shape 0.5);
        // still vertical to the kick, still monotonic, just pitches over sooner and further.
        Near("starts vertical", Ascent.PitchAtSpeed(0.0), 90.0, 1e-9);
        Near("still vertical below the turn-start speed", Ascent.PitchAtSpeed(55.0), 90.0, 1e-9);
        Near("~71 deg near 235 m/s (flattened)", Ascent.PitchAtSpeed(235.0), 71.2, 1.0);
        Near("~50 deg near 880 m/s (flattened)", Ascent.PitchAtSpeed(880.0), 49.7, 2.0);
        Near("final pitch ~30 deg at the staging speed", Ascent.PitchAtSpeed(1881.0), 30.0, 0.1);
        double prev = 91.0;
        for (double v = 0; v <= 2500; v += 50.0)
        {
            double p = Ascent.PitchAtSpeed(v);
            Check("pitch never below the horizon at " + v, p >= 0.0 && p <= 90.0, p.ToString("F1"));
            Check("pitch is monotonic non-increasing at " + v, p <= prev + 1e-9, p.ToString("F2") + " after " + prev.ToString("F2"));
            prev = p;
        }

        // ---- THE FSM advances on state, not a clock ----
        AscentInputs s = Pad();
        AscentCommand c = Ascent.Guide(s, AscentPhase.Idle);
        Check("starts a vertical rise", c.Phase == AscentPhase.VerticalRise, c.Phase.ToString());
        Check("vertical rise is full throttle", c.Throttle == 1.0, c.Throttle.ToString("F2"));
        Check("vertical rise points up", c.PitchDeg > 89.0, c.PitchDeg.ToString("F1"));

        s.SurfaceSpeedMps = 100.0;
        c = Ascent.Guide(s, AscentPhase.VerticalRise);
        Check("turns once past the kick speed", c.Phase == AscentPhase.GravityTurn, c.Phase.ToString());
        c = Ascent.Guide(s, AscentPhase.GravityTurn);
        Near("gravity-turn pitch follows the program", c.PitchDeg, Ascent.PitchAtSpeed(100.0), 0.5);

        s.SurfaceSpeedMps = 1950.0;
        c = Ascent.Guide(s, AscentPhase.GravityTurn);
        Check("commands MECO at the staging speed", c.Phase == AscentPhase.Meco, c.Phase.ToString());
        c = Ascent.Guide(s, AscentPhase.Meco);
        Check("MECO cuts the throttle", c.Throttle == 0.0, "");
        Check("MECO commands staging", c.Stage, "");
        Check("MECO is a cutoff", c.Cutoff, "");
        Check("...and moves to the coast", c.Phase == AscentPhase.Coast, c.Phase.ToString());

        c = Ascent.Guide(s, AscentPhase.Coast);
        Check("coast waits for the second stage", c.Phase == AscentPhase.Coast, c.Phase.ToString());
        s.SecondStage = true;
        c = Ascent.Guide(s, AscentPhase.Coast);
        Check("hands to the S2 burn when the MVac lights", c.Phase == AscentPhase.S2Burn, c.Phase.ToString());

        // runaway backstop
        s = Pad(); s.ApoapsisM = 400000.0;   // 1.5x target is 300 km
        c = Ascent.Guide(s, AscentPhase.GravityTurn);
        Check("runaway apoapsis aborts the burn", c.Phase == AscentPhase.Done && c.Cutoff, c.Phase.ToString());

        // throttle stays in [0,1] whatever the inputs
        s = Pad(); s.DynamicPressurePa = 40000.0; s.SurfaceSpeedMps = 300.0;
        c = Ascent.Guide(s, AscentPhase.GravityTurn);
        Check("throttle stays within [0,1]", c.Throttle >= 0.0 && c.Throttle <= 1.0, c.Throttle.ToString("F3"));
        Check("the max-Q bucket throttled down", c.Throttle < 1.0, c.Throttle.ToString("F3"));

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    static double D(double deg) { return deg * Math.PI / 180.0; }
}
