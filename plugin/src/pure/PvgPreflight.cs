// DragonScreen — PvgPreflight  (PURE: what must be TRUE before PVG is handed the vehicle)
// ============================================================================================
// ⛔⛔ **THIS FILE EXISTS BECAUSE OF ONE FLIGHT: 2026-09-07, register S214.** The vehicle sat on the
// pad and did nothing. The whole of T18–T21 was correct; the vehicle was never given a trajectory to
// fly. Everything below is derived FROM THE VENDORED SOURCE (`plugin/mech/`), not from a guess, and
// every claim carries the file and line it was read off. Keep the citations: they are the only way a
// later reader can check this against a re-pinned MechJeb (§B12.1) without re-earning the diagnosis.
//
// ============================================================================================
// ⭐ 1. THE DEFECT, AS PROVEN — an EMPTY PHASE TABLE, not a bad number
// ============================================================================================
// `MechJebLib/PSG/AscentBuilder.cs` — `Build()`'s **first statement** is:
//
//     double m0 = _phases[0].M0;
//
// `_phases` is a `PhaseCollection`, and `PhaseCollection : List<Phase>`
// (`MechJebLib/PSG/PhaseCollection.cs:20`). `List<T>`'s indexer on an EMPTY list throws
// `ArgumentOutOfRangeException: ... Parameter name: index` — **which is, word for word, the exception
// the flight logged**, `Parameter name: index` included. It is the ONLY unguarded index in `Build()`.
//
// `_phases` is filled ONLY by `AddStage` / `AddCoast`, and the only caller of either is the stage loop
// in `MechJeb2/MechJebModulePSGGlueBall.cs:245-303`, which walks `Core.StageStats.VacStats`.
//     ⇒ **`VacStats` empty ⇒ zero phases ⇒ that exact throw.**
//
// ⭐ **AND IN A HEADLESS BUILD IT IS NOT A RACE — IT IS DETERMINISTIC.** `MechJebModuleStageStats`
// harvests results in `OnFixedUpdate` but **never starts a simulation of its own**; only
// `RequestUpdate()` calls `TryStartSimulation()` (`MechJebModuleStageStats.cs:254-258`). In stock
// MechJeb the GUI pumps that constantly (`MechJebModuleAscentMenu.cs:382`, and eight sites in
// `MechJebModuleInfoItems.cs`). **T15b suppressed the GUI.** The only remaining caller is
// `MechJebModuleAscentBaseAutopilot.OnFixedUpdate` (`:119`) — which `MechJebCore.FixedUpdate` runs only
// `if (module.Enabled)` (`MechJebCore.cs:587`). So nothing starts the fuel-flow simulation until we
// enable the ascent module, and the first `Drive` after enabling it therefore ALWAYS sees an empty
// `VacStats`. **We removed the pump and did not replace it.**
//
// ⚠ **AND THE PAD IS THE ONE PLACE MECHJEB DOES NOT GUARD ITSELF.** `MechJebModulePSGGlueBall.cs:166`
// early-returns on an unusable stage table — but only `if (Vessel.VesselOffGround())`, and
// `VesselOffGround()` is FLYING/SUB_ORBITAL/ORBITING/ESCAPING only (`VesselExtensions.cs:19-21`).
// On `PRELAUNCH` it is false, the guard is skipped, and the empty table reaches `Build()`.
//
// ============================================================================================
// ⛔ 2. WHAT IT IS **NOT** — the inclination SIGN is INNOCENT, and this is the proof
// ============================================================================================
// The suspicion handed to S214 was that the configure line's `@ -51.6000°` — a NEGATIVE inclination —
// was out of the solver's domain. **It is not. It is disproven by the source, three ways:**
//
//   (a) **`AscentBuilder.SetTarget` performs NO validation whatsoever.** It assigns ten fields and
//       returns `this`. It has no branch and cannot throw. The logged frame is `Build()`, not it.
//   (b) **ALL SEVEN terminal-constraint classes take the ABSOLUTE VALUE of it.**
//       `Kepler3.cs:48` and `FlightPathAngle4.cs:52` — `double incT = Abs(_incT);` — and
//       `Kepler4.cs:36`, `Kepler5.cs:40-41`, `FlightPathAngle3Energy.cs:44`,
//       `FlightPathAngle4Energy.cs:37`, `FlightPathAngle5.cs:41` all pass `Abs(_incT)`. The sign is
//       stripped by the solver before it is ever used.
//   (c) **The sign is a DELIBERATE MechJeb convention, not an accident.**
//       `MechJebModuleAscentPSGAutopilot.cs:114` reads
//       `inclination = Math.Sign(inclination) * Core.Target.TargetOrbit.inclination;` — MechJeb
//       carries the sign ITSELF to encode launch-azimuth direction. A negative inclination is designed
//       for.
//
// **The ONLY validation anywhere on the inclination's path through the PSG library is
// `Check.Finite(incT)` (`FlightPathAngle4.cs:33`).** There is no range check to violate. So the domain
// this file enforces is the real one — FINITE, and a physically meaningful magnitude — and T18's Q1
// (which SIGN to fly) is untouched by S214: it remains an open TASTE question, and it was never the
// cause of anything.
//
// ============================================================================================
// ⭐ 3. WHO OWNS THE THROTTLE — the four windows, answered from source
// ============================================================================================
// S214's brief asked for this to be STATED, because the flight showed it was undefined. It is:
//
//   • **PRE-IGNITION** — MechJeb. `MechJebModuleAscentBaseAutopilot.DrivePrelaunch()` (`:183-218`)
//     calls `Core.Thrust.ThrustOff()`, which writes `Vessel.ctrlState.mainThrottle = 0` and
//     `Tmode = TMode.OFF` (`MechJebModuleThrustController.cs:263-272`). ⚠ **NOT "every tick", as was
//     suspected** — with no solar panels to retract it falls straight through to `_mode = ASCEND`
//     (`:216-218`), which is why the flight logged `Prelaunch -> Ascend` ONCE, 176 ms in. It is a
//     single write of zero, and it is the LAST word anyone says about the throttle before ignition.
//
//   • **IGNITION (light → 99 % → clamp release)** — ⛔ **NOBODY. THIS IS THE GAP.** We activate the
//     engine modules and command no throttle at all: `FlightDriver.SetThrottle` is an honest no-op
//     (`FlightDriver.cs:222`) and its only caller is `Actuator.FireAbort` (`Actuator.cs:440`). The
//     Dragon conductor never writes `mainThrottle`. And MechJeb will not fill the gap while
//     `Solution == null`, because `HandleThrottle` returns on that as its first line
//     (`MechJebModuleGuidanceController.cs:292-296`). So the octaweb lights into a commanded ZERO.
//     **That is the whole of "0/8227 kN, 1 lit".**
//
//   • **POST-RELEASE** and **ASCENT** — MechJeb, by the same one path:
//     `HandleThrottle -> ThrottleOn()` -> `Core.Thrust.TargetThrottle = _throttle > 0 ? _throttle : 1f`
//     (`MechJebModuleGuidanceController.cs:418`), with `_throttle` coming from
//     `Solution.InertialGuidance` (`:370`).
//
// ⭐⭐ **AND THE FIX FALLS OUT OF THAT TABLE, WHICH IS WHY THERE IS ONE FIX AND NOT TWO.**
// `HandleThrottle` is **NOT** gated on `IsGrounded()` — compare `HandleTerminal` (`:150`) and
// `UpdatePitchAndHeading` (`:354`), which both are. So **the moment a solution exists, MechJeb raises
// the throttle ON THE PAD by itself** — exactly what our light-then-release design needs. Guidance and
// throttle were never two failures. They are one: no solution.
//
// ⛔ **THEREFORE THE ORDER IS: SOLUTION FIRST, THEN LIGHT THE STAGE.** Not "light the stage and give
// the gate longer". `IgnitionGate`'s 2 s / 99 % rule is UNCHANGED and must stay unchanged — it fired
// correctly on 2026-09-07 and it is the only thing that kept the hold-downs bolted to a cold octaweb
// (§B12.7). What S214 changes is only WHEN its clock is allowed to start.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>
    /// The preconditions that must hold before the PVG ascent autopilot is handed the vehicle.
    /// Pure: the caller reads MechJeb, this decides. See the header for the source of every rule.
    /// </summary>
    public static class PvgPreflight
    {
        // ── the inclination domain ──────────────────────────────────────────────────────

        /// <summary>
        /// The largest meaningful inclination magnitude, degrees. ⚠ **NOT A TUNING VALUE and NOT an
        /// invention (§1.4):** an orbit's inclination is defined on 0–180°, and every terminal
        /// constraint in the vendored solver reduces ours with `Abs()` (header §2b), so ±180 is the
        /// exact edge of what the solver can be asked for. Stated as a constant rather than buried in
        /// the comparison so a test can pin it without reading the predicate.
        /// </summary>
        public const double MaxInclinationDeg = 180.0;

        /// <summary>
        /// Is this an inclination the vendored solver can actually be given?
        ///
        /// ⭐ THE DOMAIN IS **FINITE AND |inc| ≤ 180**, and it is the source's, not ours: the only
        /// check on the inclination's whole path through the PSG library is `Check.Finite(incT)`
        /// (`FlightPathAngle4.cs:33`), and all seven terminals take `Abs(_incT)` (header §2b).
        /// ⛔ **THE SIGN IS DELIBERATELY ACCEPTED.** A negative inclination is MechJeb's own
        /// launch-azimuth convention (`MechJebModuleAscentPSGAutopilot.cs:114`); rejecting it here
        /// would answer T18's open Q1 by the back door, which is not this line's to answer.
        /// </summary>
        public static bool InclinationInDomain(double inclinationDeg)
        {
            // ⚠ **THIS LINE IS AN EQUIVALENT MUTANT AND IS KEPT ON PURPOSE — SAID PLAINLY SO NOBODY
            // "TIGHTENS" IT LATER BELIEVING IT IS LOAD-BEARING.** S214's mutation run deleted it and the
            // suite stayed green; that is not a weak test, it is a redundant statement, and the
            // redundancy is PROVEN for every double: `NaN <= 180.0` is false, `+∞ <= 180.0` is false,
            // and `-∞` becomes `+∞` at the magnitude step below — so the comparison already rejects all
            // three and no input can distinguish the two programs. It stays because `Check.Finite` is
            // literally the only check the vendored solver performs on this value
            // (`FlightPathAngle4.cs:33`), and saying so in the same shape the source says it is worth
            // one dead branch. ⛔ It would STOP being redundant the moment the comparison below changes.
            if (double.IsNaN(inclinationDeg) || double.IsInfinity(inclinationDeg)) return false;
            double mag = inclinationDeg < 0.0 ? -inclinationDeg : inclinationDeg;
            return mag <= MaxInclinationDeg;
        }

        // ── the phase table ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Would `AscentBuilder.Build()` receive at least ONE phase from this stage table — i.e. can it
        /// be called without throwing on `_phases[0]`?
        ///
        /// ⭐ **THIS IS A FAITHFUL MIRROR OF THE VENDORED LOOP**, `MechJebModulePSGGlueBall.cs:245-303`,
        /// and it is written to match it statement for statement:
        ///   • it walks `VacStats` BACKWARDS, from `Count-1` down to 0, as the source does;
        ///   • `if (kspStage LT LastStage) break;`   — the source's break, not a continue;
        ///   • `if (deltaV LT MinDeltaV) continue;`  — the source's "skip sep motors" skip;
        ///   • anything reaching the bottom of the body reaches `AddStage`, so ONE is enough.
        /// ⚠ **DELIBERATELY CONSERVATIVE:** the source's coast branch can add a phase this ignores, so
        /// a `true` here is always safe and a `false` may merely be early. Erring toward "wait" is the
        /// correct direction on a pad.
        /// ⛔ **IF MECHJEB IS RE-PINNED (§B12.1), RE-READ THAT LOOP AND RE-READ THIS.** A mirror that
        /// has silently drifted from its original is worse than no mirror at all.
        /// </summary>
        public static bool WouldBuildAPhase(int[] kspStage, double[] deltaV, int lastStage, double minDeltaV)
        {
            if (kspStage == null || deltaV == null) return false;
            int n = kspStage.Length < deltaV.Length ? kspStage.Length : deltaV.Length;
            for (int i = n - 1; i >= 0; i--)
            {
                if (kspStage[i] < lastStage) break;      // the glue ball's own break
                if (deltaV[i] < minDeltaV) continue;     // the glue ball's own skip
                return true;                             // this one reaches AddStage
            }
            return false;
        }
    }
}
