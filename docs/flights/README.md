# Flight recordings — how to read them

Archived **FlightRecorder** CSVs kept as evidence for `docs/FLIGHT_VERIFICATION.md`. Recorder CSVs are normally git‑ignored (`.gitignore`: `*.csv`, `DragonScreen_capture/`); these four are **force‑added** because they are the evidence behind the S2 ascent root cause and the screen (DOCKUI) verification. The recorder writes to `<KSP>/DragonScreen_capture/` in flight; these are copies.

## What's here (2026‑08‑31 test session)

| File | Flight ID | Vessel | Rows | Covers |
|---|---|---|---|---|
| `Crew-2_20260831_094132.csv` | **DS‑ASC‑001** | Dragon (S2+Dragon, active) | 1763 | liftoff → MECO → S2 tumble → revert (MET 0→181 s) |
| `Crew-2_Probe_20260831_094424.csv` | DS‑ASC‑001 | Booster (non‑active recovery) | 158 | separation → entry‑burn prep |
| `Crew-2_20260831_102133.csv` | **DS‑ASC‑002** | Dragon (S2+Dragon, active) | 1688 | same profile; **the flight the root‑cause regression was run on** |
| `Crew-2_Probe_20260831_102425.csv` | DS‑ASC‑002 | Booster (non‑active recovery) | 88 | separation → entry‑burn prep |

Both flights: crewed Falcon 9 + Crew Dragon, RSS/RO, Cape, AUTO‑BOOSTER‑RECOVERY armed. Both **fail identically** at S2 (upper‑stage attitude tumble, no orbit) and were reverted. The filename time = when the recorder opened the stream (`Crew-2_HHMMSS`; `_Probe_` = the non‑active booster's parallel stream).

## Format
- **First row is the header = the column schema.** The schema is the single source of truth, defined in `plugin/src/pure/FlightRecorder.cs` (`Schema[]`) — read that file for the authoritative, commented list. Columns are added freely over time, so **look up columns by name, never by position.**
- One row per recorder tick (~4–10 Hz, real time; no warp during these ascents).
- **Invariant‑culture numbers** (`.` decimal). Blank cell = that subsystem wasn't active/filled that tick (e.g. docking columns are blank during ascent).
- `met_s` is KSP mission time (0 at liftoff). Liftoff wall‑clock for DS‑ASC‑002 was 10:22:03; map screenshots by their on‑screen MET, not by wall‑clock.

## Column groups (see `FlightRecorder.cs` for the full list + comments)
`met_s, mission_phase, mode_*` (time/mode) · `alt_m, speed_mps, srf_speed_mps, vspeed_mps, q_pa, mach, mass_kg, accel_g, thrust_n` (nav/state) · `att_err_deg, rate_cmd_rads, throttle, rcs_on, trans_*` (control) · `att_point_deg, att_rate_cmd/meas, act_pitch/yaw/roll, ctrl_tq_pitch/yaw` (attitude loop) · `rate_pitch/roll/yaw_dps` (measured body rates) · `ctrl_tq_roll, moi_pitch/roll/yaw, rcs_thrust_n` (control authority) · `ap_km, pe_km, inc_deg, raan_deg` (orbit) · `ascent_phase, pitch_deg, azimuth_deg, upfg_*` (ascent) · `boost_phase, …` (booster) · `rv_*` (rendezvous) · `dock_*` (docking) · `deorbit/entry/chute_*, drogue, main` (return) · `dv_*` (Δv) · `fdir_fault/recovery/abort, abort_mode` (FDIR) · `cal_*` (self‑cal) · `ker_*` (KerbalEngineer cross‑check) · `rcs_geo_pitch/yaw/roll` (raw geometric RCS‑torque estimate).

## The columns that matter for the S2 tumble
| Column | Meaning |
|---|---|
| `ascent_phase` | `VerticalRise → GravityTurn → Coast → S2Burn`. The tumble begins at the `Coast→S2Burn` transition (MVac ignition). |
| `att_err_deg` | pointing error. ~0–2° through MECO/coast; ramps to 100°+ once S2 lights. |
| `rate_pitch_dps / rate_yaw_dps / rate_roll_dps` | measured body rates (deg/s). Roll stays ~0 (roll‑trim); pitch/yaw run away. |
| `act_pitch / act_yaw / act_roll` | commanded actuation −1..1 (finite/active in S2 → the failure is **not** a NaN/zeroed command). |
| `ctrl_tq_pitch / ctrl_tq_yaw` | the loop's **estimated** control torque. ~62,000 in S2. |
| `rcs_geo_pitch / rcs_geo_yaw` | raw geometric RCS‑torque estimate. In S2 ≈ `ctrl_tq` → the estimate is **all RCS**. |
| `moi_pitch / moi_yaw` | moment of inertia. Drops ~45× at separation (real). |
| `rate_cmd_rads` | commanded body rate. 0.01 in S1 → peaks ~7.6 rad/s in the S2 tumble. |

## Reproduce the two key findings (Python, stdlib only)
```python
import csv, math
rows = list(csv.DictReader(open("Crew-2_20260831_102133.csv")))
def f(x):
    try: return float(x)
    except: return None
s2 = [r for r in rows if r["ascent_phase"] == "S2Burn"]

# (1) The authority OVER-READ: maxAlpha = controlTorque / MOI
r = s2[10]
print("S2 maxAlpha =", f(r["ctrl_tq_pitch"]) / f(r["moi_pitch"]), "rad/s^2  (S1 is ~0.16; >20 is impossible)")

# (2) The ROOT CAUSE — regress net torque (MOI*dw/dt) vs commanded actuation:
#     slope = real per-unit control torque; intercept = disturbance torque.
xs, ys = [], []
for a, b in zip(s2, s2[1:]):
    dt = f(b["met_s"]) - f(a["met_s"])
    if not dt or dt <= 0: continue
    alpha = math.radians(f(b["rate_pitch_dps"]) - f(a["rate_pitch_dps"])) / dt
    xs.append(f(b["act_pitch"])); ys.append(f(b["moi_pitch"]) * alpha)
n = len(xs); sx=sum(xs); sy=sum(ys); sxx=sum(x*x for x in xs); sxy=sum(x*y for x,y in zip(xs,ys))
K = (n*sxy - sx*sy) / (n*sxx - sx*sx); D = (sy - K*sx) / n
print("real control torque K =", round(-K), " disturbance D =", round(D))
# → K ≈ 445 here (≈451 when windowed to steady S2) — the real authority is the gimbal, NOT the ~62000
#   the loop used; D ≈ 0 (no disturbance).
# The loop over-reads authority ~137× → commands rates the stage can't achieve → divergent limit cycle.
```
The headless proof of this (a faithful port of `pure/AttitudeLoop.Axis` fed these numbers) lives in `plugin/test/AttitudeLoopTest.cs` (`build.py test`): the 137× over‑read limit‑cycles; the correct estimate converges.

## Related
- `docs/FLIGHT_VERIFICATION.md` — the flight log + the full root‑cause evidence chain and the proposed fix.
- `plugin/src/pure/FlightRecorder.cs` — the authoritative schema + formatting.
