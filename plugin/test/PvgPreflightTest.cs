// PvgPreflightTest — register S214. The two preconditions that must hold before PVG is handed the
// vehicle, and the ONE gate that stops an octaweb being lit into a commanded zero.
//
// ---- WHAT THIS SUITE IS FOR ----
// The flight of 2026-09-07 sat on the pad. `AscentBuilder.Build()` threw on `_phases[0]` because
// MechJeb's stage table was empty, so no guidance solution ever existed, so nothing raised the
// throttle, so the stage made 0 of 8227 kN and `IgnitionGate` correctly safed the pad. This suite
// covers everything about that which is decidable without the game:
//   • the inclination DOMAIN, proven against out-of-domain values that MUST be rejected;
//   • the phase-table mirror, against the vendored loop's own break/skip rules;
//   • that a launch GO with no guidance solution HOLDS at Idle and never reaches Ignition.
//
// ---- ⛔ THE S167 RULE, WHICH IS WHY THE DOMAIN CASES ARE WRITTEN THE WAY THEY ARE ----
// "A test that cannot fail on an out-of-domain value is not evidence." Every domain case below feeds a
// value that MUST be rejected and asserts it IS. `MutationProof` then goes further and states, as an
// executable assertion, that the predicate is not a constant in either direction — so the two ways of
// faking this test (`return true;` and `return false;`) are both killed by this suite, by name.
//
// ---- ⚠ THE LITERAL PIN ----
// `MaxInclinationDeg` is written out by hand as 180.0 below, once, so a suite derived from the
// constant cannot fail to notice the constant changing.
using System;
using DragonScreen;

public static class PvgPreflightTest
{
    static int checks = 0, failures = 0;
    static void Check(bool ok, string what)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL: " + what); } }

    public static int Run()
    {
        Console.WriteLine("PvgPreflightTest (S214) — the PVG preconditions");
        checks = 0; failures = 0;

        InclinationDomain();
        MutationProof();
        PhaseTableMirror();
        NoSolutionHoldsThePad();

        Console.WriteLine("  " + checks + " checks, " + failures + " failures");
        return failures;
    }

    // ── 1. the inclination domain ───────────────────────────────────────────────────────
    static void InclinationDomain()
    {
        // THE LITERAL PIN — 180.0 by hand, not read off the constant.
        Check(PvgPreflight.MaxInclinationDeg == 180.0,
              "the domain edge is 180°, the definition of an orbital inclination");

        // ⛔ THE CASES THAT MUST BE REJECTED. Without these this suite proves nothing (S167).
        Check(!PvgPreflight.InclinationInDomain(double.NaN),           "NaN is out of domain");
        Check(!PvgPreflight.InclinationInDomain(double.PositiveInfinity), "+∞ is out of domain");
        Check(!PvgPreflight.InclinationInDomain(double.NegativeInfinity), "-∞ is out of domain");
        Check(!PvgPreflight.InclinationInDomain(180.001),   "just over +180° is out of domain");
        Check(!PvgPreflight.InclinationInDomain(-180.001),  "just under -180° is out of domain");
        Check(!PvgPreflight.InclinationInDomain(1e9),       "a runaway magnitude is out of domain");
        Check(!PvgPreflight.InclinationInDomain(-1e9),      "a runaway negative magnitude is out of domain");
        Check(!PvgPreflight.InclinationInDomain(360.0),     "360° is out of domain, not a wrapped 0°");

        // ⭐ THE CASES THAT MUST BE ACCEPTED — and the SIGN is the point of the middle two.
        // `pure/PvgPreflight.cs` §2 proves all seven vendored terminals take `Abs(_incT)`, so a
        // negative inclination is MechJeb's own azimuth convention and NOT a defect. The flight's
        // `-51.6°` belongs in this list, and putting it here is the disproof of S214's prime suspect.
        Check(PvgPreflight.InclinationInDomain(51.6),   "+51.6° (the catalog's ISS inclination) is in domain");
        Check(PvgPreflight.InclinationInDomain(-51.6),  "⭐ -51.6° — THE FLOWN VALUE — is in domain, sign and all");
        Check(PvgPreflight.InclinationInDomain(-51.6316), "the shipped cfg's -51.6316 is in domain");
        Check(PvgPreflight.InclinationInDomain(0.0),    "0° (equatorial) is in domain");
        Check(PvgPreflight.InclinationInDomain(180.0),  "180° (retrograde equatorial) is the inclusive edge");
        Check(PvgPreflight.InclinationInDomain(-180.0), "-180° is the inclusive edge on the other side");
        Check(PvgPreflight.InclinationInDomain(97.4),   "a sun-synchronous inclination is in domain");
    }

    // ── 2. the predicate is not a constant ──────────────────────────────────────────────
    // ⭐ S167 made executable. If somebody replaces the body of `InclinationInDomain` with
    // `return true;` the first line fails; with `return false;` the second does. Stated here as its
    // own named check so the protection is visible rather than emergent.
    static void MutationProof()
    {
        bool rejectsSomething = !PvgPreflight.InclinationInDomain(double.NaN)
                             && !PvgPreflight.InclinationInDomain(1e9);
        bool acceptsSomething = PvgPreflight.InclinationInDomain(-51.6)
                             && PvgPreflight.InclinationInDomain(51.6);
        Check(rejectsSomething, "MUTATION: the domain predicate cannot be `return true;`");
        Check(acceptsSomething, "MUTATION: the domain predicate cannot be `return false;`");

        // The same, for the phase-table mirror.
        Check(!PvgPreflight.WouldBuildAPhase(new int[0], new double[0], -1, 0.0),
              "MUTATION: the phase mirror cannot be `return true;` (an empty table builds nothing)");
        Check(PvgPreflight.WouldBuildAPhase(new int[] { 0 }, new double[] { 3000.0 }, -1, 20.0),
              "MUTATION: the phase mirror cannot be `return false;` (a real stage builds)");
    }

    // ── 3. the phase-table mirror ───────────────────────────────────────────────────────
    static void PhaseTableMirror()
    {
        // ⛔ THE DEFECT ITSELF: an empty stage table is what `Build()` threw on. This one case is the
        // whole of S214's root cause, expressed as an assertion.
        Check(!PvgPreflight.WouldBuildAPhase(new int[0], new double[0], -1, 20.0),
              "⭐ AN EMPTY STAGE TABLE BUILDS NOTHING — the 2026-09-07 defect, asserted");
        Check(!PvgPreflight.WouldBuildAPhase(null, null, -1, 20.0),
              "a null stage table builds nothing and does not throw");

        // A normal two-stage Falcon: mjPhase 0 is the current (lowest) stage, KSPStage counts down as
        // the index rises — the ordering the vendored loop walks backwards through.
        int[] stages = new int[] { 2, 1, 0 };
        double[] dv  = new double[] { 3000.0, 4000.0, 1500.0 };
        Check(PvgPreflight.WouldBuildAPhase(stages, dv, -1, 20.0),
              "a normal multi-stage table builds at least one phase");

        // The source's SKIP: `if (fuelStats.DeltaV < MinDeltaV) continue;` — sep motors are skipped.
        Check(!PvgPreflight.WouldBuildAPhase(new int[] { 2, 1, 0 },
                                             new double[] { 5.0, 5.0, 5.0 }, -1, 20.0),
              "a table of nothing but sub-MinDeltaV sep motors builds nothing (the source's `continue`)");
        Check(PvgPreflight.WouldBuildAPhase(new int[] { 2, 1, 0 },
                                            new double[] { 5.0, 4000.0, 5.0 }, -1, 20.0),
              "one real stage among sep motors is enough");

        // The source's BREAK: `if (kspStage < LastStage) break;` — walked from the END, so a LastStage
        // above every row stops the walk on its first iteration and nothing is added.
        Check(!PvgPreflight.WouldBuildAPhase(new int[] { 2, 1, 0 },
                                             new double[] { 3000.0, 4000.0, 1500.0 }, 5, 20.0),
              "a LastStage above every row breaks out immediately and builds nothing (the source's `break`)");
        Check(PvgPreflight.WouldBuildAPhase(new int[] { 2, 1, 0 },
                                            new double[] { 3000.0, 4000.0, 1500.0 }, 0, 20.0),
              "a LastStage the rows satisfy still builds");

        // ⚠ THE BREAK IS A BREAK, NOT A CONTINUE — and this case is the one that tells them apart.
        // Walking backwards, row index 2 (KSPStage 0) is met FIRST. With LastStage 2 it breaks there,
        // so the rows below it are never reached even though row 0 (KSPStage 2) would have qualified.
        // A `continue` would have found it and wrongly returned true.
        Check(!PvgPreflight.WouldBuildAPhase(new int[] { 2, 1, 0 },
                                             new double[] { 3000.0, 3000.0, 3000.0 }, 2, 20.0),
              "⚠ the mirror BREAKS like the source, and does not fall through to a later qualifying row");

        // Ragged input must not throw — the caller builds both arrays, but a mirror that indexes past
        // the shorter one would be a crash on the pad.
        Check(PvgPreflight.WouldBuildAPhase(new int[] { 0, 0, 0 }, new double[] { 3000.0 }, -1, 20.0),
              "ragged arrays are walked to the shorter length rather than throwing");
    }

    // ── 4. the pad hold ─────────────────────────────────────────────────────────────────
    static void NoSolutionHoldsThePad()
    {
        // ⭐ THE FIX, ASSERTED. A launch GO with no guidance solution must NOT light the octaweb.
        AscentInputs s = AscentInputs.Nominal();
        s.LaunchCommanded = true;
        s.GuidanceReady   = false;          // set EXPLICITLY — never relying on the default
        AscentDecision d = AscentSequence.Step(s, AscentStep.Idle);
        Check(d.Next == AscentStep.Idle && d.Act == AscentAct.None,
              "⭐ launch GO with NO guidance solution HOLDS at Idle and commands nothing (got " + d + ")");
        Check(d.Act != AscentAct.IgniteStageOne,
              "⛔ and specifically does not light the octaweb into a commanded zero");
        Check(!string.IsNullOrEmpty(d.Reason), "the hold says why");

        // And with a solution it goes, exactly as before — the gate adds a precondition, it does not
        // replace the launch GO.
        s.GuidanceReady = true;
        d = AscentSequence.Step(s, AscentStep.Idle);
        Check(d.Next == AscentStep.Ignition && d.Act == AscentAct.IgniteStageOne,
              "with a guidance solution the launch GO lights the octaweb (got " + d + ")");

        // ⛔ AND THE GUIDANCE FLAG IS NOT A SUBSTITUTE FOR THE CREW'S GO. Both are required; neither
        // alone may light a stage.
        AscentInputs noGo = AscentInputs.Nominal();
        noGo.LaunchCommanded = false;
        noGo.GuidanceReady   = true;
        d = AscentSequence.Step(noGo, AscentStep.Idle);
        Check(d.Next == AscentStep.Idle && d.Act == AscentAct.None,
              "a guidance solution WITHOUT the crew's GO still lights nothing (got " + d + ")");

        // ⛔ THE IGNITION GATE IS UNTOUCHED. S214 must not have weakened it — once lit, a stage that
        // makes no thrust is still safed with the clamps held. Proven here against the FLOWN numbers:
        // 0 of 8227 kN at 2.0 s+, which is what the pad reported at 20:32:44.703.
        AscentInputs cold = AscentInputs.Nominal();
        cold.S1ThrustN = 0.0; cold.S1MaxThrustN = 8227000.0; cold.S1LitCount = 1;
        cold.SinceStepS = 2.5;
        d = AscentSequence.Step(cold, AscentStep.Ignition);
        Check(d.Next == AscentStep.Safed && d.Act == AscentAct.SafeAbort,
              "⛔ THE 2 s / 99 % GATE IS UNCHANGED — the flown cold-pad case still SAFES (got " + d + ")");

        // and a good light still releases
        AscentInputs good = AscentInputs.Nominal();
        good.S1ThrustN = 8227000.0; good.S1MaxThrustN = 8227000.0; good.S1LitCount = 1;
        good.SinceStepS = 1.0;
        d = AscentSequence.Step(good, AscentStep.Ignition);
        Check(d.Next == AscentStep.Liftoff && d.Act == AscentAct.ReleaseHoldDowns,
              "a stage at full thrust still releases the hold-downs (got " + d + ")");
    }
}
