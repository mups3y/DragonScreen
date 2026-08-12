# The numbers to fly the booster against

Measured from `bb_booster_001..008` in `quarantine/blackbox_flightdata` — F9I's own black box, from
the vehicle that lands 0.34–0.56 m from the pad. Eight flights, near-identical, so these are the
profile and not one good day.

**Use these instead of reasoning about what the booster ought to do.** Every time this project has
argued from physics instead of reading the corpus it has cost a flight.

| phase | secs | aoaRetro avg | roll travel | peak roll | ours 2026-08-12 |
|---|---|---|---|---|---|
| flip + boostback | 39 | 106.7° | **138°** | 14.9 °/s | 738° |
| coast, nose up | 33 | 123.6° | **52°** | 2.9 °/s | — |
| coast, guided | 75 | 18.6° | **50°** | 2.9 °/s | 240° (whole coast) |
| entry burn | 14 | 2.5° | **94°** | 24.0 °/s | 17° |
| descent | 48 | **6.7°** | **109°** | 9.4 °/s | 428° |
| landing burn | 19 | 0.4° | **0°** | 0.1 °/s | 92° |
| **total** | | | **443°** | | **1515°** |

## What these settle

**The booster should roll 443 degrees, not 1515.** We are 3.4x over, and it is not evenly spread:
flip+boostback is 5.4x, descent 3.9x, and the landing burn should be dead still at zero while ours
rolls 92 degrees.

**The descent flies 6.7 degrees off retrograde on average.** `F9L_AOA = 15` is a CEILING that rarely
binds — which independently confirms the conditional-rebuild port. A descent commanding a steady 15
degrees is commanding more than twice what F9I flies.

**`aoaRetro` sweeping to 123.6 degrees during the nose-up coast is correct, not a fault.** F9I is not
tracking retrograde there; `AtmGNC:425` is `lock steering to up` until vertical speed passes −50.
Our own coast already does this. A large angle-to-retrograde in that window is the profile working.

**`ctlRoll` is 0.000 in every phase** because F9I steers through kOS cooked steering, which bypasses
`FlightCtrlState`. There is no command data in this corpus — only response. Do not look for it again.

## The trap this corpus closed

Three separate times I looked for the coast and descent settings in `Reentry1` and `Flip2` and
reported "no citation available". **Both functions have no callers.** The live chain is
`Flip1 -> Boostback -> AtmGNC -> Land`, and the settings were in `AtmGNC` the whole time:

```
AtmGNC:420   torqueepsilonmax 0.04 / min 0.02     coarse deadband for the nose-up hold
AtmGNC:425   lock steering to up                  until verticalspeed < -50
AtmGNC:428   DeployGridFins()                     on vertical speed, not a timer
AtmGNC:431   torqueepsilonmax 0.0002 / min 0.001  tight deadband for the guided descent
AtmGNC:434   rollts to 10                         <-- the descent roll setting
```

`rollts 10` is now ported. **The torque epsilons are not** — F9I's second pair has max 0.0002 below
min 0.001, which is either a typo or a field whose meaning I have not established, and guessing at a
controller deadband is how the roll got worse the first time.
