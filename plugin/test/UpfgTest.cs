// Tests for UPFG (pure/Upfg.cs). A predictor-corrector's real validation is that it CONVERGES to a
// self-consistent fixed point whose desired cutoff satisfies the target, that its steering is physically
// sane, and — the property that actually matters — that single-stepped once per tick it FLIES the
// measured MECO state to the target orbit. Scenario: the RSS M-Vac problem (TWR ~0.82 from a 65 km MECO).
using System;
using DragonScreen;

public static class UpfgTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    const double Mu = 3.986004418e14, Re = 6371000.0;

    public static int Run()
    {
        Console.WriteLine("DragonScreen UPFG tests");

        // MECO state, in-plane: x = radial/up, y = downrange/prograde.
        Vec3 r = new Vec3(Re + 65000.0, 0, 0);
        double vmag = 2860.0, fpa = 18.0 * Math.PI / 180.0;
        Vec3 v = new Vec3(vmag * Math.Sin(fpa), vmag * Math.Cos(fpa), 0);

        UpfgTarget t = new UpfgTarget();
        t.Iy = new Vec3(0, 0, -1);                       // orbit in xy-plane, h=+z ⇒ iy=-z (the sign trap)
        t.RadiusM = Re + 200000.0;
        t.SpeedMps = Math.Sqrt(Mu / t.RadiusM);          // circular
        t.GammaRad = 0.0;

        // real S2 numbers (§5.0): MVac 805 kN, Isp 345 s, ~100 t → TWR ~0.82
        UpfgVehicle veh = new UpfgVehicle();
        veh.ExhaustVel = 345.0 * 9.80665;
        veh.ThrustN = 805000.0;
        veh.MassKg = 100000.0;

        UpfgState s = Upfg.Init(r, v, Mu, t, veh);
        Check("Init gives a positive time-to-go", s.Tgo >= 0.0, s.Tgo.ToString("F1"));

        // iterate the predictor-corrector on the FIXED state; tgo must converge to a fixed point.
        UpfgGuidance gd = new UpfgGuidance();
        double prev = 0.0, maxLate = 1.0;
        for (int i = 0; i < 30; i++)
        {
            gd = Upfg.Step(r, v, Mu, t, veh, ref s);
            if (i >= 25) maxLate = Math.Min(maxLate, Math.Abs(gd.TgoS - prev) / Math.Max(1.0, gd.TgoS));
            prev = gd.TgoS;
        }
        Check("guidance is valid after iterating", gd.Valid, "");
        Check("time-to-go CONVERGES (last iterations change < 1%)", maxLate < 0.01, (maxLate * 100).ToString("F3") + "%");
        double tuLimit = veh.ExhaustVel * veh.MassKg / veh.ThrustN;   // ~420 s
        Check("converged tgo is physical (0 < tgo < ve·m/F)", gd.TgoS > 0 && gd.TgoS < tuLimit, gd.TgoS.ToString("F1"));

        Check("iF is a unit vector", Math.Abs(gd.IF.Magnitude - 1.0) < 1e-6, gd.IF.Magnitude.ToString("F6"));
        Check("iF stays in the orbit plane (no cross-track)", Math.Abs(gd.IF.Z) < 1e-3, gd.IF.Z.ToString("E2"));
        Check("iF points prograde (positive downrange)", gd.IF.Y > 0.0, gd.IF.Y.ToString("F3"));
        Check("iF carries an upward (loft) component — the behaviour no fixed pitch gives", gd.IF.X > 0.0, gd.IF.X.ToString("F3"));

        Check("the converged desired-cutoff radius matches the target",
              Math.Abs(s.Rd.Magnitude - t.RadiusM) / t.RadiusM < 1e-3, s.Rd.Magnitude.ToString("F0"));

        // ---- POINT-MASS CLOSURE: single-stepped UPFG (once per tick) flies the measured engage state to orbit ----
        double apo, pe;
        bool seco = FlyPointMass(807000.0, out apo, out pe);
        Check("single-stepped UPFG reaches SECO", seco, "");
        Check("insertion apoapsis within 80 km of 200", Math.Abs(apo - 200.0) < 80.0, apo.ToString("F0") + " km");
        Check("insertion periapsis within 20 km of 200", Math.Abs(pe - 200.0) < 20.0, pe.ToString("F0") + " km");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // Minimal point-mass integrator flying UPFG once per tick (the glue's cadence), rebuilding the target
    // frame-agnostically each tick, exactly as the ascent conductor will. Engage: 62 km, 2482 m/s, ~21.7°, 116.6 t.
    static bool FlyPointMass(double thrust, out double apoKm, out double peKm)
    {
        double ve = 345.0 * 9.80665, mass = 116600.0, dry = 14500.0, mdot = thrust / ve;
        Vec3 r = new Vec3(Re + 62000.0, 0, 0);
        double vmag = 2482.0, fpa = Math.Asin(918.0 / 2482.0);
        Vec3 v = new Vec3(vmag * Math.Sin(fpa), vmag * Math.Cos(fpa), 0);
        UpfgState s = new UpfgState();
        double dt = 0.5, tsim = 0; bool seco = false;
        while (mass > dry && tsim < 600 && !seco)
        {
            Vec3 up = r.Normalized;
            Vec3 pro = (v - up * Vec3.Dot(v, up)).Normalized;
            UpfgTarget t = new UpfgTarget();
            t.Iy = Vec3.Cross(pro, up).Normalized;              // plane normal (opposite h)
            t.RadiusM = Re + 200000.0; t.SpeedMps = Math.Sqrt(Mu / t.RadiusM); t.GammaRad = 0.0;
            UpfgVehicle veh = new UpfgVehicle { ExhaustVel = ve, ThrustN = thrust, MassKg = mass };
            UpfgGuidance g = Upfg.Step(r, v, Mu, t, veh, ref s);
            if (g.TgoS <= 0.2) { seco = true; break; }
            Vec3 iF = g.Valid ? g.IF : pro;
            Vec3 grav = r * (-Mu / (r.SqrMagnitude * r.Magnitude));
            Vec3 acc = iF * (thrust / mass) + grav;
            v = v + acc * dt; r = r + v * dt; mass -= mdot * dt; tsim += dt;
        }
        double rm = r.Magnitude, vm = v.Magnitude, eps = vm * vm / 2.0 - Mu / rm, sma = -Mu / (2.0 * eps);
        Vec3 h = Vec3.Cross(r, v);
        double e = Math.Sqrt(Math.Max(0.0, 1.0 + 2.0 * eps * h.SqrMagnitude / (Mu * Mu)));
        apoKm = (sma * (1.0 + e) - Re) / 1000.0; peKm = (sma * (1.0 - e) - Re) / 1000.0;
        return seco;
    }
}
