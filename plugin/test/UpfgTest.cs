/*
 * Tests for UPFG (Unified Powered Flight Guidance). The real validation of a predictor-corrector guidance
 * is that it CONVERGES to a self-consistent solution whose desired cutoff satisfies the target
 * constraints, and that the thrust direction it commands is physically sane. That is the algorithm's
 * fixed point - a legitimate headless check, not a trajectory simulation. See pure/Upfg.cs.
 *
 * Scenario: the measured RSS M-Vac problem - a TWR ~0.82 upper stage from a 65 km, 2860 m/s (inertial),
 * ~18 deg flight-path-angle MECO, targeting a 200 km circular orbit. This is exactly where every pitch
 * heuristic failed; UPFG must solve it.
 */
using System;
using DragonScreen;
using MechJebLib.Primitives;

public static class UpfgTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen UPFG tests");

        double mu = 3.986004418e14;
        double Re = 6371000.0;

        // MECO state, in-plane (x = radial/up, y = downrange/prograde).
        double rmag = Re + 65000.0;
        V3 r = new V3(rmag, 0.0, 0.0);
        double vmag = 2860.0, fpa = 18.0 * Math.PI / 180.0;
        V3 v = new V3(vmag * Math.Sin(fpa), vmag * Math.Cos(fpa), 0.0);   // up + prograde

        // Target: 200 km circular. Orbit is in the xy-plane; h = r x v = +z, so Iy = -z (the sign trap).
        UpfgTarget t = new UpfgTarget();
        t.Iy = new V3(0.0, 0.0, -1.0);
        t.RadiusM = Re + 200000.0;
        t.SpeedMps = Math.Sqrt(mu / t.RadiusM);      // circular
        t.GammaRad = 0.0;

        // S2 M-Vac: 805 kN, Isp 345, ~100 t -> TWR ~0.82.
        UpfgVehicle veh = new UpfgVehicle();
        veh.ExhaustVel = 345.0 * 9.80665;
        veh.ThrustN = 805000.0;
        veh.MassKg = 100000.0;

        UpfgState s = Upfg.Init(r, v, mu, t, veh);
        Check("Init produces a positive time-to-go", s.TgoS > 0.0, s.TgoS.ToString("F1"));

        // Iterate the predictor-corrector on the fixed state; tgo must converge.
        double prevTgo = s.TgoS;
        UpfgGuidance g = new UpfgGuidance();
        double maxLateChange = 1.0;
        for (int i = 0; i < 25; i++)
        {
            g = Upfg.Step(r, v, mu, t, veh, ref s);
            if (i >= 20) maxLateChange = Math.Min(maxLateChange,
                Math.Abs(g.TgoS - prevTgo) / Math.Max(1.0, g.TgoS));
            prevTgo = g.TgoS;
        }
        Check("guidance is valid after iterating", g.Valid, "");
        Check("time-to-go CONVERGES (last iterations change < 1%)", maxLateChange < 0.01,
              (maxLateChange * 100.0).ToString("F3") + "%");
        Check("converged tgo is physical (0 < tgo < ve*m/F = ~420 s)",
              g.TgoS > 0.0 && g.TgoS < veh.ExhaustVel * veh.MassKg / veh.ThrustN,
              g.TgoS.ToString("F1") + " s");

        // The thrust direction must be a unit vector...
        Check("iF is a unit vector", Math.Abs(g.IF.magnitude - 1.0) < 1e-6, g.IF.magnitude.ToString("F6"));
        // ...in the orbital plane (no cross-track for an in-plane target)...
        Check("iF stays in the orbit plane (no z component)", Math.Abs(g.IF.z) < 1e-6, g.IF.z.ToString("E2"));
        // ...pointing generally prograde (positive downrange), not backwards...
        Check("iF points prograde (positive downrange)", g.IF.y > 0.0, g.IF.y.ToString("F3"));
        // ...and, from a shallow 18 deg climb needing to reach orbit, with an UPWARD (loft) component -
        // exactly the behaviour the fixed-pitch heuristics could not produce on their own.
        Check("iF carries an upward (loft) component early", g.IF.x > 0.0, g.IF.x.ToString("F3"));

        // The desired cutoff the solution converged to must satisfy the target radius.
        Check("the converged desired-cutoff radius matches the target",
              Math.Abs(s.Rd.magnitude - t.RadiusM) / t.RadiusM < 1e-3, s.Rd.magnitude.ToString("F0"));

        // A stage with almost no propellant time cannot reach it: tgo saturates below tu, not NaN.
        UpfgVehicle weak = veh; weak.ThrustN = 400000.0;   // TWR ~0.4
        UpfgState sw = Upfg.Init(r, v, mu, t, weak);
        UpfgGuidance gw = new UpfgGuidance();
        for (int i = 0; i < 25; i++) gw = Upfg.Step(r, v, mu, t, weak, ref sw);
        Check("a weaker stage still returns a finite, bounded tgo",
              !double.IsNaN(gw.TgoS) && gw.TgoS > 0.0
              && gw.TgoS < weak.ExhaustVel * weak.MassKg / weak.ThrustN, gw.TgoS.ToString("F1"));

        // ---- POINT-MASS CLOSURE: single-stepped (once per tick, as the glue does) UPFG must FLY the
        // measured engage state to the target orbit, not just converge on paper. This is the property that
        // actually matters and it caught nothing subtle in the algorithm - the algorithm is fine. What it
        // documents is the failure the GLUE guards: an Init seeded on ullage thrust (~1 kN) poisons tu and
        // lofts. See scratchpad/sim and AutoPilot.UpfgMinThrustKn. ----
        double apoGood, peGood;
        bool secoGood = FlyPointMass(mu, Re, 807000.0, "full thrust from t=0", out apoGood, out peGood);
        Check("single-stepped UPFG reaches SECO", secoGood, "");
        // A closed orbit near 200 km (coarse dt overshoots apoapsis a little), NOT a 2378 km loft.
        Check("insertion apoapsis within 80 km of 200", Math.Abs(apoGood - 200.0) < 80.0, apoGood.ToString("F0"));
        Check("insertion periapsis within 15 km of 200", Math.Abs(peGood - 200.0) < 15.0, peGood.ToString("F0"));

        // The poison: Init while the engine reads ullage thrust yields a non-physical tgo (the fingerprint
        // is `UPFG tgo 315343s`). The glue must never let this happen - hence UpfgMinThrustKn.
        UpfgVehicle full = veh; full.ThrustN = 807000.0; full.MassKg = 116600.0;
        UpfgVehicle ullage = full; ullage.ThrustN = 1200.0;         // ~1 kN, mid-ullage
        UpfgState sp = Upfg.Init(r, v, mu, t, ullage);
        Check("an ullage-thrust Init produces a non-physical tgo (why the glue gates on thrust)",
              sp.TgoS > full.ExhaustVel * full.MassKg / full.ThrustN * 10.0, sp.TgoS.ToString("F0") + " s");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // A minimal point-mass integrator flying UPFG once per tick (the glue's cadence). Engage state is the
    // measured RSS M-Vac problem (62 km, 2482 m/s inertial, ~21.7 deg FPA, ~116.6 t). Target rebuilt each
    // tick frame-agnostically, exactly as AutoPilot.UpfgFlyS2 does.
    static bool FlyPointMass(double mu, double Re, double thrust, string label, out double apoKm, out double peKm)
    {
        double ve = 345.0 * 9.80665, mass = 116600.0, dry = 14500.0, mdot = thrust / ve;
        V3 r = new V3(Re + 62000.0, 0, 0);
        double vmag = 2482.0, fpa = Math.Asin(918.0 / 2482.0);
        V3 v = new V3(vmag * Math.Sin(fpa), vmag * Math.Cos(fpa), 0);
        UpfgState s = new UpfgState();
        double dt = 0.5, tsim = 0; bool seco = false;
        while (mass > dry && tsim < 600 && !seco)
        {
            V3 up = V3.Normalize(r);
            V3 pro = V3.Normalize(v - V3.Dot(v, up) * up);
            UpfgTarget t = new UpfgTarget();
            t.Iy = V3.Normalize(V3.Cross(pro, up));
            t.RadiusM = Re + 200000.0; t.SpeedMps = Math.Sqrt(mu / t.RadiusM); t.GammaRad = 0.0;
            UpfgVehicle veh = new UpfgVehicle();
            veh.ExhaustVel = ve; veh.ThrustN = thrust; veh.MassKg = mass;
            UpfgGuidance g = Upfg.Step(r, v, mu, t, veh, ref s);
            if (g.TgoS <= 0.2) seco = true;
            V3 iF = g.Valid ? g.IF : pro;
            V3 grav = (-mu / (r.sqrMagnitude * r.magnitude)) * r;
            V3 acc = (thrust / mass) * iF + grav;
            v = v + acc * dt; r = r + v * dt; mass -= mdot * dt; tsim += dt;
        }
        double rm = r.magnitude, vm = v.magnitude, eps = vm * vm / 2.0 - mu / rm, sma = -mu / (2.0 * eps);
        V3 h = V3.Cross(r, v);
        double e = Math.Sqrt(Math.Max(0.0, 1.0 + 2.0 * eps * h.sqrMagnitude / (mu * mu)));
        apoKm = (sma * (1.0 + e) - Re) / 1000.0; peKm = (sma * (1.0 - e) - Re) / 1000.0;
        return seco;
    }
}
