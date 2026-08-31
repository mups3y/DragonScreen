# AI REVIEW HANDOFF — DragonScreen (read me first)

> **CLASSIFICATION: HISTORICAL REVIEW MATERIAL — a point‑in‑time snapshot prepared for a specific external
> review. It is NOT an instruction document and NOT a governing plan.** The authoritative live docs are
> `docs/MASTER_BUILD_SPEC.md` (the sole governing plan) and `docs/FLIGHT_VERIFICATION.md` (the evidence log);
> where they and this file disagree, they win. Findings later corrected are flagged inline (see §3 item 5,
> which a 2026‑08‑31 verification RETRACTED).
>
> A reading guide + snapshot for an **external AI reviewing the recent GNC work**: understand what was done
> and **check whether it is correct**, against the evidence (the flight CSVs) and the rules below. Be skeptical —
> the project's own rule is *"code existing ≠ working; only in‑game KSP flight is flight‑proven."*

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

5. **⚠ DS‑ASC‑004 — STILL ran dry; my first read was RETRACTED on verification (2026‑08‑31).** A1 worked, but the
   tank still emptied in the terminal phase (mmh 0.84→0.02 in ~500 s; the guard tripped at 20% yet mmh continued
   to ~0.02). I first called this a "terminal attitude limit cycle because the loop has no deadband/PWPF" —
   **that is WRONG.** `FlightDriver.cs` (L170‑203) **already has a PWPF/deadband path** (`UseRcsPulse=true`,
   `RcsPulseDeadband=0.05`, `RcsPulse.Step` on pitch/yaw/roll when the engine is off), implemented + tested in
   `pure/RcsPulse.cs` + `test/RcsPulseTest.cs`; my claim read only `AttitudeLoop.cs:27` (the PID's *internal*
   deadband). And the recorded `act_*`/`trans_*` are **pre‑pulse controller DEMAND** (`FlightLog.cs:100`), not the
   applied post‑pulse actuation — so my "68–82% duty" measured demand, not delivered firing. I also missed the
   per‑phase capsule‑RCS scaling (`CapsuleRcs.*Pct`) and `Attitude.CoastMaxRateDps`. **The terminal‑drain mechanism
   is UNKNOWN — EVIDENCE REQUIRED.** I made an **instrumentation‑only** change (record the applied post‑pulse
   actuation + pulse flags: `app_*`, `rcs_pulse_att/trans`) and owe **one focused re‑fly** to attribute the drain.
   No control‑law change until that flight proves the mechanism.

**THE OPEN DECISION (now: verification, not a fix):** the fix is NOT decided — the mechanism isn't proven, and a
deadband/PWPF already exists. The immediate step is the instrumented re‑fly, THEN quantify each contributor
(controller demand, post‑PWPF actuation, the ~1.5× authority over‑read `ctrl_tq_yaw` 10.3 vs measured ~7,
translation, CapsuleRcs scaling), THEN present the smallest root‑cause fix. **Reviewer, please still weigh in on
Question A below:** given a PWPF stage already exists, what is the most likely real cause, and is a deadband/PWPF
change even the right lever — or is it authority over‑estimation, the phase RCS scaling, saturated demand passing
through PWPF's full‑threshold, or the far‑field/approach geometry?

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

**A. The terminal fuel drain (mechanism UNKNOWN — a PWPF/deadband already exists in `FlightDriver`).** Inspect the
full command chain — `AttitudeLoop.Axis` → `AttitudeController`/`AttitudePilot.Act*` (recorded demand) →
`FlightDriver.OnFlyByWire` ownership + `RcsPulse.Step` (PWPF, `pure/RcsPulse.cs`, `test/RcsPulseTest.cs`) →
`FlightCtrlState` → RCS thrust/resource → measured rate. What is the most likely real cause of the terminal drain,
and is a deadband/PWPF change even the right lever, or is it authority over‑estimation, the per‑phase CapsuleRcs
scaling, saturated demand passing PWPF's full‑threshold, or the approach geometry? Do NOT assume "no PWPF." Also
sanity‑check the instrumentation I just added (`app_*`, `rcs_pulse_att/trans`) — is it sufficient to attribute the
drain on the next flight, or is something else needed (e.g. a per‑cause propellant tally)?

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
