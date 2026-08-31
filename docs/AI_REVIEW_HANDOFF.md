# AI REVIEW HANDOFF — DragonScreen (read me first)

> A single reading guide + current‑state snapshot for an **external AI reviewing the recent GNC work**.
> It **points to** the authoritative docs; it does **not** replace them and is **not** a governing plan
> (the sole plan is `docs/MASTER_BUILD_SPEC.md`). Your job: understand what was done and **check whether it
> is correct**, against the evidence (the flight CSVs) and the rules below. Be skeptical — the project's own
> rule is *"code existing ≠ working; only in‑game KSP flight is flight‑proven."*

Project: an autonomous **Crew Dragon on Falcon 9 inside Kerbal Space Program** with **Realism Overhaul / Real
Solar System** (RSS/RO — real Earth radius 6371 km, real µ, real physics). A C# KSP plugin: a Dragon
cockpit UI + a spacecraft/mission autopilot. Repo: `github.com/mups3y/DragonScreen`.

---

## 1. The rules I work under (the "constitution")

Authoritative: **`docs/MASTER_BUILD_SPEC.md`** (the single governing spec — read it). The operative rules:

- **Evidence discipline / verification levels.** L1 = pure/headless tests (math only). L2 = KSP integration.
  **L3 = single‑vessel in‑game flight. L4 = multi‑vessel flight.** *Only a flight proves a capability;* headless
  green is L1 and never flight‑proof. Use `UNKNOWN — EVIDENCE REQUIRED` / `CONFLICT`, never a guess.
- **STOP → INVESTIGATE → DOCUMENT → RESOLVE → IMPLEMENT → VERIFY.** Root cause, not symptom. No GUESS→CODE→PATCH.
- **Smallest safe change; protect flight‑proven systems.** Changing flight‑proven code (ascent, abort, booster,
  and now `AttitudeController.ControlTorque`) **invalidates the proof until re‑flown (rule V4).**
- **One source of truth.** The display consumes the autopilot's authoritative state; it never invents state.
  One governing plan only — do not propose competing plans.
- **Cadence.** The owner (Chris) flies KSP; the AI does L1/L2 (build/tests) and hands over regression flights.
  Plans are routed through a second‑AI reviewer (this handoff). Commits are made locally and **pushed via
  GitHub Desktop** (never git CLI here).

Build/test/install: `python plugin/build.py {test|preview|install}` (the test gate runs before install).

---

## 2. My working memory (project context I carry between sessions)

- **Governing spec** = `docs/MASTER_BUILD_SPEC.md` (overrides all other docs). Subordinate: `SCREEN_SPEC.md`.
  Other ACTIVE docs: `COMPLETION_MATRIX`, `SOURCE_OF_TRUTH`, `TELEMETRY_REGISTRY`, `COMMAND_REGISTRY`,
  `SCREEN_EVIDENCE_MATRIX`, `FLIGHT_VERIFICATION`, `DEPENDENCY_MATRIX`. Old plans were deleted (git history only).
- **Core architecture defect being fixed:** the display recomputed/invented state instead of reading the
  autopilot's authoritative state → one authoritative state + an `AuthorityManager` feeding an immutable
  read‑only display snapshot. Phases 0–7 landed (mostly CODE‑UNFLIGHTED); the docking screen look is "accepted".
- **The Dragon has no main engine** — the **Dracos** (RCS) do attitude *and* all translation/deorbit. This is
  central to the rendezvous problem below.
- **Verification truth:** flight‑proof = the owner's KSP flights; I do L1/L2. Return history: **0 autonomous
  returns to date** (crew cannot yet be brought home).

---

## 3. Current plan / state — what I did, in order (verify each)

All of this is detailed with evidence in **`docs/FLIGHT_VERIFICATION.md`** (flight entries DS‑ASC‑001..004,
DS‑DEO‑001) and **`docs/COMPLETION_MATRIX.md`**. Recent commits: `f1a0cbb..HEAD`.

1. **S2 ascent tumble → FIXED (flight‑proven).** The Falcon second stage tumbled at MVac ignition (DS‑ASC‑001/002).
   Root cause: a **×1000 units bug** — `AttitudeController.ControlTorque` built the RCS "geometric" authority
   estimate in **N·m** (`thrusterPower*1000`) while stock `GetPotentialTorque`, the gimbal report, and MOI are in
   **kN·m / t·m²**, so `Max(stock, geometric)` fed `maxAlpha = ct/MOI` a value 1000× too high (S2 read 37.6 rad/s²
   vs a real 0.27). Decomposition proof: `ctrl_tq 62,465 − rcs_geo 62,000 = gimbal 464 kN·m ≈ the flight‑delivered
   445`. **Fix:** compute the geometric in kN (drop the `*1000`), keep `Max(stock, geometric)`. **Verified:** headless
   `AttitudeLoopTest` (as‑flown limit‑cycles, units‑fixed converges) + **DS‑ASC‑003 reached a 194×403 km / 51.6°
   orbit** with S2 `ctrl_tq` reading ~526 (was 62,000). *Check: is the units diagnosis and the kept `Max(stock,
   geometric)` correct? Any axis/sign error?*

2. **Rendezvous ran the MMH tank dry (DS‑ASC‑003).** Measured: the **Dragon Dracos are Isp 240 s, 2 all‑axis
   `ModuleRCSFX` blocks with no per‑thruster control** (`RCS_BALANCE_FINDING.md`), and RCS translation is only
   **~21% efficient**; the **whole tank ≈ 66 m/s of useful Δv.** The far‑field Hohmann climb from a 200 km insertion
   (~61 m/s) cost ~85% of the tank → stranded. A budget model reproduces the stranding (needs ~140 m/s > 66).
   *Check: is 21% and the 66 m/s budget credible? Is the far‑field strategy itself the problem?*

3. **Fix A1 (implemented, re‑fly‑confirmed to work):** `AscentControl.RendezvousParkAltM` now inserts
   `ParkBelowStationKm` (default **50 km**) below the selected target's periapsis when a rendezvous target is set,
   so the efficient MVac buys the altitude and the Dragon RCS only does prox‑ops (no target → unchanged 200 km).
   **DS‑ASC‑004 confirmed A1** (inserted 366×363 km below the 417 km ISS; transfer shrank to a ~50 s ap‑raise).

4. **Fix C (safety net, implemented):** every rendezvous translation routes through `RendezvousControl.RvTranslate`,
   which inhibits + holds + warns once return propellant falls to `RvReturnReserveFrac` (0.20), to prevent a total
   drain. **It tripped correctly in DS‑ASC‑004 but did not save the flight — see #5.**

5. **⚠ DS‑ASC‑004 — STILL ran dry, and it exposed a technique error of mine.** A1 worked, but the drain **moved
   from translation to a TERMINAL ATTITUDE LIMIT CYCLE.** In the approach the attitude loop oscillates
   (`rate_yaw` ±4 dps around `att_err` ~3°, `act` ±1 at **68–82% RCS duty**) and drained mmh 0.84→0.02 in ~500 s;
   after the guard cut translation, **attitude alone drained 0.20→0.02.** Mechanism: the attitude PID has **no
   deadband/PWPF** (`pure/AttitudeLoop.cs:27`), so holding attitude on the tiny bang‑bang Dracos chatters —
   worsened by a **~1.5× authority over‑read** (`ctrl_tq_yaw` 10.3 vs a flight‑measured ~7; the "secondary
   over‑count" residual I deferred at the units fix, **not harmless without a gimbal**). **My error:** I built the
   translation guard (#4) for the *previous* drain path without first confirming the *current* one. Correct
   technique — measure the drain path (attitude vs translation duty) before fixing — is now applied.

**THE OPEN DECISION (unresolved — I want your recommendation):** to fix the terminal attitude fuel drain, should I
**(a)** implement an RCS attitude **deadband/PWPF** *and* switch `ControlTorque` to the **achievable** RCS authority
(~7, not 10.3) together, or **(b)** first **quantify** how much of the drain is the limit cycle vs the over‑read,
then implement the sized fix? My lean is **(b)** (measure, then fix) given I just misfired on #4. Is a deadband/PWPF
even the correct primary technique, or is something else the real fix?

---

## 4. How to check my claims (evidence)

- **Flight CSVs (raw recorder telemetry):** `docs/flights/*.csv` — force‑added past `.gitignore`. Key ones:
  `Crew-2_20260831_102133.csv` (DS‑ASC‑002, the S2 tumble regression), `Crew-2_20260831_141924.csv` +
  `Crew-2_deorbit_geometry_dump_manual_2500s.csv` (DS‑DEO‑001, capsule authority), and the latest
  `Crew-2_20260831_151611.csv` (DS‑ASC‑003, to‑orbit + first fuel‑out), and `Crew-2_20260831_170204.csv`
  (DS‑ASC‑004, the A1 flight — reproduce the terminal attitude limit cycle). `docs/flights/README.md` has the
  column schema and **runnable stdlib‑Python reproduction snippets** for every key number (the units bug, the 21%
  efficiency, the budget, and the DS‑ASC‑004 attitude‑vs‑translation duty).
- **Headless proof:** `plugin/test/AttitudeLoopTest.cs` (run `python plugin/build.py test`).
- **The numbers to sanity‑check yourself:** MOI is in t·m² (full stack ~120,448); the gimbal/stock authority and
  the geometric are meant to be kN·m; Draco Isp 240 (`GameData/TundraExploration/Parts/RodanV2/TE_CD2_POD.cfg`);
  ISS target ~417×421 km.

---

## 5. What I'm asking you

**A. The open decision above** — (a) fix together, or (b) measure‑then‑fix? Recommend, and say whether a
deadband/PWPF is the right primary technique for the terminal attitude limit cycle.

**B. Ascent to orbit** — independently hunt for issues in `AscentControl`, the `ControlTorque` units fix,
staging/MECO/SECO, and the A1 insertion change. Is the units‑bug diagnosis actually right? Could inserting
circular ~50 km below the station regress the just‑proven ascent (Δv margin, g‑limits, guidance) or cause
slow/failed phasing? Anything unsafe or wrong?

**C. Crew Dragon rendezvous procedures** — assess the architecture (`Phasing.cs` far‑field Hohmann + Clohessy–
Wiltshire near‑field, 100 km CW hand‑off) against **how a real Crew Dragon rendezvouses with the ISS** (phasing /
height‑adjust burns, co‑elliptic, R‑bar vs V‑bar approach, waypoints/hold points, the real DRACO Δv budget). Is
this realistic and fuel‑viable on a Draco‑only vehicle? Is a 100 km CW hand‑off inside CW's linearization validity?
Is inserting 50 km below the station the right call, or should the profile be structured differently? What is
missing or wrong versus real Dragon prox‑ops?

Cite specific files/lines and the flight CSVs. Flag anything that looks like a wrong turn.
