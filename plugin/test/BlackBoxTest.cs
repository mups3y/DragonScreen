/*
 * BlackBoxTest — the headless half of register BB1 (the flight recorder core).
 *
 * ---- WHAT A HEADLESS SUITE CAN AND CANNOT PROVE HERE, STATED UP FRONT ----
 * The recorder splits pure / glue exactly the way the rest of the tree does, and the split is not
 * cosmetic: everything below is decidable WITHOUT KSP, and it is the half where the last two recorders
 * actually went wrong. Recorder A destroyed commas and coerced NaN to 0.0; Recorder B zeroed control
 * columns under warp; both hard-coded a row period an analyser then assumed; three columns were
 * declared and never written and nobody noticed until S76 audited the corpus AFTER the flights. Every
 * one of those is a pure-side property and every one of them is asserted below.
 *
 * ⛔ WHAT IT DOES NOT PROVE: that the glue reads the right KSP field into the right column. That is
 *    `BlackBoxRecorder.cs`, it needs a Vessel, and it is confirmed on the glass by register **BB4**.
 *    The one structural guard against it — the header/row width check — is deliberately IN THE GAME
 *    (`VerifyWidth`), for the reason Recorder A gave: the row is built from live vessel state, so no
 *    headless test ever executes that path.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using DragonScreen;
using DragonScreen.BlackBox;

public static class BlackBoxTest
{
    static int bad;
    static int checks;

    static void Check(bool ok, string what)
    {
        // Counted as well as asserted. A suite that prints "ok" having run zero checks is exactly the
        // fake coverage this whole task is about, one level up.
        checks++;
        if (ok) return;
        bad++;
        Console.WriteLine("  FAIL: " + what);
    }

    public static int Run()
    {
        bad = 0; checks = 0;
        Console.WriteLine("BlackBoxTest (BB1 recorder core + BB2 two-vessel: schema, validity, rates, "
                          + "manifest, coverage, naming, scope, the two-stream join)");

        Schema();
        Formatting();
        Validity();
        RateLadder();
        WarpVoid();
        Accumulators();
        Coverage();
        Events();
        Manifest();
        Pipeline();
        Naming();            // BB2
        ScopeDeclarations(); // BB2
        TrackedCoverage();   // BB2
        TwoVesselPipeline(); // BB2

        Console.WriteLine("  " + checks + " checks, " + bad + " failed");
        return bad == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- schema integrity
    static void Schema()
    {
        Col[] cols = BlackBoxSchema.Columns;
        Check(cols.Length > 100, "the schema is populated (got " + cols.Length + ")");
        Check(BlackBoxSchema.Width == cols.Length, "Width == the column count");
        Check(BlackBoxSchema.Schema.Length == cols.Length, "the derived name array matches the table");

        // ⛔ A DUPLICATE NAME IS UNDETECTABLE IN A CSV. `Index()` returns the FIRST match, so the
        // second column with that name would never be written, would never be read, and would shift
        // nothing — a silent permanent blank. This is the ghost defect in its purest form.
        var seen = new Dictionary<string, int>();
        for (int i = 0; i < cols.Length; i++)
        {
            Check(!seen.ContainsKey(cols[i].Name), "column name '" + cols[i].Name + "' is unique");
            seen[cols[i].Name] = i;
            Check(!string.IsNullOrEmpty(cols[i].Name), "column " + i + " has a name");
            Check(!string.IsNullOrEmpty(cols[i].Units), "column '" + cols[i].Name + "' declares units");
            Check(!string.IsNullOrEmpty(cols[i].Provenance), "column '" + cols[i].Name + "' declares provenance");
            Check(!string.IsNullOrEmpty(cols[i].Source), "column '" + cols[i].Name + "' declares a source");
        }

        // Every name resolves to its own position, which is the whole point of the derived-index
        // pattern: re-ordering the table must be safe, and it is only safe if this holds.
        for (int i = 0; i < cols.Length; i++)
            Check(BlackBoxSchema.Index(cols[i].Name) == i, "Index('" + cols[i].Name + "') round-trips");
        Check(BlackBoxSchema.Index("no_such_column") == -1, "an unknown name resolves to -1, not 0");

        // Every generated index in BlackBoxCols is bound. A -1 here means a name was renamed in the
        // table without renaming its index, and its writer would then silently write nothing.
        Check(BlackBoxCols.Ut >= 0 && BlackBoxCols.Seq >= 0 && BlackBoxCols.MissionId >= 0,
              "the A-block indices bind");
        Check(BlackBoxCols.RecBuildUs >= 0, "rec_build_us binds — the recorder measures itself (§1.4(b))");
        Check(BlackBoxCols.WarpRate >= 0 && BlackBoxCols.WarpRails >= 0,
              "warp_rate/warp_rails bind — S76 defect 4: a reader must filter without inferring");

        // ⭐ The owner's Q2 observability refinement, 2026-09-04: `pure/BoosterSteer.cs` names THIS
        // register line as the reader for the deadband seam. If these ever stop existing, the seam
        // becomes undiagnosable again, which is precisely what the refinement was for.
        Check(BlackBoxCols.BoostDbPitch >= 0 && BlackBoxCols.BoostDbYaw >= 0 && BlackBoxCols.BoostDbRoll >= 0,
              "the booster deadband-ACTIVE columns exist (owner Q2, BoosterSteer.cs:43)");
        Check(BlackBoxCols.BoostDbDeg >= 0,
              "the booster deadband-VALUE column exists (owner Q2, BoosterSteer.cs:43)");

        // Tier discipline: the A block is on every row by definition (§2.1), and any other tier there
        // would make one of the ten unconditional columns conditional without saying so.
        Check(TierOf("mission_id") == Tier.Every && TierOf("seq") == Tier.Every
              && TierOf("ut") == Tier.Every && TierOf("met_s") == Tier.Every
              && TierOf("wall_s") == Tier.Every && TierOf("warp_rate") == Tier.Every
              && TierOf("warp_rails") == Tier.Every && TierOf("vessel") == Tier.Every
              && TierOf("focus") == Tier.Every && TierOf("rec_build_us") == Tier.Every,
              "the whole A block is Tier.Every (§2.1)");

        // §2.0's justification for R1 is the tune: the AoA/Q/pitch traces §B8 diagnoses from must be at
        // 10 Hz or the diagnosis (spike-at-max-Q vs deviation-before-max-Q) is not resolvable.
        Check(TierOf("q_pa") == Tier.R1 && TierOf("aoa_deg") == Tier.R1 && TierOf("pitch_deg") == Tier.R1
              && TierOf("mach") == Tier.R1 && TierOf("throttle") == Tier.R1,
              "the §B8 tune signals are R1 (§2.2/§2.3's rate justifications)");

        // Every Unfitted column names a register line, and no Live/Conditional column pretends to.
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i].Fit == Fit.Unfitted)
                Check(!string.IsNullOrEmpty(cols[i].Note),
                      "unfitted column '" + cols[i].Name + "' names the line that will fill it");
            if (cols[i].Fit == Fit.Conditional)
                Check(!string.IsNullOrEmpty(cols[i].Note),
                      "conditional column '" + cols[i].Name + "' states when it is blank");
            if (cols[i].Fit == Fit.Live)
                Check(cols[i].Note == null, "live column '" + cols[i].Name + "' carries no condition");
        }

        // The manifest's period_s must be null exactly for the tiers that ride the row rate — an
        // analyser forward-filling on a period of -1 would fill for a negative second.
        Check(BlackBoxSchema.PeriodOf(Tier.Every) < 0.0 && BlackBoxSchema.PeriodOf(Tier.R0) < 0.0,
              "Every and R0 have no period of their own");
        Check(Math.Abs(BlackBoxSchema.PeriodOf(Tier.R1) - 0.1) < 1e-12, "R1 = 0.1 s (10 Hz)");
        Check(Math.Abs(BlackBoxSchema.PeriodOf(Tier.R2) - 0.5) < 1e-12, "R2 = 0.5 s (2 Hz)");
        Check(Math.Abs(BlackBoxSchema.PeriodOf(Tier.R3) - 10.0) < 1e-12, "R3 = 10 s (0.1 Hz)");
    }

    static Tier TierOf(string name)
    {
        int i = BlackBoxSchema.Index(name);
        return i < 0 ? Tier.Every : BlackBoxSchema.Columns[i].Tier;
    }

    // ---------------------------------------------------------------- formatting
    static void Formatting()
    {
        // ⛔ RECORDER A DESTROYED COMMAS (`s.Replace(',', ';')`) — a data-falsifying "fix". RFC-4180
        // quotes instead, so the value survives the round trip.
        Check(BlackBoxSchema.Escape("a,b") == "\"a,b\"", "a comma is quoted, not substituted");
        Check(BlackBoxSchema.Escape("say \"hi\"") == "\"say \"\"hi\"\"\"", "an inner quote is doubled");
        Check(BlackBoxSchema.Escape("one\ntwo") == "\"one\ntwo\"", "a newline is quoted");
        Check(BlackBoxSchema.Escape("plain") == "plain", "a plain value is left alone");
        Check(BlackBoxSchema.Escape(null) == "", "null escapes to blank");

        // ⛔ INVARIANT CULTURE, ALWAYS. A European locale writes "1,5" and shreds the CSV. Assert it
        // under a culture that WOULD, rather than trusting the ambient one — the machine that runs the
        // tests is not the machine that runs the game.
        CultureInfo saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Check(BlackBoxSchema.Num(1.5) == "1.5", "Num is invariant under a comma-decimal culture");
            Check(BlackBoxEvents.JsonNum(1.5) == "1.5", "JsonNum is invariant too");
        }
        finally { Thread.CurrentThread.CurrentCulture = saved; }

        Check(BlackBoxSchema.Num(0.0) == "0", "zero formats as 0");
        Check(BlackBoxSchema.Bit(true) == "1" && BlackBoxSchema.Bit(false) == "0", "bools are 1/0");

        // Field counting has to survive quoting, or the width check would reject a legitimate row that
        // merely contained a comma — and then a real recording would be thrown away as malformed.
        Check(BlackBoxSchema.CountFields("a,b,c") == 3, "three plain fields count as three");
        Check(BlackBoxSchema.CountFields("a,\"b,b\",c") == 3, "a quoted comma does not split a field");
        Check(BlackBoxSchema.CountFields(BlackBoxSchema.Header()) == BlackBoxSchema.Width,
              "the header is exactly Width fields wide");
    }

    // ---------------------------------------------------------------- §4.6 validity
    static void Validity()
    {
        string[] row = BlackBoxSchema.NewRow();
        Check(row.Length == BlackBoxSchema.Width, "NewRow is schema-wide");
        for (int i = 0; i < row.Length; i++)
            if (row[i] != "") { Check(false, "NewRow pre-fills every cell BLANK, not null or zero"); break; }

        // ⛔ THE RULE: NaN/Inf -> BLANK, NEVER 0.0. Recorder A coerced NaN to 0.0, and §4.6 is explicit
        // about why that is data-falsifying: a zero is a legitimate measurement and a blank is not, so
        // coercion manufactures a reading that was never taken.
        Check(BlackBoxSchema.Num(double.NaN) == "", "NaN is blank, not 0");
        Check(BlackBoxSchema.Num(double.PositiveInfinity) == "", "+Inf is blank");
        Check(BlackBoxSchema.Num(double.NegativeInfinity) == "", "-Inf is blank");
        BlackBoxSchema.Set(row, BlackBoxCols.QPa, double.NaN);
        Check(row[BlackBoxCols.QPa] == "", "Set(NaN) leaves the cell blank");
        BlackBoxSchema.Set(row, BlackBoxCols.QPa, 0.0);
        Check(row[BlackBoxCols.QPa] == "0", "a REAL zero is still recorded as 0 — the two are different facts");

        // JSON's version of the same rule: `NaN` is not JSON and `json.loads` rejects the whole line,
        // taking a real event with it. `null` is the type system's own "no value".
        Check(BlackBoxEvents.JsonNum(double.NaN) == "null", "a NaN payload value is JSON null");

        // Set(-1) must be a silent no-op: a removed column's writers keep compiling and simply stop
        // writing, so a schema edit can never corrupt its neighbours.
        BlackBoxSchema.Set(row, -1, 42.0);
        BlackBoxSchema.Set(row, -1, "x");
        BlackBoxSchema.Set(row, -1, true);
        Check(true, "Set(-1) does not throw");

        // A string written into a cell is stored RAW and escaped once on the way out. Escaping in both
        // places would double-quote and the value would stop round-tripping.
        BlackBoxSchema.Set(row, BlackBoxCols.Vessel, "Crew-2, backup");
        Check(row[BlackBoxCols.Vessel] == "Crew-2, backup", "Set(string) stores the raw value");
        string line = BlackBoxSchema.Row(row);
        Check(line.Contains("\"Crew-2, backup\""), "Row() escapes it exactly once");
        Check(BlackBoxSchema.CountFields(line) == BlackBoxSchema.Width,
              "a row with an embedded comma is still exactly Width fields wide");
    }

    // ---------------------------------------------------------------- §2.0 / §4.6 cadence
    static void RateLadder()
    {
        RatePolicy adaptive = RatePolicy.Adaptive();
        Check(Math.Abs(adaptive.RowRateDynamicHz - 10.0) < 1e-9, "adaptive dynamic rate is 10 Hz (§2.0)");
        Check(Math.Abs(adaptive.RowRateQuiescentHz - 2.0) < 1e-9, "adaptive quiescent rate is 2 Hz (§2.0)");

        // BB1-Q1 is OPEN. What is asserted is that options (b) and (c) are a VALUE, not a rewrite —
        // so whichever way the owner rules, no other file in the recorder changes.
        RatePolicy fixed10 = RatePolicy.Fixed(10.0);
        Check(fixed10.Mode == RateMode.Fixed
              && Math.Abs(fixed10.RowRateDynamicHz - 10.0) < 1e-9
              && Math.Abs(fixed10.RowRateQuiescentHz - 10.0) < 1e-9,
              "BB1-Q1 option (b): fixed 10 Hz is one policy value");
        RatePolicy fixed5 = RatePolicy.Fixed(5.0);
        Check(Math.Abs(fixed5.RowRateQuiescentHz - 5.0) < 1e-9,
              "BB1-Q1 option (c): fixed 5 Hz is one policy value");

        // §2.0's dynamic-phase rule, clause by clause.
        Check(BlackBoxRate.IsDynamic(MissionPhase.Ascent, false, 0, false, 0, false), "Ascent is dynamic");
        Check(BlackBoxRate.IsDynamic(MissionPhase.Entry, false, 0, false, 0, false), "Entry is dynamic");
        Check(BlackBoxRate.IsDynamic(MissionPhase.Drogues, false, 0, false, 0, false), "Drogues is dynamic");
        Check(BlackBoxRate.IsDynamic(MissionPhase.Mains, false, 0, false, 0, false), "Mains is dynamic");
        Check(BlackBoxRate.IsDynamic(MissionPhase.Coast, true, 0, false, 0, false), "an abort is dynamic in any phase");
        Check(BlackBoxRate.IsDynamic(MissionPhase.Coast, false, 1.0, false, 0, false), "a powered burn is dynamic");
        Check(BlackBoxRate.IsDynamic(MissionPhase.Coast, false, 0, true, 0, false), "commanded RCS translation is dynamic");
        Check(BlackBoxRate.IsDynamic(MissionPhase.Approach, false, 0, false, 900.0, true), "Approach inside 1 km is dynamic");
        Check(!BlackBoxRate.IsDynamic(MissionPhase.Approach, false, 0, false, 5000.0, true), "Approach at 5 km is not");
        Check(!BlackBoxRate.IsDynamic(MissionPhase.Phasing, false, 0, false, 0, false), "a quiet coast is not dynamic");

        // A fresh stream's first row must fire immediately, whatever the clock reads. Recorder B's
        // `lastSampleT = -1e9` existed for exactly this and the comment said so: a revert can move UT
        // backwards, so "never sampled" cannot be 0.
        RateState st = RateState.Fresh();
        RateInputs now = In(1000.0, 50.0, false, false);
        RowPlan p = BlackBoxRate.Plan(adaptive, st, now);
        Check(p.Due && p.FillR2 && p.FillR3, "the first row fires and carries every tier");
        st = BlackBoxRate.Advance(st, p, now);

        // Quiescent: 2 Hz. A row 0.1 s later is NOT due.
        p = BlackBoxRate.Plan(adaptive, st, In(1000.1, 50.1, false, false));
        Check(!p.Due, "quiescent, 0.1 s later: not due (2 Hz)");
        p = BlackBoxRate.Plan(adaptive, st, In(1000.5, 50.5, false, false));
        Check(p.Due, "quiescent, 0.5 s later: due");

        // Dynamic: 10 Hz off the SAME state, which is what makes the ladder adaptive rather than two
        // separate recorders.
        p = BlackBoxRate.Plan(adaptive, st, In(1000.1, 50.1, true, false));
        Check(p.Due, "dynamic, 0.1 s later: due (10 Hz)");

        // R2/R3 decimation is against each tier's OWN elapsed UT, not the row's — which is what makes
        // the manifest's period_s a sufficient forward-fill instruction.
        st = RateState.Fresh();
        now = In(2000.0, 0.0, true, false);
        p = BlackBoxRate.Plan(adaptive, st, now); st = BlackBoxRate.Advance(st, p, now);
        now = In(2000.1, 0.1, true, false);
        p = BlackBoxRate.Plan(adaptive, st, now);
        Check(p.Due && !p.FillR2 && !p.FillR3, "an R1 row 0.1 s on carries neither R2 nor R3");
        st = BlackBoxRate.Advance(st, p, now);
        now = In(2000.5, 0.5, true, false);
        p = BlackBoxRate.Plan(adaptive, st, now);
        Check(p.Due && p.FillR2 && !p.FillR3, "at 0.5 s the R2 block fills, R3 does not");
        st = BlackBoxRate.Advance(st, p, now);
        now = In(2010.0, 10.0, true, false);
        p = BlackBoxRate.Plan(adaptive, st, now);
        Check(p.Due && p.FillR2 && p.FillR3, "at 10 s the R3 block fills");

        // ⛔ §4.6's WARP FLOOR. This is the rule S76's evidence demanded: without it, on-rails UT
        // advancing 1000x per frame writes ~50 rows per WALL second of nothing, and those rows then
        // pollute every statistic. With it, a 100x phasing coast writes ~1 row per wall-second.
        st = RateState.Fresh();
        now = In(3000.0, 100.0, false, true);
        p = BlackBoxRate.Plan(adaptive, st, now); st = BlackBoxRate.Advance(st, p, now);
        // 50 s of UT has passed (100x warp) but only 0.5 wall-seconds: the floor holds the row back.
        p = BlackBoxRate.Plan(adaptive, st, In(3050.0, 100.5, false, true));
        Check(!p.Due, "on rails, 50 s of UT in 0.5 wall-s does NOT emit — the wall floor holds");
        p = BlackBoxRate.Plan(adaptive, st, In(3100.0, 101.0, false, true));
        Check(p.Due, "on rails, one WALL second later: due");
        // The floor lengthens the interval; it must never shorten it. A wall second in which UT barely
        // moved is still not a row.
        st = RateState.Fresh();
        now = In(4000.0, 200.0, false, true);
        p = BlackBoxRate.Plan(adaptive, st, now); st = BlackBoxRate.Advance(st, p, now);
        p = BlackBoxRate.Plan(adaptive, st, In(4000.01, 201.0, false, true));
        Check(!p.Due, "the wall floor never OVERRIDES the UT period, it only extends it");

        // A revert moves UT backwards. If that were treated as "not due", the row that shows the
        // discontinuity would be the one row never written.
        st = RateState.Fresh();
        now = In(5000.0, 300.0, true, false);
        p = BlackBoxRate.Plan(adaptive, st, now); st = BlackBoxRate.Advance(st, p, now);
        p = BlackBoxRate.Plan(adaptive, st, In(4900.0, 300.05, true, false));
        Check(p.Due, "UT moving BACKWARDS is due, not stuck (a revert must not silence the recorder)");
    }

    static RateInputs In(double ut, double wall, bool dynamic, bool rails)
    {
        RateInputs r;
        r.Ut = ut; r.Wall = wall; r.Dynamic = dynamic; r.RailsWarp = rails;
        return r;
    }

    // ---------------------------------------------------------------- §4.6 the warp void
    static void WarpVoid()
    {
        string[] row = BlackBoxSchema.NewRow();
        // Fill the whole row with a legitimate ZERO — the exact value Recorder B wrote under warp, and
        // the reason blanking is strictly better: after voiding, a control cell must be EMPTY, not "0".
        for (int i = 0; i < row.Length; i++) row[i] = "0";
        BlackBoxVoid.Apply(row);

        Check(BlackBoxVoid.ControlColumnCount > 20, "the control block is a real set, not an empty list");
        Check(row[BlackBoxCols.Throttle] == "", "throttle is BLANKED under rails warp, not zeroed");
        Check(row[BlackBoxCols.AppPitch] == "" && row[BlackBoxCols.AppTx] == "",
              "the applied command block is blanked");
        Check(row[BlackBoxCols.RatePitchDps] == "" && row[BlackBoxCols.AttRateMeas] == "",
              "measured body rates are blanked — the physics loop did not run");
        Check(row[BlackBoxCols.QPa] == "" && row[BlackBoxCols.AoaDeg] == "",
              "q and the flow angles are blanked — they come off a velocity nothing integrated");
        Check(row[BlackBoxCols.AccIntS] == "" && row[BlackBoxCols.ActSatS] == "",
              "every R0 accumulator is blanked");
        Check(row[BlackBoxCols.BoostSteerPitch] == "" && row[BlackBoxCols.BoostDbPitch] == "",
              "the booster steering block is blanked too");

        // ⛔ AND THE OTHER HALF, WHICH IS THE ONE EASY TO GET WRONG. On rails the orbit is exactly what
        // is being propagated and resources do not drain, so these are TRUE readings of TRUE state.
        // Voiding them would delete the only data a 17-hour phasing coast produces.
        Check(row[BlackBoxCols.ApKm] == "0" && row[BlackBoxCols.PeKm] == "0", "the orbit survives warp");
        Check(row[BlackBoxCols.AltM] == "0" && row[BlackBoxCols.MassKg] == "0", "altitude and mass survive");
        Check(row[BlackBoxCols.EcFrac] == "0" && row[BlackBoxCols.MmhFrac] == "0",
              "resource fractions survive — they do not drain on rails");
        Check(row[BlackBoxCols.MissionPhase] == "0" && row[BlackBoxCols.Bus1On] == "0",
              "phase and the systems model survive");
        Check(row[BlackBoxCols.EngIgnited] == "0", "engine ignition state survives (§2.3: a commanded ignition is provable)");
        Check(row[BlackBoxCols.WarpRate] == "0" && row[BlackBoxCols.WarpRails] == "0" && row[BlackBoxCols.Ut] == "0",
              "the A block survives — it is what a reader FILTERS on");
    }

    // ---------------------------------------------------------------- §2.0's R0 tier
    static void Accumulators()
    {
        BlackBoxAccum a = BlackBoxAccum.Fresh();
        Check(!a.Any, "a fresh accumulator has nothing to emit");

        string[] row = BlackBoxSchema.NewRow();
        a.Put(row);
        Check(row[BlackBoxCols.AccIntS] == "",
              "an EMPTY interval writes blank, not a zero duty cycle — a zero would be a claim");

        // 10 physics ticks at 0.02 s: attitude only for 4, translation only for 2, both for 1, none 3.
        for (int i = 0; i < 4; i++) a.Add(0.02, 0.5, 0.0, 1.0, 100.0, 2.0);
        for (int i = 0; i < 2; i++) a.Add(0.02, 0.0, 0.4, 1.0, 100.0, 2.0);
        a.Add(0.02, 0.6, 0.3, 3.9, 30500.0, 11.0);
        for (int i = 0; i < 3; i++) a.Add(0.02, 0.0, 0.0, 1.0, 100.0, 2.0);

        Check(Math.Abs(a.IntervalS - 0.2) < 1e-9, "the interval sums to 0.2 s");
        // ⛔ THE IDENTITY THAT MAKES A DUTY CYCLE COMPUTABLE: the four categories are mutually exclusive
        // and sum to the interval. §3.2's retracted "68-82 % duty" came from snapshots, where no such
        // identity holds and the number is an alias.
        Check(Math.Abs((a.AttS + a.TransS + a.BothS + a.NoneS) - a.IntervalS) < 1e-9,
              "the four actuation categories sum EXACTLY to the interval");
        Check(Math.Abs(a.AttS - 0.08) < 1e-9, "attitude-only time is 4 ticks");
        Check(Math.Abs(a.TransS - 0.04) < 1e-9, "translation-only time is 2 ticks");
        Check(Math.Abs(a.BothS - 0.02) < 1e-9, "both-commanded time is 1 tick");
        Check(Math.Abs(a.NoneS - 0.06) < 1e-9, "idle time is 3 ticks");

        // ⛔ THE PEAK IS THE POINT. §B11's ~4 g and §B8's 30-35 kPa are PEAKS, and a peak passing
        // between two snapshots of a rising curve never appears at all. Here the 3.9 g / 30.5 kPa tick
        // is one of ten and it is the one that must survive.
        Check(Math.Abs(a.PeakAccelG - 3.9) < 1e-9, "the in-interval g PEAK survives a single tick");
        Check(Math.Abs(a.PeakQPa - 30500.0) < 1e-9, "the in-interval max-Q peak survives");
        Check(Math.Abs(a.PeakRateDps - 11.0) < 1e-9, "the in-interval rate peak survives");

        // Saturation is TIME at the limit, not a snapshot of being at it.
        BlackBoxAccum s = BlackBoxAccum.Fresh();
        s.Add(0.02, 1.0, 0.0, 1, 0, 0);
        s.Add(0.02, 0.5, 0.0, 1, 0, 0);
        Check(Math.Abs(s.SatS - 0.02) < 1e-9, "act_sat_s accumulates TIME at |command| >= 0.99");

        // A bad dt must not poison an interval — the recorder never fabricates (§4.8).
        BlackBoxAccum g = BlackBoxAccum.Fresh();
        g.Add(double.NaN, 1, 1, 1, 1, 1);
        g.Add(-0.5, 1, 1, 1, 1, 1);
        g.Add(0.0, 1, 1, 1, 1, 1);
        Check(!g.Any, "NaN / negative / zero dt is ignored, not accumulated");

        a.Put(row);
        Check(row[BlackBoxCols.AccIntS] != "" && row[BlackBoxCols.AccAttS] != ""
              && row[BlackBoxCols.ActSatS] != "" && row[BlackBoxCols.AccelGPeak] != "",
              "a populated interval writes every R0 column");
    }

    // ---------------------------------------------------------------- ⭐ the S76 ghost-column defect
    static void Coverage()
    {
        // A stream that never ran reports nothing: "the recorder never started" is a different fault
        // and the manifest's rows_written already states it.
        var empty = new BlackBoxCoverage();
        Check(empty.Findings().Count == 0, "no rows means no coverage verdict");

        // A mission where every Live column was written and no Unfitted one was: the clean case. An
        // EMPTY findings list is a POSITIVE statement, and it is the property S76 had to discover by
        // auditing the whole corpus after the flights.
        var clean = new BlackBoxCoverage();
        string[] row = BlackBoxSchema.NewRow();
        for (int i = 0; i < row.Length; i++)
            if (BlackBoxSchema.Columns[i].Fit != Fit.Unfitted) row[i] = "1";
        clean.Note(row);
        List<CoverageFinding> f = clean.Findings();
        int defects = 0;
        for (int i = 0; i < f.Count; i++) if (f[i].Defect) defects++;
        Check(defects == 0, "every Live column written + no Unfitted one written = zero defects");

        // ⛔ THE torque_cmd CASE, REPRODUCED. One Live column left blank across the whole mission is a
        // DEFECT and is named. This is the check BB1's register line requires: "BB1 must fail loudly
        // ... if a declared column is never populated across a mission."
        var ghost = new BlackBoxCoverage();
        string[] r2 = BlackBoxSchema.NewRow();
        for (int i = 0; i < r2.Length; i++)
            if (BlackBoxSchema.Columns[i].Fit != Fit.Unfitted) r2[i] = "1";
        r2[BlackBoxCols.ThrustN] = "";      // a Live column with no writer — the defect, exactly
        ghost.Note(r2);
        bool namedIt = false;
        f = ghost.Findings();
        for (int i = 0; i < f.Count; i++)
            if (f[i].Column == "thrust_n" && f[i].Defect && f[i].Kind == "never_written") namedIt = true;
        Check(namedIt, "a LIVE column never written is reported as a DEFECT, by name (the torque_cmd case)");

        // A Conditional column left blank is a fact about the FLIGHT, not about the recorder: "no
        // target was ever selected" is not a bug. It is reported, with its declared condition, and it
        // is NOT a defect — because a defect that fires on every flight is a defect nobody reads.
        var cond = new BlackBoxCoverage();
        string[] r3 = BlackBoxSchema.NewRow();
        for (int i = 0; i < r3.Length; i++)
            if (BlackBoxSchema.Columns[i].Fit == Fit.Live) r3[i] = "1";
        cond.Note(r3);
        bool condNoted = false, condDefect = false;
        f = cond.Findings();
        for (int i = 0; i < f.Count; i++)
            if (f[i].Column == "range_m") { condNoted = true; condDefect = f[i].Defect; }
        Check(condNoted, "a CONDITIONAL column never written is still reported");
        Check(!condDefect, "...but as a NOTE, not a defect — otherwise it fires on every flight");
        for (int i = 0; i < f.Count; i++)
            if (f[i].Column == "pvg_vgo_mps")
                Check(false, "an UNFITTED column never written is SILENT — that is its declared state");

        // The other direction, which nobody thought to check last time: an Unfitted column that
        // produced values means the manifest's provenance is now wrong.
        var surprise = new BlackBoxCoverage();
        string[] r4 = BlackBoxSchema.NewRow();
        for (int i = 0; i < r4.Length; i++) r4[i] = "1";
        surprise.Note(r4);
        bool flagged = false;
        f = surprise.Findings();
        for (int i = 0; i < f.Count; i++)
            if (f[i].Kind == "unexpected_writer" && f[i].Defect) flagged = true;
        Check(flagged, "an UNFITTED column that WAS written is a defect the other way round");

        // Coverage is cumulative across rows: a column written once, on one row of a 120 000-row
        // mission, has a writer. Anything stricter would flag every event-driven column.
        var acc = new BlackBoxCoverage();
        string[] a1 = BlackBoxSchema.NewRow();
        string[] a2 = BlackBoxSchema.NewRow();
        a1[BlackBoxCols.ThrustN] = "5";
        acc.Note(a2); acc.Note(a1); acc.Note(a2);
        Check(acc.WasWritten(BlackBoxCols.ThrustN), "one non-blank row is enough to prove a writer exists");
        Check(acc.Rows == 3, "rows are counted");
    }

    // ---------------------------------------------------------------- §2.9 / §4.1 the event log
    static void Events()
    {
        string line = BlackBoxEvents.Line("Crew-2_20260904_120000", "Crew-2", 1234.5, 60.25, 42,
                                          BlackBoxEvents.FlightMaxQ,
                                          new[] { Kv.Num("peak_q_pa", 30500.0), Kv.Num("alt_m", 12000.0) });
        Check(line.StartsWith("{") && line.EndsWith("}"), "an event is one JSON object");
        Check(line.IndexOf('\n') < 0, "...on exactly one line, so JSONL tolerates a truncated final line");
        Check(line.Contains("\"kind\":\"flight.maxq\""), "the kind is carried");
        // §4.5: an event carries its OWN ut plus the seq of the row it falls between — so it is placed
        // exactly AND joinable to the stream without a search.
        Check(line.Contains("\"ut\":1234.5"), "the event carries its own ut, not the next row's");
        Check(line.Contains("\"seq\":42"), "and the seq of the row it falls between");
        Check(line.Contains("\"peak_q_pa\":30500"), "the payload is typed, not an escaped blob");

        // Recorder A's `a_note` was an escaped free-text blob that no tool ever parsed. A payload here
        // must survive the characters that broke it.
        string q = BlackBoxEvents.Line("m", "v", 1, 2, 3, "rec.write_error",
                                       new[] { Kv.Str("error", "path \"C:\\a\", line\n2") });
        Check(q.IndexOf('\n') < 0, "an embedded newline is escaped, not emitted raw");
        Check(q.Contains("\\\\a"), "a backslash is escaped");
        Check(q.Contains("\\\""), "a quote is escaped");
        Check(BlackBoxEvents.JsonString(null) == "null", "a null string is JSON null");
        Check(BlackBoxEvents.JsonString("\t").Contains("\\t"), "a tab is escaped");

        string none = BlackBoxEvents.Line("m", "v", 0, 0, 0, "rec.open", null);
        Check(none.Contains("\"p\":{}"), "an empty payload is an empty object, not missing");

        // The kind constants exist so a typo is a compile error rather than a silently lost channel.
        Check(BlackBoxEvents.RecColumnNeverWritten == "rec.column_never_written",
              "the ghost-column event has a named kind");
        Check(BlackBoxEvents.RecStreamEnd == "rec.stream_end",
              "the clean-close marker has a named kind (the torn-row guard)");
    }

    // ---------------------------------------------------------------- §4.3 the manifest
    static void Manifest()
    {
        ManifestInfo m = ManifestInfo.Fresh();
        m.MissionId = "Crew-2_20260904_120000";
        m.Vessel = "Crew-2";
        m.Policy = RatePolicy.Adaptive();
        m.ModVersions.Add("RealFuels 13.3.2");
        m.Tunables.Add("BoosterSteer.DeadbandDeg = 0");

        string open = BlackBoxManifest.Build(m);
        Check(open.Contains("\"schema_version\": 1"), "the schema version is recorded (§4.2's chain guard)");
        Check(open.Contains("\"mission_id\": \"Crew-2_20260904_120000\""), "the mission id is recorded");
        Check(open.Contains("\"closed\": false"), "before close, the manifest SAYS it is not closed");
        Check(open.Contains("\"closed_ut\": null"), "...and the tail is null rather than a plausible number");
        Check(open.Contains("\"row_rate_dynamic_hz\": 10"), "the flown cadence is recorded, never assumed");
        Check(open.Contains("\"row_rate_quiescent_hz\": 2"), "both halves of it");
        Check(open.Contains("RealFuels 13.3.2"), "mod versions are recorded (§1.5: decodable in six months)");
        Check(open.Contains("BoosterSteer.DeadbandDeg = 0"),
              "every [Tunable] is recorded — §B5 changes ONE at a time and two flights must be tellable apart");

        // ⛔ §14.4(e)/(f) MARKING, ONCE, CHEAPLY. Every simulated value declares itself in ONE place, so
        // the overseer cannot mistake a marked simulation for a measurement — and no per-column
        // provenance string is written 120 000 times.
        Check(open.Contains("\"provenance\": \"SIMULATED\""),
              "the SIMULATED provenance marking is present in the manifest");
        Check(open.Contains("\"name\": \"bus1_on\""), "columns[] carries every column by name");
        Check(open.Contains("\"fit\": \"unfitted\""), "the unfitted seams declare themselves as such");
        Check(open.Contains("\"fit\": \"conditional\""), "and the conditional ones as such");

        // A column that rides the row rate must report NO period, or a forward-fill would fill for a
        // negative second and quietly invent data.
        Check(open.Contains("\"name\": \"ut\", \"units\": \"s\", \"tier\": \"every\", \"period_s\": null"),
              "an Every-tier column reports period_s null, not -1");

        m.Closed = true;
        m.ClosedReason = "scene_change";
        m.ClosedUt = 4242.0;
        m.RowsWritten = 1234;
        m.MaxRecBuildUs = 87.5;
        CoverageFinding cf;
        cf.Column = "thrust_n"; cf.Kind = "never_written"; cf.Defect = true; cf.Declared = "declared Live";
        m.Coverage.Add(cf);
        string closed = BlackBoxManifest.Build(m);
        Check(closed.Contains("\"closed\": true") && closed.Contains("\"closed_reason\": \"scene_change\""),
              "the finalised manifest carries the close reason");
        Check(closed.Contains("\"rows_written\": 1234"),
              "rows_written is recorded — a reader compares it to the CSV and detects a torn tail exactly");
        Check(closed.Contains("\"max_rec_build_us\": 87.5"),
              "the recorder's own worst frame cost is a NUMBER in the file, not an argument (§1.4(b))");
        Check(closed.Contains("\"column\": \"thrust_n\"") && closed.Contains("\"defect\": true"),
              "the coverage verdict lands in the manifest");
    }

    // ---------------------------------------------------------------- the whole pure pipeline
    /// <summary>
    /// A synthetic 20-second mission driven through the ENTIRE pure path — rate ladder, row
    /// construction, R0 accumulation, the warp void, coverage, manifest — asserting the properties
    /// that hold ACROSS rows rather than within one.
    ///
    /// The unit tests above each prove one rule in isolation; this proves they compose. The failure it
    /// is aimed at is real: Recorder A's width bug came from "columns written by four helpers whose
    /// widths depend on flags", i.e. from the INTERACTION of correct pieces, and its own comment says
    /// nothing headless could catch it. That was true of a row built from live vessel state. It is not
    /// true of the pure half, so the pure half is checked here.
    /// </summary>
    static void Pipeline()
    {
        RatePolicy policy = RatePolicy.Adaptive();
        RateState st = RateState.Fresh();
        var cov = new BlackBoxCoverage();
        var accum = BlackBoxAccum.Fresh();
        var lines = new List<string>();

        long seq = 0;
        int r2Rows = 0, r3Rows = 0, warpRows = 0;
        double ut = 10000.0, wall = 0.0;

        // 0-8 s ascent (dynamic), 8-14 s coast (quiescent), 14-20 s on-rails warp at 100x.
        for (int tick = 0; tick < 1000; tick++)
        {
            double phase = tick * 0.02;
            bool dynamic = phase < 8.0;
            bool rails = phase >= 14.0;
            double utStep = rails ? 2.0 : 0.02;     // 100x on rails
            ut += utStep; wall += 0.02;

            accum.Add(utStep, dynamic ? 0.4 : 0.0, 0.0,
                      dynamic ? 1.0 + phase * 0.3 : 0.0,
                      dynamic ? 1000.0 * phase : 0.0,
                      dynamic ? 2.0 : 0.0);

            RateInputs now = In(ut, wall, dynamic, rails);
            RowPlan plan = BlackBoxRate.Plan(policy, st, now);
            if (!plan.Due) continue;

            string[] row = BlackBoxSchema.NewRow();
            seq++;
            BlackBoxSchema.Set(row, BlackBoxCols.MissionId, "PIPE_20260904_000000");
            BlackBoxSchema.Set(row, BlackBoxCols.Seq, (double)seq);
            BlackBoxSchema.Set(row, BlackBoxCols.Ut, ut);
            BlackBoxSchema.Set(row, BlackBoxCols.MetS, phase);
            BlackBoxSchema.Set(row, BlackBoxCols.WallS, wall);
            BlackBoxSchema.Set(row, BlackBoxCols.WarpRate, rails ? 100.0 : 1.0);
            BlackBoxSchema.Set(row, BlackBoxCols.WarpRails, rails);
            BlackBoxSchema.Set(row, BlackBoxCols.Vessel, "Crew-2, pipeline");   // a comma, on purpose
            BlackBoxSchema.Set(row, BlackBoxCols.Focus, "Crew-2, pipeline");
            BlackBoxSchema.Set(row, BlackBoxCols.Throttle, dynamic ? 1.0 : 0.0);
            BlackBoxSchema.Set(row, BlackBoxCols.QPa, dynamic ? 1000.0 * phase : 0.0);
            accum.Put(row);
            accum = BlackBoxAccum.Fresh();
            if (plan.FillR2) { BlackBoxSchema.Set(row, BlackBoxCols.AltM, phase * 900.0); r2Rows++; }
            if (plan.FillR3) { BlackBoxSchema.Set(row, BlackBoxCols.Body, "Earth"); r3Rows++; }
            BlackBoxSchema.Set(row, BlackBoxCols.RecBuildUs, 40.0);
            if (rails) { BlackBoxVoid.Apply(row); warpRows++; }

            cov.Note(row);
            lines.Add(BlackBoxSchema.Row(row));
            st = BlackBoxRate.Advance(st, plan, now);
        }

        Check(lines.Count > 80, "the synthetic mission produced rows (got " + lines.Count + ")");
        Check(r2Rows > 0 && r3Rows > 0, "both decimated tiers fired at least once");
        Check(warpRows > 0, "the on-rails segment produced rows");

        // ⛔ EVERY ROW IS EXACTLY AS WIDE AS THE HEADER. This is the one property Recorder A called
        // "worse than no recording at all" to violate, and it holds across the whole run, including
        // the rows carrying an embedded comma and the rows the warp void has emptied.
        int want = BlackBoxSchema.CountFields(BlackBoxSchema.Header());
        bool allWide = true;
        for (int i = 0; i < lines.Count; i++)
            if (BlackBoxSchema.CountFields(lines[i]) != want) { allWide = false; break; }
        Check(allWide, "every row in the run is exactly header-width, quoting and voiding included");

        // R1 vs R2 cadence actually differed — i.e. the ladder ADAPTED rather than running flat. 8 s of
        // ascent at 10 Hz plus 6 s of coast at 2 Hz is ~92 rows; a flat 2 Hz run would be ~28.
        Check(lines.Count > 60, "the dynamic segment ran faster than the quiescent one (the ladder adapted)");

        // The warp segment covered 6 wall-seconds at the 1 Hz floor, NOT 600 s of UT at 2 Hz.
        Check(warpRows <= 8, "the on-rails segment wrote ~1 row per WALL second, not per UT period "
                             + "(got " + warpRows + ")");

        // Coverage sees the columns this synthetic run actually wrote, and does not see the ones it did
        // not — the mechanism works on a real sequence of rows, not just on one hand-built row.
        Check(cov.Rows == lines.Count, "coverage counted every row");
        Check(cov.WasWritten(BlackBoxCols.AltM), "an R2 column written on some rows counts as written");
        Check(!cov.WasWritten(BlackBoxCols.PvgVgoMps), "an unfitted column stays unwritten");

        ManifestInfo m = ManifestInfo.Fresh();
        m.MissionId = "PIPE_20260904_000000";
        m.Policy = policy;
        m.Closed = true; m.ClosedReason = "test"; m.RowsWritten = lines.Count;
        m.Coverage = cov.Findings();
        string json = BlackBoxManifest.Build(m);
        Check(json.Contains("\"rows_written\": " + lines.Count),
              "the manifest's row count matches the stream — which is how a torn tail is detected");
        Check(json.EndsWith("}" + (char)10), "the manifest is a complete JSON document");
    }

    // ================================================================================================
    // ⭐ REGISTER BB2 — TWO-VESSEL RECORDING
    //
    // §B16.7's booster "lands UNFOCUSED — flown by its own core on the non-active vessel", and that
    // section names the BlackBox's two-vessel recording as the thing that will answer its accepted
    // precision risk. S59 §6.1 Q3 settled the shape: ONE STREAM PER VESSEL, joined by the shared
    // `mission_id` and `ut`. Three properties carry the whole design, and all three are decidable
    // without KSP, so all three are asserted here:
    //   1. both streams carry the SAME mission id and differ only by a vessel-qualified stem (§4.4);
    //   2. a tracked stream writes NO capsule singleton — the Dragon's buses, gates, phase and FDIR
    //      verdict never appear on the booster's rows (§4.8 NEVER FABRICATE / §4.6 blank-not-plausible);
    //   3. and the coverage pass knows the difference, so those blanks are notes, not a wall of ~27
    //      ghost-column defects on every two-vessel flight.
    // ================================================================================================

    // ---------------------------------------------------------------- §4.4 the naming rule
    static void Naming()
    {
        Check(BlackBoxNaming.Sanitize("Crew-2") == "Crew-2", "a clean vessel name is unchanged");
        Check(BlackBoxNaming.Sanitize("F9 S1 / booster") == "F9_S1___booster",
              "spaces and separators become underscores — a path-safe stem");
        Check(BlackBoxNaming.Sanitize("") == "flight",
              "an empty name is 'flight', never an empty stem (a '.params.csv' is a HIDDEN file)");
        Check(BlackBoxNaming.Sanitize(null) == "flight", "...and a null name likewise");

        string mid = BlackBoxNaming.MissionId("Crew 2", "20260904_101500");
        Check(mid == "Crew_2_20260904_101500", "the mission id is <SanitizedVessel>_<stamp> (4.4)");
        Check(mid.StartsWith("Crew_2"), "...so plugin/tools/assess_flight.py's Crew-2* glob still bites");

        // ⭐ THE PROPERTY THE WHOLE TWO-VESSEL DESIGN RESTS ON. §4.4: "A second tracked vessel opens
        // `<MissionId>.<Vessel>.params.csv` — THE SAME MISSION ID. This is the fix for the paired
        // Crew-2_*.csv / Crew-2_Probe_*.csv streams that could only be associated by their timestamps."
        string capsuleStem = BlackBoxNaming.Stem(mid, BlackBoxNaming.StreamSuffix(true, "Crew 2"));
        string boostStem = BlackBoxNaming.Stem(mid, BlackBoxNaming.StreamSuffix(false, "F9 Booster"));
        Check(capsuleStem == mid, "the FIRST stream is unqualified — a one-vessel mission is the BB1 file set");
        Check(boostStem == mid + ".F9_Booster", "the second stream is vessel-qualified");
        Check(boostStem.StartsWith(mid + "."), "...UNDER THE SAME MISSION ID — the 4.4 fix, in one check");
        Check(capsuleStem != boostStem, "and the two stems are distinct, so neither truncates the other");

        // Two vessels CAN share a name (KSP allows it; a booster cloned from one craft file routinely
        // does). Two streams sharing a stem would silently truncate one recording into the other, and
        // the survivor would look complete.
        var taken = new List<string>();
        string a = BlackBoxNaming.UniqueSuffix(mid, ".Booster", taken);
        Check(a == ".Booster", "a free stem is used as-is");
        taken.Add(BlackBoxNaming.Stem(mid, a));
        string b = BlackBoxNaming.UniqueSuffix(mid, ".Booster", taken);
        Check(b == ".Booster_2", "a COLLIDING stem is disambiguated rather than overwritten");
        taken.Add(BlackBoxNaming.Stem(mid, b));
        Check(BlackBoxNaming.UniqueSuffix(mid, ".Booster", taken) == ".Booster_3", "...and again");

        // §4.4: a revert branches the MISSION (so both vessels re-open together), not one stream.
        Check(BlackBoxNaming.NextRevertSuffix(mid) == "_r2", "the first revert branches to _r2");
        string r2 = BlackBoxNaming.BranchMissionId(mid);
        Check(r2 == mid + "_r2", "the branched mission id appends the suffix");
        string r3 = BlackBoxNaming.BranchMissionId(r2);
        Check(r3 == mid + "_r3", "a second revert REPLACES _r2 with _r3 rather than stacking suffixes");
        Check(BlackBoxNaming.BranchMissionId(r3) == mid + "_r4", "...and keeps counting");
    }

    // ---------------------------------------------------------------- BB2's Scope declarations
    static void ScopeDeclarations()
    {
        // The columns whose source is a DragonScreen/conductor SINGLETON. Naming them here, by hand,
        // is the point: this list is the claim, and the schema has to agree with it. If a later task
        // adds a capsule-sourced column and forgets the declaration, this fails rather than shipping a
        // column that quietly lands on the booster's rows.
        string[] capsule =
        {
            "gnc_engaged", "mode_index", "mission_phase", "gate_id", "gate_phase", "crew_action",
            "gate_satisfied_mask", "is_return", "step_ack_mask",
            "bus1_on", "bus2_on", "str_a1", "str_b1", "str_c1", "str_a2", "str_b2", "str_c2",
            "fire_intensity", "suppressant", "leak_rate", "isolating",
            "o2_store", "n2_store", "canister_used",
            "fdir_fault", "fdir_recovery", "aborting", "abort_mode",
            "cabin_psia", "ppo2_psia", "co2_mmhg", "cabin_temp_c", "loop_a_c", "loop_b_c",
            "sev_system", "sev_vehicle", "sev_ls", "sev_thermal", "alarm_mask",
            "prop_frac", "page_l", "page_c", "page_r", "cam_view",
            "align_deg", "roll_err_deg", "pitch_err_deg", "yaw_err_deg",
            "ker_avail", "ker_stage_dv", "ker_total_dv", "ker_twr", "ker_isp", "ker_burn_s",
            "ker_stage_mass_kg", "ker_thrust_avail_n",
        };
        for (int i = 0; i < capsule.Length; i++)
        {
            int idx = BlackBoxSchema.Index(capsule[i]);
            Check(idx >= 0, "capsule column '" + capsule[i] + "' exists in the schema");
            if (idx >= 0)
                Check(BlackBoxSchema.Columns[idx].Scope == Scope.Capsule,
                      "'" + capsule[i] + "' is declared Scope.Capsule");
        }

        // And the other side of the claim: the load-bearing physics columns are the RECORDED VESSEL's,
        // which is why an unfocused booster stream is worth having at all.
        string[] perVessel =
        {
            "alt_m", "srf_speed_mps", "mach", "q_pa", "accel_g", "pitch_deg", "aoa_deg", "thrust_n",
            "eng_ignited", "app_pitch", "mass_kg", "lat_deg", "lon_deg", "downrange_m", "stage",
            "phase_classified", "boost_phase", "boost_steer_pitch", "ls_present", "comm_linked",
            "skin_temp_frac", "ut", "met_s", "vessel", "focus",
        };
        for (int i = 0; i < perVessel.Length; i++)
        {
            int idx = BlackBoxSchema.Index(perVessel[i]);
            Check(idx >= 0, "per-vessel column '" + perVessel[i] + "' exists");
            if (idx >= 0)
                Check(BlackBoxSchema.Columns[idx].Scope == Scope.Vessel,
                      "'" + perVessel[i] + "' is Scope.Vessel — read from the stream's own vessel");
        }

        // ⚠ An Unfitted column's scope is DELIBERATELY left at the default: nothing writes it, so a
        // declaration would be a guess, and the register line that fits it (T17/T18/T19/S55) is the one
        // that will know whose state it turned out to be. See BlackBoxSchema's header.
        for (int i = 0; i < BlackBoxSchema.Columns.Length; i++)
        {
            Col c = BlackBoxSchema.Columns[i];
            if (c.Fit == Fit.Unfitted && c.Scope == Scope.Capsule)
                Check(false, "'" + c.Name + "' is Unfitted AND scoped — that scope is a guess (see the header)");
        }

        Check(BlackBoxSchema.IsCapsule(BlackBoxSchema.Index("bus1_on")), "IsCapsule agrees for a capsule column");
        Check(!BlackBoxSchema.IsCapsule(BlackBoxCols.AltM), "...and for a vessel one");
        Check(!BlackBoxSchema.IsCapsule(-1), "an absent column is not capsule-scoped (and does not throw)");
        Check(BlackBoxSchema.ScopeName(Scope.Capsule) == "capsule"
              && BlackBoxSchema.ScopeName(Scope.Vessel) == "vessel", "the manifest's scope words");

        // BB2 adds no column, removes none, reorders none — so a BB1 recording still chains with a BB2
        // one (§4.2's rule), and only the RECORDER version moves.
        Check(BlackBoxSchema.SchemaVersion == 1, "BB2 did not bump schema_version — it added no column");
        Check(BlackBoxSchema.RecorderVersion == "BB2.0", "the recorder version says which build wrote the file");
    }

    // ---------------------------------------------------------------- BB2's coverage verdict
    static void TrackedCoverage()
    {
        // A TRACKED stream: it wrote every per-vessel column and, correctly, no capsule one.
        var tracked = new BlackBoxCoverage();
        tracked.Note(FilledRow(false));

        List<CoverageFinding> f = tracked.Findings(false);
        int defects = 0;
        for (int i = 0; i < f.Count; i++) if (f[i].Defect) defects++;
        // ⭐ THE HEADLINE. Without this, every two-vessel flight would close with ~27 ghost-column
        // DEFECTS that are not defects — and a defect that always fires is one nobody reads, which is
        // exactly the failure mode BlackBoxCoverage's header rejects for a blanket empty-column warning.
        Check(defects == 0, "a tracked stream that withheld every capsule column reports ZERO defects "
                            + "(got " + defects + ")");

        bool noted = false, notedAsDefect = false;
        for (int i = 0; i < f.Count; i++)
            if (f[i].Column == "bus1_on") { noted = true; notedAsDefect = f[i].Defect; }
        Check(noted, "...but the withheld column is still REPORTED — silence would hide it");
        Check(!notedAsDefect, "...as a note carrying the reason, not as a defect");

        // The SAME rows judged as a focused stream: those blanks now ARE the ghost-column defect,
        // because a capsule stream is the one that must write them.
        int asFocused = 0;
        List<CoverageFinding> ff = tracked.Findings(true);
        for (int i = 0; i < ff.Count; i++) if (ff[i].Defect) asFocused++;
        Check(asFocused > 20, "the same blanks on a FOCUSED stream are defects — the gate is everFocused, "
                              + "not a blanket exemption (got " + asFocused + ")");

        // A non-capsule Live column left blank is STILL a defect on a tracked stream. The exemption is
        // scoped to the capsule singletons and to nothing else.
        var holed = new BlackBoxCoverage();
        string[] r2 = FilledRow(false);
        r2[BlackBoxCols.ThrustN] = "";
        holed.Note(r2);
        bool namedIt = false;
        f = holed.Findings(false);
        for (int i = 0; i < f.Count; i++)
            if (f[i].Column == "thrust_n" && f[i].Defect && f[i].Kind == "never_written") namedIt = true;
        Check(namedIt, "a per-vessel LIVE column blank on a TRACKED stream is still the torque_cmd defect");

        // ⛔ THE LEAK, the other direction. A capsule value on a stream that never held the camera means
        // the Dragon's state is filed under another vessel — silent in the file, because the cell looks
        // like every other cell. This is the check that would catch the `focused` gate in BuildRow being
        // removed by a later edit.
        var leak = new BlackBoxCoverage();
        string[] r3 = FilledRow(false);
        r3[BlackBoxSchema.Index("bus1_on")] = "1";      // the leak
        leak.Note(r3);
        bool flagged = false;
        f = leak.Findings(false);
        for (int i = 0; i < f.Count; i++)
            if (f[i].Column == "bus1_on" && f[i].Kind == "capsule_leak" && f[i].Defect) flagged = true;
        Check(flagged, "a capsule value on a never-focused stream is a DEFECT named capsule_leak");

        // ...and the same value on the CAPSULE's own stream is simply correct.
        bool falsePositive = false;
        f = leak.Findings(true);
        for (int i = 0; i < f.Count; i++) if (f[i].Kind == "capsule_leak") falsePositive = true;
        Check(!falsePositive, "the leak check never fires on a stream that DID hold the camera");
    }

    // ---------------------------------------------------------------- the two-stream mission
    static void TwoVesselPipeline()
    {
        // A synthetic post-separation segment: the capsule holds the camera throughout (§B16.7 — "FOCUS
        // NEVER LEAVES THE UPPER STAGE") and the booster is tracked and unfocused. Both rows are built
        // with the same `focused` gate `BuildRow` applies, so what is asserted below is the shape of a
        // real two-vessel recording rather than of a hand-made pair of files.
        const string mid = "Crew-2_20260904_101500";
        string capStem = BlackBoxNaming.Stem(mid, BlackBoxNaming.StreamSuffix(true, "Crew-2"));
        string bstStem = BlackBoxNaming.Stem(mid, BlackBoxNaming.StreamSuffix(false, "Falcon 9 S1"));

        var capRows = new List<string[]>();
        var bstRows = new List<string[]>();
        var capCov = new BlackBoxCoverage();
        var bstCov = new BlackBoxCoverage();
        var events = new List<string>();
        long capSeq = 0, bstSeq = 0;

        for (int i = 0; i < 200; i++)
        {
            double ut = 300150.0 + i * 0.5;    // ⭐ both streams share ONE clock (§4.5)
            capRows.Add(Row(mid, "Crew-2", "Crew-2", ut, ++capSeq, true, 3, 90000.0 + i * 120.0));
            bstRows.Add(Row(mid, "Falcon 9 S1", "Crew-2", ut, ++bstSeq, false, 1, 60000.0 - i * 250.0));
            capCov.Note(capRows[capRows.Count - 1]);
            bstCov.Note(bstRows[bstRows.Count - 1]);
        }

        // Both craft write into ONE ordered narrative (§4.1 / §4.10 §10), each line naming its vessel.
        events.Add(BlackBoxEvents.Line(mid, "Crew-2", 300150.0, 150.0, 1, BlackBoxEvents.FlightStaged,
                                       new[] { Kv.Int("from", 3), Kv.Int("to", 2) }));
        events.Add(BlackBoxEvents.Line(mid, "Falcon 9 S1", 300220.0, 220.0, 140, BlackBoxEvents.EngineIgnite,
                                       new[] { Kv.Int("to", 3) }));
        events.Add(BlackBoxEvents.Line(mid, "Falcon 9 S1", 300400.0, 400.0, 500, BlackBoxEvents.FlightTouchdown,
                                       new[] { Kv.Num("vspeed_mps", -1.8) }));
        // A MISSION-level event: not either craft's, so `vessel` is null rather than a borrowed name.
        events.Add(BlackBoxEvents.Line(mid, null, 300500.0, double.NaN, 700, BlackBoxEvents.RecWarpChange,
                                       new[] { Kv.Num("from", 1.0), Kv.Num("to", 100.0) }));

        // ---- 1. the §4.4 property: ONE mission, two stems, and the id is on every row of both ----
        Check(capStem == mid && bstStem == mid + ".Falcon_9_S1", "two stems, one mission id");
        bool sameId = true;
        for (int i = 0; i < capRows.Count; i++)
            if (capRows[i][BlackBoxCols.MissionId] != mid || bstRows[i][BlackBoxCols.MissionId] != mid)
                sameId = false;
        Check(sameId, "every row of BOTH streams carries the same mission_id — so a moved file still joins");

        // ---- 2. joinable on ut, which is the shape S59 §6.1 Q3 settled on ----
        int joined = 0;
        for (int i = 0; i < capRows.Count; i++)
        {
            double cu = double.Parse(capRows[i][BlackBoxCols.Ut], CultureInfo.InvariantCulture);
            double bu = double.Parse(bstRows[i][BlackBoxCols.Ut], CultureInfo.InvariantCulture);
            if (Math.Abs(cu - bu) < 1e-9) joined++;
        }
        Check(joined == capRows.Count, "every capsule row joins a booster row on `ut` exactly (got "
                                       + joined + " of " + capRows.Count + ")");

        // ---- 3. the booster row is the BOOSTER's, and the capsule's singletons are not on it ----
        int leaked = 0;
        for (int i = 0; i < bstRows.Count; i++)
            for (int c = 0; c < bstRows[i].Length; c++)
                if (BlackBoxSchema.IsCapsule(c) && !string.IsNullOrEmpty(bstRows[i][c])) leaked++;
        Check(leaked == 0, "NOT ONE capsule singleton reached the booster's rows (4.8 never fabricate) "
                           + "— got " + leaked + " leaked cell(s)");
        Check(!string.IsNullOrEmpty(capRows[0][BlackBoxSchema.Index("bus1_on")]),
              "...while the capsule's own stream carries them, so the gate is a gate and not a deletion");

        // The booster's stream carries ITS state: its own name, its own stage, its own altitude — and
        // the `focus` column names the capsule on every row, which is how a reader knows the booster
        // was flying unfocused without inferring it (§B16.7's accepted risk, made visible).
        Check(bstRows[0][BlackBoxCols.Vessel] == "Falcon 9 S1", "the booster's rows name the booster");
        Check(bstRows[0][BlackBoxCols.Focus] == "Crew-2", "...and record that the CAPSULE held the camera");
        Check(bstRows[0][BlackBoxSchema.Index("stage")] == "1"
              && capRows[0][BlackBoxSchema.Index("stage")] == "3",
              "each stream carries its OWN stage — not the camera holder's StageManager number");
        double a0 = double.Parse(bstRows[0][BlackBoxCols.AltM], CultureInfo.InvariantCulture);
        double a1 = double.Parse(bstRows[bstRows.Count - 1][BlackBoxCols.AltM], CultureInfo.InvariantCulture);
        Check(a1 < a0, "the booster is descending while the capsule climbs — two vehicles, two histories");

        // ---- 4. both streams are exactly header-width, quoting included ----
        int want = BlackBoxSchema.CountFields(BlackBoxSchema.Header());
        bool wide = true;
        for (int i = 0; i < capRows.Count; i++)
        {
            if (BlackBoxSchema.CountFields(BlackBoxSchema.Row(capRows[i])) != want) wide = false;
            if (BlackBoxSchema.CountFields(BlackBoxSchema.Row(bstRows[i])) != want) wide = false;
        }
        Check(wide, "every row of BOTH streams is exactly header-width");

        // ---- 5. the shared event log ----
        bool oneLine = true;
        for (int i = 0; i < events.Count; i++) if (events[i].IndexOf('\n') >= 0) oneLine = false;
        Check(oneLine, "every event is one JSONL line, both vessels' alike");
        Check(events[1].Contains("\"vessel\":\"Falcon 9 S1\""), "a booster event names the booster");
        Check(events[0].Contains("\"vessel\":\"Crew-2\""), "...and a capsule event the capsule, in ONE file");
        Check(events[3].Contains("\"vessel\":null"),
              "a MISSION-level event has a null vessel — it is not either craft's, and borrowing a name "
              + "would file a global fact under one vehicle");
        Check(events[3].Contains("\"met_s\":null"),
              "...and a null MET, because MET restarts per vessel (4.5) so a mission event has none");
        bool allTagged = true;
        for (int i = 0; i < events.Count; i++)
            if (!events[i].Contains("\"mission_id\":\"" + mid + "\"")) allTagged = false;
        Check(allTagged, "every event line carries the mission id, so the log joins to both streams");

        // ---- 6. the two coverage verdicts, each judged by its own role ----
        int capDefects = 0, bstDefects = 0;
        List<CoverageFinding> cf = capCov.Findings(true);
        List<CoverageFinding> bf = bstCov.Findings(false);
        for (int i = 0; i < cf.Count; i++) if (cf[i].Defect) capDefects++;
        for (int i = 0; i < bf.Count; i++) if (bf[i].Defect) bstDefects++;
        Check(bstDefects == 0, "the booster stream closes with no coverage defect (got " + bstDefects + ")");
        Check(capDefects == 0, "and so does the capsule stream (got " + capDefects + ")");
        Check(capCov.Rows == 200 && bstCov.Rows == 200, "both streams counted their own rows");

        // ---- 7. the two manifests: same mission, different role, each pointing at the shared log ----
        string capJson = StreamManifest(mid, capStem, "Crew-2", "focused", true);
        string bstJson = StreamManifest(mid, bstStem, "Falcon 9 S1", "tracked", false);
        Check(capJson.Contains("\"mission_id\": \"" + mid + "\"")
              && bstJson.Contains("\"mission_id\": \"" + mid + "\""),
              "both manifests declare the same mission id");
        Check(bstJson.Contains("\"stream_role\": \"tracked\"") && bstJson.Contains("\"ever_focused\": false"),
              "the booster's manifest says it was tracked and never focused — so its blanks are readable");
        Check(capJson.Contains("\"stream_role\": \"focused\"") && capJson.Contains("\"ever_focused\": true"),
              "the capsule's says the opposite");
        Check(bstJson.Contains("\"params_file\": \"" + bstStem + ".params.csv\""),
              "each manifest names its own params file");
        Check(bstJson.Contains("\"events_file\": \"" + mid + ".events.jsonl\"")
              && capJson.Contains("\"events_file\": \"" + mid + ".events.jsonl\""),
              "...and BOTH name the one shared event log");
        Check(bstJson.Contains("\"stream_join_on\": [\"mission_id\", \"ut\"]"),
              "the join is STATED in the file, not left in a research doc");
        Check(bstJson.Contains("\"launch_lat_deg\": 28.6") && capJson.Contains("\"launch_lat_deg\": 28.6"),
              "both share the MISSION's launch reference, so downrange_m means the same on both");
        Check(bstJson.Contains("\"scope\": \"capsule\"") && bstJson.Contains("\"scope\": \"vessel\""),
              "every column declares its scope, so a reader can tell withheld from broken");
        Check(bstJson.Contains("\"recorder_version\": \"BB2.0\""), "the manifest names the recorder build");
    }

    /// <summary>
    /// A row filled the way `BuildRow` fills it: every fitted per-vessel column, and the capsule
    /// singletons ONLY when this stream's vessel holds the camera. The `focused` argument IS the gate.
    /// </summary>
    static string[] FilledRow(bool focused)
    {
        string[] r = BlackBoxSchema.NewRow();
        for (int i = 0; i < r.Length; i++)
        {
            Col c = BlackBoxSchema.Columns[i];
            if (c.Fit == Fit.Unfitted) continue;
            if (c.Scope == Scope.Capsule && !focused) continue;   // ⭐ exactly as BuildRow has it
            r[i] = "1";
        }
        return r;
    }

    static string[] Row(string mid, string vessel, string focus, double ut, long seq,
                        bool focused, int stage, double altM)
    {
        string[] r = FilledRow(focused);
        BlackBoxSchema.Set(r, BlackBoxCols.MissionId, mid);
        BlackBoxSchema.Set(r, BlackBoxCols.Vessel, vessel);
        BlackBoxSchema.Set(r, BlackBoxCols.Focus, focus);
        BlackBoxSchema.Set(r, BlackBoxCols.Ut, ut);
        BlackBoxSchema.Set(r, BlackBoxCols.Seq, (double)seq);
        BlackBoxSchema.Set(r, BlackBoxCols.AltM, altM);
        BlackBoxSchema.Set(r, BlackBoxSchema.Index("stage"), (double)stage);
        return r;
    }

    static string StreamManifest(string mid, string stem, string vessel, string role, bool everFocused)
    {
        ManifestInfo m = ManifestInfo.Fresh();
        m.MissionId = mid;
        m.Vessel = vessel;
        m.StreamRole = role;
        m.EverFocused = everFocused;
        m.ParamsFile = stem + ".params.csv";
        m.EventsFile = mid + ".events.jsonl";
        m.LaunchLatDeg = 28.6; m.LaunchLonDeg = -80.6; m.HaveLaunchRef = true;
        return BlackBoxManifest.Build(m);
    }
}
