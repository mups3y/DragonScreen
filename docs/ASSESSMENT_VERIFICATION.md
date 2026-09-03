> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-30; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⚠ It verifies an external assessment of code that no longer exists in the tree.

# Verification of the ChatGPT Master Engineering Directive

> Verbatim source: [`docs/CHATGPT_ASSESSMENT.md`](CHATGPT_ASSESSMENT.md). Task (Chris, 2026-08-30):
> "copy the entire assessment … then verify everything … Do not miss a thing. That plan will be
> our new plan as long as it is correct."
>
> Method: every section checked against the **actual source** (not comments, not the prompt I fed
> ChatGPT), against the **build** (`python build.py test` → 731,239 checks, 0 failed, all suites pass,
> 2026-08-30), and against KSP/RO reality. Verdict + evidence per section below.

## Bottom line

**The directive is correct. Adopt it as the project plan.** It reframes DragonScreen as a *spacecraft
simulator first, KSP mod second* and — most importantly — it names the exact process failures that stalled
this project (patch stacks §65, "worked once ≠ complete" §7, symptom-patching §76, feature-chasing §75). Those
process rules are the real value and are adopted in full.

Three evidence-based corrections keep us honest per **§67 (don't rewrite good systems)** and stop us
skipping real gaps:

1. **Some recommendations are ALREADY DONE** — do not "rebuild" them: the torque attitude loop (§18), the
   FDIR ladder + phase-awareness + hysteresis (§33/§34/§36), state-machines-not-bools (§22), named
   constants (§64), and the dispersion/robustness suite (§59). Preserve these.
2. **Two premises about our current state are inaccurate** (recommendation may still be adopted, but not as a
   "fix"): §18 says we rely on stock SAS — we do not (SAS is a dead fallback). §37 says our trajectory-
   divergence uses "an overly simplistic residual" — actually the feed is disabled (nominal), so there is no
   residual at all to simplify.
3. **The genuine gaps** worth real work: §10 (control follows the active vessel today), §12 (no formal
   channel-authority manager), §14 (no manual handover), §15 (no single authoritative NavState), §32/§35/§37
   (FDIR feeds), §38 (no command bus), §41 (font not bundled), §60 (a NaN can pass the actuator clamp), and
   the living docs §6/§57/§78. Plus the real blocker: **everything downstream of stage separation is
   unproven in flight.**

**Legend:**
✅ADOPT (sound; make it a rule) · ✅DONE (already satisfied in code — preserve) · 🟡PARTIAL (exists, needs
completing/formalizing) · 🔧GAP (real work) · ⚠️PREMISE-OFF (claim about our state is inaccurate)

---

## Section-by-section

**§1 Primary mission (correctness→…→polish, not features→features).** ✅ADOPT. The anti-thrash ordering;
matches memory `campaign-plan-process`.

**§2 Absolute priority order (safety/authority first, polish last).** ✅ADOPT.

**§3 No proprietary-SpaceX claims; document assumptions.** ✅ADOPT. Compatible with memory
`crew2-full-fidelity` — we still replicate the *publicly inferable* real pipeline; we just stop labelling
inferred behaviour as "real SpaceX flight software."

**§4 Never guess the codebase; read source; class-exists ≠ complete.** ✅ADOPT. Same rule as memory
`build-verify-no-shortcuts` / "don't trust comments." This verification obeyed it (found live code
contradicting `// Attitude on SAS` comments).

**§5 Baseline before changing.** ✅ADOPT — 🟡STARTED. Build: green, Roslyn, 731,239 checks/0 failed. KSP
1.12.5, RSS/RO/Tundra deps. The remaining baseline artifact (per-subsystem status) is §6.

**§6 docs/COMPLETION_MATRIX.md (8 statuses).** 🔧GAP. Does not exist. We have related but different docs
(`INTEGRATION_SCORECARD.md`, `PHASE_ACCEPTANCE_CRITERIA.md`). Create the matrix.

**§7 Definition of Done (7 gates; PORTED/UNIT-TESTED/WORKED-ONCE ≠ COMPLETE).** ✅ADOPT. This is our
3-tick rule made stricter; matches memory `three-tick-system`. This is the single most important rule for me
to obey.

**§8 Target architecture (CrewIF | FlightDirector → Dragon/Booster agents → NAV/GNC/FDIR → KSP/RO).** ✅ADOPT
as target — 🟡PARTIAL. We have a flight-scene host (`FlightDriver`) + per-controller logic, but not a clean
`FlightDirector`/`VesselAgent` separation. Responsibilities exist; the boxes aren't drawn.

**§9 Two-vessel VesselAgent.** 🟡PARTIAL. The *capability* exists: the separated booster is flown while
non-active on its **own** `OnFlyByWire` with its **own** `AttitudeController` instance
(`BoosterControl.DriveNonActive`, `MissionConductor.cs:185`), PRE keeping it unpacked. But it is not
formalized as a `VesselAgent`, and "Dragon controller must not depend on Falcon controller" is not enforced
by structure. Booster recovery has **never succeeded in flight.**

**§10 Active vessel ≠ control vessel.** 🔧GAP (important, correct). Today control largely *follows* the
active vessel: `FlightDriver.FixedUpdate` reads `FlightGlobals.ActiveVessel` and `Bind/Unbind` moves our
`OnFlyByWire` to whoever is active (`FlightDriver.cs:201,244,107–112`). The non-active booster works **only**
in the "Dragon stays active" configuration. The stronger invariant the directive wants — either vehicle can
be camera-focused without dropping control of the other — is **not met.** This is a real architectural item.

**§11 PRE as an isolated physics layer; no PRE assumptions in guidance.** 🟡PARTIAL-DONE. `RangeExtender.cs`
is that isolation layer. Verify no PRE coupling leaked into pure guidance (spot-check: pure/ has no KSP refs
by construction, so this largely holds).

**§12 Formal control-authority manager (owner/priority per channel; AUTO/MANUAL/ABORT).** 🔧GAP. We have
*per-channel ownership booleans* (`throttleOwned/transOwned/rollOwned/attitudeOwned`, `FlightDriver.cs:114–155`)
but no manager with priorities, acquisition/release/takeover rules. ⚠️Naming: `pure/Authority.cs` is **torque**
authority (arrestable-rate), a different concept — name the new one to avoid collision (e.g. `ChannelAuthority`).

**§13 Abort has highest authority.** 🟡PARTIAL-DONE. The abort path returns early and releases throttle/
translation/roll + `AttitudePilot.Reset` (`FlightDriver.cs:214–220, 780–805`), so no normal controller runs
during abort. But it's enforced by control-flow ordering, not an explicit `owner=ABORT` latch on each channel.
Formalize with §12.

**§14 Manual/Auto handover preserving state.** 🔧GAP (future). The autopilot is AUTO-only today; there is no
manual-takeover path that preserves mission/nav/target state. Required by the finished vision (manual
intervention). Build with §12.

**§15 One authoritative NavState.** 🔧GAP. `NavFilter` is used in only 3 files — docking + rendezvous
(prox-ops). Ascent/guidance read `v.orbit`/`obt_velocity` directly. There is no single shared navigation
state consumed by all of guidance/control. Valid.

**§16 Explicit reference frames.** ✅ADOPT. Frame/unit confusion is our documented #1 bug class (memory
`build-verify-no-shortcuts`). `Steering.cs` documents ENU/world conventions, but vectors aren't frame-typed.
Adopt a naming convention (eci/lvlh/vesselFrame prefixes).

**§17 Separate NAV / GUIDANCE / CONTROL / MISSION / FDIR.** ✅ADOPT — 🟡PARTIAL. The pure/ split roughly maps
(guidance math in pure, control in `AttitudeLoop`, mission in `CrewProcedureOps`, FDIR in `Fdir`), but the
layering isn't clean or enforced.

**§18 Attitude control — replace SAS with a torque loop.** ✅DONE + ⚠️PREMISE-OFF. The premise ("current
reliance on stock KSP SAS") is **false**: `Steering.UseGimbalLoop = true` is the default and current state
(`Steering.cs:99`); the live inner loop is the direct gimbal/RCS torque controller (`AttitudePilot` →
`pure/AttitudeLoop.cs`), which is exactly the guidance→error→rate→torque→actuator cascade §18 asks for. SAS
is a dead one-flip fallback. **Action:** none to the architecture; DO fix the stale `// Attitude on SAS`
comments in `DockingControl/ReturnControl/EntrySteering/FlightDriver` (comment rot — the exact thing Chris
warned about).

**§19 Falcon 9 recovery: RTLS and ASDS as distinct modes.** 🟡PARTIAL + 🔧. `RecoveryMode {Droneship, RTLS}`
(`pure/MissionProfile.cs:17`) and a `BoosterPhase {Idle,Flip,EntryBurn,AeroDescent,LandingBurn,Landed}` FSM
(`pure/BoosterDescent.cs:19`) exist. But **neither mode is flight-proven**, and whether ASDS targeting is
genuinely distinct from "RTLS + different coordinates" is **unverified** — must confirm the geometry differs.

**§20 Booster+Dragon simultaneous acceptance test.** 🔧GOAL. Never achieved. This is a target criterion.

**§21 Dragon mission state machine (25 states, per-state contract).** 🟡PARTIAL. A coarse top-level
`MissionPhase` (`pure/MissionPhase.cs`) + rich per-controller FSMs exist (`AscentPhase`, `RvPhase`,
`DockPhase`, `DeorbitPhase`, `EntryPhase`, `ChutePhase`, `DepPhase`). There is **no** unified state machine
with the full 25 states and a per-state {entry/exit/timeout/abort/authority} contract.

**§22 No single boolean for complex state.** ✅DONE (largely). Docking is `DockPhase{…Contact,Captured,Abort}`,
chutes `ChutePhase`, etc. — not bools. Keep as a rule.

**§23 Rendezvous operational, not just math.** 🟡PARTIAL. `RvPhase{Idle,Phasing,CoElliptic,ApproachInit,
Midcourse,Arrived}` + `FarPhase` + CW + Lambert give an operational sequence. **Not proven** — the last flight
stalled in rendezvous (root-caused to separation, see `ISSUE_REGISTER.md`).

**§24 Docking (corridor/capture/hardmate/abort/station-keep/manual).** 🟡PARTIAL. `DockPhase` waypoint
approach with holds, `Contact`, `Captured`, `Abort`; dock-corridor + IDSS capture-envelope tests exist.
Missing: manual takeover; not flight-proven.

**§25 Return is a real mission phase (not press-DEORBIT→burn-retro).** 🔧GAP/PARTIAL. `DeorbitGuidance` +
`DeorbitPhase` + `ReturnControl` exist, but per `RETURN_FIX_PLAN.md` the rescue deorbit fires an **empty
SuperDraco** (zero thrust) — the return is broken, and return *planning* (opportunity/geometry/corridor) is
minimal. Valid and high-priority.

**§26 Entry guidance (energy/range/crossrange/lift/bank).** 🟡PARTIAL. `pure/Entry.cs` + `EntrySteering`
(bank-to-σ) exist; **never flown from a correct corridor** (confounded by the broken deorbit). Adopt §26's
rule: flight-test it, don't call it done on unit tests.

**§27 Crew interface is not a HUD.** ✅ADOPT. Core design principle; matches the finished vision.

**§28 Every screen function connects to real state; no fake buttons.** ✅ADOPT — 🟡PARTIAL. Panel lamps map
to real state (`PanelButtons.cs:319–326`: `AutoPilot.Engaged`, `DeorbitOps.Engaged`, bus state). Audit for
any decorative controls.

**§29 Crew procedures as step state machines.** 🟡PARTIAL-DONE. `CrewGate`/`CrewGates`/`StepList{StepState}`
+ `CrewProcedureOps` are the crew-gate conductor. Expand coverage.

**§30 Simulate, don't fake.** ✅ADOPT. Matches memory `no-python-simulations` (invented-and-trusted vs proven).

**§31 Simulated spacecraft systems.** 🟡PARTIAL. `LifeSupport` (TAC), `CabinEnvironment`, `VehicleSystems`
exist; coolant/comms/fire/smoke/redundancy largely unmodeled. Adopt the input/state/limits/failure template.

**§32 Telemetry source classification (MEASURED/DERIVED/SIMULATED/ESTIMATED/UNAVAILABLE).** 🔧GAP. No formal
source tagging today. Valid.

**§33 FDIR architecture (DETECT→ISOLATE→RECOVER→ESCALATE; recovery ladder).** ✅DONE (design) + 🔧. `pure/Fdir.cs`
implements exactly `Continue→Retry→Reconfigure→Replan→Downmode→Abort→SafeMode`, phase-aware, with escalation,
and it **generates a report** rather than manipulating subsystems (matches §33's "generate requests"). GAP:
only the **Abort** rung is wired to act (`FdirActing` default OFF); the intermediate rungs are computed but no
mission-manager executes them yet.

**§34 FDIR phase-aware.** ✅DONE. `Fdir.Recover()` switches on `MissionPhase` (`Fdir.cs:167–199`).

**§35 Resource-failure identification (which resource).** 🔧GAP. FDIR carries one scalar `ResourceMargin01`
(fed only RCS propellant, `FlightDriver.RcsPropMargin`). It does not preserve *which* resource. Valid.

**§36 FDIR hysteresis (trip/clear thresholds + confirm/clear durations).** ✅DONE. `FaultMonitor.Update` takes
separate trip/clear + `ConfirmS`/`ClearS`/`FastConfirmS` (`Fdir.cs:69–116`).

**§37 Trajectory divergence (cross/along/radial/miss-distance).** 🔧GAP + ⚠️PREMISE-OFF. The monitor exists
(`FaultKind.TrajectoryDivergence`, trips at 5 km) but the **feed is deliberately nominal** —
`fi.TrajErrorM = 0.0` (`FlightDriver.cs:656`). So there is no "simplistic residual"; there is *no* residual.
Build the real cross/along/radial/miss-distance feed §37 describes.

**§38 Command bus (central path).** 🔧GAP (future). UI calls into `FlightCommands`/controllers directly; no
central bus with interlocks. Valid architectural target.

**§39 Safety/interlock layer before irreversible actions.** 🟡PARTIAL. `IgnitionGate` gates ignition,
`ClampGate` gates hold-down release, abort latches — but no unified authority→mission→vehicle→resource→
interlock chain. Adopt.

**§40 UI rendering performance / dirty flags.** ⚠️HEDGED — 🟡. The claim is "potentially does more work than
necessary" — unverified here; needs profiling (the directive itself says "profile first"). Governed already by
memory `build-verify-no-shortcuts` (hard 60 FPS rule). Action: profile `ScreenPainter` render cadence before
optimizing.

**§41 Font bundling.** 🔧GAP (confirmed). `ScreenPainter.cs:236` uses `Font.CreateDynamicFontFromOSFont`,
which only loads **Windows-installed** fonts; the code comment admits D-DIN is bundled under OFL but the API
can't load it from file. A user without D-DIN falls back to Arial. Valid.

**§42 Screen-state lifetime / session.** 🟡PARTIAL. `FlightDriver.ResetAll` resets statics on scene start;
IVA screen objects are destroyed on teardown. Adopt an explicit session-lifecycle object.

**§43 Unity object lifetime/cleanup.** ✅ADOPT — 🟡. Klaxon/IVA-light handling shows care; audit
RenderTextures/Materials/Cameras in `DockingCamRenderer`/`NavBallRenderer`/`HullCams` for
scene/vessel/revert cleanup.

**§44 No global scene search in hot paths.** ✅OK (largely). One `FindObjectsOfType<PanelButton>` in
`ClearArmedLamps` (`PanelButtons.cs:333`) — an event handler, **not** a per-frame path. Keep as a rule; low
priority.

**§45 Performance requirement (measure).** ✅ADOPT. Matches the 60 FPS memory rule (target i5-14400F/GTX 1080).

**§46 External mods as infrastructure.** ✅ADOPT. Matches memory `mod-dependency-policy`.

**§47 F9I reference, not blind port; stop grep→port→fly→patch.** ✅ADOPT. Matches memory
`falcon-port-dont-invent`; this *is* one of the failure patterns that cost flights.

**§48 Testing pyramid (5 levels).** 🟡PARTIAL. L1 unit is strong (731k checks); L2 component partial;
L3–L5 (integration/full-mission/fault-injection) mostly absent — inherently flight-gated in KSP. Adopt;
be honest that L3+ can't be headless.

**§49 Test invariants.** 🔧ADOPT. Write explicit invariant tests. Note: "vessel switch does not transfer
control authority" is an invariant that **currently fails** by design (§10) — enforcing it drives the §10 fix.

**§50 Formal flight-test records.** 🟡PARTIAL. We have `FlightRecorder` CSVs + `ISSUE_REGISTER.md`. Adopt the
structured build/version/craft/expected/actual/cause/fix/retest record.

**§51 Flight-test IDs (DS-FLT-001…).** 🔧ADOPT. We currently name by timestamp; adopt reproducible IDs.

**§52 Full-mission acceptance test.** 🔧GOAL. The north-star acceptance (Dragon full mission *and*
simultaneous booster recovery, no manual vessel-switch).

**§53 Failure acceptance tests.** 🔧FUTURE. After nominal works.

**§54 Manual-flight acceptance.** 🔧FUTURE. Depends on §14.

**§55 UI acceptance per screen.** 🟡ADOPT. We have `UI_AUDIT.md`; extend to functional acceptance.

**§56 Screen reflects real state (no DOCKED unless HARD_MATE).** ✅ADOPT. `DockPhase.Contact/Captured`
supports truthful docking state.

**§57 docs/ASSUMPTIONS.md.** 🔧CREATE. Assumptions are scattered across `*_RESEARCH.md`; consolidate.

**§58 docs/REFERENCE_SOURCES.md.** 🟡PARTIAL. Many research docs cite sources; consolidate into one index
(matches memory `prefer-primary-sources`).

**§59 Don't overfit to one flight (vary mass/orbit/phase/fuel…).** ✅ADOPT — ✅DONE (pure). The Tier-2
dispersion suite already property-tests 20,000 randomized cases per domain (control/rendezvous/docking/
return/fdir). Extend the philosophy to flight tuning.

**§60 Numerical robustness (NaN/inf/div0/saturation).** ✅ADOPT — 🔧FIX. `Clamp1` (`FlightDriver.cs:136`)
does **not** guard NaN: for `d=NaN`, `d<-1` and `d>1` are both false, so it returns NaN → a NaN could reach
`st.pitch/yaw/roll/X/Y/Z`. Real fail-safe hole; add a finite check.

**§61 Never invalid actuator commands (validate/clamp/finite/authority).** 🟡PARTIAL. Throttle/translation/
roll are clamped to [-1,1]; but no explicit finite guard (see §60). Adopt the full validate→clamp→finite→
authority chain at the `OnFlyByWire` boundary.

**§62 Structured logging.** 🟡PARTIAL-DONE. `FlightRecorder` CSV + extensive `Debug.Log` lines exist (matches
memory `instrument-everything`). Add explicit authority-change / vessel-registration / PRE-state events.

**§63 Don't hide failures (no weakening tests/tolerances/magic constants/suppressing warnings).** ✅ADOPT.
Core behavioural fix; names my failures directly.

**§64 No magic constants (name/document/justify/test).** ✅ADOPT — ✅DONE (largely). The `[Tunable]` pattern
(`Tuning.cs`) names + documents constants with source rationale. Keep.

**§65 No patch stacks (root cause, refactor).** ✅ADOPT. Names my #1 failure mode (patched the rendezvous
symptom instead of the separation root cause).

**§66 Report ARCHITECTURE BLOCKER and stop.** ✅ADOPT.

**§67 Don't rewrite good systems.** ✅ADOPT — critical. Verified-good to PRESERVE: the torque attitude loop
(§18), the FDIR ladder (§33), `DockPhase`/other FSMs (§22), the `[Tunable]` system (§64), the dispersion suite
(§59), `FlightRecorder` (§62), `RangeExtender`/PRE isolation (§11).

**§68 Code-quality rule (owner/inputs/outputs/state/lifecycle/tests/logs/failure).** ✅ADOPT. Matches memory
`build-verify-no-shortcuts` (pure/glue discipline, fail-loud).

**§69 NOW/NEXT/LATER lists.** ✅ADOPT. Matches `ISSUE_REGISTER.md` + campaign plan.

**§70 Don't chase cosmetics until the mission is proven.** ✅ADOPT.

**§71 Completion gates (don't advance until the prior phase meets criteria).** ✅ADOPT. Matches memory
`work-efficiency-no-second-guessing` (tune one phase at a time in mission order).

**§72 Recommended dev phases (0 audit → 11 full mission).** ✅ADOPT as the roadmap. Our campaign labels
(C0…) map onto these phases.

**§73 Final acceptance standard.** ✅ADOPT.

**§74 Golden rule — simulator first, KSP mod second.** ✅ADOPT. Reframes the project; matches the finished
vision.

**§75 Second golden rule — ask what blocks a full mission, not what feature to add.** ✅ADOPT. Anti-backlog;
names my failure.

**§76 Third golden rule — root-cause questions on failure.** ✅ADOPT. The exact discipline I skipped on the
separation collision.

**§77 First task = the A–M audit; don't code first.** ✅DOING. This verification + baseline is the start; the
A–M report and COMPLETION_MATRIX come before any code change.

**§78 Living docs (10 files).** 🟡PARTIAL. `ARCHITECTURE.md` exists; `COMPLETION_MATRIX`, `ASSUMPTIONS`,
`REFERENCE_SOURCES`, `FLIGHT_TEST_PLAN/RESULTS`, `KNOWN_ISSUES`, `CONTROL_AUTHORITY`, `MISSION_STATE_MACHINE`,
`TWO_VESSEL_ARCHITECTURE` mostly don't. Create as we do the work they document (not up front).

**§79 Change management (WHY/WHAT/AFFECTED/RISKS/TESTS/FLIGHT?/RESULT).** ✅ADOPT. Matches campaign-plan
§8-output-before-code.

**§80 Be skeptical; say "this is wrong."** ✅ADOPT. Matches the closing "permission to tell me I'm wrong" note.

**§81 Final objective (IVA feels like operating a spacecraft).** ✅ADOPT.

### Closing notes (outside the numbered directive)
- **"You have permission to tell me my approach is wrong."** ✅ADOPT — and Chris has now said it too.
- **End-of-session status block** (CURRENT MILESTONE / BLOCKER / WHAT CHANGED / WHAT VERIFIED / STILL
  UNPROVEN / NEXT ACTION). ✅ADOPT — append to every session end.
- **Progress metric = % of a complete, repeatable, astronaut-operated mission proven in RSS/RO** — NOT lines
  of code / test count / ported functions / screens. ✅ADOPT. This is *the* anti-thrash metric and directly
  corrects how I mis-measured progress.

---

## What this changes about how we work (adopted rules)
1. Nothing is "done" until flight-proven (§7). I say "built, headless-green, UNVERIFIED in flight" otherwise.
2. Measure progress by mission-proven fraction, not code volume (progress-metric note).
3. One change class per flight; root-cause confirmed ≥2 ways before a fix (§65/§76).
4. Preserve verified-good systems; don't rebuild them (§67).
5. Maintain COMPLETION_MATRIX + the end-of-session status block; audit-before-code (§6/§77).

## Immediate GAP backlog (dependency-ordered, for the A–M plan — NOT started yet)
1. Flight-prove the separation fix chain that's already built-but-unverified (the actual blocker).
2. §10 control-authority follows-mission-not-camera + §12 formal channel-authority manager (+ §13 abort latch).
3. §25 fix the broken deorbit (empty SuperDraco) — return is a real phase.
4. §15 authoritative NavState; §35/§37 real FDIR resource-ID + divergence feeds.
5. §60 NaN-guard the actuator clamp; §41 bundle the font.
6. §6/§57/§78 living docs incl. COMPLETION_MATRIX.
