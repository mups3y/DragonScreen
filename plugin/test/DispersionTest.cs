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
            double thr = ControlLaw.ThrottleLimit(baseT, q, qSoft, qLim, floor, gLim, mass, Fthr);
            Check("throttle finite", Finite(thr), "thr=" + thr);
            Check("throttle in [0,1]", thr >= 0.0 && thr <= 1.0, "thr=" + thr.ToString("F5"));
            if (gLim > 0.0 && Fthr > 0.0 && mass > 0.0)
            {
                double achievedG = thr * Fthr / (mass * ControlLaw.G0);
                double gm = gLim - achievedG;
                Check("axial accel <= crew g-limit", gm >= -1e-6,
                      "gLim=" + gLim.ToString("F2") + " achieved=" + achievedG.ToString("F3"));
                if (gm < worstGmargin) worstGmargin = gm;
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
            // above target on both apses -> never raise (far field is prograde-raise-or-coast, never lower)
            double apA = station + rng.Range(0.0, 60000.0), peA = station + rng.Range(0.0, 60000.0);
            Check("at/above target -> coast (never a needless raise)",
                  !Phasing.ShouldRaise(apA, peA, coTgt, 2000.0), "");

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
