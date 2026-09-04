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
//   · the NON-PORTS: the landing burn never commands the 3-engine set (no 3→1 handover).
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

        Check("the landing burn is the SINGLE CENTRE engine",
              lb.EnginesLit && lb.EngineMode == VehicleParts.ModeCentreOnly, "mode=" + lb.EngineMode);
        Check("...never below the engine's own measured minimum throttle (§B16.3: no zero mid-burn)",
              lb.Throttle >= BoosterDescent.MinThrottleCentreOnly - 1e-12, lb.Throttle.ToString("F6"));
        Check("legs deploy in the final hundreds of metres", lb.DeployLegs, "");
        Check("the fins are still out", lb.DeployFins, "");

        // ⛔ THE NON-PORT: no 3→1 handover. Each engineID set carries ONE ignition (craftdump).
        bool everThree = false;
        for (double a = 3000.0; a >= 1.0; a -= 7.0)
        {
            BoosterInputs w = Booster(TargetMode.Asds);
            w.AltitudeM = a; w.DescentSpeedMps = 40.0 + a / 20.0;
            w.Land = Flight(); w.Land.AltitudeM = a; w.Land.DescentSpeedMps = w.DescentSpeedMps;
            BoosterCommand wc = BoosterDescent.Guide(w, BoosterPhase.LandingBurn);
            if (wc.EnginesLit && wc.EngineMode == VehicleParts.ModeThreeEngine) everThree = true;
        }
        Check("the landing burn NEVER steps back to the 3-engine set (no 3→1 handover — ignitions = 1)",
              !everThree, "");
        Check("the entry burn and the landing burn are DISTINCT ignitions",
              VehicleParts.ModeThreeEngine != VehicleParts.ModeCentreOnly, "");

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
