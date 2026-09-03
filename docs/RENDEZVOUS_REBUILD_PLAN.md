> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — ⛔ never an instruction**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-31; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⛔ Its **own** banner: *"UNDER VERIFICATION — NOT AN INSTRUCTION, NOT APPROVED"*; a 2026-08-31 review found real DEFECTS in it. ⚠ **Named contradiction:** it designs the **hand-written** control loop that was deleted 2026-09-01. Part B builds a **pinned, privately-namespaced MechJeb embed + a pure conductor** (§B1–B16 / T15–T22) instead.

# Rendezvous Rebuild — DESIGN NOTES UNDER VERIFICATION (NOT approved, do not implement)

> **⛔ STATUS: UNDER VERIFICATION — NOT AN INSTRUCTION, NOT APPROVED. A 2026-08-31 review found real DEFECTS in
> this proposal that are being resolved BEFORE any code change (see `FLIGHT_VERIFICATION.md` "Rendezvous rebuild
> verification"):** (1) it wrongly called a lower circular orbit "stable co-elliptic" — it is a PHASING orbit that
> closes along-track (~17 m/s at 10 km below); (2) the removed circularization was not reproduced before proposing
> to restore it; (3) the "100 km CW invalid / 7.5 km correct" hand-off is asserted, not derived from a CW-vs-truth
> bound; (4) the Δv budget contradiction (66 m/s useful vs a 100–200 m/s profile) is UNRESOLVED — it hinges on the
> discrete-burn efficiency, which is UNKNOWN — EVIDENCE REQUIRED; (5) fuel attribution has a sampling caveat;
> (6) the named-burn / 7.5 km / waypoint values need source-confidence labels. **This file is retained as design
> notes only; it does NOT govern anything and MUST NOT be built from until the verification gates pass.** Governing
> plan remains `docs/MASTER_BUILD_SPEC.md` (Phase 0–7 active; the rendezvous work is the recorded Phase-9/12 exception).

## 1. Why (flight‑proven problem — the owner's two reasons, evidence below)
The rendezvous runs the MMH tank dry before docking, on **three** instrumented flights. Root causes, measured:
- **RC1 — constant thruster pulsing.** During the burn/"Phasing" segments the attitude loop holds prograde
  *continuously* — **94% applied attitude duty** (DS‑ASC‑005/206, `app_*` columns). The terminal fuel is
  **85–94% attitude**, translation ~8%. There is no free‑drift; the loop nulls att_err to ~0 and pulses the
  Dracos non‑stop. (The authority‑estimate fixes — units, then trust‑stock — were real but marginal: they moved
  applied duty 59%→51% and range 90→58 km. Not the dominant cause.)
- **RC2 — wrong procedure.** `Phasing.FarGuide` does a crude **continuous** ap‑raise → hands to CW at **100 km**.
  Range stalls (~93 km, never closes), the FSM cycles Phase/Transfer/Coast. This is not the real profile.
- **Budget context:** Draco Isp 240, RCS translation ~21% efficient (`RCS_BALANCE_FINDING.md`); full tank ≈ **66 m/s
  useful**. The real Dragon rendezvous is ~100–200 m/s spread over ~28 h of **discrete** burns + drift — affordable
  only if we stop the continuous thrash.

Evidence: `docs/FLIGHT_VERIFICATION.md` DS‑ASC‑004/005 + the 2026‑08‑31 20:48 re‑fly; CSVs in `docs/flights/`.

## 2. The real procedure (from `PHASE_3_RENDEZVOUS_RESEARCH.md` §2–4c; GNC §2)
A **named‑burn co‑elliptic rendezvous**, all on the 16 Dracos, ~16–28 h, **discrete burns with drift between them**:
**Phase** (set catch‑up rate) → **Boost/Close** (raise orbit) → **Transfer + Co‑elliptic** (establish a *circular*
co‑elliptic orbit a fixed **~10 km below/behind** the ISS) → **free drift** on that stable co‑orbit (minimal RCS) →
**AI at 7.5 km** (90 s, 0.72 m/s) → **Midcourse** → **WP0 (400 m, R‑bar) → WP1 (~200 m, V‑bar) → WP2 (20 m)** → dock.
Terminal legs = **CW two‑impulse transfers in LVLH** (§4b) to **OFFSET** aim points (never aim at the station);
coarse raises = **discrete Hohmann apsis burns** (§4b) with **phase‑lead** timing.

## 3. Gap analysis (current code vs the research)
| Real procedure | Current code | Gap |
|---|---|---|
| Insert low **and behind**, set phasing rate (Phase burn) | A1 inserts ~50 km below; no explicit behind/phase‑rate burn | partial |
| **Boost/Close/Co‑elliptic → a CIRCULAR co‑elliptic orbit ~10 km below** | raises ap toward park; **co‑elliptic circularize was REMOVED** ("~27 orbits on Dracos", `Phasing.cs`) | **MISSING — the core gap** |
| **Free drift** on the co‑elliptic between discrete burns | continuous ap‑raise + continuous prograde **hold** (94% duty) | **MISSING (RC1)** |
| Terminal **CW two‑impulse from AI = 7.5 km** | CW hand‑off at **100 km** (CW invalid there) | wrong range (RC2) |
| **WP0 (400 m R‑bar) → WP1 (200 m V‑bar) → WP2 (20 m)** waypoints | single offset aim, no waypoint ladder | **MISSING** |
| Discrete burns (point → burn to Δv → stop) | continuous trans_z until "ap ≥ target" | **MISSING (RC1/RC2)** |
| Offset targeting (passive‑abort safe) | `Lvlh.OffsetToWorld` exists (near‑field) | present, reuse |
| CW two‑impulse solver | `pure/Cw.cs TwoImpulse` exists | present, reuse |

## 4. The design (rebuilt rendezvous FSM)
A discrete, named‑burn FSM. Each burn: **acquire attitude → burn to a Δv target → stop → RELEASE attitude (drift)**.
States (pure `Phasing`/a new co‑elliptic module; glue in `RendezvousControl`):

1. **PHASE** — on the low insertion orbit, time a discrete prograde **Phase burn** (`pure/Hohmann` phase‑lead §4b)
   that sets the catch‑up rate; then **DRIFT** (warp) toward the alignment for the raise. *(A1 already gives the low
   insertion; keep it, optionally add a small "behind" component.)*
2. **CO‑ELLIPTIC ESTABLISH (Boost → Close → Co‑elliptic)** — discrete **Hohmann apsis burns** to raise to, and
   **circularize at**, `r_target − CoEllipticBelowM` (~10 km below). This restores the removed circularization but as
   **timed discrete prograde burns at apsides** (efficient), not the abandoned continuous low‑thrust circularize.
   Each burn is point‑and‑burn; drift between.
3. **CO‑ELLIPTIC DRIFT** — coast on the stable co‑elliptic orbit, **attitude RELEASED (free drift / wide deadband,
   minimal RCS)**, warp‑compressed, until the along‑track range closes to the **AI point (7.5 km)**. This is the
   fix for RC1: no continuous hold.
4. **AI + TERMINAL (CW two‑impulse, LVLH)** — at 7.5 km, run `Cw.TwoImpulse` legs to **offset** waypoints
   **WP0 (400 m below, R‑bar) → WP1 (~200 m, V‑bar) → WP2 (20 m)**, each a **discrete** first‑burn → coast →
   arrival‑null burn, with a **station‑keep hold** at each waypoint (and a GO gate hook). Free‑drift/loose attitude
   between legs. Hand to docking (Phase 4) at WP2.

**Attitude discipline (fixes RC1), applied throughout:** point‑and‑burn only; between burns and during the
co‑elliptic drift, **release the attitude channel** (extend the existing far‑field coast hysteresis
`CoastReacquireDeg`, and/or a **wide attitude deadband** so the nose drifts within a band and fires rarely). Never
hold prograde continuously. This is what makes the profile affordable on the feeble Dracos.

## 5. Files (smallest change that reaches the real procedure)
- `pure/Phasing.cs` — replace the `FarGuide` continuous PHASE→TRANSFER→COAST with the discrete named‑burn FSM
  (PHASE → CO‑ELLIPTIC ESTABLISH → DRIFT). Pure, testable. Keep `PeSafe`, the pe floor, `Hohmann` phase‑lead.
- new `pure/CoElliptic.cs` (or extend `Phasing`) — the discrete Boost/Close/Co‑elliptic burn targets + the AI‑range
  trigger. Pure, testable.
- `RendezvousControl.cs` — discrete burn execution (point → `SetTranslation` to a Δv target → stop), **free‑drift
  between burns** (attitude release), the CW terminal from **7.5 km** through the **WP0/1/2** ladder (reuse
  `Cw.TwoImpulse` + `Lvlh.OffsetToWorld`). Change `CwHandoffRangeM` semantics → an **AI range** (~7.5 km).
- Reuse unchanged: `pure/Cw.cs`, `pure/Hohmann.cs`, `pure/Lvlh.cs`, A1 (`AscentControl`), the **C reserve guard**,
  the **units fix + trust‑stock** authority, **PWPF**.

## 6. What to PRESERVE (protected/flight‑proven — rule V4/P‑protect)
A1 insertion; the ×1000 units fix; trust‑stock RCS authority; the C return‑reserve guard; PWPF pulse stage; the
pe‑safety floor; offset targeting; the recorder instrumentation (`app_*`, `rcs_pulse_*`). Do not revert these.

## 7. Verification (rules V1/V4)
- **L1 headless (pure):** the FSM produces the correct **named‑burn sequence** (Phase→Boost→Close→Co‑elliptic→AI→
  WP0→WP1→WP2), each burn **discrete** (finite Δv, then stop), the co‑elliptic target = `r_tgt − 10 km` **circular**,
  and the CW two‑impulse legs aim at **offset** points that free‑drift outside the KOS (passive‑abort). Add a
  **Δv‑budget test**: the summed burn Δv (at the 21% efficiency + the drift discipline) fits the **66 m/s** tank.
- **L2:** builds, installs (`python plugin/build.py {test|install}`).
- **L3/L4 re‑fly (owner):** rendezvous ISS → co‑elliptic → AI → WP0/1/2 → dock, within budget. The instrumentation
  proves the win: **applied attitude duty falls sharply during the co‑elliptic drift**, range **closes to 20 m**,
  MMH stays above the C‑guard reserve. Rule V4: this changes the proven rendezvous FSM → re‑fly.

## 8. Risks / open questions for the reviewer
1. **Co‑elliptic on Dracos** — the code removed circularization as "~27 orbits". Does the **discrete apsis‑burn**
   version fit the time (warp) and the 66 m/s budget? The real Dragon does it over ~28 h — confirm the KSP analogue.
2. **Δv budget at 21% efficiency** — does the full discrete profile (Phase+Boost+Close+Co‑elliptic+AI+WP0/1/2 +
   station‑keeps) fit **66 m/s**? If not, the efficiency (Option B, direct‑force translation) or a lower‑Δv profile
   returns as a prerequisite. *This is the single biggest risk — quantify it in the L1 budget test first.*
3. **Free‑drift vs pointing needs** — how loose can attitude be during co‑elliptic drift given the nav/camera needs?
   What wide‑deadband angle is safe?
4. **Waypoint values** — WP0 400 m R‑bar / WP1 200 m V‑bar / WP2 20 m (research §4c / GNC §2) — confirm for RSS/RO.
5. **Keep the far‑field Hohmann for coarse phasing** (research says yes) vs go fully CW — recommend Hohmann coarse +
   CW terminal (matches §4.3 "combine Hohmann phasing with CW terminal").

## 9. Implementation sequence (one change → one verification, per the cadence)
1. L1: the co‑elliptic‑establish + AI‑range FSM (pure) + the Δv‑budget test → **gate on the budget fitting 66 m/s**.
2. L1: the CW WP0/1/2 waypoint ladder (offset, passive‑abort) — pure.
3. Glue: discrete‑burn execution + free‑drift attitude release (RC1) in `RendezvousControl`.
4. Build/install → owner re‑fly (rendezvous‑to‑dock) → measure applied duty + range‑close + MMH.
5. Only then: tighten (station‑keep tolerances, GO gates, midcourse).

**STOP for review before implementing.** No code change is made by this document.
