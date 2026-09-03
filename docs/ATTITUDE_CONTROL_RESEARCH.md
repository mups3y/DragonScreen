> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-26; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⚠ **Named contradiction:** it designs the **hand-written** control loop that was deleted 2026-09-01. Part B builds a **pinned, privately-namespaced MechJeb embed + a pure conductor** (§B1–B16 / T15–T22) instead. ⚠ R1 §7 records this as **the reasoning that FAILED** — read it with `docs/AUTOPILOT_RECOVERY_AUDIT.md` §3.2 and §7.

# Attitude Control — the gimbal loop, ported from MechJeb (CONFIRMED by reading the source in full)

The autopilot's max-Q loss of control is a CONTROL-LAW problem, not an authority problem (S1 gimbal ±5° =
~10 MN·m torque vs ~0.3 MN·m transonic aero — [[INSTALLED_MODS_RESEARCH]]). Stock SAS's PID is too slow to
catch FAR's transonic divergence. MechJeb2 (installed) flies RO rockets on gimbal alone; its `BetterController`
is the proven law. Below is EXACTLY what it does, read in full from `Desktop/mechjeb_src/MechJeb2/
AttitudeControllers/BetterController.cs` + `DirectionTracker.cs` — this is the port target, not an assumption.

## 1. The frame conversion (BetterController.UpdatePredictionPI)
```
currentAttitude = (QuaternionD) vessel.ReferenceTransform.rotation * Quaternion.Euler(-90, 0, 0);
requestedAttitude = <target attitude>   // = Quaternion.LookRotation(worldForward, worldUp) for a world dir
```
The `Euler(-90,0,0)` rotates the KSP control transform (nose = +Y) into the LookRotation convention (nose =
+Z). So `currentAttitude`'s +Z = the vessel nose, and `requestedAttitude` = `LookRotation(dir, up)` points +Z
at `dir`. To point the nose at a world direction: `requestedAttitude = Quaternion.LookRotation(dir, up)` where
`up` is the roll reference (any vector ⟂ dir if roll is free).

## 2. The error (DirectionTracker.Desired)
```
delta   = Inverse(currentAttitude) * requestedAttitude;      // body-frame rotation current→desired
euler   = delta.eulerAngles;                                 // degrees
pitch   =  ClampPi(Deg2Rad(euler.x));
yaw     = -ClampPi(Deg2Rad(euler.y));                        // ⚠ yaw is NEGATED
roll    =  ClampPi(Deg2Rad(euler.z));
error   = (pitch, roll, yaw);    // ⚠ ORDER is pitch,roll,yaw  (index 0,1,2)
distance = acos( cos(pitch)*cos(yaw) );                      // total pointing error (roll excluded)
```

## 3. The per-axis control law (i = 0 pitch, 1 roll, 2 yaw)
```
controlTorque[i] = Σ ITorqueProvider.GetPotentialTorque()   // available control torque (gimbal+RCS+…)
MOI[i]           = vessel.MOI[i]                             // moment of inertia
maxAlpha[i]      = controlTorque[i] / MOI[i]                 // max angular accel
effLD            = soften² · maxAlpha[i] / (2·posKp²)        // linear/braking blend width (soften=0.5, posKp=2.03)
maxOmega         = maxAlpha[i] · MaxStoppingTime (=2 s)      // (or PI/MinFlipTime for a flip)

if |error[i]| <= 2·effLD:                                    // small error → position PID
    targetOmega[i] = posPID.Update(desired[i], current[i])   // Kp=2.03, Ti=1.97
else:                                                        // large error → ARRESTABLE-RATE braking curve
    targetOmega[i] = soften · sqrt(2·maxAlpha[i]·(|error[i]| − effLD)) · sign(error[i])
    targetOmega[i] = clamp(targetOmega[i], −maxOmega, maxOmega)
if distance·(180/π) > RollControlRange(=5°):  targetOmega[roll]=0   // don't fight roll until pointed

targetAlpha[i] = velPID.Update(targetOmega[i], vessel.angularVelocityD[i])   // Kp=7.98, out clamped ±maxAlpha
targetTorque[i]= MOI[i] · targetAlpha[i]
actuation[i]   = −targetTorque[i] / controlTorque[i]        // ⚠ NEGATIVE (KSP actuation orientation)
```
Apply: `s.pitch = actuation[0]; s.roll = actuation[1]; s.yaw = actuation[2]` (clamp ±1; 0 if torque≈0/NaN).

## 4. Inputs from KSP (all available in this install)
- `vessel.MOI` (Vector3) — moment of inertia. `vessel.angularVelocity` — body rates.
- **`ITorqueProvider.GetPotentialTorque(out Vector3 pos, out Vector3 neg)`** on every module — sum for the
  available control torque. ⚠ stock gimbal torque was buggy; **KSPCommunityFixes (installed) fixes it**, so
  the reported torque is trustworthy here.
- `vessel.ReferenceTransform.rotation` — the control-frame rotation.

## 5. What this gives us vs our pure `ControlLaw`
Our `pure/ControlLaw.cs` + `pure/Authority.cs` ALREADY hold the core idea (the arrestable-rate curve
`ω=√(2αθ)`, torque = I·Δω, actuation = torque/available). What we were MISSING is the frame-correct glue:
the `Euler(-90,0,0)` conversion, the quaternion error → (pitch,roll,yaw) with the **negated yaw**, and the
**negative actuation sign**, plus summing `GetPotentialTorque` for the live authority. The port:
- keep the pure law (verified, headless-tested),
- add a GLUE `AttitudePilot` that: builds `requestedAttitude` from the guidance's world aim, computes the
  error exactly as §2, feeds each axis through `ControlLaw` (or the two-PID cascade above), and writes
  `s.pitch/roll/yaw` with the negative sign — driving the GIMBAL directly, no SAS.
- This is the "direct full control" the rule demands ([[direct-part-control-hard-rule]]) AND the fix for the
  transonic max-Q divergence (a fast loop that actually uses the ample gimbal authority).

## 6. Real SpaceX parallel (fidelity note)
Falcon 9 flies a **triple-redundant flight computer** that gimbals the engines closed-loop to hold the
guidance attitude at **zero AoA** through max-Q (load relief), then the same controller flies the boostback/
entry/landing on grid-fins + gimbal. Our stack mirrors this: guidance (UPFG/pitch-program/named-burns) →
this attitude controller → gimbal/RCS actuation, with FDIR + abort over the top. See
[[dragonscreen-autopilot-rebuild-plan]], docs/TRUE_AUTOPILOT_ARCHITECTURE.md, docs/FLIGHT_SOFTWARE_PLAN.md.
