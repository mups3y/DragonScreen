# DragonScreen — Task Register

The living task list for the build. **One task at a time** (C1.1): the first non-DONE line below IS the task.
`/next` reads this file. Full detail for every task: `docs/BUILD_PLAN.md` (Part C = the protocol, §1–§14 =
Part A research, §B1–B15 = Part B research).

**Status:** `TODO` · `DOING` (at most one) · `DONE` · `NEEDS-WORK` (+ one-line note).
**Model:** `[O]` = Opus · `[S]` = Sonnet (C3). Escalate [S]→[O] if a task stalls; never downgrade an [O].

⚠️ **LIVING** (C5): split any task that won't finish before compaction; append stray findings at the bottom;
never reorder past a DONE without a note.
🛑 **BUILD-HOLD** is in force — no mod code / no `install` / no glass time until an explicit owner build-go.
T0 and T1 are harness + docs work and are exempt.

---

## Setup

### T0 [O] Scaffold + consolidate (C7) — **DONE**
- **Read:** C1 / C2 / C4 / C7.  **Build:** (1) copy plan → `docs/BUILD_PLAN.md`; (2) copy tuned Crew-2 cfg →
  `docs/reference/mechjeb_settings_type_Crew-Dragon.cfg`; (3) APPEND C1 rules (1–11) to existing `CLAUDE.md`;
  (4) create `REGISTER.md` from C4; (5) create the `/next` skill from C2.
- **DONE 2026-09-02 (by the overseer, one-time bootstrap):** plan + cfg copied byte-identical (`cmp` clean);
  `CLAUDE.md` appended 40→95 lines, original untouched; `/next` skill at `.claude/skills/next/SKILL.md`; no
  memory written (C1.11). Docs/harness-only → build/preview gate N/A; baseline `build.py test` logged below.
  See stray findings S1–S3. Owner to commit via GitHub Desktop.

---

## Part A — screens (§7 order, with this session's decisions)

### T1 [O] Docs reconcile (C7.1) — **TODO**
- **Read:** `BUILD_PLAN.md` + §14 + EVERY `docs/` file.  **Build:** update or mark `SUPERSEDED — see
  BUILD_PLAN.md` any doc conflicting with the plan (autopilot re-introduction, panel lighting NO-red, inert
  inferred controls, §14.1 numbers); mirror new screens + decisions into `SCREEN_INVENTORY.md` + the map
  artifact; fix `INDEX.md` (incl. S1, S3).  **DONE when:** no `docs/` file contradicts `BUILD_PLAN.md`; INDEX
  current.  **May SPLIT** per doc-group.

### T2 [S] Menu nav-index (`UiPage.Menu`) — **TODO**
- **Read:** §14.4(c) + `FigmaUI.cs`.  **Build:** grid/list of all pages.  **DONE when:** preview + nav test.

### T3 [S] Reference Content view (Cover `PhaseReference`) — **TODO**
- **Read:** §14.4(c) + §8 + `CoverPage.cs`.  **Build:** deorbit quick-ref.  **DONE when:** preview.

### T4 [O] Cover map-modes (2D/3D + camera) — **TODO**
- **Read:** §3 + `NavPage`.  **DONE when:** preview, modes switch.

### T5 [S] Vehicle Alerts + Consumables — **TODO**
- **Read:** §3 + `VehicleOverview`/`SubsystemPage`.  **DONE when:** preview.

### T6 [S] Rendezvous ellipse plot — **TODO**
- **Read:** §3 + §8 + Hohmann/Orbital.  **DONE when:** preview + nav test.

### T7 [S] Deorbit Burn Prep (reconstruct, marked) — **TODO**
- **Read:** §3 + §8.  **DONE when:** preview.

### T8 [S] Entry page (reconstruct, marked) — **TODO**
- **Read:** §3 + §8.  **DONE when:** preview.

### T9 [O] Prop thruster schematic + P&ID / systems-tree deep-views — **TODO**
- **Read:** §11b.  **DONE when:** preview.

### T10 [O] Lower-panel accuracy pass — **TODO**
- **Read:** §4 + §14.4(a,b) + `PanelButtons`/`PanelMap`/`FlightCommands`.  **Build:** lighting bright / no-red,
  audible click, SWAP + inferred-entry INERT.  **DONE when:** preview + panel test, click plays, inferred inert.

### T11 [O] Capsule turntable — **TODO**
- **Read:** §5.  **Build:** source the MaTte0 model → render sprites → drag-rotate.  **DONE when:** preview,
  drag rotates.

### T12 [S] Ascent/Launch page — **TODO**
- **Read:** §8 + §3.  **Build:** F9 schematic + event list.  **DONE when:** preview.

### T13 [O] Live-data wiring — **TODO**
- **Read:** §6 + `VesselData.cs`.  **Build:** replace placeholder constants.  **DONE when:** values live in-sim.

### T14 [O] Touch wiring — **TODO**
- **Read:** §6 + §4.  **Build:** display-only controls → real per the decisions.  **DONE when:** controls act (+ tests).

---

## Part B — autopilot (§B12.6 order; all [O])

### T15 [O] Embed MechJeb — **TODO**
- **Read:** §B2 / §B3 / §B12.1.  **Build:** pinned + privately-namespaced; headless core loads the Crew-Dragon
  cfg (`docs/reference/mechjeb_settings_type_Crew-Dragon.cfg`). Also create/point `docs/FLIGHT_SYSTEMS.md` (S3).
  **DONE when:** one core loads, no clash, cfg applied.

### T16 [O] Pure conductor core + tests — **TODO**
- **Read:** §B9 / §B12.2-3 + `MissionPhase.cs`.  **Build:** phase FSM as pure decisions.  **DONE when:**
  headless tests green.

### T17 [O] Glue driver, read-only — **TODO**
- **Read:** §B12.2 + `_AutopilotStub.cs`.  **Build:** report phase/engaged, no commands.  **DONE when:** in-sim
  phase matches, nothing flies.

### T18 [O] Wire Ascent (PVG) — **TODO**
- **Read:** §B8 / §B11.  **DONE when:** PVG flies to insertion in-sim.

### T19 [O] On-orbit ops + re-plan loop — **TODO**
- **Read:** §B10.2 / §B12.4 / §B9.  **DONE when:** rendezvous to the KOS in-sim.  **May SPLIT if large.**

### T20 [O] Docking hand-off + speedLimit ladder — **TODO**
- **Read:** §B10.3 / §B14.  **DONE when:** dock in-sim.

### T21 [O] Deorbit/entry/chutes + abort wiring — **TODO**
- **Read:** §B13 / §B10.4 / §B9.  **DONE when:** return + splash in-sim, EJECT abort works.

### T22 [O] Empirical tune (one param at a time vs §B11) — **TODO**
- **Read:** §B5 / §B7-11.  **DONE when:** profile matches nominal, the 4 `[EST]` numbers pinned into the cfg.
  **May SPLIT** per phase.

---

## Stray findings (appended per C1.1 — logged, not done)

### S1 [S] `CLAUDE.md` header predates Part B — **TODO** (fold into T1)
The original "What this repo is now" section (top 40 lines) says the autopilot was deleted and "if you find a
reference to any of it, it is stale; remove it." Part B RE-INTRODUCES it as the embedded-MechJeb conductor
(T15–T22), so that line is itself now stale and auto-loads every session. T0 appended the C1 rules below it but
(append-only scope) did not rewrite the header. **DONE when:** `CLAUDE.md` no longer contradicts Part B.

### S2 — repo has a large uncommitted working tree (informational)
The tree holds the Figma-UI rebuild (`CoverPage.cs`, `FigmaUI.cs`, `VehicleOverviewPage.cs`,
`docs/SCREEN_INVENTORY.md`, `plugin/GameData/DragonScreen/art/cover/`, ~26 files) — pre-existing, not T0's and
not touched by it. Owner to commit via GitHub Desktop; noted so a later task doesn't mistake it for its own diff.

### S3 [S] `docs/FLIGHT_SYSTEMS.md` is referenced but does not exist — **TODO** (folds into T1 + T15)
Live references point at a missing file: `plugin/src/pure/MissionPhase.cs`, `plugin/build/audit_comments.py`,
and `docs/INDEX.md` (lists it as existing). The §8 flight facts it should hold currently live only in
`BUILD_PLAN.md`. T15 creates it; T1 must at minimum stop `INDEX.md` advertising a missing file.
