# SESSION HANDOFF — 2026-08-30 (MechJeb capability build: wire the gold in)

> **NEXT SESSION START HERE**, then read `docs/CAPABILITY_BUILD_BACKLOG.md` (the ordered tracker) +
> `docs/MECHJEB_MASTER_MAP.md` (how MechJeb works) + the memory. Governing rules unchanged: pure-first +
> headless, ONE change class per campaign, §8 output before code, 3-tick (nothing "done" until flown),
> verify claims against live code before editing, **Settings page is LAST** (Chris's call).

## WHERE WE ARE — the big pivot
Chris flew hundreds of missions with ~zero progress. Root cause found this session: the autopilot has **~90
headless-proven pure modules**, but most are **built and wired into flight NOWHERE** ("all that gold, used at
a fraction"). The mission SPINE *is* wired (FlightDriver dispatches ascent→rendezvous→dock→deorbit→entry→
chutes→abort via CrewProcedureOps), but many phases are first-cut and several capabilities are dead gold.

**Chris ticked the FULL MechJeb capability set** (`docs/MECHJEB_CAPABILITY_CHECKLIST.md`) — he wants EVERY
ticked capability **100% wired in and USED in every place it's useful** (he cut only the genuinely-N/A:
legacy attitude laws, air-breathing, SRB-drop, skip-circularization, Principia, interplanetary/Mun, rover/
aircraft). Settings → our Settings page (do LAST). MechJeb's UI is reference only.

## LANDED THIS SESSION (built → headless → wired → installed → committed)
1. **⭐ PWPF/phase-plane RCS pulse modulation** (`1d0f613`) — `plugin/src/pure/RcsPulse.cs` + wired at
   `FlightDriver.OnFlyByWire` (translation always; attitude only when engine off → never the ascent gimbal).
   The fix for the Campaign-6 Draco chatter/propellant-waste. Headless `RcsPulseTest` 11 checks. Tunable
   `UseRcsPulse`. MechJeb never does PWPF for RCS (only its hoverslam throttle) — our improvement.
2. **⭐ NavFilter → strict-fidelity rel-nav** (`e99ed21`) — the B6 Kalman rel-nav was built but used nowhere;
   now `RendezvousControl.FlyNearFieldCw` simulates the rel-GPS from truth (+noise), fuses through NavFilter,
   and flies the CW guidance on the ESTIMATE (the real Dragon pipeline). Instrumented (rate-limited est-vs-truth
   log). Tunable `UseNavFilter`.
3. **Research base:** `docs/MECHJEB_MASTER_MAP.md` (full architecture, read from source) + the **attitude-
   controller verdict** (keep **BetterController**, NOT LQR — setpoint-PID settles better + LQR chatters on/off
   thrusters; the real lever was PWPF, now built). Artifact map: https://claude.ai/code/artifact/98b9c268-63a2-4072-b8b1-d336b32e2dc7
   ⚠ the desktop `mechjeb_src` is OLDER than the installed RO DLL (verify cfg fields against the live build).

## THE WIRING AUDIT — what's still dead gold (do these next, in order)
Grepped the glue; built-but-wired-NOWHERE:
- 🔌 **Lambert** (two-impulse intercept) → wire into `Rendezvous`/RendezvousControl far/mid-field as a proper
  intercept solver (behind a tunable, default off until flight-tuned; CW/Hohmann stay default). ← **NEXT.**
- 🔌 **NavFilter → DockingControl** (terminal rel-nav) — same pattern as the rendezvous wire, next place.
- 🔌 **Authority** (per-axis control authority + arrestable rate) — VERIFY first; likely SUPERSEDED by
  BetterController's own √-stopping curve. Don't add a redundant path.
- 🔌 **SafeLandingSite** → return LZ selection (ReturnControl has none wired).
- 🔨 Genuinely-missing glue: **landing autopilot** (land-at-target/somewhere), **deployables** (solar/antenna),
  **Translatron**, **SmartASS presets**, first-class **ReentrySimulation** predictor, **early-MECO trigger**
  (booster landing-fuel), the **PVG optimizer** (big — UPFG is the interim).
- **LAST:** the Settings-page **TUNING tab** so every tunable is live-adjustable in flight (Chris deferred it;
  it's the accelerator that collapses the rebuild loop).

## MISSION-COMPLETION BLOCKERS (flight-gated — need Chris to fly)
These are WIRED but mis-tuned by a single value each; a couple of test flights lock them down:
- **Rendezvous plane:** `FlightDriver.LaunchNodeSign` (launch-to-plane IS wired; "rendezvous doesn't work" is
  almost certainly the node 180° off) — one flight reveals the sign.
- **The RETURN** (`docs/RETURN_FIX_PLAN.md`): R1 built (`DeorbitBurn` — Draco deorbit, not the empty SuperDraco).
  R2 (entry-FPA corridor, currently a too-steep fixed 50 km pe), R4 (entry survivability / 8 g + chute 506 K),
  R6 (LZ bank steering) still owed — flight-tuned. Ledger still 12 killed / 17 hand-rescued / 0 brought home.
- New: read the **NavFilter est-vs-truth log** on the next rendezvous to confirm it tracks before trusting it.

## HOW TO PROCEED NEXT SESSION
1. Continue the wiring audit list top-to-bottom (Lambert next) — each its own §8 campaign: build/verify the
   pure logic → headless → wire at the real dispatch/actuation point → `python build.py install` → flip ✅ in
   `CAPABILITY_BUILD_BACKLOG.md`.
2. When Chris flies: read the recording, fix the flight-gated single-value blockers (node-sign, return corridor),
   verify PWPF reduced the thrash and NavFilter tracks.
3. Settings tuning tab LAST.

## HOUSEKEEPING
- **All committed** through `1b77027`. Build: `python build.py test` (headless) / `install` (full + copy;
  needs KSP closed). DLL installed; KSP needs a full restart to load it.
- ⚠ **Commits need a GitHub Desktop push** ([[push-via-github-desktop]]) — the chain `2918d39 … 1b77027`
  (CLI `git push` hangs on auth).
- Dashboard "I Smell What You're Stepping In" (KLM) + the public DragonScreen site are current from earlier.
