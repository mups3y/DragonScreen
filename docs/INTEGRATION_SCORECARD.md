> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE (RSS-RO)**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-29; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# DragonScreen — Integration Scorecard (C1)

> **C1 evidence artifact.** The longest clean recorded prefix under the now-trustworthy instrument (C0 passed).
> Stitched by wall-clock + KSP.log event order across the multi-recording mission, with `source` provenance.
> Every status cites CSV columns + KSP.log lines. **No diagnosis or fix here** — C1's only job is to make the
> failure legible and name the SINGLE earliest failed exit condition, which becomes C2's sole scope.

## Mission A — the booster-recovery flight (2026-08-29 12:04–12:13, `AutoRecoverBooster` default-ON)
Recordings: `Crew-2_120401` (S1 ascent), `Crew-2_Probe_120640` (booster), `Crew-2_Probe_121216` (fragment). Instrument
sanity: warp handled correctly (high-warp blanked, physics-warp live); `eng_ignited` trustworthy.

| # | Phase | source | Entry condition | Exit condition (designed) | Propellant / mass | Key metric (columns) | Status | Log corroboration |
|---|---|---|---|---|---|---|---|---|
| 1 | **S1 ascent → MECO** | 120401 | liftoff, clamp release @100% thrust (12:04:19) | MECO at staging energy | mass 1,572,589→208,307 kg (S1 spent) | reached MECO; `eng_ignited` 1 at liftoff | ✅ PASS | `octaweb liftoff — 1 lit`, `CLAMP RELEASE 100%`, `MECO — octaweb shut, interstage decoupled` (12:06:40) |
| 2 | **⭐ HAND-OFF: MECO → booster recovery** | log | MECO + `PRE ON` (ranges→600 km) | S2 completes insertion **and** booster recovers | — | focus leaves the S2/Dragon | ❌ **FAIL — earliest** | 12:06:40 `PRE ON`, `focus → separated booster (upper stage coasts, kept loaded by PRE)` |
| 3 | Booster entry burn | 120640 | Flip→EntryBurn (alt 58 km, +1006 m/s, 12:06:40) | decelerate for landing | thrust `thrust_n`=0 throughout | **`eng_ignited`=0 — engine NEVER ignites** | ❌ FAIL | `engine mode → AllEngines / ThreeLanding` but **no "N engine(s) lit"** |
| 4 | Booster landing burn | 120640 | LandingBurn (alt 25 km, −815 m/s, 12:10:55) | v→0 at h=0 (single centre engine) | `eng_ignited`=0, `thrust_n`=0 | **never ignites → ballistic crash @119 m/s** (alt −5 m) | ❌ FAIL | `landing legs extended on 0 modules`; crash |
| 5 | **S2 insertion** | log / 121216 | focus returns to S2 (~12:12) | MVac lit → burn → SECO → orbit | S2 coasted ~6 min on-rails since MECO | **`S2 (MVac) ignition — 0 engine(s) lit`** (repeats) + **FDIR `NoControlSolution → Abort/SafeMode`** | ❌ FAIL | 12:12:15+ `0 engine(s) lit` (×many, now ~2 s apart); `FDIR (observe) NoControlSolution` |
| 6 | Rendezvous / dock / deorbit / entry | — | — | — | — | **not reached** | — | — |

**Instrument validations passed in this flight:** `warp_rate` stamp + on-rails blanking (I1); physics-warp stays live
(Grok's LOW-warp check); `eng_ignited` proves the booster engine never lit (I2); ignition log throttled to ~2 s not
every tick (I3 — works, though the failure persists so it still logs for minutes). FDIR observe-only feed (T2b) fired
correctly — a real validation that the safety instrument works.

## Mission B — SEPARATE deorbit test (`Crew-2_121530`, met 2,327,176 s)
**Not part of Mission A** (MET discontinuity 411 s vs 2.3 M s ⇒ a different vessel/loaded state). Scored standalone:
an orbiting Dragon (ap 410 / pe 114, mass 6,396 kg) ran a `DeorbitReturn` abort (12:xx) but ended at **alt 272 km,
srf 7,436 m/s** — still orbital, so the **deorbit did not complete**. `ABORT mode=DeorbitReturn` logged. Marked
`unstitched — scored from file 121530 only`. Investigate under a later campaign once Mission A's blocker is cleared.

---

## ⭐ THE SINGLE EARLIEST FAILED EXIT CONDITION → C2 SCOPE
**Row 2 — the MECO → booster-recovery hand-off (12:06:40).** Turning booster recovery on switches focus to the
booster at MECO, which breaks the mission on **both** branches that stem from that instant:
- the **S2 insertion** is interrupted and the **MVac cannot relight** after the ~6-min coast (`0 engine(s) lit`),
  with FDIR flagging `NoControlSolution` — so the Dragon never inserts;
- the **booster's own engines never ignite** (`eng_ignited`=0 through entry + landing) → crash at 119 m/s.

Whether the common root is RealFuels **ullage/ignition** (no settle before the light on either vehicle) or the
**orchestration ordering** (recover the booster *after* S2 insertion, not at MECO) is a **C2 diagnosis question** —
NOT decided here. Per the rule, this is the only blocker C2 may scope. Rough cost annotation (secondary, not the
gate): this miss forfeits the entire Dragon mission + the booster, so it dominates.

**Next:** a new §8 State Confirmation scoping C2 = "the MECO booster-recovery hand-off ignition failure" as a single
change class. No prediction/gain/other work until then. (Cross-ref `docs/CAMPAIGN_PLAN.md`, `docs/ISSUE_REGISTER.md`.)
