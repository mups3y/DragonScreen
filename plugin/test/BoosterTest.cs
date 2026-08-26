// Tests for L3 booster recovery: the hoverslam ignition solver (pure/Hoverslam.cs), the grid-fin
// steering law (pure/GridFin.cs), and the recovery FSM (pure/BoosterDescent.cs). The FSM's headline
// contract is PERFECT CONTROL — a definite unit AimForward at all times and a capped, held AoA.
using System;
using DragonScreen;

public static class BoosterTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    static HoverslamInputs Flight()
    {
        return new HoverslamInputs {
            AltitudeM = 3000.0, DescentSpeedMps = 244.0,
            ThrustAccelMps2 = 2227000.0 / 31000.0,   // 3-engine / 31 t ≈ 71.8 m/s²
            GravityMps2 = 9.8, TerminalSpeedMps = 244.0, DeadTimeS = 6.0, SpoolS = 0.0 };
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen L3 booster tests");

        // ---- HOVERSLAM ignition solver ----
        HoverslamInputs s = Flight();
        double ign = Hoverslam.IgnitionAltitude(s);
        Check("ignition altitude is a sane band (1-3 km)", ign > 1000.0 && ign < 3000.0, ign.ToString("F0") + " m");

        HoverslamInputs noDead = s; noDead.DeadTimeS = 0.0;
        Check("the dead-time free-fall RAISES the ignition altitude (>1 km higher)",
              ign > Hoverslam.IgnitionAltitude(noDead) + 1000.0, ign.ToString("F0") + " vs " + Hoverslam.IgnitionAltitude(noDead).ToString("F0"));

        HoverslamInputs noDrag = s; noDrag.TerminalSpeedMps = 1e9;   // effectively drag-free
        Check("drag LOWERS the ignition altitude (drag brakes for free)",
              ign < Hoverslam.IgnitionAltitude(noDrag) - 10.0, ign.ToString("F0") + " vs " + Hoverslam.IgnitionAltitude(noDrag).ToString("F0"));

        HoverslamInputs spool = s; spool.SpoolS = 3.0;
        Check("a slow spool RAISES the ignition altitude",
              Hoverslam.IgnitionAltitude(spool) > ign, "");

        HoverslamInputs faster = s; faster.DescentSpeedMps = 300.0; faster.TerminalSpeedMps = 300.0;
        Check("a faster descent ignites HIGHER (monotonic in speed)", Hoverslam.IgnitionAltitude(faster) > ign, "");

        HoverslamInputs weak = s; weak.ThrustAccelMps2 = 5.0;   // TWR < 1: cannot decelerate
        Check("a stage that cannot arrest lights immediately (returns current altitude)",
              Math.Abs(Hoverslam.IgnitionAltitude(weak) - weak.AltitudeM) < 1.0, "");

        // ---- GRID-FIN steering: controlled, capped, points the correction toward −error ----
        GridFinInputs g0 = new GridFinInputs { AoaMaxDeg = 20.0, GainDegPerKm = 4.0, LeadTauS = 3.0 };
        GridFinCommand c0 = GridFin.Steer(g0);
        Check("no error → no AoA, no tilt", c0.AoaDeg == 0.0 && c0.TiltDown == 0.0, "");

        GridFinInputs gLong = g0; gLong.DownrangeErrM = 2000.0;   // 2 km long
        GridFinCommand cL = GridFin.Steer(gLong);
        Check("a downrange overshoot commands AoA", cL.AoaDeg > 0.0, cL.AoaDeg.ToString("F1"));
        Check("...and tilts the correction back (−downrange)", cL.TiltDown < -0.99, cL.TiltDown.ToString("F2"));

        GridFinInputs gCross = g0; gCross.CrossrangeErrM = 2000.0;
        GridFinCommand cC = GridFin.Steer(gCross);
        Check("a crossrange error tilts across (−cross)", cC.TiltCross < -0.99, cC.TiltCross.ToString("F2"));
        Check("the tilt is a unit direction", Math.Abs(Math.Sqrt(cC.TiltDown * cC.TiltDown + cC.TiltCross * cC.TiltCross) - 1.0) < 1e-6, "");

        GridFinInputs gHuge = g0; gHuge.DownrangeErrM = 100000.0;   // 100 km
        Check("AoA is CAPPED — no wild angle of attack", GridFin.Steer(gHuge).AoaDeg <= 20.0 + 1e-9, GridFin.Steer(gHuge).AoaDeg.ToString("F1"));
        Check("a bigger error commands a bigger AoA (until the cap)", cL.AoaDeg < GridFin.Steer(gHuge).AoaDeg, "");

        GridFinInputs gLead = g0; gLead.DownrangeErrM = 1000.0; gLead.DownrangeRateMps = 100.0;
        GridFinInputs gNoLead = g0; gNoLead.DownrangeErrM = 1000.0; gNoLead.DownrangeRateMps = 0.0;
        Check("the lead term anticipates a growing error (bigger AoA)", GridFin.Steer(gLead).AoaDeg > GridFin.Steer(gNoLead).AoaDeg, "");

        // ---- THE FSM: PERFECT CONTROL — a definite UNIT AimForward at all times ----
        BoosterInputs b = Booster();
        foreach (BoosterPhase ph in new[] { BoosterPhase.Idle, BoosterPhase.Flip, BoosterPhase.EntryBurn,
                                            BoosterPhase.AeroDescent, BoosterPhase.LandingBurn, BoosterPhase.Landed })
        {
            BoosterCommand cc = BoosterDescent.Guide(b, ph);
            Check("AimForward is a UNIT vector in phase " + ph, Math.Abs(cc.AimForward.Magnitude - 1.0) < 1e-6, cc.AimForward.Magnitude.ToString("F6"));
            Check("AoA is capped in phase " + ph, cc.AoaDeg <= 20.0 + 1e-9, cc.AoaDeg.ToString("F1"));
        }
        // even an INVALID vessel gets a defined attitude (never drifts)
        BoosterInputs bad = new BoosterInputs { Valid = false, Up = new Vec3(1, 0, 0) };
        Check("an invalid vessel still gets a defined aim (never uncommanded)",
              Math.Abs(BoosterDescent.Guide(bad, BoosterPhase.Flip).AimForward.Magnitude - 1.0) < 1e-6, "");

        // engines-first: AimForward opposes the surface velocity
        BoosterCommand flip = BoosterDescent.Guide(b, BoosterPhase.Flip);
        Check("Flip points engines-first (retrograde)", Vec3.Dot(flip.AimForward, b.SurfaceVelocity.Normalized) < -0.9, "");

        // FSM transitions on state
        BoosterInputs high = Booster(); high.AltitudeM = 80000.0; high.SpeedMps = 2000.0;
        Check("stays in Flip above the entry-burn altitude", BoosterDescent.Guide(high, BoosterPhase.Flip).Phase == BoosterPhase.Flip, "");
        BoosterInputs entry = Booster(); entry.AltitudeM = 60000.0; entry.SpeedMps = 2000.0;
        Check("starts the entry burn descending through 70 km fast", BoosterDescent.Guide(entry, BoosterPhase.Flip).Phase == BoosterPhase.EntryBurn, "");
        BoosterCommand eb = BoosterDescent.Guide(entry, BoosterPhase.EntryBurn);
        Check("entry burn is 3 engines at full", eb.EngineMode == 3 && eb.Throttle == 1.0, "");
        BoosterInputs bled = Booster(); bled.SpeedMps = 1200.0; bled.AltitudeM = 40000.0;
        Check("entry burn cuts to the aero descent at the survivable speed",
              BoosterDescent.Guide(bled, BoosterPhase.EntryBurn).Phase == BoosterPhase.AeroDescent, "");

        // aero descent flies retro + a small AoA (the aim tilts off retro by exactly the AoA)
        BoosterInputs aero = Booster(); aero.AltitudeM = 20000.0; aero.Fin.DownrangeErrM = 3000.0;
        BoosterCommand ad = BoosterDescent.Guide(aero, BoosterPhase.AeroDescent);
        Check("aero descent commands a held AoA", ad.AoaDeg > 0.0 && ad.AoaDeg <= 20.0, ad.AoaDeg.ToString("F1"));
        double tiltAngle = Math.Acos(Math.Max(-1, Math.Min(1, Vec3.Dot(ad.AimForward, Retro(aero))))) * 180 / Math.PI;
        Check("the aim is retro tilted by exactly the commanded AoA", Math.Abs(tiltAngle - ad.AoaDeg) < 0.5, tiltAngle.ToString("F2") + " vs " + ad.AoaDeg.ToString("F2"));

        // landing burn: braking, legs low
        BoosterInputs land = Booster(); land.AltitudeM = 400.0; land.DescentSpeedMps = 50.0;
        BoosterCommand lb = BoosterDescent.Guide(land, BoosterPhase.LandingBurn);
        Check("landing burn is the SINGLE CENTRE engine (no re-ignition/spool 3->1 mid-burn)",
              lb.Throttle == 1.0 && lb.EngineMode == 1, "mode=" + lb.EngineMode);
        Check("legs deploy in the final hundreds of metres", lb.DeployLegs, "");
        // and the entry burn is the three-engine mode (a different, single-ignition mode)
        Check("entry burn uses the 3-engine mode, landing the 1-engine mode (distinct ignitions)",
              eb.EngineMode == 3 && lb.EngineMode == 1, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    static Vec3 Retro(BoosterInputs b) { return (-b.SurfaceVelocity).Normalized; }

    static BoosterInputs Booster()
    {
        BoosterInputs b = new BoosterInputs();
        b.Valid = true;
        b.Up = new Vec3(1, 0, 0);
        b.SurfaceVelocity = new Vec3(-200, 50, 0);   // descending 200 m/s, 50 downrange
        b.AltitudeM = 5000.0; b.SpeedMps = 206.0; b.DescentSpeedMps = 200.0;
        b.AllNominal = true; b.OffsetToMissM = 0;
        b.Fin = new GridFinInputs { AoaMaxDeg = 20.0, GainDegPerKm = 4.0, LeadTauS = 3.0 };
        b.Land = new HoverslamInputs {
            AltitudeM = 5000.0, DescentSpeedMps = 200.0, ThrustAccelMps2 = 71.8,
            GravityMps2 = 9.8, TerminalSpeedMps = 244.0, DeadTimeS = 6.0, SpoolS = 0.0 };
        return b;
    }
}
