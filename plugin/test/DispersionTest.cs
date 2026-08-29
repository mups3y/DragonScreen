// DispersionTest.cs — Tier-2 PROPERTY-BASED DISPERSION  (docs/VALIDATION_AND_ROBUSTNESS.md §Tier 2)
// ============================================================================================
// The reference tests (the other suites) prove the math is correct on hand-picked cases. This one
// proves the LOGIC is ROBUST: it fuzzes THOUSANDS of randomized-but-bounded inputs through the pure
// controllers and asserts the crew-safety INVARIANTS hold for every single one. A property whose
// violation loses the crew or the mission is asserted across the whole envelope, not one point. A
// single failing seed is a found bug — it is printed with its inputs so it becomes a permanent
// regression case. The run is DETERMINISTIC (fixed master seed) so a failure always reproduces.
//
// ⛔ Inside the rules: this is headless C# over the pure layer the user accepts AS the certification;
// it runs NO forward model / propagation (that is the flagged Tier-4, gated separately). It asserts
// each controller's OWN contract over the dispersion — never simulates a trajectory. [[no-python-simulations]]
//
// Coverage today: the two highest-value families — CONTROL actuation safety (never command an
// un-arrestable rate, never a NaN/out-of-range/g-over-limit actuation) and RENDEZVOUS/PHASING (the
// self-deorbit class: no burn is ever emitted beyond CW's valid range — the invariant that would have
// caught flight 214827). More families slot in behind their layers' APIs as those are hardened.
using System;
using DragonScreen;

public static class DispersionTest
{
    // Cases per family. Small enough that the whole suite adds well under a second to every build;
    // bump for an on-demand deep run (a bigger number simply samples the same envelope more densely).
    const int N = 20000;
    const double Pi = Math.PI;

    static int checks = 0, failures = 0;
    // Only the FIRST failure of each invariant is printed in full (with its seed/inputs) so a broad
    // break does not bury the console; the count still reflects every violation.
    static System.Collections.Generic.HashSet<string> shown = new System.Collections.Generic.HashSet<string>();
    static void Check(string inv, bool ok, string detail)
    {
        checks++;
        if (!ok)
        {
            failures++;
            if (shown.Add(inv)) Console.WriteLine("  FAIL  " + inv + "   " + detail);
        }
    }

    static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }

    // ---- deterministic PRNG (xorshift64*) — reproducible failing seeds, platform-independent ----
    sealed class Prng
    {
        ulong s;
        public Prng(ulong seed) { s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }
        public ulong NextU64()
        {
            s ^= s >> 12; s ^= s << 25; s ^= s >> 27;
            return s * 0x2545F4914F6CDD1DUL;
        }
        public double Unit() { return (NextU64() >> 11) * (1.0 / 9007199254740992.0); } // [0,1)
        public double Range(double lo, double hi) { return lo + (hi - lo) * Unit(); }
        public double Sym(double mag) { return Range(-mag, mag); }
        public bool Bool() { return (NextU64() & 1UL) != 0UL; }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen Tier-2 dispersion (property-based robustness)");
        ControlSafety();
        RendezvousSafety();
        DockingSafety();
        ReturnSafety();
        FdirSafety();
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // ============================================================================================
    // FAMILY 1 — CONTROL actuation safety (pure/ControlLaw + pure/Authority)
    // Invariants (VALIDATION_AND_ROBUSTNESS §Control): actuation always in [-1,1] and finite; zero
    // authority -> zero command (no divide-by-zero kick); the commanded rate is ALWAYS arrestable
    // (never a rate the vehicle cannot brake); the slew limit is respected; the throttle stays in
    // [0,1] and never lets axial accel exceed the crew g-limit; RCS translation stays in [-1,1].
    // ============================================================================================
    static void ControlSafety()
    {
        Prng rng = new Prng(0xC0FFEEUL);
        double minArrestMargin = double.PositiveInfinity;  // min (arrestable - commanded) over the run
        double worstGmargin = double.PositiveInfinity;      // min (gLimit - achieved g) when the g-limit binds

        for (int i = 0; i < N; i++)
        {
            double error = rng.Sym(Pi);                 // pointing error, rad (full range)
            double rate = rng.Sym(2.0);                 // measured body rate, rad/s
            // half the time a degenerate zero-authority axis, else a broad inertia/torque envelope.
            bool zeroAuth = (i % 7) == 0;
            double inertia = zeroAuth && rng.Bool() ? 0.0 : Math.Pow(10.0, rng.Range(2.0, 7.0)); // 1e2..1e7 kg·m²
            double torque  = zeroAuth && !rng.Bool() ? 0.0 : Math.Pow(10.0, rng.Range(1.0, 6.0)); // 1e1..1e6 N·m
            double maxRate = rng.Bool() ? -1.0 : rng.Range(0.01, 1.0);   // <=0 = no extra cap
            double prev    = rng.Sym(1.0);
            string seed = "i=" + i + " err=" + error.ToString("F4") + " rate=" + rate.ToString("F4")
                        + " I=" + inertia.ToString("E2") + " T=" + torque.ToString("E2")
                        + " maxR=" + maxRate.ToString("F3") + " prev=" + prev.ToString("F3");

            // --- AxisCommand: bounded, finite, slew-limited, zero-authority-safe ---
            double a = ControlLaw.AxisCommand(error, rate, inertia, torque, maxRate, prev);
            Check("axis actuation finite", Finite(a), seed + " a=" + a);
            Check("axis actuation in [-1,1]", a >= -1.0 - 1e-12 && a <= 1.0 + 1e-12, seed + " a=" + a.ToString("F5"));
            if (torque <= 0.0 || inertia <= 0.0)
                Check("zero authority -> zero command", a == 0.0, seed + " a=" + a);

            // --- Actuate slew limit against the previous command ---
            double rateErr = rng.Sym(3.0);
            double act = ControlLaw.Actuate(rateErr, inertia, torque, prev);
            Check("actuate finite", Finite(act), seed + " act=" + act);
            Check("actuate in [-1,1]", act >= -1.0 - 1e-12 && act <= 1.0 + 1e-12, seed + " act=" + act.ToString("F5"));
            if (torque > 0.0 && inertia > 0.0)
                Check("slew step <= MaxSlewPerTick",
                      Math.Abs(act - prev) <= ControlLaw.MaxSlewPerTick + 1e-9,
                      seed + " d=" + Math.Abs(act - prev).ToString("F5"));

            // --- THE ONE THAT MATTERS: commanded rate is arrestable ---
            double angAccel = Authority.AngularAccel(torque, inertia);
            double wCmd = ControlLaw.RateCommand(error, angAccel, -1.0);   // no extra cap: test the raw law
            Check("rate command finite", Finite(wCmd), seed + " w=" + wCmd);
            if (angAccel > 0.0)
            {
                double arrest = Authority.ArrestableRate(angAccel, Math.Abs(error));
                double margin = arrest - Math.Abs(wCmd);
                Check("commanded rate is arrestable", margin >= -1e-9,
                      seed + " |w|=" + Math.Abs(wCmd).ToString("F5") + " arrest=" + arrest.ToString("F5"));
                if (Math.Abs(error) > ControlLaw.DeadbandRad && margin < minArrestMargin) minArrestMargin = margin;
            }

            // --- ThrottleLimit: bounded, finite, and crew never exceeds the g-limit ---
            double baseT = rng.Range(-0.5, 1.5);
            double q = rng.Range(0.0, 80000.0), qSoft = rng.Range(0.0, 40000.0), qLim = rng.Range(20000.0, 90000.0);
            double floor = rng.Range(0.0, 1.0), gLim = rng.Range(0.0, 6.0);
            double mass = Math.Pow(10.0, rng.Range(3.0, 5.8)), Fthr = Math.Pow(10.0, rng.Range(4.0, 6.85));
            double minThr = rng.Range(0.0, 0.5);   // RealFuels floor; 0 = stock/linear
            double thr = ControlLaw.ThrottleLimit(baseT, q, qSoft, qLim, floor, gLim, mass, Fthr, minThr);
            Check("throttle finite", Finite(thr), "thr=" + thr);
            Check("throttle in [0,1]", thr >= 0.0 && thr <= 1.0, "thr=" + thr.ToString("F5"));
            if (gLim > 0.0 && Fthr > 0.0 && mass > 0.0)
            {
                // Felt g is through the RealFuels remap engineThrottle = minThr + thr·(1−minThr).
                double engThr = minThr + thr * (1.0 - minThr);
                double achievedG = engThr * Fthr / (mass * ControlLaw.G0);
                // The felt g must not exceed the crew limit UNLESS the engine's own floor forces it — a lit
                // engine cannot throttle below minThr, so floorG is the least g it can deliver at this mass.
                double floorG = minThr * Fthr / (mass * ControlLaw.G0);
                double allow = Math.Max(gLim, floorG);
                double gm = allow - achievedG;
                Check("axial accel <= crew g-limit (or the engine's minThrottle floor)", gm >= -1e-6,
                      "gLim=" + gLim.ToString("F2") + " achieved=" + achievedG.ToString("F3") +
                      " floorG=" + floorG.ToString("F3"));
                if (allow <= gLim + 1e-9 && gm < worstGmargin) worstGmargin = gm;
            }

            // --- RCS translation axis: bounded, finite, zero-avail-safe ---
            double desired = rng.Sym(100.0), avail = (i % 5 == 0) ? 0.0 : rng.Range(0.1, 50.0);
            double tr = ControlLaw.TranslateAxis(desired, avail);
            Check("translate finite", Finite(tr), "tr=" + tr);
            Check("translate in [-1,1]", tr >= -1.0 - 1e-12 && tr <= 1.0 + 1e-12, "tr=" + tr.ToString("F5"));
            if (avail <= 1e-9) Check("zero RCS avail -> zero translate", tr == 0.0, "tr=" + tr);
        }

        Console.WriteLine("    control: " + N + " cases | min arrestable-rate margin = "
            + (double.IsInfinity(minArrestMargin) ? "n/a" : minArrestMargin.ToString("F4") + " rad/s")
            + " | min g-limit margin = "
            + (double.IsInfinity(worstGmargin) ? "n/a" : worstGmargin.ToString("F4") + " g"));
    }

    // ============================================================================================
    // FAMILY 2 — RENDEZVOUS / PHASING safety (pure/Rendezvous + pure/Phasing) — the self-deorbit class
    // ⛔ THE 214827 INVARIANT: for ANY relative geometry beyond CW's valid range, Guide emits NO burn
    // (so nothing can command the 28 km/s retrograde garbage that decayed pe +178 -> -143 km) and still
    // holds a definite unit aim (never floating). Plus: PeSafe is a strict floor; the co-elliptic target
    // is always below the station; near-field CW burns stay finite and bounded (never explode).
    // ============================================================================================
    static void RendezvousSafety()
    {
        Prng rng = new Prng(0x5A5A5A5AUL);
        double maxFarRange = 0.0;      // farthest far-field geometry proven burn-free (coverage)
        double maxNearDv = 0.0;        // largest near-field Δv seen (margin vs the explosion threshold)
        double minPeMargin = double.PositiveInfinity;

        RvPhase[] phases = { RvPhase.Phasing, RvPhase.CoElliptic, RvPhase.ApproachInit, RvPhase.Midcourse };

        for (int i = 0; i < N; i++)
        {
            // ---- Phasing pure contracts ----
            double pe = rng.Range(-500000.0, 2000000.0);   // includes the flight's decayed -143 km
            double floor = rng.Range(100000.0, 200000.0);
            Check("PeSafe is a strict floor", Phasing.PeSafe(pe, floor) == (pe > floor),
                  "pe=" + pe.ToString("F0") + " floor=" + floor.ToString("F0"));
            if (pe > floor) { double m = pe - floor; if (m < minPeMargin) minPeMargin = m; }

            double station = rng.Range(300000.0, 450000.0), below = rng.Range(1000.0, 50000.0);
            double coTgt = Phasing.CoEllipticTargetAltM(station, below);
            Check("co-elliptic target is below the station", coTgt < station && Finite(coTgt),
                  "station=" + station.ToString("F0") + " tgt=" + coTgt.ToString("F0"));
            // FarGuide invariants across dispersed geometry + every FSM state: never OVER-RAISE the apoapsis (the
            // 103303 fix) and never burn below the pe floor (crew safety). The target is the co-elliptic altitude
            // (coTgt, just below the station); once ap reaches it the far field STOPS and coasts (CW takes over).
            double chApAlt = coTgt + rng.Range(-250000.0, 60000.0);   // chaser ap around/below the target altitude
            FarInputs fin = new FarInputs
            {
                PhaseNowRad = rng.Range(0.0, 2.0 * Math.PI), PhaseLeadRad = rng.Range(0.0, 2.0 * Math.PI),
                Omega1 = 0.0012, Omega2 = 0.0011,
                ApAltM = chApAlt, TargetAltM = coTgt, RaiseTolM = 2000.0,
                PeAltM = pe, FloorM = floor
            };
            FarPhase[] fps = { FarPhase.Phase, FarPhase.Transfer, FarPhase.Coast };
            FarPhase st = fps[i % 3];
            FarCommand fcmd = Phasing.FarGuide(fin, st);
            if (st == FarPhase.Transfer && chApAlt >= coTgt - 2000.0)
                Check("FarGuide: ap at/above target in TRANSFER -> NO burn (never over-raise)", !fcmd.Burn,
                      "ap=" + chApAlt.ToString("F0"));
            if (pe <= floor)
                Check("FarGuide: pe at/below floor -> burn HELD (crew safety)", !fcmd.Burn,
                      "pe=" + pe.ToString("F0") + " floor=" + floor.ToString("F0"));
            Check("FarGuide: WaitS finite", Finite(fcmd.WaitS), "wait=" + fcmd.WaitS);

            // ---- Guide FAR FIELD: the self-deorbit invariant ----
            RendezvousInputs s = MakeInputs(rng, true);
            double r = s.Rel.RangeM;
            if (r > maxFarRange) maxFarRange = r;
            RvPhase ph = phases[i & 3];
            RendezvousCommand fc = Rendezvous.Guide(s, ph);
            Check("far field: NO burn beyond CW range", !fc.Burn && fc.BurnDvMps < 1e-9,
                  "range=" + (r / 1000.0).ToString("F0") + "km Burn=" + fc.Burn + " dv=" + fc.BurnDvMps.ToString("F3"));
            Check("far field: aim is a finite unit vector",
                  Finite(fc.AimLvlh.Magnitude) && Math.Abs(fc.AimLvlh.Magnitude - 1.0) < 1e-6,
                  "|aim|=" + fc.AimLvlh.Magnitude.ToString("F6"));

            // ---- Guide NEAR FIELD: CW runs, but must stay finite / non-exploding ----
            RendezvousInputs sn = MakeInputs(rng, false);
            RendezvousCommand nc = Rendezvous.Guide(sn, phases[(i + 1) & 3]);
            Check("near field: aim is a finite unit vector",
                  Finite(nc.AimLvlh.Magnitude) && Math.Abs(nc.AimLvlh.Magnitude - 1.0) < 1e-6,
                  "|aim|=" + nc.AimLvlh.Magnitude.ToString("F6"));
            Check("near field: dv is finite", Finite(nc.BurnDvMps), "dv=" + nc.BurnDvMps);
            // an exploding CW inverse (the 28 km/s pathology) is the failure; a legitimately large but
            // finite terminal Δv is not — flag only the explosion band.
            Check("near field: dv does not explode (<5 km/s)", nc.BurnDvMps < 5000.0,
                  "range=" + (sn.Rel.RangeM / 1000.0).ToString("F1") + "km dv=" + nc.BurnDvMps.ToString("F1"));
            if (nc.Burn && nc.BurnDvMps > maxNearDv) maxNearDv = nc.BurnDvMps;
        }

        Console.WriteLine("    rendezvous: " + N + " cases | far-field burn-free up to "
            + (maxFarRange / 1000.0).ToString("F0") + " km | max near-field dv = "
            + maxNearDv.ToString("F1") + " m/s | min pe margin = "
            + (double.IsInfinity(minPeMargin) ? "n/a" : (minPeMargin / 1000.0).ToString("F0") + " km"));
    }

    // ============================================================================================
    // FAMILY 3 — DOCKING safety (pure/DockControl) — the terminal approach must always be brakeable.
    // Invariants: the closing-speed cap is finite, non-negative, never above the far cap, and TAPERS with
    // range (closer ⇒ slower, so a leg can always brake to the ~8 cm/s contact); the per-axis servo accel is
    // finite for any dispersion and gives NO kick when already at rest on the target.
    // ============================================================================================
    static void DockingSafety()
    {
        Prng rng = new Prng(0xD0C0DEUL);
        for (int i = 0; i < N; i++)
        {
            double contact = rng.Range(0.02, 0.3);       // ~8 cm/s soft-contact speed
            double far = rng.Range(0.5, 5.0);            // far-field approach speed
            double taper = rng.Range(20.0, 400.0);
            double r1 = rng.Range(0.0, 500.0);
            double r2 = r1 + rng.Range(0.0, 500.0);       // r2 >= r1
            double cap1 = DockControl.SpeedCap(r1, contact, far, taper);
            double cap2 = DockControl.SpeedCap(r2, contact, far, taper);
            Check("dock: SpeedCap finite", Finite(cap1) && Finite(cap2), "cap1=" + cap1);
            Check("dock: SpeedCap >= 0", cap1 >= -1e-12, "cap1=" + cap1.ToString("F5"));
            Check("dock: SpeedCap never exceeds the far cap", cap1 <= Math.Max(contact, far) + 1e-9, "cap1=" + cap1.ToString("F5"));
            Check("dock: SpeedCap tapers with range (closer = slower)", cap2 >= cap1 - 1e-9,
                  "r1=" + r1.ToString("F1") + " cap1=" + cap1.ToString("F4") + " r2=" + r2.ToString("F1") + " cap2=" + cap2.ToString("F4"));

            double err = rng.Sym(200.0), rate = rng.Sym(10.0), vmax = rng.Range(0.01, 5.0);
            double kPos = rng.Range(0.01, 2.0), kVel = rng.Range(0.1, 5.0);
            double acc = DockControl.Accel(err, rate, vmax, kPos, kVel);
            Check("dock: servo accel finite", Finite(acc), "acc=" + acc);
            Check("dock: no kick at rest on target", Math.Abs(DockControl.Accel(0.0, 0.0, vmax, kPos, kVel)) < 1e-6, "");
        }
        Console.WriteLine("    docking: " + N + " cases | SpeedCap bounded + tapering, servo finite + no-kick-at-rest");
    }

    // ============================================================================================
    // FAMILY 4 — RETURN / ENTRY safety (pure/Entry + pure/Chutes) — the crew's way home must never float.
    // Invariants: Entry.Guide ALWAYS returns a finite UNIT heat-shield aim (full-control contract), a finite
    // bank |σ| ≤ π, and a CoM offset in [0,1]; the chute phase NEVER regresses and only reports splashdown at
    // /below the sea; ⛔ the ABORT chute sequence NEVER cuts the drogues (the ~122 m/s fix — keep the backstop).
    // ============================================================================================
    static void ReturnSafety()
    {
        Prng rng = new Prng(0xE471EUL);
        EntryPhase[] eph = { EntryPhase.Idle, EntryPhase.PreEntry, EntryPhase.Entry, EntryPhase.Descent };
        ChutePhase[] cph = { ChutePhase.Idle, ChutePhase.Drogue, ChutePhase.Main, ChutePhase.Splashed };
        for (int i = 0; i < N; i++)
        {
            EntryInputs e = new EntryInputs();
            e.Valid = true;
            e.Velocity = new Vec3(rng.Sym(8000), rng.Sym(8000), rng.Sym(8000));
            e.Up = new Vec3(rng.Sym(1), rng.Sym(1), rng.Sym(1));
            e.AltitudeM = rng.Range(0, 150000);
            e.EntryInterfaceAltM = 120000; e.DrogueAltM = 5486;
            e.DownrangeErrM = rng.Sym(500000); e.CrossrangeErrM = rng.Sym(500000);
            e.SpeedMps = rng.Range(0, 8000); e.TargetLoverD = rng.Range(0, 0.4);
            EntryCommand ec = Entry.Guide(e, eph[i & 3]);
            Check("entry: AimForward is a finite unit vector",
                  Finite(ec.AimForward.Magnitude) && Math.Abs(ec.AimForward.Magnitude - 1.0) < 1e-6,
                  "|aim|=" + ec.AimForward.Magnitude.ToString("F6"));
            Check("entry: bank finite and |σ| <= π", Finite(ec.BankRad) && Math.Abs(ec.BankRad) <= Pi + 1e-9, "σ=" + ec.BankRad.ToString("F4"));
            Check("entry: CoM offset in [0,1]", ec.OffsetPercent >= -1e-12 && ec.OffsetPercent <= 1.0 + 1e-12, "off=" + ec.OffsetPercent.ToString("F4"));

            ChuteInputs ci = new ChuteInputs();
            ci.Valid = true; ci.AltitudeM = rng.Range(-100, 8000); ci.DescentRateMps = rng.Sym(200);
            ci.DrogueAltM = 5486; ci.MainAltM = 1830; ci.SeaAltM = 0;
            ChutePhase inPh = cph[i & 3];
            ChuteCommand cc = Chutes.Sequence(ci, inPh);
            Check("chute: phase never regresses", (byte)cc.Phase >= (byte)inPh, "in=" + inPh + " out=" + cc.Phase);
            // the TRANSITION to splashed happens only at/below the sea (an already-latched Splashed persists).
            if (cc.Splashed && inPh != ChutePhase.Splashed)
                Check("chute: splashdown only at/below the sea", ci.AltitudeM <= ci.SeaAltM + 1e-6, "alt=" + ci.AltitudeM.ToString("F1"));

            ChuteCommand ac = Chutes.SequenceAbort(ci, inPh, rng.Range(0.0, 10.0));
            Check("abort chute: drogues are NEVER cut (backstop)", !ac.CutDrogues, "");
            Check("abort chute: phase never regresses", (byte)ac.Phase >= (byte)inPh, "");
        }
        Console.WriteLine("    return: " + N + " cases | entry aim unit + bank bounded, chute phase monotone, abort keeps drogues");
    }

    // ============================================================================================
    // FAMILY 5 — FDIR safety (pure/Fdir) — the safety spine must never lie about an abort or retry forever.
    // Invariants: the abort flag matches the rung; a healthy report is Continue (never a phantom abort); a
    // crew GO-gate hold suppresses the stall fault; the escalation rung is NEVER below the phase-correct base;
    // and a PERSISTENT fault escalates MONOTONICALLY to Abort/SafeMode (the guaranteed-floor property).
    // ============================================================================================
    static void FdirSafety()
    {
        Prng rng = new Prng(0xFD1204UL);
        MissionPhase[] mph = { MissionPhase.Prelaunch, MissionPhase.Ascent, MissionPhase.Coast,
                               MissionPhase.Phasing, MissionPhase.Approach, MissionPhase.Docked, MissionPhase.Entry };
        FaultKind[] fk = { FaultKind.None, FaultKind.KeepOutBreach, FaultKind.ThrustShortfall, FaultKind.NoControlSolution,
                           FaultKind.ResourceCritical, FaultKind.TrajectoryDivergence, FaultKind.ConvergenceStall };
        for (int i = 0; i < N; i++)
        {
            FdirState st = new FdirState();
            FdirInputs s = new FdirInputs();
            s.Valid = true; s.Dt = rng.Range(0.01, 3.0); s.Phase = mph[i % mph.Length];
            s.GateHolding = rng.Bool(); s.Powered = rng.Bool();
            s.ThrustDeliveredFrac = rng.Range(0.0, 1.2); s.TrajErrorM = rng.Range(0, 20000);
            s.PlanProgressRate = rng.Sym(10.0); s.ResourceMargin01 = rng.Range(0.0, 1.0);
            s.ControlSolutionOk = rng.Bool();
            s.KosRadiusM = rng.Bool() ? 0.0 : rng.Range(50, 500); s.KosRangeM = rng.Range(0, 1000); s.CorridorOk = rng.Bool();
            FdirReport r = Fdir.Update(ref st, s);
            Check("fdir: abort flag matches the rung",
                  r.Abort == (r.Response == Recovery.Abort || r.Response == Recovery.SafeMode), "resp=" + r.Response);
            Check("fdir: healthy report is Continue (no phantom abort)",
                  r.Fault != FaultKind.None || (r.Response == Recovery.Continue && !r.Abort), "");
            Check("fdir: a crew hold suppresses ConvergenceStall",
                  !(s.GateHolding && r.Fault == FaultKind.ConvergenceStall), "");

            FaultKind f = fk[i % fk.Length];
            MissionPhase ph = mph[(i / fk.Length) % mph.Length];
            double margin = rng.Range(0.0, 1.0);
            FdirState est = new FdirState();
            Recovery rr = Fdir.Escalate(ref est, f, ph, margin, s.Dt);
            Recovery baseR = Fdir.Recover(f, ph, margin);
            Check("fdir: escalate never below the base rung", (byte)rr >= (byte)baseR, "f=" + f + " base=" + baseR + " esc=" + rr);
            if (f == FaultKind.None) Check("fdir: no fault -> Continue", rr == Recovery.Continue, "");
        }

        // the guarantee: a persistent fault ALWAYS climbs (monotonically) to Abort/SafeMode
        {
            FdirState est = new FdirState();
            Recovery last = Recovery.Continue; bool reachedAbort = false, monotone = true;
            for (int k = 0; k < 100; k++)
            {
                Recovery rr = Fdir.Escalate(ref est, FaultKind.TrajectoryDivergence, MissionPhase.Phasing, 1.0, Fdir.RungGraceS + 0.01);
                if ((byte)rr < (byte)last) monotone = false;
                last = rr;
                if (rr == Recovery.Abort || rr == Recovery.SafeMode) { reachedAbort = true; break; }
            }
            Check("fdir: a persistent fault escalates monotonically to Abort", reachedAbort && monotone, "last=" + last);
        }
        Console.WriteLine("    fdir: " + N + " cases | abort-flag consistent, escalate >= base, persistent fault -> Abort");
    }

    // Sample a dispersed rendezvous input. far=true -> range strictly beyond CwMaxRangeM (up to 13,000 km,
    // the flight's geometry); far=false -> a valid near-field range (corridor .. just inside CwMaxRangeM).
    static RendezvousInputs MakeInputs(Prng rng, bool far)
    {
        RendezvousInputs s = new RendezvousInputs();
        s.Valid = true;
        s.N = rng.Range(1.0e-3, 1.3e-3);              // LEO mean motion band
        s.AllNominal = rng.Bool();
        s.AttitudeReady = rng.Bool();
        s.CoEllipticBelowM = 10000.0; s.CoEllipticBehindM = 20000.0;
        s.AiRangeM = 7500.0; s.CorridorRangeM = 2000.0;

        double range = far ? rng.Range(Rendezvous.CwMaxRangeM * 1.01, 13000000.0)
                           : rng.Range(2000.0, Rendezvous.CwMaxRangeM * 0.99);
        // random direction (reject a degenerate near-zero vector, then scale to the chosen range)
        double dx, dy, dz, len;
        do { dx = rng.Sym(1.0); dy = rng.Sym(1.0); dz = rng.Sym(1.0); len = Math.Sqrt(dx * dx + dy * dy + dz * dz); }
        while (len < 1e-3);
        double k = range / len;
        LvlhState rel = new LvlhState();
        rel.Rx = dx * k; rel.Ry = dy * k; rel.Rz = dz * k;
        rel.Vx = rng.Sym(50.0); rel.Vy = rng.Sym(50.0); rel.Vz = rng.Sym(50.0);
        s.Rel = rel;
        return s;
    }
}
