> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-26; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# MechJeb2 wiki — read in full + reconciled against the current source (2026-08-26)

Source: the MechJeb2 GitHub wiki (all 15 pages read: Home, Ascent Guidance, **Attitude Adjustment (PIDs)**,
Landing Guidance, Smart A.S.S., Translatron, Utilities, Modules, Maneuver Planner, Maneuver Node Editor,
Vessel/Orbit Info, Delta-V Stats, Version history, Compiling). Cross-checked against **current master**
`MechJeb2/AttitudeControllers/BetterController.cs` + our local `Desktop/mechjeb_src` checkout.

> ⚠ **CURRENCY FINDING (the reason to read the source, not just the wiki):** the wiki's **PID numbers are
> STALE.** The *Attitude Adjustment (PIDs)* page documents an **older single-velocity-PID BetterController**
> (`Kp` velocity gain 50, a direct `LD` 0.1, `MinFlipTime` 20). The **current** BetterController — in master
> AND in our checkout AND what we ported in Step B — is a **two-PID cascade**: position PI `PosKp 2.03 /
> PosTi 1.97`, velocity P `VelKp 7.98`, **computed** `effLD = soften²·maxAlpha/(2·posKp²)`, `MinFlipTime 120`,
> `Soften 0.5`, `SmoothTorque 0.10`. Our port matches current master line-for-line. **The wiki's design
> philosophy + tuning methodology remain valid; its specific constants do not.** Authority order stays:
> live source > our checkout > wiki prose.

---

## 1. Which PID controller — settled

The wiki lists four (MJAttitudeController, KosAttitudeController, HybridController, **BetterController**) and
recommends **BetterController**. A web check confirms **BetterController is the DEFAULT in the MechJeb2-RO
(Realism Overhaul) branch** — the RO maintainers' chosen controller for *exactly our environment* (RSS/RO,
gimbal-only, no reaction wheels). It is the most capable design: arrestable-rate braking (never commands a
rate it can't stop), MOI-normalized, minimal overshoot, and it **treats user/aero input as an external
disturbance to be rejected** — precisely the FAR transonic-divergence problem. **→ BetterController is best
for us, and we already ported it (Step B). No change of controller.** [wiki: Attitude Adjustment; RO branch
default]

## 2. BetterController design (wiki, still accurate)

Based on ArduCopter 2.9 `ALT_HOLD`. Two-stage cascade: **(1)** angular *position* → target angular
*velocity* via P / √P (linear near zero, square-root braking curve far out); **(2)** target velocity →
actuation via a velocity PID. The third (acceleration) cascade stage was dropped — "not reliable enough."
Result: "fast, with minimal overshoot, with a smooth decrease in acceleration." This is exactly our
`pure/AttitudeLoop.cs`.

## 3. The tuning methodology (wiki — the part worth keeping) → maps to OUR knobs

The wiki tunes the *old* names (`Kp`, `LD`); the method translates to our current cascade:

| Wiki knob | What it does | OUR current equivalent |
|---|---|---|
| `Kp` (velocity P) | snappiness of velocity tracking; too high → jitter | `VelKp` (default **7.98**) |
| `LD` (position transition) | where linear-P → √P; **lower = more gain = more overshoot** | **computed** `effLD` (tune via `Soften` 0.5 and `PosKp` 2.03) |
| `Ki` (velocity I) | **leave 0** — windup/overshoot, no steady-state benefit | `VelTi = 0` ✓ |
| `Kd` (velocity D) | optional, ~0.1 max, marginal | `VelTd = 0` ✓ |

**The procedure (how to tune in flight, from the wiki):**
1. Command KILL-ROT / a fixed target. Set a reasonable `LD`, `Ki=Kd=0`.
2. Raise the velocity gain until the **Actuation** column jitters (flips ±1 fast) — then **halve** it.
   Typical range **10–100**. (Our 7.98 is deliberately conservative — light RCS vessels jitter sooner.)
3. Then reduce `LD` (raise effective position gain) to the **lowest value with no overshoot**. The tell:
   as you approach the target the pitch/yaw actuation should **saturate opposing the motion, then "snap" to
   ~50%, then settle to 0.** No snap → too gentle (reduce LD). Over-snap / reverses → overshoot (raise LD).

**We already record `act_pitch/act_yaw/act_roll` + `att_point_deg` + `att_rate_cmd/meas`**, so this exact
diagnostic is readable from the proving-flight CSV — jitter = actuation railing ±1; overshoot = the point
error crossing zero and coming back.

## 4. ⭐ Per-vehicle tuning — the answer (full stack / booster / S2 / capsule+trunk / capsule+heatshield)

**BetterController is MOI-normalized, so ONE gain set flies every configuration — no per-config schedule.**
Everything downstream of `maxAlpha = controlTorque / MOI` (the effLD blend width, the `maxAlpha·MaxStoppingTime`
rate cap, the velocity-PID output clamp ±maxAlpha) **scales with the live plant**, which we read every tick
(`v.MOI`, `Σ ITorqueProvider.GetPotentialTorque`). The gains (`PosKp/PosTi/VelKp/Soften`) tune the
*dimensionless, normalized* response, so they are the same whether the plant is:

| Config | When | Attitude effectors | Note |
|---|---|---|---|
| **Full stack** | pad → MECO | octaweb gimbal (~10 MN·m), heavy MOI | well-damped |
| **Booster** | S1 recovery | 3/1-engine gimbal + grid fins + cold-gas | maxAlpha varies by octaweb mode |
| **S2** | SES → SECO | single MVac gimbal (pitch/yaw) + RCS (**roll only on RCS**) | keep RCS master ON in S2 for roll |
| **Capsule + trunk** | rendezvous / dock / depart | Draco RCS only (no gimbal) | all-axis RCS; lower authority |
| **Capsule + heat shield** | entry | Draco RCS only, **legs in the heat shield**, CoM-shifted | lightest MOI; CoM shift changes trim, MOI read live |

The wiki's own caveat: the defaults "produce good results with larger vessels with insufficient control…
while allowing a little bit more overshoot for smaller rockets with excessive torque." So the **light
RCS-only configs (S2 roll, capsule, entry) may show slightly more overshoot** than the heavy stack with the
same gains — that is acceptable and, if a proving flight shows it, the fix is a **global** `Soften` bump (less
aggressive) or verifying the live control-torque is accurate — **NOT** five separate gain sets. Our
`pure/Authority.cs` already stated this design intent; the wiki confirms it. **The single most important thing
for the light configs: the live `controlTorque` must be right** — hence the RCS-master gate (Step B fix) and
the SmoothTorque filter (§5).

## 5. ⭐ Improvement we were missing: SmoothTorque (control-torque low-pass)

Current BetterController **low-pass filters the control torque** before using it:
```
_controlTorque = (first tick) ? raw : _controlTorque + SmoothTorque·(raw − _controlTorque);   // SmoothTorque = 0.10
for each axis: if (raw[axis] == 0) _controlTorque[axis] = 0;   // but drops to zero authority are INSTANT
```
Our Step-B `AttitudePilot.ControlTorque` used the **raw instantaneous** sum. The EMA matters because the
available torque *fluctuates*: the octaweb gimbal authority scales with throttle (so it dips in the max-Q
throttle bucket), and RCS authority toggles on/off. Feeding the raw value straight into
`actuation = −MOI·α/controlTorque` makes the actuation spike when authority jumps. The 0.10 EMA smooths the
*rises*; the `raw==0 → 0` guard keeps *drops* instant (so cutting the engines still reads zero authority
immediately). **→ Ported into AttitudePilot (2026-08-26).**

## 6. Wobble rockets (a flight-diagnostic learning, not a code change)

The wiki is explicit: if, after KILL-ROT, the pitch/yaw terms **oscillate slowly between the rails and the
rocket wobbles in a circle**, that is a **structural** problem (KSP joint ragdolling), **NOT** a PID problem —
"not fixable in MechJeb." Fix with Kerbal Joint Reinforcement or autostruts, not gains. **For our proving
flights this is the key discriminator:** *fast* actuation jitter (railing ±1 every tick) = over-gained PID
(lower VelKp / raise effLD); *slow* oscillation + physical wobble = FAR/RO joint flex (autostrut the stack,
don't touch the loop). We must not chase gains for a structural wobble — it would waste flights.

## 7. Ascent guidance (wiki) vs ours — confirmations

- **AoA limiter with dynamic-pressure fadeout** — we have it (`MaxAoaDeg` ramping to 0 by `QAoaZeroPa`). The
  wiki notes stock users *disable* the AoA limiter for Δv efficiency; **we must KEEP it** — for a FAR-unstable
  airframe it is load relief, not an efficiency knob. [Ascent Guidance]
- **Corrective steering** = steer on the velocity vector, not the position vector — the same idea as our
  zero-AoA prograde-cone clamp. ✓
- **Limit Q** — our max-Q throttle bucket is the equivalent (measured q, not scheduled). ✓
- **Guidance:** MechJeb replaced PEG with **PVG** (Powered Vessel Guidance). We fly **UPFG** (Shuttle Unified
  Powered Flight Guidance) — same closed-loop optimal-insertion family; both need continuous thrust. ✓
- Autostage / fairing gates — we actuate directly (Actuator), don't need MechJeb's autostage. ✓

## 8. Other modules noted (for later steps, not now)

- **RCS Balancer / SmartRcs** — balanced RCS translation (fire only the thrusters that give pure translation
  with minimal attitude disturbance). Relevant to **capsule prox-ops (Steps F/G)** — our
  "attitude-first-then-translate" already avoids the coupling, but a balancer is the refinement if translation
  induces attitude error. [Modules; Utilities]
- **Flight Recorder** — read the SOURCE (`MechJebModuleFlightRecorder.cs`). ⭐ Its architecture is the fix
  we applied to our own recorder (2026-08-26): a single `RecordStruct` is filled **every sample from live
  vessel state**, NOT gated on which "phase/controller" is active — so an abort/coast/cutout is never lost.
  It records **BOTH `SpeedSurface` and `SpeedOrbital`**, `AoA/AoS/AoD`, `Acceleration`, `Q`, and the
  `GravityLosses/DragLosses/SteeringLosses` decomposition. We adopted: (1) an always-on base snapshot
  (`FlightRecorder.PutBase`) writing phase/mode/surface-speed/AoA/felt-g/measured-thrust/RCS/abort every tick;
  (2) both surface + orbital speed columns. **TODO (not yet):** the gravity/drag/**steering**-loss columns
  (steering loss = `dt·thrustAccel·(1−dot(v̂,fwd))`, harvest §L) — they need per-tick integration in the ascent
  controller and would directly diagnose the excessive-q ascent; add when we return to ascent tuning.
- **Translatron** = vertical-speed hold — not needed; our hoverslam is the direct `√(2ad)` law.
- **Delta-V Stats** — confirms MechJeb tracks **atmospheric vs vacuum** thrust/Isp separately, which validates
  our Step-C clamp-gate choice to use the **current-conditions** max thrust (`maxFuelFlow·flowMultiplier·Isp·g0`),
  not the static vacuum `maxThrust`. ✓
- **Smart A.S.S.** — the attitude-reference set (surface/orbit/target/advanced + KILL-ROT + force-roll). Our
  guidance produces the world aim directly; the AttitudePilot is the "hold that attitude" half. Force-roll ≈
  our roll-damp / entry-bank channel.

## 8b. Build / Compiling page — KEPT FOR LATER (screens work, user 2026-08-26)

Not relevant to the autopilot, but held as reference for future **screens-side** improvements. MechJeb builds
as a C# library referencing, from `KSP_Data/Managed`: **Assembly-CSharp.dll, Assembly-CSharp-firstpass.dll,
UnityEngine.dll** (+ `System.Data.Linq`), non-C# assets embedded as `MuMech.Properties.xxxxxx`. (Our own build
is Roslyn via `plugin/build.py` against the same KSP managed assemblies — see the build memory; this MechJeb
recipe is a cross-reference for UI/asset packaging patterns if we extend the screens.)

## 9. Net reassessment of Steps A / B / C

- **A (Actuator):** unchanged. Wiki's direct-actuation modules (RCS Balancer, autostage) align with our
  by-capability approach. RCS Balancer noted for Steps F/G.
- **B (AttitudePilot):** controller choice **confirmed best (RO default)**; port **confirmed current** (matches
  master constant-for-constant). **One improvement applied: SmoothTorque control-torque EMA (§5).** Key
  understanding gained: **no per-config gain scheduling** (§4) — MOI-normalization is the per-vehicle
  adaptation; the in-flight tuning method (§3) and the wobble-vs-jitter discriminator (§6) are now in hand for
  reading the proving flights.
- **C (ullage/clamp):** the current-conditions-thrust choice is **validated** by MechJeb's own atm/vac
  separation (§8). No change.
