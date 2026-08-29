# DragonScreen — MASTER FIX PLAN (Grok end-to-end assessment + my verifications)

> This REPLACES the earlier plan body (which Grok correctly flagged as stale). It is Grok's beta-mode end-to-end
> assessment, with each claim I've **VERIFIED against source** marked. Execute campaign-by-campaign; one change class
> each; §8 before code; Tick-1 headless-green → Tick-2 approve → Tick-3 flight; **re-verify file:line on the live HEAD
> before editing** (line numbers are as of the review, HEAD `09778bf`). Do NOT pre-build `[H]`/`[DG]` contingencies.
> Legend: **[V]** build now · **[H]** measure first · **[DG]** needs a NEW flight/dump · **[UNFLOWN]** coded, Tick-3 owed.

## Already true (do NOT re-open)
Ascent→orbit inc 51.64 (L1); dual-flight control (H1); H1b mode-fix (FIXED⚑ unflown, `12a2e7f`); **F4 already coded**
(`AbortControl.cs:439`); I1/I3 (C0); U1 (`98cf500`); abort chutes+RCS (C1/C2); docking signs (unflown); B1 orbit-pos;
**page router IS wired** (`DragonScreenMonitor.cs:79` comment lies); VEHICLE OVERVIEW exists.

## ⭐ MY VERIFICATIONS THIS PASS (Grok was right)
- **N1 [V] CONFIRMED — the console is a 4-command stub.** `_AutopilotStub.FlightCommands.Run` handles only Abort/
  DepressResponse/DeorbitNow/WaterDeorbit; all other panel buttons `return false` → REFUSED-flash. `CancelAllSequences()`
  = hardcoded `false`. `VehicleSystems` implements ToggleBus/ToggleString/ResetBus/SuppressFire/FireResponse but is
  NEVER called from `Run`. → **Campaign 3.**
- **N2 [V] CONFIRMED — engage lamps are stubs.** `AutoPilot/StationApproach/DockingOps.Engaged` all `return false`;
  only `DeorbitOps.Engaged` is real; `BoosterRecovery.Tracked` returns null. → **Campaign 3.**
- **C2a split [V] (Campaign 1.0 done, CSV 155116 realtime PHASING, n=1945):** FAR-field (>100 km) att_point **p95 13°**
  but **69% of firing attitude-only** (prograde-hold chatter = the real waste). NEAR-field (<100 km, CW) att_point **p95
  78°** — off-prograde **by design**, only 24% attitude-only. → the fixable waste is far-field prograde-hold; near-field
  78° is NOT a bug. Confirms Grok's N3 + fork table.

## BUILD STATUS (2026-08-29, all headless-green + installed, UNFLOWN)
- ✅ **Campaign 1(a)** — far-field coast attitude gate (`RvCoast`, `fa67023`). *(1(b) T14 inertial pre-align = follow-on.)*
- ✅ **Campaign 2** — shroud-spam idempotency + `Actuator.CloseNoseShroud` + L6 sep-fix (`e8a570e`). *(U2/U3 deferred.)*
- ✅ **Campaign 3** — console dispatcher `FlightCommands.Run` → real `Systems` handlers + engage lamps (`8cc4f62`).
- ✅ **Campaign 4** — VERIFIED, both Grok items **FALSE ALARMS** (2026-08-29). Globe is NOT mirrored (its strips assign u→screen the reverse of Quad — a trial swap put east/India on the LEFT, the preview caught it, reverted); the orbit "gap" is a rotation-corrected `min(period,3h)` spiral, NOT a closable ellipse (`(n-1)→0` = spurious chord). **No NavPage change.** Kept the preview `DrawImage` reversed-U fix (the flat-map texture was silently skipped in every preview) + a per-view u-convention regression test. **No reinstall needed** (shipped behaviour unchanged; NavPage.cs comment-only).
- ✅✅ **Campaign 5 — RESOLVED-VERIFIED (Tick-3, flight `003648`, `a4875e9`+`f12bd32`).** Root = the **MVac RealFuels minThrottle floor 0.3854** (the g-cap returned an ENGINE throttle as a MAIN throttle → felt g = setpoint×1.107), fixed by mapping the cap through the floor. CONFIRMED: S2 peak 4.53→**4.103** (flat to SECO, throttle 0.611); S1 **3.499**. Then FIDELITY: raised `S2GLimitG` 4.1→**4.5** (real Crew Dragon peak), installed.
- ✅ **Campaign 6 (RCS authority) — BUILT + INSTALLED (`c42ed20`, UNFLOWN).** MEASURED (003648): the loop's authority is pinned at ~2 N·m for 91% of RCS-on ticks → `act_pitch` saturated 51% (bang-bang chatter, NOT "latent"). `AttitudeController.ControlTorque` now takes RCS authority = **max(stock report, per-axis geometric)** always (no gate), alloc-free; the existing SmoothTorque EMA is the "hysteresis." Confirm flight owed (rendezvous/deorbit): `ctrl_tq_*` ~12–20 kN·m, `act_*` unpinned, less RCS fuel.
- ▶ **No-flight campaigns are now DONE (1a/2/3/4/5/6).** Remaining is flight-gated tuning.
- Flights owed: **6-confirm (RCS authority)**, 7 (H1b confirm), 8 (dock), 9 (return), 10 (FDIR-acting).

## Campaigns (execute in this order)

**Campaign 0 — CONFIG READ** *(reference, no code, no flight)* — do in the session that touches H1b or fidelity.
- **A-ign [H]:** read the live ConfigCache / the three octaweb `ModuleEnginesRF.ignitions` + part TEATEB remaining →
  decides whether the H1b mode-fix can light (per-mode/−1 = yes; shared-1 = no → then off-focus RF). **Don't pre-build.**
- **V0–V4 [DG-dump]:** Draco `thrusterPower`, octaweb/MVac thrust, `gimbalRange`, chute count, S1 cold-gas vs
  `docs/VEHICLE_AUDIT.md` §C. Don't change cfg until the dump is committed.

**Campaign 1 — RV-RCS** *(Phase-FSM/control)* — **THE NEXT BUILD.** Fixes C2a; unblocks F3/L2.
- **1.0 [V DONE]** the split above. Waste = far-field prograde-hold chatter (69% attitude-only, 13° err).
- **1.1 [V code / root now confirmed FAR-FIELD]:** in `RendezvousControl.FlyFarField`, during PHASE/COAST when
  time-to-burn > slew+ullage, `Steering.Release()` instead of holding prograde every tick (`:184`). **T14 safety:**
  before the warp into TRANSFER, align to the **inertial** burn vector and warp HOLDING it (de-rotate via
  `Planetarium.rotation`), size the lead from real slew (not the 12 s `BurnPlan.BurnLeadS`, too short for a 100° Draco
  slew — `TIME_WARP_RESEARCH.md` G2). ⛔ coast-release WITHOUT the inertial pre-align = mis-pointed TRANSFER (footgun).
  Do NOT touch near-field CW pointing. Extract a pure `shouldHold(phase,timeToBurn,slewS)` for headless tests.
- **1.2 [H] L2 pe-drop:** instrument the orbital-frame Δv of attitude pulses (when trans=0) — do NOT "null" it here.
- **1.3 [DG] F3** CW terminal — after MMH survives to ~100 km.

**Campaign 2 — GLUE-SMALL** *(UI/actuator glue)* — side-stream, may parallel 1 (only `Actuator.cs` overlap).
- **shroud-spam [V]:** `Actuator.OpenNoseShroud` `Progress<0.5→Toggle()+log` self-fights while opening; `AbortControl`
  calls it **every tick unlatched** (`:201/236/354`). Fix: true edge (skip if open OR opening; log only on real change)
  + latch abort callers. Same for close.
- **UI-shroud [V]:** add `Actuator.CloseNoseShroud` (none exists; only a private `ReturnControl.cs:332` copy); relabel
  `PanelMap.cs:144` → **TOGGLE SHROUD**; wire the Run() case (needs Campaign 3 or fold the actuator+Run case here).
- **U2 [V]:** `Pages.cs:1082` `Alarms.Low(Propellant01)` cautions on the near-spent ASCENT stage — suppress while the lit
  stage is ascent, or alarm on MMH/NTO (R3 cols). PageTest.
- **U3 [H/low]:** `CabinEnvironment.cs:183-185` `watts=PowerFlow*120`; 0 W = flow≈0 (arrays≈load), not a split bug. Defer.
- **L6 [V]:** `MissionConductor.LogSeparation` `FindById(dragonId)` stale post-sep → CoM-diff 0. Compute sep from the two
  live loaded vessels, not a cached id.
- **F4:** DROP (already coded).

**Campaign 3 — PANEL-DISPATCH** *(command dispatcher)* — wire `FlightCommands.Run` → `VehicleSystems`. Power/String/Reset
→ ToggleBus/ToggleString/ResetBus; SuppressFire/FireResponse; Mains/Drogues/CutMains/FirePyro → Actuator; EnableBackup*/
EntryReboot → the existing static flags; Breakout → RequestAbort/Retreat; Cancel → `CrewProcedureOps.Disengage()`. Point
the engage lamps at the real conductor (`CrewProcedureOps.Engaged`+phase; `MissionConductor` booster). Delete the stub
"autopilot was deleted" header lie. Headless `FlightCommands` tests per command. ⛔ don't change abort FX.

**Campaign 4 — NAV-DRAW** *(display)* — ✅ DONE 2026-08-29, both items **FALSE ALARMS**. (1) Globe NOT mirrored: its
strips assign u→screen the REVERSE of `Quad`, so it needs NO swap — a trial swap put east/India on the LEFT (preview
caught it), reverted; both projections east-on-right, but the flat map's `Quad` draws `uLeft=UMax` while the globe draws
`uLeft=uMin` — opposite conventions, both correct. (2) Orbit polyline: the overlay is `VesselData.GroundTrack`, a
rotation-corrected track of span `min(period,3h)` — a spiral / sub-period arc, NOT a closable ellipse, so `(n-1)→0` would
streak a chord across the ~22° rotation seam. Left open. **Kept:** preview `DrawImage` reversed-U render fix (the flat-map
texture was silently skipped in every `build.py preview`) + `PageTest.NavTexture` fencing each view's u-convention.
⛔ Do NOT "unify" the two views' u-order — that IS the mistake this campaign caught.

**Campaign 5 — G-TAPER** *(throttle law)* — ✅ DONE 2026-08-29 (`a4875e9`), but the plan's premise was WRONG. The measurement
(155116 + 144114 + 134620) showed the overshoot is NOT limiter lag and needs NO predictive taper: it is the **MVac RealFuels
minThrottle floor 0.3854**. RF remaps main→engine throttle `minThr + t·(1−minThr)`; `ControlLaw.ThrottleLimit`'s g-cap is the
correct ENGINE throttle but was returned as a MAIN throttle → felt g = setpoint·(minThr+t·(1−minThr))/t = 4.1·1.107 = 4.53.
Verified 3 ways (flight ratio 1.104–1.107, config floor 0.3854, currentThrottle 0.8666=formula). FIX: map the cap back through
the floor + `AscentControl.MinThrottle01` reads the field. ControlTest asserts felt g = setpoint; DispersionTest fuzzes the
floor. NEEDS a confirm flight (peak ≈4.1). Then FIDELITY (Chris): raise `S2GLimitG` toward real ~4.5 g. ⛔ still do NOT LOWER it.

**Campaign 6 — ATT-TORQUE** *(authority estimate)* — ✅ DONE 2026-08-30 (`c42ed20`). MEASURED the "latent" flicker to be a
REAL saturation: `ctrl_tq_pitch` median 1.8 N·m, 91% of RCS-on ticks <100 N·m, `act_pitch` |act|=1 for 51% of ticks (003648).
`AttitudeController.ControlTorque` now takes RCS authority = `max(stock report, per-axis geometric r×F)` ALWAYS (dropped the
`rcsReported < RcsTorqueFloorNm` gate the ~2 N·m flicker sat above), non-RCS providers keep the report, one alloc-free pass
(dropped `Modules.GetModules`). The "hysteresis" the plan asked for ALREADY existed — the `Compute()` SmoothTorque=0.10 EMA
(rises smoothed, drop-to-0 instant). Glue change (compile-green, not headless-testable). Confirm flight owed. F1 deorbit re-fly
should now hold retrograde (the same authority under-estimate mis-pointed those burns).

**Campaign 7 — H1b FLIGHT CONFIRM** *(Tick-3)* — no new ignition code until the next booster CSV. Pass = `eng_ignited=1`
ThreeEngine entry / CentreOnly landing. Fail → Campaign 0's numbers decide; only THEN research RF `isActiveVessel`. H1c
couples to a lit gimbal — measure coast RCS after a pass; no blind N₂.

**Campaign 8 — DOCK-TERMINAL** *(prox-ops)* — after RCS budget survives. F3 tune [DG]; **N4 [V]:** guard the `Docked` fly
step (`DriveActivePhase:309` ticks DockingControl for Docked too) — if `DockedSide.Docked(v)` release+return. S2 signs unflown.

**Campaign 9 — RETURN/ENTRY** — F1 re-fly after 6; S3 roll/cross signs [SIGN/DG] (safe failure); B8 CourseCorrect off until
an entry CSV; G7/M6 g-band [DG-low]; chutes Tick-3.

**Campaign 10 — FDIR-ACTING** *(safety spine)* — do NOT set `FdirActing=true` until a flight shows zero false aborts on a
good ascent. One class, no threshold retune in the same patch.

**Campaign 11 — FIDELITY** *(vehicle cfg)* — after Campaign 0 dump. V1 Draco thrust = **Chris decision**; V2/V3 reconcile;
V4 chutes; M5 MonoProp leave.

**Campaign 12 — DOC-ROT** *(docs, anytime)* — register: F4→FIXED⚑, I1 status vs C0, H1b old-EngineMode text; stub headers;
`DragonScreenMonitor:79`; `ULTIMATE_PLAN` B1–B4; `DockingControl:14` SAS.

**Campaign 13 — DEFERRED (do not build until asked)** — suit-leak page, docking easter-egg, B9/M2, B2 aero-feed, B6 NavFilter,
B5 multi-UPFG, B3 per-thruster RCS, `AutoAdvanceGates` (restore false for crew-in-loop), art DXT dims.

## Dependency order
`0 → 1 → 8 → mission`; `1 → 6 → 9 → 10`; `2 → 3 → console`; `4/5/7/11/12` independent-ish.
**Single next build: Campaign 1** (1.0 done → 1.1, far-field confirmed).

## Agent DO-NOT
`att_err_deg`≠pointing (it's AoA); `AttitudeReadyDeg=5`≠the hold (0.1° `ControlLaw`); don't lower `S2GLimitG`; don't null
L2 pe in the RCS patch; don't mix L4 with rendezvous; don't pre-build off-focus RF / force-focus; no blind booster N₂;
don't set `FdirActing` on with anything else; don't "fix" SURPRESS FIRE (model art); don't change abort FX; re-verify
file:line on the live HEAD. **NAV: the globe is NOT mirrored and its orbit ring is NOT a closable ellipse — don't swap
the globe's u to match the flat map, don't add `(n-1)→0` to `ProjPolyline` (Campaign 4 proved both wrong).**
