# Overnight autonomous session — 2026-08-24 → resume state

## THE MANDATE (user, going to bed)
"Use the last 20 flights' data to fix the Crew-2 mission END TO END. Whatever it takes — internet,
MechJeb source, installed mods, RSS/RO tutorials. Provide a fully working, sophisticated, SELF-HEALING
and LEARNING autopilot. Wake up with as few issues as possible. Keep working until you cannot find a
single thing wrong or missed."

Constraints: **cannot fly test flights (user asleep)** → every change must be DATA-GROUNDED (the 20-flight
corpus in `DragonScreen_capture/flight_0824_*.csv` + earlier) and/or HEADLESS-VALIDATED. Do NOT guess
(the user's #1 complaint). Read code cover-to-cover (the user twice caught me grepping/skipping).
`cd plugin && python build.py test` (headless) / `install` (needs KSP restart). `assess_flight.py` auto-
diagnoses a flight.

## BUILD STATE RIGHT NOW (must fix first)
**3 tests failing** in test/FlightTest.cs — all the OLD 3→1-handover design, which the landing fix below
replaces. Update them to the new design, then the build is green:
- line ~596 "the landing burn opens on the three-engine figure" — now opens on the CENTRE engine (27.00), because we commit to one engine up front. Rewrite to expect 27.
- line ~619 "still fast: the burn stays on three" (fastLow, AccelOne=27) — now commits to ONE from the start (==1). Rewrite.
- line ~629 "slow but no room left: do NOT hand over" (slowLow, AccelOne=27) — now ==1. Rewrite (the "no room / don't hand over" concept is gone; a stage with a centre-engine mode + TWR lands on one).
Also: `Landing.HandoverReady` (pure/Landing.cs ~1251) and `OneEngineStopDist` are now DEAD (uncalled) — delete after the tests are updated. The BoosterRecovery.cs `handedOverToOne` latch still works (harmless).

## LANDING FIX — THE CRASH (in progress, code done, tests pending)
ROOT CAUSE (read cover-to-cover, flight_0824_054405): the 3→1 handover is NOT "shut 2 of 3 engines." The
octaweb is THREE SEPARATE ModuleEngines on one part — `AllEngines` 8227 kN / `ThreeLanding` 2742 kN /
`CenterOnly` 914 kN — selected by a mode switch. Handover `Three→Center` SHUTS the ThreeLanding module and
must RELIGHT the CenterOnly module. `SetOctawebMode` returns early while stepping the mode (lights
NOTHING), and the Center relight fails in the descent → ALL thrust dies at 222 m → booster falls into the
sea at 99 m/s **with ~7 t of propellant aboard**. So "not enough fuel for both burns" was a MISDIAGNOSIS
— the reserve cut works (cut at recovFrac 0.35, ~7 t reserved); the engine flamed out on the mode swap.
A mid-air module swap cannot be made gap-free.

FIX (done in pure/Landing.cs): fly the WHOLE landing burn on ONE engine (the centre) when it has the TWR,
`LandingEngines(s, have)` — returns 1 iff `AccelOneEngine > 0 && AccelOneEngine >= Gravity*MinLandingTwr`
(1.25), else 3. `EnginesFor(LandingBurn)` now calls it (no HandoverReady). The hoverslam ignition is sized
for the chosen mode (`landModeAccel` = AccelOneEngine when landing on one) so ignition/flight are
CONSISTENT (the old bug was solve-on-3, fly-on-1). One engine ignites HIGHER (~3.6 km, longer gentle
burn), the mode switches during the engine-OFF coast (no thrust gap), and a continuous radar-altitude
hoverslam flies it to the deck = the user's "radar type setup." At 34 t the centre engine is TWR 2.7 and
can modulate 40–100 %, so a soft touchdown is possible. TODO after tests: VALIDATE the 1-engine ignition
altitude with a headless sim (scratchpad, like hs_check.py) at the real landing mass, confirm it arrests
at the deck and stays above the 0.40 floor.

## FULL BOOSTER ISSUE LIST (flight_0824_054405, don't lose these)
1. Landing crash — mode-switch handover (FIXING; see above).
2. Ignition too high (dead-time lead) TRIGGERED the old handover — now moot, but sanity-check the
   1-engine ignition altitude isn't absurd.
3. "Not enough fuel" — MISDIAGNOSIS, ~7 t left at the cut. It's the flameout.
4. Entry burn OVER-SIZED: kills 1453 m/s (2272→819), 65 % of recovery fuel. Real Crew-2 is a light bleed.
   Reserve cut works (0.35). Coupled to downrange — trimming it lands the stage further downrange.
5. Roll ELEVATED: coast 177° (1.7× F9I 102), entry burn 162° (1.7× F9I 94). Known roll-authority quality
   gap (physical: roll authority ~10× below pitch/yaw). See memory dragonscreen-audit / SESSION_2026-08-18.
6. Accuracy 19 km, ALL DOWNRANGE (cross-track +0.1 km — barge is ON the track). Needs downrange trim
   (staging velocity / entry-burn sizing), NOT a barge move. Barge is correct at (32.392, -77.036).

## CONFIRMED WORKING (don't re-break)
- Launch reaches orbit: UPFG disabled (`AutoPilot.UpfgEnabledS2=false`), gravity-turn+circularise flies it
  (7/7 record). 200×198 km, inc 51.32 vs station 51.64. **Do NOT re-enable UPFG** (0/8, over-lofts).
- AUTO SEQUENCE conductor advances ASCENT→RENDEZVOUS on its own (flight_0824_043650 log).
- Barge relocated to real Crew-2 542 km / (32.392,-77.036) in 3 places: BoosterRecovery consts,
  KK_GroupCenter_Earth_"Of Course I Still Love You".cfg, and the BargeWaypoint marker follows the const.
- Reserve cut fires at 0.35 (leaves ~7 t). Dead-time landing fix works (thrust now arrives ~818 m).
- Map waypoint on the barge works ("OCISLY droneship").

## INSTRUMENTATION (this session — use it)
- FUEL now visible in RealFuels: FlightRecorder.Margins maps Kerosene/LqdOxygen/RP-1 onto lf/ox
  (a_lfFrac/oxFrac + b_lfFrac/oxFrac were reading 0 all mission).
- New cols: d_recovFrac, d_recovUnits (booster reserve), d_autoStep, d_autoOn (conductor).
- assess_flight.py auto-diagnoses: §8 fuel+reserve per phase with flags (OVER-BURNED / reserve-cut
  DISABLED / RAN DRY); §4 ACCURACY decomposed into along/cross-track with a verdict. Reads by column NAME
  so new cols are safe. `python build/assess_flight.py [file]`, `--list` lists missions.

## PLAN FOR THE REST OF THE NIGHT (phases still to work, in order)
A. GREEN BUILD — update the 3 handover tests, delete HandoverReady/OneEngineStopDist dead code. [NOW]
B. LANDING — headless-validate the 1-engine hoverslam at the real mass; confirm soft touchdown.
C. BOOSTER ACCURACY (19 km downrange) — the entry burn is open-loop on a fixed 0.35 reserve. Consider a
   closed-loop cut on predMiss (data shows predMiss falls during the burn), floor-protected; OR trim the
   natural downrange via staging. Ground it in the 20-flight predMiss/downrange data. Watch the coupling:
   less entry burn = lands further.
D. ENTRY BURN sizing + ROLL — the roll is a physical authority gap; check the coast/entry roll cause in
   the data (per SESSION_2026-08-18 it's the reorientation bleeding through weak roll authority).
E. RENDEZVOUS — UNFLOWN end-to-end. The real named-burn L-approach (NamedRendezvous.cs / StationApproach).
   Read it cover-to-cover, check against MechJeb source (Desktop/mechjeb_src) + REAL_CREW_DRAGON_MISSION.md.
   The conductor now reaches it, so it's the next thing that'll actually fly.
F. DOCK / REFUEL / UNDOCK — DockingOps, DockedRefuel, UndockOps. Read cover-to-cover, find bugs.
G. RETURN — DeorbitOps→EntryOps→chutes. The de-orbit lands ~92 km long historically (memory). Validate
   the aim + the splashdown target (real Crew-2 Gulf/Atlantic).
H. SELF-HEALING/robustness — NoSolution handling, relight retries, sanity guards, fallback paths. Make
   each phase degrade gracefully.

## HOW TO WORK (discipline)
- One coherent fix at a time; build.py test after each; keep the build GREEN.
- Ground every constant in a flight number or a headless sim. Label MEASURED / PORT / INVENTION.
- Log every change to docs/SESSION_2026-08-23.md (the running log) so the morning review is one read.
- Update THIS file's "BUILD STATE" + "PLAN" as phases complete.
