# Flight recordings — how to read them

Archived **FlightRecorder** CSVs (and one **geometry dump**) kept as evidence for `docs/FLIGHT_VERIFICATION.md`. These files are normally git‑ignored (`.gitignore`: `*.csv`, `DragonScreen_capture/`); they are **force‑added** because they are the evidence behind the S2 ascent root cause, the deorbit/units‑bug resolution, and the screen (DOCKUI) verification. The recorder + `GeometryDump` write to `<KSP>/DragonScreen_capture/` in flight; these are copies.

## What's here (2026‑08‑31 test session)

| File | Flight ID | Vessel | Rows | Covers |
|---|---|---|---|---|
| `Crew-2_20260831_094132.csv` | **DS‑ASC‑001** | Dragon (S2+Dragon, active) | 1763 | liftoff → MECO → S2 tumble → revert (MET 0→181 s) |
| `Crew-2_Probe_20260831_094424.csv` | DS‑ASC‑001 | Booster (non‑active recovery) | 158 | separation → entry‑burn prep |
| `Crew-2_20260831_102133.csv` | **DS‑ASC‑002** | Dragon (S2+Dragon, active) | 1688 | same profile; **the flight the S2 root‑cause regression was run on** |
| `Crew-2_Probe_20260831_102425.csv` | DS‑ASC‑002 | Booster (non‑active recovery) | 88 | separation → entry‑burn prep |
| `Crew-2_20260831_141924.csv` | **DS‑DEO‑001** | Dragon capsule alone (6.8 t, no gimbal) | 833 | autopilot deorbit; capsule spins under the ×1000 over‑read — **the flight the capsule‑authority regression was run on** (n=832) |
| `Crew-2_deorbit_geometry_dump_manual_2500s.csv` | DS‑DEO‑001 | Dragon capsule alone | 5 parts / 16 thrusters | **geometry dump** (different schema, see below): stock `GetPotentialTorque` vs the geometric, for the deorbit config |
| `Crew-2_20260831_151611.csv` | **DS‑ASC‑003** | Dragon (S2+Dragon → capsule) | 4648 | **the units-fix flight: ascent to ORBIT (194×403 km / 51.6°)** then rendezvous. Proves S2 `ctrl_tq`=526 (fix live) and the rendezvous fuel-exhaustion (far-field TRANSFER burns ~85% of MMH ≈ MET 18,577–18,805) |
| `Crew-2_20260831_170204.csv` | **DS‑ASC‑004** | Dragon (A1 + guard build) | 5694 | **A1 flight:** inserted 366×363 km (50 km below the 417 km ISS), transfer small — but STILL ran dry. The terminal drain is an **attitude limit-cycle** (≈ MET 84,489–85,001): `act_pitch/yaw/roll` ±1 at 68–82% duty while `trans_z` is ~20% duty; guard tripped at mmh 0.20 but attitude alone drained 0.20→0.02 |

Ascent flights (`DS‑ASC`): crewed Falcon 9 + Crew Dragon, RSS/RO, Cape, AUTO‑BOOSTER‑RECOVERY armed; both **fail identically** at S2 (upper‑stage attitude tumble, no orbit) and were reverted. `DS‑DEO‑001`: the Dragon capsule alone on RCS (engine off) — it **spins under autopilot** from the same ×1000 authority over‑read. The filename time = when the recorder opened the stream (`Crew-2_HHMMSS`; `_Probe_` = the non‑active booster's parallel stream).

## Geometry dump format (`*_geometry_dump_*.csv`) — NOT a recorder CSV
Written by the read‑only `GeometryDump` instrument (`plugin/src/GeometryDump.cs`, Alt+G in flight). Schema is `row,part_idx,part_name,stage,mass_t,ax,ay,az,bx,by,bz,power_kn,eP,eY,eR,useZ` where the `row` tag selects the meaning: `COM` (a=vessel CoM world), `REF_RIGHT/UP/FORWARD` (a=control‑frame basis in world), `PART` (a=part world pos, `mass_t`), `RCSMOD` (a=stock `GetPotentialTorque` +, b=−; `power_kn`; `mass_t`=thruster count), `THRUSTER` (a=world pos, b=world thrust dir, `power_kn`). Everything needed to recompute nominal `Σr×F` and compare to stock. **Units note:** `RCSMOD` stock torque is kN·m (KSP); the old `ControlTorque` geometric multiplied `power_kn` by 1000 → N·m — that 1000× is the bug fixed in `AttitudeController.cs`.

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

## Reproduce the deorbit / units‑bug resolution
```python
import csv, math, statistics
def nf(x):
    try: return float(x)
    except: return None

# (3) The capsule's REAL delivered RCS authority (Dragon alone, engine off) — same regression as S2:
rows = list(csv.DictReader(open("Crew-2_20260831_141924.csv")))
seg = [r for r in rows if r.get("rcs_on")=="1" and (nf(r.get("mass_kg")) or 1e9)<12000
       and nf(r.get("act_pitch")) is not None]
xs, ys = [], []
for a, b in zip(seg, seg[1:]):
    dt = (nf(b["met_s"]) or 0) - (nf(a["met_s"]) or 0)
    if not dt or dt<=0 or dt>0.5: continue
    alpha = math.radians(nf(b["rate_pitch_dps"]) - nf(a["rate_pitch_dps"])) / dt
    xs.append(nf(b["act_pitch"])); ys.append(nf(b["moi_pitch"]) * alpha)
n=len(xs); sx=sum(xs); sy=sum(ys); sxx=sum(x*x for x in xs); sxy=sum(x*y for x,y in zip(xs,ys))
K = (n*sxy - sx*sy)/(n*sxx - sx*sx)
print("capsule delivered pitch authority K =", round(-K), " kN.m  (n=%d)"%n)   # -> ~7

# (4) The bug: the loop's own maxAlpha on the capsule (ctrl_tq / moi, both from the CSV):
print("capsule maxAlpha as-flown =", round(statistics.median([nf(r["ctrl_tq_pitch"])/nf(r["moi_pitch"])
      for r in seg if nf(r.get("ctrl_tq_pitch")) and nf(r.get("moi_pitch"))])), "rad/s^2  (real ~0.5)")
```
```python
# (5) The geometry dump shows stock vs the (N.m-bugged) geometric for the SAME config:
rows = list(csv.DictReader(open("Crew-2_deorbit_geometry_dump_manual_2500s.csv")))
mods = [r for r in rows if r["row"]=="RCSMOD"]
stock = sum(max(abs(float(r["ax"])), abs(float(r["bx"]))) for r in mods)   # stock pitch, kN.m
print("stock GetPotentialTorque pitch =", round(stock), "kN.m")            # -> ~2
# geometric (Sr x F) in N.m = ~12870; in correct kN.m = ~12.9. Delivered (above) = 7.
# stock 2 (low), bugged geometric 12870 (1000x high), fixed geometric 12.9 (~ real 7).
```
The as‑flown `ctrl_tq_pitch` here is **~12,870** (the N·m geometric); after the fix it reads **~12.9** in this config and **~526** in S2 — the recorder tell that the units fix is live.

## Reproduce the DS‑ASC‑004 terminal attitude limit-cycle (why A1 still ran dry)
```python
import csv, math, statistics
def nf(x):
    try: return float(x)
    except: return None
rows = list(csv.DictReader(open("Crew-2_20260831_170204.csv")))

# (6) A1 worked: the insertion orbit is ~365 km (50 km below the 417 km ISS), NOT 200 km:
ph = [r for r in rows if r.get("mission_phase") == "PHASING"]
print("A1 insertion: ap=%.0f pe=%.0f km" % (nf(ph[0]["ap_km"]), nf(ph[0]["pe_km"])))   # ~366 x 363

# (7) The drain is ATTITUDE, not translation. Duty cycle over the terminal drain window:
win = [r for r in rows if 84480 <= (nf(r["met_s"]) or 0) <= 85010]
def duty(col): return sum(1 for r in win if abs(nf(r.get(col)) or 0) > 0.05) / len(win)
print("attitude duty  pitch/yaw/roll = %.0f/%.0f/%.0f%%" % (duty("act_pitch")*100, duty("act_yaw")*100, duty("act_roll")*100))
print("translation duty  trans_z      = %.0f%%" % (duty("trans_z")*100))   # attitude ~68-82%, trans ~20%

# (8) It's a limit cycle: yaw rate swings +/- around a small error (no deadband in AttitudeLoop):
seg = [r for r in rows if 84600 <= (nf(r["met_s"]) or 0) <= 84880]
ry = [nf(r["rate_yaw_dps"]) for r in seg if nf(r.get("rate_yaw_dps")) is not None]
ae = [nf(r["att_err_deg"]) for r in seg if nf(r.get("att_err_deg")) is not None]
print("rate_yaw swings %.1f..%.1f dps around att_err median %.1f deg (limit cycle)" % (min(ry), max(ry), statistics.median(ae)))
```
The guard (`RvTranslate`, holds at 20%) fires in `KSP.log` ("return prop 20% ≤ reserve 20% — translation INHIBITED") but the tank still drains to ~2%, because the limit-cycle burns through the **attitude** channel, which the translation guard does not gate. Root cause: `plugin/src/pure/AttitudeLoop.cs:27` — the PID's deadband is omitted.

## Related
- `docs/FLIGHT_VERIFICATION.md` — the flight log + the full root‑cause evidence chain and the proposed fix.
- `plugin/src/pure/FlightRecorder.cs` — the authoritative schema + formatting.
