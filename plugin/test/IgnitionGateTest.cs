/*
 * Tests for the clamp-release + ullage-settle gates (pure/IgnitionGate.cs).
 *
 * These pin the two safety decisions the proving flight depends on: never release the hold-downs onto an
 * engine that hasn't reached full thrust (flight-1/2 RUD), and never light an engine before the RealFuels
 * propellant is settled (a failed relight has no retry).
 *
 * ============================================================================================
 * ⛔ W5 (2026-09-05) — RESTORED FROM `8b81816^` (2,944 B, R1 §5.3's row) AND EXTENDED WITH A **DEFECT**
 *    GROUP THAT ASSERTS THE **WRONG** BEHAVIOUR ON PURPOSE.
 * ============================================================================================
 * The suite above the DEFECT group is byte-for-byte the restored one; nothing in it was weakened, and
 * the gate itself is unchanged (`pure/IgnitionGate.cs`, comment-stripped diff against `8b81816^`:
 * identical). What is new is `DefectPins()`, which PINS the failing case rather than fixing it, exactly
 * as W5's brief requires: a known-defective file comes back stating its defect, with a test that fails
 * the day someone changes the behaviour — so the change has to be a DELIBERATE one that also updates
 * this file, and can never be a quiet slip inside another task's diff (§B12.8 rider (b)).
 *
 * ⚠ **WHAT THIS SUITE CAN AND CANNOT REACH.** `build.py test` compiles `src/pure` + `test` into the
 * headless exe (`build.py:334`); `src/Ullage.cs` is GLUE and is compiled only into the plugin DLL
 * (`build.py:317`, against the KSP references) — it is never linked here and its `ModuleEngines`
 * parameter does not exist in this exe. **So DEFECT 1 (the pure half) is pinned executably below;
 * DEFECT 2 (`Ullage.Stability`'s seven fail-open returns) is a code-reading finding this suite is
 * STRUCTURALLY unable to reach, and only the compile proves it builds.** Stated rather than implied.
 */
using DragonScreen;
using System;

public static class IgnitionGateTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen ignition-gate (clamp release + ullage) tests");

        const double AVAIL = 6.68e6;   // octaweb AllEngines ~6681 kN

        // ---- clamp release ----
        Check("hold below 99% thrust",
              IgnitionGate.Evaluate(0.90 * AVAIL, AVAIL, 1, 0.3) == ClampAction.Hold, "");
        Check("release at 99% thrust, engine lit",
              IgnitionGate.Evaluate(0.99 * AVAIL, AVAIL, 1, 0.3) == ClampAction.Release, "");
        Check("release at full thrust",
              IgnitionGate.Evaluate(AVAIL, AVAIL, 1, 0.5) == ClampAction.Release, "");
        Check("do NOT release with no engine lit (litCount 0)",
              IgnitionGate.Evaluate(AVAIL, AVAIL, 0, 0.3) != ClampAction.Release, "");
        Check("do NOT release with no available thrust",
              IgnitionGate.Evaluate(0, 0, 0, 0.3) != ClampAction.Release, "");
        Check("still holding just before timeout",
              IgnitionGate.Evaluate(0.5 * AVAIL, AVAIL, 1, IgnitionGate.MaxHoldS - 0.1) == ClampAction.Hold, "");
        Check("SAFE-ABORT after timeout with thrust still low (failed light)",
              IgnitionGate.Evaluate(0.5 * AVAIL, AVAIL, 1, IgnitionGate.MaxHoldS + 0.1) == ClampAction.SafeAbort, "");
        Check("thrust reaching 99% even at timeout still RELEASES (not abort)",
              IgnitionGate.Evaluate(AVAIL, AVAIL, 1, IgnitionGate.MaxHoldS + 1.0) == ClampAction.Release, "");

        // ---- ullage settle ----
        Check("not ready during the minimum separation coast",
              !IgnitionGate.UllageReady(1.0, 0.5, 1.0, 6.0), "");
        Check("ready once past min coast AND settled",
              IgnitionGate.UllageReady(0.999, 1.5, 1.0, 6.0), "");
        Check("not ready past min coast but still unsettled",
              !IgnitionGate.UllageReady(0.80, 2.0, 1.0, 6.0), "");
        Check("ready at the backstop even if never settled",
              IgnitionGate.UllageReady(0.80, 6.5, 1.0, 6.0), "");
        Check("exactly at the 0.996 threshold counts as settled",
              IgnitionGate.UllageReady(IgnitionGate.UllageStable, 1.5, 1.0, 6.0), "");

        DefectPins();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // =============================================================================================
    // ⛔ THE DEFECT GROUP — every check here asserts behaviour W5 believes is WRONG.
    // =============================================================================================
    // A green result here does NOT mean the gate is correct. It means the gate still behaves the way it
    // behaved on the flight that lost the booster, which is what an open defect is supposed to do until
    // somebody with the authority to change it does so. **If one of these turns red, that is the signal
    // to read `pure/IgnitionGate.cs`'s banner and W5's register line before "fixing" the test** — the
    // proposed fix is written down there and it is an owner call (C1.12), not a test repair.
    static void DefectPins()
    {
        // ---- DEFECT 1: the `maxSettleS` backstop authorises a light into UNSETTLED propellant. ------
        // The whole point is that the answer does not depend on `stability` once the backstop is passed.
        // Sweep the stability range from "empty tank end of the scale" to "just under the threshold" and
        // show every one of them is authorised. A fix that makes the backstop conditional on stability,
        // or that turns it into a distinct third verdict, turns this group red — deliberately.
        double[] hopeless = { 0.0, 0.10, 0.50, 0.80, 0.95, IgnitionGate.UllageStable - 1e-9 };
        for (int i = 0; i < hopeless.Length; i++)
        {
            Check("DEFECT 1 — backstop authorises a light at stability " + hopeless[i].ToString("0.######")
                  + " (WRONG; pinned, not fixed)",
                  IgnitionGate.UllageReady(hopeless[i], 6.5, 1.0, 6.0), "");
        }

        // The defect is the DISJUNCTION, so prove the two terms are genuinely independent: identical
        // stability, one tick either side of the backstop, opposite answers. That is the whole bug in
        // two lines — nothing about the propellant changed between them.
        Check("DEFECT 1 — same stability 0.80, just BEFORE the backstop: correctly refused",
              !IgnitionGate.UllageReady(0.80, 5.9, 1.0, 6.0), "");
        Check("DEFECT 1 — same stability 0.80, just AFTER the backstop: authorised anyway (WRONG)",
              IgnitionGate.UllageReady(0.80, 6.1, 1.0, 6.0), "");

        // ⚠ AND THE ONE GUARD THAT IS **NOT** DEFECTIVE — pinned so a fix to the above cannot quietly
        // take it with it. The minimum separation coast is checked FIRST and unconditionally, so the
        // backstop can never authorise a light before the spent stage has physically cleared.
        Check("min-coast refusal outranks the backstop (correct — pinned against collateral damage)",
              !IgnitionGate.UllageReady(0.0, 0.5, 1.0, 0.1), "");
        Check("min-coast refusal outranks a SETTLED reading too (correct)",
              !IgnitionGate.UllageReady(1.0, 0.9, 1.0, 6.0), "");

        // ---- DEFECT 2: `stability` cannot express "unknown" — pinned at the boundary it is felt. -----
        // `src/Ullage.cs` returns 1.0 on all seven of its failure paths, so "measured and settled" and
        // "nothing was measured" arrive here as the SAME number and produce the SAME verdict. This
        // suite cannot call `Ullage` (glue — see the file header), so what is pinned is the property
        // that makes the fail-open reachable: a bare 1.0 is accepted as proof, with no corroboration
        // asked for and no way to supply any.
        Check("DEFECT 2 — a bare 1.0 is authorised with no way to say where it came from (WRONG)",
              IgnitionGate.UllageReady(1.0, 1.5, 1.0, 6.0), "");
        Check("DEFECT 2 — and it is indistinguishable from a genuine measured 1.0",
              IgnitionGate.UllageReady(1.0, 1.5, 1.0, 6.0) == IgnitionGate.UllageReady(1.0, 1.5, 1.0, 6.0), "");

        // ---- THE RETRY POLICY: W5 answered NO RETRY, so pin that NOTHING here counts attempts. ------
        // `UllageReady` is a pure predicate over the CURRENT state — it has no attempt parameter, no
        // memory and no notion of a previous failed light, and after W5 it still has none. If a retry is
        // ever directed by the owner, this check is the tripwire that says the shape changed.
        Check("NO RETRY — the gate is stateless: identical inputs give identical answers, always",
              IgnitionGate.UllageReady(0.80, 2.0, 1.0, 6.0) == IgnitionGate.UllageReady(0.80, 2.0, 1.0, 6.0)
              && IgnitionGate.Evaluate(0.5 * AvailFixture, AvailFixture, 1, 0.3)
                 == IgnitionGate.Evaluate(0.5 * AvailFixture, AvailFixture, 1, 0.3), "");
    }

    const double AvailFixture = 6.68e6;
}
