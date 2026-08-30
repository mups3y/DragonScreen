# SESSION HANDOFF — 2026-08-30 (MechJeb capability build — flight-used wiring COMPLETE)

> **NEXT SESSION START HERE**, then read `docs/CAPABILITY_BUILD_BACKLOG.md` (the ordered tracker, ✅s current)
> + `docs/MECHJEB_CAPABILITY_CHECKLIST.md` (Chris's ticks) + `docs/MECHJEB_MASTER_MAP.md` + the memory.
> Governing rules unchanged: pure-first + headless, ONE change class per campaign, §8 output before code, 3-tick
> (nothing "done" until flown), verify claims against live code before editing, **Settings page is LAST**.

## HEADLINE — the dead-gold wiring is DONE
Chris flew hundreds of missions with ~zero progress; root cause was ~90 headless-proven pure modules built and
wired into flight NOWHERE. This session finished the audit and **wired every flight-USED capability that was
dead gold**, verified the rest are already present (or covered better by our own code), and installed the DLL.
What's left is UI/settings-tier (Chris deferred to LAST), the one big PVG build (UPFG is the working interim),
and flight-gated tuning that needs Chris to fly. **The autopilot is not missing wired capability anymore.**

## LANDED THIS SESSION (built → headless → wired → installed → committed)
1. **⭐ Lambert → rendezvous mid-field intercept** (`d832f57`) — `pure/RvIntercept.cs` (tof scan + **transfer-
   periapsis FLOOR GATE** so a plan can never route through re-entry + cost cap, over the tested
   `Maneuver.InterceptDv`) + `RendezvousControl.TryLambertIntercept` (closed-loop on a latched arrival UT).
   Headless `RvInterceptTest` (10). Tunable `UseLambertIntercept` **DEFAULT OFF** (CW/Hohmann stay default).
2. **⭐ NavFilter → DockingControl terminal rel-nav** (`2ee1f14`) — plus the **terminal sensor handoff**
   (`NavFilter.TerminalSensorNoiseM`: rel-GPS→DragonEye LIDAR range-scheduled 1σ, cm-class in close so the
   sub-metre dock survives) the NavFilter header had flagged. `NavFilterTest` now 13. Tunable `UseNavFilter`.
3. **SafeLandingSite → nominal return LZ** (`04d96d7`) — shared `src/LandingSiteScan.cs` (ONE copy of the F4
   water-gate fix, used by abort AND return) + `EntrySteering.SetSplashTarget` so the lifting entry steers at a
   real open-water site, not the stale orbital target. Tunable `UseSafeLandingSite`. **Authority** verified
   SUPERSEDED (BetterController's own √-stopping curve is the live path) — recorded, no redundant wire.
4. **Deployables** (`b17d75d`) — `Actuator.DeploySolarPanels/RetractSolarPanels/DeployAntennas` (direct
   ModuleDeployablePart, idempotent, safe no-op on fixed panels) + `DeployablesControl` (deploy on a stable
   outbound orbit, retract before the return deorbit), wired at FlightDriver. Tunable `UseDeployables`.
   Early-MECO verified ALREADY PRESENT (Ascent stages on `MecoSurfaceSpeedMps`=1900 → keeps booster landing fuel).

## AUDIT RESULT — every ticked capability accounted for (`MECHJEB_CAPABILITY_CHECKLIST.md`)
- **Wired this session (flight-used dead gold):** Lambert · NavFilter→dock · SafeLandingSite→return · Deployables
  (+ PWPF/RcsPulse `1d0f613` + NavFilter→rendezvous `e99ed21` from the prior pass).
- **Verified already present / [HAVE] / covered by our own (better):** VesselState, BetterController, UPFG,
  StageStats, warp, DeorbitBurn, arrestable-rate, q·α, ascent FSM, launch-clamp+ullage+unstable-ignition gate,
  differential throttle, coplanar launch-window (launch-into-plane IS wired), Authority (superseded), early-MECO,
  **ReentrySimulation/Landing-Predictions → our `Trajectory`** (measured drag, not modelled), ThrustForDv →
  our closed-loop-on-measured cutoff (better than open-loop feathering for RCS burns).
- **UI / settings-tier — DEFERRED to LAST (Chris's call), not flown by an autopilot that already aims itself:**
  SmartASS + Smart RCS presets · Translatron speed-hold · Attitude-Adjustment gain surface · Flight-Recorder
  graph · Info-Items catalog · Debug arrows · Trajectory draw · Node Editor. These live on the Settings/UI page.
- **The one big standalone BUILD remaining:** **PVG/PSG optimal-ascent optimizer** (+ its ODE/AutoDiff/root-find/
  terminals stack). UPFG is the working interim — do NOT rush a half-build; it deserves its own focused campaign.
- **Deprioritised by Chris:** booster land-at-target/land-somewhere ("let it fall with landing fuel"); the capsule
  return landing is covered (SafeLandingSite + lifting entry).

## WHAT'S LEFT (in priority order)
1. **Flight-gated enable + tune** (needs Chris to fly — these are WIRED but default-off or single-value-untuned):
   - `RendezvousControl.UseLambertIntercept` → turn ON, read the LAMBERT logs, tune the band/tof.
   - Read the **NavFilter est-vs-truth logs** (rendezvous `NAV rel-filter`, docking `DOCK rel-filter`) — confirm
     the estimate tracks before trusting it.
   - `ReturnControl.UseSafeLandingSite` LZ + the **R6 bank-steering signs** (RollSign/RollRefSign/CrossSign) to the
     recorded footprint; the **RETURN corridor R2 (entry-FPA)/R4 (survivability)** — ledger still 0 brought home.
   - Rendezvous **`FlightDriver.LaunchNodeSign`** (launch-to-plane node likely 180° off — "rendezvous doesn't work").
2. **The Settings-page TUNING tab** (Chris: LAST) — expose the tunables live in flight. This is now THE remaining
   non-flight-gated, non-PVG work, and it's the accelerator that collapses the flight-tune loop. A careful UI
   build (the screens have their own system — `pure/Pages.cs`, `docs/UI_AUDIT.md`); worth Chris's input on layout.
3. **PVG optimizer** — the big build, when Chris wants to invest in it. UPFG flies fine meanwhile.

## HOW TO PROCEED
- Best next value is a FLIGHT: enable `UseLambertIntercept`, fly a rendezvous, read the LAMBERT + NAV logs, then a
  return with `UseSafeLandingSite` to tune the bank signs + corridor. That converts the wired-but-default-off work
  into Tick-3 fact and unblocks the return ledger.
- Then the Settings tuning tab (own campaign), then PVG (own campaign).

## HOUSEKEEPING
- **All committed** through `b17d75d` (chain: `d832f57` Lambert · `2ee1f14` NavFilter→dock · `04d96d7`
  SafeLandingSite+Authority · `b17d75d` Deployables). **DLL INSTALLED** — KSP needs a full restart.
- ⚠ **Commits need a GitHub Desktop push** ([[push-via-github-desktop]]) — CLI `git push` hangs on auth. Push the
  whole chain `2918d39 … b17d75d`.
- Build: `python build.py test` (headless, also compiles the glue) / `install` (full + copy; needs KSP closed).
  731239 headless checks green; glue compiles clean.
