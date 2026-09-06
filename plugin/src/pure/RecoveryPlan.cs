// DragonScreen — RecoveryPlan  (PURE: §B16.7's focus protocol, as a decision — when the physics ranges go
// wide, how long they stay wide, and when they come back)
// ============================================================================================
// Register **W9**. This is the pure half of `src/MissionConductor.cs`'s recovery machine, and it exists
// because the thing it decides is decidable without the game: given "is the arm on", "is the stack up",
// "does the booster host hold a booster" and "are the ranges already wide", the next stage and the range
// action follow. The glue then calls `RangeExtender.Enable/Disable` and nothing else.
//
// ---- WHAT THIS IS *NOT*, AND THE DISTINCTION IS THE WHOLE DESIGN ------------------------------------
// ⛔ It does not fly the booster. `src/BoosterHost.cs` + `pure/BoosterDescent.cs` fly the booster, on
// their own vessel, with their own steering law (§B16.1) — and they already own §B16.7 steps 2 and 3
// (the booster lands UNFOCUSED; `BoosterHostPlan.LandedSettleS = 10 s` is step 3's settle, and the host
// releases on it). This file owns the steps the host is explicitly forbidden to own, and says why in its
// own words at `src/BoosterHost.cs:100`:
//
//      "NEVER WIDEN PHYSICS RANGES (that is global, and therefore the Dragon's too — register W9)."
//
// `RangeExtender.Enable` writes `vesselRanges` on EVERY loaded vessel, the Dragon included. A host that
// touches exactly one vessel may not make a call with that reach, so the reach lives here, in the one
// machine that is allowed to think about both vessels at once.
//
// ---- §B16.7, STEP BY STEP, AND WHO HAS EACH ONE -----------------------------------------------------
//   1. PRE expands the range so the booster stays loaded at separation ............ THIS FILE (Extend)
//   2. The booster lands UNFOCUSED, flown by its own core ........................ BoosterHost
//   3. +10 s settle after touchdown ............................................. BoosterHostPlan.LandedSettleS
//   4. Auto-recover the booster ................................................. ⛔ NOT BUILT — see below
//   5. PRE OFF; the default range restored ...................................... THIS FILE (Restore)
//
// ⛔ **STEP 4 IS NOT IMPLEMENTED, AND THAT IS A STATED GAP, NOT AN OVERSIGHT.** "Auto-recover" in the
// stock sense (remove the landed vessel and credit its recovery) has **no verified-real source anywhere
// in this repo**: no doc records which KSP call performs it for a NON-ACTIVE vessel in the flight scene,
// and C1.15 / §1.4 forbid inventing the binding. Guessing it wrong has two outcomes and both are bad —
// a silent no-op, or a vessel destroyed mid-flight. So the protocol runs 1-2-3-5 and the booster is left
// standing where it landed, which is benign (it unloads with the ranges and sits in the tracking station
// as any landed craft does). `RecoveryStage.Done` is therefore named for the RANGE lifecycle finishing,
// never for a recovery having been credited. The question is posed in W9's register line.
//
// ---- ⛔ FOCUS NEVER LEAVES THE UPPER STAGE (§B16.7's headline rule) ---------------------------------
// The pre-deletion `MissionConductor` had a `FocusOn` / `ForceSetActiveVessel` path and an on-screen
// caption promising the crew that arming this "sacrifices the Dragon orbit that flight". §B16.7 SETTLED
// that design away on 2026-09-03: focus stays on the upper stage, and no orbit is sacrificed. There is
// no focus verb in this file's vocabulary at all — `RangeAct` has two verbs plus "leave it alone", and
// none of the three moves a camera — because the safest place to make a superseded design unreachable
// is the type system.
//
// ---- THE ACCEPTED RISK THIS MACHINE BOUNDS ----------------------------------------------------------
// §B16.7 states it and does not bury it: an unfocused booster at ~1500 km is well past PRE's own >100 km
// caution, and KSP gives the ACTIVE vessel the floating-origin precision — so the vehicle we are landing
// accurately is the one on the coarser physics. The protocol BOUNDS that risk rather than removing it:
// the ranges go wide only while the landing needs them and are restored the moment the host lets go.
// That bounding is precisely what `Recovering → Done → Restore` below is for. Until a recorded flight
// says otherwise the risk is open and documented; nobody should be surprised by it later.
//
// Pure + allocation-free (the annunciations are literals) + headless-tested by `test/RecoveryPlanTest.cs`.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>Where the range lifecycle is. Terminal at `Done` for the flight — a scene change resets
    /// it, and the crew cycling the arm off and on re-arms it, but a completed recovery does not
    /// re-arm itself off a second separated craft.</summary>
    public enum RecoveryStage : byte
    {
        /// <summary>Not armed, or the stack is not up yet. Stock ranges.</summary>
        Idle = 0,
        /// <summary>The stack is airborne and the ranges are WIDE, waiting for separation. Step 1.</summary>
        Armed = 1,
        /// <summary>The booster host holds a separated booster and is flying it down. Steps 2-3.</summary>
        Recovering = 2,
        /// <summary>The host let go (landed+settled, lost, or the backstop tripped). Ranges restored. Step 5.</summary>
        Done = 3,
    }

    /// <summary>What the glue should do to `RangeExtender` this tick. Deliberately only two verbs plus
    /// "leave it alone" — there is no focus verb (§B16.7).</summary>
    public enum RangeAct : byte
    {
        /// <summary>Touch nothing.</summary>
        None = 0,
        /// <summary>`RangeExtender.Enable(PreRangeM)`. Re-issued on a stage change on purpose: `Enable`
        /// walks the CURRENT vessel list, and the separated booster is a vessel that did not exist when
        /// the arming call ran, so the arming call could not have widened it.</summary>
        Extend = 1,
        /// <summary>`RangeExtender.Disable()` — stock ranges back on every vessel.</summary>
        Restore = 2,
    }

    /// <summary>Everything the decision reads. The glue fills it from KSP; a test fills it by hand.</summary>
    public struct RecoveryInputs
    {
        /// <summary>The DISPLAY-tab arm (`MissionConductor.AutoRecoverBooster`).</summary>
        public bool Armed;
        /// <summary>The active vessel carries a pod — i.e. it is the Dragon stack / upper stage, the one
        /// focus must never leave. Measured by the glue with `VehicleParts.IsPod`.</summary>
        public bool ActiveIsStack;
        /// <summary>The active vessel is off the pad (FLYING / SUB_ORBITAL / ORBITING / ESCAPING).</summary>
        public bool ActiveAirborne;
        /// <summary>`BoosterHost.Engaged` — the booster host has BOUND a separated booster and is
        /// driving it. This is the one signal that says a recovery is really under way; the conductor
        /// does not do its own booster search, because a second search could disagree with the host's.</summary>
        public bool BoosterBound;
        /// <summary>`RangeExtender.Active` — are the ranges wide right now.</summary>
        public bool RangeExtended;
        /// <summary>Seconds since the recovery began, for the backstop below.</summary>
        public double RecoveringForS;
        /// <summary>The hard backstop: give the range lifecycle up after this long even if the host is
        /// somehow still bound. Supplied by the caller (`MissionConductor.RecoveryTimeoutS`), because
        /// §1.4/C1.15 keep tunable numbers out of the pure rule.</summary>
        public double TimeoutS;
    }

    /// <summary>The answer: the next stage, what to do to the ranges, and the reason in the caller's
    /// own log voice. `Note` is null when nothing happened worth a line — a per-frame machine that
    /// annunciates every tick floods KSP.log (the S40 lesson).</summary>
    public struct RecoveryDecision
    {
        public RecoveryStage Stage;
        public RangeAct Range;
        public string Note;
    }

    public static class RecoveryPlan
    {
        /// <summary>The whole machine. Total over `RecoveryStage` — every stage has a case and every
        /// case returns — so there is no "and otherwise" path to fall through.</summary>
        public static RecoveryDecision Next(RecoveryStage stage, RecoveryInputs i)
        {
            // ---- THE DISARM, CHECKED FIRST AND ABOVE EVERY STAGE ----------------------------------
            // The arm is a live control the crew can flip mid-flight, and the ONE thing it must always
            // be able to do is put the physics back. Checking it inside the stage switch would leave
            // stages where a disarm could not reach the ranges; checking it here means it always can.
            if (!i.Armed)
                return Make(RecoveryStage.Idle, i.RangeExtended ? RangeAct.Restore : RangeAct.None,
                            i.RangeExtended ? "AUTO BOOSTER RECOVERY disarmed — stock physics ranges restored" : null);

            switch (stage)
            {
                case RecoveryStage.Idle:
                    // Step 1. ARM BEFORE SEPARATION, not at it: `Enable` has to have run while the
                    // booster is still part of the stack, so that the instant KSP splits the stack the
                    // new vessel is created into a world whose ranges are already wide. Waiting for the
                    // booster to exist is the failure the whole step exists to avoid.
                    if (i.ActiveIsStack && i.ActiveAirborne)
                        return Make(RecoveryStage.Armed, RangeAct.Extend,
                                    "booster recovery ARMED — physics ranges wide before separation (§B16.7 step 1)");
                    // ⭐ IDLE IS ALSO THE SELF-HEAL, AND THE SWEEP IN `RecoveryPlanTest` IS WHAT FOUND IT.
                    // This case used to return `None` unconditionally, which left one reachable way to
                    // strand the ranges wide with nobody recovering: any path that lands back on Idle
                    // while they are still extended — an Armed-›-Idle whose `Disable` was swallowed by
                    // `RangeExtender`'s own guard, or a scene that resumes Idle with wide ranges set. Wide
                    // ranges carry §B16.7's accepted risk, so no stage may hold them without a reason, and
                    // "Idle" is the absence of a reason.
                    return Make(RecoveryStage.Idle, i.RangeExtended ? RangeAct.Restore : RangeAct.None,
                                i.RangeExtended
                                    ? "booster recovery idle with wide physics ranges — stock ranges restored"
                                    : null);

                case RecoveryStage.Armed:
                    if (i.BoosterBound)
                        // Re-Extend: see `RangeAct.Extend`. The booster did not exist at arming time.
                        return Make(RecoveryStage.Recovering, RangeAct.Extend,
                                    "booster recovery RUNNING — the host holds a separated booster, "
                                    + "flying it down UNFOCUSED (§B16.7 step 2)");
                    // ⛔ THE STACK CAME BACK DOWN WITHOUT EVER SEPARATING — an abort, a revert, a pad
                    // scrub. Wide ranges are not free (phantom forces, §B16.7's accepted risk), so they
                    // do not outlive the reason for them.
                    if (!i.ActiveAirborne)
                        return Make(RecoveryStage.Idle, i.RangeExtended ? RangeAct.Restore : RangeAct.None,
                                    "booster recovery DISARMED — the stack is no longer airborne and nothing separated");
                    // Hold. Re-Extend only if something else put the ranges back (a scene reload).
                    return Make(RecoveryStage.Armed, i.RangeExtended ? RangeAct.None : RangeAct.Extend, null);

                case RecoveryStage.Recovering:
                    // Step 5. THE HOST LETTING GO IS THE END CONDITION, and it is the right one because
                    // the host's own release already covers every way a recovery can finish:
                    // `BoosterHostPlan.StopReason` returns Landed (down AND settled +10 s — step 3),
                    // Destroyed, Unloaded, BecameActive or BindLost. Re-deriving "is it down" here would
                    // be a second opinion that could disagree with the one actually driving the vessel.
                    if (!i.BoosterBound)
                        return Make(RecoveryStage.Done, i.RangeExtended ? RangeAct.Restore : RangeAct.None,
                                    "booster recovery COMPLETE — the host released; stock physics ranges restored "
                                    + "(§B16.7 step 5). ⛔ Step 4 (auto-recover) is not built — see RecoveryPlan's header.");
                    // The backstop. A host that stays bound forever would hold the ranges wide forever,
                    // and wide ranges are the thing with the accepted risk attached.
                    if (i.TimeoutS > 0.0 && i.RecoveringForS > i.TimeoutS)
                        return Make(RecoveryStage.Done, i.RangeExtended ? RangeAct.Restore : RangeAct.None,
                                    "booster recovery TIMED OUT — backstop tripped; stock physics ranges restored");
                    return Make(RecoveryStage.Recovering, RangeAct.None, null);

                default:  // RecoveryStage.Done
                    // Terminal for the flight. It still RESTORES if it ever finds the ranges wide again,
                    // because "the ranges are wide and nobody is recovering" is the one state this file
                    // exists to make impossible.
                    return Make(RecoveryStage.Done, i.RangeExtended ? RangeAct.Restore : RangeAct.None, null);
            }
        }

        static RecoveryDecision Make(RecoveryStage s, RangeAct r, string note)
        {
            RecoveryDecision d;
            d.Stage = s; d.Range = r; d.Note = note;
            return d;
        }

        /// <summary>Is the crew being shown a recovery in progress? Exactly `Recovering` — `Armed` is a
        /// physics-range state with no booster in it yet, and lighting a lamp there would claim a
        /// recovery that has not started (§14.4(a)).</summary>
        public static bool InProgress(RecoveryStage s) { return s == RecoveryStage.Recovering; }
    }
}
