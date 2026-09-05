// DragonScreen — IgnitionGate  (PURE: the clamp-release + ullage-settle decisions)
// ============================================================================================
// ⛔⛔ THIS FILE IS RESTORED AS AN **OPEN DEFECT**. IT IS NOT A WORKING PART. ⛔⛔
// ============================================================================================
// Register **W5**, 2026-09-05. Restored from `8b81816^` — 2,502 B, byte-for-byte R1 §5.1's row — with
// **no line of logic changed**. Every edit below this banner is a COMMENT. R1 §7.1 lists this file, with
// `src/Ullage.cs`, under *"directly implicated — a named, located, **UNFIXED** defect"*, and §B12.8
// rider (b) forbids a known-defective file coming back as a quiet fix inside another task's diff. So it
// comes back stating what is wrong with it, pinned by tests that assert the WRONG behaviour on purpose
// (`test/IgnitionGateTest.cs`, the DEFECT group), with the fix **PROPOSED, NOT APPLIED**.
//
// ⚠ **NOTHING CALLS THIS FILE.** `src/BoosterHost.cs`'s `UllageSettled` hook is still null, so the FSM's
// ullage gate is still held CLOSED and no phase commands thrust. W5 deliberately did NOT wire the
// restored reader in: wiring it IS the fix, and the fix is an owner call (see DEFECT 2).
//
// ---- THE FLIGHT ----------------------------------------------------------------------------
// `Crew-2_20260829_144114` — *"Booster engine never lit (eng_ignited=0 whole descent) → ballistic →
// LOST @14 km. Root = RealFuels ullage"* (quoted from `src/BoosterHost.cs`'s own header; the same event
// is `docs/FLIGHT_144114_SCREEN_AUDIT.md:35`, *"Booster ballistic (eng never lit) → LOST (= register
// H1b, ullage)"*, and `docs/INTEGRATION_SCORECARD.md` rows 3-4, *"eng_ignited=0 — engine NEVER
// ignites"* → *"never ignites → ballistic crash @119 m/s"*).
//
// ⚠ **AND THE PROXIMATE ROOT OF THAT FLIGHT IS NOT PROVEN — say so, do not inherit the headline.**
// `docs/ISSUE_REGISTER.md`'s H1b row records the LIKELY root as a **MODE-NUMBER MISMATCH**, found by
// verifying a pushback rather than by measurement: `BoosterDescent.Guide` emitted `EngineMode` 3 for the
// entry burn and 1 for the landing burn while `VehicleParts.EngineIdIsMode` decoded 1=three / 2=centre,
// so the entry burn activated **the AllEngines set that had already spent its ignition at liftoff** and
// RealFuels refused. That row says, in its author's own words, *"NOT proven to be off-focus RF (I
// over-concluded)"*, and it is marked **FIXED — UNFLOWN**. Ullage itself was never measured on that
// booster at all: the recorder sampled only the ACTIVE vessel, so the non-active booster's whole
// 16,139-tick descent carries **no `ullage_stab` and no `eng_ignited` stream** (R1's recorder finding;
// register [[BB8]] for the ignition count).
// ⇒ **This file is implicated, and it is independently defective on its own terms — the two defects
// below are provable from the code alone. It is NOT established as that flight's proximate cause.**
//
// ============================================================================================
// ⛔ DEFECT 1 — `UllageReady`'s BACKSTOP AUTHORISES A LIGHT INTO PROPELLANT IT KNOWS IS UNSETTLED
// ============================================================================================
//     return stability >= UllageStable || settledS > maxSettleS;
//                                      ^^ this OR is the defect
// After `maxSettleS` seconds the gate answers "ready to light" **regardless of stability** — the original
// header below calls it *"a maximum settle time is a best-effort backstop"*, and the restored test
// asserts it as CORRECT behaviour (*"ready at the backstop even if never settled"*, at stability 0.80).
// On a stock build it is harmless: `Ullage.Stability` is 1.0 always, so the first term always wins.
// **On RSS-RO it is the mechanism that throws an ignition away.** RealFuels refuses an ignition into
// unsettled propellant, and §B16.4 records **`TestFlightFailure_IgnitionFail` sitting on this exact
// part** — so an attempt is not free even when it is allowed: it is a consumable spent AND a die rolled.
// "Best effort" on a finite-ignition engine is indistinguishable from discarding the ignition.
// **PROPOSED FIX (not applied):** the backstop must not authorise a light in a regime that models
// ullage. Either drop the disjunction and let the caller decide what to do with a settle that never
// converges — the FSM already has the honest answer, keep raising `UllageRcs` and do not burn — or make
// the backstop a distinct THIRD verdict the caller must handle explicitly, never a plain `true`.
// ⛔ Not applied here: it changes flight behaviour and reverses a stated design intent (C1.12).
//
// ============================================================================================
// ⛔ DEFECT 2 — THE `stability` PARAMETER CANNOT SAY "I DO NOT KNOW", AND ITS SUPPLIER RELIES ON THAT
// ============================================================================================
// `UllageReady` takes a `double stability` and treats it as a MEASUREMENT. Its only supplier,
// `src/Ullage.cs`, returns **1.0 — "fully settled" — on EVERY failure path**: RealFuels not loaded, the
// type lookup failing, a field missing, the engine not being a `ModuleEnginesRF`, a null `ullageSet`, a
// non-`double` return, or any thrown exception. So `1.0` arrives here meaning **either** "measured and
// settled" **or** "nothing was measured at all", and this signature cannot tell the two apart.
// The fail-open is DELIBERATE and correct for the case its own comment names (*"Without RealFuels …
// stability is 1.0 … which is correct for a stock build"*) — and this vehicle does not fly stock.
// **PROPOSED FIX (not applied):** `Ullage` must answer KNOWN-SETTLED / KNOWN-UNSETTLED / UNKNOWN, and
// UNKNOWN must gate CLOSED in an ullage-modelled regime. This is why W5 left `BoosterHost.UllageSettled`
// unwired: wiring today's reader would put a fail-open gate into the flight path, which is exactly the
// quiet restore of a known-defective file that §B12.8 rider (b) forbids.
//
// ============================================================================================
// ⛔ THE RETRY POLICY — [[OCT11]] DECLINED IT AND ASSIGNED IT HERE. W5's ANSWER IS **NO RETRY**.
// ============================================================================================
//  1. **A retry treats the symptom.** The engine is commanded because THIS GATE said "ready", and
//     defects 1 and 2 are the two ways it says "ready" without knowing. Re-issuing `Activate()` re-rolls
//     the same dice on the same information; fixing the gate removes the reason to retry at all.
//  2. **The owner has already ruled on this shape, and nothing has changed since.** [[OCT11]], verbatim:
//     *"OPTION (b): `currentRole` STAYS A COMMAND RECORD. MAKE THE DIVERGENCE VISIBLE. BUILD NO RETRY."*
//     — reasoned on `TestFlightFailure_IgnitionFail` sitting on this part and RealFuels' ignition count
//     being UNMEASURED. W5 owns the POLICY and so may revisit it; it finds the reasoning intact.
//     [[BB8]] is still TODO, the count is still unmeasured, and a bounded retry still spends from a
//     budget nobody has read.
//  3. **"Bounded" cannot be chosen honestly today.** Any `MaxAttempts` would be a number with no
//     measurement behind it — §B16.8 ruling 2 makes it `[UN-CONVERGED]` on sight, and ruling 3 says no
//     task converges one under the preview-only gate. A bound that is invented is not a bound; it is an
//     arbitrary number wearing one.
//  4. **The instrument must fly before the actuator.** [[OCT11]] already made the divergence VISIBLE
//     (`boost.commanded_not_ignited` / `boost.ignition_resolved`, column `boost_cmd_not_ignited`) and
//     [[BB8]] will record the per-bank ignition count. Neither has flown. Building the retry before the
//     instrument that would judge it is the ordering this project has repeatedly found to be wrong.
//  5. **The safe loop already exists, and it is not a retry.** §B16.3 is *"settle propellant with RCS
//     before EVERY relight"*, and the FSM implements exactly that: while `Ullaged` is false no phase
//     commands thrust and `UllageRcs` is raised instead. That WAIT is unbounded in ticks and costs **no
//     ignition**. The unsafe loop is the one that re-commands `Activate()`. We keep the safe one.
// ⇒ **No retry is designed and none is built.** If the owner directs one later, the constraints it must
//    meet are on record: BOUNDED, GATED on settled ullage, refusing outright on an UNSUPPLIED ignition
//    budget (this codebase's `0 = not supplied` convention), and its attempt count reaching the recorder.
// ============================================================================================
//
// ---- THE ORIGINAL HEADER, VERBATIM (C1.16 — reasoning is kept, never replaced) ---------------
// The two go/no-go gates around lighting an engine, decided in pure code so they are headless-tested:
//
//  • CLAMP RELEASE (pad): after the octaweb is commanded lit, HOLD the hold-downs until the measured
//    thrust reaches ≥99% of available with at least one engine lit — then release. If thrust has not
//    come up within MaxHoldS (Merlin spool is INSTANT, so this only trips on a failed light), SAFE-ABORT:
//    shut the engine down and keep the clamps — never release onto a bad engine (the flight-1/2 fix, plan §3.4).
//
//  • ULLAGE SETTLE (every relight in flight): RealFuels needs the propellant settled before ignition. Fire
//    the aft RCS until the ullage stability ≥0.996, then light (MechJeb ProcessUllage, plan §3.3). A minimum
//    coast lets the spent stage physically clear first; a maximum settle time is a best-effort backstop.
// ============================================================================================
namespace DragonScreen
{
    public enum ClampAction { Hold, Release, SafeAbort }

    public static class IgnitionGate
    {
        // ---- clamp release ----
        public const double ReleaseThrustFrac = 0.99;   // release only at ≥99% of available thrust
        public const double MaxHoldS = 2.0;             // spool is instant; still low by now ⇒ a light failed

        public static ClampAction Evaluate(double thrustN, double availableN, int litCount, double heldS)
        {
            if (availableN > 1.0 && litCount >= 1 && thrustN >= ReleaseThrustFrac * availableN)
                return ClampAction.Release;
            if (heldS > MaxHoldS)
                return ClampAction.SafeAbort;           // failed to reach thrust in time — keep clamps, shut down
            return ClampAction.Hold;
        }

        // ---- ullage settle ----
        public const double UllageStable = 0.996;       // RealFuels stability threshold to allow ignition

        // Ready to light: past the minimum separation coast AND (propellant settled OR the settle backstop hit).
        // ⛔ W5 DEFECT 1 lives on the next line — the `||` authorises a light at ANY stability once
        //    `settledS > maxSettleS`. Left in place on purpose; see the banner at the top of this file.
        public static bool UllageReady(double stability, double settledS, double minCoastS, double maxSettleS)
        {
            if (settledS <= minCoastS) return false;
            return stability >= UllageStable || settledS > maxSettleS;
        }
    }
}
