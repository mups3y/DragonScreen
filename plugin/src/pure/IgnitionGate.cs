// DragonScreen — IgnitionGate  (PURE: the clamp-release + ullage-settle decisions)
// ============================================================================================
// ✅✅ **REVIEWED AND CLOSED ON THE OWNER'S RULING OF 2026-09-05 (register W34).** ✅✅
// **Both of W5's defects are CLOSED AS THEORETICAL. This file is no longer an open defect, and nothing
// here warns a reader off wiring the ullage source in — because wiring it is now CORRECT (W34 did it).**
// ============================================================================================
// 🟢 **THE RULING, VERBATIM, AND THE QUESTION IT ANSWERED — because "1" alone is meaningless.**
// Asked about W5's two defects below, the owner said: **"if you use the three engine mods correctly it
// will not be an issue"**. Correcting a step-up-through-the-banks misreading, he added: **"no 3-1"**.
// Then, asked to choose between —
//   **(1)** leave the gate alone — the profile handles it, W5's defects are theoretical, close them as such
//   **(2)** fix the gate, don't touch the profile
// — he answered **"1"**.
// ⛔ **THE OVERSEER RECOMMENDED (2) AND WAS OVERRULED.** Recorded here on purpose: a closure that hides
// the disagreement is worth less than one that shows it. **This is a DECISION, not a discovery.** Nothing
// below was found to be factually wrong; the ruling is that the exposure is not worth paying to remove.
//
// ============================================================================================
// ⭐ WHY IT IS DEFENSIBLE — the substance of the closure, not a rubber stamp
// ============================================================================================
//  1. **The hoverslam engine is NEVER COLD-STARTED.** The 3→1 landing profile (OCT6, owner-ruled) has the
//     centre nozzle already burning as one of `ThreeLanding` when the shed happens, so the engine that
//     must work at 100 m is never asked to IGNITE there. Every remaining cold light — boostback, entry
//     burn, landing-burn start — is HIGH, with altitude in hand and RCS settling time available. An
//     ullage misjudgement at those altitudes costs a wait, not the vehicle.
//  2. ⚠ **THE ONE MECHANICAL POINT THAT MUST NOT BE MISREAD — the shed is NOT an ullage event.** Because
//     the three banks are **NESTED SUBSETS OF THE SAME NINE NOZZLES** (the geometry block in
//     `pure/BoosterHostPlan.cs` §4c: the centre nozzle belongs to BOTH `ThreeLanding` and `CenterOnly`,
//     so the two can never burn together), `SelectEngineSet` is **FORCED** to shut `ThreeLanding` before
//     activating `CenterOnly`. The shed is therefore technically a shut-then-light — but it is a **SINGLE
//     FRAME**, and settled propellant does not migrate in ~20 ms. **State it; do not treat it as an
//     ullage event, and do not "fix" it later on the belief that it is one.** (Verified by the overseer,
//     2026-09-05; the ordering itself is pinned in `test/BoosterHostTest.cs`'s transition table.)
//  3. **DEFECT 1 is not merely improbable — TODAY IT IS UNREACHABLE.** W34 verified it: `UllageReady` has
//     **NO CALLER** anywhere in `plugin/src` outside this file. The FSM consumes the plain bool
//     `BoosterInputs.Ullaged` (`pure/BoosterDescent.cs:821`, `:860`, `:996`); nothing supplies a
//     `maxSettleS`, so the backstop disjunction cannot fire on any code path that exists. That is a
//     stronger statement than "theoretical" and a weaker one than "fixed" — it is exactly true, and it
//     stops being true the moment somebody routes the FSM through `UllageReady`.
//
// ============================================================================================
// ⚠⚠ WHAT WOULD REOPEN THIS — **[[BB8]]**. Name it, because a closure without its reopening condition
// is how a settled decision quietly becomes a wrong one.
// ============================================================================================
// The ruling rests on the exposure being **CHEAP**: a wrong "settled" spends an ignition on a light
// RealFuels will refuse, and §B16.4 puts `TestFlightFailure_IgnitionFail` on this very part. That is
// affordable only while the ignition budget is effectively unlimited — which nobody has measured.
// **[[BB8]] measures `ignitions` per octaweb bank IN FLIGHT and is still TODO** (the install's own cfg
// sets `%ignitions = -1`, unlimited; only a PRELAUNCH pad read ever returned 1).
// ⇒ **If BB8 comes back FINITE, DEFECT 1's timeout backstop spending an ignition is a LIVE COST again,
// and this ruling deserves a fresh owner look** — together with DEFECT 2's fail-open reader, which W34
// put into the flight path on the strength of the same "it is cheap" argument.
//
// ============================================================================================
// ⛔ C1.16 — **STRUCK, NOT DELETED.** Everything from here to the `END OF STRUCK TEXT` rule below is W5's
// ORIGINAL ANALYSIS, kept **VERBATIM** and struck rather than removed: if a flight ever contradicts the
// ruling above, this analysis is the fastest way back to the answer, and re-earning it costs more than
// keeping it. Read every line between the two rules as **struck through** — it is a record of a closed
// question, not a live warning. Only the two banner lines that called it OPEN were changed.
// ============================================================================================
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~ BEGIN STRUCK TEXT (W5, 2026-09-05) ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
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
//     ⤷ ✅ **SUPERSEDED BY W34, 2026-09-05.** The seam is armed; `bi.Ullaged` can now be true and the FSM
//       can command a light. `UllageReady` itself still has no caller — see point 3 above.
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
// ✅ **DEFECT 1 — CLOSED AS THEORETICAL, owner ruling 2026-09-05 (W34). The heading and the whole
// analysis below it are STRUCK, kept verbatim. Reopens on [[BB8]] — see the banner at the top.**
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
// ✅ **DEFECT 2 — CLOSED AS THEORETICAL, owner ruling 2026-09-05 (W34). The heading and the whole
// analysis below it are STRUCK, kept verbatim. The reader was wired in AS-IS, fail-open included;
// that was the ruling, not an oversight. Reopens on [[BB8]] — see the banner at the top.**
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
//     ⤶ ✅ **OVERRULED BY THE OWNER, 2026-09-05 (W34):** option (1), leave the gate alone. `Ullage`
//       does NOT gain a three-state answer, and `BoosterHost.UllageSettled` WAS wired — to this same
//       fail-open reader, deliberately and unmodified. The proposal is kept because it is the fix
//       that would be applied if [[BB8]] ever makes the exposure expensive.
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~ END OF STRUCK TEXT (W5, 2026-09-05) ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
// Everything BELOW this rule is LIVE and unstruck.
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
        // ✅ **W34, 2026-09-05: CLOSED AS THEORETICAL on the owner's ruling ("1" — leave the gate alone).**
        //    Left in place, now deliberately rather than provisionally. Two facts make it cheap and both
        //    are verifiable from the tree: (a) **this method has NO CALLER** outside its own tests — the
        //    FSM gates on the plain bool `BoosterInputs.Ullaged`, and nothing supplies a `maxSettleS`;
        //    (b) the 3→1 profile never cold-starts the hoverslam engine, so no cold light happens low.
        //    ⚠ **Reopens on [[BB8]]:** if the in-flight per-bank `ignitions` count comes back FINITE, the
        //    backstop spending one is a live cost again. Do not "fix" this line without that ruling.
        public static bool UllageReady(double stability, double settledS, double minCoastS, double maxSettleS)
        {
            if (settledS <= minCoastS) return false;
            return stability >= UllageStable || settledS > maxSettleS;
        }
    }
}
