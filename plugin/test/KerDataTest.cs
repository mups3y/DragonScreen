// Tests for pure/KerData.cs — the stage-selection logic over the mirrored Kerbal Engineer data, and (S46) the
// readout GROUP built on it. The reflection reader (src/KerBridge.cs) needs the game; THIS logic (which stage
// is current/final, remaining Δv, reserve, the docked + finite guards and the formatting) is pure and
// headless-tested with synthetic stages, and every "no KER" path must degrade to a dash rather than a zero.
//
// The S46 half ends at the GLASS on purpose: the last two checks build the real PROPULSION tab and assert that
// "Thrust Avail" prints the group's number when there is one and the page's own dash when there is not. A hop
// that is only tested at its own end proves the value exists, not that anyone can see it.
using System;
using DragonScreen;

public static class KerDataTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }

    static KerStage S(int number, double dv, double totalDv)
    {
        return new KerStage { Number = number, DeltaVMps = dv, TotalDeltaVMps = totalDv, Valid = true };
    }

    // A FULLY populated stage, the shape KerBridge mirrors out of KER: SI throughout (newtons, kilograms), so
    // the kN and kg the readout prints are this file's own conversions and a 1000× slip would show up here.
    // Numbers are Dragon-ish: eight SuperDracos at ~71 kN, a ~9.5 t capsule.
    static KerStage Full(int number)
    {
        KerStage k = new KerStage();
        k.Number = number;
        k.DeltaVMps = 390.0; k.TotalDeltaVMps = 415.0;
        k.ThrustN = 568000.0;          // 568.0 kN available
        k.ActualThrustN = 142000.0;    // 142.0 kN at the current throttle — deliberately DIFFERENT
        k.Twr = 6.09; k.ActualTwr = 1.52; k.MaxTwr = 7.40;
        k.IspS = 235.0;
        k.MassKg = 9525.0; k.TotalMassKg = 12055.0; k.ResourceMassKg = 1388.0;
        k.BurnTimeS = 31.0;
        k.Valid = true;
        return k;
    }

    // Did the built page draw this exact string? Same shape as FigmaUINavTest's own helper.
    static bool Drew(DisplayList dl, string text)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == text) return true;
        }
        return false;
    }

    // The PROPULSION tab, built for real, with this group in PageState.
    static DisplayList Prop(KerPerformance ker)
    {
        PageState st = new PageState();
        st.Valid = true;
        st.Systems = SystemsState.Fresh();
        st.Ker = ker;
        DisplayList dl = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(dl, 2560, 1406, VehicleSubsystemPage.Sub.Propulsion, st);
        return dl;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen KerData tests");

        // ---- empty (KER absent) → clean fallback ----
        Check("empty: Current is invalid", !KerData.Current(null).Valid && !KerData.Current(new KerStage[0]).Valid, "");
        Check("empty: remaining Δv is 0", KerData.RemainingDeltaV(null) == 0.0, "");
        Check("empty: reserve fails (no data)", !KerData.HasRecoveryReserve(null, 100.0), "");

        // ---- a 3-stage vehicle: stage 2 burning now (highest number), stage 0 the final ----
        // number: 0 = final (e.g. the S2/deorbit), 2 = current (booster). Total Δv accumulates from this stage down.
        KerStage[] v = {
            S(0, 4200, 4200),   // final stage alone: 4200
            S(1, 1500, 5700),   // stage 1 + below
            S(2, 1300, 7000),   // CURRENT: 1300 this stage, 7000 remaining from here down
        };
        Check("Current = the highest-numbered (currently burning) stage", KerData.Current(v).Number == 2, KerData.Current(v).Number.ToString());
        Check("Final = the lowest-numbered (last-to-burn) stage", KerData.Final(v).Number == 0, KerData.Final(v).Number.ToString());
        Check("remaining Δv = current stage's cumulative total", Math.Abs(KerData.RemainingDeltaV(v) - 7000.0) < 1e-9, KerData.RemainingDeltaV(v).ToString("F0"));
        Check("this-stage Δv is distinct from remaining", Math.Abs(KerData.Current(v).DeltaVMps - 1300.0) < 1e-9, "");

        // ---- reserve check ----
        Check("reserve holds when remaining exceeds it", KerData.HasRecoveryReserve(v, 5000.0), "");
        Check("reserve fails when remaining is below it", !KerData.HasRecoveryReserve(v, 8000.0), "");

        // ---- single stage ----
        KerStage[] one = { S(0, 3000, 3000) };
        Check("single stage: current == final", KerData.Current(one).Number == 0 && KerData.Final(one).Number == 0, "");
        Check("single stage: remaining = its Δv", Math.Abs(KerData.RemainingDeltaV(one) - 3000.0) < 1e-9, "");

        // ================= S46: the readout GROUP (KerData.Performance) =================
        // Hops 2-4 carry the WHOLE propulsion-performance group — Δv, TWR, thrust, Isp, burn time, stage mass —
        // so all of it is asserted here even though only Thrust Avail is on the glass today. A field that is
        // carried but never checked is a field that will be wrong on the day someone gives it a home.

        KerStage[] flying = { Full(0), Full(1) };
        flying[1].ThrustN = 568000.0;                 // stage 1 is the current one (highest number)
        flying[0].ThrustN = 1000.0;                   // the final stage makes far less — proves WHICH was read
        KerPerformance p = KerData.Performance(flying, false);

        Check("group has a result when KER has stages", p.HasResult, "");
        Check("group reads the CURRENT stage, not the final one",
              Math.Abs(p.ThrustN - 568000.0) < 1e-9, p.ThrustN.ToString("F0"));

        // ---- the SI fields come through untouched ----
        Check("Δv this stage", Math.Abs(p.DeltaVMps - 390.0) < 1e-9, "");
        Check("Δv remaining is the cumulative total", Math.Abs(p.RemainingDeltaVMps - 415.0) < 1e-9, "");
        Check("actual thrust is separate from available", Math.Abs(p.ActualThrustN - 142000.0) < 1e-9, "");
        Check("TWR trio", Math.Abs(p.Twr - 6.09) < 1e-9 && Math.Abs(p.ActualTwr - 1.52) < 1e-9
                          && Math.Abs(p.MaxTwr - 7.40) < 1e-9, "");
        Check("Isp", Math.Abs(p.IspS - 235.0) < 1e-9, "");
        Check("stage / total / propellant mass", Math.Abs(p.StageMassKg - 9525.0) < 1e-9
              && Math.Abs(p.TotalMassKg - 12055.0) < 1e-9 && Math.Abs(p.ResourceMassKg - 1388.0) < 1e-9, "");
        Check("burn time", Math.Abs(p.BurnTimeS - 31.0) < 1e-9, "");

        // ---- the formatting, unit by unit. Newtons in, kN out: the one conversion a reader can get wrong. ----
        Check("Thrust Avail prints kN, not newtons", p.ThrustAvailText == "568.0 kN", p.ThrustAvailText);
        Check("Thrust Avail is MAX thrust, not the throttled value", p.ThrustAvailText != p.ActualThrustText,
              p.ActualThrustText);
        Check("actual thrust prints kN too", p.ActualThrustText == "142.0 kN", p.ActualThrustText);
        Check("Δv prints m/s", p.DeltaVText == "390 m/s", p.DeltaVText);
        Check("remaining Δv prints m/s", p.RemainingDeltaVText == "415 m/s", p.RemainingDeltaVText);
        Check("TWR prints bare", p.TwrText == "6.09", p.TwrText);
        Check("Isp prints seconds", p.IspText == "235 s", p.IspText);
        Check("burn time prints seconds", p.BurnTimeText == "31 s", p.BurnTimeText);
        Check("stage mass prints kg", p.StageMassText == "9525 kg", p.StageMassText);
        Check("total mass prints kg", p.TotalMassText == "12055 kg", p.TotalMassText);

        // ---- guard 1: KER absent, and guard 2: KER present but no result for this vessel ----
        // Both reach this function the same way — KerBridge.TryGetPerformance returned false, so the caller
        // passes null. The distinction lives in the bridge (Available vs ShowDetails); the outcome here is one
        // outcome, and it must be a dash, never a confident zero.
        KerPerformance absent = KerData.Performance(null, false);
        Check("KER absent: no result", !absent.HasResult, "");
        Check("KER absent: every text is null (=> the page dashes)",
              absent.ThrustAvailText == null && absent.ActualThrustText == null && absent.DeltaVText == null
              && absent.RemainingDeltaVText == null && absent.TwrText == null && absent.IspText == null
              && absent.BurnTimeText == null && absent.StageMassText == null && absent.TotalMassText == null, "");
        Check("KER absent: no plausible zero in the numbers either",
              absent.ThrustN == 0.0 && absent.DeltaVMps == 0.0, "");

        KerPerformance noResult = KerData.Performance(new KerStage[0], false);
        Check("KER present, no result yet: no result", !noResult.HasResult, "");
        Check("KER present, no result yet: dashes", noResult.ThrustAvailText == null, "");

        // An array of stages KER has not filled in (Valid false) is the same case, and must not read as data.
        KerStage[] blank = { new KerStage(), new KerStage() };
        Check("stages present but invalid: no result", !KerData.Performance(blank, false).HasResult, "");

        // ---- the DOCKED guard: KSP merges both craft into one Vessel, so KER simulates the STACK ----
        KerPerformance berthed = KerData.Performance(flying, true);
        Check("docked: no result even with a full sim", !berthed.HasResult, "");
        Check("docked: Thrust Avail dashes rather than showing the merged stack",
              berthed.ThrustAvailText == null, "");
        Check("docked: the raw numbers are withheld too, not just the text",
              berthed.ThrustN == 0.0 && berthed.RemainingDeltaVMps == 0.0, "");

        // ---- a non-finite value anywhere poisons the whole group (registry rule N2) ----
        KerStage[] nan = { Full(0) }; nan[0].IspS = double.NaN;
        Check("NaN anywhere: the whole group dashes", !KerData.Performance(nan, false).HasResult, "");
        KerStage[] inf = { Full(0) }; inf[0].ThrustN = double.PositiveInfinity;
        Check("infinity anywhere: the whole group dashes", !KerData.Performance(inf, false).HasResult, "");

        // ================= S46: and it reaches the GLASS =================
        DisplayList live = Prop(p);
        DisplayList dead = Prop(new KerPerformance());
        Check("PROPULSION draws the KER thrust value", Drew(live, "568.0 kN"), "");
        Check("PROPULSION dashes Thrust Avail with no KER", !Drew(dead, "568.0 kN"), "");
        Check("PROPULSION still shows a dash with no KER", Drew(dead, "—"), "");
        // The label is the template's own copy and is untouched either way (§6 scopes this to VALUES).
        Check("the Thrust Avail label is unchanged", Drew(live, "Thrust Avail") && Drew(dead, "Thrust Avail"), "");
        // And the value is not a constant: a different sim must render differently.
        KerStage[] other = { Full(0) }; other[0].ThrustN = 71000.0;
        Check("PROPULSION's thrust is not hard-coded",
              Drew(Prop(KerData.Performance(other, false)), "71.0 kN"), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
