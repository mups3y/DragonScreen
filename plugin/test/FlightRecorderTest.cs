// Tests for L7 instrumentation: the FlightRecorder (pure/FlightRecorder.cs) — the schema is the single
// source of truth, indices are looked up from it (no drift), formatting is invariant (never a locale
// comma), CSV is escaped, and every controller has a Put* filler that takes its real command struct.
using System;
using DragonScreen;

public static class FlightRecorderTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    static string[] Split(string row)
    {
        // split on commas that are not inside quotes (enough for these tests)
        var outp = new System.Collections.Generic.List<string>();
        var sb = new System.Text.StringBuilder(); bool q = false;
        foreach (char ch in row)
        {
            if (ch == '"') q = !q;
            else if (ch == ',' && !q) { outp.Add(sb.ToString()); sb.Length = 0; }
            else sb.Append(ch);
        }
        outp.Add(sb.ToString());
        return outp.ToArray();
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen L7 flight-recorder tests");

        // ---- schema + indices (drift-proof) ----
        Check("schema is non-trivially wide (records everything)", FlightRecorder.Schema.Length >= 55, FlightRecorder.Schema.Length.ToString());
        Check("every index resolves against the schema",
              FlightRecorder.MetS == 0 && FlightRecorder.RcsGeoRoll == FlightRecorder.Schema.Length - 1
              && FlightRecorder.SkinTempFrac >= 0 && FlightRecorder.RcsGeoPitch >= 0 && FlightRecorder.RcsGeoYaw >= 0
              && FlightRecorder.EngFlameout >= 0 && FlightRecorder.MmhFrac >= 0 && FlightRecorder.NtoFrac >= 0
              && FlightRecorder.KerCurThrustN >= 0 && FlightRecorder.SteerLossMps >= 0
              && FlightRecorder.DragLossMps >= 0 && FlightRecorder.CalKAero >= 0
              && FlightRecorder.RcsBalForceFrac >= 0 && FlightRecorder.CcDsigmaDeg >= 0
              && FlightRecorder.WarpRate >= 0 && FlightRecorder.EngIgnited >= 0
              && FlightRecorder.AoaSignedDeg >= 0 && FlightRecorder.RcsBalTorqueNaive >= 0
              && FlightRecorder.SteerSignC >= 0 && FlightRecorder.KerAvail >= 0, "");
        Check("named indices point at the right columns",
              FlightRecorder.Schema[FlightRecorder.MetS] == "met_s"
              && FlightRecorder.Schema[FlightRecorder.BankDeg] == "bank_deg"
              && FlightRecorder.Schema[FlightRecorder.FdirFault] == "fdir_fault", "");
        Check("header column count == schema length", Split(FlightRecorder.Header()).Length == FlightRecorder.Schema.Length, "");

        // ---- formatting: invariant, blank for NaN, escaping ----
        Check("Num is invariant (dot, not comma)", FlightRecorder.Num(1.5) == "1.5", FlightRecorder.Num(1.5));
        Check("Num blanks NaN (unset)", FlightRecorder.Num(double.NaN) == "", "");
        Check("Num keeps large magnitudes plain", FlightRecorder.Num(6.5e6) == "6500000", FlightRecorder.Num(6.5e6));
        Check("Escape quotes a value containing a comma", FlightRecorder.Escape("a,b") == "\"a,b\"", FlightRecorder.Escape("a,b"));

        // ---- a fresh row is all-blank and the right width ----
        string[] row = FlightRecorder.NewRow();
        Check("a new row has one cell per column", row.Length == FlightRecorder.Schema.Length, "");
        Check("a new row is entirely blank", Split(FlightRecorder.Row(row)).Length == FlightRecorder.Schema.Length && FlightRecorder.Row(row).Replace(",", "") == "", "");

        // ---- fillers put values in the right columns ----
        FlightRecorder.PutTime(row, 123.0);
        FlightRecorder.PutNav(row, 100000, 2000, -50, 1500, 3.2, 45000, 12000);
        Check("PutTime/PutNav land in their columns",
              Split(FlightRecorder.Row(row))[FlightRecorder.MetS] == "123"
              && Split(FlightRecorder.Row(row))[FlightRecorder.AltM] == "100000"
              && Split(FlightRecorder.Row(row))[FlightRecorder.Mach] == "3.2", "");

        // FDIR filler records enum names
        FdirReport rep = new FdirReport { Fault = FaultKind.KeepOutBreach, Response = Recovery.Abort, Abort = true };
        FlightRecorder.PutFdir(row, rep, AbortMode.KosRetreat);
        string[] cells = Split(FlightRecorder.Row(row));
        Check("PutFdir records the fault + recovery + abort mode by name",
              cells[FlightRecorder.FdirFault] == "KeepOutBreach" && cells[FlightRecorder.FdirRecovery] == "Abort"
              && cells[FlightRecorder.FdirAbort] == "1" && cells[FlightRecorder.AbortModeC] == "KosRetreat", "");

        // return filler converts bank rad → deg
        FlightRecorder.PutReturn(row, DepPhase.Phasing, DeorbitPhase.Burn, EntryPhase.Entry,
                                 Math.PI / 2.0, true, ChutePhase.Drogue, true, false);
        cells = Split(FlightRecorder.Row(row));
        Check("PutReturn converts bank rad → degrees and records phases",
              cells[FlightRecorder.BankDeg] == "90" && cells[FlightRecorder.EntryPhaseC] == "Entry"
              && cells[FlightRecorder.ComDescentMode] == "1" && cells[FlightRecorder.Drogue] == "1", "");

        // self-cal filler pulls the live estimates (β = 1/InvBeta)
        SelfCalState sc = new SelfCalState();
        for (int i = 0; i < 10; i++) SelfCal.BallisticCoefficient(ref sc, 4.0e5 / 2000.0, 4.0e5);   // β = 2000
        SelfCal.Thrust(ref sc, 20, 500000);                                                          // F = 1e7
        FlightRecorder.PutSelfCal(row, sc);
        cells = Split(FlightRecorder.Row(row));
        Check("PutSelfCal records thrust + a ballistic-coefficient near 2000",
              cells[FlightRecorder.CalThrustN] == "10000000"
              && Math.Abs(double.Parse(cells[FlightRecorder.CalBeta], System.Globalization.CultureInfo.InvariantCulture) - 2000.0) < 20.0, cells[FlightRecorder.CalBeta]);

        // booster filler takes the real command struct
        BoosterCommand bc = new BoosterCommand { Phase = BoosterPhase.LandingBurn, AoaDeg = 5, EngineMode = VehicleParts.ModeCentreOnly, Throttle = 1.0 };
        FlightRecorder.PutBooster(row, bc, 1200, 45);
        cells = Split(FlightRecorder.Row(row));
        Check("PutBooster records phase + engine mode + AoA from the command",
              cells[FlightRecorder.BoostPhase] == "LandingBurn" && cells[FlightRecorder.EngineMode] == "2"
              && cells[FlightRecorder.BoostAoaDeg] == "5", "");

        // Δv accounting residual
        FlightRecorder.PutDv(row, 100.0, 60.0);
        cells = Split(FlightRecorder.Row(row));
        Check("PutDv records planned/delivered/residual", cells[FlightRecorder.DvResidual] == "40", cells[FlightRecorder.DvResidual]);

        // a fully-populated row still has exactly one cell per column
        Check("a populated row keeps the schema width", Split(FlightRecorder.Row(row)).Length == FlightRecorder.Schema.Length, "");

        // ---- P0.0 instrument: warp stamp + engine state, and the warp-zeroing of stale control columns ----
        FlightRecorder.PutInstrument(row, 1000.0, 9, 1);
        cells = Split(FlightRecorder.Row(row));
        Check("PutInstrument records warp_rate + eng ignited/flameout",
              cells[FlightRecorder.WarpRate] == "1000" && cells[FlightRecorder.EngIgnited] == "9"
              && cells[FlightRecorder.EngFlameout] == "1", "");
        // populate the control columns, then blank them for a warp row; nav/orbit/eng-state must survive.
        FlightRecorder.PutCommand(row, 0.5, 0.1, 0.2, 0.3, 30.0, 0.01, 0.02, 0.9, 0.8, 0.7, 5000.0, 4000.0);
        FlightRecorder.PutRates(row, 8.0, 3.0, 5.0);
        FlightRecorder.ZeroControlColumnsForWarp(row);
        cells = Split(FlightRecorder.Row(row));
        Check("warp-zero BLANKS the stale control columns (act/ctrl_tq/rates/thrust/throttle)",
              cells[FlightRecorder.ActPitchC] == "" && cells[FlightRecorder.CtrlTqPitch] == ""
              && cells[FlightRecorder.RatePitchDps] == "" && cells[FlightRecorder.RcsThrustN] == ""
              && cells[FlightRecorder.Throttle] == "" && cells[FlightRecorder.AttPointDeg] == "", "");
        Check("warp-zero KEEPS nav/orbit/eng-state (alt, ap, mass, warp_rate, eng_ignited)",
              cells[FlightRecorder.AltM] != "" && cells[FlightRecorder.WarpRate] == "1000"
              && cells[FlightRecorder.EngIgnited] == "9", "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
