# BlackBox Research — the DragonScreen FLIGHT RECORDER

**Owner-directed research task, 2026-09-03 (register line S59). RESEARCH + THIS DOC ONLY — no code written,
no plan edited, no gate opened or widened (C1.1, C1.12).** Governed by `docs/BUILD_PLAN.md` §B5 (the
one-parameter-at-a-time tune), §B7–§B8 (the ascent tune this recorder exists to serve), §B10.7 (MechJeb's own
FlightRecorder), §B11 (the flight-data targets), §B12 (conductor architecture), §14.4(e)/(f) (provenance and
marking) and C7 (canonical location).

> **Freshness: [SPEC — research].** A specification to build from, not built code. Nothing here is a build-go.
> The Part-B gate covers **pure code + `test` + `preview`** only; every done-criterion that can be met only in
> the capsule needs a separate owner go (C1.12). The build itself is not scheduled — see §6.1 Q4.

---

# 0. Why — the requirement, stated as the problem it fixes

**Every flight assessment this project has made so far rests on the owner's screenshots and verbal
description, and that evidence path has already produced misdiagnoses in this repository.** Three, on the
record:

| # | The claim that was acted on | What it actually was | Register |
|---|---|---|---|
| 1 | "The glass pass ran on a stale S17 DLL — re-baseline before trusting the findings." | **The premise was false.** The task was opened, argued and closed on a wrong reading of what was installed. | S36 — *CLOSED: THE PREMISE WAS FALSE* |
| 2 | "The P&ID label→value rows are mis-wired — values sit one row off." | **A legibility illusion.** The console is viewed obliquely; the rows were correctly wired and the *layout* needed the fix. Diagnosed as a wiring defect (S34), corrected to a geometry defect (S38). | S34 → S38 |
| 3 | A glass pass "checked" the screens. | Half the checklist went **unanswered and unrecorded**; what was verified can no longer be separated from what was merely looked at. | S18 |

Two of the three cost a whole task each; the third destroyed the evidentiary value of a scarce capsule
restart. The common cause is not carelessness — it is that **a description of a screen is not a measurement
of a flight.** A human watching three 5 Hz displays, from an oblique seat, at time warp, through a nine-minute
ascent, cannot produce a record precise enough to tune `PitchRate` in ±0.1 °/s steps (§B8), and should never
be asked to.

**So: visual description is NOT admissible evidence for a precision assessment.** The BlackBox exists so a
flight can be *rebuilt and assessed exactly* — performance **and** failures — with no guessing, by an overseer
who was not in the capsule and cannot ask the pilot what they saw.

Two consequences follow immediately, and they are the two halves of the design:

- **Continuity.** A parameter sampled only when something interesting happens cannot prove that nothing
  interesting happened. The continuous stream must run for the *whole* flight, fast enough that the fastest
  thing we care about — a staging transient, a saturated control loop, an abort — cannot hide between samples.
- **Discreteness.** A state change is a *fact with a time*, not a value with a rate. Sampling it in the
  continuous stream tells you it happened *some time in the last 100 ms*; logging it as an event tells you
  *when*, *what decided it*, and *on what inputs*.

Real practice reached that same split a long time ago, which is where §1 starts.

---

# 1. REAL PRACTICE — what an expert crash investigator actually uses

## 1.1 The FDR: one parameter set, many different rates

The regulator does not specify "a recorder"; it specifies **a parameter list with a sampling interval per
parameter**. The US mandatory list (14 CFR Part 121, Appendix M — 91 numbered parameters) is the clearest
published statement of the principle, and its *rate structure* is the finding worth stealing:

| Rate | Interval | Representative Appendix M parameters |
|---|---|---|
| **8 Hz** | 0.125 s | Normal (vertical) acceleration (#5) |
| **4 Hz** | 0.25 s | Longitudinal + lateral acceleration (#11, #18); pitch attitude and pitch/lateral control **positions** on newer types (#6, #12–#16); recommended air/ground sensor (#31) |
| **2 Hz** | 0.5 s | Control-surface and control-column positions (#12–#17), ground spoiler (#87), yaw-damper status/command (#89, #90) |
| **1 Hz** | 1 s | Pressure altitude (#2), airspeed (#3), heading (#4), thrust/power per engine (#9), **autopilot engagement (#10)**, **AFCS mode and status (#25)**, master warning (#30), radio altitude (#26), groundspeed (#34), engine warnings (#62–#65), loss of cabin pressure (#78), and every "selected" target (#48–#53) |
| **0.5 Hz** | 2 s | Flap selections (#20, #21), outside air temperature (#24), angle of attack on older types (#32), hydraulic pressure (#33, #77), thrust command (#57) |
| **0.25 Hz** | 4 s | **Time / relative time counts (#1)**, latitude & longitude (#39), wind speed & direction (#38), drift angle (#37), landing-gear position (#36), EFIS display format (#55), AC/DC bus status (#74, #75), computer failure (#79) |
| **~0.016 Hz** | 64 s | Selected barometric setting (#47), CG trim-tank fuel (#59), computed centre of gravity (#73), selected decision height (#54) |

Three lessons, all of which this recorder inherits:

1. **Rate follows bandwidth, not importance.** Latitude/longitude is arguably the most consequential
   parameter in a crash investigation and it is sampled at **4 seconds** — a position cannot change fast
   enough to be missed. Normal acceleration is sampled **32× faster** because a load spike genuinely lives
   inside 125 ms. Sampling everything at the fastest rate is not rigour; it is a bigger file carrying the same
   information.
2. **Mode and "selected" values are first-class parameters.** #10 (autopilot engagement), #25 (AFCS mode and
   status) and #48–#53 (selected altitude / speed / Mach / vertical speed / heading / flight path) exist so an
   investigator can ask **what the system was told and what mode it was in** at any instant. Without them,
   "the aircraft descended" is an observation; with them, "the aircraft descended *while in VS mode with
   −1200 fpm selected*" is a finding.
3. **Display format is recorded (#55, #56).** The regulator records *what the crew was being shown*, because
   an action is only assessable against the picture that provoked it. Our screens **are** the product; this is
   not an optional channel for us.

Beside the crash-protected FDR sits the **Quick Access Recorder**: same data path, no armour, easier to pull,
usually *more* parameters at *higher* rates. The NTSB–BEA co-operation memorandum instructs that QAR data
"should be handled in the same manner as FDR data … if they provide more data than the FDR recording."
**Our recorder is a QAR, not an FDR.** Nothing in a KSP flight needs crash armour, and the survivability
engineering that dominates real recorder design is absent from our problem — which frees the entire budget
for parameters and rate.

## 1.2 The CVR: what the crew did, and what they were being told

The CVR records four channels — pilot, co-pilot, a third crew/PA channel, and the **cockpit area microphone**,
which captures aural warnings, alerts and the ambient environment — plus, on modern units, all **datalink
messages** sent or received. The standard moved from 2 h to **25 h** for new production because investigators
kept losing the beginning of the story.

We have no voice. The honest analogue is not "record audio" but **record the human–machine transaction**: the
crew's input, the machine's response to that input, and the state of the display that framed the decision.

| CVR channel | Our analogue |
|---|---|
| Crew microphones (what the crew did and said) | Every touch and every console press: which screen, which page, where, which control, what argument |
| Datalink messages in/out | The dispatch result — did the command *do* anything (`FlightCommands.Run` verdict, `PanelPressKind`, lamp state) |
| Cockpit area microphone (aural warnings, alerts, environment) | The alarm / annunciator state at that instant: `Severity`, `Alarms.Mask`, the FDIR fault word, the abort overlay |
| — (no FDR equivalent, but Appendix M #55/#56 comes close) | Which page each of the three screens was showing, always |

The CVR also supplies the field's most valuable *procedural* property: it is the independent, human-side record
that lets an investigator ask **"did the crew see what the machine saw?"** That is precisely the question the
three misdiagnoses in §0 needed answered and could not answer.

## 1.3 Spacecraft practice: two telemetry classes, one clock

Spacecraft ground systems (JPL's AMMOS is the canonical example) split downlinked telemetry into classes, of
which two matter here:

- **Channelized EHA** — Engineering, Housekeeping and Accountability: continuous engineering readings
  (temperatures, pressures, voltages, rates) as time-tagged *channels*, each with **alarm limits attached**, so
  "channel in alarm" is a first-class fact, not an afterthought.
- **EVR — Event Records**: discrete, individually time-stamped reports the flight software emits when
  something *happens* — a mode change, a command accepted or rejected, a fault detected, a limit exceeded.

That is the continuous/discrete split of §0, arrived at independently. Channels give the *shape* of the flight;
EVRs give the *story*, in the flight software's own words.

**Time is the second spacecraft lesson.** Onboard, everything is stamped in **SCLK** (spacecraft clock);
analysis happens in **SCET** (UTC); and a deliberate, recorded **time-correlation** step maps one onto the
other — the mapping is itself data (delivered as time-correlation packets, materialised as an SCLK–SCET
kernel). The clock the data is *stamped* with and the clock it is *analysed* in are different clocks.

Our pair is **UT** (`Planetarium.GetUniversalTime()` — the only monotonic, warp-consistent clock KSP has) and
**MET** (`vessel.missionTime` — the frame every §B11 target is quoted in, and the frame that restarts at zero
and jumps on a revert). Both belong on every row; the launch UT that relates them belongs in the manifest.

**Columbia** is the spacecraft case that makes the argument for an *onboard* recorder at all. Columbia alone
carried the OEX/MADS recorder — some 570 sensors, of which about 420 yielded good data — and it held valid data
until 09:00:18 EST, roughly a minute *after* the last voice transmission and after the first debris was seen
from the ground. The downlink stopped; the recorder did not. That is the whole case for writing continuously to
disk rather than depending on what an observer managed to watch.

## 1.4 The investigative principles — the six that transfer

Distilled from the NTSB *Flight Data Recorder Handbook for Aviation Accident Investigation*, the NTSB–BEA
co-operation memorandum, published NTSB FDR factual reports, and the FOQA / flight-data-monitoring literature.

**(a) One time base, established deliberately; everything else hung on it.**
FDR time is counted in subframe reference numbers (1 SRN = 1 s); external sources are aligned by matching a
*shared physical quantity*. In a published NTSB factual report, FDR time was aligned to ADS-B by matching the
recorded latitude/longitude and applying a measured **+1.625 s** offset. The handbook then names correlation as
a deliverable in its own right — *"Plots of the time correlation between radar, FDR, air traffic control
recordings, cockpit voice recorder (CVR) data, and other relevant information."* And it is explicitly **not** a
committee activity (§8.5): one owner, one time base, no negotiated timestamps.

**(b) Independent cross-checks separate sensor error from real events.**
The same factual report records that the lateral-acceleration parameter was *"intermittently valid and
invalid"* across a 27-hour recording. That is a finding about the *recorder*, not the aircraft — and mistaking
it for a finding about the aircraft sends an investigation somewhere false. The defence is redundancy of
*derivation*, not of storage: record quantities that can be checked against each other, and treat a
disagreement as evidence pointing at the instrument first. Our own corpus tooling already does exactly this
(`plugin/tools/assess_flight.py:92` — vertical speed vs d(altitude)/dt, orbital speed vs vis-viva).

**(c) "What did the system know, and when?"** — answered structurally, by recording the machine's *inputs*,
*mode*, *targets* and *outputs* side by side (Appendix M #10 / #25 / #48–#53). A guidance trace showing only
what the vehicle did cannot distinguish a bad decision from a good decision executed on bad data.

**(d) Mode and state transitions are the skeleton of the narrative.** Every reconstruction is told as a
sequence of state changes with parameter traces hung between them. Which is why transition instants must be
*exact* (an event) rather than *quantised* (a sampled column).

**(e) Validity is recorded, not implied.** Preliminary data "may contain non-validated data, and shall bear
notation to that effect"; the final report carries validated data "for the parameters and time periods used and
deemed pertinent, **but not necessarily for every parameter and data point recorded**". Real practice never
pretends a whole file is equally good. **A recorder that writes a plausible number where it has no signal is
worse than one that writes nothing**, because it destroys the reader's ability to tell the two apart.

**(f) Replay and reconstruction are downstream products; the raw data is preserved.** The obligation is to
share *"an electronic copy of the raw, unmanipulated data"* first, and computed plots only "once the accuracy
of the data files has been established"; animations and simulations are held back deliberately. **The raw file
is the evidence; every derived artefact is an argument about it**, and must be regenerable from the file at
any time.

To which the FOQA/FDM world adds the operational form: **exceedance detection** — automatically flagging any
flight where a parameter left a defined envelope, so routine flights self-triage and only interesting ones get
human attention. Its known limitation is worth carrying too: rule-based exceedance "examines each feature
independently, ignoring potential correlations among the parameters", which is why our report generator must
print the per-phase *context* around every flag, never the flag alone.

## 1.5 What transfers, in one table

| Real practice | Our analogue | Specified in |
|---|---|---|
| FDR: fixed parameter list, per-parameter rate | The continuous stream, four rate tiers | §2.1–§2.9 |
| FDR #10/#25/#48–#53 (mode + selected targets) | The GUIDANCE channel — engaged module, mode, targets, vgo/tgo, and the decision that produced them | §2.5 |
| FDR #55/#56 (display format) | `page_l` / `page_c` / `page_r` on every row | §2.7 |
| CVR (crew mics + area mic + datalink) | Every touch/press, its dispatch verdict, and the alarm state at that instant | §2.7, §2.9 |
| Spacecraft EVR | The discrete event log | §2.9 |
| Channelized EHA + alarm limits | The continuous stream + the exceedance rules | §2, §4.10 |
| SCLK ↔ SCET correlation | UT ↔ MET, with launch UT + real-world stamp in the manifest | §4.5 |
| QAR ("more data than the FDR") | Exactly our case: no armour needed, more parameters, higher rates | §4.4 |
| Validity notation | Blank-not-guess, plus the manifest's per-column provenance | §4.6 |
| "Raw, unmanipulated data" first | The CSV is the evidence; every report is regenerable from it | §4.10, §5 |
| FOQA exceedance detection | The report generator's automatic verdict section | §4.10 |
| The FDR *dataframe layout / conversion documentation* (without which the raw file is undecodable) | `*.manifest.json` — schema, units, periods, provenance, versions | §4.3 |

---

# 2. OUR PARAMETER SET + SAMPLE RATES

## 2.0 The rate ladder, and why these numbers

Four continuous tiers plus one event class. **Every interval is in UT seconds** (§4.5), and every tier is
subject to the warp rule (§4.6).

| Tier | Rate | Interval | What lives here | Justification |
|---|---|---|---|---|
| **R0** | physics rate (50 Hz), **accumulated, never sampled** | 0.02 s in, emitted with the row | Duty cycles, integrated impulse, in-interval extrema (peak g, peak q, peak rate, peak pointing error, saturation time) | A 10 Hz snapshot of a 0.06 s RCS pulse dwell is an alias, and the deleted corpus proved it: the `act_*` per-tick snapshots produced a "68–82 % duty" figure that had to be **retracted**, and only the physics-rate `acc_*` accumulators settled the question. Anything that pulses gets accumulated, not sampled. |
| **R1** | **10 Hz** | 0.1 s | The DYNAMIC block: accel/g, q, mach, AoA, attitude error + body rates, actuation demand + applied, throttle, thrust, guidance vgo/tgo/pitch/heading | Appendix M's fastest tiers are 8 Hz (normal acceleration) and 4 Hz (accelerations + control positions). 10 Hz brackets both, is an exact multiple of the 50 Hz physics tick and of the 5 Hz screen tick, and is 2× the deleted recorder's best (5 Hz) — which is the right direction for a QAR. |
| **R2** | **2 Hz** | 0.5 s | The STATE block: altitudes, velocities, mass, orbital elements, propellant, phase/mode/gate, systems, cabin, power | Appendix M puts altitude/airspeed/heading at **1 Hz** and hydraulics at **2 s**. We take one notch faster because we have no bandwidth constraint, and because 2 Hz is an exact decimation of R1. |
| **R3** | **0.1 Hz** | 10 s | The SLOW block: life-support margins, thermal margins, KER stage table (Δv/TWR/Isp/burn time), far-field target geometry, resource fractions | Appendix M's slow tier is **4 s**, and its near-static tier **64 s**. 10 s sits between: fast enough to see a life-support trend across a 19-hour mission, slow enough that 19 hours costs ~6 800 samples. |
| **R4** | **event-driven** | — | Crew/screen interactions, discrete flight events, mode/phase transitions, faults, exceptions | Principle 1.4(d): a transition is a fact with a time. Quantising it to 0.1 s throws away the one thing that makes a narrative. |

**Row cadence.** One row per R1 tick (10 Hz) while any **dynamic phase** is active; otherwise one row per R2
tick (2 Hz). Dynamic = `Ascent`, `Entry`, `Drogues`, `Mains`, any abort, any powered burn (`thrust_n > 0` or
RCS translation commanded), and `Approach` inside 1 km. R2/R3 columns are written on the rows where their own
period has elapsed and left **blank** on every other row; the manifest declares each column's period, so a
reader forward-fills unambiguously (§4.6). One schema, one file, no interleaving.

**Budget check.** A nominal Crew-2 mission ≈ 19 h to docking. Dynamic phases ≈ 2 h (ascent, terminal approach,
docking, deorbit, entry, descent) at 10 Hz = 72 000 rows; the remaining ~17 h at 2 Hz = 122 400 rows, of which
the warp rule (§4.6) removes the great majority — a phasing coast flown at 100× warp writes at wall-clock 1 Hz,
not UT 2 Hz. Call it **~120 000 rows** at ~900 bytes = **~110 MB per mission**, with an on-rails-warp-heavy
mission far smaller. That is a fifth of a modern game save's screenshot folder and is not a constraint.

## 2.1 A — TIME AND FRAME (every row, unconditionally)

| Column | Rate | Source | Units | Why |
|---|---|---|---|---|
| `mission_id` | every row | recorder | string | The fix for "a recording is half a mission" (§4.4). Self-identifying if the file is moved. |
| `seq` | every row | recorder | int | Monotonic row counter. A gap in `seq` is a dropped row and is *visible*. |
| `ut` | every row | `Planetarium.GetUniversalTime()` | s | **The analysis clock.** The single cheapest thing the previous recorder lacked, and the reason its multi-file missions cannot be chained (§3.4). |
| `met_s` | every row | `Vessel.missionTime` (`VesselData.cs:236`) | s | The presentation clock — every §B11 target is quoted in MET. Restarts per vessel; never analyse in it. |
| `wall_s` | every row | `Time.realtimeSinceStartup` | s | The recorder's own clock. Gives warp factor by construction and bounds wall-clock cost. Real analogue: SCLK vs SCET. |
| `warp_rate` | every row | `TimeWarp.CurrentRate` (already read, `VesselData.cs:326`) | × | Physics under warp is not the physics we are tuning. |
| `warp_rails` | every row | `TimeWarp.WarpMode` | 0/1 | On-rails ⇒ control columns are VOID, not zero (§4.6). |
| `vessel` | every row | `Vessel.vesselName` | string | Which stream this is (capsule / booster). |
| `focus` | every row | `FlightGlobals.ActiveVessel.vesselName` | string | Which craft has the camera — an unfocused vessel's physics is not the same physics. |
| `rec_build_us` | every row | recorder self-timing | µs | **Recorder health** (§1.4(b)). The instrument measures itself, so "the recorder cost us frames" is a measurement, not an opinion. |

## 2.2 B — VEHICLE STATE

| Column | Rate | Source (file:line where already read) | Units | Rate justification |
|---|---|---|---|---|
| `alt_m` | R2 | `Vessel.altitude` — `VesselData.cs:90` | m | Appendix M #2 = 1 Hz. Altitude cannot move fast enough to alias at 2 Hz. |
| `alt_radar_m` | R2 | `Vessel.radarAltitude` — `:71` | m | Same class; matters only near the ground, where R1 promotes it anyway (chutes are a dynamic phase). |
| `speed_mps` | R2 | `Vessel.obt_speed` — `:91` | m/s | Appendix M #3 = 1 Hz. |
| `srf_speed_mps` | R2 | `Vessel.srfSpeed` — `:92` | m/s | Both frames, always — MechJeb's own recorder does the same, and §B11 quotes MECO in Mach (surface) and insertion in orbital. |
| `vspeed_mps` | R2 | `Vessel.verticalSpeed` — `:72` | m/s | Also the (b) cross-check partner for `alt_m`. |
| `lat_deg`, `lon_deg` | R2 | `Vessel.latitude/longitude` — `:132-133` | deg | Appendix M #39 = **4 s**. We keep 2 Hz only because it is free on a row we are already writing; the *rate* is not the point, the presence is (booster landing accuracy, §B16). |
| `downrange_m` | R2 | great-circle from the latched launch lat/lon (the deleted `FlightLog.DownrangeM`) | m | The §B8 ascent profile's x-axis alternative; MechJeb's recorder offers exactly this axis choice. |
| `mach` | R1 | `Vessel.mach` — **NOT READ today** | — | §B11's MECO target is *"~80 km, ~Mach 10"*. Dynamic: it moves through 0→10 in 140 s. |
| `q_pa` | R1 | `Vessel.dynamicPressurekPa × 1000` — read at `:399`, **discarded** (only the peak is latched) | Pa | **The single most important ascent column.** §B8's tune is "flat AoA through max-Q at 30–35 kPa"; a 30 kPa peak passing in a few seconds must not be quantised. |
| `atm_density` | R2 | `Vessel.atmDensity` — NOT READ today | kg/m³ | Slow, monotonic through ascent; the independent cross-check for `q_pa` against speed. |
| `accel_g` | R1 + R0 peak | `Vessel.geeForce` — `:160` | g | Appendix M's **8 Hz** parameter, and the only one it puts there. §B11's peak-axial-g `[EST]` (~4 g) is one of the four numbers T22 exists to pin — and a peak is exactly what a snapshot misses, hence the R0 in-interval maximum beside it. |
| `accel_axial_g` | R1 | `Dot(Vessel.acceleration, ReferenceTransform.up)/9.80665` — `:1058` | g | Separates thrust acceleration from total. |
| `pitch_deg`, `heading_deg`, `roll_deg` | R1 | computed in `NavBallRenderer.Orient()` `:232-279` but **only `Debug.Log`ged — never published** | deg | Appendix M #6/#7 = 0.25–1 s. §B8's diagnosis reads the *pitch trace*; without it the recorder cannot serve the tune at all. |
| `aoa_deg`, `aos_deg` | R1 | KER `AttitudeProcessor` (`docs/KER_DATA_RESEARCH.md` §2.1) or self-compute | deg | Appendix M #32 = 0.5–2 s; we need 10 Hz because **the AoA trace *is* the §B8 tune** — spike-at-max-Q vs deviation-before-max-Q is the whole diagnosis. |
| `rate_pitch_dps`, `rate_roll_dps`, `rate_yaw_dps` | R1 + R0 peak | `Vessel.angularVelocity × Rad2Deg` — `:1043-1046` | deg/s | Appendix M's control tier (2–4 Hz). The R0 peak catches a transient a 10 Hz sample straddles. |
| `moi_pitch/roll/yaw` | R2 | `Vessel.MOI` — NOT READ today | t·m² | Denominator of the control-authority metric `tuning_db.py` already computes (`angacc_*_auth = ctrl_tq/moi`). |
| `mass_kg` | R2 | `Vessel.totalMass × 1000` — **NOT READ anywhere** | kg | The (b) cross-check on propellant flow; §B11's TWR targets need it. |
| `ap_km`, `pe_km` | R2 | `orbit.ApA/PeA` — `:93-94` | km | The insertion verdict (§B11: 190–210 km × 51.63°). |
| `inc_deg`, `raan_deg`, `ecc`, `sma_m`, `argp_deg`, `ta_deg` | R2 | `orbit.inclination` `:114`; `LAN`/`eccentricity`/`semiMajorAxis`/`argumentOfPeriapsis`/`trueAnomaly` **NOT READ today** | deg / — / m | A full element set makes the orbit *reconstructable* rather than merely *described*, and is the basis of the vis-viva self-check. |
| `period_s`, `t_ap_s`, `t_pe_s` | R2 | `orbit.period/timeToAp/timeToPe` — `:119-121` (text only today) | s | Phasing analysis. |
| `body` | R3 | `mainBody.bodyName` — `:98` | string | Removes the "detect the body from the data" hack `plugin/build/assess_flight.py:289-309` had to grow after a Kerbin constant was applied to an RSS flight. |

## 2.3 C — PROPULSION

| Column | Rate | Source | Units | Rate justification |
|---|---|---|---|---|
| `throttle` | R1 | `ctrlState.mainThrottle` — NOT READ today | 0..1 | Appendix M #42 = 1 Hz; we need 10 Hz because §B8's open question is *F9's real throttle-down through max-Q vs PVG bang-bang* — a throttle step that lasts seconds must be resolved to a tenth. |
| `thrust_n` | R1 | Σ `ModuleEngines.finalThrust × 1000` — the value is read at `:420` and **thrown away** (`> 0.1f` test only) | N | Appendix M #9 = 1 Hz. Ours is R1 because it is the partner of `throttle` and `accel_g` in the (b) cross-check: thrust/mass must reproduce measured acceleration. |
| `thrust_avail_n` | R2 | KER `ThrustN` via `KerBridge` (`KerData.cs`) | N | Already on the glass ("Thrust Avail", `VehicleSubsystemPage.cs:314`) — S46 wired it. |
| `eng_ignited`, `eng_flameout` | R1 | `ModuleEngines.EngineIgnited` / `.flameout` counts | count | **Provable ignition.** The deleted recorder added these precisely because "delivered thrust = 0" cannot distinguish *did not command* from *commanded and failed*. Keep them; they answer a §1.4(c) question directly. |
| `stage` | R2 + event | `StageManager.CurrentStage` — NOT READ today | int | Appendix M treats configuration changes as slow parameters; the *instant* goes in the event log, the column is for context. |
| `prop_frac` | R2 | `VesselData` propellant collector `:1181-1246` | 0..1 | Slow, monotonic. |
| `mmh_frac`, `nto_frac` | R2 | resource walk (`:905-909`) | 0..1 | The return-propellant budget. The deleted corpus records that the mission-ending Draco/RCS drain was **invisible in the CSV and visible only in resource-panel screenshots** until these columns were added — a §0-class failure caught by adding two columns. |
| `ec_frac` | R2 | `GetConnectedResourceTotals("ElectricCharge")` — `:142` | 0..1 | Power margin. |
| `ker_stage_dv`, `ker_total_dv`, `ker_twr`, `ker_isp`, `ker_burn_s`, `ker_stage_mass_kg` | R3 | `KerBridge` / `KerData.Performance` (`VesselData.cs:719-727`) | m/s, —, s, s, kg | KER's fuel-flow solve is throttled at 150 ms and is already driven at 5 Hz by the screens; these values change on the *stage* timescale, so 10 s is right and it costs the recorder nothing (§4.7 — the recorder never drives a processor). |
| `ker_avail` | R3 | `KerBridge.Driven && ShowDetails` | 0/1 | §1.4(e): log *whether there is a result*, so a blank is never read as a zero. |
| `dv_planned`, `dv_delivered`, `dv_residual` | R2 | conductor / Node Executor (Part B) | m/s | The burn-residual check §9 step 7 of `MECHJEB_MISSION_TUNING.md` tunes against. |
| `dv_grav_loss`, `dv_drag_loss`, `dv_steer_loss` | R2 | conductor ascent-loss decomposition | m/s | The gravity-turn objective: steering loss ≈ 0 on a zero-AoA turn; a growing one means the nose is off prograde. Retained from the deleted schema — it earned its place. |

## 2.4 D — CONTROL AND ACTUATION (the aliasing-sensitive block)

This is where the deleted corpus made — and then corrected — its biggest measurement error, and the correction
is the design rule. **Three distinct things must not be conflated:**

| Kind | Columns | Rate | Meaning |
|---|---|---|---|
| **Requested** (pre-pulse controller demand) | `act_pitch/yaw/roll`, `trans_x/y/z` | R1 | What the loop asked for. |
| **Applied** (post-PWPF command written to `FlightCtrlState`) | `app_pitch/yaw/roll`, `app_tx/ty/tz`, `rcs_pulse_att`, `rcs_pulse_trans` | R1 | What was commanded. **Not delivered force** — KSP's RCS solver owns that. Per-tick snapshots that **alias the 0.06 s pulse dwell**. |
| **Accumulated** (physics-rate, reset each row) | `acc_int_s`, `acc_att_s`, `acc_trans_s`, `acc_both_s`, `acc_none_s`, `acc_att_imp`, `acc_trans_imp`, `acc_both_imp`, `acc_req_att`, `acc_app_att`, `acc_req_trans`, `acc_app_trans` | **R0 → emitted at row rate** | The only un-aliased basis for duty cycle and propellant attribution. |

Plus:

| Column | Rate | Source | Units | Rate justification |
|---|---|---|---|---|
| `att_err_deg` | R1 + R0 peak | conductor / attitude loop | deg | The pointing-error signal `tuning_db.py` profiles per phase. |
| `att_rate_cmd`, `att_rate_meas` | R1 | attitude loop | deg/s | Commanded vs measured — a §1.4(c) pair. |
| `ctrl_tq_pitch/yaw/roll` | R2 | `GetPotentialTorque` | kN·m | Numerator of `angacc_*_auth`. |
| `rcs_thrust_n` | R2 | RCS module sum | N | Authority context. |
| `rcs_on` | R2 | `ActionGroups[RCS]` — `:218` | 0/1 | Appendix M records master switches at 1 Hz. |
| `act_sat_s` | **R0** | accumulated time with `max\|act_*\| ≥ 0.99` | s | Saturation is the definition of "out of control authority" and it is exactly the quantity a snapshot under-reports. `tuning_db.py` currently *derives* `act_sat` from a snapshot; recording it as accumulated time makes it true. |

## 2.5 E — GUIDANCE (the "what did the system know and when" channel — §1.4(c))

Idle until Part B fills the seams (`_AutopilotStub.cs`); the columns exist from day one and read blank, which
is the honest state and is also how a real recorder reports an unfitted system.

| Column | Rate | Source | Units | Rate justification |
|---|---|---|---|---|
| `gnc_engaged` | R2 | `AutoPilot.Engaged` (`_AutopilotStub.cs:143`) | 0/1 | **Appendix M #10, 1 Hz.** The single most-cited discrete in accident reports. |
| `gnc_module` | R2 + event | which MechJeb module the conductor has engaged (§B12.2) | string | **Appendix M #25** (AFCS mode and status). |
| `gnc_status` | R2 | the module's own status/convergence word | string | Whether guidance is converged, coasting, or stalled — the difference between "flew badly" and "had no solution". |
| `mode_index` | R2 | `FlightDriver.MissionMode` → `ControlMode` (`ScreenModes.cs:14`) | enum | Idle/Auto/Manual/Recovery/Abort. Constant `Idle` until Part B — and recording that constant is itself the proof the seam was idle. |
| `tgt_ap_km`, `tgt_pe_km`, `tgt_inc_deg` | R3 | the ascent settings the conductor loaded | km, deg | **Appendix M #48–#53** — the *selected* targets. Without them, "insertion was 367×336" cannot be scored. |
| `pvg_vgo_mps`, `pvg_tgo_s` | R1 | PVG guidance | m/s, s | The closed-loop guidance's own countdown; 10 Hz because the hand-off at the max-Q trigger is the moment §B8 is about. |
| `cmd_pitch_deg`, `cmd_heading_deg`, `cmd_throttle` | R1 | the conductor's command struct | deg, 0..1 | Commanded vs achieved is the (c) pair for `pitch_deg`/`heading_deg`/`throttle`. |
| `node_dv_left`, `node_point_err` | R1 | Node Executor | m/s, deg | Burn residual analysis. |
| `replan_count` | R2 + event | the §B12.4 re-plan loop | int | *The conductor's decisions.* A re-plan is a decision with a reason; the count is context, the **event** carries the reason and the inputs. |
| `deviation_m`, `deviation_mps` | R2 | conductor: predicted vs actual | m, m/s | The scalar that says "guidance is or is not tracking". |

## 2.6 F — MISSION AND CREW GATES

| Column | Rate | Source | Units | Rate justification |
|---|---|---|---|---|
| `mission_phase` | R2 + event | `Mission.AuthoritativePhase` (`VesselData.cs:87`) | enum | Slow by nature; the *transition* is an event (§1.4(d)). |
| `phase_classified` | R2 | `Mission.Classify(MissionInputs)` (`MissionPhase.cs:60`) | enum | Recorded **separately** from the authoritative phase so that a conductor/classifier disagreement is visible rather than resolved silently — a (b)-class independent cross-check on our own FSM. |
| `gate_id`, `gate_phase`, `crew_action` | R2 + event | `CrewProcedureOps` (`_AutopilotStub.cs:30-45`), `GatePhase` (`MissionPhase.cs:26`) | string, enum, 0/1 | Holding → GoReady → Go/NoGo/Abort. Every §B14 hold. |
| `gate_satisfied_mask` | R2 | `ProcState.Satisfied[]` packed to a bitmask | bits | *Which* checklist items were satisfied when the gate released — the difference between "crew pressed Go" and "crew pressed Go with item 3 unsatisfied". |
| `step_id`, `step_state`, `step_ack_mask` | R2 + event | `StepList` (`pure/StepList.cs:36-86`), `StepInputs.Acknowledged` | enum, enum, bits | The 15-row live step machine; S55 is wiring it to the glass. The ack bitmask is a crew channel. |
| `is_return` | R3 | `CrewProcedureOps.IsReturn` | 0/1 | Outbound vs return leg — changes the meaning of half the other columns. |

## 2.7 G — CREW AND SCREENS (the CVR analogue)

**Two columns on every row; everything else is an event.** A page selection is a state (record it continuously,
Appendix M #55); a press is an act (record it as an event, CVR-style).

| Column | Rate | Source | Units |
|---|---|---|---|
| `page_l`, `page_c`, `page_r` | R2 | `ScreenPainter.livePage[1..3]` (`ScreenPainter.cs:199`, published `:280-283`); names via `FigmaUI.Name(UiPage)` (`FigmaUI.cs:147`) | `UiPage` enum (values are persistence-stable — `FigmaUI.cs:19-20`) |
| `brightness_l/c/r` | R3 | `PageState.Brightness` per screen (`ScreenPainter.cs:881`) | 0..1 |
| `cam_view` | R3 | `VesselData.cameraView` `:216` | int |
| `cover_cam_l/c/r`, `cover_phase_l/c/r` | R2 | `ScreenPainter.CoverCamL/C/R`, `CoverPhaseL/C/R` — one read-only property per screen, each reading THAT screen's own `coverCam`/`coverPhase` instance field | `cover_cam_*`: `CoverPage.CoverCam` enum, by name. `cover_phase_*`: a raw 0..6 index (not a name — see the SUPERSEDED note below) |

> **SUPERSEDED 2026-09-05 (S94, closing S86-Q1).** This row named `cover_cam`/`cover_phase` as ONE
> column each, no `l/c/r` split — unlike `brightness_l/c/r` directly above. S86 (2026-09-05) found that
> spec un-fillable honestly: `coverCam`/`coverPhase` are genuinely PER-`ScreenPainter`-INSTANCE state
> (verified: no cross-instance write, unlike `livePage`), and every screen can default onto the Cover
> page simultaneously — so a single column has no one true value to report, and picking one screen's
> value to stand for all three would silently drop two-thirds of the real state (S86's option 2,
> considered and refused for exactly that reason). S86 left both columns undeclared and posed the
> question to the owner (C1.14); the overseer answered 2026-09-05, under the owner's standing directive
> that the overseer settles questions with knowable answers: **option 1, split into six `_l/c/r`
> columns**, matching `brightness_l/c/r`'s and `page_l/c/r`'s shape. The reasoning above is kept, not
> deleted (C1.16) — it is still why the single-column form was never buildable, only superseded by which
> shape replaced it.

**The interaction events** (§2.9 `crew.*`) are captured at two choke points, both of which already exist and
both of which are single:

- **Glass:** `ScreenPainter.TouchDown(float px, float py)` — `plugin/src/ScreenPainter.cs:356`, sole caller
  `ScreenTouch.cs:91`. Every press on every page on all three screens passes through it.
- **Console plate:** `PanelButton.OnMouseDown()` — `plugin/src/PanelButtons.cs:210`.
- **Shared command sink:** `FlightCommands.Run(PanelCommand)` — `plugin/src/_AutopilotStub.cs:98`, whose
  `bool` return **is** the honest "did the press do anything" verdict, alongside
  `PanelPolicy.Resolve*` → `PanelPressKind` (`pure/PanelBehaviour.cs:53-69`) and `PanelLight`.

⚠ **The one real gap this spec has to close.** There is **no unified command identifier.** The dispatch inside
`TouchDown` produces seven disjoint types depending on the surface — `NavHit`/`NavAct` + `UiPage`
(`FigmaUI.cs:82-93`), `CoverPage.CoverButton` (`CoverPage.cs:673-680`), `DockingSimPage.DockAct`
(`DockingSimPage.cs:239-245`), `SuitCheckPage.SuitAct` (`SuitCheckPage.cs:268`), an `int` index into
`ManualChuteDeployPage.Actions`, an `int` 0/1 for the subsystem FUNCTIONS/ALERTS tab, and `PanelCommand`
(`PanelMap.cs:37-58`) from the plate. A BlackBox must define a **flat, stable `control_id` string namespace**
(e.g. `nav.goto.NavOrbitPlot`, `cover.ActDeorbitBrief`, `dock.TransFwd`, `suit.Troubleshoot`,
`chute.7`, `subsys.tab.1`, `panel.FirePyro`) written once at the choke point, with the surface-specific enum
value carried alongside in the event payload. This is additive: it introduces no new dispatch and changes no
behaviour.

⚠ **Second finding, load-bearing for the design.** `ScreenPainter.FigmaMode` is `private const bool = true`
(`ScreenPainter.cs:55`), so `TouchDown` always returns at `:442` and **everything below `:445` is unreachable
at runtime** — `ChromeBar.HitTest`, `GateCard.HitTest`, `Pages.HitTest`, the whole `PageAct`/`PageHit` path.
A recorder must instrument the **live** branch (`:361-442`), not the compiled-but-dead one, or it will record
a channel that can never fire. (Noted, not fixed — C1.1; this is S49's territory.)

## 2.8 H — SYSTEMS, ENVIRONMENT AND FAULTS

| Column | Rate | Source | Units | Rate justification |
|---|---|---|---|---|
| `bus1_on`, `bus2_on` | R2 + event | `SystemsState` (`pure/VehicleSystems.cs:15`) | 0/1 | **Appendix M #74/#75 = 4 s.** We take 2 Hz because a trip cascade is the interesting case and it is instantaneous; the *edge* is an event. |
| `str_a1/b1/c1/a2/b2/c2` | R2 + event | `StringState` (`VehicleSystems.cs:6-17`) | enum Online/Isolated/Tripped | The §B15 "strings". S53 records that 1A/1B/1C currently can never light — recording the model state makes that provable from data. |
| `fire_intensity`, `suppressant` | R2 | `VehicleSystems.cs:19-20` | 0..1 | A fire is a dynamic event; 2 Hz plus an event edge. |
| `leak_rate`, `isolating` | R2 | `VehicleSystems.cs:22-23` | —, 0/1 | Cabin leak + DEPRESS RESPONSE in progress. |
| `o2_store`, `n2_store`, `canister_used` | R3 | `VehicleSystems.cs:25-26` | 0..1 | Consumables move on the day timescale (`OxygenSeconds = 4·6·3600`). |
| `ppo2_psia`, `co2_mmhg`, `cabin_psia`, `cabin_temp_c`, `loop_a_c`, `loop_b_c` | R3 | `CabinReadout` (`pure/CabinEnvironment.cs:62-71`) | psia, mmHg, psia, °C | **Appendix M #78 (loss of cabin pressure) is 1 Hz** — so `cabin_psia` is promoted to **R2**, and to R1 during an abort. The rest are R3. |
| `ls_present`, `ls_o2_frac`, `ls_co2_frac`, `ls_water_l`, `ls_limiting_days` | R3 | `LifeSupportBridge` (`:29-88`), `LifeSupport.Margins` (`pure/LifeSupport.cs:35`) | 0/1, 0..1, l, days | Margins are the natural filling for the Vehicle Overview's dashed MARGIN column (S57) and change over days. |
| `skin_temp_frac`, `hull_temp_c` | R2 | `VesselData.HottestPart` `:287-305`, `:1165-1174` | 0..1, °C | The deleted corpus records that a max-Q "Overheat!" was **invisible with no thermal column**. Entry needs R2. |
| `sev_system`, `sev_vehicle`, `sev_ls`, `sev_thermal` | R2 + event | `Alarms.SystemSeverity/VehicleSeverity/LifeSupport/Thermal` (`pure/Alarms.cs:82,122,130,138`) | enum Nominal/Caution/Alarm | **The area-microphone channel.** Appendix M #30 (master warning) = 1 Hz. |
| `alarm_mask` | R2 + event | `Alarms.Mask` (`Alarms.cs:88-104`) — the file's own comment calls it "THIS IS THE ALARM CHANNEL" | bits | What was lit, per screen area. |
| `fdir_fault`, `fdir_recovery` | R2 + event | `FlightDriver.LastFdirReport` → `FaultKind`/`Recovery` (`ScreenModes.cs:17-24`) | enum | Idle until Part B (§B15). |
| `abort_mode`, `aborting` | R1 + event | `AbortControl.Mode`, `FlightDriver.Aborting` | enum, 0/1 | An abort is the fastest state change in the mission; R1 with an exact event. |
| `comm_linked`, `comm_signal` | R3 | CommNet (`VesselData.cs:979-987`) | 0/1, 0..1 | Slow. |
| `range_m`, `closing_mps`, `align_deg`, `roll_err_deg`, `off_x/y/z_m` | R2 far / **R1 inside 1 km** | `VesselData.Docking()` `:434-511` | m, m/s, deg | §B11's approach ladder is specified to **0.1 m/s at contact** and **< 0.2 m/s inside 5 m**; at 0.1 m/s a 2 Hz sample moves 5 cm, which is fine, but the *rate* signal needs 10 Hz to be differentiable. |
| `phase_angle_rad`, `tgt_radius_m` | R3 | `:546-549` | rad, m | Phasing geometry, hours-scale. |

## 2.9 I — THE DISCRETE EVENT LOG (the EVR analogue)

Every event is one record with an exact `ut`, the row `seq` it falls between, a namespaced `kind`, and a typed
payload. No rate — events are written when they happen, and the writer is flushed immediately (§4.7).

| Namespace | Events | Payload highlights |
|---|---|---|
| `flight.*` | `liftoff`, `clamp_release`, `maxq`, `meco`, `stage_sep`, `s2_ignition`, `seco`, `dragon_sep`, `nosecone_open`, `nosecone_close`, `trunk_jettison`, `drogue_deploy`, `main_deploy`, `main_release`, `splashdown`, `touchdown` | the full R1 state at the instant, plus the trigger that fired it |
| `dock.*` | `approach_init`, `hold_1km`, `kos_entry`, `wp1`, `wp0`, `chop`, `contact`, `capture`, `hard_dock`, `hatch_open`, `undock`, `breakout` | range, closing rate, alignment, roll error |
| `stage.*` | `staged`, `engine_ignite`, `engine_shutdown`, `engine_flameout`, `ullage_start` | stage index, engine ids, commanded vs achieved |
| `gnc.*` | `module_engage`, `module_disengage`, `guidance_converged`, `guidance_lost`, `node_created`, `node_executed`, `replan`, `mode_change` | **the decision and its inputs** — which rule fired, the threshold, the measured value. This is the conductor's own account of its reasoning (§1.4(c)). |
| `phase.*` | `transition` | from, to, which classifier/authority produced it, the input tuple |
| `gate.*` | `open`, `ready`, `go`, `nogo`, `abort`, `item_toggle` | gate id, satisfied mask, which item |
| `crew.*` | `touch`, `press`, `page_change`, `dispatch` | `screen`, `page`, `px`, `py`, `control_id`, surface enum, **`acted` (the `FlightCommands.Run` verdict)**, `press_kind`, `lamp`, and the `alarm_mask`/`sev_system` at that instant — the CVR area-mic context |
| `fault.*` | `raised`, `cleared`, `recovery`, `abort_commanded`, `exceedance` | fault kind, recovery, the parameter and the limit crossed |
| `sys.*` | `bus_trip`, `bus_reset`, `string_state`, `fire_start`, `fire_out`, `leak_start`, `isolate` | before/after state |
| `rec.*` | `open`, `close`, `revert_detected`, `vessel_change`, `focus_change`, `warp_change`, `scene_change`, `write_error`, `self_disable`, `width_mismatch` | **Recorder health as data.** §1.4(e): the file states its own reliability. |
| `exception` | any caught exception in DragonScreen code | type, message, stack, the row context |

**Sub-frame edge latching.** An event that occurs between rows (a stage separation, a chute deploy) is
latched at the instant it is detected in `FixedUpdate` and written with its **own** `ut`, not the next row's.
The deleted Recorder A had exactly this mechanism (`stageLatched`, `sepLatched`, doc-commented *"Edges held
until a row carries them"*) but degraded it by folding the edge into the next 5 Hz sample. A separate event
stream removes the compromise entirely.

## 2.10 Rate summary and the honest limits

| Tier | Interval | Columns (approx.) | Rows over a 19 h mission |
|---|---|---|---|
| R0 (accumulated) | 0.02 s in | 14 | — (emitted with the row) |
| R1 (dynamic) | 0.1 s | ~40 | ~72 000 rows carry them |
| R2 (state) | 0.5 s | ~70 | every row |
| R3 (slow) | 10 s | ~30 | ~6 800 fills |
| R4 (events) | — | — | ~2 000–10 000 records |

**What these rates deliberately do NOT capture.** The physics tick is 50 Hz; anything faster than 25 Hz is
beyond Nyquist for *any* tier here, so a structural oscillation at the physics rate would appear only as a
raised R0 extremum, not as a resolved waveform. That is a conscious limit: resolving it would mean a 50 Hz
column stream, and the R0 accumulators plus the peak-in-interval columns give the *detection* without the
volume. If a flight ever shows an unexplained R0 extremum with a quiet R1 trace, the answer is a temporary
`[Tunable]` rate bump for one flight — not a permanently faster recorder.

---

# 3. REUSE, DON'T REINVENT — what to compose, what to build fresh

## 3.1 ⚠ First, a correction the rest of this section depends on

**There were TWO recorders and TWO corpora, and the repo's own notes conflate them.** Getting this wrong would
send a build at the wrong schema and the wrong analyser:

| | **Recorder A (older)** | **Recorder B (newer, the one that flew last)** |
|---|---|---|
| C# | `plugin/src/FlightRecorder.cs` — monolith, 1029 lines | `plugin/src/pure/FlightRecorder.cs` (schema, 507 lines) + `plugin/src/FlightLog.cs` (glue, 304 lines) |
| Schema style | `ut / met / a_phase / b_phase / x_owner / r_stage` (two-char block prefixes) | `met_s / mission_phase / ascent_phase / att_point_deg` (flat) |
| Filename | `flight_MMdd_HHmmss.csv` | `<VesselName>_yyyyMMdd_HHmmss.csv` → **`Crew-2_*.csv`** |
| Columns | **263** at its last revision | **136** in the final flown corpus (116 in the earliest, 2026-08-31) |
| Rate | **5 Hz** (0.2 s) | **4 Hz** (0.25 s), `[Tunable] SampleIntervalS` |
| Read by | `plugin/build/assess_flight.py` (globs `flight_*.csv`) | `plugin/tools/assess_flight.py` (globs `Crew-2*.csv`) |
| Last good commit | `0d6423d` (2026-08-26, *"BEFORE STRIPPING COMMENTS"*) | `8b81816^` (deleted 2026-09-01) |

Column-count claims in the tree are all wrong and none should be quoted: `plugin/build/assess_flight.py:5`
says "89-col", `plugin/tools/assess_flight.py:3` says "CURRENT 105-col", `plugin/build/audit_comments.py:84`
cites "145 recorder columns" *as an example of a stale prose claim that was false*. **The measured header of
the last flown file is 136 columns** (`git show 8b81816^:docs/flights/Crew-2_20260901_004929.csv`). Logged in
§6.2, not fixed (C1.1).

## 3.2 What already exists and works — the four assets

**(1) The analysers survive and are the report generator.**
`plugin/tools/assess_flight.py` (284 lines) reads the **B** schema and prints nine sections unasked —
*recorder health · physics self-check · ascent · booster · rendezvous/phasing · deorbit/entry/chute ·
abort + FDIR · control authority · verdict* — with the standing rule in its own header: *"Anything it does NOT
flag has been CHECKED, not skipped."* Its `is_warp()` filter (`:61`), its vertical-speed and vis-viva
self-checks (`:92-117`), and its "read the orbit a few rows AFTER thrust dies, not at the last burn row"
correction (`:135-141`) are all hard-won and directly reusable.
`plugin/tools/tuning_db.py` (225 lines) reads the **whole corpus** and builds per-phase statistics of every
control signal, with the derived authority metrics `act_sat` and `angacc_*_auth = ctrl_tq/moi`, into
`docs/tuning/TUNING_DB.json` + `.md`. It is the §B5 tune's memory across flights.
`plugin/build/assess_flight.py` (623 lines) reads the **A** schema only; owner decision S8 keeps it for T22.

**(2) The deleted recorder is recoverable, and its hard-won details are the specification.**
`git show 8b81816^:plugin/src/pure/FlightRecorder.cs` and `…:plugin/src/FlightLog.cs`;
`git show 0d6423d:plugin/src/FlightRecorder.cs` for Recorder A's commentary.
`git show 8b81816^:docs/flights/README.md` is **already 90 % of a recording-format spec** — the flight table
(DS-ASC-001…008, DS-DEO-001), the `act_*` / `app_*` / `acc_*` semantic warning, the format section, the column
groups, the geometry-dump schema, and four runnable stdlib-only Python reproductions of the key findings.
**Salvage it before writing anything.**

**(3) The data is already computed at 5 Hz.** `VesselData.Refresh()` self-throttles to
`RefreshInterval = 0.2f` (`VesselData.cs:13,52`) and fills a `PageState` covering time, regime, altitudes,
both velocity frames, orbital elements, body rates, g, docking geometry, power, cabin, life support, systems,
steps, gates, and mode/fault. `VesselData.State` returns a **struct copy** — a recorder snapshots it in one
read with no aliasing risk. §2 marks the ~15 values that need a genuinely new KSP call.

**(4) KER is both a data SOURCE and the collection-METHOD model.** S45 inventoried every value with exact
access paths; S46 built the reflection drive — `FlightEngineerCore.Instance.AddUpdatable(...)` once per scene
(`KerBridge.cs:163`, from `DragonScreenMonitor.cs:207`), `SimulationProcessor.RequestUpdate()` on our tick
(`KerBridge.cs:184`, called from `VesselData.cs:719`), read next tick (`VesselData.cs:722-727`). KER's
architectural lesson is the one that matters for a recorder: **compute on demand, only for what is being
consumed, behind a rate floor** (`SimManager.minSimTime` = 150 ms against our 200 ms tick). The recorder's
version of that discipline is §4.7's rule: **read what is already computed; never drive a processor.**

## 3.3 MechJeb's FlightRecorder — take the trace, do not depend on it

§B10.7 assigns MechJeb's `MechJebModuleFlightRecorder` the Q/AoA/pitch graphs that drive the §B8 tune.
Reading its source settles what it can and cannot do:

- **Record set** (`RecordStruct`): `TimeSinceMark, CurrentStage, AltitudeASL, DownRange, SpeedSurface,
  SpeedOrbital, Acceleration, Q, AoA, AoS, AoD, AltitudeTrue, Pitch, Mass, GravityLosses, DragLosses,
  SteeringLosses, DeltaVExpended` — 18 fields, exactly the ascent-tune set, including the Δv-loss
  decomposition §2.3 keeps.
- **Rate**: `readonly double Precision = 0.2` — 5 Hz, sampled when `VesselState.Time >= _lastRecordTime + Precision`.
- **⛔ Capacity**: `readonly int HistorySize = 3000`, a fixed array with **no wraparound** — recording simply
  **stops** at `HistoryIdx == History.Length - 1`. **3000 × 0.2 s = 600 seconds.** MechJeb's recorder covers
  **ten minutes** from the last `Mark()`, and `Mark()` zeroes every cumulative loss.

So: it is an excellent ascent instrument (a 9-minute ascent fits inside 600 s with 90 s to spare) and it is
**structurally incapable** of recording a 19-hour mission, a rendezvous, or a return. It is also in-memory
only — nothing is written to disk, so nothing survives a scene change for the overseer to read.

**Decision:** use MechJeb's recorder as the §B8 *in-game tuning display* exactly as §B10.7 says, and treat its
18 fields as a **specification of the minimum ascent column set** (they are all in §2.2/§2.3/§2.4 already).
Do **not** make the BlackBox depend on it: the BlackBox computes those columns itself from `VesselData` +
direct `Vessel` reads, so the recording is complete whether or not a MechJeb core is loaded, and so the
recorded Q/AoA/pitch is available to the overseer as *data* rather than as a graph the owner must screenshot —
which is precisely the §0 failure this whole task exists to end.

## 3.4 What to compose vs what to build fresh

| Item | Verdict | Detail |
|---|---|---|
| The `Schema[]`-as-single-ordered-source-of-truth pattern, with `static readonly int MetS = Index("met_s")` derived indices | **COMPOSE — take verbatim** | Recorder B's best idea: re-ordering the schema just works, positional drift is impossible. |
| `NewRow()` filling every cell with `""` (blank = not filled this tick, distinct from `0`) | **COMPOSE** | The foundation of §4.6's validity rule. |
| RFC-4180 `Escape()` (quote on `,` `"` `\n` `\r`, double inner quotes) + invariant-culture `Num()` with NaN/Inf → **blank** | **COMPOSE** | Recorder A destroyed commas (`s.Replace(',', ';')`) and coerced NaN to `0.0` — both are data-falsifying. B's are correct; take B's. |
| `VerifyWidth()` — compare the first written row's comma count to the header, once, and `LogError` on mismatch | **COMPOSE — take verbatim** | Recorder A's rationale stands: *"A ROW THAT IS NOT AS WIDE AS THE HEADER IS WORSE THAN NO RECORDING AT ALL."* |
| Explicit flush every 25 rows (Recorder A) | **COMPOSE** | B relied on implicit `StreamWriter` buffering + `Close()` — a regression: an unexpected end loses the end, which is the part that matters. At R1 (10 Hz), 25 rows = 2.5 s worst-case loss. |
| The always-on base snapshot (`PutBase`) written every row regardless of which controller is active | **COMPOSE** | Its doc-comment names the bug it fixed: *"the recorder freezing on the ascent filler through the whole abort + chute descent."* |
| Per-controller `Put*` fillers taking the controller's real command struct | **COMPOSE** | "Recording is not ad-hoc." Extends naturally to the conductor (§B12.2). |
| The `acc_*` physics-rate accumulate-and-reset block | **COMPOSE — and generalise** | §2.0's R0 tier. The one mechanism that is not an alias. |
| Voiding control columns under on-rails warp | **COMPOSE, improved** | B **zeroed** them (`ZeroControlColumnsForWarp`); §4.6 **blanks** them instead, because a zero is a legitimate control value and a blank is not. |
| The `warp_rate` / `eng_ignited` / `eng_flameout` instrument-fidelity trio | **COMPOSE** | Each exists because a specific false conclusion was drawn without it. |
| `mmh_frac` / `nto_frac` / `skin_temp_frac` | **COMPOSE** | Each was added after a real failure went invisible in the CSV and visible only in a screenshot. |
| `plugin/tools/assess_flight.py` + `tuning_db.py` | **COMPOSE — extend, do not replace** | ~85 % of the columns they read are in §2 under the same names. §4.10 lists the additions. |
| `plugin/build/assess_flight.py` | **LEAVE ALONE** | Owner decision S8. It reads Recorder A's `flight_*.csv` only; its own banner says *"Do not extend it — extend the tools/ one."* Its booster deck-miss geometry (`:398-436`, `PAD`/`BARGE` coordinates) is the only recovery coordinate pair that survived the deletion and is §B16 material — **port that section into `tools/`**, do not edit `build/`. |
| The `xx_` two-character block prefixes (`a_`, `b_`, `r_`, `m_`, `x_`) | **BREAK** | Load-bearing in `build/assess_flight.py` (`pre = name[:2]`) and a maintenance trap. B's flat names are better. |
| The `"-"` / `"Idle"` / `"None"` idle sentinels | **BREAK** | Three sentinels meaning the same thing, which `tools/assess_flight.py:44` had to widen for. Blank is the only "no value". |
| One file per autopilot engage (A) / per focused vessel (B) | **BREAK — both** | Both manufacture half-missions. A's own header: *"⛔ A RECORDING IS HALF A MISSION… 35 archived recordings; not one holds a whole mission."* Replaced by an explicit `mission_id` (§4.4), which retires the whole 120-second `SEGMENT_GAP_S` gap heuristic. |
| `met` as the only clock | **BREAK — this is the single most important fix** | B has `met_s` and **no UT column at all**, which is why A's mission-chaining logic cannot be ported forward: there is nothing to chain on. §2.1 puts `ut` on every row. |
| Hard-coded 0.2 s row period in the analyser (five literal `.2`s in `build/assess_flight.py`) | **BREAK** | Row period must be read from the manifest, never assumed. B changed to 0.25 s and every duration in the old analyser would read 20 % short. |
| Units carried in the column *name suffix* (`b_omegaRdps` vs `b_omegaR`, `massT`) | **BREAK** | A rename silently changes results by 57×. Units go in the manifest, one place. |
| Parallel per-vehicle column blocks in one row (A) vs separate files per vessel (B) | **OWNER CALL — §6.1 Q3** | A allows cross-vehicle differencing (which §B16 booster recovery needs); B is simpler. |
| A discrete event log | **BUILD FRESH** | Neither recorder had one. A had free-text `a_note`/`r_note` per row and edge latches; B had state columns only. This is the EVR half of §1.3 and it does not exist yet. |
| The crew/screen (CVR) channel | **BUILD FRESH** | Neither recorder recorded a single crew interaction or which page was displayed. This is the channel §0's three misdiagnoses actually needed. |
| A flat `control_id` namespace at the two choke points | **BUILD FRESH** | §2.7's ⚠ — seven disjoint identifier types today, no unified id. |
| The sidecar manifest | **BUILD FRESH** | Neither recorder recorded its own schema version, units, provenance, mod versions or build hash. Without it a file is undecodable in six months (§1.5, last row). |
| Screen replay | **BUILD FRESH — but the seam already exists** | `PreviewMain.cs:70` constructs a `PageState` synthetically and renders it through the same `Pages.Build` / `FigmaUI.Build` the game uses. §4.9. |
| The recorder's own KSP host (`[KSPAddon]` + `FixedUpdate`) | **BUILD FRESH** | There is **no `FixedUpdate` and no `[KSPAddon]` anywhere in `plugin/src` today** — the deleted `FlightDriver` was the flight-scene host. `ScreenPainter.OnPostRender` is not a substitute: it is per-screen, three times a frame, and dies with the IVA. |

---

# 4. FORMAT + ARCHITECTURE

## 4.1 The three streams

Per mission, three artefacts with a shared name stem:

1. **`<MissionId>.params.csv`** — the continuous parameter stream. One header row = the ordered schema; one row
   per tick. RFC-4180, invariant culture, UTF-8, `\n`.
2. **`<MissionId>.events.jsonl`** — the discrete event log. One JSON object per line.
3. **`<MissionId>.manifest.json`** — the schema and provenance record.

**Why JSONL for events and not CSV.** The parameter stream is rectangular and CSV fits it perfectly — that is
why §3.4 reuses the corpus format wholesale. The event log is *not* rectangular: a `gnc.replan` payload and a
`crew.touch` payload share almost no fields, and forcing them into one flat schema either explodes the column
count or collapses everything into an escaped free-text blob (Recorder A's `a_note`, which no tool ever
parsed). JSONL keeps payloads typed and variable, appends cleanly, tolerates a truncated final line, and costs
the Python tooling three lines. **So: the existing corpus format is reused for the stream and consciously not
reused for the events.**

## 4.2 Schema mechanics

- `public static readonly string[] Schema` in `pure/` is the **single ordered source of truth**; indices are
  derived by name (`static int Index(string)`) into `static readonly int` fields. Re-ordering is safe;
  positional drift is impossible.
- `NewRow()` returns a pre-allocated `string[]` filled with `""`.
- `Num(double)` → `ToString("0.######", InvariantCulture)`; NaN/Inf → **blank**.
- `Escape(string)` → RFC-4180.
- One `Put*` filler per subsystem, taking that subsystem's real struct.
- `VerifyWidth()` once per file, `LogError` + a `rec.width_mismatch` event on mismatch.
- Adding a column is append-only within a `schema_version`; **reordering or removing bumps the version**, and
  the manifest records it so an analyser refuses to chain files across a change (the rule
  `build/assess_flight.py` already enforces: *"column 118 means different things in a 175-column file and a
  181-column one, and a silent misalignment is worse than two separate assessments"*).

## 4.3 The manifest — the dataframe layout, which is not optional

Real FDR data is undecodable without its parameter/conversion documentation; the NTSB handbook treats that
documentation as a controlled artefact in its own right. Ours is a JSON sidecar written once at open and
finalised at close:

```
schema_version, recorder_version, dragonscreen_git_sha, ksp_version, mod_versions{RSS,RO,RealFuels,FAR,TAC-LS,KER,…}
mission_id, vessel, vessel_persistent_id, craft_file, crew[]
launch_ut, ut_at_open, wall_at_open, real_world_utc_at_open        # the UT↔MET↔wall correlation record
body, target_name
row_rate_dynamic_hz, row_rate_quiescent_hz, dynamic_phase_rule
columns[ { name, units, period_s, tier, source, provenance } ]      # provenance ∈ ksp-direct | ker | tac-ls
                                                                    #   | mechjeb | conductor | SIMULATED | derived
mechjeb_cfg_sha, tunables{}                                         # what the vehicle was FLOWN with
closed_ut, closed_reason, rows_written, events_written, write_errors, max_rec_build_us
```

Two things this buys that no column can:

- **§14.4(e)/(f) marking, once, cheaply.** Every simulated value is declared `provenance: "SIMULATED"` in one
  place, so the overseer can never mistake a marked simulation for a measurement — and no per-column
  provenance string has to be written 120 000 times.
- **The tune is reproducible.** `mechjeb_cfg_sha` + `tunables{}` record *what the vehicle was flown with*.
  §B5 changes one parameter at a time; without this, two recordings cannot be told apart.

## 4.4 Files, naming, rotation, size

- **Directory: `<KSP>/DragonScreen_capture/`.** Already established (`ScreenPainter.cs:806` writes the screen
  PNGs there), already git-ignored (`.gitignore:42`), already what `plugin/tools/assess_flight.py:22` globs.
  No new location, no C7 question (§5).
- **`MissionId` = `<SanitizedVesselName>_<yyyyMMdd_HHmmss at first open>`** — e.g.
  `Crew-2_20260903_101500`. Keeps the existing `Crew-2*` glob working and groups the three files by prefix.
  The id is also a **column on every row**, so a file that is moved or renamed still self-identifies.
- **A second tracked vessel** (the §B16 booster) opens `<MissionId>.<Vessel>.params.csv` — *the same mission
  id*. This is the fix for the paired `Crew-2_*.csv` / `Crew-2_Probe_*.csv` streams that could only be
  associated by their timestamps.
- **Rotation: none by time, none by vessel focus, none by autopilot engage.** One mission = one set. The two
  prior recorders rotated on focus change and on engage respectively, and both manufactured half-missions.
- **The two rotations that DO happen**, each with a `rec.*` event:
  - **Revert.** UT moving backwards is a new mission branch → new `MissionId` with a `_r2`, `_r3` … suffix and
    a `rec.revert_detected` event carrying the UT it reverted from and to. (Recorder A's chainer had to detect
    reverts as "a negative gap"; making it explicit retires the heuristic.)
  - **Hard size ceiling: 512 MB per file** → `<MissionId>.params.2.csv`, same header, `rec.rotate` event. A
    ceiling that should never be reached, present so an unattended run cannot fill a disk.
- **Expected size:** ~110 MB per nominal 19 h mission (§2.0); a 15-minute ascent test ≈ 8 MB.
- **Retention:** the recorder deletes nothing. Housekeeping is the owner's (§6.1 Q1).

## 4.5 The time base

- **`ut` is the analysis clock.** Monotonic within a mission branch, consistent under warp, shared by every
  vessel. All chaining, all correlation, all differencing is in UT.
- **`met_s` is the presentation frame.** Every §B11 target is quoted in MET, so every report prints MET — but
  MET restarts at 0 per vessel and jumps on a revert, so nothing is *computed* in it. `launch_ut` in the
  manifest is the correlation record, exactly as an SCLK–SCET kernel is.
- **`wall_s`** gives the warp factor by construction and bounds the recorder's real-time cost.
- **Events carry their own `ut`** plus the `seq` of the row they fall between — so an event is placed exactly
  and is also joinable to the stream without a search.
- **Cross-source correlation** (KSP.log lines, screenshots, the overseer's own notes) is done on UT, and the
  manifest's `real_world_utc_at_open` + `wall_at_open` make wall-clock artefacts (a screenshot's file mtime,
  a KSP.log timestamp) mappable onto it. This is §1.4(a) made mechanical: **one owner of the time base, one
  documented offset, no negotiated timestamps.**

## 4.6 Validity — blank, never a plausible number

Four distinct states, three of them representable in a cell:

| State | Representation | Read as |
|---|---|---|
| Sampled, valid | the value | a measurement |
| **Not sampled this row** (a decimated R2/R3 column on an R1 row) | blank | forward-fill from the last value; the column's `period_s` in the manifest makes this unambiguous |
| **No signal** (source absent, not applicable, KER unavailable, no target) | blank **on the column's own sample row** | *no value existed* — never a zero |
| **Voided** (on-rails warp: the control columns are frozen stale) | blank, with `warp_rails = 1` | not measurable in this regime |

- **NaN/Inf → blank**, never `0.0`.
- **On-rails warp voids the control block.** Recorder B *zeroed* it; blanking is strictly better because zero
  is a legitimate control value. The reason it matters is on the record: stale frozen control values under
  warp manufactured a phantom "RCS thrash" that was investigated as real.
- **Warp also changes the sampling clock.** Rates are defined in UT, but under on-rails warp UT advances up to
  1000× per frame, which would write ~50 rows per wall-second of nothing. Rule: while `warp_rails = 1`, the
  interval is `max(tier interval, 1.0 s of WALL clock)`. A phasing coast at 100× therefore writes ~1 row per
  wall-second, and the file stays proportional to *time spent*, not to *game time elapsed*.
- **Every value that is a marked simulation under §14.4(e)/(f)** carries `provenance: "SIMULATED"` in the
  manifest. A simulated value in a recording is evidence about the *model*, never about the *vehicle*, and the
  report generator labels it so.

## 4.7 Performance budget

| Item | Budget | How |
|---|---|---|
| Row construction | **< 0.3 ms**, zero steady-state allocation | Pre-allocated `string[]`; a reused `StringBuilder`; ~140 `ToString` calls at 10 Hz = ~1400/s, which is noise beside a single frame of KSP part-tree work. |
| Physics-tick accumulators (R0) | **< 0.05 ms per `FixedUpdate`** | Pure arithmetic on pre-allocated doubles. No allocation, no reflection, no LINQ. |
| File I/O | amortised | Buffered `StreamWriter`, explicit flush every 25 rows (2.5 s at R1) **and** immediately after every event write and on scene change. Never per-row. |
| **New computation** | **zero** | ⭐ **The load-bearing rule: the recorder reads what is already computed and never drives a processor.** `VesselData.State` is a struct copy of work the screens already did at 5 Hz. KER's fuel-flow solve is already driven by `VesselData.cs:719`; the recorder must **not** call `RequestUpdate()` itself — doing so would double a whole-part-tree solve. KER's own discipline (compute only for what is being consumed, behind a 150 ms floor) is the model. |
| The ~15 genuinely new KSP reads (§2) | negligible | `mach`, `totalMass`, `atmDensity`, `mainThrottle`, `MOI`, the extra `orbit` elements, per-engine `finalThrust`/`flameout`, `TimeWarp` state, surface pitch/heading/roll. All are field reads or single-pass loops over parts we already walk. |
| Self-measurement | `rec_build_us` on every row | So "the recorder cost frames" is a number in the file, not an argument. |
| Failure behaviour | never takes the flight down | The whole sample body in `try/catch`; log-and-swallow; **self-disable after N consecutive write failures** with a `rec.self_disable` event and one `LogError`. Recorder A's stronger rule also applies: a row that throws **stops the recorder** rather than writing garbage. |
| Kill switch | `[Tunable] BlackBoxEnabled` + `SampleIntervalS` overrides | The `Tuning` system already hot-reloads from `PluginData/tuning.cfg` at 1 Hz (`Tuning.cs:62-74`), so a rate can be changed between flights without a rebuild. |

**Host.** The recorder needs its own `[KSPAddon(KSPAddon.Startup.Flight, false)] MonoBehaviour` with a
`FixedUpdate` — there is none in `plugin/src` today, and `ScreenPainter.OnPostRender` cannot serve: it runs
once per screen (three times a frame), and it dies with the IVA, which is exactly what happens at a booster
hand-off. It must also **not** use `Time.realtimeSinceStartup` as its sampling clock: `OnPostRender` keeps
firing while KSP is paused, a bug already fixed once in the power-flow clock (`VesselData.cs:147-158`).

## 4.8 What the recorder must never do

- **Never fly the vehicle.** It is read-only. It never writes to `FlightCtrlState`, never stages, never sets a
  MechJeb field. §14.4(a)/(f)'s actuation boundary is untouched.
- **Never fabricate.** No interpolation, no smoothing, no gap-filling, no "reasonable default". Blank.
- **Never re-simulate.** The tooling's standing rule holds: *"⛔ Reads recorded data only — NOT a
  physics/orbital sim (those are banned)."*
- **Never modify the repo.** It writes only into `<KSP>/DragonScreen_capture/` (§5).
- **Never block a frame.** No synchronous large writes, no `File.WriteAllText` per row.

## 4.9 Replay

Two levels, both offline, neither requiring KSP.

**Level 1 — data replay (the default).** `params.csv` + `events.jsonl` + `manifest.json` → plots and the
report (§4.10). This is what answers "what did the vehicle do, and when". Everything is regenerable from the
raw files, which are never edited (§1.4(f)).

**Level 2 — screen replay.** ⭐ **The seam already exists.** `plugin/preview/PreviewMain.cs:70` constructs a
`PageState` **synthetically** and renders it through the exact `Pages.Build` / `FigmaUI.Build` /
`PreviewMain.Render` path the game uses (`build.py preview` compiles `src/pure` + `preview` only — no KSP, no
Unity). So a recorded row can be **rehydrated into a `PageState`** and drawn:

```
row (+ events, + the sub-structures below)  →  PageState  →  FigmaUI.Build/Pages.Build  →  PNG
```

The fields §2 records were chosen to make this possible; the additional per-screen state a faithful frame
needs is small and is listed here so a build does not discover it late: `MapView` (pan/zoom/centre),
`ChromeState` (incl. `AlertMask`), `PageControls`, the suit-run seed + countdown, `coverPhase`, `coverCam`,
and the turntable state.

Usage: `replay_screens.py <MissionId> --at "T+00:01:12"` or `--event flight.maxq` → three PNGs, one per
screen, of **exactly what the crew was looking at**. That is the direct, permanent fix for the S34→S38
oblique-angle illusion and for S18's unrecorded checklist: the question "what did the screen actually show?"
stops being answered from memory.

⚠ **Honest limit:** a replayed frame is a faithful reconstruction of the *display data*, not a photograph of
the capsule. It does not reproduce the IVA viewing angle, the RenderTexture filtering, or the ambient
lighting — which is precisely the class of problem S38 turned out to be. So screen replay settles *content*
questions definitively and **cannot** settle *legibility-in-the-seat* questions; those still need glass.

## 4.10 The report generator — an `assess_flight`-style pass the overseer runs

**Extend `plugin/tools/assess_flight.py`; do not write a new one.** Its nine sections map onto §2's schema
under mostly the same names, and its standing promise — *"Anything it does NOT flag has been CHECKED, not
skipped"* — is exactly the property the overseer needs. Required changes and additions:

| # | Section | Status |
|---|---|---|
| 0 | **Provenance** — manifest summary: schema/recorder/git versions, mod versions, tunables, `mechjeb_cfg_sha`, and **which columns are `SIMULATED`** | **NEW** — §1.4(e) and §14.4(f) marking, surfaced before any number is read |
| 1 | Recorder health — rows, gaps in `seq`, write errors, `max_rec_build_us`, dormant blocks, constant/empty/all-zero columns | EXTEND (add `seq` gaps + the manifest's own error counts) |
| 2 | Physics self-check — `vspeed` vs d(`alt_m`)/dt, `speed_mps` vs vis-viva from the recorded elements, `thrust_n`/`mass_kg` vs `accel_g`, `mass_kg` vs integrated propellant | EXTEND (the last two are new; body read from the manifest, retiring the `detect_body` hack) |
| 3 | Ascent — event-by-event against §B11's `[DOC]` targets: max-Q 30–35 kPa at ~12 km, MECO ~80 km/Mach 10, SECO-1, insertion 190–210 km × 51.63° | EXTEND (drive off `events.jsonl`, not phase-column transitions) |
| 4 | Booster (§B16) — phases, deck-miss along/cross track, on-deck ±25 m / ±12.5 m | EXTEND — **port the geometry from `plugin/build/assess_flight.py:398-436`**, the only surviving recovery coordinates |
| 5 | Rendezvous / phasing — the periapsis floor (≥150 km), the intercept ladder 4 km→1 km→220 m→20 m, dock ~19 h | KEEP |
| 6 | Deorbit / entry / chute — entry interface 122 km at 7.8 km/s, FPA, peak decel, chute gates | KEEP |
| 7 | Abort + FDIR | KEEP |
| 8 | Control authority — per-phase pointing error and saturation, now from the **R0 accumulators** rather than snapshots | EXTEND — the retraction in §3.2 is the reason |
| 9 | **Crew & screens** — the CVR pass: every interaction with its dispatch verdict, page timeline per screen, and any press that did nothing | **NEW** |
| 10 | **Event timeline** — the whole `events.jsonl` as one ordered narrative with the parameter context at each instant | **NEW** — §1.4(d) |
| 11 | **Exceedances** (FOQA) — every §B11 target and every `CabinLimits` threshold as a rule; each hit printed **with its surrounding per-phase context**, because rule-based exceedance ignores correlations between parameters | **NEW** |
| 12 | Verdict | EXTEND |

`plugin/tools/tuning_db.py` needs no conceptual change — it re-reads the whole corpus and rebuilds
`docs/tuning/TUNING_DB.json` + `.md`, accumulating across flights, which is the §B5 tune's memory. (Its output
directory `docs/tuning/` does not currently exist — §6.2.)

**Output:** one text/Markdown report per mission, regenerable at any time from the three raw files, printed to
stdout and writable to a file the owner can paste to the overseer. Never a plot the owner must interpret; a
number, its target, and the delta.

---

# 5. C7 / EVIDENCE — the boundary, stated explicitly

**The recorder WRITES into the KSP install.** `<KSP>/DragonScreen_capture/` is the **deploy target**. C7 makes
the KSP install "deploy/runtime — write-only", and that is exactly what this is: the mod writes there at
runtime, the same directory it already writes screen PNGs to (`ScreenPainter.cs:806`), and the directory is
already git-ignored (`.gitignore:42`). **Nothing in the repo is written by the recorder**, and no build ever
reads from `GameData/`.

**The overseer READS recordings as EVIDENCE — exactly as it already reads `KSP.log` and screenshots.** A
recording is an observation *about a flight*. It is:

- ✅ **evidence** — the basis for a finding, a diagnosis, a tune step, a register line;
- ⛔ **never a build source** (C7) — not a spec, not a doc, not a label authority, not a source of truth for a
  displayed value. §1.4's source ladder (verified-real → other users'/mod → simulate-marked) is unchanged: a
  number appearing in a recording tells you what our model produced, not what the real Dragon does. A
  `provenance: "SIMULATED"` column is evidence about our simulation and nothing more.

**How a recording becomes actionable, concretely.** The same path as any other evidence: a finding is quoted
*into* a register line or a doc, with the mission id, the UT/MET, and the columns it rests on. The build chat
acts on the register line. It does not go and read the KSP install to start a task — if a needed input is not
in the repo, it stops and flags it (C7).

**Recordings in the repo.** `.gitignore` excludes `*.csv` and `DragonScreen_capture/`. The deleted
`docs/flights/` corpus existed only because those files were **force-added** as named evidence behind specific
findings. That precedent is fine and it is an **owner call each time** (§6.1 Q1) — a force-added recording is
an evidence artefact attached to a finding, still not a build source.

---

# 6. What this research leaves open

## 6.1 Owner decisions — batched (C1.9), posed as paste-ready overseer prompts (C1.13)

> **Q1 — Where do flight recordings live, and how does the overseer get them?**
> **Situation.** `docs/BLACKBOX_RESEARCH.md` (S59) specifies a flight recorder writing three files per mission
> into `<KSP>/DragonScreen_capture/` — the deploy target, already git-ignored, ~110 MB per full mission. The
> overseer must be able to read a flight as evidence, but a build chat may never read the KSP install (C7).
> **Decision needed.** How does a recording reach the overseer?
> **(a)** KSP directory only; the owner runs the report generator and pastes the report. *(Smallest change; the
> raw data never leaves the machine; the overseer sees only what the report prints.)*
> **(b)** As (a), plus the generated **report** (text/Markdown, a few hundred KB) is committed to
> `docs/flights/` per assessed flight. *(The overseer can re-read a past flight; raw data stays out of git.)*
> **(c)** Force-add the raw CSVs too, as the deleted `docs/flights/` corpus did, per flight, on an explicit
> owner call. *(Full re-analysis possible later; adds ~100 MB per flight to the repo.)*
> **Gate.** (b) and (c) change what gets committed and touch `.gitignore` — an owner call (C1.12); a build chat
> cannot decide it.

> **Q2 — Fixed or adaptive row rate?**
> **Situation.** The spec proposes 10 Hz while a dynamic phase is active and 2 Hz otherwise, with an on-rails
> warp floor of 1 row per wall-second (§2.0, §4.6). This keeps a 19 h mission at ~110 MB.
> **Decision needed.** **(a)** Adaptive, as specified. **(b)** Fixed 10 Hz throughout — simpler to reason about
> and to analyse, ~5× the file size. **(c)** Fixed 5 Hz throughout, matching the screen tick and the deleted
> Recorder A — smallest and simplest, but halves the resolution of the §B8 AoA/Q traces the whole tune depends
> on. *No gate; a design preference with a size consequence.*

> **Q3 — One row per mission with parallel per-vehicle columns, or one stream per vessel?**
> **Situation.** Recorder A put ascent (`a_`) and booster (`b_`) side by side in one row — cross-vehicle
> differencing is trivial, the schema doubles. Recorder B opened a separate file per focused vessel — simpler,
> but the two streams could only be associated by their timestamps. §B16 (Falcon-9 booster recovery) is a
> separate-vessel autopilot and will want both craft on one clock.
> **Decision needed.** **(a)** Separate stream per vessel, joined by a shared `mission_id` and `ut` (this
> spec's default — keeps the schema flat and lets the analyser join on UT). **(b)** Parallel column blocks in
> one row. *No gate; affects §B16's analysis ergonomics.*

> **Q4 — When is the BlackBox built, relative to Part B?**
> **Situation.** §B12.6's build order is (1) embed MechJeb → … → (7) deorbit/entry/chutes → (8) the §B5 tune,
> and the Part-B gate defers the tune "until after the first recorded flight". **There is no recorder in the
> tree** — the phrase "the first recorded flight" currently has nothing to record it. The analysers survive
> (`plugin/tools/assess_flight.py`, `tuning_db.py`); the writer that feeds them was deleted 2026-09-01.
> `docs/MECHJEB_MISSION_TUNING.md:252` reads *"The tooling for this already exists in the repo and does not
> need writing"* — true of the analysers, **not** of the recorder.
> **Decision needed.** Where does the BlackBox go in the register?
> **(a)** A new task **before T15** — so the very first Part-B flight is fully recorded and no capsule time is
> ever spent on an unrecorded flight. *(Recommended: it is pure + glue, headless-testable, and needs no
> conductor; the seams it samples already exist and read blank honestly.)*
> **(b)** After T17 (the first in-sim step), so it is built against a conductor that actually produces
> guidance data.
> **(c)** Split: the stream + events + manifest before T15; the conductor/guidance channel (§2.5) filled in
> alongside each controller as §B12.5 replaces the stubs.
> **Gate.** Adding a register line is normal; the **placement** is an owner call because it reorders the Part-B
> sequence in `REGISTER.md`, and building it is Part-B-gated code (pure + `test` + `preview` only; any in-sim
> confirmation needs a separate glass go).

> **Q5 — Does the recorder ship enabled?**
> **Situation.** It writes ~110 MB per mission into the player's KSP folder and deletes nothing.
> **Decision needed.** **(a)** On by default, with a `[Tunable]` kill switch (recommended for our own use — an
> unrecorded flight is the failure this task exists to prevent). **(b)** Off by default, opt-in via
> `DragonScreen.cfg` (right for public distribution). **(c)** On for the Crew-Dragon craft only.
> *No gate; a packaging preference. Relevant to the GPLv3 public-distribution question in §B12.1.*

## 6.2 Logged, not done (C1.1 — noticed during this task, out of its scope)

1. **`plugin/tools/assess_flight.py:3` claims the "CURRENT 105-col recorder schema".** The last flown corpus
   header is **136 columns** (`git show 8b81816^:docs/flights/Crew-2_20260901_004929.csv`). A stale count in
   the header of the tool the §B5 tune depends on.
2. **`plugin/build/assess_flight.py` contradicts itself.** Line 7 correctly says it reads *"the historical
   `flight_*.csv` corpus (pre-2026-08-26)"*; line 11 says *"The old `Crew-2_*.csv` corpus this script reads"* —
   which is the **other** recorder's corpus, in a schema this script explicitly cannot parse. Line 7 matches
   the code (it globs `flight_*.csv`). ⚠ Comment-only fix; S8 says keep the file, and its own banner says do
   not extend it.
3. **`docs/INDEX.md` §6 lists only `plugin/build/assess_flight.py`.** `plugin/tools/assess_flight.py` and
   `plugin/tools/tuning_db.py` — the *current* analysers, and the ones `MECHJEB_MISSION_TUNING.md` §9 points
   every tune step at — have no INDEX entry at all. Related to **S58**; add there.
4. **`docs/MECHJEB_MISSION_TUNING.md:252-256`** reads *"The tooling for this already exists in the repo and
   does not need writing"*. True for the two analysers; **the recorder that feeds them does not exist**. As
   written, a session starting the §B5 tune from that sentence will believe the chain is intact.
5. **`docs/tuning/` does not exist**, though `plugin/tools/tuning_db.py` writes `TUNING_DB.json`/`.md` there.
   It will be created on first run; noted so its absence is not read as a deletion.
6. **`plugin/build/assess_flight.py:52-54`** hard-codes `F9I_ROLL` / `F9I_TOTAL_ROLL` constants sourced from
   `docs/F9I_BOOSTER_TARGETS.md`, **which was deleted** in `8b81816`. The constants are now unsourced. §B16
   material; recoverable via `git show 8b81816^:docs/F9I_BOOSTER_TARGETS.md`.
7. **`git show 8b81816^:docs/flights/README.md` should be salvaged before any recorder is built.** It is the
   only surviving statement of the `act_*` / `app_*` / `acc_*` semantic distinction and of the geometry-dump
   schema, and it contains four runnable reproductions of the corpus's key findings.
8. **`ScreenPainter.FigmaMode` is a `private const bool = true`** (`:55`), making everything below
   `TouchDown:445` unreachable — `ChromeBar.HitTest`, `GateCard.HitTest`, `Pages.HitTest`, the whole
   `PageAct`/`PageHit` path. Already S49's territory; restated here because a recorder that instruments the
   dead branch records a channel that can never fire.
9. **`KerBridge`'s unit conversions are flight-unverified** (kN→N, t→kg, and stage-index semantics —
   `KerBridge.cs:34-36`, `KerData.cs:148-151`). Already held on **S47**. Any recorded `ker_*` column inherits
   that caveat until S47 clears it, and the manifest's provenance field should say so.
10. **No `[KSPAddon]` and no `FixedUpdate` exist in `plugin/src`.** Noted as a structural fact a recorder build
    must supply, not a defect.

---

# Sources

**Real practice — aviation.**
14 CFR Part 121, Appendix M — *Airplane Flight Recorder Specifications* (the 91-parameter list with
per-parameter sampling intervals) · 14 CFR 121.344 · NTSB, *Flight Data Recorder Handbook for Aviation
Accident Investigation* (Dec 2002) — §§6–9 (readout, preliminary data, validation, correlation) · the NTSB–BEA
co-operation memorandum reproduced in that handbook (raw-data-first, QAR parity, time-correlation plots) ·
published NTSB FDR factual reports (SRN timing; FDR↔ADS-B alignment via lat/long with a measured +1.625 s
offset; the intermittently-valid lateral-acceleration parameter) · EUROCAE ED-112A (crash-protected recorder
MOPS; the 2 h → 25 h CVR duration change; four audio channels incl. the cockpit area microphone and datalink
recording) · FOQA / flight-data-monitoring literature on rule-based exceedance detection and its
per-parameter-independence limitation.

**Real practice — spacecraft.**
JPL/AMMOS telemetry classes — EVR (event records) vs channelized EHA (engineering/housekeeping/accountability)
vs data products · CCSDS / NAIF time-correlation practice — SCLK (onboard) ↔ SCET (UTC), time-correlation
packets and the SCLK–SCET kernel · Columbia Accident Investigation Board material and NASA/press accounts of
the OEX/MADS recorder (≈570 sensors, ≈420 with good data; valid data to 09:00:18 EST, after loss of signal).

**MechJeb.**
`MuMech/MechJeb2` `dev` — `MechJeb2/MechJebModuleFlightRecorder.cs`: the 18-field `RecordStruct`,
`Precision = 0.2`, `HistorySize = 3000` with no wraparound, and `Mark()` semantics.

**This repository.**
`docs/BUILD_PLAN.md` §B5, §B7–§B8, §B9, §B10.7, §B11, §B12, §B13–§B16, §14.4(a)(e)(f), Part C ·
`docs/KER_DATA_RESEARCH.md` (S45) §1.3, §1.6, §2, §4, §5.2 · `docs/MECHJEB_MISSION_TUNING.md` (S48) §1B.v, §9 ·
`docs/SCREEN_LIVENESS_AUDIT.md` (S49) · `REGISTER.md` S8, S18, S34, S36, S38, S45–S49, T22 ·
`plugin/tools/assess_flight.py`, `plugin/tools/tuning_db.py`, `plugin/build/assess_flight.py` ·
`plugin/src/VesselData.cs`, `KerBridge.cs`, `ScreenPainter.cs`, `ScreenTouch.cs`, `PanelButtons.cs`,
`_AutopilotStub.cs`, `Tuning.cs`, `LifeSupportBridge.cs` · `plugin/src/pure/` — `Pages.cs`, `MissionPhase.cs`,
`FigmaUI.cs`, `ScreenModes.cs`, `Alarms.cs`, `VehicleSystems.cs`, `CabinEnvironment.cs`, `LifeSupport.cs`,
`StepList.cs`, `PanelMap.cs`, `PanelBehaviour.cs`, `CoverPage.cs`, `DockingSimPage.cs`, `SuitCheckPage.cs`,
`ManualChuteDeployPage.cs` · `plugin/preview/PreviewMain.cs` · `plugin/build.py`.

**This repository's git history** (evidence only — deleted code, not a build source):
`git show 8b81816^:plugin/src/pure/FlightRecorder.cs` (the 136-column schema) ·
`…:plugin/src/FlightLog.cs` (the glue: `SampleIntervalS = 0.25`, file naming, per-vessel rotation) ·
`…:plugin/src/FlightDriver.cs` (the `[KSPAddon]` host + `FixedUpdate` tick) ·
`…:docs/flights/README.md` (the recording-format spec and the `act_*`/`app_*`/`acc_*` warning) ·
`…:docs/flights/Crew-2_20260901_004929.csv` (the measured 136-column header) ·
`git show 0d6423d:plugin/src/FlightRecorder.cs` (Recorder A: 263 columns, `IntervalS = 0.2`,
`FlushEvery = 25`, `VerifyWidth()`, the edge latches).
