/*
 * DragonScreen headless tests - the phasing leg.
 *
 * The two that matter are both regressions against real flights:
 *   * flight 014: a correct period and a semi-major axis that back-solved to 373,106 m instead of
 *     691,957 m, from a `^ (1/3)`. It became a 1353 m/s RETROGRADE burn with periapsis at -539.9 km.
 *   * flight 015: vis-viva called with a radius of ~1,168,479 m instead of 686,270 m, giving
 *     -1300.69 m/s where +11.5 m/s was correct.
 *
 * So: assert the semi-major axis against Kepler independently, and assert the DIRECTION, because
 * both failures produced a burn of roughly the right size pointing the wrong way.
 */
using System;
using DragonScreen;

public static class PhasingTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    const double Mu = 3.5316e12;             // Kerbin
    const double Rk = 600000.0;

    // The station's measured orbit, and us alongside it.
    static PhasingInputs Gap(double gapM, int laps)
    {
        PhasingInputs p = new PhasingInputs();
        p.StationSmaM = Rk + 86300.0;
        p.StationPeriodS = 2.0 * Math.PI * Math.Sqrt(Math.Pow(p.StationSmaM, 3) / Mu);
        p.RadiusM = p.StationSmaM;
        p.SpeedMps = Math.Sqrt(Mu / p.RadiusM);
        p.Mu = Mu;
        p.GapM = gapM;
        p.Orbits = laps;
        return p;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen phasing tests");

        // ---- FLIGHT 014's CASE: 52.9 km AHEAD ----
        PhasingInputs ahead = Gap(52900.0, 1);
        PhasingSolution a = Phasing.Solve(ahead);
        Check("a 52.9 km lead has a solution", a.Ok, a.Note);

        // ⛔ THE DIRECTION. Ahead means RAISE - a longer period lets the station catch up. This is
        // what makes a phasing orbit unable to drop periapsis, and 014's bad axis inverted it.
        Check("ahead of the station, the phasing orbit is HIGHER",
              a.PhaseSmaM > ahead.StationSmaM,
              a.PhaseSmaM.ToString("F0") + " vs " + ahead.StationSmaM.ToString("F0"));
        Check("so the entry burn is PROGRADE", a.EntryDvMps > 0.0, a.EntryDvMps.ToString("F2"));
        Check("and the sanity check agrees", Phasing.DirectionSane(ahead, a), "");

        // The linearised axis must match the cube-root answer it replaced, to within metres.
        double exact = Math.Pow(Mu * Math.Pow(a.PhasePeriodS / (2.0 * Math.PI), 2.0), 1.0 / 3.0);
        Check("the linearised semi-major axis matches Kepler to within 100 m",
              Math.Abs(a.PhaseSmaM - exact) < 100.0,
              "linear " + a.PhaseSmaM.ToString("F0") + " vs exact " + exact.ToString("F0"));
        // ...and is nowhere near flight 014's garbage.
        Check("and is nothing like the 373 km that flew on 014",
              a.PhaseSmaM > 600000.0, a.PhaseSmaM.ToString("F0"));

        // ---- COST. F9I's simulated table: 51 km costs 17.7 m/s over one lap. ----
        Check("the cost is in the range F9I simulated for this gap",
              Math.Abs(a.EntryDvMps) > 1.0 && Math.Abs(a.EntryDvMps) < 40.0,
              a.EntryDvMps.ToString("F1"));

        // ---- BEHIND IS THE MIRROR ----
        PhasingInputs behind = Gap(-52900.0, 1);
        PhasingSolution b = Phasing.Solve(behind);
        Check("behind the station, the phasing orbit is LOWER",
              b.PhaseSmaM < behind.StationSmaM, b.PhaseSmaM.ToString("F0"));
        Check("so the entry burn is RETROGRADE", b.EntryDvMps < 0.0, b.EntryDvMps.ToString("F2"));
        Check("and it costs about the same either way",
              Math.Abs(Math.Abs(a.EntryDvMps) - Math.Abs(b.EntryDvMps)) < 1.0, "");
        Check("the sanity check agrees for behind too", Phasing.DirectionSane(behind, b), "");

        // ---- MORE LAPS IS CHEAPER AND SLOWER. F9I: "2 halves the dv and doubles the wait." ----
        PhasingSolution two = Phasing.Solve(Gap(52900.0, 2));
        Check("two laps costs about half as much",
              Math.Abs(two.EntryDvMps) < Math.Abs(a.EntryDvMps) * 0.6,
              two.EntryDvMps.ToString("F2") + " vs " + a.EntryDvMps.ToString("F2"));
        Check("and takes about twice as long", two.CoastS > a.CoastS * 1.8,
              two.CoastS.ToString("F0") + " vs " + a.CoastS.ToString("F0"));

        // ---- THE dv CAP IS A WRONG-ANSWER DETECTOR, NOT A BUDGET ----
        // A gap so large the solve degenerates must be REFUSED, not flown. 014's 1353 m/s is the case.
        PhasingInputs absurd = Gap(50000000.0, 1);
        PhasingSolution bad = Phasing.Solve(absurd);
        Check("an absurd gap is refused rather than flown", !bad.Ok, bad.Note);
        Check("and it says why", bad.Note.Length > 0, bad.Note);
        Check("the cap is F9I's 250 m/s", Math.Abs(Phasing.MaxDvMps - 250.0) < 1e-9, "");

        // ---- CIRCULARISING BACK ----
        double exitDv = Phasing.ExitDvMps(ahead.RadiusM, ahead.SpeedMps, Mu);
        Check("already circular needs no exit burn", Math.Abs(exitDv) < 1e-6,
              exitDv.ToString("F4"));
        // Coming back from a higher phasing orbit we are slower at this radius, so the exit burn is
        // prograde - the mirror of the entry.
        double slow = ahead.SpeedMps - 10.0;
        Check("arriving slow, the exit burn is prograde",
              Phasing.ExitDvMps(ahead.RadiusM, slow, Mu) > 0.0, "");

        // ---- A ZERO GAP IS NOT A MANOEUVRE ----
        PhasingSolution none = Phasing.Solve(Gap(0.0, 1));
        Check("no gap, no burn", Math.Abs(none.EntryDvMps) < 1e-6, none.EntryDvMps.ToString("F6"));


        // ---- ORBIT MATCH COMES FIRST, AND THE OTHER IMPLEMENTATION IS DEAD CODE ----
        // F9_payload.ks's MatchPlanes/MatchSMA are marked "DEAD SINCE 2026-08-04... do not wire this
        // one back in by mistake because the name reads right". StMatchStationOrbit is the live one.
        double stnSma = Rk + 86300.0;
        Check("500 m of semi-major axis is already co-orbital",
              !OrbitMatch.Needed(stnSma + 400.0, stnSma), "");
        Check("a kilometre is not",
              OrbitMatch.Needed(stnSma + 1000.0, stnSma), "");
        Check("and it does not care which side we are on",
              OrbitMatch.Needed(stnSma - 1000.0, stnSma), "");

        // An elliptical orbit whose apoapsis is at the station's radius: circularising there is
        // prograde, and it both rounds the orbit off and matches the altitude in one burn.
        double ra = stnSma;
        double ourSma = stnSma - 5000.0;               // apoapsis here, periapsis lower
        double matchDv = OrbitMatch.CirculariseAtApoapsisDv(ra, ourSma, Mu);
        Check("circularising at apoapsis is a PROGRADE burn", matchDv > 0.0,
              matchDv.ToString("F2"));
        Check("and it is a small one from a nearly-matched orbit",
              matchDv < 20.0, matchDv.ToString("F2"));
        Check("an already circular orbit needs no match burn",
              Math.Abs(OrbitMatch.CirculariseAtApoapsisDv(stnSma, stnSma, Mu)) < 1e-6, "");

        // ---- AND THE LADDER PUTS IT BEFORE EVERYTHING ELSE ----
        // A CW solve against a different semi-major axis is nonsense: its frame IS the station's.
        Check("not co-orbital: match the orbit before phasing or CW",
              Approach.LegFor(50000.0, 50000.0, 60.0, stnSma + 20000.0, stnSma)
              == ApproachLeg.MatchOrbit, "");
        Check("co-orbital with a big gap: phase",
              Approach.LegFor(50000.0, 50000.0, 60.0, stnSma, stnSma) == ApproachLeg.Phasing, "");
        Check("co-orbital with a small gap: CW",
              Approach.LegFor(2000.0, 2000.0, 60.0, stnSma, stnSma) == ApproachLeg.Clohessy, "");
        Check("inside the terminal range, RCS - whatever the orbits say",
              Approach.LegFor(300.0, 300.0, 60.0, stnSma + 20000.0, stnSma)
              == ApproachLeg.Terminal, "");
        Check("at the aim point, arrived",
              Approach.LegFor(50.0, 50.0, 60.0, stnSma, stnSma) == ApproachLeg.Arrived, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
