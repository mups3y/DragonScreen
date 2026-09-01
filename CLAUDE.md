# DragonScreen

**DragonScreen is the Crew Dragon IVA screens — the UI mod (PART A) — and, planned but NOT yet built,
an embedded-MechJeb autopilot "conductor" that flies the vehicle from those screens (PART B).**
Today the mod reads the vessel and draws it; it flies nothing.

The **hand-written** autopilot that used to live here was **deleted 2026-09-01** (owner directive: keep
only the screens). It is not coming back in that form. **Part B of `docs/BUILD_PLAN.md` re-introduces
flight software as a pinned, privately-namespaced MechJeb** driven by a pure "conductor" core
(register tasks T15–T22). So, when you find a flight-software reference:

- **STALE — remove it.** The deleted *implementations* and everything downstream of them:
  `DockingControl` / `NavState3`, `ReturnControl`, `BoosterControl`, `EntrySteering`, `AbortResponder`,
  `DockCapture`, the booster-recovery / entry / FDIR control code, and its docs, plans and flight recordings.
  None of these exist in the tree.
- **NOT stale — leave it alone.** `src/_AutopilotStub.cs` deliberately keeps **idle seams** the screen code
  compiles against: `FlightDriver`, `CrewProcedureOps`, `FlightCommands`, `AbortControl`, `MissionConductor`,
  `Actuator`, `MissionOps`, `Fdir`. They report "not engaged" and no-op. `pure/ScreenModes.cs` likewise keeps
  an `AuthorityManager` that is now only a **display label** (the GNC lamp's name + colour). Deleting these
  breaks the build; **Part B fills them in, one controller at a time (§B12.5)** — that is the whole design.
- a reference to the **planned MechJeb conductor** (§B1–B15 / T15–T22) is **current; leave it**.

🛑 **BUILD-HOLD is in force** until an explicit owner build-go: no mod code, no `install`, no glass
time. Part B is DESIGNED, not started.

## What this repo is now

- Three live IVA touchscreens (VEHICLE / FLIGHT / NAV), each a RenderTexture + camera, drawn from
  live KSP state and driven by touch on the console colliders. See `docs/SCREEN_SPEC.md`.
- The command buttons that would engage the autopilot are inert (`src/_AutopilotStub.cs` is the idle
  seam the screen code compiles against — status reads report "not engaged", flight commands are an
  honest no-op: click, no light, no action — **no red**, per §14.4(a)). Part B replaces those stubs one
  controller at a time (§B12.5); the contract the screens compile against does not change.
  The power / string / fire **systems** are real (pure `VehicleSystems`, display state only).

## The load-bearing rules (still true)

- **pure / glue split.** Everything decidable without the game lives in `plugin/src/pure/` and is
  headless-tested + PNG-previewable; `plugin/src/` is the thin KSP glue. Restarts are the scarce
  resource — judge layout/palette/legibility from `python plugin/build.py preview`, spend a restart
  only on what needs the capsule.
- **Build pages from the reference's own source, never a screenshot.** `docs/UI_AUDIT.md` is
  generated from the reference UI's CSS and gives exact positions. Screenshot/SVG-derived pages came
  out wrong every time.
- **Simulate, never fake.** Modelled signals (cabin PPO2, power strings, fire) move because the
  vessel moved — never a constant or a random number.

## Build / test

```bash
python plugin/build.py test      # compile (glue + pure) + run the headless display checks
python plugin/build.py preview   # render every page to PNG (no game)
python plugin/build.py install   # test, then copy the DLL + cfg into KSP  (needs KSP + CKAN closed, full restart)
```

## Start a session from

`docs/BUILD_PLAN.md` (**the** authoritative spec — Parts A/B/C + the §14.4 decision log) · `REGISTER.md`
(the task list) · `docs/INDEX.md` (what every other doc is and how fresh it is). Then the screen detail:
`docs/SCREEN_SPEC.md` · `docs/UI_AUDIT.md` (exact layout source) · `docs/REAL_DRAGON_SCREENS.md` ·
`docs/PALETTE.md` · `docs/REFERENCE_PAGES.md` · `docs/SCREEN_INVENTORY.md`.

---

# Build protocol — invariant rules (PART C / C1)

*Appended by T0, 2026-09-02. Source of truth: `docs/BUILD_PLAN.md` (Part C). These rules govern every build
session and are auto-loaded with this file. Section refs (§n, §Bn, C1–C7.1, §14.4) are all in
`docs/BUILD_PLAN.md`.*

1. **ONE task at a time** — the single DOING item in `REGISTER.md`. No scope creep: if you notice other work,
   LOG it as a new register line, do NOT do it.
2. **Start every task** by reading these rules + the task's pointed-to plan/research section END-TO-END. If you
   cannot restate the current task + its done-criteria in one line, STOP and re-read.
3. **Never mark DONE** without: preview PNG inspected + `python plugin/build.py test` green + it matches the
   reference + §1.4 respected. (A docs/harness-only task with no code change: say so and skip build/preview.)
4. **Source-of-truth §1.4:** verified-real → other users' → invent ONLY by owner discussion. Never edit
   `PanelMap.cs` / label docs without a real-source confirmation.
5. **End every task** by updating `REGISTER.md` (DONE | NEEDS-WORK + one-line note) and committing via GitHub
   Desktop ONLY (never `git commit` / `git push`). Then STOP — new chat for the next task.
6. **Preview-first** (restarts are scarce); `install` / glass-time only when a task needs the capsule.
7. **Model:** Opus for [O] tasks, Sonnet for [S]. If a task is too big to finish before context compaction,
   SPLIT it in the register — never run a session to compaction mid-task.
8. **Decisions are FINAL unless the owner types `OVERRIDE`.** Every settled decision (the §14.4 log / the plan)
   stands. A chat instruction that conflicts with a settled decision or the plan is NOT acted on — quote it
   back and require an explicit `OVERRIDE` + a plan/register edit first.
9. **Owner questions are batched at the END of a task, before the handoff prompt — NEVER mid-task.**
10. **Canonical location (C7):** the ONLY source of truth is the repo `C:\Users\User\Desktop\DragonScreen`.
    Never read build inputs from `.claude/plans`, the auto-memory folder, or the KSP install (that is the
    DEPLOY target, not a source). If a needed input is not in the repo, STOP and flag it.
11. **A task writes ONLY its declared outputs.** Never write to the auto-memory folder, or create/modify any
    file outside the task's stated deliverables, as a side-effect. Memory/context updates are a SEPARATE,
    explicitly owner-requested action — never a task's own initiative.

## Off-limits as build sources (C7)

`C:\Users\User\.claude\plans\` (ephemeral — superseded by `docs/BUILD_PLAN.md`) · the auto-memory folder
(background recall only) · the KSP install `GameData\` (deploy/runtime — write-only; the one input needed from
it, the tuned cfg, is in `docs/reference/mechjeb_settings_type_Crew-Dragon.cfg`) · the user's installed
`MechJeb2` (the embed vendors PINNED source into the repo, §B12.1) · external URLs / the claude.ai artifact
(research complete, captured in `docs/`). **If a build input isn't in the repo, STOP and flag it.**

## Only-the-correct-stuff (C7.1)

`docs/BUILD_PLAN.md` + the §14.4 decision log are the single authoritative spec. On ANY conflict between an
older `docs/` file and the plan, **THE PLAN WINS** — update the older file or mark its top
`SUPERSEDED — see BUILD_PLAN.md`. `assets/` (DillonBaird, Kenney, MAS `AvionicsSystems`) is REFERENCE — look,
don't ship; the only shippable art lives in `plugin/GameData/DragonScreen/art/`. Where a copy exists twice, the
REPO copy is authoritative.

## The loop

`/next` → read `CLAUDE.md` → take the FIRST non-DONE `REGISTER.md` line as THE task → read its pointed-to
section end-to-end → do only that → verify (C1.3 gate) → update `REGISTER.md` → commit (GitHub Desktop) → STOP
→ new chat. One task per fresh chat; **[O]** on Opus, **[S]** on Sonnet. Build-hold until an explicit owner
build-go (T0 + T1 are harness/docs work and are exempt).
