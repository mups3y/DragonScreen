# The recovered flight corpus — what it actually contains

> **[HIST — evidence].** Produced by **S76** (2026-09-04) by running `plugin/tools/assess_flight.py` over
> every file in `docs/flights/`, the 21 MB RSS-RO corpus **W26** recovered (`cdd37f5`) on the owner's
> decision. It reports what the recordings contain and what their attitude telemetry shows. It **decides
> nothing** — in particular it draws no conclusion about what the Part-B steering law should do; that is
> **W24**'s task under its own gate (C1.11 / C1.12), and this document is W24's evidence.
>
> **Source-of-truth order:** `docs/BUILD_PLAN.md` wins on any conflict (C7.1). The format spec is
> `docs/flights/README.md`; this file does not restate it.
>
> ⚠ **These are recordings of the DELETED hand-written autopilot.** Their *numbers* are the only RSS-RO
> empirical data this project has. Their *control behaviour* is the thing that failed.

---

## 0. Headline

| | |
|---|---|
| Files in `docs/flights/` | **23** — 13 recorder CSVs, 3 geometry dumps, 2 KSP.log excerpts, 3 PNGs, 1 README |
| Recorder CSVs the analyser **read successfully** | **13 of 13** |
| Recorder CSVs that **failed** | **0** (2 failed on the first run; the cause was a reader bug, now fixed — §1) |
| Geometry dumps the analyser reads | **0 of 3, by design** — different schema, now excluded from the glob |
| Distinct flights | **9** — DS-ASC-001…008 + DS-DEO-001 (three CSVs are the parallel booster stream, one is a continuation file) |
| Flights that reached orbit | **6 of 8** ascents (DS-ASC-003…008); DS-ASC-001/002 were suborbital, reverted out of the S2 tumble |
| Flights that completed a docking | **0** |
| Flights that completed a return / entry / splashdown | **0** |

**The corpus covers launch → orbit → far-field rendezvous, and stops there.** No recording contains a
terminal approach inside 58 km, a docking, an undock, a deorbit burn that fired, an entry, or a chute
deployment. Every `dock_*`, `entry_*` and `chute_*` column is blank in every file.

---

## 1. Where the analysers were looking, and where they look now

`plugin/tools/assess_flight.py` and `plugin/tools/tuning_db.py` both read the *right* schema and the
*right* filename pattern, and both globbed **only two directories, neither of them in the repo**:

- `…\Kerbal Space Program\DragonScreen_capture` (the KSP install — a C7 deploy target, not a source)
- `…\Desktop\quarantine\dragonscreen_flightdata`

`docs/flights/` — the in-repo corpus — was in neither list, so the analysers and their data could not see
each other. **Fixed (S76):** `docs/flights/` is now the first and, by default, the *only* directory read.
The two external paths are kept as an explicit opt-in (`assess_flight.py --external`; `tuning_db.py
<corpus_dir>`), because the KSP capture directory is still where a *new* recording lands.

⚠ **The `--external` sweep is not a build source and this document does not use it.** See §7 Q1.

**Three further defects, found by running it:**

| # | Defect | Fix |
|---|---|---|
| a | `Crew-2_deorbit_geometry_dump_manual_2500s.csv` **matches the `Crew-2*.csv` glob** but is the `GeometryDump` instrument's schema (`row,part_idx,part_name,…`). Fed to the analyser it produces a page of zeroes, not a finding. | Both tools now skip `*geometry_dump*`. |
| b | Both DS-ASC-001/002 **probe files end with a torn row** (36 and 77 of 116 fields) — the recorder stream was cut mid-line when the flight was reverted. `csv.DictReader`'s default `restval` is `None`, so the missing columns came back as the *object* `None`, which is not the *string* `"None"` the blank-filters test for; the torn tail read as a phase transition and then crashed the string joins in sections 4–8. | `restval=""` — a missing trailing field now reads exactly like an inactive subsystem's blank cell — plus `torn_rows()`, so recorder health **reports** the truncation instead of silently absorbing it. |
| c | The `tools/` script's own header advertises **nine** reported sections, ending *"9 verdict"*. It prints **eight** — there is no verdict section in the code. Documentation-only; no output is wrong. | **Not fixed by S76** (C1.1 — logged, not done). Logged as a register line. |

`plugin/build/assess_flight.py` (the old gen-1-schema script) is **not** extended — its own header forbids
it and that instruction stands. Its header carried two false statements, both corrected in place: it never
read `Crew-2_*.csv` (it globs `flight_*.csv`), and the Crew-2 corpus is no longer gone. The accurate
statement is that **no `flight_*.csv` exists anywhere in this repo**, so that script still has nothing to
read.

---

## 2. Per-file inventory

Row counts are as recorded. **"realtime"** excludes on-rails high-warp rows, where the recorder blanks the
control columns — in the ascent files roughly a thousand rows are a **pre-launch pad hold at MET 0** under
time warp, so the raw row count materially overstates how much flight is in the file.

| File | Flight | Rows (realtime / warp) | MET span | Phases reached | Ended with | Verdict |
|---|---|---|---|---|---|---|
| `Crew-2_20260831_094132.csv` | DS-ASC-001 | 1763 (785 / 978) | 0–181 s | VerticalRise→GravityTurn→Coast→**S2Burn** | ap 118 / pe −6168 km, **suborbital** | **USABLE — the S2 tumble, uncorrected.** The primary attitude-failure record. |
| `Crew-2_Probe_20260831_094424.csv` | DS-ASC-001 booster | 158 (158 / 0) | 0–41 s | **EntryBurn** | 91.7 km, 1709 m/s | **USABLE but tiny + torn tail.** 41 s of a saturated entry burn. |
| `Crew-2_20260831_102133.csv` | DS-ASC-002 | 1688 (713 / 975) | 0–163 s | …→**S2Burn** | ap 121 / pe −6169 km, **suborbital** | **USABLE — the S2 tumble, second instance.** The flight the S2 root-cause regression was run on. |
| `Crew-2_Probe_20260831_102425.csv` | DS-ASC-002 booster | 88 (88 / 0) | 0–23 s | **EntryBurn** | 89 km | **MARGINAL — 23 s, torn tail.** Too short for statistics; a corroborating sample only. |
| `Crew-2_20260831_141924.csv` | DS-DEO-001 | 833 (833 / 0) | 31–247 s | DEORBIT TrunkJettison→Settle→**Burn**; `abort_mode=DeorbitReturn` | ap 342 / pe 313 km — **the burn never delivered** (`dv_planned` 78.04, `dv_delivered` **0.00**, residual 78.04) | **USABLE — the capsule-authority failure.** Capsule alone, 7.7→7.1 t, engine off, RCS only. |
| `Crew-2_20260831_151611.csv` | DS-ASC-003 | 4648 (4018 / 630) | 0–20,315 s | …S2Burn; RV Phasing→**ApproachInit** | 194×404 km; **MMH 0.000** | **USABLE — orbit + far-field rendezvous;** ran dry at 108 km range. |
| `Crew-2_20260831_170204.csv` | DS-ASC-004 | 5694 (4783 / 911) | 0–85,241 s | …S2Burn; RV Phasing→ApproachInit→Phasing | 302×368 km; **MMH 0.000** | **USABLE — the A1/guard flight;** dry at 81 km range. |
| `Crew-2_20260831_194036.csv` | DS-ASC-005 | 5200 (4279 / 921) | 0–85,202 s | …S2Burn; RV Phasing→ApproachInit→Phasing | 316×368 km; **MMH 0.000** | **USABLE — first `app_*` build** (124 cols); dry at 86 km. |
| `Crew-2_20260831_204829.csv` | DS-ASC-006 | 7394 (6486 / 908) | 0–85,108 s | …S2Burn; RV Phasing→ApproachInit→Phasing | 357×371 km; **MMH 0.000** | **USABLE, with a caveat** — dry at 59 km, then **600 s of dead drift** (all actuation zero, `att_point` median 107°). That tail is post-failure coast, not control data. |
| `Crew-2_20260831_220928.csv` | DS-ASC-007 | 4884 (3929 / 955) | 0–85,118 s | …S2Burn; RV Phasing→ApproachInit→Phasing | 350×370 km; **MMH 0.000** | **USABLE — the `acc_*` build** (136 cols). The pre-deadband baseline; the richest attitude record. |
| `Crew-2_20260831_222644.csv` | DS-ASC-007 cont. | 727 (**144** / 583) | 85,119–188,922 s | RV Phasing→CoElliptic→Phasing | 350×370 km; MMH already 0.000 | **JUNK for control work.** 144 realtime rows across 29 hours of warp, tank already empty, `att_point` pinned at 74°. Keep as the mission-outcome record; do not pool it. |
| `Crew-2_20260901_004929.csv` | DS-ASC-008 | 3778 (2866 / 912) | 0–84,752 s | …S2Burn; RV Phasing→**ApproachInit** | 366×402 km; **MMH 0.584** | **USABLE — the deadband build.** The only flight that did **not** run dry. Stopped at 109 km range. |
| `Crew-2_Probe_20260901_005210.csv` | DS-ASC-008 booster | 1314 (1314 / 0) | 0–341 s | **EntryBurn→LandingBurn** | 12.3 km, still descending | **USABLE — the only substantial booster record.** Entry burn through landing burn; not landed. |

**Not recorder CSVs** (the analyser correctly does not read them):

| File | What it is |
|---|---|
| `Crew-2_deorbit_geometry_dump_manual_2500s.csv` | GeometryDump, DS-DEO-001 config: 1 COM, 3 REF axes, **5 PART**, **2 RCSMOD**, **16 THRUSTER** |
| `DS-ASC-008_geometry_dump_pad.csv` | GeometryDump, DS-ASC-008 on the pad: **27 PART**, **8 RCSMOD**, **36 THRUSTER** |
| `DS-ASC-008_geometry_dump_manual_0s.csv` | Same config taken manually at T+0 — identical row census to the pad dump |
| `Crew-2_20260831_204829_KSPlog_excerpt.txt` (1.5 KB) · `Crew-2_20260831_220928_KSPlog_excerpt.txt` (0.9 KB) | KSP.log excerpts for DS-ASC-006 / 007 |
| `DS-ASC-008_screen1/2/3.png` | The three DOCKUI screen captures cross-checked against DS-ASC-008 |

---

## 3. ⭐ The attitude telemetry — what the data shows

All numbers below are computed from `docs/flights/` by `plugin/tools/assess_flight.py` and the derived
authority metrics `act_sat = max|act_pitch,yaw,roll|` and `angacc_<axis>_auth = ctrl_tq_<axis> /
moi_<axis>`. On-rails warp rows are excluded throughout; reversal rates are counted only across
consecutive ticks less than 0.5 s apart, so a warp gap is never counted as an oscillation.

### 3.0 Three columns first, because they change how everything after them reads

1. **`torque_cmd` is EMPTY in all 13 recordings** — 0 of 1763, 0 of 4884, 0 of 1314 … the column is in
   every header and the recorder never wrote a value to it in any file. **There is no recorded commanded
   torque in this corpus.** The commanded-effort signals that *do* exist are `rate_cmd_rads` (identical to
   `att_rate_cmd` in every file) and the normalised actuation `act_pitch/yaw/roll`.
2. **`att_err_deg` and `att_point_deg` are two different signals, not aliases.** They agree (within 0.05°)
   on only **0–18%** of rows. `att_point_deg` is the attitude loop's own pointing error — median 0.0–0.1°
   in nominal flight; `att_err_deg` runs much larger on the same rows (median 3.8–33°) and is what
   `tuning_db.py` labels `aoa_deg`. `docs/flights/README.md` describes `att_err_deg` as "pointing error";
   the recorded data does not support treating the two as one quantity. **Both are reported separately
   below.** Which the deleted recorder intended is not decidable from the data alone and is not decided
   here (§7 Q3).
3. **`mode_holding` / `mode_flying` carry no usable state.** Every Dragon file is `mode_flying=1,
   mode_holding=0` for all but a single tick; every probe file is `0/0` throughout.

### 3.1 When attitude error diverged, and what was commanded at the time — DS-ASC-001 / 002

The S2 tumble begins **at the `Coast → S2Burn` transition**, i.e. at MVac ignition. Sustained divergence
(`att_point_deg` > 10° for 10 consecutive ticks) is first met **3.4 s after S2 ignition** on DS-ASC-001
(MET 146.3 s; ignition 142.9 s) and **at the ignition tick itself** on DS-ASC-002.

DS-ASC-001, every 4th tick (`ctrl_tq_pitch` kN·m, `moi_pitch` t·m², `angacc` = their ratio, rad/s²):

| MET | phase | att_point | att_err | rate_cmd (rad/s) | rate_meas | act P / Y / R | rate P / Y (dps) | ctrl_tq_p | angacc_p |
|---|---|---|---|---|---|---|---|---|---|
| 141.8 | Coast | 10.0 | 2.0 | −0.541 | 0.005 | 0.12 / −0.00 / 0.00 | 0.3 / −0.3 | 61,923 | **36.9** |
| 142.9 | **S2Burn** | 22.0 | 2.7 | −0.936 | 0.011 | 0.20 / −0.09 / 0.00 | 0.6 / 4.1 | 62,192 | **37.0** |
| 146.0 | S2Burn | 9.7 | 29.9 | −0.532 | −0.180 | 0.08 / 0.02 / 0.00 | −10.3 / 10.5 | 62,510 | 37.4 |
| 150.2 | S2Burn | 53.3 | 71.3 | **+2.625** | 0.017 | −0.55 / 0.58 / 0.00 | 1.0 / −6.1 | 62,480 | 37.6 |
| 155.4 | S2Burn | 118.4 | 100.9 | 0.556 | 0.640 | 0.02 / **−0.91** / 0.00 | 36.6 / −20.2 | 62,470 | 37.8 |
| 158.5 | S2Burn | 70.2 | 86.7 | **+3.408** | 0.876 | −0.53 / **−1.00** / 0.00 | 50.2 / 36.7 | 62,443 | 38.0 |
| 161.6 | S2Burn | **161.6** | 147.8 | 0.763 | 0.895 | 0.03 / −0.84 / 0.00 | 51.3 / 69.9 | 62,434 | 38.1 |
| 178.2 | S2Burn | 156.7 | 152.4 | −1.016 | −0.176 | 0.17 / −0.78 / 0.00 | −10.1 / **68.4** | 62,277 | 39.0 |
| 181.4 | S2Burn (end) | 108.9 | 89.7 | −0.529 | −0.405 | 0.03 / 0.67 / 0.00 | −23.2 / 83.0 | 62,260 | 39.2 |

**What the numbers say:**

- **The commanded rate is the thing that runs away, not the actuation.** `rate_cmd_rads` reaches
  **3.41 rad/s (195 °/s)** on DS-ASC-001 and **7.61 rad/s (436 °/s)** on DS-ASC-002, against a *measured*
  `att_rate_meas` that never exceeds **1.01 / 1.18 rad/s**. Throughout the burn the loop asks for a rate
  the stage never achieves.
- **The authority estimate the loop used is physically impossible.** `angacc_pitch_auth` sits at
  **37–39 rad/s²** and `angacc_roll_auth` at **61–65 rad/s²** for the whole S2 burn. `ctrl_tq_pitch` steps
  from 31,455 to 61,923 at separation and then holds ~62,400 kN·m. The **same metric on the same phase
  after the units fix reads `angacc_pitch_auth` p50 = 0.43 rad/s²** (DS-ASC-003…008) — a factor of ~90 in
  the recorded metric itself.
- **Saturation is real but partial.** `act_sat` p50 = 0.58 / 0.66, p95 = 1.00; duty above 0.95 is
  **19–20%** and duty above 0.5 is **56–66%**. `act_yaw` is the saturating axis (repeatedly ±1.00);
  `act_roll` never exceeds 0.09.
- **This is NOT a limit cycle — it is a divergence.** Across the whole S2 burn the measured body rates
  reverse sign only **2–3 times** (0.05–0.10 /s) while `rate_yaw_dps` climbs monotonically to **90 dps**.
  Compare §3.2, where the reversal rate is 10–40× higher at a fraction of the amplitude.

### 3.2 Where the limit cycle IS visible — the terminal rendezvous

Same metrics, segment `RV/ApproachInit`, contiguous realtime ticks only:

| Flight | rows | contiguous | `att_point` p50 / p95 | act_pitch rev/s | act_yaw rev/s | act_roll rev/s | act_sat duty |
|---|---|---|---|---|---|---|---|
| DS-ASC-003 | 984 | 255 s | 76.18 / 168.83 | 0.07 | 0.07 | 0.13 | 0.899 |
| DS-ASC-004 | 1025 | 266 s | 3.10 / 4.73 | 1.07 | 0.35 | 1.16 | 0.423 |
| DS-ASC-005 | 1004 | 260 s | 3.09 / 4.66 | 1.05 | 0.35 | 1.11 | 0.426 |
| DS-ASC-006 | 916 | 238 s | 3.03 / 3.34 | 0.38 | 0.35 | 0.45 | 0.773 |
| **DS-ASC-007** (pre-deadband) | 922 | 239 s | 3.03 / 3.38 | 0.43 | 0.37 | 0.53 | 0.771 |
| **DS-ASC-008** (deadband build) | 462 | 120 s | 3.02 / 3.28 | 0.38 | **0.07** | **0.08** | 0.463 |

The terminal drain window (the last 600 s of realtime in each file):

| Flight | `att_point` p50 | pitch duty>0.05 / rev/s | yaw duty>0.05 / rev/s | roll duty>0.05 / rev/s | trans_z duty | `mmh_frac` over the window |
|---|---|---|---|---|---|---|
| DS-ASC-004 | 3.82° | 0.81 / 0.94 | 0.89 / 0.57 | 0.85 / 0.82 | 0.05 | 0.669 → **0.000** |
| DS-ASC-006 | 106.61° | 0.00 / 0.00 | 0.00 / 0.00 | 0.00 / 0.00 | 0.00 | 0.000 → 0.000 *(already dry — dead drift)* |
| DS-ASC-007 | 3.02° | 0.86 / 0.91 | 0.86 / **1.10** | 0.85 / 0.44 | 0.18 | 0.745 → **0.000** |
| DS-ASC-008 | 3.02° | 0.73 / 0.45 | **0.18** / 0.33 | 0.34 / 0.47 | 0.25 | 0.822 → **0.584** |

**What the numbers say:**

- **The limit cycle is in the ACTUATION, not in the error.** In DS-ASC-004/007 all three attitude channels
  fire **81–89% of the time** and reverse sign roughly **once a second**, while the pointing error sits at
  a median of **3.0–3.8°** and barely moves. Measured as oscillation about its own median,
  `att_point_deg` reverses only **0.13–0.29 /s** and `att_err_deg` only **0.01–0.25 /s**. The error is held
  tight; the price is continuous firing.
- **`att_err_deg` does not show the cycle more clearly than `att_point_deg` does.** In the DS-ASC-007
  terminal window its median is 3.85° but its p95 amplitude about that median is 72° — it is dominated by
  large excursions rather than by the cycle. The cycle is legible in the actuation columns.
- **The deadband build changed exactly the channels a deadband would.** DS-ASC-007 → DS-ASC-008: yaw duty
  **0.86 → 0.18**, yaw reversals **1.10 → 0.33 /s**, roll reversals in ApproachInit **0.53 → 0.08 /s**, and
  the flight **ended with 58.4% MMH instead of 0.0%** — the only recording in the corpus that did not run
  its tank to zero. Pitch is the channel it changed least (duty 0.86 → 0.73).
- **`att_point_deg` sits at a median of 3.02–3.10° in the terminal approach on five separate flights**,
  across three different builds. That constancy is recorded here as an observation; this document does not
  interpret it.

### 3.3 Saturation in the authority metrics, per phase

Pooled over the 13 recordings by `plugin/tools/tuning_db.py` (written to a scratch directory, **not** to
`docs/tuning/` — see the Appendix, note (b)):

| Segment | flights | n | `act_sat` p50 / p95 | `angacc_auth` p50 p/r/y (rad/s²) | `angacc_auth` max p/r/y | `point_err` p50 / p95 |
|---|---|---|---|---|---|---|
| ASCENT/VerticalRise | 8 | 579 | 0.002 / 1.00 | 0.059 / 0.335 / 0.059 | 0.061 / 0.352 / 0.061 | 0.0003 / 0.013 |
| ASCENT/GravityTurn | 8 | 3917 | 0.089 / 0.193 | 0.078 / 0.468 / 0.078 | 0.160 / 0.836 / 0.160 | 0.008 / **0.355** |
| ASCENT/Coast | 8 | 64 | 1.00 / 1.00 | 0.041 / 0.060 / 0.046 | **36.9 / 59.6 / 42.4** | 9.97 / 10.15 |
| ASCENT/S2Burn | 8 | 9148 | 0.047 / **1.00** | 0.436 / **0.000** / 0.435 | **39.2 / 65.3 / 44.9** | 0.052 / 0.311 |
| RV/Phasing | 7 | 8457 | 0.217 / **1.00** | 0.067 / 0.160 / 0.055 | 13.0 / 0.678 / 12.9 | 7.09 / 144.2 |
| RV/ApproachInit | 6 | 5310 | **1.00 / 1.00** | 0.330 / 0.615 / 0.249 | 0.352 / 0.664 / 0.266 | 3.06 / 119.1 |
| ABORT/DeorbitReturn | 1 | 833 | **1.00 / 1.00** | **894.5 / 948.4 / 639.8** | 942.4 / 1003.8 / 674.4 | 83.1 / 115.1 |

- **`ASCENT/GravityTurn` is the one phase with no saturation anywhere** — `act_sat` p95 = 0.19, pointing
  error p95 = 0.36°. It is the only phase in the corpus whose control looks healthy on every flight.
- **The `max` column in ASCENT/Coast and ASCENT/S2Burn is DS-ASC-001/002 pulling the pool.** Those two
  flights' over-read (37–65 rad/s²) sits in the same bin as the six post-fix flights' 0.43 rad/s². The
  pooled `max` for those segments is **not a property of the vehicle**; it is the bug.
- **Roll authority reads as exactly zero for most of S2 on every post-fix flight.** `angacc_roll_auth`
  p50 = **0.000**; `ctrl_tq_roll` is exactly 0 on **74–91%** of S2Burn ticks (DS-ASC-003: 1300 of 1426;
  DS-ASC-007: 1104 of 1500). In the same phase `act_roll` saturates (|·| > 0.95) on **3–16%** of ticks and
  `rate_roll_dps` reaches **23–31 dps**. Note carefully: **not one saturated `act_roll` tick coincides with
  a zero `ctrl_tq_roll` tick** — the zero-authority ticks and the saturated ticks are interleaved in time,
  not simultaneous. On DS-ASC-001 (pre-fix) `ctrl_tq_roll` is instead a near-constant ~12,413 kN·m and
  `act_roll` never exceeds 0.09.
- **DS-DEO-001 is the extreme case of the authority over-read:** `angacc_*_auth` p50 of
  **640–948 rad/s²** on a 7 t capsule, `rate_cmd_rads` p50 **18.6** and max **28.4 rad/s (1627 °/s)**
  against an `att_rate_meas` max of 6.15 rad/s, `act_sat` p50 = **1.000** with 52% duty above 0.95, and
  `rate_pitch_dps` reaching **352 dps**. `dv_delivered` stayed at 0.00 against a planned 78.04 m/s.
- **The boosters are saturated for essentially their entire recorded life.** `act_sat` duty above 0.95:
  DS-ASC-001 probe **1.000**, DS-ASC-002 probe 0.920, DS-ASC-008 probe **0.998**. The first two carry the
  same over-read as their parent flights (`angacc_roll_auth` p50 ≈ 233 rad/s²); the DS-ASC-008 probe,
  post-fix, reads `angacc_*_auth` p50 = 0.000 with p95 1.4–3.1 — **and still saturates**, with
  `rate_yaw_dps` spanning −92 to +104 dps.

---

## 4. Physics self-check — does the corpus agree with itself?

The analyser's section 2 ran on all 13 files:

- **`vspeed` vs d(alt)/dt:** median relative error **0.001–0.009** on 12 of 13 files. The outlier is
  **DS-DEO-001 at median 0.368 / p95 0.807** — expected: the capsule is spinning, so altitude and
  vertical-speed samples are taken in a rapidly rotating frame.
- **Orbital speed vs vis-viva:** **0 of 451 sampled rows** off by more than 3%, across every file with a
  positive periapsis. The orbital state in this corpus is internally consistent.
- **`accel_g`:** max 3.50 g on the two aborted ascents; exactly **4.50 g** on all six orbital ascents (a
  limiter, not a measurement); 0.04 g on DS-DEO-001; and **22.58 g** on the DS-ASC-008 booster — a real
  entry-burn / landing-burn peak on an unmanned stage, not a crew number.
- **No NaN or Inf in any numeric column of any file.**

---

## 5. What the corpus cannot answer

Stated plainly, so no later task mines for what is not there:

1. **Nothing about docking.** No `dock_phase` value is ever set. DS-ASC-008 got closest and stopped at
   **109 km**, 9 km short of the 100 km near-field hand-off.
2. **Nothing about entry, chutes or splashdown.** No `entry_phase`, `chute_phase`, `drogue` or `main`
   transition anywhere in the corpus.
3. **Nothing about a delivered deorbit burn.** The one deorbit recording delivered **0.00 m/s** of a
   planned 78.04.
4. **Nothing about a landed booster.** The best booster record ends at **12.3 km**, still descending.
5. **No commanded torque.** `torque_cmd` was never written (§3.0).
6. **No ballistic-coefficient re-derivation.** These 13 files are not the 48-flight `BoosterDrag` corpus,
   and they do not carry the atmospheric density / drag-acceleration / unpowered-phase marking that
   §B16.8 says such a re-derivation needs.

---

## 6. §B16.8 — the correction of fact made by this task

`docs/BUILD_PLAN.md` §B16.8 said the raw CSVs behind both surviving distillates "are GONE — gitignored,
never committed — so neither can be re-derived or re-checked from anything in this repo." That sentence was
written before W26. It is **still true of both named corpora** and **no longer true in general**, so its
wording has been narrowed to say exactly what is lost and what is back. **§B16.8's rulings are untouched** —
every recovered constant remains UN-CONVERGED until a task actually re-derives it. This was a correction of
fact, not of policy (C1.12).

---

## Open questions for the owner

### Q1 — The quarantine archive still exists on this machine, with ~128 recordings in it.

**Situation.** `plugin/tools/assess_flight.py` used to glob
`C:\Users\User\Desktop\quarantine\dragonscreen_flightdata`. Running the fixed tool with `--external` showed
that directory is **present and populated**: roughly **128 `Crew-2*.csv` files dated 2026-08-26 →
2026-09-01**, including the 13 the repo now holds. That date range is the same window §B16.8 attributes to
the **55-flight TUNING_DB corpus** (2026-08-26 → 08-29), and it extends earlier than anything in
`docs/flights/`. §B16.8 says that corpus "cannot be re-derived, re-binned or re-checked from anything in
this repo" — still true of the *repo*, but the raw data may not be destroyed at all.

**S76 read not one byte of it** beyond the filename listing the glob returned, and this document uses none
of it. C7 puts it off-limits as a build source, and pulling data into the repo is the kind of decision W26
needed an explicit owner call for (C1.12).

1. **Leave it alone; record its existence in §B16.8 and stop there.** *(recommended: it is the smallest
   step that stops the plan asserting something that may be false, it commits nobody to 128 uncurated
   files, and it is free to reverse. §B16.8's "un-converged" ruling is unaffected either way.)*
2. **Open a recovery task** (a W26-style line) to inventory the archive, identify the 55 flights the
   TUNING_DB was built from and the 48 the `BoosterDrag` bc curve was built from, and copy only those into
   `docs/flights/` under the same force-add. **Needs an owner go** — it is a bulk import of 100 MB+ of
   uncommitted data into the repo, and only the owner opens that gate.
3. **Copy the whole archive in** and curate later. Largest, least reversible.
4. **Do nothing and leave §B16.8 as it now stands** (which already no longer claims the general case).

### Q2 — `tuning_db.py` has two limitations that affect any number pooled from it. Fix, or log?

**Situation.** Running it over the repo corpus surfaced two behaviours that are not path bugs, and were
therefore **not** fixed by S76 (C1.1 — log it, do not do it):

- It has **no warp filter**. `assess_flight.py` excludes on-rails warp rows because the recorder blanks the
  control columns there; `tuning_db.py` pools them. In this corpus that is ~9,000 rows, most of them a
  pre-launch pad hold at MET 0.
- Its `segment_label()` **has no `boost_phase` case**, so every booster row — all 1,560 of them — pools
  into a phantom `MISSION/-` segment instead of `BOOST/EntryBurn` and `BOOST/LandingBurn`.

Neither affects the recovered `docs/tuning/TUNING_DB.md`, which was built before the deletion and is no
longer regenerated. Both affect any *new* pooled statistic.

1. **Log both as one register line and fix them there.** *(recommended: they are real defects with a known
   fix, but fixing them inside S76 is scope creep, and `TUNING_DB` is not on any current task's critical
   path.)*
2. **Fix them now**, as part of the analyser reunification.
3. **Leave them** and document the caveat only, on the grounds that Part B replaces this tooling anyway.

### Q3 — Which of the two pointing-error columns is the one to trust?

**Situation.** §3.0 (2) shows `att_err_deg` and `att_point_deg` are distinct signals that agree on 0–18% of
rows. `docs/flights/README.md` calls `att_err_deg` "pointing error"; `tuning_db.py` treats it as `aoa_deg`;
`assess_flight.py` reports `att_point_deg` as the pointing error. The recorder that would settle it is
deleted, and its `Schema[]` comments went with it.

1. **Record the ambiguity in `docs/flights/README.md`, and have every consumer state which column it
   used** — as this document does. *(recommended: it is honest about what the data can and cannot settle,
   and it costs nothing.)*
2. **Rule that `att_point_deg` is the pointing error and `att_err_deg` is angle-of-attack**, and correct
   `flights/README.md` accordingly. Consistent with both scripts' behaviour and with the magnitudes — but
   it is an interpretation the data does not prove.
3. **Recover the deleted `FlightRecorder.cs` from `8b81816^` and read its `Schema[]` comments** to settle
   it from the source. Needs its own task; §B16.8 / BlackBox already want that file read.

---

## Appendix — how to reproduce everything above

```bash
python plugin/tools/assess_flight.py --list
```

```bash
python plugin/tools/assess_flight.py --all
```

```bash
python plugin/tools/assess_flight.py docs/flights/Crew-2_20260831_094132.csv
```

```bash
python plugin/tools/tuning_db.py docs/flights <some_out_dir>
```

**(a)** `--external` additionally sweeps the KSP capture directory and the quarantine archive. Neither is a
build source (C7), so a number taken from them is not reproducible from this repo. Do not use it to produce
a deliverable without an owner decision (Q1).

**(b)** `plugin/tools/tuning_db.py` with **no** `<out_dir>` now **refuses to run** rather than overwrite
`docs/tuning/TUNING_DB.{json,md}` — that file is the recovered **55-flight** distillate, and this repo's 13
recordings cannot rebuild it. Always pass an output directory. (C1.16.)
