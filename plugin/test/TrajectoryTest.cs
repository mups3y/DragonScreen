/*
 * DragonScreen headless tests - the atmospheric trajectory integrator.
 *
 * The point of these is that the integrator can be CHECKED WITHOUT THE GAME. A vacuum case has a
 * closed-form answer, so the integrator is asserted against arithmetic rather than against itself;
 * a drag case has no closed form, so it is asserted against the properties drag must have.
 *
 * This is the file that decides whether a de-orbit burn with a 50 m stop tolerance is flying against
 * a real prediction or a hopeful one.
 */
using System;
using DragonScreen;

public static class TrajectoryTest
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

    // Kerbin's atmosphere, close enough for a test: sea level 1.225 kg/m3, scale height ~5600 m.
    static double Density(double alt)
    {
        if (alt < 0.0 || alt >= AtmH) return 0.0;
        return 1.225 * Math.Exp(-alt / 5600.0);
    }

    static double Vacuum(double alt) { return 0.0; }

    static TrajectoryInputs Dropped(double altM, double bc)
    {
        TrajectoryInputs s = new TrajectoryInputs();
        s.Px = Rk + altM; s.Py = 0.0; s.Pz = 0.0;
        s.Vx = 0.0; s.Vy = 0.0; s.Vz = 0.0;
        s.Mu = Mu; s.BodyRadiusM = Rk; s.AtmosphereDepthM = AtmH;
        s.BallisticCoefficient = bc;
        s.BodyOmega = 0.0;
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen trajectory tests");

        // ---- VACUUM HAS A CLOSED FORM, SO CHECK AGAINST ARITHMETIC ----
        // Dropped from 10 km with no air: near-uniform g = mu/r^2 gives t = sqrt(2h/g).
        double alt = 10000.0;
        double g = Mu / ((Rk + alt / 2) * (Rk + alt / 2));
        double expect = Math.Sqrt(2.0 * alt / g);
        TrajectoryResult v = Trajectory.Solve(Dropped(alt, 0.0), Vacuum);
        Check("a vacuum drop reaches the ground", v.Ok, v.Note);
        Check("and it takes the time free-fall says it should",
              Math.Abs(v.TimeToImpactS - expect) / expect < 0.02,
              v.TimeToImpactS.ToString("F1") + " vs " + expect.ToString("F1"));
        Check("it lands directly below where it was dropped",
              Math.Abs(v.Iy) < 1.0 && Math.Abs(v.Iz) < 1.0, "");
        Check("and it says it was a vacuum solve", !v.DragModelled, v.Note);
        // Impact speed from energy: v = sqrt(2gh).
        Check("impact speed matches sqrt(2gh)",
              Math.Abs(v.ImpactSpeedMps - Math.Sqrt(2.0 * g * alt)) / Math.Sqrt(2.0 * g * alt) < 0.02,
              v.ImpactSpeedMps.ToString("F1"));

        // ---- DRAG HAS NO CLOSED FORM, SO CHECK THE PROPERTIES IT MUST HAVE ----
        TrajectoryResult d = Trajectory.Solve(Dropped(alt, 500.0), Density);
        Check("a dropped capsule with drag still reaches the ground", d.Ok, d.Note);
        Check("and it says drag was modelled", d.DragModelled, d.Note);
        Check("drag makes the fall take LONGER", d.TimeToImpactS > v.TimeToImpactS,
              d.TimeToImpactS.ToString("F1") + " vs " + v.TimeToImpactS.ToString("F1"));
        Check("and the arrival slower", d.ImpactSpeedMps < v.ImpactSpeedMps,
              d.ImpactSpeedMps.ToString("F1") + " vs " + v.ImpactSpeedMps.ToString("F1"));

        // A LOWER ballistic coefficient is a draggier vehicle: slower still.
        TrajectoryResult draggier = Trajectory.Solve(Dropped(alt, 100.0), Density);
        Check("a draggier vehicle arrives slower than a sleeker one",
              draggier.ImpactSpeedMps < d.ImpactSpeedMps,
              draggier.ImpactSpeedMps.ToString("F1") + " vs " + d.ImpactSpeedMps.ToString("F1"));
        // ...and a very high BC is nearly a vacuum solve.
        TrajectoryResult sleek = Trajectory.Solve(Dropped(alt, 1e7), Density);
        Check("a very high BC converges on the vacuum answer",
              Math.Abs(sleek.TimeToImpactS - v.TimeToImpactS) / v.TimeToImpactS < 0.02,
              sleek.TimeToImpactS.ToString("F2") + " vs " + v.TimeToImpactS.ToString("F2"));

        // ---- ⛔ DRAG ONLY EVER SHORTENS A DOWNRANGE TRAJECTORY ----
        // This is the property the whole de-orbit depends on, and the reason a vacuum solve is
        // always LONG. Fire one horizontally from 40 km and compare the ground track.
        TrajectoryInputs lofted = Dropped(40000.0, 0.0);
        lofted.Vy = 2000.0;
        TrajectoryResult noAir = Trajectory.Solve(lofted, Vacuum);
        lofted.BallisticCoefficient = 500.0;
        TrajectoryResult withAir = Trajectory.Solve(lofted, Density);
        Check("both come down", noAir.Ok && withAir.Ok, "");
        double downNo = Math.Atan2(noAir.Iy, noAir.Ix);
        double downWith = Math.Atan2(withAir.Iy, withAir.Ix);
        Check("drag lands the vehicle SHORTER than vacuum does", downWith < downNo,
              (downWith * Rk / 1000.0).ToString("F1") + " km vs "
              + (downNo * Rk / 1000.0).ToString("F1") + " km");
        Check("and by a distance worth predicting, not a rounding error",
              (downNo - downWith) * Rk > 1000.0,
              ((downNo - downWith) * Rk / 1000.0).ToString("F1") + " km");

        // ---- BODY ROTATION IS CARRIED ----
        TrajectoryInputs spinning = Dropped(alt, 0.0);
        spinning.BodyOmega = 2.0 * Math.PI / 21600.0;         // Kerbin's day
        TrajectoryResult sp = Trajectory.Solve(spinning, Vacuum);
        Check("the body's rotation during the flight is reported",
              sp.BodyRotationRad > 0.0, sp.BodyRotationRad.ToString("F6"));
        Check("and it matches omega times the flight time",
              Math.Abs(sp.BodyRotationRad - spinning.BodyOmega * sp.TimeToImpactS) < 1e-6, "");

        // ---- AN ORBIT THAT DOES NOT COME DOWN HAS NO IMPACT POINT ----
        // Reporting one would be worse than reporting none.
        TrajectoryInputs orbit = Dropped(100000.0, 0.0);
        orbit.Vy = Math.Sqrt(Mu / (Rk + 100000.0));           // circular
        TrajectoryResult none = Trajectory.Solve(orbit, Vacuum);
        Check("a circular orbit yields no impact", !none.Ok, none.Note);
        Check("and says so rather than returning a point", none.Note.Length > 0, none.Note);

        // ---- THE MEASUREMENT ----
        // BC = 0.5 * rho * v^2 / a. At 1.0 kg/m3, 100 m/s, 5 m/s^2: 0.5*1*10000/5 = 1000.
        Check("the ballistic coefficient back-solves correctly",
              Math.Abs(Trajectory.BallisticCoefficientFrom(1.0, 100.0, 5.0) - 1000.0) < 1e-6,
              Trajectory.BallisticCoefficientFrom(1.0, 100.0, 5.0).ToString("F1"));
        Check("no air means no measurement, not a wrong one",
              Trajectory.BallisticCoefficientFrom(0.0, 100.0, 5.0) == 0.0, "");
        Check("barely moving means no measurement",
              Trajectory.BallisticCoefficientFrom(1.0, 1.0, 5.0) == 0.0, "");
        Check("and no measurable deceleration means no measurement",
              Trajectory.BallisticCoefficientFrom(1.0, 100.0, 0.0) == 0.0, "");

        // ---- THE FILTER ----
        Check("the first good sample seeds the estimate",
              Math.Abs(Trajectory.SmoothBc(0.0, 800.0, 0.02, 3.0) - 800.0) < 1e-9, "");
        Check("a bad sample never poisons a good estimate",
              Math.Abs(Trajectory.SmoothBc(800.0, 0.0, 0.02, 3.0) - 800.0) < 1e-9, "");
        double smoothed = Trajectory.SmoothBc(800.0, 1000.0, 0.02, 3.0);
        Check("a new sample moves the estimate toward it",
              smoothed > 800.0 && smoothed < 1000.0, smoothed.ToString("F2"));
        Check("but only a little in one tick - drag is noisy",
              smoothed < 810.0, smoothed.ToString("F2"));

        // ---- ⛔ LIFT: the predictor flies the VESSEL'S REAL LIFTING PROFILE, not a ballistic fall ----
        // A grid-fin booster at AoA and a bank-modulated capsule both generate lift; a drag-only default
        // would mispredict both. Bank 0 = lift up (flies farther), bank 180 = lift down (shorter), bank 90
        // = lift to a side (crossrange). Compare against the same drag-only fall (downWith, BC 500).
        TrajectoryInputs liftUp = Dropped(40000.0, 500.0); liftUp.Vy = 2000.0;
        liftUp.LiftToDrag = 0.3; liftUp.BankRad = 0.0;
        double downUp = Math.Atan2(Trajectory.Solve(liftUp, Density).Iy, Trajectory.Solve(liftUp, Density).Ix);
        Check("lift UP (bank 0) flies FARTHER than the ballistic fall",
              downUp > downWith, (downUp * Rk / 1000).ToString("F1") + " vs " + (downWith * Rk / 1000).ToString("F1") + " km");

        TrajectoryInputs liftDn = Dropped(40000.0, 500.0); liftDn.Vy = 2000.0;
        liftDn.LiftToDrag = 0.3; liftDn.BankRad = Math.PI;
        double downDn = Math.Atan2(Trajectory.Solve(liftDn, Density).Iy, Trajectory.Solve(liftDn, Density).Ix);
        Check("lift DOWN (bank 180) flies SHORTER than the ballistic fall", downDn < downWith,
              (downDn * Rk / 1000).ToString("F1") + " km");

        TrajectoryInputs liftX = Dropped(40000.0, 500.0); liftX.Vy = 2000.0;
        liftX.LiftToDrag = 0.3; liftX.BankRad = Math.PI / 2.0;
        TrajectoryResult rx = Trajectory.Solve(liftX, Density);
        Check("a banked lift develops CROSSRANGE (out-of-plane)", Math.Abs(rx.Iz) > 1000.0,
              (rx.Iz / 1000).ToString("F1") + " km");
        Check("...while the ballistic fall stayed in-plane", Math.Abs(withAir.Iz) < 100.0,
              withAir.Iz.ToString("F1"));

        // L/D = 0 is only the degenerate case (NOT the operating default): it reproduces the drag-only fall.
        TrajectoryInputs liftZero = Dropped(40000.0, 500.0); liftZero.Vy = 2000.0; liftZero.LiftToDrag = 0.0;
        double downZero = Math.Atan2(Trajectory.Solve(liftZero, Density).Iy, Trajectory.Solve(liftZero, Density).Ix);
        Check("L/D=0 reproduces the drag-only solve exactly", Math.Abs(downZero - downWith) < 1e-12, "");

        // ---- MEASURE the live L/D + bank from the vessel's aero acceleration (self-calibrated profile) ----
        // velocity +Y, radial-up +X → lift-up dir = +X, lift-right dir = v x up = -Z.
        Trajectory.AeroProfile mp = Trajectory.MeasureAero(0, -5, 0, 0, 100, 0, 1, 0, 0);
        Check("pure drag reads L/D = 0", mp.Valid && Math.Abs(mp.LiftToDrag) < 1e-9, mp.LiftToDrag.ToString());
        Check("...and the drag magnitude back-solves", Math.Abs(mp.DragAccel - 5.0) < 1e-9, mp.DragAccel.ToString());
        Trajectory.AeroProfile mu = Trajectory.MeasureAero(1.5, -5, 0, 0, 100, 0, 1, 0, 0);
        Check("lift-up: L/D measured 0.3", Math.Abs(mu.LiftToDrag - 0.3) < 1e-6, mu.LiftToDrag.ToString("F3"));
        Check("lift-up: bank ~ 0", Math.Abs(mu.BankRad) < 1e-6, mu.BankRad.ToString("F4"));
        Trajectory.AeroProfile mr = Trajectory.MeasureAero(0, -5, -1.5, 0, 100, 0, 1, 0, 0);
        Check("lift-right: L/D measured 0.3", Math.Abs(mr.LiftToDrag - 0.3) < 1e-6, mr.LiftToDrag.ToString("F3"));
        Check("lift-right: bank ~ +90 deg", Math.Abs(mr.BankRad - Math.PI / 2.0) < 1e-6, (mr.BankRad * 180 / Math.PI).ToString("F1"));
        Check("too slow to measure a profile", !Trajectory.MeasureAero(1, -5, 0, 0, 1, 0, 1, 0, 0).Valid, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
