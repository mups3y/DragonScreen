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

        // ---- MULTI-STAGE (B5): the n=1 case must reproduce the proven single-stage law EXACTLY ----
        UpfgState sa = new UpfgState();
        UpfgGuidance ga = Upfg.Step(r, v, Mu, t, veh, ref sa);
        UpfgState sb = new UpfgState();
        UpfgStage[] one = { new UpfgStage { Mode = 1, ExhaustVel = veh.ExhaustVel, ThrustN = veh.ThrustN, M0 = veh.MassKg, MaxT = 1.0e6 } };
        UpfgGuidance gb = Upfg.Step(r, v, Mu, t, one, veh.MassKg, ref sb);
        Check("multi-stage n=1 reproduces the single-stage iF", (ga.IF - gb.IF).Magnitude < 1e-9, (ga.IF - gb.IF).Magnitude.ToString("E2"));
        Check("multi-stage n=1 reproduces the single-stage Tgo", Math.Abs(ga.TgoS - gb.TgoS) < 1e-6, Math.Abs(ga.TgoS - gb.TgoS).ToString("E2"));

        // ---- MULTI-STAGE point-mass closure: a 2-stage vehicle (low-Isp stage → high-Isp upper) flies the
        //      engage state to orbit, exercising the cross-stage tgoi1 accumulation AND the staging transition. ----
        double mapo, mpe;
        bool mseco = FlyPointMassMultiStage(out mapo, out mpe);
        Check("multi-stage UPFG reaches SECO", mseco, "");
        Check("multi-stage insertion apoapsis within 80 km of 200", Math.Abs(mapo - 200.0) < 80.0, mapo.ToString("F0") + " km");
        Check("multi-stage insertion periapsis within 30 km of 200", Math.Abs(mpe - 200.0) < 30.0, mpe.ToString("F0") + " km");

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

    // Two-stage point-mass flight: a lower-Isp active stage (index 0) whose full Δv is LESS than |Vgo| — so
    // multi-stage UPFG must plan across BOTH stages (cutoff in stage 1) — then a higher-Isp upper stage that
    // closes the orbit. Exercises the cross-stage tgoi1 accumulation and the staging jettison. UPFG state
    // carries across the stage change (Vgo is vehicle-independent); the stage list shrinks to [upper] after sep.
    static bool FlyPointMassMultiStage(out double apoKm, out double peKm)
    {
        double ve0 = 320.0 * 9.80665, ve1 = 345.0 * 9.80665;
        double thr0 = 950000.0, thr1 = 805000.0;
        double m00 = 116600.0, dry0 = 75000.0, m1 = 55000.0, dry1 = 13000.0;
        double mdot0 = thr0 / ve0, mdot1 = thr1 / ve1;
        double maxT0 = (m00 - dry0) / mdot0, maxT1 = (m1 - dry1) / mdot1;

        Vec3 r = new Vec3(Re + 62000.0, 0, 0);
        double vmag = 2482.0, fpa = Math.Asin(918.0 / 2482.0);
        Vec3 v = new Vec3(vmag * Math.Sin(fpa), vmag * Math.Cos(fpa), 0);

        UpfgState s = new UpfgState();
        double mass = m00, dt = 0.5, tsim = 0, t0burn = 0;
        int stage = 0; bool seco = false;

        while (tsim < 900 && !seco)
        {
            double thr, mdot; UpfgStage[] stages;
            if (stage == 0)
            {
                thr = thr0; mdot = mdot0;
                stages = new UpfgStage[] {
                    new UpfgStage { Mode = 1, ExhaustVel = ve0, ThrustN = thr0, M0 = m00, MaxT = maxT0 - t0burn },
                    new UpfgStage { Mode = 1, ExhaustVel = ve1, ThrustN = thr1, M0 = m1,  MaxT = maxT1 } };
            }
            else
            {
                thr = thr1; mdot = mdot1;
                stages = new UpfgStage[] {
                    new UpfgStage { Mode = 1, ExhaustVel = ve1, ThrustN = thr1, M0 = m1, MaxT = maxT1 } };
            }

            Vec3 up = r.Normalized;
            Vec3 pro = (v - up * Vec3.Dot(v, up)).Normalized;
            UpfgTarget t = new UpfgTarget { Iy = Vec3.Cross(pro, up).Normalized, RadiusM = Re + 200000.0,
                                            SpeedMps = Math.Sqrt(Mu / (Re + 200000.0)), GammaRad = 0.0 };

            UpfgGuidance g = Upfg.Step(r, v, Mu, t, stages, mass, ref s);
            if (g.TgoS <= 0.2) { seco = true; break; }
            Vec3 iF = g.Valid ? g.IF : pro;
            Vec3 grav = r * (-Mu / (r.SqrMagnitude * r.Magnitude));
            Vec3 acc = iF * (thr / mass) + grav;
            v = v + acc * dt; r = r + v * dt; mass -= mdot * dt; tsim += dt;

            if (stage == 0)
            {
                t0burn += dt;
                if (t0burn >= maxT0 || mass <= dry0) { stage = 1; mass = m1; }   // jettison the spent lower stage
            }
            else if (mass <= dry1) break;                                        // ran dry before SECO
        }

        double rm = r.Magnitude, vm = v.Magnitude, eps = vm * vm / 2.0 - Mu / rm, sma = -Mu / (2.0 * eps);
        Vec3 h = Vec3.Cross(r, v);
        double e = Math.Sqrt(Math.Max(0.0, 1.0 + 2.0 * eps * h.SqrMagnitude / (Mu * Mu)));
        apoKm = (sma * (1.0 + e) - Re) / 1000.0; peKm = (sma * (1.0 - e) - Re) / 1000.0;
        return seco;
    }
}
