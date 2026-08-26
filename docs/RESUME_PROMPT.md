# Next-session resume prompt

Paste the block below at the start of the next session to pick up the autopilot rebuild exactly where
it left off. (Updated 2026-08-26 — ALL pure layers L0–L7 AND ALL glue seams 1–6 complete + installed; what
remains is IN-GAME proving flights + tuning. When something changes, update "WHERE WE ARE"/"DO NEXT".)

---

```
Resume the Crew Dragon autopilot rebuild (project "CLAUDE"), RSS/RO DragonScreen.

FIRST, read in full: docs/AUTOPILOT_REBUILD_PLAN.md (the governing plan — §8b execution log
has the live progress; §5 is the research-derived Constants Register) and the memory
dragonscreen-autopilot-rebuild-plan. Then continue the build.

WHERE WE ARE: the old autopilot was DELETED; we're rebuilding FRESH, bottom-up, headless-tested.
DONE and green: L0 (verified-reuse), L1 nav (impact predictor w/ measured drag+lift, q/Mach,
authority), L2 control (quaternion-PD on the arrestable-rate bound, throttle bucket+g-limit, RCS),
L3 ascent (S1 DM-1 pitch program + phase FSM; S2 UPFG ported from the PEGAS primary source),
L3 booster (hoverslam + grid-fin steering + descent FSM), L3 rendezvous (LVLH + CW two-impulse +
Hohmann + named-burn FSM), L3 docking (glideslope servo + R-bar→V-bar L-approach FSM), and
L3 RETURN, L4 MODE MANAGER + CREW GATES, L5 FDIR, L6 SELF-CAL, and L7 INSTRUMENTATION
(pure/FlightRecorder.cs — 60-column per-flight CSV schema + invariant formatting + a Put* filler per
controller). ⛔ ALL PURE GUIDANCE/AUTOPILOT LAYERS L0–L7 ARE COMPLETE, fresh, and headless-green
(~900+ checks). The CoM shifter (AdjustableCoMShifter) is engaged ONCE before entry and never toggled to
steer.

KSP GLUE — built in SEAMS (game-side, not headless-testable; each seam verified in-game before the next).
DONE + installed: SEAM 1 (FlightDriver.cs [KSPAddon(Flight)] host survives handover; CrewProcedureOps.cs the
REAL conductor; FlightLog.cs per-flight CSV). SEAM 2 ASCENT (Steering.cs SAS inner loop + ENU/pitch-heading;
AscentControl.cs = S1 pitch program on the LaunchAzimuth heading, max-Q+g-limit throttle, MECO→stage, S2
ignition, UPFG insertion, SECO on measured Pe, Dragon decoupler, then PhaseComplete). SEAM 3 BOOSTER
(BoosterControl.cs = flies the separated S1 when it is the active vessel — flip→entry burn ThreeLanding→aero
descent→hoverslam CenterOnly; engine modes selected ABSOLUTELY by Activating the matching-engineID
ModuleEngines while off, only on a mode change, one ignition per mode, NEVER NextEngineMode; fins+legs by
capability). FlightDriver throttle authority generalised (SetThrottle/ReleaseThrottle), shared by ascent +
booster; a lone booster dispatched before the conductor. DLL 223.0 KB, installed. Attitude inner loop is
stock SAS for now (guidance is ours) — pure ControlLaw+Authority loop is a later swap.
⚠ First-cut items to VALIDATE against the CSV, one change per flight: UPFG Iy plane normal (=−(r×v)), SECO
cutoff, ENU heading sign (SelfCal.SteerSign guards), StageManager order; booster = retrograde-hold+hoverslam
(no droneship targeting yet — that + the engine-mode Activate/Shutdown behaviour + booster avionics are what
to confirm).

SEAM 4 RENDEZVOUS (RendezvousControl.cs = Fly(Phasing): LVLH from the targeted station via pure/Lvlh,
Rendezvous.Guide named-burn Δv on the Dracos, ATTITUDE-FIRST-THEN-TRANSLATE, opens the nose shroud before
any Draco burn, hands to the G9 gate at ~7.5 km; FlightDriver gained RCS SetTranslation/ReleaseTranslation).
DLL 225.5 KB, installed. ⚠ validate: the RCS translation axis/sign (ForwardSign default −1). Env note: a
DRONESHIP is already placed + the Cape Canaveral mod gives RTLS landing pads — so booster targeting is now
unblocked (VehicleParts.IsDroneship marker "Droneship"; RTLS pad is a static position).

BOOSTER TARGETING refinement DONE (src/BoosterTargeting.cs — L1 Trajectory impact predictor with a measured
ballistic coeff, rotation-corrected, vs the droneship/targeted RTLS pad → GridFin steers the booster onto the
deck; BoosterControl feeds it + measures BC while coasting). ⚠ validate: BC measurement, rotation-correction
sign, CrossSign.

SEAM 5 DOCKING (DockingControl.cs = flies the L-approach with pure/DockControl glideslope servo, one leg at
a time keyed on CrewProcedureOps.NextGateId → WP0 400m below/WP1 220m/WP2 20m/contact on the Dracos, ring
pointed at the port, PhaseComplete per WP so the G10/G11/G12 gate releases the next leg; DockedSide.Docked =
capture). DLL 229.5 KB, installed. ⚠ validate: RCS translation axis SIGNS (RcsRight/Up/FwdSign), servo gains,
tolerances; KOS auto-abort not wired (crew ABORT on the gate is the path).

SEAM 6 RETURN (ReturnControl.cs — return Phasing→FlyDeparture: undock the docking node + Departure CW burns
on Dracos; Entry→FlyDeorbitEntry: DeorbitGuidance trunk-jettison→close shroud→retrograde Draco burn on
measured Pe, then lifting entry ENGAGING the CoM shifter Descent Mode via AdjustableCoMShifter ToggleMode,
shield-forward; Drogues/Mains/Splashdown→FlyChutes: Chutes state-based ModuleParachute deploy→splashdown.
CrewProcedureOps.IsReturn (set at G14) splits return-Phasing from outbound rendezvous). DLL 235.0 KB,
installed. ⚠ BANK-ANGLE ENTRY STEERING NOT WIRED (stable shield-forward + CoM engaged + chutes; S-turn bank
is the refinement — SAS holds a direction not a roll, needs a roll loop).

✅ THE ENTIRE AUTOPILOT IS BUILT + INSTALLED — pure L0–L7 + glue seams 1–6 (pad→orbit→booster recovery+
targeting→rendezvous→docking→undock→departure→deorbit→entry→splashdown). DLL 235.0 KB.

BANK-ANGLE ENTRY STEERING DONE (src/EntrySteering.cs = L1 footprint predictor WITH LIFT + measured BC,
rotation-corrected, vs the capsule's splashdown target → Entry.Guide down/cross error; + measured-bank; the
roll loop banks to σ via FlightDriver.SetRoll st.roll while SAS holds retrograde). DLL 237.5 KB, installed.

DO NOT read the deleted tree. ✅ THE ENTIRE AUTOPILOT IS BUILT + INSTALLED (pure L0–L7 + glue seams 1–6 +
booster targeting + bank-angle entry). DLL 237.5 KB. What remains is IN-GAME.

DO NEXT: IN-GAME PROVING FLIGHTS + TUNING (no more blind glue). Fly Crew-2 on AUTO SEQUENCE, read the
DragonScreen_capture/*.csv, fix flagged first-cut items ONE change per flight (NO Python sims):
(1) RCS translation SIGNS (RendezvousControl.ForwardSign, DockingControl.RcsRight/Up/FwdSign, ReturnControl.
ForwardSign) — a burn/translate the wrong way; (2) UPFG Iy plane normal + SECO cutoff + ENU heading sign
(AscentControl); (3) booster BC/rotation/CrossSign (BoosterTargeting); (4) deorbit target Pe; (5) entry
bank/roll SIGNS (ReturnControl.RollSign, EntrySteering.RollRefSign/CrossSign) + roll gain (RollKp). Target
the droneship (booster) and a splashdown recovery-ship/waypoint (entry) so the predictors have a target.
REMAINING REFINEMENTS: the pure ControlLaw+Authority attitude loop replacing the SAS inner loop
(src/Steering.cs); roll-ONLY entry control (aero-trim AoA); KOS auto-abort in docking. Build fresh
referencing the pure controllers.

NON-NEGOTIABLE RULES:
- ⛔ DO NOT read, reference, or resurrect plugin/_deleted_autopilot/ — it's the old unproven code.
  Build only from the research docs + primary sources (cite them). The SCREENS were kept.
- FULL CONTROL AT ALL TIMES: the guidance always outputs a definite attitude — never floating/drifting.
  The Dragon has no reaction wheels (16 Dracos share rotation+translation) → attitude-first-then-
  translate, never both at once.
- Actuate BY CAPABILITY from the real craft (docs/CRAFT_DUMP_VEHICLE_MAP.md): Draco = MMH+NTO;
  open the nose shroud before any Draco burn; octaweb has 1 ignition per engine mode (landing =
  CenterOnly, no 3→1 mid-burn, select mode absolutely while off, never NextEngineMode).
- Constants come from the research (plan §5 / live ConfigCache), NOT old tuned values. Instrument
  everything the same pass. No Python sims — validate with headless C# tests. Do NOT commit/install
  unless I ask.

The pure stack is done; the KSP glue is the last build step, then flight testing (one change per flight,
validated against the FlightRecorder CSV — no Python sims).
```
</content>
