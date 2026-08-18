/*
 * DragonScreen headless tests - the de-orbit ignition-point solver.
 *
 * The orbital math has closed-form answers, so it is asserted against arithmetic: a circular orbit's
 * periapsis IS its radius; a known ellipse recovers its known periapsis; a retrograde burn sized to a
 * target periapsis produces exactly that periapsis. The search is asserted against a function whose
 * minimum is known.
 */
using System;
using DragonScreen;

public static class DeorbitPointTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    const double Mu = 3.5316e12;             // Kerbin
    const double Rk = 600000.0;
    const double AtmH = 70000.0;

    // Kerbin's atmosphere, close enough for a test - same model TrajectoryTest uses.
    static double Density(double alt)
    {
        if (alt < 0.0 || alt >= AtmH) return 0.0;
        return 1.225 * Math.Exp(-alt / 5600.0);
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen de-orbit point tests");

        // ---- PERIAPSIS OF A CIRCULAR ORBIT IS ITS RADIUS ----
        double rc = Rk + 100000.0;
        double vc = Math.Sqrt(Mu / rc);
        double rpCirc = DeorbitPoint.PeriapsisRadius(rc, 0, 0, 0, vc, 0, Mu);
        Check("a circular orbit's periapsis equals its radius",
              Math.Abs(rpCirc - rc) < 1.0, rpCirc.ToString("F1") + " vs " + rc.ToString("F1"));

        // ---- A KNOWN ELLIPSE RECOVERS ITS KNOWN PERIAPSIS ----
        // At apoapsis ra=800 km with periapsis rp=600 km: a=700 km, v_apo = sqrt(mu(2/ra - 1/a)).
        double ra = Rk + 200000.0, rpKnown = Rk;
        double a = 0.5 * (ra + rpKnown);
        double vApo = Math.Sqrt(Mu * (2.0 / ra - 1.0 / a));
        double rpEll = DeorbitPoint.PeriapsisRadius(ra, 0, 0, 0, vApo, 0, Mu);
        Check("a known ellipse recovers its periapsis",
              Math.Abs(rpEll - rpKnown) < 5.0, rpEll.ToString("F1") + " vs " + rpKnown.ToString("F1"));

        // ---- A RETROGRADE BURN HITS THE PERIAPSIS IT WAS SIZED FOR ----
        // From the 100 km circular orbit, size a burn to a -31.8 km periapsis (the de-orbit floor).
        double targetRp = Rk + DeorbitBurn.PeriapsisTargetM;           // 600000 - 31800
        double dv = DeorbitPoint.DvForPeriapsis(rc, 0, 0, 0, vc, 0, Mu, targetRp);
        Check("lowering periapsis needs a real, positive burn", dv > 10.0, dv.ToString("F1"));
        double s = 1.0 - dv / vc;
        double rpAfter = DeorbitPoint.PeriapsisRadius(rc, 0, 0, 0, vc * s, 0, Mu);
        Check("and the burn produces exactly that periapsis",
              Math.Abs(rpAfter - targetRp) < 50.0, rpAfter.ToString("F1") + " vs " + targetRp.ToString("F1"));

        // Monotonicity: a lower target needs more dv.
        double dvShallow = DeorbitPoint.DvForPeriapsis(rc, 0, 0, 0, vc, 0, Mu, Rk - 10000.0);
        double dvDeep = DeorbitPoint.DvForPeriapsis(rc, 0, 0, 0, vc, 0, Mu, Rk - 60000.0);
        Check("a deeper periapsis costs more dv than a shallow one", dvDeep > dvShallow,
              dvDeep.ToString("F1") + " vs " + dvShallow.ToString("F1"));

        // ---- ALREADY STEEP ENOUGH: NO BURN ----
        // An orbit whose periapsis is already below the target must not be told to add energy.
        double vLow = vc * 0.7;                                         // a decaying ellipse
        double rpLow = DeorbitPoint.PeriapsisRadius(rc, 0, 0, 0, vLow, 0, Mu);
        Check("the low orbit really is below the target", rpLow < targetRp, rpLow.ToString("F1"));
        Check("an orbit already below the target needs no burn",
              DeorbitPoint.DvForPeriapsis(rc, 0, 0, 0, vLow, 0, Mu, targetRp) == 0.0, "");

        // ---- THE SEARCH FINDS A KNOWN MINIMUM ----
        // A V-shaped miss with its floor at ut = 1234 must be found to within the refine floor.
        double known = 1234.0;
        IgnitionMissAtUt f = delegate(double ut)
        {
            IgnitionMiss m = new IgnitionMiss();
            m.Ok = true; m.MissM = Math.Abs(ut - known); m.PeriapsisM = -20000.0; m.DvMps = 100.0;
            return m;
        };
        double bestUt;
        IgnitionMiss best = DeorbitPoint.Search(1000.0, 1500.0, f, out bestUt);
        Check("the ignition search converges on the minimum",
              best.Ok && Math.Abs(bestUt - known) < 1.0, "ut " + bestUt.ToString("F2"));
        Check("and reports the miss there is near zero",
              best.MissM < 1.0, best.MissM.ToString("F2"));

        // A delegate that never lands leaves Ok false rather than inventing a time.
        IgnitionMissAtUt none = delegate(double ut) { return new IgnitionMiss(); };  // Ok defaults false
        double u2;
        IgnitionMiss noneRes = DeorbitPoint.Search(1000.0, 1500.0, none, out u2);
        Check("a search that never lands reports failure, not a guess", !noneRes.Ok, "");

        // ---- END TO END: THE SEARCH PLACES A REAL DRAG DESCENT ON A REACHABLE TARGET ----
        // A 90 km circular orbit, no body spin, so the impact "longitude" is just atan2(Iy, Ix). This
        // exercises DvForPeriapsis + Trajectory.Solve + Search TOGETHER - everything the flight code
        // composes except KSP's own frame swizzle and lat/lon (guarded in flight by ValidateIgnition-
        // Frame). It is the test that proves the search actually improves the landing, not just that
        // its parts each work.
        double Ro = Rk + 90000.0;
        double wOrb = Math.Sqrt(Mu / (Ro * Ro * Ro));
        double periodO = 2.0 * Math.PI / wOrb;
        double targetRp2 = Rk + DeorbitBurn.PeriapsisTargetM;
        const double bcCap = 440.0;                              // the measured capsule BC

        // Impact longitude (radians) for an ignition at ut, modelling the burn to the -31.8 km floor.
        Func<double, double> lonAt = delegate(double ut)
        {
            double th = wOrb * ut;
            double px = Ro * Math.Cos(th), py = Ro * Math.Sin(th);
            double sp = Ro * wOrb;
            double vx = -sp * Math.Sin(th), vy = sp * Math.Cos(th);
            double dvb = DeorbitPoint.DvForPeriapsis(px, py, 0, vx, vy, 0, Mu, targetRp2);
            double sc = 1.0 - dvb / sp;
            TrajectoryInputs si = new TrajectoryInputs();
            si.Px = px; si.Py = py; si.Pz = 0;
            si.Vx = vx * sc; si.Vy = vy * sc; si.Vz = 0;
            si.Mu = Mu; si.BodyRadiusM = Rk; si.AtmosphereDepthM = AtmH;
            si.BallisticCoefficient = bcCap; si.ImpactAltitudeM = 0.0; si.BodyOmega = 0.0;
            TrajectoryResult tr = Trajectory.Solve(si, Density);
            return tr.Ok ? Math.Atan2(tr.Iy, tr.Ix) : double.NaN;
        };

        // Target a landing we KNOW is reachable: wherever a mid-orbit ignition comes down. Then make
        // the search rediscover an ignition that hits it, from a full-orbit window.
        double targetLon = lonAt(periodO * 0.5);
        Check("the reference ignition actually lands", !double.IsNaN(targetLon), targetLon.ToString("F3"));

        IgnitionMissAtUt land = delegate(double ut)
        {
            IgnitionMiss m = new IgnitionMiss();
            double lon = lonAt(ut);
            if (double.IsNaN(lon)) { m.Ok = false; return m; }
            double d = lon - targetLon;
            while (d > Math.PI) d -= 2 * Math.PI;
            while (d < -Math.PI) d += 2 * Math.PI;
            m.Ok = true; m.MissM = Math.Abs(d) * Rk;
            m.DvMps = 0.0; m.PeriapsisM = DeorbitBurn.PeriapsisTargetM;
            return m;
        };
        double landUt;
        IgnitionMiss landed = DeorbitPoint.Search(0.0, periodO, land, out landUt);
        Check("the search lands the modelled burn on the reachable target",
              landed.Ok && landed.MissM < 3000.0, (landed.MissM / 1000.0).ToString("F2") + " km");
        // A tenth-of-an-orbit-wrong ignition (the class of error the fixed lead made) misses by a lot -
        // proof the search is doing real work, not landing near target by luck.
        IgnitionMiss wrong = land(landUt + periodO * 0.1);
        Check("and a tenth-orbit-wrong ignition misses by tens of km", wrong.Ok && wrong.MissM > 20000.0,
              (wrong.MissM / 1000.0).ToString("F1") + " km");

        // ---- ⛔ ROTATING BODY: THE IMPACT LON MUST BE WOUND BACK OVER THE IGNITION LEAD ----
        // The bug on flight_0818_154218: the miss was scored in the body frame AS OF NOW, ignoring the
        // ~104 deg Kerbin turns across the 6262 s lead - the search reported 2.0 km for an ignition that
        // flew 1103 km off. This models it: a fixed representative fall, a real body spin, and a lead of
        // thousands of seconds. The correct miss winds the impact lon back by omega*(ut - now); the
        // stale one (the bug) does not.
        double omega = 2.0 * Math.PI / 21600.0;                 // Kerbin day, rad/s
        double now0 = 5000.0;
        // Body-fixed impact longitude for an ignition at ut. Reuse the same circular-orbit burn model;
        // Trajectory.Solve carries the FALL rotation, and the lead rotation is omega*(ut-now0).
        Func<double, double, double> lonBody = delegate(double ut, double leadSign)
        {
            double th = wOrb * ut;
            double px = Ro * Math.Cos(th), py = Ro * Math.Sin(th);
            double sp = Ro * wOrb;
            double vx = -sp * Math.Sin(th), vy = sp * Math.Cos(th);
            double dvb = DeorbitPoint.DvForPeriapsis(px, py, 0, vx, vy, 0, Mu, targetRp2);
            double sc = 1.0 - dvb / sp;
            TrajectoryInputs si = new TrajectoryInputs();
            si.Px = px; si.Py = py; si.Pz = 0;
            si.Vx = vx * sc; si.Vy = vy * sc; si.Vz = 0;
            si.Mu = Mu; si.BodyRadiusM = Rk; si.AtmosphereDepthM = AtmH;
            si.BallisticCoefficient = bcCap; si.ImpactAltitudeM = 0.0; si.BodyOmega = omega;
            TrajectoryResult tr = Trajectory.Solve(si, Density);
            if (!tr.Ok) return double.NaN;
            // inertial impact lon, minus the FALL rotation (as PredictFromState does), minus the LEAD
            // rotation over (ut-now0) scaled by leadSign (1 = correct fix, 0 = the stale bug).
            return Math.Atan2(tr.Iy, tr.Ix) - tr.BodyRotationRad - leadSign * omega * (ut - now0);
        };
        double rotTarget = lonBody(now0 + 4000.0, 1.0);          // a reachable, wound-back target
        Check("rotating reference ignition lands", !double.IsNaN(rotTarget), "");
        IgnitionMissAtUt landRot = delegate(double ut)
        {
            IgnitionMiss mm = new IgnitionMiss();
            double lon = lonBody(ut, 1.0);
            if (double.IsNaN(lon)) { mm.Ok = false; return mm; }
            double dd = lon - rotTarget;
            while (dd > Math.PI) dd -= 2 * Math.PI;
            while (dd < -Math.PI) dd += 2 * Math.PI;
            mm.Ok = true; mm.MissM = Math.Abs(dd) * Rk;
            return mm;
        };
        double rotUt;
        IgnitionMiss rotBest = DeorbitPoint.Search(now0 + 200.0, now0 + periodO, landRot, out rotUt);
        Check("with the lead wound back, the search lands on target through body spin",
              rotBest.Ok && rotBest.MissM < 4000.0, (rotBest.MissM / 1000.0).ToString("F2") + " km");
        // The stale frame (the bug) at the SAME ignition is off by hundreds of km - the ~104 deg lead.
        double staleLon = lonBody(rotUt, 0.0);
        double staleD = staleLon - rotTarget;
        while (staleD > Math.PI) staleD -= 2 * Math.PI;
        while (staleD < -Math.PI) staleD += 2 * Math.PI;
        Check("and omitting the wind-back would miss by hundreds of km (the bug)",
              Math.Abs(staleD) * Rk > 200000.0, (Math.Abs(staleD) * Rk / 1000.0).ToString("F0") + " km");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
