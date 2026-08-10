/*
 * DragonScreen headless tests - the return budget.
 *
 * Two of these have a flight behind them and neither is a rounding question:
 *   * flight 026 undocked 53 units short and nothing noticed until the de-orbit burn died on the
 *     reserve floor - so the budget must be answerable BEFORE undocking
 *   * with the S2 attached F9I once announced "MONOPROP SHORT by 125 units" on a vehicle that had
 *     175 units and a full second stage to de-orbit with. The de-orbit costs ZERO mono then, and a
 *     zero line is a correct answer rather than a failed calculation.
 */
using System;
using DragonScreen;

public static class ReturnBudgetTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // Kerbin, and the station's measured orbit: 86.8 x 85.8 km.
    static BudgetInputs AtStation(double mono, bool s2, LandingMode mode)
    {
        BudgetInputs b = new BudgetInputs();
        b.MonoUnits = mono;
        b.MassT = 6.1;                       // capsule + trunk, per falcon-dragon-two-decouplers
        b.ApoapsisM = 86800.0;
        b.SmaM = 600000.0 + 86300.0;
        b.BodyRadiusM = 600000.0;
        b.Mu = 3.5316e12;
        b.S2Attached = s2;
        b.Mode = mode;
        return b;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen return budget tests");

        // ---- THE S2 CASE. A ZERO DE-ORBIT LINE IS CORRECT. ----
        BudgetReport withS2 = ReturnBudget.Report(AtStation(175.0, true, LandingMode.Parachute));
        Check("with the S2 attached the de-orbit costs no monopropellant",
              Math.Abs(withS2.DeorbitUnits) < 1e-9, withS2.DeorbitUnits.ToString("F2"));
        Check("so 175 units is comfortably sufficient", withS2.Sufficient,
              withS2.MarginUnits.ToString("F1"));
        Check("and the line SAYS it is the S2 being budgeted for",
              withS2.Line.Contains("S2"), withS2.Line);

        // ---- THE DRACO CASE ----
        BudgetReport draco = ReturnBudget.Report(AtStation(175.0, false, LandingMode.Parachute));
        Check("without the S2 the de-orbit costs real monopropellant",
              draco.DeorbitUnits > 1.0, draco.DeorbitUnits.ToString("F2"));
        // F9I quotes 39-85 units for a Draco return from this orbit.
        Check("and it is in the range F9I measured, 39-85 units",
              draco.DeorbitUnits > 20.0 && draco.DeorbitUnits < 120.0,
              draco.DeorbitUnits.ToString("F1"));
        Check("the line says Draco", draco.Line.Contains("Draco"), draco.Line);
        Check("the S2 case is strictly cheaper than the Draco case",
              withS2.NeedUnits < draco.NeedUnits, "");

        // ---- THE RESERVE DEPENDS ON HOW IT LANDS, NOT ON HOW IT DE-ORBITS ----
        BudgetReport chute = ReturnBudget.Report(AtStation(175.0, false, LandingMode.Parachute));
        BudgetReport prop = ReturnBudget.Report(AtStation(175.0, false, LandingMode.Propulsive));
        Check("a propulsive landing reserves more than a parachute one",
              prop.LandingUnits > chute.LandingUnits,
              prop.LandingUnits + " vs " + chute.LandingUnits);
        Check("and it is the same de-orbit either way",
              Math.Abs(prop.DeorbitUnits - chute.DeorbitUnits) < 1e-9, "");

        // ---- SHORT IS REPORTED, NOT ROUNDED AWAY. Flight 026 undocked 53 units short. ----
        BudgetReport thin = ReturnBudget.Report(AtStation(40.0, false, LandingMode.Parachute));
        Check("40 units is NOT enough for a Draco return", !thin.Sufficient,
              thin.MarginUnits.ToString("F1"));
        Check("and the margin says by how much", thin.MarginUnits < 0.0,
              thin.Line);

        // ---- ENTRY AND LANDING ARE ALWAYS IN THE BILL ----
        // A budget that only counted the burn is how a capsule arrives with nothing to steer with.
        Check("entry steering is always budgeted",
              Math.Abs(draco.EntryUnits - ReturnBudget.EntryUnits) < 1e-9, "");
        Check("so is the landing reserve", draco.LandingUnits > 0.0, "");
        Check("need is the sum of all three",
              Math.Abs(draco.NeedUnits
                       - (draco.DeorbitUnits + draco.EntryUnits + draco.LandingUnits)) < 1e-9, "");

        // ---- THE SERIES EXPANSION IS ACCURATE WHERE IT IS USED ----
        // F9I: accurate to 0.07% at dv~113 m/s, under 1% out to 450 m/s. Check it against exp().
        double dv = 113.0;
        double x = dv / (ReturnBudget.MonoIsp * ReturnBudget.G0);
        double series = x - x * x / 2.0;
        double exact = 1.0 - Math.Exp(-x);
        Check("the second-order expansion matches exp() where it is used",
              Math.Abs(series - exact) / exact < 0.001,
              (Math.Abs(series - exact) / exact * 100.0).ToString("F3") + "%");

        // ---- A RETURN IS ONLY MEANINGFUL FROM AN ORBIT ----
        string why;
        Check("on the ground, refused",
              !ReturnBudget.ReturnAllowed(true, 0.0, 0.0, 70000.0, out why), "");
        Check("inside the atmosphere, refused - that is a vessel already coming down",
              !ReturnBudget.ReturnAllowed(false, 40000.0, 30000.0, 70000.0, out why), "");
        Check("negative periapsis, refused even from above the air",
              !ReturnBudget.ReturnAllowed(false, 200000.0, -1000.0, 70000.0, out why), "");
        Check("and a real orbit is allowed",
              ReturnBudget.ReturnAllowed(false, 86800.0, 85800.0, 70000.0, out why), why);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
