// Tests for §B16 booster recovery: the hoverslam ignition solver (pure/Hoverslam.cs), the grid-fin
// steering law (pure/GridFin.cs), and the FIVE-PHASE return-flight FSM (pure/BoosterDescent.cs).
//
// ⚠ W8, 2026-09-04 — WHAT CHANGED AND WHY. W3 restored this suite verbatim and recorded, in its own
// header, that it "exercises nothing of the boostback", so a green run must not be read as a complete
// FSM. W8 extended the FSM to §B16.2's five phases and this suite with it. It now covers:
//   · the FSM's THREE headline contracts in EVERY ONE of the eight phases, for BOTH target modes, and
//     for garbage inputs — a definite UNIT `AimForward`, `|AoaDeg| <= AoaCapDeg`, and an aim that sits
//     EXACTLY `|AoaDeg|` off retrograde wherever an AoA is commanded;
//   · BOTH profiles ENTERING boostback (§B16.2's C1.8 OVERRIDE: one always-entered state, never an
//     RTLS-only optional one), and ASDS's DEFAULT MAGNITUDE OF ZERO reproducing the old "no boostback"
//     behaviour exactly;
//   · the ballistic COAST between boostback and the entry burn, in both directions out of it;
//   · the §4.1 flip shaper, §4.2 boostback throttle law, §4.3 payload correction, §4.4 authority taper,
//     §4.5 landing throttle + terminal AoA schedule, §5's signed long/short test and two-tier
//     prediction, §6's ullage gate and spool ramp, and §B16.3's ignition-budget refusal;
//   · OCT6 (2026-09-05): the landing burn's 3→1 SHED. The owner ruled *"1. (2)"* — `ThreeLanding`
//     shedding to `CenterOnly` — with the shed point *"comput[ed] from current hover slam solver"*, so
//     W8's "NON-PORT: never commands the 3-engine set" claim above it is SUPERSEDED. What is asserted
//     now is the ORDERED pair and the ONE-WAY latch: three engines brake, one flies the touchdown, and
//     the burn never returns to three.
//
// ⛔ WHAT A GREEN RUN OF THIS SUITE DOES AND DOES NOT PROVE. R1 §7.4 names this file, with
// `pure/Hoverslam.cs`, as a "constants with NO STATED REGIME" DEFECT, and the fixture below is where that
// defect actually lives — `Flight()` and `Booster()` carry the only anchors in the wave:
//     v_term/descent 244 m/s · 31 t · 2,227 kN 3-engine (→ 71.8 m/s²) · g 9.8 · DeadTimeS 6.0 · SpoolS 0.0
//     grid fins: AoaMaxDeg 20 · GainDegPerKm 4 · LeadTauS 3
// NONE of those is attributed to a regime by anything in this repo, and they DISAGREE with the only other
// written anchor set — gen-1 `HoverslamTest.cs`, whose header cites *"the real 0824 landing (v_term 244,
// 31 t, 1925 kN, spool 3.5 s)"* while its own fixture sets `SpoolS = 1.2` / `DeadTimeS = 5.4`. Three
// anchor sets, one named landing, and **whether that landing was ours (RSS-RO) or F9I's (stock) is
// recorded nowhere** (R1 §7.4). The 48-flight corpus that could settle it was gitignored and is GONE
// (§B16.8, R1 §3.5).
// ⇒ These checks are PROPERTY checks — monotonicity, sign, unit length, cap, FSM transition, refusal.
// They prove the solver's ARITHMETIC, the laws' SHAPE and the FSM's CONTRACTS. They prove NOTHING about
// tuning: every constant the FSM carries is `[UN-CONVERGED]` bar the craft dump's own `_minThrottle`, and
// the booster was NEVER RECOVERED in flight (R1 §4.2). Do not read green here as a validated profile.
using System;
using DragonScreen;

public static class BoosterTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    static readonly BoosterPhase[] AllPhases = {
        BoosterPhase.Idle, BoosterPhase.Flip, BoosterPhase.Boostback, BoosterPhase.Coast,
        BoosterPhase.EntryBurn, BoosterPhase.AeroDescent, BoosterPhase.LandingBurn, BoosterPhase.Landed };

    static HoverslamInputs Flight()
    {
        return new HoverslamInputs {
            AltitudeM = 3000.0, DescentSpeedMps = 244.0,
            ThrustAccelMps2 = 2227000.0 / 31000.0,   // 3-engine / 31 t ≈ 71.8 m/s²
            GravityMps2 = 9.8, TerminalSpeedMps = 244.0, DeadTimeS = 6.0, SpoolS = 0.0 };
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen booster recovery tests (§B16: hoverslam + grid fins + the 5-phase FSM)");

        HoverslamChecks();
        EnginesForChecks();
        GridFinChecks();
        ContractChecks();
        FlipChecks();
        BoostbackChecks();
        CoastChecks();
        EntryBurnChecks();
        AeroDescentChecks();
        LandingBurnChecks();
        PredictionChecks();
        UllageAndSpoolChecks();
        PhaseCommandGateInvariantChecks();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // =====================================================================================
    // HOVERSLAM ignition solver — unchanged from W3's suite.
    // =====================================================================================
    static void HoverslamChecks()
    {
        HoverslamInputs s = Flight();
        double ign = Hoverslam.IgnitionAltitude(s);
        Check("ignition altitude is a sane band (1-3 km)", ign > 1000.0 && ign < 3000.0, ign.ToString("F0") + " m");

        HoverslamInputs noDead = s; noDead.DeadTimeS = 0.0;
        Check("the dead-time free-fall RAISES the ignition altitude (>1 km higher)",
              ign > Hoverslam.IgnitionAltitude(noDead) + 1000.0,
              ign.ToString("F0") + " vs " + Hoverslam.IgnitionAltitude(noDead).ToString("F0"));

        HoverslamInputs noDrag = s; noDrag.TerminalSpeedMps = 1e9;   // effectively drag-free
        Check("drag LOWERS the ignition altitude (drag brakes for free)",
              ign < Hoverslam.IgnitionAltitude(noDrag) - 10.0,
              ign.ToString("F0") + " vs " + Hoverslam.IgnitionAltitude(noDrag).ToString("F0"));

        HoverslamInputs spool = s; spool.SpoolS = 3.0;
        Check("a slow spool RAISES the ignition altitude", Hoverslam.IgnitionAltitude(spool) > ign, "");

        HoverslamInputs faster = s; faster.DescentSpeedMps = 300.0; faster.TerminalSpeedMps = 300.0;
        Check("a faster descent ignites HIGHER (monotonic in speed)", Hoverslam.IgnitionAltitude(faster) > ign, "");

        HoverslamInputs weak = s; weak.ThrustAccelMps2 = 5.0;   // TWR < 1: cannot decelerate
        Check("a stage that cannot arrest lights immediately (returns current altitude)",
              Math.Abs(Hoverslam.IgnitionAltitude(weak) - weak.AltitudeM) < 1.0, "");
    }

    // =====================================================================================
    // OCT6 (2026-09-05) — `Hoverslam.EnginesFor`, WHICH HAD NO CALLER AND NO TEST.
    // =====================================================================================
    // It has carried the landing burn's engine-count decision since it was written — *"Fewest engines
    // that can still arrest from here"* — and nothing in `plugin/` ever called it and nothing tested it.
    // The owner's ruling (*"1. (2)"* — ThreeLanding shedding to CenterOnly; the shed point
    // *"comput[ed] from current hover slam solver"*) makes it the live decision, so it gets its first
    // test here: BOTH branches, the 0 case, and the monotonicity the one-way latch depends on.
    //
    // The two banks below are a PROPERTY fixture, not a converged profile: a centre bank at TWR ~2.4 and
    // a three-engine bank at exactly 3x its acceleration, which is the ONE relationship between the banks
    // that is structural (nested subsets of the same nozzles, one throttle — OCT3's mechanism note)
    // rather than tuned. Nothing here is evidence about the real vehicle (see the file header).
    const double EngG = 9.8, EngCentreAccel = 24.0, EngThreeAccel = 72.0, EngVterm = 244.0;

    static HoverslamInputs Bank(double accel, double altM, double vMps)
    {
        return new HoverslamInputs {
            AltitudeM = altM, DescentSpeedMps = vMps, ThrustAccelMps2 = accel,
            GravityMps2 = EngG, TerminalSpeedMps = EngVterm, DeadTimeS = 0.0, SpoolS = 0.0 };
    }

    static void EnginesForChecks()
    {
        // ---- BRANCH "3": the single engine cannot ignite below where we already are, three can brake ----
        // Sit the stage EXACTLY at the single engine's own ignition altitude — the moment the aero descent
        // hands over. One engine is then out of margin by construction; three are not.
        double vHigh = 244.0;
        double ignOne = Hoverslam.IgnitionAltitude(Bank(EngCentreAccel, 0.0, vHigh));
        HoverslamInputs oneAtHandover = Bank(EngCentreAccel, ignOne, vHigh);
        HoverslamInputs threeAtHandover = Bank(EngThreeAccel, ignOne, vHigh);
        Check("OCT6: at the hand-over altitude the solver calls for THREE engines",
              Hoverslam.EnginesFor(oneAtHandover, threeAtHandover) == 3,
              "ignOne=" + ignOne.ToString("F0") + " alt=" + ignOne.ToString("F0"));
        Check("...and that 3 is not vacuous — three engines really do stop shorter than one",
              Hoverslam.IgnitionAltitude(threeAtHandover) < Hoverslam.IgnitionAltitude(oneAtHandover),
              Hoverslam.IgnitionAltitude(threeAtHandover).ToString("F0") + " vs " + ignOne.ToString("F0"));

        // ---- BRANCH "1": comfortably above the single engine's ignition altitude ----
        HoverslamInputs oneRoomy = Bank(EngCentreAccel, ignOne * 3.0, vHigh);
        HoverslamInputs threeRoomy = Bank(EngThreeAccel, ignOne * 3.0, vHigh);
        Check("OCT6: with three times the altitude it needs, ONE engine suffices",
              Hoverslam.EnginesFor(oneRoomy, threeRoomy) == 1, "");

        // ---- BRANCH "0": the un-recoverable case its own comment names ----
        // Both banks below a thrust-to-weight of one: neither can decelerate against gravity at all.
        Check("OCT6: neither bank able to decelerate returns 0 (the un-recoverable landing)",
              Hoverslam.EnginesFor(Bank(5.0, 500.0, 200.0), Bank(9.0, 500.0, 200.0)) == 0, "");
        Check("...and a three-engine bank that CAN decelerate is never reported as 0",
              Hoverslam.EnginesFor(Bank(5.0, 500.0, 200.0), Bank(EngThreeAccel, 500.0, 200.0)) == 3, "");

        // ---- BRANCH "0", REACHABLE EVEN WHEN THREE ENGINES OUT-THRUST GRAVITY (OCT8, 2026-09-05) ----
        // Before OCT8 the three-engine branch was a bare TWR test, so "0" was reachable only when three
        // engines ALSO couldn't out-thrust gravity — almost never, and narrower than the function's own
        // comment ("0 if even three cannot stop") ever promised. Sit the stage at HALF the altitude three
        // engines need to arrest — TWR comfortably above 1 for BOTH banks, too low and too fast for
        // EITHER to actually stop in the room that is left.
        double ignThree = Hoverslam.IgnitionAltitude(Bank(EngThreeAccel, 0.0, vHigh));
        HoverslamInputs oneTooLow = Bank(EngCentreAccel, ignThree / 2.0, vHigh);
        HoverslamInputs threeTooLow = Bank(EngThreeAccel, ignThree / 2.0, vHigh);
        Check("OCT8: too low/fast for the 3-engine bank returns 0, not 3 — even with 3-engine TWR > 1",
              Hoverslam.EnginesFor(oneTooLow, threeTooLow) == 0,
              "ignThree=" + ignThree.ToString("F0") + " alt=" + (ignThree / 2.0).ToString("F0"));

        // ---- MONOTONICITY — the property the ONE-WAY LATCH rests on ----
        // At a fixed altitude, sweeping the descent speed DOWN (which is what a landing burn does), the
        // answer must move 3 -> 1 and never back. `IgnitionAltitude` is monotonic in speed (checked
        // above), so the crossing happens exactly once — that is what makes a latch honest rather than a
        // way of hiding a solver that wanders.
        const double sweepAlt = 900.0;
        int last = 3, flips = 0; bool everBack = false;
        for (double v = 250.0; v >= 2.0; v -= 1.0)
        {
            int n = Hoverslam.EnginesFor(Bank(EngCentreAccel, sweepAlt, v), Bank(EngThreeAccel, sweepAlt, v));
            if (n != last) { flips++; if (n == 3 && last == 1) everBack = true; last = n; }
        }
        Check("OCT6: as the stage slows the answer goes 3 -> 1 and NEVER back to 3",
              !everBack && flips == 1 && last == 1, "flips=" + flips + " last=" + last);
        Check("...and it really did start at 3 (the sweep is not vacuously all-ones)",
              Hoverslam.EnginesFor(Bank(EngCentreAccel, sweepAlt, 250.0),
                                   Bank(EngThreeAccel, sweepAlt, 250.0)) == 3, "");
    }

    // =====================================================================================
    // GRID-FIN steering: controlled, capped, points the correction toward −error.
    // =====================================================================================
    static void GridFinChecks()
    {
        GridFinInputs g0 = new GridFinInputs { AoaMaxDeg = 20.0, GainDegPerKm = 4.0, LeadTauS = 3.0 };
        GridFinCommand c0 = GridFin.Steer(g0);
        Check("no error → no AoA, no tilt", c0.AoaDeg == 0.0 && c0.TiltDown == 0.0, "");

        GridFinInputs gLong = g0; gLong.DownrangeErrM = 2000.0;   // 2 km long
        GridFinCommand cL = GridFin.Steer(gLong);
        Check("a downrange overshoot commands AoA", cL.AoaDeg > 0.0, cL.AoaDeg.ToString("F1"));
        Check("...and tilts the correction back (−downrange)", cL.TiltDown < -0.99, cL.TiltDown.ToString("F2"));

        GridFinInputs gCross = g0; gCross.CrossrangeErrM = 2000.0;
        GridFinCommand cC = GridFin.Steer(gCross);
        Check("a crossrange error tilts across (−cross)", cC.TiltCross < -0.99, cC.TiltCross.ToString("F2"));
        Check("the tilt is a unit direction",
              Math.Abs(Math.Sqrt(cC.TiltDown * cC.TiltDown + cC.TiltCross * cC.TiltCross) - 1.0) < 1e-6, "");

        GridFinInputs gHuge = g0; gHuge.DownrangeErrM = 100000.0;   // 100 km
        Check("AoA is CAPPED — no wild angle of attack", GridFin.Steer(gHuge).AoaDeg <= 20.0 + 1e-9,
              GridFin.Steer(gHuge).AoaDeg.ToString("F1"));
        Check("a bigger error commands a bigger AoA (until the cap)", cL.AoaDeg < GridFin.Steer(gHuge).AoaDeg, "");

        GridFinInputs gLead = g0; gLead.DownrangeErrM = 1000.0; gLead.DownrangeRateMps = 100.0;
        GridFinInputs gNoLead = g0; gNoLead.DownrangeErrM = 1000.0; gNoLead.DownrangeRateMps = 0.0;
        Check("the lead term anticipates a growing error (bigger AoA)",
              GridFin.Steer(gLead).AoaDeg > GridFin.Steer(gNoLead).AoaDeg, "");
    }

    // =====================================================================================
    // THE THREE HEADLINE CONTRACTS, in EVERY phase, for BOTH modes, and on garbage.
    // =====================================================================================
    static void ContractChecks()
    {
        double[] altitudes = { 150000.0, 70000.0, 20000.0, 5000.0, 1500.0, 400.0, 100.0, 0.5 };

        foreach (TargetMode mode in new[] { TargetMode.Rtls, TargetMode.Asds })
        {
            foreach (double alt in altitudes)
            {
                BoosterInputs b = Booster(mode);
                b.AltitudeM = alt;
                b.Land.AltitudeM = alt;
                b.Fin.DownrangeErrM = 3000.0;      // demand a steer, so the cap is doing work
                b.Fin.CrossrangeErrM = 900.0;

                foreach (BoosterPhase ph in AllPhases)
                {
                    BoosterCommand cc = BoosterDescent.Guide(b, ph);
                    string where = mode + "/" + ph + "@" + alt.ToString("F0") + "m";

                    Check("(1) AimForward is a definite UNIT vector — " + where,
                          cc.AimForward.IsFinite && Math.Abs(cc.AimForward.Magnitude - 1.0) < 1e-9,
                          cc.AimForward.Magnitude.ToString("F9"));

                    Check("(2) |AoaDeg| <= AoaCapDeg — " + where,
                          Math.Abs(cc.AoaDeg) <= cc.AoaCapDeg + 1e-9,
                          cc.AoaDeg.ToString("F3") + " vs cap " + cc.AoaCapDeg.ToString("F3"));

                    Check("(2b) the cap never exceeds the fixture's own 20° authority — " + where,
                          cc.AoaCapDeg <= 20.0 + 1e-9, cc.AoaCapDeg.ToString("F3"));

                    if (Math.Abs(cc.AoaDeg) > 1e-9)
                    {
                        double off = Vec3.Angle(cc.AimForward, Retro(b)) * 180.0 / Math.PI;
                        Check("(3) the aim sits EXACTLY |AoaDeg| off retrograde — " + where,
                              Math.Abs(off - Math.Abs(cc.AoaDeg)) < 1e-6,
                              off.ToString("F6") + " vs " + Math.Abs(cc.AoaDeg).ToString("F6"));
                    }

                    Check("the throttle is a fraction in [0,1] — " + where,
                          cc.Throttle >= 0.0 && cc.Throttle <= 1.0, cc.Throttle.ToString("F3"));
                    Check("nothing is throttled while the engines are not lit — " + where,
                          cc.EnginesLit || cc.Throttle == 0.0, cc.Throttle.ToString("F3"));
                    Check("the command carries the target mode — " + where, cc.Mode == mode, cc.Mode.ToString());
                }
            }
        }

        // an INVALID vessel still gets a defined attitude (never uncommanded)
        BoosterInputs bad = new BoosterInputs { Valid = false, Up = new Vec3(1, 0, 0) };
        foreach (BoosterPhase ph in AllPhases)
            Check("an invalid vessel still gets a defined aim in " + ph,
                  Math.Abs(BoosterDescent.Guide(bad, ph).AimForward.Magnitude - 1.0) < 1e-9, "");
        Check("an invalid vessel parks in Idle", BoosterDescent.Guide(bad, BoosterPhase.LandingBurn).Phase == BoosterPhase.Idle, "");

        // ...and so does OUTRIGHT GARBAGE: NaN velocity, a zero Up, an unnormalised facing.
        foreach (BoosterPhase ph in AllPhases)
        {
            BoosterInputs junk = Booster(TargetMode.Rtls);
            junk.SurfaceVelocity = new Vec3(double.NaN, 0.0, 0.0);
            junk.Up = Vec3.Zero;
            junk.Facing = new Vec3(0.0, 0.0, 0.0);
            junk.CommandedForward = new Vec3(double.PositiveInfinity, 0, 0);
            BoosterCommand jc = BoosterDescent.Guide(junk, ph);
            Check("NaN velocity + zero Up still yields a unit aim in " + ph,
                  jc.AimForward.IsFinite && Math.Abs(jc.AimForward.Magnitude - 1.0) < 1e-9,
                  jc.AimForward.Magnitude.ToString("F9"));
            Check("...and a capped AoA in " + ph, Math.Abs(jc.AoaDeg) <= jc.AoaCapDeg + 1e-9, "");
        }

        // the mode resolver — the seam §B16.9's per-mission LZ resolution feeds (W3: it had no consumer)
        Check("RecoveryMode.RTLS resolves to TargetMode.Rtls",
              BoosterDescent.TargetModeFor(RecoveryMode.RTLS) == TargetMode.Rtls, "");
        Check("RecoveryMode.Droneship resolves to TargetMode.Asds",
              BoosterDescent.TargetModeFor(RecoveryMode.Droneship) == TargetMode.Asds, "");
    }

    // =====================================================================================
    // §4.1 THE FLIP — a rate-limited command SHAPER, not a slew-to-target.
    // =====================================================================================
    static void FlipChecks()
    {
        Vec3 up = new Vec3(1, 0, 0);
        Vec3 from = new Vec3(0, -1, 0);
        Vec3 to = new Vec3(0, 1, 0);              // the ANTIPARALLEL case — the 180° boostback flip
        Vec3 axis = new Vec3(0, 0, 1);
        bool done;

        Vec3 step1 = BoosterDescent.AdvanceFlip(Vec3.Zero, to, from, axis, 0.02, out done);
        Check("the flip ADVANCES rather than commanding the final attitude", !done, "");
        Check("...by the rate-limited step, not the whole 180°",
              Math.Abs(Vec3.Angle(step1, from) * 180.0 / Math.PI
                       - BoosterDescent.FlipRateDegPerS * 0.02) < 1e-6,
              (Vec3.Angle(step1, from) * 180.0 / Math.PI).ToString("F4"));
        Check("...and the antiparallel 180° case still resolves to a unit vector",
              Math.Abs(step1.Magnitude - 1.0) < 1e-9, step1.Magnitude.ToString("F9"));

        // THE LEAD GATE: the command stops advancing while the vehicle falls behind it.
        Vec3 lagging = BoosterDescent.RotateToward(from, to, BoosterDescent.FlipLeadGateDeg + 5.0, axis);
        Vec3 held = BoosterDescent.AdvanceFlip(lagging, to, from, axis, 0.02, out done);
        Check("the LEAD GATE holds the command while the vehicle is behind it",
              Vec3.Angle(held, lagging) < 1e-9, (Vec3.Angle(held, lagging) * 180 / Math.PI).ToString("F4"));
        Vec3 keptUp = BoosterDescent.AdvanceFlip(lagging, to, lagging, axis, 0.02, out done);
        Check("...and releases it the moment the vehicle catches up",
              Vec3.Angle(keptUp, lagging) > 1e-9, "");

        // SNAP: inside the snap angle the command becomes the exact final vector.
        Vec3 nearly = BoosterDescent.RotateToward(to, from, BoosterDescent.FlipSnapDeg - 1.0, axis);
        Vec3 snapped = BoosterDescent.AdvanceFlip(nearly, to, nearly, axis, 0.02, out done);
        Check("inside the snap angle the flip completes", done, "");
        Check("...on the EXACT final vector", Vec3.Angle(snapped, to) < 1e-9, "");

        // no clock (headless) → defined behaviour: snap immediately.
        BoosterDescent.AdvanceFlip(from, to, from, axis, 0.0, out done);
        Check("with no clock supplied the flip snaps and is defined", done, "");

        // the FSM: the flip is a STATE, and it holds until the shaper says it is complete.
        BoosterInputs high = Booster(TargetMode.Rtls);
        high.AltitudeM = 150000.0; high.Land.AltitudeM = 150000.0;
        high.DtS = 0.02; high.Facing = Retro(high); high.CommandedForward = Vec3.Zero;
        BoosterCommand f = BoosterDescent.Guide(high, BoosterPhase.Flip);
        Check("the FSM STAYS in Flip while the slew is still running", f.Phase == BoosterPhase.Flip, f.Phase.ToString());
        Check("Idle enters Flip", BoosterDescent.Guide(high, BoosterPhase.Idle).Phase == BoosterPhase.Flip, "");
        Check("nothing burns during the flip", !f.EnginesLit && f.Throttle == 0.0, "");
        Check("OCT3: the flip's OFF state is spelled ModeOff, never the ascent (all-engines) mode",
              f.EngineMode == VehicleParts.ModeOff, "mode=" + f.EngineMode);

        // late/degenerate: already at the entry gate, so there is no return leg left to fly.
        BoosterInputs late = Booster(TargetMode.Rtls);
        late.AltitudeM = 40000.0; late.DtS = 0.02; late.Facing = Retro(late);
        Check("a flip started below the entry gate falls through rather than slewing on",
              BoosterDescent.Guide(late, BoosterPhase.Flip).Phase == BoosterPhase.Boostback, "");
    }

    // =====================================================================================
    // §4.2 BOOSTBACK — ONE ALWAYS-ENTERED STATE, magnitude parameterized by target mode.
    // =====================================================================================
    static void BoostbackChecks()
    {
        // ⛔ §B16.2's C1.8 OVERRIDE: BOTH profiles enter it. Never an RTLS-only optional state.
        foreach (TargetMode mode in new[] { TargetMode.Rtls, TargetMode.Asds })
        {
            BoosterInputs b = Booster(mode);
            b.AltitudeM = 150000.0; b.Land.AltitudeM = 150000.0;
            b.DtS = 0.0;                                  // no clock → the flip snaps → complete
            Check("BOTH profiles ENTER boostback out of the flip — " + mode,
                  BoosterDescent.Guide(b, BoosterPhase.Flip).Phase == BoosterPhase.Boostback, mode.ToString());
        }

        // ASDS's DEFAULT MAGNITUDE IS ZERO — the old "no boostback" behaviour, reproduced exactly.
        Check("ASDS's default boostback magnitude is ZERO",
              BoosterProfile.For(TargetMode.Asds).BoostbackMagnitude == 0.0,
              BoosterProfile.For(TargetMode.Asds).BoostbackMagnitude.ToString("F3"));
        Check("RTLS's default boostback magnitude is the full return burn",
              BoosterProfile.For(TargetMode.Rtls).BoostbackMagnitude == 1.0, "");

        BoosterInputs asds = Booster(TargetMode.Asds);
        asds.AltitudeM = 150000.0; asds.Land.AltitudeM = 150000.0;
        asds.DownrangeErrM = 400000.0; asds.InitialDownrangeErrM = 400000.0;
        BoosterCommand ab = BoosterDescent.Guide(asds, BoosterPhase.Boostback);
        Check("a zero-magnitude ASDS boostback commands NO thrust", !ab.EnginesLit && ab.Throttle == 0.0, "");
        Check("...holds FULL surface retrograde, exactly as the four-phase FSM did",
              Vec3.Dot(ab.AimForward, Retro(asds)) > 1.0 - 1e-9,
              Vec3.Dot(ab.AimForward, Retro(asds)).ToString("F9"));
        Check("...and leaves for the ballistic COAST on the same tick",
              ab.Phase == BoosterPhase.Coast, ab.Phase.ToString());

        // RTLS: the full flip-and-null-target-error return burn.
        BoosterInputs rtls = Booster(TargetMode.Rtls);
        rtls.AltitudeM = 150000.0; rtls.Land.AltitudeM = 150000.0;
        rtls.DownrangeErrM = 400000.0; rtls.InitialDownrangeErrM = 400000.0;
        BoosterCommand rb = BoosterDescent.Guide(rtls, BoosterPhase.Boostback);
        Check("an RTLS boostback BURNS", rb.EnginesLit && rb.Throttle > 0.0, rb.Throttle.ToString("F3"));
        Check("...on the three-engine set", rb.EngineMode == VehicleParts.ModeThreeEngine, "mode=" + rb.EngineMode);
        Check("...steering at the target bearing, NOT retrograde",
              Vec3.Dot(rb.AimForward, rtls.TargetBearing.Normalized) > 0.99,
              Vec3.Dot(rb.AimForward, rtls.TargetBearing.Normalized).ToString("F3"));
        Check("...and STAYS in boostback while the error is still large",
              rb.Phase == BoosterPhase.Boostback, rb.Phase.ToString());

        // THE THROTTLE LAW: proportional to the remaining error, normalised by the error at burn start.
        BoosterProfile p = BoosterProfile.For(TargetMode.Rtls);
        double full = BoosterDescent.BoostbackThrottle(p, 400000.0, 400000.0);
        double half = BoosterDescent.BoostbackThrottle(p, 200000.0, 400000.0);
        double late = BoosterDescent.BoostbackThrottle(p, 20000.0, 400000.0);
        Check("the burn opens at full authority", Math.Abs(full - 1.0) < 1e-9, full.ToString("F3"));
        Check("...tapers smoothly as the impact walks onto the target", half < full && half > late,
              half.ToString("F3") + " vs " + late.ToString("F3"));
        Check("...and never falls below the engine's own minimum throttle",
              BoosterDescent.BoostbackThrottle(p, 1.0, 400000.0) >= BoosterDescent.MinThrottleThreeLanding - 1e-12,
              BoosterDescent.BoostbackThrottle(p, 1.0, 400000.0).ToString("F6"));
        Check("the floor IS the craft dump's measured ThreeLanding minimum",
              p.ThrottleFloor == BoosterDescent.MinThrottleThreeLanding, p.ThrottleFloor.ToString("F6"));
        Check("a nulled error CUTS the burn", BoosterDescent.BoostbackThrottle(p, 0.0, 400000.0) == 0.0, "");
        Check("an impact already PAST the target cuts the burn (the signed test)",
              BoosterDescent.BoostbackThrottle(p, -5000.0, 400000.0) == 0.0, "");
        Check("a zero-magnitude profile never throttles",
              BoosterDescent.BoostbackThrottle(BoosterProfile.For(TargetMode.Asds), 400000.0, 400000.0) == 0.0, "");

        // the deliberate LONG bias is NOT SEEDED — it is a convergence target (§B16.2 / method §10).
        Check("the downrange aim bias starts at ZERO on both profiles (NOT seeded from the tier-2 2700 m)",
              BoosterProfile.For(TargetMode.Rtls).DownrangeAimM == 0.0
              && BoosterProfile.For(TargetMode.Asds).DownrangeAimM == 0.0, "");
        Check("the ASDS aim offset starts at ZERO too (convergence target: 5°)",
              BoosterProfile.For(TargetMode.Asds).AimOffsetDeg == 0.0, "");
        // ...but the bias is honoured once it IS converged: the cut then leaves the impact LONG by it.
        BoosterProfile biased = BoosterProfile.For(TargetMode.Rtls); biased.DownrangeAimM = 2700.0;
        Check("with a bias set, the cut leaves the impact LONG by exactly that bias",
              BoosterDescent.BoostbackThrottle(biased, 2700.0, 400000.0) == 0.0
              && BoosterDescent.BoostbackThrottle(biased, 8000.0, 400000.0) > 0.0, "");

        // §B16.3's IGNITION BUDGET — refuse a phase the budget cannot cover, and say why.
        BoosterInputs starved = rtls; starved.IgnitionsThreeLanding = 1; starved.IgnitionsCentreOnly = 1;
        BoosterCommand sb = BoosterDescent.Guide(starved, BoosterPhase.Boostback);
        Check("boostback REFUSES when ThreeLanding's only ignition is owed to the entry burn",
              !sb.EnginesLit && sb.Throttle == 0.0 && sb.Refusal != null, sb.Refusal);
        Check("...and hands on to the coast rather than stalling", sb.Phase == BoosterPhase.Coast, "");
        BoosterInputs funded = rtls; funded.IgnitionsThreeLanding = 2; funded.IgnitionsCentreOnly = 1;
        Check("...but burns when the budget genuinely covers both",
              BoosterDescent.Guide(funded, BoosterPhase.Boostback).EnginesLit, "");
        Check("an unsupplied ignition count leaves the guard INERT (0 = not supplied)",
              rtls.IgnitionsThreeLanding == 0 && rb.Refusal == null, "");

        // an RTLS with nothing to aim at refuses rather than burning in a made-up direction.
        BoosterInputs blind = rtls; blind.TargetBearing = Vec3.Zero;
        BoosterCommand bb = BoosterDescent.Guide(blind, BoosterPhase.Boostback);
        Check("RTLS with no target bearing REFUSES to burn", !bb.EnginesLit && bb.Refusal != null, bb.Refusal);
        Check("...and still returns a definite unit aim", Math.Abs(bb.AimForward.Magnitude - 1.0) < 1e-9, "");
    }

    // =====================================================================================
    // The BALLISTIC COAST — §B16.2 phase 2, absent entirely from the four-phase FSM.
    // =====================================================================================
    static void CoastChecks()
    {
        BoosterInputs high = Booster(TargetMode.Asds);
        high.AltitudeM = 150000.0; high.Land.AltitudeM = 150000.0; high.SpeedMps = 2200.0;
        BoosterCommand ch = BoosterDescent.Guide(high, BoosterPhase.Coast);
        Check("the coast is BALLISTIC — nothing lit, nothing throttled",
              !ch.EnginesLit && ch.Throttle == 0.0, "");
        Check("...held retrograde", Vec3.Dot(ch.AimForward, Retro(high)) > 1.0 - 1e-9, "");
        Check("...with no AoA claimed", ch.AoaDeg == 0.0 && ch.AoaCapDeg == 0.0, "");
        Check("...and holds above the entry gate", ch.Phase == BoosterPhase.Coast, ch.Phase.ToString());

        BoosterInputs fast = Booster(TargetMode.Asds);
        fast.AltitudeM = 60000.0; fast.Land.AltitudeM = 60000.0; fast.SpeedMps = 2000.0;
        Check("the coast hands to the ENTRY BURN at the gate, fast",
              BoosterDescent.Guide(fast, BoosterPhase.Coast).Phase == BoosterPhase.EntryBurn, "");

        BoosterInputs slow = Booster(TargetMode.Asds);
        slow.AltitudeM = 60000.0; slow.Land.AltitudeM = 60000.0; slow.SpeedMps = 900.0;
        Check("...and skips straight to the AERO DESCENT when it is already slow enough",
              BoosterDescent.Guide(slow, BoosterPhase.Coast).Phase == BoosterPhase.AeroDescent,
              BoosterDescent.Guide(slow, BoosterPhase.Coast).Phase.ToString());
    }

    // =====================================================================================
    // §4.3 ENTRY BURN — altitude gate, velocity cutoff, payload-mass correction.
    // =====================================================================================
    static void EntryBurnChecks()
    {
        BoosterInputs entry = Booster(TargetMode.Asds);
        entry.AltitudeM = 60000.0; entry.Land.AltitudeM = 60000.0; entry.SpeedMps = 2000.0;
        BoosterCommand eb = BoosterDescent.Guide(entry, BoosterPhase.EntryBurn);
        Check("the entry burn is the 3-engine set at full",
              eb.EnginesLit && eb.EngineMode == VehicleParts.ModeThreeEngine && eb.Throttle == 1.0,
              "mode=" + eb.EngineMode + " thr=" + eb.Throttle.ToString("F2"));
        Check("...steered PURE surface-retrograde (no steering-to-target here)",
              Vec3.Dot(eb.AimForward, Retro(entry)) > 1.0 - 1e-9, "");
        Check("...with the grid fins out at the gate", eb.DeployFins, "");

        BoosterInputs bled = Booster(TargetMode.Asds);
        bled.SpeedMps = 1200.0; bled.AltitudeM = 40000.0; bled.Land.AltitudeM = 40000.0;
        BoosterCommand cut = BoosterDescent.Guide(bled, BoosterPhase.EntryBurn);
        Check("the entry burn CUTS ON SPEED, into the aero descent", cut.Phase == BoosterPhase.AeroDescent, "");
        Check("...and stops thrusting as it does", !cut.EnginesLit && cut.Throttle == 0.0, "");

        // the payload-mass correction: a lighter payload raises the gate and tightens the cutoff.
        BoosterProfile plain = BoosterProfile.For(TargetMode.Rtls);
        Check("the payload correction is INERT with no reference payload (RSS-RO has none — see Q2)",
              BoosterDescent.EntryGateAltM(plain, 3500.0) == plain.EntryGateAltM
              && BoosterDescent.EntryCutSpeedMps(plain, 3500.0) == plain.EntryCutSpeedMps, "");
        BoosterProfile withRef = plain; withRef.MaxPayloadKg = 7000.0;
        Check("...and inert again when the live payload is not supplied",
              BoosterDescent.EntryGateAltM(withRef, 0.0) == withRef.EntryGateAltM, "");
        Check("a lighter payload RAISES the entry gate (a hotter, faster booster)",
              BoosterDescent.EntryGateAltM(withRef, 3500.0) > BoosterDescent.EntryGateAltM(withRef, 6900.0),
              BoosterDescent.EntryGateAltM(withRef, 3500.0).ToString("F0"));
        Check("...and TIGHTENS the cutoff speed by the same shortfall",
              BoosterDescent.EntryCutSpeedMps(withRef, 3500.0) < BoosterDescent.EntryCutSpeedMps(withRef, 6900.0), "");
        Check("...both linearly in the shortfall",
              Math.Abs(BoosterDescent.EntryGateAltM(withRef, 3500.0)
                       - (withRef.EntryGateAltM + 3500.0 / BoosterDescent.EntryGatePayloadDivisor)) < 1e-6, "");
        Check("a payload OVER the reference does not push the gate down",
              BoosterDescent.EntryGateAltM(withRef, 9000.0) == withRef.EntryGateAltM, "");

        // §B16.3's budget refusal, on the entry burn too.
        BoosterInputs spent = entry; spent.IgnitionsThreeLanding = 0; spent.IgnitionsCentreOnly = 1;
        BoosterCommand sc = BoosterDescent.Guide(spent, BoosterPhase.EntryBurn);
        Check("the entry burn refuses a spent ThreeLanding rather than commanding a dead engine",
              !sc.EnginesLit && sc.Refusal != null, sc.Refusal);
    }

    // =====================================================================================
    // §4.4 AERO DESCENT — one steering law, and the AUTHORITY TAPER that makes it vertical.
    // =====================================================================================
    static void AeroDescentChecks()
    {
        BoosterInputs aero = Booster(TargetMode.Asds);
        aero.AltitudeM = 20000.0; aero.Land.AltitudeM = 20000.0; aero.Fin.DownrangeErrM = 3000.0;
        BoosterCommand ad = BoosterDescent.Guide(aero, BoosterPhase.AeroDescent);
        Check("the aero descent commands a HELD AoA", ad.AoaDeg > 0.0 && ad.AoaDeg <= 20.0, ad.AoaDeg.ToString("F1"));
        Check("...and no thrust", !ad.EnginesLit && ad.Throttle == 0.0, "");
        double tiltAngle = Vec3.Angle(ad.AimForward, Retro(aero)) * 180.0 / Math.PI;
        Check("the aim is retro tilted by EXACTLY the commanded AoA",
              Math.Abs(tiltAngle - ad.AoaDeg) < 1e-6, tiltAngle.ToString("F6") + " vs " + ad.AoaDeg.ToString("F6"));

        // THE AUTHORITY TAPER — the cap is surrendered smoothly as the ground approaches.
        Check("above the taper altitude the cap is the vehicle's own AoA limit",
              BoosterDescent.AoaCapDeg(BoosterPhase.AeroDescent, 20000.0, 20.0) == 20.0, "");
        Check("the taper bites below 10 km",
              BoosterDescent.AoaCapDeg(BoosterPhase.AeroDescent, 1500.0, 20.0) < 20.0, "");
        double[] ladder = { 5000.0, 1500.0, 1000.0, 500.0, 100.0 };
        for (int i = 1; i < ladder.Length; i++)
            Check("the cap tapers MONOTONICALLY toward the ground (" + ladder[i - 1] + " -> " + ladder[i] + " m)",
                  BoosterDescent.AoaCapDeg(BoosterPhase.AeroDescent, ladder[i], 20.0)
                  < BoosterDescent.AoaCapDeg(BoosterPhase.AeroDescent, ladder[i - 1], 20.0), "");
        Check("the cap reaches ZERO at the deck — the stage stands up on its thrust axis",
              BoosterDescent.AoaCapDeg(BoosterPhase.AeroDescent, 0.0, 20.0) == 0.0, "");

        // ...and the FSM actually applies it: a huge error low down is still capped by the taper.
        BoosterInputs low = Booster(TargetMode.Asds);
        low.AltitudeM = 500.0; low.Land.AltitudeM = 500.0; low.Fin.DownrangeErrM = 100000.0;
        BoosterCommand lowc = BoosterDescent.Guide(low, BoosterPhase.AeroDescent);
        Check("a 100 km miss at 500 m still commands only the tapered cap",
              Math.Abs(lowc.AoaDeg - BoosterDescent.AoaCapDeg(BoosterPhase.AeroDescent, 500.0, 20.0)) < 1e-9,
              lowc.AoaDeg.ToString("F3"));

        // and the hand-off to the landing burn is the LIVE ignition solution, evaluated every tick.
        BoosterInputs arm = Booster(TargetMode.Asds);
        arm.AltitudeM = 1200.0; arm.DescentSpeedMps = 244.0;
        arm.Land = Flight(); arm.Land.AltitudeM = 1200.0;
        Check("the aero descent hands to the LANDING BURN at the live ignition altitude",
              BoosterDescent.Guide(arm, BoosterPhase.AeroDescent).Phase == BoosterPhase.LandingBurn,
              "ign=" + Hoverslam.IgnitionAltitude(arm.Land).ToString("F0"));
    }

    // =====================================================================================
    // §4.5 LANDING BURN — the throttle law, the terminal AoA schedule, and the NON-PORT.
    // =====================================================================================
    static void LandingBurnChecks()
    {
        BoosterInputs land = Booster(TargetMode.Asds);
        land.AltitudeM = 400.0; land.DescentSpeedMps = 50.0;
        land.Land = Flight(); land.Land.AltitudeM = 400.0; land.Land.DescentSpeedMps = 50.0;
        BoosterCommand lb = BoosterDescent.Guide(land, BoosterPhase.LandingBurn);

        // ⛔ `land` supplies NO `LandThree`, which is the struct's "0 = NOT SUPPLIED" convention: with
        // only one bank measured the OCT6 shed is INERT and the burn flies CenterOnly throughout, exactly
        // as it did before OCT6. That inert path is what the next few checks pin.
        Check("with no three-engine bank supplied, the landing burn is the SINGLE CENTRE engine",
              lb.EnginesLit && lb.EngineMode == VehicleParts.ModeCentreOnly, "mode=" + lb.EngineMode);
        Check("...and it reports the shed latch SET, because CenterOnly is what it commanded",
              lb.LandingShedLatched, "");
        Check("...never below the engine's own measured minimum throttle (§B16.3: no zero mid-burn)",
              lb.Throttle >= BoosterDescent.MinThrottleCentreOnly - 1e-12, lb.Throttle.ToString("F6"));
        Check("legs deploy in the final hundreds of metres", lb.DeployLegs, "");
        Check("the fins are still out", lb.DeployFins, "");

        // ⛔ OCT6 REPLACED THE ASSERTION THAT USED TO STAND HERE. It read *"the landing burn NEVER
        // steps back to the 3-engine set (no 3→1 handover — ignitions = 1)"*, swept the altitude band
        // and required `everThree == false`. It was GREEN and it was WRONG after the owner's ruling
        // (*"1. (2)"*): the burn now STARTS on three. The half of it that survives the ruling — and is
        // in fact strengthened by it — is the direction: never back to three ONCE SHED. Same sweep,
        // now with both banks supplied and the latch carried, which is how the host runs it.
        // At the HAND-OVER state — the altitude `AeroDescent` gives up the stage at, which is by
        // definition the single engine's own ignition altitude — three engines are what can still arrest.
        double handoverAlt = Hoverslam.IgnitionAltitude(Bank(EngCentreAccel, 0.0, 244.0));
        BoosterInputs opening = Booster(TargetMode.Asds);
        opening.AltitudeM = handoverAlt; opening.DescentSpeedMps = 244.0;
        opening.Land = Bank(EngCentreAccel, handoverAlt, 244.0);
        opening.LandThree = Bank(EngThreeAccel, handoverAlt, 244.0);
        BoosterCommand oc = BoosterDescent.Guide(opening, BoosterPhase.LandingBurn);
        Check("OCT6: the landing burn OPENS on the 3-engine bank (the ruling, not W8's non-port)",
              oc.EnginesLit && oc.EngineMode == VehicleParts.ModeThreeEngine, "mode=" + oc.EngineMode);
        Check("...and has not latched the shed yet",
              !oc.LandingShedLatched, "");
        Check("...and its throttle is solved against the THREE-engine bank it just lit, not the centre one",
              Math.Abs(oc.Throttle
                       - BoosterDescent.LandingThrottle(opening, opening.LandThree,
                                                        BoosterDescent.MinThrottleThreeLanding)) < 1e-12,
              oc.Throttle.ToString("F6"));

        // ⛔ AND THE LATCH, at the one state that can prove it in isolation: the SAME inputs that just
        // returned three, with the latch already set, must return CENTRE and stay there. `EnginesFor`
        // still says 3 here — this is the tick the un-latched code would chatter on.
        BoosterInputs reopened = opening; reopened.LandingShedLatched = true;
        BoosterCommand rc2 = BoosterDescent.Guide(reopened, BoosterPhase.LandingBurn);
        Check("OCT6: once shed, the identical state that asked for THREE commands CENTRE instead",
              rc2.EnginesLit && rc2.EngineMode == VehicleParts.ModeCentreOnly
              && Hoverslam.EnginesFor(opening.Land, opening.LandThree) == 3, "mode=" + rc2.EngineMode);
        Check("...and the latch stays set — nothing in the FSM ever clears it",
              rc2.LandingShedLatched, "");
        Check("the entry burn and the landing burn are DISTINCT engine sets",
              VehicleParts.ModeThreeEngine != VehicleParts.ModeCentreOnly, "");

        // The 0 case reaches the command as an honest REFUSAL, never a silent fallback to some bank.
        BoosterInputs hopeless = Booster(TargetMode.Asds);
        hopeless.AltitudeM = 500.0; hopeless.DescentSpeedMps = 200.0;
        hopeless.Land = Bank(5.0, 500.0, 200.0);        // TWR < 1 on one engine...
        hopeless.LandThree = Bank(9.0, 500.0, 200.0);   // ...and on three
        BoosterCommand hc = BoosterDescent.Guide(hopeless, BoosterPhase.LandingBurn);
        Check("OCT6: 'even three cannot arrest' REFUSES rather than falling back to a bank",
              !hc.EnginesLit && hc.EngineMode == VehicleParts.ModeOff && hc.Refusal != null, hc.Refusal);
        Check("...and the refusal says which impossibility it is",
              hc.Refusal != null && hc.Refusal.IndexOf("3-engine") >= 0, hc.Refusal);

        // THE THROTTLE LAW: stopDistance / trueAltitude + margin, self-correcting, evaluated live.
        // "tight" = far more braking distance needed than altitude left; "roomy" = comfortably above it.
        BoosterInputs tight = land;
        tight.AltitudeM = 60.0; tight.Land.AltitudeM = 60.0;
        tight.DescentSpeedMps = 200.0; tight.Land.DescentSpeedMps = 200.0;
        BoosterInputs roomy = land; roomy.AltitudeM = 2000.0; roomy.Land.AltitudeM = 2000.0;
        Check("needing more braking distance than altitude SATURATES the throttle law",
              BoosterDescent.LandingThrottle(tight) > BoosterDescent.LandingThrottle(roomy)
              && BoosterDescent.LandingThrottle(tight) == 1.0,
              BoosterDescent.LandingThrottle(tight).ToString("F3") + " vs "
              + BoosterDescent.LandingThrottle(roomy).ToString("F3"));
        Check("...and comfortably above it, the throttle backs off to the engine floor",
              BoosterDescent.LandingThrottle(roomy) == BoosterDescent.MinThrottleCentreOnly,
              BoosterDescent.LandingThrottle(roomy).ToString("F6"));
        Check("the throttle law never exceeds 1", BoosterDescent.LandingThrottle(tight) <= 1.0, "");
        BoosterInputs faster = land; faster.Land.DescentSpeedMps = 150.0;
        Check("a faster arrival commands more throttle (self-correcting by construction)",
              BoosterDescent.LandingThrottle(faster) > BoosterDescent.LandingThrottle(land),
              BoosterDescent.LandingThrottle(faster).ToString("F3"));

        // THE FLARE: a step in throttle margin below the flare altitude, far larger than the two metres
        // of geometry either side of it could produce. Speeds chosen so neither point is saturated.
        BoosterInputs justBelow = land;
        justBelow.AltitudeM = 24.0; justBelow.Land.AltitudeM = 24.0;
        justBelow.DescentSpeedMps = 35.0; justBelow.Land.DescentSpeedMps = 35.0;
        BoosterInputs justAbove = justBelow;
        justAbove.AltitudeM = 26.0; justAbove.Land.AltitudeM = 26.0;
        Check("the FLARE steps the throttle margin up in the last metres",
              BoosterDescent.LandingThrottle(justBelow) > BoosterDescent.LandingThrottle(justAbove) + 0.3,
              BoosterDescent.LandingThrottle(justBelow).ToString("F3") + " vs "
              + BoosterDescent.LandingThrottle(justAbove).ToString("F3"));
        Check("...without saturating either side of it",
              BoosterDescent.LandingThrottle(justBelow) < 1.0
              && BoosterDescent.LandingThrottle(justAbove) > BoosterDescent.MinThrottleCentreOnly, "");

        // THE TERMINAL AoA SCHEDULE: the lean goes NEGATIVE and tightens toward the deck.
        BoosterInputs steer = land; steer.Fin.DownrangeErrM = 3000.0;
        BoosterCommand sc = BoosterDescent.Guide(steer, BoosterPhase.LandingBurn);
        Check("the terminal lean is NEGATIVE — the opposite way to the descent correction",
              sc.AoaDeg < 0.0, sc.AoaDeg.ToString("F3"));
        double offRetro = Vec3.Angle(sc.AimForward, Retro(steer)) * 180.0 / Math.PI;
        Check("...and the aim still sits EXACTLY |AoaDeg| off retrograde",
              Math.Abs(offRetro - Math.Abs(sc.AoaDeg)) < 1e-6, offRetro.ToString("F6"));
        Check("the terminal cap is banded, never wider than the band's top",
              BoosterDescent.AoaCapDeg(BoosterPhase.LandingBurn, 100000.0, 20.0)
              == BoosterDescent.TerminalAoaMaxDeg, "");
        Check("...and is PINNED to the band's floor below the pin altitude",
              BoosterDescent.AoaCapDeg(BoosterPhase.LandingBurn, 200.0, 20.0) == BoosterDescent.TerminalAoaMinDeg, "");
        Check("...so the authority is tighter at 200 m than at 400 m",
              BoosterDescent.AoaCapDeg(BoosterPhase.LandingBurn, 200.0, 20.0)
              < BoosterDescent.AoaCapDeg(BoosterPhase.LandingBurn, 400.0, 20.0), "");

        // touchdown.
        BoosterInputs down = land; down.AltitudeM = 0.5; down.DescentSpeedMps = 1.0;
        down.Land.AltitudeM = 0.5; down.Land.DescentSpeedMps = 1.0;
        BoosterCommand dc = BoosterDescent.Guide(down, BoosterPhase.LandingBurn);
        Check("touchdown ends the burn", dc.Phase == BoosterPhase.Landed && !dc.EnginesLit && dc.Throttle == 0.0, "");
        Check("...and claims no AoA", dc.AoaDeg == 0.0 && dc.AoaCapDeg == 0.0, "");
        Check("OCT3: Landed's OFF state is spelled ModeOff, never the ascent (all-engines) mode",
              dc.EngineMode == VehicleParts.ModeOff, "mode=" + dc.EngineMode);

        // §B16.3's budget refusal, on the landing burn too.
        BoosterInputs spent = land; spent.IgnitionsThreeLanding = 1; spent.IgnitionsCentreOnly = 0;
        BoosterCommand pc = BoosterDescent.Guide(spent, BoosterPhase.LandingBurn);
        Check("the landing burn refuses a spent CenterOnly rather than commanding a dead engine",
              !pc.EnginesLit && pc.Refusal != null, pc.Refusal);
    }

    // =====================================================================================
    // §5 — the prediction primitive: ONE predictor, two tiers, and the signed long/short test.
    // =====================================================================================
    const double Mu = 3.5316e12, Rk = 600000.0, AtmH = 70000.0;
    static double Density(double alt)
    { return (alt < 0.0 || alt >= AtmH) ? 0.0 : 1.225 * Math.Exp(-alt / 5600.0); }

    static void PredictionChecks()
    {
        // ---- the SIGNED miss, on a synthetic sphere ----
        Vec3 centre = Vec3.Zero;
        double R = 6371000.0;
        Vec3 target = new Vec3(R, 0, 0);
        Vec3 vehicle = new Vec3(R * Math.Cos(0.1), -R * Math.Sin(0.1), 0);   // 0.1 rad short of the target

        Vec3 beyond = new Vec3(R * Math.Cos(0.001), R * Math.Sin(0.001), 0);  // past it, going away
        ImpactError lng = BoosterDescent.ErrorTo(beyond, target, vehicle, centre);
        Check("the impact primitive resolves", lng.Valid, "");
        Check("an impact BEYOND the target reads LONG (+)", lng.DownrangeM > 0.0, lng.DownrangeM.ToString("F0"));
        Check("...with the great-circle magnitude R·θ", Math.Abs(lng.GreatCircleM - R * 0.001) < 1.0,
              lng.GreatCircleM.ToString("F1"));
        Check("...and the signed error carries that same magnitude",
              Math.Abs(Math.Abs(lng.DownrangeM) - lng.GreatCircleM) < 1.0, "");

        Vec3 shortOf = new Vec3(R * Math.Cos(0.001), -R * Math.Sin(0.001), 0);   // on the vehicle's side
        ImpactError shrt = BoosterDescent.ErrorTo(shortOf, target, vehicle, centre);
        Check("an impact SHORT of the target reads negative (−)", shrt.DownrangeM < 0.0, shrt.DownrangeM.ToString("F0"));

        Vec3 sideways = new Vec3(R * Math.Cos(0.001), 0, R * Math.Sin(0.001));
        ImpactError xr = BoosterDescent.ErrorTo(sideways, target, vehicle, centre);
        Check("a purely lateral miss reads as CROSSRANGE, not downrange",
              Math.Abs(xr.CrossrangeM) > Math.Abs(xr.DownrangeM) * 100.0,
              xr.CrossrangeM.ToString("F0") + " / " + xr.DownrangeM.ToString("F0"));
        Check("a bang-on impact is a zero miss",
              BoosterDescent.ErrorTo(target, target, vehicle, centre).GreatCircleM < 1e-6, "");
        Check("garbage in gives an INVALID error, not a wrong number",
              !BoosterDescent.ErrorTo(new Vec3(double.NaN, 0, 0), target, vehicle, centre).Valid, "");

        // ---- the TWO-TIER predictor: ours, drag-fed, no second predictor ----
        TrajectoryInputs drop = new TrajectoryInputs();
        drop.Px = Rk + 40000.0; drop.Py = 0.0; drop.Pz = 0.0;
        drop.Mu = Mu; drop.BodyRadiusM = Rk; drop.AtmosphereDepthM = AtmH;

        TrajectoryResult bare = Trajectory.Solve(drop, Density);
        TrajectoryResult fed = BoosterDescent.PredictImpact(drop, Density);
        Check("PredictImpact answers", fed.Ok, fed.Note);
        Check("...wiring BoosterDrag's curve in for a caller that supplied none",
              fed.DragModelled && !bare.DragModelled, fed.Note + " / " + bare.Note);
        Check("...so the drag-fed answer falls SHORT of the bare vacuum one (drag only shortens)",
              fed.TimeToImpactS > bare.TimeToImpactS - 1e-9, "");

        TrajectoryInputs owned = drop;
        owned.DragFactor = delegate(double mach, double rhoV) { return 0.0; };   // caller's own: drag-free
        TrajectoryResult kept = BoosterDescent.PredictImpact(owned, Density);
        Check("a caller's OWN DragFactor is not overwritten",
              kept.Ok && Math.Abs(kept.TimeToImpactS - bare.TimeToImpactS) < 0.5,
              kept.TimeToImpactS.ToString("F2") + " vs " + bare.TimeToImpactS.ToString("F2"));

        TrajectoryInputs nobody = drop; nobody.Mu = 0.0;
        Check("with no body, neither tier invents an answer",
              !BoosterDescent.PredictImpact(nobody, Density).Ok, "");

        Check("the bc curve is the one the booster core reads (Mach-binned, not a scalar)",
              BoosterDrag.BcAtMach(2.0) != BoosterDrag.BcAtMach(0.5), "");
    }

    // =====================================================================================
    // §6 — ullage before EVERY ignition, and the spool ramp. This is the failure that lost the
    // booster (H1b / FLIGHT_144114): "booster ballistic, eng never lit → LOST".
    // =====================================================================================
    static void UllageAndSpoolChecks()
    {
        // every powered phase refuses to thrust unsettled, and asks for RCS instead.
        BoosterInputs bb = Booster(TargetMode.Rtls);
        bb.AltitudeM = 150000.0; bb.Land.AltitudeM = 150000.0;
        bb.DownrangeErrM = 400000.0; bb.InitialDownrangeErrM = 400000.0; bb.Ullaged = false;
        BoosterCommand bc = BoosterDescent.Guide(bb, BoosterPhase.Boostback);
        Check("an unsettled boostback does not thrust", !bc.EnginesLit && bc.Throttle == 0.0, "");
        Check("...it asks for RCS settling instead", bc.UllageRcs, "");
        Check("...and holds in boostback rather than giving the phase up", bc.Phase == BoosterPhase.Boostback, "");

        BoosterInputs eb = Booster(TargetMode.Asds);
        eb.AltitudeM = 60000.0; eb.Land.AltitudeM = 60000.0; eb.SpeedMps = 2000.0; eb.Ullaged = false;
        BoosterCommand ec = BoosterDescent.Guide(eb, BoosterPhase.EntryBurn);
        Check("an unsettled entry burn does not thrust, and asks for RCS",
              !ec.EnginesLit && ec.Throttle == 0.0 && ec.UllageRcs, "");

        BoosterInputs lb = Booster(TargetMode.Asds);
        lb.AltitudeM = 400.0; lb.DescentSpeedMps = 50.0; lb.Ullaged = false;
        lb.Land = Flight(); lb.Land.AltitudeM = 400.0; lb.Land.DescentSpeedMps = 50.0;
        BoosterCommand lc = BoosterDescent.Guide(lb, BoosterPhase.LandingBurn);
        Check("an unsettled landing burn does not thrust, and asks for RCS",
              !lc.EnginesLit && lc.Throttle == 0.0 && lc.UllageRcs, "");
        Check("a SETTLED stage is not asked to ullage",
              !BoosterDescent.Guide(Booster(TargetMode.Asds), BoosterPhase.Coast).UllageRcs, "");

        // THE SPOOL RAMP: ignite at a trickle, then ramp; never step.
        double first = BoosterDescent.RampThrottle(0.0, 1.0, 0.02);
        Check("ignition starts at a TRICKLE, not at the commanded value", first > 0.0 && first < 0.2,
              first.ToString("F4"));
        double second = BoosterDescent.RampThrottle(first, 1.0, 0.02);
        Check("...and ramps up from there", second > first && second < 1.0, second.ToString("F4"));
        Check("...reaching the command in bounded time",
              BoosterDescent.RampThrottle(0.9, 1.0, 1.0) == 1.0, "");
        Check("the ramp also limits the way DOWN (spool is symmetric)",
              BoosterDescent.RampThrottle(1.0, 0.4, 0.02) > 0.4, "");
        Check("a commanded CUT is immediate — it is a shutdown, not a spool",
              BoosterDescent.RampThrottle(1.0, 0.0, 0.02) == 0.0, "");
        Check("with no clock the ramp passes the command straight through (headless is defined)",
              BoosterDescent.RampThrottle(0.0, 1.0, 0.0) == 1.0, "");

        // and the FSM runs every ignition through it.
        BoosterInputs ramped = Booster(TargetMode.Rtls);
        ramped.AltitudeM = 150000.0; ramped.Land.AltitudeM = 150000.0;
        ramped.DownrangeErrM = 400000.0; ramped.InitialDownrangeErrM = 400000.0;
        ramped.DtS = 0.02; ramped.CommandedThrottle = 0.0;
        BoosterCommand rc = BoosterDescent.Guide(ramped, BoosterPhase.Boostback);
        Check("the FSM's first boostback tick is a spool, not a step",
              rc.EnginesLit && rc.Throttle > 0.0 && rc.Throttle < 1.0, rc.Throttle.ToString("F4"));
    }

    // =====================================================================================
    // OCT5 (2026-09-05): THE FSM'S OWN OUTPUT AGAINST THE HOST'S PHASE GATE — mutation-proven.
    // BoosterHostTest.PhaseGateTests proves `BoosterHostPlan.PhaseAllows` correct against HAND-BUILT
    // (phase, role) pairs; it never asks whether THIS FSM's own commands satisfy it. OCT3 lit the
    // Boostback bank then flipped the phase to Coast on the same tick without clearing it — a real
    // command the gate would refuse, from a producer nothing exercised. For every phase this drives
    // Guide() through both its engine-lighting branch (stays in phase, still burning) and its exit
    // branch (transitions out, on the SAME tick the bank was lit where that is reachable), and asserts
    // the gate accepts the RETURNED command — never a hand-built stand-in for it.
    // =====================================================================================
    static void GateCheck(string where, BoosterCommand c)
    {
        EngineRole role = BoosterHostPlan.CommandedRole(c.EnginesLit, c.EngineMode);
        // OCT6: the latch is read off the RETURNED command, which is exactly what `src/BoosterHost.cs`
        // does — one latch, one tick, so the gate and the FSM can never disagree about which bank is
        // the legal one.
        Check("OCT5: the FSM's own command is gate-legal — " + where,
              BoosterHostPlan.PhaseAllows(c.Phase, role, c.LandingShedLatched),
              "commanded phase=" + c.Phase + " EnginesLit=" + c.EnginesLit
              + " EngineMode=" + c.EngineMode + " role=" + role + " shed=" + c.LandingShedLatched);
    }

    static void PhaseCommandGateInvariantChecks()
    {
        // ---- Idle: no lighting branch; exits straight to Flip ----
        BoosterInputs idle = Booster(TargetMode.Rtls);
        idle.AltitudeM = 150000.0; idle.Land.AltitudeM = 150000.0; idle.DtS = 0.0;
        GateCheck("Idle -> Flip", BoosterDescent.Guide(idle, BoosterPhase.Idle));

        // ---- Flip: no lighting branch; stays slewing, and exits (late/degenerate) to Boostback ----
        BoosterInputs flipStays = Booster(TargetMode.Rtls);
        flipStays.AltitudeM = 150000.0; flipStays.Land.AltitudeM = 150000.0;
        flipStays.DtS = 0.02; flipStays.Facing = Retro(flipStays); flipStays.CommandedForward = Vec3.Zero;
        GateCheck("Flip, still slewing", BoosterDescent.Guide(flipStays, BoosterPhase.Flip));

        BoosterInputs flipExits = Booster(TargetMode.Rtls);
        flipExits.AltitudeM = 40000.0; flipExits.DtS = 0.02; flipExits.Facing = Retro(flipExits);
        GateCheck("Flip -> Boostback (late/degenerate)", BoosterDescent.Guide(flipExits, BoosterPhase.Flip));

        // ---- Boostback: THE OCT5 SHAPE. Lit while staying, and lit while EXITING on the same tick —
        // via the ALTITUDE gate (the report's own scenario: the entry gate is reached independently of
        // the downrange error) and via BoostbackComplete's DEADBAND (a residual error too small to keep
        // burning for but still large enough, at the throttle floor, to command > 0). ----
        BoosterInputs bbStays = Booster(TargetMode.Rtls);
        bbStays.AltitudeM = 150000.0; bbStays.Land.AltitudeM = 150000.0;
        bbStays.DownrangeErrM = 400000.0; bbStays.InitialDownrangeErrM = 400000.0;
        GateCheck("Boostback, still burning", BoosterDescent.Guide(bbStays, BoosterPhase.Boostback));

        BoosterInputs bbExitsAlt = Booster(TargetMode.Rtls);
        double gateAlt = BoosterDescent.EntryGateAltM(bbExitsAlt.Profile.Normalized(), bbExitsAlt.PayloadMassKg);
        bbExitsAlt.AltitudeM = gateAlt; bbExitsAlt.Land.AltitudeM = gateAlt;
        bbExitsAlt.DownrangeErrM = 400000.0; bbExitsAlt.InitialDownrangeErrM = 400000.0;
        GateCheck("Boostback -> Coast at the entry gate, still burning to the last tick (OCT5)",
                   BoosterDescent.Guide(bbExitsAlt, BoosterPhase.Boostback));

        BoosterInputs bbExitsDeadband = Booster(TargetMode.Rtls);
        bbExitsDeadband.AltitudeM = 150000.0; bbExitsDeadband.Land.AltitudeM = 150000.0;
        bbExitsDeadband.DownrangeErrM = bbExitsDeadband.Profile.DownrangeAimM + BoosterDescent.BoostbackDeadbandM / 2.0;
        bbExitsDeadband.InitialDownrangeErrM = 400000.0;
        GateCheck("Boostback -> Coast inside the deadband, still throttling at the floor (OCT5)",
                   BoosterDescent.Guide(bbExitsDeadband, BoosterPhase.Boostback));

        // ---- Coast: no lighting branch; exits both ways (fast -> EntryBurn, slow -> AeroDescent) ----
        BoosterInputs coastStays = Booster(TargetMode.Asds);
        coastStays.AltitudeM = 150000.0; coastStays.Land.AltitudeM = 150000.0; coastStays.SpeedMps = 2200.0;
        GateCheck("Coast, still ballistic", BoosterDescent.Guide(coastStays, BoosterPhase.Coast));

        BoosterInputs coastFast = Booster(TargetMode.Asds);
        coastFast.AltitudeM = 60000.0; coastFast.Land.AltitudeM = 60000.0; coastFast.SpeedMps = 2000.0;
        GateCheck("Coast -> EntryBurn", BoosterDescent.Guide(coastFast, BoosterPhase.Coast));

        BoosterInputs coastSlow = Booster(TargetMode.Asds);
        coastSlow.AltitudeM = 60000.0; coastSlow.Land.AltitudeM = 60000.0; coastSlow.SpeedMps = 900.0;
        GateCheck("Coast -> AeroDescent", BoosterDescent.Guide(coastSlow, BoosterPhase.Coast));

        // ---- EntryBurn: lit while staying, and lit while exiting on the speed cut ----
        BoosterInputs ebStays = Booster(TargetMode.Asds);
        ebStays.AltitudeM = 60000.0; ebStays.Land.AltitudeM = 60000.0; ebStays.SpeedMps = 2000.0;
        GateCheck("EntryBurn, still burning", BoosterDescent.Guide(ebStays, BoosterPhase.EntryBurn));

        BoosterInputs ebExits = Booster(TargetMode.Asds);
        ebExits.SpeedMps = 1200.0; ebExits.AltitudeM = 40000.0; ebExits.Land.AltitudeM = 40000.0;
        GateCheck("EntryBurn -> AeroDescent at the speed cut", BoosterDescent.Guide(ebExits, BoosterPhase.EntryBurn));

        // ---- AeroDescent: no lighting branch; stays gliding, and exits to LandingBurn ----
        BoosterInputs aeroStays = Booster(TargetMode.Asds);
        aeroStays.AltitudeM = 20000.0; aeroStays.Land.AltitudeM = 20000.0; aeroStays.Fin.DownrangeErrM = 3000.0;
        GateCheck("AeroDescent, still gliding", BoosterDescent.Guide(aeroStays, BoosterPhase.AeroDescent));

        BoosterInputs aeroExits = Booster(TargetMode.Asds);
        aeroExits.AltitudeM = 1200.0; aeroExits.DescentSpeedMps = 244.0;
        aeroExits.Land = Flight(); aeroExits.Land.AltitudeM = 1200.0;
        GateCheck("AeroDescent -> LandingBurn at the ignition altitude",
                   BoosterDescent.Guide(aeroExits, BoosterPhase.AeroDescent));

        // ---- LandingBurn: lit while staying, and lit while exiting at touchdown ----
        BoosterInputs lbStays = Booster(TargetMode.Asds);
        lbStays.AltitudeM = 400.0; lbStays.DescentSpeedMps = 50.0;
        lbStays.Land = Flight(); lbStays.Land.AltitudeM = 400.0; lbStays.Land.DescentSpeedMps = 50.0;
        GateCheck("LandingBurn, still hoverslamming", BoosterDescent.Guide(lbStays, BoosterPhase.LandingBurn));

        BoosterInputs lbExits = lbStays;
        lbExits.AltitudeM = 0.5; lbExits.DescentSpeedMps = 1.0;
        lbExits.Land.AltitudeM = 0.5; lbExits.Land.DescentSpeedMps = 1.0;
        GateCheck("LandingBurn -> Landed at touchdown", BoosterDescent.Guide(lbExits, BoosterPhase.LandingBurn));

        // ---- Landed: the terminal default branch, never lights anything ----
        BoosterInputs landed = Booster(TargetMode.Asds);
        landed.AltitudeM = 0.0; landed.DescentSpeedMps = 0.0;
        GateCheck("Landed (terminal)", BoosterDescent.Guide(landed, BoosterPhase.Landed));

        // ---- OCT6: THE WHOLE LANDING BURN, ACROSS THE SHED ----
        LandingBurnWalkChecks();
    }

    // =====================================================================================
    // OCT6 (2026-09-05) — A WHOLE LANDING BURN, TICK BY TICK, ACROSS THE SHED.
    // =====================================================================================
    // Every check above drives `Guide()` at a single hand-placed state. The 3→1 shed is not a state, it
    // is a TRANSITION between two of them, and the thing that can go wrong with it — chatter — only
    // exists across ticks. So this closes the loop: `Guide()` flies the stage, the stage's own response
    // to the commanded bank and throttle feeds the next tick's inputs, and the carried state (phase,
    // throttle, shed latch) is handed back exactly as `src/BoosterHost.cs` hands it back.
    //
    // ⚠ The integrator is the SAME arithmetic `Hoverslam` uses (gravity, v-squared drag against a
    // terminal speed, thrust) at a coarse fixed step. It is a TEST HARNESS for the command sequence,
    // NOT a flight model and NOT evidence about the vehicle: the banks are the property fixture from
    // `EnginesForChecks`, and the file header's warning about anchors applies to every number here.
    struct BurnWalk
    {
        public int Ticks;
        public bool SawThree, SawCentre, ThreeAfterCentre, EverIllegal;
        public string FirstIllegal;
        public int Sheds;
        // OCT9 — the margin is measured as a DELAY, not an early shed. `IgnitionAltitude` grows with a
        // wider spool, so the margined criterion is HARDER to satisfy at any given (v, altitude) than the
        // raw one — the FSM holds `ThreeLanding` (which has plenty of spare deceleration) for LONGER,
        // past the tick the un-margined solver would already have committed to `CenterOnly`, and only
        // sheds once the ramp-honest model says one engine will actually make it. `RawCrossTick` is the
        // first tick the RAW (un-margined) solver would have sheared; `ShedTick` is the tick the FSM
        // actually did. `ShedTick > RawCrossTick` is the margin: extra ticks of full three-engine braking
        // banked before committing to the bank that must then ramp up.
        public int RawCrossTick, ShedTick;
    }

    static BurnWalk FlyLandingBurn(double dt, int maxTicks)
    {
        BurnWalk w = new BurnWalk();
        double v = 244.0;
        double alt = Hoverslam.IgnitionAltitude(Bank(EngCentreAccel, 0.0, v));   // the hand-over altitude
        BoosterPhase phase = BoosterPhase.LandingBurn;
        bool shed = false;
        double lastThrottle = 0.0;

        for (int i = 0; i < maxTicks && alt > 0.0 && phase == BoosterPhase.LandingBurn; i++)
        {
            BoosterInputs b = Booster(TargetMode.Asds);
            b.AltitudeM = alt; b.DescentSpeedMps = v;
            b.SurfaceVelocity = new Vec3(-v, 5.0, 0.0);      // descending along -Up, a little downrange
            b.SpeedMps = Math.Sqrt(v * v + 25.0);
            b.Land = Bank(EngCentreAccel, alt, v);
            b.LandThree = Bank(EngThreeAccel, alt, v);
            b.LandingShedLatched = shed;
            b.CommandedThrottle = lastThrottle;
            b.DtS = dt;                                      // ⛔ a real clock: the spool ramp is LIVE,
                                                             // which is what makes the swap transient real
            BoosterCommand c = BoosterDescent.Guide(b, phase);
            w.Ticks++;

            EngineRole role = BoosterHostPlan.CommandedRole(c.EnginesLit, c.EngineMode);
            if (!BoosterHostPlan.PhaseAllows(c.Phase, role, c.LandingShedLatched))
            {
                if (!w.EverIllegal)
                    w.FirstIllegal = "tick " + w.Ticks + " alt=" + alt.ToString("F0") + " v=" + v.ToString("F0")
                                     + " phase=" + c.Phase + " role=" + role + " shed=" + c.LandingShedLatched;
                w.EverIllegal = true;
            }

            // OCT9 — `b.Land`/`b.LandThree` here are the RAW banks this tick built (SpoolS = 0, the same
            // convention `BoosterHost` feeds), so calling `EnginesFor` on them directly is exactly the
            // UN-MARGINED shed test OCT6 shipped. Record the FIRST tick it would already shed, whether or
            // not the (margined) FSM has actually shed yet.
            if (w.RawCrossTick == 0 && Hoverslam.EnginesFor(b.Land, b.LandThree) == 1)
                w.RawCrossTick = w.Ticks;

            bool three = c.EnginesLit && c.EngineMode == VehicleParts.ModeThreeEngine;
            bool centre = c.EnginesLit && c.EngineMode == VehicleParts.ModeCentreOnly;
            if (three && w.SawCentre) w.ThreeAfterCentre = true;
            if (centre && !w.SawCentre && w.SawThree)
            {
                w.Sheds++;
                w.ShedTick = w.Ticks;
            }
            if (three) w.SawThree = true;
            if (centre) w.SawCentre = true;

            double a = 0.0;
            if (three) a = EngThreeAccel * c.Throttle;
            else if (centre) a = EngCentreAccel * c.Throttle;
            double drag = EngG * (v * v) / (EngVterm * EngVterm);
            v += (EngG - drag - a) * dt;
            if (v < 0.0) v = 0.0;
            alt -= v * dt;

            phase = c.Phase;
            shed = c.LandingShedLatched;
            lastThrottle = c.Throttle;
        }
        return w;
    }

    static void LandingBurnWalkChecks()
    {
        BurnWalk w = FlyLandingBurn(0.1, 4000);

        Check("OCT6: the whole landing burn actually ran (not a one-tick walk)", w.Ticks > 20, "ticks=" + w.Ticks);
        Check("OCT6: the burn LIT THREE ENGINES (the owner's ruling, flown)", w.SawThree, "");
        Check("OCT6: the burn SHED to the centre engine", w.SawCentre, "");
        Check("OCT6: it shed exactly ONCE", w.Sheds == 1, "sheds=" + w.Sheds);
        Check("OCT6: every command the whole burn returned is legal under the phase gate",
              !w.EverIllegal, w.FirstIllegal);
        // ⛔ THE MUTATION TARGET. Remove the latch in `BoosterDescent`'s LandingBurn case and this is the
        // check that goes red: `EnginesFor` re-demands three engines during the swap transient, because
        // the spool ramp means the newly-selected centre bank is not yet at the thrust the solve assumed.
        Check("OCT6: the bank NEVER returns to ThreeLanding after shedding — the latch holds across ticks",
              !w.ThreeAfterCentre, "");
        // ⛔ THE OCT9 MUTATION TARGET. Drop the `landForShed.SpoolS` line in `BoosterDescent`'s LandingBurn
        // case (call `Hoverslam.EnginesFor(s.Land, s.LandThree)` directly again) and this goes red: the
        // FSM then sheds on EXACTLY the tick the raw solver first crosses (`ShedTick == RawCrossTick`,
        // the zero-margin behaviour OCT9 was opened against), instead of banking extra three-engine
        // braking time first.
        Check("OCT9: the shed is HELD BACK past the un-margined boundary (extra 3-engine braking banked)",
              w.Sheds == 1 && w.RawCrossTick > 0 && w.ShedTick > w.RawCrossTick,
              "shedTick=" + w.ShedTick + " rawCrossTick=" + w.RawCrossTick);
    }

    // =====================================================================================
    static Vec3 Retro(BoosterInputs b) { return (-b.SurfaceVelocity).Normalized; }

    static BoosterInputs Booster(TargetMode mode)
    {
        BoosterInputs b = new BoosterInputs();
        b.Valid = true;
        b.Up = new Vec3(1, 0, 0);
        b.SurfaceVelocity = new Vec3(-200, 50, 0);   // descending 200 m/s, 50 downrange
        b.AltitudeM = 5000.0; b.SpeedMps = 206.0; b.DescentSpeedMps = 200.0;
        b.AllNominal = true; b.OffsetToMissM = 0;
        b.Fin = new GridFinInputs { AoaMaxDeg = 20.0, GainDegPerKm = 4.0, LeadTauS = 3.0 };
        b.Land = new HoverslamInputs {
            AltitudeM = 5000.0, DescentSpeedMps = 200.0, ThrustAccelMps2 = 71.8,
            GravityMps2 = 9.8, TerminalSpeedMps = 244.0, DeadTimeS = 6.0, SpoolS = 0.0 };

        b.Profile = BoosterProfile.For(mode);
        b.Ullaged = true;                            // settled unless a check says otherwise
        b.TargetBearing = new Vec3(0, 1, 0);         // horizontal (⟂ Up), opposite the horizontal retrograde
        b.Facing = (-b.SurfaceVelocity).Normalized;
        b.DtS = 0.0;                                 // no clock by default: flip snaps, throttle unramped
        return b;
    }
}
