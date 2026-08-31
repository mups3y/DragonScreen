# AI REVIEW HANDOFF — DragonScreen (read me first)

> **CLASSIFICATION: HISTORICAL REVIEW MATERIAL — a point‑in‑time snapshot prepared for a specific external
> review. It is NOT an instruction document and NOT a governing plan.** The authoritative live docs are
> `docs/MASTER_BUILD_SPEC.md` (the sole governing plan) and `docs/FLIGHT_VERIFICATION.md` (the evidence log);
> where they and this file disagree, they win. The "candidate fixes" in §3/§5 are **unratified reviewer inputs,
> not a plan** — the roadmap stays `MASTER_BUILD_SPEC.md`. Findings later corrected are flagged inline.
>
> **Snapshot date: 2026‑09‑01, after flight DS‑ASC‑008.** (Supersedes the DS‑ASC‑004‑era snapshot; the earlier
> "mechanism UNKNOWN" ask is now RESOLVED — see §3.)
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

## 3. Current state — what is proven, what is open (verify each)

Full evidence in **`docs/FLIGHT_VERIFICATION.md`** (flight entries DS‑ASC‑001..008). The "candidate fixes" below
are **unratified reviewer inputs, not a plan** — the roadmap stays `docs/MASTER_BUILD_SPEC.md`.

**PROVEN (flight):**
- **Ascent to orbit — FLIGHT‑PROVEN.** The S2 tumble (DS‑ASC‑001/002) was a **×1000 units bug**:
  `AttitudeController.ControlTorque` built the geometric RCS authority in **N·m** (`thrusterPower*1000`) while
  stock torque / gimbal / MOI are **kN·m / t·m²**, so `maxAlpha = ct/MOI` read 1000× high (S2 37.6 vs ~0.27 rad/s²).
  Fixed (drop the `*1000`); **DS‑ASC‑003/008 reach orbit** (194×403, then 367×336 km / 51.6°), S2 `ctrl_tq` ~520.
- **Booster recovery — PARTIALLY PROVEN** (controlled, not landed): DS‑ASC‑008 probe flew EntryBurn→LandingBurn.

**RESOLVED mechanism (the RCS‑loss question the last handoff asked):** the physics‑rate `acc_*` accounting
(DS‑ASC‑007) showed rendezvous fuel is **~97% ATTITUDE** (52% attitude‑only + 45% simultaneous attitude+translation),
**3% translation**; PWPF does **not** cut it (saturated demand passes the full‑threshold). Fix implemented: a
**phase‑plane (angle,rate) attitude‑hold deadband** on the prox‑ops loop (`AttitudeLoop.Axis`, ±2°/±0.2°/s,
gimbal‑gated so ascent is untouched) — drift inside the box, fire on exit.

**OPEN #1 — terminal approach + dock UNPROVEN (headline).** DS‑ASC‑008 (deadband re‑fly) ended with **58% Draco
propellant left** (vs DS‑ASC‑007 running dry) — but **stopped at 109 km, 9 km short of the 100 km near‑field CW
hand‑off**, so the terminal CW legs → dock were **never entered**. The deadband is proven for the far field only; a
rendezvous has never been flown through the terminal legs to a dock.

**OPEN #2 — ROLL UNDER‑CONTROL (owner‑reported this session; root‑caused; one cause, three symptoms).** *"The ship
shakes violently at the same place in ascent and only stops when RCS is switched on"* and *"we rolled uncontrolled
during multiple manoeuvres."* Confirmed vs the CSV + code:
- **S2 ascent:** the single MVac has **zero roll authority** (`ctrl_tq_roll=0` for 79% of S2). `AscentControl.cs:397‑414`
  disables RCS during S2 and only pulses it back when body rate > 6 dps (release 1.5) → roll **winds up in a sawtooth
  (peak 27.5 dps)**, RCS toggles **17×**, pitch/yaw gimbal **limit‑cycles ~2 Hz** → the visible violent shake; it
  stops only with continuous RCS. **Same root cause as the 27.5 dps separation tumble and the ~17% of Draco propellant
  burned detumbling after separation.**
- **Mission manoeuvres:** the capsule Dracos DO roll, but the rendezvous coast calls `FlightDriver.ReleaseAttitude()`
  (releases all axes; 46% of the rendezvous was full drift) and `Steering.Point` damps roll RATE but holds no roll
  ANGLE (`rollUpRef=0`, `Steering.cs:102`) → roll commanded only 21% of the time; the capsule drifted ~54° in roll.
- **Root cause:** roll is treated as a low‑priority / fuel‑saving axis, contradicting the spec's "full control at all
  times / crew orientation." **Touches FLIGHT‑PROVEN ascent → not yet changed (V4 gate + owner review).**

**OPEN #2b — RCS OVER‑THRUST (owner‑reported: "thrusters stop pulsing so much when I drop the thrust limit under 90%";
CONFIRMED config mistake).** The pod Dracos fire at **5×** the fine‑control design intent: RO sets 0.4 kN, and
`DragonScreen.cfg:141‑147` multiplies `@thrusterPower *= 5` (confirmed: the flown pod geometry dump reads `power_kn=2`).
The ×5 was meant to be paired with dialing `thrustPercentage` DOWN per task (`CapsuleRcs` ~20–40% for fine phases,
`DragonScreen.cfg:134‑139`) — **but that scale‑down was removed** (only the comment survives). So fine attitude hold
runs at 5× thrust; the min 0.06 s pulse kicks the rate ~0.05–0.50 °/s (≥ the 0.2 °/s deadband) → overshoot → chatter,
and dropping the limiter past `RcsPulseFull=0.90` (`FlightDriver.cs:174`) makes it fire continuously ("stops pulsing").
**The phase‑plane deadband (`a6eb15f`) was masking this symptom.** Candidate fix: restore per‑phase RCS thrust scaling
(fine phases ~20–40%, burns/roll 100%).

**OPEN #3 — secondary:** residual terminal attitude limit cycle (~7%/200 s even with the deadband; the tight terminal
HOLD does not get the far‑field coast's channel‑release economy); FDIR `NoControlSolution` seen in DS‑ASC‑008
(unlocalized); phasing is slow (~15.6 orbits). The Lambert mid‑field intercept (`UseLambertIntercept`) is built but OFF.

---

## 4. How to check my claims (evidence)

- **Flight CSVs (raw recorder telemetry):** `docs/flights/*.csv` — force‑added past `.gitignore`; `README.md` has the
  schema + **runnable stdlib‑Python reproduction snippets**. Key ones:
  - `Crew-2_20260901_004929.csv` (**DS‑ASC‑008**, the deadband re‑fly): the 58%‑left fuel, the 109 km stop, **and the
    S2 roll wind‑up / shake** (`ascent_phase=S2Burn`, `ctrl_tq_roll`, `rate_roll_dps`, `rcs_on` toggles). The
    screenshot window is MET ≈ 282–290 s; RCS comes on at MET 290.6 and roll damps live.
  - `Crew-2_20260831_220928.csv` (**DS‑ASC‑007**, pre‑deadband): the `acc_*` 97%‑attitude split, ran dry at 91 km.
  - `Crew-2_20260831_102133.csv` (DS‑ASC‑002, the S2 units‑bug tumble) + `Crew-2_20260831_141924.csv` /
    `..._geometry_dump_manual_2500s.csv` (DS‑DEO‑001, capsule authority + the geometry dump).
- **Code to read for the roll finding:** `AscentControl.cs:58‑64` + `:397‑414` (the S2 roll‑trim hysteresis);
  `Steering.cs:102` (`Point` damps roll rate, no roll angle); `RendezvousControl.cs:314‑315, 419‑420` (coast releases
  the attitude channel). For the terminal fuel: `pure/AttitudeLoop.Axis` (the new deadband) + `pure/RcsAccounting`.
- **Headless proof:** `plugin/test/AttitudeLoopTest.cs` (`python plugin/build.py test`).
- **Numbers to sanity‑check:** MOI in t·m² (full stack ~120,448); gimbal/stock/geometric authority in kN·m; Draco
  Isp 240 (`GameData/TundraExploration/Parts/RodanV2/TE_CD2_POD.cfg`); ISS target ~417×421 km; RCS translation ~21%
  efficient → whole tank ≈ 66 m/s useful Δv.

---

## 5. What I'm asking you

**A. Roll control (the freshest, owner‑reported issue — OPEN #2).** The single MVac has no roll authority and the
ascent deliberately runs RCS in a 6/1.5‑dps hysteresis (`AscentControl.cs:397‑414`), producing the violent S2 shake +
the 27.5 dps separation tumble + ~17% detumble fuel; the rendezvous never angle‑holds roll and releases the whole
attitude channel on coast. **Is a continuous roll‑only RCS damper with a tight rate/angle deadband (everywhere) the
right fix?** What is the smallest safe change, and what is the risk of enabling continuous roll RCS during the
flight‑proven S2 (fuel, gimbal interaction, MECO/SECO, plane hold)? Is there a way to give S2 roll authority without
RCS at all? Cite `AscentControl`, `Steering`, `AttitudeLoop`, `RendezvousControl`.

**B. Terminal approach + dock (OPEN #1).** DS‑ASC‑008 ended with 58% propellant but stopped 9 km short of the 100 km
near‑field CW hand‑off, so the terminal legs → dock are unproven. **Does the far→near architecture actually converge
the last 100 km, and is 58% enough to dock AND return?** Read `RendezvousControl.FlyNearFieldCw` + `pure/Rendezvous.cs`
(the named‑burn FSM) + `pure/Cw.cs`. Is the 100 km CW hand‑off inside CW's linearization validity? Why does the
chaser crawl the last 130→100 km at ~87 m/s with no braking? Is the phasing (15.6 orbits) or the Lambert intercept
(`UseLambertIntercept`, OFF) the better closing strategy?

**C. Crew Dragon rendezvous realism — `docs/RENDEZVOUS_REBUILD_PLAN.md` (UNDER VERIFICATION).** Assess the far‑field
Hohmann + near‑field CW architecture against **how a real Crew Dragon rendezvouses with the ISS** (named phasing
burns, co‑elliptic, R‑bar/V‑bar approach, waypoints/holds, the DRACO Δv budget). Does the full profile fit the ~66 m/s
budget at 21% efficiency? What is missing or wrong versus real Dragon prox‑ops?

Cite specific files/lines and the flight CSVs. Flag anything that looks like a wrong turn — be skeptical:
*"code existing ≠ working; only in‑game KSP flight is flight‑proven."*
